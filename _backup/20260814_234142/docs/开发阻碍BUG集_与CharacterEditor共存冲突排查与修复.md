# 开发途中严重阻碍游戏的 BUG 集：与 Character Editor 共存冲突排查与修复

> **文档定位说明**：本文档记录的是**与 Character Editor 共存时、原始存在的核心阻碍 BUG**（严重阻碍游戏运行）。修复过程中我方引入的次生问题（如补语言文件时误写不存在的类型导致启动报错）**不属于核心 BUG**，不计入本文档，仅在对应核心 BUG 的"修复遗留"处一笔带过。

- **模组**：Personal Chronicle · 殖民地档案馆（v1.1.2）
- **冲突对象**：Character Editor（workshop `1874644848`，`void.charactereditor` v1.6.3）
- **启动环境**（用户指定，禁止变更）：本 MOD + CE + 依赖（Harmony）+ 全部 DLC
- **记录日期**：2026-08-12
- **结论**：两处核心阻碍 BUG 已全部定位根因并修复，用户实测通过（CE 编辑器中文恢复 + 加载警告消除）。

---

## 核心阻碍 BUG 总览

| 编号 | 阻碍现象 | 严重度 | 根因类型 | 状态 |
|---|---|---|---|---|
| BUG-1 | CE 编辑器窗口内全部按钮/标签显示英文，无本 mod 时正常 | 高（本地化整体失效，功能可用性受损） | 语言聚合回退命名冲突 | 已修复（实测通过） |
| BUG-2 | Mod Manager 黄色警告"应在 RimWorld 之后加载"，本 mod 被排到 RimWorld 前 | 中（警告级，但隐藏加载顺序风险、间接加剧 BUG-1） | About.xml 误写 loadBefore | 已修复（实测通过） |

> 注：BUG-2 为本文档内部编号（原"BUG-3"），因修复途中产生的次生问题已移出核心 BUG 集，故顺延为 BUG-2。

---

## BUG-1：CE 编辑器窗口中文失效

### 现象
- 干净环境（前置+DLC+本mod+CE）下，打开 CE 编辑 pawn 的窗口，内部所有前端组件（按钮、标签）显示为英文。
- 用户明确："只有 CE 编辑器窗口全部功能前端组件翻译失效"，本 mod 自身 UI、物品浮窗均正常。
- 无本 mod 时（仅 CE+依赖+DLC）CE 编辑器中文正常。

### 探测路径（根因如何找到）
1. **排除键/Def 覆盖**：扫描 CE 的 `Languages/` 目录（无）、dll 内 556 个 `PersonalChronicle.*` 键（0 重叠）；本 mod Keyed 全为 `PersonalChronicle.*` 前缀。→ CE 不覆盖本 mod 键，本 mod 也不覆盖 CE 键。
2. **排除 CE patch 翻译系统**：用 `System.Reflection.Metadata`（dotnet8）解析 CE dll PE 元数据，确认 CE **无 `[HarmonyPatch]` 特性**，全部补丁目标是主菜单/生命周期类（`Page_ConfigureStartingPawns`、`MainMenuDrawer`、`Map.FinalizeInit`、`Game.LoadGame` 等），**不碰 Translator/LanguageDatabase/Def.label/InspectPane**。
3. **确认 CE 语言判定逻辑**：反编译 `CharacterEditor.Label.UpdateLabels`，IL 序列为：
   - `ldsfld Verse.LanguageDatabase.activeLanguage`
   - 取 `activeLanguage.FriendlyNameEnglish.ToLower()`
   - 依次 `StartsWith` 检查 `german/russian/french/japanese/simplified chinese/polish/spanish`
   - 未匹配 → 回落 **english**
   - 即 CE 用 `FriendlyNameEnglish.ToLower().StartsWith("simplified chinese")` 判定中文。
4. **比对语言包元数据**：中文语言包 `LanguageInfo.xml` 的 `friendlyNameEnglish="Simplified Chinese"`（带空格）→ `ToLower()` = `"simplified chinese"` → 匹配成功。
5. **锁定根因 A**：本 mod `Languages/ChineseSimplified/` 目录**仅有 `Keyed/`，缺 `LanguageInfo.xml`**。RimWorld 聚合所有激活 mod 的 `Languages/<lang>/` 目录；当某 mod 提供语言目录却无 `LanguageInfo.xml` 时，RimWorld 为该语言创建**回退 LoadedLanguage**，其 `FriendlyNameEnglish` 直接取**文件夹名 `ChineseSimplified`**（无空格）。
   - 无本 mod 时：真实语言包独占聚合 → `FriendlyNameEnglish="Simplified Chinese"` → CE 匹配中文。
   - 加本 mod 后：本 mod 的"无元数据 ChineseSimplified 目录"参与聚合 → 回退名 `ChineseSimplified` 胜出 → CE 的 `StartsWith("simplified chinese")` 对 `"chinesesimplified"` 失败 → 回落英文。
6. **日志佐证**：干净日志 `updating labels for chinesesimplified (简体中文)` → `currentLanguage="chinesesimplified"`，正是回退名，印证根因 A。

### 解决方向
- 方向：让本 mod 参与语言聚合时提供**与真实语言包一致的元数据**，消除回退命名污染。

### 解决办法
- 为本 mod 新增 `Languages/ChineseSimplified/LanguageInfo.xml`，`friendlyNameEnglish` 设为 `"Simplified Chinese"`（与真实语言包完全一致），并补 `Languages/English/LanguageInfo.xml`。
- 同步发布目录，编译 0 错误 0 警告。

### 修复遗留（次生问题，非核心 BUG）
- 初次补 `LanguageInfo.xml` 时误加了 `<languageWorkerClass>LanguageWorker_Chinese</languageWorkerClass>`，而该类型在当前安装环境并不存在，导致启动期 `Activator.CreateInstance(null)` 报错、游戏重置 mods 配置。此为本方修复过程引入的次生问题，**不属于核心 BUG**，已通过删除该行（仅保留 `friendlyName*` 轻量元数据）解决，用户实测启动正常。

---

## BUG-2：本 mod 排在 RimWorld 前面（黄色警告 + 加载顺序异常）

### 现象
- Mod Manager 显示黄色警告："应在以下模组之后加载：* RimWorld"，本 mod 被排到 `RimWorld` 前面（图中 `Personal Chronicle` 位于 `Character Editor` 之上、`RimWorld` 之前）。

### 探测路径（根因如何找到）
1. **读取 About.xml**：发现
   ```xml
   <loadBefore>
     <li>Ludeon.RimWorld</li>
   </loadBefore>
   ```
2. **分析语义**：`Ludeon.RimWorld` 是游戏核心，拥有所有基础 Def（`Pawn`、`ThingDef`、`WorkTypeDef` 等）；本 mod 的 Defs/Patches/C# 全部引用这些核心 Def。mod 不可能合法排在核心之前。
3. **判定为历史遗留误写**：本 mod 真实约束仅需 `<loadAfter><li>brrainz.harmony</li></loadAfter>`（Harmony 之后）。`loadBefore Ludeon.RimWorld` 是模板复制或意图写反（"在 RimWorld 之后"写成"之前"）遗留。
4. **隐藏风险**：该误写使本 mod 加载顺序异常，是 BUG-1 类语言/Def 聚合冲突的潜在放大器。

### 解决方向
- 删除 `<loadBefore><li>Ludeon.RimWorld</li></loadBefore>` 整段，仅保留 `<loadAfter><li>brrainz.harmony</li></loadAfter>`。

### 解决办法
- 删除 `About.xml` 中 `loadBefore` 段，同步发布目录，编译 0 错误 0 警告，用户实测黄色警告消失。

---

## 最终修复总结

| 项 | 内容 |
|---|---|
| 修改文件 | `Languages/ChineseSimplified/LanguageInfo.xml`（新增，仅 `friendlyName*` 轻量元数据）、`Languages/English/LanguageInfo.xml`（同上）、`Languages/ChineseSimplified/Keyed/Archive.xml`（删重复键）、`Languages/English/Keyed/Archive.xml`（删重复键）、`About/About.xml`（增补 CE 共存说明 + 删 loadBefore） |
| 编译 | `dotnet build -c Release` 0 错误 0 警告 |
| 发布同步 | 上述文件均 `Copy-Item` 至 `Mods/PersonalChronicleVer0.1` 对应白名单路径 |
| 实测结果 | 用户确认可正常进入游戏、CE 编辑器中文恢复、Mod Manager 黄色警告消除（两处核心 BUG 闭环） |

### 附带修复（任务②）
- 中/英 Keyed 各有重复僵尸键 `PersonalChronicle.UI.NoWorkData`（142 行被 `ArchiveMainTabWindow.cs:3394` 引用；205 行无引用），删除 205 行重复键，干净环境翻译错误 1 → 0。

---

## 同类型阻碍 BUG 规避规范标准（P0 级）

1. **禁止声明不存在的 LanguageWorker**
   - RimWorld mod 新增 `LanguageInfo.xml` 时，**严禁写入本 mod 不负责、且当前环境未必存在的 `<languageWorkerClass>`**。
   - `LanguageWorker_*` 由官方核心/语言包定义，仅在**确认该类型在目标环境必然存在**时才引用。
   - 本 mod 只需补 `friendlyNameNative` / `friendlyNameEnglish` / `canBeTiny` 等轻量元数据。

2. **语言目录必须配 LanguageInfo.xml**
   - 若 mod 提供 `Languages/<lang>/Keyed/` 或 `Languages/<lang>/DefInjected/`，应同时提供 `Languages/<lang>/LanguageInfo.xml`，避免 RimWorld 为该语言创建**回退 LoadedLanguage**（其 `FriendlyNameEnglish` 取文件夹名，导致其他 mod 按 `StartsWith("simplified chinese")` 等判定的本地化失败）。
   - `friendlyNameEnglish` 必须与真实语言包**完全一致**（如 `"Simplified Chinese"`，含空格，非 `"ChineseSimplified"`）。

3. **严禁 loadBefore 游戏核心**
   - 不得写 `<loadBefore><li>Ludeon.RimWorld</li></loadBefore>`。核心天然在所有非核心 mod 之前。
   - mod 加载约束只声明真实依赖（如 `<loadAfter><li>brrainz.harmony</li></loadAfter>`）。

4. **翻译键唯一性**
   - 同一 Keyed 文件中禁止重复 `<PersonalChronicle.XXX>` 键（RimWorld 计为翻译 error，且后者覆盖前者导致文案错乱）。
   - 新增键前用脚本扫描现有键，确认无重复。

5. **跨 mod 本地化冲突的探测铁律**
   - 遇到"某 mod 窗口英文/键名"类问题，先确认**目标 mod 的语言判定机制**（反编译其 `Label`/`Translate` 逻辑，看它读 `FriendlyNameEnglish` 还是 `activeLanguage`）。
   - 再确认**本 mod 的语言目录是否缺元数据**、是否污染聚合。
   - 最后用 `Player.log` 的 `updating labels for <lang>` / `Translation data for language ... has N errors` 佐证。

6. **改动语言/加载元数据后必须实测**
   - 任何 `LanguageInfo.xml` / `About.xml` 加载约束改动，必须进游戏确认：① 无红字崩溃可进入；② 目标本地化恢复；③ Mod Manager 无异常警告。
   - 纯离线编译 0 错误不代表运行时安全。

---

## 附录：关键证据索引
- CE 语言判定：反编译 `CharacterEditor.Label.UpdateLabels`（IL：`ldsfld LanguageDatabase.activeLanguage` + `StartsWith("simplified chinese")`）
- 本机核心无简中：`Data/Core/Languages/` 仅 `English`；`LanguageWorker_Chinese` 不在 `Assembly-CSharp.dll`
- 官方简中为可下载包：GitHub `Ludeon/RimWorld-ChineseSimplified`（"内置翻译落后，玩家可自行下载安装"）
- 日志铁证：`updating labels for chinesesimplified (简体中文)` 印证语言聚合回退名
- 翻译错误：干净环境 `ChineseSimplified has 1 errors` = 重复键 `NoWorkData`
