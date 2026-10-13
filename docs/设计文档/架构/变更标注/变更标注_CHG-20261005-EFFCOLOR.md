---
change_id: CHG-20261005-EFFCOLOR
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Overview.cs
    type: M
    symbols: WorkshopManageView.BuildBigBlockReadings (ovEffColors 赋值), WorkshopManageView.DrawEfficiencySection
    lines: 428-439
---

# 变更标注 · CHG-20261005-EFFCOLOR

## 背景

玩家 2026-10-05 实机反馈：「稼动表建议将产出-机时线双线互相高对比色值」。

## 根因（取证：RUIF `Tokens.cs` 实测色值）

| 令牌 | RGB | 观感 |
|---|---|---|
| `Tokens.Accent`（原·产出） | `(0.306, 0.604, 0.941)` | 天蓝 |
| `Tokens.Info`（原·机时） | `(0.478, 0.722, 0.910)` | **浅蓝** |

原实现走的是色板序色 `Tokens.Series(0)` / `Series(1)`，而这两个恰好是**同一蓝色系**
（仅明度/饱和不同）⇒ 深色底（`Tokens.Card` = `0.133,0.149,0.180`）上两条线几乎叠在一起。

## 改动

```csharp
ovEffColors[0] = RUIF.Tokens.Accent;   // 产出：天蓝（保持"产出是主角"的既有视觉关系）
ovEffColors[1] = RUIF.Tokens.Warn;     // 机时：橙黄（与蓝接近互补 ⇒ 对比最强）
```

**选色依据**：

1. **色相互补**：蓝 ↔ 橙在色轮上接近对立 ⇒ 在 6 色板（Accent/Info/Alive/Warn/Dead/Muted）
   中这是对比最强的一对；
2. **不选 `Alive`（绿）**：蓝 ↔ 绿是相邻色相，对比明显弱于蓝 ↔ 橙；
3. **色盲友好**：蓝-橙对红绿色盲仍可辨（蓝-绿对则不可）；
4. **仍全部走令牌**：没有裸写 `new Color(...)`（skill 红线 1），只是**换用哪几个令牌**。

⚠ `Warn` 字面语义是"警告"，但把语义色用作**图表系列色**是框架既定用法
（`Tokens.Series(3)` 与 `ChartCandFmt.Palette` 都含 `Warn`），且图例有「● 机时」文字标注
⇒ 不会读成"机时告警"。

## 图例同步

`DrawEfficiencySection` 的图例色块与曲线读的是**同一对** `ovEffColors`
⇒ 换色只改一处，图例自动跟随，不存在"图例一个色、线另一个色"。

## 存档影响：**无**

纯绘制配色，不涉及任何持久字段、不改 Scribe 键、无 Migration。

## 验证

重启后打开概览页 ⇒「稼动 vs 产出」两条线应为**天蓝 + 橙黄**，肉眼可分辨；
图例两个色块与两条线颜色逐项一致。

## 残留风险

1. 若将来 RUIF 调整 `Tokens.Warn` 的色值，本页双线对比度会随之变化（属令牌语义范围内）；
2. 本页只解决"两线互相区分"，未改线宽/线型（`Charts.LineChart` 的行宽固定 1.5f，
   组件不提供虚线能力）—— 若仍嫌不够，需改框架而非本页。
