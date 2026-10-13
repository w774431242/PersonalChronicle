---
change_id: CHG-20260919-MVS-AUDIT-001
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Archive/**, Source/PersonalChronicle/MasterControl/**, Source/PersonalChronicle/Economy/UI/**, Source/PersonalChronicle/Application/**, Languages/**, docs/**]
files:
  - path: Source/PersonalChronicle/Archive/UI/UIComponents.KpiGrid.cs
    type: M
    symbols: SixGrid（裁剪栈 finally） / scrollOpen / groupOpen
    lines: 88-135, 370-400
  - path: Source/PersonalChronicle/Archive/UI/UIComponents.cs
    type: M
    symbols: ClickableCard（箭头热区优先级）
    lines: 344-385
  - path: Source/PersonalChronicle/Archive/UI/RuifBridge.cs
    type: M
    symbols: TryLineChartMulti / TryStatTableLegacy / TryRankList / TryAreaChart / TryCapacityBar / TryLineChart / TryDonutChart / TryBarChartV / TryStatTableEx（门控统一走 Available）
    lines: 250-640
  - path: Source/PersonalChronicle/Archive/ArchiveMainTabWindow.cs
    type: M
    symbols: DoWindowContents / DrawWindowContentsCore（异常隔离）
    lines: 279-320
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.cs
    type: M
    symbols: DoWindowContents / DrawWindowContentsCore（异常隔离）
    lines: 132-175
  - path: Source/PersonalChronicle/Economy/UI/EconomyPanelChrome.cs
    type: M
    symbols: SectionHeader（+tip、截断自动并入） / MergeTip
    lines: 208-265
  - path: Source/PersonalChronicle/Economy/UI/EconomySocietyPanel.cs
    type: M
    symbols: TierTone（色环） / BlockSectionPopulation 与 BlockSectionTraits 挂 tip / EnsureDonutTexture 签名
    lines: 275-300, 430-460, 560-620
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs
    type: M
    symbols: AppendDynamicMarketTraits（符号统一）
    lines: 340-370
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.Society.cs
    type: M
    symbols: AppendDerivedChips（因子归属） / BuildScaleTip（移除因子并轨） / 降级分支标题
    lines: 175-200, 275-300, 340-365
  - path: Source/PersonalChronicle/Application/ReadModels/EconomySocietySnapshot.cs
    type: M
    symbols: FactorsTip 注释与实现同步
    lines: 95-115
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: Market.Sum.Index / Society.Market.Gap / Society.Market.Stock（改）· Scale.TitleSettlements（+1）
    lines: 216-245, 331-340
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: 同上
    lines: 208-238, 321-330
  - path: docs/changelog/v1.4.x_2026-09-19_审计修复批.md
    type: A
    symbols: CHG-20260919-MVS-AUDIT-001
    lines: 1-140
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-AUDIT-001.md
    type: A
    symbols: CHG-20260919-MVS-AUDIT-001
    lines: 1-120
  - path: docs/设计文档/架构/审计-前端设计拥挤与失效-2026-09-19.md
    type: A
    symbols: 审计报告（本批的输入依据）
    lines: 1-200
---

# 变更标注 · CHG-20260919-MVS-AUDIT-001（前端审计修复批）

**Change ID：** `CHG-20260919-MVS-AUDIT-001` · **Version：** v1.4.x · **Date：** 2026-09-19
**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物

## Change Annotation
- 用户「**批量执行**」⇒ 按审计报告 §5 的处置顺序执行
- Scope(摘要): 裁剪栈 P0 · 页面隔离 P0 · 本日自引入 6 条 · P1 便宜项 3 条 = **12 项**

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|
| `Archive/UI/UIComponents.KpiGrid.cs` | M | `SixGrid` | **Begin/End 全部入 finally**（状态变量计数逆序闭合）—— 修 P0 裁剪栈失衡 | UI-001 / LAND | UI |
| `Archive/UI/UIComponents.cs` | M | `ClickableCard` | **先判箭头区**再判卡身 ⇒ 消除"画得出来点不到"的假热区 | UI-001 | UI |
| `Archive/UI/RuifBridge.cs` | M | 9 处图表桥 | 门控由字段 `available` 改为属性 **`Available`**（触发惰性重试）⇒ RUIF 晚加载可自愈 | PERF-001 / GOV-004 | PERF |
| `Archive/ArchiveMainTabWindow.cs` | M | `DoWindowContents` | **整页 try/catch + 记日志**（原版标签页侧此前无兜底） | UI-001 / GOV-004 | UI |
| `MasterControl/MasterControlTabWindow.cs` | M | `DoWindowContents` | **整页 try/catch + 记日志**（该目录此前唯一 catch 是注释） | UI-001 / GOV-004 | UI |
| `Economy/UI/EconomyPanelChrome.cs` | M | `SectionHeader` / `MergeTip` | 共享组件补**悬停**，并自动并入被截断的 label/note ⇒ 治系统性截断缺口 | UI-001 / LAND-009 | UI / LOC |
| `Economy/UI/EconomySocietyPanel.cs` | M | `TierTone` / 段头 tip / 签名 | 档位色改**色环**（与段数解耦）；段头挂 tip；纹理签名补段序号 | UI-001 / PERF-001 | UI |
| `.../BuiltinEconomyProvider.MarketPanel.cs` | M | `AppendDynamicMarketTraits` | 供需符号与市场面板**统一**（供应 − 需求） | DATA-007 | DATA |
| `.../BuiltinEconomyProvider.Society.cs` | M | `BuildScaleTip` 等 | 因子归属改挂市场特征段头；降级分支标题成对切换 | GOV-010 / UI-001 | UI |
| `Application/ReadModels/EconomySocietySnapshot.cs` | M | `FactorsTip` 注释 | 注释与实现同步（**注释腐烂是审计 §3.4 的模式**） | DATA-001 | DATA |
| `Languages/*/Keyed/MVS.xml` | M | 4 键 | 「均价」→供需比 · 「需求总缺口」→供需净值 · 「供应库存」→本方库存 · +`Scale.TitleSettlements` | GOV-010 / LOC-001 | LOC |
| `docs/changelog/v1.4.x_2026-09-19_审计修复批.md` | A | `CHG-...-AUDIT-001` | 更新日志（含**未做项与原因**表） | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-AUDIT-001.md` | A | `CHG-...-AUDIT-001` | 本文件自身（自指一条） | ANNO-001 / GOV-013 | REL / DELTA |
| `docs/设计文档/架构/审计-前端设计拥挤与失效-2026-09-19.md` | A | 审计报告 | 本批的输入依据（上一轮落盘、**从未进过任何标注** ⇒ 本批补入） | ANNO-001 | DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll`
> 已重编并同步（MD5 `BED2A5333A9B3C12B307E197333E9D3B`，两区一致）。

### 关键内容片段

```diff
// KpiGrid —— 裁剪栈入 finally
+ bool scrollOpen = false; bool groupOpen = false;
  try {
      Widgets.BeginScrollView(rect, ref scroll, inner); scrollOpen = true;
      ... GUI.BeginGroup(card); groupOpen = true; ... GUI.EndGroup(); groupOpen = false;
- }
- finally { GUI.color = prevColor; Text.Font = prevFont; }
+ } finally {
+     if (groupOpen) { GUI.EndGroup(); }
+     if (scrollOpen) { Widgets.EndScrollView(); }
+     GUI.color = prevColor; Text.Font = prevFont;
+ }
```

```diff
// ClickableCard —— 箭头热区必须先判（否则事件已被整卡消费，判定恒 false）
- bool clicked = Widgets.ButtonInvisible(rect);   // 整卡：含箭头条 ⇒ 抢走事件
- if (subClickable && !jumpClicked) { arrowBtnRect … }   // ← 恒不可达
+ if (subClickable && Widgets.ButtonInvisible(arrowBtnRect)) { jumpClicked = true; }
+ if (!jumpClicked) { /* 右键快捷 */ else clicked = Widgets.ButtonInvisible(rect); }
```

```diff
// TierTone —— 令牌不足 5 枚且会撞色 ⇒ 改用色环
- case 1: return UITheme.TimelineSocial; case 2: return UITheme.Accent; default: return UITheme.Alive;
+ float hue = Mathf.Repeat(0.58f + Mathf.Max(0, tierIndex) * 0.137f, 1f);
+ return Color.HSVToRGB(hue, 0.52f, 0.86f);
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件（含**上一轮落盘但从未标注过的审计报告**）
- [x] 清单范围 ⊆ Change Summary.Scope
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] 无临时文件残留（`_scripts/_tmp_*` 跑完即删）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| `ClickableCard` 的 `jumpClicked` 已在外层声明 | 审计报告 §2.4 + 本次编译 | ⚠ 我第一版**重复声明**了它 ⇒ 立即修正（**编译拦下**） |
| RUIF 门控哪些走字段 | `search_content "if \(!available"` | ✅ 9 处图表桥走字段、6 处已走属性 ⇒ 统一后 grep 无残留 |
| `FactorsTip` 的 append 在哪个文件 | grep（**第一次打错文件**） | ⚠ 它在 `Society.cs` 的 `BuildScaleTip` 里，不在 MarketPanel ⇒ 定位后才改对 |
| 主题令牌是否够 5 枚可辨色 | 审计 §6.3（Epic 的 `accent`/`info` 都是金、距离 0.13） | ❌ 不够 ⇒ 改用色环（与段数解耦） |

### ⚠ 本批**未做**（显式留后，非遗漏）
派系状态面板无滚动（§2.1）· 面板级隔离（§2.2）· `DrawNoticeBox` 计高（§2.5）·
主题 `Dead≡Threat` 与 `Dim` 对比度（§2.6/§2.7）· `homeTabLabels` 语言失效（§2.9，**未核实的 API 不写**）·
`CompactNav` 与截断补偿全量（§2.10/§3.2）· `Chips` 死字段（§0.7，**声明原文未取证，不凭猜改**）·
~~空态文案重复与链路卡片无标签数字（§7.4/§8.2）~~ —— 🔴 **误引，2026-09-20 撤回**：
该审计**只到 §5，无 §7/§8**；复核审计全文后确认**不存在**这两条。见 `CHG-20260920-MVS-AUDIT-005` 的更正记录。
逐条原因见 Changelog 的"本批未做"表。

### 回滚定位
1. **只回 P0 裁剪栈**：`KpiGrid` 的 flags 与 finally 还原（**不建议** —— 那是真 P0）。
2. **只回标签**：Keyed 四键改回原值（值不动，纯文案）。
3. **只回 UI 归属**：`SocietyPanel` 两处 `SectionHeader` 去掉 tip 实参。
4. **运行时止损**：无 —— 全部为绘制路径与文案，不触存档、不改游戏状态。
