using System;
using System.Collections.Generic;

namespace PersonalChronicle.Domain.PawnEconomy.Learning
{
    /// <summary>M9 bounded ledger of experience summaries, one row per institution key. The key set is
    /// capped: a full ledger refuses a new key instead of trimming the samples of existing keys, so
    /// learning history can never evict a row that another reading still relies on. Nothing here is a
    /// payment credential.</summary>
    internal sealed class ExperienceLedger
    {
        public const int MaximumKeys = 32;
        private readonly Dictionary<string, ExperienceSummary> rows = new Dictionary<string, ExperienceSummary>(StringComparer.Ordinal);

        public int Count => rows.Count;

        /// <summary>Stable, sorted key list for diagnostics and tests.</summary>
        public IReadOnlyList<string> Keys
        {
            get
            {
                var keys = new List<string>(rows.Keys);
                keys.Sort(StringComparer.Ordinal);
                return keys.AsReadOnly();
            }
        }

        public ExperienceSummary Read(string key)
            => string.IsNullOrWhiteSpace(key) ? null : (rows.TryGetValue(key, out var row) ? row : null);

        /// <summary>Folds one real outcome in. Returns false when the key is blank, the delay is
        /// negative, or the ledger is full and the key is new — a refusal, never a silent trim.</summary>
        public bool Record(string key, bool success, long delayTicks, int now)
        {
            if (string.IsNullOrWhiteSpace(key) || delayTicks < 0) return false;
            if (!rows.TryGetValue(key, out var summary))
            {
                if (rows.Count >= MaximumKeys) return false;
                summary = new ExperienceSummary(key);
            }
            rows[key] = summary.Record(success, delayTicks, now);
            return true;
        }
    }
}
