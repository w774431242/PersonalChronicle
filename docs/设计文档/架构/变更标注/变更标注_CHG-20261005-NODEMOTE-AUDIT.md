---
change_id: CHG-20261005-NODEMOTE-AUDIT
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle, Languages]
files:
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.WorkTabHide.cs
    type: M
    symbols: ChronicleGameComponent.HiddenWorkTabCount, ChronicleGameComponent.ClearHiddenFromWorkTab, ChronicleGameComponent.SanitizeHiddenWorkTab
    lines: 100-135, 175-185, 240-260
  - path: Source/PersonalChronicle/Settings/SettingsDrawer.cs
    type: M
    symbols: SettingsDrawer.PaintHiddenWorkTabAction
    lines: 425-440
  - path: Source/PersonalChronicle/ChronicleSettings.cs
    type: M
    symbols: ChronicleSettings.DrawWorkTabOriginalSection
    lines: 300-320
  - path: Source/PersonalChronicle/Automation/Zone_Workshop.cs
    type: M
    symbols: Zone_Workshop.Delete
    lines: 902-920
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView.Refresh, WorkshopManageView.OnRosterChanged
    lines: 350-360, 1240-1250
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.WorkTakeover.cs
    type: M
    symbols: MapComponent_WorkshopManager.ReleaseVanillaWorkFor
    lines: 327-345
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.AutoHide.cs
    type: M
    symbols: MapComponent_WorkshopManager.ReconcileAutoHideOnLeave
    lines: 154-180
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: WorkTab.Settings.OriginalPanel, WorkTab.Settings.HiddenCount, WorkTab.Settings.ClearHidden, WorkTab.Settings.HiddenCleared
    lines: 3673-3676
---

# 变更标注 · CHG-20261005-NODEMOTE-AUDIT

## 背景

对**自己刚交付**的 `CHG-20261005-NODEMOTE`（降档通道移除 + 出口自动化）做深度审计 ——
因存在"自证清白"风险，改由**三个独立 agent** 并行只读复核（架构/正确性、数据/存档、性能/边界），
合计 186 次工具调用。本节登记对**审计确认为真问题**的修复；已核对通过项与被裁定为可接受项见文末。

## 修复一（P1）：「恢复显示全部殖民者」入口不可达

**问题**：分账后该入口仍按 `HiddenWorkTabColonists.Count`（手动集合）门控 ⇒
PC 自动隐藏成为常态时，计数恒为 0 ⇒ 按钮**不渲染**、还显示"无隐藏成员"⇒ **逃生舱形同虚设**。

**修复**：新增零分配的**并集去重计数** `HiddenWorkTabCount`（手动计数 + 自动集中不在手动集的数量），
`SettingsDrawer.PaintHiddenWorkTabAction` 与 `ChronicleSettings.DrawWorkTabOriginalSection` 改用之。
同时修掉计数虚高：`ClearHiddenFromWorkTab` 的 `removed` 原为**两集合相加**（同一人双集合时重复计数，
与"已恢复显示 {0} 名"文案不符）⇒ 改用同一去重计数。

## 修复二（P1）：删除车间不触发对账 ⇒ 跨存读档后永久漏还原

**问题**：对账只挂在面板 `Refresh` / `OnRosterChanged`，而 `Zone_Workshop.Delete` 是原版路径
（无任何 Manager 通知）⇒ 删区后成员虽已不在册，但若玩家**没开面板就存档**，
运行态快照 `lastWorkshopMembers` 不入档 ⇒ 读档后差集基准被重置
⇒ 这批人的自动隐藏与接管**永久无从还原**。

**修复**：`Zone_Workshop.Delete` 在 `finally` 之后补一次
`MapComponent_WorkshopManager.Get(Map)?.ReconcileAutoHideOnLeave()`（玩家动作路径，非 tick/绘制）。
放在 `finally` 之后的原因：此时 zone 已真正从 `ZoneManager` 移除，成员口径自然已排除它。

## 修复三（P2）：`Refresh` 不调 `SyncAutoHideWorkTabColonists`

**问题**：该方法的唯一调用点是 `OnRosterChanged`，而人员页「添加人员」入口的回调只做
`zone.AddMember`（不触发 `OnRosterChanged`）⇒ 经该入口入册的成员在下次名册事件前**不会被隐藏/接管**，
且反复开关面板也不补救；这与变更标注里"Refresh + OnRosterChanged 均在 SyncAutoHide 之后"的承诺不符。

**修复**：`Refresh` 内补 `SyncAutoHideWorkTabColonists(zone)`（在 `ReconcileAutoHideOnLeave` 之前，
保持"先收新人、再放旧人"）⇒ 覆盖**所有**入册路径。承诺随之成真。

## 修复四（P2）：分账未同步到周边契约

| 子项 | 问题 | 修复 |
|---|---|---|
| `SanitizeHiddenWorkTab` 只清洗手动集合 | 自动集合的过期键永不被清理 ⇒「内存键==存档键」「名单有界」只兑现一半 | 两个集合都清洗；并**声明上限口径**（`MaxHiddenColonists` 只约束手动集合，自动集合规模由成员数自然约束，避免成员多时把玩家手动隐藏的挤掉） |
| 对账遇已消亡 pawn 直接跳过 | 自动隐藏键 + 接管记录双残留（旧注释误称"由 Sanitize 清理"） | 消亡分支顺手 `SetAutoHiddenFromWorkTab(false)` + `DiscardTakeoverPawn`（均幂等） |
| `ReleaseVanillaWorkFor` 缺 pawn 级校验 | 与全量版"逐条一致"承诺不符（全量版过滤 `Destroyed/Dead/非玩家阵营`） | 补齐同款前置校验（防御式，成本可忽略），承诺成真 |

## 修复五（P2）：名册事件重复对账

`OnRosterChanged` 直调 `ReconcileAutoHideOnLeave` 后又由 `Refresh` 再调一次 ⇒
每次名册事件白跑一整轮成员收集与分配（第二次差集恒空）。现只保留 `Refresh` 内那一次
（它同时覆盖"开面板"与"名册变更"两条路径）。

## 修复六（既有缺陷）：英文缺 4 个键

`PersonalChronicle.WorkTab.Settings.{OriginalPanel, HiddenCount, ClearHidden, HiddenCleared}`
中文有、英文**完全没有**（非本批引入）⇒ 已在 `Languages/English/Keyed/Archive.xml` 的
`WorkTab.Settings.EnableTakeover` 旁补齐英文。

## 存档影响：**无**

全部为逻辑/文案修复；未新增或改动任何持久字段，不改 Scribe 键，无 Migration。

## 审计已核对通过项（重要，避免重复排查）

- **成员口径唯一**：隐藏/接管/释放/初始化四处均为 `AllWorkshopMemberPawns`，无分叉；
- **差集无误判**：同一人多车间取并集、`crew/班次/名册`删除均走 `OnRosterChanged`、读档首帧快照为空；
- **两台账分账、无串用**：释放只读接管账（闸门"当前=0"），迁移还原只读降档账（闸门"当前=4"）；
- **不含 tick / 绘制路径写入**：新增写入全部落在 `Refresh`/`OnRosterChanged`/按钮回调；
- **迁移"只跑一次"的隐式表达（台账清空）经论证可靠**：每条必 `Discard`、清空即移除记录、空台账早退、崩溃半途亦幂等；
- **存档**：加性字段，旧档可加载，**无需 Migration、无需 bump schemaVersion**；
- **性能**：全部发生在人手点击/编辑级（无 tick、无每帧），量级 µs~亚毫秒；
  `IsHiddenFromWorkTab` 的并集判定 = 每人每次表格重算 +1 次 HashSet 查找（表格重算是脏驱动，非每帧）。

## 残留与待办（照实登记）

1. ⚠ **旧档"自动隐藏"项全落在手动集合**（结构上无法区分旧自动/旧手动）⇒
   **升级前**加入车间的成员，移出后不会自动恢复显示，需点一次「恢复显示全部殖民者」。
   不做结构迁移的原因：会误揭玩家手动隐藏的人。已在验收说明中登记；
2. ⚠ **离班时段移出的人**，其车间类工作停在"未勾选"（离班链写 0 前已丢弃台账记录，按人解除无记录可还）；
   在班时移出则正常勾回。属产品语义，**待玩家裁定**是否接受（若要改需补写入面 + 例外登记）；
3. ⚠ **其它例外仍在引用降档语义**：`EXC-2026-013`（"部分被 015 取代"）、`EXC-2026-016`（"对降档通道全文有效"）、
   `EXC-2026-023`（仍称"降档还原使用 SetPriority(x,0)"，事实已错）⇒ 需各加一行 2026-10-05 修订注
   （本次未做，避免超出本批范围；**下一轮必须补**）；
4. **死代码/陈旧注释**：`PruneDeadPawns`/`IsPawnStillRelevant`/`LastRestoreCount` 无生产调用方；
   `WorkStationStatus.ledgerEntries` 仍有死赋值；多处注释仍称"降档通道在役"⇒ 随下次触碰清理；
5. **无测试覆盖**：`pcAutoHiddenWorkTab` 与迁移管理层无单测（Tests 仅台账级）⇒ 建议下批补。
