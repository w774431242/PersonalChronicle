---
change_id: CHG-20261009-ECON-NEEDS-POLICY-CARD
state: ImplementedAndDeployedByThisBatch
scope: 需求页「长期生活规划」区：从 2 行长文本改为结构化政策卡（逐项一行 + 可归一者带条）
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Measure.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Demand.cs
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyPlanning.xml
  - path: Languages/English/Keyed/PawnEconomyPlanning.xml
  - path: Tests/PawnNeedDashboardTests.cs
  - path: Tests/PawnShadowPlanningTests.cs
  - path: _scripts/verify-pawn-needs-ui.ps1
---

# 长期生活规划政策卡 变更定位

**触发**：PM 实机截图 2026-10-09 ——「长期生活规划」区 2 行撑不满、值文本被折成难看的两行。

**根因**：`LongTerm` 契约只有 `Label + Value` 两段文本，四项政策（工时/闲暇/储备/储蓄倾向）被塞成**一行**，
在 `ValueShare=.45` 的值列里必然折行（断点落在"储备 2 / 日；结余储蓄倾向 20%"）。

**验证基线**：编译 **0 错 0 警**；测试 **1030 通过 / 0 失败 / 1 跳过**（`NeedsCycle-V19.trx`）；verify **PASS**（110 双语键 / 40 用例）。

## 改动

| 层 | 改动 |
| --- | --- |
| 契约 | `LongTerm: IReadOnlyList<NeedTextRow>` → **`Policy: NeedPolicyView`**（新结构化类型：WorkHours/Leisure/Reserve/Saving/Sources + 三个归一比例）。**替换而非并存**，不留死分区 |
| presenter | `BuildLongTerm` → **`BuildPolicy`**；工时/闲暇按 `HoursPerDay = 24f` 归一（客观时间常量，非策略阈值）；储蓄倾向用 `BasisPoints` 归一；**保障期无自然上界 ⇒ 不给比例**（不画条）；就业不适用时工时如实写"不适用" |
| 表现层 | `DrawPolicyCard`：卡片底 + 角框，逐项 `StatRow`；可归一者传 `bar01`（`Tokens.Info` 着色），其余不画条 |
| 语言键 | 删 `Planning.Policy` / `Planning.PolicyValues`（成死键）；加 `Policy.WorkHours/Leisure/Reserve/Saving` + `HoursPerDay/Days/Percent`（中英对称，净 +5） |
| 测试迁移 | 两处 `BuildLongTerm` 断言改为 `BuildPolicy`：新增**归一化数值断言**（`WorkHours01 == 8/24`、`Saving01 == .2`）—— 正是它抓出了下面的 bug |
| verify | `LongTerm`→`Policy`、`BuildLongTerm`→`BuildPolicy`、字段名 `longTermPartitionSplit`→`policyCardStructured` |

## 过程中被抓出的真实 bug（未流入部署前）

首版 `Normalized(x)` 只做 `Clamp01`，**调用处漏了除以 `HoursPerDay`** ⇒ 工时 8 小时被算成 1.0（进度条满格）。
由本批新增的**数值断言**（`Is.EqualTo(8f/24f).Within(.001f)`）在测试期拦下，修正后 1030 全绿。

## 未做（如实登记）

1. **实机未验收**：已部署待重启；重点看政策卡 5 行的条长与文本配合、窄窗口下是否折行。
2. **独立 Gate 未覆盖本批**（沿用 `CHG-20261009-ECON-NEEDS-CARDS` 的自评结论：无新跨库成员面、无 Harmony、无静态状态、依赖方向未变）。
3. **政策参数只读**：本批仅呈现，未做编辑（PM 诉求是"UI 优化"）。
4. `HoursPerDay = 24f` 是**客观时间常量**，非策略阈值；若日后引入自定义日长需同步（已加注释标注）。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
