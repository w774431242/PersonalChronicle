---
change_id: CHG-20261012-ECON-LABOR2
title: 合同解锁搬运/清洁工种（红线收窄）+ 劳务核实新逻辑（产物入库即交付）—— 本轮交付第一段
date: 2026-10-12
state: CodeVerified / TestVerified（第一段；第二段「合同避让」+ 第三段「链 B」未实装）
layer: Domain/PawnEconomy/Employment（不可写清单）+ Tests + 例外登记
scope: 用户实机反馈「派了工没人接（搬运合同必失败）」的止血点；核实链重构（链 B）已完成设计裁决、本轮未实装
author: CodeBuddy / DeepSeek-V4.1-Flash
program: PROG-ECON（本轮未占用共享 change-context，见 §四）
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Employment/WorkOwnershipRules.cs
    type: M
    note: 不可写清单由四项（Hauling/Cleaning/Doctor/Firefighter）收窄为 Doctor/Firefighter；永不写 0 与只认领玩家关闭态两条硬边界不变
  - path: Tests/EconomyWorkOwnershipTests.cs
    type: M
    note: 锁定旧口径的断言订正（四项 ⇒ 两项）+ 补"玩家已开档位仍归玩家（PlayerOwned）"的牙齿
  - path: docs/设计文档/架构/EXC-2026-028.md
    type: M
    note: §1.2 例外范围变更登记（含与 EXC-2026-016 MandatoryKeep 的关系变更与"避让尚未实装"的如实标注）
---

# CHG-20261012-ECON-LABOR2 · 搬运/清洁合同解锁（第一段）

## 一、问题（实机取证）

`Player.log`：`[PC][OCCUPY] 承接失败：合同=…|Hauling|Haul|… 人选=Pierce 工种=Hauling 原因=WorkTypeUnavailable`

根因是三道判据互锁：

| # | 判据 | 位置 |
|---|---|---|
| 1 | 有合同授权**仍要求**该工种在该 Pawn 上 `GetPriority > 0`（否则当场终止合同并释放工种） | `EconomyVanillaWork.cs:574-582`（承接）、`:534-544`（新签） |
| 2 | 写优先级的通道**不可写清单含 `Hauling`/`Cleaning`** ⇒ 返回 `InvalidType`，永不写入 | `WorkOwnershipRules.cs:16`（改前）、`EXC-2026-028` §1.2 |
| 3 | 该通道只认领"玩家关闭态"（`GetPriority == 0`） | `EXC-2026-028` §1.4（未变） |

⇒ 搬运合同**必然**失败（除非玩家手动勾选搬运）。日志同时证明**工作闸门本身是生效的**（`[PC] 工作合同闸门已生效`），即"锁"没丢，丢的是"合同解锁"这一半。

## 二、本轮已交付（第一段）

`WorkOwnershipRules.NeverManaged`：`{Hauling, Cleaning, Doctor, Firefighter}` ⇒ **`{Doctor, Firefighter}`**。

- **保留**：① 本通道**永不写 0**（禁用方向只属 `EXC-2026-016` / `EXC-2026-026`）；② 只认领玩家关闭态；③ 退出按所有权还原。
- **效果**：搬运 / 清洁合同的承接复核（要求 `GetPriority > 0`）现在可以通过 —— 通道会把玩家关闭态写成 `EnabledPriority = 3` 并记台账，合同结束时按原值还原。

## 三、验证证据（实测）

| 项 | 结果 |
|---|---|
| 正式编译 | `exit=0` · **0 错 0 警** · BuildStamp `2026-10-12 15:52 UTC`（`_scripts/compile_with_sdk.ps1`；备份 `.bak_20261012235213`） |
| 全量测试 | **1640 通过 / 0 失败 / 1 跳过**（订正后的断言 + 新加的"玩家选择仍归玩家"分支） |
| 全仓影响面（**已订正**） | 该"四项"清单在 `Source/**` 中**另有 3 处同源副本**：`Domain/AutoAssignPriority.cs:32`（保护项）、`Automation/MapComponent_WorkshopManager.Runtime.cs`、`WorkAuthority.cs:43`（默认白名单）—— 本批**只收窄本通道那一份**，其余副本语义不同（保护项 floor / 白名单），不随本批改变（单源常量化为下一轮 P2）。⚠ 原稿写的"grep 0 命中"是**我 grep 模式过窄造成的假阴性**（只匹配四项连续同序），已更正 —— 教训：影响面结论不能只靠单条正则 |
| 断言语义订正 | 旧断言 `MandatoryKeepTypesAreNeverManagedInEitherDirection` 编码的是旧口径（四项皆不可写）⇒ 按新口径改名并订正为"两项不可写 + 两项可认领 + 玩家选择不放宽" |

## 四、未实装 / 未验证（如实登记，不得视为完成）

| 项 | 状态 |
|---|---|
| **合同避让（fail-closed）** | **未实装**。用户 2026-10-12 裁决：格位被写 0 通道（`EXC-2026-016` 工坊接管 / `EXC-2026-026` 离班停工）拥有时**不写**并给稳定原因。台账已定位（`Data/ChronicleGameComponent.WorkTakeover.cs` 的 `WorkTakeoverLedger` + `TakeoverEntry.WorkTypeDefName`），但**判据与稳定原因的接线未做** ⇒ 未实装期间的表现：合同写入后被写 0 通道夺走格位 ⇒ FA-11 复核失败、合同当场终止（**可观测、不静默**，非数据损坏） |
| **链 B（核实新逻辑）** | **未实装**。用户 2026-10-12 裁决口径 = **产物入库即交付（B1）**：复用 Haul 的"真实放入合格储存区"实收判据（`Patch_VanillaHaulWork.cs:130-131` 先例），产物进入殖民地储存区/仓库即算交付 ⇒ 不搬、不到站、守恒成立。需新增"入库即交付"捕获出口 + 批次闭合 + 分次放款 + 对账自愈（自愈需"批次缺失 ∧ 地图无实例 ∧ 无回执"三 ∧ 可判定判据），并**新登记例外**（付款与实物脱钩属资金/凭证侧口径变更，且将推翻 `CB-ECON-003` 的两条既有裁决：部分交付不放款、对账中不自动重入）⇒ 必须带守恒回归，不能半途交付 |
| **实机验证** | **NOT RUN**：新档派 `Hauling` 合同应有人承接并按证据计酬（该工种本就走 `EvidenceOnly`，无需搬产物） |
| **部署** | 见收尾回复（本轮已部署：游戏需重启生效） |
| 互链更正（EXC-016 / 026 / 025 的"四项同一条红线"表述） | **未做**：本轮只登记了 `EXC-2026-028` 侧的关系变更；其余三处的同步更正列入下一轮（避免在未实装避让前留下"已完成"的假陈述） |

## 四之二、Gate 结论（2026-10-12 跑 · 用户要求"须 ARCH+COMP 复核"）

| Gate | 席 | 结论 | 关键发现 |
|---|---|---|---|
| 入口/AI-001 | `pc-orchestrator` | Hardcoding=**Yes**（清单表在代码内）⇒ **触发 Architecture Review**；`EXC-2026-028` 属**范围变更**（非新登记）；**链 B 需新 EXC**（资金/凭证侧口径变更，且将推翻 `CB-ECON-003` 两条既有裁决） | 给出 016/026 跨台账互踩面与"付款与实物脱钩"的守恒风险 ⇒ 促成两项补充裁决（合同避让 / B1 入库即交付） |
| ARCH | `pc-arch-gatekeeper` | **PASS WITH EXCEPTION** | 四问全 No；写入路径仍 fail-closed（6 类仍被拒：空 defName / 未初始化 / 能力禁用 / 当前≠0 / 已有台账 / 非受管）；**订正本轮 3 处不实表述**（见下） |
| COMP | `pc-compat-auditor` | **PASS WITH EXCEPTION** | 无第三方硬引用（COMP-001 通过）；例外见下 |
| DELTA | 主 Agent（自洽范围外口径） | **PASS**（3 文件 · 提示 1 条） | 见 §五 |

**例外项（登记，按优先级由下一轮处理）**

1. **P1** 合同避让未实装（台账 `WI-021`）—— 真实后果是「活跃合同 + 工种档位 0」的**静默滞留**（`CanStillWork` 不看 `GetPriority`，`EconomyEmployment.cs:336-344`），非"当场终止"（本标注与 `EXC-2026-028` 已订正）。
2. **P1** 链 B 未实装（台账 `WI-020`，口径已裁决 = 产物入库即交付）。
3. **P1** 第三方工作类 Mod（WorkTab / Pick Up And Haul 类）**无检测、无适配** ⇒ "存在时只做条件适配"未证明；需一次实机或明文登记"不检测、接受档位被覆盖"。
4. **P1** `WP_Common_Maintenance` → 搬运/清洁的 **dormant 第二写非 0 通道**（`Defs/WorkshopProcesses.xml:277-281` + `Automation/WorkshopPostLandingService.cs:103,120`）未登记：`Defs/WorkshopTypes.xml` 只要引用它即可接线。
5. **P2** 同源四项在 `Source/**` 另有 3 处副本 ⇒ 收成单源常量（`GOV-005`/`BASE-010`）。
6. **P2** 离图 / 被销毁 Pawn 的"已写 3、无人还原"稳定态与台账收敛口径（`EconomyEmployment.cs:430-445` 明写不强改）。
7. **P2** `CanStillWork` 补 `GetPriority` 分支（让"合同活跃但工种被夺"可被退出判据发现，而非静默滞留）。
8. **P3** `EconomyWorkOwnership.cs:25` 幂等分支误用 `PlayerOwned`（应为 `AlreadyActive`）。
9. **P3** `AutoAssignService.cs:25` 注释称"写 0 唯一调用点仍在 WorkTakeover.cs"，与 `:192` 事实不符（登记口径 `GOV-007`）。

## 五、并发与共享上下文（如实）

本轮**未**改写 `.codebuddy/audit/change-context.json` 与 `.codebuddy/audit/scope/**`（当时由并行会话 `CHG-20261012-ECON-HAULCMD` 等持有）；GATE-DELTA 使用自建范围外清单（=`全部 git 改动 − 本批文件 − 标注本身`）。

*本标注由 AI 助手 · CodeBuddy（模型：DeepSeek-V4.1-Flash）于 2026-10-12 生成；"已完成"结论均以编译/测试实测为准，未实装项已在 §四 明确标注。*
