---
change_id: CHG-20261005-AUTOASSIGN-ONLY1
version: v1.1.5
date: 2026-10-03
author: AI
scope: [Source/PersonalChronicle/Automation, Source/PersonalChronicle/Archive/UI/WorkTab, Languages]
files:
  - path: Source/PersonalChronicle/Automation/AutoAssignService.cs
    type: M
    symbols: AutoAssignService.Run / WriteCell / Result.Disabled
    lines: 39-160
  - path: Source/PersonalChronicle/Archive/UI/WorkTab/WorkTabTakeoverPanel.cs
    type: M
    symbols: WorkTabTakeoverPanel.ConfirmAutoAssign
    lines: 128-131
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: Key:PersonalChronicle.WorkTab.Pc.AutoAssignConfirm / AutoAssignDone
    lines: 3683-3684
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: Key:PersonalChronicle.WorkTab.Pc.AutoAssignConfirm / AutoAssignDone
    lines: 3675-3676
---

# 变更标注 · CHG-20261005-AUTOASSIGN-ONLY1

自动分配改为「只保留最优 1 项工作类型」。Build **1.6.4871 rev590** · 编译 **0 错 0 警**（EXIT=0）。

## 规则变更

原规则：所有映射到技能的工作类型都写入算得档位（1~4）。
**新规则：每人只保留技能最契合的那 1 项，其余全部取消勾选（档位 0）。**

目标：离班时（车间类被 OFFSHIFT pass 取消）该殖民者**没有任何零碎活可做** ⇒ 直接去睡觉。

## 关键裁决

| 项 | 裁决 | 依据 |
|---|---|---|
| 两遍扫描 | 第一遍只算档位找最优项；第二遍才写入 | 单遍无法在写入前知道"最优是谁"；两遍避免把最优项自己取消掉 |
| **保护名单永不取消** | `Hauling/Cleaning/Doctor/Firefighter/Patient/PatientBedRest` 不参与"只留1 项" | 这些是生存底线（搬食物 / 清污 / 看病 / 灭火），与"只做一件事"无关；若一并取消，殖民地会因没人搬食物而崩 |
| 车间托管项 | 仍然完全跳过 | 归车间域权威；离班时的取消由 `MapComponent_WorkshopManager.OffShift` 负责，避免两个写入方打架 |
| 无技能映射类型 | 仍然跳过、不动 | S3 保护（见 `AutoAssignPriority`） |
| **开始写 0** | 单格写入统一走 `WriteCell` 唯一出口，写 0 前必做 M1 退让 | 0 是 `EXC-2026-016` 接管哨兵值；不退让则玩家点「解除接管」会把档位还原 ⇒ 离班期间被重新启用 |
| 幂等 | `current <= 0` 跳过 | 每 tick/每次派发不重复写 |
| 提示 | 确认框明写「只保留 1 项」+「保护项永不取消」+「下班后直接去睡觉」 | 批量改写玩家核心数据，必须知情 |

## Gate Results

| Gate | Result | Evidence |
|---|---|---|
| ARCH | **待判** | 自动分配现**也写 0** ⇒ 离班 pass 之外出现第二类 0 写入场景，须由 `pc-arch-gatekeeper` 出 |
| DATA | PASS | 零 Def 新增 |
| SAVE | PASS（静态） | 零新字段；台账 `Record(assignedPriority=0)` 已支持 0 值；不 bump SchemaVersion |
| UI | PASS（静态） | 确认框 4 条副作用 +结果 4 计数；**实机未执行** |
| LOC | PASS | 2 键改写，中英对等，占位符 `{0}~{3}` 一致；**去掉 `<b>` 标签**（Keyed 不保证解析富文本，改用【】） |
| COMP | **待判** | 与 OFFSHIFT 的 0 写入叠加关系须由 `pc-compat-auditor` 复核 |
| PERF | PASS（静态） | 每人两遍 O(工作类型数)；列表规模由 Def 数决定，有界 |
| TEST | 部分 PASS | 0 错 0 警；**"下班后直接睡觉"的实机行为未执行**（无游戏进程，无伪造 PASS） |
| REL | **未执行** | 版本 / Changelog 待定稿 |
| DELTA | 见 §5 | 由 `gate-runner.ps1` 机械校验 |

## Remaining Risks

1. **P0** 写 0 与接管台账误判 ⇒ `WriteCell` 内强制 M1 退让。
2. **P1** 每人只剩 1 项 ⇒  colonists 无法响应临时需求（如需要有人采矿时只有该殖民者的挖掘被勾）。
3. **P1** 派发后该殖民者对**其他**工作完全无响应，若最优项当前无可做之事 ⇒ 会闲置（这是本规则的预期结果，但需玩家知情）。
4. **P2** 保护名单若被玩家手动取消，规则不会恢复它们（只保护、不强推）。

## 附

前序单元 `WORKTAB-HIDE`(8) / `WORKTAB-SHIFT`(4) / `WORKTAB-TAKEOVER`(7) / `AUTOASSIGN`(8) /
`OFFSHIFT`(2) 均已单独通过 GATE-DELTA，其文件在本单元 `-ExcludeFromFile` 中排除。