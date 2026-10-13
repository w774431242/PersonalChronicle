---
change_id: CHG-20261005-SETTINGS-UI
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle/Settings, Source/PersonalChronicle, Languages]
files:
  - path: Source/PersonalChronicle/Settings/SettingsCatalog.cs
    type: A
    symbols: SettingsCatalog, SettingsTab, SettingKind, SettingTone, SettingDef, SettingId
    lines: 1-300
  - path: Source/PersonalChronicle/Settings/SettingsDrawer.cs
    type: A
    symbols: SettingsDrawer.Paint, SettingsDrawer.ContentHeight, SettingsDrawer.PaintRow, SettingsDrawer.PaintControl, SettingsDrawer.GetToggle, SettingsDrawer.SetToggle, SettingsDrawer.GetNumber, SettingsDrawer.SetNumber, SettingsDrawer.SegmentedIndexOf, SettingsDrawer.SetSegmentedIndex
    lines: 1-560
  - path: Source/PersonalChronicle/Settings/SettingsText.cs
    type: A
    symbols: SettingsText.EnsureFresh, SettingsText.T, SettingsText.Raw, SettingsText.Tabs, SettingsText.Options
    lines: 1-130
  - path: Source/PersonalChronicle/ChronicleSettings.cs
    type: M
    symbols: ChronicleSettings.DoSettingsWindowContents
    lines: 185-235
  - path: Languages/ChineseSimplified/Keyed/SettingsUi.xml
    type: A
    symbols: PersonalChronicle.Settings.Tab.*, PersonalChronicle.Settings.Use.*, PersonalChronicle.Settings.Detail.*, PersonalChronicle.Settings.RestShort.*
    lines: 1-90
  - path: Languages/English/Keyed/SettingsUi.xml
    type: A
    symbols: PersonalChronicle.Settings.Tab.*, PersonalChronicle.Settings.Use.*, PersonalChronicle.Settings.Detail.*, PersonalChronicle.Settings.RestShort.*
    lines: 1-85
---

# 变更标注 · CHG-20261005-SETTINGS-UI

## Change Summary

玩家反馈（2026-10-04）：

> 「我对 mod 设置不满意，太乱了，没有模块分组，没有信息简化。没有声明来源、用途等等，
> 太粗糙了。且可以基于 RUIF 框架 mod」

**整块重做 Mod 设置页的绘制层**。行为与数据**零改动**：字段、默认值、Scribe 键名一律未动。

### 诊断（为什么"太粗糙"）

| 反馈 | 根因 |
|---|---|
| 无模块分组 | 14 类设置平铺成一条长列表，只有 `listing.Label` 做视觉分隔，且顺序 = **代码历史追加顺序** |
| 无信息简化 | 只有 1 项有说明；`DebugLiveCount` 之类标题玩家看不懂 |
| 无来源/用途 | 完全没有 —— 而**代码注释里全都有**（CHG/EXC 编号、默认值理由、影响代价），只是从未呈现 |
| 太粗糙 | 还在用原版 `Listing_Standard` 裸控件，而 PC 其它界面已全面走 RUIF（126+ 处 `RUIF.Draw.Box` 等） |

### 信息架构（5 页签，按"你在干什么"分组）

| 页签 | 行数 | 内容 |
|---|---|---|
| 档案 | 5 | 记录总开关 / 单人上限 / 自动归档（开关 + 银币阈值 + 保留天数） |
| 车间 | 6 | 离班停工 / 在班回写 / 摸鱼切分 / 自动判定类型 / 跨天重放 / 默认作息策略 |
| 作息与用餐 | 6 | 三餐开关 / 档位 / 早午晚三行（起止步进）/ **班次级用餐指路行** |
| 界面 | 2 | 主题 / 工作面板隐藏名单（恢复入口） |
| 诊断 | 3 | 性能探针 / 调试计数 / **启动自检**（旧版有字段但从未暴露） |

### 行形态（"简化 + 声明来源"的载体）

```
▸ 在班自动回写            [ ●——]        CHG-20261004-WSSETTINGS
  班次开始时把工作档位补回来，避免"上班了却排不出活"。
  （展开后追加）默认开：这是修复"写 0 与回写不对称"的缺陷，不是行为偏好…
```

- **默认收起** ⇒ 列表干净；点箭头展开 ⇒ 用途 / 默认值理由 / 影响代价（截断可见 + 悬停全文）
- **来源角标**按性质配色（新增 `Alive` / 修复 `Info` / 实验 `Warn` / 核心 `Dim`）
  ⇒ 避免"所有卡片长一个样只换文字"（skill 反劣质黑名单）
- 角标直接显示 **CHG/EXC 编号**（稳定标识不是可译文本，中英界面同一编号，GOV-009）

### 技术选型（全部 RUIF 既有组件，契约已逐项取证）

| 用途 | 组件（Cecil 取证的真实签名） |
|---|---|
| 分页 | `NavAtoms.TabBar(Rect, string[], ref int, ref float)` |
| 分节容器 | `Atoms.SectionFrame(Rect, string)` → **返回值即内容区** |
| 开关 | `InputAtoms.ToggleSwitch(Rect, ref bool)` |
| 滑块 | `InputAtoms.SliderWithValue(Rect, ref float, min, max, roundTo, Func<float,string>)` |
| 小时起止 | `InputAtoms.Stepper(Rect, ref int, min, max, step)` |
| 分段单选 | `InputAtoms.Segmented(Rect, string[], ref int)` |
| 展开箭头 | `StatusAtoms.Caret(Rect, bool expanded, Color?)` |
| 滚动 | `Layout.BeginScroll(Rect, float, ref Vector2, bool)` / `Layout.EndScroll()` |
| 度量 / 令牌 / 绘制 | `Measure.*` · `Tokens.*`（间距/色/字档）· `Draw.*` |

### 纪律遵守（RimWorld 组件绘制 skill）

| 铁律 | 落实 |
|---|---|
| 1 度量派生、禁字面量行高 | 全部 `Measure.RowHeight/ControlHeight`；结构常量仅"控件列宽"并注明来源 |
| 2 标签排版归调用方 | 标题走 `EllipsisLabel` + 单元矩形预算；无绝对定位 |
| 3 溢出必被遮盖 | 行底先画 → 内容 → 分隔线在行末；逐行 `try/catch` 隔离；`EndScroll` 放 `finally` |
| 4 先算空间预算 | `MinPanelW/H` 断言 + 降级文案；`ContentHeight` 按可用宽缓存 |
| 绘制期零翻译 | `SettingsText` 缓存全部文案，按**语言对象引用**失效（零分配比较） |
| 组件零状态 | 页签/滚动/展开态由 `SettingsDrawer` 持有（受控静态，不入存档） |

## 兼容性

| 项 | 结论 |
|---|---|
| 存档影响 | **无**。未新增/未改动任何字段与 Scribe 键名 ⇒ 旧档正常加载、无需 Migration、不 bump `schemaVersion` |
| 行为影响 | **无**。所有取值/写值与旧实现逐字等价（含 `MealScheduleService` 的 UI 不直写字段纪律 MATRIX-003） |
| 回滚 | 一行：把 `DoSettingsWindowContents` 里的 `SettingsDrawer.Paint(inRect, this)` 改回 `LegacySettingsContents(inRect)`（旧实现按此目的保留一轮） |
| 第三方 | 无新 Def、无 Harmony；RUIF 本就是硬依赖（csproj + About.xml 已声明） |

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（首次编译暴露并修掉两处：入口方法名遮蔽 `RUIF.Draw`、`UITheme` 命名空间） |
| XML | 已验证 | 中英 `SettingsUi.xml` 各解析通过 |
| 部署 | 已验证 | Defs 31 / Languages **39**（含新增 2）/ Assemblies 1 |
| 组件契约 | 已验证 | 全部单元已 Cecil 取证（签名见上表），非凭直觉 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-SETTINGS-UI.md -ExcludeFromFile .codebuddy/tmp/settingsui-out-of-scope.txt -Blocking` |
| 实机 | **未验证** | 需重启后目视：5 页签是否显示、控件是否可点、窄窗口降级是否触发 |

## 实机修正（第 2 轮，2026-10-05 玩家截图驱动）

玩家给出 5 个页签的实机截图 ⇒ 逐张核对后修正 **9 类问题**。其中 4 类的根因是**组件自画内容**
（skill 红线 1「落地前必答：它自己画了什么」）—— 我第一次落地时漏做该取证，这轮补齐：

| # | 现象（截图） | 根因（源码级） | 修法 |
|---|---|---|---|
| 1 | 滑块行只有 `10 / 1000`，**看不到当前值** | `InputAtoms.SliderWithValue` 把值画在 **rect 上方 18px**（源码 `sliderRect.y - 18f`）⇒ 顶到标题行 | 改用同一底层控件 `Widgets.HorizontalSlider`（RUIF 也是这么调的）+ 当前值自画在滑块**左侧独立列**（56px） |
| 2 | 三餐步进器读数看不清 | `Stepper` 内部 = `−`(26) + 值 + `+`(26)；我把它拆成**半宽 66px** ⇒ 数值只剩 14px | `DualIntStep` 控件列加宽到 310（每个约 152） |
| 3 | 「餐段优先级」右侧文字溢出 | `Segmented` 用 `Draw.Label`（**不截断**）⇒ 长选项必然溢出 | 新增短标签键 `MealPri.Short.Normal/Urgent`（"正常/优先"） |
| 4 | 开关"悬"在行中间，不贴右 | `ToggleSwitch` 从 **rect 左端**开始画（轨道 36px）⇒ 我给 140px 右对齐列，右侧留白 80px | `Toggle` 控件列收窄到 78（= 轨道 36 + 6 + "开"） |
| 5 | 4 处**标题/用途直出键名** | `Use.AutoArchivePurge`（XML 里是 `…PurgeDays`）、`Settings.RestPolicy`、`Settings.ShiftMealPoint`、`Settings.EnableStartupSelfTest` 键不存在 | 修正键名 + 新增缺失键；并加**自动核对**（见下） |
| 6 | 标题被截断成半句（离班停工、性能探针等） | Mod 设置窗口实测仅约 **590px**，完整长标题放不下 | 新增 14 个**本页专用短标题**（`Title.*`）—— 不改既有长标题键，车间面板等仍用原键 |
| 7 | 来源角标被截断（`CHG-20261004-WSSETTI…`） | 完整编号 ≈210px，挤掉标题 | 角标改**两段式**：列表显示短形式（`1004·WS-SET`），**悬停显示完整编号**（`BadgeFull`） |
| 8 | 主题行**没有控件** | 我在 `Theme` 行没给 `OptionKeys`（主题是运行时集合），`Segmented` 收到 null ⇒ 直接不画 | 主题选项改为**动态取** `UITheme.ThemeIds` + `GetDisplayName`（3 个：史诗/哥特/蒸汽朋克） |
| 9 | 文案里的 `**` 星号被原样显示 | 我误用了 Markdown 强调语法（游戏不渲染） | 删除全部星号 |

**新增护栏（防第 5 类再犯）**：交付前跑一段"键名核对"脚本 —— 从 `SettingsCatalog.cs`
提取全部键后缀，逐个检查是否存在于 `Languages/ChineseSimplified/Keyed/*.xml`
（**只以 XML 为源**，避免把 Catalog 自身算作来源的自证循环）。本轮结果：`ALL KEYS PRESENT`。

另注：截图里三餐起止显示为异常值，一度怀疑读取失败；读玩家配置
（`Config/Mod_…PersonalChronicleMod.xml`）确认 `mealBreakfastStartHour=23` 等**已正确落盘**
⇒ 是第 2 项的显示宽度问题，不是数据问题。

## Remaining（本标注未完成项）

1. **精美度走查**（skill `09` 册 5 维度 + 反劣质 12 条）**待实机截图后**逐条核对
   —— 无实机画面时无法判定"层次/呼吸/对齐"。
2. **旧实现删除**：`LegacySettingsContents` + `DrawWorkTabOriginalTab*` 三个旧方法暂留作回滚对照，
   下版随本标注删除（它们只被彼此引用，删除无行为影响）。
3. **详情文案续补**：`Detail.*` 目前覆盖 7 行（车间/用餐/诊断的重项）；
   档案页 5 行暂只有"用途"一句，按需补"默认值理由"。
4. `ChronicleSettings` 内 4 个旧滑块边界常量（`EventsPerPawnSliderMin` 等）现已由绘制层自带
   同名常量，随第 2 项一并清理。

## 实机验证要点（重启后）

1. **选项 → Mod 设置 → Personal Chronicle**：顶部应出现 5 个页签（档案 / 车间 / 作息与用餐 /
   界面 / 诊断），默认停在「档案」。
2. 每行左端有 ▸ 箭头 ⇒ 点它展开"用途 / 默认值理由"；再点收起。
3. 行右侧控件可点：开关切换立即生效；「作息与用餐」页的三餐起止用 **+/- 步进**；
   「车间」页的作息策略是三段分段。
4. 换页后滚动条回到顶部；把窗口拉窄到 420px 以下应显示"窗口太小"降级文案而不是画崩。
5. **行为回归**：切主题、开关记录、自动归档阈值——改完关窗重开应保持（值仍写入原字段）。
