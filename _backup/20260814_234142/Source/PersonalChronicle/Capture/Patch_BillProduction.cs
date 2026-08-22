using System;
using HarmonyLib;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// v1.1.4 劳模「工作场所」捕获点（方案 A）：RimWorld.Bill_Production.Notify_IterationCompleted。
    ///
    /// 1.6 target verification（反射核验）：
    ///   RimWorld.Bill_Production.Notify_IterationCompleted(Verse.Pawn billDoer,
    ///       System.Collections.Generic.List`1[Verse.Thing] ingredients) [public,
    ///       instance, non-virtual]
    ///   Bill_Production.billStack (public field) → BillStack.billGiver
    ///   (public field, type RimWorld.IBillGiver) → Building_WorkTable
    ///   （implements IBillGiver，含 Building_WorkTable.billStack 字段）→ defName。
    ///
    /// 制造完成迭代（配方一次完成）调用一次，低频；从这里能同时拿到：
    ///   * 工坊建筑 defName（Building_WorkTable.def.defName，稳定键）
    ///   * 工坊所在房间角色（billGiver.Position 所在 Room.Role）
    /// 与 <see cref="ChronicleGameComponent.SampleResidence"/>（方案 B 住所定期快照）
    /// 组合即「劳模住所 + 工作场所」双检测。
    ///
    /// 只对玩家派系、chronicle 相关、humanlike 的殖民者记录；写入
    /// <see cref="IArchiveService.OnWorkplaceUsed"/>（数据层聚合 WorkplaceSnapshot，
    /// 不写事件流——工坊使用低频且非事件语义）。
    ///
    /// Patch 档案：
    ///   Patch Target      : RimWorld.Bill_Production.Notify_IterationCompleted(Pawn, List`1[Thing])
    ///   Patch Type        : Postfix (read-only, never skips the original)
    ///   Reason            : 记录劳模的工作场所（Building_WorkTable defName + 房间角色）
    ///   Alternative       : Building_WorkTable.IsWorking 轮询 (rejected: 零轮询铁律)；
    ///                       JobDriver_DoBill Toils (rejected: 分散在多子类、patch 面广)
    ///   Compatibility Risk: 低 — Notify_IterationCompleted 是引擎稳定回调，public 非 virtual
    ///   Execution Order   : 无顺序契约；read-only Postfix 任何顺序均安全
    ///   Failure Behavior  : catch → Log.Warning，原方法不受影响
    /// </summary>
    [HarmonyPatch(typeof(Bill_Production), "Notify_IterationCompleted")]
    public static class Patch_BillProduction
    {
        /// <summary>
        /// Cached probe of the patched target signature. [HarmonyPatch] resolves
        /// lazily and silently skips a drifted target, so we verify it once at
        /// startup instead of letting the workplace capture degrade silently.
        /// </summary>
        internal static readonly bool TargetMethodExists = ProbeTargetMethod();

        private static bool ProbeTargetMethod()
        {
            bool found = AccessTools.Method(
                typeof(Bill_Production),
                "Notify_IterationCompleted",
                new Type[] { typeof(Pawn), typeof(System.Collections.Generic.List<Thing>) }) != null;
            if (!found)
            {
                ChronicleLog.Error(ChronicleLog.Category.Capture, "Bill_Production.Notify_IterationCompleted target signature changed; workplace capture silently disabled - update Patch_BillProduction");
            }
            return found;
        }

        public static void Postfix(Bill_Production __instance, Pawn billDoer)
        {
            try
            {
                if (__instance == null || billDoer == null)
                {
                    return;
                }
                if (billDoer.Faction == null || !billDoer.Faction.IsPlayer)
                {
                    return;
                }
                if (!billDoer.RaceProps.Humanlike)
                {
                    return;
                }
                if (!ChronicleColonistScanner.TryClassifyCurrent(billDoer, out _))
                {
                    return;
                }
                // __instance.billStack → billGiver (IBillGiver) → Building_WorkTable。
                // 反射核验：Building_WorkTable implements IBillGiver，as 转换有效。
                BillStack billStack = __instance.billStack;
                Building_WorkTable workbench = (billStack != null && billStack.billGiver != null)
                    ? billStack.billGiver as Building_WorkTable
                    : null;
                if (workbench == null || workbench.def == null)
                {
                    return;
                }
                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }
                service.OnWorkplaceUsed(billDoer, workbench);
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Capture, "Bill_Production.Notify_IterationCompleted patch failed: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Startup hook that forces the one-time signature probe for
    /// RimWorld.Bill_Production.Notify_IterationCompleted.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Patch_BillProductionStartup
    {
        static Patch_BillProductionStartup()
        {
            _ = Patch_BillProduction.TargetMethodExists;
        }
    }
}
