---
change_id: CHG-20261005-RUIFTABLE
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle]
files:
  - path: Source/PersonalChronicle/Archive/UI/UIComponents.Tables.cs
    type: M
    symbols: UIComponents.TableHeader (新增 StatCol[] 重载，旧 3 参保留)
    lines: 15-56
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView.DeviceColStylesFull, WorkshopManageView.DeviceColStylesCompact, WorkshopManageView.DeviceColSharesFull, WorkshopManageView.DeviceCompactWidth, WorkshopManageView.deviceColWidths, WorkshopManageView.DrawDeviceRow, WorkshopManageView.DurabilityTone, WorkshopManageView.DrawDevices(表头调用)
    lines: 578-700, 725-930, 975-995
---

# 变更标注 · CHG-20261005-RUIFTABLE

## 背景（玩家 2026-10-05，附设备页截图）

> 「**基于 RUIF 框架模组，优化设备页面。对齐等等前端优化**」

## 一、根因诊断（三条，全部可在截图里对上）

| # | 问题 | 代码证据 |
|---|---|---|
| 1 | **表头与数据列错位** | `UIComponents.TableHeader` **硬编码 `TextAnchor.MiddleLeft`** 画列名，而数据行的数值列是 `MiddleRight` ⇒ 两边锚点不同，**必然错位**（截图里"产出/累计运行/累计产值"的列名贴左、数字飘在别处） |
| 2 | **列边界不齐** | 每列各自算宽 `rect.width * shares[i]`，且数值列还额外减 `numPad` ⇒ 无统一列间留白，末列与中间列的间距不一致 |
| 3 | **11 列挤在约 895px 里** | `DeviceCompactWidth` 写死 900，而玩家窗口约 895px **仍走全档** ⇒ 每列仅 50~70px ⇒ 全面截断（"B · 炉灶（电力）"被切、"烹饪简单食物 永远"独占） |

## 二、改法：把对齐交给 RUIF，而不是两处各写一份

### 1. 表头与数据行**共用同一份列样式**（核心）

```csharp
// 列样式（新）：文本列左、数值列右、弱信息列 Dim
private static readonly RUIF.StatCol[] DeviceColStylesFull = { ... };

// 表头：由框架解析锚点
UIComponents.TableHeader(headRect, DeviceHeaders(compact), shares, styles);

// 数据行：读的是**同一个数组**
RUIF.TableFmt.ColumnAlignOf(styles, i)      // 每列一处
```

⇒ 结构上不可能再错位 —— 不是"人工保证两份常量一致"，而是**只有一个来源**。

### 2. 列宽改用框架的切分（统一 gap）

```csharp
RUIF.TableFmt.ColumnSplit(rect, shares, DeviceColGap, w);   // 写入复用缓冲，绘制期零分配
...
x += w[i] + DeviceColGap;
```

### 3. 窄档阈值由「每列可读下限」推导（不再写死）

```csharp
private const float DeviceMinColW = 88f;
private static readonly float DeviceCompactWidth = DeviceColSharesFull.Length * DeviceMinColW;  // 11 × 88 = 968
```

⇒ 玩家截图那个约 895px 的窗口现在会**走窄档（8 列，每列约 110px）** ⇒ 不再截断。
⚠ 声明顺序有意义：该字段读数组，必须排在数组声明**之后**（否则拿到 null）。

### 4. 位置列缩写

`thing.Position.ToString()` → `"(98, 0, 113)"`（13 字符）⇒ 改为 `"98,0,113"`（9 字符，同样信息）。

### 5. 耐久配色改用框架阈值

```csharp
return RUIF.TableFmt.BandColor(thing.HitPoints / (float)thing.MaxHitPoints);
```

⇒ 与 RUIF `StatTable` 带色列**同一套** `<0.34 / <0.67` 分档，同屏不会出现两种判色标准。
（框架的 `BandColor` 返回 `Dead/Warn/Alive`，比原先自写的三档更符合"低=红、高=绿"的直觉。）

## 三、用到的 RUIF 能力（本批 = "基于 RUIF" 的具体落实）

| RUIF 成员 | 用途 |
|---|---|
| `TableFmt.ColumnAlignOf(cols, i)` | 表头与数据格**共用**的逐列锚点 |
| `TableFmt.ColumnSplit(rect, shares, gap, widths)` | 比例列宽 + 统一列间留白（零分配，写调用方缓冲） |
| `TableFmt.BandColor(ratio)` | 阈值三档配色（耐久列） |
| `StatCol { Align, Dim }` | 列样式声明（文本/数值/弱化） |

### ⚠ 为什么**没有**直接用 `RUIF.TableAtoms.StatTable` 整体替换

`StatTable` 的契约明确写着「**不读 Mouse/Event**」（纯 presentational）⇒ 直接替换会**丢掉**
「点击行 → 跳转镜头并选中设备」以及品质徽标、状态色点等行内元素。
故本批**只借它的三件底层能力**（列样式解析 / 列切分 / 阈值色），保留自定义行绘制 —— 功能不减、对齐由框架保证。

## 存档影响：**无**（纯绘制层，不涉及任何 Scribe 键或运行时状态）

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（`_scripts/compile_with_sdk.ps1`） |
| 部署 | 已验证 | Defs 31 / Languages 39 / Assemblies 1 同步 |
| 兼容性 | 已验证 | `TableHeader` 3 参重载保留 ⇒ 班次表 / 人员页行为**逐字不变** |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-RUIFTABLE.md` |
| 实机 | 未验证 | 见下 |

### 实机验证要点（重启后）

1. **对齐**：设备页表头「产出 / 累计运行 / 累计产值 / 建立时间 / 效率 / 位置」应与其**数字右边缘对齐**
   （此前表头贴左、数字贴右）；
2. **列不再挤压**：约 895px 的窗口现在走窄档 ⇒ 8 列、无截断；
   拉宽窗口超过 968px ⇒ 自动回到 11 列；
3. **位置列**显示为 `98,0,113`（无括号空格），不再被截成 `(98, 0…`；
4. **耐久列颜色**：≥67% 绿、34~67% 黄、<34% 红（框架阈值）；
5. 点击任意行 ⇒ 跳转镜头并选中该设备（功能未回退）；
6. 班次表 / 人员页外观**无变化**（它们仍用旧重载）；
7. `Player.log` 无新的 `PersonalChronicle` 异常。

## 残留风险

1. **其它表未统一**：班次表、人员页仍走 `TableHeader` 的 3 参重载（全左对齐）。
   它们的数值列较少，问题不突出；若要统一，只需给它们各补一份 `StatCol[]` 并传第 4 参（**机制已就绪**）；
2. **列宽仍是"份额 + 余宽归末列"**：未做用户可拖拽调整（RUIF 备有 `TableAtoms` 的列宽手柄 + `TableFmt.ResizeWidth`，
   属可选的后续增强）；
3. **窄档列数由"抽掉三列"硬编码**：若将来新增列，需同步维护 `DeviceColStylesCompact` / `DeviceColSharesCompact`
   与行内的下标映射（三处，已在注释里标明两组下标对照）；
4. **`DeviceColGap = 4f` 是常量**：与 RUIF `PawnTableFrame` 用的 2f 不同（本页列多、需要更大呼吸），
   属**有意差异**，非疏漏。
