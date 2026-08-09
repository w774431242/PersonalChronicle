using System.Collections.Generic;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Api
{
    /// <summary>
    /// Unified event input model. The single write contract that any capture
    /// source (vanilla Harmony patches, third-party mods) uses to feed the
    /// archive. Replaces the scattered <c>On*</c> entry points for external
    /// callers; the legacy methods remain as internal thin wrappers.
    ///
    /// Field rules (see design doc §5.1):
    /// - <see cref="SourceId"/>: registering mod's stable identity, e.g. "MyMod.Combat".
    /// - <see cref="EventTypeDefName"/>: points to a ChronicleEventDef; never localizable text.
    /// - <see cref="Tick"/>: supplied by caller; defaults to current game tick when 0.
    /// - <see cref="Importance"/>: optional override; otherwise resolved from the Def.
    /// - <see cref="Primary"/>: main entity (stable id). Required.
    /// - <see cref="Subjects"/>: participants / weapons / locations (association edges).
    /// - <see cref="Parameters"/>: language-independent key/value data.
    /// - <see cref="DeduplicationKey"/>: optional; same-key writes within a session are dropped.
    /// </summary>
    public sealed class ArchiveEventInput
    {
        /// <summary>Stable identity of the reporting source mod (e.g. "PersonalChronicle.Capture", "MyMod.Combat").</summary>
        public string SourceId;

        /// <summary>defName of a ChronicleEventDef (e.g. "PersonalChronicleEventDeath").</summary>
        public string EventTypeDefName;

        /// <summary>Tick of the event. 0 means "use current game tick".</summary>
        public long Tick;

        /// <summary>Optional explicit importance; null means resolve from the Def.</summary>
        public ChronicleImportance? Importance;

        /// <summary>Primary entity of the event (required, must be valid).</summary>
        public ArchiveEntityRef Primary;

        /// <summary>Associated entities (subjects / weapons / locations). May be null/empty.</summary>
        public IReadOnlyList<ArchiveEntityRef> Subjects;

        /// <summary>Language-independent parameters. Keys should come from ChronicleEventParams.</summary>
        public IReadOnlyDictionary<string, string> Parameters;

        /// <summary>
        /// Optional idempotency key. When two writes share the same non-empty key
        /// within one game session, the second is dropped. Recommended shape:
        /// SourceId + ":" + StableId + ":" + EventType + ":" + Tick.
        /// </summary>
        public string DeduplicationKey;

        /// <summary>True when the required identity fields are present.</summary>
        public bool IsValid
        {
            get
            {
                return !string.IsNullOrEmpty(SourceId)
                    && !string.IsNullOrEmpty(EventTypeDefName)
                    && Primary != null
                    && Primary.IsValid;
            }
        }
    }
}
