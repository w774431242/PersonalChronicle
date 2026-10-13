---
change_id: CHG-20260919-ECON-UI-007
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent + 三路只读审计 subagent）
scope: [Source/PersonalChronicle/Application/SystemCenters/Builtin/**, docs/**]
files:
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FlowGraph.cs
    type: M
    symbols: BuiltinEconomyProvider.BuildFlowGraph（边的 Weight01 回填段）
    lines: 150-175
  - path: docs/设计文档/架构/审计-前后端数据对齐-2026-09-19.md
    type: A
    symbols: 审计报告
    lines: 1-200
  - path: docs/changelog/v1.4.x_2026-09-19_流向权重错位修复.md
    type: A
    symbols: CHG-20260919-ECON-UI-007
    lines: 1-120
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-007.md
    type: A
    symbols: CHG-20260919-ECON-UI-007
    lines: 1-120
---

# 变更标注 · CHG-20260919-ECON-UI-007（流向权重索引错位修复 + 对齐审计）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-ECON-UI-007`
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-ECON-UI-007
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent；三路 code-explorer 并行审计 → 主 Agent 逐条复核高危断言 → 修复）
- Scope(摘要): 修复 `Weight01` 索引错位（错误数字正被画到屏幕）；产出前后端数据对齐审计报告

### 一句话动机
用户要求「审计当前前端项目与后端数据对齐程度」。审计本意是查"前端有没有正确消费后端数据"，
过程中发现**后端给前端的那个数字本身就是错的** —— 属"对齐"问题的最坏形态，故本轮先修它、
再交付审计报告。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `Application/SystemCenters/Builtin/BuiltinEconomyProvider.FlowGraph.cs` | M | `BuildFlowGraph` 的边构建 + `Weight01` 回填段 | 150-175 | 权重归一化改为**就地**（`snapshot.Edges[i].Weight01 /= maxFlow`），**不再回读入参 `edges[i]`**；`Weight01` 先存本条 baseline 作占位并把负值夹 0 | GOV-009 / DATA-001 | DATA |
| `docs/设计文档/架构/审计-前后端数据对齐-2026-09-19.md` | A | 审计报告 | 1-200 | 三向对齐 / UI 派生合规度 / 非经济面板真实性 / 已修 / 未修清单（P0-P3）/ 复核方法 | REL-001 / GOV-013 | REL |
| `docs/changelog/v1.4.x_2026-09-19_流向权重错位修复.md` | A | `CHG-20260919-ECON-UI-007` | 1-120 | 更新日志：修复说明 + 影响链 + 审计核心结论 + 4 项残余风险 | REL-001 / REL-004 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-007.md` | A | `CHG-20260919-ECON-UI-007` | 1-120 | 本文件自身（GATE-DELTA 要求标注覆盖**全部**实际改动文件，含标注自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步至发布区
> （MD5 `01D84BE1E846D737A5D1CFFEB63A88E9`，两区一致）。

### 关键 diff 片段

```diff
// BuiltinEconomyProvider.FlowGraph.cs  （子集下标取全表 ⇒ 整体错位）
-                    if (e.Baseline > maxFlow) { maxFlow = e.Baseline; }
+                    // ⚠ Weight01 先存**本条自己的 baseline** 作占位，待本循环结束后就地归一化。
+                    edge.Weight01 = e.Baseline > 0f ? e.Baseline : 0f;
+                    if (e.Baseline > maxFlow) { maxFlow = e.Baseline; }
                     snapshot.Edges.Add(edge);
                 }
                 if (maxFlow > 0f)
                 {
-                    for (int i = 0; i < snapshot.Edges.Count; i++)
-                    {
-                        EconomyFlowEdge e = snapshot.Edges[i];
-                        float baseline = edges[i] != null ? edges[i].Baseline : 0f;   // 🔴 跨表取下标
-                        e.Weight01 = baseline / maxFlow;
-                    }
+                    // 🔴 就地归一化，不得回读入参 `edges`：snapshot.Edges 是**过滤后的子集**
+                    //（一端被封顶截掉的边被 continue 跳过、超 MaxFlowEdges 者不进），
+                    // 其下标与 edges 不对应；旧写法只要有一条边被跳过，其后权重就整体张冠李戴。
+                    for (int i = 0; i < snapshot.Edges.Count; i++)
+                    {
+                        snapshot.Edges[i].Weight01 /= maxFlow;
+                    }
                 }
```

### 审计报告的核心结论（供快速索引）

| 维度 | 对齐度 | 要点 |
|---|---|---|
| Read Model 三向对齐 | ≈80~93% | **0 个「无理由未填字段」**；18 个孤儿字段（填了没人读） |
| UI 只画不派生 | 92%（`Economy/UI`）~ 50%（`MasterControl`） | 迁移未完成，非机制缺失 |
| 数据真实性 | 高 | **无伪造数值型假数据**（除 DevMode 假数据工厂） |
| 降级健壮性 | 🟠 3 处缺口 | 3 个 `AppendKpis` 无 try/catch ⇒ **一整页空白** |

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏（生成脚本自检 declared = hit）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— 003~006 批次遗留文件由 `-ExcludeFromFile` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致（`CHG-20260919-ECON-UI-007`）
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 断言（来自 subagent 报告） | 求证方式 | 结论 |
|---|---|---|
| `Weight01` 回填存在跨表下标错位 | 主 Agent 直读 `FlowGraph.cs:134-169` 全文（边过滤 `continue` 在 `:148`、截断在 `:136`、回填在 `:161-169`） | ✅ **证实** |
| `KpiSectionTitle` 是死链 | 全仓检索 `KpiSectionTitle`（8 处命中：1 接口 + 5 实现 + 1 声明 + 1 赋值；**0 处 UI 读取**） | ✅ **证实** |
| `AppendKpis` 无 try/catch ⇒ 整页空白 | 直读 `BuiltinLogisticsProvider.cs:41-70`（无 catch）+ `SystemCenterReadModelProvider.cs:230-233`（在整体 try 内）+ `:404-408`（唯一 catch 置 `IsEmpty=true`） | ✅ **证实** |

> 三条最高危断言**全部经主 Agent 复核证实**后才写入报告 —— 未复核的 subagent 结论一律标注为粗估。

### 回滚定位
1. **退回错误算法**：还原 `FlowGraph.cs` 的回填段（**不建议**，那会重新产生错位权重）。
2. **只退文档**：删除审计报告与 changelog，代码保持修复态（文档与代码无运行期耦合）。
