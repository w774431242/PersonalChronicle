---
change_id: CHG-20261009-ECON-M10-AUTH
date: 2026-10-09
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedAndDeployedByThisBatch
scope: U6-A —— M10「已授权未开工」授权账落地（持久化 + 读数接线 + 治理提交面）+ 政策链默认参数 P1 受控修复
rule_ids: AI-001, AI-003, AI-011, AI-014, GOV-006, GOV-011, GOV-012, GOV-013, DATA-001, DATA-004, DATA-007, SAVE-001, SAVE-002, SAVE-003, SAVE-004, SAVE-006, SAVE-007, ARC-001, ARC-005, ARC-008, MATRIX-001, UI-001, UI-004, UI-008, LOC-001, LOC-002, LOC-003, PERF-001, TEST-001, COMP-001
files:
  # ── 新增 · Domain / Data / Economy / Tests（5）──
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Production/ProductionAuthorisation.cs
    type: A
  - path: Source/PersonalChronicle/Data/PawnEconomy/ProductionAuthorisationSaveData.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyProductionAuthorisation.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Authorisation.cs
    type: A
  - path: Tests/ProductionAuthorisationTests.cs
    type: A
  # ── 修改 · 授权账与接线（7）──
  - path: Source/PersonalChronicle/Application/PawnEconomy/Production/WorkshopExecutorPorts.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Production/EconomyWorkshopExecutor.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Production/EconomyProductionReadoutSource.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyProductionFacade.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Overview.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Policy.cs
    type: M
  # ── 修改 · P1 政策链默认参数移除（3）──
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Governance/InstitutionPolicy.cs
    type: M
  - path: Source/PersonalChronicle/Data/PawnEconomy/InstitutionPolicySaveData.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionPolicy.cs
    type: M
  # ── 修改 · 本地化（2）──
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  # ── 修改 · 测试（3）──
  - path: Tests/EconomyProductionTests.cs
    type: M
  - path: Tests/InstitutionPolicyTests.cs
    type: M
  - path: Tests/InstitutionPolicyReadConsistencyTests.cs
    type: M
  # ── 修改 · 进度源与生成物（2）──
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/实施批次状态.json
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# U6-A · M10 授权账与读数接线（含 P1 政策链受控修复）

**ChangeID**：`CHG-20261009-ECON-M10-AUTH`（U6-A）｜**日期**：2026-10-09｜**授权**：用户「继续完整实装」+ 总控 AI-001 入口裁定

## 1. 背景与裁定

`变更标注_CHG-20261009-ECON-P1-SERVICEPRICE.md` 的「未做」段引用「M10 最小 slice 缺口 1+2（规格已就绪，见 docs/设计文档/架构/变更标注/ 内 M10 评估结论）」，但该评估文件**全仓检索不存在（引用悬空）**。总控 `pc-orchestrator` 按 M10 分册 §4（`:44-56`）+ 代码现状裁定：

| 缺口 | 输入域 | 本轮落地 |
|---|---|---|
| 1 | `AuthorisedNotStarted` 已授权未开工量 | 模块自有**授权计划账**（既有 `RestockProposalLedger` 持久化）+ 与观察在制的品类级收敛 |
| 2 | `FreeStockTarget` 自由库存目标 | **未落地**（属机构政策声明字段，需 UI + Save 扩展）⇒ 列为 U6-A2，输入保持显式 Unknown |

**未落地项如实声明**：`ConfirmedUndeliveredDemand`（属 M7 订单域）、`AuthorisedUndeliveredExports`（属出口域）仍显式 Unknown；`FreeStockTarget` 仍 Unknown。⇒ `RestockEvaluator` 顶层仍会短路返回 Unknown，**本批不得对外表述为"补货评估可用"**。

## 2. 改动

| 文件 | 改动 |
|---|---|
| `Domain/…/Production/ProductionAuthorisation.cs`（新） | 收敛规则 `EffectiveNotStarted = max(0, 账内授权 − 观察在制)`、`Sum`（溢出拒绝不 clamp）、`IsExpired`（`>` 边界；tick 回退/未知 tick 一律不过期）、原型年龄上限 `MaximumAuthorisationAgeTicks = 120000`（2 游戏日，待剖析）、诊断码常量 |
| `Application/…/Production/WorkshopExecutorPorts.cs` | 账改行模型 `RestockProposalRow{ProposalId,Receipt,AcceptedTick}`；**回执确定性化**（`restock-v1:` + ProposalId，替换原含插入序号的 `restock:{count}:{id}` —— 后者读档后序号漂移会让"同键返原回执"跨档失效）；`TryRelease` / `TrimExpired` / `AuthorisedAmount` / `ExportRows` / `ImportRows`（先清后载、结构校验、单一上界 16、按 tick 保留最新）/ `TryParseProposalId`（`map\|category\|amount` 正典；⚠ 必须走 `Split(char[])` 重载，net48 无 `Split(char, StringSplitOptions)`） |
| `Data/…/ProductionAuthorisationSaveData.cs`（新） | 加法节点容器：`version` + 行表（≤16，与运行时**同一上界**）、PostLoadInit 过滤空行/损坏行、**不解析 receipt**（不透明串原样往返） |
| `Economy/…/EconomyProductionAuthorisation.cs`（新） | 落盘桥：导出在 Scribe_Deep **之前**、恢复在 **PostLoadInit**（M9-S6 范式）；`TryQueryAuthorisedNotStarted`（不可读 ⇒ false ⇒ 调用方必须 Unknown，绝不填 0）；250 tick 年龄裁剪 |
| `Economy/…/EconomyRuntime.cs` | 新节点 `pawnEconomicProductionAuthorisations`（与既有 10 节点无重名）+ 250 tick 裁剪调用 |
| `Economy/…/Production/EconomyProductionReadoutSource.cs` | `AuthorisedNotStarted` 由 Unknown 改为 **Known(收敛值)**；不可读时仍 Unknown |
| `Economy/…/Production/EconomyWorkshopExecutor.cs` | 账单一实例暴露（导出/恢复同一对象，无双头账）；受理时写入当前 tick |
| `Economy/…/Dialog_PawnEconomy.Authorisation.cs`（新） | 治理提交面（**唯一提交路径**，经 Guard）：政策页「授权生产 / 撤销授权」两个按钮 + FloatMenu；诊断码走 `LocalizedId("Reason.")`，品类名走 `Def` 标签（DefInjected 管线），撤销失败有反馈 |
| `Dialog_PawnEconomy.Policy.cs` / `.Overview.cs` | 政策页按钮行（常量化的 y=42/高度 28）；总览补货输入行补「已授权未开工」、授权计数改用 `ProductionAuthorisationCount` |
| `Domain/…/InstitutionPolicy.cs` / `Data/…/InstitutionPolicySaveData.cs` / `Economy/…/EconomyInstitutionPolicy.cs` | **P1 受控修复**：第 9 参 `servicePricePerUnitMinor` 的默认值从**三处**（构造器 / `With` / `Apply` / `ApplyInstitutionPolicy`，共四处签名的默认值口径统一取消，见 §5 勘误）移除 ⇒ 政策编辑漏传将**编译失败**而非静默清零服务价；对话框 8 个按钮全部显式透传当前值 |
| `Languages/{zh,en}/Keyed/PawnEconomy.xml` | +15 键（授权 9 + 撤销拒绝 1 + 诊断码 4 + `Overview.Authorised` 1 计 9+1+4+1=15），改值 2（`Proposals` → 授权/authorisations、`NotWiredInputs` 去掉"未开工"）；中英 168/168 一一对应 |

## 3. 不变量与边界

- **零第二执行器**：受理/落盘/裁剪均不建 Bill、不改排班；开工由原版 `IBillGiver.BillStack` 观察进入既有在制桶。
- **同一量只扣一次**：账内授权与观察在制按品类收敛（`max(0, 授权 − 在制)`）。⚠ **偏离 M10 分册 §4「已授权计划与相同订单/出口键关联」**：被关联对象（订单/出口键）尚不存在，本批为品类级最小实现；**EXC 待登记**（见 §6）。
- **未知不当零**：账不可读 ⇒ Unknown；三个无来源输入仍 Unknown；空账 = Known(0)（账即权威，SAVE-003）。
- **分层**：Domain 纯规则（零 Verse）；Application 不引 Economy/Automation/Data；UI 只经组件门面；Data 仅存储（不认识 Application 类型）。

## 4. 验证基线（全部实跑）

| 项 | 判据 | 结果 | 证据 |
|---|---|---|---|
| 编译 | 输出无 `error CS`（退出码不可信） | **0 错 0 警** | `.codebuddy/tmp/u6a-build2.log`；`_scripts/compile.rsp` 含 4 个新源文件（防 glob 静默跳过） |
| 全量离线测试 | dotnet test（net48 运行期） | **1160 通过 / 0 失败 / 1 跳过（总 1161）**（基线 1152/0/1，+8 新用例） | `Tests/TestResults/U6A-Auth-Full-V3.trx` |
| 该批用例的"牙齿" | 中间运行真实失败 | 中间版本 `U6A-Auth-Full.trx` 本组 **4 条真失败**（`MissingMethodException: Split(Char, StringSplitOptions)`，netstandard2.1 重载在 net48 缺失）⇒ 修复后全绿 | 两份 trx 对照 |
| 语言文件 | 良构 + 键集一致 | XML 良构、168/168 键一一对应、键无重复 | GATE-LOC 独立复核 |
| 存档节点 | 与既有节点不重名 | 11 节点全互异；导出/Scribe/恢复顺序符合范式 | GATE-TEST 独立复核（`EconomyRuntime.cs:49-52`） |
| 部署 | deploy-to-game.ps1 | Defs 36 / Languages 66 / Assemblies 1 全 synced；目标 DLL 2026-10-09 22:38:56 | 脚本输出 |
| **实机** | 重启游戏后读档/授权/裁剪 | **NOT EXECUTED**（沙箱无游戏进程） | — |

## 5. 勘误与已修缺陷（本轮自曝）

1. **net48 BCL 面**：`string.Split(char, StringSplitOptions)` 编译能过、net48 运行期抛 `MissingMethodException` —— 已改为 `Split(new[] {'|'})` 并写入测试反证。**这是本工程第 2 次踩同类陷阱**（前次 `GetValueOrDefault`）。
2. **回执稳定性**：原回执串含插入序号 ⇒ 读档重建后序号漂移、跨档"同键返原回执"失效、满额拒绝的负向断言也失去意义。已改确定性回执。
3. **持久化 + 满额拒新 = 永久锁死**：内存账时代"读档自然清空"的性质在落盘后消失，故必须补年龄裁剪（250 tick 检查、上限 2 天）。
4. **P1 存量缺陷（受控修复）**：政策对话框 8 个非服务按钮经 8 参 `Apply` 会静默清零已发布服务价。默认值已在 **4 处签名**统一取消（`InstitutionPolicy` 构造器 / `With` / `InstitutionPolicySaveData.Apply` / `GameComponent_PawnEconomy.ApplyInstitutionPolicy`），并补用例 `UpdatingOnePolicyFieldCannotSilentlyClearTheServicePrice` 与 12 个既有调用点显式传参。
5. **标注自曝偏差**：本批文件数 **22**（不含 `Assemblies/*.dll`，gate-runner 自动跳过 `.dll`）。除上表逐项点名者外，另有 8 个在 front matter 声明、按角色归并说明：`Tests/ProductionAuthorisationTests.cs`（新增夹具 8 用例）、`Tests/EconomyProductionTests.cs`（既有账用例改签名 + 确定性回执断言）、`Tests/InstitutionPolicyTests.cs` 与 `Tests/InstitutionPolicyReadConsistencyTests.cs`（第 9 参显式化共 13 个调用点）、`Economy/PawnEconomy/EconomyProductionFacade.cs`（`WorkshopExecutorTyped` 单实例门面，防双头账）、`Dialog_PawnEconomy.Overview.cs`（读数行补「已授权未开工」+ 授权计数）、`实装落地/实施批次状态.json`（进度源）、`Source/PersonalChronicle/BuildStamp.g.cs`（编译自动改写，按先例列入）。

## 6. 待办 / 例外 / 三态

- **未做**：`FreeStockTarget`（U6-A2，需机构政策声明字段 + UI + Save 扩展）；`ConfirmedUndeliveredDemand` / `AuthorisedUndeliveredExports`（待 M7 订单域/出口域）；机构级读数绑定（仍地图级，已在 UI 如实标注）；`Overview.Proposals` 之外的趋势/治理面（M12）。
- **EXC 待登记（需 PM 批准后落盘）**：品类级收敛偏离 M10 §4 的"计划-工单键关联"；scope = 该推断；到期 = 未来实现计划-工单键关联单元。**落盘路径 `docs/设计文档/架构/EXC-2026-030.md` 属新建文档，受 DOC-004 前置门约束，本轮未落盘**（GOV-012 不静默绕过）。
- **做不到**：0。
- **NOT EXECUTED**：实机读档往返、游戏内 tick 耗时/FPS/GC、语言切换、Vanilla 启动与 Mod 组合 —— 全部属验收单元 `CHG-20261008-ECON-ACCEPT`。
- **后续建议（下行单元）**：U6-A2（库存目标）→ U6-B（M8 服务供给机制）→ U6-C（M7）→ U6-D（M2/M3 §6 端口、M6 退出 UI、M9 指纹、M12）。

---
AI 署名：本变更标注与实现由 AI 助手完成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。
