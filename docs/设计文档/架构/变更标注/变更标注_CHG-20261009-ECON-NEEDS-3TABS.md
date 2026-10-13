---
change_id: CHG-20261009-ECON-NEEDS-3TABS
state: ImplementedAndDeployedByThisBatch
scope: 「个人需求」页三页签重构为**需求 / 目标 / 决策**（与需求循环的观察/目标/仲裁三环节一一对应）+ 按推荐扩展（Mood 进概览卡、D03/D09 状态条件卡）
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml
  - path: Languages/English/Keyed/PawnEconomyNeeds.xml
  - path: Tests/PawnNeedDashboardTests.cs
  - path: Tests/TestResults/NeedsCycle-V9.trx
---

# 「个人需求」页三页签重构 + 循环环节对齐 变更定位

**触发**：PM 裁决 2026-10-09 —— 「需求 tab / 目标 tab / 决策 tab 三类进行，按你推荐进行扩展、并重构前端 UI 布局」。上一版（`CHG-20261009-ECON-NEEDS-TABS`）的「需求 / 决策 / 观测明细」被本版的三分法取代。

**部署事实**：本批**已部署**。⚠ 发布目录同期存在并行会话写入活动，若并行会话后续再次部署，发布区将不再是本批构建。

**验证基线**：编译 **0 错 0 警**；全量测试 **1028 通过 / 0 失败 / 1 跳过**（`Tests/TestResults/NeedsCycle-V9.trx`）；`verify-pawn-needs-ui.ps1` **PASS**（94 双语键 / 38 用例）。

## 三个页签与需求循环的对应

| 页签 | 循环环节 | 内容 |
| --- | --- | --- |
| **需求** | ① 观察 | 生活保障概览卡（全部需求满足度均值 + **心情** + 规划计数）· 全部需求满足度行 · **状态条件卡** |
| **目标** | ② 目标 | 已纳入规划的「目标值 + 缺口」行 · 「仅观察（未纳入规划）」域名单 |
| **决策** | ③ 仲裁 | 影子仲裁/预算/政策/机会 · 原始观测明细 |

## 按推荐实施的两项扩展

1. **Mood 进概览卡**：心情是**只读结果量**（原设计明确"不生成购买心情目标"），现作为概览卡的第二个读数。数据来自 `NeedDashboardView.Mood`（新增结构化字段，取自 `NeedFactSet.MoodRatio`，此前只在明细里有一行文本）。
2. **D03/D09 状态条件卡**：衣着与温度、健康与生育准备**没有需求条**（设计红线：`01-状态与需求感知` §5「不制造'健康需求条'」），故以**文字条件行**呈现（衣着覆盖/温区/健康待治疗）。新增 `NeedDashboardView.Status` 结构化分区，`NeedDashboardPresenter` 在构建时把 `Health/Apparel/Temperature/ComfortRange` 四行从 `Facts` **迁入** `Status`。

| 文件 | 定位 | 说明 |
| --- | --- | --- |
| Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs | 三页签重写 | 页签索引改 `TabDemand/TabGoal/TabDecision`；`DrawDemandTab`（概览含 Mood + 满足度行 + 状态卡）/`DrawGoalTab`（目标与缺口行 + 未纳入规划名单）/`DrawDecisionTab`（影子行 + 明细）；**两套列宽**（需求页签 3 列 / 目标页签 4 列），列头与数据行共用 `BarWidth()` 同一公式；`NarrowWidth` 提到 400（4 列所需） |
| Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs | View 加两个结构化分区 | `NeedDashboardView` 新增 `Mood`（`Fact<float>`）与 `Status`（`IReadOnlyList<NeedTextRow>`），ctor 加两个可选参数（additive）；`Build` 内把 Health/Apparel/Temperature/ComfortRange 移入 `status`，并记录 `mood` |
| Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml | +5 键（共 94） | `Cycle.Tab.Goal` / `Cycle.Section.Satisfaction` / `Cycle.Section.Goal` / `Cycle.Section.Status` / `Cycle.Overview.Mood` |
| Languages/English/Keyed/PawnEconomyNeeds.xml | +5 键（共 94） | 同上 |
| Tests/PawnNeedDashboardTests.cs | 1 处断言迁移 | `SlowBodyConditionKeepsItsOwnAgeAndUnknownCollectionsDoNotClaimZero` 的"Visible health records"断言从 `Facts` 改为 `Status`（该行按本批设计迁区） |
| Tests/TestResults/NeedsCycle-V9.trx | 本批全量测试结果 | — |

## 需求域覆盖度（与设计文档对照）

设计侧共 **12 个生活观测域**（`原版需求缺口驱动经济AI_设计方案_v0.3.md:41-54`），落在 **10 种连续 Need**（`游戏代码设计/01-状态与需求感知_代码设计方案.md:151`）上。本页当前覆盖：

| 域 | 本页呈现 | 状态 |
| --- | --- | --- |
| D01 营养 / D02 睡眠 / D05 娱乐 | 需求页签有满足度；目标页签有目标与缺口 | **完整**（`LifePolicy.Targets` 覆盖） |
| D04 舒适 / D06 美观 / D07 居住空间 / D08 室内外 / D12 儿童学习 | 需求页签有满足度；目标页签列在"仅观察" | **观测可读，未纳入规划**（目标面尚未扩展） |
| D03 衣着与温度 / D09 健康与生育准备 | 需求页签「状态条件」卡 | **按设计红线以条件行呈现** |
| D10 伴侣关系与婚礼 / D11 婴幼儿照护 | **不在本页** | 设计明确"单身不生成婚姻不足"（`v0.3:58`）且照护需跨 Pawn ⇒ 应落家庭页 |

## 未做与已知问题（如实登记）

1. **实机渲染未验**：已部署未重启确认。待确认：三页签宽度与选中态、需求页签（约 8 行 + 状态卡 4 行 + 概览）是否一屏放得下、目标页签四列在 580 宽下的可读性。
2. **独立 Gate 未覆盖本批**：`pc-ui-loc-guard` / `pc-compat-auditor` 的结论对应 `SINGLE-COLUMN` 批次状态；本批新增 `Status`/`Mood` 分区与三页签列宽，**尚未经独立复核**（不得记为 PASS，GOV-012）。
3. **目标面扩展未做**（属功能实装，非 UI）：把 D04/D06/D07/D08 纳入 `LifePolicy.Targets` 需要各自的"缺口如何形成"规则，应走 B5/B6 批次。
4. **死键 1 个**：`PC_PEA.Needs.Cycle.Title`（上一版的"需求循环"分区标题）在本次分区改名后已无消费者，待下轮清理。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。需求裁决与实机验收由 PM 负责。
