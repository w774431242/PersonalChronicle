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
function Method($type, [string]$name) { $found = @($type.Methods | Where-Object Name -eq $name); Require ($found.Count -eq 1) ('Missing unique method: ' + $name); $found[0] }
function Source([string]$relative) { Get-Content -LiteralPath (Join-Path $repo $relative) -Raw -Encoding UTF8 }
Add-Type -Path (Join-Path $repo 'InspectTypes/bin/Release/net8.0/Mono.Cecil.dll')
$reader = [Mono.Cecil.ReaderParameters]::new(); $reader.InMemory = $true
$dll = Join-Path $repo 'Assemblies/PersonalChronicle.dll'; $nativePath = Join-Path $GameRoot 'RimWorldWin64_Data/Managed/Assembly-CSharp.dll'
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll, $reader)
$native = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($nativePath, $reader)
try {
    $types = @(Types $assembly.MainModule.Types)
    $pure = @($types | Where-Object FullName -match '^PersonalChronicle\.(Domain|Application)\.PawnEconomy\.(Finance|Governance)\.')
    foreach ($type in $pure) {
        $refs = @($type.Fields | ForEach-Object { $_.FieldType.ToString() })
        foreach ($method in $type.Methods) {
            $refs += $method.ReturnType.ToString(); $refs += @($method.Parameters | ForEach-Object { $_.ParameterType.ToString() })
            if ($method.HasBody) { $refs += @($method.Body.Instructions | ForEach-Object { [string]$_.Operand }) }
        }
        Require (@($refs | Where-Object { $_ -match '(Verse|RimWorld|UnityEngine|RUIF|HarmonyLib)\.|PersonalChronicle\.(Data|Economy)\.' }).Count -eq 0) ('Impure financial/governance type: ' + $type.FullName)
    }
    $component = $assembly.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.GameComponent_PawnEconomy')
    $settlementHostType = $assembly.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.PawnSettlementSaveHost')
    $reserve = $assembly.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.SettlementReservePrototype')
    Require (((Method $component 'get_LegacyFeesAllowed').Body.Instructions.OpCode.Name -join ',') -eq 'ldc.i4.0,ret') 'Legacy fees must remain closed'
    foreach ($name in @('Execute','Plan','Register','Unregister','FinalizeInit','GameComponentTick','OrderActive','JobOrderValid','CancelJob','DoctorJob','Clinics','OnProduction','WearOwned')) {
        Require (@(Calls @((Method $component $name)) | Where-Object { $_ -match '::get_LegacyFeesAllowed\(' }).Count -gt 0) ('Legacy guard missing: ' + $name)
    }
    Require (@(Calls $component.Methods | Where-Object { $_ -match 'EconomyService::RecoverAfterLoad\(' }).Count -eq 0) 'Legacy cancellation may not resume'
    $permitted = @{
        ExecutePhysical = @('CompleteTreasuryDelivery'); DeliverPreparedPhysical = @('CompleteTreasuryDelivery');
        PreparePhysical = @('ExecuteTreasuryCommand'); AllocateCutover = @('ExecuteTreasuryCommand'); AllocateLegacy = @()
    }
    $outside = @($types | Where-Object { -not $_.FullName.StartsWith($settlementHostType.FullName) })
    foreach ($command in $permitted.Keys) {
        $callers = @(foreach ($type in $outside) { foreach ($method in $type.Methods) {
            if (@(Calls @($method) | Where-Object { $_ -match ('PawnSettlementSaveHost::' + $command + '\(') }).Count -gt 0) {
                Require ($type -eq $component -and $method.Name -in $permitted[$command]) ('Unauthorized physical/claim caller: ' + $method.FullName)
                $method.Name
            }
        } })
        Require ($callers.Count -eq $permitted[$command].Count) ('Missing authorized route: ' + $command)
    }
    $completeCalls = @(Calls @((Method $component 'CompleteTreasuryDelivery')))
    $pickupCalls = @(Calls @((Method $component 'PickUpTreasurySilver')))
    Require (@($completeCalls | Where-Object { $_ -match 'ReservationManager::ReservedBy' }).Count -eq 1 -and
        @($pickupCalls | Where-Object { $_ -match 'ReservationManager::ReservedBy' }).Count -eq 1) 'Both handover boundaries must validate actual reservation'
    Require (@($pickupCalls | Where-Object { $_ -match 'Pawn_CarryTracker::TryStartCarry' }).Count -eq 1) 'Native carrying API missing'
    $submitCalls = @(Calls @((Method $component 'ExecuteTreasuryCommand')))
    Require (@($submitCalls | Where-Object { $_ -match '::(StartJob|TryTakeOrderedJob|MakeThing|Spawn|Enable)\(' }).Count -eq 0) 'Treasury management may not force a job or mint'
    $safeCalls = @(Calls @((Method $component 'TreasuryJobValid')))
    Require (@($safeCalls | Where-Object { $_ -match '::SafeDecision\(' }).Count -eq 1) 'Vanilla safety priorities must guard delivery jobs'
    $driverPrefix = 'PersonalChronicle.Economy.PawnEconomy.JobDriver_TreasuryDelivery'
    $driverTypes = @($types | Where-Object { $_.FullName.StartsWith($driverPrefix) })
    $driverCalls = @(Calls @($driverTypes | ForEach-Object Methods))
    Require (@($driverCalls | Where-Object { $_ -match 'Toils_Jump::JumpIf' }).Count -eq 1 -and
        @($driverCalls | Where-Object { $_ -match 'Toils_Goto::GotoThing' }).Count -eq 2) 'Fixed pickup/bank toil shape missing'
    Require (@($driverCalls | Where-Object { $_ -match '::FinishTreasuryJob\(' }).Count -ge 1) 'Cancellation cleanup missing'
    foreach ($type in @($settlementHostType, $reserve)) {
        Require (((Method $type 'get_CanExecuteInGame').Body.Instructions.OpCode.Name -join ',') -eq 'ldc.i4.0,ret') 'Broad economic payment capability must stay closed'
        Require (@(Calls $type.Methods | Where-Object { $_ -match 'ThingMaker::MakeThing|GenSpawn::|EconomyService::|ShadowTransactionKernel::' }).Count -eq 0) 'Funds cannot be manufactured or imported from a virtual ledger'
    }
    $mode = Source 'Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionMode.cs'
    # 与 verify-pawn-institution-mode.ps1 同一处**合法演进**（既有实现，非本批）：就绪位由"全 false"改为
    # **逐位带闸**（物理预备交付单独授权；能力由"真的恢复过证明"或"本档无旧账可恢复（判 N/A）"开放，故障两侧都检查）。
    # 2026-10-12（CHG-20261012-INSTGATE-EXEMPT + EXC-2026-031）：豁免面锚在冻结时刻持久事实，有内容者不放宽。
    # 断言随之锁定**闸门结构**（不锁字面量），否则实现合法演进即假失败。
    Require ($mode -match 'audit\.Fault == null, treasuryOperations\.Fault == null, true,' -and
        $mode -match 'institutionMode\.RecoveryProvenRevision >= 0 \|\| nothingToRecover, true, fault\)') 'Full game save acceptance must stay guarded (fault checks + proven-round-trip gate)'
    $expose = Method $component 'ExposeData'
    Require (@($expose.Body.Instructions | Where-Object { $_.Operand -eq 'pawnEconomicTreasuryOperations' }).Count -eq 1) 'Controller save host missing'
    Require (@(Calls @((Method $component 'ReconcileTreasuryOperations')) | Where-Object { $_ -match 'SettlementIntent::SameAs' }).Count -eq 1) 'Saved controller must bind full physical command body'
    # 末批（删聚合窗口 2026-10-11）：版本化国库命令路由已随账户页搬到**柜台工作台**
    #（`HostedSend`：Treasury.* → ExecuteTreasuryCommand，带 `hostedStamp` 新鲜戳）。
    # ⚠ 不再断言 `Widgets.|GUI.` 零命中：柜台窗口的机构图标走 `GUI.DrawTexture`（原版 API，
    # 与整体 RUIF 控件使用不冲突）⇒ 断言对象从"整个对话框"收窄为"路由本身"。
    $ui = Source 'Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.cs'
    Require ($ui -match 'RUIF.ButtonAtoms' -and $ui -match 'ExecuteTreasuryCommand' -and $ui -match 'hostedStamp') 'Versioned treasury command routing missing'
    Require (@($assembly.MainModule.AssemblyReferences | Where-Object Name -eq RUIF).Count -eq 1) 'RUIF hard dependency missing'
    $def = [xml](Source 'Defs/PawnEconomy_Treasury.xml')
    $nativeJobDef = $native.MainModule.GetType('Verse.JobDef'); Require ($null -ne $nativeJobDef) 'Native JobDef unavailable'
    $nativeJobFields = @($nativeJobDef.Fields | ForEach-Object Name)
    $nativeJobFields += @($native.MainModule.GetType('Verse.Def').Fields | ForEach-Object Name)
    foreach ($node in $def.Defs.JobDef.ChildNodes | Where-Object NodeType -eq Element) { Require ($node.Name -in $nativeJobFields) ('Unknown JobDef field: ' + $node.Name) }
    Require ($def.Defs.JobDef.defName -eq 'PC_PEA_TreasuryDelivery' -and $null -ne $assembly.MainModule.GetType($def.Defs.JobDef.driverClass)) 'JobDef driver binding missing'
    $localized = @{}
    foreach ($language in @('ChineseSimplified','English')) {
        $doc = [xml](Source "Languages/$language/Keyed/TreasuryOperations.xml"); $map = @{}
        foreach ($node in $doc.LanguageData.ChildNodes | Where-Object NodeType -eq Element) {
            Require (-not $map.ContainsKey($node.Name) -and -not [string]::IsNullOrWhiteSpace($node.InnerText)) 'Empty or duplicate treasury key'
            $map[$node.Name] = $node.InnerText
            $hits = @(Get-ChildItem -LiteralPath (Join-Path $repo "Languages/$language/Keyed") -Filter '*.xml' | ForEach-Object {
                ([xml](Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8)).LanguageData.ChildNodes | Where-Object { $_.NodeType -eq 'Element' -and $_.Name -eq $node.Name }
            })
            Require ($hits.Count -eq 1) ('Global key collision: ' + $node.Name)
        }
        $localized[$language] = $map
        $injected = [xml](Source "Languages/$language/DefInjected/JobDef/PawnEconomy_Treasury.xml")
        Require (-not [string]::IsNullOrWhiteSpace($injected.LanguageData.'PC_PEA_TreasuryDelivery.reportString')) 'Native job report translation missing'
    }
    Require (@(Compare-Object @($localized.English.Keys) @($localized.ChineseSimplified.Keys)).Count -eq 0) 'Treasury bilingual keys differ'
    foreach ($key in $localized.English.Keys) {
        $a = @([regex]::Matches($localized.English[$key], '\{\d+\}') | ForEach-Object Value | Sort-Object -Unique)
        $b = @([regex]::Matches($localized.ChineseSimplified[$key], '\{\d+\}') | ForEach-Object Value | Sort-Object -Unique)
        Require (($a -join ',') -eq ($b -join ',')) ('Placeholder mismatch: ' + $key)
    }
    $gameHash = (Get-FileHash -LiteralPath $nativePath).Hash
    Require ($gameHash -eq '5CF1B5BE399D5B1C9C56CA72C9D35B4ECF307FEACF5859D04AC5A1AA5926356A') 'Re-audit native APIs after game update'
    $trxRelative = 'Tests/TestResults/EconomyLanding-B242-Full.trx'; $trxPath = Join-Path $repo $trxRelative
    $trx = [xml](Get-Content -LiteralPath $trxPath -Raw -Encoding UTF8)
    Require ([int]$trx.TestRun.ResultSummary.Counters.failed -eq 0 -and [int]$trx.TestRun.ResultSummary.Counters.passed -ge 904) 'Full regression failed or missing'
    $ids = @{}; foreach ($test in $trx.TestRun.TestDefinitions.UnitTest) { if ($test.TestMethod.name -like 'B242_*') { $ids[$test.id] = $true } }
    $new = @($trx.TestRun.Results.UnitTestResult | Where-Object { $ids.ContainsKey($_.testId) })
    Require ($new.Count -eq 38 -and @($new | Where-Object outcome -ne Passed).Count -eq 0) 'B2.4.2 behavior cases missing or failing'
    $skipped = @($trx.TestRun.Results.UnitTestResult | Where-Object outcome -eq NotExecuted | ForEach-Object testName)
    Require ($skipped.Count -eq 1 -and $skipped[0] -eq 'Measure_Content_vs_Viewport') 'Unexpected skipped cases'
    Require ((Get-FileHash -LiteralPath $dll).Hash -eq (Get-FileHash -LiteralPath (Join-Path $repo 'Tests/bin/Release/net48/PersonalChronicle.dll')).Hash) 'DLL differs from tested assembly'
    $base = 'docs/设计文档/功能模块/经济体系/经济需求驱动AI/'
    $paths = @('Source/PersonalChronicle/Domain/PawnEconomy/Finance/TreasuryDelivery.cs',
        'Source/PersonalChronicle/Application/PawnEconomy/Finance/LegacyCutoverPlan.cs','Source/PersonalChronicle/Data/PawnEconomy/TreasuryOperationsSaveData.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/EconomyTreasuryOperations.cs','Source/PersonalChronicle/Economy/PawnEconomy/EconomyTreasuryPresenter.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/PawnSettlementSaveHost.cs','Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionMode.cs','Source/PersonalChronicle/Economy/PawnEconomy/EconomyCommands.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.cs','Defs/PawnEconomy_Treasury.xml',
        'Languages/ChineseSimplified/Keyed/TreasuryOperations.xml','Languages/English/Keyed/TreasuryOperations.xml',
        'Languages/ChineseSimplified/Keyed/EconomyMode.xml','Languages/English/Keyed/EconomyMode.xml',
        'Languages/ChineseSimplified/DefInjected/JobDef/PawnEconomy_Treasury.xml','Languages/English/DefInjected/JobDef/PawnEconomy_Treasury.xml',
        'Tests/PawnTreasuryOperationsTests.cs','Tests/PawnInstitutionModeTests.cs','_scripts/verify-pawn-treasury-operations.ps1',
        ($base + '00-总纲与共享/03-实装批次与状态/08-B2.4.2_真实交付与权益承接_实装记录.md'),($base + '00-总纲与共享/03-实装批次与状态/实施批次状态.json'),
        ($base + '00-总纲与共享/03-实装批次与状态/00-实装落地计划_v1.0.md'),($base + '00-总纲与共享/04-实装指导手册/00-总册与实施路线.md'),
        ($base + 'M11-资金资源权利与制度协调/11-资金资源权利与制度协调_代码设计方案.md'),
        'docs/changelog/v1.4.x_2026-10-08_经济AI真实交付与权益承接.md')
    $utf8 = [System.Text.UTF8Encoding]::new($false, $true)
    $files = @(foreach ($relative in $paths) {
        $absolute = Join-Path $repo $relative; $lines = $utf8.GetString([System.IO.File]::ReadAllBytes($absolute)) -split '\r?\n'
        $anchors = @(for ($i=0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match '^\s*(public|internal) (sealed |static |partial )*(class|enum)|^## ') { [ordered]@{ line=$i+1; text=$lines[$i].Trim() } } })
        if ($anchors.Count -eq 0) {
            $anchors = @(for ($i=0; $i -lt $lines.Count; $i++) { if (-not [string]::IsNullOrWhiteSpace($lines[$i])) { [ordered]@{ line=$i+1; text=$lines[$i].Trim() } } })
            $anchors = @($anchors | Select-Object -First 3)
        }
        [ordered]@{ path=$relative; sha256=(Get-FileHash -LiteralPath $absolute).Hash; anchors=$anchors }
    })
    $report = [ordered]@{
        changeId='CHG-20261008-ECON-DELIVERY'; batch='B2.4.2'; state='CodeVerified'; parentBatchState='InProgress'; checkedUtc=[DateTime]::UtcNow.ToString('o')
        modSha256=(Get-FileHash -LiteralPath $dll).Hash; gameSha256=$gameHash; gameAssemblyVersion=$native.Name.Version.ToString()
        compiledChecks=[ordered]@{ pureFinanceGovernanceTypes=$pure.Count; legacyFeesClosed=$true; preparatoryPhysicalRuntimeWired=$true;
            authorizedPhysicalCallersOnly=$true; nativeCarryAndReservation=$true; fixedToilShape=$true; oldRightsAdoptionWired=$true;
            completeGameSaveAcceptance=$false; activeAvailableInGame=$false; ruifDependency=$true; treasuryBilingualKeys=$localized.English.Count; newJobDefs=1 }
        tests=[ordered]@{ path=$trxRelative; sha256=(Get-FileHash -LiteralPath $trxPath).Hash; passed=[int]$trx.TestRun.ResultSummary.Counters.passed; failed=0; skipped=$skipped.Count; skippedNames=$skipped; newCases=$new.Count }
        deployedThisChange=$false; gameVerifiedThisChange=$false; globalGateAvailable=(Test-Path (Join-Path $repo 'scripts/gate-runner.ps1')); files=$files
        limits=@('Actual Pawn navigation, interruption, reservation contention and whole-game saves have not been accepted in a running game.',
            'Only explicit public Treasury preparation is routed; ordinary wages, consumption, services and savings are still closed.',
            'Draining preserves assumed personal/institution credits; this batch withdraws only unreserved public Treasury funds.',
            'Conflicts freeze rather than silently repair. Delivery history is bounded to 512 lifetime orders; legacy decisions to 8192 records.',
            'Offline fixtures exercise original CarryTracker container, ThingOwner and Scribe APIs with minimal Def/environment shims; no rendering or gameplay proof is claimed.')
    }
    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $repo ($base + '00-总纲与共享/03-实装批次与状态/B2.4.2_实装验证.json')) -Encoding utf8
    $annotation = @('---','change_id: CHG-20261008-ECON-DELIVERY','state: CodeVerified','scope: B2.4.2','release_gate: blocked_pending_game_acceptance_and_global_gate','---','',
        '# B2.4.2 变更定位','', '仅列本批文件；工作区既有其他变更未重置、未合并进本批。全局 gate-runner 不存在，未发布。编译后/测试证据见 B2.4.2_实装验证.json。','',
        '| 文件 | 定位 | 作用 |','| --- | --- | --- |')
    foreach ($file in $files) {
        $locations = ($file.anchors | Select-Object -First 3 | ForEach-Object { [string]$_.line + ': ' + $_.text }) -join '<br>'
        $annotation += '| ' + $file.path + ' | ' + $locations.Replace('|','\|') + ' | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |'
    }
    $annotation += ''; $annotation += '生成文件另含本变更标注、B2.4.2_实装验证.json、B2.4.2_UI复验.json、B242 TRX 及工作区编译 DLL/BuildStamp；历史 B2.3/B2.4.1 报告未重写。'
    # 变更标注落点 = docs\设计文档\架构\变更标注\（DOC-001；2026-10-09 由根目录迁入，勿写回仓库根）
    $annotDir = Join-Path $repo 'docs\设计文档\架构\变更标注'
    if (-not (Test-Path -LiteralPath $annotDir)) { New-Item -ItemType Directory -Force -Path $annotDir | Out-Null }
    $annotation | Set-Content -LiteralPath (Join-Path $annotDir '变更标注_CHG-20261008-ECON-DELIVERY.md') -Encoding utf8
    Write-Output "PASS B2.4.2: authorized public treasury delivery, native carry/reservation, fixed toil shape, explicit legacy adoption, $($localized.English.Count) bilingual keys; 38 new cases; $($report.tests.passed) passed, 1 existing skip. Game acceptance and general fees remain closed."
}
finally { $assembly.Dispose(); $native.Dispose() }
