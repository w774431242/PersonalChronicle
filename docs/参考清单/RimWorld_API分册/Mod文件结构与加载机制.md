# Mod 文件结构与加载机制

> 来源：原总库 §4、§91、§92、§106、§107。

## 4. RimWorld Mod 目录与运行时加载接口

标准结构：

```text
MyMod/
├─ About/
│  ├─ About.xml
│  ├─ Preview.png
│  ├─ ModIcon.png
│  └─ PublishedFileId.txt
├─ Assemblies/
│  └─ MyMod.dll
├─ Defs/
│  ├─ ThingDefs/
│  ├─ RecipeDefs/
│  ├─ HediffDefs/
│  └─ ...
├─ Patches/
│  └─ Patches.xml
├─ Languages/
│  ├─ English/
│  └─ ChineseSimplified/
├─ Textures/
├─ Sounds/
├─ LoadFolders.xml
└─ About/
```

### 4.1 About.xml 核心字段

典型字段：

```xml
<ModMetaData>
    <name>My Mod</name>
    <author>Author</author>
    <packageId>Author.MyMod</packageId>
    <supportedVersions>
        <li>1.6</li>
    </supportedVersions>
    <description>...</description>
    <loadAfter>
        <li>Some.Other.Mod</li>
    </loadAfter>
</ModMetaData>
```

### 常用关系字段

| 字段 | 作用 |
|---|---|
| `name` | Mod 显示名 |
| `author` | 作者 |
| `packageId` | Mod 唯一包标识 |
| `supportedVersions` | 支持版本 |
| `description` | 描述 |
| `modDependencies` | 强依赖 |
| `loadAfter` | 载入顺序约束 |
| `loadBefore` | 载入顺序约束 |
| `incompatibleWith` | 冲突声明 |
| `packageId` | Mod 间引用核心身份 |

目录结构参考：
<https://rimworldwiki.com/wiki/Modding_Tutorials/Folder_structure>

## 91. Mod 文件系统：官方支持的全部主要内容入口

### 91.1 标准目录

```text
MyMod/
├─ About/
│  ├─ About.xml
│  ├─ Preview.png
│  ├─ ModIcon.png
│  └─ PublishedFileId.txt
│
├─ Assemblies/
├─ Defs/
├─ Patches/
├─ Languages/
│  └─ English/
│     ├─ DefInjected/
│     ├─ Keyed/
│     └─ Strings/
├─ Textures/
├─ Sounds/
├─ Materials/
├─ AssetBundles/
├─ LoadFolders.xml
└─ 1.6/                 # 可选版本化目录
   ├─ Assemblies/
   ├─ Defs/
   ├─ Patches/
   ├─ Languages/
   ├─ Textures/
   └─ ...
```

RimWorld 对文件夹和若干特殊路径具有固定大小写要求。跨平台 Mod 必须把大小写当作实际兼容性约束，而不能依赖 Windows 的大小写不敏感文件系统。

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/Mod_Folder_Structure

### 91.2 About.xml：官方 Mod 元数据与依赖接口

1.6 开发中最重要的字段类别：

- `packageId`
- `name`
- `author` / `authors`
- `description`
- `supportedVersions`
- `url`
- `modDependencies`
- `modDependenciesByVersion`
- `alternativePackageIds`
- `loadBefore`
- `loadAfter`
- 对应的版本化加载/依赖字段

### 1.6 特别值得纳入标准

```xml
<modDependencies>
  <li>
    <packageId>some.mod</packageId>
    <displayName>Some Mod</displayName>
    <alternativePackageIds IgnoreIfNoMatchingField="True">
      <li>some.mod.dev</li>
    </alternativePackageIds>
  </li>
</modDependencies>
```

`alternativePackageIds` 是 1.6 新增的重要依赖兼容能力，可用于稳定版/开发版 packageId 不同的依赖兼容。

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/About.xml

## 92. LoadFolders.xml：官方支持的版本/DLC/Mod 条件加载系统

这是当前 Mod 工程最容易被低估、但对大型架构极其重要的官方加载机制。

### 92.1 基本结构

```xml
<loadFolders>
  <v1.6>
    <li>/</li>
    <li>1.6</li>
  </v1.6>
</loadFolders>
```

### 92.2 DLC/Mod 条件

```xml
<loadFolders>
  <v1.6>
    <li IfModActive="Ludeon.RimWorld.Odyssey">Odyssey</li>
    <li IfModNotActive="Ludeon.RimWorld.Odyssey">NoOdyssey</li>
  </v1.6>
</loadFolders>
```

### 92.3 1.6 的重要能力：All 条件

`IfModActiveAll` 用于多个 Mod/DLC 同时满足时才加载目标目录，相比 `IfModActive` 的 OR 语义可以建立更清晰的功能矩阵。

### 推荐工程模式

```text
Common/
Odyssey/
VanillaExpanded/
CombatExtended/
Compatibility/
```

通过 `LoadFolders.xml` 把兼容代码从核心实现中隔离，能够显著降低条件分支与程序集耦合。

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/Mod_Folder_Structure

## 106. 官方支持的 Mod 分发能力

RimWorld 原生支持 Steam Workshop 发布流程。

典型流程：

```text
本地 Mod
  ↓
About.xml
  ↓
Preview.png
  ↓
Development Mode
  ↓
Mods 菜单
  ↓
Upload on Steam
  ↓
PublishedFileId.txt
```

`PublishedFileId.txt` 保存 Workshop 项目的唯一 ID，用于后续更新。

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/Distribution

## 107. 官方支持的跨版本架构能力

建议将 Mod 架构设计为：

```text
MyMod/
├─ About/
├─ Common/
│  ├─ Defs/
│  ├─ Languages/
│  └─ Textures/
├─ 1.6/
│  ├─ Assemblies/
│  ├─ Defs/
│  ├─ Patches/
│  └─ Languages/
├─ Odyssey/
│  ├─ Defs/
│  └─ Patches/
├─ Compatibility/
│  ├─ ModA/
│  └─ ModB/
└─ LoadFolders.xml
```

这样可以把：

- 游戏版本差异
- DLC 差异
- Mod 兼容层
- Core 基础层
- UI/本地化层
- C# 行为层

彻底隔离。

这与“统一、标准化、防过耦合、防硬编码”的工程目标高度一致。
