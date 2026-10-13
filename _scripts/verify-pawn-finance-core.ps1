#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
function Require([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Types($items) {
    foreach ($item in $items) { $item; if ($item.HasNestedTypes) { Types $item.NestedTypes } }
}
function Calls($methods) {
    @($methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions |
        Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.ToString() } })
}
Add-Type -Path (Join-Path $repo 'InspectTypes/bin/Release/net8.0/Mono.Cecil.dll')
$dll = Join-Path $repo 'Assemblies/PersonalChronicle.dll'
$parameters = [Mono.Cecil.ReaderParameters]::new(); $parameters.InMemory = $true
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll, $parameters)
try {
    $types = @(Types $assembly.MainModule.Types)
    $pure = @($types | Where-Object { $_.FullName -match '^PersonalChronicle\.(Domain|Application)\.PawnEconomy\.Finance\.' })
    Require ($pure.Count -ge 14) 'Finance contracts, kernel or reconciler are missing'
    foreach ($type in $pure) {
        $references = @($type.Fields | ForEach-Object { $_.FieldType.ToString() })
        foreach ($method in $type.Methods) {
            $references += $method.ReturnType.ToString()
            $references += @($method.Parameters | ForEach-Object { $_.ParameterType.ToString() })
            if ($method.HasBody) { $references += @($method.Body.Instructions | ForEach-Object { [string]$_.Operand }) }
        }
        Require (@($references | Where-Object { $_ -match '(Verse|RimWorld|UnityEngine|RUIF|HarmonyLib)\.|PersonalChronicle\.(Data|Economy)\.|EconomyService::' }).Count -eq 0) ('Impure finance type: ' + $type.FullName)
        if ($type.Name -notlike '<*') {
            Require (@($type.Fields | Where-Object { $_.IsStatic -and -not $_.IsInitOnly -and -not $_.IsLiteral }).Count -eq 0) ('Mutable static finance field: ' + $type.FullName)
        }
    }
    $outsideCalls = @(Calls @($types | Where-Object { $_.FullName -notmatch '\.PawnEconomy\.Finance\.' } | ForEach-Object Methods))
    Require (@($outsideCalls | Where-Object { $_ -match 'ShadowTransactionKernel::' }).Count -eq 0) 'Shadow kernel must not be used for runtime payments'
    $kernel = $assembly.MainModule.GetType('PersonalChronicle.Application.PawnEconomy.Finance.ShadowTransactionKernel')
    foreach ($name in @('BackingVerified', 'CanExecuteInGame')) {
        $getter = @($kernel.Methods | Where-Object Name -eq ('get_' + $name))[0]
        Require (@($getter.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldc.i4.0' }).Count -eq 1) ($name + ' must remain false')
        Require (@($getter.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldc.i4.1' }).Count -eq 0) ($name + ' must not claim readiness')
    }
    $economyHostType = $assembly.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.GameComponent_PawnEconomy')
    $query = @($economyHostType.Methods | Where-Object Name -eq QueryLegacyReconciliation)
    Require ($query.Count -eq 1) 'Readonly reconciliation host missing'
    $queryCalls = @(Calls $query)
    Require (@($queryCalls | Where-Object { $_ -match 'LegacyReconciler::Capture\(' }).Count -eq 1) 'Host must use the readonly reconciler'
    Require (@($queryCalls | Where-Object { $_ -match 'EconomyService::|::(Account|Person|Enable|Prepare|Commit|RecoverAfterLoad)\(' }).Count -eq 0) 'Reconciliation query must not change legacy state'
    Require (@($assembly.MainModule.AssemblyReferences | Where-Object Name -eq RUIF).Count -eq 1) 'Existing RUIF hard dependency missing'

    $trxRelative = 'Tests/TestResults/EconomyLanding-B21-Full.trx'
    $trxPath = Join-Path $repo $trxRelative
    $trx = [xml](Get-Content -LiteralPath $trxPath -Raw -Encoding UTF8)
    Require ([int]$trx.TestRun.ResultSummary.Counters.failed -eq 0) 'Full regression failed'
    $ids = @{}
    foreach ($test in $trx.TestRun.TestDefinitions.UnitTest) {
        if ($test.TestMethod.className -match '\.Pawn(FinanceKernel|LegacyReconciliation)Tests$') { $ids[$test.id] = $true }
    }
    $newCases = @($trx.TestRun.Results.UnitTestResult | Where-Object { $ids.ContainsKey($_.testId) })
    Require ($newCases.Count -eq 48 -and @($newCases | Where-Object outcome -ne Passed).Count -eq 0) 'Finance cases missing or failing'
    $skipped = @($trx.TestRun.Results.UnitTestResult | Where-Object outcome -eq NotExecuted | ForEach-Object testName)
    Require ($skipped.Count -eq 1 -and $skipped[0] -eq 'Measure_Content_vs_Viewport') 'Unexpected skipped cases'
    $testedDll = Join-Path $repo 'Tests/bin/Release/net48/PersonalChronicle.dll'
    Require ((Get-FileHash -LiteralPath $dll).Hash -eq (Get-FileHash -LiteralPath $testedDll).Hash) 'Test output assembly differs from current mod assembly'

    $paths = @('Source/PersonalChronicle/Domain/PawnEconomy/Finance/FinancialContracts.cs',
        'Source/PersonalChronicle/Application/PawnEconomy/Finance/ShadowTransactionKernel.cs',
        'Source/PersonalChronicle/Application/PawnEconomy/Finance/LegacyReconciliation.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/EconomyReconciliation.cs',
        'Tests/PawnFinanceKernelTests.cs', 'Tests/PawnLegacyReconciliationTests.cs',
        '_scripts/verify-pawn-finance-core.ps1',
        'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/04-B2.1_事务内核与旧账对账_实装记录.md',
        'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/实施批次状态.json',
        'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/00-实装落地计划_v1.0.md',
        'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/00-总册与实施路线.md',
        'docs/changelog/v1.4.x_2026-10-08_经济AI事务与对账内核.md')
    $utf8 = [System.Text.UTF8Encoding]::new($false, $true)
    $files = @(foreach ($path in $paths) {
        $absolute = Join-Path $repo $path
        $lines = $utf8.GetString([System.IO.File]::ReadAllBytes($absolute)) -split '\r?\n'
        $anchors = @(for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match '^\s*(public|internal) (sealed |static |partial )*(class|enum)|^\s*public FinancialResult |^## ') {
                [ordered]@{ line = $i + 1; text = $lines[$i].Trim() }
            }
        })
        [ordered]@{ path = $path; sha256 = (Get-FileHash -LiteralPath $absolute).Hash; anchors = $anchors }
    })
    $report = [ordered]@{
        changeId = 'CHG-20261008-ECON-FINANCE-CORE'; checkedUtc = [DateTime]::UtcNow.ToString('o')
        state = 'CodeVerified'; batch = 'B2.1'; modSha256 = (Get-FileHash -LiteralPath $dll).Hash
        compiledChecks = [ordered]@{ pureTypesIncludingGenerated = $pure.Count; noMutableDeclaredStaticState = $true;
            shadowKernelNotRuntimeWired = $true; readinessAlwaysFalse = $true; readonlyQueryCalls = $queryCalls; existingRuifDependency = $true }
        tests = [ordered]@{ path = $trxRelative; sha256 = (Get-FileHash -LiteralPath $trxPath).Hash;
            passed = [int]$trx.TestRun.ResultSummary.Counters.passed; failed = 0; skipped = $skipped.Count; skippedNames = $skipped; newCases = $newCases.Count }
        files = $files
        limits = @('Shadow balances and opaque evidence IDs do not prove physical deposits or real fulfillment.',
            'No v2 persistence, raw legacy backup, migration mapping, ThingOwner reserve, settings switch or payment takeover implemented in B2.1.',
            'Legacy report observes current revision only; old load recovery may already have cancelled pending orders.',
            'Legacy diagnostics check bounded financial and identity invariants, not every business reference or original payload field from unknown versions.',
            'No deployment, game acceptance or performance profiling executed; full workspace GATE-DELTA runner absent.')
    }
    $output = Join-Path $repo 'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/B2.1_实装验证.json'
    [System.IO.File]::WriteAllText($output, ($report | ConvertTo-Json -Depth 12) + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    Write-Output ('PASS: finance core, ' + $newCases.Count + ' new cases; full ' + $report.tests.passed + ' passed, ' + $skipped.Count + ' existing skipped; no runtime payment route.')
} finally { $assembly.Dispose() }
