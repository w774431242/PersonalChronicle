---
change_id: CHG-20261012-LONGPROJ-LEDGER
version: v3.0
date: 2026-10-12
author: AI 助手（CodeBuddy · DeepSeek-V4.1-Flash）
scope: [docs/设计文档/架构/（规范文本：AI-017 新增节 + 规则编号索引）]
scope-paths: [docs/设计文档/架构/]
title: 长项目续航机制 —— AI-017 跨轮台账 + 波次收敛（M9）
state: Implemented
layer: 工程治理设施（工具区 `.codebuddy/**` + 规范文本 `docs/设计文档/架构/`）；**无 Source / Defs / Patches / Languages / About / Textures 改动**
files:
  - path: docs/设计文档/架构/07-AI开发规范.md
    type: M
    symbols: §17 AI-017 长项目台账与波次协议（唯一台账 / 硬不变量 / 轮次协议 / M9 收敛判据 / 波次与反碎片化 / Gate 关联）
    lines: 309-356
    note: 新增 §17 全节；未改动 §1–§16 既有条款（§16.3 保持原样）
  - path: docs/设计文档/架构/00-规则编号索引.md
    type: M
    symbols: 规则编号表「AI 执行协议」行 —— 区间 AI-001–AI-016 ⇒ AI-001–AI-017
    lines: 32
    note: DOC-006 索引义务（新增规则 ID 须登记区间）
---

# 变更标注 · CHG-20261012-LONGPROJ-LEDGER

## 一、用户需求

「agent 集群 bug 问题：无法进行长项目，每次都会留下大批量任务，每次都是 100w token 左右的小问题，如何解决？」
经用户确认「按你推荐进行，能够解决当前问题的推荐」后实施。

## 二、根因与证据（本仓实测，非推断）

| 证据 | 实测值 | 含义 |
|---|---|---|
| Change ID 碎片化 | `.codebuddy/audit/gate-records/` **131 份 / 66 个不同 CHG-ID**（09 月 45 份；10 月 12 天 86 份 ≈ 7 份/天） | 交付单元被打碎；单日单项目 `ECON-CONTRACTIZE` 派生 `-B1/-B2/-HANDOFF/-W-ALLSEATS/-W-PERF-TEST` 多个 ID，各自跑全套十道 Gate + 标注 |
| 遗留项无落点 | 收尾回复留档仅 `.codebuddy/tmp/round-*.md` **4 份**；`change-context.json` 为**单槽**（只记当前 1 个 changeId） | R6「风险与遗留」只活在对话里，上下文重置即蒸发 |
| 无脚本读上一轮 | 全仓检索 `台账/backlog` 与「读取上一轮产物」⇒ **0 命中**；`round-closure.ps1` 只写 JSON 证据、无人读 | 下一轮靠**重扫全库**重新发现同一批小问题 ⇒ 同一问题反复计价 |
| 计划区 orphan | `.codebuddy/plans/` 4 份一次性方案，全仓仅 1 处历史引用 | 长项目根本没有权威台账 |
| 未提交积压（本轮实测） | `git status --porcelain -uall` = **2412 条**（其中 docs 域 1241 条） | 「大批量遗留」的量化现状 |

**根因链**：交付原子被默认为「一轮对话」⇒ 治理固定开销（前置路由 + 集群体检 + 十道 Gate 派发 + 变更标注 + gate-runner + round-closure + doc-guard + R1–R8）按轮次线性计价 ⇒ 单轮预算大部分被治理税吃掉 ⇒ 只剩做小问题的余量；而遗留项没有台账 ⇒ 下一轮重新发现、再次计价 ⇒ 永不收敛。

## 三、本次交付（规范侧，进入 GATE-DELTA 判定）

| 层 | 改动 |
|---|---|
| 规范 AI-017 | `07-AI开发规范.md` §17：唯一台账落点与唯一写入口；硬不变量（新增必带 `-Assertion`、`DONE` 必带 `-Evidence`、`DROPPED` 必带 `-Note`）；轮次协议（开工读 / 收尾写）；**M9 收敛判据**（`剩余 before → after` 必须下降或归零，否则需 `-LedgerWaiver` 书面理由，否则 BLOCKED）；波次与反碎片化（同 program 同波次复用一个 ChangeID）；归属不变（总控执行、`pc-release-clerk` 核验、不建第 8 个 agent） |
| 规则 frontdoor | `.codebuddy/rules/agent-cluster-frontdoor.md`：触发范围增「长项目轮次」；前置协议增第 9 步（一键收尾链）；新增「长项目轮次协议（AI-017）」7 条硬条款；禁区增 4 条（不得跳过台账读写 / 不得手工编辑 JSON / 不得同波次另建 ChangeID / 不得为让 M9 变绿而伪造 `DONE`·`DROPPED`） |
| 编号索引 | `00-规则编号索引.md`：AI 域区间更新为 `AI-001–AI-017` |

## 四、本次交付（工程工具区，`.codebuddy/**` 属 git 忽略区 ⇒ **不入 GATE-DELTA 判定**，仅在此正文登记）

| 文件 | 类型 | 作用 |
|---|---|---|
| `.codebuddy/audit/program-ledger.ps1` | A | 台账工具（`-Init/-Add/-Set/-List/-Snapshot/-Delta`）；唯一写入入口；原子替换 + 独占锁（单写者纪律）；硬不变量在写入侧强制 |
| `.codebuddy/audit/program-ledger.json` | A | 台账数据（SSOT）。本轮灌入 `PROG-ECON` 16 项、`PROG-DOCGOV` 3 项，全部带 `-Assertion` 与 `-Source` 溯源 |
| `.codebuddy/audit/round-closure.ps1` | M | 增 **M9** 长项目台账收敛（`-Program` / `-LedgerWaiver`）；v1.1 ⇒ v1.2；未指定 `-Program` 时 `M9 = N/A`（向后兼容） |
| `.codebuddy/audit/change-context.ps1` | M | 项目槽：`-Set -Program/-Wave` 写入并保留上一轮值；`-Validate` 增「program 必须在台账中真实存在」；`-ShowCommands` 增收尾链命令 |
| `.codebuddy/audit/close-round.ps1` | A | 一键收尾链：①集群体检 ②收尾齐备性(含 M9) ③FINALIZE ④台账基线刷新；④ 为唯一写盘动作且**仅在 ② 非 BLOCKED 时执行** |
| `.codebuddy/audit/toolchain-registry.md` | M | 登记 2 个新工具 + 补登记 `round-closure.ps1`（此前漏登）；`pc-orchestrator` / `pc-release-clerk` 工具映射更新 |

## 五、硬不变量与自检证据（可复现）

自检为**分档台账**（`-LedgerPath .codebuddy/tmp/st2.json`），**不污染正式 SSOT**；脚本与输出：`.codebuddy/tmp/selftest-ledger-v11.ps1`。

**5.1 正向 / 异常用例矩阵（v1.1，20 例全命中期望）**

| 用例 | 期望 | 实测（`台账结论:` 摘要） |
|---|---|---|
| 未知参数 `-Progarm` | BLOCKED | `未知参数：-Progarm X —— 参数拼错不得静默忽略` |
| 初始化（不再写空基线） | PASS | `已初始化 T · 波次 W1 · 剩余 0 · 接着 -Add 灌项…` |
| 空台账写基线 | BLOCKED | `T 无任何工作项 ⇒ 拒绝写基线` |
| 新增缺 `-Assertion` | BLOCKED | `-Add 需要 -Assertion…` |
| 手工 `-Id` 重复 | BLOCKED | `工作项 ID 重复：WI-001` |
| `-Set` 工作项不存在 | BLOCKED | `工作项不存在：T / WI-999` |
| 无快照判收敛 | BLOCKED | `台账未收敛：T: 缺开工快照` |
| 空转（before=after=2） | BLOCKED | `台账未收敛：T: 2→2 未下降` |
| 空转 + `-Waiver` | 放行 | `PASS WITH WAIVER（…理由：…）` |
| `DONE` 缺 `-Evidence` | BLOCKED | `DONE 必须带 -Evidence…` |
| `DROPPED` 缺 `-Note` | BLOCKED | `DROPPED 必须带 -Note…` |
| 非法 `-Status FIXED` | BLOCKED | `非法 -Status：FIXED（合法：…）` |
| 关闭一项后 | 收敛 PASS | `PASS（T 本轮收敛 · 共关闭 1 项）` |
| `-Init -Force` 重置含项程序无理由 | BLOCKED | `-Force 重置含 2 个工作项的程序，必须带 -Note…` |
| `-Init -Force` 带理由 | 放行 | `已初始化 T …`（旧台账已备份 `.bak`） |
| 重置后空项判收敛 | BLOCKED | `T: 工作项为 0（不可判定）` |
| `-Delta -Program ALL` 且台账无 program | BLOCKED | `无项目可判定（-Program ALL 未匹配任何项目…）` |
| `round-closure` **不传** `-Program`（change-context 已挂槽） | M9 从项目槽回落，非 N/A | `M9 … BLOCKED：PROG-ECON｜台账未收敛：PROG-ECON: 0→16 未下降` |
| `round-closure` 未知参数 | BLOCKED | `收尾结论: BLOCKED（… BLOCKED=2 …）` |
| `change-context` 未知参数 | FAIL | `CONTEXT: FAIL —— 未知参数：-Progarm X` |

**5.2 集群体检 / 路由回归（派发前置门禁）**

| 项 | 实测 |
|---|---|
| `verify-cluster.ps1 -Blocking` | `集群自检: PASS (runId=7f8373e67527406289c82131e6b3ad39)`；`结论: PASS (PASS=7 / BLOCKED=0 / Gate覆盖=10/10 / skill覆盖=10/10)` |
| `audit-subagents.ps1 -Blocking` | `结论: PASS  (PASS=7 / BLOCKED=0 / Gate覆盖=10/10 / skill覆盖=10/10)` |
| `route-task.tests.ps1` | `路由回归: PASS=23 / FAIL=0` |
| 四脚本语法解析 | `program-ledger.ps1 / round-closure.ps1 / change-context.ps1 / close-round.ps1` 均 `PARSE OK` |

**5.3 四席 Gate 抓出的缺陷与处置（本轮最有价值的部分）**

四席（`pc-orchestrator` / `pc-data-steward` / `pc-perf-tester` / `pc-release-clerk`）共报 **8 条 fail-open 通道**，全部在本轮修掉并加自检钉住：

| # | 缺陷（原文口径） | 处置 |
|---|---|---|
| 1 | 不传 `-Program` ⇒ M9 静默 N/A ⇒ 收尾门被绕过（B1） | `round-closure` M9 回落读 `change-context.json.program`；自检第 18 例钉住 |
| 2 | 空 program / `programs` 被清空 ⇒ `after==0` 判 CONVERGED（真空绿） | 新增 `NO_ITEMS` 与「无项目可判定」两态，均 BLOCKED |
| 3 | `-Init -Force` 静默清空全部工作项 ⇒ 直接满足收敛 | 含项重置必须带 `-Note`（理由）+ 强制写 `.bak` |
| 4 | `-Init` 内写「开工快照」⇒ 得到 `items:[] / remaining:0` 的错误基线 | 移除；基线一律由 `-Snapshot` / `close-round` ④ 在灌项后写；空台账写基线直接拒绝 |
| 5 | `-Snapshot` 未纳入独占锁 ⇒ 与 `-Add` 并发时基线错位 | 纳入锁 |
| 6 | 锁无 TTL / 无 PID ⇒ 进程被杀后残留锁使写盘全停 | 锁内写 PID + 陈旧锁（>120s 或持锁进程已退出）自动回收 |
| 7 | 锁内 `throw` ⇒ 无结论行 + exit=1，违背「结论行固定」契约 | 改为软失败 `Fail`，统一并入末尾结论行 |
| 8 | 无 `[CmdletBinding()]` ⇒ 参数拼错静默忽略（如 `-Progarm` ⇒ M9 静默降级） | 四脚本加 `$args` 非空即阻断 |
| 9 | `close-round` ④ 在 ② 结论行异常（FAIL）时仍刷新基线 | 判据改为「② 非 PASS 一律不刷新」 |
| 10 | `change-context -Set` 白名单重建 ⇒ 未知字段静默蒸发（并行会话用旧版即抹掉项目槽） | 改为「读旧 → 合并 → 写」 |
| 11 | `change-context -Validate` 用裸文本正则判 program 存在 ⇒ 注释/字段值可误通过 | 改为解析 JSON 后按 `programs[].id` 精确比对 |
| 12 | `round-closure` M9 无结论行唯一性断言 | 补 `Count -ne 1 ⇒ BLOCKED` + 行首锚定三态正则（WAIVER 判定置于 PASS 之前） |
| 13 | `close-round` 向无 `-Root` 形参的 `verify-cluster` 传 `-Root`（被静默吞） | 移除 |
| 14 | doc-guard 在一次收尾里被扫两遍（≈ 链耗时 33%） | ③ 对 finalize 传 `-SkipDocGuard`（证据取 ② 的 M7） |
| 15 | **参数位置绑定错绑**（`close-round` 首跑实测暴露，非四席预判）：`-Artifact a b` 的多出值被当**位置参数**绑到第一个形参 `$Root` ⇒ 整条链在错误根目录上跑，且 `$args` 为空、无任何报错（现象伪装成「标注不存在」） | 四脚本加 `[CmdletBinding(PositionalBinding = $false)]`（位置参数直接触发绑定错误，loud fail）；多值须用 `-Artifact a,b` 单 token；`close-round` 透传改为 join 单 token |

**5.4 收尾链实测（唯一权威结论行）**

```
① 集群体检        : PASS —— 集群自检: PASS (runId=847c4dd8ec2c47ebb1973e3d48d97768)
② 收尾齐备性      : PASS —— 收尾结论: PASS（机器项 通过=5 / N/A=1 / WARN=2 / BLOCKED=0 · 人工项 8 项）
   M9 = WARN —— PROG-ECON 未收敛但已给理由（台账建立轮）｜[台账] PROG-ECON：剩余 0 → 16（Δ16 · 本轮关闭 0 项）
   M7 = WARN —— doc-guard BLOCKED（既有状态，非本轮引入）
③ 终检 FINALIZE   : PASS —— FINALIZE: PASS（④ GATE-DELTA: PASS — 覆盖 2 个改动文件，无遗漏/超范围/残留）
④ 台账基线刷新    : PASS —— 台账结论: PASS（PROG-ECON 基线已写 · 剩余 16）
收尾链结论: PASS（跳过 0 步）
```

⇒ **基线已真刷新**（`PROG-ECON-before.json.remaining = 16`），满足 `pc-release-clerk` 对 waiver 的「一次性 + 基线必须真刷新」两项有效条件；下一轮 `-Delta` 必须严格下降，否则按 M9 语义 BLOCKED。

**实现期实测踩到的 PowerShell 陷阱（已内联注释固化，防复发）**：
1. 函数形参不得用 `$pid`（内置只读自动变量，赋值即抛）；
2. **参数不得命名为 `$Ledger`** —— PowerShell 变量名**大小写不敏感**，与数据变量 `$ledger` 同体，`[string]$Ledger` 会把 hashtable 强制转成字符串 `"System.Collections.Hashtable"`，表现为恒假「程序不存在」（静默失败，不报错）；
3. 同名陷阱之三：局部变量 `$delta` 与 `[switch]$Delta` 同体 ⇒ `Cannot convert Int32 to SwitchParameter`；
4. `switch` 会把 `$_` 改成「被测试的值」⇒ 在 `switch` 内引用外层管道行会得到空值（已在 `-Delta` 明细里先 `$rowItem = $_`）；
5. 结论行统一由文件末尾输出（避免分支内先输出一次、末尾再输出一次，形成重复结论行）。

## 六、明确未做 / 边界（不得当作已完成）

- **M9 只保证「遗留项数量在减少」，不保证「减对」**：`-Assertion` / `-Evidence` 仍是自由文本、只校验非空不校验内容，仍需人工复核（与 AI-016 M8「出现 ≠ 内容合格」同源）。`pc-data-steward` 判为 2 条独立缺陷（P2-2）。
- **本轮 waiver 的裁定（`pc-release-clerk`）**：属**如实登记**而非绕过 —— 建立轮首次枚举必然 0→16，M9 判 **WARN**（非 PASS），规范侧逐字披露未收敛，台账 19 项无一是 `DONE`/`DROPPED`（未用豁免压低剩余数）。**限制条件**：① 一次性，任一 program 连续两轮 waiver ⇒ 升级为显式用户裁决；② 基线必须真刷新（本段完成）；③ 下一轮必须严格下降。
- **本轮为台账建立轮，不是收敛轮**：`PROG-ECON` 16 项 / `PROG-DOCGOV` 3 项全部 `OPEN`；收敛判定自**下一轮**起生效。
- **未能完成的 P0/P1（已登记为台账项，不谎报）**：
  - `program-ledger.json` / 快照 / `change-context.json` 三处**读取端仍不做 `schema` 判别**（`pc-data-steward` 判 DATA/SAVE = BLOCKED）：字段改名会静默假收敛或永久 BLOCKED；`schema` 当前形同注释。
  - `-Add` **不幂等**（同参数重跑生成新 ID ⇒ 重试即重复计价）、键序非确定（Hashtable）、快照非纯派生（不可从正文回算）、基线落 `.codebuddy/tmp`（易失）。
  - `close-round` ④ 的刷新路径无锁无版本，属旁路可覆盖点。
  - **波次（Wave）仅为协议约定、无机器强制**（`pc-orchestrator` 裁定「需补最小机器强制」）：建议堵「同波次多 ChangeID」与「wave 双写」（`change-context.wave` vs `program-ledger.wave`）。
- **规范双副本已同步（DOC-009 已闭环）**：`sync_from_docs.ps1 -Source docs/设计文档/架构` ⇒ `Copied 62 spec files · Hash check: all files match the source · Rule-reference lint: all references resolve (230 defined IDs)`，AI-017 已进入 skill `references/`（运行时 `use_skill` 可见）。
  **但比对纪律只做了一半（如实登记）**：同步前仅对 `07-AI开发规范.md` 做了「skill 侧独有行 = 0」的双头比对，其余 61 个文件未逐一比对（依赖「skill `references/` 为主体副本的逐字节副本」这一既有口径）。已登记为 `PROG-TOOLGOV` 的同步纪律项。
- `.codebuddy/plans/` 未复活、未迁移：台账落在 `.codebuddy/audit/`（与 `change-context.json` / `doc-baseline.json` 同域），`plans/` 维持 orphan 现状（`pc-orchestrator` 记 P2-4「第二真相源并存」）。
- `doc-guard` 当前 `BLOCKED`（C3 11 条 / C7 越基线修改 / 新增 135）属**既有状态**（11 条 C3 全在 `docs/参考资料/DLC接口参考包_20261011/` 与 `docs/验证/…`，本轮未动），已作为 `PROG-DOCGOV` 台账项登记，不谎报收口。
- `docs/设计文档/架构/07-AI开发规范.md` 版本行**有意不升**（仍 v3.0）：升版会与 skill 侧 `references/` 副本产生版本字符串漂移。
- **范围外清单粒度问题（`pc-release-clerk`）**：`out-of-scope.txt` 为 2411 条**文件粒度**豁免，使 DELTA 退化为「新文件探测器」，对**存量脏文件的二次篡改零鉴别力**（同目录 `docs/设计文档/架构/` 下 100+ 文件被豁免）。建议改**目录粒度**豁免（已登记为台账项）。
- 未做：`program-ledger` 的 HTML 看板 / 图表；跨机迁移（台账为单机文件）。

## 七、GATE-DELTA / 回填

- **非本轮产物（具名登记，不认领、不修改）**：`Tests/LaborPresentationTests.cs`（**未跟踪新文件，mtime 20:26:31，晚于范围外清单生成时刻 20:26:27 ⇒ 并行会话在途产物**）、`docs/README.md`（mtime 20:12:34）、以及 `git status` 中全部 2411 条历史积压。均属既有/并行状态，**非本轮引入**。
- **GATE-DELTA 结论**：见收尾回复 R4 段（由 `close-round.ps1` 输出的实测行，本文不预先声称 PASS —— 上一稿此处写过未执行的 PASS，已按 GOV-012 撤回）。
- `收尾结论:` / `收尾链结论:` 见收尾回复 R3/R4 段。
- 变更标注自身不进入 `files:`（GATE-DELTA 会把标注从改动集移除，列入必然 phantom）。
- 工具侧交付物（`.codebuddy/**`）属 **git 忽略区**，不进 `files:`、不进 GATE-DELTA 判定，仅在本标注 §4 正文登记（并已在 `toolchain-registry.md` 登记）。

---

署名：本变更由 AI 助手（CodeBuddy · DeepSeek-V4.1-Flash）于 2026-10-12 实施并生成全部交付物。

> ⚠ 署名行刻意**不加粗**：`round-closure` M6 的行级署名判据要求 `由 AI 助手` 连续出现；写成 `由 **AI 助手**` 会被 `**` 隔断而漏判（2026-10-12 `pc-perf-tester` 实测抓出）。
