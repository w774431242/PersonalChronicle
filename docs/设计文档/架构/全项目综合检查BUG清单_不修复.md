# 全项目综合检查 BUG 清单

> **审查基准版本**：2026-08-12（基于 `docs/设计文档/架构/` **v3.0 规范体系** 重新审查；同日进行 **第二次综合检查**）
> **审查范围**：`Source/PersonalChronicle`（约 82 个 .cs 文件）+ `Defs/` + `Languages/` + `Patches/`
> **审查依据（v3.0 规范体系）**：
> - `00-规范体系总纲.md`（GOV-001~012，优先级 P0~P5）
> - `00-规范关系矩阵.md`（MATRIX-001~010，依赖矩阵）
> - `00-统一术语与定义.md`（TERM-001~040）
> - `01-基础层规范.md`（BASE-001~012，TEST-001）
> - `02-架构层-核心架构规范.md`（ARC-001~012）
> - `03-架构层-数据与扩展规范.md`（DATA-001~012，SAVE-001~008）
> - `04-架构层-兼容与接入规范.md`（COMP-001~012）
> - `05-表现层规范.md`（UI-001~012，LOC-001~008，ASSET-001~008，PERF-001~006）
> - `06-变更日志与发布规范.md`（REL-001~012）
> - `07-AI开发规范.md`（AI-001~012，P0~P4 违规分级）
> - `00-统一验收标准.md`（GATE-ARCH/DATA/LOC/UI/COMP/SAVE/TEST/PERF/REL）
> - `附录-项目架构实施映射.md`（PersonalChronicle 落地映射）
> **审查方式**：静态代码扫描 + 关键文件逐行复核 + 翻译键双向完整性审计脚本（不运行游戏）。
> **修复状态**：
> - **第一次检查**：3 项合规缺口（BUG-ARCH-01/02/03）**2026-08-12 已全部修复**（编译 0 错 0 警，dll 已同步发布目录）。
> - **第二次检查**：新增 1 个真实 BUG（BUG-ARCH-04 翻译键缺失）+ 1 个 P3 治理项（BUG-ARCH-05 业务标识字符串散落）+ P4 多余翻译键清单。**已落地修复**（编译 0 错 0 警，dll + 中英翻译文件已同步发布目录）。
> - **基础层专向检查**：按 BASE-001~012 逐条扫描，新增 BUG-BASE-01（P1 god class）+ BUG-BASE-02（P3 日志格式不统一）。**本次仅建立清单，未修复**。

---

## 审查结论摘要

| v3.0 维度 | 结论 |
|---|---|
| MATRIX-001~010 单向依赖 / 循环依赖 | ✅ 未发现；Domain 无 Archive/UI 引用，无循环 |
| ARC-002 / MATRIX-002 Core 纯净边界 | ⚠️ 见 BUG-ARCH-03（Translation Key 字面量散落 Domain） |
| ARC-008 静态可变状态 | ✅ `DamageLedger` 为 static readonly 容器且有 Reset()（`ChronicleGameComponent.cs:198` 调用），生命周期闭环 |
| ARC-005 Contract/API 边界 | ✅ 未泄漏 UI/第三方类型 |
| DATA-008 事件输入校验 | ⚠️ 见 BUG-ARCH-01（Parameters 无容量上限） |
| DATA-011 / LOC-006 数据与本地化隔离 | ✅ Save 不存翻译文本，Domain 不执行 `.Translate()`，翻译仅在 UI 解析 |
| COMP-005 Harmony Patch 风险 | ✅ 3 个 Prefix 均只读不阻断；反射探测在静态构造期一次性完成并缓存 |
| PERF-001/004 热路径约束 | ✅ Tick/每帧无 LINQ、反射、无界分配；Capture 采集节流 |
| BASE-009 / COMP-009 空 catch | ⚠️ 见 BUG-ARCH-02 |
| BASE-010 / GOV-006 硬编码治理 | ⚠️ 见 BUG-ARCH-03 |
| ASSET-002 / UI-012 Theme Token | ✅ 窗口内无 `new Color(...)` 与魔法数字散落（`\d{2,4}f` 0 匹配，全走 UITheme/常量） |
| UI-003 UI 只读 Read Model | ✅ 窗口经 ArchiveUiDataProvider 消费快照，无 IArchiveService 直读 |
| LOC-005 翻译覆盖审计 | ⚠️ 见 BUG-ARCH-04（KpiEvents 缺失）；无重复 Key（v1.1.2 已删 NoWorkData）；P4 多余键若干 |

**总体判定（第二次检查后）**：v3.0 合规度**高**，无 P0/P1 级违规，无 High 级阻断 bug。第二次检查新增 **1 个 P2 真实 BUG（翻译键缺失）+ 1 个 P3 治理项（业务标识字符串散落）**；另确认一批 P4 多余翻译键（无运行期影响）。

---

## 第二次检查新增 BUG

### BUG-ARCH-04（P2 · LOC-005 翻译键缺失 — 真实运行期缺陷）
- **规则**：`LOC-005 翻译覆盖审计`（"发布前扫描：缺失 Key…"）；`GATE-LOC`（"英文/默认语言完整"）；`BASE-003 用户可见文本必须本地化`
- **文件**：
  - 引用点：`Source/PersonalChronicle/Archive/ArchiveMainTabWindow.cs:4151`（武器 Overview 的"事件"KPI 格 `"PersonalChronicle.UI.KpiEvents".Translate().ToString()`）
  - 缺失位置：`Languages/English/Keyed/Archive.xml`、`Languages/ChineseSimplified/Keyed/Archive.xml`
- **问题**：`PersonalChronicle.UI.KpiEvents` 为**完整翻译键**（非动态拼接），在代码中被 `.Translate()` 引用，但中/英翻译文件均**未定义**该键。运行时 `.Translate()` 会**原样回显 key 字符串**（`PersonalChronicle.UI.KpiEvents`），玩家在武器 Overview 会看到未翻译的原始 key。经双向审计脚本确认：**这是全项目唯一缺失的完整翻译键**（419 个代码引用 vs 555 个翻译定义，其余"缺失"均为动态拼接前缀或 Def labelKey 引用）。
- **同族佐证**：`KpiKills`（`:738,:760,:4153`）/`KpiHolders`（`:4155`）/`KpiCrafter`（`:4157`）均在翻译文件定义且中英对称；唯独 `KpiEvents` 遗漏。
- **影响**：武器详情 Overview 的"事件"KPI 格显示原始 key 文本。非崩溃，但属用户可见本地化缺陷。
- **严重度**：Medium（P2：违反 LOC-005/GATE-LOC，用户可见文本未本地化）
- **✅ 已修复（2026-08-12）**：在 `Languages/English/Keyed/Archive.xml` 与 `Languages/ChineseSimplified/Keyed/Archive.xml` 的 Kpi 键区（`KpiCrafter` 之后）各补一条：
  - 英：`<PersonalChronicle.UI.KpiEvents>Events</PersonalChronicle.UI.KpiEvents>`
  - 中：`<PersonalChronicle.UI.KpiEvents>事件</PersonalChronicle.UI.KpiEvents>`
  - 两个语言文件已同步发布目录。

### BUG-ARCH-05（P3 · BASE-010 业务标识字符串散落）
- **规则**：`BASE-010 硬编码治理`（"禁止散落：DefName、Translation Key、路径…"）；`GOV-006`（"魔法数字/字符串不得散落"）
- **文件**（业务标识魔法字符串跨 5 文件重复出现）：
  - `"Map"` / `"Caravan"`（PlaceKind 判别串）：`Data/ChronicleGameComponent.cs:504,525,541,557,574`；`Archive/ReadModels/ArchiveUiDataProvider.cs:693,1088`；`Archive/ArchiveMainTabWindow.cs:3619`；`Domain/PlaceVisit.cs:15`（注释）；`Api/DomainProviders/IPlaceProvider.cs:16`（注释）
  - `"World_"`（世界对象 StableId 前缀）：`Data/ChronicleGameComponent.cs:970`；`Capture/LocationAtlasCapture.cs:132,339,750`
  - `"tile:"`（商队地点 key 前缀）：`Data/ChronicleGameComponent.cs:559`；`Archive/ReadModels/ArchiveUiDataProvider.cs:693,1088,1090`；`Archive/ArchiveMainTabWindow.cs:3619,3621`
  - `"Unvisited"`（DeinitReason 值）：`Data/ChronicleGameComponent.cs:989`；`Capture/LocationAtlasCapture.cs:355`
- **问题**：以上业务标识以字符串字面量散落多处。`"Map"/"Caravan"/"tile:"/"World_"` 还是**持久化标识**（PlaceVisit.PlaceKind / StableId 前缀 / DeinitReason 写入存档）与外部 Provider 契约（IPlaceProvider），散落增加拼写漂移与跨文件不一致风险。相比 Translation Key（BUG-ARCH-03），这些是**存档/契约语义字符串**，改值会破坏旧存档，集中到常量类时**必须保持原值**。
- **影响**：纯质量项，无当前运行期缺陷（各处字面量一致）。
- **严重度**：Low（P3：规范治理项）
- **✅ 已修复（2026-08-12）**：新建 `Domain/PlaceVisitKeys.cs` 常量类集中 `KindMap="Map"` / `KindCaravan="Caravan"` / `WorldIdPrefix="World_"` / `TileKeyPrefix="tile:"` / `DeinitReasonUnvisited` / `DeinitReasonDestroyed` / `DeinitReasonAbandoned`（**保持原字符串值不变**，存档与契约兼容）；`Data/ChronicleGameComponent.cs`（7 处）、`Capture/LocationAtlasCapture.cs`（5 处，含 `"Destroyed"/"Abandoned"` 收敛）、`Archive/ReadModels/ArchiveUiDataProvider.cs`（2 处，`StartsWith` 改 Ordinal + `Substring(5)`→`TileKeyPrefix.Length`）、`Archive/ArchiveMainTabWindow.cs`（3 处，含 `LocationDeinitText` 分支）全部改引常量。

### P4 · 多余翻译键清单（无代码引用，可清理）
经双向审计确认以下键**只存在于翻译文件、代码无引用**（不影响运行，按 LOC-005 建议清理）：
`PersonalChronicle.UI.KpiBattles`、`KpiRelations`、`Kpi.Health`、`Kpi.Unit.PerDay`、`Kpi.WeekHours`（注意 `Kpi.WeekHours.S` 被引用）、`HealthValuation.Age`、`HealthValuation.Depreciation`、`HealthValuation.TipEmpty`、`HealthValuation.TipTotal` 等。
> 注：`KpiBattles` 在 `docs/设计文档/功能模块/战斗履历/战斗履历设计文档.md:198` 有设计引用，清理前需同步文档。

---

## BUG 清单（按 v3.0 规则 ID 标注）

### BUG-ARCH-01（P2 · DATA-008 输入保护缺口）
- **规则**：`DATA-008 事件输入与数据校验`（"参数范围、去重键和最大集合容量"）；附录 §4（"ID/类型/Tick/容量/去重校验"）
- **文件**：`Source/PersonalChronicle/Api/ArchiveEventInput.cs:53-62`（`IsValid`）
- **问题**：`ArchiveEventInput.IsValid` 仅校验 `SourceId` / `EventTypeDefName` / `Primary` 非空，**未校验 `Parameters` 条目数量与单值长度上限**。`TryRecord`（`ArchiveService.cs:3015`）在 `IsValid` 通过后即 `ToDictionary`（`:3085`）进入归档，外部 mod 可传入无界字典，造成存储膨胀与存档膨胀。
- **影响**：仅影响第三方 mod 误用/恶意写入；内置捕获路径的 Parameters 受控。非当前崩溃源。
- **严重度**：Medium（P2：违反强制输入校验规范，防御纵深不足）
- **✅ 已修复（2026-08-12）**：`ArchiveEventInput` 新增 `MaxParameters=32` / `MaxParameterKeyLength=64` / `MaxParameterValueLength=256` 常量，`IsValid` 增加 `ParametersWithinLimits()` 校验（数量 + 键长 + 值长），超限返回 `CaptureResult.Rejected`（经 `TryRecord` 的 `IsValid` 短路）。文件：`Api/ArchiveEventInput.cs`。

### BUG-ARCH-02（P2 · BASE-009 / COMP-009 空 catch 规范违例）
- **规则**：`BASE-009 日志、诊断与错误分级`（"禁止在 Tick 或每帧无界输出。外部 Provider 失败必须隔离并降级，不能使用空 catch"）；`COMP-009 兼容失败降级`（"禁止用空 catch"）；`AI-004` 禁止清单（"空 catch、静默失败"）
- **文件**：`Source/PersonalChronicle/Capture/Patch_PawnTakeDamage.cs:180-187`
- **问题**：
  ```csharp
  catch (Exception)
  {
      // Deliberately silent: Pawn.TakeDamage is one of the hottest methods...
  }
  ```
  捕获异常后完全静默，无 `Log.Warning`、无 Provider/Target/Operation 上下文。
- **缓解事实**：`Pawn.TakeDamage` 是游戏最热路径之一，每发子弹/每帧火焰触发，记录日志会刷屏反成性能问题；助攻数据装饰性，丢一帧优于打断原版管线。工程理由充分，但形式上违反强制规范。
- **影响**：若 `NoteDamage` 出现持续性异常（如 API 漂移），将静默失败且无法从 Player.log 定位，违背可观测性。
- **严重度**：Medium（P2：形式违例，有合理工程理由）
- **✅ 已修复（2026-08-12）**：空 catch 改为**限频告警**——新增 `AssistWarningCooldownTicks=6000`（约 100s）与 `_lastAssistWarningTick` 字段，catch 内带 `SourceId=PersonalChronicle.Capture`、能力 `PawnTakeDamage.Assist` 的 `Log.Warning`，冷却期内不再重复输出，兼顾可观测性与热路径不刷屏。文件：`Capture/Patch_PawnTakeDamage.cs`。

### BUG-ARCH-03（P3 · BASE-010 / GOV-006 硬编码治理）
- **规则**：`BASE-010 硬编码治理`（"禁止散落：DefName、Translation Key、路径…允许的常量必须有命名、有来源、有作用域"）；`GOV-006`（"所有硬编码必须有分类、来源和生命周期；魔法数字/字符串不得散落"）
- **文件**：
  - `Source/PersonalChronicle/Domain/HealthValuation.cs`：约 15+ 处内联 `"PersonalChronicle.UI.HealthValuation.Factor.*"` / `"PersonalChronicle.UI.HealthValuation.EventTag.*"` 字符串字面量（`:216,235,246,271,276,282,291,297,305,311,316,397,398,408,413`）
  - `Source/PersonalChronicle/Domain/ChronicleEventParams.cs:51`：`UnknownKillerLabel = "PersonalChronicle.UI.UnknownKiller"`
- **问题**：Translation Key 以字符串字面量**散落在 Domain 层**，未集中到常量类。虽然**不违反 LOC-006**（Domain 层未执行 `.Translate()`，key 仅作引用由 UI 在渲染期解析，注释已明确此设计意图），但按 BASE-010 应集中管理。
- **影响**：Key 重构/拼写错误时无单点编译期保障；第三方扫描 Translation Key 困难。纯质量项，无运行期影响。
- **严重度**：Low（P3：规范治理项）
- **✅ 已修复（2026-08-12）**：新建 `Domain/HealthValuationKeys.cs` 常量类集中全部 HealthValuation Key（Title/NoData/Stat 格/Verdict*/Dim*/Factor.*/EventTag.*/EventPrefix）；`Domain/HealthValuation.cs` 15+ 处字面量全部改引常量；UI 层 `ArchiveUiDataProvider.cs`（Verdict*、EventPrefix、UnknownKiller→`ChronicleEventParams.UnknownKillerLabel`）与 `ArchiveMainTabWindow.cs`（Title/NoData/Stat/Dim/Tip/NoEvents/Verdict）同步收敛。文件：`Domain/HealthValuationKeys.cs`（新增）、`Domain/HealthValuation.cs`、`Archive/ReadModels/ArchiveUiDataProvider.cs`、`Archive/ArchiveMainTabWindow.cs`。

---

## 已核查并确认合规的维度（v3.0 留痕）

| v3.0 规则 | 判定 | 核查证据 |
|---|---|---|
| MATRIX-002 Core 纯净边界（第三方/UI） | ✅ | 全仓无第三方 mod 命名空间引用；Domain 无 `using PersonalChronicle.Archive` / `Widgets` / `GUI` / `WindowStack` |
| MATRIX-010 循环依赖 | ✅ | Domain 无 Archive/UI 引用，Application/Archive 单向向下 |
| ARC-008 静态可变状态 | ✅ | 唯一 static 可变容器 `DamageLedger` 为 `readonly` 且有 `Reset()`，`ChronicleGameComponent.cs:198`（FinalizeInit）调用清空，防串档 |
| ARC-005 Contract 边界 | ✅ | `IArchiveUiDataProvider.BuildSection` 返回 `ArchiveSectionSnapshot`（纯数据），无 `Rect/GUIStyle/Texture/绘制回调` |
| COMP-005 Harmony 风险 | ✅ | 3 个 Prefix 均只读（`void` 或 `out __state`，从不 `return false`）；Postfix 为主；Patch 档案含 Target/Reason/Alternative/失败行为 |
| COMP-008 / PERF-004 反射时机 | ✅ | 3 处 `AccessTools.Method` 均在 `[StaticConstructorOnStartup]` 静态构造期一次性探测并缓存为 `readonly bool TargetMethodExists`（`Patch_GenRecipe.cs:66,121-128`） |
| PERF-001/004 热路径 | ✅ | GameComponent Tick 受采样/对账节流；Capture 9 Patch 无每 tick 分配；窗口 Draw 无 LINQ/OrderBy（排序已在 ArchiveUiDataProvider 缓存期完成） |
| DATA-011 / LOC-006 本地化隔离 | ✅ | Domain 层 `HealthValuation.cs` 内 0 处 `.Translate()` 调用；翻译仅在 UI/Read Model 解析；Save 不存翻译文本 |
| UI-003 / ASSET-002 | ✅ | 窗口经 `ArchiveUiDataProvider` 消费快照；`GUI.color`/`Text.Font`/`Text.Anchor` 经 `UIComponents.Label` 等组件层配对恢复（prev + try/finally）；窗口内 0 处 `new Color(...)` |
| UI-007 / PERF-005 大数据集降级 | ✅ | 关系网节点上限 24、时间轴内部滚动、阵营卡 MemberLines 内滚动 + 分页展示 |
| BASE-006 / COMP-003 外部 Def 只读 | ✅ | 全仓无完整覆写第三方/Vanilla Def；Patch 均最小字段（如 Patches/ 目录） |
| BASE-002 稳定 ID | ✅ | 无 label/LabelCap 逻辑判断；`IsMentalHediff` 用 defName Ordinal 匹配（稳定 ID，合规） |
| ARC-009 事件边界 | ✅ | `TryRecord` 含会话去重（DedupKey）+ Def 存在性校验 + Tick 校验；`ChronicleGameComponent` 有 `ResetRaidLordLinks`/`Reset` 防串档 |
| SAVE-001~008 存档/迁移 | ✅ | `SchemaVersion` 0→5 迁移全部幂等（含 v4.14 地点收敛）；`[Unsaved]` 缓存均可重建（`ResetBattleRaidCounters`/`RelinkOngoingBattles`）；legacy 降级镜像 `BuildLegacyPawnMirror`；读档 `cameFromLoad` 信号正确 |
| DATA-002/005 Def 引用一致 | ✅ | `ChronicleEventType` ↔ `Chronicle_Events.xml` defName 全匹配（且有 `ValidateEventTypeKeys` 启动校验）；`HealthValuation.xml` penalty 的 `hediffDefName` 均为引擎已知 Def |
| DATA-003 DefModExtension 选型 | ✅ | `IncidentBattleExtension` 用 `GetModExtension` 数据驱动判定战役，无 defName 硬比较（P1 红线遵守） |
| ARC-006 Provider Registry | ✅ | `ArchiveProviderRegistry` 实例化（非全局静态）、按 Priority 降序 + ProviderId 升序、异常隔离去重、`GetByCapability` 能力过滤 |
| COMP-005 剩余 Patch | ✅ | `Patch_PawnKill`/`Patch_Relations`/`Patch_IncidentWorker`/`Patch_ThingDestroy` 均 Postfix 只读、带 `[HarmonyAfter]`（IsekaiLeveling）、签名探测缓存、catch 带日志 |
| BASE-011 静态质量 | ✅ | 全仓 0 处 `TODO/FIXME/HACK/XXX`；0 处空 catch（除已修复的 BUG-ARCH-02）；编译 0 错 0 警 |
| LOC-005 动态 key 族完整 | ✅ | `Ev*`（7 个）、`Tab.*`（20+）、`IntensityTier.*`/`IntensityTag.*`（各 5）、`Event.*`（6）、`HealthValuation.Penalty.*`（7）均中英对称存在 |

---

## Gate 快照（v3.0 GATE 对照）

| Gate | 判定 | 说明 |
|---|---|---|
| GATE-ARCH | PASS | 无 P0/P1；BUG-ARCH-03（BASE-010 治理项）已修复 |
| GATE-DATA | PASS（修复后） | BUG-ARCH-01 Parameters 容量/长度校验已落地 |
| GATE-LOC | PASS（修复后） | BUG-ARCH-04 `KpiEvents` 翻译键已补齐中英条目；Key 命名空间唯一、fallback 完整；Key 已集中到 HealthValuationKeys |
| GATE-UI | PASS | Theme Token 集中、状态配对恢复、Empty/Unavailable 处理完整 |
| GATE-COMP | PASS（修复后） | BUG-ARCH-02 空 catch 已改为限频结构化告警 |
| GATE-SAVE | PASS | 缓存可从存档重建、Migration 幂等、不存 UI/翻译文本 |
| GATE-TEST | PASS（静态） | `dotnet build -c Release` 0 错 0 警（修复后实测）；运行期矩阵未重跑 |
| GATE-PERF | PASS | 热路径无违规；限频告警不破坏热路径预算；DamageLedger 有容量阈值+节流 |
| GATE-REL | PASS | v1.1.3 版本/依赖/说明已在 About.xml + README 对齐 |

---

## 基础层专向检查（BASE-001~012，2026-08-12 追加）

> 依据 `01-基础层规范.md` 逐条扫描。本专向检查**仅排查，未修复**。

### 专向检查结果总览

| 规则 | 结论 | 说明 |
|---|---|---|
| BASE-001 命名空间与唯一前缀 | ✅ | 全部 27 个 DefName 带 `PersonalChronicle` 前缀；0 个裸 DefName；C# Namespace 统一 `PersonalChronicle.*`；Translation Key 前缀 `PersonalChronicle.` |
| BASE-002 稳定 ID 与显示文本分离 | ✅ | 全仓 0 处 `.label ==`/`.LabelCap ==` 逻辑判断；Domain 层 0 处 `.label`/`.LabelCap` 访问 |
| BASE-003 用户可见文本必须本地化 | ✅ | 0 处 `Widgets.Label(rect, "中/英裸文本")`；所有 UI 文本走 `.Translate()` |
| BASE-004 Def 唯一性与身份稳定 | ✅ | DefName 唯一且带前缀；无重命名/删除风险（`ValidateEventTypeKeys` 启动校验 drift） |
| BASE-005 文件/类单一职责 | 🔴 **见 BUG-BASE-01** | `ArchiveMainTabWindow.cs` 7686 行 / 209 方法 / 185 字段（god class） |
| BASE-006 外部对象默认只读 | ✅ | Patches 仅 `PatchOperationAdd` 最小注入（RaidEnemy/Infestation），XPath 精确到 defName |
| BASE-007 资源命名空间 | ✅ | `Textures/UI/ArchiveIcon`（About 图标约定）；`modIconPath=UI/ArchiveIcon` |
| BASE-008 职责化命名 | ✅ | 0 个 `Manager`/`Helper`/`Utility` 类；后缀符合 TERM 命名语义 |
| BASE-009 日志/诊断/分级 | ⚠️ **见 BUG-BASE-02** | 日志缺 `[Warning]/[Error]` 分类标签；`ArchiveService.cs` 部分日志缺对象上下文 |
| BASE-010 硬编码治理 | ✅ | 窗口内 0 处 `new Color`；常量均带命名；业务标识/翻译 Key 已集中（BUG-ARCH-03/05 修复后） |
| BASE-011 编码与静态质量 | ✅ | 15 个 XML 全部可解析；编译 0 错 0 警；全仓 0 处 TODO/FIXME |
| TEST-001 最小测试矩阵 | ✅（静态） | `Tests/` NUnit 工程存在（覆盖 WorkIntensityEvaluator/SocialRelationFilter/ChronicleEventImportance）；运行期矩阵未重跑 |
| BASE-012 基础层验收 | ⚠️ | 见 BUG-BASE-01/02；其余适用项通过 |

### 专向检查新增 BUG

### BUG-BASE-01（P1 · BASE-005 巨型 God Class）
- **规则**：`BASE-005 文件、类、模块单一职责`（"禁止长期维护无界文件…不能同时承载业务规则、UI、Save、Localization、Harmony 和第三方兼容"）；`TERM-004` 命名语义（"禁止无界类"）
- **文件**：`Source/PersonalChronicle/Archive/ArchiveMainTabWindow.cs`
- **问题**：**7686 行 / 209 个方法 / 185 个字段**，远超单文件健康阈值（约 ≤1000 行）。职责虽已收口为"纯 UI 绘制"（0 处 Harmony/Scribe/ExposeData/Settings 逻辑，0 处 IArchiveService 直读——Read Model 收口成功），但作为单一 UI 窗口仍承载全部视图（Home/Overview/Detail/Weapon/Location/Battle/FactionCodex/Social/HealthValuation/Timeline 等 10+ 区块绘制）。
- **影响**：维护成本高、并行开发冲突、LSP/编译慢、单点风险。无当前运行期缺陷。
- **严重度**：High（P1：违反 BASE-005 强制规范，但属工程治理非运行期 bug）
- **关联**：`docs/README.md` "仍打开的工作项 LE-1：ArchiveMainTabWindow god class 拆分（架构级，独立排期）"。
- **建议修复方向（未执行）**：按 `UI-002`（Window/Panel/Component 职责）拆分——`ArchiveWindow` 壳 + 各 `*Panel` 绘制类（HomePanel/OverviewPanel/DetailPanel/FactionCodexPanel 等），常量与绘制方法随区块迁移。独立排期，非本次。

### BUG-BASE-02（P3 · BASE-009 日志格式不统一）
- **规则**：`BASE-009 日志、诊断与错误分级`（"统一日志前缀和类别：`[MyMod][Info]`/`[MyMod][Warning]`/`[MyMod][Error]`…"）
- **文件**：全仓 36 处 `Log.Warning/Error/Message("PersonalChronicle: ...")`（Capture 12 个 Patch、ArchiveService 10+ 处、ChronicleGameComponent 3 处等）
- **问题**：日志均带 `PersonalChronicle:` 前缀但**缺 `[Warning]/[Error]/[Compatibility]/[Save]` 分类标签**，未严格符合规范建议的 `[MyMod][Warning]` 格式。部分日志（如 `Patch_Relations`/`Patch_GenRecipe` 的 `"...patch failed"`）有对象上下文 ✅，但无分类标签；`ChronicleGameComponent` 3 处 `Log.Message` 属迁移统计（Info 级合理）也缺标签。
- **影响**：Player.log 过滤/分类诊断略不便。纯格式规范项，无功能影响。
- **严重度**：Low（P3：格式规范项）
- **建议修复方向（未执行）**：统一为 `Log.Warning("PersonalChronicle[Warning][Capture]: ...")` 等带分类标签格式；或集中一个 `ChronicleLog` 静态包装类（含 `[Mod][Category]` 前缀 + 可选上下文），全仓替换。属批量机械替换，独立排期。

### 专向检查合规亮点（留痕）
- 日志**无界输出防线完整**：`Patch_PawnTakeDamage` 限频告警（BUG-ARCH-02 修复后）、`ITab_Pawn_Chronicle` 用 `Log.WarningOnce`、Provider 失败带 ProviderId 去重（`ArchiveService.cs:804`）。
- 缓存无界防线：`maxVisits=32`（PlaceHistory）、`MaxEventsPerPawn`（设置项）、`EventsPerPawnSliderMin/Max=10/1000`（滑条界）。
- XML 全部可解析（15/15）、DefName 全部唯一带前缀、无裸 UI 文本、无显示文本逻辑判断、无 Manager/Helper/Utility 类。

---

## 备注

- 本清单初版为**静态审查**结果；2026-08-12 已按用户要求**落地修复**全部 3 项（BUG-ARCH-01/02/03），`dotnet build -c Release` 0 错 0 警，`Assemblies/PersonalChronicle.dll` 已同步发布目录。
- **第二次检查（同日）**：通过翻译键双向完整性审计脚本发现 BUG-ARCH-04（`KpiEvents` 缺失，全项目唯一缺失完整键）与 BUG-ARCH-05（业务标识字符串散落），**已按用户要求落地修复**（编译 0 错 0 警，dll + 中英翻译文件已同步发布目录）。
- 修复未改变公共 API 契约语义、Save schema 与版本（仍为 v1.1.3 口径），不触发 Breaking。
- 运行期（Player.log / 实际存档往返）验证尚未执行；按 v3.0 `AI-009` 诚实标注"未执行"，需进游戏实测后补充 Evidence。运行期真实缺陷已记录于 `性能技术检测报告.md`（HealthValuation.xml 越界）与 `开发阻碍BUG集_与CharacterEditor共存冲突排查与修复.md`（CE 共存），不在本清单重复。
