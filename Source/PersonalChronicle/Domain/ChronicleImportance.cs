using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Importance used by the timeline read model. Routine events remain
    /// available for audit, but the UI can hide them and the write layer can
    /// apply a smaller retention budget to them.
    /// </summary>
    public enum ChronicleImportance
    {
        Routine = 0,
        Normal = 1,
        Important = 2,
        Critical = 3
    }

    public static class ChronicleEventImportance
    {
        /// <summary>
        /// Resolves the default level for legacy events and newly-created
        /// events. The event's persisted explicit level wins when present.
        /// </summary>
        public static ChronicleImportance Resolve(ChronicleEvent ev)
        {
            if (ev == null)
            {
                return ChronicleImportance.Routine;
            }
            if (ev.ImportanceLevel >= 0 && ev.ImportanceLevel <= (int)ChronicleImportance.Critical)
            {
                return (ChronicleImportance)ev.ImportanceLevel;
            }
            return Resolve(ev.TypeKey, ev.Params);
        }

        public static ChronicleImportance Resolve(string typeKey, System.Collections.Generic.IReadOnlyDictionary<string, string> parameters)
        {
            if (typeKey == ChronicleEventType.Death)
            {
                string combatRole;
                if (parameters != null
                    && parameters.TryGetValue(ChronicleEventParams.CombatRole, out combatRole)
                    && combatRole == ChronicleEventParams.CombatRoleKill)
                {
                    return ChronicleImportance.Important;
                }
            }
            ChronicleEventDef def = !string.IsNullOrEmpty(typeKey)
                ? DefDatabase<ChronicleEventDef>.GetNamedSilentFail(typeKey)
                : null;
            if (def != null && def.defaultImportance >= 0
                && def.defaultImportance <= (int)ChronicleImportance.Critical)
            {
                return (ChronicleImportance)def.defaultImportance;
            }
            if (typeKey == ChronicleEventType.Death)
            {
                return ChronicleImportance.Critical;
            }
            if (typeKey == ChronicleEventType.Battle
                || typeKey == ChronicleEventType.Join
                || typeKey == ChronicleEventType.Social)
            {
                return ChronicleImportance.Important;
            }
            if (typeKey == ChronicleEventType.Crafted || typeKey == ChronicleEventType.Built)
            {
                return ChronicleImportance.Routine;
            }
            return ChronicleImportance.Normal;
        }
    }
}
