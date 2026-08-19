# Gate Record — 2026-08-16 全项目架构符合性审计

> **恢复说明**：本文件于 2026-08-19 发现丢失（工作区文件被移除），依据审计会话记录原样恢复。

- **Change:** 基于 docs/设计文档/架构（v3.0 规范体系，22 份文件）的全项目架构审计
- **Version:** v1.1.4+（Save Schema 6）
- **Date:** 2026-08-16
- **Reviewer:** AI（静态审计 + 编译/单测验证）
- **Scope:** Source/ 全部 6 层 + Defs + Languages + Tests + 发布区
- **Evidence:** 编译 0 错 0 警；单测 153 过/3 跳/0 失败；静态扫描脚本输出（本记录附件）

## Gate 结果

| Gate | Result | Rule IDs | 结论 |
|---|---|---|---|
| ARCH | PASS（1 项本轮修复） | ARC-001/002/005/013, MATRIX-001/002/010 | 分层单向依赖成立；Data↔Application 循环与 Api 契约倒挂为遗留技术债（见下） |
| DATA | PASS | DATA-001/005/006/007, SAVE-001~004 | 四类数据边界清晰；Schema v6 幂等迁移链完整；缓存可重建 |
| LOC | PASS（6 处本轮修复） | BASE-003, LOC-002, GOV-009 | 中英键集合完全一致；逻辑判断不再依赖显示文本 |
| UI | PASS WITH EXCEPTION | UI-003, ASSET-002, PERF-001/002 | 新代码走 UIComponents/UITheme；老 ArchiveMainTabWindow 文件散落配对式 GUI.color 为遗留债 |
| COMP | PASS | COMP-001/004, GOV-007, EXC-2026-001 | 18 处 [HarmonyPatch] 全部在 EXC-2026-001 登记（14 文件清单匹配） |
| SAVE | PASS | SAVE-001~004 | Schema 6；迁移幂等；无 UI/Texture/翻译写入 Save |
| TEST | PASS | TEST-001 | 153 过 / 3 跳 / 0 失败；编译 0 错 0 警 |
| PERF | PASS（1 项本轮修复） | PERF-001/004 | Location 绘制路径 LINQ 已缓存；ReadModel 聚合归位 |
| REL | PASS（1 项本轮修复） | REL-006 | 发布区孤儿 XML 已清理；无源码/临时素材 |

## 本轮修复清单

| # | 规则 | 问题 | 修复 |
|---|---|---|---|
| R1 | BASE-002/GOV-009（P1） | ArchiveUiDataProvider.Detail.cs 用已翻译关系名（"子/女/后代/嗣/child/offspring…"）判断后代 | RelationView 增加 RelationDefName 稳定键；判定改 PawnRelationDef 白名单（Child/Grandchild/Stepchild） |
| R2 | BASE-003/LOC-002（P3） | 6 处硬编码中文（制造成果文案/银单位/h·天/徽章占位/年份后缀/档位角标） | 全部改翻译键（中英各 +6 键）；档位角标经 TierLabel 首字派生 |
| R3 | ARC-013（P1） | ITab_Pawn_Career.cs 1259 行大文件（新增即违规） | 拆 5 个 partial：主 422 / Overview 370 / Resume 179 / Honor 251 / Fit 120，零契约改动 |
| R4 | REL-006（P4） | 发布区根目录孤儿 XML（Archive.xml / MedalDefs.xml） | 删除 |
| R7 | PERF-001（P2） | Location 绘制路径每帧 GetEventsFor + LINQ OrderBy | 按 stableId + revision 缓存排序结果（DATA-007 可重建） |

## 遗留技术债（建议排期，非本轮阻塞）

| # | 规则 | 现状 | 建议 |
|---|---|---|---|
| T1 | MATRIX-010（P1） | Data↔Application 双向引用：ChronicleGameComponent.Sampling/主文件 调 ArchiveService（ResetRaidLordLinks / RunQualification / 查询接口） | 下沉或经事件/契约解耦（ResetRaidLordLinks 移 Data；RunQualification 触发经组件内部调度） |
| T2 | ARC-005（P2） | Api 层接口（IArchiveQueryService 等）定义在 Application 层，Api 反向引用 | 契约接口迁往 Api/Contracts，Application 实现之 |
| T3 | BASE-004（P2） | ~~ProfessionalSkills.xml 无前缀 defName~~ | ✅ 已修复：XpPolicy_Manufacturing→ProfessionalXpPolicy_Manufacturing，Rating_*→ProfessionalRating_*（代码无硬引用、labelKey 显式、不持久化，零风险） |
| T4 | UI-003/ASSET-002（P3） | ArchiveMainTabWindow 历史文件散落配对式 GUI.color/Text.Font | ✅ 已扫描：全部配对恢复（0 未配对风险点）；风格债保留，逐步迁移 UIComponents |
| T5 | BASE-003（P3） | ~~DevTestButtons Messages 硬编码中文~~ | ✅ 已修复：9 条 Messages + 按钮文案全部改翻译键（中英各 +9 键）；测试数据参数文本（海盗/部落等）保留为测试内容 |
| T6 | REL-001（P3） | ~~Schema 口径文档过期~~ | ✅ 已同步：项目整理与功能清单.md + MEMORY.md 更新为 Schema 6 |

## 已确认合规项

- 分层：Domain 零 UI/Archive/Capture 引用；Capture 只转调 Service；Archive 消费 ReadModel
- 捕获治理：18 处 [HarmonyPatch] 全部在 EXC-2026-001 清单内（Patch_Relations/GenRecipe 等 2 处 attribute 与清单一致）
- Def 命名：除 T3 外全部带 PersonalChronicle/业务前缀
- 本地化：87 个 Career 键 + 本轮 12 键，中英集合一致；动态拼接键（Career.Major.<key>）12 专业已落地
- 存档：SchemaVersion 0→6 幂等迁移链（含 v4.14 地点收敛、勋章 defName 规范化）；无 UI/Texture/翻译写入 Save
- UI：ITab 新代码全走 UIComponents/UITheme；窗口只消费 DetailSnapshot；滚动容器 + 空态齐备
- 性能：绘制路径 LINQ 已清零（Location 缓存修复后）；Def 查找均在 ReadModel 构建期
- 发布：无源码/备份/临时脚本；Fonts OFL 授权保留；Medals 贴图已同步

## 2026-08-16 第二轮修复（T1/T3/T4/T5/T6）

| # | 修复内容 |
|---|---|
| T1 | Data↔Application 双向依赖清零：raidLordToBattle 字段、ResetRaidLordLinks、LinkRaidLords、RunQualification 下沉 Data.ChronicleGameComponent（新增 Battle/Qualification partial）；MedalAwardService + MedalTranslationKeys 整体迁至 PersonalChronicle.Data（UI/测试引用同步）；ArchiveService 保留接口转发（IArchiveService 兼容）；Data 层 Application 引用归零，Application→Data 单向成立 |
| T3 | 5 个无前缀 DefName 加 Professional 前缀（ProfessionalXpPolicy_/ProfessionalRating_），代码无硬引用、labelKey 显式、不持久化，零迁移风险 |
| T4 | GUI 状态配对扫描：0 未配对风险点（全部 prev 快照 + try/finally 恢复） |
| T5 | DevTestButtons 9 条 Messages + 按钮文案 → 翻译键（中英各 +9）；const→static readonly 修正 |
| T6 | Schema 口径文档同步（v5→v6） |

验证：编译 0 错 0 警；单测 153 过 / 3 跳 / 0 失败；发布区已同步（dll + 中英翻译 + Defs/ProfessionalSkills.xml）。
遗留：T2（Api 契约倒挂，涉及公共 API 演进，建议独立迭代经 REL-008 流程）。
