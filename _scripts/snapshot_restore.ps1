# Restore tracked files that differ between the 08-29 snapshot and the working tree.
# Untracked files are LIVE post-reset work and are never touched.
# ASCII only.
$ErrorActionPreference = 'Stop'
$root = 'E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统'
$snap = Join-Path $root '_backup\20260829_154500'
Set-Location $root

$tracked = @(git ls-files 2>$null | ForEach-Object { $_ -replace '/','\' })
$trackedSet = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($t in $tracked) { [void]$trackedSet.Add($t) }

$snapRes = (Resolve-Path $snap).Path
$restored = @()
$skipped = @()
Get-ChildItem $snap -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
    $rel = $_.FullName.Substring($snapRes.Length + 1)
    if (-not $trackedSet.Contains($rel)) { $skipped += $rel; return }
    $cur = Join-Path $root $rel
    if (-not (Test-Path $cur)) { $skipped += ($rel + ' [missing now]'); return }
    $h1 = (Get-FileHash $_.FullName -Algorithm MD5).Hash
    $h2 = (Get-FileHash $cur -Algorithm MD5).Hash
    if ($h1 -ne $h2) {
        $dir = Split-Path $cur -Parent
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        Copy-Item $_.FullName $cur -Force
        $restored += $rel
    }
}
Write-Host ("restored tracked files: " + $restored.Count)
$restored | ForEach-Object { Write-Host ("  " + $_) }
Write-Host ("skipped (untracked/missing): " + $skipped.Count)
