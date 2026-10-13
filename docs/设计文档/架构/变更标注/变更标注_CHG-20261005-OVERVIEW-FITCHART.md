---
change_id: CHG-20261005-OVERVIEW-FITCHART
version: v1.4.1
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle/Automation/WorkshopManageView.Overview.cs]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Overview.cs
    type: M
    symbols: WorkshopManageView.ChartPlotH, WorkshopManageView.TickBandH, WorkshopManageView.TickLabelW, WorkshopManageView.EffSectionH, WorkshopManageView.DrawBottleTrendSection, WorkshopManageView.DrawEfficiencySection
    lines: 46-75, 946-965, 1010-1020
---

# 变更标注 · CHG-20261005-OVERVIEW-FITCHART

## 背景

PM 实机截图反馈（制造中心 › 车间管理 › **概览**页底部大组件区）：

> 「发现一处车间体系的前端瑕疵，这两张图表的纵坐标间隔太小，导致文本遮盖挤兑问题。
> 而且概览界面还存在一部分空余位置，可以优化这两张表的参数。」

两张图 = **瓶颈演变（近 7 日 · 各设备机时）** 与 **稼动 vs 产出（各自峰值 = 100%）**。

## 一、根因（RUIF 绘制契约取证，非估值）

到 `RUIF/Source/Components/Charts.cs` 逐行核对，`GridH`（`:490-501`）是两张图共用的刻度绘制：

```csharp
private static void GridH(Rect plot, float hi)
{
    int n = ChartsFmt.NiceTicks(0f, hi, tickBuf);                       // target = 4 ⇒ 最多 6 档
    for (int i = 0; i < n; i++)
    {
        float y = ChartsFmt.Scale(tickBuf[i], 0f, hi, plot.yMax, plot.y);
        Draw.LineH(plot.x, y, plot.width, Tokens.BorderHair);
        Draw.Label(new Rect(plot.xMax - 34f, y - 12f, 34f, 12f),       // ← 标签几何
            tickBuf[i].ToString("0.#"), Tokens.FontLabel, Tokens.Dim, TextAnchor.UpperRight);
    }
}
```

由这段契约 + 本页原常量（`PeakGridH = 96` ⇒ `EffSectionH = 22 + 96 + 18 + 8 = 144`，
绘图区高 = `144 − 22 − 18 − 8 = 96`）推出三个缺陷：

| # | 缺陷 | 机制 | 实机证据 |
|---|---|---|---|
| **G1** | **最高刻度的标签压进标题行** | 标签矩形是 `plot.y − 12 .. plot.y`（`UpperRight`）⇒ 最高刻度（`y = plot.y`）的标签**越过绘图区上边界 12px**，而标题行只占 `rect.y + 4 .. + 22` ⇒ 落进标题带 | 右脚注「左旧 → 右新（6 天）」被最高刻度的数字压住（裁图确认） |
| **G2** | **刻度标签盖住最右侧数据** | 标签固定 34px 宽且右缘贴 `plot.xMax` ⇒ 与最右列数据同处一区 | 左图「今天」柱与刻度数字同列（裁图确认） |
| **G3** | **刻度间距不足 ⇒ 挤兑** | 间距 = 绘图区高 ÷ (刻度数 − 1)；`NiceTicks(target=4)` 给 6 档（如 hi=100 ⇒ 0/20/40/60/80/100）⇒ 96px 高时仅 **19px** 间距，而标签矩形高 **12px**，视觉几乎相接 | 右图右侧 6 个刻度数字明显拥挤（裁图确认） |

## 二、修复（只改本页几何，不动 RUIF）

新增三个**取自上述契约**的常量，并把两张图的绘图区按契约内缩：

```csharp
private const float ChartPlotH = 120f;   // 绘图区高（96 → 120 ⇒ 6 档刻度间距 24px，G3 解除）
private const float TickBandH  = 12f;    // 顶部刻度带（G1：最高刻度标签的归属带）
private const float TickLabelW = 34f;    // 右侧刻度标签列宽（G2：与 Charts.GridH 同值）
private const float EffSectionH = 22f + TickBandH + ChartPlotH + 18f + 8f;   // 144 → 180
```

绘图区：`plot.y = rect.y + 22 + TickBandH`、`plot.width = rect.width − padX*2 − TickLabelW`。

**几何自检（逐波段核对，确认无重叠）**：

| 段 | 瓶颈演变 | 稼动 vs 产出 |
|---|---|---|
| 标题 | `+4 .. +22` | `+4 .. +22` |
| 刻度带（最高刻度标签落点 `plot.y−12..plot.y`） | `+22 .. +34` ✓ | `+22 .. +34` ✓ |
| 绘图区 | `+34 .. +136`（高 102） | `+34 .. +154`（高 120） |
| 天标签 | `+137 .. +155` | — |
| 图例（`rect.yMax − 18 − 4`） | `+158 .. +176` | `+158 .. +176` |
| 底边距 | 4px ✓ | 4px ✓ |

- 刻度数 6 时间距 = 120 ÷ 5 = **24px**（标签 12px ⇒ 留 12px 净空，G3 解除）；
- 瓶颈演变的绘图区 102px ⇒ 间距 20px ✓（它多一条天标签带，故比右图矮）；
- **G1 解除**：最高刻度标签恰好落在预留的刻度带内，不再进入标题行；
- **G2 解除**：标签落在右侧 34px 内缩带内，不再盖数据。

## 三、概览页空余高度的处置

`OvBigBlockH = PeakSectionH + Gap + EffSectionH`（从 `314 → 350`，**+36**），
`OverviewContentHeight()` 与 `DrawOverviewBody()` 共用该常量 ⇒ **滚动范围自动同步**，
不会出现"滚不到底/底部空白"（本文件既有注释专门警告过这一对必须同步）。

本次只加高**这两张图**（`PeakGridH` 保持 96 ⇒ 上方热力网格不变），符合 PM「优化这两张表的参数」的口径。

## 四、验证

| 项 | 结果 |
|---|---|
| 编译 | **0 错 0 警** |
| 部署 | 127 文件（DLL 14:20:06） |
| 高度模型一致性 | `OvBigBlockH` / `OverviewContentHeight` / `DrawBigBlocks` 三者共用常量 ✓ |
| 实机 | **未执行**（游戏未运行）—— 需重启后按下方验收点核对 |

### 实机验收点

1. 右图右上角：脚注「左旧 → 右新（6 天）」**不再被数字压**，最高刻度单独占一行；
2. 两张图的纵轴刻度数字**两两之间有明显空隙**（24px / 20px），不再挤成一团；
3. 刻度数字**不再压在**最右侧的柱 / 折线端点上；
4. 概览页滚到底**仍有内容**、无异常空白（内容高 +36 已同步）。

## 五之二、第二轮：**改 RUIF 本体**（PM 授权）

### 触发

一轮（PC 侧留刻度带 + 右缩）交付后，PM 实机回报：

> 「Y 坐标的 100 80 60 40 0 这些刻度还是存在遮盖问题。我建议将文本放置右侧，不要滞留内部。
> **同时修改 RUIF 框架 mod 对应的组件**」

⇒ 一轮只是**绕开**，根因确实在框架侧：

| 复核项 | 事实 |
|---|---|
| `Charts.GridH`（`Charts.cs:490`） | 标签仍画在**绘图区内侧且贴右缘**：`Rect(plot.xMax - 34f, y - 12f, 34f, 12f)` + `UpperRight` ⇒ ① 压最右一列数据；② **最高刻度顶出绘图区上边界 12px**；③ 纵向上每档只隔 `绘图区高/(档数−1)`，标签高 12px ⇒ 档数 6 时必然挤兑 |
| `Charts.StackedBarChartV`（`:246`） | **唯一不走 `PlotArea` 的图表**：`GridH(r, …)` + 按 `r` 算柱几何 ⇒ 网格与柱铺满整 rect，连顶部内缩都没有 |

### 修复（RUIF：`Source/Components/Charts.cs`）

| 改动 | 内容 |
|---|---|
| `GridH` 标签归位 | 改画在 `plot.xMax + 2` 起的**右侧轴列**、以刻度线**垂直居中**（`MiddleLeft`）⇒ 上下各只探出 6px，恰好落在 `PlotArea` 预留的半行内 ⇒ **标签恒在传入 rect 内，既不压数据也不越界**（正是 PM 要的「放右侧、不要滞留在内部」） |
| `PlotArea` | 预留 **轴列（`AxisLabelW = 34`）** + **上下各半行（`AxisLabelH/2 = 6`）** + 原有底部分类标签行 |
| `StackedBarChartV` | 改走 `PlotArea` ⇒ 网格 / 柱几何 / 柱高**三者同源**于 `plot` |
| 新增公开 API | `Charts.AxisLabelW`（const 34）+ `Charts.PlotRectOf(rect, bottomLabels)` —— 后者是调用方自画"逐列对齐内容"的**唯一正确入口**（**仅新增，无签名删改**） |

### PC 侧相应调整

- **撤除**一轮的临时绕法：`TickBandH = 12` / `TickLabelW = 34` 两个常量与两处 `plot` 内缩（框架已负责）；
- 图表高度 120 → **140**（框架内缩 12 后净绘图区 128 ⇒ 6 档间距 ≈ 25.6px）；
- `EffSectionH = 22 + 140 + 18 + 8 = 188`，`OvBigBlockH = 162 + 8 + 188 = 358`；
- **「天标签」改用 `RUIF.Charts.PlotRectOf(plot)` 取列宽**：柱几何现在基于比传入 rect 窄 34px 的绘图区
  ⇒ 拿 rect 直接算会整体**错开一个轴列宽**（本轮实证）。天标签改为锚在 `barArea.yMax + 1`，与图例间距 9px。

### 影响面核查（PC 全部 6 个图调用点逐一过）

`RuifBridge` 转发的 5 个图表调用点 + 概览页 2 处：**均未自画列对齐标签**（`labels = null`，口径由下方文案说明）
⇒ 无新错位风险。另两处需注意但**非本轮引入**：

- `ArchiveMainTabWindow.PawnCharts.cs` 把 `LineChart` 叠画在 `BarChartV` 上：两者 `PlotArea` 的底部分类标签行差异（18px）
  **在改动前后完全相同** ⇒ 相对错位不变 ✓；
- 各调用点用 `plot.yMax` 往下堆内容：图表内部底边现在高 6px ⇒ 下方间距多 6px（纯观感，无对齐问题）。

### 验证

| 项 | 结果 |
|---|---|
| RUIF 编译 | **0 错 0 警**（`dotnet build Source/RUIF.csproj -c Release`） |
| RUIF 同步 | `sync_ruif.ps1` ⇒ 开发区/发布区 `RUIF.dll` hash 一致（`729243D7…`） |
| RUIF API 快照 | `gen_api_reference.ps1` 已刷新（184 类型 / 923 公共成员） |
| PC 编译 / 部署 | **0 错 0 警** / 127 文件（DLL 14:27:26） |
| 实机 | **未执行**（游戏未运行） |

### 版本处置

按 RUIF 既有惯例：`docs/06-发布记录/CHANGELOG.md` **累积到「未发布」段、不 bump 版本**（仅新增 API ⇒ 不触发 SemVer 主/次版本规则）。RUIF 当前 `modVersion` 仍为 `v1.3.0`。

### 补：第二轮实机验收点

5. 两张图的刻度数字**位于各自面板的右侧、且完全在绘图区之外**（不再压柱 / 压折线端点）；
6. 最高刻度（如 100）**单独一行**，不再与右上角脚注「左旧 → 右新（6 天）」重叠；
7. 「瓶颈演变」的天标签（6 天前…今天）与柱体**逐列对齐**（不偏移）。

## 五、残留（如实登记）

1. **刻度数与间距仍由数据决定**：`NiceTicks(target=4)` 的档数随 `hi` 变化（4～6 档）。
   当前 120px 高在最差情形（6 档）下间距 24px ✓；若某天 `hi` 使档数超过 6（不会：`target=4` 上界固定），
   需再抬 `ChartPlotH`。**改 RUIF 的 `GridH` 才能从根本上解耦"标签高度 × 档数"** —— 本次不改 RUIF。
2. `G1` 的根因（标签可越出绘图区）仍在 RUIF 侧：PC 侧是用"预留刻度带"绕开，而非修好契约。
   若后续 RUIF 把 `GridH` 改为在绘图区内锚定标签，本页的 `TickBandH` 可回退为 0。
