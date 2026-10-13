---
change_id: CHG-20261009-ECON-A6-TOOLS
date: 2026-10-09
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedNotDeployed
scope: U6-A6（第 1 项）—— 机构工具模板接线上屏（消除 `InstitutionToolCatalogue` 的零消费方）；**按用户指令本轮不部署**
rule_ids: AI-001, AI-003, AI-011, AI-014, GOV-006, GOV-009, GOV-011, GOV-012, GOV-013, UI-001, UI-004, LOC-001, LOC-002, LOC-003, TEST-001
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Policy.cs
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

# U6-A6 · 机构工具模板接线上屏（第 1 项）

**ChangeID**：`CHG-20261009-ECON-A6-TOOLS`｜**日期**：2026-10-09｜**授权**：用户「批量批准，先进行缺口修复，等全部修复再同步 dll」

## 1. ⚠ 部署纪律变更（本批起生效）

用户指令：**修复期间不逐批部署**，全部修复完成后**统一同步一次 DLL**。故本批 `state = ImplementedNotDeployed`：

- 照常执行：编译 / 全量测试 / 变更标注 / 调用点可达性自检 / GATE-DELTA；
- **不执行**：`deploy-to-game.ps1`。发布区 DLL 仍为 U6-A5 构建（SHA `44F1A1BD…`）；
- 后果与义务：`19-U6_实机验收清单` 的 §0 校验哈希**必须在最终统一部署后更新**，否则用户会据旧哈希误判"跑的不是本批"。

## 2. 改动

| 文件 | 改动 |
|---|---|
| `Dialog_PawnEconomy.Policy.cs` | 政策行新增「工具: …」（`ToolLabel(kind)` → `InstitutionToolCatalogue.ForKind`）；最多列 3 个模板、其余 `+N` 收尾、需库存的模板追加「（需库存）」标记。**声明性事实**：只显示该机构该有的工具与库存需求，不产生建筑/工单/派工 |
| `Languages/{zh,en}/Keyed/PawnEconomy.xml` | +14 键（`Policy.Tool`、`Policy.RequiresStock`、12 个模板名，命名空间 `PC_PEA.Policy.Tool.*`），zh/en 各 12 个模板键，**逐键对齐** |

## 3. 消除的零消费方（审计 `ECON-WIRING-AUDIT-006` ⑤ 之一）

`InstitutionToolCatalogue`（12 个模板）此前**只有测试消费**；现由政策页渲染链消费：`DrawPolicyRow` → `ToolLabel` → `ForKind`/`Of`（调用点 `Dialog_PawnEconomy.Policy.cs:113/179/185`）。

## 4. 验证基线（全部实跑）

| 项 | 判据 | 结果 | 证据 |
|---|---|---|---|
| 编译 | 输出无 `error` | **0 错 0 警** | `_scripts/compile_with_sdk.ps1` |
| 全量离线测试 | dotnet test（net48） | **1187 通过 / 0 失败 / 1 跳过（总 1188）** | `Tests/TestResults/U6A6-ToolCatalogue-V1.trx` |
| 调用点可达性（强制自检） | 工具目录必须有生产调用点 | `Policy.cs:113`（行文本）/`:179`（`ForKind`）/`:185`（`Of`） | 检索输出 |
| 双语键 | 12/12 模板键中英对齐 | `zh=12 en=12` | 检索输出 |
| 部署 | **按指令不部署** | 发布区仍为 `44F1A1BD…` | — |
| DELTA | `gate-runner.ps1 -Base HEAD -ExcludeFromFile .codebuddy/tmp/u6a6-out-of-scope.txt -Blocking` | 见 §6 | 脚本输出 |
| 实机 | 政策行「工具」渲染 | **NOT EXECUTED** | — |

## 5. 未做（U6-A6 剩余项，按裁定顺序）

| 项 | 为什么还没做 |
|---|---|
| `QueryZoneWagePolicy` 接线 | 会改变**签约金额语义**，必须与 M6「工资承诺」口径**一起设计**（已裁定，不可顺手改） |
| `QueryLegacyReconciliation` 接线 | 需要先定"报告哪些字段上屏"（属 B2.4.3 验收内容），不能只贴一行摘要 |
| `MarkPerceptionDirty` 接线 | 需一轮设计："哪些事件后标脏、标哪些组"——猜着接会引入缓存一致性缺陷 |
| `ShadowTransactionKernel` | 架构级（是否把实盘切到新内核），待 PM 裁定 |

## 6. DELTA 与收尾

（由 `gate-runner.ps1` 输出回填；部署统一在"全部修复完成"轮执行。）

---
AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
