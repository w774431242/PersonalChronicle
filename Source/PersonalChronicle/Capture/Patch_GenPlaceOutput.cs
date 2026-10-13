using System;
using System.Reflection;
using HarmonyLib;
using PersonalChronicle.Economy.PawnEconomy;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// CHG-20261011-LABOR-VISUAL：包装原版真实 placedAction，记录存活载体和本次增量。
    /// 本机 DLL 核实无 out 重载委托有 out 重载；只补后者一次，不重复记录。
    /// 合堆回调 count 是本次吸收量；拆分落地 count 是本次生成量。最终 false 也可能部分落地。
    /// Prefix 只包装回调，不改返回值、数量、落点或随机产量；保留原回调及其异常语义。
    /// 目标漂移显式告警；捕获失败不影响原版落地。删除此 patch 即回滚捕获。
    /// </summary>
    [HarmonyPatch]
    public static class Patch_GenPlaceOutput
    {
        internal static readonly bool TargetMethodExists = ProbeTargetMethod();
        internal static MethodInfo TargetMethod() => AccessTools.Method(typeof(GenPlace), nameof(GenPlace.TryPlaceThing),
            new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(),
                typeof(Action<Thing, int>), typeof(Predicate<IntVec3>), typeof(Rot4?), typeof(int) });

        private static bool ProbeTargetMethod()
        {
            bool exists = TargetMethod() != null;
            if (!exists) ChronicleLog.Error(ChronicleLog.Category.Capture,
                "GenPlace.TryPlaceThing out-Thing signature changed; real output capture disabled.");
            return exists;
        }

        internal static void Prefix(Thing thing, IntVec3 center, Map map, ref Action<Thing, int> placedAction)
        {
            try
            {
                var component = GameComponent_PawnEconomy.Current;
                var context = component?.BeginGeneratedOutputCapture(thing, center, map);
                if (context == null) return;
                placedAction = WrapPlacedAction(placedAction,
                    (placed, quantity) => component.NoteGeneratedThing(context, placed, quantity));
            }
            catch (Exception error)
            {
                ReportCaptureFailure("setup", error);
            }
        }

        internal static Action<Thing, int> WrapPlacedAction(Action<Thing, int> original, Action<Thing, int> observer)
            => (placed, quantity) =>
            {
                try { observer?.Invoke(placed, quantity); }
                catch (Exception error)
                {
                    ReportCaptureFailure("callback", error);
                }
                original?.Invoke(placed, quantity);
            };

        private static void ReportCaptureFailure(string phase, Exception error)
        {
            try { ChronicleLog.Warning(ChronicleLog.Category.Capture, "GenPlace output " + phase + " failed: " + error.Message); }
            catch { /* 捕获及诊断自身的异常不能阻断原版 callback/产物落地。 */ }
        }
    }

    [StaticConstructorOnStartup]
    public static class Patch_GenPlaceOutputStartup
    {
        static Patch_GenPlaceOutputStartup() { _ = Patch_GenPlaceOutput.TargetMethodExists; }
    }
}
