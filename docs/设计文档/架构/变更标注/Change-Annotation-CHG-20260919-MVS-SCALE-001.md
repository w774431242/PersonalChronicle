---
change_id: CHG-20260919-MVS-SCALE-001
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/MVS/**, Source/PersonalChronicle/Application/**, Source/PersonalChronicle/Economy/UI/**, Defs/**, Languages/**, Tests/**, docs/**]
files:
  - path: Source/PersonalChronicle/MVS/Domain/FactionScale.cs
    type: A
    symbols: FactionScaleData / FactionScaleModel.TryNormalizeWeight / FactionScaleModel.EstimateHeadcount
    lines: 1-160
  - path: Source/PersonalChronicle/MVS/Data/MvsFactionScaleTuningDef.cs
    type: A
    symbols: MvsFactionScaleTuningDef / ConfigErrors
    lines: 1-80
  - path: Source/PersonalChronicle/MVS/Data/FactionScaleReader.cs
    type: A
    symbols: FactionScaleReader.TryRead / GroupWeightOf / MaxGroupWeight / CountSettlements / TuningK
    lines: 1-200
  - path: Source/PersonalChronicle/MVS/Application/Adapters/FactionScaleAdapter.cs
    type: A
    symbols: FactionScaleAdapter..ctor / TryGetScale
    lines: 1-50
  - path: Source/PersonalChronicle/MVS/Application/Contracts/IMarketFactorProvider.cs
    type: M
    symbols: IFactionScaleProvider
    lines: 53-82
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/MarketContracts.cs
    type: M
    symbols: MarketContracts.scale / Scale / IsWired / Wire
    lines: 28-95
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.Society.cs
    type: M
    symbols: BuildSociety（规模块）/ 移除 CountFactionSettlements
    lines: 1-200
  - path: Source/PersonalChronicle/Application/ReadModels/EconomySocietySnapshot.cs
    type: M
    symbols: EconomySocietySnapshot.HeadcountLabel / HeadcountValue / PopulationSectionTitle（文档）
    lines: 31-90
  - path: Source/PersonalChronicle/Economy/UI/EconomySocietyPanel.cs
    type: M
    symbols: ScaleRow / HeadcountRow / BuildPlan / DrawPlan
    lines: 45-260
  - path: Defs/MVS_ScaleTuning.xml
    type: A
    symbols: MVS_FactionScaleTuning
    lines: 1-35
  - path: Languages/ChineseSimplified/DefInjected/MvsFactionScaleTuningDef/MVS_ScaleTuning.xml
    type: A
    symbols: MVS_FactionScaleTuning.label / .description
    lines: 1-8
  - path: Languages/English/DefInjected/MvsFactionScaleTuningDef/MVS_ScaleTuning.xml
    type: A
    symbols: MVS_FactionScaleTuning.label / .description
    lines: 1-9
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: MVS.Economy.Society.Scale.HeadcountLabel / HeadcountValue
    lines: 200-210
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: MVS.Economy.Society.Scale.HeadcountLabel / HeadcountValue
    lines: 193-203
  - path: Tests/FactionScaleModelTests.cs
    type: A
    symbols: FactionScaleModelTests（13 例）
    lines: 1-140
  - path: Tests/MvsAdapterContractTests.cs
    type: M
    symbols: FactionScaleAdapter_HasNoStaticMutableState / Adapters_AllImplementTheirDeclaredContracts / Adapters_HaveWorldTakingConstructor / FactionScaleAdapter_NullWorld_EmptyName_IsNotAvailable
    lines: 33-130
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/REQ_派系画像人数因子接入.md
    type: A
    symbols: REQ-MVS-001~004
    lines: 1-160
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/SDD_派系画像人数因子接入.md
    type: A
    symbols: SDD（Draft v0.2）
    lines: 1-260
  - path: docs/changelog/v1.4.x_2026-09-19_派系规模人数模型落地.md
    type: A
    symbols: CHG-20260919-MVS-SCALE-001
    lines: 1-140
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-SCALE-001.md
    type: A
    symbols: CHG-20260919-MVS-SCALE-001
    lines: 1-160
---

# 变更标注 · CHG-20260919-MVS-SCALE-001（派系规模人数模型落地）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-MVS-SCALE-001`
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-MVS-SCALE-001
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent；落地对象是**用户此前已拍板**的上游设计 §7，SDD 为转写）
- Scope(摘要): 派系运行时规模读数（据点真值 + ③ 估算人数）落地并接入社会背景面板

### 一句话动机
用户要求「拟定社会背景面板人数因子接入方案」并指示继续。
`方案/市场经济框架_派系建模扩展设计方案.md` **§7 已拍板**（人数锚＝方案 A），
而实现状态是「游戏内 C# 仍待实现」⇒ 本批把它**逐条翻译成代码**，未新增设计口径。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `MVS/Domain/FactionScale.cs` | A | `FactionScaleData` · `FactionScaleModel.TryNormalizeWeight/EstimateHeadcount` | 1-160 | 人数估算**纯函数**（不认识 Verse ⇒ 可离线单测）；除零守卫、越界收口、上限钳制 | GOV-004 / PERF-001 | ARCH / TEST |
| `MVS/Data/MvsFactionScaleTuningDef.cs` | A | `MvsFactionScaleTuningDef` · `ConfigErrors` | 1-80 | K 与偏置限幅的 **Def**（GOV-006：③ 假设不得写成代码常量）+ 启动期自检 | GOV-006 / DATA-002 | DATA |
| `MVS/Data/FactionScaleReader.cs` | A | `TryRead` · `GroupWeightOf` · `MaxGroupWeight` · `CountSettlements` · `TuningK` | 1-200 | 读 `pawnGroupMakers.commonality` 与 `Find.WorldObjects.Settlements`；**刻意无缓存**（响应式） | DATA-007 / DATA-009 / PERF-001 | DATA / PERF |
| `MVS/Application/Adapters/FactionScaleAdapter.cs` | A | `ctor(World)` · `TryGetScale` | 1-50 | 薄适配器 —— 避免 Data 反向实现 Application 接口（GATE-ARCH 硬线） | ARC-006 / GOV-004 | ARCH / DATA |
| `MVS/Application/Contracts/IMarketFactorProvider.cs` | M | `IFactionScaleProvider` | 53-82 | 新契约。**不并进** `IFactionProfileProvider`：Def 静态 vs 世界运行时，生命周期不同（DATA-001 边界） | DATA-001 / ARC-006 | ARCH / DATA |
| `Application/SystemCenters/Builtin/MarketContracts.cs` | M | `scale` · `Scale` · `Wire` | 28-95 | 组合根装配新适配器（第 5 个契约） | ARC-006 | ARCH |
| `.../BuiltinEconomyProvider.Society.cs` | M | `BuildSociety` 规模块 · 移除 `CountFactionSettlements` | 1-200 | 规模块改走 `IFactionScaleProvider`；据点计数**整体移除**并收敛到读取器（消除同功能两处实现） | GOV-005 / DATA-007 | DATA / REL |
| `Application/ReadModels/EconomySocietySnapshot.cs` | M | `HeadcountLabel` · `HeadcountValue` · `PopulationSectionTitle`（文档） | 31-90 | 新增两字段承载人数行；③ 标记由 Keyed 承载的说明 | UI-001 / DATA-001 | UI |
| `Economy/UI/EconomySocietyPanel.cs` | M | `ScaleRow` · `HeadcountRow` · `BuildPlan` · `DrawPlan` | 45-260 | 规模段改两行；**计划阶段判空**（内容高必须在 BeginScrollView 前定）；人数行走 Dim 降调 | UI-001 / UI-004 | UI |
| `Defs/MVS_ScaleTuning.xml` | A | `MVS_FactionScaleTuning` | 1-35 | K=71.1（③，标定过程写在 XML 注释里）；偏置限幅 | GOV-006 / DATA-002 | DATA |
| `Languages/*/DefInjected/MvsFactionScaleTuningDef/MVS_ScaleTuning.xml` | A | `.label` · `.description` | 1-9 | 中英双份 DefInjected（目录名 = C# 类短名） | GOV-010 / LOC-002 | LOC |
| `Languages/*/Keyed/MVS.xml` | M | `Society.Scale.HeadcountLabel / HeadcountValue` | ~200 | 中英双份各 +2 键（键集合 **314 = 314**） | GOV-010 / LOC-001 | LOC |
| `Tests/FactionScaleModelTests.cs` | A | 13 例 | 1-140 | 含**头号断言**「权重不可用 ⟹ 判 false（而非返回 0）」与标定靶复现 | GOV-011 / PERF-001 | TEST |
| `Tests/MvsAdapterContractTests.cs` | M | 4 处 | 33-130 | 把新适配器纳入既有架构红线测试（无静态可变状态 / 实现契约 / World ctor / null 降级） | GOV-011 / ARC-008 | TEST / ARCH |
| `docs/设计文档/功能模块/经济体系/市场经济框架/REQ_派系画像人数因子接入.md` | A | `REQ-MVS-001`~`004` | 1-160 | Change Intent（Stage 0）+ DoR 自检 + **3 个待裁决点** | REQ-001~003 | REQ |
| `docs/设计文档/功能模块/经济体系/市场经济框架/SDD_派系画像人数因子接入.md` | A | Draft v0.2 | 1-260 | 12 节完整 SDD；含实现期两处修订与**程序性偏离登记** | SDD-002 / SDD-009 | SDD |
| `docs/changelog/v1.4.x_2026-09-19_派系规模人数模型落地.md` | A | `CHG-20260919-MVS-SCALE-001` | 1-140 | 更新日志：依据 / 设计决策 / 未做项 / **K 的已知弱点** / **发布区事故与修复** | REL-001 / REL-004 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-SCALE-001.md` | A | `CHG-20260919-MVS-SCALE-001` | 1-160 | 本文件自身（标注须覆盖**全部**改动文件，含自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步
> （MD5 `AED005B8301024191AB92387D79F5EA0`，两区一致）。

### 关键 diff 片段

```diff
// BuiltinEconomyProvider.Society.cs —— 规模块改走契约（并移除本地计数）
- int settlements = CountFactionSettlements(selectedFactionDefName);
- snapshot.PopulationLabel = T("MVS.Economy.Society.Scale.Label");
- snapshot.PopulationValue = TS1("MVS.Economy.Society.Scale.Value", settlements.ToString("N0", ...));
+ FactionScaleData scale;
+ if (MarketContracts.Scale != null
+     && MarketContracts.Scale.TryGetScale(selectedFactionDefName, out scale))
+ {
+     snapshot.PopulationLabel = T("MVS.Economy.Society.Scale.Label");
+     snapshot.PopulationValue = TS1("MVS.Economy.Society.Scale.Value", ...scale.SettlementCount...);
+     if (scale.HasWeightData)   // ⚠ 不可用 ⇒ 不画该行（**不是**画 0）
+     {
+         snapshot.HeadcountLabel = T("MVS.Economy.Society.Scale.HeadcountLabel");
+         snapshot.HeadcountValue = TS1("MVS.Economy.Society.Scale.HeadcountValue", ...);
+     }
+ }
```

```diff
// EconomySocietyPanel.cs —— 计划阶段判空（内容高必须在 BeginScrollView 之前定）
- Plan(BlockDataRow, 0, EconomyPanelChrome.RowHeight);
+ if (!string.IsNullOrEmpty(snapshot.PopulationValue)) { Plan(BlockDataRow, ScaleRow, ...); }
+ if (!string.IsNullOrEmpty(snapshot.HeadcountValue))  { Plan(BlockDataRow, HeadcountRow, ...); }
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏（生成脚本自检 declared = hit）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— 004~012 批次遗留文件由 `-ExcludeFromFile` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] 无调试代码残留 / 死代码（**已显式移除** `CountFactionSettlements` 并**不留注释掉的旧签名**）
- [x] 无临时文件残留（`_verify_tmp/` 为 Cecil 探针产物，已登记待清）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| `FactionDef.pawnGroupMakers` 存在与类型 | Mono.Cecil `type RimWorld.FactionDef full` | ✅ `List<PawnGroupMaker>` |
| 权重字段名 | `type RimWorld.PawnGroupMaker full` | ✅ `public float commonality`（**不是** `weight`） |
| 据点计数 API | 前批已证 | ✅ `WorldObjectsHolder.Settlements`（在 `RimWorld.Planet`）；`WorldObject.Faction` 可空 ⇒ 逐项守卫 |
| 现有数据契约无冲突 | 读 `EconomySocietySnapshot` 全字段 | ✅ 复用既有 `Population*`，仅新增 2 字段（向后兼容） |
| 新适配器纳入架构红线 | 读 `MvsAdapterContractTests` 的硬编码列表 | ✅ 已把 `FactionScaleAdapter` 加入 4 处断言 |

### ⚠ 本批登记的两项风险（详见 Changelog）
1. **K 的已知弱点**：归一化分母取「全体派系最大权重」（照 §7 字面），故 K 依赖
   「帝国是权重最大派系」这一**未核实前提** ⇒ 必须实机抽查 ≥2 个不同 techLevel 派系。
2. **发布区事故（已修复）**：`Copy-Item <dir> <existing-dir> -Recurse` 会把源目录**复制为子目录**，
   产生 `Defs/Defs/` ⇒ RimWorld **递归扫描** Defs ⇒ **每个 Def 重复加载 ⇒ 启动报重复 defName**。
   已即时删除并改用逐文件复制，复核 `same=76 diff_or_missing=0`。

### 回滚定位
1. **只退人数行**：`BuiltinEconomyProvider.Society.cs` 删 `if (scale.HasWeightData){...}` 块 ⇒ 回落为纯据点行。
2. **整体退 T2**：`MarketContracts.Scale` 回 `null` 即可（Provider 已判空）⇒ 规模段自动不画；再删 Def/契约/适配器。
3. **运行时止损**：无 —— 纯展示层 + 只读读取，不触存档、不改任何游戏状态。
