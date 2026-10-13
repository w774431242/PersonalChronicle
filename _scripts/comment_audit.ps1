# Comment coverage audit for PersonalChronicle sources. ASCII only.
$ErrorActionPreference = 'Continue'
$src = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Source\PersonalChronicle'

$files = Get-ChildItem $src -Recurse -Filter *.cs | Sort-Object FullName
$totalLines = 0; $totalComments = 0; $zero = New-Object System.Collections.Generic.List[string]
$rows = New-Object System.Collections.Generic.List[object]

foreach ($f in $files) {
    $lines = [System.IO.File]::ReadAllLines($f.FullName, [System.Text.Encoding]::UTF8)
    $n = 0; $c = 0; $inBlock = $false
    foreach ($l in $lines) {
        $n++
        $t = $l.Trim()
        if ($inBlock) { $c++; if ($t -like '*/') { $inBlock = $false }; continue }
        if ($t -eq '') { continue }
        if ($t.StartsWith('/*')) { $c++; if ($t -notlike '*/') { $inBlock = $true }; continue }
        if ($t.StartsWith('//')) { $c++; continue }
    }
    $totalLines += $n; $totalComments += $c
    $rel = $f.FullName.Substring($src.Length + 1)
    if ($c -eq 0) { $zero.Add($rel) }
    $rows.Add([pscustomobject]@{ File = $rel; Lines = $n; Cmt = $c })
}

Write-Host ('files: ' + $files.Count)
Write-Host ('total lines: ' + $totalLines)
Write-Host ('comment lines: ' + $totalComments)
if ($totalLines -gt 0) {
    Write-Host ('coverage: ' + [math]::Round(100.0 * $totalComments / $totalLines, 1) + '%')
}
Write-Host '=== files with ZERO comments ==='
$zero | ForEach-Object { Write-Host ('  ' + $_) }

Write-Host '=== 10 files with LOWEST comment ratio (>=150 lines) ==='
$rows | Where-Object { $_.Lines -ge 150 } |
    Sort-Object { if ($_.Lines -gt 0) { $_.Cmt / $_.Lines } else { 0 } } |
    Select-Object -First 10 |
    ForEach-Object { Write-Host ('  ' + [math]::Round(100.0 * $_.Cmt / $_.Lines, 1).ToString().PadLeft(5) + '%  ' + $_.Cmt.ToString().PadLeft(4) + '/' + $_.Lines.ToString().PadLeft(5) + '  ' + $_.File) }

Write-Host '=== guardrail keywords in comments ==='
$enc = New-Object System.Text.UTF8Encoding($false)
$all = Get-ChildItem $src -Recurse -Filter *.cs | ForEach-Object { [System.IO.File]::ReadAllText($_.FullName, $enc) }
$joined = ($all -join "`n")
foreach ($k in @('Patch 档案', '铁律', 'GOV-', 'ARC-013', '单一真源', '禁止', '勿改', '不可反向', '必须')) {
    $cnt = ([regex]::Matches($joined, [regex]::Escape($k))).Count
    Write-Host ('  ' + $k.PadRight(12) + ' x' + $cnt)
}
