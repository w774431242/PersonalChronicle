#requires -Version 7.0
param(
    [string]$GameRoot = 'E:\SteamLibrary\steamapps\common\RimWorld',
    [string]$FullTestResults = 'Tests/TestResults/EconomyLanding-B13-Full.trx',
    [string]$ReportRelative = 'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/B1.3_实装验证.json',
    [string]$ChangeId = 'CHG-20261008-ECON-NEEDS-UI'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
function Require([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Calls($methods) {
    @($methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions |
        Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.ToString() } })
}
$utf8 = [System.Text.UTF8Encoding]::new($false, $true)
$localizations = @{}
foreach ($language in @('ChineseSimplified', 'English')) {
    # 本页消费**两套**键文件：PawnEconomyNeeds（页正文）与 PawnEconomyPlanning（长期政策 + 劳动/照护分区）。
    # 只查一套会让 partial 里新增的 Planning.* 文案逃过校验（2026-10-09 升级修复的覆盖缺口）。
    $keys = @{}
    foreach ($keyFile in @('PawnEconomyNeeds.xml', 'PawnEconomyPlanning.xml')) {
        $path = Join-Path $repo "Languages/$language/Keyed/$keyFile"
        $bytes = [System.IO.File]::ReadAllBytes($path)
        Require (-not ($bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191)) 'Localization XML must be UTF-8 without BOM'
        $xml = [xml]$utf8.GetString($bytes)
        foreach ($node in $xml.LanguageData.ChildNodes | Where-Object NodeType -eq Element) {
            Require (-not $keys.ContainsKey($node.Name)) ('Duplicate key: ' + $node.Name)
            Require (-not [string]::IsNullOrWhiteSpace($node.InnerText)) ('Empty translation: ' + $node.Name)
            $keys[$node.Name] = $node.InnerText
            $null = [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, $node.InnerText, [object[]](0..10))
        }
    }
    # Check duplicate keys across the existing namespace too.
    $seen = @{}
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo "Languages/$language/Keyed") -Filter *.xml) {
        $allXml = [xml]([System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8))
        foreach ($node in $allXml.LanguageData.ChildNodes | Where-Object NodeType -eq Element) {
            if (-not $node.Name.StartsWith('PC_PEA.Needs.')) { continue }
            Require (-not $seen.ContainsKey($node.Name)) ('Namespace collision: ' + $node.Name)
            $seen[$node.Name] = $true
        }
    }
    $localizations[$language] = $keys
}
$zh = $localizations['ChineseSimplified']; $en = $localizations['English']
Require ($zh.Count -eq $en.Count) 'Bilingual key counts differ'
foreach ($key in $zh.Keys) {
    Require ($en.ContainsKey($key)) ('Missing English key: ' + $key)
    $zp = @([regex]::Matches($zh[$key], '\{(\d+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    $ep = @([regex]::Matches($en[$key], '\{(\d+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    Require (($zp -join ',') -eq ($ep -join ',')) ('Placeholder mismatch: ' + $key)
}
# 字面量键覆盖：**含全部 partial 与两个 presenter**（2026-10-09 升级修复 —— 此前 4 个 partial 里的 N(...)
# 文案与 presenter 的 T(...) 文案都不在校验面内）。
# 末批（删聚合窗口 2026-10-11）：原首项（聚合窗口的需求页宿主）已删除 —— 需求页现由主控经济分页承载。
$uiPaths = @(
    'Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs',
    'Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPanel.cs',
    'Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs',
    'Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Measure.cs',
    'Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Demand.cs',
    'Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Goal.cs',
    'Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Decision.cs',
    'Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.cs',
    'Source/PersonalChronicle/Economy/PawnEconomy/PawnEconomyFactsPresenter.cs',
    'Source/PersonalChronicle/Economy/PawnEconomy/ITab_Pawn_PersonalNeeds.cs')
foreach ($path in $uiPaths) {
    $body = [System.IO.File]::ReadAllText((Join-Path $repo $path), [System.Text.Encoding]::UTF8)
    foreach ($match in [regex]::Matches($body, '\b(?:N|T)\("([A-Za-z0-9.]+)"')) {
        $suffix = $match.Groups[1].Value
        if ($suffix.EndsWith('.')) { continue } # Enum families are exercised by the tests below.
        # 两个落点都试（键仍必须真实存在，只是落点不同）：
        #   面板/ITab 用 N(suffix) → PC_PEA.Needs.<suffix>
        #   presenter 用 T(suffix) → PC_PEA.Needs.Planning.<suffix>
        Require ($zh.ContainsKey('PC_PEA.Needs.' + $suffix) -or $zh.ContainsKey('PC_PEA.Needs.Planning.' + $suffix)) ('Missing literal UI key: ' + $suffix)
    }
    # Raw keys (e.g. ITab labelKey) never pass through N()/T() but are still user-visible.
    foreach ($match in [regex]::Matches($body, '"PC_PEA\.Needs\.([A-Za-z0-9.]+)"')) {
        $suffix = $match.Groups[1].Value
        if ($suffix.EndsWith('.')) { continue }
        Require ($zh.ContainsKey('PC_PEA.Needs.' + $suffix)) ('Missing raw UI key: ' + $suffix)
    }
}
$tabSource = [System.IO.File]::ReadAllText((Join-Path $repo 'Source/PersonalChronicle/Economy/PawnEconomy/ITab_Pawn_PersonalNeeds.cs'), [System.Text.Encoding]::UTF8)
Require ($tabSource -match 'labelKey\s*=\s*"PC_PEA\.Needs\.TabTitle"') 'Personal-needs tab must bind the raw TabTitle key'
Require (-not ($tabSource -match 'labelKey[^\r\n]*Translate\(')) 'labelKey must stay raw (engine translates lazily)'
Add-Type -Path (Join-Path $repo 'InspectTypes/bin/Release/net8.0/Mono.Cecil.dll')
$dll = Join-Path $repo 'Assemblies/PersonalChronicle.dll'
$ruifDll = Join-Path $GameRoot 'Mods/RUIF/Assemblies/RUIF.dll'
$mod = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll)
$ruif = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($ruifDll)
try {
    $all = @($mod.MainModule.Types)
    $core = @($all | Where-Object { $_.Namespace -eq 'PersonalChronicle.Application.PawnEconomy.Perception' -and $_.Name -match 'NeedDashboard|PawnNeedSummary' })
    Require ($core.Count -eq 4) 'Dashboard application contracts or builder missing'
    foreach ($type in $core) {
        $refs = @($type.Fields | ForEach-Object { $_.FieldType.ToString() })
        foreach ($method in $type.Methods) {
            $refs += $method.ReturnType.ToString(); $refs += @($method.Parameters | ForEach-Object { $_.ParameterType.ToString() })
            if ($method.HasBody) { $refs += @($method.Body.Instructions | ForEach-Object { [string]$_.Operand }) }
        }
        Require (@($refs | Where-Object { $_ -match '(Verse|RimWorld|UnityEngine|RUIF|HarmonyLib)\.' }).Count -eq 0) ('Impure dashboard contract: ' + $type.Name)
    }
    # 末批（删聚合窗口 2026-10-11）：原「聚合窗口构造签名保持二进制兼容」的断言**对象已不存在**
    #（窗口整体删除）⇒ 断言退役。需求页的窗口宿主改为总控经济分页（见下方改指）。
    # 内容层（宿主无关面板）：7 个 RUIF 原子、禁 legacy、显式换行各恰一次、且不得引用宿主类型。
    $panel = $mod.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.NeedDashboardPanel')
    Require ($null -ne $panel) 'Needs panel content layer missing from assembly'
    $panelCalls = @(Calls @($panel.Methods))
    foreach ($primitive in @('DataAtoms::StatTable', 'PanelAtoms::StatRow', 'FeedbackAtoms::AlertBanner', 'FeedbackAtoms::EmptyState', 'Layout::BeginScroll', 'Layout::EndScroll', 'ButtonAtoms::PrimaryButton')) {
        Require (@($panelCalls | Where-Object { $_.Contains($primitive) }).Count -gt 0) ('RUIF primitive not used: ' + $primitive)
    }
    Require (@($panelCalls | Where-Object { $_ -match 'Verse\.Widgets::|::(Execute|Send|Query|PollPopulation|Capture|Tick|Account|Person|MarkDirty)\(' }).Count -eq 0) 'Needs panel must not invoke legacy query, commands, raw widgets, sampling or enrollment'
    Require (@($panelCalls | Where-Object { $_ -match 'NeedDashboardTextFlow::Wrap\(' }).Count -eq 1) 'Panel RUIF labels require explicit line flow before drawing'
    $panelRefs = @(@($panel.Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions | ForEach-Object { [string]$_.Operand } }))
    Require (@($panelRefs | Where-Object { $_ -match 'Dialog_PawnEconomy|Verse\.Window|ITab_Pawn_PersonalNeeds' }).Count -eq 0) 'Needs panel must stay host-agnostic'
    # 末批（删聚合窗口 2026-10-11）：需求页的**窗口宿主**由聚合窗口换成**主控经济分页** ——
    # 断言改指幸存宿主，保持同一意图：必须委托共享面板、且不得调 legacy 查询 / 命令 / 原版控件。
    #（原「唯一一处显式折行」断言随聚合窗口的 Flow 薄封装一并退役：内容层自带的折行断言在下方。）
    $window = $mod.MainModule.GetType('PersonalChronicle.MasterControl.MasterControlTabWindow')
    Require ($null -ne $window) 'Needs page window host (master control) missing from assembly'
    # ⚠ 断言范围**只到需求页宿主方法** `DrawColonyNeedsPage`：主控窗口自身的 chrome 用原版
    # `Widgets.ButtonText`（既有实现、与需求页无关）⇒ 若把窗口的其它方法一并纳入，"不得调原版控件"
    # 会因为无关代码假失败（2026-10-11 实测确认：违规命中来自 `DrawColonyEconomyPage` 的按钮）。
    $windowMethods = @($window.Methods | Where-Object { $_.Name -eq 'DrawColonyNeedsPage' })
    $windowCalls = @(Calls $windowMethods)
    Require (@($windowCalls | Where-Object { $_ -match 'NeedDashboardPanel::Draw\(' }).Count -gt 0) 'Host must delegate needs rendering to the shared panel'
    Require (@($windowCalls | Where-Object { $_ -match 'Verse\.Widgets::|::(Execute|Send|Query|PollPopulation|Capture|Tick|Account|Person|MarkDirty)\(' }).Count -eq 0) 'Needs host method must not invoke legacy query, commands, raw widgets, sampling or enrollment'
    # Pawn 检视窗「个人需求」页：存在、派生自 ITab、只读、经单人门面取数。
    $tab = $mod.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.ITab_Pawn_PersonalNeeds')
    Require ($null -ne $tab) 'Personal-needs ITab missing from assembly'
    Require ([string]$tab.BaseType.FullName -eq 'RimWorld.ITab') 'Personal-needs tab must derive from RimWorld.ITab'
    $tabCalls = @(Calls @($tab.Methods))
    Require (@($tabCalls | Where-Object { $_ -match 'QueryNeedDashboardSingle\(' }).Count -gt 0) 'Personal-needs tab must read through the single-pawn facade'
    Require (@($tabCalls | Where-Object { $_ -match 'Verse\.Widgets::|::(Execute|Send|Query|PollPopulation|Capture|Account|Person|Enable|Prepare|Commit|RefreshPawns|MarkDirty)\(' }).Count -eq 0) 'Personal-needs tab must remain read-only'
    # 零原版 UI 依赖（PM 裁决 2026-10-09：移除原版需求列）：
    # 本页只以原版为**数据真值来源**，不得调用任何原版需求 UI 成员，也不得触碰原版需求页签。
    Require (@($tabCalls | Where-Object { $_ -match 'NeedsCardUtility::|PawnNeedsUIUtility::' }).Count -eq 0) 'Personal-needs tab must not call any vanilla need UI member'
    Require (@($tabCalls | Where-Object { $_ -match 'ITab_Pawn_Needs\b' }).Count -eq 0) 'Personal-needs tab must not touch the vanilla needs tab'
    # 右栏（需求循环面板）：与名册面板同族的宿主无关内容层。
    $cycle = $mod.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.NeedsCyclePanel')
    Require ($null -ne $cycle) 'Needs cycle panel missing from assembly'
    $cycleCalls = @(Calls @($cycle.Methods))
    foreach ($primitive in @('NavAtomsB::LayerSwitch', 'PanelAtoms::SectionBar', 'PanelAtoms::StatRow', 'ProgressAtoms::CapacityBar', 'Shapes::InsetPanel', 'Shapes::CornerFrame', 'FeedbackAtoms::EmptyState', 'Layout::BeginScroll', 'Layout::EndScroll', 'Draw::Label')) {
        Require (@($cycleCalls | Where-Object { $_.Contains($primitive) }).Count -gt 0) ('Cycle panel RUIF primitive not used: ' + $primitive)
    }
    Require (@($cycleCalls | Where-Object { $_ -match 'Verse\.Widgets::|(NeedsCardUtility|PawnNeedsUIUtility)::|::(Execute|Send|Query|PollPopulation|Capture|Tick|Account|Person|MarkDirty)\(' }).Count -eq 0) 'Cycle panel must not touch vanilla need UI, legacy query or commands'
    $cycleRefs = @(@($cycle.Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions | ForEach-Object { [string]$_.Operand } }))
    Require (@($cycleRefs | Where-Object { $_ -match 'Dialog_PawnEconomy|Verse\.Window|ITab_Pawn_PersonalNeeds|GameComponent_PawnEconomy' }).Count -eq 0) 'Cycle panel must stay host-agnostic and must not query data itself'
    # 组合根：仍调用注入器，且三个页签类型都在注册列表中（防注入点漂移）。
    $startup = $mod.MainModule.GetType('PersonalChronicle.ChronicleStartup')
    Require ($null -ne $startup) 'ChronicleStartup missing from assembly'
    $startupCalls = @(Calls @($startup.Methods))
    Require (@($startupCalls | Where-Object { $_ -match 'ChronicleInspectTabInjector::InjectAll\(' }).Count -gt 0) 'Startup must call the inspect-tab injector'
    $startupRefs = @(@($startup.Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions | ForEach-Object { [string]$_.Operand } }))
    foreach ($registered in @('ITab_Pawn_Chronicle', 'ITab_Pawn_Career', 'ITab_Pawn_PersonalNeeds')) {
        Require (@($startupRefs | Where-Object { $_ -match $registered }).Count -gt 0) ('Startup must register inspect tab: ' + $registered)
    }
    $hostType = $mod.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.GameComponent_PawnEconomy')
    $queryCalls = @(Calls @($hostType.Methods | Where-Object { $_.Name -match '^QueryNeedDashboard' }))
    Require (@($queryCalls | Where-Object { $_ -match '::(Execute|Account|Person|Enable|Prepare|Commit|RefreshPawns|Tick|PollPopulation|Capture)\(' }).Count -eq 0) 'Dashboard facades must remain read-only'
    # 长短期同屏（2026-10-09 裁决）：需求页签逐条给「短期满足度 × 长期目标」，页尾独立长期政策区；
    # 长期政策行必须由 presenter 的专用入口产出，且决策页行不得重复同一份数据（否则口径会漂移）。
    # 注：测量缓存的成员是**字段**（与既有 measure 同风格），IL 里是 NeedsCycleMeasure::X；视图侧是属性（get_Policy）。
    Require (@($cycleRefs | Where-Object { $_ -match 'NeedsCycleMeasure::(ShownTargets|HiddenTargets|HeaderLongTarget|Policy|PolicyLabels)\b' }).Count -ge 5) 'Cycle panel must render the long-term target column and the long-term plan area'
    Require (@($cycleRefs | Where-Object { $_ -match 'NeedDashboardView::get_Policy\(\)' }).Count -gt 0) 'Cycle panel must read the long-term partition from the view'
    $plannerType = $mod.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.ShadowPlanPresenter')
    Require ($null -ne $plannerType) 'Shadow plan presenter missing from assembly'
    Require (@($plannerType.Methods | Where-Object { $_.Name -eq 'BuildPolicy' -and -not $_.IsConstructor }).Count -eq 1) 'Long-term policy card must come from one dedicated presenter entry point'
    $viewType = $mod.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.NeedDashboardView')
    Require ($null -ne $viewType) 'Need dashboard view missing from assembly'
    Require (@($viewType.Properties | Where-Object { $_.Name -eq 'Policy' }).Count -eq 1) 'Need dashboard view must expose the long-term partition'
    $singleMethod = @($hostType.Methods | Where-Object { $_.Name -eq 'QueryNeedDashboardSingle' })[0]
    $singleCalls = @(Calls @($singleMethod))
    Require (@($singleCalls | Where-Object { $_ -match 'BuildPolicy\(' }).Count -gt 0) 'Single-pawn facade must feed the long-term partition'
    Require (@($singleCalls | Where-Object { $_ -match 'ShadowPlanPresenter::Build\(' }).Count -gt 0) 'Single-pawn facade must still feed the decision partition'
    # 结构化上屏（2026-10-09 第二批）：目标态 / 预算 / 候选岗位 / 四循环必须都是**结构化分区**并走到只读页。
    foreach ($prop in @('Policy', 'Goals', 'Budget', 'Opportunities', 'Cycles')) {
        Require (@($viewType.Properties | Where-Object { $_.Name -eq $prop }).Count -eq 1) ('Need dashboard view must expose partition: ' + $prop)
    }
    foreach ($entry in @('BuildPolicy', 'BuildGoals', 'BuildBudget', 'BuildOpportunities', 'BuildCycles')) {
        Require (@($plannerType.Methods | Where-Object { $_.Name -eq $entry -and -not $_.IsConstructor }).Count -eq 1) ('Shadow plan presenter entry missing: ' + $entry)
    }
    Require (@($singleCalls | Where-Object { $_ -match 'BuildGoals\(|BuildBudget\(|BuildOpportunities\(|BuildCycles\(' }).Count -ge 4) 'Single-pawn facade must feed every structured partition'
    foreach ($partition in @('NeedDashboardView::get_Goals', 'NeedDashboardView::get_Cycles', 'NeedDashboardView::get_Budget', 'NeedDashboardView::get_Opportunities')) {
        Require (@($cycleRefs | Where-Object { $_.Contains($partition) }).Count -gt 0) ('Cycle panel does not consume partition: ' + $partition)
    }
    # 真实账本事实分区（2026-10-09 升级）：劳动与合同 / 照护与服务必须走结构化分区到只读页，
    # 且单人门面保持**纯只读** —— 不得出现结束合同、释放工作类型、签发/扣费或推进能力评估的写路径。
    foreach ($prop in @('Employment', 'Care')) {
        Require (@($viewType.Properties | Where-Object { $_.Name -eq $prop }).Count -eq 1) ('Need dashboard view must expose partition: ' + $prop)
    }
    $factsType = $mod.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.PawnEconomyFactsPresenter')
    Require ($null -ne $factsType) 'Economy facts presenter missing from assembly'
    foreach ($entry in @('BuildEmployment', 'BuildCare')) {
        Require (@($factsType.Methods | Where-Object { $_.Name -eq $entry -and -not $_.IsConstructor }).Count -eq 1) ('Economy facts entry missing: ' + $entry)
    }
    Require (@($singleCalls | Where-Object { $_ -match 'BuildEmployment\(|BuildCare\(' }).Count -ge 2) 'Single-pawn facade must feed the ledger-fact partitions'
    foreach ($partition in @('NeedDashboardView::get_Employment', 'NeedDashboardView::get_Care')) {
        Require (@($cycleRefs | Where-Object { $_.Contains($partition) }).Count -gt 0) ('Cycle panel does not consume partition: ' + $partition)
    }
    Require (@($singleCalls | Where-Object { $_ -match '::(Terminate|TrySignForOpportunity|CreateCareAuthorization|ChargeCareAuthorization|RefreshServiceCapability|EvaluateServiceCapability|ReleaseWorkTypes|TrySubmitRestockProposal)\(' }).Count -eq 0) 'Single-pawn facade must stay read-only (no contract/care/production write paths)'
    # 个人账户摘要（CB-ECON-002）：账户分区必须由**单一 presenter 入口**产出、经单人门面装配、并被表现层消费；
    # 账本可用性必须**先于**账户读数判定（否则"账本不可判定"会被误报成"尚未建立账户"）；
    # 六态标签必须都在键集里（缺一态就会把裸标识打上屏，P0-4）。
    Require (@($viewType.Properties | Where-Object { $_.Name -eq 'Account' }).Count -eq 1) 'Need dashboard view must expose partition: Account'
    Require (@($factsType.Methods | Where-Object { $_.Name -eq 'BuildAccount' -and -not $_.IsConstructor }).Count -eq 1) 'Economy facts entry missing: BuildAccount'
    Require (@($singleCalls | Where-Object { $_ -match 'BuildAccount\(' }).Count -gt 0) 'Single-pawn facade must feed the account partition'
    Require (@($singleCalls | Where-Object { $_ -match 'ReadLedgerState\(' }).Count -gt 0) 'Single-pawn facade must read ledger availability before the account'
    Require (@($cycleRefs | Where-Object { $_.Contains('NeedDashboardView::get_Account') }).Count -gt 0) 'Cycle panel does not consume partition: NeedDashboardView::get_Account'
    foreach ($state in @('Known', 'NotEstablished', 'Unavailable', 'Unsupported', 'Error', 'NotApplicable')) {
        Require ($zh.ContainsKey('PC_PEA.Needs.Planning.Account.State.' + $state)) ('Missing account state label: ' + $state)
    }
    # 账户主行列宽必须**测量与绘制同源**（2026-10-11 GATE-UI H1 实测：字体/宽度任一处不一致，
    # RUIF.Draw.Label（WordWrap=false 且不裁剪）就会把文字溢出到相邻元素）。
    $accountLayoutType = $mod.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.NeedAccountMeasure')
    Require ($null -ne $accountLayoutType) 'Measured account layout missing'
    Require (@($cycleRefs | Where-Object { $_ -match 'NeedAccountText::Rect' }).Count -ge 2) 'Account measure and paint must share the exact measured text rectangles'
    Require (@($cycleRefs | Where-Object { $_ -match 'NeedAccountView::get_Model' }).Count -gt 0) 'Account UI must consume the structured decision model'
    $cycleLineType = $mod.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.NeedCycleLine')
    foreach ($prop in @('Id','Status','ReasonCode','Source','SampledTick','Stale')) {
        Require (@($cycleLineType.Properties | Where-Object { $_.Name -eq $prop }).Count -eq 1) ('Cycle evidence field missing: ' + $prop)
    }
    $settlementHostType = $mod.MainModule.GetType('PersonalChronicle.Economy.PawnEconomy.PawnSettlementSaveHost')
    $displayReads = @($settlementHostType.Methods | Where-Object { $_.Name -in @('ReadLedgerState','ReadAccountForDisplay','TryReadAccountForDisplay') })
    Require (@(Calls $displayReads | Where-Object { $_ -match '::(Recover|AuditCore|EnrollAccount|Cancel|CompletePhysical)\(' }).Count -eq 0) 'Display reads must not recover, allocate accounts or mutate settlements'
    # 新引用的 RUIF 原子（**成员级**扩展，无新增程序集引用）：既要在表现层被用到，也要在已引用程序集里确实存在。
    foreach ($primitive in @('StatusAtoms::PulseDot', 'StatusFmt::DotRect', 'DataAtoms::TraitChips', 'VisualAtoms::Swatch')) {
        Require (@($cycleCalls | Where-Object { $_.Contains($primitive) }).Count -gt 0) ('Cycle panel RUIF primitive not used: ' + $primitive)
    }
    $panelAtoms = $ruif.MainModule.GetType('RUIF.PanelAtoms')
    $dataAtoms = $ruif.MainModule.GetType('RUIF.DataAtoms')
    $statusAtoms = $ruif.MainModule.GetType('RUIF.StatusAtoms')
    $visualAtoms = $ruif.MainModule.GetType('RUIF.VisualAtoms')
    Require ($null -ne $panelAtoms -and $null -ne $dataAtoms -and $null -ne $statusAtoms -and $null -ne $visualAtoms) 'RUIF atom type missing from the referenced assembly'
    Require (@($panelAtoms.Methods | Where-Object { $_.Name -eq 'StatRow' -and $_.Parameters.Count -eq 5 }).Count -eq 1) 'RUIF StatRow(Rect,string,string,float?,Color?) overload missing'
    Require (@($dataAtoms.Methods | Where-Object { $_.Name -eq 'TraitChips' }).Count -eq 1) 'RUIF TraitChips missing'
    Require (@($statusAtoms.Methods | Where-Object { $_.Name -eq 'PulseDot' }).Count -eq 1) 'RUIF PulseDot missing'
    Require (@($visualAtoms.Methods | Where-Object { $_.Name -eq 'Swatch' }).Count -eq 1) 'RUIF Swatch missing'
    # 末批（删聚合窗口 2026-10-11）：原断言检查聚合窗口 `DoWindowContents` 里"需求分支调用后立即离开
    # 本方法" —— 该窗口已删除，同一语义现由**主控经济分页**承担：`DrawColonyEconomyPage` 内的
    # `DrawColonyNeedsPage` 调用之后必须立即返回（不得落回其它视图分支）。
    $draw = @($window.Methods | Where-Object Name -eq 'DrawColonyEconomyPage')[0].Body.Instructions
    $needsCall = @($draw | Where-Object { [string]$_.Operand -match '::DrawColonyNeedsPage\(' })[0]
    Require ($null -ne $needsCall) 'Needs branch call missing from the colony economy page'
    # 语义：调用后必须立即离开本方法，不得落回 legacy query/command 路径。
    # 允许编译器插入 nop，且允许 return 被合并为该方法的公共 ret（表现为 br 到末尾 ret）——
    # 原实现断言「下一条就是 ret」，会随方法体变大而误报（2026-10-08 实测）。
    $after = @($draw | Where-Object { $_.Offset -gt $needsCall.Offset } | Select-Object -First 3)
    Require (@($after | Where-Object { $_.OpCode.Name -eq 'ret' -or ($_.OpCode.Name -eq 'br' -and $_.Operand -is [Mono.Cecil.Cil.Instruction] -and $_.Operand.OpCode.Name -eq 'ret') }).Count -gt 0) 'Needs branch must return before the other economy views'
    foreach ($typeName in @('RUIF.DataAtoms','RUIF.PanelAtoms','RUIF.FeedbackAtoms','RUIF.Layout','RUIF.ButtonAtoms','RUIF.Measure','RUIF.Draw','RUIF.Tokens','RUIF.ProgressAtoms','RUIF.Shapes','RUIF.StatusAtoms','RUIF.NavAtomsB')) {
        Require ($null -ne $ruif.MainModule.GetType($typeName)) ('Installed RUIF type missing: ' + $typeName)
    }
    Require (@($mod.MainModule.AssemblyReferences | Where-Object Name -eq RUIF).Count -eq 1) 'RUIF hard assembly dependency missing'
    $trxPath = Join-Path $repo $FullTestResults
    $trx = [xml](Get-Content -LiteralPath $trxPath -Raw -Encoding UTF8)
    Require ([int]$trx.TestRun.ResultSummary.Counters.failed -eq 0) 'Full regression failed'
    Require ((Get-FileHash -LiteralPath $dll).Hash -eq (Get-FileHash -LiteralPath (Join-Path $repo 'Tests/bin/Release/net48/PersonalChronicle.dll')).Hash) 'Current DLL differs from tested DLL'
    $ids = @{}
    foreach ($test in $trx.TestRun.TestDefinitions.UnitTest) { if ($test.TestMethod.className -match '\.PawnNeedDashboardTests$') { $ids[$test.id] = $true } }
    $newTests = @($trx.TestRun.Results.UnitTestResult | Where-Object { $ids.ContainsKey($_.testId) })
    Require ($newTests.Count -ge 32 -and @($newTests | Where-Object outcome -ne Passed).Count -eq 0) 'Dashboard tests missing or failing'
    # 事实分区测试（2026-10-09 升级）：劳动与合同 / 照护与服务 / 循环④真实三态 / 目标页等待加权。
    $factIds = @{}
    foreach ($test in $trx.TestRun.TestDefinitions.UnitTest) { if ($test.TestMethod.className -match '\.PawnEconomyFactsPresenterTests$') { $factIds[$test.id] = $true } }
    $factTests = @($trx.TestRun.Results.UnitTestResult | Where-Object { $factIds.ContainsKey($_.testId) })
    Require ($factTests.Count -ge 9 -and @($factTests | Where-Object outcome -ne Passed).Count -eq 0) 'Economy fact partition tests missing or failing'
    $skipped = @($trx.TestRun.Results.UnitTestResult | Where-Object outcome -eq NotExecuted | ForEach-Object testName)
    $paths = @('Source/PersonalChronicle/Application/PawnEconomy/Perception/NeedDashboard.cs',
        'Source/PersonalChronicle/Domain/PawnEconomy/Perception/PawnFactSnapshot.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/Perception/VanillaPawnFactCapture.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardTextFlow.cs',
        # 末批（删聚合窗口 2026-10-11）：原两项（聚合窗口主文件 / 需求页宿主）已删除。
        'Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPanel.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Measure.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Demand.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Goal.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Decision.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/PawnEconomyFactsPresenter.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.cs',
        'Source/PersonalChronicle/Domain/PawnEconomy/Planning/ShadowPlan.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs',
        'Source/PersonalChronicle/Economy/PawnEconomy/ITab_Pawn_PersonalNeeds.cs',
        'Tests/PawnEconomyFactsPresenterTests.cs',
        'Tests/PawnShadowPlanningTests.cs',
        'Languages/ChineseSimplified/Keyed/PawnEconomyPlanning.xml',
        'Languages/English/Keyed/PawnEconomyPlanning.xml',
        'docs/开发参考/个人需求页-模块索引.md',
        'Source/PersonalChronicle/Archive/ChronicleInspectTabInjector.cs',
        'Source/PersonalChronicle/ChronicleMod.cs',
        'Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Economy.cs',
        'Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml','Languages/English/Keyed/PawnEconomyNeeds.xml',
        'Tests/PawnNeedDashboardTests.cs','_scripts/verify-pawn-needs-ui.ps1','_scripts/verify-pawn-economy.ps1',
        'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/03-B1.3_RUIF需求页_实装记录.md',
        'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/实施批次状态.json',
        'docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/00-总册与实施路线.md',
        'docs/changelog/v1.4.x_2026-10-08_经济AI需求观察页.md')
    $files = @(foreach ($path in $paths) {
        $absolute = Join-Path $repo $path; $lines = $utf8.GetString([System.IO.File]::ReadAllBytes($absolute)) -split '\r?\n'
        $anchors = @(for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match '^\s*(internal|public) (sealed |static |partial )*(class|interface)|^\s*(internal|private) (NeedDashboardView|void|bool) (QueryNeedDashboard|DrawNeeds|MeasureNeeds|IsPerceptionMapAvailable)|^## ') {
                [ordered]@{ line = $i + 1; text = $lines[$i].Trim() }
            }
        })
        [ordered]@{ path = $path; sha256 = (Get-FileHash -LiteralPath $absolute).Hash; anchors = $anchors }
    })
    $report = [ordered]@{
        changeId = $ChangeId; componentBatch = 'B1.3'; originalChangeId = 'CHG-20261008-ECON-NEEDS-UI'
        evidenceScope = $(if ($ChangeId -eq 'CHG-20261008-ECON-NEEDS-UI') { 'Initial B1.3 verification' } else { 'Current revalidation of B1.3; not its original historical fingerprint' })
        checkedUtc = [DateTime]::UtcNow.ToString('o')
        modSha256 = (Get-FileHash -LiteralPath $dll).Hash; ruifSha256 = (Get-FileHash -LiteralPath $ruifDll).Hash
        ruifVersion = $ruif.Name.Version.ToString(); compiledChecks = [ordered]@{ pureApplicationTypes = $core.Count; ruifOnlyNewPage = $true; returnsBeforeLegacyQuery = $true; readOnlyFacade = $true; vanillaUiDependencyRemoved = $true; policyCardStructured = $true; cyclePanelReadOnly = $true; ledgerFactPartitions = $true; newUiCalls = $panelCalls; cycleCalls = $cycleCalls; windowCalls = $windowCalls; tabCalls = $tabCalls; facadeCalls = $queryCalls }
        localization = [ordered]@{ keysPerLanguage = $zh.Count; keyFiles = @('PawnEconomyNeeds.xml', 'PawnEconomyPlanning.xml'); utf8NoBom = $true; placeholdersMatch = $true; noKeyCollisions = $true }
        tests = [ordered]@{ path = $FullTestResults; sha256 = (Get-FileHash -LiteralPath $trxPath).Hash; passed = [int]$trx.TestRun.ResultSummary.Counters.passed; failed = 0; skipped = $skipped.Count; skippedNames = $skipped; newCases = $newTests.Count; factCases = $factTests.Count }
        files = $files
        limits = @('No game process, rendering, clicks, save/load, resolution or profiling acceptance executed.',
            'Structured partitions (goals / budget / opportunities / cycles) are proven to reach the read-only page by IL inspection only; their on-screen layout is not verified.',
            'The long-term area renders reviewed policy parameters only; no forward need-gap projection (future shortfall over time) exists or is implied.',
            'RUIF layout is compiled and text-measured in code; offline presenter tests do not prove actual Chinese glyphs or visual layout.',
            'Life targets are now read through the shadow planning policy by both facades (roster and single); execution stays shadow-only: no budget spending, order, enrollment, trade or AI job is created.',
            'Ledger-fact partitions (labour/contract and care) are rendered from read-only readings taken at the assembly point; their on-screen layout and the interaction with the 60-tick refresh gate are not verified in game.',
            'The personal-account summary (CB-ECON-002) is assembled from a read-only ledger-availability probe plus one account read and reuses the decision page''s own budget view strings; its card layout, hover tips and in-page decision jump are IL/probe-verified only and NOT verified in game.',
            'Account states are kept apart (known / not established / unavailable / unsupported / error / not applicable); no state opens, migrates, funds or releases anything, and an unknown balance is never rendered as 0.00.',
            'Contract exit is a read-only prediction that reuses ContractExitRules.ShouldExit; it never terminates a contract, releases a work type or moves money.',
            'No deployment. Build updates only the workspace assembly. Historical B1.1/B1.2 fingerprints remain historical.')
    }
    $reportPath = Join-Path $repo $ReportRelative
    [System.IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 10), $utf8)
    Write-Output ("PASS: RUIF read-only needs page; {0} bilingual keys; {1} new tests; full {2} passed, {3} skipped. Game/UI acceptance unverified." -f $zh.Count, $newTests.Count, $report.tests.passed, $report.tests.skipped)
}
finally { $mod.Dispose(); $ruif.Dispose() }
