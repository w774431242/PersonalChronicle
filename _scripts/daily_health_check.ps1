#Requires -Version 5.1
<#
.SYNOPSIS
    PersonalChronicle 每日常规体检（按架构文件夹逐层扫描）。
.DESCRIPTION
    检查项：
      A. 规模      —— 各层文件数 / 总行数 / 超 800 行文件（ARC-013 红线）
      B. 方法长度  —— 启发式扫描 > 60 行方法（开发铁律 6.2）
      C. 分层依赖  —— 单向 UI -> Application -> Domain <- Data/Capture
      D. UI 规范   —— Archive 层裸 new Color / GUI.color 未配对 / 直读 ChronicleGameComponent
      E. XML 卫生  —— Defs/Languages/Patches/About 的 UTF-8 BOM
      F. 发布同步  —— 开发区 vs 发布区白名单文件哈希
    输出：docs/_daily/<yyyy-MM-dd>.md
#>
[CmdletBinding()]
param(
    [string]$DevRoot  = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统',
    [string]$PubRoot  = 'E:\SteamLibrary\steamapps\common\RimWorld\Mods\殖民地综合管理系统',
    [int]   $FileLineLimit = 800,
    [int]   $MethodLineLimit = 60
)

$ErrorActionPreference = 'Continue'
$srcRoot = Join-Path $DevRoot 'Source\PersonalChronicle'
$outDir  = Join-Path $DevRoot 'docs\_daily'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
$outFile = Join-Path $outDir ((Get-Date).ToString('yyyy-MM-dd') + '.md')

$report = New-Object System.Collections.Generic.List[string]
function W([string]$s = '') { $report.Add($s) }

# ---------- A. 规模 ----------
$Layers = @('Api', 'Application', 'Archive', 'Capture', 'Data', 'Domain', 'Effects')
$rootCs = Get-ChildItem -Path $srcRoot -Filter *.cs -File
$layerStats = @{}
foreach ($layer in $Layers) {
    $d = Join-Path $srcRoot $layer
    $files = @()
    if (Test-Path $d) { $files = Get-ChildItem -Path $d -Filter *.cs -File -Recurse }
    $total = 0; $big = @()
    foreach ($f in $files) {
        $n = (Get-Content -LiteralPath $f.FullName -Encoding UTF8).Count
        $total += $n
        if ($n -gt $FileLineLimit) { $big += [pscustomobject]@{ Name = $f.Name; Lines = $n } }
    }
    $layerStats[$layer] = [pscustomobject]@{ Files = $files.Count; Lines = $total; Big = $big }
}

# ---------- B. 方法长度（启发式） ----------
function Get-LongMethods {
    param([string]$Path, [int]$Limit)
    $lines = [System.IO.File]::ReadAllLines($Path)
    $hits = @()
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]
        if ($l -match '^\s*(public|private|internal|protected)\s' -and
            $l -match '\)\s*$' -and $l -notmatch '=>' -and $l -notmatch ';\s*$') {
            $depth = 0; $started = $false; $j = $i
            for (; $j -lt $lines.Count; $j++) {
                $body = $lines[$j] -replace '//.*$', '' -replace '"(\\.|[^"\\])*"', '""' -replace '/\*.*\*/', ' '
                foreach ($ch in $body.ToCharArray()) {
                    if ($ch -eq '{') { $depth++; $started = $true }
                    elseif ($ch -eq '}') { $depth-- }
                }
                if ($started -and $depth -le 0) { break }
            }
            $len = $j - $i + 1
            if ($len -gt $Limit) {
                $hits += [pscustomobject]@{ Line = ($i + 1); Len = $len; Sig = ($l.Trim() -replace '\s+', ' ') }
            }
        }
    }
    return $hits
}

# ---------- C. 分层依赖 ----------
$forbidden = @{
    'Domain'      = @('PersonalChronicle.Application', 'PersonalChronicle.Archive', 'PersonalChronicle.Capture', 'PersonalChronicle.Data')
    'Application' = @('PersonalChronicle.Archive', 'PersonalChronicle.Capture')
    'Archive'     = @('PersonalChronicle.Capture', 'PersonalChronicle.Data')
    'Capture'     = @('PersonalChronicle.Archive')
    'Data'        = @('PersonalChronicle.Archive', 'PersonalChronicle.Capture')
    'Effects'     = @('PersonalChronicle.Archive', 'PersonalChronicle.Data')
    'Api'         = @('PersonalChronicle.Capture', 'PersonalChronicle.Archive')
}

# ---------- D. UI 规范 ----------
# 统计前剔除注释与字符串字面量，避免把注释里的 "GUI.color" 计入。
function Strip-Comments([string]$t) {
    $t = [regex]::Replace($t, '/\*.*?\*/', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $t = [regex]::Replace($t, '//[^\r\n]*', '')
    $t = [regex]::Replace($t, '"(\\.|[^"\\])*"', '""')
    return $t
}
# 枚举方法体（大括号配平），返回 Line / Sig / Body
function Get-MethodBodies([string]$Path) {
    $lines = [System.IO.File]::ReadAllLines($Path)
    $list = @()
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]
        if ($l -match '^\s*(public|private|internal|protected)\s' -and
            $l -match '\)\s*$' -and $l -notmatch '=>' -and $l -notmatch ';\s*$') {
            $depth = 0; $started = $false; $j = $i
            for (; $j -lt $lines.Count; $j++) {
                $body = $lines[$j] -replace '//.*$', '' -replace '"(\\.|[^"\\])*"', '""'
                foreach ($ch in $body.ToCharArray()) {
                    if ($ch -eq '{') { $depth++; $started = $true }
                    elseif ($ch -eq '}') { $depth-- }
                }
                if ($started -and $depth -le 0) { break }
            }
            $len = $j - $i + 1
            $raw = ($lines[$i..$j] -join "`n")
            $list += [pscustomobject]@{ Line = ($i + 1); Len = $len; Sig = ($l.Trim() -replace '\s+', ' '); Body = (Strip-Comments $raw) }
        }
    }
    return $list
}

$uiNewColor = @(); $uiGuiColorLeak = @(); $uiDirectComp = @()
$archiveDir = Join-Path $srcRoot 'Archive'
$themeFiles = @('UITheme.cs', 'ArchiveUiStyle.cs')
if (Test-Path $archiveDir) {
    foreach ($f in Get-ChildItem -Path $archiveDir -Filter *.cs -File -Recurse) {
        if ($themeFiles -contains $f.Name) { continue }
        $txt = Strip-Comments ([System.IO.File]::ReadAllText($f.FullName))
        $nc = ([regex]::Matches($txt, 'new\s+Color\s*\(')).Count
        if ($nc -gt 0) { $uiNewColor += [pscustomobject]@{ Name = $f.Name; Count = $nc } }
        # 绘制态泄漏判定：方法内存在 GUI.color 赋值，但从未把旧值存进局部变量
        foreach ($m in (Get-MethodBodies $f.FullName)) {
            if ($m.Body -notmatch 'GUI\.color') { continue }
            $assign = ([regex]::Matches($m.Body, 'GUI\.color\s*(?<![=!<>])=')).Count
            $save   = ([regex]::Matches($m.Body, '=\s*GUI\.color\s*;')).Count
            if ($assign -gt 0 -and $save -eq 0) {
                $uiGuiColorLeak += [pscustomobject]@{ Name = $f.Name; Line = $m.Line; Sig = $m.Sig }
            }
        }
        if ($txt -match 'ChronicleGameComponent\.' -and $f.Name -notmatch '^Dev') {
            $uiDirectComp += $f.Name
        }
    }
}

# ---------- E. XML BOM ----------
$bomHits = @()
foreach ($d in @('Defs', 'Languages', 'Patches', 'About')) {
    $p = Join-Path $DevRoot $d
    if (-not (Test-Path $p)) { continue }
    foreach ($f in Get-ChildItem -Path $p -Include *.xml -File -Recurse) {
        $fs = [System.IO.File]::OpenRead($f.FullName)
        $b = New-Object byte[] 3
        [void]$fs.Read($b, 0, 3); $fs.Close()
        if ($b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF) {
            $bomHits += $f.FullName.Substring($DevRoot.Length + 1)
        }
    }
}

# ---------- F. 发布同步 ----------
$syncWhite = @(
    'Assemblies\PersonalChronicle.dll',
    'Assemblies\PersonalChronicle.pdb',
    'Languages\ChineseSimplified\Keyed\Archive.xml',
    'Languages\English\Keyed\Archive.xml'
)
$syncRows = @()
foreach ($rel in $syncWhite) {
    $a = Join-Path $DevRoot $rel
    $b = Join-Path $PubRoot $rel
    $ha = if (Test-Path $a) { (Get-FileHash -LiteralPath $a -Algorithm SHA256).Hash } else { 'MISSING' }
    $hb = if (Test-Path $b) { (Get-FileHash -LiteralPath $b -Algorithm SHA256).Hash } else { 'MISSING' }
    $syncRows += [pscustomobject]@{ Rel = $rel; Same = ($ha -eq $hb); Dev = $ha.Substring(0, 8); Pub = $hb.Substring(0, 8) }
}

# ---------------- 渲染报告 ----------------
W ('# PersonalChronicle 每日常规体检 — ' + (Get-Date).ToString('yyyy-MM-dd'))
W ''
W ('> 生成脚本：`_scripts\daily_health_check.ps1`　开发区：`' + $DevRoot + '`')
W ''
W '## A. 各层规模（红线：单文件 > 800 行）'
W ''
W '| 层 | 文件数 | 总行数 | 超红线文件 |'
W '|---|---:|---:|---|'
$sumFiles = 0; $sumLines = 0; $allBig = @()
foreach ($layer in $Layers) {
    $s = $layerStats[$layer]
    $sumFiles += $s.Files; $sumLines += $s.Lines
    $bigTxt = if ($s.Big.Count -eq 0) { '—' } else { (($s.Big | ForEach-Object { $_.Name + '(' + $_.Lines + ')' }) -join '；') }
    $allBig += $s.Big | ForEach-Object { [pscustomobject]@{ Layer = $layer; Name = $_.Name; Lines = $_.Lines } }
    W ('| ' + $layer + ' | ' + $s.Files + ' | ' + $s.Lines + ' | ' + $bigTxt + ' |')
}
$rootLines = ($rootCs | ForEach-Object { (Get-Content -LiteralPath $_.FullName -Encoding UTF8).Count } | Measure-Object -Sum).Sum
W ('| *(根文件)* | ' + $rootCs.Count + ' | ' + $rootLines + ' | — |')
W ('| **合计** | **' + ($sumFiles + $rootCs.Count) + '** | **' + ($sumLines + $rootLines) + '** | **' + $allBig.Count + ' 项** |')
W ''

W '## B. 超长方法（启发式，阈值 ' + $MethodLineLimit + ' 行，铁律 6.2）'
W ''
$longMethods = @()
foreach ($layer in $Layers) {
    $d = Join-Path $srcRoot $layer
    if (-not (Test-Path $d)) { continue }
    foreach ($f in Get-ChildItem -Path $d -Filter *.cs -File -Recurse) {
        $hits = Get-LongMethods -Path $f.FullName -Limit $MethodLineLimit
        foreach ($h in $hits) {
            $longMethods += [pscustomobject]@{ Layer = $layer; File = $f.Name; Line = $h.Line; Len = $h.Len; Sig = $h.Sig }
        }
    }
}
if ($longMethods.Count -eq 0) {
    W '无告警。'
} else {
    W '**各层分布：**'
    W ''
    W '| 层 | 超长方法数 | 涉及文件数 |'
    W '|---|---:|---:|'
    foreach ($layer in $Layers) {
        $sub = @($longMethods | Where-Object { $_.Layer -eq $layer })
        if ($sub.Count -eq 0) { continue }
        $nf = @($sub | ForEach-Object { $_.File } | Sort-Object -Unique).Count
        W ('| ' + $layer + ' | ' + $sub.Count + ' | ' + $nf + ' |')
    }
    W ''
    W '（> 120 行的方法视为高优先治理对象，详见下表前段）'
    W ''
    W '| 层 | 文件 | 行 | 方法长度 | 签名 |'
    W '|---|---|---:|---:|---|'
    foreach ($m in ($longMethods | Sort-Object Len -Descending | Select-Object -First 40)) {
        W ('| ' + $m.Layer + ' | ' + $m.File + ' | ' + $m.Line + ' | ' + $m.Len + ' | `' + $m.Sig + '` |')
    }
    if ($longMethods.Count -gt 40) { W ('（仅列前 40，共 ' + $longMethods.Count + ' 项）') }
}
W ''
W '> 说明：方法长度用大括号配平的启发式统计，字符串/字符字面量中的大括号已做剔除，但仍可能存在个别误报，告警项需人工复核后确认。'
W ''

W '## C. 分层依赖方向（单向 UI → Application → Domain ← Data/Capture）'
W ''
$depViol = @()
foreach ($layer in $Layers) {
    $d = Join-Path $srcRoot $layer
    if (-not (Test-Path $d)) { continue }
    foreach ($f in Get-ChildItem -Path $d -Filter *.cs -File -Recurse) {
        $txt = [System.IO.File]::ReadAllText($f.FullName)
        foreach ($ns in $forbidden[$layer]) {
            if ($txt -match ('using\s+' + [regex]::Escape($ns) + '\s*;')) {
                $depViol += [pscustomobject]@{ Layer = $layer; File = $f.Name; Ns = $ns }
            }
        }
    }
}
if ($depViol.Count -eq 0) { W '**无违规**（各层 using 方向合规）。' }
else {
    $devRows = @($depViol | Where-Object { $_.File -match '^Dev' })
    $prodRows = @($depViol | Where-Object { $_.File -notmatch '^Dev' })
    W ('- 开发调试工具（`Dev*` 前缀，历史约定豁免）：**' + $devRows.Count + '** 处')
    W ('- **生产代码违规：' + $prodRows.Count + '** 处（下方表格）')
    W ''
    W '| 层 | 文件 | 违规 using | 分类 |'
    W '|---|---|---|---|'
    foreach ($v in ($prodRows + $devRows)) {
        W ('| ' + $v.Layer + ' | ' + $v.File + ' | `' + $v.Ns + '` | ' + $(if ($v.File -match '^Dev') { 'Dev 工具' } else { '**生产**' }) + ' |')
    }
}
W ''

W '## D. Archive（UI 表现层）规范'
W ''
W ('- 裸用 `new Color(`（排除 UITheme/ArchiveUiStyle）：**' + $uiNewColor.Count + ' 处**' + $(if ($uiNewColor.Count) { ' → ' + (($uiNewColor | ForEach-Object { $_.Name + '(' + $_.Count + ')' }) -join '、') } else { '' }))
W ('- `GUI.color` 赋值但未保存旧值（疑似绘制态泄漏）：**' + $uiGuiColorLeak.Count + ' 处**')
if ($uiGuiColorLeak.Count -gt 0) {
    W ''
    W '| 文件 | 行 | 方法 |'
    W '|---|---:|---|'
    foreach ($g in $uiGuiColorLeak) { W ('| ' + $g.Name + ' | ' + $g.Line + ' | `' + $g.Sig + '` |') }
}
W ''
W ('- 非 Dev 文件直读 `ChronicleGameComponent`：**' + $uiDirectComp.Count + '**' + $(if ($uiDirectComp.Count) { ' → ' + ($uiDirectComp -join '、') } else { '' }))
W ''

W '## E. XML 卫生（UTF-8 BOM 检查，铁律 1.1）'
W ''
if ($bomHits.Count -eq 0) { W 'Defs / Languages / Patches / About 全部 XML 无 BOM，合规。' }
else { foreach ($b in $bomHits) { W ('- ⚠ ' + $b) } }
W ''

W '## F. 开发区 → 发布区 同步一致性'
W ''
W '| 文件 | 一致 | 开发区 SHA256(前8) | 发布区 SHA256(前8) |'
W '|---|---|---|---|---|'
foreach ($r in $syncRows) {
    W ('| `' + $r.Rel + '` | ' + $(if ($r.Same) { 'OK' } else { '**不一致**' }) + ' | ' + $r.Dev + ' | ' + $r.Pub + ' |')
}
W ''

# Defs 目录级同步（数量 + 逐文件哈希差异）
$defsDiff = @()
$devDefs = Join-Path $DevRoot 'Defs'
$pubDefs = Join-Path $PubRoot 'Defs'
if ((Test-Path $devDefs) -and (Test-Path $pubDefs)) {
    $devMap = @{}; foreach ($f in Get-ChildItem $devDefs -Include *.xml -File -Recurse) { $devMap[$f.FullName.Substring($devDefs.Length + 1)] = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash }
    $pubMap = @{}; foreach ($f in Get-ChildItem $pubDefs -Include *.xml -File -Recurse) { $pubMap[$f.FullName.Substring($pubDefs.Length + 1)] = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash }
    foreach ($k in $devMap.Keys) {
        if (-not $pubMap.ContainsKey($k)) { $defsDiff += "缺于发布区：$k" }
        elseif ($pubMap[$k] -ne $devMap[$k]) { $defsDiff += "哈希不一致：$k" }
    }
    foreach ($k in $pubMap.Keys) { if (-not $devMap.ContainsKey($k)) { $defsDiff += "发布区多出：$k" } }
    W ('Defs 目录：开发区 ' + $devMap.Count + ' 个 XML / 发布区 ' + $pubMap.Count + ' 个，差异 **' + $defsDiff.Count + '** 项')
    foreach ($d in $defsDiff) { W ('- ' + $d) }
}
W ''
W '---'
W ''
W ('体检完成时间：' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))

[System.IO.File]::WriteAllLines($outFile, $report, (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("REPORT=" + $outFile)
Write-Host ("FILES=" + ($sumFiles + $rootCs.Count) + " LINES=" + ($sumLines + $rootLines))
Write-Host ("BIGFILES=" + $allBig.Count + " LONGMETHODS=" + $longMethods.Count + " DEPV=" + $depViol.Count + " BOM=" + $bomHits.Count + " DEFSDIFF=" + $defsDiff.Count)
