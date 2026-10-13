using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Planning;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// 影子计划 → 只读页文本/结构。四类产出**严格互补、零重复**：
    /// <list type="bullet">
    /// <item><see cref="Build"/>：决策页的仲裁与预算行（不含生活政策）</item>
    /// <item><see cref="BuildLongTerm"/>：需求页签的「长期生活规划」区（生活政策与来源）</item>
    /// <item><see cref="BuildGoals"/> / <see cref="BuildBudget"/> / <see cref="BuildOpportunities"/> / <see cref="BuildCycles"/>：目标页与决策页卡片的**结构化**数据</item>
    /// </list>
    /// 策略阈值（<see cref="PlanningPolicy"/> 的进入阈值）**只在此处转成文本** —— 表现层不得直接读 Domain 常量，
    /// 否则同一口径会出现两套实现并缓慢漂移。
    /// </summary>
    internal sealed partial class ShadowPlanPresenter
    {
        private readonly INeedDashboardText text;
        public ShadowPlanPresenter(INeedDashboardText text) { this.text = text ?? throw new ArgumentNullException(nameof(text)); }
        private string T(string key, params object[] args) => text.Text("Planning." + key, args);
        private string Money(long minor) => T("Money", (minor / 100m).ToString("0.##", CultureInfo.CurrentCulture));
        private string Money(Fact<long> amount) => amount.TryGetValue(out long value) ? Money(value) : T("Unknown");
        private static string Number(float value) => value.ToString("0.##", CultureInfo.CurrentCulture);
        private string State(FactState state) => text.Text("Fact." + state);
        private string Percent(Fact<float> fact) => fact.TryGetValue(out float value)
            ? value.ToString("P0", CultureInfo.CurrentCulture) : State(fact.State);
        /// <summary>比例差 → 百分点文本（0.14 → "14"）；不可用时为事实状态文本（不得当作 0）。</summary>
        private string Points(Fact<float> fact) => fact.TryGetValue(out float value) ? Number(value * 100f) : State(fact.State);
        private string Flag(Fact<bool> fact) => fact.TryGetValue(out bool value) ? T(value ? "Yes" : "No") : State(fact.State);
        private string Alloc(long minor) => (minor / 100m).ToString("0.##", CultureInfo.CurrentCulture);

        /// <summary>
        /// 影子计划**陈旧判据**（唯一定义）：决策页仲裁行与需求页账户摘要共用同一判据，
        /// 避免两处各自实现后缓慢漂移。计划是**带采样时点**的读数，不是实时数据 ——
        /// <c>now</c> 回退（读档 / 时间回退）同样视为陈旧（不得把"未来"的计划当新鲜）。
        /// 无计划时返回 <c>false</c>：那不是陈旧，而是从未采样（调用方另有"计划待刷新"口径）。
        /// </summary>
        internal static bool IsStale(ShadowPlan plan, int now)
            => plan != null && (now < plan.BuiltTick || (long)now - plan.BuiltTick > PlanningPolicy.RefreshTicks + 300);

        /// <summary>
        /// 决策页的**仲裁与预算**行。生活政策与政策来源**不**并入本区（按 2026-10-09 裁决移入需求页签的
        /// 「长期生活规划」区），避免同一份数据两处渲染造成口径漂移。
        /// </summary>
        public IReadOnlyList<NeedTextRow> Build(ShadowPlan plan, int now)
        {
            var rows = new List<NeedTextRow> { new NeedTextRow(T("Mode"), T("ShadowOnly")) };
            if (plan == null) { rows.Add(new NeedTextRow(T("Decision"), T("Waiting"))); return rows.AsReadOnly(); }
            bool stale = IsStale(plan, now);
            rows.Add(new NeedTextRow(T("Age"), T("AgeTicks", Math.Max(0L, (long)now - plan.BuiltTick))));
            rows.Add(new NeedTextRow(T("Decision"), stale ? T("Stale") : T("Decision." + plan.Decision)));
            rows.Add(new NeedTextRow(T("Reason"), T("Reason." + plan.Reason)));
            // v0.4 §9：连续由基本保障占满 ⇒ 如实反馈"原生活计划不可持续"（设计要求给出可行动的原因）。
            rows.Add(new NeedTextRow(T("Sustainability"),
                plan.BasicNeedStreakTicks >= PlanningPolicy.UnsustainableStreakTicks ? T("Unsustainable") : T("Sustainable")));
            // v0.9 §2 M4：四类方案的可行性与**不可行原因** —— 玩家要能看到"为什么没选购买/服务"。
            // 与 BuildCycles 共用同一个 OptionText（口径唯一化，防两处实现漂移）。
            // 选项集为空 ⇒ 本计划在产出方案比较之前就结束了（如"等待事实"），此时**不出行**：
            // 空集不是"四个方案都不可行"，把它画出来会让玩家看到四个凭空的 ✗。
            if (plan.Options.Count > 0)
            {
                var parts = new List<string>(plan.Options.Count);
                for (int i = 0; i < plan.Options.Count; i++) parts.Add(OptionText(plan.Options[i], plan.Options[i].Kind));
                rows.Add(new NeedTextRow(T("Options"), string.Join(" · ", parts)));
            }
            // v0.4 §7：制度性受阻 —— 既缺资金又无法通过劳动补足的必要项。有才显示，空集合不出噪音。
            if (plan.InstitutionallyBlocked.Count > 0)
                rows.Add(new NeedTextRow(T("Blocked"), T("BlockedCount", plan.InstitutionallyBlocked.Count)));
            if (plan.SelectedOpportunity != null) rows.Add(new NeedTextRow(T("Opportunity"), plan.SelectedOpportunity));
            return rows.AsReadOnly();
        }

        /// <summary>
        /// **需求页「长期生活规划」卡**：生活政策**逐项**结构化（原先四项塞成一行文本，长值会被折行截断）。
        /// 工时 / 闲暇按 <see cref="HoursPerDay"/> 归一供画条；储蓄倾向本身即比例；保障期无自然上界故不给比例。
        /// 就业不适用时工时如实显示"不适用"，不显示数字。无影子计划时返回 <c>null</c>，调用方隐藏整区。
        /// </summary>
        public NeedPolicyView BuildPolicy(ShadowPlan plan)
        {
            var policy = plan?.Policy;
            if (policy == null) return null;
            bool employed = plan.EmploymentApplicable.TryGetValue(out bool employment) && employment;
            string workText = employed
                ? T("HoursPerDay", policy.PreferredWorkHours.ToString("0.#", CultureInfo.CurrentCulture))
                : plan.EmploymentApplicable.State == FactState.Known ? T("NotApplicable") : T("Unknown");
            var sources = new List<string>(); foreach (string source in policy.Sources) sources.Add(T("Source." + source));
            return new NeedPolicyView(workText, employed ? Normalized(policy.PreferredWorkHours / HoursPerDay) : -1f,
                T("HoursPerDay", policy.RetainedLeisureHours.ToString("0.#", CultureInfo.CurrentCulture)),
                Normalized(policy.RetainedLeisureHours / HoursPerDay),
                T("Days", policy.ReserveDays),
                T("Percent", policy.SavingBasisPoints / 100),
                Normalized(policy.SavingBasisPoints / (float)PlanningPolicy.BasisPoints),
                string.Join(" / ", sources));
        }

        /// <summary>归一化基准：一天 24 小时（客观时间常量，**不是**策略阈值）。</summary>
        private const float HoursPerDay = 24f;

        /// <summary>钳到 0..1（net48 无 <c>Math.Clamp</c>）；用于进度条刻度，不改动原始数值。</summary>
        private static float Normalized(float value) => Math.Min(1f, Math.Max(0f, value));

        /// <summary>
        /// **目标页**：每条需求的目标态 —— 当前满足度、**进入阈值 → 目标值**、优先级、活跃性、缺口三口径。
        /// 缺口三口径语义由 Domain 固定：相对（占目标的比例差）、绝对（比例差）、原始（原单位差，可能不受支持）。
        /// </summary>
        public IReadOnlyList<NeedGoalLine> BuildGoals(ShadowPlan plan)
        {
            if (plan?.Goals == null || plan.Goals.Count == 0) return Array.Empty<NeedGoalLine>();
            var rows = new List<NeedGoalLine>(plan.Goals.Count);
            foreach (var goal in plan.Goals)
            {
                var gap = goal.Gap;
                Fact<float> actual = gap?.ActualRatio ?? Fact<float>.Unknown("NoProjection");
                bool planned = gap != null && gap.TargetState == TargetState.Enabled;
                rows.Add(new NeedGoalLine(
                    text.NeedLabel(goal.NeedId), goal.NeedId, actual.TryGetValue(out float ratio) ? ratio : -1f, actual.State, Percent(actual),
                    planned ? T("Thresholds", EnterThreshold(goal.NeedId), Percent(gap.TargetRatio)) : T("NotApplicable"),
                    gap == null ? T("Unknown") : Points(gap.RelativeTargetGap),
                    gap == null ? T("Unknown") : Points(gap.AbsoluteRatioGap),
                    gap == null ? T("Unknown") : RawGap(gap),
                    goal.Priority, goal.Active, planned, DeadlineText(goal),
                    // v0.4 §9 饿死防护：等待时长此前**算出但零消费方**（Domain 排序在用，界面从未显示）。
                    // 上限随行给出：消费者可展示「已等待 N / 上限 M」，不得自造阈值（§7.10）。
                    goal.WaitingTicks, PlanningPolicy.MaximumWaitWeightTicks));
            }
            return rows.AsReadOnly();
        }

        /// <summary>
        /// **决策页预算卡**：结构化预算 + 三分配 + 可画进度条的派生比例（现金 / 下水位 相对储备目标）。
        /// 派生比例 <c>&lt; 0</c> 表示不可计算（证据未知或目标为 0）—— 消费方必须据此不画条，不得当作 0。
        /// </summary>
        public NeedBudgetView BuildBudget(ShadowPlan plan)
        {
            var budget = plan?.Budget;
            if (budget == null) return null;
            bool knownTarget = budget.ReserveTargetMinor.TryGetValue(out long target) && target > 0;
            bool knownCash = budget.AvailableMinor.TryGetValue(out long cash) && cash >= 0;
            float cash01 = knownTarget && knownCash ? Math.Min(1f, (float)cash / target) : -1f;
            float low01 = knownTarget && budget.LowWaterMinor.TryGetValue(out long low) && low >= 0
                ? Math.Min(1f, (float)low / target) : -1f;
            return new NeedBudgetView(T("Budget." + budget.State),
                Money(budget.AvailableMinor), Money(budget.ReserveTargetMinor),
                // v0.9 §2 M5：应急储备此前**算出但零消费方**（presenter 不读、无逻辑读取、无测试）。
                // 并入下水位一行而非另起一行 —— 它是下水位之下更紧的一档，独立成行会与"水位"语义混淆。
                T("LowWaterWithEmergency", Money(budget.LowWaterMinor), Money(budget.EmergencyReserveMinor)),
                Money(budget.IncomeGapMinor), budget.RefillActive.TryGetValue(out bool active) ? T(active ? "Yes" : "No") : T("Unknown"),
                Alloc(budget.ImmediateAllocationMinor), Alloc(budget.SavingAllocationMinor), Alloc(budget.OptionalAllocationMinor),
                cash01, low01);
        }

        /// <summary>
        /// **决策页候选岗位**：本次评估看到的候选各自一行（能力 / 可执行 / 资金 / 可达 + 工资与到账时点）。
        /// 候选**不等于**签约或收入 —— "未选中"本身也是玩家需要看到的解释。
        /// </summary>
        public IReadOnlyList<NeedTextRow> BuildOpportunities(ShadowPlan plan)
        {
            if (plan?.Opportunities == null || plan.Opportunities.Count == 0) return Array.Empty<NeedTextRow>();
            var rows = new List<NeedTextRow>(plan.Opportunities.Count);
            foreach (var offer in plan.Opportunities)
                rows.Add(new NeedTextRow(offer.Id, T("Offer", Money(offer.WageMinor), Flag(offer.Capable),
                    Flag(offer.Executable), Flag(offer.Funded), offer.Reachable ? T("Yes") : T("No"))));
            return rows.AsReadOnly();
        }

        /// <summary>
        /// **决策页四循环**：把 <see cref="ShadowDecision"/> 与预算/目标事实映射到设计文档的四个业务循环
        /// （即时满足 / 储蓄保障 / 劳动收入 / 消费与服务）。映射只是**呈现口径**，不新增 Domain 字段。
        /// 循环④读 <see cref="ShadowPlan.Options"/> 的**真实三态**：可行 / 买不起（受阻）/ 无报价或供给（未接通）——
        /// 此前硬编码「未实装」是 M7 落地后的语义滞后（模块索引 §7.11 要求改为真实状态）。
        /// </summary>
        /// <summary>取一类候选方案；该方案未报告时为 <c>null</c>（**不得**当作"不可行"处理）。</summary>
        private static PlanOption OptionOf(ShadowPlan plan, PlanOptionKind kind)
        {
            for (int i = 0; i < plan.Options.Count; i++)
                if (plan.Options[i].Kind == kind) return plan.Options[i];
            return null;
        }

        /// <summary>
        /// 单类方案的「名称 ✓」/「名称 ✗（原因）」文本。不可行按 M4 契约必带原因码；
        /// 码缺失时如实标注"未报告"，不显示空括号（GOV-009：只读结构化字段，不反解文本）。
        /// </summary>
        private string OptionText(PlanOption option, PlanOptionKind kind)
        {
            string name = T("Option." + kind);
            if (option == null) return name + " " + T("OptionUnreported");
            if (option.Feasible) return name + " ✓";
            string reason = string.IsNullOrEmpty(option.ReasonCode) ? T("OptionUnreported") : T("Reason." + option.ReasonCode);
            return name + " ✗（" + reason + "）";
        }

        private static IEnumerable<string> OfferIds(ShadowPlan plan)
        {
            foreach (var offer in plan.Opportunities) yield return offer.Id;
        }

        private string CycleName(int index)
        {
            switch (index)
            {
                case 0: return T("Cycle.Instant");
                case 1: return T("Cycle.Reserve");
                case 2: return T("Cycle.Labour");
                default: return T("Cycle.Consume");
            }
        }

        /// <summary>
        /// v0.4 §6 时限与余量的一段文本。**不可估时如实写明原因**（无基线 / 未在下降），
        /// 不显示假数字 —— 设计明令"无法可靠预测时限的目标用类别和区间处理"。
        /// </summary>
        private string DeadlineText(ShadowNeedGoal goal)
        {
            if (!goal.Active && goal.RiskDeadlineTicks < 0) return T("Deadline") + " " + T("DeadlineUnknown");
            string deadline = goal.RiskDeadlineTicks >= 0
                ? T("DeadlineTicks", goal.RiskDeadlineTicks) : T("DeadlineUnknown");
            string text = T("Deadline") + " " + deadline;
            if (goal.SlackTicks >= 0 || goal.SlackTicks < -1) text += "  ·  " + T("Slack") + " " + goal.SlackTicks;
            return text;
        }

        /// <summary>进入阈值（原型策略常量，非游戏机制阈值）：仅 Food/Rest/Joy 有政策目标。</summary>
        private static string EnterThreshold(string needId)
        {
            switch (needId)
            {
                case "Food": return PlanningPolicy.FoodEnter.ToString("P0", CultureInfo.CurrentCulture);
                case "Rest": return PlanningPolicy.RestEnter.ToString("P0", CultureInfo.CurrentCulture);
                case "Joy": return PlanningPolicy.JoyEnter.ToString("P0", CultureInfo.CurrentCulture);
                default: return PlanningPolicy.FoodEnter.ToString("P0", CultureInfo.CurrentCulture);
            }
        }

        /// <summary>原单位差（营养 / 休息点等）；语义不受支持或读取失败时如实给出状态。</summary>
        private string RawGap(CurrentNeedGap gap)
            => gap.RawLevelShortfall.TryGetValue(out float value) ? Number(value) : State(gap.RawLevelShortfall.State);
    }
}
