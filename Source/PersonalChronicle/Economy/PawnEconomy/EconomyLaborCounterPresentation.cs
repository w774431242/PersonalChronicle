using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy.Employment;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class Building_EconomyInstitution
    {
        // Transient presentation references. They never move a Thing or serialize a second inventory.
        private Pawn laborPresenter;
        private Thing presentedPaper;
        private Thing presentedProduct;
        private int presentationJobId;

        internal void PresentLaborDocuments(Pawn pawn)
        {
            if (pawn?.CurJob == null) return;
            laborPresenter = pawn;
            presentationJobId = pawn.CurJob.loadID;
            presentedPaper = GameComponent_PawnEconomy.Current?.QueryLaborHeldPaper(pawn);
            presentedProduct = GameComponent_PawnEconomy.Current?.QueryLaborHeldProduct(pawn);
        }

        internal void ClearLaborPresentation(Pawn pawn)
        {
            if (laborPresenter != pawn) return;
            laborPresenter = null; presentedPaper = null; presentedProduct = null;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            // No ledger queries, holder scans or stage changes in the drawing path.
            if (laborPresenter == null || !laborPresenter.Spawned || laborPresenter.Map != Map
                || laborPresenter.CurJob?.loadID != presentationJobId
                || laborPresenter.CurJob.targetA.Thing != this || laborPresenter.Position != InteractionCell) return;
            DrawPresentedThing(presentedPaper, .35f);
            DrawPresentedThing(presentedProduct, -.35f);
        }

        private void DrawPresentedThing(Thing thing, float offset)
        {
            if (thing == null || thing.Destroyed || thing.Spawned || thing.MapHeld != Map
                || thing.PositionHeld != laborPresenter.Position) return;
            var location = laborPresenter.DrawPos + new Vector3(offset, 0, .25f);
            location.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            thing.Graphic.Draw(location, Rot4.North, thing);
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (var option in base.GetFloatMenuOptions(selPawn)) yield return option;
            var economy = GameComponent_PawnEconomy.Current;
            if (economy == null) yield break;
            foreach (var option in economy.LaborInteractionOptions(selPawn, this)) yield return option;
        }

        private void OpenLaborReconciliationMenu()
        {
            var economy = GameComponent_PawnEconomy.Current;
            var flows = economy?.ReadReconciliationFlowsAtStation(this);
            var options = new List<FloatMenuOption>();
            if (flows != null)
                foreach (var flow in flows)
                {
                    var pawn = economy.QueryPawnByKey(flow.PawnKey);
                    // Only flows relevant to this map and station; choosing one never acts on other rows.
                    if (pawn?.Map != Map || (flow.CurrentStationKey != GetUniqueLoadID() && flow.InstitutionKey != GetUniqueLoadID())) continue;
                    var totals = economy.QueryLaborDeliveryTotals(flow.ContractId);
                    string row = EconomyLabels.Formatted("PC_PEA.LaborVisual.Reconcile.Row", "ReconcileRow",
                        pawn.LabelShortCap, LaborFlowRules.ShortCode(flow.ContractId), totals.Delivered, totals.Remaining,
                        LaborVisualText.ReasonLabel(flow.ReasonCode));
                    var selected = flow;
                    options.Add(new FloatMenuOption(row, () => ConfirmLaborReconciliation(selected)));
                }
            if (options.Count == 0) options.Add(new FloatMenuOption(EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual.Reconcile.NoRows", "NoRows"), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ConfirmLaborReconciliation(LaborWorkflow selected)
        {
            var economy = GameComponent_PawnEconomy.Current;
            var current = economy?.QueryLaborWorkflowById(selected.WorkflowId);
            if (current == null || current.WorkflowId != selected.WorkflowId || current.Hold != FlowHold.ReconciliationRequired) return;
            var totals = economy.QueryLaborDeliveryTotals(current.ContractId);
            string description = EconomyLabels.Formatted("PC_PEA.LaborVisual.Reconcile.Confirm", "ReconcileConfirm", current.WorkflowId, totals.Remaining)
                + "\n" + (economy.QueryPawnByKey(current.PawnKey)?.LabelShortCap.ToString() ?? current.PawnKey)
                + " · " + LaborFlowRules.ShortCode(current.ContractId) + "\n" + LaborVisualText.ReasonLabel(current.ReasonCode)
                + "\n" + EconomyLabels.Formatted("PC_PEA.LaborVisual.Reconcile.Row", "ReconcileRow",
                    current.PawnKey, LaborFlowRules.ShortCode(current.ContractId), totals.Delivered, totals.Remaining,
                    EconomyLabels.FormatSilver(totals.DueMinor));
            Find.WindowStack.Add(new Dialog_MessageBox(description,
                EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual.Reconcile.Resume", "Resume"),
                () => ResolveOneLaborFlow(current.WorkflowId, ReconciliationAction.Resume),
                EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual.Reconcile.WriteOff", "WriteOff"),
                () => ResolveOneLaborFlow(current.WorkflowId, ReconciliationAction.WriteOffRemaining),
                EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual.Reconcile.Title", "ReconcileTitle")));
        }

        private static void ResolveOneLaborFlow(string workflowId, ReconciliationAction action)
        {
            var economy = GameComponent_PawnEconomy.Current;
            if (economy == null) return;
            var code = economy.ResolveLaborReconciliation(workflowId, action, out string reason);
            bool ok = code == LaborFlowCode.Applied || code == LaborFlowCode.Replay;
            Messages.Message(EconomyLabels.Formatted("PC_PEA.LaborVisual.Reconcile.Result", "ReconcileResult",
                LaborVisualText.ReasonLabel(code.ToString()), LaborVisualText.ReasonLabel(reason)),
                ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput, false);
        }
    }

    public sealed partial class GameComponent_PawnEconomy
    {
        internal LaborUiTotals QueryLaborDeliveryTotals(string contractId)
            => new LaborUiTotals(contractId, LaborFlow.ReadBatchesFor(contractId), Employment.ReadObligationsForContract(contractId));
        internal List<LaborWorkflow> ReadReconciliationFlowsAtStation(Building_EconomyInstitution station)
        {
            var result = new List<LaborWorkflow>();
            foreach (var flow in ReadReconciliationFlows())
                if (QueryPawnByKey(flow.PawnKey)?.Map == station.Map
                    && (flow.CurrentStationKey == station.GetUniqueLoadID() || flow.InstitutionKey == station.GetUniqueLoadID())) result.Add(flow);
            return result;
        }
        internal Thing QueryLaborHeldPaper(Pawn pawn)
        {
            var flow = QueryLaborFlowFor(pawn);
            var contract = flow == null ? null : Employment.ReadContract(flow.ContractId);
            if (contract == null) return null;
            return FindLaborHeldThing(pawn.inventory, thing => thing is Thing_WorkContractDocument paper
                && paper.IsValidFor(pawn.GetUniqueLoadID(), contract));
        }
        internal Thing QueryLaborHeldProduct(Pawn pawn)
        {
            if (!TryPendingSubmission(pawn, out var wage)) return null;
            var carry = pawn.carryTracker?.CarriedThing;
            if (carry != null && MatchesBatchInstance(pawn, carry, wage.ContractId, wage.EvidenceId)) return carry;
            return FindLaborHeldThing(pawn.inventory, thing => MatchesBatchInstance(pawn, thing, wage.ContractId, wage.EvidenceId));
        }
        private static Thing FindLaborHeldThing(IThingHolder holder, System.Predicate<Thing> match, int depth = 0)
        {
            if (holder == null || depth > 3) return null;
            var things = holder.GetDirectlyHeldThings();
            if (things == null) return null;
            for (int i = 0; i < things.Count; i++)
            {
                var thing = things[i];
                if (match(thing)) return thing;
                if (thing is IThingHolder nested)
                { var found = FindLaborHeldThing(nested, match, depth + 1); if (found != null) return found; }
            }
            return null;
        }
    }
}
