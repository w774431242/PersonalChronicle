---
change_id: CHG-20261010-VANILLA-JOBS
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedAndDeployedByThisBatch
scope: Phase 1 —— 劳务站识别玩家的原版工作动作（砍伐 / 采矿）并作为可承接的「工作合同」；完成计件由国库付酬
rule_ids: AI-001, AI-003, AI-005, AI-011, GOV-003, GOV-005, GOV-009, GOV-010, GOV-012, ARC-001, ARC-013, DATA-003, SAVE-001, UI-003, LOC-001, LOC-005, PERF-001, PERF-002, TEST-001, COMP-001, DELTA-001
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyVanillaWorkSource.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyVanillaWork.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyShadowPlanning.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs
    type: M
  - path: Source/PersonalChronicle/Capture/Patch_ItemFlowPlant.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkspaceQueries.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  - path: docs/UI预览/经济机构工作台/机构工作台预览.html
    type: M
---

# 劳务站「工作合同」Phase 1 · CHG-20261010-VANILLA-JOBS

**ChangeID**：`CHG-20261010-VANILLA-JOBS`｜**日期**：2026-10-10｜**作者**：AI（CodeBuddy / DeepSeek-V4.1-Flash）
**需求（用户原话）**：「劳务站必须能否识别玩家的动作，然后放入劳务站作为工作合同。不论是原版还是第三方 mod 的工作动作」
**前期裁决**：`pc-orchestrator`（AI-001 + 架构 + 分期）与 `pc-compat-auditor`（通用识别三路线对比）两份只读裁决；用户以「往我的预期方向修复」批准推荐组合。
**Save 变更**：**无**（零新字段、零 schema、零 Def 变更）；**Harmony 新增挂点**：**零**（只改既有 patch 的一个判据）。

---

## 一、三层根因（修复前均已逐行取证）

| # | 现象 | 根因 | 证据 |
|---|---|---|---|
| 1 | 砍伐等原版工作不进劳务站 | 岗位链硬要求"绑定车间区 + 活车间区 + 班次" ⇒ 只覆盖制造岗位 | `Planning/EconomyInstitutionWorkSource.cs:41-44` |
| 2 | 即便上报也不显示 | 劳务站读模型读的是 **legacy 岗位表**，而真实合同在新就业表 | `EconomyWorkspaceQueries.cs`（旧版 LaborOffice 分支） |
| 3 | 计酬口径混入作物 | `Chop` 同时覆盖"伐木木材"与"农田收获"（两者都走 `JobDriver_PlantHarvest`），原判据未区分 | `Capture/Patch_ItemFlowPlant.cs`（旧版，仅判 `mode == Chop`） |

## 二、改动清单（9 文件）

| 文件 | 类型 | 摘要 |
|---|---|---|
| Planning/EconomyVanillaWorkSource.cs | A | `EconomyVanillaWorkSource`（读 `DesignationManager` 产出工作机会）+ `EconomyCompositeWorkSource`（组合源；任一子源不完整 ⇒ 整体不完整） |
| EconomyVanillaWork.cs | A | partial：`VanillaWorkTypes`/常量、key 构造与解析（`vanilla:{workType}:{anchorKey}`）、`QueryVanillaWorkInstitutions`、`QueryVanillaWorkBacklog`（60 tick 缓存 + 有界早退 ≤64）、`TrySignVanillaWork`、`EvaluateVanillaContractExit`、`QueryVanillaContractRows`、`CountVanillaContracts`、`WorkTypeLabel` |
| EconomyShadowPlanning.cs | M | `WorkSource` 由单一机构源改为组合源（车间岗位 + 原版工作） |
| EconomyEmployment.cs | M | `TrySignForOpportunity` / `EvaluateContractExit` 各加 **key 前缀分派**（`IsVanillaOpportunity`） |
| Capture/Patch_ItemFlowPlant.cs | M | 上报判据加 `IsTree`：只有**树**计为 `PlantCutting`，作物收获不再混入 |
| EconomyWorkspaceQueries.cs | M | 劳务站分支重写：KPI=砍伐待办/采矿待办/承接人/未付工资；明细=本图待办 + 承接中合同；**不再读 legacy 岗位表** |
| Languages/{CN,EN}/Keyed/PawnEconomy.xml | M | 新增 6 条 `PC_PEA.Workspace.*`（中英同批）；删除 1 条因改版而孤儿的键 |
| docs/UI预览/经济机构工作台/机构工作台预览.html | M | 蓝本同步（劳务站 KPI 与明细按实装重写） |

## 三、关键设计

1. **识别零挂点、零轮询**：玩家动作在 `DesignationManager.designationsByDef`（`DefMap`，对任意 Def 含第三方自动定容）里已是权威状态；读取经 `SpawnedDesignationsOfDef` 有界枚举 + 60 tick 缓存。
2. **合同形态**：`PositionKey = vanilla:{workType}:{anchorKey}`（一次定型）；一纸合同覆盖该工种当前待办，件数 `N = min(待办, 硬顶 32, 国库可用/单价)` ⇒ **部分承接**；完成由既有证据链逐件核销。
3. **不新建资金链**：签约 `Employment.Sign`、付款 `AcceptEvidence → WageObligation → TrySettlePayable`（幂等 / 欠薪补付 / 读档保留）。
4. **发布方 = 劳务站**：要求 `LaborOffice` + 政策 `Open` + `PublishesWage` + 营业时段；**不要求绑定车间区**（原版工作与车间区无关）。
5. **退出分派**：原版工作合同没有区/班次，走专属判据；否则 `parsed=false` 会在读档后第一帧把合同误清退。
6. **纠偏**：`mode == Chop && def.plant.IsTree`（`IsTree` = `harvestTag=="Wood" || forceIsTree`，已核实）。

## 四、验证证据

| 项 | 结果 | 证据 |
|---|---|---|
| 编译 | **0 错 0 警** | `compile_with_sdk.ps1`（`BuildStamp 2026-10-10 11:15 UTC`） |
| 离线测试 | **1285 通过 / 0 失败 / 1 跳过** | `dotnet test`（EXIT=0；较上批 +13） |
| Cecil 静态核实 | **PASS** | 2 个新类型命中；`refs` 反查接线：`TrySignVanillaWork ← TrySignForOpportunity`、`EvaluateVanillaContractExit ← EvaluateContractExit`、`QueryVanillaWorkBacklog ← TrySignVanillaWork + QueryWorkspace`、`CountVanillaContracts ← QueryWorkspace`、`IsVanillaOpportunity ← 两个分派点`、`TryParseVanillaOpportunity ← 3 处`、`VanillaOpportunityKey ← ReadOpportunities` |
| GATE-ARCH | **PASS**（无 P0/P1） | 分层合规（机会源不直读 designation，经 owner 委托）、无环、既有链行为等价（车间 key 前缀必非 `vanilla:`）、缓存/性能有界、除零与溢出已护、文件行数全 ≤800 |
| GATE-UI | **PASS** | 窗口零特判、行族唯一实现（`EconomyRowList.Draw` 仅 2 个调用者）、KPI 与明细同源、待办 0 仍显示、截断有悬停 |
| GATE-LOC | **BLOCKED → 已收口转 PASS** | 两项例外本批修复：① `VanillaHint` 文案"第三方 Mod 的工作同理"**超出实装**（识别集封闭为两个原版工种）⇒ 改为限定表述；② `Workspace.Kpi.Contracts` 改版后**孤儿** ⇒ 中英两处删除 |
| ARCH 收口项 | 已修 | R1（P3）：`"PlantCutting"/"Mining"` 字面量散落 7 处 ⇒ 集中为 `VanillaWorkTypePlantCutting/Mining` 两常量；R2（P2）：补签约后复核（合同仍活跃 + 工作类型确实可用，否则终止并释放 —— 与 FA-11 同构） |
| 部署 | **已部署** | `deploy-to-game.ps1`（DLL 与工程一致；见交付回执） |
| GATE-DELTA | **PASS** | `gate-runner.ps1 -Base HEAD -ExcludeFromFile .codebuddy/tmp/vanillajobs-out-of-scope.txt -Annotation <本文件> -Blocking` |
| 游戏内验证 | **NOT RUN** | 见 §六 验收步骤 |

## 五、未做项（Phase 2 / 3，如实登记）

- **Phase 2**：建造 / 拆除 / 收获 / 狩猎逐工种接入（拆除预期需 1 个新挂点 ⇒ 触发 Architecture Review）；**每工种单价表**（政策加法字段 + UI）；收获的"复采序号"证据键（涉及 Save 评估）。
- **Phase 3**：第三方**新增**工作类型的声明式映射（XML Def 行）+ "未接入"如实显示 + 可选 Job 观测（只读）；手动"承接"按钮。
- **不做**：医生 / 搬运 / 清洁等**无 designation** 的自动类工作（另立项）；不接管原版派工（ThinkTree 仍负责选活）。

## 六、实机验收步骤（待执行）

1. 重启 RimWorld；启用经济系统（Mod 设置，首次注入 1000）。
2. 建劳务站 → 打开其窗口 → 点「机构政策」把**营业**打开并设置**岗位工资**（>0）→ 确认提交成功。
3. 在树上划若干「砍伐」（含"砍伐木材"）；回到劳务站窗口 → **本图待办**应显示"砍伐 N"。
4. 等待规划循环（约 1–2 秒）→ KPI「承接人」> 0，明细「承接中的工作合同」出现殖民者与 `已完成/上限`。
5. 殖民者砍倒树木 → 每棵 ×N 立刻计入合同进度，并**从国库支付**到该殖民者账户（个人账户余额上升、国库下降）。
6. 回归项：**农田收获不再计入"砍伐"**；取消砍伐指定后待办下降；国库不足时签不出合同（或按预算部分承接）；读档后合同不被清退、进度保留。

### 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖实际改动全部文件（9/9）
- [x] 清单范围 ⊆ Scope
- [x] 每个文件标注类型与摘要
- [x] Change ID 与两 Gate 结论、前期裁决可互链
- [x] 无调试代码 / TODO 残留
- [x] 已用 `gate-runner.ps1` 自动校验
