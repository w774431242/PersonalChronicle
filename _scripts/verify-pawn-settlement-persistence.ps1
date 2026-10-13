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
    $pure = @($types | Where-Object FullName -match '^PersonalChronicle\.(Domain|Application)\.PawnEconomy\.Finance\.')
    foreach ($type in $pure) {
        $references = @($type.Fields | ForEach-Object { $_.FieldType.ToString() })
        foreach ($method in $type.Methods) {
            $references += $method.ReturnType.ToString(); $references += @($method.Parameters | ForEach-Object { $_.ParameterType.ToString() })
            if ($method.HasBody) { $references += @($method.Body.Instructions | ForEach-Object { [string]$_.Operand }) }
        }
        Require (@($references | Where-Object { $_ -match '(Verse|RimWorld|UnityEngine|RUIF|HarmonyLib)\.|PersonalChronicle\.(Data|Economy)\.|EconomyService::' }).Count -eq 0) ('Impure finance type: ' + $type.FullName)
        if ($type.Name -notlike '<*') {
            Require (@($type.Fields | Where-Object { $_.IsStatic -and -not $_.IsInitOnly -and -not $_.IsLiteral }).Count -eq 0) ('Mutable static finance state: ' + $type.FullName)
        }
    }
    $settlementHostType = $assembly.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.PawnSettlementSaveHost')
    $ledger = $assembly.MainModule.GetType('PersonalChronicle.Application.PawnEconomy.Finance.FundedSettlementLedger')
    $reserve = $assembly.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.SettlementReservePrototype')
    Require ($null -ne $settlementHostType -and $settlementHostType.IsNotPublic -and $null -ne $ledger -and $null -ne $reserve) 'Persistent settlement types missing'
    $settlementHostTypes = @($types | Where-Object { $_.FullName -eq $settlementHostType.FullName -or $_.FullName.StartsWith($settlementHostType.FullName + '/') })
    $settlementHostCalls = @(Calls @($settlementHostTypes | ForEach-Object Methods))
    Require (@($settlementHostCalls | Where-Object { $_ -match 'SettlementReservePrototype::Execute\(' }).Count -eq 1) 'Physical command must pass through one reserve adapter'
    Require (@($settlementHostCalls | Where-Object { $_ -match 'ShadowTransactionKernel::|EconomyService::|ThingMaker::MakeThing|GenSpawn::' }).Count -eq 0) 'New host may not mint objects or use old/shadow funds'
    $outsideCalls = @(Calls @($types | Where-Object { $_.FullName -notin $settlementHostTypes.FullName } | ForEach-Object Methods))
    Require (@($outsideCalls | Where-Object { $_ -match 'PawnSettlementSaveHost::(ExecutePhysical|AllocateLegacy)\(' }).Count -eq 0) 'Gameplay payment takeover is not authorized in B2.3'
    foreach ($type in @($settlementHostType, $reserve)) {
        $ready = @($type.Methods | Where-Object Name -eq get_CanExecuteInGame)[0].Body.Instructions
        Require (@($ready | Where-Object { $_.OpCode.Name -eq 'ldc.i4.0' }).Count -eq 1) ($type.Name + ' must keep game capability false')
        Require (@($ready | Where-Object { $_.OpCode.Name -eq 'ldc.i4.1' }).Count -eq 0) 'Readiness cannot claim activation'
    }
    $component = $assembly.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.GameComponent_PawnEconomy')
    $expose = @($component.Methods | Where-Object Name -eq ExposeData)[0]
    $exposeCalls = @(Calls @($expose))
    $captureIndex = -1; $recoverIndex = -1
    for ($i = 0; $i -lt $exposeCalls.Count; $i++) {
        if ($exposeCalls[$i] -match 'PawnSettlementSaveHost::CaptureLegacy\(') { $captureIndex = $i }
        if ($exposeCalls[$i] -match 'EconomyService::RecoverAfterLoad\(') { $recoverIndex = $i }
    }
    Require ($captureIndex -ge 0 -and $recoverIndex -gt $captureIndex) 'Original legacy archive must precede legacy cancellation'
    Require (@($expose.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'pawnEconomicSettlement' }).Count -eq 1) 'Stable sidecar save key missing'
    Require (@($settlementHostCalls | Where-Object { $_ -match 'Convert::ToBase64String|Convert::FromBase64String' }).Count -eq 2) 'Raw text must have explicit lossless wire encoding'
    $complete = @($ledger.Methods | Where-Object Name -eq CompletePhysical)[0]
    $proofCalls = @(Calls @($complete))
    Require (@($proofCalls | Where-Object { $_ -match 'ReserveMovementEvidence::get_HasExactQuantityProof' }).Count -eq 1) 'Funding must require two-end quantity evidence'
    Require (@($proofCalls | Where-Object { $_ -match 'ReserveMovementIntent::SameAs' }).Count -eq 1) 'Funding must bind the full original movement request'
    $reserveCalls = @(Calls $reserve.Methods)
    Require (@($reserveCalls | Where-Object { $_ -match 'ThingMaker::MakeThing|GenSpawn::|EconomyService::' }).Count -eq 0) 'Reserve cannot manufacture coins or use old balances'
    Require (@($assembly.MainModule.AssemblyReferences | Where-Object Name -eq RUIF).Count -eq 1) 'RUIF hard dependency missing'
    $gameHash = (Get-FileHash -LiteralPath $gameDll).Hash
    Require ($gameHash -eq '5CF1B5BE399D5B1C9C56CA72C9D35B4ECF307FEACF5859D04AC5A1AA5926356A') 'Game assembly changed; re-audit native save/movement contracts'
    $nativeSaver = $game.MainModule.GetType('Verse.ScribeSaver')
    $nativeWrite = @(Calls @($nativeSaver.Methods | Where-Object Name -eq WriteElement))
    Require (@($nativeWrite | Where-Object { $_ -match 'XmlWriter::WriteElementString' }).Count -eq 1) 'Native XML writer contract changed'
    $trxRelative = 'Tests/TestResults/EconomyLanding-B23-Full.trx'
    $trxPath = Join-Path $repo $trxRelative
    $trx = [xml](Get-Content -LiteralPath $trxPath -Raw -Encoding UTF8)
    Require ([int]$trx.TestRun.ResultSummary.Counters.failed -eq 0 -and [int]$trx.TestRun.ResultSummary.Counters.passed -ge 828) 'Full regression missing or failed'
    $ids = @{}
    foreach ($test in $trx.TestRun.TestDefinitions.UnitTest) {
        if ($test.TestMethod.className -match '\.PawnSettlementLedgerTests$' -or $test.TestMethod.name -like 'B23_*') { $ids[$test.id] = $true }
    }
    $newCases = @($trx.TestRun.Results.UnitTestResult | Where-Object { $ids.ContainsKey($_.testId) })
    Require ($newCases.Count -eq 40 -and @($newCases | Where-Object outcome -ne Passed).Count -eq 0) 'B2.3 behavior and persistence cases missing or failing'
    $skipped = @($trx.TestRun.Results.UnitTestResult | Where-Object outcome -eq NotExecuted | ForEach-Object testName)
    Require ($skipped.Count -eq 1 -and $skipped[0] -eq 'Measure_Content_vs_Viewport') 'Unexpected skipped cases'
    Require ((Get-FileHash -LiteralPath $dll).Hash -eq (Get-FileHash -LiteralPath (Join-Path $repo 'Tests/bin/Release/net48/PersonalChronicle.dll')).Hash) 'Current DLL differs from tested DLL'
    $base = 'docs/设计文档/功能模块/经济体系/经济需求驱动AI/'
    $paths = @('Source/PersonalChronicle/Domain/PawnEconomy/Finance/SettlementContracts.cs',
        'Source/PersonalChronicle/Application/PawnEconomy/Finance/FundedSettlementLedger.cs',
        'Source/PersonalChronicle/Data/PawnEconomy/SettlementLedgerCodec.cs',
        'Source/PersonalChronicle/Data/PawnEconomy/LegacySettlementArchive.cs',
        'Source/PersonalChronicle/Data/PawnEconomySaveData.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/PawnSettlementSaveHost.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/SettlementReservePrototype.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs',
        'Tests/PawnSilverReserveTests.cs', 'Tests/PawnSettlementPersistenceTests.cs',
        '_scripts/verify-pawn-settlement-persistence.ps1',
        ($base + '00-总纲与共享/03-实装批次与状态/06-B2.3_持久事务与旧账保护_实装记录.md'),
        ($base + '00-总纲与共享/03-实装批次与状态/实施批次状态.json'), ($base + '00-总纲与共享/03-实装批次与状态/00-实装落地计划_v1.0.md'),
        ($base + '00-总纲与共享/04-实装指导手册/00-总册与实施路线.md'),
        ($base + 'M11-资金资源权利与制度协调/11-资金资源权利与制度协调_代码设计方案.md'),
        'docs/changelog/v1.4.x_2026-10-08_经济AI持久事务与旧账保护.md')
    $utf8 = [System.Text.UTF8Encoding]::new($false, $true)
    $files = @(foreach ($path in $paths) {
        $absolute = Join-Path $repo $path
        $lines = $utf8.GetString([System.IO.File]::ReadAllBytes($absolute)) -split '\r?\n'
        $anchors = @(for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match '^\s*(public|internal) (sealed |static |partial )*(class|enum)|^## ') { [ordered]@{ line = $i + 1; text = $lines[$i].Trim() } }
        })
        [ordered]@{ path = $path; sha256 = (Get-FileHash -LiteralPath $absolute).Hash; anchors = $anchors }
    })
    $report = [ordered]@{
        changeId = 'CHG-20261008-ECON-PERSISTENCE'; batch = 'B2.3'; state = 'CodeVerified'; checkedUtc = [DateTime]::UtcNow.ToString('o')
        modSha256 = (Get-FileHash -LiteralPath $dll).Hash; gameSha256 = $gameHash; gameAssemblyVersion = $game.Name.Version.ToString()
        compiledChecks = [ordered]@{ pureFinanceTypesIncludingGenerated = $pure.Count; sidecarSaveWired = $true;
            paymentRuntimeWired = $false; readinessAlwaysFalse = $true; archiveBeforeLegacyRecovery = $true;
            fullProofBinding = $true; losslessTextWireEncoding = $true; ruifDependency = $true; nativeXmlWriteCalls = $nativeWrite }
        tests = [ordered]@{ path = $trxRelative; sha256 = (Get-FileHash -LiteralPath $trxPath).Hash;
            passed = [int]$trx.TestRun.ResultSummary.Counters.passed; failed = 0; skipped = $skipped.Count; skippedNames = $skipped; newCases = $newCases.Count }
        gameSaveWired = $true; offlineHostScribeVerified = $true; finalLegacyCutoverComplete = $false; backingVerified = $false
        deployedThisChange = $false; gameVerifiedThisChange = $false; globalGateAvailable = (Test-Path (Join-Path $repo 'scripts/gate-runner.ps1'))
        files = $files
        limits = @('Sidecar is mounted but no gameplay deposit, withdrawal or legacy allocation command calls exist outside the internal host.',
            'Legacy working recovery still cancels pending orders; the original payload and rights are independently preserved before it.',
            'Initial archive is not final cutover settlement. B2.4 must stop the old route, reconcile changes and protect nonmonetary rights before any activation.',
            'Only balances and unpaid wages have explicit funded cash allocation rules. Orders, contracts and asset titles remain preserved, not activated.',
            'Offline tests use minimal Silver Def, compatible type inventory and existing Unity host shims. Core Scribe/Thing transfer methods are not patched.',
            'No deployment, complete game/save acceptance, UI inspection, performance profiling or third-party trade/haul acceptance performed.')
    }
    $output = Join-Path $repo ($base + '00-总纲与共享/03-实装批次与状态/B2.3_实装验证.json')
    [System.IO.File]::WriteAllText($output, ($report | ConvertTo-Json -Depth 12) + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    Write-Output ('PASS B2.3: ' + $newCases.Count + ' new cases; ' + $report.tests.passed + ' full passed, 1 existing skip; save sidecar only, payments/game off.')
}
finally { $assembly.Dispose(); $game.Dispose() }
