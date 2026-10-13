---
change_id: CHG-20261009-ECON-M3-WAIT
state: ImplementedAndDeployedByThisBatch
scope: M3「目标与多需求仲裁」**超额实装** —— v0.4 §6 风险时限与 slack + §7 前置链与环检测 + §9 等待加权 / 饿死防护 / 不可持续反馈（含表现层）
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Planning/LifePolicy.cs
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Planning/ShadowPlan.cs
  - path: Tests/ShadowGoalRankingTests.cs
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/实施批次状态.json
---

# M3 §9 目标等待加权与饿死防护 变更定位

**触发**：PM 批准「逐个模块实装」，首个 = **目标与多需求仲裁（M3）**。

**先做的差距分析**（对照 `01-方案主线/多需求并发仲裁_设计补充_v0.4.md` 全 15 节 vs `ShadowPlanBuilder` 现状）：

| v0.4 节 | 现状 | 本批 |
| --- | --- | --- |
| §4 原版控制权 / §5 风险定级（P0–P2）| ✅ 已实现 | — |
| §14 双阈值滞回（进入/完成分离）| ✅ 已实现 | — |
| **§9 饿死防护** | ❌ **缺** | ✅ **本批实装** |
| §5 P3 长期改善 | ❌ 缺，且**无目标源**（现有 Goals 只含 Food/Rest/Joy）| 后置（待 M5 储蓄目标源）|
| §6 `slack`（首次有效帮助时刻 + 风险时限）| ❌ 缺 —— 需"路程/排队/服务时长"估计源 | 后置（依赖未实装模块）|
| §7 前置链 DAG + 循环检测 | ❌ 缺 —— 需依赖规则表 | 后置 |
| §9 "原生活计划不可持续"跨周期反馈 | ❌ 缺 —— 需新字段 + 表现层消费者 | 后置 |
| §8 中断/清理/恢复 | N/A —— 影子模式不执行 Job | — |

## 改动

| 文件 | 改动 |
| --- | --- |
| `LifePolicy.cs`（`PlanningPolicy`）| 新增 `MaximumWaitWeightTicks = 6000` —— 等待加权**上限**；等待只用于同级排先后，不得突破等级/合法性/原版控制权/P0 保护 |
| `ShadowPlan.cs`（`ShadowNeedGoal`）| 新增 `WaitingTicks`（已连续等待，带上限）与 `ActiveSinceTick`（本段等待起点，**会话内传递、不落盘**）|
| `ShadowPlan.cs`（`BuildCore`）| 等待累计：仅对**active 且 carries** 的目标，从上一周期同 `NeedId` 的连续激活段续算；冻结/未知/不适用项恒为 0；跨会话（seed）从 0 重来，不制造永久欠账 |
| `ShadowPlan.cs`（新增 `RankGoals`）| 排序抽为 **internal 纯函数**：① 先等级 ② 同级按等待降序 ③ 稳定键兜底（`Gap?.RequirementKey ?? NeedId`）—— 抽出来是为了让"排序规则"可离线证伪 |
| `Tests/ShadowGoalRankingTests.cs` | 新增 4 用例：同级等久者先办 / **等待永不突破等级** / 同分稳定有序 / 非激活项不携带等待权重 |

**Save 影响：无** —— 等待状态只在会话内的 `ShadowPlan` 链上传递（`previous` 参数），不写入 `ShadowPlanLatch`。按设计"不能把休闲欠账做成永久累积的失控压力"，跨会话重置是**有意**的。

## 超额实装第二批（同批次内追加）：v0.4 §6 风险时限与 slack + §9 不可持续反馈

| 项 | 实现 |
| --- | --- |
| **§6 风险时限** | `ShadowPlanBuilder.RiskDeadline`：用**上一周期与本周期读数差分**估算消耗速率，推算距跌破风险线（Food .35 / Rest .25）还有多少 tick。前提不成立（读数未知 / 无基线 / 时间未推进 / **未在下降**）一律返回 `-1` 不可估 —— 设计明令"不制造伪精度" |
| **§6 slack** | `slack = 风险时限 − 预计首次有效帮助时刻`；帮助时刻只取**可得**来源（该需求自身成本切片的 `RequiredByTick`，即"这笔钱什么时候要"）。未接成本来源时该表为空 ⇒ slack 一律不可估（如实） |
| **§6 接入排序** | `RankGoals`：等级之后先比 slack —— **可估者优先于不可估者**（否则"算不出来"会被当成"不重要"永久压后），都可估时 slack 小者更急；再落到 §9 等待加权 |
| **§9 不可持续反馈** | `ShadowPlan.BasicNeedStreakTicks` 连续累计"基本保障占满"的时长（同会话连续计划间累计、中断清零、跨会话不继承）；达到 `UnsustainableStreakTicks = 60000`（1 游戏日）时，决策页仲裁区显示**"不可持续：基本保障长期占满，应考虑降低可选消费/储蓄、调整工时或增加照护分担"**（设计要求的可行动原因） |
| **快照传递** | `ShadowPlan.RatioSnapshot`（按 NeedId 的读数快照）：仅供下周期差分，**会话内传递、不落盘、不是玩家可见状态** |
| **表现层** | 目标卡新增第 3 行 **「风险时限 3200 tick · 余量 900」**（`Tokens.Info` 着色；不可估时显示"不可估（无基线或未在下降）"）；决策页仲裁区新增「生活计划可持续性」行 |

**新增键 10 个**（`PawnEconomyPlanning.xml` 中英对称）：`Sustainability` / `Sustainable` / `Unsustainable` / `Deadline` / `DeadlineTicks` / `DeadlineUnknown` / `Slack`。

## 超额实装第三批（同批次内追加）：v0.4 §7 前置链与环检测

| 项 | 实现 |
| --- | --- |
| **依赖边** | 「需求 → 它需要的资金」—— 直接取**真实的** `RequirementCostSlice`（必要项 + 总价 + 已有覆盖），不是虚构的规则表 |
| **环判定** | `ShadowPlanBuilder.FindInstitutionallyBlocked(costs, availableMinor, labourChannelAvailable)`（**纯函数**）：仅当**劳动通道不可用**（基本保障占满 P0/P1 / 年龄或资格不允许）**且**某必要项存在无法被覆盖抵掉的资金缺口时，该需求才构成环 —— 需求 → 资金 → 劳动 → 需求 |
| **输出** | `ShadowPlan.InstitutionallyBlocked`（受阻需求键集合）；对它们**不递归创建子目标**，也不驱动"为钱去工作"的死循环 |
| **表现层** | 决策页仲裁区在集合非空时显示「制度性受阻：N 项必要需求既缺资金、又无法通过劳动补足（依赖成环，不再重复索取）」—— 空集合不出噪音 |
| **与 §9 联动** | "基本保障占满"用与 `BasicNeedStreakTicks` 同源的判据（`goals.Any(g => g.Active && g.Priority <= 1)`），避免两处口径分叉 |

**新增键 2 个**（中英对称）：`Blocked` / `BlockedCount`。

## 超额实装第四批（同批次内追加）：§6 帮助时刻多源合并

| 项 | 实现 |
| --- | --- |
| **多源取最小** | "预计首次有效帮助时刻"从**单一来源**（成本切片 `RequiredByTick`）扩为**多源取最小**：① 成本切片时限（钱什么时候必须到位）② **已发布且可行的岗位里最早的工资到账时刻**（`WorkOpportunity.Eligible` + `PaysAtTick`）—— 劳动同样是"首次有效帮助"的一种形态 |
| **仍不造伪精度** | 两个来源都缺 ⇒ slack 保持 `-1` 不可估。新增 `PlanningPolicy.HelpEstimateRequiresExplicitSource` 常量把这条纪律写进策略层：**在接入层给出真实位置/排队数据之前，不填经验值** |

**验证基线**：编译 **0 错 0 警**；测试 **1042 通过 / 0 失败 / 1 跳过**；verify **PASS**。已部署。

## 明确不做（并说明理由）

**§6 的「路程 + 排队 + 开始产生效果前的服务时间」不填经验常量。** 设计 §6 明令"无法可靠预测时限的目标用类别和区间处理，**不制造伪精度**"。

用一个人的移动速度乘一个拍脑袋的"服务前摇"会产出一个**看起来精确、实际无依据**的数字，恰好是这条设计禁令要防的东西 —— 而它还会通过 slack 直接改变目标的先后顺序（即影响行为）。

真正的做法是走接入层：`PlanningSourcePorts` 新增端口，由 Economy 层读原版 `Pawn` 位置与地图上的可达点、服务队列长度后给出**区间**（乐观/保守两个界），再据此算 slack 区间。这属**新端口 + 原版 API 核实**的独立工作单元，需单独一轮（含 `TicksPerMove` 等参数的 Cecil 核实），已在 `实施批次状态.json` 登记。

**验证基线**：编译 **0 错 0 警**；测试 **1042 通过 / 0 失败 / 1 跳过**（`M3-WAIT-V8.trx`，本批累计 +12 用例：4 等待排序 + 4 slack 排序 + 4 受阻判定）；verify **PASS**（110 双语键）。已部署。

**过程中被测试拦下的两处**（都不是实现错，但都值得记）：
1. 首版断言把「总需求 5000 / 覆盖 4500」当成"已覆盖"⇒ 实际仍余 500 缺口，**实现行为正确、断言写错**；已拆成"覆盖不足仍有缺口 ⇒ 成环"与"完全覆盖 ⇒ 不成环"两条。
2. `blocked` 变量因 `Result` 局部函数的**早期 return 点在声明之前**而编译失败 —— 局部函数捕获的变量必须在**所有调用点**（含早退）之前可见。

**权威进度源已更新**：`实施批次状态.json` 的 `lastChangeId` → `CHG-20261009-ECON-M3-WAIT`、B3 新增 `goalStarvationGuardWired: true` 与未做项清单。

## 未做（如实登记）

1. **实机未验收** —— 等待加权是纯领域逻辑，离线用例已证伪式覆盖；实机看的是"长期被挤占的目标是否真的先被办"。
2. **独立 Gate 未覆盖本批**（沿用自评结论；无 Harmony、无新跨库面、无静态状态、Save 未变）。
3. M3 剩余三项（§6 slack / §7 前置链 / §9 不可持续反馈）已在 `实施批次状态.json` 内逐条登记为未做。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
