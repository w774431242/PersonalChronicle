using System;

namespace PersonalChronicle.Domain.PawnEconomy.Learning
{
    /// <summary>M9 bounded experience for one institution and kind. It carries counts and delays only:
    /// it is never a payment credential, so trimming samples can never allow a double payment.</summary>
    internal sealed class ExperienceSummary
    {
        public const int MaximumSamples = 64;
        public string Key { get; }
        public int Samples { get; }
        public int Successes { get; }
        public long TotalDelay { get; }
        public int LastTick { get; }
        public ExperienceSummary(string key, int samples = 0, int successes = 0, long totalDelay = 0, int lastTick = 0)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("KeyRequired", nameof(key));
            if (samples < 0 || successes < 0 || successes > samples || totalDelay < 0) throw new ArgumentOutOfRangeException(nameof(samples));
            Key = key; Samples = samples; Successes = successes; TotalDelay = totalDelay; LastTick = lastTick;
        }
        /// <summary>§3 显式先验 Beta(1,1) 估计：<c>mean=(1+onTime)/(2+knownOutcomes)</c>。</summary>
        public CredibilityEstimate Credibility => new CredibilityEstimate(Successes, Samples);
        /// <summary>
        /// §3 口径的可信度万分比。**注意与裸成功率不同**：样本为 0 时是 5000（先验中性）而不是 0 ——
        /// 裸成功率会把"还没证据"显示成"极不可靠"，那是设计明令禁止的
        /// （§6 验收："供应方未知/截止未知 ⇒ 显示证据不足，不记失败或满信誉"）。
        /// </summary>
        public int ReliabilityBasisPoints => Credibility.BasisPoints;
        /// <summary>先验在读数中的占比（1 = 全是先验），供展示"先验/样本数"（§3）。</summary>
        public float PriorWeight => Credibility.PriorWeight;
        public long AverageDelayTicks => Samples == 0 ? 0 : TotalDelay / Samples;
        /// <summary>Folds one real outcome in. Sample count is capped so history stays bounded; nothing
        /// about money or dedupe credentials is ever trimmed here.</summary>
        public ExperienceSummary Record(bool success, long delayTicks, int now)
        {
            if (delayTicks < 0) throw new ArgumentOutOfRangeException(nameof(delayTicks));
            int samples = Samples, successes = Successes; long delay = TotalDelay;
            if (samples >= MaximumSamples)
            { long kept = (long)samples - 1; successes = samples == 0 ? 0 : (int)((long)successes * kept / samples); delay = samples == 0 ? 0 : delay * kept / samples; samples = (int)kept; }
            return new ExperienceSummary(Key, samples + 1, successes + (success ? 1 : 0), delay + delayTicks, now);
        }
    }
}