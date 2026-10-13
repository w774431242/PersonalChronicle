---
change_id: CHG-20261004-WORKTAB-HIDE
version: v1.1.5
date: 2026-10-03
author: AI
scope: [Source/PersonalChronicle/Domain, Source/PersonalChronicle/Data, Source/PersonalChronicle/Capture, Languages]
files:
  - path: Source/PersonalChronicle/Domain/WorkTabHideFilter.cs
    type: A
    symbols: WorkTabHideFilter.ShouldHide / WorkTabHideFilter.SanitizeKey / MaxHiddenColonists
    lines: 1-48
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.WorkTabHide.cs
    type: A
    symbols: ChronicleGameComponent.hiddenWorkTabColonists / ExposeWorkTabHide / NotifyWorkTabChanged
    lines: 1-155
  - path: Source/PersonalChronicle/Capture/Patch_MainTabWindowWorkPawns.cs
    type: A
    symbols: Patch_MainTabWindowWorkPawns.Postfix / RevealAll / Reset
    lines: 1-100
  - path: Source/PersonalChronicle/Capture/Patch_PawnWorkTabGizmo.cs
    type: A
    symbols: Patch_PawnWorkTabGizmo.Postfix / HideIcon / ShowIcon
    lines: 1-121
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.cs
    type: M
    symbols: ChronicleGameComponent.ExposeData / ChronicleGameComponent.FinalizeInit
    lines: 221-222, 280-281
  - path: Source/PersonalChronicle/Capture/CapturePatchDiagnostics.cs
    type: M
    symbols: CapturePatchDiagnostics.Verify
    lines: 48-61
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: Key:PersonalChronicle.WorkTab.Hide / Key:PersonalChronicle.WorkTab.Show / Key:PersonalChronicle.WorkTab.HideDesc / Key:PersonalChronicle.WorkTab.ShowDesc
    lines: 3669-3673
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: Key:PersonalChronicle.WorkTab.Hide / Key:PersonalChronicle.WorkTab.Show / Key:PersonalChronicle.WorkTab.HideDesc / Key:PersonalChronicle.WorkTab.ShowDesc
    lines: 3661-3665
---

# 变更标注 · CHG-20261004-WORKTAB-HIDE

在原版「工作」标签（工作优先级面板）隐藏指定殖民者。目标 Build **1.6.4871 rev590** · 编译 **0 错 0 警**（`_scripts/compile_with_sdk.ps1` EXIT=0）。

## 1. Scope（8 个文件）

| # | 文件 | 状态 | 行 | 内容 |
|---|---|---|---|---|
| 1 | `Source/PersonalChronicle/Domain/WorkTabHideFilter.cs` | 新增 | 1-48 | 纯逻辑 `ShouldHide`/`SanitizeKey`/`MaxHiddenColonists=256`，无状态、不引用 Pawn/Def/UI |
| 2 | `Source/PersonalChronicle/Data/ChronicleGameComponent.WorkTabHide.cs` | 新增 | 1-155 | 存档分部：字段 + `ExposeWorkTabHide()` + 键清洗 + 有界裁剪 + `NotifyWorkTabChanged()` |
| 3 | `Source/PersonalChronicle/Capture/Patch_MainTabWindowWorkPawns.cs` | 新增 | 1-100 | **P1** Postfix `MainTabWindow_Work.get_Pawns` 过滤；`RevealAll` 会话开关 + `Reset()` |
| 4 | `Source/PersonalChronicle/Capture/Patch_PawnWorkTabGizmo.cs` | 新增 | 1-121 | **P2** Postfix `Pawn.GetGizmos` 追加「隐藏/显示」按钮 |
| 5 | `Source/PersonalChronicle/Data/ChronicleGameComponent.cs` | 修改 | +221-222 / +280-281 | 各加 1 行：`ExposeWorkTabHide();`、`Capture.Patch_MainTabWindowWorkPawns.Reset();` |
| 6 | `Source/PersonalChronicle/Capture/CapturePatchDiagnostics.cs` | 修改 | +48-61 | 追加 2 条启动自检，沿用既有 `Verify(...)`+`AccessTools.Method` 格式 |
| 7 | `Languages/ChineseSimplified/Keyed/Archive.xml` | 修改 | +3669-3673 | 新增 4 键 |
| 8 | `Languages/English/Keyed/Archive.xml` | 修改 | +3661-3665 | 同上，键集对等 |

本文件亦属 Scope（已在范围外清单中排除）。

## 2. 规则 ID

`GOV-001` `GOV-003` `GOV-005` `GOV-006` `GOV-007` `GOV-010` `GOV-011` `GOV-012` `ARC-008` `SAVE-*` `UI-005` `LOC-001` `COMP-004` `COMP-005` `PERF-001` `PERF-004` `AI-003` `AI-004` `AI-014`

## 3. Architecture

分层落点`Domain/`（纯逻辑）← `Data/`（存档）← `Capture/`（Harmony 钩子）；`Compatibility/` 未触碰。依赖方向 `Capture → Data → Domain` 全为既有允许方向（README：`UI → Application → Domain ← Data / Capture`），**无新增跨层边、无环、无 static mutable 单例**。

扩展点（AI-003）：

- ❌ **否决** `PatchOperation(MainButtonDef.Work/tabWindowClass)` + 子类 override —— 该扩展点**已核实真实可用**（`MainButtonDef.tabWindowClass` public Type；`get_TabWindow()` IL = `Activator.CreateInstance`；生产先例 `workshop/content/294100/3773496046/1.6/Defs/MainTabDefs/MainTabWindow.xml:8`）。**否决原因是用户决策，非技术不可行。**
- ❌ `PawnColumnDef`/`PawnTableDef`：IL 证实只能加列不能删行，能力不成立
- ❌ `DefModExtension`（静态配置 vs 运行时状态）、`CompPawn`（需为所有人类 Pawn 挂 Comp，侵入面更大）
- ❌ `Prefix`（需复刻整条谓词链，原版加条件即静默漏掉）、`Transpiler`（风险最高）
- ✅ Harmony **Postfix ×2**

## 4. Data / Save

Def 零新增。存档为**加性可选字段** `hiddenWorkTabColonists`（`HashSet<string>`，camelCase 与工程既有风格一致）；旧档读回 `null` ⇒ 兜底空集合 ⇒ 行为与旧档一致，**不 bump `SchemaVersion`、无 Migration**（同 `workshopWorkDemotion`/`stockSeries` 先例）。只存 `ThingID` 不存 `Pawn` 引用（避免读档悬挂引用）。
**P0 防护**：Saving 阶段先清洗键（复用 `WorkshopWorkDemotionLedger.SanitizeKey`，与 2026-10-02 非法 XML 字符导致整档报废事故同源），PostLoadInit 幂等兜底。有界 `MaxHiddenColonists=256`，写入前判容量、超限裁剪。

## 5. Localization

新增 4 键，中英**键集对等**：`PersonalChronicle.WorkTab.Hide` / `.Show` / `.HideDesc` / `.ShowDesc`，沿用 `PersonalChronicle.<Area>.<Sub>.<Leaf>` 风格。

## 6. Compatibility（Harmony）

| Patch | 目标 | 特性 | 降级 |
|---|---|---|---|
| P1 | `typeof(MainTabWindow_Work), "get_Pawns"` | `[HarmonyPriority(Priority.Low)]`、Postfix `ref IEnumerable<Pawn>` | **fail-open**：名单空/组件缺失/异常 ⇒ 原样返回「不隐藏任何人」 |
| P2 | `typeof(Pawn), nameof(Pawn.GetGizmos)` | 同上、Postfix `ref IEnumerable<Gizmo>` | **fail-open**：仅少一个按钮，名单不受影响 |

**铁律（已写入类注释）**：`__result` 只换引用、绝不原地改。IL 实测 `MainTabWindow_Work.get_Pawns` 返回惰性 `IEnumerable`；基类返回的 `MapPawns.FreeColonists` 是**内部复用缓存 List**，全库 **63 处**消费（`ColonistBar.CheckRecacheEntries` / `CompAssignableToPawn*.AssigningCandidates` / `PawnsFinder.get_AllMaps_FreeColonists` / `StrengthWatcher.get_StrengthRating` / `QuestNode_GetMap` / `ThingUtility.FindAllOfType`）。对它 `RemoveAll` 会污染原版缓存 ⇒ 连锁出错。
漂移检测：追加 2 条启动自检（`AccessTools.Method` 反射，不依赖 Harmony 成功）。

## 7. Performance

名单空 ⇒ 立即 return 零分配；非空 ⇒ 手写循环 + 单个 `List<Pawn>`，**不用 LINQ**；`get_Pawns` 仅在 `PawnTable` dirty 时调用；刷新走 `Notify_PawnsChanged()`（`SetDirty()` protected，唯一公开入口）；`WindowStack` 用索引器遍历（不实现 `IEnumerable`，编译期已证）。

## 8. Exceptions（待Architecture Review 批准）

| ID | 范围 | 等级 | 理由 |
|---|---|---|---|
| EXC-2026-017 | P1 `get_Pawns` | **E2** | Harmony 打原版 UI 内部实现；字符串目标在原版升级后**静默不生效**（已用启动自检 + fail-open 缓解） |
| EXC-2026-018 | P2 `GetGizmos` | **E2** | 原版无任何 Def/XML 手段为 Pawn 追加运行时 Gizmo |
| EXC-2026-019 | Scribe key | E1 | 稳定标识符不可避免以字面量出现（`Scribe_Collections.Look` API 强制） |
| EXC-2026-020 | GOV-007 偏离 | E1 | **明知`MainButtonDef.tabWindowClass` 可用而不用**，原因见 §3 |

## 9. Gate Results

| Gate | Result | Evidence |
|---|---|---|
| ARCH | **待判** | 分层/依赖/无环/无 static 已自证，但**最终判定须由 `pc-arch-gatekeeper` 出**（总控 `OWNED_GATES: NONE`，不得代判） |
| DATA | PASS | Def 零新增；运行时不入 Def/UI |
| SAVE | PASS（静态） | 加性可选 + 旧档null 兜底 + 不 bump 版本 + Saving 前键清洗 + PostLoadInit 幂等。**实机存读档迁移未执行** |
| UI | PASS | 入口为追加式 `Command`，不持持久状态；`RevealAll` 为会话级不落盘 |
| LOC | PASS | 4 键全Keyed、中英对等、fallback 可解释 |
| COMP | **待判** | 2 个 Postfix 的目标解析/降级/叠加冲突矩阵须由 `pc-compat-auditor` 出 |
| PERF | **未执行** | 无实机采样；仅有静态复杂度论证 |
| TEST | 部分PASS | 0 错 0 警已达成；**Vanilla 启动、存读档、实机叠加未执行**（沙箱起不了游戏进程，无伪造 PASS） |
| REL | **未执行** | 版本号/Change Summary/Changelog 待定稿 |
| DELTA | 见 §10 | 由 `gate-runner.ps1` 机械校验 |

## 10. Remaining Risks

1. **P0** 污染 `MapPawns.FreeColonists` —— 本方案不碰它，过滤只发生在 Postfix 返回值上；已用「只换引用」铁律 + 注释锁定。
2. **P0** 破坏存档 —— 已用 Saving 前键清洗 + 加性可选段 + 不 bump 版本。
3. **P1** 静默不生效 —— 字符串目标失效只表现为功能消失；已加 2 条启动自检 + Player.log `capture target missing`。
4. **P1** 其它 mod 也patch `get_Pawns` —— 用 `HarmonyPriority(Low)` 争取最后执行；本机 641 个 mod 扫描未发现同类。
5. **P2** 原版重构取人链不再消费 `get_Pawns` ⇒ 自检全绿但功能无效，**启动时不可行，已登记为残余风险**。
6. **P2** 图标路径 `UI/Designators/HideEverything` / `UI/Buttons/Visible` 未经实机验证，`ContentFinder.Get(path, false)` 返回 null 时按既有先例回落 `TexButton.OpenStatsReport`。

## 11. 未实现的已批准项

「**热键临时显示全部已隐藏者**」**本轮未实现**，需新增 `KeyBindingDef` + DefInjected 中英双份 + 第 3 处基类补丁（`Window.WindowUpdate`，已核实由 `WindowStack.WindowsUpdate` 驱动）。当前恢复出口为**同一 Gizmo 切回**（殖民者在任何位置被选中都能点），已覆盖核心可用性；`RevealAll` 静态开关与 `Reset()` 已就位，作为后续接线的落点。
