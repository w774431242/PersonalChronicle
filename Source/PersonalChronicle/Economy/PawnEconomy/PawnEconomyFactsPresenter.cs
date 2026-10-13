using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Care;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Learning;
using PersonalChronicle.Domain.PawnEconomy.Planning;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// 「个人需求」页的**经济事实分区**转文本层（需求页「个人账户摘要」+ 决策页「劳动与合同」「照护与服务」）。
    ///
    /// <para>与 <see cref="ShadowPlanPresenter"/> 的分工：那边读**影子计划**（规划层的只读投影），
    /// 这边读**真实账本事实**（合同 / 应付 / 退避 / 可靠性 / 照护授权 / 服务能力）——
    /// 两者严格互补：规划层回答"该做什么"，本层回答"已经发生了什么"，同一事实不在两处渲染。</para>
    ///
    /// <para>本类只把**装配点取好的只读事实**转成结构化分区：不查询、不持有宿主、不写任何状态。
    /// 服务种类与能力标签复用既有的 <c>PC_PEA.Care.*</c> 命名空间（<see cref="INeedDashboardText.IdLabel"/>），
    /// **不新增第二套命名**（GOV-010 / 命名唯一性）。</para>
    /// </summary>
    internal sealed class PawnEconomyFactsPresenter
    {
        private readonly INeedDashboardText text;
        public PawnEconomyFactsPresenter(INeedDashboardText text) { this.text = text ?? throw new ArgumentNullException(nameof(text)); }
        private string T(string key, params object[] args) => text.Text("Planning." + key, args);
        private static string Money(long minor) => (minor / 100m).ToString("0.##", CultureInfo.CurrentCulture);

        /// <summary>
        /// 决策页「劳动与合同」分区（B4 合同 / M6 退出 / M9 应付与可靠性）。
        /// <paramref name="contract"/> = 当前履约中的合同（可为 null）；
        /// <paramref name="exit"/> = **只读**退出预判（<see cref="ContractExitCode.None"/> = 无退出条件）；
        /// <paramref name="payables"/> = 该 Pawn 的全部应付工资；
        /// <paramref name="purchaseBackoffFailures"/> = M9 购买退避的连续失败数（0 = 无失败记录）；
        /// <paramref name="credibility"/> = 合同机构的工资可信度读数（null = 无样本账，不出行）。
        /// 不可读的事实一律省略该行，**不写 0**（模块索引 §4 显示纪律）。
        /// </summary>
        public NeedEmploymentView BuildEmployment(WorkContract contract, ContractExitCode exit, int now,
            IReadOnlyList<WageObligation> payables, int purchaseBackoffFailures, WageCredibility credibility)
        {
            var rows = new List<NeedTextRow>(6);
            if (contract != null)
            {
                rows.Add(new NeedTextRow(T("Employment.Position"), contract.PositionKey));
                rows.Add(new NeedTextRow(T("Employment.Wage"),
                    T("Employment.WageValue", Money(contract.WagePerJobMinor), contract.CompletedJobs, contract.MaxJobs)));
                rows.Add(new NeedTextRow(T("Employment.Term"),
                    T("Employment.TermValue", Math.Max(0L, (long)contract.ExpiresTick - now), ExitText(exit))));
            }
            rows.Add(PayableRow(payables));
            rows.Add(new NeedTextRow(T("Employment.Backoff"), purchaseBackoffFailures > 0
                ? T("Employment.BackoffValue", purchaseBackoffFailures) : T("Employment.BackoffNone")));
            if (credibility != null)
            {
                rows.Add(new NeedTextRow(T("Employment.Credibility"),
                    T("Employment.CredibilityValue", T("CredibilityBand." + credibility.Band), credibility.Samples)));
            }
            return new NeedEmploymentView(contract != null ? T("Employment.State.Active") : T("Employment.State.None"),
                contract != null ? NeedCycleTone.Ok : NeedCycleTone.Unknown, rows);
        }

        /// <summary>
        /// 决策页「照护与服务」分区（M8 事实，以本 Pawn 为**受益人**的视角）。
        /// 无照护授权且能力不可读时返回 <c>null</c>（M8 是可选授权，不是每个 Pawn 都有 —— 空区只是噪声）。
        /// 已计费 / 上限 / 剩余期限逐条给出；服务能力复用既有 <c>PC_PEA.Care.Capability.*</c> 标签。
        /// </summary>
        public NeedCareView BuildCare(IReadOnlyList<CareAuthorization> authorizations, ServiceCapabilityState capability, int now)
        {
            bool hasAny = authorizations != null && authorizations.Count > 0;
            if (!hasAny && capability == null) return null;
            var rows = new List<NeedTextRow>();
            int count = 0;
            if (hasAny)
            {
                for (int i = 0; i < authorizations.Count; i++)
                {
                    var row = authorizations[i];
                    if (row == null) continue;
                    count++;
                    rows.Add(new NeedTextRow(text.IdLabel("PC_PEA.Care.Service." + row.Kind),
                        T("Care.RowValue", Money(row.ChargedMinor), Money(row.MaxAmountMinor),
                            Math.Max(0L, (long)row.ExpiresTick - now))));
                }
            }
            if (capability != null)
                rows.Add(new NeedTextRow(T("Care.Capability"), text.IdLabel("PC_PEA.Care.Capability." + capability.Capability)));
            bool ready = capability != null && capability.Capability == ServiceCapability.ReadyPaid;
            return new NeedCareView(count > 0 ? T("Care.Summary", count) : T("Care.None"),
                ready ? NeedCycleTone.Ok : count > 0 ? NeedCycleTone.Pending : NeedCycleTone.Unknown, rows);
        }

        /// <summary>
        /// **需求页「生活保障」卡内的个人账户摘要**（CB-ECON-002）。输入全部是**装配点已取好的只读事实**：
        /// 账户快照（可为 <c>null</c>）、账本可用性、决策页那份 <see cref="NeedBudgetView"/>
        /// （摘要的预算两项**直接取用它的文本** —— 同一数据契约，不另算一套预算）、
        /// 以及与之同源的 <see cref="ShadowPlan"/>（提供结构化补储判定、启动线、政策窗口与当前判定/原因）。
        ///
        /// <para>收入状态是**状态机**而不是文案拼接：计划缺失 / 计划过期 / 预算非就绪（等待报价、等待账户、
        /// 证据不一致）/ 暂不需要补储 / 无法工作 / 正在补储，各自一个稳定键。
        /// 判定**只读结果** —— 不重算预算、不改滞回、不改决策、不写钱。</para>
        ///
        /// <para>账户不可读时三个金额为 <c>null</c>（消费者显示 <see cref="NeedAccountView.StateText"/>，
        /// 绝不显示 0.00）；已知零照实显示 <c>0.00</c>。两者是不同事实，禁止合并。</para>
        /// </summary>
        public NeedAccountView BuildAccount(FinancialAccountSnapshot account, SettlementLedgerState ledger,
            NeedBudgetView budget, ShadowPlan plan, int now, string subject, bool inScope = true, NeedAccountModelView model = null)
        {
            // Availability always wins over a stale caller-supplied account snapshot.
            if (!inScope || ledger != SettlementLedgerState.Ready) account = null;
            NeedAccountState state = !inScope ? NeedAccountState.NotApplicable
                : account != null ? NeedAccountState.Known
                : ledger == SettlementLedgerState.Unavailable ? NeedAccountState.Unavailable
                : ledger == SettlementLedgerState.Unsupported ? NeedAccountState.Unsupported
                : ledger == SettlementLedgerState.Faulted ? NeedAccountState.Error
                : NeedAccountState.NotEstablished;
            NeedCycleTone tone = !inScope ? NeedCycleTone.Unknown
                : state == NeedAccountState.Known ? NeedCycleTone.Ok
                : state == NeedAccountState.Error ? NeedCycleTone.Blocked
                : state == NeedAccountState.NotEstablished ? NeedCycleTone.Unknown
                : NeedCycleTone.Pending;
            string stateText = state == NeedAccountState.Known ? null : T("Account.State." + state);
            long planAgeTicks = plan == null ? -1L : Math.Max(0L, (long)now - plan.BuiltTick);
            string incomeState = inScope ? IncomeState(plan, now, out NeedCycleTone incomeTone) : OutOfScopeIncome(out incomeTone);
            // 预算两项：与决策页消费**同一个** NeedBudgetView 实例的文本，口径不可能漂移。
            string reserveTarget = budget != null ? budget.ReserveTarget : T("Account.NoPlan");
            string incomeGap = budget != null ? budget.IncomeGap : T("Account.NoPlan");
            return new NeedAccountView(state, tone,
                account == null ? null : Money2(account.AvailableMinor),
                account == null ? null : Money2(account.BalanceMinor),
                account == null ? null : Money2(account.ReservedMinor),
                stateText, reserveTarget, incomeGap, incomeState, incomeTone, planAgeTicks,
                AccountTip(stateText, subject), BudgetTip(plan, now, reserveTarget), model);
        }

        /// <summary>
        /// 收入状态（结构化状态机，纯读）。顺序即优先级：**先看计划是否可用/新鲜，再看预算能否估计，
        /// 最后才看补储判定** —— 否则会把"其实还没算出来"说成"不需要补储"。
        /// 「无法工作」只在**已知**不允许就业（年龄或资格）时单独成态；其余阻碍留在悬停的判定/原因行。
        /// </summary>
        private string IncomeState(ShadowPlan plan, int now, out NeedCycleTone tone)
        {
            if (plan == null) { tone = NeedCycleTone.Unknown; return T("Account.Income.NoPlan"); }
            if (ShadowPlanPresenter.IsStale(plan, now)) { tone = NeedCycleTone.Pending; return T("Account.Income.Stale"); }
            var budget = plan.Budget;
            if (budget == null) { tone = NeedCycleTone.Unknown; return T("Account.Income.NoPlan"); }
            switch (budget.State)
            {
                case BudgetState.WaitingQuote: tone = NeedCycleTone.Pending; return T("Account.Income.WaitingQuote");
                case BudgetState.WaitingAccount: tone = NeedCycleTone.Pending; return T("Account.Income.WaitingAccount");
                case BudgetState.Inconsistent: tone = NeedCycleTone.Blocked; return T("Account.Income.Inconsistent");
                case BudgetState.Unknown: tone = NeedCycleTone.Unknown; return T("Account.Income.Unknown");
            }
            if (!budget.RefillActive.TryGetValue(out bool refill)) { tone = NeedCycleTone.Unknown; return T("Account.Income.Unknown"); }
            if (!refill) { tone = NeedCycleTone.Ok; return T("Account.Income.Sufficient"); }
            if (plan.EmploymentApplicable.TryGetValue(out bool permitted) && !permitted)
            { tone = NeedCycleTone.Blocked; return T("Account.Income.NotPermitted"); }
            tone = NeedCycleTone.Pending;
            return T("Account.Income.RefillNeeded");
        }

        /// <summary>范围外（无可用地图 / 会话不适用）：本页不取账户也不取预算，收入状态如实为"计划待刷新"。</summary>
        private string OutOfScopeIncome(out NeedCycleTone tone)
        {
            tone = NeedCycleTone.Unknown;
            return T("Account.Income.NoPlan");
        }

        /// <summary>
        /// 账户金额：**固定两位小数** + 既有货币单位键（需求单 §3.2 明文口径："固定两位小数及银币单位；
        /// 已知零显示 0.00"）。与决策页预算行的 <c>0.##</c> 口径**刻意不同**（那处沿用既有显示不改），
        /// 故此处不共用 <see cref="ShadowPlanPresenter"/> 的格式函数；文案键仍复用
        /// <c>PC_PEA.Needs.Planning.Money</c>，不另建第二套货币文案（GOV-010）。
        /// </summary>
        private string Money2(long minor) => T("Money", (minor / 100m).ToString("0.00", CultureInfo.CurrentCulture));

        /// <summary>账户区悬停说明（构建期拼好，绘制期零分配）：归属 / 口径 / 来源 / 采样时点 / 状态原因。</summary>
        private string AccountTip(string stateText, string subject)
        {
            var lines = new List<string>(5)
            {
                T("Account.Tip.Subject", string.IsNullOrEmpty(subject) ? T("Account.Tip.SubjectUnknown") : subject),
                T("Account.Tip.Formula"),
                T("Account.Tip.Source"),
                T("Account.Tip.Sampling"),
            };
            if (stateText != null) lines.Add(T("Account.Tip.State", stateText));
            return string.Join("\n", lines);
        }

        /// <summary>
        /// 储备与收入区悬停说明：计划采样时点 / 预算窗口 / 储备目标 / 启动线（唯一来源 = 预算出口的
        /// <c>RefillTriggerMinor</c>，**不在此重算公式**）/ 退出线 / 当前判定与原因 / 未到账不计入。
        /// </summary>
        private string BudgetTip(ShadowPlan plan, int now, string reserveTarget)
        {
            var lines = new List<string>(6);
            if (plan == null)
            {
                // 无计划 ≠ 无钱：账户数字照常呈现，只是预算与补储状态暂无估计。
                lines.Add(T("Account.Tip.NoPlan"));
                return string.Join("\n", lines);
            }
            lines.Add(T("Account.Tip.PlanAge", T("AgeTicks", Math.Max(0L, (long)now - plan.BuiltTick))));
            if (plan.Budget != null) lines.Add(T("Model.PlanSampleValue", plan.Budget.AvailableMinor.TryGetValue(out long sampledCash)
                ? Money2(sampledCash) : T("Unknown"), plan.BuiltTick, Math.Max(0L, (long)now - plan.BuiltTick),
                ShadowPlanPresenter.IsStale(plan, now) ? T("Stale") : T("Model.Fresh")));
            if (plan.Policy != null) lines.Add(T("Account.Tip.Window", plan.Policy.ReserveDays));
            var budget = plan.Budget;
            if (budget != null && budget.ReserveTargetMinor.TryGetValue(out long _))
                lines.Add(T("Account.Tip.Objective", reserveTarget));
            if (budget != null && budget.RefillTriggerMinor.TryGetValue(out long trigger)
                && budget.LowWaterMinor.TryGetValue(out long low) && budget.EmergencyReserveMinor.TryGetValue(out long emergency))
                lines.Add(T("Account.Tip.Trigger", Money(trigger), Money(low), Money(emergency)));
            if (budget != null && budget.ReserveTargetMinor.TryGetValue(out long target) && target > 0)
                lines.Add(T("Account.Tip.Exit", reserveTarget));
            lines.Add(T("Account.Tip.Decision", T("Decision." + plan.Decision), T("Reason." + plan.Reason)));
            return string.Join("\n", lines);
        }

        /// <summary>退出预判文案（只读；<see cref="ContractExitCode.None"/> = 无退出条件）。</summary>
        private string ExitText(ContractExitCode exit)
            => exit == ContractExitCode.None ? T("Employment.NoExit") : T("Employment.Exit." + exit);

        /// <summary>应付工资：未付金额 = Σ(应付 − 已付)，只计未结清（Paid）之外的义务。</summary>
        private NeedTextRow PayableRow(IReadOnlyList<WageObligation> payables)
        {
            long unpaid = 0;
            int count = 0;
            if (payables != null)
            {
                for (int i = 0; i < payables.Count; i++)
                {
                    var row = payables[i];
                    if (row == null || row.State == ObligationState.Paid) continue;
                    unpaid += row.AmountMinor - row.PaidMinor;
                    count++;
                }
            }
            return new NeedTextRow(T("Employment.Payable"),
                count == 0 ? T("Employment.NoPayable") : T("Employment.PayableValue", Money(unpaid), count));
        }
    }
}
