---
change_id: CHG-KPI-SHOP-001
version: v1.5.0
date: 2026-09-22
author: AI
scope: [Source/PersonalChronicle/Application, Source/PersonalChronicle/Automation, Source/PersonalChronicle/MasterControl, Languages, 系统中心蓝图, docs]
files:
  # ---- 新增：车间运行摘要契约（人时/在岗/成员/排班覆盖）----
  - path: Source/PersonalChronicle/Application/Workshop/WorkshopOpsAccess.cs
    type: A
    symbols: WorkshopOpsDigest / WorkshopOpsAccess.SetSource / WorkshopOpsAccess.Digest
  - path: Source/PersonalChronicle/Automation/WorkshopOpsAccessBinder.cs
    type: A
    symbols: WorkshopOpsAccessBinder（StaticConstructorOnStartup）/ BuildDigest / AddFixedSlots / AddShifts / AddRotations
  # ---- 修改：KPI 口径重拟（移除 4 项 + 新 9 卡）----
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinManufacturingProvider.cs
    type: M
    symbols: KpiKeys（9 新键）/ AppendKpisCore / CollectProducedStats / CollectProducedQuality（兼容包装）/ BonusUplift / AddTextCard / CollectEquipment（保留）
  - path: Source/PersonalChronicle/Application/SystemCenters/SystemCenterKpiFactory.cs
    type: M
    symbols: SystemCenterKpiFactory.Text（+note 重载，数字卡）
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.cs
    type: M
    symbols: MapComponent_WorkshopManager.ActiveWorkerCount（只读计数属性）
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.AppShell.cs
    type: M
    symbols: DrawAppKpiCard（表盘让出副标带 + 卡面常显口径副标）/ DrawKpiDial（+drawCard 参数）
  # ---- 本地化（删 4 组虚假指标键 + 新 9 卡键）----
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: Kpi.Availability / Utilization / OutputEfficiency / ManHours / Unit.Hours / RosterCoverage / OnDuty / QualityYield / BonusUplift（+Note/NoRecord；删 Oee.* / CapacityUtilization / QualityRate / QueueLoad / SupervisionBandwidth）
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: 同上（英文）
  # ---- 预览（1:1 权威源同步）----
  - path: 系统中心蓝图/待落地项/制造系统中心预览_RUIF单页统一版.html
    type: M
    symbols: "#kpi 元素文本（9 卡）/ METRICS 模拟量 / updateGauges / updateOEE / renderKpiLine 悬停汇总"
  # ---- 文档 ----
  - path: 系统中心蓝图/待落地项/制造中心核心指标重拟方案_2026-09-22.md
    type: A
    symbols: 指标重拟方案（用户「要求一」）
  - path: docs/设计文档/架构/审计-制造中心总控KPI与广播-2026-09-22.md
    type: A
    symbols: 审计报告（本批依据）
  - path: docs/设计文档/架构/Change-Annotation-CHG-KPI-SHOP-001.md
    type: A
    symbols: 本变更标注（自指）
  - path: docs/changelog/v1.4.x_2026-09-22_制造中心核心指标重拟.md
    type: A
    symbols: Changelog
---

# 变更标注 · CHG-KPI-SHOP-001（制造中心核心指标重拟）

> **机器校验**：`gate-runner.ps1 -Base HEAD -Annotation <本文件> -ExcludeFromFile .codebuddy/tmp/kpi-shop-out-of-scope.txt -Blocking`
> 前序批次 `CHG-SHOP-AUDIT-001`（车间体系审计修复，49 文件）已单独 GATE-DELTA PASS，其文件在本批按**范围外**声明（见范围外清单）。

## §1 目标（用户「要求一」）

「虚假无法实现的指标移除，然后重新拟定基于车间体系，面向工业制造方向的核心指标。」⇒ 已按推荐 4 点全量落地：
① 9 格清单定稿 ② 口径副标**卡面常显** ③ 预览 HTML 同步 ④ 窗口 = 近 15 日（一个 quadrum）。

## §2 移除（配平）

| 移除项 | 理由 | 证据 |
|---|---|---|
| 性能率 | 真口径「实际节拍 ÷ 额定节拍」原版无数据源（Bill 无单张进度） | 审计 §1 |
| 综合设备效率 OEE | `A×P×Q` 依赖性能率 ⇒ 指代错 | 审计 §1 |
| 监管带宽 | 自研监管机制从未落地（长期 Pending） | 审计 §0 |
| 第二个「良品率」 | 与 OEE-Q 同值同标签，零增量 | 审计 §2 |

## §3 新 9 卡（口径 → 数据源）

| # | 卡 | 口径 | 数据源 |
|---|---|---|---|
| 1 | 设备可用率 | 非故障 ÷ 总设备 | `CollectEquipment`（原版 comp） |
| 2 | 在产设备率 | （通电 ∧ 有活）÷ 总设备 | 同上 |
| 3 | 产出效率 | 近 15 日产出 ÷ 近 15 日人时（件/人时，**数字卡**） | `CollectProducedStats` 窗口计数 + `WorkshopOpsAccess.ManHourTicks` |
| 4 | 车间人时 | 近 15 日考勤人时（副标加班%）**数字卡** | `WorkshopOpsAccess`（`Zone_Workshop.Attendance`） |
| 5 | 排班覆盖率 | （24 − 无人值守小时）÷ 24 | `WorkshopOpsAccess`（`WorkshopRosterCoverage` 三类排班并集） |
| 6 | 在岗人数 | 在岗 ÷ 编内成员（去重） | `WorkshopOpsAccess`（`ActiveWorkerCount` + `Members`） |
| 7 | 良品率 | 良好及以上 ÷ 有品质产出（累计） | `CollectProducedStats`（职业 ledger） |
| 8 | 加成效力 | 在车间人员平均工时降幅（副标速度 +X%） | `WorkshopBonusAccess.WorkingBonusOf/WorkingTypeLabelOf` |
| 9 | 平均心情 | 殖民者心情均值 | `Need_Mood`（保留） |

## §4 架构

- **新增契约** `WorkshopOpsAccess`（Application 层受控静态钩子 + `WorkshopOpsDigest` 数据快照）+
  `WorkshopOpsAccessBinder`（Automation 侧 `[StaticConstructorOnStartup]` 注入）——
  与 `WorkshopBonusAccess` 同范式；Application **零** Automation 直读（A-1 规范延续）。
- 排班覆盖构造口径**照抄** `WorkshopScheduleChart.BuildRoster`（固定班 1 人 / 班组 `RequiredCount`（0→池规模）/ 轮岗 1 人），
  覆盖度计算复用 `WorkshopRosterCoverage` 唯一实现。
- 卡面副标：表盘路径让出底部一行（Tiny 行高 18）⇒ RUIF `Gauge` 与 PC 回落 `DrawKpiDial` 两路一致；
  卡背景改由调用方画满整卡（`drawCard` 参数）避免缩 rect 缺底边。

## §5 数据面 / 存档

- **零 SAVE 变更**：不新增持久化字段（`Attendance` / `ActiveWorkerCount` 均为既有/运行态）。
- 无新采集：全部读既有台账（考勤 / 排班 / 产出 ledger / 加成契约）。

## §6 已知取舍（如实登记）

- 产出效率窗口受考勤台账 `RetainDays = 15` 限制（更长窗口无数据）⇒ 显示口径写明"近 15 日"。
- 「产出效率 / 车间人时」为**数字卡**（非 0..1 比率）⇒ 不走表盘，避免 `Share01` Clamp01 截断（形态混合属有意，预览 `#kpi` 亦是 4+5 混合）。
- 加成效力仅统计**在车间且实际吃到工时加成**的殖民者（无样本 → 未接入文案）。
- 设备详情页 `Floor.Detail.Metric.Oee`（单台 OEE）与分析页 `Analysis.Loss.Title`（OEE 六大损失）**不在本批范围**，登记为后续观察项。

## §7 未执行

- 实机目视（9 格读数与游戏状态一致性 / 副标在 62px 卡内的可读性 / 表盘半径缩小后的观感）—— 无运行环境。
