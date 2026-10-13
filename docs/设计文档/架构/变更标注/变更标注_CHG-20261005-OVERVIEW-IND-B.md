---
change_id: CHG-20261005-OVERVIEW-IND-B
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle/Automation, Languages]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Overview.cs
    type: M
    symbols: WorkshopManageView.DrawBottleneckSection, WorkshopManageView.BuildBottleneckBuffers, WorkshopManageView.DrawLedSection, WorkshopManageView.BuildOverviewReadings, WorkshopManageView.DrawOverviewIndustrial
    lines: 45-52, 108-180, 195-230, 285-330
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: PersonalChronicle.Automation.Tab.GaugeProduce, BottleneckTitle, BottleneckNoData
    lines: 480-490
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: PersonalChronicle.Automation.Tab.GaugeProduce, BottleneckTitle, BottleneckNoData
    lines: 475-485
---

# 变更标注 · CHG-20261005-OVERVIEW-IND-B

## Change Summary

概览页工业化改造 **批次 B**（玩家 2026-10-05 批准："感觉不错，继续下一批"）。
承 A 批（`CHG-20261005-OVERVIEW-IND`），本批做三件事 + 修一个我在实机截图里发现的视觉缺陷。

## ① 修复：灯阵被拉伸成"大色块"（P0）

**现象**（玩家实机截图）：3 台设备渲染成三个约 **233×52** 的巨大色块，而不是小方灯。

**根因（源码级取证）**：`RUIF.IndustrialFmt.LedCell`（`Industrial.cs:83-91`）：

```csharp
float cw = r.width / cols, ch = r.height / rows;   // ← 把整块 rect 均分
return new Rect(r.x + cx*cw + 0.5f, r.y + cy*ch + 0.5f, max(1,cw-1), max(1,ch-1));
```

⇒ `LedMatrix` 不按"灯格大小"画，而是**填满你给的 rect**。A 批我传了满宽 ⇒ 3 列均分整行 ⇒ 每格 233px 宽。

**修法**：按**期望格子大小反推 rect**（`cols × (LedCellMax+4)`），即
`RUIF.Industrial.LedMatrix(new Rect(x, y, cols*26, rows*26), statuses, cols)`。

## ② 产出达成仪表（第 4 张表）

- 值 = **今日产出合计**（Σ `Manager.DeviceLedger.ProducedTodayOf`，与设备页「今日」列**同一口径**）；
- 量程 = **全员目标** = `zone.PerMemberDailyTarget × 车间成员数`；
- ⚠ **口径说明（诚实标注）**：分母用**成员数**而非在岗数 —— 概览页不掌握"本车间在岗人数"
  （`ActiveWorkerCount` 是全图口径，混用会得到无意义的比值）⇒ 标签与文件内注释都写明是"全员目标"。

## ③ 瓶颈带（`RUIF.Industrial.BottleneckRibbon`）

- 段标签 = 各设备显示名；段值 = **各设备累计运行时长**（`DeviceLedger.RunTicksOf`，每 60 tick 真实采样）；
- **为什么用运行时长**：本工程没有逐工序耗时统计，而"谁跑得最久 = 谁最忙 = 瓶颈"正是瓶颈的工程定义；
  组件会把最大段标红 + 描白边 + 下方自动注脚（注脚文案走 RUIF 自己的 Keyed，已多语言）；
- 全为 0（设备刚放下、还没采样到）⇒ 显示"暂无运行数据"，**不画均匀假带**（LAND-009）；
- 设备数按组件契约截断到 **16 段**。

## ④ 纪律：绘制期零分配

瓶颈带的 `string[16]` / `float[16]` 缓冲**移到 Refresh 期**（`BuildBottleneckBuffers`），
绘制路径只读 —— 与 skill 反模式清单「绘制路径里 new / 拼接」一致。

## 兼容性

- **存档影响：无**（纯 UI + 只读台账；未动任何字段 / Scribe 键）。
- **行为影响：无**（本批全部是 `*Of(...)` 读方法调用）。
- 性能：读数与缓冲都在 Refresh 期算好；`RunTicksOf/ProducedTodayOf` 是字典查找（O(1)），
  且仅在开窗刷新时按设备数遍历一次。

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警 |
| XML | 已验证 | 中英 `Automation.xml` 解析通过 |
| 部署 | 已验证 | Languages 39 / Assemblies 1 |
| 组件契约 | 已验证 | `LedCell` 内部几何已读源码（根因）；`BottleneckRibbon` 段数上限 16 已按契约截断 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-OVERVIEW-IND-B.md -ExcludeFromFile .codebuddy/tmp/ovb-out-of-scope.txt -Blocking` |
| 实机 / 精美度 | **未验证** | 需重启后目视 |

## 实机验证要点

1. **灯阵**：每台设备应是**小方块**（约 22px），排成一行/两行，**不再是大色块**。
2. **仪表台**：从 3 张变 **4 张**（新增「今日产出」）；每张表盘直径会相应变小（窗口宽度不变）。
3. **瓶颈带**：出现一条设备负载条 —— 跑得最久的设备**标红描边**，下方自动注脚「瓶颈：<设备> (N%)」。
   若设备刚放下还没跑够 1 秒，显示"暂无运行数据"。
4. **行为回归**：设备页的「今日 / 5 天」「运行」列应与概览读数一致（同台账同口径）。

## Remaining（批次 C / 后续）

1. **设备状态时间线**（`MachineStateTimeline`）**暂不做**：取证确认 `WorkshopDeviceLedger`
   只有 `Dictionary<int, DeviceStatEntry>` 的**窗口计数**，**没有逐台状态时间序列**
   ⇒ 要画它必须新增"按台状态采样环"（持久或运行期）。信息量（"这台设备昨天停过"）与成本不成正比。
   **替代建议**：用台账已有的 `ProducedOnDayOf` 画**近 5 日产出趋势**（零新增数据）—— 待玩家定。
2. **窗口标题行的「格数 198 · 可用空地 176 · 设备 3」**：仍在（属窗口框架 `Dialog_WorkshopManage`），
   与仪表不再重复（仪表给比例+口径），故本批未动；若要精简需改窗口框架，风险与收益需再评估。
3. **精美度走查**（skill `09` 册 5 维度 + 反劣质 12 条）：需实机画面逐条核对。
