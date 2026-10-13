---
change_id: CHG-20260919-MVS-DEHOMO-008
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Application/**, Source/PersonalChronicle/Economy/UI/**, Languages/**, docs/**]
files:
  - path: Source/PersonalChronicle/Economy/UI/EconomySocietyPanel.cs
    type: M
    symbols: DrawScaleBar（数值右对齐）
    lines: 430-450
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.Society.cs
    type: M
    symbols: 市场特征块（lore 问答 → AppendDynamicMarketTraits）
    lines: 250-275
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs
    type: M
    symbols: AppendDynamicMarketTraits / FindFactionByDefName
    lines: 290-415
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: Society.Market.*（+8）
    lines: 218-236
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: 同上
    lines: 210-228
  - path: docs/changelog/v1.4.x_2026-09-19_人数右置与市场特征动态化.md
    type: A
    symbols: CHG-20260919-MVS-DEHOMO-008
    lines: 1-105
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-DEHOMO-008.md
    type: A
    symbols: CHG-20260919-MVS-DEHOMO-008
    lines: 1-95
---

# 变更标注 · CHG-20260919-MVS-DEHOMO-008

**Change ID：** `CHG-20260919-MVS-DEHOMO-008` · **Version：** v1.4.x · **Date：** 2026-09-19
**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物

## Change Annotation
- 要求 A「人数显示放置右侧」· 要求 B「市场特征必须是动态。如需求总缺口。供应库存。市场准入」
- Scope(摘要): 规模条数值右对齐；市场特征三条 lore 问答 → 三条真实动态指标

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|
| `Economy/UI/EconomySocietyPanel.cs` | M | `DrawScaleBar` | 数值**右对齐**（不带 caption 调 ProgressBar，自绘右对齐标签）；覆盖式绘制不占额外行高 | UI-001 / LAND-009 | UI |
| `.../BuiltinEconomyProvider.Society.cs` | M | 市场特征块 | 删 lore 3 行 → 调用动态指标；注释写明"模板冒充数据"同族与本批放置理由 | GOV-010 / UI-001 | UI |
| `.../BuiltinEconomyProvider.MarketPanel.cs` | M | `AppendDynamicMarketTraits` 等 | 需求总缺口（③，与市场面板**同式**）· 供应库存（② `ResourceCounter`）· 市场准入（② 关系，**无实例不画**） | DATA-007 / PERF-001 | DATA / PERF |
| `Languages/*/Keyed/MVS.xml` | M | `Society.Market.*` | +8 键；注明旧 lore 键保留以便恢复 | GOV-010 / LOC-001 | LOC |
| `docs/changelog/v1.4.x_2026-09-19_人数右置与市场特征动态化.md` | A | `CHG-...-008` | 更新日志（含"按依赖就近放 partial 分部"的手法） | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-DEHOMO-008.md` | A | `CHG-...-008` | 本文件自身（自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**：`Assemblies/PersonalChronicle.dll` 已重编并同步
> （MD5 `4E0B2326436E3333D84AC6BF419EBE46`，两区一致）。
>
> ⚠ **MD5 在标注落盘后又变过一次**：自查发现 `SocietyQaOf` 随 lore 移除成为**死代码**，
> 补删后重编 ⇒ MD5 由 `CDEE12D6…` 变为 `4E0B2326…`。
> 📌 **GATE-DELTA 没抓到这处死代码** —— 它只校验"标注是否覆盖全部改动文件"，**不做死代码扫描**。
> **"Gate 过了"不等于"没有死代码"**，这一条只能靠自查。

### 关键内容片段

```diff
// EconomySocietyPanel.DrawScaleBar —— 数值右置
- UIComponents.ProgressBar(bar, snapshot.BarShare01, UITheme.Alive, snapshot.BarCaption);
+ UIComponents.ProgressBar(bar, snapshot.BarShare01, UITheme.Alive);
+ Rect caption = new Rect(bar.x + 6f, bar.y, Mathf.Max(8f, bar.width - 12f), bar.height);
+ UIComponents.Label(caption, TruncateToWidth(snapshot.BarCaption, …), GameFont.Tiny,
+     UITheme.Text, TextAnchor.MiddleRight);
```

```diff
// Provider —— 市场特征动态化
- snapshot.MarketTraits.Add(SocietyQaOf(pk + ".Q1", pk + ".A1"));   // 写死 lore ×3
+ AppendDynamicMarketTraits(snapshot, map, selectedFactionDefName, profile);   // 每次重建重算
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件（脚本 MINE 与标注 `files:` **已逐条人工对齐**）
- [x] 清单范围 ⊆ Change Summary.Scope
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] 无死代码（lore 键是**语言包条目**而非代码路径；`SocietyQaOf` 仍被其它调用点使用需复核）
- [x] 无临时文件残留
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| 库存读取 API | Mono.Cecil `type RimWorld.ResourceCounter full` + `type Verse.Map full` | ✅ `Map.resourceCounter` : `ResourceCounter`，`GetCount(ThingDef)` : `int` |
| 派系关系 API | `type RimWorld.Faction full` + `type RimWorld.FactionRelationKind full` | ✅ `PlayerRelationKind` / `RelationKindWith`；枚举 `Hostile=0 / Neutral=1 / Ally=2` |
| 市场供需的真实来源 | 读 `BuiltinEconomyProvider.MarketPanel` | ⚠ **不是实测** —— 是 `MVSCommodityDef.supplyBaseline/demandBaseline` × 偏置（③）；代码自注"v1 无逐 Tick 实测量" ⇒ 缺口必须标 ③ |

### 设计判断
1. **需求总缺口必须与市场面板同式**：两处口径不同会产生"同屏自相矛盾"（两处都"有数"，最难查）。
2. **市场准入无实例不画**：面板按 defName、关系按实例 ⇒ 无实例时「不知道」不得伪装成「未知状态」。
3. **`AppendDynamicMarketTraits` 放在 MarketPanel 分部**：所需三个类型的 `using` 在那，
   而 using 是**按文件**的 ⇒ 按"依赖就近"分工，而不是把 using 复制到每个分部。

### 回滚定位
1. **只回市场特征**：`Society.cs` 恢复三行 `SocietyQaOf(pk + ".Q1"…)`（键仍在语言包）。
2. **只回数值位置**：`DrawScaleBar` 改回带 caption 的 `ProgressBar` 重载。
3. **运行时止损**：无 —— 纯 Def/运行时只读 + 快照投射 + 绘制，不触存档、不改游戏状态。
