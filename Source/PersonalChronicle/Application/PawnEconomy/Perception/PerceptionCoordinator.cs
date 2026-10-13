using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Application.PawnEconomy.Perception
{
    internal sealed partial class PerceptionCoordinator
    {
        private sealed class Entry
        {
            public PerceptionSubject Subject;
            public long FastDue, BodyDue, RelationshipDue;
            public PerceptionGroups Dirty;
            public long DirtyDue = long.MaxValue;
            public long Due => Math.Min(DirtyDue, Math.Min(FastDue, Math.Min(BodyDue, RelationshipDue)));
        }
        private sealed class EntryComparer : IComparer<Entry>
        {
            public int Compare(Entry x, Entry y)
            { int due = x.Due.CompareTo(y.Due); return due != 0 ? due : string.CompareOrdinal(x.Subject.PawnId, y.Subject.PawnId); }
        }
        private readonly IPawnPerceptionSource source;
        private readonly PerceptionSettings settings;
        private readonly Func<long> epochFactory;
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private readonly Dictionary<string, Entry> active = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Dictionary<string, PawnFactSnapshot> snapshots = new Dictionary<string, PawnFactSnapshot>(StringComparer.Ordinal);
        private readonly SortedSet<Entry> scheduled = new SortedSet<Entry>(new EntryComparer());
        private readonly LinkedList<string> retired = new LinkedList<string>();
        private readonly Dictionary<string, LinkedListNode<string>> retiredIndex = new Dictionary<string, LinkedListNode<string>>(StringComparer.Ordinal);
        private IReadOnlyList<PerceptionSubject> subjects = PerceptionCollections.Freeze<PerceptionSubject>(null);
        private int lastTick = -1, populationTick = -1, omitted;
        private long revision, batch, captures, errors, overruns;
        private bool populationComplete;
        private double lastCaptureMs, lastPopulationMs;
        public long SessionEpoch { get; private set; }
        public IReadOnlyList<PerceptionSubject> Subjects { get { AssertOwnerThread(); return subjects; } }

        public PerceptionCoordinator(IPawnPerceptionSource source, PerceptionSettings settings, Func<long> epochFactory)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.epochFactory = epochFactory ?? throw new ArgumentNullException(nameof(epochFactory));
            Reset();
        }
        public void Reset()
        {
            AssertOwnerThread();
            long next = epochFactory();
            if (next <= 0 || next == SessionEpoch) throw new InvalidOperationException("A new positive session epoch is required.");
            source.Reset(); active.Clear(); snapshots.Clear(); scheduled.Clear(); retired.Clear(); retiredIndex.Clear();
            subjects = PerceptionCollections.Freeze<PerceptionSubject>(null);
            SessionEpoch = next; lastTick = -1; populationTick = -1; populationComplete = false;
            revision = batch = captures = errors = overruns = 0; omitted = 0; lastCaptureMs = lastPopulationMs = 0;
        }
        public void Tick(int now)
        {
            AssertOwnerThread();
            if (now < 0) return;
            if (now < lastTick) Reset();
            if (now == lastTick) return;
            lastTick = now; batch++;
            var timer = Stopwatch.StartNew();
            try
            {
                var population = source.PollPopulation(now);
                if (population != null) ApplyPopulation(population, now);
            }
            catch (Exception) { errors++; populationComplete = false; }
            lastPopulationMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart();
            int processed = 0;
            while (scheduled.Count != 0 && scheduled.Min.Due <= now && processed < settings.MaximumCapturesPerTick)
            {
                if (processed != 0 && timer.Elapsed.TotalMilliseconds >= settings.CaptureBudgetMilliseconds) break;
                var entry = scheduled.Min; scheduled.Remove(entry);
                Capture(entry, now); processed++;
            }
            lastCaptureMs = timer.Elapsed.TotalMilliseconds;
            if (lastCaptureMs > settings.CaptureBudgetMilliseconds) overruns++;
        }
        public void MarkDirty(string pawnId, PerceptionGroups groups)
        {
            AssertOwnerThread();
            if ((groups & ~PerceptionGroups.All) != 0) throw new ArgumentOutOfRangeException(nameof(groups));
            if (pawnId == null || !active.TryGetValue(pawnId, out var entry)) return;
            scheduled.Remove(entry); entry.Dirty |= groups;
            if (groups != PerceptionGroups.None) entry.DirtyDue = Math.Min(entry.DirtyDue, Math.Max(0, lastTick));
            scheduled.Add(entry);
        }
        internal void AssertOwnerThread()
        { if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Perception coordination and queries are owner-thread only."); }
        private void ApplyPopulation(PopulationUpdate update, int now)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            omitted = update.OmittedCount;
            foreach (var subject in update.Subjects) if (subject != null) seen.Add(subject.PawnId);
            if (update.Complete && omitted == 0)
            {
                var missing = new List<Entry>();
                foreach (var entry in active.Values) if (!seen.Contains(entry.Subject.PawnId)) missing.Add(entry);
                foreach (var entry in missing)
                {
                    // Authoritative census carries scalar terminal facts; never add unbudgeted per-Pawn captures here.
                    var location = update.Departures.TryGetValue(entry.Subject.PawnId, out var terminal)
                        && terminal == SubjectLocation.Dead ? SubjectLocation.Dead : SubjectLocation.OffMap;
                    long nextRevision = ++revision;
                    var context = new FactGroup<PawnContextFact>(CaptureState.Complete,
                        new PawnContextFact(location, entry.Subject.MapId), now, nextRevision, "LeftObservationScope");
                    Publish(entry, now, nextRevision, new PawnCaptureResult(context)); Retire(entry);
                }
            }
            seen.Clear();
            foreach (var subject in update.Subjects)
            {
                if (subject == null || !seen.Add(subject.PawnId)) continue;
                if (active.TryGetValue(subject.PawnId, out var existing))
                {
                    if (existing.Subject.MapId != subject.MapId)
                    { existing.Subject = subject; MarkDirty(subject.PawnId, PerceptionGroups.All); }
                    continue;
                }
                if (active.Count >= settings.MaximumSubjects) { omitted++; continue; }
                Unretire(subject.PawnId);
                var entry = new Entry { Subject = subject, FastDue = now, BodyDue = now, RelationshipDue = now };
                active.Add(subject.PawnId, entry); scheduled.Add(entry);
                snapshots.Remove(subject.PawnId); // Re-entry must not reuse stale conditions from the previous scope.
            }
            populationComplete = update.Complete && omitted == 0; populationTick = now; PublishSubjects();
        }
        private void Capture(Entry entry, int now)
        {
            PerceptionGroups groups = entry.Dirty | PerceptionGroups.Context;
            if (entry.FastDue <= now) groups |= PerceptionGroups.Needs;
            if (entry.BodyDue <= now) groups |= PerceptionGroups.Body;
            if (entry.RelationshipDue <= now) groups |= PerceptionGroups.Relationships;
            long nextRevision = ++revision;
            PawnCaptureResult result;
            try
            {
                result = source.Capture(new PawnCaptureRequest(entry.Subject.PawnId, SessionEpoch, nextRevision, now, groups));
                if (result == null) throw new InvalidOperationException("Missing capture result.");
                var capturedLocation = result.Context?.Data?.Location ?? SubjectLocation.Unknown;
                if (capturedLocation != SubjectLocation.Dead && capturedLocation != SubjectLocation.OffMap)
                    result = new PawnCaptureResult(result.Context ?? Error<PawnContextFact>(now, nextRevision),
                        (groups & PerceptionGroups.Needs) != 0 ? result.Needs ?? Error<NeedFactSet>(now, nextRevision) : null,
                        (groups & PerceptionGroups.Body) != 0 ? result.Body ?? Error<BodyLifeFactSet>(now, nextRevision) : null,
                        (groups & PerceptionGroups.Relationships) != 0 ? result.Relationships ?? Error<RelationshipFactSet>(now, nextRevision) : null);
            }
            catch (Exception)
            {
                errors++;
                result = new PawnCaptureResult(Error<PawnContextFact>(now, nextRevision),
                    (groups & PerceptionGroups.Needs) != 0 ? Error<NeedFactSet>(now, nextRevision) : null,
                    (groups & PerceptionGroups.Body) != 0 ? Error<BodyLifeFactSet>(now, nextRevision) : null,
                    (groups & PerceptionGroups.Relationships) != 0 ? Error<RelationshipFactSet>(now, nextRevision) : null);
            }
            Publish(entry, now, nextRevision, result); captures++;
            var capturedContext = snapshots[entry.Subject.PawnId].Context.Data;
            var location = capturedContext?.Location ?? SubjectLocation.Unknown;
            if (location == SubjectLocation.Dead || location == SubjectLocation.OffMap)
            { Retire(entry); PublishSubjects(); return; }
            if (capturedContext != null && (location == SubjectLocation.SpawnedMap || location == SubjectLocation.HeldOnMap)
                && capturedContext.MapId != entry.Subject.MapId)
            { entry.Subject = new PerceptionSubject(entry.Subject.PawnId, capturedContext.MapId); PublishSubjects(); }
            if ((groups & PerceptionGroups.Needs) != 0) entry.FastDue = NextDue(now, settings.FastInterval, entry.Subject.PawnId);
            if ((groups & PerceptionGroups.Body) != 0) entry.BodyDue = NextDue(now, settings.BodyInterval, entry.Subject.PawnId);
            if ((groups & PerceptionGroups.Relationships) != 0) entry.RelationshipDue = NextDue(now, settings.RelationshipInterval, entry.Subject.PawnId);
            entry.Dirty = PerceptionGroups.None; entry.DirtyDue = long.MaxValue; scheduled.Add(entry);
        }
        private void Publish(Entry entry, int now, long nextRevision, PawnCaptureResult result)
        {
            snapshots.TryGetValue(entry.Subject.PawnId, out var old);
            snapshots[entry.Subject.PawnId] = new PawnFactSnapshot(entry.Subject.PawnId, SessionEpoch, nextRevision, batch,
                Validate(result.Context, old?.Context, now, nextRevision), Validate(result.Needs, old?.Needs, now, nextRevision),
                Validate(result.Body, old?.Body, now, nextRevision), Validate(result.Relationships, old?.Relationships, now, nextRevision));
        }
        private static FactGroup<T> Validate<T>(FactGroup<T> value, FactGroup<T> old, int now, long revision) where T : class
        {
            if (value == null) return old ?? FactGroup<T>.Unknown();
            return value.CapturedTick == now && value.Revision == revision ? value
                : new FactGroup<T>(CaptureState.Error, null, now, revision, "InvalidSourceStamp");
        }
        private static FactGroup<T> Error<T>(int now, long revision) where T : class
            => new FactGroup<T>(CaptureState.Error, null, now, revision, "CaptureFailed");
        private static long NextDue(int now, int interval, string id)
        {
            uint hash = 2166136261;
            foreach (char character in id) hash = unchecked((hash ^ character) * 16777619);
            return (long)now + interval - (((long)now + hash % (uint)interval) % interval);
        }
        private void Retire(Entry entry)
        {
            scheduled.Remove(entry); active.Remove(entry.Subject.PawnId); source.Release(entry.Subject.PawnId);
            Unretire(entry.Subject.PawnId);
            retiredIndex[entry.Subject.PawnId] = retired.AddLast(entry.Subject.PawnId);
            while (retired.Count > settings.MaximumRetired)
            { string id = retired.First.Value; retired.RemoveFirst(); retiredIndex.Remove(id); snapshots.Remove(id); }
        }
        private void Unretire(string id)
        { if (retiredIndex.TryGetValue(id, out var node)) { retired.Remove(node); retiredIndex.Remove(id); } }
        private void PublishSubjects()
        {
            var list = new List<PerceptionSubject>(active.Count);
            foreach (var entry in active.Values) list.Add(entry.Subject);
            list.Sort((a, b) => string.CompareOrdinal(a.PawnId, b.PawnId)); subjects = list.AsReadOnly();
        }
        public PerceptionDiagnostics Diagnostics
        {
            get
            {
                AssertOwnerThread();
                return new PerceptionDiagnostics(active.Count, retired.Count, captures,
                    errors, overruns, lastCaptureMs, lastPopulationMs,
                    scheduled.Count == 0 ? 0 : Math.Max(0, (long)lastTick - Math.Max(0, scheduled.Min.Due)),
                    populationTick, populationComplete, omitted);
            }
        }
    }
}
