---
change_id: CHG-20261005-DEVRENAME
version: v1.4.x
date: 2026-10-05
author: AI (CodeBuddy) + PersonalChronicle 维护者
scope: [Source/PersonalChronicle/Domain, Source/PersonalChronicle/Automation, Source/PersonalChronicle/Capture, Source/PersonalChronicle/Archive, Source/PersonalChronicle/ChronicleSettings.cs, Languages, docs]
files:
  - path: Source/PersonalChronicle/Domain/WorkshopDeviceNaming.cs
    type: A
    symbols: WorkshopDeviceNaming.Normalize / IsWithinLength / SameName / Conflicts / MaxLength
    lines: 1-105
  - path: Source/PersonalChronicle/Capture/Patch_WorkshopDeviceLabel.cs
    type: A
    symbols: Patch_WorkshopDeviceLabel（HarmonyPostfix）/ ManagerFor / ArchiveOverrides / Postfix
    lines: 1-170
  - path: Source/PersonalChronicle/Capture/Patch_BuildingLabel.cs
    type: D
    symbols: Patch_BuildingLabel（职责并入 #22）
    lines: 1-48
  - path: Source/PersonalChronicle/Automation/WorkshopDeviceLedger.cs
    type: M
    symbols: DeviceStatEntry.customName / SetCustomName / HasCustomName / CustomName ; WorkshopDeviceLedger.customNameCount / CustomNameCount / CustomNameOf / SetCustomNameOf / RecomputeCustomNameCount ; PruneNotIn ; ExposeData
    lines: 281-337, 615-640, 约655-660, PruneNotIn内
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.cs
    type: M
    symbols: DeviceRenameOutcome / TryRenameDevice / EffectiveNameOf / ArchiveOverrideOf / DisplayNameOf
    lines: 403-540
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: DrawDeviceRow（RenameBtnW / 按钮 / renameTipRect）/ OpenDeviceRenameDialog / HandleRenameOutcome
    lines: 858-905, 1007-1105
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Overview.cs
    type: M
    symbols: 概览 LED 名取数（ovLedNames）
    lines: 171-174
  - path: Source/PersonalChronicle/Automation/WorkshopFloorViewProvider.Detail.cs
    type: M
    symbols: EffectiveDeviceName / BuildHeader（detail.Name）
    lines: 125-145
  - path: Source/PersonalChronicle/Automation/Workshop/WorkshopPaletteAccessBinder.cs
    type: M
    symbols: nameManager / DeviceEntry label 取数
    lines: 144-172
  - path: Source/PersonalChronicle/Capture/CapturePatchDiagnostics.cs
    type: M
    symbols: Verify("GenLabel.ThingLabel(Thing,int,bool,bool)")
    lines: 118-132
  - path: Source/PersonalChronicle/ChronicleSettings.cs
    type: M
    symbols: DeviceRenameApplyToVanilla
    lines: 131, 158
  - path: Source/PersonalChronicle/Archive/Dialog_RenameWorkplace.cs
    type: M
    symbols: Dialog_RenameWorkplace.validate / 加性重载 / CommitAndClose
    lines: 37, 47-77, 约160-175
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: Key:PersonalChronicle.Automation.Tab.DeviceRename.* ×11
    lines: 100-111
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: Key:PersonalChronicle.Automation.Tab.DeviceRename.* ×11
    lines: 100-111
  - path: docs/设计文档/架构/EXC-2026-001.md
    type: M
    symbols: #14 → Retired ; 新增 #22 ; 清单变更记录 + 待办
    lines: 11, 40, 48, 63-64, 67
  - path: docs/设计文档/功能模块/车间体系/车间体系_设备重命名_需求与设计_2026-10-05.md
    type: A
    symbols: REQ-WORKSHOP-001~010 / O-1~O-4 裁决 / §18 Gate 预审与修正决议
    lines: 1-740
---

# 变更标注 · CHG-20261005-DEVRENAME（车间设备重命名）

- Change ID: **CHG-20261005-DEVRENAME**（贯穿 Intake → REQ/SDD → Annotation → Gate → Changelog）
- Version: v1.4.x
- Date: 2026-10-05
- Author/AI: AI (CodeBuddy) + 维护者
- Scope(摘要): 车间设备自定义名 —— 设备页行内入口 + 原版全局显示（Harmony 单点） + 设备级持久化 + 禁止重名

> ⚠ **状态：代码已落盘 + 已编译（0 错 0 警）+ 已部署到发布区；但【仍未实机验证】**。
> 本标注不是发布级证据；`GATE-TEST` 的实机项与 `GATE-PERF` 最小证据集仍为未执行。
> 行号取自落盘后实测（非估算）。

## 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 / Key | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `Domain/WorkshopDeviceNaming.cs` | **A** | `Normalize` / `IsWithinLength` / `SameName` / `Conflicts` / `MaxLength=24` | 1-105 | 新增纯函数规则层（可离线单测）。`Normalize` 含 **XML 非法字符清洗（P0-A）**，快路径零分配 | BASE-005 / DATA-001 / SAVE-004 | DATA / TEST |
| `Capture/Patch_WorkshopDeviceLabel.cs` | **A** | `Patch_WorkshopDeviceLabel`（`[HarmonyPatch(typeof(GenLabel), nameof(GenLabel.ThingLabel), new[]{Thing,int,bool,bool})]`） | 1-170（关键 47-49、104-160） | 新增**单点 Postfix**：`public` 类（问题十五铁律）+ 弱引用单条目缓存 + 双早退 + 节流告警；回落链 `车间名 ?? 档案覆盖 ?? 原版` | GOV-007 / COMP-004 / PERF-001 / EXC-002 | ARCH / COMP / PERF |
| `Capture/Patch_BuildingLabel.cs` | **D** | `Patch_BuildingLabel` | 1-48 | **退役**（方案 A 合并 ⇒ 挂点 2→1，双源覆盖冲突消除）。旧档 `BuildingLabelOverrides` 不迁移、继续只读 | GOV-005 / GOV-007 | ARCH / COMP / DELTA |
| `Automation/WorkshopDeviceLedger.cs` | M | `DeviceStatEntry.customName` / `HasCustomName` / `SetCustomName`；`customNameCount` / `CustomNameOf` / `SetCustomNameOf` / `RecomputeCustomNameCount`；`PruneNotIn`；`ExposeData` | 281-337 / 615-640 / ≈655-660 / PruneNotIn 内 | 加性字段（默认 null，**无 Migration**）+ 运行态计数（**三处**维护：改 / 淘汰 / 读档重算） | DATA-001/004 / SAVE-001 | DATA / SAVE |
| `Automation/MapComponent_WorkshopManager.cs` | M | `DeviceRenameOutcome` / `TryRenameDevice` / `EffectiveNameOf` / `ArchiveOverrideOf` / `DisplayNameOf` | 403-540 | **唯一写口**（五步校验：存在→已追踪→归一化+清洗→长度→同图重名）；`DisplayNameOf` 改走有效名 | GOV-005 / DATA-001 / LAND-009 | DATA / UI |
| `Automation/WorkshopManageView.cs` | M | `DrawDeviceRow`（`RenameBtnW=56f` / 按钮 / `renameTipRect`）/ `OpenDeviceRenameDialog` / `HandleRenameOutcome` | 858-905 / 1007-1105 | 行尾固定宽「改名」按钮（**画在整行 `ButtonInvisible` 之前**，否则点不到）+ 悬停（**在整行 TipRegion 之后注册**）+ 对话框接线与结果提示 | UI-006/009 / LAND-008 | UI |
| `Automation/WorkshopManageView.Overview.cs` | M | `ovLedNames` 取数 | 171-174 | 概览 LED 名改走有效名（除同屏两名） | GOV-005 / UI-003 | UI |
| `Automation/WorkshopFloorViewProvider.Detail.cs` | M | `EffectiveDeviceName` / `BuildHeader` | 125-145 | 设备卡标题改走有效名 | GOV-005 | UI |
| `Automation/Workshop/WorkshopPaletteAccessBinder.cs` | M | `nameManager` / `label` | 144-172 | 俯视卡 / 画布卡设备名同口径（管理器循环外取一次） | GOV-005 / PERF-001 | UI / PERF |
| `Capture/CapturePatchDiagnostics.cs` | M | `Verify("GenLabel.ThingLabel(Thing,int,bool,bool)")` | 118-132 | 签名探测（**带完整参数表**，防命中另一个 `ThingLabel` 重载） | COMP-004 / EXC-002 | COMP |
| `ChronicleSettings.cs` | M | `DeviceRenameApplyToVanilla` | 131, 158 | 新增开关（默认 `true`）+ Scribe；**P0-C：只关「车间名段」，不碰档案覆盖** | UI-002 / COMP-011 | UI / COMP |
| `Archive/Dialog_RenameWorkplace.cs` | M | `validate` / 加性重载 / `CommitAndClose` | 37, 47-77, ≈160-175 | **加性**重载（既有 3 调用点不变）：可拒绝校验（不关窗、保留输入）+ hint 参数通道（否则 `{0}` 显示不出） | UI-004/008 / LOC-004 | UI / LOC |
| `Languages/ChineseSimplified/Keyed/Automation.xml` | M | `PersonalChronicle.Automation.Tab.DeviceRename.*` ×11 | 100-111 | 中文键 | LOC-001/002/005 | LOC |
| `Languages/English/Keyed/Automation.xml` | M | 同上 ×11 | 100-111 | 英文键（双语同步） | LOC-003/005 | LOC |
| `docs/设计文档/架构/EXC-2026-001.md` | M | `#14` → Retired；新增 `#22` | 11 / 40 / 48 / 63-64 / 67 | Harmony 例外台账更新（含行为扩展登记与回滚说明） | EXC-001/002 / GOV-007 | REL / COMP |
| `docs/设计文档/功能模块/车间体系/车间体系_设备重命名_需求与设计_2026-10-05.md` | **A** | REQ-WORKSHOP-001~010 / O-1~O-4 / §18 | 1-740 | 需求与设计（含 5 Gate 预审结论 §18，优先于前文） | REQ-004/005 / SDD-002/004 | REL |

改动类型：**A**=新增，**M**=修改，**D**=删除。

## 设计裁决留痕（本变更的「为什么」）

| 项 | 结论 | 依据 |
|---|---|---|
| 收口点 | `RimWorld.GenLabel.ThingLabel(Thing,int,bool,bool)` | Cecil 实证：private `NewThingLabel` 仅被它调用；9 处 `get_LabelNoCount` 覆写里设备走的 `ThingWithComps` 会调 base |
| 为何不用更窄的 `ThingWithComps.get_LabelNoCount` | 会漏 `LabelNoParenthesis` / `GetCustomLabelNoCount` ⇒ 不满足 REQ-002 | 同上 |
| 为何不用原版钩子 `ThingComp.TransformLabel` | 原生且风险低，但需**逐 Def** XML Patch ⇒ 对象不可穷举（任意工作台） | 设计文档 §18.6 |
| 方案 A（合并遗留补丁） | 成立，**不回落方案 B** | GATE-ARCH：合并只改 Capture 显示层读取点，Archive 存储/写入端零 diff |
| 覆盖边界 | `MinifiedThing`（收存箱）自建标签 ⇒ **收存态不显示自定义名**（设计内行为） | 同上 |

## 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖本变更**全部**改动文件（12 代码/语言 + 1 删除 + 2 文档），无遗漏
- [x] 清单范围 ⊆ Scope，无超范围改动（未触碰 Archive 数据链路 / Def / Asset）
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 EXC 台账、REQ/SDD、Changelog 互链一致
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留（新增代码内无 `Console.WriteLine` / `Log.Message` 调试输出）
- [ ] **`gate-runner.ps1 -Annotation` 自动校验：未执行**（待编译轮一起跑；本仓 git 全未跟踪，需 `-Base HEAD -ExcludeFromFile`）

## 关键 diff 片段

```diff
// Source/PersonalChronicle/Capture/Patch_WorkshopDeviceLabel.cs（新增 · 单点收口）
+[HarmonyPatch(typeof(GenLabel), nameof(GenLabel.ThingLabel),
+    new[] { typeof(Thing), typeof(int), typeof(bool), typeof(bool) })]
+public static class Patch_WorkshopDeviceLabel
+{
+    private static void Postfix(Thing __0, ref string __result)
+    {
+        // ① 车间自定义名（受开关控制）→ ② 档案覆盖（不受开关控制，P0-C）→ 否则不动 __result
+    }
+}

// Source/PersonalChronicle/Automation/WorkshopDeviceLedger.cs（加性字段 · 无 Migration）
+private string customName;
+Scribe_Values.Look(ref customName, "customName", null);

// Source/PersonalChronicle/Domain/WorkshopDeviceNaming.cs（P0-A · XML 非法字符清洗）
+public static string Normalize(string raw)   // 空/纯空白 → null；剔除 U+0000–U+001F（除 \t\n\r）
```

## 未完成项（本标注如实登记，勿当作已完成）

1. ~~未编译~~ ⇒ **已编译：0 错 0 警**（2026-10-05 15:33 · `run_csc.ps1`）。首轮报 **1 错 1 警**，均已修：
   - `error CS0103`：Mod 入口类实名是 **`PersonalChronicleMod`**（文件名 `ChronicleMod.cs` 具误导性）
   - `warning CS0618`：`object hintArg` 会强制绑定已过时的 `Translate(string, params object[])`；
     形参改 `string` 后绑定非过时的 `Translate(string, string)` ⇒ **该条属工程级陷阱，写新代码时勿用 `object` 传翻译参数**
2. ~~未部署~~ ⇒ **已部署**到发布区（`_scripts\deploy-to-game.ps1`；127 文件同步 · Languages 39 已含本次 2 个 XML · dll 哈希 dev==pub · 备份 `_backups\deploy_20261005_153323`）。**仍未实机验证**（需重启游戏；GATE-PERF 证据集 B1–B7 全未执行）。
3. **设置页开关行未接入** `SettingsCatalog` / `SettingsDrawer` / `SettingId` —— 开关**默认已生效**，仅无 UI 入口。
4. **离线单测未写**（命名规则含非法字符用例、Scribe 往返、计数不变式）。
5. **U-16 未核**（收口点跑在 `ThingComp.TransformLabel` 之前 ⇒ comp 后缀泄漏；须先核工作台类 Def 实际挂载的 comp）。
6. **设备页列宽预算未做**：56px 从名称文本可用宽中切出，**未改** `DeviceHeaders` / `DeviceColShares*` / `DeviceCompactWidth`（刻意的零布局风险取舍，Gate-UI P1-9 后续项）。
7. `MapComponent_WorkshopManager.cs:309-312` 的**旧注释仍断言「原版无 Rename 成员 ⇒ 故意不改 Thing 显示名」**——该结论已被本变更推翻，但注释未清理（避免超范围改动，留待下次顺手修）。
