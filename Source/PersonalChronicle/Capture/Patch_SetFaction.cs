using System;
using HarmonyLib;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// Captures colonist joins: Pawn.SetFaction(Faction, Pawn recruiter = null)
    /// is the vanilla join path (recruit, refugee join, dev force-join, etc.).
    /// Prefix is read-only (captures the old faction); it never returns false so
    /// the original method always runs.
    ///
    /// Patch 档案:
    ///   Patch Target      : Pawn.SetFaction(Faction, Pawn)
    ///   Patch Type        : Prefix (read-only __state capture) + Postfix (采集)
    ///   Reason            : record colonist join event when a pawn becomes a
    ///                       player-faction humanlike (no native join callback)
    ///   Alternative       : PawnTracker / FactionPawnNotify (rejected: no join
    ///                       hook covering all join paths); MapPawns join events
    ///                       (rejected: map-dependent, misses world joins)
    ///   Compatibility Risk: low-medium — SetFaction is patched by many mods
    ///                       (prisoner/slave/recruit mods); read-only Prefix +
    ///                       Postfix with __state is order-independent; the
    ///                       `__instance.Faction != newFaction` check guards
    ///                       against earlier prefixes skipping the original
    ///   Execution Order   : no ordering contract; read-only, safe in any order
    ///   Failure Behavior  : catch → Log.Warning, original method unaffected
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction), new Type[] { typeof(Faction), typeof(Pawn) })]
    public static class Patch_SetFaction
    {
        public static void Prefix(Pawn __instance, out Faction __state)
        {
            __state = __instance != null ? __instance.Faction : null;
        }

        public static void Postfix(Pawn __instance, Faction newFaction, Faction __state)
        {
            try
            {
                if (__instance == null || newFaction == null)
                {
                    return;
                }
                // 1: original method actually ran (not skipped by an earlier prefix,
                //    and faction really changed).
                if (__instance.Faction != newFaction)
                {
                    return;
                }
                // 2: old faction was not already the player faction.
                if (__state == Faction.OfPlayer)
                {
                    return;
                }
                // 3: new faction is the player faction.
                if (newFaction != Faction.OfPlayer)
                {
                    return;
                }
                // 4: humanlike only.
                if (!__instance.RaceProps.Humanlike)
                {
                    return;
                }
                // 5: not dead.
                if (__instance.Dead)
                {
                    return;
                }
                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }
                // 6: 按共享谓词判定角色（自由殖民者/奴隶/囚犯），写入档案徽标。
                //    非当前殖民地人口（如亚人类、其他派系）不建档。
                if (!ChronicleColonistScanner.TryClassify(__instance, out PawnRole role))
                {
                    return;
                }
                // 7: stable-id dedupe is enforced inside the service/storage layer.
                service.OnColonistJoined(__instance, role);
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Capture, "SetFaction patch failed: " + ex.Message);
            }
        }
    }
}
