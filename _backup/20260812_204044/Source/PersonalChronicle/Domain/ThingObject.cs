using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Archived thing. <see cref="ThingDefName"/> is the data key for labels;
    /// <see cref="WeakId"/> (defName:thingIDNumber) is only valid within a loaded
    /// session and is kept as a historical snapshot — never a live reference.
    /// CreatedTick/DestroyedTick are intentionally NOT persisted: the timeline is
    /// derived from the eventsByObject index (single source of truth, P2).
    /// </summary>
    public sealed class ThingObject : ArchiveObject
    {
        public string ThingDefName;

        /// <summary>defName:thingIDNumber — memory-only historical snapshot.</summary>
        public string WeakId;

        /// <summary>Derived from the event timeline; not saved.</summary>
        [Unsaved]
        public long CreatedTick = -1L;

        /// <summary>Derived from the event timeline; not saved.</summary>
        [Unsaved]
        public long DestroyedTick = -1L;

        /// <summary>Runtime current holder (stable id of the pawn/thing holding it); not saved.</summary>
        [Unsaved]
        public string CurrentHolderId;

        /// <summary>Successive holders over time (optional, reserved for Phase B); persisted.</summary>
        public List<ObjectRef> HolderHistory = new List<ObjectRef>();

        /// <summary>
        /// Legacy chain (传承): holder records with ownership kind / start tick,
        /// persisted. Added in v4.7 while <see cref="HolderHistory"/> stays for
        /// binary/save compatibility (old saves keep both empty until new captures).
        /// Read Model derives per-holder kill counts from the event index.
        /// </summary>
        public List<HolderRecord> HolderRecords = new List<HolderRecord>();

        /// <summary>
        /// v4.9: 退役仪式 (decommission) — the thing's death record, captured
        /// read-only at destroy time. Null for in-service equipment. Persisted.
        /// </summary>
        public DecommissionRecord Decommission;

        public override string CategoryKey
        {
            get { return ArchiveCategoryKeys.Thing; }
        }

        public ThingObject()
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ThingDefName, "thingDefName");
            Scribe_Values.Look(ref WeakId, "weakId");
            Scribe_Collections.Look(ref HolderHistory, "holderHistory", LookMode.Deep);
            if (HolderHistory == null) HolderHistory = new List<ObjectRef>();
            Scribe_Collections.Look(ref HolderRecords, "holderRecords", LookMode.Deep);
            if (HolderRecords == null) HolderRecords = new List<HolderRecord>();
            Scribe_Deep.Look(ref Decommission, "decommission");
            // CreatedTick/DestroyedTick/CurrentHolderId deliberately not looked:
            // timeline is derived from eventsByObject to avoid a second source
            // of truth; CurrentHolderId is a live runtime cache only.
        }
    }
}
