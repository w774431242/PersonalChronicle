using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// A single chronicle moment. TypeKey references a ChronicleEventDef
    /// (data-driven rendering); Params holds typed data keys consumed by the
    /// def template (supplementary only — never used for associations).
    /// v2.1 associations live in Primary/Subjects (ObjectRef edges).
    /// </summary>
    public class ChronicleEvent : IExposable
    {
        public long Id;
        public long Tick;
        public string TypeKey;

        /// <summary>
        /// v4.1: stable identity of the reporting source mod (e.g.
        /// "PersonalChronicle.Capture" or a third-party mod id). Used for
        /// attribution / filtering; never user-visible. New field — Scribe keeps
        /// it null-safe for old saves.
        /// </summary>
        public string SourceId;

        /// <summary>
        /// Persisted timeline importance. -1 means auto-resolve for old saves
        /// or events created before the importance policy was introduced.
        /// </summary>
        public int ImportanceLevel = -1;

        /// <summary>
        /// Legacy v0.2 field — kept permanently for save compatibility.
        /// Always read so old saves load intact. NOT a source of truth in v2.1:
        /// Primary/Subjects are. On save it is re-synced as a shadow of Primary
        /// (when the primary is a Pawn) so that downgrading to v0.2 does not
        /// lose pawn linkage.
        /// </summary>
        public string PawnStableId;

        public Dictionary<string, string> Params = new Dictionary<string, string>();

        /// <summary>Main object reference (replaces v0.2 PawnStableId semantics).</summary>
        public ObjectRef Primary;

        /// <summary>Linked object references (association edges).</summary>
        public List<ObjectRef> Subjects = new List<ObjectRef>();

        public ChronicleEvent()
        {
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id", 0L);
            Scribe_Values.Look(ref Tick, "tick", 0L);
            Scribe_Values.Look(ref TypeKey, "typeKey");
            Scribe_Values.Look(ref ImportanceLevel, "importanceLevel", -1);
            Scribe_Values.Look(ref SourceId, "sourceId");

            // v0.2 downgrade shadow: keep the legacy field in sync from the
            // v2.1 source of truth whenever we save a Pawn-primary event.
            if (Scribe.mode == LoadSaveMode.Saving
                && Primary != null
                && Primary.CategoryKey == ArchiveCategoryKeys.Pawn
                && !string.IsNullOrEmpty(Primary.StableId))
            {
                PawnStableId = Primary.StableId;
            }
            Scribe_Values.Look(ref PawnStableId, "pawnStableId");

            Scribe_Collections.Look(ref Params, "params", LookMode.Value, LookMode.Value);
            Scribe_Deep.Look(ref Primary, "primary");
            Scribe_Collections.Look(ref Subjects, "subjects", LookMode.Deep);

            if (Params == null)
            {
                Params = new Dictionary<string, string>();
            }
            if (Subjects == null)
            {
                Subjects = new List<ObjectRef>();
            }
        }
    }
}
