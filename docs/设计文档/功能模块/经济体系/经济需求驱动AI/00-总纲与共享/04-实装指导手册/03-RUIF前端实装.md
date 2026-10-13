# 03 RUIF 前端实装

> 后续细化：需求观察与玩家调整闭环见[07-需求驾驶舱与政策反馈](07-需求驾驶舱与政策反馈.md)。制度总开关按 v0.7 只放 Mod 设置，本文早期“政策页总开关”不再作为第二入口；政策页保留机构经营参数。

本册落实“前端必须基于 RUIF 框架 Mod”。版本基线与验证见[总册](00-总册与实施路线.md)、[验证记录](05-证据索引与验证记录.md)。业务写入规则见[数据册](01-数据模型与源码依据.md)。

## 1. 硬依赖与工程接入

当前 `Source/PersonalChronicle.csproj` 已无条件引用安装版 RUIF，并设置 `Private=false`；`About/About.xml` 已声明 `PersonalChronicle.RUIFramework`。正式实装沿用现有依赖，不把 RUIF.dll 复制进消费者 Assemblies，也不在缺失时改用自研 Widgets 组件。

最低能力基线设为本次核验的 1.3.0。启动阶段通过现有 `Archive/UI/RuifBridge.cs` 或等价集中桥校验能力，UI 不每帧反射。`Api.VersionString` 是编译常量，读取它不能单独证明运行时实际 DLL 版本；运行时要通过非内联版本入口/程序集身份及启动日志核对。本手册中的 DLL 元数据取证读取的是磁盘文件，不能代替游戏实际加载证据。

允许直接使用：Verse.Window/MainTabWindow 宿主、Unity Rect/Vector2、标准输入事件在窗口边界处理。**所有通用业务组件使用 RUIF**。不得在消费者中新写表格、按钮、复选框、滚动条，再只套一层 RUIF Tokens 声称合规。RUIF 自己内部使用 Widgets 不构成消费者违规。

## 2. 页面放在哪里

建议在现有系统中心增加“个人经济”能力入口/子页，保留当前库存、市值与统计观察内容。先扩展 Application 可选契约，不向所有 Provider 的核心接口强加新成员。

已有入口：`Application/SystemCenters/IColonyEconomyDataProvider.cs`、`SystemCenterDataRegistry.cs`、`MasterControl/MasterControlTabWindow.Economy.cs`。其现有 `BuildColonyEconomy/BuildStockGallery` 不等于已经存在个人经济页。

拟议契约：

```csharp
// 契约草图；本次不注册到正式项目，不是已经存在的 API。
interface IPawnEconomyDataProvider
{
    PawnEconomyPageSnapshot BuildPawnEconomy(PawnEconomyViewRequest request);
}

interface IPawnEconomyCommandService
{
    EconomyCommandResult Execute(EconomyCommand command);
}
```

实际接口需要完整命名空间和 DTO 后再编译；已提供的独立 RUIF 示例只验证真实组件用法，不声称此契约草图已实现。

## 3. 五个首版业务页

| 页 | 展示内容 | 用户动作 | RUIF 组件 |
|---|---|---|---|
| 总览 | 公共余额、个人余额合计、冻结、欠薪、活跃目标、最近成交/失败 | 进入异常订单/个人详情 | `PanelAtoms.StatRow`、`DataAtoms.StatTable`、`FeedbackAtoms.AlertBanner` |
| 个人 | 选中 Pawn 的钱包、收入/支出、储蓄、合同、当前目标、实际衣物 | 选择 Pawn、暂停自主经济、刷新详情 | `SearchField`、`PrimaryButton`、`StatRow`、`StatTable` |
| 目标 | 目标阶段、缺口、预算、候选得分、拒绝原因、付款/使用分离的轨迹 | 暂停、继续、重新评估、查看订单 | `CapacityBar`、`StatTable`、`PrimaryButton`、`EmptyState` |
| 市场 | 柜台、真实资产、固定价、原版估值、挂牌修订、可售/预订 | 选择资产、上架/暂停/撤回、编辑下一修订价格 | `SearchField`、`StatTable`、`StatRow`、`Stepper`、`PrimaryButton` |
| 政策 | 总开关、许可人群/时段、工资档、公共预算、服务白名单、欠薪状态 | 修改草稿、应用、放弃草稿 | `Checkbox`、`Segmented`、`Stepper`、`PrimaryButton`、`AlertBanner` |

页签使用 `NavAtomsB.LayerSwitch`。合同第一版可放政策页的子区域，不再单开大型企业页面。主视图优先展示玩家能做决策的信息；源码类名、Scribe 保存键、补丁细节留在开发调试模式。

0.2 增补：在上述页面增加“机构”子页/详情入口，呈现营业条件、岗位、队列、价格、付款方和服务结果。劳务申请与体检预约的具体组件映射见[微观机构册](06-微观机构与场所工具.md)，不为每类机构复制一套通用窗口组件。

## 4. 已核对的组件接口

下面是实际接口形状；省略默认值时使用表内参数数量，不能随意缩短 `SearchField`。安装版完整 CLR 签名和重载见[JSON 证据](evidence/本机接口证据.json)。

```csharp
// RUIF namespace; UnityEngine Rect/Color/Vector2.
PanelAtoms.StatRow(Rect r, string label, string value,
                  float? bar01 = null, Color? barColor = null);
DataAtoms.StatTable(Rect r, string[] headers, string[][] rows,
                    float[] shares, float rowH = 20f);
ButtonAtoms.PrimaryButton(Rect r, string label, bool enabled = true,
                         ButtonTone tone = ButtonTone.Default,
                         bool focusRing = false, string controlName = null);
InputAtoms.Checkbox(Rect r, string label, ref bool on, bool enabled = true,
                    ButtonTone tone = ButtonTone.Default,
                    bool focusRing = false, string controlName = null);
InputAtoms.SearchField(Rect r, ref string query, ref string buffer,
                      string placeholder, ref float lastInputTime, float delay = 0.2f);
InputAtoms.Stepper(Rect r, ref int value, int min, int max, int step = 1);
FeedbackAtoms.EmptyState(Rect r, string title, string sub, string cta = null);
FeedbackAtoms.AlertBanner(Rect r, AlertPriority level, string msg, string actionLabel = null);
ProgressAtoms.CapacityBar(Rect r, float used, float capacity, Color? fill = null);
NavAtomsB.LayerSwitch(Rect r, string[] layers, ref int selected);
RUIF.Layout.Column(Rect r, float gap = 0f); // 返回 VStack
RUIF.Layout.BeginScroll(Rect outer, float contentHeight, ref Vector2 scroll,
                       bool showScrollbars = true);
RUIF.Layout.EndScroll();
```

此处为接口速查表达，带限定方法名和分号，**不是可直接粘贴编译的类定义**。可编译用法见 [EconomyRuifExample.cs](examples/EconomyRuifExample.cs)。`Segmented` 存在 3 参数及带 disabled/tone 的重载，正式使用指定已验证的重载，避免与可选参数混淆。

必须理解组件限制：

* `StatTable` 是展示表，没有行点击、排序、筛选、分页或虚拟化。调用者先筛选分页；超过绘制区域的行会被截断，不能把截断当分页。
* 需要选择行时，使用单独的 RUIF 按钮列表/选择区域，再显示该项的静态表格；不在表格上叠自制透明点击层制造隐式控件。
* `SearchField` 的 query、buffer、lastInputTime 由窗口保持；输入变更只更新查询请求，不逐字符扫地图。
* `Stepper` 为 int 编辑器。首版给单价/工资设置合理 int UI 上限，经过校验转为 long 最小单位；不能用它编辑全部 long 账本值并静默截断。
* 固定 Tokens 不等于存在主题服务。不新增皮肤、主题配置或消费端通用控件体系。

## 5. 读模型与命令边界

`PawnEconomyPageSnapshot` 建议包含：

| 分组 | 字段 |
|---|---|
| 状态 | readiness、reasonCode、builtAtTick、dataRevision、policyRevision、mapKey |
| 页头 | 已本地化标题、摘要行、异常数量、刷新时间说明 |
| 列表 | totalCount、pageIndex、pageSize、当前页行 DTO、稳定业务 ID |
| 详情 | selectedPawnKey/goalId/orderId/listingId、统计行、候选解释、失败原因 |
| 动作 | 每个命令的 enabled、disabledReason、expectedRevision、目标 ID |

业务 DTO 使用 ID/标量。最终 UI 投影可含缓存的本地化字符串和 `string[][]`，但不写入存档。UI 不持有可写 AccountRecord、OwnershipRecord，也不直接遍历 GameComponent 真值集合。

命令最少包含 `commandId/type/targetId/expectedRevision/policyRevision/payload`。流程：控件点击产生意图→在安全的窗口事件边界交给 Application→服务重查真值和保护→提交或拒绝→按返回 revision 刷新快照。不能在一次 Layout/Repaint 中各扣一次款。

| 命令草案 | 复核 | 失败反馈 |
|---|---|---|
| SetAutonomyEnabled | Pawn 合法、当前政策修订 | 人群不支持或修订冲突 |
| PauseGoal/ResumeGoal | 目标状态、关联订单、版本 | 正在恢复/目标已关闭 |
| RequestReevaluation | 目标有效、冷却/限频 | 显示下一允许刻 |
| CreateListing/WithdrawListing | 真实物品位置、主人、预订、版本 | 已被取走/正在成交 |
| ApplyPolicyDraft | 所有字段、预算守恒、修订 | 保留草稿并标出错误，不部分应用 |
| RetryRecovery | 仅允许已设计可安全恢复的动作 | 禁止“强制标记成功”掩盖不平账 |

按钮的 enabled 只是提示；即使快照说可点，服务仍要复核。快速双击以 commandId 去重；选中旧行后商品售出，返回 StaleListing 并刷新，不能扣买方的钱。

## 6. 五种就绪状态与错误处理

| 状态 | 页面内容 | 动作许可 |
|---|---|---|
| Loading | RUIF 提示“正在恢复账户/订单”，显示阶段 | 不开放交易/改政策 |
| Ready | 完整列表和详情 | 逐命令判断 |
| Empty | 例如没有合格 Pawn、没有挂牌或没有目标 | 显示明确原因和合适入口 |
| Unavailable | 未选地图、模块关闭、目标移除、能力缺失 | 只允许有意义的切图/启用/返回动作 |
| Error | 不支持 schema、账本异常、恢复失败 | 只读观察和可验证恢复；不自动归零 |

`FeedbackAtoms.EmptyState` 的 CTA 返回值和 `AlertBanner` 动作返回值必须在宿主处理；只画出动作文字却没有响应属于未完成。真正缺失硬依赖时本 Mod 不应正常加载；页面 Error 不能代替 About 声明。

## 7. 布局、分页与缓存

布局基于内容 Rect 和 `RUIF.Layout`，不固定整窗绝对坐标。建议初值：8 像素区域间距、24–28 像素行高、每页最多 12 行，按可用高度进一步缩小。页数为 `ceil(total/pageSize)`；筛选后先 clamp pageIndex，选中项移除后转 Empty。

宽屏采用左侧选择列表、右侧详情；窄屏切为先列表后详情/返回按钮，不把所有字段压成极小字体。分页前后按钮使用 RUIF `PrimaryButton`。长解释放详情或 RUIF 滚动区，`BeginScroll/EndScroll` 必须成对；不能把全部历史账目拼成一个长期增长的 string。

缓存键包含 mapKey、业务 revision、选中 ID、筛选、页码、语言版本。账户/目标事件只使相关缓存失效；搜索只查已建索引/列表投影。OnGUI 不调用全图扫描、全量 LINQ 排序、反射探测或经济评分。

RUIF 表格内部有分配，验收应量测实际 UI 开销；消费者能控制的是快照构建次数、可见行数、字符串格式化和查询成本，不能以“框架宣传零分配”代替实测。

## 8. 本地化键草案

使用独立 `PC_PEA_` 前缀，正式开发登记到项目语言文件。至少配对 SimplifiedChinese/English，并校验占位符一致。

| 键 | 中文草稿 |
|---|---|
| PC_PEA_Title | 个人经济 |
| PC_PEA_Tab_Overview / People / Goals / Market / Policy | 总览 / 个人 / 目标 / 市场 / 政策 |
| PC_PEA_BalanceAvailable / Reserved / WagePayable | 可用余额 / 已预留 / 待付工资 |
| PC_PEA_Goal_WaitingFunds / AcquiredPendingUse | 等待收入 / 已购，待使用 |
| PC_PEA_Action_Pause / Resume / Apply / Reevaluate | 暂停 / 继续 / 应用 / 重新评估 |
| PC_PEA_Error_InsufficientFunds | 余额不足，需要 {0}，可用 {1} |
| PC_PEA_Error_StaleListing | 商品信息已变化，请刷新后重试 |
| PC_PEA_Error_RecoveryRequired | 此订单需要恢复处理，关联金额暂时冻结 |
| PC_PEA_Empty_NoSupply | 暂无符合目标的可售商品 |
| PC_PEA_Reason_ProtectedJob | 当前重要任务优先，稍后再进行经济活动 |

错误码保持稳定英文标识，展示时翻译；不得用翻译文本作为保存键、商品 ID 或条件分支。测试中文、英文和人为加长 50% 的文案，数字金额、原因与按钮都必须能看清。

## 9. 前端完成标准

1. 所有新增通用控件可映射到 RUIF 实际方法，依赖和程序集身份可核对。
2. 页面五种就绪状态、缺地图、空列表、长文本和分页均有游戏内截图。
3. UI 点击只产生命令；双击、旧快照、选项切换不会重复写账。
4. 页面刷新与仿真分离；关闭页面不停止经济 AI，打开页面不增加评估频率。
5. 本次提供的示例工程成功编译，只证明接口用法；完整页面和视觉结果要在正式实装后验收。
