# 自动化农机 开发流程 SOP v1.0

> 文档定位：**照着做就能落地**的操作手册——从需求到发布区验收的完整流水线。
> 与其它文档的分工：
> - PM 契约（做什么/为什么）：`自动化农业设备_开发设计指导_v2.md`（v2.1）
> - 架构与生命周期（内部怎么长）：`自动化设备_开发架构样例_生命周期管线_v1.0.md`
> - 物流（产出怎么运走）：`../../车间体系/管线运输体系_设计草案_v0.1.md`（已升 v0.2）
> - 本文件：**流程、命令、踩坑台账、验收清单**（做的时候按什么顺序、敲什么命令、哪里会炸）
>
> 状态：基于 2026-09-06 全链路实机实测整理（农机 S/M/L + 物流管道 + 隐藏管道 + 掉落物收集 全部跑通）。

---

## 1. 架构一图（当前真实状态）

```
┌─ Def 层 (XML) ────────────────────────────────────────────────────┐
│  Automation_AutoFarm.xml    PC_AutoFarmBase → S/M/L（组件组装链）   │
│  Automation_Pipe.xml        PC_PipeSegment(+Name) → Hidden 子类     │
│  Automation_Conveyor / Jobs / Research / WorkType / FarmComponents  │
│  comps 声明能力：PowerTrader/Refuelable/Flickable/Glower/Battery/   │
│                 CompProperties_AutoAgriculture/ItemStorage/ItemPort │
├─ 建筑薄壳层（零游戏逻辑）─────────────────────────────────────────┤
│  Building_AutoFarm : Building, IWorkshopDevice                      │
│  Building_PipeSegment : Building（虚拟流）；Building_PipeHidden 子类 │
│  Building_ConveyorSegment : Building_Storage（实体流备选）          │
├─ Comp 能力层 ─────────────────────────────────────────────────────┤
│  CompAutoAgriculture   农业状态机（+ .Collect.cs 掉落物收集 partial）│
│  CompItemStorage       内置仓（IItemStorage，capacity<=0=无上限）   │
│  CompItemPort          输出端口（绑定定向目标，推入管网）            │
├─ Map 协调层 ──────────────────────────────────────────────────────┤
│  MapComponent_AutoFarmTerrain  地格归属登记（多机防冲突）            │
│  MapComponent_PipeNetwork      管网拓扑 + 惰性 BFS 距离表            │
│  MapComponent_WorkshopManager  车间排班                             │
├─ 接缝层（对外契约，禁 defName 匹配）──────────────────────────────┤
│  IWorkshopDevice  设备识别（车间自动纳入，零注册）                   │
│  IItemStorage     物流对接（Capacity/Buffer/AcceptQuantity/Push/     │
│                   TakeOut/DropAll）——新设备接管道的唯一入口          │
└───────────────────────────────────────────────────────────────────┘
```

**四条分层铁律**（详见生命周期文档 §1）：建筑只做壳、能力参数化进 comp、跨机协调进 MapComponent、对外只暴露接口。

---

## 2. 流水线总览

```
① 需求契约 → ② 骨架（Def+薄壳）→ ③ 能力（comp/Tick）→ ④ 产出物流
        → ⑤ 交互 UI → ⑥ 资产贴图 → ⑦ 收尾闭环（编译→单测→审计→同步→实机）
                                          ↑                              │
                                          └────── 失败回退到对应阶段 ◄────┘
```

---

## 3. 阶段详解

### ① 需求与契约（PM 决策先行）

- 功能边界写进 PM 契约文档 §PM 决策清单，落地时以文档为准，**不在代码里口头约定**。
- 明确三件事：**运行条件**（通电/燃料/开关）、**语义诚实点**（什么情况该反压而不是丢弃）、**玩家入口**（gizmo/检视/弹窗）。
- 例：机器收成不走 `GenRecipe`（不触发职业捕获，PM 契约：机器无人力）。

### ② 骨架搭建

| 步骤 | 要点 |
|---|---|
| Def 抽象基类 | 用 `<ThingDef Name="XxxBase">` 供继承；**实体 def 也能带 `Name` 当继承源** |
| 档位子类 | `ParentName` 引用，只覆写差异字段（继承是**追加非覆盖**，comps 同理） |
| 必补字段 | 不继承 `BuildingBase` 时须显式给 `selectable`（默认 false = 无法选中）、`altitudeLayer` |
| 建筑薄壳 | `XxxBuilding : Building, IWorkshopDevice`，只有 `DeviceLabel` + 范围可视化 + gizmo |
| 测试期 | 0 成本直造，并在 XML 注释写清"发布前要恢复什么"（成本/研究/技能门槛） |

### ③ 能力实现（Tick 纪律）

统一用 `CompTickInterval(int interval)` 累加配额，**禁硬编码 `progress += 250`**（1.6 自适应 tick）。
每条节拍通道一个独立配额计数器；门控不满足**零扫描零分配**。

当前农机四条通道（新增设备照此开通道）：

| 通道 | 节拍 | 工作量 | 说明 |
|---|---|---|---|
| A 工作片 | 120 | 12 格/次 | 轮转游标，播种/收割 |
| B 生长驱动 | 250 | 全量 | 对齐原版 Long ticker，快照自愈（在涨就让位，防双倍加速） |
| C 改造推进 | 90 | 一圈前沿 | 圈层外扩，与种植区解耦 |
| D 掉落物收集 | 90 | 96 格/波、4 堆/波 | **分片扫描与覆盖区大小解耦**（L 档 441 格也不全扫） |

端口推送 90、管道流动 90（均与传送带同拍）。

### ④ 产出与物流

```
收获 → CompItemStorage.Push（先进仓，无上限则不落地）
     → CompItemPort 每拍取一批 → 相邻管道段（须管网可达）
     → 虚拟流沿 BFS「距离-1」邻段推进 → 出口段物化落定向目标
     → 目标拒收/仓满 → 反压滞留（绝不 Destroy，零丢失）
```

- 缓冲**只存 `ThingDef + count`**，绝不存 Thing 引用。
- 自家设备：走 `IItemStorage.Push`；原版/第三方存储：**逐占格**找可收格落格（只绑 `Position` 单格会漏多格深存储）。
- 仓的语义要在**存储入口**守：`cropOnly` 白名单（见踩坑 #6）。

### ⑤ 交互与 UI

| 入口 | 规则 |
|---|---|
| `GetGizmos` / `CompGetGizmosExtra` | 先 yield base，再补本机键；**实例级缓存**（每帧调用，每次 new 实测 160→60 FPS） |
| `DrawExtraSelectionOverlays` | 消费 comp 缓存列表，勿改 |
| `CompInspectStringExtra` | **每个失败分支都要有字面提示**，禁止静默失败（见踩坑 #5） |
| 弹窗 | Def 选择统一 `Dialog_DefPicker<T>` 骨架（搜索+网格+详情），薄子类只给数据源 |
| 翻译 | `string.Translate(params object[])` 已过时（CS0618）；带占位符用 `key.Translate().ToString()` 再 `string.Format` |

### ⑥ 资产与贴图

- **能程序生成就不手绘**：`_scripts/gen_pipe_trench_sprites.ps1`（地下管沟+隐藏细线）、`fit_pipe_tile_fill.ps1`（管径统一/边缘实心化）、`slice_pipe_sprites.ps1`（图集切片）、`process_autofarm_textures.ps1`、`rotate_conveyor_sprites.ps1`。
- 生成脚本**先备份再覆盖**，输出打印每张的尺寸/参数，便于回归。
- 无缝的正确解法是**贴图四边贯通**，不是端环对齐（见踩坑 #1）。
- 多向贴图族必须做**像素审计**：边缘 alpha、跨变体直径一致。

### ⑦ 收尾闭环（每次必做，顺序不能换）

```powershell
# 1) 权威编译（必须 0 错 0 警）
& 'C:\dotnet8\dotnet.exe' build Source\PersonalChronicle.csproj -c Release /p:Platform=x64

# 2) 单测（当前 203/203）
& 'C:\dotnet8\dotnet.exe' test Tests\PersonalChronicle.Tests.csproj -c Release

# 3) 自动化审计（当前 245 项 0 问题）
powershell -NoProfile -ExecutionPolicy Bypass -File '_scripts\audit_automation.ps1'

# 4) 同步发布区：白名单 Assemblies/Languages/Defs/Patches/About/Textures
#    逐文件 Copy-Item + Get-FileHash 校验；旧 dll 先备份 .bak_<时间戳>
#    若报「用户映射区域打开的文件」= 游戏没关，先关游戏

# 5) 重启游戏实机验证（RimWorld 不热加载；漏同步 = 静默跑旧 dll）
```

Player.log：`C:\Users\Administrator\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`
（**第一取证现场**：`[PersonalChronicle][Automation] ...` 全链路埋点，卡在哪一环一查便知。）

---

## 4. 铁律速查卡

- Tick 一律 `TickInterval(int)` 累加配额；工作量与 tick 率无关。
- 缓冲只存 `ThingDef + count`；`Destroy` 只作落地彻底失败的兜底。
- Scribe：缓存不存（Spawn 重建）、外部世界影响必存、Def 走 `Reference`、平行表**不能 readonly** + `PostLoadInit` 对齐等长。
- 撤除还原以「当前地形 == 本机所铺」核对 + 全图登记表仲裁（防多机交叠污染）。
- 每帧入口禁 new；停机/无电/无燃料 → 零扫描。
- 反压优先于丢弃：下游满就停取，**不凭空 Destroy**。
- 新设备接管道：挂 `CompProperties_ItemStorage` 或实现 `IItemStorage`；管道端只依赖接口。
- 机器收成不走 `GenRecipe`（不触发职业捕获）。
- 动画一律用 **Fleck**（不是 Thing，不 tick 不存档）；**逻辑先行**，动画纯装饰，丢帧/削峰零物品丢失。

---

## 5. 踩坑台账（实机实测，按"症状 → 根因 → 修复"）

| # | 症状 | 根因 | 修复 |
|---|---|---|---|
| 1 | 管道段之间有缝、粗细不一 | 切片脚本等比 fit + 2px 留边；边缘行列半透明（alpha≈108）；Body 34px vs In/Out 24px 管径不一 | 程序化生成**四边贯通**沟槽贴图；族内统一管径 + 边缘行列克隆实心化 |
| 2 | 横铺管道渲染成竖管 | Body 素材「无后缀=竖管」与 In/Out「无后缀=东向」**命名惯例不一致** | `BodyHorizontal` 显式取 `Body_east`；多向素材逐张哈希核实，不许照搬惯例 |
| 3 | 贴图变 X / 路径错 | `Rot4.ToString()` 返回数字 "0"-"3" 而非方位名 | 方向后缀显式映射（east→无后缀/north/south/west） |
| 4 | 绑定目标后**完全不出货**（日志 0 推送 0 交付） | 非接口存储建筑只绑 `Position` 单格；多格深存储其它格不可达即全链静默 | 绑定改「建筑目标」，交付逐 `OccupiedRect()` 占格找可收格 |
| 5 | 卡死但界面无任何提示 | 目标拒收是静默分支 | 检视补「目标不收 {0}（筛选拒收→端口停取）」双语键 |
| 6 | 仓里混进钢铁 | 覆盖区内 **mod 植物**成熟即割，金属产物入仓 | 三层防线：只割 `Zone_Growing.PlantDefToGrow`；掉落物只从种植区收；`cropOnly` 白名单兜底 |
| 7 | 管道无法铺在墙/机器下（占格子） | 未设 `<building><isEdifice>false</isEdifice></building>`，默认 true 登记 EdificeGrid | 对齐原版 `PowerConduit`：`isEdifice=false`+`isInert=true`+`canConstructInCorner`+`altitudeLayer=Conduits` |
| 8 | 非 edifice 后同格可叠铺两段（幽灵段） | **1.6 无 `PlaceWorker_OnlyOnePerTile`** | 自写 `PlaceWorker_NoPipeStack` 在放置阶段拦截 |
| 9 | `map.GetComponent` 取不到组件 | 1.6 **无 `MapComponentDef`**（字节核实 0 命中） | `Get(map)` 惰性新建 + `map.components.Add`（项目统一写法） |
| 10 | 大型机在建造列表里消失 | 1.6 删除 `CompProperties_PowerPlantSolar`，Def 加载即抛异常被整体丢弃 | 改用 `CompProperties_Power` + `compClass=CompPowerPlantSolar` + `transmitsPower` |
| 11 | 编译报 `ChronicleLog.Warn` 不存在 | 项目日志类只有 `Info/Warning/Error/Compatibility/Save` | 用 `ChronicleLog.Warning` |
| 12 | 每帧检视面板新建列表 | `CompInspectStringExtra` 里调扫描方法 | 缓存 + 仅选中/状态变化时重算 |
| 13 | 检视面板每帧 `GetComponent` 线性扫 | `Graphic` getter 每次查组件 | 按管网版本号缓存（已有 `visualVersion` 机制） |
| 14 | 存档/读档后行为异常 | **dll 未同步发布区**，游戏静默跑旧 dll | 编译后必同步 + 哈希校验（铁律） |

**1.6 环境其它确认**：无 `ZoneDef`/`InspectTabDef`；`SlotGroupManager` 已并入 `StorageGroupManager`；泛型类里辅助方法不能叫 `T`（撞类型参数）→ 用 `Tr`。

---

## 6. 游戏内验收清单（每次改动照此过一遍）

**农机**
- [ ] 三档均能建造、选中、显示覆盖范围
- [ ] 通电 + 燃料 + 开关三连：缺任一即停，检视有字面提示
- [ ] 播种/收割/夜间补光/耕地改造各自生效
- [ ] 仓已满（或无上限时）行为符合预期；「取出全部」可清空
- [ ] 只收种植区指定作物；mod 植物/野生植物不被割；掉落物只从种植区吸
- [ ] 收集动画：粒子沿最短直径向设备收敛

**物流管道**
- [ ] 可见段 + 隐藏段均能铺、可混接；相邻段无缝
- [ ] 可铺在墙/机器/货架下（不占格子）；同格叠放被拦并提示
- [ ] 端口绑定设备/存储建筑后按拍推送（`[ItemPort] pushed`）
- [ ] 管网连通则交付（`[PipeSeg] delivered`）；未连通/目标拒收有检视提示
- [ ] 目标仓满/筛选不收 → 反压滞留，**零物品丢失**
- [ ] 拆除管道段 → 管内货物落地回收（`[PipeSeg] dropped`）
- [ ] 存档读档后：管网重建、绑定目标、管内批次一致

---

## 7. 命令速查

```powershell
# 编译 / 测试 / 审计
& 'C:\dotnet8\dotnet.exe' build Source\PersonalChronicle.csproj -c Release /p:Platform=x64
& 'C:\dotnet8\dotnet.exe' test Tests\PersonalChronicle.Tests.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File '_scripts\audit_automation.ps1'

# 贴图（先备份后覆盖）
powershell -File '_scripts\gen_pipe_trench_sprites.ps1'     # 地下管沟 + 隐藏细线
powershell -File '_scripts\fit_pipe_tile_fill.ps1'          # 管径统一 / 边缘实心化

# 脚本含中文必须 UTF-8 BOM
powershell -File '_scripts\ensure_ps1_bom.ps1'

# 同步发布区（示例，白名单逐文件）
Copy-Item <dev>\Assemblies\PersonalChronicle.dll <rel>\Assemblies\ -Force
(Get-FileHash <src>).Hash -eq (Get-FileHash <dst>).Hash   # 必须 True
```

> ⚠️ 发布区：`E:\SteamLibrary\steamapps\common\RimWorld\Mods\PersonalChronicleVer0.1`
> 开发区：`e:\SteamLibrary\steamapps\common\RimWorld\11\PersonalChronicle` —— 二区不可混用。
