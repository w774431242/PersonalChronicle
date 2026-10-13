---
change_id: CHG-20261005-COLTUNE3
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView.DeviceColSharesFull, WorkshopManageView.DeviceColSharesCompact, WorkshopManageView.DeviceColStylesFull, WorkshopManageView.DeviceColStylesCompact, WorkshopManageView.DeviceHeaders, WorkshopManageView.DrawDeviceRow, WorkshopManageView.DeviceSortNumber, WorkshopManageView.DeviceMinColW (注释)
    lines: 590-680, 700-730, 780-830, 910-960
---

# 变更标注 · CHG-20261005-COLTUNE3

## 背景（玩家 2026-10-05）

> 「**移除 效率 / 位置**」

## 一、结构变化：10 列 → 8 列（窄档 7 列）

```
全档（8 列）0名称 1操作员 2状态 3当前制作 4产出 5累计运行 6累计产值 7建立时间
窄档（7 列）0名称 1操作员 2状态 3当前制作 4产出 5累计运行 6累计产值
```

✅ **顺带的彻底简化**：移除这两列后，**全档与窄档的前 7 列完全同号**，
两者仅差末尾一列（建立时间）。行内的下标定义从"10 个常量 + 2 个三元"
变成"7 个 `const` + 1 个三元"，比较器与样式数组同步收缩。

## 二、列宽重分（份额总和 = 1）

**全档**：名称 `0.23` / 操作员 `0.10` / 状态 `0.075` / 制作 `0.21` / 产出 `0.11` /
运行 `0.105` / 产值 `0.11` / 建立 `0.06`

**窄档**：名称 `0.25` / 操作员 `0.11` / 状态 `0.08` / 制作 `0.22` /
产出 `0.12` / 运行 `0.11` / 产值 `0.11`

> 腾出的空间按"文本列优先"分配：名称与「当前制作」各得最多（它们承载最长的动态文本），
> 数值列按各自数字宽度（产出 `8 / 168`、运行 `13 天 16 小时`、产值 `2,505`）够用即可。

## 三、连带处理

| 项 | 处理 |
|---|---|
| `DeviceSortNumber` 的「效率」case 与「位置」default | 删除；default 改为返回 0（不该到达 —— 文本列已被 `CompareDevicesBySort` 提前分流） |
| 行内的 `effText` 解包 | 删除（快照里仍生成 `rowText[5]` —— **无害**，且将来恢复该列可直接取用） |
| 悬停提示的效率补齐 | 删除（该信息已无列可"补"） |
| **门槛** | `8 × 100 = 800` ⇒ 玩家那个约 895px 的窗口**回到全档（8 列）**、每列约 105px；更窄才降 7 列 |

### ⚠ 信息去向（如实交代）

- **位置**：**没有丢** —— 它本来就在行悬停提示的第二行（`thing.Position`，原生既有内容）；
- **效率**（件/机时）：按你的要求**彻底移除** —— 表格与悬停都不再显示。
  数据本身仍在（`DeviceStatEntry` 的产出/运行原始值），随时可恢复。

## 存档影响：**无**（纯绘制层与列定义常量）

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（`_scripts/compile_with_sdk.ps1`） |
| 残留检查 | 已验证 | 全文件无 `iEff` / `iPos` / `effText` / `effRect` / `posText` 引用 |
| 部署 | 已验证 | Defs 31 / Languages 39 / Assemblies 1 同步 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-COLTUNE3.md` |
| 实机 | 未验证 | 见下 |

### 实机验证要点（重启后）

1. 设备页表头应为 **8 列**：`设备名称 | 操作员 | 状态 | 当前制作 | 产出（今日/本周） | 累计运行 | 累计产值 | 建立时间`
   —— **不再有「效率」「位置」**；
2. 各列明显更宽松（尤其名称与「当前制作」）；行内数字与表头仍逐列对齐；
3. **悬停任意设备行** ⇒ 提示第二行仍是位置（如 `(98, 0, 113)`）；
4. 点列名排序仍可用（数值列：产出 / 运行 / 产值 / 建立；文本列：名称 / 操作员 / 状态 / 制作）；
5. `Player.log` 无新的 `PersonalChronicle` 异常。

## 残留风险

1. **效率数据仍在快照里生成**（`BuildDeviceRowTextCache` 的 `rowText[5]`）而无处显示 ——
   属"低频（Refresh 期）计算的白开销"，量极小（一次除法 + 一次拼串/设备），
   保留是为将来恢复该列时零改动；若要彻底清除，需连同快照一起改（本批未做）；
2. **`ColEfficiency` / `ColPos` / `ColDurability` 三个语言键成为孤儿**（中英共 6 条）：
   键查找语义下零运行时影响，未清理；
3. **位置只能在悬停里看到**：若你希望它回到表格，需重新评估列宽预算（8 列已是该宽度下的舒适上限）。
