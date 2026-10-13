---
change_id: CHG-20260919-ECON-UI-010
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Archive/UI/**, Source/PersonalChronicle/MasterControl/**, Source/PersonalChronicle/Manufacturing/UI/**, docs/**]
files:
  - path: Source/PersonalChronicle/Archive/UI/UIComponents.Loc.cs
    type: A
    symbols: UIComponents.Loc / UIComponents.locCache / UIComponents.locCacheLanguage / UIComponents.LocCacheCapacity
    lines: 1-95
  - path: Source/PersonalChronicle/Archive/UI/UIComponents.cs
    type: M
    symbols: UIComponents.DrawSubsectionHeader（`key.Translate()` → `Loc(key)`）
    lines: 549-556
  - path: Source/PersonalChronicle/Archive/UI/UIComponents.KpiGrid.cs
    type: M
    symbols: UIComponents.KpiGrid（卡片标题 / NoRecord / CatMore 注释）
    lines: 142-150,204-212,351-360
  - path: Source/PersonalChronicle/Archive/UI/UITheme.cs
    type: M
    symbols: UITheme.GetDisplayName
    lines: 53-60
  - path: Source/PersonalChronicle/Archive/UI/ArchiveEmbeddedHost.cs
    type: M
    symbols: ArchiveEmbeddedHost.DrawUnavailable
    lines: 113-122
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.cs
    type: M
    symbols: MasterControlTabWindow.Translate(string)
    lines: 256-272
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.PopulationViews.cs
    type: M
    symbols: MasterControlTabWindow.WidestTabLabelWidth / DrawPopulationViewTab / TabLabelOf / DrawContractBlock / DrawDevToggleButton
    lines: 245-255,305-330,349-392,524-540
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Roster.cs
    type: M
    symbols: MasterControlTabWindow（人口表头排序提示）
    lines: 1370-1380
  - path: Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.cs
    type: M
    symbols: WorkshopLayoutCanvas.Translate(string)
    lines: 1693-1708
  - path: Source/PersonalChronicle/Manufacturing/UI/FloorDeviceDetailWindow.cs
    type: M
    symbols: FloorDeviceDetailWindow.Translate(string)
    lines: 973-982
  - path: docs/changelog/v1.4.x_2026-09-19_绘制期文案缓存.md
    type: A
    symbols: CHG-20260919-ECON-UI-010
    lines: 1-140
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-010.md
    type: A
    symbols: CHG-20260919-ECON-UI-010
    lines: 1-150
---

# 变更标注 · CHG-20260919-ECON-UI-010（绘制期文案缓存 Loc）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-ECON-UI-010`
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-ECON-UI-010
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent；先统计真实分布再动手，避免照清单盲改）
- Scope(摘要): 新增语言级文案缓存 `UIComponents.Loc` + 迁移 `MasterControl` / `Manufacturing·UI` 的绘制期翻译

### 一句话动机
绘制期 `"Key".Translate()` 的真正代价不是"慢"（单次只是字典查询），而是 `.ToString()`
**每帧分配字符串** ⇒ 稳态 GC 压力 + 违反 GOV-010「绘制期零翻译、零拼接」。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `Archive/UI/UIComponents.Loc.cs` | A | `Loc` / `locCache` / `locCacheLanguage` / `LocCacheCapacity` | 1-95 | **新增语言级文案缓存**（只服务无参键；按 `folderName` 失效；容量 512 兜底；单线程假设已写明） | GOV-010 / PERF-001 / UI-001 | UI / PERF |
| `Archive/UI/UIComponents.cs` | M | `DrawSubsectionHeader` | 549-556 | 共享绘制原语的分区标题由 `key.Translate()` 改 `Loc(key)`（被各面板在绘制期调用） | GOV-010 / PERF-001 | PERF |
| `Archive/UI/UIComponents.KpiGrid.cs` | M | 卡片标题 · `NoRecord` · `CatMore`（注释） | 142-150,204-212,351-360 | 前两者改走 `Loc`（在**卡片循环**里，每帧 N 次）；`CatMore` **带参 ⇒ 有意保留**并写明理由 | GOV-010 / PERF-001 | PERF |
| `Archive/UI/UITheme.cs` | M | `GetDisplayName` | 53-60 | 动态键（`Theme.Xxx`）改走 `Loc`（容量兜底覆盖动态键） | GOV-010 | PERF |
| `Archive/UI/ArchiveEmbeddedHost.cs` | M | `DrawUnavailable` | 113-122 | 降级空态改走 `Loc` | GOV-010 | PERF |
| `MasterControl/MasterControlTabWindow.cs` | M | `Translate(string)` | 256-272 | **改助手实现体** ⇒ 一处覆盖本类全部无参调用点（页头 / 面包屑 / 返回 / 各面板标题 / 空态 / 表头 …）；带参版本注释写明**有意不缓存** | GOV-010 / PERF-001 | PERF / UI |
| `MasterControl/MasterControlTabWindow.PopulationViews.cs` | M | `WidestTabLabelWidth` · `DrawPopulationViewTab` · `TabLabelOf` · `DrawContractBlock` · `DrawDevToggleButton` | 245-255,305-330,349-392,524-540 | 视图 tab 标签 ×3、契约块 9 条标签、Dev 开关标签 → 助手/`Loc`；⚠ 就地写下**动态键陷阱**警告（`"…Gap"+i` 不得写成 `Loc("…Gap")+i`） | GOV-010 / PERF-001 | PERF |
| `MasterControl/MasterControlTabWindow.Roster.cs` | M | 人口表头排序提示 | 1370-1380 | 原为**每列每帧**翻译 + 分配 ⇒ 改助手 | GOV-010 / PERF-001 | PERF |
| `Manufacturing/UI/WorkshopLayoutCanvas.cs` | M | `Translate(string)` | 1693-1708 | **改助手实现体** ⇒ 覆盖本类 13 处 `Draw*` 内翻译 | GOV-010 / PERF-001 | PERF |
| `Manufacturing/UI/FloorDeviceDetailWindow.cs` | M | `Translate(string)` | 973-982 | **改助手实现体** ⇒ 覆盖本窗口 `OnGUI` 内的 9 处 | GOV-010 / PERF-001 | PERF |
| `docs/changelog/v1.4.x_2026-09-19_绘制期文案缓存.md` | A | `CHG-20260919-ECON-UI-010` | 1-140 | 更新日志：真实分布统计表 + 4 条设计约束 + 动态键陷阱 + 有意保留清单 + 4 项残余风险 | REL-001 / REL-004 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-010.md` | A | `CHG-20260919-ECON-UI-010` | 1-150 | 本文件自身（GATE-DELTA 要求标注覆盖**全部**实际改动文件，含标注自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步至发布区
> （MD5 `4D0003C8F9529CDAEEE4B27AA8BDADA1`，两区一致）。

### 关键 diff 片段

```diff
// MasterControlTabWindow.cs  —— 一处改动覆盖全类全部无参调用点
  private static string Translate(string key)
  {
-     return key.Translate().ToString();
+     return UIComponents.Loc(key);
  }
```

```diff
// UIComponents.Loc.cs  —— 缓存本体（只服务无参键）
+ string language = LanguageDatabase.activeLanguage == null ? string.Empty
+     : LanguageDatabase.activeLanguage.folderName;
+ if (!string.Equals(locCacheLanguage, language, StringComparison.Ordinal))
+ {
+     locCache.Clear();          // 切语言 ⇒ 整表作废（混语言比慢更糟）
+     locCacheLanguage = language;
+ }
+ if (locCache.TryGetValue(key, out cached)) return cached;
+ string text = key.Translate().ToString();
+ if (locCache.Count >= LocCacheCapacity) locCache.Clear();   // 动态键兜底：有界优先
+ locCache[key] = text;
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏（生成脚本自检 declared = hit）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— 003~009 批次遗留文件由 `-ExcludeFromFile` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致（`CHG-20260919-ECON-UI-010`）
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 复核结论 |
|---|---|---|
| 「绘制期翻译 ≥80 处，跨 15 文件」 | 全仓检索 `\.Translate\(` + 逐文件核读**是否在绘制路径** | ✅ 成立，但**实际分布极不均匀**：`MasterControlTabWindow.Translate` / `WorkshopLayoutCanvas.Translate` / `FloorDeviceDetailWindow.Translate` 三个助手**背后就占了大头** ⇒ 改 3 处实现体比改 50 处调用点更安全 |
| 「`Application` 层也有同形 `Translate` 助手，应一并处理」 | 读 `SystemCenterKpiFactory.cs:107` / `SystemCenterReadModelProvider.cs:729` 的调用位置 | ❌ **有意不动**：它们在**构建期**（Provider 建快照）运行，不在绘制路径上；给它们加缓存只会引入无谓静态状态 |
| 「无参 Keyed 键只随语言变化」 | 核对 Keyed 数据来源（本工程 `PersonalChronicle.*` 命名空间，无运行期注入） | ✅ 成立 ⇒ 失效条件只需"语言变了" |
| 动态键能否进缓存 | 检查 `UITheme.GetDisplayName` / `KpiGrid` 卡片标题 / `Contract.Gap{i}` 三处动态键 | ✅ 可（容量兜底已覆盖）；⚠ 但**必须先拼键再缓存** —— 已就地写下反例警告 |

### 回滚定位
1. **只退助手**：把 3 个 `Translate(string)` 改回 `key.Translate().ToString()`，其余点仍走 `Loc`（**不建议** —— 那会恢复大头）。
2. **全退**：删 `UIComponents.Loc.cs` + 还原各调用点（**不建议** —— 会恢复每帧分配）。
3. **运行时止损**：若怀疑缓存导致混语言，把 `Loc` 的首行改成 `return key.Translate().ToString();`（一行即回退到"每次都翻译"，语义完全等价）。
