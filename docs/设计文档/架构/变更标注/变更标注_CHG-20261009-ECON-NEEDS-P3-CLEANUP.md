---
change_id: CHG-20261009-ECON-NEEDS-P3-CLEANUP
state: ImplementedAndDeployedByThisBatch
scope: 批量清理「个人需求」页专项审计遗留的全部 P3（10 项，含 1 个死键）
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/ITab_Pawn_PersonalNeeds.cs
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml
  - path: Languages/English/Keyed/PawnEconomyNeeds.xml
  - path: Tests/TestResults/NeedsCycle-V13.trx
---

# 需求页签 P3 批量清理 变更定位

**触发**：`AUDIT-20261009-ECON-NEEDS-DEMAND-TAB.md` 列出的 P3 批量（含 1 个死键），PM 指令「批量执行」。

**部署事实**：本批**已部署**。⚠ 发布目录同期存在并行会话写入活动。

**验证基线**：编译 **0 错 0 警**；全量测试 **1028 通过 / 0 失败 / 1 跳过**（`Tests/TestResults/NeedsCycle-V13.trx`）；`verify-pawn-needs-ui.ps1` **PASS**（96 双语键 / 38 用例）。

## 清理清单（10 项）

| # | 原 P3 | 处置 | 位置 |
| --- | --- | --- | --- |
| 1 | 切页签不复位滚动（长页签滚到底再切短页签，偏移被钳制） | 检测到 `measure.Tab != state.CycleTab` 时 `state.Scroll = Vector2.zero` | `NeedsCyclePanel.Draw` |
| 2 | `SectionBar` 的 count 语义错位（"需求满足度"旁挂"已规划 N/M"） | 需求页签的 count 改传空串；该计数已在概览卡呈现，不重复 | `DrawDemandTab` |
| 3 | `CapacityBar` 语义借用无注释 | 补注释：以 `(满足度, 1)` 复用容量条；视觉语义一致，故不另起进度条 | `DrawOverview` |
| 4 | 就地镜像 Tokens 常量（有漂移风险） | **取消镜像**，6 个度量直接引用 `RUIF.Tokens.*`（经核实全为 `public const float`，编译期绑定） | 常量区 |
| 5 | 角框复述条色（视觉噪声） | 角框改中性 `RUIF.Tokens.Dim`（条色已由卡内进度条表达） | `DrawOverview` |
| 6 | `PercentWidth` 依赖 "Unconfigured≈84px" 假设且无截断兜底 | 注释更新为实际基准（英文最长 "Unsupported" ≈77px）；**并加 `Flow` 折行兜底**，状态文本与未来新增状态键都不会被硬切，折行高度计入行高 | `Measured` |
| 7 | `SectionBar` 的 `▾` 不可点＝假折叠提示 | **接受**（RUIF 组件既有语义，本页所有分区恒为展开态）；不再视为缺陷 | — |
| 8 | Empty 时页签栏被早退隐藏但留 48px 死区 | 空态**不画页签栏**且 `topInset = 0`，死区消除 | `Draw` |
| 9 | Error 复用"请先进入殖民地地图"提示有误导 | 新增专用键 `Cycle.Error`（"本页暂时无法读取"），catch 分支改用 | `ITab_Pawn_PersonalNeeds.FillTab` |
| 10 | 死键 `PC_PEA.Needs.Cycle.Title` | 删除（中英各 1 键） | 语言文件 |

**键数变化**：96 − 1（删 `Cycle.Title`）+ 1（加 `Cycle.Error`）= **96**，verify 键数校验通过。

## 未做（如实登记）

1. **实机未验**：已部署未重启确认。本批多为结构性微调，重点看：切页签不再跳位、概览卡角框变中性色后的观感、状态文本折行是否触发（正常数据下不应触发）。
2. **独立 Gate 未覆盖本批**：审计结论对应修复前状态；本批为 P3 清理，未再派独立复核（不得记为 PASS，GOV-012）。
3. 审计记录的 P1/P2 已由 `CHG-20261009-ECON-NEEDS-AUDIT-FIX` 修复，本批仅收尾 P3。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。审计结论由专项 agent 独立出具，清理与验证由主 Agent 执行。
