---
change_id: CHG-20261004-SHIFTMEAL
version: v1.1.5
date: 2026-10-04
author: AI
scope: [Source/PersonalChronicle, Languages]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopShift.cs
    type: M
    symbols: WorkshopShift.MealBreakfastStartHour, WorkshopShift.HasShiftMeal, WorkshopShift.ClearShiftMeal, WorkshopShift.ExposeData
    lines: 35-77, 186-192
  - path: Source/PersonalChronicle/Domain/Meal/MealScheduleDefaults.cs
    type: M
    symbols: MealScheduleDefaults.DefaultDinnerStartHour, MealScheduleDefaults.DefaultDinnerEndHour
    lines: 81-93
  - path: Source/PersonalChronicle/Automation/WorkshopDutyCoordinator.cs
    type: M
    symbols: WorkshopDutyCoordinator.BuildCrewMealHours, WorkshopDutyCoordinator.AddMealHoursOfShift, WorkshopDutyCoordinator.MarkMealWindow, WorkshopDutyCoordinator.MarkWindow, WorkshopDutyCoordinator.IsMealHour, WorkshopDutyCoordinator.ApplyWorkHoursToCrew
    lines: 137-165, 246, 268-275, 298, 318-410
  - path: Source/PersonalChronicle/Automation/Dialog_WorkshopShiftEditor.cs
    type: M
    symbols: Dialog_WorkshopShiftEditor.InitialSize, Dialog_WorkshopShiftEditor.DrawShiftMealRow, Dialog_WorkshopShiftEditor.SetShiftMeal
    lines: 56-60, 166-190, 338-390
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: PersonalChronicle.Automation.Tab.MealBreakfast, MealLunch, MealDinner, ShiftMealFollowGlobal, ShiftMealHint, ShiftMealClearAll
    lines: 302-315
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: PersonalChronicle.Automation.Tab.MealBreakfast, MealLunch, MealDinner, ShiftMealFollowGlobal, ShiftMealHint, ShiftMealClearAll
    lines: 303-316
---

# 变更标注 · CHG-20261004-SHIFTMEAL

## Change Summary

玩家需求（2026-10-04，游戏内验收时提出）：

> 「建议班次设置表优化，添加用餐时间段设置」「标准三餐，七点、十二点、五点」

动因：**倒班制下白班与夜班的餐点本来就不该是同一时间**，而此前的用餐只能配"全局一份"
（设置页三餐作息）。本条实现**班次级三餐**，并保留全局作为未配置时的回落。

## 实现

### ① 数据（`WorkshopShift`）

新增 6 个加性字段（早/午/晚各 start/end）：

- **`-1 = 未设置`** ⇒ 该班次回落全局三餐（与"`start == end` = 该餐显式关闭"区分开，两者语义不同）；
- `HasShiftMeal`（任一段起点 ≥ 0）、`ClearShiftMeal()`（UI「全部跟随全局」用）；
- `ExposeData` 6 行 `Scribe_Values.Look(..., -1)`。
- ⚠ 加性可选字段 + 默认值 ⇒ **旧档读取即为"未设置"**，不需要 Migration，不需要 bump `schemaVersion`。

### ② 写入链（`WorkshopDutyCoordinator`）

`mealHours`（24 位）由 `BuildCrewMealHours` 按**逐班次**合并：

```
对每个相关班次：配了班中餐 ⇒ 用它的三段；没配 ⇒ 用全局三餐窗口（全局开关关闭时贡献为空）
结果取并集
```

- `FixedPerShift` ⇒ 用该班组的**全部指派班次**（`crew.AssignedShifts`）；
- `RotateDaily` ⇒ 用**当日实际生效**的班次（`effective`）。
- `IsMealHour` 参数由"全局 `MealSchedule`"换成 `bool[] mealHours`（保留"手涂用餐格不被冲掉"的分支）；
- 窗口展开复用 `Domain.Meal.MealWindow.Contains`（半开区间 + 跨天语义单一权威，GOV-005）；
- 口径边界不变：**只在工作时间**内涂用餐，非工作时间照旧写 Sleep。

### ③ UI（班次编辑器）

新增 3 行「班中早餐 / 午餐 / 晚餐」，每行一个时段按钮（`Dialog_TimeRange`）：

- **未设置**时按钮显示「跟随全局（07:00–09:00）」—— 直接显示继承来的值，而不是空白（UI-009 自证）；
- 末行「未设置的餐点沿用设置页…」+「全部跟随全局」按钮（同行左右分栏，省一行高度）；
- ⚠ 该窗口 `resizeable = false` 且无滚动视图 ⇒ `InitialSize` **同步** 338 → **490**（4 × 38）。

### ④ 全局默认（按玩家口径）

`MealScheduleDefaults`：晚餐默认 **18–20 → 17–19**（早餐 7–9、午餐 12–14 本就符合
「七点、十二点、五点」）。

- ⚠ **只改默认值**：已保存的设置不受影响 ⇒ 想让现有殖民地也用 17 点，请在设置页
  「三餐作息」把晚餐起点拖到 17（一次拖动）。

## 兼容性

| 项 | 结论 |
|---|---|
| 旧档加载 | **能**。6 个新字段缺失 ⇒ 默认 `-1` ⇒ 全部"跟随全局"，与升级前行为**完全一致** |
| Migration | **不需要** |
| schemaVersion | **不需要** bump（加性可选段，符合项目惯例） |
| 公共 API | `ApplyWorkHoursToCrew` 为 private，签名变更不外泄 |
| 第三方 | 无新 Def、无 Harmony、不触碰其它 Mod |

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（首次编译暴露并修正了 `IsMealHour` 调用点漏改旧参数） |
| XML | 已验证 | 中/英 Keyed 各 2/2 解析通过 |
| 部署 | 已验证 | Defs 31 / Languages 37 / Assemblies 1 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261004-SHIFTMEAL.md -ExcludeFromFile .codebuddy/tmp/shiftmeal-out-of-scope.txt -Blocking` |
| 实机 | 待玩家验证 | 见下 |

## 实机验证要点

1. 车间管理 → **班次表** → 点某条班次 ⇒ 弹窗底部应有「班中早餐 / 午餐 / 晚餐」三行，
   且分别显示「跟随全局（07:00–09:00）」「（12:00–14:00）」「（17:00–19:00 或你设的值）」
   （*窗口比之前高，底部按钮不应被裁*）。
2. 点「班中早餐」→ 设为 `23:00–01:00`（跨天）→ 关闭并点一次「刷新」⇒ 该班次成员的
   【管制】表在 23:00 与 00:00 应是「用餐」（前提：这两小时在该班次工作时段内）。
3. 点「全部跟随全局」⇒ 三行回到「跟随全局」，管制表相应回到全局餐点。
4. 全局路径回归：不配任何班中餐时，行为应与升级前完全一致。

## 回滚 / 风险

- 纯加性改动：删除 6 字段的读取即可回滚；不写 `-1` 以外的值时不改变任何既有行为。
- 风险：多班次共用同一班组时，用餐小时取**并集**（可能比单个班次更多）；
  实际排班中"一班组 ↔ 一班次"是常态，故影响面小。
