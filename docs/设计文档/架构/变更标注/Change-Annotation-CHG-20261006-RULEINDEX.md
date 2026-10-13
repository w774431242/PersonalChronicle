---
change_id: CHG-20261006-RULEINDEX
version: v3.1（规范文档 · Patch 级登记订正，不改规则语义）
date: 2026-10-06
author: AI（CodeBuddy）
scope: [docs/设计文档/架构（规范体系主副本）]
files:
  - path: docs/设计文档/架构/00-规范体系总纲.md
    type: M
    symbols: '§4 GOV-013（恢复） / §5 新增 ANNO-001 / §1 GOV 范围 012→013'
    lines: '见 §1 / §4 / §5'
  - path: docs/设计文档/架构/00-统一验收标准.md
    type: M
    symbols: '§11 GATE-DELTA（恢复） / 原 §11「统一 Gate 记录模板」顺延为 §12'
    lines: '§11–§12'
  - path: docs/设计文档/架构/06-变更日志与发布规范.md
    type: M
    symbols: '§13 REL-013 变更标注（恢复）'
    lines: '§13'
  - path: docs/设计文档/架构/00-工程生命周期规范.md
    type: M
    symbols: 'Stage 6.5（LIFE-011 变更标注，恢复）'
    lines: 'Stage 6 与 Stage 7 之间'
  - path: docs/设计文档/架构/00-规则编号索引.md
    type: M
    symbols: '§1 编号域表（GOV/ARC/REL/AI 范围订正、TEST/EXC 订正、GATE 补 DELTA、补登 LIFE/DL/ANNO） / §2 标题 BASE-013'
    lines: '§1 / §2'
  - path: docs/设计文档/架构/README.md
    type: M
    symbols: '文件目录树 · 00-规则编号索引.md 描述行（域名补全）'
    lines: '第 27 行'
  - path: docs/设计文档/架构/00-需求与SDD规范.md
    type: M
    symbols: 'Gate 记录模板锚点 #11→#12（2 处，随 §12 顺延）'
    lines: '§1 关联行 / 附录 A 评审记录模板'
  - path: docs/设计文档/架构/01-基础层规范.md
    type: M
    symbols: 'Gate 记录模板锚点 #11→#12'
    lines: '§12 末段'
---

# Change Annotation · CHG-20261006-RULEINDEX

**规则：** ANNO-001 / GOV-013（唯一规范性定义） / GATE-DELTA 前置产物
**背景：** 域完整性检测发现 `00-规则编号索引.md` 登记与分册实际定义存在 8 处不一致；同时确认「变更标注体系」（GOV-013 / ANNO-001 / GATE-DELTA / REL-013 / LIFE-011）及其它 skill 侧领先内容在项目源中缺失，本次一次性补齐并双侧一致化（源 → `sync_from_docs.ps1` → skill references，SHA256 校验 + 引用 lint 通过）。因 GATE-DELTA 恢复为 §11、Gate 记录模板顺延为 §12，连带更新 2 个分册中的模板锚点引用（避免互链断裂，REL-012）。

> 顶部 YAML front matter 为机器可解析段（`scripts/gate-runner.ps1` 据此自动校验）；下方表格为人工可读段，两者一致。

## Change Annotation

- Change ID: CHG-20261006-RULEINDEX
- Version: v3.1（Patch 级：登记订正 + 条款恢复，无规则语义变更）
- Date: 2026-10-06
- Author/AI: AI（CodeBuddy）
- Scope(摘要): 规范体系文档域（索引登记完整性 + 变更标注体系恢复 + 源↔副本一致化 + 锚点顺延）

### 改动清单（逐文件）

| 文件 | 改动类型 | 涉及符号 / DefName / Key / AssetID | 定位 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `docs/设计文档/架构/00-规范体系总纲.md` | M | §4 表格 GOV-013；§5 新增 `### ANNO-001`；§1 范围 | §1 / §4 / §5 | 恢复 GOV-013（变更标注唯一权威定义）；新增 ANNO-001（ID 体系登记，指向 GOV-013）；GOV 范围 012→013 | GOV-013 / ANNO-001 | ARCH / REL |
| `docs/设计文档/架构/00-统一验收标准.md` | M | §11 GATE-DELTA；§12 记录模板 | §11–§12 | 恢复 GATE-DELTA 章节（7 条检查项 + 失败条件 + 自动校验用法）；原 §11 顺延 §12 | GATE-DELTA / GOV-013 | DELTA |
| `docs/设计文档/架构/06-变更日志与发布规范.md` | M | §13 REL-013 | §13 | 恢复 REL-013（变更标注定位与规则，指向 GOV-013 唯一权威源） | REL-013 / GOV-013 | REL / DELTA |
| `docs/设计文档/架构/00-工程生命周期规范.md` | M | Stage 6.5 · LIFE-011 | Stage 6–7 之间 | 恢复 Stage 6.5（变更标注阶段，驱动 GATE-DELTA） | LIFE-011 / GOV-013 | DELTA |
| `docs/设计文档/架构/00-规则编号索引.md` | M | §1 编号域表（23 行）；§2 `### BASE-001–BASE-013` | §1 / §2 | 订正 GOV/ARC/REL/AI 范围；TEST 范围订正为 001；EXC 标注 005/006 未使用；GATE 补 DELTA；补登 LIFE / DL / ANNO | GOV-013 / ANNO-001 / LIFE-011 | REL / DELTA |
| `docs/设计文档/架构/README.md` | M | 目录树索引描述 | 第 27 行 | 域名列表补全为全规则域 | — | REL |
| `docs/设计文档/架构/00-需求与SDD规范.md` | M | Gate 记录模板锚点 | §1 / 附录 A | `#11-统一-gate-记录模板` → `#12-统一-gate-记录模板`（2 处，随 §12 顺延） | REL-012 | REL |
| `docs/设计文档/架构/01-基础层规范.md` | M | Gate 记录模板锚点 | §12 末段 | 同上（1 处） | REL-012 | REL |

改动类型：M=修改，A=新增，D=删除，R=重命名，C=复制/拆分/物理切片。

**不入 git 校验集的配套改动（声明，不参与 A⊆B / B⊆A 判定）**：

- `C:\Users\Administrator\.codebuddy\skills\RimWorld工程架构规范\SKILL.md`（路由表 DL 表述澄清）+ `references/` 全量同步（56 文件，SHA256 全部一致，引用 lint 220 ID 全解析）+ `scripts/gate-runner.ps1`（两处缺陷修复，见下）——**skill 目录在仓库外**。
- `.codebuddy/tmp/` 下 4 个辅助文件：`_rule_domain_audit.ps1`（域完整性检测器）、`_sync_diff_check.ps1`（源↔副本差异对比）、`gen-ruleindex-scope.ps1`（范围外清单生成器）、`ruleindex-out-of-scope.txt`（清单）——该目录被 `.gitignore` 排除（`git ls-files --others --exclude-standard` 不产出），属未跟踪工具产物。

### 附带工具修复（skill 侧，仓库外）

- `scripts/gate-runner.ps1` · `IsExcluded`：裸 `Contains` → **路径边界匹配**（`-eq` 或 `StartsWith($el + '/')`）。修复根 `README.md` 串扰 `docs/**/README.md` 的误排除（本批实测：修复前 BLOCKED → 修复后 PASS）。
- `scripts/gate-runner.ps1` · 标注自身移除：`$annRel` **正斜杠化**，修复 `HashSet.Remove` 静默失配——标注自身不再必须写进 `-ExcludeFromFile`（旧写法兼容）。

### 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖 git diff / 实际改动的全部文件，无遗漏（本批 8 文件已声明；其余为范围外历史未提交工作）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 在 Intake（本次会话）→ Summary → Annotation → Gate 间一致
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留
- [x] 已用 `pwsh <skill>\scripts\gate-runner.ps1 -Base HEAD -Annotation <本文件> -ExcludeFromFile <清单> -Blocking` 自动校验

### 关键 diff 摘要（节选）

```diff
# 00-规范体系总纲.md
+ | GOV-013 | 每次交付必须生成**变更标注（修改内容清单）**，精确到文件/符号/行，作为验收定位与回滚依据；它与用户向 Changelog 职责分离，互不替代。**（本条目为「变更标注」唯一规范性定义；`LIFE-011` / `AI-013` / `REL-013` / `GATE-DELTA` 仅作引用，不得另立定义。）** |
+ ### ANNO-001 变更标注与变更公告 ID

# 00-统一验收标准.md
+ ## 11. GATE-DELTA：变更一致性 Gate
+ （7 条检查项 + 失败条件 + gate-runner 自动校验）
- ## 11. 统一 Gate 记录模板
+ ## 12. 统一 Gate 记录模板

# 00-需求与SDD规范.md / 01-基础层规范.md
- 00-统一验收标准.md#11-统一-gate-记录模板
+ 00-统一验收标准.md#12-统一-gate-记录模板   （共 3 处）

# 00-规则编号索引.md（§1 表）
- | GOV | 总体治理 | 总纲 | GOV-001–GOV-012 |
+ | GOV | 总体治理 | 总纲 | GOV-001–GOV-013 |
+ | LIFE | 工程生命周期 | 工程生命周期规范 | LIFE-001–LIFE-011 |
+ | DL | 数据层契约与UI对接 | 08c | DL-1–DL-25（正文以数字章节承载） |
+ | ANNO | 变更标注与公告 ID | 总纲 | ANNO-001（ID 体系登记；规范性定义见 GOV-013） |
```

### 验证证据

- 域完整性检测（v3）：21 个编号域全部 OK（含 ANNO 1-1 / LIFE 1-11 / GOV 1-13 / REL 1-13 / AI 1-14）；GATE 覆盖 10/10；dangling `[ID]` 引用 = 0。
- 回同步校验：`sync_from_docs.ps1` ⇒ `Copied 56 spec files` + `Hash check: all files match` + `Rule-reference lint: all references resolve (220 defined IDs)`（EXITCODE=0）。
- GATE-DELTA 终检：`gate-runner.ps1 -Base HEAD -Blocking` ⇒ PASS（标注覆盖 8 个改动文件，无遗漏/超范围/残留）。
