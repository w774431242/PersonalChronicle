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
function Method($type, [string]$name) {
    $found = @($type.Methods | Where-Object Name -eq $name)
    Require ($found.Count -eq 1) ('Expected one method: ' + $type.FullName + '.' + $name)
    return $found[0]
}
function ReadSource([string]$path) { Get-Content -LiteralPath (Join-Path $repo $path) -Raw -Encoding UTF8 }
Add-Type -Path (Join-Path $repo 'InspectTypes/bin/Release/net8.0/Mono.Cecil.dll')
$parameters = [Mono.Cecil.ReaderParameters]::new(); $parameters.InMemory = $true
$dll = Join-Path $repo 'Assemblies/PersonalChronicle.dll'
$gameDll = Join-Path $GameRoot 'RimWorldWin64_Data/Managed/Assembly-CSharp.dll'
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll, $parameters)
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($gameDll, $parameters)
try {
    $types = @(Types $assembly.MainModule.Types)
    $pure = @($types | Where-Object FullName -match '^PersonalChronicle\.(Domain|Application)\.PawnEconomy\.Governance\.')
    Require ($pure.Count -ge 5) 'Governance contracts or state machine missing'
    foreach ($type in $pure) {
        $references = @($type.Fields | ForEach-Object { $_.FieldType.ToString() })
        foreach ($method in $type.Methods) {
            $references += $method.ReturnType.ToString(); $references += @($method.Parameters | ForEach-Object { $_.ParameterType.ToString() })
            if ($method.HasBody) { $references += @($method.Body.Instructions | ForEach-Object { [string]$_.Operand }) }
        }
        Require (@($references | Where-Object { $_ -match '(Verse|RimWorld|UnityEngine|RUIF|HarmonyLib)\.|PersonalChronicle\.(Data|Economy)\.|EconomyService::' }).Count -eq 0) ('Impure governance: ' + $type.FullName)
        if ($type.Name -notlike '<*') {
            Require (@($type.Fields | Where-Object { $_.IsStatic -and -not $_.IsInitOnly -and -not $_.IsLiteral }).Count -eq 0) 'Mutable static governance state'
        }
    }
    $component = $assembly.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.GameComponent_PawnEconomy')
    $gate = Method $component 'get_LegacyFeesAllowed'
    Require (($gate.Body.Instructions.OpCode.Name -join ',') -eq 'ldc.i4.0,ret') 'Legacy fee route must be unconditionally closed'
    $gated = @('Execute', 'Plan', 'Register', 'Unregister', 'FinalizeInit', 'GameComponentTick', 'OrderActive', 'JobOrderValid', 'CancelJob', 'DoctorJob', 'Clinics', 'OnProduction', 'WearOwned')
    foreach ($name in $gated) {
        Require (@(Calls @((Method $component $name)) | Where-Object { $_ -match '::get_LegacyFeesAllowed\(' }).Count -ge 1) ('Legacy gate missing: ' + $name)
    }
    foreach ($name in @('FinishVisit', 'Pulse')) {
        Require (@(Calls @((Method $component $name)) | Where-Object { $_ -match '::OrderActive\(' }).Count -eq 1) ('Legacy order validation missing: ' + $name)
    }
    $componentCalls = @(Calls $component.Methods)
    Require (@($componentCalls | Where-Object { $_ -match 'EconomyService::RecoverAfterLoad\(' }).Count -eq 0) 'Legacy recovery may not cancel preserved rights'
    Require (@(Calls @((Method $component 'PlanPreservedAssetUse')) | Where-Object { $_ -match 'EconomyService::|PawnSettlementSaveHost::(ExecutePhysical|AllocateLegacy)' }).Count -eq 0) 'Owned asset use may not settle or create financial records'
    $expose = Method $component 'ExposeData'
    Require (@($expose.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'pawnEconomicMode' }).Count -eq 1) 'Stable mode save key missing'
    $modeCalls = @(Calls @((Method $component 'ApplyInstitutionModeSetting')))
    Require (@($modeCalls | Where-Object { $_ -match 'InstitutionModeSaveData::Freeze\(' }).Count -eq 1) 'Cutover freeze missing'
    Require (@($modeCalls | Where-Object { $_ -match 'PawnEconomySaveData::Encode\(' }).Count -ge 1) 'Latest working legacy state must be archived independently'
    $modeSource = ReadSource 'Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionMode.cs'
    # B2.4.1 语义**合法演进**（既有实现，非本批）：就绪位由"与故障无关的全 false"改为**逐位带闸** ——
    # 物理预备交付单独授权（`true`），能力由"真的恢复过证明"或"本档无旧账可恢复（判 N/A）"开放，
    # 且资金/交付两侧都挂故障检查（见 EconomyInstitutionMode.cs 的就绪构造与其上方注释）。
    # 2026-10-12（CHG-20261012-INSTGATE-EXEMPT + EXC-2026-031，用户裁决）：豁免面锚在**冻结时刻的持久事实**
    # （旧账快照 + LedgerVersionAtFreeze）；有旧账、或冻结时账本已有条目的档**语义不放宽**。
    # 断言随之改为锁定**闸门结构**（不锁字面量），否则实现合法演进即假失败。
    Require ($modeSource -match 'new InstitutionReadiness\(institutionMode\.Frozen, institutionMode\.Frozen && !oldRights,' -and
        $modeSource -match 'audit\.Fault == null, treasuryOperations\.Fault == null, true,' -and
        $modeSource -match 'institutionMode\.RecoveryProvenRevision >= 0 \|\| nothingToRecover, true, fault\)') 'Readiness must stay guarded (fault checks + proven-round-trip capability gate)'
    $hostTypes = @($types | Where-Object { $_.FullName -eq 'PersonalChronicle.Economy.PawnEconomy.PawnSettlementSaveHost' -or $_.FullName.StartsWith('PersonalChronicle.Economy.PawnEconomy.PawnSettlementSaveHost/') })
    # B2.4.1 后的**合法接线**（既有实现，非本批）：缴存 / 提取的**实物交付完成**会调用物理执行 ——
    # 那是"真钱背书"的唯一通道（银币真的被搬到银行才入账），上游 `TreasuryAuthorized` 有完整闸门，
    # 且 `CanExecuteInGame => false`（下方断言）仍守住"命令不得凭空执行"。
    # 断言随之由"零调用方"收紧为**唯一调用方**，并保留"旧权益分配不得接线"（AllocateLegacy 零调用）。
    $otherCalls = @(Calls @($types | Where-Object { $_.FullName -notin $hostTypes.FullName } | ForEach-Object Methods))
    # ⚠ `Calls` 返回的是**被调用方**（callee）的目标串，按**调用点数**计 —— 因此"唯一调用方"的正确口径是
    #   "全工程指向 ExecutePhysical 的调用点数" == "交付完成点内部的调用点数"（相等即无旁路，且不依赖调用方名字格式）。
    $deliveryCallSites = @(Calls @((Method $component 'CompleteTreasuryDelivery')) |
        Where-Object { $_ -match 'PawnSettlementSaveHost::ExecutePhysical\(' })
    Require ($deliveryCallSites.Count -ge 1) 'Treasury delivery completion must execute the physical settlement'
    $allPhysicalCallSites = @($otherCalls | Where-Object { $_ -match 'PawnSettlementSaveHost::ExecutePhysical\(' })
    Require ($allPhysicalCallSites.Count -eq $deliveryCallSites.Count) 'Physical settlement must only be reachable from the treasury delivery completion'
    Require (@($otherCalls | Where-Object { $_ -match 'PawnSettlementSaveHost::AllocateLegacy\(' }).Count -eq 0) 'Legacy allocation must remain unwired'
    foreach ($name in @('PawnSettlementSaveHost', 'SettlementReservePrototype')) {
        $ready = Method ($assembly.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.' + $name)) 'get_CanExecuteInGame'
        Require (($ready.Body.Instructions.OpCode.Name -join ',') -eq 'ldc.i4.0,ret') 'Physical game capability must remain false'
    }
    $slot = $assembly.MainModule.GetType('PersonalChronicle.Automation.WorkshopSlot')
    $slotMatches = Method $slot 'Matches'
    # B2.4.1 语义（**不得回退**）：携带 legacy 经济合同的槽位必须先 `return false`，早于任何普通资格判定。
    #
    # ⚠ 旧断言锁的是 IL **形状**（`ldfld; brfalse.s; ldc.i4.0; ret`）。该形状随字段类型的**合法演进**
    #   失效：`EconomicContractId` 由 `int`（`!= 0` ⇒ `ldfld; brfalse`）变为 `long`
    #   （`!= 0L` ⇒ `ldc.i4.0; conv.i8; cgt.un; brfalse`）。2026-10-10 复核：**语义未变**
    #   （命中 legacy 仍走 `ldc.i4.0` 返回 false），是断言过脆。
    #   现改为两条**语义/顺序**断言（对类型与比较形式的演进稳健）：
    #   ① 源：守卫必须仍是 hard early return；
    #   ② IL：`EconomicContractId` 的读取必须早于普通资格判定的首个读取（`AssignedPawn`）。
    Require ((ReadSource 'Source/PersonalChronicle/Automation/WorkshopSchedule.cs') -match
        'if\s*\(\s*EconomicContractId\s*!=\s*0\s*\)\s*return false;') 'Legacy slot guard must stay a hard early return'
    # ⚠ 变量名不得叫 `$matches`：那是 PowerShell 的**自动变量**，`-match` 运算符会把它覆盖成匹配结果
    #   （PowerShell 变量名大小写不敏感）⇒ 上一条源断言执行后，Cecil 方法对象会被静默冲掉。
    $slotIns = @($slotMatches.Body.Instructions)
    $legacyAt = -1; $ordinaryAt = -1
    for ($i = 0; $i -lt $slotIns.Count; $i++) {
        $operand = $slotIns[$i].Operand
        if ($operand -isnot [Mono.Cecil.FieldReference]) { continue }
        if ($operand.Name -eq 'EconomicContractId' -and $legacyAt -lt 0) { $legacyAt = $i }
        if ($operand.Name -eq 'AssignedPawn' -and $ordinaryAt -lt 0) { $ordinaryAt = $i }
    }
    Require ($legacyAt -ge 0 -and $ordinaryAt -gt $legacyAt) 'Legacy slot must return false before evaluating ordinary slot eligibility'
    Require ((ReadSource 'Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.Runtime.cs') -match 'slot != null && slot.EconomicContractId == 0\) AddRuntimeMember') 'Legacy slots may not suppress ordinary work as roster members'
    Require ((ReadSource 'Source/PersonalChronicle/ChronicleSettings.cs') -match 'EnablePawnEconomicInstitution = true' -and
        (ReadSource 'Source/PersonalChronicle/ChronicleSettings.cs') -match 'ref EnablePawnEconomicInstitution, "enablePawnEconomicInstitution", true') 'Unique setting must default true (CHG-20261010-ECON-DEFAULT-ON) and use stable save key'
    Require ((ReadSource 'Source/PersonalChronicle/Settings/SettingsDrawer.cs') -match 'ApplyInstitutionModeSetting\(on\)') 'Paused settings change must notify runtime'
    # 末批（删聚合窗口 2026-10-11）：原断言「旧并行开关必须退役」的对象（聚合窗口的设置按钮 + 不可达的
    # Enable 动词）已随窗口**整体删除** ⇒ 该意图现在由"窗口不存在"直接保证，断言退役。
    # RUIF / 折行断言改指幸存宿主（政策页内容层）。
    $ui = ReadSource 'Source/PersonalChronicle/Economy/PawnEconomy/Dialog_WorkContract.cs'
    # C-1（2026-10-10）：显式折行实现自 P0-6 起在 EconomyRowList.cs（行族），不在对话框主文件 ——
    # 断言必须指向**实现所在文件**，否则实现搬家即假失败（旧口径自那之后恒不满足，GATE-UI 已指出）。
    Require ($ui -notmatch 'Widgets\.|GUI\.' -and $ui -match 'RUIF.ButtonAtoms' -and
        (ReadSource 'Source/PersonalChronicle/Economy/PawnEconomy/EconomyRowList.cs') -match 'wrappedRows\[i\] = Flow') 'Economic UI must use RUIF and explicit text wrapping'
    Require (@($assembly.MainModule.AssemblyReferences | Where-Object Name -eq RUIF).Count -eq 1) 'RUIF hard dependency missing'
    $modeKeys = @{}
    foreach ($language in @('ChineseSimplified', 'English')) {
        $document = [xml](ReadSource "Languages/$language/Keyed/EconomyMode.xml")
        $nodes = @($document.LanguageData.ChildNodes | Where-Object NodeType -eq Element)
        $modeKeys[$language] = @($nodes | ForEach-Object Name)
        # ⚠ 旧口径是**快照计数** `-eq 22`（B2.4.1 审计时的键数）。该文件随后经多个批次**合法增长**
        #   到 59 键（中英同步、无重复 —— 2026-10-10 复核）⇒ 硬计数已失效。
        #   现锁**不变量**而非快照：不低于审计基线（不缩水）+ 无重复；双语一致性由下方 Compare-Object 守。
        Require ($nodes.Count -ge 22 -and $nodes.Count -eq @($modeKeys[$language] | Sort-Object -Unique).Count) 'Mode keys missing or duplicated'
        foreach ($node in $nodes) {
            Require (-not [string]::IsNullOrWhiteSpace($node.InnerText)) 'Empty translated text'
            $duplicates = @(Get-ChildItem -LiteralPath (Join-Path $repo "Languages/$language/Keyed") -Filter '*.xml' | ForEach-Object {
                ([xml](Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8)).LanguageData.ChildNodes | Where-Object { $_.NodeType -eq 'Element' -and $_.Name -eq $node.Name }
            })
            Require ($duplicates.Count -eq 1) ('Duplicate global translation key: ' + $node.Name)
            Require ($node.InnerText -notmatch '\{\d+\}') 'Unexpected translation placeholders require explicit parity validation'
        }
    }
    Require (@(Compare-Object $modeKeys.ChineseSimplified $modeKeys.English).Count -eq 0) 'Bilingual mode key mismatch'
    $gameHash = (Get-FileHash -LiteralPath $gameDll).Hash
    Require ($gameHash -eq '5CF1B5BE399D5B1C9C56CA72C9D35B4ECF307FEACF5859D04AC5A1AA5926356A') 'Game assembly changed; re-audit native contracts'
    $settingsCtor = @($game.MainModule.GetType('RimWorld.Dialog_ModSettings').Methods | Where-Object { $_.IsConstructor -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -eq 'Verse.Mod' })
    Require ($settingsCtor.Count -eq 1) 'Native ModSettings window constructor changed'
    $trxRelative = 'Tests/TestResults/EconomyLanding-B241-Full.trx'; $trxPath = Join-Path $repo $trxRelative
    $trx = [xml](Get-Content -LiteralPath $trxPath -Raw -Encoding UTF8)
    Require ([int]$trx.TestRun.ResultSummary.Counters.failed -eq 0 -and [int]$trx.TestRun.ResultSummary.Counters.passed -ge 866) 'Full regression missing or failed'
    $ids = @{}
    foreach ($test in $trx.TestRun.TestDefinitions.UnitTest) {
        if ($test.TestMethod.className -match '\.PawnInstitutionModeRulesTests$' -or $test.TestMethod.name -like 'B241_*' -or $test.TestMethod.name -like 'CHG20261010_*') { $ids[$test.id] = $true }
    }
    $newCases = @($trx.TestRun.Results.UnitTestResult | Where-Object { $ids.ContainsKey($_.testId) })
    Require ($newCases.Count -eq 38 -and @($newCases | Where-Object outcome -ne Passed).Count -eq 0) 'B2.4.1 behavior cases missing or failing'
    $skipped = @($trx.TestRun.Results.UnitTestResult | Where-Object outcome -eq NotExecuted | ForEach-Object testName)
    Require ($skipped.Count -eq 1 -and $skipped[0] -eq 'Measure_Content_vs_Viewport') 'Unexpected skipped cases'
    Require ((Get-FileHash -LiteralPath $dll).Hash -eq (Get-FileHash -LiteralPath (Join-Path $repo 'Tests/bin/Release/net48/PersonalChronicle.dll')).Hash) 'Current DLL differs from tested DLL'
    $base = 'docs/设计文档/功能模块/经济体系/经济需求驱动AI/'
    $paths = @('Source/PersonalChronicle/Domain/PawnEconomy/Governance/InstitutionModeContracts.cs',
        'Source/PersonalChronicle/Application/PawnEconomy/Governance/InstitutionModeService.cs',
        'Source/PersonalChronicle/Data/PawnEconomy/InstitutionModeSaveData.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionMode.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/EconomyPreservedAssets.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs', 'Source/PersonalChronicle/Economy/PawnEconomy/EconomyCommands.cs',
        # 末批（删聚合窗口 2026-10-11）：原此处为聚合窗口主文件（已删除）—— 政策内容层是其唯一幸存对应物。
        'Source/PersonalChronicle/Economy/PawnEconomy/Dialog_WorkContract.cs',
        'Source/PersonalChronicle/Automation/WorkshopSchedule.cs', 'Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.Runtime.cs',
        'Source/PersonalChronicle/ChronicleSettings.cs', 'Source/PersonalChronicle/Settings/SettingsCatalog.cs', 'Source/PersonalChronicle/Settings/SettingsDrawer.cs',
        'Languages/ChineseSimplified/Keyed/EconomyMode.xml', 'Languages/English/Keyed/EconomyMode.xml',
        'Tests/PawnInstitutionModeTests.cs', 'Tests/PawnSettlementPersistenceTests.cs', '_scripts/verify-pawn-institution-mode.ps1', '_scripts/verify-pawn-needs-ui.ps1',
        ($base + '00-总纲与共享/03-实装批次与状态/07-B2.4.1_制度设置与互斥门禁_实装记录.md'), ($base + '00-总纲与共享/03-实装批次与状态/实施批次状态.json'),
        ($base + '00-总纲与共享/03-实装批次与状态/00-实装落地计划_v1.0.md'), ($base + '00-总纲与共享/04-实装指导手册/00-总册与实施路线.md'),
        ($base + 'M11-资金资源权利与制度协调/11-资金资源权利与制度协调_代码设计方案.md'),
        'docs/changelog/v1.4.x_2026-10-08_经济AI制度设置与互斥门禁.md')
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
        changeId = 'CHG-20261008-ECON-MODE-GATES'; batch = 'B2.4.1'; state = 'CodeVerified'; parentBatchState = 'InProgress'; checkedUtc = [DateTime]::UtcNow.ToString('o')
        modSha256 = (Get-FileHash -LiteralPath $dll).Hash; gameSha256 = $gameHash; gameAssemblyVersion = $game.Name.Version.ToString()
        compiledChecks = [ordered]@{ pureGovernanceTypesIncludingGenerated = $pure.Count; guardedLegacyEntrypoints = $gated;
            legacyFeeRouteAlwaysClosed = $true; legacyLoadCancellationRetired = $true; modeSaveWired = $true;
            finalLegacyFreezeWired = $true; legacyRightsFullyAssumed = $false; physicalPaymentRuntimeWired = $false;
            actualReadinessIncomplete = $true; legacySlotsPaused = $true; ownedAssetUseWithoutPayment = $true;
            bilingualKeys = $modeKeys.English.Count; ruifDependency = $true; nativeSettingsConstructorVerified = $true }
        tests = [ordered]@{ path = $trxRelative; sha256 = (Get-FileHash -LiteralPath $trxPath).Hash;
            passed = [int]$trx.TestRun.ResultSummary.Counters.passed; failed = 0; skipped = $skipped.Count; skippedNames = $skipped; newCases = $newCases.Count }
        deployedThisChange = $false; gameVerifiedThisChange = $false; activeAvailableInGame = $false
        globalGateAvailable = (Test-Path (Join-Path $repo 'scripts/gate-runner.ps1')); files = $files
        limits = @('Preparing is the highest actual runtime activation state; pure readiness fixtures do not authorize gameplay.',
            'Physical deposit/withdrawal Jobs, payer authority and final legacy rights mapping remain B2.4.2 work.',
            'Legacy slots retain identity and data; pause and owned apparel routing are structurally checked, not accepted in a running game.',
            'B2.3 historical reports describe the then-current cancellation behavior; B2.4.1 explicitly retires that behavior without rewriting historical evidence.',
            'Offline Scribe tests use a minimal engine fixture; actual save compatibility, paused UI, fonts and layout still need game acceptance.')
    }
    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $repo ($base + '00-总纲与共享/03-实装批次与状态/B2.4.1_实装验证.json')) -Encoding utf8
    Write-Output "PASS B2.4.1: $($pure.Count) pure governance types; $($gated.Count) legacy gates; mode save; exclusive routes; paused legacy slots; 22 bilingual keys; 38 new cases; $($report.tests.passed) passed, 1 existing skip. Active/deployment/game verification remain false."
}
finally { $assembly.Dispose(); $game.Dispose() }
