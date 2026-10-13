---
change_id: CHG-20261005-PERFDIAG
version: v1.1.5
date: 2026-10-03
author: AI
scope: [Source/PersonalChronicle, Languages]
files:
  - path: Source/PersonalChronicle/ChronicleSettings.cs
    type: M
    symbols: ChronicleSettings.EnableOffShiftCancel
    lines: 44-52, 83, 103
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.OffShift.cs
    type: M
    symbols: MapComponent_WorkshopManager.ApplyOffShiftWorkTypes
    lines: 44-51
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: Key:PersonalChronicle.Settings.EnableOffShiftCancel
    lines: 3687-3688
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: Key:PersonalChronicle.Settings.EnableOffShiftCancel
    lines: 3679-3680
---

# 变更标注 · CHG-20261005-PERFDIAG

> ⚠ **P0 互写项已由 `CHG-20261004-SHIFTGATE` 根治**（2026-10-04）：新增班次门控
> `ShouldSkipAutoEnableForShift`，`EnableWorkTypes` 不再对离班者提权，0/3 互跳根除。
> 本开关**保持默认 true**（玩家口径：岗位手动指派 + 工作类型按班次自动分配 + 离班停工）。
> 本单元的其余嫌疑分析继续有效。

性能隔离诊断开关。Build **1.6.4871 rev590** · 编译 **0 错 0 警**（DLL 1508864 bytes @ 23:04:40）。

## 目的

玩家报告严重帧率下降。静态取证已**排除**两个原先怀疑点：
- `Game.GetComponent<T>()` 是 `IsInstanceOfType` 线性扫描，但全库继承 `GameComponent` 的类型**仅 6 个**（+PC 自己 1个）⇒ 成本可忽略。
- `Pawn_WorkSettings.Notify_UseWorkPrioritiesChanged()` IL 仅一句 `workGiversDirty = true` ⇒ 本身便宜（但会作废work-giver 缓存）。

**首要嫌疑**：离班取消（写 0）与车间 `EnableWorkTypes`（写 3/1）操作**同一批格位**，
而 `SyncVanillaWorkSettings` 有 4 处调用方（含 tick 驱动的 `SyncWorkStationIfDirty`）
⇒ 可能互写并反复作废全殖民地 work-giver 缓存。**这既是性能问题也是正确性问题**（档位在 0/3 间跳）。

## 变更内容

新增 `ChronicleSettings.EnableOffShiftCancel`（默认 true）+ 设置页勾选框 + 中英 Keyed。
`ApplyOffShiftWorkTypes` 读取该开关；关闭时**每 tick 直接 return**（并把 `lastOffShiftHour` 复位为 -1，
保证重新开启后下一 tick 立即补跑一次，不丢状态）。

## 为什么不直接修

若热点实际在接管面板的每帧路径（`RowHeight` 为 O(殖民者数 × 列数)/帧），
先改车间互写逻辑就是白改既有代码。**先用零风险开关把范围压到一条链路**。

## Gate Results

| Gate | Result | Evidence |
|---|---|---|
| ARCH | **待判** | 未新增层/依赖边；最终判定须由 `pc-arch-gatekeeper` 出 |
| DATA | PASS | 零 Def 新增 |
| SAVE | PASS（静态） | 新增 ModSettings 项（**非存档 schema**），不 bump SchemaVersion |
| UI | PASS（静态） | 设置页新增 1 个 CheckboxLabeled，沿用既有 listing 范式 |
| LOC | PASS | 中英各 1 键对等 |
| COMP | **待判** | 判定须由 `pc-compat-auditor` 出 |
| PERF | **部分** | 本单元**本身**是诊断工具，不是性能修复；真因**未定位**（无 Player.log / 无实机采样） |
| TEST | 部分 PASS | 0 错 0 警；**帧率对比未执行**（需 PM 实机） |
| REL | **未执行** | 版本 / Changelog 待定稿 |
| DELTA | 见 §5 | 由 `gate-runner.ps1` 机械校验 |

## Remaining Risks

1. **P0** 离班取消 ↔ 车间启用 的互写**尚未修**，仍在代码里（仅提供关闭开关）。
2. **P1** 关闭本开关会**丢失离班取消功能**（离班者仍有零碎活，不去睡）⇒ 仅用于定位，不建议长期关闭。
3. **P2** 沙箱无 Player.log / 无实机采样 ⇒ 热点**未证实**。

## 附

前序单元 WORKTAB-HIDE(8) / SHIFT(4) / TAKEOVER(7) / AUTOASSIGN(8) / AUTOASSIGN-ONLY1(4) /
OFFSHIFT(2) 均已单独通过 GATE-DELTA，其文件在本单元 `-ExcludeFromFile` 中排除。