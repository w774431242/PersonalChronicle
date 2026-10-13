using System;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Economy.PawnEconomy.Planning;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>Immutable pre-completion player intent. Never inferred from a vanished designation.</summary>
    internal sealed class VanillaOrderIntent
    {
        internal readonly string Order, WorkType, TargetId, ContractId;
        internal readonly Pawn Worker;
        internal readonly int MapId;
        internal VanillaOrderIntent(string order, string work, string target, Pawn worker, int map, string contractId)
        { Order = order; WorkType = work; TargetId = target; Worker = worker; MapId = map; ContractId = contractId; }
    }
    public sealed partial class GameComponent_PawnEconomy
    {
        internal VanillaOrderIntent CaptureVanillaOrderIntent(string orderKey, Thing target, Pawn pawn)
        {
            var order = FindVanillaOrder(orderKey);
            Map map = pawn?.Map;
            if (order == null || target == null || map == null || target.Map != map || pawn.Faction != Faction.OfPlayer) return null;
            var def = DefDatabase<DesignationDef>.GetNamedSilentFail(order.DesignationDefName ?? VanillaHarvestPlantDefName);
            if (def == null) return null;
            var designation = map.designationManager?.DesignationOn(target, def);
            if (designation == null || !playerDesignations.Contains(DesignationKey(designation))) return null;
            var contract = ActiveContractFor(pawn.GetUniqueLoadID());
            if (contract == null || !DeclaresWorkType(contract, order.WorkTypeDefName)
                || !contract.AllowsHour(GenLocalDate.HourOfDay(map))) return null;
            if (!WorkPriceRules.Parse(contract.PositionKey, out _, out _, out string scopedOrder)) return null;
            if (scopedOrder != null && scopedOrder != orderKey) return null;
            return new VanillaOrderIntent(orderKey, order.WorkTypeDefName, target.GetUniqueLoadID(), pawn, map.uniqueID, contract.ContractId);
        }
        // eventKey identifies repeatable events (successful tame or haul trip); dead-target IDs are sufficient for animals.
        internal bool ReportVanillaOrderCompletion(VanillaOrderIntent intent, int now, string eventKey = null)
        {
            Pawn pawn = intent?.Worker;
            if (pawn?.Map == null || pawn.Map.uniqueID != intent.MapId) return false;
            var contract = ActiveContractFor(pawn.GetUniqueLoadID());
            if (contract == null || contract.ContractId != intent.ContractId || contract.ExpiresTick <= now || !contract.AllowsHour(GenLocalDate.HourOfDay(pawn.Map))
                || !DeclaresWorkType(contract, intent.WorkType)) return false;
            if (!WorkPriceRules.Parse(contract.PositionKey, out _, out string anchorKey, out string scopedOrder)
                || scopedOrder != null && scopedOrder != intent.Order) return false;
            bool validAnchor = false;
            for (int i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor != null && anchor.Spawned && !anchor.Destroyed && anchor.Map == pawn.Map
                    && !anchor.IsBurning() && anchor.GetUniqueLoadID() == anchorKey) { validAnchor = true; break; }
            }
            if (!validAnchor) return false;
            var evidence = new WorkEvidence(contract.ContractId,
                "order:" + intent.Order + ":" + intent.MapId + ":" + intent.TargetId + ":" + (eventKey ?? "completed"),
                "VanillaWork", 0, 1, "job", now);
            var accepted = Employment.AcceptEvidence(evidence);
            if (accepted.Code != EmploymentCode.Applied) return false;
            InvalidateVanillaBacklog();
            // CHG-20261011-LABOR-VISUAL：**第三个完成入口**同样走统一出口（三处同规则；漏这一处即为
            // "订单完成提前付款"漏口，T08 会抓到）。
            return TrySettleAfterEvidence(pawn, accepted, now);
        }
        internal int AvailableOrderJobs(Map map, string anchor, VanillaOrder order)
            => Math.Max(0, QueryOrderBacklog(map, order, VanillaWorkBacklogCap)
                - VanillaJobsCommitted(map, order.WorkTypeDefName, anchor, order.Key));
        internal static string CompletionOrder(string workType, Thing target, Pawn pawn)
        {
            if (workType == VanillaWorkTypeMining) return "Mine";
            if (workType == VanillaWorkTypePlantCutting)
                return WorkPriceRules.OrderForJob(pawn?.CurJob?.def?.defName, target is Plant p && p.def?.plant?.IsTree == true);
            return null;
        }
        internal bool AllowsVanillaOrderTarget(Pawn pawn, string workType, string orderKey, Thing target, IntVec3? cell = null)
        {
            if (!WorkAuthorityService.Enabled || pawn?.Map == null) return true;
            // Whitelist/actual shift is an independent authority, so a narrow contract cannot revoke it.
            if (WorkAuthorityService.HasIndependentAuthority(pawn, workType)) return true;
            var contract = ActiveContractFor(pawn.GetUniqueLoadID());
            if (contract == null || !DeclaresWorkType(contract, workType)) return true;
            if (!WorkPriceRules.Parse(contract.PositionKey, out _, out _, out string scope) || scope == null) return true;
            if (scope != orderKey) return false;
            var order = FindVanillaOrder(orderKey);
            if (order == null) return false;
            var manager = pawn.Map.designationManager;
            Designation designation;
            if (cell.HasValue)
            {
                designation = manager.DesignationAt(cell.Value, DesignationDefOf.Mine)
                    ?? manager.DesignationAt(cell.Value, DesignationDefOf.MineVein);
            }
            else
            {
                var def = DefDatabase<DesignationDef>.GetNamedSilentFail(order.DesignationDefName ?? VanillaHarvestPlantDefName);
                designation = target == null || def == null ? null : manager.DesignationOn(target, def);
            }
            return designation != null && playerDesignations.Contains(DesignationKey(designation));
        }
    }
}
