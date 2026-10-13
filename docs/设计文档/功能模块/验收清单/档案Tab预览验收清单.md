# 人物视窗「档案」Tab 预览验收清单（基于 archive-tab-preview.html）

日期：2026-08-10 ｜ 模块：v4.15 浓缩方案（六宫格 + 横向时间轴）｜ 版本：1.1.1
基准：`docs/archive-tab-preview.html`（HTML 视觉基准）

## 验收前置
- [x] `dotnet build -c Release` 0 警告 0 错误（已通过）
- [x] dll 已同步发布版 `Mods/PersonalChronicleVer0.1/Assemblies`（已通过）
- [ ] 已**重启 RimWorld** 加载新 dll 实测（未做）

## 验收项（逐项对照预览 ↔ C# 落地）

### A. 布局结构（预览：身份条 + 六宫格 + 横向时间轴 + 按钮）
- [x] **A1 身份条**：3D 头像 + 姓名 + 领袖/在世 Pill + 在职状态 → C# `ITab_Pawn_Chronicle.DrawHeader` 用 `Widgets.DrawTextureFitted(PortraitsCache.Get)` + `Pill`（无硬编码文案，走 Keyed）。✅ 已落地
- [x] **A2 六宫格 3×2**：工时/产出/击杀/战役/足迹/关系 → C# `SixGrid` + `BuildCells`（6 个 `KpiCell`）。✅ 已落地
- [x] **A3 底部按钮「打开完整档案」** → C# `DrawFooterButton`。✅ 已落地
- [x] **A4 时间轴区块为视觉主轴** → 本次改造重点（见 C 节）。

### B. 六宫格数据零硬编码（用户硬约束）
- [x] **B1 数据来源**：全部来自 `DetailSnapshot` 快照（`WorkIntensity`/`Kills`/`BattleCount`/`LegacyOffspring`/`ProductionTotal`），窗口不聚合、不直读 `ChronicleGameComponent`。✅
- [x] **B2 标题走 Keyed**：`UI.Kpi.Work/Prod/Kill/Battle/Foot/Rel`。✅
- [x] **B3 单位/副标题走 Keyed**（本次整改）：原硬编码「件/击杀/战役」→ 改为 `UI.Kpi.Unit.Pieces/Kills/Battles`（`.Translate()`），中/英 Keyed 已同步。✅ 已落地
- [x] **B4 语义色单点映射**：`UITheme.TintForEventKind(KindKey)`（work→PillGold / prod→PillGreen / kill→PillRed / battle→PillBlue / foot→Info / rel→Alive），窗口内无 if/switch 散落。✅

### C. 时间轴横向滚动 + 凸出横轴（预览核心诉求，本次落地）
- [x] **C1 单条横轴贯穿**：原 C# 为"横向铺排 + 换行到下一行 + 纵向滚动"（`DrawMilestones` 算 rows、`HorizontalTimeline` 每行画 spine）。→ 改造为**单条横轴贯穿全程**，`HorizontalTimeline` 去掉换行逻辑，一条 `Widgets.DrawLine` 横轴 + 节点骑轴。✅ 已落地
- [x] **C2 横向滚动**：`DrawMilestones` 改为 `contentW = nodes.Count*(TlNodeW+TlGapX)+TlGapX`（单行），`inner.height = min(singleRowH, TimelineScrollH)`，`BeginScrollView` 在 `contentW > view.width` 时出现**横向滚动条**。✅ 已落地
- [x] **C3 凸出横轴**：横轴用 `UITheme.TimelineSpine`（`DrawLine` 3px），节点 `dot` 骑在 `spineY` 上（`dot.y = spineY - 5.5f`），文案从 `spineY + 12f` 垂下。✅ 已落地
- [x] **C4 节点语义色**：沿用 C# 既有 `Timeline*` 体系（`TimelineJoin/Death/Battle/Social/Craft/Built/Other`），由 `BuildTimelineNodes` 经 `TimelineColorFor(kind)` 着色——与预览 HTML 的 6 色示意不同源，但属真实里程碑 kind 映射，保留。✅
- [ ] **C5 游戏内实测**：横轴渲染、节点骑轴、横向滚动条、超量里程碑（>每行容量）不挤兑窗口。⏳ 待重启游戏验证

### D. 标准规范化样式
- [x] **D1 token 化**：预览语义色对应 C# `UITheme` 令牌（`--tone-*` ↔ `UITheme.*`），无窗口内 `new Color`。✅
- [x] **D2 配对恢复**：`HorizontalTimeline` / `SixGrid` 均已 `prevColor/prevFont` + `try/finally` 配对。✅
- [x] **D3 中文行高**：`TlRowH=64f ≥ 64`（StatCell 经验值）；`SixGridH=234f=2×112+gap`。✅

## 本次落地处理摘要
1. `ITab_Pawn_Chronicle.DrawMilestones`：换行纵向滚动 → 单轴横向滚动（`contentW` 按节点数计算，固定单行高度）。
2. `UIComponents.HorizontalTimeline`：去掉 `if(x+TlNodeW > rect.width) wrap` 分支与逐行 spine，改为一条贯穿横轴 + 节点骑轴。
3. `ITab_Pawn_Chronicle.BuildCells`：六宫格副标题硬编码「件/击杀/战役」→ `UI.Kpi.Unit.*` 翻译键（上轮已做，本清单登记）。
4. 编译 0 错 0 警；dll 同步发布版。

## v4.15.1 视觉增强（基于游戏截图反馈，2026-08-10）
**问题**：游戏内时间轴渲染与预览差距大——横轴只是灰线、dot 是方框无光晕、区块无背景区分，整体太"平淡"，不够凸出时间轴风格。
**修复**：
1. **区块容器**（`DrawMilestones`）：加 `UITheme.Panel` 深色面板背景 + `UITheme.AccentSoft` 2px 顶高光线，让时间轴区块从周围内容中凸出。
2. **横轴线条**（`HorizontalTimeline`）：从 `UITheme.TimelineSpine`(灰) 改为 `UITheme.Accent`(金)，3px 粗——醒目的金色主轴贯穿全程。
3. **节点光晕**：dot 后面画 16×16 半透明方框（alpha=0.25）模拟发光效果，前面 11×11 实心色方框骑在轴上。
4. **连接竖线**：从 dot 底部到文字顶部画 1px `BorderSoft` 竖线，让文字像"挂在轴上"的卡片。
5. **引擎约束修复**：`Widgets.DrawCircle` 不存在 → 用 `DrawBoxSolid` 方框替代；`UITheme.Panel2` 不存在 → 用 `UITheme.Panel`。

## 待办
- [ ] 重启 RimWorld 实测 C5（横轴渲染 / 横向滚动 / 溢出不挤兑）。
- [ ] 预览 HTML 时间轴节点色为 6 色示意，与 C# `Timeline*` 体系不同源；如需预览更贴近，可将 HTML 节点色改为映射 `Timeline*` 语义（Join绿/Death红/Battle蓝/Social青/...）。可后续对齐。
