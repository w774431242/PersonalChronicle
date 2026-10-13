---
change_id: CHG-20261005-POSTLAND
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle, Defs, Languages]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopPostLandingService.cs
    type: A
    symbols: WorkshopPostLandingService (整文件：Land / BuildShopPool / ResolvePostOf / PickWorkType)
    lines: 1-260
  - path: Source/PersonalChronicle/Automation/WorkshopPostDef.cs
    type: M
    symbols: WorkshopPostDef.landingExempt
    lines: 92-320
  - path: Source/PersonalChronicle/Automation/AutoAssignService.cs
    type: M
    symbols: AutoAssignService.WriteCell (private→internal，返回退让标记)
    lines: 190-235
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Posts.cs
    type: M
    symbols: WorkshopManageView.DoLandPosts, WorkshopManageView.postLandResult, WorkshopManageView.DrawPostsPage
    lines: 22-35, 345-395
  - path: Defs/WorkshopPosts.xml
    type: M
    symbols: WP_Operator.landingExempt
    lines: 55-60
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: PostLandButton, PostLandTip, PostLandDone (新增)
    lines: 449-456
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: PostLandButton, PostLandTip, PostLandDone (新增)
    lines: 445-452
---

# 变更标注 · CHG-20261005-POSTLAND

## 背景（玩家 2026-10-05，附「岗位」页截图）

> 「现在将这些岗位进行落地，**除操作员工暂时不动**，其他都**对应原版工作类型**进行落地。」

截图：岗位页 11 行（操作员工 / 设备技术 / 燃料(物料) / 仓储 / 研究(研发) / 医务 / 安保 / 农艺 / 采矿 / 驯兽），
每行已有「岗位 → 原版工作类型」映射，右侧统一标着「规划中·未接管」。

## 一、口径确认（动手前已向玩家确认，两条都有破坏性歧义）

| 问题 | 玩家选择 |
|---|---|
| 落地口径 | **按岗位勾选优先** —— 担任某岗位的成员，其岗位对应的原版工作被**勾选并设为优先档**；**其它工作一律不动** |
| 多个工作类型时 | 「存在多个原版工作类型的，**按车间类型进行自动分配单个**工作类型。**是精准分工**」 |

> ⚠ **为什么要先问**：另一种理解（"接管清零"）在本工程会**直接造成灾难** ——
> PC 目前只实现「值勤」（`PC_WorkshopDuty`）与「用餐」（`PC_MealGiver`）两个 Job，
> **没有任何生产 Job**（建造 / 研究 / 种植 / 采矿 / 驯兽）。一旦清零成员的原版勾选，
> 殖民地会立刻失去这些服务且无人替代。这正是 `WorkshopPostDef` 与 EXC-2026-016 反复警告的场景。

## 二、实现：一条链，每个人只写一格

```
成员 → 岗位（玩家覆盖优先，否则分配器结果）
     → 岗位承接的工作类型 ∩ 车间类型声明的工作类型池
     → AutoAssignPriority 同一公式按技能挑出最优的那一个（精准分工）
     → AutoAssignService.WriteCell（勾选 + 按技能定档）
```

1. **`WorkshopPostLandingService`（新增）** —— Automation 层，与 `AutoAssignService` 同级授权；
2. **车间类型池** = 该 `WorkshopTypeDef` 的四个任务类型组（production / maintenance / qualityCheck /
   standby）**并集** + 设备通道。取并集而非按岗位 `dutyTaskTypeKey` 取单组：那个键是"值勤任务类型"，
   与"本车间关心哪些工作类型"不是同一维度（按单组取会漏，例如农艺车间在 Production 组声明 Growing
   而农艺员工的 dutyTaskTypeKey 未必是 Production）。**车间类型未指定 ⇒ 空池 ⇒ 不筛**（保证"没设类型也能落地"）；
3. **挑单个**：用 `AutoAssignPriority.TryCompute` 的同一公式算档位，取**档位最小者**（数字小 = 更优先 =
   他最擅长/最该做的那项）；并列时按 defName 序数排序，**结果可复现**（不依赖 Def 加载顺序）；
4. **写入**：复用 `AutoAssignService.WriteCell`（本批把它从 `private` 提升为 `internal`，
   返回"是否发生 M1 退让"）—— **同一套退让 + 记账 + 钳位**，不另写一份（GOV-005）；
5. **操作员工豁免**：`WorkshopPostDef` 新增 `landingExempt` 字段（**默认 false = 参与落地**），
   只在 `WP_Operator` 上写 `<landingExempt>true</landingExempt>`。
   默认值这样定是**反语义**的：新增岗位默认生效，漏写字段不会让岗位静默失效。

### UI

岗位页新增「**按岗位落地**」按钮（**点击事件**，不是绘制路径）+ 一行结果反馈
（`已落地 N 人（无岗位 A · 无可用工作 B）`）+ 悬停说明。
页面原有的"只读展示"承诺**仍然成立**（它约束 Refresh / 绘制路径），类注释已补上这条边界例外。

## 三、安全边界（本批最重要的一节）

| 边界 | 说明 |
|---|---|
| **不清零** | 本服务**只写**该岗位选中的那 1 个工作类型；**不碰其它任何格位**。`SetPriority(x, 0)` 的唯一授权点仍只在接管通道（EXC-2026-016） |
| **不接管** | 右侧「未接管」标记**保持不变** —— 它说的是"PC 能否独立交付该 Job"，与本操作是两件事（悬停文案已讲清） |
| **硬红线** | 搬运 / 清洁 / 医疗 / 灭火在 `AutoAssignPriority` 里属保护名单（`ProtectedFloor = 2`）⇒ 物料 / 仓储 / 医务 / 安保四个岗位**只让这些工作更靠前，永不取消勾选** ⇒ 安全 |
| **玩家手动优先** | 写入走 `AutoAssignLedger` 记账 ⇒ 玩家之后在 Work 面板手动改过的格位，PC **不再写**（既有裁决） |
| **幂等** | 目标档位与当前一致 ⇒ 不写、不记账；重复点击无副作用 |

## 存档影响：**无新增 Scribe 键**，不需要 Migration / bump `schemaVersion`

- 记账复用既有 `ChronicleGameComponent.AutoAssignLedger`（`AutoAssignService` 已在用）；
- 新增的 Def 字段 `landingExempt` 是**静态配置**（不入档）；
- 唯一持久影响是 `Pawn_WorkSettings` 的档位值（原版自己落盘）。

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（`_scripts/compile_with_sdk.ps1`） |
| XML | 已验证 | Automation 中/英 + **WorkshopPosts.xml** 均可解析 |
| 部署 | 已验证 | Defs 31 / Languages 39 / Assemblies 1 同步 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-POSTLAND.md` |
| 实机 | 未验证 | 见下 |

### 实机验证要点（重启后）

1. **前置**：车间**已指定类型**（岗位页若提示"尚未指定车间类型"，先指定）；
   原版「工作」面板需开启**手动优先级**（否则档位写了不被读取，属既有披露）；
2. 打开 `车间管理 → 岗位` ⇒ 顶部出现「**按岗位落地**」按钮；
3. 点它 ⇒ 右侧出现结果（如 `已落地 3 人（无岗位 1 · 无可用工作 2）`）；
4. **核对原版工作面板**：担任「设备技术员工」的成员 ⇒ 「建造」被勾选且档位靠前；
   担任「研发员工」⇒ 「研究」被勾选提档；**其余工作栏位保持不变**；
5. **操作员工**（`WP_Operator`）⇒ 其成员的原版工作**完全不变**（豁免生效）；
6. 重复点一次 ⇒ 结果里 `已落地` 应为 0（幂等，值已正确）；
7. 手动在 Work 面板改某格 ⇒ 再点落地 ⇒ 该格**不再被改写**（玩家手动优先）；
8. `Player.log` 无新的 `PersonalChronicle` 异常。

## 残留风险

1. **车间类型未声明池 / 声明不全**：池非空时会过滤掉"车间没声明的工作类型" ⇒ 若某车间类型漏声明
   某工作类型，对应岗位的该工作不会被落地（当前**无 UI 提示**说明"被车间类型过滤掉了"）。
   建议后续在结果里区分"被池过滤"与"岗位本身没有"；
2. **一人一岗一项工作**：精准分工是按玩家口径实现的（每岗 `MaxHolders = 1`，每人只落 1 项）。
   若将来想让一个人兼顾多项，需要新口径；
3. **岗位名与仓库 XML 不一致**（玩家截图显示"燃料员工 / 研究员工 / 驯兽员工"，仓库为
   "物料员工 / 研发员工 / 畜牧员工"）：说明**玩家游戏内跑的是较早版本的 Defs**。
   本批未改任何 label（`postKey` / `defName` 一律不动，存档存的是 postKey ⇒ 安全）；
   重启部署后应以仓库 XML 的 label 为准；
4. **未同步更新 `PostNotDeliversTip` 的措辞**（它写"your vanilla work tab is untouched"，
   在新按钮存在后略显绝对）。已在新增的 `PostLandTip` 里讲清边界，故本批不动旧文案以缩小改动面。
