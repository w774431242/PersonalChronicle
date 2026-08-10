# 装备传承拓展（Equipment Legacy）· v4.9 C# 落地记录

**日期**：2026-08-10
**状态**：C# 已落地并编译通过（0 警告 0 错误）；**版本号未 bump**——待游戏实测后敲定小版本更新。
**定位**：PersonalChronicle 是**只读编年史**——只记录游戏内真实发生的事件，经 `IArchiveUiDataProvider` 聚合成 Read Model 快照，在窗口内纯消费呈现。

---

## 0. 设计红线（落地过程中严格遵守）

1. **只读记录，不制造机制**——退役捕获 `Patch_ThingDestroy` 是 Postfix，绝不跳过原方法。
2. **数据驱动，零硬编码叙事**——所有展示内容来自真实事件流 / 持有记录 / 退役捕获；无"器灵评语"类凭空生成文本。
3. **窗口只消费快照**——排序 / null-guard / 重叠计算归属 `ArchiveUiDataProvider`。
4. **UI 经 Design System 两层**——`UITheme` 令牌 + `UIComponents` 组件，无窗口内散落 `GUI.color = new Color(...)`；`DrawLegacyTab` 之后的所有新绘制走 `UIComponents.Label/Pill/Badge/ProgressBar/StatCell`。

---

## 1. Weapon 详情 tab 变更（v4.7 → v4.9）

| v4.7（旧） | v4.9（新） | 说明 |
|---|---|---|
| Overview | Overview | 保留（类型/制造者/时间线尾部） |
| Timeline | — | 退役，内容并入 Overview / 新增 tab |
| CombatLog | — | 退役，击杀信息见 Legacy 表 |
| Legacy | Legacy | 保留 |
| — | **Origin（溯源）** | 新增 |
| — | **CoUse（同袍共用）** | 新增 |
| — | **Decommission（退役仪式）** | 新增 |

`PawnTabKeys` 不变（Overview/Career/CombatLog/Social）。

---

## 2. 各 tab 数据契约与 C# 实现

### 2.1 溯源 Origin + 工坊署名链 MakerChain
- 快照：`ThingOriginView` / `MakerChainView`（`ReadModels/SectionSnapshots.cs`）。
- 推导：`ArchiveUiDataProvider.BuildOrigin` 扫描事件流，**首个** Craft/Built 事件 → `kind=craft`（来源=制造者 subject）；首个 Battle/Death 事件 → `kind=battle`（战场出身）。`BuildMakerChain` 读制造者 `PawnRecord`：若制造者 `IsArchived` 且死亡事件 Subjects 含本武器 Thing edge → `MakerDiedByOwn=true`（"此匠死于自己亲手所造之物"）。
- 空态：无制造/建造/战斗记录 → `IsEmpty` → 显示 `Origin.Empty`。

### 2.2 同袍共用 CoUse
- 快照：`CoUseView` / `CoUseRowView`。
- 推导：`BuildCoUse` 对 `HolderRecords` 构建互斥任期区间（同 `BuildLegacy`），两两计算重叠天数，排除当前持有者，按共用天数降序，share% 相对最长者。
- 空态：少于 2 名持有者或无重叠 → `CoUse.Empty`。

### 2.3 退役仪式 Decommission
- 快照：`DecommissionView`。
- Domain：`DecommissionRecord`（Tick / LastHolderStableId / LastHolderLabel / LastPlaceLabel / ServiceDays / LastBattleLabel），持久化到 `ThingObject.Decommission`（Scribe null-safe）。
- 捕获：`Patch_ThingDestroy`（Postfix `Thing.Destroy(DestroyMode)`，Harmony `PatchAll` 自动生效），过滤 equipable + 仅已入档物品。写入经 `IArchiveService.OnThingDestroyed(Thing, Pawn)`。
- 空态：在册物品 `HasRecord=false` → `Decommission.Empty`。

---

## 3. 文件改动清单

| 文件 | 改动 |
|---|---|
| `Source/.../ReadModels/SectionSnapshots.cs` | +4 快照类；`DetailSnapshot` +4 字段 |
| `Source/.../ReadModels/ArchiveUiDataProvider.cs` | +`BuildOrigin`/`BuildMakerChain`/`BuildCoUse`/`BuildDecommission` + 辅助 |
| `Source/.../Domain/DecommissionRecord.cs` | **新增** |
| `Source/.../Domain/ThingObject.cs` | +`Decommission` 字段并持久化 |
| `Source/.../Application/IArchiveService.cs` | +`OnThingDestroyed` |
| `Source/.../Application/ArchiveService.cs` | +`OnThingDestroyed`/`PlaceLabelForDestroyedThing` |
| `Source/.../Capture/Patch_ThingDestroy.cs` | **新增**（含签名探测） |
| `Source/.../Archive/ArchiveMainTabWindow.cs` | `WeaponTabKeys`→5 tab；+缓存字段；+3 case；+3 高度分支；+3 绘制方法 |
| `Languages/ChineseSimplified/Keyed/Archive.xml` | +22 键 |
| `Languages/English/Keyed/Archive.xml` | +22 键 |

---

## 4. 验证

- 编译：`D:\toolbox\dotnet-sdk\dotnet.exe build Source\PersonalChronicle.csproj -c Release` → **0 警告 0 错误**。
- XML：zh/en 语言文件 426 标签配对，新键齐全。
- 深度检查：新绘制方法全部经 `UIComponents`（内部配对恢复）；`BuildMakerChain` 的死亡事件武器 Subject edge 判定已对照 `AttachCombatSubjects`（武器以 `ArchiveCategoryKeys.Thing` 入 Subjects）确认成立。

---

## 5. 开放问题（待游戏实测）

1. **退役捕获面**：`Thing.Destroy` 覆盖熔毁/损耗/拆解，但**卖出/掉落不触发**（物品转移到商队/地面）。实测确认这是否符合预期。
2. **CoUse 数据稀疏**：当前捕获只记录顺序持有（无并行标记），多数装备 CoUse 为空态。若要真实并行共用数据，需扩展 `HolderRecord.Kind`（Shared/Reunite）——列入后续 v5.x。
3. **版本号**：按用户指示待实测后敲定小版本更新（`About.xml` / `ApiVersion` 均未动）。
