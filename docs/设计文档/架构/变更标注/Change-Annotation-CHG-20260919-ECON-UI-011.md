---
change_id: CHG-20260919-ECON-UI-011
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Archive/ITab_Pawn_Chronicle.cs, Source/PersonalChronicle/Application/ReadModels/EconomyFactionStatusSnapshot.cs, Defs/SystemCenterDefs.xml, docs/**]
files:
  - path: Source/PersonalChronicle/Archive/ITab_Pawn_Chronicle.cs
    type: M
    symbols: ITab_Pawn_Chronicle.Draw（产出宫格的 prodKinds / weekVal / monthVal）
    lines: 458-485
  - path: Source/PersonalChronicle/Application/ReadModels/EconomyFactionStatusSnapshot.cs
    type: M
    symbols: EconomyFactionStatusSnapshot.Truncated / ReadOnlyDegraded / MarketReady
    lines: 58-95
  - path: Defs/SystemCenterDefs.xml
    type: M
    symbols: policy_center.reserved
    lines: 159-172
  - path: docs/changelog/v1.4.x_2026-09-19_空态与声明诚实性收尾.md
    type: A
    symbols: CHG-20260919-ECON-UI-011
    lines: 1-120
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-011.md
    type: A
    symbols: CHG-20260919-ECON-UI-011
    lines: 1-130
---

# 变更标注 · CHG-20260919-ECON-UI-011（空态诚实性 + 声明与实际对齐）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-ECON-UI-011`
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-ECON-UI-011
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent；清审计 §5 的 P2/P3，并把两项"需裁决"的诚实交接）
- Scope(摘要): 产出宫格 0 冒充空态；三个孤儿字段的虚假注释承诺；`policy_center` 的 reserved 声明与实际不符

### 一句话动机
三件"**说的和做的不一样**"：同一格内 `--` 与 `0 类` 自相矛盾；三个字段的注释承诺了从未实现的 UI 行为；
一个中心声明"模块已存在"，而全仓该模块**零代码**。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `Archive/ITab_Pawn_Chronicle.cs` | M | `Draw` 的 `prodKinds` / `weekVal` / `monthVal` | 458-485 | 三处统一到 `prodOk` 判据：**从未产出 ⇒ `--`**；**有产出但窗口内为 0 ⇒ `0 银`（真值）**。消除同格 `--` 与 `0 类`/`0 银` 并存的自相矛盾 | LAND-009 / UI-001 | UI |
| `Application/ReadModels/EconomyFactionStatusSnapshot.cs` | M | `Truncated` / `ReadOnlyDegraded` / `MarketReady` | 58-95 | **仅注释**：删掉"UI 显示提示 / UI 挂角标"这类**从未实现**的承诺，改为如实描述；字段全保留（稳定契约 + 测试钉默认值） | GOV-013 / DATA-001 | DATA |
| `Defs/SystemCenterDefs.xml` | M | `policy_center.reserved`：`false` → **`true`** | 159-172 | 判据取自本文件 `population_center` 段的既有裁定（`reserved=false` = **模块已存在**）；该中心零代码 ⇒ 声明与实不符。就地写明反悔路径（补 Provider 后改回一行） | GOV-006 / DATA-001 | DATA |
| `docs/changelog/v1.4.x_2026-09-19_空态与声明诚实性收尾.md` | A | `CHG-20260919-ECON-UI-011` | 1-120 | 更新日志：三处处置 + **两项"有意未做"的交接**（含为什么不做的判据区分）+ 4 项残余风险 | REL-001 / REL-004 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-011.md` | A | `CHG-20260919-ECON-UI-011` | 1-130 | 本文件自身（GATE-DELTA 要求标注覆盖**全部**实际改动文件，含标注自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步至发布区
> （MD5 `A2BD554C65C719BE373CB86D2D73E195`，两区一致）。`Defs/SystemCenterDefs.xml` **不是二进制**，
> 已在 `files:` 声明（MD5 `C54796D2F8E0FE5745165417EFA97A32`，两区一致）。

### 关键 diff 片段

```diff
// ITab_Pawn_Chronicle.cs  （同格两口径 ⇒ 统一判据）
- string prodKinds = (...Count > 0)
-     ? Count + " 类"
-     : "0 " + "…Kpi.Cat".Translate();          // 🔴 与左边大值的 "--" 自相矛盾
+ string catUnit = "…Kpi.Cat".Translate().ToString();
+ string prodKinds = (...Count > 0)
+     ? Count + " " + catUnit
+     : (prodOk ? "0 " + catUnit : "--");       // 从未产出 ⇒ --；有产出但无分类 ⇒ 真的是 0
- string weekVal = Mathf.RoundToInt(snap.WeeklyProductionSilver) + " " + silver;   // 🔴 恒画 0 银
+ string weekVal = prodOk ? Mathf.RoundToInt(snap.WeeklyProductionSilver) + " " + silver : "--";
```

```diff
// SystemCenterDefs.xml  （声明与实际不符）
+ <!-- ⚠ 判据取自本文件 population_center 段：reserved=false 的语义是「模块已存在，只是没接框架」。
+      而本中心在 Source/ 下零代码（全仓 policy 命中均为 *PolicyDef，与本中心无关）
+      ⇒ 声明 false 等于谎称"模块已存在"。改 true 后显示「预留（待做）」。
+      ⚠ 将来落地该中心：补 Provider + 把本行改回 false，一行即回。 -->
- <reserved>false</reserved>
+ <reserved>true</reserved>
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏（生成脚本自检 declared = hit）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— 003~010 批次遗留文件由 `-ExcludeFromFile` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致（`CHG-20260919-ECON-UI-011`）
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 判断 | 求证方式 | 结论 |
|---|---|---|
| `policy_center` 是否真有模块 | 全仓检索 `policy`（大小写不敏感），逐条核对 24 处命中 | ✅ 全部是 `*PolicyDef` 命名模式，**与该中心无关** ⇒ 零代码 |
| `reserved=false` 到底代表什么 | 读 `SystemCenterDefs.xml` 自身 `population_center` 段的裁定注释 | ✅ 原文：「模块已存在 ⇒ reserved=false + 无 Provider ⇒ 显示『数据未接入』而不是『预留（待做）』」 |
| `reserved=true` 会不会触发启动校验拦停 | 核对审计记录的校验语义（`ChronicleMod.cs` 拦「**预留却注册** Provider」） | ✅ 不冲突：该中心无 Provider |
| 三个孤儿字段能否直接删 | 检索测试引用 | ❌ **不能盲删**：`EconomySnapshotContractTests` 钉着它们的默认值 ⇒ 属稳定契约；**改注释**才对症 |
| 产出宫格的 `0 类` 是真 0 还是冒充 | 读 `prodOk` 的构成（累计器总量）与窗口量的关系 | ⚠️ **两者是不同判据**：从未产出 ⇒ 冒充（改 `--`）；有产出但窗口 0 ⇒ **真值**（保留 `0 银`） |

### 有意**未做**的两项（交接给裁决，非遗漏）
| 项 | 为什么不擅自改 |
|---|---|
| `SystemCenterDetailSnapshot.KpiSectionTitle` 死链 | 修法属**产品意图二选一**：（A）UI 读它并在 KPI 区上方画标题（涉 LAND 1:1 版式）；（B）删字段+接口成员+5 个实现（缩小契约）。两者代价方向相反 |
| `Archive/ArchiveMainTabWindow.*` 的 100+ 绘制期翻译 | 独立迁移面，一次改 100+ 处**无法有效复核**；`Loc` 已就位，可按文件分批推进 |

> **判据**：`policy_center` 属**事实性**问题（本工程自己的注释写明了规则）⇒ 可以改；
> 上表两项需**选方向**（意图问题）⇒ **事实可以改，意图不能替用户决定**。

### 回滚定位
1. **只退空态**：把 `prodKinds`/`weekVal`/`monthVal` 的三元判断去掉（**不建议** —— 会恢复同格自相矛盾）。
2. **只退 Def**：`policy_center.reserved` 改回 `false`（**一行**，已在 XML 注释里写明）。
3. **只退注释**：无意义（注释纠偏不影响运行）。
