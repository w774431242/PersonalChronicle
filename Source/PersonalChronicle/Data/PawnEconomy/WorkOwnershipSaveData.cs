using System;
using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Data.PawnEconomy
{
    /// <summary>Additive save node for the economy employment work-priority ledger (EXC-2026-028).
    /// Records what this system enabled and the value it replaced, so a release can restore exactly
    /// our own writes and keeps every player edit. A save without this node has no ownership at all.</summary>
    internal sealed class WorkOwnershipSaveData : IExposable
    {
        public sealed class Entry : IExposable
        {
            public string PawnKey;
            public string WorkTypeDefName;
            public int WrittenPriority;
            public int OriginalPriority;
            public int AppliedTick;
            public void ExposeData()
            {
                Scribe_Values.Look(ref PawnKey, "pawn");
                Scribe_Values.Look(ref WorkTypeDefName, "workType");
                Scribe_Values.Look(ref WrittenPriority, "written");
                Scribe_Values.Look(ref OriginalPriority, "original");
                Scribe_Values.Look(ref AppliedTick, "tick");
            }
        }
        public const int MaximumEntries = 4096;
        private List<Entry> entries = new List<Entry>();
        private Dictionary<string, Entry> index;
        private static string Key(string pawnKey, string workTypeDefName) => pawnKey + "|" + workTypeDefName;
        private Dictionary<string, Entry> Index
        {
            get
            {
                if (index != null) return index;
                index = new Dictionary<string, Entry>(StringComparer.Ordinal);
                for (int i = 0; i < entries.Count; i++)
                {
                    var row = entries[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.PawnKey) || string.IsNullOrWhiteSpace(row.WorkTypeDefName)) continue;
                    var key = Key(row.PawnKey, row.WorkTypeDefName);
                    if (index.ContainsKey(key)) { entries.RemoveAt(i); i--; continue; }   // first record wins
                    index[key] = row;
                }
                return index;
            }
        }
        public void ExposeData()
        {
            Scribe_Collections.Look(ref entries, "ownershipEntries", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (entries == null) entries = new List<Entry>();
                index = null;
                var reached = Index;
                if (reached.Count != entries.Count) entries.RemoveAll(row => row == null || !reached.ContainsValue(row));
                Prune();
            }
        }
        public int Count => Index.Count;
        public Entry Read(string pawnKey, string workTypeDefName)
            => pawnKey == null || workTypeDefName == null || !Index.TryGetValue(Key(pawnKey, workTypeDefName), out var row) ? null : row;
        public IReadOnlyList<Entry> ReadFor(string pawnKey)
        {
            var rows = new List<Entry>();
            if (pawnKey == null) return rows.AsReadOnly();
            foreach (var row in Index.Values) if (string.Equals(row.PawnKey, pawnKey, StringComparison.Ordinal)) rows.Add(row);
            return rows.AsReadOnly();
        }
        /// <summary>First record wins: a later record never rewrites the original value we must restore.</summary>
        public bool Record(string pawnKey, string workTypeDefName, int writtenPriority, int originalPriority, int appliedTick)
        {
            if (string.IsNullOrWhiteSpace(pawnKey) || string.IsNullOrWhiteSpace(workTypeDefName)) return false;
            if (Index.ContainsKey(Key(pawnKey, workTypeDefName))) return false;
            var row = new Entry { PawnKey = pawnKey, WorkTypeDefName = workTypeDefName, WrittenPriority = writtenPriority,
                OriginalPriority = originalPriority, AppliedTick = appliedTick };
            entries.Add(row); index[Key(pawnKey, workTypeDefName)] = row;
            if (entries.Count > MaximumEntries) Prune();
            return true;
        }
        public void Discard(string pawnKey, string workTypeDefName)
        {
            if (pawnKey == null || workTypeDefName == null) return;
            var key = Key(pawnKey, workTypeDefName);
            if (!Index.TryGetValue(key, out var row)) return;
            index.Remove(key); entries.Remove(row);
        }
        public int DiscardPawn(string pawnKey)
        {
            var owned = ReadFor(pawnKey);
            for (int i = 0; i < owned.Count; i++) Discard(owned[i].PawnKey, owned[i].WorkTypeDefName);
            return owned.Count;
        }
        private void Prune()
        {
            if (entries.Count <= MaximumEntries) return;
            entries.Sort((a, b) => b.AppliedTick.CompareTo(a.AppliedTick));
            entries.RemoveRange(MaximumEntries, entries.Count - MaximumEntries);
            index = null;
        }
    }
}
