using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Application.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Economy.PawnEconomy
{
    internal interface INeedDashboardText
    {
        string Text(string suffix, params object[] arguments);
        string NeedLabel(string id);
        /// <summary>
        /// 取**完整键名**的标签（不套 <c>PC_PEA.Needs.</c> 前缀）。用于复用既有命名空间的稳定 ID 标签
        /// （如 <c>PC_PEA.Care.Service.Treatment</c>）—— **不得**为同一概念另建第二套命名。
        /// 键缺失时返回键名本身（与 <c>Translate()</c> 同语义，永不空白）。
        /// </summary>
        string IdLabel(string key);
    }
    internal sealed class NeedTextRow
    {
        public string Label { get; }
        public string Value { get; }
        public NeedTextRow(string label, string value) { Label = label; Value = value; }
    }
    internal sealed class NeedTableLine
    {
        public string Name { get; }
        public string Current { get; }
        public string Target { get; }
        public string Gap { get; }
        public string Source { get; }
        public string Age { get; }
        public string Status { get; }
        /// <summary>
        /// 当前满足度 0..1（供可视化条使用）；不可用为 −1。
        /// 结构化数值随行携带，消费者不得从 <see cref="Current"/> 文本反解（GOV-009）。
        /// </summary>
        public float Ratio01 { get; }
        /// <summary>
        /// 满足度事实的状态（Known / Unknown / NotApplicable / Unsupported / Error）。
        /// <see cref="Ratio01"/> &lt; 0 时供消费者**如实呈现原因**（"未适配"/"读取错误"/"不适用"），
        /// 不得一律显示为"未配置"（设计 §5 要求显式部分覆盖提示）。
        /// </summary>
        public FactState RatioState { get; }
        /// <summary>该需求是否已纳入生活规划（目标态为 Enabled）；用于把"未配置"合并为一行提示。</summary>
        public bool Planned { get; }
        /// <summary>原版需求页是否显示该项（true = 原版隐藏项，须与常规需求区分呈现）。</summary>
        public bool Hidden { get; }
        /// <summary>影子优先序：0 = 未被规划选中，1 = 基本，2 = 次要（0 与"安全级"由 <see cref="Active"/> 区分）。</summary>
        public int Priority { get; }
        /// <summary>影子目标当前是否**已激活**（越过进入阈值或尚未补足）。</summary>
        public bool Active { get; }
        public NeedTableLine(string name, string current, string target, string gap, string source, string age, string status,
            float ratio01 = -1f, bool planned = false, FactState ratioState = FactState.Unknown, bool hidden = false,
            int priority = 0, bool active = false)
        {
            Name = name; Current = current; Target = target; Gap = gap; Source = source; Age = age; Status = status;
            Ratio01 = ratio01; Planned = planned; RatioState = ratioState; Hidden = hidden;
            Priority = priority; Active = active;
        }
    }

    /// <summary>
    /// 需求页「长期生活规划」卡：生活政策**逐项**结构化。
    /// 可归一到 0..1 的参数另给比例供画条（工时 / 闲暇按 24 小时日归一；储蓄倾向本身即比例）；
    /// 保障期没有自然上界，故不给比例（消费方不画条）。比例 <c>&lt; 0</c> = 不可计算。
    /// </summary>
    internal sealed class NeedPolicyView
    {
        public string WorkHours { get; }
        public float WorkHours01 { get; }
        public string Leisure { get; }
        public float Leisure01 { get; }
        public string Reserve { get; }
        public string Saving { get; }
        public float Saving01 { get; }
        /// <summary>风格依据（性格特质映射来源）；始终非空。</summary>
        public string Sources { get; }
        public NeedPolicyView(string workHours, float workHours01, string leisure, float leisure01,
            string reserve, string saving, float saving01, string sources)
        {
            WorkHours = workHours; WorkHours01 = workHours01; Leisure = leisure; Leisure01 = leisure01;
            Reserve = reserve; Saving = saving; Saving01 = saving01; Sources = sources;
        }
    }

    /// <summary>需求循环的**呈现色调**（四循环卡用）。与 RUIF 的 <c>StatusKind</c> 解耦：契约层不引第三方枚举。</summary>
    internal enum NeedCycleTone { Unknown, Ok, Pending, Blocked, Unimplemented }
    internal enum NeedCycleId { Instant, Reserve, Labour, Consume }
    internal enum NeedCycleStatus { NotAssessed, Pending, WaitingFacts, Blocked, Feasible, Executing, Satisfied, NotApplicable, Stale }

    /// <summary>
    /// CB-ECON-002 **个人账户读取状态**（需求页账户摘要）。六态**必须分开呈现**，禁止塌缩：
    /// <list type="bullet">
    /// <item><see cref="Known"/>：账户已建立且读数为本次刷新时的实时值（**已知零 ≠ 未知**，零要显示 0.00）；</item>
    /// <item><see cref="NotEstablished"/>：账本健康，但该主体尚未建立个人账户；</item>
    /// <item><see cref="Unavailable"/>：账本正在存读档或恢复未完成（暂不可用，不得当作"没有账户"）；</item>
    /// <item><see cref="Unsupported"/>：账本以原始态保留（schema / 文本编码不受支持）；</item>
    /// <item><see cref="Error"/>：账本恢复或校验失败（读取失败）；</item>
    /// <item><see cref="NotApplicable"/>：本页当前上下文不适用（无可用地图 / 会话范围外），未读取账本。</item>
    /// </list>
    /// 全部为**只读**事实：任何一态都不得触发开户、迁移、补齐资金或解除预留。
    /// </summary>
    internal enum NeedAccountState { Known, NotEstablished, Unavailable, Unsupported, Error, NotApplicable }

    /// <summary>
    /// 目标页的一行：一条需求的完整目标态 —— 当前满足度、**进入阈值 → 目标值**、优先级、活跃性、缺口三口径。
    /// 全部为已格式化的只读文本/数值；缺口三口径不可用时给事实状态文本，消费方不得当作 0。
    /// </summary>
    internal sealed class NeedGoalLine
    {
        public string Name { get; }
        /// <summary>原版需求 DefName（如 Food）：用于与需求行按**结构化键**对齐，禁止按显示文本匹配（GOV-009）。</summary>
        public string NeedId { get; }
        /// <summary>当前满足度 0..1；不可用为 −1。</summary>
        public float Ratio01 { get; }
        public FactState RatioState { get; }
        public string RatioPercent { get; }
        /// <summary>「进入 X% → 目标 Y%」；未纳入规划时为「不适用」。</summary>
        public string Thresholds { get; }
        /// <summary>v0.4 §6 风险时限与余量（人类可读一段）；不可估时如实写明原因，不造伪精度。</summary>
        public string Deadline { get; }
        /// <summary>相对缺口（占目标的比例差，百分点文本）。</summary>
        public string RelativeGap { get; }
        /// <summary>绝对缺口（比例差，百分点文本）。</summary>
        public string AbsoluteGap { get; }
        /// <summary>原始缺口（原单位差，如营养点）。</summary>
        public string RawGap { get; }
        /// <summary>影子优先序：0 = 安全级，1 = 基本，2 = 次要；未纳入为 0。</summary>
        public int Priority { get; }
        public bool Active { get; }
        public bool Planned { get; }
        /// <summary>
        /// v0.4 §9 饿死防护：该目标**已连续等待**的 tick 数（0 = 本周期才开始等待；受阻/未知项恒 0）。
        /// 结构化数值随行携带，消费者不得从 <see cref="Deadline"/> 文本反解（GOV-009）。
        /// </summary>
        public int WaitingTicks { get; }
        /// <summary>等待加权上限（<c>PlanningPolicy.MaximumWaitWeightTicks</c> 经 presenter 转出的只读值）。</summary>
        public int WaitCapTicks { get; }
        public NeedGoalLine(string name, string needId, float ratio01, FactState ratioState, string ratioPercent, string thresholds,
            string relativeGap, string absoluteGap, string rawGap, int priority, bool active, bool planned, string deadline = null,
            int waitingTicks = 0, int waitCapTicks = 0)
        {
            Name = name; NeedId = needId; Ratio01 = ratio01; RatioState = ratioState; RatioPercent = ratioPercent;
            Thresholds = thresholds; Deadline = deadline; RelativeGap = relativeGap; AbsoluteGap = absoluteGap; RawGap = rawGap;
            Priority = priority; Active = active; Planned = planned;
            WaitingTicks = waitingTicks; WaitCapTicks = waitCapTicks;
        }
    }

    /// <summary>
    /// 决策页预算卡：结构化预算文本 + 三分配 + 两个**派生比例**（供画条）。
    /// 派生比例 <c>&lt; 0</c> = 不可计算（证据未知或目标为 0），消费方必须据此不画条而不是画 0。
    /// </summary>
    internal sealed class NeedBudgetView
    {
        /// <summary>预算状态（自解释，例如"缺已核验个人账户"）。</summary>
        public string State { get; }
        public string Cash { get; }
        public string ReserveTarget { get; }
        public string LowWater { get; }
        public string IncomeGap { get; }
        public string Refill { get; }
        public string Immediate { get; }
        public string Saving { get; }
        public string Optional { get; }
        /// <summary>现金 / 储备目标（0..1）；&lt; 0 = 不可计算。</summary>
        public float CashVsTarget01 { get; }
        /// <summary>下水位 / 储备目标（0..1，进度条刻度）；&lt; 0 = 不可计算。</summary>
        public float LowWaterVsTarget01 { get; }
        public NeedBudgetView(string state, string cash, string reserveTarget, string lowWater, string incomeGap,
            string refill, string immediate, string saving, string optional, float cashVsTarget01, float lowWaterVsTarget01)
        {
            State = state; Cash = cash; ReserveTarget = reserveTarget; LowWater = lowWater; IncomeGap = incomeGap;
            Refill = refill; Immediate = immediate; Saving = saving; Optional = optional;
            CashVsTarget01 = cashVsTarget01; LowWaterVsTarget01 = lowWaterVsTarget01;
        }
    }

    /// <summary>
    /// 决策页四循环卡的一栏：循环名 / 状态 / 明细 / 色调。
    /// 映射是**呈现口径**（见 `ShadowPlanPresenter.BuildCycles`），不代表新增了 Domain 字段。
    /// </summary>
    internal sealed class NeedCycleLine
    {
        public string Name { get; }
        public string State { get; }
        public string Detail { get; }
        public NeedCycleTone Tone { get; }
        public NeedCycleId Id { get; }
        public NeedCycleStatus Status { get; }
        public string ReasonCode { get; }
        public string Source { get; }
        public int SampledTick { get; }
        public bool Stale { get; }
        public bool Focused { get; }
        public NeedCycleLine(string name, string state, string detail, NeedCycleTone tone)
            : this(NeedCycleId.Instant, NeedCycleStatus.NotAssessed, name, state, detail, tone, "", "", -1, false, false) { }
        public NeedCycleLine(NeedCycleId id, NeedCycleStatus status, string name, string state, string detail,
            NeedCycleTone tone, string reasonCode, string source, int sampledTick, bool stale, bool focused)
        {
            Id = id; Status = status; Name = name; State = state; Detail = detail; Tone = tone;
            ReasonCode = reasonCode ?? string.Empty; Source = source ?? string.Empty;
            SampledTick = sampledTick; Stale = stale; Focused = focused;
        }
    }

    /// <summary>
    /// **决策页「劳动与合同」分区**（B4 合同 / M6 合理退出 / M9 可靠性事实）：合同状态摘要 +
    /// 岗位、工资与计件、到期与退出预判、应付工资、机构工资可信度与购买退避。
    /// 无合同时调用方仍可渲染（摘要为「未签合同」，明细为空集合）—— **不**返回 null，
    /// 因为"这个 Pawn 现在没有合同"本身是玩家要看到的事实。
    /// </summary>
    internal sealed class NeedEmploymentView
    {
        /// <summary>合同状态摘要（履约中 / 未签合同 / 已结束）。</summary>
        public string State { get; }
        public NeedCycleTone Tone { get; }
        /// <summary>明细行（岗位 / 工资 / 到期与退出 / 应付 / 学习与退避）。不填时为空集合，非 null。</summary>
        public IReadOnlyList<NeedTextRow> Rows { get; }
        public NeedEmploymentView(string state, NeedCycleTone tone, IEnumerable<NeedTextRow> rows)
        {
            State = state ?? string.Empty; Tone = tone; Rows = PerceptionCollections.Freeze(rows);
        }
    }

    /// <summary>
    /// **决策页「照护与服务」分区**（M8 事实，以本 Pawn 为<b>受益人</b>的只读视角）：
    /// 照护授权（付款方 / 已计费 / 上限 / 到期）与治疗服务能力状态。
    /// 无授权且能力不可读时为 <c>null</c>（调用方隐藏整区）—— 与劳动分区不同，
    /// "没有照护授权"对本页是噪声而非事实（M8 是可选授权，不是每个 Pawn 都有）。
    /// </summary>
    internal sealed class NeedCareView
    {
        public string State { get; }
        public NeedCycleTone Tone { get; }
        public IReadOnlyList<NeedTextRow> Rows { get; }
        public NeedCareView(string state, NeedCycleTone tone, IEnumerable<NeedTextRow> rows)
        {
            State = state ?? string.Empty; Tone = tone; Rows = PerceptionCollections.Freeze(rows);
        }
    }

    /// <summary>
    /// CB-ECON-002 **需求页「生活保障」卡内的个人账户摘要**（宿主无关只读读模型）。
    ///
    /// <para>六项必显字段全部**结构化**随行（GOV-009，禁止按显示文本反解）：可用余额 / 账户总额 / 预留金额 /
    /// 生活储备目标 / 补储缺口 / 收入状态。其中预算两项**直接取用决策页同一份**
    /// <see cref="NeedBudgetView"/> 的文本 —— 摘要与详情消费同一数据契约，不另算一套预算。</para>
    ///
    /// <para>未知与已知零**严格区分**：账户不可读时三个金额为 <c>null</c>（消费者须显示具体状态说明，
    /// 绝不写 0.00）；预算不可用时给事实状态文本而非 0。金额为整数 minor 换算（100 minor = 1 银币），
    /// 账户三项固定两位小数。</para>
    ///
    /// <para>时点分离：账户是**本次装配时的实时读数**（无独立时间戳），预算来自**带采样时点**的计划
    /// （<see cref="PlanAgeTicks"/>）——两者时点不一致时消费者必须分开陈述，不得声称缺口按最新余额计算。</para>
    /// </summary>
    internal sealed class NeedAccountView
    {
        /// <summary>账户读取状态（六态，见 <see cref="NeedAccountState"/>）。</summary>
        public NeedAccountState State { get; }
        /// <summary>状态色调（供状态行着色；金额行不据此变红，避免"余额少 = 生活差"的误读）。</summary>
        public NeedCycleTone Tone { get; }
        /// <summary>可用余额（最突出；固定两位小数 + 货币单位）。账户不可读时为 <c>null</c>。</summary>
        public string Available { get; }
        /// <summary>账户总额（含已被预留的余额，不称"可花余额"）。账户不可读时为 <c>null</c>。</summary>
        public string Balance { get; }
        /// <summary>预留金额（账本真实硬预留，不是预测开销或未来工资）。账户不可读时为 <c>null</c>。</summary>
        public string Reserved { get; }
        /// <summary>账户不可读时的**具体状态说明**（自解释，非裸枚举）；账户可读时为 <c>null</c>。</summary>
        public string StateText { get; }
        /// <summary>生活储备目标（同一份预算；无计划时为"计划待刷新"口径文本，非 0）。</summary>
        public string ReserveTarget { get; }
        /// <summary>补储缺口（同一份预算；未知时为事实状态文本，非 0）。</summary>
        public string IncomeGap { get; }
        /// <summary>收入状态：正在补储 / 暂不需要补储 / 无法工作 / 等待报价 / 等待账户 / 计划过期 / 计划待刷新。</summary>
        public string IncomeState { get; }
        public NeedCycleTone IncomeTone { get; }
        /// <summary>计划采样年龄（tick）；<c>&lt; 0</c> = 无计划（账户数字不受影响）。</summary>
        public long PlanAgeTicks { get; }
        /// <summary>账户区悬停说明（归属 / 口径 / 来源 / 采样 / 状态原因）。构建期拼好，绘制期零分配。</summary>
        public string Tip { get; }
        /// <summary>储备与收入区悬停说明（预算窗口 / 储备目标 / 启动线 / 退出线 / 当前阻断原因）。</summary>
        public string BudgetTip { get; }
        public NeedAccountModelView Model { get; }
        public NeedAccountView(NeedAccountState state, NeedCycleTone tone, string available, string balance, string reserved,
            string stateText, string reserveTarget, string incomeGap, string incomeState, NeedCycleTone incomeTone,
            long planAgeTicks, string tip, string budgetTip, NeedAccountModelView model = null)
        {
            State = state; Tone = tone; Available = available; Balance = balance; Reserved = reserved;
            StateText = stateText; ReserveTarget = reserveTarget; IncomeGap = incomeGap;
            IncomeState = incomeState ?? string.Empty; IncomeTone = incomeTone;
            PlanAgeTicks = planAgeTicks; Tip = tip ?? string.Empty; BudgetTip = budgetTip ?? string.Empty; Model = model;
        }
    }

    internal sealed class NeedRosterLine
    {
        public string Id { get; }
        public string Text { get; }
        public NeedRosterLine(string id, string text) { Id = id; Text = text; }
    }
    internal sealed class NeedDashboardView
    {
        public NeedDashboardSnapshot Snapshot { get; }
        public string Coverage { get; }
        public string Selection { get; }
        public string SelectionState { get; }
        public IReadOnlyList<NeedRosterLine> Roster { get; }
        public IReadOnlyList<NeedTableLine> Needs { get; }
        public IReadOnlyList<NeedTextRow> Facts { get; }
        /// <summary>
        /// 需求循环末端（影子仲裁 / 预算 / 政策 / 机会）的**结构化**行，供「个人需求」页单独成区。
        /// 名册路径不填（影子行沿用旧口径并入 <see cref="Facts"/>）；不填时为空集合，非 null。
        /// 结构化分区避免消费者按显示文本匹配（GOV-009）。
        /// </summary>
        public IReadOnlyList<NeedTextRow> Planning { get; }
        /// <summary>
        /// 心情（只读**结果量**，非消费目标；原设计明确不生成"购买心情"目标）。
        /// 供概览卡做总览读数；不可用时为 <c>default</c>。
        /// </summary>
        public Fact<float> Mood { get; }
        /// <summary>
        /// **状态条件卡**：没有需求条、只能按具名条件呈现的域（D03 衣着与温度、D09 健康与生育准备）。
        /// 设计红线要求这两类"不制造需求条"（`01-状态与需求感知` §5），故以文字条件行呈现。
        /// </summary>
        public IReadOnlyList<NeedTextRow> Status { get; }
        /// <summary>被采集上限截断、未纳入本次读取的需求条数（0 = 未截断）。</summary>
        public int HiddenCount { get; }
        /// <summary>
        /// **长期生活规划**（长期需求）：生活政策参数（劳动/闲暇/储备/储蓄倾向）与政策来源，
        /// 供「需求」页签的长期区渲染。与 <see cref="Planning"/> 严格互补——预算属当下钱的安排，留在决策页；
        /// 同一数据不得两处渲染（2026-10-09 裁决）。不填时为空集合，非 null。
        /// </summary>
        /// <summary>需求页「长期生活规划」卡（结构化政策参数）；无影子计划时为 <c>null</c>（消费方隐藏整区）。</summary>
        public NeedPolicyView Policy { get; }
        /// <summary>目标页：每条需求的目标态（优先级 / 活跃 / 双阈值 / 缺口三口径）。不填时为空集合。</summary>
        public IReadOnlyList<NeedGoalLine> Goals { get; }
        /// <summary>决策页预算卡；无影子计划时为 <c>null</c>（消费方据此隐藏整卡）。</summary>
        public NeedBudgetView Budget { get; }
        /// <summary>决策页候选岗位（每行一个 offer）；无候选时为空集合。</summary>
        public IReadOnlyList<NeedTextRow> Opportunities { get; }
        /// <summary>决策页四个业务循环（即时满足 / 储蓄保障 / 劳动收入 / 消费与服务）。不填时为空集合。</summary>
        public IReadOnlyList<NeedCycleLine> Cycles { get; }
        /// <summary>
        /// 决策页「劳动与合同」分区（B4/M6/M9 事实）；**单人路径填**，名册路径不填（<c>null</c>）。
        /// 与 <see cref="Cycles"/> 互补：循环③说明"是否需要收入"，本区说明"签了什么、何时退、拿没拿到钱"。
        /// </summary>
        public NeedEmploymentView Employment { get; }
        /// <summary>
        /// 决策页「照护与服务」分区（M8 事实）；无照护授权且能力不可读时为 <c>null</c>（隐藏整区）。
        /// </summary>
        public NeedCareView Care { get; }
        /// <summary>
        /// **需求页「生活保障」卡内的个人账户摘要**（CB-ECON-002，M1 生活保障）：只读账户读数 +
        /// **同一份**预算的摘要。仅在**单人路径**装配；名册路径不填（<c>null</c>）。
        /// 账户不可读时**仍然非 null** —— "该主体尚未建立账户 / 账本不可判定"本身就是要如实呈现的事实，
        /// 不得以需求空态或账户空态吞掉本区（需求与账户是两个不同量纲，账户不参与满足度均值）。
        /// </summary>
        public NeedAccountView Account { get; }
        public NeedDashboardView(NeedDashboardSnapshot snapshot, string coverage, string selection, string state,
            IEnumerable<NeedRosterLine> roster, IEnumerable<NeedTableLine> needs, IEnumerable<NeedTextRow> facts,
            IEnumerable<NeedTextRow> planning = null, Fact<float> mood = default, IEnumerable<NeedTextRow> status = null,
            int hiddenCount = 0, NeedPolicyView policy = null, IEnumerable<NeedGoalLine> goals = null,
            NeedBudgetView budget = null, IEnumerable<NeedTextRow> opportunities = null,
            IEnumerable<NeedCycleLine> cycles = null, NeedEmploymentView employment = null, NeedCareView care = null,
            NeedAccountView account = null)
        {
            Snapshot = snapshot; Coverage = coverage; Selection = selection; SelectionState = state;
            Roster = PerceptionCollections.Freeze(roster); Needs = PerceptionCollections.Freeze(needs); Facts = PerceptionCollections.Freeze(facts);
            Planning = PerceptionCollections.Freeze(planning); Mood = mood; Status = PerceptionCollections.Freeze(status);
            HiddenCount = hiddenCount; Policy = policy;
            Goals = PerceptionCollections.Freeze(goals); Budget = budget;
            Opportunities = PerceptionCollections.Freeze(opportunities); Cycles = PerceptionCollections.Freeze(cycles);
            Employment = employment; Care = care; Account = account;
        }
    }
    internal sealed class NeedDashboardPresenter
    {
        private readonly INeedDashboardText text;
        public NeedDashboardPresenter(INeedDashboardText text) { this.text = text ?? throw new ArgumentNullException(nameof(text)); }
        private string T(string key, params object[] args) => text.Text(key, args);
        private string State(FactState state) => T("Fact." + state);
        private string Ready(PerceptionReadiness state) => T("Readiness." + state);
        private string Capture(CaptureState state) => T("Capture." + state);
        private string Age(long age) => age < 0 ? State(FactState.Unknown) : T("AgeTicks", age);
        /// <summary>
        /// 数值呈现格式：最多 2 位小数。原 <c>0.####</c> 会把缺口渲染成 <c>14.9648</c> 这类
        /// 玩家不可读的噪声位（实机反馈 2026-10-09）。
        /// </summary>
        private static string Number(float value) => value.ToString("0.##", CultureInfo.CurrentCulture);
        private string Percent(Fact<float> fact) => fact.TryGetValue(out float value)
            ? value.ToString("P0", CultureInfo.CurrentCulture) : State(fact.State);
        private string Numeric(Fact<float> fact) => fact.TryGetValue(out float value) ? Number(value) : State(fact.State);
        private string Bool(Fact<bool> fact) => fact.TryGetValue(out bool value) ? T(value ? "Yes" : "No") : State(fact.State);
        private string Name(Fact<string> name, string id) => name.TryGetValue(out string value) ? value : id;
        private string Bed(Fact<BedFact> bed) => bed.TryGetValue(out var value) ? value.BedId : State(bed.State);
        private string Collection(int count, CaptureState state, int unread)
            => state == CaptureState.Complete || state == CaptureState.Partial ? T("Collection", count, Capture(state), unread) : Capture(state);
        /// <summary>
        /// 装配读模型。<paramref name="goals"/> 为影子规划的目标态（可空）：给需求行补上
        /// **优先序**与**活跃性**两项结构化标记；缺失时保持 0 / false（不猜、不默认"活跃"）。
        /// </summary>
        public NeedDashboardView Build(NeedDashboardSnapshot snapshot, IReadOnlyList<NeedGoalLine> goals = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var goalIndex = IndexGoals(goals);
            var roster = new List<NeedRosterLine>();
            foreach (var person in snapshot.Roster)
                roster.Add(new NeedRosterLine(person.PawnId, T("Person", Name(person.Name, person.PawnId), Ready(person.Facts.Readiness))));
            var diagnostic = snapshot.Diagnostics;
            string coverage = diagnostic == null ? Ready(snapshot.Selected?.Facts.Readiness ?? PerceptionReadiness.Loading)
                : T("Coverage", snapshot.MapSubjects, diagnostic.ActiveSubjects,
                    T(diagnostic.PopulationComplete ? "CensusComplete" : "CensusPartial"), diagnostic.OmittedSubjects,
                    Age(diagnostic.PopulationTick < 0 ? -1 : (long)snapshot.BuiltTick - diagnostic.PopulationTick));
            var observation = snapshot.Selected;
            var facts = observation?.Facts;
            var pawn = facts?.Snapshot;
            var context = pawn?.Context.Data;
            string selection = snapshot.SelectedId == null ? T("NoSelection") : Name(context?.DisplayName ?? default, snapshot.SelectedId);
            string readiness = facts == null ? diagnostic?.PopulationComplete == true ? T("NoSubjects") : Ready(PerceptionReadiness.Loading) : Ready(facts.Readiness);
            var rows = new List<NeedTableLine>(); var details = new List<NeedTextRow>();
            var status = new List<NeedTextRow>(); Fact<float> mood = default;
            if (facts != null)
            {
                foreach (var group in facts.Groups)
                    details.Add(new NeedTextRow(T("Group." + group.Group), T("GroupState", Ready(group.Readiness), Age(group.AgeTicks))));
            }
            var gaps = new Dictionary<string, CurrentNeedGap>(StringComparer.Ordinal);
            if (observation != null) foreach (var gap in observation.Gaps) gaps[gap.RequirementKey] = gap;
            if (pawn?.Needs.Data != null)
            {
                var needReadiness = facts.Readiness;
                foreach (var group in facts.Groups) if (group.Group == PerceptionGroups.Needs) needReadiness = group.Readiness;
                foreach (var need in pawn.Needs.Data.Needs)
                {
                    gaps.TryGetValue(RequirementIdentity.ForNeed(pawn.PawnId, need.NeedDefName, RequirementIdentity.LevelComponent), out var gap);
                    var actual = gap?.ActualRatio ?? Fact<float>.Unknown("NoProjection");
                    string target = gap == null || gap.TargetState == TargetState.Unknown ? T("Unconfigured") : Percent(gap.TargetRatio);
                    // 分区归属：只有 Enabled 才算"已纳入规划"；Disabled（显式关闭）与 NotApplicable（无此目标）不是规划项。
                    bool planned = gap != null && gap.TargetState == TargetState.Enabled;
                    // 文本口径**独立于**分区归属：Disabled/NotApplicable 仍有明确语义（N/A），不得降级为"未配置"。
                    bool hasTarget = gap != null && gap.TargetState != TargetState.Unknown;
                    goalIndex.TryGetValue(need.NeedDefName ?? "", out var goalLine);
                    rows.Add(new NeedTableLine(text.NeedLabel(need.NeedDefName),
                        T("CurrentRaw", Percent(actual), Numeric(need.CurrentLevel), Numeric(need.MaximumLevel)), target,
                        hasTarget
                            ? gap.AbsoluteRatioGap.TryGetValue(out float shortfall) ? Number(shortfall * 100f) : State(gap.AbsoluteRatioGap.State)
                            : T("Unconfigured"),
                        T(need.StandardRatioSupported ? "SourceVanilla" : "SourceUnadapted", need.NeedDefName),
                        Age((long)snapshot.BuiltTick - need.CapturedTick), T("GroupState", State(actual.State), Ready(needReadiness)),
                        actual.TryGetValue(out float ratio01) ? ratio01 : -1f, planned, actual.State, !need.ShowOnNeedList,
                        goalLine?.Priority ?? 0, goalLine?.Active ?? false));
                }
                mood = pawn.Needs.Data.MoodRatio;
                details.Add(new NeedTextRow(T("Mood"), Percent(pawn.Needs.Data.MoodRatio)));
                details.Add(new NeedTextRow(T("NeedCoverage"), T("NeedCounts", pawn.Needs.Data.ActualNeedIds.Count,
                    pawn.Needs.Data.UnsupportedNeedIds.Count, pawn.Needs.Data.FailedNeedIds.Count, pawn.Needs.TruncatedCount)));
            }
            if (context != null)
            {
                details.Add(new NeedTextRow(T("Location"), T("Location." + context.Location)));
                details.Add(new NeedTextRow(T("Downed"), Bool(context.Downed)));
                details.Add(new NeedTextRow(T("Drafted"), Bool(context.Drafted)));
                details.Add(new NeedTextRow(T("PlayerForced"), Bool(context.PlayerForced)));
            }
            var body = pawn?.Body.Data;
            if (body != null)
            {
                // D03 衣着与温度 / D09 健康与生育准备：无需求条，只能按具名条件呈现（设计红线：不制造需求条）。
                status.Add(new NeedTextRow(T("Health"), Collection(body.VisibleHediffs.Count, body.HealthState, body.UnexaminedHealthCount)));
                status.Add(new NeedTextRow(T("Apparel"), Collection(body.Apparel.Count, body.ApparelState, body.UnexaminedApparelCount)));
                status.Add(new NeedTextRow(T("Temperature"), Numeric(body.AmbientTemperature)));
                status.Add(new NeedTextRow(T("ComfortRange"), T("Range", Numeric(body.ComfortableMinimum), Numeric(body.ComfortableMaximum))));
                details.Add(new NeedTextRow(T("OwnedBed"), Bed(body.OwnedBed)));
                details.Add(new NeedTextRow(T("CurrentBed"), Bed(body.CurrentBed)));
                details.Add(new NeedTextRow(T("Pregnancy"), Bool(body.VisiblePregnancy)));
                details.Add(new NeedTextRow(T("Conditions"), Capture(body.ConditionsState)));
                foreach (var condition in body.Conditions)
                    details.Add(new NeedTextRow(T("Condition", text.NeedLabel(condition.NeedDefName)),
                        T("GroupState", Percent(condition.CurrentConditionRatio), Age((long)snapshot.BuiltTick - condition.CapturedTick))));
            }
            if (pawn?.Relationships.Data != null)
                details.Add(new NeedTextRow(T("Relations"), T("RelationCount", pawn.Relationships.Data.DirectRelations.Count)));
            return new NeedDashboardView(snapshot, coverage, selection, readiness, roster, rows, details, null, mood, status,
                pawn?.Needs.TruncatedCount ?? 0);
        }

        /// <summary>需求行的优先序/活跃性来自影子目标态；按 NeedId（结构化键，非显示文本）建索引，重复时保留首个。</summary>
        private static Dictionary<string, NeedGoalLine> IndexGoals(IReadOnlyList<NeedGoalLine> goals)
        {
            var index = new Dictionary<string, NeedGoalLine>(StringComparer.Ordinal);
            if (goals == null) return index;
            for (int i = 0; i < goals.Count; i++)
            {
                var goal = goals[i];
                if (goal?.NeedId != null && !index.ContainsKey(goal.NeedId)) index[goal.NeedId] = goal;
            }
            return index;
        }
    }
}
