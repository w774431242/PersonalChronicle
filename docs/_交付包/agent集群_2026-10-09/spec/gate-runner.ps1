<#
.SYNOPSIS
  GATE-DELTA 自动校验：比对 git 实际改动与变更标注，拦截遗漏/超范围/调试残留。
.DESCRIPTION
  依据 GOV-013 / GATE-DELTA：变更标注必须覆盖全部实际改动，且范围 ⊆ Change Summary。
  本脚本把"自觉合规"升级为"护栏合规"：
    1) 取 git 实际改动文件集 A（含未提交/未跟踪）。
    2) 解析变更标注 YAML front matter 得到标注文件集 B。
    3) 校验 A⊆B 且 B⊆A（后缀匹配，容忍标注写子路径）。
    4) 扫描调试残留（仅源码类文件：TODO/FIXME/HACK/XXX:/Console.Write/Debug.Log/Debugger.Break）。
  默认仅出报告（exit 0）；-Blocking 时遇到 BLOCKED 退出码 2。
.EXAMPLE
  pwsh gate-runner.ps1 -Annotation assets/change-annotation-template.md
  pwsh gate-runner.ps1 -Base origin/main -Annotation CHG-001.md -Blocking
  # 存在历史未提交工作、只想校验本次变更时，用 -Exclude 声明范围外前缀：
  pwsh gate-runner.ps1 -Base HEAD -Annotation CHG-002.md `
      -Exclude 'docs/','_research/','Tools/' -Blocking
#>
param(
    [string]$Base = 'origin/main',
    [Parameter(Mandatory = $true)][string]$Annotation,
    [string]$Repo = (Get-Location).Path,
    # 范围外路径前缀/子串（大小写不敏感）。用于「仓库存在与本变更无关的历史未提交工作」
    # 的场景：A⊆B 且 B⊆A 的严格互含校验无法表达"预先存在的无关改动"，
    # 强行要求标注覆盖全部历史改动会迫使标注失真。声明 -Exclude 后，
    # 这些路径既不参与 missing 判定，也不参与 phantom 判定，但在报告中单独列出以供复核。
    [string[]]$Exclude = @(),
    # 从文件读取范围外清单（每行一条，支持 # 注释）。用于历史遗留文件过多、
    # 无法逐条写进命令行参数的场景（实测：某仓库历史未提交工作达 800+ 文件）。
    [string]$ExcludeFromFile = '',
    [switch]$Blocking
)

$ErrorActionPreference = 'Stop'
$problems = [System.Collections.Generic.List[string]]::new()
# 被 -Exclude 排除的文件（仅记账，不判错）
$excludedFiles = [System.Collections.Generic.List[string]]::new()

# 合并 -Exclude 与 -ExcludeFromFile
$excludeList = [System.Collections.Generic.List[string]]::new()
foreach ($e in $Exclude) { if ($e) { $null = $excludeList.Add($e) } }
if ($ExcludeFromFile -and (Test-Path -LiteralPath $ExcludeFromFile)) {
    foreach ($line in (Get-Content -LiteralPath $ExcludeFromFile)) {
        $t = $line.Trim()
        if ($t -and -not $t.StartsWith('#')) { $null = $excludeList.Add($t) }
    }
}

# 1) git 实际改动文件集 A
$gitFiles = [System.Collections.Generic.List[string]]::new()
$Repo = Resolve-Path $Repo
Push-Location $Repo
try {
    # core.quotePath=false：中文等非 ASCII 路径按原样输出，避免引号/八进制转义导致与标注比对不一致。
    # ⚠ 仅加 -c core.quotePath=false 还不够：PowerShell 捕获外部进程 stdout 时默认用系统 ANSI
    #   代码页（简体中文 Windows = GBK/936）解码，会把 git 输出的 UTF-8 中文路径整体弄乱，
    #   导致「中文路径文件永远无法与标注匹配」——既误报 missing 又误报 phantom。
    #   实测（2026-09-18）：232 个中文路径全部失配。修法＝显式把控制台输入/输出编码设为 UTF-8。
    $prevOutEnc = [Console]::OutputEncoding
    $prevInEnc = [Console]::InputEncoding
    try {
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        [Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
        $OutputEncoding = [System.Text.UTF8Encoding]::new($false)

        $gitRaw = @()
        $gitRaw += (git -c core.quotePath=false diff --name-only "$Base" 2>$null)
        $gitRaw += (git -c core.quotePath=false diff --name-only 2>$null)
        $gitRaw += (git -c core.quotePath=false ls-files --others --exclude-standard 2>$null)
    } finally {
        [Console]::OutputEncoding = $prevOutEnc
        [Console]::InputEncoding = $prevInEnc
    }
    $gitRaw -split "`n" | Where-Object { $_ } | ForEach-Object { $gitFiles.Add($_.Trim().Trim('"')) }
} finally { Pop-Location }

# 过滤生成物/日志类路径，避免污染 missing/phantom 与残留扫描
$skipSeg = @('.log', '.cache', '.dll', '.pdb', '.bak', '/obj/', '\obj\', '/bin/', '\bin\', '_backups/', '_backup/', '/.vs/', '\.vs\')
function ShouldSkip($p) {
    $pl = $p.ToLower()
    foreach ($s in $skipSeg) { if ($pl.Contains($s)) { return $true } }
    return $false
}
$gitSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
function IsExcluded($p) {
    if (-not $excludeList -or $excludeList.Count -eq 0) { return $false }
    $pl = $p.ToLower().Replace('\', '/')
    foreach ($e in $excludeList) {
        if (-not $e) { continue }
        $el = $e.ToLower().Replace('\', '/').TrimEnd('/')
        # 路径边界匹配：全等，或作为目录前缀（$el/…）。
        # ⚠ 不再用裸 Contains —— 旧实现会把 `docs/x/README.md` 当成清单里裸 `README.md`
        #   的子串而误排除（2026-10-06 实测：CHG-20261006-RULEINDEX 批次 README.md 被判
        #   「标注中存在 git 无对应改动的文件」）。修复后判定集不受同名子串串扰。
        if ($pl -eq $el -or $pl.StartsWith($el + '/')) { return $true }
    }
    return $false
}
$gitFiles | Where-Object { $_ -and -not (ShouldSkip $_) } | ForEach-Object {
    if (IsExcluded $_) { $null = $excludedFiles.Add($_) } else { $null = $gitSet.Add($_) }
}
# 排除标注文件自身（若位于仓库内，会被当作未跟踪改动导致误报）
try {
    $annFull = Resolve-Path -LiteralPath $Annotation
    $repoFull = Resolve-Path -LiteralPath $Repo
    if ($annFull.Path.StartsWith($repoFull.Path, [System.StringComparison]::OrdinalIgnoreCase)) {
        # ⚠ 转正斜杠：git 改动集里是 `/`，若保持 `\` 会导致下面 Remove 静默失配（2026-10-06 修复）
        $annRel = $annFull.Path.Substring($repoFull.Path.Length).TrimStart('\', '/').Replace('\', '/')
        $null = $gitSet.Remove($annRel)
        $null = $excludedFiles.Remove($annRel)
    }
} catch { }

# 2) 解析变更标注 front matter（YAML，手写解析，避免依赖 ConvertFrom-Yaml）
if (-not (Test-Path -LiteralPath $Annotation)) { Write-Error "Annotation not found: $Annotation"; exit 1 }
$raw = Get-Content -LiteralPath $Annotation -Raw
$annSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$annType = @{}
if ($raw -match '(?s)^---\s*\n(.*?)\n---\s*\n') {
    $yaml = $Matches[1] -split "`n"
    $cur = $null
    foreach ($line in $yaml) {
        if ($line -match '^\s*-\s*path:\s*(.+?)\s*$') {
            $cur = $Matches[1].Trim().Trim('"').Trim("'")
            $null = $annSet.Add($cur); if (-not $annType.ContainsKey($cur)) { $annType[$cur] = 'M' }
        } elseif ($cur -and $line -match '^\s+type:\s*(.+?)\s*$') {
            $annType[$cur] = $Matches[1].Trim().Trim('"').Trim("'")
        }
    }
}
# 退化：若无 front matter，扫描 Markdown 表格第一列（路径单元格）
if ($annSet.Count -eq 0) {
    foreach ($line in ($raw -split "`n")) {
        if ($line -match '^\|\s*([^\|]+\.(cs|xml|md|png|json|asset))\s*\|') {
            $null = $annSet.Add($Matches[1].Trim().Trim('"').Trim("'"))
        }
    }
}

# 3) 后缀匹配比对
function Match-Suffix($a, $b) {
    return $a -eq $b -or
    $a.EndsWith($b, [StringComparison]::OrdinalIgnoreCase) -or
    $b.EndsWith($a, [StringComparison]::OrdinalIgnoreCase)
}
$missing = [System.Collections.Generic.List[string]]::new()  # git 有但标注无
foreach ($g in $gitSet) {
    $hit = $false
    foreach ($a in $annSet) { if (Match-Suffix $g $a) { $hit = $true; break } }
    if (-not $hit) { $missing.Add($g) }
}
$phantom = [System.Collections.Generic.List[string]]::new()   # 标注有但 git 无（非删除类型）
foreach ($a in $annSet) {
    $hit = $false
    foreach ($g in $gitSet) { if (Match-Suffix $g $a) { $hit = $true; break } }
    if (-not $hit -and $annType[$a] -ne 'D') { $phantom.Add($a) }
}
if ($missing.Count) { $problems.Add("标注遗漏实际改动文件: " + ($missing -join ', ')) }
if ($phantom.Count) { $problems.Add("标注中存在 git 无对应改动的文件(非删除): " + ($phantom -join ', ')) }

# 4) 调试残留扫描（仅源码类文件；仅注释上下文中的 TODO/FIXME/HACK，排除构建/工具脚本目录，避免误报）
#   ⚠ 不扫 .md：变更标注/设计文档会「引用」这些 token（例如上一轮标注里写的
#     "残留扫描结论：未见 Console.WriteLine"），扫 .md 必然产生自指假阳性。
#     实测（2026-09-18）：6 处命中里 2 处来自 docs/*.md 的自指文本。文档残留改由人工复核。
$residue = @('(?://|#|\*|/\*)\s*TODO', '(?://|#|\*|/\*)\s*FIXME', '(?://|#|\*|/\*)\s*HACK', 'XXX:', 'Console\.Write', 'Debug\.Log', 'Debugger\.Break', 'Debug\.Assert')
$srcExt = @('.cs', '.xml', '.ps1', '.py', '.js', '.ts', '.jsx', '.html', '.css', '.json')
$residueSkip = @('_scripts/', '/tools/', '/scripts/', '/ruif/', '_research/', '_verify_tmp/')
$hits = @()
foreach ($g in $gitSet) {
    $gl = $g.ToLower()
    $skip = $false
    foreach ($s in $residueSkip) { if ($gl.Contains($s)) { $skip = $true; break } }
    if ($skip) { continue }
    $fp = Join-Path $Repo $g
    if ((Test-Path -LiteralPath $fp -PathType Leaf) -and ($srcExt -contains [System.IO.Path]::GetExtension($g).ToLower())) {
        $hits += Select-String -LiteralPath $fp -Pattern ($residue -join '|') |
            ForEach-Object { "$g :$($_.LineNumber) $($_.Line.Trim())" }
    }
}
if ($hits.Count) {
    $shown = if ($hits.Count -gt 20) { $hits[0..19] + ("  ... 及另外 $($hits.Count - 20) 处") } else { $hits }
    $problems.Add("发现调试/待办残留（共 $($hits.Count) 处）:`n" + ($shown -join "`n"))
}
# 5) 结果
if ($excludedFiles.Count) {
    Write-Host ("GATE-DELTA: 范围外（-Exclude 声明，不参与判定）共 $($excludedFiles.Count) 个文件。") -ForegroundColor DarkGray
    if ($excludedFiles.Count -le 20) {
        $excludedFiles | ForEach-Object { Write-Host ("   (out) " + $_) -ForegroundColor DarkGray }
    } else {
        $excludedFiles[0..4] | ForEach-Object { Write-Host ("   (out) " + $_) -ForegroundColor DarkGray }
        Write-Host ("   ... 及另外 $($excludedFiles.Count - 5) 个") -ForegroundColor DarkGray
    }
}
if ($problems.Count -eq 0) {
    Write-Host "GATE-DELTA: PASS — 标注覆盖 $($gitSet.Count) 个改动文件，无遗漏/超范围/残留。" -ForegroundColor Green
    exit 0
} else {
    Write-Host "GATE-DELTA: BLOCKED" -ForegroundColor Red
    $problems | ForEach-Object { Write-Host (" - " + $_) -ForegroundColor Yellow }
    if ($Blocking) { exit 2 } else { exit 0 }
}
