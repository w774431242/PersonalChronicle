---
change_id: CHG-20260920-MVS-SOCIETY-001
version: v1.4.x
date: 2026-09-20
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Economy/UI/**, Source/PersonalChronicle/Application/SystemCenters/**, Source/PersonalChronicle/Application/ReadModels/**, docs/**]
files:
  - path: Source/PersonalChronicle/Economy/UI/EconomySocietyPanel.cs
    type: M
    symbols: 删 BlockSectionTraits/BlockQa/BlockChips 常量与 DrawQa 及 .qa 四常量 / BuildPlan 回归 void 并删 width 参数（计划与宽度无关，滚动分支不再重建）/ DrawPlan 去参删两 case / DrawPurchaseRow 条色 TierTone→UITheme.Alive / 类头偏离登记
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.Society.cs
    type: M
    symbols: 删 AppendDynamicMarketTraits 调用与 TraitsSectionTitle 填充 / AppendDerivedChips 删 FactorsTip 组装 / 删 FactorText 死方法（复审抓出）/ 新增 ThingLabelOf（ThingDef→LabelCap）/ 收购行 KindLabelOf→ThingLabelOf / map 参数闲置声明 / 注释同步（复审例外②③④）
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs
    type: M
    symbols: 删 AppendDynamicMarketTraits 方法本体（供需净值/本方仓储/市场准入三条）与专属 FindFactionByDefName 副本
  - path: Source/PersonalChronicle/Application/ReadModels/EconomySocietySnapshot.cs
    type: M
    symbols: 删 TraitsSectionTitle/TraitsBadge/FactorsTip/MarketTraits 四字段与 SocietyQa 类（Chips 先例：读模型死字段删除）/ RowIndex 与 TierIndex 注释修正（0 基）
  - path: Source/PersonalChronicle/Application/SystemCenters/Dev/DevEconomyFixtures.cs
    type: M
    symbols: 停填已删字段 / TierIndex 1 基→0 基（与真实 Provider 同基准）/ headcount 取键对齐 Scale.Headcount* / 聚合行注释修正
  - path: docs/changelog/v1.4.x_2026-09-20_移除市场特征与收购项优化.md
    type: A
    symbols: CHG-20260920-MVS-SOCIETY-001
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260920-MVS-SOCIETY-001.md
    type: A
    symbols: CHG-20260920-MVS-SOCIETY-001
---

# 变更标注 · CHG-20260920-MVS-SOCIETY-001（移除市场特征 + 收购项优化）

**Change ID：** `CHG-20260920-MVS-SOCIETY-001` · **Version：** v1.4.x · **Date：** 2026-09-20
**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**派发链：** `route-task.ps1`（UNROUTED ⇒ 默认总控）→ `audit-subagents.ps1 -Blocking` PASS → **pc-orchestrator** AI-001 裁决（方案 A/B 核验通过 + 2 修正：`FactorText` 随批删、快照字段按 Chips 先例删）→ 实施后 **pc-ui-loc-guard**（GATE-UI/LOC）+ **pc-data-steward**（GATE-DATA/SAVE）双复审 → 复审例外 7+1 条与观察项 2 条**已随批全部消化**。

## Change Annotation
- 用户两指令：「移除市场特征」（社会背景面板整段）、「优化据点收购项」（截图实测 defName 泄漏 + 彩虹色条噪声）。

### 改动清单（逐文件）
详见 changelog 同名文件「变更文件清单」；每个代码文件的符号级明细见上表 `symbols`。

### 关键内容片段

```csharp
// ① 收购行显示名：PawnKindDef 查找（错）→ ThingDef 查找（对）
- row.Label = KindLabelOf(entry.ThingDefName);   // 动物碰巧同名才"正常"，纯物品泄漏 defName
+ row.Label = ThingLabelOf(entry.ThingDefName);  // ThingDef → LabelCap → 兜底 defName

// ② 收购条：色环分类色（错语义）→ 单一语义色
- TierTone(row.RowIndex)
+ UITheme.Alive   // 与规模条同为"份额/覆盖"语义；RowIndex 保留供聚合行判定

// ③ BuildPlan：派生量随段消失 ⇒ 回归 void、计划与宽度无关
- private static float BuildPlan(EconomySocietySnapshot snapshot, float width)
+ private static void BuildPlan(EconomySocietySnapshot snapshot)
  //（滚动分支不再用滚动宽重建计划 —— 旧宽度依赖项 chips 换行 / .qa 列宽均已不存在）
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖本批 git 实际改动的全部文件（5 代码 + 2 文档；dll 走禁令①）
- [x] 清单范围 ⊆ Change Summary.Scope
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Changelog 互链一致
- [x] 无临时文件残留（gen 脚本跑完即删；out-of-scope 清单按惯例保留在 `.codebuddy/tmp/`）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile … -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| defName 泄漏根因 | 读 `KindLabelOf`（PawnKindDef 查找）+ `FactionPurchaseEntry.ThingDefName` 语义（FactionPurchase.cs）+ 截图逐行比对（动物正常/纯物品泄漏） | ✅ 用错 Def 类型，非翻译缺失 |
| 四因子无其它 UI 出口 | pc-orchestrator 全工程扫 `.FactorsTip`/`Chip.*` 消费链 | ✅ 唯一出口 = 市场特征段头 tooltip ⇒ 随段有意消失 |
| 已删成员零残留 | pc-data-steward 全工程（含 Tests/）grep + 编译 Rebuild 0/0 | ✅ 零代码引用 |
| 快照删字段 vs 留注释 | pc-orchestrator 指出与"改注释不删字段"旧例冲突；data-steward 复核 `ArchiveSectionSnapshot` 为刷新节奏重建的 UI 读模型（非持久化契约）+ 同文件 Chips 先例 | ✅ 删除合规，SAVE N/A |
| 收购条色令牌选型 | pc-orchestrator 排除 Info（Epic 下与 Accent 撞色 0.13）/Muted（2px 条隐形）/Dead/Blood（负面语义） | ✅ Alive 为令牌集内最优 |

### 回滚定位
1. 恢复「市场特征」段：从 git 历史取回 `AppendDynamicMarketTraits` / `DrawQa` / `.qa` 常量 / 快照四字段与 `SocietyQa` 类，接回 `BlockSectionTraits`(=4)/`BlockQa`(=8)（编号保留未重排）；语言包键原样在位。
2. 收购行名称回退 = 改回 `KindLabelOf`（**不建议** —— 即 defName 泄漏缺陷本身）。
3. 色条回退 = 改回 `TierTone(row.RowIndex)`（不建议 —— 彩虹噪声）。
