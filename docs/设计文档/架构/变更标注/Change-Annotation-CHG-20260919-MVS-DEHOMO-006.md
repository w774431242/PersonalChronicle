---
change_id: CHG-20260919-MVS-DEHOMO-006
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/MVS/**, Source/PersonalChronicle/Application/**, Source/PersonalChronicle/Economy/UI/**, Languages/**, docs/**]
files:
  - path: Source/PersonalChronicle/MVS/Domain/FactionScale.cs
    type: M
    symbols: HeadcountShare01 / HasHeadcountShare / TryShareOfTotal(float,float,out float)
    lines: 16-190
  - path: Source/PersonalChronicle/MVS/Data/FactionScaleReader.cs
    type: M
    symbols: TryRead（两个分母） / CountSettlementsAndMasses
    lines: 1-5, 44-90, 205-280
  - path: Source/PersonalChronicle/Application/ReadModels/EconomySocietySnapshot.cs
    type: M
    symbols: BarShare01 / HasBarShare / BarCaption / FactorsTip（原 SettlementShare* 更名）
    lines: 50-140
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.Society.cs
    type: M
    symbols: 口径选择（人口/据点）/ BuildScaleTip（因子段）/ AppendDerivedChips → FactorsTip
    lines: 105-160, 230-300
  - path: Source/PersonalChronicle/Economy/UI/EconomySocietyPanel.cs
    type: M
    symbols: DrawScaleBar / DrawQa（+questionWidth）/ qaQuestionWidth / 删 chip 渲染链
    lines: 37-105, 195-225, 460-520
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: Scale.Title（改名）/ BarHeadcount / HeadcountShareTip / FactorsHeading（+3）
    lines: 208-222
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: 同上
    lines: 200-214
  - path: docs/changelog/v1.4.x_2026-09-19_影响因子入tooltip与市场特征优化.md
    type: A
    symbols: CHG-20260919-MVS-DEHOMO-006
    lines: 1-125
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-DEHOMO-006.md
    type: A
    symbols: CHG-20260919-MVS-DEHOMO-006
    lines: 1-115
---

# 变更标注 · CHG-20260919-MVS-DEHOMO-006

**Change ID：** `CHG-20260919-MVS-DEHOMO-006` · **Version：** v1.4.x · **Date：** 2026-09-19
**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物

## Change Annotation
- 要求 A「将影响因子放入 tooltip。据点规模改为总人数显示」· 要求 B「优化市场特征结构」
- Scope(摘要): 影响因子移入规模条 tooltip（并删整条 chip 渲染链）· 规模条主口径改**人口份额/总人数** · Q/A 结构优化

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|
| `MVS/Domain/FactionScale.cs` | M | `HeadcountShare01` / 浮点 `TryShareOfTotal` | 新增人口份额（**K 在比值里约掉** ⇒ 无需全局总人口）；浮点守卫用 `!(x>0)` 拦 NaN，并容忍 1e-4 浮点余量 | DATA-001 / GOV-004 | ARCH / DATA |
| `MVS/Data/FactionScaleReader.cs` | M | `CountSettlementsAndMasses` | **一次扫描出两个分母**；权重按 defName 缓存（否则每据点一次全表遍历）；无权重者两边都不进 | DATA-007 / PERF-001 | DATA / PERF |
| `Application/ReadModels/EconomySocietySnapshot.cs` | M | `BarShare01` 等 | **字段更名**（`SettlementShare*` → `Bar*`）：语义变了，名字必须跟着走 | DATA-001 / UI-001 | DATA |
| `.../BuiltinEconomyProvider.Society.cs` | M | 口径选择 / `BuildScaleTip` | caption 与份额**必须同口径**；无权重退回据点口径；tooltip 加因子段（空行分隔） | GOV-010 / UI-001 | UI |
| `Economy/UI/EconomySocietyPanel.cs` | M | `DrawQa` / `qaQuestionWidth` | 问题列宽**实测**取代写死 74px；答案**两行换行**取代硬截断；**删除 chip 渲染链** | UI-001 / LAND-009 | UI / DELTA |
| `Languages/*/Keyed/MVS.xml` | M | `Scale.*` | 段标题改名 + 3 新键（caption 自带 ③） | GOV-010 / LOC-001 | LOC |
| `docs/changelog/v1.4.x_2026-09-19_影响因子入tooltip与市场特征优化.md` | A | `CHG-...-006` | 更新日志 | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-DEHOMO-006.md` | A | `CHG-...-006` | 本文件自身（自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**：`Assemblies/PersonalChronicle.dll` 已重编并同步
> （MD5 `A01E9581CBBA262BE732CC1CF1690C0E`，两区一致）。

### 关键内容片段

```diff
// FactionScaleReader.cs —— 一次扫描出两个分母
+ float totalMass; float selectedMass;
+ count = CountSettlementsAndMasses(factionDefName, out totalSettlements, out totalMass, out selectedMass);
+ // 人口质量 = Σ(每个据点的 owner 权重)；K 在比值里约掉，故不需要全局总人口
```

```diff
// EconomySocietyPanel.cs —— Q/A 结构优化
- float qWidth = Mathf.Min(QaQuestionWidth /* 写死 74 */, inner.width * 0.26f);
- UIComponents.Label(answer, TruncateToWidth(qa.Answer, …));      // 单行硬截断
+ float qWidth = Mathf.Min(questionWidth /* BuildPlan 实测 */, inner.width * 0.4f);
+ EconomyPanelChrome.DrawWrapped(answer, qa.Answer, 2, UITheme.Text);   // 两行换行
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件（脚本 MINE 与标注 `files:` **已逐条人工对齐**）
- [x] 清单范围 ⊆ Change Summary.Scope
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] **无死代码**：chip 渲染链（3 方法 + 3 常量 + 计划行 + switch 分支）已整体删除
- [x] 无临时文件残留
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录
| 事实 | 求证方式 | 结论 |
|---|---|---|
| 人口份额可否不求全局总人口 | 公式推导 | ✅ `人数 = 据点 × K × 权重`，**K 是全局常量 ⇒ 在比值中约掉** |
| 权重查询成本 | 代码审查 | ⚠ 逐据点调用 `GroupWeightOf` 会把线性扫描变成"每据点一次全表遍历" ⇒ **按 defName 缓存** |
| 那 4 枚 chip 的归属 | 读 `BuildPlan` 的块序列 | ⚠ 它们**没有归属段落**（在规模段与市场特征段之间）⇒ 这才是噪声的真正来源 |
| `DrawWrapped` 是否可用 | 读既有用法（`DrawVerdict`） | ✅ `EconomyPanelChrome.DrawWrapped(rect, text, maxLines, color)` |

### 设计判断：**条与 caption 必须同口径**
要求 A 把主口径改成总人数（③），因此条的**填充比例**也必须换成**人口份额**。
若保留据点份额而只换 caption，就是"**条画的是甲、字写的是乙**" —— 比不画更糟。
⇒ 两个字段在 Provider 里**成对选择**（人口可用则两者都用人口，否则两者都退回据点）。

### 回滚定位
1. **只回口径**：Provider 的 `if (scale.HasHeadcountShare && …)` 分支去掉 ⇒ 回到据点口径（caption/份额成对回退）。
2. **只回 tooltip**：`AppendDerivedChips` 末尾改回 4 个 `Chips.Add` ⇒ 但 chip 渲染链已删，需要一并恢复（不建议）。
3. **只回 Q/A**：`DrawQa` 改回 `TruncateToWidth` + `Plan(BlockQa, i, RowHeight)`。
4. **运行时止损**：无 —— 纯 Def 只读 + 快照投射 + 绘制，不触存档、不改游戏状态。
