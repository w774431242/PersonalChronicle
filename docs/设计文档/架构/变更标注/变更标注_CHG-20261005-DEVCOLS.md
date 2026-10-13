---
change_id: CHG-20261005-DEVCOLS
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle, Languages]
files:
  - path: Source/PersonalChronicle/Automation/Dialog_WorkshopManage.cs
    type: M
    symbols: Dialog_WorkshopManage.InitialSize, Dialog_WorkshopManage.MaxW, Dialog_WorkshopManage.MaxH
    lines: 22-27, 36-58
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.cs
    type: M
    symbols: MapComponent_WorkshopManager.deviceAliases, MapComponent_WorkshopManager.aliasNextIndex, MapComponent_WorkshopManager.AliasOf, MapComponent_WorkshopManager.EnsureDeviceAliases, MapComponent_WorkshopManager.AliasFor, MapComponent_WorkshopManager.DisplayNameOf, MapComponent_WorkshopManager.ExposeData, MapComponent_WorkshopManager.AccumulateDeviceRuntime
    lines: 214-224, 322-458
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.Schedule.cs
    type: M
    symbols: MapComponent_WorkshopManager.NotifyProductMade
    lines: 991-1015
  - path: Source/PersonalChronicle/Automation/WorkshopDeviceLedger.cs
    type: M
    symbols: DeviceStatEntry.firstSeenTick, DeviceStatEntry.producedValue, DeviceStatEntry.MarkSeen, DeviceStatEntry.AddProducedValue, DeviceStatEntry.ExposeData, WorkshopDeviceLedger.Touch, WorkshopDeviceLedger.FirstSeenTickOf, WorkshopDeviceLedger.ProducedValueOf, WorkshopDeviceLedger.AddProducedValue
    lines: 78-130, 300-360, 540-550
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView.DeviceCompactWidth, WorkshopManageView.DeviceColSharesFull, WorkshopManageView.DeviceColSharesCompact, WorkshopManageView.DeviceHeaders, WorkshopManageView.BuildDeviceRowTextCache, WorkshopManageView.ValueText, WorkshopManageView.BornText, WorkshopManageView.EfficiencyText, WorkshopManageView.DrawDevices, WorkshopManageView.DrawDeviceRow
    lines: 108, 152-200, 300-320, 520-720
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Overview.cs
    type: M
    symbols: WorkshopManageView.DrawEfficiencySection, WorkshopManageView.DrawPeakSection
    lines: 840-960
  - path: Source/PersonalChronicle/Automation/WorkshopBonusAccessBinder.cs
    type: M
    symbols: WorkshopBonusAccessBinder.ProductMadeOf
    lines: 25-45
  - path: Source/PersonalChronicle/Application/Effects/WorkshopBonusAccess.cs
    type: M
    symbols: WorkshopBonusAccess.SetProductMadeSink, WorkshopBonusAccess.NotifyProductMade
    lines: 134-170
  - path: Source/PersonalChronicle/Capture/Patch_GenRecipe.cs
    type: M
    symbols: Patch_GenRecipe (PostProcessProduct postfix call site)
    lines: 127-131
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: ColWorker, ColValue, ColBorn, ColEfficiency, ColBornDay
    lines: 525-530
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: (same 5 keys)
    lines: 516-521
---

# 变更标注 · CHG-20261005-DEVCOLS

## 背景（玩家 2026-10-05 实机截图，两项诉求 + 一项自查缺陷）

1. **概览页出现滚动** ⇒ 建议「车间管理界面长宽等比扩大范围」；
2. **设备过多难区分** ⇒ 加「建立时间」列、按时间自动给设备名加 Ab 格式别名、
   加「操作员」列（插在设备名称右侧）、加「累计产值」列（插在累计运行右侧），并允许建议列；
3. ⚠ **自查发现的自身缺陷**：上一批「大组件区」里「稼动 vs 产出」的图例与脚注**同行重叠**
   —— 玩家截图里表现为「右四运动·左四新」的乱码（半宽面板下 190px 脚注与左侧图例矩形相交）。

## 交付一：窗口按屏幕自适应放大（消除滚动）

`Dialog_WorkshopManage`：`InitialSize` 由固定 `1080×760` 改为
`Mathf.Clamp(UI.screenWidth - 80, 1080, 1600) × Mathf.Clamp(UI.screenHeight - 120, 760, 1040)`
（UI 空间，已含 UIScale）。

⚠ **只放大、不放开手动缩放**：`resizeable` 保持 `false` —— 2026-10-02 审计 P1-H 已判定
「缩窄会让卡片网格 / 成员行 / chip 的固定宽块进入**负宽区**（三列重叠）」，
放大方向不存在该风险。取证：`Verse.Window.resizeable` 为 public 字段、
`Verse.UI.screenWidth/screenHeight` 为 public static int（Cecil 实测）。

效果：概览内容高约 608 + 头部/底条约 100 ⇒ 在 ≥1080 高窗口内**一屏容纳**，滚动条不再出现。

## 交付二：设备页新增列 + 设备别名

新列序（10 列）：**名称（别名·设备名·品质）| 操作员 | 状态 | 当前制作 | 产出(今日/本周) | 累计运行 |
累计产值 | 建立时间 | 效率(件/机时) | 位置**

- **操作员**：与概览页/调色板**同源**（`WorkshopDeviceReader.CollectActiveWorkers`，唯一实现），
  Refresh 期一次读、行内只查快照；
- **累计产值**：`product.MarketValue` 逐件累加（见下「契约变更」）；
- **建立时间**：`DeviceStatEntry.firstSeenTick`（台账首次看到该设备的 tick）⇒ 显示「第 N 天」；
- **效率**（本轮**建议列**）：累计产出 ÷ 累计运行小时（件/机时）—— 一眼看出"同样在跑，谁产得少"；
- **别名**：同名设备（13 台「炉灶（电力）」）的可区分标识。

### ⚠ 为什么别名不是"改设备真实名字"（已取证）

- `Verse.Building` **无任何 Rename 成员**；
- `Thing.GetCustomLabelNoCount(includeHp)` 是 **virtual**，不可从外部写入。

⇒ 「运行时改建筑显示名」在原版 API 上**不存在**。硬改（反射写私有字段 / Harmony 覆盖显示名）
会污染存档并影响其它 Mod 的读名路径。故采用**本系统内别名**：只在 PC 车间界面显示，
**不影响原版与其它 Mod 看到的名字**。分配规则：按建立时间排序依次 A、B、C…Z，超过 26 用 A2/B2；
**一经落盘即固定**（设备拆除不回收字母、不重排）。

### 列宽收口（顺带修的既有隐患）

表头（`DrawDevices`）与行（`DrawDeviceRow`）原先**各写一份 share 常量** —— 正是历史
「表头与行错位 / 活动列负宽」事故的根源。本批收口为**共用同一份数组**
（`DeviceColSharesFull` 10 列 / `DeviceColSharesCompact` 8 列），并加**窄档降级**：
`rect.width < 900` 时隐藏「建立时间 / 效率」（skill 铁律 4：先算预算，不够就降级，不画崩）。

## 交付三：修复上一批的重叠缺陷

- `WorkshopManageView.Overview.cs` · `DrawEfficiencySection`：脚注由「与图例同行」改为
  **标题行右侧**（两段文字各占独立 rect，不再相交）；
- 同文件 · `DrawPeakSection`：色阶图例由「两个标签都堆在色块上方」改为
  **标签在两端、色块居中渐变（上=多/深、下=少/浅）**。

## 契约变更（跨层通道不变，仅扩参数）

`WorkshopBonusAccess.NotifyProductMade(Pawn)` → `(Pawn, Thing)`，
`SetProductMadeSink(Action<Pawn>)` → `(Action<Pawn, Thing>)`。

**为什么必须由 Postfix 传产物**：`worker.CurJob` 在 Postfix 时刻可能已被清除/切换，
反推会得到空或错的产物；而 `Patch_GenRecipe` 手上本来就有 `product` ⇒ 传下来是唯一准确、
零额外扫描的做法。跨层方向未变（Capture → Application 契约 → Automation 实现），
`WorkshopBonusAccessBinder` 的注入处用方法组，签名自动适配。

## 存档影响：**无迁移**（全部为加性可选字段）

| 字段 | 位置 | 旧档行为 |
|---|---|---|
| `runWindowCounts` / `runWindowDays` | `DeviceStatEntry` | **补登**（见下"自身遗漏"）⇒ 缺省空，`EnsureWindow` 补齐 |
| `firstSeenTick` | `DeviceStatEntry` | = 0 ⇒ 升级后首轮扫描**补记"现在"**（之后固定），或列显示「—」 |
| `producedValue` | `DeviceStatEntry` | = 0 ⇒ 从新版起重新累计（历史产出市场价不可追溯，**不回填猜测值**） |
| `deviceAliases` / `aliasNextIndex` | `MapComponent_WorkshopManager` | 缺省空 / 0 ⇒ 刷新期从 A 开始补发 |

⚠ **自身遗漏（照实登记）**：`runWindowCounts` / `runWindowDays` 首次实现（CHG-20261005-TRENDBOARD）
时**漏了 ExposeData 落盘**（写入正确、读档会丢）⇒ 本批已补登。上一批范围外清单可复核。

## 实机验证要点

1. **窗口**：重开车间管理 ⇒ 窗口应占屏幕大部分（1080p 下约 1600×960），概览页**无需滚动**即见三件图表；
2. **设备页 10 列**：名称列出现「A · 炉灶（电力）」「B · 炉灶（电力）」…；操作员列在班时应显示人名；
3. **累计产值 / 建立时间 / 效率**：新产出后产值增长；建立时间显示「第 N 天」（旧设备升级后首轮刷新补记）；
4. **窄档**：用检视 Tab（较窄）打开 ⇒ 自动隐藏「建立时间 / 效率」两列，**不应出现列错位或文字互压**；
5. **重叠修复**：「稼动 vs 产出」标题行右侧显示脚注，图例独居一行，**不再出现叠字**。

## 回滚

三块互相独立：窗口改回常量 `new Vector2(1080f, 760f)`；设备页把 `DeviceHeaders`/share 数组切回
6 列版本；数据字段留档无害（不 PRUNE）。契约扩展如需回退，把 `Action<Pawn, Thing>` 改回
`Action<Pawn>` 并同步 3 个调用点即可。

## 残留风险（照实登记）

1. **别名为 PC 侧标识**：原版与其它 Mod 看到的仍是原设备名（这是刻意选择，见上）；
   若将来原版提供建筑改名 API，可平滑切到真名而不改数据结构。
2. **建立时间对旧档是"升级时刻"**：旧设备没有真实建造时间，补记值 = 升级后首轮扫描时间；
   新设备才是准确值（列本身不区分这两种来源）。
3. **效率列口径 = 累计产出 ÷ 累计运行**：设备"待机但有产出"（自动化设备）或"运行但无产出"
   （缺料空转）都会让该值偏离直觉 —— 它是**相对比较**用的，不是绝对产能指标。
4. 累计产值对**升级前**的产出不计入（从 0 起），设备页横幅的"本周产出"仍是件数口径（未改）。
