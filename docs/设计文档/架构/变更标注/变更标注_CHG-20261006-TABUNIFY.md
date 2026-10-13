---
change_id: CHG-20261006-TABUNIFY
version: v1.1.5
date: 2026-10-06
author: AI
scope: [Source]
files:
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.AppShell.cs
    type: M
    symbols: 新增常量 AppTabBarBottomPad 与复用缓冲 tabBarLabels/tabBarWidths；DrawAppTabBar 增 maxTabs 参数 + 改零分配；app 调用点补 maxTabs；DrawActivePane 逐块 try/catch → DrawDetailPanelFallback（9 处）
    lines: 46-60, 525, 728-796, 1555-1714
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.FloorTabs.cs
    type: M
    symbols: DrawFloorTabBar 与第二套页签几何（TabBarHeight/TabBarPadX/TabBarPadTop/TabButtonWidth/TabButtonGap）删除；FloorTabs 数组 PanelCount 改引用各面板 const；新增 DegradedVisibleTabCount；补 using
    lines: 2, 31-60, 67-78
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Detail.cs
    type: M
    symbols: ComputeDetailHeight 页签栏计高改 AppTabBarHeight + AppTabBarBottomPad（与绘制返回值同源）；DrawDetailContent 新增降级路径 selectedFloorTab 单点钳位；旧调用点改 DrawAppTabBar(viewRect, y, DegradedVisibleTabCount)
    lines: 152-158, 344-353, 395-397
  - path: Source/PersonalChronicle/Manufacturing/UI/ManufacturingAnalysisPanels.cs
    type: M
    symbols: PanelCount private → internal（=4，单一真源）
    lines: 21-24
  - path: Source/PersonalChronicle/Manufacturing/UI/ManufacturingOperationsPanels.cs
    type: M
    symbols: PanelCount private → internal（=3，单一真源）
    lines: 28-31
  - path: Source/PersonalChronicle/Manufacturing/UI/ManufacturingStockQueuePanels.cs
    type: M
    symbols: 新增 internal const PanelCount = 2（单一真源）
    lines: 21-24
  - path: Source/PersonalChronicle/Manufacturing/UI/ManufacturingEventsPanels.cs
    type: M
    symbols: 新增 internal const PanelCount = 2（单一真源）
    lines: 31-34
  - path: Source/PersonalChronicle/Manufacturing/UI/ManufacturingBridgePanels.cs
    type: M
    symbols: 新增 internal const PanelCount = 6（单一真源）；BlockHeight 消除魔数 PanelHeight * 6f
    lines: 26-29, 33-36
  - path: Source/PersonalChronicle/Manufacturing/UI/ManufacturingDataLayerPanels.cs
    type: M
    symbols: 新增 internal const PanelCount = 2（单一真源）；BlockHeight 改由常量派生
    lines: 25-28, 32
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
    symbols: BuildInfo.BuildTimeUtc（compile_with_sdk.ps1 自动重写的生成物，未跟踪文件）
    lines: 1-6
---

# 变更标注 · CHG-20261006-TABUNIFY

## 背景

2026-10-06 制造系统中心只读审计（GATE-UI/GATE-LOC，`pc-ui-loc-guard`）给出 P2-2 / P2-3 两项结构性缺口；
随后由工程总控 `pc-orchestrator` 执行 AI-001 任务入口协议，给出四项实施计划与三处 P1 风险裁定
（含对主 agent 简报的 4 项**转述更正**，本节全部以总控实读结论为准）：

| 步 | 缺口 | 总控裁定要点 |
|---|---|---|
| 3a | 底栏页签**两套实现**（旧版固定 78f、无溢出处理） | 差异不止宽度：栏高 27 vs 26、按钮竖距、左 padding、返回值全不同；且 `TabBarHeight` 有**第二处使用**（`Detail.cs` 计高）⇒ 必须同批同步，否则滚动高漂移 |
| P2-2 | app 主路径**无面板级错误降级** | 禁「switch 外层整体 try」、禁 lambda 助手（每帧闭包）；`TabLayout` 的 try 必须覆盖到 `MirrorLayoutToSave` |
| 3c | `PanelCount` 双真源 | 范围修订：**2 处**已有常量提升可见性 + **4 处新增**（原简报误称 6 处均有常量） |
| 3b | 降级路径**不支持**第 7 页签「编排」 | 残留 `selectedFloorTab=6` 全工程**无重置点** ⇒ 必须**带单点钳位**方可交付 |

## 一、3a 页签栏统一为单一实现

**删除**（`FloorTabs.cs`）：`DrawFloorTabBar`（第二套实现）与 5 个专属几何常量
`TabBarHeight=27f` / `TabBarPadX=13f` / `TabBarPadTop=5f` / `TabButtonWidth=78f` / `TabButtonGap=4f`。

**统一到** `AppShell.DrawAppTabBar`（量宽 → 严格配额压缩 ≥`MinAppTabWidth` → 溢出「+N」+tooltip）。
几何以 app 路径值为准（总控裁决 A4）：栏高 `AppTabBarHeight`=26f、底距 4f。

**计高同源**（P1-R1）：`Detail.ComputeDetailHeight` 原用裸 `TabBarHeight`(27f) 计高，现改为
`AppTabBarHeight + AppTabBarBottomPad`（= 30f），与 `DrawAppTabBar` 的 `return bar.yMax + AppTabBarBottomPad`
**同一表达式**。底距由字面量 `4f` 提为具名常量 `AppTabBarBottomPad`（GOV-006）。
净位移 +3px，已由总控裁定接受（R6，待实机与预览对账）。

**顺带收口**（P3-R8）：`DrawAppTabBar` 原每帧 `new string[count]` / `new float[count]` ⇒ 改为实例字段
`tabBarLabels` / `tabBarWidths` 复用缓冲（首帧或扩容时分配，之后逐帧零分配，PERF-001）。

## 二、P2-2 app 主路径逐块失败隔离

`DrawActivePane` 的 `try` 原先只有 `finally`（保证 `EndScrollView` 收口）、**没有 catch** ⇒ 任一面板抛异常
即逃逸到外层整页隔离 ⇒ 当帧余下绘制全部消失且无可见提示。

现按旧路径已验证的范式（`Detail.DrawTabPaneContent` 逐面板 `try/catch` + `DrawDetailPanelFallback`
含 5s 节流日志）补 **9 处** `try/catch`：

| case | 隔离块数 | 说明 |
|---|---|---|
| Cockpit | 1 | KPI 九卡带 |
| Layout | **1（含 4 条语句）** | `CollectPalette` / `LoadLayoutFromSave` / `Draw` / `MirrorLayoutToSave` |
| Dispatch | 2 | 路线切换条、队列块**各自**隔离 |
| Analysis / Ops / Event | 各 1 | — |
| Data | 2 | DataLayer 块、Bridge 块各自隔离 |

**关于 Layout 的 4 条语句合并为单块**（对总控计划「逐条」的细化，理由已在代码注释写明）：
拆分会让 `Draw` 失败后 `MirrorLayoutToSave` 仍执行 ⇒ **渲染失败仍写存档**。单块隔离使 catch 分支
不含镜像调用，天然满足 P1-R3 的收口要求。

**验收红线**（全部静态核验通过）：catch 一律留在 **case 内** —— 外层那个 `try` 是 IMGUI 滚动配对所必需，
它**只有 `finally`、没有 `catch`**（不是"整体 try/catch"）；
**零 lambda/委托**（避免每帧闭包分配）；catch 必调 `DrawDetailPanelFallback`（无空 catch）。

## 三、3c 消除 PanelCount 双真源

| 面板模块 | 原状态 | 现状态 |
|---|---|---|
| `ManufacturingAnalysisPanels` | `private const int PanelCount = 4` | `internal const`（=4） |
| `ManufacturingOperationsPanels` | `private const int PanelCount = 3` | `internal const`（=3） |
| `ManufacturingStockQueuePanels` | **无**（2 为隐式） | 新增 `internal const`（=2） |
| `ManufacturingEventsPanels` | **无**（2 为隐式） | 新增 `internal const`（=2） |
| `ManufacturingBridgePanels` | **无**，且块高用魔数 `PanelHeight * 6f` | 新增 `internal const`（=6）+ 块高改由常量派生 |
| `ManufacturingDataLayerPanels` | **无**，块高用 `PanelHeight * 2f` | 新增 `internal const`（=2）+ 块高改由常量派生 |

`FloorTabs` 数组改为**引用**这些常量（`Cockpit=1` / `Layout=1` 两项保留字面量并在 GOV-006 登记注释里
写明理由：二者由 `AppShell` 单块绘制、无对应面板模块）。组成公式已写入登记注释：
`Dispatch=2 / Analysis=4 / Ops=3 / Event=2 / Data=2+6=8`。

> 已知边界（总控 R7）：这是**声明值**；运行期实际绘制按快照动态（如 Bridge 的 `snapshot.Panels.Count`
> 可少于 6）。本项只保证「页签（n）」与面板模块自述口径**编译期联动**。

## 四、3b 降级路径隐藏「编排」页签 + 单点钳位

**能力边界**：降级路径（`UsesAppShell == false`：Chrome 快照空 或 Workshop 为 `NoMap`）是**滚动长页**，
而编排画布要求"未滚动的坐标系"（app 路径为该页签专禁页内滚动）⇒ 该路径**不提供**画布。

**改动**：`DrawAppTabBar` 增 `maxTabs` 参数；降级路径传 `DegradedVisibleTabCount`（`= TabData + 1`，
**派生自 `TabData`，不写裸 6**）。被能力裁掉的页签**不计入**「+N」—— 否则 tooltip 会承诺"拉宽可见"，
而那几项是能力不可用、不是宽度不足（总控 R4：GOV-010 不静默、也不撒谎）。

**钳位**（P1-R2，本项是 3b 可交付的前提）：`Detail.DrawDetailContent` 的非 app 分支、进入
`DrawTabPaneArea` 之前，新增单点钳位 `if (selectedFloorTab > TabData) { selectedFloorTab = TabData; contentScroll = Vector2.zero; }`。
理由：`selectedFloorTab` 唯一写入点是点击（`DrawFloorTabButton`），**全工程无其它重置点**，
窗口实例随会话存续 ⇒ 玩家在 app 路径选「编排」后落入降级态会残留 6，内容区落 default 返回 0
= **空白页签且无提示**。
（表述订正：同文件 `Detail.cs` 的 populationView 是**消费端局部** `Mathf.Clamp`；本批为**写回式**钳位 +
滚动重置，两者口径不同但同属"单点收口"。）

## 五、明确不做（非目标 / 范围外）

1. **不给降级路径补画布**（需结构性改造，另案）；
2. 不删降级路径的 `.hazard` 斜纹条（app 路径已按基線删除；两路径版式统一另案）；
3. 不改 `DrawFloorTabButton` 的视觉/交互；
4. 不动 app shell 布局预算（`fixedRows` 等）与 `ActivePaneHeight` 的异常隔离（P2-2 未覆盖该函数，登记为 R5 残留）；
5. 不动 `WorkshopLayoutCanvas` / `WorkshopFloorPlan` 本体；不动 `FloorDeviceDetailWindow` 的**同名独立** `TabBarHeight=26f`；
6. **零新增翻译键 / Defs / 存档字段 / Harmony / 第三方触点**（所以 scope 仅 `Source`）。

## 六、校验证据

| 项 | 结果 |
|---|---|
| 编译 | `pwsh _scripts/compile_with_sdk.ps1` ⇒ `EXITCODE=0`、`error CS=0`、`warning CS=0`（`/warn:4`） |
| 残留扫描 | `MasterControl/` 内 `DrawFloorTabBar` / `TabBarPadX` / `TabButtonWidth` / `TabButtonGap` / `TabBarPadTop` / 裸 `TabBarHeight` **代码零残留**（仅存注释留痕） |
| P2-2 静态约束 | `DrawDetailPanelFallback` 调用 = **9**；`catch (Exception ex)` = **9**；空 catch = **0**；`DrawActivePane` 内无 lambda/委托 |
| 3c 单一真源 | 全仓 `PanelCount` 定义 = 6 个面板 `internal const` + 1 个 `FloorTab` 字段 + 2 处 label 消费；无第二处硬写数值 |

## 七、Gate Results

| Gate | 结论 | 证据 |
|---|---|---|
| ARCH | **PASS WITH EXCEPTION**（独立复核 `pc-arch-gatekeeper`） | 依赖方向：`MasterControl → Manufacturing.UI` 为**既存边**（本批沿该方向 +1 using / +6 `const` 读取），**零新增反向边**，`Domain/` 对两 UI 命名空间 0 命中 ✓；四项硬触发均 No，含 **Static Mutable State 专项**（`tabBarLabels/tabBarWidths` 确为**实例**字段）✓；公共面未扩大（`internal const` 不入 API）✓。例外 2 项**均为存量**：① `AppShell.cs` ≈1716 行超 ARC-013 且本批 +≈70 行；② `MasterControl ⇄ Manufacturing.UI` 存量 UI↔UI 循环 ⇒ 两者见 §八-6 / §八-7 强制记账 |
| DATA / SAVE | **N/A** | 无 Def / Runtime 数据 / Read Model 契约 / Scribe 字段变更；`selectedFloorTab` 保持 UI 瞬时态 |
| COMP | **N/A** | 无第三方 / DLC 类型、无 Patch / Harmony、无加载顺序影响 |
| LOC | **PASS**（独立复核） | **零新增键、零改动 `Languages/`**；复用键 `…Floor.Tab.Label`（两参模板，两处调用点均传 2 个 `NamedArgument`）与 `…Floor.Tab.OverflowTip` 双语齐备、占位符一致 |
| UI | **PASS WITH EXCEPTION**（独立复核） | 7 项核验通过：计高≡绘制同源 ✓ / 旧几何标识符**零残留**（含注释亦无正式写法）✓ / `FloorDeviceDetailWindow` 同名常量未误删 ✓ / 溢出「+N」全链路按 **cap 后集合**计算 ✓ / 钳位位置正确且**充分**（12 处消费面逐核，`UsesColonyEconomyPage` 分支不读该字段）✓ / P2-2 红线（9 catch↔9 fallback、零闭包、无空 catch、`EndScrollView` 配对）✓ / `DrawFloorTabButton` 交互未破坏 ✓。例外 = 1 个 **P2 已当场收口**：降级路径复用 `OverflowTip` 会撒谎（文案承诺"含「编排」"）⇒ 改为**仅在未被能力裁剪时**挂该 tooltip |
| PERF | **PASS WITH EXCEPTION**（独立复核） | 缓冲复用正确（扩容仅由 `count` 驱动、6↔7 翻转无反复分配、无陈旧读取）✓；P2-2 **零闭包**（全文件 `=>`/`delegate`/`Action`/`Func` = 0）✓；零 Tick 注册、零反射；新 catch 全走 5s 节流日志 ✓。例外 = 运行期**无采样**（沙箱无游戏进程），仅静态可复核指标；`Translate` 字符串与 `int`→`NamedArgument` 装箱为**既有**形态（非本批新增），登记为 P3 |
| TEST | **PASS（离线）**／实机**未执行** | 独立复核**实跑**：`compile_with_sdk.ps1` ⇒ `EXITCODE=0`、`error CS=0`、`warning CS=0`；`dotnet test Tests/PersonalChronicle.Tests.csproj -c Release` ⇒ **548 通过 / 0 失败 / 1 跳过（共 549）**，与最近基线（`CHG-20261006-T1FLAT`）逐位一致；`Languages/**/Archive.xml` mtime 早于本批源文件且本批引用的 **115 个**键双语全在 ⇒ **零触碰核验成立**；失败隔离路径**不可离线单测**（IMGUI + 无合法异常注入点），实机注入方案已由复核给出 |
| DELTA | **PASS** | `gate-runner.ps1 -Base HEAD -ExcludeFromFile .codebuddy/tmp/tabunify-out-of-scope.txt -Blocking` ⇒ `PASS — 标注覆盖 10 个改动文件，无遗漏/超范围/残留`，`EXITCODE=0`。范围外清单自检：A=1414 / 范围外=1404 / **本轮恰 10 文件 = `files:` 声明数** |

## 八、残留与风险（照实登记）

1. **实机未执行**（沙箱无法启动游戏）：需 PM 复核 —— ① app 路径 7 页签全显 / 窄窗出现「+N」+tooltip /
   编排画布拖拽命中无偏移；② 降级路径 6 页签（编排隐藏）+ 残留钳位；③ 关窗重开 / 切中心 / 切档往返；
   ④ 单面板异常注入（若无法安全注入须记「未执行 + 理由」，不得伪造 PASS）；
2. **+3px 净位移**（3a 几何统一的结果，总控 R6）：栏高 27→26 且底距 4f 保持，hazard 前位置随之微移，
   待与预览 HTML 对账（LAND-001 先量后写）后如有异议再裁；
3. **P2-2 未覆盖 `ActivePaneHeight`**（总控 R5）：该函数（高度计算）与 `DrawAppFloorPanel` 其余绘制仍可
   整页中止 —— 登记为可选二期；
4. 3c 的 Bridge/DataLayer 计数为**声明值**，与运行期动态块数可能不同（见 §三 已知边界）；
5. 本轮改动叠加在**未跟踪**的制造中心模块之上；`ManufacturingEventsPanels.cs` 同文件另含
   `CHG-20261006-EVENTSTIME` 的改动（本批在其上方插入 6 行 ⇒ 已同步修正该标注行号为 `199-246`）。
6. **【ARCH 强制记账 · 承接存量债】**（独立复核 P2-1）：`MasterControlTabWindow.AppShell.cs` 实测 ≈1716 行，
   超 ARC-013「渲染密集 ≤1200」约 43%，本批又在同一文件净增 ≈70 行。该项**非本批肇因**
   （2026-09-15 审计已在案 >1309 行；`EXC-2026-011` R-10/C-1 明示为"未登记的存量违规"）；
   复核裁定 **本批接受 + 强制记账**，三项缺一不可：
   a) 将本批增量补登记，承接并解除 R-10/C-1 的"未登记"状态；
   b) 排期 partial 切片（建议把 `DrawActivePane` 区块与 app tab bar 区块切成新 partial，零契约改动）；
   c) **硬性触发器：下一次修改 `AppShell.cs` 的批次必须先切片（先切后改）；未切片再增量时，GATE-ARCH 应判 BLOCKED。**
7. **【存量循环依赖 · 登记待破环】**（独立复核 P2-2）：`MasterControl ⇄ Manufacturing.UI` 存在**存量** UI↔UI
   循环（正向：`AppShell.cs` / `Detail.cs` / `MasterControlTabWindow.cs` / `FloorTabs.cs` 对 Manufacturing.UI 的调用；
   反向：`ManufacturingEventsPanels.cs` 对 `MasterControl.Population.PersonnelArchivePanel` 的调用）。
   **本批未新增任何方向**（反向引用行未被触碰）。破环建议：把事件文本渲染助手从 `MasterControl.Population`
   移到共享层或抽为契约，或登记为受控例外。
8. **其余复核 P3（登记，本批不处置）**：① 极端窄窗下「+N」自身也放不下时仍静默（**非本批引入**，
   2026-10-02 P1-9 既有实现）；② 钳位重置的是页面滚动 `contentScroll` 而非 `.fpanes` 区内 `tabPaneScroll`
   （与点击切页签同口径）；③ 每帧残余分配（`Translate` 字符串 + `int`→`NamedArgument` 装箱 ×7）属**既有**形态；
   ④ 设计文档 `系统中心蓝图/待落地项/制造系统中心_RUIF改造方案.md` 仍以已删除的 `DrawFloorTabBar` 为锚点
   （文档残留，非代码残留）。

## 九、Changelog 措辞（用户向，不含行号）

- 制造中心 ② 面板底栏页签合并为**统一实现**，窄窗口时末尾页签会压缩显示并给出「+N」提示，不再静默吞掉；
- 降级视图（车间未划定 / 数据未就绪）不再显示无法使用的「编排」页签，且不会停留在空白页；
- 单个面板绘制异常时**只降级该面板**为提示块（并在日志留痕），不再让整页内容当帧消失。
