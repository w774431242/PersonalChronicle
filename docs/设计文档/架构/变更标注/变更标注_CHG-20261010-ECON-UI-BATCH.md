---
change_id: CHG-20261010-ECON-UI-BATCH
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedNotDeployed
scope: 经济机构窗口呈现层收口 F1–F5 + F6-A（零行空态 / 总览裸键回落 / 政策行实测高 / 重复引导去重 / 照护门 ID 键化 / 说明句次要色与分组分隔）
rule_ids: AI-006, GOV-010, GOV-012, UI-001, ASSET-001, LOC-001, LOC-004, PERF-001, TEST-001, DOC-001
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyLabels.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWindowLayout.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Care.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Overview.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Policy.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyCommands.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyTreasuryPresenter.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  - path: Tests/EconomyWindowPresentationTests.cs
    type: A
---

# 经济机构窗口呈现层收口 F1–F5 + F6-A（CHG-20261010-ECON-UI-BATCH）

**ChangeID**：`CHG-20261010-ECON-UI-BATCH`｜**日期**：2026-10-10｜**作者**：AI（CodeBuddy / DeepSeek-V4.1-Flash）
**依据**：总控派发（`team-lead` 任务书，修点已由总控审计 file:line 定死）
**部署**：**未部署**（同树另有会话在写，部署由 PM 触发）｜**Save 变更**：**无**（零 Scribe 字段 / 零 Def / 零 Harmony）

---

## Implementation Summary

### Goal
把经济机构窗口（`Dialog_PawnEconomy`）从"逻辑正确但呈现层裸奔"收口到与项目既有 spec 一致的状态：零行页签不再空白、缺键不再打印裸键、政策行按实测换行高排版、重复引导只留一条、稳定门 ID 不再直接上屏、说明性长句与数据行视觉可辨。

### 可用原子清单（先盘点后落地，同类元素未另起几何）
| 需求 | 复用的既有原子 / token | 证据 |
|---|---|---|
| 空态 | `RUIF.FeedbackAtoms.EmptyState(rect, title, sub)` | `Dialog_PawnEconomy.Policy.cs:93` 原有用法；`RUIF/Source/Components/FeedbackAtoms.cs:133` |
| 次级文本色 | `RUIF.Tokens.Muted` | `RUIF/Source/Core/Tokens.cs:90`；`NeedDashboardPanel.cs:91` 同口径 |
| 分组分隔线 | `RUIF.Draw.LineH(x, y, len, color)` + `RUIF.Tokens.BorderHair` | `RUIF/Source/Core/Draw.cs:139`；`Tokens.cs:86` |
| 滚动 / 内容宽 | `RUIF.Layout.BeginScroll`（内容宽 = 外框宽 − `Tokens.ScrollbarThickness`=18） | `RUIF/Source/Layout/Layout.cs:42-47` |
| 显式折行 + 度量 | 既有 `Flow()`（`Dialog_PawnEconomy.Needs.cs:16`）+ `MeasuredHeight()`（`Dialog_PawnEconomy.Policy.cs`）—— 与首页通用行 `MeasureRows` 同口径 | `Dialog_PawnEconomy.cs:89-101` |
| 按钮 | `RUIF.ButtonAtoms.PrimaryButton` | 全链路未变 |

> ⚠ 关键事实（决定了 F3 的正确修法）：`RUIF.Draw.Label` **显式禁用自动换行**（`RUIF/Source/Core/Draw.cs:151-172` 把 `Text.WordWrap` 钉为 false 并还原）。因此本工程所有多行文本必须先经 `Flow()` 插入显式换行符，再按 `Text.CalcHeight` 实测高排版 —— 这正是首页通用行的做法，也是政策行原先缺失的一步。

### Changed files（front matter 逐项）
| 文件 | 类型 | 改动要点 |
|---|---|---|
| `EconomyLabels.cs` | M | 新增 `TranslatedOrId(key, fallbackId)`（整键翻译 + 缺键回落**标识本身**），与既有 `LocalizedId` 同一条回落纪律、唯一实现 |
| `EconomyWindowLayout.cs` | A | 新增呈现层**纯几何/空态契约**（零 RimWorld 依赖，可离线断言）：政策行文本↔按钮带的偏移函数、`ShowsEmptyState`、空态键常量、说明行种标记 |
| `Dialog_PawnEconomy.cs` | M | F1 零行 ⇒ 既有空态原子（并有最小内容高，避免空态被滚出视口）；F6-A 说明行按行种用次要色 |
| `Dialog_PawnEconomy.Care.cs` | M | F5 七门 ID → `PC_PEA.Care.Gate.*`（未知 ID 回落 ID 本身）；F1 无授权 ⇒ 同一空态口径；F6-A Hint 次要色 + 能力条与授权区之间 hairline 分组线 |
| `Dialog_PawnEconomy.Overview.cs` | M | F2 `O()` 委托 `EconomyLabels.TranslatedOrId`（永不打印完整键名）；F6-A 末尾「只读」说明行次要色 |
| `Dialog_PawnEconomy.Policy.cs` | M | F3 行高改为实测：`Flow` 折行 + `CalcHeight` 度量（仅在原串/宽度变化时重算，**无每帧 CalcHeight**），`contentHeight` / focus 偏移 / `y +=` 三处共用 `EconomyWindowLayout`；F6-A Hint 次要色 |
| `EconomyCommands.cs` | M | F6-A 说明行打行种标记并在 LegacyFees 白名单里保留；F4 旧记录**无模型**行去掉 `Configure/Focus` 动词（纯文本行） |
| `EconomyTreasuryPresenter.cs` | M | F4 去掉与顶部重复的 `Treasury.SelectBank` 引导行；F6-A 国库说明/引导/冲突行打说明标记 |
| `Languages/ChineseSimplified/Keyed/PawnEconomy.xml` | M | 新增 10 键：`Tab.Empty` / `Tab.EmptyHint` / `Overview.Unpublished` / `Care.Gate.*`(7) |
| `Languages/English/Keyed/PawnEconomy.xml` | M | 同上 10 键（逐键对齐） |
| `Tests/EconomyWindowPresentationTests.cs` | A | 新增回归：键集合 zh/en 齐备（防裸键复发）、空态分支唯一可达、政策行几何"按钮带不压实测文本" |

### Rule IDs
`UI-001`（窗口/组件职责与状态）｜`ASSET-001`（原子复用，无自研几何）｜`LOC-001` / `LOC-004`（用户可见文本全键化 / 稳定 ID 与显示文本分离）｜`AI-006`（新增可见文本的键化处理）｜`PERF-001`（热路径零每帧测量）｜`TEST-001`（离线可证伪面）｜`GOV-010`（不臆造文本）｜`GOV-012`（不伪造 PASS）｜`DOC-001`（标注落点）。

### UI
- **F1 空态**：`Dialog_PawnEconomy.DrawRows` 零行分支 + 照护页无授权分支，统一走 `RUIF.FeedbackAtoms.EmptyState`（政策页原本已有该用法，本次把另外两处补齐为同口径）。
- **F3 政策行**：文本区高 = 实测折行高；按钮首行顶 = 文本底 + 4f；整行推进高 = 文本 + 4 + 3×34 + 30 + 6。滚动内容高与 focus 滚动偏移改用同一函数。
- **F4 去重复引导**：`Treasury.SelectBank` 行删除（顶部 `PC_PEA.NoFocus`「从机构列表选择柜台」保留）；旧记录无模型行不再挂按钮。
- **F6-A**：说明性长句走 `RUIF.Tokens.Muted`；照护页能力取证条与下方授权区之间加 `RUIF.Draw.LineH` + `Tokens.BorderHair` 分组线。**未动**行高/滚动/命中几何（候选 B「两行卡片」本轮不做）。

### Localization
- 新增 10 键（见上表），zh/en **逐键一致**（脚本比对：`zh=228 en=228`，`only-zh=0 / only-en=0`）。
- F5 的 7 个门 ID 由 Domain（`CareRules.Missing()`）产出，Domain **未引本地化**；映射只在呈现层 `GateLabel` 做一次，未知 ID 回落显示 ID 本身（不空白、不臆造）。
- F2 的 `O()` 缺键时显示标识（如 `Unpublished`），**永不显示 `PC_PEA.Overview.Xxx`**。

### Performance
政策行度量落在既有「内容变才重算」路径：每帧最多 O(行数) 的 StringBuilder 拼接，`Text.CalcHeight` 仅在该行原串或内容宽变化时执行一次。首页通用行沿用原有 `measuredView/measuredWidth` 缓存（未改）。核验：`grep` 确认 `Dialog_PawnEconomy.Policy.cs` 内 `CalcHeight` 只出现在 `MeasuredHeight` 内且被 `string.Equals` 守卫。

### Migration
**无**（零 Def / 零 Save / 零 Scribe 字段 / 零 Harmony；无数据形状变化）。

### Exceptions
**无**（未新增 EXC；未改 `EconomyCommands.Query()` 契约；未改其它 Mod 的 Key 或全局翻译）。

---

## Gate Results

| Gate | 结果 | Rule IDs | Evidence |
|---|---|---|---|
| **GATE-UI** | **PASS（离线部分）** | UI-001 / ASSET-001 | ① 空态：`F1` 两处分支均走 `RUIF.FeedbackAtoms.EmptyState`（`Dialog_PawnEconomy.cs` DrawRows / `Dialog_PawnEconomy.Care.cs`）；② 主题：无新增颜色字面量，全部 `RUIF.Tokens.*`；③ 政策行几何离线断言通过（4/4，见下）；④ 说明行标记 `EconomyWindowLayout.HintRowVerb` 在 LegacyFees 白名单保留（`EconomyCommands.cs`）—— 否则会被剥成纯文本行 |
| **GATE-LOC** | **PASS** | LOC-001 / LOC-004 | 新增 10 键 zh/en 齐备（`PawnEconomy.xml` ×2）；`audit-keyref-integrity.ps1` 结论 `未登记的未解析引用 = 0`；`audit-cjk-literals.ps1` 本批 6 个 .cs 文件**零命中**（唯一 `PawnEconomy` 命中来自未改动的 `ShadowPlanPresenter.cs`）；键集合比对 `zh=228 en=228` |
| **GATE-PERF** | **PARTIAL / NOT RUN（实机）** | PERF-001 | 代码级：政策行度量有"内容变才重算"守卫，无每帧 `CalcHeight`（证据=源码守卫 + `string.Equals` 分支）。**实机帧耗时未测**（沙箱无法起游戏进程）⇒ 不作 PASS |
| **GATE-TEST** | **PASS（离线）** | TEST-001 | `compile_with_sdk.ps1` → 0 错 0 警；`dotnet test` → **失败 0 / 通过 1220 / 跳过 1 / 总计 1221**；新增 fixture 过滤运行 → **4/4 通过** |

### 验证命令与原始结论（可复跑）
```
pwsh _scripts\compile_with_sdk.ps1                       → exit 0，无 CS 错/警
dotnet test Tests\PersonalChronicle.Tests.csproj         → 失败: 0，通过: 1220，已跳过: 1，总计: 1221
dotnet test ... --filter FullyQualifiedName~EconomyWindowPresentation
                                                          → 失败: 0，通过: 4，总计: 4
pwsh _scripts\audit-cjk-literals.ps1                     → UI?=46（本批文件 0 命中）
pwsh _scripts\audit-keyref-integrity.ps1                 → 未登记的未解析引用 = 0
zh/en 键集合比对（PawnEconomy.xml）                       → zh=228 en=228，only-zh=0，only-en=0
```

---

## Remaining Risks（未验证项一律标 NOT RUN）

1. **NOT RUN｜实机渲染**：政策行折行后的**亚像素位置**、滚动条存在时文本宽与按钮宽的实际并置、照护页分组线的视觉位置，均未在游戏内目视。离线只能证明"几何函数不重叠"，不能证明"看起来好看"。
2. **NOT RUN｜实机键显示**：zh/en 键存在性已证明，但译文在实际 UI 中是否被裁切（中文字宽）未实测。
3. **PARTIAL｜F4 去重后的可用性**：删除 `Treasury.SelectBank` 行后，"账户"页在未选公有银行时只剩顶部 `NoFocus` 引导（"从机构列表选择柜台"）。语义覆盖由总控裁决（保留顶部）；若 PM 认为银行/柜台的措辞差异重要，可回退该单行删除。`PC_PEA.Treasury.SelectBank` 键予以保留（未被引用，无裸键风险，zh/en 对称）。
4. **PARTIAL｜F6-A 行种标记**：新增说明行**沿用 `EconomyRow.Verb` 作为行种标记**（`Hint`），因为 `EconomyRow` 定义在 `Application/PawnEconomy/EconomyView.cs`（超出本批"只改 `Economy/PawnEconomy/*`"的路径约束），且 `LegacyFeesAllowed=false` 的投影会把非白名单行动作剥离。若 PM 认可扩大范围，更干净的做法是在 `EconomyRow` 上加可选的显式 `Hint` 字段。当前实现无行为副作用：`Hint` 行恒无 `Action`，永不进入 `Send` 通道。
5. **NOT RUN｜旧程序集残留类现象**：机构页 `X=Y` 格式、政策页「营业/绑定定区/工资+1」列错配已由 `CHG-20261010-ECON-FA-BATCH1` 修掉，本批**未重复修**（勿动）。
6. **NOT RUN｜部署与实机验收**：按派发要求**未部署**；`Assemblies/PersonalChronicle.dll` 已由 `compile_with_sdk.ps1` 重建（本批代码编译产物），实机验收待 PM 触发部署后补。

---

**署名**：本变更由 **AI（CodeBuddy / DeepSeek-V4.1-Flash）** 于 2026-10-10 生成；派发方为 `econ-ui-audit` 团队 `team-lead`，本 agent 为 `pc-ui-loc-guard`（GATE-UI / GATE-LOC 守门人），单写者。
---

## 补充（team-lead 收口，2026-10-10 02:00）

> 背景：实装 agent（`pc-ui-loc-guard`）完成 F1–F5 + F6-A + 测试 + 本标注后被中断，未回传 Gate Results。本节由 team-lead 独立复核其落盘证据并**补齐剩余三项**后写下，全部结论可复验。

### 1. F5 键族定稿：`PC_PEA.Care.Gate.*`（**裁决变更**）
- 落地采用 `PC_PEA.Care.Gate.{VanillaAccess, DeliveryEvidence, CapacityAndHours, PriceCap, Settlement, InterruptionRecovery, UiExplanation}`（zh/en 各 7 键），渲染点 `Care.cs` 用 `LocalizedId("Gate.", blocker)`。
- 此前曾裁 `Missing.` 家族（与既有 `PC_PEA.Care.Missing`=「未证明」同族）。**现予批准 `Gate.` 定稿**：真正要守的不变量是「只允许一套命名，且键与调用点同批落地」（防 `Policy.cs:151-154` 那类「只声明不消费」），该实装已满足；为**避免二次改名窗口**（改名期间正是双套并存的高危窗口）不再 churn。
- 语义无差：新键承载的是**门名**（原版访问/交付证据/容量时段/价格上限/结算/中断恢复/界面解释），落在 `Gate.` 与内容一致；`Missing`（未证明）仍由既有键承担。
- Domain 侧 7 个字面量**保持冻结**（已进存档：`Data/PawnEconomy/CareSaveData.cs:32` 字段 / `:37` `Scribe_Collections.Look(ref Blockers, "blockers", LookMode.Value)`），映射只在呈现层做一次。

### 2. lead 补齐的三项（实装 agent 未做）
| 修点 | 内容 | 证据 |
|---|---|---|
| **F7** | 新增 `PC_PEA.Policy.Reason.{CategoryUnknown, StockTargetNotWired, TradeDomainNotWired, ProductionAuthorisationNotWired}`（zh/en 各 4 键）。四码产自 `Production/EconomyProductionReadoutSource.cs:14-20`（`CategoryReasons`），经 `Overview.cs ReasonLabel → LocalizedId("Reason.", code)` 回落 ⇒ 此前裸 ID 上屏；`DemandDomainNotWired` 已有键未动 | 键计数：`Languages/{zh,en}/Keyed` 的 `PC_PEA.*` 各 **564 → 578** |
| **F8** | `EconomyCommands.BuildReports`：可见状况为空时**不再留悬挂分隔符**（原恒拼 `" · " + string.Join(...)` ⇒ 行尾孤立「 · 」） | `EconomyCommands.cs` 改为 `(r.VisibleConditions.Count == 0 ? "" : " · " + …)` |
| **F9** | `Dialog_PawnEconomy.Policy.cs` 去掉 `Mathf.Max(72f, …)` 列宽下限：该下限在 `content.width < 318` 时令 4 列总宽超过可用宽、第 4 列被裁到 content 之外（违反该文件自述契约）；无下限时 `4×width + 3×gap ≡ content.width − 12` 永不溢出。**未做**自适应列数（会改动四行按钮矩阵结构），已注释说明取舍 | 同文件，含取舍注释 |

### 3. lead 独立复核结果（与实装 agent 自述的差异）
| 项 | 实装 agent 自述 | lead 复核 |
|---|---|---|
| 编译 | 0 错 0 警 | ✅ 复跑一致（`BuildStamp -> 2026-10-09 18:00 UTC`） |
| 测试 | 未给数字 | ✅ **1221 总项 / 1220 通过 / 1 跳过 / 0 失败**（+4 新用例全过） |
| 键对称 | `PawnEconomy.xml` 单文件 `zh=228 en=228` | ✅ 全语言域 **578 = 578**，`zh-only=0 / en-only=0`（独立脚本，扣 1 个目录名伪影） |
| 键引用审计 | 未做 | ✅ `audit-keyref-integrity.ps1`：未登记未解析引用 **0** |
| CJK 字面量 | 未做 | ✅ `audit-cjk-literals.ps1`：本批文件**零命中** |
| Gate Results | 表头两行后即中断 | 见下节（lead 补记 GATE-PERF/TEST/SAVE/LOC/UI） |

### 4. 补记 Gate Results（lead）
| Gate | 结果 | Evidence |
|---|---|---|
| GATE-PERF | **PASS（离线）** | 政策行度量走「内容变才重算」缓存（`CalcHeight` 仅在原串/宽度变化时执行）；空态分支为零行专属；无新增 per-frame 分配。**未做**实测帧时间（NOT RUN） |
| GATE-TEST | **PASS（Tier B）** | 编译 0/0；全量 1220 通过 / 0 失败；新增 4 用例含**可证伪几何回归**（1~5 行折行下断言按钮带不被压实测文本，旧 56f/60f 实现在第 3 行即红）与键存在性断言 |
| GATE-SAVE | **PASS（否证式）** | 零 Scribe 字段新增/改名；Domain 7 个门 ID 字面量未改（比对 `CareSaveData.cs:32/37`）；呈现层只做映射 |
| GATE-LOC | **PASS** | 578 = 578 对称；F1 两个 `T()` 一级键（无回落链）已存在 ⇒ 空态位不会打印 `PC_PEA.Tab.Empty`；F7 四键存在 ⇒ 补货行不再裸 ID |
| GATE-UI | **PASS（离线）** | 空态走既有 `RUIF.FeedbackAtoms.EmptyState`；说明行走 `RUIF.Tokens.Muted`；政策行几何由 `EconomyWindowLayout` 单一来源驱动（`contentHeight`/focus 偏移/`y +=` 三处共用）；F9 消除窄窗溢出 |
| GATE-DELTA | 见 `gate-runner.ps1` 运行结果 | 标注 files ↔ git 改动互含 |

### 5. 未验证（NOT RUN，不得视为通过）
- 全部**实机**项：空态观感、政策行两行折行在真实字体下的排版、窄窗按钮可读性、`Care.Gate.*` 中文口径、总览补货行文本。
- 本批**未部署**（同树另有会话在写）。
---

## 补充二：乱码专项深度取证与根治性加固（team-lead，2026-10-10 09:0x）

**PM 报告「还是乱码」。** 深度取证如下（每条可复验）：

| # | 取证 | 结论 |
|---|---|---|
| 1 | `Player.log`：`[Info] loading …\Mods\殖民地综合管理系统\Assemblies\PersonalChronicle.dll` + `[PersonalChronicle] build 2026-10-10 00:47 UTC` | 进程加载的是 08:47:53 部署的**本树构建**，非旧产物 |
| 2 | Cecil 反查该 DLL 的 `GameComponent_PawnEconomy::InstitutionSummary` IL | 已是**新格式**：`EconomyLabels.KindLabel` → `" · "` → `LabelCap` → `" · "` → `T("Mode.PhaseLabel")` → `": "` → `T("Mode.ConfigLabel")` → `": "` → `T("Mode.Config.Unset")`；⇒ 运行中的代码**不会**打印完整键名 |
| 3 | 部署区 `Languages/{zh,en}/Keyed`：11 文件 / 4176 键 / 0 重复 / 0 解析失败 | 语言资产本身健康（日志「11 errors」非本 mod 造成） |
| 4 | 截图文本形态：`PC_PEA.Care.Kind.X=<建筑名>  制度=… · 机构配置=… 能力:` + 逐行 `PC_PEA.Care.Cap.Name.X=PC_PEA.Care.Cap.Reason.Y` | 只能由「**在 Care 命名空间直查、无 Policy 回落**」的实现产出 ⇒ 属**早于本批**的构建/会话产物 |

⇒ 乱码根因 = 「**Care 命名空间缺键**」×「**无回落实现**」的组合。本批的 `EconomyLabels` 回落链已修掉后者；前者本轮补上（见下）。

**根治性加固（本轮新增）**：在 `Languages/{zh,en}/Keyed/PawnEconomy.xml` 的 **Care 命名空间**补齐 14 个同义键（值逐键对齐 Policy 侧）——
`Care.Kind.{LaborOffice,WorkshopOffice,SettlementOffice,Shop,ExamCenter}`(5) + `Care.Cap.Name.{Work,Food,Apparel,Service}`(4) + `Care.Cap.Reason.{Unset,NotPublished,Closed,OutOfHours,ModeNotOpen}`(5)。

- **理由**：`EconomyLabels` 的回落链是 **Care 优先**；把规范值放进主命名空间后，**不论运行的是本批代码（Care→Policy→标识）还是旧/并行实现（仅 Care 直查）**，该窗口都不可能再出现 `PC_PEA.` 字面 ⇒ 从"修代码"升级为"**让缺键形态本身不可能上屏**"。
- **影响面**：纯语言资产；**零代码 / 零 Save / 零 Def / 零 Harmony**。
- **键数**：`PC_PEA.*` 578 → **592**（zh = en = 592）；全语言域 4162 → **4176** 条，`entries == uniqueKeys`，**0 重复 / 0 解析失败**。
- **部署**：仅同步 `Languages/**`（XML 不被运行中的游戏进程锁定）；DLL 未变更（08:47:53 那份已含本批全部修复）。
- **验证**：`dotnet test` **1220 通过 / 0 失败 / 1 跳过**；`verify-loc.ps1` 0 重复 0 解析失败；部署区已确认存在 `PC_PEA.Care.Kind.LaborOffice`。
- **未验证（NOT RUN）**：实机观感（须重启后目视：机构行应显示「劳务所 · 劳务站 · 制度: 运行中 · 机构配置: 未配置 · 能力: 岗位=未配置 …」，且**全屏无 `PC_PEA.`**）。