# 08b ArchiveService.cs 功能识别与验收清单

**版本：** v1.0（2026-08-15）  
**上位：** [08-架构层-代码轻量化方案.md](08-架构层-代码轻量化方案.md) §2.5（排序→识别→剥离）、§3.2（方法级映射）  
**目标文件：** `Source/PersonalChronicle/Application/ArchiveService.cs`（3517 行 / 111 方法 / sealed class，非 partial）

---

## 1. 功能识别（Step 2 成果）

按 §2.5 方法，从 111 个方法中识别出 **8 个独立功能单元**。识别依据：方法语义聚类 + 文件内 `// ----` 注释块边界（71 live colonist cache / 562 work-intensity / 1424 live query helpers）+ 接口归属。

| # | 功能单元 | 接口归属 | 方法数 | 行数区间 | 说明 |
|---|---|---|---|---|---|
| F1 | **记录/事件基础读写** | `IArchiveQueryService` / `IArchiveEventSink` | 17 | 116–450, 3399–3477 | 构造 + 记录/事件查询入口 + 事件写入（RecordEvent/TryRecord/ToChronicleEvent）+ 服务日/视图模式 |
| F2 | **工时强度** | `IWorkIntensityService` / `IWorkTimeCaptureService` | 21 | 475–1058 | 工时统计、强度档位、殖民地聚合、技能档案 |
| F3 | **生产** | `IArchiveService` | 5 | 1118–2125 | 生产汇总、按类目聚合、生产事件、制造捕获 |
| F4 | **损耗** | `IArchiveService` | 3 | 1204–2230 | 损耗汇总、消耗捕获、工作场所使用 |
| F5 | **战役** | `IArchiveService` | 28 | 1378–3143 | 战役查询/击杀/参战/物品事件/击杀去重 |
| F6 | **社交关系** | `IArchiveService` | 5 | 1523–1758 | 加入/死亡/关系变更/快照 |
| F7 | **地点/持有者** | `IArchiveService` | 7 | 1309–2573 | 实时地点、持有者、世界坐标、物品销毁/建造 |
| F8 | **Live 实时缓存与别名** | `IArchiveService`（查询侧） | 25 | 240–384, 2284–2470, 3176–3282 | live pawn 缓存、live colonist 计数、别名读写、事件写入辅助 |

> 合计 111 方法（17+21+5+3+28+5+7+25）。与各接口方法签名**一一对应，拆分不得改签名**。

---

## 2. 字段归属（搬移前必须核对）

主文件字段（行 33–114）按功能归类，搬移时字段声明留主文件（partial 共享），仅搬方法：

| 字段 | 行 | 归属功能 |
|---|---|---|
| `LivePawnCacheWindow` / `livePawnCache` / `livePawnCacheTick` / `cacheGameComponent` | 34/62–69 | F8 Live |
| `LiveCountCacheWindow` / `liveCountCacheComponent` / `cachedLiveColonistCount` 等 5 个 | 37/78–93 | F8 Live |
| `workIntensityProviders` / `workIntensityPolicy` / `workIntensityCache*` / `cachedColonyWorkAggregate` / `warnedIntensityProviders` | 96–114 | F2 Work |
| `ThingIdPrefix` / `raidLordToBattle` | 44/54 | F1/F5 共用（留主文件） |
| 静态 `Component` 属性(126) | 126 | 全局（留主文件） |

---

## 3. 验收清单（Phase 3 落地的逐条 Check）

### 3.1 编译与测试
- [ ] `dotnet build Source\PersonalChronicle.csproj -c Release` → **0 错误 0 警告**
- [ ] `dotnet test Tests\PersonalChronicle.Tests.csproj -c Release` → **全部通过**
- [ ] 主文件类声明改为 `public sealed partial class ArchiveService`（implements 5 接口不变）

### 3.2 文件产出（8 文件，对照 §3.2 映射）
- [ ] `ArchiveService.cs`（主）：F1 构造+基础读写+服务日/视图模式 + 全部字段声明 + 静态 `Component`
- [ ] `ArchiveService.Work.cs`：F2 全部 21 方法
- [ ] `ArchiveService.Production.cs`：F3 全部 5 方法
- [ ] `ArchiveService.Consumption.cs`：F4 全部 3 方法
- [ ] `ArchiveService.Battle.cs`：F5 全部 28 方法
- [ ] `ArchiveService.Social.cs`：F6 全部 5 方法
- [ ] `ArchiveService.Location.cs`：F7 全部 7 方法
- [ ] `ArchiveService.Live.cs`：F8 全部 25 方法

### 3.3 契约零变更
- [ ] 5 个接口（`IArchiveService`/`IWorkIntensityService`/`IWorkTimeCaptureService`/`IArchiveQueryService`/`IArchiveEventSink`）方法签名逐一比对无改动
- [ ] 公共方法名/参数/返回类型/可访问性（public）无变化（grep 校验方法数 111 不变）
- [ ] 翻译键计数无变化（`grep -c "UI\.` 前后一致）

### 3.4 跨功能依赖（partial 天然合法，仅记录不修改）
- [ ] F6 Social → F8 Live：`EnsurePawnArchivedForCapture` 由 join/death 链路调用（同 class，无需改）
- [ ] F5 Battle → F8 Live：`GetLivePawn` / `FindLivePawnByUniqueLoadId` 在战斗查询使用
- [ ] F8 Live → F2 Work：`GetWorkIntensity` provider 注册表在构造器注入
- [ ] F1 写入 → F8 Live：`RecordEvent` / `ToChronicleEvent` 在 Live 的事件辅助中被引用

### 3.5 运行时抽查（游戏内）
- [ ] 主菜单读档后档案馆首页正常（live colonist 计数来自 F8 缓存）
- [ ] 打开某殖民地成员详情：六宫格工时/生产/击杀/损耗/足迹/传承均正常（F2–F7）
- [ ] 制造/消耗/战斗事件在游戏内实时出现（F3/F4/F5 写入链路）
- [ ] 重命名建筑/房间类型后别名持久化（F8 别名读写）

### 3.6 diff 审查
- [ ] `git diff --stat` 仅显示「删行 + 8 个新增 partial 文件」
- [ ] 无任何逻辑行改动（无 `if`/`for`/字段初始化变更）
- [ ] 发布区仅复制 `Assemblies\PersonalChronicle.dll`，重启游戏生效

---

## 4. 风险标注

| 风险 | 等级 | 缓释 |
|---|---|---|
| 字段漏搬导致某 partial 编译找不到符号 | 低 | 3.1 编译即暴露；字段统一留主文件 |
| 接口方法被误改签名 | 中 | 3.3 逐接口签名比对 |
| 跨 partial 调用误加访问级别 | 低 | partial 同类成员天然可见，3.4 仅记录 |

---

## 5. 功能数量结论

**ArchiveService.cs 识别出 8 个独立功能单元**（F1–F8），对应 8 个目标文件（主 + 7 partial）。拆分后单文件最大约 800 行（F5 Battle），最小约 280 行（F4 Consumption），均 ≤ 800 行验收上限。
