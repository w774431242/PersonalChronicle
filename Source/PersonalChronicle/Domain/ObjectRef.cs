using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// A stable, language-independent reference to an archived object. Used as
    /// the primary object and the subject (association) edges of a
    /// <see cref="ChronicleEvent"/>. Only strings are persisted — never live
    /// references, never user-visible prose (except the identity snapshot).
    /// </summary>
    public class ObjectRef : IExposable
    {
        /// <summary>Category discriminator: "Pawn" / "Thing" / "Battle" / "Location".</summary>
        public string CategoryKey;

        /// <summary>Stable identity, e.g. Pawn.GetUniqueLoadID() for pawns.</summary>
        public string StableId;

        /// <summary>
        /// Identity snapshot for display. For pawns this is the name; for things
        /// the UI falls back to ThingDef label via ThingDefName at render time.
        /// May be null for legacy events migrated from v0.2 (no snapshot existed).
        /// </summary>
        public string LabelSnapshot;

        public ObjectRef()
        {
        }

        public ObjectRef(string categoryKey, string stableId, string labelSnapshot)
        {
            CategoryKey = categoryKey;
            StableId = stableId;
            LabelSnapshot = labelSnapshot;
        }

        public static ObjectRef ForPawn(string stableId, string labelSnapshot)
        {
            return new ObjectRef(ArchiveCategoryKeys.Pawn, stableId, labelSnapshot);
        }

        /// <summary>
        /// v4.13: location edge factory. <paramref name="mapId"/> is the stable
        /// Map.uniqueID string; <paramref name="cellLabel"/> the identity snapshot.
        /// Used by event capture to hang "this happened at map X" onto event subjects.
        /// </summary>
        public static ObjectRef ForLocation(string mapId, string cellLabel)
        {
            return new ObjectRef(ArchiveCategoryKeys.Location, mapId, cellLabel);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref CategoryKey, "categoryKey");
            Scribe_Values.Look(ref StableId, "stableId");
            Scribe_Values.Look(ref LabelSnapshot, "labelSnapshot");
        }
    }
}
