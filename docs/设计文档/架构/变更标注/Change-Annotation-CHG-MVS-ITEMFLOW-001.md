---
change_id: CHG-MVS-ITEMFLOW-001
version: v1.4.x
date: 2026-09-20
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Domain/**, Source/PersonalChronicle/Data/**, Source/PersonalChronicle/Application/**, Source/PersonalChronicle/Capture/**, Source/PersonalChronicle/Economy/UI/**, Tests/**, Languages/**, docs/**]
files:
  - path: Source/PersonalChronicle/MVS/Data/ItemStructureIndex.cs
    type: A
    symbols: ItemStructureIndex / ItemStructureIndex.Rebuild / ItemStructureIndex.UpstreamOf / ItemStructureIndex.DownstreamOf / ItemStructureIndex.MaxNeighborsPerSide / ItemStructureIndex.Freeze
    lines: 1-215
  - path: Source/PersonalChronicle/Economy/UI/EconomyInventoryChainPanel.cs
    type: M
    symbols: EconomyInventoryChainPanel.DrawMain（取消 .chnav 切换器 + 空态判据改为 Nodes.Count）/ EconomyInventoryChainPanel.DrawChainNav（Deprecated 停用，保留一版本周期）
    lines: 81-145
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Economy.cs
    type: M
    symbols: MasterControlTabWindow.OnEconomyMarketMemberSelected / DrawEconomyGridPage（市场面板接线 onSelectMember）
    lines: 147-205
  - path: Source/PersonalChronicle/Application/ReadModels/SystemCenterReadModelProvider.cs
    type: M
    symbols: SystemCenterReadModelProvider.uiMarketItemSelection / SetMarketItemSelection / MarketItemSelection / BuildDetail 投影（SetSelectedMarketItem）
    lines: 60-215
  - path: Source/PersonalChronicle/Application/SystemCenters/IEconomyFactionStatusDataProvider.cs
    type: M
    symbols: IEconomySelectionAware.SetSelectedMarketItem
    lines: 58-75
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.cs
    type: M
    symbols: BuiltinEconomyProvider.selectedMarketItemDefName / BuiltinEconomyProvider.SetSelectedMarketItem
    lines: 211-245
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FlowGraph.cs
    type: M
    symbols: BuiltinEconomyProvider.BuildFlowGraph（品种级 Def 链 → **物品级结构链**：中心物品 + 结构边 + 库存数量 + 取消切换 + 主要项收口 KeepMajorSide）
    lines: 40-330
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FactionStatus.cs
    type: M
    symbols: BuiltinEconomyProvider.BuildFactionStatus（**玩家殖民地放行 + 置顶 + Group.Player 组头**）
    lines: 84-200
  - path: Source/PersonalChronicle/Economy/EconomyMarketFormat.cs
    type: M
    symbols: EconomyMarketFormat.DaysPerMonth / PerDayToPerMonth / FormatRate（**取整**）/ FormatQuantity（**大数升格**）/ FormatSigned / FormatQuantitySigned
    lines: 32-130
  - path: Source/PersonalChronicle/ChronicleMod.cs
    type: M
    symbols: 启动 Keyed 校验清单（+ MVS.Unit.PerMonth / Scale.Suffix / Scale.Divisor）
    lines: 420-440
  - path: Tests/EconomyMarketFormatTests.cs
    type: M
    symbols: 格式断言更新（取整语义）+ 新增 FormatQuantity 升格测试 + FormatSigned ±0 阈值测试
    lines: 19-70
  - path: Source/PersonalChronicle/Application/SystemCenters/Dev/DevEconomyFixtures.cs
    type: A
    symbols: DevEconomyFixtures / BuildRandom / BuildKpis / BuildSociety / BuildFactionDetail / BuildMarket / BuildFlow / BuildFactionSelector
    lines: 1-480
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Detail.cs
    type: M
    symbols: DrawDetailContent（经济分支改用单一取值点 EconomyForDisplay）
    lines: 244-258
  - path: Source/PersonalChronicle/MVS/Data/ItemUseWeightIndex.cs
    type: A
    symbols: ItemUseWeightIndex / ItemUseWeightIndex.Rebuild / ItemUseWeightIndex.WeightOf / ItemUseWeightIndex.AddAllowed / ItemUseWeightIndex.IsBuilt
    lines: 1-150
  - path: Source/PersonalChronicle/MVS/Data/ItemLevelFlowModel.cs
    type: A
    symbols: ItemLevelFlowModel / ItemLevelFlowModel.FillSupplyShares / ItemLevelFlowModel.FillDemandShares / ItemLevelFlowModel.NormalizeWeights
    lines: 1-160
  - path: Tests/ItemLevelFlowModelTests.cs
    type: A
    symbols: ItemLevelFlowModelTests（5 例：归一 / 全 0 均分 / 非法权重 / 计数边界 / null 安全）
    lines: 1-90
  - path: Source/PersonalChronicle/Domain/ItemFlowLedger.cs
    type: A
    symbols: ItemFlowLedger / ItemFlowLedger.QuadrumSlots / ItemFlowLedger.MaxKeysPerSlot / ItemFlowLedger.RecentWindowCount / ItemFlowLedger.OverflowWarnIntervalTicks / ItemFlowLedger.productionSlots / ItemFlowLedger.consumptionSlots / ItemFlowLedger.QuadrumAnchor / ItemFlowLedger.StartTick / ItemFlowLedger.Record / ItemFlowLedger.Advance / ItemFlowLedger.RotateOne / ItemFlowLedger.TryRatePerDay / ItemFlowLedger.EnsureSlots / ItemFlowLedger.WarnOverflow / ItemFlowLedger.LogOverflowThrottled / ItemFlowLedger.ExposeData
    lines: 1-330
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.ItemFlow.cs
    type: A
    symbols: ChronicleGameComponent.ItemFlow / ChronicleGameComponent.ExposeItemFlow / ChronicleGameComponent.RecordItemFlow / ChronicleGameComponent.TryGetItemFlowRate
    lines: 1-75
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.cs
    type: M
    symbols: ChronicleGameComponent.ExposeData（链尾追加 ExposeItemFlow 调用）
    lines: 228-233
  - path: Source/PersonalChronicle/Application/ArchiveService.ItemFlow.cs
    type: A
    symbols: ArchiveService.RecordItemFlow / ArchiveService.TryGetItemFlowRate
    lines: 1-60
  - path: Source/PersonalChronicle/Application/IArchiveService.cs
    type: M
    symbols: IArchiveService.RecordItemFlow / IArchiveService.TryGetItemFlowRate
    lines: 297-314
  - path: Source/PersonalChronicle/Application/ReadModels/EconomyMarketPanelSnapshot.cs
    type: M
    symbols: EconomyMarketMember.DefName / EconomyMarketMember.HasFlow / EconomyMarketMember.SupplyText / EconomyMarketMember.DemandText / EconomyMarketMember.NetText / EconomyMarketMember.NetTone
    lines: 190-290
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs
    type: M
    symbols: BuiltinEconomyProvider.itemFlowService / BuiltinEconomyProvider.itemFlowEnabled / BuiltinEconomyProvider.anyItemFlow / BuiltinEconomyProvider.AppendMembers / MVS.Economy.Market.Sub.PlayerItemFlow 副标
    lines: 120-145,230-260,480-570
  - path: Source/PersonalChronicle/Economy/UI/EconomyMarketPanel.cs
    type: M
    symbols: EconomyMarketPanel.NoDataMark（注释语义收窄）/ EconomyMarketPanel 子行三列条件填充（HasFlow）
    lines: 118-140,470-500
  - path: Source/PersonalChronicle/Capture/Patch_ItemFlowIngredients.cs
    type: A
    symbols: Patch_ItemFlowIngredients / Patch_ItemFlowIngredients.TargetMethodExists / Patch_ItemFlowIngredients.Prefix / Patch_ItemFlowIngredientsStartup
    lines: 1-140
  - path: Source/PersonalChronicle/Capture/Patch_ItemFlowMining.cs
    type: A
    symbols: Patch_ItemFlowMining / Patch_ItemFlowMining.TargetMethodExists / Patch_ItemFlowMining.Postfix / Patch_ItemFlowMiningStartup
    lines: 1-125
  - path: Source/PersonalChronicle/Capture/Patch_ItemFlowPlant.cs
    type: A
    symbols: Patch_ItemFlowPlant / Patch_ItemFlowPlant.TargetMethodExists / Patch_ItemFlowPlant.Prefix / Patch_ItemFlowPlantStartup
    lines: 1-140
  - path: Source/PersonalChronicle/Capture/Patch_GenRecipe.cs
    type: M
    symbols: Patch_GenRecipe.Postfix（追加 RecordItemFlow 生产记录）
    lines: 110-118
  - path: Source/PersonalChronicle/Capture/Patch_GenConstruct.cs
    type: M
    symbols: Patch_GenConstruct.Postfix（追加 RecordItemFlow 建造产物记录）
    lines: 70-78
  - path: Source/PersonalChronicle/Capture/Patch_ThingIngested.cs
    type: M
    symbols: Patch_ThingIngested.Postfix（追加 RecordItemFlow 摄入记录）
    lines: 73-82
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: MVS.Economy.Market.Sub.PlayerItemFlow
    lines: 233-238
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: MVS.Economy.Market.Sub.PlayerItemFlow
    lines: 221-227
  - path: Tests/ItemFlowLedgerTests.cs
    type: A
    symbols: ItemFlowLedgerTests（11 例：常量一致 / 未建立 / 累加 / 覆盖天数 / 两季窗口 / 读写对齐 / 超窗清零 / 产消独立 / 真值 0 / 非法输入 / 有界性）
    lines: 1-190
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/SDD_物品级产消读数.md
    type: A
    symbols: REQ-MVS-013 / 用户三裁决 / 捕获点表 / 口径公式 / 已知缺口
    lines: 1-140
  - path: docs/设计文档/架构/EXC-2026-001.md
    type: M
    symbols: EXC-2026-001.Scope（18→21 类 / 16→19 文件）/ Patch 清单 #19-#21 / 清单变更记录 2026-09-20
    lines: 11-12,44-52,55-60
  - path: docs/设计文档/架构/工程约定/开发手册.md
    type: M
    symbols: Patch 清单表（Patch_GenRecipe 目标名更正为 PostProcessProduct + 新增 3 个 ItemFlow patch 行）
    lines: 169-176
  - path: docs/开发参考/项目整理与功能清单.md
    type: M
    symbols: Patch 清单表（目标名更正 + ItemFlow 三 patch 行）
    lines: 78-84
  - path: docs/开发参考/源码文件索引.md
    type: M
    symbols: Capture 文件表（Patch_GenRecipe 目标名更正 + 3 个 ItemFlow patch 行）
    lines: 106-114
  - path: docs/设计文档/架构/Change-Annotation-CHG-MVS-ITEMFLOW-001.md
    type: A
    symbols: 变更标注（本文件）
    lines: 1-999
---

# CHG-MVS-ITEMFLOW-001 · 物品级产消读数（细分物品级供需扩展）

- **需求**：`REQ-MVS-013`（用户 2026-09-20：「实现细分物品级的供需关系扩展」）
- **SDD**：`docs/设计文档/功能模块/经济体系/市场经济框架/SDD_物品级产消读数.md`（Approved）
- **Gate 记录**：`.codebuddy/audit/gate-records/GR-AI001-20260920-itemflow.md` · `GR-DATA-SAVE-UI-LOC-20260920-itemflow.md`

## 1. 摘要

市场面板展开行（二级成员物品）的「生产 / 消费 / 净值」三列，从**无条件 `—`** 升级为
**有权威来源则填数、无来源则 `—`+原因**：新增**殖民地级物品级产消账本**（按 ThingDef 记件数、
4 季滚动、近两季日均），由 Capture 层 6 个只读钩子记账（3 个新增 Target + 3 个既有 patch 语义扩展）。

**用户三项裁决的落地**：
1. 口径 = **月/季窗口** ⇒ 4 季槽滚动，展示近两季（≈30 天）日均（件/日，与父行同量纲）。
2. AI 派系 ⇒ **保持 `—`**（原版无生产模拟，无权威源）+ 副标如实说明。
3. 覆盖 = **全量**（制造/烹饪/屠宰/熔炼/采矿/伐木收获/建造产物/摄入）。

## 2. 关键设计决策（含被否决方案）

| # | 决策 | 理由 / 否决项 |
|---|---|---|
| D1 | **新建 `ItemFlowLedger`** 而非复用 per-pawn 累加器 | 四要素（殖民地级+物品级+件数+季窗）皆为新数据面；`ProductionAccumulator` 是 per-pawn 且只有累计量与**市值**桶 |
| D2 | 滚动 = **4 槽引用旋转 + 最老槽 Clear 复用**（O(1)） | `QuadrumBuckets` 是单序列拷贝移位，服务"每 pawn 一条曲线"；本类是"每 defName 一条曲线"的字典族 —— 数据结构与目的不同（ARCH B-03 记录） |
| D3 | 读数主体 = **仅玩家殖民地** | AI 派系物品级产消**无任何权威源**（Cecil + 平台事实）；摊派品种基线 = 编造（红线） |
| D4 | 数量语义 = 入参即事实 | `MakeRecipeProducts` 调用链已证：`CalculateIngredients`（按配方量 SplitOff）→ **本钩子** → `ConsumeIngredients` ⇒ `ingredients[i].stackCount` 即本次消耗量 |
| D5 | `Plant.PlantCollected` 选 **Prefix** 且 `by == null` 首行守卫 | Prefix 时植株完好（Postfix 已 Destroy，`YieldNow()` 失真）；本 Mod 农机以 null 调用 ⇒ 跳过（"机器收成无人力"与职业捕获契约一致） |
| D6 | **不推进 `SchemaVersion`** | 加性可选字段：旧档读回 null ⇒ 空账本 ⇒ 三列 `—`（既有先例：编排布局 / v4.18 季度桶） |
| D7 | 有界性 = 单槽 1024 键 + 溢出拒绝新键 + **每 6000t 至多一条日志** | PERF 硬约束；日志用 `[NoInlining]` 隔离（`Verse.Log` 是 ECall，离线单测会抛 —— 已踩并修） |
| D8 | 读取路径也 `Advance`（幂等） | 只写不对齐 ⇒ 闲置殖民地会把 n 季前件数画成"近期"（ARCH B-02；`ProductionAccumulator.AdvanceQuadrum` 同纪律） |
| ⛔ | 否决：品种基线摊派 / `ResourceCounter` 差分 / 经手集当速率 / 改 `amounts` 键空间 / 绘制期现算 | 见 SDD §3 与 GR 记录（各有独立理由） |

## 3. 数据流

```text
Capture（只读钩子，全部玩家派系守卫 + try/catch + O(1)）
  Patch_GenRecipe.Postfix            → 生产（制造产物）
  Patch_ItemFlowIngredients.Prefix   → 消费（配方原料；含屠宰/熔炼）
  Patch_ItemFlowMining.Postfix       → 生产（采矿产物）
  Patch_ItemFlowPlant.Prefix         → 生产（收获/砍伐产物）
  Patch_GenConstruct.Postfix         → 生产（建造产物）
  Patch_ThingIngested.Postfix        → 消费（摄入）
        ↓ IArchiveService.RecordItemFlow（Application 具名门径，GATE-ARCH A-01）
ChronicleGameComponent.ItemFlow → ItemFlowLedger.Record（Domain；跨季旋转）
        ↓ 构建期（300t 门控）：IArchiveService.TryGetItemFlowRate
BuiltinEconomyProvider.AppendMembers → EconomyMarketMember.HasFlow/SupplyText/DemandText/NetText/NetTone
        ↓ 快照（只读、已格式化）
EconomyMarketPanel 子行三列（HasFlow ? 文本 : NoDataMark）
```

## 4. 门禁与验证

| Gate | 结论 | 证据 |
|---|---|---|
| ARCH | PASS WITH EXCEPTION（条件 B-02/A-01/B-03/B-05/C-01/C-02/C-05 已在本批闭合：读取对齐 / 具名门径 / 旋转理由登记 / `StartTick` 分母字段 / Bill 备选否决记录 / Prefix 措辞更正 / EXC 台账 18→21） | `GR-AI001` + 本节 |
| DATA | PASS WITH EXCEPTION（AI 派系不填数、桥接在 Application、无假数据；覆盖缺口如实标注） | `GR-DATA-SAVE-UI-LOC` |
| SAVE | PASS WITH EXCEPTION（加性字段不推版本；`??=` 归一；⚠ SAVE-007 旧档矩阵**未执行**——无运行环境，如实登记） | 同上 |
| UI | 三态契约（`HasFlow` 显式字段，禁文本反推）+ 同源（净值 = 供应 − 需求，与父行同式） | 同上 |
| LOC | 新 Keyed 中英各 1（`MVS.Economy.Market.Sub.PlayerItemFlow`），符号类维持硬编码合规 | 同上 |
| COMP | PASS WITH EXCEPTION（3 新 Target 自带签名探测 + Startup hook；Prefix 全包 catch 且**绝不 return false**；EXC-2026-001 已更新 18→21；Rollback = 单文件删除） | 同上 |
| TEST | 编译 **0 错 0 警**；单测 **477 通过 / 0 失败**（+11）；两区 dll MD5 `5F2FA296CC1314730069F6A2203FC7DC` 一致；76 个 XML 全同（diff=0） | 命令输出 |

## 5. 已知缺口（如实登记，勿当缺陷重报）

- 建造**耗材**（蓝图阶段消耗）、`ThingComp.GetAdditionalHarvestYield` 附加产出、车间"额外产出/耗材返还"不经本账本 ⇒ `0` 可能同时意味着"未覆盖"与"真无产出"（副标说明只覆盖玩家殖民地；覆盖清单见 SDD §9）。
- 采矿 `mineableYieldWasteable` 矿（稀有）读数略高估（`yieldPct` 为 private，按纪律不反射读取）。
- `PlantCollected` 为 virtual：第三方子类覆写且不调 base ⇒ 静默不命中（已知缺口）。
- 收获 `harvestFailable` 且 stat < 1 时可能记入失败尝试（该字段默认 false，影响面≈0）。
- 屠宰耗材（尸体）是否进 `ingredients` 待实机确认。

## 6. 未执行（GOV-012，不得伪造）

实机截图定标（列宽是否容纳新数字）· 旧档加载矩阵（SAVE-007）· 4 季滚动长局（≥60 天）行为 · 中英切换实测 · 三主题配色回归。
**必须重启游戏**（dll + XML 已同步两区）。

## 7. 变更集说明

- `Assemblies/PersonalChronicle.dll`（二进制产物）同步两区但**不进本 files: 清单**（GATE-DELTA 改动集不含二进制）。
- `.codebuddy/**`（审计记录 / 临时脚本）不参与 git 改动集（已核）。
- 📌 文档漂移修正（COMP 评审 F1）：`Patch_GenRecipe` 的真实目标一直是 `GenRecipe.PostProcessProduct`，而 `开发手册.md` / `项目整理与功能清单.md` / `源码文件索引.md` 三处写作 `MakeRecipeProducts`（历史漂移）。本批**顺手更正**并在表中登记 3 个新 patch；⚠ `docs/开发参考/源码文件索引.html` 是**生成产物**（含 size/lines 数据），本批未手动改，**下次重生成脚本运行时同步**（已在正文登记，避免"两套真相"）。

---

## 8. 第二批：全派系物品级供需模型（REQ-MVS-014 / CHG-MVS-ITEMFLOW-002，同一 Change ID 连续交付）

**触发**：用户实测"细分物品供需还是不存在" + 新需求「全派系的物品级供需，我需要一个基础框架模型提供全派系供需」。

**诊断（两个原因，均有证据）**：
1. 面板派系选择器默认回落 Empire / 首个可见派系（`SystemCenterReadModelProvider.ResolveSelectedFactionDefName`）⇒ 按第一批裁决"AI 派系画 `—`" ⇒ **默认视图永远看不到物品级数据**；
2. 物品级账本需要一次真实产消才建立 —— 最新存档 `新移民9.rws` 中**无 `itemFlow` 字段**（从未建立），用户读档后只看面板不看产出 ⇒ 全 `—`。

**改动（新增 3 / 修改 6）**：
- 新增：`MVS/Data/ItemUseWeightIndex.cs`（用途权重 = 配方原料 `GetBaseCount` + 建造 `costList`；可重建缓存，禁 Scribe）· `MVS/Data/ItemLevelFlowModel.cs`（③ 模型：供给 × 价值份额 / 需求 × 用途份额；`NormalizeWeights` 纯算术可离线测）· `Tests/ItemLevelFlowModelTests.cs`（5 例）。
- 修改：`BuiltinEconomyProvider.MarketPanel.cs`（逐行来源分流 ②/③ + 双副标文案 + `supplyView/demandView` 提升作用域以便模型分支复用）· `EconomyMarketPanelSnapshot.cs`（`DataLevel` / `LevelText`）· `EconomyMarketPanel.cs`（子行圈码）· `Languages/{ChineseSimplified,English}/Keyed/MVS.xml`（`MVS.Economy.Market.Sub.ItemFlowModel`）· SDD（§14 模型设计 + 口径变更登记）。

**口径变更（显式登记，红线语义不变）**：旧红线"品种基线摊派 = 编造"针对的是**未标注的摊派**；本模型 = 全链 ③ 标注（逐行圈码 ③ + 副标"模型估算"）+ **不参与任何计算**（不进价格因子 / 不进 `.msum`）。份额恒归一 ⇒ **Σ子行 = 父行**（与父行同源）。

**验证**：编译 0/0 · 单测 **482 通过 / 0 失败**（+5 纯算术）· 双区同步 + 本标注重跑 GATE-DELTA PASS。

---

## 9. 第三批：物品级上下游结构链 + 子行交互（REQ-MVS-015 / CHG-MVS-ITEMCHAIN-001，同一 Change ID 连续交付）

**触发**：用户三问 + 三裁决（2026-09-20）：

| 问题 | 用户裁决 |
|---|---|
| ① 品种 / 子行后的"3"是什么 | **视觉上隐藏**（行内不画圈码；数据级别说明改由副标承担） |
| ② 细分品种点不动 | 由问题三一并解决：**子行可点击选中** |
| ③ 总库存面板的分类切换 | **完全取消，跟随市场选中** + 链路**只显示结构与上次更新的库存数量，不显示速率** |

**改动（新增 1 / 修改 10）**：
- 新增 `MVS/Data/ItemStructureIndex.cs`：物品级上下游结构（配方产物↔原料、建造产物↔材料；沿 `MVSCommodityRegistry` 范式做可重建缓存 + defName 序号序确定性排序 + 每侧封顶 24）。
- `BuiltinEconomyProvider.FlowGraph.cs`：`BuildFlowGraph` 由**品种级 Def 链**重写为**物品级结构链** ——
  中心 = 市场面板选中的物品；边 = 结构关系（`FlowText` 留空，符合"不显示速率"）；节点值 = `map.resourceCounter.AllCountedAmounts` 的**库存数量**（非 `CountAsResource` 物品如实画 `—`）。
- `EconomyMarketPanel.cs`：子行点击选中（`onSelectMember` 回调经 `Draw→DrawTable→DrawVisibleTable→HandleRowClicks` 全链透传）+ 选中态高亮（`UITheme.Accent` 半透明底 + 3px 左条，判据用稳定键 `defName`）+ **父行与子行圈码视觉隐藏**。
- `EconomyInventoryChainPanel.cs`：`.chnav` 切换器停用（`DrawChainNav` 标 **Deprecated**，保留一个版本周期以便回退）+ 空态改为「请在市场面板点击一个细分物品」。
- 选择态链：`SystemCenterReadModelProvider.uiMarketItemSelection`（唯一真源，UI 状态不入档）→ `IEconomySelectionAware.SetSelectedMarketItem` → Provider 实例字段 → 市场快照（选中高亮）与链路快照（中心物品）。
- Keyed ×4（中英）：`MVS.Economy.Flow.Empty.PickItem` / `Flow.Sub.ItemCenter` / `Flow.Unit.Count` / `Flow.Stock.NotCounted`。

**验证**：编译 0/0 · 单测 **482 通过 / 0 失败** · 双区 dll `E90F0AB90EC35B7E7ED11E7A5EE9FDED` · GATE-DELTA PASS（本标注重跑）。

**已知缺口（如实登记）**：① 库存数量只覆盖 `CountAsResource` 物品（食物 / 药品 / 成品显示 `—`）；② 结构边不区分"经由哪条配方"（多配方合并）；③ 品种级 Def 链（`MVSFlowEdgeDef` / `IFlowGraphReadModel`）**保留但不再被本视图消费**（登记待重估，未删除以免动 MVS 数据层）。

---

## 10. 第四批：列排序 + 主要项收口 + 玩家殖民地条目（REQ-MVS-016，2026-09-20 用户四问）

**用户四问**：① 市场面板列不能排序 · ② 供需数据存在小数点 / 单位口径 · ③ 总库存上下游只显示主要的 · ④ 缺少本地玩家殖民地。

| 问题 | 处置 |
|---|---|
| ① 列排序 | **本批实现**：表头第 `1..4` 列（生产/消费/净值/价格）可点；同列再点**翻转方向**、换列**默认降序**；排序由 **Provider 执行**（数据职责，键 = 快照 `Raw*` 原始值，禁文本反解）；表头画 `▲/▼`；⚠ **展开态键同步从"行下标"改为稳定键 `defName`** —— 否则排序后已展开的品种会跳到别的行上 |
| ② 小数 / 口径 | **解释为主（零改动）**：`EconomyMarketFormat.FormatRate` 的精度阶梯（≥1000 千分位 / ≥10 一位小数 / <10 两位小数）是**设计**（经济量级 10⁰..10⁴，固定位数可读性优先）；量纲 = **件/日**（父行 = ③ 品种基线 × 派系偏置；子行 = ② 玩家实测或 ③ 模型）。若要求统一整数显示 ⇒ 另开裁决（会动 `FormatRate` 与其单测） |
| ③ 主要项收口 | **本批实现**：`KeepMajorSide` 按 ② 实测产消速率降序（**无实测/速率为 0 时退回库存降序，且不筛**），累计到 **80%**（`MajorFlowShare`）即收口（至少 1 项、至多 `MaxFlowNodes`）；副标如实标注「仅显示主要项（按产消量）」 |
| ④ 玩家殖民地 | **本批实现**：根因 = 原版 `PlayerColony` 的 FactionDef 是 **`hidden=true`**，被"非 hidden"可见性规则一并滤掉；改为**放行玩家派系 + 列表置顶 + 独立组头**「本地玩家殖民地」（Keyed `FactionList.Group.Player`）。它是唯一带 ② 物品级实测产消的派系 |

> **【更正 · 2026-09-20】** 上表 ④ 的"根因"**证伪**：`PlayerColony.def.hidden == false`
> （Core `Factions_Player.xml` 未设置；预览数据集玩家派系亦不含「隐藏派系」理由）。
> 玩家殖民地曾被排除**与 `def.hidden` 无关**，旧排除条件**不可考**（实现文件未入库、无 git 历史）。
> 保留的有效结论：放行 + 置顶 + 独立组头（现行行为，见 `BuiltinEconomyProvider.FactionStatus.cs:99`）。
> 详见 `docs/设计文档/架构/审计-派系列表-原版核心-2026-09-20.md` §3-P1-A。

**诚实边界（问题③）**：账本记的是"**该物品自己**的产消量"，**不是**"中心物品流向它的份额"（原版不追踪流向）⇒ 速率只用于排序/筛选，副标**不得称"占比"**。

**验证**：编译 0/0 · 单测 482 通过 / 0 失败 · 双区 dll `A3B518084A51B9A08207B53A33812362` · GATE-DELTA PASS。

---

## 11. 第五批：口径统一为「件/月」+ 取整 + 大数升格（REQ-MVS-017，2026-09-20 用户裁决）

**用户裁决原文**：「统一产能速率改为月或者季，取整。可以添加重量单位自动更换更高的重量单位。」

| 项 | 落地 |
|---|---|
| **口径** | **件/月**（= 件/日 × **30**；常量 `EconomyMarketFormat.DaysPerMonth`）。选"月"而非"季"：账本窗口本身就是 2 季 = 30 天 ⇒ 用"月"无需二次折算 |
| **折算点** | **定义处一次性折算**（父行 `supplyView`/`demandView` 定义处 + 子行 ② 实测处，均调 `PerDayToPerMonth`）⇒ 显示 / 汇总 / 排序键 / ③ 模型 / 社会面板**全部自动同口径**。⚠ **绝不在各显示点各乘一次** —— 漏任何一处就是"同屏两套量纲"（最难查的一类错） |
| **取整** | `FormatRate` 改为**取整 + 千分位**，且用**远离零舍入**（.NET 默认银行家舍入会让 `12.5 → 12`，读数上像"凭空少一件"）；`FormatSigned` 的"持平"阈值同步为 `< 0.5`（否则显示 `±0` 而实际 0.4） |
| **大数升格** | `FormatQuantity(value, scaleSuffix, scaleDivisor)`：≥ 除数 ⇒ `1.2万`（中）/ `12.3k`（英）。**后缀与除数走 Keyed**（中英习惯不同，同一阈值不通用，绝不能硬编码）；键缺失/解析失败 ⇒ 退回千分位整数（降级可解释，不静默改口径） |
| **单位可见** | 市场面板副标追加 `MVS.Unit.PerMonth`（口径变更必须自解释）；社会面板「供需净值」值后带单位 |
| **库存同步** | 链路面板节点值与中心卡、社会面板「本方库存」都走 `FormatQuantity`（同一格式函数） |
| **测试** | 原"三级精度阶梯"断言**按裁决重写**；新增升格测试（中「万」/英「k」两种参数，含阈值下与参数缺失的降级）+ `FormatSigned` 半值 `±0` 测试 ⇒ **484 通过 / 0 失败** |

⚠ **被否决的旧设计已在代码注释标注**：`FormatRate` 的三级精度阶梯明确写为"已被用户否决"，防止后人照旧注释回改。

**验证**：编译 0/0 · 单测 **484 通过 / 0 失败** · 双区 dll `A73B9859B957801EF8C6129959D1A1AE` · GATE-DELTA PASS。

---

## 12. 第六批：流向记账 · A 步（REQ-MVS-018，2026-09-20 用户裁决「先做 A」）

**目标**：为"桑基流向图"建立**唯一真实数据地基** —— 「中心物品 → 各用途」的消耗份额（② 实测）。

| 层 | 落点 |
|---|---|
| **Domain** | `ItemFlowLedger` 新增**流向槽**（4 季滚动）：键 = `material\u0001product`（**扁平化**）—— `Scribe_Collections.Look` 的 `LookMode.Value` **不支持嵌套字典**（第二层会静默写不出/读不回，存档级事故）。API：`RecordMaterialFlow` / `TryMaterialFlows`（件数降序 + defName 兜底确定性） |
| **Capture** | `Patch_ItemFlowIngredients`（**既有钩子** —— 已接 `GenRecipe.MakeRecipeProducts` 的 `ingredients`）新增 `RecipeDef recipeDef` 形参 ⇒ 取**主产物** ⇒ 对每种原料记一笔流向。⚠ 选它而不选 `Bill_Production.Notify_IterationCompleted`：前者的唯一调用点 = `Toils_Recipe`，覆盖工作台/简易点/屠宰/熔炼**全部配方式**；后者有覆盖差集（该文件注释已论证） |
| **转发** | `ChronicleGameComponent.ItemFlow` → `IArchiveService` → `ArchiveService.ItemFlow`（**具名门径**，GATE-ARCH A-01；Provider 不直取组件） |
| **消费** | `BuiltinEconomyProvider.FlowGraph.KeepMajorSide` 改为**流向份额优先**：下游查一次（中心为 material）、上游逐项查；无流向记录 ⇒ 退回产消速率 ⇒ 再退回库存（都无 ⇒ **不筛**）。⇒ 上一批"速率不是流向份额"的诚实边界**已消除**（现在用的就是真实份额） |
| **口径文案** | 链路副标改「中心物品 · **仓储数量**（上次更新）」+ 社会面板「本方**仓储**」—— `ResourceCounter` 只数**仓储组**（IL 已证），写"库存"会被读成"地图上全部"。`0`（仓储空）与 `—`（不参与计数）两种状态已分别注明 |
| **测试** | `ItemFlowLedgerTests` +3：无记录返回 `false`（**非 0**）· 累加与降序 + 前缀不误命中 · 跨 4 季归零 ⇒ **487 通过 / 0 失败** |

**局限（如实登记）**：① 多产物配方只记**首个产物**（重复计入每个产物会让份额整体翻倍；精确分摊属估算不属实测，暂不做）；② 只覆盖**配方消耗**路径 —— 采掘/收获/建造耗材/摄入不产生流向记录 ⇒ **查无记录 ≠ 无流向**。

**验证**：编译 0/0 · 单测 **487 通过 / 0 失败** · 双区 dll `BCE2E995531EBB139CD97093FCB69F6C` · GATE-DELTA PASS。

---

## 13. 第七批：测试数据按钮覆盖经济中心全内容（REQ-MVS-019，2026-09-20 用户需求）

**需求原文**：「将测试数据按钮的测试范围扩展覆盖经济系统中心全内容。」

| 项 | 落地 |
|---|---|
| **新 fixture** | `Application/SystemCenters/Dev/DevEconomyFixtures.BuildRandom(seed)` ⇒ 产出**只含经济五块 + KPI** 的详情快照（`ShowManifest = false`；其余分支字段保持 null ⇒ 顶栏/清单/其它业务视图不受影响） |
| **覆盖范围（满值 + 留缺口各一档）** | KPI 7 张（**留 1 张无值** ⇒ 检验 `—` 占位路径）· 社会背景（三档分档 + 4 条市场特征问答 + 5 行收购 + 1 行聚合（`RowIndex = -1`）+ 规模条 + 双 tooltip）· 派系状态（6 条影响因子 + 3 组事件流）· 市场（18 行 × 3~12 成员；**1 行 null** 检验"空行照旧占位"的既有约定；每品种**留 1 个无读数成员**检验 `—`；汇总 4 项）· 链路（上游 1 + 中心 + 下游 4，**1 格"不计数"** ⇒ 检验 `—`；边与投影与 Provider 同构）· 派系列表（10 行 + 3 组头，**含玩家殖民地行**） |
| **取值点（单一）** | `MasterControlTabWindow.EconomyForDisplay(detail)`：`DevPreviewActive && UsesEconomyGrid(真实快照)` 时返回 fixture（按 `devSeed` 缓存）⇒ `Detail.cs` 经济分支**只经它**取 detail |
| **安全边界** | 只产 POCO；**不改真实快照对象、不入存档、不触碰 Provider 路径**；关→开立即恢复真实数据（与 `DevPopulationFixtures` / `DevArchiveFixtures` 完全同规） |
| **判据纪律** | "是不是经济中心"必须用**真实**快照判（`UsesEconomyGrid(detail)`）—— 若用 fixture 自身判，任何中心打开开关都会变成经济页（假数据顶掉真数据） |
| **文本纪律** | 全部文本在构建期 `.Translate()`（GOV-010：绘制期零翻译）；样例文本统一走新键 `PersonalChronicle.Dev.EconomySample`（中英各 1 条），**fixture 内零硬编码文案**（LOC） |

**验证**：编译 0/0 · 单测 487 通过 / 0 失败 · 双区 dll `F69640DFA38786094908AA899AD24A3B` · GATE-DELTA PASS。

### 13.1 修正（同批次内，用户 2026-09-20 反馈「我只要数据层次的随机，不改变原有字段」）

**问题**（用户实测截图取证）：第一版 fixture 把**样例文本**塞进了**文案字段**（KPI `Note` / `SubText` / 列头 / 徽章 / 判据行 / 图例），并**猜错键名** ⇒ 界面出现 `MVS.Economy.Faction.Status.Title`、`MVS.Economy.FactionList.Col.Tech` 这类**键名泄漏**，以及 `DevCommodity_*` 造名。

**修正（同批次内重做 fixture）**：

| 项 | 修正后 |
|---|---|
| **文本来源** | **一律取真实 Keyed**，且**先抓语言包核对键名**：`MVS.Economy.Kpi.*`（7 组含 `.Note`）· `FactionDetail.{Title,Factor.Title,Factor.Badge,Event.Title,Event.Badge1/2}` · `FactionDetail.Faction.Royal.*`（因子 6 / 事件 3 / 画像）· `FactionList.{Col.Faction,Col.Tech,Col.TradersSummary,Filter.*,Group.*,Legend.*,Rule.Title,Rule.L1-L4}` · `Society.*` 与 `Society.Faction.Royal.*`（Tier1-3 / Q1-3 / Headline / Population / Chip）· `Market.{Title,Stencil,Badge,Col.*,Sub,Sum.Produce/Consume/Net/Index,Footnote}` · `Flow.*` · `Tech.*` |
| **名称类** | **取真实 Def**：物品/链路节点/品种显示名 = `DefDatabase<ThingDef>` 采样；派系名 = `Find.FactionManager`（无世界时退回真键文案） |
| **可选文案字段** | **留空**（`SubText` / `FactorsTip` / `FactionDetail.SubText`）—— 真实构建路径本就在"无内容"时留空，dev 不得为填空借用无关键 |
| **随机范围** | **只作用数值与条目**：件数 / 份额（**归一后**）/ 行数 / 成员数 / 库存 / 涨跌幅 / 语气四色轮转；结构（列数 3 与 5、分组行位置、边与投影）与真实构建路径一致 |
| **删除** | 键 `PersonalChronicle.Dev.EconomySample`（中英各 1）—— 不再需要样例文本模板 |

**复验**：编译 0/0 · 单测 487 通过 / 0 失败 · 双区 dll `5306F34BFF377077476AC915C5C989F6` · GATE-DELTA PASS。
