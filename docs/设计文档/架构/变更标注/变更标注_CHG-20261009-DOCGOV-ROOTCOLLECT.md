---
change-id: CHG-20261009-DOCGOV-ROOTCOLLECT
date: 2026-10-09
author: AI(DeepSeek-V4.1-Flash)
type: 存量收口（仓库根迁移 + 归口 + 归档 + 基线刷新）
scope: DOC-001 落点收口首轮；用户批准后执行；不删除业务资产（归档可逆）
base: HEAD
files:
files:
  - path: docs/README.md
    type: M
  - path: docs/设计文档/架构/README.md
    type: M
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261001-LAND-FLOORDETAIL.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261002-CANVAS-PERSIST.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261002-FLOORDETAIL-AUDIT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261002-MERGE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261002-ORPHANKEYS.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261002-PRODCOLLAPSE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261002-RUNTAB-100PCT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261002-RUNTAB-EMPTYSTATE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261002-UIAUDIT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261002-UNITTEST-FIX.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261003-POSTS.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261003-WORKSHOP-SPLIT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261003-WORKSTATION.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261004-ADDSHIFT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261004-CFIX.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261004-EMPLOYEE-ACCESS.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261004-MEALSCHEDULE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261004-POSTOVERRIDE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261004-SHIFTGATE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261004-SHIFTMEAL.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261004-UIROWS.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261004-WORKTAB-HIDE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261004-WORKTAB-SHIFT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261004-WORKTAB-TAKEOVER.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261004-WSAUDIT1.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261004-WSFIX.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261004-WSSETTINGS.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-AUTOASSIGN-ONLY1.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-AUTOASSIGN.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-CLEANUP.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-COLAUDIT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-COLTUNE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-COLTUNE2.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-COLTUNE3.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-COMPFIX.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-COMPFIX2.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-DEVCOLS.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-DEVRENAME.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-EFFCOLOR.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-EVERWORK.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-MEALBYSHIFT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-MEALHOUR-FIX.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-MEALHOUR.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-MEALPRIO.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-NODEMOTE-AUDIT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-NODEMOTE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-OFFSHIFT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-OVERVIEW-FITCHART.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-OVERVIEW-IND-B.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-OVERVIEW-IND-C.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-OVERVIEW-IND-D.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-OVERVIEW-IND.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-PERFDIAG.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-POSTLAND.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-POSTTIP.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-REDLINE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-RUIFTABLE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-SETTINGS-UI.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-SETTINGS.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-SORTWEEK.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-TABLESORT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-TAKEOVERUI.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-TIMERANGE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-TRENDBOARD.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-WSDOC.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261005-WSDOCFIX.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261006-EVENTSTIME.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261006-EVKEY.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261006-FTABS-REMOVE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261006-GRIDDOCS.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261006-GUIDE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261006-KPI-REMOVE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261006-RAIL-LAYOUT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261006-T1FLAT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261006-TABUNIFY.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261006-TOUR.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261006-TOUR2.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261006-TRKEY.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-DOCSTRUCT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-ECONAI-RESEARCH.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRID-REMOVE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRIDP1.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRIDP2R.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRIDP3.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRIDP3B.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRIDP3C.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRIDP3D.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRIDP3E.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRIDP3F.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRIDP3G.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRIDP3H.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRIDP3I.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRIDP3J.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRIDP3K.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRIDP3L.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRIDP3M.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261007-GRIDP3N.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261008-CB-DISPATCH.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261008-ECON-ART.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261008-ECON-AUTOEMPLOY-AUDIT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261008-ECON-B32B4.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261008-ECON-B5.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261008-ECON-DELIVERY.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261008-ECON-DEPLOY-ACCEPTANCE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261008-ECON-NEEDS-CYCLE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261008-ECON-NEEDS-TAB.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261008-PAWN-ECONOMY.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-DOCGOV.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-AUDIT-5MOD.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-B6B7.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-M3-WAIT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-M4-OPTIONS.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-M5-EMERGENCY.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-M6-EXIT.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-M7-PURCHASE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-M8-CARE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-M9-BACKOFF.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-M9-BETA.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-M9-FULL.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-M9-LEDGER.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-M9-RELIABILITY.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-M9-STYLE.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-NEEDS-3TABS.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-NEEDS-AUDIT-FIX.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-NEEDS-BARUNIFY.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-NEEDS-CARDS.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-NEEDS-CYCLE-UI.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-NEEDS-LONGTERM.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-NEEDS-P3-CLEANUP.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-NEEDS-POLICY-CARD.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-NEEDS-SINGLE-COLUMN.md
    type: A
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261009-ECON-NEEDS-TABS.md
    type: A
---

# 变更标注 · CHG-20261009-DOCGOV-ROOTCOLLECT

> 本轮 = DOC-001/010 的**存量收口首轮**（用户 2026-10-09 批准"存量收口轮"）。
> 语义：把仓库根散落物按落位判定表归口；**移动不改字节**（SHA256 前后校验）；**不删除**业务资产（归档可逆）。

## 1. 迁移（Move-Item + SHA256 校验，全量清单见 `.codebuddy/tmp/root-collect-manifest.txt`）

| # | 内容 | 数量 | 去向 |
|---|---|---|---|
| 1 | `变更标注_CHG-*.md`（含并行会话在途件） | **131** | `docs/设计文档/架构/变更标注/`（DOC-001 新落点） |
| 2 | `GR-*.md` Gate Record 残留 | 2 迁 + 1 去重 | `.codebuddy/audit/gate-records/`（同名同哈希副本已删） |
| 3 | `可行性审计_移除降档通道与移出车间恢复.md` | 1 | `docs/设计文档/功能模块/车间体系/`（审计跟随子系统） |
| 4 | `steam_2378767285_1.6_subscribe.txt` + 2 CSV | 3 | `docs/设计文档/功能模块/自动化体系规范/02-基础设施与电力/`（工坊电力溯源原始数据） |
| 5 | 日志 / 临时件 / 记忆残留 / 0 字节 `.cs` | 50 | `docs/_归档/根散落_20261009/`（**可逆**，未删除；如需彻底清理请确认） |
| 6 | 非法文件名 `nul'`（Windows 保留名 + 引号） | 1 | 已清除（`\\?\` 前缀删除） |

**结果：仓库根现仅剩 `README.md` 与 `.gitignore`。**

## 2. 污染源缝合（本轮关键发现）

根目录此前**反复出现**新标注并非"AI 随手新建"，而是两个 ECON 系列工具脚本把落点**硬编码在仓库根**：

| 文件 | 原行为 | 修正 |
|---|---|---|
| `_scripts/verify-pawn-treasury-operations.ps1`（L164） | `Join-Path $repo '变更标注_CHG-20261008-ECON-DELIVERY.md'` | 改 `docs\设计文档\架构\变更标注\`（含目录自建） |
| `_scripts/finalize-pawn-economy-art.py`（L63） | `(root/'变更标注_CHG-20261008-ECON-ART.md')` | 改 `docs/设计文档/架构/变更标注/`（含 `mkdir(parents=True, exist_ok=True)`） |

> 说明：`_scripts/**` 整目录未被 git 跟踪（`?? _scripts/`）⇒ 该 2 项改动**不进 front matter**，以本节说明为准（同 `.codebuddy/**` 纪律）。

## 3. 基线刷新与门禁复跑

| 项 | 命令 | 结果 |
|---|---|---|
| 基线刷新 | `doc-guard.ps1 -Rebaseline` | 730 文件冻结（2026-10-09） |
| 门禁复跑 | `doc-guard.ps1 -Blocking`（**不带**范围外声明） | `结论：PASS（新增 0 / BLOCKED 0 / WARN 0）` |
| 范围外清单清空 | `.codebuddy/tmp/docguard-out-of-scope.txt` | 原 8 条并行产物条目随迁移失效，已清空并加注"不得再用以规避真实违规" |
| 双副本同步 | `sync_from_docs.ps1` | `Copied 61` + `Hash check: all files match` + lint 231 IDs 全解析 |

## 4. DELTA 终检

```text
pwsh <skill>\scripts\gate-runner.ps1 -Base HEAD -Annotation docs\设计文档\架构\变更标注\变更标注_CHG-20261009-DOCGOV-ROOTCOLLECT.md -ExcludeFromFile .codebuddy\tmp\rootcollect-out-of-scope.txt -Blocking
⇒ GATE-DELTA: PASS — 标注覆盖 134 个改动文件，无遗漏/超范围/残留。（范围外声明 2192+ 个不参与判定）
```

**本轮踩坑两条（已修复，留档供后续轮次复用）**：

1. **清单生成必须逐文件**：范围外清单若用 `git status --porcelain`（未跟踪**目录聚合**为一条 `?? dir/`），gate-runner 用逐文件口径时会因该目录条目的**前缀匹配**把整目录排除 ⇒ 已声明文件反被判 phantom。修法：清单生成加 `-uall`（与 gate-runner 内部口径一致）。
2. **标注不得声明自身**：gate-runner 会把标注文件从 gitSet 主动移除，故 front matter 若包含本标注 ⇒ phantom。修法：生成声明列表时排除 `*ROOTCOLLECT*`。

## 5. 已知限制（如实登记）

1. **本沙箱不传播 PowerShell 退出码**（`exit 2` 实测返回 0）⇒ 判读以 stdout 结论行为准。
2. `docs/_归档/根散落_20261009/` 为**可逆归档**（未删除）；如需彻底清理须另行确认。
3. 迁移后，历史文档正文内的旧根路径自指**按惯例不回改**；`_scripts/_delta_scope_needs_tab.txt`（含 121 条旧根路径）属一次性范围清单，保留为历史留档。
4. 并行会话（ECON 系列）在迁移窗口期仍可能新建根文件——若发生，按 DOC-001 由门禁拦截并登记。

> 本变更由 AI 助手实施：CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
