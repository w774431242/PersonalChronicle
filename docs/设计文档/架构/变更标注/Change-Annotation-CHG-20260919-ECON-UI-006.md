---
change_id: CHG-20260919-ECON-UI-006
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Economy/UI/**, Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Economy.cs, Languages/ChineseSimplified/Keyed/MVS.xml, docs/**]
files:
  - path: Source/PersonalChronicle/Economy/UI/EconomySocietyPanel.cs
    type: M
    symbols: EconomySocietyPanel.Draw / EconomySocietyPanel.BuildPlan / EconomySocietyPanel.PlanTotalHeight / EconomySocietyPanel.DrawPlan / EconomySocietyPanel.Plan / EconomySocietyPanel.EnsurePlanCapacity / EconomySocietyPanel.VerdictHeight / EconomySocietyPanel.DrawVerdict / EconomySocietyPanel.DrawTierRow / EconomySocietyPanel.DrawQa / EconomySocietyPanel.DrawChips / EconomySocietyPanel.MeasureChipsHeight / EconomySocietyPanel.ChipNeedsWrap / EconomySocietyPanel.TierTone
    lines: 1-420
  - path: Source/PersonalChronicle/Economy/UI/EconomyPanelChrome.cs
    type: M
    symbols: EconomyPanelChrome.DataRow
    lines: 216-240
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Economy.cs
    type: M
    symbols: MasterControlTabWindow.economySocietyScroll / MasterControlTabWindow.DrawEconomyGridPage
    lines: 60-80,135-145
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: MVS.Economy.Society.Faction.Royal.A2
    lines: 229
  - path: docs/changelog/v1.4.x_2026-09-19_社会背景面板审计与修复.md
    type: A
    symbols: CHG-20260919-ECON-UI-006
    lines: 1-130
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-006.md
    type: A
    symbols: CHG-20260919-ECON-UI-006
    lines: 1-140
---

# 变更标注 · CHG-20260919-ECON-UI-006（社会背景面板审计与修复）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-ECON-UI-006`
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-ECON-UI-006
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent，先取证主题令牌与预览 CSS → 审计七条 → 再修复 → 编译/单测/Gate）
- Scope(摘要): 社会背景面板的实测缺陷审计与修复（P0 溢出丢行、P1 档位撞色与截断无悬停、P2 文案英文 token、P3 描边/注释/守卫）

### 一句话动机
用户截图反馈「对社会背景面板进行优化，审计」。审计发现**七条缺陷**，其中一条使整行数据
在玩家侧静默消失（第三条纹丝不动地存在，只是没被画出来）。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `Economy/UI/EconomySocietyPanel.cs` | M | `Draw`（+`ref scroll`） · `BuildPlan`/`PlanTotalHeight`/`DrawPlan`/`Plan`/`EnsurePlanCapacity`（新，**布局计划**） · `VerdictHeight`/`DrawVerdict`/`DrawTierRow`/`DrawQa`/`DrawChips`（不再返回 y） · `MeasureChipsHeight`/`ChipNeedsWrap`（新，**测量与绘制共用换行判定**） · `TierTone`（重映射） | 1-420 | **P0** 溢出改为内层滚动（不再 break 丢行）；**P1** 档位色重映射 + 两处挂悬停；**P3** 补 hair 描边 + `descWidth` 守卫 + null 行不抛 | UI-001 / LAND-001 / LAND-012 / PERF-001 / GOV-010 | UI / PERF |
| `Economy/UI/EconomyPanelChrome.cs` | M | `DataRow` | 216-240 | 按预览补底部分隔线（引擎无虚线 ⇒ 1px 实线，B 类降级），并更正"承诺了没做"的原注释 | LAND-001 / GOV-013 | UI |
| `MasterControl/MasterControlTabWindow.Economy.cs` | M | `economySocietyScroll`（新字段） · `DrawEconomyGridPage`（调用点） | 60-80,135-145 | 滚动态**归调用方**（窗口实例字段，与 `economyMarketScroll` 同规；GATE-UI / ARC-008） | ARC-008 / GATE-UI | UI / ARCH |
| `Languages/ChineseSimplified/Keyed/MVS.xml` | M | `MVS.Economy.Society.Faction.Royal.A2` | 229 | 去英文 defName（`Metallic`/`Fabric`/`Woody` → 金属/织物/木质）并压缩长度 | GOV-010 / GOV-009 | LOC |
| `docs/changelog/v1.4.x_2026-09-19_社会背景面板审计与修复.md` | A | `CHG-20260919-ECON-UI-006` | 1-130 | 更新日志：七条审计结论（带证据）+ 布局计划的设计理由 + 4 项残余风险 | REL-001 / REL-004 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-006.md` | A | `CHG-20260919-ECON-UI-006` | 1-140 | 本文件自身（GATE-DELTA 要求标注覆盖**全部**实际改动文件，含标注自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步至发布区
> （**最终** MD5 `DAFE64C04C7E8D3A3FC6E4F3756A4F50`，两区一致；`DataRow` 补线后重编过一次，以此值为准）。

### 关键 diff 片段

```diff
// EconomySocietyPanel.cs  （P0：满了就 break ⇒ 改为布局计划 + 内层滚动）
- if (tier == null || y + 26f > body.yMax) { break; }        // 静默丢行
- ...
- if (qa == null || y + RowHeight > body.yMax) { break; }    // ← 实测吞掉了 Q3「门槛·敏感度」
+ BuildPlan(snapshot, body.width);
+ float needed = PlanTotalHeight();
+ if (needed > body.height + 0.5f) {
+     BuildPlan(snapshot, innerWidth);      // 窄一个滚动条宽 ⇒ chips 换行点会变，必须重建
+     needed = PlanTotalHeight();
+     Widgets.BeginScrollView(body, ref scroll, new Rect(0f, 0f, innerWidth, needed));
+     try { DrawPlan(content, snapshot); } finally { Widgets.EndScrollView(); }
+ }
```

```diff
// EconomySocietyPanel.cs  （P1：档位色撞色 —— Epic 下 accent 与 info 都是金，距离仅 0.13）
- case 1: return UITheme.Info;
- case 2: return UITheme.Accent;
- default: return UITheme.Alive;
+ case 1: return UITheme.TimelineSocial;   // 预览 --t-floor #967EBC 与该令牌逐字节相同（紫）
+ case 2: return UITheme.Accent;           // 预览 --info 金
+ default: return UITheme.Alive;           // 预览 --gain 绿（本来就对）
```

```diff
// EconomySocietyPanel.cs  （P1：截断必须配悬停；P3：描述区不足 24px 就不画，避免叠字）
- float descWidth = Mathf.Max(16f, row.xMax - 9f - metaWidth - 9f - x);
- UIComponents.Label(new Rect(x, row.y, descWidth, row.height), TruncateToWidth(tier.Desc, …));
+ float descWidth = Mathf.Max(0f, metaX - 6f - x);
+ if (descWidth >= 24f) {
+     UIComponents.Label(…, TruncateToWidth(tier.Desc, descWidth, GameFont.Tiny), …);
+     TooltipHandler.TipRegion(new Rect(x, row.y, descWidth, row.height), tier.Desc);
+ }
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏（生成脚本自检 declared = hit）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— 005/004/003 批次遗留文件由 `-ExcludeFromFile` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致（`CHG-20260919-ECON-UI-006`）
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留（旧 `DrawTableBody` 式返回 y 的写法已整体替换，非留空）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| 预览三档档位色的真实取值 | 读预览 CSS：`.tier.t1/t2/t3{color:var(--t-floor/--info/--gain)}` + 主题块变量定义 | t1=紫 `#967EBC` / t2=金 / t3=绿 |
| 工程主题令牌里有没有紫 | 读 `UITheme.cs` 三个 Palette 块（Epic/Gothic/Steampunk） | 有：`timelineSocial` 在 Epic 下 **= (0.588,0.494,0.737)，与预览 `--t-floor` 逐字节相同** |
| 旧映射到底撞不撞色 | 比对 Epic 的 `accent`(0.831,0.686,0.216) 与 `info`(0.855,0.706,0.341) | **撞**（欧氏距离 0.13）⇒ 旧注释"三档可辨"的断言不成立 |
| `SectionHeader` 能不能改 | 检索全工程调用点 | 四个经济面板共用 ⇒ **不动**（只在社会背景面板内解决） |
| `DataRow` 能不能改 | 检索全工程调用点 | **仅本面板使用** ⇒ 可安全补分隔线 |

### 回滚定位
1. **退回无滚动**：`Draw` 里去掉 `BeginScrollView` 分支、直接用 `DrawPlan(new Rect(body.x, body.y, body.width, needed))` —— 会重新丢行，**不建议**。
2. **只退档位色**：`TierTone` 三个 `return` 改回旧令牌（会重新撞色）。
3. **运行时止损**：`BuildPlan` 末尾追加块即可自动纳入滚动，无需改绘制代码（计划的收益）。
