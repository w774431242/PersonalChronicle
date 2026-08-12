using System.Collections.Generic;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Api.DomainProviders
{
    /// <summary>
    /// Language-independent production input for a single capture batch. Third-party
    /// work benches / production systems can supply their own accumulator instead of
    /// the built-in <see cref="ProductionAccumulator"/>. The provider returns a
    /// <see cref="ProductionContribution"/> that the archive merges into the pawn's
    /// canonical <see cref="ProductionAccumulator"/>.
    ///
    /// Design doc §7.3: external providers must return data keys (defName) only —
    /// never localized text.
    /// </summary>
    public sealed class ProductionInput
    {
        /// <summary>Pawn/thing stable id the production is attributed to.</summary>
        public readonly string StableId;

        /// <summary>Product defName → quantity produced this batch.</summary>
        public readonly IReadOnlyDictionary<string, int> QuantityByDef;

        /// <summary>Product defName → market value produced this batch.</summary>
        public readonly IReadOnlyDictionary<string, float> MarketValueByDef;

        /// <summary>Game tick of the production event (for LastProductionTick).</summary>
        public readonly long GameTick;

        /// <summary>Optional source id for deduplication provenance.</summary>
        public readonly string SourceId;

        public ProductionInput(
            string stableId,
            IReadOnlyDictionary<string, int> quantityByDef,
            IReadOnlyDictionary<string, float> marketValueByDef,
            long gameTick,
            string sourceId = null)
        {
            StableId = stableId;
            QuantityByDef = quantityByDef ?? new Dictionary<string, int>();
            MarketValueByDef = marketValueByDef ?? new Dictionary<string, float>();
            GameTick = gameTick;
            SourceId = sourceId;
        }
    }

    /// <summary>
    /// Immutable contribution returned by a production provider. The archive folds
    /// this into the canonical <see cref="ProductionAccumulator"/>; providers never
    /// mutate stored state directly.
    /// </summary>
    public sealed class ProductionContribution
    {
        public readonly bool IsDefined;
        public readonly int TotalQuantity;
        public readonly float TotalMarketValue;
        public readonly IReadOnlyDictionary<string, int> QuantityByDef;
        public readonly IReadOnlyDictionary<string, float> MarketValueByDef;
        public readonly long GameTick;

        public ProductionContribution(
            bool isDefined,
            int totalQuantity,
            float totalMarketValue,
            IReadOnlyDictionary<string, int> quantityByDef,
            IReadOnlyDictionary<string, float> marketValueByDef,
            long gameTick)
        {
            IsDefined = isDefined;
            TotalQuantity = totalQuantity;
            TotalMarketValue = totalMarketValue;
            QuantityByDef = quantityByDef ?? new Dictionary<string, int>();
            MarketValueByDef = marketValueByDef ?? new Dictionary<string, float>();
            GameTick = gameTick;
        }

        public static readonly ProductionContribution Empty =
            new ProductionContribution(false, 0, 0f, null, null, -1L);
    }

    /// <summary>
    /// Optional external production evaluator (design doc §7.3). Extends the unified
    /// <see cref="IArchiveProvider"/> base contract so it lives alongside other
    /// domain providers in one registry and is discovered by capability token
    /// ("production"). Providers return data keys only, never localized text.
    /// </summary>
    public interface IProductionProvider : IArchiveProvider
    {
        // ProviderId / Priority / ContractVersion / Capabilities inherited from
        // IArchiveProvider — do not re-declare (CS0108).
        bool TryContribute(ProductionInput input, out ProductionContribution contribution);
    }

    /// <summary>Capability token used to look up production providers.</summary>
    public static class ProductionCapabilities
    {
        public const string Production = "production";
    }
}
