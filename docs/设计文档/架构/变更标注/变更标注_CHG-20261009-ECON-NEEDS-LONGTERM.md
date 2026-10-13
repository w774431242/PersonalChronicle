---
change_id: CHG-20261009-ECON-NEEDS-LONGTERM
state: ImplementedAndDeployedByThisBatch
scope: 「个人需求」页 需求页签：短期满足度与长期目标/生活政策同屏显示
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml
  - path: Languages/English/Keyed/PawnEconomyNeeds.xml
  - path: Tests/PawnNeedDashboardTests.cs
  - path: Tests/PawnShadowPlanningTests.cs
  - path: _scripts/verify-pawn-needs-ui.ps1
  - path: docs/开发参考/个人需求页-模块索引.md
---

# 长短期需求同屏 变更定位

**触发**：PM 指令 2026-10-09 ——「长期需求与短期需求都要显示」（实机截图显示「需求」页签只有当前满足度）。

**PM 裁决（三项，不可再议）**：
1. 长期 = **长期目标 + 生活政策**（饮食/休息/娱乐目标值、可用工时、保留闲暇、保障期、储蓄率、政策来源）；不含未来缺口预测（该数据不存在）
2. 呈现 = **行内加「长期目标」列 + 底部独立「长期生活规划」区**
3. 去重 = **政策参数归需求页、预算归决策页**（需求页 = 长期生活标准；决策页 = 钱的安排与仲裁），同一份数据不得两处渲染

**部署事实**：本批**已部署**。

**验证基线**：编译 **0 错 0 警**；全量测试 **1030 通过 / 0 失败 / 1 跳过**（`Tests/TestResults/NeedsCycle-V15.trx`）；`verify-pawn-needs-ui.ps1` **PASS**（**98** 双语键 / 40 用例）。

## 改动定位

| 文件 | 改动 |
| --- | --- |
| `ShadowPlanPresenter.cs` | **新增 `BuildLongTerm(plan)`**：产出政策参数行 + 政策来源行；**`Build` 移除 Policy/Sources 两行**（决策页零重复）；`plan == null` 或政策缺失时返回空集合 |
| `NeedDashboardPresenter.cs` | `NeedDashboardView` 新增 `IReadOnlyList<NeedTextRow> LongTerm`（第 12 个可选参数，不填 = 空集合，非 null） |
| `EconomyNeedDashboard.cs` | `QueryNeedDashboardSingle` 用同一 `ShadowPlanPresenter` 实例分别喂 `planning`（决策页）与 `longTerm`（需求页）；名册路径保持不填 |
| `NeedsCyclePanel.cs` | ① 需求满足度区与「其它需要」区**行内新增「长期目标」列**（直接复用 `NeedTableLine.Target`，表现层不重算政策）；② 页尾新增**「长期生活规划」区**（`PanelAtoms.StatRow`）；③「其它需要」区补列头（与上方对齐）；④ **列位解耦**：`DrawHeaderRow`/`DrawNeedRow`/`BarWidth` 的 `withTarget` 与 `withGap` 独立，新增 `TargetRight()` helper，列头与数据行共用同一公式 |
| 语言文件 | `Column.LongTarget`（长期目标 / Long-term）、`Cycle.Section.LongTerm`（长期生活规划 / Long-term life plan）；键数 **96 → 98** |
| `Tests/PawnNeedDashboardTests.cs` | `Words` 改为同时加载 `PawnEconomyNeeds.xml` 与 `PawnEconomyPlanning.xml`（政策键在后者）；新增 2 用例：政策行拆分且决策页零重复、无影子计划时长期区为空集合 |
| `Tests/PawnShadowPlanningTests.cs` | `:234` **迁移**：原断言 `Build(...)` 含 `Planning.NotApplicable`（依赖政策行住在 Build 里）改为断言 `BuildLongTerm(...)` —— 属裁决的预期行为变更，非回归 |
| `_scripts/verify-pawn-needs-ui.ps1` | 新增 8 条断言：需求页签渲染长期列与长期区（字段级）、`BuildLongTerm` 唯一入口、`NeedDashboardView.LongTerm` 存在、单人门面同时喂两个分区；`vanillaNeedColumnReused` 字段**改名为 `vanillaUiDependencyRemoved`**（旧名与"已移除原版列"的事实相反，属遗留误导字段） |

## 未做（如实登记）

1. **未来缺口预测未实现**：`CurrentNeedProjector` 只算**当前**缺口（Actual/Desired/Relative/Absolute/Raw），不存在"照此消耗 N 小时后低于目标"的前瞻投影 ⇒ 本页不含任何前瞻数字。已在 verify 报告的 `limits` 中显式登记。
2. **实机未验收**：已部署未重启确认。重点看：长期目标列是否与满足度条/百分数重叠、底部长期区在窄窗口下的折行、"其它需要"新增列头后的行高。
3. **独立 Gate 未覆盖本批**（沿用 `CHG-20261009-ECON-NEEDS-3TABS` 评审结论：无新跨库成员面、无 Harmony、无静态可变状态、依赖方向未变）。
4. **「目标」页签的 Target 列与需求页签新列同源同数据**（均来自 `LifePolicy.Targets`）——按裁决未动目标页签（它是"目标 + 缺口"专页，含缺口列）；若日后判定冗余，需另开裁决。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。裁决与放行由 PM 负责。
