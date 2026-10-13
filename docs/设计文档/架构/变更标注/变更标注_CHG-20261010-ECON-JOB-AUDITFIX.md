---
change_id: CHG-20261010-ECON-JOB-AUDITFIX
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: Implemented
supersedes_claim: CHG-20261010-ECON-JOB-INTENT 的「精准识别」表述降级为「主要路径精准 + 已登记边界」
scope: 用户「最佳修复方案」—— 落实两位 Gate 独立审计对识别边界/数量/精准的发现
rule_ids: AI-001, AI-003, ARCH-004, COMP-004, SAVE-003, PERF-001, TEST-001, DELTA-001
files:
  - path: Source/PersonalChronicle/Capture/Patch_DesignationSource.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyVanillaWork.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyVanillaWorkSource.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
---

# 审计修复批 · CHG-20261010-ECON-JOB-AUDITFIX

**ChangeID**：`CHG-20261010-ECON-JOB-AUDITFIX`｜**日期**：2026-10-10｜**作者**：AI（CodeBuddy / DeepSeek-V4.1-Flash）
**用户指令**：「最佳修复方案」（承接审计：识别边界 / 数量 / 精准三维度）

---

## 一、方案的核心设计（为什么这是"最佳"）

审计暴露两条互相冲突的诉求，**单一集合无法同时满足**：

| 诉求 | 集合语义 |
|---|---|
| **待办**要精确反映**玩家意图**（宁少不多） | 需要**玩家白名单** |
| **计酬**要"工人确实干了活就付"（不能因漏记而拒付） | 需要**自动黑名单** |

⇒ 最终方案：**两套集合各司其职**。
- `playerDesignations`（白名单）：待办计数用；
- `autoDesignations`（黑名单，**本轮新增**）：**计酬排除**用；
- 两者**互斥维护**（同一条下达只落一边）。

这样：自动清障/枯萎/树桩/矿脉续标的劳动**不再付酬**（消除错付），而"玩家经非设计器路径下达"（如 `Plant.GetGizmos` 的「全砍枯萎」——它不进白名单**也不在**黑名单）**仍会付酬**（不误伤）。

## 二、审计发现 → 处置对照

| 审计项 | 来源 | 处置 |
|---|---|---|
| **P1 计酬不校验来源**（自动劳动照付） | 两席**独立**确认 | ✅ 修：`ReportVanillaWorkCompletion` 增加"目标键 ∉ autoDesignations"过滤（植物按 `CutPlant`/`HarvestPlant` 的 Thing 键，矿按 `Mine`/`MineVein` 的 Cell 键） |
| **P2 矿脉自动延伸被误纳为玩家** | A 席（穷举 33 个调用点） | ✅ 修：判据改为"栈内存在 **Designator 的实例方法帧**" —— `FloodFillDesignations` 是 **static** 却声明在 Designator 子类上，只认实例帧即可排除；玩家矿脉路径仍覆盖（其实例帧命中于第 7 帧） |
| **P3 帧预算余量仅 0–1 帧** | A 席 | ✅ 修：8 → **12 帧**（命中即停） |
| **P3 上限静默丢弃 + 触顶 O(n²)** | B 席 | ✅ 修：丢弃时 `Log.WarningOnce`（可观测） |
| **P3 载入侧不封顶** | B 席 | ✅ 修：`FillFromWire` 在载入时二次封顶 |
| **P3 待办上限合计可达 2×cap** | B 席 | ✅ 修：第二段传**剩余预算**，合计严格 ≤ cap |
| **P3 缓存无失效点（"立即"实为 ≤60 tick）** | A/B 两席 | ✅ 修：来源集合变化时 `InvalidateVanillaBacklog()` ⇒ **取消/下达立即反映** |
| **P3 三处陈旧文案/注释** | 两席 | ✅ 修：`EconomyVanillaWorkSource` 注释、玩家文案补"旧档不追溯" |
| **「全砍枯萎」玩家命令漏记** | A 席 | ⚠ **不修**（改为不误伤）：它落在"非白名单且非黑名单"⇒ **计酬照付**；不列入待办属**已知边界**（下一批若要做需在 gizmo 路径补来源标记） |
| 其余假阴性（动物栏 / Flick / 飞船回放 / Blueprint） | A 席 | 📝 登记：均不在当前计酬工种内 |
| 第三方以 Designator 子类做自动投放 | A 席 | 📝 登记为**不可区分**的外部风险（无通用修复） |
| 跨图 Cell 键无地图维度 | A/B 两席 | 📝 登记（原版自动 Mine 源≈0，实务概率极低；引入地图维度属存档语义再变更，需另立项） |
| 单测覆盖（键构建/去重/封顶/往返） | B 席 | 📝 **未做**，B 席已给出 6 条最小用例清单，建议下批补 |

## 三、验证

- 编译 **0 错 0 警**；`dotnet test` **1286 通过 / 0 失败 / 1 跳过**；部署成功。
- **NOT RUN**（实机）：需重启后核对 —— ① 取消一条 ⇒ 待办**立即**−1（不再等 1 秒）；② 风电机自动割除**不进**待办、其完成**不再计酬**；③ 自己下达的标记照常计酬；④ 读档后白/黑名单都保留。

## 四、主张更正（诚实登记）

`CHG-20261010-ECON-JOB-INTENT` 的「**精准识别**」为**过强表述**，经两席独立审计证伪并已在本文档更正为：
> **主要路径精准（33 个原版调用点已穷举甄别，30 条玩家路径正确识别、全部自动来源正确排除）+ 已登记边界缺口（「全砍枯萎」等 4 条漏记路径、第三方不可区分投放、跨图 Cell 键）。**

## 五、回滚

`git checkout -- <6 路径>`；存档新增 `pawnEconomicAutoDesignations` 节点随档保留（无害，仅不再更新）。

### 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖实际改动全部文件（6/6）
- [x] 清单范围 ⊆ Scope
- [x] Change ID 与审计发现逐条可互链
- [x] 无调试代码 / TODO 残留
- [x] 已用 `gate-runner.ps1` 自动校验
