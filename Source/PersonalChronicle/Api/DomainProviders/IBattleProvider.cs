using System.Collections.Generic;

namespace PersonalChronicle.Api.DomainProviders
{
    /// <summary>
    /// Language-independent battle evaluation context. A battle is an open engagement
    /// identified by battleId; the provider appraises its significance from the
    /// supplied data keys (participant count, casualties, outcome) — never from
    /// localized text.
    /// </summary>
    public sealed class BattleAppraisalInput
    {
        /// <summary>Stable battle id (e.g. a raid encounter key).</summary>
        public readonly string BattleId;

        /// <summary>Data keys describing the battle (e.g. "participants", "casualties",
        /// "outcome"). Defined by each provider; the archive stores them verbatim.</summary>
        public readonly IReadOnlyDictionary<string, string> DataKeys;

        public BattleAppraisalInput(string battleId, IReadOnlyDictionary<string, string> dataKeys)
        {
            BattleId = battleId;
            DataKeys = dataKeys ?? new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Appraisal returned by a battle provider. <see cref="IsSignificant"/> drives
    /// whether the engagement is surfaced in the battle timeline section.
    /// </summary>
    public sealed class BattleAppraisal
    {
        public readonly bool IsDefined;
        public readonly bool IsSignificant;
        public readonly int Weight;

        public BattleAppraisal(bool isDefined, bool isSignificant, int weight)
        {
            IsDefined = isDefined;
            IsSignificant = isSignificant;
            Weight = weight;
        }

        public static readonly BattleAppraisal Empty = new BattleAppraisal(false, false, 0);
    }

    /// <summary>
    /// Optional external battle evaluator (design doc §7.3). Returns a significance
    /// appraisal for an open battle so the timeline can collapse trivial skirmishes
    /// and promote decisive engagements. Providers return data keys only.
    /// </summary>
    public interface IBattleProvider : IArchiveProvider
    {
        // ProviderId / Priority / ContractVersion / Capabilities inherited from
        // IArchiveProvider — do not re-declare (CS0108).
        bool TryAppraise(BattleAppraisalInput input, out BattleAppraisal appraisal);
    }

    /// <summary>Capability token used to look up battle providers.</summary>
    public static class BattleCapabilities
    {
        public const string Battle = "battle";
    }
}
