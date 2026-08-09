namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Single source of truth for the ChronicleEvent TypeKey constants. Values
    /// MUST stay byte-identical to the ChronicleEventDef defNames in
    /// Defs/Chronicle_Events.xml — TypeKey is written to savegames as the event
    /// identity (defName), so renaming breaks old saves.
    ///
    /// Every consumer (ArchiveService writes, UI classification, startup
    /// validation) references these constants; no code may hold its own copy of
    /// a TypeKey string. ChronicleStartup validates the binding at load.
    /// </summary>
    public static class ChronicleEventType
    {
        public const string Join = "PersonalChronicleEventJoin";
        public const string Death = "PersonalChronicleEventDeath";
        public const string Crafted = "PersonalChronicleEventCrafted";
        public const string Built = "PersonalChronicleEventBuilt";
        public const string Battle = "PersonalChronicleEventBattle";
        /// <summary>v3.1 P3: social relation formed/ended (lover, spouse, family…).</summary>
        public const string Social = "PersonalChronicleEventSocial";
    }
}
