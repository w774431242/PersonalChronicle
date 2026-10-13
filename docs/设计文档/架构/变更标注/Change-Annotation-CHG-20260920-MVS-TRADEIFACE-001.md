---
change_id: CHG-20260920-MVS-TRADEIFACE-001
version: v1.4.x
date: 2026-09-20
author: AI（主 Agent）
scope: [Source/PersonalChronicle/MVS/**, Source/PersonalChronicle/Application/SystemCenters/**, Languages/**, Tests/**, docs/**]
files:
  - path: Source/PersonalChronicle/MVS/Domain/FactionPurchase.cs
    type: M
    symbols: TradeInterfaceMatch.AnyMemberIn（+）
    lines: 1-45
  - path: Source/PersonalChronicle/MVS/Data/SettlementTradeReader.cs
    type: M
    symbols: TryResolveFaction（+） / HandledThingsOf（+）
    lines: 95-185
  - path: Source/PersonalChronicle/MVS/Data/FactionCommodityVisibility.cs
    type: M
    symbols: IsTraded（+，并列保留 IsVisible）
    lines: 30-75
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs
    type: M
    symbols: 面板参与判据（:133） / AppendDynamicMarketTraits 参与判据（:326，已提循环外） / SubText 降级说明
    lines: 110-135, 218-232, 335-360
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: Market.Sub.FallbackTech（+1）
    lines: 250-262
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: Market.Sub.FallbackTech（+1）
    lines: 243-252
  - path: Tests/TradeInterfaceMatchTests.cs
    type: A
    symbols: TradeInterfaceMatchTests（5 例）
    lines: 1-90
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/SDD_市场面板真实贸易接口驱动.md
    type: A
    symbols: Draft → Approved v0.2（含实施期三处修订）
    lines: 1-200
  - path: docs/changelog/v1.4.x_2026-09-20_市场面板真实贸易接口.md
    type: A
    symbols: CHG-20260920-MVS-TRADEIFACE-001
    lines: 1-70
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260920-MVS-TRADEIFACE-001.md
    type: A
    symbols: 本文件（自指一条）
    lines: 1-80
---

# 变更标注 · CHG-20260920-MVS-TRADEIFACE-001（市场面板真实贸易接口 · C 步）

**Change ID：** `CHG-20260920-MVS-TRADEIFACE-001` · **Version：** v1.4.x · **Date：** 2026-09-20
**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物 · **上游：** SDD v0.2 **Approved**（A=(a) B=(a) C=(a)）

## Change Annotation
- 用户「按推荐进行」⇒ 实现 **C 步**：市场面板「参与判据」由 techLevel 换成**该派系据点的真实贸易接口**
- Scope(摘要): Domain 纯判据 · Data 经手集（并集）· 市场面板 2 处调用点 + 降级说明 · 本地化 +1 · 测试 +5

### 改动清单
| 文件 | 类型 | 涉及符号 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|
| `MVS/Domain/FactionPurchase.cs` | M | `TradeInterfaceMatch.AnyMemberIn` | 纯判据（存在即参与）+ **fail-open** 保留 | DATA-001/002 | DATA |
| `MVS/Data/SettlementTradeReader.cs` | M | `TryResolveFaction` / `HandledThingsOf` | 派系解析**单点实现**；经手集并集，**空集归一成 null** | DATA-007 / ARC-006 | DATA / ARCH |
| `MVS/Data/FactionCommodityVisibility.cs` | M | `IsTraded` | 新增**并列**判据（旧 `IsVisible` 保留，零破坏） | DATA-001 / GOV-004 | DATA |
| `.../BuiltinEconomyProvider.MarketPanel.cs` | M | 2 处参与判据 + `SubText` | 真判据优先；不可用 ⇒ 退回科技档 **+ 副标说明**；⚠ 并集**提到循环外** | DATA-007 / PERF-001 / LAND-009 | DATA / PERF / UI |
| `Languages/*/Keyed/MVS.xml` | M | `Market.Sub.FallbackTech` | +1 键 ×2 语言 | GOV-010 / LOC-001 | LOC |
| `Tests/TradeInterfaceMatchTests.cs` | A | 5 例 | 命中/未命中/空集/fail-open/空名 | TEST | TEST |
| `docs/.../SDD_市场面板真实贸易接口驱动.md` | A | v0.2 | **Draft → Approved**；含实施期三处修订 | SDD-004/009 | REL |
| `docs/changelog/v1.4.x_2026-09-20_市场面板真实贸易接口.md` | A | `CHG-...-TRADEIFACE-001` | 更新日志 | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260920-MVS-TRADEIFACE-001.md` | A | 本文件 | 自指一条 | ANNO-001 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步。

### 关键内容片段
```diff
// 市场面板：判据替换（**两处必须同时改**）
- row.NotInvolved = !FactionCommodityVisibility.IsVisible(commodity, profile.TechLevel);
+ row.NotInvolved = usesRealInterface
+     ? !FactionCommodityVisibility.IsTraded(commodity, handledThings)
+     : !FactionCommodityVisibility.IsVisible(commodity, profile.TechLevel);   // 降级
…
+ if (!usesRealInterface) { snapshot.SubText += " " + T("MVS.Economy.Market.Sub.FallbackTech"); }
```

```diff
// ⚠ 循环不变量提到循环外（我第一版写在循环内 ⇒ 每品种重算一次据点并集）
+ HashSet<string> handledForTotals = SettlementTradeReader.HandledThingsOf(factionDefName);
  for (int i = 0; i < commodities.Count; i++) { … bool involvedForTotals = handledForTotals != null ? … : …; }
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件（本批 **10** 个）
- [x] 清单范围 ⊆ Change Summary.Scope
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Change Summary、Changelog、SDD 互链一致
- [x] 无临时文件残留（本轮**未创建**临时脚本 —— 清单直接内联于本次命令，风险更低）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| `IsVisible` 有几个调用点 | `search_content "FactionCommodityVisibility\.\|IsVisible\("` 全工程 | 🔴 **2 处**（`MarketPanel.cs:133` + **`:326`**）—— **推翻我"1 处"的说法** |
| `:326` 所在方法能否取到派系 | 读方法签名 | ✅ 是 **static** 且**已收到 `factionDefName` 参数** ⇒ 用参数（`selectedFactionDefName` 会 CS0120） |
| `WillTrade` 的语义 | 早前 IL（`stockGenerators` → `HandlesThingDef`） | ✅ 「经手」⇒ 用作**参与判据**恰当 |
| 供给侧有无静态判据 | 早前 `TraderKindDef` / `StockGenerator` 类型面 | ❌ 无（`GenerateThings` 动态）⇒ **如实登记，不在本步承诺** |

### ⚠ 本批**未做**（按批准 A=(a)）
A 步（行集合换成具体物品）· `NotInvolved` 三态化 · 供给侧真实化。
**运行时路径单测**：`HandledThingsOf` / `IsTraded` 依赖 `Find.WorldObjects` / `DefDatabase` ⇒ **未写**（SDD §9 已登记边界）。

### 回滚定位
1. **只回判据**：两处调用点改回 `IsVisible(...)`，删 `IsTraded` / `HandledThingsOf` / `TradeInterfaceMatch`。
2. **只回副标**：删 `SubText` 追加段（Keyed 键保留）。
3. **运行时止损**：无 —— **零 Schema**、不改游戏状态；表现面仅"不涉及"列与副标。
