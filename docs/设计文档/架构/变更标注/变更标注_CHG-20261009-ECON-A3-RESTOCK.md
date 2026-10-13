---
change_id: CHG-20261009-ECON-A3-RESTOCK
date: 2026-10-09
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedAndDeployedByThisBatch
scope: U6-A3 —— 补货评估消费面收口：`RestockEvaluator` 唯一生产消费点（总览页数值/阻断上屏）+ 展示投影纯函数化 + 有界确定性截断
rule_ids: AI-001, AI-003, AI-011, AI-014, GOV-006, GOV-009, GOV-011, GOV-012, GOV-013, DATA-001, ARC-001, UI-001, UI-004, UI-008, LOC-001, LOC-002, LOC-003, PERF-001, PERF-002, TEST-001
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Production/ProductionReadout.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyProductionFacade.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Overview.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  - path: Tests/RestockPresentationTests.cs
    type: A
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/实施批次状态.json
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# U6-A3 · 补货评估消费面（全模块审计后的第一项缺口修复）

**ChangeID**：`CHG-20261009-ECON-A3-RESTOCK`｜**日期**：2026-10-09｜**授权**：用户「进行缺口修复」

## 1. 背景

全模块接线审计（`ECON-WIRING-AUDIT-006`）查出一处**消费面断头**：U6-A/U6-A2 把两个输入接线进了读数，但 `RestockEvaluator`（唯一把六输入变成"净缺口"的算法）**生产零调用方** ⇒ 授权账与库存目标的成果**不上屏、不进决策**。本批补上这一环。

## 2. 改动（含两席评审的必守条件）

| 文件 | 改动 | 对应条件 |
|---|---|---|
| `Domain/…/Production/ProductionReadout.cs` | 新增 `RestockPresentation`（**纯函数**）：`Describe`（**只有 `Shortfall` 才有数值**；其余一律只给原因码）+ `Select`（Ordinal 排序 / 有界 ≤4 / 报总数）；`MaximumRowsShown = 4` | PERF C3、UI ②③ |
| `Economy/…/EconomyProductionFacade.cs` | 新增 `QueryRestockEvaluations`：**一次批量读**（`ProductionReadoutGuard.Read` 覆盖全部品类，避免每类各扫一遍建筑）→ 逐行纯 `RestockEvaluator.Evaluate` → `Select`；`out totalCategories` 供 UI 明示截断 | PERF C1、UI ③ |
| `Economy/…/Dialog_PawnEconomy.Overview.cs` | 新增「补货评估」行：`品类=净缺口 N` 或 `品类=无法评估:<原因码>`；末尾「显示 N/总 M」；读数不可用明示；**仍在 60 tick 快照内** | UI ①②③、PERF C2 |
| `Languages/{zh,en}/Keyed/PawnEconomy.xml` | +6 键（`Overview.Restock/RestockBlocked/RestockShown/RestockUnavailable` + `Policy.Reason.DemandDomainNotWired/ReadoutUnavailable`），中英 1:1，**复用既有 `ReasonLabel` 管道** | UI ①④ |
| `Tests/RestockPresentationTests.cs`（新） | 4 用例：仅 Shortfall 显数值 / 未知与不一致**绝不成 0**（含反向对照）/ 有界确定性截断（含顺序无关对照）/ 空集与上限 0 不伪造行 | PERF C3 |

## 3. 不变量（本轮新增的两条，可复用）

1. **"0 只能来自真实读数"**：`hasNumber` 仅当 `Outcome == Shortfall`；Unknown / Inconsistent 一律只出原因码 —— 屏幕上永远不会出现"读成 0 的未知"。
2. **展示有界且确定**：Ordinal 排序取前 4，并**必须**附「显示 N/总 M」；截断不得静默省略。

## 4. 验证基线（全部实跑）

| 项 | 判据 | 结果 | 证据 |
|---|---|---|---|
| 编译 | 输出无 `error` | **0 错 0 警** | `_scripts/compile_with_sdk.ps1` |
| 全量离线测试 | dotnet test（net48） | **1184 通过 / 0 失败 / 1 跳过（总 1185）**（+4 新用例） | `Tests/TestResults/U6A3-Restock-V2.trx` |
| **调用点可达性（新增强制自检）** | 新包装方法必须有唯一生产调用点 | `QueryRestockEvaluations` ⇒ `Dialog_PawnEconomy.Overview.cs:100`（定义 `EconomyProductionFacade.cs:88`） | 调用点检索输出 |
| 部署 | deploy-to-game.ps1 | Defs 36 / Languages 66 / Assemblies 1 synced；DLL SHA `F385D515…` | 脚本输出 |
| DELTA | `gate-runner.ps1 -Base HEAD -ExcludeFromFile .codebuddy/tmp/u6a3-out-of-scope.txt -Blocking` | 见 GR 记录 §4 | 脚本输出 |
| **实机** | 总览页补货评估行渲染 | **NOT EXECUTED** | — |

## 5. 诚实口径（最重要的一条）

接线后屏上将出现 **「无法评估:需求域未接线」**（因 `ConfirmedUndeliveredDemand` 仍为 `Unknown`，评估器在首个未知输入处短路）。**这不是缺陷，是如实呈现**；本批**不得**被表述为"补货评估可用"。净缺口数值分支（`Shortfall`）要等到 M7 订单域/出口域接线后才会真正出现——实现已就位并有测试守护。

## 6. 未闭环（已登记 `ECON-WIRING-AUDIT-006`）

`QueryFoodPricesPublished` 接线（UI 席限定只替换食品那一项）· 服务能力周期复核 + 照护授权到期清理 · `InstitutionToolCatalogue`/`QueryZoneWagePolicy`/`QueryLegacyReconciliation`/`OwnershipCount`/`MarkPerceptionDirty`/`ShadowTransactionKernel` 零消费 · `EconomyEmployment.cs:45-46` 过期注释。

---
AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
