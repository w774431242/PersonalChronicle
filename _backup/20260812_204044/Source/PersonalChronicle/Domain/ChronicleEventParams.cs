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

        /// <summary>
        /// StableId bucket for kills whose DamageInfo instigator could not be
        /// resolved to a chronicle colonist (melee-forwarded / environment kills).
        /// Kept as a constant so the combat log still records these instead of
        /// dropping them entirely.
        /// </summary>
        public const string UnknownKillerId = "__unknown_killer__";

        /// <summary>
        /// Display label used when a death event's killer could not be resolved
        /// (environment / melee-forwarded / unresolvable instigator). Must be a
        /// translation key reference resolved by the UI, never a hardcoded string.
        /// </summary>
        public const string UnknownKillerLabel = "PersonalChronicle.UI.UnknownKiller";

        /// <summary>
        /// Death-event param: the assist pawn's LabelShort snapshot. The assist is
        /// the chronicle colonist who dealt the most damage but did NOT land the
        /// finishing blow (e.g. A chipped 80% HP, B took the kill). Displayed via
        /// the "PersonalChronicle.UI.Assist" translation key.
        /// </summary>
        public const string Assist = "assist";

        // v4.3: victim faction/kind snapshots for faction-codex aggregation.
        // Victim PawnObjects are NOT archived (external enemies/animals), so the
        // faction/category must be snapshotted at record time. These are pure
        // string params; old saves lacking them fall back to the "unknown" bucket.
        public const string VictimFactionDefName = "victimFactionDef";
        public const string VictimFactionLabel = "victimFactionLabel";
        public const string VictimKindDefName = "victimKindDef";
        public const string VictimCategory = "victimCategory";
        public const string VictimCategoryHumanlike = "humanlike";
        public const string VictimCategoryMechanoid = "mechanoid";
        public const string VictimCategoryAnimal = "animal";

        /// <summary>PawnRelationDef.defName for social events.</summary>
        public const string Relation = "relation";

        /// <summary>Social action: "formed" or "ended".</summary>
        public const string RelationAction = "relationAction";

        public const string RelationActionFormed = "formed";
        public const string RelationActionEnded = "ended";
    }
}
