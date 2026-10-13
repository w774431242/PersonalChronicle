---
change_id: CHG-20261005-COMPFIX
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle, Languages]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Overview.cs
    type: M
    symbols: WorkshopManageView.BuildBottleneckBuffers, WorkshopManageView.ovBottleneckIndex, WorkshopManageView.DrawBottleneckSection, WorkshopManageView.BottleneckNameOf, WorkshopManageView.T2, WorkshopManageView.BuildBigBlockReadings, WorkshopManageView.DrawBottleTrendSection
    lines: 378, 449-485, 680-720, 988
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: EffFooter, BottleneckFoot
    lines: 498-504
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: EffFooter, BottleneckFoot
    lines: 489-495
---

# 变更标注 · CHG-20261005-COMPFIX

## 背景

玩家 2026-10-05 实机截图「检查组件问题」。逐项核对后确认 **4 处问题**（3 处真缺陷 + 1 处误读排除）。

## 缺陷 1：瓶颈带段内标签溢出（真缺陷）

**取证**（`RUIF\Source\Components\Industrial.cs:337-339`）：

```csharp
if (stageLabels != null && i < stageLabels.Length && w > 24f)
    Draw.Label(new Rect(x, r.y + barH / 2f - labelRowH / 2f, w, labelRowH), stageLabels[i],
        Tokens.FontLabel, Tokens.Text, TextAnchor.MiddleCenter);
```

组件**会自己在段宽 >24px 的段内画标签**，且 `Draw.Label`（`Draw.cs:151-172`，实测**不做裁剪**，
只禁用自动换行）⇒ 设备名以 `MiddleCenter` 溢出压住相邻段。
玩家截图里条带右侧那段孤立白字「**待机（电力）**」正是它（该段实际宽度不足容纳五个字）。

**修复**：`BottleneckRibbon(strip, null, bnTimes, ...)` —— **不传段标签**；注脚改由本页在条带下方自画
（`BottleneckFoot` key + `BottleneckNameOf`）⇒ 仍能指出"谁是瓶颈"，且**名字可带设备别名**。

## 缺陷 2：瓶颈演变图例无法区分设备（真缺陷）

截图里 4 个图例项全叫「炉灶（电力）」—— 我上一批给设备页加了别名，但**概览页的图例没有跟上**。

**修复**：`ovBottleNames[j]` 改用 `Manager.DisplayNameOf(thing)`（含别名）。
⇒ 别名必须贯穿**所有**显示设备名的位置，否则"设备过多难区分"只解决一半。

## 缺陷 3：瓶颈演变无天标签（真缺陷）

柱状图组件**不画类目标签**（契约），而 7 天窗口里目前只有 1 天有采样 ⇒
玩家看到"孤零零一根柱子"无法判断它是哪一天，会以为图坏了。

**修复**：柱图下方补一行天标签（复用 `ovDailyLabels` 与波峰图同一套文案），
高度分配改为「标题 22 + 柱图 + 天标签 18 + 图例 18 + 底边距」，两者不重叠。

## 缺陷 4：稼动图脚注过窄（真缺陷）

脚注文案约 250px 而 rect 仅 200px ⇒ 溢出到左侧标题区（视觉上标题与脚注黏连、疑似缺字）。

**修复**：文案缩短为「左旧 → 右新（6 天）」（英文 `oldest -> newest (6d)`）⇒ 落在 200px 内。

## 排除项：底部提示文案（非缺陷）

截图读作「共 13 台设备，打开时统计一次，改动点后（刷新）」，实际 key
`FooterHint` = 「共 {0} 台设备 · 打开时统计一次，改动后点「刷新」」⇒ 文案本身通顺，系截图 OCR 误读，**未改**。

## 存档影响：**无**

纯绘制与取值改动（含 2 个 Keyed 文案），不涉及持久字段、不改 Scribe 键、无 Migration。

## 验证

1. **瓶颈带**：条带上**不再出现**孤立溢出的白字设备名；条带下方仍有「瓶颈 X · Y%」注脚；
2. **瓶颈演变**：图例显示「A · 炉灶（电力）」等带别名项，柱图下方出现「6 天前 … 今天」标签；
3. **稼动 vs 产出**：标题行右侧脚注完整显示「左旧 → 右新（6 天）」，不与标题黏连。

## 残留风险

1. **段内标签整体移除**：若将来希望保留段内标识，需先给 RUIF 的 `BottleneckRibbon` 加
   文本截断（`TruncateToWidth`）——那属于框架改动，不在本批范围；
2. 天标签在半宽面板下每列约 100px，文案「6 天前」可容纳；若将来窗口进一步变窄，
   标签会先于柱体变得拥挤（未做省略号降级）。
