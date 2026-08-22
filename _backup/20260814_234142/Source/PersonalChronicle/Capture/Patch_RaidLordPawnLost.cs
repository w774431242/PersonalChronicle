using System;
using HarmonyLib;
using PersonalChronicle.Application;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// Captures the repulse of a raid (v4.11 P0, "击退袭击的时间").
    ///
    /// 1.6 engine ground truth (verified by MetadataLoadContext reflection on
    /// Assembly-CSharp.dll):
    ///   - RimWorld has NO "raid ended" event and RaidStrategyWorker has NO
    ///     Notify_* pawn-lifecycle callbacks.
    ///   - A raid is a Lord (LordJob_AssaultColony etc.) whose ownedPawns are the
    ///     enemy raiders. When a raider dies / is downed / captured / exits, the
    ///     engine calls Lord.Notify_PawnLost(Pawn, PawnLostCondition, DamageInfo?)
    ///     and removes it from ownedPawns. This is the precise, per-raid,
    ///     process-completed signal — exactly the archive positioning (no polling).
    ///
    /// Patch 档案:
    ///   Patch Target      : Verse.AI.Group.Lord.Notify_PawnLost(Pawn, PawnLostCondition, DamageInfo?)
    ///   Patch Type        : Postfix (read-only, never skips the original)
    ///   Reason            : when a raid Lord loses a pawn, decrement the linked
    ///                       battle's RemainingRaidCount; finalize (write EndTick)
    ///                       when the last raider is gone.
    ///   Compatibility Risk: low — Notify_PawnLost fires for ALL Lords (rituals,
    ///                       caravans, player lords too), but we only act when the
    ///                       Lord was linked to a battle by LinkRaidLords. Lords that
    ///                       are not raid Lords are no-ops here.
    ///   Execution Order   : none required; we never mutate the original.
    ///   Failure Behavior  : catch → Log.Warning, original unaffected.
    /// </summary>
    [HarmonyPatch(typeof(Lord), nameof(Lord.Notify_PawnLost), new Type[] { typeof(Pawn), typeof(PawnLostCondition), typeof(DamageInfo?) })]
    public static class Patch_RaidLordPawnLost
    {
        public static void Postfix(Lord __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }
                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }
                // Notify_PawnLost has already removed the pawn from ownedPawns by
                // the time the postfix runs, so ownedPawns.Count is the authoritative
                // number of raiders still on the map. We pass it directly instead of
                // decrementing a counter, which avoids double-counting (a pawn can be
                // reported lost more than once across PawnLostCondition values) and
                // naturally finalizes when the last raider is gone.
                int remaining = __instance.ownedPawns != null ? __instance.ownedPawns.Count : 0;
                service.OnRaidPawnGone(__instance.loadID, remaining);
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Capture, "Lord.Notify_PawnLost patch failed: " + ex.Message);
            }
        }
    }
}
