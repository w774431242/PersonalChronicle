---
change_id: CHG-20261005-WSDOCFIX
version: v1.1.5
date: 2026-10-05
author: AI
kind: P1/P2 打磨修复（注释/死码）+ 清单误报复核
scope: [Source/PersonalChronicle, docs]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: DrawDevices（列数注释 10→8）· 类头注释（去 ITab cref / 4 页→7 页）
    lines: 14-23, 730-737
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Overview.cs
    type: M
    symbols: DrawTrendSection（删）· ovDailyTotal（删）· ovDailyValues（删）· ovDailyLabels（保留，瓶颈趋势图在用）· DrawOverviewBody 注释
    lines: 107-109, 212-231, 550-556, 689-700
  - path: Source/PersonalChronicle/Automation/Dialog_WorkshopShiftEditor.cs
    type: M
    symbols: 类头注释（名额行已补回，与实现对齐）
    lines: 23-35
  - path: Source/PersonalChronicle/Automation/Dialog_WorkshopManage.cs
    type: M
    symbols: 类头注释（去 ITab cref）
    lines: 12-17
  - path: Source/PersonalChronicle/Automation/JobDriver_WorkshopDuty.cs
    type: M
    symbols: CheckDutyEnd doc（断句重复与未闭合括号）
    lines: 105-106
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.Schedule.cs
    type: M
    symbols: ShiftTicksFor doc 归位（原顶部顶 Zone_Workshop.PruneScheduleHistory 的注释）
    lines: 1044-1047, 1115-1126
  - path: Source/PersonalChronicle/Effects/WorkshopStatPartInjector.cs
    type: M
    symbols: Enabled 字段注释（当前为启用，非"已移除"）
    lines: 36-47
  - path: docs/设计文档/功能模块/车间体系/车间体系_完工功能清单_v2_2026-10-05.md
    type: M
    symbols: §6 增加复核修订记录（4 条误报更正）
---

# 变更标注 · CHG-20261005-WSDOCFIX

> 玩家 2026-10-05：「**6-P0忽略，P1P2 按你建议修复**」

## 一、本批实际动了什么（6 条）

| 条目 | 改动 | 性质 |
|---|---|---|
| **P1-07** | `WorkshopManageView.DrawDevices`：列数注释 `10 列（含效率/位置）` → **8 列**（与 `DeviceHeaders` 实测一致），并列明列数演化（11→10→8） | 注释漂移 |
| **P1-08** | `WorkshopStatPartInjector.Enabled`：注释原文写"2026-09-25 加成体系移除"（读起来像已停用），**实际为启用** ⇒ 改为说明当前状态并附事实依据（`WorkshopTypes.xml` 9 个类型都填了加成实值；置 false 会让配置静默失效） | 注释漂移 |
| **P1-09** | `Dialog_WorkshopShiftEditor` 类头：原文"名额 UI **不恢复**…当前无 UI 入口"与实现（②bis 行已补回、缺省显示实际生效值 1）**矛盾** ⇒ 以实现为准改写 | 注释与实现矛盾 |
| **P2-04** | 去掉 2 处**不存在的 cref** `ITab_WorkshopManage`（全工程无定义）；并把"概览/设备/配方/设置"（旧 4 页）改为 **7 页** | 残留引用 + 漂移 |
| **P2-09** | `JobDriver_WorkshopDuty`：重复断句 + 括号未闭合；`Schedule.cs`：`ShiftTicksFor` 的 doc 漂在文件中部（其位置被 `Zone_Workshop.PruneScheduleHistory` 的注释占据，两者无关）⇒ **两处注释各自归位** | 编辑残留 |
| **P2-02** | 概览趋势盘死码清理：删 `DrawTrendSection` + `ovDailyTotal` + `ovDailyValues` + 其填充循环；**`ovDailyProduced` 保留**（大组件区候选 3 在用）、**`ovDailyLabels` 保留**（瓶颈趋势图在用） | 死码清理 |

## 二、⚠ 复核发现 4 条**误报**（清单初版的问题，已在 §6 更正）

| 原登记 | 真相 | 依据 |
|---|---|---|
| P1-02 成员残留字典（有界性漏洞） | **早已修复（2026-10-02）** | `Manager.cs` 清 `activeWorkers` 时**同步**清 `dutyStartTicks`/`bonusCache`；我读到的那行"注释"就是该次修复的说明 |
| P1-03 删班次后班组悬空 ID | **早已修复（2026-10-02）** | `RemoveShift` 已走 `Crew.UnassignShift`（同时清引用与持久 ID） |
| P2-01 停用模块无说明 | **已有明确留档** | 4 个文件均写明停用原因 + 保留理由（源码目录未纳入版本控制） |
| P2-03 `DualIntStep` 残留 | **已决策保留** | 注释写明"删枚举会牵动目录/校验两处，收益为零" |

**根因**：清单初版由并行扫描生成，扫描把**"解释修复的注释"读成了"现存漏洞"**。
⇒ 已在 §6 顶部加入"复核修订记录"，并把该教训写成条目（**⚠ 标记必须打开代码确认时态，不能只看注释**）。

## 三、未动项（按类型说明，非遗漏）

| 类型 | 条目 | 为何不动 |
|---|---|---|
| 约定类（设计即如此） | P1-04（池必须返副本）· P2-07（字段保留兼容旧档）· P2-08（每日重放旧档） | 现状正确，只在注释固定约定 |
| 功能开发 | P1-01（班组↔班次映射层）· P2-05（休息态/线尾控制台/区域图标） | 属新功能，非打磨 |
| 需换依赖/改存档 | P1-05（RUIF 版本同步）· P1-06（成品加成写进 Thing） | 风险面大，需你确认 |
| 待裁决 | P2-06（编排画布是否入档） | 产品决策 |
| 按你指令 | P0-01 / P0-02（接管空转 + 无生产 Job） | 你已明确"P0 忽略" |

## 四、验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | ✅ 已验证 | **0 错 0 警**（首轮即兜住一次漏引用：`ovDailyLabels` 被瓶颈图消费，已回滚删除） |
| 部署 | ✅ 已验证 | Defs 31 / Languages 39 / Assemblies 1 同步 |
| GATE-DELTA | 见下 | — |
| 实机 | ⏳ 未验证 | 本批无行为改变，仅注释/死码；预期与之前完全一致（**唯一可观察点：概览页不再有"产出趋势"区块** —— 但该区块在玩家移除后本就未绘制，故视觉应无变化） |

## 五、残留

1. 清单 §6 的 P1-01 / P1-05 / P1-06 / P2-05 / P2-06 仍待你裁决；
2. 趋势盘的 5 个语言键（`Trend*`）保留未删 —— 若恢复趋势盘可直接复用；若要彻底清理，需同时动中英语言文件（本批未做，避免扩大改动面）；
3. 本批**未触碰** `Defs/`、存档字段、Scribe 键 ⇒ 无存档影响。
