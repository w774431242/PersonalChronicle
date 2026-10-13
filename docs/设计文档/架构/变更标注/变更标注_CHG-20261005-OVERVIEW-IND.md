---
change_id: CHG-20261005-OVERVIEW-IND
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle/Automation, Languages]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Overview.cs
    type: A
    symbols: WorkshopManageView.BuildOverviewReadings, WorkshopManageView.DrawOverviewIndustrial, WorkshopManageView.DrawGaugeCard, WorkshopManageView.DrawLedSection, WorkshopManageView.DrawFitSection, WorkshopManageView.LedCodeOf, WorkshopManageView.LedSectionHeight
    lines: 1-300
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView.DrawOverview, WorkshopManageView.Refresh
    lines: 417-437, 1120-1132
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: PersonalChronicle.Automation.Tab.GaugeOccupancy, GaugeDeviceRun, GaugeDeviceManned, LedTitle, LedIdle, LedRun, LedWarn, LedFault, LedNoDevice, FitMissingNames
    lines: 462-480
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: PersonalChronicle.Automation.Tab.GaugeOccupancy, GaugeDeviceRun, GaugeDeviceManned, LedTitle, LedIdle, LedRun, LedWarn, LedFault, LedNoDevice, FitMissingNames
    lines: 461-475
---

# 变更标注 · CHG-20261005-OVERVIEW-IND

## Change Summary

玩家批准（2026-10-05）后实施**概览页工业化改造**。

> 玩家口径：「这是**单个车间区**的概览，所以不需要太宏观，以**工业化、数字化、智能化**视角去优化」

⇒ 该页改为该车间的 **HMI 面板**，回答四个工厂级问题：跑得多满 / 哪台在跑 / 有没有人盯着 / 还差什么设备。

## 改造前的问题（玩家截图实证）

| # | 问题 |
|---|---|
| 1 | **信息重复**：窗口标题已写「格数/可用空地/设备」，下方三张大卡重复同一组数 |
| 2 | **60% 空白**：概览只占约 40% 高度 |
| 3 | `22 / 198` **读不懂**：无标签、无百分比、无口径说明 |
| 4 | 匹配度只有一行计数 —— 而**缺失设备名早已算好**（`stats.MissingDeviceLabels`）却没露出来 |
| 5 | 看不到设备**逐台**状态（哪台在跑、哪台缺料、哪台故障） |

## 改造后（批次 A：仪表台 + 灯阵 + 匹配）

```
┌ ① 仪表台 Gauge ×3 ──────────────────────────────────────┐
│   空间占用 ●      设备运转率 ●      设备有人率 ●          │
│   占用/总格        运转台/总台        有人操作台/总台      │
├ ② 设备状态灯阵 LedMatrix ──────┬ 图例（4 色 + 计数）─────┤
│   ■■■□■□■  逐台一个灯           │ 停机 N / 运转 N / …    │
├ ③ 设备匹配 ─────────────────────────────────────────────┤
│   匹配 N · 多余 N                                        │
│   缺少：A / B（**单独成行**，橙色）                       │
└ ④ 教育性提示：仅"本区无设备"时出现（不再常驻噪音）───────┘
```

**用 RUIF 工业组件族的真实契约**（Cecil + 源码取证，非凭签名猜）：

| 组件 | 取证到的关键契约 |
|---|---|
| `RUIF.Industrial.Gauge(r, v, lo, hi, label, warn01, dead01)` | **弧形表盘**（弧+刻度+指针）；轴心 `r.yMax-6`、半径 `min(w*0.42, 高度余量)` ⇒ **高度不足静默不画**（源码 `radius > 2f`）⇒ 本页给 `GaugeH = 84` |
| `RUIF.Industrial.LedMatrix(r, statuses, cols)` | `statuses[i]` 语义 **0 熄 / 1 正常 / 2 警告 / 3 故障**（≥2 自加描边）；格子由 `cols` 自动排 |
| `RUIF.Industrial.LedColor(status)` | 0 `BorderHair` / 1 `Alive` / 2 `Warn` / 3+ `Dead` ⇒ 灯色与图例同源 |

**状态 → 灯码映射**（与设备详情页 `StateColor` 逐项对齐，不另立一套阈值）：

| stateKey | 灯码 | 依据 |
|---|---|---|
| `run` | 1 正常 | 运转 |
| `fault` | 3 故障 | 故障 |
| `starved` / `AgNoPower` / `AgNoFuel` | 2 警告 | 缺料 / 缺电 / 缺燃料 |
| `idle` / `off` / `AgOff` / 未知 | 0 熄 | 停机或未知（**不虚报告警**） |

**数据来源（全部已有，无需新统计）**：`WorkshopStats`（占用/空地/设备数、`MissingDeviceLabels`）·
`WorkshopDeviceStateResolver.KeyOf`（状态唯一实现）· `WorkshopDeviceReader.CollectActiveWorkers`（谁在操作设备）·
`buildFitBodyText`（匹配/多余，沿用既有 Refresh 期预构建）。

## 删除 / 保留

- **删除**：`DrawKpiCard`（与窗口标题重复）、`DrawOccupancyCard`（被仪表取代）、`DrawFitCard`（被 `DrawFitSection` 取代）—— 均为纯死代码，无行为影响。
- **保留**：`BuildFitBodyText`（新段复用）、`OverviewHint`（改为仅空态显示）。

## 自查发现并修正的隐患

灯阵段初版只给 18px 呼吸 ⇒ 内容区 62px，而右侧**图例是 4 行 × 18 = 72px** ⇒ 末行会画出容器外
（skill 铁律 3：IMGUI 无裁剪）。已把段高修正为 `22 + 2×26 + 40`（`LedSectionHeight`）。

## 兼容性

- **存档影响：无**（纯 UI；未动任何字段 / Scribe 键）。
- **行为影响：无**（读数全为只读；未改任何写入路径）。
- 性能：全部读数在 **Refresh 期**算好（`BuildOverviewReadings`），绘制路径只取值 ⇒ 零每帧分配/零翻译。

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（过程中修正两处：partial 可访问性须与主声明一致 `internal sealed`；补 `Archive.UI` using） |
| XML | 已验证 | 中英 `Automation.xml` 解析通过 |
| 部署 | 已验证 | Defs 31 / Languages 39 / Assemblies 1 |
| 组件契约 | 已验证 | 三个工业组件均读源码取证（含"高度不足静默不画"这条隐藏约束） |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-OVERVIEW-IND.md -ExcludeFromFile .codebuddy/tmp/ovind-out-of-scope.txt -Blocking` |
| 实机 / 精美度 | **未验证** | 需重启后目视；随后按 skill `09` 册 5 维度 + 反劣质 12 条走查 |

## 实机验证要点

1. 打开车间管理 →「概览」：应看到 **3 个弧形表盘**（空间占用 / 设备运转率 / 设备有人率），
   每个表盘下方有精确值行（如 `3 / 12`）；表盘为**弧形+指针**（不是进度条）。
2. 灯阵：每台设备一个方块，颜色四态（灰/绿/橙/红）+ 右侧图例带计数。
3. 匹配段：第二行**橙色**显示「缺少：设备名…」（缺口为 0 时不显示该行）。
4. 空车间（无设备）：灯阵处显示引导文案，底部出现教育性提示。
5. **回归**：设备页 / 人员页 / 排班页 / 班次表 / 岗位页 / 设置页与本改造无关，应完全不变。

## 下一批（B）的前置问题

| 区块 | 缺什么 |
|---|---|
| ③ 瓶颈带 `BottleneckRibbon` | 需要"逐工序耗时/队列"口径 —— 建议用"该工序设备的待加工队列 + 今日产出"近似，**待玩家确认口径** |
| ④ 设备状态时间线 `MachineStateTimeline` | 需核实 `WorkshopDeviceLedger` 是否保留**逐台时间序列**；若无，需新增按台状态采样环（台账多一份数据） |
| ① 产出达成仪表 | 需要"今日产出 vs 目标"的聚合读法（`zone.PerPersonDailyTarget` × 在岗），与 B 的绩效块一起做更合理 |
