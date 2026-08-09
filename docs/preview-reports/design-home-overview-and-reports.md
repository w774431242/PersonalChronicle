# 首页总览（B+E 混合）与 日报/周报/月报/季报 — 设计开发方案

> 范围：首页总览改版 = **B 方案（KPI 仪表盘）为默认底色 + E 方案（编年史时间轴）可切换**；
> 日报/周报/月报/季报 = **独立新功能**（不在首页，单独入口）。
> 状态：**阶段 1 首页双视图已按 B 方案 KPI 仪表盘落地并修正字体重叠/时间轴布局问题**（2026-08-09 晚间补丁）；日报/周报/月报/季报（阶段 2）待开发。

---

## 0. 现状核查（数据源已 100% 就绪）

| 能力 | 现状 | 用途 |
|---|---|---|
| 事件模型 | `ChronicleEvent { Tick, TypeKey, ImportanceLevel, Primary, Subjects, Params }` | 时间轴/报告的最小单元 |
| 重要性 | `ChronicleImportance`：Routine=0 / Normal=1 / Important=2 / Critical=3；`Resolve(ev)` 可判定 | 时间轴筛"关键节点"、报告抽"关键事件" |
| 事件类型 | `Join / Death / Built / Crafted / Battle / Social`（常量见 `ChronicleEventType`） | 时间轴节点图标/颜色分类、报告聚合维度 |
| 全量事件流 | `ChronicleGameComponent.Events`（真相源 `List<ChronicleEvent>`）；`GetRecentEvents(count)` 按 Tick 降序 | 时间轴（升序排）、报告（按窗口筛）取数基础 |
| KPI 实时 | `GetLiveColonistCounts(out free,out slave,out prisoner)`、`GetLiveColonistCount()` | B 方案实时指标组 |
| KPI 快照 | `GetObjectsOfCategory(key)`、`GetActive/ArchivedSnapshotCount()` | B 方案历史档案库、F 等 |
| 周期换算 | `GenDate.TicksPerDay=60000`、`TicksPerYear=3_600_000`、`TicksPerQuadrum=900_000` | 报告周期窗口、时间轴"第 N 天" |
| 接入锚点 | `WorkTime.FirstObservedTick`（per-pawn）；全局起点取 `events.Min(Tick)` 或组件首记录 tick | 时间轴起点、报告无需成立时间 |

**结论：数据源全部现成，无需新增任何 Harmony 捕获点；本次仅需在 Application 层加"查询/聚合"，在 Archive 层加"渲染"。**

---

## 1. 架构设计

### 1.1 首页双视图（B 底色 + E 切换）

新增枚举 `HomeViewMode { Kpi, Timeline }`，`ArchiveMainTabWindow` 持有字段 `homeViewMode`（默认 `Kpi`）。

- `DrawHomeContent` 改造：页头下方画一行视图切换 tab（KPI 仪表盘 / 时间轴），点击切换 `homeViewMode` 并重建缓存。
- 分派：
  - `Kpi` → `DrawHomeKpi`（**B 方案**）
    - 实时指标组（绿框强调）：当前殖民者（含自由/奴隶/囚犯拆解）、在役天数、在世殖民者
    - 历史档案库组（auto-fit 小卡）：殖民者 / 物品 / 战斗 / 地点 / 已归档
    - 双栏：最近历史 + 重要档案（**复用现有** `DrawRecentHistory` / `DrawImportantArchives`，不改写）
  - `Timeline` → `DrawHomeTimeline`（**E 方案**）
    - 中轴 spine + 左右交错节点（按 Tick 升序）
    - 节点筛选：`ChronicleEventImportance.Resolve(ev) >= Important` 作为"关键生命线节点"（其余可折叠/省略，防大存档卡顿）
    - 类型→图标/颜色映射：`Join`=人口、`Death`=骷髅(红)、`Built/Crafted`=锤(绿)、`Battle`=剑(红)、`Social`=心(紫)
    - 节点点击 → `OpenEventDetail` / `NavigateTarget` 跳事件详情
    - 顶部"查看完整年表"入口（如节点过多）

- **偏好持久化（已定）**：`homeViewMode` 存入 `ChronicleGameComponent.ExposeData`，默认 `Kpi`，跨会话记忆，旧档读不到时安全回退 `Kpi`。

### 1.2 日报 / 周报 / 月报 / 季报（独立功能）

- 入口（**已定**）：侧边栏"快捷"区把占位的"报告"落地为真实入口，复用现有 `ToolsPlaceholder` 位置（**不**新增主视图 tab，不动主导航结构）。内部仍以 `MainView.Report` 承载视图状态。
- 周期定义（mod 内常量，基于 `GenDate`，**已拍板口径：现实日历映射**）：
  - 日 = `GenDate.TicksPerDay` (60000)
  - 周 = `TicksPerDay * 7`（现实周 7 天）
  - 月 = `TicksPerDay * 30`（现实月 ≈30 天）
  - 季 = `TicksPerDay * 90`（现实季度 ≈3 个月）
  - 年报：**不做**（报告周期只到季报；年报窗口 365 天 ≈6 个游戏年，语义偏怪，已定舍去）
- **视觉样式（已定）**：采用 **G · 殖民地日报** 头版样式——羊皮纸底色 + 衬线双栏排版；报头含刊名/期号/周期/天数，头条为本期重大事件，双栏分"人事动态"（加入/死亡/晋升/关系）与"战地·建造快讯"，中缝为聚合统计小条。参考 mockup：`docs/ui-preview/scheme-g-newspaper.html`。
- 报告视图：
  - 顶部 tab：日 / 周 / 月 / 季（含"上一期/本期"偏移选择）
  - 窗口锚点：`to = Find.TickManager.TicksGame`；`from = to - periodTicks * (offset+1)`
  - 内容（映射到报纸栏目）：
    - ① **头条**：本期 `Importance >= Important` 中最新/最重大的一条事件，规则生成标题与副标题
    - ② **中缝统计**：按 `TypeKey` 分组计数（Join/Death/Built/Crafted/Battle/Social）+ 当前人口/在役天数
    - ③ **双栏**：左"人事动态"（Join/Death/Social，写成加入/讣告/晋升/关系栏目）；右"战地·建造快讯"（Battle/Built/Crafted 事件，按 Tick 降序）
- 纯规则拼接摘要，**不引入 LLM / 外部依赖**。

### 1.3 模块 / 文件改动清单

| 文件 | 改动 |
|---|---|
| `Archive/ArchiveMainTabWindow.cs` | `enum HomeViewMode`；`homeViewMode` 字段+刷新钩子；`DrawHomeContent` 改派发+顶部 tab；**`DrawHomeStats` 重构为 `DrawHomeKpi`**（3 大实时指标卡 + 5 小历史档案库卡，B 方案）；新增 `DrawHomeMetricCard`；`DrawHomeTimeline`（E）修正节点/连接线布局；`MainView.Report` + `DrawReportContent`（阶段 2 待做） |
| `Application/IArchiveService.cs` | 新增 `GetAllEvents()`（供时间轴/报告取全量）；`GetServiceDays()`（B 方案在役天数）；`Get/SetHomeViewMode()`（偏好持久化）；阶段 2 再新增 `GetReport(ReportPeriod period, int offset) → ReportView` |
| `Application/ArchiveService.cs` | 实现上述查询方法（取 `component.Events` 全量 → 排序/筛选/聚合） |
| `Application/ArchiveReportViews.cs`（新增） | `ReportPeriod` 枚举（Day/Week/Month/Quadrum）；`ReportView` 聚合结果类（周期/窗口/各 TypeKey 计数/关键事件/摘要） |
| `Data/ChronicleGameComponent.cs` | `ExposeData` 加 `homeViewMode` 持久化；`GetAllEvents()`（或复用 `GetRecentEvents(int.MaxValue)`） |
| `Defs/` + `Languages/*/Keyed/` | 新增 key：`HomeKpiView`/`HomeTimelineView`/`ReportDay`/`ReportWeek`/`ReportMonth`/`ReportQuadrum`/报告标题/各类型标签/摘要模板 |

### 1.4 周期换算与锚点（明确写法）

```csharp
// 时间轴起点
long anchor = component.Events.Count > 0
    ? component.Events.Min(e => e.Tick)
    : Find.TickManager.TicksGame;

// 报告窗口（offset: 0=本期, 1=上一期…）
long to   = Find.TickManager.TicksGame;
long from = to - periodTicks * (offset + 1);
```

---

## 2. 开发步骤（分阶段，含验证）

- **阶段 0｜数据层补充**：`GetAllEvents()` + `GetReport()`/`ReportView` + 周期常量；编译验证（`dotnet build -c Release`）。
- **阶段 1｜首页双视图**：`HomeViewMode` + 顶部 tab + `DrawHomeKpi`(B) + `DrawHomeTimeline`(E) + 偏好持久化；游戏内切换验证。
- **阶段 2｜报告功能**：`MainView.Report` + 周期 tab + `DrawReportContent` + 聚合；各周期抽样验证计数正确。
- **阶段 3｜多语言与入口**：补全 `Keyed`；侧边栏"报告"入口落地（替换占位）。
- **阶段 4｜验收**：`dotnet build -c Release` + `validate_mod.py` + 游戏内实测（视图切换、四周期报告、旧档兼容）。

---

## 3. 风险与注意

- **IMGUI 手绘时间轴**：中轴+交错节点需 `Rect` 手动布局，窄屏须保证节点不溢出；限制关键节点数（`Importance >= Important`）+ "完整年表"入口，避免大存档卡顿。
- **全量事件性能**：`GetAllEvents` 复制整表；时间轴/报告属低频刷新（随 `DataRevision` 缓存），**绝不每帧跑**。
- **"月/周"口径**：RimWorld 原生只有 天/季/年，**无"月"**。不拍板则"月报/周报"语义含糊（见 §4）。
- **报告摘要**：纯规则拼接，不依赖外部；避免把"推断/研判"写进摘要（遵守数据权威性要求）。
- **持久化兼容**：`homeViewMode` 入 `ExposeData` 须给默认值 `Kpi`，旧档读不到该字段时安全回退。
- **缓存复用**：现有 `cachedRecentLines`/`cachedImportantCards` 刷新机制可复用；时间轴/报告在其刷新入口后重建。

---

## 4. 已拍板结论（2026-08-09 定稿）

1. **报告入口**：侧边栏"快捷"区的"报告"占位落地为真实入口（复用 `ToolsPlaceholder` 位置），不新增主视图 tab。
2. **首页默认视图**：`Kpi`（B 仪表盘），已按 3 大实时指标卡 + 5 小历史档案库卡落地；切换偏好跨会话记忆（存 `ChronicleGameComponent`）。
3. **周期口径（现实日历映射）**：日 = 1 天 / 周 = 7 天 / 月 = 30 天 / 季 = 90 天（均 `TicksPerDay` 倍数）。
4. **年报**：不做，报告周期只到季报（日报 / 周报 / 月报 / 季报 四档）。
5. **阶段 1 修正**（2026-08-09 晚间补丁）：职业生涯工种卡片字体重叠已修复；首页 KPI 仪表盘已严格按 B 方案实现；时间轴布局、节点与连接线绘制已修正。

> 阶段 1 已完成并修正。下一步：阶段 0/2（数据层 `GetAllEvents` + `GetReport`/`ReportView` + 周期常量）。
