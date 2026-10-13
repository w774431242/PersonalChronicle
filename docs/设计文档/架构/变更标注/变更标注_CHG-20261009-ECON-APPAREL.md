---
change_id: CHG-20261009-ECON-APPAREL
date: 2026-10-09
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedAndDeployedByThisBatch
scope: 首版闭环 §6 第 5–7 条 —— 衣物挂牌价政策字段 + 公共售货点衣物购买与**原版穿戴请求**（含"已购待使用"如实上报、穿戴失败保留衣物）；不改目标体系（§6 第 4 条留下一批）
rule_ids: AI-001, AI-003, AI-005, AI-011, AI-014, GOV-003, GOV-005, GOV-006, GOV-009, GOV-010, GOV-011, GOV-012, GOV-013, DATA-001, DATA-007, SAVE-001, SAVE-002, SAVE-003, SAVE-004, ARC-001, ARC-003, UI-003, UI-006, LOC-001, LOC-003, LOC-004, PERF-002, TEST-001
files:
  # ── Domain：政策契约（1）──
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Governance/InstitutionPolicy.cs
    type: M
  # ── Data：持久化 + 写路径（1）──
  - path: Source/PersonalChronicle/Data/PawnEconomy/InstitutionPolicySaveData.cs
    type: M
  # ── Economy：查询 / 购买链 / 自然决策点接入（3）──
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionPolicy.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyConsumption.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs
    type: M
  # ── UI：政策页显示与唯一提交路径（2）──
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Policy.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.StockTarget.cs
    type: M
  # ── 本地化（2）──
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  # ── 测试（4）──
  - path: Tests/InstitutionPolicyTests.cs
    type: M
  - path: Tests/InstitutionPolicyReadConsistencyTests.cs
    type: M
  - path: Tests/StockTargetTests.cs
    type: M
  - path: Tests/ProductionAuthorisationTests.cs
    type: M
  # ── 编译自动改写（1）──
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# 闭环 §6 第 5–7 条：衣物挂牌 → 购买 → 请求原版穿戴

**ChangeID**：`CHG-20261009-ECON-APPAREL`｜**日期**：2026-10-09｜**用户指令**：「进行闭环修复」

## 1. 对照文档（`00-总册与实施路线.md` §6 / §7）

| 文档条款 | 本批处置 |
|---|---|
| §6-5「公共售货点将一件公共衣物按固定内部价格**挂牌**」 | ✅ 政策新增 `ApparelPriceMinor`（零 = 未挂牌，显式） |
| §6-6「在允许的自然决策点购物；到店交付统一结算，随后**请求原版穿戴逻辑**；穿戴失败时保留已购买衣物」 | ✅ 购买链 + 结算 + `Pawn_ApparelTracker.Wear`；失败**不回滚**、物留背包并如实上报 `PendingUse` |
| §6-7「钱包、所有权、穿戴分别更新；只有**实际使用**产生的状态变化才能作为缺口改善证据」 | ⚠️ 部分：钱包/所有权/穿戴三者已分写；"改善证据回写目标"依赖 §6-4 的目标体系接入（**未做**） |
| §6-4「个人发现合法可穿戴衣物的缺口；钱不足 ⇒ 目标进入等待收入」 | ❌ **未做**（留下一批）：本批的缺口判据用于"买不买"，未接入 `ShadowPlan` 目标体系 |

## 2. 改动

| 文件 | 改动 |
|---|---|
| `Domain/…/InstitutionPolicy.cs` | 新增 `ApparelPriceMinor` 字段 + 构造器/`With` 参数（**无默认值**：漏传必须编译失败）+ `PublishesApparelPrice` |
| `Data/…/InstitutionPolicySaveData.cs` | `Entry` 加可选字段 `apparelPrice`（旧档无此键 ⇒ 读回 0 = 未挂牌）；载入校验；`Apply` 签名与两处写入分支（新建/更新）；`ToPolicy` 单点转换带上该字段 |
| `Economy/…/EconomyInstitutionPolicy.cs` | `ApplyInstitutionPolicy` 加第 11 参；新增 `QueryApparelPricePolicy(mapId)`（锚键序最低者胜，绝不平均） |
| `Economy/…/EconomyConsumption.cs` | 新增 `TryPurchaseApparelAtCounter`（与食品链同构：真报价 / 真库存 / 买方自有现金 / 人须在柜台；交割与结算原子、失败先退货再取消）；新增 `FirstWearableApparel`（合法可穿戴 + 非尸体衣物 + `FillsAWearableGap` 缺口判据）与 `TryWearFromInventory`（交给原版 `Wear`，失败落回背包并如实返回 false） |
| `Economy/…/EconomyEmployment.cs` | `OnPlanPublished` 在食品链不适用后尝试衣物链（**本批不接 M9 退避账**：衣物链失败多为"未挂牌/无库存"这类长期条件，逐次记账会刷满退避历史） |
| `Economy/…/Dialog_PawnEconomy.Policy.cs` | 政策行显示「衣物价」（未挂牌显式显示"未公布"）；第四行新增「衣物价 −/+」两个按钮；**所有既有按钮**（含库存目标菜单）显式透传当前衣物价；文本区 44→56、行距 +12、`PolicyRowHeight` 186→198（加行必须同步，见该常量注释） |
| `Economy/…/Dialog_PawnEconomy.StockTarget.cs` | 库存目标两级菜单透传衣物价（改库存目标不得清零已发布衣物价） |
| `Languages/{zh,en}/Keyed/PawnEconomy.xml` | +3 键：`Policy.ApparelPrice` / `.ApparelPriceDown` / `.ApparelPriceUp`（中英一一对应） |
| `Tests/{InstitutionPolicy,InstitutionPolicyReadConsistency,StockTarget,ProductionAuthorisation}Tests.cs` | 既有 28 处 `Apply(...)` 调用**显式补第 11 参**（`0L` = 确实不发布）；`InstitutionPolicyTests` +1 回归锁（能声明 / 能读回 / **改工资不得静默清零衣物价** / 未发布显式 false） |

## 3. 不变量与边界

- **价格面唯一提交路径**：衣物挂牌价只能经政策页「衣物价 −/+」按钮（`ApplyApparel`）写入；其它按钮一律显式透传当前值 —— 与 U6-A/U6-A2 的服务价、库存目标同一 P1 缺陷类防护（默认参数已移除，漏传即编译失败）。
- **交易原子性**：交割成功后才结算；结算失败先退货再取消扣款；退不回则报 `TransactionUnknown` 并**不**静默修好（沿用食品链既有纪律）。
- **穿戴失败不回滚**：结算已完成 ⇒ 衣物归买方（§6 第 6 条）；失败仅如实上报 `PendingUse`。
- **缺口判据取原版实况**：同类（同 def）已穿着 ⇒ 不构成缺口 ⇒ 不重复购买；不使用任何推测性磨损模型。
- **未知不当零**：未挂牌显式显示"未公布"并拒绝购买（`NoPublishedPrice`），不把 0 当作免费。
- **存档兼容**：新字段可选，旧档读回 0；zero新增 Scribe 节点、零 Migration。

## 4. 验证（全部实跑）

| 项 | 判据 | 结果 | 证据 |
|---|---|---|---|
| 编译 | 无 `error CS` / 警告 | **0 错 0 警** | `compile_with_sdk.ps1` |
| 全量离线测试 | dotnet test（net48） | **1191 通过 / 0 失败 / 1 跳过**（本批 +1；既有 28 处调用点补参后零回归） | `Tests/TestResults/Apparel-V2.trx` |
| 调用点完整性 | 依赖编译器"漏传即失败"逐个暴露 | 主工程 4 处 + 测试 28 处全部显式补参（脚本按错误行:列做括号平衡扫描，保留原 BOM/行尾） | 编译错误列表 → 逐轮收敛至 0 |
| 部署 | `deploy-to-game.ps1` | **已执行**（2026-10-10 00:02）：233 文件同步；发布区 DLL 与源**双侧 SHA-256 一致** `F1443B0B…C51969`（2,039.0 KB） | 脚本 stdout + 哈希比对 |
| GATE-DELTA | `gate-runner.ps1 -Base HEAD … -Blocking` | **PASS**（标注覆盖 14 个改动文件，无遗漏/超范围/残留） | 脚本 stdout；范围外清单 `.codebuddy/tmp/apparel-out-of-scope.txt` |
| 实机 | 挂牌 → 买 → 穿戴 | **NOT EXECUTED**（无游戏进程） | — |

## 5. 未做（如实登记，勿据本批声称闭环完成）

1. **§6 第 4 条**：着装缺口未接入 `ShadowPlan` 目标体系（"钱不足 ⇒ 目标等待收入"不成立）；
2. **R2 的"取消/恢复"**（部分购物的取消与恢复语义）；
3. **实机录像 + 存档证据**（R3 退出条件）；
4. 衣物链未接 M9 退避账（理由见 §2）。

---
AI 署名：本变更标注与实现由 AI 助手完成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
