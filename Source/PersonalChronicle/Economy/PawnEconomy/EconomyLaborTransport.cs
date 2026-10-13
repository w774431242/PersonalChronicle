using System;
using PersonalChronicle.Application.PawnEconomy.Employment;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy
    {
        // One instantaneous physical action: preserve the actual split object instead of merging its identity away.
        internal bool LaborTakeInventory(Pawn pawn, Thing source, int requested, string transportId)
        {
            if (pawn?.inventory?.innerContainer == null || source == null || source.Destroyed
                || !source.Spawned || source.Map != pawn.Map || source.IsForbidden(pawn) || requested <= 0) return false;
            var flow = QueryLaborFlowFor(pawn);
            if (flow == null || flow.Hold == FlowHold.ReconciliationRequired) return false;
            bool paper = source is Thing_WorkContractDocument;
            ProductBatch batch = null;
            int quantity = Math.Min(source.stackCount, requested);
            if (paper)
            {
                var contract = Employment.ReadContract(flow.ContractId);
                if (contract == null || !((Thing_WorkContractDocument)source).IsValidFor(pawn.GetUniqueLoadID(), contract)) return false;
                quantity = 1;
            }
            else
            {
                if (!TryPendingSubmission(pawn, out var obligation)) return false;
                foreach (var row in LaborFlow.ReadOpenBatchesFor(obligation.ContractId))
                    if (row.EvidenceId == obligation.EvidenceId && row.ProducerPawnKey == pawn.GetUniqueLoadID()
                        && row.ThingIdNumber == source.thingIDNumber && row.ThingDefName == source.def.defName)
                    { batch = row; break; }
                if (batch == null) return false;
                quantity = Math.Min(quantity, batch.RemainingQuantity);
                if (!LaborFlow.CanTransferBatchIdentity(batch.BatchId, source.thingIDNumber, quantity, transportId))
                { LaborPlanningBlocked(flow, "BatchWriteBlocked", "Diagnose"); return false; }
            }
            if (quantity <= 0) return false;
            int sourceId = source.thingIDNumber;
            string defName = source.def.defName;
            var origin = source.Position;
            var map = source.Map;
            var split = source.SplitOff(quantity);
            if (split == null || split.Destroyed) return false;
            if (!pawn.inventory.innerContainer.TryAdd(split, false))
            {
                if (!split.Destroyed && split.holdingOwner == null && !split.Spawned)
                {
                    if (source != split && !source.Destroyed) source.TryAbsorbStack(split, false);
                    if (!split.Destroyed && !split.Spawned && split.holdingOwner == null
                        && !GenPlace.TryPlaceThing(split, origin, map, ThingPlaceMode.Near))
                    {
                        // Last native holder fallback keeps a failed pickup saved rather than losing an unowned object.
                        pawn.carryTracker?.TryStartCarry(split);
                        LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, "BatchUnknown", "Diagnose",
                            flow.Stage, Find.TickManager.TicksGame, 0);
                        return false;
                    }
                }
                LaborPlanningBlocked(flow, "ProductNotCarried", "RetryLater");
                return false;
            }
            if (batch != null)
            {
                var code = LaborFlow.TransferBatchIdentity(batch.BatchId, sourceId, split.thingIDNumber,
                    quantity, transportId, Find.TickManager.TicksGame, "pawn:" + pawn.GetUniqueLoadID());
                if (code != LaborFlowCode.Applied && code != LaborFlowCode.Replay)
                {
                    LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, "BatchUnknown", "Diagnose",
                        flow.Stage, Find.TickManager.TicksGame, 0);
                    return false; // Goods remain physically held; never forge a receipt after an identity-write failure.
                }
            }
            LaborInventoryReceived(pawn, quantity, defName);
            return true;
        }
    }
}
