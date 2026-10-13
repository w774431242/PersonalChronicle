using System.Threading;
using PersonalChronicle.Application.PawnEconomy.Finance;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy
    {
        private readonly int reconciliationOwnerThread = Thread.CurrentThread.ManagedThreadId;
        // Deliberate read only. Never call Account/Person/Enable/Recover while building a report.
        internal LegacyReconciliationReport QueryLegacyReconciliation()
        {
            if (Thread.CurrentThread.ManagedThreadId != reconciliationOwnerThread)
                throw new System.InvalidOperationException("WrongThread");
            return LegacyReconciler.Capture(State);
        }
    }
}
