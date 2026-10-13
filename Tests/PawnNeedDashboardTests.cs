using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Planning;
using PersonalChronicle.Economy.PawnEconomy;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class PawnNeedDashboardTests
    {
        private sealed class Source : INeedDashboardSource
        {
            public long Epoch { get; set; } = 20;
            public PerceptionReadiness Availability { get; set; } = PerceptionReadiness.Ready;
            public List<PerceptionSubject> People = new List<PerceptionSubject>();
            public IReadOnlyList<PerceptionSubject> Subjects => People;
            public PerceptionDiagnostics Diagnostics { get; set; } = new PerceptionDiagnostics(1, 0, 1, 0, 0, 0, 0, 0, 100, true, 0);
            public readonly List<PerceptionReadRequest> Requests = new List<PerceptionReadRequest>();
            public Func<PerceptionReadRequest, int, FactQueryResult> Reader;
            public FactQueryResult Read(PerceptionReadRequest request, int now)
            { Requests.Add(request); return Reader == null ? Facts(request.PawnId, request.SessionEpoch) : Reader(request, now); }
        }
        private sealed class Words : INeedDashboardText
        {
            private readonly Dictionary<string, string> values = new Dictionary<string, string>();
            public Words(string language)
            {
                string root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../.."));
                // 需求页消费三套键：PawnEconomyNeeds（页正文）、PawnEconomyPlanning（长期政策行）、
                // PawnEconomy（跨命名空间的稳定 ID 标签，如 PC_PEA.Care.Service.*）。
                foreach (string file in new[] { "PawnEconomyNeeds.xml", "PawnEconomyPlanning.xml", "PawnEconomy.xml" })
                {
                    var xml = new XmlDocument(); xml.Load(Path.Combine(root, "Languages", language, "Keyed", file));
                    foreach (XmlNode child in xml.DocumentElement.ChildNodes)
                        if (child.NodeType == XmlNodeType.Element && !values.ContainsKey(child.Name)) values.Add(child.Name, child.InnerText);
                }
            }
            public string Text(string suffix, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, values["PC_PEA.Needs." + suffix], arguments);
            public string NeedLabel(string id) => "need:" + id;
            /// <summary>完整键名直译：键缺失即抛（有牙齿 —— 新增的分区标签必须真实落在键文件里）。</summary>
            public string IdLabel(string key) => values[key];
        }
        private static NumericNeedFact Need(string id = "Food", Fact<float>? current = null, bool supported = true, int tick = 100)
            => new NumericNeedFact(id, "food", "test", current ?? Fact<float>.Known(.6f), Fact<float>.Known(2f), supported, "nutrition", 1, tick);
        private static FactQueryResult Facts(string id = "p", long epoch = 20, IEnumerable<NumericNeedFact> needs = null,
            PerceptionReadiness readiness = PerceptionReadiness.Ready, BodyLifeFactSet body = null)
        {
            var snapshot = new PawnFactSnapshot(id, epoch, 1, 1,
                new FactGroup<PawnContextFact>(CaptureState.Complete,
                    new PawnContextFact(SubjectLocation.HeldOnMap, 1, developmentalStage: Fact<string>.Known("Baby"),
                        downed: Fact<bool>.Known(true), drafted: Fact<bool>.Known(true), playerForced: Fact<bool>.Known(true), displayName: Fact<string>.Known("Lin")), 100, 1, "test"),
                new FactGroup<NeedFactSet>(CaptureState.Complete, new NeedFactSet(needs ?? new[] { Need() }, new[] { "Food" }, null, null,
                    Fact<float>.NotApplicable("NoMood")), 100, 1, "test"),
                body == null ? null : new FactGroup<BodyLifeFactSet>(CaptureState.Partial, body, 20, 1, "test"));
            return new FactQueryResult(readiness, readiness.ToString(), readiness == PerceptionReadiness.Ready, snapshot,
                new[] { new GroupReadStatus(PerceptionGroups.Needs, CaptureState.Complete, 10, true, readiness, "test"),
                    new GroupReadStatus(PerceptionGroups.Body, body == null ? CaptureState.Unknown : CaptureState.Partial, body == null ? -1 : 90, false,
                        body == null ? PerceptionReadiness.Loading : PerceptionReadiness.Stale, "test") });
        }
        private static Source One(Func<PerceptionReadRequest, int, FactQueryResult> reader = null)
        { var source = new Source { Reader = reader }; source.People.Add(new PerceptionSubject("p", 1)); return source; }
        private static NeedDashboardSnapshot Build(Source source, int page = 0, string id = null, IReadOnlyDictionary<string, NeedTarget> targets = null)
            => NeedDashboardBuilder.Build(source, 1, 110, page, id, targets);
        private static NeedDashboardView Present(NeedDashboardSnapshot snapshot, string language = "English")
            => new NeedDashboardPresenter(new Words(language)).Build(snapshot);

        [Test] public void PaginationFiltersOnlyByMapAndUsesStableIdsWithABoundedReadCount()
        {
            var source = One(); source.People.Clear();
            for (int i = 24; i >= 0; i--) source.People.Add(new PerceptionSubject("p" + i.ToString("D2"), 1));
            source.People.Add(new PerceptionSubject("p00", 1)); source.People.Add(new PerceptionSubject("other", 2));
            var view = Build(source);
            Assert.That(view.MapSubjects, Is.EqualTo(25)); Assert.That(view.PageCount, Is.EqualTo(3));
            Assert.That(view.Roster.Select(p => p.PawnId), Is.EqualTo(Enumerable.Range(0, 12).Select(i => "p" + i.ToString("D2"))));
            Assert.That(source.Requests.Count, Is.EqualTo(12));
            Assert.That(view.Selected.Facts.Snapshot.Context.Data.DevelopmentalStage.TryGetValue(out string stage), Is.True);
            Assert.That(stage, Is.EqualTo("Baby")); Assert.That(view.Selected.Facts.Snapshot.Context.Data.Downed.TryGetValue(out bool downed) && downed, Is.True);
        }
        [TestCase(-99, 0, 12)] [TestCase(999, 2, 1)]
        public void PageBoundsClampWithoutDroppingTheLastMember(int requested, int actual, int count)
        {
            var source = One(); source.People.Clear();
            for (int i = 0; i < 25; i++) source.People.Add(new PerceptionSubject("p" + i.ToString("D2"), 1));
            var page = Build(source, requested);
            Assert.That(page.PageIndex, Is.EqualTo(actual)); Assert.That(page.Roster.Count, Is.EqualTo(count));
            Assert.That(source.Requests.Count, Is.EqualTo(count));
        }
        [Test] public void SelectionOutsidePageRequiresOnlyOneExtraReadAndDoesNotSwitchPawn()
        {
            var source = One(); source.People.Clear();
            for (int i = 0; i < 24; i++) source.People.Add(new PerceptionSubject("p" + i.ToString("D2"), 1));
            var page = Build(source, id: "p23");
            Assert.That(page.SelectedId, Is.EqualTo("p23")); Assert.That(source.Requests.Count, Is.EqualTo(13));
        }
        [Test] public void VanishedSelectionRetainsOutOfScopeWithoutShowingAnotherPawnsNeeds()
        {
            var source = One((r, n) => r.PawnId == "gone" ? new FactQueryResult(PerceptionReadiness.OutOfScope, "Left", false) : Facts());
            var view = Present(Build(source, id: "gone"));
            Assert.That(view.Snapshot.SelectedId, Is.EqualTo("gone")); Assert.That(view.SelectionState, Is.EqualTo("Out of scope"));
            Assert.That(view.Needs, Is.Empty);
        }
        [TestCase((int)PerceptionReadiness.Loading, "Awaiting sample")] [TestCase((int)PerceptionReadiness.Error, "Read error")]
        public void UninitializedOrFailedSessionsNeverIssueFakeValidEpochQueries(int state, string label)
        {
            var source = One(); source.Epoch = 0; source.Availability = (PerceptionReadiness)state; source.Diagnostics = null;
            var view = Present(Build(source));
            Assert.That(source.Requests, Is.Empty); Assert.That(view.SelectionState, Is.EqualTo(label));
            Assert.That(view.Coverage, Is.EqualTo(label)); Assert.That(view.Needs, Is.Empty);
        }
        [Test] public void EmptyAuthoritativeCensusIsNotPresentedAsAllNeedsZero()
        {
            var source = One(); source.People.Clear();
            var view = Present(Build(source));
            Assert.That(view.Roster, Is.Empty); Assert.That(view.Needs, Is.Empty);
            Assert.That(view.SelectionState, Does.Contain("No observed members"));
        }
        [Test] public void RepeatedBuildsOnlyUseReadPortAndDisplayPolicy()
        {
            var source = One(); var original = Facts(); source.Reader = (r, n) => original;
            for (int i = 0; i < 100; i++) Assert.That(Build(source).Selected.Facts, Is.SameAs(original));
            Assert.That(source.Requests.All(r => r.Purpose == PerceptionPurpose.Display
                && r.RequiredGroups == (PerceptionGroups.Context | PerceptionGroups.Needs)
                && r.MaximumAge(PerceptionGroups.Needs) == 300 && r.MaximumAge(PerceptionGroups.Body) == 500), Is.True);
            Assert.That(original.Snapshot.Needs.Data.Needs[0].CurrentLevel.TryGetValue(out float value) && value == .6f, Is.True);
        }
        [Test] public void SessionChangeIsReflectedInEveryRequestAndNewSnapshot()
        {
            var source = One(); Build(source); source.Epoch = 21;
            var next = Build(source);
            Assert.That(next.Epoch, Is.EqualTo(21)); Assert.That(source.Requests.Last().SessionEpoch, Is.EqualTo(21));
            Assert.That(next.Selected.Facts.Snapshot.SessionEpoch, Is.EqualTo(21));
        }
        [TestCase("English")] [TestCase("ChineseSimplified")]
        public void NonUnitFoodMaximumAndMissingTargetsRemainDistinct(string language)
        {
            var view = Present(Build(One()), language); var words = new Words(language);
            Assert.That(view.Needs[0].Current, Does.Contain(0.3f.ToString("P0")));
            Assert.That(view.Needs[0].Current, Does.Contain("0.6 / 2"));
            Assert.That(view.Needs[0].Target, Is.EqualTo(words.Text("Unconfigured")));
            Assert.That(view.Needs[0].Gap, Is.EqualTo(words.Text("Unconfigured")));
        }
        [Test] public void ConfiguredTargetUsesPercentagePointsWithoutTreatingPaymentAsFulfillment()
        {
            var targets = new Dictionary<string, NeedTarget> { ["Food"] = new NeedTarget(TargetState.Enabled, Fact<float>.Known(.8f), 2) };
            var line = Present(Build(One(), targets: targets)).Needs.Single();
            Assert.That(line.Target, Is.EqualTo(.8f.ToString("P0"))); Assert.That(line.Gap, Is.EqualTo("50"));
            Assert.That(line.Source, Does.Contain("Vanilla Need"));
        }
        [Test] public void KnownZeroUnknownNotApplicableUnsupportedAndErrorDoNotCollapse()
        {
            var readings = new[] { Need("zero", Fact<float>.Known(0)), Need("unknown", Fact<float>.Unknown("x")),
                Need("na", Fact<float>.NotApplicable("x")), Need("special", Fact<float>.Known(.1f), false), Need("error", Fact<float>.Error("x")) };
            var view = Present(Build(One((r, n) => Facts(needs: readings))));
            Assert.That(view.Needs[0].Current, Does.StartWith(0f.ToString("P0")));
            Assert.That(view.Needs[1].Current, Does.StartWith("Unknown")); Assert.That(view.Needs[2].Current, Does.StartWith("N/A"));
            Assert.That(view.Needs[3].Current, Does.StartWith("Unsupported")); Assert.That(view.Needs[4].Current, Does.StartWith("Read error"));
            Assert.That(view.Needs.Select(p => p.Status).Distinct().Count(), Is.EqualTo(5));
        }
        [Test] public void DisabledTargetIsNotApplicableRatherThanUnconfiguredOrZeroGap()
        {
            var targets = new Dictionary<string, NeedTarget> { ["Food"] = new NeedTarget(TargetState.Disabled, default, 2) };
            var line = Present(Build(One(), targets: targets)).Needs.Single();
            Assert.That(line.Target, Is.EqualTo("N/A")); Assert.That(line.Gap, Is.EqualTo("N/A"));
        }
        [Test] public void SlowBodyConditionKeepsItsOwnAgeAndUnknownCollectionsDoNotClaimZero()
        {
            var body = new BodyLifeFactSet(null, null, new[] { new NeedConditionFact("Beauty", Fact<float>.Known(.5f), 20, 1) },
                default, default, default, Fact<BedFact>.NotApplicable("NoBed"), default, default);
            var view = Present(Build(One((r, n) => Facts(body: body))));
            Assert.That(view.Needs[0].Age, Is.EqualTo("10 ticks ago"));
            Assert.That(view.Facts.Single(p => p.Label == "need:Beauty: instant condition").Value, Does.Contain("90 ticks ago"));
            // D09 健康属"状态条件卡"分区（2026-10-09 起由 Facts 移入 Status，因为设计红线要求它不造需求条）。
            Assert.That(view.Status.Single(p => p.Label == "Visible health records").Value, Is.EqualTo("Unknown"));
            Assert.That(view.Facts.Single(p => p.Label == "Assigned bed ID").Value, Is.EqualTo("N/A"));
        }
        [Test] public void StaleValuesRemainVisibleWithStaleStatusAndOriginalAge()
        {
            var view = Present(Build(One((r, n) => Facts(readiness: PerceptionReadiness.Stale))));
            Assert.That(view.SelectionState, Is.EqualTo("Stale")); Assert.That(view.Needs.Single().Status, Does.Contain("Stale"));
            Assert.That(view.Needs.Single().Age, Is.EqualTo("10 ticks ago"));
        }
        [Test] public void ReadModelsDefensivelyCopyListsAndRetainImmutableNames()
        {
            var source = One(); var snapshot = Build(source); source.People.Clear();
            var roster = new List<NeedRosterLine> { new NeedRosterLine("p", "Lin") };
            var view = new NeedDashboardView(snapshot, "", "", "", roster, null, null); roster.Clear();
            Assert.That(snapshot.Roster.Count, Is.EqualTo(1)); Assert.That(view.Roster.Count, Is.EqualTo(1));
            Assert.That(snapshot.Roster[0].Name.TryGetValue(out var name) && name == "Lin", Is.True);
        }
        [Test] public void OneSubjectQueryFailureDoesNotEraseTheOtherSubjectsReadings()
        {
            var source = One((r, n) => r.PawnId == "bad" ? throw new InvalidOperationException("test") : Facts(r.PawnId));
            source.People.Add(new PerceptionSubject("bad", 1));
            var view = Present(Build(source, id: "p"));
            Assert.That(view.Roster.Count, Is.EqualTo(2));
            Assert.That(view.Roster.Single(p => p.Id == "bad").Text, Does.Contain("Read error"));
            Assert.That(view.Needs.Count, Is.EqualTo(1)); Assert.That(view.SelectionState, Is.EqualTo("Fresh"));
        }
        [Test] public void MissingQueryResultIsAnErrorRatherThanAnEmptyHealthyMember()
        {
            var view = Present(Build(One((r, n) => null)));
            Assert.That(view.SelectionState, Is.EqualTo("Read error")); Assert.That(view.Needs, Is.Empty);
        }
        [TestCase("English")] [TestCase("ChineseSimplified")]
        public void EveryFiniteStateHasALocalizedLabel(string language)
        {
            var words = new Words(language);
            foreach (var state in Enum.GetValues(typeof(FactState))) Assert.That(words.Text("Fact." + state), Is.Not.Empty);
            foreach (var state in Enum.GetValues(typeof(PerceptionReadiness))) Assert.That(words.Text("Readiness." + state), Is.Not.Empty);
            foreach (var state in Enum.GetValues(typeof(CaptureState))) Assert.That(words.Text("Capture." + state), Is.Not.Empty);
            foreach (var state in Enum.GetValues(typeof(SubjectLocation))) Assert.That(words.Text("Location." + state), Is.Not.Empty);
        }
        [TestCase("known unknown", 7, "known\nunknown")]
        [TestCase("需求缺口状态", 3, "需求缺\n口状态")]
        [TestCase("abcdef", 3, "abc\ndef")]
        [TestCase("a\n\nb", 10, "a\n\nb")]
        public void ExplicitLineFlowFitsNarrowCellsAndPreservesWordsOrExistingBreaks(string value, int width, string expected)
        {
            string result = NeedDashboardTextFlow.Wrap(value, width, s => new StringInfo(s).LengthInTextElements);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(result.Split('\n').All(line => new StringInfo(line).LengthInTextElements <= width), Is.True);
        }
        [Test] public void ExplicitLineFlowDoesNotSplitSurrogatePairsOrCombiningMarks()
        {
            string result = NeedDashboardTextFlow.Wrap("A\U0001F600e\u0301B", 2, s => new StringInfo(s).LengthInTextElements);
            Assert.That(result, Is.EqualTo("A\U0001F600\ne\u0301B"));
        }
        [Test] public void EmptyLabelsStayEmptyInsteadOfCreatingPlaceholderValues()
        {
            Assert.That(NeedDashboardTextFlow.Wrap(null, 10, s => s.Length), Is.Empty);
            Assert.That(NeedDashboardTextFlow.Wrap("", 10, s => s.Length), Is.Empty);
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidLineFlowWidthIsRejected(float width)
        { Assert.Throws<ArgumentOutOfRangeException>(() => NeedDashboardTextFlow.Wrap("need", width, s => s.Length)); }

        // ---- Pawn 检视窗「个人需求」单人视图（BuildSingle） ----
        private static NeedDashboardSnapshot Single(Source source, string id,
            IReadOnlyDictionary<string, NeedTarget> targets = null,
            Func<string, IReadOnlyDictionary<string, NeedTarget>> provider = null)
            => NeedDashboardBuilder.BuildSingle(source, 1, 110, id, targets, provider);

        [Test] public void SingleDashboardReadsExactlyOnePawnAndKeepsAnEmptyRoster()
        {
            var source = One(); source.People.Clear();
            for (int i = 0; i < 24; i++) source.People.Add(new PerceptionSubject("p" + i.ToString("D2"), 1));
            var single = Single(source, "p23");
            Assert.That(source.Requests.Count, Is.EqualTo(1));
            Assert.That(source.Requests[0].PawnId, Is.EqualTo("p23"));
            Assert.That(single.SelectedId, Is.EqualTo("p23"));
            Assert.That(single.Roster.Count, Is.EqualTo(0));
            Assert.That(single.PageIndex, Is.EqualTo(0));
            Assert.That(single.PageCount, Is.EqualTo(1));
            Assert.That(single.MapSubjects, Is.EqualTo(24));
            Assert.That(single.Selected.Facts.Snapshot.PawnId, Is.EqualTo("p23"));
        }

        [Test] public void SingleSelectionMatchesTheRosterPathForTheSamePawn()
        {
            var source = One(); source.People.Clear();
            for (int i = 0; i < 24; i++) source.People.Add(new PerceptionSubject("p" + i.ToString("D2"), 1));
            var page = Build(source, id: "p23");
            int pageReads = source.Requests.Count;
            source.Requests.Clear();
            var single = Single(source, "p23");
            Assert.That(pageReads, Is.EqualTo(13));
            Assert.That(source.Requests.Count, Is.EqualTo(1));
            var fromPage = Present(page); var fromSingle = Present(single);
            Assert.That(fromSingle.Selection, Is.EqualTo(fromPage.Selection));
            Assert.That(fromSingle.SelectionState, Is.EqualTo(fromPage.SelectionState));
            Assert.That(fromSingle.Coverage, Is.EqualTo(fromPage.Coverage));
            Assert.That(fromSingle.Needs.Select(x => x.Name + "|" + x.Status), Is.EqualTo(fromPage.Needs.Select(x => x.Name + "|" + x.Status)));
            Assert.That(fromSingle.Facts.Select(x => x.Label + "=" + x.Value), Is.EqualTo(fromPage.Facts.Select(x => x.Label + "=" + x.Value)));
        }

        [Test] public void SingleQueryReadsNothingBeforeTheFirstSampleAndStaysRenderable()
        {
            var source = One(); source.Epoch = 0;
            var single = Single(source, "p");
            Assert.That(source.Requests.Count, Is.EqualTo(0));
            Assert.That(single.Epoch, Is.EqualTo(0));
            Assert.That(single.Selected, Is.Not.Null);
            Assert.That(single.Selected.Gaps, Is.Empty);
            Assert.That(Present(single).Needs, Is.Empty);
        }

        [Test] public void SingleCountsOnlyMapSubjectsWithoutReadingThem()
        {
            var source = One(); source.People.Clear();
            source.People.Add(new PerceptionSubject("p1", 1));
            source.People.Add(new PerceptionSubject("p2", 1));
            source.People.Add(new PerceptionSubject("other", 2));
            source.People.Add(new PerceptionSubject("p1", 1));
            var single = Single(source, "p1");
            Assert.That(single.MapSubjects, Is.EqualTo(2));
            Assert.That(source.Requests.Count, Is.EqualTo(1));
        }

        [Test] public void SingleViewResolvesTheSameTargetPolicyAsTheRosterPath()
        {
            // 无目标策略时「目标 / 缺口」为未配置；接通策略后必须变为真实数值。
            var targets = new Dictionary<string, NeedTarget>(StringComparer.Ordinal)
            { ["Food"] = new NeedTarget(TargetState.Enabled, Fact<float>.Known(.8f), 1) };
            var source = One();
            var without = Present(Single(source, "p"));
            source.Requests.Clear();
            var with = Present(Single(source, "p", targets));
            Assert.That(without.Needs[0].Target, Is.Not.EqualTo(with.Needs[0].Target), "Target must react to the policy");
            Assert.That(without.Needs[0].Gap, Is.Not.EqualTo(with.Needs[0].Gap), "Gap must react to the policy");
            Assert.That(with.Needs[0].Target, Does.Contain("%"));
            Assert.That(source.Requests.Count, Is.EqualTo(1));
        }

        [Test] public void SingleViewResolvesTargetsThroughTheProviderExactlyOnce()
        {
            var source = One(); var asked = new List<string>();
            var targets = new Dictionary<string, NeedTarget>(StringComparer.Ordinal)
            { ["Food"] = new NeedTarget(TargetState.Enabled, Fact<float>.Known(.8f), 1) };
            var single = NeedDashboardBuilder.BuildSingle(source, 1, 110, "p",
                targetProvider: id => { asked.Add(id); return targets; });
            Assert.That(asked, Is.EqualTo(new[] { "p" }));
            Assert.That(source.Requests.Count, Is.EqualTo(1));
            var view = Present(single);
            Assert.That(view.Needs.Count, Is.EqualTo(1));
            // provider 的返回值必须真正落进视图；否则"调了 provider 却丢弃结果"的实现也会通过。
            Assert.That(view.Needs[0].Target, Does.Contain("%"));
        }

        [Test] public void LongTermPolicyIsSplitOutOfTheDecisionRowsAndCarriesItsParameters()
        {
            // 长短期同屏（2026-10-09）：生活政策由 BuildLongTerm 单独产出，决策页行不得重复同一份数据。
            var plan = new ShadowPlan("p", 20, 1, 110, 0, ShadowDecision.NoIncomeNeeded, "ReserveSufficient",
                new LifePolicy(null), new LifeBudget(BudgetState.Ready, "Ok"), employmentApplicable: Fact<bool>.Known(true));
            var planner = new ShadowPlanPresenter(new Words("English"));
            var policy = planner.BuildPolicy(plan);
            var decision = planner.Build(plan, 110);
            Assert.That(policy, Is.Not.Null, "long-term policy card must be produced");
            Assert.That(policy.WorkHours, Does.Contain("8"), "work-hours item must carry the parameter");
            Assert.That(policy.WorkHours01, Is.EqualTo(8f / 24f).Within(.001f), "work hours normalise against a 24h day");
            Assert.That(policy.Saving01, Is.EqualTo(.2f).Within(.001f), "saving basis points normalise to a ratio");
            Assert.That(policy.Sources, Is.Not.Empty, "sources must resolve at least the baseline source");
            // 零重复：政策项不得同时出现在决策页行里（政策名与决策行标签不得重合）。
            var decisionLabels = decision.Select(row => row.Label).ToList();
            foreach (var label in new[] { policy.WorkHours, policy.Leisure, policy.Reserve, policy.Saving })
                Assert.That(decisionLabels.Contains(label), Is.False, "decision rows must not repeat: " + label);
        }

        [Test] public void LongTermAreaIsAnEmptyCollectionWhenNoLiveShadowPlanExists()
        {
            var planner = new ShadowPlanPresenter(new Words("English"));
            Assert.That(planner.BuildPolicy(null), Is.Null, "no live plan => no policy card");
            var source = One();
            Assert.That(Present(Build(source, 0, "p")).Policy, Is.Null,
                "roster path keeps the policy card absent");
            Assert.That(Present(NeedDashboardBuilder.BuildSingle(source, 1, 110, "p")).Policy, Is.Null,
                "single path with no live shadow plan has no policy card");
        }
    }
}
