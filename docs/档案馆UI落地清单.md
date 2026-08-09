# 档案馆 UI 规格落地清单（Personal Chronicle）

> 对照文件：`D:/AAA下载/Personal_Archive_UI_Spec.txt`（v2.1 UI 信息架构预览）
> 生成方式：环世界模组工程专家团 SOP（需求解析 → 系统/兼容并行 → 质量审计 → 汇总裁决）
> 日期：2026-08-08 ｜ 基线：RimWorld 1.6.4871，用户环境 221 mod

---

## ⚡ 执行状态（2026-08-08 14:51 更新）

**批次 A/B/C/D + 治理已全部落地，最终编译 0 错误 0 警告，DLL 已产出。**

| 任务 | 状态 | 交付要点 |
|---|---|---|
| L0-1~L0-5 | ✅ 完成 | ChronicleEventType 常量类+启动校验；EventKind 枚举；behavior 接线（Battle/Location→StatOnly）；killer 翻译映射；GetLivePawn 缓存（thingIDNumber int 键 + 120 tick） |
| LA-1~LA-5 | ✅ 完成 | Domain 四类只增字段 + WorkPrioritySnapshot；Scribe 序列化链验证通过（组件无需补改，旧档兼容） |
| LB-1 | ✅ 完成 | 5 方法（GetWorkPriorities/GetLiveLocation/GetCurrentHolder/GetActiveBattle/GetProductionEvents）+ 3 显示类型，1.6 API 实测 |
| LC-1~LC-5 | ✅ 完成 | Kill 防御（`!__instance.Dead` 修正）；DlcStatus（ModsConfig.AnomalyActive + Pawn.IsSubhuman）；GenRecipe 反射探测；IncidentWorker 读 `__result`；**LC-2 死亡关联战役（Battle Subject 边，683-686 行）** |
| LD-1~LD-7 | ✅ 完成 | Production 聚合表；Work/Places/Holders 活读 Tab；Weapon Combat 击杀列表；**LD-6 参战战役列表（TargetEvent 贯通导航）**；Def 缺失防御 |
| LE-2a/2b/3a | ✅ 完成 | ArchiveCategoryKeys 独立文件；static 只读暴露；Slider 常量；DrawStats 合并；CollectCandidates 单次扫描；健康阈值常量；CategoryCount 格式 key |
| LE-2c | ✅ 完成 | GetEventsFor 改 ReadOnlyCollection（调用方排查全安全）；P3-7 索引经评估跳过（数据量小收益不匹配） |
| LE-1（god class 拆分） | ⏸ 待定 | 深度重构（显示模型下沉 + partial 拆分），建议独立批次执行 |

**遗留说明**：LE-1（god class 拆分）未执行——属架构级重构，与功能交付解耦，建议单独排期。

---

## 〇、规格对照结论（已实现 / 差距）

规格描述的三栏骨架、5 主视图（home/overview/pawn/weapon/event）、人物 11 Tab + 武器 6 Tab、关联跳转（data-v ↔ NavTarget）、全部呈现组件（统计格/事件行/卡片/详情头/信息格/时间轴/chip/表格/技能条/关系行/数字格/事件关联树）**已在 `ArchiveMainTabWindow.cs`（约 2480 行）中实现**。数据层（多态 ArchiveObject、ChronicleEvent 边模型、IArchiveService、ChronicleEventDef/ArchiveCategoryDef 数据驱动）也已就绪。

**真实差距（规格有、代码未落地）：**

| 差距项 | 性质 | 根因 |
|---|---|---|
| 人物「工作/生产/地点」、武器「持有者」4 Tab | Placeholder | 生产=仅缺 UI 聚合；工作/持有者=缺活读+快照降级；地点=LocationObject 零采集 |
| 武器「战斗」击杀列表为空 | Partial | 数据已采集（killer weapon 已是死亡事件 Subjects 边），UI 只扫 Primary 视角 |
| 人物「战斗」无参战战役反查 | Partial | Battle 事件 Subjects 为空，BattleObject 无参战字段 |
| Battle/Location 深度差异化 | 假数据驱动 | ArchiveCategoryDef.behavior 死 API，UI 硬编码「仅 Pawn/Thing 可进详情」 |

---

## 一、专家产出摘要（三份）

| 专家 | 产出 | 核心结论 |
|---|---|---|
| 容万合（兼容） | 8 条任务 T1-T8 | **无 P0/P1**。5 个 patch 全 read-only + try-catch 降级。P2 优先：Patch_PawnKill 缺 Dead 校验、Patch_GenRecipe private static 签名脆弱、Anomaly subhuman backfill 门控、存档第三方 Def 缺失防御 |
| 董源码（系统） | 15 条任务 T1-T15 + 依赖 DAG | Production 数据已齐只缺 UI；Places 根因 LocationObject 零采集；Combat 缺 Battle Subjects 边。全部「只增字段」向后兼容 |
| 程检规（质量） | P0=0 / P1=0 / P2=6 / P3=7 / P4=6 | 无阻断。P2 集中「UI 层业务逻辑上浮」（TypeKey 子串分类、behavior 死 API、每帧 GetLivePawn 扫描、DefDatabase 直查、killer key 泄漏、TypeKey 双份维护）+ god class |

---

## 二、最终落地清单（主理人合并裁决后）

> 编号规则：`L0-` 合规修复（先做）→ `LA-` 数据模型 → `LB-` 服务层 → `LC-` 采集层 → `LD-` UI 落地 → `LE-` 工程治理。
> 每个任务含：源编号、模块、依赖、验收标准。**裁决已消解三份清单的冲突**（见各任务备注）。

### 阶段 0：合规修复（P2 收口，先做，多数不依赖系统任务）

| # | 任务 | 源 | 模块 | 依赖 | 验收标准 |
|---|---|---|---|---|---|
| L0-1 | 事件 TypeKey 单一来源 + 启动校验 | 审计 P2-6 | Application | 无 | 收敛 `ChronicleEventType` 常量类；[StaticConstructorOnStartup] 校验 TypeKey 均存在 Def，漂移即 Log.Error |
| L0-2 | ChronicleEventDef 增 `EventKind` 枚举，UI 去子串分类 | 审计 P2-1 | Domain + UI | 无 | `IsDeathEvent/IsBattleEvent/IsCraftEvent` 改经 Def 读取；UI 零 `TypeKey.IndexOf`；新增事件类型只改 XML |
| L0-3 | ArchiveCategoryDef.behavior 接线（激活死 API） | 审计 P2-2 | UI + Defs | 无 | Overview 可点击改由 `GetCategoryBehavior` 驱动；Battle/Location 改 behavior=StatOnly 与意图一致 |
| L0-4 | killer key 翻译映射 + 常量收敛 | 审计 P2-5/P3-1 | UI + Capture | L0-1 | 时间轴/事件树无原始英文数据 key；`ChronicleEventParams.Killer` 共享常量 |
| L0-5 | GetLivePawn 缓存（解每帧扫描） | 审计 P2-3 | Application | 无 | 三 Tab 打开时 GetUniqueLoadID 调用从「每帧×N」降到「每 2s×N」；与窗口 120 tick 刷新同节奏 |

### 阶段 A：数据模型扩展（只增字段，Scribe 向后兼容）

| # | 任务 | 源 | 模块 | 依赖 | 验收标准 |
|---|---|---|---|---|---|
| LA-1 | PawnObject 增 WorkSnapshot/SkillSnapshot | 系统 T1 | Domain | 无 | 字段+Scribe Look+判空；旧档加载不报错 |
| LA-2 | ThingObject 增 CurrentHolderId([Unsaved]) + 可选 HolderHistory | 系统 T2 | Domain | 无 | 同上 |
| LA-3 | LocationObject 增 WorldTile/MapDefName | 系统 T3 | Domain | 无 | 同上；稳定身份不照抄 Battle 的 `defName@tick` 弱身份 |
| LA-4 | BattleObject 增 ParticipantIds（可选） | 系统 T4 | Domain | 无 | 同上 |
| LA-5 | ChronicleGameComponent Scribe 持久化新字段 | 系统 T5 | Data | LA-1~LA-4 | 存档往返一致；旧档 SchemaVersion 不变也能加载 |

### 阶段 B：服务层追加（append-only，不破坏现有签名）

| # | 任务 | 源 | 模块 | 依赖 | 验收标准 |
|---|---|---|---|---|---|
| LB-1 | IArchiveService 追加 5 方法：GetWorkPriorities / GetLiveLocation / GetCurrentHolder / GetActiveBattle / GetProductionEvents | 系统 T6 | Application | LA-5 | 现有调用方零改动；越界返回 null/空列表；返回快照不暴露内部 List（对齐 P3-5） |

### 阶段 C：采集层扩展（Harmony 语义最小增量）

| # | 任务 | 源 | 模块 | 依赖 | 验收标准 |
|---|---|---|---|---|---|
| LC-1 | Patch_PawnKill 加固：`__instance.Dead` 校验 + HarmonyAfter 清单集中管理 | 兼容 T3 | Capture | 无 | 其他 mod 跳过 Kill 时不误记；221 mod 排序声明可配置化 |
| LC-2 | OnPawnDied 关联进行中战役（Battle 作为 Subjects 边） | 系统 T7 | Capture | LB-1(GetActiveBattle), LA-4 | 战役中死亡的 pawn 能反查到 Battle |
| LC-3 | DlcStatus + Anomaly subhuman 门控（world-pawn backfill 路径） | 兼容 T2 | Application + Data | 无 | Anomaly 启用时 subhuman 不建档；未启用行为不变；探测结果缓存 |
| LC-4 | Patch_GenRecipe 反射目标探测 + 运行期 patched 校验 | 兼容 T4 | Capture | L0-1 | 签名变更静默失效而非抛错；1.6/1.7 均可加载 |
| LC-5 | Patch_IncidentWorker 读 `__result`（TryExecute=false 不记 Battle） | 兼容 T5 | Capture | 无 | 事件被拒时不污染档案 |
| LC-6 | Craft 事件 Params 补质量/材料（增强，可选） | 系统 T8 | Capture | LA-5 | 新 Craft 事件 Params 含质量/材料 key |

### 阶段 D：UI 落地（4 Placeholder Tab + 2 Combat 修复）

| # | 任务 | 源 | 模块 | 依赖 | 验收标准 |
|---|---|---|---|---|---|
| LD-1 | Production Tab（按 ThingDef 聚合 Crafted/Built 表） | 系统 T11 | UI + Application | **L0-2**（聚合逻辑下沉，禁窗口内 TypeKey 子串） | 表含物品类型/数量/最近时间；点击跳武器详情 |
| LD-2 | Work Tab（优先级表+当前工作，活读+快照降级） | 系统 T10 | UI | LB-1, L0-5 | 活 pawn 显示 workSettings；死 pawn 显示快照；无数据占位 |
| LD-3 | Places Tab（当前位置信息格，Phase A 活读） | 系统 T12 | UI | LB-1, L0-5 | 显示地图/商队/世界位置；无数据占位 |
| LD-4 | Holders Tab（当前持有者，Phase A 活读） | 系统 T13 | UI | LB-1, L0-5 | 显示当前持有 pawn；无持有者占位 |
| LD-5 | Weapon Combat 补「本武器为 Subject 的击杀列表」 | 系统 T14 | UI | **L0-2**（经 EventKind 过滤，禁子串） | 武器详情战斗 Tab 列出用该武器击杀的记录 |
| LD-6 | Pawn Combat 补「参战战役列表」 | 系统 T15 | UI | LC-2 | pawn 战斗 Tab 列出参与的 Battle 事件并跳转 |
| LD-7 | 存档第三方 Def 缺失防御（GetNamedSilentFail + 占位 + debug 日志） | 兼容 T8 | UI + Application | 无 | 卸载 Medieval Overhaul 等后 UI 不崩不红字 |

### 阶段 E：工程治理（god class + P3/P4）

| # | 任务 | 源 | 模块 | 依赖 | 验收标准 |
|---|---|---|---|---|---|
| LE-1 | god class A+C 拆分：业务缓存构建下沉 Application 显示模型层 + partial 文件拆分 | 审计（任务1） | Archive + Application | L0-2, L0-4 | 单文件 ≤800 行；UI 无业务分类/聚合；新增视图只动 Archive 层 |
| LE-2 | P3 批量：DrawStats 合并 / ArchiveCategoryKeys 独立文件 / static 只读暴露 / GetEventsFor 防御拷贝 / CollectCandidates 单次扫描 / category 索引 | 审计 P3-2~P3-7 | 全层 | 随各阶段 | 逐项按验收执行 |
| LE-3 | P4 批量：健康阈值常量 / Slider 常量 / CategoryCount 格式 key / 缺 Def debug 日志 | 审计 P4-1~P4-6 | UI | 无 | 逐项按验收执行 |

---

## 三、依赖 DAG 与并行批次

```
[L0-2 EventKind] ──► [LD-1 Production] ─┐
                 └──► [LD-5 WeaponCombat] ┘
[L0-5 GetLivePawn 缓存] ──► [LD-2/3/4 活读 Tab]
[LA-1~4 字段] ──► [LA-5 Scribe] ──► [LB-1 服务方法] ──► [LC-2 战役关联] ──► [LD-6 PawnCombat]
[L0-1 TypeKey] ──► [LC-4 反射探测]
```

**建议并行批次：**
- **批次 A（立即开工，零依赖）**：L0-1, L0-2, L0-3, L0-4, L0-5, LC-1, LC-3, LD-7 —— 合规修复 + 采集加固并行
- **批次 B（A 完成后）**：LA-1~4（并行）→ LA-5 → LB-1；同时 LD-1/LD-5 可随 L0-2 落地即做
- **批次 C（B 完成后）**：LC-2 → LD-6；LD-2/3/4 依赖 L0-5 + LB-1
- **批次 D（收尾）**：LE-1（依赖多阶段完成，最后做）

---

## 四、P0-P4 遗留裁决

| 等级 | 遗留 | 裁决 |
|---|---|---|
| P0 | 无 | — |
| P1 | 无 | — |
| P2 | 6 项（L0-1~L0-5 + god class） | **必须收口**，阶段 0 + 阶段 E |
| P3 | 7 项 | 应修改，随阶段顺带（LE-2） |
| P4 | 6 项 | 可选（LE-3） |

## 五、风险与回滚

- **存档兼容**：全部数据模型扩展为「只增字段 + 默认值」，旧档无损；TypeKey/类名/命名空间严禁改动（Scribe 存档身份）。
- **Harmony 风险**：LC-1/LC-2 为 read-only Postfix 增量，失败 Log.Warning 降级；历史轨迹/历任持有者的 Level 5 Harmony（Pawn_EquipmentTracker / 地图切换）**明确推迟**（跨 Mod 风险与收益不成比例）。
- **卸载友好**：仅依赖 Harmony；第三方 mod 移除后正常加载，唯一影响由 LD-7 兜底。
- **回滚**：每阶段独立提交；L0 系列不依赖系统任务，可单独回退。
