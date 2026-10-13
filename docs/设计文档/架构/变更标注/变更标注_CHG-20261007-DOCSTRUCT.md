---
change_id: CHG-20261007-DOCSTRUCT
version: v0.1
date: 2026-10-07
author: AI
scope: [docs/设计文档/功能模块/资产规范/, docs/设计文档/功能模块/自动化体系规范/, docs/README.md, docs/设计文档/架构/, docs/设计文档/功能模块/车间体系/, Tools/algo_sprite_gen/, _scripts/]
files:
  - path: docs/设计文档/功能模块/资产规范/README.md
    type: A
    symbols: DOC:资产规范目录索引
    lines: 全文
  - path: docs/设计文档/功能模块/自动化体系规范/README.md
    type: A
    symbols: DOC:自动化体系规范目录索引
    lines: 全文
  - path: docs/README.md
    type: M
    symbols: DOC:docs 总索引
    lines: L8-18（T1 基础设施链接）/ L60-70（目录树）/ L177-193（新增「第三批」整理记录）
  - path: docs/设计文档/架构/10-美术资产标准化规范.md
    type: M
    symbols: DOC:10-美术资产标准化规范 §1 分类表 E 行
    lines: L18
  - path: docs/设计文档/功能模块/车间体系/管线运输体系_设计草案_v0.1.md
    type: M
    symbols: DOC:管线运输体系_设计草案 文档定位
    lines: L5
  - path: docs/设计文档/功能模块/资产规范/01-智能化生产设备_T1T3/资产图集提示词/00_公共风格基底与使用说明.md
    type: M
    symbols: DOC:00_公共风格基底与使用说明
    lines: L4-5（依据路径）/ L13（目录结构示意）
  - path: docs/设计文档/功能模块/资产规范/01-智能化生产设备_T1T3/T1_智能化生产设备_功能数值设计.md
    type: M
    symbols: DOC:T1_智能化生产设备_功能数值设计
    lines: L6
  - path: docs/设计文档/功能模块/资产规范/03-T1基础设施_美术提示词_2026-10-06/美术资产设计与使用说明.md
    type: M
    symbols: DOC:T1基础设施美术资产设计与使用说明
    lines: L7 / L13
  - path: docs/设计文档/功能模块/资产规范/04-电网保护设备_美术提示词_2026-10-07/电力保护设备_美术资产设计与使用说明.md
    type: M
    symbols: DOC:电力保护设备美术资产设计与使用说明
    lines: L6-7
  - path: docs/设计文档/功能模块/资产规范/04-电网保护设备_美术提示词_2026-10-07/GPTImage_v0.2/原始说明_v0.1.md
    type: M
    symbols: DOC:原始说明_v0.1
    lines: L4-5
  - path: docs/设计文档/功能模块/自动化体系规范/01-T1生产设备/T1生产设备_实装记录_2026-10-05.md
    type: M
    symbols: DOC:T1生产设备_实装记录
    lines: L41
  - path: docs/设计文档/功能模块/自动化体系规范/01-T1生产设备/T1生产设备_贴图导入记录_2026-10-05.md
    type: M
    symbols: DOC:T1生产设备_贴图导入记录
    lines: L35 / L63
  - path: docs/设计文档/功能模块/自动化体系规范/01-T1生产设备/_历史稿/T1九台生产设备_游戏参数矩阵_2026-10-05.md
    type: M
    symbols: DOC:T1九台生产设备_游戏参数矩阵
    lines: L3
  - path: docs/设计文档/功能模块/自动化体系规范/01-T1生产设备/_历史稿/T1智能化生产设备_初版拟定稿_2026-10-05.md
    type: M
    symbols: DOC:T1智能化生产设备_初版拟定稿
    lines: L3 / L5
  - path: docs/设计文档/功能模块/自动化体系规范/02-基础设施与电力/AUTO-2026-10-06-T1基础设施补全设计方案.md
    type: M
    symbols: DOC:T1基础设施补全设计方案
    lines: L238
  - path: docs/设计文档/功能模块/自动化体系规范/02-基础设施与电力/AUTO-2026-10-06-基础设施电力体系方案.md
    type: M
    symbols: DOC:基础设施电力体系方案
    lines: L633
  - path: docs/设计文档/功能模块/自动化体系规范/03-电网系统/AUTO-2026-10-06-电网系统开发管线.md
    type: M
    symbols: DOC:电网系统开发管线
    lines: L408-409
  - path: docs/设计文档/功能模块/自动化体系规范/03-电网系统/AUTO-2026-10-06-电网系统设计方案.md
    type: M
    symbols: DOC:电网系统设计方案
    lines: L295-296
  - path: Tools/algo_sprite_gen/README.md
    type: M
    symbols: DOC:algo_sprite_gen 对齐文档
    lines: L9-11
  - path: Tools/algo_sprite_gen/pcgen/__init__.py
    type: M
    symbols: pcgen 模块 docstring
    lines: L7-9
  - path: Tools/algo_sprite_gen/pcgen/palette.py
    type: M
    symbols: palette 模块 docstring
    lines: L3
  - path: _scripts/repair-t1-directions.py
    type: M
    symbols: OUT / APPROVED
    lines: L10-11
  - path: _scripts/trial-repair-t1-smelter.py
    type: M
    symbols: OUT
    lines: L10
---

# CHG-20261007-DOCSTRUCT — 自动化体系规范 / 资产规范 目录结构整理

## 1. Change Summary

- **Title**：把 `功能模块/自动化体系规范/`（平铺 19 项）与 `功能模块/资产规范/`（平铺 12 项）按主题分组，并各加一份目录索引 `README.md`。
- **Scope**：仅文档与仓库内辅助脚本的**路径与索引**；**不改文档正文内容**（除链接修正），不动 `Source/`、`Defs/`、`Languages/`、`About/`、`Textures/`，不动任何运行资源。
- **Reason**：两个目录此前为平铺堆放，设计稿 / 历史稿 / 规范 / 出图批次 / 交付包混在同一层；`docs/README.md` §2026-10-05 已登记「`资产规范/` 的 T1T3 与资产图集提示词保留原位」的归属决策，但没有层级表达。分组后按「主题 → 批次」两跳可达，AI 与人都能确定性定位。
- **User impact**：无游戏内行为变化；仅文档路径变化。**`docs/` 不是运行时资源，游戏不加载。**
- **Rule IDs**：`GOV-013`（变更标注）、`GATE-DELTA`；沿用 `docs/README.md` §命名规范 6（专项规范放 `功能模块/<子系统>/` 主题子目录）。
- **Compatibility**：无。不涉及 Vanilla/DLC/第三方 Mod。
- **Localization**：无 Key 变化。
- **Save/Migration**：无。
- **新增文件命名（rel-002 分类）**：`Changed`（文档结构）+ `Fixed`（顺带修正 4 条既有断链）。

## 2. 迁移映射（before → after）

### 2.1 `docs/设计文档/功能模块/自动化体系规范/`

| 归口组 | 迁入文件（文件名未改） |
|---|---|
| `00-通用规范/` | `自动化农机_开发流程SOP_v1.0.md` · `自动化农业设备_开发设计指导_v2.md` · `自动化设备_开发架构样例_生命周期管线_v1.0.md` |
| `01-T1生产设备/` | `T1生产设备_玩法与实现设计方案_2026-10-05.md` · `T1生产设备_实装记录_2026-10-05.md` · `T1生产设备_贴图导入记录_2026-10-05.md` · `T1全部生产设备_游戏参数矩阵_2026-10-05.md` · `生产设备卡_T1实装与T2T3接口_2026-10-05.md` |
| `01-T1生产设备/_历史稿/` | `T1智能化生产设备_初版拟定稿_2026-10-05.md` · `T1九台生产设备_游戏参数矩阵_2026-10-05.md` |
| `02-基础设施与电力/` | `AUTO-2026-10-06-T1基础设施补全设计方案.md` · `AUTO-2026-10-06-基础设施电力体系方案.md` · `AUTO-2026-10-06-工坊电力Mod功能清单.md` |
| `02-基础设施与电力/_交付包/` | `T1基础设施_设计与美术提示词_v0.1_2026-10-06.zip` + `.sha256` |
| `03-电网系统/` | `REQ_电网系统.md` · `SDD-2026-10-06-电网系统.md` · `AUTO-2026-10-06-电网系统设计方案.md` · `AUTO-2026-10-06-电网系统开发管线.md` |
| 新增 | `README.md`（目录索引 + 落位规则 + 未决项） |

### 2.2 `docs/设计文档/功能模块/资产规范/`

| 归口组 | 迁入文件 / 批次目录（名称未改） |
|---|---|
| `00-风格与提示词规范/` | `自动化设备_美术风格规范_v1.0.md` · `自动化设备贴图风格规范与提示词组.md` · `物流管道_贴图提示词组全集.md` |
| `01-智能化生产设备_T1T3/` | `T1_智能化生产设备_功能数值设计.md` · `T1_智能化生产设备_详细规格.md` · `资产图集提示词/`（00~03） |
| `02-T1生产设备/` | `T1生产设备_导入预览_2026-10-05.png` · `T1生产设备_贴图提示词_2026-10-05/` · `T1生产设备_四向修订_2026-10-05/` · `T1生产设备_方向核查_2026-10-05/` · `智能冶炼台_试修_2026-10-05/` |
| `03-T1基础设施_美术提示词_2026-10-06/` | 原 `T1基础设施_美术提示词_2026-10-06/`（整目录改名加序号，内部结构不变） |
| `04-电网保护设备_美术提示词_2026-10-07/` | 原 `电网保护设备_美术提示词_2026-10-07/`（整目录改名加序号，内部结构不变） |
| 新增 | `README.md`（目录索引 + 落位规则 + 未决项） |

**未改名的理由**：`SDD-2026-10-06-电网系统.md`、`REQ_电网系统.md` 被 `EXC-2026-027`、`ADR-GRID-1` 以**裸文件名**引用（`SDD-003` 命名合规），改名会连带打断例外与决策记录的追踪链；故只加层级、不改文件名。

## 3. 同步修正的引用（12 处）

| 文件 | 改动 |
|---|---|
| `docs/README.md` | 顶部 T1 基础设施 10 条链接 + 目录树 + 新增整理记录 |
| `docs/设计文档/架构/10-美术资产标准化规范.md` | §1 表 E 行指向风格规范的链接 |
| `docs/设计文档/功能模块/车间体系/管线运输体系_设计草案_v0.1.md` | 上游架构样例路径 |
| `Tools/algo_sprite_gen/README.md` · `Tools/algo_sprite_gen/pcgen/__init__.py` · `Tools/algo_sprite_gen/pcgen/palette.py` | 对齐文档 / 色板真源路径（含 docstring） |
| `_scripts/repair-t1-directions.py` · `_scripts/trial-repair-t1-smelter.py` | 输出目录常量 |
| 两目录内互链 8 处 | `资产规范`↔`自动化体系规范` 跨目录链接、历史稿指向当前参数矩阵、批次说明指向母图/对照索引 |

**顺带修正的既有断链（4 条，非本次搬迁造成，登记在案）**：
1. `10-美术资产标准化规范.md` → 风格规范（原写 `资产规范/...`，相对 `架构/` 解析不存在）；
2. `管线运输体系_设计草案_v0.1.md` → 上游架构样例（原作 `架构/自动化体系规范/...`，该路径不存在）；
3、4. `资产规范/04-.../GPTImage_v0.2/原始说明_v0.1.md` 的两条链接（原少一级 `../`）；
另：`资产规范/01-智能化生产设备_T1T3/资产图集提示词/00_公共风格基底与使用说明.md` §1 目录结构示意写的 `docs/设计文档/资产图集提示词/` 缺两级，一并修正。

## 4. 范围外改动（不进 `files:` 的说明）

`docs/设计文档/功能模块/{资产规范,自动化体系规范}/` 两目录在 git 中**整体未跟踪**（`git status` 只报两个目录级 `??`），其内部约 **600 个载荷文件**（PNG / JPG / TXT 提示词 / ZIP / 校验 JSON）本轮只发生**目录搬迁**，内容与文件名一字未动（除第 3 节列出的 8 个 `.md`）。这些载荷文件与仓库既有的历史未提交工作一并写入范围外清单 `.codebuddy/tmp/docstruct-out-of-scope.txt`，不参与 GATE-DELTA 的 `A⊆B`/`B⊆A` 判定，在此只作说明。

同一处理适用于 gitignore 区：`.codebuddy/tmp/{copy,verify}-grid-textures.ps1` 与 `_verify_tmp/*.py`、`_backups/**` 内的旧路径——前两者按可复跑性同步了目录常量（gitignore 区，不入判定），`_backups/` 为备份快照，**不回改**。

历史 `变更标注_CHG-*.md`（根 12+ 份、`架构/变更标注/` 43 份）内引用的旧路径按项目惯例**不回改**（它们是当时事实的记录，不是检索入口）；`docs/README.md` §整理记录 已登记该原则。

## 5. 验证证据（可复核）

| 项 | 结果 |
|---|---|
| 两目录 markdown 链接全量校验（自写 PS 脚本，解析所有 `](target)` 并 `Test-Path`） | **MISSING = 0**（校验前为 3，位于 `_历史稿/`，已修） |
| `docs/README.md` + 两份新 README + 5 份被改说明文档的链接校验 | **MISSING = 0** |
| 残留旧路径扫描（全仓非 gitignore 的 `*.md/*.py/*.ps1/*.json/*.cs/*.xml/*.txt/*.js/*.html`） | 仅剩历史 `变更标注_CHG-*` 与 `_backups/`（均已声明不回改） |
| 目录层级核对 | `自动化体系规范/` = 4 组 + README；`资产规范/` = 5 组 + README；两层内无散落顶层文件 |
| 影响面 | 仅 `docs/` 与 `Tools/`、`_scripts/`；**未触碰 Source/Defs/Languages/About/Textures**；未编译、未部署（无运行时代码改动 ⇒ 无需重编 DLL） |

**未跑的检查（照实登记）**：GitHub/Gate 全链路（GATE-ARCH/DATA/SAVE/COMP/PERF/TEST/REL）本轮不适用——无代码、无 Def、无 Key、无存档字段、无依赖变化。GATE-DELTA 见 §6。

## 6. GATE-DELTA

```text
pwsh <skill>/scripts/gate-runner.ps1 -Base HEAD `
  -Annotation 变更标注_CHG-20261007-DOCSTRUCT.md `
  -ExcludeFromFile .codebuddy/tmp/docstruct-out-of-scope.txt -Blocking
```

判定口径：`A` = 本轮触及的 23 个文件（2 新增 + 21 修改）；`B` = 本标注 `files:`；其余未提交工作与 600 个搬运载荷由 `-ExcludeFromFile` 归入「范围外记账」。

## 7. 未决 / 已知缺口

| 项 | 状态 |
|---|---|
| `自动化体系规范/03-电网系统/` 是否升为顶层 `功能模块/电网系统/` | ⛔ 待 PM 裁决（两份 README 与 `docs/README.md` §整理记录均已登记理由与影响） |
| `资产规范/02-T1生产设备/智能冶炼台_试修_2026-10-05/`（未通过美术验收）是否清理 | ⛔ 待 PM 裁决 |
| `资产规范/01-智能化生产设备_T1T3/资产图集提示词/03_T3_全自动化生产设备.md` → `车间体系/冶炼车间_全自动冶炼设备设计蓝图_v1.0.md` | ⛔ 目标文件已不在磁盘（属 `docs/README.md` §并行观察 登记的 7 份消失文件之一），链接保持断链并已登记，未擅自改指向 |

## 8. 回滚

纯文档搬迁，回滚 = 按 §2 映射表反向 `Move-Item`（把各主题组的文件移回目录根、`03-`/`04-` 目录去掉序号前缀），并撤销 §3 列出的 12 处链接修正与 2 份新增 README。**无存档副作用、无 DLL 变更、无需要重编的内容**；即便不回滚也不影响游戏运行。
