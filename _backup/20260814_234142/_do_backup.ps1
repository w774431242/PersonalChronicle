$src = 'e:\SteamLibrary\steamapps\common\RimWorld\11\PersonalChronicle'
$ts = Get-Date -Format 'yyyyMMdd_HHmmss'
$dst = Join-Path $src ('_backup\' + $ts)
New-Item -ItemType Directory -Path $dst -Force | Out-Null
Get-ChildItem $src -Force | Where-Object { $_.Name -notin @('_backup', '.git') } | ForEach-Object {
    Copy-Item $_.FullName -Destination $dst -Recurse -Force
}
$files = Get-ChildItem $dst -Recurse -File
$size = ($files | Measure-Object Length -Sum).Sum / 1MB
Write-Output ("Backup done: " + $ts + " | files=" + $files.Count + " | size=" + [math]::Round($size, 1) + "MB")
