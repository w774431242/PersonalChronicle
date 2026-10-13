param(
    [string]$GameRoot = 'E:\SteamLibrary\steamapps\common\RimWorld',
    [string]$ReportPath = ''
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $repo 'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/B1.2_实装验证.json'
}
Add-Type -Path (Join-Path $repo 'InspectTypes/bin/Release/net8.0/Mono.Cecil.dll')
$gameDll = Join-Path $GameRoot 'RimWorldWin64_Data/Managed/Assembly-CSharp.dll'
$modDll = Join-Path $repo 'Assemblies/PersonalChronicle.dll'
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($gameDll)
$mod = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($modDll)
function Require([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Types($types) {
    foreach ($type in $types) { $type; Types $type.NestedTypes }
}
function Calls($type, [string]$method) {
    @($type.Methods | Where-Object { $_.Name -eq $method -and $_.HasBody } | ForEach-Object {
        $_.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.ToString() }
    })
}
try {
    $allTypes = @(Types $mod.MainModule.Types)
    $core = @($allTypes | Where-Object {
        $_.FullName.StartsWith('PersonalChronicle.Domain.PawnEconomy.Perception.') -or
        $_.FullName.StartsWith('PersonalChronicle.Application.PawnEconomy.Perception.') -or
        $_.FullName -eq 'PersonalChronicle.Application.PawnEconomy.EconomyReadQueries'
    })
    Require ($core.Count -ge 20) 'Missing compiled perception contracts/coordinator'
    foreach ($type in $core) {
        $references = @($type.Fields | ForEach-Object { $_.FieldType.ToString() })
        foreach ($method in $type.Methods) {
            $references += $method.ReturnType.ToString()
            $references += @($method.Parameters | ForEach-Object { $_.ParameterType.ToString() })
            if ($method.HasBody) { $references += @($method.Body.Instructions | ForEach-Object { [string]$_.Operand }) }
        }
        Require (@($references | Where-Object { $_ -match '(Verse|RimWorld|UnityEngine|HarmonyLib|RUIF)\.' }).Count -eq 0) ('Impure core type: ' + $type.FullName)
    }
    $economyHostType = $mod.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.GameComponent_PawnEconomy')
    $tick = @(Calls $economyHostType 'GameComponentTick')
    Require ($tick.Count -gt 1 -and $tick[0] -match '::TickPerception\(') 'Observation must run before fee/ledger gates'
    $finalize = @(Calls $economyHostType 'FinalizeInit')
    Require ($finalize[0] -match '::QueuePerceptionInitialization\(') 'Loading worker must defer perception construction'
    $queueInit = @(Calls $economyHostType 'QueuePerceptionInitialization')
    Require (@($queueInit | Where-Object { $_ -match 'LongEventHandler::ExecuteWhenFinished\(' }).Count -eq 1) 'Perception initialization must use the loading completion queue'
    Require (@($queueInit | Where-Object { $_ -match '::InitializePerception\(' }).Count -eq 0) 'Loading queue must not directly construct the perception source'
    $tryInit = @(Calls $economyHostType 'TryInitializePerception')
    Require ($tryInit[0] -match 'UnityData::get_IsInMainThread\(') 'Deferred source construction must verify main-thread ownership first'
    # Release compilation lowers ReferenceEquals to direct reference comparison branches.
    $tryInitInstructions = @($economyHostType.Methods | Where-Object Name -eq 'TryInitializePerception' | ForEach-Object { $_.Body.Instructions })
    Require (@($tryInitInstructions | Where-Object { $_.OpCode.Name -match '^bne.un' }).Count -eq 2 -and
        @($tryInitInstructions | Where-Object { $_.OpCode.Name -eq 'ldfld' -and [string]$_.Operand -match '::perceptionInitializationToken$' }).Count -eq 1 -and
        @($tryInitInstructions | Where-Object { $_.OpCode.Name -eq 'ldfld' -and [string]$_.Operand -match '::perceptionGame$' }).Count -eq 2) 'Deferred callback must validate both initialization token and owning game'
    Require ($tryInit[-1] -match '::InitializePerception\(') 'Perception construction must follow lifecycle guards'
    $invalidate = @(Calls $economyHostType 'ExposeData')
    Require (@($invalidate | Where-Object { $_ -match '::InvalidatePerception\(' }).Count -eq 1) 'Load must invalidate old callbacks and cached observations'
    $saveCalls = @(Calls $economyHostType 'ExposeData')
    Require (@($saveCalls | Where-Object { $_ -match 'Scribe' -and $_ -match 'Perception' }).Count -eq 0) 'Observation cache must not be serialized'
    $captureTypes = @($allTypes | Where-Object { $_.FullName.StartsWith('PersonalChronicle.Economy.PawnEconomy.Perception.') })
    $captureCalls = @($captureTypes.Methods | Where-Object HasBody | ForEach-Object {
        $_.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.ToString() }
    } | Sort-Object -Unique)
    Require (@($captureCalls | Where-Object { $_ -match '::(StartJob|TryTakeOrderedJob|set_CurLevel|Prepare|Commit|Enable)\(' }).Count -eq 0) 'Capture path must not order jobs, alter needs or transact'
    Require (@($captureCalls | Where-Object { $_ -match '::(BasicPawn|SafeDecision)\(' }).Count -eq 0) 'Observation cannot reuse old action eligibility'
    Require (@($captureCalls | Where-Object { $_ -match '::get_IsFrozen\(' }).Count -eq 0) 'Protected freeze getter was not approved'
    $biotech = $mod.MainModule.GetType('PersonalChronicle.Compatibility.Dlc.PawnLifeBiotechAdapter')
    Require ($null -ne $biotech) 'Biotech adapter missing'
    $gameNames = @('Verse.MapPawns','Verse.Pawn','Verse.Thing','Verse.Hediff','Verse.Hediff_Pregnant',
        'RimWorld.Need','RimWorld.NeedDef','RimWorld.Pawn_NeedsTracker','RimWorld.Need_Play','RimWorld.Need_Learning',
        'RimWorld.Pawn_ApparelTracker','RimWorld.Pawn_RelationsTracker','RimWorld.Pawn_Ownership','Verse.GenTemperature',
        'Verse.LongEventHandler','Verse.UnityData')
    $api = @(foreach ($name in $gameNames) {
        $type = $game.MainModule.GetType($name)
        Require ($null -ne $type) ('Missing current game type: ' + $name)
        [ordered]@{ type = $name; methods = @($type.Methods | Where-Object {
            ($_.IsPublic -or $_.IsFamily) -and $_.Name -match 'CurLevel|MaxLevel|IsFrozen|FreeColonists|MapHeld|PositionHeld|DevelopmentalStage|IsFreeNonSlaveColonist|Visible|Severity|TendableNow|AllNeeds|WornApparel|DirectRelations|OwnedBed|ComfortableTemperatureRange|CurCategory|ExecuteWhenFinished|IsInMainThread'
        } | ForEach-Object ToString); fields = @($type.Fields | Where-Object { $_.IsPublic -and $_.Name -match 'needClass|def|loadID|hediffs' } | ForEach-Object ToString) }
    })
    $trxPath = Join-Path $repo 'Tests/TestResults/EconomyLanding-B12-Full.trx'
    $trx = [xml](Get-Content -LiteralPath $trxPath -Encoding UTF8 -Raw)
    $counter = $trx.TestRun.ResultSummary.Counters
    Require ([int]$counter.failed -eq 0) 'Full regression has failures'
    $ids = @{}
    foreach ($definition in $trx.TestRun.TestDefinitions.UnitTest) {
        if ($definition.TestMethod.className -match '^PersonalChronicle.Tests.PawnPerception') { $ids[$definition.id] = $true }
    }
    $newResults = @($trx.TestRun.Results.UnitTestResult | Where-Object { $ids.ContainsKey($_.testId) })
    Require ($newResults.Count -ge 41 -and @($newResults | Where-Object outcome -ne 'Passed').Count -eq 0) 'New perception cases incomplete or failing'
    $skipNames = @($trx.TestRun.Results.UnitTestResult | Where-Object outcome -eq 'NotExecuted' | ForEach-Object testName)
    $paths = @(
        'Source/PersonalChronicle/Domain/PawnEconomy/Perception/PawnFactSnapshot.cs',
        'Source/PersonalChronicle/Domain/PawnEconomy/Perception/PerceptionReadRequest.cs',
        'Source/PersonalChronicle/Domain/PawnEconomy/Perception/NeedObservation.cs',
        'Source/PersonalChronicle/Application/PawnEconomy/Perception/PerceptionSource.cs',
        'Source/PersonalChronicle/Application/PawnEconomy/Perception/PerceptionCoordinator.cs',
        'Source/PersonalChronicle/Application/PawnEconomy/Perception/PerceptionQuery.cs',
        'Source/PersonalChronicle/Application/PawnEconomy/EconomyReadQueries.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/Perception/VanillaNeedRegistry.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/Perception/VanillaPawnFactCapture.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/Perception/VanillaPawnFactCapture.Body.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/EconomyPerception.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs',
        'Source/PersonalChronicle/Compatibility/Dlc/PawnLifeBiotechAdapter.cs',
        'Tests/PawnPerceptionCoordinatorTests.cs','Tests/PawnPerceptionReadQueriesTests.cs','Tests/PawnPerceptionVanillaNeedTests.cs',
        '_scripts/verify-pawn-perception.ps1',
        'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/02-B1.2人口采集与查询_实装记录.md',
        'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/实施批次状态.json',
        'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/00-总册与实施路线.md',
        'docs/changelog/v1.4.x_2026-10-08_经济AI人口感知与查询.md'
    )
    $utf8 = [System.Text.UTF8Encoding]::new($false, $true)
    $files = @(foreach ($relative in $paths) {
        $path = Join-Path $repo $relative
        $body = $utf8.GetString([System.IO.File]::ReadAllBytes($path)); $lines = $body -split '\r?\n'
        $anchors = @(for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match '^\s*(internal (sealed |static |readonly |partial )*(class|struct|interface|enum)|public sealed class PawnPerception)|^## ') {
                [ordered]@{ line = $i + 1; text = $lines[$i].Trim() }
            }
        })
        [ordered]@{ path = $relative; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash; anchors = $anchors }
    })
    $report = [ordered]@{
        changeId = 'CHG-20261008-ECON-PERCEPTION'; checkedUtc = [DateTime]::UtcNow.ToString('o')
        scope = 'B1.2 compiled contracts/source/runtime integration and offline behavior; no game process executed, no UI addition/deployment/money migration.'
        assembly = [ordered]@{ gameVersion = (Get-Content -LiteralPath (Join-Path $GameRoot 'Version.txt') -Raw).Trim(); gameSha256 = (Get-FileHash -LiteralPath $gameDll).Hash; gameMvid = $game.MainModule.Mvid.ToString(); modSha256 = (Get-FileHash -LiteralPath $modDll).Hash }
        compiledChecks = [ordered]@{ coreTypes = $core.Count; noGameUiReferencesInCore = $true; tickCalls = $tick; finalizeCalls = $finalize; deferredInitializationCalls = $queueInit; guardedInitializationCalls = $tryInit; guardedInitializationIl = @($tryInitInstructions | ForEach-Object ToString); saveCalls = $saveCalls; captureReadOnlyBoundary = $true; actionEligibilityNotReused = $true; protectedFreezeNotCalled = $true }
        gameApi = $api; sourceCalls = $captureCalls; files = $files
        tests = [ordered]@{ path = 'Tests/TestResults/EconomyLanding-B12-Full.trx'; sha256 = (Get-FileHash -LiteralPath $trxPath).Hash; total = [int]$counter.total; passed = [int]$counter.passed; failed = [int]$counter.failed; skipped = $skipNames; newPerceptionCases = $newResults.Count; newPassed = $newResults.Count }
        limits = @('Offline vanilla Need tests shim Verse.Log only; Unity textures, map/holder/DLC execution and game timing remain unverified.',
            'Named work quotas and elapsed diagnostics are implemented, but 20/100/300 Pawn game profiling and allocation measurement have not been performed.',
            'Omitted population entries can include unexamined map/candidate entries; they are not an exact count of unique missing eligible Pawns.',
            'Dirty marking is available but no new Harmony event hook is installed; periodic sampling is the active fallback.',
            'Legacy account snapshots do not verify physical currency backing; B2 migration remains planned.',
            'Historical design/B1.1 fingerprints describe their original source revision; this record fingerprints the changed B1.2 sources.')
    }
    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
    Write-Output ("Perception metadata/source checks passed; new cases {0}; full regression {1} passed, {2} skipped. Runtime/game acceptance unverified." -f $newResults.Count, $counter.passed, $skipNames.Count)
}
finally { $mod.Dispose(); $game.Dispose() }
