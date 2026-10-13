$ErrorActionPreference = 'Stop'
$src = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Source\PersonalChronicle'
$files = Get-ChildItem -Path $src -Recurse -Filter *.cs
"==== TOTAL_CS={0}" -f $files.Count

"==== LAYER FILE COUNTS"
Get-ChildItem -Path $src -Directory | ForEach-Object {
    $c = (Get-ChildItem -Path $_.FullName -Recurse -Filter *.cs).Count
    "{0}: {1}" -f $_.Name, $c
}

"==== FILES > 600 LINES (red-line = 800, render-dense <= 1200)"
foreach ($f in $files) {
    $n = (Get-Content -Path $f.FullName -Encoding UTF8).Count
    if ($n -gt 600) {
        $rel = $f.FullName.Substring($src.Length + 1)
        "{0,5}  {1}" -f $n, $rel
    }
}

"==== DOMAIN LAYER referencing engine (using Verse/RimWorld/UnityEngine)"
Get-ChildItem -Path $src\Domain -Recurse -Filter *.cs | ForEach-Object {
    $t = Get-Content -Path $_.FullName -Encoding UTF8 -Raw
    $m = ''
    if ($t -match 'using Verse;') { $m += ' Verse' }
    if ($t -match 'using RimWorld') { $m += ' RimWorld' }
    if ($t -match 'using UnityEngine') { $m += ' UnityEngine' }
    if ($m -ne '') { "{0} ->{1}" -f $_.Name, $m }
}

"==== UI WINDOW FILES (non-component) referencing ChronicleGameComponent directly"
Get-ChildItem -Path $src\Archive -Recurse -Filter *.cs | Where-Object { $_.Name -notmatch 'UIComponents|UITheme|ArchiveUiStyle' } | ForEach-Object {
    $t = Get-Content -Path $_.FullName -Encoding UTF8 -Raw
    if ($t -match 'ChronicleGameComponent') { "{0}" -f $_.Name }
}

"==== DONE"
