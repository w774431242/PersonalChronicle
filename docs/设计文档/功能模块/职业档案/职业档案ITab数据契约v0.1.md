# 职业档案 ITab 数据契约 v0.1

> 状态：信息架构冻结后的**第一份数据契约**（Phase 2 产物）。
> 目的：把「职业档案」ITab 6 个页面（概览 / 履历 / 职称 / 成果 / 论文 / 荣誉）的每一个
> 字段，绑定到确定的「数据对象 → 数据来源 → 数据类型 → 持久化方式 → 计算归属 → 刷新机制」。
> 本契约只定数据，不写 UI 像素、不写业务模型实现。
>
> 配套原型：`docs/UI预览/人物档案视窗/职业档案Tab预览.html`（信息架构原型，mock 数据，
> 不代表最终 UI；其中命中"UI 反向规定业务模型"的字段已在本文「风险标注」列指出）。
>
> 引擎基线：RimWorld 1.6 / PersonalChronicle v1.1.4。
> 分层铁律：UI 只消费 `DetailSnapshot` 等 Read Model 快照；排序 / null-guard / 翻译键解析
> 归属 `ArchiveUiDataProvider`；持久化归属 `ChronicleGameComponent` + `PawnObject`（Scribe）。

---

## 0. 总览：6 页字段与后端就绪度

| 页面 | 后端数据就绪 | 说明 |
| --- | --- | --- |
| 概览 Overview | ✅ 已就绪 | 全部字段已由 `DetailSnapshot` 派生，无新增模型 |
| 履历 Resume | ✅ 已就绪 | 时间线 / 里程碑 / 关键事件已由 `DetailSnapshot` 派生 |
| 职称 Title | ❌ 未实现 | 后端无 `TitleRecord` 领域模型，全字段为**占位契约**（待领域设计） |
| 成果 Achievement | ❌ 未实现 | 后端无 `AchievementRecord` / `ProjectRecord`，全字段为**占位契约** |
| 论文 Thesis | ❌ 未实现 | 后端无 `ThesisRecord` / `DefenseRecord`，全字段为**占位契约** |
| 荣誉 Honor | ⚠️ 部分就绪 | 勋章墙已由 `MedalView` 派生；"总等级/综合评定"为 UI 聚合，**非持久化模型** |

> **重要**：「职称 / 成果 / 论文」三页当前**没有任何后端数据**。
> 本契约为它们定义了**目标数据结构**（字段 → 建议数据对象），但标记为「未实现」。
> 在后端领域模型落地前，这三页在真实 ITab 中只能渲染空态占位，不能编造数据。

---

## 1. 全局共用：固定身份卡（Identity Card）

> 位于 ITab 顶部，6 页共用、不随二级导航切换而重建。

| UI 字段 | 数据对象 | 数据来源 | 数据类型 | 持久化 | 计算归属 | 刷新机制 | 当前状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 姓名 | `Pawn.LabelCap` / `Pawn.Name` | `Pawn`（活体）/ `PawnObject.NameCache`（已故） | `string` | 存档随 Pawn | `PawnObject.EnsureNameCache`（死亡时落缓存） | 选中 pawn 时取一次；改名后实时 | ✅ 已就绪 |
| 当前职称 | 见「职称页」主职称字段 | 同职称页 | `string` | 同职称页 | 同职称页 | 切换职称页 / 授勋事件 | ❌ 占位（依赖职称模型） |
| 专业方向 | `PawnObject.PrimaryWorkTypeLabel`（建议新增） | 工时占比最高工种类别 | `string` | `PawnObject`（建议 append-only，不 bump schema） | `WorkIntensityView` 派生 / `CareerBarView` 取 `IsPrimary` | 每日聚合缓存失效时 | ⚠️ 需新增字段 |
| 综合星级 | 见「荣誉页·总等级」 | UI 聚合（勋章权重求和） | `int`（1–5） | **不持久化** | `ArchiveUiDataProvider` 运行时聚合 | 每次打开 ITab | ⚠️ UI 聚合（非模型） |

> **风险标注（来自原型评审）**：身份卡「综合星级」是 UI 聚合值，不得固化为持久化"总等级"
> 字段；原型把星级画成"神秘五星总等级"属于 UI 反向规定业务模型，落地时改为勋章权重聚合显示。

---

## 2. 概览页 Overview

> 数据全部来自 `DetailSnapshot`（runtime-derived，不持久化，由 `ArchiveUiDataProvider.BuildDetail` 一次性聚合）。

| UI 字段 | 数据对象 | 数据来源 | 数据类型 | 持久化 | 计算归属 | 刷新机制 | 当前状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 生命周期阶段 | `LifePhaseView[]` | `DetailSnapshot.LifePhases` | `IReadOnlyList<LifePhaseView>` | 否（派生） | `BuildDetail` → `LifePhases` | 打开 ITab 时重建 | ✅ 已就绪 |
| 职业画像 · 工种类占比条 | `CareerBarView[]` | `DetailSnapshot.CareerBars` | `IReadOnlyList<CareerBarView>` | 否 | `BuildDetail` → `CareerBars` | 同上 | ✅ 已就绪 |
| 关键里程碑卡 | `MilestoneView[]` | `DetailSnapshot.Milestones` | `IReadOnlyList<MilestoneView>` | 否 | `BuildDetail` → `Milestones` | 同上 | ✅ 已就绪 |
| 关键事件流（Top-N） | `KeyEventView[]` | `DetailSnapshot.KeyEvents` | `IReadOnlyList<KeyEventView>` | 否 | `BuildDetail` → `KeyEvents` | 同上 | ✅ 已就绪 |
| 社会关系摘要 | `RelationView[]` | `DetailSnapshot.Relations` | `IReadOnlyList<RelationView>` | 否 | `BuildDetail` → `Relations` | 同上 | ✅ 已就绪 |
| 住所 / 工坊 | `ResidenceView` / `WorkplaceView` | `DetailSnapshot.Residence` / `.Workplace` | 单对象 | 否（快照；底层 `PawnObject.Residence/Workplace` 持久化） | 采样 Patch（A/B 方案） | 120 tick 采样 + 打开 ITab 取快照 | ✅ 已就绪 |
| 健康残值 · 资产折旧 | `HealthView` | `DetailSnapshot.Health` | 单对象 | 否（派生） | `BuildDetail` → `Health` | 打开 ITab 时重建 | ✅ 已就绪 |

> **风险标注**：原型「概览」偏 RPG 面板化（六边形雷达 / 大数字卡）。契约只锁数据字段，
> 不锁布局；落地建议回归"时间线 + 里程碑 + 关键事件"的信息层级（与履历页一致），
> 避免把概览做成属性面板。

---

## 3. 履历页 Resume

> 以时间线为核心，节点可点击下钻到 `KeyEventView` / `MilestoneView` 明细。

| UI 字段 | 数据对象 | 数据来源 | 数据类型 | 持久化 | 计算归属 | 刷新机制 | 当前状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 时间线节点 | `LifePhaseView` + `MilestoneView` + `KeyEventView` | `DetailSnapshot.LifePhases / Milestones / KeyEvents` | 合并有序流（按 `RawTick` 升序） | 否 | `BuildDetail` 预排序；窗口只消费 | 打开 ITab 时 | ✅ 已就绪 |
| 节点语义色 | `LifePhaseView.Kind` / `MilestoneView.KindKey` / `KeyEventView.IsHighlight` | 同上 | `enum` / `bool` | 否 | Read Model 提供中性语义键；UI 仅做着色 | 同上 | ✅ 已就绪 |
| 节点点击下钻 | `RawTick` + 事件流索引 | `DetailSnapshot.RawEvents` | `ChronicleEvent` 引用 | 否 | `BuildDetail` 组装 `RawEvents`（已去 null、按 tick 排序） | 同上 | ✅ 已就绪 |
| 阶段分段标题（起源/加入/活跃/离世） | `LifePhaseView.PhaseKey` | `DetailSnapshot.LifePhases` | `string`（翻译键） | 否 | `BuildDetail` 解析翻译键 | 同上 | ✅ 已就绪 |

> **风险标注**：原型时间线方向（自上而下、阶段分段）获评审肯定，契约予以固化。
> 节点语义着色必须由 Read Model 提供 `Kind`/`KindKey` 中性键，UI 不硬编码翻译。

---

## 4. 职称页 Title（❌ 后端未实现 — 目标契约）

> **当前无任何后端模型**。以下为建议的目标数据结构，供后续领域设计（AI-002 先架构后编码）
> 落地。落地前此页渲染空态占位 + "数据模型待实现"提示。

### 4.1 建议数据对象（待建）

```
TitleRecord                       // 单条职称获得记录（append-only，写事件流）
  - string DefName                // 职称 Def 稳定键（TitleDef.defName）
  - int Tier                     // 职级（1..N，如 助理/中级/高级/首席）
  - long GrantedTick             // 授予 game tick
  - string GrantedByEventKey     // 触发来源（晋升事件 / 手动授衔）
  - bool IsCurrent               // 是否为当前主职称（每 Pawn 至多 1 条 true）

TitleDef (Defs/ XML)              // 职称定义（数据驱动，符合 AI-003 扩展点优先级）
  - string defName
  - string labelKey
  - int order
  - string categoryKey           // 工程技术 / 医疗 / 战斗 ...（专业方向分组）
  - List<string> PrerequisiteDefNames  // 下一职称资格前置
```

### 4.2 字段契约

| UI 字段 | 数据对象 | 数据来源 | 数据类型 | 持久化 | 计算归属 | 刷新机制 | 当前状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 当前主职称 | `TitleRecord(IsCurrent)` | `PawnObject.TitleRecords` | `string`（label） | `PawnObject`（append-only） | 授衔 Patch / 手动 | 授衔事件触发 `DataRevision` | ❌ 占位 |
| 职称路线图（已得/当前/下一） | `TitleDef` 链 | `DefDatabase<TitleDef>` + 已得 `TitleRecord` | 有序 `TitleDef[]` | 定义存 XML；已得存 PawnObject | Read Model 按 `order` 串链 | 打开 ITab / 授衔 | ❌ 占位 |
| 下一职称资格 | `TitleDef.PrerequisiteDefNames` | `DefDatabase<TitleDef>` | `List<string>` | XML | 定义即配置 | 同上 | ❌ 占位 |
| 历史授衔时间线 | `TitleRecord[]` | `PawnObject.TitleRecords` | 有序流（按 `GrantedTick`） | `PawnObject` | 授衔 Patch | 授衔事件 | ❌ 占位 |

> **风险标注**：原型把"职称路线图"做成确定性进度条（如 首席=第 4 级满）。
> 契约明确路线图必须由 `TitleDef.order` + `Prerequisite` **数据驱动**，不得硬编码级数。
> 在 `TitleDef` 体系落地前，职称页不可显示任何模拟职称。

---

## 5. 成果页 Achievement（❌ 后端未实现 — 目标契约）

> 与"产出宫格"区分：产出宫格是**累计产值统计**（`ProductionTypeViews` 已就绪），
> 而"成果"是**命名项目/代表作**（如"建成首座 geothermal 电站"），需独立 `ProjectRecord`。

### 5.1 建议数据对象（待建）

```
ProjectRecord                     // 单个命名成果（重大建造/科研里程碑）
  - string DefName                // 项目稳定键（或事件派生 id）
  - string Label                  // 成果名（可玩家自定义 / 事件标题）
  - string CategoryKey            // 建筑 / 科研 / 农业 / 医疗 ...
  - long CompletedTick
  - float MarketValue             // 折算银币（可选）
  - string SourceEventKey         // 触发事件（Built / ResearchFinished ...）
```

### 5.2 字段契约

| UI 字段 | 数据对象 | 数据来源 | 数据类型 | 持久化 | 计算归属 | 刷新机制 | 当前状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 成果列表（代表作） | `ProjectRecord[]` | `PawnObject.ProjectRecords`（待建） | 有序流 | `PawnObject`（append-only） | 建造/科研完成 Patch 捕获 | 完成事件触发 | ❌ 占位 |
| 成果分类汇总 | `ProjectRecord` 按 `CategoryKey` 分组 | 同上 | 分组聚合 | 派生 | Read Model | 打开 ITab | ❌ 占位 |
| 累计产值（银） | `ProductionSummaryView.TotalMarketValue` | `DetailSnapshot` 已派生 | `float` | 底层 `ProductionAccumulator` 持久化 | `BuildDetail` | 打开 ITab | ✅ 已就绪（复用产出宫格） |
| 产出类目 Top-N | `ProductionTypeView[]` | `DetailSnapshot.ProductionTypeViews` | `IReadOnlyList` | 底层持久化 | `BuildDetail` | 打开 ITab | ✅ 已就绪 |

> **风险标注**：原型把"成果"与"累计产值"混为一谈（大数字 + 类目条）。
> 契约将二者解耦：累计产值复用现有产出宫格快照；**命名成果**才是本页新增领域，
> 需 `ProjectRecord`（建议从 Built/ResearchFinished 事件流派生，符合现有 Capture 架构）。

---

## 6. 论文页 Thesis（❌ 后端未实现 — 目标契约）

> RimWorld 原版无"论文/答辩"概念，属 mod 原创领域。以下为目标契约；落地前渲染空态。

### 6.1 建议数据对象（待建）

```
ThesisRecord                     // 单篇论文/著作
  - string DefName
  - string Title                 // 论文标题
  - string FieldKey              // 学科领域
  - long PublishedTick
  - string QualityKey            // 质量评级（S/A/B/C，数据驱动）
  - List<string> CoAuthorStableIds  // 合著者（跨 PawnObject 引用）

DefenseRecord                    // 答辩记录（可选，独立于 Thesis）
  - long DefenseTick
  - string VerdictKey            // 通过 / 优秀 / 暂缓
  - string CommitteeText         // 答辩委员会（已本地化）
```

### 6.2 字段契约

| UI 字段 | 数据对象 | 数据来源 | 数据类型 | 持久化 | 计算归属 | 刷新机制 | 当前状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 论文列表 | `ThesisRecord[]` | `PawnObject.ThesisRecords`（待建） | 有序流 | `PawnObject`（append-only） | 写作完成事件 / 手动 | 完成事件 | ❌ 占位 |
| 质量星标（视觉摘要） | `ThesisRecord.QualityKey` | `DefDatabase` 质量评级 | `enum` 派生星数 | 评级存 Def；记录存 PawnObject | Read Model 映射星数 | 打开 ITab | ❌ 占位 |
| 合著者 | `ThesisRecord.CoAuthorStableIds` | 跨 `PawnObject` 引用 | `List<string>` stableId | PawnObject | Read Model 解析 label | 同上 | ❌ 占位 |
| 答辩结论 | `DefenseRecord.VerdictKey` | `PawnObject.DefenseRecords`（待建） | `string` 翻译键 | PawnObject | 答辩事件 | 答辩事件 | ❌ 占位 |

> **风险标注（来自原型评审）**：论文"质量星标"应是**视觉摘要**（由 `QualityKey` 映射），
> 不得作为独立持久化"评分"字段写回业务模型。评审明确指出"论文星标只是视觉汇总"。
> 合著者跨 PawnObject 引用须在 Read Model 解析，UI 不持有引用逻辑。

---

## 7. 荣誉页 Honor（⚠️ 部分就绪）

> 勋章墙已就绪；"总等级/综合评定"为 UI 聚合（非模型）。

### 7.1 字段契约

| UI 字段 | 数据对象 | 数据来源 | 数据类型 | 持久化 | 计算归属 | 刷新机制 | 当前状态 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 勋章墙（每系列最高档） | `MedalView[]`（`IsHighestTier=true`） | `DetailSnapshot.Medals` | `IReadOnlyList<MedalView>` | 底层 `PawnObject.GrantedMedals`（`List<string>` defName）持久化 | `ArchiveUiDataProvider.BuildMedals`（阈值引擎派生） | 打开 ITab | ✅ 已就绪 |
| 勋章图标 | `MedalDef`（texPath 缺省 `<DefName>.png`） | `Textures/Medals/` | `Texture2D` | 资源文件 | 引擎加载（缺省回退） | 首次加载缓存 | ✅ 已就绪 |
| 勋章增益文案 | `MedalView.BuffText` | `MedalBuffDef.displayBonus` | `string` | XML 定义 | `BuildMedals` 解析 | 打开 ITab | ✅ 已就绪 |
| 新授标记 | `MedalView.IsNewAward` | 判定引擎 `IsNewAward` | `bool` | 派生 | `MedalAwardEvaluator` | 授勋事件 | ✅ 已就绪 |
| 总等级 / 综合评定 | UI 聚合（勋章权重求和） | `MedalView.Tier` 权重 | `int` 1–5 | **不持久化** | `ArchiveUiDataProvider` 运行时聚合 | 打开 ITab | ⚠️ UI 聚合 |

> **风险标注（来自原型评审）**：原型把荣誉页做成"神秘五星总等级 + 包裹式卡片"，
> 属于 UI 反向规定业务模型。契约明确：
> 1. 勋章墙只展示 `IsHighestTier` 单枚（系列归并），不展开全部档位；
> 2. "总等级"仅作勋章权重**聚合展示**，`PawnObject` 不得新增 `TotalGrade` 字段；
> 3. 避免"包裹式五星"视觉，回归扁平勋章墙 + 进度灰态。

---

## 8. 刷新与生命周期契约（全局）

| 维度 | 契约 |
| --- | --- |
| 快照入口 | 所有 6 页数据经 `IArchiveUiDataProvider` 一次性 `BuildDetail` 聚合为 `DetailSnapshot`；窗口只 `EnsureSnapshot` 取引用，不重建 |
| 排序 / null-guard | 归属 `ArchiveUiDataProvider`（预排序、去 null），窗口绘制路径零排序零判空 |
| 持久化边界 | 仅 `ChronicleGameComponent` + `PawnObject`（Scribe）可写；UI 经 `IArchiveService` 命令式写入（如授衔/改名），禁止 UI 直写 Save |
| 新增模型策略 | 职称/成果/论文若为 append-only 列表，落在 `PawnObject` 新字段，**不 bump Schema Version**（参考 v1.1.4 工坊/住所 append-only 先例） |
| 扩展点优先级 | 优先 `TitleDef`/`ProjectDef`/`ThesisDef` 等 XML Def + `DefModExt`；其次 `PawnObject` 字段；再次 Capture Patch；最后 Harmony（符合 AI-003） |
| 刷新触发 | 捕获事件（授勋/建造/授衔）→ `DataRevision` 失效聚合缓存 → 下次打开 ITab 重建快照 |
| 主题 | UI 只消费 `UITheme` 令牌 + `UIComponents` 组件；6 页共用同一主题，不在页内散落 `GUI.color` |

---

## 9. 待决问题（需用户/架构评审确认）

1. **职称/成果/论文三套领域模型**是否纳入下一版本开发？若纳入，须先完成各自
   《领域设计》（AI-002 先架构后编码），本文仅为 UI 数据视角的目标契约。
2. **专业方向**（身份卡）是否新增 `PawnObject.PrimaryWorkTypeLabel` 持久化字段，
   还是每次由 `CareerBarView.IsPrimary` 实时派生（不持久化）？
3. **综合星级 / 总等级**的权重公式（金/银/铜各计多少）是否需 XML 可配置？
4. **合著者跨 PawnObject 引用**的解析成本（N² 遍历）是否在大规模殖民地下可接受？
   建议限定合著者上限或延迟解析。

---

## 10. 变更记录

| 版本 | 日期 | 作者 | 说明 |
| --- | --- | --- | --- |
| v0.1 | 2026-08-16 | AI | 首版数据契约：6 页字段全绑定数据对象/来源/类型/持久化/计算/刷新；标注职称/成果/论文三页后端未实现；固化原型评审中的风险纠正项 |
