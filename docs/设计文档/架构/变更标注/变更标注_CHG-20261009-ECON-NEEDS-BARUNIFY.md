---
change_id: CHG-20261009-ECON-NEEDS-BARUNIFY
state: ImplementedAndDeployedByThisBatch
scope: 需求页进度条规格统一化（行内条走唯一 helper；政策卡条起点/长度/粗细与需求行逐像素对齐）
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Demand.cs
---

# 进度条规格统一化 变更定位

**触发**：PM 实机截图 2026-10-09 ——「统一化进度条」。

**问题根因**：长期生活规划卡的条走 `RUIF.PanelAtoms.StatRow(rect, label, value, bar01, barColor)`，
该重载内部按 `PanelFmt.RowSplit` 排版 ⇒ 条被压在标签与值之间，**起点偏右、可用长度短、粗细由内部决定**；
而需求满足度条是自绘的 `CapacityBar`，起点为 `row.x + NameWidth + Gap`。两者同屏不同规格 ⇒ 看起来像两套 UI。

**验证基线**：编译 **0 错 0 警**；测试 **1030 通过 / 0 失败 / 1 跳过**（`NeedsCycle-V20.trx`）；verify **PASS**（110 双语键 / 40 用例）。

## 改动

| 文件 | 改动 |
| --- | --- |
| `NeedsCyclePanel.cs` | 新增 **`RatioBarRect(row, barWidth)`** —— 本页**行内比率条的唯一规格入口**：起点 `row.x + NameWidth + Gap`、高度 `BarHeight`、行内垂直居中；`DrawNeedRow` 改为调用它（原先就地 `new Rect(...)`） |
| `NeedsCyclePanel.Demand.cs` | `DrawPolicyCard` 弃用 `StatRow` 带条重载，改为按**与需求行相同的列结构**自绘：标签列 = `NameWidth`、条起点同上、条宽 = 同一 `BarWidth(…, withTarget:false, withGap:false)` 公式、值列右对齐到同一 `PercentWidth` 收尾位置 |

⇒ 政策卡前 3 列（标签 / 条 / 值）与需求行的（名称 / 条 / 满足度）**列位一致**，条不再被挤到中间。

**刻意不统一的一处**：概览卡的"生活保障"条是有意放大的**卡片级主指标条**（`BarHeight * 1.5f`），属视觉层次，已在 `RatioBarRect` 的注释里写明。

**颜色语义保持区分**：满足度用 `RatioColor`（好/中/差三色），政策参数占比用中性 `Tokens.Info` —— 几何统一、语义不混。

## 未做

1. **独立 Gate 未覆盖本批**（沿用 `CHG-20261009-ECON-NEEDS-CARDS` 自评结论；无新跨库成员面、无 Harmony、无静态状态；`route-task.ps1 -Skill RimWorld工程架构规范` 命中 7 agent、`audit-subagents.ps1 -Blocking` = PASS，但子 agent 派发此前多次被环境中断，未重试）。
2. **实机未验收**：已部署待重启；重点看政策条与需求条是否对齐成一条视觉轴线。
3. **记忆已写入**：`memory/feedback_ui_style_unification.md`（feedback 类，含 Why / How to apply）。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
