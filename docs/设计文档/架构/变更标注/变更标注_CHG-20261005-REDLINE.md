---
change_id: CHG-20261005-REDLINE
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle]
files:
  - path: Source/PersonalChronicle/Domain/WorkshopTakeover.cs
    type: M
    symbols: WorkshopTakeover.MandatoryKeep (语义), WorkshopTakeover.IsMandatoryKeep, WorkshopTakeover.IsTakeoverAllowed (新增三参重载)
    lines: 17-23, 42-130
  - path: Source/PersonalChronicle/Automation/WorkshopPostAssignmentService.cs
    type: A
    symbols: WorkshopPostAssignmentService (整文件：AssignOf / HasStaffedPostCovering / IsHolderOfWorkType / ResolvePostDefNameOf)
    lines: 1-250
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.WorkTakeover.cs
    type: M
    symbols: MapComponent_WorkshopManager.TakeOverVanillaWork (候选筛选 + 豁免岗主 + 兜底依据注释)
    lines: 157-200, 210-255
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Posts.cs
    type: M
    symbols: WorkshopManageView.BuildPostRows (改为调用服务), 删除 5 个重复实现
    lines: 30-45, 55-115, 180-200
---

# 变更标注 · CHG-20261005-REDLINE

## 背景（玩家 2026-10-05）

> 「**硬红线移除。有专属岗位。** 解释 PC 能否独立交付该 Job」

## 一、先回答：「PC 能否独立交付该 Job」是什么意思

`deliversOwnService` 回答的是一个具体问题：**把原版这件活从成员身上取消勾选之后，
PC 自己有没有 Job 去把它干完？**

| | 含义 | 后果 |
|---|---|---|
| 能（true） | PC 有自己的 Job 做这件事 | 取消原版勾选 ⇒ PC 派自己的 Job 干 ⇒ 活照做 |
| **不能（false）** | PC **没有**对应 Job | 取消勾选 ⇒ **没人做** ⇒ 服务消失 |

**当前实况**（已核实 `Defs/Automation_Jobs.xml`）：PC 只有两个 Job ——
值勤 `PC_WorkshopDuty` + 用餐 `PC_MealGiver`。**对建造/研究/种植/采矿/驯兽/搬运/清洁/医疗/灭火，
一个都不能独立交付。** 所以"接管"目前对任何工作都是"清零之后没人替代"；
红线的存在只是把**最致命的四个**单独锁住。

## 二、玩家的论证成立，但需要一层保护

「**有专属岗位**」是对的前提：既然有物料/仓储/医务/安保四个岗位承接这些工作，
就不该无条件禁掉。**但不能直接删掉红线** —— 接管是「**按人清零**」，
若连**负责搬运的物料员工本人**也一起清零，结果仍是"所有人都不能搬运" ⇒ 断料停摆。

## 三、改法：闸门语义变更 + 豁免岗主（两层，缺一不可）

### 第 1 层：红线从「一票否决」→「需在岗专属岗位」

```csharp
public static bool IsTakeoverAllowed(string workTypeDefName, bool deliveredByPost,
    bool staffedPostCovers)
{
    if (string.IsNullOrEmpty(workTypeDefName)) return false;      // fail-closed 保留
    if (IsMandatoryKeep(workTypeDefName))
    {
        // 敏感项：不再一票否决，改为"必须有在岗的专属岗位承接"。
        // 刻意**不看** deliveredByPost —— 敏感项关心的不是"PC 有没有实现 Job"
        // （PC 至今没有它们的 Job），而是"**有没有人在岗位上做这件事**"。
        return staffedPostCovers;
    }
    return deliveredByPost;   // 非敏感项：能力对等（现状：PC 无 Job ⇒ 仍不可接管）
}
```

`staffedPostCovers` = 存在岗位承接它 **且** 该岗位在本车间**确实有人担任**。

### 第 2 层：清零时**豁免岗位持有者本人**

```csharp
if (WorkshopPostAssignmentService.IsHolderOfWorkType(pawn, type.defName, zone, postAssigned))
{
    continue;   // 该成员的岗位正是负责这件事 ⇒ 不清他的零
}
```

### 两层合起来的闭环（**新保证**）

> **每一个被接管的敏感工作类型，都必然有一个在岗岗主仍在做它。**

- 无岗主 ⇒ 闸门第 1 层直接拒（`staffedPostCovers = false`）；
- 有岗主 ⇒ 允许接管，但第 2 层保证那位岗主不被清零。

⇒ 服务不会消失、死锁仍不可能发生 —— 与旧的"永不接管"等效安全，但**不再无条件禁掉玩家的意图**。
`TakeOverVanillaWork` 里"自由人兜底"的依据注释已同步更新（旧的依据是"红线保证还剩 4 项"，
现有依据是上面这条闭环）。

### 保留的两条兼容与约束

- `IsTakeoverAllowed(string, bool)` **两参重载保留**（转发为 `staffedPostCovers: false`）
  ⇒ 旧调用点与单测行为不变（更保守）；
- **非敏感项仍然不可接管**（`deliveredByPost` 全 false，因 `implementedWorkTypeDefNames` 为空）
  ⇒ 想接管建造/研究/种植等，必须先真的实现对应 Job（Phase 4 原设计，本批不改变）。

## 四、顺带：把「人 → 岗位」分配下沉为单一实现

接管闸门与岗位页**都需要**"人 → 岗位"，原先只在 UI 层算 ⇒ 若接管自己再算一遍，
同一批成员可能得出不同岗位，出现"页面显示他是物料员工、接管却按别的岗位判定"的自相矛盾。

⇒ 新增 `WorkshopPostAssignmentService`（Automation 层，纯读），并**删除** UI 层
5 个重复实现（`ResolvePostDefs` / `FindPawnByStableIdSafe` / `ResolveHolderNames` /
`BuildPostCandidates` / `BuildPostSpecs`）与 2 个缓存字段。

## 存档影响：**无**（纯判定与分配逻辑，不涉及任何 Scribe 键）

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（`_scripts/compile_with_sdk.cmd`→`compile_with_sdk.ps1`） |
| 残留检查 | 已验证 | 旧分配实现已无**代码**引用（仅剩说明注释） |
| 部署 | 已验证 | Defs 31 / Languages 39 / Assemblies 1 同步 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-REDLINE.md` |
| 实机 | 未验证 | 见下 |

### 实机验证要点（重启后）

1. **前置**：车间有成员；且**确实有人担任**承接该工作类型的岗位（岗位页能看到承担者）；
2. 点「接管原版工作」⇒ 若此时**有人担任**物料/仓储/医务/安保岗位：
   - 该工作类型**可以被接管**（除岗主外，其他成员的原版该勾选被取消）；
   - **岗主本人仍保留**该项勾选（打开他的工作面板核对）；
3. 若**无人担任**对应岗位 ⇒ 该工作类型**仍不被接管**（保护生效，日志与结果计数体现）；
4. 建造 / 研究 / 种植 / 采矿 / 驯兽等**非敏感项** ⇒ 仍然不被接管（PC 没有对应 Job）；
5. 点「解除接管」⇒ 按台账还原（岗主从未被清零 ⇒ 无异常）；
6. `Player.log` 无新的 `PersonalChronicle` 异常。

## 残留风险

1. **`Takeover_no_takeoverable_work` 文案未更新**（它说"所有岗位都还是「规划中·未接管」…"）：
   在敏感项已可放行后其表述不完整（虽然不算错误）。建议后续补一句"敏感项（搬运/清洁/医疗/灭火）
   按在岗岗位放行"；
2. **岗主的判定基于"自动分配 + 玩家覆盖"**：若玩家手动把某人的岗位覆盖成空（跟随自动）
   而自动分配恰好没分给他 ⇒ 该工作类型可能"无人担任" ⇒ 敏感项被拒（安全侧，可预期）；
3. **非敏感项的接管仍不可用**（PC 无 Job）—— 这是设计而非缺陷；要实现它需要真的写出
   建造/研究/种植等 Job（Phase 4）；
4. **两参重载保留**：若将来有人用它，会得到"敏感项永不接管"的旧行为（更保守，安全侧）。
