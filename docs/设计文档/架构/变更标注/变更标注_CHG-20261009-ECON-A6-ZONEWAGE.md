---
change_id: CHG-20261009-ECON-A6-ZONEWAGE
date: 2026-10-09
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedNotDeployed
scope: U6-A6（第 4 项）—— 车间区已发布工资报价接线上屏（消除 `QueryZoneWagePolicy` 零消费方）；**不改签约金额语义**（该口径待 PM 裁定）；按用户指令修复期不部署
rule_ids: AI-001, AI-003, AI-011, AI-014, GOV-006, GOV-009, GOV-011, GOV-012, GOV-013, UI-001, UI-004, LOC-001, LOC-002, LOC-003, PERF-002, TEST-001
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

# U6-A6 · 车间区工资报价上屏（第 4 项）

**ChangeID**：`CHG-20261009-ECON-A6-ZONEWAGE`｜**日期**：2026-10-09｜**授权**：用户「继续」

## 1. 为什么只做"显示"而不动签约金额（本项的核心裁定）

`QueryZoneWagePolicy(mapId, zoneId, out anchorKey)` 的语义（已核实 `EconomyInstitutionPolicy.cs:113-125`）：取**绑定该车间区且已发布工资**的机构中 anchorKey 最低者 —— 即"该区对外的工资报价"。

**现状**：M6 签约金额取自岗位自身（`position.Wage`），**不取**该区报价。若直接把签约改成"取区报价"，等于单方面改变**合同金额语义**（谁决定工资、合同如何承诺、已签合同是否追溯），属 M6「工资承诺」口径，**必须 PM 裁定**，不可由实现方顺手改。

**本批做法**：只把该查询接到**只读聚合上屏**（确实需要"每区一个报价"的确定性解法，故必须走同一查询而不是 UI 自算），**金额语义零改动**。

## 2. 改动

| 文件 | 改动 |
|---|---|
| `Dialog_PawnEconomy.Overview.cs` | 新增「区工资报价」行：从已发布政策里取 distinct `ZoneId`（升序），**最多 3 个区**（每区一次有界查询 ⇒ 最坏 3×机构数），显示 `#<区>=<银/人> (<机构短键>)`；超过 3 个附「显示 N/总 M」；无任何已发布区工资时显示「未发布」 |
| `Languages/{zh,en}/Keyed/PawnEconomy.xml` | +1 键 `Overview.ZoneWage`（中英各 1） |

## 3. 消除的零消费方（审计 `ECON-WIRING-AUDIT-006` ⑤ 之四）

`QueryZoneWagePolicy` 此前**零消费方**；现由总览页消费（`Overview.cs:141/158`；定义 `EconomyInstitutionPolicy.cs:114`）。审计第五条的 5 项里，**第 4 项（区域工资）就此闭环为"只读接线"**。

## 4. 待 PM 裁定（本批刻意不做，已登记）

> **合同金额是否应取"该车间区已发布工资报价"而不是岗位自身的 `position.Wage`？**
> - 若"是"：需同时定 **①新签/在签合同是否追溯 ②无报价区怎么签 ③改价是否可被雇员拒绝**（三条都涉及"承诺"语义与既有 EMP-01~10 验收项）。
> - 若"否"：本行上屏即最终形态（报价仅作对外公示），需在 M6 分册补一句口径说明。

## 5. 验证基线（全部实跑）

| 项 | 判据 | 结果 | 证据 |
|---|---|---|---|
| 编译 | 输出无 `error` | **0 错 0 警** | `_scripts/compile_with_sdk.ps1` |
| 全量离线测试 | dotnet test（net48） | **1188 通过 / 0 失败 / 1 跳过（总 1189）**（本批无新增用例；与上批同计数） | `Tests/TestResults/U6A6-ZoneWage-V1.trx` |
| 调用点可达性（强制自检） | 零消费方必须有生产调用点 | `Overview.cs:141/158` | 检索输出 |
| 部署 | **按指令不部署** | 发布区仍为 `44F1A1BD…` | — |
| DELTA | `gate-runner.ps1 -Base HEAD -ExcludeFromFile .codebuddy/tmp/u6a6d-out-of-scope.txt -Blocking` | 见 §6 | 脚本输出 |
| 实机 | 总览「区工资报价」行渲染 | **NOT EXECUTED** | — |

## 6. DELTA 与收尾

（由 `gate-runner.ps1` 输出回填。）U6-A6 剩余：**`ShadowTransactionKernel`（待 PM 架构裁定：实盘资金流是否切到新转账内核）**。全部修复完成后统一部署一次，并更新验收清单 §0 哈希与进度源 `deployedBuildSha256`。

---
AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
