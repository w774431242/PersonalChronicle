---
change_id: CHG-20261005-CLEANUP
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle, Tests]
files:
  - path: Source/PersonalChronicle/Domain/WorkTabHideFilter.cs
    type: M
    symbols: WorkTabHideFilter.CountUnion
    lines: 19-46
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.WorkTabHide.cs
    type: M
    symbols: ChronicleGameComponent.HiddenWorkTabCount
    lines: 117
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.WorkDemote.cs
    type: M
    symbols: MapComponent_WorkshopManager (file header), MapComponent_WorkshopManager.SyncWorkStationForZone, MapComponent_WorkshopManager.LastRestoreCount
    lines: 9-40, 537, 570-580
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.WorkEnable.cs
    type: M
    symbols: MapComponent_WorkshopManager.SyncVanillaWorkSettings
    lines: 93-100, 110-118
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.WorkStation.cs
    type: M
    symbols: WorkshopManageView.BuildWorkStationStatusText, WorkshopManageView.DrawWorkStationStatusRow
    lines: 92-105, 166-182
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.AutoHide.cs
    type: M
    symbols: MapComponent_WorkshopManager (file header)
    lines: 18-30
  - path: Source/PersonalChronicle/Domain/WorkshopWorkDemotionLedger.cs
    type: M
    symbols: WorkshopWorkDemotionLedger (file header)
    lines: 6-32
  - path: Source/PersonalChronicle/Capture/Patch_PawnJobTracker.cs
    type: M
    symbols: Patch_PawnJobTracker_WorkTypeDisabled (doc comment)
    lines: 29-35
  - path: Tests/WorkTabHideFilterTests.cs
    type: A
    symbols: WorkTabHideFilterTests (6 cases)
    lines: 1-75
---

# 变更标注 · CHG-20261005-CLEANUP

## 背景

玩家要求「进行修复、清理，并给我说明验收清单」—— 对 `CHG-20261005-NODEMOTE-AUDIT`
审计报告中登记的**残留待办**做一次性收口。本批**不含行为变更**（除补测试外均为注释/治理/可测性）。

## 一、治理：三处例外登记的语义漂移已修订（`docs/`，不进 git）

| 文件 | 原表述（已与现实矛盾） | 修订 |
|---|---|---|
| `EXC-2026-013.md:48-51` | 备注称"其余所有已勾选工作会被统一降到最低档 4" | 补 2026-10-05 修订段：该半句**已恢复全文有效**（降档移除后没有任何代码重排其它工作类型）；顺带修正原文一处嵌套反引号笔误 |
| `EXC-2026-016.md:7-8` | "该条对**降档通道**仍然全文有效" | 补修订段：降档通道已不存在 ⇒ 本 EXC 实质约束只剩"**唯一允许 `SetPriority(type,0)` 的地方是接管通道**"，并声明新增的自动化出口（`ReleaseVanillaWorkFor`）沿用同一台账与闸门、**不扩大授权面** |
| `EXC-2026-016.md` Alt-1 | 以"降档（仍可做）"作对照组 | 补注：对照组已失去现实对应，**结论不受影响** |
| `EXC-2026-023.md:35` | "**降档还原**同样使用 `SetPriority(x, 0)`" | 补修订段：**两处事实错误**（降档还原写的是 4 不是 0；且该通道已移除）⇒ 更正为"只剩接管通道" |

> ⚠ `docs/` 不在 git 跟踪范围内（`.gitignore`）⇒ 不进本标注 files 段，在此登记备查
> （GATE-DELTA 曾因把 `docs/` 文件列进 files 段而报"标注中存在 git 无对应改动的文件"，已按规则处理）。

## 二、可测性：去重计数收口为纯函数 + 补单测

- 新增 `WorkTabHideFilter.CountUnion(manual, auto)`（`Domain/`，**纯逻辑、可离线单测**）；
- `ChronicleGameComponent.HiddenWorkTabCount` 改为转调它（GOV-005 单一实现，且顺带把算法从存档层挪到可测层）；
- 新建 `Tests/WorkTabHideFilterTests.cs`（6 个用例）—— **专门钉死审计发现的 P1**：
  * `CountUnion_AutoOnly_IsCounted`：分账后"仅自动隐藏"是常态，旧实现（只数手动集）在此恒返 0
    ⇒ 恢复入口不渲染 —— 本用例即防该回归；
  * `CountUnion_Overlap_IsCountedOnce`：同一人同时在两集合必须去重（并集 = 3 而非 2+2）；
  * 另含空集/单侧/完全重合/null 边界。

**测试结果**：`dotnet test` ⇒ **505 通过 / 0 失败 / 1 跳过**（共 506）。
（首次运行 1 条失败，原因是**我写的期望值有误**——`{A,B} ∪ {B,C}` 应为 3 而我写成 2；
实现正确、测试修正，并顺带补了"完全重合"用例。）

## 三、注释与文档漂移清理（本批主要工作量）

原则：**代码注释必须反映现状**，否则下一轮审计/维护会据其做出错误判断（本项目已发生过多次）。

| 位置 | 原表述 | 现表述 |
|---|---|---|
| `WorkDemote.cs` 文件头 | "降档通道 + 一键全解除"；三条硬红线含"降档目标恒为 4" | 重写为"只提权 + 旧档迁移还原"，列明现在只做三件事，并**明确禁止**把已删按钮接回 |
| `WorkDemote.cs` `SyncWorkStationForZone` | 注释含"降档"隐含语义 | 说明该调用现仅为**状态装配**，不再改任何档位 |
| `WorkEnable.cs:93-100` | "仍要走一趟降档通道…降到最低档" | "仍要走一趟**状态装配**…不会改动任何工作档位" |
| `WorkEnable.cs:110-118` | "降档通道必须在 EnableWorkTypes 之后，否则把刚打开的类型压回 4" | 改为"状态装配须在写入之后（它统计写入结果）"，并注明降档顺序风险已不存在 |
| `WorkStation.cs` 按钮 summary | 整段描述已删按钮的可用性/点击行为 | 改为"绘制**状态行**（只读读数；按钮已移除）"，并**明确不要加回**、指向自动化出口 |
| `WorkStation.cs` 读数注释 | "台账中 K 项可解除 = ledgerEntries" | 改为"在册 K 人 = scopedPawns"，并注明台账项已随降档移除 |
| `AutoHide.cs` 文件头 | "刻意不做『离队自动取消隐藏』"（理由是怕放出玩家手动隐藏的人） | 改为"该理由已由**分账**解决 ⇒ 现在**会**在移出车间时自动取消隐藏，且玩家手动隐藏的人依旧不受影响" |
| `WorkshopWorkDemotionLedger.cs` 头 | 以降档在役为前提描述用途 | 全文改为"**已停用**"，列出仅剩两个用途（旧档迁移 / 存档留档），并**明确警告**"`PruneDeadPawns` 等已无生产调用方，不要据此认为降档仍在运行" |
| `Patch_PawnJobTracker.cs` | "与**降档还原**同样用 `SetPriority(x, 0)`" | 更正：降档还原写 **4** 不是 0，且该通道已移除 ⇒ 改为"接管通道及其还原路径" |
| `WorkDemote.cs` `LastRestoreCount` | "供 UI 提示（一键全解除）" | 改为"仅迁移路径会写；**暂无读取者**，保留作排障观测点（有死代码风险认知）" |

## 四、死代码 / 死赋值

- 删除 `SyncWorkStationForZone` 中 `zone == null` 分支的 `ledgerEntries` **死赋值**
  （该字段自降档移除后已无任何读取者）；
- `PruneDeadPawns` / `IsPawnStillRelevant` / `LastRestoreCount` **保留**（前两者被既有单测引用、
  后者是迁移的可观测字段），但已在头注释里登记"无生产调用方"的现实，避免下轮审计误判。

## 存档影响：**无**

本批不新增/不改动任何持久字段，不改 Scribe 键，无 Migration。

## 验证

1. **编译**：0 错 0 警；**部署**：成功（Defs/Languages/Assemblies 全同步）；
2. **单测**：505 通过 / 0 失败（含新增 6 例）；
3. 无行为变更 ⇒ 不需要实机验证（实机验收见 `变更标注_CHG-20261005-NODEMOTE.md` 与
   `NODEMOTE-AUDIT` 的验收要点）。

## 残留（照实登记，均已知且低风险）

1. `PruneDeadPawns` / `IsPawnStillRelevant`：无生产调用方（仅单测）—— 保留以不动单测与历史留档，
   已在头注释声明；
2. 语言文件里可能仍有个别"一键全解除"字样的**注释行**（非玩家可见文本，Keyed 键本身已删净）；
3. `docs/` 内其余历史变更标注（`CHG-20261003-*` 等）仍以降档在役叙述 —— 属历史留档，
   按项目惯例不回改历史文档，仅在 `EXC` 三处加了修订指引。
