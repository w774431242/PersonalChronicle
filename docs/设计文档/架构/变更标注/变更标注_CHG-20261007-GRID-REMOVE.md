---
change_id: CHG-20261007-GRID-REMOVE
version: v0.1
date: 2026-10-07
author: AI
scope: [Source/PersonalChronicle/, Tests/, Defs/, Languages/, Textures/Things/Building/, docs/设计文档/架构/, docs/设计文档/功能模块/, docs/README.md]
files:
  # ── D · 源文件：Domain/Grid（10）──
  - path: Source/PersonalChronicle/Domain/Grid/CulpritSelector.cs
    type: D
  - path: Source/PersonalChronicle/Domain/Grid/GridMetering.cs
    type: D
  - path: Source/PersonalChronicle/Domain/Grid/GridMeterRing.cs
    type: D
  - path: Source/PersonalChronicle/Domain/Grid/GridPartition.cs
    type: D
  - path: Source/PersonalChronicle/Domain/Grid/GridPartitionRecord.cs
    type: D
  - path: Source/PersonalChronicle/Domain/Grid/GridProtectionEvaluator.cs
    type: D
  - path: Source/PersonalChronicle/Domain/Grid/PriorityPlanner.cs
    type: D
  - path: Source/PersonalChronicle/Domain/Grid/ProtectionSetting.cs
    type: D
  - path: Source/PersonalChronicle/Domain/Grid/ShedReason.cs
    type: D
  - path: Source/PersonalChronicle/Domain/Grid/TripReason.cs
    type: D
  - path: Source/PersonalChronicle/Domain/PowerPriorityDef.cs
    type: D
  # ── D · 源文件：Application（5）──
  - path: Source/PersonalChronicle/Application/SystemCenters/GridSimAccess.cs
    type: D
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinPowerGridProvider.cs
    type: D
  - path: Source/PersonalChronicle/Application/SystemCenters/GridServices/DispatchService.cs
    type: D
  - path: Source/PersonalChronicle/Application/SystemCenters/GridServices/ProtectionService.cs
    type: D
  - path: Source/PersonalChronicle/Application/SystemCenters/GridServices/StandbyService.cs
    type: D
  # ── D · 源文件：Automation（8）──
  - path: Source/PersonalChronicle/Automation/Building_PowerCircuitBreaker.cs
    type: D
  - path: Source/PersonalChronicle/Automation/Building_PowerIsolator.cs
    type: D
  - path: Source/PersonalChronicle/Automation/CompPowerCircuitBreaker.cs
    type: D
  - path: Source/PersonalChronicle/Automation/GridTickDriver.cs
    type: D
  - path: Source/PersonalChronicle/Automation/MapComponent_PowerGridSim.cs
    type: D
  - path: Source/PersonalChronicle/Automation/PowerGridAccessBinder.cs
    type: D
  - path: Source/PersonalChronicle/Automation/PowerNetEnergyWriter.cs
    type: D
  - path: Source/PersonalChronicle/Automation/PowerNetTickHandler.cs
    type: D
  # ── D · 源文件：Capture / Compatibility / MasterControl（7）──
  - path: Source/PersonalChronicle/Capture/Patch_PowerNetTick.cs
    type: D
  - path: Source/PersonalChronicle/Capture/Patch_PowerIncidentCulprit.cs
    type: D
  - path: Source/PersonalChronicle/Compatibility/PowerMods/PowerModAdapter.cs
    type: D
  - path: Source/PersonalChronicle/Compatibility/PowerMods/PowerModDetector.cs
    type: D
  - path: Source/PersonalChronicle/Compatibility/PowerMods/PowerModDiagnostics.cs
    type: D
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.PowerGrid.cs
    type: D
  # ── D · 测试（10）──
  - path: Tests/GridMeterRingTests.cs
    type: D
  - path: Tests/PowerGridCulpritTests.cs
    type: D
  - path: Tests/PowerGridDispatchTests.cs
    type: D
  - path: Tests/PowerGridMeteringTests.cs
    type: D
  - path: Tests/PowerGridPartitionTests.cs
    type: D
  - path: Tests/PowerGridPriorityTests.cs
    type: D
  - path: Tests/PowerGridProtectionTests.cs
    type: D
  - path: Tests/PowerGridScribeTests.cs
    type: D
  - path: Tests/PowerGridServiceTests.cs
    type: D
  - path: Tests/PowerModDetectTests.cs
    type: D
  # ── D · Def / 语言 / 贴图（17）──
  - path: Defs/PowerGrid_Protection.xml
    type: D
  - path: Defs/PowerGrid_Priority.xml
    type: D
  - path: Languages/English/DefInjected/ThingDef/PowerGrid_Protection.xml
    type: D
  - path: Languages/ChineseSimplified/Keyed/PowerGrid.xml
    type: D
  - path: Languages/English/Keyed/PowerGrid.xml
    type: D
  - path: Textures/Things/Building/PC_PowerCircuitBreaker_north.png
    type: D
  - path: Textures/Things/Building/PC_PowerCircuitBreaker_south.png
    type: D
  - path: Textures/Things/Building/PC_PowerCircuitBreaker_east.png
    type: D
  - path: Textures/Things/Building/PC_PowerCircuitBreaker_west.png
    type: D
  - path: Textures/Things/Building/PC_PowerIsolator_north.png
    type: D
  - path: Textures/Things/Building/PC_PowerIsolator_south.png
    type: D
  - path: Textures/Things/Building/PC_PowerIsolator_east.png
    type: D
  - path: Textures/Things/Building/PC_PowerIsolator_west.png
    type: D
  - path: Textures/Things/Building/PC_PowerIsolator_Off_north.png
    type: D
  - path: Textures/Things/Building/PC_PowerIsolator_Off_south.png
    type: D
  - path: Textures/Things/Building/PC_PowerIsolator_Off_east.png
    type: D
  - path: Textures/Things/Building/PC_PowerIsolator_Off_west.png
    type: D
  # ── M · 接线点摘除（9 源 + 4 XML）──
  - path: Source/PersonalChronicle/Domain/SystemCenterIds.cs
    type: M
  - path: Source/PersonalChronicle/Application/SystemCenters/SystemCenterDataRegistry.cs
    type: M
  - path: Source/PersonalChronicle/Application/ReadModels/SystemCenterReadModelProvider.cs
    type: M
  - path: Source/PersonalChronicle/Application/ReadModels/SystemCenterSnapshots.cs
    type: M
  - path: Source/PersonalChronicle/ChronicleSettings.cs
    type: M
  - path: Source/PersonalChronicle/Settings/SettingsCatalog.cs
    type: M
  - path: Source/PersonalChronicle/Settings/SettingsDrawer.cs
    type: M
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Detail.cs
    type: M
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Chrome.cs
    type: M
  - path: Defs/SystemCenterDefs.xml
    type: M
  - path: Languages/English/DefInjected/SystemCenterDef/SystemCenterDefs.xml
    type: M
  - path: Languages/English/Keyed/SettingsUi.xml
    type: M
  - path: Languages/ChineseSimplified/Keyed/SettingsUi.xml
    type: M
  # ── M · 治理同步（12）──
  - path: docs/设计文档/架构/EXC-2026-027.md
    type: M
  - path: docs/设计文档/架构/EXC-2026-001.md
    type: M
  - path: docs/设计文档/架构/ADR-GRID-1-电网子系统扩展点选型.md
    type: M
  - path: docs/设计文档/功能模块/自动化体系规范/03-电网系统/收尾报告_电网SDD实装_2026-10-07.md
    type: M
  - path: docs/设计文档/功能模块/自动化体系规范/03-电网系统/SDD-2026-10-06-电网系统.md
    type: M
  - path: docs/设计文档/功能模块/自动化体系规范/03-电网系统/REQ_电网系统.md
    type: M
  - path: docs/设计文档/功能模块/自动化体系规范/03-电网系统/AUTO-2026-10-06-电网系统设计方案.md
    type: M
  - path: docs/设计文档/功能模块/自动化体系规范/03-电网系统/AUTO-2026-10-06-电网系统开发管线.md
    type: M
  - path: docs/设计文档/功能模块/自动化体系规范/03-电网系统/SDD-2026-10-07-增补-历史采样通道.md
    type: M
  - path: docs/README.md
    type: M
  - path: docs/设计文档/功能模块/自动化体系规范/README.md
    type: M
  - path: docs/设计文档/功能模块/资产规范/README.md
    type: M
  # ── M · 编译生成物（1）──
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# 变更标注 · CHG-20261007-GRID-REMOVE（电网子系统整体净删）

**规则**：ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**裁决**：PM 验收裁决（2026-10-07）**D 档** —— 电网子系统「效果非常一般」⇒ **整体净删，一步到位、不做空壳过渡**（PM 明确）。
**覆盖**：2026-10-07 实装的 15 个 GRID ChangeID（GRIDP1 / GRIDP2R / GRIDP3 / GRIDP3B~GRIDP3M / GRIDP3N）之全部**实装内容**。

## 1. 删除范围（57 文件）
- **源文件 30**：Domain/Grid 10 + PowerPriorityDef 1 + Application 5（GridSimAccess / BuiltinPowerGridProvider / GridServices×3）+ Automation 8 + Capture 2 + Compatibility/PowerMods 3 + MasterControl 1；
- **测试 10**（含 Scribe 往返类）；
- **Def 2**（PowerGrid_Protection / PowerGrid_Priority）+ **语言 3**（Keyed/PowerGrid ×2 · DefInjected/ThingDef ×1）；
- **贴图 12**（断路器 4 + 隔离开关 4 + _Off 4）。

## 2. 接线点摘除（14 处 / 13 文件）
1. `Domain/SystemCenterIds.cs`：删 `Power = "power_center"` 常量（35-40）；
2. `SystemCenterDataRegistry.cs`：删 `BuiltinPowerGridProvider` 注册（143-147）；
3. `SystemCenterReadModelProvider.cs`：删电力分流块（286-305，含 `SystemCenterIds.Power` 引用）；
4. `SystemCenterSnapshots.cs`：删 `PowerGridRows` / `PowerGridBreakers` / `PowerGridStatus` 三字段（250-276）；
5. `ChronicleSettings.cs`：删 `GridTakeOverEnabled` 字段 + Scribe 行（27-28 / 172）；
6. `SettingsCatalog.cs`：删 `SettingId.GridTakeOver = 43` + SettingDef 行（146-147 / 313-315）；
7. `SettingsDrawer.cs`：删两 case 分支（606 / 625）；
8. `MasterControlTabWindow.Detail.cs`：删电力页调用块（255-276）；
9. `MasterControlTabWindow.Chrome.cs`：清理 0913c 历史注释（113-118，纯注释）；
10. `Defs/SystemCenterDefs.xml`：删 power_center 块（336-376）；
11. `Languages/English/DefInjected/SystemCenterDef/SystemCenterDefs.xml`：删 9 键（86-94）；
12. `Languages/{ChineseSimplified,English}/Keyed/SettingsUi.xml`：删 GridTakeOver 各 3 键（95-97 / 93-95）；
13. `_scripts/compile.rsp`：**由脚本自动重生成**（compile_with_sdk.ps1 枚举现存 .cs）——电力相关仅剩 `PowerGridReading.cs`（自动生成物走范围外清单）；
14. Harmony：两补丁类（#23 / #24）**随文件删除自然退出**（`PatchAll` 反射扫描）——无显式清单需摘（EXC-2026-027 同步注销）。

## 3. 保留边界（唯一豁免 · 已逐行核实）
`Source/PersonalChronicle/Domain/PowerGridReading.cs` **保留** —— 制造中心 3 个 Provider 直接引用（`BuiltinManufacturingProvider.DataLayer.cs:258-259` / `.Chrome.cs:111-113` / `.Bridge.cs:154-155`）。
经核实：该类**完全自包含**（仅依赖原版 `PowerNet` / `CompPower` / `CompPowerBattery`），**不依赖任何将删的 Grid 类型** ⇒ 无需就地改造（预设的"内联/最小改造"条件不触发）。

## 4. 旧档影响（PM 已确认接受；工程红线验证）
- 旧档已放置的 `PC_PowerCircuitBreaker` / `PC_PowerIsolator` 建筑：Def 删除 ⇒ 载入时 Log.Error 红字 + 该物消失（**不 crash**）；
- `MapComponent_PowerGridSim`（MapComponent）：类型删除 ⇒ 旧档节点被丢弃（日志红字，**不 crash**）；
- **结论：不 crash、可正常进入地图**（引擎对"缺失 Def / 缺失类型 / 缺失 MapComponent"均为 Log.Error 后跳过，非硬失败）。
- ⚠ **发布区残留警示**：游戏 Mod 目录存在 **17 个已删文件残留**（Def 2 / 语言 3 / 贴图 12）——其中残留 Def 指向已删 C# 类（红字放大）⇒ 必须先执行清理与部署再启动游戏（见 §7.1）。

## 5. 验证证据（全部实跑；判据以输出为准）
| 项 | 判据 | 结果 | 证据 |
|---|---|---|---|
| 编译 | 输出无 `error CS` | **0 错 0 警** | `.codebuddy/tmp/grid-removal-build.log`（BuildStamp 23:37 同步） |
| 残留扫描 | 全仓 6 pattern | 除豁免面（PowerGridReading + 制造中心 3 Provider）与农业 `PC_PowerModule` 外**无残留** | 扫描输出（12 处命中全部为豁免/农业） |
| 单元测试 | dotnet test（引用新 DLL） | **548 通过 / 0 失败 / 1 跳过**（基线 670 → 549；−121 = 被删 10 个电网测试类用例总数） | `.codebuddy/tmp/grid-removal-test.log` |
| keyref 审计 | EXIT=0 | **0 裸键风险** | `audit-keyref-integrity.ps1` 实跑 |
| definject 审计 | EXIT=0 | **0 孤儿注入** | `audit-definject-orphans.ps1` 实跑 |
| 部署 | `deploy-to-game.ps1` | ⏳ **待执行（需审批）** | 见 §7.1 |

## 6. 范围外说明
- 本仓存在大量历史未提交工作（约 1500 项）⇒ 必须 `-ExcludeFromFile`（清单生成：`git 改动集 − 本标注 files`，脚本 `.codebuddy/tmp/gen-grid-remove-scope.ps1`）；
- 自动生成物 `_scripts/compile.rsp` 按上批惯例进范围外（每次编译重写）——其当前内容已同步为"仅保留 PowerGridReading"；
- `Source/PersonalChronicle/BuildStamp.g.cs`（每次编译重写）**列入本标注（type: M）**——遵循 `CHG-20261007-GRIDP1` 实测教训（`.cs` 生成物不列会判"标注遗漏实际改动文件"）；
- `Assemblies/PersonalChronicle.dll` 不入清单（gate-runner 自动跳过 `.dll`）；
- ⚠ 并行会话观察：`docs/README.md` 在会话期间被并行修改过（非本批）；本批编辑已基于其最新状态，其当前全部 diff 由本标注覆盖。

## 7. 未闭环 / 风险
1. **发布区清理与部署（待审批）**：`E:\SteamLibrary\steamapps\common\RimWorld\Mods\殖民地综合管理系统` 下 17 个已删文件残留（Defs 2 / Languages 3 / Textures 12）。执行方式：备份 → 清理 17 项（清单已落盘 `.codebuddy/tmp/grid-removal-target-cleanup-list.txt`）→ 运行 `deploy-to-game.ps1` → 复扫；完整脚本随交付报告提供（本环境审批通道在本时段不可用，未自动执行）。
2. **在 §7.1 完成并确认前请勿启动游戏**：残留 Def 指向已删类（红字；不 crash 但不属预期状态）。
3. 完成后正常启动旧档的预期表现 = §4（丢物 + 红字、可进图）。
4. 历史记录类电网引用（`交付报告_CHG-20261007-GRIDP1.md`、`审计计划_SDD电网实装_2026-10-07.md`、资产 04/05 批文档、根目录 GRIDP* 变更标注）——按"历史记录不删不标"处理；本轮仅同步治理链上活跃的 EXC / ADR / SDD 目录与索引（含两个子目录 README）。
5. **3 个空目录壳待清理**（`Source/PersonalChronicle/Domain/Grid` / `Application/SystemCenters/GridServices` / `Compatibility/PowerMods`）：文件已全删，目录壳的删除受本环境审批通道超时阻断 ⇒ 随 §7.1 部署批一并清理。

## 8. 回滚
- 本批删除/编辑均在**未提交工作区**（相关文件多为 untracked，无 git 历史可回）⇒ 回滚依赖：① 发布区备份（`_backups\release-deploy\deploy_<ts>\` 与 `_backups\grid-removal-deploy-cleanup_20261007\`）；② 最近 dll 备份（`Assemblies\PersonalChronicle.dll.bak_20261007233738`）；
- 逆向重建：按 GRIDP1~GRIDP3N 各变更标注的 `files:` 清单可逐文件恢复源码文本（标注即恢复图）。