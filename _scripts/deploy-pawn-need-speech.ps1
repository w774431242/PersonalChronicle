$ErrorActionPreference = 'Stop'
$speechRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$speechModsRoot = [IO.Path]::GetFullPath((Join-Path $speechRoot '../../Mods'))
$speechTarget = [IO.Path]::GetFullPath((Join-Path $speechModsRoot '殖民地综合管理系统'))
if (-not $speechTarget.StartsWith($speechModsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Deployment target is outside the RimWorld Mods directory.'
}
if (@(Get-Process -Name 'RimWorld*' -ErrorAction SilentlyContinue).Count) {
    throw 'RimWorld is running. Close it before deploying this build.'
}
$speechRecords = Join-Path $speechRoot 'docs/验证/Pawn需求聊天气泡_20261010'
$speechEvidence = Get-Content -LiteralPath (Join-Path $speechRecords 'verification.json') -Raw -Encoding UTF8 | ConvertFrom-Json
[xml]$speechSourceAbout = Get-Content -LiteralPath (Join-Path $speechRoot 'About/About.xml') -Raw -Encoding UTF8
[xml]$speechTargetAbout = Get-Content -LiteralPath (Join-Path $speechTarget 'About/About.xml') -Raw -Encoding UTF8
if ([string]::IsNullOrWhiteSpace($speechSourceAbout.ModMetaData.packageId) -or
    $speechSourceAbout.ModMetaData.packageId -ne $speechTargetAbout.ModMetaData.packageId) { throw 'Mod package identity differs.' }
if ((Get-FileHash -LiteralPath (Join-Path $speechRoot 'Assemblies/PersonalChronicle.dll')).Hash -ne $speechEvidence.modSha256) {
    throw 'The DLL differs from the tested build. Revalidate before deployment.'
}
foreach ($speechFile in @($speechEvidence.sourceFiles) + @($speechEvidence.testReports)) {
    if ((Get-FileHash -LiteralPath (Join-Path $speechRoot $speechFile.path)).Hash -ne $speechFile.sha256) {
        throw "Verification input changed: $($speechFile.path)"
    }
}
$speechFiles = @('Assemblies/PersonalChronicle.dll',
    'Languages/ChineseSimplified/Keyed/PawnNeedSpeech.xml', 'Languages/English/Keyed/PawnNeedSpeech.xml')
$speechBefore = @{}
foreach ($speechFile in Get-ChildItem -LiteralPath $speechTarget -Recurse -File) {
    $speechRelative = [IO.Path]::GetRelativePath($speechTarget, $speechFile.FullName).Replace('\', '/')
    $speechBefore[$speechRelative] = (Get-FileHash -LiteralPath $speechFile.FullName).Hash
}
$speechBackup = Join-Path $speechRoot ('_backups/pawn-need-speech_' + [DateTime]::Now.ToString('yyyyMMdd_HHmmss_fff'))
New-Item -ItemType Directory -Path $speechBackup | Out-Null
$speechWritten = @()
foreach ($speechRelative in $speechFiles) {
    $speechSource = Join-Path $speechRoot $speechRelative
    $speechDestination = Join-Path $speechTarget $speechRelative
    if (Test-Path -LiteralPath $speechDestination) {
        $speechOld = Join-Path $speechBackup $speechRelative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $speechOld) | Out-Null
        Copy-Item -LiteralPath $speechDestination -Destination $speechOld
        if ((Get-FileHash -LiteralPath $speechOld).Hash -ne $speechBefore[$speechRelative]) { throw 'Backup checksum mismatch.' }
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $speechDestination) | Out-Null
    Copy-Item -LiteralPath $speechSource -Destination $speechDestination
    $speechSha = (Get-FileHash -LiteralPath $speechSource).Hash
    if ((Get-FileHash -LiteralPath $speechDestination).Hash -ne $speechSha) { throw "Deployment checksum mismatch: $speechRelative" }
    $speechWritten += [ordered]@{path=$speechRelative;sha256=$speechSha;previousSha256=$speechBefore[$speechRelative]}
}
$speechUnchanged = 0
foreach ($speechRelative in $speechBefore.Keys) {
    if ($speechRelative -in $speechFiles) { continue }
    if ((Get-FileHash -LiteralPath (Join-Path $speechTarget $speechRelative)).Hash -ne $speechBefore[$speechRelative]) {
        throw "Unrelated deployed file changed: $speechRelative"
    }
    $speechUnchanged++
}
[ordered]@{
    changeId='CHG-20261010-PAWN-NEED-SPEECH';verifiedUtc=[DateTime]::UtcNow.ToString('o')
    source=$speechRoot;target=$speechTarget;backup=$speechBackup;files=$speechWritten
    unchangedFilesVerified=$speechUnchanged;deployed=$true;gameVerified=$false
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $speechRecords 'deployment.json') -Encoding UTF8
Write-Output "PASS: 3 files deployed; $speechUnchanged other files preserved. Backup: $speechBackup"
