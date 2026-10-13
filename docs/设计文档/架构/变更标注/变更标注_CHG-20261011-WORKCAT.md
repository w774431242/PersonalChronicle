---
changeId: CHG-20261011-WORKCAT
title: 工作识别注册表（S1）+ 数据表格（S2a/S2b：筛选/排序/搜索/列宽拖拽/密度/行展开）
date: 2026-10-11
state: Implemented
layer: Economy/PawnEconomy（读模型 + 呈现数据组装）· Application/PawnEconomy（表模型纯函数）
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Planning/VanillaOrderCatalogue.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyVanillaWork.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkspaceQueries.cs
    type: M
  - path: Tests/VanillaOrderCatalogueTests.cs
    type: A
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  - path: _scripts/compile.rsp
    type: M
    note: 构建脚本自动同步；非手工编辑
  - path: Source/PersonalChronicle/Application/PawnEconomy/EconomyTable.cs
    type: A
    note: S2a 表模型 + 会话态 + 排序/筛选纯函数
  - path: Source/PersonalChronicle/Application/PawnEconomy/EconomyView.cs
    type: M
    note: S2a EconomyRow 追加可选列化字段
  - path: Source/PersonalChronicle/Application/PawnEconomy/WorkspaceView.cs
    type: M
    note: S2a 快照携带表格声明
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.cs
    type: M
    note: S2a 筛选条 / 表头排序 / 列化行渲染；S2b 快照期按区段 Key 取表状态 + 切换条改分段件（表格 chrome 已切片）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.Table.cs
    type: A
    note: S2b partial 切片 —— 表格 chrome（工具栏/筛选/搜索/密度/列宽拖拽/行展开）与会话态（ARC-013 大文件纪律）
  - path: Tests/EconomyTableOpsTests.cs
    type: A
    note: S2a 4 条 + S2b 7 条（三态/稳定/分段/搜索/Apply/列宽布局/WidthsFor）
  - path: Tests/WorkspaceSecondaryTabTests.cs
    type: M
    note: S2b 追加 Tables 非 null 契约测试（GATE 第二轮 P0 修复的防回归）
  - path: docs/设计文档/功能模块/经济体系/实装落地/交接单_20261011_UI迁移与工作目录.md
    type: M
    note: S2b 会话续记（状态/进度/Gate 两轮/实机待验清单/文件地图；GATE-ARCH 文书链条要求）
---

# 变更标注 · CHG-20261011-WORKCAT（S1）

## 一、用户需求

「原版的这些工作加上本 mod 的工作岗位」—— 前端必须识别**原版 Orders 菜单的全部工作**
（图：开采 L / 伐木 B / 割除 Y / 收获 H / 狩猎 O / 宰杀 M / 驯服 N / 搬运 P）**以及本 Mod 的车间岗位**，
并且在后续对话中明确「以数据表格可以筛选、排序」。

## 二、本次交付（S1：**先看得见，再接通**）

| 层 | 改动 |
|---|---|
| 注册表 | 新增 `VanillaOrderCatalogue`：**8 张原版订单**一行一条（订单 Key / `WorkTypeDef` / 待办口径 / **接线状态** / designation defName），并按**相邻同工作类型**归并出 6 个组（Mining / PlantCutting / Growing / Hunting / Handling / Hauling）—— "已派发 / 可派发 / 每份"只有在这一级才精确（合同按工作类型派发） |
| 计数 | 新增 `QueryOrderBacklog` / `QueryCatalogueBacklogSums`；**口径与既有计酬链同源**（复用 `CountPlayerDesignations`：玩家下达 ∩ 当前仍存在），逐段相减保证不超上界 |
| 去重 | `CountVanillaBacklog`（签约路径在用）**改为委托注册表** —— 原 switch 的两条分支变成注册表里的两行，消除"同功能两处实现" |
| 工作目录 | 劳务站 §1 由"两个工种硬编码行"改为**工作目录**：逐组一行，列出 工作类型 · 订单明细 · 待办 ·（已接线组才有）已派发/可派发/每份 · 工资 · **计酬状态**；**未接线 5 项照常计数并标注**（绝不静默漏付） |
| 岗位族 | 新增 `AddCataloguePositionRows`：本图**开放的车间岗位**（车间区 · 工资 · 时段 · 在岗/容量 · 计酬）—— 与工坊管理处同源，劳务站作为"接活总台"可见 |
| KPI | 「割除」「采矿」两枚 → **「可计酬待办」「未接线待办」**：订单从 2 张扩到 8 张后逐工种 KPI 会溢出 4 槽宽档（LAND-006 阈值 160），而"能付的活 / 看得见却付不了的活"这两数才是玩家需要的 |
| 诚实性 | 「计酬」列三态：`已接线` / `⚠ 未接线（完成信号未接入，暂不计酬）`；未接线组**不给派发覆盖率进度**（不给假进度） |

**接线状态（如实）**：已接线 3 项（开采 / 伐木 / 割除 —— 各有既有对象级捕获点）；**未接线 5 项**（收获 / 狩猎 / 宰杀 / 驯服 / 搬运 —— 完成信号在 S3 逐族接入，每个挂点须先 Cecil 静态核实）。

## 三、验证

- 编译 **0 错 0 警**；`dotnet test` **1300 通过 / 0 失败 / 1 跳过**（新增 4 条注册表断言：覆盖 8 张单 / 接线状态与真实挂点一致 / 单指定口径必给 defName / 相邻同类型归组正确）
- 接线（Cecil）：`CountVanillaBacklog → QueryOrderBacklog`、`QueryCatalogueBacklogSums → QueryOrderBacklog`、`AddWorkCatalogueRows → QueryOrderBacklog`
- 本地化：两语言各 **324** 节点，**缺失 0**（新增 15 键 ×2）
- **NOT RUN**：实机截图（沙箱无游戏进程）

## 三之补、S2a：数据表格（**排序 + 筛选**，与行族同源）

按用户「以数据表格可以筛选、排序」的要求，并在 `pc-orchestrator` 跑完 **AI-001** 后落地（其裁决：
复用 `EconomyRow` 可选字段 + 行族自定义行通道 **优于**新建独立表格组件 —— 后者会复制测量/滚动/命中，
即"同功能两处实现"；并明确点出"排序改了行序却沿用旧 view 引用 ⇒ 行族测量缓存陈旧"的坑）。

| 层 | 改动 |
|---|---|
| DTO | `EconomyRow` 追加**可选** `Cells`（列化载荷）/ `CellSorts`（排序数值）/ `Tag`（筛选标签）—— 与 `Progress` 同一先例，既有全部行零改动；**AI-001 已核实零 Scribe 面 ⇒ 非 schema 变更** |
| 表模型 | 新增 `EconomyTable`（列集 + 可筛选面）/ `EconomyTableColumn`（稳定列键 / 已本地化标签 / 预量宽 / 是否数字列）/ `EconomyTableState`（会话态）/ **`EconomyTableOps`**：`Sort` / `Filter` / `Apply` 三个**纯函数**（零 UnityEngine，可离线断言） |
| 排序语义 | **只排数据行，说明/分区行留在原位**（否则分区标题被排进数据中间、表格失去语境 —— 由断言锁定）；数字列按 `CellSorts` 数值排（不是字符串序） |
| 渲染 | 柜台窗口新增**筛选条 / 表头（可点三态排序 ▲▼）/ 列化行**：列宽按声明宽度对可用宽**等比缩放**（只缩不放，单元格各自 D1 省略 + 悬停全文）；行族仍负责测量/滚动/分页/空态 |
| PERF | 排序/筛选在 **60 tick 快照期**完成并产出**新的派生视图引用**（测量缓存随之失效）；筛选条与表头的文本/宽度/带箭头表头 **全部快照期预量**（绘制路径零 `Measure.Width`、零拼接） |
| 数据 | 劳务站"工作目录"改为列化行（未接线组的三列给 `—` 而非 `0` —— 0 会被读成"没人接"）；列集与筛选面由读模型声明（呈现层零列硬编码） |
| 本地化 | 新增 4 键 ×2（工作 / 订单 / 计酬 / 全部）；列标签尽量复用既有键 |

**验证**：编译 **0 错 0 警**；`dotnet test` **1304 通过 / 0 失败**（新增 4 条：数字列按数值排、分区行留原位、筛选取并、`Apply` 不改原集合）；**NOT RUN**：实机截图。
**本轮未派子 agent Gate**（上下文预算）—— 依 AI-001 的 Gate 计划，**下一轮必须补** `pc-ui-loc-guard`（GATE-UI/LAND：六态、命中、降级、1:1）与 `pc-arch-gatekeeper`（GATE-ARCH：单实现与依赖）；`gate-runner`（DELTA）本批已跑。

## 三之补二、S2b：工作合同表 + 搜索 / 列宽拖拽 / 密度 / 行展开（含 S2a Gate 缺陷族修复）

**背景（诚实记录）**：S2a 交付后补派的两个 Gate（`pc-ui-loc-guard` / `pc-arch-gatekeeper`）**一致判 BLOCKED**，
且指向同一 P0：`Window_InstitutionWorkspace` 用 `ReferenceEquals(tableSource, view.Table)` 判断"是否同一张表"，
而读模型**每次快照都新建**表实例 ⇒ 判据恒假 ⇒ 排序/筛选在每次 60 tick 快照被整体复位 ——
**S2a 的头号交付物"可筛选排序"在部署态从未真正上屏**（点击后 1 帧内失效 = F4 禁项"点了没反应"），
且编译/单测/DELTA 都无法察觉。S2b 与缺陷族修复**同批交付**（同一根因：表格 chrome 的会话态与列布局是半成品）。

| 层 | 改动 |
|---|---|
| 会话态 | 表状态**按区段稳定 Key 寻址**（`Dictionary<string, EconomyTableState>`，每表一份、切换互不串）—— 删除 `ReferenceEquals` 判据；行展开按**行 Key** 记（行序随排序变化，下标不可用） |
| 多表承载 | `WorkspaceView.Table`（单表）→ `Tables`（`WorkspaceTableSection`：Key/Label/Table/Rows）；劳务站 = **工作目录 + 工作合同**两张表（列结构不同 —— 合并成一列集必然"别人的列头、我的数据"）；呈现层零机构类型/页签判断（LAND-010） |
| 合同表 | `QueryVanillaContractRows` 列化（承接人/工种/已完成/总件数/已付/未付/每件）+ 搜索键（承接人 · 合同号）+ 展开详情（合同号/签订/到期/岗位键，`GenDate` 快照期格式化）；**已付/未付与 `VanillaWorkStats` 同源**（obligations 按 ContractId 逐合同过滤） |
| 工具栏 | 表切换（≥2 表）+ 搜索框（`InputAtoms.SearchField`，防抖 200ms）+ 密度三档（`TableAtoms.DensitySwitch`）；窄窗降级：宽度不足时整件隐藏（不做半截输入框） |
| 表头 | `TableAtoms.HeaderCell`（内置 ▲▼ **三态**）+ `TableAtoms.ResizeHandle`（列宽拖拽）；不可排序列画静态标签（**不挂死按钮**，F4） |
| 列布局 | **唯一布局函数** `EconomyTableLayout.Compute`（表头/数据行/拖拽共用同一结果）—— 修 GATE-UI P1"表头与列化行双基准错位"；列区几何 `TableColumnArea` 与行族 `CardFrame` 内容区同表达式 |
| 排序 | 三态（无→升→降→无，`EconomyTableState.NextSort`）；**稳定**（段内插入排序）+ **只在连续列化段内回填**（修 GATE-ARCH P2-①"跨分区填槽"） |
| 搜索 | `EconomyRow.Search`（可选字段）+ `EconomyTableOps.Search`（大小写不敏感子串；**丢弃骨架行** —— 搜索是"找行"不是"筛分区"） |
| 行展开 | `EconomyRow.Detail`（可选）；点击整行切换；详情折行与高度**快照期**预算（绘制期零 Measure/CalcHeight）；`▸/▾` 指示条从末列扣 |
| 选中态 | 筛选条 / 表切换 / 次级页切换条统一改 `InputAtoms.Segmented`（选中段有填充）—— 修 GATE-UI P1"Default\|Accent 同色 ⇒ 选中不可辨"（一处修复三处受益） |
| 切片 | 表格 chrome 切出 `Window_InstitutionWorkspace.Table.cs`（partial：870 → **543 + 398** 行，两文件均 <800）—— GATE-ARCH 不变量 #8（>800 行须切片） |
| 拖拽输入 | 用 `Event.current` 的 MouseDrag/MouseUp 而非 `UnityEngine.Input`（编译引用集不含 `InputLegacyModule`；全仓 0 处使用 `Input`，**不新增构建依赖**） |
| 本地化 | 新增 12 键 ×2（表名/搜索提示/合同列/详情）；**删 2 个孤儿键**（`Workspace.Section.VanillaContracts`、`Workspace.VanillaNoContracts` —— 合同表由切换条与通用空态承载）；列头复用既有 `Workspace.Col.Work` / `Wage` |

**验证**：编译 **0 错 0 警**；`dotnet test` **1312 通过 / 0 失败 / 1 跳过**（S2a 4 条 + S2b 7 条 + `S2b_TablesContractIsNeverNull` 1 条）；
`verify-pawn-economy.ps1` **PASS**（339 双语键一致、无重复、Def/ThinkTree 不变）；切片后行数：主文件 **543** + 表格 partial **398**（均 < 800，ARC-013）。

**S2b 两轮 Gate（如实记录，不伪造 PASS）**：
- **第一轮复审**（`pc-ui-loc-guard` / `pc-arch-gatekeeper`）**一致判 BLOCKED**，指出本批**自引入**的缺陷——
  两席独立复现同一根因链，证明并发写入者未污染、结论可复核：
  - **P0**：生产端对**非劳务站柜台**把 `null` 写进 `WorkspaceView.Tables` ⇒ 4/5 类柜台窗口打开即 NRE
    （`view.Tables.Count` 位于 `DoWindowContents` 的 try 之外，首帧异常逃逸、明细区此后每帧静默失败）。
    **已修**：`Tables` 属性化 + `SetTables` 唯一入口（null 归一为空集，**类型层面**消除接缝）+
    契约测试 `S2b_TablesContractIsNeverNull`。
  - **P1**：列宽拖拽**全程不可用** —— `TableAtoms.ResizeHandle` 的返回值来自 `Widgets.ButtonInvisible`
    （GUI.Button 契约 = **MouseUp 帧**才为 true），不能当"按下"判据；且拖拽帧事件会被 hot control 的 `Use()` 改型。
    **已修**：自持 MouseDown/MouseDrag/MouseUp 状态机 + 自算命中（与把手几何一致），状态机推进
    **先于任何 ButtonInvisible**；松手帧抑制表头点击（防误触排序）。
  - **P2/P3 已修**：行内列区二次扣滚动条（18px 错位）；表头列名补悬停全文（LAND-008）；合同行悬停全文补"未付"；
    工资列改短列头键 `Workspace.Col.Wage`（长句键留给政策页）；密度三档宽 176→184（第三档曾压成 48px）；
    表切换/筛选条按**真变化**判定（Segmented 点当前段也返回 changed）；详情折行宽不再二次扣除；
    搜索占位符快照期预翻译；切表时行族滚动归零；清理过期注释与仅快照期使用的字段。
  - **登记为取舍（未改，附理由）**：数字列**表头**左对齐（`TableAtoms.HeaderCell` 内部左对齐写死；数据格仍右对齐——
    改表头对齐须自绘第二套表头或改 RUIF 外部框架，均另立批次）；**汇总页脚**未做；键盘焦点态未接线。
- **第二轮复审结论**（同批两席并行）：`pc-ui-loc-guard` = **PASS WITH EXCEPTION**（其 GATE-LOC = PASS；唯一例外 = 本轮新引入 P2：
  拖拽起手缺竖向约束 —— 数据行 / 工具栏上恰好落到分割线 x 的按下也会起手）；`pc-arch-gatekeeper` = **PASS**
  （1 项 P2：MouseUp 丢失时兜底未实现、注释与实现不符）。两席均独立确认 P0/P1 真修复、契约收紧非"掩盖"。
- **第三轮修复（第二轮结论的直接处置；**未经第三轮子 agent 复审**，如实登记）**：
  ① 起手加**竖向约束**（`area.Contains(mouse)`）并镜像绘制断点（UI P2 + 回审 P3）；
  ② MouseDown 帧**复位悬空拖拽**（`resizingNow && !justEnded ⇒ resizingColumn = -1`；对照 `WorkshopLayoutCanvas`
     的 MouseDown 复位先例），兑现"下次按下兜底"注释（ARCH P2）；
  ③ 修正重快照理由表述（展开折行宽取整列区宽、不随单列变化）与切片行数口径（**543 + 398**）；
  ④ `WorkspaceTableSection.Rows` 收紧为 `AsReadOnly`（ARCH P3 可选项）；
  ⑤ EN 搜索占位符 worker → contractor（本表术语一致）；
  ⑥ 交付物归位：`Tests/WorkspaceSecondaryTabTests.cs` 补入本标注 files，并从范围外清单移除
     （第二轮回审发现 —— 曾构成"已声明交付物被排除"风险）。
- **Gate Records**：两轮四席结论归档于 `.codebuddy/audit/gate-records/GR-CHG-20261011-WORKCAT-*.md`（含证据行号）。
**NOT RUN**：实机（沙箱无游戏进程）—— 排序/筛选上屏、拖拽手感、密度视觉、展开对齐、P0 的实际崩溃形态，均需实机复验。

## 四、未做（登记）

- **S2 余项**：**汇总页脚**（合计行）未做 —— 本批交付筛选/排序/搜索/列宽拖拽/密度/行展开；
- **S3**：5 个未接线族的完成信号（收获 / 狩猎 / 宰杀 / 驯服 / 搬运）；
- **S3**：5 个未接线族的完成信号（收获 / 狩猎 / 宰杀 / 驯服 / 搬运）；
- **S4**：定价模型（含"件"的计量单位与按工作类型定价；涉及 Save schema + Migration）；
- 表格/筛选状态是否跨窗口记忆、是否新增"解约"活命令 —— 待用户裁决（见对话）。


> 2026-10-11续接：S3八订单完成信号、S4两级定价及本轮表格布局修正已由既有变更 CHG-20261012-ECON-CONTRACTIZE §九接续；本件仍仅是S1/S2历史标注。正式12:05 UTC DLL/1429通过0失败1跳过；实机NOT RUN。由AI助手Codex就地更新。
