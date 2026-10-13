---
change_id: CHG-20261009-ECON-A5-CLOSEOUT
date: 2026-10-09
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedAndDeployedByThisBatch
scope: U6-A5 小项收口 —— 食品报价计数与工作占用计数接线上屏（消除两处零消费方）+ 学习账过期注释修正 + 其余零消费符号**逐项裁定**（接线/下线/待裁定）
rule_ids: AI-001, AI-003, AI-011, AI-014, GOV-006, GOV-009, GOV-011, GOV-012, GOV-013, UI-001, UI-004, LOC-001, LOC-002, LOC-003, PERF-002, TEST-001
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Overview.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs
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

# U6-A5 · 小项收口 + 零消费符号裁定

**ChangeID**：`CHG-20261009-ECON-A5-CLOSEOUT`｜**日期**：2026-10-09｜**授权**：用户「继续修复」

**前置**：`route-task.ps1` ⇒ UNROUTED ⇒ **语义兜底**判定适用面为 UI/LOC（上屏计数）与 TEST（可达性自检）；`verify-cluster.ps1 -Blocking` ⇒ `集群自检: PASS`（runId `cab759d1…`）。

## 1. 本批消掉的两处零消费方（审计 `ECON-WIRING-AUDIT-006` ⑤）

| 符号 | 处置 | 证据 |
|---|---|---|
| `QueryFoodPricesPublished` | **接线上屏**：总览"报价"行的食品计数改走该查询 | 调用点 `Dialog_PawnEconomy.Overview.cs:61`（定义 `EconomyConsumption.cs:112`） |
| `OwnershipCount` | **接线上屏**：车间容量行追加「工作占用=N」 | 调用点 `Dialog_PawnEconomy.Overview.cs:125`（定义 `EconomyWorkOwnership.cs:60`） |

⚠ **刻意只替换食品那一项**：该查询只覆盖食品报价，工资/服务计数保持内联重算 —— 两者语义不同，不得因"看起来同源"连带替换（前置 UI 席明确要求）。

## 2. 过期注释修正

`EconomyEmployment.cs` 的学习账注释原称"**落盘未接通**"，与事实不符（`EconomyLearningPersistence` 已在 `EconomyRuntime.ExposeData` 的 Saving 段导出、`PostLoadInit` 恢复）。已按实际链路改写并注明旧档语义（无节点 ⇒ 空账，SAVE-003）。**这类"注释比代码旧"是后续核查的误导源，属真实维护成本。**

## 3. 零消费符号裁定表（本批交付的核心产物）

| 符号 | 裁定 | 理由与约束 |
|---|---|---|
| `InstitutionToolCatalogue`（M10 机构工具模板） | **接线（待排期）** | 设计 §3 明列机构需工具模板；当前仅测试消费。接线落点应在机构/政策面（显示该机构可用的工具与是否需库存），**不是**删除 —— 它承载设计语义 |
| `QueryZoneWagePolicy`（按车间区取工资报价） | **接线（待排期，属 M6）** | 现状签约工资取自 `position.Wage`，未走政策报价；接线会改变签约金额语义 ⇒ 必须与 M6 的"工资承诺"口径一起设计，不能顺手改 |
| `QueryLegacyReconciliation`（旧账对账报告） | **接线（待排期，属 B2.4.3 验收内容）** | 该报告是验收单元明确项；需治理面/总览行 + 报告字段上屏，且**只读**（方法本身已禁在构建报告时调用 Account/Person） |
| `MarkPerceptionDirty`（事件驱动感知失效） | **接线（需一轮设计）** | 现状感知靠 150/250/600 tick 轮询；事件驱动失效需要明确"哪些事件后标脏、标哪些组"——猜着接会把缓存一致性问题引进来，故先不接 |
| `ShadowTransactionKernel`（M11 真实转账内核） | **待 PM 裁定（架构级）** | 实盘资金流全部走 `FundedSettlementLedger`/`SettlementIntent`；"是否把实盘切到新内核"是架构决策，不属修复批 |

## 4. 验证基线（全部实跑）

| 项 | 判据 | 结果 | 证据 |
|---|---|---|---|
| 编译 | 输出无 `error` | **0 错 0 警** | `_scripts/compile_with_sdk.ps1` |
| 全量离线测试 | dotnet test（net48） | **1187 通过 / 0 失败 / 1 跳过（总 1188）**（本批无新增用例，纯接线 + 注释 + 文案） | `Tests/TestResults/U6A5-Closeout-V1.trx` |
| **调用点可达性（强制自检）** | 两处零消费方必须各有生产调用点 | `Overview.cs:61` / `Overview.cs:125` | 调用点检索输出 |
| 部署 | deploy-to-game.ps1 | Defs 36 / Languages 66 / Assemblies 1 synced | 脚本输出 |
| **实机** | 总览两行计数渲染 | **NOT EXECUTED** | — |

## 5. 剩余（不在本批）

周期复核覆盖面 4/5 类（Treatment 需"真实执行者上下文"来源设计）· 机构级读数绑定 · M7 预约与部分结算（U6-C）· 照护摄取（U6-B2，需 ARCH + EXC）· 实机验收（`CHG-20261008-ECON-ACCEPT`）。

---
AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
