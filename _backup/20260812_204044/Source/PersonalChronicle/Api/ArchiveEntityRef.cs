namespace PersonalChronicle.Api
{
    /// <summary>
    /// A language-independent reference to an entity involved in an archive event.
    /// API-layer counterpart of <see cref="PersonalChronicle.Domain.ObjectRef"/>;
    /// the sink converts this to a Domain <see cref="PersonalChronicle.Domain.ObjectRef"/>
    /// when persisting, so external mods never touch Domain persistence types.
    ///
    /// <see cref="SourceId"/> is the registering mod's stable identity (e.g.
    /// "MyMod.Combat"); it scopes external entities that have no RimWorld
    /// stable id of their own. For RimWorld pawns/things leave it null and use
    /// the native stable id.
    /// </summary>
    public sealed class ArchiveEntityRef
    {
        /// <summary>Category key: "Pawn" / "Thing" / "Battle" / "Location".</summary>
        public string CategoryKey;

        /// <summary>Stable identity (Pawn.GetUniqueLoadID(), "defName:thingIDNumber", ...).</summary>
        public string StableId;

        /// <summary>Optional display snapshot (name). May be null.</summary>
        public string LabelSnapshot;

        /// <summary>
        /// Origin mod of this reference. Null for native RimWorld entities; set
        /// for external entities keyed by SourceId + LocalId.
        /// </summary>
        public string SourceId;

        public ArchiveEntityRef() { }

        public ArchiveEntityRef(string categoryKey, string stableId, string labelSnapshot = null, string sourceId = null)
        {
            CategoryKey = categoryKey;
            StableId = stableId;
            LabelSnapshot = labelSnapshot;
            SourceId = sourceId;
        }

        /// <summary>
        /// True when this ref carries enough identity to be persisted (a category
        /// and a stable id, either native or source-scoped).
        /// </summary>
        public bool IsValid
        {
            get
            {
                return !string.IsNullOrEmpty(CategoryKey)
                    && !string.IsNullOrEmpty(StableId);
            }
        }

        /// <summary>Converts to the Domain persistence type.</summary>
        public PersonalChronicle.Domain.ObjectRef ToObjectRef()
        {
            return new PersonalChronicle.Domain.ObjectRef(CategoryKey, StableId, LabelSnapshot);
        }
    }
}
