---
change_id: CHG-20261010-UI-INSTCARD
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedDeployed
scope: 机构页 **UI 重构**（现代化/高级感）：把"压平成一行的字符串"改为**卡片**——结构化分层 + 能力 chip 语义色 + 悬停/整卡可点 + 降级
rule_ids: AI-001, AI-003, AI-011, GOV-002, GOV-003, UI-001, UI-003, LOC-001, PERF-002
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Institutions.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.cs
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# 机构页 UI 重构（卡片化）

**ChangeID**：`CHG-20261010-UI-INSTCARD`｜**日期**：2026-10-10｜**当前程序集**：`038FCE41E4C9B3FB5C9E3654EBB6BB5409A574173F97BD1540D75E9875103232`（= 游戏目录）｜**部署**：已部署

## 一、改前 vs 改后

**改前**（每机构一行纯文本、单字号单色）：
```
事务所 · 制度：运行中 | 机构配置：营业 | 营业：非营业时段 | 时段：10:00–21:00 | 能力：岗位=非营业时段 食品=未挂牌 …
```

**改后**（每机构一张卡，两层信息、语义色、可点）：
```
▌ 事务所 · 事务所            [政策]
  岗位 ●  食品 ○  衣物 ○  服务 ○        10:00–21:00
```
- 左色条 = **制度阶段**（`Active` 绿 / `Preparing` 橙 / `Draining` 蓝 / `Faulted` 红 / 其他 灰）
- 能力 chip 的**颜色即状态**：可用=绿、非营业时段=橙、停业=红、未挂牌/未配置=灰、其余=中灰（**五级原因码各有其色，不做单色阶**）
- chip 悬停给完整原因文本；**整卡可点**（与「政策」按钮同义，扩大可点面积）

## 二、关键设计决策

1. **不改应用层、不解析字符串**：卡片直接读权威读模型（`QueryInstitutions` / `QueryInstitutionPolicy` / `InstitutionCapabilityRules.Evaluate`），与那根 `InstitutionSummary` 拼装串同源同义。⇒ 零 Data/Save 变更、零应用层改动。
2. **索引按 `view` 引用缓存**：`view` 每 60 tick 或制度快照变更才重建 ⇒ 卡片化**不引入逐帧查询**；构造失败 `Log.ErrorOnce` 不刷屏。
3. **几何全部派生**：高度 = `Measure.RowHeight(Small) + RowHeight(Tiny) + CardPadY×2 + SpaceXs`；按钮宽 = `Measure.Width(label) + SpaceMd×2`（并有下限）；无手写魔数。
4. **降级 D2**：chip 排不下即止（`if (x + width > row.xMax) break;`），不硬挤、不压邻件。
5. **只动机构行**：识别条件 = 应用层给机构行发的动作 verb 恰为 `Policy` 且 `Key` 非空 ⇒ 说明行 / 旧归档行 / 演员行 / 空态 / 其余页签**渲染路径完全不变**。
6. **本地化合规**：机构类型用既有 `KindLabel`（`PC_PEA.Kind.*`），能力名/原因用既有 `EconomyLabels.LocalizedId("Cap.Name."/"Cap.Reason.", …)` ⇒ **零新增键**；时段用不变文化格式化（顺带统一了此前两处不同文化的手写格式）。

## 三、技能规范落实（`RimWorld组件绘制`）

| 铁律 | 落实 |
|---|---|
| 度量派生（禁魔数） | 全部尺寸来自 `Measure.*` / `Tokens.*` |
| 单元矩形而非固定宽绝对定位 | chip 采纳 `x += width + gap` 游标，宽度经 `Measure.Width` 精算 |
| 溢出遮盖 | 卡片内用 `Layout.Inset` + `Layout.Column` 取行，不越界；chip 超界即停 |
| 异常隔离 | 索引构造 `try/catch` + `Log.ErrorOnce` |
| 精美度 | 层次 3 层（卡底→左色条→内容）+ 六态（悬停底/边框变化、按钮由 RUIF 六态）+ 对齐 + 呼吸（`CardPadX/Y`、`SpaceXs`）+ 准确（色值来自令牌语义） |

**⚠️ 如实登记（未过项）**：本次为**直接实现**，**未做 HTML/RUIF 预览件** ⇒ 规范红线 1 的"预览与实现 1:1 对照"**无对照物可依**，故**只达 CodeVerified**；精美度 Gate 需**游戏内截图**才能判定（`B/C` 类差异待定责）。已在下方列为待办。

## 四、验证

| 项 | 值 |
|---|---|
| 编译 | **0 错 0 警** |
| 全量测试 | **1220 通过 / 0 失败 / 1 跳过** |
| 部署 | **已部署**，工程 DLL = 游戏 DLL（哈希见文首） |
| 实机 | **NOT EXECUTED** —— 需重启游戏后截图核对（卡片层次/配色/对齐/悬停/chip 降级） |

## 五、待办（如实登记）

1. **游戏内截图验收**（精美度五项逐一过；窄窗宽窗两档各看一次）。
2. **诊断窗同步卡片化**（当前仍是纯 vanilla 文本；同类层次与配色可复用本件的做法）。
3. **其它页签**（差价合同/商店/客户/需求/结算/订单）仍是文本行，未统一视觉语言。

---
AI 署名：本变更标注由 AI 助手（CodeBuddy / DeepSeek-V4.1-Flash）起草，2026-10-10。
