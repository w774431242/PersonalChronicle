---
change_id: CHG-20261004-WORKTAB-TAKEOVER
version: v1.1.5
date: 2026-10-03
author: AI
scope: [Source/PersonalChronicle/Capture, Source/PersonalChronicle/Archive/UI/WorkTab, Source/PersonalChronicle, Languages]
files:
  - path: Source/PersonalChronicle/Capture/Patch_MainTabWindowWorkTabTakeover.cs
    type: A
    symbols: Patch_MainTabWindowWorkTabTakeover / Patch_WorkTabExtraTopSpace / Patch_WorkTabExtraBottomSpace
    lines: 1-99
  - path: Source/PersonalChronicle/Archive/UI/WorkTab/WorkTabTakeoverPanel.cs
    type: A
    symbols: WorkTabTakeoverPanel.Draw / DrawToolbar / DrawHideEye / DistributeWidths
    lines: 1-320
  - path: Source/PersonalChronicle/Archive/UI/WorkTab/WorkTabTakeoverBridge.cs
    type: A
    symbols: WorkTabTableAccess.TryGetTable / WorkTabPriorityWriter.Clamp / WorkTabHideService
    lines: 1-92
  - path: Source/PersonalChronicle/ChronicleSettings.cs
    type: M
    symbols: ChronicleSettings.TakeoverWorkTab
    lines: 40-42, 70, 88
  - path: Source/PersonalChronicle/Capture/CapturePatchDiagnostics.cs
    type: M
    symbols: CapturePatchDiagnostics.Verify
    lines: 62-81
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: Key:PersonalChronicle.WorkTab.Pc.Search / RevealOn / EscapeHatch / Footer / Settings.EnableTakeover
    lines: 3675-3680
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: Key:PersonalChronicle.WorkTab.Pc.Search / RevealOn / EscapeHatch / Footer / Settings.EnableTakeover
    lines: 3667-3672
---

# 变更标注 · CHG-20261004-WORKTAB-TAKEOVER（单元 A）

由 PC **接管**原版「工作」主标签的绘制。Build **1.6.4871 rev590** · 编译 **0 错 0 警**（EXIT=0）。

## 1. Scope（7 文件）

| # | 文件 | 类型 | 内容 |
|---|---|---|---|
| 1 | `Capture/Patch_MainTabWindowWorkTabTakeover.cs` | A | **3 个 Prefix/Postfix**：接管 `DoWindowContents` + 两个 extra space 有条件归零 |
| 2 | `Archive/UI/WorkTab/WorkTabTakeoverPanel.cs` | A | 自绘外壳：工具条（搜索 / 临时显示全部 / 逃生口）、表头条、行区滚动、行内隐藏眼标、底栏、列宽分配 |
| 3 | `Archive/UI/WorkTab/WorkTabTakeoverBridge.cs` | A | 反射取 `PawnTable`（EXC-2026-022）、档位钳位、隐藏门面 |
| 4 | `ChronicleSettings.cs` | M | `TakeoverWorkTab`（默认 true，D-4）+ Scribe + 设置页勾选框 |
| 5 | `Capture/CapturePatchDiagnostics.cs` | M | 追加 3 条启动自检（接管三目标） |
| 6-7 | `Languages/{ChineseSimplified,English}/Keyed/Archive.xml` | M | 各 5 键，中英对等 |

## 2. 规则 ID

`GOV-001` `GOV-007` `GOV-010` `GOV-011` `GOV-012` `ARC-008` `SAVE-*`（Mod 设置项落盘，非存档 schema）
`UI-005` `UI-006` `UI-007` `UI-009` `LOC-001` `COMP-004` `COMP-005` `PERF-001` `PERF-004` `AI-003` `AI-004` `AI-014`

## 3. Architecture

落点 `Capture/`（Harmony 钩子）+ `Archive/UI/WorkTab/`（表现层）+ `ChronicleSettings`（Mod 设置）。
**零 Def、零存档 schema 变更、零 SchemaVersion 变更**（`TakeoverWorkTab` 走 ModSettings XML，与存档无关）。
分层依据：Harmony 打原版 UI 类型，工程 19/19 既成先例（`Patch_BuildingLabel` / `Patch_RoomRoleLabel`）均在 `Capture/`。

扩展点（AI-003）：采纳 **Harmony Prefix 阻止 `DoWindowContents`**（唯一能抑制原版绘制的手段）。
否决并记录原因：`MainButtonDef.tabWindowClass` XML 接管**已核实可用**但因用户决策否决
（EXC-2026-020 覆盖该 GOV-007 偏离）；`PawnColumnDef`/`PawnTableDef` 只能加列不能删行，能力不成立；
Postfix 无法抑制原版输出；Transpiler 风险最高且无必要。

## 4. 关键裁决与依据

| 项 | 裁决 | 依据 |
|---|---|---|
| 格子绘制 | **交回原版 `PawnColumnWorker.DoHeader` / `DoCell`** | 二者 `public virtual`，`GetMinWidth`/`GetOptimalWidth`/`GetMaxWidth`/`GetMinCellHeight`/`GetMinHeaderHeight`/`VisibleCurrently` 全公开 ⇒ 列可见性与原版逐字一致，未来原版改列规则自动继承；不必复刻私有列宽算法 |
| 表数据源 | **反射复用窗口自己的 `PawnTable`**（EXC-2026-022） | 自建实例会出现两套缓存两套名单，与上一单元 `get_Pawns` 补丁结果分叉 |
| 刷新时机 | 直接读 `table.Columns` / `PawnsListForReading` | 二者首句都是 `RecacheIfDirty()`（IL 实证）⇒ 必然新鲜 |
| 尺寸契约 | 不覆写 `RequestedTabSize`，改为两个 extra space 归零 | 避免与主标签栏抢尺寸；由基类公式自动跟随 |
| ⚠ P0 陷阱 | Top 打 **Work 的覆写**；Bottom 打**基类 + `is MainTabWindow_Work` 守卫** | Work **只覆写了 Top**（基类 0），**没有**覆写 Bottom（基类 53）⇒ 无守卫会把分配/作息/动物/野生动物底部空间全清零 |
| 档位安全 | `Clamp[0, LowestPriority]` | IL 实证 `SetPriority` 对越界值**只 Log.Message 然后照样写**（不抛不拒） |
| fail-open | Prefix 内任何异常 ⇒ `__result = true` 交还原版 | 玩家看到「变回原版」，不是坏界面 |
| 逃生口（双层） | 面板内按钮（会话态）+ **Mod 设置勾选框（落盘）** | 落盘项是「他人 mod 抢 Prefix 导致面板缺失」时**唯一可靠**的退路 |
## 5. D-2 决策的落地边界（**未完成，须如实记录**）

D-2 裁决是「车间托管格**只读** + 徽标」。本轮**只落了信息位提示，未落硬锁**，原因：

- 车间侧托管集合的构造（`workSyncTypes` / `AddWorkTypeNames` / `DutyWorkType()`）**全是 private**，
  且「某pawn × 某工作类型」是否托管依赖当班班组解析；
- 自造判定会违反 GOV-005（跨模块读内部实现）**且无法实机验证**。

⇒ 硬锁留 Phase 2，前置条件是**车间侧先补出公开契约**。
**当前残留 UX 债**：玩家在 PC 面板改动车间托管的工作类型档位，仍可能被下次排班变更静默回滚
（`EnableWorkTypes` 把值勤收敛回 3、映射类型收敛回 1）。本轮已在底栏文案提示，但未强制阻断。

## 6. Compatibility

| Patch | 目标 | 形态 | 降级 |
|---|---|---|---|
| P1 | `MainTabWindow_Work.DoWindowContents` | Prefix `ref bool __result` | 异常 / 设置关闭 / 取表失败 ⇒ `__result=true` 交还原版 |
| P2 | `MainTabWindow_Work.get_ExtraTopSpace` | Postfix `ref float` | 恒 0 |
| P3 | `MainTabWindow_PawnTable.get_ExtraBottomSpace` | Postfix + `is MainTabWindow_Work` 守卫 | 非 Work 标签不受影响 |

叠加冲突：专用 work-tab mod（Better Work Tab / Work Tab (Continued) / Enhanced Work Tab）也 Prefix/Postfix
`DoWindowContents`；`HarmonyPriority(Priority.Low)` 让对方面板先跑、我方兜底。
**冲突不可见**（Harmony 按 priority），故落盘逃生口是必需项而非可选项。

## 7. Performance

- 宽度/行高/表头高取自原版公开度量，**不猜**；`DistributeWidths` 每帧 O(列数)。
- 可见行裁剪（UI-007）⇒ 200 人 colony 只绘制视口内行。
- 行高钳 `[24, 64]`（`MaxRowH`）⇒ 异常列不撑爆布局。
- 反射每帧一次 `FieldInfo.GetValue`（`FieldInfo` 已 static 缓存）。
- `BeginScrollView` / `EndScrollView` 包在 `try/finally`（工程既有三处同款纪律）。

## 8. Gate Results

| Gate | Result | Evidence |
|---|---|---|
| ARCH | **待判** | 分层/依赖方向/EXC 成立性已自证；**最终判定须由 `pc-arch-gatekeeper` 出**（总控 `OWNED_GATES: NONE`） |
| DATA | PASS | 零 Def 新增；运行时不入 Def |
| SAVE | PASS（静态） | 零存档字段 / 零 SchemaVersion；`TakeoverWorkTab` 走 ModSettings 非存档。**实机存读档未执行** |
| UI | PASS（静态） | 命中顺序、可见行裁剪、try/finally、Tooltip 延迟求值、零尺寸早退；**实机布局未执行** |
| LOC | PASS | 新增 5 键中英对等，复用上一单元既有 `WorkTab.Hide` / `.Show` |
| COMP | **待判** | 3 patch 目标、叠加冲突矩阵、两级降级已给；**判定须由 `pc-compat-auditor` 出** |
| PERF | **未执行** | 有静态复杂度论证（§7），**无实机采样** |
| TEST | 部分 PASS | 0 错 0 警；**Vanilla 启动、面板行为、多 mod 实机叠加未执行**（沙箱无游戏进程，无伪造 PASS） |
| REL | **未执行** | 版本 / Changelog 待定稿；⚠ **EXC-002 第 5 条「Vanilla 回归证据」尚缺，列为 Release 前置条件** |
| DELTA | 见 §10 | 由 `gate-runner.ps1` 机械校验 |

## 9. Remaining Risks

1. **P0** `get_ExtraBottomSpace` 守卫写错 ⇒ 四个标签底部空间被清零。已三重锁定：代码注释 + `is` 守卫 + 本标注 §4。
2. **P0** 档位越界写脏档位（`SetPriority` 不拦）⇒ `Clamp[0, LowestPriority]`。
3. **P1** 他人 mod 抢 Prefix ⇒ 面板缺失且会话逃生不可用 ⇒ **落盘 Mod 设置是唯一可靠退路**。
4. **P1** private 字段 `table` 漂移 ⇒ `TryGetTable` 返回 null ⇒ fail-open 回原版（改进项：每会话一次告警，当前静默）。
5. **P1** D-2 硬锁未落地 ⇒ 见 §5，UX 债已如实登记。
6. **P2** `Widgets.TextField` 每帧调用（沿用原版范式，未加脏标记节流）。

## 10. 附

- 前序单元 `CHG-20261004-WORKTAB-HIDE`（隐藏名单 8 文件）与 `CHG-20261004-WORKTAB-SHIFT`（班次勾选 4 文件）
  均已单独通过 GATE-DELTA，其文件在本单元 `-ExcludeFromFile` 中按「已交付单元」排除。