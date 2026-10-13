#requires -Version 7.0
param([string]$GameRoot = 'E:\SteamLibrary\steamapps\common\RimWorld')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
function Require([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Types($items) { foreach ($item in $items) { $item; if ($item.HasNestedTypes) { Types $item.NestedTypes } } }
function Calls($methods) {
    @($methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions |
        Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.ToString() } })
}
Add-Type -Path (Join-Path $repo 'InspectTypes/bin/Release/net8.0/Mono.Cecil.dll')
$parameters = [Mono.Cecil.ReaderParameters]::new(); $parameters.InMemory = $true
$dll = Join-Path $repo 'Assemblies/PersonalChronicle.dll'
$gameDll = Join-Path $GameRoot 'RimWorldWin64_Data/Managed/Assembly-CSharp.dll'
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll, $parameters)
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($gameDll, $parameters)
try {
    $types = @(Types $assembly.MainModule.Types)
    $domain = @($types | Where-Object FullName -match '^PersonalChronicle\.Domain\.PawnEconomy\.Finance\.')
    foreach ($type in $domain) {
        $references = @($type.Fields | ForEach-Object { $_.FieldType.ToString() })
        foreach ($method in $type.Methods) {
            $references += $method.ReturnType.ToString(); $references += @($method.Parameters | ForEach-Object { $_.ParameterType.ToString() })
            if ($method.HasBody) { $references += @($method.Body.Instructions | ForEach-Object { [string]$_.Operand }) }
        }
        Require (@($references | Where-Object { $_ -match '(Verse|RimWorld|UnityEngine|RUIF|HarmonyLib)\.|PersonalChronicle\.(Data|Economy)\.' }).Count -eq 0) ('Impure finance contract: ' + $type.FullName)
    }
    $prototype = $assembly.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.SettlementReservePrototype')
    Require ($null -ne $prototype -and $prototype.IsNotPublic) 'Physical prototype must remain internal'
    Require ($prototype.BaseType.FullName -eq 'System.Object') 'Reserve must not be a spawned Thing/building'
    Require (@($prototype.Interfaces | Where-Object { $_.InterfaceType.FullName -eq 'RimWorld.IHaulSource' }).Count -eq 0) 'Reserve cannot expose IHaulSource'
    $prototypeTypes = @($types | Where-Object { $_.FullName -eq $prototype.FullName -or $_.FullName.StartsWith($prototype.FullName + '/') })
    $prototypeCalls = @(Calls @($prototypeTypes | ForEach-Object Methods))
    Require (@($prototypeCalls | Where-Object { $_ -match 'ThingMaker::MakeThing|GenSpawn::|EconomyService::|ShadowTransactionKernel::' }).Count -eq 0) 'Reserve may not mint items, spend legacy accounts or call the shadow book'
    $otherCalls = @(Calls @($types | Where-Object { $_.FullName -notin $prototypeTypes.FullName } | ForEach-Object Methods))
    Require (@($otherCalls | Where-Object { $_ -match 'SettlementReservePrototype::' }).Count -eq 0) 'Prototype must not be attached to game payment or save routes yet'
    $execute = @($prototype.Methods | Where-Object Name -eq Execute)[0]
    $transfer = @($execute.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and [string]$_.Operand -match 'ThingOwner::TryTransferToContainer' })
    Require ($transfer.Count -eq 1 -and $transfer[0].Previous.OpCode.Name -eq 'ldc.i4.0') 'Physical moves must disable merging and retain result ownership evidence'
    $ready = @($prototype.Methods | Where-Object Name -eq get_CanExecuteInGame)[0].Body.Instructions
    Require (@($ready | Where-Object { $_.OpCode.Name -eq 'ldc.i4.0' }).Count -eq 1) 'Prototype may not claim game readiness'
    $parent = @($prototype.Methods | Where-Object Name -eq get_ParentHolder)[0].Body.Instructions
    Require (@($parent | Where-Object { $_.OpCode.Name -eq 'ldnull' }).Count -eq 1) 'Reserve must not inherit a map/caravan parent'
    foreach ($type in $prototypeTypes | Where-Object Name -notlike '<*') {
        Require (@($type.Fields | Where-Object { $_.IsStatic -and -not $_.IsInitOnly -and -not $_.IsLiteral }).Count -eq 0) 'No mutable declared static business state'
    }
    $saveCalls = @(Calls @($prototype.Methods | Where-Object Name -eq ExposeData))
    Require (@($saveCalls | Where-Object { $_ -match 'Scribe_Deep::Look|ReserveJournalCodec::Encode|ReserveJournalCodec::Decode' }).Count -ge 3) 'Independent holder/journal persistence missing'
    Require (@($assembly.MainModule.AssemblyReferences | Where-Object Name -eq RUIF).Count -eq 1) 'RUIF hard dependency must remain'

    # Check audited behaviors in the current game DLL, not only in saved decompilation text.
    Require ((Get-FileHash -LiteralPath $gameDll).Hash -eq '5CF1B5BE399D5B1C9C56CA72C9D35B4ECF307FEACF5859D04AC5A1AA5926356A') 'Game assembly changed; reserve isolation needs a new audit'
    $orbital = $game.MainModule.GetType('RimWorld.TradeUtility').NestedTypes | Where-Object Name -match '^<AllLaunchableThingsForTrade>'
    $orbitalCalls = @(Calls $orbital.Methods)
    Require (@($orbitalCalls | Where-Object { $_ -match 'GridsUtility::GetThingList' }).Count -gt 0) 'Orbital trade entry path changed'
    $ground = $game.MainModule.GetType('RimWorld.Pawn_TraderTracker').NestedTypes | Where-Object Name -match '^<ColonyThingsWillingToBuy>'
    $groundCalls = @(Calls $ground.Methods)
    Require (@($groundCalls | Where-Object { $_ -match 'ListerThings::get_AllThings' }).Count -gt 0) 'Ground trade spawned-thing path changed'
    Require (@($groundCalls | Where-Object { $_ -match 'AllColonistBuildingsOfType<RimWorld.IHaulSource>' }).Count -gt 0) 'Ground trade IHaulSource path changed'
    $haulCalls = @(Calls @($game.MainModule.GetType('RimWorld.WorkGiver_Haul').Methods | Where-Object Name -eq PotentialWorkThingsGlobal))
    Require (@($haulCalls | Where-Object { $_ -match 'ListerHaulables::ThingsPotentiallyNeedingHauling' }).Count -eq 1) 'Vanilla haul discovery path changed'

    $trxRelative = 'Tests/TestResults/EconomyLanding-B22-Full.trx'
    $trxPath = Join-Path $repo $trxRelative; $trx = [xml](Get-Content -LiteralPath $trxPath -Raw -Encoding UTF8)
    Require ([int]$trx.TestRun.ResultSummary.Counters.failed -eq 0) 'Full regression failed'
    $ids = @{}
    foreach ($test in $trx.TestRun.TestDefinitions.UnitTest) { if ($test.TestMethod.className -match '\.PawnSilverReserveTests$') { $ids[$test.id] = $true } }
    $newCases = @($trx.TestRun.Results.UnitTestResult | Where-Object { $ids.ContainsKey($_.testId) })
    Require ($newCases.Count -eq 42 -and @($newCases | Where-Object outcome -ne Passed).Count -eq 0) 'Reserve/Scribe cases missing or failing'
    $skipped = @($trx.TestRun.Results.UnitTestResult | Where-Object outcome -eq NotExecuted | ForEach-Object testName)
    Require ($skipped.Count -eq 1 -and $skipped[0] -eq 'Measure_Content_vs_Viewport') 'Unexpected skipped tests'
    Require ((Get-FileHash -LiteralPath $dll).Hash -eq (Get-FileHash -LiteralPath (Join-Path $repo 'Tests/bin/Release/net48/PersonalChronicle.dll')).Hash) 'Tests used a different mod assembly'

    $paths = @('Source/PersonalChronicle/Domain/PawnEconomy/Finance/SilverReserveContracts.cs',
        'Source/PersonalChronicle/Data/PawnEconomy/ReserveJournalCodec.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/SettlementReservePrototype.cs',
        'Tests/PawnSilverReserveTests.cs','Tests/PersonalChronicle.Tests.csproj','_scripts/verify-pawn-silver-reserve.ps1',
        'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/05-B2.2_实物银币储备原型_实装记录.md',
        'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/实施批次状态.json',
        'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/00-实装落地计划_v1.0.md',
        'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/00-总册与实施路线.md',
        'docs/changelog/v1.4.x_2026-10-08_经济AI实物储备原型.md')
    $files = @(foreach ($path in $paths) {
        $absolute = Join-Path $repo $path; $lines = [System.IO.File]::ReadAllLines($absolute, [System.Text.UTF8Encoding]::new($false, $true))
        $anchors = @(for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match '^\s*(public|internal) (sealed |static )*(class|enum)|^\s*public (void ExposeData|ReserveMovementResult Execute|ReserveAuditSnapshot ReadAudit)|^## ') {
                [ordered]@{ line = $i + 1; text = $lines[$i].Trim() }
            }
        })
        [ordered]@{ path = $path; sha256 = (Get-FileHash -LiteralPath $absolute).Hash; anchors = $anchors }
    })
    $sourcePaths = @('Verse/ThingOwner.cs','Verse/ThingOwner.2.cs','Verse/Thing.cs','Verse/IThingHolder.cs',
        'RimWorld/TradeUtility.cs','RimWorld/Pawn_TraderTracker.cs','RimWorld/WorkGiver_Haul.cs')
    $sources = @(foreach ($path in $sourcePaths) {
        $absolute = Join-Path $repo ('_research/vanilla/Assembly-CSharp/' + $path)
        [ordered]@{ path = $path; sha256 = (Get-FileHash -LiteralPath $absolute).Hash }
    })
    $report = [ordered]@{
        changeId = 'CHG-20261008-ECON-SILVER-RESERVE'; batch = 'B2.2'; state = 'CodeVerified'
        checkedUtc = [DateTime]::UtcNow.ToString('o'); modSha256 = (Get-FileHash -LiteralPath $dll).Hash
        gameAssemblyVersion = $game.Name.Version.ToString(); gameSha256 = (Get-FileHash -LiteralPath $gameDll).Hash
        compiledChecks = [ordered]@{ pureDomain = $true; noItemCreation = $true; noGameplayOrGameSaveRoute = $true;
            mergingDisabled = $true; gameplayReadyFalse = $true; parentHolderNull = $true; noIHaulSource = $true;
            independentScribe = $true; orbitalEntryCalls = $orbitalCalls; groundEntryCalls = $groundCalls; haulCalls = $haulCalls }
        tests = [ordered]@{ path = $trxRelative; sha256 = (Get-FileHash -LiteralPath $trxPath).Hash;
            passed = [int]$trx.TestRun.ResultSummary.Counters.passed; failed = 0; skipped = $skipped.Count; skippedNames = $skipped; newCases = $newCases.Count }
        files = $files; gameSourceEvidence = $sources
        limits = @('Internal standalone physical prototype, not attached to GameComponent, map, caravan, UI or game payment routing.',
            'Offline real ThingOwner/ThingMaker/SplitOff/Destroy/Scribe are used with a minimal uninitialized Silver Def fixture.',
            'Test-only shims: Log, DeepProfiler, Find.UniqueIDsManager/Maps/FactionManager and GenTypes.AllTypes; an assembly inventory is registered and restored. No physical transfer or Scribe core methods are patched.',
            'CLR-incompatible engine types are excluded only from the offline type inventory; this is not full game initialization or compatibility acceptance.',
            'Journal net deposits are not account liabilities. Treasury authorization, v2 ledger recovery, original legacy backup/migration and institutional switch remain B2.3/B2.4.',
            'Single source stack per movement. No merge, ground extraction, multi-stack atomic withdrawal, authorized UI, or external trade automation.',
            'Vanilla isolation is source/current-IL evidence plus holder properties; live trade/haul, third-party scans, full game saves, performance and deployment not executed.')
    }
    $output = Join-Path $repo 'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/B2.2_实装验证.json'
    [System.IO.File]::WriteAllText($output, ($report | ConvertTo-Json -Depth 12) + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    Write-Output ('PASS: real holder and standalone Scribe, ' + $newCases.Count + ' new cases; full ' + $report.tests.passed + ' passed, 1 existing skipped; prototype not gameplay-enabled.')
} finally { $assembly.Dispose(); $game.Dispose() }
