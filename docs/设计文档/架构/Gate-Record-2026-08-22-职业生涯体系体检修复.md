# Gate Record — 2026-08-22 职业生涯体系体检与修复

> **依据**：`02-架构层-核心架构规范` + `Gate-Record-2026-08-19-职业生涯体系验收`（基线）+ `EXC-2026-001`
> **范围**：职业生涯体系全链路静态体检（FAIL/WARN 闭环修复）
> **Date**：2026-08-22
> **Reviewer**：AI（静态审计；本机无 .NET SDK，编译/单测引用 08-19 复验证据）

## 0. 体检来源

基于架构文档，对职业生涯体系做全量静态体检，发现 **2 FAIL + 3 WARN**（详见会话体检报告）。本记录为修复落地记录。

## 1. 体检结果 → 修复状态

| # | 级别 | 规则 | 问题 | 状态 | 修复证据 |
|---|---|---|---|---|---|
| 1 | FAIL | UI-001 | 资格子页 `ITab_Pawn_Career.Qualification.cs` 绕过 Read Model 直查 Domain + 窗口内重复实现业务判定（`HighestGrantedTier`/`FlowPassed`/`HasActiveExam` 等 7 个私有方法） | ✅ FIXED | 7 个私有方法删除，上移至 `Application/ArchiveService.Qualification.cs` 的 `QualificationFlowService`（新增 9 个 public static 查询：`CalcSkillLevel`/`CalcCareerSpanTicks`/`FlowPassed`/`HasActiveExam`/`ActiveExam`/`FindTheoryRecord`/`FindThesisRecord`/`FindDefenseRecord`/`GrantedTierIndex`）；UI 全部改为 `QualificationFlowService.Xxx` 调用 |
| 2 | FAIL | EXC-2026-001 | `Capture/Patch_PawnTitleBadge.cs` 含 2 个 `[HarmonyPatch]`（`_Map`/`_Label`）未登记 | ✅ FIXED | EXC-2026-001 清单补 #15/#16；源文件类文档加 EXC 引用注释 |
| 3 | WARN | ARC-002 | `Domain/Profession/ProfessionalDirectionDef.cs:50` `GetDisplayLabel()` 调 `.Translate()`（Domain 翻译泄漏） | ✅ FIXED | 方法改为返回 `labelKey`（不翻译）；3 个 UI 调用点（`Dialog_SetPrimaryDirection.cs` 188/250/389）加 `.Translate()` |
| 4 | WARN | ARC-008 | `ProfessionalEffectRegistry` 静态字典/列表未标注受控状态 | ✅ FIXED | 类头加 ARC-008 受控单例声明注释；3 个静态字段加受控缓存标注 |
| 5 | WARN | ARC-002 | `MedalAwardService.AnnounceGold` 调 `.Translate()`（勋章文本） | ✅ FIXED（治理） | 方法加注释：翻译键源集中 `MedalTranslationKeys`（非散落硬编码），属 Application→Presentation 边界必要副作用，符合 ARC-002 例外 |

## 2. 修复后复验（静态）

| 检查项 | 结果 | 证据 |
|---|---|---|
| UI 无残留私有方法调用 | ✅ | `Select-String` 确认 UI 仅调用 `QualificationFlowService.*`，无 `HighestGrantedTier`/`FlowPassed` 等私有调用 |
| FlowService 新增段无 UI 类型 | ✅ | `Widgets./GUI./Verse.Text/TooltipHandler` 零命中 |
| 翻译键一致性 | ✅ | 50 个方向 `labelKey` 中文 Archive.xml 100% 命中（0 缺失）；`GetDisplayLabel` 返回 key 在 xml 均存在 |
| XML 良构 | ✅ | `XmlTextReader` 全量解析通过（Archive.xml 良构） |
| 编译/单测 | ⏳ 未复跑 | 本机无 .NET SDK（runtime only）；引用 08-19 复验：编译 0 错 0 警 / 单测 174 过 3 跳 0 失败。本次为纯方法迁移 + 调用点替换 + 注释，无新语法风险 |

## 3. 剩余事项（非违规，记录备查）

- UI 资格子页仍直接读 `cd` 实时明细（`cd.Qualification.Get` / `cd.GrantedTitles`）作展示数据访问——属 UI→Core 读取范畴（与 Overview 页经 `snap` 间接读 `cd` 一致），**业务判定已全部上移**。若要 100% 消除 `cd` 引用需扩展 `CareerOverviewView` 快照结构（改动面大），本次未做。
- 本机无 SDK，建议具备工具链环境复跑编译 + 单测后再发布。

## 4. 涉及文件清单

| 文件 | 改动 |
|---|---|
| `Source/PersonalChronicle/Application/ArchiveService.Qualification.cs` | `QualificationFlowService` 新增 9 个 public static 查询方法 |
| `Source/PersonalChronicle/Archive/ITab_Pawn_Career.Qualification.cs` | 删除 7 个私有业务方法；调用点改 `QualificationFlowService.Xxx` |
| `Source/PersonalChronicle/Archive/Dialog_SetPrimaryDirection.cs` | 3 处 `GetDisplayLabel()` 调用加 `.Translate()` |
| `Source/PersonalChronicle/Domain/Profession/ProfessionalDirectionDef.cs` | `GetDisplayLabel()` 返回 key 不翻译 |
| `Source/PersonalChronicle/Capture/Effects/ProfessionalEffectRegistry.cs` | ARC-008 受控单例注释 |
| `Source/PersonalChronicle/Application/MedalAwardService.cs` | `AnnounceGold` ARC-002 注释 |
| `Source/PersonalChronicle/Capture/Patch_PawnTitleBadge.cs` | EXC-2026-001 引用注释 |
| `docs/设计文档/架构/EXC-2026-001.md` | 补登 Patch #15/#16 |
| `Languages/*/Keyed/Archive.xml` | 前次 Tooltip 改动已同步发布区（本次未改翻译键） |

## 5. 结论

**职业生涯体系体检 2 FAIL + 3 WARN 全部闭环。** 业务判定逻辑唯一归属 Application 层（`QualificationFlowService`），Domain 纯净边界修复，Harmony Patch 登记完备，静态自检与翻译键一致性通过。建议工具链环境复跑编译/单测后发布。
