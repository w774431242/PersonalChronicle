---
change-id: CHG-20261011-AINEED-VIZ
date: 2026-10-11
author: AI(DeepSeek-V4.1-Flash)
type: 可视化工程（当前 AI 需求模型一页图 · archify dataflow）+ 索引登记
scope: 新增 archify 规格源 / 成品 / 浏览器证据收据（docs/开发参考/）+ docs/README 开发参考段索引登记；无 Source/Defs/Save/UI 改动
base: HEAD
files:
  - path: docs/README.md
    type: M
    symbols: 开发参考段三行（AI需求模型.archify.json / .archify.html / .visual-check.json）
    lines: 104-107
  - path: docs/开发参考/AI需求模型.archify.json
    type: A
    symbols: archify dataflow 规格源（9 节点 / 4 阶段 / 9 条流）
    lines: —
  - path: docs/开发参考/AI需求模型.archify.html
    type: A
    symbols: 成品（deliver 9/9 · artifact `ab81c146…`）
    lines: —
  - path: docs/开发参考/AI需求模型.visual-check.json
    type: A
    symbols: 浏览器证据收据（containment / readability / captures 三组 PASS）
    lines: —
---

# 变更标注 · CHG-20261011-AINEED-VIZ

> 用户指令：「执行该skills，对象是当前AI需求模型」→ 文档变更申请（DOC-004）→ 用户「**批准**」。承载技能：`archify`（按 fast authoring path 执行）。

## 1. 交付物与三类证据（claim 分开陈述）

| 证据类别 | 结果 |
|---|---|
| `validate --quality showcase` | `ok=True` · composition **pass / errors 0 / warnings 0** · **9/9 artifact checks** |
| `deliver`（确定性工件检查） | `ok=True` · spec `ca68c21c…` / 4,430 B · artifact `ab81c146…` / 717,013 B |
| `visual-check`（真实浏览器有界证据） | **containment pass**（1440×900 / 1600×1000 / 1920×1080 / 2048×1320：`scrollW=innerW`、`scrollH=innerH`）· **readability pass**（投影最小文本 6.1px ≥ 6px）· **captures pass**（1440×900 与 2048×1320 × 明/暗 4 张，`ok=True`） |
| 感知复核（图像能力者读最终字节截图） | 未代判：收据 `visualReview` 保持 `pending`，**最终审美归用户** |

## 2. 修复轨迹（10 轮，单点按诊断修，未删语义）

| 轮 | 诊断 | 修法 |
|---|---|---|
| 1–7 | sublabel 超宽×6 / 回边穿 `execute` / 走廊重合 / 标签压节点 / 7px 微段 | 单点：加宽或缩短 sublabel；回边改「右侧上行 → 顶部通道 → 目标顶边」；`labelDy` / `labelAt` / 端口 `fromSide`/`toSide` 三处约束 |
| 8 | `composition/desktop-readability`（viewBox 1160 > 可用 930 ⇒ 子标签投影 4.81px < 6px） | 依判据实现（`renderers/shared/desktop-readability.mjs`：`projected = source × min(1, 930/viewBoxWidth)`）**改 4 阶段、宽 930（缩放比 1.0）**；因阶段中心固定 `x = 100 + stage×215`（dataflow 布局契约），5 阶段内禀宽 ≥ 1068 **无解** |
| 9 | — | showcase **PASS（9/9，0 错 0 警）**，规格冻结后交付 v1 |
| 10 | 交付后端到端实测：`viewer/viewport-overflow`（1440×900 `scrollH=1360`） | 内禀 930×720（比 1.29）过高 ⇒ 压到 **3 行轨道 930×500（比 1.86）** + 卡片文案收紧 + 末列改「目标→预算→仲裁」上行链与 `settle→goals` 短回边 ⇒ 重校验 → 重交付 → **visual-check PASS** |

**与批准申请的差异（如实登记）**：申请描述为「10 节点 / 4 阶段 / 11 条流」；为通过 containment，v2 收敛为 **9 节点 / 4 阶段 / 9 条流** —— 独立的「只读呈现」节点被移除，改由卡片承载（「只读呈现（ITab 单人 / 总控名册）零写路径」），并删除两条重复口径的入边。其余节点语义未改。

## 3. 对象界定与证据来源（该图为何可证伪）

- **对象**：当前**实装**的经济需求驱动 AI 需求模型（非 REQ/SDD 开发需求模型，非设计方案 v0.3/v0.9 的待建多需求模型）。
- **取证**：`code-explorer` 只读调查（含行号）—— `VanillaPawnFactCapture.CaptureNeeds`（`needs.AllNeeds` 逐项读值、Mood 单独摘出）· `VanillaNeedRegistry`（9 项适配表 + `ShowOnNeedList` 透传）· `CurrentNeedProjector`（相对/绝对/原始三口径 + Fact 状态）· `LifePolicy`/`PlanningPolicy`（`FoodEnter .55` / `RestEnter .45` / `JoyEnter .35`、等待加权上限）· `LifeBudget`（三分配 + 下水位 + 应急储备）· `ShadowPlanBuilder`（闸门序列、`RankGoals`、`PlanOption` 四类）· `ShadowPlanningCoordinator` + `IShadowPlanningSink.OnPlanPublished` · 只读 `Query*`（合同/退出预判/应付/退避/工资可信度/照护授权/服务能力）· 两个只读宿主（`ITab_Pawn_PersonalNeeds`、`MasterControlTabWindow.Economy.cs:321`）+ 需求气泡。
- **附带取证更正**：`Dialog_PawnEconomy*.cs` **全仓已不存在**（源码仅存陈旧注释），名册宿主现为总控面板经济「需求观察」页。
- 卡片登记的 3 项缺口取自 `实装落地/实施批次状态.json`（`ECON-AUTOEMPLOY-001` Open · Service 恒 `ServiceNotImplemented` · `consumptionWired: food-only` · `gameVerifiedThisChange:false`）。

## 4. 边界与遗留（诚实登记）

1. **未落盘**：4 张视口截图与 contact sheet（`AI需求模型.archify.visual-check.*.png` / `.visual-check.html`）保留在 `.codebuddy/tmp/archify/`（可复跑再生；不落盘即不新增受治理件）。
2. **审美未复核**：`visualReview` 为 `pending`，本件只证明机器判据与浏览器有界事实。
3. **未派发代码 Gate agent**：本轮无 `Source/`、`Defs/`、`Save`、`UI` 改动，按 `agent-cluster-frontdoor` 触发范围不适用；DELTA/收尾仍按集群口径跑。
4. **规格/证据落点**：`.archify.json` 与 `.visual-check.json` 不属 `doc-guard` 治理扩展名；`.archify.html` 走上一轮「批准 a」确立的流程豁免。
5. **未纳入**：`ECON-AUTOEMPLOY-001` 等缺口**仅登记不修复**（本轮为只读可视化，不改业务代码）。

## 5. FINALIZE 终检结果（实测 stdout）

`change-context -Set -ChangeId CHG-20261011-AINEED-VIZ -Annotation <本件>` ⇒ `CONTEXT: OK`（清单 = `.codebuddy/tmp/chg-20261011-aineed-viz-out-of-scope.txt`）；`-GenScope` ⇒ 标注声明 4 个 · git 改动 2334 个 · 范围外 2329 个；`-Validate` ⇒ `CONTEXT: OK（标注与清单口径一致）`。

```text
① context 校验      : OK
② 范围外清单         : OK —— 标注声明 4 个 · git 改动 2334 个 · 范围外 2329 个
③ doc-guard         : BLOCKED —— 结论：BLOCKED（新增 113 / BLOCKED 11 / WARN 2 / C7越基线修改 13 / C7消失 0）
④ GATE-DELTA        : PASS —— GATE-DELTA: PASS — 标注覆盖 4 个改动文件，无遗漏/超范围/残留（提示 1 条）。
⑤ 回填检查          : OK
⇒ FINALIZE: BLOCKED（唯一阻断 = ③，且**与本轮无关**：11 条全部落在其他并行线 ——
  `docs/参考资料/DLC接口参考包_20261011/` 10 条 + `docs/验证/公有资产与机构资金贯通_20261011/体系链路与修复方案.md` 1 条，
  均为 C3 front-matter 缺/非法；本轮新增件与改动件 **0 违规**）
```

**收尾齐备性校验**（`round-closure.ps1 -ChangeId CHG-20261011-AINEED-VIZ -Annotation <本件> -Artifact <本件> -Blocking`）：

```text
M1 PASS · M2 PASS · M2b PASS · M3 WARN(当日 19 个 / 含本轮 0 —— 本轮无适用 Gate，理由见 §4-3)
M4 INFO · M5 INFO · M6 PASS(行级署名) · M7 WARN(doc-guard 非 PASS，既有) · M8 N/A
⇒ 收尾结论: PASS（机器项 通过=4 / N/A=1 / WARN=2 / BLOCKED=0 · 人工项 8 项待确认）
```

**口径说明**：④ 证明「本轮标注 ⟷ git 改动集双向互含、无超范围、无调试残留」；③ 的 11 条阻断在本轮**之前**即存在（其他并行线在途产物，未纳入本轮声明），本轮未能也不应为其背书（GOV-012）。

> 本变更由 AI 助手实施：CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-11。
