---
change_id: CHG-20260919-MVS-AUDIT-003
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Archive/**, Languages/**, docs/**]
files:
  - path: Source/PersonalChronicle/Archive/ArchiveMainTabWindow.CompactNav.cs
    type: M
    symbols: CompactNavItem（哨兵） / DrawCompactNav / DrawCompactNavOverflow（+）
    lines: 30-130
  - path: Source/PersonalChronicle/Archive/UI/UITheme.cs
    type: M
    symbols: Palette.Gothic.dim · Palette.Epic/Gothic/Steampunk.threat
    lines: 448-520
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: PersonalChronicle.UI.CompactNavOverflow（+1）
    lines: 246-252
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: PersonalChronicle.UI.CompactNavOverflow（+1）
    lines: 239-245
  - path: docs/changelog/v1.4.x_2026-09-19_审计修复批3.md
    type: A
    symbols: CHG-20260919-MVS-AUDIT-003
    lines: 1-110
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-AUDIT-003.md
    type: A
    symbols: CHG-20260919-MVS-AUDIT-003
    lines: 1-90
---

# 变更标注 · CHG-20260919-MVS-AUDIT-003（审计修复批 3）

**Change ID：** `CHG-20260919-MVS-AUDIT-003` · **Version：** v1.4.x · **Date：** 2026-09-19
**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**前置批次：** `...-AUDIT-001`（12 项）· `...-AUDIT-002`（3 项），均已 PASS

## Change Annotation
- 用户「**批量执行**」⇒ 接着做审计 §5 顺序里的下一批
- Scope(摘要): 窄档导航静默消失（§2.10）· 主题令牌 `dead≡threat`（§2.6）· Gothic `dim` 对比度（§2.7）

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|
| `Archive/ArchiveMainTabWindow.CompactNav.cs` | M | `CompactNavItem` / `DrawCompactNav` / `DrawCompactNavOverflow` | 放不下 ⇒ **越界哨兵**（x=+∞）取代静默 `return false`；末尾判定并画 `…` + 悬停；**接住最后一个返回值** | UI-001 / LAND-006 | UI |
| `Archive/UI/UITheme.cs` | M | `Palette.Gothic.dim` / 三主题 `.threat` | `threat` 与 `dead` 拉开距离（两处原**逐通道相同**）；Gothic `dim` 对比度 2.5:1 → ~4.7:1 | UI-001 / ASSET-002 | UI |
| `Languages/*/Keyed/MVS.xml` | M | `PersonalChronicle.UI.CompactNavOverflow` | +1 键（中英各一） | GOV-010 / LOC-001 | LOC |
| `docs/changelog/v1.4.x_2026-09-19_审计修复批3.md` | A | `CHG-...-AUDIT-003` | 更新日志（含留后清单与原因） | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-AUDIT-003.md` | A | `CHG-...-AUDIT-003` | 本文件自身（自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步。

### 关键内容片段

```diff
// CompactNavItem —— 放不下：静默丢弃 → 越界哨兵
- if (x + width > row.xMax) { return false; }
+ if (x + width > row.xMax) {
+     // 语义：x 越界 = 本行已有项被丢弃（x 单调增 ⇒ 后续一律放不下）
+     x = float.PositiveInfinity; return false;
+ }
```

```diff
// DrawCompactNav —— 接住最后一个 + 画出溢出
- CompactNavCategory(inner, x, ArchiveCategoryKeys.Location, service);   // 返回值被丢弃
+ x = CompactNavCategory(inner, x, ArchiveCategoryKeys.Location, service);
+ if (float.IsInfinity(x)) { DrawCompactNavOverflow(inner); }   // "…" + 悬停说明
```

```diff
// UITheme —— Gothic dim 对比度（按 WCAG 相对亮度复算）
- dim: new Color(0.361f, 0.329f, 0.408f, 1f),     // vs card ≈ 2.507:1
+ dim: new Color(0.540f, 0.490f, 0.610f, 1f),     // vs card ≈ 4.7:1 · vs panel ≈ 4.9:1
```

```diff
// UITheme —— threat 与 dead 拉开（Gothic / Steampunk 原值逐通道相同）
- threat: new Color(0.784f, 0.251f, 0.251f, 1f),  // == dead（Gothic）
+ threat: new Color(0.902f, 0.435f, 0.180f, 1f),  // 距 dead 0.36 · 距 warn 0.36
- threat: new Color(0.722f, 0.353f, 0.290f, 1f),  // == dead（Steampunk）
+ threat: new Color(0.878f, 0.510f, 0.196f, 1f),  // 距 dead 0.26
- threat: new Color(0.784f, 0.353f, 0.290f, 1f),  // 距 dead 仅 0.12（Epic）
+ threat: new Color(0.878f, 0.494f, 0.196f, 1f),  // 距 dead 0.25 · 距 warn 0.55
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件（本批 6 个）
- [x] 清单范围 ⊆ Change Summary.Scope
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] 无临时文件残留（`_scripts/_tmp_*` 跑完即删）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| `dead` 与 `threat` 是否真同值 | `search_content` 取三主题 `Palette` **字面量** | ✅ Gothic、Steampunk **逐通道相同**；Epic 距离 0.12 ⇒ 确为缺陷 |
| Gothic `dim` 的实际对比度 | 按 WCAG 相对亮度公式手算（sRGB→线性→Y→比值） | ✅ **2.507:1**，与审计的"≈2.5:1"一致 ⇒ 确为缺陷 |
| `CompactNavItem` 是否有"放不下"的既有信号 | 读方法体 | ❌ 只返回 `bool clicked` —— **两种语义共用一个返回值**才是根因 |
| 最后一个 `CompactNavCategory` 是否接住返回值 | 读 `DrawCompactNav` | ❌ **未接收**（即使有哨兵也会漏判）⇒ 一并修 |
| 修后配色是否可辨 | 逐对算欧氏距离 | ✅ threat↔dead ≥0.25、threat↔warn ≥0.26 |

### ⚠ 本批**未做**（累计留后，非遗漏）
§2.2 面板级隔离 · **§2.5 `DrawNoticeBox`**（**取证实为结构性**：`NoticeBoxHeight` 是 const，
而 `PageHeaderBlockHeight()` 等**四处计高没有宽度参数** ⇒ 改成"按文本计高"必须连带改签名）·
§3.2 截断补偿全量。~~§7.4 空态文案重复 · §8.2 链路卡片无标签数字~~
—— 🔴 **后两项系误引，2026-09-20 撤回**：该审计**只到 §5，无 §7/§8**，复核后确认不存在。

### ⚠ 本批**有可见行为变化**（应在实机验证时特意看）
- **导航条新增 `…` 与悬停说明**（只在放不下时出现）
- **三主题 `threat` 变橙、Gothic 次要文字变亮** —— 这是修复本身。

### 回滚定位
1. **只回导航**：`CompactNavItem` 的哨兵与 `DrawCompactNavOverflow` 调用去掉（退回"静默少一项"）。
2. **只回配色**：`UITheme.cs` 四处字面量改回原值（无逻辑依赖）。
3. **运行时止损**：无 —— 本批不触存档、不改游戏状态。
