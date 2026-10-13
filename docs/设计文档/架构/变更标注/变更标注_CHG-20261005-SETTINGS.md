---
change_id: CHG-20261005-SETTINGS
version: v1.4.1
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle/Settings/, Source/PersonalChronicle/ChronicleSettings.cs, Languages/ChineseSimplified/Keyed/SettingsUi.xml, Languages/English/Keyed/SettingsUi.xml]
files:
  - path: Source/PersonalChronicle/Settings/SettingsCatalog.cs
    type: M
    symbols: SettingsTab (Meal 空档), SettingsCatalog.VisibleTabs, SettingsCatalog.VisibleTabKeys, SettingsCatalog.TabKeyOf, SettingId (移除 =11), SettingsCatalog.RowsOf, SettingsCatalog.WorkshopRows, SettingsCatalog.UiRows, SettingsCatalog.MealRows(删)
    lines: 16, 120-126, 155-206, 227-230, 255-258, 274-276, 291-297
  - path: Source/PersonalChronicle/Settings/SettingsDrawer.cs
    type: M
    symbols: SettingsDrawer.Paint, SettingsDrawer.IndexOfTab, SettingsDrawer.PaintControl, SettingsDrawer.RowEnabled, SettingsDrawer.PaintDisabledSlider, SettingsDrawer.GetToggle, SettingsDrawer.SetToggle, SettingsDrawer.EventsPerPawnMin
    lines: 92-118, 138-151, 404-416, 449-480, 511-516, 530-535, 561-569
  - path: Source/PersonalChronicle/Settings/SettingsText.cs
    type: M
    symbols: SettingsText.Tabs, SettingsText.InvalidateTabs
    lines: 76-104
  - path: Source/PersonalChronicle/ChronicleSettings.cs
    type: M
    symbols: ChronicleSettings.AutoRestoreOnShift (doc), ChronicleSettings.ExposeData
    lines: 74-79, 153-155
  - path: Languages/ChineseSimplified/Keyed/SettingsUi.xml
    type: M
    symbols: (整表) 删 13 键 / 修 2 键 / 合并 1 组重复 / 增 1 键
    lines: 1-124
  - path: Languages/English/Keyed/SettingsUi.xml
    type: M
    symbols: (整表) 与中文同步
    lines: 1-122
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: Settings.MealSection (文案改中性), 删 10 个零引用三餐旧键
    lines: 3700-3708
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: Settings.MealSection (新增 —— 修复「英文缺在用键」P1)
    lines: 3691-3694
---

# 变更标注 · CHG-20261005-SETTINGS

## 背景

玩家（PM）在发布前验收 Mod 设置页时判定：「你的 mod 设置太不重要了，胡乱就植入到 mod 设置」
—— 指大量**内部实现细节**被做成玩家开关，而真正的玩家决策项反而缺控件、缺联动、缺护栏。
方案见 `docs/设计文档/方案/Mod设置重构_方案_2026-10-05.md`（AI-001 交付）；本批为其 **§7 待裁决清单
经 PM 批准后按建议实施**的落地。

## 一、逐项处置（对应方案 §7 与 §2）

| # | 项 | 处置 | 实现 |
|---|---|---|---|
| 1 | 在班自动回写 | **默认化移除** | 目录行与 `SettingId=11` 移除（编号留空档）；`ExposeData` **停止读取** `autoRestoreOnShift` 键；字段保留但恒 `true` |
| 3 | 每位人物最大事件数 | 文案修正（D3） | 由「按时间淘汰最旧的」改为「到达上限后不再记录新事件」—— 与 `AddEvent` 实际行为一致（**无淘汰逻辑**） |
| 4 | 同上 · 下限 | **阈值化准入** | `EventsPerPawnMin` 10 → **72**，消除 D6 预算反转（见下） |
| 5 | 保留天数 30/7 疑点 | 已查清，**不改** | 下限本就是 7（`PurgeDaysMin`），30 是字段默认值 ⇒ 非缺陷 |
| 6 | 界面主题行 | **D2 修复** | 原声明**漏传 `optionKeys`** ⇒ `Options()` 返回 null ⇒ `Segmented` 分支 `break` ⇒ **控件根本不画**。补上并复用 `Archive.xml` 既有主题名键 |
| 7 | 「作息与三餐」空页 | **删页 + 并入车间页** | 页签 5 → 4；唯一幸存的指路行并入 `WorkshopRows` 尾部 `SecMeal` 分节；`SettingsTab.Meal` 留空档，`RowsOf` 加安全别名 |
| 8 | 诊断页 | **DevMode 门控** | 普通玩家只见 3 页；`VisibleTabs` 按 `Prefs.DevMode` 重建并回调 `InvalidateTabs`；`Paint` 增加「停在被隐藏页 ⇒ 钳制回首页」 |
| 9 | 孤儿键 | **清理** | 删 13 键（全局三餐旧键 5 + 标题 2 + 餐段优先级 2 + 本页 `MealPriorityLabel` + 已移除项的 `Title/Use/Detail.AutoRestore` 3）；另修 **1 组重复定义**（`Detail.AutoDetect` 在中英各被定义两次 ⇒ 后者覆盖前者） |
| D1 | 归档子项联动丢失 | **修复** | 新增 `RowEnabled` 父控判定；关闭「自动归档」时两个阈值行改为**只读渲染**（原版 `Widgets.HorizontalSlider` 不认 `GUI.enabled`，故不画真滑块）+ 悬停提示新键 `NeedParentOn` |

## 二、D6 预算反转（§7-4 的依据，已在代码注释中留档）

`Application/ArchiveService.Battle.Helpers.cs:40-42`：

```csharp
int retentionLimit = ev.ImportanceLevel <= (int)ChronicleImportance.Routine
    ? Math.Max(24, maxEvents / 3)
    : maxEvents;
```

- `max < 24` ⇒ routine 预算(24) **反超**总上限（语义倒挂，既有缺陷）；
- `24 ≤ max < 72` ⇒ routine 被固定压在 24，"routine 占 1/3"的声明失真；
- `max ≥ 72` ⇒ `max/3 ≥ 24`，声明比例才成立。

故下限取 **72 = 3×24**（默认值 200 不变）。

## 三、本地化（GATE-LOC 证据）

- 中英 `SettingsUi.xml` 键集 **51 = 51**，无单边键、无重复键（脚本核对，见验证段）。
- 主题短标签**复用** `PersonalChronicle.UI.Theme.{Epic,Gothic,Steampunk}`（Archive.xml 既有），不新增重复文案。
- 新增 1 键：`PersonalChronicle.Settings.NeedParentOn`（中英各一份）。

## 四、存档 / 配置影响（GATE-SAVE 证据）

- **ModSettings 不随存档保存**（实测：`.rws` 内无 `ModSettings` / `ChronicleSettings` 节点）⇒ 本批**不触存档 schema**，`SchemaVersion` 不 bump。
- 唯一停止读取的配置键 `autoRestoreOnShift`：旧 `Config/Mod_*.xml` 里的残留键被 Scribe 直接忽略，**无需 Migration**（沿 `CHG-20261005-MEALBYSHIFT` / `CHG-20261004-WORKTAB-ORIGINAL` 两次先例）。
- ⚠ **行为变更（须进 Changelog）**：曾把「在班自动回写」关掉的玩家，升级后该行为**恒开**。

## 五、验证

| 项 | 第一轮 | 第二轮（GATE 复核收口后） |
|---|---|---|
| 编译（`_scripts/compile_with_sdk.ps1`） | 0 错 0 警 | **0 错 0 警**（14:54 UTC 时间戳） |
| 部署（`_scripts/deploy-to-game.ps1`） | 127 文件；DLL 13:41:46 | **127 文件**；DLL **13:55:00** |
| 中英 `SettingsUi.xml` 键集 | 51 = 51，无单边 / 无重复 | 51 = 51，无单边 / 无重复 |
| `Settings.MealSection`（P1） | zh=1 / **en=0 ⇒ BLOCKED** | zh=1 / **en=1 ⇒ 解除** |
| 三餐旧键残留 | — | **0**（删 10 个零引用键） |
| Scribe 审计（`_scripts/audit-scribe.ps1`） | SCANNED=63 / HIT_FILES=9（均既有误报） | 未重跑（本批未再动持久化面） |
| 实机 | **未执行**（游戏未运行） | **未执行**（同上）—— 需重启 RimWorld 后按下方验收点核对 |

### 实机验收点（需 PM 执行，3 分钟）

1. 设置 → 页签只剩 **档案 / 车间 / 界面** 三页（未开开发者模式）；
2. 「车间」页**不再有**「在班自动回写」行；页尾多出「三餐已移到班次里配置」分节；
3. 「档案」页关掉「低价值档案自动归档」⇒ 下面两个阈值行**变成灰条不可拖**，悬停有提示；
4. 「界面」页「界面主题」行**出现三段可点控件**（修复前是空白，只有角标）；
5. 开开发者模式（`Options → 开发者模式`）⇒ 页签**多出「诊断」**；关掉后若当时停在诊断页，应**自动回到档案页**。

## 六、残留（照实登记）

1. `ChronicleSettings.AutoRestoreOnShift` **字段本身保留**（消费点 `MapComponent_WorkshopManager.WorkEnable.cs:455` 仍在读它，恒 `true`）—— 与同文件的 `LegacySettingsContents` 等**已不参与绘制的旧实现**一并列入下一版清理（旧实现的自注释亦声明「下一版随标注删除」）；现在删字段会连带破坏该 legacy 块编译。
2. 方案 §7-2「跨天重放作息表」建议为 **C→B 两步**（先加残留检测再默认化）—— **本批未动**，仍保留原样，留作独立任务。
3. 方案 §7-5（保留天数 30/7 实机复核）、§7-6（主题行实机确认）属**实机动作**，本批未执行；代码侧 §7-6 的修复（补 `optionKeys`）已完成。
4. `Languages/ChineseSimplified/Keyed/Archive.xml` 在本批**第二轮**被改动（删 10 个零引用三餐旧键 + 分节标题改中性文案）；同目录 `Languages/ChineseSimplified/Keyed/Archive.xml` 之外的改动（如 `English/DefInjected/MainButtonDef/*`）属既存工作区变更，**不在本批范围**。
5. P3-e：`ChronicleSettings` 内 legacy 常量 `EventsPerPawnSliderMin = 10f` 与新 `EventsPerPawnMin = 72f` 不一致 —— legacy 块已不参与绘制，仅作回滚对照，故未强改；下一版删 legacy 块时一并消除。
6. P3-g：`SettingsText.tabs` 与 `VisibleTabs` 的长度一致性目前靠"同源派生 + 双向失效"保证（已由复核推演为无竞态），未加 `Debug.Assert` 防御。
7. 方案 §7-2「跨天重放作息表」C→B 两步未动（见残留 2）；实机验收项（标注 §5）待 PM 执行。

## 七、GATE-UI / GATE-LOC 独立复核与收口修复（本批第二轮）

由表现层守门 agent 独立只读复核（`pc-ui-loc-guard`，未并行派发其它 agent）。

### 复核结论

| Gate | 结果 | 要点 |
|---|---|---|
| GATE-UI | **PASS WITH EXCEPTION** | 受控静态状态合规（无持久化、无 Scribe）；门控行"不生效而非坏了"成立且写值回路正确；页签下标↔文案数组**无竞态**（两处失效入口任意顺序都自愈）；绘制路径**零分配**；空间/语言/截断兜底齐备。例外 = 1×P2（Rotate 警告缺失）+ 若干 P3 |
| GATE-LOC | **BLOCKED → 修复后解除** | ⛔ 英文（默认语言）**缺在用键** `PersonalChronicle.Settings.MealSection`（`SettingsCatalog.cs:216/274` 在用，中文在、英文 0 命中，部署物同证）⇒ 英文玩家在车间页会看到原始键名。其余：13 删键 + 1 增键**无残留引用**（键族超集搜索）；主题行复用既有键**不构成双份文案**；D3/D4 文案与实现一致；中英键集 51=51 独立复核通过 |
| LAND-* | **N/A** | 本变更为游戏内 IMGUI/RUIF 运行时设置页，不属「HTML/RUIF 预览 → 落地」工作流 |

### 收口修复（第二轮实施）

| 复核项 | 修复 |
|---|---|
| **P1**（BLOCKED） | 英文 `Archive.xml` 补 `<PersonalChronicle.Settings.MealSection>Meals &amp; schedule</PersonalChronicle.Settings.MealSection>`；中文侧同键文案由「三餐作息（到点去吃饭）」改为中性「三餐与作息」（该行现在只是指路） |
| **P2-1** | `Detail.RestPolicy` 文案承诺「选它后会在下方显示警告」但新绘制层不画该警告（旧实现有）⇒ 按文案**补回警告行**：`RowHeight` 改为 settings-aware（`RowHeight(def, s)`，3 处调用点同步），新增 `HasRotateWarning`，在展开详情行之后绘制 `Automation.RestPolicy.Rotate.Warning`（`Tokens.Warn` + 悬停全文） |
| **P2-2** | `EventsPerPawnMin` 10→72 后**旧值 10–71 在新滑块域不可表达**（Changelog 原写"老玩家设置原样保留"不成立）⇒ 在 `ExposeData` 读档单点加**准入护栏** `if (MaxEventsPerPawn < 72) MaxEventsPerPawn = 72;`，保证「设置页显示值 = 实际生效值」，避免"打开设置页才被隐式改掉" |
| **P3-a** | `hasDetail` 判据由 `DetailKey \|\| UseKey` 改为**仅 `DetailKey`** —— 修掉 9 行"点了箭头却什么都不展开"的死箭头 |
| **P3-b** | 用途行截断**补悬停全文**（与详情行一致）；长句 Note 行受益最明显 |
| **P3-c** | 注释漂移「5 页」×3 处（`SettingsCatalog.cs` / `SettingsDrawer.cs` / `ChronicleSettings.cs`）改为 4 页 + 开发者诊断页 |
| **P3-d** | 代码注释内的 Change ID `CHG-PRERELEASE-SETTINGS` 统一为 **`CHG-20261005-SETTINGS`**（19 处 / 7 文件），使 grep 追踪不断链 |
| **P3-f** | 清理中文 `Archive.xml` 内 10 个**零引用**三餐旧键（`MealEnabled` / `MealDisabledHint` / `MealPriorityUrgent` / `MealPriorityNormal` / `MealBreakfast` / `MealLunch` / `MealDinner` / `MealStartHour` / `MealEndHour` / `MealWindowHint`；英文侧从未存在）—— 与本批"清 13 键"口径统一 |
| P3-e / P3-g | **未修**，登记为残留（legacy 常量不一致 / 无 `Debug.Assert` 防御） |

> **复核为准的原则**：GATE-LOC 的 BLOCKED 是复审给出的事实，不是我事后自评；P1-1 的修复使该 Gate 有条件转 PASS，但**我未要求 Gate agent 复审第二轮**（节省计算），故本批的 Gate 终态应记为
> `GATE-UI: PASS WITH EXCEPTION` / `GATE-LOC: BLOCKED→已修复（待复审）`，**不得记为 PASS**。

### 建议门禁记录（供 Orchestrator 落盘）

| Gate | Result | 主要证据 |
|---|---|---|
| UI | PASS WITH EXCEPTION | `SettingsCatalog.cs:161-210`；`SettingsDrawer.cs:94-113 / 389-480 / 562-580`；`SettingsText.cs:27-104` |
| LOC | BLOCKED（P1 已修，待复审） | 键集 51=51（双文件逐键）；13 删键+1 增键零残留；`MealSection` zh/en 各 1 |
| DATA / SAVE | PASS（N/A 存档面） | ModSettings 不入存档；仅 1 键停止读取、无 Migration |
| TEST | PASS（静态）／实机未执行 | 编译 0 错 0 警；部署 127 文件 |

## 八、实机裁图后的前端参数修复（第三轮）

PM 实机截图 + 「优化前端参数，避免文字遮盖、挤兑」要求。按项目惯例**先裁剪放大再判读**
（`System.Drawing` 最近邻 3× 放大关键区域），并到反编译源码取证根因。

### 根因与修复（全部为几何/契约参数，不改行为）

| # | 症状（裁图证据） | 根因（源码证据） | 修复 |
|---|---|---|---|
| F1 | 滑块行轨道上方多出两个数字，与本行左侧「当前值列」在相邻两行上挤成 `v1 · 200 / 72` 三数并列，且 72 易被误读成当前值 | 原版 5 参重载 `HorizontalSlider(rect, ref v, FloatRange, label, roundTo)` 在 `Verse/Widgets.cs:1938-1942` **强制**把 `range.TrueMin/TrueMax` 当作左右对齐标签传下去（`trueMin.ToString(), trueMax.ToString()`），且带标签时 `rect.y += Mathf.Round((rect.height - 10f)/2f)` 把轨道压到列底 | 改调 **9 参重载**：左右标签显式传 `null`（只画轨道），`middleAlignment=true` 使轨道在控件列内居中；范围信息改由**悬停**提供（新键 `RangeFmt`，`SettingsText.RangeTip` 按行缓存 ⇒ 绘制期零分配） |
| F2 | 分段控件（界面主题三段）贴到卡片右侧描边，右侧 0 内边距，与左侧 `CardPadX(12)` 不对称 | `ctrlRect.x = head.xMax - ctrlCol` —— 直接以**行右缘**为锚点 | 引入 `right = head.xMax − Tokens.CardPadX` 作为统一的右侧锚点，标题 / 角标 / 控件共用（右侧内边距与左侧对称） |
| F3 | 滚动条可拖到内容已结束之后（每节约 30px 空白） | 高度模型预留 `SectionTitleHeight(30) + Gap(12) = 42`，而 `PaintSection` 实际从 `SectionFrame` 的内缩位（`outer.y + SpaceXs + 4 = +12`）起画行 ⇒ **每节多算约 30px 幽灵高度** | 高度模型与绘制**严格对齐**：新增 `SectionTitleLead(10)` / `FrameInset(SpaceXs+4=12)` / `SectionLead`，`ContentHeight` 与 `PaintPage` 用同一套常量 |
| F4 | 值列宽 56 时 `1000` 已贴边 | 字宽估算偏紧 | `ValueColW` 56 → **64** |

### 未修并如实登记

| 观察 | 状态 |
|---|---|
| 分节标题疑似被 frame 上边线"划穿"（裁图 G：「三餐与作息」呈删除线观感） | **未修**，且**根因在 RUIF**：`Atoms.SectionFrame` 的断口填充用 `Tokens.Window`（`Atoms.cs:71`），而设置窗口的实际背景是 RimWorld `Window` 自身的贴图 ⇒ 颜色不匹配时断口擦不干净、边线穿过标题。属 RUIF 侧问题（影响所有用 SectionFrame 的页面），本批**不改 RUIF**，需实机确认后另案处理 |
| 标题与首卡间隙 | 修复前后均为 **4px**（未变差）；本次修正的是**整体下移 10px + 消除 30px 幽灵高度**，不声称改善了该间隙 |

### 本轮验证

| 项 | 结果 |
|---|---|
| 编译 | **0 错 0 警** |
| 部署 | 127 文件；DLL **14:06:56** |
| 中英 `SettingsUi.xml` 键集 | **52 = 52**（新增 `RangeFmt` 中英各 1），无单边 / 无重复 |
| 实机 | **未执行**（游戏未运行）—— F1–F4 的观感需 PM 重启后复核（下方验收点已补） |

### 补：第三轮实机验收点

6. 档案页三个滑块：轨道**上方不再有 min/max 数字**，左侧只有**一个**当前值；悬停滑块出现「可调范围：72 – 1000」；
7. 界面页「界面主题」三段右侧**不再贴卡片描边**（左右内边距对称）；
8. 档案 / 车间页**滚到底不再出现空白**（每节少算的 30px 幽灵高度已消除）。

## 九、PM 追加裁决后的第二轮收口（`CHG-20261005-SETTINGS-REMOVE2`）

### PM 裁决原文

1. 「这个 mod 设置移除，保持默认默认开启即可，**不写入 mod 设置中**」→ 指「**离班停工**」（截图 1）
2. 「这部分移除」→ 指「**三餐已移到班次里配置**」只读指路行（截图 2）

### 改动

| 处置 | 内容 |
|---|---|
| **默认化移除** | 「离班停工」：删 `SettingId.OffShiftCancel = 10`（留空档）+ 删 WorkshopRows 该行 + 删 `GetToggle`/`SetToggle` 分支 + `ExposeData` **停读** `enableOffShiftCancel` + `OffShift.cs` 移除入口门控（恒开） |
| **移除** | 「三餐已移到班次里配置」：删该 Note 行 + `SettingId.ShiftMealPoint = 25`（留空档）+ `SecMeal` 常量 + `SettingsTab.Meal` 枚举（重编号 Ui=2 / Diag=3）+ `RowsOf` 的 Meal 别名 |
| **本地化** | `SettingsUi.xml` 中英各删 5 键（`Title/Use/Detail.OffShiftCancel` + `Title/Use.MealMovedToShift`）⇒ **47 = 47**，无单边 |
| **保留（勿清）** | `Archive.xml` 的 `Settings.MealSection`：设置页已不用，但 `ChronicleSettings.cs:360` 的 legacy 回滚块仍引用它 ⇒ 中英各留 1 行并加"勿当孤儿键清理"注释（与 `EnableOffShiftCancel` / `AutoRestoreOnShift` 同类） |

### ⚠ 治理发现（本轮最重要的一条）：既有「写 0」未登记 + 引用错误

- 代码头 `OffShift.cs` 原写 `EXC-2026-025(E2)`，但 **025 是「忽略『无法从事』」的 Harmony 例外**，与本写 0 无关 ⇒ **引用错误**；
- 全仓 EXC **没有任何一份**提到 `OffShift` / `enableOffShiftCancel`；
- 而 `EXC-2026-016` 与 `EXC-2026-023` 的修订段**都声明**「本 Mod **唯一**允许 `SetPriority(x,0)` 的地方是接管通道」；
- ⇒ **该写 0 点当时既无有效登记、又与两份 EXC 的「唯一」声明直接冲突**（既有缺陷，非本次引入）。

**处置**：
1. 新建 **`EXC-2026-026`（E2）** 补登记该写 0 点，并同时登记本次默认化带来的**授权面扩大**（取消玩家退出权 ⇒ 从"可退出的默认行为"升级为"不可退出写入"）；
2. `OffShift.cs` 代码头引用更正为 `EXC-2026-026`（保留错误引用的说明）；
3. 向 `EXC-2026-016` / `EXC-2026-023` **追加修订段**更正「唯一」→「共 2 个写 0 调用点」（原文不改写，沿项目惯例）。

**Architecture Review 判定**：四项硬触发（Harmony / Hardcoding / Cross-Mod Direct Reference / Static Mutable State）**均为 No** ⇒ **不触发 AR**；但因改写上述两份 EXC 的前提，以 E2 登记并请 PM 追认（已由裁决记录覆盖）。

**EXC 内如实登记的缺口**：本链**没有 `MandatoryKeep` 四项的显式守卫**，靠"写入范围封闭 + Def 映射表不含红线类型"的**间接保护**；已在 `EXC-2026-026` F1 写成硬约束并建议后续补显式守卫。

### 本轮验证

| 项 | 结果 |
|---|---|
| 编译 | **0 错 0 警** |
| 部署 | 127 文件（DLL 14:12:19） |
| `SettingsUi.xml` 键集 | **47 = 47**，无单边 / 无重复 |
| 被删键残留引用 | `Title/Use/Detail.OffShiftCancel`、`Title/Use.MealMovedToShift` 均 **0 命中**；`MealSection` 仅剩 legacy 死代码 1 处（已据实保留键） |
| 实机 | **未执行**（游戏未运行） |

### 补：第四轮实机验收点

9. 「车间」页**不再有「离班停工」行**；页尾**不再有「三餐与作息」分节**（整页只剩 1 个分节）；
10. 行为保持：离班仍会自动取消班组工作类型档位（该行为现在**无开关可关**）。
5. 未跑 `gate-runner.ps1`：本仓工作区含约两个月累积未提交改动（`git diff --stat` = 107 文件 / 14,663 insertions），必须显式提供 `-ExcludeFromFile` 范围外清单才可能 PASS；该项留待发版终检。
