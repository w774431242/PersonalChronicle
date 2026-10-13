---
change_id: CHG-LCE-003
version: v1.4.x
date: 2026-09-21
author: AI
scope: [Source/PersonalChronicle/Domain, Source/PersonalChronicle/Data, Source/PersonalChronicle/Application, Source/PersonalChronicle/MasterControl, Tests, Languages]
files:
  - path: Source/PersonalChronicle/Domain/EconomyPeriodTier.cs
    type: A
    symbols: EconomyPeriodTier / EconomyPeriodTier.TicksOf / BoundaryOf / RetentionOf / KeyOf
  - path: Source/PersonalChronicle/Domain/StockCandle.cs
    type: A
    symbols: StockCandle / StockCandle.Merge / IsFlat
  - path: Source/PersonalChronicle/Domain/StockSeries.cs
    type: A
    symbols: StockSeries.AppendSample / CloseTier / Feed / CountOf / CandleAt / TryDeltaPct / Ensure / TierRing.Trim
  - path: Source/PersonalChronicle/Domain/StockSeriesStore.cs
    type: A
    symbols: StockSeriesStore.GetOrCreate / AppendSample / WarnOverflow / MaxTrackedDefs
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.StockSeries.cs
    type: A
    symbols: ChronicleGameComponent.StockSeries / StockSampleIntervalTicks / ExposeStockSeries / SampleStockSeries
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.cs
    type: M
    symbols: ChronicleGameComponent.ExposeData
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.Sampling.cs
    type: M
    symbols: ChronicleGameComponent.GameComponentTick
  - path: Source/PersonalChronicle/Application/SystemCenters/ColonyStockViewRequest.cs
    type: A
    symbols: ColonyStockViewRequest
  - path: Source/PersonalChronicle/Application/ReadModels/ColonyStockGallerySnapshot.cs
    type: A
    symbols: ColonyStockGallerySnapshot / ColonyStockItemRow / ColonyStockCandle / HasAnyData / HasError
  - path: Source/PersonalChronicle/Application/SystemCenters/IColonyEconomyDataProvider.cs
    type: M
    symbols: IColonyEconomyDataProvider.BuildStockGallery
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinColonyEconomyProvider.StockGallery.cs
    type: A
    symbols: BuiltinColonyEconomyProvider.BuildStockGallery / StockSeriesOrNull / CompareByValueDesc
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinColonyEconomyProvider.cs
    type: M
    symbols: BuiltinColonyEconomyProvider（改 partial，业务未改）
  - path: Source/PersonalChronicle/Application/ReadModels/SystemCenterSnapshots.cs
    type: M
    symbols: SystemCenterDetailSnapshot.ColonyStockGallery
  - path: Source/PersonalChronicle/Application/ReadModels/SystemCenterReadModelProvider.cs
    type: M
    symbols: SystemCenterReadModelProvider.BuildDetail
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.cs
    type: M
    symbols: MasterControlTabWindow.RebuildDetailSnapshot / GoCenter
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Economy.cs
    type: M
    symbols: MasterControlTabWindow.DrawColonyEconomyPage / DrawKlineView / DrawHeatView / LayoutTreemap / ResetColonyStockView
  - path: Tests/StockSeriesTests.cs
    type: A
    symbols: StockSeriesTests（12 条）
  - path: Languages/ChineseSimplified/Keyed/ColonyEconomy.xml
    type: M
    symbols: Key:PersonalChronicle.Economy.Stock.*
  - path: Languages/English/Keyed/ColonyEconomy.xml
    type: M
    symbols: Key:PersonalChronicle.Economy.Stock.*
  - path: docs/设计文档/功能模块/经济体系/SDD_经济中心落地-单面板K线与热力图.md
    type: A
    symbols: SDD-LCE-003
  - path: docs/设计文档/架构/Change-Annotation-CHG-LCE-003.md
    type: A
    symbols: 本标注自身（GATE-DELTA 自指要求）
  - path: Source/PersonalChronicle/Economy/UI/EconomyPanelChrome.cs
    type: M
    symbols: EconomyPanelChrome.DrawCornerBrackets（改 RUIF.Shapes.CornerFrame）
  - path: docs/设计文档/架构/审计-经济页硬编码与RUIF组件合规-2026-09-21.md
    type: A
    symbols: 硬编码/RUIF 合规审计 + EXC-RUIF-001/002
  - path: docs/changelog/v1.4.x_2026-09-21_经济中心单面板K线与热力图落地.md
    type: A
    symbols: 用户向 Changelog（REL-004）
  - path: Tests/StockSeriesScribeTests.cs
    type: A
    symbols: StockSeriesScribeTests（全保真往返 / 空集合归一 / 缺段不抛）+ 离线日志与 DeepProfiler 垫片
  - path: Tests/PersonalChronicle.Tests.csproj
    type: M
    symbols: CopyGameDependencies（非框架游戏 dll 批量拷贝 —— Scribe GenTypes 枚举全类型所需）
  - path: docs/设计文档/架构/08c-数据层契约与UI对接标准.md
    type: M
    symbols: §12.7 离线 Scribe 往返测试驱动法（三条硬约束 + 推荐断言面）
  - path: docs/设计文档/功能模块/经济体系/实机验收清单_经济中心R1-R4-2026-09-21.md
    type: A
    symbols: R1–R4 实机验收清单（判据 / 证据路径 / 回填表 / FAIL 回报模板）
  - path: docs/设计文档/功能模块/经济体系/REQ_经济中心重构-本地殖民地经济.md
    type: M
    symbols: §4.1 上卷策略（基准档直喂全部高档）+ 变更记录 v3.1（实机修正）
  - path: docs/设计文档/功能模块/经济体系/评估_蜡烛线无界保存-2026-09-21.md
    type: A
    symbols: 无界保存全维度评估（实测基准 / 速率模型 / 情景矩阵 / 五方案对比 / 三决策点 / §10 裁决落地）
  - path: 系统中心蓝图/殖民地经济中心预览_RUIF单页统一版.html
    type: M
    symbols: 预览 v10（五档：移除 60分 按钮与数据项 · 口径/性能注记改「无上限 + 基准档=日」· 默认档=日）
  - path: docs/设计文档/架构/EXC-2026-009.md
    type: A
    symbols: 例外登记（K 线全档无上限：有界性硬约束的定向例外，含实测成本与缓解）
  - path: docs/changelog/v1.4.x_2026-09-21_蜡烛线五档无界与60分退役.md
    type: A
    symbols: 用户向 Changelog（REL-004）
---

# 变更标注：CHG-LCE-003 —— 经济中心阶段 3 落地（单面板 K 线 + 树图热力图 · 统一周期基准）

> Change ID：`CHG-LCE-003`（阶段 3 落地批）
> 上游：`REQ-LCE-001` v9（Approved）、`SDD-LCE-003`（Approved）、预览 v9（1:1 权威源）
> 日期：2026-09-21
> 适用规则：GOV-001/003/004/006/007/009、ARC-001/006/011、DATA-001/002、SAVE-001、UI-001、LAND-001/002/010/012、PERF-001/003、LOC-001、AI-003/005
> Architecture Review：**触发**（Hardcoding=Yes 周期常量 + 新 Scribe 段 + 新热路径采样；治理 = 单一定义类 `EconomyPeriodTier`）

---

## 1. 变更清单（33 项）

| # | 路径 | 类型 | 说明 |
|---|---|---|---|
| 1 | `Source/PersonalChronicle/Domain/EconomyPeriodTier.cs` | 新增 | **统一周期基准**（唯一真相）：六档跨度 / 保留上限 / 边界（TicksAbs）/ 本地化键；零周期字面量外泄 |
| 2 | `Source/PersonalChronicle/Domain/StockCandle.cs` | 新增 | 蜡烛 OHLC + 样本数：开=首样本、高/低=极值、收=最新；极值合并语义；IExposable |
| 3 | `Source/PersonalChronicle/Domain/StockSeries.cs` | 新增 | 单品种六档有界环 + 进行中桶；在线增量 + 封桶上卷（60分→日；日→周、月；月→季；季→年）+ 保留裁剪 |
| 4 | `Source/PersonalChronicle/Domain/StockSeriesStore.cs` | 新增 | 按 defName 惰建集合；有界键 1024 + 节流告警；Scribe（`LookMode.Value, Deep`） |
| 5 | `Source/PersonalChronicle/Data/ChronicleGameComponent.StockSeries.cs` | 新增 | 采样：`tick % 250` 读主殖民地 `ResourceCounter.AllCountedAmounts`；`Scribe_Deep` 加性可选段 `stockSeries` |
| 6 | `Source/PersonalChronicle/Data/ChronicleGameComponent.cs` | 修改 | `ExposeData` 链追一行 `ExposeStockSeries()`（加性可选字段，旧档 null ⇒ 空集合） |
| 7 | `Source/PersonalChronicle/Data/ChronicleGameComponent.Sampling.cs` | 修改 | `GameComponentTick` 增加 250 tick 采样门 + `MarkChanged()` |
| 8 | `Source/PersonalChronicle/Application/SystemCenters/ColonyStockViewRequest.cs` | 新增 | 视图请求（选中品种 / 档位）—— 交互态显式传参，不入快照 |
| 9 | `Source/PersonalChronicle/Application/ReadModels/ColonyStockGallerySnapshot.cs` | 新增 | 画廊快照：`ColonyStockCandle` / `ColonyStockItemRow`（**与 Domain 类型解耦**）+ 六档标签 + `HasAnyData` |
| 10 | `Source/PersonalChronicle/Application/SystemCenters/IColonyEconomyDataProvider.cs` | 修改 | 可选契约加一个方法 `BuildStockGallery(Map, request)`（不给 `ISystemCenterDataProvider` 加成员） |
| 11 | `Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinColonyEconomyProvider.StockGallery.cs` | 新增 | Provider 分部：目录（① 真值）+ 蜡烛/涨跌幅（② 实测）+ **失败隔离族** |
| 12 | `Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinColonyEconomyProvider.cs` | 修改 | 主类改 `partial`（拆分前置）；业务代码未改 |
| 13 | `Source/PersonalChronicle/Application/ReadModels/SystemCenterSnapshots.cs` | 修改 | `SystemCenterDetailSnapshot` 加 `ColonyStockGallery` 字段 |
| 14 | `Source/PersonalChronicle/Application/ReadModels/SystemCenterReadModelProvider.cs` | 修改 | `BuildDetail(defName, request = default)` 可选参；构建画廊并透传请求 |
| 15 | `Source/PersonalChronicle/MasterControl/MasterControlTabWindow.cs` | 修改 | `RebuildDetailSnapshot` 透传视图请求；`GoCenter` 调 `ResetColonyStockView()` |
| 16 | `Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Economy.cs` | 修改 | **整页重写**：单面板两视图（自绘蜡烛 / squarified 树图）+ 顶栏切换 + 交互 `ForceRefresh` |
| 17 | `Tests/StockSeriesTests.cs` | 新增 | 对账单测 **12 条**（在线增量 vs 批式参考逐桶全等 + 边界矩阵 + 保留 + 上卷 + 空态） |
| 18 | `Languages/ChineseSimplified/Keyed/ColonyEconomy.xml` | 修改 | 新增 24 键（`Economy.Stock.*`，含六档档名） |
| 19 | `Languages/English/Keyed/ColonyEconomy.xml` | 修改 | 同步 24 键 |
| 20 | `docs/设计文档/功能模块/经济体系/SDD_经济中心落地-单面板K线与热力图.md` | 新增 | `SDD-LCE-003`（先架构后编码，GOV-001） |
| 21 | `docs/设计文档/架构/Change-Annotation-CHG-LCE-003.md` | 新增 | **本标注自身**（GATE-DELTA 要求标注自指，否则自指项被判为"遗漏"） |
| 22 | `Source/PersonalChronicle/Economy/UI/EconomyPanelChrome.cs` | 修改 | 四角包角 8 条自绘 ⇒ **`RUIF.Shapes.CornerFrame`**（RUIF 合规审计整改） |
| 23 | `docs/设计文档/架构/审计-经济页硬编码与RUIF组件合规-2026-09-21.md` | 新增 | 硬编码清单 + RUIF 映射表 + A/B/C 定责 + **EXC-RUIF-001/002** |
| 24 | `docs/changelog/v1.4.x_2026-09-21_经济中心单面板K线与热力图落地.md` | 新增 | 用户向 Changelog（REL-004） |
| 25 | `Tests/StockSeriesScribeTests.cs` | 新增 | **Scribe 往返用例**（4 条）：真实管线全保真（六档逐根全等）+ 空集合归一 + 旧档缺段不抛 + **读档路径裁剪**（超大 XML ⇒ 裁到上限、丢最老）；含离线日志/DeepProfiler 垫片 |
| 26 | `Tests/PersonalChronicle.Tests.csproj` | 修改 | `CopyGameDependencies`：批量拷贝非框架游戏 dll（Scribe `GenTypes` 枚举 Assembly-CSharp 全类型的前置条件） |
| 27 | `docs/设计文档/架构/08c-数据层契约与UI对接标准.md` | 修改 | 新增 **§12.7 离线 Scribe 往返测试驱动法**（三条硬约束 + 推荐断言面，跨会话复用） |
| 28 | `docs/设计文档/功能模块/经济体系/实机验收清单_经济中心R1-R4-2026-09-21.md` | 新增 | **R1–R4 实机验收清单**（判据 / 证据路径 / 回填表 / FAIL 回报模板 / 已知预期行为） |
| 29 | `docs/设计文档/功能模块/经济体系/REQ_经济中心重构-本地殖民地经济.md` | 修改 | §4.1 上卷策略改「基准档直喂全部高档」+ 变更记录 v3.1（**实机反馈修正**） |
| 30 | `docs/设计文档/功能模块/经济体系/评估_蜡烛线无界保存-2026-09-21.md` | 新增 | **无界保存全维度评估**（实测 170 字节/根 · 速率模型 · 情景矩阵 · 五方案 · 三决策点）+ §9 **60分 只留 1 周专项** + §10 **裁决与落地**（用户裁决「无上限，去除 60 分钟」） |
| 31 | `系统中心蓝图/殖民地经济中心预览_RUIF单页统一版.html` | 修改 | 预览 **v10**：五档（移除 60分 按钮/数据项）· 口径与性能注记改「全档无上限 + 基准档=日」· 默认档=日（1:1 权威源同步） |
| 32 | `docs/设计文档/架构/EXC-2026-009.md` | 新增 | **例外登记**：K 线全档无上限（有界性硬约束定向例外；实测成本 170.4 字节/根 · 品种维度仍有界 · 抽稀为预案 · R3 实测项） |
| 33 | `docs/changelog/v1.4.x_2026-09-21_蜡烛线五档无界与60分退役.md` | 新增 | 用户向 Changelog（REL-004；兼容面与已知项） |

## 2. 六类数据声明（DATA-001）

| 类 | 声明 |
|---|---|
| Def | **无**（六档 = Domain 常量，不新增 Def/DefModExtension） |
| Runtime | 新增 `StockSeries` 序列 + 画廊快照（纯运行期，可从存档重建） |
| Save | **新增加性可选段** `stockSeries`（`Scribe_Deep`；旧档无段 ⇒ null ⇒ 空集合，**无 Migration**） |
| UI | 经济整页重写；交互态（选中品种/档位/视图/悬停）= 窗口瞬时，**不落盘** |
| Translation | 24 键 ×2 语言（缺键回退键名） |
| Asset | **无** |

## 3. 兼容 / 性能 / 存档结论

- **COMP**：无 Harmony、无第三方 Mod 引用、无 Def 覆写 ⇒ 窄 PASS（理由：本批只新增只读采样与自绘 UI）。
- **PERF**：采样 1 次 / 250 ticks，单次 O(有计数的品种)（典型 ≤ 100）⇒ 平均 < 20 读/秒；上卷仅在封桶时刻 O(蜡烛)；保留阶梯 ≤ 280 根/品种；绘制路径零每帧分配，重绘随读模型节流。
- **SAVE**：加性可选段；旧档可加载（无该段 ⇒ 空态）；`schemaVersion` 不变；无 Migration。
- **实机反馈修复（2026-09-21 · P1）**：用户实测「月以上没有采集」（截图：选「季」= 尚无记录）。
  **根因** = 上卷为**次级链**（`日→周、月`；`月→季`；`季→年`）⇒ 季须等 15 游戏日、年须等 30 游戏日才有第一根（高档"等待低档封桶"）。
  **修法** = 基准档（60分）封桶**直喂全部高档**（`60分 → 日/周/月/季/年`）⇒ **1 游戏小时内所有档出现进行中蜡烛**；
  **等价性** = OHLC 包络结合律（开 = 首喂 / 高·低 = 极值 / 收 = 末喂 / 样本数 = 求和，逐项相同），成本 = 每基准桶 ≤ 5 次 O(1) 归并（仍不做跨档重复采样）；
  **无 Migration**（段形状不变，改的只是前向喂入路径；历史缺口不可回填 —— `ResourceCounter` 无历史）。
  **回归** = `StockSeriesTests.Rollup_AllHigherTiers_AppearWithinOneHour`（含"同源不变式"断言）。
  另：保留阶梯逐档上限（48 / 60 / 52 / 60 / 40 / 20 = **280 根/品种**）已单测钉死（回答用户"每根蜡烛的保存极限"）。
- **v3.2 改版（2026-09-21 · 用户裁决「无上限，去除 60 分钟，批准落地」）**：
  **60分 档退役**（槽位保留 = 存档形状**双向兼容**：升级零迁移、降级不误读；保留上限 0 ⇒ 读档清空旧小时数据；中英语言键删除 = 零孤儿）
  + 保留五档（日/周/月/季/年）**全档无上限**（`EconomyPeriodTier.Unbounded = int.MaxValue`，**复用既有有界机制** ⇒ 不引入"用 0 当无界 = 静默清库/静默不写入"陷阱；登记 **`EXC-2026-009`**）
  + **基准档下移为日**（采样直落日档；日封桶直喂周/月/季/年 ⇒ 高档 **1 游戏日内**出现进行中蜡烛）
  + 档位显示域 = 5（`DisplayCount` / `TierAt` / `DisplayIndexOf`；Provider 与 UI 走**显示索引**，槽位映射唯一入口在 Domain）。
  **成本（实测外推）**：170.4 字节/根 · 五档合计 75.6 根/年/品种 ≈ 12.9 KB/品种/年 ⇒ 100 品种 × 10 年 ≈ **12.9 MB**（60分 曾是 95% 成本源 ⇒ 移除后量级问题消解）。
  **回归**：`Retention_UnboundedKeepsAllHistory_AndRetiredHourSlotStaysEmpty` · `Rollup_AllUpperTiers_AppearOnFirstDayClose` · `OnlineAggregation_MatchesBatchReference_AllUpperTiers` · Scribe 侧 `Load_UnboundedDayRing_IsKept_AndRetiredHourSlotCleared` + `Load_LegacyShape_KeepsSlotAlignment_AndClearsRetiredHourSlot`。

## 4. 验证证据

```text
编译：_scripts/compile_with_sdk.ps1（RimWorld SDK csc；自动备份旧 dll）→ 0 警告 / 0 错误
测试：dotnet test Tests/PersonalChronicle.Tests.csproj -c Release → 376 通过 / 0 失败 / 1 跳过（共 377）
XML ：Languages/*/Keyed/ColonyEconomy.xml 解析 OK（×2；五档键 = 60m 键已删）
dll ：dev=rel `CF46A2C497DEDB13A3BFEBC91E96F5A3` **MATCH**（1,384,448 B，2026-09-21 04:2x）
发布同步：`_scripts/sync_release.ps1` → dll MATCH · XML identical=65 / DIFF=0 / ONLY-IN-DEV=0 · PublishedFileId 保留
GATE-DELTA：gate-runner.ps1 → PASS（33 文件覆盖，无遗漏/超范围/残留）
预览：v10（五档）· CDP 实测：档位条 5 键（日/周/月/季/年）· 默认「日」60 根 · 切「年」20 根 · 切「月」60 根 · 热力图面板在位
```

## 5. 已知未执行项（诚实登记）

- **Vanilla 启动 / 存读档 / 实机目视**：本环境无游戏执行能力 ⇒ **未执行**，待用户实机确认（不得记为 PASS）。
  - 2026-09-21 更新：其中**代码层正确性**已由 `Tests/StockSeriesScribeTests.cs`（4 条真实 Scribe 管线用例：六档逐根全等 / 空集合归一 / 旧档缺段不抛 / 读档路径裁剪）自动化兜住；DATA/SAVE 复审 ⇒ **PASS WITH EXCEPTION（例外唯一化至实机矩阵）**，记录：`.codebuddy/audit/gate-records/GR-CHG-LCE-003-DATA-SAVE-20260921.md`。
  - 剩余实机项 **R1–R4** 已整理为**可勾选清单**：`docs/设计文档/功能模块/经济体系/实机验收清单_经济中心R1-R4-2026-09-21.md`（执行后回填 ⇒ 例外解除，GATE-SAVE 转无条件 PASS）。
