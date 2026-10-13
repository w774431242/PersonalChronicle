---
change_id: CHG-20261009-ECON-A4-CARE-CYCLE
date: 2026-10-09
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedAndDeployedByThisBatch
scope: U6-A4 —— 周期任务缺口修复：照护授权到期裁剪（250 tick）+ 非 CaptureWired 服务类型的周期复核（让诚实状态落到能力表）
rule_ids: AI-001, AI-003, AI-011, AI-014, GOV-006, GOV-011, GOV-012, GOV-013, DATA-001, DATA-004, SAVE-002, SAVE-006, ARC-001, PERF-001, PERF-002, TEST-001
files:
  - path: Source/PersonalChronicle/Data/PawnEconomy/CareSaveData.cs
    type: M
  - path: Source/PersonalChronicle/Application/PawnEconomy/Care/CareService.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyCare.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs
    type: M
  - path: Tests/CareExpiryTrimTests.cs
    type: A
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/实施批次状态.json
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# U6-A4 · 周期任务缺口修复（照护到期裁剪 + 服务能力周期复核）

**ChangeID**：`CHG-20261009-ECON-A4-CARE-CYCLE`｜**日期**：2026-10-09｜**授权**：用户「继续」

**前置**：`route-task.ps1` ⇒ UNROUTED ⇒ 按规则第 3 条**语义兜底**判定适用面为 TEST/PERF（周期任务的成本与判据一致性）；门禁 `verify-cluster.ps1 -Blocking` ⇒ `集群自检: PASS`（runId `1fcc9ac6…`）。

## 1. 修的是全模块审计（`ECON-WIRING-AUDIT-006`）里的两条周期任务缺口

| 缺口（审计原文） | 本批处置 |
|---|---|
| 「照护授权无周期到期清理（仅惰性判定）」 | `CareSaveData.TrimExpired(now)` + `CareService.TrimExpired`，由运行时 **250 tick** 调用；判据与 `CareAuthorization.IsExpired`（`now > ExpiresTick`）**逐字一致** |
| 「服务能力无降级/复核周期任务（capability 只在 tend 时单点更新）」 | `RefreshUnwiredServiceCapabilities()`：250 tick 复核**非 CaptureWired** 的类型，让"过程在·摄取未接 / 原版无过程"的诚实状态真正落到能力表 |

## 2. 关键取舍（必须写清楚，否则会被误读）

- **周期复核刻意跳过 `CaptureWired` 类型**（当前仅 Treatment）：它们的容量门需要**真实执行者上下文**，无上下文的批量评估会把已证成的 `ReadyPaid` **降级**。降级与"用代理值放行"是同一类错误的两个方向，故宁可少评估、不误伤。
- **到期裁剪只清行，不改判据**：读/写路径本就惰性判定过期（`CanCharge` 的 `Expired` 分支），本批只是把陈行真正删掉，避免 4096 行上限被历史行挤满（那会挤压有效授权）。
- 三条新成员均**幂等、不抛**，且成本有界（授权表 ≤4096、服务类型 5 类）。

## 3. 验证基线（全部实跑）

| 项 | 判据 | 结果 | 证据 |
|---|---|---|---|
| 编译 | 输出无 `error` | **0 错 0 警** | `_scripts/compile_with_sdk.ps1` |
| 全量离线测试 | dotnet test（net48） | **1187 通过 / 0 失败 / 1 跳过（总 1188）**（+3 新用例） | `Tests/TestResults/U6A4-Care-V1.trx` |
| **调用点可达性（强制自检）** | 两个新周期任务必须被 250 tick 块调用 | `EconomyRuntime.cs:186/187`（定义 `EconomyCare.cs:99/106`） | 调用点检索输出 |
| 新用例可证伪性 | 等值边界保留 / 时间回退不裁 / 二次调用幂等 / 已扣费同样按时间裁 | 全部覆盖且非恒真 | `Tests/CareExpiryTrimTests.cs` |
| 部署 | deploy-to-game.ps1 | Defs 36 / Languages 66 / Assemblies 1 synced | 脚本输出 |
| DELTA | `gate-runner.ps1 -Base HEAD -ExcludeFromFile .codebuddy/tmp/u6a4-out-of-scope.txt -Blocking` | 见 §5 | 脚本输出 |
| **实机** | 250 tick 复核与裁剪的真实行为 | **NOT EXECUTED** | — |

## 4. Save schema

**无变更**：本批只操作既有节点内的行集合（`CareSaveData` 的 `authorizations`/`capabilities`），不新增字段、不改判据语义 ⇒ 无迁移；旧档照常加载。

## 5. 未闭环（登记在 `ECON-WIRING-AUDIT-006`）

1. `QueryFoodPricesPublished` 接线（UI 席限定：只替换**食品**那一项，工资/服务计数保持内联）。
2. `EconomyEmployment.cs:45-46` 过期注释未改（未在同一轮内取到原文，避免盲改）。
3. 其余零消费符号：`InstitutionToolCatalogue` / `QueryZoneWagePolicy` / `QueryLegacyReconciliation` / `OwnershipCount` / `MarkPerceptionDirty` / `ShadowTransactionKernel` —— 需逐项裁定"接线还是下线"。
4. **周期复核的覆盖面仍是 4/5 类**（Treatment 走事件驱动）：Treatment 的周期化复核需要"真实执行者上下文"的来源设计，属后续单元。

---
AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
