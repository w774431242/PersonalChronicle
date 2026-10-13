$ErrorActionPreference = 'Stop'
$artRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artModsRoot = [IO.Path]::GetFullPath((Join-Path $artRoot '../../Mods'))
$artTarget = [IO.Path]::GetFullPath((Join-Path $artModsRoot '殖民地综合管理系统'))
if (-not $artTarget.StartsWith($artModsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Deployment target is outside RimWorld Mods.' }
if (@(Get-Process -Name 'RimWorld*' -ErrorAction SilentlyContinue).Count) { throw 'Close RimWorld before deploying textures.' }
$artRecords = Join-Path $artRoot 'docs/验证/共有私有仓储美术_20261010'
$artManifest = Get-Content -LiteralPath (Join-Path $artRecords 'asset-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $artManifest.technicalChecksPassed -or -not $artManifest.visualReviewed -or @($artManifest.files).Count -ne 8) { throw 'Eight reviewed and verified directional assets required.' }
$artRuntime = Get-Content -LiteralPath (Join-Path $artRoot 'docs/验证/共有私有仓储与食品付款_20261010/verification.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$artCurrentRuntimeSha = (Get-FileHash -LiteralPath (Join-Path $artTarget 'Assemblies/PersonalChronicle.dll')).Hash
if ((Get-FileHash -LiteralPath (Join-Path $artRoot 'Assemblies/PersonalChronicle.dll')).Hash -ne $artCurrentRuntimeSha) { throw 'Current source and deployed runtime differ.' }
# Other work may rebuild the mod while art is generated. Preserve the current matching
# runtime; historical test evidence must not be relabeled as testing this new binary.
foreach ($artInput in $artRuntime.sourceFiles) {
    if ($artInput.path -in @('Defs/PawnEconomy_Storage.xml','Source/PersonalChronicle/BuildStamp.g.cs')) { continue }
    if ((Get-FileHash -LiteralPath (Join-Path $artRoot $artInput.path)).Hash -ne $artInput.sha256) { throw "Runtime verification input changed: $($artInput.path)" }
}
[xml]$artSourceAbout = Get-Content -LiteralPath (Join-Path $artRoot 'About/About.xml') -Raw -Encoding UTF8
[xml]$artTargetAbout = Get-Content -LiteralPath (Join-Path $artTarget 'About/About.xml') -Raw -Encoding UTF8
if ([string]::IsNullOrWhiteSpace($artSourceAbout.ModMetaData.packageId) -or $artSourceAbout.ModMetaData.packageId -ne $artTargetAbout.ModMetaData.packageId) { throw 'Mod package identity differs.' }
if ((Get-FileHash -LiteralPath (Join-Path $artRoot 'Defs/PawnEconomy_Storage.xml')).Hash -ne $artManifest.defSha256) { throw 'Def differs from reviewed art configuration.' }
$artFiles = @('Defs/PawnEconomy_Storage.xml')
foreach ($artAsset in $artManifest.files) {
    if ($artAsset.path -notmatch '^Textures/Things/Building/PawnEconomy/PC_(Public|Private)Storage_(north|east|south|west)\.png$') { throw 'Asset outside deployment whitelist.' }
    if ((Get-FileHash -LiteralPath (Join-Path $artRoot $artAsset.path)).Hash -ne $artAsset.sha256 -or
        (Get-FileHash -LiteralPath (Join-Path $artRecords $artAsset.source)).Hash -ne $artAsset.sourceSha256) { throw "Art checksum mismatch: $($artAsset.path)" }
    $artFiles += $artAsset.path
}
if (@($artFiles | Select-Object -Unique).Count -ne 9) { throw 'Duplicate asset entries.' }
$artBefore = @{}
foreach ($artFile in Get-ChildItem -LiteralPath $artTarget -Recurse -File) {
    $artRelative = [IO.Path]::GetRelativePath($artTarget, $artFile.FullName).Replace('\', '/')
    $artBefore[$artRelative] = (Get-FileHash -LiteralPath $artFile.FullName).Hash
}
$artBackup = Join-Path $artRoot ('_backups/economy-storage-art_' + [DateTime]::Now.ToString('yyyyMMdd_HHmmss_fff'))
New-Item -ItemType Directory -Path $artBackup | Out-Null
$artWritten = @()
foreach ($artRelative in $artFiles) {
    $artDestination = Join-Path $artTarget $artRelative
    if (Test-Path -LiteralPath $artDestination) {
        $artOld = Join-Path $artBackup $artRelative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $artOld) | Out-Null
        Copy-Item -LiteralPath $artDestination -Destination $artOld
        if ((Get-FileHash -LiteralPath $artOld).Hash -ne $artBefore[$artRelative]) { throw 'Backup checksum mismatch.' }
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $artDestination) | Out-Null
    Copy-Item -LiteralPath (Join-Path $artRoot $artRelative) -Destination $artDestination
    $artSha = (Get-FileHash -LiteralPath (Join-Path $artRoot $artRelative)).Hash
    if ((Get-FileHash -LiteralPath $artDestination).Hash -ne $artSha) { throw "Deployment checksum mismatch: $artRelative" }
    $artWritten += [ordered]@{path=$artRelative;sha256=$artSha;previousSha256=$artBefore[$artRelative]}
}
$artUnchanged = 0
foreach ($artRelative in $artBefore.Keys) {
    if ($artRelative -in $artFiles) { continue }
    if ((Get-FileHash -LiteralPath (Join-Path $artTarget $artRelative)).Hash -ne $artBefore[$artRelative]) { throw "Unrelated deployed file changed: $artRelative" }
    $artUnchanged++
}
[ordered]@{changeId='ART-20261010-ECONOMY-STORAGE';verifiedUtc=[DateTime]::UtcNow.ToString('o');source=$artRoot;target=$artTarget
    backup=$artBackup;files=$artWritten;unchangedFilesVerified=$artUnchanged;runtimeDllSha256=$artCurrentRuntimeSha
    historicalTestedRuntimeDllSha256=$artRuntime.modSha256;runtimePreserved=$true;runtimeMatchesHistoricalTests=($artCurrentRuntimeSha -eq $artRuntime.modSha256)
    deployed=$true;gameVerified=$false} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $artRecords 'deployment.json') -Encoding UTF8
Write-Output "PASS: 8 textures and 1 Def deployed; $artUnchanged other files preserved. Backup: $artBackup"
