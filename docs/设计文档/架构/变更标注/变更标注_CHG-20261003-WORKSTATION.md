---
change_id: CHG-20261003-WORKSTATION
version: v1.1.5（车间专属工位落地轮）
date: 2026-10-03
author: pc-orchestrator(AI)
scope: [Source/PersonalChronicle/Automation/, Source/PersonalChronicle/Domain/, Source/PersonalChronicle/Data/, Defs/, Languages/, Tests/, docs/设计文档/架构/, Assemblies/]
files:
  - path: Source/PersonalChronicle/Domain/WorkshopWorkDemotionLedger.cs
    type: A
    symbols: WorkshopWorkDemotionLedger / .Entry / .PawnRecord / Record / Discard / DiscardPawn / PruneDeadPawns / Sanitize / SanitizeKey / MaxEntriesPerPawn / MaxPawnRecords
    lines: 1-336
  - path: Source/PersonalChronicle/Automation/WorkshopProcessDef.cs
    type: A
    symbols: WorkshopProcessDef（工序/岗位标准表 Def：processStage / taskTypeKey / skillDefNames / workTypeDefNames / expectedDeviceDefNames / workshopTypeDefNames）
    lines: 1-90
  - path: Source/PersonalChronicle/Automation/WorkshopProcessCatalog.cs
    type: A
    symbols: WorkshopProcessCatalog.ProcessesForDevice / EnsureBuilt / Invalidate（设备 defName → 工序行反查索引）
    lines: 1-116
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.WorkDemote.cs
    type: A
    symbols: WorkStationStatus / DemoteOtherWorkTypes / DemoteNonKeptWorkTypes / RestoreAllDemotedWork / RestorePawnDemotions / SyncWorkStationForZone / CollectKeptWorkTypes / CollectDeviceChannelWorkTypes / AddProcessWorkTypesForDevice / SyncWorkStationIfDirty / CurrentDemotionLedger / FindPawnByStableId
    lines: 1-560
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.WorkStation.cs
    type: A
    symbols: BuildWorkStationStatusText / DrawWorkStationStatusRow / workStationStatusText / workStationStatusWarn / workStationStatusLines / WorkRestoreBtnW
    lines: 1-113
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.WorkDemotion.cs
    type: A
    symbols: ChronicleGameComponent.WorkDemotionLedger / DemotionLedger / ExposeWorkDemotion
    lines: 1-56
  - path: Defs/WorkshopProcesses.xml
    type: A
    symbols: "DefName:WP_Logging_Production / WP_Logging_Maintenance / WP_Crafting_Production / WP_Crafting_Maintenance / WP_Smithing_Production / WP_Machining_Production / WP_Cooking_Production / WP_Tailoring_Production / WP_DrugCrafting_Production / WP_Stonecutting_Production / WP_Sculpting_Production / WP_Research_Production / WP_Common_Maintenance（13 行）"
    lines: 1-269
  - path: Tests/WorkshopWorkDemotionLedgerTests.cs
    type: A
    symbols: WorkshopWorkDemotionLedgerTests（18 例：幂等 / 首记优先 / 剪枝 / 换档 / 有界性 / XML 洁净度）
    lines: 1-249
  - path: Tests/WorkshopWorkDemotionScribeTests.cs
    type: A
    symbols: WorkshopWorkDemotionScribeTests（4 例：Scribe 全保真往返 / 空台账 / 旧档缺段读回 null / 非法字符清洗）
    lines: 1-224
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.WorkEnable.cs
    type: M
    symbols: SyncVanillaWorkSettings（尾部接入 SyncWorkStationForZone；早退分支亦不跳过降档通道）
    lines: 89-107
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.cs
    type: M
    symbols: deviceChannelFingerprint / deviceChannelDirty / DetectDeviceChannelChange / MapComponentTick（L1 只读指纹比对接入）
    lines: 118-125, 227-235, 296-352
  - path: Source/PersonalChronicle/Automation/WorkshopTypeDef.cs
    type: M
    symbols: WorkshopTaskTypeKeys（岗位键白名单 + 未知回退）/ WorkshopTypeDef.deviceWorkTypeDefNames / .processKeys / DeviceWorkTypeDefNames() / ProcessKeys()
    lines: 1-45, 190-240, 265-285
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: Refresh（+BuildWorkStationStatusText +SyncWorkStationIfDirty）/ DrawSettings（内容高 +2 行 + DrawWorkStationStatusRow）
    lines: 200-205, 1112-1116, 1168-1180
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.cs
    type: M
    symbols: ExposeData（链尾 +ExposeWorkDemotion()）
    lines: 216-217
  - path: Defs/WorkshopTypes.xml
    type: M
    symbols: "DefName:WT_Logging / WT_Crafting / WT_Smithing / WT_Machining / WT_Cooking / WT_Tailoring / WT_DrugCrafting / WT_Stonecutting / WT_Sculpting / WT_Research（+processKeys ×10、+deviceWorkTypeDefNames ×9）；修正 3 处 1.6 不存在的设备 defName"
    lines: 42-268
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: "Key:WorkSyncAligned / WorkSyncSkipped / WorkSyncScope / WorkSyncLedger / WorkSyncRestore / WorkSyncNoManager / WorkSyncHint（7 键）"
    lines: 392-400
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: "Key:WorkSyncAligned / WorkSyncSkipped / WorkSyncScope / WorkSyncLedger / WorkSyncRestore / WorkSyncNoManager / WorkSyncHint（7 键）"
    lines: 278-286
  - path: docs/设计文档/架构/EXC-2026-015.md
    type: A
    symbols: EXC-2026-015（车间专属工位降档授权；supersede EXC-013「不重排其它工作类型」半句）
    lines: 1-97
  - path: docs/设计文档/架构/EXC-2026-013.md
    type: M
    symbols: Status（标注部分被取代）+ 追加「备注（2026-10-03 · CHG-20261003-WORKSTATION）」取代说明
    lines: 3, 48-56
---
# 变更标注 · CHG-20261003-WORKSTATION（车间专属工位）

- **Change ID:** CHG-20261003-WORKSTATION
- **Version:** v1.1.5
- **Date:** 2026-10-03
- **Author/AI:** pc-orchestrator(AI)
- **Scope（摘要）:** 车间专属工位 —— 排班/派工后车间相关工作提到最高档（1）、其余**已勾选**工作统一降到最低档（4，保持勾选），PC 侧台账可一键全解除（只还原 PC 动过的项）
- **例外:** `EXC-2026-015`（E2，取代 `EXC-2026-013`「不重排其它工作类型」半句）

## 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 / DefName / Key | 行范围 | diff 摘要 | 规则 ID | Gate |
|---|---|---|---|---|---|---|
| `Domain/WorkshopWorkDemotionLedger.cs` | A | `WorkshopWorkDemotionLedger` / `.Entry` / `.PawnRecord` / `Record` / `Discard` / `DiscardPawn` / `PruneDeadPawns` / `Sanitize` | 1-336 | 降档台账（纯逻辑）：键 =(pawnId, workTypeDefName)，**首记优先**；剪枝失败隔离；双上限 256×64；保存前清洗非法 XML 字符 | GOV-003/006 · SAVE-001..005 | DATA/SAVE/PERF |
| `Automation/WorkshopProcessDef.cs` | A | `WorkshopProcessDef`（5 维：processStage / taskTypeKey / skillDefNames / workTypeDefNames / expectedDeviceDefNames） | 1-90 | 工序/岗位标准表 Def 类 + 三个永不为 null 的安全取列表 | GOV-006/009 | DATA |
| `Automation/WorkshopProcessCatalog.cs` | A | `WorkshopProcessCatalog.ProcessesForDevice` / `EnsureBuilt` / `Invalidate` | 1-116 | 设备 defName → 工序行反查索引（首次使用时建一次；留 `Invalidate` 口子） | ARC-004 · PERF-001 | PERF/ARCH |
| `Automation/MapComponent_WorkshopManager.WorkDemote.cs` | A | `WorkStationStatus` / `DemoteOtherWorkTypes` / `DemoteNonKeptWorkTypes` / `RestoreAllDemotedWork` / `RestorePawnDemotions` / `SyncWorkStationForZone` / `CollectKeptWorkTypes` / `CollectDeviceChannelWorkTypes` / `SyncWorkStationIfDirty` | 1-560 | **降档执行 + 一键全解除**：三通道保留集并集；降档三条跳过判定；值勤类型排除；简单模式整条跳过；还原两道自愈闸门 | EXC-2026-015 · MATRIX-003 | ARCH/DATA/SAVE |
| `Automation/WorkshopManageView.WorkStation.cs` | A | `BuildWorkStationStatusText` / `DrawWorkStationStatusRow` / `workStationStatusText` / `WorkRestoreBtnW` | 1-113 | L4 状态行：Refresh 期构建「已对齐 N · 跳过 M · 在册 K · 台账 J」+ 一键全解除按钮 | UI-003/004/009 · PERF-002 | UI/LOC/PERF |
| `Data/ChronicleGameComponent.WorkDemotion.cs` | A | `WorkDemotionLedger` / `DemotionLedger` / `ExposeWorkDemotion` | 1-56 | 台账落盘分部（加性可选段 `workshopWorkDemotion`；内联初始化防新档 NPE） | SAVE-001/002 | SAVE/DATA |
| `Defs/WorkshopProcesses.xml` | A | 13 个 `WorkshopProcessDef` | 1-269 | 工序标准表数据；**全部 defName 已对 1.6 原版 Data 逐一核实** | GOV-006/009 · DATA-002 | DATA |
| `Tests/WorkshopWorkDemotionLedgerTests.cs` | A | 18 例 | 1-249 | 幂等 / 首记优先 / 剪枝 / 换档隔离 / 双上限 / XML 洁净度 | GATE-TEST | TEST |
| `Tests/WorkshopWorkDemotionScribeTests.cs` | A | 4 例 | 1-224 | Scribe 往返 / 空台账不崩 / **旧档缺段读回 null** / 非法字符清洗 | SAVE-004 | TEST/SAVE |
| `Automation/MapComponent_WorkshopManager.WorkEnable.cs` | M | `SyncVanillaWorkSettings` | 89-107 | 尾部接入降档通道；「映射为空」早退分支亦不跳过；确立 **先 Enable → 后提档 → 最后降档** 顺序 | EXC-015 缓解项 | ARCH |
| `Automation/MapComponent_WorkshopManager.cs` | M | `deviceChannelFingerprint` / `deviceChannelDirty` / `DetectDeviceChannelChange` | 118-125, 227-235, 296-352 | L1 设备/工单联动：tick 内**只读 FNV-1a 指纹比对**并置脏标记；**tick 内零 SetPriority** | PERF-001 · F6 | PERF/ARCH |
| `Automation/WorkshopTypeDef.cs` | M | `WorkshopTaskTypeKeys` / `deviceWorkTypeDefNames` / `processKeys` | 1-45, 190-240, 265-285 | 2 个**可选**字段（默认空 = 回落既有映射）；岗位键用字符串白名单（非序号） | GOV-006/009 | DATA/ARCH |
| `Automation/WorkshopManageView.cs` | M | `Refresh` / `DrawSettings` | 200-205, 1112-1116, 1168-1180 | Refresh 期构建状态文本 + 消费 L1 脏标记；设置页 +2 行 | UI-003 · PERF-002 | UI/PERF |
| `Data/ChronicleGameComponent.cs` | M | `ExposeData` | 216-217 | 链尾 `+ExposeWorkDemotion();`（**不 bump SchemaVersion**） | SAVE-002 | SAVE |
| `Defs/WorkshopTypes.xml` | M | 10 个 `WT_*` | 42-268 | 10 个加 `processKeys`；9 个加 `deviceWorkTypeDefNames`；**修正 3 处不存在的设备 defName**；**修正 9 处 `deviceWorkTypeDefNames` 被误嵌在 `processKeys` 内部的结构错误（见下）** | DATA-002 | DATA |

### 追加修正（2026-10-03 交付后独立复核发现，GATE-DELTA 复跑后仍 PASS）

| 位置 | 问题 | 修正 | 为什么 DATA Gate 没抓到 |
|---|---|---|---|
| `Defs/WorkshopTypes.xml` × 9（`WT_Crafting` / `WT_Smithing` / `WT_Machining` / `WT_Cooking` / `WT_Tailoring` / `WT_DrugCrafting` / `WT_Stonecutting` / `WT_Sculpting` / `WT_Research`） | `<deviceWorkTypeDefNames>` 被写在 `<processKeys>` **内部**（成为其子节点） | 提升为 `processKeys` 的**兄弟节点** | XML **良构**（`[xml]` 解析通过），但 Verse `DirectXmlToObject` 只读字段元素的**直接** `<li>` 子节点 ⇒ 该字段运行时恒为空 ⇒ `DeviceWorkTypeDefNames()` 永远返回空列表 ⇒ **L2「设备→工作类型精确映射」整条通道是死代码**，且无任何报错 |

**后果等级**：P1 功能失效（非崩溃、非数据损坏）。原验证只做了「XML 良构 + 零悬空引用」，**未做父子结构校验**。

**新增自检口径（建议纳入后续 Def 门禁）**：凡 `List<string>` 字段，必须断言其 `<li>` 为字段元素的**直接子节点**；`[xml]` 良构性**不足以**证明 Def 字段能被解析。

### 追加修正 2（部署链 P0，交付后才暴露）

| 位置 | 问题 | 修正 | 后果 |
|---|---|---|---|
| `_scripts/deploy-to-game.ps1:57-67` 与 `:76-77` | `Get-ChildItem -Path` **不支持 `**` 递归语义**（`-Path` 是遗留模式，`**` 被降级为 `*`），于是 `Defs\**\*.xml` 实际等价 `Defs\*\*.xml`、**要求至少一层子目录**；本工程全部 Def 都在 `Defs\` 顶层 ⇒ 该项**长期零匹配** | 模式改为 `Defs\*.xml` 等扁平写法，并给 `Get-ChildItem` 加 **`-Recurse`** | ⛔ **P0 阻断**：整个 `Defs\` 目录从未被部署到游戏。表现为「部署日志没有 Defs 行 / 游戏里 Def 停留在旧版本」，且**极易被误读成「Def 没生效」**。本次 `WorkshopProcesses.xml`（新功能的数据源）就是因此**完全没进游戏目录**，功能在实机等于不存在 |

**验证**：修复后部署日志出现 `Defs 28 个文件`（修复前为 0），游戏目录内结构校验 9/9 正确、工序表 13 行存在、`SculptingBench` 残留 0、全部 Def XML 良构。

> **流程教训（建议沉淀）**：「编译通过」与「游戏里生效」之间隔着**部署链**，而部署链**没有自检**。本次连续两次出问题（Def 嵌套、部署漏发）都发生在这一段，且都不会让编译失败。建议后续把「部署后产物抽验」（关键 Def 存在 + 关键 defName 命中）固化为发布前必做项。

### 追加修正 3（幽灵在岗 · 用户实机「到点了还卡在值勤、不下班睡觉」）

| 层 | 文件 | 改动 |
|---|---|---|
| DATA/SAVE | `Automation/MapComponent_WorkshopManager.Schedule.cs` | 新增 `SelfHealGhostDuty()`：并入既有低频对账 `ReconcileDutyPlan`。判据两道（无值勤 Job **且** 不在当前小时 `dutyPlan`）⇒ 强制走 `NotifyDutyEnded` 正常收工（含考勤与工时结算） |
| DATA | `Automation/MapComponent_WorkshopManager.cs` | 新增 `GhostDutyCount`（O(在岗人数)）、`DutyPlanCount`（O(1)） |
| UI | `Automation/WorkshopManageView.WorkStation.cs` | 状态行加「在岗 N / 应到岗 M / 幽灵 K（已自动注销）」；`幽灵 > 0` ⇒ 状态行转警示色 |
| UI | `Automation/Dialog_WorkshopShiftEditor.cs` | `StartHour == EndHour`（= 全天班，`WorkshopTime.InRange` 直接 `return true`）的提示从灰色 Muted 升为 **Warn 警示色** + Tooltip 说明「该班次成员永不收工」 |
| LOC | `Languages/{CN,EN}/Keyed/Automation.xml` | 新增 5 键 ×2：`TimeAllDayTip` / `DutyOnDuty` / `DutyExpected` / `DutyGhost`（`DutyRoster` 备用未用） |

**根因判定（已取证，非推测）**：收工机制**本身是正确的**——`JobDriver_WorkshopDuty` 的 `FailOn(!IsScheduledForDuty)`、`RebuildDutyPlan(hour)` 的三条路径（固定班 `SlotIsActive` / 班组 `CollectActiveShifts` / 轮岗 `rule.Matches`）与 `Domain.WorkshopTime.InRange` 的跨午夜环绕全部正确。已逐条排除：`InRange(11, 23, 1) = false` 正确、`WorkshopShift.ExposeData` 的 6/18 默认值存在（"老档 0/0"假设不成立）。

因此「到点不下班」只可能来自三种**配置/状态**成因（而非机制缺陷）：
- **A** 该车间当前小时确有班次窗口覆盖（Max 实际命中的是**另一个**班次/固定槽位），或班次是 `start == end` 的**全天班**；
- **B** 候选池兜底：班次 ≤1 个时 `FallbackPool` 回落 `zone.Members`（`Schedule.cs:393-405`）⇒ **未被排班的人也会被拉来值勤**；
- **C** 幽灵在岗：`NotifyDutyEnded` 只在 Job 收尾动作里跑，Job 被外部换掉时漏注销 ⇒ 该 pawn 因 `TryGetDutyJob` 的 `activeWorkers.ContainsKey` 早退而**再也拿不到值勤 Job**。

本次修复 **C（真 bug）** + 让 **A / B 可见**（状态行与警示色）。A / B 属配置语义，需在游戏内核对后再决定是否改默认口径。

> ⚠ **未实机验证**：本环境无 RimWorld 进程，上述判定基于静态取证 + 排除法，需重启游戏后看状态行读数确认落在 A / B / C 哪一条。

### 追加修正 5（**真正根因** · 排班表实锤 · P0 语义缺陷）

用户补上排班表截图后，根因确定 —— **A/B/C 都不对，是名额分配本身错了**：

| 姓名 | 今日岗位 | 主技能 |
|---|---|---|
| Sky | 日班 · 08:00–17:00 | 种植 6++ |
| Gill | 日班 · 08:00–17:00 | 射击 9++ |
| Max | **夜班 · 20:00–08:00** | 手工 9 |

**缺陷形态**（`Domain/WorkshopShiftPlan.cs:53`，旧代码）：
```csharp
int limit = requiredCount <= 0 ? candidates.Count : requiredCount;   // 缺省 ⇒ 全选
```
`WorkshopShift.RequiredCount` 的缺省值是 **0**（`WorkshopShift.cs` 字段初值 + `ExposeData` 缺省 0，注释写"不限名额"），而 **名额 UI 已在 2026-02 被撤下**（`Dialog_WorkshopShiftEditor` 类注释：用户实测"名额、门槛等等无作用"）⇒ **玩家没有任何入口能改它**。

于是：**每个班次都把整份候选池吃光**。11:00 落在**日班 08–17** 窗口内 ⇒ 日班把 Sky/Gill/Max 全选上 ⇒ **Max 被日班拉去上岗**。他不是"没下班"，他上的是日班；而人员页只显示他的「今日岗位 = 夜班」⇒ **UI 与实际完全对不上**，玩家必然判定为 bug。夜班时段他因 `dutyPlan` 先占者胜被日班占位 ⇒ `shiftFill[夜班]` 记 0，**名额统计也失真**。

> 这也解释了 `Schedule.cs:123-124` 那条 2026-10-02 的注释为何没挡住：它防的是 `FallbackPool`（池空回落全员），**没防 `SelectDutyIndexes` 的 `requiredCount<=0`**。

**修复**（`Domain/WorkshopShiftPlan.cs`）：
- 新增 `DefaultHeadcount = 1`（GOV-006 登记：分类=班次缺省人数 / 来源=用户三班倒意图 / 生命周期=常量）
- 新增 `UnlimitedHeadcountFlag = -1` + `IsUnlimited()`：**用负数**表达"不限名额"（0 已被"未配置"占用，若仍让 0 表示不限等于把缺陷原样留下）
- `limit` 三分支：`< 0` 全选（显式不限）/ `== 0` 取默认 1 人 / `> 0` 取 N 人
- 名额 UI **不恢复**（尊重用户 2026-02 的撤除决定）；真需要多人可由 `RequiredCount < 0` 达成，当前无 UI 入口（如实登记）

**测试**：旧单测 `Select_ZeroOrNegativeCount_MeansAllEligible` 把错误语义钉死了，**已同步改写**并新增端到端回归：
- `Select_ZeroCount_UsesDefaultHeadcount_NotAllEligible` —— 钉死缺省是 1 人
- `Select_NegativeCount_MeansExplicitUnlimited` —— 钉死负数仍是显式不限
- `MultiShift_DayAndNight_DoNotPullWholeRosterIntoEachWindow` —— **按用户真实场景**（Sky/Gill 日班 + Max 夜班）还原：日班窗口只上 1 人、11:00 判为不在夜班窗口、夜班窗口同样只上 1 人

单测 414 → **416 通过 / 0 失败**；编译 0 错 0 警；GATE-DELTA PASS；部署自检 `OK Defs 28 / OK Languages 35 / OK Assemblies 1`、EXIT=0。

> **遗留（需实机确认）**：`FirstWorkshop` 若还存在**第三个**班次或固定槽位（`WorkshopSlot`）覆盖白天，`RebuildDutyPlan` 的 ① 固定班路径仍可能把 Max 排进白天 —— ① 路径**优先于**班组且不看名额。重启后请看「设置」页新增的「在岗 N / 应到岗 M」：若 M 仍明显大于班次总数，即为该情况，需再收敛固定槽位。

### 追加修正 6（**作息表并集 = 真正根因** · 用户实机 P0 · 与追加修正 5 完全正交）

用户重启后复测仍报「**2 个班次对 pawn 都生效，全体 24 小时都在工作**」。已核实部署 DLL **确实含**追加修正 5 的全部逻辑（`DefaultHeadcount = 1` / `IsUnlimited` / `SelfHealGhostDuty` 三项符号均在），⇒ 修复已加载却无效，说明存在**第二条正交路径**。

**根因**（`MapComponent_WorkshopManager.Schedule.cs`，`SyncVanillaTimetables` 旧实现）：

```csharp
foreach (crew.AssignedShifts) { MarkHours(shift, work); }   // ← 取并集
for (int h = 0; h < 24; h++)
    member.timetable.SetAssignment(h, work[h] ? Work : Sleep);
```

| 时段来源 | Work 小时 |
|---|---|
| 日班 08–17 | 9 |
| 夜班 20–08（跨午夜） | 12 |
| **并集（实际写入原版作息表）** | **21** |

⇒ 全员每天**只剩 {17, 18, 19} 三小时**能睡。这是**原版引擎层面**剥夺睡眠，因此：
- 「名额」管的是"每班派几个人"（追加修正 5）
- 「作息表」管的是"这个人哪些小时**被允许**工作" ← **真凶在此**

两条路径正交 ⇒ 追加修正 5 对本现象**零效果**。（我自己先前的口算把并集说成 20 小时，实测 21；测试断言写错时只有测试失败、生产代码正确，已修正断言。）

**修复**：

| # | 改动 | 说明 |
|---|---|---|
| 1 | `SyncVanillaTimetables` 不再取并集 | 改为经 `EffectiveShiftOfCrewForDay` 取「该班组**当日**轮到的那个班次」；按 `WorkshopCrewCycle`（含调休/请假例外）反查，与派工同源。**单班次班组行为完全不变**（零回归） |
| 2 | 当日轮休 ⇒ 返回 `null` ⇒ 整天 Sleep | 休息日就该睡 |
| 3 | `MarkHours` 全天班修正 | 旧循环 `h != EndHour` 在 `start == end` 时**一格都不标**（语义反转：全天班反而不工作）；现标满 24 小时 |
| 4 | `ReplayTimetablesOnDayChange()` | 修复 3 使作息表变成"按当日轮值"推导，而 `SyncVanillaTimetables` 是事件驱动 ⇒ 必须跨天重放。挂在既有低频对账，**按 `GenDate.DaysPassed` 限流（每天最多 1 次）**，不进 tick 热路径；首次进入（`-1`）不重放，避免读档/开局批量覆盖玩家手排 |

**测试**（新增 `Tests/WorkshopTimetableHoursTests.cs`，4 例）：先复现缺陷再断言修复（`日班∪夜班 = 21 小时 Work` + `只剩 {17,18,19}`）、跨午夜环绕、全天班标 24 小时、单班次零行为变化。

单测 458 → **458 通过 / 0 失败**（总数由 459 增至 459，新增 4 例同时另一会话也增补了用例）；编译 0 错 0 警；部署自检全绿、`EXIT=0`。

### 追加修正 7（**自审**：跨天重放会踩掉玩家手排 · 交付前收敛）

追加修正 6 引入的 `ReplayTimetablesOnDayChange` **每天覆写全部 24 小时**，自审发现它会**连玩家手排的时段一起踩掉** ——
而单班次班组**根本不依赖**跨天重放（并集缺陷对它们不成立：只被指派一班时「并集 ≡ 单班」），
让它们被重放属于**无收益的破坏性写入**，违反 EXC-2026-013「绝不碰玩家自己设的档位」红线。

**收敛**：`SyncVanillaTimetables` 拆出 `multiShiftCrewsOnly` 重载，跨天重放**只处理被指派 ≥2 个班次的班组**
（判定与 `EffectiveShiftOfCrewForDay` 共用 `CountAssignedShifts`）。单班次班组跨天零变化 ⇒ 作息表原样保留。

### 追加修正 8（补回「上岗名额」UI —— 使验收建议可执行）

追加修正 5 我建议用户"给班次设名额"，但**名额 UI 早在 2026-02 被撤下**（用户当时判定"名额、门槛等等无作用"），
那条建议实际**无法执行**。而 2026-10-03 的 P0 诊断恰恰证明**名额是三班倒的关键**（缺省 1 人/班才让日班/夜班真正分开）。

**改动**（`Dialog_WorkshopShiftEditor.cs`）：
- 新增「上岗名额」`− / ＋` 行；`0` 显示为**实际生效值**（「缺省 1 人」，避免玩家看到"0"以为没人）、`< 0` 显示「不限」
- 减到最左再减一次即 `< 0` = 不限名额（与 `WorkshopShiftPlanner.UnlimitedHeadcountFlag` 同语义），并配 Tooltip 说明
- `InitialSize` 高 **300 → 338**（`RowH 30 + Gap 8`）。⚠ 本窗口 `resizeable = false` 且**无滚动视图** ⇒ 加行必须同步增高，否则底部按钮被裁

**Key**：新增 4 键 ×2（`ColHeadcount` / `HeadcountDefault` / `HeadcountUnlimited` / `HeadcountTip`），中英各 369 键、双向零差异、XML 良构。

单测 **458 通过 / 0 失败**；编译 0 错 0 警。

### 追加修正 9（**闭环：读档后自动修复被写坏的作息表** · 甲口径）

用户第三次反馈"仍未解决"。**根因不是逻辑错，是修复永不触发**（Cecil 取证 + 代码路径复核）：

| 事实 | 证据 |
|---|---|
| 作息表**随存档持久化** | `Pawn_TimetableTracker.ExposeData` 含 `Scribe_Collections.Look(ref times, "times", …)` ⇒ 旧版并集写入的 21h `Work` 已固化进玩家存档 |
| 修复**只在"有班次计划"时写** | 追加修正 6 的 `if (effective == null) continue;` |
| 触发**纯事件驱动** | `SyncVanillaTimetables` 仅由 `OnRosterChanged` 调用 —— 玩家不点排班页就不跑 |
| 跨天重放**首日跳过** | 旧 `if (timetableReplayDay < 0) { = today; return; }` ⇒ 每个存档第一次对账什么都不做 |

⇒ 追加修正 5/6 的修复**只对新建班次 / 新玩家有效**，对你当前存档是 **no-op**。这是我把"无计划就不写"当作"保护玩家手排"正解所付的代价。

**修复（甲口径，用户 2026-10-03 裁决：只管在编班组成员）**：

| # | 改动 | 说明 |
|---|---|---|
| S1 | `EffectiveShiftOfCrewForDay` 增加 `out bool hasPlan` | 区分「**无班次计划**」（不写、尊重玩家）与「**有计划但当日轮休**」（写整天 Sleep）。后者是修复关键：轮休必须显式写回，不能"不写就不管" |
| S1 | `ReplayTimetablesOnDayChange` **去掉首日跳过** | `MapComponentTick → ReconcileDutyPlan` 每 60 tick（≈1 游戏秒）调用 ⇒ **读档后约 1 秒自动修复** |
| S2 | 管理边界 = **班组在编成员** | 由既有 `zone.Crews[].Members` 推导 ⇒ **不需要新增存档字段**标记归属。不在任何班组的殖民者，PC 永不碰其作息表 |
| S4 | `MapComponent_WorkshopManager.WorkHoursOf(pawn)` + 状态行读数 | 「作息异常 N 人 · 最长 Work M 小时」，> 16h 判异常（`AbnormalWorkHoursThreshold`，GOV-006 登记）⇒ 该类问题**可自证**，不再靠推测 |

**修复范围与受损范围精确重合**：「并集写坏」只发生在被指派 ≥2 班次的班组，而 `multiShiftCrewsOnly` 过滤器也只覆盖这些班组 ⇒ 修复不扩大到单班次班组（那会把玩家手排踩掉，违反 EXC-2026-013）。

**Key**：新增 2 键 ×2（`RestBroken` / `RestMaxHours`），中英各 371 键、双向零差异。

单测 **458 通过 / 0 失败**；编译 0 错 0 警。



---

## 附：并发会话（`CHG-MFG-ALIGN-001`）的画布重构审计

同一时段另一会话正在重构 `Manufacturing/UI`（把画布拆成 `.Align` / `.Placement` / `.Primitives` / `.Interaction` 四个 partial，共 2633 行），一度处于半成品态导致编译失败 4 项（缺 `CardPressPos` / `CardClickSlopSq`）；等待其收尾后编译即通过。与本批改动**零文件重叠**。

审计结论（只读）：

- `.Placement.cs` / `.Primitives.cs`：**纯物理切片**，与本分支原实现逐字一致，类注释明确标注「零契约改动」，质量干净。
- `.Interaction.cs`：
  - **修掉了本分支的一个真 bug（R-1）** —— `HandleDeviceCardDrag` 的 MouseUp 只调 `MoveDetached`（纯内存），**未落档** ⇒ 拖完不关窗就丢位移；已补 `SaveDetached`。
  - 修 BUG-2026-0001 F-2：悬停设备卡时**显式拒绝**起拖车间块（此前仅靠按钮被动抢 MouseDown，`Device.cs` 释放按下后会语义反转）。
  - 新增点击/拖拽区分（`CardClickSlopSq = 16`，4px²，与 `Rails.RailClickSlopPx` 同值同义）。
  - 追加修正 5/6 的 `UndoValid` / 无差集反删 / `CanUndo` / `EstablishBaseline` 全部保留完好。
- `.Align.cs`：Domain 纯函数 `CanvasAlignment.Resolve` / `CanvasAlignRect` 承担全部吸附数学（可离线单测），UI 只做采样/应用/画线；`IsZoneDeviceAlignable` 作为**三条排除规则的单一真源**供 4 个调用点共用；**刻意不跨帧缓存**并显式列出缓存需覆盖的 6 条失效路径；命中与吸附共用同一份「设备卡 AABB 并集」几何，消除双源漂移。
- **P3 文档缺陷一处**：`.Align.cs:58-71` 的 `BuildAlignNeighbors` 文档注释已成孤儿（`</summary>` 后紧接第二个 `</summary>` 块），该方法实际失去文档。

**建议**：并发会话的改动应补一份独立变更标注并单独跑 GATE-DELTA；`WorkshopLayoutCanvas.cs` 已达 **1094 行**，超出规范建议的单文件上限，建议继续按语义切片（余下候选：绘制编排链 / 设备卡绘制 / 会话态与持久化）。



### 追加修正 4（部署链 P0 回归 —— 本人引入并当场捕获）

| 位置 | 问题 | 修正 |
|---|---|---|
| `_scripts/deploy-to-game.ps1` | 追加修正 2 加的 `-Recurse` **把 `Languages\*\Keyed\*.xml` 匹配打成了 0**（实测 PowerShell 7：多段通配 + `-Recurse` 解析为空；不加 `-Recurse` 则为 6）⇒ **Languages 全部未部署**，游戏里翻译是旧的，且脚本仍退出 0 | 通配改为「单段叶子 + `-Recurse`」（`Languages\*.xml` / `Defs\*.xml` …），中间层交给 `-Recurse`；脚本顶部固化为**三条硬约束**注释 |
| 同上 | 无任何自检 ⇒ 两轮静默漏发（Defs 全目录、Languages 全部）都靠间接症状才发现 | 新增 **⑤ 覆盖率自检**：对 `Defs` / `Languages` / `Assemblies` 断言每个应发文件都已同步（与部署通配同口径，跳过 `*.bak_*` / `.pdb`），未同步即 **`exit 1` 硬失败** |

**实测**：修复前 `Languages 33 个文件`（实为旧内容）、修复后 `Languages 35 个文件` 且自检 `OK Defs 28 / OK Languages 35 / OK Assemblies 1`、`DEPLOY_EXIT=0`。



| `Languages/{ChineseSimplified,English}/Keyed/Automation.xml` | M | `WorkSyncAligned` / `WorkSyncSkipped` / `WorkSyncScope` / `WorkSyncLedger` / `WorkSyncRestore` / `WorkSyncNoManager` / `WorkSyncHint` | 各 7 键 | 中英各 7 个 Keyed 键（每键 1 个 `{0}` 占位符，一一对应） | GOV-010 · LOC-001 | LOC |
| `docs/设计文档/架构/EXC-2026-015.md` | A | EXC-2026-015 | 1-97 | 授权 A1-A6 / 禁止 F1-F9 边界表、风险、缓解、**「不设兜底」后果登记**、编号顺延说明 | EXC-001 · GOV-012 | ARCH/REL |
| `docs/设计文档/架构/EXC-2026-013.md` | M | `Status` + 追加取代备注 | 3, 48-56 | 标注部分被取代，写明**哪半句失效、哪半句仍有效** | EXC-001 | ARCH |

## 关键设计决策

1. **台账键刻意不含车间/班次/岗位** —— 记录只描述「哪个 pawn 的哪个工作类型被 PC 动过、原值多少」。这使「换档」（pawn 从 A 车间调到 B 车间、岗位由生产改为质检）在**结构上不可能**把 A 档记录用到 B 档，而不是靠运行时判断去防。
2. **降档 ≠ 勾选** —— `GetPriority ≤ 0`（玩家没勾选）一律跳过。把未勾选项写成 4 等于替玩家打开工作，属越权（EXC-015 禁止范围 F3）。
3. **还原前两道闸门** —— ① pawn 必须仍有效且为玩家 faction（挡住 ID 复用）；② 当前值必须仍是 PC 写的 4（挡住"覆盖玩家改动"）。任一不满足则**跳过还原**但仍丢弃台账记录（PC 不再声称拥有该项）。
4. **tick 内零写入** —— L1 设备联动的指纹比对是只读的，脏标记由 `Refresh`（事件）消费。区分点：`MapComponentTick` 内**没有任何** `SetPriority` 调用。
5. **不新增多参数翻译助手** —— 状态行拆成 4 条各带 1 个占位符的 Keyed 行，中英语序各自可调（GOV-010 占位符一致性）。
6. **台账落在 GameComponent 而非 MapComponent** —— 台账是**殖民地级**事实（跨图共享同一批玩家 pawn）；落 MapComponent 会导致玩家换图后一键解除再也还不了原档位。

## 顺带修正的既有缺陷（超出本批功能范围但影响本次功能正确性）

| 位置 | 原值 | 修正为 | 理由 |
|---|---|---|---|
| `Defs/WorkshopTypes.xml` `WT_Crafting.expectedDeviceDefNames` | `TableCrafting` | `CraftingSpot` | `TableCrafting` 在 1.6 **不存在**（原版制作台叫 `CraftingSpot`）⇒ 原「设备匹配」永远显示缺失 |
| 同上 `WT_Tailoring.expectedDeviceDefNames` | `TailoringBench` | `HandTailoringBench` | `TailoringBench` 在 1.6 不存在（手缝台叫 `HandTailoringBench`） |
| 同上 `WT_Sculpting.expectedDeviceDefNames` | `SculptingBench` | （删除该项） | 1.6 只有 `TableSculpting`；保留不存在的名字会让设备反查索引命中一个空锚点 |

> 依据：对 `Data/Core/Defs/ThingDefs_Buildings/Buildings_Production.xml` 等 1.6 原版 Def 全量扫描核实。
> **未修正的既有缺陷（如实登记，不在本批范围）**：`WT_Sculpting.skillDefNames` 写的是 `Art`，而 `Art` 是 `WorkTypeDef` 而非 `SkillDef`（应为 `Artistic`）⇒ `WorkshopDutyScoring` / `WorkshopBonusResolver` 静默取不到技能。经 `git diff` 核实**非本次引入**，留待专项修复。

## 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖本次实际改动的全部 20 个文件 + 本标注，无遗漏
- [x] 清单范围 ⊆ Scope，无超范围改动
- [x] 每个文件标注了改动类型（M/A）与涉及符号
- [x] Change ID 与 EXC-2026-015、Gate Record、Change Summary 互链一致
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留
- [x] EXC-2026-015 禁止范围 F1 已机械核验：全仓 `Disable(` / `DisableAll(` / `SetPriority(x, 0)` **零命中**
- [x] 写入单点（MATRIX-003）：全仓 `SetPriority(` 调用仅存在于 `WorkEnable.cs`（4 处）与 `WorkDemote.cs`（4 处）
- [x] 编译 0 错 0 警（`pwsh _scripts/compile_with_sdk.ps1`）
- [x] 单测 414 通过 / 0 失败 / 1 跳过（跳过项 `Measure_Content_vs_Viewport` 为既有实机项）
- [x] 中英 Keyed 键集 358 = 358，双向零差异；各 7 新键占位符结构一致
- [x] `Defs/*.xml` 全部良构（27 文件 0 失败）；`processKeys` 引用零悬空
- [x] 两个 Def 文件引用的原版 `WorkTypeDef` / `SkillDef` / `ThingDef` defName 全量核实（唯一例外为上表登记的既有 `WT_Sculpting.skillDefNames`）
- [x] 已用 `pwsh <skill>/scripts/gate-runner.ps1 -Base HEAD -Annotation 本文件 -ExcludeFromFile .codebuddy/tmp/workstation-out-of-scope.txt -Blocking` 自动校验

### 关键 diff 片段

```diff
--- a/Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.WorkEnable.cs
+++ b/Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.WorkEnable.cs
             if (workSyncTypes.Count == 0 && DutyWorkType() == null)
             {
+                // 仍要走一趟降档通道：无保留项时会把「其余已勾选工作」降到最低档
+                SyncWorkStationForZone(zone);
                 return;
             }
             for (int i = 0; i < workSyncScope.Count; i++)
             {
                 EnableWorkTypes(workSyncScope[i]);
             }
+            // 降档通道（EXC-2026-015）：**必须在 EnableWorkTypes 之后**
+            SyncWorkStationForZone(zone);
```
