using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Care;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Learning;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Planning;
using PersonalChronicle.Economy.PawnEconomy;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 「个人需求」页**经济事实分区**的离线契约测试（CHG-20261009-ECON-NEEDS-UPGRADE）：
    /// 劳动与合同 / 照护与服务 / 循环④真实三态 / 目标页等待加权。
    ///
    /// 全部走**真实键文件**：<see cref="Words"/> 对缺失键直接抛（KeyNotFoundException），
    /// 因此"界面文案少了一条键"必然让本 fixture 变红，而不是静默显示键名。
    /// </summary>
    [TestFixture]
    public sealed class PawnEconomyFactsPresenterTests
    {
        private sealed class Words : INeedDashboardText
        {
            private readonly Dictionary<string, string> values = new Dictionary<string, string>();
            public Words(string language)
            {
                string root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../.."));
                foreach (string file in new[] { "PawnEconomyNeeds.xml", "PawnEconomyPlanning.xml", "PawnEconomy.xml" })
                {
                    var xml = new XmlDocument(); xml.Load(Path.Combine(root, "Languages", language, "Keyed", file));
                    foreach (XmlNode child in xml.DocumentElement.ChildNodes)
                        if (child.NodeType == XmlNodeType.Element && !values.ContainsKey(child.Name)) values.Add(child.Name, child.InnerText);
                }
            }
            public string Text(string suffix, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, values["PC_PEA.Needs." + suffix], arguments);
            public string NeedLabel(string id) => "need:" + id;
            /// <summary>完整键名直译：缺键即抛（有牙齿）。</summary>
            public string IdLabel(string key) => values[key];
        }

        private static PawnEconomyFactsPresenter Presenter(string language = "English") => new PawnEconomyFactsPresenter(new Words(language));

        private static WorkContract Contract(int expiresTick = 1100, int completedJobs = 1, long wagePerJobMinor = 250)
            => new WorkContract("c1", "p", "inst", "anchor/12/34", new[] { "Hauling" }, wagePerJobMinor, 3, 100, expiresTick,
                ContractState.Active, completedJobs);

        private static WageObligation Obligation(string id, long amount, long paid, ObligationState state)
            => new WageObligation("wage:" + id, "c1", id, "p", amount, 200, paid, state);

        private static WageCredibility WeakCredibility()
            => WageCredibility.From(new ExperienceSummary("inst", 8, 2, 0, 100));

        // ---- 劳动与合同 ----

        [Test] public void EmploymentRendersContractExitPayablesBackoffAndCredibility()
        {
            var view = Presenter().BuildEmployment(Contract(), ContractExitCode.InstitutionClosed, 110,
                new[] { Obligation("e1", 300, 100, ObligationState.Due), Obligation("e2", 500, 500, ObligationState.Paid) },
                2, WeakCredibility());
            Assert.That(view.State, Is.EqualTo("On contract"), "contract state must be localized, not a raw enum");
            Assert.That(view.Tone, Is.EqualTo(NeedCycleTone.Ok));
            var rows = view.Rows.ToDictionary(row => row.Label, row => row.Value);
            Assert.That(rows.Keys, Does.Contain("Position"), "position row must be present");
            Assert.That(rows["Wage and jobs"], Does.Contain("2.5").And.Contain("1 / 3"), "wage row must carry per-job pay and progress");
            Assert.That(rows["Contract term"], Does.Contain("990").And.Contain("institution closed"), "term row must carry remaining ticks and the exit prediction");
            // 应付只统计**未结清**部分：300−100=200（已付的那笔不得计入）。
            Assert.That(rows["Payable wages"], Does.Contain("2").And.Contain("1 unpaid"), "payables must sum unpaid remainder only");
            Assert.That(rows["Purchase backoff"], Does.Contain("2"), "backoff row must carry the consecutive failure count");
            Assert.That(rows["Wage credibility"], Does.Contain("weak").And.Contain("8"), "credibility row must carry band and samples");
        }

        [Test] public void EmploymentWithoutContractStillReportsPayablesAndBackoffAndNoExitClaim()
        {
            var view = Presenter().BuildEmployment(null, ContractExitCode.None, 110,
                new[] { Obligation("e1", 300, 300, ObligationState.Paid) }, 0, null);
            Assert.That(view.State, Is.EqualTo("No contract"));
            Assert.That(view.Tone, Is.EqualTo(NeedCycleTone.Unknown));
            var rows = view.Rows.ToDictionary(row => row.Label, row => row.Value);
            Assert.That(rows.Keys, Does.Not.Contain("Position"), "no contract ⇒ no position/term rows");
            Assert.That(rows["Payable wages"], Is.EqualTo("none"), "settled payables must report none, never a zero amount");
            Assert.That(rows["Purchase backoff"], Is.EqualTo("no failures"));
            Assert.That(rows.Keys, Does.Not.Contain("Wage credibility"), "no contract ⇒ no institution to read credibility for");
        }

        // ---- 照护与服务 ----

        [Test] public void CareIsHiddenWithoutAuthorizationOrCapability()
        {
            Assert.That(Presenter().BuildCare(null, null, 110), Is.Null, "no authorisation and no capability ⇒ the whole partition is hidden");
            Assert.That(Presenter().BuildCare(Array.Empty<CareAuthorization>(), null, 110), Is.Null);
        }

        [Test] public void CareListsAuthorisationsAndCapabilityUsingTheCareNamespaceLabels()
        {
            var authorisation = new CareAuthorization("auth1", ServiceKind.Treatment, "p", null, "doc", "p", 10000, 100, 5000, 1, 2500);
            var capability = new ServiceCapabilityState(ServiceKind.Treatment, ServiceCapability.ReadyPaid, 3, null);
            var view = Presenter().BuildCare(new[] { authorisation }, capability, 110);
            Assert.That(view, Is.Not.Null);
            Assert.That(view.State, Does.Contain("1"), "summary must carry the authorisation count");
            Assert.That(view.Tone, Is.EqualTo(NeedCycleTone.Ok), "a paid-ready capability is the healthy state");
            var rows = view.Rows.ToDictionary(row => row.Label, row => row.Value);
            // 服务种类标签复用 PC_PEA.Care.Service.* —— 不得为同一概念另建一套命名。
            Assert.That(rows.Keys, Does.Contain("Treatment"), "service kind must reuse the care namespace label");
            Assert.That(rows["Treatment"], Does.Contain("25").And.Contain("100").And.Contain("4890"),
                "row must carry charged / cap / remaining ticks");
            // 能力标签必须来自 PC_PEA.Care.Capability.* 的**译文**（IdLabel 缺键即抛，见 Words），不得回落成键名。
            Assert.That(rows["Treatment service"], Is.Not.EqualTo("PC_PEA.Care.Capability.ReadyPaid").And.Not.Empty,
                "capability must reuse the care namespace label, never the raw key");
        }

        // ---- 循环④：消费与服务的真实三态 ----

        private static ShadowPlan PlanWith(params PlanOption[] options)
            => new ShadowPlan("p", 20, 1, 110, 0, ShadowDecision.NoIncomeNeeded, "ReserveSufficient",
                new LifePolicy(null), new LifeBudget(BudgetState.Ready, "Ok"),
                employmentApplicable: Fact<bool>.Known(true), options: options);

        [Test] public void ConsumeCycleIsAvailableWhenPurchaseIsFeasible()
        {
            var cycles = new ShadowPlanPresenter(new Words("English")).BuildCycles(PlanWith(
                new PlanOption(PlanOptionKind.Purchase, true, string.Empty),
                new PlanOption(PlanOptionKind.Service, false, "ServiceNotImplemented")));
            Assert.That(cycles[3].Status, Is.EqualTo(NeedCycleStatus.Feasible));
            Assert.That(cycles[3].Tone, Is.EqualTo(NeedCycleTone.Ok));
            Assert.That(cycles[3].Detail, Does.Contain("✓"), "a feasible option must be marked, not just named");
        }

        [Test] public void ConsumeCycleIsBlockedWhenPricedButUnaffordable()
        {
            var cycles = new ShadowPlanPresenter(new Words("English")).BuildCycles(PlanWith(
                new PlanOption(PlanOptionKind.Purchase, false, "ConsumptionUnaffordable"),
                new PlanOption(PlanOptionKind.Service, false, "ServiceNotImplemented")));
            Assert.That(cycles[3].Status, Is.EqualTo(NeedCycleStatus.Blocked), "affordability is a real block, not 'not implemented'");
            Assert.That(cycles[3].Tone, Is.EqualTo(NeedCycleTone.Blocked));
        }

        [Test] public void ConsumeCycleReportsNotWiredWhenThereIsNoPublishedPrice()
        {
            var cycles = new ShadowPlanPresenter(new Words("English")).BuildCycles(PlanWith(
                new PlanOption(PlanOptionKind.Purchase, false, "NoPricedConsumption"),
                new PlanOption(PlanOptionKind.Service, false, "ServiceNotImplemented")));
            // 三态不可合并：无报价 ≠ 买不起 —— 玩家排查方向完全不同。
            Assert.That(cycles[3].Status, Is.EqualTo(NeedCycleStatus.WaitingFacts));
            Assert.That(cycles[3].Tone, Is.EqualTo(NeedCycleTone.Pending));
            Assert.That(cycles[3].Detail, Does.Contain("✗").And.Not.Contain("Reason."),
                "infeasible options must show a translated reason, never a raw key prefix");
        }

        [Test] public void ConsumeCycleSaysNotAssessedWhenThePlanEndedBeforeComparingOptions()
        {
            // 第四态（2026-10-09 实机反馈修正）：选项集为空 = 计划在方案比较前就结束了（早退）。
            // 把它并进"未接通"会让玩家去查报价/供给，而实际上什么都没评估。
            var cycles = new ShadowPlanPresenter(new Words("English")).BuildCycles(PlanWith());
            Assert.That(cycles[3].State, Is.EqualTo("Not assessed"));
            Assert.That(cycles[3].Tone, Is.EqualTo(NeedCycleTone.Unknown));
            Assert.That(cycles[3].Detail, Is.EqualTo("no option comparison in this assessment"));
        }

        // ---- 目标页：等待加权 ----

        [Test] public void GoalLineCarriesWaitingTicksAndCapForTheViewToRender()
        {
            var goal = new ShadowNeedGoal("Food", null, active: true, priority: 1, waitingTicks: 1234, activeSinceTick: 100);
            // 目标缺 Gap（本用例只测等待字段的透传）：显式给出 lastKnownActive，避免走 Gap 默认投影。
            var plan = new ShadowPlan("p", 20, 1, 110, 0, ShadowDecision.NativeNeed, "BasicNeedFirst",
                new LifePolicy(null), new LifeBudget(BudgetState.Ready, "Ok"), new[] { goal },
                lastKnownActive: new[] { "Food" });
            var rows = new ShadowPlanPresenter(new Words("English")).BuildGoals(plan);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].WaitingTicks, Is.EqualTo(1234), "waiting ticks must survive as structured data (not only in the deadline text)");
            Assert.That(rows[0].WaitCapTicks, Is.EqualTo(PlanningPolicy.MaximumWaitWeightTicks));
        }

        // ---- 个人账户摘要（CB-ECON-002）----

        /// <summary>用**真实规划器**造一份预算（需求页账户区读的是它出口的结构化字段，不是手搓值）。</summary>
        private static ShadowPlan AccountPlan(long targetMinor, long cashMinor, bool previousRefill, Fact<bool> employment = default)
            => new ShadowPlan("p", 20, 1, 110, 0, ShadowDecision.MaintainReserve, "ReserveSufficient",
                new LifePolicy(null),
                LifeBudgetPlanner.Build(Fact<FinancialAccountSnapshot>.Known(new FinancialAccountSnapshot("p", cashMinor, 0)),
                    new[] { new RequirementCostSlice("food", 10, 120010, true, true, Fact<long>.Known(targetMinor)) },
                    10, 120010, true, previousRefill),
                employmentApplicable: employment);

        private static ShadowPlan QuoteWaitingPlan()
            => new ShadowPlan("p", 20, 1, 110, 0, ShadowDecision.WaitingBudget, "CostEstimateUnavailable",
                new LifePolicy(null),
                LifeBudgetPlanner.Build(Fact<FinancialAccountSnapshot>.Known(new FinancialAccountSnapshot("p", 500, 0)),
                    new[] { new RequirementCostSlice("food", 10, 120010, true, true, Fact<long>.Known(4000)) },
                    10, 120010, false, false));

        private static NeedBudgetView BudgetOf(ShadowPlan plan) => new ShadowPlanPresenter(new Words("English")).BuildBudget(plan);

        // A01：余额 12345 / 预留 2345 minor ⇒ 总额 123.45、预留 23.45、可用 100.00（固定两位小数）。
        [Test] public void AccountSummaryShowsBalanceReservedAndAvailableWithTwoDecimals()
        {
            var view = Presenter().BuildAccount(new FinancialAccountSnapshot("Pawn_42", 12345, 2345),
                SettlementLedgerState.Ready, null, null, 110, "Nick");
            Assert.That(view.State, Is.EqualTo(NeedAccountState.Known));
            Assert.That(view.Available, Is.EqualTo("100.00 silver"), "available = balance − reserved, always two decimals");
            Assert.That(view.Balance, Is.EqualTo("123.45 silver"), "the total includes what is already reserved");
            Assert.That(view.Reserved, Is.EqualTo("23.45 silver"));
            Assert.That(view.StateText, Is.Null, "a readable account must not also carry a state message");
            // A03 归属：悬停给出**显示名**，绝不把内部账户 ID 打上屏。
            Assert.That(view.Tip, Does.Contain("Nick"), "the tip must name the account holder");
            Assert.That(view.Tip, Does.Not.Contain("Pawn_42"), "internal account ids must never reach the page");
            Assert.That(view.Tip, Does.Not.Contain("PC_PEA"), "tips must never leak raw keys");
        }

        // A02：已知零 / 未建立 / 暂不可用 / 不受支持 / 读取失败 —— 五种状态必须分开，未知不得伪装成 0.00。
        [Test] public void ZeroBalanceIsKnownWhileEveryUnreadableLedgerStateStaysSeparate()
        {
            var zero = Presenter().BuildAccount(new FinancialAccountSnapshot("p", 0, 0), SettlementLedgerState.Ready, null, null, 110, "Nick");
            Assert.That(zero.State, Is.EqualTo(NeedAccountState.Known));
            Assert.That(zero.Available, Is.EqualTo("0.00 silver"), "a known zero renders 0.00, not a state");
            Assert.That(zero.StateText, Is.Null);

            var missing = Presenter().BuildAccount(null, SettlementLedgerState.Ready, null, null, 110, "Nick");
            var unavailable = Presenter().BuildAccount(null, SettlementLedgerState.Unavailable, null, null, 110, "Nick");
            var unsupported = Presenter().BuildAccount(null, SettlementLedgerState.Unsupported, null, null, 110, "Nick");
            var faulted = Presenter().BuildAccount(null, SettlementLedgerState.Faulted, null, null, 110, "Nick");
            Assert.That(missing.State, Is.EqualTo(NeedAccountState.NotEstablished));
            Assert.That(unavailable.State, Is.EqualTo(NeedAccountState.Unavailable));
            Assert.That(unsupported.State, Is.EqualTo(NeedAccountState.Unsupported));
            Assert.That(faulted.State, Is.EqualTo(NeedAccountState.Error));
            foreach (var view in new[] { missing, unavailable, unsupported, faulted })
            {
                Assert.That(view.Available, Is.Null, "an unreadable account must not render 0.00");
                Assert.That(view.Balance, Is.Null);
                Assert.That(view.Reserved, Is.Null);
                Assert.That(view.StateText, Is.Not.Null.And.Not.Empty);
                Assert.That(view.StateText, Does.Not.StartWith("PC_PEA"), "state messages must be translated, never raw keys");
            }
            // 合并状态会让"账本受损"被谎报成"尚未建立账户" ⇒ 四段说明必须两两不同。
            var texts = new HashSet<string> { missing.StateText, unavailable.StateText, unsupported.StateText, faulted.StateText };
            Assert.That(texts.Count, Is.EqualTo(4), "each unreadable ledger state needs its own explanation");
        }

        [Test] public void OutOfScopePageReportsNotApplicableWithoutReadingTheLedger()
        {
            var view = Presenter().BuildAccount(null, SettlementLedgerState.Unavailable, null, null, 110, "Nick", inScope: false);
            Assert.That(view.State, Is.EqualTo(NeedAccountState.NotApplicable));
            Assert.That(view.Available, Is.Null);
            Assert.That(view.StateText, Is.EqualTo("Not applicable here (no live map)"));
        }

        // A04：账户已知 + 无计划 ⇒ 账户数字照常，预算两项与收入状态如实说"计划待刷新"，不得伪造成 0。
        [Test] public void AReadableAccountIsNotSwallowedByAMissingPlan()
        {
            var view = Presenter().BuildAccount(new FinancialAccountSnapshot("p", 1000, 0), SettlementLedgerState.Ready, null, null, 110, "Nick");
            Assert.That(view.Available, Is.EqualTo("10.00 silver"), "the account is independent of planning");
            Assert.That(view.ReserveTarget, Is.EqualTo("plan awaiting refresh"));
            Assert.That(view.IncomeGap, Is.EqualTo("plan awaiting refresh"));
            Assert.That(view.IncomeState, Is.EqualTo("plan awaiting refresh"));
            Assert.That(view.PlanAgeTicks, Is.EqualTo(-1), "no plan ⇒ age is -1, never 0 (0 would claim it was sampled now)");
            Assert.That(view.BudgetTip, Does.Contain("plan"), "the tip must say why the budget area is empty");
        }

        // A04：真实目标 0 与"缺价"不是同一状态；账户数字不受预算空态牵连。
        [Test] public void ARealZeroTargetIsNotTheSameAsAMissingQuote()
        {
            var zeroPlan = AccountPlan(0, 0, false);
            var zero = Presenter().BuildAccount(new FinancialAccountSnapshot("p", 0, 0), SettlementLedgerState.Ready, BudgetOf(zeroPlan), zeroPlan, 110, "Nick");
            Assert.That(zero.ReserveTarget, Is.EqualTo("0 silver"), "a real zero target is a number");
            Assert.That(zero.IncomeGap, Is.EqualTo("0 silver"));

            var quotePlan = QuoteWaitingPlan();
            var unpriced = Presenter().BuildAccount(new FinancialAccountSnapshot("p", 500, 0), SettlementLedgerState.Ready, BudgetOf(quotePlan), quotePlan, 110, "Nick");
            Assert.That(unpriced.Available, Is.EqualTo("5.00 silver"), "an unknown quote must not hide a known account");
            Assert.That(unpriced.ReserveTarget, Is.EqualTo("Unknown"), "a missing quote reports a state, not zero");
            Assert.That(unpriced.IncomeState, Is.EqualTo("waiting for living-cost quotes"));
        }

        // A05：收入状态跟随预算出口的结构化判定（同一份 NeedBudgetView + 同一份计划），不重算水位公式。
        [Test] public void IncomeStateFollowsTheBudgetTheDecisionPageShows()
        {
            var above = AccountPlan(4000, 3500, false);
            var view = Presenter().BuildAccount(new FinancialAccountSnapshot("p", 3500, 0), SettlementLedgerState.Ready,
                BudgetOf(above), above, 110, "Nick");
            Assert.That(view.IncomeState, Is.EqualTo("no top-up needed right now"), "C=35.00 with H=31.20 must not top up");
            Assert.That(view.IncomeTone, Is.EqualTo(NeedCycleTone.Ok));
            Assert.That(view.ReserveTarget, Is.EqualTo("40 silver"), "the summary reuses the decision page's own budget strings");
            Assert.That(view.IncomeGap, Is.EqualTo("5 silver"));
            // 启动线来自预算出口（31.20），且只在悬停解释里出现 —— 绘制层不重算。
            Assert.That(view.BudgetTip, Does.Contain("31.2"), "the start line must be explained from the exported trigger");

            var below = AccountPlan(4000, 3000, false);
            var refilling = Presenter().BuildAccount(new FinancialAccountSnapshot("p", 3000, 0), SettlementLedgerState.Ready,
                BudgetOf(below), below, 110, "Nick");
            Assert.That(refilling.IncomeState, Is.EqualTo("Income needed for reserves"), "C=30.00 < H=31.20 must start topping up without implying actual employment");
            Assert.That(refilling.IncomeTone, Is.EqualTo(NeedCycleTone.Pending));
        }

        [Test] public void IncomeStateNamesTheBlockerAndTheStalePlanInsteadOfClaimingNeed()
        {
            var blocked = AccountPlan(4000, 3000, false, Fact<bool>.Known(false));
            var view = Presenter().BuildAccount(new FinancialAccountSnapshot("p", 3000, 0), SettlementLedgerState.Ready,
                BudgetOf(blocked), blocked, 110, "Nick");
            Assert.That(view.IncomeState, Is.EqualTo("cannot work: age or eligibility forbids ordinary employment"));
            Assert.That(view.IncomeTone, Is.EqualTo(NeedCycleTone.Blocked));

            var stale = AccountPlan(4000, 3000, false);
            var late = Presenter().BuildAccount(new FinancialAccountSnapshot("p", 3000, 0), SettlementLedgerState.Ready,
                BudgetOf(stale), stale, 110 + PlanningPolicy.RefreshTicks + 301, "Nick");
            Assert.That(late.IncomeState, Is.EqualTo("plan is stale, awaiting re-evaluation"),
                "a stale plan must not be presented as a live reading");
            Assert.That(late.PlanAgeTicks, Is.EqualTo(PlanningPolicy.RefreshTicks + 301));

            // A06：预计金额不进钱包 —— 账户数字只来自账本快照，视图不因预算/工资改变任何金额。
            Assert.That(late.Available, Is.EqualTo("30.00 silver"), "forecast wages or budget never move the account figure");
        }
    }
}
