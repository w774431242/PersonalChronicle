using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Domain.PawnEconomy.Planning
{
    /// <summary>Persisted planning hysteresis for one subject: the reserve-refill latch and the needs
    /// that were still active when the game was saved. It is a decision-memory value only — never a
    /// payment authority, never a balance and never a goal that survives its own evidence (M5/M3).</summary>
    internal sealed class ShadowPlanLatch
    {
        public string PawnId { get; }
        public int MapId { get; }
        public bool RefillLatch { get; }
        public IReadOnlyList<string> ActiveRequirements { get; }
        public ShadowPlanLatch(string pawnId, int mapId, bool refillLatch, IEnumerable<string> activeRequirements)
        {
            PawnId = MinorCurrency.Required(pawnId); MapId = mapId; RefillLatch = refillLatch;
            ActiveRequirements = PerceptionCollections.Freeze(activeRequirements);
        }
        /// <summary>True when nothing is remembered, so a caller can skip persisting an empty latch.</summary>
        public bool IsEmpty => !RefillLatch && ActiveRequirements.Count == 0;
    }
}
