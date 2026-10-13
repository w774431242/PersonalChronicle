---
change-id: CHG-20261011-ROUNDCLOSE
date: 2026-10-11
author: AI(DeepSeek-V4.1-Flash)
type: 集群规范升级（AI-015 用户向开工准备报告 + AI-016 每轮必输出契约与收尾闭环）
scope: 07-AI开发规范.md §15/§16 + 00-规则编号索引.md + pc-orchestrator/pc-release-clerk 职责与工作流 + frontdoor 规则 + 审计清单 + 审计脚本专项要素 + 新增 round-closure.ps1 收尾 oracle；**续修**：既有 21 条 doc-guard 阻断分层处置（9 补 fm / 12 豁免）+ `docs/README.md` 登记 `验证/` 与第七批记录 + 12- 副本漂移修复 + 交付轮严格模式默认化；无 Source/Defs/Save/UI 改动
base: HEAD
files:
  - path: docs/设计文档/架构/07-AI开发规范.md
    type: M
    symbols: AI-015 / AI-016 / §15 用户向开工准备报告协议（含可视化排版模板）/ §16 每轮必输出契约
    lines: 225-309
  - path: docs/设计文档/架构/00-规则编号索引.md
    type: M
    symbols: AI-001–AI-016
    lines: 32
  - path: docs/README.md
    type: M
    symbols: 用途判定表「验证/」行 / 第七批记录（21 件分层处置与结果）
    lines: 147,159-172
  - path: docs/任务派发/CodeBuddyCN/经济机构界面重构计划_2026-10-10.md
    type: M
    symbols: doc-id:DOC-TASK-20261010-ECON-UI-REFACTOR（本轮仅补 DOC-005 fm）
    lines: 1-8
  - path: docs/任务派发/CodeBuddyCN/经济AI逐模块深度审计_2026-10-10.md
    type: M
    symbols: doc-id:DOC-TASK-20261010-ECON-MODULE-AUDIT（本轮仅补 DOC-005 fm）
    lines: 1-8
  - path: docs/设计文档/功能模块/经济体系/实装落地/交接单_20261011_UI迁移与工作目录.md
    type: M
    symbols: doc-id:DOC-ECON-20261011-UI-MIGRATE-HANDOFF（本轮仅补 DOC-005 fm）
    lines: 1-8
  - path: docs/设计文档/架构/发布验收/第4批_实机验收清单.md
    type: M
    symbols: doc-id:DOC-ACCEPT-20261010-BATCH4（本轮仅补 DOC-005 fm）
    lines: 1-8
  - path: docs/验证/共有私有仓储美术_20261010/美术更新说明.md
    type: M
    symbols: doc-id:DOC-ASSET-20261010-PUBPRIV-ART（本轮仅补 DOC-005 fm）
    lines: 1-8
  - path: docs/验证/共有私有仓储与食品付款_20261010/说明与游戏验收.md
    type: M
    symbols: doc-id:DOC-VERIFY-20261010-PUBPRIV-PAYMENT（本轮仅补 DOC-005 fm）
    lines: 1-8
  - path: docs/验证/原版储存区公有化_20261010/说明与游戏验收.md
    type: M
    symbols: doc-id:DOC-VERIFY-20261010-VANILLA-STORAGE（本轮仅补 DOC-005 fm）
    lines: 1-8
  - path: docs/验证/Pawn需求聊天气泡_20261010/说明与游戏验收.md
    type: M
    symbols: doc-id:DOC-VERIFY-20261010-NEEDS-BUBBLE（本轮仅补 DOC-005 fm）
    lines: 1-8
  - path: docs/UI预览/经济机构工作台/机构工作台预览.html
    type: M
    symbols: doc-id:DOC-PREVIEW-20261010-ECON-COUNTER（HTML 注释承载 fm）
    lines: 1-9
---

# 变更标注 · CHG-20261011-ROUNDCLOSE

> 用户指令（2026-10-11）：①「按你推荐」⇒ 执行 AI-015 开工准备报告（A/A/A 档：多模块/新功能/用户可见变化必出七段、单文件小修三行简版；默认只在对话内输出不落盘；不加触发词）；②「发现没有一个收尾的 agent 去查验集群产品，经常性会缺漏一些，必须敲定 agent 集群中每轮对话必须输出的规范」⇒ 新增 AI-016 每轮必输出契约 + 收尾 oracle。

## 1. 缺口诊断（实证，非印象）

| 事实 | 证据 |
|---|---|
| 开工时点无用户向产物 | AI-001 是工程向（层级/规则 ID/Gate 计划）；Stage 0/1 产物是 `Change Intent`/ADR/依赖图/测试矩阵；交付报告与 Changelog 均为**事后**件 |
| 「准备报告」零存量 | `pwsh .codebuddy\audit\doc-guard.ps1 -Find 准备报告` ⇒ 文件名命中 **0** / 内容命中 **0** |
| 收尾无「产物齐备性」一层 | 既有 `finalize-change.ps1` 只串 ①context ②范围外清单 ③doc-guard ④GATE-DELTA ⑤回填检查；**无任何环节核对"本轮该有的产物是否都在"** |
| 缺漏已在案（本轮实测） | `doc-guard.ps1 -Blocking` ⇒ `结论：BLOCKED（新增 96 / BLOCKED 21 / WARN 2 / C7越基线修改 10 / C7消失 0）`；21 条全为 C3 缺 front-matter 的**并行会话新增件**（`docs/验证/**`、`docs/任务派发/CodeBuddyCN/*`、`docs/UI预览/经济机构工作台/*`） |

## 2. 变更清单

| # | 项 | 文件 | 类型 |
|---|---|---|---|
| 1 | 新增 **AI-015 用户向开工准备报告协议**（七段：一句话结论 / 怎么做 / 用什么做 / 前后区别 / 你要做什么 / 风险与回滚 / 怎么验收；语言约束：禁规则 ID、禁 Gate 名、禁分层术语；默认不落盘） | `docs/设计文档/架构/07-AI开发规范.md` §15（225-253） | M |
| 2 | 新增 **AI-016 每轮必输出契约**（R1–R8 八项 + 收尾 oracle + 边界声明：非第 11 道 Gate） | 同上 §16（254-289） | M |
| 3 | 规则编号索引登记 | `docs/设计文档/架构/00-规则编号索引.md:32`（AI-001–AI-014 → AI-001–AI-016） | M |
| 4 | 总控职责接入：AI-015 准备报告（动手前）+ AI-016 收尾校验；工作流插入 `1.5` 与 `11`；铁律与契约加 `OUTPUT_CONTRACT` | `.codebuddy/agents/pc-orchestrator.md` | M |
| 5 | **收尾闭环承载**：新增「收尾闭环（Round Closure · AI-016）」小节（6 条勾选 + 命令 + 越界边界）+ 铁律 + 契约 `ROUND_CLOSURE_ORACLE` + description 顺带登记 | `.codebuddy/agents/pc-release-clerk.md` | M |
| 6 | 前置路由协议加 **第 0 步（先报后做）** 与 **第 8 步（收尾校验）**，禁区补两条 | `.codebuddy/rules/agent-cluster-frontdoor.md` | M |
| 7 | 检查清单：§0 集群级加 2 项（准备报告 / 收尾校验）、§1 总控 S1/S4 加 3 项、§7 收尾 S12 加 3 项 | `.codebuddy/audit/subagent-audit-checklist.md` | M |
| 8 | 审计强制挂钩：`$extraLiterals` 为总控加 `AI-015/AI-016/round-closure.ps1`，为收尾承载 agent 加 `AI-016/round-closure.ps1`（**漏则审计 BLOCKED**） | `.codebuddy/audit/audit-subagents.ps1` | M |
| 9 | **新增收尾 oracle**：`round-closure.ps1`（M1 标注存在 / M2 schema / M2b ChangeId 一致 / M3 Gate Record 当日 / M4 记忆留痕 / M5 文档留痕 / M6 交付物署名 / M7 doc-guard 证据 / M8 R1–R8 出现（`-Transcript` 严格模式）+ 8 项人工清单；固定结论行 `收尾结论:`；`-Blocking` exit 2） | `.codebuddy/audit/round-closure.ps1` | A |
| 10 | skill 副本定点同步（**未跑 `sync_from_docs.ps1`**，理由见 §3-3）：`references/07-AI开发规范.md`、`references/00-规则编号索引.md` | `C:\Users\Administrator\.codebuddy\skills\RimWorld工程架构规范\`（**仓库外**） | M |
| 11 | skill 速查卡登记 AI-015 / AI-016 | 同上 `assets/cheat-sheet.md`（**仓库外**） | M |

## 3. 边界与待裁决项（诚实登记）

1. **不新建第 8 个 agent**。收尾闭环承载在 `pc-release-clerk`（REL/DELTA 侧，其 description 已含"交付终检"）；理由：`verify-cluster.ps1` 对"审计 PASS 计数恰为 7"是**硬断言**，且工程规则要求保留 `pc-orchestrator` 唯一总控、不重复创建总控。收尾环节**不判业务 Gate、不构成第 11 道 Gate**（10 道 Gate 归属不变）。
2. **`.codebuddy/**` 在 `.gitignore` 内** ⇒ 本轮 6 个 `.codebuddy` 改动（编号 4–9）**不进 gate-runner 的 git 改动集**，按 `CHG-20261010-EO-REG` 先例由范围外清单承载，其质量由 §4 的审计 / 哈希 / 实跑证据承担。
3. **未跑 `sync_from_docs.ps1`（有意）**。同步前双头体检实测：① `12-文档与交付物治理规范.md` 已**双头漂移**（docs 侧 2 行 ≠ skill 侧 2 行，脚本会单向覆盖）；② 顶层 **24 份 `EXC-2026-0xx.md`** 会被脚本 `Move-Item` 进 `references/history-archive/`，副作用远超本轮范围。⇒ 改为**定点 `Copy-Item` 本轮 2 个改动册 + SHA256 核验一致**（§4-5）。**遗留**：全量同步待 12- 漂移裁决后单独执行。
4. **收尾 oracle 的诚实边界**：R1–R8 中 R1/R5/R6/R7/R8 属**对话内产物**，机器不可判 ⇒ 一律标 `[人工]`，脚本不得代判 PASS；只有 `-Transcript` 严格模式才做"R 字样是否出现"的机械核对（出现 ≠ 内容合格）。
5. **doc-guard 既有 21 条阻断非本轮引入**（并行会话新增件）。本轮不修改他人产物（并发写入纪律）；`-Blocking` 结论如实贴出。**待裁决**：① 按 EO-REG 先例分层处置（补 fm / 走豁免）；② 或由各并行会话自查。

## 4. 验证证据

| # | 检查 | 结果 | 证据 |
|---|---|---|---|
| 1 | 集群合规审计（改后） | **PASS** | `结论: PASS  (PASS=7 / BLOCKED=0 / Gate覆盖=10/10 / skill覆盖=10/10)`；`注册表双源: OK（与 route-task.ps1 一致）` |
| 2 | 新增审计挂钩生效 | **PASS** | `$extraLiterals` 已含 `AI-015/AI-016/round-closure.ps1`（总控）与 `AI-016/round-closure.ps1`（pc-release-clerk）；未命中即 BLOCKED |
| 3 | 规则编号索引一致 | **PASS** | `00-规则编号索引.md:32` ⇒ `\| AI \| AI 执行协议 \| 07 \| AI-001–AI-016 \|`；07 正文实有 `## 15.`(225) / `## 16.`(254) |
| 4 | 双头体检（同步前） | **实测** | docs↔skill 顶层 24 册比对：`12-文档与交付物治理规范.md` DIFF（各 2 行不同）；24 份 `EXC-*.md` 在 docs 顶层但不在 skill（脚本会搬移） |
| 5 | skill 副本定点同步 | **PASS** | `07-AI开发规范.md` src=`25EB3F252E9F` skill=`25EB3F252E9F` 一致=True；`00-规则编号索引.md` src=`846B84C21996` skill=`846B84C21996` 一致=True |
| 6 | 收尾 oracle 实跑 | 见 §5 | `round-closure.ps1 -Blocking` 结论行 |
| 7 | 10 道业务 Gate | **N/A** | 理由：本轮为集群规范/工具链变更，无 `Source/`·`Defs/`·`Patches/`·`Languages/`·存档·UI 改动，业务 Gate 无适用对象 |

## 5. FINALIZE 终检结果（实测 stdout）

`pwsh .codebuddy\audit\change-context.ps1 -Set -ChangeId CHG-20261011-ROUNDCLOSE -Annotation <本件>` ⇒ `CONTEXT: OK`（excludeFromFile = `.codebuddy/tmp/chg-20261011-roundclose-out-of-scope.txt`）；`-Validate` ⇒ `CONTEXT: OK（标注与清单口径一致）`。

`pwsh .codebuddy\audit\finalize-change.ps1`：

```text
changeId   : CHG-20261011-ROUNDCLOSE
base       : HEAD

① context 校验      : OK
② 范围外清单         : OK —— 标注声明 2 个 · git 改动 2262 个 · 范围外 2259 个
③ doc-guard         : BLOCKED —— 结论：BLOCKED（新增 97 / BLOCKED 21 / WARN 2 / C7越基线修改 13 / C7消失 0）
④ GATE-DELTA        : PASS —— GATE-DELTA: PASS — 标注覆盖 2 个改动文件，无遗漏/超范围/残留（提示 0 条）。
⑤ 回填检查          : OK
⇒ FINALIZE: BLOCKED（唯一阻断 = ③；21 条全为并行会话新增件的 C3 缺 front-matter，见 §3-5）
```

**口径说明（诚实边界）**：④ `GATE-DELTA: PASS` 证明的是「本轮标注 ⟷ git 改动集双向互含、无超范围、无调试残留」；它**不覆盖** 6 个 `.codebuddy` 改动（`.gitignore` 区，见 §3-2），后者的证据是 §4-1/§4-2 的审计结论与挂钩生效。③ 的 BLOCKED 为本轮**之前**即存在的状态（本轮实测前值：新增 96 / BLOCKED 21），本轮改动使"新增"由 96→97（新增件为本标注，走 `Test-ExemptFlow` 豁免、不计入 BLOCKED），**未新增任何 C3 违规**。

**续修后终跑（第二轮 · 全绿）**：

```text
① context 校验      : OK
② 范围外清单         : OK —— 标注声明 12 个 · git 改动 2262 个 · 范围外 2249 个
③ doc-guard         : PASS —— 结论：PASS（新增 97 / BLOCKED 0 / WARN 2 / C7越基线修改 13 / C7消失 0）
④ GATE-DELTA        : PASS —— GATE-DELTA: PASS — 标注覆盖 12 个改动文件，无遗漏/超范围/残留（提示 1 条）。
⑤ 回填检查          : OK
⇒ FINALIZE: PASS
```

**首跑暂态（已实测澄清）**：第一次复跑曾报 `① FAIL —— 范围外清单包含标注已声明文件 ×3（自我豁免，GOV-012）`。原因是清单生成早于标注 `files:` 扩容（清单里仍留着 3 个新声明文件）。② 重生成清单后复跑即 `OK`，**非真缺陷**；该现象说明**改标注 `files:` 后必须让 ② 重跑一次再判 ①**（改 `change-context.ps1 -GenScope` 的调用顺序属独立议题，本轮不改）。

## 6. 收尾复核记录（收尾闭环首次实跑 · 由 pc-release-clerk 承载）

**派发**：按 frontdoor 第 8 步，`verify-cluster.ps1 -Blocking` 先得 `集群自检: PASS (runId=6e8ce224eb264bc3aa5098da810578af)` ⇒ 派发收尾承载 agent `pc-release-clerk` 做只读复核（29 次工具调用，含 3 条指定命令 + 3 条补充验证），落 `GR-CHG-20261011-ROUNDCLOSE-REL-DELTA.md`（REL=N/A / DELTA=PASS）。

**复核结论：`PASS WITH EXCEPTION`**，抓出 4 类真缺陷（全部当轮修掉，逐条留证）：

| # | 缺陷（复核发现） | 性质 | 本轮回修 |
|---|---|---|---|
| 1 | `subagent-audit-checklist.md`「收尾回复按 R1–R8 **十项**」 | 数目失真（全集群唯一硬性不一致） | 已改「八项」（同轮） |
| 2 | `round-closure.ps1` M6 署名检查用 `CodeBuddy` 大小写不敏感 ⇒ **引用 `.codebuddy` 路径的文档一律"已署名"**（近恒真） | **假通过风险** | 改**行级署名**模式（`由 AI 助手` / `CodeBuddy（模型`）；注释与实现同步 |
| 3 | M3「当日有其它记录但**含本轮 0 个**」仍判 PASS；M2b 全文命中即过 | 假通过风险 | M3 本轮无记录降 `WARN`（`-RequireGateRecord` 才阻断）；M2b **front matter 优先**，仅正文命中降 `WARN` |
| 4 | `Join-Path` 拼接绝对路径 ⇒ 误报"文件不存在"⇒ 误 BLOCKED | 误阻断缺陷 | 新增 `Resolve-ArgPath`（绝对路径直用）；脚本头补 AI 署名 |
| — | M8「出现 ≠ 内容合格」（出现字样即过） | 已知边界 | 保留设计，但 PASS 语显式声明"内容仍须人工复核"（脚本不判内容） |

**修后回归（同一命令，实跑）**：

```text
A. 严格模式（-Transcript）：M1 PASS · M2 PASS · M2b PASS(front matter) · M3 PASS(当日 1 / 含本轮 1) ·
   M6 PASS(行级署名) · M8 PASS(R1–R8 全部出现) ⇒ 收尾结论: PASS（机器项 通过=6 / WARN=1 / BLOCKED=0）EXIT=0
B. 反向对照（传无行级署名的 07 规范）：M6 BLOCKED（"无行级署名"）⇒ 收尾结论: BLOCKED · EXIT=2
   ⇒ 证明修后 M6 **有牙齿**，不再是近恒真 ✅
C. FINALIZE 复跑：①OK ②OK（标注声明 2 / git 改动 2262 / 范围外 2259）③doc-guard BLOCKED（既有 21，非本轮）④GATE-DELTA PASS ⑤OK
D. 集群审计复跑：结论: PASS (PASS=7 / BLOCKED=0 / Gate覆盖=10/10 / skill覆盖=10/10)；注册表双源: OK
```

**复核指出的"证据不足"项（属对话内产物，无法落盘取证）**：R1 开工准备报告 / R5 现在能用吗 / R3 完整四态表 / R7 记忆与文档动作 —— 由本轮收尾回复（R1–R8）补齐；`round-closure.ps1` 的人工清单亦已逐项声明"机器不可判"。

**本轮自检追加发现（在复核后再跑时抓到，一并修）**：M4 原实现只数 `.codebuddy/memory-journal/<当日>/requests`，而 `propose_change.py --stage` **实测**把提案写进 `.codebuddy/memory-journal/requests/`（**非按日分区**）⇒ 恒报 `requests=0`，会误导 R7 的"记忆动作"判断。已改为「全局 `requests/` + 当日 `pending/committed`」双口径；修正后实跑 `requests=1`（本轮提案 `req-8ec8ade44cdc48ff871acb39aeb93d1e`，`mem-20261011-8863a3195a65`，状态"待审核"）。

## 7. 续修（同日第二轮 · 修复收尾 · 用户指令「按你推荐进行修复收尾」）

| # | 项 | 处置 | 证据 |
|---|---|---|---|
| 1 | doc-guard **既有 21 条 C3 阻断** | 9 件补 DOC-005 五字段 fm（零正文改动、只插 fm 块、保留 BOM/行尾；`approved` 取各件**真实创建时间**）；12 件资产提示词**输入件**走 `Test-ExemptFlow` 流程豁免（字节即资产，注入 fm 会污染提示词） | 修前 `结论：BLOCKED（新增 96 / BLOCKED 21 / WARN 2 …）` → 修后 `结论：PASS（新增 97 / BLOCKED 0 / WARN 6 / C7越基线修改 13 / C7消失 0）` |
| 2 | 新用途目录 `验证/` 未登记 | 用途判定表补一行（DOC-001）+ 「第七批」记录（DOC-006） | `docs/README.md:147` 与第七批段 |
| 3 | `12-文档与交付物治理规范.md` **双头漂移** | 逐行比对确认 docs 侧为 v2.1 权威（skill 侧 2 行为旧版）⇒ 定点同步到 skill 副本 | 顶层副本全量复检 `剩余漂移=0`；`12-` SHA256 双侧 `44808C586C62…` 一致 |
| 4 | 收尾严格模式未默认 | **交付轮默认 `-Transcript`** 写进总控工作流第 11 步与检查清单 S4 | 本轮即以严格模式实跑，M8 PASS（R1–R8 字样全出现） |

**诚实边界**：

1. 补 fm 的 9 件属**并行会话产物**；本轮只插入 fm 块、**零正文改动**（逐件保留原 BOM 与行尾，脚本打印字节增量作为证据），其正文结论不在本轮复核范围、也未被本轮改变。
2. 复核豁免式样时曾疑 `/生成提示词/` 全局匹配会连带豁免存量路径 —— 经查基线内唯一命中项是**文件名**（`全部生成提示词.txt`，非目录）不匹配该式样，**无实际误豁免**；仍主动收窄为 `^docs/验证/[^/]+/生成提示词/` 以提高精度（并写入脚本注释）。
3. `WARN 6` 中 4 条为新件 C6（主题索引未提及叶子名，已由第七批记录部分覆盖）、2 条为既有 C6（`.codebuddy/audit/tmp/route-econ-ui-refactor.txt`、`.codebuddy/rules/engineering-orchestrator-entry.md`）；`-Blocking` 口径不阻断，`-Strict` 口径会升级 BLOCKED（仍待裁决）。

## 8. 第三次修订（同日 · 开工准备报告可视化排版）

用户指令：「对开工准备报告进行可视化文本排版，尽量简洁明了」。

| # | 项 | 处置 |
|---|---|---|
| 1 | `07-AI开发规范.md` §15 | 七段清单 → **固定可视化排版模板**（`━━` 规则行 + `①结论 ②做法 ③工具 ④变化 ⑤你要 ⑥风险 ⑦验收` 标签列 + 固定箭头语义 `→ ⇒ ⟶ ▸ ⟲ ⚑`）+ 一行简版 + **排版硬规则 6 条**（每段 ≤2 行 · 整份 ≤15 行 · ③ 只写真实命令名 · ⑤ 无事写 `⚑无` · ⑦ 未跑写「未执行」· 仍禁规则 ID/Gate 名/分层术语） |
| 2 | skill `assets/cheat-sheet.md` | 同步登记骨架（含并排压缩版与箭头语义表） |
| 3 | `.codebuddy/rules/agent-cluster-frontdoor.md` 第 0 步 | 指向 §15 可视化模板，三处描述一致（规范 / 速查卡 / 前置规则） |

**边界**：本轮只改**输出形态**，不改 AI-015 语义（七段内容、语言约束、默认不落盘、`R1` 在 AI-016 中的位置均不变）；`files:` 段不变（`07-AI开发规范.md` 已声明；两个 `.codebuddy` 文件在 `.gitignore` 区，按先例由范围外清单承载）。

> 本变更由 AI 助手实施：CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-11。
