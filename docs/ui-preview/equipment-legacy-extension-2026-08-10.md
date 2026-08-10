# 装备传承（Equipment Legacy）拓展方案 · 开发文档

**版本口径**：v4.9（预览原型，对应 `docs/ui-preview/完整档案馆UI预览.html` 当前状态）
**日期**：2026-08-10
**定位**：PersonalChronicle 是**只读编年史**——只记录游戏内真实发生的事件，经 `IArchiveUiDataProvider` 聚合成 Read Model 快照，在窗口内纯消费呈现。

---

## 0. 设计红线（不可逾越）

本 mod 不是装备养成 RPG。所有拓展必须遵守：

1. **只读记录，不制造机制**——不得通过 Harmony 给装备加战斗/经济加成。
2. **数据驱动，零硬编码叙事**——展示内容必须来自真实事件流（`ChronicleEvent`），不凭空生成文本（"器灵评语" 之类被否决）。
3. **窗口只消费快照**——排序 / null-guard / 关联计算归属 `ArchiveUiDataProvider`，不在窗口内。
4. **UI 经 Design System 两层**——新增视觉走 `UITheme` 令牌 + `UIComponents` 组件，绘制态配对恢复。

---

## 1. 现有传承（v4.7）拆解基线

- **数据原子**：`ThingObject.HolderRecords`（`HolderRecord`：StableId / LabelSnapshot / StartTick / EndTick / IsFirst / Kind）。
- **当前呈现**：`BuildLegacy` 把持有链**线性化**成「第N任持有者」表，衍生称号 / 概要 / 评价 / 纪要。
- **局限**：单链，无法表达并行共用、回流、制造者命运双联。

---

## 2. 拓展 Tab 总览（Weapon › thing-xxx）

| Tab | 键 | 数据来源 | 状态 |
|---|---|---|---|
| 概览 Overview | `Overview` | 现有 w.events/kills/holderCount/crafter | 已有（恢复） |
| 溯源 Origin | `Origin` | 新增 `origin{}` + `makerChain{}` | 新增 |
| 传承 Legacy | `Legacy` | 现有 `legacy[]` | 已有 |
| 同袍 CoUse | `CoUse` | 新增 `coUse[]` | 新增 |
| 退役 Decommission | `Decommission` | 新增 `decommission{}` | 新增 |

五个 tab 全部为**只读叙事**，对应 C# 侧应落在 `ArchiveUiDataProvider.BuildDetail(Thing)` 产出的 Section 快照。

---

## 3. 各 Tab 数据契约草案

### 3.1 溯源 Origin（`origin` + `makerChain`）
```csharp
class ThingOriginView {
    OriginKind Kind;        // Craft | Battle | Trade | Salvage | Gift
    string FromLabel;       // 来源对象显示名（战利品=被剥取者；购入=商队）
    string FromStableId;    // 可下钻 pawn/其他；null 表示无名
    string WhereLabel;      // 出处地点
    string Note;            // 溯源纪要（接事件 desc，非生成）
}
class MakerChainView {
    string MakerStableId;   // 制造者
    bool MakerDiedByOwn;    // 双联叙事：制造者死于自己造的物
}
```
- **来源**：`OriginKind` 由 `ChronicleEvent` 的 `EventKind` 直接映射（Crafted→Craft、Kill-and-strip→Battle 等），**零新增写入**。
- **makerChain**：纯字段关联——读 `crafterId` 对应的 `PawnObject` 死亡记录，若死因事件涉及本物 StableId，则 `MakerDiedByOwn=true`。

### 3.2 同袍 CoUse（`coUse`）
```csharp
class CoUseView {
    List<CoUseRow> Rows;    // 曾与本装备并行的殖民者
}
class CoUseRow {
    string PawnStableId;
    string PawnLabel;
    int SharedDays;         // 并行持有天数（重叠区间求并）
}
```
- **来源**：反转 `IRelationProvider`——对 `HolderRecords` 做时间区间重叠计算，得到共用矩阵。纯 Read Model 聚合，性能需 cap（活跃成员 ≤20）。

### 3.3 退役 Decommission（`decommission`）
```csharp
class DecommissionView {
    bool HasRecord;
    string LastHolderLabel; string LastHolderStableId;
    string LastPlaceLabel;
    int ServiceDays;
    string LastBattleLabel;
}
```
- **来源**：新增**只读捕获点** `OnThingDestroyed`（HP=0 / 熔毁 / 丢弃进废料），仍走 `TryRecord`，**不阻止销毁**。对应 pawn 的 `OnPawnDied`，是「物的死亡记录」。
- 在册装备 `HasRecord=false`，UI 显示空态。

---

## 4. 前端实现（预览原型已落地）

`完整档案馆UI预览.html` 现状：
- `WEAPON_TABS = ["Overview","Origin","Legacy","CoUse","Decommission"]`。
- i18n：zh/en 两块新增 `origin*/makerChain/coUse*/decommission*` 键（见文件 1250–1270 行附近）。
- 渲染分支：`renderWeaponPanel` 内 `if (tab === "Origin"|"CoUse"|"Decommission")` 三块已实现。
- 样式：`.chip-origin` / `.chip-taint` / `.bar` / `.bar-fill` / `.decommission-stamp` / `.maker-chain` 经 CSS 令牌，未散落 `new Color`。
- JS 语法校验通过（`new Function`）。

---

## 5. 落地优先级

| 梯队 | 内容 | 改动面 | 风险 |
|---|---|---|---|
| 即时 | 工坊署名链（makerChain） | 仅 `BuildDetail` 加关联 | 零（已有 crafterId） |
| 短期 | 溯源 Origin | 事件流按 `StableId` 串联 + 映射 `OriginKind` | 低 |
| 短期 | 退役 Decommission | 加 `OnThingDestroyed` 捕获点 | 低（只读） |
| 中期 | 同袍 CoUse | Read Model 跨对象区间聚合 | 中（性能 cap） |
| 中期 | 传承图谱（DAG） | `HolderRecord` 加 `ParentIds`+`Shared/Reunite` 类型 | 低（旧档兼容） |

> 注：传承图谱（A 方案）是 Legacy 的自然延伸，需扩展 `HolderRecord.Kind` 枚举，本文档预留接口位但未在预览中实现。

---

## 6. 待确认 / 开放问题

1. `OnThingDestroyed` 的触发时机：RimWorld 中装备销毁有多条路径（熔毁/损耗/卖店），需确认统一捕获点。
2. `SharedDays` 重叠计算是否计入「借用（loan）」kind（当前 `HolderRecord.Kind` 已含 loan）。
3. 跨存档「流浪物证（外流→回流）」暂未纳入，需 `WorldComponent`，列入后续 v5.x。
