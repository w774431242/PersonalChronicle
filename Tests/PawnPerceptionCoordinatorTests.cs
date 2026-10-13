using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class PawnPerceptionCoordinatorTests
    {
        private sealed class Source : IPawnPerceptionSource
        {
            public readonly List<PerceptionSubject> Population = new List<PerceptionSubject>();
            public readonly List<PawnCaptureRequest> Requests = new List<PawnCaptureRequest>();
            public readonly List<string> Released = new List<string>();
            public PopulationUpdate Update;
            public Func<PawnCaptureRequest, PawnCaptureResult> Reader;
            private bool first;
            public void Reset() { first = true; }
            public PopulationUpdate PollPopulation(int tick)
            {
                if (Update != null) { var result = Update; Update = null; first = false; return result; }
                if (!first) return null;
                first = false; return new PopulationUpdate(Population, true);
            }
            public PawnCaptureResult Capture(PawnCaptureRequest request)
            { Requests.Add(request); return Reader == null ? Normal(request) : Reader(request); }
            public void Release(string id) { Released.Add(id); }
        }
        private static FactGroup<T> Group<T>(T data, PawnCaptureRequest r, CaptureState state = CaptureState.Complete) where T : class
            => new FactGroup<T>(state, data, r.Tick, r.Revision, "test");
        private static PawnCaptureResult Normal(PawnCaptureRequest r, SubjectLocation location = SubjectLocation.SpawnedMap, int map = 1)
        {
            var context = new PawnContextFact(location, map, developmentalStage: Fact<string>.Known("Baby"),
                downed: Fact<bool>.Known(true), drafted: Fact<bool>.Known(true), playerForced: Fact<bool>.Known(true));
            return new PawnCaptureResult(Group(context, r), (r.Groups & PerceptionGroups.Needs) == 0 ? null
                : Group(new NeedFactSet(new[] { new NumericNeedFact("Food", "food", "test", Fact<float>.Known(.3f),
                    Fact<float>.Known(2f), true, "nutrition", r.Revision, r.Tick) }, new[] { "Food" }, null, null, default), r),
                (r.Groups & PerceptionGroups.Body) == 0 ? null : Group(new BodyLifeFactSet(null, null,
                    new[] { new NeedConditionFact("Beauty", Fact<float>.Known(.9f), r.Tick, r.Revision) },
                    Fact<float>.Known(20), Fact<float>.Known(18), Fact<float>.Known(26),
                    Fact<BedFact>.NotApplicable("NoBed"), Fact<BedFact>.NotApplicable("NoBed"), default,
                    CaptureState.Complete, CaptureState.Complete, CaptureState.Complete), r),
                (r.Groups & PerceptionGroups.Relationships) == 0 ? null : Group(new RelationshipFactSet(null), r));
        }
        private Source source;
        private PerceptionCoordinator coordinator;
        private long epoch;
        [SetUp] public void Setup()
        {
            epoch = 100; source = new Source(); source.Population.Add(new PerceptionSubject("p", 1));
            coordinator = Make(new PerceptionSettings(fast: 2, body: 1000, relationships: 6000, budgetMilliseconds: 1000));
        }
        private PerceptionCoordinator Make(PerceptionSettings settings) => new PerceptionCoordinator(source, settings, () => ++epoch);
        private PerceptionReadRequest Request(string id = "p", PerceptionGroups groups = PerceptionGroups.All,
            int fastAge = 10, int bodyAge = 1000, int relationshipAge = 6000, int map = 1,
            PerceptionPurpose purpose = PerceptionPurpose.Display, long? session = null)
            => new PerceptionReadRequest(id, session ?? coordinator.SessionEpoch, map, purpose, groups, fastAge, bodyAge, relationshipAge);

        [Test] public void InitialReadIsLoadingAndDoesNotStartCollection()
        {
            Assert.That(coordinator.QueryFacts(Request(), 0).Readiness, Is.EqualTo(PerceptionReadiness.Loading));
            Assert.That(source.Requests, Is.Empty);
        }
        [Test] public void ReadOnlyQueriesDoNotRecaptureOrModifyPublishedSnapshots()
        {
            coordinator.Tick(0);
            var original = coordinator.QueryFacts(Request(), 0);
            for (int i = 0; i < 100; i++) Assert.That(coordinator.QueryFacts(Request(), 1).Snapshot, Is.SameAs(original.Snapshot));
            Assert.That(source.Requests.Count, Is.EqualTo(1));
            Assert.That(original.RequiredGroupsUsable, Is.True);
        }
        [Test] public void HeldChildWithControlRestrictionsStillHasItsOwnNeeds()
        {
            source.Reader = r => Normal(r, SubjectLocation.HeldOnMap);
            coordinator.Tick(0); var result = coordinator.QueryFacts(Request(), 0);
            Assert.That(result.Snapshot.Context.Data.Location, Is.EqualTo(SubjectLocation.HeldOnMap));
            Assert.That(result.Snapshot.Context.Data.DevelopmentalStage.TryGetValue(out var stage), Is.True);
            Assert.That(stage, Is.EqualTo("Baby"));
            Assert.That(result.Snapshot.Needs.Data.Needs.Count, Is.EqualTo(1));
            Assert.That(result.Snapshot.Needs.Data.Needs[0].MaximumLevel.TryGetValue(out var maximum), Is.True);
            Assert.That(maximum, Is.EqualTo(2f));
        }
        [Test] public void FastRefreshDoesNotRefreshBodyOrConditionAge()
        {
            coordinator.Tick(0); coordinator.Tick(5);
            var result = coordinator.QueryFacts(Request(groups: PerceptionGroups.Needs | PerceptionGroups.Body, bodyAge: 2), 5);
            Assert.That(result.Snapshot.Needs.CapturedTick, Is.EqualTo(5));
            Assert.That(result.Snapshot.Body.CapturedTick, Is.Zero);
            Assert.That(result.Snapshot.Body.Data.Conditions[0].CapturedTick, Is.Zero);
            Assert.That(result.Readiness, Is.EqualTo(PerceptionReadiness.Stale));
            Assert.That(result.RequiredGroupsUsable, Is.False);
        }
        [Test] public void OptionalStaleGroupDoesNotEraseFreshRequiredFields()
        {
            coordinator.Tick(0); coordinator.Tick(5);
            var result = coordinator.QueryFacts(Request(groups: PerceptionGroups.Needs, bodyAge: 2), 5);
            Assert.That(result.Readiness, Is.EqualTo(PerceptionReadiness.Partial));
            Assert.That(result.RequiredGroupsUsable, Is.True);
        }
        [TestCase(0)] [TestCase(1)]
        public void BothPurposesRetainStaleDisplayValuesWithoutGrantingFreshness(int purpose)
        {
            coordinator.Tick(0);
            var result = coordinator.QueryFacts(Request(groups: PerceptionGroups.Needs, fastAge: 1,
                purpose: (PerceptionPurpose)purpose), 5);
            Assert.That(result.Readiness, Is.EqualTo(PerceptionReadiness.Stale));
            Assert.That(result.Snapshot, Is.Not.Null); Assert.That(result.RequiredGroupsUsable, Is.False);
        }
        [Test] public void WrongSessionReturnsNoSnapshotFromAnotherGame()
        {
            coordinator.Tick(0);
            var result = coordinator.QueryFacts(Request(session: coordinator.SessionEpoch + 1), 0);
            Assert.That(result.ReasonCode, Is.EqualTo("SessionMismatch")); Assert.That(result.Snapshot, Is.Null);
        }
        [Test] public void ClockRollbackRebuildsSessionAndRejectsOldRequests()
        {
            coordinator.Tick(100); var oldRequest = Request(); coordinator.Tick(90);
            Assert.That(coordinator.SessionEpoch, Is.Not.EqualTo(oldRequest.SessionEpoch));
            Assert.That(coordinator.QueryFacts(oldRequest, 90).ReasonCode, Is.EqualTo("SessionMismatch"));
            Assert.That(coordinator.QueryFacts(Request(), 89).ReasonCode, Is.EqualTo("ClockMovedBackwards"));
        }
        [Test] public void RepeatedTickWhilePausedDoesNotGenerateObservations()
        {
            coordinator.Tick(0); coordinator.Tick(0); coordinator.Tick(0);
            Assert.That(source.Requests.Count, Is.EqualTo(1)); Assert.That(coordinator.Diagnostics.Captures, Is.EqualTo(1));
        }
        [Test] public void MapScopeIsCheckedIndependentlyOfAge()
        {
            coordinator.Tick(0);
            Assert.That(coordinator.QueryFacts(Request(map: 2), 0).ReasonCode, Is.EqualTo("MapMismatch"));
        }
        [Test] public void PopulationMapMoveInvalidatesOldScopeThenCapturesNewContext()
        {
            coordinator.Tick(0); source.Update = new PopulationUpdate(new[] { new PerceptionSubject("p", 2) }, true);
            source.Reader = r => Normal(r, map: 2); coordinator.Tick(1);
            Assert.That(coordinator.QueryFacts(Request(), 1).Readiness, Is.EqualTo(PerceptionReadiness.OutOfScope));
            Assert.That(coordinator.QueryFacts(Request(map: 2), 1).RequiredGroupsUsable, Is.True);
        }
        [Test] public void FreshCaptureCanDetectMapMoveBeforeNextPopulationCycle()
        {
            coordinator.Tick(0); source.Reader = r => Normal(r, map: 2); coordinator.Tick(5);
            Assert.That(coordinator.Subjects.Single().MapId, Is.EqualTo(2));
            Assert.That(coordinator.QueryFacts(Request(), 5).Readiness, Is.EqualTo(PerceptionReadiness.OutOfScope));
            Assert.That(coordinator.QueryFacts(Request(map: 2, groups: PerceptionGroups.Needs), 5).RequiredGroupsUsable, Is.True);
        }
        [Test] public void CaptureExceptionPublishesErrorsAndRetriesOnSchedule()
        {
            source.Reader = r => throw new InvalidOperationException("failed getter");
            coordinator.Tick(0); var result = coordinator.QueryFacts(Request(), 0);
            Assert.That(result.Readiness, Is.EqualTo(PerceptionReadiness.Error));
            Assert.That(result.Snapshot.Needs.State, Is.EqualTo(CaptureState.Error));
            source.Reader = null; coordinator.Tick(5);
            Assert.That(coordinator.QueryFacts(Request(groups: PerceptionGroups.Needs), 5).RequiredGroupsUsable, Is.True);
            Assert.That(coordinator.Diagnostics.CaptureErrors, Is.EqualTo(1));
        }
        [Test] public void PartialRequiredGroupStaysUsableButIsStillReportedAsPartial()
        {
            // 2026-10-10 语义修正（实测驱动）：`Partial` 表示"该组**有数据**，但部分条目不支持/被截断"
            // （NeedFactSet 的 unsupported / failed / truncated）。此前只认 Ready ⇒ 实测中因**一个**需求的
            // StandardRatio 不支持就让 Needs 恒 Partial，三名殖民者的规划永久 FactsUnavailable、经济整链停摆。
            // 新语义：**可用性**只看"有没有数据"，**完整性**由 Readiness 如实报告 —— 两者分离，互不冒充。
            source.Reader = r => { var n = Normal(r); return new PawnCaptureResult(n.Context,
                new FactGroup<NeedFactSet>(CaptureState.Partial, n.Needs.Data, r.Tick, r.Revision, "UnsupportedNeed"), n.Body, n.Relationships); };
            coordinator.Tick(0); var result = coordinator.QueryFacts(Request(groups: PerceptionGroups.Needs), 0);
            Assert.That(result.Readiness, Is.EqualTo(PerceptionReadiness.Partial), "incompleteness must still be reported honestly");
            Assert.That(result.RequiredGroupsUsable, Is.True, "a partially captured group still carries usable data");
            Assert.That(result.Snapshot.Needs.Data?.Needs, Is.Not.Null, "the data itself is present");

            // 反向对照：Unknown / Error 仍是硬拒绝 —— 那才是真的"没有数据"。
            source.Reader = r => { var n = Normal(r); return new PawnCaptureResult(n.Context,
                new FactGroup<NeedFactSet>(CaptureState.Unknown, null, r.Tick, r.Revision, "NeedTrackerUnavailable"), n.Body, n.Relationships); };
            coordinator.MarkDirty("p", PerceptionGroups.Needs); coordinator.Tick(1);
            Assert.That(coordinator.QueryFacts(Request(groups: PerceptionGroups.Needs), 1).RequiredGroupsUsable, Is.False,
                "missing data must still block");
        }
        [Test] public void InvalidFutureSourceStampCannotBecomeFresh()
        {
            source.Reader = r => { var n = Normal(r); return new PawnCaptureResult(n.Context,
                new FactGroup<NeedFactSet>(CaptureState.Complete, n.Needs.Data, r.Tick + 1, r.Revision, "bad"), n.Body, n.Relationships); };
            coordinator.Tick(0); var result = coordinator.QueryFacts(Request(), 0);
            Assert.That(result.Snapshot.Needs.ReasonCode, Is.EqualTo("InvalidSourceStamp"));
            Assert.That(result.RequiredGroupsUsable, Is.False);
        }
        [Test] public void MissingRequestedGroupBecomesErrorInsteadOfReusingOldFreshValue()
        {
            coordinator.Tick(0);
            source.Reader = r => { var n = Normal(r); return new PawnCaptureResult(n.Context, n.Needs, null, n.Relationships); };
            coordinator.MarkDirty("p", PerceptionGroups.Body); coordinator.Tick(1);
            var result = coordinator.QueryFacts(Request(groups: PerceptionGroups.Needs), 1);
            Assert.That(result.Snapshot.Body.State, Is.EqualTo(CaptureState.Error));
            Assert.That(result.Snapshot.Body.CapturedTick, Is.EqualTo(1));
            Assert.That(result.RequiredGroupsUsable, Is.True);
        }
        [Test] public void BodyFailureDoesNotEraseValidNeeds()
        {
            source.Reader = r => { var n = Normal(r); return new PawnCaptureResult(n.Context, n.Needs,
                new FactGroup<BodyLifeFactSet>(CaptureState.Error, null, r.Tick, r.Revision, "GroupCaptureFailed"), n.Relationships); };
            coordinator.Tick(0); var result = coordinator.QueryFacts(Request(groups: PerceptionGroups.Needs), 0);
            Assert.That(result.Snapshot.Needs.State, Is.EqualTo(CaptureState.Complete));
            Assert.That(result.Readiness, Is.EqualTo(PerceptionReadiness.Partial)); Assert.That(result.RequiredGroupsUsable, Is.True);
        }
        [Test] public void DirtyRequestsMergeIntoOneCaptureAndPreserveRequestedGroups()
        {
            coordinator.Tick(0); coordinator.MarkDirty("p", PerceptionGroups.Needs);
            for (int i = 0; i < 100; i++) coordinator.MarkDirty("p", PerceptionGroups.Body);
            coordinator.Tick(1);
            Assert.That(source.Requests.Count, Is.EqualTo(2));
            Assert.That(source.Requests[1].Groups & (PerceptionGroups.Needs | PerceptionGroups.Body),
                Is.EqualTo(PerceptionGroups.Needs | PerceptionGroups.Body));
        }
        [Test] public void QuotaAndOldestDueOrderPreventContinuousDirtyTrafficFromStarvingOthers()
        {
            source.Population.Clear();
            for (int i = 0; i < 10; i++) source.Population.Add(new PerceptionSubject("p" + i, 1));
            coordinator = Make(new PerceptionSettings(fast: 1000, body: 1000, relationships: 1000,
                maxCaptures: 1, budgetMilliseconds: 1000));
            for (int tick = 0; tick < 25; tick++)
            {
                coordinator.MarkDirty("p0", PerceptionGroups.Needs); int before = source.Requests.Count;
                coordinator.Tick(tick); Assert.That(source.Requests.Count - before, Is.LessThanOrEqualTo(1));
            }
            Assert.That(source.Requests.Select(r => r.PawnId).Distinct().Count(), Is.EqualTo(10));
        }
        [Test] public void AuthoritativeDeparturesPublishDeathAndReleaseReferencesWithoutExtraCaptures()
        {
            coordinator.Tick(0); int calls = source.Requests.Count;
            source.Update = new PopulationUpdate(null, true, departures: new Dictionary<string, SubjectLocation> { ["p"] = SubjectLocation.Dead });
            coordinator.Tick(1); var result = coordinator.QueryFacts(Request(), 1);
            Assert.That(result.ReasonCode, Is.EqualTo("Dead")); Assert.That(coordinator.Subjects, Is.Empty);
            Assert.That(source.Released, Does.Contain("p")); Assert.That(source.Requests.Count, Is.EqualTo(calls));
        }
        [Test] public void PartialCensusDoesNotInventDepartures()
        {
            coordinator.Tick(0); source.Update = new PopulationUpdate(null, false, 1); coordinator.Tick(1);
            Assert.That(coordinator.Subjects.Count, Is.EqualTo(1)); Assert.That(source.Released, Is.Empty);
            Assert.That(coordinator.Diagnostics.PopulationComplete, Is.False);
        }
        [Test] public void FullReplacementAtCapacityRemovesOldBeforeAddingNew()
        {
            coordinator = Make(new PerceptionSettings(maxSubjects: 1, budgetMilliseconds: 1000)); coordinator.Tick(0);
            source.Update = new PopulationUpdate(new[] { new PerceptionSubject("new", 1) }, true); coordinator.Tick(1);
            Assert.That(coordinator.Subjects.Single().PawnId, Is.EqualTo("new"));
            Assert.That(coordinator.Diagnostics.OmittedSubjects, Is.Zero);
        }
        [Test] public void SubjectCapReportsPartialCoverageInsteadOfClaimingEmpty()
        {
            source.Population.Add(new PerceptionSubject("q", 1));
            coordinator = Make(new PerceptionSettings(maxSubjects: 1, budgetMilliseconds: 1000)); coordinator.Tick(0);
            Assert.That(coordinator.Subjects.Count, Is.EqualTo(1));
            Assert.That(coordinator.Diagnostics.OmittedSubjects, Is.EqualTo(1));
            Assert.That(coordinator.Diagnostics.PopulationComplete, Is.False);
            Assert.That(coordinator.QueryFacts(Request("q"), 0).Readiness, Is.EqualTo(PerceptionReadiness.Loading));
        }
        [Test] public void RetiredHistoryIsBoundedAndReentryDoesNotReuseOldSlowGroups()
        {
            coordinator = Make(new PerceptionSettings(maxRetired: 1, budgetMilliseconds: 1000)); coordinator.Tick(0);
            source.Update = new PopulationUpdate(null, true); coordinator.Tick(1);
            source.Update = new PopulationUpdate(new[] { new PerceptionSubject("q", 1) }, true); coordinator.Tick(2);
            source.Update = new PopulationUpdate(null, true); coordinator.Tick(3);
            Assert.That(coordinator.Diagnostics.RetiredSubjects, Is.EqualTo(1));
            source.Update = new PopulationUpdate(new[] { new PerceptionSubject("p", 1) }, true);
            source.Reader = r => new PawnCaptureResult(Normal(r).Context); coordinator.Tick(4);
            Assert.That(coordinator.QueryFacts(Request(), 4).Snapshot.Body.State, Is.Not.EqualTo(CaptureState.Complete));
        }
        [Test] public void ObservationProjectionRetainsAgesAndUnknownTargetsWithoutSourceQueries()
        {
            coordinator.Tick(0); int calls = source.Requests.Count;
            var missing = coordinator.QueryObservation(Request(fastAge: 1), 5, null);
            Assert.That(missing.Facts.Readiness, Is.EqualTo(PerceptionReadiness.Stale));
            Assert.That(missing.Gaps.Single().RelativeTargetGap.State, Is.EqualTo(FactState.Unknown));
            var known = coordinator.QueryObservation(Request(), 0,
                new Dictionary<string, NeedTarget> { ["Food"] = new NeedTarget(TargetState.Enabled, Fact<float>.Known(.8f), 2) });
            Assert.That(known.Gaps.Single().RawLevelShortfall.TryGetValue(out var raw), Is.True);
            Assert.That(raw, Is.EqualTo(1.3f).Within(.000001)); Assert.That(source.Requests.Count, Is.EqualTo(calls));
        }
        [Test] public void PublishedCollectionsCopyInputAndCannotBeMutatedByReaders()
        {
            var list = new List<string> { "Food" }; var facts = new NeedFactSet(null, list, null, null, default);
            list.Clear(); Assert.That(facts.ActualNeedIds.Count, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)facts.ActualNeedIds).Add("Rest"));
        }
        [Test] public void DefaultEmptySubcollectionsDoNotClaimConfirmedAbsence()
        {
            var body = new BodyLifeFactSet(null, null, null, default, default, default, default, default, default);
            Assert.That(body.VisibleHediffs, Is.Empty); Assert.That(body.HealthState, Is.EqualTo(CaptureState.Unknown));
            Assert.That(body.ApparelState, Is.EqualTo(CaptureState.Unknown));
            Assert.That(body.ConditionsState, Is.EqualTo(CaptureState.Unknown));
        }
        [Test] public void InvalidSettingsAndRequestsFailBeforeReadingWorld()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PerceptionSettings(fast: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PerceptionSettings(budgetMilliseconds: double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => Request(groups: PerceptionGroups.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => Request(fastAge: -1));
            Assert.That(source.Requests, Is.Empty);
        }
        [Test] public void BackgroundQueryAndCoordinationCannotRaceTheGameThread()
        {
            var request = Request();
            Task.Run(() => {
                Assert.Throws<InvalidOperationException>(() => coordinator.QueryFacts(request, 0));
                Assert.Throws<InvalidOperationException>(() => coordinator.Tick(0));
                Assert.Throws<InvalidOperationException>(() => coordinator.MarkDirty("p", PerceptionGroups.All));
                Assert.Throws<InvalidOperationException>(() => { var value = coordinator.Diagnostics; });
                Assert.Throws<InvalidOperationException>(() => { var value = coordinator.Subjects; });
            }).GetAwaiter().GetResult();
            Assert.That(source.Requests, Is.Empty);
        }
    }
}
