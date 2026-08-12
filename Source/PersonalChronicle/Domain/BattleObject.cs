using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Archived battle. <see cref="IncidentDefName"/> records the source Incident
    /// as a data key (not a business judgment).
    ///
    /// Three-element capture (v4.11, P0): the raid's trigger time, force size and
    /// repulse time are archived once the whole process completes — no polling, no
    /// real-time sampling. <see cref="StartTick"/> is snapshotted when the incident
    /// fires; <see cref="RaidCount"/> is the number of enemy pawns the raid Lord(s)
    /// spawned; <see cref="EndTick"/> is written when the last raid pawn leaves the
    /// map (death / capture / exit) via Lord.Notify_PawnLost.
    /// </summary>
    public sealed class BattleObject : ArchiveObject
    {
        /// <summary>Source Incident defName (data key, never translated).</summary>
        public string IncidentDefName;

        /// <summary>
        /// v4.14: IncidentDef.category snapshot — "ThreatBig"/"ThreatSmall"/null.
        /// Data key only; the UI resolves the label and accent via translation +
        /// UITheme (ThreatBig=Accent / ThreatSmall=Tag). Null when the incident
        /// carried no threat category (e.g. custom IncidentBattleExtension-only
        /// battles). Persisted so Def drift never changes history.
        /// </summary>
        public string ThreatKey;

        /// <summary>Game tick the raid incident fired (persisted). -1 = unknown.</summary>
        public long StartTick = -1L;

        /// <summary>Game tick the last raid pawn left the map (persisted). -1 = still ongoing / closed by fallback.</summary>
        public long EndTick = -1L;

        /// <summary>Number of enemy pawns the raid spawned (persisted). -1 = not captured (e.g. non-Lord threat).</summary>
        public int RaidCount = -1;

        /// <summary>
        /// Runtime-only countdown of raid pawns still on the map, linked to this
        /// battle through the raid Lord(s). Initialized from <see cref="RaidCount"/>
        /// at creation / load; decremented by Lord.Notify_PawnLost. Never persisted
        /// (recomputed from RaidCount after a save/load).
        /// </summary>
        [Unsaved]
        public int RemainingRaidCount = -1;

        /// <summary>Participating pawn stable ids (optional, for Combat lookups); persisted.</summary>
        public List<string> ParticipantIds = new List<string>();

        public override string CategoryKey
        {
            get { return ArchiveCategoryKeys.Battle; }
        }

        public BattleObject()
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref IncidentDefName, "incidentDefName");
            Scribe_Values.Look(ref ThreatKey, "threatKey");
            Scribe_Collections.Look(ref ParticipantIds, "participantIds", LookMode.Value);
            if (ParticipantIds == null) ParticipantIds = new List<string>();
            Scribe_Values.Look(ref StartTick, "startTick", -1L);
            Scribe_Values.Look(ref EndTick, "endTick", -1L);
            Scribe_Values.Look(ref RaidCount, "raidCount", -1);
            // RemainingRaidCount is [Unsaved]: reconstructed from RaidCount after load.
        }
    }
}
