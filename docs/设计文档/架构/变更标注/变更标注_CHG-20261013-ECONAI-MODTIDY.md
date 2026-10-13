---
change_id: CHG-20261013-ECONAI-MODTIDY
version: v1.0
date: 2026-10-13
author: AI 助手（CodeBuddy · DeepSeek-V4.1-Flash）
scope: [docs/设计文档/功能模块/经济体系/经济需求驱动AI/（9 类型目录 ⇒ 12 模块目录 + 00-总纲与共享）, docs/README.md（第八批登记）, docs/changelog/（本轮条目）, 外部存活引用（问题跟踪/开发参考/任务派发/changelog/经济体系 README）, _scripts/（8 个路径改）, .codebuddy/audit/doc-baseline.json（59 条路径改）]
scope-paths: [docs/设计文档/功能模块/经济体系/经济需求驱动AI/, docs/README.md, docs/设计文档/功能模块/经济体系/README.md, docs/changelog/, docs/问题跟踪/, docs/开发参考/, docs/任务派发/CodeBuddyCN/, _scripts/, .codebuddy/audit/doc-baseline.json, Source/PersonalChronicle/Domain/PawnEconomy/Planning/LifePolicy.cs]
title: 经济需求驱动 AI 文档按十二功能模块物理重组（v0.9 §2 模块划分 + 编号消歧 + 12 份模块入口）
state: Implemented
layer: 文档治理 / 目录重组（**无 Defs / Patches / Languages / About / Textures 改动；Source 仅 1 行引用注释**）
files:
  # A 类：本轮新建
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M01-状态与需求感知/README.md
    type: A
    symbols: 模块入口 README（职责 / 契约所有权 / 材料指针 / 编号消歧 / 变更历史），DOC-005 五字段 front-matter，approved=2026-10-13 00:24
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M02-长期生活规划/README.md
    type: A
    symbols: 模块入口 README（职责 / 契约所有权 / 材料指针 / 编号消歧 / 变更历史），DOC-005 五字段 front-matter，approved=2026-10-13 00:24
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M03-目标与多需求仲裁/README.md
    type: A
    symbols: 模块入口 README（职责 / 契约所有权 / 材料指针 / 编号消歧 / 变更历史），DOC-005 五字段 front-matter，approved=2026-10-13 00:24
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M04-方案比较与行动调度/README.md
    type: A
    symbols: 模块入口 README（职责 / 契约所有权 / 材料指针 / 编号消歧 / 变更历史），DOC-005 五字段 front-matter，approved=2026-10-13 00:24
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M05-生活预算与储蓄/README.md
    type: A
    symbols: 模块入口 README（职责 / 契约所有权 / 材料指针 / 编号消歧 / 变更历史），DOC-005 五字段 front-matter，approved=2026-10-13 00:24
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M06-就业与劳动合同/README.md
    type: A
    symbols: 模块入口 README（职责 / 契约所有权 / 材料指针 / 编号消歧 / 变更历史），DOC-005 五字段 front-matter，approved=2026-10-13 00:24
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M07-消费与服务/README.md
    type: A
    symbols: 模块入口 README（职责 / 契约所有权 / 材料指针 / 编号消歧 / 变更历史），DOC-005 五字段 front-matter，approved=2026-10-13 00:24
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M08-家庭与照护协作/README.md
    type: A
    symbols: 模块入口 README（职责 / 契约所有权 / 材料指针 / 编号消歧 / 变更历史），DOC-005 五字段 front-matter，approved=2026-10-13 00:24
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M09-经验反馈与失败恢复/README.md
    type: A
    symbols: 模块入口 README（职责 / 契约所有权 / 材料指针 / 编号消歧 / 变更历史），DOC-005 五字段 front-matter，approved=2026-10-13 00:24
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M10-再生产与公有机构运行/README.md
    type: A
    symbols: 模块入口 README（职责 / 契约所有权 / 材料指针 / 编号消歧 / 变更历史），DOC-005 五字段 front-matter，approved=2026-10-13 00:24
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M11-资金资源权利与制度协调/README.md
    type: A
    symbols: 模块入口 README（职责 / 契约所有权 / 材料指针 / 编号消歧 / 变更历史），DOC-005 五字段 front-matter，approved=2026-10-13 00:24
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M12-需求可视化与治理反馈/README.md
    type: A
    symbols: 模块入口 README（职责 / 契约所有权 / 材料指针 / 编号消歧 / 变更历史），DOC-005 五字段 front-matter，approved=2026-10-13 00:24
  # A 类：本轮交付物
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261013-ECONAI-MODTIDY.md
    type: A
    symbols: 本变更标注
  - path: docs/changelog/v1.4.x_2026-10-13_经济AI文档按十二模块重组.md
    type: A
    symbols: 本轮 changelog 条目
  # M 类：外部存活引用（未移动，仅改路径/链接）
  - path: docs/README.md
    type: M
    symbols: 指向主题子树的路径/链接同步为新目录（doc-baseline.json 为 59 条 p 路径改写，h/fm 保留）
  - path: docs/设计文档/功能模块/经济体系/README.md
    type: M
    symbols: 指向主题子树的路径/链接同步为新目录（doc-baseline.json 为 59 条 p 路径改写，h/fm 保留）
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/README.md
    type: M
    symbols: 指向主题子树的路径/链接同步为新目录（doc-baseline.json 为 59 条 p 路径改写，h/fm 保留）
  - path: docs/问题跟踪/经济机构_实装记录_2026-10-08.md
    type: M
    symbols: 指向主题子树的路径/链接同步为新目录（doc-baseline.json 为 59 条 p 路径改写，h/fm 保留）
  - path: docs/问题跟踪/实机测试问题记录.md
    type: M
    symbols: 指向主题子树的路径/链接同步为新目录（doc-baseline.json 为 59 条 p 路径改写，h/fm 保留）
  - path: docs/问题跟踪/待修复问题.md
    type: M
    symbols: 指向主题子树的路径/链接同步为新目录（doc-baseline.json 为 59 条 p 路径改写，h/fm 保留）
  - path: docs/开发参考/源码文件索引.md
    type: M
    symbols: 指向主题子树的路径/链接同步为新目录（doc-baseline.json 为 59 条 p 路径改写，h/fm 保留）
  - path: docs/任务派发/CodeBuddyCN/CB-ECON-003_劳务履约过程可视化与实体合同_需求单.md
    type: M
    symbols: 指向主题子树的路径/链接同步为新目录（doc-baseline.json 为 59 条 p 路径改写，h/fm 保留）
  - path: docs/任务派发/CodeBuddyCN/回报/CB-ECON-003_经理审计回执_20261012.md
    type: M
    symbols: 指向主题子树的路径/链接同步为新目录（doc-baseline.json 为 59 条 p 路径改写，h/fm 保留）
  - path: docs/任务派发/CodeBuddyCN/回报/CB-ECON-003_执行回报.md
    type: M
    symbols: 指向主题子树的路径/链接同步为新目录（doc-baseline.json 为 59 条 p 路径改写，h/fm 保留）
  - path: docs/changelog/v1.4.x_2026-10-08_经济AI实装启动.md
    type: M
    symbols: 指向主题子树的路径/链接同步为新目录（doc-baseline.json 为 59 条 p 路径改写，h/fm 保留）
  - path: docs/changelog/v1.4.x_2026-10-08_经济AI人口感知与查询.md
    type: M
    symbols: 指向主题子树的路径/链接同步为新目录（doc-baseline.json 为 59 条 p 路径改写，h/fm 保留）
  - path: docs/changelog/v1.4.x_2026-10-08_经济AI需求观察页.md
    type: M
    symbols: 指向主题子树的路径/链接同步为新目录（doc-baseline.json 为 59 条 p 路径改写，h/fm 保留）
  - path: docs/changelog/v1.4.x_2026-10-08_经济AI部署与实机验收准备.md
    type: M
    symbols: 指向主题子树的路径/链接同步为新目录（doc-baseline.json 为 59 条 p 路径改写，h/fm 保留）
  - path: .codebuddy/audit/doc-baseline.json
    type: M
    symbols: 指向主题子树的路径/链接同步为新目录（doc-baseline.json 为 59 条 p 路径改写，h/fm 保留）
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Planning/LifePolicy.cs
    type: M
    symbols: 指向主题子树的路径/链接同步为新目录（doc-baseline.json 为 59 条 p 路径改写，h/fm 保留）
  - path: _scripts/deploy-economy-shadow-planning.ps1
    type: M
    symbols: 主题文档路径同步
  - path: _scripts/deploy-pawn-economy-art.ps1
    type: M
    symbols: 主题文档路径同步
  - path: _scripts/finalize-pawn-economy-art.py
    type: M
    symbols: 主题文档路径同步
  - path: _scripts/test-economy-shadow-planning.ps1
    type: M
    symbols: 主题文档路径同步
  - path: _scripts/verify-economy-shadow-planning.ps1
    type: M
    symbols: 主题文档路径同步
  - path: _scripts/verify-pawn-finance-core.ps1
    type: M
    symbols: 主题文档路径同步
  - path: _scripts/verify-pawn-institution-mode.ps1
    type: M
    symbols: 主题文档路径同步
  - path: _scripts/verify-pawn-needs-ui.ps1
    type: M
    symbols: 主题文档路径同步
  - path: _scripts/verify-pawn-perception.ps1
    type: M
    symbols: 主题文档路径同步
  - path: _scripts/verify-pawn-settlement-persistence.ps1
    type: M
    symbols: 主题文档路径同步
  - path: _scripts/verify-pawn-silver-reserve.ps1
    type: M
    symbols: 主题文档路径同步
  - path: _scripts/verify-pawn-treasury-operations.ps1
    type: M
    symbols: 主题文档路径同步
  # R 类：主题内搬移（166 件）；内容改写的逐件列出，字节不变的按组聚合
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/02-代码设计总册/00-公有经济第一批代码设计.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/02-代码设计总册/00-全部模块方案_合订本.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/02-代码设计总册/00-全部模块方案_总册.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/00-调研与来源/经济需求驱动殖民者AI_可行性调研_2026-10-07.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/01-方案主线/多需求并发仲裁_设计补充_v0.4.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/01-方案主线/公有机构与外贸循环_优化方案_v0.7.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/01-方案主线/经济学优化与需求可视化_方案_v0.8.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/01-方案主线/玩法完整度评估与设计准入_v0.6.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/01-方案主线/原版需求缺口驱动经济AI_设计方案_v0.3.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/00-实装落地计划_v1.0.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/01-B1.1事实与缺口内核_实装记录.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/02-B1.2人口采集与查询_实装记录.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/09-B2.4.2_实机验收清单_2026-10-08.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/实施批次状态.json
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/实装验证.json
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/B1.2_实装验证.json
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/B1.3_实装验证.json
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/B2.1_实装验证.json
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/B2.2_实装验证.json
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/B2.3_实装验证.json
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/B2.4.1_实装验证.json
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/B2.4.2_实装验证.json
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/B2.4.2_UI复验.json
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/NEEDS-UPGRADE_实装验证.json
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/00-总册与实施路线.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/05-证据索引与验证记录.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/06-微观机构与场所工具.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/07-需求驾驶舱与政策反馈.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/evidence/全模块_设计清单.json
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/evidence/验证结果.json
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/tools/Collect-AllModulesEvidence.ps1
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/tools/Collect-LifePlanningEvidence.ps1
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/tools/Collect-NeedEvidence.ps1
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/tools/Collect-PerceptionEvidence.ps1
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/tools/Collect-PublicEconomyEvidence.ps1
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/tools/Export-AllModulesDesign.ps1
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/tools/Validate-AllModulesDesign.ps1
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/tools/Validate-LifePlanningDesign.ps1
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/tools/Validate-PerceptionDesign.ps1
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/07-归档/经济机构首版_操作与验收.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M01-状态与需求感知/01-状态与需求感知_代码设计方案.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M02-长期生活规划/02-长期生活规划_代码设计方案.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M03-目标与多需求仲裁/03-目标与多需求仲裁_代码设计方案.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M04-方案比较与行动调度/04-方案比较与行动调度_代码设计方案.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M05-生活预算与储蓄/05-生活预算与储蓄_代码设计方案.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M06-就业与劳动合同/06-就业与劳动合同_代码设计方案.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M07-消费与服务/07-消费与服务_代码设计方案.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M08-家庭与照护协作/08-家庭与照护协作_代码设计方案.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M09-经验反馈与失败恢复/09-经验反馈与失败恢复_代码设计方案.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M10-再生产与公有机构运行/10-再生产与公有机构运行_代码设计方案.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M11-资金资源权利与制度协调/11-资金资源权利与制度协调_代码设计方案.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M12-需求可视化与治理反馈/12-需求可视化与治理反馈_代码设计方案.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/M01-状态与需求感知/01-状态与需求感知_方案评审.md
    type: R
    symbols: 目录归口 + 内部相对链接/路径重算
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/（字节不变搬移的 113 件）
    type: R
    symbols: 仅 Move-Item，SHA-256 与迁前一致；逐件旧/新路径见 .codebuddy/tmp/econai-modtidy/move-manifest.json
---

# 变更标注 CHG-20261013-ECONAI-MODTIDY —— 经济需求驱动 AI 按十二模块物理重组

> 本轮为**纯文档域重组**（不改运行代码，Source/ 仅 1 行引用注释随迁）。
> 该主题子树**未被 git 跟踪**（git ls-files 计数 0），故本标注无法由 gate-runner 以 diff 校验，证据以逐文件 SHA-256 清单替代：
> `.codebuddy/tmp/econai-modtidy/move-manifest.json`（旧路径 / 新路径 / 迁前 SHA-256）+ 同目录 `backup/`（迁前字节快照）。

## 1. 统计

| 类别 | 件数 |
|---|---|
| 新增文档 | 12（12 份模块 README） |
| 搬移·字节不变 | 113 |
| 搬移·内容改写（链接/路径） | 53 |
| 未移动·内容改写（外部存活引用） | 28 |
| 删除 | 0 |

## 2. 新增文档（DOC-005 五字段 front-matter，`approved: 2026-10-13 00:24`）

- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/M01-状态与需求感知/README.md`
- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/M02-长期生活规划/README.md`
- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/M03-目标与多需求仲裁/README.md`
- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/M04-方案比较与行动调度/README.md`
- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/M05-生活预算与储蓄/README.md`
- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/M06-就业与劳动合同/README.md`
- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/M07-消费与服务/README.md`
- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/M08-家庭与照护协作/README.md`
- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/M09-经验反馈与失败恢复/README.md`
- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/M10-再生产与公有机构运行/README.md`
- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/M11-资金资源权利与制度协调/README.md`
- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/M12-需求可视化与治理反馈/README.md`

## 3. 搬移映射（逐组）

| 旧位置 | 新位置 | 件数 |
|---|---|---|
| `docs/设计文档/功能模块/经济体系/经济需求驱动AI/游戏代码设计/01…12-*_代码设计方案.md` + `01-…_方案评审.md` | `M01-…`–`M12-…`（文件名不变） | 13 |
| `docs/设计文档/功能模块/经济体系/经济需求驱动AI/游戏代码设计/`（余 3 件） | `docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/02-代码设计总册/` | 3 |
| `docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-调研与来源/` | `docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/00-调研与来源/` | 4 |
| `docs/设计文档/功能模块/经济体系/经济需求驱动AI/01-方案主线/` | `docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/01-方案主线/` | 7 |
| `docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/` | `docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/` | 37 |
| `docs/设计文档/功能模块/经济体系/经济需求驱动AI/模块化实装指导手册/` | `docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/` | 62 |
| `docs/设计文档/功能模块/经济体系/经济需求驱动AI/经济AI流程图/` + `可视化/` | `docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/05-表现层/{经济AI流程图,可视化}/` | 22 |
| `docs/设计文档/功能模块/经济体系/经济需求驱动AI/交付包/` | `docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/06-交付包/` | 4 |
| `docs/设计文档/功能模块/经济体系/经济需求驱动AI/_归档/` | `docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/07-归档/` | 1 |

## 4. 内容改写（未移动的外部存活引用）

- `docs/README.md`
- `docs/设计文档/功能模块/经济体系/README.md`
- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/README.md`
- `docs/问题跟踪/经济机构_实装记录_2026-10-08.md`
- `docs/问题跟踪/实机测试问题记录.md`
- `docs/问题跟踪/待修复问题.md`
- `docs/开发参考/源码文件索引.md`
- `docs/任务派发/CodeBuddyCN/CB-ECON-003_劳务履约过程可视化与实体合同_需求单.md`
- `docs/任务派发/CodeBuddyCN/回报/CB-ECON-003_经理审计回执_20261012.md`
- `docs/任务派发/CodeBuddyCN/回报/CB-ECON-003_执行回报.md`
- `docs/changelog/v1.4.x_2026-10-08_经济AI实装启动.md`
- `docs/changelog/v1.4.x_2026-10-08_经济AI人口感知与查询.md`
- `docs/changelog/v1.4.x_2026-10-08_经济AI需求观察页.md`
- `docs/changelog/v1.4.x_2026-10-08_经济AI部署与实机验收准备.md`
- `.codebuddy/audit/doc-baseline.json`
- `Source/PersonalChronicle/Domain/PawnEconomy/Planning/LifePolicy.cs`
- `_scripts/deploy-economy-shadow-planning.ps1`
- `_scripts/deploy-pawn-economy-art.ps1`
- `_scripts/finalize-pawn-economy-art.py`
- `_scripts/test-economy-shadow-planning.ps1`
- `_scripts/verify-economy-shadow-planning.ps1`
- `_scripts/verify-pawn-finance-core.ps1`
- `_scripts/verify-pawn-institution-mode.ps1`
- `_scripts/verify-pawn-needs-ui.ps1`
- `_scripts/verify-pawn-perception.ps1`
- `_scripts/verify-pawn-settlement-persistence.ps1`
- `_scripts/verify-pawn-silver-reserve.ps1`
- `_scripts/verify-pawn-treasury-operations.ps1`

## 5. 不改清单（按项目惯例登记，属历史快照）

- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/03-实装批次与状态/` 内 33 份 `变更标注_CHG-*.md`
- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/04-实装指导手册/evidence/*.json`（各批校验报告内的旧路径 + 哈希属当时快照；须先 `Collect-*` 重新取证）
- `docs/设计文档/功能模块/经济体系/经济需求驱动AI/00-总纲与共享/06-交付包/交付清单.json` 与 v1.0 ZIP（包快照，路径按包内路径）
- `_scripts/_delta_scope_needs_tab.txt`（一次性范围清单，124 处旧路径）
- `docs/设计文档/架构/变更标注/变更标注_CHG-20261008-PAWN-ECONOMY.md` 中一条**存量**断链（正文以仓库根相对路径当相对链接写，非本轮引入）

## 6. 验证证据

| 项 | 命令 | 结果 |
|---|---|---|
| 搬移字节一致 | `migrate-econai-modtidy.ps1 -Mode Verify` | ok=167 / bad=0 |
| 主题子树断链 | `link-scan.ps1 -Scope <主题根>` | checked=327 / broken=0 |
| 文档域门禁 | `doc-guard.ps1 -Blocking` | 新增 158（含 12 份新 README，已登记索引）/ **C7 消失 0** / BLOCKED 11（**均为他会话存量 C3**，非本轮） |
| 手册结构校验 | `tools/Verify-Handbook.ps1 -SkipBuild` | ⛔ pre-existing 失败：`Expected 7 handbook chapters, got 8`（原目录即为 8 个 `*.md`，已用字节级备份核实；本轮未改该断言） |
| 基线路径 | doc-baseline.json | 59 条主题路径逐条改写，old-form 残留 0（未用 `-Rebaseline`） |

## 7. 回滚

1. 按 `README.md` §八 的旧⇒新表反向 `Move-Item`；或直接执行 `.codebuddy/tmp/econai-modtidy/migrate-econai-modtidy.ps1 -Mode Rollback`。
2. 内容层以 `.codebuddy/tmp/econai-modtidy/backup/`（迁前字节快照）覆盖还原。
3. 外部存活引用需手工回退（无迁前快照）：`docs/README.md` · `经济体系/README.md` · `_scripts/` 8 个 · `doc-baseline.json` 59 条。

> 由 AI 助手实施：CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-13。