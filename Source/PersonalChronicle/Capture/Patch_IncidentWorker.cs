using System;
using HarmonyLib;
using PersonalChronicle.Application;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// Captures battle-grade incident starts (raids, infestations...). Postfix
    /// on IncidentWorker.TryExecute — the single non-virtual entry point every
    /// incident goes through.
    ///
    /// 1.6 target verification (decompiled Assembly-CSharp):
    ///   RimWorld.IncidentWorker::TryExecute(RimWorld.IncidentParms) — NON-virtual
    ///   (newslot=false, virtual=false) and its IL ends with
    ///   `callvirt IncidentWorker.TryExecuteWorker` (which IS virtual and is
    ///   overridden by IncidentWorker_RaidEnemy etc.). Patching TryExecuteWorker
    ///   on the base class would MISS every subclass override; patching
    ///   TryExecute catches them all. DoIncident does NOT exist in 1.6 (verified
    ///   by full-assembly search) — TryExecute is the correct 1.6 entry point.
    ///
    /// P1 red line: no IncidentDef.defName string comparison. Judgment is fully
    /// data-driven through IncidentBattleExtension on the IncidentDef.
    ///
    /// Patch 档案:
    ///   Patch Target      : RimWorld.IncidentWorker.TryExecute(IncidentParms)
    ///   Patch Type        : Postfix (read-only, never skips the original)
    ///   Reason            : record a Battle event (PersonalChronicleEventBattle)
    ///                       when the incident def carries isBattle=true
    ///   Alternative       : IncidentWorker.TryExecuteWorker (rejected: virtual,
    ///                       subclass overrides bypass the base patch — would
    ///                       miss RaidEnemy); IncidentWorker.DoIncident (rejected:
    ///                       does not exist in 1.6)
    ///   Compatibility Risk: low-medium — TryExecute is a hot path; the Postfix
    ///                       is cheap (one GetModExtension lookup + early exit).
    ///                       Many mods patch incident workers; [HarmonyAfter] not
    ///                       required because we are read-only
    ///   Execution Order   : no ordering contract; Postfix order irrelevant
    ///                       because we never mutate
    ///   Failure Behavior  : catch → Log.Warning, original method unaffected
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute), new Type[] { typeof(IncidentParms) })]
    public static class Patch_IncidentWorker
    {
        public static void Postfix(IncidentWorker __instance, bool __result)
        {
            try
            {
                // TryExecute returned false — the incident was rejected (parms
                // vetoed / insufficient points / rewritten by another mod). Do
                // NOT record a Battle for an incident that never fired.
                if (!__result)
                {
                    return;
                }
                if (__instance == null || __instance.def == null)
                {
                    return;
                }
                // Data-driven gate: only incidents explicitly flagged as battle.
                IncidentBattleExtension ext = __instance.def.GetModExtension<IncidentBattleExtension>();
                if (ext == null || !ext.isBattle)
                {
                    return;
                }
                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }
                service.OnBattleStarted(__instance.def);
            }
            catch (Exception ex)
            {
                Log.Warning("PersonalChronicle: IncidentWorker.TryExecute patch failed: " + ex.Message);
            }
        }
    }
}
