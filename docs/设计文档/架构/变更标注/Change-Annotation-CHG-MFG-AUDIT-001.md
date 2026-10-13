# 变更标注 · CHG-MFG-AUDIT-001 — 制造系统中心审计修复（一次性完整交付）

- **Change ID:** CHG-MFG-AUDIT-001
- **日期:** 2026-09-21
- **工程:** PersonalChronicle（RimWorld 1.6 Mod）
- **触发:** 用户指令「进入修复阶段，一次性完整交付」
- **依据审计:** `docs/设计文档/架构/审计-制造系统中心-2026-09-21.md`（2026-09-21，判定 ARCH/UI/LOC/PERF/TEST 五维 BLOCKED）
- **分层豁免:** `docs/设计文档/架构/EXC-2026-011.md`（E2）

## 1. 范围
制造中心 UI（`Manufacturing/UI/`）+ Application 层受控契约（`Application/Workshop/`、`Application/ReadModels/`）+ Automation 侧注入（`Automation/Workshop/`）。

## 2. 修复映射（审计 §4 清单）

| 审计项 | 级别 | 处置 | Evidence |
|---|---|---|---|
| ARCH：`WorkshopLayoutCanvas` 直读 `WorkshopDeviceReader`/`WorkshopStats`/`Zone_Workshop`/`MapComponent_WorkshopManager`/`WorkshopDutyScoring` | P1 | 契约化（A-1） | `WorkshopLayoutCanvas.Palette.cs` 经 `WorkshopPaletteAccess` 取数；`WorkshopPaletteAccessBinder.cs` 启动注入；主文件不再直读 |
| ARCH：静态可变态硬触发 | P1 | 收敛 + EXC | `paletteScanTicks`/`DeviceCountCache`/`DeviceListCache`/`MemberCache` 迁至 `WorkshopPaletteAccessBinder`；`ClearSession`→`WorkshopPaletteAccess.ClearCache()`；`EXC-2026-011` |
| UI：裁剪栈未 finally | P1 | 已修 | `FloorDeviceDetailWindow.cs` `DrawDetailContent` 进 try/finally |
| TEST：测试工程编译失败（CS0117×3+CS0246×6） | P1 | 已修 | 主工程 + 测试工程均 0 错 0 警（`-t:Rebuild`） |
| LOC：`WorkshopTypeDef` 无 EN DefInjected | P2 | 已修 | `Languages/English/DefInjected/WorkshopTypeDef/WorkshopTypes.xml`（15 WT_*） |
| UI：无面板级隔离 | P2 | 已修 | `MasterControlTabWindow.Detail.cs` `DrawTabPaneContent` 各面板 try/catch + `DrawDetailPanelFallback` 占位 + 5s 节流日志 |
| UI：平面图裸色值 | P3 | 已修 | 新增 `FloorPalette.cs`（Slab/LegendFill/RailFill/HazardBase/DeviceTier1/5/6 + HazardTint/HazardTintStrong）；`WorkshopFloorPlan.cs`/`.Layers.cs`/`FloorDeviceDetailWindow.cs` 引用，无裸 `new Color` |
| UI：行高偏紧 | P3 | 已修 | `ManufacturingStockQueuePanels.cs` RowHeight 17→20、标题框 18→22/20→24 |
| LOC：嵌套 `DefInjected/DefInjected/` | P3 | 已修（删除嵌套目录，内容比对为顶层重复/子集，删不丢翻译） | 删 `Languages/English/DefInjected/DefInjected/`；ArchiveCategoryDef 字节一致、MainButtonDef 为顶层子集 |
| PERF：每帧无界日志 | P3 | 已修 | `WorkshopLayoutCanvas.Device.cs` 设备卡失败日志 5s 节流 |
| PERF：时间字面量 | P3 | 已修 | `ManufacturingEventsPanels.cs` `2500L`→`GenDate.TicksPerHour` 等 |

## 3. 新增 / 修改文件
- 新增：`Source/PersonalChronicle/Application/ReadModels/WorkshopPaletteModels.cs`
- 新增：`Source/PersonalChronicle/Application/Workshop/IWorkshopPaletteAccess.cs`
- 新增：`Source/PersonalChronicle/Automation/Workshop/WorkshopPaletteAccessBinder.cs`
- 新增：`Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.Palette.cs`
- 新增：`Source/PersonalChronicle/Manufacturing/UI/FloorPalette.cs`
- 新增：`docs/设计文档/架构/EXC-2026-011.md`
- 改：`WorkshopLayoutCanvas.cs`（删重复结构体/缓存字段，`ClearSession`→契约）
- 改：`WorkshopFloorPlan.cs` / `.Rails.cs` / `MasterControlTabWindow.AppShell.cs`（引用迁移到 ReadModels）
- 改：`FloorDeviceDetailWindow.cs`（裁剪栈 finally + 裸色）
- 改：`ManufacturingStockQueuePanels.cs`（行高）
- 改：`ManufacturingEventsPanels.cs`（时间字面量 + `using RimWorld`）
- 改：`WorkshopLayoutCanvas.Device.cs`（日志节流）
- 改：`MasterControlTabWindow.Detail.cs`（面板级隔离 + `using System`）
- 改：`Languages/ChineseSimplified/Keyed/Archive.xml` + `Languages/English/Keyed/Archive.xml`（新增 `PanelFailed` 键）

## 4. Gate 复验（5 Gate 只读子 agent）
- ARCH / UI-LOC / DATA-SAVE / PERF-TEST / COMP：见 `.codebuddy/audit/gate-records/GR-MFG-AUDIT-001-{ARCH,UI-LOC,DATA-SAVE,PERF-TEST,COMP}.md`
- 编译实证：主工程 0 错 0 警；测试工程 0 错 0 警；单测实跑 386 通过 / 0 失败 / 1 跳过。
- 终验结论：ARCH=PASS(WEXC) / UI=PASS / LOC=PASS / DATA=PASS / SAVE=PASS(WEXC) / PERF=PASS(WEXC) / TEST=PASS / COMP=PASS。原五维 BLOCKED 全部闭合（仅实机采样 / 旧档 / 冒烟为 tier-C 降级）。

## 5. 残留（Tier 降级 · 如实登记，不折算 PASS）
- LOC 嵌套 `DefInjected/DefInjected/` 与 UI 裸色 2 处白-alpha 字面量：终验前由主 Agent 补修（删除嵌套目录 / 收进 `FloorPalette.HazardTint`·`HazardTintStrong` / 移除无用 `using PersonalChronicle.Automation;`）⇒ LOC=PASS、UI=PASS（4/4）。
- GATE-PERF：无 PerfProbe/基准 ⇒ 无可复核运行时指标（代码已做节流，但缺实机采样）。
- GATE-SAVE：实机旧档（无制造段）读入冒烟测试未执行 ⇒ 仅代码实证。
- GATE-TEST：Vanilla 启动 / 存读档迁移 / UI 关键路径 / 语言切换 ⇒ 无运行环境。
- GATE-COMP：兼容测试矩阵 / ~7/18 Harmony 目标签名核验 ⇒ 超出静态只读范围（WARN 仍 Open）。
- DATA：审计提及 `BridgePanelView.Weight` 死字段 ⇒ 全代码检索无引用，实为已清理/不存在，不另处理。

## 6. GATE-DELTA
交付前跑 `gate-runner.ps1 -Base origin/main -Annotation <本标注> -Blocking`（脚本物理缺失，由主 Agent 按 diff 覆盖核对：本批改动 ⊆ diff ⊆ Scope）。
