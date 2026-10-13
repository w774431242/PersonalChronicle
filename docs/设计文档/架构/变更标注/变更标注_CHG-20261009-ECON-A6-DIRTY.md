---
change_id: CHG-20261009-ECON-A6-DIRTY
date: 2026-10-09
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedNotDeployed
scope: U6-A6（第 3 项）—— 事件驱动感知失效接线（消除 `MarkPerceptionDirty` 零消费方）：真实结算落地后给受益人的 Needs|Body 标脏；按用户指令**修复期不部署**
rule_ids: AI-001, AI-003, AI-011, AI-014, GOV-006, GOV-011, GOV-012, GOV-013, ARC-001, PERF-001, PERF-002, TEST-001
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyCare.cs
    type: M
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/实施批次状态.json
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# U6-A6 · 事件驱动感知失效接线（第 3 项）

**ChangeID**：`CHG-20261009-ECON-A6-DIRTY`｜**日期**：2026-10-09｜**授权**：用户「批准继续」

## 1. 前置设计（先裁定再落码，避免"猜着接"）

`MarkPerceptionDirty(pawnId, groups)` 的接口语义（已核实 `PerceptionCoordinator.cs:84-92`）：owner-thread only；`groups=0` 不排期；对**未登记 Pawn 是 no-op**；标脏会把该 Pawn 的采集**排到当前 tick**（`DirtyDue = min(DirtyDue, lastTick)`）。

**裁定**：
1. **触发点**：一次**真实结算落地**（照护/诊疗交付并完成扣费）之后 —— 这是"事实确实变了"的时刻；
2. **标哪些域**：只标 `Needs | Body` —— 该事件改变的是受益人的需求与身体；标 `All` 会让一次照护触发全量重采（浪费且无依据）；
3. **不做的事**：不标"钱变了"（钱不在感知域内）；不在非主线程调用（`MarkDirty` 会抛，**刻意不吞**：非主线程调用本身就是要修的缺陷，掩盖它比报错更危险）。

## 2. 改动

| 文件 | 改动 |
|---|---|
| `EconomyCare.cs` | 两处 `return Care.Charge(...)` 收敛为 `ChargeAndMarkFacts(...)`：扣费成功（`Applied`）即 `MarkPerceptionDirty(patientKey, Needs|Body)`；新增该私有方法并写明上述裁定 |

## 3. 消除的零消费方（审计 `ECON-WIRING-AUDIT-006` ⑤ 之三）

`MarkPerceptionDirty` 此前**零消费方**（感知只靠 150/250/600 tick 轮询）；现由结算路径消费（`EconomyCare.cs:85/91`；定义 `EconomyPerception.cs:82`）。

## 4. 验证基线（全部实跑）

| 项 | 判据 | 结果 | 证据 |
|---|---|---|---|
| 编译 | 输出无 `error` | **0 错 0 警** | `_scripts/compile_with_sdk.ps1` |
| 全量离线测试 | dotnet test（net48） | **1188 通过 / 0 失败 / 1 跳过（总 1189）** | `Tests/TestResults/U6A6-Dirty-V1.trx` |
| ⚠ 计数说明 | 上批为 1187/0/1（总 1188）⇒ 本批 **+1 总计数非本批新增用例**（本批未加测试），疑并行写入者新增；**不做归因声明** | 如实登记 | 两份 trx 对照 |
| 调用点可达性（强制自检） | 零消费方必须有生产调用点 | `EconomyCare.cs:85`（调用）/`:91`（定义处） | 检索输出 |
| 部署 | **按指令不部署** | 发布区仍为 `44F1A1BD…` | — |
| DELTA | `gate-runner.ps1 -Base HEAD -ExcludeFromFile .codebuddy/tmp/u6a6c-out-of-scope.txt -Blocking` | 见 §5 | 脚本输出 |
| 实机 | 结算后立即重采（tick 行为） | **NOT EXECUTED** | — |

## 5. DELTA 与收尾

（由 `gate-runner.ps1` 输出回填。）U6-A6 剩余：`QueryZoneWagePolicy`（联 M6 工资承诺口径）· `ShadowTransactionKernel`（**待 PM 架构裁定**）。**统一部署**在所有修复完成后一次性执行，届时更新验收清单 §0 哈希与进度源 `deployedBuildSha256`。

---
AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
