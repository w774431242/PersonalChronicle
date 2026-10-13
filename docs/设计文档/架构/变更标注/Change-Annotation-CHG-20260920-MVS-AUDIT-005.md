---
change_id: CHG-20260920-MVS-AUDIT-005
version: v1.4.x
date: 2026-09-20
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Archive/UI/**, docs/**]
files:
  - path: Source/PersonalChronicle/Archive/UI/UITheme.cs
    type: M
    symbols: Palette.Steampunk.dim
    lines: 508-520
  - path: Source/PersonalChronicle/Archive/UI/RuifBridge.cs
    type: M
    symbols: StatRowHeight（自失效） / statRowHeightThemeRef / statRowHeightLangRef / statRowHeightScale
    lines: 465-512
  - path: docs/changelog/v1.4.x_2026-09-20_审计修复批5.md
    type: A
    symbols: CHG-20260920-MVS-AUDIT-005
    lines: 1-110
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260920-MVS-AUDIT-005.md
    type: A
    symbols: CHG-20260920-MVS-AUDIT-005
    lines: 1-90
---

# 变更标注 · CHG-20260920-MVS-AUDIT-005（审计修复批 5）

**Change ID：** `CHG-20260920-MVS-AUDIT-005` · **Version：** v1.4.x · **Date：** 2026-09-20
**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**前置批次：** `...-AUDIT-001`（12 项）· `-002`（3 项）· `-003`（3 项）· `-004`（2 项），均已 PASS

## Change Annotation
- 用户「**继续**」⇒ 接着做审计 §2.7 / §2.8 的**未闭环部分**
- Scope(摘要): Steampunk `dim` 对比度（§2.7 续）· 行高缓存**自失效**（§2.8 续）

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|
| `Archive/UI/UITheme.cs` | M | `Palette.Steampunk.dim` | (0.416,0.400,0.322) → (0.545,0.525,0.425)：对 `card` 2.92→**4.6:1**、徽章底 2.65→**4.6:1** | UI-001 / ASSET-002 | UI |
| `Archive/UI/RuifBridge.cs` | M | `StatRowHeight` + 3 个失效键字段 | 缓存**自失效**（主题/语言/UI 缩放，**零分配**）；文档更正（原承诺的入口是**死方法**） | PERF-001 / DATA-001 | PERF / DATA |
| `docs/changelog/v1.4.x_2026-09-20_审计修复批5.md` | A | `CHG-...-AUDIT-005` | 更新日志（含留后清单与原因） | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260920-MVS-AUDIT-005.md` | A | `CHG-...-AUDIT-005` | 本文件自身（自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll`
> 已重编并同步（MD5 `27F39FB17B09D124F255FBAC825B7402`，两区一致）。

### 关键内容片段

```diff
// UITheme —— Steampunk dim（与上一批 Gothic 同一公式复算）
- dim: new Color(0.416f, 0.400f, 0.322f, 1f),     // vs card ≈ 2.92:1 · vs dim 徽章底 ≈ 2.65:1
+ dim: new Color(0.545f, 0.525f, 0.425f, 1f),     // vs card ≈ 4.6:1 · vs panel ≈ 4.7:1
```

```diff
// RuifBridge.StatRowHeight —— 缓存自失效（原文承诺的 InvalidateRowHeight 是死方法）
+ object themeRef = UITheme.ActiveThemeId;
+ object langRef  = Verse.LanguageDatabase.activeLanguage;
+ float  scale    = Prefs.UIScale;
+ if (!ReferenceEquals(themeRef, statRowHeightThemeRef)
+     || !ReferenceEquals(langRef, statRowHeightLangRef)
+     || scale != statRowHeightScale)
+ { statRowHeight = 0f; statRowHeightThemeRef = themeRef; statRowHeightLangRef = langRef; statRowHeightScale = scale; }
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件（本批 4 个）
- [x] 清单范围 ⊆ Change Summary.Scope
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] 无临时文件残留（`_scripts/_tmp_*` 跑完即删）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| `InvalidateRowHeight` 是否真无调用方 | `search_content "InvalidateRowHeight"` 全文件 | ✅ 只有**定义**与文档引用，**零调用** ⇒ 确为死方法 |
| 缓存真值的输入是什么 | 读 `ReadStatRowHeight` + 文档 | ✅ `Measure.RowHeight(Tokens.FontLabel)` ⇒ 输入 = **语言/字体/缩放 + 主题令牌** |
| Steampunk `dim` 是否真不达标 | 手算 WCAG（sRGB→线性→Y→比值） | ✅ **2.92:1**（card）· **2.65:1**（dim 徽章底）⇒ 确为缺陷（**审计未点到**） |
| Gothic `dim` 修复是否顺带解决 `Embedded` 徽章 | 手算徽章底（`WithAlpha(dim,0.15)` 合成） | ✅ 2.14:1 → ≈**4.1:1** ⇒ 该处 Gothic 已闭环 |
| `Prefs.UIScale` 是否存在 | 编译 | ✅ 通过（编译 0 错） |

### ⚠ 本批**未做**（累计留后，非遗漏）
**§3.2/§2.3 预截断调用点逐个迁移**（机制已建；实测 **266 处**，`MasterControlTabWindow.Roster.cs` 46 处最多；
需**逐个确认是否真会被截断**，否则是纯增开销）· §2.2 面板级隔离 ·
**§0.7 第 2 条 `qaQuestionWidth` 隐式跨方法通道**（**审计里唯一尚未处置的条目**；修法：`BuildPlan` 返回该值 →
`Draw` 存局部 → `DrawPlan` 传参 → 删字段；本批未做，理由见更正记录末段）。
~~§7.4 空态文案重复 / §8.2 链路卡片无标签"件/日"~~ —— 🔴 **误引，2026-09-20 撤回**。

### ⚠ 本批**有可见行为变化**（实机验证时特意看）
- **Steampunk 次要文字变亮**（`dim` 提亮）—— 这是修复本身。
- **行高在"改 UI 缩放 / 切语言 / 换主题"当帧修正**（改前本会话不再修正）——
  未改设置时**数值不变**（自失效只在键变化时清缓存）。

### 回滚定位
1. **只回配色**：`UITheme.cs` 的 Steampunk `dim` 字面量改回 `(0.416,0.400,0.322)`（无逻辑依赖）。
2. **只回自失效**：删 `StatRowHeight` 顶部那段判定与三个键字段（退回"永不失效"，**不建议**）。
3. **运行时止损**：无 —— 不触存档、不改游戏状态。
