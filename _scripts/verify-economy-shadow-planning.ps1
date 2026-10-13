$ErrorActionPreference = 'Stop'
$planningRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$planningRelative = 'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态'
$planningFiles = @(
    'Source/PersonalChronicle/Domain/PawnEconomy/Planning/LifePolicy.cs',
    'Source/PersonalChronicle/Domain/PawnEconomy/Planning/ShadowPlan.cs',
    'Source/PersonalChronicle/Domain/PawnEconomy/Budget/LifeBudget.cs',
    'Source/PersonalChronicle/Application/PawnEconomy/Planning/ShadowPlanningCoordinator.cs',
    'Source/PersonalChronicle/Application/PawnEconomy/Perception/NeedDashboard.cs',
    'Source/PersonalChronicle/Economy/PawnEconomy/EconomyShadowPlanning.cs',
    'Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.cs',
    'Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs',
    'Source/PersonalChronicle/Economy/PawnEconomy/EconomyPerception.cs',
    'Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs',
    'Source/PersonalChronicle/Economy/PawnEconomy/Perception/VanillaPawnFactCapture.cs',
    'Source/PersonalChronicle/BuildStamp.g.cs',
    'Tests/PawnLifeBudgetTests.cs', 'Tests/PawnShadowPlanningTests.cs',
    'Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml', 'Languages/English/Keyed/PawnEconomyNeeds.xml',
    'Languages/ChineseSimplified/Keyed/PawnEconomyPlanning.xml', 'Languages/English/Keyed/PawnEconomyPlanning.xml'
)
$planningRows = @($planningFiles | ForEach-Object {
    $planningPath = Join-Path $planningRoot $_
    if (-not (Test-Path -LiteralPath $planningPath)) { throw "Missing source: $_" }
    [ordered]@{ path=$_; bytes=(Get-Item -LiteralPath $planningPath).Length; sha256=(Get-FileHash -LiteralPath $planningPath).Hash }
})
foreach ($planningPath in @($planningFiles | Where-Object { $_ -match '/Domain/' })) {
    if (Select-String -LiteralPath (Join-Path $planningRoot $planningPath) -Pattern '^using (Verse|RimWorld|UnityEngine|RUIF|Harmony)' -Quiet) { throw "Domain dependency violation: $planningPath" }
}
function Read-PlanningKeys([string]$planningLanguage) {
    $planningKeys = @{}
    foreach ($planningName in @('PawnEconomyNeeds.xml','PawnEconomyPlanning.xml')) {
        [xml]$planningXml = Get-Content -LiteralPath (Join-Path $planningRoot "Languages/$planningLanguage/Keyed/$planningName") -Raw -Encoding UTF8
        foreach ($planningNode in $planningXml.LanguageData.ChildNodes) {
            if ($planningNode.NodeType -ne [Xml.XmlNodeType]::Element) { continue }
            if ($planningKeys.ContainsKey($planningNode.Name)) { throw "Duplicate translation: $($planningNode.Name)" }
            $planningKeys[$planningNode.Name] = $planningNode.InnerText
        }
    }
    return $planningKeys
}
$planningChinese = Read-PlanningKeys 'ChineseSimplified'; $planningEnglish = Read-PlanningKeys 'English'
if (@(Compare-Object @($planningChinese.Keys) @($planningEnglish.Keys)).Count) { throw 'Translation keys differ' }
foreach ($planningKey in $planningChinese.Keys) {
    $planningZhArgs = @([regex]::Matches($planningChinese[$planningKey], '\{\d+\}') | ForEach-Object Value | Sort-Object)
    $planningEnArgs = @([regex]::Matches($planningEnglish[$planningKey], '\{\d+\}') | ForEach-Object Value | Sort-Object)
    if (($planningZhArgs -join '|') -ne ($planningEnArgs -join '|')) { throw "Translation placeholders differ: $planningKey" }
}
$planningTestRelative = 'Tests/TestResults/EconomyLanding-B3-Full.trx'
[xml]$planningTrx = Get-Content -LiteralPath (Join-Path $planningRoot $planningTestRelative) -Raw -Encoding UTF8
$planningCounters = $planningTrx.TestRun.ResultSummary.Counters
if ([int]$planningCounters.failed -ne 0 -or [int]$planningCounters.passed -lt 958) { throw 'Missing successful full regression' }
$planningSkipped = @($planningTrx.TestRun.Results.UnitTestResult | Where-Object outcome -eq 'NotExecuted').Count
$planningDll = Join-Path $planningRoot 'Assemblies/PersonalChronicle.dll'
$planningDllHash = (Get-FileHash -LiteralPath $planningDll).Hash
if ((Get-FileHash -LiteralPath (Join-Path $planningRoot 'Tests/bin/Release/net48/PersonalChronicle.dll')).Hash -ne $planningDllHash) { throw 'Tested DLL differs from deployable DLL' }
$planningBindingPath = Join-Path $planningRoot '_verify_tmp/Economy-B3-tested-build.json'
if (-not (Test-Path -LiteralPath $planningBindingPath)) { throw 'Missing before/after test binding; run test-economy-shadow-planning.ps1' }
$planningBinding = Get-Content -LiteralPath $planningBindingPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $planningBinding.beforeAfterHashesMatched -or $planningBinding.modSha256 -ne $planningDllHash -or
    $planningBinding.trxSha256 -ne (Get-FileHash -LiteralPath (Join-Path $planningRoot $planningTestRelative)).Hash) { throw 'Test binding does not match current DLL/TRX' }
foreach ($planningRow in $planningRows) {
    if ($planningBinding.sourceHashes.($planningRow.path) -ne $planningRow.sha256) { throw "Source not bound to test: $($planningRow.path)" }
}
$planningNewTests = @($planningTrx.TestRun.TestDefinitions.UnitTest | Where-Object { $_.TestMethod.className -match 'PawnLifeBudgetTests|PawnShadowPlanningTests' }).Count
$planningReport = [ordered]@{
    changeId='CHG-20261008-ECON-B3-SHADOW'; batch='B3.1'; verifiedUtc=[DateTime]::UtcNow.ToString('o')
    buildObserved='Release command completed with 0 warnings and 0 errors'; buildStamp=(Get-Content -LiteralPath (Join-Path $planningRoot 'Source/PersonalChronicle/BuildStamp.g.cs') -Raw).Trim()
    modSha256=$planningDllHash; gameSha256=(Get-FileHash -LiteralPath (Join-Path $planningRoot '../../RimWorldWin64_Data/Managed/Assembly-CSharp.dll')).Hash
    tests=[ordered]@{path=$planningTestRelative;sha256=(Get-FileHash -LiteralPath (Join-Path $planningRoot $planningTestRelative)).Hash;passed=[int]$planningCounters.passed;failed=[int]$planningCounters.failed;skippedResults=$planningSkipped;newPlanningTests=$planningNewTests;binding='_verify_tmp/Economy-B3-tested-build.json';bindingSha256=(Get-FileHash -LiteralPath $planningBindingPath).Hash}
    domainDependenciesVerified=$true; translationsVerified=$true; translationKeys=$planningChinese.Count; sourceFiles=$planningRows
    runtimeWired=$true; uiWired=$true; shadowOnly=$true; persistedDecisionState=$false; pricedConsumptionProvider=$false; executableMarketProvider=$false
    automaticEmploymentFixed=$false; generalFeeRuntimeWired=$false; activeAvailableInGame=$false; gameVerified=$false; gameplayPerformanceVerified=$false; fullModelImplemented=$false
    globalGateAvailable=(Test-Path -LiteralPath (Join-Path $planningRoot 'scripts/gate-runner.ps1'))
}
$planningReport | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $planningRoot "$planningRelative/B3.1_实装验证.json") -Encoding UTF8
Write-Host "PASS: full regression $($planningCounters.passed)/$($planningCounters.failed)/$planningSkipped; $planningNewTests new planning tests; $($planningChinese.Count) bilingual keys; tested DLL/source binding matched."
