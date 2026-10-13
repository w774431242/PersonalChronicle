---
changeId: CHG-20261006-FTABS-REMOVE
title: 移除制造中心 ② 面板底栏 Tab 栏整行（7 个页签）
date: 2026-10-06
version: v1.5.1（未发版，仅工作区）
scope:
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.AppShell.cs
    type: M
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Detail.cs
    type: M
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.FloorTabs.cs
    type: M
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.cs
    type: M
---

# 变更标注 — CHG-20261006-FTABS-REMOVE

> 范围：按用户指令（实机截图框选）**移除 ② 面板底栏 Tab 栏整行**（预览 `#ftabs` / 7 个页签：总控·调度·分析·运营·事件·数据·编排）
> 日期：2026-10-06 ｜ 触发：用户明确选择「整条底栏 Tab 栏（7 个按钮）」，含各 tab 内容区的分发入口
> 编译：`dotnet build Source/PersonalChronicle.csproj` → **本轮 4 个文件 0 错 0 警**；
> 仓库另有 2 个既有错误（`ChronicleSettings.cs:216`、`WorkshopManageView.Posts.cs:123`），**非本轮引入**。
> 范围外：仓库存在大量既有未提交工作（含上一轮 `CHG-20261006-RAIL-LAYOUT` 的 8 个文件），
> 通过 `-ExcludeFromFile .codebuddy/tmp/ftabs-oos.txt` 声明排除。
> 注：本标注文件自身由 gate-runner 从 diff 集合中剔除，故不列入 `scope`。

---

## 1. 删除面（代码）

| 位置 | 删除内容 | 说明 |
|---|---|---|
| `AppShell.cs` | `DrawAppTabBar(...)`（约 81 行，含 2026-10-02 审计 P1-9 的"统一配额 + 等比压缩 + 溢出 +N + 降级路径能力裁剪"） | tab 栏的**唯一绘制实现** |
| `AppShell.cs` | `MinAppTabWidth = 46f` | 仅 tab 栏用 |
| `AppShell.cs` | `AppTabBarHeight = 26f` / `AppTabBarBottomPad = 4f` | 页签栏几何与底距 |
| `AppShell.cs` | `tabBarLabels` / `tabBarWidths` 缓冲字段 | 仅 `DrawAppTabBar` 用（绘制期零分配缓存） |
| `AppShell.cs` | app 路径调用点 `y = DrawAppTabBar(inner, y, FloorTabs.Length);` | ② 面板绘制链 |
| `Detail.cs` | 降级路径调用点 `y = DrawAppTabBar(viewRect, y, DegradedVisibleTabCount);` | 滚动长页路径 |
| `FloorTabs.cs` | `FloorTab` struct + `FloorTabs[]` 页签表 | Keyed 标签 + 各面板 `PanelCount` 计数 |
| `FloorTabs.cs` | `DrawFloorTabButton(...)` | 单页签按钮（选中/悬停/点击切页签） |
| `FloorTabs.cs` | `DegradedVisibleTabCount` | 降级路径"不含编排"的能力裁剪数 |

## 2. 保留面（有意不删）

| 保留项 | 理由 |
|---|---|
| `selectedFloorTab` 字段 + `TabCockpit…TabLayout` 语义常量 | 内容区分发（`AppShell.DrawActivePane`）、计高（`Detail.TabPaneContentHeight`）、降级路径钳位（`selectedFloorTab > TabData`）仍按它们工作；恢复 Tab 栏即重新获得全部页签 |
| 6 个页签的**内容面板**（调度 `StockQueuePanels` / 分析 / 运营 / 事件 / 数据 `DataLayerPanels+BridgePanels` / 编排 `DrawActivePane` 的 layout 分支） | 用户仅要求移除**页签栏整行**；这些面板的代码与数据采集一律未动 |
| `Detail` 路径的 hazard 斜纹条（`DrawFloorHazardStrip`） | 不属于"Tab 栏"，保持原样 |
| 9 个翻译键（`Floor.Tab.Label` / `.Cockpit/.Dispatch/.Analysis/.Ops/.Event/.Data/.Layout` / `.OverflowTip`） | **孤儿键**：随 Tab 栏移除后零引用。**有意保留**（用户可能恢复）——按 GATE-LOC 属 P4，已登记 |

## 3. 计高同步点（删除必须成对，否则滚动高漂移）

| 位置 | 原表达式 | 现值 |
|---|---|---|
| `AppShell.DrawAppFloorPanel` 的 `fixedRows` | `AppTabBarHeight + ZoomRowHeight + SopGap * 3f` | `ZoomRowHeight + SopGap * 2f`（少一行高 + 一段距） |
| `Detail.ComputeDetailHeight` | `+ AppTabBarHeight + AppTabBarBottomPad + HazardStripHeight` | `+ HazardStripHeight`（两项去项） |

两处均在代码注释里写明"删除时必须同时改"的约定（原 `TABUNIFY 3a` 的计高≡绘制契约）。

## 4. 后果（必须知晓）

- ② 面板底栏不再有页签；**内容区固定渲染 `selectedFloorTab`**，其初值 = `TabCockpit` ⇒ 恒显示「总控」KPI 带（截图中的 9 张卡）。
- 另外 6 个页签（调度 / 分析 / 运营 / 事件 / 数据 / 编排）**当前无入口**（代码保留、分支不可达）。
  ⚠ 守门实测的**具体影响面**：工艺**路线切换条**（`AppShell.DrawRouteSwitchRow`）唯一调用点在 `TabDispatch` 分支
  ⇒ 现在同样无入口；「编排」pane 不可达，但**画布编排模式仍可从 `#cline` 的「查看 ｜ 编排」分段进入**（非全死）。
  ⇒ 6 个页签的去留（恢复入口 / 正式下线，含其 UI 与数据采集）需**用户终裁**（GATE-UI 记 P2）。
- 恢复方式（两处代码注释均写明）：从 git 历史取回 `DrawAppTabBar` 与 `FloorTabs[]`，并在 §3 两处计高加回。

## 5. Gate 结果

| Gate | Result | Evidence |
|---|---|---|
| GATE-UI | **PASS WITH EXCEPTION** | `pc-ui-loc-guard` 只读审查：9 个已删符号全库 **0 代码引用**（Source / Tests / _scripts 均已核）；`AppShell.fixedRows` 与 `Detail.ComputeDetailHeight` 两处计高**同源且无第三处**；`selectedFloorTab` 除钳位外无写入点、无悬挂控件；代数闭合到 `inner.yMax`。例外 = P2「6 页签无入口（含路线切换条）」待用户裁决 + 实机项未执行 |
| GATE-LOC | **PASS** | 9 个 `Floor.Tab.*` 键确认为孤儿（zh/en 双侧俱在，**有意保留**，P4）；本轮 **0 新增 / 0 改动**用户可见文本（scope 仅 4 个 .cs） |
| GATE-TEST | **部分**（编译证据） | 本轮 4 文件 0 错 0 警；游戏内启动与实测**未执行**（沙箱无游戏进程） |
| GATE-DELTA | **PASS** | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261006-FTABS-REMOVE.md -ExcludeFromFile .codebuddy/tmp/ftabs-oos.txt -Blocking` → 「标注覆盖 4 个改动文件，无遗漏/超范围/残留」 |

## 6. 待实机验收

1. ② 面板底栏**不再出现** 7 个页签按钮，且**不残留空白带**（去掉 26px + 4px 后平面图应多出约 34px 高度：检查平面图是否变大、下方无溢出/重叠）。
2. 内容区仍显示「总控」KPI 带（9 卡），滚动高与可点区域正常（计高≡绘制）。
3. 降级路径（无 Chrome 快照时）同样无页签栏、hazard 位置正确。
4. 平面图两侧轨道（上一轮 `RAIL-LAYOUT`）在新高度下正常（含极矮保护）。

## 7. 回滚

`git checkout -- <§1 的 4 个文件>`；翻译键未改动，无需回滚。无 Save / Migration / 版本号影响。
