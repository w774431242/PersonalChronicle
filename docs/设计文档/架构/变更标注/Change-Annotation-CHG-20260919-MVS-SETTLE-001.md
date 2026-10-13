---
change_id: CHG-MVS-SETTLE-001
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/MVS/**, Source/PersonalChronicle/ChronicleMod.cs, Tests/MarketShockModelTests.cs, Patches/MVS_IncidentImpacts.xml, Languages/**, docs/**]
files:
  - path: Source/PersonalChronicle/MVS/Domain/MarketShockModel.cs
    type: A
    symbols: MarketShockModel.Delta / MarketShockModel.Decay / MarketShockModel.Step / MarketShockModel.EffectiveDemand / MarketShockModel.TargetFactor / MarketShockModel.IsNeutral
    lines: 1-232
  - path: Source/PersonalChronicle/MVS/Data/WorldComponent_Market.Settlement.cs
    type: A
    symbols: WorldComponent_Market.PendingShock / WorldComponent_Market.EnqueueShock / WorldComponent_Market.WorldComponentTick / WorldComponent_Market.SettleMarket / WorldComponent_Market.PendingShockCount / WorldComponent_Market.PendingShockDropped / WorldComponent_Market.SettleSteps
    lines: 1-421
  - path: Tests/MarketShockModelTests.cs
    type: A
    symbols: MarketShockModelTests
    lines: 1-437
  - path: Source/PersonalChronicle/MVS/Domain/MvsConstants.cs
    type: M
    symbols: MvsConstants.ShockCap / MvsConstants.FactorNeutralEpsilon / MvsConstants.SettlementIntervalTicks / MvsConstants.ShockHalfLifeDays / MvsConstants.ShockOffsetBound / MvsConstants.ShockNeutralEpsilon / MvsConstants.PendingShockQueueCapacity / MvsConstants.MaxSettlementDeltaTicks
    lines: 45-75,120-207
  - path: Source/PersonalChronicle/MVS/Data/WorldComponent_Market.cs
    type: M
    symbols: WorldComponent_Market / WorldComponent_Market.CurrentSchemaVersion / WorldComponent_Market.shockOffsets / WorldComponent_Market.ExposeData / WorldComponent_Market.MigrateIfNeeded / WorldComponent_Market.PruneInvalidShockOffsets / WorldComponent_Market.SetFactor
    lines: 32-43,86-118,196-200,236-260,375-400,455-505
  - path: Source/PersonalChronicle/MVS/Data/MVSCommodityDef.cs
    type: M
    symbols: MVSIncidentImpactDef.commodities / MVSIncidentImpactDef.Validate
    lines: 160-215
  - path: Source/PersonalChronicle/MVS/Application/MarketEventSink.cs
    type: M
    symbols: MarketEventSink.RecordCore
    lines: 106-127
  - path: Source/PersonalChronicle/ChronicleMod.cs
    type: M
    symbols: ChronicleStartup.ValidateMvsIncidentImpacts / ChronicleStartup.ValidateMvsAssets
    lines: 254-372
  - path: Patches/MVS_IncidentImpacts.xml
    type: M
    symbols: MVSIncidentImpactDef:commodities / MVS.Inv.Category.ThreatBig / MVS.Inv.Category.Infestation / MVS.Inv.Category.Manhunter / MVS.Inv.Category.Caravan / MVS.Inv.Category.ColdSnap / MVS.Inv.Category.HeatWave / MVS.Inv.Category.Drought / MVS.Inv.Category.ToxicFallout / MVS.Inv.Category.Frenzied / MVS.Inv.Category.Trade / MVS.Inv.Category.OrbitalTrade
    lines: 17-55,60-300
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: MVS.Log.InvalidShockPruned / MVS.Log.ShockQueueOverflow / MVS.Log.SettleGated / MVS.Log.SettlementFailed
    lines: 98-101
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: MVS.Log.InvalidShockPruned / MVS.Log.ShockQueueOverflow / MVS.Log.SettleGated / MVS.Log.SettlementFailed
    lines: 93-96
  - path: docs/设计文档/方案/市场经济框架_v1设计方案.md
    type: M
    symbols: §3 分层修订 / §5.3 Hook 面登记 / §6 标题 / §6.3 情形⑦⑧ / §6.4 必落盘·必不落盘 / §9.1 行7/行9/行17-21
    lines: 42-60,130-150,157,207-250,292-320
  - path: docs/设计文档/架构/EXC-2026-006.md
    type: M
    symbols: EXC-2026-006.Mitigation.3 / EXC-2026-006.Evidence / EXC-2026-006.变更记录
    lines: 24-45,55-62
  - path: docs/changelog/v1.4.x_2026-09-19_MVS结算器落地.md
    type: A
    symbols: CHG-MVS-SETTLE-001
    lines: 1-190
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-SETTLE-001.md
    type: A
    symbols: CHG-MVS-SETTLE-001
    lines: 1-180
---

> **行范围口径**：新增文件为「全文件」；修改文件为「**改动区域**」（非全文件跨度），
> 已按 `Get-Content -Encoding UTF8` 实测行数复核（含文件总长：
> `MarketShockModel` 232 / `…Settlement` 421 / `Tests` 437 / `MvsConstants` 207 /
> `WorldComponent_Market` **757** / `MVSCommodityDef` 323 / `MarketEventSink` 127 /
> `ChronicleMod` 577）。
> ⚠ **ARC-013 提示（本批新产生的压力，非本批缺陷）**：`WorldComponent_Market.cs` 已达 **757 行**，
> 距「单文件 ≤ 800」仅剩 43 行 ⇒ **下一轮只要再往主文件加字段/方法，就必须先切 partial**
> （可切出的最大块是其中的 `FactionInventoryEntry` 类，约 70 行；字段声明与 `ExposeData` 须按
> 铁律 6.3「契约锚点留主文件」保留）。

# 变更标注 · CHG-MVS-SETTLE-001（MVS 结算器落地）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-MVS-SETTLE-001`（贯穿 Intake → Summary → Annotation → Gate → Changelog）
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-MVS-SETTLE-001
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent，前置路由 → ARCH/DATA-SAVE/PERF 三份只读预审 → 实现）
- Scope(摘要): MVS 后端结算器 —— 「事件冲击 → 供需偏移 → 全球因子 → 原版 `MarketValue`」写入侧闭链（+ schema v2）

### 一句话动机
核查发现 MVS **骨架完备但传导链断裂**：`SetFactor` 零调用者、`globalFactors` 恒空 ⇒ `StatPart_MarketValue` 虽已挂载却**永远恒等变换** ⇒ 经济中心屏幕所有数字都来自 Def 静态基线，**没有一个是游戏行为产生的**。本批把这条链的写入侧接起来。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 / DefName / Key | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `Source/PersonalChronicle/MVS/Domain/MarketShockModel.cs` | A | `MarketShockModel.Delta / Decay / Step / EffectiveDemand / TargetFactor / IsNeutral` | 1-232 | 新建对数域冲击模型（纯函数、零 Verse）。核心不变式 `offset=0 ⟺ 因子 1.0`；**有意舍弃绝对供需比**（13/18 基线是③假设值，纳入即等于用拍脑袋数值给全部商品定价） | ARC-001 / GOV-003 / GOV-006 | ARCH / DATA |
| `Source/PersonalChronicle/MVS/Data/WorldComponent_Market.Settlement.cs` | A | `PendingShock` / `EnqueueShock` / `WorldComponentTick` / `SettleMarket` / `PendingShockCount` / `PendingShockDropped` / `SettleSteps` | 1-421 | 新建结算器 partial。环形队列（覆盖最旧+节流 Error）+ 每 tick 钩子自节流（首 tick 重锚 / 负 Δ 重锚 / 先锚定后结算）+ 遍历域 = 偏移∪因子并集（**非全库扫描**）+ 先衰减后加冲击 + 稳态零分配 + `PerfProbe("mvs.settle")` | ARC-003 / ARC-013 / PERF-001 / PERF-003 / GOV-012 | ARCH / PERF |
| `Tests/MarketShockModelTests.cs` | A | `MarketShockModelTests`（**30 例**） | 1-437 | 符号 / 量纲 / 定义域 / 半衰分段可加性 / 核心不变式 / 常量值域守卫 + 两个端到端纯函数仿真（含**收敛终止性**：防止结算器每 250t 永久空转） | AI-009 / TEST-001 | TEST |
| `Source/PersonalChronicle/MVS/Domain/MvsConstants.cs` | M | `ShockCap`（语义澄清）· `FactorNeutralEpsilon` · `SettlementIntervalTicks` · `ShockHalfLifeDays` · `ShockOffsetBound` · `ShockNeutralEpsilon` · `PendingShockQueueCapacity` · `MaxSettlementDeltaTicks` | 45-75 / 120-210 | `ShockCap`「单 tick」→「**单结算步**」（值 0.02 未变）；新增 5 项常量（全部 GOV-006 登记 + 值域守卫）；`FactorNeutralEpsilon` 把散在 `SetFactor` 的字面量单源化 | GOV-006 / ARC-008 | ARCH / DATA |
| `Source/PersonalChronicle/MVS/Data/WorldComponent_Market.cs` | M | `WorldComponent_Market`（改 partial）· `CurrentSchemaVersion` 1→2 · 字段 `shockOffsets` · `ExposeData` · `MigrateIfNeeded` · `PruneInvalidShockOffsets` · `SetFactor` | 32-43 / 86-118 / 236-260 / 375-395 / 455-505 | v2：新增落盘 `shockOffsets`（Value/Value）+ `??=` 兜底 + 单调 if 链 v1→v2 迁移（**不产生数值**）+ 非法键值剪枝（**悬空 defName 键保留**，同孤儿裁定） | SAVE-001 / SAVE-002 / DATA-001 | DATA / SAVE |
| `Source/PersonalChronicle/MVS/Data/MVSCommodityDef.cs` | M | `MVSIncidentImpactDef.commodities` · `MVSIncidentImpactDef.Validate` | 150-215 | 新增**受影响品种轴**（Def 引用，非字符串）⇒ 免去 `category → 品种` 硬编码映射；`Validate()` 覆盖 null / 空 / 含 null 元素三形态 | DATA-004 / GOV-006 / GOV-008 | DATA / LOC |
| `Source/PersonalChronicle/MVS/Application/MarketEventSink.cs` | M | `MarketEventSink.RecordCore` | 106-128 | 去重门**之后**追加 O(1) 入队（`MarketAccess` → `EnqueueShock`）；依赖方向 Application → Data；补 `using ...Adapters` | ARC-004 / COMP-004 / GOV-007 | ARCH / COMP |
| `Source/PersonalChronicle/ChronicleMod.cs` | M | `ChronicleStartup.ValidateMvsIncidentImpacts`（新）· `ValidateMvsAssets`（调用点） | 254-375 | 接线此前**零调用点**的 `MVSIncidentImpactDef.Validate()`；断言白名单非空（`==0` ⇒ Error，整份补丁静默零命中的头号形态）；**不断言"恰好 11 项"**（DLC 差异） | DATA-004 / TEST-002 | DATA / TEST |
| `Patches/MVS_IncidentImpacts.xml` | M | 11 段 `<commodities>` + 文件头三条硬纪律 | 17-55 / 60-300 | 11 项全部补受影响品种；新增头注释：空列表 ⇒ **不产生冲击**（Fail-Closed）/ 元素可 null / **禁引用 MayRequire 品种**（否则把"玩家没装 DLC"误报成配置错误） | DATA-004 / GOV-008 | DATA / COMP |
| `Languages/ChineseSimplified/Keyed/MVS.xml` | M | 4 个 `MVS.Log.*` 键 | 98-101 | 新增日志键（中英对称）：`InvalidShockPruned` / `ShockQueueOverflow` / `SettleGated` / `SettlementFailed` | LOC-001 / GOV-010 | LOC |
| `Languages/English/Keyed/MVS.xml` | M | 4 个 `MVS.Log.*` 键 | 93-96 | 同上（英文侧） | LOC-001 / GOV-010 | LOC |
| `docs/设计文档/方案/市场经济框架_v1设计方案.md` | M | §3 / §5.3 / §6 标题 / §6.3 ⑦⑧ / §6.4 两表 / §9.1 行7·行9·行17-21 | 42-60 / 130-150 / 157 / 207-250 / 292-320 | §3 修订「Application = `MarketService`」与代码现实分叉（改「数学=Domain / 编排=Data / 采集与读=Application」）；§5.3 补 **Hook 面登记**（含 Cecil 调用链证据、说明非 Harmony）；§6.3 补⑦迁移⑧回滚；§6.4 补落盘判据与 grep 复验口径；§9.1 补 5 项新常量 | GOV-001 / ANNO-001 / SAVE-004 | ARCH / DATA / SAVE |
| `docs/设计文档/架构/EXC-2026-006.md` | M | `EXC-2026-006.Mitigation.3` / `Evidence` / `变更记录` | 24-45 / 55-62 | 「只读 Postfix、零 mutation」→「零直接 mutation + O(1) 入队（终将间接改写权威因子表）」；补风险面 / 双闸门 / **回滚说明**；Scope 与 Rule IDs 不变 | COMP-004 / EXC-002 / GOV-007 | COMP |
| `docs/changelog/v1.4.x_2026-09-19_MVS结算器落地.md` | A | `CHG-MVS-SETTLE-001` | 1-190 | 更新日志（含**条件性 Breaking** 回滚表 + 8 项残余风险诚实登记 + 变更文件清单） | REL-001 / REL-004 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-SETTLE-001.md` | A | `CHG-MVS-SETTLE-001` | 1-180 | 本文件自身（GATE-DELTA 要求标注覆盖**全部**实际改动文件，含标注自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步至发布区（MD5 `8A94602903B94C64925FEEC34F658F29`，两区一致），但因门禁改动集不含二进制，只能在标注正文说明 —— 写进 `files:` 会被判"git 无对应改动"。

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏 —— 脚本自检 `annotation declared = 14   hit in diff = 14`（**0 条 declared-but-not-in-git**）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— 范围外 903 条历史未提交工作由 `-ExcludeFromFile .codebuddy/tmp/mvs-settle-001-exclude.txt` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致（`CHG-MVS-SETTLE-001`）
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验 ——
      **输出：`GATE-DELTA: PASS — 标注覆盖 15 个改动文件，无遗漏/超范围/残留`（EXITCODE=0）**
      ⚠ **诚实声明**：`_scripts/check_annotation_parity.py` 对本批 **N/A** —— 该脚本的 `MINE`
      列表是**上一批硬编码**的（60 条 Economy UI 文件），对本批运行必然误报 phantom / uncovered。
      本批以 skill 指定的 GATE-DELTA 校验器 `gate-runner.ps1` 为准（已 PASS）。
      该脚本的通用化（改读本文件 YAML front matter 的 `files:`）**列为下轮 P3**，本轮不动以免超范围。

### 会话瞬态禁 Scribe 复验（GATE-SAVE 专项）
```
grep "Scribe_\w+\(.*(queue|Queue|pending|Pending|settle|Settle|shock|Shock)"
  → 全工程 0 命中
Scribe_Collections.Look(ref shockOffsets, "shockOffsets", ...) → 恰 1 处（:242）
MVS 全部 Scribe 调用点：schemaVersion / globalFactors / shockOffsets / inventory
                        + FactionInventoryEntry{loadID,factionDefName,amounts}  —— 全部为设计内字段
```

### 关键 diff 片段

```diff
// Source/PersonalChronicle/MVS/Data/WorldComponent_Market.Settlement.cs  （时序止损三纪律）
+ if (!settleAnchorSet) { settleAnchorSet = true; lastSettleTick = now; return; }  // 首 tick 只重锚
+ long delta = (long)now - lastSettleTick;
+ if (delta < 0L) { lastSettleTick = now; return; }                               // 时钟回退 ⇒ 重锚
+ if (delta < (long)MvsConstants.SettlementIntervalTicks) return;
+ lastSettleTick = now;                                                            // 先锚定，后结算
+ try { SettleMarket((int)delta); } catch (Exception ex) { /* 节流日志 */ }
```

```diff
// Source/PersonalChronicle/MVS/Data/WorldComponent_Market.Settlement.cs  （遍历域 = 并集，非全库扫描）
+ settleKeys.Clear();
+ foreach (KeyValuePair<string, float> kv in shockOffsets) { settleKeys.Add(kv.Key); }
+ foreach (KeyValuePair<string, float> kv in globalFactors)
+     if (!shockOffsets.ContainsKey(kv.Key)) { settleKeys.Add(kv.Key); }   // 去重：一步只推一次
```

```diff
// Source/PersonalChronicle/MVS/Application/MarketEventSink.cs  （入队在去重门之后）
+ WorldComponent_Market market = MarketAccess.TryGetComponent();
+ if (market != null) { market.EnqueueShock(impact, tick); }
```

```diff
// Source/PersonalChronicle/MVS/Data/WorldComponent_Market.cs  （v1 → v2 迁移：只 ??=，不产生数值）
+ if (schemaVersion < 2)
+ {
+     if (shockOffsets == null) { shockOffsets = new Dictionary<string, float>(); }
+     PruneInvalidShockOffsets();
+     schemaVersion = 2;
+     ChronicleLog.Save("[MVS][Save] 迁移至 schema v2（新增 shockOffsets 归一 + 非法键值剪枝，未产生任何数值）。");
+ }
```

### 回滚定位
1. **退回"零 mutation"旧形态**：删 `MarketEventSink.RecordCore` 末尾入队段即可（队列恒空 ⇒ 因子表恒空 ⇒ 价格通道恒等变换，无存档副作用）。
2. **退回 v1 schema**：需同时改 `CurrentSchemaVersion = 1`、移除 v1→v2 段与 `shockOffsets` 字段（**并保留一段只读兼容**，否则 v2 档会被误判并丢字段）。
3. **运行时止损**：`MvsConstants.SettlementIntervalTicks` 设为极大值（如 `int.MaxValue`）⇒ 结算步永不触发 ⇒ 等价于未上线。
