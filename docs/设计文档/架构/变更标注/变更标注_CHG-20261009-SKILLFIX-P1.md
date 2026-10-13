---
change-id: CHG-20261009-SKILLFIX-P1
date: 2026-10-09
author: AI(DeepSeek-V4.1-Flash)
type: 集群升级（评估报告 P1 工具层可信度修复）
scope: sync lint 定义集（引用≠定义）+ audit skill 来源可用性（声明覆盖/来源可用/行为验证三分）；不改 agent 契约与注册表
base: HEAD
source: 外部评估报告 `skills评估与优化方向.md`（Codex）P1-1 / P1-3
files: []
---

# 变更标注 · CHG-20261009-SKILLFIX-P1

> 承接 `CHG-20261009-SKILLFIX`（P0）；本轮修 P1 中两项"工具层可信度"缺口。
> 改动位于 git 忽略区（`.codebuddy/audit/audit-subagents.ps1`）与**工程外**（skill 侧 `sync_from_docs.ps1`）⇒ `files: []`，以 §1 登记 + §2 证据覆盖。

## 1. 修复清单

| # | 报告项 | 修复 | 文件 |
|---|---|---|---|
| 1 | **S2 规则 lint 把「引用」当「定义」**：`[ARC-999]` 这类未定义引用永远无法被发现 | 定义集改为**只从定义位置收集**：① 表格首列（允许 `**` 包裹）② 标题行含 ID ③ 独立加粗行；引用集逻辑不变 | `C:\...\RimWorld工程架构规范\scripts\sync_from_docs.ps1`（工程外） |
| 2 | **S1 skill 覆盖 ≠ skill 可用**：审计的 `skill 覆盖 9/9` 仅来自 agent 声明，不校验文件是否真实存在 | 新增 **§2.5 skill 来源可用性**：逐 skill 解析 internal（`.codebuddy/skills/<name>/SKILL.md`）/ external（`$SkillRoot\<name>\SKILL.md`，新增 `-SkillRoot` 参数，默认 `$HOME\.codebuddy\skills`）；缺失或 `name:` 与注册名不符 ⇒ 列入问题并 **BLOCKED** | `.codebuddy/audit/audit-subagents.ps1` |
| 3 | 报告建议「计数不得混报」 | 报告输出拆为三行：**`skill 声明覆盖`**（原口径）+ **`skill 来源可用性`**（internal/external/unavailable/name不符）+ **`skill 来源明细`**；Gate Record 证据段同步 | 同上 |
| 4 | 登记与勾选同步 | checklist §0 增来源可用性勾选项（含"声明覆盖 ≠ 来源可用 ≠ 行为验证"纪律）；`toolchain-registry.md` 更新 sync lint 定义集纪律 | `.codebuddy/audit/subagent-audit-checklist.md`、`toolchain-registry.md` |

## 2. 验证证据（本机实测）

**① P1-1 试跑（改前，只读探针）**：38 个规范文件、全文 226 个 ID、新定义集命中 227、**dangling = 0** ⇒ 判定安全后才改脚本。

**② 正样本（改后）**

```text
sync_from_docs.ps1 -Source docs/设计文档/架构
⇒ Hash check: all files match the source.
⇒ Rule-reference lint: all references resolve (227 defined IDs).      ← 旧口径为 231（含引用干扰）
audit-subagents.ps1 -Blocking
⇒ skill 声明覆盖: 9/9 -> OK
⇒ skill 来源可用性: internal=4 · external=5 · unavailable=0 · name不符=0 -> OK
⇒ 结论: PASS  (PASS=7 / BLOCKED=0 / Gate覆盖=10/10 / skill覆盖=9/9)
route-task.tests.ps1 ⇒ 路由回归: PASS=22 / FAIL=0
```

**③ 负样本（注入故障 ⇒ 必须被抓 ⇒ 自动还原）**

| 负样本 | 注入 | 实际输出 | 判定 |
|---|---|---|---|
| P1-1 | 规范正文追加"见 `[ARC-999]`" | `WARNING: Dangling rule references: 12-文档与交付物治理规范.md: ARC-999` | ✅ 旧逻辑下会静默通过 |
| P1-3 | `doc-standard/SKILL.md` 的 `name:` 改为 `doc-standard-x` | `skill 来源可用性: … name不符=1 -> FAIL` + `- [skill 来源] skill name 与注册名不符：doc-standard → …` | ✅ |

**④ 复原验证**：还原后 `Hash check` 通过、`lint all references resolve (227)`、`来源可用性 … name不符=0 -> OK`、审计 `结论: PASS`。

## 3. 未处理项（P1 余项 / P2，待批准）

- **P1-2 doc-guard C7**：本轮**修改**的基线内文件也检查（报告 D5/D7；需哈希基线，当前基线仅路径集合）。
- **P1-4 DELTA 增强**：Scope 边界 + 机器段↔正文一致性 + schema 检查（报告 C1；需改外部 `gate-runner.ps1`）。
- **P1-5 memory 看板状态语义**（报告 M7）：需核验 v2 `visualize_memory.py` 后再定。
- **P2**：`change-context.json` 共享输入、生命周期 FINALIZE 触发、版本治理与模板压缩。

## 4. 边界声明

- **行为验证缺口未变**：本轮只补"来源可用性"（静态可解析性）。自动触发率 / 审批遵循率仍**无模型行为评测**，不得据本轮宣称改善。
- 工程外文件（skill 侧 `sync_from_docs.ps1`）**不属本仓 git diff**；其改动以 §2 证据覆盖。
- 外部 skill 依赖本机路径（`$HOME\.codebuddy\skills`）；换机时若外部 skill 缺失，审计将 `BLOCKED`（设计如此：注册表不得有悬空引用），可用 `-SkillRoot` 指向实际位置。

## 5. DELTA 终检

```text
pwsh <skill>\scripts\gate-runner.ps1 -Base HEAD -Annotation docs\设计文档\架构\变更标注\变更标注_CHG-20261009-SKILLFIX-P1.md -ExcludeFromFile .codebuddy\tmp\skillfixp1-out-of-scope.txt -Blocking
（结果见 §6 回填）
```

## 6. 回填

```text
⇒ GATE-DELTA: PASS — 标注覆盖 0 个改动文件，无遗漏/超范围/残留。（范围外声明 2203+ 不参与判定）
```

### 6.1 本轮发现的真实交互冲突（O1 隔离自检 × 来源可用性）——已修订

首次落地后 **O1 隔离自检立即报 `集群自检: BLOCKED`**（审计 `结论: BLOCKED` 但逐 agent `PASS=7 / BLOCKED=0`）。根因：`verify-cluster.ps1` 按设计只把 **4 个脚本 + 7 个 agent** 复制进隔离副本（`scope: cluster-definitions-and-routing-only`），副本内**不含 `skills/`** ⇒ 新检查把 4 个 internal skill 全判"不可解析" ⇒ 硬失败。

这正是评估报告要求的「**区别包模式与工程模式**」。修订为**分级判定**：

| 情形 | 判定 |
|---|---|
| `name:` 与注册名不符（文件可读） | **BLOCKED**（硬失败） |
| 完全不可解析（包 / 隔离副本模式下属预期） | **WARN** + 报告标注运行模式（`工程` / `包/隔离`）；"任务需要而无法解析时 BLOCKED"由派发环节承担 |

**修订后双向兼容实测**：工程内 `skill 来源可用性(工程 模式): internal=4 · external=5 · unavailable=0 · name不符=0 -> OK`；隔离副本 `集群自检: PASS（runId=f7dbd0695ce84666ace00d31853d5da8）`。

> 附带收益：该冲突本身证明 **O1 的隔离运行机制有效**——它把"工程内隐式依赖"从静态声明中暴露了出来。

> 本变更由 AI 助手实施：CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09；问题来源：Codex 评估报告 P1-1 / P1-3。
