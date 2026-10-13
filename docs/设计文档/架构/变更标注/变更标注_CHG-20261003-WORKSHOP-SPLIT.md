---
changeId: CHG-20261003-WORKSHOP-SPLIT
title: 车间名册/班次/值勤三模块拆分 + 修睡眠时段按天翻转与值勤边界反复中断
version: v1.1.7
date: 2026-10-03
scope:
  - path: Source/PersonalChronicle/Domain/Workshop/WorkshopRosterRules.cs
    type: A
  - path: Source/PersonalChronicle/Domain/Workshop/WorkshopShiftPolicy.cs
    type: A
  - path: Source/PersonalChronicle/Domain/Workshop/WorkshopDutyRules.cs
    type: A
  - path: Source/PersonalChronicle/Automation/WorkshopRestPolicyDef.cs
    type: A
  - path: Source/PersonalChronicle/Automation/WorkshopDutyCoordinator.cs
    type: A
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.RestPolicy.cs
    type: A
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.Schedule.cs
    type: M
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.cs
    type: M
  - path: Source/PersonalChronicle/Automation/JobDriver_WorkshopDuty.cs
    type: M
  - path: Source/PersonalChronicle/Automation/Zone_Workshop.cs
    type: M
  - path: Source/PersonalChronicle/Automation/WorkshopCrew.cs
    type: M
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Schedule.cs
    type: M
  - path: Defs/WorkshopRestPolicies.xml
    type: A
  - path: Tests/WorkshopRestPolicyTests.cs
    type: A
  - path: Tests/WorkshopDutyRulesTests.cs
    type: A
  - path: Tests/WorkshopRosterRulesTests.cs
    type: A
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
  - path: Languages/English/Keyed/Automation.xml
    type: M
---

# 变更标注 · CHG-20261003-WORKSHOP-SPLIT

- **Change ID:** CHG-20261003-WORKSHOP-SPLIT
- **Version:** v1.1.7
- **Date:** 2026-10-03
- **Scope(摘要):** 车间名册/班次/值勤三模块拆分（Domain 纯函数化）+ 修两个实机确认缺陷

## 用户已确认的事实与裁决

| 项 | 结论 |
|---|---|
| 成因 A | **存在**：`SyncVanillaTimetables` 按「班组当日轮到的班次」改写作息表 ⇒ 日与日之间 Work 小时翻转约 12 小时 |
| 成因 B | **存在**：`EnsureDutyPlan` 按小时重建计划 ⇒ 跨小时时 `FailOn(!IsScheduledForDuty)` 打断值勤 Job |
| 修法 | 两个都修；按推荐方案（作息策略枚举 + 最小连续时长）推进 |
| 并发约束 | **不触碰 `Manufacturing/**`**（另一会话 `CHG-MFG-ALIGN-001`）—— 已核实最近写入 14:14:37，全程未改 |

## 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 关联规则 | 关联 Gate |
|---|---|---|---|---|
| `Domain/Workshop/WorkshopRosterRules.cs` | A | 名册模块：`ResolveFixedShiftId` / `DedupeMemberships` / `ResolveCandidatePool` | GOV-004 / ARC-005 | ARCH / TEST |
| `Domain/Workshop/WorkshopShiftPolicy.cs` | A | 班次模块：`WorkshopRestPolicyKind` / `WritesTimetable` / `FlipsSleepAcrossDays` | GOV-006 / GOV-009 | ARCH / DATA / TEST |
| `Domain/Workshop/WorkshopDutyRules.cs` | A | 值勤模块：`ShouldEndDuty` / `HoursSince` / `MarkWorkHours` | ARC-005 | ARCH / TEST |
| `Automation/WorkshopRestPolicyDef.cs` | A | `WorkshopRestPolicyDef`（Def 驱动策略档位） | GOV-006 / GOV-009 | DATA |
| `Automation/WorkshopDutyCoordinator.cs` | A | **唯一 timetable 写入口**；`PolicyOf` / `SyncVanillaTimetables` | MATRIX-003 / EXC-2026-013 | ARCH / DATA |
| `Automation/WorkshopManageView.RestPolicy.cs` | A | 策略选择 UI（只发命令 + RotateDaily 强制警示） | MATRIX-003 / GOV-010 | UI / LOC |
| `Automation/MapComponent_WorkshopManager.Schedule.cs` | M | `SyncVanillaTimetables` 改薄转发；新增 `ShouldEndDutyNow` 桥接 | GOV-004 | ARCH |
| `Automation/MapComponent_WorkshopManager.cs` | M | 新增 `dutyShiftOf` / `DutyCoordinator` | — | ARCH |
| `Automation/JobDriver_WorkshopDuty.cs` | M | **`FailOn` 收窄为硬边界 + `tickIntervalAction` 主动收工** | 见下方 ADR | ARCH / PERF |
| `Automation/Zone_Workshop.cs` | M | 新增 `restPolicyDefName`（加性存档字段） | SAVE-002 | SAVE |
| `Automation/WorkshopCrew.cs` | M | 新增 `FixedShiftId`（加性存档字段） | SAVE-002 | SAVE |
| `Automation/WorkshopManageView.Schedule.cs` | M | 排班页接入策略行 | UI | UI |
| `Defs/WorkshopRestPolicies.xml` | A | 3 个 `WorkshopRestPolicyDef` | DATA-004 | DATA |
| `Tests/*` ×3 | A | 22 条新单测（策略 / 收工 / 绑定） | — | TEST |
| `Languages/*/Keyed/Automation.xml` | M | 各 +9 键（中英一一对应） | GOV-010 | LOC |

## 关键 ADR：`FailOn` → `tickIntervalAction`

**任务书假设有误，已按取证修正。** 任务书 §3.3 要求"在 `JobDriver.tick()` 里主动 `EndJobWith`"，
但 **Cecil 取证（1.6 实测）证明该 API 不存在**：

| 取证项 | 结果 |
|---|---|
| `Verse.AI.JobDriver.tick()` | ❌ **不存在** |
| `JobDriver.DriverTick()` | `public` 但**非 virtual** ⇒ 无法 override |
| `Toil.tickAction` / `tickIntervalAction` | ✅ `public Action` / `Action<int>`，`DriverTick` 每 tick `Invoke` |
| `JobDriver.EndJobWith(JobCondition)` | ✅ `public` |
| vanilla 先例 | ✅ `JobDriver_WatchTelevision.WatchTickAction` 正是 tickIntervalAction 内调 `EndJobWith` |

**裁决**：改用 `Toil.tickIntervalAction(60)` + `EndJobWith(JobCondition.Succeeded)`，
与原版同构。间隔取 60 tick（1 游戏秒）—— 判定结果在同一小时内恒定，每 tick 求值 60 倍冗余。

**保留 `FailOn` 的部分**：`Manager == null`、`pawn.Downed` —— 这些是**硬边界**，
必须立即中断，不存在"再等等"的语义。

## 验收结果（GATE-DELTA）

| # | 项 | 结果 | Evidence |
|---|---|---|---|
| 1 | 编译 0 错 0 警 | ✅ PASS | `compile_with_sdk.ps1` 无 error/warning 输出 |
| 2 | 单测 0 失败 | ✅ PASS | **499 通过 / 1 跳过 / 0 失败**（基线 477 ⇒ +22） |
| 3 | 中英键一一对应 | ✅ PASS | EN 443 = CN 443，`Compare-Object` 差异 = 0 |
| 4 | Def 语义级校验 | ✅ PASS | 3 条 Def；`field/li` 均为 0（未用 `List<string>`）；全 Defs 良构失败数 = 0；3 个枚举值对真实程序集可解析 |
| 5 | 变更标注 | ✅ PASS | 本文件（front matter `- path:` / `type:`） |
| 6 | Domain 零 Verse 依赖 | ✅ PASS | 剥离注释后三模块代码级引擎引用 = **0** |
| 7 | 不触碰 Manufacturing | ✅ PASS | 最近写入仍为 `14:14:37 FloorDeviceDetailWindow.Draw.cs` |

## 未实机验证部分（如实标注 · GOV-012）

| 项 | 状态 | 原因 |
|---|---|---|
| 睡眠时段不再按天翻转 | **未执行** | 需实机跑 ≥2 游戏日并逐日核对作息表 |
| 值勤不再边界反复中断 | **未执行** | 需实机观察跨小时值勤是否连续 |
| RotateDaily 警示 UI 实际呈现 | **未执行** | 需实机开排班页确认中英文本不溢出 |
| 旧存档迁移表现 | **未执行** | 需实机载入旧档确认作息被重写为绑定班次 |
| PERF（tick 开销） | **部分执行** | 已按 60 tick 间隔限流；无实机 FPS 读数 |

**结论：编译/单测/静态 Gate 全部 PASS；两个缺陷的实机行为确认标记为「未执行」，
不伪造 PASS（GOV-012）。**