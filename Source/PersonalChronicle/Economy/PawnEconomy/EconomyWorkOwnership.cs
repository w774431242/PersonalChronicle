using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>Economy employment work-priority channel, authorized by EXC-2026-028. Enable direction
    /// only: a signed contract may switch on the work types its position needs, every write is recorded
    /// before it happens, and a release restores exactly our own value while keeping player edits.
    /// This channel never disables a work type (that stays with EXC-2026-016 / EXC-2026-026).</summary>
    public sealed partial class GameComponent_PawnEconomy
    {
        private PersonalChronicle.Data.PawnEconomy.WorkOwnershipSaveData workOwnership
            = new PersonalChronicle.Data.PawnEconomy.WorkOwnershipSaveData();

        internal OwnershipCode ApplyWorkType(Pawn pawn, WorkTypeDef type, int now)
        {
            if (pawn?.workSettings == null || type == null) return OwnershipCode.InvalidType;
            var code = WorkOwnershipRules.CanEnable(type.defName, pawn.workSettings.Initialized,
                pawn.WorkTypeIsDisabled(type), pawn.workSettings.GetPriority(type));
            if (code != OwnershipCode.Applied) return code;
            if (!workOwnership.Record(pawn.GetUniqueLoadID(), type.defName, WorkOwnershipRules.EnabledPriority, 0, now))
                return OwnershipCode.PlayerOwned;   // already recorded: idempotent, never rewrite the original
            pawn.workSettings.SetPriority(type, WorkOwnershipRules.EnabledPriority);
            return OwnershipCode.Applied;
        }
        internal int ApplyWorkTypes(Pawn pawn, IEnumerable<WorkTypeDef> types, int now)
        {
            int applied = 0;
            if (types == null) return applied;
            foreach (var type in types) if (ApplyWorkType(pawn, type, now) == OwnershipCode.Applied) applied++;
            return applied;
        }
        /// <summary>Releases this pawn's records: restores only values this system wrote (EXC-2026-028 §2).
        /// A priority the player changed since is kept, and its record is dropped without writing.</summary>
        internal int ReleaseWorkTypes(Pawn pawn)
        {
            if (pawn?.workSettings == null) return 0;
            int restored = 0;
            var owned = workOwnership.ReadFor(pawn.GetUniqueLoadID());
            for (int i = 0; i < owned.Count; i++)
            {
                var row = owned[i];
                var type = DefDatabase<WorkTypeDef>.GetNamedSilentFail(row.WorkTypeDefName);
                if (type != null && WorkOwnershipRules.CanRestore(pawn.workSettings.GetPriority(type), row.WrittenPriority) == OwnershipCode.Restored)
                { pawn.workSettings.SetPriority(type, row.OriginalPriority); restored++; }
                workOwnership.Discard(row.PawnKey, row.WorkTypeDefName);
            }
            return restored;
        }
        internal IReadOnlyList<string> QueryOwnedWorkTypes(string pawnKey)
        {
            var rows = new List<string>();
            var owned = workOwnership.ReadFor(pawnKey);
            for (int i = 0; i < owned.Count; i++) rows.Add(owned[i].WorkTypeDefName);
            return rows.AsReadOnly();
        }
        internal int OwnershipCount => workOwnership.Count;
    }
}
