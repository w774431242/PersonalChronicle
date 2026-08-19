using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Api;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// Partial of <see cref="ArchiveService"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveService : IArchiveService, IWorkIntensityService, IWorkTimeCaptureService, IArchiveQueryService, IArchiveEventSink
    {

        public ProductionSummaryView GetProductionSummary(string stableId)
        {
            PawnObject pawn = GetObject(stableId) as PawnObject;
            if (pawn == null || pawn.Production == null)
            {
                return new ProductionSummaryView(0, 0f, -1L, new List<ProductionTypeView>());
            }
            ProductionAccumulator production = pawn.Production;
            List<ProductionTypeView> rows = new List<ProductionTypeView>();
            if (production.QuantityByDef != null)
            {
                foreach (KeyValuePair<string, int> pair in production.QuantityByDef)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0)
                    {
                        continue;
                    }
                    float value = 0f;
                    if (production.MarketValueByDef != null)
                    {
                        production.MarketValueByDef.TryGetValue(pair.Key, out value);
                    }
                    rows.Add(new ProductionTypeView(pair.Key, pair.Value, value));
                }
            }
            // Legacy saves have production events but no v4 aggregate. Count
            // those events as a conservative quantity fallback; historical
            // market value is intentionally left at zero because old events
            // never persisted a value snapshot.
            if (rows.Count == 0)
            {
                Dictionary<string, int> legacyCounts = new Dictionary<string, int>();
                IReadOnlyList<ChronicleEvent> legacyEvents = GetProductionEvents(stableId);
                for (int i = 0; i < legacyEvents.Count; i++)
                {
                    ChronicleEvent ev = legacyEvents[i];
                    if (ev == null || ev.Primary == null || string.IsNullOrEmpty(ev.Primary.StableId))
                    {
                        continue;
                    }
                    string defName = ev.Primary.StableId;
                    int colon = defName.IndexOf(':');
                    if (colon > 0)
                    {
                        defName = defName.Substring(0, colon);
                    }
                    int count;
                    if (!legacyCounts.TryGetValue(defName, out count))
                    {
                        count = 0;
                    }
                    legacyCounts[defName] = count + 1;
                }
                foreach (KeyValuePair<string, int> pair in legacyCounts)
                {
                    rows.Add(new ProductionTypeView(pair.Key, pair.Value, 0f));
                }
            }
            // v4.6.5: aggregate by ThingCategory (e.g. "Weapons") instead of the
            // concrete item (e.g. "Bow_Wood"). The overview shows extraction by
            // broad category, not individual item defs.
            List<ProductionTypeView> categoryRows = AggregateProductionByCategory(rows);
            categoryRows.Sort((a, b) => b.MarketValue.CompareTo(a.MarketValue));
            rows = categoryRows;
            int totalQuantity = production.TotalQuantity;
            float totalValue = production.TotalMarketValue;
            if (totalQuantity <= 0 && rows.Count > 0)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    totalQuantity += rows[i].Quantity;
                    totalValue += rows[i].MarketValue;
                }
            }
            return new ProductionSummaryView(
                totalQuantity,
                totalValue,
                production.LastProductionTick,
                rows);
        }

        private static List<ProductionTypeView> AggregateProductionByCategory(
            IReadOnlyList<ProductionTypeView> byDef)
        {
            if (byDef == null || byDef.Count == 0)
            {
                return new List<ProductionTypeView>();
            }
            Dictionary<string, ProductionTypeView> grouped = new Dictionary<string, ProductionTypeView>();
            for (int i = 0; i < byDef.Count; i++)
            {
                ProductionTypeView row = byDef[i];
                if (row == null)
                {
                    continue;
                }
                string key = ResolveProductionCategoryDefName(row.DefName);
                if (string.IsNullOrEmpty(key))
                {
                    key = row.DefName;
                }
                ProductionTypeView existing;
                if (grouped.TryGetValue(key, out existing))
                {
                    grouped[key] = new ProductionTypeView(
                        key, existing.Quantity + row.Quantity, existing.MarketValue + row.MarketValue);
                }
                else
                {
                    grouped[key] = new ProductionTypeView(key, row.Quantity, row.MarketValue);
                }
            }
            return new List<ProductionTypeView>(grouped.Values);
        }

        private static string ResolveProductionCategoryDefName(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return null;
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null || def.thingCategories == null || def.thingCategories.Count == 0)
            {
                return null;
            }
            // Prefer the top-level category (no parent) so e.g. "Bow_Wood" groups
            // under "Weapons" rather than a leaf sub-category.
            for (int i = 0; i < def.thingCategories.Count; i++)
            {
                ThingCategoryDef cat = def.thingCategories[i];
                if (cat != null && cat.parent == null)
                {
                    return cat.defName;
                }
            }
            return def.thingCategories[0].defName;
        }


    }
}
