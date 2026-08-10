# 性能技术检测报告（2026-08-10）

> 依据《高拓展高接入架构方案.md》§11 性能预算清单（热路径禁忌 / 输入保护 / 失败隔离），对 `Source/PersonalChronicle` 做静态核查 + 运行期 Player.log 交叉验证 + Release 编译。
> 本次修复：运行期 Def 配置错误（`HealthValuation.xml`），编译 `dotnet build -c Release` **0 错 0 警**。

## 一、运行期真实问题（已修）

### 🔴 Def 字段越界 → 健康残值惩罚静默失效
- **位置**：`Defs/HealthValuation.xml` 的 `<penalties>` 列表，7 个 `<li>` 均含 `<label>哮喘</label>` 等节点。
- **根因**：`HealthPenaltyDef`（`Domain/HealthValuationDef.cs`）是**内联数据类**（非 `Verse.Def`），只有 `defName / hediffDefName / penaltyFraction / labelKey` 字段，**没有 `label`**。RimWorld XML 反序列化遇到未知字段会报 `doesn't correspond to any field in type ...HealthPenaltyDef` 并**跳过该条**，导致 7 条 penalty 全部进入 `IgnoredNodes` 被丢弃 → `penalties` 列表为空 → 健康残值三维扣分完全不生效。
- **Player.log 证据**：加载期 7 条 XML error（每个 hediff 一条）。
- **修复**：删除 7 个 `<label>` 节点，仅保留 `defName/hediffDefName/penaltyFraction/labelKey`。显示名仍由 `labelKey` 经翻译键提供，无功能损失。

## 二、热路径核查（方案 §11）

| 热路径 | 判定 | 证据 |
|---|---|---|
| **GameComponentTick**（`Data/ChronicleGameComponent.cs:189` 起） | ✅ 通过 | 采样受 `WorkSampleIntervalTicks=120` 节流；对账受 `ReconcileIntervalTicks=600` 节流；节流分支外仅做 revision 比较，无全量枚举/分配。 |
| **Capture Patch**（Capture/ 9 个） | ✅ 通过 | 均为无返回值 prefix/postfix，无堆分配；`Patch_PawnTakeDamage` 助攻数据经批量合并 + 节流，且调用方包 `try/catch`（失败隔离，单条异常不污染游戏 tick）。 |
| **每帧 Draw**（`ArchiveMainTabWindow.cs` 全部 `Draw*`） | ✅ 通过（经复核） | 排序/null-guard 已全部下沉至 `ArchiveUiDataProvider`（Read Model），Draw 仅消费 `cached*` 字段（如 `cachedTimelineEvents`/`cachedWorkIntensity`）与配置型只读列表（`GetIntensityTiers()` 返回 Def 配置，无每帧分配）。**注**：初步排查曾误报"3 个 Draw 阻断点（每帧 new List + OrderBy）"，经源码复核不成立——排序已在缓存重建期（`RefreshNow`）完成，Draw 内无 `OrderBy`/`new List`。 |
| **缓存/脏标记** | ✅ 通过 | 窗口重建由 `CacheRefreshInterval=120`（tick 节流，约 2s）权威 gate，注释明确不依赖每 2s bump 的 `DataRevision`（避免每 tick 全量重建）；工时聚合 `GetColonyWorkAggregate` 另有 `WorkIntensityCacheWindow` 时间窗双重判脏。 |
| **失败隔离** | ✅ 通过 | `ArchiveProviderRegistry.ForEach` try/catch + per-ProviderId 去重警告；Capture 调用链包 try/catch。 |
| **输入保护** | ✅ 通过 | 采集点均对 `pawn/thing/def` 做 null 与 `Destroyed` 校验；`RecordEvent` 入口对 `ArchiveEventInput` 做空值/字段合法性校验。 |

## 三、遗留低风险项（非阻塞，建议后续）

| # | 项 | 风险 | 建议 |
|---|---|---|---|
| L1 | `DrawWorkIntensityHeader` 在 Draw 内调用 `service.GetIntensityTiers()`（`:3443`） | 低 | 返回 Def 配置列表，无分配；可缓存为 `cachedTiers` 字段进一步去直读，但非必须。 |
| L2 | `DrawWorkIntensityHeader` 循环内反复 `Text.Font=/GUI.color=` 且 `:3470` 末置 `Small` 未配对恢复 | 低 | 已被 `:3498` 的 try/finally 部分缓解；建议统一引入 `UiTextScope` 封装收口（详见架构审计 R5）。 |
| L3 | `CacheRefreshInterval=120` 与采样节流同频（2s） | 低 | 当前仅在窗口打开时触发，殖民规模大时每次重建约 2s 一次全 Section 快照；如未来卡顿，可上调至 300（5s）或按视图分脏标记。 |

## 四、结论
性能架构符合方案 §11 预算：热路径零每帧分配、采样/对账节流、失败隔离、输入保护、Read Model 边界均达标。**唯一运行期真实缺陷是 Def 配置错误（健康残值惩罚失效），已修复并编译通过**。其余为低风险优化项，不影响本版发布。
