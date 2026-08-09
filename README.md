# Personal Chronicle - Archive (v0.3)

环世界 1.6 殖民地档案馆。RimWorld 1.6 colony archive mod.

## 功能 Features

殖民地档案馆：总览 / 分类 / 详情 / 档案网络四层浏览。
A colony archive with four navigation layers: Overview / Categories / Detail / Archive Network.

- **总览 Overview**：分类统计卡 + 最近全局事件流（跨全部分类的最新 5 条事件：日期 + 事件名 + 主对象名，120-tick 节流刷新）。
  Category stat cards + recent global event stream (latest 5 events across all categories: date + event name + primary object, throttled on a 120-tick cadence).
- **分类 Categories**：人物 / 物品 / 战役 / 地点 分类浏览，行渲染按分类行为（ArchiveDepthBehavior）分支——Event/Record 可下钻详情；StatOnly 显示「统计模式」标记、不跳转详情（为未来统计型分类预留）。
  Browse by Pawn / Thing / Battle / Location; row rendering branches on category behavior — Event/Record drill into detail; StatOnly shows a "Stats Only" tag without drill-down (reserved for future stats-only categories).
- **详情 Detail**：对象信息 + 事件时间轴 + 关联档案网络。时间轴行可点击选中展开事件描述（ChronicleEventDef.descriptionKey）。
  Object info + event timeline + related-archive network. Click an event row to expand its description (ChronicleEventDef.descriptionKey).
- **档案网络 Archive Network**：事件主/从对象之间的关联跳转（例如制造者 ↔ 物品、死亡者 ↔ 凶器）。
  Association hops between event Primary/Subject objects (e.g. crafter ↔ item, deceased ↔ weapon).
- **活读统计 Live stats**：首页区分「当前殖民者（游戏事实）」与「档案记录（快照）」双轨；GameComponent 定期 reconcile 补漏建档。
  Home dual-track: live FreeColonists vs archive snapshots; periodic GameComponent reconcile for missed joins.

### 已支持事件类型 Supported event types

- 加入殖民地 Joined the colony（`PersonalChronicleEventJoin`）
- 死亡 Died（`PersonalChronicleEventDeath`）
- 制造 Crafted（`PersonalChronicleEventCrafted`）
- 建造 Built（`PersonalChronicleEventBuilt`）
- 战役 Battle（`PersonalChronicleEventBattle`）

事件文案完全数据驱动：`ChronicleEventDef`（defName=存档身份、labelKey/descriptionKey=翻译入口）→ 翻译 key 渲染；缺失 Def 时显示原始 TypeKey。
Event copy is fully data-driven: `ChronicleEventDef` (defName = save identity, labelKey/descriptionKey = translation entry points) → translation keys; missing defs fall back to the raw TypeKey.

## 目录结构 Layout

```text
PersonalChronicle/
├── About/                 # Mod 元数据
├── Assemblies/            # 编译产物 PersonalChronicle.dll
├── Defs/                  # XML 定义（事件 / 分类 / 主按钮 / 战役扩展）
├── Languages/
│   ├── ChineseSimplified/Keyed/
│   └── English/
│       ├── Keyed/
│       └── DefInjected/   # ArchiveCategoryDef · MainButtonDef
├── Source/
│   ├── PersonalChronicle.csproj
│   └── PersonalChronicle/
│       ├── Application/   # IArchiveService · ArchiveService · 活读视图
│       ├── Archive/       # MainTabWindow UI
│       ├── Capture/       # Harmony patches（只读 + 降级）
│       ├── Data/          # GameComponent · 存档 / reconcile
│       ├── Domain/        # 对象模型 · Def · 扫描器
│       ├── ChronicleMod.cs
│       └── ChronicleSettings.cs
├── Textures/UI/
├── docs/                  # 设计与落地文档（见 docs/README.md）
└── README.md
```

分层依赖（单向）：**UI → Application → Domain ← Data / Capture**。

## 依赖 Dependency

- **Harmony**（`brrainz.harmony`）——唯一必需依赖。声明于 About.xml（modDependencies + loadAfter）。
  Harmony is the only required dependency (declared in About.xml).

## 安装 Install

1. 将整个 `PersonalChronicle` 文件夹放入游戏 Mods 目录（`<RimWorld>/Mods/`）。
   Copy the `PersonalChronicle` folder into `<RimWorld>/Mods/`.
2. 游戏内 Mod 列表启用本 mod（并确保 Harmony 已启用且在其之前/同批加载）。
   Enable the mod in-game (make sure Harmony is active and loads first).
3. 启动游戏，主界面底部出现「档案馆」按钮。
   Start the game; the "Archive" button appears on the main tab bar.

## 已知边界 Known Boundaries

- 记录自本 mod 安装并新开/继续存档起累积；安装前的历史不追溯。
  Records accumulate from when the mod is installed on a save; history before installation is not backfilled.
- 旧版（v0.1/v0.2）数据：v0.2 存档自动迁移到 v2.1 对象模型（PawnObject），可无损升级；v0.1 数据作废。
  v0.2 saves auto-migrate to the v2.1 object model (PawnObject) losslessly; v0.1 data is voided.
- 事件文案走数据驱动（ChronicleEventDef + 翻译 key）；缺失 Def 时显示原始 TypeKey，缺失 descriptionKey 时不显示描述。
  Event copy is data-driven (ChronicleEventDef + translation keys); missing defs fall back to the raw TypeKey, missing descriptionKeys render no description.
- UI 层零硬编码文案：所有用户可见文本经 `PersonalChronicle.UI.*` / `PersonalChronicle.Event.*` 翻译 key。
  Zero hardcoded user copy: all visible text flows through `PersonalChronicle.UI.*` / `PersonalChronicle.Event.*` translation keys.

## 开发 Build

```bash
dotnet build Source/PersonalChronicle.csproj -c Release
```

- 源码：`Source/`（net48 / C# 9，无第三方 NuGet 业务依赖，仅 Harmony + 游戏程序集）。
- 产物输出到 `Assemblies/PersonalChronicle.dll`。
- 本地路径：`PersonalChronicle.csproj` 中 `RimWorldPath` / `HarmonyPath` 可按机器覆盖。
- 设计与历史落地清单见 [`docs/README.md`](docs/README.md)。
- **UI 可交互预览**（浏览器打开）：[`docs/ui-preview/archive-ui-preview.html`](docs/ui-preview/archive-ui-preview.html)（v3.1 方案示例）。
- **详情 Tab（v3.1 已落地）**：人物 概览/编年/生涯/战斗履历/社会关系；武器 概览/编年/战斗履历/流转。
- **生涯**：累计工时采样（非工作优先级）；**战斗**：击杀边 + 参战名册；**社会关系**：恋爱/婚姻/亲缘结成与结束；**足迹**：地点进出履历。

## 遗留 / 可选后续

| 项 | 说明 |
|---|---|
| LE-1 god class 拆分 | `ArchiveMainTabWindow.cs` ≈2500 行，建议 partial / 显示模型下沉 |
| LeftTick 离开字段 | 放逐/卖奴等离开路径 defer |
| 新采集 Harmony | 出生等热点路径暂靠 reconcile 兜底，不新增 Patch |
