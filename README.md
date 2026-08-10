# Personal Chronicle · 殖民地档案馆

> 给你的殖民地写一本「纪传体史书」——人物、物品、战役、地点，全部自动归档，随时回溯。
> A living chronicle for your colony — pawns, things, battles and places, auto-archived and always at your fingertips.

[![Version](https://img.shields.io/badge/version-4.7.0-blue)](https://github.com/w774431242/PersonalChronicle)
[![RimWorld](https://img.shields.io/badge/RimWorld-1.6-9B1D20)](https://rimworldgame.com/)
[![Requires](https://img.shields.io/badge/Requires-Harmony-orange)](https://github.com/pardeike/Harmony)
[![Languages](https://img.shields.io/badge/Languages-中文%20%2F%20English-informational)](https://github.com/w774431242/PersonalChronicle)
[![Status](https://img.shields.io/badge/status-公开%20Beta-yellow)](https://github.com/w774431242/PersonalChronicle)

---

## 这是什么 · What is this

**Personal Chronicle** 是一个 RimWorld 1.6 殖民地档案馆模组。它会默默记录你的殖民地里发生的一切，并把它们整理成一本可以按「人物 / 物品 / 战役 / 地点」检索的档案库。

不需要你手动记笔记，也不需要第三方工具——只要启用模组，游戏里的每一个「加入、死亡、制造、建造、战斗」都会被自动捕捉，并在主界面底部的 **「档案馆」** 按钮里呈现。

*Personal Chronicle is a colony-archive mod for RimWorld 1.6. It quietly records everything that happens in your colony and organizes it into a searchable archive of pawns, things, battles and places — openable from the "Archive" button on the main tab bar.*

---

## 功能亮点 · Features

| 模块 | 你能用它做什么 |
|---|---|
| **总览 Overview** | 一眼看到各分类的统计卡片，以及全殖民地最新的 5 条事件流（日期 + 事件名 + 主角），每 120 tick 智能刷新，不卡顿。 |
| **分类浏览 Categories** | 按 **人物 / 物品 / 战役 / 地点** 四大类分别浏览；可下钻的对象（事件/记录）点一下就能看详情。 |
| **详情档案 Detail** | 对象的完整信息 + **事件时间轴** + **关联档案网络**。点时间轴上的某一行，即可展开该事件的描述。 |
| **档案网络 Network** | 在事件的主/从对象之间跳转，例如「制造者 ↔ 物品」「死者 ↔ 凶器」，像查族谱一样顺藤摸瓜。 |
| **活读统计 Live Stats** | 首页区分「当前殖民者（游戏实时事实）」与「档案记录（历史快照）」双轨，避免把冷冻舱里的人算进人口。 |
| **阵营图鉴 Faction Codex** | 记录涉及阵营的敌友关系与关键事件，主权/中立/敌对一目了然（v4.3）。 |
| **检视页签 Inspect Tab** | 在 pawn 检视面板直接打开其专属档案，无需回主窗口（v4.6）。 |
| **健康残值 Health Valuation** | 肢体 / 精神 / 未衰老三维评分，量化「这具身体还剩多少价值」（v4.6.1）。 |
| **传承 Legacy** | 所有权变更链：谁造的、谁抢的、谁继承的，资产流转全程可追溯（v4.7）。 |

### 每个人 / 每件物，都有专属档案

- **人物档案**：概览 · 编年史 · 生涯（累计工时，而非工作优先级）· 战斗履历（击杀边 + 参战名册）· 社会关系（恋爱 / 婚姻 / 亲缘的结成与结束）· 足迹（地点进出履历）· 健康残值。
- **物品档案**：概览 · 编年史 · 战斗履历 · 流转记录 · 传承链。
- **战役档案**：参战双方、关键节点与战果一览。
- **地点档案**：被访问、被争夺的每一个角落。
- **强度五档**：核动力驴 / 超级牛马 / 常规牛马 / 劣质牛马 / 社会懒汉（阈值由 Def 驱动，UI 零硬编码）。

### 已支持的事件类型 · Supported events

| 事件 | 说明 |
|---|---|
| 加入殖民地 Joined | 新成员入伙的那一刻起被记录 |
| 死亡 Died | 记录死因与现场关联（如凶器） |
| 制造 Crafted | 谁造了什么、用的什么材料 |
| 建造 Built | 建筑落成进入编年史 |
| 战役 Battle | 战斗过程与参战名册 |

*所有事件文案均为数据驱动（`ChronicleEventDef` + 翻译键），缺失时优雅降级为原始类型名，绝不崩溃。*

---

## 界面一览 · Preview

启用模组并进入游戏后，主界面底部标签栏会出现 **「档案馆」** 按钮，点击即可打开四层档案浏览器。

想先看看长什么样？可以直接在浏览器打开这份可交互预览（无需启动游戏）：

> **[在浏览器中预览 UI →](docs/ui-preview/完整档案馆UI预览.html)**

---

## 安装方法 · Install

### 方式一：手动安装（推荐，适用于本仓库）

1. 将整个 `PersonalChronicle` 文件夹复制到游戏的 `Mods` 目录：
   - Windows：`.../RimWorld/Mods/`
   - macOS：`~/Library/Application Support/RimWorld/Mods/`
   - Linux：`~/.config/RimWorld/Mods/`
2. 启动 RimWorld，在 **Mod 列表** 中启用 **Personal Chronicle - Archive**。
3. 确保 **Harmony** 已启用，且在本模组之前加载（已在 `About.xml` 中声明 `loadAfter`，通常自动处理）。
4. 进入游戏，主界面底部出现「档案馆」按钮即代表成功。

### 方式二：Steam 创意工坊

> 本仓库即为完整的模组包，可直接放入 `Mods` 目录使用。创意工坊订阅链接将在发布后补充。

*This repository is a complete, ready-to-use mod package. A Steam Workshop subscription link will be added when published.*

---

## 兼容性与依赖 · Requirements

- **RimWorld 版本**：1.6（已在 `supportedVersions` 中声明）。
- **必需依赖**：[Harmony](https://github.com/pardeike/Harmony)（唯一外部依赖，自动随 Mod 列表加载）。
- **语言**：内置 **简体中文** 与 **English** 翻译；跟随游戏语言自动切换。
- **平台**：Windows / macOS / Linux 均可（模组本身跨平台）。

---

## 常见问题 · FAQ

**Q：想要一份「完整无缺」的生涯 / 战斗 / 社交时间线，该新开档还是中途加装？**
A：**想全量补全，请新开档。** 本模组是「实时事件采集」——Harmony 补丁在事件发生的那一刻才记录，RimWorld 本身不保存历史事件流。因此**启用模组之前**已经发生的加入 / 死亡 / 制造 / 战斗 / 社交，**任何存档都补不回来**，既不是 0 也不是假数据，就是没有。新开档从殖民地创立第一天起就被完整记录，时间线 100% 无缺口。

**Q：那中途加装（ongoing save）还有意义吗？**
A：有意义，但不会「全量」。模组读档时会自动把**当前在场**的自由殖民者 / 奴隶 / 囚犯全部建档（老成员不会变成「新人」，人物列表不空），启用后发生的新事件也照常累积。缺的只是「加装前那段历史」，且永久补不回。若你接受这个缺口，中途加装完全可用；若你要的是完美档案，请新开档。

**Q：安装模组前发生的历史会补录吗？**
A：不会。记录从你启用模组并继续 / 新开存档的那一刻起累积；之前的往事不追溯（见上一条）。

**Q：我现有的存档能用吗？**
A：可以。早期存档会自动迁移到最新的对象模型（无损升级），当前存档 Schema 版本为 3；中途加装老档也不会损坏既有进度，只是如上所述缺少加装前的历史。

**Q：中途加入存档（ongoing save）时，已经存在的殖民者会被认出来吗？**
A：会。模组加载时会自动把当前殖民地的全部人口建档，老成员不会被当成「新人」。但他们的「加入事件 / 历史时间线」是空的——因为那些发生在加装之前，无法回溯。

**Q：会拖慢游戏吗？**
A：不会明显变慢。事件流采用 120-tick 节流刷新，后台仅做轻量的补漏建档（reconcile）。

**Q：UI 里看到的中文 / 英文文本是写死的吗？**
A：不是。所有玩家可见文本都走翻译键（`PersonalChronicle.UI.*` / `PersonalChronicle.Event.*`），跟随游戏语言切换，也方便社区贡献翻译。

---

## 开发者 · Developers

<details>
<summary>构建与项目结构（点击展开）</summary>

### 构建 Build

```bash
dotnet build Source/PersonalChronicle.csproj -c Release
```

- 源码：`Source/`（net48 / C# 9，无第三方 NuGet 业务依赖，仅 Harmony + 游戏程序集）。
- 产物输出到 `Assemblies/PersonalChronicle.dll`。
- 本地路径：`PersonalChronicle.csproj` 中 `RimWorldPath` / `HarmonyPath` 可按机器覆盖。

### 目录结构 Layout

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
├── Source/                # 源码（net48 / C# 9）
├── Textures/UI/           # 图标
├── docs/                  # 设计与落地文档
└── README.md
```

分层依赖（单向）：**UI → Application → Domain ← Data / Capture**。

### 已知技术边界 · Known boundaries

| 项 | 说明 |
|---|---|
| LE-1 god class 拆分 | `ArchiveMainTabWindow.cs` 较长，建议 partial / 显示模型下沉 |
| LeftTick 离开字段 | 放逐 / 卖奴等离开路径 defer |
| 新采集 Harmony | 出生等热点路径暂靠 reconcile 兜底，不新增 Patch |

</details>

---

*Personal Chronicle - Archive · v4.7.0 · 为 RimWorld 1.6 打造*
