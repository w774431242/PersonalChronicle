using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Planning;
using Verse;

namespace PersonalChronicle.Data.PawnEconomy
{
    /// <summary>Additive save node for the planning hysteresis (M5/M3 decision memory). Nothing here is a
    /// balance, a contract or a payment credential; a save without this node simply replans from live
    /// facts, and entries are bounded so this can never grow without limit.</summary>
    internal sealed class ShadowPlanningSaveData : IExposable
    {
        public sealed class Entry : IExposable
        {
            public string PawnId;
            public int MapId;
            public bool RefillLatch;
            public List<string> ActiveRequirements = new List<string>();
            public int WrittenTick;
            public void ExposeData()
            {
                Scribe_Values.Look(ref PawnId, "pawn");
                Scribe_Values.Look(ref MapId, "map");
                Scribe_Values.Look(ref RefillLatch, "refillLatch");
                Scribe_Collections.Look(ref ActiveRequirements, "active", LookMode.Value);
                Scribe_Values.Look(ref WrittenTick, "writtenTick");
                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                {
                    if (ActiveRequirements == null) ActiveRequirements = new List<string>();
                    if (ActiveRequirements.Count > MaximumRequirements) ActiveRequirements.RemoveRange(MaximumRequirements, ActiveRequirements.Count - MaximumRequirements);
                }
            }
        }
        public const int MaximumEntries = 4096, MaximumRequirements = 64;
        private List<Entry> entries = new List<Entry>();
        private Dictionary<string, Entry> index;
        private Dictionary<string, Entry> Index
        {
            get
            {
                if (index != null) return index;
                index = new Dictionary<string, Entry>(StringComparer.Ordinal);
                for (int i = 0; i < entries.Count; i++)
                {
                    var row = entries[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.PawnId) || row.MapId <= 0) continue;
                    if (index.TryGetValue(row.PawnId, out var kept))
                    { if (kept.MapId == row.MapId && kept.WrittenTick >= row.WrittenTick) continue; index.Remove(row.PawnId); entries.Remove(kept); }
                    index[row.PawnId] = row;
                }
                return index;
            }
        }
        public void ExposeData()
        {
            Scribe_Collections.Look(ref entries, "planningLatches", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (entries == null) entries = new List<Entry>();
                index = null;
                var reached = Index;   // validates and de-duplicates in place
                if (reached.Count != entries.Count) entries.RemoveAll(row => row == null || !reached.ContainsValue(row));
                Prune();
            }
        }
        public int Count => Index.Count;
        public ShadowPlanLatch Read(string pawnId, int mapId)
        {
            if (pawnId == null || !Index.TryGetValue(pawnId, out var row) || row.MapId != mapId) return null;
            return new ShadowPlanLatch(row.PawnId, row.MapId, row.RefillLatch, row.ActiveRequirements);
        }
        /// <summary>Stores the latch of one built plan. Only a Ready budget is real evidence about the
        /// reserve water marks; waiting, unknown and error plans keep whatever was last proven, so a
        /// temporary blind spot never silently resets the hysteresis.</summary>
        public void Write(ShadowPlan plan)
        {
            if (plan == null || plan.PawnId == null || plan.MapId <= 0) return;
            if (plan.Budget == null || plan.Budget.State != BudgetState.Ready) return;
            bool refill = plan.Budget.LastKnownRefill;
            var active = plan.LastKnownActiveRequirements ?? (IReadOnlyList<string>)Array.Empty<string>();
            if (!Index.TryGetValue(plan.PawnId, out var row))
            {
                row = new Entry { PawnId = plan.PawnId, MapId = plan.MapId };
                entries.Add(row); index[plan.PawnId] = row;
            }
            if (row.MapId == plan.MapId && row.RefillLatch == refill && row.WrittenTick == plan.BuiltTick && SameActive(row.ActiveRequirements, active)) return;
            row.MapId = plan.MapId; row.RefillLatch = refill; row.WrittenTick = plan.BuiltTick;
            row.ActiveRequirements = new List<string>(active);
            if (row.ActiveRequirements.Count > MaximumRequirements) row.ActiveRequirements.RemoveRange(MaximumRequirements, row.ActiveRequirements.Count - MaximumRequirements);
            if (entries.Count > MaximumEntries) Prune();
        }
        private static bool SameActive(List<string> stored, IReadOnlyList<string> current)
        {
            if (stored == null || stored.Count != current.Count) return false;
            for (int i = 0; i < stored.Count; i++) if (!string.Equals(stored[i], current[i], StringComparison.Ordinal)) return false;
            return true;
        }
        private void Prune()
        {
            if (entries.Count <= MaximumEntries) return;
            entries.Sort((a, b) => b.WrittenTick.CompareTo(a.WrittenTick));
            entries.RemoveRange(MaximumEntries, entries.Count - MaximumEntries);
            index = null;
        }
    }
}
