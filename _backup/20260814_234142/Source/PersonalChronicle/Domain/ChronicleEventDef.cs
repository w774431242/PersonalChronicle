using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Semantic category of a ChronicleEventDef. Drives UI classification
    /// (death / battle / craft rows) — the UI MUST read this field through the
    /// Def, never substring-match on TypeKey strings.
    /// </summary>
    public enum ChronicleEventKind
    {
        Other,
        Death,
        Battle,
        Craft,
        Join,
        Built,
        /// <summary>v3.1 P3: social relation events (formed / ended).</summary>
        Social
    }

    /// <summary>
    /// Data-driven event template. Holds translation keys only (no user-visible
    /// text, no hardcoded copy). Rendering happens in the UI layer via
    /// labelKey/descriptionKey.Translate(...).
    ///
    /// Identity vs. rendering are deliberately DECOUPLED (v0.3):
    ///   - <see cref="Def.defName"/> is the SAVE IDENTITY — it is written to
    ///     savegames as <see cref="ChronicleEvent.TypeKey"/> and MUST be
    ///     immutable (rename = broken old saves). Dot-free, per RimWorld.
    ///   - <see cref="labelKey"/> / <see cref="descriptionKey"/> are TRANSLATION
    ///     ENTRY POINTS — freely changeable, never used as an identity.
    ///   - <see cref="kind"/> is the semantic category used for UI row
    ///     classification; loaded from the Def's &lt;kind&gt; element.
    /// </summary>
    public class ChronicleEventDef : Def
    {
        /// <summary>
        /// Translation key for the event label (e.g. "PersonalChronicle.Event.Join").
        /// Rendering-only: the UI resolves the Def by defName/TypeKey first, then
        /// translates through this key. NEVER used as an identity.
        /// </summary>
        public string labelKey;

        /// <summary>
        /// Translation key for the event description. Rendering-only, same
        /// decoupling rule as <see cref="labelKey"/>.
        /// </summary>
        public string descriptionKey;

        /// <summary>
        /// Semantic event category (Death/Battle/Craft/Join/Built). UI
        /// classification reads this through the Def; TypeKey substrings are
        /// forbidden. Defaults to <see cref="ChronicleEventKind.Other"/>.
        /// </summary>
        public ChronicleEventKind kind;

        /// <summary>Default timeline importance, configured in Defs.</summary>
        public int defaultImportance = -1;
    }
}
