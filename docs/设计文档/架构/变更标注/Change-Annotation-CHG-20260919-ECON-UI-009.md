---
change_id: CHG-20260919-ECON-UI-009
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Application/ReadModels/**, Source/PersonalChronicle/Archive/ITab_Pawn_Chronicle.cs, Languages/**, docs/**]
files:
  - path: Source/PersonalChronicle/Application/ReadModels/ArchiveUiDataProvider.Career.cs
    type: M
    symbols: ArchiveUiDataProvider.BuildCareer（hours 换算） / ArchiveUiDataProvider.CareerSpanBreakdown / ArchiveUiDataProvider.BuildQualificationRows（Tooltip.Time 参数）
    lines: 97-110,330-345,505-540
  - path: Source/PersonalChronicle/Application/ReadModels/ArchiveUiDataProvider.Detail.cs
    type: M
    symbols: ArchiveUiDataProvider.SpanBreakdown
    lines: 283-312
  - path: Source/PersonalChronicle/Archive/ITab_Pawn_Chronicle.cs
    type: M
    symbols: ITab_Pawn_Chronicle.Draw（工时 bars 的 hours 换算）
    lines: 436-445
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: PersonalChronicle.UI.Kpi.NetValue / PersonalChronicle.UI.Career.Qual.Tooltip.Time
    lines: 771,1282-1291
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: PersonalChronicle.UI.Kpi.NetValue / PersonalChronicle.UI.Career.Qual.Tooltip.Time
    lines: 766,1273-1282
  - path: docs/changelog/v1.4.x_2026-09-19_工时口径统一与文案纠偏.md
    type: A
    symbols: CHG-20260919-ECON-UI-009
    lines: 1-120
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-009.md
    type: A
    symbols: CHG-20260919-ECON-UI-009
    lines: 1-130
---

# 变更标注 · CHG-20260919-ECON-UI-009（工时口径统一 + 文案纠偏）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-ECON-UI-009`
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-ECON-UI-009
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent；承接同日审计报告 §3 的 P1，并对其中一条**澄清了审计的误判**）
- Scope(摘要): 工时换算三处收口到 `GenDate.TicksPerHour`；资历悬停不再产出假时长；「净值」标签纠偏

### 一句话动机
同一人物在两个界面上算出**不同的工时**（一处写死 2400、一处用原版常量 2500，差 4.2%）；
另有两处"标签与数值不符"（悬停把件数标成小时；产出宫格把 30 天产出标成"净值"）。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `ReadModels/ArchiveUiDataProvider.Career.cs` | M | `BuildCareer`（hours 换算） · `CareerSpanBreakdown` · `BuildQualificationRows`（Tooltip 参数 12→8） | 97-110,330-345,505-540 | `2400L` → `GenDate.TicksPerHour`（**原注释「60000tick ≈ 25h」是笔误**）；分项不再累加 `*Hours`；Tooltip 参数减为 8 个 | GOV-006 / UI-001 | DATA / LOC |
| `ReadModels/ArchiveUiDataProvider.Detail.cs` | M | `SpanBreakdown` | 283-312 | 删除 4 个 `*Hours` 字段（保留 `*Count`）；补类注释澄清「量子化只对 `WorkCompleted` 成立」 | DATA-001 / GOV-013 | DATA |
| `Archive/ITab_Pawn_Chronicle.cs` | M | 工时 bars 的 hours 换算 | 436-445 | `2500f` 字面量 → `GenDate.TicksPerHour`（值正确但违反工程自订「禁止硬编码 2500」） | GOV-006 | UI |
| `Languages/ChineseSimplified/Keyed/Archive.xml` | M | `Kpi.NetValue` → 「月产出」；`Qual.Tooltip.Time` 占位符 `{0}..{7}` | 771,1282-1291 | 标签与数值对齐；悬停改为「完成次数」，删掉 `（{n} h）` | GOV-010 / LOC-001 | LOC |
| `Languages/English/Keyed/Archive.xml` | M | `Kpi.NetValue` → "Month output"；同上 | 766,1273-1282 | 中英同步 | GOV-010 / LOC-001 | LOC |
| `docs/changelog/v1.4.x_2026-09-19_工时口径统一与文案纠偏.md` | A | `CHG-20260919-ECON-UI-009` | 1-120 | 更新日志：五项处置（含**一条复核后不改**的说明）+ 5 项残余风险 + 实机验证表 | REL-001 / REL-004 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-009.md` | A | `CHG-20260919-ECON-UI-009` | 1-130 | 本文件自身（GATE-DELTA 要求标注覆盖**全部**实际改动文件，含标注自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步至发布区
> （MD5 `723FE1A6DDCF6B63E9312D5487DFDBCF`，两区一致）。

### 关键 diff 片段

```diff
// ArchiveUiDataProvider.Career.cs  （注释里的算术本身就是错的）
- // 相关工时：按事件首末 tick 跨度（60000tick ≈ 25h → 1h ≈ 2400 tick）。
- int hours = (int)(spanTicks / 2400L);
+ // ⚠ 原断言「60000tick ≈ 25h」错：1 天 = 60000 tick = 24 小时 ⇒ 1h = 2500 tick。
+ // 写死 2400 使工时系统性偏高 ~4.2%，且与 PawnMedal 的 GenDate.TicksPerHour 口径不一致。
+ int hours = (int)(spanTicks / GenDate.TicksPerHour);
```

```diff
// ArchiveUiDataProvider.Career.cs  （分项不再产出假小时）
- case "ItemProduced": b.CraftingCount++; b.CraftingHours += 1; break;
- case "BookProduced": b.OtherCount++; b.OtherHours += 1; break;
- default: b.OtherCount++; b.OtherHours += 1; break;
+ case "ItemProduced": b.CraftingCount++; break;
+ // BookProduced 与其余类型同归「其他」（原来分两个 case 但行为相同 ⇒ 合并）
+ default: b.OtherCount++; break;
```

```diff
// Keyed（中英同步）
- <PersonalChronicle.UI.Kpi.NetValue>净值</…>            → <…>月产出</…>
- • 制造 {4} 次（{5} h）                                  → • 制造 {4} 次
- 时长来源（按事件类型）：                                  → 资历来源（按事件类型的**完成次数**）：
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏（生成脚本自检 declared = hit）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— 003~008 批次遗留文件由 `-ExcludeFromFile` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致（`CHG-20260919-ECON-UI-009`）
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留（`*Hours` 字段与 `CraftingHours` 等引用**已整体删除**）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（**本轮最重要的一节：两条审计结论被复核推翻/收窄**）
| 审计原文 | 求证方式 | 复核结论 |
|---|---|---|
| 「`Career.cs:99` 的 `2400` 是错的」 | 直读注释断言「60000tick ≈ 25h」并做算术 | ✅ **成立**：1 天 = 60000 tick = 24h ⇒ 1h = 2500。注释本身就是笔误源头 |
| 「`Career.cs:519-523` 事件数冒充时长（违规）」 | 直读上游 `ArchiveService.Career.cs:595-603` 的量子化自述 | ⚠️ **部分推翻**：`WorkCompleted` **确实**按「每游戏 1 小时记 1 条」量子化，其"每事件 = 1h"是**有意设计且正确**。真问题收窄为「`ItemProduced`/`ConstructionCompleted`/`ResearchCompleted`/`BookProduced` 是完成瞬间事实，不该算小时」 |
| 「`ITab_Pawn_Chronicle.cs:438` 硬编码 2500 违规」 | 核对工程自订铁律（`PawnOverview.cs:386`） | ✅ **成立**（值正确但违规） |
| 「`PawnMedal.cs:687` 的 `2f` 硬编码参与派生结论」 | 直读该常量声明 + UI 单位文案（`Ledger.NetUnit` = 「银（估）」）+ 成本子行 | ❌ **推翻（过度报告）**：它已是命名常量 + GOV-006 注释，且 UI 明确标注"（估）"并展示算式。**本次有意不改**，改的是"同名两口径" |

> **方法学结论**：审计报告里的"违规"判定也需**逐条复核上游注释**——
> 本轮 4 条里 **1 条被部分推翻、1 条被完全推翻**。已把这条写进 Changelog §3。

### 回滚定位
1. **只退工时口径**：把 `GenDate.TicksPerHour` 改回 `2400` / `2500f`（**不建议** —— 那会恢复 4.2% 偏差与两处不同值）。
2. **只退悬停文案**：恢复 `*Hours` 字段 + 12 参 `Translate` + Keyed 的 `（{n} h）`（**不建议** —— 会恢复"两个数永远相等"的假时长）。
3. **运行时止损**：无。纯展示层，不影响存档与逻辑。
