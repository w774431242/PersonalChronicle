---
change_id: CHG-20260919-MVS-AUDIT-004
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Archive/**, Source/PersonalChronicle/MasterControl/**, docs/**]
files:
  - path: Source/PersonalChronicle/Archive/UI/UIComponents.cs
    type: M
    symbols: TruncateToWidth（+tipRect 重载） / Label（兜底悬停）
    lines: 24-75, 73-135
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Shell.cs
    type: M
    symbols: DrawNoticeBox（两行 + 悬停全文）
    lines: 264-305
  - path: docs/changelog/v1.4.x_2026-09-19_审计修复批4.md
    type: A
    symbols: CHG-20260919-MVS-AUDIT-004
    lines: 1-110
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-AUDIT-004.md
    type: A
    symbols: CHG-20260919-MVS-AUDIT-004
    lines: 1-90
---

# 变更标注 · CHG-20260919-MVS-AUDIT-004（审计修复批 4）

**Change ID：** `CHG-20260919-MVS-AUDIT-004` · **Version：** v1.4.x · **Date：** 2026-09-19
**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**前置批次：** `...-AUDIT-001`（12 项）· `-002`（3 项）· `-003`（3 项），均已 PASS

## Change Annotation
- 用户「**批量执行**」⇒ 接着做审计 §5 顺序里的下一批
- Scope(摘要): **截断补偿收进共享组件**（§3.2）· `DrawNoticeBox` 两行绘制（§2.5）

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|
| `Archive/UI/UIComponents.cs` | M | `TruncateToWidth`（+重载） / `Label` | 截断与补偿**合并进同一调用**；`Label` 兜底（命中才测） | UI-001 / LAND-009 | UI / PERF |
| `MasterControl/MasterControlTabWindow.Shell.cs` | M | `DrawNoticeBox` | 显式开 `WordWrap` 画**两行**（计高侧一字未动）+ 装不下挂悬停给全文 | UI-001 / LAND-002 | UI |
| `docs/changelog/v1.4.x_2026-09-19_审计修复批4.md` | A | `CHG-...-AUDIT-004` | 更新日志（含**实测**迁移面 266 处） | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-AUDIT-004.md` | A | `CHG-...-AUDIT-004` | 本文件自身（自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll`
> 已重编并同步（MD5 `FB9D7095481B66C6BB4F82092B3E0509`，两区一致）。

### 关键内容片段

```diff
// UIComponents —— 补偿做进截断本身（调用点结构上不可能只截断不补偿）
+ internal static string TruncateToWidth(string text, float maxWidth, GameFont font, Rect tipRect)
+ {
+     string clipped = TruncateToWidth(text, maxWidth, font);
+     // 判据用"结果是否变了"而不是重算宽度（截断内部已经量过）
+     if (!string.IsNullOrEmpty(text) && !string.Equals(clipped, text, StringComparison.Ordinal))
+     { TooltipHandler.TipRegion(tipRect, text); }
+     return clipped;
+ }
```

```diff
// UIComponents.Label —— 兜底（覆盖"完整文本直接交给 Label"的调用点）
+ // 成本控制：先矩形命中测试，只有鼠标停在这条上才做文本实测 ⇒ 无 per-frame 全量测量
+ if (Event.current != null && !string.IsNullOrEmpty(text)
+     && rect.Contains(Event.current.mousePosition)
+     && TruncateToWidth(text, rect.width, font) != text) { TooltipHandler.TipRegion(rect, text); }
```

```diff
// DrawNoticeBox —— 计高两行 / 绘制单行 → 两边一致（计高不动）
- UIComponents.Label(new Rect(box.x + 12f, box.y + 2f, box.width - 20f, box.height - 4f), text, GameFont.Small, UITheme.Muted);
+ Rect textRect = new Rect(box.x + 12f, box.y + 2f, box.width - 20f, box.height - 4f);
+ bool prevWrap = Text.WordWrap; Text.WordWrap = true;
+ try { Widgets.Label(textRect, text); } finally { Text.WordWrap = prevWrap; }
+ if (Text.CalcHeight(text, textRect.width) > textRect.height) { TooltipHandler.TipRegion(box, text); }
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
| `Label` 是否单行 | 读方法体 | ✅ **强制 `WordWrap = false`**（其注释自陈"预期单行"）⇒ §2.5 的"单行绘制"确证 |
| `Label` 是否有测量能力 | 读 `TruncateToWidth` | ✅ 内部用 `Text.CalcSize`；**无公开 MeasureWidth** ⇒ 判据改用"结果是否变了"（复用同一路径） |
| `NoticeBoxHeight` 是 const 还是变量 | 读 `MasterControlTabWindow.cs:59` | ✅ **const 44**；且计高侧（`PageHeaderBlockHeight()` 等）**无宽度参数** ⇒ 改动态计高属结构性改动 |
| 截断调用点到底多少处 | `Select-String "TruncateToWidth\("` 逐行计数 | ✅ **266 处**（我草稿里估的"86"**是错的**，已按实测更正） |

### ⚠ 本批**未做**（累计留后，非遗漏）
**§3.2 预截断调用点的逐个迁移**（机制已建；266 处需**逐个确认是否真会被截断**，
否则会给从不截断的标签挂空悬停 = 纯增开销；且悬停矩形取法因面板而异）·
§2.2 面板级隔离。~~§7.4 空态文案重复 · §8.2 链路卡片无标签数字~~
—— 🔴 **后两项系误引，2026-09-20 撤回**：该审计**只到 §5，无 §7/§8**，复核后确认不存在。

### ⚠ 本批**有意不改的另一条路**
§2.5 也可以走"把 `NoticeBoxHeight` 改成按文本计高"，但那要求**计高侧获得宽度**
（四处签名连带改并向上传宽度）。**44 本来就是按两行定的** ⇒ 把绘制补齐到计高是**更小、更稳**的解，
且两边现在真正一致（LAND-002）。**登记这条比较，以免下一轮又把它当"没修"重做一遍。**

### 回滚定位
1. **只回 NoticeBox**：`Shell.cs` 的 `DrawNoticeBox` 还原为 `UIComponents.Label` 单行。
2. **只回截断补偿**：删 `TruncateToWidth` 四参重载与 `Label` 末尾那段判定（**无调用方依赖**）。
3. **运行时止损**：无 —— 全部为绘制路径，不触存档、不改游戏状态。
