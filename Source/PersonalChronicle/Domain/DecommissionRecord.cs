using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// v4.9: 退役仪式 (decommission) — the "death record" of an equipment thing,
    /// mirroring <see cref="PawnRecord"/>'s death fields for pawns. Captured
    /// read-only at destroy time (never prevents the destroy). Only stable ids and
    /// label snapshots are saved — no user-visible prose, no live references.
    /// Language-independent by design; the Read Model localizes for display.
    /// </summary>
    public sealed class DecommissionRecord : IExposable
    {
        /// <summary>Game tick of the destroy; &lt;= 0 means unknown.</summary>
        public long Tick = -1L;

        /// <summary>Stable id of the last holder pawn (may be null).</summary>
        public string LastHolderStableId;

        /// <summary>Identity snapshot of the last holder at destroy time.</summary>
        public string LastHolderLabel;

        /// <summary>
        /// Biome defName (language-independent) of the place where the thing was
        /// destroyed, or "—" when off-map. The Read Model resolves this to a
        /// localized label for display. Older saves may hold a pre-v4.9.1 localized
        /// label here; BiomeLabel falls back to showing the raw string, so both are
        /// display-safe.
        /// </summary>
        public string LastPlaceLabel;

        /// <summary>Total service days (derived from tenure span at capture).</summary>
        public int ServiceDays;

        /// <summary>Label of the final battle the thing saw (may be null).</summary>
        public string LastBattleLabel;

        public bool IsEmpty
        {
            get { return Tick <= 0L; }
        }

        public DecommissionRecord()
        {
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Tick, "tick", -1L);
            Scribe_Values.Look(ref LastHolderStableId, "lastHolderStableId");
            Scribe_Values.Look(ref LastHolderLabel, "lastHolderLabel");
            Scribe_Values.Look(ref LastPlaceLabel, "lastPlaceLabel");
            Scribe_Values.Look(ref ServiceDays, "serviceDays", 0);
            Scribe_Values.Look(ref LastBattleLabel, "lastBattleLabel");
        }
    }
}
