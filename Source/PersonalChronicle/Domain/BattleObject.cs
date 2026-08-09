using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Archived battle. <see cref="IncidentDefName"/> records the source Incident
    /// as a data key (not a business judgment). StartTick/EndTick are NOT
    /// persisted: the timeline is derived from the eventsByObject index
    /// (single source of truth, P2).
    /// </summary>
    public sealed class BattleObject : ArchiveObject
    {
        /// <summary>Source Incident defName (data key, never translated).</summary>
        public string IncidentDefName;

        /// <summary>Derived from the event timeline; not saved.</summary>
        [Unsaved]
        public long StartTick = -1L;

        /// <summary>Derived from the event timeline; not saved.</summary>
        [Unsaved]
        public long EndTick = -1L;

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
            Scribe_Collections.Look(ref ParticipantIds, "participantIds", LookMode.Value);
            if (ParticipantIds == null) ParticipantIds = new List<string>();
            // StartTick/EndTick deliberately not looked: timeline is derived
            // from eventsByObject to avoid a second source of truth.
        }
    }
}
