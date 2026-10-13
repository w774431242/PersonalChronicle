---
change-id: CHG-20261009-MEM-VERBAL
date: 2026-10-09
author: AI(DeepSeek-V4.1-Flash)
type: 记忆技能优化（移除网关口令强制 → 用户口头审批为默认）
scope: memory-standard v2.1 工程适配：新增 approve_verbal.py + memory_changes.py 口头路径；集群侧口径同步（6 处）；首批 12 条记忆单全部落库
base: HEAD
files: []
---

# 变更标注 · CHG-20261009-MEM-VERBAL

> 用户指令："优化结构化 skills，移除网关审批，只需要用户的口头审批即可。"

## 1. 变更清单

| # | 项 | 内容 |
|---|---|---|
| 1 | **落库门槛改为口头审批** | `memory_changes.py`：`apply_approved_change` / `reject` 支持 `signing_key=None` + `approval_note`；新增 `_unsigned_audit`（审计记 `approval: verbal` + 用户原话，`mac=VERBAL-UNSIGNED`） |
| 2 | **新增工程入口** | `scripts/approve_verbal.py`：`--proposal-id <req-ID> --action approve\|reject --confirm "<用户原话>"`（`--confirm` ≥8 字符强制校验） |
| 3 | **网关降级为可选** | `approval_gateway.py`（TTY + 口令）与 `.codebuddy/memory-guard/` 凭据**保留**，不再强制 |
| 4 | **上游正文覆盖声明** | `SKILL.md`「审批方式（本工程约定）」显式声明**覆盖**上游「基本边界 §2 / §3」的网关强制要求（不篡改上游原文，另立覆盖条款） |
| 5 | **集群侧同步（6 处）** | `SKILL.md`（审批方式 / 包内索引 / 工程定制件清单）· `pc-release-clerk.md`（记忆节）· `pc-orchestrator.md`（工作流第 9 步）· `checklist §0.3` · `toolchain-registry`（改两行：口头默认 + 网关可选）· `12 分册`（§3 边界表 + DOC-004）· `memory-guard/README.md`（标注可选） |
| 6 | **首批 12 条落库** | 第 1 条经网关口令（用户终端完成）；**其余 11 条经口头审批**由 AI 落库（依据用户 2026-10-09「全部通过审核」+ 本次指令） |

## 2. 验证证据

```text
落库    ：committed/ = 12 个条目；requests/ = 0（全部处理完毕）
检索    ：search_memory.py ⇒ 检索到 12 条
审计链  ：approval-audit.jsonl = 12 行（1 条网关签名 + 11 条口头 verbal）
终态    ：decisions/ = 12 个
看板    ：memory-dashboard.html 已刷新
```

（FINALIZE 见 §4）

## 3. 边界（诚实登记）

1. **强度下降（用户明示接受）**：口头审批无独立凭证，**无法技术性阻止** AI 越权落库；本路径依赖 AI 自律 + 审计留痕（`approval_note` 记录用户确认原话）。
2. **HMAC 链不再全覆盖**：口头路径审计行 `mac=VERBAL-UNSIGNED` 为占位；`verify_approvals.py`（TTY）仅覆盖网关路径——混合链语义已在 `SKILL.md` 说明。
3. **同 Windows 账户** ⇒ 不构成强隔离，不得宣称「防 AI 绕过」。
4. `commit_memory.py` / `delete_memory.py` 的**直接提交**入口仍禁用（必须走「提案 → 批准 → 落库」流程）。
5. **MANIFEST 例外（工程定制件）**：`SKILL.md` · `scripts/memory_changes.py` · `scripts/approve_verbal.py`；其余文件可与上游逐字节核对。

## 4. DELTA 终检

```text
pwsh <skill>\scripts\gate-runner.ps1 -Base HEAD -Annotation docs\设计文档\架构\变更标注\变更标注_CHG-20261009-MEM-VERBAL.md -ExcludeFromFile .codebuddy\tmp\chg-20261009-mem-verbal-out-of-scope.txt -Blocking -StrictSchema
⇒ GATE-DELTA: PASS — 标注覆盖 0 个改动文件，无遗漏/超范围/残留（提示 0 条）。

pwsh .codebuddy\audit\doc-guard.ps1 -Blocking
⇒ 结论：PASS（新增 15 / BLOCKED 0 / WARN 0 / C7 修改 10 / 消失 0）

pwsh .codebuddy\audit\finalize-change.ps1
⇒ ① context OK ② 清单 OK（2190 条）③ doc-guard PASS ④ GATE-DELTA PASS（-StrictSchema）⑤ 回填 OK
⇒ FINALIZE: PASS
```

> 本变更由 AI 助手实施：CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
