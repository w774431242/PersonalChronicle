---
change_id: CHG-20261012-ECON-HAULCMD
version: v3.0
date: 2026-10-12
author: AI 助手（CodeBuddy · DeepSeek-V4.1-Flash）
scope: [Source/PersonalChronicle/Economy/PawnEconomy/Planning/（新增判据）、Source/PersonalChronicle/Capture/（新增补丁）、Source/PersonalChronicle/Domain/PawnEconomy/Employment/（能力矩阵）、Tests/（用例）、docs/设计文档/架构/（本标注）]
scope-paths: [Source/, Tests/, docs/设计文档/架构/]
title: 原版搬运命令放宽（食品/武器等 alwaysHaulable）+ 劳务站搬运族接线（Haul 改判 EvidenceOnly）
state: Implemented（游戏内未验证）
layer: 捕获层（2 处新 Harmony 挂点）+ 经济域纯判据 + 领域能力矩阵 + 测试
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Planning/HaulCommandRules.cs
    type: A
    symbols: 放宽判据唯一来源（纯布尔重载 + Thing 适配）
  - path: Source/PersonalChronicle/Capture/Patch_HaulDesignator.cs
    type: A
    symbols: Designator_Haul.CanDesignateThing Postfix / GridsUtility.GetFirstHaulable Postfix / 启动期签名探测
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Employment/LaborFlow.cs
    type: M
    symbols: LaborFlowRules.CapabilityFor
    lines: 541-556
  - path: Tests/LaborFlowTests.cs
    type: M
    symbols: CapabilityMatrixIsExplicitAndFailClosed
    lines: 182-192
  - path: Tests/HarmonyStartupTests.cs
    type: M
    symbols: 新增 2 条 AssertRegistered（搬运命令挂点）
    lines: 59-62
  - path: Tests/HaulCommandRulesTests.cs
    type: A
    symbols: 6 用例（形态边界 / Thing 适配 / 判据矩阵 / 兜底同源 / 挂点探测 / 能力矩阵未波及）
---

# 变更标注 · CHG-20261012-ECON-HAULCMD

## 一、用户需求（原话）

「能否优化原版【搬运】命令，使其能够对食品、武器等等物品进行命令类搬运工作，同时要接入劳务站的工作需求识别。」（附截图 = 原版 Orders 菜单的「搬运」设计器图标）

## 二、目标 / 非目标

**目标**

1. 放宽原版 Orders 菜单「搬运」设计器的受理范围：食品、武器、原料等 `alwaysHaulable` 物品也可被玩家下达 Haul designation。
2. 让该 designation 走通劳务站既有链路（识别 → 待办计数 → 上屏 → 合同承接 → 真实送达计酬）。

**非目标**（本轮不做，避免范围外扩）

- 不放宽 Hunting / Slaughter 的受理（其权威回执来源仍不存在 ⇒ 保持 Unsupported）。
- 不改写任何原版 Def / XML，不覆盖原版 Keyed 文案，不新增本地化键（暂无可复用消费方）。
- 不部署到游戏目录（未获指示）。

## 三、原版语义与断点（只读取证）

| 事实 | 位置 |
|---|---|
| 受理硬判 `designateHaulable` | `_research/vanilla/Assembly-CSharp/RimWorld/Designator_Haul.cs:70-85` |
| 格内取候选只认 `designateHaulable` | 同文件 `:44-61`（CanDesignateCell）与 `:64-67`（DesignateSingleCell）**都**走 `c.GetFirstHaulable(Map)` |
| GetFirstHaulable 实现 | `_research/vanilla/Assembly-CSharp/Verse/GridsUtility.cs:255-266` |
| EverHaulable 语义 | `_research/vanilla/Assembly-CSharp/Verse/ThingDef.cs:16-22` |
| designateHaulable 在原版 Core 仅石块类使用 | `Data/Core/Defs/ThingDefs_Misc/Various_Stone.xml:23` |

**结论**：食品 / 武器 / 原料的 `alwaysHaulable = true` ⇒ 它们本来就**永远不能**被搬运命令标记；放宽只需两处挂点，且必须**成对**，否则 `DesignateSingleCell` 取到 null ⇒ `new Designation(null, ...)` ⇒ `Notify_Added` 里对 null 目标解引用 ⇒ NRE。

## 四、实现（文件级）

| 文件 | 类型 | 内容 |
|---|---|---|
| `HaulCommandRules.cs` | A | 放宽判据**唯一来源**：纯布尔重载（可离线断言）+ Thing 适配 |
| `Patch_HaulDesignator.cs` | A | ① `Designator_Haul.CanDesignateThing` Postfix（只在原版**裸拒**时补判）；② `GridsUtility.GetFirstHaulable` Postfix（仅 null 时补候选）；③ 启动期签名探测 |
| `LaborFlow.cs` | M | `CapabilityFor(Haul)`：Unsupported ⇒ **EvidenceOnly** |
| `LaborFlowTests.cs` | M | 同步断言；保留 Hunt / Slaughter / 未声明族 / null 的反例 |
| `HarmonyStartupTests.cs` | M | 新增 2 条 `AssertRegistered`（挂点登记，漂移即红灯） |
| `HaulCommandRulesTests.cs` | A | 6 用例 |

### 4.1 判据（为什么这样写）

- **形态** `IsCommandableShape` = `Spawned && EverHaulable && category == Item`。与 `GetFirstHaulable` 在原版里的角色一致（形态过滤器）；真正的受理判定仍留在 `CanDesignateThing`。
- **放宽** `ShouldRelaxVanillaRejection` = 形态 && 未已标记 && **未已在合格储存区**。
- ⚠ **第三条必须显式重做**（对既定设计方向的一处修正）：原版对 `designateHaulable` 目标走得到 `IsInValidStorage()` 并返回**带 Reason** 的报告；但 `alwaysHaulable` 目标在第一条判定处就被**裸拒**（Reason == null）。若 Postfix 只按「Reason == null 就放宽」，会给「已在库内」的物品也放行 ⇒ 挂上一个永远不会被执行的 designation（`ListerHaulables.ShouldBeHaulable` 会把它过滤掉）⇒ 幽灵待办 + 永不消失的标记。
- **已在库内**复用原版键 `MessageAlreadyInStorage` 作为提示（与石块类目标行为一致；不新增键、不覆写 Def）。

### 4.2 劳务站接线（Haul ⇒ EvidenceOnly）

CA-03（2026-10-12）把 Hunt / Slaughter / Haul 一并降级，理由是「没有权威接收回执来源」。本轮**只撤销 Haul**，依据：

`Capture/Patch_VanillaHaulWork.cs` 的四段补丁在**真实放置回调**里取证 —— 格子路径校验 `SlotGroup.Settings.AllowedToAccept(thing)`（`:130-131`），容器路径按**实物减少量**（`:137-144`），经 `ReportVanillaOrderCompletion` 入账（`:108-109`）。凭据由真实交付产生，**不含任何伪造的 accepted:… 字符串**。

故 `EvidenceOnly`（无实物交验、完成证据即凭据）与事实一致：物品已真实进入合格储存区，**不应**再让 Pawn 把它搬去柜台（`CarryToCounter` 是错的）。Hunt / Slaughter 的回执来源仍不存在 ⇒ 保持 Unsupported（本轮**不放行**）。

传导面（有意）：`TryBeginLaborIntent` 不再以 `DeliveryUnsupported` 拒绝搬运机会（可签约）；`LaborInteractionOptions` 不再跳过搬运合同（可见可接）；`PlanContractSubmission` 给出交验 Job；`PlanContractCollectOutputs` 仍**只**对 `CarryToCounter` 生效 ⇒ 搬运不会被要求搬货（正确）。

## 五、第三方影响 / 兼容性

- 两个挂点在全仓（`Source` / `Tests`）**零既有补丁**（本仓 grep：这三个符号的既有命中只在文档，不在任何补丁文件）；只在原版拒绝后补判，原版其余行为逐字不动。
- 不做 Transpiler；两处补丁均带 Patch 档案注释（Target / Type / Reason / Alternative / Compatibility Risk / Execution Order / Failure Behavior / Rollback）+ 启动期签名探测（`AccessTools.Method` + 唯一错误码）。
- `Designator.hasDesignateAllFloatMenuOption` 默认 false 且 `Designator_Haul` 构造里未置位（`_research/vanilla/Assembly-CSharp/Verse/Designator.cs:559`）⇒ **不存在**「一键标记全图所有物品」的放大面。
- 已知边界（与原版同构）：格内多目标时按 `ThingsListAt` 顺序取第一个**形态合格**者；若该首个候选已在合格储存区，该格不会再去试同格第二个可搬目标（与原版多石块同格的首个匹配行为一致）。

## 六、证据（实测输出）

| 项 | 命令 | 结果 |
|---|---|---|
| 编译（裸 csc 全语法） | `pwsh -NoProfile -File _scripts/compile_with_sdk.ps1` | 无错无警（exit 0），产出 `Assemblies/PersonalChronicle.dll`，备份 `PersonalChronicle.dll.bak_20261012230021` |
| 全量单测 | `dotnet test Tests/PersonalChronicle.Tests.csproj -c Release` | 失败 0 / 通过 1635 / 跳过 1 / 总计 1636 |
| 新增用例 | 同上加 `--filter FullyQualifiedName~HaulCommandRulesTests` | 失败 0 / 通过 6 / 总计 6 |
| 挂点登记 | `Tests/HarmonyStartupTests.cs`（含在全量 1635 内） | 2 条新 `AssertRegistered` 通过 ⇒ 两个 Postfix 被 Harmony 实际安装 |

GATE-DELTA 与收尾齐备性结论见收尾回复 R4（本文不预先声称 PASS）。

## 七、明确未做 / 未验证

- **游戏内验证一律 NOT RUN**：未部署、未启动游戏。①「玩家能对地上的食品/武器下达搬运命令」②「劳务站能承接搬运合同并计酬」③「已在库物品不会产生幽灵待办」三条均**未实测**。
- 台账侧按「未验证 ⇒ OPEN」如实登记（见 §八）。
- 未新增 Keyed 文案（暂无可复用消费方）；若后续要在劳务站「工作目录」的「搬运」行加说明，另走 GOV-010 中英同键。
- 未做：`GetFirstHaulable` 同格多候选的「跳过不可用候选」优化（与原版行为一致，属有意保留）。

## 八、台账与文档动作（AI-017 / DOC）

- **PROG-ECON · W1** 新增 2 项：①（DONE）代码级交付 —— 离线断言全绿 + 两处挂点注册；②（OPEN）实机验收 —— 食品/武器可被搬运命令标记并真实入库、劳务站搬运合同可承接/计酬、无幽灵待办。
- 同 `(program,wave)` 已存在 ChangeID（CHG-20261012-LONGPROJ-LEDGER）⇒ 另起本 ChangeID 时已用 `-ForceNewChange` 留痕（理由：新增需求与「长项目工具链收口」无内容重叠）。
- 文档变更申请（DOC-004）：本轮新增**本标注 1 份**，落点 `docs/设计文档/架构/变更标注/变更标注_CHG-20261012-ECON-HAULCMD.md`（用户在本轮指令中明确要求落此路径 = 已获批）。未新建/覆盖其它文档；未改写规范文本。
- 旧件处置：无（本轮不存在被取代的文档）。

## 九、附带工具修复（.codebuddy/** = git 忽略区，不入 GATE-DELTA）

**现象**：本轮收尾跑 program-ledger.ps1 -Delta -Program PROG-ECON ⇒ 台账结论 BLOCKED（… PROG-ECON: SNAP_INCONSISTENT），而该判据声明为**不可豁免** ⇒ 收尾链被硬阻。

**定位**（只读探针 .codebuddy/tmp/probe-recon.ps1 实测输出）：takenAt=2026-10-12 22:58:49.727，快照 before=16 / itemCount=16；PROG-ECON 的 16 个**旧格式项**（无 history 键）全部被算成 hasLog=True 且 st 为空串 ⇒ 不计剩余 ⇒ recon=0，既不等于 16 也不等于 17 ⇒ 判 SNAP_INCONSISTENT。

**根因**：Get-RemainingAt 里 foreach ($h in @($it.history)) 对**缺失键**会产出「一个 $null 元素」，而 [string]$null.at 为空串 ⇒ 空串 -le $at 恒真 ⇒ 误置 hasLog 并把 st 写成空串。原文注释的第三条规则（无 history ⇒ 回落 updatedAt 比较）因此**永不生效**。

**修复**：program-ledger.ps1 v1.3 → **v1.4**（.codebuddy/**）：判据加空条目守卫 if ($h -and (...))，并同处写明本实证；$toolVersion 同步为 v1.4。

**回归证据**：修复后同命令 ⇒ [台账] PROG-ECON：剩余 16 → 17（Δ1 · 本轮关闭 0 项），SNAP_INCONSISTENT 消失；未收敛仍在，且豁免被判「升级用户裁决」⇒ 本轮收尾结论为 BLOCKED（**如实上报，未自行使用 USER_RULED**，见 §十）。

## 十、非本轮产物（并行会话在途，具名登记、不认领、不修改）

本轮执行期间检测到**并行会话**在改仓内文件：change-context.ps1 -Validate 首次报 3 个范围外文件「基线后内容被改动」，gate-runner.ps1 又报 1 个新增未跟踪文件。

| 文件 | mtime | 处置 |
|---|---|---|
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyCommands.cs | 23:05:55 | 非本轮产物；经 -GenScope -ForceRebaseline **显式吸收**并在此具名登记 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyLabels.cs | 23:05:54 | 同上 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorld.cs | 23:06:01 | 同上 |
| Tests/InstitutionGateGuidanceTests.cs | 23:06:17 | 同上（新增未跟踪件） |
| Source/PersonalChronicle/BuildStamp.g.cs | 23:06:34 | 构建生成物（并行会话 23:06:36–47 的 Debug 构建） |
| docs/设计文档/架构/变更标注/变更标注_CHG-20261012-INSTGATE-GUIDE.md | R2 期间 | **R2 复跑 GATE-DELTA 时报「标注遗漏实际改动文件」**：该文件是**并行会话的新变更标注**，非本轮产物（未认领、未修改）；经 `-GenScope -ForceRebaseline` 显式吸收后 DELTA 复 PASS |
| docs/设计文档/架构/EXC-2026-031.md · 变更标注_CHG-20261012-REPORTFMT.md · 工程约定/报告排版需求.md | R3 期间 | 同上：R3 复跑 DELTA 再次报「标注遗漏」，三项均为**并行会话新产物**（本轮未认领未修改）；再次 `-GenScope -ForceRebaseline` 吸收后 DELTA 复 PASS。范围外条目数 2425 → **2428（+3）**，增量即此三项 |
| docs/设计文档/架构/变更标注/变更标注_CHG-20261012-ECON-LABOR2.md | R4 期间 | 同上：**并行会话新变更标注**（本轮未认领未修改）；再次 `-GenScope -ForceRebaseline` 吸收后 DELTA 复 PASS。范围外条目数 2428 → **2429（+1）** |
| —（趋势） | R1→R4 累计 | 范围外条目数 **2423 → 2429（+6）**，**全部**为并行会话在途产物；本轮自身声明恒为 **6 个文件**、无遗漏/超范围/残留 |

- 范围外条目数趋势 2423 → **2425（+2）**，增幅即上述并行在途产物；**不解释为「本轮新积压」**。
- 风险（如实登记）：并行写者仍活跃 ⇒ 若其再次改动上述文件，-Validate 会再次 FAIL（这正是「不可自愈门禁」的设计意图）；本轮以当时点的显式吸收为准。
- 归属裁定属 PROG-TOOLGOV / WI-009（OPEN）；本轮只能做到「具名登记 + 显式吸收」，无法机器化判定归属。
- **M9 升级**：program-ledger -Delta 的豁免连击状态为 consecutive=2 / lastCode=USER_RULED（来源 **PROG-TOOLGOV**，全程序共享）⇒ 本轮 PROG-ECON 的任何非 USER_RULED 豁免都被判「第 3 轮 ⇒ 须用户裁决」而 BLOCKED。本轮**未自行使用 USER_RULED**，交由用户裁决。

## 十一、R2 返修（2026-10-12 实机反馈「无法搬运非石类」）

**现象**：玩家实地对非石类物品下达「搬运」⇒ 仍出现原版提示「必须指定可以搬运的物品」。Player.log 证实游戏已加载本轮构建（`[PersonalChronicle] build 2026-10-12 15:16 UTC`）⇒ **不是「未重启 / 旧 DLL」**。

**根因（R1 设计缺陷）**：R1 把「格内候选」放宽挂在 `Verse.GridsUtility.GetFirstHaulable` 上。该方法是 6 行静态小方法，被 `Designator_Haul.CanDesignateCell` / `DesignateSingleCell` 各调用一次；**Mono JIT 极可能把它内联进调用方**，于是补丁挂在一个从不被执行的调用点上（Harmony 安装"成功"、单元测试登记断言也过，但实机行为零变化）。原版提示由 `CanDesignateCell` 在 `GetFirstHaulable == null` 时给出 ⇒ 现象与根因一致。

**修复**：格内路径改挂 `Designator_Haul` 的**最外层虚方法**（由基类 `Designator.DesignateMultiCell` 经 vtable 调用 ⇒ 不受内联影响）：
- `Patch_HaulDesignatorCell`（`CanDesignateCell` Postfix）：原版无候选时用 `HaulCommandRules.FindCommandableInCell` 重判；**判据自足**（不再回调 `CanDesignateThing`，避免二次依赖可能被内联的实例方法）。
- `Patch_HaulDesignatorSingleCell`（`DesignateSingleCell` Prefix 接管）：`CanDesignateCell` 放宽后原版仍会用 `GetFirstHaulable` 取目标 ⇒ 恒 null ⇒ `new Designation(null,…)` 在 `AddDesignation` 里对 null 目标解引用。接管只在原版无候选时发生，且**必须经 `DesignationManager.AddDesignation` 写入**（绕开会让该次下达不被记为玩家来源 ⇒ 既不计待办也不计酬）。
- 删除 `Patch_HaulFirstHaulable`（已被实测否决的挂点，保留即双路径）；`CanDesignateThing` 的 Postfix 保留（对象级入口 / 第三方调用方）。
- **判据实现从 Harmony 挂点降级为普通方法** `HaulCommandRules.FirstCommandableIn` / `FindCommandableInCell`：不再依赖任何内联行为。

**可区分成因的一次性探针（新增，最多两条，`Log.WarningOnce`）**：
- `0x5A1833` `haul-command probe: no vanilla candidate and no commandable Item here (cell)` ⇒ 补丁跑了，但该格确实无可搬运物（例如只框到草地）。
- `0x5A1834` `haul-command probe: commandable candidate taken over (defName at cell)` ⇒ 接管路径已跑通。
⇒ 下一次实机若仍失败，两条探针即可把「补丁未执行 / 框错位置」分开，不必再猜。

**R2 证据**：编译 0 错 0 警（BuildStamp 2026-10-12 15:22 UTC）；全量单测 **失败 0 / 通过 1639 / 跳过 1 / 总计 1640**；新增 `CellSelectorIsNullSafeAndFailClosed`；挂点登记断言改为 `CanDesignateCell` + `DesignateSingleCell`；部署 255 文件（备份 `deploy_20261012_232230_030`），工程 DLL == 部署区 DLL = `9FDED571…88ED2B`（23:22:02，2,403.0 KB）；产物内含 `Patch_HaulDesignatorCell` 与两条探针串、**不再含** `Patch_HaulFirstHaulable`。

**仍未验证**：R2 的实机效果（游戏内三项）依旧 **NOT RUN** —— 需玩家重启后复测。若探针 `0x5A1834` 出现而提示仍在，则说明还存在第三层成因（届时应改挂 `Designator.DesignateMultiCell` 或改用自绘 Designator）。

**方法论（本轮教训）**：**「单元测试里断言补丁已注册」≠「实机该补丁会被执行」** —— 小方法的内联绕过是 Harmony 的经典失效面；凡补丁挂在**被同类小方法调用**的目标上，必须另行实证"补丁真的跑了"（本次靠一次性探针），不能只靠 `GetPatchedMethods` 登记断言。

**⚠ 本节取代 §六 的编译/测试数字与 §七 的「未部署」表述**（R2 已部署；R1 数字为 1635/1636，R2 为 1639/1640）。

## 十二、R3 去噪（2026-10-12 玩家反馈「爆黄字」）

**反馈**：玩家打开 Debug log 看到黄字。取 `Player.log` 实证，黄字含三类，**只有第一类是本轮新增**：

1. `[PC][Economy] haul-command probe: ...`（**R2 埋的两条一次性探针**，Warning 级 = 黄字）—— 它们正是设计目的：`commandable candidate taken over (Gun_Revolver at (126, 0, 126))` 证明接管路径已跑通（R2 修复生效的直接证据，已记录在案）。
2. `[PC][LOC] unresolved key: PC_PEA.LaborVisual.Reason.Applied`（**预存缺陷，非本轮**，见下）。
3. `[PC][RECRUIT] ...`（预存的诊断行，Information 级，非本轮）。

**处置**：探针达成使命 ⇒ **移除**（`Patch_HaulDesignator.cs` 内以注释保留取证结论与恢复方式）。R3 证据：BuildStamp `2026-10-12 15:39 UTC`；编译 0 错 0 警；全量单测 **1639 通过 / 0 失败**；部署（DLL `4E8431C7…D455B8`，23:39:13，2,402.5 KB）；产物核对：**已无**探针串、接管逻辑仍在。

**顺带取证（预存缺陷 · 已登记台账 `PROG-ECON / WI-019` · 本轮未修）**：`PC_PEA.LaborVisual.Reason.Applied` 在中文与英文语言文件中**均不存在**（grep 计数 0/0）；该键族由 `LaborVisualText.ReasonLabel(code)` 生产 —— 疑似**成功码被当作「原因」渲染**（`EconomyLaborCounterPresentation.cs:110` 形如 `ReasonLabel(code.ToString())`）。**未修的两条理由**：① 属 LABOR-VISUAL 流，且并行会话当时正改该文件（单写者纪律）；② 直接补键会把「成功码当原因」的逻辑问题粉饰掉，须先裁定「补键」还是「改调用点」。

**★ R2/R3 的实证价值（本轮最重要的结论）**：

- `[PC][RECRUIT] Niklas ... selected=vanilla:Hauling:Thing_PC_PEA_LaborOffice45842:Haul` → `stage=IntentAccepted`：**劳务站侧搬运合同已被殖民者承接**（改之前这一步被 `DeliveryUnsupported` 挡住）⇒ 「接入劳务站」这一半**已在实机跑通**。
- `commandable candidate taken over (Gun_Revolver ...)`：**放宽受理已在实机生效**（武器可被命令搬运）。
- 由此「命令类搬运 + 劳务站计酬」两侧的实机证据均已取得；仍待玩家完成的是**端到端一条龙**（标记 → 入户 → 交验 → 发薪到账）。

## 十三、R4 根因修复（2026-10-12 玩家反馈「没有像原版石类那种高亮显示」）

**现象**：框选时石块类会显示高亮方块，食品/武器不显示。

**取证路径**：原版高亮链 = `DesignationDragger.DraggerUpdate`（`Verse/DesignationDragger.cs:97-126`）
→ `SelDes.RenderHighlight(cells)`（`Designator.RenderHighlight`，`Verse/Designator.cs:535-538`）
→ `DesignatorUtility.RenderHighlightOverSelectableThings`（`Verse/DesignatorUtility.cs:55-73`）
→ 逐物体 `designator.CanDesignateThing(thing).Accepted` 才画 `DragHighlightThingMat`。
⇒ 高亮**完全依赖 `CanDesignateThing`**，与 `CanDesignateCell` 无关（后者只决定哪些格进 `tmpHighlightCells`）。

**★ 真根因（我自己的判据错误，R1 起就存在）**：原版
`AcceptanceReport.WasRejected = new AcceptanceReport(""){ acceptedInt = false }`
（`Verse/AcceptanceReport.cs:43-52`）⇒ 设计器里直接 `return false;` 产生的报告，其 `Reason` 是
**空串而非 `null`**。而 `Patch_HaulDesignatorThing` 的旧守卫写作
`if (__result.Accepted || __result.Reason != null) return;` —— 空串 `!= null` 为**真**
⇒ **每一条裸拒都被判成「原版已给理由」而提前返回** ⇒ `CanDesignateThing` 的放宽**从未执行过一次**
（R2 之所以"看起来好了"，是因为我把 `CanDesignateCell` 的判据写成自足式、不再回调 `CanDesignateThing`）。

**修复**：新增唯一判据 `HaulCommandRules.IsBareRejection(AcceptanceReport)`
（`!Accepted && string.IsNullOrEmpty(Reason)`），`Patch_HaulDesignatorThing` 改用它。
带具名理由的拒绝（`MessageAlreadyInStorage`）仍**不是**裸拒、原样透出。

**R4 证据**：编译 0 错 0 警（BuildStamp `2026-10-12 15:52 UTC`）；全量单测 **1640 通过 / 0 失败**
（新增 `BareRejectionIsEmptyReasonNotNul`，直接钉住「`WasRejected.Reason` 是空串而非 null」这一原版事实）；
部署 255 文件（备份 `deploy_20261012_235234_868`）；工程 DLL == 部署区 DLL = `589924E6…4AE880`
（23:52:19，2,402.5 KB）；产物核对：含 `IsBareRejection`、无探针串。

**⚠ 对 §十一「R1 根因 = 内联」的更正（如实标注）**：R1 的**单元格路径**失效（原版提示「必须指定可以搬运的物品」）
**不能**由本轮这个空串缺陷解释（那条提示出自 `CanDesignateCell` 的 `GetFirstHaulable == null` 分支，
与 `Reason` 判据无关）⇒ 「`GridsUtility.GetFirstHaulable` 的补丁未被执行（内联/其它绕过）」仍是**推断**，
未经反例确证；但**已确认并修复的**是本节这条空串判据缺陷（它独立地使对象级/高亮路径失效）。
R2 的挂点迁移（改挂 `CanDesignateCell` / `DesignateSingleCell`）因此仍是**必要**改动（与内联无关地绕开了该缺陷的连带面）。

---

署名：本变更由 AI 助手（CodeBuddy · DeepSeek-V4.1-Flash）于 2026-10-12 实施并生成全部交付物。
