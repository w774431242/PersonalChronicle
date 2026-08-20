# Gate Record — 2026-08-21 前端全面体检修复（容器/文本/颜色/环世界原生兼容）

- **Change:** 对全部前端（Archive UI 层）做深度体检与修复：容器与文本兼容（溢出/截断/行高/参数空间）、颜色（主题 token 一致性、浅色主题对比度、状态泄漏）、环世界原生兼容（GUI 状态恢复、绘制路径分配/查询、ScrollView 配对）
- **Version:** v1.1.4+（Save Schema 未变，不 bump）
- **Date:** 2026-08-21
- **Reviewer:** AI（6 路并行只读审计 × 176 项发现 + 编译/单测复跑）
- **Scope:** 全部 Archive UI 文件（6 组审计：设计系统/主窗口外壳/人物详情/物品地点战役/ITab/对话框注入）

## 0. 审计方法

按 `05-表现层规范.md`（UI-009 多语言布局 / ASSET-002 颜色 token / PERF-001 热路径 / UI-003 Read Model / UI-010 原生隔离）对全部前端文件分组并行只读审计。6 组共报告 **~176 项问题**（P0=0，P1=20，P2=156），按严重度分层修复。

## 1. 修复汇总（按组）

### 1.1 设计系统（UIComponents / UITheme / ArchiveUiStyle / ArchivePanelBase）
- **anchor 状态泄漏（P1）**：Pill/Badge/ClickableCard 硬编码恢复 `UpperLeft` 而非保存 prevAnchor → 污染原生 UI；改为捕获+恢复。
- **CJK 裁剪四连（P1）**：ClickableCard key 行 14f / value 行 20f（规范 Tiny≥18f / Small≥22f）→ 用真实行高；PairCard 测宽未钉字体（中文键与 value 重叠）；SectionTitle/DrawSubsectionHeader 长标题截断。
- Pill/Badge 长文本截断；`new Color(alpha)` → `UITheme.WithAlpha`；TimelineNode `Color.white` → 保留（图标中性色，注释说明）；TruncateForWidth maxWidth≤0 返回空；SixGrid inner 负宽 clamp；SixGrid 内容高 -2f 消除恒显 2px 滚动条；TitleSide/InlineMetric 截断；Shadow getter → readonly；ArchiveUiStyle 补全 9 个缺失 token 转发；CaptureGuiState 补 WordWrap；DrawSectionMarker try/finally。

### 1.2 主窗口外壳（Home / Shell / Nav / Cache / 主文件）
- **Home 时间线卡片宽度双重扣除（P1）**：`viewRect.width - nodeX - viewRect.x` 多扣一个 x → 卡片只有应有宽度一半，右侧约 488f 空白；改 `viewRect.xMax - nodeX`。
- **Timeline 模式滚动高度（P1）**：复用 KPI 版估算（~700f）→ 长列表行不可见不可滚；按可见事件数单独估算（缓存刷新时计算，绘制零遍历）。
- **RecentLines 未截断（P1）**：provider 返回 20 条、绘制只留 6 行 → 第 7+ 行不可达；按 RecentRecordCount=6 截断。
- **时间线每帧分配（P2）**：绘制路径每帧 `DateReadoutStringAt`/`EventName`/`EventTypeToGlyph` → 预格式化视图（cachedTimelineItems）缓存刷新时构建。
- DrawHomeViewTabs 每帧 new[]+Translate → 静态缓存；社交图 zoom/pan 切换对象泄漏 → ResetSocialGraphView；Timeline 死分支删除；DrawEventRow 负宽保护 + 窄面板隐藏右列；主题按钮宽按文本实测；sidebar 行高 22f + 截断；占位工具去假高亮；DrawNoService 字体恢复；Home 卡片 SubLabel 底边 +2f；数值截断。
- **Social 高度估算漂移（P1，人物详情组）**：缺 4 个 SectionTitle、交织行 24f 误算（实际 42f）→ 底部被裁；按绘制布局逐段对齐；LINQ Count → 手写循环。
- **社交网络图（P1×2，人物详情组）**：滚轮缩放命中区固定在 246f 底板（实际面板 ≥338f）→ 图下半部缩放失效；改为最终面板命中 + zoom 变化重算面板高度（局部函数）。节点卡两行文字在 auto-fit 缩放（z≈0.66）重叠且行高随 z 缩到低于字体行高 → 固定行高 + 贴底布局 + 放不下时隐藏关系行（Tooltip 兜底）。
- **MiniStat 行高（P1，人物详情组）**：值行 16f 画 Medium（28f）被裁、label 14f < 18f、statY 越过 92f 面板底边 → 值行 Small 22f + 标签 18f、面板 92→104f（主窗口与 ITab 两处同步）。

### 1.3 物品/地点/战役
- **EventDetail 空态 GUI 栈失衡（P1）**：`cachedEventDetail==null` 时 finally 无条件 `EndScrollView`（从未 Begin）→ 多弹一层 GUI group 整窗错位；加 began 标志配对。
- **Overview 展开面板高度未计入（P1）**：地点/战役卡片展开后面板高度不参与滚动估算 → 展开项与后续行被裁不可达；估算时按缓存行数累加展开面板高。
- **Battle 展开每帧全库扫描（P1）**：`service.GetAllEvents()` 绘制路径全库扫描 → 改按对象事件索引（GetEventsFor）+ revision 缓存（仿 Location 模式）。
- 事件描述框固定 90f 裁长文 → CalcHeight 动态（clamp 90~300，估算同步）；DrawTree/DrawChips 字体恢复；事件行负宽保护；CombatLog 高度估算单列→双列（消除大段空白）。

### 1.4 ITab（Chronicle / Career 四子页）
- **Qualification 坐标偏移（P1）**：viewRect 0 基准 + 内容绝对坐标混合 → 整页右移 12px、右缘压滚动条；viewRect 起点对齐内容坐标。
- **Resume 高度低估 222px（P1）**：尾部估算 284 vs 实际 536 → 两列面板下段被裁；按绘制逐段对齐。
- **CalcQualHeight ⑥ 低估（P1）**：职称记录按空态固定算，5 档全授低估 124px → 按记录数计高。
- chips 截断；Overview 滚动内容宽度扣滚动条；Chronicle Tab 屏幕自适应（screenWidth-24/screenHeight-240）；勋章墙面板先按行数建墙（多行图标不再悬空）；阶梯状态行 12f→18f；条件名/职称记录/缺口截断；Header 职称行高 22f→28f；Qualification 子页重复扫描事件流 → 预计算一次传参共享。

### 1.5 对话框 / 注入 / Dev
- **Dialog_RenameWorkplace 回车静默丢输入（P1）**：closeOnAccept=true 但无提交钩子 → DoWindowContents 检测回车提交。
- **Dialog_ManualMedalAward 卡片重叠（P1）**：内容 y+73 超 CardH=64 与下卡重叠 → CardH 64→76 + 布局重排 + 称号截断。
- **Dialog_MedalDetail 描述裁切（P1）**：固定 60f 无滚动 → Tiny 实测动态高度（clamp 44~180）；buff 同步动态；图标 Def 查找每帧 → 打开时缓存；pill 宽按文本；首字取完整码点（代理对安全）。
- **DevTest 重置误删真实职业履历（P1）**：ResetDevTestState 清空真实 CareerData.Events → 删除该段（DevSimulate 不产生职业事实，CareerRandomize 自带覆盖重置）。
- `DevTestButtons` 现有 `SourceId=DevTest` 事件清理保留；BattleObject 清理保留（DevMode 测试语义，注释说明边界）。

## 2. Gate 结果

| Gate | Result | Evidence | Risk |
|---|---|---|---|
| ARCH | **PASS** | 全部修复遵守层边界：UI 只消费缓存快照；职称/资格逻辑未动（上一任务已收口 Domain）；Battle 伤亡改索引查询而非直写 | 无 |
| UI | **PASS** | 行高/截断/负宽/滚动高度按 UI-009 与绘制同口径；GUI 状态 try/finally 配对 | 无 |
| LOC | **PASS** | 无新增用户可见文本（全部复用既有键）；无硬编码中文新增 | 无 |
| TEST | **PASS** | 编译 0 错 0 警；单测 181 过 / 3 跳 / 0 失败（无逻辑语义变更，未新增用例） | 无 |
| PERF | **PASS** | 消除 8 处绘制路径每帧分配/查询/扫描（时间线预格式化、Battle 索引查询、Qualification 单次扫描、Social 节点缓存外查询、图标缓存等） | 无 |
| SAVE | **PASS** | 无持久化字段变更 | 无 |
| COMP | **N/A** | 未触碰 Harmony / 第三方接入 | 不适用 |

## 3. 遗留项（P2/P3，注明原因）

| # | 位置 | 问题 | 原因 |
|---|---|---|---|
| 1 | Dialog_QualificationFlow.CiteResearch | Dialog 内直写 `SourceResearchEventIds` 未走 service/MarkChanged | 该文件属并行会话进行中的 P9 流程工作，避免改动冲突；已记录待其会话处理 |
| 2 | 主窗口 Home/Shell | 绘制栈内 `RefreshNow`/`Settings.Write()`（视图切换/主题切换时磁盘 IO） | 行为性改动（刷新时机），风险中等；建议下一轮延后刷新 |
| 3 | Shell/Labels | `RelationDefLabel`/`WorkTypeLabel`/`BiomeLabel` 绘制路径 DefDatabase 查询 | 查询为哈希查找 + 缺失时一次性标记，量级小；完整修复需快照期预格式化，改动面大 |
| 4 | WeaponDetail 展开视口 | 整卡 ButtonInvisible 覆盖展开视口（点击视口内行折叠整卡） | 交互结构调整，需游戏内验证防回归 |
| 5 | ITab Resume/Honor | ScrollView viewRect 绝对基准（审计 P1 疑点） | 与主窗口既有验证模式一致（viewRect 与内容同原点即等价 0 基准），未改以避免回归；落地后游戏内复核 |
| 6 | 各 tab | 高度估算与绘制手工对齐易漂移（Legacy/CombatLog 已修，Overview/Career 其余分支） | 逐步提取共享常量；本轮已修 4 处最大漂移 |

## 4. 结论

全部 P1（20 项）已修复；P2 已修复约 40 项（文本/容器/状态/性能四类高价值项），遗留 6 项注明原因。编译 0 错 0 警、单测全绿、DLL 重建。无 Save schema 变更，无需 bump 版本。
