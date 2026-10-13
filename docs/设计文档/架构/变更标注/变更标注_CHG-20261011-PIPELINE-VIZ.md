---
change-id: CHG-20261011-PIPELINE-VIZ
date: 2026-10-11
author: AI(DeepSeek-V4.1-Flash)
type: 可视化工程（每轮工程流水线一页图 · archify）+ 文档域豁免口径登记
scope: 新增 archify 规格源 / 成品 / 浏览器证据件（docs/开发参考/）+ 12-规范「流程豁免」登记 + docs/README 索引登记；无 Source/Defs/Save/UI 改动
base: HEAD
files:
  - path: docs/设计文档/架构/12-文档与交付物治理规范.md
    type: M
    symbols: DOC-005 流程豁免（新增 `*.archify.html` 条）
    lines: 169
  - path: docs/README.md
    type: M
    symbols: 开发参考段三行（集群每轮流水线.archify.json / .archify.html / .visual-check.json）
    lines: 101-103
  - path: docs/开发参考/集群每轮流水线.archify.json
    type: A
    symbols: archify workflow v2 规格源（9 节点 / 3 车道 / 5 相位带）
    lines: —
  - path: docs/开发参考/集群每轮流水线.archify.html
    type: A
    symbols: 成品（deliver 9/9 · sha AA78DD1BE0D6）
    lines: —
  - path: docs/开发参考/集群每轮流水线.visual-check.json
    type: A
    symbols: 浏览器证据收据（四视口 × 明暗 containment PASS）
    lines: —
---

# 变更标注 · CHG-20261011-PIPELINE-VIZ

> 用户指令：「可以参考该 skills 进行可视化工程」→ 交付评审 → 用户「**批准 a**」（落点 + 门禁口径）。承载技能：`archify`（外部 skill，按 fast authoring path 执行）。

## 1. 交付物与三类证据（claim 分开陈述）

| 证据类别 | 结果 |
|---|---|
| `validate --quality showcase` | `ok=True` · composition **errors 0 / warnings 0** · **9/9 artifact checks** |
| `deliver`（确定性工件检查） | `ok=True` · spec `12AD5E2C2205…` · artifact `AA78DD1BE0D6…` / 713,598 B |
| `visual-check`（真实浏览器有界证据） | **status=pass** · 1440×900 / 1600×1000 / 1920×1080 / 2048×1320 × light+dark 全部 `scrollW ≤ innerW` 且 `scrollH ≤ innerH` |
| 感知复核（图像能力者读最终字节截图） | 一屏装下、主路径清晰、标签无遮挡、无裁字；**最终审美归用户**（收据 `visualReview` 保持 `pending`，未代判） |
| 校准对照 | 拿 skill 自带样例跑同一把尺子 ⇒ `status=pass`（scrollH 恰 900）⇒ 判据可达，非环境问题 |

## 2. 修复轨迹（4 轮，单点按诊断修，未删语义）

| 轮 | 诊断 | 修法 |
|---|---|---|
| 1 | `workflow/node-overlap`（同车道同列重叠 <8px） | 改列位，保持 `mainPath` 列号非递减 |
| 2 | `label/sublabel 宽度`×2 + `composition/ambiguous-corridor`（回退边与主边共 54px 走廊） | 加宽两节点（保标签语义）+ 回退边 `route: return-left` |
| 3 | `composition/desktop-readability`（加宽后缩放变小、投影文字 <6px） | **回退加宽**，改为压高（内禀 660 → 528） |
| 4 | `viewer/viewport-overflow`（910 → 904） | 撤与相位带信息重叠的引导视图条 + 收一条 tag → **PASS（900）** |

**顺带产出的可复用约束（含校准取证）**：containment 要求内禀**长宽比 ≥1.60**；readability 要求内禀**宽 ≤ ~935**（投影文字 ≥6px）——两者夹出可行域，是本图从 1.37 调到 1.71 的量化依据。

## 3. 门禁口径变更（用户「批准 a」）

`12-文档与交付物治理规范.md` §4「流程豁免」新增：**`*.archify.html`**（引擎交付件字节被 `deliver` 收据钉死，注入 fm 会使收据失真 ⇒ 同「自有模板 / 副本容器」口径）。`doc-guard.ps1` 同步实现并按其类型分档输出豁免提示。

## 4. 边界与遗留（诚实登记）

1. **未落盘**：4 张视口截图与 contact sheet 保留在 `.codebuddy/tmp/archify/`（可复跑再生；不落盘即不新增受治理件）。
2. **非受治理扩展名**：规格 `.archify.json` 与证据 `.visual-check.json` 不在 `doc-guard` 治理扩展名内（`.md/.markdown/.html/.txt/.rst`）⇒ 天然不判；本条豁免**只**针对 `.archify.html`。
3. **`.codebuddy/audit/doc-guard.ps1`**（本轮的豁免实现）在 `.gitignore` 区 ⇒ 不进 git 改动集，按 `CHG-20261010-EO-REG` 先例由范围外清单承载。
4. **未由用户复核审美**：感知复核由 AI 执行，收据 `visualReview` 仍为 `pending`。
5. **doc-guard 现状**：本轮新增件 0 违规（新 `.html` 走豁免）；另有 **1 条既有 BLOCKED 与本轮无关** —— `docs/设计文档/功能模块/经济体系/实装落地/交接单_20261011_UI迁移与工作目录.md` 的 `status` 于 10:59:44 被**经济 UI 线**改为 `updated`，触发 D1「相对基线新增的文件不得自报 update」。**本轮未触碰该件**（并发写入纪律），修复建议：改回 `status: new`（1 词、零正文改动）。

## 5. FINALIZE 终检结果（实测 stdout）

`change-context -Set -ChangeId CHG-20261011-PIPELINE-VIZ -Annotation <本件>` ⇒ `CONTEXT: OK`（清单 = `.codebuddy/tmp/chg-20261011-pipeline-viz-out-of-scope.txt`）；`-Validate` ⇒ `CONTEXT: OK（标注与清单口径一致）`。

```text
① context 校验      : OK
② 范围外清单         : OK —— 标注声明 5 个 · git 改动 2268 个 · 范围外 2262 个
③ doc-guard         : BLOCKED —— 结论：BLOCKED（新增 99 / BLOCKED 1 / WARN 2 / C7越基线修改 13 / C7消失 0）
④ GATE-DELTA        : PASS —— GATE-DELTA: PASS — 标注覆盖 5 个改动文件，无遗漏/超范围/残留（提示 1 条）。
⑤ 回填检查          : OK
⇒ FINALIZE: BLOCKED（唯一阻断 = ③，且**与本轮无关**：1 条为经济 UI 线 10:59 改动 `交接单_20261011_UI迁移与工作目录.md` 的 `status: updated`，见 §4-5；本轮新增件 0 违规）
```

**收尾齐备性校验**（`round-closure.ps1 -ChangeId … -Annotation … -Artifact … -Blocking`）：

```text
M1 PASS · M2 PASS · M2b PASS(front matter) · M3 WARN(当日 5 个 / 含本轮 0) · M6 PASS(行级署名) · M7 WARN(doc-guard 非 PASS，既有)
⇒ 收尾结论: PASS（机器项 通过=4 / N/A=1 / WARN=2 / BLOCKED=0）
```

**口径说明**：④ 证明「本轮标注 ⟷ git 改动集双向互含、无超范围、无调试残留」；③ 的 1 条阻断在本轮**之前**即存在（落盘前后均为 1，**未新增**）。

> 本变更由 AI 助手实施：CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-11。
