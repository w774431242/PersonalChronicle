---
change-id: CHG-20261009-DOCGOV
date: 2026-10-09
author: AI(DeepSeek-V4.1-Flash)
type: 治理设施新增 + 规范新增（文档与交付物治理）
scope: DOC-001~010 规则 + doc-standard skill + 集群接线（7 agent / 注册表 / checklist / 回归测试）+ doc-guard 门禁 + 基线；存量文件不动
base: HEAD
files:
  - path: docs/设计文档/架构/12-文档与交付物治理规范.md
    type: A
  - path: docs/设计文档/架构/00-规则编号索引.md
    type: M
  - path: docs/设计文档/架构/README.md
    type: M
  - path: docs/README.md
    type: M
---

# 变更标注 · CHG-20261009-DOCGOV

> 落点为本轮新建的变更标注目录 `docs/设计文档/架构/变更标注/`（DOC-001 首件实践）。
> 过程证据/临时文件（`.codebuddy/tmp/**`、`gate-records`、标注自身）不在本节声明范围内，另见范围外清单。

## 1. 声明范围（21 个交付物）

| # | 文件 | 类型 | 改动摘要 / 涉及符号 |
|---|---|---|---|
| 1 | `docs/设计文档/架构/12-文档与交付物治理规范.md` | 新建 | DOC-001~010 规则正文（含 front-matter 契约、落位判定、门禁 C1–C6、范围外声明、双副本同步纪律） |
| 2 | `.codebuddy/skills/doc-standard/SKILL.md` | 新建 | 文档治理执行手册（五大铁律、写入协议、申请模板、集群联动；非独立 Gate 声明） |
| 3 | `.codebuddy/skills/doc-standard/references/schema.md` | 新建 | 字段字典 + 落位表 + 申请/取代模板 + doc-guard 用法 |
| 4 | `.codebuddy/rules/doc-write-gate.md` | 新建 | alwaysApply 前置门（DOC-003/DOC-004；禁区清单） |
| 5 | `.codebuddy/audit/doc-guard.ps1` | 新建 | 门禁脚本：C1/C2 落点、C3 front-matter、C4 取代、C5 相似度、C6 索引；`-Blocking`/`-Strict`/`-Find`/`-Rebaseline`/`-ExcludeFromFile` |
| 6 | `.codebuddy/audit/doc-baseline.json` | 新建 | 文件系统基线（724 文件路径集合，2026-10-09 20:09 冻结） |
| 7 | `docs/设计文档/架构/00-规则编号索引.md` | 修改 | 编号域表 + 稳定清单区登记 DOC 域（DOC-001–010） |
| 8 | `docs/设计文档/架构/README.md` | 修改 | 目录树/使用顺序登记 12 分册；变更标注新落点与冻结口径；计数 43→45 |
| 9 | `docs/README.md` | 修改 | 新增「文档治理基线（2026-10-09）」段 + 待办登记 |
| 10 | `.codebuddy/agents/pc-orchestrator.md` | 修改 | `SKILLS` 加 doc-standard；AI-001 入口加「文档影响」项；越界禁区加文档禁令；工作流加第 10 步 |
| 11 | `.codebuddy/agents/pc-release-clerk.md` | 修改 | 触发词 +7（文档治理/新建文档/写文档/更新文档/文档落点/文档索引/变更标注归档）；`SKILLS` 加 doc-standard；新增「文档产出规范」横切节；铁律 +2 |
| 12 | `.codebuddy/agents/pc-arch-gatekeeper.md` | 修改 | 越界禁区加文档域禁令 |
| 13 | `.codebuddy/agents/pc-data-steward.md` | 修改 | 同上 |
| 14 | `.codebuddy/agents/pc-ui-loc-guard.md` | 修改 | 同上 |
| 15 | `.codebuddy/agents/pc-compat-auditor.md` | 修改 | 同上 |
| 16 | `.codebuddy/agents/pc-perf-tester.md` | 修改 | 同上 |
| 17 | `.codebuddy/audit/route-task.ps1` | 修改 | `$knownSkills` 加 doc-standard（与 audit 侧双源一致） |
| 18 | `.codebuddy/audit/audit-subagents.ps1` | 修改 | `$knownSkills` 加 doc-standard；`$baseLiterals` 加 `doc-standard`（7 agent 硬保证） |
| 19 | `.codebuddy/audit/route-task.tests.ps1` | 修改 | 新增 3 条用例（T1/文档治理、T1/文档落点、S1/doc-standard） |
| 20 | `.codebuddy/audit/subagent-audit-checklist.md` | 修改 | §0 集群级 +2 项；§0.1 触发词表；§0.3 注册表 + 挂载点 8→9；§7 勾选 +3 项 |
| 21 | `.codebuddy/audit/toolchain-registry.md` | 修改 | E 类登记 doc-guard.ps1（含调用方式）；F 类 pc-release-clerk 主用工具 +doc-guard |

## 2. 范围外声明（不入本次 diff 断言）

### 2.1 git 忽略区（`.codebuddy/**`）——本交付实质修改但不进 front matter

`.codebuddy/**` 未被 git 跟踪（gitignore 区），无法通过 diff 证明 ⇒ 按既有纪律改为 body 说明（下列 17 项**必须与实物一致**，由 `audit-subagents.ps1` + `route-task.tests.ps1` + `doc-guard.ps1` 的实测证据覆盖）：

| 文件 | 类型 | 改动摘要 |
|---|---|---|
| `.codebuddy/skills/doc-standard/SKILL.md` | A | 文档治理执行手册（五大铁律 / 写入协议 / 申请模板 / 集群联动） |
| `.codebuddy/skills/doc-standard/references/schema.md` | A | 字段字典 + 落位表 + 模板 + doc-guard 用法 |
| `.codebuddy/rules/doc-write-gate.md` | A | alwaysApply 前置门（DOC-003/004 + 禁区） |
| `.codebuddy/audit/doc-guard.ps1` | A | 门禁 C1–C6 + 基线 + 范围外声明 |
| `.codebuddy/audit/doc-baseline.json` | A | 基线快照（724 文件，2026-10-09 20:09） |
| `.codebuddy/agents/pc-orchestrator.md` | M | SKILLS + 文档影响项 + 越界禁令 + 工作流第 10 步 |
| `.codebuddy/agents/pc-release-clerk.md` | M | 触发词 +7 / SKILLS / 文档产出规范节 / 铁律 +2 |
| `.codebuddy/agents/pc-arch-gatekeeper.md` | M | 越界禁令 +1 行 |
| `.codebuddy/agents/pc-data-steward.md` | M | 越界禁令 +1 行 |
| `.codebuddy/agents/pc-ui-loc-guard.md` | M | 越界禁令 +1 行 |
| `.codebuddy/agents/pc-compat-auditor.md` | M | 越界禁令 +1 行 |
| `.codebuddy/agents/pc-perf-tester.md` | M | 越界禁令 +1 行 |
| `.codebuddy/audit/route-task.ps1` | M | `$knownSkills` +doc-standard |
| `.codebuddy/audit/audit-subagents.ps1` | M | `$knownSkills` +doc-standard；`$baseLiterals` +doc-standard |
| `.codebuddy/audit/route-task.tests.ps1` | M | +3 条回归用例 |
| `.codebuddy/audit/subagent-audit-checklist.md` | M | §0 / §0.1 / §0.3 / §7 同步 |
| `.codebuddy/audit/toolchain-registry.md` | M | E 类登记 doc-guard.ps1 + F 类映射 |

### 2.2 过程证据 / 临时文件（`.codebuddy/tmp/**`、gate-records）

- `.codebuddy/tmp/task-doc-gov.txt` / `.json`（路由证据）、`.codebuddy/tmp/docguard-out-of-scope.txt`（doc-guard 范围外清单）、`.codebuddy/tmp/docgov-out-of-scope.txt`（gate-runner 范围外清单）、`.codebuddy/tmp/gen-outofscope.ps1`（一次性生成脚本）；
- `.codebuddy/audit/gate-records/GR-20261009-DOCGOV.md`（Gate 证据）。

### 2.3 工程外 / 并行会话

- 本机全局 skill 目录 `C:\Users\Administrator\.codebuddy\skills\RimWorld工程架构规范\`：`scripts/sync_from_docs.ps1`（lint 前缀加 `DOC`）+ `references/` 同步产物（工程外，非本仓 diff）；
- `变更标注_CHG-20261009-ECON-M8-CARE.md`（仓库根）：**并行会话在途产物**，非本轮改动，按惯例不纳入、不回改、不迁移。

## 3. 验证证据

| 项 | 命令 | 结果 |
|---|---|---|
| 集群审计 | `pwsh .codebuddy/audit/audit-subagents.ps1 -Blocking` | PASS（7/7、Gate 10/10、skill 9/9、双源一致） |
| 路由回归 | `pwsh -NoProfile -File .codebuddy/audit/route-task.tests.ps1` | PASS=22 / FAIL=0 |
| 文档门禁（正样本） | `pwsh .codebuddy/audit/doc-guard.ps1 -Blocking -ExcludeFromFile .codebuddy/tmp/docguard-out-of-scope.txt` | 结论 PASS（新增 0 / 范围外 1） |
| 文档门禁（负样本） | 三处建测试 .md（仓库根 / docs 顶层 / docs 子目录） | C1/C2/C3 全拦截（3 BLOCKED），测试文件已清理 |
| 双副本同步 | `sync_from_docs.ps1 -Source docs/设计文档/架构` | Copied 61 / Hash 全对 / lint 全解析（231 IDs） |
| DELTA 终检 | `gate-runner.ps1 -Base HEAD -Annotation <本标注> -ExcludeFromFile .codebuddy/tmp/docgov-out-of-scope.txt -Blocking` | 见 §4 |

## 4. DELTA 终检结果

```text
pwsh <skill>\scripts\gate-runner.ps1 -Base HEAD -Annotation docs\设计文档\架构\变更标注\变更标注_CHG-20261009-DOCGOV.md -ExcludeFromFile .codebuddy\tmp\docgov-out-of-scope.txt -Blocking
⇒ GATE-DELTA: PASS — 标注覆盖 4 个改动文件，无遗漏/超范围/残留。（范围外声明 2284 个不参与判定）
```

- 首次运行 BLOCKED 的两条真因（均已修复，留档）：
  1. 标注 front matter 格式须为 `- path: <路径>`（gate-runner 手写解析器 + 表格退化分支只认第一列），`- <路径>` 不被识别；
  2. `.codebuddy/**` 属 git 忽略区（未跟踪）⇒ 写入 front matter 会触发「phantom（标注有、git 无）」；正确做法是**只声明 git 可见文件，忽略区改由 body 说明**。
- 环境限制：本沙箱不传播 PowerShell 退出码（`exit 2` 实测返回 0），判读以 stdout 结论行为准。

> 本变更由 AI 助手实施：CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
