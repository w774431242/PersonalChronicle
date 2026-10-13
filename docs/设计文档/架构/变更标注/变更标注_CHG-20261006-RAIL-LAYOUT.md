---
changeId: CHG-20261006-RAIL-LAYOUT
title: 制造中心两侧轨道（原料库 / 设备库 / 成品库）布局优化 · 消除文字硬切与挤压
date: 2026-10-06
version: v1.5.1（未发版，仅工作区）
scope:
  - path: Source/PersonalChronicle/Manufacturing/UI/WorkshopFloorPlan.Rails.cs
    type: M
  - path: Source/PersonalChronicle/Manufacturing/UI/WorkshopFloorPlan.Layers.cs
    type: M
  - path: Source/PersonalChronicle/Manufacturing/UI/WorkshopFloorPlan.Routes.cs
    type: M
  - path: Source/PersonalChronicle/Application/ReadModels/WorkshopFloorSnapshot.cs
    type: M
  - path: Source/PersonalChronicle/Automation/WorkshopFloorViewProvider.cs
    type: M
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.AppShell.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
  - path: Languages/English/Keyed/Archive.xml
    type: M
---

# 变更标注 — CHG-20261006-RAIL-LAYOUT

> 范围：制造系统中心 ② 主视图两侧轨道（左：原料库 / 设备库；右：成品库）的**前端布局优化**
> 日期：2026-10-06 ｜ 触发：用户截图（左轨文案被硬切 + 空态像"面板坏了"）
> 编译：`dotnet build Source/PersonalChronicle.csproj` → **本轮 8 个文件 0 错 0 警**；
> 仓库另有 **2 个既有错误**（`ChronicleSettings.cs:216` `Settings` 未定义、`WorkshopManageView.Posts.cs:123`
> .NET Framework 下无 `GetValueOrDefault`），**非本轮引入**，已在 Remaining 中登记。
> 范围外：仓库存在大量既有未提交工作，通过 `-ExcludeFromFile .codebuddy/tmp/rail-oos.txt` 声明排除。
> 注：本标注文件自身由 gate-runner 从 diff 集合中剔除，故不列入 `scope`。

---

## 1. 问题 → 根因 → 修法

| 用户可见瑕疵（截图实证） | 真实根因 | 修法 |
|---|---|---|
| 标题口径行被硬切（`STOCK 原料 · 全地图库存` → `S|CK…全局图库存`）；`收货月台 RECEIVING ×2` 被切 | ① 默认轨宽 126px 是预览的**演示值**（预览字号 8–9.5px 连续缩放 `--rfs`），引擎只有三档字号、Tiny 的中文混排文案在 126−14 内放不下；② 截断未挂补偿（信息静默丢失） | 默认宽 126 → **172**，`RailGutter` 由本常量**派生**（172+10=182）；口径行文案收敛（`STOCK · 全地图库存`）+ 完整口径进悬停；**全部截断**改走 `TruncateToWidth(text,w,font,tipRect)`（截断即自动挂原文悬停） |
| 文字被垂直挤压；轨道拉宽后行与行重叠 | 行高/行距/小框高写死 `15 / 16 / 17 / 22` 四个字面量，**全部小于 Tiny 真实行高 18**；字号档升到 Small(22)/Medium(30) 后行高不变 | 全部派生：`RailLine()=Text.LineHeightOf(RailFont)`、`RailRowPitch()=RailLine()+2`、`RailChipHeight()=RailLine()+6`、成品库大数字用 `LineHeightOf(Medium)` |
| 空态只有一行 dim 文案，读起来像"面板坏了" | 空态无原因、无下一步；且**缺预览本来有的分区头** | 空态改两行（原因 `Floor.Raw.Empty` + 引导 `Floor.Raw.EmptyHint`）；补 `rsechead` 分区头（含 1px 虚线底） |
| 原料列表超出容量时**静默截断** | 原实现 `capacity` 截断后无任何提示（设备库有、原料库没有） | 预留一行画 `Floor.Raw.More`（`… 还有 N 项未显示`）；成品库 Top 同理（`Floor.Depot.More`） |
| 与预览相比缺 `rabc` 需求分档角标 | 预览 `buildRails()` 有 ABC 三色角标，落地产物没有 | `FloorRawItemView.TierKey`（Provider 计算：按当前工单**单次需求量**降序累计占比 A≤50% / B≤80% / C 其余，与预览 `railAbc()` 同法）；UI 只做 `TierKey → 颜色` 映射 |

---

## 2. 文件清单（GATE-DELTA 互含依据）

本轮改动 **8 个文件**：

| # | 文件 | 状态 | 归属 Gate |
|---|---|---|---|
| 1 | `Source/PersonalChronicle/Manufacturing/UI/WorkshopFloorPlan.Rails.cs` | 修改（重写） | UI / LOC / PERF |
| 2 | `Source/PersonalChronicle/Manufacturing/UI/WorkshopFloorPlan.Layers.cs` | 修改 | UI / PERF |
| 3 | `Source/PersonalChronicle/Manufacturing/UI/WorkshopFloorPlan.Routes.cs` | 修改 | UI / LOC |
| 4 | `Source/PersonalChronicle/Application/ReadModels/WorkshopFloorSnapshot.cs` | 修改 | DATA |
| 5 | `Source/PersonalChronicle/Automation/WorkshopFloorViewProvider.cs` | 修改 | DATA / UI |
| 6 | `Source/PersonalChronicle/MasterControl/MasterControlTabWindow.AppShell.cs` | 修改 | UI |
| 7 | `Languages/ChineseSimplified/Keyed/Archive.xml` | 修改 | LOC |
| 8 | `Languages/English/Keyed/Archive.xml` | 修改 | LOC |

---

## 3. 逐文件改动明细

### 3.1 `WorkshopFloorPlan.Rails.cs`（重写）

| 项 | 改动 |
|---|---|
| 常量 | 新增 `internal const float RailWidthDefault = 172f`（原 private 126）；`RailWidthMin` 保持 126；新增 `RailRefWidth=126`、`RailMinBarWidth=12` |
| 度量 | 新增 `RailLine()` / `RailRowPitch()` / `RailChipHeight()` / `RailScale(railWidth)`；删除 `RailRowHeight=17` / `RailChipHeight=16` 字面量 |
| 绘制原语 | 新增 `DrawRailSurface` / `DrawRailText(4 参截断补偿)` / `DrawRailTextFont` / `DrawRailTextAt` / `DrawRailSection`（rsechead）/ `DrawRailNote`（多行 + 未画到容器外）/ `SplitNote`（折行结果缓存，绘制路径零重复分配）/ `DrawRailAbc`（内联在 `DrawRawRow`）/ `DrawRailHandleHint`（把手 hover / 拖拽高亮 + 提示） |
| 左轨 | 页签带**待放置设备计数**（`设备库 (N)`）；正常态与空态都画分区头 `Floor.Raw.Section`；原料行 = `[ABC 角标] [名称] [水位条+30% 安全线] [数值]`，列宽按 `--rfs` 比例缩放、空间不足从名称列让位（D3）、极窄档退化为只画名称（D6）；溢出明示；空态两行 |
| 右轨 | 大数字行高由 `LineHeightOf(Medium)` 派生；在库件数 → Top 成品 → `Floor.Depot.Section` 品质分布 → 打包/发货小框；列表与 chips 均有底界约束 |
| 设备库 | 顶部 `rsechead`（`待放置设备 · N`）；分组头加虚线底；单行 tooltip 增加 `DeviceEntry.SubLine`（工单 · 功率）；溢出计数修正为"只统计未画出的行"（原实现把整车间都算 hidden） |
| 极矮保护 | `DrawRawPane` / `DrawDepotRail` 各加最小高度断言（不足时只留底板，不画半截、不溢出到轨道外） |
| 会话态 | `ResetRailSession()` 增补 `railPressId / railPressPos` 复位 |
| 注释如实化 | 拖宽档（>182px）轨道覆盖画布交互区的问题**未被本轮闭合**，原注释"绝不侵入画布"与实现不符 ⇒ 改为如实描述（见 §5 已知偏差） |

### 3.2 `WorkshopFloorPlan.Layers.cs`

`T(string key)` 由 `key.Translate().ToString()`（每帧翻译 + 分配）改为 `UIComponents.Loc(key)`（语言级缓存）。
一处改动覆盖本 partial 类全部无参翻译点（PERF-002）。

### 3.3 `WorkshopFloorPlan.Routes.cs`

`T1(key, int)` 由 `key.Translate(arg)` 改为 `key.Translate((NamedArgument)arg)`（避免落到已过时的 `params object[]` 重载）；
本方法**两处 partial 共用一份定义**（Rails.cs 不再重复定义，此前会导致 CS0111）。

### 3.4 `WorkshopFloorSnapshot.cs`

`FloorRawItemView` 新增 `public string TierKey = string.Empty;`（需求分档 A/B/C；空串 = 不画角标，不编造）。
**Save 影响：无**（本条是 Read Model 快照字段，不入存档）。

### 3.5 `WorkshopFloorViewProvider.cs`

`FillRawRail` 新增分档计算（按单次需求降序累计占比 → A/B/C），随行写入 `TierKey`。
口径差已在代码注释与 `Floor.Raw.AbcTip` 悬停中写明：预览按"全部配方需求"（静态演示），本工程按"当前工单需求"（动态）。

### 3.6 `MasterControlTabWindow.AppShell.cs`

`RailGutter` 由 `136f`（写死）改为 `WorkshopFloorPlan.RailWidthDefault + 10f`（派生）。避免"轨道默认宽改了、画布让位忘改"⇒ 轨道盖住画布落点区（历史 P1-11 同类事故）。

### 3.7 语言文件（zh + en，双侧同步）

新增键：`Floor.Raw.StencilTip / Section / AbcTip / EmptyHint / More`、
`Floor.Depot.StencilTip / Section / SectionTip / More`、
`Layout.RailTab.DeviceSection / DeviceSectionTip`、`Layout.RailResizeTip`。
文案微调：`Floor.Raw.Stencil`（`STOCK 原料 · 全地图库存` → `STOCK · 全地图库存`）、
`Floor.Depot.Stencil`（`DEPOT 成品 · 车间范围内` → `DEPOT · 车间范围内`）、
`Floor.Raw.Empty` / `EmptyHint`（拆分原因与引导）、三处"溢出提示"文案改为
`（拉高窗口可见）`（原"拉宽 / 拉高"方向错误——拉宽会升字号档、行数反而更少）。占位符 `{0}` 双侧一致。

---

## 4. Gate 结果

| Gate | Result | Evidence |
|---|---|---|
| GATE-UI | **PASS WITH EXCEPTION** | `pc-ui-loc-guard` 只读审查（2026-10-06）：状态齐全（页签选中 / 悬停 / 拖拽把手 / 待选行 / 空态 4 种 / 溢出 3 处）、Theme 令牌集中（0 处裸色值）、列宽预算在 126px 极窄档数学核验不产生负宽/重叠；例外 = 2 项 P3 边界（见 §5） |
| GATE-LOC | **PASS** | 双侧 Archive.xml 键逐条核对齐备、`{0}` 一致、无裸显示文本；`T1` 走 `NamedArgument`；`\n` 用法与既有先例（`PersonalChronicle.UI.Combat.Notes`）一致 |
| GATE-PERF | 未跑（转交） | 绘制期仍有 tooltip 字符串拼接（原实现既有），`T()` 改缓存是改善；建议由 `pc-perf-tester` 复核 |
| GATE-TEST | **部分**（编译证据） | 本轮 8 文件 0 错 0 警；游戏内启动 / 存读档 / 语言切换**未执行**（沙箱无游戏进程）⇒ 见 §6 待验收 |
| GATE-DELTA | 见 gate-runner 输出 | `-Base HEAD -ExcludeFromFile .codebuddy/tmp/rail-oos.txt -Blocking` |

---

## 5. 已知偏差 / 降级登记（不伪造通过）

1. **B 类降级**（引擎缺口，登记接受）：
   - 预览 `.siderail` 的 `linear-gradient` 底色与圆角 → 实色 + 1px 边框；
   - 预览 `.rhandle:hover` 渐变 → 1px 强调色竖线 + 悬停提示；
   - 预览 `.rdock` 的前缀符号 `▣ / ◈`（U+25A3 / U+25C8）**未采用**：无法在本机核实引擎字体是否含该字形（缺字会显示为方框，反而制造新的视觉瑕疵）；
   - 预览 `.rqual span` 的 chip 描边 → 纯文本（轨道窄档下描边 chip 会挤掉行）。
2. **P3-1（拖宽档交互重叠，未闭合）**：轨道可拖至 360px，而画布让位是**编译期常量** 182px ⇒ 拖宽超过 182 时轨道盖住画布左侧，该区域的落点判定与绘制不一致（`Mouse.IsOver(画布)` 仍可能判真）。彻底闭合需把画布让位改成运行时值（独立变更）。默认 172 档安全（行右端 173 < 182）。
3. **P3-2（极矮窗口）**：已加最小高度断言（只留底板）；实机"平面图高 120–166px"档需复验。
4. **P4（保留项）**：品质 chips 溢出仍是静默 `break`（7 档极少触发）；设备库页签文案 `设备库 (N)` 每帧拼接（已挂悬停补偿）；`lastFloorVp*` 只写不读（既有死字段，非本轮）。
5. **既有编译错误（非本轮）**：`ChronicleSettings.cs:216`（`Settings` 未定义）、`WorkshopManageView.Posts.cs:123`（`GetValueOrDefault` 在 .NET Framework 不存在）。

---

## 6. 待实机验收（本标注不得据此判"完成"）

1. **1:1 还原**：默认 172px 下 `STOCK · 全地图库存`、`收货月台 RECEIVING ×2`、`发货月台 SHIPPING ×2` 是否完整显示（Small 档中英混排宽度）。
2. **行高回归**：默认（Small）与拖宽 ≥239px（Medium）两档无行重叠、无文字下半截被盖。
3. **空态渲染**：两行是否按预期分行（无残留 `\n` 字面量）。
4. **分档角标**：有工单时 A/B/C 是否与"单次需求量"排序一致；悬停 `AbcTip` 口径文案是否正确。
5. **拖宽档**：拖到 240/360 后，在重叠区点击是否出现"设备行点选 + 画布放置"双响应。
6. **极矮窗口**：窗高拉小到平面图 < 166px 时轨道是否只留底板。

---

## 7. 回滚

本变更为纯 UI 层 + 一个 Read Model 字段（不入存档）：
- 回滚代码：`git checkout -- <§2 的 8 个文件>`；
- 语言文件新增键可保留（未引用键无副作用）；
- 无 Save / Migration / 版本号影响。
