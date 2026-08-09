namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Shared constants for ChronicleEvent.Params data keys. Params are
    /// language-independent identity snapshots stored per event (e.g. a killer
    /// pawn's LabelShort). The UI maps each known key to a translation entry at
    /// render time via <see cref="ChronicleEventParams"/>.Translate; unknown
    /// keys are filtered out — no hardcoded copy in code.
    /// </summary>
    public static class ChronicleEventParams
    {
        /// <summary>
        /// Death-event param: the killer pawn's LabelShort snapshot. Displayed
        /// through the "PersonalChronicle.UI.Killer" translation key.
        /// </summary>
        public const string Killer = "killer";

        /// <summary>
        /// Death/kill-event param: victim LabelShort when the archived subject is
        /// the killer (P2 kill list). Displayed via UI translation at render time.
        /// </summary>
        public const string Victim = "victim";

        /// <summary>
        /// Language-independent victim identity used to make external-kill
        /// capture idempotent when a game or another mod invokes Kill twice.
        /// </summary>
        public const string VictimStableId = "victimStableId";

        /// <summary>
        /// Combat line role marker stored in Params for debug/filter (optional).
        /// Values: "kill" when this death event is indexed primarily as a kill.
        /// </summary>
        public const string CombatRole = "combatRole";

        public const string CombatRoleKill = "kill";

        /// <summary>PawnRelationDef.defName for social events.</summary>
        public const string Relation = "relation";

        /// <summary>Social action: "formed" or "ended".</summary>
        public const string RelationAction = "relationAction";

        public const string RelationActionFormed = "formed";
        public const string RelationActionEnded = "ended";
    }
}
