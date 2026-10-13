# Gate Record — CHG-MFG-ALIGN-001

> **代写声明**：§1 GATE-ARCH 正文由 `pc-arch-gatekeeper` 子 agent 交付（该 agent 会话未挂载写文件工具），主 Agent **原样落盘**，未改写结论。其余 Gate 由主 Agent 复核后出具。
> 日期：2026-10-03 ｜ 变更：`docs/设计文档/架构/Change-Annotation-CHG-MFG-ALIGN-001.md`
> 范围：编排画布**对齐吸附**（M1 Domain 纯函数 + M2 UI 接线）+ ARC-013 物理切片 + 设备卡 MouseUp 落档补齐

## 0. 证据分级（GOV-012）

| 级别 | 含义 |
|---|---|
| Tier A | 独立执行/取证，有可复核命令与输出 |
| Tier B | 他方脚本产出、未独立复现 |
| Tier C | **未执行**（无游戏进程 / 工具受限 / 无基线） |

## 1. GATE-ARCH（pc-arch-gatekeeper 复判）

### 1.1 结论：**PASS**（无 Exception）

| 项 | 上轮要求 | 实测 | 判定 |
|---|---|---|---|
| 主文件体量 | < 1200（ARC-013 渲染密集上限，源 `08册:154`） | `WorkshopLayoutCanvas.cs:1159` | ✅ 余量 41 行 |
| 同类文件连带 | 全部 `WorkshopLayoutCanvas*` | 113/69/304/304/26/584 | ✅ 全 < 1200 |
| 零契约改动 | 类名/可见性/schema/翻译键 | `internal static partial WorkshopLayoutCanvas` 不变；存档链路成员全留主文件；翻译键由 `Primitives.cs` 原样承接 | ✅ |

**不为 PASS WITH EXCEPTION**：本变更已无偏离可豁免（ARC-013 达标、零新增静态态、无 Harmony、无硬编码）。标 WITH EXCEPTION 等于凭空声明一条活动豁免。

**四条硬触发（均 No）**：Harmony=No（零 `Patches/` 改动）｜ Hardcoding=No（阈值常量只在 Domain `:138/141/147`；线宽具名 `Align.cs:55-56`；`0.85f` 进 Theme 令牌；UI 内 `new Color(`=0）｜ Cross-Mod=No ｜ 新增 Static Mutable State=No（`Align.cs` 静态字段 0；3 新字段全在 `CanvasState`）。

### 1.2 `EXC-2026-011` review trigger 处置

**裁定：结案为「已收敛（trigger 消解）」，不进 EXC 流程，不登记新 EXC。**

1. 触发被**构造性消解**而非申请延期：trigger「主文件 >1400 行」，触发时 1538，处置后 1159 ⇒ 条件不复存在。
2. 新 EXC 无可豁免对象 ⇒ 登记义务不成立。
3. **⚠ 编号已被占用**：`EXC-2026-012.md` 已是「车间排班联动（E2，Approved 2026-10-02）」，另有 013/014/015 ⇒ **下个可用编号是 `EXC-2026-016`**。
4. `EXC-2026-011.md:5` 规则 ID 误引（ARC-011→**ARC-008**、02 册→**08 册**）判**必须本轮更正** —— 按 ARC-011 查「静态可变状态」会查到「架构风险评审触发」，使已批准的豁免**不可执行**。

### 1.3 检查清单（逐条）

| # | 检查项 | 结论 |
|---|---|---|
| 1 | 层级和模块已写明 | ✔ 标注 §0 列 Layer |
| 2 | 依赖在允许矩阵内 | ✔ Domain 零引擎 · Presentation→Core=✓ |
| 3 | 无循环依赖 | ✔ Domain ↛ UI |
| 4 | Core 未引用 UI/Theme/Asset/第三方 | ✔ `CanvasAlignRect` 只用 float |
| 5 | 跨模块经 Contract/DTO | ✔ `SaveDetached`→`ManufacturingLayoutService` 既有链路，只调不改 |
| 6 | 继承/Harmony/全局状态有必要性 | ✔ Harmony=0；新增静态态=0 |
| 7 | 降级与替代方案已定义 | ✔ 降级=阈值封顶+单测锁定；替代=否决缓存/否决锚点框/否决 Harmony |
| 8 | 大文件按 ARC-013 治理 | ✔ **唯一阻断项已闭合**（1159<1200） |
| 9 | 切片仅物理搬移，未改契约 | ✔ **B 级证据（无 git 基线）**；残留 U-8 |
| 10 | 未执行项已如实登记 | ✔ 标注 §4 的 U-1~U-9 |

### 1.4 强制动作执行

| ID | 动作 | 状态 |
|---|---|---|
| M-1 | `EXC-2026-011` 复审结案（trigger 消解、阈值改 >1200） | ✅ |
| M-2 | 更正规则 ID `ARC-011`→`ARC-008`、册号 02→08 | ✅ |
| M-3 | 19 条断言 + 行位移剖面固化进标注 §3.1 | ✅（E-1~E-6） |
| M-4 | 方案文档删 `AlignDragActive` | ⚠ 该文档在 gitignore 目录，已由受控标注取代 |
| M-5 | 标注 F1「8 值」→「6 值」 | ✅ |
| C-1 | `AppShell.cs` ≥1332 行存量债登记（R-10） | ✅ 记入 EXC 关联节 + 标注 §5 |
| O-1 | 3 个静态字段随切片落 `Interaction.cs` 的说明 | ✅ 标注 §3.2 |
| O-2 | 主文件自相矛盾注释订正 | ✅ `WorkshopLayoutCanvas.cs:151-158` |

### 1.5 ARCH 汇总

| Gate | Result | Evidence | Risk |
|---|---|---|---|
| ARCH | **PASS** | 主文件 1159 行；6 partial 全 <1200；Domain 纯净；C3/C4 入口唯一（`ScreenThresholdToDesign`×1、`Resolve`×1）；`CanvasState` +3 字段；零新增静态态；行位移剖面闭合（+0/+10/+12 两断点 = 申报编辑）；成员唯一性 0 重复 | 存量债 R-10（不阻断）；O-1（已判可接受）；U-8（无 VCS 基线，等级 B）；实机 U-1/2/4/5/6 未执行 |
| ARCH-台账 | **DONE** | M-1~M-5、C-1、O-1、O-2 全部完成 | — |

**未执行项（不伪造）**：U-1/2/4/5/6（实机）· U-7（PERF 实测）· U-8（切片语义等价性基线）· U-9（AppShell 精确行数）。

---

## 1.6 M3 追加复判（车间块吸附）

| Gate | Result | Evidence |
|---|---|---|
| ARCH | **PASS** | ① **依赖方向不变**：`Align.cs` → `Domain`（Presentation→Core=✓），零引擎 using 引入。② **零新增静态可变状态**：`IsZoneDeviceAlignable` / `TryGetZoneDeviceBounds` / `BuildZoneAlignNeighbors` / `ResolveAndMoveZone` 全为 `private static` **方法**（无静态字段），沿用既有 `deviceCardDragId` / `state.DragIndex` 作拖拽态。③ **零契约改动**：`FindBlockAt` / `HandleDragAndDrop` 签名不变；存档 schema、翻译键未动。④ **主文件体量**：M3 全部落在 `Align.cs`（新 partial），主文件 **1159 行不变** ⇒ ARC-013 仍达标。⑤ **双源漂移已消除**：吸附几何（`TryGetZoneDeviceBounds`）与命中几何（`FindBlockAt`）共用同一函数与同一排除真源（`IsZoneDeviceAlignable`）。 |
| DATA | **PASS** | Def 零变更；无新增持久化字段；`CanvasState` 仍只 3 字段且无 Scribe 路径。 |
| SAVE | **PASS** | 车间块位置走既有 `mfgLayout` + 宿主**每帧** `MirrorLayoutToSave()`（`AppShell.cs:1442`/`:1568`）⇒ **确认无「拖完丢位移」缺口**，故 M3 **不需要** M2 那种 MouseUp 补落档（已实测核实，非假设）。 |
| UI | **PASS**（视觉项待实测） | 参考线显隐门禁由「仅设备卡」放宽为「设备卡 **或** `state.DragIndex >= 0`」，两者都结束才清残留 ⇒ 车间块拖拽也有参考线、且松手不残留。`LastAction` 命中吸附时显示「已对齐」。查看态不出现（`#lbar` 仅编辑态绘制）。 |
| LOC | **PASS** | M3 **未新增键**，只把 `SnapTip` / `SnapTipNoTarget` 中英文案扩到覆盖车间块（中英同步修改）⇒ 键集合仍一一对应，无孤儿键、无占位符。 |
| PERF | **PASS** | 零分配（沿用 `AlignBuf` 预分配缓冲 + `CanvasAlignRect` 值类型）；`FindBlockAt` 新增并集 **early-out** 反而**降低**了逐卡测试次数；并集累加用 `Union` 返回新实例（4 个 float，无堆分配）。 |
| TEST | **PASS** | 编译 **0 错 0 警**（`--no-incremental` 强制全量）；单测 **454 通过 / 0 失败 / 1 跳过**（M2 的 443 ⇒ **+11**）；`verify-align.ps1` **0 FAIL**。 |
| DELTA | **PASS** | 见 §6（重跑，EXITCODE=0）。 |

**M3 修正的两处自身问题（如实登记）**
- `CanvasAlignRect` 是 `readonly struct`，最初写的就地 `UnionWith` **编译不过**（CS8331 家族）⇒ 改为返回新实例的 `Union`，保住 readonly 语义。
- ⚠ **一次「0 错 0 警」是缓存假通过**：改完 `Union` 后未强制重建，增量构建报「所有项目均是最新的」并显示 0 错，实际测试工程随即报 12 个 `CS0117/CS1061`。**结论：编译门禁必须配 `--no-incremental`**，否则「0 错」不可信。已在本轮全部改用强制重建。

---

## 2. GATE-DATA / GATE-SAVE

| Gate | Result | Evidence |
|---|---|---|
| **DATA** | **PASS** | ① **Def 零变更**：`git status` 无 `Defs/` 改动。② **Def 只放静态配置**：本次无 Def 新增/修改；吸附参数是**会话 UI 偏好**（`SnapEnabled`），刻意**不进 Def**（否则每次调手感要重开游戏，且会波及 `DATA-001` 与 `SystemCenterWiringTests`）。③ **Runtime 不入 Def/UI**：`AlignBuf`/`AlignGuides` 是帧内输出缓冲（每帧重算，无跨帧语义）；`SnapEnabled` 是编辑器偏好 —— 均不描述玩法。④ **缓存可从权威数据重建**：`AlignBuf` 无跨帧状态、无失效面（每帧 `Clear()+Add`），不存在「缓存脏了只能靠猜修」。⑤ Domain 新文件不含任何 `defName` 字面量（C1/C2 通过，全文为纯标量数学）。 |
| **SAVE** | **PASS**（schema 无变更） | ① **schema 可识别**：`ChronicleGameComponent.DeviceLayout.cs` 容器键 `mfgDeviceLayout` 未变，条目字段集实测仍为 `StableId,x,y`（脚本正则抽取确认）。② **新字段默认值**：未新增任何持久化字段 ⇒ N/A。③ **旧档可加载**：字段集未变 ⇒ 天然可加载。④ **Migration**：不需要（schema 未 bump，`LayoutSchemaVersion`=1）。⑤ **失败隔离**：`SaveDetached` 自带 try/catch + `Log.ErrorOnce`，写入失败不影响绘制。⑥ **幂等**：`SetDeviceLayoutEntry` 的 `\|Δ\|<0.01 → return` 守卫（`DeviceLayout.cs:78`）⇒ 重复落档零脏写。 |

**AI-007 结论**：旧 schema → 当前 schema = **无迁移** ｜ 缺失字段默认值 = N/A ｜ 未知值降级 = N/A ｜ 重命名映射 = N/A ｜ Migration 幂等性 = N/A。

**Save 侧唯一行为变更（写入时机）**：设备卡位置从「关窗 / 撤销时」改为「**拖拽结束即写**」。值域不变 ⇒ 非 schema 变更。
- 频率：1 次 / 拖拽结束（**非每帧**）⇒ 不违反 PERF-001
- **已知量化边界（如实登记）**：守卫阈值 0.01 设计单位，而 `MinSnapDesign=1f`、坐标不取整 ⇒ 视觉值与存档值允许 <0.01 偏差被吞。属可接受边界，非阻断。
- **旧档回归**（`读档 → 画布 → Shift 拖拽吸附 → 存 → 重开位置正确`）⇒ **待实机（U-5）**，不得标已验证。

---

## 3. GATE-UI / GATE-LOC

| Gate | Result | Evidence |
|---|---|---|
| **UI** | **PASS**（视觉项待实测） | ① **Window/Panel 职责**：未新增窗口/面板，改动全在既有 `DrawCanvasBody` / `#lbar` 内。② **UI State 不持持久状态**：3 个新字段全在 `CanvasState`（窗口实例态），**无 Scribe 路径** ⇒ 天然不入档（与 GATE-SAVE 交叉印证）。③ **Theme Token 集中**：新增 `UITheme.AlignGuide`（`UITheme.cs:272`），`Align.cs` 内 `new Color(` = **0**。**刻意做成派生属性** `=> WithAlpha(Accent, 0.85f)` 而非 `Apply` 里赋值 ⇒ 任何主题切换自动同步，不可能「切了主题参考线还是旧颜色」。④ **状态完备**：Normal / On（`Accent` 边框 + 18% 底）/ Hover（既有 `ButtonInvisible`）/ Disabled（**整条 `#lbar` 只在 `floorEditMode` 绘制**，查看态天然不出现）/ Unavailable（`HasAlignTarget` 为假时提示切到 `SnapTipNoTarget`，**不静默失效**）。⑤ **大列表降级**：`#lbar` 有 `bar.xMax - LbarPadX` 溢出保护 + 提示区 `x + hintWidth < bar.xMax - LbarPadX - 260f` 预算判断兜底。 |
| **LOC** | **PASS** | ① **职责分离**：4 键均属 `PersonalChronicle.UI.Layout.*`，未写 DefInjected（非 Def 字段）。② **全 Keyed**：中英**双侧齐备**（`Compare-Object` 零差异，EN 46 / ZH 46）。③ **无孤儿键**：4 键**均被代码引用** —— `ToggleSnap`（`AppShell` 开关）· `SnapTip`/`SnapTipNoTarget`（`AlignSnapTip`）· `SnapGuideAligned`（`Interaction.cs` MouseUp 命中吸附时把 `LastAction` 显示为「已对齐」，让玩家不必猜吸附是否生效）。④ **fallback 可解释**：无 fallback 逻辑。⑤ **占位符一致**：4 键**均不含占位符** ⇒ `LOC-004` 无风险。⑥ **GOV-009**：判定源是 `bool SnapEnabled` 与 `HasAlignTarget` 返回值，⛔ 不按翻译文本判断。 |

**GATE-UI 待实测（如实登记）**：U-2（参考线裁剪正确性/无残线/视觉权重）· U-3（窄视口下 `#lbar` 是否挤掉右侧 Save 预览）。

---

## 4. GATE-COMP

| Gate | Result | Reason |
|---|---|---|
| **COMP** | **NOT APPLICABLE** | **带理由的 N/A**（非逃避）：本变更集**零第三方 Mod / DLC 类型引用**（`WorkshopCanvasAlignment.cs` 与 `WorkshopLayoutCanvas.Align.cs` 不含外部程序集类型；Domain 经 C1 校验无引擎 using）、**零 Harmony**、**零 `Patches/` 改动**、**零 `Compatibility/` 改动**、**`About.xml` 未改**（无新增 `modDependencies`/`loadAfter`）⇒ 无可判对象。依据：全量改动集不含上述路径。 |

---

## 5. GATE-PERF / GATE-TEST

| Gate | Result | Evidence |
|---|---|---|
| **PERF** | **PASS**（增量零分配已证；实机指标待测） | ① **热路径无全库扫描/反射**：只遍历 `DetachedList` + `PlacedZoneIds`（画布自身集合）。② **无界分配**：**零分配**由设计保证 —— 两个缓冲在 `Create()` 预建，每帧只 `Clear()` + `Add(struct)`（struct 入栈不装箱）；参考线走既有 `DrawPolyline`（`DrawBoxSolid` 串联，无 LINQ）。残留扫描 **0 命中**。③ **UI 重绘有刷新条件**：参考线**只在拖拽态绘制**，非拖拽帧一次 `Clear()` 即 return。④ **日志不在热路径**：`SaveDetached` 每拖拽 1 次且自带 `Log.ErrorOnce` 节流。⑤ **可复核指标**：投影规模 = `|DetachedList| + Σ 各车间在册卡数`，每帧 O(N + M·|DetachedList|) 次 `IndexOfDetached` 比较。**如实声明**：未做空间索引（PERF-001 禁「无证据的预先优化」）；实机 N>200 再复评。⑥ **既有热路径问题不在本期**（R-5）：`Measure` 的 `Text.CalcSize`、`Translate1/2` 每次 `string.Format`、`PaletteIndexOf` 每帧 O(N)、`BuildSavePreview` 每帧重建 —— 吸附**不加剧**（零分配），不修复。 |
| **TEST** | **PASS**（编译+单测 Tier A 实测；实机项未执行） | ① **0 错 0 警**：`dotnet build Source\PersonalChronicle.csproj -c Release` ⇒ 0 warnings / 0 errors。② **单测全绿**：`dotnet test …` ⇒ **443 passed / 0 failed / 1 skipped**（基线 416 ⇒ **+27 条**）。③ **XML/翻译解析**：两份 `Archive.xml` `[xml]` 良构；中英键 46/46 一致。④ **约束脚本 0 FAIL**：`verify-align.ps1` 六项（C1/C2/C3/C3b/C4/C4b）+ LOC + SAVE + 残留 + 体量。⑤ **失败 Provider 不影响核心**：不适用（本次无 Provider）。⑥ **Vanilla 启动 / UI 关键路径 / 存读档迁移** ⇒ **未执行**（沙箱无法起游戏进程）⇒ 诚实降级，**不写 PASS**。⑦ **GATE-DELTA** ⇒ PASS（EXITCODE=0）。 |

---

## 6. GATE-REL / GATE-DELTA

| Gate | Result | Evidence |
|---|---|---|
| **REL** | **NOT APPLICABLE** | **带理由的 N/A**：本 Change **不发版**（无 `About.xml` `<version>` 变更、无 `Changelog` 产出）。版本号 / DLC 声明 / Harmony 范围 / 回滚说明不在本次范围。若并入下次 Release 则 REL 必判。 |
| **DELTA** | **PASS** | `gate-runner.ps1 -Base HEAD -Annotation Change-Annotation-CHG-MFG-ALIGN-001.md -ExcludeFromFile .codebuddy/tmp/chg-mfg-align-001-exclude.txt -Blocking` ⇒ **EXITCODE=0**，「标注覆盖 15 个改动文件，无遗漏/超范围/残留」。基线：本地 `HEAD=21fd0a9` **领先** `origin/main=dbcf8f8` 1 commit ⇒ 必须 `-Base HEAD`（用 `origin/main` 会把整个历史未提交工作算成本次改动）。范围外清单 1069 项，并已剔除 3 条「是 in-scope 路径子串」的条目 —— `-Exclude` 按**子串**匹配，裸文件名会把自己的文件也排除（曾致 3 个假 phantom，已修）。 |

---

## 7. 汇总

| Gate | Result | 负责 |
|---|---|---|
| ARCH | **PASS** | pc-arch-gatekeeper（复判） |
| DATA | **PASS** | 主 Agent 复核 |
| SAVE | **PASS**（schema 无变更） | 主 Agent 复核 |
| UI | **PASS**（视觉项待实测） | 主 Agent 复核 |
| LOC | **PASS** | 主 Agent 复核 |
| COMP | **N/A**（零第三方触点） | 主 Agent 复核 |
| PERF | **PASS**（实机指标待测） | 主 Agent 复核 |
| TEST | **PASS**（编译+单测实测；实机未执行） | 主 Agent 复核 |
| REL | **N/A**（不发版） | 主 Agent 复核 |
| DELTA | **PASS** | 主 Agent 复核 |

**P0/P1 违规：0** ⇒ 允许交付。
**P2 已知风险**：R-5（既有热路径，不在本期）· R-10（AppShell 体量存量债）· U-3（窄视口布局待实测）· U-8（切片无 VCS 基线，等级 B）。
**已消解**：R-1（MouseUp 落档已修）· R-6（排除式去重，与顺序无关）· R-7（EXC 复审结案）· R-8（+27 条单测基线）· R-9（规则 ID 已更正）。

## 8. Remaining Risks

| ID | 风险 | 级 | 处置 |
|---|---|---|---|
| R-1 | 设备卡位移中途丢失 | — | ✅ 本轮已修（MouseUp 落档） |
| R-2 | 缩放不锚定光标，放大吸附体验问题 | P2 | 改它会变更所有落点/命中/ghost 行为，回归面超本需求 ⇒ 独立 Change 候选 |
| R-3 | `current.shift` 在 `MouseDrag` 的实机可靠性 | P2 | 实机验证（U-1）；失真则回落 MouseDown 锁存（需 ARCH/UI 复核） |
| R-4 | Z<50% 阈值封顶 ⇒ 近似对齐 | P3 | 如实降级，`MaxSnapDesign` + 单测锁定 + U-6 |
| R-5 | 既有热路径问题 | P2 | 不在本期；吸附不加剧（零分配） |
| R-6 | 静态 `DetachedList` 多条失效路径耦合 | — | ✅ 已消解（排除式去重，与顺序无关；刻意不跨帧缓存） |
| R-7 | 主文件体量触发 EXC review trigger | — | ✅ 已消解（1159<1200；EXC 已复审结案 + 规则 ID 更正） |
| R-8 | 零画布单测基线 | — | ✅ 已建（+27 条） |
| R-9 | `EXC-2026-011.md:5` 规则 ID 误引 | — | ✅ 已更正（ARC-008 / 08 册） |
| R-10 | `AppShell.cs` ≥1332 行超 ARC-013 且未登记 | P2 | 已登记为存量债；建议后续独立 Change 做 partial 切片；不阻断 |
| U-1~U-9 | 实机 / 无基线类未测项 | — | 见变更标注 §4 |

## 9. 交付摘要

**Goal**：车间俯视编排画布增加**对齐吸附**工具（拖动设备卡时按住 Shift 对齐到邻近卡的边线与中心线 + 参考线），并修复设备卡位移中途丢失的既有缺口。

**Changed files**：12（新增 6 · 修改 6），详见变更标注 §0。

**Rule IDs**：GOV-001/003/005/009/010/012/013 · ARC-002/008/013 · DATA-001/007 · SAVE-001/003/004 · UI-001~009 · LOC-001/004/005/007 · PERF-001/002/003 · TEST-001 · ANNO-001 · AI-001/003/004/005/007/012/014。

**Architecture**：Domain 新增纯函数模块（零引擎引用，可离线单测）→ UI 只做采样/枚举/调用/绘制/画线；依赖单向 Presentation→Core，无循环。零 Harmony、零新增静态可变状态。

**Data**：Def 零变更；Runtime 仅新增帧内派生缓冲；Save schema 无变更（字段集 `StableId,x,y` 不变、`LayoutSchemaVersion`=1、无 Migration）；UI State 落 `CanvasState` 实例态且无 Scribe 路径。

**Localization**：新增 4 键，中英一一对应（EN 46 / ZH 46），全部被代码引用，无孤儿键，均不含占位符；逻辑不读翻译文本（GOV-009）。

**Compatibility**：N/A（零第三方触点）。

**Performance**：增量零分配（两个预分配缓冲，每帧 `Clear()+Add(struct)`）；参考线仅拖拽态绘制；未做空间索引（禁无证据的预先优化）；既有热路径问题不加剧、不在本期修复。

**Migration**：无。

**Exceptions**：不登记新 EXC。`EXC-2026-011` 复审结案为「已收敛（trigger 消解）」，并更正其规则 ID 误引（ARC-011→ARC-008、02 册→08 册）。

**未测项**：U-1~U-9（实机 6 项 + PERF 指标 + 切片基线 + AppShell 行数）—— 详见变更标注 §4。

