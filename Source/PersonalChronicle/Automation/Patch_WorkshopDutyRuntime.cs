using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Automation
{
    // 工作链入口守卫：防止到饭点/下班后刚结束又接回同一种生产任务。
    [HarmonyPatch(typeof(JobGiver_Work), "PawnCanUseWorkGiver")]
    internal static class Patch_WorkshopDutyWorkWindow
    {
        private static bool Prefix(JobGiver_Work __instance, Pawn pawn, WorkGiver giver, ref bool __result)
        {
            if (__instance.emergency || pawn?.Map == null) return true;
            var manager = MapComponent_WorkshopManager.Get(pawn.Map);
            if (manager == null || manager.CanRunScheduledWork(pawn, giver?.def?.workType)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_DoBill), nameof(WorkGiver_DoBill.JobOnThing))]
    internal static class Patch_WorkshopDutyBillTarget
    {
        private static bool Prefix(WorkGiver_DoBill __instance, Pawn pawn, Thing thing, bool forced, ref Job __result)
        {
            if (pawn?.Map == null || forced) return true;
            var manager = MapComponent_WorkshopManager.Get(pawn.Map);
            if (manager == null || manager.AllowBillTarget(pawn, __instance, thing, forced)) return true;
            __result = null;
            return false;
        }
    }

    // 精确结算短工单：T1 加工可能短于 60 tick，不能只靠地图轮询登记。
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    internal static class Patch_WorkshopDutyActualJobStarted
    {
        private static void Postfix(Pawn ___pawn)
        {
            if (___pawn?.Map == null) return;
            MapComponent_WorkshopManager.Get(___pawn.Map)?.NotifyAutomaticWorkshopJobStarted(___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
    internal static class Patch_WorkshopDutyActualJobEnded
    {
        private static void Prefix(Pawn ___pawn)
        {
            if (___pawn?.Map == null) return;
            var manager = MapComponent_WorkshopManager.Get(___pawn.Map);
            if (manager?.ActiveZoneOf(___pawn) != null) manager.NotifyDutyEnded(___pawn);
        }
    }
}
