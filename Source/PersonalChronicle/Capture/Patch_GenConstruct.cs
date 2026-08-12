using System;
using HarmonyLib;
using PersonalChronicle.Application;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// Captures completed construction. Postfix on RimWorld.Frame.CompleteConstruction
    /// — the vanilla "frame becomes the real building" step (MakeThing +
    /// Spawn + Destroy(frame)). Runs exactly once per completed building.
    ///
    /// 1.6 target verification (decompiled Assembly-CSharp):
    ///   RimWorld.Frame::CompleteConstruction(Verse.Pawn worker) — non-virtual
    ///   instance method. IL: reads frame.def.entityDefToBuild, ThingMaker.MakeThing,
    ///   SetFactionDirect, then Thing.Destroy(this), spawns the building.
    ///   Chosen over JobDriver_ConstructFinishFrame.MakeNewToils (iterator +
    ///   reserved-toil plumbing; completion is implicit, hard to pin down) and
    ///   over JobDriver_Construct (does not exist in 1.6 — verified).
    ///
    /// Patch 档案:
    ///   Patch Target      : RimWorld.Frame.CompleteConstruction(Pawn)
    ///   Patch Type        : Postfix (read-only, never skips the original)
    ///   Reason            : record a Built event (PersonalChronicleEventBuilt)
    ///                       with the completed building's ThingDef as Primary
    ///   Alternative       : JobDriver_ConstructFinishFrame completion toil
    ///                       (rejected: no single stable completion method;
    ///                       iterator-based toils are fragile targets);
    ///                       GenConstruct.PlaceBlueprintForBuild (rejected: that
    ///                       is blueprint placement, not completion)
    ///   Compatibility Risk: low-medium — Frame.CompleteConstruction is a known
    ///                       vanilla extension point; read-only Postfix stays
    ///                       safe even if another mod patches it
    ///   Execution Order   : no ordering contract; Postfix order irrelevant
    ///                       because we never mutate
    ///   Failure Behavior  : catch → Log.Warning, original method unaffected
    /// </summary>
    [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction), new Type[] { typeof(Pawn) })]
    public static class Patch_GenConstruct
    {
        public static void Postfix(Frame __instance, Pawn worker)
        {
            try
            {
                if (__instance == null || __instance.def == null)
                {
                    return;
                }
                // Only player-colony construction.
                if (worker == null || worker.Faction == null || !worker.Faction.IsPlayer)
                {
                    return;
                }
                // The frame's def is a Frame ThingDef; entityDefToBuild is the
                // actual building being completed.
                BuildableDef buildable = __instance.def.entityDefToBuild;
                ThingDef builtDef = buildable as ThingDef;
                if (builtDef == null)
                {
                    return;
                }
                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }
                // Stable id from the frame's identity (session-unique historical
                // snapshot, consistent with ThingObject.WeakId semantics).
                string builtStableId = builtDef.defName + ":" + __instance.thingIDNumber;
                service.OnThingBuilt(builtDef, builtStableId, worker);
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Capture, "Frame.CompleteConstruction patch failed: " + ex.Message);
            }
        }
    }
}
