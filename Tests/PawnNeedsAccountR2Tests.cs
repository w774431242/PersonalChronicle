using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using System.Xml;
using System.Globalization;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Planning;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Economy.PawnEconomy;

namespace PersonalChronicle.Tests
{
    public sealed partial class PawnSilverReserveTests
    {
        [Test]
        public void AccountR2_UnrecoveredPreparedHostQueriesNeverCancelOrReleaseFunds()
        {
            var host = HostFunded();
            var ledger = Field<FundedSettlementLedger>(host, "ledger");
            var party = new Holder();
            var intent = HostIntent(host, party, Contents(Reserve(host))[0], 25, "display-pending", ReserveMovementKind.Withdrawal);
            Assert.That(ledger.Prepare(intent).Code, Is.EqualTo(SettlementCode.Prepared));
            SetField(host, "recovered", false);
            string before = SettlementLedgerCodec.Encode(ledger, null, null);
            long reserveVersion = Reserve(host).ReadAudit().Version;
            for (int i = 0; i < 12; i++)
            {
                Assert.That(host.ReadLedgerState(), Is.EqualTo(SettlementLedgerState.Unavailable));
                Assert.That(host.ReadAccountForDisplay("Treasury"), Is.Null);
            }
            Assert.That(Field<bool>(host, "recovered"), Is.False);
            Assert.That(SettlementLedgerCodec.Encode(ledger, null, null), Is.EqualTo(before));
            Assert.That(ledger.Read(intent.CommandId).Phase, Is.EqualTo(SettlementPhase.Prepared));
            Assert.That(ledger.ReadAccount("Treasury").ReservedMinor, Is.EqualTo(2500));
            Assert.That(Reserve(host).ReadAudit().Version, Is.EqualTo(reserveVersion));
            Assert.That(Field<string>(host, "fault"), Is.Null);

            // The lifecycle recovery still works; only an explicit recovery may cancel this movement.
            host.Recover();
            Assert.That(ledger.Read(intent.CommandId).Phase, Is.EqualTo(SettlementPhase.Cancelled));
            Assert.That(host.ReadAccountForDisplay("Treasury").ReservedMinor, Is.Zero);
        }

        [Test]
        public void AccountR2_RealScribeLoadDisplayWaitsForLifecycleRecovery()
        {
            var loaded = RoundTripHost(HostFunded(), node => EditLedger(node,
                ledger => ledger.Element("Command").SetAttributeValue("phase", "Prepared")));
            var ledger = Field<FundedSettlementLedger>(loaded, "ledger");
            string before = SettlementLedgerCodec.Encode(ledger, null, null);
            Assert.That(Field<bool>(loaded, "recovered"), Is.False);
            Assert.That(loaded.ReadLedgerState(), Is.EqualTo(SettlementLedgerState.Unavailable));
            Assert.That(loaded.ReadAccountForDisplay("Treasury"), Is.Null);
            Assert.That(SettlementLedgerCodec.Encode(ledger, null, null), Is.EqualTo(before));
            Assert.That(ledger.Read("deposit").Phase, Is.EqualTo(SettlementPhase.Prepared));
            loaded.Recover();
            Assert.That(loaded.ReadLedgerState(), Is.EqualTo(SettlementLedgerState.Ready));
            Assert.That(loaded.ReadAccountForDisplay("Treasury").BalanceMinor, Is.EqualTo(10000));
            Assert.That(ledger.Read("deposit").Phase, Is.EqualTo(SettlementPhase.Finalized));
        }

        [Test]
        public void AccountR2_UnexpectedReserveQuantityNeverDisplaysAnApparentlyHealthyAccount()
        {
            var host = HostFunded();
            Contents(Reserve(host))[0].stackCount--;
            var ledger = Field<FundedSettlementLedger>(host, "ledger");
            string before = SettlementLedgerCodec.Encode(ledger, null, null);
            Assert.That(host.ReadLedgerState(), Is.EqualTo(SettlementLedgerState.Faulted));
            Assert.That(host.ReadAccountForDisplay("Treasury"), Is.Null);
            Assert.That(SettlementLedgerCodec.Encode(ledger, null, null), Is.EqualTo(before));
            Assert.That(Field<string>(host, "fault"), Is.Null, "display health detection must not write host fault");
            Assert.That(host.ReadAudit().Fault, Is.EqualTo("ReserveQuantityMismatch"));
        }

        [Test]
        public void AccountR2_LedgerReserveMismatchUsesTheAuthorityAuditTruth()
        {
            var host = HostFunded();
            SetField(host, "ledger", new FundedSettlementLedger());
            Assert.That(host.ReadLedgerState(), Is.EqualTo(SettlementLedgerState.Faulted));
            Assert.That(host.ReadAccountForDisplay("Treasury"), Is.Null);
            Assert.That(host.ReadAudit().Fault, Is.EqualTo("ReserveLedgerMismatch"));
        }

        [Test]
        public void AccountR2_MissingSubjectReadDoesNotEnrollAnAccount()
        {
            var host = HostFunded();
            var ledger = Field<FundedSettlementLedger>(host, "ledger");
            string before = SettlementLedgerCodec.Encode(ledger, null, null);
            Assert.That(host.ReadLedgerState(), Is.EqualTo(SettlementLedgerState.Ready));
            Assert.That(host.ReadAccountForDisplay("missing-pawn"), Is.Null);
            Assert.That(SettlementLedgerCodec.Encode(ledger, null, null), Is.EqualTo(before));
        }

        [TestCase("preserveRaw", (int)SettlementLedgerState.Unsupported)]
        [TestCase("busy", (int)SettlementLedgerState.Unavailable)]
        public void AccountR2_UnavailableOrUnsupportedHostNeverLeaksAnOldAccount(string field, int state)
        {
            var host = HostFunded(); SetField(host, field, true);
            Assert.That(host.ReadLedgerState(), Is.EqualTo((SettlementLedgerState)state));
            Assert.That(host.ReadAccountForDisplay("Treasury"), Is.Null);
            Assert.That(Field<bool>(host, field), Is.True);
        }
    }

    [TestFixture]
    public sealed class PawnNeedsAccountR2AssemblyTests
    {
        // This is an assembly wiring assertion, not a substitute for a live Unity single-Pawn view.
        [Test]
        public void SinglePawnFacadeReadsMoodStatusHiddenCountAndUsesThePureAccountPort()
        {
            var method = typeof(GameComponent_PawnEconomy).GetMethod("QueryNeedDashboardSingle", BindingFlags.Instance | BindingFlags.NonPublic);
            var calls = Calls(method).ToArray();
            foreach (string getter in new[] { "get_Mood", "get_Status", "get_HiddenCount" })
                Assert.That(calls.Any(m => m.DeclaringType == typeof(NeedDashboardView) && m.Name == getter), Is.True, getter);
            Assert.That(calls.Any(m => m.DeclaringType == typeof(PawnSettlementSaveHost) && m.Name == "ReadAccountForDisplay"), Is.True);
            Assert.That(calls.Any(m => m.DeclaringType == typeof(PawnSettlementSaveHost) && m.Name == "ReadAccount"), Is.False);
        }

        private static IEnumerable<MethodBase> Calls(MethodInfo method)
        {
            var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)).ToDictionary(c => c.Value);
            byte[] bytes = method.GetMethodBody().GetILAsByteArray();
            for (int p = 0; p < bytes.Length;)
            {
                short value = bytes[p++];
                if (value == 0xfe) value = unchecked((short)(0xfe00 | bytes[p++]));
                var opcode = opcodes[value];
                if (opcode.OperandType == OperandType.InlineMethod)
                    yield return method.Module.ResolveMethod(BitConverter.ToInt32(bytes, p));
                switch (opcode.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: p += 1; break;
                    case OperandType.InlineVar: p += 2; break;
                    case OperandType.InlineI8: case OperandType.InlineR: p += 8; break;
                    case OperandType.InlineSwitch: p += 4 + BitConverter.ToInt32(bytes, p) * 4; break;
                    default: p += 4; break;
                }
            }
        }
    }

    [TestFixture]
    public sealed class PawnNeedsAccountR2ModelTests
    {
        private sealed class Words : INeedDashboardText
        {
            private readonly Dictionary<string, string> values = new Dictionary<string, string>();
            public Words(string language = "English")
            {
                string root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../.."));
                foreach (string file in new[] { "PawnEconomyNeeds.xml", "PawnEconomyPlanning.xml", "PawnEconomy.xml" })
                {
                    var xml = new XmlDocument(); xml.Load(Path.Combine(root, "Languages", language, "Keyed", file));
                    foreach (XmlNode child in xml.DocumentElement.ChildNodes)
                        if (child.NodeType == XmlNodeType.Element && !values.ContainsKey(child.Name)) values.Add(child.Name, child.InnerText);
                }
            }
            public string Text(string suffix, params object[] arguments) => string.Format(CultureInfo.InvariantCulture, values["PC_PEA.Needs." + suffix], arguments);
            public string NeedLabel(string id) => "need:" + id;
            public string IdLabel(string id) => values[id];
        }
        private static LifeBudget Budget(long cash = 3500, long target = 4000, bool refill = false)
            => LifeBudgetPlanner.Build(Fact<FinancialAccountSnapshot>.Known(new FinancialAccountSnapshot("p", cash, 0)),
                new[] { new RequirementCostSlice("food", 110, 120110, true, false, Fact<long>.Known(target)) },
                110, 120110, true, refill);
        private static ShadowPlan Plan(ShadowDecision decision = ShadowDecision.NoIncomeNeeded, LifeBudget budget = null,
            IEnumerable<ShadowNeedGoal> goals = null, IEnumerable<PlanOption> options = null,
            string opportunity = null, IEnumerable<WorkOpportunity> offers = null)
            => new ShadowPlan("p", 20, 1, 110, 1, decision, "ReserveSufficient", new LifePolicy(null), budget ?? Budget(),
                goals: goals, options: options, opportunity: opportunity, opportunities: offers,
                employmentApplicable: Fact<bool>.Known(true));
        private static ShadowPlanPresenter Presenter(string language = "English") => new ShadowPlanPresenter(new Words(language));

        [TestCase("English")] [TestCase("ChineseSimplified")]
        public void AllDecisionsProduceLocalizedReasonNextAndExactlyFourDistinctLoops(string language)
        {
            var words = new Words(language);
            foreach (ShadowDecision decision in Enum.GetValues(typeof(ShadowDecision)))
            {
                var model = new ShadowPlanPresenter(words).BuildModel(Plan(decision), 120, new FinancialAccountSnapshot("p", 3500, 0));
                Assert.That(model.DecisionLabel, Is.EqualTo(words.Text("Planning.Model.Decision." + decision)), decision.ToString());
                Assert.That(model.Reason, Is.Not.Empty);
                Assert.That(model.Next, Is.Not.Empty);
                Assert.That(model.Cycles.Select(c => c.Id), Is.EqualTo(new[] { NeedCycleId.Instant, NeedCycleId.Reserve, NeedCycleId.Labour, NeedCycleId.Consume }));
                Assert.That(model.Cycles.All(c => !c.State.Contains("PC_PEA") && !string.IsNullOrEmpty(c.Source)), Is.True);
            }
        }

        [Test]
        public void NoAssessedGoalsNeverClaimsImmediateNeedsAreSatisfied()
        {
            var cycles = Presenter().BuildCycles(Plan(), 120);
            Assert.That(cycles[0].Status, Is.EqualTo(NeedCycleStatus.NotAssessed));
            Assert.That(cycles[3].Status, Is.EqualTo(NeedCycleStatus.NotAssessed));
        }

        [TestCase(.9f, (int)NeedCycleStatus.Satisfied)]
        [TestCase(.6f, (int)NeedCycleStatus.Pending)]
        public void InactiveGoalRequiresActualEvidenceBeforeSayingSatisfied(float actual, int expected)
        {
            var reading = new NumericNeedFact("Food", "food", "test", Fact<float>.Known(actual), Fact<float>.Known(1f), true, "nutrition", 1, 100);
            var gap = CurrentNeedProjector.Project("p", reading, new NeedTarget(TargetState.Enabled, Fact<float>.Known(.8f), 1));
            var cycles = Presenter().BuildCycles(Plan(goals: new[] { new ShadowNeedGoal("Food", gap, false, 1) }), 120);
            Assert.That(cycles[0].Status, Is.EqualTo((NeedCycleStatus)expected));
        }

        [TestCase(659, false)] [TestCase(660, false)] [TestCase(661, true)] [TestCase(109, true)]
        public void CycleStalenessUsesTheSinglePlanAgeBoundary(int now, bool stale)
        {
            var cycles = Presenter().BuildCycles(Plan(), now);
            Assert.That(cycles.All(c => c.Stale), Is.EqualTo(stale));
            if (stale) Assert.That(cycles.All(c => c.Status == NeedCycleStatus.Stale), Is.True);
            Assert.That(cycles.All(c => c.SampledTick == 110), Is.True);
        }

        [TestCase(3500, false, (int)NeedCycleStatus.Feasible)]
        [TestCase(3000, false, (int)NeedCycleStatus.Pending)]
        [TestCase(3500, true, (int)NeedCycleStatus.Pending)]
        [TestCase(4000, true, (int)NeedCycleStatus.Satisfied)]
        public void ReserveLoopDisplaysTheExistingHysteresisRatherThanInventingAWorkTrigger(long cash, bool priorRefill, int expected)
        {
            var budget = Budget(cash: cash, refill: priorRefill);
            Assert.That(Presenter().BuildCycles(Plan(budget: budget), 120)[1].Status, Is.EqualTo((NeedCycleStatus)expected));
        }

        [Test]
        public void CoveragePreservesOverflowAndSeparatesCurrentAccountFromPlanSampling()
        {
            var model = Presenter().BuildModel(Plan(budget: Budget(cash: 3000)), 120, new FinancialAccountSnapshot("p", 7000, 1000));
            Assert.That(model.CoverageRatio, Is.EqualTo(.75f).Within(.001f), "ratio must use the same sampled plan cash as its income gap");
            Assert.That(model.CoverageText, Does.Contain("75"));
            Assert.That(model.PlanCash, Does.Contain("30"));
            Assert.That(model.AccountSampledTick, Is.EqualTo(120));
            Assert.That(model.PlanSampledTick, Is.EqualTo(110));
            Assert.That(model.ThresholdText, Does.Contain("31.2").And.Contain("40"));
            var overflow = Presenter().BuildModel(Plan(budget: Budget(cash: 6000)), 120, new FinancialAccountSnapshot("p", 7000, 1000));
            Assert.That(overflow.CoverageRatio, Is.EqualTo(1.5f).Within(.001f));
            Assert.That(overflow.CoverageText, Does.Contain("150"));
        }

        [Test]
        public void MissingQuoteTargetAndZeroTargetNeverBecomeZeroCoverage()
        {
            foreach (var budget in new[] { new LifeBudget(BudgetState.WaitingQuote, "CostEstimateUnavailable"), Budget(target: 0) })
            {
                var model = Presenter().BuildModel(Plan(budget: budget), 120, new FinancialAccountSnapshot("p", 3000, 0));
                Assert.That(model.CoverageRatio, Is.LessThan(0));
            }
        }

        [TestCase(false, false, (int)NeedCycleStatus.WaitingFacts)]
        [TestCase(false, true, (int)NeedCycleStatus.Blocked)]
        [TestCase(true, false, (int)NeedCycleStatus.Feasible)]
        public void ConsumptionSeparatesMissingQuoteAffordabilityAndFeasibility(bool feasible, bool unaffordable, int expected)
        {
            var plan = Plan(options: new[] { new PlanOption(PlanOptionKind.Purchase, feasible,
                feasible ? "" : unaffordable ? "ConsumptionUnaffordable" : "NoPricedConsumption") });
            Assert.That(Presenter().BuildCycles(plan, 120)[3].Status, Is.EqualTo((NeedCycleStatus)expected));
        }

        private static WorkContract Contract(bool completed = false, string pawn = "p")
            => new WorkContract("c1", pawn, "inst", "anchor/12/34", new[] { "Hauling" }, 250, 3, 100, 1100,
                completed ? ContractState.Completed : ContractState.Active, completed ? 3 : 0);

        [Test]
        public void SelectedOpportunityAndSignedContractNeverBecomeWorkingOrPaidEvidence()
        {
            var words = new Words();
            var offer = new WorkOpportunity("inst|Hauling|CarryTrip", Fact<bool>.Known(true), Fact<bool>.Known(true),
                Fact<bool>.Known(true), true, 1100, 150, 250, 3, true, true);
            var plan = Plan(ShadowDecision.SeekWork, opportunity: offer.Id, offers: new[] { offer });
            var selected = Presenter().BuildModel(plan, 120, new FinancialAccountSnapshot("p", 3000, 0));
            Assert.That(selected.ExecutionLabel, Is.EqualTo(words.Text("Planning.Model.Execution.Selected")));
            Assert.That(selected.Cycles[2].Status, Is.EqualTo(NeedCycleStatus.Feasible));
            var signed = Presenter().BuildModel(plan, 120, new FinancialAccountSnapshot("p", 3000, 0), Contract());
            Assert.That(signed.ExecutionLabel, Is.EqualTo(words.Text("Planning.Model.Execution.Contract")));
            Assert.That(signed.Cycles[2].Status, Is.EqualTo(NeedCycleStatus.Pending));
            Assert.That(signed.Cycles.Any(c => c.Status == NeedCycleStatus.Executing), Is.False,
                "no current Job association was provided; a contract is not proof of executing that Job");
        }

        [TestCase("other-contract", "p", 110, "tx", 250, (int)ObligationState.Paid)]
        [TestCase("c1", "other-pawn", 110, "tx", 250, (int)ObligationState.Paid)]
        [TestCase("c1", "p", 99, "tx", 250, (int)ObligationState.Paid)]
        [TestCase("c1", "p", 121, "tx", 250, (int)ObligationState.Paid)]
        [TestCase("c1", "p", 110, null, 250, (int)ObligationState.Paid)]
        [TestCase("c1", "p", 110, "", 250, (int)ObligationState.Paid)]
        [TestCase("c1", "p", 110, "tx", 100, (int)ObligationState.Paid)]
        [TestCase("c1", "p", 110, "tx", 250, (int)ObligationState.Due)]
        public void UnrelatedFutureOrUnprovenPaymentNeverReportsPaid(string contract, string pawn, int dueTick,
            string transaction, long paidMinor, int obligationState)
        {
            var payment = new WageObligation("wage:proof", contract, "proof", pawn, 250, dueTick, paidMinor,
                (ObligationState)obligationState, transaction);
            var model = Presenter().BuildModel(Plan(), 120, new FinancialAccountSnapshot("p", 3000, 0), Contract(true), new[] { payment });
            Assert.That(model.Cycles[2].ReasonCode, Is.Not.EqualTo("WageSettlementRecorded"));
        }

        [Test]
        public void CompletedContractSettledEvidenceReportsPaidButNeverNeedFulfillment()
        {
            var payment = new WageObligation("wage:proof", "c1", "proof", "p", 250, 110, 250, ObligationState.Paid, "tx:actual");
            var model = Presenter().BuildModel(Plan(), 120, new FinancialAccountSnapshot("p", 3000, 0), Contract(true), new[] { payment });
            Assert.That(model.Cycles[2].ReasonCode, Is.EqualTo("WageSettlementRecorded"));
            Assert.That(model.Cycles[2].Status, Is.EqualTo(NeedCycleStatus.Satisfied));
            Assert.That(model.Cycles[0].Status, Is.EqualTo(NeedCycleStatus.NotAssessed));
            Assert.That(model.Cycles[2].Source, Is.EqualTo("EmploymentLedger"));
        }

        [Test]
        public void OutstandingCurrentWageTakesPriorityOverAnOlderPaidReceipt()
        {
            var rows = new[] {
                new WageObligation("wage:paid", "c1", "proof-paid", "p", 250, 110, 250, ObligationState.Paid, "tx:actual"),
                new WageObligation("wage:due", "c1", "proof-due", "p", 250, 115, 100, ObligationState.Shortfall)
            };
            var model = Presenter().BuildModel(Plan(), 120, new FinancialAccountSnapshot("p", 3000, 0), Contract(true), rows);
            Assert.That(model.Cycles[2].ReasonCode, Is.EqualTo("PaymentShortfall"));
            Assert.That(model.Cycles[2].Status, Is.EqualTo(NeedCycleStatus.Blocked));
            Assert.That(model.ExecutionLabel, Does.Contain("1.5"));
        }

        [TestCase((int)BudgetState.Unknown, (int)NeedCycleStatus.NotAssessed)]
        [TestCase((int)BudgetState.WaitingQuote, (int)NeedCycleStatus.WaitingFacts)]
        [TestCase((int)BudgetState.WaitingAccount, (int)NeedCycleStatus.WaitingFacts)]
        [TestCase((int)BudgetState.Inconsistent, (int)NeedCycleStatus.Blocked)]
        public void ReserveBudgetFailuresNeverMasqueradeAsSufficient(int state, int expected)
        {
            var plan = Plan(ShadowDecision.WaitingBudget, new LifeBudget((BudgetState)state, "CostEstimateUnavailable"));
            Assert.That(Presenter().BuildCycles(plan, 120)[1].Status, Is.EqualTo((NeedCycleStatus)expected));
            Assert.That(Presenter().BuildModel(plan, 120, null).CoverageRatio, Is.LessThan(0));
        }

        [Test]
        public void MissingPlanAndOutOfScopeHaveFourExplicitLoopPlaceholders()
        {
            Assert.That(Presenter().BuildCycles(null, 120).Select(c => c.Status),
                Is.EqualTo(Enumerable.Repeat(NeedCycleStatus.NotAssessed, 4)));
            Assert.That(Presenter().BuildCycles(Plan(ShadowDecision.OutOfScope), 120).Select(c => c.Status),
                Is.EqualTo(Enumerable.Repeat(NeedCycleStatus.NotApplicable, 4)));
        }

        [TestCase((int)SettlementLedgerState.Unavailable, (int)NeedAccountState.Unavailable)]
        [TestCase((int)SettlementLedgerState.Unsupported, (int)NeedAccountState.Unsupported)]
        [TestCase((int)SettlementLedgerState.Faulted, (int)NeedAccountState.Error)]
        public void NonReadyLedgerRejectsAnAccidentallyProvidedStaleAccountSnapshot(int ledger, int expected)
        {
            var view = new PawnEconomyFactsPresenter(new Words()).BuildAccount(new FinancialAccountSnapshot("p", 5000, 0),
                (SettlementLedgerState)ledger, null, null, 120, "Pawn");
            Assert.That(view.State, Is.EqualTo((NeedAccountState)expected));
            Assert.That(view.Available, Is.Null);
            Assert.That(view.Balance, Is.Null);
            Assert.That(view.Reserved, Is.Null);
        }
    }
}
