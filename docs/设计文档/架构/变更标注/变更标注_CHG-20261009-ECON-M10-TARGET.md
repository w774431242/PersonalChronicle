---
change_id: CHG-20261009-ECON-M10-TARGET
date: 2026-10-09
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedAndDeployedByThisBatch
scope: U6-A2 —— M10 自由库存目标（FreeStockTarget）接线：机构政策每品类目标声明 + 单一确定性投影 + 治理编辑入口，并落盘 EXC-2026-030
rule_ids: AI-001, AI-003, AI-011, AI-014, GOV-006, GOV-009, GOV-011, GOV-012, GOV-013, DATA-001, DATA-004, DATA-007, SAVE-001, SAVE-002, SAVE-003, SAVE-004, SAVE-006, SAVE-007, ARC-001, UI-001, UI-004, UI-008, LOC-001, LOC-002, LOC-003, LOC-005, PERF-001, TEST-001, COMP-001
files:
  # ── 新增（2）──
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.StockTarget.cs
    type: A
  - path: Tests/StockTargetTests.cs
    type: A
  # ── 修改 · Domain/Data/Economy（6）──
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Governance/InstitutionPolicy.cs
    type: M
  - path: Source/PersonalChronicle/Data/PawnEconomy/InstitutionPolicySaveData.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionPolicy.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Production/EconomyProductionReadoutSource.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Policy.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Overview.cs
    type: M
  # ── 修改 · 本地化（2）──
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  # ── 修改 · 测试（3）──
  - path: Tests/InstitutionPolicyTests.cs
    type: M
  - path: Tests/InstitutionPolicyReadConsistencyTests.cs
    type: M
  - path: Tests/ProductionAuthorisationTests.cs
    type: M
  # ── 治理与进度源（3）──
  - path: docs/设计文档/架构/EXC-2026-030.md
    type: A
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/实施批次状态.json
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# U6-A2 · M10 自由库存目标接线（政策字段 + 单一投影 + 治理入口）

**ChangeID**：`CHG-20261009-ECON-M10-TARGET`（U6-A2）｜**日期**：2026-10-09｜**授权**：用户「按你推荐进行下一步」

## 1. 背景

U6-A（`CHG-20261009-ECON-M10-AUTH`）落定了缺口 1（已授权未开工账）；本批落定**缺口 2 = `FreeStockTarget`**：`EconomyProductionReadoutSource` 的该输入由显式 `Unknown` 改为真实读数。

## 2. 改动

| 文件 | 改动 |
|---|---|
| `Domain/…/Governance/InstitutionPolicy.cs` | 新增 `StockTarget`（品类 defName + 正数最小单位，**0 不可构造** ⇒ 0 只能是"未发布"）、`StockTargetCandidate`；`InstitutionPolicy` 增 `StockTargets`（归一化冻结列表）+ `PublishesStockTarget`/`StockTargetOf`；`InstitutionPolicyRules` 增**唯一归一函数** `NormalizeStockTargets`（丢空/非法/零行 → 输入序"后写胜"去重 → Ordinal 排序）、上界 `MaximumStockTargets = 32`、**单一确定性投影** `ResolveStockTarget`（最低 anchorKey 胜出，**禁求和/平均**） |
| `Data/…/InstitutionPolicySaveData.cs` | `Entry` 内**加法嵌套列表** `stockTargets`（无新顶层节点 ⇒ 无新 schema 身份、无迁移；旧档缺列表 ⇒ 载入归一为空 ⇒ "未发布"）；新增**单一 Entry→Policy 工厂** `ToPolicy`（Read/ReadAll 物理同源，杜绝 U6-A 那类双读分叉）；写端超限**拒绝**（`InvalidPolicy`，绝不静默裁剪）；载入端裁剪 + 坏行剔除不抛；服务价与目标参数**均无默认值** |
| `Economy/…/EconomyInstitutionPolicy.cs` | `ApplyInstitutionPolicy` 增第 10 参（无默认值）+ `QueryStockTargetPolicy(mapId, categoryKey, out target)`（收集候选 → 调 Domain 纯投影） |
| `Economy/…/Production/EconomyProductionReadoutSource.cs` | `FreeStockTarget` 由 Unknown 改为 Known（有发布者时）；无发布者仍 Unknown，原因码 `StockTargetNotWired` 语义已注释钉死为"本图无机构发布该品类" |
| `Economy/…/Dialog_PawnEconomy.StockTarget.cs`（新） | 治理入口：政策行第四行按钮「库存目标」→ 两级菜单（品类 ≤32 → 步进对 `−/＋`，步长常量 `StockTargetStep` 独立命名），每次**整体重写**声明（读→替换一项→提交），改工资/价格/时段经 `Apply`/`ApplyService` 助手**原样带回**当前目标列表 |
| `Dialog_PawnEconomy.Policy.cs` / `.Overview.cs` | `PolicyRowHeight 152→186` + 命名常量 `PolicyFourthRow` 容纳第四行；行内文本增"库存目标: N/未发布"；总览"补货输入"行接线项增"目标库存"，未接线项减为"需求·出口" |
| `Languages/{zh,en}/Keyed/PawnEconomy.xml` | +5 键（`Policy.StockTarget/Up/Down/None`、`Overview.StockTarget`），改值 3（`Policy.Hint` 增库存目标条目、`Overview.NotWiredInputs` 去"目标"、`Wired` 列表经代码增项）；中英逐键对齐 |
| `docs/…/EXC-2026-030.md`（新） | 用户批准的例外登记（U6-A 品类级收敛偏离 M10 §4 键关联） |

## 3. 不变量与边界

- **未发布 ≠ 目标 0**：`StockTarget` 构造器拒绝 ≤0；载入端零行/空键行**剔除**；无发布者 ⇒ 读数 Unknown。三层同规。
- **单一确定性投影**：多机构同品类 ⇒ **最低 anchorKey 胜出**，禁止求和/平均；候选顺序无关（内含反向对照用例）。
- **改字段不清兄弟字段**：第 9/10 参均无默认值；UI 的两个 `Apply` 助手从权威读路径取当前声明原样带回。
- **无第二事实源**：目标只存于既有政策节点；运行时视图是派生（`NormalizedTargets` 不落盘）。
- **诚实口径**：`ConfirmedUndeliveredDemand`（M7）与 `AuthorisedUndeliveredExports`（出口域）仍 Unknown ⇒ **`RestockEvaluator` 仍顶层短路，不得说"补货评估可用"**。

## 4. 验证基线（全部实跑）

| 项 | 判据 | 结果 | 证据 |
|---|---|---|---|
| 编译 | 输出无 `error` | **0 错 0 警** | `_scripts/compile_with_sdk.ps1` |
| 全量离线测试 | dotnet test（net48） | **1166 通过 / 0 失败 / 1 跳过（总 1167）**（基线 1160/0/1，+6） | `Tests/TestResults/U6A2-StockTarget-V2.trx` |
| 新增用例 | 5（StockTarget）+ 1（载入端零/空键剔除） | 全过 | 同上 |
| 部署 | deploy-to-game.ps1 | Defs 36 / Languages 66 / Assemblies 1 synced | 脚本输出 |
| **实机** | 政策页编辑库存目标 / 读数变化 / 读档往返 | **NOT EXECUTED** | — |

## 5. 前置评审例外与处置

| Gate | 结论 | 例外 → 处置 |
|---|---|---|
| DATA+SAVE | PASS WITH EXCEPTION | ①多机构"最低 anchor"单一投影与设计"每机构×品类"粒度差 ⇒ 逐键/逐机构投影待机构级绑定单元；本批在代码注释与 §3 声明。②`StockTargetNotWired` 语义漂移（未接线→无发布者）⇒ **已在原因码注释钉死**并同步 Overview 文案。前置约束（禁默认参/单一归一/0 行丢弃/超限拒绝/PostLoadInit 归一）**全部落实** |
| UI+LOC | PASS WITH EXCEPTION | ①`PolicyRowHeight 186` + 第四行命名常量 ✅；②3+1 键（本批 4 + 复用 `Unpublished`）双语 ✅；③开菜单恰 1 次有界扫描、绘制零扫描零持久态 ✅；④菜单分两级（≤32 → 步进对）✅；⑤`{0}` 带参位占位符**刻意不用**（键值不含数字 ⇒ 与步长常量不存在漂移面）✅ |

## 6. 未做 / 待办 / 三态

- **未做**：机构级读数绑定（目标投影仍是"每图单一机构"口径）；`ConfirmedUndeliveredDemand` / `AuthorisedUndeliveredExports`（待 M7 订单域与出口域）。
- **已闭环**：`EXC-2026-030` 已落盘（Approved，用户 2026-10-09 批准）。
- **工具修复（`.codebuddy/` 属 gitignore 区，不入判定，特此登记）**：`verify-cluster.ps1` 修复"CLIXML 进度记录被误判为 stderr 非空 ⇒ 假 BLOCKED"（子进程设 `$ProgressPreference='SilentlyContinue'` + 兜底剥离 CLIXML）；复跑 `集群自检: PASS`（runId `319147bb…`）。
- **Gate Record**：`.codebuddy/audit/gate-records/GR-CHG-20261009-ECON-M10-TARGET.md`（gitignore 区）。
- **NOT EXECUTED**：实机渲染/交互、读档往返、语言切换、Vanilla 启动 —— 属验收单元 `CHG-20261008-ECON-ACCEPT`。
- **下一步**：U6-B（M8 服务供给机制 + 照护安排 + 家庭参与度）→ U6-C（M7 + M4）→ U6-D（M2/M3 §6 端口、M6 退出 UI、M9 指纹、M12 草稿趋势）。

## 6.1 勘误（2026-10-09 深夜全模块审计发现，已修并重新部署）

**§2 表中「政策行第四行按钮『库存目标』」在当时**并未真正绘制** —— 我只加了 `PolicyFourthRow` 常量与 `fourthRow` 变量声明，**漏了按钮本体**，导致：

- `OpenStockTargetMenu` / `OpenStockTargetStepMenu` / `ApplyStockTarget` **全仓无调用方**（典型"定义了但不可达"）；
- 库存目标声明面**玩家不可达** ⇒ `StockTargets` 恒为空 ⇒ `QueryStockTargetPolicy` 恒 false ⇒ 读数恒 `Unknown(StockTargetNotWired)`；
- 而 1180 条离线用例**全部通过**——因为测试只覆盖 Domain/SaveData 层，**没有任何用例覆盖"菜单是否可达"**。

**处置**：已在 `Dialog_PawnEconomy.Policy.cs` 补绘第四行按钮并在修复处写明告诫；`OpenStockTargetMenu` 现为 1 个生产调用点（调用链 `DrawPolicyRow` → 菜单 → `ApplyStockTarget` → `ApplyInstitutionPolicy`）。修复构建已编译 0 错 0 警、全量 **1180 通过 / 0 失败 / 1 跳过**、已部署（DLL SHA `C9C746B3…`）。

**教训（可复用）**：**"声明一行常量"不等于"入口存在"**；UI 接线类改动必须做一次**调用点存在性核对**（`search` 调用方）作为交付自检项——本轮正是靠全模块审计才发现，此前的 Gate 与测试都没拦住它。

## 7. 文件全集索引（front matter 逐条对应）

除 §2 表逐项点名者外，以下声明文件按角色说明：`Tests/StockTargetTests.cs`（新增夹具 5 用例：0 语义、双读一致、改字段不清目标、去重/Ordinal/超限拒绝、单一确定性投影）、`Tests/InstitutionPolicyTests.cs`（13 个 `Apply` 调用点第 10 参显式化 + 新增「存档零额/空键目标行被剔除且不炸载入」用例）、`Tests/InstitutionPolicyReadConsistencyTests.cs` 与 `Tests/ProductionAuthorisationTests.cs`（既有 P1 判例同步新签名；后者含"改工资不清服务价/目标"反向样本）、`Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Overview.cs`（接线项增「目标库存」、未接线项减为「需求·出口」）、`docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/实施批次状态.json`（进度源：lastChangeId / currentUnit / 部署哈希 / B6 输入面）、`Source/PersonalChronicle/BuildStamp.g.cs`（编译自动改写，按先例列入）。

---
AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
