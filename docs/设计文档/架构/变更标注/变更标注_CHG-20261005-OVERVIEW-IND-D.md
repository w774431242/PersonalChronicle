---
change_id: CHG-20261005-OVERVIEW-IND-D
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle/Automation]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Overview.cs
    type: M
    symbols: WorkshopManageView.DrawBottleneckSection, WorkshopManageView.DrawOverviewBody, WorkshopManageView.OverviewContentHeight, WorkshopManageView.OvBigBlockH
    lines: 210-250, 280-340
---

# 变更标注 · CHG-20261005-OVERVIEW-IND-D

## Change Summary

按玩家 2026-10-05 的三条要求重构概览页（**修一个 bug + 删两块 + 腾出大组件区**）。

| 玩家要求 | 处置 |
|---|---|
| 「设备负载组件有溢出现象」 | **修复**（见下，根因在组件内部的宽度累加） |
| 「产出趋势可以删除」 | **已移除渲染**（方法体保留一轮作对照，下版清理） |
| 「工序设备匹配可以删除」 | **已移除渲染**（同上） |
| 「剩余空间可以添加一个大型组件」 | 已预留接口 `OvBigBlockH`（**当前 0**，方案定后填入） |

## ① 修复：设备负载条溢出

**根因（源码级）**：`RUIF.Industrial.BottleneckRibbon`（`Industrial.cs:309-346`）逐段：

```csharp
float w = Mathf.Max(1f, r.width * shareBuf[i] - 2f);   // ① 小段被抬到最小 1px
Draw.Box(seg, ...);
x += w + 2f;                                            // ② 每段再吃 2px 间距
```

⇒ 累计宽度 = Σ(max(1, w·s − 2) + 2) **必然超过 r.width**（段数越多、份额越不均越明显）
⇒ 玩家截图实证：末段红块越出面板右缘。

**修法**（不改 RUIF，调用侧预留）：`strip.width -= 段数 × 2 + 4`。
本页段数 = 设备数（上限 16）⇒ 最多让出 36px，视觉上无感，但右端不会再越界。

## ② 移除两块

- **近 5 日产出趋势**：`DrawTrendSection` 的调用已移除；
- **工序设备匹配**：`DrawFitSection` 的调用已移除（连带 `BuildFitBodyText` 不再被读）。

⚠ 两处**方法体与相关字段保留一轮**（与"旧设置页保留作回滚对照"同一策略，本项目已用过且玩家未反对），
在代码内以醒目注释标明"已停用"。下版随标注一并删除（已登记 Remaining）。

## ③ 大组件区预留

- `OverviewContentHeight()` 改为 `仪表台 + 灯阵 + 瓶颈带 + OvBigBlockH`；
- `OvBigBlockH` **当前为 0**（避免滚到底出现莫名空白），方案选定后填入高度并在
  `DrawOverviewBody` 末尾按该值绘制。

## 兼容性

- **存档影响：无**（纯 UI）。
- **行为影响：无**（删的是展示块；`BuildFitBodyText` 仍在 Refresh 期执行一次，只有字符串构建开销，可忽略）。

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警 |
| 部署 | 已验证 | Languages 39 / Assemblies 1 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-OVERVIEW-IND-D.md -ExcludeFromFile .codebuddy/tmp/ovd-out-of-scope.txt -Blocking` |
| 实机 | 待验证 | 负载条右端不再越出面板；趋势与匹配两块消失 |

## Remaining

1. **删除停用的方法体**：`DrawTrendSection` / `DrawFitSection` / `BuildFitBodyText` / `fitBodyText` /
   `ovDaily*` 字段 / 趋势与匹配的 5+3 个 Keyed 键（中英）—— 下版随本标注清理。
2. **大组件区**：等玩家选定方案后填入（见对话中的 5 个候选）。
