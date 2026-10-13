---
change_id: CHG-20261009-ECON-NEEDS-AUDIT-FIX
state: ImplementedAndDeployedByThisBatch
scope: 按专项审计结论修复需求页签的三批问题（A 布局 / B 语义 / C 数量口径）
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Perception/NeedObservation.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Perception/VanillaNeedRegistry.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml
  - path: Languages/English/Keyed/PawnEconomyNeeds.xml
  - path: Tests/TestResults/NeedsCycle-V12.trx
---

# 需求页签专项审计修复 变更定位

**触发**：`AUDIT-20261009-ECON-NEEDS-DEMAND-TAB.md`（`pc-data-steward` + `pc-ui-loc-guard` 双专项审计）结论——1×P1 + 4×P2 + 若干 P3；PM 批准按建议修复。

**部署事实**：本批**已部署**。⚠ 发布目录同期存在并行会话写入活动。

**验证基线**：编译 **0 错 0 警**；全量测试 **1028 通过 / 0 失败 / 1 跳过**（`Tests/TestResults/NeedsCycle-V12.trx`）；`verify-pawn-needs-ui.ps1` **PASS**（96 双语键 / 38 用例）。

## A 批 · 布局

| # | 修复 | 位置 | 效果 |
| --- | --- | --- | --- |
| A1 | **行高双重补偿**：`Wrapped()` 返回值已含 `+Gap`，原实现又加 `InnerGap` ⇒ 行高虚高 8px | `NeedsCyclePanel` 行高计算 | 行高 34→26；**一屏容量从 ~7 条提到 ~11 条** |
| A2 | **页签栏被卷走**：原画在滚动内容内（注释却称"常驻顶部"） | `NeedsCyclePanel.Draw` | 页签改画在 `BeginScroll` **之外**，内容滚动时恒常驻 |

## B 批 · 语义

| # | 修复 | 位置 | 效果 |
| --- | --- | --- | --- |
| B1 | **`FactState` 语义保真**（P1）：`Ratio01 < 0` 原统一显示"未配置"，现按事实状态显示"未适配 / 读取错误 / 不适用 / 未知" | `NeedTableLine.RatioState` + `PercentText()` | 满足设计 §5「保留 Unsupported 清单与显式部分覆盖提示」 |
| B2 | **Mood 无组件时**显示"不适用"而非"未配置" | `m.MoodText` | 语义正确 |
| B3 | **`Planned` 判据收窄**：`!= Unknown` → `== Enabled` | presenter | `Disabled`/`NotApplicable` 不再被归入"已纳入规划"（原为契约级潜伏缺陷，当前不可达） |
| B4 | **文本口径与分区归属解耦**（修复过程中由测试暴露）：`Disabled` 的分区归属改判后，其 `Target/Gap` 文本**不得**随之降级为"未配置"，仍应为 `N/A` | presenter `hasTarget` | 两件事分开表述，避免语义倒退 |

## C 批 · 数量口径

| # | 修复 | 位置 | 效果 |
| --- | --- | --- | --- |
| C1 | **原版隐藏项区分**：`ShowOnNeedList=false` 的项（RoomSize / Suppression / 未激活 Chemical / Disabled Indoors-Outdoors）原混在主列表里 ⇒ 现单独成区「其它需要（原版不显示）」 | `NumericNeedFact.ShowOnNeedList`（Domain additive）+ `VanillaNeedRegistry.ReadNumeric` 填值 + 面板分区 | 清单变干净，符合"保留 Unsupported 清单"的设计要求 |
| C2 | **截断告知**：采集上限截断数原只在决策页签暴露 ⇒ 现追加到概览摘要（"· 隐藏 N 项"） | `NeedDashboardView.HiddenCount` + 面板 | "清单看起来完整"的误导消除 |

| 文件 | 定位 | 说明 |
| --- | --- | --- |
| Source/PersonalChronicle/Domain/PawnEconomy/Perception/NeedObservation.cs | `NumericNeedFact` 加 `ShowOnNeedList`（可选参数，默认 true） | additive，不破坏既有构造点 |
| Source/PersonalChronicle/Economy/PawnEconomy/Perception/VanillaNeedRegistry.cs | `ReadNumeric` 填 `need.ShowOnNeedList` | 用 `Need.ShowOnNeedList`（virtual）而非 `def.showOnNeedList`，尊重 mod 覆写 |
| Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs | `NeedTableLine` 加 `RatioState`/`Hidden`；`NeedDashboardView` 加 `HiddenCount`；`Planned`/`hasTarget` 口径拆分 | 全部 additive 可选参数 |
| Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs | 三批修复的 UI 侧（页签固定 / 行高 / 状态语义 / 隐藏项分区 / 截断提示） | — |
| Languages/{ChineseSimplified,English}/Keyed/PawnEconomyNeeds.xml | +2 键（共 96） | `Cycle.Section.OtherNeeds` / `Cycle.Hidden` |
| Tests/TestResults/NeedsCycle-V12.trx | 本批全量测试结果 | — |

## 过程记录（测试的牙齿）

B3 改动后 `DisabledTargetIsNotApplicableRatherThanUnconfiguredOrZeroGap` **立即失败**（期望 `N/A`，实际 `Unconfigured`）—— 该用例原本就在守护"`Disabled` 的文本语义"。这暴露了我的改动过粗（把分区归属与文本口径用同一个布尔量控制），遂拆为 `planned`（分区）与 `hasTarget`（文本）两件事。**测试在此起到实质拦截作用**，非空断言。

## 未做（如实登记）

1. **实机未验**：已部署未重启确认。待确认：一屏容量提升是否符合预期、页签固定后的视觉分层、隐藏项分区的观感。
2. **P3 未清**：审计列出的 UI 侧 P3（切页签不复位滚动、`SectionBar` count 语义错位、`CapacityBar` 语义借用注释、就地镜像 Tokens 常量、角框复述条色、`PercentWidth` 无截断兜底、`SectionBar` 的 `▾` 假折叠提示、Empty 死滚动、Error 提示误导）+ 死键 `Cycle.Title` 仍未处理。
3. **独立 Gate 未覆盖本批**（审计结论对应的是修复前状态）。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。审计结论由专项 agent 独立出具，修复与验证由主 Agent 执行。
