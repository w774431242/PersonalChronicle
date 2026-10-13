---
change_id: CHG-20261002-FLOORDETAIL-AUDIT
version: v1.4.0
date: 2026-10-02
author: AI (CodeBuddy) · 复核 pc-orchestrator
scope: [Source/PersonalChronicle/Manufacturing, Source/PersonalChronicle/Automation, Source/PersonalChronicle/Application/ReadModels, Languages]
files:
  - path: Source/PersonalChronicle/Manufacturing/UI/FloorDeviceDetailWindow.cs
    type: M
    symbols: MetricColumnCount（新增）/ MetricValueFont（新增）/ MetricCardHeight（新增）/ MetricCardPadX · MetricCardPadY（新增）/ InitialSize / DoWindowContents / DrawMetrics / DrawBillQueueSection / DrawCountStepperFallback（新增）/ DrawTabSection / MeasureSectionHeight / DrawSummaryFoldButton / SummaryFoldButtonHeight（新增）/ QualityChartHeight · QualityChartGap · QualityChartTail（新增）
    lines: 51-135, 180-215, 230-260, 417-460, 480-580, 562-680, 743-780, 947-1100, 1149-1250, 1381-1420
  - path: Source/PersonalChronicle/Manufacturing/UI/FloorDeviceDetailWindow.Draw.cs
    type: M
    symbols: DrawRows（值列右对齐 / 列间距 30→26）/ DrawQualityChart（标签行高按 Tiny 真值）
    lines: 63-190, 192-240
  - path: Source/PersonalChronicle/Automation/WorkshopFloorViewProvider.Detail.cs
    type: M
    symbols: BuildHeader（CycleText 语义）/ BuildMetrics / BuildTabs（块登记）/ BuildRunTab / BuildRunInspectTab（新增）/ BuildBomTab（新增）/ BuildBillTab / BuildCrewTab（行集恒定）/ AppendAggregatedBom
    lines: 121-170, 296-330, 353-500, 520-560, 565-600, 690-800, 808-1010
  - path: Source/PersonalChronicle/Application/ReadModels/FloorDeviceDetailSnapshot.cs
    type: M
    symbols: CycleText（契约注释同步）/ Metrics（注释同步）
    lines: 41-58
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: +Bill.BomCaption / +Shift.OpenBillsValue
    lines: 3085-3089, 3091-3093
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: +Bill.BomCaption / +Shift.OpenBillsValue
    lines: 3081-3082, 3084-3085
  - path: 系统中心蓝图/待落地项/设备卡弹窗_唯一基准表_2026-10-02.md
    type: A
    symbols: 唯一基准表（逐区块：预览锚点 → 游戏锚点 → 以谁为准 → 状态）
    lines: 1-90
  - path: 系统中心蓝图/待落地项/设备卡弹窗预览_重构版_2026-09-22.html
    type: M
    symbols: 顶部注释块追加 CHG-20261002-FLOORDETAIL-AUDIT 段（行号基准指向基准表 + 受控偏离登记 + 过时注记作废）
    lines: 7-30
  - path: _scripts/compile_with_sdk.ps1
    type: M
    symbols: 裸 csc 路径下刷新 BuildStamp.g.cs（构建时间戳）
    lines: 14-24
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
    symbols: BuildInfo.BuildTimeUtc（生成物，每次编译刷新）
    lines: 1-7
---

# 变更标注 · CHG-20261002-FLOORDETAIL-AUDIT（设备卡弹窗 4 Tab ↔ 预览稿逐项对齐）

**基准**：`系统中心蓝图/待落地项/设备卡弹窗预览_重构版_2026-09-22.html`（含 2026-10-01/02 CHG 标注）。
**触发**：用户给出 4 个 Tab 实机截图，要求建立问题清单并逐项修复（前端 UI / 空间布局 / 数据层）。

| 文件 | 改动 | 摘要 | 关联 Gate |
|---|---|---|---|
| `FloorDeviceDetailWindow.cs` | M | ① **KPI 列数 = min(6, 指标数)**（预览 `.mkpi{repeat(4,1fr)}` HTML:167；旧实现恒 6 列排 4 卡 ⇒ 卡宽 1/6、右侧 1/3 空白）② **KPI 卡内几何/字号按字档真值派生**（数值取 `UITheme.FontValue`=Medium ← 预览 `.v{21px}` LAND-007；旧用 Small 属错档，且标签 rect 14 < Tiny 行高 18 ⇒ **中文标签半截字**，2026-10-02 实机截图实证）③ **警示条改绝对定位覆盖层**（预览 HTML:137-139；旧实现计入内容顶偏移 ⇒ 非故障留空带、故障态反向下沉 7px）④ **工单行高 = 2×`Text.LineHeightOf(Tiny)`+2**（预览 `.bl1/.bl2` 两行，HTML:213-222；旧 22px 行放不下两行 ⇒ 副行半截字）⑤ 行内按钮改单行高、垂直居中 ⑥ **新增 `DrawCountStepperFallback`**（`[-][n][+]` 1..99）⑦ 检视块已有 Caption 时不再重复画内部标题 ⑧ **摘要折叠按钮移到行集之后**且**高度按 Small 真值**（预览 `.foldrow` HTML:816；旧 16px rect 裁「详情」）⑨ 直方图柱区/间隙/标签行高收敛为共享常量 | UI · PERF |
| `FloorDeviceDetailWindow.Draw.cs` | M | ① **值列右对齐** `TextAnchor.UpperRight`（预览 `.drow2 b{margin-left:auto;text-align:right}` HTML:192）② 列间距 30→**26**（预览 `.dsec.on{gap:0 26px}` HTML:181/432）③ 品质直方图**标签行高按 Tiny 真值**（旧 14px rect ⇒ 7 档中文标签半截） | UI |
| `WorkshopFloorViewProvider.Detail.cs` | M | ① **概览拆块**：检视串移出运行块 ⇒ 新块 `BuildRunInspectTab`（预览 HTML:729-738 排在 15 行之后；旧实机顺序为「块标题→检视串→15 行」）② **配料独立成块** `BuildBomTab`（预览 HTML:837-850；旧实现挂在摘要块内 ⇒ 默认折叠态整块不可见）③ 待加工工单值改用新键（去标签/值同文）④ 状态行右侧由「节拍采集中」改为**当前作业**（预览 `.dstate .sp` HTML:155）⑤ 人员页「当前工作/当前时段排班/派工」**恒占位**（预览 18 行定表 HTML:862-889） | DATA · UI · LOC |
| `FloorDeviceDetailSnapshot.cs` | M | 契约注释同步：`CycleText` 实际承载**当前作业**（旧注释写「节拍文本」，字段名保留以免波及消费点）；`Metrics` 注释去掉已删除的 6 项旧口径 | DATA |
| `Languages/ZH+EN Archive.xml` | M | 新增 2 键 ×2 语言：`Bill.BomCaption`（配料/Ingredients）、`Shift.OpenBillsValue`（`{0} 项` / `{0}`）；占位符一致 | LOC |
| `设备卡弹窗_唯一基准表_2026-10-02.md` | A | **新增**：逐区块收敛「预览锚点 → 游戏锚点（符号+行号）→ 以谁为准 → 状态」，供后续轮次先查表再比对（总控 P1-5 建议） | REL · UI |
| `设备卡弹窗预览_重构版_2026-09-22.html` | M | **仅注释登记**（不改预览结构与样式）：行号基准指向基准表；登记本轮受控偏离（一屏内不滚动/工具栏评估件/6 格未补/空态与展开面差异）；此前三条过时注记作废 | REL · UI |
| `_scripts/compile_with_sdk.ps1` + `BuildStamp.g.cs` | M | **构建时间戳修复**：csproj 的 `WriteBuildStamp` 只在 `dotnet build` 路径执行，裸 csc 脚本下 `BuildStamp.g.cs` 不刷新 ⇒ DevMode 卡头显示**上一次 dotnet build** 的时间（2026-10-02 实测：新 DLL 仍显示 10:24），会被误读成"改了没变化"（参照工作记忆 `reference_deploy_path`）。现脚本编译前按同格式重写该文件 | REL · TEST |

### 一致性自检（可复核证据）
- [x] **编译**：`_scripts/compile_with_sdk.ps1` ⇒ **0 错 0 警**（`/warn:4`，2026-10-02 18:38）
- [x] **测量↔绘制口径**：折叠开关（绘制净推进 `(18-4)+6` ↔ 计高 `CaptionHeight+2`）、检视标题（两侧同判 `string.IsNullOrEmpty(section.Caption)`）、BillQueue（绘制 `bills*(行高+2)+RowHeight+10` ↔ 计高同式）三处恒等
- [x] **XML**：ZH/EN `Archive.xml` 良构；键集 **ZH=EN=3489，双向差 0**；新键在**部署副本**内亦存在（`.codebuddy/tmp/loc-gate.ps1` 输出 [C]）
- [x] **部署**：`_scripts/deploy-to-game.ps1` 49 文件；DLL 源=目标哈希一致，build 10-02 18:38
- [x] **并发声明**：仓库内并行会话为 `CHG-SHOP-SHIFTSLIM-001` 与 `CHG-20261002-ORPHANKEYS`（均写 Automation / Languages）⇒ 本次为**合并树编译**，GATE-DELTA 因交叉污染**降级**为「编译 0 错 0 警 + 键集对账 + DLL 哈希」三源证据

### 孤儿键登记（本轮新增孤儿，按 `变更标注_CHG-20261002-ORPHANKEYS.md` 先例登记，不扩线删除）
- `PersonalChronicle.UI.Floor.Detail.CycleAvailable` —— 本轮移除最后一个引用（状态行右侧改「当前作业」）；
  **两语言保留未删**（回滚成本最低），待下轮孤儿键专项清理或按需删除。
- 同段既有孤儿（非本轮产生，仅登记）：`...Detail.Run.Empty`、`...Detail.Crew.TimetableNote`、`...Detail.Queue.Assign`、`...Detail.Queue.AssignTip`
  （`...Detail.Qual.Cat.*` 为前缀拼接引用，**非孤儿**，勿误删）。

### 受控偏离 / 未落地（需用户裁决，未伪造通过）
| 项 | 依据 | 现状 |
|---|---|---|
| 预览评估工具栏（KPI 6 格 / 徽标开关 / 窄档 / 主题 / RUIF 开关 / 警示条开关） | 预览 JS `state.kpi='4'`（HTML:1111）为默认口径；工具栏属**评估件非交付物** | 不落地；`.kpiExtra` 两格（当前品质档 / 操作者心情）数据可得但按上款不补 |
| 健康页品质直方图 + 7 档枚举 | 本机未挂 `CompQuality` ⇒ 无数据源（LAND-009 不编造） | 保留空态；预览 9 行形态属 CompQuality 机型 |
| 预览「每个页签一屏内不放滚动」硬约束（HTML:676-677） | 与游戏「内容自适应窗高 + 88vh 上限 + 超限滚动」兜底冲突 | **待用户裁决**：按预览收严（裁内容）或维持兜底（可滚） |
| 展开面 9 行（`RepeatCount/Paused/ShouldDoNow/PawnRestriction/SkillReq/产物/配料`） | 预览表格只列 6 行（HTML:820-825），展开面无基准 | 保留（均为原版直读）；`Bill.ShouldDoNow()` 是否直出玩家界面待裁决 |
| 生产页空态为 1 行（预览 2 行） | `Queue.Empty` 单键单句；块内单行不折行（LAND-007） | 保留；措辞已含引导语 |
| 块级并排（预览 `.full.half` 使摘要/配料并排） | 游戏块模型恒纵向堆叠，两栏只发生在块内 Rows | 保留（块序已对齐预览） |
| 预览/上游标注**行号漂移**（如预览引 `FloorDeviceDetailWindow.cs:879-920`、`变更标注_CHG-20261001-...:11,29`） | 预览行号为裁决时点快照 | 本轮**未回写**（避免与并行会话冲突）；建议下轮预览反向同步时批量刷新 |
