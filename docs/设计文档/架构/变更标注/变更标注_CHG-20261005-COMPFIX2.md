---
change_id: CHG-20261005-COMPFIX2
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle, Languages]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Overview.cs
    type: M
    symbols: WorkshopManageView.BuildBottleneckBuffers, WorkshopManageView.DrawBottleneckSection, WorkshopManageView.bnLabels, WorkshopManageView.BuildOverviewReadings
    lines: 456-475, 672-686
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: BottleneckFoot (removed)
    lines: 500-502
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: BottleneckFoot (removed)
    lines: 491-493
---

# 变更标注 · CHG-20261005-COMPFIX2

## 背景

上一批（CHG-20261005-COMPFIX）修「瓶颈带段内标签溢出」时，采取的是**绕开**方案：标签传 `null`、
注脚改本页自画。当时即说明「想保留段内设备名，需先给框架加文本截断，**要你点头才做**」。
玩家 2026-10-05 指示「按你推荐修复」⇒ 本批执行**根治**方案：改框架 + 恢复段内标签。

## 改动一（跨仓 · RUIF 框架）

`RUIF/Source/Components/Industrial.cs:339-347`：`BottleneckRibbon` 段内标签
`Draw.Label` → **`Draw.EllipsisLabel`**（走 `Widgets.LabelEllipses` → 原版 `Text.ClampTextWithEllipsis`）
⇒ 放不下补省略号、**永不越界**。

- 该修复的**取证依据**：`Draw.cs:151-172` 的 `Label` 实测**不做裁剪**（只禁用自动换行），
  `MiddleCenter` 锚点下超宽文字向两侧溢出矩形 —— 这正是消费方截图里那段孤立白字的成因。
- 框架内**已有** `EllipsisLabel`（`Draw.cs:188-202`，注释即写明"这是文本被切割的常规解"）⇒ 未新增 API。
- 已按 RUIF 自身规范登记 `docs/06-发布记录/CHANGELOG.md`（含发布定位 PATCH / 影响面 / Gate 摘要）。

## 改动二（本仓 · PC 恢复段内标签）

- `BuildBottleneckBuffers(List<Thing> list)`（原接收 `int deviceCount`）⇒ 逐段填
  `bnLabels[i] = Manager.DisplayNameOf(thing)`（**带设备别名**，与设备页/瓶颈演变图例同一口径）；
- `DrawBottleneckSection` ⇒ 标签**传回**组件，删除上一批的自画注脚；
- 删除随之失去用途的 `BottleneckNameOf` / `T2` 助手与 `ovBottleneckIndex` 字段（减法审计）；
- 语言文件删除 `BottleneckFoot`（中英各 1 键）—— 注脚回归组件自画（key
  `RUIF.Comp.Industrial.Bottleneck`，框架已提供双语）。

## 与上一批的关系（刻意的一致性）

| 项 | COMPFIX（上一批） | COMPFIX2（本批） |
|---|---|---|
| 段内标签 | 传 `null`（绕开） | **传回设备名**（根治后） |
| 注脚 | 本页自画 | **组件自画**（取 `stageLabels[bottleneck]`） |
| 溢出风险 | 无（不画标签） | 无（框架截断） |
| 别名 | 注脚带别名 | 段内 + 注脚**都**带别名 |

> 注脚回归组件自画的额外好处：它与"最大段标红描边"出自**同一判定**（`IndustrialFmt.BottleneckIndex`），
> 不会出现"红段标的是 A、注脚写的是 B"这类两处口径不一致。

## 存档影响：**无**

纯绘制与取值改动 + 删 1 个 Keyed 键，不涉及持久字段、不改 Scribe 键、无 Migration。

## 验证

1. **瓶颈带**：条带上出现带别名的设备名（如「A · 炉灶（电力）」），**窄段显示省略号而非溢出**；
   条带下方仍有「瓶颈 X 占 Y%」注脚（组件自画）；
2. **无溢出**：条带右端不再有飘出的孤立文字；
3. **设备页 / 图例**：与瓶颈带显示同一套「别名 · 设备名」。

## 残留风险

1. **跨仓耦合**：本批效果依赖 RUIF DLL **已更新**（框架输出直接写 `RUIF/Assemblies/RUIF.dll`，
   已编译成功）。若玩家回退 RUIF 版本，段内标签会恢复成"可能溢出"的旧行为 —— 但那只是外观，
   不会崩、不写存档；
2. 段宽 ≤ 24px 时组件仍**不画**标签（既有门槛，未改）⇒ 极窄段无标签是预期行为；
3. 省略号长度由原版 `Text.ClampTextWithEllipsis` 决定，超大字号下省略号本身可能贴近段宽上限
   （未做二次收紧）。
