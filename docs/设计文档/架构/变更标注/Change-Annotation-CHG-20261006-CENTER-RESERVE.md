---
change_id: CHG-20261006-CENTER-RESERVE
version: v1.4.x
date: 2026-10-06
author: AI（玩家裁决）
scope: [Defs, Source/PersonalChronicle/Application/SystemCenters, Source/PersonalChronicle/Domain, Languages, docs/changelog]
files:
  - path: Defs/SystemCenterDefs.xml
    type: M
    symbols: Def:logistics_center(reserved false→true) / Def:medical_center(reserved false→true)
  - path: Source/PersonalChronicle/Application/SystemCenters/SystemCenterDataRegistry.cs
    type: M
    symbols: SystemCenterDataRegistry.CreateWithBuiltins（解除 2 条 Provider 注册）
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinMedicalProvider.cs
    type: D
    symbols: BuiltinMedicalProvider（整文件 139 行）
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinLogisticsProvider.cs
    type: D
    symbols: BuiltinLogisticsProvider（整文件 120 行）
  - path: Source/PersonalChronicle/Domain/SystemCenterIds.cs
    type: M
    symbols: SystemCenterIds.Medical / SystemCenterIds.Logistics（删常量 + 墓碑注释）
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: Key:PersonalChronicle.UI.MasterControl.Kpi.{Downed,MedicineIndustrial,MedicineHerbal,ItemWealth,StorageBuildings}（删 5 条）
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: Key:PersonalChronicle.UI.MasterControl.Kpi.{Downed,MedicineIndustrial,MedicineHerbal,ItemWealth,StorageBuildings}（删 5 条）
  - path: docs/changelog/v1.4.x_2026-10-06_医疗与内务降级为预留中心.md
    type: A
    symbols: 用户向 Changelog（REL-004）
---

# CHG-20261006-CENTER-RESERVE · 医疗与内务降级为预留中心

## 1. 需求与裁决

**玩家原话**：「将医疗和内务禁用」（附总控中心实机截图）。

**裁决过程**：主 Agent 提出三档（新增启用/禁用子系统 / 降级为预留中心 / 彻底移除），
玩家明确选择**第二档「只降级为「预留中心」」**，并给出约束：*不动框架代码，改动最小*。

**目标**：总控中心里 `medical_center`（医疗系统中心）与 `logistics_center`（内务系统中心）
从「初始中心」分组移入「预留中心」分组，徽标显示「预留 · 未接入」，不采集 KPI，详情页不接数据源。

**非目标**（玩家已锁死）：不新增启用/禁用机制、不新增设置项、不新增第四态徽标、不改 UI 绘制代码、
不删 Def、不动 `SystemCenterReadModelProvider` / `MasterControlTabWindow` 的任何逻辑。

## 2. 为什么必须「Def + 组合根」成对改（本批的核心判断）

`SystemCenterDef.reserved` 不是纯展示位——框架用它做两件事：

1. `SystemCenterReadModelProvider.BuildCard`：`reserved` 为真时 **不查数据源**（`provider = null`），
   `ResolveWiring` 直接返回 `Reserved`，因此不取 KPI（LAND-009 诚实性）；注册表页按它分区。
2. `ChronicleMod.ValidateSystemCenterProviders` 的**反向校验**：`reserved=true` 的中心若仍注册了
   数据 Provider，则每次启动走 `ChronicleLog.Warning` → `Log.Warning` 写一条
   「SystemCenter provider registered for a reserved center」——该分支自述此 Provider
   「UI 永远不会调用」＝**死代码**。

⇒ 只改 Def 不改注册表 = 把「每次都刷的不一致告警 + 两坨死代码」引进仓库。
故本批把 Def 与组合根**成对**修改（见下文 §3 的 A/B/C），这也是全部删除动作的动机。

## 3. 逐文件说明

### A. `Defs/SystemCenterDefs.xml`（M）

- `logistics_center`、`medical_center` 的 `<reserved>` 由 `false` 改为 `true`；
- 各补一段中文注释块（仿本文件 `policy_center` 的既有风格），记录：日期 / CHG ID / 裁决人 = 玩家 /
  为什么用 `reserved` 而不是删 Def（可逆、保稳定主键与档位、保住其他中心指向内务的 `dependsOn` 锚点）/
  **成对约束** / 复活路径；
- 注释内不含 `TODO` / `FIXME` / `HACK` / `XXX:` 等残留扫描 token。

### B. `SystemCenterDataRegistry.cs`（M）

`CreateWithBuiltins` 内删除两行：

```text
- registry.Register(new BuiltinLogisticsProvider());
- registry.Register(new BuiltinMedicalProvider());
```

原地留注释说明「预留 Def ⟺ 无 Provider」的成对约束、被删两行的原文与复活步骤。
其余三个内置 Provider（制造 / 殖民地经济 / 人员）与 `Register` / `Get` 契约**未动**。

### C. 两个 Provider 源文件（D）+ `SystemCenterIds.cs`（M）

- `BuiltinMedicalProvider.cs`、`BuiltinLogisticsProvider.cs` **整文件删除**。
  两者均为纯只读观测（无 Harmony、无 Scribe、无策略 State），其 `AppendKpis` 的
  try/catch + Pending 降级链是 2026-09-19 审计的既有成果，随文件一并归档。
- **删除前已归档**：`_backups/centers_retire_20261006/`（两个 `.cs` 原件 + `README.md` manifest，
  含完整 6 步回滚）。**为什么必须归档**：这两个文件在本仓库中**未被 git 跟踪**
  （`git ls-files --error-unmatch` 报 `did not match any file(s) known to git`），
  删除后没有任何历史副本可恢复；项目既有恢复机制即 `_backups/`（先例 `_backups/lce_cleanup_20260920/`）。
- `SystemCenterIds.cs`：删 `Medical` / `Logistics` 两个常量并留墓碑注释。
  依据是本类的自述不变量「**只登记被内置数据源绑定的中心 ID**」——预留期这两个中心没有内置数据源，
  留着常量会让常量与注释互相矛盾。`Manufacturing` / `Economy` / `Population` 未动。

### D. 翻译（M ×2）

删除 5 个 KPI Key × 中英各 1 组（`Downed` / `MedicineIndustrial` / `MedicineHerbal` /
`ItemWealth` / `StorageBuildings`），每处留一行墓碑注释（CHG ID + 复活路径）。
**逐键零引用确权**：全仓检索命中面仅这两个 Provider 的私有常量（`BuiltinMedicalProvider.cs:40-42`、
`BuiltinLogisticsProvider.cs:41-42`）+ 中英 Keyed 自身；`Tests/` 0 命中、`_scripts/` 0 命中。
相邻键 `Kpi.Silver` / `Kpi.ColonyWealth`（经济中心在用）与 `MasterControl.Status.Reserved`（本次要显示的
「预留 · 未接入」文案）**未动**。

## 4. 依赖影响（已知、无需处理，登记以备下轮审计）

`manufacturing_center` / `economy_center` / `medical_center` 的 `dependsOn` 仍指向 `logistics_center`
（现已预留）。**判定：合法，不连带修改。** 依据：

- 消费面穷举：`dependsOn` 在 Source 中的全部出现都是**显示 / 模型**用途
  （`SystemCenterReadModelProvider.cs:120,327-328`、`MasterControlTabWindow.Detail.cs:371`、
  `PopulationViews.cs:369,434`、`SystemCenterDef.cs:46`、`SystemCenterSnapshots.cs:207`），
  **无校验、无拓扑执行** ⇒ 运行期零影响；
- Def 语义：预留 = 「ID 与档位已登记、玩法待对齐」，蓝图依赖指向预留中心属合法状态；
- 先例：`population_center` 对已预留的 `policy_center` 的事件契约引用照常保留。

## 5. 兼容性 / 存档

- **Save 净变更 0**：两个 Provider 无 Scribe、无持久化字段；`SystemCenterDataRegistry` 是运行期对象，
  不进存档。旧档可直接加载，无 Migration 需求（SAVE-005：本次删除项不涉及持久化数据）。
- **第三方**：`SystemCenterIds.Medical/Logistics` 是 `public const`（**非契约面**，契约面为
  `IPersonalChronicleApi`）。`const` 编译期内联 ⇒ 已编译的第三方程序集无运行期破坏；
  仅「重新编译才有意义」的源码级引用会断，已在 Changelog 的 `Removed` 段登记。
- **运行期扩展点不变**：`PersonalChronicleMod.SystemCenterData` / `Register` / `Get` 全部照旧；
  第三方若自行给这两个中心注册 Provider，会命中启动期告警（设计如此，非本批引入）。

## 6. 回滚路径

1. 拷贝 `_backups/centers_retire_20261006/Builtin{Medical,Logistics}Provider.cs` 回
   `Source/PersonalChronicle/Application/SystemCenters/Builtin/`；
2. 恢复 `SystemCenterIds` 两个常量（文件内墓碑注释已附原文）；
3. 恢复 `CreateWithBuiltins` 两行 `Register`（注释已附原文）；
4. `Defs/SystemCenterDefs.xml` 的 `reserved` 改回 `false`；
5. 恢复中英各 5 条 KPI Key（墓碑注释已标位置）；
6. 重编译 + 部署。

**步骤 3 与 4 必须成对**，只做其一即触发启动期不一致告警。

## 7. 验证证据

| 项 | 结果 |
|---|---|
| 编译（`_scripts/compile_with_sdk.ps1`，`compile.rsp` 自动重生） | **0 错 0 警** |
| 离线单测（`dotnet test Tests/PersonalChronicle.Tests.csproj`） | **548 通过 / 0 失败 / 1 跳过（549）** |
| XML 良构（`[xml]` 解析） | `SystemCenterDefs.xml` OK（12 个 Def）· 中英 `Archive.xml` OK |
| 残留引用扫描（Source 全量） | 被删符号仅剩注释文本，零活引用 |
| 双区同步（`_scripts/sync_release.ps1`，数组形式传 4 文件） | 4 文件 SYNC · **DLL MATCH=True** · XML identical=78 |

**范围外（预先存在，非本批引入）**：两区比对仍报 `DIFF: Defs\T1_Production.xml` 与
`ONLY-IN-REL: Languages\English\DefInjected\DesignatorDropdownGroupDef\T1_Production.xml`
（T1 产线相关，与本批零交集，未处理，登记待办）。

## 8. 实机验证清单（沙箱无游戏进程，必须人工复核）

- [ ] 启动无红错；Player.log **不得出现** `SystemCenter provider registered for a reserved center`
      （本批要消除的正是这条）；
- [ ] 总控页：初始中心 **3 卡**（制造 / 殖民地经济 / 人员），预留中心 **7 卡**
      （防御 / 医疗 / 内务 / 科研 / 外交 / 环境 / 政策）；
- [ ] 两张新预留卡：灰卡 + 徽标「预留 · 未接入」+ 无 KPI 读数；点进详情页显示预留说明；
- [ ] 中英语言切换目视徽标与分组；
- [ ] 旧档加载抽验（理论零影响）。

## 9. 未登记为正式变更的文件（不列入 front matter 的理由）

- 本标注自身：`gate-runner.ps1:99-107` 会把 `-Annotation` 文件从 git 改动集中移除，
  一旦列入反而被判 phantom（沿用 CHG-LCE-001 先例，不列最稳）；
- `_backups/centers_retire_20261006/*`：`_backups/` 在 `.gitignore` 与 gate 的 skip 段内；
- `Assemblies/*.dll`、`_scripts/compile.rsp`、`Tests/bin|obj`：生成物（gate 的 skip 段 / 范围外清单）。
