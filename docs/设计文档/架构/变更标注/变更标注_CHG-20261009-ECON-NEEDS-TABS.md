---
change_id: CHG-20261009-ECON-NEEDS-TABS
state: ImplementedAndDeployedByThisBatch
scope: 「个人需求」页改为**一页式 + 页内三页签**（需求 / 决策 / 观测明细），页签栏由 RUIF NavAtomsB.LayerSwitch 承载；折叠区与折叠按钮退役
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPanel.cs
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml
  - path: Languages/English/Keyed/PawnEconomyNeeds.xml
  - path: _scripts/verify-pawn-needs-ui.ps1
  - path: Tests/TestResults/NeedsCycle-V7.trx
---

# 「个人需求」页一页式 + 页内多页签 变更定位

**触发**：PM 裁决 2026-10-09 —— 「我需要一页式，允许多tab」（承接 `CHG-20261009-ECON-NEEDS-SINGLE-COLUMN` 的单栏重排版；该批的纵向堆叠会让「仲裁与预算」与「观测明细」落到滚动区之下）。

**部署事实**：本批**已部署**（目标 DLL 时间戳 `2026-10-09 18:09:43`）。⚠ 发布目录同日存在并行会话写入活动，若并行会话后续再次部署，发布区将不再是本批构建。

**验证基线**：编译 **0 错 0 警**；全量测试 **1028 通过 / 0 失败 / 1 跳过**（`Tests/TestResults/NeedsCycle-V7.trx`）；`verify-pawn-needs-ui.ps1` **PASS**（89 双语键 / 38 用例）。

| 文件 | 定位 | 说明 |
| --- | --- | --- |
| Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs | 加页签栏与三页签分流 | 顶部常驻 `RUIF.NavAtomsB.LayerSwitch(rect, TabLabels, ref state.CycleTab)`；`DrawDemandTab`（概览卡 + 已规划区 + 仅观察区）/ `DrawDecisionTab`（影子仲裁与预算）/ `DrawObserveTab`（原始观测明细）；测量一次算全三页签的内容高，`Height` 按当前页签取值（切页签无需重测，仅取另一高度）；门控新增 `measure.Tab != state.CycleTab` |
| Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPanel.cs | `NeedPanelState` 状态位替换 | 删 `CycleDetailsExpanded`（折叠已由页签取代），加 `int CycleTab`；`ResetForNewSubject` 复位为首页签 |
| Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml | +2 键（共 89） | `Cycle.Tab.Demand` / `Cycle.Tab.Decision`（第三页签复用既有 `Cycle.Details` = 观测明细） |
| Languages/English/Keyed/PawnEconomyNeeds.xml | +2 键（共 89） | 同上 |
| _scripts/verify-pawn-needs-ui.ps1 | 组件断言更新 | 加入 `NavAtomsB::LayerSwitch`；移除已退役的 `StatusAtoms::Caret` 与 `ButtonAtoms::PrimaryButton`；RUIF 类型存在性表加 `RUIF.NavAtomsB` |
| Tests/TestResults/NeedsCycle-V7.trx | 本批全量测试结果 | — |

## 设计要点

1. **一页式**：页签栏常驻顶部，三页签内容各自一屏内可见（需求页 ≈ 概览 60 + 8 行 × 26 + 2 个分区栏 ≈ 340px，远小于 640 页高）⇒ **正常情况无需滚动**；滚动仅在极端需求数或明细很长时兜底。
2. **页签取代折叠**：原「观测明细」折叠按钮 + `StatusAtoms.Caret` 退役（页签本身就是切换），连带移除 `ButtonAtoms.PrimaryButton` 在该页的唯一用途。
3. **页签状态是会话态**：`state.CycleTab` 随换 Pawn 复位为首页签（0），不落盘、不写 Def。
4. **组件选型**：`NavAtomsB.LayerSwitch(rect, string[] layers, ref int sel)` 在项目内已有 3 处先例（`Dialog_PawnEconomy.cs` / `MasterControlTabWindow.Economy.cs`），属既有引用面；`RuifBridge` 中的 `NavAtoms.TabBar` 未被采用（`TabBar` 需额外维护 `indicatorX` 动画量，`LayerSwitch` 语义更贴合"同页分层切换"）。

## 未做与已知问题（如实登记）

1. **实机渲染未验**：已部署但未重启游戏确认。待确认：页签栏在三页签下的宽度分配与选中态；三个页签的高度差是否造成页面跳动（页签切换会改变内容高 → 滚动位置可能需复位）。
   **已知取舍**：切页签时未强制复位滚动位置（`state.Scroll` 保持），若在长页签滚动后切到短页签，滚动位置可能被钳制；未处理的显式复位待实机确认必要性。
2. **独立 Gate 未覆盖本批**：`pc-ui-loc-guard` / `pc-compat-auditor` 的结论对应 `CHG-20261009-ECON-NEEDS-SINGLE-COLUMN` 的状态；本批新增 `NavAtomsB.LayerSwitch` 引用与页签状态位，**尚未经独立 Gate 复核**（不得记为 PASS，GOV-012）。
3. 本批未处理：`CapacityBar` 的语义借用注释（沿用上批的待办）。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。需求裁决与实机验收由 PM 负责。
