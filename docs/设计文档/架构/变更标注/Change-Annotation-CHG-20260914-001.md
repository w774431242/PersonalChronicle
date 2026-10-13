# 变更标注 · CHG-20260914-001 — 人员系统中心概览与名册落地

- **日期**：2026-09-14
- **来源需求**：把 `系统中心蓝图/人员系统中心预览_RUIF单页统一版.html`（v2 · 4 视图）落地为游戏内 UI
- **Change ID**：CHG-20260914-001
- **适用规则**：ARC-001（分层与单向依赖）· GOV-003/006/010/012 · AI-001/003/005 ·
  LAND-001/005/006/007/009/010/011/013 · GATE-ARCH/DATA/UI/LOC/PERF/DELTA

## 1. 落地范围裁决

| 预览视图 | 裁决 | 理由 |
|---|---|---|
| `#v-archive`（承载档案馆主界面） | **已落地，不复写** | `ArchiveCenterViewProvider` + `ArchiveEmbeddedHost` 已在 `ChronicleMod` 绑定 |
| `#v-overview`（概览 8 面板） | **本次落地 5 面板** | 见下 |
| `#v-pawn`（人物档案 5 Tab） | **落地「入口」**：名册 + 定位 | 档案绘制必须复用 `ArchiveMainTabWindow.PawnDetail`（P0-1 禁重写） |
| `#v-spec`（分档/令牌/缺口表） | **不落地** | 是设计与验收文档，不是玩家界面（总控裁决 P1） |

**未落地的概览面板（如实登记，非遗漏）**：

| 预览面板 | 状态 | 原因 |
|---|---|---|
| 信仰与理念（Ideology） | 跳过 | 工程无 Ideology Provider/口径，需 DLC 判定与 COMP 评估 |
| 派系与外交（Goodwill） | 跳过 | 只有按击杀分组的 `FactionCodex`，无 goodwill 外交视图 |
| 派生属性 · 本 Mod（12 项） | **占位** | 无殖民地级聚合口径（`OvF.Group.ModSelf` 只是 per-pawn 分组标签）⇒ 画说明，不填数值（LAND-009） |
| 人员绩效（排行榜/趋势） | 跳过 | 「近 8 季」需时序快照，当前无任何历史采样（需 Save 变更 ⇒ 二期） |

## 2. 文件清单

| 文件 | 类型 | 符号 / Key | 层 |
|---|---|---|---|
| `Source/PersonalChronicle/Application/ReadModels/PopulationOverviewSnapshot.cs` | A | `PopulationOverviewSnapshot` + 4 个行类型 + `PopulationRosterStatus` | Application·ReadModel |
| `Source/PersonalChronicle/Application/SystemCenters/IPopulationOverviewProvider.cs` | A | `IPopulationOverviewProvider.BuildOverview()` | Application·契约 |
| `Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinPopulationProvider.Overview.cs` | A | `BuiltinPopulationProvider`（partial 新增）：规模/性别/能力均值/工时聚合/勋章档位/职称/主方向/名册 | Application·Provider |
| `Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinPopulationProvider.cs` | M | `class` → `sealed partial class` | Application |
| `Source/PersonalChronicle/Application/ReadModels/SystemCenterSnapshots.cs` | M | `SystemCenterDetailSnapshot.PopulationOverview` | Application·ReadModel |
| `Source/PersonalChronicle/Application/ReadModels/SystemCenterReadModelProvider.cs` | M | `BuildDetail`：`provider as IPopulationOverviewProvider` | Application |
| `Source/PersonalChronicle/Archive/UI/ArchiveEmbeddedHost.cs` | M | `FocusPawn(string)` | Archive·UI |
| `Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Population.cs` | A | 区块布局 / 面板网格 / 高度纯函数 | UI |
| `Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Roster.cs` | A | 面板内容 + 人员名册表 + 行原子 | UI |
| `Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Detail.cs` | M | `ComputeDetailHeight` + `DrawDetailContent` 接入 | UI |
| `Source/PersonalChronicle/MasterControl/MasterControlTabWindow.cs` | M | `selectedRosterStableId`（UI 瞬时状态） | UI |
| `Languages/ChineseSimplified/Keyed/Archive.xml` | M | `…MasterControl.Population.*`（38 项）+ `…Population.Role.*` | Localization |
| `Languages/English/Keyed/Archive.xml` | M | 同上（英文） | Localization |

**Def / Save / Asset**：无变更。运行期新增字段均为活读或由已持久化数据派生。
**Harmony**：无新增。

## 3. 关键设计决策（ADR 摘要）

1. **概览不新建注册表字典**：与制造中心的 `provider as IManufacturing*DataProvider` 同形，
   由 KPI Provider 兼实现 `IPopulationOverviewProvider` ⇒ 不引入第二种绑定机制（GOV-002）。
2. **名册点击只驱动既有窗口**：`ArchiveEmbeddedHost.FocusPawn` 调 `ArchiveMainTabWindow.RequestPawnDetail`
   （public），承载视图下一帧即显示该人档案；总控**不复制** PawnDetail 五段绘制（P0-1），
   也**不读** `cachedDetailObject` 等私有态（P0-2，ARC-001）。
3. **高度与绘制同源**：`PanelHeights`（纯函数）+ `PopulationBlockHeight`（估算）+ `DrawPopulationBlock`（绘制）
   共用同一组常量与同一套列数算法 ⇒ 不会漂移为滚动异常。
4. **占比在 Provider 侧算好**（`FreeShare01` 等 5 个字段），UI 只按 `Share01` 画条，不重算（08c DL 契约）。
5. **派生面板恒为占位**：`HasDerivedMetrics` 字段保留为唯一判据，UI 有确定的占位分支，不在绘制路径猜。

## 4. 验证

| 项 | 结果 | 证据 |
|---|---|---|
| 编译 | **0 错 0 警** | `dotnet build Source\PersonalChronicle.csproj -c Release` |
| 单测 | **316 通过 / 0 失败** | `dotnet test Tests\PersonalChronicle.Tests.csproj -c Release` |
| dll 同步 | dev == pub `F0ACF716…BFE454C` | `Get-FileHash` 两区一致；`About\PublishedFileId.txt` 仍在 |
| 实机视觉 | **未执行** | 无 RimWorld 运行环境，需启动游戏验证（见「待验证」） |
| GATE-DELTA | **BLOCKED（基线与范围不匹配，非本次缺陷）** | `gate-runner.ps1 -Base origin/main` 把工作区相对 `origin/main` 的**历史累积改动**（97 文件，含 `generated-images/`、`InspectTypes/Console.WriteLine` 调试残留等既有内容）一并计入「标注遗漏」。本次会话开始前该基线即已落后 1 个 commit + 大量未暂存改动，无法用 `origin/main` 隔离出「本次差异」，故本 Gate 结果**不可采信**，需待历史改动提交（或以本次起始 commit 为基）后重跑。**不伪造 PASS（GOV-012）。** |

## 5. 遗留风险

1. 概览采集逐人遍历（O(N)），挂在 300 tick 节流内；殖民地人数极大（>200）时需重测帧耗时。
2. 名册事件数用 `GetEventsFor(stableId).Count`（每人一次）；名册上限 12 行，故最多 12 次/周期。
3. 名册点击后承载视图不会自动滚动到顶部 —— 由档案馆窗口自身状态决定，需实机确认观感。

## 6. 审计与修复（2026-09-14 · 第二轮）

审计派发：`pc-data-steward`（DATA/SAVE）· `pc-ui-loc-guard`（UI/LOC）· `pc-perf-tester`（PERF/TEST）。
原始结论：**DATA = BLOCKED**（P1×2）· **PERF = BLOCKED**（P1×3 + 无指标）· **UI/LOC = PASS WITH EXCEPTION**；修复后如下。

| # | Gate | 等级 | 发现 | 处置 |
|---|---|---|---|---|
| 1 | DATA | P1 阻断 | 名册工时直读 `TotalWorkTicks`：旧档缺该字段 ⇒ 显示 0.0，与同屏档案馆个人页（走回算）矛盾 | 新增 `TotalWorkTicksOf`（与 `ArchiveService.Work.GetTotalWorkTicks` 同口径回算，注释互链） |
| 2 | DATA | P1 阻断 | 「人均工时」分母 = 现役人数（含从未上工者），与既有「排除未干活者」口径冲突 | 新增 `LaborContributors`（有工时记录的现役成员数）作分母 + 口径注释 |
| 3 | PERF | P1 阻断 | 绘制路径每帧 ≈34 次 Translate / ≈200 次 CalcSize / ≈41 次数组分配，与「绘制零翻译」契约漂移 | `EnsurePopulationDrawCache`（按 **数据修订 × 可用宽 × 语言** 失效的**实例级有界**缓存，非 static）+ `RosterColumnRatios` static readonly + 行值 `switch` 直取 |
| 4 | PERF | P1 | 名册对**全部历史档案**构造行（上限只截断输出、不限工作量） | `RosterCandidate` 轻量排序键 + 排序后**只对前 12 名** `BuildRow`（事件查询与 Def 遍历随之下界） |
| 5 | PERF | P1 | `Capacities` 明细采集（N×11 次 `GetLevel` + 每项一个行对象）全链路零消费 | 删明细（保留均值），快照移除 `PopulationCapacityRow` |
| 6 | DATA | P2 | 硬编码 2500 / 60000 | 改 `GenDate.TicksPerHour` / `GenDate.TicksPerDay` |
| 7 | DATA | P2 | 性别分母与身份分母不一致 | 性别改报绝对数（`GenderText`，Keyed 模板），删死字段 `MaleShare01/FemaleShare01` |
| 8 | PERF | P2 | 失败日志无节流（每 300 tick 可无限增长） | `ChronicleLog.Throttled("population_overview_fail", 60000)` |
| 9 | DATA | P3 | 异常路径留半截快照 | catch 内**重建干净快照**再置 `IsEmpty` |
| 10 | UI | P2 | 截断无悬停兜底；名册被裁到 12 行但界面不说 | 行级 `TipRegion` **无条件注册** + 脚注 Key `RosterFooter`「共 {0} 人 · 显示前 {1} 行」 |
| 11 | UI | P2 | `HasDerivedMetrics` 有判据无人写（假合规） | 删字段；占位改为**唯一形态** + 注释说明（口径落地时才新增真实分支） |
| 12 | 三方 | P3 | 死局部 `inner` · 未用 `index` 参数 · `paramref` 名不符 · 分隔符硬编码 · `DrawLineHorizontal` 未走 Token · 勋章循环未判空 · `PendingPurge` 并入「死亡」· 职称空值留白 | 逐条清理：`UIComponents.Rule` · `PairSlash`/`PairDot` Keyed 模板 · `StatusOf`（`PendingPurge` → 已归档）· 职称空 → 「未知」 |

**未采用项（如实登记，非漏项）**：`IArchiveQueryService.GetEventCount()`（需改 Api 契约与实现，属下一轮）· `objectsByCategory` 索引 · 概览快照的 DataRevision 复用（300 tick 门控已足够）· PerfProbe 埋点 · 审计建议的 18 条单测（GATE-TEST 仍 BLOCKED 于「零覆盖」，不在本次范围）。

**修复后验证**：编译 **0 错 0 警** · 单测 **316 通过 / 0 失败** · dll 两区一致 `96FA3BC5…08632A5`（`PublishedFileId.txt` 仍在）。实机指标与旧档加载冒烟**未执行**（无运行环境）。
