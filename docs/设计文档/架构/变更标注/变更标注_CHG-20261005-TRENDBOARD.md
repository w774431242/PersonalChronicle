---
change_id: CHG-20261005-TRENDBOARD
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle, Languages]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopDeviceLedger.cs
    type: M
    symbols: DeviceStatEntry.runWindowCounts, DeviceStatEntry.runWindowDays, DeviceStatEntry.RunTicksOnDay, DeviceStatEntry.AddRunTicksOnDay, DeviceStatEntry.SlotOf, DeviceStatEntry.EnsureWindow, WorkshopDeviceLedger.RunTicksOnDay, WorkshopDeviceLedger.AddRunTicksOnDay
    lines: 93, 104, 267-268, 357, 369, 389, 406
  - path: Source/PersonalChronicle/Automation/Zone_Workshop.cs
    type: M
    symbols: Zone_Workshop.TrendDayCount, Zone_Workshop.TrendHourCount, Zone_Workshop.AddHourlyRun, Zone_Workshop.HourlyRunAt, Zone_Workshop.EnsureHourly, Zone_Workshop.HasHourlyTrend, Zone_Workshop.ExposeData
    lines: 319-430, 665-668
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.cs
    type: M
    symbols: MapComponent_WorkshopManager.AccumulateDeviceRuntime
    lines: 307, 348-351
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Overview.cs
    type: M
    symbols: WorkshopManageView.PeakGridH, WorkshopManageView.PeakSectionH, WorkshopManageView.EffSectionH, WorkshopManageView.OvBigBlockH, WorkshopManageView.BuildBigBlockReadings, WorkshopManageView.DrawBigBlocks, WorkshopManageView.DrawPeakSection, WorkshopManageView.DrawBottleTrendSection, WorkshopManageView.DrawEfficiencySection, WorkshopManageView.OvTrendDays
    lines: 45-59, 250, 266, 569, 803, 818, 890, 936
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: PeakTitle, PeakNoData, PeakLegendLess, PeakLegendMore, PeakBest, PeakBestHours, PeakFooter, EffTitle, EffNoData, EffLegendProduced, EffLegendRun, EffFooter, BottleTrendTitle, BottleTrendNoData, BottleTrendOther
    lines: 501-522
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: (same 15 keys as ChineseSimplified)
    lines: 492-513
---

# 变更标注 · CHG-20261005-TRENDBOARD

## 背景

概览页「大组件区」选型：玩家 2026-10-05 **批准候选 1 + 3 + 4**（txt 可视化对照后决定），
即「设备使用波峰」「稼动 vs 产出」「瓶颈演变」。候选 2（状态时间线）/ 5（俯视格网）未采纳。

选型依据（`.codebuddy\tmp\overview-bigblocks-preview.txt`）：候选 1 回答"什么时候最忙"（可反哺排班）、
候选 3 回答"忙了有没有产出"（补盲区）、候选 4 与 3 **共用同一份逐日机时数据**（同一批数据两个视图）。

## 交付内容（三件图表）

| 组件 | 数据源 | RUIF 组件 | 空态 |
|---|---|---|---|
| 设备使用波峰（7 日 × 24 小时） | `Zone_Workshop.HourlyRunAt`（新增 7×24 采样） | `Charts.HeatmapGrid` | 全空 ⇒ "暂无运行采样"，不画全暗网格 |
| 瓶颈演变（7 日 · 各设备机时） | `DeviceLedger.RunTicksOnDay`（新增逐日机时）Top5 + "其它" | `Charts.StackedBarChartV` | 同上 |
| 稼动 vs 产出（7 日 · 归一化） | 逐日机时 + 逐日产出（既有台账窗） | `Charts.LineChart`（多系列重载） | 两者皆空 ⇒ 说明文案 |

### 组件契约取证（skill 步 1「不可跳过」，逐行读 RUIF 源码）

| 组件 | 契约（源码位置） | 本页必须补的部分 |
|---|---|---|
| `Charts.HeatmapGrid(r, float[行,列])` | 按**网格内最大值**归一 → `ChartsFmt.HeatColor`（Card→Alive）画 `cw-1 × ch-1`；不画轴/刻度/标签（`Charts.cs:458-474`） | 行标签（天）、列标签（每 6 小时）、色阶图例（**逐格调用同一个 HeatColor**，不另写插值） |
| `Charts.StackedBarChartV(r, float[系列][列])` | 共享 Nice 网格 + 逐列自底堆叠 `Tokens.Series(j)`；不画类目标签/图例（`Charts.cs:227-262`） | 图例（**同一 `Tokens.Series(j)` 同一序**）；系列数已在数据层截到 6（`Tokens.Series` 仅 6 色循环） |
| `Charts.LineChart(r, float[系列][列], Color[], dots, grid)` | **共享 y 上界**（hi = 各系列最大值，`Charts.cs:286-296`）；折线烘焙成贴图、滚动视图内安全（`Charts.cs:313-334`） | 两量纲**必须先归一化**（见下） |

### 两个关键设计决定（附理由）

1. **候选 3 归一化（各自峰值 = 100）**：`LineChart` 多系列共享 y 上界，件数与机时量纲不同时
   小量纲那条会贴底不可读。归一到"各自峰值 = 100"后比较的是**形状相关性**
   （"跑得多的日子是否也产得多"），绝对读数由仪表台与标题行给出（UI-009：图给形状、数给准确值）。
2. **不新增第二份车间级逐日存储**：候选 3/4 的逐日机时由**台账逐台读数聚合**而来
   （`DeviceLedger.RunTicksOnDay`），避免同一事实两处落盘、两处漂移（DATA 纪律）。

## 存档影响：**无迁移**（加性可选字段）

- `DeviceStatEntry`：新增 `runWindowCounts` / `runWindowDays`（与既有产出窗**同一不变式**：
  写入按 `day % 7` 定位、读取按槽内自带绝对天过滤 ⇒ 跨日自然归零、**扩容安全**）。
  旧档缺省为空列表 ⇒ `EnsureWindow()` 补齐 ⇒ **不需要 bump SchemaVersion、不需要迁移**（SAVE-004）。
- `Zone_Workshop`：新增 `pcHourlyRunTicks`（168）/ `pcHourlyRowDays`（7），
  旧档缺省为空 ⇒ `PostLoadInit` 调 `EnsureHourly()` 补齐 ⇒ 同上。
- ⚠ 未改任何**既有** Scribe 键结构（审计"不建议动 Zone 持久字段迁移"的红线未触碰）。

## 性能（PERF 纪律）

- 全部读数在 **Refresh 期**算好（`BuildBigBlockReadings`），绘制路径**只读、零分配**；
- 采样挂在既有 60 tick 路径（`AccumulateDeviceRuntime`），**不新增 tick 钩子**；
- 存储**有界**：168 + 7 个 int（Zone，恒定）+ 每设备 14 个 int（台账，随设备数有界）；
- `HasHourlyTrend` 是 Refresh 期一次 168 项扫描（非绘制期）。

## 实机验证要点

1. **首次进入会看到空态文案**（正常）：机时采样从新版 DLL 启动后才开始累计；
   产出序列（台账 7 日窗）立即有值 ⇒ 候选 3 可能"一条有线、一条只有今天"。
2. 运行 **≥1 游戏小时**后：波峰图应出现第一列色块（当前小时的列）；跨 2-3 天后形状才丰满。
3. 柱状/热力/折线的**颜色应与图例逐项对应**（图例取色与组件内部同函数）。
4. 概览页滚到底应能看到三件图表，**无底部空白、无内容被裁**（`OvBigBlockH` 与绘制顺序同步）。

## 回滚

三件图表互相独立、且都不影响存档语义：若要回退，把 `OvBigBlockH` 改回 `0f`
并移除 `DrawOverviewBody` 内的 `DrawBigBlocks(...)` 一行即可（数据字段留档无害，PRUNE 不涉及）。

## 残留风险（照实登记）

1. **7 日窗与产出窗严格对齐**：`OvTrendDays` 由 5 改为 `Zone_Workshop.TrendDayCount`（7）——
   与台账 `ProducedWindowDays`（7）同窗；若将来缩短该常量，须同时处理环形槽位（见 `EnsureWindow` 注释的缩容警告）。
2. **热力色阶为单色阶**（Card→Alive）：这是 `ChartsFmt.HeatColor` 的既定语义（框架白名单③），
   本页未改；若将来要"冷-热"双色阶，需改框架而非本页。
3. **峰值时段 = 列合计**（7 天里同一小时的合计）：单日异常（停工日）会被平滑；
   这是刻意取舍（回答"通常几点最忙"），已在方法注释写明。
