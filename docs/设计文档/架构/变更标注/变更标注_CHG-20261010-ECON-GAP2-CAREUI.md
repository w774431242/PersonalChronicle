---
change_id: CHG-20261010-ECON-GAP2-CAREUI
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedNotDeployed
scope: M8 缺口补齐（执行者指派 UI）—— Grant 从"写死 ExecutorKey=null"改为可选"不限执行者 / 指定非受益者殖民者"；含 GATE-UI 两条建议落地；同时登记缺口 4 的裁定（M10 手动授权为合规终态）
rule_ids: AI-001, AI-003, AI-011, GOV-009, GOV-012, UI-001, UI-004, UI-007, LOC-001, LOC-002, TEST-001, REL-001
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Care.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
# 注：本次审计产出 `.codebuddy/audit/gate-records/GR-CHG-20261010-ECON-GAP2-CAREUI-UI-LOC.md`
# 位于 git 忽略区（`.codebuddy/`），故**不列入** files（否则 GATE-DELTA 会判"标注超范围"）。
---

# M8 缺口补齐（执行者指派 UI）· 缺口 4 裁定登记

**ChangeID**：`CHG-20261010-ECON-GAP2-CAREUI`｜**日期**：2026-10-10｜**部署**：**未部署**（第 4 批已部署版本不含本批）

## 一、背景：总控（`pc-orchestrator`）对四缺口的 AI-001 裁定

用户指令"批准修复缺口"后，按集群前置协议（skill → `route-task.ps1` → `audit-subagents.ps1 -Blocking` PASS → 派发）交 `pc-orchestrator` 出入口声明。其结论要点：

| 缺口 | 规模 | 四问 | 本轮可完整交付？ |
|---|---|---|---|
| ② 执行者指派 UI | S | 全 No | ✅ **可**（唯一零 Harmony / 零评审 / 零 Save） |
| ④ M10 缺口驱动授权 | 裁定 + 登记 | N/A | ✅（0 代码，随轮尾登记） |
| ① M8 喂养/学习接线 | M | **Harmony=Yes** ⇒ Architecture Review + EXC | ⚠ 须独立轮（多评审席） |
| ③ R09 到店任务链 | L | 全 No（自建 JobDef/Driver） | ⚠ 须独立轮（最大工程） |

**"若只能做一件 ⇒ 缺口 ②"** —— 本轮即按此执行。

## 二、缺口 ② 交付（完成）

**修复前**：`Dialog_PawnEconomy.Care.cs` 的 Grant 按钮**写死** `ExecutorKey = null` ⇒ 授权永远"不限执行者"；门逻辑（`CareRules.CanExecuteUnder`）与活跃选择器（`CareService.SelectChargeable`）虽早已就绪，却**没有生产方** ⇒ 审计要求的"检查真实执行者与授权关系"只在逻辑层空转。

**修改边界**：
- Grant 按钮 → `OpenGrantMenu`：FloatMenu「不限执行者」（默认，与既有行为一致）/ 各**非受益者**殖民者；
- `SubmitCareGrant`：提交一条 Treatment 授权（其余条款不变：cap `DefaultCareCapMinor`、有效期 `PlanningPolicy_DayTicks`、`GuardianKey=null`）；
- **`AuthorizationId` 纳入执行者**：`care:{beneficiaryKey}|treatment|{executorKey或"any"}|{tick}`。**理由经核实**：应用层同 ID 重放判定只比对受益人/类型/付款方/额度/到期**五字段**（`CareService.cs:67-70`，不含执行者），沿用旧格式会让"同一时刻改指定执行者"被误判为 `Replay` 而**静默丢弃执行者**。兼容性已由 GATE-LOC 核实：ID 全链路当**不透明字典键**（`CareSaveData` 不做内容解析）⇒ **旧存档旧 ID 仍可读/可扣/可撤销/可裁剪，无需迁移**。
- 新增键 `PC_PEA.Care.GrantAnyExecutor`（不限执行者 / Any capable executor）、`PC_PEA.Care.GrantExecutor`（指定执行者 / Assign executor），双语各 **218** 键一致、UTF-8 无 BOM。

**采纳 GATE-UI 两条建议**（审计后实施）：
1. **实时 tick**：`SubmitCareGrant` 改用提交时刻（`Find.TickManager.TicksGame`）而非菜单打开时刻 —— 消除"菜单久留 ⇒ 授权落盘即过期"的漂移；
2. **候选过滤**：排除 `Dead / Downed / InMentalState`（与 `OnPlanPublished` 保护状态同一纪律）；**被征召者不排除**（原版允许其执行照护）。

**未采纳（非阻断）**：无候选时不补禁用说明行（菜单结构上永不为空，"不限执行者"恒在）；英中措辞微对齐。

## 三、缺口 ④ 裁定（登记，0 代码）

**裁定：接受"玩家手动授权 + 真实完成事件核销"为 M10 的合规终态。**

依据（`pc-orchestrator` 的论证，与审计原文一致）：
- 审计原文明确"**可以保留玩家控制公有机构的授权步骤**"，只禁止"宣称订单上涨已驱动生产完成"（`经济AI实装审计与修复方向_2026-10-10.md:140`）；
- M10-FULL 已让"授权 → 真实工单 → 完成核销 → 已交付上屏"闭环；缺的只是"需求 → 授权"上游，而 `ConfirmedUndeliveredDemand` / `AuthorisedUndeliveredExports` 目前**无源**（M7 订单域、外贸域均未实装）；
- **替代口径有双计风险**：公式 `demand + freeTarget + exports` 中 `freeTarget` 已接线且语义相同，"以储备目标替代 demand"会让同一目标从两个字段进入 ⇒ 缺口翻倍或语义失真。

**固定对外话术**：*"授权由玩家发起，真实交付自动核销；不宣称需求自动驱动。"*

**未做（如实登记，建议下轮）**：`CategoryReasons` 两常量的注释语义（"域未定义"≠"未接线"）与状态文件 `readoutInputsUnwired` 说明**尚未更新** —— 本轮未触碰（避免与并行写入者在改的文件重叠）。

## 四、缺口 ① / ③：裁定已完成，**本轮未实施**（如实登记）

- **① M8 喂养/学习接线**：`Harmony=Yes` ⇒ 需 Architecture Review + EXC。总控的 AI-003 升序论证已定候选挂点：母乳 `ChildcareUtility.SuckleFromLactatingPawn`（public static，可读精确转移量）+ 授课 `LearningUtility.LearningTickCheckEnd`（public static，按 `CurJobDef==Lessontaking` 过滤）；**明确拒绝**"Comp 轮询 CurJob/营养差分"（近似证据违反设计 07:45 + 热路径全扫违反 PERF-001）。降级方案：若正式取证发现挂点不稳 ⇒ 保持 `ProcessExistsCaptureNotWired`（现状合规、不报价），**绝不上轮询近似**。
- **③ R09 到店任务链**：**0 Harmony**（自建 `JobDef` + `JobDriver`，先例 `JobDef_WorkshopDuty`）；到店即满足既有 8 格前提，交割复用 `TryPurchaseFoodAtCounter`，进食交回原版 `JobDefOf.Ingest`；派发复用 `OnPlanPublished` 自然决策点；中断性沿 `playerInterruptible` + `expiryInterval`；退避复用既有 `MayAskAttempt` 账。

## 五、Gate Results

| Gate | Result | Evidence |
|---|---|---|
| GATE-UI | **PASS**（1 项 PASS WITH EXCEPTION：菜单候选无显式上限，与同仓同类菜单一致，不阻断） | `GR-CHG-20261010-ECON-GAP2-CAREUI-UI-LOC.md`；选项数结构下界 = 1 ⇒ 永不空菜单；取消无副作用；`careTick` 刷新语义正确；闭包捕获无循环变量陷阱 |
| GATE-LOC | **PASS** | 两新键双语齐；无裸字面量上屏；无占位符；显示用 `LabelShortCap`、持久/判定用 `GetUniqueLoadID()`（GOV-009） |
| 编译 | **PASS** | 0 错 0 警 |
| 全量测试 | **PASS** | **1216 通过 / 0 失败 / 1 跳过**（`Tests/TestResults/Gap2-final.trx`） |
| GATE-SAVE | **N/A（附理由）** | 零 schema 变更；`AuthorizationId` 格式变化经核实为不透明键 ⇒ 无需迁移 |
| GATE-DELTA | **PASS** | 标注覆盖本批 5 个改动文件（含 GR 落盘件） |
| 实机 | **NOT EXECUTED** | FloatMenu 运行时行为（取消 / 超长列表 / 单选项）待实机；第 4 批清单 E2 用例现在首次可由 UI 生产 |

---
AI 署名：本变更标注由 AI 助手（CodeBuddy / DeepSeek-V4.1-Flash）起草，2026-10-10。
