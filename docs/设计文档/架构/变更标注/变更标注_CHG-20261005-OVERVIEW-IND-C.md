---
change_id: CHG-20261005-OVERVIEW-IND-C
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle/Automation, Languages]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Overview.cs
    type: M
    symbols: WorkshopManageView.DrawTrendSection, WorkshopManageView.DrawOverviewIndustrial, WorkshopManageView.DrawOverviewBody, WorkshopManageView.OverviewContentHeight, WorkshopManageView.BuildOverviewReadings
    lines: 45-60, 150-230, 250-330, 390-440
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: PersonalChronicle.Automation.Tab.TrendTitle, TrendTotal, TrendNoData, TrendToday, TrendDaysAgo, GaugeDeviceManned
    lines: 486-500
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: PersonalChronicle.Automation.Tab.TrendTitle, TrendTotal, TrendNoData, TrendToday, TrendDaysAgo, GaugeDeviceManned
    lines: 481-495
---

# 变更标注 · CHG-20261005-OVERVIEW-IND-C

## Change Summary

概览页工业化改造 **批次 C**（玩家 2026-10-05："截图，继续"）。

## ① 新增：近 5 日产出趋势（`RUIF.Charts.BarChartV`）

- 数据源：台账 `ProducedOnDayOf(thing, day)` —— **逐日槽位直查、跨日自然归零**，**零新增数据**；
- 组件契约（源码 `Charts.cs:177-201`）：它自画**水平网格线 + 柱体 + 底部类目标签**，
  **不画数值** ⇒ 合计值由本页画在标题行右侧；
- 全为 0（新车间/未产出）⇒ 显示"暂无产出记录"，**不画空网格**（LAND-009）；
- 入参缓冲（`float[]` + `string[]`）在 Refresh 期填充并在首次分配后复用 ⇒ 绘制路径零分配。

## ② 修正：「设备有人率」→「工作台有人率」

**问题**（玩家截图核对时发现）：设备运转率 **100%**、今日产出 **31 件**，而"设备有人率"显示 **0%** ——
两者并列会误导玩家。

**根因**：分母含**自研自动化设备**（自动农场 / 传送带等，本身无人值守）⇒ 读数恒被拉向 0%。

**修法**：只统计需要人操作的**原版工作台**（`thing is IBillGiver`）；标签同步改为「工作台有人率」
（中/英）。自研设备不计入分母，也不再拉低读数。

## ③ 新增：概览页滚动

四张仪表 + 趋势 + 灯阵 + 负载 + 匹配的累计高度已超过窗口可用高（实测约 380px）⇒ 不加滚动必有内容被裁。
现改为 `RUIF.Layout.BeginScroll/EndScroll`（**`EndScroll` 放 `finally`** —— skill 铁律 3：出栈不入 finally，异常会让整局 GUI 卡死）。
内容总高由 `OverviewContentHeight()` 集中给出，并在注释里写明**必须与绘制顺序同步**。

另：本批还核实了 **`Gauge` 指针算法无问题**（源码 `GaugeNeedleDeg`：0%→180° 左端、100%→360° 右端，上半环；
指针与弧线共用同一角度约定）。玩家截图中"11% 的指针看着偏右"是低分辨率误判，未做改动。

## 兼容性

- **存档影响：无**（纯 UI + 只读台账）。
- **行为影响：无**（全部为 `*Of(...)` 读方法）。
- 性能：所有读数与缓冲在 Refresh 期完成；绘制路径只读。

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警 |
| XML | 已验证 | 中英 `Automation.xml` 解析通过 |
| 部署 | 已验证 | Languages 39 / Assemblies 1 |
| 组件契约 | 已验证 | `BarChartV` 自画内容已读源码（含"不画数值"这条） |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-OVERVIEW-IND-C.md -ExcludeFromFile .codebuddy/tmp/ovc-out-of-scope.txt -Blocking` |
| 实机 / 精美度 | **未验证** | 见下 |

## 实机验证要点

1. **趋势块**（概览最上方）：标题「近 5 日产出趋势」+ 右侧「合计 N 件」+ 5 根柱（柱下标签：4 天前 … 今天）。
2. **工作台有人率**：读数应与"有几台原版工作台正被操作"一致；自动化设备不再拉低它。
3. **滚动**：概览内容可上下滚动到底（匹配段应完整可见，不再被裁）。
4. **回归**：今日产出仪表与设备页「今日」列仍应一致。

## 精美度自查（对玩家截图 · skill `09` 册）

| 维度 | 自查结论 |
|---|---|
| 层次 ≥3 | 窗口底 → Panel → Card → 表盘/条/灯 ⇒ **达标** |
| 反馈 | 本页为纯展示面（无交互件）；刷新/关闭沿用原版按钮 ⇒ **无缺口** |
| 对齐 | 四仪表等宽、灯阵左对齐、合计右对齐 ⇒ 达标（实机再复核） |
| 呼吸 | 统一 `Tokens.SpaceXs` / `Gap`；不贴边 ⇒ 达标 |
| 准确 | 表盘分区色 / 灯色 / 瓶颈红均与组件契约一致；数值与单位分离 ⇒ 达标 |
| 反劣质 12 条 | 无空态缺口、无假数据（全 0 时显"暂无…"）、无硬编码文本、无静默裁切 |

⚠ **仍待实机确认**：① 四仪表并排时表盘直径是否够看（窗口窄时）；② 趋势柱在 68px 段高内的可读性；
③ 滚动条与内容右缘是否重叠。

## Remaining

1. **窗口标题行**「格数 198 · 可用空地 176 · 设备 3」仍在（属窗口框架，本批未动）。
2. **设备状态时间线**：已确认台账无逐台时间序列 ⇒ 本批用"5 日产出趋势"作为替代（更省数据、信息更实用）。
3. 概览内容总高已接近容量上限：再加区块须先压缩现有段高或引入分栏。
