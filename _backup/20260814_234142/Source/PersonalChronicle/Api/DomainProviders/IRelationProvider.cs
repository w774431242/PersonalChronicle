using System.Collections.Generic;

namespace PersonalChronicle.Api.DomainProviders
{
    /// <summary>
    /// Language-independent relation context for a pair of colonists. The provider
    /// reduces stored <see cref="SignificantRelation"/> data into a significance
    /// verdict from data keys only (e.g. relationDef count, bond strength) — never
    /// from localized names.
    /// </summary>
    public sealed class RelationAppraisalInput
    {
        /// <summary>First colonist stable id.</summary>
        public readonly string A;

        /// <summary>Second colonist stable id.</summary>
        public readonly string B;

        /// <summary>Data keys describing the relation (e.g. "relationDefs",
        /// "formedTick"). Defined by each provider.</summary>
        public readonly IReadOnlyDictionary<string, string> DataKeys;

        public RelationAppraisalInput(
            string a, string b, IReadOnlyDictionary<string, string> dataKeys)
        {
            A = a;
            B = b;
            DataKeys = dataKeys ?? new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Appraisal returned by a relation provider. <see cref="IsSignificant"/> drives
    /// whether the relationship is surfaced in the social timeline section.
    /// </summary>
    public sealed class RelationAppraisal
    {
        public readonly bool IsDefined;
        public readonly bool IsSignificant;

        public RelationAppraisal(bool isDefined, bool isSignificant)
        {
            IsDefined = isDefined;
            IsSignificant = isSignificant;
        }

        public static readonly RelationAppraisal Empty = new RelationAppraisal(false, false);
    }

    /// <summary>
    /// Optional external relationship evaluator (design doc §7.3). Scores the
    /// significance of a pairwise colonist relation so the social timeline can
    /// prioritize meaningful bonds. Providers return data keys only.
    /// </summary>
    public interface IRelationProvider : IArchiveProvider
    {
        // ProviderId / Priority / ContractVersion / Capabilities inherited from
        // IArchiveProvider — do not re-declare (CS0108).
        bool TryAppraise(RelationAppraisalInput input, out RelationAppraisal appraisal);
    }

    /// <summary>Capability token used to look up relation providers.</summary>
    public static class RelationCapabilities
    {
        public const string Relation = "relation";
    }
}
