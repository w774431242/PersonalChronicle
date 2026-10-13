# ADR-GRID-1 — 电网子系统扩展点选型（拓扑阻断 / 甩负荷接管 / 分区持久化）

> ⚠ **已废弃（Superseded / Deprecated，2026-10-07）**：本 ADR 的三项扩展点选型（拓扑阻断 / 甩负荷接管 / 分区持久化）随电网子系统整体净删（`CHG-20261007-GRID-REMOVE`）而失效——原依据文件（REQ / SDD / EXC-2026-027）均保留历史记录，本 ADR 保留为历史决策档案。

- **Status**：~~**Accepted**（PM 于 2026-10-06 追认）~~ → **Superseded / Deprecated（2026-10-07）**：随电网子系统整体净删而废弃（`CHG-20261007-GRID-REMOVE`）。
- **Date**：2026-10-06（**同日随 SDD v0.4 刷新状态面板**）
- **Deciders**：PM（7 项裁决已冻结）+ AI（方案与取证）
- **Rule IDs**：`GOV-001` / `GOV-007` / `GOV-008` / `AI-003` / `ARC-008` / `ARC-010` / `COMP-002` / `SAVE-001` / `DATA-007`
- **Related Gates**：`GATE-ARCH` · `GATE-COMP` · `GATE-SAVE`（**已于 2026-10-06 复评**，记录见 `.codebuddy/audit/GR-20261006-GRID-REQ-SDD-ARCH-DATA-SAVE-UI-LOC-COMP-PERF-TEST-REL-DELTA.md`）
- **关联文档**：`REQ_电网系统.md` · `SDD-2026-10-06-电网系统.md` · **`EXC-2026-027.md`** · `AUTO-2026-10-06-电网系统设计方案.md` · `AUTO-2026-10-06-电网系统开发管线.md`

## Context

原版「大范围断电」由两机制叠加：① Zzztt 短路抽干**整张网的全部电池**；② `PowerNetTick` 在供需不足时每 20 tick 随机关掉**全网** `max(1, 5%)` 耗电设备（Cecil 实证，Build 1.6.4871 rev590）。

要让「故障只跳一段」，必须能**主动阻断网的连通**。原版 `PowerNet` 无任何电气物理量（只有 `powerComps` 平表与全局 `CurrentEnergyGainRate()`），故方案只能是**拓扑分区**。

本 ADR 记录三处关键扩展点选型的取舍过程。**扩展点选型是本工程默认必须留 ADR 的项**（`AI-003`：不得因"实现简单"直接选 Harmony）。

## Decision

1. **拓扑阻断**：用 `Building.TransmitsPowerNow` 覆写（**原生扩展点**），**不挂 Harmony**。
2. **甩负荷接管**：用 `PowerNet.PowerNetTick` 的 **Harmony Prefix + `__runOriginal = false` + 安全阀**（P3 唯一 Harmony）。
3. **分区持久化**：记录挂在**锚建筑 `thingIDNumber`** 上，附 `LastKnownMembers` 作**匹配提示**（非权威拓扑）。

## Options Considered

### 决策一：拓扑阻断

| 方案 | 风险等级 | 优点 | 缺点 | 结论 |
|---|---|---|---|---|
| **原生 `TransmitsPowerNow` 覆写** | **低** | IL 实证 BFS 本就消费该属性（`PowerNetMaker.ContiguousPowerBuildings` 的 `IL_00EA`–`IL_00F1`：对每个邻居 `callvirt Building.get_TransmitsPowerNow`，`false` 即 `brfalse` 跳过）；**零 Harmony**；与第三方零挂点竞争 | 仅对「我们自己的分段设备」有效（第三方建筑无法受益） | ✅ **采纳** |
| Harmony Postfix `ContiguousPowerBuildings` | 中 | 可拦截任意 BFS | 该方法是 **`private static`**（需非公开反射）；与 **Power Diode 同挂点互踩**；且**完全没必要** | ❌ 否决 |
| Harmony Prefix 替换整个 BFS | 高 | 控制力最强 | 需自建二级网；与 Logic Switch / Parallel PG 的做法冲突 | ❌ 否决 |

> **取证关键**：初版设计稿选的是「Postfix `ContiguousPowerBuildings`」。写 SDD 时做 IL 核实，发现① 它是 `private static`；② BFS **本来就按 `TransmitsPowerNow` 决定扩不扩散** ⇒ 阻断**原生成立**。该发现使 P1/P2 **零新增 Harmony**，并消除了与 Power Diode 的冲突。

### 决策二：甩负荷接管

| 方案 | 风险等级 | 优点 | 缺点 | 结论 |
|---|---|---|---|---|
| **`PowerNetTick` Prefix + `__runOriginal=false` + 安全阀** | **高** | 唯一能真正取代原版随机 5% 的位置（`PowerNetTick` 是 `public`，非脆） | 位于原版热路径；**必须同时重建两条分支**（见 Consequences） | ✅ **采纳**（PM 裁决 #7） |
| 只挂 Prefix 不阻断 | 中 | 改动小 | **原版随机 5% 仍在跑 = 等于没接管**（Power Priority 的坑）；且我们随机 + 它随机 = 双重随机 | ❌ 否决 |
| 不接管（仅观测/告警） | 低 | 风险最低 | 不满足 `REQ-Grid-012`（有序甩负荷是本批核心目标之一） | ❌ 否决（PM 裁决 #7 已定"默认接管"） |
| 前 5 级扩展点（Def / DefModExtension / Comp / Service / PatchOperations） | — | — | **均不可行**：原版在 `PowerNetTick` 内硬编码了随机甩负荷，没有任何 Def 钩子或可注册的服务点能替换它 | ❌ 不可行 |
| Harmony Transpiler | 最高 | 精确替换 | 不可回滚；上游用「第 N 个 call」定位极脆 | ❌ 否决（全案零 Transpiler） |

### 决策三：分区持久化

| 方案 | 风险等级 | 优点 | 缺点 | 结论 |
|---|---|---|---|---|
| **锚 `thingIDNumber` + `LastKnownMembers` 匹配提示** | 低 | `ThingIDNumber` 随建筑存档且**永不复用**；分区拓扑本身不存（可重建，`DATA-007`） | 锚被拆后需靠成员集兜底匹配 | ✅ **采纳** |
| 直接存分区（含成员集） | 低 | 读档即用 | 分区是**派生量**，拓扑一变就失真 ⇒ 存的是过期真相，违反 `DATA-007` | ❌ 否决 |
| 只存锚、不做交集匹配 | 低 | 最简单 | 锚被拆 ⇒ **玩家命名丢失** | ❌ 否决（初稿即此，GATE-SAVE S1 判定该分支**无可计算输入**） |

## Consequences

**正面**

- Harmony 面收敛到 **P3 单点**（P1 零、P2 零新增）——本工程历史上 Harmony 面最小的子系统之一。
- 与 Power Diode 的挂点冲突**消失**；`DoShortCircuit` 仍不碰（已 5+ 家）。
- 跳闸态用 overlay 表示 ⇒ **断路器免除 `_Off` 贴图依赖**（仅隔离开关仍需）。

**负面（必须补偿）**

- **`PowerNetTick` 实为三分支，接管漏一半会出顽疾**：原方法在「盈余」时还会**按 `max(30, 200/count)` 节流开启**设备；另有一条「轻度缺口（电池可覆盖）」中间出口。⇒ 硬要求 **A′/B′/C′/D′/E′（五条）**（SDD §5.3）。
- 缺电判据必须用原版复合量 `CurrentStoredEnergy() + CurrentEnergyGainRate()`，**不能只看 `gain`**（否则满电池的网被误甩）。
- 必须尊重 `Map.gameConditionManager.ElectricityDisabled`（太阳耀斑）。
- 必须重建 **`V_2 = stored − 5`** 储备门（`IL_0036`–`IL_0060`），否则电池网在 ≤5 Wd 量级上口径分叉（**E′**）。
- `PowerNet.partsWantingPowerOn` / `potentialShutdownParts` 是原版 **`static`** 暂存表 ⇒ **不得复用**。
- **存能面（已取证，2026-10-06）**：`PowerNet.ChangeStoredEnergy` **已读完完整 IL**（`extra>0` 分发 / `extra≤0` 十轮扣取 / 欠额 `Log.Warning`），并据此形成 **D′**。⚠ **入参必须是 `CurrentEnergyGainRate()` 原值（Wd/tick）**，**不得传 `NetWatts`**（单位 W、已除以 `WattsToWattDaysPerTick`，差约 60000× ⇒ 能量虚增）。PERF 席**以 Cecil dump 独立复算确认**。
- 回滚必须**可执行**：「关闭 Patch」曾是空话，现落成「**删 `Capture/Patch_PowerNetTick.cs` 类文件即发版**（PatchAll 反射扫描）+ 次选设置开关」（`GATE-COMP` R4 已闭环）。

**需要的补偿措施**

| 项 | 措施 |
|---|---|
| 缓存失效 | `partitions` 按拓扑指纹失效；`LastKnownMembers` 仅作匹配提示，不参与判定 |
| 降级路径 | 安全阀判据须**精确谓词**（初稿「本图无任何分区配置」与「相邻输电体自动成分区」冲突 ⇒ 恒不成立，`GATE-ARCH` R5） |
| Migration | `schemaVersion` 定义**当前值与置位时机**（初稿缺失 ⇒ 恒为 0 ⇒ D1 永不成立，`GATE-SAVE` S2） |
| EXC | **Harmony 例外登记缺失** ⇒ `GATE-COMP` 主因之一，须按 `EXC-2026-001` 补登 |

## Compliance

- **是否触发 Architecture Review**：**Yes** —— 触发项 **`Harmony = Yes`**（P3 Prefix）。
  - `Hardcoding = Yes`：❌ 否（`SystemCenterIds` 常量属 `GOV-006` 登记的**数据键**）。
  - `Cross-Mod Direct Reference = Yes`：❌ 否（反射探测，无硬引用；但探测件**须落 `Compatibility/`**，`GOV-008`，当前缺失）。
  - `Static Mutable State = Yes`：❌ 否（§7.1 探测结果为**加载期一次性只读**，属 `ARC-008` 允许形态；实现期须以代码形态复核，防落成可写 static）。
- **是否需要例外登记**：**是**。① P3 新增 `Patch_PowerNetTick`；② P2 `Patch_IncidentWorker` **语义扩展**（该补丁现有书面契约含 "we never mutate"，引入跳闸写回将推翻它）⇒ 两者均须按 `EXC-2026-001` 登记。
- **验证方式**：
  - 决策一：实机「断开断路器 ⇒ 分区数 1→2」（SDD §9.2 P1-2）；静态搜索确认**无** `ContiguousPowerBuildings` 补丁。
  - 决策二：SDD §9.2 **P3-1**（接管生效）/**P3-7**（设备能重新开启，针对本 ADR 的 A′）/**P3-8**（满电池不误甩，B′）/**P3-9**（太阳耀斑，C′）/**P3-4**（安全阀），以及 `GATE-PERF` 要求的可复核指标。
  - 决策三：SDD §9.1 `PowerGridPartitionTests`（锚命中 / 交集命中 / 新分区 / 并网合并 / 分裂复用）+ §9.2 P1-3/P1-4。

## 待闭环（本 ADR 转 Accepted 的前置）

| # | 项 | 来源 | 状态（2026-10-06 随 SDD v0.4 刷新） |
|---|---|---|---|
| 1 | Application ↔ Runtime 数据通道 | `GATE-ARCH` R2（P1） | ✅ **已闭环**（SDD §3.5，逐条对齐 `WorkshopOpsAccess` 范式） |
| 2 | 安全阀**精确谓词** | `GATE-ARCH` R5（P1） | ✅ **已闭环**（SDD §5.3；**含让位出口**，O(1)） |
| 3 | 依赖图重画 | `GATE-ARCH` R1（P1） | ✅ **已闭环**（SDD §3.2 v0.4：`UITheme` 移入 UI 侧框 + 逐条禁向表） |
| 4 | `schemaVersion` 生命周期 | `GATE-SAVE` S2 | ✅ **已闭环**（SDD §4.5 版本生命周期 + §8.4 版本策略） |
| 5 | Harmony 例外登记 | `GATE-COMP` R11 | ✅ **已落盘** `EXC-2026-027.md` **并登记进 `EXC-2026-001` 台账（#23 + #11 语义扩展）**；⏳ 仅剩 **PM 追认**（Proposed → Approved） |
| 6 | `ChangeStoredEnergy` 取证 | `GATE-PERF` R-P0-3 | ✅ **已闭环**（SDD §5.3 修正四；**PERF 席以 Cecil dump 独立复算确认**） |
| 7 | 可复核性能指标 | `GATE-PERF` R-P0-1 | ✅ **协议已定**（SDD §9.2 P3-6 六要素模板：测试档/样本规模/两态采样/阈值/工具/落盘）；⏳ 实际数值待 P3 实装 |
| 8 | 分支完整性（A₂ 轻度缺口） | `GATE-PERF` N-3 | ✅ **已闭环**（SDD §5.3 修正三补 A₂ 出口 + 硬要求 **E′**） |
| 9 | D′ 入参单位 | `GATE-ARCH` N1 | ✅ **已闭环**（澄清为 `CurrentEnergyGainRate()` **原值 Wd/tick**，禁传 `NetWatts`） |

⇒ **转 Accepted 仅剩一项人工动作**：PM 追认 #5（`EXC-2026-027` Proposed → Approved）。
其余全部为「已闭环」，或属**实装后补证据**类（#7 的数值），**不阻塞**。
