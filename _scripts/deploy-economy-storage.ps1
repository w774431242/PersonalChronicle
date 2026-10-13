$ErrorActionPreference = 'Stop'
$storageRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$storageModsRoot = [IO.Path]::GetFullPath((Join-Path $storageRoot '../../Mods'))
$storageTarget = [IO.Path]::GetFullPath((Join-Path $storageModsRoot '殖民地综合管理系统'))
if (-not $storageTarget.StartsWith($storageModsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Deployment target is outside the RimWorld Mods directory.'
}
if (@(Get-Process -Name 'RimWorld*' -ErrorAction SilentlyContinue).Count) {
    throw 'RimWorld is running. Close it before deploying this build.'
}
$storageRecords = Join-Path $storageRoot 'docs/验证/共有私有仓储与食品付款_20261010'
$storageEvidence = Get-Content -LiteralPath (Join-Path $storageRecords 'verification.json') -Raw -Encoding UTF8 | ConvertFrom-Json
[xml]$storageSourceAbout = Get-Content -LiteralPath (Join-Path $storageRoot 'About/About.xml') -Raw -Encoding UTF8
[xml]$storageTargetAbout = Get-Content -LiteralPath (Join-Path $storageTarget 'About/About.xml') -Raw -Encoding UTF8
if ([string]::IsNullOrWhiteSpace($storageSourceAbout.ModMetaData.packageId) -or
    $storageSourceAbout.ModMetaData.packageId -ne $storageTargetAbout.ModMetaData.packageId) { throw 'Mod package identity differs.' }
if ((Get-FileHash -LiteralPath (Join-Path $storageRoot 'Assemblies/PersonalChronicle.dll')).Hash -ne $storageEvidence.modSha256 -or
    (Get-FileHash -LiteralPath (Join-Path $storageRoot 'Tests/bin/Release/net48/PersonalChronicle.dll')).Hash -ne $storageEvidence.modSha256) {
    throw 'The DLL differs from the tested build. Revalidate before deployment.'
}
foreach ($storageFile in @($storageEvidence.sourceFiles) + @($storageEvidence.testReports)) {
    if ((Get-FileHash -LiteralPath (Join-Path $storageRoot $storageFile.path)).Hash -ne $storageFile.sha256) {
        throw "Verification input changed: $($storageFile.path)"
    }
}
$storageFiles = @('Assemblies/PersonalChronicle.dll', 'Defs/PawnEconomy_Storage.xml',
    'Languages/ChineseSimplified/Keyed/PawnEconomyStorage.xml', 'Languages/English/Keyed/PawnEconomyStorage.xml',
    'Languages/English/DefInjected/ThingDef/PawnEconomyStorage.xml',
    'Languages/ChineseSimplified/Keyed/PawnNeedSpeech.xml', 'Languages/English/Keyed/PawnNeedSpeech.xml')
$storageBefore = @{}
foreach ($storageFile in Get-ChildItem -LiteralPath $storageTarget -Recurse -File) {
    $storageRelative = [IO.Path]::GetRelativePath($storageTarget, $storageFile.FullName).Replace('\', '/')
    $storageBefore[$storageRelative] = (Get-FileHash -LiteralPath $storageFile.FullName).Hash
}
$storageBackup = Join-Path $storageRoot ('_backups/economy-storage_' + [DateTime]::Now.ToString('yyyyMMdd_HHmmss_fff'))
New-Item -ItemType Directory -Path $storageBackup | Out-Null
$storageWritten = @()
foreach ($storageRelative in $storageFiles) {
    $storageSource = Join-Path $storageRoot $storageRelative
    $storageDestination = Join-Path $storageTarget $storageRelative
    if (Test-Path -LiteralPath $storageDestination) {
        $storageOld = Join-Path $storageBackup $storageRelative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $storageOld) | Out-Null
        Copy-Item -LiteralPath $storageDestination -Destination $storageOld
        if ((Get-FileHash -LiteralPath $storageOld).Hash -ne $storageBefore[$storageRelative]) { throw 'Backup checksum mismatch.' }
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $storageDestination) | Out-Null
    Copy-Item -LiteralPath $storageSource -Destination $storageDestination
    $storageSha = (Get-FileHash -LiteralPath $storageSource).Hash
    if ((Get-FileHash -LiteralPath $storageDestination).Hash -ne $storageSha) { throw "Deployment checksum mismatch: $storageRelative" }
    $storageWritten += [ordered]@{path=$storageRelative;sha256=$storageSha;previousSha256=$storageBefore[$storageRelative]}
}
$storageUnchanged = 0
foreach ($storageRelative in $storageBefore.Keys) {
    if ($storageRelative -in $storageFiles) { continue }
    if ((Get-FileHash -LiteralPath (Join-Path $storageTarget $storageRelative)).Hash -ne $storageBefore[$storageRelative]) {
        throw "Unrelated deployed file changed: $storageRelative"
    }
    $storageUnchanged++
}
[ordered]@{
    changeId='CHG-20261010-ECONOMY-STORAGE';verifiedUtc=[DateTime]::UtcNow.ToString('o')
    source=$storageRoot;target=$storageTarget;backup=$storageBackup;files=$storageWritten
    unchangedFilesVerified=$storageUnchanged;deployed=$true;gameVerified=$false
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $storageRecords 'deployment.json') -Encoding UTF8
Write-Output "PASS: $($storageFiles.Count) files deployed; $storageUnchanged other files preserved. Backup: $storageBackup"
