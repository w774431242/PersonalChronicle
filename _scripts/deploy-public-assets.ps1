$ErrorActionPreference = 'Stop'
$publicRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$publicModsRoot = [IO.Path]::GetFullPath((Join-Path $publicRoot '../../Mods'))
$publicTarget = [IO.Path]::GetFullPath((Join-Path $publicModsRoot '殖民地综合管理系统'))
if (-not $publicTarget.StartsWith($publicModsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Deployment target is outside the RimWorld Mods directory.'
}
if (@(Get-Process -Name 'RimWorld*' -ErrorAction SilentlyContinue).Count) { throw 'Close RimWorld before deploying.' }
$publicRecords = Join-Path $publicRoot 'docs/验证/公有资产与机构资金贯通_20261011'
$publicEvidence = Get-Content -LiteralPath (Join-Path $publicRecords 'verification.json') -Raw -Encoding UTF8 | ConvertFrom-Json
[xml]$publicSourceAbout = Get-Content -LiteralPath (Join-Path $publicRoot 'About/About.xml') -Raw -Encoding UTF8
[xml]$publicTargetAbout = Get-Content -LiteralPath (Join-Path $publicTarget 'About/About.xml') -Raw -Encoding UTF8
if ([string]::IsNullOrWhiteSpace($publicSourceAbout.ModMetaData.packageId) -or
    $publicSourceAbout.ModMetaData.packageId -ne $publicTargetAbout.ModMetaData.packageId) { throw 'Mod package identity differs.' }
if ((Get-FileHash -LiteralPath (Join-Path $publicRoot 'Assemblies/PersonalChronicle.dll')).Hash -ne $publicEvidence.modSha256 -or
    (Get-FileHash -LiteralPath (Join-Path $publicRoot 'Tests/bin/Release/net48/PersonalChronicle.dll')).Hash -ne $publicEvidence.modSha256) {
    throw 'DLL differs from the tested build. Revalidate before deploying.'
}
foreach ($publicFile in @($publicEvidence.sourceFiles) + @($publicEvidence.testReports)) {
    if ((Get-FileHash -LiteralPath (Join-Path $publicRoot $publicFile.path)).Hash -ne $publicFile.sha256) {
        throw "Verification input changed: $($publicFile.path)"
    }
}
$publicFiles = @('Assemblies/PersonalChronicle.dll',
    'Languages/ChineseSimplified/Keyed/PawnEconomyPublicAssets.xml', 'Languages/English/Keyed/PawnEconomyPublicAssets.xml',
    'Languages/ChineseSimplified/Keyed/TreasuryOperations.xml', 'Languages/English/Keyed/TreasuryOperations.xml')
$publicBefore = @{}
foreach ($publicFile in Get-ChildItem -LiteralPath $publicTarget -Recurse -File) {
    $publicRelative = [IO.Path]::GetRelativePath($publicTarget, $publicFile.FullName).Replace('\', '/')
    $publicBefore[$publicRelative] = (Get-FileHash -LiteralPath $publicFile.FullName).Hash
}
$publicBackup = Join-Path $publicRoot ('_backups/public-assets_' + [DateTime]::Now.ToString('yyyyMMdd_HHmmss_fff'))
New-Item -ItemType Directory -Path $publicBackup | Out-Null
# Complete backups and freeze the exact payload before writing any target files.
foreach ($publicRelative in $publicFiles) {
    $publicOld = Join-Path $publicTarget $publicRelative
    if (Test-Path -LiteralPath $publicOld) {
        $publicSaved = Join-Path $publicBackup ('previous/' + $publicRelative)
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $publicSaved) | Out-Null
        Copy-Item -LiteralPath $publicOld -Destination $publicSaved
        if ((Get-FileHash -LiteralPath $publicSaved).Hash -ne $publicBefore[$publicRelative]) { throw 'Backup checksum mismatch.' }
    }
    $publicFrozen = Join-Path $publicBackup ('payload/' + $publicRelative)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $publicFrozen) | Out-Null
    Copy-Item -LiteralPath (Join-Path $publicRoot $publicRelative) -Destination $publicFrozen
    $publicExpected = if ($publicRelative -eq 'Assemblies/PersonalChronicle.dll') { $publicEvidence.modSha256 }
        else { ($publicEvidence.sourceFiles | Where-Object path -eq $publicRelative).sha256 }
    if ((Get-FileHash -LiteralPath $publicFrozen).Hash -ne $publicExpected) { throw "Payload drifted: $publicRelative" }
}
if (@(Get-Process -Name 'RimWorld*' -ErrorAction SilentlyContinue).Count) { throw 'RimWorld restarted before deployment.' }
$publicWritten = @()
try {
    foreach ($publicRelative in $publicFiles) {
        $publicSource = Join-Path $publicBackup ('payload/' + $publicRelative)
        $publicDestination = Join-Path $publicTarget $publicRelative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $publicDestination) | Out-Null
        Copy-Item -LiteralPath $publicSource -Destination $publicDestination
        $publicHash = (Get-FileHash -LiteralPath $publicSource).Hash
        if ((Get-FileHash -LiteralPath $publicDestination).Hash -ne $publicHash) { throw "Deployment checksum mismatch: $publicRelative" }
        $publicWritten += [ordered]@{path=$publicRelative;sha256=$publicHash;previousSha256=$publicBefore[$publicRelative]}
    }
} catch {
    foreach ($publicRelative in $publicFiles) {
        $publicSaved = Join-Path $publicBackup ('previous/' + $publicRelative)
        if (Test-Path -LiteralPath $publicSaved) { Copy-Item -LiteralPath $publicSaved -Destination (Join-Path $publicTarget $publicRelative) -Force }
        elseif (Test-Path -LiteralPath (Join-Path $publicTarget $publicRelative)) { Remove-Item -LiteralPath (Join-Path $publicTarget $publicRelative) }
    }
    throw
}
$publicUnchanged = 0
foreach ($publicRelative in $publicBefore.Keys) {
    if ($publicRelative -in $publicFiles) { continue }
    if ((Get-FileHash -LiteralPath (Join-Path $publicTarget $publicRelative)).Hash -ne $publicBefore[$publicRelative]) {
        throw "Unrelated deployed file changed: $publicRelative"
    }
    $publicUnchanged++
}
[ordered]@{
    changeId='CHG-20261011-PUBLIC-ASSET-LINKS';verifiedUtc=[DateTime]::UtcNow.ToString('o')
    source=$publicRoot;target=$publicTarget;backup=$publicBackup;files=$publicWritten
    unchangedFilesVerified=$publicUnchanged;deployed=$true;gameVerified=$false
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $publicRecords 'deployment.json') -Encoding UTF8
Write-Output "PASS: $($publicFiles.Count) files deployed; $publicUnchanged other files preserved. Backup: $publicBackup"

