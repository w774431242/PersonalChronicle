using System;
using HarmonyLib;
using PersonalChronicle.Application;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// 物品级产消读数（`REQ-MVS-013`）：**采矿产物**捕获点。
    ///
    /// <para>1.6 target verification（Cecil 静态核实，Assembly-CSharp 1.6.4871）：
    ///   <c>RimWorld.Mineable.DestroyMined(Pawn pawn)</c> [public, 非 virtual]。
    ///   调用点 = <c>JobDriver_Mine.DoDamage</c>（正常挖掘）与
    ///   <c>Designator_Mine.DesignateSingleCell</c>（设计取消时立即挖除）。
    ///   IL：方法内部 <c>Building.Destroy(DestroyMode)</c> + <c>TrySpawnYield(map, 1, pawn)</c>。</para>
    ///
    /// <para><b>产量口径（复刻 TrySpawnYield 的 IL 公式）</b>：
    /// <c>count = Max(1, def.building.EffectiveMineableYield)</c>；
    /// 产物 = <c>def.building.mineableThing</c>。
    /// ⚠ 已知偏差：<c>mineableYieldWasteable</c> 为 true 的矿（稀有）会再乘难度修正
    /// <c>yieldPct</c>（private 字段，按工程纪律**不反射私有字段**）⇒ 这类矿读数略高估。</para>
    ///
    /// Patch 档案：
    ///   Patch Target      : RimWorld.Mineable.DestroyMined(Pawn)
    ///   Patch Type        : Postfix (read-only)
    ///   Reason            : 物品级产消读数（市场面板展开行「生产」列）
    ///   Alternative       : `Thing.Destroy(DestroyMode)` 扩展 —— **否决**：既有 `Patch_ThingDestroy`
    ///                       的范围被 ThingArchivePolicy 锁死为装备且无 pawn/产量信息；
    ///                       Tick 轮询存量差分 —— **否决**（PERF-001，且无法分离产量/贸易/损失）
    ///   Compatibility Risk: 中低 — 采矿类 Mod 可能改采掘流程；只读 Postfix 安全，
    ///                       风险集中于读数偏差（已登记）而非破坏运行
    ///   Execution Order   : 无顺序契约；read-only 任意顺序均安全
    ///   Failure Behavior  : catch → Log.Warning，原方法不受影响
    ///   Rollback          : 删除本文件即回滚；EXC-2026-001 台账同步变更记录
    /// </summary>
    [HarmonyPatch(typeof(Mineable), nameof(Mineable.DestroyMined), new Type[] { typeof(Pawn) })]
    public static class Patch_ItemFlowMining
    {
        /// <summary>签名探测（漂移 ⇒ Harmony 静默跳过 ⇒ 启动期显式报出）。</summary>
        internal static readonly bool TargetMethodExists = ProbeTargetMethod();

        private static bool ProbeTargetMethod()
        {
            bool found = AccessTools.Method(
                typeof(Mineable),
                "DestroyMined",
                new Type[] { typeof(Pawn) }) != null;
            if (!found)
            {
                ChronicleLog.Error(ChronicleLog.Category.Capture,
                    "Mineable.DestroyMined target signature changed; item-flow mining capture disabled - update Patch_ItemFlowMining");
            }
            return found;
        }

        public static void Postfix(Mineable __instance, Pawn pawn)
        {
            try
            {
                if (__instance == null || __instance.def == null)
                {
                    return;
                }
                // pawn 可为 null（设计取消路径）⇒ 无归属，不记（记不清归属的量宁可丢弃）。
                if (pawn == null || pawn.Faction == null || !pawn.Faction.IsPlayer)
                {
                    return;
                }
                BuildingProperties props = __instance.def.building;
                if (props == null || props.mineableThing == null)
                {
                    return;
                }
                // 2026-10-10：原版作业计酬（采矿）。无经济合同 / 合同未声明 Mining ⇒ 该方法零成本返回；
                // 异常只降级为一次 warning，绝不影响原版采矿。
                try
                {
                    var economy = PersonalChronicle.Economy.PawnEconomy.GameComponent_PawnEconomy.Current;
                    if (economy != null) economy.ReportVanillaWorkCompletion("Mining", __instance, pawn, Find.TickManager.TicksGame);
                }
                catch (Exception error)
                {
                    Log.Warning("[PC][Economy] vanilla mining work evidence failed: " + error.Message);
                }
                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }
                int count = Math.Max(1, props.EffectiveMineableYield);
                service.RecordItemFlow(props.mineableThing.defName, count, true);
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Capture,
                    "Mineable.DestroyMined postfix (item flow) failed: " + ex.Message);
            }
        }
    }

    /// <summary>启动期一次性强制探测。</summary>
    [StaticConstructorOnStartup]
    public static class Patch_ItemFlowMiningStartup
    {
        static Patch_ItemFlowMiningStartup()
        {
            _ = Patch_ItemFlowMining.TargetMethodExists;
        }
    }
}
