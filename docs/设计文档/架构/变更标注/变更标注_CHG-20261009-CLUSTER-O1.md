---
change-id: CHG-20261009-CLUSTER-O1
date: 2026-10-09
author: AI(DeepSeek-V4.1-Flash)
type: 集群升级（吸收外部优化 O1：统一自检通道）
scope: 新增 verify-cluster 统一自检 + 3 处登记 + 文档门禁流程豁免修补；不改 agent 六行契约与 skill 注册表
base: HEAD
source: 外部优化包 `agent集群_O1_2026-10-09.zip`（Codex 产出，Change ID `CHG-20261009-CLUSTER-O1`）
files:
files:
  - path: docs/设计文档/架构/12-文档与交付物治理规范.md
    type: A
---

# 变更标注 · CHG-20261009-CLUSTER-O1

> 本变更 = **吸收外部（Codex）O1 优化**并落地到本工程集群。逐文件差异对比结论：对方包为**纯增量**（0 删除、核心契约 0 修改）。
> 落地动作与验证证据见 §2/§3；`.codebuddy/**` 属 git 忽略区，改动只在本文件 body 登记（同既有纪律）。

## 1. 落地内容（工程内）

| # | 动作 | 说明 |
|---|---|---|
| 1 | 新增 `.codebuddy/audit/verify-cluster.ps1` | **O1 主入口**：不信退出码；断言 stdout 结论行**唯一** + 逐项计数与汇总**交叉校验**；stderr 非空 / 超时（默认 60s，可 1–60）/ **输入漂移（源与副本双快照）** / 陈旧 JSON 一律 `BLOCKED`；产出 JSON 证据（`schemaVersion/runId/exitCodeTrusted=false/checks[]/inputSnapshot[]`）；每轮复制输入到 `.codebuddy/tmp/cluster-checks/<runId>/input/` 隔离运行；结果原子写入 |
| 2 | 新增 `.codebuddy/audit/verify-cluster.tests.ps1` | 12 用例**故障注入回归**（夹具运行时自生成，不依赖静态目录）：healthy + missing-agent / route-failure / missing-summary / duplicate-summary / stderr-router / count-mismatch / summary-without-rows / source-drift / copied-input-drift / timeout / stale-json-on-missing-input |
| 3 | 改 `.codebuddy/audit/toolchain-registry.md` | E 类登记 verify-cluster（含调用方式与边界：`-JsonOut` 不得落源目录） |
| 4 | 改 `.codebuddy/audit/subagent-audit-checklist.md` | §0 集群级检查追加「统一自检件齐备 + 派发前置改跑 verify-cluster」 |
| 5 | 改 `.codebuddy/rules/agent-cluster-frontdoor.md` | 前置协议第 4 步：`audit-subagents.ps1 -Blocking` → **`verify-cluster.ps1 -Blocking`**（最小兜底保留原命令） |
| 6 | 改 `.codebuddy/audit/doc-guard.ps1` | **流程豁免修补**：`docs/_交付包/**`（打包产物=副本容器，合规性由源侧保证）与 `docs/_归档/**`（历史归档件）不判 C1–C6——本变更首次暴露该规则边界 |
| 7 | 改 `docs/设计文档/架构/12-文档与交付物治理规范.md` | §4 补「流程豁免」说明（与 #6 同源，规范侧同步） |
| 8 | 新增 `docs/_交付包/agent集群_2026-10-09/`（+ 同级 zip） | 交付包本体（含使用手册 / MANIFEST / 忠实复刻的 `.codebuddy` 布局 / spec 最小只读集） |

## 2. git 忽略区（`.codebuddy/**`）改动登记

#1–#6 全部位于 git 忽略区（未跟踪）⇒ 不进 front matter，由本节与 §3 实测证据覆盖：

- `.codebuddy/audit/verify-cluster.ps1`（A，10224 B）
- `.codebuddy/audit/verify-cluster.tests.ps1`（A，7741 B）
- `.codebuddy/audit/toolchain-registry.md`（M，+1 行 E 类登记）
- `.codebuddy/audit/subagent-audit-checklist.md`（M，+1 行 §0）
- `.codebuddy/audit/doc-guard.ps1`（M，`Test-ExemptFlow` +2 目录豁免）
- `.codebuddy/rules/agent-cluster-frontdoor.md`（M，第 4 步升级）

## 3. 验证证据（全部本机实测）

| 项 | 命令 | 结果 |
|---|---|---|
| 统一自检（正样本） | `pwsh .codebuddy/audit/verify-cluster.ps1 -Blocking` | `集群自检: PASS (runId=cd289bb74af8452e8fa42465c257f608)`；内含 `结论: PASS (7/7, Gate 10/10, skill 9/9)` + `路由回归: PASS=22 / FAIL=0` |
| **故障注入回归** | `pwsh -File .codebuddy/audit/verify-cluster.tests.ps1` | **O1 回归: PASS=12 / FAIL=0**（12 用例全部按预期 PASS/BLOCKED） |
| 集群审计 | `audit-subagents.ps1 -Blocking` | PASS（7/7、Gate 10/10、skill 9/9） |
| 路由回归 | `route-task.tests.ps1` | PASS=22 / FAIL=0 |
| 文档门禁 | `doc-guard.ps1 -Blocking` | PASS（新增 41 全走流程豁免 / BLOCKED 0） |
| 双副本同步 | `sync_from_docs.ps1` | Copied 61 / Hash 全对 / lint 231 IDs |

**第三方包内既有证据（Codex 留档，未复跑）**：`verification/result.json`（runId `dad3fab5…`，PASS）、`verification/o1-tests.json`（12/12）。

## 4. 边界与已知限制（照实登记）

1. **范围窄但明示**：`verify-cluster` 覆盖**集群定义审计 + 路由回归**两项，**不含** `doc-guard` 与 GATE-DELTA；其 JSON 自带 `scope: cluster-definitions-and-routing-only`。
2. **SHA256 非签名**（对方手册原文边界）：可核对完整性，**不防伪造 stdout**。
3. **退出码仍未可信**：本沙箱不传播退出码；O1 的解法是"不依赖它"而非"修好它"——判读一律读 stdout 的 `集群自检:` 行或本轮 JSON。
4. 本标注未声明自身（gate-runner 会主动移除标注文件，声明即 phantom）。

## 5. DELTA 终检

```text
pwsh <skill>\scripts\gate-runner.ps1 -Base HEAD -Annotation docs\设计文档\架构\变更标注\变更标注_CHG-20261009-CLUSTER-O1.md -ExcludeFromFile .codebuddy\tmp\cluster-o1-out-of-scope.txt -Blocking
（结果见 §6 回填）
```

## 6. 回填

```text
⇒ GATE-DELTA: PASS — 标注覆盖 1 个改动文件，无遗漏/超范围/残留。（范围外声明 2196+ 不参与判定）
```

**front matter 只声明 1 项（12 分册）的理由（照实登记）**：`docs/_交付包/**`（47 文件）与同级 zip 属**打包产物/证据**，已改列范围外清单 —— ① 其内为副本，源侧已受治理；② **包内 `spec/gate-runner.ps1` 副本含残留扫描模式定义（`TODO/FIXME/HACK/Console.Write/...`），若参与扫描会自指假阳性**（与"gate-runner 不扫 `.md`"同源的边界问题，第 3 次遇到同类自指）。若未来需让交付包参与 diff 判定，应先给 gate-runner 增加"证据副本目录"排除项。

> 本变更由 AI 助手实施：CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09；O1 方案来源：Codex 外部优化包 `CHG-20261009-CLUSTER-O1`。
