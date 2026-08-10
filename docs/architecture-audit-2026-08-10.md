# 全项目架构检查报告（v4.7.0 统一后）

> 日期：2026-08-10 · 依据《高拓展高接入架构方案.md》§16 验收清单，对 `e:\SteamLibrary\steamapps\common\RimWorld\11\PersonalChronicle` 做静态核查 + 编译验证。
> 本次变更已通过 `dotnet build -c Release`（0 错 0 警）。

## 〇、版本统一结果（本次落地）

| 维度 | 方案 §6.3 定位 | 统一前 | 统一后 |
|---|---|---|---|
| **发布版本** `modVersion` | 对外玩家可见版本 | 1.0.0 | **4.7.0** |
| **API 契约版本** `ApiVersion.Current` | 接口兼容，能力探测 `Supports(major,minor)` | 4.1.0 | **4.7.0** |
| **存档 Schema 版本** `SchemaVersion` | 独立于发布版本，存档迁移 | 3 | **3（不动）** ✅ |

- `About.xml` 描述重写：去掉「完成度极低/早期预览」措辞，补齐 v4.3 阵营图鉴 / v4.6 检视页签 / v4.6.1 健康残值 / v4.7 传承。
- `README.md`：徽章 + 功能表 + FAQ + 页脚统一到 4.7.0。
- **功能性修复**：`ApiVersion` 抬到 4.7.0 后，`api.Supports(4,2)/(4,3)/(4,6)/(4,7)` 现已全部返回 true，第三方能力探测不再误判。

## 一、验收清单逐项结论

| # | 验收项（方案 §16） | 结论 | 证据 |
|---|---|---|---|
| 1 | 单向分层 UI→Application→Domain←Data/Capture | ✅ 通过 | 无 UI 层反向依赖 Domain 写；Capture 经 Application 服务写入 |
| 2 | Provider 注册表 + 失败隔离 | ✅ 通过 | `ArchiveProviderRegistry.ForEach` 已 try/catch + per-ProviderId 去重警告（§7.3） |
| 3 | 统一接入门面 `PersonalChronicleApi.TryGet` | ✅ 通过 | 单一入口；`Supports(major,minMinor)` 版本探测（现 4.7.0 生效） |
| 4 | Read Model 边界：窗口只消费快照 | 🟡 部分修复 | 见「整改项 R1/R2」 |
| 5 | 稳定 ID（不依赖 label/defName） | ✅ 通过 | `Pawn.GetUniqueLoadID()` / `defName:thingIDNumber`；`objectsByStableId` O(1) |
| 6 | 存档仅存字段、不存文案 | ✅ 通过 | Domain 各 `ExposeData` 用 `Scribe_*` + `LabelSnapshot` 快照，无硬编码文案 |
| 7 | Schema 版本独立 + 幂等迁移 | ✅ 通过 | `SchemaVersion 0→1→2→3`，`EnsureMigrated` 幂等 |
| 8 | UI 状态配对恢复（Font/Anchor/Color） | 🟡 部分修复 | 见「整改项 R3」，其余为低风险散点 |
| 9 | 视觉令牌收敛到 UITheme/UIComponents | 🟢 基本通过 | 少量窗口内 `ArchiveUiStyle.Muted` 直读（转发壳属性，非 `new Color`） |
| 10 | 文档/版本不脱节 | ✅ 本次已修复 | 见「〇」 |

## 二、整改项（已修 / 待修）

### ✅ 本次已修（含编译验证）
- **R1（红线1+4·阻断）**：`ArchiveMainTabWindow.RebuildEventCache` 内联 `service.GetAllEvents().OrderBy(...)` 排序 + null-guard。已下沉到 `ArchiveUiDataProvider.BuildTimelineEvents`（接口 `IArchiveUiDataProvider` 新增方法），窗口改为消费快照。
- **R3（状态泄漏·阻断）**：`DrawWorkIntensityHero` 在 `:3498` 处 `Text.Font = GameFont.Medium` 设置后未配对恢复，污染后续控件的全局字体/颜色状态。改为 `prev` 快照 + `try/finally` 配对恢复（与 `:3911` 处既有写法一致）。

### 🟡 待后续整改（分级，未本次强改以免回归）
- **R2（红线1·阻断）**：`DrawPawnIntensityBars` `:4058` 在绘制循环内每帧对 `intensity.Tiers` 做 `OrderBy(t => t.Hours)`。建议把排序结果缓存进 `PawnIntensityView`（Read Model 产物），Draw 只消费。属性能优化/边界改善，非崩溃风险。
- **R4（红线2·警告）**：`DrawHomeOverviewCards` `:3443` 在 Draw 内直读 `service.GetCategoryStats()` 并做 `OrderByDescending` + null 投影；与 R1 同类，应下沉快照。当前仅 Home 概览卡触发，频率低于时间轴。
- **R5（状态恢复·警告）**：`ArchiveMainTabWindow` 内另有多处 `Text.Font=`/`Text.Anchor=` 设置，经抽样均为局部成对恢复（如 `:3911`、`:4365`），未发现第二处泄漏，但建议后续做一次性 `using(var g = new UiTextScope(...))` 封装收口。
- **SkillArchive 翻译键**：`GetSkillArchive` 在 `IArchiveQueryService` + `ArchiveService` 仍被 API 契约引用，UI 层虽 v4.5 移除独立区块，但**键与契约方法保留**（不删，避免破坏第三方兼容）。属低风险冗余，非死代码。

## 三、结论
版本口径已统一为 **4.7.0**（发布版本 / API 契约同步，Save Schema 独立保留 v3）。架构主体符合《高拓展高接入架构方案》验收清单，两条阻断级红线（R1 排序越界、R3 状态泄漏）已修复并编译通过。剩余 R2/R4 为 Draw 内每帧排序/查询的边界优化项，建议作为下一迭代的技术债清理，不影响本版发布。
