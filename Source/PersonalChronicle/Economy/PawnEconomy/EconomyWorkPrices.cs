using System;
using System.Linq;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Economy.PawnEconomy.Planning;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy
    {
        private WorkPriceSaveData workPrices = new WorkPriceSaveData();
        internal bool WorkPricesSupported => workPrices.Supported;
        private void ExposeWorkPrices()
        {
            Scribe_Deep.Look(ref workPrices, "pawnEconomicWorkPrices");
            if (workPrices == null) workPrices = new WorkPriceSaveData(); // old save: no configured prices
        }
        internal long ReadWorkPrice(string anchor, string work, string order = null) => workPrices.Read(anchor, work, order);
        internal bool SetWorkPrice(string anchor, string work, string order, long minor)
        {
            var reference = anchors.FirstOrDefault(a => a != null && a.Spawned && !a.Destroyed && a.GetUniqueLoadID() == anchor);
            if (reference == null) return false;
            if (!string.IsNullOrEmpty(order))
            {
                var item = FindVanillaOrder(order);
                if (item == null || item.WorkTypeDefName != work) return false;
            }
            return workPrices.Set(anchor, work, order, minor);
        }
        internal long QuotedWorkPrice(Map map, string anchor, string work, string order, out bool fromMarket)
        {
            if (!workPrices.Supported) { fromMarket = false; return 0; }
            long recommended = RecommendWageMinor(map, work, out fromMarket);
            long def = workPrices.Read(anchor, work), over = workPrices.Read(anchor, work, order);
            if (def > 0 || over > 0) fromMarket = false;
            return WorkPriceRules.Resolve(def, string.IsNullOrEmpty(order) ? 0 : over, recommended);
        }
        internal static VanillaOrder FindVanillaOrder(string key)
        {
            foreach (var item in VanillaOrderCatalogue.Orders) if (item.Key == key) return item;
            return null;
        }
    }
}
