---
change_id: CHG-20260919-ECON-UI-012
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.Society.cs, Languages/**, docs/**]
files:
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.Society.cs
    type: M
    symbols: BuiltinEconomyProvider.BuildSociety / AppendDerivedChips / FactorText / TechLevelKey / CountFactionSettlements
    lines: 1-250
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: MVS.Economy.Society.Scale.Title / Scale.Label / Scale.Value / Chip.Tech / Chip.Production / Chip.TradeImport / Chip.Luxury / Chip.Shock
    lines: 198-207
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: （同上的 8 键）
    lines: 191-200
  - path: docs/changelog/v1.4.x_2026-09-19_派系画像反推.md
    type: A
    symbols: CHG-20260919-ECON-UI-012
    lines: 1-130
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-012.md
    type: A
    symbols: CHG-20260919-ECON-UI-012
    lines: 1-140
---

# 变更标注 · CHG-20260919-ECON-UI-012（派系画像反推）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-ECON-UI-012`
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-ECON-UI-012
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent；先读链路定位到"画像被丢弃"，再按 Cecil 铁律核实新数据源）
- Scope(摘要): 社会背景面板 —— `FactionArchetypeData` 由「只当门禁用」改为**真正驱动显示**；规模块改运行时真值

### 一句话动机
用户反馈「除帝国外，其他派系的数据没有反推」。查下来根因不是"没反推"，而是
**反推出来的数被 `out ... _` 丢掉了** —— 面板画的是硬编码 Keyed 文案。
帝国那张看着对纯属巧合（文案与系数同源）。修复 = **把算出来的数用起来**。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `Builtin/BuiltinEconomyProvider.Society.cs` | M | `BuildSociety` · `AppendDerivedChips`(新) · `FactorText`(新) · `TechLevelKey`(新) · `CountFactionSettlements`(新) | 1-250 | ① `out FactionArchetypeData _` → 具名 `profile` 并**真正消费**；② 5 枚 chips 改由画像反推；③ 规模块改运行时真值据点计数；④ **合并** `Royal`/`Generic` 两条逐字重复的分支为一条渲染路径；⑤ 段标题键改 `Scale.Title` | GOV-010 / DATA-001 / GOV-006 | DATA / UI |
| `Languages/ChineseSimplified/Keyed/MVS.xml` | M | `Society.Scale.*`（3） + `Society.Chip.*`（5） | 198-207 | 新增 8 键（模板化，带占位符）；`Population.Title` 保留但已不被引用（登记为死键） | GOV-010 / LOC-001 | LOC |
| `Languages/English/Keyed/MVS.xml` | M | （同上 8 键） | 191-200 | 中英同步；**键集合逐一比对一致（312 = 312）** | GOV-010 / LOC-001 | LOC |
| `docs/changelog/v1.4.x_2026-09-19_派系画像反推.md` | A | `CHG-20260919-ECON-UI-012` | 1-130 | 更新日志：根因 / 四步修复 / **三块没有反推的如实登记** / API 核实表 / 5 项残余风险 | REL-001 / REL-004 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-012.md` | A | `CHG-20260919-ECON-UI-012` | 1-140 | 本文件自身（GATE-DELTA 要求标注覆盖**全部**实际改动文件，含标注自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步至发布区
> （MD5 `D160AA111613895E6D7C325F09D23309`，两区一致）。

### 关键 diff 片段

```diff
// BuiltinEconomyProvider.Society.cs  （根因：画像算出来就丢）
- || !MarketContracts.Factions.TryGetProfile(selectedFactionDefName, out FactionArchetypeData _))
+ FactionArchetypeData profile;
+ if (string.IsNullOrEmpty(selectedFactionDefName)
+     || MarketContracts.Factions == null
+     || !MarketContracts.Factions.TryGetProfile(selectedFactionDefName, out profile))
  { ... 未建模裁决 ... }
```

```diff
// BuiltinEconomyProvider.Society.cs  （chips：硬编码文案 → 画像反推）
- snapshot.Chips.Add(T(pk + ".Chip1"));   // 「产能过剩 ×1.5」写死在语言包里
- snapshot.Chips.Add(T(pk + ".Chip2"));
+ snapshot.Chips.Add(TS1("MVS.Economy.Society.Chip.Tech", T(TechLevelKey(profile.TechLevel))));
+ snapshot.Chips.Add(TS1("MVS.Economy.Society.Chip.Production", FactorText(profile.ProductionFactor)));
+ snapshot.Chips.Add(TS1("MVS.Economy.Society.Chip.TradeImport", FactorText(profile.TradeImportFactor)));
+ snapshot.Chips.Add(TS1("MVS.Economy.Society.Chip.Luxury", FactorText(profile.LuxuryPreference)));
+ snapshot.Chips.Add(TS1("MVS.Economy.Society.Chip.Shock", FactorText(profile.ShockSensitivity)));
```

```diff
// BuiltinEconomyProvider.Society.cs  （规模：占位符 → 运行时真值）
- snapshot.PopulationLabel = T(pk + ".Population.Label");
- snapshot.PopulationValue = T(pk + ".Population.Value");   // Generic 的这行是「运行时计数」——方法名当答案
+ int settlements = CountFactionSettlements(selectedFactionDefName);
+ snapshot.PopulationLabel = T("MVS.Economy.Society.Scale.Label");
+ snapshot.PopulationValue = TS1("MVS.Economy.Society.Scale.Value", settlements.ToString("N0", InvariantCulture));
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏（生成脚本自检 declared = hit）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— 003~011 批次遗留文件由 `-ExcludeFromFile` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致（`CHG-20260919-ECON-UI-012`）
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留（**被删除的是两份重复分支，非留空**）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| 「画像被丢弃」 | 直读 `Society.cs` 的 `TryGetProfile(... out FactionArchetypeData _)` | ✅ 证实：`_` 丢弃，仅作布尔门禁 |
| 「帝国文案与系数同源」 | 比对 `Royal.Chip1 = 产能过剩 ×1.5` 与 `FactionArchetype.ForTechLevel(Ultra) = 1.5f` | ✅ 证实：数值相同 ⇒ 掩盖了"面板没用它" |
| 非帝国派系是否进 Generic 分支 | 读 `IsEconomicallyModeled`（6 字段门禁）核定：Outlander/Tribe **通过**，Pirate（`permanentEnemy`）不通过 | ✅ 通过 ⇒ 走 Generic，故用户看到的是"有模板但没数字" |
| `Find.WorldObjects` / `Settlements` / `Faction` 的存在性 | Mono.Cecil 静态核实（`type X full`） | ✅ 存在；⚠ **`WorldObjectsHolder` 在 `RimWorld.Planet` 而非 `RimWorld`**（首次按顶层查 `NOT FOUND`，纠正了命名空间猜测） |
| `Faction` 是否可空 | 读属性签名：`public virtual Faction Faction { get; }` | ⚠ **可为 null**（无主对象）⇒ 已逐项守卫，避免"一个 null 让整面板退化成未建模" |
| 语言包能否用 XML 注释 | 检索 `Languages/**/*.xml` 的 `<!--` | ❌ **0 命中（全工程首例）**⇒ **主动撤销**：风险虽小但一旦语言加载器不容忍就是**整个语言包失效**（全界面显示键名），且离线无法验证；说明转由代码注释与 Changelog 承载 |
| 中英键集合是否对齐 | 逐键比对（过滤 Element 节点） | ✅ **312 = 312，零差异**（注：`ChildNodes.Count` 的 344/341 差异是缩进空白节点，非缺键） |

### 有意**未做**的三项（如实登记，非遗漏）
| 项 | 为什么不反推 |
|---|---|
| 阶级分布（层数/占比/人数） | 原版**无社会分层字段**；`pawnGroupMakers` 是"出兵构成"≠"人口分层"，硬折算 = 编造 |
| 人口数 | 原版据点未进入前不生成地图，无"驻留人口"字段；`45 人/据点 × 因子` 是设计常量 |
| 多层星球据点 | `Settlements` 仅覆盖表面层；跨层求和是否该合并属**产品口径问题** |

> **判据**：**能从权威数据反推的 → 反推**；**需要假设才能得到的 → 不填或标注为假设**。跨过这条线就是编造。

### 回滚定位
1. **只退 chips**：`AppendDerivedChips` 改回 5 行 `T(pk + ".ChipN")`（键仍在语言包里，一行级回退）。
2. **只退规模块**：把 `Scale.*` 三行改回 `pk + ".Population.Label/.Value"`（但**会恢复占位符当答案**，不建议）。
3. **运行时止损**：无 —— 纯展示层，不影响存档与逻辑。
