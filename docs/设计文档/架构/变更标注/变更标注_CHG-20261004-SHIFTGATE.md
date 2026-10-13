---
change_id: CHG-20261004-SHIFTGATE
version: v1.1.5
date: 2026-10-04
author: AI
scope: [Source/PersonalChronicle, Languages]
files:
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.WorkEnable.cs
    type: M
    symbols: MapComponent_WorkshopManager.ShouldSkipAutoEnableForShift, MapComponent_WorkshopManager.SyncVanillaWorkSettings
    lines: 101-152
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.OffShift.cs
    type: M
    symbols: MapComponent_WorkshopManager.ApplyOffShiftWorkTypes
    lines: 48-52
  - path: Source/PersonalChronicle/ChronicleSettings.cs
    type: M
    symbols: ChronicleSettings.EnableOffShiftCancel
    lines: 42-55, 86-89, 108-109
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: Key:PersonalChronicle.Settings.EnableOffShiftCancel
    lines: 3685-3686
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: Key:PersonalChronicle.Settings.EnableOffShiftCancel
    lines: 3678-3679
---

# 变更标注 · CHG-20261004-SHIFTGATE

岗位手动 + 离班停工 + 0/3 互跳根除。Build **1.6.4871 rev590** · 编译 **0 错 0 警**。

## 产品口径（2026-10-04 玩家决策）

> **玩家只分配岗位，系统自动分配工作类型，且离班要停工。**

三条约束同时成立，必须靠**分离「提权」与「清零」两条链的触发条件**才能同时满足。

## 根因（CHG-20261005-PERFDIAG 记录的 P0，本单元根治）

| 链 | 触发 | 对离班者做什么 |
|---|---|---|
| `EnableWorkTypes` |面板 `Refresh` / `OnRosterChanged`（`WorkEnable.cs:76`） | **写 值勤 3 / 映射 1**，且**不区分是否在班** |
| `DisableForCrew` | `ApplyOffShiftWorkTypes`，tick 驱动但按**游戏小时**去重（`OffShift.cs:45-59`） | **写 0** |

玩家开面板 → 写 3；到整点 → 写 0；再开面板 → 写 3……**0/3 无限互跳**。
根因不是「两条链都存在」，而是**提权链缺少班次门控**。

## 变更内容

1. **新增班次门控** `ShouldSkipAutoEnableForShift(Pawn, Zone_Workshop)`（`WorkEnable.cs:115-152`），
   在 `SyncVanillaWorkSettings` 的提权循环前拦截（`:101-108`）。
2. 门控判据**严格镜像** `RunOffShiftPass` / `DisableForCrew` 的范围：
   已编入本 zone 班组 + 班组已排班 + 当前无任一班次在班 ⇒ 跳过提权。
3. `EnableOffShiftCancel` 保持 **默认 true**（离班停工），文案改为反映真实语义。

### 关键取舍：班次候选池不受门控

`workSyncScope` 有三个来源（`WorkEnable.cs:110-148`）：`crew.Members`、未建班组时的 `zone.Members`、
`shift.Pool`。其中 **`shift.Pool` 里但未编入班组的人不受门控**——离班链只遍历 `crew.Members`，
根本不碰他们；若门控一并关掉，就形成「没人写 0、也没有档位可写」的黑洞，岗位无故失效。

### 口径分叉会致命

两条链的判据若不一致，会出现：
- 门控认为**在班**（不写）而离班链认为**离班**（写 0）⇒ 岗位无故失效
- 反向 ⇒ 等于互跳没修

故门控**必须**镜像离班链，而非各判各的。

## 变更后行为矩阵

| pawn 状态 | 面板刷新（提权链） | 整点（离班链） | 净结果 |
|---|---|---|---|
| 在班 | 写 3/1 | 跳过（`onDuty`） | **稳定 3/1** ✅ |
| 离班（开关开） | **门控跳过** | 写 0 | **稳定 0** ✅ 停工 |
| 离班（开关关） | **门控跳过** | 不写 | **稳定在上班时的值** ✅ 不停工 |
| 未编班组 / 班组未排班 | 写 3/1 | 不碰 | 稳定 3/1 ✅ |

## 降档通道未受影响（已核）

`SyncWorkStationForZone` → `DemoteOtherWorkTypes` 会跳过 `WorkTypeIsDisabled` 的类型
（`WorkDemote.cs:25-28` 守卫），故离班者被写 0 的格位**不会被降档通道写回非 0**。

## Gate Results

| Gate | Result | Evidence |
|---|---|---|
| ARCH | **待判** | 新增一个 private 方法 + 一处调用点拦截，无新增层/依赖边/公开签名变更；**最终判定须由 `pc-arch-gatekeeper` 出**，本单元不自判 |
| DATA | **N/A** | 零 Def 新增 |
| SAVE | PASS（静态） | Scribe 键 `enableOffShiftCancel` 不变、schema 未 bump；默认值双处一致（`ChronicleSettings.cs:55` / `:89`） |
| UI | PASS（静态） | 勾选框位置/数量不变，仅文案语义改写 |
| LOC | PASS | 中英各 1 键对等，无占位符 |
| COMP | **N/A** | 零 Harmony、零第三方 Mod 接入 |
| PERF | PASS（改善） | 每整小时点的 `RunOffShiftPass` 因幂等守卫（`GetPriority(wt) <= 0 → continue`）不重复写；**本单元消除了面板刷新与整点两条链的重复写入** |
| TEST | 部分 PASS | 0 错 0 警；**实机未执行**（沙箱无游戏进程），行为矩阵**未经游戏内验证** |
| REL | **未执行** | 版本 / Changelog 待定稿 |
| DELTA | 见 §6 | 由 `gate-runner.ps1` 机械校验 |

## Remaining Risks

1. **P1** 门控依赖 `zone.Crews` 与 `crew.AssignedShifts` 的**同一性**。若玩家在成员已编组、
   但班次为空的状态下期望「照旧提权」，本实现按「未排班 ⇒ 照旧提权」处理（`WorkEnable.cs:136-139`），
   口径与离班链一致（离班链同样跳过未排班班组），**但这是设计选择而非玩家明示要求**。
2. **P1** 跨午夜班次（如 20:00→08:00）依赖 `HourInShift` 的 `s >= e` 分支，
   该分支为既有代码，**本单元未新增测试覆盖**。
3. **P2** 玩家若在 `ModSettings.xml` 中存有 `enableOffShiftCancel = false`（上一轮中间态可能写入），
   读档时以存档值为准 ⇒ 离班不会停工。需手动勾回。
4. **P2** 帧率真因**仍未定位**：本单元消除了互跳这一处已证实的重复写入，
   但「帧率问题是否由此完全解决」**未证实**，需实机 A/B。

## 附：取代关系

- 取代 `CHG-20261004-MANUALPOST`（中间态：曾把默认值改为 false，现已改回 true + 加门控）。
- 部分取代 `CHG-20261005-PERFDIAG` 的 P0 互写项：根因已修，嫌疑分析其余部分继续有效。
