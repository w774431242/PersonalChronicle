using System;
using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy.Production;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Production;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy.Production
{
    /// <summary>Stable diagnostic IDs for production readouts. Presentation maps them to text.</summary>
    internal static class CategoryReasons
    {
        public const string CategoryUnknown = "CategoryUnknown";
        public const string DemandDomainNotWired = "DemandDomainNotWired";
        /// <summary>U6-A2 语义已收口：该输入**已接线**（机构政策声明字段），此原因码现在的含义是
        /// 「本图**无任何机构**为该品类发布库存目标」= 未发布 ⇒ 读数保持 Unknown，绝不当作目标 0。</summary>
        public const string StockTargetNotWired = "StockTargetNotWired";
        public const string TradeDomainNotWired = "TradeDomainNotWired";
        public const string NotStartedNotWired = "ProductionAuthorisationNotWired";
    }

    /// <summary>Read-only map lookup by unique id for the production adapters.</summary>
    internal static class ProductionMapLookup
    {
        public static Map Find(int mapId)
        {
            if (mapId < 0) return null;
            var maps = Verse.Find.Maps;   // qualified: the helper method name must not shadow Verse.Find
            if (maps == null) return null;
            for (int i = 0; i < maps.Count; i++) if (maps[i] != null && maps[i].uniqueID == mapId) return maps[i];
            return null;
        }
    }

    /// <summary>M10 real readouts. Stock and completable work in progress are read from the running
    /// game (resource counter and vanilla bill stacks on colonist bill givers); the authorised-not-started
    /// input is read from the bounded authorisation ledger (U6-A), converged with the observed work in
    /// progress so the same quantity is never covered by both buckets; the inputs whose domains are not
    /// implemented yet (confirmed demand, free stock target, authorised exports) stay explicitly unknown
    /// and are never published as zero. Readings are map level; the per-institution binding is not wired
    /// yet and is reported as such.</summary>
    internal sealed class EconomyProductionReadoutSource : IProductionReadoutSource
    {
        public const int MaximumDeviceScan = 256, MaximumBillsPerDevice = 32;
        private readonly GameComponent_PawnEconomy owner;
        public EconomyProductionReadoutSource(GameComponent_PawnEconomy owner)
        { this.owner = owner ?? throw new ArgumentNullException(nameof(owner)); }

        public IReadOnlyList<ProductionReadout> ReadReadouts(int mapId, IReadOnlyList<string> categoryKeys, out bool complete)
        {
            complete = false;
            var rows = new List<ProductionReadout>();
            _ = owner;   // the owner keeps this adapter alive with its component; no second truth is read
            var map = ProductionMapLookup.Find(mapId);
            if (map == null || map.resourceCounter == null) return rows;
            var keys = categoryKeys ?? Array.Empty<string>();
            if (keys.Count == 0) { complete = true; return rows; }
            if (keys.Count > ProductionReadoutGuard.MaximumReadouts) return rows;
            var wip = ReadWorkInProgress(map, keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (string.IsNullOrWhiteSpace(key)) continue;
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(key);
                if (def == null)
                {
                    rows.Add(new ProductionReadout(key,
                        Fact<long>.Unsupported(CategoryReasons.CategoryUnknown), Fact<long>.Unsupported(CategoryReasons.CategoryUnknown),
                        Fact<long>.Unsupported(CategoryReasons.CategoryUnknown), Fact<long>.Unsupported(CategoryReasons.CategoryUnknown),
                        Fact<long>.Unsupported(CategoryReasons.CategoryUnknown), Fact<long>.Unsupported(CategoryReasons.CategoryUnknown)));
                    continue;
                }
                long stock = Math.Max(0, map.resourceCounter.GetCount(def));
                long inProgress = wip.TryGetValue(def, out int amount) ? amount : 0;
                // U6-A：已授权未开工来自授权账，并与上面这一份在制读数收敛（同一量不会被两个桶各扣一次）。
                Fact<long> authorisedNotStarted = owner != null
                    && owner.TryQueryAuthorisedNotStarted(mapId, key, inProgress, out long effective)
                    ? Fact<long>.Known(effective)
                    : Fact<long>.Unknown(CategoryReasons.NotStartedNotWired);
                // CHG-20261012-ECON-CONTRACTIZE · B2：库存目标随机构政策一并退役（裁决：概念整体退役，
                // 不为它另找载体）⇒ 恒 Unknown（「未发布 ≠ 目标 0」，M5）。保留原因码与 Fact 形状，
                // 下游补货评估继续按"读数不可用"处理，不把缺失当零。
                Fact<long> freeStockTarget = Fact<long>.Unknown(CategoryReasons.StockTargetNotWired);
                rows.Add(new ProductionReadout(key,
                    Fact<long>.Unknown(CategoryReasons.DemandDomainNotWired),
                    freeStockTarget,
                    Fact<long>.Unknown(CategoryReasons.TradeDomainNotWired),
                    Fact<long>.Known(stock),
                    Fact<long>.Known(inProgress),
                    authorisedNotStarted));
            }
            complete = true;
            return rows;
        }

        /// <summary>Completable work in progress per product: every colonist bill stack that has a bill
        /// that should run now and whose recipe outputs the category counts its per-cycle product amount.
        /// Bounded scan: at most <see cref="MaximumDeviceScan"/> buildings and
        /// <see cref="MaximumBillsPerDevice"/> bills each.</summary>
        private static Dictionary<ThingDef, int> ReadWorkInProgress(Map map, IReadOnlyList<string> keys)
        {
            var result = new Dictionary<ThingDef, int>();
            var wanted = new HashSet<ThingDef>();
            for (int i = 0; i < keys.Count; i++)
            {
                var def = string.IsNullOrWhiteSpace(keys[i]) ? null : DefDatabase<ThingDef>.GetNamedSilentFail(keys[i]);
                if (def != null) wanted.Add(def);
            }
            if (wanted.Count == 0) return result;
            var buildings = map.listerBuildings?.allBuildingsColonist;
            if (buildings == null) return result;
            int scanned = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (++scanned > MaximumDeviceScan) break;
                var building = buildings[i];
                var giver = building as IBillGiver;
                var stack = giver?.BillStack;
                if (stack == null) continue;
                int limit = Math.Min(stack.Count, MaximumBillsPerDevice);
                for (int b = 0; b < limit; b++)
                {
                    var bill = stack[b];
                    if (bill == null || bill.DeletedOrDereferenced || !bill.ShouldDoNow()) continue;
                    var products = bill.recipe?.products;
                    if (products == null) continue;
                    for (int p = 0; p < products.Count; p++)
                    {
                        var product = products[p];
                        if (product?.thingDef == null || !wanted.Contains(product.thingDef)) continue;
                        result.TryGetValue(product.thingDef, out int existing);
                        result[product.thingDef] = existing + Math.Max(1, product.count);
                    }
                }
            }
            return result;
        }
    }
}
