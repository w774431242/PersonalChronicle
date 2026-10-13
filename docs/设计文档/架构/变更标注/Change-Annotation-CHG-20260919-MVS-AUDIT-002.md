---
change_id: CHG-20260919-MVS-AUDIT-002
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Archive/**, Source/PersonalChronicle/Economy/UI/**, Source/PersonalChronicle/Application/**, Languages/**, docs/**]
files:
  - path: Source/PersonalChronicle/Application/ReadModels/EconomyFactionDetailSnapshot.cs
    type: M
    symbols: OverflowFormat（+）
    lines: 56-70
  - path: Source/PersonalChronicle/Application/ReadModels/EconomySocietySnapshot.cs
    type: M
    symbols: Chips（**删除**，零引用取证后）
    lines: 142-155
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FactionDetail.cs
    type: M
    symbols: Build（填 OverflowFormat）
    lines: 44-56
  - path: Source/PersonalChronicle/Economy/UI/EconomyFactionDetailPanel.cs
    type: M
    symbols: Draw（空项/放不下分开判 · 段头无条件 · hiddenFactors/hiddenEvents） / DrawOverflowNote（+）
    lines: 38-150
  - path: Source/PersonalChronicle/Archive/ArchiveMainTabWindow.Home.cs
    type: M
    symbols: homeTabLang（+） / DrawHomeViewTabs（语言键）
    lines: 15-30, 80-115
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: FactionDetail.Overflow（+1）
    lines: 245-255
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: FactionDetail.Overflow（+1）
    lines: 238-248
  - path: docs/changelog/v1.4.x_2026-09-19_审计修复批2.md
    type: A
    symbols: CHG-20260919-MVS-AUDIT-002
    lines: 1-120
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-AUDIT-002.md
    type: A
    symbols: CHG-20260919-MVS-AUDIT-002
    lines: 1-100
---

# 变更标注 · CHG-20260919-MVS-AUDIT-002（审计修复批 2）

**Change ID：** `CHG-20260919-MVS-AUDIT-002` · **Version：** v1.4.x · **Date：** 2026-09-19
**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**前置批次：** `CHG-20260919-MVS-AUDIT-001`（12 项，已 PASS）

## Change Annotation
- 用户「**继续**」⇒ 接着做审计 §5 顺序里的下一批
- Scope(摘要): 派系面板**静默丢弃**（§2.1）· 语言缓存失效（§2.9）· 死字段清除（§0.7）

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|
| `Application/ReadModels/EconomyFactionDetailSnapshot.cs` | M | `OverflowFormat` | 新增溢出提示模板字段（模板走语言管道） | GOV-010 / DATA-001 | DATA / LOC |
| `Application/ReadModels/EconomySocietySnapshot.cs` | M | `Chips` | **删除死字段**（全工程引用扫描零命中后才删） | DATA-001 | DATA |
| `.../BuiltinEconomyProvider.FactionDetail.cs` | M | `Build` | 填 `OverflowFormat`（`T(...)`），并注明"本面板无滚动"的取舍 | GOV-010 | LOC |
| `Economy/UI/EconomyFactionDetailPanel.cs` | M | `Draw` / `DrawOverflowNote` | **空项与放不下分开判**；段头无条件画；放不下⇒显式提示 | UI-001 / GOV-004 | UI |
| `Archive/ArchiveMainTabWindow.Home.cs` | M | `homeTabLang` / `DrawHomeViewTabs` | 预翻译缓存补**语言键**（与三处既有先例同源） | UI-001 / GOV-010 | UI / LOC |
| `Languages/*/Keyed/MVS.xml` | M | `FactionDetail.Overflow` | +1 键（中英各一） | GOV-010 / LOC-001 | LOC |
| `docs/changelog/v1.4.x_2026-09-19_审计修复批2.md` | A | `CHG-...-AUDIT-002` | 更新日志（含留后清单与"为什么不做"） | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-AUDIT-002.md` | A | `CHG-...-AUDIT-002` | 本文件自身（自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll`
> 已重编并同步（MD5 `52E11968578F19922EDDC8753342313A`，两区一致）。

### 关键内容片段

```diff
// EconomyFactionDetailPanel —— 静默丢弃 → 显式说出
- if (factor == null || y + RowHeight > body.yMax) break;      // 两件事挤一个条件
+ if (factor == null) continue;                                 // 坏数据：跳过，不影响后续
+ if (y + RowHeight > body.yMax) { /* 数出剩下非空项 */ hiddenFactors++; … break; }
…
- if (Events.Count == 0 || y + SectionHeaderHeight >= body.yMax) return;   // 整段凭空消失
+ if (Events.Count == 0) return;
+ y = SectionHeader(…);                                        // 段头无条件画
…
+ if (hiddenFactors > 0) y = DrawOverflowNote(body, y, snapshot.OverflowFormat, hiddenFactors);
```

```diff
// ArchiveMainTabWindow.Home —— 缓存补语言键
- if (homeTabLabels == null) { homeTabLabels = new[] { "…".Translate(), … }; }
+ string labelLang = LanguageDatabase.activeLanguage == null ? "" : LanguageDatabase.activeLanguage.folderName;
+ if (homeTabLabels == null || !string.Equals(homeTabLang, labelLang, Ordinal)) {
+     homeTabLabels = new[] { "…".Translate(), … }; homeTabLang = labelLang;
+ }
```

```diff
// EconomySocietySnapshot —— 死字段清除（引用扫描零命中）
- /// <summary>画像 chip（已本地化短标签，如"产能过剩 ×1.5"）。</summary>
- public List<string> Chips = new List<string>();
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件（本批 9 个）
- [x] 清单范围 ⊆ Change Summary.Scope
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] 无临时文件残留（`_scripts/_tmp_*` 跑完即删）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| `Chips` 是否还有消费者 | `search_content "\.Chips\b"` 全 Source 扫描 | ✅ **零命中**（其余都是别的类型的同名成员）⇒ 可删 |
| `Chips` 的声明原文 | `search_content "Chips"` + 逐行读 | ✅ 已取证（不凭猜改） |
| 语言标识的既有做法 | `search_content "activeLanguage\|folderName"` | ✅ **三处先例**：`ThemeSwitch` / `Loc` / `Population` 都用 `LanguageDatabase.activeLanguage.folderName` |
| `EconomyPanelChrome.Draw` 是否带滚动 | `search_content "internal static Rect Draw("` | ❌ **不带**（第 6/7 参是 `accentPanel`/`tightBody`）⇒ 只能走"显式说出"而非滚动 |
| 主题令牌的字面量 | `search_content "(Dead\|Threat\|Dim)\s*(=\|=>\|{ get)"` | ⚠ 只取到**赋值处**、未取到字面量定义 ⇒ §2.6/§2.7 **不做**（不凭猜改） |

### ⚠ 本批**未做**（累计留后，非遗漏）
§2.2 面板级隔离 · §2.5 `DrawNoticeBox` 计高 · **§2.6/§2.7 主题令牌**（**字面量未取证**）·
§2.10 `CompactNav` · §3.2 截断补偿全量。~~§7.4 空态文案重复 · §8.2 链路卡片无标签数字~~
—— 🔴 **后两项系误引，2026-09-20 撤回**：该审计**只到 §5，无 §7/§8**，复核后确认不存在。
逐条原因见 Changelog 的留后清单表。

### ⚠ 一处"修法不如理想"的如实登记
本批对 §2.1 选择**"显式说出"而非补滚动** —— 因为滚动需要跨帧状态，而该面板是 `static class`。
**为它引入静态可变状态，违反的纪律比这个缺陷本身更重**（且审计 §4.1 刚确认
"无跨会话滚动态残留"是优点）。**这是本批唯一一处修法不理想的地方，登记在此。**

### 回滚定位
1. **只回面板**：`EconomyFactionDetailPanel` 三处判定还原（**不建议**——会退回"假空态"）。
2. **只回语言键**：`Home.cs` 的 `homeTabLang` 判定去掉（退回"切语言不生效"）。
3. **运行时止损**：无 —— 全部为绘制路径与文案，不触存档、不改游戏状态。
