---
change_id: CHG-20261005-NODEMOTE
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle, Languages]
files:
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.WorkDemote.cs
    type: M
    symbols: MapComponent_WorkshopManager.AlignKeptWorkTypes, MapComponent_WorkshopManager.SyncWorkStationForZone, MapComponent_WorkshopManager.RestoreAllDemotedWork, MapComponent_WorkshopManager.DemoteNonKeptWorkTypes (removed)
    lines: 277, 334, 380
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.WorkStation.cs
    type: M
    symbols: WorkshopManageView.BuildWorkStationStatusText, WorkshopManageView.DrawWorkStationStatusRow, WorkshopManageView.WorkRestoreBtnW (removed)
    lines: 92, 120, 175, 178
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView.Refresh, WorkshopManageView.OnRosterChanged
    lines: 346, 349, 354, 1233
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.WorkTakeover.cs
    type: M
    symbols: MapComponent_WorkshopManager.ReleaseVanillaWorkFor
    lines: 327
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.AutoHide.cs
    type: M
    symbols: MapComponent_WorkshopManager.SyncAutoHideWorkTabColonists, MapComponent_WorkshopManager.ReconcileAutoHideOnLeave, MapComponent_WorkshopManager.lastWorkshopMembers
    lines: 68, 101, 124
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.WorkTabHide.cs
    type: M
    symbols: ChronicleGameComponent.autoHiddenWorkTabColonists, ChronicleGameComponent.AutoHiddenWorkTabColonists, ChronicleGameComponent.SetAutoHiddenFromWorkTab, ChronicleGameComponent.IsHiddenFromWorkTab, ChronicleGameComponent.ClearHiddenFromWorkTab, ChronicleGameComponent.ExposeWorkTabHide
    lines: 38, 62, 94, 100, 111
  - path: Source/PersonalChronicle/Capture/Patch_MainTabWindowWorkPawns.cs
    type: M
    symbols: Patch_MainTabWindowWorkPawns.Postfix
    lines: 43
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: WorkSyncAligned (kept), WorkSyncLedger/WorkSyncRestore (removed), WorkSyncHint (rewritten)
    lines: 431-441
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: (same)
    lines: 431-437
---

# 变更标注 · CHG-20261005-NODEMOTE

## 背景（玩家裁定，2026-10-05）

> 「能否移除这项设置（一键全解除）……我不需要这个，只需要人员被移除车间体系，
> 然后恢复显示原版的工作面板，玩家可以手动进行调整，避免多余。」→ 可行性审计后**批准 A 方案**。

方案核心：**删掉"降档 + 手动出口"，把出口改为自动化（人员移出车间体系即自动恢复）**。

## 交付一：移除降档通道 + 旧档一次性迁移

- `MapComponent_WorkshopManager.WorkDemote.cs`：
  - 方法 `DemoteOtherWorkTypes` → **改名 `AlignKeptWorkTypes`**，删掉原 ② 段（"其余已勾选工作降到最低档 4"）；
  - `DemoteNonKeptWorkTypes`（降档核心循环，73 行）**整体删除**，保留删除说明与"勿误删资产"清单；
  - `SyncWorkStationForZone` 仍装配 `LastWorkStationStatus`（去掉 `ledgerEntries`）。
- **⚠ 修正记录（照实登记）**：首次实施时误把调用循环**整段删除**，但那会一并删掉 `AlignKeptWorkTypes` 的
  ① 段（"映射类型提到最高档 1"）—— 它是"车间工作优先"的**唯一全 scope 路径**，
  删除会让车间优先退化成只对在班者生效。**已恢复**，只剥降档。
- **旧档一次性迁移**：`WorkshopManageView.Refresh` 每次开面板调 `RestoreAllDemotedWork()` ——
  它**幂等且自清理**（还原即从台账移除，台账空则立刻返回）⇒ **无需新增"已迁移"标记**。
  防的是：旧档里被压到 4 档的项在原出口（按钮）删除后**永久卡住**。
- 行为变化（预期）：其余工作保持玩家原档位；车间相关工作仍是最高档 1。
- `ChronicleGameComponent.DemotionLedger` 字段**保留不删**（不动 Scribe 键结构），仅停止写入。

## 交付二：隐藏名单分账 + 成员差集对账（移出车间即恢复）

- `ChronicleGameComponent.WorkTabHide.cs`：
  - 新增 `autoHiddenWorkTabColonists`（加性字段，Scribe 键 `pcAutoHiddenWorkTab`）；
  - 判定改为**两集合并集**（`IsHiddenFromWorkTab`）；新增 `SetAutoHiddenFromWorkTab`；
  - `ClearHiddenFromWorkTab`（Mod 设置的「恢复显示全部」）改为**两集合一起清**。
- `MapComponent_WorkshopManager.AutoHide.cs`：
  - `SyncAutoHideWorkTabColonists` 改走**自动集合**；
  - 新增 **`ReconcileAutoHideOnLeave()`**：以 `AllWorkshopMemberPawns`（与接管/隐藏/作息表同一份成员口径）
    收集本轮在册集合并与 `lastWorkshopMembers`（Manager 运行态、**不入存档**）做**差集** ⇒
    离开者：① 从自动集合移除（面板恢复显示）② 按人解除接管。
- 调用点（事件驱动、非 tick）：`Refresh` + `OnRosterChanged`，**均在 SyncAutoHide 之后**（先收新人再放旧人）。
- ⚠ **为什么用差集而非给每个移除入口挂钩**：取证显示"移出车间"至少 4 条路径
  （人员页移除 / 班组编辑器移出 / 删除班组 / 删除班次），且成员口径是 `zone.Members ∪ crew.Members`
  —— 逐入口挂钩必然漏、且易与成员口径分叉。
- ⚠ 读档后首次对账：`lastWorkshopMembers` 为空 ⇒ 差集为空 ⇒ **不会误判任何人"已离开"**。

## 交付三：按人解除接管

`MapComponent_WorkshopManager.WorkTakeover.cs` 新增 **`ReleaseVanillaWorkFor(Pawn)`**：
闸门与全量版 `ReleaseVanillaWork` **逐条一致**（`workSettings` 不可用 ⇒ 丢弃；
类型禁用/解析失败 ⇒ 只丢弃；`WorkshopTakeover.ShouldRestore` 判否 ⇒ 只丢弃不还原）
⇒ 玩家手动改过的项不会被覆盖（GOV-005 单一判定）。

## 交付四：UI 与文案

- **删「一键全解除」按钮**（`DrawWorkStationStatusRow`）+ 状态文本占满整行 + 删 `WorkRestoreBtnW` 常量；
- 状态行**保留**「已对齐 / 跳过 / 在册 / 在岗 / 应到岗」（+ 幽灵 / 作息异常），只去掉「台账 N 项」；
- 语言文件：删 `WorkSyncLedger` / `WorkSyncRestore`（中英各 2），重写 `WorkSyncHint`
  （去掉降档与"一键全解除"表述，改为"其余工作保持你原来的档位"）。

## 存档影响

| 字段 | 位置 | 变化 |
|---|---|---|
| `autoHiddenWorkTabColonists`（`pcAutoHiddenWorkTab`） | `ChronicleGameComponent` | **新增**加性字段；旧档为空 ⇒ 既有隐藏项全视为"玩家手动" ⇒ 无迁移 |
| `DemotionLedger` | `ChronicleGameComponent` | **保留**，仅停止写入 |
| `workshopWorkTakeover` | `ChronicleGameComponent` | 不变（按人还原复用同一本） |

⇒ **不需要 bump SchemaVersion、不需要 Migration**（SAVE-004）。

## 验证

1. **降档消失**：排班后打开原版【工作】面板 ⇒ 车间相关工作应为 1，**其余工作保持原档位**（不再全被压到 4）；
2. **旧档不受害**：读旧档（曾降档过的）⇒ 开一次面板后，原先被压到 4 的工作应**已还原**；
3. **移出车间即恢复**：把某人从**人员名单与所有班组**移除 ⇒ 开关一次面板 ⇒
   原版【工作】面板里**重新出现该殖民者**，且其车间类工作**已被勾回**（原档位）；
4. **玩家手动隐藏不受影响**：用 gizmo 手动隐藏某人 ⇒ 无论开关面板多少次，他**始终隐藏**（分账保证）；
5. **Mod 设置「恢复显示全部殖民者」**⇒ 应把两批（手动 + 自动）**一次全清**。

## 残留风险与待办

1. ✅ **`EXC-2026-015` 已同步修订**（本批一并完成）—— 在 `docs/设计文档/架构/EXC-2026-015.md`
   顶部新增 2026-10-05 修订段：申明**降档授权面失效**、列出"仅作迁移保留"的资产清单、
   更正其 `Alt 7` 中"唯一出口 = 一键全解除"的表述（现为**自动化出口**），
   Status 由 `Approved` 改为 **Superseded**。
   ⚠ 该文件位于 `docs/` 下、**不在 git 跟踪范围**（`.gitignore`）⇒ 不进本标注的 files 段，
   在此登记备查（GATE-DELTA 曾因此报"标注中存在 git 无对应改动的文件"，已按规则移出）；
2. ⚠ **`DemotableWorkTypes()` 不是死代码**（`WorkTakeover.cs:191` 仍在用）⇒ 勿删（已在代码注释更正）；
3. `RestoreAllDemotedWork` / `RestorePawnDemotions` / `CurrentDemotionLedger` 保留，**仅供迁移**，
   不可当作"可用功能"再挂 UI 出口；
4. 差集对账的成员口径**必须**始终与 `AllWorkshopMemberPawns` 一致 —— 若将来新增第二个"成员视图"，
   必须同步此处，否则会把仍在车间的人误判为已离开；
5. 「只从班组移出、但仍留在车间人员名单」**不触发**恢复（仍算在车间）—— 这是设计语义，
   若玩家反馈困惑，需在 UI 上补一行说明。
