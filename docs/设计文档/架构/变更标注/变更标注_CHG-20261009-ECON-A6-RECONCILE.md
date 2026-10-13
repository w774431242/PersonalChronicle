---
change_id: CHG-20261009-ECON-A6-RECONCILE
date: 2026-10-09
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedNotDeployed
scope: U6-A6（第 2 项）—— 旧账对账报告接线上屏（消除 `QueryLegacyReconciliation` 零消费方）；按用户指令**修复期不部署**
rule_ids: AI-001, AI-003, AI-011, AI-014, GOV-006, GOV-009, GOV-011, GOV-012, GOV-013, UI-001, UI-004, LOC-001, LOC-002, LOC-003, TEST-001
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Overview.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/实施批次状态.json
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# U6-A6 · 旧账对账报告上屏（第 2 项）

**ChangeID**：`CHG-20261009-ECON-A6-RECONCILE`｜**日期**：2026-10-09｜**授权**：用户「继续批量」

## 1. 字段裁定（先定再落码 —— 这是本项的前置设计）

| 字段 | 上屏方式与理由 |
|---|---|
| `SourceCount` | 直接显示（条数，有界） |
| `DiagnosticChecksPassed` / `Issues.Count` | 显示 `诊断=通过` 或 `诊断=问题:N` —— 只报**数量**，不逐条铺开（报告内部 Issues 可能很长） |
| `UnpaidWagesMinor` | 显示（decimal → `0.##`，**不做四舍五入到整数**以免伪造精度） |
| `BackingVerified` | **本属性恒为 `false`（报告自述）** ⇒ 固定显示「背书未证」，**绝不允许**显示成已核验 |
| `Fault` | 非空时追加在诊断段（稳定故障码，非本地化文本） |
| `Records` / 各 raw sums / `CanActivateNewLedger` | **不上屏**：前者是逐条记录（可能很长），后者恒 `false`，铺开只会变成噪声 |

## 2. 改动

| 文件 | 改动 |
|---|---|
| `Dialog_PawnEconomy.Overview.cs` | 新增「旧账对账」行（只读、有界）；`QueryLegacyReconciliation()` 被 UI 线程外的异常（该方法**刻意**在非主线程抛 `InvalidOperationException`）降级为「旧账对账: 未知」，**不吞异常成假数据** |
| `Languages/{zh,en}/Keyed/PawnEconomy.xml` | +7 键（`Reconciliation`/`RecordCount`/`Diagnostics`/`Passed`/`Issues`/`UnpaidWages`/`BackingUnverified`），中英各 7 键 |

## 3. 消除的零消费方（审计 `ECON-WIRING-AUDIT-006` ⑤ 之二）

`QueryLegacyReconciliation` 此前**零消费方**；现由总览页消费（`Dialog_PawnEconomy.Overview.cs:72/76`；定义 `EconomyReconciliation.cs:10`）。该报告是 **B2.4.3 验收**的构成项，本批只做"可见"，**未做**任何账务语义变更（方法本身只读、禁副作用）。

## 4. 验证基线（全部实跑）

| 项 | 判据 | 结果 | 证据 |
|---|---|---|---|
| 编译 | 输出无 `error` | **0 错 0 警** | `_scripts/compile_with_sdk.ps1` |
| 全量离线测试 | dotnet test（net48） | **1187 通过 / 0 失败 / 1 跳过（总 1188）** | `Tests/TestResults/U6A6-Reconcile-V1.trx` |
| 调用点可达性（强制自检） | 零消费方必须有生产调用点 | `Overview.cs:72`（调用）/`:76`（降级） | 检索输出 |
| 部署 | **按指令不部署** | 发布区仍为 `44F1A1BD…` | — |
| DELTA | `gate-runner.ps1 -Base HEAD -ExcludeFromFile .codebuddy/tmp/u6a6b-out-of-scope.txt -Blocking` | 见 §5 | 脚本输出 |
| 实机 | 总览「旧账对账」行渲染 | **NOT EXECUTED** | — |

## 5. DELTA 与收尾

（由 `gate-runner.ps1` 输出回填。）U6-A6 剩余项：`QueryZoneWagePolicy`（联 M6 口径）· `MarkPerceptionDirty`（需设计）· `ShadowTransactionKernel`（待 PM）。**统一部署**在所有修复完成后一次性执行。

---
AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
