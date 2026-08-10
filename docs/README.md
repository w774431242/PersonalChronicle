# docs — 设计与落地文档索引

本目录存放设计方案、UI 预览与执行清单，**不是运行时资源**（游戏不会加载）。

| 文档 | 状态 | 说明 |
|---|---|---|
| **[档案馆Tab重构优化方案.md](./档案馆Tab重构优化方案.md)** | 📋 **v3.1** 待确认后开工 | 覆盖评估 · 5 Tab · 概览加厚 · 生涯加深 · 去统计 |
| **[ui-preview/archive-ui-preview.html](./ui-preview/archive-ui-preview.html)** | ✅ **v3.1 可交互方案示例** | 概览/编年/生涯/战斗履历/社会关系；武器 4 Tab |
| [档案馆UI落地清单.md](./档案馆UI落地清单.md) | ✅ 历史落地 | v2.1 任务史；其中部分活读 Tab 方向被 v3.0 废止 |
| [统计活读化修复方案.md](./统计活读化修复方案.md) | ✅ A1–A4 + B1 已落地 | 首页双轨统计（与详情 Tab 解耦，仍有效） |
| [工时强度评价设计.md](./工时强度评价设计.md) | ✅ **v4.0 已落地** | 强度五档 / 同工种站位 / 技能能力附属 |
| [高拓展高接入架构方案.md](./高拓展高接入架构方案.md) | ✅ **v4.3 当前架构** | Provider 注册表 / Read Model 隔离 / 公共接入契约 |
| [WorkIntensityIntegration.md](./WorkIntensityIntegration.md) | ✅ | 工时 Provider 与采样示例（英文） |

## 子目录

| 目录 | 内容 |
|---|---|
| `ui-preview/` | 各方案可交互 HTML mockup（A–H 方案对比、档案馆 UI 总览） |
| `ui-preview-equipment/` | 装备「完整继承使用链」独立调试沙盒（copy 自完整预览，聚焦 Custody Tab：每任持有者 + 击杀数 + 第一任标记） |
| `combat/` | 战斗履历设计文档（`设计文档_战斗履历.md`）+ UI 预览（`preview_combat.html`）+ 击杀图鉴卡片方案（`方案_击杀图鉴卡片.md`，评审中） |
| `preview-reports/` | 首页总览(B+E) 与 报告功能的设计真源（`design-home-overview-and-reports.md`）+ 可视化 mockup（`home-and-reports-preview.html`） |
| `overview/` | Pawn 个人档案 Overview 子页：**设计文档**（`设计文档_Pawn概览.md`）+ **布局重构方案**（`方案_概览重构设计.md`，A/B/C）+ **内容机制重构方案**（`方案_概览内容机制重构.md`，M1–M3）+ **五区块内容重置方案**（`方案_概览区块内容重构.md`）+ 沙盒（`preview_pawn_overview.html`）+ **布局示例**（`examples/overview-a-linear.html`/`overview-b-two-column.html`/`overview-c-cards.html`）+ **内容机制示例**（`examples/content-milestones.html`/`content-blurb.html`/`content-keyevents.html`）+ **五区块重置示例**（`examples/rich-1-lifecycle-career-footprint.html`/`rich-2-milestones-keyevents.html`/`rich-3-archive-page.html`） |

## UI 预览怎么用

1. 用浏览器打开 `docs/ui-preview/archive-ui-preview.html`（双击即可）。
2. 顶部可切换：首页 / 全部档案 / 人物 / 武器 / 事件；支持中英文案。
3. 「标注组件」会叠加 C# 绘制方法名（`DrawHomeStats`、`DrawTabBar` 等），方便对照改 `ArchiveMainTabWindow.cs`。
4. Tab 色点：绿=快照 Real · 蓝=活读 Live · 橙=Partial · 灰=Placeholder。

## 仍打开的工作项

1. **LE-1**：`ArchiveMainTabWindow` god class 拆分（架构级，独立排期）
2. **C1/C2 登记项**（见统计方案）：不新增出生类 Harmony；`LeftTick` 离开字段 defer

## 代码分层速查

| 层 | 路径 | 职责 |
|---|---|---|
| UI | `Source/.../Archive/` | MainTab 窗口与绘制 |
| Application | `Source/.../Application/` | `IArchiveService` 读契约、活读缓存 |
| Domain | `Source/.../Domain/` | 对象模型、Def、扫描谓词 |
| Data | `Source/.../Data/` | GameComponent、Scribe、reconcile 写路径 |
| Capture | `Source/.../Capture/` | Harmony 采集（read-only + try/catch 降级） |
