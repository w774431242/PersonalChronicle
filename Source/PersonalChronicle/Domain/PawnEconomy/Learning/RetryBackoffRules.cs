using System;

namespace PersonalChronicle.Domain.PawnEconomy.Learning
{
    /// <summary>M9 重试阶段。<see cref="PersistentBlocked"/> 不再按失败数倍增，改为事件驱动 + 兜底探测。</summary>
    internal enum RetryPhase { Ready, CoolingDown, PersistentBlocked }

    /// <summary>
    /// v0.9 §2 M9 的**有界退避**（纯函数，离线可证伪）。
    ///
    /// 出处：《09-经验反馈与失败恢复_代码设计方案》§4 ——
    /// 原型退避 <c>min(6000, 150×2^min(failureCount−1,6))</c> Tick，失败数至少 1，
    /// **增加稳定小抖动避免全体同相位**；**第 5 次同指纹失败后标 `PersistentBlocked` 并改成事件驱动
    /// + 每 6000 Tick 兜底**；有真实相关变化可提前复核。**条件指纹由有意义的报价/库存/预算/资格/地图修订
    /// 组成，不含当前 Tick**。原版**紧急保护不受经济冷却限制**；**变化只解除相关原因，
    /// 不把所有重试历史清零**。
    ///
    /// "稳定"抖动是硬要求：同一条件指纹必须恒得同一相位，否则存读档后相位跳变、
    /// 退避不再可复现。故抖动由 <see cref="Fingerprint"/> 的确定性哈希给出，**不用随机数**。
    /// </summary>
    internal static class RetryBackoffRules
    {
        public const int BaseTicks = 150;
        public const int CeilingTicks = 6000;
        /// <summary>倍增上限：<c>150 << 6 = 9600</c> 已越过天花板，再倍增只会被截断。</summary>
        public const int MaximumDoublings = 6;
        /// <summary>第 5 次同指纹失败即判结构性受阻。</summary>
        public const int PersistentBlockedAt = 5;
        /// <summary>结构性受阻后的兜底探测间隔（事件驱动之外的保底）。</summary>
        public const int BaselineProbeTicks = 6000;

        /// <summary>退避时长：<c>min(6000, 150×2^min(failureCount−1,6))</c>，失败数至少按 1 计。</summary>
        public static int BackoffTicks(int consecutiveFailures)
        {
            if (consecutiveFailures < 1) consecutiveFailures = 1;
            int exponent = consecutiveFailures - 1;
            if (exponent > MaximumDoublings) exponent = MaximumDoublings;
            long value = (long)BaseTicks << exponent;
            return value > CeilingTicks ? CeilingTicks : (int)value;
        }

        /// <summary>
        /// 稳定小抖动（幅度为退避时长的 1/8）：让不同条件指纹散开相位，避免全体同时重试。
        /// 同一指纹恒得同一值 —— 存读档可复现，**不得**用随机数。
        /// </summary>
        public static int JitterTicks(string fingerprint, int backoffTicks)
        {
            if (backoffTicks <= 0) return 0;
            int span = backoffTicks >> 3;
            if (span <= 0) return 0;
            return (int)(StableHash(fingerprint) % (uint)(span + 1));
        }

        /// <summary>下一次可重试时刻与阶段。<see cref="RetryPhase.PersistentBlocked"/> 用平坦兜底间隔，不再抖动。</summary>
        public static int NextRetryTick(int now, int consecutiveFailures, string fingerprint, out RetryPhase phase)
        {
            if (consecutiveFailures >= PersistentBlockedAt)
            {
                phase = RetryPhase.PersistentBlocked;
                return now + BaselineProbeTicks;
            }
            phase = consecutiveFailures >= 1 ? RetryPhase.CoolingDown : RetryPhase.Ready;
            int backoff = BackoffTicks(consecutiveFailures);
            return now + backoff + JitterTicks(fingerprint, backoff);
        }

        /// <summary>
        /// 是否放行重试。**紧急保护不受冷却限制**（原版紧急/求生的放行权在调用方）。
        /// <paramref name="relevantChange"/> 表示"有真实相关变化"⇒ 提前复核；
        /// 但它**只解除相关原因**，调用方**不得**因此把失败数清零（设计明令）。
        /// </summary>
        public static bool MayRetry(int now, int nextRetryTick, bool relevantChange, bool emergency)
            => emergency || relevantChange || now >= nextRetryTick;

        /// <summary>
        /// 条件指纹：由**有意义的报价/库存/预算/资格/地图修订**组成。
        /// 参数刻意做成枚举式的具名分量而**不接受 Tick** —— 含 Tick 会让每次重算都产生"新条件"，
        /// 退避形同不存在。这是结构性防呆，不靠调用方自觉。
        /// </summary>
        public static string Fingerprint(string quoteRevision, string stockRevision, long budgetMinor,
            string qualification, int mapRevision)
            => string.Concat(quoteRevision ?? "-", "|", stockRevision ?? "-", "|",
                budgetMinor.ToString(System.Globalization.CultureInfo.InvariantCulture), "|",
                qualification ?? "-", "|", mapRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>FNV-1a（32 位）：跨进程、跨存读档稳定，故可作"稳定抖动"的相位来源。</summary>
        private static uint StableHash(string text)
        {
            uint hash = 2166136261u;
            if (string.IsNullOrEmpty(text)) return hash;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
