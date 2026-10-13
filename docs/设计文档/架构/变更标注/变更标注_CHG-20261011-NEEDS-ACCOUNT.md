---
change-id: CHG-20261011-NEEDS-ACCOUNT
date: 2026-10-11
author: AI(Codex)（本轮维护；首轮 CodeBuddy CN）
type: 功能实装（个人需求页 · 生活保障卡内只读个人账户摘要）+ 最小只读接口扩展
scope: 累计 R1 + CodeBuddy 部分 R2 + Codex R2 完成，含高密度 UI、四循环/执行只读解释、食品拒绝诊断端口与测试；需求页账户摘要链路（Domain 只读出口 → 只读门面装配 → 结构化读模型 → 测量/绘制）+ 中英键 + 行为测试 + 页级门禁覆盖面；不含金融写入/结算/预算算法/持久化/Def/Harmony/资产/部署
base: HEAD
task-id: CB-ECON-002
executor: Codex（R2 收尾；保留 CodeBuddy CN 首轮证据）
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Finance/SettlementContracts.cs
    type: M
    symbols: SettlementLedgerState{Ready,Unavailable,Unsupported,Faulted}
    lines: 108-131
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Budget/LifeBudget.cs
    type: M
    symbols: LifeBudget.RefillTriggerMinor / 构造器 trigger 参数 / LifeBudgetPlanner.Build（H 的唯一计算点）
    lines: 52-77, 118-133
  - path: Source/PersonalChronicle/Economy/PawnEconomy/PawnSettlementSaveHost.cs
    type: M
    symbols: PawnSettlementSaveHost.ReadLedgerState（O(1) 只读可用性，不复制账户数组）
    lines: 61-77
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs
    type: M
    symbols: GameComponent_PawnEconomy.QueryNeedDashboardSingle（先判账本可用性再读账户；装配 account 分区）
    lines: 1-9, 72-89
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs
    type: M
    symbols: NeedAccountState / NeedAccountView / NeedDashboardView.Account（可选构造参数）
    lines: 88-101, 215-269, 302-306, 332-343
  - path: Source/PersonalChronicle/Economy/PawnEconomy/PawnEconomyFactsPresenter.cs
    type: M
    symbols: BuildAccount / IncomeState / OutOfScopeIncome / Money2 / AccountTip / BudgetTip
    lines: 1-12, 89-200
  - path: Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.cs
    type: M
    symbols: ShadowPlanPresenter.IsStale（计划陈旧唯一判据）
    lines: 34-44, 49-52
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPanel.cs
    type: M
    symbols: NeedDashboardPanel.RowSpacing
    lines: 215-224
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs
    type: M
    symbols: AccountTwoColumnWidth / AccountButtonMaxWidth / JumpToDecision / DrawContent 空态分支
    lines: 15-22, 60-68, 175-186, 196-209
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Measure.cs
    type: M
    symbols: NeedsCycleMeasure 账户字段 / MeasureAccount
    lines: 34-66, 258-266, 393-467
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Demand.cs
    type: M
    symbols: DrawDemandTab / DrawOverview / DrawAccount
    lines: 12-17, 105-116, 131-135, 137-209
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml
    type: M
    symbols: Key:PC_PEA.Needs.Cycle.Account.{Title,AvailableValue,Balance,Reserved,Target,Gap,IncomeValue,Details}
    lines: 119-126
  - path: Languages/English/Keyed/PawnEconomyNeeds.xml
    type: M
    symbols: Key:PC_PEA.Needs.Cycle.Account.* ×8（占位符与中文一致）
    lines: 119-126
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyPlanning.xml
    type: M
    symbols: Key:PC_PEA.Needs.Planning.Account.{State×6,NoPlan,Income×9,Tip×13}
    lines: 137-165
  - path: Languages/English/Keyed/PawnEconomyPlanning.xml
    type: M
    symbols: Key:PC_PEA.Needs.Planning.Account.* ×29（占位符与中文一致）
    lines: 137-165
  - path: _scripts/verify-pawn-needs-ui.ps1
    type: M
    symbols: 账户分区断言（Account 分区存在 / BuildAccount 唯一 / 门面先 ReadLedgerState / 表现层消费 / 六态标签齐）+ limits +2
    lines: 202-213, 300-302
  - path: Tests/PawnLifeBudgetTests.cs
    type: M
    symbols: RefillStartLineIsExportedByTheBudgetAndMatchesTheDocumentedThresholds / RefillHysteresisFollowsTheExportedStartLine / StartLineIsUnknownWheneverTheBudgetCannotBeEstimated
    lines: 41-68
  - path: Tests/PawnEconomyFactsPresenterTests.cs
    type: M
    symbols: 个人账户摘要 7 个用例 + using Finance
    lines: 12-13, 180-318
  - path: docs/开发参考/个人需求页-模块索引.md
    type: M
    symbols: 交付链第 12 行 / 账户读取链 / 表现层行数 / 内容规格 / 验证基线 / 已知边界 9·10 / 红线 8·9·14·15
    lines: 35-41, 82-93, 95-104, 125-126, 150-167, 178-190
  - path: docs/changelog/v1.4.x_2026-10-08_经济AI需求观察页.md
    type: M
    symbols: 新增 2026-10-11 用户向节（同一页面的新批次段落）
    lines: 13-26
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/B1.3_实装验证.json
    type: M
    symbols: 门禁脚本生成的机器证据（keysPerLanguage 285 / passed 1467 / newCases 40 / factCases 16）
    lines: —
  - path: docs/任务派发/CodeBuddyCN/回报/CB-ECON-002_ACK.json
    type: A
    symbols: 执行器回执
    lines: —
  - path: docs/任务派发/CodeBuddyCN/回报/CB-ECON-002_PROGRESS.json
    type: A
    symbols: 进度回报
    lines: —
  - path: docs/任务派发/CodeBuddyCN/回报/CB-ECON-002_RESULT.json
    type: A
    symbols: 完成回报（ReadyForReview）
    lines: —
  - path: docs/任务派发/CodeBuddyCN/回报/CB-ECON-002_执行回报.md
    type: A
    symbols: 可读执行回报
    lines: —
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Account.cs
    type: A
    symbols: NeedAccountMeasure / MeasureAccount / DrawAccount / AccountCycleColor
    lines: —
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Decision.cs
    type: M
    symbols: DrawDecisionTab / Account.Model.Details / DrawCycleRow
    lines: —
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedAccountModelView.cs
    type: A
    symbols: NeedAccountModelView（采样、状态、覆盖率、只读解释链）
    lines: —
  - path: Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.Cycles.cs
    type: A
    symbols: BuildCycles / NeedCycleId / NeedCycleStatus / 结构化证据
    lines: —
  - path: Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.AccountModel.cs
    type: A
    symbols: BuildAccountModel / Decision / Reason / Next / Details
    lines: —
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyStorage.cs
    type: M
    symbols: DiagnoseLowestFoodQuote / 有界食品费用拒绝原因
    lines: —
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyFoodCostSource.cs
    type: M
    symbols: ReadCosts / LastRejectionReason / 食品原因向计划透传
    lines: —
  - path: Source/PersonalChronicle/Application/PawnEconomy/Planning/PlanningSourcePorts.cs
    type: M
    symbols: 成本来源诊断只读端口
    lines: —
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Planning/ShadowPlan.cs
    type: M
    symbols: 成本拒绝原因链 / ShadowPlanBuilder
    lines: —
  - path: Tests/FoodQuoteDiagnosisTests.cs
    type: A
    symbols: 食品拒绝分支与预算原因链回归
    lines: —
  - path: Tests/PawnNeedsAccountR2Tests.cs
    type: A
    symbols: 四循环状态、执行证据、采样与门面装配回归
    lines: —
  - path: docs/README.md
    type: M
    symbols: CB-ECON-002 当前回执与实现状态索引
    lines: —
---

# 变更标注 · CHG-20261011-NEEDS-ACCOUNT

> **当前轮次：Codex 接手完成 R2（2026-10-11）。顶部 files 为累计范围；§1～§6 为首轮历史证据，不代表本轮构建或部署。当前实装及证据见 §7。R2 已于23:01本机部署、未实机验证；首轮 21:37:25 部署记录仅作历史。**

> 任务来源：用户直接指派「按需求单实装」→ 需求单 `docs/任务派发/CodeBuddyCN/CB-ECON-002_生活保障个人账户组件_需求单.md`（经理侧 `approved: 2026-10-11 20:45`）。执行器 = CodeBuddy CN。
> **两阶段部署**：第一阶段只交付可审查代码与证据（✓ 21:33 收尾 PASS）；第二阶段经用户口头放行「部署」，已于 **2026-10-11 21:37:25** 执行 `deploy-to-game.ps1`（249 文件，含备份 `deploy_20261011_213725_669`），部署后复核游戏侧 DLL 哈希 = 工程件、中英 `Account.*` 键均到位。**仍需重启游戏**才会重载程序集。

## 1. 改动清单（逐文件）

首轮清单为 25 项；顶部 front matter `files:` 现为累计 R1/R2 机器可解析段；下方为人工可读摘要与规则/Gate 关联。

| 层 | 文件 | 类型 | 涉及符号 | 关联规则 | 关联 Gate |
|---|---|---|---|---|---|
| Domain（只读出口） | `Domain/PawnEconomy/Budget/LifeBudget.cs` | M | `RefillTriggerMinor` | DATA-001 / GOV-003 | DATA |
| Domain（只读契约） | `Domain/PawnEconomy/Finance/SettlementContracts.cs` | M | `SettlementLedgerState` | DATA-001 / ARC-005 | DATA |
| Data（账本宿主） | `Economy/PawnEconomy/PawnSettlementSaveHost.cs` | M | `ReadLedgerState` | PERF-001 / SAVE-001（N/A） | PERF / SAVE |
| 装配点 | `Economy/PawnEconomy/EconomyNeedDashboard.cs` | M | `QueryNeedDashboardSingle` | ARC-003 / DATA-001 | ARCH / DATA |
| 视图契约 | `Economy/PawnEconomy/NeedDashboardPresenter.cs` | M | `NeedAccountState` / `NeedAccountView` / `Account` | GOV-009 / UI-001 | UI / ARCH |
| 表现（转文本） | `Economy/PawnEconomy/PawnEconomyFactsPresenter.cs` | M | `BuildAccount` 等 | GOV-010 / LOC-001 | LOC / UI |
| 表现（判据唯一化） | `Economy/PawnEconomy/ShadowPlanPresenter.cs` | M | `IsStale` | GOV-006 | ARCH |
| 表现（复用工具） | `Economy/PawnEconomy/NeedDashboardPanel.cs` | M | `RowSpacing` | UI-001 | UI |
| 表现（壳/跳转） | `Economy/PawnEconomy/NeedsCyclePanel.cs` | M | `JumpToDecision` 等 | UI-001 / UI-007 | UI |
| 表现（测量） | `Economy/PawnEconomy/NeedsCyclePanel.Measure.cs` | M | `MeasureAccount` | UI-002 / PERF-003 | UI / PERF |
| 表现（绘制） | `Economy/PawnEconomy/NeedsCyclePanel.Demand.cs` | M | `DrawAccount` | UI-001 / UI-003 | UI |
| 本地化 | `Languages/{ChineseSimplified,English}/Keyed/PawnEconomyNeeds.xml` | M | `Cycle.Account.*` ×8 | LOC-001 / LOC-004 | LOC |
| 本地化 | `Languages/{ChineseSimplified,English}/Keyed/PawnEconomyPlanning.xml` | M | `Planning.Account.*` ×29 | LOC-001 / LOC-004 | LOC |
| 门禁覆盖面 | `_scripts/verify-pawn-needs-ui.ps1` | M | 账户分区断言 ×6 | GOV-012 / TEST-001 | TEST / DELTA |
| 测试 | `Tests/{PawnLifeBudgetTests,PawnEconomyFactsPresenterTests}.cs` | M | 16 个新用例 | TEST-001 | TEST |
| 文档 | 模块索引 / 用户 Changelog / B1.3 机器证据 / 三份回报 | M+A | — | DOC-001 / REL-002 | REL / DELTA |

**二进制产物不进 `files:`**（GATE-DELTA 对 `.dll` 自动跳过）：`Assemblies/PersonalChronicle.dll` 已由 `_scripts/compile_with_sdk.ps1` 重编（0 错 0 警），SHA-256 `FA9DD16BC239B8020C8812A33BADF792BD8C68C732A34EB466025E78CFEDE473`（2,282,496 B），与 `Tests/bin/Release/net48/PersonalChronicle.dll` 同哈希（页级门禁断言）。**已于 2026-10-11 21:37:25 部署**到 `Mods/殖民地综合管理系统`（249 文件；部署后复核该侧 DLL 哈希与工程件一致）。

## 2. 对 2026-10-09「预算只出现在决策页」布局裁决的修订（用户授权）

- **旧约定载体**：`NeedsCyclePanel.cs:15-16`（类头「零重复」）、`Demand.cs` 政策卡注释、`NeedDashboardPresenter.cs` 的 `Policy` 注释。
- **本轮授权**：需求单 §2 明文「本次用户明确要求顶部个人账户摘要，允许在需求页增加摘要；决策页继续保留预算细节」，并预置「应更新相关注释和布局说明」。
- **修订内容**：需求页只增加**摘要**，且预算两项**直接取用决策页同一份 `NeedBudgetView` 的字符串**；补储判定与启动线只读预算出口的结构化字段。完整预算细节（下水位、应急额、三分配）仍只在决策页。
- **判定**：属**注释/布局说明更新**，非规范偏离 ⇒ **不登记 EXC**（总控只读裁决 D-1 同结论：旧载体是代码注释与模块索引，不是规范条文）。已在三处注释与模块索引 §4/§7-15 留痕。

## 3. 关键 diff 片段（最小可定位）

```diff
// Source/PersonalChronicle/Domain/PawnEconomy/Budget/LifeBudget.cs
+        public Fact<long> RefillTriggerMinor { get; }
...
                 long emergency = checked(target * PlanningPolicy.EmergencyReserveBasisPoints / PlanningPolicy.BasisPoints);
                 long trigger = Math.Min(target, checked(low + emergency));
                 bool refill = target > 0 && cash < target && (previousRefill || cash < trigger);
...
-                    Fact<long>.Known(emergency));
+                    Fact<long>.Known(emergency), Fact<long>.Known(trigger));

// Source/PersonalChronicle/Economy/PawnEconomy/PawnSettlementSaveHost.cs
+        internal SettlementLedgerState ReadLedgerState()
+        {
+            AssertThread(); Recover();
+            if (Scribe.mode != LoadSaveMode.Inactive) return SettlementLedgerState.Unavailable;
+            if (preserveRaw) return SettlementLedgerState.Unsupported;
+            return fault == null ? SettlementLedgerState.Ready : SettlementLedgerState.Faulted;
+        }

// Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs
+            bool inScope = IsPerceptionMapAvailable(map);
+            var ledgerState = inScope ? settlement.ReadLedgerState() : SettlementLedgerState.Unavailable;
+            var account = ledgerState == SettlementLedgerState.Ready ? settlement.ReadAccount(pawnId) : null;
+            var accountView = facts.BuildAccount(account, ledgerState, budget, plan, now, view.Selection, inScope);
             return new NeedDashboardView(..., employment: employment, care: care, account: accountView);
```

## 4. 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖本轮实际改动全部文件（二进制 `.dll` 与 gitignore 区另行说明，见 §1 与 §5）
- [x] 清单范围 ⊆ 需求单授权范围（只读展示 + 最小只读接口扩展），无超范围改动
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与回报、Gate Record、Changelog 互链一致（`CHG-20261011-NEEDS-ACCOUNT`）
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留（`.codebuddy/tmp/*` 为过程证据且 gitignore）
- [x] 已用 `gate-runner.ps1 -Base HEAD -Annotation <本件> -ExcludeFromFile <清单> -StrictSchema -Blocking` 自动校验（结果见 §5）

## 5. 终检结果（实测 stdout）

```text
① change-context -Set      : CONTEXT: OK —— changeId = CHG-20261011-NEEDS-ACCOUNT
                             excludeFromFile = .codebuddy/tmp/chg-20261011-needs-account-out-of-scope.txt
② change-context -GenScope : 标注声明 25 个 · git 改动 2340 个 · 范围外 2314 个
③ change-context -Validate : CONTEXT: OK（changeId=CHG-20261011-NEEDS-ACCOUNT · 标注与清单口径一致）
④ GATE-DELTA               : GATE-DELTA: PASS — 标注覆盖 25 个改动文件，无遗漏/超范围/残留（提示 0 条）
⑤ 页级门禁                 : PASS: RUIF read-only needs page; 285 bilingual keys; 40 new tests;
                             full 1467 passed, 1 skipped. Game/UI acceptance unverified.
⑥ 编译                     : 0 error / 0 warning（_scripts/compile_with_sdk.ps1，exit 0）
⑦ 全量回归                 : 1467 通过 / 0 失败 / 1 跳过（Tests/TestResults/NeedsAccount.trx）
```

**Gate 各席结论**（`.codebuddy/audit/gate-records/GR-CHG-20261011-NEEDS-ACCOUNT-*.md`）：
ARCH **PASS** · DATA **PASS** / SAVE **N/A（三重核证）** · **UI 初审 BLOCKED → 已按根因修复 → 复审 PASS** / LOC **PASS** ·
COMP **N/A（RUIF 适用项 PASS）** · PERF **PASS** / TEST **PASS** · REL **PASS** / DELTA **PASS**。

**初审 BLOCKED 的修复（GATE-UI H1/H2/H3，全部按根因而非逐条补丁）**：
① **H1（P2）** 账户主行测量↔绘制口径不一致（字体 Tiny→Small、宽度 `inner−SpaceXs`→`inner×.6`、折行测量 vs `Draw.Label` 强制 `WordWrap=false` 不裁剪）⇒ 窄屏文字溢出邻件。
修法：主行折行**改用绘制侧字体与列宽**，并把两列宽存入测量结构（`AccountTitleWidth` / `AccountHeadlineWidth`）由测量与绘制**共用同一字段**；`verify-pawn-needs-ui.ps1` 增加 IL 反回归断言「两个宽度字段至少各被引用一次」。
② **H2（P3）** 空态滚动总高按页签取值（tab1/2 只有 ≈EmptyHeight）而绘制恒为 `EmptyState + 账户区` ⇒ 账户区下半被裁。修法：空态总高按**实际绘制内容**显式计算。
③ **H3（P3）** 空态 `DrawAccount` 内宽非同源（差 24px）⇒ 改为与卡片路径同一 `width − CardPadX*2` 口径。
④ **H4（P3）** 按钮宽 `+32f` 镜像 `RUIF.ButtonFmt.FullWidthPad` ⇒ 改引常量。

> 复审证据：修复后重编译 **0 错 0 警**、全量回归 **1467/0/1**、页级门禁 **PASS**（含新增断言）。

命令（可复现）：

```powershell
pwsh .codebuddy/audit/change-context.ps1 -Set -ChangeId CHG-20261011-NEEDS-ACCOUNT -Annotation docs/设计文档/架构/变更标注/变更标注_CHG-20261011-NEEDS-ACCOUNT.md
pwsh .codebuddy/audit/change-context.ps1 -GenScope
pwsh .codebuddy/audit/change-context.ps1 -Validate
pwsh "$env:USERPROFILE\.codebuddy\skills\RimWorld工程架构规范\scripts\gate-runner.ps1" -Base HEAD -Annotation "docs/设计文档/架构/变更标注/变更标注_CHG-20261011-NEEDS-ACCOUNT.md" -ExcludeFromFile .codebuddy/tmp/chg-20261011-needs-account-out-of-scope.txt -Blocking -StrictSchema
pwsh -NoProfile -File _scripts/verify-pawn-needs-ui.ps1 -FullTestResults Tests/TestResults/NeedsAccount.trx -ChangeId CHG-20261011-NEEDS-ACCOUNT
```

**口径说明**：④ 证明「本轮标注 ⟷ git 实际改动集双向互含、无超范围、无调试残留」。`Assemblies/PersonalChronicle.dll` 属二进制产物，gate-runner 自动跳过（工程既有惯例），其哈希登记在 `CB-ECON-002_RESULT.json` 与 §1；`Tests/TestResults/**`（TRX）与 `.codebuddy/**` 在 `.gitignore` 内，不参与判定。

## 6. 首轮历史改动文件（25 项；当前累计清单见 front matter）

1. `Source/PersonalChronicle/Domain/PawnEconomy/Finance/SettlementContracts.cs`
2. `Source/PersonalChronicle/Domain/PawnEconomy/Budget/LifeBudget.cs`
3. `Source/PersonalChronicle/Economy/PawnEconomy/PawnSettlementSaveHost.cs`
4. `Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs`
5. `Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs`
6. `Source/PersonalChronicle/Economy/PawnEconomy/PawnEconomyFactsPresenter.cs`
7. `Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.cs`
8. `Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPanel.cs`
9. `Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs`
10. `Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Measure.cs`
11. `Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Demand.cs`
12. `Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml`
13. `Languages/English/Keyed/PawnEconomyNeeds.xml`
14. `Languages/ChineseSimplified/Keyed/PawnEconomyPlanning.xml`
15. `Languages/English/Keyed/PawnEconomyPlanning.xml`
16. `_scripts/verify-pawn-needs-ui.ps1`
17. `Tests/PawnLifeBudgetTests.cs`
18. `Tests/PawnEconomyFactsPresenterTests.cs`
19. `docs/开发参考/个人需求页-模块索引.md`
20. `docs/changelog/v1.4.x_2026-10-08_经济AI需求观察页.md`
21. `docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/B1.3_实装验证.json`（页级门禁脚本生成的机器证据）
22. `docs/任务派发/CodeBuddyCN/回报/CB-ECON-002_ACK.json`
23. `docs/任务派发/CodeBuddyCN/回报/CB-ECON-002_PROGRESS.json`
24. `docs/任务派发/CodeBuddyCN/回报/CB-ECON-002_RESULT.json`
25. `docs/任务派发/CodeBuddyCN/回报/CB-ECON-002_执行回报.md`

---

> 本变更由 AI 助手实施：CodeBuddy CN（模型 DeepSeek-V4.1-Flash），2026-10-11。执行器 = CodeBuddy CN，经理 = Codex；两阶段部署，本件不构成部署授权。


## 7. Codex R2 完成与本轮证据（2026-10-11）

用户明确交接「由你进行收尾工作」。本轮补齐需求单 §11，保留 CodeBuddy CN 已完成的 LR-01（单人门面心情/状态/截断字段转传）、LR-03（展示查询不恢复账本）及食品原因初稿；对首轮源码和本轮增量统一编译、回归、验证，不引用首轮通过数证明 R2。

### 7.1 实装差异与边界

- 高密度账户布局新增 `NeedsCyclePanel.Account.cs`：大号完整余额、总额与硬预留、计划值覆盖/缺口/严格启动线、四循环格、决策与目标、主要原因/下一步及紧凑详情。文本与矩形只在测量时格式化，绘制使用同一矩形；账户高度进入概览卡/滚动内容总高。580 宽四格，窄宽两格或逐格；长文和大额完整折行，不用省略号伪装正常。概览保留生活满足度及心情，账户余额不改变满足度配色。
- 四循环具稳定业务 ID、九态枚举、理由、来源、采样 tick 与陈旧性；颜色据枚举、当前重点据结构化 Focused。无活跃需要不是「已满足」，SeekWork 不是实际工作，候选空不能断言功能未实装。四格只跳转既有决策页，不声称精准定位。
- `NeedAccountModelView` 与 presenter partial 输出事实→缺口→选择→执行；账户与计划采样、计划余额分开陈述。覆盖率保留超过 100% 的文字真值，条仅截到 100%；未知/真实零目标不画伪覆盖率。执行链依据实际只读合同/应付/结算事实，没有实际 Job 关联或提交结果时明确未确认，不生成时间线或写入工资。
- 成本来源诊断经 Application 端口、Domain 原因出口与 Economy 适配传播；区分公有库存、营养、权属、可达、适配或其它实际拒绝分支，仍不捏造报价、不强行派工。不改变价格、预算水位或工资算法。
- 面板缓存增加 `TinyFontSupported` 失效；字体降级后重测，避免暂停设置改变后继续沿用旧行高。原决策循环列的点宽/文本字体/全三列测量口径已统一。
- 本轮无 Save Schema、Def、Harmony、资产、字体包、依赖或工坊发布改动；不改玩家存档及合同。当前 ACK/PROGRESS/RESULT/执行回报由总控维护，规范 input 任务单保持同一份。

### 7.2 证据冻结与部署边界

终态构建 2026-10-11 **14:57 UTC / 上海22:57**，`_scripts/compile_with_sdk.ps1` **0 diagnostics**（`.codebuddy/tmp/codex-account-r2/build.txt`）。工程与 Release 测试 DLL SHA-256 同为 `9AEA719E6E544A2F1FF2D6E10B12766B0BD850E33BBBCE2E882000A2639969AB`。

本次全量回归 **1523 通过 / 0 失败 / 1 跳过（总1524）**，TRX `.codebuddy/tmp/codex-account-r2/account-r2-final.trx` SHA-256 `6FF3B79D14684D0F038118211180140F85B579CAEE3152E252F2BF452C4A6375`。页级 stdout：`PASS: RUIF read-only needs page; 384 bilingual keys; 40 new tests; full 1523 passed, 1 skipped. Game/UI acceptance unverified.` 其中40为原名册用例累计数，不能称本轮新用例；中文/英文各 **Needs131 + Planning253 =384**，键/参数一致、无重复、无BOM。当前报告 SHA-256 `2A277EEAACE1D8BC7F2E5264307C6C90375BA6262B13B880F796BBE25A426C9B`，没有覆盖旧B1.3报告冒充相同构建。

严格 DELTA 结果：`GATE-DELTA: PASS — 标注覆盖 37 个改动文件，无遗漏/超范围/残留（提示 0 条）。` 唯一 actual2346、excluded2308、scope含annotation38、排除交集0；runner显示2474为重复查询口径，不等于唯一排除项。日志/脚本/scope `.codebuddy/tmp/codex-account-r2/{delta.txt,run-delta.ps1,delta-scope.json}`。本轮 Gate 记录为 `.codebuddy/audit/gate-records/GR-CHG-20261011-NEEDS-ACCOUNT-R2-UI-LOC.md` 与 `GR-CHG-20261011-NEEDS-ACCOUNT-R2-REL-DELTA.md`。静态样例仅证明可审查排布，不授予 GameVerified。

拟部署清单为 5 文件：`Assemblies/PersonalChronicle.dll`，中英 `PawnEconomyNeeds.xml`，中英 `PawnEconomyPlanning.xml`。此阶段未执行部署；保留当前已部署首轮文件。部署时须先把上述 5 文件原件备份并核对绝对目标根，再写入、逐项哈希复核；无 Save 改动，回滚恢复该备份即可。

### 7.3 当前累计文件清单（与 front matter 同源）

- `Source/PersonalChronicle/Domain/PawnEconomy/Finance/SettlementContracts.cs`
- `Source/PersonalChronicle/Domain/PawnEconomy/Budget/LifeBudget.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/PawnSettlementSaveHost.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/PawnEconomyFactsPresenter.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPanel.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Measure.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Demand.cs`
- `Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml`
- `Languages/English/Keyed/PawnEconomyNeeds.xml`
- `Languages/ChineseSimplified/Keyed/PawnEconomyPlanning.xml`
- `Languages/English/Keyed/PawnEconomyPlanning.xml`
- `_scripts/verify-pawn-needs-ui.ps1`
- `Tests/PawnLifeBudgetTests.cs`
- `Tests/PawnEconomyFactsPresenterTests.cs`
- `docs/开发参考/个人需求页-模块索引.md`
- `docs/changelog/v1.4.x_2026-10-08_经济AI需求观察页.md`
- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/B1.3_实装验证.json`
- `docs/任务派发/CodeBuddyCN/回报/CB-ECON-002_ACK.json`
- `docs/任务派发/CodeBuddyCN/回报/CB-ECON-002_PROGRESS.json`
- `docs/任务派发/CodeBuddyCN/回报/CB-ECON-002_RESULT.json`
- `docs/任务派发/CodeBuddyCN/回报/CB-ECON-002_执行回报.md`
- `Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Account.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Decision.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/NeedAccountModelView.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.Cycles.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.AccountModel.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyStorage.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyFoodCostSource.cs`
- `Source/PersonalChronicle/Application/PawnEconomy/Planning/PlanningSourcePorts.cs`
- `Source/PersonalChronicle/Domain/PawnEconomy/Planning/ShadowPlan.cs`
- `Tests/FoodQuoteDiagnosisTests.cs`
- `Tests/PawnNeedsAccountR2Tests.cs`
- `docs/README.md`
