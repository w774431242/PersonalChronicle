---
change_id: CHG-20261010-ECON-COUNTER-PREVIEW
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: PreviewDeliveredNoCodeChange
scope: 经济机构工作台预览批 —— 5 柜台独立窗口的 1:1 落地蓝本（RUIF 同源令牌 + 既有行族几何）+ docs 索引同步
rule_ids: LAND-001, LAND-002, LAND-003, LAND-004, LAND-006, LAND-007, LAND-008, LAND-009, LAND-013, GOV-010, GOV-012, DOC-001, DOC-003, DOC-006
files:
  - path: docs/UI预览/经济机构工作台/机构工作台预览.html
    type: A
  - path: docs/README.md
    type: M
---

# 经济机构工作台预览 · CHG-20261010-ECON-COUNTER-PREVIEW

**ChangeID**：`CHG-20261010-ECON-COUNTER-PREVIEW`｜**日期**：2026-10-10｜**作者**：AI（CodeBuddy / DeepSeek-V4.1-Flash）
**批次性质**：**预览批（设计蓝本）——零代码改动、零存档改动、零 Def 改动**。实装批另立 ChangeID。
**前置决策（用户已批准「按推荐」）**：① 聚合窗口保留、柜台窗口只做聚焦面 + 深链；② **先出预览再实装**；③ 术语以 Def label 为准（改 10 条 `Policy.Kind.*` / `Care.Kind.*` 键值）；④ 窗口只用「已按机构可得」数据面 + 深链（不动应用层读模型，缺口登记见预览 §3）。
**落地蓝本**：`docs/UI预览/经济机构工作台/机构工作台预览.html`（1:1，含 §0 令牌同源表 / §1 五窗口 / §2 状态样例 / §3 基准表与缺口）

---

## 一、为什么先出预览

两位 Gate agent（`pc-orchestrator` AI-001 裁决 + `pc-ui-loc-guard` 表现层约束）独立给出同一结论：**不做 5 套布局，做 1 个窗口类 + 1 张 `kind → 区块` 配置表**。5 形态 ~90% 共享外壳，若各自实现会撞 `08-代码轻量化` 阈值并违反「同类元素走同一 helper」纪律。
「现代化高级感」属主观要求，直接实装存在审美返工风险 ⇒ 按 LAND 规范先产出可对照蓝本（LAND-001 先量后写）。

## 二、改动清单（逐文件）

| 文件 | 改动类型 | 涉及符号 / Key | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| docs/UI预览/经济机构工作台/机构工作台预览.html | A | 5 柜台窗口蓝本（`PC_PEA_LaborOffice/WorkshopOffice/SettlementOffice/Shop/ExamCenter`） | 全文（新增） | 5 个 1080×760 窗口 + 窄窗降级样例 + 令牌同源表 + 落地基准表 + 缺口登记 | LAND-001/002/003/004/006/007/008/009/013 | UI/LOC |
| docs/README.md | M | `UI预览/` 章节索引行 | 112 | 登记 `经济机构工作台/ 机构工作台预览.html` | DOC-001/006 | — |

### 关键约束（预览侧已执行）

- **同源取证**：色值 16 项逐值取自 `11\RUIF\Source\Core\Tokens.cs:72-102`；间距取自 `Tokens.cs:16-58`；卡片/芯片几何取自 `EconomyUiKit.cs:24-46`；行族几何取自 `Dialog_PawnEconomy.cs:99,117,153-177,204-259`；按钮四态取自 `ButtonAtoms.cs:135-154`；空态取自 `FeedbackAtoms.cs:133-144`。
- **引擎降级如实**：预览**不使用**圆角 / 阴影 / 渐变 / 模糊 / CSS 动画（引擎均不支持，LAND §3 已登记降级）。
- **不写死数值**：预览标注行高真值须由 `Measure.RowHeight()` 派生（`Measure.cs:14-18`），禁止写死 18/22/30。
- **缺口如实（LAND-009）**：岗位 / 凭证 / 报告当前未按机构过滤，预览以 `需 scoped 查询` 标记；`EconomyReceipt` 无机构字段，工资结算处不伪造成机构流水。

## 三、验证证据

| 项 | 结果 | 证据 |
|---|---|---|
| 视觉自检（headless 渲染） | **通过** | Chrome headless（`--force-device-scale-factor=2 --window-size=2360,1160`）实拍：首屏即 5 窗口区；KPI 四卡（LAND-006 宽档横向 `[数值 Medium][标签 Tiny]`）、行卡（左语义色条 + 右按钮）、明细面板（Window→Panel→Card 三层）、图标贴图加载正常；**无重叠 / 无溢出 / 无截断** |
| 一轮修正记录（A 类定责） | 已修 | 初版 KPI 卡为上下两行（30+18 内容 > 52−16 可用高）⇒ 标签被挤压；**A 类：我做错了** ⇒ 改 LAND-006 横向宽档（`kpi` 卡高 52 + `align-items:center`） |
| 落点核验 | 通过 | `Test-Path` ⇒ `EXISTS=True / SIZE=42775`；陷阱目录 `11_殖民地综合管理系统\docs\UI预览` ⇒ `False`（未误落） |
| 截图证据 | 已留档 | `.codebuddy\tmp\counter-top2.png`（首窗放大）、`.codebuddy\tmp\counter-full2.png`（全页） |
| 编译 / 测试 | **N/A** | 本轮零 `.cs` 改动（预览批） |
| 游戏内验证 | **NOT RUN** | 无运行环境；实装批执行 |

## 四、回滚

删除预览文件与 `docs/README.md` 的登记行即可；无代码、无存档、无 Def 影响。

### 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖实际改动全部文件（2/2）
- [x] 清单范围 ⊆ Scope，无超范围改动（零代码改动）
- [x] 每个文件标注类型与涉及符号
- [x] Change ID 可互链（决策来源：本次会话 AI-001 裁决 + 表现层约束）
- [x] 无调试残留（临时脚本未落盘；截图在 `.codebuddy/tmp/` 豁免区）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile … -Blocking` 校验
