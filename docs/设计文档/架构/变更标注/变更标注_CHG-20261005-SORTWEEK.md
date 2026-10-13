---
change_id: CHG-20261005-SORTWEEK
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView.DeviceSortNumber (case 4)
    lines: 1443-1453
---

# 变更标注 · CHG-20261005-SORTWEEK

## 背景（玩家 2026-10-05）

> 「**产出排序以本周数量为准**」

## 一、这是一处**名实错位**（不是单纯换排序键）

设备页「产出」列一格显示两个数：`8 / 168`，其中

| 位置 | 取值 | 台账方法 |
|---|---|---|
| 前半段（今日） | 今日产出 | `ProducedOnDayOf(thing, today)` |
| 后半段（列标题写"本周"） | **5 天滚动窗口** | `ProducedInWindowOf(thing, today)` |

而**排序键原先取的是前半段（今日）** ⇒ 点「产出」列排序时，列表按 `8 / 1 / 0 …` 排，
而玩家眼里那一列的主数字是 `168 / 102 / 0 …` —— **排序与显示不是一回事**（UI-009 不可自证）。

## 二、改动（一行）

```csharp
case 4:   // 产出
-    return ledger != null ? ledger.ProducedOnDayOf(thing, today) : 0f;
+    return ledger != null ? ledger.ProducedInWindowOf(thing, today) : 0f;
```

**口径与列显示同源**（`ProducedInWindowOf` 正是设备页「产出」列后半段与顶部 KPI 横幅用的方法）
⇒ 排序结果与玩家看到的数字一致，不再出现"显示 168、却按 8 排"。

⚠ 只改排序键，**不动显示、不动台账**（显示口径若要改，属另一批，见下）。

## 三、⚠ 登记一处既有的名实不符（本批未修）

**列标题写「本周」，底层却是 5 天滚动窗口**（并非 7 天）：

- 2026-10-02 的口径调整把该列由"今日产量"改成"5 天窗口"（`ProducedInWindowOf` 的注释有记），
  但**列标题文案一直是"本周"**；
- 本批按你的要求把排序对齐到那个数（即列上真实显示的那个），**标题与底层的不一致仍然存在**。

**要修的话有两条路**（都不难，但会动到语言文件或台账口径，本批没做）：

1. 改标题为「产出（今日 / 近 5 日）」⇒ 与底层一致，只动中英 Key；
2. 把窗口真改成"本周"（7 天 / 自然周）⇒ 动 `ProducedInWindow` 的口径与 KPI 横幅。

## 存档影响：**无**（纯排序键；不新增/不改任何 Scribe 键）

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（`_scripts/compile_with_sdk.ps1`） |
| 部署 | 已验证 | Defs 31 / Languages 39 / Assemblies 1 同步 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-SORTWEEK.md` |
| 实机 | 未验证 | 见下 |

### 实机验证要点（重启后）

1. 点设备页「产出（今日/本周）」列名 ⇒ 变 ▲；
2. **核对**：升序时列表应按**斜杠后面的数字**递增（`0 / 0` → `0 / 12` → `1 / 102` → `8 / 168`），
   而不是按斜杠前面的今日数字；再点 ⇒ ▼ 反向；
3. 与顶部 KPI 横幅的产量口径应一致（同一个方法）；
4. `Player.log` 无新的 `PersonalChronicle` 异常。

## 残留风险

1. **列标题「本周」≠ 底层 5 天窗口**（见第三节，本批只对齐排序）；
2. **产出列只有"今日/窗口"两种读数**：若你希望排序能**分别按今日或窗口**选（如两次点击循环），
   需要给该列加"排序子模式"（当前是"点列名 = 一个键"的模型），属功能扩展；
3. 排序状态仍是会话态（关面板/读档后回到不排序）—— 与前几批一致。
