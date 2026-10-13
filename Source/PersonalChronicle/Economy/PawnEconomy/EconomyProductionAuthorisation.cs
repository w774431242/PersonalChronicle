using System;
using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy.Production;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Production;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>U6-A (M10 "authorised, not yet started"): the authorisation ledger lives on the workshop
    /// executor — the single writer — and is exported into / restored from an additive save node. A save
    /// without the node loads as an empty ledger, which is the authoritative "nothing authorised" answer
    /// (SAVE-003); an unreadable ledger is reported as unknown and is never published as zero.</summary>
    public sealed partial class GameComponent_PawnEconomy
    {
        private ProductionAuthorisationSaveData productionAuthorisations = new ProductionAuthorisationSaveData();

        /// <summary>Exports the live ledger into the save container. Must run before the node is scribed,
        /// otherwise the previous snapshot would be written.</summary>
        private void ExportProductionAuthorisationsInto(ProductionAuthorisationSaveData data)
        {
            if (data == null) return;
            var rows = new List<ProductionAuthorisationSaveData.Row>();
            var live = WorkshopExecutorTyped;
            if (live != null)
            {
                var exported = live.Ledger.ExportRows();
                for (int i = 0; i < exported.Count; i++)
                    rows.Add(new ProductionAuthorisationSaveData.Row
                    {
                        ProposalId = exported[i].ProposalId, Receipt = exported[i].Receipt, AcceptedTick = exported[i].AcceptedTick,
                        // M10 完整闭环：工单绑定与已交付量必须随档 —— 否则读档后授权会"复活"成未交付。
                        TargetBillKey = exported[i].TargetBillKey, DeliveredAmount = exported[i].DeliveredAmount
                    });
            }
            data.ReplaceRows(rows);
        }

        /// <summary>Restores the ledger from the save container. Load is idempotent: the ledger is cleared
        /// first, structurally invalid rows are dropped by the ledger import, and a corrupt row can never
        /// fail the load.</summary>
        private void RestoreProductionAuthorisationsFrom(ProductionAuthorisationSaveData data)
        {
            var live = WorkshopExecutorTyped;
            if (live == null || data == null) return;
            var source = data.ValidRows;
            var rows = new List<RestockProposalRow>(source.Count);
            for (int i = 0; i < source.Count; i++)
                rows.Add(new RestockProposalRow(source[i].ProposalId, source[i].Receipt, source[i].AcceptedTick,
                    source[i].TargetBillKey, source[i].DeliveredAmount));
            live.Ledger.ImportRows(rows);
        }

        /// <summary>Authorised, not yet started quantity for one category, converged with the observed work
        /// in progress (the same quantity is never covered by both buckets). False means the ledger cannot
        /// be read: callers must publish unknown, never zero.</summary>
        internal bool TryQueryAuthorisedNotStarted(int mapId, string categoryKey, long wipObserved, out long effective)
        {
            effective = 0;
            var live = WorkshopExecutorTyped;
            if (live == null || mapId < 0 || string.IsNullOrWhiteSpace(categoryKey) || wipObserved < 0) return false;
            try
            {
                // M10 完整闭环：三个桶互不重叠 —— 授权总量 / 已交付量 / 观察在制量，同一量只扣一次。
                effective = ProductionAuthorisationRules.EffectiveNotStarted(
                    live.AuthorisedAmount(mapId, categoryKey), live.DeliveredAmount(mapId, categoryKey), wipObserved);
            }
            catch (ArgumentOutOfRangeException) { return false; }   // an overflowing ledger is unknown, never a guess
            return true;
        }

        /// <summary>Deterministic age trim of the persisted ledger. Runs on a 250 tick cadence so a full
        /// ledger can never refuse new authorisations forever.</summary>
        internal int TrimProductionAuthorisations(int now) => WorkshopExecutorTyped?.TrimExpired(now) ?? 0;

        /// <summary>
        /// M10 完整闭环（2026-10-10）：**真实完成事件 → 授权核销**。按工单稳定键回匹配授权行，
        /// 并按配方中**属于该品类**的真实产出量累加已交付量（多产物配方只计目标品类）。
        /// 无匹配授权 ⇒ 零成本返回（绝大多数完成事件不来自授权工单）。
        /// 刻意**不**经过 <c>LegacyFeesAllowed</c>：授权闭环是新体系能力，与遗留费用开关无关。
        /// </summary>
        internal void ReportProductionDelivery(Bill_Production bill, int now)
        {
            var live = WorkshopExecutorTyped;
            if (live == null || bill?.recipe?.products == null) return;
            var bench = bill.billStack?.billGiver as Thing;
            if (bench == null) return;
            string billKey = Production.EconomyWorkshopExecutor.BillKeyOf(bench, bill);
            if (string.IsNullOrEmpty(billKey)) return;
            for (int i = 0; i < bill.recipe.products.Count; i++)
            {
                var product = bill.recipe.products[i];
                if (product?.thingDef == null || product.count <= 0) continue;
                live.ReportDelivery(billKey, product.thingDef.defName, product.count);
            }
        }

        internal IReadOnlyList<RestockProposalRow> QueryProductionAuthorisationRows()
            => WorkshopExecutorTyped?.Ledger.ExportRows() ?? (IReadOnlyList<RestockProposalRow>)Array.Empty<RestockProposalRow>();
        internal int ProductionAuthorisationCount => WorkshopExecutorTyped?.Ledger.Count ?? 0;
        internal bool TryReleaseProductionAuthorisation(string proposalId) => WorkshopExecutorTyped?.TryRelease(proposalId) ?? false;
    }
}
