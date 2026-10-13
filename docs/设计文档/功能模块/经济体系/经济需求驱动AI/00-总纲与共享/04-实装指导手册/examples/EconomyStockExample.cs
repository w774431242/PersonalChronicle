// API specimen only. No Def registers this comp in the production mod.
using System.Collections.Generic;
using Verse;

namespace PawnEconomy.HandbookExamples
{
    public sealed class CompEconomyStockExample : ThingComp, IThingHolder
    {
        private ThingOwner<Thing> stock;

        private void EnsureStock()
        {
            if (stock == null)
                stock = new ThingOwner<Thing>(this, false, LookMode.Deep);
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            EnsureStock();
            return stock;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            // The MVP permits only simple apparel without nested containers.
            // Production code must enforce that whitelist before accepting stock.
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref stock, "peaExampleRealStock", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                EnsureStock();
        }
        // No public transfer method: transaction-aware stock access is a separate
        // production adapter, with ownership and rollback guarantees to implement.
    }
}
