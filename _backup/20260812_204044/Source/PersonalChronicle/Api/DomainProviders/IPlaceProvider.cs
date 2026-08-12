using System.Collections.Generic;

namespace PersonalChronicle.Api.DomainProviders
{
    /// <summary>
    /// Language-independent place visit context. The provider derives a footprint
    /// verdict from data keys (e.g. biome key, dwell ticks) — never from localized
    /// place names.
    /// </summary>
    public sealed class PlaceFootprintInput
    {
        /// <summary>Map biome defName, or "tile:{id}" for caravan (matches
        /// <see cref="PersonalChronicle.Domain.PlaceVisit.PlaceKey"/>).</summary>
        public readonly string PlaceKey;

        /// <summary>"Map" or "Caravan".</summary>
        public readonly string PlaceKind;

        /// <summary>Total dwell ticks (LeaveTick - EnterTick when closed).</summary>
        public readonly long DwellTicks;

        /// <summary>Data keys describing the place (e.g. "biome", "faction").</summary>
        public readonly IReadOnlyDictionary<string, string> DataKeys;

        public PlaceFootprintInput(
            string placeKey, string placeKind, long dwellTicks, IReadOnlyDictionary<string, string> dataKeys)
        {
            PlaceKey = placeKey;
            PlaceKind = placeKind;
            DwellTicks = dwellTicks;
            DataKeys = dataKeys ?? new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Footprint verdict returned by a place provider. <see cref="IsSignificant"/>
    /// drives whether the place is surfaced in the overview footprint section.
    /// </summary>
    public sealed class PlaceFootprint
    {
        public readonly bool IsDefined;
        public readonly bool IsSignificant;

        public PlaceFootprint(bool isDefined, bool isSignificant)
        {
            IsDefined = isDefined;
            IsSignificant = isSignificant;
        }

        public static readonly PlaceFootprint Empty = new PlaceFootprint(false, false);
    }

    /// <summary>
    /// Optional external place evaluator (design doc §7.3). Scores the significance
    /// of a colonist's stay at a place (map biome / caravan tile) so the overview
    /// footprint can prioritize meaningful locations. Providers return data keys only.
    /// </summary>
    public interface IPlaceProvider : IArchiveProvider
    {
        // ProviderId / Priority / ContractVersion / Capabilities inherited from
        // IArchiveProvider — do not re-declare (CS0108).
        bool TryEvaluate(PlaceFootprintInput input, out PlaceFootprint footprint);
    }

    /// <summary>Capability token used to look up place providers.</summary>
    public static class PlaceCapabilities
    {
        public const string Place = "place";
    }
}
