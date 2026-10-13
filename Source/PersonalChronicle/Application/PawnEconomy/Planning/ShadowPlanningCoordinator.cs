using System;
using System.Collections.Generic;
using System.Threading;
using PersonalChronicle.Application.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Planning;
using PersonalChronicle.Domain.PawnEconomy.Budget;

namespace PersonalChronicle.Application.PawnEconomy.Planning
{
    internal interface IShadowPlanningSource
    {
        long Epoch { get; }
        IReadOnlyList<PerceptionSubject> Subjects { get; }
        ShadowPlanningInput ReadInput(PerceptionSubject subject, int now);
    }
    /// <summary>Optional cross-session memory for the planning hysteresis. A source that does not
    /// implement it keeps the session-only behaviour instead of failing (B3.2 is additive).</summary>
    internal interface IShadowPlanningPersistence
    {
        ShadowPlanLatch ReadLatch(string pawnId, int mapId);
        void WriteLatch(ShadowPlan plan);
    }
    /// <summary>Optional sink notified once per built plan (never from UI). The host uses it to act on a
    /// published decision without the coordinator knowing anything about employment or contracts.</summary>
    internal interface IShadowPlanningSink
    {
        void OnPlanPublished(ShadowPlan plan);
    }
    /// <summary>Instance-owned shadow cache. Tick publishes; UI queries never collect or plan.</summary>
    internal sealed class ShadowPlanningCoordinator
    {
        private readonly IShadowPlanningSource source;
        private readonly IShadowPlanningPersistence persistence;
        private readonly IShadowPlanningSink sink;
        private readonly int owner = Thread.CurrentThread.ManagedThreadId;
        private sealed class Pending
        {
            public PerceptionSubject Subject;
            public long Due;
        }
        private readonly Dictionary<string, Pending> subjects = new Dictionary<string, Pending>(StringComparer.Ordinal);
        private readonly SortedSet<Pending> scheduled = new SortedSet<Pending>(Comparer<Pending>.Create((a, b) => {
            int order = a.Due.CompareTo(b.Due); return order != 0 ? order : StringComparer.Ordinal.Compare(a.Subject.PawnId, b.Subject.PawnId);
        }));
        private readonly Dictionary<string, ShadowPlan> reports = new Dictionary<string, ShadowPlan>(StringComparer.Ordinal);
        private long epoch, nextPopulation;
        private IReadOnlyList<PerceptionSubject> populationIdentity;
        private int lastTick = -1;
        public int LastProcessed { get; private set; }
        public int SourceErrors { get; private set; }
        public int OmittedSubjects { get; private set; }
        public ShadowPlanningCoordinator(IShadowPlanningSource source)
        { this.source = source ?? throw new ArgumentNullException(nameof(source)); persistence = source as IShadowPlanningPersistence; sink = source as IShadowPlanningSink; }
        private void AssertOwner() { if (owner != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("WrongPlanningThread"); }
        public void Tick(int now)
        {
            AssertOwner(); LastProcessed = 0;
            if (now < 0) throw new ArgumentOutOfRangeException(nameof(now));
            long currentEpoch = source.Epoch;
            if (epoch != currentEpoch || now < lastTick)
            { epoch = currentEpoch; reports.Clear(); subjects.Clear(); scheduled.Clear(); populationIdentity = null; nextPopulation = 0; lastTick = -1; SourceErrors = 0; }
            if (lastTick == now) return;
            lastTick = now;
            if (epoch <= 0) return;
            var population = source.Subjects;
            if (now >= nextPopulation || !ReferenceEquals(populationIdentity, population))
            {
                var seen = new HashSet<string>(StringComparer.Ordinal); OmittedSubjects = 0;
                populationIdentity = population;
                int examined = Math.Min(population.Count, PlanningPolicy.MaximumSubjects);
                OmittedSubjects = population.Count - examined;
                for (int i = 0; i < examined; i++)
                {
                    var subject = population[i];
                    if (subject == null || !seen.Add(subject.PawnId)) continue;
                    if (!subjects.TryGetValue(subject.PawnId, out var pending))
                    { pending = new Pending { Subject = subject, Due = now }; subjects.Add(subject.PawnId, pending); scheduled.Add(pending); }
                    else if (pending.Subject.MapId != subject.MapId)
                    { scheduled.Remove(pending); pending.Subject = subject; pending.Due = now; reports.Remove(subject.PawnId); scheduled.Add(pending); }
                }
                foreach (var key in new List<string>(subjects.Keys))
                    if (!seen.Contains(key)) { scheduled.Remove(subjects[key]); subjects.Remove(key); reports.Remove(key); }
                nextPopulation = (long)now + PlanningPolicy.PopulationRefreshTicks;
            }
            while (scheduled.Count > 0 && scheduled.Min.Due <= now && LastProcessed < PlanningPolicy.SubjectsPerTick)
            {
                var pending = scheduled.Min; scheduled.Remove(pending); var subject = pending.Subject;
                reports.TryGetValue(subject.PawnId, out var previous);
                LastProcessed++;
                try
                {
                    var latch = previous == null ? persistence?.ReadLatch(subject.PawnId, subject.MapId) : null;
                    var input = source.ReadInput(subject, now);
                    var built = latch == null ? ShadowPlanBuilder.Build(subject.PawnId, epoch, subject.MapId, now, input, previous)
                        : ShadowPlanBuilder.BuildSeeded(subject.PawnId, epoch, subject.MapId, now, input, previous, latch);
                    reports[subject.PawnId] = built;
                    persistence?.WriteLatch(built);
                    sink?.OnPlanPublished(built);
                }
                catch (Exception)
                {
                    SourceErrors = Math.Min(int.MaxValue - 1, SourceErrors) + 1;
                    // Replace stale successes with an explicit failure, not an old Ready decision.
                    reports[subject.PawnId] = new ShadowPlan(subject.PawnId, epoch, subject.MapId, now, 0,
                        ShadowDecision.Error, "PlanningSourceFailed", null,
                        new LifeBudget(BudgetState.Unknown, "PlanningSourceFailed", latch: previous?.Budget?.LastKnownRefill == true),
                        lastKnownActive: previous?.LastKnownActiveRequirements);
                }
                pending.Due = (long)now + PlanningPolicy.RefreshTicks; scheduled.Add(pending);
            }
        }
        public ShadowPlan Query(string id, int map, int now)
        {
            AssertOwner();
            if (id == null || now < lastTick || epoch != source.Epoch || !reports.TryGetValue(id, out var report)
                || report.MapId != map || now < report.BuiltTick) return null;
            // UI must also inspect BuiltTick; no collection, planning or writes from this method.
            return report;
        }
    }
}
