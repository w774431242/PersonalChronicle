$ErrorActionPreference = "SilentlyContinue"
$roots = @(
  "E:\SteamLibrary\steamapps\common\RimWorld",
  "C:\Users\Administrator\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios"
)
$hits = @()
foreach ($r in $roots) {
  if (Test-Path $r) {
    Get-ChildItem $r -Recurse -File | Where-Object { $_.Name -match "TranslationReport" } | ForEach-Object { $hits += $_.FullName }
  }
}
Write-Host "REPORT_FILES:"; $hits | Sort-Object -Unique

# check AI pack overlap
$ourKeys = @()
$xml = [System.IO.File]::ReadAllText("e:\SteamLibrary\steamapps\common\RimWorld\11\PersonalChronicle\Languages\ChineseSimplified\Keyed\Archive.xml", [System.Text.Encoding]::UTF8)
[regex]::Matches($xml, '<PersonalChronicle\.[A-Za-z0-9_.]+>') | ForEach-Object { $ourKeys += $_.Value.Trim('<','>') }
Write-Host "OUR_KEYS=$($ourKeys.Count)"
$overlap = @()
Get-ChildItem "E:\SteamLibrary\steamapps\common\RimWorld\Mods\!Translation_AI_Pack\Languages" -Recurse -File -Filter *.xml | ForEach-Object {
  $c = [System.IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::UTF8)
  foreach ($k in $ourKeys) { if ($c.Contains($k)) { $overlap += "$($_.FullName) -> $k"; break } }
}
if ($overlap.Count -eq 0) { Write-Host "AI_PACK_NO_OVERLAP" } else { $overlap }
