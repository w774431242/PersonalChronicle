# RimWorld 1.6「建筑方向」Mod 开发公开支持资料

> 检索整理日期：2026-08-14
> 来源：RimWorld 官方 Wiki（rimworldwiki.com）、Ludeon 官方《1.6 Modder Primer》、社区资源枢纽
> 用途：PersonalChronicle 档案馆 UI 中展示「劳模所属工坊 / 住所」等建筑信息的前置调研，以及后续建筑类 mod 开发参考。
> 注意：Wiki 部分页面标注「Outdated / Under Review」，引用时以反编译引擎 dll 核验为准（本仓库铁律）。

---

## 一、官方权威资料源（优先阅读）

| 资料 | 链接 | 说明 |
|---|---|---|
| 官方 Wiki Modding Tutorials 枢纽 | https://rimworldwiki.com/wiki/Modding_Tutorials | 社区维护，经 RimWorld Discord #mod-development 审核 |
| **1.6 Mod Updates**（Wiki 版本） | https://rimworldwiki.com/wiki/Modding_Tutorials/RimWorld_1.6_Mod_Updates | 1.6 全部 Breaking Changes + 新 API（含 Odyssey DLC 剧透） |
| **1.6 Modder Primer**（Ludeon 官方 Google Doc） | https://docs.google.com/document/d/e/2PACX-1vRKE9u5ZW_zG45pxzwNvy4sxvozDeqtxlxpac5jwenOeW6liQCPgmPl9bIbtcMuqL1NPIDHOLFg64M_/pub | 官方面向 Modder 的 1.6 技术变更说明，每 5 分钟自动更新 |
| 1.6 原版变更日志（官方 Google Doc） | https://docs.google.com/document/d/e/2PACX-1vRCjqVtPQDFGu4POiKTUd_8o3U2Asdhx99SOvcgU66ABdYtk3Cgndd53yJ6BC4tZX530pp_m6lf4Z9P/pub | 1.6 全部原版游戏改动 |
| 官方 Wiki ThingDef 教程 | https://rimworldwiki.com/wiki/Modding_Tutorials/ThingDef | 讲解「如何自学任何 XML 标签含义」的方法论（非字典） |
| 社区资源枢纽 RWModdingResources | https://spdskatr.github.io/RWModdingResources/ | 涵盖更广的 Mod 制作指南 |
| Roxxploxx Mod 制作教程集 | https://github.com/roxxploxx/RimWorldModGuide/wiki | 社区教程 |

### 官方 Wiki 建筑相关教程状态（截至检索日）
- **Simple Building**（基础建筑教程）：🔜 预告中，未发布
- **Custom Workbench**（自定义工作台教程）：🔜 预告中，未发布
- **ThingDef explained**：⚠️ 过时 / 待重写区，需甄别 1.5/1.6 失效信息
- PlaceWorker / Designator / Blueprint / Frame：Wiki 枢纽页未单列教程，需靠反编译原版代码自学

---

## 二、1.6 对「建筑 / 工作台」Mod 的关键变更

### 2.1 XML / Def 层变更（迁移必改）

| 旧字段 | 新字段 | 说明 |
|---|---|---|
| `<placingDraggableDimensions>` | `<drawStyleCategory>` | 接入 1.6 新的设计ator 形状系统，值为 `DrawStyleCategoryDef` |
| `<soundAmbient>` | `CompProperties_AmbientSound` ThingComp | 环境音改为 Comp 实现 |

`drawStyleCategory` 原版预设：`Default2D`、`Fill2D`、`Default1D`、`FilledRectangle`、`Walls`、`Conduits`、`Defenses`、`Zones`、`Areas`、`Orders`、`Mine`、`Paint`、`Plans`、`RemovePlans`、`RemoveZones`、`Foundations`、`Floors`、`Cancel`、`Plants`。

`CompProperties_AmbientSound` 官方示例（来自 Toxifier Generator）：
```xml
<li Class="CompProperties_AmbientSound">
    <sound>Toxifier_Working</sound>
    <disabledOnUnpowered>true</disabledOnUnpowered>
</li>
```

### 2.2 C# / API 层 Breaking Changes（建筑相关）

| 旧 API | 新 API |
|---|---|
| `Designator.DraggableDimensions`（虚属性） | `DrawStyleCategory` 属性，返回 `DrawStyleCategoryDef` |
| `InteractionUtility`（社交互动） | `SocialInteractionUtility`（注意现 `InteractionUtility` 是「命令 Pawn 与物体互动」的另一类） |

### 2.3 建筑「读取 / 交互」相关的 1.6 新机制

1. **可变 Tick 率（VTR）系统**：`Job` / `ThingComp` / `Thing` 新增 `TickInterval(int delta)`；非关键逻辑（状态检查、动画推进）应迁移至 delta 驱动以省性能。配方进度等关键逻辑仍可用 Tick。
2. **地基层（Foundations）**：新增地形网格层（顶层与底层之间），原版桥梁属此层；**地面可铺在桥梁之上** → 水上/桥面工作台类 mod 可直接受益。
3. **`IPathFindCostProvider` 接口**：Thing 可实现该接口给地图添加额外路径成本（危险地形、禁区建筑），路径系统自动计算。
4. **浮动菜单重构 `FloatMenuOptionProvider`**：巨型方法拆分为独立 try-catch 的选项 Provider，单个选项报错不崩整个菜单。自定义 Building 右键菜单/交互选项建议迁移。
5. **`DrawStyleDef`**：支持矩形以外的选区形状（椭圆/圆、线条），可覆盖 `SingleCell` 改单选单元格。
6. **`IThingHolder` 自动 Tick 内容物**：默认自动 tick 内容物；需恢复 1.5 行为可实现 `IThingHolderTickable` 使 `ShouldTickContents` 返回 false。
7. **Thing 跨地图移动回调**：新增 `PreSwapMap` / `PostSwapMap`。

### 2.4 大型建筑生成（1.6 新增系统）

- **Structure Layouts**：结构 = 房间集合（LayoutRoomDef + RoomPartDef），优先拆分为可复用 `RoomPartDef`，而非自定义 `RoomContentsWorker`。
- **Prefabs 预制件**：游戏内创建物体集合 → 导出 XML 复用；调试操作 `Generation/Create Prefab`；默认只复制建筑。
- **MapGenUtility**：`GetClearRects` / `TryGetRandomClearRect` / `TryGetLargestClearRect` / `TryGetClosestClearRectTo`；`UsedRects` 系统防重叠。
- **GenStep 顺序重排**（有自定义 GenStep 的 mod 必须对齐）：关键大型结构（400–500）→ 玩家出生点（850）→ 重要建筑 1600+（须手动更新迷雾网格）。

### 2.5 性能建议（对建筑 Comp）

- Comp 类 **sealed** 时 `GetComp` 性能最佳。
- 热路径避免「请求不存在的非密封 Comp」或「请求类型不存在但有派生类型」两种情况。

---

## 三、建筑 ThingDef 基础结构（结合原版代码核验口径）

Wiki 教程强调 ThingDef 有 200+ 标签无法逐一文档化，推荐方法论：
1. 游戏原版 XML（`Core/Defs/ThingDefs_Buildings/`）搜索字段实际用法；
2. 反编译 `Assembly-CSharp.dll` 查 `ThingDef` / `BuildingProperties` 字段；
3. 右键字段「分析」引用处看游戏如何使用。

建筑类 ThingDef 常用字段（常识性参考，落地前须反编译核验）：
```xml
<ThingDef>
  <defName>...</defName>
  <size>1,1</size>              <!-- IntVec2，多格建筑如 2,3 -->
  <graphic>...</graphic>        <!-- GraphicData：贴图、图层、着色 -->
  <statBases>...</statBases>    <!-- HP/美观度/易燃性 等 StatDef 基础值 -->
  <building>...</building>      <!-- BuildingProperties：workToBuild、constructEffect 等 -->
  <blueprint>...</blueprint>    <!-- 蓝图 ThingDef -->
  <frame>...</frame>            <!-- 建造框架 ThingDef -->
  <placeWorkers>...</placeWorkers> <!-- PlaceWorker 列表：放置合法性校验 -->
</ThingDef>
```

### 建筑 C# 类体系（与 PersonalChronicle 读取需求直接相关）

| 类 | 关键成员 | 用途 |
|---|---|---|
| `Building`（基类） | `def`、`Rotation`、`Map`、`LabelCap` | 一切建筑基类 |
| `Building_WorkTable` | **`BillStack billStack`** | **工作台建筑**（真实类名是 `WorkTable` 而非 `WorkBench`），持制造清单 |
| `Building_WorkTable_HeatPush` | 派生 `Building_WorkTable` | 发热型工作台 |
| `Building_WorkTableAutonomous` | 派生 `Building_WorkTable` | 自动化工作台（含机械孵化台等派生） |
| `BillStack` | **`Field billGiver`**、`get_Bills` | 反向指向持有该 BillStack 的工坊建筑自身 |
| `Bill` / `Bill_Production` | `Field billStack`、`Field recipe` | 单个制造订单 |
| `Room` | `Role`（→`RoomRoleDef`）、`ContainedBeds`、`ProperRoom` | 房间角色（卧室/营房）与床反查 |
| `RoomRoleDef` | `LabelCap`、`defName` | 房间角色名称 |
| `Pawn_Ownership` | `OwnedRoom`、`OwnedBed`、`Bedroom` | **Pawn 的住所**入口（`pawn.ownership`） |

> 以上均经 2026-08-14 PowerShell 反射核验 `Assembly-CSharp.dll`（16130 类型）确认存在。

---

## 四、对 PersonalChronicle 的落地启示

### 4.1「劳模所属工坊」读取路径（经核验可用）

- **住所**：`pawn.ownership.OwnedRoom.Role.LabelCap`（如「卧室 / 营房」），或 `pawn.ownership.OwnedBed` 反查房间。
- **工作建筑（正向 建筑→人）**：遍历 `map.listerBuildings` 取 `Building_WorkTable.billStack.Bills`，按 `Bill.recipe` 反查该工坊的制造任务。
- **工作建筑（反向 人→建筑）**：原版无 `pawn.工作建筑` 直接属性，须扫描所有 `Building_WorkTable.billStack.billGiver` 反查 worker。
- **当前 mod 局限**：`Patch_GenRecipe` Postfix 签名 `(Thing product, RecipeDef recipeDef, Pawn worker)` 无工坊参数；`OnThingCrafted` 未记录建筑；`GetLiveLocation` 只记 Map 名。
- **改造成路径**：捕获点换到 `Bill_Production.Notify_IterationCompleted` / 工作台完成事件，可拿 `Building_WorkTable` 实例写 `def.defName`（如 `Building_WorkTable_Smithy`）——属 DATA/COMP 层契约变更，落地前需走 AI-001 任务入口 + AI-007 Save 风险评估。

### 4.2 其余可借力点
- 若未来做「工坊统计」「建筑分布」功能，1.6 的 `MapGenUtility` + `UsedRects` 只对生成有用；运行时统计仍走 `listerBuildings` 扫描。
- 自定义建筑相关 UI/交互注意 1.6 `FloatMenuOptionProvider` 重构。

---

## 五、资料时效性提示

- Wiki「ThingDef explained」「Writing custom code」位于 Outdated / Under Review 区，勿直接引用其字段结论。
- 官方 Modder Primer 为实时维护文档（每 5 分钟自动更新），以官网最新版为准。
- 本仓库铁律：**首次使用引擎 API 必须先编译探错或 PowerShell 反射核验，严禁推断**。
