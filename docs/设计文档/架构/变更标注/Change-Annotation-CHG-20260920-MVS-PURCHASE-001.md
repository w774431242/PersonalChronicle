---
change_id: CHG-20260920-MVS-PURCHASE-001
version: v1.4.x
date: 2026-09-20
author: AI（主 Agent）
scope: [Source/PersonalChronicle/MVS/**, Source/PersonalChronicle/Capture/**, Tests/**, docs/**]
files:
  - path: Source/PersonalChronicle/MVS/Domain/FactionPurchase.cs
    type: A
    symbols: FactionPurchaseEntry / FactionPurchase.Aggregate / DefaultTopN
    lines: 1-150
  - path: Source/PersonalChronicle/MVS/Data/SettlementTradeReader.cs
    type: A
    symbols: PickTraderKind / PurchaseSetOf / TryReadFactionPurchase / purchaseCache / purchaseCacheSource
    lines: 1-215
  - path: Source/PersonalChronicle/Capture/LocationAtlasCapture.cs
    type: M
    symbols: ApplyCommerceSnapshot（两处清空残留 + 兜底不挑 [0]）
    lines: 518-575
  - path: Tests/FactionPurchaseTests.cs
    type: A
    symbols: FactionPurchaseTests（6 例）
    lines: 1-120
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/SDD_据点收购项与库存观测.md
    type: A
    symbols: Draft → Approved v0.2；§1.3 纠正框 / §4.1 改法 / §11 追踪矩阵 / Change Log
    lines: 1-240
  - path: Source/PersonalChronicle/MVS/Application/Contracts/IMarketFactorProvider.cs
    type: M
    symbols: TryGetFactionPurchase（+）
    lines: 70-110
  - path: Source/PersonalChronicle/MVS/Application/Adapters/FactionProfileAdapter.cs
    type: M
    symbols: TryGetFactionPurchase / FindFactionByDefName（+）
    lines: 62-135
  - path: Source/PersonalChronicle/Application/ReadModels/EconomySocietySnapshot.cs
    type: M
    symbols: PurchaseSectionTitle / PurchaseNote / PurchaseRows（+） / SocietyPurchaseRow（+）
    lines: 150-215
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.Society.cs
    type: M
    symbols: 收购项段填充（+）
    lines: 130-180
  - path: Source/PersonalChronicle/Economy/UI/EconomySocietyPanel.cs
    type: M
    symbols: BlockSectionPurchase / BlockPurchaseRow / BuildPlan / DrawPlan / DrawPurchaseRow（+）
    lines: 45-70, 240-270, 300-330, 400-480
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: Purchase.Title / Purchase.Note / Purchase.Count（+3）
    lines: 246-258
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: 同上（+3）
    lines: 239-251
  - path: docs/changelog/v1.4.x_2026-09-20_据点收购项数据侧.md
    type: A
    symbols: CHG-20260920-MVS-PURCHASE-001（含"追加：Application+UI 接线"段）
    lines: 1-120
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260920-MVS-PURCHASE-001.md
    type: A
    symbols: CHG-20260920-MVS-PURCHASE-001
    lines: 1-80
---

# 变更标注 · CHG-20260920-MVS-PURCHASE-001（据点收购项 · 批 1 数据侧）

**Change ID：** `CHG-20260920-MVS-PURCHASE-001` · **Version：** v1.4.x · **Date：** 2026-09-20
**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**上游：** `SDD_据点收购项与库存观测.md` **v0.2 Approved**（用户批准 **A=(a) B=(a) C=(a) D=(a)**）

## Change Annotation
- 用户「**按你推荐进行，批准执行**」⇒ 执行 **批 1 的数据侧**（`REQ-MVS-009` / `009a`）
- ⚠ **按分层拆分**（AI-014 允许的"架构分层隔离"）：Domain+Data+Capture 本轮；Application+UI 下一轮

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|
| `MVS/Domain/FactionPurchase.cs` | A | `FactionPurchase.Aggregate` | 并集 + 计数 + **全序**（平手按 defName）+ 长尾聚合 + 不可用返回空（**不兜底**） | DATA-001/002 | DATA |
| `MVS/Data/SettlementTradeReader.cs` | A | `PickTraderKind` / `PurchaseSetOf` / `TryReadFactionPurchase` | 与原版逐字同式选 kind；**L1 memo + Def 实例自校验**；会失败且**无兜底** | DATA-007 / PERF-001 | DATA / PERF |
| `Capture/LocationAtlasCapture.cs` | M | `ApplyCommerceSnapshot` | **两处清空残留三字段**；兜底路径**不再挑 `[0]`**（改为不填）+ `using` | DATA-001 / GOV-004 | DATA |
| `Tests/FactionPurchaseTests.cs` | A | 6 例 | 并集/计数 · 全序破平 · 长尾聚合 · 份额分母 · **降级不编造** · 空 defName 跳过 | TEST | TEST |
| `docs/.../SDD_据点收购项与库存观测.md` | A | v0.2 | **Draft → Approved**；🔴 **§1.3 纠正我夸大的缺陷范围**；§4.1 改法；§11 矩阵；Change Log | SDD-004/009 | REL |
| `docs/changelog/v1.4.x_2026-09-20_据点收购项数据侧.md` | A | `CHG-...-PURCHASE-001` | 更新日志（含"未做/未验证"诚实标记） | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260920-MVS-PURCHASE-001.md` | A | 本文件 | 自指一条 | ANNO-001 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll`
> 已重编并同步（MD5 `FE431BD37E3C53611E65885BE122134D`，两区一致）。
>
> **📌 本标注覆盖批 1 的**两半**（数据侧 + Application/UI 接线）—— 同一 `Change ID`，
> 因为它们是同一个可交付单元（AI-014）；按分层拆成两轮只是执行顺序，不是两个变更。

### 关键内容片段

```diff
// 采集：两处清空残留（修"陈旧值冒充当前值"）
- loc.CanTrade = false; return;                      // 三个字段留着上一次的值
+ loc.CanTrade = false;
+ loc.TraderKindDefName = null; loc.PermitRequiredDefName = null; loc.TradeKindKeys = new List<string>();
+ return;
…
- ApplyTraderKindSnapshot(loc, traderKinds[0]);       // ← 兜底路径挑第一个 = 编造
+ TraderKindDef kind = SettlementTradeReader.PickTraderKind(faction, null);   // 无据点上下文 ⇒ null
+ if (kind != null) { ApplyTraderKindSnapshot(loc, kind); } else { /* 三个字段一律清空 */ }
```

```diff
// Data：与原版逐字同式 + L1 自校验（不挂外部重载钩子）
+ int idx = Mathf.Abs(Gen.HashOffset(settlement)) % kinds.Count;
+ return kinds[idx];
…
+ if (purchaseCache.TryGetValue(kind.defName, out cached)
+     && purchaseCacheSource.TryGetValue(kind.defName, out source)
+     && ReferenceEquals(source, kind)) { return cached; }   // Def 实例变了即重算
```

### Application + UI 接线（批 1 后半，同 Change ID）

| 文件 | 类型 | 涉及符号 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|
| `MVS/Application/Contracts/IMarketFactorProvider.cs` | M | `TryGetFactionPurchase` | 新增契约方法（**会失败、无兜底**；注明**不得**放进绘制路径） | DATA-007 / PERF-001 | DATA / PERF |
| `MVS/Application/Adapters/FactionProfileAdapter.cs` | M | `TryGetFactionPurchase` / `FindFactionByDefName` | 实现（⚠ 需**派系实例**——据点归属实例，非 Def） | GOV-005 | ARCH |
| `Application/ReadModels/EconomySocietySnapshot.cs` | M | `PurchaseRows` / `SocietyPurchaseRow` | 新增段字段 + 行类型（**刻意不复用 `SocietyTier`**） | DATA-001 / UI-001 | DATA / UI |
| `.../BuiltinEconomyProvider.Society.cs` | M | 收购项段填充 | 不可用 ⇒ **整段不画**；聚合行 `RowIndex = -1` | GOV-010 / DATA-007 | DATA / LOC |
| `Economy/UI/EconomySocietyPanel.cs` | M | `BlockSectionPurchase` / `BlockPurchaseRow` / `DrawPurchaseRow` | 计划表判空跳过 · 行绘制（截断走 **4 参重载**；**聚合行不画条**） | UI-001 / LAND-009 | UI |
| `Languages/*/Keyed/MVS.xml` | M | `Purchase.*`（+3 ×2 语言） | 标题与注**都点明"会买"**（防误读为库存） | GOV-010 / LOC-001 | LOC |
| `docs/changelog/v1.4.x_2026-09-20_据点收购项数据侧.md` | A | 同 Change ID | 追加"Application+UI 接线"段 | REL-001 | REL |

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件（本批 **14** 个）
- [x] 清单范围 ⊆ Change Summary.Scope
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] 无临时文件残留（`_scripts/_tmp_*` 跑完即删）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| `traderKinds[0]` 的影响范围 | **读调用点** `LocationAtlasCapture.cs:416 / 465-471` | 🔴 **推翻我 v0.1 的说法**：据点路径走 `settlement.TraderKind`（精确）⇒ `[0]` **只在兜底路径** |
| 空列表分支是否清字段 | 读 `:533-541` 原文 | 🔴 确实**不清** ⇒ 真缺陷（已修） |
| 原版选 kind 的算法 | `il Planet.Settlement_TraderTracker get_TraderKind` | ✅ `\|HashOffset(settlement)\| % Count`，**无加权随机** |
| `WillTrade` 的判定落点 | `il RimWorld.TraderKindDef WillTrade` | ✅ 遍历 `stockGenerators` → `HandlesThingDef`（**推翻**我"走 tradeTags"的记忆） |
| `baseTraderKinds` 空时 `CanTrade` 语义 | 读代码 | ✅ 该派系**无贸易接口** ⇒ `false` 成立 |

### ⚠ 本批**未做**（显式登记）
- **Application / UI 接线**（SDD §6/§8）—— 下一轮；**本轮无用户可见变化**
- **`SettlementTradeReader` 的运行时路径单测**：依赖 `Find.WorldObjects` / `DefDatabase`
  ⇒ 属"需运行中游戏"，**未写**（SDD §10 已登记边界，**不伪造覆盖**）
- 批 2（库存观测落档）—— 按批准 **A=(a) 不启动**

### 回滚定位
1. **只回采集**：两处清空与 `PickTraderKind` 调用还原为 `traderKinds[0]`（**不建议** —— 那是真缺陷）。
2. **只回新功能**：删 `FactionPurchase.cs` / `SettlementTradeReader.cs` / 测试
   （`LocationAtlasCapture` 的 `using` 与调用点是唯一耦合，需一并回）。
3. **运行时止损**：无 —— 本批**不触存档**、不改游戏状态、**无可见行为变化**。
