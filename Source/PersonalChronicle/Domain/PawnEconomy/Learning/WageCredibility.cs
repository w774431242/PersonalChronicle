using System;

namespace PersonalChronicle.Domain.PawnEconomy.Learning
{
    /// <summary>M9 wage credibility bands. A band is only claimed once enough samples exist;
    /// below that the reading stays Unproven and declares nothing.</summary>
    internal enum CredibilityBand
    {
        /// <summary>Not enough samples: no claim is made either way.</summary>
        Unproven = 0,
        /// <summary>Enough samples and most of them failed or were slow.</summary>
        Weak = 1,
        /// <summary>Mixed record.</summary>
        Mixed = 2,
        /// <summary>Consistently paid on time.</summary>
        Strong = 3
    }

    /// <summary>M9 derived reading for one institution: counts, reliability and delay only. It is a
    /// planning hint, never a payment credential or an authorisation, so it can never be used to pay
    /// twice and its loss on load can never lose a right.</summary>
    internal sealed class WageCredibility
    {
        public const int MinimumSamplesForClaim = 8;
        public const int WeakBelowBasisPoints = 5000;
        public const int StrongAtBasisPoints = 8000;
        public string Key { get; }
        public int Samples { get; }
        public int ReliabilityBasisPoints { get; }
        public long AverageDelayTicks { get; }
        public CredibilityBand Band { get; }

        private WageCredibility(string key, int samples, int basisPoints, long averageDelay, CredibilityBand band)
        {
            Key = key; Samples = samples; ReliabilityBasisPoints = basisPoints; AverageDelayTicks = averageDelay; Band = band;
        }

        /// <summary>True only when the band is backed by enough real samples.</summary>
        public bool HasClaim => Samples >= MinimumSamplesForClaim;

        /// <summary>True for a low-confidence warning: enough samples to judge AND a weak record.</summary>
        public bool IsLowConfidence => HasClaim && Band == CredibilityBand.Weak;

        public static WageCredibility From(ExperienceSummary summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (summary.Samples < MinimumSamplesForClaim)
                return new WageCredibility(summary.Key, summary.Samples, summary.ReliabilityBasisPoints,
                    summary.AverageDelayTicks, CredibilityBand.Unproven);
            int basisPoints = summary.ReliabilityBasisPoints;
            var band = basisPoints >= StrongAtBasisPoints ? CredibilityBand.Strong
                : basisPoints >= WeakBelowBasisPoints ? CredibilityBand.Mixed
                : CredibilityBand.Weak;
            return new WageCredibility(summary.Key, summary.Samples, basisPoints, summary.AverageDelayTicks, band);
        }
    }
}
