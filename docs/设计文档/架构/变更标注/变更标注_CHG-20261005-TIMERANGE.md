---
change_id: CHG-20261005-TIMERANGE
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle, Languages, _scripts]
files:
  - path: Source/PersonalChronicle/Automation/Dialog_TimeRange.cs
    type: M
    symbols: Dialog_TimeRange (closedWhenEqual 字段 + 4 参构造 + DoWindowContents)
    lines: 20-45, 70-110
  - path: Source/PersonalChronicle/Automation/Dialog_WorkshopShiftEditor.cs
    type: M
    symbols: Dialog_WorkshopShiftEditor.DrawShiftMealRow
    lines: 351-360
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView.DeviceColSharesFull, WorkshopManageView.DeviceHeaders, WorkshopManageView.DrawDeviceRow, WorkshopManageView.DurabilityText, WorkshopManageView.DurabilityTone
    lines: 182-215, 620-660, 786-800
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: ColDurability, TimeClosedMeal, PresetMealOff
    lines: 534-540
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: ColDurability, TimeClosedMeal, PresetMealOff
    lines: 526-532
  - path: _scripts/audit-scribe.ps1
    type: A
    symbols: audit-scribe.ps1 (Scribe 完整性静态扫描，全文件)
    lines: 1-100
---

# 变更标注 · CHG-20261005-TIMERANGE

## 背景（玩家 2026-10-05：「按你推荐进行」）

三项一起落地：

1. **时段弹窗的语义分叉**（上批登记、本轮按推荐收口）；
2. **设备页「耐久」列**（零成本维护列，玩家在候选列中选定）；
3. **Scribe 体检脚本固化**到 `_scripts\`（本次证明有效）。

---

## 一、时段弹窗：同一个控件、两套相反的"起 == 止"

### 分叉的实际表现（取证）

`Dialog_TimeRange` 现状是一律显示「全天」：

```csharp
if (start == end) note += " · " + T("TimeAllDay");
// 第三快捷档 PresetAll：start = 0; end = 0;（按钮文字「全天」）
```

但两处调用方对"两端相等"的解释**相反**，且**两套解释都有正确的消费者**：

| 场景 | 底层判定 | 起 == 止 的含义 |
|---|---|---|
| 班次时段 | `WorkshopHours.InRange` | **全天班**（覆盖 0–23 时） |
| 用餐窗口 | `MealWindow.IsEmpty` | **空窗口 ⇒ 该餐关闭** |

⇒ 玩家在班次里把某餐设成 0–0 时，**界面说"全天"、实际关掉了这餐**
（UI 承诺与行为相反，UI-009）；点「全天」按钮更是直接踩进这个坑。

### 修法：**不动底层语义**，只让弹窗与调用方语义一致

> ⚠ 为什么不"统一为关闭"（我原先的初步倾向）：班次时段的"起 == 止 = 全天"是
> `WorkshopHours.InRange` 的**既有约定**，被班次命中判定 / 离班判定 / 固定班多处消费
> ⇒ 改它会把"全天班"静默变成"空班次"，属破坏性变更。两侧都另有一批既有用法，**改任一侧都会破坏**，
> 所以正确做法是让**界面说清楚**，而不是让底层二选一。

实现：

```csharp
/// 「起 == 止」在本场景下表示关闭（而非全天）
private readonly bool closedWhenEqual;

public Dialog_TimeRange(int s, int e, Action<int,int> onApply) : this(s, e, onApply, false) { }  // ← 班次侧，3 参保持向后兼容
public Dialog_TimeRange(int s, int e, Action<int,int> onApply, bool closedWhenEqual) { ... }
```

- 提示行：`closedWhenEqual ? T("TimeClosedMeal") : T("TimeAllDay")`；
- 第三快捷档：文字切为「关闭该餐」/「全天」（**同一个 0–0 数值、两种含义，文字必须跟着语义走**）；
- 用餐调用点（`DrawShiftMealRow`）传 `closedWhenEqual: true`；
- 班次调用点**不改**（3 参构造转发为 `false`，行为逐字不变）。

`TimeAllDayTip`（解释"全天班"的长提示）只挂在班次侧，用餐侧不受影响 ✓。

---

## 二、设备页「耐久」列（HP%）

- **列序**：名称 | 操作员 | 状态 | **耐久** | 当前制作 | 产出 | 累计运行 | 累计产值 | 建立时间 | 效率 | 位置（11 列）
- **与「状态」互补而非重复**：状态回答"现在能不能动"，耐久回答"还能撑多久"
  ⇒ 一眼扫出"在跑但快坏了"的机器；
- **零成本**：直接读 `Thing.HitPoints / MaxHitPoints`，**不新增采样、不新增持久字段**；
- **三段配色**：≥66% `Muted` / 33–66% `Threat`（橙）/ &lt;33% `Warn`（红）
  —— `Threat` 与 `Warn` 已由 2026-09-19 审计在三主题下互相拉开（距离 ≥ 0.25）⇒ 不会与"正常"档混淆；
- **无 HP 的 Def**（`MaxHitPoints == 0`）⇒ 显示 `--`，不谎报 0%（LAND-009）；
- **窄档降级**：宽 &lt; 900 时抽掉「耐久 / 建立时间 / 效率」三列（仍是 8 列），
  行绘制索引按档位分叉（**不能共用下标** —— 这是"表头与行错位"的典型来源，已在注释里标明两组下标）。

---

## 三、`_scripts\audit-scribe.ps1`（新增）

把本次用于数据域体检的扫描固化为仓内脚本，参数化（`-Root` / `-Out`）。

- **它抓到过什么**：本仓 2026-10-05 就是靠同类检查抓到 `runWindowCounts`（逐日机时窗）
  与设备产值字段两处**真实漏落盘**（写入正确、`ExposeData` 漏写 ⇒ 读档即丢）；
- **已知误报三类**（已写进脚本文件头，避免下次重走弯路）：
  ① 属性/`readonly`/`static`（脚本已过滤）；
  ② **枚举 ↔ int 互转写法**（字段名不出现在 `ref` 里，如 `PawnRecord.ArchiveStatus`）；
  ③ 缓存/派生/会话态字段（**正确做法就是不落盘**，如 `dutyPlan`、`maxRing`）；
- **建议用法**：任何改动持久字段的批次，在编译前跑一次（只读，无副作用）。

---

## 存档影响

**无。** 本批不新增/不修改任何 Scribe 键：
- 时段弹窗只改显示与按钮文案；用餐窗口的存储形状不变（仍是 `WorkshopShift` 的六个 int 键）；
- 耐久列是**纯读取**（`Thing.HitPoints` 由原版落盘）；
- 脚本不参与运行时。

⇒ 无需 Migration，不需要 bump `schemaVersion`。

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（`_scripts/compile_with_sdk.ps1`） |
| XML | 已验证 | 中/英 `Automation.xml` 均可解析 |
| 部署 | 已验证 | Defs 31 / Languages 39 / Assemblies 1 同步 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-TIMERANGE.md` |
| 实机 | 未验证 | 见下 |

### 实机验证要点（重启后）

1. **语义提示**：班次编辑器 → 点班次时段按钮 → 把两端设成相同 ⇒ 提示应是「全天」；
   再点**班中餐**的时段按钮 → 两端设成相同 ⇒ 提示应是「**起 == 止 = 关闭该餐**」，
   第三按钮文字为「**关闭该餐**」；
2. **耐久列**：设备页（全档宽度）⇒ 「耐久」列在「状态」右侧，显示百分比；
   低耐久设备呈橙/红色；无 HP 的设备显示 `--`；
3. **窄档**：用检视 Tab（窄）打开 ⇒ 自动少三列（耐久/建立/效率），无错位、无文字互压；
4. `Player.log` 无新的 `PersonalChronicle` 异常。

## 残留风险

1. **用餐窗口无法表达"整日供应"**：`MealWindow` 的首尾相等 = 关闭，因此"全天吃"在用餐侧
   没有表示法（班次侧有）。若玩家需要，应新增一个"整日"标志位（本批不做，避免扩大改动面）；
2. **`TimeAllDayTip` 的文案仍写"全天班"**：它只服务班次侧（已核对唯一调用点），
   用餐侧不会看到 ⇒ 无矛盾；
3. **耐久色在 Epic 主题下 `Warn` 是纯红**（1.0, 0, 0），观感较强；这是该令牌的既有语义
   （审计已确认它与 `Threat`/`Dead` 拉开），若要柔和需改主题令牌，属另一批。
