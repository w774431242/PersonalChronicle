using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Application.PawnEconomy.Production;
using PersonalChronicle.Domain.PawnEconomy.Learning;
using PersonalChronicle.Domain.PawnEconomy.Production;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>B6 wiring: production readouts, the workshop executor contract and the bounded wage
    /// credibility ledger. Everything here is read-only or bookkeeping — no bill is created, no
    /// schedule is written, no money moves.</summary>
    public sealed partial class GameComponent_PawnEconomy
    {
        private IProductionReadoutSource productionReadouts;
        private IWorkshopExecutorContract workshopExecutor;
        private readonly ExperienceLedger wageExperience = new ExperienceLedger();

        private IProductionReadoutSource ProductionReadouts => productionReadouts
            ?? (productionReadouts = new PersonalChronicle.Economy.PawnEconomy.Production.EconomyProductionReadoutSource(this));
        private IWorkshopExecutorContract WorkshopExecutor => workshopExecutor
            ?? (workshopExecutor = new PersonalChronicle.Economy.PawnEconomy.Production.EconomyWorkshopExecutor());
        /// <summary>The same single executor instance, typed: the authorisation ledger (U6-A) is exported
        /// and restored through exactly this object, never through a second ledger.</summary>
        private PersonalChronicle.Economy.PawnEconomy.Production.EconomyWorkshopExecutor WorkshopExecutorTyped
            => WorkshopExecutor as PersonalChronicle.Economy.PawnEconomy.Production.EconomyWorkshopExecutor;

        /// <summary>Distinct product categories that current colonist bill stacks output. Used as the
        /// default category set for the readout page; bounded and sorted.</summary>
        internal IReadOnlyList<string> QueryProducedCategoryKeys(Map map)
        {
            var keys = new List<string>();
            var buildings = map?.listerBuildings?.allBuildingsColonist;
            if (buildings == null) return keys.AsReadOnly();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int scanned = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (++scanned > PersonalChronicle.Economy.PawnEconomy.Production.EconomyProductionReadoutSource.MaximumDeviceScan) break;
                var stack = (buildings[i] as IBillGiver)?.BillStack;
                if (stack == null) continue;
                int limit = Math.Min(stack.Count, PersonalChronicle.Economy.PawnEconomy.Production.EconomyProductionReadoutSource.MaximumBillsPerDevice);
                for (int b = 0; b < limit; b++)
                {
                    var bill = stack[b];
                    if (bill == null || bill.DeletedOrDereferenced) continue;
                    var products = bill.recipe?.products;
                    if (products == null) continue;
                    for (int p = 0; p < products.Count; p++)
                    {
                        var defName = products[p]?.thingDef?.defName;
                        if (string.IsNullOrEmpty(defName) || !seen.Add(defName)) continue;
                        if (keys.Count >= ProductionReadoutGuard.MaximumReadouts) { keys.Sort(StringComparer.Ordinal); return keys.AsReadOnly(); }
                        keys.Add(defName);
                    }
                }
            }
            keys.Sort(StringComparer.Ordinal);
            return keys.AsReadOnly();
        }

        /// <summary>Read production readouts through the guard. complete=false means nothing was
        /// published (no map, adapter failure or oversize publication), never "all zero".</summary>
        internal IReadOnlyList<ProductionReadout> QueryProductionReadouts(Map map, IReadOnlyList<string> categoryKeys, out bool complete)
        {
            ProductionReadoutGuard.Read(ProductionReadouts, map?.uniqueID ?? -1, categoryKeys, out var rows, out complete);
            return rows;
        }

        /// <summary>One category evaluated for a real shortfall. Unknown inputs produce an explicit
        /// Unknown outcome, never a fabricated gap.</summary>
        internal RestockEvaluation EvaluateRestock(Map map, string categoryKey)
        {
            if (map == null || string.IsNullOrWhiteSpace(categoryKey))
                return RestockEvaluation.Unknown(categoryKey ?? "unknown", "ReadoutUnavailable");
            var keys = new[] { categoryKey };
            ProductionReadoutGuard.Read(ProductionReadouts, map.uniqueID, keys, out var rows, out bool complete);
            if (!complete || rows.Count == 0)
                return RestockEvaluation.Unknown(categoryKey, "ReadoutUnavailable");
            return RestockEvaluator.Evaluate(rows[0]);
        }

        /// <summary>U6-A3：总览页的补货评估面（这也是 `RestockEvaluator` 的**唯一生产消费点**）。
        /// 一次**批量读**覆盖全部品类（绝不每类各扫一次建筑），再逐行走纯评估；返回的是**有界且确定性**的
        /// 展示切片（≤4 行），`totalCategories` 供 UI 明示"显示 N/总 M"。未知输入保持未知。</summary>
        internal IReadOnlyList<RestockEvaluation> QueryRestockEvaluations(Map map, IReadOnlyList<string> categoryKeys,
            out int totalCategories, out bool complete)
        {
            totalCategories = 0; complete = false;
            var keys = categoryKeys ?? Array.Empty<string>();
            if (map == null || keys.Count == 0) return Array.Empty<RestockEvaluation>();
            ProductionReadoutGuard.Read(ProductionReadouts, map.uniqueID, keys, out var rows, out complete);
            if (!complete || rows.Count == 0) return Array.Empty<RestockEvaluation>();
            var evaluated = new List<RestockEvaluation>(rows.Count);
            for (int i = 0; i < rows.Count; i++) evaluated.Add(RestockEvaluator.Evaluate(rows[i]));
            return RestockPresentation.Select(evaluated, RestockPresentation.MaximumRowsShown, out totalCategories);
        }

        /// <summary>Workshop capacity through the guarded contract; unreadable stays unknown.</summary>
        internal WorkshopCapacityReading QueryWorkshopCapacity(Map map)
            => WorkshopExecutorGuard.ReadCapacity(WorkshopExecutor, map?.uniqueID ?? -1);

        /// <summary>Submits one restock proposal to the workshop executor contract. Acceptance is
        /// bookkeeping only: no bill is created and the existing executor stays the single runner. A
        /// repeated proposal answers with the original receipt.</summary>
        internal bool TrySubmitRestockProposal(Map map, string categoryKey, long shortfallMinor,
            out string receipt, out bool replay, out string reason)
        {
            receipt = null; replay = false; reason = null;
            if (map == null || shortfallMinor <= 0) { reason = ProductionProposalReasons.Invalid; return false; }
            ProductionProposal proposal;
            try
            {
                proposal = new ProductionProposal(map.uniqueID.ToString(CultureInfo.InvariantCulture),
                    categoryKey, shortfallMinor, ProductionProposalReasons.Restock);
            }
            catch (ArgumentException) { reason = ProductionProposalReasons.Invalid; return false; }
            return WorkshopExecutorGuard.TrySubmit(WorkshopExecutor, proposal, out receipt, out replay, out reason);
        }

        /// <summary>Accepted restock proposal receipts (bounded, read-only view).</summary>
        internal IReadOnlyList<string> QueryRestockReceipts()
            => workshopExecutor is PersonalChronicle.Economy.PawnEconomy.Production.EconomyWorkshopExecutor typed
                ? typed.Receipts : Array.Empty<string>();

        /// <summary>M9: folds one real settlement outcome into the bounded credibility ledger. Only
        /// counts and delay are stored; this is never a payment credential.</summary>
        internal void RecordWageOutcome(string institutionKey, bool success, long delayTicks, int now)
            => wageExperience.Record(institutionKey, success, delayTicks, now);

        internal WageCredibility QueryWageCredibility(string institutionKey)
        {
            var summary = wageExperience.Read(institutionKey);
            return summary == null ? null : WageCredibility.From(summary);
        }

        internal IReadOnlyList<string> QueryCredibilityKeys() => wageExperience.Keys;
        internal int QueryCredibilityCount => wageExperience.Count;
    }
}
