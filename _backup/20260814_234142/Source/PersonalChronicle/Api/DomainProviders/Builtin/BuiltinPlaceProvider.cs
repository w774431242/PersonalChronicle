using System;
using System.Collections.Generic;
using PersonalChronicle.Application;

namespace PersonalChronicle.Api.DomainProviders.Builtin
{
    /// <summary>
    /// Built-in place evaluator (P2). Judges footprint significance from dwell time
    /// (data keys only), so it needs no global state. A place is significant when the
    /// colonist stayed longer than one in-game day. Third-party mods register a
    /// higher-priority <see cref="IPlaceProvider"/> to override the verdict.
    /// </summary>
    public sealed class BuiltinPlaceProvider : IPlaceProvider
    {
        /// <summary>Dwell threshold for "significant" — one in-game day.</summary>
        private const long SignificantDwellTicks = 60000L;

        private readonly IArchiveService service;

        public BuiltinPlaceProvider(IArchiveService service)
        {
            this.service = service;
        }

        public string ProviderId { get { return "PersonalChronicle.Builtin.Place"; } }
        public int Priority { get { return 0; } }
        public string ContractVersion { get { return "1"; } }
        public IReadOnlyCollection<string> Capabilities
        {
            get { return new List<string> { PlaceCapabilities.Place }; }
        }

        public bool TryEvaluate(PlaceFootprintInput input, out PlaceFootprint footprint)
        {
            footprint = PlaceFootprint.Empty;
            if (input == null || string.IsNullOrEmpty(input.PlaceKey))
            {
                return false;
            }
            bool significant = input.DwellTicks >= SignificantDwellTicks;
            footprint = new PlaceFootprint(isDefined: true, isSignificant: significant);
            return true;
        }
    }
}
