using System;
using PersonalChronicle.Domain.PawnEconomy.Finance;

namespace PersonalChronicle.Domain.PawnEconomy.Production
{
    /// <summary>M10 restock inputs: every field is a real observation (confirmed demand, free stock
    /// target, authorised exports, available stock, completable work in progress, authorised not started).</summary>
    internal sealed class RestockInput
    {
        public long ConfirmedUndeliveredDemand { get; }
        public long FreeStockTarget { get; }
        public long AuthorisedUndeliveredExports { get; }
        public long AvailableStock { get; }
        public long CompletableWorkInProgress { get; }
        public long AuthorisedNotStarted { get; }
        public RestockInput(long demand, long freeTarget, long exports, long stock, long wip, long notStarted)
        {
            foreach (var value in new[] { demand, freeTarget, exports, stock, wip, notStarted })
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(demand));
            ConfirmedUndeliveredDemand = demand; FreeStockTarget = freeTarget; AuthorisedUndeliveredExports = exports;
            AvailableStock = stock; CompletableWorkInProgress = wip; AuthorisedNotStarted = notStarted;
        }
    }
    internal static class RestockPlanner
    {
        /// <summary>M10 §4: max(0, confirmed demand + free stock target + authorised exports - available
        /// stock - completable WIP - authorised not yet started). Authorised production that has not
        /// started is already covered, so a repeated evaluation never adds the same quantity twice.</summary>
        public static long Shortfall(RestockInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            try
            {
                long need = checked(input.ConfirmedUndeliveredDemand + input.FreeStockTarget + input.AuthorisedUndeliveredExports);
                long covered = checked(input.AvailableStock + input.CompletableWorkInProgress + input.AuthorisedNotStarted);
                long gap = need - covered;
                if (gap <= 0) return 0;
                MinorCurrency.Validate(gap);
                return gap;
            }
            catch (OverflowException) { throw new ArgumentOutOfRangeException(nameof(input), "RestockOverflow"); }
        }
    }
}