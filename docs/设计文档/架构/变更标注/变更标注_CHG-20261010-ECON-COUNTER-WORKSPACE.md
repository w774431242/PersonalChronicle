---
change_id: CHG-20261010-ECON-COUNTER-WORKSPACE
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedAndDeployedByThisBatch
scope: 经济机构「柜台工作台」—— 5 台机构各自独立窗口（RUIF 原子 + 共享行族 + 零特判），入口按机构类型分派；并统一机构类型术语
rule_ids: AI-001, AI-003, AI-005, AI-011, GOV-003, GOV-005, GOV-010, GOV-012, LAND-002, LAND-003, LAND-006, LAND-008, LAND-010, ARC-013, UI-003, UI-007, LOC-001, PERF-002, TEST-001, DELTA-001
files:
  - path: Source/PersonalChronicle/Application/PawnEconomy/WorkspaceView.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyRowList.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkspaceQueries.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Policy.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Needs.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorld.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyCommands.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyUiKit.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWindowLayout.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  - path: docs/UI预览/经济机构工作台/机构工作台预览.html
    type: M
---

# 经济机构「柜台工作台」实装 · CHG-20261010-ECON-COUNTER-WORKSPACE

**ChangeID**：`CHG-20261010-ECON-COUNTER-WORKSPACE`｜**日期**：2026-10-10｜**作者**：AI（CodeBuddy / DeepSeek-V4.1-Flash）
**蓝本**：`docs/UI预览/经济机构工作台/机构工作台预览.html`（同批按实装修正）
**当前程序集**：`Assemblies/PersonalChronicle.dll` `2026-10-10 18:18:14` · 2,123.5 KB（`BuildStamp -> 2026-10-10 10:18 UTC`）
**部署**：**已部署**（末次 2026-10-10 18:31:06，`_scripts/deploy-to-game.ps1` → `RimWorld\Mods\殖民地综合管理系统`，复制 239 文件 <About 13 / Assemblies 1 / Defs 37 / Fonts 8 / Languages 71 / Patches 3 / README 1 / Textures 105>；目标 DLL `18:30:49` 与工程一致；被覆盖文件备份到 `_backups/release-deploy/deploy_20261010_183106_103`）
**Save 变更**：**无**（零 Scribe 字段、零 schema、零 Def 变更、零 Harmony）

---

## 一、目标与非目标

**目标**：玩家点选任一经济机构柜台 ⇒ 打开**该柜台自己的窗口**（不再是唯一聚合窗口）；窗口基于 RUIF 原子做布局，复用既有读模型与唯一写通道。
**非目标（本轮明确不做）**：① 不改命令语义 / 账本 / 写入路径；② 不动 Def 与存档；③ 不拆跨机构页（账户 / 账本 / 报告 / 需求 / 照护 / 总览仍留聚合窗口）；④ 不放开任何旧动词。

## 二、改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | diff 摘要 |
|---|---|---|---|
| Application/PawnEconomy/WorkspaceView.cs | A | `WorkspaceView` / `WorkspaceKpi` / `WorkspaceAction` / `WorkspaceTone` / `WorkspaceVerb` | 读快照契约（零 UI 依赖，可离线断言） |
| Economy/PawnEconomy/EconomyRowList.cs | A | `EconomyRowListState` / `EconomyRowList.{Flow,RowButtonWidth,PageCount,Measure,Draw}` | **共享行族**（测量 / 绘制 / 分页 / 空态唯一实现）；差异经三个回调注入 |
| Economy/PawnEconomy/EconomyWorkspaceQueries.cs | A | `GameComponent_PawnEconomy.QueryWorkspace` | 唯一读入口：按机构类型组装 KPI / 行动 / 明细行（UI 零特判） |
| Economy/PawnEconomy/Window_InstitutionWorkspace.cs | A | `Window_InstitutionWorkspace` | 柜台工作台窗口（U1 头部 / U2 KPI / U3 行动 / U4 明细） |
| Economy/PawnEconomy/Dialog_PawnEconomy.cs | M | `DrawRows` → `EconomyRowList.Draw`；新增 `DrawRowButton`、`initialTab` 构造、页签索引常量 | 行族三方法外移为薄调用（净减 ≥100 行）；深链入口与政策定位 |
| Economy/PawnEconomy/Dialog_PawnEconomy.Policy.cs | M | `PolicyTab` 改 `internal` | 供深链使用，调用方不再硬编码 7 |
| Economy/PawnEconomy/Dialog_PawnEconomy.Needs.cs | M | 注释 | 修正指向已删除方法的陈旧注释 |
| Economy/PawnEconomy/EconomyWorld.cs | M | `GetGizmos()` | 按 `EconomyInstitutionExtension.kind` 分派；缺 kind 回落聚合窗口 |
| Economy/PawnEconomy/EconomyUiKit.cs | M | `ToneColor(WorkspaceTone)` | 第 5 张映射表（KPI 强调程度），绘制点不得自取色 |
| Economy/PawnEconomy/EconomyWindowLayout.cs | M | 工作台几何常量 | 几何唯一来源（含"4×1 恒为宽档"的推导注释） |
| Languages/{ChineseSimplified,English}/Keyed/PawnEconomy.xml | M | 新增 **24** 条 `PC_PEA.Workspace.*`；`Policy.Kind.*` / `Care.Kind.*` 各 5 条术语统一 | 中英同批；术语与 Def label 同词 |
| docs/UI预览/经济机构工作台/机构工作台预览.html | M | 5 个窗口的行动区与明细行 | 按实装修正（旧动词按钮移除，只留真实放行动词） |

### 关键设计（可复核）

1. **零特判（LAND-010）**：`Window_InstitutionWorkspace.cs` 内 `InstitutionKind` 命中 **0**；类型差异唯一组装点在 `EconomyWorkspaceQueries.cs` 的 `switch (kind)`。
2. **同一 helper**：两个窗口的明细区同走 `EconomyRowList.Draw`（聚合窗口传机构卡回调与行尾按钮；工作台传 `null,null,null` = 只读）。
3. **F4（不给点了没用的按钮）**：`Execute` 首行 `if (!LegacyFeesAllowed) return false;`（`EconomyCommands.cs`，该值恒 `false`）⇒ 旧动词（撤下 / 解约 / 上架 / 指派医生 / 预约体检 / 补发欠薪）全部不可用。工作台明细行**不挂按钮**；行动区只列 `Policy`（深链政策页并定位该柜台）/ `OpenAccounts`（深链账户页）/ `Reverify`（故障时，就地执行）。
4. **写通道唯一**：工作台只读 `QueryWorkspace`；唯一写动作是既有的 `ReVerifyInstitutionMode()`；政策与国库命令的参数一律由聚合窗口既有实现构造（工作台不复制参数语义）。

## 三、计划与交付的差异（如实登记）

| 计划（预览/审核阶段） | 交付实装 | 原因 |
|---|---|---|
| 「1 主类 + 5 个 kind 切片 + 1 张配置表」 | **1 个窗口类 + 组件侧 `switch (kind)` 组装**（不设切片与配置表实体） | 机构差异全在数据组装层 ⇒ 切片会变成 5 个近空文件；ARC-013 与 AI-003 均支持更少抽象 |
| 结算处窗口挂「缴存 / 提取」直接命令 | 改为**深链账户页** | 直接构造 Treasury 命令参数会产生第二处参数语义（id/key 口径），深链零风险 |
| KPI「窄窗降 2×2」 | **恒 4×1**（无档位分支） | 窗口下限 760 ⇒ 单卡 ≈177 ≥ LAND-006 阈值 160 ⇒ 全部可达尺寸均为宽档；已在预览"未做项"登记 |

## 四、验证证据

| 项 | 结果 | 证据 |
|---|---|---|
| 编译 | **0 错 0 警** | `compile_with_sdk.ps1`（csc 无诊断输出）；期间修正 3 处编译错误（缺 using ×2、`UI` 命名空间歧义 CS0234 → `Verse.UI`） |
| 离线测试 | **1272 通过 / 0 失败 / 1 跳过** | `dotnet test Tests/PersonalChronicle.Tests.csproj`（EXIT=0） |
| **Cecil 静态核实**（编译之外的独立一步） | **PASS** | `Dumper type … full`：6 个新类型全部命中（`Window_InstitutionWorkspace` / `EconomyRowList` / `EconomyRowListState` / `WorkspaceView` / `WorkspaceKpi` / `WorkspaceVerb`）；`[internal] WorkspaceView QueryWorkspace(Map, String)` 存在（简模式不列 internal ⇒ 首轮"未命中"是工具口径而非缺类型）；`refs QueryWorkspace` 反查出真实引用点 `Window_InstitutionWorkspace::Refresh`（非死代码）；`Building_EconomyInstitution.StockCapacity` 命中 |
| 编译被锁事件（如实登记） | 已处置 | 首轮收口编译报 `CS2012`（DLL 被映射占用，前次 testhost 残留）⇒ **当轮 `dotnet test` 的 1272 通过属旧 DLL 假绿，已作废**；锁释放后重编重测（`BuildStamp 2026-10-10 10:22 UTC`）方为有效证据 |
| GATE-UI | **PASS WITH EXCEPTION**（无 P0/P1） | `pc-ui-loc-guard`：零特判 ✅ / 同一 helper ✅ / 无残留第二套行几何 ✅ / F4 ✅ / Theme Token 零裸色 ✅ / PERF 绘制期零 Measure.Width ✅；例外 2 项（LAND-008 头部悬停缺失 → **本批已补**；Tier-S 未实现 → **已登记**） |
| GATE-LOC | **PASS WITH EXCEPTION**（无 P0/P1） | 键 24×2 中英全对齐、无孤儿/无缺键、键值非键形；术语与 Def label 逐词相等（中英各自核）；例外 1 项（`Policy.Hint.BindZoneFirst` 旧称「车间管理所」→ **本批已改**） |
| GATE-ARCH | **PASS**（无 P0/P1） | `pc-arch-gatekeeper`：分层与依赖方向合规（`WorkspaceView` 零 RimWorld/Unity 依赖）、无循环依赖、ARC-013 逐文件行数达标（新建最大 231 行；`Dialog_PawnEconomy.cs` 由 ≥259 → **151** 行）、P1 守门通过；Review 触发项 `Harmony=N / Hardcoding=N / Cross-Mod=Y（已登记硬依赖）/ StaticMutable=N` |
| 集群门禁 | **PASS** | `verify-cluster.ps1 -Blocking` → `PASS=7 / BLOCKED=0 / Gate覆盖=10/10`，runId `d591bffaec6146c083356d258a2a6a99` |
| GATE-DELTA | **PASS** | `gate-runner.ps1 -Base HEAD -ExcludeFromFile .codebuddy/tmp/workspace-out-of-scope.txt -Annotation <本文件> -Blocking`（`HEAD=21fd0a9 ≠ origin/main=dbcf8f8` ⇒ 必须 `-Base HEAD`） |
| 游戏内验证 | **NOT RUN** | 无运行环境；需重启游戏后点选 5 台柜台逐一核对（见 §六 验收步骤） |

## 五、已收口的 Gate 例外（本批内）

1. **LAND-008**：头部 4 处可截断文本（标题 / 类型名 / 阶段芯片 / 营业芯片 / 时段）全部补 `TooltipHandler.TipRegion` 悬停全文。
2. **LOC 旧术语**：`PC_PEA.Policy.Hint.BindZoneFirst` 的「车间管理所」→「工坊管理处」。
3. **死常量**：`WorkspaceRowsTop` / `WorkspaceKpiMinWidth` 零引用 ⇒ 删除，保留推导注释。
4. **注释口径**：`EconomyWorld.cs` 注释与实现对齐（仅"未声明 kind"回落）；`Dialog_PawnEconomy.Needs.cs` 陈旧注释修正。
5. **文案指向不存在按钮**：商店页不再沿用 `StockHint`（其指引的入库按钮写路径已关闭）。
6. **账户键单点化**：`ClinicAccountKey(anchorKey)` 收进单点并注明"若域侧提供访问器应改为委托"。

7. **容量字面量统一（收口轮）**：`64` 曾三处各写 —— 入库门（`EconomyCommands.cs`）、搬运入库（`EconomyWorld.Store`）、工作台 KPI 显示（`EconomyWorkspaceQueries.cs`）⇒ 收为**唯一常量** `Building_EconomyInstitution.StockCapacity`，并注明"与列表显示上限 `Take(64)` 语义不同，不要合并"。

8. **Gizmo 图标（实机反馈修复，2026-10-10）**：用户截图显示 5 台柜台的「经济机构」按钮是**品红底红叉**。根因（IL 实证）不是贴图丢失 —— `Verse.Command::DrawIcon` 在 `icon == null` 时**直接回落 `BaseContent.BadTex`**（该占位图同时用于"未提供图标"与"贴图丢失"，故外观像故障）。本按钮**从未设过 icon**（**既有表现，非本批引入**；本批只是改了它的 action，用户因此注意到）。修法：`icon = def?.uiIcon`（与柜台工作台窗口头部同一张图；Def 未声明 uiIcon 时为 null ⇒ 仍回落原占位，无回归）。

**未收口（登记）**：Tier-S 档位分支（见 §三，已论证不可达）。

## 六、回滚与验收

**回滚**：还原 §二 13 个文件 → `compile_with_sdk.ps1` → `deploy-to-game.ps1`（`_backups/release-deploy/deploy_20261010_181851_694` 内有部署前副本）。零存档影响。
**实机验收步骤（待执行）**：重启 RimWorld → 分别点选 劳务站 / 工坊管理处 / 工资结算处 / 生活用品商店 / 体检中心 的柜台 → 确认各自打开独立窗口（标题 = 建筑名，KPI 四卡随类型变化）→ 点「机构政策」确认落到政策页并**滚动定位到本柜台卡片** → 结算处点「账户/需求」确认落在账户页 → 悬停标题/芯片/时段确认 tooltip → 缩放 UI 或改窗口尺寸确认无溢出/无重叠。

### 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖实际改动全部文件（13/13）
- [x] 清单范围 ⊆ Scope
- [x] 每个文件标注类型与涉及符号
- [x] Change ID 与 Gate 结论、预览蓝本可互链
- [x] 无调试代码 / 注释掉的死代码 / TODO 残留
- [x] 已用 `gate-runner.ps1` 自动校验
