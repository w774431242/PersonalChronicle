---
doc-id: REF-AINEED-MECH-INDEX
status: new
supersedes: —
author: AI(DeepSeek-V4.1-Flash)
approved: 2026-10-11 23:05
---

# AI 需求模型 · 机制逻辑图集（11 张 · 本目录唯一入口）

> **定位**：把总图 `../AI需求模型.archify.html` 的九个节点**下钻到每个机制**，每张图只讲一个机制的判定顺序 / 阈值 / 原因码 / 状态迁移。
> **逻辑来源**：**当前实装代码**（`Source/PersonalChronicle`，两轮 `code-explorer` 只读取证，含行号），**不是**设计文档的意图版本。
> **生成方式**：`archify` skill 的 fast authoring path —— `validate --quality showcase` → `deliver` → `visual-check`。
> **落盘日期**：2026-10-11 · Change ID `CHG-20261011-AINEED-MECH`

## 一、清单与对应关系

| # | 机制 | 图型 | 规格源 / 成品 | 对应总图节点 | 关键判定（一句话） |
|---|---|---|---|---|---|
| 01 | 感知采集与适配 | workflow | `01-感知采集与适配.archify.json` / `.html` | 感知采集 | `Match` 三道校验（defName + DefinitionVerified + 运行期类型严格相等），失败不丢弃只标 unsupported |
| 02 | 缺口投影 | workflow | `02-缺口投影.archify.json` / `.html` | 缺口投影 | `ActualRatio` 六守卫 + `TargetRatio` 四态；任一端不可取则三条缺口整体回落 |
| 03 | 目标与阈值 | workflow | `03-目标与阈值.archify.json` / `.html` | 目标态与阈值 | `active = 已知 ∧ ¬frozen ∧ (ratio < Enter ∨ (wasActive ∧ ratio < 目标值))`；风险时限为差分估算 |
| 04 | 预算与储蓄 | workflow | `04-预算与储蓄.archify.json` / `.html` | 预算与储蓄 | 三分配 + 下水位 70% / 应急 8% / 触发线 `min(目标, 低+应急)`；`State ≠ Ready` 一律 Unknown |
| 05 | 多需求仲裁 | workflow | `05-多需求仲裁.archify.json` / `.html` | 多需求仲裁 | 16 道闸门顺序即优先级；`RankGoals` 五级键（等级 → slack → 等待 → 稳定键） |
| 06 | 影子计划与四类方案 | workflow | `06-影子计划与四类方案.archify.json` / `.html` | 影子计划 | 四类固定顺序（自救 → 劳动 → 购买 → 服务）；购买三态可行性与其原因码 |
| 07 | 计划发布与执行接线 | workflow | `07-计划发布与执行接线.archify.json` / `.html` | 执行接线 | `Tick` 节流 2 人/tick · 复查 250 tick；发布顺序「报告 → latch → sink」；执行侧 11 步 |
| 08 | 合同与就业退出 | lifecycle | `08-合同与就业退出.archify.json` / `.html` | 执行接线（子机制） | 四退出码顺序（Expired → JobCapReached → InstitutionClosed → PositionUnavailable）+ 幂等 |
| 09 | 购买尝试与退避 | lifecycle | `09-购买尝试与退避.archify.json` / `.html` | 执行接线（子机制） | 退避 `min(6000, 150×2^min(n−1,6))` + FNV-1a 确定性抖动；第 5 次转 PersistentBlocked |
| 10 | 结算与学习 | dataflow | `10-结算与学习.archify.json` / `.html` | 结算与学习 | 0.10 单次增量 × 0.03 日额；`BeginDay` vs `Restore`（读档 ≠ 新的一天） |
| 11 | 只读呈现装配 | dataflow | `11-只读呈现装配.archify.json` / `.html` | （总图卡片承载） | 两宿主 + 只读门面 + 四装配器；60 tick 三重失效门控；零写路径 |

> 每张图另配 `NN-<机制>.archify.visual-check.json`（浏览器证据收据）。

## 二、三类证据（逐张均为 PASS）

1. `validate --quality showcase`：**9/9 工件检查 · composition 0 错 0 警**；
2. `deliver`：规格与成品字节被 SHA-256 收据钉死（成品 `NN-*.archify.html` 因此受 doc-guard 流程豁免，**不得事后编辑**）；
3. `visual-check`：真实浏览器 **containment / readability / captures 三组 PASS**（1440×900 · 1600×1000 · 1920×1080 · 2048×1320；明暗两组抽样截图 `ok=True`）。

视口截图与 contact sheet 按先例留在 `.codebuddy/tmp/archify/mech-sidecars/`（可复跑再生，不落盘）。

## 三、本集登记到的「设计/注释 ≠ 实装」事实（核心增益）

| # | 事实 | 位置摘要 |
|---|---|---|
| 1 | 适配表不是 9 项封死：Biotech 激活追加 `Play` / `Learning` | 机制 01 |
| 2 | `IsFrozen` 恒 `Unknown` ⇒ 计划侧 `frozen` 恒为 false（冻结通道未接通） | 机制 01 |
| 3 | 不存在 `Complete` 常量与「风险时限」常量：退出阈值 = 目标比率，时限为差分估算 | 机制 03 |
| 4 | `RankGoals` 实际键序在等待之前插入 slack 两级，与类注释不一致 | 机制 05 |
| 5 | `InstitutionallyBlocked` **不是闸门**，仅出口字段，早退路径恒为空列表 | 机制 05 |
| 6 | `MaintainReserve` 枚举无任何产生点；Service 方案调用点恒传 false ⇒ 恒 `ServiceNotImplemented` | 机制 06 |
| 7 | 第 5 次退避判定键是 `(pawn, actionClass, reason)` 而非「同指纹」；`MayRetry` 的紧急保护与真实变化放行当前恒传 false | 机制 09 |
| 8 | 八维学习中只有 `ReliabilityPreference` 有投递入口；**5 维全仓无消费方**，实际生效仅 3 处 | 机制 10 |

（上述每条都在对应图的结论卡里原文登记，可逐张核对。）

## 四、阅读建议

- **先看总图**：`../AI需求模型.archify.html`（一页 9 节点，含三项已登记缺口）。
- **再按链路读**：01 → 02 → 03 → 04 → 05 → 06 → 07，再看两条执行侧子机制 08 / 09，最后 10 / 11。
- **改代码后必须重跑**：`node bin/archify.mjs validate <type> <spec> --quality showcase` → `deliver` → `visual-check`（三关全绿才算交付；成品不得手改）。

> AI 署名：本图集由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-11。逻辑均取自实装代码并逐条给出行号取证；与设计文档不符处已在上表如实登记，未做任何代码改动。
