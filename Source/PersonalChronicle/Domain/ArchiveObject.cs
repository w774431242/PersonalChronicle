using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Polymorphic base of every archived object. Subclasses are persisted via
    /// Scribe's native polymorphism: Scribe writes the concrete class name and
    /// instantiates it on load — therefore subclass class names AND namespaces
    /// must stay stable forever (renaming breaks old saves).
    /// Business code must discriminate with `is` / pattern matching, never by
    /// inspecting a serialized class tag.
    /// </summary>
    public abstract class ArchiveObject : IExposable
    {
        /// <summary>Stable identity, e.g. Pawn.GetUniqueLoadID() for pawns.</summary>
        public string StableId;

        /// <summary>Identity snapshot for display (language independent).</summary>
        public string LabelSnapshot;

        /// <summary>Category discriminator returned by each concrete subclass.</summary>
        public abstract string CategoryKey { get; }

        public ArchiveObject()
        {
        }

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref StableId, "stableId");
            Scribe_Values.Look(ref LabelSnapshot, "labelSnapshot");
        }
    }
}
