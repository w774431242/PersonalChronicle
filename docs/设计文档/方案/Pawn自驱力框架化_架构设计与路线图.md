# Pawn 自驱力框架化 —— 架构设计与路线图

> 定位：把「Pawn 自驱力」从 PersonalChronicle 的一个功能方案，升级为**可对外发布的独立框架 MOD**。
> 前置依据：`Pawn自驱力机制_原版与MOD支持性评估报告.md`（§6 建议架构 / §6.5 裁决）、`思维树设计范式研究报告.md`（P-19 / P-20 / P-46）。
> 本文所有对原版机制与生态现状的断言，**均引用上述两份已验证报告**，不另行臆造；未验证项统一标 ⚠️。

---

## 0. 决策快照

| 决策项 | 结论 | 影响 |
|---|---|---|
| 受众定位 | **公开框架**（发布创意工坊，供他人依赖） | 需 API 版本化与冻结、下游文档、长期维护、向后兼容承诺 |
| 与 PC 的关系 | **独立成新 MOD + 独立程序集**，PC 降级为**消费者** | 需独立目录 / `About` / `Defs` / `Languages` / `Assemblies`；PC 改为引用式依赖 |
| 工作中途抢占 | **延后决定** | 架构上必须让「触发时机」与「意图仲裁」**解耦**，使抢占成为后期**可追加的触发源**而非重写（见 §4.2） |

> ⚠️ 抢占未决不阻塞 Phase 1–4。§4.2 的解耦设计就是为它预留的；§11 记录延后带来的风险。

---

## 1. 定位与价值主张

### 1.1 为什么值得框架化（生态稀缺性论证）

来自两份已验证报告的量化事实：

1. **`insertTag` 槽位是生态最稀缺资源**（范式报告核心结论）。单是 `Humanlike_PostMain` 上已挂载巴尔的三棵树，`insertPriority` = **−8 / −12 / −20**。
2. **抢占地更紧张**：`Pawn_JobTracker.StartJob` 已被 **Common Sense + CAI 5000 双占**；而 `CheckForJobOverride` / `JobTrackerTickInterval` 在本机 **247 个 MOD 中零占用**（评估报告 §6.5）。
3. **`SubtreesByTag` 的求值语义**是「按 `insertPriority` 降序，第一个产出有效 job 者胜出」；高优先级节点若总产出无效 job，会逐级下探（评估报告 §6.5）。故**槽位本身不产生饿死，但产生持续的额外求值开销**。

结论：当前每个「想让 pawn 自主」的 MOD 都在**各自抢槽、各自 patch**。框架的差异化价值不是「我也能让 pawn 自主」，而是：

> **由框架独占一个 `insertTag` 槽位，并在需要时独占一个零争用的 Harmony 点；下游 MOD（含 PC）只向中央意图队列「投递驱动力」，由框架统一仲裁。**

即：**把多头竞争收敛为单一仲裁**。

### 1.2 框架做什么 / 不做什么（边界）

**做**：
- 驱动力（`Drive`）的注册、存储、生命周期
- 意图（`Intent`）的生成、排序、退避、过期、持久化
- 单一仲裁器：同一时刻为 pawn 选出「该做什么」
- 原版挂载：唯一 `insertTag` 槽位 + 唯一 `JobGiver`
- 对下游 drive 的**异常隔离**与降级
- 调试/观测接口

**不做**（留给下游或消费者）：
- 具体驱动力内容（吃什么、社交、娱乐……）——由下游 `DriveDef` / `IAutonomyDrive` 提供
- 玩家 UI 的经济/平衡数值
- 任何具体玩法内容（框架应保持内容中立）

**与 PC 现有 §6 设计的关系**：原 `PawnDrive`（`PersonalIntent` / `DriveState` / `JobGiver_Agenda`）方案**不废弃**，而是迁为框架的**首个参考实现 / 参考 drive**（§9 Phase 4）。其数据模型被框架吸收为 §3.2。

---

## 2. 分层架构

### 2.1 依赖方向（含新增 `Api` 层）

沿用工程既定铁律 `UI → Application → Domain → Data` **单向**，`Data` 不得依赖 `Application`。框架化后新增 `Api` 层：

```
下游 MOD  ──依赖──▶  Api（仅此一层，冻结契约）
                     │
                     ▼
AI(JobGiver) ─▶ Application/Engine ─▶ Domain ─▶ Data
                     ▲
                   UI（调试面板，可选）
```

- **`Api` 只依赖 `Domain`**（用于暴露模型类型），**不依赖 Application / AI / UI**——保证下游不会间接拖入实现。
- **下游 MOD 只引用 `Api`**，不得直接引用 `Engine` / `Data`。
- `Data`（`GameComponent`）**仅持数据与索引**，编排逻辑在 `Engine`（评估报告 §6.5 架构裁决：避免 Data 承载业务规则）。

### 2.2 独立 MOD 目录结构

```
<PawnAutonomyFramework>/
├─ About/                 About.xml, Preview.png, PublishedFileId.txt
├─ Defs/
│  ├─ AutonomyDriveDefs/   框架内置的示例/参考 drive（可为空）
│  └─ ThinkTrees/          Autonomy_PostMain.xml（insertTag 挂载）
├─ Languages/
│  ├─ English/Keyed/        
│  └─ ChineseSimplified/Keyed/
├─ Source/
│  ├─ Api/                 ★ 公开契约（冻结面）
│  ├─ Domain/              意图/驱动力/仲裁规则（纯规则）
│  ├─ Data/                GameComponent + Def 存储 + 索引
│  ├─ Engine/              心跳 + 仲裁 + 退避（原 Application 编排）
│  ├─ AI/                  唯一 JobGiver + 可选 ThinkNode
│  ├─ Compat/              软依赖探测 + （后期）抢占补丁
│  └─ Debug/               调试面板（可选，可裁剪）
├─ Assemblies/             输出（csproj 直输，仿 RUIF 子库先例）
└─ Source/*.csproj
```

### 2.3 程序集与先例

- 程序集输出**直落发布区**（`csproj` 的 `OutputPath` 指向 `Assemblies/`）——本工程 `RUIF.csproj` 已是此模式，无需手动复制。
- 框架**不引入** HugsLib / Prepatcher（评估报告 §6.5 裁决：现代做法用 Harmony + `Mod` + `Scribe` 足够，引入框架只会扩大依赖面）。

---

## 3. 核心抽象与公开 API

### 3.1 公开契约（`Api` 层，v1 冻结面）

```csharp
namespace PawnAutonomy.Api
{
    // 版本探测门面（对齐 RUIF.Api 的既有做法）
    public static class AutonomyApi
    {
        public static string VersionString { get; }          // "1.0.0"
        public static bool  IsAvailable { get; }             // 框架已加载且可用

        // 注册——两种扩展方式
        public static void RegisterDrive(AutonomyDriveDef def);  // XML 零代码
        public static void RegisterDrive(IAutonomyDrive drive);  // C# 复杂逻辑

        // 查询（只读，供调试/观测）
        public static AutonomyIntent GetTopIntent(Pawn pawn);
        public static IEnumerable<AutonomyIntent> GetActiveIntents(Pawn pawn);
    }

    // 复杂驱动力的扩展点
    public interface IAutonomyDrive
    {
        string  DefName { get; }                              // 唯一键
        float   GetUrgency(Pawn pawn, DriveContext ctx);      // 返回 0 表示本轮不参与
        bool    CanRun(Pawn pawn, out string blockerKey);     // blockerKey = 翻译键
        bool    TryMakeJob(Pawn pawn, AutonomyIntent intent, out Job job);
    }
}
```

> 关键约束：`GetUrgency` 会被**频繁调用**（树求值路径），必须**纯函数、无分配、无全库扫描**——此为范式 P-20 明确记载的代价。

### 3.2 `AutonomyIntent` 数据模型

吸收评估报告 §6.2（其结构已采纳巴尔 `BPC_PersonalAutonomy` 的成熟形态）：

```csharp
class AutonomyIntent : IExposable
{
    int    intentId, pawnId;
    string driveDefName;             // 生产者标识（用于回调 TryMakeJob）
    float  urgency;
    IntentState state;               // Planned / Acting / Completed / Blocked
    int    createdTick, expiresTick, notBeforeTick, attempts;
    string blockerKey;               // ★ 翻译键，不是自由文本（见下）
    // 目标：targetPawnId / targetThingId / targetCell
}
```

**与巴尔的一处刻意偏离**：巴尔的 `blocker` 是**自由字符串**。本工程硬约束「C# 出现中文即 `validate_mod.py` P1」+ 中英双语，故 `blockerKey` 必须是 **Keyed 翻译键**，由语言文件承担文案。下游 drive 同理。

### 3.3 扩展模型（两档）

| 档位 | 方式 | 适用 |
|---|---|---|
| **零代码** | 写 `AutonomyDriveDef`（XML），声明 urgency 曲线、条件、目标 job | 简单驱动力 |
| **需代码** | 实现 `IAutonomyDrive` 并 `AutonomyApi.RegisterDrive(...)` | 复杂/动态决策 |

两者进入**同一个仲裁队列**，框架不区别对待。

---

## 4. 仲裁引擎

### 4.1 仲裁顺序（沿用巴尔已验证排序）

直接采纳 P-19 记载的 `SelectRunnableIntent` 排序，作为 v1 起点：

```
排序键：urgency ↓ → createdTick ↑ → intentId ↑
并发上限：同一 pawn 同时最多 3 条意图
顶替规则：新意图须比队列中最弱者高 0.06 才顶替
```

> ⚠️ 上述阈值（3 条 / 0.06）来自巴尔的具体调参，**非普适定律**。v1 照抄作为安全起点，Phase 4（PC 接入）后按实测调整，并写入设置项。

### 4.2 ★ 触发时机与仲裁解耦（为延后的抢占预留）

这是本设计**最关键的结构决策**。

原版主树是**事件驱动**而非每 tick 常驻求值（P-46）：仅在「无 job / job 到期（带 `checkOverrideOnExpire`）/ 显式调用 `CheckForJobOverride()`」时求值；常数树每 **30 tick**，`Need` 相关每 **150 tick**。

因此：

- **「选什么意图」= 仲裁**（`Engine.Evaluate(Pawn)`）——纯函数式，与时机无关。
- **「何时求值」= 触发源**——独立可插拔：

| 触发源 | 能力 | 是否 Harmony | 阶段 |
|---|---|---|---|
| 思维树求值（`insertTag` + `JobGiver`） | 空闲期自主 | 否 | Phase 2（v1 自带） |
| `CheckForJobOverride` / `JobTrackerTickInterval` | **工作中途抢占** | 是（软依赖） | 后期追加（未决） |

**结论**：抢占若后期要做，只是**新增一个触发源**去调用同一个 `Engine.Evaluate(Pawn)`，**不动仲裁、不动 API**——不会返工。

配套三项门控（无论何时实现都必须满足）：
1. **能力探测**：Harmony 缺失时自动降级为「仅空闲期自主」，不报错、不崩溃。
2. **设置开关**：玩家/档位可关闭抢占（对齐评估报告 §6.3 的 `Off/Safe/Full` 思路）。
3. **单一持有**：抢占补丁**只由框架持有**，下游 MOD 不得自行 patch——这正是框架的价值所在。

### 4.3 退避、冷却与失败处理

- 执行失败 → `attempts++`、`notBeforeTick` 按退避递增（避免同一意图刷屏）。
- `CanRun` 返回 false → 记 `blockerKey`，可转为「请求玩家」信号（原 §6.3 P3 的 `AssistRequestService` 思路，属于消费者/下游职责，框架只暴露 blocker）。
- 意图过期（`expiresTick`）→ 回收。

---

## 5. 原版接入（挂载）

### 5.1 `insertTag` 单槽策略

- 挂载点：`Humanlike_PostMain`（框架**只占这一个槽**）。
- `insertPriority` 选型（评估报告 §6.5 硬约束）：
  - **绝对不要用 `0` 或整数平手值**；
  - 该 tag 上已有巴尔 **−8 / −12 / −20**；
  - 建议取**唯一且不重叠**的负值（如 **−15**），并在文档中登记，便于他家避让。
- 求值语义已确认：降序 + 「首个有效 job 胜出」+ 高优先级持续无效则下探 → **不会饿死他家**，但会增加求值开销（故槽位越少越好，框架单槽是优点）。

### 5.2 唯一 `JobGiver`（P-19：树退化为取货口）

```csharp
class JobGiver_Autonomy : ThinkNode_JobGiver
{
    protected override Job TryGiveJob(Pawn pawn)
    {
        var intent = Engine.Evaluate(pawn);      // 唯一的仲裁调用
        if (intent == null) return null;
        return Engine.MakeJob(pawn, intent);     // 回调生产者的 TryMakeJob
    }
}
```

- 决策逻辑**全部在 `Engine`/`Domain`**，此节点不含任何规则（评估报告 §6.5：继承 `ThinkNode_JobGiver` 是官方设计意图，但逻辑必须组合进 Domain 服务）。
- 全框架**只此一个 JobGiver** → 与生态其他树 MOD 的冲突面最小。

### 5.3 可选动态优先级（P-20）与性能约束

若「同一 JobGiver 在不同情境下该不该抢先」无法用 XML 常量表达，可覆写 `GetPriority(pawn)` 实时计算。
⚠️ 硬约束：**该方法被频繁调用，必须纯函数、无分配、无扫描**（P-20 明确代价）。v1 建议**先不做**，留作性能实测后的优化项。

---

## 6. 兼容性设计（公开框架的重点）

### 6.1 槽位与 priority 约定

- 框架自称「槽位托管方」，在文档中**公示所占 tag 与 priority 值**，供他家避让。
- 下游 MOD **不得**自行挂 `insertTag` 来投递驱动力——必须走 `AutonomyApi`。

### 6.2 下游 drive 异常隔离（框架必需）

公开框架会接到质量参差的下游 drive。必须：

- **逐个 drive 包 try/catch**：单个 drive 抛异常**只跳过该 drive**，不得中断整个仲裁循环。
- 异常需**节流记录**（避免每 tick 刷日志——同时符合 GATE-PERF「日志不在热路径无界增长」）。
- 首个异常后可将该 drive 标记为「本局禁用」并记 `blockerKey`。

> 依据：评估报告 §6.5 降级策略已确认原版 `ThinkNode_Priority.TryIssueJobPackage` 自带 try/catch（异常被记录后跳过，不影响核心）；框架需在其之上做**更细粒度的 per-drive 隔离**。

### 6.3 依赖声明

- **零硬依赖**：框架核心不引用任何第三方类型，仅原版 `ThinkNode*` / `GameComponent`（评估报告 §6.5 GATE-COMP 裁决 PASS）。
- **Harmony 为软依赖**（仅抢占用到）：能力探测，缺失即降级，不声明硬依赖。
- `insertTag` 匹配发生在**首次求值**时扫 `DefDatabase.AllDefs`（此时全部 MOD 已加载完），故**无需 `loadAfter`**（§6.5 已裁决）。

### 6.4 命名空间 / defPrefix 唯一性

公开框架必须避免与他人 Def 冲突：
- 命名空间、`defPrefix`、`modTarget` 相关命名需**选唯一前缀**（见 §10 命名提案）。
- 内置 Def 一律带该前缀。

### 6.5 与既有自主性 MOD 共存

- 与巴尔 `BPC_PersonalAutonomy` 等**是竞争关系也是互补**：它们各自占槽，框架另占一槽并给出统一仲裁入口。
- 框架的长期价值在于「若他家愿意改为向框架投递，则全生态收敛为单槽」——但 **v1 不追求此目标**，先保证自身可独立工作、不干扰他家。

---

## 7. 数据持久化与版本

- 持有者：`Data/GameComponent_Autonomy`（`Current.Game.GetComponent<T>()`）。
- **禁止 static mutable**（跨存档泄漏、不同步）——§6.5 已裁决，沿用 PC 既有 `GameComponent` 惯例。
- **schema 版本化**：`AutonomyIntent` / 状态需带版本号，旧档可加载，新字段有默认值，迁移**幂等可诊断**（GATE-SAVE）。
- **中途加入存档兜底**：`FinalizeInit` 末尾回填（PC 已有 `BackfillExistingColonists` 先例，§6.5 已确认此模式）。

---

## 8. 工程规范符合性

| 约束 | 本方案符合性 |
|---|---|
| 分层单向 `UI→App→Domain→Data`；`Data` 不依赖 `Application` | §2.1 已满足；`Api` 仅依赖 `Domain` |
| 用户可见文案零硬编码（P1 红线） | 全部走 Keyed XML；`blockerKey` 为翻译键（§3.2 对巴尔的刻意偏离） |
| 中英双语 | `Languages/{English,ChineseSimplified}/Keyed/` |
| 禁止 static mutable 全局状态 | `GameComponent` 持有（§7） |
| 单一职责 / 防硬编码 | 驱动力由 `DriveDef` 数据驱动，非枚举硬编码（§6.4 of 评估报告） |
| 性能：热路径无分配/无扫描 | `GetUrgency` / `GetPriority` 纯函数约束（§3.1 / §5.3） |
| 不引入 HugsLib / Prepatcher | §2.3（§6.5 裁决） |

---

## 9. 路线图

| 阶段 | 目标 | 关键交付物 | 验收 |
|---|---|---|---|
| **P0 决策** | 定名/命名空间/defPrefix、仓库与工坊占位 | 命名定稿 | 本文 §10 确认 |
| **P1 骨架 + spike** | 独立 MOD 目录、`About`、csproj（输出直落 `Assemblies/`）；**实机验证 `insertTag` 单槽挂载 + 单 JobGiver 跑通** | 可加载的空框架 | 游戏内无报错；⚠️ 顺带补掉评估报告 §7 待办第 1 项（实机打印 `matchedTrees` 顺序，此前**从未实测**） |
| **P2 核心引擎** | `Domain`+`Data`+`Engine`：`DriveDef`、仲裁（§4.1）、持久化（§7） | 空闲期自驱可用 | 单 pawn 多 drive 排序正确；存读档无损 |
| **P3 API 冻结 1.0** | 公开契约定稿 + **下游文档** + 双语 Keyed + `AutonomyApi.VersionString` | v1.0.0 | API 评审；文档可照抄接入 |
| **P4 PC 接入（消费者）** | PC 改为引用框架；原 `PawnDrive` 逻辑迁为**参考 drive** | PC 无回归 | PC 编译 0 错 0 警；旧行为保持 |
| **P5 抢占（可选，未决）** | 追加触发源（§4.2）+ Harmony 软依赖 + 开关 | 工作中途自主 | 降级路径可测（移除 Harmony 仍正常） |
| **P6 调试 + 发布** | 调试面板、工坊发布、版本与迁移说明 | 已发布 | `validate_mod.py` 通过（唯一已知 WARN 为 MainButtonDef 原版类型误报） |

> **P1–P4 零 Harmony 可达**：与评估报告 §6.3 的 P1–P3 一致。抢占（P5）是唯一需要 Harmony 的项，且已按 §4.2 解耦，延后不影响 P1–P4 交付。

---

## 10. 命名提案（待确认）

| 项 | 提案 | 说明 |
|---|---|---|
| MOD 名 | **Pawn Autonomy Framework** | 「Autonomy」与生态既有术语一致（巴尔 `BPC_PersonalAutonomy`） |
| 程序集 | `PawnAutonomy.dll` | |
| 命名空间 | `PawnAutonomy`（`PawnAutonomy.Api` 为公开面） | |
| Def 前缀 | `PAF_` | 需确认不与生态他家冲突（§6.4） |

> 命名一旦随 P3 冻结即难更改，**建议在 P1 前定稿**。

---

## 11. 风险与未决项

### 11.1 未决/延后

1. **工作中途抢占是否纳入**（用户决定「后期再看」）。已按 §4.2 解耦，追加成本可控；但若最终要做，建议在 **P3 API 冻结之前**决策——因为它可能需要新增「触发源注册」类 API（虽不影响既有契约）。
2. **`insertTag` 求值顺序从未实机验证**（评估报告 §7 待办 1）：P1 spike 必须补上。

### 11.2 风险

| 风险 | 缓解 |
|---|---|
| API 冻结后重构成本高 | P1–P2 快速迭代；P3 前完成充分 spike（含 P1 的实机验证） |
| 下游 drive 质量参差拖垮仲裁 | §6.2 per-drive 异常隔离 + 节流日志 |
| 抢占的 Harmony 与未来生态冲突 | 选 `CheckForJobOverride` / `JobTrackerTickInterval`（当前 247 MOD 零占用）；**避开** `StartJob`（已被双占） |
| Def/命名空间与他人冲突 | §6.4 唯一前缀；P0 定名时核查 |
| 公开框架的文档与维护负担 | P3 起必须同步维护下游文档；纳入版本说明 |

### 11.3 已知环境问题（非本设计缺陷）

`.codebuddy/agents/` 的 `pc-*` Gate agent 在本环境**无法启动**（其 frontmatter 声明的 `read_file` / `search_content` / `search_file` / `list_dir` 无法解析）。故本设计的 GATE-ARCH / GATE-COMP 裁决由本文**自行给出**（§6.3 / §8 对照既有工程约束），与评估报告 §6.5 的处理方式一致。**建议修复 agent frontmatter 的工具名**，否则后续 P3 的 API 评审无法走正规门禁。

---

## 12. 下一步（待批准）

按工程规范「跨模块修改需审批」，在动工前请确认：

1. **§10 命名**是否定稿（尤其是 Def 前缀 `PAF_`）。
2. 是否批准进入 **P1 骨架 + spike** —— 将新建独立 MOD 目录、`About/`、csproj，并做 `insertTag` 单槽实机验证。
3. **§11.1 的抢占决策**是否需要在 P3 之前补做（若需要，我会在 P1 spike 中一并验证 `CheckForJobOverride` 触发路径）。

---

*本文档为设计/规划产物，尚未产生任何代码改动。所有对原版与生态的断言均引用 `Pawn自驱力机制_原版与MOD支持性评估报告.md` 与 `思维树设计范式研究报告.md`。*
