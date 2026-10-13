---
change_id: CHG-20261010-ECON-VANILLA-CUT
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: Implemented
scope: 用户需求「割除任务没有真实数量」+「工作合同允许玩家修改并派发多个不同数量的合同」
rule_ids: AI-001, AI-005, GOV-005, GOV-009, GOV-010, DATA-003, UI-006, LOC-001, PERF-001, TEST-001, DELTA-001
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyVanillaWork.cs
    type: M
  - path: Source/PersonalChronicle/Capture/Patch_ItemFlowPlant.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkspaceQueries.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyVanillaWorkSource.cs
    type: M
  - path: Source/PersonalChronicle/Application/PawnEconomy/WorkspaceView.cs
    type: M
  - path: Source/PersonalChronicle/Application/PawnEconomy/Employment/EmploymentService.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs
    type: M
  - path: Tests/EconomyEmploymentTests.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
---

# 割除识别 + 多合同派发 · CHG-20261010-ECON-VANILLA-CUT

**ChangeID**：`CHG-20261010-ECON-VANILLA-CUT`｜**日期**：2026-10-10｜**作者**：AI（CodeBuddy / DeepSeek-V4.1-Flash）
**用户需求（原话）**：「第一。割除任务没有真实数量。第二：工作合同允许玩家修改并派发多个不同数量的合同，如１００个割除可以修改派发１０个合同。按我的预期修复」

---

## 一、问题 1：割除恒为 0 —— 根因与修复

### 根因（静态核实，非推测）
原版对植物有**两个独立 designation**（`Data/Core/Defs/Misc/Designations/Designations.xml:50` = `CutPlant`，`:56` = `HarvestPlant`）：
- 玩家对草/灌木用的**割除**工具 `Designator_PlantsCut` 打的是 `CutPlant`（Cecil：`refs CutPlant` → `Designator_PlantsCut::.ctor`）；
- 旧实现只统计 `HarvestPlant` 中的树 ⇒ **玩家对草/灌木下达的割除恒为 0**（计数与计酬双缺）。

### 修复 1 · 计数（`EconomyVanillaWork.cs`）
`CountVanillaBacklog` 的 `PlantCutting` 分支改为 `CountTreeHarvest(manager, cap) + CountOf(manager, DesignationDefOf.CutPlant, cap)`。

### 修复 2 · 计酬（`Patch_ItemFlowPlant.cs`）
- **上报移到** `harvestedThingDef == null` 检查**之前**：割除的草/灌木常无产物，旧写法在"无产物"处提前 return ⇒ 即使识别也永远拿不到钱；
- 条件由 `mode == Chop && IsTree` 放宽为 `(mode == Chop && IsTree) || mode == Cut`。
- **依据（Cecil IL）**：`JobDriver_PlantCut::get_PlantDestructionMode` = `ldc.i4.3; ret`（枚举 `Smash=0,Flame=1,Chop=2,Cut=3` ⇒ `Cut`）；其 `PlantWorkDoneToil()` 只被覆写为 `DestroyThing`，而 `PlantCollected` 的唯一调用点挂在**工作 toil** 的 `tickIntervalAction`（`JobDriver_PlantWork/<>c__DisplayClass11_0::<MakeNewToils>b__1`）⇒ 割除同走本 Prefix。
- **幂等**：证据键 `"vanilla:{workType}:{target.GetUniqueLoadID()}:{pawnKey}"`（`EconomyEmployment.cs:134-137`）⇒ 同一对象同一 Pawn 只计一件，重复回调被去重（`Smash`/`Flame` 仍不计酬）。

## 二、问题 2：玩家可修改并派发多个合同

### 2.1 同工种**待办预留**（`EconomyVanillaWork.cs`）
新增只读 `VanillaJobsCommitted(map, workType, anchorKey)` = Σ(活跃 vanilla 合同 `MaxJobs − CompletedJobs`)，遍历路径与既有 `QueryVanillaContractRows` 同源（本图自由殖民者 + `QueryActiveContract`），**零新增 Save 读取 API**。
签约件数 `maxJobs = min(max(0, 待办 − 已派发), 每份件数)`；`≤ 0` 时以 `"JobsAlreadyCommitted"` 提前返回（与 `InsufficientBacking` 区分）。
守恒性：完成一件 ⇒ designation 消失（待办−1）∧ `CompletedJobs+1`（已派发−1）⇒ 同向自平衡，不双重计数。

### 2.2 玩家可调**每份件数**（运行期设定）
`VanillaJobsPerContract`：预设序列 `{1,2,4,8,10,16,24,32,48,64}` 间步进（默认 32；含 **10** ⇒ "100 件拆 10 份"一步可达）。
- **不入存档**：存读档回默认 32（如实告知，不假装持久化；故零 Save schema 变更）。
- **出口**：`StepVanillaJobsPerContract(±1)`；工作台行动区 `每份 −/+`（`WorkspaceVerb.VanillaJobsDown/Up`，就地写，同 `Reverify` 型）。
- **同源**：`economy` 签约与 UI 显示都读同一个 `VanillaJobsPerContract`，无第二处定义。

### 2.3 修 `Sign` 的判据错配（`EmploymentService.cs`）
原判据 `CountActiveByPosition(positionKey) >= request.MaxJobs` 把**件数**当**份数上限** ⇒ 件数型合同在小余量时误拒合法签约（余 4 件、已有 4 份 ⇒ `4 ≥ 4` ⇒ 第 5 份被拒、剩余工作派不出去）。
改为**显式** `PositionCapacity`：`> 0` 才裁决（车间岗位传 `EmploymentPolicy.MaxJobsPerPosition`，语义与行为不变）；件数型合同传 `0` = 不限份数（总量由待办预留约束）。

### 2.4 UI（`EconomyWorkspaceQueries.cs` / `Window_InstitutionWorkspace.cs`）
工种行显示 `待办 N · 已派发 M · 可派发 K · 每份 X · 工资 W`；劳务站行动区新增 `每份 −` / `每份 +`。可派发量用的正是签约公式（UI 与行为同源）。

## 三、契约演进与测试

- `SignRequest.PositionCapacity` 成为**显式**语义参数（`Tests/EconomyEmploymentTests.cs` 的工厂同步声明 `PositionCapacity = maxJobs`，既有用例语义逐字保持）；
- 新增 `CHG20261010_ZeroPositionCapacityLetsPieceworkContractsStack`：验证件数型岗位可并存多份（旧判据会误拒的第 5 份场景）。

## 四、Gate 结果

| Gate | 结果 | 要点 |
|---|---|---|
| **ARCH** | **PASS**（四硬触发全 No；9 项全过） | 独立只读审查：无新增 Harmony / Hardcoding / Cross-Mod 引用 / 静态可变状态；分层与依赖方向合规；`VanillaJobsCommitted` 与同族方法同构；性能可接受（UI 已被 60 tick 快照节流；签约路径**不应**缓存，否则"显示与行为异源"）；`"JobsAlreadyCommitted"` 无 UI 消费者，与既有 reason 同模式 |
| **DATA/SAVE** | **N/A（无持久化面变更）** | 每份件数为运行期设定；未改任何 Scribe 字段、schema 与 Def |
| **TEST** | **PASS（逻辑改动全绿）** | 逻辑改动后一次全绿：编译 0 错 0 警 + **1286 通过 / 0 失败 / 1 跳过**（含新增用例） |
| **DELTA** | **PASS** | 覆盖 11 个改动文件，无遗漏/超范围/残留 |

**ARCH 的 Cut 计酬边界（如实登记，判定可接受）**：会触发计酬的 Cut 路径除玩家割除工具外，还含 `CompAutoCut`（自动割除）、播种清障（`WorkGiver_GrowerSow::JobOnCell`）、建造清障（`GenConstruct`）、屋顶清障、种树清障 —— 它们都是**真实的 PlantCutting 劳动**，且只对有该工种活跃合同的 Pawn 出账；"Cut→Chop 双付"不可能（Cut 完成即 `DestroyThing`，植物不复存在；且证据键同植物去重）。

## 五、未执行 / 未覆盖

1. **NOT RUN · 实机验证**：沙箱无游戏进程。需重启 RimWorld 后核对：
   - 对草/灌木下达**割除** ⇒ 劳务站「待办」立即出现**非零**数字；完成一件 ⇒ 殖民者账户到账；
   - 把「每份」调到 10 ⇒ 100 件待办的「可派发」按 10 件/份被多个殖民者分批承接（10 份）。
2. **测试复跑受阻（并行会话）**：最终一轮（仅注释 + 语言键，零逻辑）复跑时，`Tests` 因**并行会话在途改动**（`PerceptionGroups` / `PerceptionPurpose` / `VanillaNeedRegistry` 等本 ChangeID 未触碰的类型）编译失败 ⇒ 与本次改动无关；本轮的逻辑改动以 12:06 那次全绿为证据，最终产物（含本轮全部改动，Cecil 反查 `HandleAction → StepVanillaJobsPerContract` 已确认）编译通过并已部署。
3. **ARCH 转交项未做**（下一批候选）：`Sign` 的 `SlotTaken` 判据边界已在 2.3 修掉；`EconomyVanillaWorkSource` 识别口径注释与 `Workspace.VanillaHint` 文案（中英）已在本次一并补入「割除」。

## 六、回滚

- 代码：`git checkout -- <11 paths>`；无 Save 侧残留（本轮零持久化面变更）。
- 契约：`PositionCapacity` 为**新增**字段（默认 0 = 不限份数），回滚时需同时还原测试工厂与新增用例。

### 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖实际改动全部文件（11/11）
- [x] 清单范围 ⊆ Scope
- [x] 每个文件标注类型与摘要
- [x] Change ID 与 Gate 结论、契约测试可互链
- [x] 无调试代码 / TODO 残留
- [x] 已用 `gate-runner.ps1` 自动校验
