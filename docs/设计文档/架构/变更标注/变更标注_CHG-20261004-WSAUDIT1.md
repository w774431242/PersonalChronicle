---
change_id: CHG-20261004-WSAUDIT1
version: v1.1.5
date: 2026-10-04
author: AI
scope: [Source/PersonalChronicle]
files:
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.Schedule.cs
    type: M
    symbols: MapComponent_WorkshopManager.ResolveShiftPool, MapComponent_WorkshopManager.RebuildDutyPlan
    lines: 204-207, 500-517
  - path: Source/PersonalChronicle/Automation/WorkshopDutyCoordinator.cs
    type: M
    symbols: WorkshopDutyCoordinator.managerShiftCount
    lines: 470-490
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.cs
    type: M
    symbols: MapComponent_WorkshopManager (removed field dutyPlanBuiltTick)
    lines: 52-57
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.OffShift.cs
    type: M
    symbols: MapComponent_WorkshopManager.HourInShift
    lines: 34-48
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.WorkTakeover.cs
    type: M
    symbols: MapComponent_WorkshopManager.TryTakeOverVanillaWork (scope 构建)
    lines: 170-181
---

# 变更标注 · CHG-20261004-WSAUDIT1

## 背景

车间体系**深度并行审计**（4 维：ARCH-耦合 / UI-LOC / PERF / DATA-SAVE，覆盖 100 个 .cs、26,127 行，
只读完成，未改动任何文件）。本标注登记**批次 2（真缺陷）+ 架构域零风险清理**；
批次 1 的数据域 / UI 域 / 性能域另行提交（每域独立编译验证，遵守"不破坏"原则）。

## 缺陷修复（批次 2）

### X1 `ResolveShiftPool` 别名泄漏 —— 「殖民者莫名被添加到其他班组」的根因

`MapComponent_WorkshopManager.Schedule.cs`（`ResolveShiftPool`）

- **问题**：轮转候选池 `basePool` 直接引用了 `crews[i].Members` / `zone.Members` / `shift.Pool`
  这些**持久化集合的活引用**，而方法末尾会把"显式指派班次"的成员 `Add` 进去
  ⇒ **查询函数改写了存档名册**。表现为玩家报过三次的「殖民者莫名被编入其他班组」，
  且 `DedupeCrewMemberships` 只能治标（下一次排班再追加一次）。
  这也解释了为何"找遍自动编组入口都找不到"——它不是一个入口，而是这里的副作用。
- **修法**：`return basePool != null ? new List<Pawn>(basePool) : null;`
  —— 返回副本，任何调用方（含 `public ResolvePoolFor`）都无法再写回名册。
- **调用点核对**：`RebuildDutyPlan:259` 只读遍历（`pool.Count`/`pool[i]`）、`ResolvePoolFor`
  供 UI 计数 ⇒ 返回副本不影响既有语义，`null` 语义不变。

### X2 `managerShiftCount` 条件写反

`WorkshopDutyCoordinator.cs`（`managerShiftCount`）

- **问题**：语义是"**本车间**参与倒班的班次序位数量"，条件却写成 `shift.TargetZone == zone ⇒ continue`
  ⇒ 排除本车间、只统计其它车间 ⇒ `shiftCount` 取到 0 或他车间值
  ⇒ 调用点（`:432` → `WorkshopCrewCycle.CrewOfShiftSlot`）轮转基数错误
  ⇒ 班组被轮到错误的班次（"人莫名其妙值错班"的同源问题）。
- **修法**：`== zone` → `!= zone`（附注释说明）。
- ⚠ 属**行为变更（修正）**：倒班轮转结果会与修复前不同，需按下方"实机验证要点"确认轮转正确。

## 零风险清理（架构域，批次 1 首批）

| 项 | 位置 | 改动 |
|---|---|---|
| 删死字段 | `MapComponent_WorkshopManager.cs:52-57`、`.Schedule.cs:204-207` | `dutyPlanBuiltTick` 只写不读，已删（失效判定由 `dutyPlanHour` / `dutyPlanDirty` / `CheckIntervalTicks` 兜底） |
| 去重复实现 | `.WorkTakeover.cs:170-181` | 作用域改为调用既有 `AllWorkshopMemberPawns(zone)`（原为第二份逐字复制；注释自称"共用"却未共用）。行为等价：同序并集、同去重、同 `Destroyed` 过滤 |
| 统一区间判定 | `.OffShift.cs:34-48` | `HourInShift` 转发 `Domain.WorkshopTime.InRange`（原为工程内第 3 份手工实现）。逐条核对语义一致：左闭右开、`start == end` 全天、`end < start` 跨午夜 |

## 存档影响

**无。** 五项均不改变 Scribe 键结构或写入形状：

- X1 修的是"本就不该写"的持久集合被写入（落盘结构不变，只是不再被污染）；
- 其余为字段删除 / 等价替换 / 死代码清理。
- 旧档可正常加载；**不需要 Migration，不需要 bump `schemaVersion`。**

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（`_scripts/compile_with_sdk.ps1`） |
| 部署 | 已验证 | Defs 31 / Languages 37 / Assemblies 1 同步 |
| 调用点核对 | 已验证 | `ResolveShiftPool` 两个消费点均只读；`seen` 在替换段后无引用 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261004-WSAUDIT1.md -ExcludeFromFile .codebuddy/tmp/wsaudit-out-of-scope.txt -Blocking` |
| 实机 | 未验证 | 见下 |

## 实机验证要点（重启后）

1. **班组名册不再膨胀**：给班组勾选班次指派 → 跨天 → 检查该班组人数是否**不再自己增长**；
   反复几次后 `车间管理 → 班组页` 人数应稳定。
2. **倒班轮转正确**：多班组 + 多班次（启用转班）观察连续几天的"当日值哪个班"是否符合
   `CrewOfShiftSlot` 的轮转规则（此前基数错误会表现为固定偏向某个班组/某班次空岗）。
3. **回归**：`Player.log` 不应出现新的 `PersonalChronicle` 警告；
   离班/在班的作息表写入应与此前一致（`HourInShift` 语义等价）。

## 回滚 / 风险

- X2 是行为修正：若实机发现轮转与预期不同，需回看做一次 `!=` → `==` 的对照实验（并保留本标注）。
- X1 / 三项清理均为等价或纯删除，回滚只需还原本标注所列 5 个文件。
