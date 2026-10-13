---
change_id: CHG-20261005-TABLESORT
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle]
files:
  - path: Source/PersonalChronicle/Archive/UI/UIComponents.Tables.cs
    type: M
    symbols: UIComponents.TableHeader (新增 gap/样式/三态排序重载), UIComponents.headerWidths
    lines: 15-105
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView.deviceSortCol, WorkshopManageView.deviceSortState, WorkshopManageView.ApplyDeviceSort, WorkshopManageView.CompareDevicesBySort, WorkshopManageView.DeviceSortTextOf, WorkshopManageView.DeviceSortNumber, WorkshopManageView.Refresh, WorkshopManageView.DrawDevices(表头调用)
    lines: 269-280, 305-315, 735-755, 1395-1520
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Shifts.cs
    type: M
    symbols: WorkshopManageView.ShiftColShares(补末列), WorkshopManageView.ShiftColGap, WorkshopManageView.ShiftColStyles, WorkshopManageView.shiftColWidths, WorkshopManageView.DrawShiftRow, WorkshopManageView.DrawShifts(表头调用)
    lines: 37-75, 120-135, 175-210
---

# 变更标注 · CHG-20261005-TABLESORT

## 背景（玩家 2026-10-05）

> 「**设备页面可以排序嘛？** 批准其他页面进行统一 RUIF」

⇒ 回答：**可以，已加**（用 RUIF 自己的三态排序语义，不另造一套）。

## 一、顺带修掉一个**既有不一致**（本次最重要的一处）

上批（CHG-20261005-RUIFTABLE）我把**数据行**改成 `RUIF.TableFmt.ColumnSplit(rect, shares, gap, w)`
—— 该函数切列时会**扣掉 gap**；而**表头**仍是 `shares[i] × rect.width`（不扣）⇒ **两端列宽本来就不同**，
越靠右误差越大。这不是"新 bug"，而是"只改了一端的必然结果"，本次一并收口：

```csharp
// 表头：内部也用 ColumnSplit（与数据行同一切分口径）
RUIF.TableFmt.ColumnSplit(rect, shares, gap, widths);
...
x += widths[i] + gap;
```

⇒ **表头与数据行的列宽现在逐像素一致**（`gap` 由调用方传同一常量）。

## 二、排序：用 RUIF 的三态，重排放在 Refresh 期

### 表头组件（`UIComponents.TableHeader` 新增重载）

```csharp
internal static int TableHeader(Rect rect, string[] cols, float[] shares,
    RUIF.StatCol[] styles, float gap, int sortCol, ref RUIF.SortState sortState)
```

- 点击某列 ⇒ 切到该列并置**升序**；再点同列 ⇒ **升 → 降 → 无**（循环），
  复用 `RUIF.TableFmt.NextSort` —— 与 RUIF `TableCandAtoms.PawnTableFrame` **同一套语义**；
- 排序指示：当前列列名加 `▲/▼` 并用 `UITheme.Accent`（与 RUIF `TableAtoms.HeaderCell` 的视觉语言一致），
  但**锚点仍由列样式决定** —— 直接改用 `HeaderCell` 会让数值列表头又跑回左边（它写死左对齐）；
- **本方法不排序任何数据**：只维护"点了哪列、什么方向"。

### 重排位置（关键性能决定）

```csharp
// 表头点击 ⇒ 只更新状态，然后调 Refresh()
if (UIComponents.TableHeader(..., ref deviceSortState) >= 0) { Refresh(); }

// Refresh() 内（设备清单重建之后）
ApplyDeviceSort();
```

⇒ **绘制路径零排序成本**。若把 `Sort` 放进每帧入口，就是每帧 O(n log n) + 台账查询。

### 排序键口径

| 列 | 键 |
|---|---|
| 名称 | **别名**（别名按建立时间分配 ⇒ 等价于"按加入车间的先后"，比按同一个 `def.label` 更有信息量） |
| 操作员 / 当前制作 | **Refresh 期快照文本**（不拼串） |
| 状态 | 状态键 |
| 耐久 / 产出 / 运行 / 产值 / 建立 / 效率 | **台账原始数值** |
| 位置 | `x+y+z` 的单调键 |

⚠ **不解析显示文本**：`"1,080"` / `"13 天 21 小时"` / `"0.5"` 这类**格式化字符串按数字比较会全错**
（`"13 天…"` 与 `"9 天…"` 的首字符就分不出大小）。这是本实现最容易踩的坑，已在比较器注释里写明。

**稳定性**：同值兜底比较 `thingIDNumber` ⇒ 反复刷新不会让同值行的相对顺序跳来跳去。

## 三、统一范围：两处已统一，人员页**评估后不动**

| 页面 | 处理 | 依据 |
|---|---|---|
| **设备页** | 已统一（列样式 + gap + 排序） | 本次主题 |
| **班次表** | 已统一（列样式 + gap） | 5 列全文本 ⇒ 锚点全左（与现状一致），收益在**统一切分口径** |
| **人员页** | **不动**（保持现状） | 见下 |

### 为什么人员页不动（**评估结论，非遗漏**）

1. 它的列宽是**混合模型**：4 个常量份额 + **动态"活动"列**（由余量预算 clamp）+ **自适应 chip**
   （岗位列按钮宽按文字测宽）+ 固定 `RemoveBtnW`；
2. 硬套 `ColumnSplit` 会与 **chip 自适应宽度**机制冲突 —— 那条机制是 2026-10-04 专门为解决
   "整格按钮与表头视觉错位"引入的（`PostUnplaced` / `ColPostShare` 降级为上限的注释即为此）；
3. 人员页**全列左对齐、无数值列** ⇒ 不存在本次要修的"对齐"问题 ⇒ 收益小、回归风险中。

⇒ 结论：**记录为"评估后不改"**；若将来该页出现数值列，接入方式与设备页完全相同（补一份 `StatCol[]`，
并把宽度计算换成 `ColumnSplit` + `w[i]`）。

## 存档影响：**无**（纯绘制与内存排序状态；`deviceSortCol` / `deviceSortState` 是会话态，不入档）

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（`_scripts/compile_with_sdk.ps1`） |
| 部署 | 已验证 | Defs 31 / Languages 39 / Assemblies 1 同步 |
| 兼容性 | 已验证 | `TableHeader` 3 参重载保留 ⇒ 未接入样式的表行为逐字不变 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-TABLESORT.md` |
| 实机 | 未验证 | 见下 |

### 实机验证要点（重启后）

1. **排序**：点设备页任意列名 ⇒ 该列名变色并出现 `▲`；再点一次 ⇒ `▼`；第三次 ⇒ 箭头消失（回到原序）；
   换另一列 ⇒ 该列从 `▲` 开始；
2. **排序正确性**（重点核对这三列）：
   - 「**累计运行**」升序 ⇒ 顶部应是 `0 小时` 的设备，底部是 `13 天 21 小时`；
   - 「**产出**」升序 ⇒ `0 / 0` 在前、`21 / 181` 在后（按**件数**，不是按 "1 / 102" 的字符串）；
   - 「**耐久**」升序 ⇒ 100% 与受损设备分开（受损若存在应排最前）；
3. **列对齐**：表头与数字的右边缘**逐列重合**（含最右的「位置」列）；
4. **班次表**：外观与之前基本一致（列间多了 4px 呼吸），列名与行内容仍对齐；
5. 点击表头**不会**触发"跳到设备镜头"（表头区与数据行区互不干扰）；
6. `Player.log` 无新的 `PersonalChronicle` 异常。

## 残留风险

1. **人员页未统一**（见第三节的评估结论）；
2. **排序状态是会话态**（不入档）：关面板/读档后回到"不排序"。
   若要持久化，需落盘两个字段（属加性可选字段，成本很低），但当前**未做**；
3. **排序指示箭头的宽度未计入列宽**：列名 + `▲` 可能在极窄列上溢出 1~2px（视觉可接受；
   若要严格，需在表头用 `TruncateToWidth` 并预留箭头宽）；
4. **末列吃余宽的语义**：班次表补了一个 `0f` 份额给「操作」列，使余宽归它
   （与原"操作吃余"一致）；若将来在中间插列，需同步维护该数组长度与 `ColumnSplit` 的 n = min 行为。
