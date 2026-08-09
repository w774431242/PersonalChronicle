using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Persistent production summary for a pawn. It replaces the need to keep
    /// every routine craft/build event as the only source of career statistics.
    /// </summary>
    public sealed class ProductionAccumulator : IExposable
    {
        public Dictionary<string, int> QuantityByDef = new Dictionary<string, int>();
        public Dictionary<string, float> MarketValueByDef = new Dictionary<string, float>();
        public int TotalQuantity;
        public float TotalMarketValue;
        public long LastProductionTick = -1L;

        public void Add(string defName, int quantity, float marketValue, long gameTick)
        {
            if (string.IsNullOrEmpty(defName) || quantity <= 0)
            {
                return;
            }
            if (QuantityByDef == null)
            {
                QuantityByDef = new Dictionary<string, int>();
            }
            if (MarketValueByDef == null)
            {
                MarketValueByDef = new Dictionary<string, float>();
            }
            int oldQuantity;
            if (!QuantityByDef.TryGetValue(defName, out oldQuantity))
            {
                oldQuantity = 0;
            }
            float oldValue;
            if (!MarketValueByDef.TryGetValue(defName, out oldValue))
            {
                oldValue = 0f;
            }
            QuantityByDef[defName] = oldQuantity + quantity;
            MarketValueByDef[defName] = oldValue + marketValue;
            TotalQuantity += quantity;
            TotalMarketValue += marketValue;
            if (gameTick > LastProductionTick)
            {
                LastProductionTick = gameTick;
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref QuantityByDef, "quantityByDef", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref MarketValueByDef, "marketValueByDef", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref TotalQuantity, "totalQuantity", 0);
            Scribe_Values.Look(ref TotalMarketValue, "totalMarketValue", 0f);
            Scribe_Values.Look(ref LastProductionTick, "lastProductionTick", -1L);
            if (QuantityByDef == null)
            {
                QuantityByDef = new Dictionary<string, int>();
            }
            if (MarketValueByDef == null)
            {
                MarketValueByDef = new Dictionary<string, float>();
            }
        }
    }
}
