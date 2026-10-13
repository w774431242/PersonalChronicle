# 变更标注 · CHG-20260914-002 — 承载区档案视图对齐 `#v-archive` + 旧内容清理

- **日期**：2026-09-14 · **续** CHG-20260914-001
- **缺陷**（用户实机截图）：人员系统中心详情页承载区渲染的是 `ArchiveMainTabWindow` **整个窗口**
  （顶部「殖民地档案馆」大字 + 左侧导航「首页总览/全部档案/使用手册」+ 档案分类 + 首页「实时指标/档案概览」），
  与预览 `#v-archive`（单层 arc = 头部 + 统计带 + 四分类卡 + 明细 pane，**无侧栏、无首页总览、标题只一处**）不符。
- **适用规则**：GOV-005/006/009/010 · ARC-001/013 · UI-002/006/009 · LOC-001~005 ·
  PERF-001/002 · LAND-001/005/006/007/008/009/010/011/013 · AI-001/003/005
- **触发 Architecture Review**：无（未新增 Harmony / 第三方直连 / 静态可变状态；无 Def / Save / Asset 变更）

## 1. 根因（P0）

`ArchiveEmbeddedHost.Draw` 直接调 `ArchiveMainTabWindow.DoWindowContents` —— 那是**窗口**入口
（`ArchiveMainTabWindow.cs:275-309`），必然画 Header + Sidebar，且默认视图是 `MainView.Home`
（`ArchiveMainTabWindow.cs:98`）＝预览两轮改造**已删除**的「首页总览 + 时间轴」页。
承载契约（`ArchiveViewSnapshot`）只声明「空间预算 + 能不能画」，从未声明「画哪个视图」⇒ 结构信息缺失（LAND-010）。

## 2. 逐组件对照（预览参数 → 落地）

图例：**✔ 已落地** ｜ **B** 引擎降级（登记） ｜ **C** 缺口（登记，非静默省略）

### 2.1 `.arc` 外壳与头部

| 组件 | 预览参数（HTML/CSS 行） | 落地 |
|---|---|---|
| `.arc` 单层容器 | `flex column;border 1px border-soft`（335-336） | ✔ `UIComponents.Panel(rect)`（嵌入入口自画 1 层） |
| `.arc-hd` 头部 | `height:56px;padding:0 13px;border-bottom:1px accent`（337-339） | ✔ 56（= `UITheme.HeaderHeight`）· padX 13 · 底边 1px `UITheme.Accent` |
| `.arc-hd .seal` 印章 | `28×28;circle;border 1.5px accent`（341-345） | ✔ 28×28 + `PanelRaised` + 1px Accent 边 + Key `Archive.Seal`　**B**（圆→直角） |
| `.arc-hd h1` 标题 | `15px/800;letter-spacing .1em`（340） | ✔ `GameFont.Small`（LAND-007：13–15px→Small）· Key `UI.ColonyArchive` · **只此一处** |
| `.arc-hd .bdg` 口径徽章 | `.bdg{9.5px;padding 1.5px 7px;border-radius 99px}`（187-191） | ✔ `UIComponents.Badge` + Key `UI.OverviewDesc`　**B**（圆角→直角） |
| `.arc-hd` 主题按钮 | `.btn` + `.pdot`（205-211） | ✔ 复用 `UIComponents.ThemeSwitch`（既有唯一实现，自带 D1/D2/D6 降级） |
| `.arc-bd` 主体内缩 | `padding:10px`（347） | ✔ 常量 `EmbeddedBodyPadding = 10` |
| `.arc-main` 区块间距 | `gap:9px;padding:10px;overflow:auto`（361-362） | ✔ 常量 `EmbeddedBlockGap = 9` |
| **`.arc-side` 侧栏** | **预览已删除**（HTML 无 `.arc-side`） | ✔ 嵌入入口**不调用** `DrawSidebar`（原版标签页路径保留） |
| Header / CompactNav | — | ✔ 嵌入入口**不调用**（消除大标题与导航条） |

### 2.2 ① 档案统计带（`.ovstat`）

| 组件 | 预览参数（行） | 落地 |
|---|---|---|
| `.ovkpis` 4 列 | `repeat(4,1fr);gap:9px`（290） | ✔ 4 等分 + gap 9 |
| `.ovkpi` 卡 | `padding 6px 10px;border 1px hair;border-left 2px accent;radius 5px;bg card`；`.l` 9.5px dim；`.v` mono 20px/800（291-294） | ✔ 卡高 47 · 左 2px 强调竖标 · 标签 Tiny/Dim · 数值 Small（LAND-007 上限）　**B**（圆角/等宽字体降级） |
| 4 项读数 | 在册档案 / 已归档 / 档案跨度 / 事件总量（1034-1037） | ✔ 前 3 项同预览；第 4 项改**档案对象**总数（事件总量需全量扫描，热路径禁用 `GetAllEvents`）⇒ **C** 登记 |
| `.ovtime` 四段时限带 | `height:9px` + 4 段绝对定位（295-297,1038-1049） | **不做** ⇒ **C**（无历史分期口径） |

### 2.3 ② 四分类卡阵列（`.ovcards`）

| 组件 | 预览参数（行） | 落地 |
|---|---|---|
| `.ovcards` | `repeat(4,1fr);gap:9px`（298） | ✔ 4 等分 + gap 9 |
| `.ovcard` 卡体 / `.on` | `padding 8px 10px;gap 5px;border 1px hair;radius 6px`；`.on` accent 边 + 8% 底（299-301） | ✔ 卡高 92 · 选中 `PanelRaised` + Accent 边　**B**（`color-mix`→`WithAlpha`） |
| `.hd`：`.no` / `.nm` / `.dep` | `#01` mono 9px dim；名称 12px/800；行为徽章 `8.5px;padding 0 5px;border 1px`（302-305） | ✔ 编号 + `CategoryLabel` + 深度徽章（Key `Archive.Depth.StatOnly/Event/Record`，**按行为枚举取值**，GOV-009） |
| `.big` + `.unit` | mono 23px/800（306-309） | ✔ `GameFont.Medium`（LAND-007 上限 22） |
| `.stack` 三段堆叠条 | `height:7px` + 3–4 段（310-311） | ✔ 高 7；**单段占比带**（分类内部分项口径只有人物类有）⇒ **B/C** 登记 |
| `.lg` 图例行 | `gap 9px;9px;dim`（312） | ✔ Key `Archive.Card.Share`「{0} / {1} 条」 |
| 点击切分类 | `data-cat` → `selectCat`（JS） | ✔ 整卡 `ButtonInvisible` → `GoOverview(key)` |

### 2.4 ③ 明细 pane

| 组件 | 预览参数（行） | 落地 |
|---|---|---|
| `.catpane` 切换 | 同一时刻只显示 1 个（521-522） | ✔ 按 `overviewCategoryFilter` 只画 1 个 pane |
| **人物筛选 seg** | 连体分段；`padding 3px 12px`；选中 20% accent 底（212-219,1086-1091） | ✔ 新 `DrawEmbeddedPawnFilterSeg`（左对齐连体、选中 20% Accent）· 复用 Key `UI.PawnFilter.*` |
| **12 列表** | th 9px/`5px 7px`；td `3.5px 7px`；`.num` 右对齐（252-282,1096-1102） | ✔ 列比例表（12 项合计 1.0）+ 行高 20 + 表头 20 + 数值列右对齐（新增 `Archive.Col.Id/Kind/JoinDate`，其余复用总控 `Population.Col.*`） |
| 行交互 | `tr:hover` 6% accent / `tr.sel` 13% | ✔ 悬停 6% Accent + 点击 → `RequestPawnDetail`（下钻既有档案页） |
| 表格滚动 | `.dtwrap{overflow:auto}`（252） | ✔ 唯一内层 scroll view（在外层弧内），行超界 `break` |
| 物品 / 战役 / 地点 pane | 7 列表；`kstrip` 5/8 格 + 卡 250×150 / 250×176（1136-1256） | ✔ 复用既有 `DrawOverviewSection`（战役/地点卡几何本就与预览一致）⇒ **不重写** |
| 表头排序 / 分页 | 预览自陈「列排序为预览演示」（1023） | **不做** ⇒ **C** 登记 |

### 2.5 概览区（`#v-overview`，同页上半区）

维持 CHG-001 裁决：已落地 5 面板 + 名册；**未落地**（缺口径，登记）：身份 Donut / 性别金字塔 /
能力面积图与 11 项条 / 12 工种竖柱图 / 人员绩效多线趋势 / 信仰与理念 / 派系与外交 / 派生 12 项（占位）。

## 3. 代码改动

| 文件 | 类型 | 说明 |
|---|---|---|
| `Archive/ArchiveMainTabWindow.Embedded.cs` | **A** | 嵌入入口 `DrawEmbeddedView`：arc 外壳（56/13/10/9）+ 分发（下钻复用既有；否则 `#v-archive` 形态）+ pane 分发 + 非人物 pane 高度 |
| `Archive/ArchiveMainTabWindow.EmbeddedCards.cs` | **A** | 统计带（4 卡，47f）+ 四分类卡（92f，含深度徽章、占比带、点击切分类） |
| `Archive/ArchiveMainTabWindow.PawnTable.cs` | **A** | 12 列人物表 + seg 筛选；**行缓存按数据修订号失效**（行内含事件查询与工时聚合） |
| `Archive/UI/ArchiveEmbeddedHost.cs` | M | `DoWindowContents` → `DrawEmbeddedView`（附不可回退注释） |
| `MasterControl/MasterControlTabWindow.Archive.cs` | M | 区块工具条不再画 `TitleText`（同屏两处「殖民地档案馆」），改画视图名 `UI.OverviewTitle` |
| `Languages/{ChineseSimplified,English}/Keyed/Archive.xml` | M | 新增 11 个 Key（Seal / TooSmall / Stat.Span / Stat.Objects / Card.Share / Depth×3 / Col×3） |

**Def / Save / Asset**：无。`HomeViewMode` 字段**保留、停止在嵌入路径读取**（旧档零迁移）。
**Harmony**：无新增。

## 4. 引擎降级登记（B 类，须并入 `11-预览落地标准化规范 §3`）

圆角（6/5/99/50%）→ 直角 · `color-mix` → `UITheme.WithAlpha(token,a)` · 绝对定位段条 → 顺序 `DrawBoxSolid` ·
mono/display 字体 → `Tiny/Small/Medium` · 渐变/阴影 → 纯色 + 1px 描边 · `position:sticky` 表头 → 表头固定于滚动区外。

## 5. 缺口登记（C 类，如实、非静默省略）

统计带「事件总量」与「四段时限带」· 分类卡三段堆叠条（分类内部分项口径）· 表头排序 / 搜索 / 分页 ·
概览区 6 项图形面板（见 §2.5）· 战役/地点「实际不可下钻」的诚实说明仍由既有实现承载。

## 6. 验证

| 项 | 结果 | 证据 |
|---|---|---|
| 编译 | **0 错 0 警** | `dotnet build Source\PersonalChronicle.csproj -c Release /p:Platform=x64` |
| 单测 | **316 通过 / 0 失败** | `dotnet test Tests\PersonalChronicle.Tests.csproj -c Release` |
| dll 同步 | dev == pub `766501BD33A3…` | `Get-FileHash` 两区一致；`PublishedFileId.txt` 仍在 |
| 实机视觉 | **未执行** | 无 RimWorld 运行环境（需重启游戏后看：① 不再出现侧栏与首页总览；② 同屏只剩一处「殖民地档案馆」；③ 统计带/分类卡/12 列表可见） |
| GATE-DELTA | **未执行** | ⚠ 需先固定本次起始 commit 作基线（`origin/main` 基线落后，见 CHG-001 §6） |

## 7. 第二轮清理（用户第二轮实机反馈：「旧内容未删除干净」）

清掉的**「我方有、预览没有」**内容（这才是"未删除干净"的实质）：

| 项 | 位置 | 处置 |
|---|---|---|
| KPI 带：在册档案 / 已归档 / 现役殖民者 / 服务天数 | 详情页顶部（`AppendKpis` → 详情页 KPI grid） | **删除**：新增可选契约 `ISystemCenterKpiVisibility`，人员中心 `ShowsKpis = false` ⇒ 不画 KPI（**采集保留** —— 「有没有 Provider」仍是该中心「已接入」的判据）；注册表卡片也不再取首项 KPI |
| 「人员概览」工具条 + 「现役 N 人 · 名册 N 人」 | `MasterControlTabWindow.Population.cs` | **删除**（`PopulationTitleHeight = 0`，高度估算与绘制仍共用同一符号） |
| 人员名册 9 列表 | `MasterControlTabWindow.Roster.cs` | **删除**：与承载区 12 列人物表重复（同屏两张人员表 = 同一批读数画两次）；**名册采集同步停用**（不再逐人构造行 → 去掉 O(全档) 的字符串/事件查询/Def 遍历） |
| 外层工具条「殖民地档案馆 / 档案总览」+「嵌入视图·完整档」 | `MasterControlTabWindow.Archive.cs` | **删除**（`ArchiveBlockTitleHeight = 0`）⇒ `.arc` 直接铺满承载区（预览里承载的就是一层 arc） |
| 死字段 `selectedRosterStableId` / 名册列常量 / 名册文案缓存槽 | `MasterControlTabWindow.*` | 删除（0 警要求） |

**补做（1:1）**：分类卡三段堆叠条 —— 人物类按真实分项（**在世 / 阵亡 / 归档**）分段着色（`Alive/Dead/Dim`，段宽 = 占比，顺序拼接），图例 Key `Archive.Card.PawnBreakdown`；其它三类仍为单段占比带（分类内分项口径未落地，登记 §5）。

**验证（第二轮）**：编译 **0 错 0 警** · 单测 **316 通过** · dll 两区一致 `C9FA4126D435…` · `PublishedFileId.txt` 仍在。

## 8. 第三轮：概览区图形面板（1:1 补做）

| 预览面板 | 预览组件（行） | 落地 |
|---|---|---|
| ① 人员规模与构成 | 身份 **Donut 150×150** + 图例（609-635）· 性别金字塔（670-674）· **11 项能力条** `列 52/1fr/30,行 14`（730-746） | **身份 → 堆叠条 + 图例**（段宽 = 占比、三段 `Alive/Info/Threat`，「自由 8 · 奴隶 3 · 囚犯 1」Keyed 预拼）**B 类**（引擎无圆弧）；**能力 11 项条**已补（名称 56 + 条 + 数值右对齐，满值显绿）；**恢复能力明细采集**（上轮曾因无消费者删除 —— 采集与消费必须同时存在） |
| ⑤ 劳动力与劳动强度 | `.barv{height:96px}` **12 根竖柱** + 集中度/人均（889-903） | **12 工种竖柱图**：柱高按**最大工种归一**（最高满格），柱宽 = 列宽 × 0.62（LAND-004），第一根实色其余 55% 透明，**悬停给「工种 · 工时」**（常态零分配：仅在悬停时拼串，LAND-008）；工种上限 **8 → 12**（`LaborMaxRows`） |
| ② 信仰与理念 / ③ 派系与外交 | Donut + chips + drow | **未落地**（无 Ideology / Goodwill 口径）⇒ 缺口登记 |
| ⑥ 人员绩效 | 多线趋势（951-961）+ 排行（968-974） | **未落地**（需「近 8 季」时序快照 = Save 变更）⇒ 缺口登记 |
| ⑧ 专业分布（技能梯队 4 档） | 4 档 bars（1000-1003） | **未落地**（需技能等级分布聚合口径）⇒ 缺口登记 |

**同轮清理**：面板 ① 改为堆叠条后，`RowFree / RowSlave / RowPrisoner` 三个文案槽与三条键值行成为死代码 ⇒ 一并删除（保持 0 警）。

**验证（第三轮）**：编译 **0 错 0 警** · 单测 **316 通过** · dll 两区一致 `6F52BC3D9E38…`。

## 9. 第四轮：性别构成 + 技能梯队 4 档

| 预览面板 | 预览组件（行） | 落地 |
|---|---|---|
| ① 性别金字塔 | `.pyr` 分层双向条（670-674） | **男 / 女两行占比条**（女 `Threat` / 男 `Info`；分母 = 现役殖民者数，与身份占比**同分母**，可互相加总核对）；形状属 **B 类降级**（引擎无金字塔） |
| ⑧ 技能梯队 4 档 | `.bars` 四行：专家 ≥15 / 熟练 10–14 / 入门 5–9 / 学徒 <5（1000-1003） | **已落地**：按每人**最高专业技能等级**（`ProfessionalSkillData.level`）归档，顺序固定「高→低」不按人数排序；无技能记录者不计入（不虚构「学徒」）；标签/值 Keyed（`Population.Tier.*`），占比 = 档位 / 有技能者总数 |

**同轮清理**：性别由单行文本改双行条后，`RowGender` 文案槽删除（0 警）。

**验证（第四轮）**：编译 **0 错 0 警** · 单测 **316 通过** · dll 两区一致 `67561CCDF7C1…`。

## 10. 第五轮：批量补全（绩效 / 派系 / 能力折线 / 事件总量）

| # | 预览组件（行） | 落地 |
|---|---|---|
| ⑥ **人员绩效**（933-977 的排行） | 新增第 7 面板：**产出市值合计** + Top 8 排行（条宽按**榜首**归一，附注 = 工时）。**刻意不做银币折算** —— 预览的「工时 × 2 银/h」是示例汇率，引入即编造经济假设；排行键始终是可直接观测的产出市值 |
| ③ **派系与外交**（819-837） | 新增第 6 面板：一行一派系 = 名称 + 关系徽章（敌对/中立/盟友）+ 好感度（带色，`Tone` 由 Provider 判定）+ 好感条（`Goodwill01` 映射 −100..100）。数据**活读** `Find.FactionManager`（不持久化）；顺序：本殖民地优先、其余按名称序（**不按好感度排** —— 会让列表随外交漂移）；过滤 `defeated` / `hidden != true` |
| ① 能力**折线**（698-728 面积图的折线等价） | 11 项能力值连成**阶梯线** + 顶点标记；引擎无多边形填充（B 类）⇒ 只画折线；水平段用 `DrawBoxSolid` 拼（**不用两点版 `Widgets.DrawLine`**，它不认 GUI.matrix） |
| 承载区统计带第 4 卡「事件总量」（1034-1037） | **已落地**：新增 `IArchiveQueryService.GetEventCount()`（**O(1)**，直读 `ChronicleGameComponent.Events.Count`，零复制零排序）—— 取代此前的「档案对象」替代口径；`GetAllEvents()` 仍标为 O(n) 复制、禁入热路径 |

面板数 5 → **7**（规模 / 劳动力 / 荣誉 / 专业 / 派生 / 派系 / 绩效），`PanelHeights` 与标题槽同步扩展。

**验证（第五轮）**：编译 **0 错 0 警** · 单测 **316 通过** · dll 两区一致 `0EE6C8EBC048…`。

**仍未落地（登记，非遗漏）**：承载区物品/战役/地点卡的**分类内分项堆叠条**（需 `ThingObject` / `BattleObject` / `LocationObject` 字段调研）· 四段**时限带**（需历史分期口径）· 表头排序/搜索/分页（预览自陈未实现）· **信仰与理念**（需 Ideology DLC 判定 + COMP 评估）· 绩效**趋势线**（需时序快照 = Save 变更，二期）。

## 11. 第六轮：承载区三类卡的分项堆叠条

四类卡的堆叠条现在**全部按真实字段分段**（此前只有人物类有分项）：

| 分类 | 三段口径（真实字段） | 配色（语义色，非单色阶） |
|---|---|---|
| 人物 | 在世 / 已死亡 / 已归档（`DeathTick` × `ArchiveStatus`） | Alive / Dead / Dim |
| 物品 | 在用 / 已退役 / 已毁（`Decommission != null` × `DestroyedTick`） | Accent / Muted / Dead |
| 战役 | **大规模 / 小规模 / 未标注**（`ThreatKey`：ThreatBig / ThreatSmall / 空）—— 工程**没有战役胜负字段**，故不按预览的「决胜/惨胜/失利」分（如实改用有数据支撑的威胁等级维度） | Threat / Info / Dim |
| 地点 | 家园 / 派系据点 / 其它（`IsPlayerHome` × `FactionDefName`） | Accent / Info / Dim |

无分项口径的分类仍退化为单段占比带（当前已无此类）。图例文案 Keyed：`Archive.Card.{Pawn|Thing|Battle|Location}Breakdown`。

**验证（第六轮）**：编译 **0 错 0 警** · 单测 **316 通过** · 语言文件两区一致 · ⚠ **dll 未同步**（游戏正在运行占用发布区 dll，见 §12）。

**明确不做（如实登记）**：承载区四段**时限带** —— 预览的四段是**叙事分期**（落地/开拓/战事/当下），工程无对应史实划分；用「入册时间等分」替代会改变语义（属编造），故不做。表头排序/搜索/分页：预览自陈「列排序为预览演示」。

## 12. 第七轮：② 信仰与理念（Ideology 软探测）

| 预览组件（753-816） | 落地 |
|---|---|
| 理念分布（Donut + 图例）· Meme chips · Precept drow · 仪式 chips | **落地分布部分**：新增第 8 面板「信仰与理念」—— 逐理念一行（名称 + 成员数 + 占比条）+ 可选「无信仰」行 |

**实现要点（COMP 合规）**：
- **软探测**：`ModsConfig.IdeologyActive` 为 false 时只置 `IdeologyEnabled = false`，面板画**如实提示**（不画空列表、不伪造理念）—— DLC 缺失仍可运行（GOV-008：Core 不认识目标 Mod/DLC 分支只用在**能力探测**而非直接依赖）。
- **活读** `pawn.Ideo`（不持久化）；⚠ 原版 `Ideo` 的显示名字段是 **`name`（小写字段）**，不是 `Name` 属性（编译期踩到）。
- **未落地（缺口登记）**：Meme chips / Precept 明细 / 仪式次数 —— 需更深的 Ideology 读取面（`Ideo.memes` / `PreceptsListForReading` / 仪式记录），本轮不扩。

**面板数 7 → 8**（规模 / 劳动力 / 荣誉 / 专业 / 派生 / 派系 / 绩效 / 信仰）。

**验证（第七轮）**：编译 **0 错 0 警** · 单测 **316 通过** · **dll 与语言两区一致 `71A3C4446BA6…`**（游戏已关闭后完成同步；`PublishedFileId.txt` 仍在）。

## 13. 第八轮：一次性落地余项（物品表 / Meme / 时段条 / 活跃度趋势）

| # | 预览组件（行） | 落地 |
|---|---|---|
| 1 | **物品 7 列表**（`#cat-thing`，1141-1151） | 新增 `ArchiveMainTabWindow.ItemTable.cs`：弱标识 / 物品 / **末任持有** / 持有代 / **击杀** / 当前持有 / 退役；行点击 → `RequestWeaponDetail`（复用既有 `OpenWeaponDetail`）。**两处如实缺口**：① 工程无「制造者」字段 ⇒ 列名改「末任持有」（取 `Decommission.LastHolderLabel`），不把末任持有者冒充制造者；② 「击杀」需按武器筛选的事件索引，本轮未接 ⇒ 画「—」而非 0（表下 Keyed 说明） |
| 2 | ② **Meme chips**（753-816） | `IdeologyMemeText`：取首个有理念者的前 3 个 `MemeDef.LabelCap`（构建期拼好，绘制期零拼接）⇒ 面板末行「理念价值」 |
| 3 | 统计带 **四段时限带**（`.ovtime`，1038-1049） | **可算等价**：档案时段分布条（4 段等分**入库时间跨度**，段宽 = 段内对象数占比；起算 tick 取 `Pawn.JoinTick` / `Thing.CreatedTick` / `Battle.StartTick`，**无该口径的对象不参与**——不猜、不用 0 冒充最老）。标签写明「按入库时间四分」，**不冒充**预览的叙事分期。**由 Archive 层自算**（`cachedCategoryObjects` + revision 缓存）——不跨模块取总控快照（ARC-005） |
| 4 | ⑥ **绩效趋势线**（951-961） | **零 Save 变更方案**：新增 `IArchiveQueryService.GetEventBuckets(count, bucketTicks)`（Data 层 **O(N) 单次遍历、零排序零复制**）⇒ 绩效面板底部「档案活跃度（近 8 季事件数）」柱（末柱实色 = 当下，悬停给数值）。预览那条是「产出/工时累计」曲线，需历史采样 ⇒ Save 变更，故以**事件索引自带时间戳**的真实序列替代（口径写进标签，缺口登记） |

**面板数 8** 不变（本轮补的是既有面板内部与承载区 pane）。新增 Api 能力 2 项：`GetEventCount()`（前轮）、`GetEventBuckets()`（本轮），均为**只读、无副作用、不进存档**。

**验证（第八轮）**：编译 **0 错 0 警** · 单测 **316 通过** · dll + 语言两区一致 `3D3C34BE84F4…`（`PublishedFileId.txt` 仍在）。

**最终未落地项（如实登记，均附不可做/不做的理由）**：
1. `#v-spec`（1923-2639）—— **规格与验收文档页**（响应式分档表、状态完备性、主题令牌真值、接入清单与缺口），不是玩家界面 ⇒ 落地无意义。
2. `#v-pawn` Hero 带（1270-1308）—— 既有档案页已由 `DrawDetailContent`（`DrawObjectHeader` + 5 Tab）承载，属**既有实现**；本轮不重写（避免同功能两处实现）。
3. 表头排序 / 搜索 / 分页 —— 预览自陈「列排序为预览演示」（1023）。
4. 预览时限带的**叙事分期**（落地/开拓/战事/当下）—— 工程无该史实划分（已用时间四分替代并标注）。

## 14. Gate 终检与修复轮（第九轮）

**派发**：`pc-ui-loc-guard`（GATE-UI+LOC）与 `pc-perf-tester`（GATE-PERF+TEST）并行终检；硬门禁 `audit-subagents.ps1 -Blocking` = PASS（7/7）。
**Gate 记录**：`.codebuddy/audit/gate-records/GR-CHG-20260914-002-UI-LOC.md`、`…-PERF-TEST.md`（前者由主会话代落盘，后者由 agent 自落）。

### 修复清单（P0 全部清零）

| 源 | 问题 | 修复 |
|---|---|---|
| PERF P0 | 统计带**每帧**调 `GetActiveSnapshotCount`/`GetArchivedSnapshotCount`（各 O(N) 全对象扫描 + 新 List）与 `GetServiceDays`（内部 **`GetAllEvents()` 全量复制**）——60fps 下每秒复制上百 KB | 改读 120 tick 节流缓存 `cachedActivePawnCount/cachedArchivedPawnCount/cachedServiceDays`；事件总量走 O(1) `GetEventCount()`（`EmbeddedCards.cs`） |
| UI P0 | 统计卡按 `rect.height`（=47+25=72）绘制 ⇒ 下沿压住时段条 | 卡片高改用 `EmbeddedStatStripHeight`（47） |
| UI P0 | LAND-008「截断必有悬停」未覆盖表格与键值行 | 人物/物品表**整行构建期 Tooltip**（`JoinedPawnTooltip` / `JoinedThingTooltip` + `TipRegion`）；概览键值行截断时悬停给完整「标签 · 数值」 |
| UI P0 | GOV-009：按翻译文本判断配色 | 行结构加 `HolderStateCode`（0/1/2），配色只读状态码 |
| PERF P1 | 分类卡每帧 O(N)×4 + `GetCategoryBehavior`（LINQ）+ Translate | `EnsureEmbeddedCardAggregates`：按「修订 × 语言」预计算（`EmbeddedCardAgg`），绘制期零遍历 |
| PERF P1 | 表格无可见区窗口化（每帧全行 × 12/7 列 `TruncateToWidth`） | 按滚动偏移算首/末可见行（±1 缓冲）只画视口内（人物表 + 物品表） |
| UI P1 | 滚动时最后一列压住滚动条 ~14px | 列宽按内容视口宽（扣 `EmbeddedScrollbarWidth`） |
| UI P1 | 筛选段固定 76px 无截断保护 | 加 `TruncateToWidth(cell.width - 4)` |
| LOC P1 | 死键 13 条（中英各一）+ 死代码 4 符号 | 语言 3396→3383 / 3393→3380；删 `PopulationRosterStatus/Row`、`Snapshot.Roster/RosterTotal`、`RosterMaxRows`、`ArchiveEmbeddedHost.FocusPawn`（无调用点） |
| PERF P2 | `PopulationBlockHeight` 每帧 `new float[8]` | `PopulationHeightScratch` 静态复用 |

### 转下轮（已登记，非本轮范围）

1. `GetEventBuckets` 单测 —— 须先抽纯函数 `CountEventBuckets(events, now, count, ticks)`（现入口依赖 `Find.TickManager` 静态态）。
2. `DrawValueRow` 局部量 74/6、`DrawCapacityBar` 56/38 入常量区（纯整理）。
3. `#v-pawn` **健康 Tab** 接入（预览 1318 行明确要求「现按要求接入」；既有档案页 4 Tab 无健康）。
4. `ToString("F1")` 统一 `InvariantCulture`（P2 风格）。
5. 实机视觉验收（Tier-L/M/S 三档 + 统计卡/时段条不重叠的截图终裁）——需运行环境。

### 验证（第九轮）

编译 **0 错 0 警** · 单测 **316 通过** · dll + 语言两区一致 `4D86BB0124BA…`。

## 15. 实机事故：概览 8 面板整块消失（2026-09-14，已修）

**现象**（用户实机截图）：打开人员系统中心，只见承载区「殖民地档案馆」，概览 8 面板整块不显示。

**根因**（Player.log 决定性证据）：
```
[PersonalChronicle][Provider] population overview failed: System.NullReferenceException
  at ...BuiltinPopulationProvider.FillFactions (…)  ← at ...BuiltinPopulationProvider.BuildOverview ()
```
`BuildOverview` 原来把**全部** `Fill*` 包在**一个** try/catch 里 ⇒ `FillFactions` 的 NRE 让整个概览退回空快照（`IsEmpty = true`）⇒ UI 按契约不画概览。触发点最可能是 `Faction.Name`：非人类派系的 `def.factionNameMaker` 缺失时 NameGenerator 抛 NRE，而它出现在**比较器**与**循环体**两处。

**修复（三层）**：
1. **面板级失败隔离**：新增 `SafeSection(snapshot, name, action)` —— 单个面板抛异常只丢该面板（计一次 `FailedSections` 并记 `[Provider] population overview section failed: <name>`，日志节流键含面板名）。
2. **派系读取加固**：新增 `SafeFactionName`（`Name` → `defName` → `loadID` 逐级回落，**任何情况不抛**，且比较器改用它）；`PlayerGoodwill` / `PlayerRelationKind` / `PlayerProduction` 等取值进**逐派系 try**，一个派系读不动只跳过它。
3. **如实提示**：快照新增 `FailedSections`；非 0 时概览末行画「概览采集失败项（详见日志）」（挂在最后一个面板尾部，不动面板栅格与滚动量）。

**教训（已写入长期记忆）**：**"整体 try/catch" 是失败隔离的反模式** —— 它把「局部可降级」变成「全量消失」，且用户侧没有任何可见症状线索。凡采集/聚合类 Provider，隔离粒度必须是**面板级**，兜底 catch 只用于"整体不可用且必须返回安全快照"的场合。

**验证**：编译 0 错 0 警 · 单测 316 · dll + 语言两区一致 `E96333F23EF1…`。**待实机复测**（概览应出现在承载区上方；若仍有面板缺失，日志会给出精确面板名）。

## 16. 完整交付轮（第十一轮 · AI-014 一次性交付）

用户明确「不要分批次，一次性交付完整产品」⇒ 本轮把此前登记的全部"转下轮"与降级项**一轮做完**（不再留 TODO 尾巴）。

| # | 交付项 | 实现 |
|---|---|---|
| 1 | **图表引擎**（新 `Archive/UI/UICharts.cs`） | 环形 Donut（**纹理烘焙** 128×128 + 键缓存，上限 8 张，超限整体销毁）/ 面积图 / 镜像条（金字塔）/ 多序列折线 / `NormalizeToMax`。引擎无图表控件 ⇒ 全部自绘；**不用 `Widgets.DrawLine`**（不认 GUI.matrix） |
| 2 | ① 面板图形化 | 身份 **Donut**（环心写现役人数 + 右侧图例）+ 性别**金字塔**（左右镜像条 + 两侧数字）+ 能力**面积图**（按最大值归一化，构建期算好） |
| 3 | ⑥ 趋势图形化 | 活跃度柱 → **面积图**（8 季事件数；提示文本构建期拼好） |
| 4 | ④ 派生指标**真实化** | 新 `BuiltinPopulationProvider.Derived.cs`：人均产出 / 首人产出占比（单点依赖）/ 装备达标 / 情绪风险（<35%）/ 伤病 / 有床 / 温饱（≥0.3）/ 技能覆盖（≥8 级）—— 8 项**可复算**口径，替换原「两行占位」 |
| 5 | ② 戒律榜 | `ideo.PreceptsListForReading` 聚合（按人数降序前 5） |
| 6 | ⑦ 勋章构成榜 | `PawnObject.GrantedMedals` → `MedalDef` 聚合（前 5，条宽按榜首归一） |
| 7 | ③ 派系科技 | 行内新增科技列（`FactionDef.techLevel` 枚举名，列预算重排 40/12/16/14/14） |
| 8 | ④ 地点 **4 段** | 专用路径 `CountLocationBreakdown4`（家园 / 派系据点 / 任务点 / 其它，判据 `IsPlayerHome` + `WorldObjectDefName` 含 Settlement/Site/Camp）+ 新 Key `LocationBreakdown4`（既有 3 段管道不动） |
| 9 | `#v-pawn` **健康 Tab** | `PawnTabKeys` 4→5；新 `ArchiveMainTabWindow.PawnHealth.cs`（综合分 + 三维度条 + 减值因子 + 结语），数据**复用既有 `cachedHealth`**（Read Model HealthView），不重算口径 |
| 10 | **表格排序 + 搜索** | 人物 12 列 / 物品 7 列：点列头排序（▲▼，**数值列用真实数值字段**，避免 "9" > "10"）+ 搜索框 + 命中计数；**排序与过滤均在构建期**（缓存键含排序/搜索），绘制期零遍历零分配 |

**⚠ 同时清除一个实机隐患**：`Languages/ChineseSimplified|English/Keyed/Keyed/Archive.xml`（**1073 个重复 key**）。RimWorld 对 `Keyed/**` 是**递归**加载 ⇒ 该嵌套副本的旧值会覆盖主文件的新文案。处置：把其中**唯一**主文件缺失的 `PersonalChronicle.UI.Theme.Wuxia` 合并进主文件，然后**删除嵌套目录**（源区 + 发布区）。

**验证**：编译 **0 错 0 警** · 单测 **316 通过** · 语言 XML 解析 OK（zh 3346 / en 3343 节点，差 3 = 中文注释行）· dll + 语言两区一致 `CA3DE0E627B2…` · **待实机验收**（Tier-L/M/S 三档 + 环形/面积/金字塔渲染 + 健康 Tab + 排序搜索）。

## 17. 实机复测反馈三问 → 深度对比与修复（第十二轮）

用户实机复测报 3 问：**① 缺少 tab ② 缺少图表组件 ③ 多出额外内容（属旧内容）**。逐项根因与处置：

### ① 缺少 tab —— **根因：旧实现是「单页滚动」，不是预览的「4 视图分页」**

预览 `#navseg` 是 4 个视图（HTML 563-568）：`概览 / 档案总览 / 人物档案 / 分档与契约`；
而旧实机把「概览 8 面板 + 档案馆承载 + 契约清单」**同屏全塞**在一个滚动页里 ⇒ 既没有 tab，也把不同视图的内容混在了一起（正是第 ③ 问）。

**修复**：新增 `MasterControlTabWindow.PopulationViews.cs` ——
- `DrawPopulationViewTabs`：4 枚胶囊 tab（选中项 accent 底 + 下沿高亮，不可用项灰显且点击无效，与预览固定 4 项一致）；
- `populationView` 状态（UI 瞬时，不入档）；
- **同一时刻只画当前 tab 的内容**（概览 / 档案承载 / 人物档案 / 契约页）；
- 可用性是**数据判据**（概览 ⇔ `PopulationOverview` 非空；档案/人物 ⇔ `ArchiveView` 非空；契约恒可用），不认中心名（LAND-010）。

### ② 缺少图表组件 —— 补齐 2 处 Donut + 1 处确认口径

| 预览图表 | 处置 |
|---|---|
| ② 信仰**理念 Donut**（HTML 761-806） | **新增**：`DrawMiniRing`（理念分布）—— 已核 `Share01` 在 Provider 里按**总数**归一（真实占比），环形精确；超出色板段数的份额并入末段，环闭合不静默丢失 |
| ⑧ 专业**方向 Donut**（HTML 1001） | **新增**：同一 `DrawMiniRing`（专业方向） |
| ① Donut / 性别金字塔 / 能力面积图、⑥ 活跃度面积图、⑤ 12 柱 | 第十一轮已落地，本轮复验口径无变化 |
| ⑥「产出/工时累计曲线」 | **B 类**：需历史采样存档；现以真实事件时间序列（近 8 季）替代，口径写在标签 |

### ③ 多出额外内容（旧内容）—— 同屏混装导致，随 tab 化消除

旧实机在概览页下方直接串接「殖民地档案馆」承载块与契约清单 —— 这些在预览里属于**另外的视图**。
tab 化后同一时刻只渲染当前视图 ⇒ 概览页不再出现承载块，承载块只出现在「档案总览 / 人物档案」tab ✔

### 深度对比检测（预览 4 视图 ↔ 实机 4 tab）

| 预览视图（HTML 行） | 实机 tab | 状态 |
|---|---|---|
| `#v-overview` 概览 · 8 面板（593-1010） | 「概览」 | ✔ 落地（含 Donut / 金字塔 / 面积图 / 12 柱 / 8 项派生 / 戒律榜 / 勋章榜 / 派系科技） |
| `#v-archive` 档案总览 · 承载 + 4 分类 + pane（1011-1269） | 「档案总览」 | ✔ 落地（人物 12 列 / 物品 7 列 + 排序搜索 / 战役 / 地点 4 段 / 时段条） |
| `#v-pawn` 人物档案 · Hero + 5 Tab（1270-1922） | 「人物档案」 | ✔ 落地（承载区驱动嵌入档 `PawnDetail`；健康 Tab 已补为第 5 个） |
| `#v-spec` 分档与契约（1923-2639） | 「分档与契约」 | ✔ 本轮落地为**数据契约与接入现状**页（中心 ID / 数据供给 / 职责 / 提供 / 消费 / 依赖 / 发出 / 挂点 + **未落地项如实登记 3 条**） |

**验证**：编译 **0 错 0 警** · 单测 **316** · dll + 语言两区一致（本轮 hash 见交付说明）。

## 18. 实机 ↔ 预览逐格对照修复（第十三轮）

用户提供实机截图 + 预览截图，判定「**布局空间位置完全不符合 + 部分组件缺失**」。逐格比对与处置：

### A 类（我方实现错，已修）

| # | 现象 | 根因 | 修复 |
|---|---|---|---|
| 1 | **面板位置全错**：实机行序 = 规模/劳动力/荣誉 · 派生/派系/绩效 · 信仰/专业；预览行序 = **规模/绩效/荣誉 · 派系/派生/劳动力 · 信仰/专业** | 早期实现的索引顺序与预览栅格序不同 | **三处索引契约同步重排**：`PanelHeights` / `PanelTitleKeyOf` / `DrawOverviewPanel` 的 switch 全改为预览序（0 规模 · 1 绩效 · 2 荣誉 · 3 派系 · 4 派生 · 5 劳动力 · 6 信仰 · 7 专业），并注明「顺序即契约，三处须同步」 |
| 2 | 荣誉面板**缺 Donut**（预览 ③ 有勋章环） | 未实现 | 新增 `DrawMedalRing`（金/银/铜三段 + 环心总枚数 + 三行图例；配色每帧从主题令牌刷新，零分配、主题切换即时生效）；全 0 时不占高不绘制 |
| 3 | 劳动力 / 绩效面板**整块空白** | 新档无工时/无产出 ⇒ 面板无内容且无说明 | 两面板加**空态说明行**（Keyed：「暂无工时记录…」/「暂无产出账目…」），`PanelHeights` 同步降为 1 行 |

### B 类（引擎/数据不可达，如实登记，非缺陷）

| 预览组件 | 不可达原因 |
|---|---|
| ① 面板的**绿/红双线图** | 需历史采样（存档时序）；现为单条能力面积图 |
| ③ 荣誉的「3 勋章条 + 时间线」 | 时间线需时序；勋章按档环形已落地 |
| ⑤ 派系的「文化：宽容主义」行 | 原版无稳定文化读取面（科技等级已落地） |
| ② 绩效「净收益算式 = 产出市值 − 工时×2 银/h」 | 需银币汇率折算口径（工程拒绝编造汇率） |
| ⑤ 派生「4 列卡片网格」 | 信息等价，现为分组行列表（呈现差异，备案） |

### 深度对比表（8 面板逐格）

| 预览序 | 面板 | 实机（本轮后） |
|---|---|---|
| ① | 人员规模与构成 | Donut（环心现役数 + 图例占比）+ 性别金字塔 + 能力面积图 + 11 项能力条 ✔ |
| ② | 人员绩效 | 合计 + 排行条 + 活跃度面积图；无数据 → 说明行 ✔ |
| ③ | 荣誉与勋章 | 已授予 x/y + **勋章 Donut** + 档位读数 + 职称行 + 勋章榜 ✔ |
| ④ | 派系与外交 | 一行一派系：名称 + 科技等级 + 关系 + 好感 + 好感条 ✔ |
| ⑤ | 派生属性 · 本 Mod | 4 分组 8 项可复算指标 ✔ |
| ⑥ | 劳动力与工时 | 合计/人均/前三 + 12 工种柱图；无数据 → 说明行 ✔ |
| ⑦ | 信仰与理念 | 理念 Donut + 理念行 + 无信仰 + Meme + 戒律榜 ✔ |
| ⑧ | 专业分布 | 方向 Donut + 已设定 + 方向行 + 技能梯队 4 档 ✔ |

**验证**：编译 0 错 0 警 · 单测 316 · 语言 XML OK · dll + 语言两区一致（hash 见交付说明）。

## 19. 「组件参数大小」总根因修复（第十四轮复测）

用户复测报「组件参数大小、图表组件」问题。像素级比对后找到**总根因**：

**`DrawOverviewPanel` 给每个面板方法传的 `rowRect` 高度只有一行（18px）**，而面板方法内部：
- ⑦ 信仰的环形需要 ≥24px 高 ⇒ `DrawMiniRing` 里 `rect.height < 24` 直接 return ⇒ **环形从未画出**；
- ① 的环形 `side = min(150, min(宽×0.5, 高-4))` ⇒ 被压到 24px；
- ⑤ 派生卡片 / ⑦ 两列布局等内部栅格全部被压扁。

**修复**：
1. `DrawOverviewPanel` 传**整块内容高**（`populationPanelHeights[index] - Header - padY×1.5`）——面板内部栅格终于有正确的可用高度；
2. 行级绘制与块级布局解耦：`At()` 行高固定为 `PopulationRowHeight`（否则行会被画成整块高，灾难性拉伸），`DrawValueRow` 返回 `y + PopulationRowHeight`；
3. 组件尺寸对齐预览：① Donut **150px**（HTML 609 `width:150px;height:150px`，旧 68）、能力面积图 **64px**（旧 40）、⑦ 信仰环 **106px 定宽**（HTML 764，环心写有理念者人数，`DrawMiniRing` 换为直接 `UICharts.Ring`）。

**教训（重要）**：把「面板方法拿到的矩形」从"一行高"改为"整块高"是**布局系统的分水岭**——之前所有"面板内多列"修复都建立在错误的可用高度上，组件当然画不对。**先修容器的尺寸契约，再修容器里的组件**。

**验证**：编译 0 错 0 警 · 单测 316 · dll 两区一致（本轮 hash 见交付）。

## 20. 遗留风险

1. 12 列在窄档（<900）可能横向拥挤 ⇒ 已全部 `TruncateToWidth`，但未做列优先级裁剪（登记）。
2. 工时列走 `GetWorkTimeStats` 求和（与个人页同源），代价 = 每行一次列表分配；已按修订号缓存，未做批量接口（登记 `GetEventCount`/批量工时 为后续项）。
3. `HighestTitleOf` 与 `BuiltinPopulationProvider.HighestTitleText` 同口径两处实现 ⇒ 待抽公共 helper（登记）。
4. 外层（总控页面）滚动与嵌入 pane 内层滚动仍并存；滚轮在内层被消费属预期，若实机手感不佳则改为「承载区高度固定 + 外层不滚」。
