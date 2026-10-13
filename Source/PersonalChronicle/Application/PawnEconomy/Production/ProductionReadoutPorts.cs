using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Production;

namespace PersonalChronicle.Application.PawnEconomy.Production
{
    /// <summary>M10 readout port: one row per requested product category, published only from real
    /// observations. A source with no readable map reports complete=false and publishes nothing;
    /// categories it cannot resolve stay explicitly unknown inside their row.</summary>
    internal interface IProductionReadoutSource
    {
        IReadOnlyList<ProductionReadout> ReadReadouts(int mapId, IReadOnlyList<string> categoryKeys, out bool complete);
    }

    /// <summary>Safe zero: nothing observed, nothing claimed.</summary>
    internal sealed class UnpublishedProductionReadoutSource : IProductionReadoutSource
    {
        public IReadOnlyList<ProductionReadout> ReadReadouts(int mapId, IReadOnlyList<string> categoryKeys, out bool complete)
        { complete = false; return Array.Empty<ProductionReadout>(); }
    }

    /// <summary>Single guarded entry point for production readouts. Null rows, oversize publications and
    /// source exceptions degrade to "nothing published" and never to a partial publication that still
    /// looks complete.</summary>
    internal static class ProductionReadoutGuard
    {
        public const int MaximumReadouts = 32;
        public static void Read(IProductionReadoutSource source, int mapId, IReadOnlyList<string> categoryKeys,
            out IReadOnlyList<ProductionReadout> rows, out bool complete)
        {
            rows = Array.Empty<ProductionReadout>(); complete = false;
            if (source == null || mapId < 0) return;
            try
            {
                var read = source.ReadReadouts(mapId, categoryKeys ?? Array.Empty<string>(), out bool published);
                if (!published || read == null || read.Count > MaximumReadouts) return;
                for (int i = 0; i < read.Count; i++) if (read[i] == null) return;
                rows = read; complete = true;
            }
            catch (Exception) { rows = Array.Empty<ProductionReadout>(); complete = false; }
        }
    }
}
