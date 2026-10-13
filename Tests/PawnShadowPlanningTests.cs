using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Perception;
using PersonalChronicle.Application.PawnEconomy.Planning;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Planning;
using PersonalChronicle.Economy.PawnEconomy;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class PawnShadowPlanningTests
    {
        private static FactQueryResult Facts(int tick = 10, string id = "p", long epoch = 1, int map = 1,
            float food = .9f, float rest = .95f, float joy = .8f, bool drafted = false, bool forced = false, string stage = "Adult", string assignment = "Anything", bool frozen = false)
        {
            NumericNeedFact Need(string name, float value) => new NumericNeedFact(name, name, "test", Fact<float>.Known(value), Fact<float>.Known(1),
                true, "level", 1, tick, Fact<bool>.Known(frozen));
            var context = new PawnContextFact(SubjectLocation.SpawnedMap, map, developmentalStage: Fact<string>.Known(stage),
                drafted: Fact<bool>.Known(drafted), downed: Fact<bool>.Known(false), mentalState: Fact<bool>.Known(false),
                playerForced: Fact<bool>.Known(forced), timeAssignmentDefName: Fact<string>.Known(assignment));
            var snapshot = new PawnFactSnapshot(id, epoch, tick + 1, 1,
                new FactGroup<PawnContextFact>(CaptureState.Complete, context, tick, 1, "test"),
                new FactGroup<NeedFactSet>(CaptureState.Complete, new NeedFactSet(new[] { Need("Food", food), Need("Rest", rest), Need("Joy", joy) },
                    new[] { "Food", "Rest", "Joy" }, null, null, Fact<float>.Known(.8f)), tick, 1, "test"));
            return new FactQueryResult(PerceptionReadiness.Ready, "Ready", true, snapshot);
        }
        private static WorkOpportunity Offer(string id = "job", bool capable = true, bool executable = true, bool funded = true,
            int priority = 0, int expires = 1000, int pays = 500, bool reachable = true, long wage = 1200,
            bool incrementalIncome = false, bool playerPosted = false)
            => new WorkOpportunity(id, Fact<bool>.Known(capable), Fact<bool>.Known(executable), Fact<bool>.Known(funded), reachable, expires, pays, wage, priority,
                incrementalIncome: incrementalIncome, playerPosted: playerPosted);
        private static ShadowPlanningInput Input(FactQueryResult facts = null, int now = 10, long cash = 0, bool priced = true,
            IEnumerable<WorkOpportunity> offers = null, bool market = true, Fact<bool>? protection = null, int? due = null)
            => new ShadowPlanningInput(facts ?? Facts(tick: now), new LifePolicy(null), Fact<FinancialAccountSnapshot>.Known(new FinancialAccountSnapshot("p", cash)),
                new[] { new RequirementCostSlice("foodBudget", now, now + 120000, true, false, Fact<long>.Known(1200), requiredByTick: due) },
                priced, offers, market, protection ?? Fact<bool>.Known(false), Fact<bool>.Known(true));
        private static ShadowPlan Plan(ShadowPlanningInput input, ShadowPlan previous = null, int now = 10)
            => ShadowPlanBuilder.Build("p", 1, 1, now, input, previous);
        [Test] public void FourPlanOptionsLeaveTheBuilderOnTheNormalPathAndStayEmptyOnAnEarlyExit()
        {
            // 缺陷回归锁（2026-10-09 实机发现）：Result 局部函数曾漏传 options ⇒ ShadowPlan.Options 恒为空
            // ⇒ M4 的四方案比较从未到达任何消费方（影子仲裁的可行/不可行行与循环④的真实三态都读不到数据）。
            var normal = Plan(Input());
            Assert.That(normal.Options.Count, Is.EqualTo(4), "the four options must leave the builder on the normal path");
            Assert.That(normal.Options[2].Kind, Is.EqualTo(PlanOptionKind.Purchase));
            // 早退路径（事实不可用）：计划在产出方案比较之前结束 ⇒ 空集是**正确语义**，不是漏传。
            var early = Plan(Input(facts: new FactQueryResult(PerceptionReadiness.Loading, "Loading", false, null)));
            Assert.That(early.Decision, Is.EqualTo(ShadowDecision.WaitingFacts));
            Assert.That(early.Options, Is.Empty, "an early exit must not carry a fabricated option set");
        }
        [Test] public void TraitsRebuildFromNeutralBaselineWithoutAccumulatingAndUnknownTraitsDoNotCreateEconomicBeliefs()
        {
            var traits = new[] { new EffectiveTrait("Industriousness", 2), new EffectiveTrait("Ascetic", 0), new EffectiveTrait("Gourmand", 0),
                new EffectiveTrait("NightOwl", 0), new EffectiveTrait("Greedy", 0), new EffectiveTrait("Industriousness", 2) };
            var a = new LifePolicy(traits); var b = new LifePolicy(traits);
            Assert.That(a.PreferredWorkHours, Is.EqualTo(8.4f).Within(.0001f)); Assert.That(a.LifeStandard, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(a.FoodTarget, Is.EqualTo(.84f).Within(.0001f)); Assert.That(a.PrefersNight, Is.True);
            Assert.That(a.ReserveDays, Is.EqualTo(2)); Assert.That(a.ReservePreference, Is.EqualTo(.5f));
            Assert.That(a.PreferredWorkHours, Is.EqualTo(b.PreferredWorkHours)); Assert.That(a.Sources, Does.Not.Contain("Greedy"));
        }
        [Test] public void AllWorkPrioritiesZeroStillAllowsCapableFundedCandidateInShadowPlan()
        {
            var report = Plan(Input(offers: new[] { Offer(priority: 0) }));
            Assert.That(report.Decision, Is.EqualTo(ShadowDecision.SeekWork)); Assert.That(report.SelectedOpportunity, Is.EqualTo("job"));
            Assert.That(report.Budget.IncomeGapMinor.TryGetValue(out long gap) && gap == 1200, Is.True);
        }
        [TestCase(false, true, true, true)] [TestCase(true, false, true, true)] [TestCase(true, true, false, true)] [TestCase(true, true, true, false)]
        public void CapabilityExecutionFundingAndReachabilityAreHardPrerequisites(bool capable, bool executable, bool funded, bool reachable)
        {
            var report = Plan(Input(offers: new[] { Offer(capable: capable, executable: executable, funded: funded, reachable: reachable) }));
            Assert.That(report.Decision, Is.EqualTo(ShadowDecision.WaitingOpportunity)); Assert.That(report.SelectedOpportunity, Is.Null);
        }
        [Test] public void UnknownAbilityDoesNotCountAsCapableAndExpiredOffersAreExcluded()
        {
            var unknown = new WorkOpportunity("unknown", default, Fact<bool>.Known(true), Fact<bool>.Known(true), true, 1000, 500, 100, 0);
            Assert.That(Plan(Input(offers: new[] { unknown, Offer(expires: 10) })).Decision, Is.EqualTo(ShadowDecision.WaitingOpportunity));
        }
        [Test] public void EarlierPaymentThenStableIdBreakTiesRegardlessOfInputOrder()
        {
            var report = Plan(Input(offers: new[] { Offer("z", pays: 600), Offer("b", pays: 300), Offer("a", pays: 300) }));
            Assert.That(report.SelectedOpportunity, Is.EqualTo("a"));
        }
        [TestCase(false)] [TestCase(true)]
        public void PlayerPostedContractWinsOverItsSameTimeLegacyFallbackRegardlessOfInputOrder(bool reverse)
        {
            // Actual keys: ordinal sorting otherwise puts the shorter legacy fallback first.
            const string fallbackId = "vanilla:PlantCutting:A";
            const string postedId = "vanilla:PlantCutting:A:ChopWood";
            var fallback = Offer(fallbackId, wage: 200, pays: 300, incrementalIncome: true);
            var posted = Offer(postedId, wage: 200, pays: 300, incrementalIncome: true, playerPosted: true);
            var report = Plan(Input(offers: reverse ? new[] { posted, fallback } : new[] { fallback, posted }));
            Assert.That(report.Decision, Is.EqualTo(ShadowDecision.SeekWork));
            Assert.That(report.SelectedOpportunity, Is.EqualTo(postedId));
        }
        [Test] public void UnfundedPlayerPostedContractDoesNotDisplaceAFeasibleFallback()
        {
            var report = Plan(Input(offers: new[] {
                Offer("vanilla:PlantCutting:A:ChopWood", wage: 200, pays: 300, incrementalIncome: true, playerPosted: true, funded: false),
                Offer("vanilla:PlantCutting:A", wage: 200, pays: 300, incrementalIncome: true)
            }));
            Assert.That(report.Decision, Is.EqualTo(ShadowDecision.SeekWork));
            Assert.That(report.SelectedOpportunity, Is.EqualTo("vanilla:PlantCutting:A"));
        }
        [Test] public void PlayerPostedPreferenceDoesNotOverrideEarlierFeasiblePayment()
        {
            var report = Plan(Input(offers: new[] {
                Offer("vanilla:PlantCutting:A:ChopWood", wage: 200, pays: 301, incrementalIncome: true, playerPosted: true),
                Offer("vanilla:PlantCutting:A", wage: 200, pays: 300, incrementalIncome: true)
            }));
            Assert.That(report.SelectedOpportunity, Is.EqualTo("vanilla:PlantCutting:A"));
        }
        [Test] public void PlayerPostedContractDoesNotRecruitWithoutAnIncomeGapOrWithoutKnownPrices()
        {
            var posted = Offer("vanilla:PlantCutting:A:ChopWood", wage: 200, incrementalIncome: true, playerPosted: true);
            var fundedBudget = Plan(Input(cash: 1200, offers: new[] { posted }));
            Assert.That(fundedBudget.Decision, Is.EqualTo(ShadowDecision.NoIncomeNeeded));
            Assert.That(fundedBudget.SelectedOpportunity, Is.Null);
            var unpriced = Plan(Input(priced: false, offers: new[] { posted }));
            Assert.That(unpriced.Decision, Is.EqualTo(ShadowDecision.WaitingBudget));
            Assert.That(unpriced.SelectedOpportunity, Is.Null);
        }
        [TestCase(true, false)] [TestCase(false, true)]
        public void PlayerPostedContractStillYieldsToDraftingAndPlayerForcedJobs(bool drafted, bool forced)
        {
            var report = Plan(Input(facts: Facts(drafted: drafted, forced: forced), offers: new[] {
                Offer("vanilla:PlantCutting:A:ChopWood", wage: 200, incrementalIncome: true, playerPosted: true)
            }));
            Assert.That(report.Decision, Is.EqualTo(ShadowDecision.NativeYield));
            Assert.That(report.SelectedOpportunity, Is.Null);
        }
        [TestCase("Sleep")] [TestCase("Joy")] [TestCase("Meditate")] [TestCase("PC_Meal")]
        public void PlayerPostedContractStillRespectsProtectedTimetable(string assignment)
        {
            var report = Plan(Input(facts: Facts(assignment: assignment), offers: new[] {
                Offer("vanilla:PlantCutting:A:ChopWood", wage: 200, incrementalIncome: true, playerPosted: true)
            }));
            Assert.That(report.Reason, Is.EqualTo("ProtectedTimetable"));
            Assert.That(report.SelectedOpportunity, Is.Null);
        }
        [Test] public void IncomeMustArriveBeforeUncoveredNecessaryExpenseNotMerelyBeforeWindowEnd()
        {
            Assert.That(Plan(Input(offers: new[] { Offer(pays: 500) }, due: 400)).Decision, Is.EqualTo(ShadowDecision.WaitingOpportunity));
            Assert.That(Plan(Input(offers: new[] { Offer(pays: 400) }, due: 400)).Decision, Is.EqualTo(ShadowDecision.SeekWork));
        }
        [Test] public void MissingQuotesAreNotMisreportedAsPovertyOrAReasonToTakeAnUnfundedJob()
        {
            var report = Plan(Input(priced: false, offers: new[] { Offer() }));
            Assert.That(report.Decision, Is.EqualTo(ShadowDecision.WaitingBudget)); Assert.That(report.Budget.IncomeGapMinor.State, Is.EqualTo(FactState.Unknown));
            Assert.That(report.SelectedOpportunity, Is.Null);
        }
        [Test] public void SmallWageThatCannotProtectTheFirstUnfundedExpenseIsNotACompleteFinancingPlan()
        {
            Assert.That(Plan(Input(offers: new[] { Offer(wage: 200) })).Decision, Is.EqualTo(ShadowDecision.WaitingOpportunity));
            var report = Plan(Input(offers: new[] { Offer(wage: 1200) }));
            Assert.That(report.Budget.AvailableMinor.TryGetValue(out long cash) && cash == 0, Is.True);
            Assert.That(report.Budget.IncomeGapMinor.TryGetValue(out long gap) && gap == 1200, Is.True);
        }
        [Test] public void IncrementalPieceworkCanStartEarningWithoutPretendingOnePieceClosesTheBudgetGap()
        {
            var report = Plan(Input(offers: new[] { Offer(wage: 200, incrementalIncome: true) }));
            Assert.That(report.Decision, Is.EqualTo(ShadowDecision.SeekWork));
            Assert.That(report.SelectedOpportunity, Is.EqualTo("job"));
            Assert.That(report.Budget.AvailableMinor.TryGetValue(out long cash) && cash == 0, Is.True,
                "an offer is not money received");
            Assert.That(report.Budget.IncomeGapMinor.TryGetValue(out long gap) && gap == 1200, Is.True,
                "piecework selection must not book unearned future wages");
            Assert.That(Plan(Input(offers: new[] { Offer(wage: 200) })).Decision, Is.EqualTo(ShadowDecision.WaitingOpportunity),
                "ordinary financing offers retain the complete first-expense coverage requirement");
        }
        [TestCase(399, true)]
        [TestCase(400, true)]
        [TestCase(401, false)]
        public void IncrementalIncomeStillMustArriveByTheFirstUncoveredExpense(int pays, bool expectedSeekWork)
        {
            var report = Plan(Input(offers: new[] { Offer(wage: 200, pays: pays, incrementalIncome: true) }, due: 400));
            Assert.That(report.Decision, Is.EqualTo(expectedSeekWork ? ShadowDecision.SeekWork : ShadowDecision.WaitingOpportunity));
            if (expectedSeekWork) Assert.That(report.SelectedOpportunity, Is.EqualTo("job"));
            else Assert.That(report.SelectedOpportunity, Is.Null);
        }
        [TestCase(false, true, true, true)]
        [TestCase(true, false, true, true)]
        [TestCase(true, true, false, true)]
        [TestCase(true, true, true, false)]
        public void IncrementalIncomeRetainsAbilityExecutionFundingAndReachabilityGuards(bool capable, bool executable, bool funded, bool reachable)
        {
            var report = Plan(Input(offers: new[] { Offer(wage: 200, incrementalIncome: true,
                capable: capable, executable: executable, funded: funded, reachable: reachable) }));
            Assert.That(report.Decision, Is.EqualTo(ShadowDecision.WaitingOpportunity));
            Assert.That(report.SelectedOpportunity, Is.Null);
        }
        [TestCase(0, 1000, 500)]
        [TestCase(200, 10, 500)]
        [TestCase(200, 1000, 9)]
        public void IncrementalIncomeRejectsZeroWageExpiredAndAlreadyMissedPaymentOffers(long wage, int expires, int pays)
        {
            var report = Plan(Input(offers: new[] { Offer(wage: wage, expires: expires, pays: pays, incrementalIncome: true) }));
            Assert.That(report.Decision, Is.EqualTo(ShadowDecision.WaitingOpportunity));
            Assert.That(report.SelectedOpportunity, Is.Null);
        }
        [Test] public void UnknownMarketDiffersFromKnownNoFeasibleWork()
        {
            Assert.That(Plan(Input(market: false)).Reason, Is.EqualTo("OpportunityNotPublished"));
            Assert.That(Plan(Input()).Reason, Is.EqualTo("NoFeasibleOpportunity"));
        }
        [TestCase(.2f, .9f)] [TestCase(.9f, .2f)]
        public void UrgentNeedsYieldToNativeBeforeIncome(float food, float rest)
            => Assert.That(Plan(Input(facts: Facts(food: food, rest: rest), offers: new[] { Offer() })).Decision, Is.EqualTo(ShadowDecision.NativeYield));
        [Test] public void SimultaneousBasicNeedsAndJoyRetainAllGoalsAndBasicNeedsPrecedeSaving()
        {
            var report = Plan(Input(facts: Facts(food: .5f, rest: .4f, joy: .2f), offers: new[] { Offer() }));
            Assert.That(report.Decision, Is.EqualTo(ShadowDecision.NativeNeed));
            Assert.That(report.Goals.Where(g => g.Active).Select(g => g.Priority), Is.EqualTo(new[] { 1, 1, 2 }));
        }
        [Test] public void NeedActivationUsesHysteresisAndDoesNotDuplicateRequirementKeysOnReevaluation()
        {
            var a = Plan(Input(facts: Facts(food: .5f)));
            var b = Plan(Input(facts: Facts(tick: 260, food: .6f), now: 260), a, 260);
            var c = Plan(Input(facts: Facts(tick: 510, food: .9f), now: 510), b, 510);
            Assert.That(b.Goals.Single(g => g.NeedId == "Food").Active, Is.True);
            Assert.That(c.Goals.Single(g => g.NeedId == "Food").Active, Is.False);
            Assert.That(a.Goals.Single(g => g.NeedId == "Food").Gap.RequirementKey, Is.EqualTo(b.Goals.Single(g => g.NeedId == "Food").Gap.RequirementKey));
        }
        [Test] public void FrozenNeedsDoNotCreateLaborPressure()
        {
            var report = Plan(Input(facts: Facts(food: .1f, rest: .1f, joy: .1f, frozen: true), cash: 1200));
            Assert.That(report.Goals.All(g => !g.Active), Is.True); Assert.That(report.Decision, Is.EqualTo(ShadowDecision.NoIncomeNeeded));
        }
        [Test] public void TemporarilyStaleFactsKeepLastKnownActivationWithoutPublishingItAsCurrentAction()
        {
            var a = Plan(Input(facts: Facts(food: .5f)));
            var staleFacts = new FactQueryResult(PerceptionReadiness.Stale, "AgeExceeded", false, Facts(food: .6f).Snapshot);
            var b = Plan(Input(facts: staleFacts, now: 260), a, 260);
            Assert.That(b.Decision, Is.EqualTo(ShadowDecision.WaitingFacts)); Assert.That(b.Goals, Is.Empty);
            var c = Plan(Input(facts: Facts(tick: 510, food: .6f), now: 510), b, 510);
            Assert.That(c.Goals.Single(g => g.NeedId == "Food").Active, Is.True);
        }
        [TestCase(true, false)] [TestCase(false, true)]
        public void DraftingAndPlayerForcedJobsYieldEvenWhenIncomeIsNeeded(bool drafted, bool forced)
            => Assert.That(Plan(Input(facts: Facts(drafted: drafted, forced: forced), offers: new[] { Offer() })).Decision, Is.EqualTo(ShadowDecision.NativeYield));
        [TestCase("Sleep")] [TestCase("Joy")] [TestCase("Meditate")] [TestCase("PC_Meal")]
        public void ProtectedTimetablePreventsEconomicProposal(string assignment)
            => Assert.That(Plan(Input(facts: Facts(assignment: assignment), offers: new[] { Offer() })).Reason, Is.EqualTo("ProtectedTimetable"));
        [Test] public void WorkTimetableIsAllowedAndReserveTargetStopsFurtherRecruitment()
        {
            Assert.That(Plan(Input(facts: Facts(assignment: "Work"), offers: new[] { Offer() })).Decision, Is.EqualTo(ShadowDecision.SeekWork));
            Assert.That(Plan(Input(cash: 1200, offers: new[] { Offer() })).Decision, Is.EqualTo(ShadowDecision.NoIncomeNeeded));
        }
        [TestCase("Baby")] [TestCase("Child")]
        public void ChildrenNeverGetOrdinaryEmployment(string stage)
        {
            var report = Plan(Input(facts: Facts(stage: stage), offers: new[] { Offer() }));
            Assert.That(report.Reason, Is.EqualTo("EmploymentNotPermitted"));
            Assert.That(report.EmploymentApplicable.TryGetValue(out bool applicable) && !applicable, Is.True);
        }
        [Test] public void UnknownSafetyStaleFactsAndWrongSessionCannotProposeWork()
        {
            Assert.That(Plan(Input(protection: Fact<bool>.Unknown("missing"), offers: new[] { Offer() })).Decision, Is.EqualTo(ShadowDecision.NativeYield));
            var stale = new FactQueryResult(PerceptionReadiness.Stale, "AgeExceeded", false, Facts().Snapshot);
            Assert.That(Plan(Input(facts: stale)).Decision, Is.EqualTo(ShadowDecision.WaitingFacts));
            Assert.That(Plan(Input(facts: Facts(epoch: 2))).Decision, Is.EqualTo(ShadowDecision.WaitingFacts));
        }
        private sealed class Source : IShadowPlanningSource
        {
            public long Epoch { get; set; } = 1;
            public List<PerceptionSubject> People = new List<PerceptionSubject>();
            public IReadOnlyList<PerceptionSubject> Subjects => People;
            public int Reads;
            public bool Fail;
            public ShadowPlanningInput ReadInput(PerceptionSubject subject, int now)
            { Reads++; if (Fail) throw new InvalidOperationException("test"); return Input(Facts(now, subject.PawnId, Epoch, subject.MapId), now); }
        }
        [Test] public void CoordinatorUsesTickQuotaAndQueryNeverRunsSourceOrPlanning()
        {
            var source = new Source(); for (int i = 0; i < 20; i++) source.People.Add(new PerceptionSubject("p" + i.ToString("D2"), 1));
            var coordinator = new ShadowPlanningCoordinator(source); coordinator.Tick(10);
            Assert.That(source.Reads, Is.EqualTo(2)); Assert.That(coordinator.LastProcessed, Is.EqualTo(2));
            var report = coordinator.Query("p00", 1, 10); Assert.That(report, Is.Not.Null);
            for (int i = 0; i < 20; i++) Assert.That(coordinator.Query("p00", 1, 10), Is.SameAs(report));
            coordinator.Tick(10); Assert.That(source.Reads, Is.EqualTo(2));
            for (int tick = 11; tick < 20; tick++) coordinator.Tick(tick);
            Assert.That(source.Reads, Is.EqualTo(20)); coordinator.Tick(20); Assert.That(source.Reads, Is.EqualTo(20));
        }
        [Test] public void CoordinatorInvalidatesSessionMapClockAndFailuresWithoutShowingOldSuccess()
        {
            var source = new Source(); source.People.Add(new PerceptionSubject("p", 1));
            var coordinator = new ShadowPlanningCoordinator(source); coordinator.Tick(10);
            Assert.That(coordinator.Query("p", 2, 10), Is.Null);
            source.Epoch = 2; Assert.That(coordinator.Query("p", 1, 10), Is.Null); coordinator.Tick(11);
            Assert.That(coordinator.Query("p", 1, 11).Epoch, Is.EqualTo(2));
            coordinator.Tick(5); Assert.That(coordinator.Query("p", 1, 5).BuiltTick, Is.EqualTo(5));
            source.Fail = true; coordinator.Tick(255);
            Assert.That(coordinator.Query("p", 1, 255).Decision, Is.EqualTo(ShadowDecision.Error)); Assert.That(coordinator.SourceErrors, Is.EqualTo(1));
            source.Fail = false; coordinator.Tick(505);
            Assert.That(coordinator.Query("p", 1, 505).Decision, Is.Not.EqualTo(ShadowDecision.Error));
            source.People[0] = new PerceptionSubject("p", 2); coordinator.Tick(605);
            Assert.That(coordinator.Query("p", 1, 605), Is.Null); Assert.That(coordinator.Query("p", 2, 605), Is.Not.Null);
        }
        [Test] public void CoordinatorHasBoundedPopulationAndOwnerThread()
        {
            var source = new Source(); for (int i = 0; i < 4100; i++) source.People.Add(new PerceptionSubject("p" + i, 1));
            var coordinator = new ShadowPlanningCoordinator(source); coordinator.Tick(10);
            Assert.That(coordinator.OmittedSubjects, Is.EqualTo(4));
            Exception error = Task.Run(() => { try { coordinator.Query("p0", 1, 10); return null; } catch (Exception e) { return e; } }).Result;
            Assert.That(error, Is.TypeOf<InvalidOperationException>());
        }
        [Test] public void NewPublishedPopulationStartsPlanningWithoutWaitingForThePollingInterval()
        {
            var source = new Source(); var coordinator = new ShadowPlanningCoordinator(source); coordinator.Tick(0);
            source.People = new List<PerceptionSubject> { new PerceptionSubject("p", 1) };
            coordinator.Tick(1); Assert.That(coordinator.Query("p", 1, 1), Is.Not.Null); Assert.That(source.Reads, Is.EqualTo(1));
        }
        private sealed class Words : INeedDashboardText
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
            public Words(string language)
            {
                string root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../.."));
                var document = new XmlDocument(); document.Load(Path.Combine(root, "Languages", language, "Keyed", "PawnEconomyPlanning.xml"));
                foreach (XmlNode child in document.DocumentElement.ChildNodes) if (child.NodeType == XmlNodeType.Element) Values.Add(child.Name, child.InnerText);
            }
            public string Text(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, Values["PC_PEA.Needs." + key], args);
            public string NeedLabel(string id) => id;
            /// <summary>本 fixture 只加载政策键文件，跨命名空间标签不在此测试面内 —— 原样返回键名。</summary>
            public string IdLabel(string key) => key;
        }
        [Test] public void BothLanguagesCoverAllDecisionsBudgetStatesAndPublishedReasons()
        {
            var zh = new Words("ChineseSimplified"); var en = new Words("English");
            Assert.That(zh.Values.Keys, Is.EquivalentTo(en.Values.Keys));
            foreach (ShadowDecision decision in Enum.GetValues(typeof(ShadowDecision)))
                foreach (var words in new[] { zh, en }) Assert.That(words.Text("Planning.Decision." + decision), Is.Not.Empty);
            foreach (BudgetState state in Enum.GetValues(typeof(BudgetState)))
                foreach (var words in new[] { zh, en }) Assert.That(words.Text("Planning.Budget." + state), Is.Not.Empty);
            foreach (var words in new[] { zh, en })
            {
                var presenter = new ShadowPlanPresenter(words);
                Assert.That(presenter.Build(null, 10), Is.Not.Empty);
                Assert.That(presenter.Build(Plan(Input()), 10).Any(r => r.Value == words.Text("Planning.Decision.WaitingOpportunity")), Is.True);
                Assert.That(presenter.Build(Plan(Input()), 1000).Any(r => r.Value == words.Text("Planning.Stale")), Is.True);
                // 生活政策由 BuildPolicy 产出独立政策卡（长短期同屏，决策页零重复）；
                // 就业不适用时**工时项**必须如实写 NotApplicable，不得静默显示数字。
                Assert.That(presenter.BuildPolicy(Plan(Input(facts: Facts(stage: "Child")))).WorkHours,
                    Is.EqualTo(words.Text("Planning.NotApplicable")));
            }
            foreach (string key in zh.Values.Keys)
            {
                string[] Placeholders(string value) => System.Text.RegularExpressions.Regex.Matches(value, @"\{\d+\}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).ToArray();
                Assert.That(Placeholders(zh.Values[key]), Is.EquivalentTo(Placeholders(en.Values[key])), key);
            }
        }
    }
}
