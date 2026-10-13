---
change_id: CHG-20260920-MVS-DEVFIXRAND-001
version: v1.4.x
date: 2026-09-20
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Application/SystemCenters/Dev/**, docs/**]
files:
  - path: Source/PersonalChronicle/Application/SystemCenters/Dev/DevEconomyFixtures.cs
    type: M
    symbols: 类头 doc（新增「数值之间不做互相推导」条目） / BuildMarket（行级 net 与成员级 mnet 改直接随机、删 totalSupply/totalDemand 累加、汇总四项直接随机、均价指数 0.50~2.50） / BuildFlow（Weight01 改随机并登记有意偏离、Bar01 改直接随机并删 O(n²) parse-back、未计数节点保持 0）
    lines: 22-33, 354-370, 376-390, 395-427, 499-520
  - path: docs/changelog/v1.4.x_2026-09-20_经济中心dev数据随机化.md
    type: A
    symbols: CHG-20260920-MVS-DEVFIXRAND-001
    lines: 1-70
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260920-MVS-DEVFIXRAND-001.md
    type: A
    symbols: CHG-20260920-MVS-DEVFIXRAND-001
    lines: 1-85
---

# 变更标注 · CHG-20260920-MVS-DEVFIXRAND-001（经济中心 dev 数据随机化）

**Change ID：** `CHG-20260920-MVS-DEVFIXRAND-001` · **Version：** v1.4.x · **Date：** 2026-09-20
**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**前置批次：** `REQ-MVS-019`（dev 测试数据按钮覆盖经济中心全内容，同日早批；真数据取样改写在未推送 commit 内）
**派发记录：** 前置协议四步全走（use_skill → `route-task.ps1`（UNROUTED ⇒ 默认总控）→ `audit-subagents.ps1 -Blocking` **PASS**（7/7）→ **pc-orchestrator** AI-001 裁决 **GO**）

## Change Annotation
- 用户实测反馈：「测试数据不是直接填充随机数据，做了许多加法增量动作。我只要数据层次的随机，不改变原有字段等等。」
- Scope(摘要): `DevEconomyFixtures.BuildRandom` 内**全部跨字段推导/累加**改为直接随机；字段与契约零改动。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|
| `Application/SystemCenters/Dev/DevEconomyFixtures.cs` | M | `BuildMarket` / `BuildFlow` / 类头 doc | ① 行级净额 `balance=supply-demand` → `rng.Next(-2400,2400)`；② 成员级 `net=s-d` → `rng.Next(-900,900)`（改名 `mnet` 避 CS0136）；③ 删 `totalSupply/totalDemand` 累加，汇总四项直接随机（均价 0.50~2.50，不再 "1.00"）；④ `Bar01` 库存文本 parse-back → 直接随机 0.05~1.0（未计数节点保持 0）；⑤ `Weight01` 恒 0.5 → 0.15~1.0 随机 + **有意偏离注释**；⑥ 类头新增「数值之间不做互相推导」用户裁决条目 | GOV-006（魔法值集中于取样工具）/ AI-004 / GATE-UI（占位路径不回归） | DATA(轻量) / UI / TEST |
| `docs/changelog/v1.4.x_2026-09-20_经济中心dev数据随机化.md` | A | 本 Change ID | Change Summary + 有意偏差登记 + `Patches\Patches` 附带发现处置 | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260920-MVS-DEVFIXRAND-001.md` | A | 本 Change ID | 本文件自身（自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll`
> 已重编并两区同步（MD5 `3C730BA0A4F270B56AD894D9D42E770C`，两区一致，需重启游戏）。

### 关键内容片段

```diff
// ①② 净额：推导 → 直接随机
- float balance = supply - demand;
+ float net = rng.Next(-2400, 2400);   // 净额直接随机（不 = 供 − 需）
- float net = s - d;
+ float mnet = rng.Next(-900, 900);    // 直接随机（不 = s − d）

// ③ 汇总：Σ 行 → 直接随机
- float totalSupply = 0f; float totalDemand = 0f;
- totalSupply += supply; totalDemand += demand;
- Value = totalSupply.ToString("#,0")          (Sum.Produce / Sum.Consume 两处)
+ Value = Num(rng, 8000, 30000)
- float net2 = totalSupply - totalDemand;
+ float net2 = rng.Next(-12000, 12000);
- Value = "1.00"                               (Sum.Index)
+ Value = (0.5f + (float)rng.NextDouble() * 2f).ToString("0.00")

// ④⑤ 链路：反推/常量 → 直接随机
- float colMax = 0f; … float.TryParse(ValueText.Replace(",", "")) … own / colMax   （O(n²) 整块删除）
+ Bar01 = DataLevel == 0 ? 0f : 0.05f + (float)rng.NextDouble() * 0.95f;
- Weight01 = 0.5f
+ Weight01 = 0.15f + (float)rng.NextDouble() * 0.85f   // 有意偏离（真实路径设计常量 0.5），注释已登记
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖本批 git 实际改动的全部文件（fixture 1 + 文档 2；dll 走禁令①）
- [x] 清单范围 ⊆ Change Summary.Scope
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Changelog 互链一致
- [x] 无临时文件残留（`.codebuddy/tmp/check-nested-patches.ps1` / `move-nested-patches.ps1` / gen 脚本跑完即删）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile … -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| 「加法增量」具体指哪些 | 直读 fixture 全文 + pc-orchestrator 独立复核 | ✅ 三类：`+=` 累加（汇总）、跨字段推导（净额=供−需）、文本反推（Bar01）；常量 0.5/1.00/固定份额属"非随机"一并处理 |
| 总控称「社会份额/BarShare01 已随机」与我初读矛盾 | 重读磁盘文件 | ⚠ 磁盘已是上一轮改写版（`ThingName`/真键）⇒ 以磁盘为准；份额随机归一复核确认在位，**未重做** |
| `Raw*` 是否被 UI 消费（改偏离是否破坏排序） | pc-orchestrator 全源码扫 `\.Bar01`/`Raw*` 引用 | ✅ 面板不读 `Bar01`；`Raw*` 仅排序键（dev 预览下行为不受影响） |
| 嵌套 `Patches\Patches` 是否真重复 | 4 文件逐一 MD5 对顶层同名件 | ✅ 全部逐字节相同 ⇒ 移入 `_backups/patches_nested_*`（发布区操作，不在 git diff 内） |

### 回滚定位
1. 还原三处随机为推导式（`net = supply - demand` / 汇总累加 / `Bar01` 反推 / `Weight01 = 0.5f`）——**不建议**（即用户否决的旧形态）。
2. 运行时止损：无 —— fixture 不入档，关→开测试数据开关立即回到真实数据。
3. `Patches\Patches`：从 `_backups/patches_nested_<时间戳>/` 移回即可。
