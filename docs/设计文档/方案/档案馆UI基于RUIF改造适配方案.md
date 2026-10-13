# 档案馆 UI 层基于 RUIF 改造：适配方案

> 日期：2026-09-10 · 状态：草案（待拍板 M1 接线时点）
> 范围：`Source/PersonalChronicle/Archive/`（UI 层）→ `RUIF.*` 迁移
> ⚠️ **路径基准（2026-09-12 起）**：本文所有 `RUIF/…` 路径均指**独立仓库根** `E:\SteamLibrary\steamapps\common\RimWorld\11\RUIF`（已移出本仓库，与 `PersonalChronicle` 平级），**不再**是本仓库子目录。本文为历史草案（实际执行走 `系统中心蓝图/待落地项/制造系统中心_RUIF改造方案.md`）。
> 上游约束：`RUIF/docs/落地管线方案.md` §5-R5、§7（I0–I4 零耦合 / I5 首个消费者接入）

---

## 1. 现状扫描结论

### 1.1 档案馆 UI 层（消费侧）

| 层 | 文件 | 规模 | 说明 |
|---|---|---|---|
| L1 令牌层 | `Archive/UI/UITheme.cs` | 460 行 | ~40 颜色 token + 间距/字号/度量；3 套自定义主题（Epic/Gothic/Steampunk）；`Apply` 联动刷新转发壳 |
| L2 组件层 | `Archive/UI/UIComponents*.cs`（4 个 partial）+ `UITextureLibrary.cs` | ~1,180 行 | 25 个组件成员（见 §3 映射表） |
| 兼容壳 | `Archive/ArchiveUiStyle.cs` | 178 行 | v4.5 转发壳：~50 token 转发 + 少量档案专属绘制（SectionMarker/SelectedNavigation/FactionCodex） |
| 调用方 | `ArchiveMainTabWindow.*`（~25 partial）+ `ITab_Pawn_*` + `Dialog_*` 等 | ~21,000 行 | 40+ 文件 |

**调用点统计（grep 实测）：**
- `UIComponents.*` 调用 ≈ **494 处**（Label 204 / TruncateToWidth 41 / TintedBox 39 / KpiBar 30 / StatCell 26 / Border 26 / KpiRow 24 / SectionTitle 14 / Panel 13 / Rule 12 / Badge 11 / KpiCell 10 / Card 9 / ProgressBar 8 / Pill 8 / 其余零星）
- `UITheme.*` 调用 ≈ **700+ 处**（Muted 110 / FontLabel 83 / Text 79 / CardPadX 65 / Dim 62 / BorderSoft 46 / Accent 44 …）
- `ArchiveUiStyle.*` ≈ 120 处（壳注释自述）
- 裸 `GUI.color` / `Widgets.DrawBoxSolid` / `Text.Font` 直调：**36 个文件**（迁移红线清理对象）

### 1.2 RUIF（框架侧）

- 已落地：`RUIF/Source` 37 文件，`RUIF/Assemblies/RUIF.dll` 已编译；Core/Draw（`Scope` 栈式配对、`WithAlpha`、`Box/Border/LineH/LineV/Label`）、Layout（`VStack/HStack/Grid/Split/BeginScroll/Inset`）、Theme（`Palette` 16 色 struct + `Register/Apply`，内置 vanilla/dark）、Components 26 文件（Text/Panel/Table/Nav/Input/Data/DataEx/Charts/Feedback/Progress/Status/Interact/Visual/Industrial 等）、DevTools Gallery。
- 关键缺口：Palette 仅 16 色；**无 Badge / Pill / Card(可点击) / KPI 网格 / 字符串截断** 的直接对应。
- 管线约束：`落地管线方案.md` 明确 **PC 在 I5 前不引用 RUIF**（风险 R5：双工程耦合过早）；I5 = 首个消费者接入，"另行排期"。本方案即该排期的具体化。

---

## 2. 总体策略

**三原则：**

1. **令牌单一真源先行，组件迁移殿后。** 先把颜色/间距的真源从 `UITheme` 切到 `RUIF.Theme`（调用方零改动），再按调用频次逐个替换组件——每一步都可编译、可回滚、可目测。
2. **语义色不进框架。** Timeline*/Pill*/Faction*/TitleTier* 等 ~24 个档案专属令牌**不上推到 RUIF Palette**（违反通用性红线），改为 PC 侧「语义派生层」：全部从 RUIF 16 令牌 + `WithAlpha`/`Series` 派生（派生规则见 §4）。
3. **复合组件双向流动。** RUIF 已有的直接换；RUIF 没有且通用价值高的（KPI 网格、Badge/Pill）反哺回框架；纯档案语义的（TimelineNode、PairCard）留在 PC 侧作为 L3 之上的复合件。

**时点决策（需拍板）：**
- **方案 A（推荐）**：M0 现在就做（零耦合，符合管线）；M1 接线等 RUIF I3/I4 收口后执行。
- **方案 B**：提前耦合——RUIF 当前已具 I1–I2 能力（Layout/Theme/组件/Gallery 可用），若你接受 API 未冻结的风险（I2→I3 可能改签名），M1 可立即执行。同一作者双仓同开发，返工成本可控。

---

## 3. 组件映射表（PC UIComponents → RUIF）

| PC 成员（调用数） | RUIF 对应 | 策略 | 批次 |
|---|---|---|---|
| `Label` (204) | `Draw.Label(rect,text,font,color,anchor)` | 直接替换 | B1 |
| `TruncateToWidth` (41) | 无直接等价（`TextAtoms.OverflowFade` 是溢出淡出绘制，语义不同） | **暂留 PC**（或上推框架） | — |
| `TintedBox` (39) | `Draw.Box` + `Draw.WithAlpha` | 直接替换 | B2 |
| `KpiBar` (30) | 无对应 | M3 贡献回框架 | B5 |
| `StatCell` (26) | `PanelAtoms.StatRow` / `DataAtoms.StatTable` | 替换（参数形态适配） | B2 |
| `Border` (26) | `Draw.Border` | 直接替换 | B1 |
| `KpiRow` (24) / `KpiCell` (10) / `SixGrid` (1) | 无对应（`Layout.Grid` + `StatRow` 可组合） | M3 贡献回框架 | B5 |
| `SectionTitle` (14) / `DrawSubsectionHeader` (3) | `PanelAtoms.SectionBar`（带 count/expanded，更强） | 替换 | B2 |
| `Panel` (13) | `Shapes.InsetPanel` / `Draw.Box` | 直接替换 | B1 |
| `Rule` (12) | `Shapes.RuleSplit / RuleWithLabel / Draw.LineH` | 直接替换 | B1 |
| `Badge` (11) / `Pill` (8) | 无直接对应 | **暂留 PC，候选上推** | B4 |
| `Card` (9) / `ClickableCard` (3) | `Shapes.InsetPanel` + 交互原子（无现成可点卡片） | 组合实现；候选上推 | B4 |
| `ProgressBar` (8) | `ProgressAtoms.CapacityBar` / `VisualAtoms.GradientBar` | 替换 | B2 |
| `KpiGap` 等度量 | `Theme.GridGap/BlockGap` | 令牌对齐 | M1 |
| `TimelineNode` (3) | `DataAtoms.TimelinePro`（更强但 API 形态不同） | 评估后重构 | B6 |
| `PairCard` (0) / `ShadowLabel` (1) | 无对应 | 暂留 PC | — |
| `TableHeader` / `ZebraRow` | `TableAtoms.HeaderCell / ValueCell / RowHeight` | 替换 | B3 |
| `EmptyState` | `FeedbackAtoms.EmptyState` | 直接替换 | B3 |
| `SidebarItem` / `ThemedButton` | `NavAtoms`（TabBar/SidebarCollapse）+ `InteractAtoms` | 核实签名后替换 | B3 |

**批次原则**：B1（纯几何/文本原语，~290 处，风险最低）→ B2（数据单元）→ B3（表格/导航/反馈）→ B4（卡片/Pill/Badge，含上推评估）→ B5（KPI 反哺框架）→ B6（Timeline 重构）。每批：grep 清零该成员 → 编译 → Gallery 三主题目测 → 删对应转发。

---

## 4. 令牌派生规则（语义层设计）

新建 `Archive/UI/ArchiveSemantic.cs` 取代 `ArchiveUiStyle` 的 token 转发职责（FactionCodex/SectionMarker/SelectedNavigation 绘制逻辑并入）：

| PC 令牌 | 派生自 RUIF | 说明 |
|---|---|---|
| Window/Panel/PanelRaised/Card/CardHover/Border/BorderSoft/BorderHair/Text/Muted/Dim/Accent/Info/Alive/Dead/Warn | 同名 1:1 | 直通 |
| SecondaryText | `Muted` | 合并 |
| AccentSoft / InfoSoft / OverlayWhite04 / BadgeIncompleteFill | `WithAlpha(Accent/Info/白/BorderSoft, …)` | 半透明族 |
| Threat / Blood | `Warn` / `Dead` | 语义映射 |
| FactionAlly / FactionHostile / FactionNeutral | `Alive` / `Dead` / `Muted` | FactionCodex 派生不变 |
| PillBlue/Gold/Red/Green | `WithAlpha(Info/Accent/Dead/Alive, 0.16)` + 同色文字 | 半透明底+彩字 |
| TimelineSpine/Join/Death/Battle/Social/Craft/Built/Other | `BorderHair` / `Alive` / `Dead` / `Warn` / `Info` / `Accent` / `Alive`(变体) / `Muted` | 8 色需稳定性，见风险 R3 |
| TitleTier0–4（readonly） | 保持字面值 | 头衔品阶色是游戏数据语义，非 UI chrome（RUIF 白名单精神） |
| StateColor / TintForEventKind / MedalTierColor | 保留函数，内部改引派生令牌 | 逻辑不动 |

**主题注册**：`UITheme` 的 Epic/Gothic/Steampunk 三套 Palette 在 PC 静态构造期经 `Theme.Register("pc-epic", …)` 等注入 RUIF；`UITheme.Apply(id)` 改为转发 `Theme.Apply("pc-" + id)`；`RefreshFromTheme` 改为从 `RUIF.Theme` 读回派生。三主题在 RUIF Gallery 中自动可选（`RegisteredIds`）。

---

## 5. 分阶段计划

| 阶段 | 内容 | 交付/门禁 |
|---|---|---|
| **M0 映射审计**（零耦合，可立即做） | 冻结 §3/§4 映射表；核实 B3/B4 涉及的 RUIF 签名（SidebarItem 对应、ClickableCard 组合件）；产出 `ArchiveSemantic.cs` 派生规则原型（不接线，纯设计） | 映射表定稿；签名核实清单 |
| **M1 接线**（= 管线 I5 时点） | csproj 增 `RUIF.dll` 引用（`Condition Exists`）；`About.xml` 声明 `<loadAfter>RUIF</loadAfter>`；三主题经 `Theme.Register` 注入；`UITheme.Apply/RefreshCurrent` 改造；`ArchiveSemantic.cs` 上线 | 编译 0 错 0 警；三主题切换目测与改造前逐屏一致（像素级回归基线）；`validate_mod.py` 0 P1；Player.log 无异常 |
| **M2 组件迁移 B1–B3** | 按 §3 批次替换调用方，逐批删 `ArchiveUiStyle`/`UIComponents` 对应成员；36 文件裸 `GUI.color` 顺手改 `Draw.Scope` | 每批 grep 清零 + 编译 + 目测；B1–B3 完成后 UIComponents 仅剩暂留成员 |
| **M3 复合组件归位** | KpiGrid（SixGrid/KpiRow/KpiCell/KpiBar，~75 处）与 Badge/Pill、可点 Card 上推 RUIF（通用化去档案语义）；PC 改消费框架版 | RUIF 侧新增组件进 Gallery；PC 调用方切换 |
| **M4 收口** | 删 `ArchiveUiStyle.cs`（转发清零后）；`UITheme.cs` 仅剩主题注册/切换逻辑；TimelineNode 迁 `TimelinePro` 评估结论落地；docs 同步 | `UI/` 目录收敛为「主题注册 + 档案专属复合件」两个文件级单元；全量回归 |

**回滚保障**：M1 前打 tag；每批独立 commit；`ArchiveUiStyle` 转发壳保留至 M4，任何批次可单独 revert。

---

## 6. 风险登记

| # | 风险 | 缓解 |
|---|---|---|
| R1 | RUIF API 未冻结（I2→I3 可能改签名），提前耦合返工 | 方案 A 等收口；或 M1 冻结当前 dll 版本号，变更时人工 diff |
| R2 | 700+ 令牌调用点漏改产生静默漂移 | M1 后 `ArchiveUiStyle.RefreshFromTheme` 保留一次性审计断言（dev 构建检查 forwarded 值 == RUIF 派生值） |
| R3 | Timeline 8 色 一对一映射后语义撞色（Join 与 Built 都映射绿） | 派生表预留 `WithAlpha` 明度变体；M1 目测基线重点盯 Timeline 屏 |
| R4 | 21k 行调用方一次性回归成本高 | 严格按 B1–B6 批次，每批一个可目测的页面组（Home→Overview→PawnDetail→Reports） |
| R5 | 双 mod 分发：玩家未装 RUIF 则 PC 崩 | About.xml `loadAfter` + 启动时 `ModLister` 探测 RUIF，缺失时打日志并按 Harmony 模式提示（与 RUIF 发布收口 I4 联动） |
| R6 | Dev 文件（DevTestButtons/DevSelfTest/DevCoverageProbe ~2.2k 行）混入迁移面 | 排到最后一批或不迁（dev 专用，不进发布质量口径） |

---

## 7. 待拍板事项

1. **接线时点**：方案 A（等 I5 收口）还是方案 B（立即 M1）？
2. **KpiGrid / Badge / Pill 上推方向**：确认按 M3 反哺框架（推荐，框架本就沉淀自 PC），还是长期留在 PC 侧？
3. **TruncateToWidth**（41 处）：上推 RUIF 作通用截断助手，还是暂留？
4. **Timeline 色映射**：§4 提案表是否接受，或需保留独立 8 色（即 PC 注册主题时扩展字典，不走纯派生）？
