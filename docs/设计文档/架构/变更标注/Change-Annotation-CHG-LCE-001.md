---
change_id: CHG-LCE-001
version: v1.4.x
date: 2026-09-20
author: AI（主 Agent）
scope: [Source/PersonalChronicle/**, Tests/**, Defs/**, Languages/**, _scripts/**, docs/**]
files:
  - path: Source/PersonalChronicle.csproj
    type: M
    symbols: MVS 四条 Compile glob 移除 / Economy glob 注释更新
    lines: 57-64, 70-72
  - path: Source/PersonalChronicle/ChronicleMod.cs
    type: M
    symbols: ValidateMvsAssets 调用块 + 4 个 MVS 校验方法整体删除（-303 行，279 行终稿）
    lines: 163-173, 176-467
  - path: Source/PersonalChronicle/Application/ReadModels/SystemCenterSnapshots.cs
    type: M
    symbols: 五块经济快照字段（EconomyMarket/Flow/Factions/Society/FactionDetail）→ ColonyEconomy
    lines: 233-260
  - path: Source/PersonalChronicle/Application/ReadModels/ColonyEconomySnapshot.cs
    type: A
    symbols: ColonyEconomySnapshot（TitleText / BodyLines）
    lines: 1-30
  - path: Source/PersonalChronicle/Application/ReadModels/SystemCenterReadModelProvider.cs
    type: M
    symbols: 选择态四组整体切除 / 投影块删除 / 五块经济构建→ColonyEconomy / ResolveSelectedFactionDefName 删除 / MVS using 移除
    lines: 1-238, 390-430, 515-573
  - path: Source/PersonalChronicle/Application/SystemCenters/IColonyEconomyDataProvider.cs
    type: A
    symbols: IColonyEconomyDataProvider（BuildColonyEconomy）
    lines: 1-28
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinColonyEconomyProvider.cs
    type: A
    symbols: BuiltinColonyEconomyProvider（KPI 三格 ① 原版真值 + 骨架页构建）
    lines: 1-160
  - path: Source/PersonalChronicle/Application/SystemCenters/SystemCenterDataRegistry.cs
    type: M
    symbols: MarketContracts.Wire 移除 / Register(BuiltinEconomyProvider) → Register(BuiltinColonyEconomyProvider)
    lines: 126-134
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Economy.cs
    type: M
    symbols: 整文件重写（UsesColonyEconomyPage / DrawColonyEconomyPage；五面板与 dev fixture 引用全删）
    lines: 1-104
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Detail.cs
    type: M
    symbols: UsesEconomyGrid/EconomyForDisplay 分支 → UsesColonyEconomyPage；两处注释更新
    lines: 199-201, 246-255, 433-436
  - path: Source/PersonalChronicle/Capture/Patch_IncidentWorker.cs
    type: M
    symbols: MVS 市场事件 try/catch 段 + using + 头注释删除（战斗记录段原样保留）
    lines: 1-120
  - path: Source/PersonalChronicle/Capture/LocationAtlasCapture.cs
    type: M
    symbols: PickTraderKind(faction,null) 恒等结果内联；MVS.Data using 移除
    lines: 6, 548-560
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.cs
    type: M
    symbols: ResetMarketEventLog 方法 + FinalizeInit 调用点删除
    lines: 39-61, 292-298
  - path: Tests/EconomyMarketFormatTests.cs
    type: M
    symbols: 面板列宽夹具 → RuifBridgeColumnShareTests（SampleShares 化；三个面板测试删除、两测试保留改写）
    lines: 205-374
  - path: Defs/SystemCenterDefs.xml
    type: M
    symbols: economy_center（label/职责/cap/events/hooks 重写）+ 两处跨中心引用改名
    lines: 31, 86-121, 138
  - path: Languages/English/DefInjected/SystemCenterDef/SystemCenterDefs.xml
    type: M
    symbols: economy_center 英文块（职责 5→4）+ 两处引用改名
    lines: 16, 28-37, 46
  - path: Languages/ChineseSimplified/Keyed/ColonyEconomy.xml
    type: A
    symbols: 10 键（KPI 三格 + Note + 页面标题/说明三行）
    lines: 1-16
  - path: Languages/English/Keyed/ColonyEconomy.xml
    type: A
    symbols: 同上（英文）
    lines: 1-16
  - path: _scripts/sync_release.ps1
    type: M
    symbols: 默认同步清单 MVS 三件 → ColonyEconomy 两件
    lines: 3-16
  - path: docs/changelog/v1.4.x_2026-09-20_经济中心清场重建.md
    type: A
    symbols: CHG-LCE-001
    lines: 1-90
  - path: docs/设计文档/功能模块/经济体系/REQ_经济中心重构-本地殖民地经济.md
    type: A
    symbols: REQ-LCE-001
    lines: 1-262
  - path: docs/设计文档/架构/EXC-2026-001.md
    type: M
    symbols: 清单变更记录 +#11 语义回收行
    lines: 62
  - path: docs/设计文档/架构/EXC-2026-006.md
    type: M
    symbols: Status → Obsolete + 变更记录
    lines: 3, 59-63
  - path: docs/设计文档/架构/EXC-2026-007.md
    type: M
    symbols: Status → Obsolete
    lines: 3
  - path: docs/设计文档/架构/EXC-2026-008.md
    type: M
    symbols: Status → Obsolete
    lines: 3
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/EXC_MVS定价通道A.md
    type: M
    symbols: YAML status → Obsolete + 顶部作废说明 + §8 边界表
    lines: 5, 22-29
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/EXC_动态价格登记.md
    type: M
    symbols: 禁区限定段补「MVS 线已删除」说明
    lines: 33-38
  - path: docs/设计文档/架构/Change-Annotation-CHG-LCE-001.md
    type: A
    symbols: 本文件自身（自指一条；gate-runner 的标注路径自动剔除在 Windows 反斜杠形态下失效，按既有先例自指）
    lines: 1-240
---

# 变更标注 · CHG-LCE-001（经济中心清场重建 —— MVS 市场体系整体删除）

**Change ID：** `CHG-LCE-001` · **Version：** v1.4.x · **Date：** 2026-09-20
**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**前置批次：** `REQ-LCE-001`（Draft v2，用户「全部删除」拍板）· AI-001 判定 `GR-AI001-20260920-lce.md`
**派发记录：** 前置协议全走（use_skill → `route-task.ps1`（命中 pc-orchestrator）→ `audit-subagents.ps1 -Blocking` **PASS 7/7** → 总控 AI-001 **GO**（删除批触发 Architecture Review））

## Change Annotation

- 用户裁决（2026-09-20）：「将现有的经济系统中心进行全部删除……不做市场，只做本地殖民地经济」——
  **回归立项初心**（`经济体系/README.md` 2026-09-15 原文「产能→库存→财富的观测与记账层，而非模拟市场」；
  MVS 是经 `EXC-MVS-001` 例外扩进来的 ⇒ 删除后红线恢复零例外）。
- Scope(摘要): MVS 市场体系（38 .cs + 五面板 + 经济 Provider/契约/快照/测试/Defs/Patches/语言包）
  **整体删除**；捕获层（ItemFlowLedger / 工时 / 产出）**零改动**；经济中心重建为**最小骨架**
  （KPI 三格 + 建设中说明）；4 项 EXC 作废。

### 改动清单（逐文件 · 28 项在 git 视野内；删除项另表见下）

| 文件 | 类型 | 涉及符号 | diff 摘要 | 关联 Gate |
|---|---|---|---|---|
| `Source/PersonalChronicle.csproj` | M | Compile globs | MVS 四条 glob 移除；Economy glob 注释更新（**保留** chrome/格式化） | ARCH |
| `ChronicleMod.cs` | M | `ValidateMvsAssets` 等 | MVS 启动断言调用块 + 4 个校验方法整体删除（-303 行） | DATA/REL |
| `SystemCenterSnapshots.cs` | M | `SystemCenterDetailSnapshot` | 五块经济字段 → `ColonyEconomy`（死字段删除非留空） | UI |
| `SystemCenterReadModelProvider.cs` | M | 选择态 / 经济构建 | 选择态四组整体切除；五块构建 → `BuildColonyEconomy`；解析器删除 | DATA/UI |
| `SystemCenterDataRegistry.cs` | M | 组合根 | 去 `MarketContracts.Wire`；注册骨架 Provider | ARCH |
| `MasterControlTabWindow.Economy.cs` | M | 整文件 | 重写为骨架页（KPI 条 + 建设中面板） | UI/LAND-010 |
| `MasterControlTabWindow.Detail.cs` | M | 分支 | `UsesEconomyGrid` → `UsesColonyEconomyPage`（去 dev fixture 通道） | UI |
| `Patch_IncidentWorker.cs` | M | Harmony Postfix | MVS 段删除（战斗段保留；target/类型不变） | COMP |
| `LocationAtlasCapture.cs` | M | `PickTraderKind` | 恒等内联（语义逐字保留）；去 MVS using | COMP |
| `ChronicleGameComponent.cs` | M | `ResetMarketEventLog` | 方法与调用点删除 | DATA |
| `BuiltinColonyEconomyProvider.cs` | A | 骨架 Provider | KPI 三格（财富/银/库存市值 ① 真值）+ 骨架页 | UI/DATA |
| `ColonyEconomySnapshot.cs` / `IColonyEconomyDataProvider.cs` | A | 契约/快照 | 新中心整页面 | UI/DATA |
| `Tests/EconomyMarketFormatTests.cs` | M | 列宽夹具 | → `RuifBridgeColumnShareTests`（保留 RUIF 归一化契约两条） | TEST |
| `Defs/SystemCenterDefs.xml` + EN DefInjected | M | `economy_center` | 改名「Colony Economy」；职责 5→4；cap 更名；事件成对裁决；hooks 空 | DATA/LOC |
| Keyed `ColonyEconomy.xml` ×2 | A | 10 键 | KPI + 页面说明（中英镜像） | LOC |
| `_scripts/sync_release.ps1` | M | 默认清单 | MVS 三件 → ColonyEconomy 两件 | REL |
| EXC ×6（001/006/007/008/MVS-001/动态价格） | M | 状态/记录 | 4 项作废；001 补 #11 语义回收；边界表同步 | REL |
| REQ / Changelog / README | A/M | — | REQ-LCE-001 与 Change Summary；README 边界表 | REL |

### 删除面（59 项 · dev 与 release 双区同步清理；git 中均未跟踪 ⇒ 不入 front matter，此表留痕）

| 区域 | 内容 |
|---|---|
| `Source/PersonalChronicle/MVS/**` | 38 .cs（Domain 12 / Data 16 / Application 3 / Adapters 6 / Compatibility 1 含 `StatPart_MarketValue`） |
| `Economy/UI/` | EconomyMarketPanel / EconomySocietyPanel / EconomyFactionDetailPanel / EconomyFactionSelectorPanel / EconomyInventoryChainPanel |
| `Application/SystemCenters/` | BuiltinEconomyProvider ×6 + MarketContracts + DevEconomyFixtures + IEconomy* ×3 |
| `Application/ReadModels/` | EconomyMarketPanelSnapshot / EconomyFlowGraphSnapshot / EconomyFactionStatusSnapshot / EconomySocietySnapshot / EconomyFactionDetailSnapshot |
| `Tests/` | TradeInterfaceMatch / TradeBias / MvsAdapterContract / MarketShockModel / ItemLevelFlowModel / FactionTraits / FactionScaleModel / FactionPurchase / FactionComposition / CommodityVisibility / EconomySnapshotContract |
| `Defs/` + `Patches/` | MVS_Commodities / MVS_FlowEdges / MVS_ScaleTuning；MVS_StatParts / MVS_IncidentImpacts |
| `Languages/` | Keyed MVS.xml ×2（357×2 键）+ DefInjected 6 文件 |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll`
> 已重编并两区同步（MD5 `1C9B5FAEA7F3585D8AD200661B4D5194`，两区 MATCH，需重启游戏）。

### 关键内容片段

```diff
// 组合根：MVS 接线 → 骨架 Provider
- MarketContracts.Wire(Find.World);
- registry.Register(new BuiltinEconomyProvider());
+ registry.Register(new BuiltinColonyEconomyProvider());

// 快照：五块经济 → 一块骨架
- public EconomyMarketPanelSnapshot EconomyMarket;  // …×5
+ public ColonyEconomySnapshot ColonyEconomy;

// 事件补丁：MVS 副作用段删除（战斗段保留）
- using PersonalChronicle.MVS.Application;
- try { if (!MarketEventSink.HasImpact(def)) return; MarketEventSink.Record(def, faction, tick); }
- catch (Exception ex) { … }
  //    Rollback: delete the MVS try/catch block plus its two `using` lines —— 本次照此配方执行。

// 图鉴：恒等内联（语义不变）
- TraderKindDef kind = SettlementTradeReader.PickTraderKind(faction, null);  // 该调用恒 null
- if (kind != null) { ApplyTraderKindSnapshot(loc, kind); } else { /* 清空三字段 */ }
+ loc.TraderKindDefName = null; loc.PermitRequiredDefName = null; loc.TradeKindKeys = new List<string>();
```

### 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖本批 git 视野内全部实际改动（生成脚本 `gen-lce-scope.ps1` 白名单交叉核对：**mine=28**；dll 走禁令①）
- [x] **已知例外（工具语义，如实登记）**：`docs/设计文档/功能模块/经济体系/README.md`（本批 1 行边界表更新）**未列入 front matter** ——
  exclude 清单中的历史条目裸 `README.md`（仓库根 README = 历史未提交改动，必须排除）在 gate-runner 的
  `-Exclude` **子串语义**下会命中一切 `README.md`，二者无法区分；该文件属批内改动，已在 Changelog「已知问题」段与下方「改动清单」表内留痕。
- [x] 清单范围 ⊆ Change Summary.Scope
- [x] 删除项（git 未跟踪）以正文表格留痕；发布区同名文件已显式清单删除（`lce-delete.ps1`，含 MVS 残检）
- [x] 每个 front matter 文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Changelog 互链一致
- [x] 临时文件：本批运行脚本（`lce-backup*.ps1` / `lce-delete.ps1` / `lce-trim-chroniclemod.ps1` / `gen-lce-scope.ps1`）在 `.codebuddy/tmp`（不入 git、不入发布区）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile .codebuddy/tmp/lce-out-of-scope.txt -Blocking` 自动校验（exclude=931）

### 支点事实求证记录（先证后用）

| 事实 | 求证方式 | 结论 |
|---|---|---|
| 捕获层是否依赖 MVS | code-explorer 全链测绘（定义/捕获/存储/消费） | ✅ 三件套全在 `PersonalChronicle.Domain`，零 MVS 依赖 ⇒ 原地保留 |
| 捕获层是否有 Scribe 变更 | 直读 `ChronicleGameComponent.ItemFlow.cs` | ✅ 零改动（schemaVersion 不变） |
| `PickTraderKind(faction,null)` 是否恒 null | 直读 `SettlementTradeReader.cs:140-162` 全分支 | ✅ `settlement == null ⇒ return null` ⇒ 内联恒等结果 |
| `evt.economy.reorder` 是否有消费方 | 全仓 grep | ✅ 制造/物流 `<consumes>`（成对保留）；`trade_settled` 无消费者（移除） |
| git 中本批删除项形态 | `git status` 过滤（MVS/经济路径 0 命中） | ✅ 均未跟踪 ⇒ 删除不进 diff（标注以正文留痕） |

### 回滚定位

1. 源码：`git` 历史 + `_backups/lce_cleanup_20260920/`（`dev_backup.zip` 44 项 / `dev_contracts_backup.zip` 3 项 / `release_backup.zip` 13 项）。
2. 发布区：解压 `release_backup.zip` 回原位（并还原旧 dll 备份）。
3. 旧档应急预案：如实测发现 `WorldComponent_Market` 段引发问题 ⇒ 加空壳兼容类（默认不启用）。
