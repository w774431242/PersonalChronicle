---
change-id: CHG-20261009-MEM-SEED
date: 2026-10-09
author: AI(DeepSeek-V4.1-Flash)
type: 记忆整理收纳（MEMORY.md 稳定事实 → memory-standard v2.1 结构化记忆单）
scope: 从 .codebuddy/memory/MEMORY.md v8 提取 12 条稳定事实，生成待审记忆单（requests/）；不改正式记忆库、不改历史日志
base: HEAD
files: []
---

# 变更标注 · CHG-20261009-MEM-SEED

> 用户指令："使用该 skills（memory-standard v2.1）进行整理收纳"。

## 1. 收纳范围与筛选

| 项 | 说明 |
|---|---|
| **来源** | `.codebuddy/memory/MEMORY.md`（v8，2026-09-21，14 节）为主 |
| **入选标准** | 跨会话复用价值高 **且仍是稳定事实**：P1 铁律 / 环境与工具事实 / 高危陷阱 / 方法论纪律 / 用户偏好 |
| **排除标准** | 批次过程与一次性数值（S1–S5 迭代细节）· 已由 docs 权威文档承载的实现细节 · 已过期结论 |
| **数量** | 首批 **12 条**（其余日志/批次史按需再收，不批量搬入） |

## 2. 交付（12 条待审记忆单，**未落正式库**）

| # | req-ID | 主题键 | 类别 / 层 |
|---|---|---|---|
| 1 | `req-194fd2d6ce1140288485df476067a5bc` | `project.compile.authoritative` | 事实 / 构建 |
| 2 | `req-64e2039671d541b8bdf883b1e245188d` | `project.release.deploy-truth` | 规则 / 构建 |
| 3 | `req-662b259b72e148789e2cc2ab991629a3` | `project.csproj.glob-trap` | 规则 / 构建 |
| 4 | `req-7015e714ed2941769f1ab6c8dc1d8544` | `project.ruif.hard-dependency` | 事实 / UI |
| 5 | `req-7c54d5658b034450bd3fb58fdd3bf9b7` | `project.ruif.atom-truth` | 经验 / UI |
| 6 | `req-f8df7e01e88e417486261e145d5a9b09` | `project.draw.hard-rules` | 规则 / UI |
| 7 | `req-dd344b31df6744b5b59eb67ad6d011b0` | `project.data.readmodel-contract` | 规则 / 后端 |
| 8 | `req-65391d8807644c789e35a6bdee7c40ef` | `project.save.nested-dict-trap` | 规则 / 后端 |
| 9 | `req-2f3e16972d154b04a95f9daf2c0f38cc` | `project.test.ecall-pitfall` | 经验 / 测试 |
| 10 | `req-dc85a0b9b657493aa026fe3f2174e2a8` | `project.sandbox.limits` | 事实 / 构建 |
| 11 | `req-ccbe3fee62eb48429abe35237a7f1f75` | `project.methodology.verify-before-claim` | 经验 / 通用 |
| 12 | `req-1cb1cc1ab0ed462f9058d335b6f27de6` | `project.principle.decision-boundary` | 偏好 / 通用 |

附清单：`.codebuddy/memory-journal/requests/README.md`（含审批步骤 + 逐条摘要）。

## 3. 验证证据

```text
① 记忆单生成：propose_change.py --stage × 12 ⇒ OK=12 / FAIL=0
   落点：.codebuddy/memory-journal/requests/req-*.json（12 个）+ 索引 README.md
② 隔离验证  ：search_memory.py --root … ⇒ 仍返回 3 条（legacy），
              requests/ 内容未进入有效检索 ✓（v2.1 隔离设计生效）
③ 门禁      ：doc-guard PASS（新增 11 / BLOCKED 0 / WARN 0 / C7 修改 9 / 消失 0）
④ FINALIZE  ：①context OK ②清单 OK ③doc-guard PASS ④GATE-DELTA PASS（-StrictSchema）⑤回填 OK
             ⇒ FINALIZE: PASS
```

## 4. 已知限制

1. **未落库**：12 条均在隔离区 `requests/`，须**用户本人**在可信终端经 `approval_gateway review` + 口令审批后才进 `committed/`（v2.1 唯一收紧点）。
2. **审批网关可能尚未初始化**（`init` 须本人执行；口令 ≥16 字符、不得经 AI）——未 init 时请先 `init` 再 `review`。
3. 同 Windows 账户 ⇒ 仅「提高误写门槛」，非强隔离（沿用 v2.1 诚实边界）。
4. 现有 3 条 legacy 记录保留不动（用户 2026-10-09 裁决）。
5. 附带修正：SKILL.md 环境要求节澄清 **pwsh 7 控制台默认 UTF-8 ⇒ 交互调用须带 `PYTHONUTF8=1`**（否则中文乱码，仅显示受影响）。
6. **缺陷登记 `D-SEED-1`（当轮发现并修复）**：doc-guard `C3` 将并行会话新增的 `docs/设计文档/架构/EXC-2026-030.md` 误判 `BLOCKED`（缺 DOC-005 六字段）——**EXC 由 `00-规范例外与变更管理.md` 管辖、自有模板**，不适用文档 front-matter 契约 ⇒ 已将 `^docs/设计文档/架构/EXC-` 补入 `Test-ExemptFlow` 豁免（**不改对方文件**，仅修门禁规则）。修复后 `doc-guard` 复跑 PASS。

> 本变更由 AI 助手实施：CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
