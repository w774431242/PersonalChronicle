using System;

namespace PersonalChronicle.Domain.PawnEconomy.Production
{
    /// <summary>Stable diagnostic IDs for production authorisations. Presentation maps them to text;
    /// program logic never parses localized strings.</summary>
    internal static class ProductionAuthorisationReasons
    {
        public const string AuthorisedNotStarted = "ProductionAuthorisedNotStarted";
        public const string ReleasedToWip = "ProductionReleasedToWip";
        public const string Expired = "ProductionAuthorisationExpired";
        public const string SourceUnavailable = "ProductionAuthorisationUnavailable";
        public const string Revoked = "ProductionAuthorisationRevoked";
        /// <summary>M10 闭环：授权已由真实完成事件足额交付并核销。</summary>
        public const string Delivered = "ProductionAuthorisationDelivered";
        /// <summary>M10 闭环：受理时找不到可执行的合法工单载体（无工坊/无匹配配方/工单栈满）⇒ 拒绝授权。</summary>
        public const string ExecutorUnavailable = "ProductionExecutorUnavailable";
    }

    /// <summary>M10 §4: the ledger holds authorised production that has not started yet. The observed
    /// work in progress is already subtracted by the restock formula, so the authorised quantity must
    /// converge with it — the same quantity is never covered by both buckets.
    ///
    /// ⚠ 范围边界（2026-10-09，U6-A）：设计 §4 要求"已授权计划与相同订单/出口键关联"。订单域（M7）与
    /// 出口域尚不存在，故本批按**品类级**收敛（max(0, 账内授权 − 观察在制)）：这是"同一量只扣一次"
    /// 的最小实现，不是按计划键的精确归属。计划键落地后应改为逐键移出。</summary>
    internal static class ProductionAuthorisationRules
    {
        /// <summary>Prototype hard age cap for one authorisation, so a persisted ledger that refuses new
        /// rows at capacity can never lock up forever. 120,000 ticks = two in-game days; the value is a
        /// prototype bound and needs profiling before release.</summary>
        public const int MaximumAuthorisationAgeTicks = 120000;

        /// <summary>Effective not-started quantity for one category: the authorised sum minus the work in
        /// progress already observed for it, never negative. A negative contribution can never be
        /// produced (the formula's other buckets would otherwise be double counted).</summary>
        public static long EffectiveNotStarted(long authorisedSum, long deliveredSum, long wipObserved)
        {
            if (authorisedSum < 0) throw new ArgumentOutOfRangeException(nameof(authorisedSum));
            if (deliveredSum < 0) throw new ArgumentOutOfRangeException(nameof(deliveredSum));
            if (wipObserved < 0) throw new ArgumentOutOfRangeException(nameof(wipObserved));
            // 三个桶互不重叠：已交付的既不算在制、也不算未开工。M10 完整闭环（2026-10-10）新增中间项 ——
            // 此前只有 max(0, 授权 − 在制)，交付量无处记账 ⇒ 完成的授权永远停在"未开工"。
            long remaining = authorisedSum - deliveredSum - wipObserved;
            return remaining <= 0 ? 0 : remaining;
        }

        /// <summary>Sum with overflow refusal: an overflowing authorisation is reported, never clamped.</summary>
        public static long Sum(long left, long right)
        {
            if (left < 0 || right < 0) throw new ArgumentOutOfRangeException(nameof(right));
            try { return checked(left + right); }
            catch (OverflowException) { throw new ArgumentOutOfRangeException(nameof(right), "AuthorisationOverflow"); }
        }

        public static bool IsExpired(int acceptedTick, int now, int maximumAgeTicks)
        {
            if (maximumAgeTicks < 0) throw new ArgumentOutOfRangeException(nameof(maximumAgeTicks));
            if (acceptedTick <= 0) return false;   // 0 = tick unknown (loaded legacy row) => never expire on a guess
            if (now < acceptedTick) return false;  // a tick rollback (load/rewind) must not expire anything
            return now - acceptedTick > maximumAgeTicks;
        }
    }
}
