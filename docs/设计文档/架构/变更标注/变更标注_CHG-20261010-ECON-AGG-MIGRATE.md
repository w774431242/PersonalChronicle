---
change_id: CHG-20261010-ECON-AGG-MIGRATE
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: InProgress
scope: 用户「确认」（移除统一聚合窗口，按已确认的分配方案）—— 批次 A 第一页（需求观察）就地承接完成
rule_ids: AI-001, AI-003, UI-001, UI-006, LAND-010, LOC-001, TEST-001, DELTA-001
files:
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Economy.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyLabels.cs
    type: M
  - path: _scripts/compile.rsp
    type: M
    note: 由构建脚本自动同步（删除的源文件从编译清单移除）；非手工编辑
  - path: Source/PersonalChronicle/Economy/PawnEconomy/CarePanel.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Care.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Overview.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Authorisation.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/OverviewPanel.cs
    type: A
  - path: Source/PersonalChronicle/Application/PawnEconomy/WorkspaceView.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkspaceQueries.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWindowLayout.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyRowList.cs
    type: M
  - path: Tests/WorkspaceSecondaryTabTests.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionCards.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Institutions.cs
    type: D
  - path: _scripts/verify-pawn-economy.ps1
    type: M
  - path: _scripts/verify-pawn-institution-mode.ps1
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/PolicyPanel.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Authorisation.cs
    type: D
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.StockTarget.cs
    type: D
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyHostedTabs.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.cs
    type: D
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Needs.cs
    type: D
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Care.cs
    type: D
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Overview.cs
    type: D
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorld.cs
    type: M
  - path: _scripts/verify-pawn-needs-ui.ps1
    type: M
  - path: _scripts/verify-pawn-treasury-operations.ps1
    type: M
---

# 移除统一聚合窗口 · 批次 A-1（需求观察就地承接） · CHG-20261010-ECON-AGG-MIGRATE

**ChangeID**：`CHG-20261010-ECON-AGG-MIGRATE`｜**日期**：2026-10-10｜**作者**：AI（CodeBuddy / DeepSeek-V4.1-Flash）
**用户决策**：「确认」（= 确认分配方案：政策→工坊管理处；账户→工资结算处；台账/报告→结算处/体检中心；需求/照护/总览→**总控面板**）

---

## 一、总工程与批次计划（来自彻底勘察）

**目标**：移除 `Dialog_PawnEconomy`（10 页签 / 8 partial / ≈130KB），把功能分配到 5 个柜台窗口与总控面板。**纪律：先承接、全部可用后才删窗**（保证任何时刻功能不丢失）。

| 批次 | 内容 | 改动面 | 风险 |
|---|---|---|---|
| **A-1** | **需求观察 → 总控**（本批完成） | 1 文件 | 低（内容层宿主无关） |
| A-2 | 照护 → 总控（含 FloatMenu 执行者选择） | 1 文件 + 菜单宿主 | 中 |
| A-3 | 总览 → 总控（14 类查询装配整段照搬） | 1–2 文件 | 中 |
| B-1 | 台账 / 报告 → 结算处 / 体检中心 | 各 1 文件 | 低-中 |
| B-2 | 账户 → 结算处（**须新增宿主能力**：操作人选择 + `QueryTreasuryStamp` 戳） | 2–3 文件 | **高** |
| C-1 | 机构列表 → 工坊管理处 | 2 文件 | 中 |
| C-2 | 政策编辑 → 工坊管理处（12+ 会话态、11 参写通道、二级菜单、卡内滚动） | 3–4 文件 | **最高** |
| 末 | 删除聚合窗口 + 全部深链改造 + 脚本断言同步 | — | — |

**不可搬迁 / 需裁剪项（勘察已定）**：顶部「设置/诊断」按钮行须另找家；跨页共用头部（模式解释/故障/阻塞）**裁掉不复制**（避免 5 份同义提示）；`focus` 字符串机制不迁移（新宿主用自身 `anchor`）；旧动词按钮不复活；旧权益承接留在结算处。

## 二、本批完成内容

**需求观察（原聚合窗口 tab 6）→ 总控面板经济整页**

- 总控经济页第二个按钮由「打开聚合窗口的需求页」改为**就地视图切换**（市场 ⇄ 需求），功能点不丢失；
- 内容层与聚合窗口**完全同源**：复用 `NeedDashboardPanel` + `NeedPanelState`（该内容层自述宿主无关、脚本已断言不引 `Verse.Window`）—— **不存在第二套实现**；
- 刷新节流与聚合窗口一致：**60 tick** + 语言 / 地图可用性 / 感知纪元三重失效；
- 键全部复用（`PC_PEA.Needs.*`），零新增键。

## 二之二、批次 A-2：照护 → 总控（**已完成**）

**内容层抽取（唯一实现）**：新建宿主无关 `CarePanel`（+ `CarePanelState`）—— 原 `Dialog_PawnEconomy.Care.cs` 的 391 行快照 / 绘制 / 执行者菜单**逐字搬入并参数化**（`runtime` / `map` / `rect` / `state`）；跨页工具一律调共享实现（`EconomyLabels.ShortKey/FormatMinor/LocalizedId`、`EconomyRowList.CardSideInset/CardStackGap`、`Text.CalcHeight`），**零第二份副本**。

**双宿主接入**：
- 聚合窗口 `Dialog_PawnEconomy.Care.cs`：391 行 → **23 行薄壳**（只保留页签索引 + 会话态 + 一行委托）；
- 总控面板经济页：新增**第三个视图**「照护」（三个按钮等宽互斥切换：市场 / 需求 / 照护）。

**编译暴露并修正的 3 处跨文件依赖**（薄壳化的连带面，全部改为复用内容层）：
1. 总览页复用照护的 `ServiceLabel` / `CapabilityLabel` ⇒ 内容层将其暴露为 `public`，总览页改调 `CarePanel.*`（消除重复映射）；
2. 聚合窗口切页重置用 `careScroll`/`careTick` ⇒ 改走 `careState.Scroll` / `careState.Invalidate()`；
3. 页签名 `C("Tab")` ⇒ 改 `EconomyLabels.TranslatedOrId("PC_PEA.Care.Tab", "Tab")`（原 `C` 助手随薄壳化移除）。

## 二之三、批次 A-3：总览 → 总控（**已完成**）

**内容层抽取**：新建宿主无关 `OverviewPanel`（+ `OverviewPanelState`）—— 原 330 行**逐字搬入**，
**14 类查询的装配与 60 tick 门控逐条照搬**（回归高风险区，未改查询序列）；跨页工具改调共享实现。

**前置依赖提取（避免删窗时断链）**：`FormatSilver`（政策页）、`CategoryLabel` / `ReasonLabel`（授权菜单页）
⇒ 全部提取到 `EconomyLabels` 为**共享唯一实现**，原三处改为薄委托（既有调用点零改动）。

**双宿主接入**：聚合窗口 `Dialog_PawnEconomy.Overview.cs` 330 行 → **16 行薄壳**；
总控面板经济页新增**第四个视图「总览」**（四按钮等宽，互斥切换：市场 / 需求 / 照护 / 总览）。

**编译暴露并修正的 2 处连带依赖**：切页重置的 `overviewTick`（改走 `overviewState.Invalidate()`）；
页签名 `O("Tab")`（改共享标签入口）。

## 二之四、批次 B-1：台账 → 工资结算处、报告 → 体检中心（**已完成**）

**机制（新）：柜台工作台的次级页承载**。与 A-1/A-2/A-3 的"抽内容层"不同 —— **台账 / 报告本来就是通用行族页**
（与机构 / 岗位 / 库存 / 账户共用 `EconomyRowList` + `runtime.Query(map, focus, tab)`），因此这里不抽内容层，
而是给柜台窗口加一层**切换条 + 次级页明细区**：

| 层 | 改动 |
|---|---|
| 读快照 | `WorkspaceView.SecondaryTabs`（+ 新类型 `WorkspaceSecondaryTab{Index, Label}`）—— 由 `QueryWorkspace` 按机构声明可承载页；**标签在应用层已本地化** |
| 组装点 | `EconomyWorkspaceQueries.SecondaryTabsFor(kind)`：`SettlementOffice → LedgerTab`、`ExamCenter → ReportsTab`，其余空集。**这是本地唯一的类型判断点**（呈现层因此保持 LAND-010 零特判） |
| 几何 | `EconomyWindowLayout.WorkspaceStripHeight = 26f`（只在有次级页时占位；其余柜台的明细区逐像素不变） |
| 呈现 | 窗口新增 `DrawStrip`（「本柜台」+ 次级页，选中态 `ButtonTone.Accent`）+ `DrawHostedRows`（**同一读入口** `runtime.Query(map, anchorKey, tab)` 与**同一行族** ⇒ 与聚合窗口同族同排）；换页时**滚动与测量缓存一并重置**（否则新页继承别页滚动，视觉错位） |
| 文案 | 新增 `PC_PEA.Workspace.Detail.Primary`（本柜台 / This counter）；次级页标签**复用** `Tab.Ledger` / `Tab.Reports`（与聚合窗口同一套键，玩家不会看到两套命名） |

**编译暴露并修正的 1 处**：行族第二参数收的是**视图**（`EconomyView`）而非其行集 —— 编译期即拦截。

**⚠ 本批交付曾带 P0**：切换条「位置 → 聚合页签号」翻译缺失 ⇒ 台账 / 报告查错页（详见 §二之五，B-2 已修复并加断言）。

**未做（下一批）**：机构列表 / 政策 → 工坊管理处属 C-1 / C-2；删聚合窗口与脚本同步属末批。

## 二之五、批次 B-2：账户 → 工资结算处（**已完成**，含 GATE-ARCH P0 修复）

账户页与台账 / 报告不同：它**带写动词**（缴存 / 提取 / 承接旧权益 / 取消交付），因此柜台必须提供原属聚合窗口的
**写上下文**。本批把这三样一并落地：

| 能力 | 落点 |
|---|---|
| 能力声明 | `WorkspaceSecondaryTab.NeedsCommandHost`（账户 = 可写；台账 / 报告 = 只读）。只读页**不挂按钮** —— 台账的 `Cancel` 经核实是**死路径**（`Execute` 首行 `!LegacyFeesAllowed` 恒拒，且 `Query` 已把它剥成纯文本行），故"不搬"= **零丢失**（GATE-ARCH 独立复核） |
| 操作人 | 切换条右侧芯片：点击循环（取模回绕，与聚合窗口同型）；缺操作人用既有 `NoActor` 键；悬停给全文（LAND-008） |
| 国库戳 | `EnsureHostedView` 与视图**同批取样** `QueryTreasuryStamp()`（提交时原样回传，命令首行校验）；提交后 `view = null` ⇒ 下帧重取视图与戳 |
| 写分发 | `HostedSend`：`Treasury.*` → `ExecuteTreasuryCommand`；其余 → `Execute`。与聚合窗口 `Send` **逐字同语义**（GATE-ARCH 逐条比对通过：前缀判据 / actorKey 取法 / revision 来源 / 拒绝消息 / 失效行为） |
| 色调单一来源 | `RowButtonTone` 由聚合窗口**迁入行族** `EconomyRowList`（两宿主共用；避免同一动词两种颜色） |
| 移除冗余入口 | 删除 `WorkspaceVerb.OpenAccounts` 与结算处的深链动作 —— 账户已就地承载，同一窗口不再有两个同名入口（已核实无孤儿引用、不入存档） |

### ⚠ GATE-ARCH 抓出的 P0（**B-1 引入、本批修复**）

`EnsureHostedView` 曾把**切换条位置** `hostedTab`（0,1,…）直接当**聚合页签号**传给 `Query`：位置 0 命中
`BuildInstitutions`（机构页）⇒ **B-1 的台账 / 报告与 B-2 的账户全部查错页**、写动词永不可达（且 B-2 又删了深链
⇒ 构成功能回退）。此项**编译与既有 1286 条单测天然覆盖不到**（`SecondaryTab.Index` 当时全仓零读取）。

**修复（结构性收口，非补丁）**：新增纯函数 `WorkspaceView.HostedTabIndex(position)` 作为"位置 → 页签号"的
**唯一翻译点**，窗口只经它取页签号；并新增 `Tests/WorkspaceSecondaryTabTests.cs`（4 条断言）锁定
"位置 0 → 页签号 3 / 位置 1 → 页签号 4 / 越界 → -1"。**IL 级核实**：`EnsureHostedView` 现为
`ldfld SecondaryTabs → callvirt HostedTabIndex → callvirt Query`（已部署构建）。
另加护栏契约：可写承载页的动词集合限定为 `Reverify` + `Treasury.*`；导航动词 `Focus/Policy` 不属于承载页，
越界即 fail-closed（`Execute` 拒绝 + 如实回报），不会静默无效。

**教训（写给 C 批）**：快照里新增的"翻译字段"必须**有消费端断言** —— 仅靠类型系统无法发现"字段被读取但语义用错"。

## 二之六、批次 C-1：机构列表 → 工坊管理处（**已完成**，含一次组件抽取）

机构页与其他页不同：它是**卡片列表**（自定义行高 + 卡内按钮 + 整卡可点 + 索引缓存），且绘制实现原先
**私有在聚合窗口的 partial 里**。柜台要用，就必须先把它变成组件。

| 层 | 改动 |
|---|---|
| 组件抽取 | 新增 `EconomyInstitutionCards`（internal，UI 层）：卡片高度 / 绘制 / 命中 / 索引缓存（view 引用 + 语言键失效）/ chip 与时段度量全部**原样迁入**；对外只有 `IsCardRow` / `HeightOf` / `Draw` 三个入口，**不含任何导航语义**（命中即返回 true 交宿主） |
| 聚合窗口 | 改为委托（懒建 `Cards`）：`HeightOf` 作自定义行高、`Draw` 作自定义行绘制，命中后的导航（切政策页 + 聚焦 + 滚动定位）**逐字不变** ⇒ 零回归 |
| 能力位 | `WorkspaceSecondaryTab` 新增 `NeedsNavigation`，并新增 `HostsRowVerbs => NeedsCommandHost \|\| NeedsNavigation` 作为**挂按钮的唯一判据**（只看 `NeedsCommandHost` 会把机构页的「政策」「配置」按钮静默丢掉） |
| 声明 | `WorkshopOffice → [机构列表(InstitutionsTab, 导航)]` |
| 窗口 | 承载页**焦点** `hostedFocus`（`Focus` 动词就地重定；null ⇒ 本柜台 anchorKey，**柜台自身内容恒读 anchorKey 不受影响**）+ 卡片组件**无条件挂上**（数据驱动：非机构行高 0 且不命中 ⇒ 呈现层零页签特判，LAND-010） |
| 动词分发 | 新增唯一入口 `OnHostedVerb`：`Reverify` 就地核验 / `Focus` 就地重定焦点 / `Policy` 打开该机构政策页（**临桥**：政策页尚未搬进柜台，C-2 改为就地切页签的**唯一改点**）/ 其余走写通道 |

**抽取代价**：`Dialog_PawnEconomy.Institutions.cs`（240 行）**删除**，实现迁入新文件（`A`+`D`）。合法改编 4 处：
`view` 由字段变参数、`CardSideInset` → `EconomyRowList.CardSideInset`（同值）、`KindLabel` 包装 →
`EconomyLabels.KindLabel`（同委托）、`Map` 由字段变构造参数。**保真由 GATE-UI 独立复核**（结论：未丢语义）。

### 关于两条验证脚本的既有失效（本轮一并修复，如实登记）

`verify-pawn-economy.ps1` 与 `verify-pawn-institution-mode.ps1` 是**早期批次的门禁脚本**，其中多条断言锁的是
"当时的实现形状 / 快照值"，随本迁移（行族抽取、实现搬家、字段类型演进、合法接线）**陆续失效**。本轮修好 6 处
（**全部按"断言 = 语义不变量"重写，而非放宽容差**）：

| 断言 | 旧口径（失效原因） | 新口径（不变量） |
|---|---|---|
| 显式折行 / 滚动容器 | 指向对话框主文件（实现已于 P0-6 搬到行族） | 指向 `EconomyRowList.cs` |
| 就绪位 | 锁"全 false"字面量（实现已合法演进为逐位带闸） | 锁**闸门结构**（故障检查 + 恢复证明） |
| 物理结算接线 | "零调用方"（缴存交付完成点已合法接线） | **唯一通路**（调用点数相等） |
| 旧槽位守卫 | 锁 IL **形状**（`int → long` 使 `ldfld;brfalse` 变 `cgt.un;brfalse`） | 源：hard early return + IL：**读取顺序** |
| 模式键计数 | 硬编码 `-eq 22` 快照（文件已合法增至 59 键） | 不重复 + 不低于基线 + 双语一致 |
| 缺键扫描 | 漏排除自带前缀的呈现器（`ShadowPlanPresenter`） | 排除表补齐（其键由 needs-ui 脚本独立校验） |

**仍未通过（不属本批，且不应由本批重钉）**：`verify-pawn-institution-mode.ps1` 收尾是**工件钉**
（`Current DLL differs from tested DLL`，其后还有 `Assembly-CSharp.dll` 哈希钉与
`EconomyLanding-B241-Full.trx` 路径钉）—— 按设计绑定**该批次发布时的具体工件**，只能由那次审计重钉。
⇒ 该脚本是**批次级发布门禁**，不是通用回归门禁（通用门禁 = `gate-runner.ps1` + `dotnet test`；
本批 `verify-pawn-economy.ps1` 已全绿）。**建议**：末批把工件钉迁入对应批次的发布记录或显式标注"仅 B2.4.1 复现用"。

## 二之七、批次 C-2：政策页 → 所有柜台（**已完成**，含 GATE-ARCH 两项 P1 修复）

政策页是本次搬迁里**最大的一次抽取**：它散在三个 partial（Policy 539 行 / Authorisation 87 行 / StockTarget 58 行），
且天然有状态（快照 + **会话草稿** + 滚动 + 一次性聚焦键 + Hint 测量缓存）。

| 层 | 改动 |
|---|---|
| 内容层 | 新增 `PolicyPanel`（**有状态实例**，与 CarePanel 的"state + 静态内容层"不同 —— 理由：草稿/滚动只服务一个宿主，收在同一实例里使迁移**近乎逐字**，把"搬迁引入新语义"压到最低）。对外仅 `Draw` / `FocusOn` / `ResetView` 三接口 |
| 聚合窗口 | 三个 partial 收敛为**宿主壳**（539 → 42 行）+ 删除 `Authorisation` / `StockTarget` 两个文件（内容已迁入面板） |
| 契约 | `WorkspaceHostedRenderer { RowList, Policy }`（快照声明渲染器 ⇒ 呈现层零页签判断）；删除 `WorkspaceVerb.Policy`（政策已是承载页）；新增纯函数 `HostedPositionOf`（页签号 → 位置，`HostedTabIndex` 的**逆向翻译**，含 round-trip 断言） |
| 声明 | **五个柜台全部承载政策页**（劳务站/商店 = 仅政策页）；行动区的公共「政策」深链动作**删除**（同一窗口不留两个同名入口） |
| 窗口 | `DrawHostedRows` 按 `Renderer` 分发（政策页不走行族读入口 ⇒ 零多余查询）；`OnHostedVerb("Policy")` 改为**就地切页 + 聚焦该机构**；切到政策页时 `FocusOn(hostedFocus ?? anchorKey)` |

### ⚠ GATE-ARCH 抓出的两项 P1（均已修复）

**B1｜聚合窗口"滚入可视区"静默失效**：我把切页签的会话归零写成了 `policyPanel.FocusOn(null)` —— 它连**一次性聚焦键**一起清。
而该键与"切到政策页"发生在**同一次事件**里（点机构卡的「政策」按钮既设键又置 tab），宿主下一帧必然先跑归零分支再绘制 ⇒
三条聚焦路径（深链构造 / 机构卡 / 行尾按钮）全部失效。**修复**：新增 `ResetView()`（只清滚动与快照，**绝不动聚焦键**）。

**B2｜面板按绝对原点绘制（柜台会整页叠到窗口左上角）**：面板的外层矩形写的是 `(0,0)` 绝对坐标 —— 在聚合窗口
（rect 原点恒为 `(0,0)`）等价，但柜台传入的是列布局里的 `area`（非零原点）⇒ 标题/Hint/滚动视口全部错位、命中随之错位。
**修复**：外层矩形一律相对 `rect.x/rect.y` 定位；滚动视图**内部**保持局部坐标（`BeginScroll` 返回原点 0 的内容区，与行族同一约定）。
**同型排查（A 批连带修复）**：`CarePanel`（3 处）与 `OverviewPanel`（2 处）有完全相同的隐患，而**A 批实机从未跑过**
（一直是 NOT RUN）⇒ 本轮一并修好，否则总控面板的经济页也会叠到左上角。

**次要项处置**：A3 死代码（宿主壳里 6 个无调用方的薄委托 + 主窗口的 `RowButtonWidth`）已删；A4 文案（`Workspace.NoActions`
仍指向聚合窗口）已同步双语；A5 `Invalidate()` 无用接口被 `ResetView()` 取代；A7 末批断链面（口径见 §四）。

**未做（末批）**：删聚合窗口 + 退役已搬页签 + 把 `Dialog_PawnEconomy.*Tab` 常量与两处深链调用点一并处置 + 脚本同步。

## 二之八、批次 D-1：岗位 → 工坊管理处、库存 → 商店（**已完成**，末批的最后前置）

末批（删聚合窗口）前必须把**剩下的两页**也搬出去，否则它们会随窗口一起消失。两页都是**通用行族页**，
且其行动词早已被 `Query` 的动词白名单剥成纯文本行 ⇒ **只读承载 = 零丢失**（与台账同一判定）：

| 页 | 宿主 | 为何只读 = 零丢失 |
|---|---|---|
| 岗位（`JobsTab`） | 工坊管理处 | 行动词 `Withdraw` / `Terminate` 走 `Execute`，而 `LegacyFeesAllowed` 恒 false；白名单（`Focus/Policy/Reverify/Hint/Treasury.*`）**不含**二者 ⇒ 聚合窗口里也没有可点按钮 |
| 库存（`StockTab`） | 生活用品商店 | 行动词 `Stock`（上架）同样不在白名单 ⇒ 同样无按钮；余下是存货清单与每件估值（只读事实） |

### 十页承载覆盖（末批前置已闭合）

| 聚合页签 | 承载宿主 |
|---|---|
| 机构列表 / 岗位 | 工坊管理处（柜台切换条） |
| 库存 | 生活用品商店（柜台切换条） |
| 账户 / 台账 | 工资结算处（柜台切换条） |
| 报告 | 体检中心（柜台切换条） |
| 政策 | **全部柜台**（C-2） |
| 需求 / 照护 / 总览 | 主控窗口经济分页（A-1 / A-2 / A-3） |

⇒ 聚合窗口 `Dialog_PawnEconomy` 已**全冗余**，末批可执行。

### 末批影响面（已枚举，4 处外部引用 + 脚本）

1. `EconomyWorkspaceQueries.cs`：7 个 `Dialog_PawnEconomy.*Tab` 常量引用 ⇒ 需迁到**中立页签号持有者**
   （常量随窗口类一起消失，而这些引用活在柜台侧）；
2. `EconomyWorld.cs:51`：`new Dialog_PawnEconomy(Map, GetUniqueLoadID())` —— **经济入口**，需改为打开柜台窗口 / 主控经济分页；
3. `MasterControlTabWindow.Economy.cs:200`：主控的「打开经济窗口」按钮，同上；
4. `Window_InstitutionWorkspace.cs:430/439`：政策页签常量 + **深链兜底**（所有柜台都已承载政策页 ⇒ 该分支已不可达，应随之删除）。

脚本侧：`verify-pawn-institution-mode.ps1` 与 `verify-pawn-economy.ps1` 各有一段以 `Dialog_PawnEconomy.cs`
为对象的 RUIF 断言 ⇒ 需改指幸存宿主（`PolicyPanel` / `Window_InstitutionWorkspace`）或退役。

## 二之九、末批：删除聚合窗口 `Dialog_PawnEconomy`（**已完成 —— 迁移收口**）

聚合页**十页已全部有宿主**（见 §二之八 覆盖表）⇒ 聚合窗口整体删除。四件事：

| # | 事项 | 处置 |
|---|---|---|
| 1 | 5 个 partial（主文件 / Needs / Care / Overview / Policy） | **删除**（内容早已在面板层：`PolicyPanel` / `CarePanel` / `OverviewPanel` / `NeedDashboardPanel` / 行族 / 机构卡组件） |
| 2 | 页签号常量（原 `Dialog_PawnEconomy.*Tab`） | 迁到中立的 **`EconomyHostedTabs`**：`0..5` 与 `Query` 的 `switch (tab)` **一一对应**；`Policy = 7` 是**自绘承载页**、**绝不可传给 `Query`**（已在类型注释写明，并在窗口侧以渲染器提前返回保证） |
| 3 | 两个入口 | ① 建筑 gizmo（`EconomyWorld.GetGizmos`）原先"未声明 kind 时回落到聚合窗口" ⇒ 改为**如实报错**（新键 `PC_PEA.KindMissing`，双语）；② 主控经济分页的「市场」按钮（唯一跨窗口入口）**移除**，三个就地视图（需求/照护/总览）各占 1/3 |
| 4 | 柜台内的深链兜底 | 删除（所有柜台都承载政策页 ⇒ 分支不可达；契约若被破坏则记一次错误，**不假装有动作**） |

### 脚本同步（4 个门禁脚本）

| 脚本 | 处置 |
|---|---|
| `verify-pawn-economy.ps1` | RUIF 断言改指幸存宿主（`PolicyPanel`）⇒ **全绿 PASS** |
| `verify-pawn-institution-mode.ps1` | 退役"旧并行开关已退役"（对象随窗口删除 ⇒ 意图由"窗口不存在"直接保证）；RUIF / 折行断言改指 `PolicyPanel` |
| `verify-pawn-needs-ui.ps1` | 退役"聚合窗口构造二进制兼容"；宿主断言改指**主控经济分页**，并把"不得调 legacy / 原版控件"的范围**收窄到需求页宿主方法**（实测确认：按整窗口扫描会命中主控自身 chrome 的 `Widgets.ButtonText`，属无关假失败） |
| `verify-pawn-treasury-operations.ps1` | 版本化国库路由断言改指**柜台工作台**（`ExecuteTreasuryCommand` + `hostedStamp`），并说明为何去掉 `Widgets.|GUI.` 零命中子句（柜台图标走原版 `GUI.DrawTexture`）；另修一处与 institution-mode **同源的过期就绪位形状断言**（改锁闸门结构） |

**三个脚本（institution-mode / needs-ui / treasury）现只停在各自的"工件钉"**（`Current DLL differs from tested DLL`）
—— 那是**绑定该批次发布工件**的设计选择，不由本批重钉（§二之六已登记）；其**全部内容断言均已通过**。

**遗留（如实登记，未做）**：聚合窗口专属的语言键可能成为孤儿（如 `Mode.OpenSettings`）；本批**未做全量键审计**
（属 LOC 门范畴），留待下一次键审计一并清理。

## 三、验证

- 编译 **0 错 0 警**（`BuildStamp 2026-10-10 14:38 UTC`）；`dotnet test` **1286 通过 / 0 失败 / 1 跳过**；部署成功。
- **NOT RUN**（实机）：重启后打开总控面板经济页 → 点第二个按钮 ⇒ 应**就地**进入需求视图（不再弹窗）；再点返回市场视图。

## 四、已知风险（勘察登记，搬迁期需逐批处置）

1. **账户页**的"操作人 + 戳"无宿主承接（唯一必须新增宿主能力的页）；
2. 账户页依赖"焦点 = 公有银行"，不可给其它柜台开；
3. **政策页**会话态最密（12+ 字段）与一次性滚动定位（`policyFocusKey`）语义易失效；
4. 三页的"`BeginScroll` + `try/finally EndScroll` + 自绘滚动条"范式必须一并搬走（异常逃出会造成 GUIClip 失衡、整局 GUI 卡死）；
5. 刷新节流一致但**几何预算不同**（政策卡/总览卡高度由度量派生）；
6. 删文件会先断 5 个脚本/编译清单引用，且 `verify-pawn-institution-mode.ps1:74` 是**既有失效断言**（搬迁时顺手修正，勿当回归基线）。

## 五、回滚

`git checkout -- Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Economy.cs`（单文件；聚合窗口完全未动，回滚后行为与之前一致）。

### 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖实际改动全部文件（1/1）
- [x] 清单范围 ⊆ Scope
- [x] 批次计划与裁剪项已登记
- [x] 无调试代码 / TODO 残留
- [x] 已用 `gate-runner.ps1` 自动校验
