using System;
using System.Collections.Generic;

namespace PersonalChronicle.Domain.PawnEconomy.Perception
{
    /// <summary>Presentation-only queue. Business identity is supplied by the committed event,
    /// never derived from a frame/tick. Only time when a pawn could be shown consumes its budget.</summary>
    public sealed class LaborSpeechQueue
    {
        public sealed class Entry
        {
            public string PawnId, EventId, Key;
            public object Payload;
            public object[] Args;
            public int Priority;
            public double VisibleWait;
            internal bool WasEligible;
        }
        private readonly Dictionary<string, List<Entry>> pending = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> recent = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly List<string> recentOrder = new List<string>();
        private readonly int pawnLimit, perPawnLimit, recentLimit;
        private readonly double visibleLifetime;
        private double previousTime = double.NaN;
        public long DuplicateCount { get; private set; }
        public long OverflowCount { get; private set; }
        public long ExpiredCount { get; private set; }
        public long DiscardedCount { get; private set; }
        public int PawnCount => pending.Count;
        public LaborSpeechQueue(int pawnLimit = 64, int perPawnLimit = 8, int recentLimit = 2048, double visibleLifetime = 90)
        {
            if (pawnLimit < 1 || perPawnLimit < 1 || recentLimit < 1 || visibleLifetime <= 0
                || double.IsNaN(visibleLifetime) || double.IsInfinity(visibleLifetime)) throw new ArgumentOutOfRangeException();
            this.pawnLimit = pawnLimit; this.perPawnLimit = perPawnLimit;
            this.recentLimit = recentLimit; this.visibleLifetime = visibleLifetime;
        }
        public bool Enqueue(string pawnId, string eventId, string key, object payload, object[] args, int priority)
        {
            if (string.IsNullOrEmpty(pawnId) || string.IsNullOrEmpty(eventId) || string.IsNullOrEmpty(key)) return false;
            string identity = pawnId + "|" + eventId;
            if (recent.ContainsKey(identity)) { DuplicateCount++; return false; }
            if (!pending.TryGetValue(pawnId, out var queue))
            {
                if (pending.Count >= pawnLimit)
                {
                    string victimPawn = null;
                    int victimPriority = priority;
                    foreach (var pair in pending)
                    {
                        int highest = 0;
                        foreach (var old in pair.Value) highest = Math.Max(highest, old.Priority);
                        if (highest >= victimPriority) continue;
                        victimPawn = pair.Key; victimPriority = highest;
                    }
                    if (victimPawn == null) { OverflowCount++; return false; }
                    OverflowCount += pending[victimPawn].Count;
                    pending.Remove(victimPawn);
                }
                pending[pawnId] = queue = new List<Entry>();
            }
            // A pending identity must remain deduplicated even after leaving the bounded recent ring.
            foreach (var entry in queue)
                if (entry.EventId == eventId) { DuplicateCount++; return false; }
            if (queue.Count >= perPawnLimit)
            {
                int victim = 0;
                for (int i = 1; i < queue.Count; i++) if (queue[i].Priority < queue[victim].Priority) victim = i;
                OverflowCount++;
                if (queue[victim].Priority > priority) return false;
                queue.RemoveAt(victim);
            }
            queue.Add(new Entry { PawnId = pawnId, EventId = eventId, Key = key, Payload = payload,
                Args = args == null ? null : (object[])args.Clone(), Priority = priority });
            recent[identity] = true; recentOrder.Add(identity);
            if (recentOrder.Count > recentLimit) { recent.Remove(recentOrder[0]); recentOrder.RemoveAt(0); }
            return true;
        }
        public void Observe(double realtime, Func<Entry, bool> eligible)
        {
            if (double.IsNaN(realtime) || double.IsInfinity(realtime)) return;
            double elapsed = double.IsNaN(previousTime) || realtime < previousTime ? 0 : realtime - previousTime;
            previousTime = realtime;
            if (pending.Count == 0) return;
            var empty = new List<string>();
            foreach (var pair in pending)
            {
                var queue = pair.Value;
                bool visible = queue.Count > 0 && eligible(queue[0]);
                for (int i = queue.Count - 1; i >= 0; i--)
                {
                    if (visible && queue[i].WasEligible) queue[i].VisibleWait += elapsed;
                    queue[i].WasEligible = visible;
                    if (queue[i].VisibleWait >= visibleLifetime) { queue.RemoveAt(i); ExpiredCount++; }
                }
                if (queue.Count == 0) empty.Add(pair.Key);
            }
            foreach (string id in empty) pending.Remove(id);
        }
        public Entry Peek(Func<Entry, bool> eligible)
        {
            foreach (var pair in pending)
                if (pair.Value.Count > 0 && eligible(pair.Value[0])) return pair.Value[0];
            return null;
        }
        /// <summary>Remove events whose subject can never return. Eligibility (sleep, visibility,
        /// another map, caravan) is deliberately a separate concern and must not be used here.</summary>
        public void DiscardWhere(Func<Entry, bool> permanentlyInvalid)
        {
            if (pending.Count == 0) return;
            var empty = new List<string>();
            foreach (var pair in pending)
            {
                var queue = pair.Value;
                for (int i = queue.Count - 1; i >= 0; i--)
                    if (permanentlyInvalid(queue[i])) { queue.RemoveAt(i); DiscardedCount++; }
                if (queue.Count == 0) empty.Add(pair.Key);
            }
            foreach (string id in empty) pending.Remove(id);
        }
        public bool Consume(Entry entry)
        {
            if (entry == null || !pending.TryGetValue(entry.PawnId, out var queue) || queue.Count == 0 || queue[0] != entry) return false;
            queue.RemoveAt(0);
            if (queue.Count == 0) pending.Remove(entry.PawnId);
            return true;
        }
        public void Clear()
        {
            pending.Clear(); recent.Clear(); recentOrder.Clear(); previousTime = double.NaN;
            DuplicateCount = 0; OverflowCount = 0; ExpiredCount = 0; DiscardedCount = 0;
        }
    }
}
