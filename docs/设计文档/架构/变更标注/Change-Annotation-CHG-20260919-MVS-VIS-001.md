---
change_id: CHG-20260919-MVS-VIS-001
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/MVS/**, Source/PersonalChronicle/Application/**, Languages/**, Tests/**, docs/**]
files:
  - path: Source/PersonalChronicle/MVS/Domain/CommodityVisibility.cs
    type: A
    symbols: CommodityVisibility.IsVisible
    lines: 1-60
  - path: Source/PersonalChronicle/MVS/Data/TechLevelBridge.cs
    type: A
    symbols: TechLevelBridge.ToArchetype / ToVerse
    lines: 1-70
  - path: Source/PersonalChronicle/MVS/Data/FactionCommodityVisibility.cs
    type: A
    symbols: FactionCommodityVisibility.IsVisible
    lines: 1-70
  - path: Source/PersonalChronicle/MVS/Application/Adapters/FactionProfileAdapter.cs
    type: M
    symbols: TryGetProfile（改用 TechLevelBridge）/ 移除私有 ToArchetypeTechLevel
    lines: 50-80
  - path: Source/PersonalChronicle/Application/ReadModels/EconomyMarketPanelSnapshot.cs
    type: M
    symbols: EconomyMarketRow.NotInvolved
    lines: 118-140
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs
    type: M
    symbols: BuildMarketPanel（profile 捕获 / notInvolvedMark / notInvolvedCount / 分支 / SubText）
    lines: 44-190
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: MVS.Economy.Market.Sub.NotInvolved / MVS.Economy.Market.NotInvolved
    lines: 116-122
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: MVS.Economy.Market.Sub.NotInvolved / MVS.Economy.Market.NotInvolved
    lines: 108-114
  - path: Tests/CommodityVisibilityTests.cs
    type: A
    symbols: CommodityVisibilityTests（8 例）
    lines: 1-130
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/SDD_派系画像人数因子接入.md
    type: A
    symbols: SDD（Draft v0.3）
    lines: 1-270
  - path: docs/changelog/v1.4.x_2026-09-19_市场视图派系过滤.md
    type: A
    symbols: CHG-20260919-MVS-VIS-001
    lines: 1-130
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-VIS-001.md
    type: A
    symbols: CHG-20260919-MVS-VIS-001
    lines: 1-140
---

# 变更标注 · CHG-20260919-MVS-VIS-001（市场视图按派系科技档过滤）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-MVS-VIS-001`
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-MVS-VIS-001
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent；落地对象是**用户此前已拍板**的上游设计 §10-1）
- Scope(摘要): 市场面板按选中派系的科技档过滤可见品种，被过滤品种如实显示「不涉及」

### 一句话动机
用户指示「继续」⇒ 落地 §10-1。实现时选择**从真实 `ThingDef.techLevel` 推导**而非给品种手工声明档位
⇒ **零编造值**；代价是与 §10 的举例产生一处行为差异，**已上报未擅自处置**。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `MVS/Domain/CommodityVisibility.cs` | A | `IsVisible` | 1-60 | 规则内核（纯函数）：物品档 ≤ 派系档 ⇒ 可见；**未知档两个方向 fail-open** | GOV-004 / DATA-006 | ARCH / TEST |
| `MVS/Data/TechLevelBridge.cs` | A | `ToArchetype` / `ToVerse` | 1-70 | **双向**枚举桥；把正向从 `FactionProfileAdapter` 上移，消除即将出现的重复 | ARC-006 / GOV-004 | ARCH |
| `MVS/Data/FactionCommodityVisibility.cs` | A | `IsVisible` | 1-70 | 读 `MVSCommodityRegistry.ThingsOf` 判「存在任一物品够得着」；空命中集 fail-open | DATA-006 / DATA-007 | DATA |
| `MVS/Application/Adapters/FactionProfileAdapter.cs` | M | `TryGetProfile` | 50-80 | 改用 `TechLevelBridge.ToArchetype`；**删除私有副本**（防双向映射漂移） | GOV-004 | ARCH |
| `Application/ReadModels/EconomyMarketPanelSnapshot.cs` | M | `EconomyMarketRow.NotInvolved` | 118-140 | 新增 bool + 长注释（为何不用"文本 == 不涉及"反推 ⇒ GOV-009） | GOV-009 / UI-001 | UI / DATA |
| `.../BuiltinEconomyProvider.MarketPanel.cs` | M | `BuildMarketPanel` | 44-190 | ① 捕获 `profile`（原 `out _` 丢弃）；② 逐行判可见性；③ 不涉及行画占位且**不进汇总**；④ 副标追加计数 | GOV-005 / LAND / GOV-010 | DATA / UI / LOC |
| `Languages/*/Keyed/MVS.xml` | M | `Market.NotInvolved` / `Market.Sub.NotInvolved` | ~116 / ~108 | 中英各 +2 键（键集合 **316 = 316**） | GOV-010 / LOC-001 | LOC |
| `Tests/CommodityVisibilityTests.cs` | A | 8 例 | 1-130 | 含**头号断言**「Undefined 两向 fail-open」+ 单调性穷举 + `Ultra` 边界 | GOV-011 | TEST |
| `docs/设计文档/.../SDD_派系画像人数因子接入.md` | A | Draft v0.3 | 1-270 | 登记 §4.4 实现方式变更 + **§10 举例行为差异** + `Ultra/Archotech` 边界 | SDD-002 / SDD-009 | SDD |
| `docs/changelog/v1.4.x_2026-09-19_市场视图派系过滤.md` | A | `CHG-20260919-MVS-VIS-001` | 1-130 | 更新日志：零编造设计 / 差异上报 / 未做项 | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-VIS-001.md` | A | `CHG-20260919-MVS-VIS-001` | 1-140 | 本文件自身（标注须覆盖全部改动文件，含自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步
> （MD5 `9CF3787A8F969874A7F872AE7C0B8DB6`，两区一致）。

### 关键 diff 片段

```diff
// BuiltinEconomyProvider.MarketPanel.cs —— 第三次"把丢弃的 out 参数用起来"
- || !MarketContracts.Factions.TryGetProfile(selectedFactionDefName, out FactionArchetypeData _))
+ FactionArchetypeData profile;
+ if (string.IsNullOrEmpty(selectedFactionDefName)
+     || MarketContracts.Factions == null
+     || !MarketContracts.Factions.TryGetProfile(selectedFactionDefName, out profile))
```

```diff
// BuiltinEconomyProvider.MarketPanel.cs —— 逐行可见性（不进汇总）
+ row.NotInvolved = !FactionCommodityVisibility.IsVisible(commodity, profile.TechLevel);
+ if (row.NotInvolved)
+ {
+     row.SupplyText = notInvolvedMark;
+     row.DemandText = notInvolvedMark;
+     row.NetText = notInvolvedMark;
+     row.NetTone = SystemCenterTone.Flat;
+     notInvolvedCount++;
+     // ⚠ 价格列照常画：因子是全球读数，与派系是否参与无关
+ }
+ else
+ {
      row.SupplyText = FormatRate(commodity.supplyBaseline);
      ...
      totalSupply += commodity.supplyBaseline;   // ⚠ 只累加参与品种
      totalDemand += commodity.demandBaseline;
+ }
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏（生成脚本自检 declared = hit）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— T2 批次与其前批次由 `-ExcludeFromFile` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] 无调试代码残留 / 死代码（**已删除** `FactionProfileAdapter.ToArchetypeTechLevel` 私有副本）
- [x] 无临时文件残留
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| 品种的可视性判据数据源 | 读 `MVSCommodityRegistry` 的公开面 | ✅ `ThingsOf(MVSCommodityDef)` 公开，返回 `IReadOnlyList<ThingDef>` |
| `ThingDef.techLevel` 存在与类型 | Mono.Cecil `type Verse.ThingDef full` | ✅ `[public] TechLevel techLevel` |
| §10 举例的关键反例 | **读游戏原文** `Core/Defs/ThingDefs_Items/Items_Resource_Manufactured.xml` | ⚠ `MedicineHerbal` 的 `techLevel = Neolithic` ⇒ 部落**会**看到「药品」，与 §10 举例不符（已上报） |
| 架构部对 techLevel 过滤的裁定 | 读 `MVSCommodityRegistry.cs:332-338` | ✅ "**下沉到消费侧**" —— 本批正是那个消费侧 |
| `Ultra` 是否含 `Archotech` | 单测（**自己抓到**） | ❌ 不含：`Ultra(6) < Archotech(7)` ⇒ 阶梯比较下够不到；§10「Ultra 全显示」需按"能力范围内"理解 |

### 回滚定位
1. **只退过滤**：`BuiltinEconomyProvider.MarketPanel.cs` 把 `row.NotInvolved` 恒置 `false` ⇒ 全品种可见（一行级）。
2. **整体退 T1a**：删 `CommodityVisibility` / `TechLevelBridge` / `FactionCommodityVisibility` 三文件，
   并把 `FactionProfileAdapter` 的枚举翻译还原为私有方法（`TechLevelBridge` 的注释里保留了完整论证）。
3. **运行时止损**：无 —— 纯展示层 + 只读索引查询，不触存档、不改任何游戏状态。
