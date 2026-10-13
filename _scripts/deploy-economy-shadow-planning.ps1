$ErrorActionPreference = 'Stop'
$planningRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$planningTarget = [IO.Path]::GetFullPath((Join-Path $planningRoot '../../Mods/殖民地综合管理系统'))
$planningRecords = Join-Path $planningRoot 'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态'
$planningEvidence = Get-Content -LiteralPath (Join-Path $planningRecords 'B3.1_实装验证.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (@(Get-Process -Name 'RimWorld*' -ErrorAction SilentlyContinue).Count) { throw 'Game running; deployment not started. Close RimWorld and rerun this script.' }
[xml]$planningSrcAbout = Get-Content -LiteralPath (Join-Path $planningRoot 'About/About.xml') -Raw -Encoding UTF8
[xml]$planningDstAbout = Get-Content -LiteralPath (Join-Path $planningTarget 'About/About.xml') -Raw -Encoding UTF8
if ([string]::IsNullOrWhiteSpace($planningSrcAbout.ModMetaData.packageId) -or
    $planningSrcAbout.ModMetaData.packageId -ne $planningDstAbout.ModMetaData.packageId) { throw 'Target package identity differs' }
foreach ($planningFile in $planningEvidence.sourceFiles) {
    if ((Get-FileHash -LiteralPath (Join-Path $planningRoot $planningFile.path)).Hash -ne $planningFile.sha256) { throw "Source drift: $($planningFile.path)" }
}
if ((Get-FileHash -LiteralPath (Join-Path $planningRoot 'Assemblies/PersonalChronicle.dll')).Hash -ne $planningEvidence.modSha256) { throw 'DLL differs from tested build' }
if ((Get-FileHash -LiteralPath (Join-Path $planningRoot $planningEvidence.tests.path)).Hash -ne $planningEvidence.tests.sha256) { throw 'Test report drift' }
$planningDeploy = @('Assemblies/PersonalChronicle.dll','Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml','Languages/English/Keyed/PawnEconomyNeeds.xml','Languages/ChineseSimplified/Keyed/PawnEconomyPlanning.xml','Languages/English/Keyed/PawnEconomyPlanning.xml')
$planningBefore = @{}
foreach ($planningFile in Get-ChildItem -LiteralPath $planningTarget -Recurse -File) {
    $planningRel = [IO.Path]::GetRelativePath($planningTarget,$planningFile.FullName).Replace('\','/')
    $planningBefore[$planningRel] = (Get-FileHash -LiteralPath $planningFile.FullName).Hash
}
$planningBackup = Join-Path $planningRoot ('_backups/economy-b3-deploy_' + [DateTime]::Now.ToString('yyyyMMdd_HHmmss_fff'))
New-Item -ItemType Directory -Path $planningBackup | Out-Null
$planningWritten = @()
foreach ($planningRel in $planningDeploy) {
    $planningSrc = Join-Path $planningRoot $planningRel; $planningDst = Join-Path $planningTarget $planningRel
    $planningOld = Join-Path $planningBackup $planningRel
    if (Test-Path -LiteralPath $planningDst) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $planningOld) | Out-Null
        Copy-Item -LiteralPath $planningDst -Destination $planningOld
        if ((Get-FileHash -LiteralPath $planningOld).Hash -ne $planningBefore[$planningRel]) { throw "Backup mismatch: $planningRel" }
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $planningDst) | Out-Null
    Copy-Item -LiteralPath $planningSrc -Destination $planningDst
    $planningSha = (Get-FileHash -LiteralPath $planningSrc).Hash
    if ((Get-FileHash -LiteralPath $planningDst).Hash -ne $planningSha) { throw "Deploy mismatch: $planningRel" }
    if ($planningRel.EndsWith('.xml')) { $null = [xml](Get-Content -LiteralPath $planningDst -Raw -Encoding UTF8) }
    $planningWritten += [ordered]@{path=$planningRel;sha256=$planningSha;previousSha256=$planningBefore[$planningRel];newFile=(-not $planningBefore.ContainsKey($planningRel))}
}
$planningUntouched = 0
foreach ($planningRel in $planningBefore.Keys) {
    if ($planningRel -in $planningDeploy) { continue }
    if ((Get-FileHash -LiteralPath (Join-Path $planningTarget $planningRel)).Hash -ne $planningBefore[$planningRel]) { throw "Unrelated deployed file changed: $planningRel" }
    $planningUntouched++
}
$planningDeployment = [ordered]@{
    changeId='CHG-20261008-ECON-B3-SHADOW'; verifiedUtc=[DateTime]::UtcNow.ToString('o');source=$planningRoot;target=$planningTarget;backup=$planningBackup
    testedBuildSha256=$planningEvidence.modSha256;deployedBuildSha256=(Get-FileHash -LiteralPath (Join-Path $planningTarget 'Assemblies/PersonalChronicle.dll')).Hash
    deployed=$true; files=$planningWritten; unchangedFilesVerified=$planningUntouched;workshopIdentityPreserved=$true; gameVerified=$false;automaticEmploymentFixed=$false
}
$planningDeployment | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $planningRecords 'B3.1_部署验证.json') -Encoding UTF8
Write-Host "PASS: 5 files deployed, $planningUntouched other files preserved. Backup: $planningBackup. Restart RimWorld for this build."
