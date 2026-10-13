---
change_id: CHG-20261010-ECON-POLICY-HINT
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedNotDeployed
scope: 实测反馈修复 —— 机构政策页在"未绑定车间区"时行内直给"下一步"引导（引导玩家从"配了工资却不发岗位"的困惑中脱出）
rule_ids: AI-001, AI-011, GOV-010, GOV-012, UI-001, UI-008, LOC-001, LOC-002, TEST-001
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Policy.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# 实测反馈修复：政策页"未绑车间区"的下一步引导

**ChangeID**：`CHG-20261010-ECON-POLICY-HINT`｜**日期**：2026-10-10｜**部署**：见文末

## 一、反馈与取证（存档级证据，非推测）

玩家反馈："这些经济机构貌似不起作用……**太乱了，而且启用后也无效**"。

从 `新移民5.rws` 解出的实际状态：

| 项 | 实际值 | 判定 |
|---|---|---|
| 机构制度 | `phase="Active"`、Mod 设置已开 | 门是开的 ✓ |
| 机构政策 | **5 条**，`open=True`、`wagePerJob=500/700`、时段 8–18 / 11–18 | **已配置** ✓ |
| **车间区（`Zone_Workshop`）** | **存档中不存在任何车间区节点** | ❗**根因** |
| 政策行的 `ZoneId` | 全部为 0（= 未绑定） | 与上一条互为因果 |

⇒ **根因**：政策页页首明写"**未绑定车间的机构不发布任何岗位**" —— 玩家配了工资与营业时段，但**从未划出车间区、也未绑定**，因此该机构**一个岗位都不发布**，Pawn 自然看不到可应聘的活。**不是"启用无效"，而是"缺一道前置"**。

（另：另一处提示"当前没有可声明目标的品类（车间里没有产出的工单）"已存在，说明库存目标同理需要车间内先有工单。）

## 二、修复（本轮唯一代码改动）

`Dialog_PawnEconomy.Policy.cs` 的 `PolicyRowText`：当 `zoneId <= 0` 时，在该行末尾追加一条**醒目引导**：

```
⚠ 未绑定车间区 → 岗位不会发布：先在车间管理所划出车间区，再回到本行绑定
```

**理由**：此前原因要玩家在十余个"未发布"字段之间自行推断（实测反馈正是"配了工资、开了营业，Pawn 仍不去工坊"）。引导**只在真的缺前置时出现**，不占正常行版面、不改既有布局。

新增键 `PC_PEA.Policy.Hint.BindZoneFirst`（中/英），双语各 **247** 键一致、UTF-8 无 BOM。

**未做的**（如实登记，避免过度承诺）：未改政策页整体信息密度（玩家评"太乱了"）—— 归并/折叠式重排属**表现层重设计**，需按 `11-预览落地标准化规范` 走 LAND 流程（量预览 → 1:1 定义 → 卡型阈值），不宜在本轮临时改版；**已建议单独立项**。

## 三、验证

| 项 | 值 |
|---|---|
| 编译 | **0 错 0 警** |
| 全量测试 | **1220 通过 / 0 失败 / 1 跳过**（`Tests/TestResults/Hint-fix.trx`） |
| 本地化 | 双语各 247 键、计数一致、无 BOM、新键可解析 |
| 实机 | **NOT EXECUTED**（引导文案的实际观感待重启后确认） |

---
AI 署名：本变更标注由 AI 助手（CodeBuddy / DeepSeek-V4.1-Flash）起草，2026-10-10。
