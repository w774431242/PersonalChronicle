---
change_id: CHG-20261005-OFFSHIFT
version: v1.1.5
date: 2026-10-03
author: AI
scope: [Source/PersonalChronicle/Automation]
files:
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.OffShift.cs
    type: A
    symbols: MapComponent_WorkshopManager.ApplyOffShiftWorkTypes / RunOffShiftPass / DisableForCrew / HourInShift
    lines: 1-131
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.cs
    type: M
    symbols: MapComponent_WorkshopManager.MapComponentTick
    lines: 261-262
---

# 变更标注 · CHG-20261005-OFFSHIFT

离班自动取消班组工作类型。Build **1.6.4871 rev590** · 编译 **0 错 0 警**（EXIT=0）。

## 规则

当前游戏时间**不在**某班组**任何一个**已指派班次的时间段内 ⇒ 该班组**成员**在这些班次
覆盖的工作类型（岗位映射类型 + 值勤 `PC_WorkshopOperation`）上被**取消勾选（档位 0）**。
在班时本功能不做任何事 —— 由既有 `SyncVanillaWorkSettings` / `EnableWorkTypes`
写值勤 3 / 映射类型 1（互补而非重复）。

时间窗判定支持跨午夜（`StartHour > EndHour`，如 20:00→08:00）；起止相同 = 全天在班。

## 关键裁决

| 项 | 裁决 | 依据 |
|---|---|---|
| 触发 | 挂在既有 60-tick `MapComponentTick`内，**用游戏小时变更做内层门控** | 每游戏小时最多跑一次，不是每 tick；复用既有循环，零新增调度 |
| 门控字段 | `lastOffShiftHour` 存**实例字段**而非 static | 避免跨地图 / 跨存档串味 |
| **0 的写入** | **EXC-2026-025(E2)**：全工程第二个 `SetPriority(x,0)` 调用点 | 0 是 `EXC-2026-016` 接管哨兵值，其单点授权前提是「PC 写 0 只发生在接管」 |
| 不误判 | 写 0 前 **M1 退让**：`DemotionLedger.Discard` + `DiscardTakeover` | 接管闸门是「当前==0 才还原」；若写 0 后台账仍有记录，玩家点「解除接管」会把档位还原 ⇒ **离班期间被重新启用**（错的）。退让后台账无记录 ⇒ 闸门遍历不到 |
| 幂等 | 已是 0 的格位跳过 | 避免每 tick 重复写 |
| 边界 | 不改 `Pawn_WorkSettings` 之外的任何状态；不碰降档/接管台账的写入路径 | 台账只被Discard，不新增写入 |

## Gate Results

| Gate | Result | Evidence |
|---|---|---|
| ARCH | **待判** | 判定须由 `pc-arch-gatekeeper` 出（总控 `OWNED_GATES: NONE`）；本单元新增第二个 `SetPriority(x,0)` 授权面，属架构问题 |
| DATA | PASS | 零 Def 新增 |
| SAVE | **N/A** | 零存档字段、零 SchemaVersion（只调用既有 Discard） |
| UI | N/A | 无UI 改动 |
| LOC | N/A | 无用户可见文本 |
| COMP | **待判** | 第二个 0 写入点须由 `pc-compat-auditor` 复核与 EXC-2026-016 的叠加 |
| PERF | PASS（静态） | 每游戏小时 1 次；每次遍历车间数×班组数×成员数，且「已是 0 跳过」 |
| TEST | 部分 PASS | 0 错 0 警；**跨午夜时间窗、离班→在班恢复的实机行为未执行**（无游戏进程，无伪造 PASS） |
| REL | **未执行** | 版本 / Changelog 待定稿 |
| DELTA | 见 §5 | 由 `gate-runner.ps1` 机械校验 |

## Remaining Risks

1. **P0** 写 0 与接管台账误判 ⇒ 已用 M1 退让，完备性依赖「Discard 确实移除记录」—— 需 `pc-data-steward` 复核。
2. **P1** 离班期间玩家的手动改动会被下一小时的 pass 再次取消（收敛链必然，非 bug，但需文案说明）。
3. **P1** 恢复路径依赖既有 `EnableWorkTypes`；若班组在离班期间被删/改班次，格位可能停在 0。
4. **P2** `lastOffShiftHour` 初始 -1 ⇒ 读档后首个 tick 立即跑一次（预期行为）。

## 附

前序单元 `WORKTAB-HIDE`(8) / `WORKTAB-SHIFT`(4) / `WORKTAB-TAKEOVER`(7) / `AUTOASSIGN`(8)
均已单独通过 GATE-DELTA，其文件在本单元 `-ExcludeFromFile` 中按「已交付单元」排除。