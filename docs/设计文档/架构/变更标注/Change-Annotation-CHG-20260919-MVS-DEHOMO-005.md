---
change_id: CHG-20260919-MVS-DEHOMO-005
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/MVS/**, Source/PersonalChronicle/Application/**, Source/PersonalChronicle/Economy/UI/**, Languages/**, docs/**]
files:
  - path: Source/PersonalChronicle/MVS/Domain/FactionScale.cs
    type: M
    symbols: FactionScaleData.SettlementShare01 / HasSettlementShare / FactionScaleModel.TryShareOfTotal
    lines: 16-145
  - path: Source/PersonalChronicle/MVS/Data/FactionScaleReader.cs
    type: M
    symbols: TryRead（份额） / CountSettlementsAndTotal
    lines: 44-80, 182-235
  - path: Source/PersonalChronicle/Application/ReadModels/EconomySocietySnapshot.cs
    type: M
    symbols: TitleTraitsText / SettlementShare01 / HasSettlementShare / ScaleBarTip / CompositionTip / SocietyTier.Share01
    lines: 50-135
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.Society.cs
    type: M
    symbols: AppendDerivedChips（② 进顶栏）/ BuildScaleTip / 构成 tooltip / 不再填徽章
    lines: 24-30, 100-145, 205-300
  - path: Source/PersonalChronicle/Economy/UI/EconomyPanelChrome.cs
    type: M
    symbols: Draw（+titleTraits）/ DrawTitleBar
    lines: 59-100, 145-175
  - path: Source/PersonalChronicle/Economy/UI/EconomySocietyPanel.cs
    type: M
    symbols: BlockComposition / BlockScaleBar / DrawComposition / DrawScaleBar / EnsureDonutTexture / CompositionHeight
    lines: 37-95, 160-210, 300-460
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: Society.Scale.ShareTip（+1）
    lines: 210-218
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: Society.Scale.ShareTip（+1）
    lines: 202-210
  - path: docs/changelog/v1.4.x_2026-09-19_社会背景面板三改.md
    type: A
    symbols: CHG-20260919-MVS-DEHOMO-005
    lines: 1-130
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-DEHOMO-005.md
    type: A
    symbols: CHG-20260919-MVS-DEHOMO-005
    lines: 1-120
---

# 变更标注 · CHG-20260919-MVS-DEHOMO-005（社会背景面板三改）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-MVS-DEHOMO-005` · **Version：** v1.4.x · **Date：** 2026-09-19

## Change Annotation
- 用户三问：要求一「社会特点不要额外组件，嵌入面板顶栏即可」· 要求二「据点规模采用结构进度条组件，同时添加 tooltip，结构列表保持干净」· 问题三「建议饼图并添加 tooltip」
- Scope(摘要): 顶栏承载社会特点（`StatRow` 徽章退场）+ 规模段改进度条 + 构成列表改环形图与图例

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|
| `MVS/Domain/FactionScale.cs` | M | `SettlementShare01` / `TryShareOfTotal` | 进度条需要**分母**（份额）；纯函数守卫分母 ≤0 / 分子越界 | DATA-001 / GOV-004 | ARCH / DATA |
| `MVS/Data/FactionScaleReader.cs` | M | `CountSettlementsAndTotal` | **一次扫描**同时得分子与分母（PERF-001）；无主据点不计入分母 | DATA-007 / PERF-001 | DATA / PERF |
| `Application/ReadModels/EconomySocietySnapshot.cs` | M | 5 个新字段 + `SocietyTier.Share01` | 图表用**数值**（不反解析已格式化文本）；tooltip 字段 | DATA-001 / UI-001 | DATA / UI |
| `.../BuiltinEconomyProvider.Society.cs` | M | `BuildScaleTip` 等 | ② 组改填顶栏；tooltip 用**已本地化的既有标签/取值**拼（不漂移） | GOV-010 / UI-001 | UI / LOC |
| `Economy/UI/EconomyPanelChrome.cs` | M | `Draw` / `DrawTitleBar` | 顶栏**优先**画社会特点，有它则不画徽章（装饰与数据互斥） | UI-001 / LAND-009 | UI |
| `Economy/UI/EconomySocietyPanel.cs` | M | 新块 + 环形图纹理 | 进度条 + 环形图 + 图例；纹理**只 new 一次**；**删死代码**（`BlockTier`/`BlockDataRow`/`JoinMeta`/`DrawTierRow` 等） | UI-001 / PERF-001 / LAND | UI / PERF / DELTA |
| `Languages/*/Keyed/MVS.xml` | M | `Society.Scale.ShareTip` | +1 键；注明既有标签键**未废弃**（现在活在 tooltip 里） | GOV-010 / LOC-001 | LOC |
| `docs/changelog/v1.4.x_2026-09-19_社会背景面板三改.md` | A | `CHG-...-005` | 更新日志（含**我对问题三的建议与原图标注不同**的论证） | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-DEHOMO-005.md` | A | `CHG-...-005` | 本文件自身（自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll`
> 已重编并同步（MD5 `D1A7126A59067AA48752383DDBC123A1`，两区一致）。

### 关键内容片段

```diff
// EconomyPanelChrome.cs —— 顶栏两个候选互斥
+ if (!string.IsNullOrEmpty(titleTraits)) { /* 画社会特点 + tooltip */ }
- if (!string.IsNullOrEmpty(badge) && ...) { Badge(...); }
+ else if (!string.IsNullOrEmpty(badge) && ...) { Badge(...); }
```

```csharp
// EconomySocietyPanel.cs —— 环形图纹理：只 new 一次
if (donutTexture != null && signature == donutSignature) return;   // 否则一次会话泄漏上百张纹理
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件（脚本 MINE 与标注 `files:` **已逐条人工对齐**）
- [x] 清单范围 ⊆ Change Summary.Scope
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] **无死代码**：被替换的块类型 / arg / 常量 / 两个方法已删除
- [x] 无临时文件残留（`_scripts/_tmp_*` 跑完即删）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| 项目已有进度条组件 | 检索 `UIComponents` 方法表 | ✅ `ProgressBar(rect, share01, fill)` + caption 重载（内部走 RUIF `ProgressAtoms.CapacityBar`） |
| RUIF 是否有饼/环原语 | 检索 `Arc\|Donut\|Pie\|Ring\|Chart\|Sector\|Wedge\|Radial` | ❌ **无** ⇒ 自烘纹理（已登记） |
| 顶栏 `StatRow` 是什么 | 检索 Keyed | `MVS.Economy.Society.Badge = StatRow` ⇒ 预览 `.ruif` **契约徽章**（非 bug，但零信息量） |
| 进度条需要分母 | 设计推导 + 现有契约检查 | `FactionScaleData` 原只有绝对量与"相对最强派系"的权重 ⇒ 补 `SettlementShare01` |

### ⚠ 设计判断：**问题三的图表没有放在"市场特征"上**（已向用户说明）
饼图的前提是"整体中的份额"。市场特征的四个系数是**各自独立的乘子**（×1.3 / ×1.15 / ×1.2 / ×1.1），
既不构成整体、也不该相加 —— 画成饼会让读者以为彼此有占比关系，是**用视觉撒谎**。
⇒ 图表落在**成员构成**（合计恒 100%），同时兑现要求二的"结构列表保持干净"。

### ⚠ 已知缺口
- `FactionScaleModel.TryShareOfTotal` **未加单测**（本轮预算用尽）。**如实登记，不写成"已验证"。**
- 环形图为**逐像素烘焙**，无抗锯齿（靠 `FilterMode.Bilinear` 缓解）。未实机比对预览。

### 回滚定位
1. **只回顶栏**：`Draw(..., titleTraits: null)` + Provider 恢复 `PanelBadgeText` ⇒ 回到徽章。
2. **只回规模段**：`BuildPlan` 恢复两行 `DataRow`、删 `BlockScaleBar`（份额字段可留着不用）。
3. **只回构成图**：恢复 `BlockTier` + `DrawTierRow`（`SocietyTier.Share01` 为附加字段，可留）。
4. **运行时止损**：无 —— 纯 Def 只读 + 快照投射 + 绘制，不触存档、不改游戏状态。
