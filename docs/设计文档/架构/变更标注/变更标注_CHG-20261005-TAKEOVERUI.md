---
change_id: CHG-20261005-TAKEOVERUI
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle, Languages]
files:
  - path: Source/PersonalChronicle/Automation/Zone_Workshop.cs
    type: M
    symbols: Zone_Workshop.autoTakeoverDone, Zone_Workshop.AutoTakeoverDone, Zone_Workshop.ExposeData
    lines: 283, 286, 330
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView.Refresh
    lines: 330-341
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: WorkPrioManualOn, WorkPrioManualOff, WorkPrioHint, TakeoverIdle, TakeoverDoTip, TakeoverReleaseTip
    lines: 427-431, 450-454
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: (same 6 keys)
    lines: 293-296, 446-450
---

# 变更标注 · CHG-20261005-TAKEOVERUI

## 背景（玩家裁定，2026-10-05）

> 「默认开启这个，并优化这项设置的说明以及 tooltip 说明，尽可能简单明了，减少阅读成本。」
> 经确认：「这个」= **接管原版工作**。

## 交付一：把「接管原版工作」做成**真的默认开启**

### 审计发现：文案与实现早已不一致

- 文案写着「**默认关闭**」（`TakeoverIdle`）；
- 但实现自 `CHG-20261003-TAKEOVER-R2` 起已在 `WorkshopManageView.OnRosterChanged` 里
  调 `SyncVanillaWorkTakeover(zone)` ⇒ **排班/名册变更时会自动接管**（EXC-2026-016 F4 已相应修订）。

⇒ 缺的只有一处：**触发条件仍不充分**。`OnRosterChanged` 只在"排班/名册变更"时跑，
**读档后不改任何配置就不会触发** ⇒ 玩家仍得手点一次才"生效"（与 `CHG-20261005-EVERWORK` 同一缺口）。

### 修复

`Zone_Workshop` 新增 **`autoTakeoverDone`**（加性可选、随档持久化）+ `Refresh()` 内兜底：

```csharp
if (zone != null && !zone.AutoTakeoverDone)
{
    Manager?.SyncVanillaWorkTakeover(zone);
    zone.AutoTakeoverDone = true;
}
```

**为什么是"一次性"而不是"每次开面板都跑"**：
`TakeOverVanillaWork` 只跳过"档位已为 0"的项，**不区分"PC 取消的"与"玩家刚手动勾回的"**
⇒ 若每次都跑，玩家勾回的项会在下次开面板被再次取消，**「解除接管」形同虚设**。
一次性 ⇒ 首次自动接管，之后完全由玩家掌控。

**为什么顺序在 `EnsureWorkSettingsInitialized` 之后**：先保证成员工作设置可用，再取消勾选车间类工作；
反过来的话，取消勾选会落在未初始化的 `workSettings` 上（原版会 `Log.Error` 且不生效）。

**与「名册变更自动接管」并存**：`OnRosterChanged` 那条路径**不受本标记限制** —— 名册真的变了，
新成员需要被处理。

## 交付二：文案与 tooltip 精简（中英各 6 条）

| Key | 前（字符） | 后 | 变化 |
|---|---|---|---|
| `WorkPrioManualOn` | 28 | 21 | 状态行只留结论 |
| `WorkPrioManualOff` | 33 | 20 | 去掉重复解释 |
| `WorkPrioHint`（tooltip） | ≈120 | ≈60 | **砍掉一半**：只留"为什么开"+"开了会怎样"+"还能改" |
| `TakeoverIdle` | 57 | 43 | 去掉"默认关闭"（已改为默认开启） |
| `TakeoverDoTip` | 80 | 36 | 一句话说清"做什么+可逆" |
| `TakeoverReleaseTip` | 55 | 22 | 只留唯一判据 |

合计中文约 **373 → 202 字（-46%）**；英文同步精简。

**顺带修的格式缺陷**：中英两个语言文件里 `PostNotDelivers` 与 `TakeoverIdle` 原先**挤在同一行**
（缺换行）⇒ 已拆为独立行，避免后续编辑误伤。

## 存档影响：**加性字段，无迁移**

`autoTakeoverDone` 旧档缺键读回 `false` ⇒ 读档后首次开面板补做一次自动接管，与"新车间"同行为
⇒ **不需要 bump SchemaVersion、不需要 Migration**（SAVE-004）。

## 验证

1. **默认开启生效**：读档后**不做任何操作**，开一次车间面板 ⇒
   成员在工作面板里的「车间类」工作应已被取消勾选（搬运/清洁/医疗/灭火保持勾选）；
2. **可逆且稳定**：点「解除接管」还原后，**再开关面板多次** ⇒ 不会被再次自动取消
   （标记已置位，这是本批的关键设计目标）；
3. **文案**：设置页两行说明与四条 tooltip 明显变短，不再需要逐字读完才能理解。

## 残留风险

1. **首次自动接管是"沉默"发生的**（只写工作面板、不弹提示）—— 沿用既有自动路径的行为，
   不做额外通知以免刷屏；玩家可从工作面板或本行状态看出结果；
2. **新成员仍走 `OnRosterChanged` 路径**：名册变更时自动接管，不受一次性标记限制
   ⇒ 若你希望某人保留车间类原版工作，请在**名册变更之后**再手动勾回（那时不会再被覆盖）；
3. 若将来需要"每个新成员都自动接管一次"（而非只对首次），需把标记改为"按成员记"
   —— 当前设计与"保护玩家手改"的目标一致，刻意不做。
