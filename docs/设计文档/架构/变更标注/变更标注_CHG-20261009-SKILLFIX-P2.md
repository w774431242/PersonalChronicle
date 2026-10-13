---
change-id: CHG-20261009-SKILLFIX-P2
date: 2026-10-09
author: AI(DeepSeek-V4.1-Flash)
type: 集群升级（评估报告 P1 余项：doc-guard C7 / gate-runner 标注 schema / memory 看板核验）
scope: doc-guard 基线 v2 与 C7 + gate-runner 标注 schema·scope-paths·正文一致性 + memory-standard 测试可移植性修复；不改 agent 契约与注册表
base: HEAD
source: 外部评估报告 `skills评估与优化方向.md`（Codex）P1-2 / P1-4 / P1-5
files: []
---

# 变更标注 · CHG-20261009-SKILLFIX-P2

> 承接 `CHG-20261009-SKILLFIX-P1`（P1-1/P1-3），本轮完成 P1 余项：
> **P1-2** doc-guard 基线 v2 + C7 修改件检查 · **P1-4** gate-runner 标注 schema / scope-paths / 正文一致性 · **P1-5** memory 看板状态语义核验（结论：**M7 不成立**，但核验过程中发现并修复 3 个真实缺陷）。

## 1. 变更清单

| # | 报告项 | 处置 | 文件 |
|---|---|---|---|
| 1 | **P1-2（报告 D5/D7）**：基线"只拦新增"⇒ 存量被**修改 / 删除**完全无检查 | doc-guard 基线升级 **v2**（路径 + `h` 哈希前 16 位 + `fm` 标记；读取兼容 v1）；新增 **C7**：① 基线内文件消失 → `BLOCKED`；② 哈希变化且基线 `fm=true` → 校验当前 front-matter 仍合法（非法 `BLOCKED`）；③ 哈希变化且 `fm=false` → 提示（历史存量，不判违规） | `.codebuddy/audit/doc-guard.ps1` + `.codebuddy/audit/doc-baseline.json`（v2，779 文件） |
| 2 | **P1-4（报告 C1）**：标注 schema 无校验、Scope 无边界、机器段与正文可不一致 | gate-runner 增三项：**（a）schema**（`change_id`/`date`/`author`/`scope`/`files` 必填 + 格式；默认 WARN，`-StrictSchema` 升级 `BLOCKED`）；**（b）`scope-paths` 边界**（声明即约束，越界 `BLOCKED`）；**（c）正文一致性**（front matter 声明但正文未提及 → WARN） | `C:\...\RimWorld工程架构规范\scripts\gate-runner.ps1`（工程外） |
| 3 | **P1-5（报告 M7）**：指控"committed 待审仍显示已通过 / 过期仍收集" | **核验结论：不成立**——v2 `memory_core.active()` 严格区分 `pending / archived / 已替代 / 过期 / 有效`，看板 `_state` 按 `today` 计算，既有测试已覆盖（`test_expired_excluded`、`test_expire_archive_and_dashboard`）。**但核验发现真实缺陷并修复**（见 §3） | `.codebuddy/skills/memory-standard/tests/test_memory.py` |
| 4 | 登记同步 | 12 分册 DOC-010（C1–C7）、§4 门禁表、§5 执行清单；checklist §7 增 C7 与 `-StrictSchema` 口径；toolchain-registry 三行（gate-runner / doc-guard / 审计行） | `docs/设计文档/架构/12-…md`、`.codebuddy/audit/subagent-audit-checklist.md`、`toolchain-registry.md`、`.codebuddy/skills/doc-standard/references/schema.md` |

## 2. 验证证据

**① P1-2（doc-guard v2 + C7）**

```text
# 正样本（v2 基线建立后）
基线已刷新（v2）：…（779 个文件，含哈希与 fm 标记）
结论：PASS（新增 0 / BLOCKED 0 / WARN 0 / C7越基线修改 0 / C7消失 0）

# 负样本（注入 → 命中 → 自动还原 → 复原 PASS）
a) 12 分册删 status 行       ⇒ X C7 修改后 front-matter 缺/非法 [status] → 结论：BLOCKED  ✅
b) docs/README.md 追加一行   ⇒ i C7 修改了无 front-matter 存量文档（不判违规）→ 结论：PASS  ✅
c) 移走 开发铁律.md          ⇒ X C7 存量文档消失 → 结论：BLOCKED  ✅
复原 ⇒ 结论：PASS（C7 0/0）  ✅
```

**② P1-4（gate-runner）**

```text
0) 正样本（本轮标注）          ⇒ GATE-DELTA: PASS（提示 0 条）                                    ✅
1) 缺 author                   ⇒ ! 标注 schema 缺/非法 [author] + PASS（提示 1）                  ✅
1b) 缺 author + -StrictSchema  ⇒ GATE-DELTA: BLOCKED — 标注 schema 缺/非法 [author]                ✅
2) scope-paths: [Source/]      ⇒ 改动越出 scope-paths 声明: .gitignore / About/… / Defs/… → BLOCKED ✅
3) files 声明 docs/README.md 而正文未提及 ⇒ ! front matter 声明但正文未提及（1）                  ✅
```

**③ P1-5（memory-standard 测试）**

```text
python .codebuddy/skills/memory-standard/tests/test_memory.py
⇒ Ran 20 tests … OK      （修复前：FAILED (errors=4) → 中间态 errors=2 → 全部修复后 OK）
```

## 3. 缺陷登记（本轮发现并修复）

| ID | 缺陷 | 根因 | 修复 |
|---|---|---|---|
| `D-P2-1` | doc-guard `-Rebaseline` 抛 `Argument types do not match`（基线无法生成） | 本机 PowerShell 下 `[ordered]@{ files = @($List[object]) }` 类型解析失败（最小复现确认；`object[]` 写法可行） | 改 `object[] + +=` 累加 |
| `D-P2-2` | memory 测试 4 个 error（中文 Windows） | `script()` 子进程 stdout 为 cp936，父进程以 utf-8 解码失败 ⇒ `proc.stdout=None` | 子进程传 `PYTHONIOENCODING=utf-8` |
| `D-P2-3` | memory 测试 2 个 error（读 `index.md`） | `Path.read_text()` 未指定编码（locale cp936 读 UTF-8 文件） | 3 处补 `encoding='utf-8'` |

## 4. 未处理项 / 边界声明

- **P1-2 分级说明**：`fm=false` 的历史存量被修改只出**提示**（不判违规），避免存量收口前的噪音；`fm=true` 者修改必须保持 front-matter 合法。
- **P1-4 默认强度**：schema 默认 **WARN**（不打断既有调用与并行会话），交付终检口径为 **`-StrictSchema`**（已写入 checklist 与 toolchain 登记）。
- **P2 级**（待批准）：`change-context.json` 共享输入、生命周期 FINALIZE 触发、版本治理与模板压缩。
- **行为验证缺口**：自动触发率 / 审批遵循率仍无模型行为评测；三类计数（声明覆盖 / 来源可用 / 行为验证）不得混报。
- **并行产物**：`docs/_交付包/agent集群_2026-10-09/**`（37 件，流程豁免）为并行会话打包产物，内含本会话早期产物副本，**副本同步性由打包方负责**；`.codebuddy/skills/memory-standard/references/`（lifecycle/retrieval/security）为并行会话产物，本轮未审。

## 5. DELTA 终检

```text
pwsh <skill>\scripts\gate-runner.ps1 -Base HEAD -Annotation docs\设计文档\架构\变更标注\变更标注_CHG-20261009-SKILLFIX-P2.md -ExcludeFromFile .codebuddy\tmp\skillfixp2-out-of-scope.txt -Blocking -StrictSchema
⇒ GATE-DELTA: PASS — 标注覆盖 0 个改动文件，无遗漏/超范围/残留（提示 0 条）。
```

## 6. 回填

### 6.1 首跑即抓到的 C7 假阳性（已修，登记为 `D-P2-4`）

v2 基线建立后**首次实跑**即 BLOCKED：

```text
X C7 修改后 front-matter 缺/非法 [status, supersedes, approved, author]：
  .codebuddy/skills/doc-standard/references/schema.md（DOC-005/007）
```

**根因**：`fm` 标记判定只看"前 40 行是否出现 `doc-id:`"⇒ `schema.md` 正文里的 **front-matter 示例 / 契约模板**被误判为"该文件自身有 front-matter"⇒ 进而要求它具备六字段（它本不该有）。

**修复（两处）**：
1. `fm` 判定**排除代码块**（``` 感知），只认正文中的 front-matter 形态；
2. C7 的 **front-matter 合法性校验范围对齐 C3**（仅 `^docs/`）；非 docs 域（`.codebuddy/**` 各有契约）的修改只登记"修改件提示"。

**修复后实测**：基线抽查 `schema.md fm=False` / `12-分册 fm=True`；修改 `schema.md` ⇒ `i C7 修改件（不判违规，无 front-matter 存量）` + `结论：PASS`；还原后 `C7 0/0` + `PASS`。

### 6.2 基线刷新说明（纪律合规声明）

本轮 `-Rebaseline` 执行两次，均为**格式与判据修正**（v1→v2 补哈希/`fm`；修正 `fm` 判据），**不引入任何新豁免**：

- 第一次：779 文件（v1→v2），相对新增 0（此前 49 个"新增"已查明：交付包 37 件[流程豁免] + 变更标注 9 件[流程豁免] + memory refs 3 件[已索引]，均为并行会话/流程性产物）；
- 第二次：780 文件（含本标注，属流程豁免对象），相对新增 0。

两次均在用户批准的"P1 余项"范围内，**不属"日常刷基线"**；下一次常规刷新仍须等存量收口轮次（DOC-010）。

### 6.3 待办（独立轮次，本轮仍未动）

- 仓库根 121 份 `变更标注_CHG-*.md` 迁入 `docs/设计文档/架构/变更标注/`（DOC-001 落地）；
- 仓库根散落清理（`.log` / 临时 `.cs` / `MEMORY-raw.md` 等）。

> 本变更由 AI 助手实施：CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09；问题来源：Codex 评估报告 P1-2 / P1-4 / P1-5。
