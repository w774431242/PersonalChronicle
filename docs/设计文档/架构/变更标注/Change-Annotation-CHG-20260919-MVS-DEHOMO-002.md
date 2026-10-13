---
change_id: CHG-20260919-MVS-DEHOMO-002
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/MVS/**, Source/PersonalChronicle/Application/SystemCenters/**, Languages/**, Tests/**, docs/**]
files:
  - path: Source/PersonalChronicle/MVS/Domain/FactionTraits.cs
    type: A
    symbols: FactionTraitsData
    lines: 1-120
  - path: Source/PersonalChronicle/MVS/Domain/FactionArchetype.cs
    type: M
    symbols: FactionArchetypeData.Traits / Neutral / Make
    lines: 17-140
  - path: Source/PersonalChronicle/MVS/Data/FactionTraitsReader.cs
    type: A
    symbols: FactionTraitsReader.Read / CountOrZero / SanitizeWeight
    lines: 1-140
  - path: Source/PersonalChronicle/MVS/Application/Adapters/FactionProfileAdapter.cs
    type: M
    symbols: TryGetProfile（填充 profile.Traits）
    lines: 55-85
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.Society.cs
    type: M
    symbols: AppendDerivedChips（②/③ 分组 + 6 枚新 chip）
    lines: 150-215
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: Society.Trait.*（+6）/ Society.Chip.*（改 5）
    lines: 116-140
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: Society.Trait.*（+6）/ Society.Chip.*（改 5）
    lines: 108-132
  - path: Tests/FactionTraitsTests.cs
    type: A
    symbols: FactionTraitsTests（14 例）
    lines: 1-170
  - path: Tests/TradeBiasTests.cs
    type: M
    symbols: Profile（补 Traits 赋值）
    lines: 22-34
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/SDD_派系画像去同质化.md
    type: A
    symbols: SDD（Approved v0.2）
    lines: 1-240
  - path: docs/changelog/v1.4.x_2026-09-19_派系画像去同质化落地.md
    type: A
    symbols: CHG-20260919-MVS-DEHOMO-002
    lines: 1-140
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-DEHOMO-002.md
    type: A
    symbols: CHG-20260919-MVS-DEHOMO-002
    lines: 1-130
---

# 变更标注 · CHG-20260919-MVS-DEHOMO-002（派系画像去同质化 · 层 1+2 落地）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-MVS-DEHOMO-002`
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-MVS-DEHOMO-002
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent；方案由 `CHG-20260919-MVS-DEHOMO-001` 产出，用户 2026-09-19 批准 A=(a)、C）
- Scope(摘要): 画像新增结构性特征维度（② 真读数）+ 契约加字段 + 社会背景 chips 分标圈码

### 一句话动机
用户报「派系画像太少 ⇒ 社会背景同质化」。根因是**上游 §4 的 9 类输入只落地了 1 个（techLevel）**
⇒ 画像空间仅 7 档。本批把**不需要分类判断**的 4 类输入接进画像，并把 ②/③ 圈码**分组分标**。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `MVS/Domain/FactionTraits.cs` | A | `FactionTraitsData` | 1-120 | 8 个字段，**全部 ② 真读数**（计数/存在性/原值），**一条假设都不放**；与 ③ 系数分结构以支持分别标圈码 | DATA-001 / GOV-004 | ARCH / DATA |
| `MVS/Domain/FactionArchetype.cs` | M | `Traits` · `Neutral` · `Make` | 17-140 | 加 `Traits` 字段；两处逐字段构造点补赋值（**CS0165 是编译期拦截，不是运行期怪象**） | GOV-004 / DATA-001 | ARCH |
| `MVS/Data/FactionTraitsReader.cs` | A | `Read` · `CountOrZero` · `SanitizeWeight` | 1-140 | **只读、只计数、不折算**；null 列表守卫；非有限/负权重净化；**类尾逐条登记 3 类未做项及原因** | DATA-007 / PERF-001 | DATA / PERF |
| `MVS/Application/Adapters/FactionProfileAdapter.cs` | M | `TryGetProfile` | 55-85 | 填充 `profile.Traits`（Domain 不认识 Verse ⇒ 由 Data 填） | ARC-006 | ARCH |
| `.../BuiltinEconomyProvider.Society.cs` | M | `AppendDerivedChips` | 150-215 | chips **按圈码分两组**；新增 6 枚 ② chip；`leaderTitle` 为空则不画 | UI-001 / GOV-010 | UI / LOC |
| `Languages/*/Keyed/MVS.xml` | M | `Society.Trait.*` / `Society.Chip.*` | ~116 / ~108 | 中英各 +6 键、改 5 键（**圈码写进模板**）；键集 **322 = 322** | GOV-010 / LOC-001 | LOC |
| `Tests/FactionTraitsTests.cs` | A | 14 例 | 1-170 | **头号断言＝回归护栏**：四系数逐档穷举快照（层 1 改动即红）+ 区分度 + 不得编造 + 读取器逐字段 | GOV-011 / PERF-001 | TEST |
| `Tests/TradeBiasTests.cs` | M | `Profile` | 22-34 | 补 `p.Traits` 赋值（结构体含结构体字段 ⇒ 全字段必赋值） | GOV-011 | TEST |
| `docs/设计文档/.../SDD_派系画像去同质化.md` | A | Approved v0.2 | 1-240 | **Draft → Approved**；登记**两处实现期设计修订**（见下） | SDD-002 / SDD-009 | SDD |
| `docs/changelog/v1.4.x_2026-09-19_派系画像去同质化落地.md` | A | `CHG-20260919-MVS-DEHOMO-002` | 1-140 | 更新日志：翻案说明 / 新增维度表 / 未做项 / **事故与修复** | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-DEHOMO-002.md` | A | `CHG-20260919-MVS-DEHOMO-002` | 1-130 | 本文件自身（标注须覆盖全部改动文件，含自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步
> （MD5 `3DC94F2E2A2981389CE1618C50D4D554`，两区一致）。

### 关键内容片段

```diff
// FactionArchetype.cs —— 结构体加了结构体字段 ⇒ 所有逐字段构造点都必须补赋值
  private static FactionArchetypeData Make(...)
  {
      FactionArchetypeData data;
      data.TechLevel = level;
      ...
+     data.Traits = default(FactionTraitsData);   // 漏了 = CS0165（编译期拦截）
      return data;
  }
```

```diff
// BuiltinEconomyProvider.Society.cs —— chips 按圈码分两组
+ // ══ 第 1 组：② 官方推导（真读数）══
  snapshot.Chips.Add(TS1("MVS.Economy.Society.Chip.Tech", ...));
+ snapshot.Chips.Add(traits.TitleTagCount > 0 ? T1("...Trait.Titles", ...) : T("...Trait.NoTitles"));
+ if (!string.IsNullOrEmpty(traits.LeaderTitle)) { snapshot.Chips.Add(TS1("...Trait.Leader", ...)); }
+ snapshot.Chips.Add(T1("...Trait.Trade", 四类计数之和));
+ snapshot.Chips.Add(traits.ApparelStuffRestricted ? T1("...ApparelLimited", ...) : T("...ApparelOpen"));
+ // ══ 第 2 组：③ 设计假设 ══
  snapshot.Chips.Add(TS1("MVS.Economy.Society.Chip.Production", ...));
  ...
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏（生成脚本自检 declared = hit）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— 前序批次由 `-ExcludeFromFile` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] 无调试代码残留 / 死代码
- [x] 无临时文件残留
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| `ThingFilter` 读取面 | Mono.Cecil `type Verse.ThingFilter full` | ✅ `AllowedThingDefs` : `IEnumerable<ThingDef>`、`AllowedDefCount` : `int` |
| 四类 `*TraderKinds` 与治理字段 | `type RimWorld.FactionDef full` | ✅ `List<TraderKindDef>` ×4、`List<string> royalTitleTags`、`string leaderTitle`、`List<PawnKindDef> fixedLeaderKinds`、`float settlementGenerationWeight` |
| 测试能否访问 `internal` 读取器 | 检索 `InternalsVisibleTo` | ✅ `AssemblyInfo.Visibility.cs` 已有 `InternalsVisibleTo("PersonalChronicle.Tests")` ⇒ 读取器**可离线测** |
| 四个系数是否被本批改动 | **逐档穷举单测** | ✅ **未变**（8 档快照全过）⇒ T1a/T1b **零回归** |

### ⚠ 两处实现期设计修订（SDD-009 登记，已写进 SDD v0.2）
1. **新维度一律不进四个系数**（草稿曾设想"由多维输入合成"）——
   那会同时**污染 ③ 假设**（混血值无法标圈码）与**静默改变 T1a/T1b 行为**。改为**并列** ⇒ 两处问题都不存在。
2. **§4 的 3 类输入不做**（材料分类 / 人口构成 / 集团类型）—— 都需**分类判断**，
   而原版无稳定结构化依据（只能 defName 比对 = P1 红线所禁模式）。已在 `FactionTraitsReader` 类尾逐条登记。

### ⚠ 本批发生的一次事故（已修复，如实登记）
同步前单测出现 **1 失败**（`TempR8Measure.Measure_Content_vs_Viewport`，此前一直"已跳过"）。
根因**不在本批代码**：该既有取证工具的 **`[OneTimeSetUp]` 会往 `_verify_tmp\` 写文件**，
而该目录**被我在早先几轮当"临时目录"清理掉了**（还当作清理完成上报）。
⇒ 已重建目录，跑回 **443 通过 / 1 已跳过 / 0 失败**。
📌 **教训**：名字像临时目录的目录可能是**既有工具的运行前提**；删之前先检索谁在用。

### 回滚定位
1. **只退面板**：`AppendDerivedChips` 删掉 ② 组 ⇒ 回到 5 枚 chip（数据仍在画像里）。
2. **整体退层 1**：`FactionProfileAdapter` 不再填 `profile.Traits` ⇒ `Traits` 恒默认 ⇒ chips 只剩科技档位 + 四系数（**行为等价于本批之前**）。
3. **运行时止损**：无 —— 纯 Def 只读 + 快照投影，不触存档、不改游戏状态。
