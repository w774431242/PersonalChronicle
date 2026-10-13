---
change-id: CHG-20261011-AINEED-MECH
date: 2026-10-11
author: AI(DeepSeek-V4.1-Flash)
type: 可视化工程（AI 需求模型 · 机制级逻辑图集 11 张 · archify）+ 索引登记
scope: 新增 docs/开发参考/AI需求模型-机制图/（11 规格源 + 11 成品 + 11 浏览器证据 + 1 主题索引）+ docs/README 开发参考段登记；无 Source/Defs/Save/UI 改动
base: HEAD
files:
  - path: docs/README.md
    type: M
    symbols: 开发参考段新增机制图集目录一行
    lines: 108
  - path: docs/开发参考/AI需求模型-机制图/README.md
    type: A
    symbols: 主题索引（11 张机制图 ↔ 总图节点 ↔ 关键判定 ↔ 8 条实装事实）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/01-感知采集与适配.archify.json
    type: A
    symbols: workflow 规格源（12 节点 / 3 车道 / 4 相位带）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/01-感知采集与适配.archify.html
    type: A
    symbols: 成品（deliver 9/9 · artifact `f6d3dfb06bd8e344`）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/01-感知采集与适配.archify.visual-check.json
    type: A
    symbols: 浏览器证据收据（containment / readability / captures 全 PASS）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/02-缺口投影.archify.json
    type: A
    symbols: workflow 规格源（9 节点 / 3 车道；内禀 900×560）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/02-缺口投影.archify.html
    type: A
    symbols: 成品（deliver 9/9 · artifact `faa5c5f94122283b`）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/02-缺口投影.archify.visual-check.json
    type: A
    symbols: 浏览器证据收据（四视口 PASS）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/03-目标与阈值.archify.json
    type: A
    symbols: workflow 规格源（10 节点 / 3 车道）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/03-目标与阈值.archify.html
    type: A
    symbols: 成品（deliver 9/9 · artifact `ced137661ccc90e7`）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/03-目标与阈值.archify.visual-check.json
    type: A
    symbols: 浏览器证据收据（四视口 PASS）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/04-预算与储蓄.archify.json
    type: A
    symbols: workflow 规格源（10 节点 / 3 车道）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/04-预算与储蓄.archify.html
    type: A
    symbols: 成品（deliver 9/9 · artifact `21765258479f2fc1`）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/04-预算与储蓄.archify.visual-check.json
    type: A
    symbols: 浏览器证据收据（四视口 PASS）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/05-多需求仲裁.archify.json
    type: A
    symbols: workflow 规格源（10 节点 / 3 车道）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/05-多需求仲裁.archify.html
    type: A
    symbols: 成品（deliver 9/9 · artifact `a412f08142603466`）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/05-多需求仲裁.archify.visual-check.json
    type: A
    symbols: 浏览器证据收据（四视口 PASS）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/06-影子计划与四类方案.archify.json
    type: A
    symbols: workflow 规格源（8 节点 / 3 车道）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/06-影子计划与四类方案.archify.html
    type: A
    symbols: 成品（deliver 9/9 · artifact `39c63e7a9a61f925`）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/06-影子计划与四类方案.archify.visual-check.json
    type: A
    symbols: 浏览器证据收据（四视口 PASS）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/07-计划发布与执行接线.archify.json
    type: A
    symbols: workflow 规格源（10 节点 / 3 车道）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/07-计划发布与执行接线.archify.html
    type: A
    symbols: 成品（deliver 9/9 · artifact `400b2218e18904c3`）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/07-计划发布与执行接线.archify.visual-check.json
    type: A
    symbols: 浏览器证据收据（四视口 PASS）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/08-合同与就业退出.archify.json
    type: A
    symbols: lifecycle 规格源（7 状态 / 3 车道；内禀 940×570）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/08-合同与就业退出.archify.html
    type: A
    symbols: 成品（deliver 9/9 · artifact `b64f31e109644058`）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/08-合同与就业退出.archify.visual-check.json
    type: A
    symbols: 浏览器证据收据（四视口 PASS）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/09-购买尝试与退避.archify.json
    type: A
    symbols: lifecycle 规格源（5 状态 / 1 车道；内禀 940×570）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/09-购买尝试与退避.archify.html
    type: A
    symbols: 成品（deliver 9/9 · artifact `1dec5999c2bc2d61`）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/09-购买尝试与退避.archify.visual-check.json
    type: A
    symbols: 浏览器证据收据（四视口 PASS）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/10-结算与学习.archify.json
    type: A
    symbols: dataflow 规格源（5 节点 / 4 阶段 / 4 条流）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/10-结算与学习.archify.html
    type: A
    symbols: 成品（deliver 9/9 · artifact `6e0a7dbaffd55613`）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/10-结算与学习.archify.visual-check.json
    type: A
    symbols: 浏览器证据收据（四视口 PASS）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/11-只读呈现装配.archify.json
    type: A
    symbols: dataflow 规格源（7 节点 / 4 阶段 / 7 条流）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/11-只读呈现装配.archify.html
    type: A
    symbols: 成品（deliver 9/9 · artifact `09f343094606f279`）
    lines: —
  - path: docs/开发参考/AI需求模型-机制图/11-只读呈现装配.archify.visual-check.json
    type: A
    symbols: 浏览器证据收据（四视口 PASS）
    lines: —
---

# 变更标注 · CHG-20261011-AINEED-MECH

> 用户指令：「细化到每个机制的逻辑图」→ 文档变更申请（DOC-004：11 张 / 34 件 / 落点与门禁口径）→ 用户「**批准（一次性）**」。承载技能：`archify`；逻辑来源：两轮 `code-explorer` 只读取证（含行号）。

## 1. 交付物与三类证据（逐张，claim 分开陈述）

| 证据类别 | 结果 |
|---|---|
| `validate --quality showcase` | **11/11 张 PASS**：9/9 工件检查 · composition 0 错 0 警 |
| `deliver` | **11/11 张 ok**：spec 与 artifact 均被 SHA-256 钉死（上表前端逐一登记） |
| `visual-check` | **11/11 张 status=pass**：containment（1440×900 / 1600×1000 / 1920×1080 / 2048×1320，`ok=True`）+ readability + captures 三组 PASS |
| 感知复核 | 未代判：11 份收据 `visualReview` 均保持 `pending`，**最终审美归用户** |

## 2. 修复轨迹（12 轮，单点按诊断修，未删语义）

1. **类型契约**：workflow 节点误用状态类型（decision/failure/…）⇒ 全部改为组件类型；
2. **语义检查**：12 处零入/零出节点补进 `semanticChecks`（源/汇是设计事实，不是缺陷）；
3. **lifecycle 事件带**：非主车道只允许列 0–2 ⇒ 重排带内状态并加 `yOffset`；
4. **宽度/标签**：sublabel 超宽、标签压节点、`labelDy`/`labelDx`/`fromSide`/`toSide` 逐处收敛；
5. **走廊与穿节点**：按「先删低价值边、再调路由」处理（删 `desired→codes`、`water→refill`、`active→waiting`、`ok`/`recon` 等低价值节点/边，语义移入结论卡）；
6. **可读性判据**：`projected = source × min(1, 930/viewBoxWidth)` ⇒ 显式 viewBox 宽 >930 会触发字号下限失败（实测 1300 宽三次失败后回退）；
7. **整版式切换（关键发现）**：内禀**长宽比 < 1.6** 会触发阅读器的「高版式」⇒ 02（1.48）与 08（1.50）在 1440×900 纵向溢出（scrollH 1191 / 1180）；改为 **02 `900×560`、08/09 `940×570`**（比 ≥1.61）后重交付并 PASS；
8. 09 另需满足 lifecycle `viewBox` 高度下限 566。

**与批准申请的差异（如实登记）**：申请列「11 张 × 3 件 + 1 主题 README = 34 件」，实际落地 **35 处改动**（多 1 项为 `docs/README.md` 索引登记）；内容层面有 4 张在收敛过程中**把低价值边/节点移入结论卡**（02 的 `desired→codes`、04 的补储回边、05/09 的零记录路径、11 的装配顺序节点），机制语义与判定事实**未删减**，均在卡片中登记。

## 3. 逻辑来源与取证（为何可证伪）

- 两轮 `code-explorer` 只读调查覆盖 11 个机制，产出**判定顺序 / 阈值 / 原因码 / 状态迁移**级别的行号证据；
- 图内每一个量化值（如 `.55/.45/.35`、`7000/800/2000`、`6000/150/2^(n−1)`、`0.10/0.03`、`60 tick / 2 人 / 250 / 4096`）均取自实装常量；
- **8 条「设计/注释 ≠ 实装」**在主题 README §三 与本标注 §4 双处登记，并在对应图的结论卡内原文可见。

## 4. 边界与遗留（诚实登记）

1. **未落盘**：22 张视口截图与 11 张 contact sheet 保留在 `.codebuddy/tmp/archify/mech-sidecars/`（可复跑再生；不落盘即不新增受治理件）。
2. **未改一行业务代码**：本集为只读可视化；发现的 8 条实装事实/缺口**仅登记不修复**。
3. **未派发代码 Gate agent**：无 `Source/`、`Defs/`、`Save`、`UI` 改动，按 `agent-cluster-frontdoor` 触发范围不适用；DELTA/收尾仍按集群口径跑。
4. **审美未复核**：`visualReview` 全为 `pending`；本件只证明机器判据与浏览器有界事实。
5. **既有并行线阻塞**：`doc-guard` 的 11 条 BLOCKED 落在其他线（`docs/参考资料/DLC接口参考包_20261011/` 等），非本轮引入、本轮未触碰。
6. **维护义务**：成品 `*.archify.html` 字节被 deliver 收据钉死 ⇒ **不得手改**；改代码后须重跑 `validate → deliver → visual-check` 三关。

## 5. FINALIZE 终检结果（实测 stdout）

`change-context -Set -ChangeId CHG-20261011-AINEED-MECH -Annotation <本件>` ⇒ `CONTEXT: OK`；`-GenScope` ⇒ 标注声明 35 个 · git 改动 2381 个 · 范围外 2345 个；`-Validate` ⇒ `CONTEXT: OK（标注与清单口径一致）`。

```text
① context 校验      : OK
② 范围外清单         : OK —— 标注声明 35 个 · git 改动 2381 个 · 范围外 2345 个
③ doc-guard         : BLOCKED —— 结论：BLOCKED（新增 129 / BLOCKED 11 / WARN 2 / C7越基线修改 13 / C7消失 0）
④ GATE-DELTA        : PASS —— GATE-DELTA: PASS — 标注覆盖 35 个改动文件，无遗漏/超范围/残留（提示 1 条）。
⑤ 回填检查          : OK
⇒ FINALIZE: BLOCKED（唯一阻断 = ③，且**与本轮无关**：11 条全部落在其他并行线 ——
  `docs/参考资料/DLC接口参考包_20261011/` 10 条 + `docs/验证/公有资产与机构资金贯通_20261011/体系链路与修复方案.md` 1 条，
  均为 C3 front-matter 缺/非法；本轮产出件 **0 违规**：`.archify.html` 走流程豁免、`.json` 不属受治理扩展名、
  主题 README 已补 DOC-005 front-matter（首轮曾 12 条含本轮 1 条，补 fm 后回落至 11 条）
```

**收尾齐备性校验**（`round-closure.ps1 -ChangeId CHG-20261011-AINEED-MECH -Annotation <本件> -Artifact <本件> -Blocking`）：

```text
M1 PASS · M2 PASS · M2b PASS · M3 WARN(当日 34 个 / 含本轮 0 —— 本轮无适用 Gate，理由见 §4-3)
M4 INFO · M5 INFO · M6 PASS(行级署名) · M7 WARN(doc-guard 非 PASS，既有) · M8 N/A
⇒ 收尾结论: PASS（机器项 通过=4 / N/A=1 / WARN=2 / BLOCKED=0 · 人工项 8 项待确认）
```

**口径说明**：④ 证明「本轮标注 ⟷ git 改动集双向互含、无超范围、无调试残留」；③ 的 11 条阻断在本轮**之前**即存在（其他并行线在途产物，未纳入本轮声明），本轮未能也不应为其背书（GOV-012）。

> 本变更由 AI 助手实施：CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-11。
