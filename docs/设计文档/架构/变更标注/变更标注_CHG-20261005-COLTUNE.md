---
change_id: CHG-20261005-COLTUNE
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle]
files:
  - path: Source/PersonalChronicle/Archive/UI/UIComponents.Tables.cs
    type: M
    symbols: UIComponents.TableHeader (sortCol 改 ref；两个兼容重载同步)
    lines: 60-140
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView.DrawDevices, WorkshopManageView.DeviceColSharesFull, WorkshopManageView.DeviceColStylesFull, WorkshopManageView.DeviceRowH, WorkshopManageView.DeviceHeaders, WorkshopManageView.DrawDeviceRow, WorkshopManageView.CompareDevicesBySort, WorkshopManageView.DeviceSortNumber, WorkshopManageView.FilteredDevices, WorkshopManageView.DurabilityText(删), WorkshopManageView.DurabilityTone(删), WorkshopManageView.search(删), WorkshopManageView.filteredCache/filteredFor/filteredDirty(删), WorkshopManageView.RebuildFiltered(删)
    lines: 176-200, 258-290, 590-640, 680-760, 775-800, 1390-1520
---

# 变更标注 · CHG-20261005-COLTUNE

## 背景（玩家 2026-10-05，附设备页截图）

> 「**移除搜索框、表头排序无效。移除耐久、优化列宽列高。**」

四项逐条落实；其中"排序无效"是**我上一批引入的真 bug**，先说明根因。

## 一、排序无效：`sortCol` 按值传递（我的 bug）

```csharp
// 错误（上一批）：sortCol 是**值**参数
internal static int TableHeader(..., int sortCol, ref RUIF.SortState sortState)
{
    ...
    sortCol = clicked;        // ← 只改了方法局部副本
}

// 调用方
TableHeader(..., deviceSortCol, ref deviceSortState)   // ← 传的是字段的**副本**
```

⇒ 切换列时字段**永远保持 -1** ⇒ 表现为「点表头完全没反应、不显示 ▲▼」。
`sortState` 因为本来就带 `ref` 而能变，所以现象是"列永远不切换"（点同一列降序也无效，
因为 `sortCol == clicked` 恒不成立）。

**修复**：签名改 `ref int sortCol`，两个兼容重载各自准备局部变量。**一行参数修饰符之差，行为天壤之别**。

## 二、移除搜索框（含死代码清理）

- 删 UI：`Widgets.TextField` + 整个 26px 输入行；KPI 横幅直接顶到页面顶部；
- 删字段：`search` / `filteredCache` / `filteredFor` / `filteredDirty`；
- 删方法：`RebuildFiltered`；
- `FilteredDevices()` **保留方法名与调用点**但不再过滤（直接返回 `devices`，其顺序已由 Refresh 期的
  `ApplyDeviceSort` 定好）—— 保留是为了将来若重新需要过滤/分组时有**唯一入口**，而不是让各页面各写一遍。

**理由记录**：一个车间的设备是十几台量级，页面已有 KPI 横幅与排序 ⇒ 搜索收益为零，
却常年占 26px 并把 KPI 行往下挤。

## 三、移除耐久列（11 → 10 列）

连带清理：`DeviceColSharesFull` 一项、`DeviceColStylesFull` 一项、`DeviceHeaders` 一项、
`DrawDeviceRow` 的耐久块、`DurabilityText` / `DurabilityTone` 两个方法（已无使用者）、
`DeviceSortNumber` 的 case 3。

### ✅ 顺带的**结构简化**（值得单独记一笔）

耐久列原本插在第 4 列（下标 3），把后面所有列都推后一位 ⇒ 行内要写一堆
`compact ? 5 : 6` 之类的三元下标。移除它之后：

```
全档 0名称 1操作员 2状态 3制作 4产出 5运行 6产值 7建立 8效率 9位置（10 列）
窄档 0名称 1操作员 2状态 3制作 4产出 5运行 6产值 7位置        （8 列）
```

⇒ **前 7 列在两种档位下完全同号**，只有末尾三列分叉。行内前 7 个下标从"每个都写三元表达式"
变成一组 `const`。这同时**降低了下标漂移的风险**（那正是"表头与行错位"的历史根源）。

## 四、列宽与列高

- **列宽**（10 列，总和 = 1）：名称 `.19` / 操作员 `.08` / 状态 `.07` / 制作 `.17` / 产出 `.10` /
  运行 `.095` / 产值 `.10` / 建立 `.07` / 效率 `.065` / 位置 `.06`
  —— 文本列给足以免截断、数值列够用；
- **列高**：新增 `DeviceRowH = 26f`（原用全局 `RowH = 30`）。设备行内容只有单行文本，
  30 在 20 号字下留白过多；26 能在同样高度里多显示约一屏半的行。
  ⚠ **刻意不动全局 `RowH`** —— 人员页 / 班次表也用它，改它属越界变更。

## 存档影响：**无**（纯绘制层与内存状态；排序状态本就是会话态）

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（`_scripts/compile_with_sdk.ps1`） |
| 部署 | 已验证 | Defs 31 / Languages 39 / Assemblies 1 同步 |
| 死代码 | 已验证 | 搜索缓存三件 + `RebuildFiltered` + 两个耐久方法均已无引用 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-COLTUNE.md` |
| 实机 | 未验证 | 见下 |

### 实机验证要点（重启后）

1. **排序（重点）**：点任意列名 ⇒ **列名变色并出现 ▲**；再点 ⇒ ▼；第三次 ⇒ 恢复原序；
   换列 ⇒ 从 ▲ 开始；排序结果按"台账原始值"而非显示文本（如「累计运行」升序时 `0 小时` 在顶、
   `13 天 16 小时` 在底）；
2. **搜索框**：设备页顶部不再有输入框，KPI 横幅（`设备 13 · 运行中 3 · …`）紧贴页面顶部；
3. **耐久列**：表头与行中都不再有「耐久 / 100%」；列数从 11 变 10；
4. **列宽/列高**：行更紧凑（26px），一屏能多看见几行；各列文字不再被挤掉（名称、当前制作给足宽度）；
5. **对齐**：表头与数字的右边缘仍逐列重合；
6. `Player.log` 无新的 `PersonalChronicle` 异常。

## 残留风险

1. **`ColDurability` 语言键成为孤儿**（中英各一条）：键查找语义下零运行时影响，本批未清理；
2. **排序状态不持久化**：关面板 / 读档后回到"不排序"（仍是会话态）；
3. **`DeviceCompactWidth` 仍按 11 列推导**：`DeviceColSharesFull.Length × 88` ⇒ 现在是 `10 × 88 = 880`，
   门槛自动变低（更多情况走全档）。这是**正确**的（列少了、每列更宽裕），但需留意：
   若将来改为 12 列，门槛会反向升高——机制本身是自洽的；
4. **窄档的 8 列布局未变**，`DeviceColSharesCompact` / `DeviceColStylesCompact` 与服务端下标映射
   仍保持"抽掉末尾两列"的语义（已在注释里写明两组下标对照）。
