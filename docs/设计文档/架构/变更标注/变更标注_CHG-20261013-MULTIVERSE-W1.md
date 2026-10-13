---
change_id: CHG-20261013-MULTIVERSE-W1
version: v0.1
date: 2026-10-13
author: AI 助手（CodeBuddy · DeepSeek-V4.1-Flash）
scope: [工程根（LoadFolders.xml 新增、1.7/ 骨架占位）, 1.6/Assemblies（重影清理 + 本轮编译产出）, _scripts/（编译与部署链探测化）, Source/PersonalChronicle/BuildStamp.g.cs（编译自动重写的时间戳常量）]
scope-paths: [LoadFolders.xml, 1.7/, 1.6/Assemblies/, _scripts/compile_with_sdk.ps1, _scripts/deploy-to-game.ps1, _scripts/compile.rsp, Source/PersonalChronicle/BuildStamp.g.cs]
title: 多版本目录骨架（1.6 优先 / 1.7 占位）+ LoadFolders.xml + 程序集重影清理 + 编译部署链探测化 + 双 mod 部署
state: Implemented
layer: 打包与工具链层（Mod 目录布局 + 构建脚本 + 部署；**无 Def / Languages / About / Textures 内容改动**；`BuildStamp.g.cs` 为编译生成物）
files:
  - path: LoadFolders.xml
    type: A
    symbols: loadFolders/v1.6（li:/ + li:1.6）
    lines: 1-15
    note: 版本键恰好 2 段；不使用已废弃的 default 键；与「无该文件时引擎兜底顺序」同序 ⇒ 零行为变更
  - path: 1.6/Assemblies/250c559d-bb01-44b7-8293-9c942d879880_PersonalChronicle.dll
    type: D
    symbols: 重影程序集（PersonalChronicle, Version=0.1.0.0）
    note: 5 个同标识重影 dll 之一；曾受 git 跟踪，可由 git restore 还原
  - path: 1.6/Assemblies/527152be-b45a-4f01-80ec-20c70a6bc691_PersonalChronicle.dll
    type: D
    symbols: 重影程序集（同标识）
    note: 同上
  - path: 1.6/Assemblies/7656c543-8fc3-4024-8e52-f0301f8362f2_PersonalChronicle.dll
    type: D
    symbols: 重影程序集（同标识）
    note: 同上
  - path: 1.6/Assemblies/84582c26-a489-4566-ac84-bc2a64e6edcf_PersonalChronicle.dll
    type: D
    symbols: 重影程序集（同标识）
    note: 同上
  - path: 1.6/Assemblies/a2d89431-95ab-419e-8ee6-e0b0f9e846c1_PersonalChronicle.dll
    type: D
    symbols: 重影程序集（同标识）
    note: 同上
  - path: _scripts/compile.rsp
    type: M
    symbols: csc 响应文件（/out + 全部 /reference + 源文件清单）
    note: 生成物：由 compile_with_sdk.ps1 每次编译重写；本轮由「旧工程绝对路径」改为探测结果
  - path: _scripts/compile_with_sdk.ps1
    type: M
    symbols: Resolve-RimWorldRoot / Resolve-WorkshopAsm / Resolve-Csc；$outDir = <devRoot>\<GameVersion>\Assemblies
    lines: 1-150
    note: 移除硬编码旧工程绝对路径与 RUIF 死路径；缺硬依赖时显式 throw；修复 Resolve-Csc 的 $hits[0] 单元素退化取字符 bug
  - path: _scripts/deploy-to-game.ps1
    type: M
    symbols: -GameVersion / -RimWorldRoot；$items 与 $checkMap 带版本前缀；首次部署分支
    lines: 1-235
    note: 部署结构 = <mod>\LoadFolders.xml + <mod>\About\... + <mod>\<ver>\...；目标不存在则创建、无 About 则跳过 packageId 比对
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
    symbols: BuildInfo.BuildTimeUtc
    note: 由 compile_with_sdk.ps1 每次编译自动重写（=2026-10-13 12:41 UTC）；属生成物，非手改
---

# 变更标注 · CHG-20261013-MULTIVERSE-W1

## 一、用户需求

「我想一个 mod 兼容多个版本，先建立 1.6 和 1.7 版本文件夹，因为 1.7 是新引擎，优先建立 1.6 版本。」
→ 「现在进行部署，E:\RUIF 和本 mod 2 个部署 mod」（同轮续做）。

经四问拍板：**①** 1.7 侧本轮**只建目录、不声明**（不写 v1.7 块、About 仍只写 1.6）；**②** 1.7/ 只建空骨架；
**③** 1.6/Assemblies 移走 GUID 重影 dll + 归档全部 .bak；**④** 编译链本轮改探测式。

## 二、本轮交付（工程侧，进 GATE-DELTA 判定）

| 文件 | 类型 | 改动 |
|---|---|---|
| `LoadFolders.xml`（工程根） | A | 新建。仅 `v1.6` 块：`<li>/</li>` + `<li>1.6</li>`。键恰好 2 段（写 `1.6.4871` 会触发 `MalformedVersion`）；不使用 1.6 已废弃的 `default` 键。写法与在产多版本范例（Harmony 工坊模组 `2009463077`）逐字一致。 |
| `1.7/` | A | 新建空骨架 5 目录：`Assemblies` / `Defs` / `Languages` / `Patches` / `Textures`。**本轮不声明、不入包语义**。 |
| `1.6/Assemblies/` 5 个重影 dll | D | 删除 `1.6/Assemblies/250c559d-bb01-44b7-8293-9c942d879880_PersonalChronicle.dll`、`1.6/Assemblies/527152be-b45a-4f01-80ec-20c70a6bc691_PersonalChronicle.dll`、`1.6/Assemblies/7656c543-8fc3-4024-8e52-f0301f8362f2_PersonalChronicle.dll`、`1.6/Assemblies/84582c26-a489-4566-ac84-bc2a64e6edcf_PersonalChronicle.dll`、`1.6/Assemblies/a2d89431-95ab-419e-8ee6-e0b0f9e846c1_PersonalChronicle.dll`（git 跟踪件 ⇒ 状态 ` D`，可 `git restore` 还原）+ 779 个 `PersonalChronicle.dll.bak_*`（未被 git 跟踪）。⚠ **更正**：归档落点 `_backups/assemblies_prune_20261013/` 事后实测**不存在**（见 §三.6）⇒ 779 个 `.bak_*` 不可恢复（属编译备份，可再生）。 |
| `1.6/Assemblies/PersonalChronicle.dll` | M | 本轮编译产出 2,460,160 B @`2026-10-13 20:41:47`（csc 无诊断输出 = 0 错 0 警）。⚠ **不列入 front matter `files[]`**：`gate-runner.ps1` 的 `$skipSeg` 含 `.dll` ⇒ 该文件的 M 对 DELTA 不可见，列入会被判「标注有、diff 无」而 BLOCKED。 |
| `_scripts/compile_with_sdk.ps1` | M | 全部外部路径改探测式：`Resolve-RimWorldRoot`（含 `RIMWORLD_ROOT` 环境变量与多候选）/ `Resolve-WorkshopAsm`（工坊 `<ver>` → `Current` → 根）/ `Resolve-Csc`（多 SDK 根取最新）。输出改为 `<devRoot>\<GameVersion>\Assemblies`。缺 RUIF 时**显式 throw** 并给订阅指引。 |
| `_scripts/deploy-to-game.ps1` | M | 新增 `-GameVersion` / `-RimWorldRoot`；`$items` 与覆盖率自检 `$checkMap` 全部带版本前缀；部署结构 = `<mod>\LoadFolders.xml + <mod>\About\... + <mod>\<ver>\...`；游戏根改探测式；新增「首次部署」支持（目标不存在则创建、无 About 则跳过 packageId 比对）。 |
| `_scripts/compile.rsp` | M | csc 响应文件（生成物）：由 `compile_with_sdk.ps1` 每次编译重写，`/out`、全部 `/reference` 与源文件清单随之改为探测结果（旧内容指向已消失的旧工程绝对路径）。 |
| `Source/PersonalChronicle/BuildStamp.g.cs` | M | 编译脚本自动重写的时间戳常量（非手改）。 |
| 游戏 `Mods\`（仓库外，**不入 GATE-DELTA**） | A/M | 部署两个 mod：`Mods\RUIF`（6 文件）+ `Mods\殖民地综合管理系统`（256 文件 = `LoadFolders.xml` + `About` 13 + `1.6` 241）。 |

### 本轮已关闭（原「未纳入」项）

- `WI-006` RUIF 硬依赖恢复并跑通一次编译 ⇒ **DONE**（探测命中 `Mods\RUIF\Assemblies\RUIF.dll`，产出新 dll）
- `WI-007` 发布区重建与两区一致性校验 ⇒ **DONE**（256 文件部署；259 项 SHA256 比对 0 DIFF）

### 仍未纳入本轮（已登记台账 PROG-MULTIVERSE）

- `WI-004` 1.7 加载机制核实（缺 1.7 程序集，**不可执行**）
- `WI-005` 1.7 对外声明（v1.7 块 + supportedVersions）
- `WI-008` `_scripts` 其余旧绝对路径迁移（约 38 个脚本）
- `WI-009` 1.7 引擎下 Def/XML 与 API 兼容性评估
- `WI-010` 1.6 实机冒烟（需用户启动游戏）
- `WI-011` Tests 工程引用路径过期（测试通路不可用）
- `WI-012` 编译/部署脚本缺版本一致性与重影守卫

## 三、平台事实与证据（可复现）

1. **加载机制**（ilspycmd 反编译 `Assembly-CSharp.dll` @1.6.4871）：`Verse.ModContentPack.InitLoadFolders()` —— 有 `LoadFolders.xml` 且已定义版本键时，先按当前版本命中，未命中则取「最大且 ≤ 当前版本」的键，最后才是 `default`；**无该文件**时默认加载 `<root>\<当前版本不带 build>`（即 `1.6\`），该目录不存在则扫描 root 下最大版本目录，随后挂 `Common\`，最后挂 mod 根。
   ⇒ 本文件落地后加载列表 = `[root\1.6, root]`，与改造前**完全同序**。
2. **重影危害**（`ModAssemblyHandler` / `ModContentPack` 反编译）：引擎按扩展名 `.dll` 过滤后用 `DirectoryInfo.GetFiles()` 枚举（**含 Hidden**），6 个文件程序集标识完全相同（`PersonalChronicle, Version=0.1.0.0`），按文件名排序 `2…` 先于 `P…` ⇒ 先加载者胜。清理后 `1.6/Assemblies` 的 `*.dll`（含 Hidden）计数 = **1**。
3. **脚本语法**：两脚本 `[System.Management.Automation.Language.Parser]::ParseFile` 均 **0 错误**。
4. **首轮试跑编译（RUIF 缺失时）**：`compile_with_sdk.ps1` 在探测阶段于第 92 行 `throw`「未找到 RUIF.dll（硬依赖：工坊 3799502474）」—— **明确失败而非静默引用死路径**。
5. **目录现状**：游戏根 `Version.txt` = `1.6.4871 rev590`。⚠ **环境漂移**：`…\RimWorld\11\殖民地综合管理系统` 空目录于 `2026-10-13 20:31:48` 重现（本会话前段实测为「不存在」），内容为空、与本轮改动无关，来源未确定。
6. **归档实物核验（事故登记）**：清理动作执行时终端回显 `archived count = 784`，但随后多途径复核（`cmd dir /a`、`[System.IO.Directory]::GetDirectories`、`Test-Path`、`E:\` 深度 5 搜索 `*assemblies_prune*`、工作区递归搜 `PersonalChronicle.dll.bak_*`）**一致显示该目录不存在**；`_backups\` 仅剩 `gate-hygiene-2026-09-18`，其自身 mtime 停在 `2026-10-13 20:16`（= 归档创建时刻，未随「删除」更新）。根因未确定（怀疑命令沙箱重定向或外部会话清理），**不作推断性结论**。影响：779 个 `.bak_*` 不可恢复（编译备份，可再生）；5 个 GUID dll 可 `git restore`。
7. **编译已执行（RUIF 就位后）**：探测命中 `Harmony = …\workshop\content\294100\2009463077\1.6\Assemblies\0Harmony.dll`、`RUIF = …\Mods\RUIF\Assemblies\RUIF.dll`；csc **无诊断输出**（判据 = 0 错 0 警），产出 `1.6\Assemblies\PersonalChronicle.dll` 2,460,160 B @`2026-10-13 20:41:47`（旧件自动备份 `.bak_20261013204132` / `…204145`）。期间修复 `Resolve-Csc` 的 `$hits[0]` 单元素退化取首字符 bug（症状：`Cannot find path …\D`）。
8. **部署已执行**：`Mods\RUIF` ← RUIF 6 文件（`About.xml` / `Preview.png` / `RUIF.dll` / `RUIF.xml` / 中英 `Keyed`；**不含** `PublishedFileId.txt`，保持工坊关联独立）；`Mods\殖民地综合管理系统` ← 256 文件（`LoadFolders.xml` + `About` 13 + `1.6` 241）；deploy 覆盖率自检 `Defs 38 / Languages 77 / Assemblies 1` 全 OK，目标 DLL 时间戳 `2026-10-13 20:41:47`。
9. **两区一致性**：开发区 ↔ 发布区逐文件 SHA256 `compared=259 / DIFF=0`；5 项 MISSING 均为**有意不含**（2 个编译 `.bak`、`.pdb`、`About.xml.bak_20260912_144217`、白名单外 `Textures/UI/ArchiveIcon.dds` —— 该 dds 全库 0 引用）。

## 四、一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖本轮实际改动全部文件，无遗漏
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动
- [x] 每个改动文件都标注了类型与涉及符号
- [ ] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留
- [x] 已用 `gate-runner.ps1 -Annotation <本文件>` 自动校验通过（见 §五 下方结论行）

## 五、未执行与已更正（诚实标注，GOV-012 不伪造 PASS）

| 项 | 状态 | 原因 |
|---|---|---|
| 编译产出 | **已执行 ✅** | 见 §三.7（RUIF 就位后编译成功，0 错 0 警） |
| 双 mod 部署 | **已执行 ✅** | 见 §三.8（`Mods\RUIF` 6 文件 + `Mods\殖民地综合管理系统` 256 文件） |
| 两区一致性 | **已执行 ✅** | 见 §三.9（259 项比对，0 DIFF；5 项有意不含） |
| 1.6 实机冒烟 | **已执行 ✅** | 见 §六：游戏正常启动并退出；`ModLoadFolder*` 告警 0、`Exception` 0、两 mod 均在 mod 列表、自检 40/41 PASS（唯一 FAIL 为新档无归档样本，属预期） |
| 1.7 任何结论 | **未执行** | 本机无 1.7 程序集，引擎机制不可核实 |
| 归档可回滚性 | **证伪（已更正）** | 归档目录实测不存在（§三.6）；已同步更正本标注与台账 `PROG-MULTIVERSE.WI-002` 证据 |
| 单元测试通路 | **不可用（既有缺陷）** | `Tests` csproj 引用已不存在的根 `..\Assemblies\`（真实在 `1.6\Assemblies\`）+ 无 Condition 的 `Mods\RUIF` 复制 ⇒ `dotnet test` 失败；已登记 `PROG-MULTIVERSE.WI-011` |
| 中英语言切换 | **未实测** | 本次运行语言为简体中文；切换语言需再启一次游戏（列为下轮候选 WI） |

## 六、实机验收证据（2026-10-13 · 用户启动游戏）

日志：`%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`（19,617 B · 20:50:50）

1. **两 mod 均被加载**：`Initializing new game with mods:` 段列出 `brrainz.harmony` → `Ludeon.RimWorld`(+5 DLC) → `PersonalChronicle.RUIFramework` → `colony.integratedmanagement`（顺序正确：Harmony → RUIF → 本 mod）。
2. **LoadFolders 无 issue**：grep `LoadFolder` / `ModLoadFolder` **0 命中** ⇒ 键合法、目录存在、无 `default`、无块序与空块告警。
3. **无异常**：grep `Exception` / `NullReference` / `Could not load assembly` / `failed to load` **0 命中**。
4. **跑的是本轮新 dll**：`[PersonalChronicle] build 2026-10-13 12:41 UTC`（= 本地 20:41，本轮编译产物）；`[PersonalChronicle] RUIF detected — RUIF-backed UI enabled.`。
5. **自检 `pass=40 fail=1`**：唯一 `[FAIL] D0 已归档样本 :: 0/7 battles=0 events=2`（新开局无归档样本，属预期而非缺陷）；A 组 Def 引用（专业技能 48 / 方向 / 效果 / 评级）全 PASS。
6. **Harmony 生效**：`[PC] WorkGate contract lock installed; target=WidgetsWork.DrawWorkBoxFor`。
7. **观察项（下轮候选 WI，本轮不入台账以免 Δ0）**：
   1. `Type GameComponent_PawnEconomy probably needs a StaticConstructorOnStartup attribute … field speechTailTexture of type Texture2D` —— 本 mod 触发的官方警告，建议核实并改为主线程预载。
   2. `Translation data for language Simplified Chinese has 2 errors` —— 需生成翻译报告定位（可能属 DLC 或本 mod）。
   3. `[ECON-PERF] planning ticks=1 subjects=2 avgMs=18.236 maxMs=18.236 slow>2ms=1` —— 单次规划 18 ms，建议纳入性能基线。

---

由 AI 助手（CodeBuddy · DeepSeek-V4.1-Flash）生成 · 2026-10-13。
