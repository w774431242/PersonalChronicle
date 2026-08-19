# Gate Record — 2026-08-19 职业生涯体系验收（含修复记录）

> **恢复说明**：本文件于 2026-08-19 发现丢失（工作区文件被移除），依据验收会话记录恢复最终版（含 P1~P3 修复记录、差异化特化点与数据模拟工具追加记录）。

- **Change:** 职业生涯体系（P1~P8）对照 v3.0 规范体系全量检查与验收；2026-08-19 第二轮：按首轮发现项完成代码/文档修复并复验
- **Version:** v1.1.4+（Save Schema 6）
- **Date:** 2026-08-19
- **Reviewer:** AI（静态审计；编译/单测见说明）
- **Scope:** 职业生涯体系相关 Source（Domain/Profession、Qualification、Honor、Career、Application/Effects、Capture/Effects、Data、Archive/ITab_Pawn_Career.*）+ Defs（ProfessionalSkills/QualificationDefs/MedalDefs）+ Languages + Tests + 设计文档（职业档案/勋章体系）
- **依据:** 00-规范体系总纲、00-统一验收标准、00-工程生命周期规范、00-需求与SDD规范、01~07 分层规范、00-规则编号索引、00-规范例外与变更管理

## 0. 验收方法说明

- 本机无 .NET SDK（dotnet 仅 runtime，MSBuild/vswhere 均不可用），**无法复跑编译与单测**。
- 编译/测试证据引用 2026-08-16 全项目架构审计记录（Gate-Record-2026-08-16-架构审计.md：编译 0 错 0 警；单测 153 过 / 3 跳 / 0 失败）。
- 首轮以静态审计 + 文档一致性核对为主；**第二轮完成修复后，修改过的代码同样未能复跑编译（环境限制），建议在具备 .NET SDK / RimWorld 的工具链环境复验后再发布**。
- 中英翻译键一致性采用脚本实测（899 = 899，职业相关 318 键一致，修复未触碰翻译键）。

## 1. Gate 结果（第二轮复验后）

| Gate | 首轮 | 复验后 | 规则 ID | 结论 |
|---|---|---|---|---|
| REQ（需求） | BLOCKED | **BLOCKED（待补登记）** | REQ-001/002/004 | 需求有 Change Intent 等效物，但无 REQ 条目编号与追踪矩阵（存量文档早于新规范；建议补登记或豁免，见 §4） |
| SDD（设计文档） | PASS WITH EXCEPTION | **PASS** | SDD-001/002/003/004/008 | P2-A/P3-A/P4-A 状态行已同步实现阶段；P4 Q3/Q4 裁决已闭环；Schema 口径统一为 6 |
| ARCH | PASS | PASS | ARC-001/002/005/013, MATRIX-001 | Domain 已彻底去 RimWorld 依赖（P2-5 修复）；T2 Api 契约倒挂仍为遗留 |
| DATA | PASS | PASS | DATA-001/005/006/007/010 | 修复均为 append-only 字段追加（Finished / QualificationDefName），无 schema 变更 |
| LOC | PASS | PASS | BASE-003, LOC-001/002/006 | 中英键 899=899 一致；未新增任何硬编码文本 |
| UI | PASS | PASS | UI-001/003/007/008, PERF-002 | 未触碰 UI 代码（DevTestButtons 仅补字段赋值） |
| COMP | PASS | PASS | COMP-001/004/008/009, GOV-007, EXC-2026-001 | 18 处 [HarmonyPatch] 全部在 EXC-2026-001 登记；未新增 Patch |
| SAVE | PASS | PASS | SAVE-001~004 | 新增字段带默认值且旧档兼容（Finished=false / QualificationDefName=null） |
| TEST | PASS WITH EXCEPTION | **PASS WITH EXCEPTION** | TEST-001 | 新增 8 个回归用例（品质枚举/硬门槛/超时/答辩匹配，见 §5）；仍无法本机复跑 |
| PERF | PASS | PASS | PERF-001/004 | 修复未引入热路径开销（RecordExamProduced 仅在制造产出时执行） |
| REL | PASS | PASS | REL-006 | 无发布区变更；git 未提交见 §3-16 |

## 2. 符合项证据（PASS 要点，首轮结论不变）

| # | 检查项 | 证据 |
|---|---|---|
| 1 | CareerEvent 事实层铁律（事实≠评价） | `Domain/Career/CareerEvent.cs`：白名单 9 种 = P1 的 5 种 + P5~P8 扩展 4 种，DISCLOSE 决策已落地 |
| 2 | D2 字段追加（RecipeDefName/Quantity） | `CareerEvent.cs:47-52`；ExposeData 默认值 null/1 兼容旧档 |
| 3 | P2 状态层与事实层分离 | `ArchiveService.Career.cs` ApplyProfessionalProgress 只写 CareerData.Professional |
| 4 | 效果三层链路（Effect→Resolver→Adapter） | `ProfessionalEffectResolver` → `ProfessionalEffectService` → `StatPart_ProfessionalWorkSpeed` + `TryApplyQualityBias`；零新增 Patch |
| 5 | 评级软上限/递减（V2.0 §13 红线） | maxLevel=50 + 0.4 幂递减曲线 + 4 档评级封顶（10/25/38/45） |
| 6 | P8 勋章双路径（D-M1） | `MedalDefs.xml` 27 Threshold + 4 Achievement；授勋回写 MedalGranted 闭环 |
| 7 | 资格自动授予（D-T1） | `ChronicleGameComponent.Qualification.cs` RunQualification；与勋章同 reconcile 节流 |
| 8 | 考试证据复用 P1 采集点（D-E1） | `ArchiveService.Qualification.cs` RecordExamProduced 由 Patch_GenRecipe 链路调用 |
| 9 | 本地化 | 实测中英 899=899；职业相关 318 键一致 |
| 10 | Harmony 治理 | 18 处 [HarmonyPatch] 与 EXC-2026-001 14 文件清单逐一对应；签名探测 + 启动强制探测 |
| 11 | Save 安全 | CareerData 全部容器 Scribe 幂等 + null 重建 |
| 12 | UI 只读 Read Model | `ITab_Pawn_Career.cs:8` 明确"绝不自行查询+排序"；DetailSnapshot + BuiltFromRevision |
| 13 | 测试覆盖 | 7 个专项测试文件约 98 用例（含本轮新增 8 个回归） |

## 3. 发现项清单（含修复状态）

### P1 级（影响正确性）

| # | 问题 | 状态 | 修复证据 |
|---|---|---|---|
| P1-1 | ExamScoring.QualityRank 品质枚举与 RimWorld 1.6 不符（Superior 不存在 / 缺 Masterwork） | ✅ **FIXED** | `ExamScoring.cs` QualityRank 改为 Excellent=4/Masterwork=5/Legendary=6，删 Superior；新增 `CountAtLeast` 纯函数；回归用例 `QualityRank_Masterwork_RanksAboveExcellent` |
| P1-2 | 实践考试"最低品质"未落地为硬门槛（Score>0 即通过） | ✅ **FIXED** | `ArchiveService.Qualification.cs` RecordExamProduced：`CountAtLeast(qualities, MinQuality) >= RequiredCount` 为通过前置条件；数量够但品质不足时考试继续；回归用例 `RecordExamProduced_QualityBelowMinimum_NotPassedAndContinues` |
| P1-3 | 实践考试超时路径卡死（超时后永不评分） | ✅ **FIXED** | `ExamData.cs` PracticalExamRecord 新增 `Finished` 字段（append-only，默认 false）；超时即按当前证据评分（×0.6）并置 Finished；回归用例 `RecordExamProduced_Overtime_*` 两条 |
| P1-4 | 答辩判定字段错配（ThesisId == defName） | ✅ **FIXED** | `ThesisData.cs` DefenseRecord 新增 `QualificationDefName` 字段（append-only）；`QualificationEvaluator` DefensePassed/DefenseScore 优先按 QualificationDefName 精确匹配，旧记录（字段空）回退 ThesisId 兼容早期 DevTest 数据；`DevTestButtons.cs` 同步补字段；回归用例 2 条 |
| P1-5 | P6 论文/答辩正式入口缺失（Senior+ 职称正式游戏不可授予） | ✅ **已裁决→已恢复（流程修补）** | 2026-08-19 首裁方案 A（临时关闭门槛）→ 同日**否决并恢复**（反馈：符合资格后未经过考试论文答辩就被授予=流程错误）：`QualificationDefs.xml` 全档 `requiredExam=true`（Junior/Assistant 补考试），Senior/Specialist/Master 恢复 `requiredThesis/requiredDefense=true`；生效前提=P9-B 论文/答辩入口落地（P9 预览已确认流程） |

### P2 级

| # | 问题 | 状态 | 修复证据 |
|---|---|---|---|
| P2-1 | levelScore 语义混淆（maxLevelOrFifty 实返 requiredMinLevel） | ✅ **FIXED** | `QualificationEvaluator.cs`：运行时入口解析技能 maxLevel → skillMaxLevels 映射；注入版可选参数、缺省 50 兜底；删除 `maxLevelOrFifty` 扩展类 |
| P2-2 | P4 叠加/替代决策未闭环 | ✅ **FIXED** | `制造类职业评级设计.md` §7 记录 Q1~Q4 已裁决（叠加模式）、§3.2 数值冻结表（0.03/0.05/0.08/0.10 速度，0/0.02/0.04/0.06 品质，阈值 10/25/38/45） |
| P2-3 | P2-A/P3-A/P4-A 文档状态行滞后 | ✅ **FIXED** | 三份文档状态行更新为"2026-08-16 起进入 X-B 实现"，并挂接本验收记录 |
| P2-4 | P3-A/P4-A 引擎基线写 Schema 5（实际 6） | ✅ **FIXED** | P2-A/P3-A/P4-A 引擎基线统一为 Save Schema Version 6 |
| P2-5 | Domain 纯净边界轻微突破（using RimWorld 纯枚举） | ✅ **FIXED** | `ProfessionalEffectResolver.ClampQuality` 签名改 `int` 索引，文件删 `using RimWorld`；适配层做枚举转换；测试 4 个 ClampQuality 用例同步改 int 断言 |
| P2-6 | ChronicleGameComponent.Qualification.cs 缩进错乱 | ✅ **FIXED** | 整文件缩进规范化重写（内容零变更） |

### P3 级

| # | 问题 | 状态 | 修复证据 |
|---|---|---|---|
| P3-1 | StatPart 空白名单 = 全 RecipeDef 注入风险 | ✅ **FIXED（防御性）** | `ProfessionalEffectRegistry.InjectWorkSpeedStatParts` 对空白名单技能输出 ChronicleLog.Warning 告警 |
| P3-2 | Def reload 后 StatPart 不重注入 | ✅ **FIXED** | `ProfessionalEffectRegistry` 重构：`CollectTargetStats` 抽离 + `EnsureInjected()` 幂等重注入（按 parts 实例检测，reload 后补注入）；`ChronicleGameComponent.Sampling` reconcile 节流挂接（600 tick） |
| P3-3 | ProfessionalEffectDef 用基类 description 存翻译键 | ✅ **FIXED** | 新增 `labelKey` 字段；XML 两个效果 Def 的 `<description>` 迁移为 `<labelKey>` |
| P3-4 | XP 品质系数多 Good=1.2 档未同步文档 | ✅ **FIXED** | `制造类职业领域设计.md` §5 品质系数表补"良好 1.2" |
| P3-5 | 08-16 遗留 T2：Api 契约倒挂 | ⏳ **遗留** | 独立迭代经 REL-008 流程 |

### 工程卫生

| # | 问题 | 状态 | 说明 |
|---|---|---|---|
| P3-6 | git 未提交（职业生涯体系 + 审计成果 + 验收全部未入库） | ⏳ **待提交** | 建议修复复验通过后一次性提交 |

## 4. 需求 ↔ SDD 对齐检查（按 00-需求与SDD规范）

| 检查项 | 首轮 | 复验后 | 说明 |
|---|---|---|---|
| Change Intent 成文（REQ-001/002） | ✅ 等效成立 | ✅ | V2.0 指南 + P2-A~P8-A + 决策记录 D1~D5 覆盖要素 |
| 需求条目编号 REQ-<域>-<序号>（REQ-004） | ❌ 缺失 | ❌ 待补 | 建议补 Change Intent + 追踪矩阵（REQ-Career-*），或登记存量豁免 EXC |
| 追踪矩阵（双向覆盖） | ❌ 缺失 | ❌ 待补 | 同上 |
| 文档状态机（SDD-004） | ⚠️ 部分 | ✅ | 状态行已同步实现阶段（P2-3） |
| 变更记录（SDD-009） | ✅ | ✅ | 各文档含变更记录；修复已写入文档与代码注释 |

## 5. 第二轮修复与回归测试记录

### 修复清单（代码）

| 文件 | 修复 |
|---|---|
| `Domain/Qualification/ExamScoring.cs` | QualityRank 枚举修正（P1-1）+ 新增 CountAtLeast（P1-2 硬门槛辅助） |
| `Domain/Qualification/ExamData.cs` | PracticalExamRecord 新增 Finished 字段 + Scribe（P1-3） |
| `Application/ArchiveService.Qualification.cs` | RecordExamProduced 重写：品质硬门槛 + 超时结束（P1-2/P1-3） |
| `Domain/Qualification/ThesisData.cs` | DefenseRecord 新增 QualificationDefName + Scribe（P1-4） |
| `Domain/Qualification/QualificationEvaluator.cs` | DefensePassed/DefenseScore 按 QualificationDefName 匹配 + 旧数据回退（P1-4）；levelScore 按技能 maxLevel 归一 + 删 maxLevelOrFifty（P2-1）；前置职称双键匹配（模拟工具暴露，2026-08-19 追加） |
| `Domain/Profession/ProfessionalEffectResolver.cs` | ClampQuality 改 int 签名、删 using RimWorld（P2-5） |
| `Capture/Effects/ProfessionalEffectRegistry.cs` | 空白名单注入告警（P3-1） |
| `Domain/Profession/ProfessionalEffectDef.cs` | 新增 labelKey 字段（P3-3） |
| `Data/ChronicleGameComponent.Qualification.cs` | 缩进规范化（P2-6） |
| `Capture/Patch_GenRecipe.cs` | TryApplyQualityBias 枚举转换适配（P2-5） |
| `Archive/DevTestButtons.cs` | DefenseRecord 补 QualificationDefName（P1-4 同步） |
| `Defs/ProfessionalSkills.xml` | 效果 Def description → labelKey（P3-3） |

### 回归测试（Tests/CareerQualificationTests.cs 新增 8+2 用例）

| 用例 | 覆盖 |
|---|---|
| QualityRank_Masterwork_RanksAboveExcellent | P1-1 枚举修正回归 |
| CountAtLeast_CountsOnlyQualified | P1-2 硬门槛辅助函数 |
| RecordExamProduced_QualityBelowMinimum_NotPassedAndContinues | P1-2 品质不足不通过且考试继续 |
| RecordExamProduced_AllQualified_PassesAndFinishes | P1-2/P1-3 正常通过路径 |
| RecordExamProduced_Overtime_EndsExamWithPenalty | P1-3 超时结束 + ×0.6 罚分（60 分） |
| RecordExamProduced_Overtime_QualityShortfall_FailsAndEnds | P1-3 超时 + 品质不足 → 结束且不过（30 分） |
| Qualification_Defense_MatchByQualificationDefName | P1-4 新字段精确匹配 |
| Qualification_Defense_LegacyRecordFallsBackToThesisId | P1-4 旧数据回退兼容 |
| Qualification_PreviousTitle_MatchByQualificationDefName | 前置职称双键匹配（资格 defName 匹配） |
| Qualification_PreviousTitle_Unmet_NotEligible | 前置职称未满足阻断 |

> 同步修改：`Tests/ProfessionalEffectResolverTests.cs` 4 个 ClampQuality 用例改 int 断言（P2-5 签名变更）。所有测试**未在本机复跑**（无 .NET SDK），语法与既有用例风格一致，建议工具链环境全量复验。

### 文档同步

| 文档 | 同步内容 |
|---|---|
| `制造类职业评级设计.md` | §3.2 数值冻结表 + §7 Q1~Q4 裁决记录 + 状态行 + Schema 6 |
| `制造类职业领域设计.md` | 状态行 + Schema 6 + §5 品质表补 Good=1.2 + §7.1 差异化特化点 |
| `制造类职业效果设计.md` | 状态行 + Schema 6 |
| `制造类职业资格与荣誉体系设计.md` | DefenseRecord 结构补 QualificationDefName；§2.3 补硬门槛与超时语义 |

## 6. 结论与剩余事项

**结论**：首轮 5 项 P1 级问题中 **4 项已修复**（P1-1~P1-4，含回归测试），P1-5 待 PM 裁决；6 项 P2 级全部修复；P3 级修复 3 项、2 项登记为已知限制/遗留。**P1~P4 垂直切片（精密制造闭环）可验收；P5~P8 链路在 P1-5 裁决与修复复验通过后满足闭环验收条件。**

### 后续新增特性（2026-08-19 第二轮后）

**二级方向差异化特化点**（回应"方向数据加成雷同"反馈）：

- 诊断：效果数值全局共享（`ProfessionalEffectDef.value`）、评级权重全局统一（`ProfessionalRatingDef`）、方向 Def 无数据字段 → 结构性雷同。
- 机制（C#，append-only Def 字段）：`ProfessionalSkillDef.effectOverrides`（技能级数值覆盖 + 评级权重缩放）、`ProfessionalDirectionDef.specializationKey/specializationDescKey/labelKey`（方向特化语义）、Resolver 覆盖解析。
- 数据：4 方向 Def 与 `Profession.Direction.*` 中英 8 键已落地（907=907 一致）；武器/装备/工业三方向技能与配方随各自垂直切片落地（V2.0 §22）。
- 测试：`ProfessionalEffectResolverTests` 新增 5 例（覆盖生效/评级缩放/禁用评级/跨效果隔离/品质覆盖）。
- 文档：P2-A §7.1 特化定位与 4 方向数据蓝图（数值 D5 未冻结）。

**数据模拟工具（REQ-Tools-001）及其暴露的缺陷修复**：

- 工具：`Tools/DataSimulator`（Node.js 零依赖，离线按架构管线模拟：行为→事实→XP→能力→评级→效果→资格→授予→报告）。SDD 见 `docs/设计文档/功能模块/数据模拟工具/数据模拟工具需求与设计.md`。
- 金样自测 29 项断言通过（公式对齐 C# 转写表）；4 预置场景全部可跑（`--selftest` / `node run.js`）；交互式 HTML 报告（`--open` / `--doc` 同步文档目录）。
- **暴露并修复 P2 级缺陷（前置职称链）**：`QualificationDef.requiredPreviousTitle` 存资格 defName，而 `GrantedTitles` 记录职称 defName——原 Evaluator 只比 TitleDefName，第二档起职称链永远不满足（模拟器运行暴露，C# 双键匹配修复，含 2 个回归用例）。
- **开发者调试 UI（REQ-Tools-002）**：预览 HTML「📊 数据初始化 / 🧪 数据模拟」接入真实管线——221 种原版可制作物品 + 16 配方 + 建造/研究/著书 + 评价模式开关 + 勋章判定 + 履历分段；`?simtest=1` 无头回归全通。
- 模拟验证输出示例（direction-compare，400 次同条件）：Precision x1.0330/+1 品质、Weaponry x1.0575/无品质、Equipment x1.0216、Industrial x1.0448——差异化特化点数据生效。

剩余事项（按优先级）：

```text
1. ✅ PM 裁决 P1-5（2026-08-19 批准方案 A：临时关闭 Senior+ 论文/答辩门槛，P9 恢复）
2. ✅ .NET SDK 复验完成：编译 0 错 0 警；单测 168 过 / 3 跳 / 0 失败；DLL 已重建提交
3. ✅ 职业生涯体系 Change Intent + 追踪矩阵已补（docs/设计文档/功能模块/职业档案/职业生涯体系-需求追踪矩阵.md，REQ-Career-001~011）
4. ✅ git 已提交（6 个逻辑提交，2026-08-19，含重建 DLL）
5. 可选后续：P9 UI（REQ-Career-011，论文/答辩正式入口落地后恢复门槛）
```

## 7. 全部落地记录（2026-08-19 PM 批准"全部落地 + 按推荐方案修复缺口"）

| 项 | 执行 |
|---|---|
| P1-5 裁决 | ✅ 方案 A（关闭 Senior+ thesis/defense 门槛，Defs 注释登记） |
| P3-2 | ✅ reload 幂等重注入（EnsureInjected + reconcile 挂接） |
| P3-5 T2 | ✅ 复核：接口已定义于 `PersonalChronicle.Api` 命名空间，无倒挂（08-16 遗留已随 T1 治理解决） |
| 追踪矩阵 | ✅ `职业生涯体系-需求追踪矩阵.md`（REQ-Career-001~011 存量补登记） |
| 文件丢失 | ✅ EXC-2026-001 / Gate-Record 08-16 / 08-19 恢复；08-15 占位（内容未留存） |
| git 提交 | ✅ 5 个逻辑提交（规范体系 / 职业体系实现 / 档案工作区 / 工具与预览 / 杂项文档）；DLL 待重建 |
| SDK 复验 | ✅ 完成（2026-08-19）：dotnet-install 安装 SDK 8.0.424 → 主项目编译 **0 错 0 警** → 单测 **168 过 / 3 跳 / 0 失败**（含本轮 15 个新用例全部通过）→ DLL 已重建并提交 |
