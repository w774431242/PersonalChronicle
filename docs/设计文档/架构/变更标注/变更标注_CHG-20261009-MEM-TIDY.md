---
change-id: CHG-20261009-MEM-TIDY
date: 2026-10-09
author: AI(DeepSeek-V4.1-Flash)
type: 记忆与日志体系整理（索引化 + 缺口修复，不删除）
scope: 为 .codebuddy/memory 与 .workbuddy/memory 建立统一索引与代际登记；散落件原位归位；补集群触发词缺口；不改任何历史日志与记忆正文内容
base: HEAD
files: []
---

# 变更标注 · CHG-20261009-MEM-TIDY

> 用户指令："按现有规范对这两个文件夹中的记忆、日志文件进行一次大整理（**不删除**），新文件按规范 skills 输出。"

## 1. 整理原则（为什么是「索引化」而非「搬迁」）

| 决策 | 依据 |
|---|---|
| **历史件全部原位保留，不移动** | 6 处文档引用了这些路径（见索引 §五）；移动即破坏引用链（DOC-007 精神：历史件不回改） |
| **不删除**（用户明示） | 本轮未执行任何删除 |
| **不合并两处 `MEMORY.md`** | 两处为不同代际（09-17 → 09-18 → 09-21 v8），各为当时快照；合并会掩盖演进 ⇒ 用**取代登记**（DOC-007） |
| **新增仅索引与登记** | DOC-001 落点唯一 + DOC-006 索引义务 + DOC-007 取代登记 |

## 2. 变更清单

| # | 动作 | 文件 |
|---|---|---|
| 1 | 新建**记忆体系统一索引**（五体系地图 / 代际链 / 检索入口 / 引用登记 / 纪律） | `.codebuddy/memory/README.md`（新增） |
| 2 | 新建**姊妹目录索引**（.workbuddy 记忆定位与并轨登记） | `.workbuddy/memory/README.md`（新增） |
| 3 | 现行长记忆顶部插入「体系指针」（不改其它内容） | `.codebuddy/memory/MEMORY.md`（修改） |
| 4 | 散落件**原位归位**（不删除） | `.codebuddy/tmp_check_preview.js` → `.codebuddy/tmp/` |
| 5 | **修复路由缺口**：`整理记忆 / 整理日志 / 记忆整理 / 日志整理` 补入 `pc-release-clerk` 触发词（本轮实测该任务判 `UNROUTED`） | `pc-release-clerk.md` + `checklist §0.1` |
| 6 | 本标注 | `变更标注_CHG-20261009-MEM-TIDY.md` |

## 3. 清点结果（整理前摸底）

| 目录 | 文件数 | 说明 |
|---|---|---|
| `.codebuddy/memory/` | **45** | 44 日志（2026-08-09 ~ 09-22）+ `MEMORY.md` |
| `.workbuddy/memory/` | **20** | 18 日志（2026-08-08 ~ 10-09）+ `MEMORY.md` + `MEMORY_ARCHIVE_2026-09-17.md` |
| `.codebuddy/memory-journal/` | 6 | v2.1 记忆库（3 committed + index + dashboard + index.json） |
| `.codebuddy/doc-journal/` | 1 | `2026-10-09/committed/2030-docgov-intake.md` |
| 散落件 | 1 | `.codebuddy/tmp_check_preview.js` |

## 4. 验证证据

**① 整理后状态**

```text
.codebuddy/memory/README.md      新增（记忆体系统一索引：五体系 + 代际链 + 检索入口 + 引用登记）
.workbuddy/memory/README.md      新增（姊妹目录索引 + 并轨登记）
.codebuddy/memory/MEMORY.md      修改（顶部 +4 行「体系指针」；正文与全部历史内容未动）
.codebuddy/tmp_check_preview.js → .codebuddy/tmp/tmp_check_preview.js（原位归位）
历史日志 / 记忆正文：0 处内容改动 · 0 处移动 · 0 处删除（满足"不删除"要求）
```

**② 路由缺口修复验证（本轮发现 → 当轮修复）**

```text
修复前：route-task.ps1 -Task '整理 .codebuddy 与 .workbuddy 的记忆与日志文件…' ⇒ UNROUTED
修复后：route-task.ps1 -Task '整理记忆与日志文件' ⇒ 命中 pc-release-clerk.md ✓
audit-subagents.ps1 -Blocking ⇒ PASS（7/7 · Gate 10/10 · skill 9/9；无触发词二义）
route-task.tests.ps1 ⇒ 路由回归 PASS=22 / FAIL=0
```

**③ FINALIZE 一键链**

```text
① context OK ② 清单 OK（2172 条）③ doc-guard PASS（新增 9 / BLOCKED 0 / WARN 0 / C7 修改 9 / 消失 0）
④ GATE-DELTA PASS（-StrictSchema，提示 0 条）⑤ 回填 OK
⇒ FINALIZE: PASS
```

## 5. 已知限制

- 逐日日志**双轨并存**（两目录同名不同内容）为历史事实，本轮**未合并**；检索须两处并查（已写入索引）。
- `.workbuddy/memory/` 至 2026-10-09 仍有更新 ⇒ 该工作区**可能仍在使用**；本轮仅加索引，未改其内容。
- 本轮路由实测 `route-task.ps1` 判 **UNROUTED**（"整理"类无触发词）——已在本轮修复（§2-5），修复后须重跑 `audit-subagents.ps1` + `route-task.tests.ps1`。

> 本变更由 AI 助手实施：CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
