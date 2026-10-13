---
change_id: CHG-20261005-MEALHOUR
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle, Languages]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopShift.cs
    type: M
    symbols: WorkshopShift.MealBreakfastHour, WorkshopShift.MealLunchHour, WorkshopShift.MealDinnerHour, WorkshopShift.HasShiftMeal, WorkshopShift.ClearShiftMeal, WorkshopShift.ExposeData
    lines: 41-95, 200-245
  - path: Source/PersonalChronicle/Automation/Dialog_HourPick.cs
    type: A
    symbols: Dialog_HourPick (整文件，单点时间选择器)
    lines: 1-120
  - path: Source/PersonalChronicle/Automation/Dialog_WorkshopShiftEditor.cs
    type: M
    symbols: Dialog_WorkshopShiftEditor.ShiftMealHourOf, Dialog_WorkshopShiftEditor.SetShiftMealHour, Dialog_WorkshopShiftEditor.DrawShiftMealRow
    lines: 340-380
  - path: Source/PersonalChronicle/Automation/WorkshopDutyCoordinator.cs
    type: M
    symbols: WorkshopDutyCoordinator.AddMealHoursOfShift, WorkshopDutyCoordinator.MarkMealHour, WorkshopDutyCoordinator.BuildCrewMealHours(注释)
    lines: 415-480
  - path: Source/PersonalChronicle/Automation/Dialog_TimeRange.cs
    type: M
    symbols: Dialog_TimeRange (移除 closedWhenEqual 参数)
    lines: 20-45, 65-105
  - path: Source/PersonalChronicle/Domain/Meal/MealSchedule.cs
    type: D
    symbols: MealWindow (整文件删除)
    lines: 1-65
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: HourPickTitle, HourPickHint, HourPickClose (新增)
    lines: 538-546
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: HourPickTitle, HourPickHint, HourPickClose (新增)
    lines: 530-538
---

# 变更标注 · CHG-20261005-MEALHOUR

## 背景（玩家 2026-10-05，附班次编辑器截图）

> 「优化班次编辑器，**三餐不要时间段，而是改为时间选择，选择单个时间**。」

截图显示：班次编辑器里「班中早餐 / 午餐 / 晚餐」三行点开后是**起止段**弹窗
（"开始 00 — 结束 00 · 起 == 止 = 关闭该餐"）+ 三个快捷档。玩家要的是**选一个时刻**。

## 一、数据模型：6 字段 → 3 字段（单点）

| 旧（CHG-20261004-SHIFTMEAL） | 新（本批） |
|---|---|
| `MealBreakfastStartHour` + `MealBreakfastEndHour` | `MealBreakfastHour` |
| `MealLunchStartHour` + `MealLunchEndHour` | `MealLunchHour` |
| `MealDinnerStartHour` + `MealDinnerEndHour` | `MealDinnerHour` |

- `-1` = 该餐关闭（不再是"未设置 ⇒ 回落全局"，全局设置已在上一批删除）；
- **为什么单点是对的**：原版管制表本来就是**小时粒度**（每格 1 小时），
  一个时刻正好涂一格 ⇒ 不再需要"段 → 格"的展开，也就没有"跨天段 / 起 == 止"这些
  与"到点吃饭"无关的语义（那正是玩家实测困惑的来源）。

### 旧档迁移（不需要 Migration，不 bump `schemaVersion`）

```csharp
if (Scribe.mode == LoadSaveMode.LoadingVars)          // 只读档期读旧键
{
    Scribe_Values.Look(ref legacyMealBreakfastStart, "mealBreakfastStartHour", -1);
    ...（午/晚同）
}
if (Scribe.mode == LoadSaveMode.PostLoadInit)         // 幂等迁移
{
    if (MealBreakfastHour < 0 && legacyMealBreakfastStart >= 0)
        MealBreakfastHour = legacyMealBreakfastStart;
    ...
}
```

- **只在 `LoadingVars` 期**调用旧键的 `Look` ⇒ 新存档**不再写**这些键（自然淘汰）；
  旧档里的多余键由 Scribe 忽略；
- 迁移口径：**取旧起点**（7-8 ⇒ 7 点）——"到点吃饭"落在起点最贴近原意；
  旧终点在单点模型下无意义，**不读即丢弃**；
- 幂等：新字段已有值（≥0）则不覆盖 ⇒ 反复读档结果一致。

## 二、新增 `Dialog_HourPick`（单点时间选择器）

- 一个滑块（0-23）+ `−`/`+` 步进，显示 `07:00`；
- 底部三键：**「关闭该餐」**（回调 `-1`）/ 确定 / **取消（不发任何回调）**；
- **为什么不复用 `Dialog_TimeRange`**：那是"起止段"编辑器，带着半开区间 / 起==止 / 跨天
  一整套语义，对"单个时刻"是多余的 —— 玩家实测就是被这套语义搞混的。
- 格式化 `LabelOfHour` 为 `internal static`：班次编辑器行内文本与弹窗**共用同一实现**
  （否则会出现"弹窗里 07:00、行里 7 点"的漂移，GOV-005）。

## 三、涂表逻辑：段展开 → 单格

```csharp
private static void MarkMealHour(bool[] hours, bool[] shiftWork, int hour)
{
    if (hours == null || hour < 0 || hour >= hours.Length) return;
    if (shiftWork != null && (hour >= shiftWork.Length || !shiftWork[hour])) return;  // 不在工作时段内 ⇒ 不生效
    hours[hour] = true;
}
```

上一批（CHG-20261005-MEALBYSHIFT）的"**餐点必须落在该班次工作时段内**"判据**保留**，
只是逐项从"展开一段"变成"看一个格"。夜班餐点污染白班的问题不会回归。

## 四、连带清理（上一批的修正自然作废）

| 项 | 处理 | 理由 |
|---|---|---|
| `Dialog_TimeRange.closedWhenEqual`（CHG-20261005-TIMERANGE） | **移除** | 它的唯一用途是让用餐窗口把"起==止"显示为"关闭该餐"；三餐改用单点后**不再使用该弹窗** ⇒ 参数失去调用方 |
| `Domain/Meal/MealSchedule.cs` 的 `MealWindow` | **整文件删除** | 它是"段"的展开器（`Contains`/`IsEmpty`/`CrossesMidnight`），单点模型下无使用者（编译 0 错为证） |
| `WorkshopDutyCoordinator.MarkMealWindow` / `MarkWindow` | 删除 | 同上，由 `MarkMealHour` 取代 |

## 存档影响：无 Migration，不需要 bump `schemaVersion`

- 新键 `mealBreakfastHour` 等为**加性可选字段**（缺省 -1）；
- 旧键只在读档期读取、不再写入 ⇒ 旧档安全、新档干净；
- `WorkshopShift` 的其它字段与 `MapComponent` 侧完全未动。

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（`_scripts/compile_with_sdk.ps1`） |
| XML | 已验证 | Automation 中/英均可解析 |
| 残留检查 | 已验证 | 全仓无 `MealBreakfastStartHour` / `MealWindow` / `closedWhenEqual` 的**代码**引用（仅注释与迁移字符串） |
| 删除确认 | 已验证 | `Domain/Meal/MealSchedule.cs` 不存在 |
| 部署 | 已验证 | Defs 31 / Languages 39 / Assemblies 1 同步 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-MEALHOUR.md` |
| 实机 | 未验证 | 见下 |

### 实机验证要点（重启后）

1. **班次编辑器**：班中餐三行显示形如 `07:00` 或「未设置」；
   点击 ⇒ 打开**单点**选择器（一个滑块 + −/+，**没有"结束"那一行**）；
2. 弹窗底部「关闭该餐」⇒ 该行回到「未设置」；
   「取消」⇒ 保持原值（不会误关）；
3. **管制表**：配 07:00 的白班 ⇒ 7 点那一格是「用餐」；配 23:00 的夜班 ⇒ 23 点那格是「用餐」；
   **配在班次时段外的时刻不生效**（例如把夜班餐点配到 12:00）；
4. **旧档**：读档后原先的 7-8 餐段应变成单点 **7 点**（取起点）；
5. `Player.log` 无新的 `PersonalChronicle` 异常。

## 残留风险

1. **旧档迁移的信息损失**：旧"起止段"的**终点被丢弃**（7-8 ⇒ 7）。
   若玩家原本希望"8 点也在吃"，迁移后不再成立 —— 这是单点模型的必然结果（一个时刻 = 一格）；
2. **越界餐点仍无 UI 提示**：配到班次工作时段外的时刻静默不生效（上一批已列为残留，本批未变）；
3. **`TimeClosedMeal` / `PresetMealOff`（上一批新增的语言键）现为孤儿键**：
   键查找语义下零运行时影响，本批不动（后续清理批次可一并移除）。
