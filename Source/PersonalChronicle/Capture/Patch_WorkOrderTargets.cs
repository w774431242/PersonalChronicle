using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PersonalChronicle.Economy.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Capture
{
    /// <summary>订单目标判定只看真实 JobDef；砍树与割除同树目标也必须区别。</summary>
    public static class WorkOrderTargetRules
    {
        public static string OrderForJob(string job, bool tree)
            => WorkPriceRules.OrderForJob(job, tree);

        internal static bool Reject(Pawn pawn, WorkGiver_Scanner giver, Thing target, string order, bool forced, IntVec3? jobCell = null)
        {
            if (forced || pawn?.Map == null || !WorkAuthorityService.Enabled) return false;
            var runtime = GameComponent_PawnEconomy.Current;
            if (runtime == null) return false;
            string work = giver?.def?.workType?.defName;
            if (string.IsNullOrEmpty(work)) return false;
            IntVec3? cell = order == "Mine" ? jobCell ?? (target != null ? target.Position : (IntVec3?)null) : null;
            return !runtime.AllowsVanillaOrderTarget(pawn, work, order, target, cell);
        }
    }

    /// <summary>
    /// 本机 Cecil 已核实 6 个具体 JobOnThing(Pawn,Thing,bool) 出口。仅限制合同唯一授权下的自动候选，
    /// 保留 forced、白名单与实际值勤授权。Postfix 不改工作面板读数；异常记录后保留原候选。
    /// 原生 Def/Comp 无候选过滤扩展点；已有订单合同不能允许另一订单无薪工作，故需要只读 Postfix。
    /// 修改风险为返回候选的第三方补丁顺序，Priority.Last 后按最终 JobDef 判断；无 Transpiler。
    /// 回滚：关闭工作合同闸门；缓存/存档均无新字段。
    /// </summary>
    [HarmonyPatch]
    public static class Patch_WorkOrderTargets
    {
        public static IEnumerable<MethodBase> VerifiedTargets()
        {
            foreach (Type type in new[] { typeof(WorkGiver_PlantsCut), typeof(WorkGiver_Miner),
                typeof(WorkGiver_HunterHunt), typeof(WorkGiver_Slaughter), typeof(WorkGiver_Tame), typeof(WorkGiver_HaulGeneral) })
            {
                var method = AccessTools.DeclaredMethod(type, "JobOnThing", new[] { typeof(Pawn), typeof(Thing), typeof(bool) });
                if (method != null) yield return method;
                else Log.ErrorOnce("[PC] 订单目标挂点签名缺失：" + type.FullName, type.FullName.GetHashCode());
            }
        }
        private static IEnumerable<MethodBase> TargetMethods() => VerifiedTargets();

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(WorkGiver_Scanner __instance, Pawn pawn, Thing t, bool forced, ref Job __result)
        {
            if (__result == null || forced || __result.playerForced) return;
            try
            {
                Thing target = __result.targetA.Thing ?? t;
                string order = WorkOrderTargetRules.OrderForJob(__result.def?.defName, target?.def?.plant?.IsTree == true);
                IntVec3? cell = order == "Mine" && __result.targetA.IsValid ? __result.targetA.Cell : (IntVec3?)null;
                if (WorkOrderTargetRules.Reject(pawn, __instance, target, order, forced, cell)) __result = null;
            }
            catch (Exception error)
            { Log.ErrorOnce("[PC] 订单目标过滤失败（保留原候选）：" + error.Message, 0x5A1830); }
        }
    }

    /// <summary>Hunt/Slaughter 自己重写 HasJobOnThing，须在最近目标挑选之前一起限制，避免近处非合同目标吞掉扫描。</summary>
    [HarmonyPatch]
    public static class Patch_WorkOrderTargetAvailability
    {
        public static IEnumerable<MethodBase> VerifiedTargets()
        {
            foreach (Type type in new[] { typeof(WorkGiver_HunterHunt), typeof(WorkGiver_Slaughter) })
            {
                var method = AccessTools.DeclaredMethod(type, "HasJobOnThing", new[] { typeof(Pawn), typeof(Thing), typeof(bool) });
                if (method != null) yield return method;
                else Log.ErrorOnce("[PC] 订单可用目标挂点签名缺失：" + type.FullName, type.FullName.GetHashCode());
            }
        }
        private static IEnumerable<MethodBase> TargetMethods() => VerifiedTargets();

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(WorkGiver_Scanner __instance, Pawn pawn, Thing t, bool forced, ref bool __result)
        {
            if (!__result || forced) return;
            try
            {
                string order = __instance is WorkGiver_HunterHunt ? "Hunt" : "Slaughter";
                if (WorkOrderTargetRules.Reject(pawn, __instance, t, order, forced)) __result = false;
            }
            catch (Exception error)
            { Log.ErrorOnce("[PC] 订单可用目标过滤失败（保留原候选）：" + error.Message, 0x5A1831); }
        }
    }
}
