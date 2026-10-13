---
change_id: CHG-SHOP-AUDIT-001
version: v1.4.x
date: 2026-09-21
author: AI
scope: [Source/PersonalChronicle/Application, Source/PersonalChronicle/Domain, Source/PersonalChronicle/Automation, Source/PersonalChronicle/Effects, Source/PersonalChronicle/Capture, Source/PersonalChronicle/Manufacturing, Source/PersonalChronicle/MasterControl, Tests, Languages, Assemblies, docs]
files:
  # ---- 新增：契约层 / 服务层 / 纯规则（A-1 A-2 A-6）----
  - path: Source/PersonalChronicle/Application/Effects/WorkshopBonusData.cs
    type: A
    symbols: WorkshopEffectiveBonus / WorkshopEffectiveBonus.Neutral / WorkshopProductBonus（自 Automation 上移）
  - path: Source/PersonalChronicle/Application/Effects/WorkshopBonusAccess.cs
    type: A
    symbols: WorkshopBonusAccess.SetSources / WorkingBonusOf / WorkingTypeLabelOf / TryGetProductBonus / RegisterProductBonus
  - path: Source/PersonalChronicle/Application/EquipmentStateText.cs
    type: A
    symbols: EquipmentStateText.TranslationKeyOf / FloorStatePrefix / AutomationTabPrefix
  - path: Source/PersonalChronicle/Application/Workshop/WorkshopProductionEffects.cs
    type: A
    symbols: WorkshopProductionEffects.Apply / ApplyQualityBias / ApplyProductBonuses / SpawnExtraProduct / RefundIngredient
  - path: Source/PersonalChronicle/Domain/Workshop/WorkshopProductionRules.cs
    type: A
    symbols: WorkshopProductionRules.ShouldRegisterProductBonus / ShouldSpawnExtraProduct / ShouldRefundIngredient / SalvageQualityTargetIfTriggered
  - path: Source/PersonalChronicle/Automation/WorkshopBonusAccessBinder.cs
    type: A
    symbols: WorkshopBonusAccessBinder（StaticConstructorOnStartup）/ EffectiveBonusOf / WorkingTypeLabelOf / ProductBonusOf / MarkProductBonus
  # ---- 新增：ARC-013 partial 切片（零契约改动）----
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.Schedule.cs
    type: A
    symbols: MapComponent_WorkshopManager（partial；EnsureDutyPlan..PruneScheduleHistory 区块原样迁移）
  - path: Source/PersonalChronicle/Automation/WorkshopFloorViewProvider.Detail.Tabs.cs
    type: A
    symbols: WorkshopFloorViewProvider（partial；BuildBillQueueTab..AddRow 区块原样迁移）
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Schedule.cs
    type: A
    symbols: WorkshopManageView（partial；DrawSchedule..ModeLabel 区块原样迁移）
  - path: Source/PersonalChronicle/Manufacturing/UI/FloorDeviceDetailWindow.Draw.cs
    type: A
    symbols: FloorDeviceDetailWindow（partial；DrawTimeline..DrawDisabledNavButton 区块原样迁移）
  - path: Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.Palette.cs
    type: A
    symbols: WorkshopLayoutCanvas（partial；CollectPalette..BuildMemberInitials 区块原样迁移）
  - path: Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.Device.cs
    type: A
    symbols: WorkshopLayoutCanvas（partial；DeviceGridPos..DrawMetricsRow 区块原样迁移）
  # ---- 修改：A-1 消费点改线 + 死 using ----
  - path: Source/PersonalChronicle/Effects/StatPart_WorkshopWorkSpeed.cs
    type: M
    symbols: StatPart_WorkshopWorkSpeed.TransformValue / ExplanationPart（改走 WorkshopBonusAccess）
  - path: Source/PersonalChronicle/Effects/StatPart_WorkshopResearchSpeed.cs
    type: M
    symbols: StatPart_WorkshopResearchSpeed.TransformValue / ExplanationPart（同上）
  - path: Source/PersonalChronicle/Effects/StatPart_WorkshopProduct.cs
    type: M
    symbols: StatPart_WorkshopMaxHp / StatPart_WorkshopAccuracy（TryGetProductBonus 改走契约）
  - path: Source/PersonalChronicle/Effects/WorkshopStatPartInjector.cs
    type: M
    symbols: WorkshopStatPartInjector.InjectedStats（ARC-008 声明；删 Automation 死 using）
  # ---- 修改：A-2 业务下沉（Patch 瘦身）----
  - path: Source/PersonalChronicle/Capture/Patch_GenRecipe.cs
    type: M
    symbols: Patch_GenRecipe.Postfix（只留守卫 + 转调；业务下沉）；5 个业务方法移除
  # ---- 修改：A-1 数据契约上移的连带 ----
  - path: Source/PersonalChronicle/Automation/WorkshopBonusResolver.cs
    type: M
    symbols: WorkshopBonusResolver（struct 上移；using）
  - path: Source/PersonalChronicle/Automation/WorkshopProductRegistry.cs
    type: M
    symbols: WorkshopProductRegistry（struct 上移；using）
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.cs
    type: M
    symbols: MapComponent_WorkshopManager（using；切片后加 partial）
  # ---- 修改：A-4 会话态守卫 / A-6 键面归位 / P3 键 ----
  - path: Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.cs
    type: M
    symbols: WorkshopLayoutCanvas.EnsureSessionScope / ClearSession / DrawCanvasBody（入口守卫）；DetachedList 区块迁出
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.cs
    type: M
    symbols: MasterControlTabWindow.PreClose（关窗清会话态）
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Dev.cs
    type: M
    symbols: 设备状态取词改走 EquipmentStateText
  - path: Source/PersonalChronicle/Domain/EquipmentState.cs
    type: M
    symbols: EquipmentStateResolver.TranslationKeyOf（上移至 Application；原位置留迁移注释）
  - path: Source/PersonalChronicle/Domain/WorkshopDeviceReadings.cs
    type: M
    symbols: 注释 cref 同步（EquipmentStateText）
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView（取词改线；排班区块迁出；partial）
  - path: Source/PersonalChronicle/Automation/WorkshopHandoverBoard.cs
    type: M
    symbols: 设备状态取词改走 EquipmentStateText
  - path: Source/PersonalChronicle/Automation/WorkshopFloorViewProvider.cs
    type: M
    symbols: 设备状态取词改走 EquipmentStateText
  - path: Source/PersonalChronicle/Automation/WorkshopFloorViewProvider.Detail.cs
    type: M
    symbols: 设备状态取词改线；页签区块迁出
  - path: Source/PersonalChronicle/Automation/WorkshopDeviceStateResolver.cs
    type: M
    symbols: 注释 cref 同步
  - path: Source/PersonalChronicle/Automation/IWorkshopDevice.cs
    type: M
    symbols: 注释 cref 同步
  - path: Source/PersonalChronicle/Automation/Zone_Workshop.cs
    type: M
    symbols: Zone_Workshop.nextColorIndex（ARC-008 声明）
  - path: Source/PersonalChronicle/Automation/Dialog_LogisticsManage.cs
    type: M
    symbols: Dialog_LogisticsManage.T1 / 阈值与提示两处 Keyed 化
  - path: Source/PersonalChronicle/Manufacturing/UI/FloorDeviceDetailWindow.cs
    type: M
    symbols: FloorDeviceDetailWindow（切片后加 partial）
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinManufacturingProvider.Events.cs
    type: M
    symbols: 设备状态取词改走 EquipmentStateText
  # ---- 测试 ----
  - path: Tests/WorkshopProductionRulesTests.cs
    type: A
    symbols: WorkshopProductionRulesTests（10 条）
  - path: Tests/WorkshopProductRegistryTests.cs
    type: M
    symbols: using（WorkshopProductBonus 类型迁移）
  - path: Tests/EquipmentStateResolverTests.cs
    type: M
    symbols: TranslationKeyOf 调用改走 EquipmentStateText
  # ---- 本地化 ----
  - path: Languages/English/DefInjected/WorkshopTypeDef/WorkshopTypes.xml
    type: A
    symbols: WT_* ×15 label/description（EN 注入，修复 L-1）
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: PersonalChronicle.Automation.Port.UnitPieces
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: PersonalChronicle.Automation.Port.UnitPieces
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: PersonalChronicle.UI.Floor.Detail.Crew.TimetableNote
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: PersonalChronicle.UI.Floor.Detail.Crew.TimetableNote
  # ---- 产物 / 文档 ----
  # ⚠ Assemblies/PersonalChronicle.dll 已随本批 Rebuild（0 错 0 警），但不列入标注：
  #   gate-runner 的生成物过滤（.dll/.pdb/.log/bin/obj）不对其做 missing/phantom 判定。
  - path: docs/设计文档/架构/审计-车间体系-2026-09-21.md
    type: A
    symbols: 审计报告（本批次依据）
  - path: docs/设计文档/架构/EXC-2026-010.md
    type: A
    symbols: Automation 目录混放定向豁免（A-3 处置）
  - path: docs/设计文档/架构/EXC-2026-003.md
    type: M
    symbols: 备注同步（PurgeDeadReferences 判死项已闭环）
  - path: docs/设计文档/架构/EXC-2026-005.md
    type: M
    symbols: 备注同步（同上）
  - path: docs/设计文档/架构/Change-Annotation-CHG-SHOP-AUDIT-001.md
    type: A
    symbols: 本变更标注（自指）
  - path: docs/changelog/v1.4.x_2026-09-21_车间体系审计修复.md
    type: A
    symbols: Changelog
---

# 变更标注 · CHG-SHOP-AUDIT-001（车间体系审计修复）

> **机器校验**：`gate-runner.ps1 -Base HEAD -Annotation docs/设计文档/架构/Change-Annotation-CHG-SHOP-AUDIT-001.md -ExcludeFromFile <范围外清单> -Blocking`
> 上方 YAML front matter 的 `files:` 是唯一被解析的清单；本正文仅人读。

## §1 目标

按 2026-09-21《车间体系严格审计》结论一次性修复：**P1×2**（跨层直读、Patch 业务未下沉）+ **P2×3**（EN 注入缺失、编排画布会话态、Automation 目录混放）+ **P3 全清单**（缺键、硬编码、键面归位、ARC-008 声明、ARC-013 切片、EXC 备注同步）。

## §2 关键设计决策

1. **A-1 契约层选型**：契约与两个数据快照放 `Application/Effects/`（与既有 `WorkshopRatingShift` 同范式），而非 Domain —— 理由是工程已有 Automation → Application 的绑定器惯例（`WorkshopRatingShiftBinder`），零新增依赖边；Effects 消费契约 = `domain → effects` 之外的 `Application → Effects` 方向在本工程既有且被接受（`ProfessionalEffectService` 也在 Application）。
2. **A-2 下沉目标层**：`Application/Workshop/`（`ProfessionalEffectService` 同层先例），Patch 只转调 —— 与 `开发铁律.md:123`「Capture 只做钩子 → 调 Application」逐字一致。
3. **A-3 处置**：登记豁免（EXC-2026-010）而非物理切片 —— 切片会新增两条命名空间级边且偏离原版 `Zone.GetGizmos` 惯例（详见例外文档「Alternatives rejected」）。
4. **A-5 切片纪律**：只做行块搬运 + 类声明加 `partial`，**未改**任何方法名/签名/契约/Save schema/翻译键。

## §3 行为等价性（可复核）

| 项 | 旧 | 新 | 判据 |
|---|---|---|---|
| 额外产出 | `yieldFactor>1.0001f && Rand.Chance(yieldFactor-1f)` | `ShouldSpawnExtraProduct(yieldFactor, Rand.Value)` | 逐位同式（`roll < p`） |
| 主料返还 | `stuffCostFactor>0 && <0.999f && Rand.Chance(1-stuff)` | `ShouldRefundIngredient(stuff, Rand.Value)` | 同上 |
| 劣质回炉 | Awful/Poor && `Rand.Chance(goodRate-1f)` → Normal | `SalvageQualityTargetIfTriggered` | 同上 + 枚举顺序断言 |
| 加成登记 | `maxHp>1.0001f \|\| acc>1.0001f` | `ShouldRegisterProductBonus` | 同式 |
| 取值链 | `manager.WorkingBonusOf(pawn)` 直读 | 契约 → binder → 同方法 | 委托体逐字相同 |

## §4 已知未执行项（不得折算为 PASS）

- 实机存读档矩阵 / UI 双语双视口目视 / PerfProbe 采样 / 第三方设备组合矩阵 —— 无运行环境。
- GATE-PERF 维持 BLOCKED（证据缺失）。

## §5 范围外说明

工作树含**经济中心批次（CHG-LCE-003 / RUIF 整改）**等未提交改动，与本批无关；
GATE-DELTA 用 `-ExcludeFromFile` 声明范围外清单（生成方法见 changelog 同批记录）。
