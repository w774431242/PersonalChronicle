using System.Collections.Generic;
using PersonalChronicle.Application;

namespace PersonalChronicle.Api.DomainProviders.Builtin
{
    /// <summary>
    /// Built-in production evaluator (P2). Delegates to the canonical
    /// <see cref="IArchiveService"/> production accumulator so the unified registry
    /// has at least one production provider out of the box. Third-party mods register
    /// higher-priority providers to override the verdict. Returns data keys only.
    /// </summary>
    public sealed class BuiltinProductionProvider : IProductionProvider
    {
        private readonly IArchiveService service;

        public BuiltinProductionProvider(IArchiveService service)
        {
            this.service = service;
        }

        public string ProviderId { get { return "PersonalChronicle.Builtin.Production"; } }
        public int Priority { get { return 0; } }
        public string ContractVersion { get { return "1"; } }
        public IReadOnlyCollection<string> Capabilities
        {
            get { return new List<string> { ProductionCapabilities.Production }; }
        }

        public bool TryContribute(ProductionInput input, out ProductionContribution contribution)
        {
            contribution = ProductionContribution.Empty;
            if (service == null || input == null || string.IsNullOrEmpty(input.StableId))
            {
                return false;
            }
            // The built-in accumulator already owns production state; this provider
            // only exposes it through the unified contract. A non-empty quantity map
            // means the pawn has produced something this session.
            bool has = input.QuantityByDef != null && input.QuantityByDef.Count > 0;
            contribution = new ProductionContribution(
                isDefined: has,
                totalQuantity: Sum(input.QuantityByDef),
                totalMarketValue: Sum(input.MarketValueByDef),
                quantityByDef: input.QuantityByDef,
                marketValueByDef: input.MarketValueByDef,
                gameTick: input.GameTick);
            return has;
        }

        private static int Sum(IReadOnlyDictionary<string, int> map)
        {
            if (map == null)
            {
                return 0;
            }
            int total = 0;
            foreach (KeyValuePair<string, int> kv in map)
            {
                total += kv.Value;
            }
            return total;
        }

        private static float Sum(IReadOnlyDictionary<string, float> map)
        {
            if (map == null)
            {
                return 0f;
            }
            float total = 0f;
            foreach (KeyValuePair<string, float> kv in map)
            {
                total += kv.Value;
            }
            return total;
        }
    }
}
