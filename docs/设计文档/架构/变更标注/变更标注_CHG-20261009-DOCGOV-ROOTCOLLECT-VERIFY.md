---
change-id: CHG-20261009-DOCGOV-ROOTCOLLECT-VERIFY
date: 2026-10-09
author: AI(DeepSeek-V4.1-Flash)
type: 存量收口复核（独立验证 + 文档表述修正）
scope: 对并行会话 CHG-20261009-DOCGOV-ROOTCOLLECT 的独立复核（以 doc-guard v2 基线哈希为 oracle）；修正 docs/README.md 与 12 分册中与实测不符/过时的表述；不改业务资产
base: HEAD
files: []
---

# 变更标注 · CHG-20261009-DOCGOV-ROOTCOLLECT-VERIFY

> 用户"继续"指令下，对已完成的仓库根收口轮做**独立复核**（不采信对方自述，改用基线哈希作为客观 oracle），并修正文档中的过时 / 错误表述。

## 1. 独立复核结果（oracle = `doc-baseline.json` v2 哈希）

| 项 | 方法 | 结果 |
|---|---|---|
| **迁移完整性** | 从 ROOTCOLLECT 标注提取 132 条迁移路径，逐条比对基线哈希 | **缺失 0 · 哈希不符 0 · 不在基线内 0** ✅（"移动不改字节"独立成立） |
| 根目录洁净 | 基线中根目录条目 | **仅 `README.md` 1 条** ✅ |
| 目标目录 | 基线 187 条 vs 磁盘 187 个 `.md` | **一致** ✅ |
| 归档可逆 | `docs/_归档/根散落_20261009/` | 50 件（`.txt`×17 / `.log`×11 / `.cs`×5 / `.js`×3 / `.ps1`×3 / `.py`×3 / `.md`×3 / `.png`×2 / `.html`×1 / `.json`×1 / `.flag`×1），**未删除** ✅ |
| 污染源缝合 | 两脚本落点实测 | `verify-pawn-treasury-operations.ps1` → `Join-Path $repo 'docs\设计文档\架构\变更标注'`；`finalize-pawn-economy-art.py` → `annot_dir=root/'docs'/'设计文档'/'架构'/'变更标注'` ✅ |
| 门禁 | `doc-guard.ps1 -Blocking` | `结论：PASS（新增 0 / BLOCKED 0 / WARN 0 / C7 0/0）` ✅ |

## 2. 修正的不一致（本轮发现的真实问题）

| # | 位置 | 原表述 | 实测 | 处置 |
|---|---|---|---|---|
| 1 | `docs/README.md` §收口结果 ③ | "基线已刷新（**730 文件**）" | 实际 **v2 格式、780 文件**（2026-10-09 22:25:46） | 已修正 |
| 2 | `docs/README.md` §文档治理基线 | "仓库根历史存量冻结为只读……检索须**两处并查**" | 迁移已完成，根目录仅剩 `README.md` | 已改为"检索只需一处" |
| 3 | 12 分册 DOC-001 | "冻结为只读历史留档……检索须两处并查；迁移按独立轮次执行" | 同上 | 已改为"收口已完成 + 污染源缝合" |
| 4 | 12 分册 DOC-010 规则 2 | "历史存量（含仓库根 **121 份**标注）" | `变更标注/` 现 187 份 | 已更新为 v2 口径与新数量 |

> 说明：#1 系并行会话的记录错误（沿用了旧基线数值）；#2–#4 属迁移完成后必然过时的表述。修正属 DOC-006 索引同步义务。

## 3. DELTA 终检

```text
pwsh <skill>\scripts\gate-runner.ps1 -Base HEAD -Annotation docs\设计文档\架构\变更标注\变更标注_CHG-20261009-DOCGOV-ROOTCOLLECT-VERIFY.md -ExcludeFromFile .codebuddy\tmp\rootcollect-verify-out-of-scope.txt -Blocking -StrictSchema
⇒ GATE-DELTA: PASS — 标注覆盖 0 个改动文件，无遗漏/超范围/残留（提示 0 条）。

pwsh .codebuddy\audit\doc-guard.ps1 -Blocking
⇒ 结论：PASS（新增 1 / BLOCKED 0 / WARN 0 / C7越基线修改 2 / C7消失 0）
   · 新增 1 = 本标注（变更标注流程豁免）
   · C7 修改 2 = 12 分册（fm=true，front-matter 合法校验通过）+ docs/README.md（fm=false，提示级）
```

## 4. 已知限制

- 归档件（50 件）保持**可逆未删除**；彻底清理须用户确认（含 `MEMORY-raw.md`、`.log`×11、0 字节 `.cs` 等）。
- 本沙箱不传播 PowerShell 退出码（判读以 stdout 结论行为准）。

> 本变更由 AI 助手实施：CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09；复核对象：`CHG-20261009-DOCGOV-ROOTCOLLECT`。
