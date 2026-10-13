---
change_id: CHG-20261012-TOOLGOV-W1
version: v3.0
date: 2026-10-12
author: AI 助手（CodeBuddy · DeepSeek-V4.1-Flash）
scope: [docs/设计文档/架构/（规范文本：AI-016 收尾契约扩到 R1–R9 + 固定可视化骨架 + AI-017 反绕过清单）]
scope-paths: [docs/设计文档/架构/]
title: 收尾报告可视化骨架（R1–R9）+ 台账反绕过机器强制（PROG-TOOLGOV/W1）
state: Implemented
layer: 工程治理设施（工具区 `.codebuddy/**` + 规范文本 `docs/设计文档/架构/`）；**无 Source / Defs / Patches / Languages / About / Textures 改动**
files:
  - path: docs/设计文档/架构/07-AI开发规范.md
    type: M
    symbols: §16.1 八项 ⇒ 九项必输出（新增 R9 台账收敛）/ §16.1b 固定可视化骨架（新增）/ §16.3 Gate 关联（R9 同源）/ §17.6 反绕过机器强制清单（新增）
    lines: 278-395
    note: 用户反馈「R1–R9 报告样式不够直观可视」⇒ 定骨架（首末 ━━ 规则行 + R<n> 行首锚定 + 状态符 + 每条 ≤2 行 + 整份 ≤20 行），与 AI-015 开工报告同构
---

# 变更标注 · CHG-20261012-TOOLGOV-W1

## 一、用户需求

「按你推荐修复。新发现问题：R1–R9 报告的样式不够直观可视」——
即：① 把上一轮交付时列出的待修项按推荐就地修掉；② 给每轮收尾报告定一套一眼可扫的排版。

## 二、本轮交付（规范侧，进 GATE-DELTA 判定）

| 层 | 改动 |
|---|---|
| AI-016 §16.1 | 必输出项 **R1–R8 ⇒ R1–R9**：新增 `R9 台账收敛`（before → after + 本轮关闭项；非长项目轮次写 `N/A` 并给理由）。动机：收敛证据此前只存在于 `round-closure` 的 M9 行，收尾回复不强制复述 ⇒ 读报告的人看不到「遗留降没降」 |
| AI-016 §16.1b（新） | **固定可视化骨架**（照抄骨架 + **7 条**排版硬规则）：首末 `━━` 规则行；`R<n>` 行首；状态符固定 `✅/⚠/⛔/▸/→/└`；每条 ≤2 行、**正文骨架 ≤20 行**（空行与证据附录不计）；**R3 先给一行汇总再给明细**；**R6 必须给数字与去向**；R1/R5/R6/R9 面向用户、禁内部术语。**并如实标注两处强度边界**：① `-Transcript` 实为**词边界**正则（非行首锚定）⇒ 机器只能排「完全缺项」，骨架合规是**人工项**（已列入脚本人工清单）；② `M9=WARN`/`M9=N/A` 两分支的 R9 同源判据**纯人工**，且机器分不清「漏挂项目槽」与「真非长项目」 |
| AI-016 §16.3 | `R9` 同源判据：缺失或与 `M9` 结论行不一致（台帐 BLOCKED 而 R9 写「收敛」）按 `AI-011` 的 `P1` 处理（伪造收敛 = 伪造 PASS） |
| AI-017 §17.6（新） | **反绕过机器强制清单** 10 条（把首版只有约定的协议补成有牙齿的强制项），并显式列出**仍未机器化**的 2 项（目录粒度范围外清单、HTML 看板），禁止声称已具备 |
| 规则 frontdoor | 前置协议第 8 步改为「按 `R1–R9` + 照抄 §16.1b 骨架」；长项目协议第 4/5 条更新为「`-WaiverCode` 必填枚举 + 连续两轮同 code 升级用户裁决」「波次唯一权威源 = 台账 + 同波次唯一 ChangeID」 |

## 三、本轮交付（工程工具区，`.codebuddy/**` 属 git 忽略区 ⇒ **不入 GATE-DELTA 判定**）

| 文件 | 类型 | 改动 |
|---|---|---|
| `.codebuddy/audit/program-ledger.ps1` | M（v1.1 ⇒ v1.2） | ① `Get-Ledger`/快照读取加 **schema 判别**（不符即 BLOCKED，禁静默继续）；② `-Add` **幂等**（同 title+assertion 命中原项）；③ `ConvertTo-StableNode` **键序确定化**（同数据 ⇒ 同字节）；④ 快照带 `ledgerUpdatedAt/itemCount/itemsHash` **指纹**，`-Delta` 交叉校验（篡改 ⇒ `SNAP_TAMPERED`）；⑤ `-Waiver` 必须配 **`-WaiverCode` 枚举**，**连续两轮同 code ⇒ BLOCKED**，收敛轮清零；⑥ 新增 `-Advance`（切波次）；⑦ JSON 损坏/锁内 `throw` 一律转为结论行 |
| `.codebuddy/audit/change-context.ps1` | M | ① **波次唯一权威源 = 台账**（`-Set` 只读校验，不一致直接 FAIL；切波次用 `-Advance`）；② **同 `(program,wave)` 唯一 ChangeID**（含升级当轮的**隐式历史**，使机制当轮即生效；逃生口 `-ForceNewChange "<理由>"`）；③ 台账中不存在的 program 直接拒绝（防空挂槽）；④ `-Validate` 新增**清单陈旧守卫**（条目在清单生成后被改动 ⇒ FAIL）；⑤ `-GenScope` 登记 `outOfScopeCount` 并输出逐轮趋势；⑥ `Save-Context -Depth 5 ⇒ 8`（history 加深后 -Depth 5 已在临界） |
| `.codebuddy/audit/round-closure.ps1` | M（v1.2 ⇒ v1.3） | `$rItems` 与 M8 核验循环 **8 ⇒ 9**（R9）；header 注明「R1–R9 必须按 §16.1b 骨架输出，`-Transcript` 只数字样」 |
| `.codebuddy/rules/agent-cluster-frontdoor.md` | M | 见上表 |

## 四、验证证据（可复现）

**4.1 自检矩阵**（`.codebuddy/tmp/selftest-ledger-v12.ps1`，分档台账，不污染 SSOT；**22 例全命中期望**）

| 组 | 用例 → 实测 |
|---|---|
| A schema | `schema=2` ⇒ `BLOCKED（台账 schema 不符：文件=2 / 本工具支持=1）`；文件损坏 ⇒ 有结论行的 `BLOCKED` |
| B 幂等/键序/指纹 | 同 title+assertion 重跑 ⇒ `幂等命中，未新增`（项数实测 = 2）；项内键序实测 `assertion,depends,evidence,gate,id,note,source,status,title,updatedAt,wave`（已排序）；篡改快照 ⇒ `BLOCKED（快照指纹不符）` |
| C Waiver | 缺 code ⇒ `BLOCKED`；首轮 `FIRST_BASELINE` ⇒ `PASS WITH WAIVER`；**连续第二轮同 code ⇒ `BLOCKED`（须升级用户裁决）**；改用 `USER_RULED` ⇒ 放行 |
| D 波次/唯一 ChangeID | 波次≠台账 ⇒ `FAIL`；台账无此 program ⇒ `FAIL`；同 (PROG-ECON,W1) 第二个 ChangeID ⇒ `FAIL（已存在 CHG-20261012-LONGPROJ-LEDGER）`；`-ForceNewChange` ⇒ 放行；测后已还原正式 `change-context.json`（Show 复核无污染） |
| E 切波次 | `-Advance W2` ⇒ PASS；重复切 ⇒ `已是 W2，无需切换`；缺 `-Wave` ⇒ `BLOCKED` |

**4.2 上轮已具证据（本轮沿用）**：`集群自检: PASS`；`PASS=7 / Gate覆盖=10/10`；`路由回归: PASS=23 / FAIL=0`；四脚本 `PARSE OK`。

**4.3 本轮台账收敛（M9）**

```
[台账] PROG-TOOLGOV：剩余 9 → 8（Δ-1 · 本轮关闭 7 项）
```

口径如实说明：**关闭 7 项、同轮新增 6 项**（第三轮三席审计发现的新缺口，见 §4.4，未静默吞掉）⇒ 净 −1。
本轮**不是** waiver 轮（未使用 `-LedgerWaiver`），收敛由真实净下降达成。

**4.4 第三轮三席审计抓出的缺陷与处置（含我本轮的 P0 回归）**

| # | 缺陷（原文口径） | 处置 |
|---|---|---|
| R1 | **P0 回归（我引入）**：`round-closure` / `close-round` 只传 `-Waiver` 不传 `-WaiverCode` ⇒ 台账侧恒判「缺 code」⇒ **收尾链合法豁免出口被关死** | 两脚本补 `-LedgerWaiverCode` 参数并透传；静态核对两处调用点（`round-closure:329` / `close-round:127`） |
| R2 | **P0**：快照指纹只覆盖 `(id=status)` ⇒ **只改 `remaining` 即可伪造收敛** | 指纹改为覆盖 `remaining + itemCount + (id=status)`；自检 F4/F5 |
| R3 | **P0**：`-Init` 在台账不可读/schema 不符时用空骨架继续 ⇒ **整档覆盖且无备份** | 拒绝执行（`$script:ledgerReadFailure` + 文件存在即拒）；自检 G1/G2（原文件字节未变） |
| R4 | P1：完整性告警**可被 `-Waiver` 消音**（篡改被一句理由抹平） | `SNAP_*` 独立为**不可豁免类**；自检 I1 |
| R5 | P1：豁免连击只比 `lastCode` ⇒ **交替两个 code 可无限规避**；且连击状态落 `tmp`（跨轮事实放易失目录） | 改为「上一轮豁免过即升级」（任意 code，`USER_RULED` 例外）；豁免态迁至**与台账同目录**（`.codebuddy/audit/program-ledger.waivers.json`）并加 schema/kind 判别 + 独占锁 |
| R6 | P1：`-Delta` 已非只读（写豁免态）但**不在锁内**（并发丢更新） | 读-改-写包进 `Invoke-WithLock` |
| R7 | P1：`change-context.json` 同样是「版本号只写不判」；陈旧守卫对**目录条目必然误报** | 补 schema 判别；陈旧守卫跳过目录条目 |
| R8 | P2：`R1–R8` 残留 6 处（§16.2 / round-closure `.DESCRIPTION` / frontdoor 禁区 / `subagent-audit-checklist` 4 处 / close-round 注释 / 工具登记册） | 全部改 `R1–R9`；登记册补版本与能力（含豁免态文件登记） |
| R9 | P2：版本真源三处不一致（脚本 tool 串 v1.1 ↔ 标注 v1.2 ↔ registry 无版本） | 脚本 tool 串统一 `v1.2`；registry 三行补版本；**根本治理**（单一 VERSION 常量）登记为 `WI-012` 未做 |
| R10 | P2：`WI-001` / `WI-003` 的 `-Evidence` **过度声明**（引证错位、范围夸大） | 两条 Evidence 已按实际实现重写（覆盖三读取点 / 指纹含 remaining） |

**4.5 计数口径声明**：§4.1 的「22 例」= 带 A–E 编号的断言用例数（A2+B9+C5+D4+E3=23 行，其中 C5 为 setup、D 组有一行是「已还原」复核）⇒ 断言数按 **22** 计。

## 五、明确未做 / 边界（不得当作已完成）

- **`R1–R9` 骨架：机器不判排版合规**。`round-closure -Transcript` 用的是**词边界**正则（非行首锚定），连行内提及与 `R1–R9` 区间写法都会命中 ⇒ 只能排除「完全缺项」，**证伪力≈0**；骨架合规已列入脚本人工项清单，仍未机器化。
- **`R9` ↔ `M9` 无机器比对**（`WI-010`）：`M9=WARN`/`N/A` 两分支的「一致性」无判据；机器分不清「长项目漏挂项目槽」与「真非长项目」。
- **范围外清单仍为文件粒度**（`WI-011`）：对被豁免文件的二次篡改零鉴别力；陈旧守卫只对**文件**生效、**已删除条目漏报**，且**重跑 `-GenScope` 即变绿 ⇒ 是减速带而非门禁**。
- **版本真源仍靠人肉一致**（`WI-012`）：本轮只把三处口径**改齐**，未引入单一 VERSION 常量。
- **快照仍不可从正文回算**（`WI-013`）：无状态变迁日志、原地覆盖、且落在 `.codebuddy/tmp`（清 tmp 即失）。
- **`ConvertTo-StableNode` 只规范化键序**（`WI-014`）：`$null` vs 缺失键、空数组 vs 缺失、数组元素序**均未规范化**，且 `updatedAt` 每写必变 ⇒ 「同数据 ⇒ 同字节」**仅在忽略时间戳时成立**（`WI-002` 的原断言口径过强，已在本节更正）。
- **豁免态已迁出 tmp、快照仍在 tmp**（`WI-015`）：跨轮事实落点治理只做了一半。
- **台账 HTML 看板 / 跨机迁移**：未做。
- **`-WaiverCode` 是封闭枚举**：新场景需先扩枚举（有意为之，防自由文本复活）。

## 六、GATE-DELTA / 回填

- **GATE-DELTA 结论**：见收尾回复 `R4` 段（由 `close-round.ps1` 实测输出；本文不预先声称 PASS，遵守上轮 GOV-012 纠错口径）。
- **证据粒度声明（第三轮审计要求补的）**：`gate-runner` 对 `07-AI开发规范.md` 的 diff 是**文件级**判定；该文件相对 `HEAD` 还含 §13–§15 等**往轮未提交内容**（HEAD 过旧），本轮只声明 §16.1/§16.1b/§16.3/§17.6 四个符号 ⇒ 「本轮改动 ⊆ 标注」在**行级不可核验**，此处如实声明。
- 非本轮产物（既有/并行，**具名登记、不认领、不修改**）：
  `00-规则编号索引.md`（上轮所改，本轮未动，走范围外清单）；
  `Tests/LaborPresentationTests.cs`、`Tests/LaborFulfillmentSchedulingTests.cs`、`Tests/LaborOutputCaptureTests.cs`
  （**并行会话在途产物**：均在范围外清单生成后才出现，`gate-runner` 首跑因此报「标注遗漏实际改动文件」；
  经重跑 `-GenScope` 吸收后 DELTA 回复 PASS —— 这正是本轮新增**陈旧守卫**要暴露的那类竞态）；
  `git status` 中其余历史积压。
- 工具侧交付物（`.codebuddy/**`）在 §3 正文登记 + `toolchain-registry.md` 登记；台账侧未做项在 `PROG-TOOLGOV` 逐条登记（WI-008…WI-015）。

---

署名：本变更由 AI 助手（CodeBuddy · DeepSeek-V4.1-Flash）于 2026-10-12 实施并生成全部交付物。
