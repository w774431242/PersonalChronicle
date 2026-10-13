---
change_id: CHG-20261012-REPORTFMT
version: v1.0
date: 2026-10-12
author: AI 助手（CodeBuddy · DeepSeek-V4.1-Flash）
scope: [docs/设计文档/架构/（报告排版需求新建 + 正本 §15 / §16.1b 编号规则）, docs/README.md（索引登记）]
scope-paths: [docs/设计文档/架构/, docs/README.md]
title: 报告排版需求（多项目编号规则 + 速查文档）+ 集群口径 R1–R8 ⇒ R1–R9 同步
state: Implemented
layer: 工程治理设施（规范文本 + 工程约定；**无 Source / Defs / Patches / Languages / About / Textures 改动**）
files:
  - path: docs/设计文档/架构/07-AI开发规范.md
    type: M
    symbols: §15 开工骨架（②④⑤⑦ 多项目改编号）/ §15 硬规则 6 条 ⇒ 7 条（新增「多项并列 ≥3 必须编号」+ 说人话）/ §16.1b 收尾骨架（R3/R5/R6 明细改编号）/ §16.1b 硬规则 7 条 ⇒ 8 条（新增编号条；`▸` 限定 ≤2 项）
    lines: 237-350
    note: 用户反馈「多个项应该采用序号排列 1. 2. 3. 更直观」「说明干练、不需要过于专业化」
  - path: docs/设计文档/架构/工程约定/报告排版需求.md
    type: A
    symbols: 新增（doc-id DOC-ARCH-20261012-REPORT-FORMAT）：11 条排版要求 + 两套骨架模板 + 填写示例 + 与相邻产物分工
    lines: 1-190
    note: 给人看的速查版；口径冲突以正本 §15 / §16.1b 为准（文内已声明）
  - path: docs/设计文档/架构/README.md
    type: M
    symbols: 工程约定目录树登记「报告排版需求.md」（DOC-006 索引同步）
  - path: docs/README.md
    type: M
    symbols: 架构目录树登记「报告排版需求」
---

# 变更标注 · CHG-20261012-REPORTFMT

## 一、用户需求

「给我一份当前 agent 集群的输出报告样式模板」→ 「如果有多个项，应该采用序号排列，如 1. 2. 3. 等等，直观一些」
→ 「我需要文本排版需求，直观简洁，说明干练不需要过于专业化」→ 「2 和 3，更新升级 agent 集群」。

即三件事：**①** 一份可照抄的报告模板；**②** 多项目一律编号；**③** 把该排版需求落成仓内文档（选项 2）并写进规范正本、同步升级集群（选项 3）。

## 二、本轮交付（规范侧，进 GATE-DELTA 判定）

| 文件 | 类型 | 改动 |
|---|---|---|
| `docs/设计文档/架构/07-AI开发规范.md` | M | ① **§15 开工骨架**：`②做法 / ④变化 / ⑤你要 / ⑦验收` 的多项改为 `1.` `2.` `3.` 逐项；② **§15 硬规则新增第 4 条**「多项并列（≥3 项）必须编号，只用于清单、不替代 `→`/`⇒`、不吃行数上限」，第 3 条 `▸` 限定 **仅 ≤2 项**，第 7 条补「措辞直白、说人话、禁缩写堆叠」；③ **§16.1b 收尾骨架**：`R3` 明细 / `R5` 你要做 / `R6` 遗留明细改编号；④ **§16.1b 硬规则 7 条 ⇒ 8 条**（新增第 8 条编号规则；第 2 条 `▸` 限定 ≤2 项；第 7 条补直白措辞）；⑤ **同轮补修「输出保真」**：§15 硬规则 7 ⇒ **8 条**、§16.1b 硬规则 8 ⇒ **9 条**，新增「报告须带代码块围栏发出，否则渲染端合并单个换行、整份压成一段」。**未改动 R1–R9 的项名与语义**，只改排版与投递方式 |
| `docs/设计文档/架构/工程约定/报告排版需求.md` | A | 新建速查件（DOC-005 五字段齐备，`approved=2026-10-12 23:27`）：11 条排版要求（整体 / 行首 / 多项目 / 写到什么程度）+ 开工 & 收尾两套模板 + 填写示例（含编号效果）+ 与 Change Summary / 变更标注 / Changelog 的分工。**明确标注权威源为正本**，本文只是速查。**同轮补 v1.1**：新增「六、发出去时注意什么（输出保真）」—— 必须带代码围栏，围栏不计行数，被压平即交付缺陷 |
| `docs/设计文档/架构/README.md` | M | 工程约定目录树登记新件（C6 索引同步） |
| `docs/README.md` | M | 架构目录树登记「报告排版需求」 |

## 三、本轮交付（工程工具区，`.codebuddy/**` 属 git 忽略区 ⇒ **不入 GATE-DELTA 判定**）

| 文件 | 类型 | 改动 |
|---|---|---|
| `.codebuddy/agents/pc-orchestrator.md` | M | ① AI-015 段补「多项并列 ≥3 必须编号 / 说人话」；② **三处旧口径 `R1–R8` ⇒ `R1–R9`**（AI-016 条、端到端流程第 11 步、每轮必输出红线） |
| `.codebuddy/agents/pc-release-clerk.md` | M | ① description 的 `R1–R8 输出清单` ⇒ `R1–R9`；② 收尾闭环清单 **八项 ⇒ 九项**（补 `R9 台账收敛`），并新增一条「按 §16.1b 核对骨架：多项并列 ≥3 必须编号」 |
| `.codebuddy/audit/subagent-audit-checklist.md` | M | S4 第 155 行既有笔误「按 `R1–R9` 八项」⇒ **九项**，并补编号要求 |
| `.codebuddy/audit/round-closure.ps1` | M（v1.4 ⇒ **v1.7**） | ① **v1.5**：`$rItems` 人工项清单新增「多项目编号（并列 ≥3 须 `1.` `2.` `3.`，≤2 才用 `▸`）」（人工项 9 ⇒ 10 项，`M8` 词边界核验逻辑**未动**）；② **v1.6（WI-024，本轮实测暴露后当场修）**：M9 回落读 `change-context.json` 项目槽前补 **changeId 归属校验** —— 槽内 `changeId` 与本轮 `-ChangeId` 不一致（被另一会话占用）即**不采用**其 program，改判 `N/A` 并在理由行明示占用者；③ **v1.7**：人工项再增「输出保真（带代码块围栏发出）」（人工项 10 ⇒ 11 项）；JSON 证据 `note` 同步登记 v1.5/v1.6/v1.7 |
| `.codebuddy/audit/toolchain-registry.md` | M | 登记 `round-closure.ps1` v1.5 与新增人工项（改工具须同日更新登记册） |
| `.codebuddy/rules/agent-cluster-frontdoor.md` | M | 前置协议第 8 步的骨架描述补「多项目编号 `1./2./3.`」，使入口即传递该口径 |
| skill 副本 `RimWorld工程架构规范/references/07-AI开发规范.md` | M | 经 `sync_from_docs.ps1 -Source docs\设计文档\架构` 回同步（**同步前先对比源↔副本**：差异 42 行全部为本轮正本改动，副本无独有内容） |

## 四、验证证据（可复现）

| # | 断言 | 命令 | 实测 |
|---|---|---|---|
| V1 | 集群结构/契约/路由无回归 | `pwsh .codebuddy\audit\verify-cluster.ps1 -Blocking` | `集群自检: PASS（PASS=7 / BLOCKED=0 · Gate覆盖=10/10 · skill覆盖=10/10 · 路由回归 PASS=23 / FAIL=0）` |
| V2 | 同步前无「副本独有内容」被单向覆盖 | `Compare-Object (Get-Content 正本) (Get-Content 副本) -PassThru` | 差异行全部落在本轮改动的骨架与硬规则处，无副本独有条款 |
| V3 | skill 副本与正本逐字节一致 | `sync_from_docs.ps1 -Source <架构目录>` | `Copied 62 spec files · Hash check: all files match the source · Rule-reference lint: all references resolve (230 defined IDs)` |
| V4 | 收尾脚本可解析且版本已升 | `pwsh .codebuddy\audit\round-closure.ps1 -Version` | `round-closure.ps1 v1.5` |
| V5 | 新文档落点与 front-matter 合规、索引已登记 | `pwsh .codebuddy\audit\doc-guard.ps1 -Blocking` | 新件 `报告排版需求.md` **未出现在 BLOCKED/WARN 列表**；全局 `BLOCKED 11 / WARN 4` **全为既有阻断**（见下） |

**V5 既有阻断如实区分（非本轮引入）**：`BLOCKED 11` 全部为 2026-10-11 并行会话新增件的 C3 缺 front-matter —— `docs/参考资料/DLC接口参考包_20261011/**`（10 件，含 README 与 snapshot）与 `docs/验证/公有资产与机构资金贯通_20261011/体系链路与修复方案.md`（1 件）；`WARN 4` 为 `.codebuddy/audit/scope/`、`.codebuddy/audit/tmp/`、`.codebuddy/rules/engineering-orchestrator-entry.md` 的 C6。**本轮未触碰上述任一文件，也未刷新基线**（DOC-010：基线刷新仅限存量收口轮次）。

## 五、GATE-DELTA 与并行会话范围划分

| 项 | 值 |
|---|---|
| 命令 | `gate-runner.ps1 -Base HEAD -Annotation <本标注> -ExcludeFromFile .codebuddy/audit/scope/chg-20261012-reportfmt-out-of-scope.txt -Blocking` |
| 结论 | **PASS — 标注覆盖 4 个改动文件，无遗漏/超范围/残留（提示 0 条）**，`EXITCODE=0` |
| 范围外 | 2593 个文件（预先存在的无关改动 + 并行会话在途产物）；清单由 `CHG-20261012-ECON-HAULCMD` 的范围外清单**派生**，并剔除本轮 in-scope 的 3 个文件 |
| 并行写者（如实登记） | 本轮检测到**并行会话 `CHG-20261012-ECON-HAULCMD` 在途**：`change-context.json` 于 23:02 挂该 Change、23:23 刷新范围外计数。DELTA 首跑报出 6 个 `Source/**` `Tests/**` 越界项 + `EXC-2026-031.md` + 其自身标注 ⇒ 判定为**并行会话在途产物**，本轮**未覆盖其 `change-context.json` 状态**，只把这 8 项登记为范围外。`docs/README.md` 为**双写共享文件**（两会话各改各节），本轮按**文件粒度**声明 in-scope |

## 六、未执行 / 遗留（不伪造）

1. **GATE-DELTA**：**已执行并 PASS**（命令与结论见第五节）。Gate Record 已落 `.codebuddy/audit/gate-records/GR-CHG-20261012-REPORTFMT-REL-DELTA.md`（含本轮 N/A 业务门的理由）。
2. **专家团双轨同步未做（待裁决）**：`C:\Users\Administrator\.workbuddy\...\rimworld-mod-studio\` 侧 `rimworld-studio-lead` 的 Phase 6 仍写「收尾契约 R1–R8」。按 `CHG-20261011-CLUSTER-SYNC` 纪律（方向恒为集群→专家团、先备份再改、只改 `agents/` + README + `plugin.json`）需用户确认后另行执行。**已登记 `PROG-TOOLGOV/WI-025`**。
3. **WI-024 本轮已修复并关闭**（原为「只登记、未修」）：实测暴露 `round-closure` 的 M9 在**多会话并行**下回落读共享 `change-context.json`，把并行会话 `CHG-20261012-ECON-HAULCMD` 的 `PROG-ECON` 槽当成本轮判据（首跑报 `PROG-ECON 16→17` BLOCKED，且豁免因「连续两轮」再被拦）。已修 **v1.6 归属校验**：槽内 `changeId` ≠ 本轮 `-ChangeId` 即不采用，改判 `N/A` 并明示占用者；复跑 ⇒ `M9 N/A`、`收尾结论: PASS`。台账 `WI-024 ⇒ DONE`（带 Evidence）。
4. **既有 doc-guard 阻断 11 项未清**：属 2026-10-11 并行会话产物，非本轮引入，**未刷新基线**（DOC-010），按「收尾 PASS ≠ 文档门禁 PASS」如实保留。
5. **游戏内验证 N/A**：本轮无 Source / Defs / 运行时改动，不涉及实机验收。

## 七、同轮补修：实际产出被渲染压平（用户实测截图）

**现象**：用户贴出实际收到的收尾报告截图 —— 首末 `━━` 规则行与 `R1` 连成同一行、`R1…R9` 全挤成一大段、`└` 明细行消失。

**根因**：报告以**裸行**投递时，聊天窗/预览器按 Markdown 软换行处理，**把单个换行合并成空格**；而骨架靠的正是逐行锚定 ⇒「一眼可扫」完全失效。规范此前只规定骨架怎么写、**没规定投递时必须保留代码围栏** —— 属规范缺口，不是排版写错。

**修法**（同 Change 内补，不另开轮次，避免碎片化）：

1. 正本：§15 硬规则 **7 ⇒ 8 条**、§16.1b 硬规则 **8 ⇒ 9 条**，新增**输出保真** —— 报告须带代码块围栏（围栏语言标 `text`）发出；围栏不计入行数上限；不支持围栏的环境改用**行尾两空格**；**被压平即交付缺陷，须重发、不得声称已收尾**。
2. 速查件 `报告排版需求.md` 新增第六节「发出去时注意什么（输出保真）」⇒ v1.1。
3. 集群同步：`pc-orchestrator`（AI-015 / AI-016 两处）、`pc-release-clerk`（收尾清单）、`subagent-audit-checklist`、`round-closure.ps1` **v1.7** 人工项、`rules/agent-cluster-frontdoor.md` 第 8 步。
4. skill 副本回同步（`Copied 63 spec files` · `Hash check: all files match the source`）。
5. **本次收尾报告以围栏形式重发**（即本 Change 的产出修复验证）。

**验证**：`verify-cluster.ps1 -Blocking` ⇒ 集群自检 PASS；`gate-runner.ps1` ⇒ DELTA PASS（4 文件 · 0 提示）；`round-closure.ps1 -Version` ⇒ **v1.7**；严格模式收尾校验结论见 R4。

## 八、同轮续段：专家团侧同步（v1.5.0 ⇒ v1.6.0）

**方向与纪律**（沿用 `CHG-20261011-CLUSTER-SYNC`）：源 = 集群 `.codebuddy/agents/`，目标 = WorkBuddy 专家包 `rimworld-mod-studio`；**先备份再改**；只改 `agents/` + `README.md` + `plugin.json`（**不含 `skills/**`**，需用户单独批）；不照搬机器契约行；新写文本不新增 `.codebuddy` 硬引用（条件引用口径）。

| 目标文件 | 改动 |
|---|---|
| `agents/rimworld-studio-lead.md` | ① 门禁设施段 `R1–R8` ⇒ `R1–R9`；② **Phase 6 收尾契约 八项 ⇒ 九项**（补 `R9 台账收敛`）并补**报告排版硬要求 5 条**（行首锚定 / 状态符固定 / 每条 ≤2 行且 ≤20 行 / **并列 ≥3 项必须编号** / **代码块围栏投递**）；③ 协作规则第 10 条同步 |
| `agents/rimworld-qa-release.md` | 收尾齐备项 八项 ⇒ 九项（补 `R9`）并入排版硬要求（编号 + 围栏） |
| `README.md` | 顶部版本说明 ⇒ **v1.6.0**；「开工准备与收尾契约」节升级为九项 + 新增「报告排版硬要求」条；版本历史表加 v1.6.0 行 |
| `.codebuddy-plugin/plugin.json` | `version` 1.5.0 ⇒ **1.6.0**；`description` 的 `R1-R8` ⇒ `R1-R9 … (numbered lists for 3+ items, fenced-block delivery)` |

**验证（替代证据 —— `.workbuddy/**` 对 GATE-DELTA / doc-guard 天然不可见 ⇒ 标 N/A 并给理由）**：

1. **备份**：`..\_backup_rimworld-mod-studio_20261012\`（同级目录；`Test-Path` = True，内含旧版 v1.5.0 文本）。
2. **残留检查**：`R1–R8` 命中 **2 处**，均为**历史行与变更描述**（v1.5.0 版本历史行、v1.6.0 行内「R1–R8 ⇒ R1–R9」），**规范性残留 = 0**。
3. **覆盖检查**：`R1–R9` 命中 **5 处**（lead 2 · qa-release 1 · README 2）。
4. **结构校验**：`plugin.json` 解析 OK，`version=1.6.0`，`agents=8`（与目录内 8 个 agent 文件一致）。
5. **哈希**：`rimworld-studio-lead.md 9AC37F32BC617112` · `rimworld-qa-release.md D46385460E660B89` · `README.md 58EF57952FA4B4FE` · `plugin.json 42BBCB53C34E92A7`（SHA256 前 16 位）。

**未做**：`skills/**` 未动（需用户单独批；既有遗留①「包内 `memory-standard` skill 仍 v1 形态」延续）。台账 `PROG-TOOLGOV/WI-025 ⇒ DONE`（带 Evidence）。

## 九、同轮续段：专家团包内 `memory-standard` 升级 v1 ⇒ v2.1

**背景**：v1.6.0 同步时按纪律**未动** `skills/**`（需用户单独批）。本轮用户指示「升级到 v2.1」。

**比对先行（禁止单向覆盖）**：先做两棵树逐文件比对 —— 专家包 v1 独有 = `SKILL.md` 的四大铁律 / 三方形态声明 / 相邻 skill 边界表 / 落点与环境口径；v2.1 为**超集 + 更强强制**（检测 → 提案 → 差异 → 网关审批 → HMAC 审计链 → 看板）。故**以 v2.1 为底、合并 v1 独有条款**，而非覆盖删除。

| 层 | 处置 |
|---|---|
| **A 类（字节引入）** | `scripts/**` 17 件（含 v2.1 版 `scaffold_day` / `validate_entry` / `visualize_memory`）· `references/{detection,lifecycle,retrieval,security}.md` 新增 · `references/schema.md` 替换 · `tests/test_memory_v21.py` · `README.md` · `RELEASE_NOTES.md` · `VERSION`(2.1.0) · `MANIFEST.sha256.json` |
| **B 类（文本改写）** | `SKILL.md`：v2.1 正文 + 新写「本包形态与部署适配（专家团形态 · 条件引用口径）」—— 三方形态声明（保留）· root 条件引用（工程工作区沿用其约定，否则上游默认）· **默认强审批（网关），口头审批降级须由部署工程用户裁决**（不擅自放宽跨工程资产的安全默认）· 集群联动条件引用 · 字段行机器契约 · 旧条目兼容 · 环境要求 · 文件索引 · 定制件清单 |
| **C 类（不同步）** | `__pycache__`（已清理，不进包） |

**验证（替代证据 —— `.workbuddy/**` 对 DELTA / doc-guard 不可见 ⇒ N/A + 理由）**：

1. **对照实验（关键）**：专家包侧与工程侧**同环境同命令**跑 `PYTHONUTF8=1 python -m unittest discover -s tests -v` ⇒ **两侧同为 `37/39`（failures=1 / errors=1）** ⇒ **本次同步未引入回归**（等价性成立）。
2. **两项失败定性（既有问题，非本轮）**：① `test_expired_short_excluded` 写死 `until='2026-10-10'` ⇒ 创建日期晚于它即被「有效期早于记忆创建日期」校验拒绝 ⇒ **时间炸弹**（2026-10-11 起必失败）；② `test_rejection_does_not_write_memory` 断言 `approval_gateway.decision(…,'reject',…)` 为 `None`，实际返回 reject 决策 dict ⇒ **测试与实现返回语义不一致**（与日期无关）。**上游测试字节保持原样**（否则 `MANIFEST` 失真），已登记 `PROG-TOOLGOV/WI-026`，并把「37/39 及两条原因」写进**两侧 `SKILL.md`**（工程侧原记 39/39 属过期结论，已如实修正）。
3. **MANIFEST 逐件核对**：排除 3 件定制件（`SKILL.md` / `scripts/memory_changes.py` / `scripts/approve_verbal.py`）⇒ **通过 24 件 · BAD 0**；MISS 仅两件上游声明**未纳入**件（`CHATGPT_PROJECT_INSTRUCTIONS.md`、`tools/install_codex.ps1`）。
4. **结构**：`VERSION` = `2.1.0`；包内 5 份 references + 17 脚本 + tests 齐备；`plugin.json` 解析 OK、`version=1.7.0`。
5. **脚本语法**：17 个 `.py` 全部 `py_compile` 通过。

**未做（不伪造）**：① 不改上游测试字节；② `CHATGPT_PROJECT_INSTRUCTIONS.md` / `tools/install_codex.ps1` 不纳入（沿用上游声明口径）；③ 缺陷**只登记未修**（WI-026）。

## 十、同轮续段：修 `memory-standard` 上游自测 2 项（WI-026 关闭）

**批准**：用户对上轮提出的修复回「批准」。采用**改测试文件本身并登记为第 4 件定制件**，而非「改实现迁就测试」。理由：① 时间炸弹只能从测试侧解（写死的是测试自己的日期）；② `reject` 返回决策事件是有用语义（调用方可读决策），改实现会削掉信息；③ 两处**只改与时间 / 实现耦合的写法，原意图不变**。

| 用例 | 原写法（缺陷） | 新写法 | 原意保持 |
|---|---|---|---|
| `test_expired_short_excluded` | 写死 `until='2026-10-10'` + `today=date(2026,10,11)` ⇒ 创建日期晚于 10-10 即被「有效期早于记忆创建日期」拒绝 ⇒ **时间炸弹** | 相对日期：当日创建、`until=明日`、检索 `today=后日` | 「过期短期记忆不进检索」✓（**注**：写成「昨天到期」会被同一校验拒绝，做不到 —— 已验证并写入注释） |
| `test_rejection_does_not_write_memory` | `assertIsNone(decision(…,'reject',…))`，而 `approval_gateway.decision` 的 reject 分支返回**决策事件 dict** ⇒ 恒失败（与日期无关） | `assertIsNotNone(rej)` + `assertEqual(rej['action'],'reject')`；后三行「未写记忆」断言**未动** | 「拒绝不写记忆」✓ |

**验证**：

1. 修复后**两侧同环境**跑 `PYTHONUTF8=1 python -m unittest discover -s tests` ⇒ **`Ran 39 tests … OK`**（工程侧 17.3s / 专家包 19.6s）。
2. 两侧 `tests/test_memory_v21.py` **字节一致**：SHA256 `cf41798dbe3279410fe32d01b6d38f2dcc699e4bfd3e2e668d0fb46b1623b894`。
3. **MANIFEST 复核**：排除 4 件定制件（`SKILL.md` / `scripts/memory_changes.py` / `scripts/approve_verbal.py` / `tests/test_memory_v21.py`）⇒ **通过 23 · BAD 0**；MISS 仅两件上游声明未纳入件。
4. 两侧 `SKILL.md` 同步登记：实测记录改为「10-09 `39/39` → 10-12 首跑 `37/39`（同环境对照证明非引入）→ **同日修复后 `39/39`**」；定制件清单 **3 ⇒ 4 件**。
5. 台账 `PROG-TOOLGOV/WI-026 ⇒ DONE`（带 Evidence）。

**代价与残留（如实）**：MANIFEST 可校验件由 24 ⇒ **23**；上游 `tests` 字节不再原样（已登记为定制件；如需复原，可用备份 `_backup_rimworld-mod-studio_20261012\` 内原文件对照）。

## 十一、同轮续段：行宽与折行（防横向滚动）

**现象**：用户截图 —— 带围栏的收尾报告在聊天窗内**需要左右拉动**才能看完（长行超出容器宽度）。

**根因**：**代码块不自动折行**（等宽 + 保留空白）。前一轮为解决「裸行被压平」强制围栏，围栏解决了换行、却引入**横滚**：`R4` 标准行与 `R6` 明细的视觉宽超过容器 ⇒ 必然溢出。**两条要求必须同时满足**：既不压平（裸行），也不横滚（超长行）。

**修法**：

1. 正本 §15 新增**规则 9**、§16.1b 新增**规则 10**：每行**视觉宽 ≤72**（中文 / 全角**按 2 计**）；代码块不折行 ⇒ **先砍措辞，砍不动才主动折行**（续行缩进 4 空格）；**折行不计入「每段 ≤2 行」**（逻辑行 ≠ 物理行）。
2. 速查件新增「投递（防横滚 / 防压平）」三条（第 12–14 条）。
3. 集群同步：`pc-orchestrator`（AI-015 / AI-016 两处）· `pc-release-clerk`（收尾清单）· `subagent-audit-checklist` · `round-closure.ps1` **v1.8** 人工项 · `toolchain-registry.md`。
4. skill 副本回同步：`Copied 63 spec files` · `Hash check: all files match the source` · 规则引用 lint 通过（230 个 ID）。
5. **本次收尾报告即按新样式发出**（生产验证）。

**验证**：`verify-cluster.ps1 -Blocking` ⇒ 集群自检 PASS（PASS=7 / BLOCKED=0 · Gate 10/10 · skill 10/10 · 路由回归 23/0）；`round-closure.ps1 -Version` ⇒ **v1.8**；严格模式 `收尾结论` 见 R4。

## 十二、同轮续段：编号逐行（禁止同行内联）

**要求**：用户「每个编号进行换行」—— `1.` `2.` `3.` 各类清单**每项独占一行**，不得 `1. … 2. … 3. …` 同行内联。

**修法**：

1. 正本 §15 **规则 4**、§16.1b **规则 8**：加「**每项独占一行**」并明写「**禁止同行内联**」；把「步骤」与「清单」彻底分开 —— **步骤用 `→` 串联、不编号**（§15 骨架 ② 做法回退为箭头串联）。
2. **行数上限相应放宽**（逐行必然占行）：§15 **≤15 ⇒ ≤18**；§16.1b 规则 3 **≤20 ⇒ ≤26**（`R3` 七门各占一行）。
3. 骨架与示例同步：§15 ② 做法、§16.1b 的 `R3` / `R5` / `R6` 明细、速查件模板与填写示例**全部改为逐行**。
4. 速查件第 2 / 6 条更新 ⇒ **v1.2**（变更历史已登记）。
5. 集群同步：`pc-orchestrator`（AI-015 **并更新 15 ⇒ 18**）· `pc-release-clerk`（**20 ⇒ 26**）· `subagent-audit-checklist` · `round-closure.ps1` **v1.9** 人工项 · `toolchain-registry.md`。
6. skill 副本回同步：`Copied 63 spec files` · `Hash check: all files match the source` · 规则引用 lint 通过（230 ID）。

**验证**：集群自检 PASS（PASS=7 / BLOCKED=0 · Gate 10/10 · skill 10/10 · 路由回归 23/0）；`round-closure.ps1 -Version` ⇒ **v1.9**；本次收尾报告**以逐行编号形式重发**。

**副作用（如实登记）**：报告整体**变长**（`R3` 七门由 2 行 ⇒ 7 行），换来的是**每行更短、无横滚、可逐项扫**；行数上限 18 / 26 即是对该权衡的显式登记。

## 十三、本 Change 跨仓产出一览

| 仓 | 文件数 | 说明 |
|---|---|---|
| 本仓（PersonalChronicle） | 13 | 规范 1 · 新文档 1 · 索引 2 · 集群 6 · skill 副本 1 · 证据 2 |
| 跨仓（WorkBuddy 专家包） | 30 | v1.6.0 段 4 件 + v1.7.0 段 26 件（scripts 17 · references 5 · tests 1 · 元数据 4，含 README/VERSION/MANIFEST/RELEASE_NOTES 中的变更项） |

> 本变更由 AI 助手实施：CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-12。
