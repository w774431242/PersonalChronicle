using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// How an archive category behaves. Behavior differences MUST be driven by
    /// this enum (through ArchiveCategoryDef), never by code branching on
    /// depth magic numbers.
    /// </summary>
    public enum ArchiveDepthBehavior
    {
        /// <summary>Category contributes statistics only.</summary>
        StatOnly,

        /// <summary>Category contributes archive records.</summary>
        Record,

        /// <summary>Category contributes timeline events.</summary>
        Event
    }

    /// <summary>
    /// Data-driven depth/breadth configuration for an archive category
    /// (Level 1 Def). depth is display metadata only; behavior is driven by
    /// <see cref="behavior"/> (P2 consumes it). Adding a category = adding a Def,
    /// zero code.
    /// </summary>
    public class ArchiveCategoryDef : Def
    {
        /// <summary>
        /// Stable category key ("Pawn"/"Thing"/"Battle"/"Location") this Def
        /// configures. Bridges the data keys (ArchiveCategoryKeys / ObjectRef.CategoryKey)
        /// to Def-driven display metadata (label/depth/behavior). Adding a category =
        /// adding a Def with a matching key, zero code.
        /// </summary>
        public string categoryKey;

        /// <summary>Display depth 1-5 (metadata only; never branch on it).</summary>
        public int depth;

        /// <summary>Behavior switch for this category.</summary>
        public ArchiveDepthBehavior behavior;
    }
}
