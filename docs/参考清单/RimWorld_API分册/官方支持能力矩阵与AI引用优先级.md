# 官方支持能力矩阵与 AI 引用优先级

> 来源：原总库 §90、§105、§111、§112。

## 90. 面向 Modder 的官方支持面

> “官方支持”应理解为：**游戏本体明确提供、允许 Mod 使用、或者官方资料明确针对 Modder 进行说明的扩展能力**；它不等于“稳定 ABI/API”。

### 90.1 官方支持能力总矩阵

| 能力 | 1.6 | 官方/本体支持 | 是否推荐 | 典型入口 |
|---|---:|---|---|---|
| Mod 文件夹加载 | ✓ | 本体 | ✓ | `Mods/` |
| `About.xml` | ✓ | 本体 | ✓ | `About/About.xml` |
| `Preview.png` | ✓ | 本体/Workshop | ✓ | `About/Preview.png` |
| `ModIcon.png` | ✓ | 本体 | ✓ | `About/ModIcon.png` |
| `Assemblies/` | ✓ | 本体 | ✓ | C# DLL |
| `Defs/` | ✓ | 本体 | ✓ | XML Def |
| `Patches/` | ✓ | 本体 | ✓ | `PatchOperation*` |
| `Languages/` | ✓ | 本体 | ✓ | `DefInjected` / `Keyed` / `Strings` |
| `Textures/` | ✓ | 本体 | ✓ | Unity Texture |
| `Sounds/` | ✓ | 本体 | ✓ | 音频资源 |
| `Materials/` | ✓ | 本体 | ✓ | 材质资源 |
| `AssetBundles/` | ✓ | 本体 | ✓ | Unity AssetBundle |
| `LoadFolders.xml` | ✓ | 本体 | ✓ | 版本/DLC/Mod 条件加载 |
| `IfModActive` | ✓ | 本体 | ✓ | 条件目录加载 |
| `IfModNotActive` | ✓ | 本体 | ✓ | 条件目录加载 |
| `IfModActiveAll` | ✓ | 本体，1.6 增强 | ✓ | 多条件 AND |
| 版本化目录 | ✓ | 本体 | ✓ | `1.6/`、`Common/` 等 |
| Mod 依赖声明 | ✓ | 本体 | ✓ | `modDependencies` |
| 替代 packageId | ✓ | 本体，1.6 新增 | ✓ | `alternativePackageIds` |
| Load Before/After | ✓ | 本体 | ✓ | `loadBefore` / `loadAfter` |
| ModSettings | ✓ | 本体 | ✓ | `ModSettings` / `Mod.GetSettings<T>()` |
| DefModExtension | ✓ | 本体架构 | ✓ | 自定义 Def 数据 |
| ThingComp | ✓ | 本体架构 | ✓ | Thing 级行为 |
| HediffComp | ✓ | 本体架构 | ✓ | Hediff 级行为 |
| AbilityComp 等 Comp | ✓ | 本体架构 | ✓ | Def/Thing/Pawn 子系统 |
| MapComponent | ✓ | 本体架构 | ✓ | Map 生命周期 |
| WorldComponent | ✓ | 本体架构 | ✓ | World 生命周期 |
| GameComponent | ✓ | 本体架构 | ✓ | Game 生命周期 |
| WorldObjectComp | ✓ | 本体架构 | ✓ | WorldObject 生命周期 |
| Custom Def Class | ✓ | XML → C# | ✓ | `workerClass` / `thingClass` 等 |
| Custom C# | ✓ | 本体程序集加载 | ✓ | `Assemblies/*.dll` |
| Harmony Patch | ✓ | 第三方运行时库 | ✓ | Prefix/Postfix/Transpiler |
| Reflection | ✓ | .NET/Mono | △ | 私有成员访问 |
| ExposeData | ✓ | 本体存档框架 | ✓ | `Scribe_*` |
| `Log.Error` / Config Errors | ✓ | 本体日志系统 | ✓ | 开发期验证 |
| DebugAction | ✓ | 本体开发模式 | ✓ | `[DebugAction]` |
| Development Mode | ✓ | 本体 | ✓ | Debug Toolbar |
| TweakValue | ✓ | 本体开发工具 | ✓ | 快速调整参数 |
| Steam Workshop 上传 | ✓ | 本体 + Steam | ✓ | Dev Mode / Workshop |
| Rider/VS 调试 | ✓ | Unity/运行时 + 社区工具 | ✓ | Debugger |
| Asset Bundle 平台区分 | ✓ | 本体，1.6 强化 | ✓ | Bundle 内容加载 |

来源：
- RimWorld Wiki：Mod Folder Structure / About.xml / ModSettings / GameComponent / DebugActions / Testing Mods / Asset Bundles
- Ludeon：1.6 + Odyssey 官方发布说明

## 105. 官方支持与第三方支持必须严格区分

### 105.1 官方/本体层

```text
RimWorld Mod Loader
├─ About.xml
├─ LoadFolders.xml
├─ Def Loader
├─ Patch Loader
├─ Language Loader
├─ Assembly Loader
├─ Asset Loader
├─ ModSettings
├─ Scribe
├─ Comp / Component
├─ Development Mode
└─ Steam Workshop integration
```

### 105.2 第三方层

```text
Harmony
HugsLib
Vanilla Expanded Framework
XML Extensions
Community Frameworks
其他 Mod API
```

其中 Harmony 是目前社区修改运行时代码的通用工具，但**不能描述为 Ludeon 自己开发的官方 Mod API**。RimWorld Wiki 将 Harmony 作为修改运行时方法的最佳实践之一，同时建议直接使用 Harmony，而不是仅为了 Harmony 引入 HugsLib。

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/Harmony

## 111. 面向 AI Mod 开发的“官方支持面”引用优先级

为避免 AI 编码时把第三方方案误认为原版能力，建议固定使用以下标签：

| 标签 | 含义 |
|---|---|
| `VANILLA_SUPPORTED` | RimWorld 本体直接提供 |
| `OFFICIAL_DOC` | Ludeon 官方文档/公告说明 |
| `OFFICIAL_FORUM` | Ludeon 官方论坛资料 |
| `GAME_ASSEMBLY` | 目标 Build 实际程序集能力 |
| `COMMUNITY_DOC` | Wiki/社区维护资料 |
| `DECOMPILED` | 反编译/源码镜像所得 |
| `THIRD_PARTY_API` | Harmony/HugsLib/VFE 等第三方框架 |
| `UNSTABLE_INTERNAL` | 内部实现，不应视为稳定接口 |

### AI 生成代码时必须先判断

```text
我要的功能
↓
原版 XML 能否解决？
↓ 否
原版 DefModExtension / Comp / Component？
↓ 否
原版公开/可引用 C# 类？
↓ 否
Harmony？
↓ 必要时
Reflection / 私有字段？
↓ 最后手段
```

这样可以尽量保证兼容性与可维护性。

## 112. 本库新增“官方支持”覆盖清单

本次整合后，本总库除原有 API/变量/Def/程序集体系外，已经覆盖：

- [x] 官方 1.6 Modder Primer
- [x] 官方 1.6 Change Log
- [x] Mod 文件系统
- [x] About.xml
- [x] Mod dependencies
- [x] alternativePackageIds
- [x] loadBefore / loadAfter
- [x] supportedVersions
- [x] LoadFolders.xml
- [x] IfModActive
- [x] IfModNotActive
- [x] IfModActiveAll
- [x] 版本化目录
- [x] Common 目录
- [x] Defs
- [x] PatchOperations
- [x] DefModExtension
- [x] Comp / Component
- [x] Game/World/Map Component
- [x] WorldObjectComp
- [x] XML ↔ C# class binding
- [x] Languages
- [x] DefInjected
- [x] Keyed
- [x] Strings
- [x] ModSettings
- [x] ExposeData / Scribe
- [x] Development Mode
- [x] DebugAction
- [x] TweakValues
- [x] Error/Config validation
- [x] Texture / Sound / Materials
- [x] AssetBundles
- [x] Workshop distribution
- [x] Debugger / Rider / VS Code / Visual Studio 工具链
- [x] 官方论坛 / 官方开发 Discord
- [x] 官方源码研究与反编译边界
- [x] AI 开发引用优先级
