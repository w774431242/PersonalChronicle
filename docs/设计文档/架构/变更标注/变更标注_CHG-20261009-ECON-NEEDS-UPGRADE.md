---
change_id: CHG-20261009-ECON-NEEDS-UPGRADE
date: 2026-10-09
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedAndDeployedByThisBatch
scope: 个人需求页（第 11 批）—— 按最新经济 AI 模型补上屏：循环④改真实三态（可行/受阻/未接通）+ 决策页「劳动与合同」「照护与服务」两个只读事实分区 + 目标页 M3 等待加权；并修复 verify 门禁的三处覆盖缺口（partial 未入键覆盖与指纹、Planning 键集未加载、只读门面缺写路径断言）
rule_ids: AI-001, AI-003, AI-005, AI-011, AI-014, GOV-003, GOV-005, GOV-006, GOV-009, GOV-010, GOV-012, GOV-013, ARC-001, ARC-002, ARC-003, ARC-008, ARC-013, DATA-001, DATA-007, DATA-011, SAVE-001, UI-003, UI-006, UI-008, UI-009, UI-011, LOC-001, LOC-002, LOC-003, LOC-004, PERF-002, TEST-001
files:
  # ── 新增 · 只读事实转文本层与测试（2）──
  - path: Source/PersonalChronicle/Economy/PawnEconomy/PawnEconomyFactsPresenter.cs
    type: A
  - path: Tests/PawnEconomyFactsPresenterTests.cs
    type: A
  # ── 修改 · 受控缺陷修复 · Domain（1）（实机截图发现，见 §5.9）──
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Planning/ShadowPlan.cs
    type: M
  # ── 修改 · 读模型契约与 presenter / 只读查询 / 装配点（4）──
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs
    type: M
  # ── 修改 · 表现层 partial（3）──
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Measure.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Goal.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Decision.cs
    type: M
  # ── 修改 · 文本接口的测试实现（2）──
  - path: Tests/PawnNeedDashboardTests.cs
    type: M
  - path: Tests/PawnShadowPlanningTests.cs
    type: M
  # ── 修改 · 本地化（4）──
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyPlanning.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomyPlanning.xml
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomyNeeds.xml
    type: M
  # ── 修改 · 门禁脚本（1）──
  - path: _scripts/verify-pawn-needs-ui.ps1
    type: M
  # ── 修改/生成 · 文档与证据 + 编译自动改写（3）──
  - path: docs/开发参考/个人需求页-模块索引.md
    type: M
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/NEEDS-UPGRADE_实装验证.json
    type: A
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# 个人需求页 · 按最新模型补上屏（第 11 批）

**ChangeID**：`CHG-20261009-ECON-NEEDS-UPGRADE`｜**日期**：2026-10-09｜**用户指令**：「按最新的AI模型代码对个人需求tab进行完整更新升级」
**入口裁定**：`pc-orchestrator` AI-001 ⇒ 定性为「**把既有只读事实补到只读页 + 修正语义滞后**」，非新功能开发；
四项判定 `Harmony=No / Hardcoding=No / Cross-Mod=No / StaticMutableState=No` ⇒ **不触发 Architecture Review**。

## 1. 背景与缺口（双源核实）

`docs/开发参考/个人需求页-模块索引.md §7.11` 早已写明「M7 落地后必须把『消费与服务』从『未实装』改为真实状态」，但 `ShadowPlanPresenter.BuildCycles` 仍**硬编码** `NeedCycleTone.Unimplemented`（`ShadowPlanPresenter.cs:201` 旧行），且下列**已实装**的模型事实零 UI 消费：

| # | 模型事实 | 数据源 | 本轮处置 |
|---|---|---|---|
| 1 | 循环④真实三态（M7 购买 / M8 服务） | `ShadowPlan.Options`（`ShadowPlan.cs:158`） | ✅ 改为 可行 / 受阻 / 未接通 |
| 2 | M3 等待加权（`WaitingTicks` / `MaximumWaitWeightTicks`） | `ShadowPlan.cs:21`、`LifePolicy.cs:8` | ✅ 目标卡新增等待行（0 时不出行） |
| 3 | M6 合理退出（`ContractExitCode`） | `ContractExitRules.ShouldExit` | ✅ 只读退出预判 |
| 4 | B4 应付工资 | `Employment.ReadObligationsFor` | ✅ 未付金额 + 笔数 |
| 5 | M9 购买退避 | `EconomyEmployment.ConsecutiveFailures` | ✅ 连续失败数 |
| 6 | M9 机构工资可信度 | `WageCredibility`（分档 + 样本数） | ✅ 读数行 |
| 7 | M8 照护授权（受益人视角） | `Care.ReadForBeneficiary` | ✅ 照护与服务分区 |
| 8 | M8 治疗服务能力 | `Care.ReadCapability`（**纯读**） | ✅ 同上 |

**本轮显式未做（如实登记，勿据本批声称已上屏）**：
- **M5 触发线**（下水位+应急额）：既有 `LowWaterWithEmergency` 已承载同一信息，为避免 Domain 改动优先。
- **M7 金额面**（`TotalCostMinor` / `Coverage` / `RequiredByTick`）：`PlanOption` 只有 `Kind/Feasible/ReasonCode`，金额在 `RequirementCostSlice` 且**未随 `ShadowPlan` 暴露** —— 需新增 Domain 出口，超出「只上屏既有事实」边界。
- **U6-A 授权账 / B6 生产读数**：属机构/地图级事实，已在经济窗口承载；进本页会破坏「以 Pawn 为中心」并与既有宿主重复渲染。

## 2. 改动

| 文件 | 改动 |
|---|---|
| `Economy/…/NeedDashboardPresenter.cs` | `INeedDashboardText` 新增 `IdLabel(key)`（跨命名空间复用既有标签，**不新建第二套命名**）；`NeedGoalLine` 新增 `WaitingTicks`/`WaitCapTicks`；新增 `NeedEmploymentView`/`NeedCareView`（状态 + 色调 + 明细行）；`NeedDashboardView` 新增 `Employment`/`Care` 两个**可选**分区（默认 null ⇒ 名册路径零改动） |
| `Economy/…/ShadowPlanPresenter.cs` | `BuildGoals` 透出等待加权结构化字段（上限随行给出，消费方不自造阈值）；`BuildCycles` 循环④改真实三态：`Purchase`/`Service` 的 `Feasible` 决定 Ok；`ConsumptionUnaffordable` ⇒ Blocked（受阻）；其余（无报价/成本未读全/服务未实装）⇒ Unimplemented（**未接通**）；**选项集为空 ⇒ 第四态「未评估」**（`Cycle.State.NotAssessed`，早退路径，不与「未接通」合并）。新增 `OptionOf` / `OptionText`（仲裁区与循环④共用同一实现） |
| `Domain/…/Planning/ShadowPlan.cs` | **受控缺陷修复（§5.9）**：局部函数 `Result` 补传 `options` —— 此前 `ShadowPlan.Options` **恒为空**，M4 四方案比较从未到达任何消费方 |
| `Economy/…/PawnEconomyFactsPresenter.cs`（新） | 只读事实 → 两个分区：输入全是**装配点取好的值**（不查询、不持宿主）；应付只计未结清（`State != Paid`）；无合同 ⇒ 不出岗位/期限/可信度行；无授权且能力不可读 ⇒ 返回 null（整区隐藏） |
| `Economy/…/EconomyEmployment.cs` | 新增三个**纯读**方法：`QueryActiveContract` / `QueryContractExit`（复用 `ContractExitRules.ShouldExit`，**不** Terminate、**不** ReleaseWorkTypes）/ `QueryPurchaseBackoffFailures` |
| `Economy/…/EconomyNeedDashboard.cs` | `QueryNeedDashboardSingle` 装配两个新分区；服务能力走**纯读** `QueryServiceCapability`（不用会推进 revision 的 `RefreshServiceCapability`）；`VanillaNeedDashboardText` 实现 `IdLabel` |
| `Economy/…/NeedsCyclePanel.Measure.cs` | 新增两分区的状态行 + 明细行测量与决策页总高；目标页新增 `GoalWaits` 行测量 |
| `Economy/…/NeedsCyclePanel.Decision.cs` | 四循环之后插入两个事实分区（`DrawFactSection`：分区标题 → 色点 + 着色调状态 → 明细行）；几何与测量公式逐项对应 |
| `Economy/…/NeedsCyclePanel.Goal.cs` | 目标卡插入等待行（空串不画，避免每卡一行 0） |
| `Tests/PawnEconomyFactsPresenterTests.cs`（新） | 9 用例：合同/退出/应付（只计未付）/退避/可信度、无合同路径、照护隐藏与复用 `PC_PEA.Care.*` 标签、循环④三态（可行/受阻/未接通/未报告） |
| `Tests/PawnNeedDashboardTests.cs` / `Tests/PawnShadowPlanningTests.cs` | fake `INeedDashboardText` 实现 `IdLabel`；前者把 `PawnEconomy.xml` 纳入键源（缺键即抛，有牙齿） |
| `Languages/{zh,en}/Keyed/PawnEconomyPlanning.xml` | **+31 键 · 删死键 1 · 改值 1**（103 − 1 + 31 = 132）：劳动与合同 20 + 可信度分档 4 + 照护 4 + 循环状态 2 + `OptionUnreported` 1 = 31；删 `Cycle.Unimplemented`；改值 `Cycle.State.Unimplemented`：未实装/Not implemented → **未接通/Not wired**；102 → 132 键 |
| `Languages/{zh,en}/Keyed/PawnEconomyNeeds.xml` | +3 键（`Cycle.Section.Employment` / `Cycle.Section.Care` / `Cycle.Waiting`）；110 → 113 键 |
| `_scripts/verify-pawn-needs-ui.ps1` | **门禁覆盖修复**（见 §5）＋ 新分区断言（View 属性 / presenter 入口 / 门面喂入 / 面板消费 / **只读门面写路径禁止清单**）＋ 事实分区测试计数断言（≥9）＋ 指纹补 6 个文件 |
| `docs/开发参考/个人需求页-模块索引.md` | 交付链 §2（+第 11 批）、调用链 §3.1（追加只读事实装配图）、表现层 §3.2（行数 + 新文件）、内容规格 §4、验证基线 §5、已知边界 §6（+两项）、接手红线 §7（第 8/11 条改写，新增第 13/14 条） |
| `…/实装落地/NEEDS-UPGRADE_实装验证.json`（新） | 由 `verify-pawn-needs-ui.ps1` 生成：29 条文件指纹（sha256 + 结构锚点）+ 键数 + 测试计数 + IL 调用清单 + limits |
| `Source/PersonalChronicle/BuildStamp.g.cs` | 编译自动改写（BuildStamp `2026-10-09 15:30 UTC`）；按项目先例列入清单 |

## 3. 不变量与边界

- **只读**：`QueryNeedDashboardSingle` 不得出现 `Terminate` / `TrySignForOpportunity` / `CreateCareAuthorization` / `ChargeCareAuthorization` / `RefreshServiceCapability` / `EvaluateServiceCapability` / `ReleaseWorkTypes` / `TrySubmitRestockProposal` —— 已写成 **IL 断言**（写路径一旦混入即 BLOCKED）。
- **宿主无关**：`NeedsCyclePanel` 仍不引用 `GameComponent_PawnEconomy` / `Verse.Window` / `ITab_Pawn_PersonalNeeds`（既有断言保持）；会话态仍在 `NeedPanelState`（零 static）。
- **未知不当零**：退出码 `None` ⇒ 「无退出条件」；无应付 ⇒ 「无」；可信度无样本账 ⇒ **不出行**（而非 0）；照护无授权且能力不可读 ⇒ 整区隐藏。分区判定全部走结构化字段（GOV-009）。
- **性能**：新增读数全部为 O(账本条数) 的字典/列表读取，且 ITab 已有 **60 tick 刷新门控**（`ITab_Pawn_PersonalNeeds.NeedRefreshTicks`）⇒ 非每帧查询。
- **零 Save 变更**：不新增/修改任何持久化字段（SAVE 面 **N/A**）；不新增 Harmony、不新增程序集引用、不新增 RUIF 成员面。

## 4. 验证基线（全部实跑）

| 项 | 判据 | 结果 | 证据 |
|---|---|---|---|
| 编译 | 输出无 `error CS` / 警告 | **0 错 0 警** | `_scripts/compile_with_sdk.ps1`（BuildStamp **15:40 UTC**，实机缺陷修复后重跑） |
| 全量离线测试 | dotnet test（net48） | **1188 通过 / 0 失败 / 1 跳过（总 1189）**（本批前基线 1166/0/1；**本批新增 10 例**，其余增量为并行批次） | `Tests/TestResults/NeedsUpgrade-Fix1.trx` |
| 页级门禁 | `verify-pawn-needs-ui.ps1`（throw 即失败） | **PASS**：**247** 双语键 / 40 + 10 用例 / IL 结构断言全过 | 本轮 stdout：`PASS: RUIF read-only needs page; 247 bilingual keys; 40 new tests; full 1188 passed, 1 skipped.` |
| 键文件 | 良构 + 无 BOM + 键集一一对应 + 占位符一致 | 132/132（Planning）、113/113（Needs） | 脚本自检 + verify 断言 |
| 测试的"牙齿" | 中间运行真实失败 | 首跑 `GoalLineCarriesWaitingTicksAndCap…` **真失败**（`ShadowPlan` 默认投影对 `Gap==null` 的 NRE）⇒ 显式传 `lastKnownActive` 后全绿 | `PawnEconomyFactsPresenterTests` 两轮运行对照 |
| Gate 独立审计 | 5 份 Gate Record | ARCH **PASS** / DATA **PASS** / SAVE **N/A(附理由)** / UI+LOC **PWE** / PERF+TEST **PWE** / COMP **N/A(附理由)+RUIF PASS**；P0/P1 = 0 | `.codebuddy/audit/gate-records/GR-CHG-20261009-ECON-NEEDS-UPGRADE-{ARCH,DATA-SAVE,UI-LOC,PERF-TEST,COMP}.md` |
| GATE-DELTA | `gate-runner.ps1 -Base HEAD -Annotation <本文> -ExcludeFromFile … -Blocking` | **PASS**（标注覆盖 19 个改动文件，无遗漏/超范围/残留） | 脚本 stdout；范围外清单 `.codebuddy/tmp/needs-upgrade-out-of-scope.txt` |
| 部署 | `deploy-to-game.ps1` | **已执行（用户指令 2026-10-09 23:43）**：233 文件同步（Assemblies 1 / Defs 36 / Languages 66 / Patches 3 / Textures 105 / About 13 / Fonts 8 / README 1）；发布区 DLL 与源**双侧 SHA-256 一致** `1130D691…F807`（2,082,816 B）；备份 `_backups/release-deploy/deploy_20261009_234322_419` | 脚本 stdout + 哈希比对 |
| 部署期并发声明 | 部署时仓内仍有**并行写入者**活动（`EconomyCare.cs` / `Dialog_PawnEconomy.Overview.cs` / `.Policy.cs` / `PawnEconomy.xml` 于 23:36–23:42 被非本批修改）⇒ 本次部署的 DLL **包含那些改动**（已通过全量 1188 例验证为绿）；若并行会话在其后重建并再次部署，本批 DLL 会被覆盖 —— 复验时请以发布区 DLL 哈希 `1130D691…F807` 为判据 | 文件时间戳扫描 + `DeployCheck.trx` |
| **实机** | 重启游戏看新分区/等待行/循环④ | **NOT EXECUTED**（沙箱无游戏进程） | — |

**并发一致性声明（GOV-012）**：审计期间工作区存在并行写入者（U6-B1 Evidence / U6-A3 Restock 等批次在 15:10–15:23 重建过 DLL）。首轮证据（`NeedsUpgrade-Final.trx`）绑定 15:09 快照，GATE-TEST 判为**记录级 P1（证据链漂移，非功能缺陷：本批 49 例在所有快照全绿）**。**处置**：P2 修复后已**重跑编译 + 测试 + verify**（15:30 UTC，同一时刻自洽），本文所有引用数字以 **V3 快照**为准。

## 5. 已修缺陷（本轮自曝：门禁曾"绿而失守"）

`verify-pawn-needs-ui.ps1` 此前有三处覆盖缺口 —— 它一直是 **PASS**，但下列内容**根本不在校验面内**：

1. **4 个 partial 的字面量键不受校验**：`$uiPaths` 只含 `NeedsCyclePanel.cs`，而 `N("…")` 文案大半在 `.Measure/.Demand/.Goal/.Decision` 里 ⇒ 新增文案漏翻译不会被发现。**已纳入**。
2. **4 个 partial 不在文件指纹内**：改动不进入 `B1.3_实装验证.json` 的 sha256/锚点 ⇒ 验收定位不到实际改动。**已纳入**（另补新 presenter 与只读查询文件）。
3. **`PawnEconomyPlanning.xml` 未进键集**：`$keys` 只加载 `PawnEconomyNeeds.xml`，而 `Planning.*` 文案全在另一文件 ⇒ 修好第 1 项后必然误报。**已改为键集合并两文件**（各自做 BOM/重复/空值检查 + 跨文件命名空间冲突检查）。
4. **只读门面缺"写路径禁止"断言**：原有断言只覆盖 `Execute|Send|Query|…` 等通用动词；本轮新增分区引入了合同/照护/生产域 ⇒ 补上 8 个域级写方法的 IL 禁止清单。

> 教训（已写入索引 §7.14）：**扩页必须同步门禁覆盖面**，否则"脚本 PASS"不等于"红线守住"。

**Gate 独立审计追加发现并已受控修复（P2，P0/P1 = 0）**：

5. **等待行空串测量多算一行**（GATE-UI P2-UI-2）：`Wrapped("")` 并不返回 0（恒 ≥ `RowHeight + Gap`），无条件相加会让卡底留白、滚动总高虚高。已改 `goal.WaitingTicks > 0 ? Wrapped(...) : 0f` —— 与绘制侧同源的**结构化条件**。
6. **状态行测量/绘制可用宽差 4px**（GATE-UI P2-UI-1）：测量按 `width − SpaceXs`、绘制按 `width − dot − SpaceXs`，文本像素宽落入该窗口时会在绘制期二次折行并与下一行重叠。已提共用常量 `FactDotSize` 使两侧同源。
7. **验证指纹与键覆盖仍有盲区**（GATE-LOC P2-LOC-1）：`ShadowPlanPresenter.cs`、两个测试文件、两个 `PawnEconomyPlanning.xml`、本索引文档不在指纹内；两个 presenter 不在字面量键检查面内。已补入 `$paths` / `$uiPaths`，并让键检查同时接受 `PC_PEA.Needs.` 与 `PC_PEA.Needs.Planning.` 两个落点（键仍必须真实存在）。
8. **未修 · 如实登记**：Care 分区无 per-pawn 上界（GATE-UI P2-UI-3；域账本全局上限 4096、per-pawn 无界）—— 正常规模小，列为后续收口项；另有 2 条 P2 观察（stringly 原因码比对、全角括号沿用既有风格）与 1 条 P3（循环列宽两处字面常量）留待后续。

### 5.9 实机反馈缺陷修复（P1 · 2026-10-09 用户截图发现）

**症状**：循环④明细显示「购买 **未报告** · 服务 **未报告**」，且「影子仲裁」区从未出现四方案比较行。

**根因（已定位到行）**：`Domain/PawnEconomy/Planning/ShadowPlan.cs:214-225` 的局部函数 `Result(...)` 构造 `ShadowPlan` 时**漏传第 18 个参数 `options`** —— `BuildPlanOptions` 的结果在 `:294` 算出后只赋给局部变量，从未随计划出口 ⇒ `ShadowPlan.Options` **恒为空集合**。

**影响面**（不止循环④）：
- 「影子仲裁」区的四方案行（`Build` 的 `if (plan.Options.Count > 0)`）**从未显示过** —— 即 **M4「四方案可行性上屏」这批的实装从未真正接通**；
- M7 的购买可行性/原因码搭同一条链 ⇒ 玩家从未在界面上看到"为什么没选购买/服务"；
- 本批循环④读到空集 ⇒ 显示「未报告」。

**修复**：
1. `Result` 补传 `options`（**零字段、零算法、零契约变更**；早退路径读到的是 `:208` 的初始空列表 —— 这是正确语义：计划在产出方案比较之前就结束了）；
2. 表现层区分**第四态「未评估」**（选项集为空，早退）与「未接通」（评估过了但无报价/供给），并让仲裁区与循环④共用同一 `OptionText`（口径唯一化）。

**回归锁**：`Tests/PawnShadowPlanningTests.FourPlanOptionsLeaveTheBuilderOnTheNormalPathAndStayEmptyOnAnEarlyExit`（正常路径 4 条 / 早退空集）+ `Tests/PawnEconomyFactsPresenterTests.ConsumeCycleSaysNotAssessedWhenThePlanEndedBeforeComparingOptions`。

**同组截图已排除的非缺陷**（核实后不修）：
- 「原因：事实缺失、陈旧或来源范围不一致」= `Reason.FactsUnavailable` 的**完整**译文（非截断）；
- 「最低储备水位 0 银币 / 储备目标 0 银币」= 账户未知时**政策参数的真实值**（`LifeBudget.cs:108-109` 明确传 `Fact<long>.Known(target/low)`），**不是**"未知当零"；
- 制度「准备中」⇒ 不签约/不计费 = **预期行为**（归验收单元 `CHG-20261008-ECON-ACCEPT`）。

## 6. 待办 / 例外 / 三态

- **例外（EXC）**：**0 件**（无 Harmony、无硬编码、无第三方引用、无静态可变状态、无 Save 变更）。
- **未做**：见 §1 末三项（M5 触发线 / M7 金额面 / U6-A·B6 机构级事实）。
- **做不到**：0。
- **NOT EXECUTED**：实机渲染与点击、60 tick 刷新门控下的切 Pawn 一致性、窄窗口折行、中英字形、部署 —— 全部属验收单元 `CHG-20261008-ECON-ACCEPT`。

---
AI 署名：本变更标注与实现由 AI 助手完成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
