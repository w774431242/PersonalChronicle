---
change_id: CHG-20261005-AUTOASSIGN
version: v1.1.5
date: 2026-10-03
author: AI
scope: [Source/PersonalChronicle/Domain, Source/PersonalChronicle/Data, Source/PersonalChronicle/Automation, Source/PersonalChronicle/Archive/UI/WorkTab, Languages]
files:
  - path: Source/PersonalChronicle/Domain/AutoAssignPriority.cs
    type: A
    symbols: AutoAssignPriority.TryCompute / Clamp / IsProtected
    lines: 1-86
  - path: Source/PersonalChronicle/Domain/AutoAssignLedger.cs
    type: A
    symbols: AutoAssignLedger.Record / RecordOf / Sanitize
    lines: 1-140
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.AutoAssign.cs
    type: A
    symbols: ChronicleGameComponent.AutoAssign / AutoAssignLedger / ExposeAutoAssign
    lines: 1-41
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.cs
    type: M
    symbols: ChronicleGameComponent.ExposeData
    lines: 223-224
  - path: Source/PersonalChronicle/Automation/AutoAssignService.cs
    type: A
    symbols: AutoAssignService.Run / ManagedWorkTypeDefNames
    lines: 1-150
  - path: Source/PersonalChronicle/Archive/UI/WorkTab/WorkTabTakeoverPanel.cs
    type: M
    symbols: WorkTabTakeoverPanel.DrawToolbar / ConfirmAutoAssign / FreeColonistsOf
    lines: 41-142
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: Key:PersonalChronicle.WorkTab.Pc.AutoAssign / AutoAssignConfirm / AutoAssignDone / Execute
    lines: 3682-3686
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: Key:PersonalChronicle.WorkTab.Pc.AutoAssign / AutoAssignConfirm / AutoAssignDone / Execute
    lines: 3674-3678
---

# 变更标注 · CHG-20261005-AUTOASSIGN

按技能（+激情）自动分配工作优先级，手动派发；车间托管的工作类型一律跳过。
Build **1.6.4871 rev590** · 编译 **0 错 0 警**（EXIT=0）。

## 1. 强干预语义（本单元核心）

**会自动勾选原版工作类型**，且走的就是原版唯一入口
`Pawn_WorkSettings.SetPriority(WorkTypeDef,int)` —— 与玩家手点原版格子**完全等价**，
不另造任何 UI / 事件 / 状态。面板每格也是 `PawnColumnWorker.DoCell` 原样委托。

档位公式（全整数，可离线精确单测）：
```
skillAvgX10 = round(AverageOfRelevantSkillsFor(wt) * 10)      // 0..200
total = skillAvgX10*70/200 + (Passion.Major 30 / Minor 15 / None 0)
raw   = total>=70 ? 1 : total>=45 ? 2 : total>=20 ? 3 : 4
return max(raw, floor(wt))     // 保护名单 floor=2
```
映射表**不自建**：原版 `WorkTypeDef.relevantSkills` 即权威映射（Cecil 实证 19 处消费）。

## 2. 关键裁决

| 项 | 裁决 | 依据 |
|---|---|---|
| 班次协同 | **跳过**本地图被任何班次覆盖的工作类型（值勤 +各班次岗位映射类型） | `WorkEnable.EnableWorkTypes` 已在按班次写这些格位；自动分配再写就是第二次改写，玩家点一次排班变更就被静默回滚 |
| 无技能映射类型 | **跳过不写**（不是写 0） | 写 0 会新增第二个 `SetPriority(x,0)` 调用点，而 0 是 `EXC-2026-016` 接管哨兵值、其授权点全工程唯一 |
| 台账冲突 | 写入前 **M1 退让**：`DemotionLedger.Discard` + `DiscardTakeover` | 复用既有裁决「玩家已改动 ⇒ PC 不再拥有」，**零新授权面**；否则写 4 会被降档闸门（`current==4`）误判并静默还原 |
| 保护名单 | `MandatoryKeep` 4 项 + `Patient` + `PatientBedRest`，floor=2 | `MandatoryKeep` 闸门查 `current==0`，写 4 绕过它；压到 4 无任何闸门会报异常 |
| 派发入口 | 接管面板工具条「自动分配」+ **二次确认框** | 批量改写玩家核心数据必须留退路；确认框明写三条副作用 |
| 作用域 | 当前地图自由民 | 跨地图需额外过滤链，本轮不做 |
| 档位越界 | `AutoAssignPriority.Clamp` | IL 实证 `SetPriority` 越界只 Log.Message 后照样写 |

## 3. 存档

新增**加性可选**字段 `workshopWorkAutoAssign`（`AutoAssignLedger`：pawnId → workTypeDefName /
originalPriority / wasDisabled / assignedPriority）。**不 bump SchemaVersion**；
旧档读回 null ⇒ 空台账 ⇒ 无行为变化。`wasDisabled` 用于将来撤销时能恢复成「未勾选」。
有界：`MaxPawnRecords=256` / `MaxEntriesPerPawn=512`；Saving 阶段复用
`WorkshopWorkDemotionLedger.SanitizeKey` 清洗键（2026-10-02 整档报废事故同源防护）。

## 4. Gate Results

| Gate | Result | Evidence |
|---|---|---|
| ARCH | **待判** | 分层/依赖已自证；判定须由 `pc-arch-gatekeeper` 出（总控 `OWNED_GATES: NONE`） |
| DATA | PASS | 零 Def 新增 |
| SAVE | PASS（静态） | 加性可选 + 不 bump 版本 + 键清洗 + 有界；**实机存读档未执行** |
| UI | PASS（静态） | 确认框后写、命中顺序、可见行裁剪；**实机布局未执行** |
| LOC | PASS | 新增 4 键，中英对等，占位符 `{0}~{2}` 一致 |
| COMP | **待判** | 须由 `pc-compat-auditor` 出 |
| PERF | **未执行** | 有静态复杂度论证，**无实机采样** |
| TEST | 部分 PASS | 0 错 0 警；**Vanilla 启动、实机派发、存读档未执行**（无游戏进程，无伪造 PASS） |
| REL | **未执行** | 版本 / Changelog 待定稿 |
| DELTA | 见 §6 | 由 `gate-runner.ps1` 机械校验 |

## 5. Remaining Risks

1. **P0** 写 4 与降档台账值域碰撞 ⇒ 已用 M1 退让规避（台账无记录 ⇒ 闸门遍历不到）。
2. **P0** 档位越界写脏档位 ⇒ `Clamp`。
3. **P1** 强干预会覆盖玩家手动取消 —— 已在确认框明写；**撤销功能本轮未做**（台账已具备还原所需字段）。
4. **P1** 「按班次」当前是**地图级保守跳过**，非 per-pawn 精确判定（班组→班次解析需 WorkshopCrew 公开契约）。
5. **P2** 班次覆盖项的并集随排班变化 ⇒ 同一格位可能一次被改、下次被跳过。

## 6. 附

前序单元 `WORKTAB-HIDE`(8) / `WORKTAB-SHIFT`(4) / `WORKTAB-TAKEOVER`(7) 均已单独通过 GATE-DELTA，
其文件在本单元 `-ExcludeFromFile` 中按「已交付单元」排除。