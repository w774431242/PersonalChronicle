# CHG-20261005-DATAAUDIT：Scribe 完整性静态扫描（只读，不修改任何文件）
#
# 用途：找出「有 ExposeData 的类里，声明了持久字段但没有对应 Scribe 调用」的**漏落盘**点。
#       漏一个持久字段 = 读档静默丢数据，是最隐蔽的一类缺陷 ——
#       本仓 2026-10-05 就是靠同类检查抓到 `runWindowCounts`（逐日机时窗）与
#       设备产值字段两处真实缺口（写入正确、ExposeData 漏写 ⇒ 读档即丢）。
#
# 用法（仓库根目录执行）：
#   pwsh -NoProfile -File _scripts\audit-scribe.ps1
#   pwsh -NoProfile -File _scripts\audit-scribe.ps1 -Root Source\PersonalChronicle -Out .codebuddy\tmp\scribe-audit.txt
#
# ⚠ **结果必须人工复核**：以下三类是已知误报，不是缺陷 ——
#     ① 属性（含 `=>` 表达式体）与 `readonly`/`static`/`const` 字段：已在本脚本过滤；
#     ② **枚举/int 互转写法**：`int x = (int)Field; Scribe_Values.Look(ref x, "field"); Field = (X)x;`
#        —— 字段名不出现在 `ref` 里，会误报（如 PawnRecord.ArchiveStatus）；
#     ③ 缓存 / 派生 / 会话态字段（组件引用、扫描指纹、节流累加器、失效标记）：
#        **正确做法就是不落盘**，读档重建（如 MapComponent 的 dutyPlan、CompAutoAgriculture 的 maxRing）。
#   ⇒ 判定顺序：先看是否有 int 互转、再问"丢了它读档后能否重建"，都排除才是真漏。
#
# 退出码：0 = 扫描完成（无论是否命中）；非 0 = 路径错误。

param(
    [string]$Root = 'Source\PersonalChronicle',
    [string]$Out  = '.codebuddy\tmp\scribe-audit.txt'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Root)) {
    Write-Error "扫描根目录不存在：$Root（请在仓库根目录执行）"
    exit 2
}

$lines = New-Object System.Collections.Generic.List[string]
$files = Get-ChildItem $Root -Recurse -File -Include *.cs
$scanned = 0
$hits = 0

foreach ($f in $files) {
    $text = Get-Content $f.FullName -Raw -Encoding UTF8
    if ($text -notmatch 'ExposeData') { continue }
    $scanned++

    # ① 已落盘的字段名（Scribe_*.Look(ref name, ...) 的第一参数）
    $scribed = @{}
    foreach ($m in [regex]::Matches($text, 'Scribe_[A-Za-z]+\.Look\s*(?:<[^>]*>)?\s*\(\s*ref\s+([A-Za-z_][\w\.]*)')) {
        $scribed[$m.Groups[1].Value] = $true
    }

    # ② 字段声明（跳过 const/static/readonly/方法/属性/[Unsaved]）
    $decls = New-Object System.Collections.Generic.List[object]
    $prevAttr = ''
    $ln = 0
    foreach ($raw in ($text -split "`n")) {
        $ln++
        $line = $raw.Trim()
        if ($line -match '^\[Unsaved\]') { $prevAttr = 'unsaved'; continue }
        if ($line -match '^\[(?!Unsaved)') { $prevAttr = 'attr'; continue }
        $isDecl = $line -match '^(private|protected|internal|public)\s+' -and
                  $line -match ';' -and
                  $line -notmatch '\b(const|static|readonly|event|delegate|void|class|struct|enum|interface)\b' -and
                  $line -notmatch '\(' -and
                  $line -match '([A-Za-z_]\w*)\s*(=[^;]*)?;'
        if ($isDecl) {
            $name = [regex]::Match($line, '([A-Za-z_]\w*)\s*(=[^;]*)?;').Groups[1].Value
            if ($name -and $name -notin @('return', 'get', 'set', 'value')) {
                $decls.Add([pscustomobject]@{
                    Line = $ln; Name = $name; Unsaved = ($prevAttr -eq 'unsaved'); Text = $line
                })
            }
        }
        if ($line -ne '') { $prevAttr = '' }
    }

    # ③ 求差
    $miss = @()
    foreach ($d in $decls) {
        if ($d.Unsaved) { continue }
        if ($scribed.ContainsKey($d.Name)) { continue }
        $miss += $d
    }
    if ($miss.Count -gt 0) {
        $hits++
        $rel = $f.FullName.Replace((Resolve-Path $Root).Path + '\', '')
        $lines.Add("--- $rel")
        foreach ($d in $miss) { $lines.Add(("  L{0,-5} {1}" -f $d.Line, $d.Text)) }
    }
}

$lines.Add("")
$lines.Add("扫描文件（含 ExposeData）：$scanned    命中文件：$hits（⚠ 需按文件头说明人工复核误报）")
$lines | Out-File -Encoding utf8 $Out
"SCANNED=$scanned  HIT_FILES=$hits  OUT=$Out"
