# Location 地图志（Overview › Location）验收清单

日期：2026-08-10 ｜ 模块：v4.12/v4.13 Location Atlas ｜ 版本：1.0.0

## 背景
此前 `LocationAtlasCapture.cs` 因编译缓存损坏被误判为 API 错误并临时排除。清理 `Source/obj` + `Source/bin` 后，确认**当前磁盘代码已用正确 RimWorld 1.6 API**（`Tile.PrimaryBiome/elevation/hilliness/WaterCovered/IsCoastal/MaxTemperature/MinTemperature`、`WorldGrid[tile]` indexer、`StockGenerator_Category.categoryDef` 等），编译 0 错 0 警。本清单用于验收 Location 地图志功能在游戏内是否正常。

## 验收前置
- [ ] `dotnet build -c Release` 0 警告 0 错误（已通过）
- [ ] dll 已同步发布版 `Mods/PersonalChronicleVer0.1/Assemblies`（已通过）
- [ ] 已**重启 RimWorld** 加载新 dll（未做）

## L1 身份（Identity）
- [ ] Overview › Location 分类计数显示 > 0（地图/定居点被归档）
- [ ] 每张地点卡显示名称/类型（settlement / quest site / 玩家地图）
- [ ] `WorldObjectDefName` 正确区分 Settlement / MapParent / 任务地点
- [ ] `MapGeneratorDefName` 正确记录地图生成器（Base_Player / 任务专用）
- [ ] `MapSize` 正确显示 "WxH"

## L2 归属（Ownership）
- [ ] `FactionDefName` 记录拥有派系
- [ ] `IsPlayerHome` 对玩家定居点 = true，其他派系 = false
- [ ] 无主之地（no man's land）`FactionDefName` 为空、`CanTrade` = false

## L3 地理（Geography）
- [ ] `Hilliness` 显示 Flat / Hilly / Mountainous / Impassable
- [ ] `Altitude` = Tile.elevation 数值
- [ ] `Pollution` = Tile.pollution 数值
- [ ] `IsCoastal` = Tile.IsCoastal
- [ ] `AvgTempC` = (Tile.MinTemperature + Tile.MaxTemperature) / 2（或 NaN 兜底）
- [ ] `SnowCovered` / `WaterCovered` 地理信息正确

## L4 生命周期（Lifecycle）
- [ ] `EstablishedTick` 首次观察时写入（仅一次，幂等）
- [ ] 地图 deinit / 世界对象销毁后 `DeinitTick` 写入当前 reconcile tick
- [ ] `DeinitReason` = "Abandoned"（无销毁对象）/ "Destroyed"（销毁对象）
- [ ] 重复 reconcile 不重复归档（AddObject 按 StableId 去重）

## 商贸快照（Commerce，纯 Def，无交易历史）
- [ ] 拥有 `baseTraderKinds` 的非空派系 `CanTrade` = true
- [ ] `TraderKindDefName` / `PermitRequiredDefName` 正确
- [ ] `TradeKindKeys` 归一为 8 类之一（res/cloth/food/drug/weapon/armor/implant/tech），去重、cap 4
- [ ] `StockGenerator_Category` → categoryDef / `StockGenerator_Tag` → tradeTag / `StockGenerator_SingleDef` → thingDef 分类均覆盖
- [ ] 不记录交易历史（只读编年史定位）

## 稳定性 / 防御
- [ ] reconcile 外层 try/catch：单个地图/世界对象异常不阻断整个 pass
- [ ] 老存档加载（无 v4.13 字段）Scribe 默认值零迁移不报错
- [ ] `LocationAtlasCapture` 不在热路径（仅 ReconcileIntervalTicks 节流窗内调用）

## 回归（本次会话其他改动）
- [ ] 社会关系网络（Social tab）节点上限 24、初次自动 fit、放大不重叠
- [ ] 编译产物不含 LocationAtlasCapture 的 CS 错误

## 测试结果
（游戏内实测后填写 PASS/FAIL + 备注）

## 待办
- [ ] 若 `LocationObject.SnowCovered` 未被 `ApplyTileSnapshot` 赋值（Tile 结构用 `WaterCovered`），确认是否需补赋值或删除该字段
