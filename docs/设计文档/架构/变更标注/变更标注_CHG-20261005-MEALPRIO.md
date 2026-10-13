---
change_id: CHG-20261005-MEALPRIO
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopDutyCoordinator.cs
    type: M
    symbols: WorkshopDutyCoordinator.ApplyWorkHoursToCrew
    lines: 324-350
  - path: Source/PersonalChronicle/Domain/Meal/MealScheduleDefaults.cs
    type: M
    symbols: MealScheduleDefaults.DefaultBreakfastEndHour, MealScheduleDefaults.DefaultLunchEndHour, MealScheduleDefaults.DefaultDinnerEndHour
    lines: 81-102
  - path: Source/PersonalChronicle/ChronicleSettings.cs
    type: M
    symbols: ChronicleSettings.ExposeData
    lines: 166-186
---

# 变更标注 · CHG-20261005-MEALPRIO

## 背景（玩家实机反馈，2026-10-05）

> 「唯一问题是【管制】相关代码。我在班次中设置了对应的三餐时间，重新修改了班组，
> 但最终无法正常执行。我建议【用餐】只占 1 个小时。默认设置白班：早上七点、中午 12 点、下午 5 点。」

截图实证：原版【管制】表里白班行只有「工作/睡眠」两色，**看不到任何用餐格**。

## 根因（已取证）

`WorkshopDutyCoordinator.ApplyWorkHoursToCrew` 的**判定顺序**把「非工作时段」放在了用餐之前：

```csharp
if (!work[h])                             { target = Sleep; }   // ← 先判：非工作时段 ⇒ 一律睡眠
else if (meal != null && IsMealHour(...)) { target = meal; }     // ← 后判：用餐只在此分支内才有机会
```

⇒ **落在非工作时段（或跨越工作/睡眠边界）的餐点，永远不会被涂成「用餐」** ——
玩家在班次里配好的三餐被静默吞掉，管制表上自然看不到用餐格。

**已排除的其它可能**（逐项取证，避免误判）：

| 可能原因 | 取证结论 |
|---|---|
| 班次编辑器没存下餐点 | ❌ 不是。`DrawShiftMealRow` 点击后经 `Dialog_TimeRange` 回调 `SetShiftMeal(slot, s, e)` 写回 ≥0；未设时显示"跟随全局"✓ |
| 班次餐点没落盘 | ❌ 不是。`WorkshopShift.ExposeData` 六个键齐全 ✓ |
| `Normalize` 限制窗口长度 | ❌ 不是。只做 `ClampHour`，无最小/最大跨度 ✓ |
| `MarkWindow` 不认 1 小时窗口 | ❌ 不是。半开区间 `Contains`，`[7,8)` 正常命中 7 ✓ |
| 设置页改不到 1 小时 | ❌ 不是。Slider 0-23，无最小跨度 ✓ |

## 修复一：用餐改为**最高优先**

```csharp
if (meal != null && IsMealHour(mealHours, slots, h, meal))
{
    target = meal;                          // ① 用餐最高优先
}
else if (!work[h])
{
    target = TimeAssignmentDefOf.Sleep;     // ② 非工作时段 ⇒ 睡眠
}
else
{
    target = onDuty;                        // ③ 工作时段
}
```

**理由**：餐点是玩家**显式配置**的（班次字段或全局窗口），显式配置必须压过"非工作时段"这条**隐式默认**。
**代价照实说**：若把某班次的餐点配到该成员的睡眠时段（如夜班配在正午），他会在睡眠时段被要求去吃饭 ——
但那是玩家自己配的，属预期；不加这个优先级，就只剩"餐点必须恰好落在工作时段内才生效"这种不可预测语义。

## 修复二：默认每餐 **2 小时 → 1 小时**（7-8 / 12-13 / 17-18）

起点本就已是 **7 / 12 / 17**（2026-10-04 那次调整的成果）⇒ 本次只收窄长度。
依据：餐段是"到点去吃"的**触发窗口**而非"吃两小时"；2 小时会把上午切成"工作—吃饭—工作—吃饭"四段。

### ⚠ 配套：老档**指纹式迁移**（否则改默认值对老档无效）

Scribe 键在老档里**已有值**（7-9 / 12-14 / 17-19），`Scribe_Values.Look` 只在**缺键**时才用默认值
⇒ 只改常量的话玩家会看到"改了但我的没变"（UI-009 不可自证）。

`ChronicleSettings.ExposeData` 中新增：

```csharp
if (Scribe.mode == LoadSaveMode.PostLoadInit
    && MealBreakfastStartHour == 7 && MealBreakfastEndHour == 9
    && MealLunchStartHour == 12 && MealLunchEndHour == 14
    && MealDinnerStartHour == 17 && MealDinnerEndHour == 19)
{
    MealBreakfastEndHour = MealScheduleDefaults.DefaultBreakfastEndHour;   // 9 → 8
    MealLunchEndHour = MealScheduleDefaults.DefaultLunchEndHour;           // 14 → 13
    MealDinnerEndHour = MealScheduleDefaults.DefaultDinnerEndHour;         // 19 → 18
}
```

- **安全**：三个窗口**同时**等于旧默认才迁移 ⇒ 玩家只要自定义过任意一餐就不覆盖（指纹式，非无条件覆盖）；
- **幂等**：迁移后不再匹配旧默认 ⇒ 不会重复 ⇒ **无需新增标记字段**（不动 SchemaVersion）；
- **只在 `PostLoadInit`**：Saving 阶段跑会改写玩家正在保存的档。

## 存档影响：**无新增字段、无 SchemaVersion 变更**

## 已核对通过

- **班次餐点优先于全局**：`AddMealHoursOfShift` 走 `shift.HasShiftMeal` 分支时**不读全局开关** ⇒
  即使「三餐作息」总开关关闭，只要班次配了餐点仍生效（这正是玩家遇到的路径）；
- **餐点并集**：`BuildCrewMealHours` = 该班组全部指派班次的餐点并集 ∪ 当日生效班次，重复幂等 ✓；
- **幂等写入**：值未变不写，避免原版 `SetAssignment` 的 Postfix 唤醒洪泛 ✓。

## ⚠ 登记：一处既有语义分叉（本批**未改**）

`Dialog_TimeRange` 的口径是「**起 == 止 = 全天**」（见该文件类注释，与 `WorkshopHours.InRange` 一致）；
而 `MealWindow` 的口径是「**起 == 止 = 空窗口 ⇒ 该餐关闭**」（`IsEmpty`）。
⇒ 同一个时段控件在两处被解释成相反含义：玩家在班次里把某餐设成"起==止"时，
**意图若是"全天供应"则失效，意图若是"关掉这餐"则恰好符合**。
1 小时窗口（7→8）不涉及此分叉。**是否统一口径需玩家裁定**（改任一侧都可能有既有用法受影响）。

## 验证

1. **重启后**：设置页「作息与用餐」中三餐应为 **7-8 / 12-13 / 17-18**（老档自动迁移；若你曾自定义过则保持你的值）；
2. **核心**：把某班次的班中餐设到**任意时段**（包括原本睡眠的时段）⇒ 打开原版【管制】表 ⇒
   **应立即看到「用餐」色块**，不再被吞；
3. 改班组 / 改班次 / 跨天 ⇒ 用餐格**保持**（不再被重建冲掉）；
4. 到点后：成员在餐段内且饿（`CurLevel < 0.9`）⇒ 会去吃饭（`WorkGiver_Meal` 驱动）。

## 残留风险

1. **餐点落在睡眠时段的取舍**（见修复一"代价照实说"）—— 若实际观感不佳，可退回"仅工作时段内涂用餐"
   并把规则改为"配到睡眠时段的餐点忽略"，但需玩家明确选择；
2. **旧默认迁移是一次性的**：迁移后若玩家又手动调回 7-9/12-14/17-19，下次读档会**再次**迁移
   （因为指纹又匹配了）—— 这是"幂等"与"尊重意图"的冲突点，当前选择前者（更贴合本次诉求）；
3. **未同步修英文/中文文案**：本批无新增/改动 Keyed 文本（默认值改动不影响显示格式）。
