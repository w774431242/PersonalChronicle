using System;
using System.Globalization;

namespace PersonalChronicle.Domain.PawnEconomy.Perception
{
    internal enum TargetState { Unknown = 0, Enabled = 1, Disabled = 2, NotApplicable = 3 }

    internal static class RequirementIdentity
    {
        public const string LevelComponent = "level";

        // Length-prefixed stable IDs avoid delimiter collisions. No Tick, price, payer or revision.
        public static string ForNeed(string beneficiaryId, string canonicalNeedId, string component)
        {
            return Part(beneficiaryId, nameof(beneficiaryId)) + Part(canonicalNeedId, nameof(canonicalNeedId))
                + Part(component, nameof(component));
        }

        private static string Part(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A stable ID is required.", parameter);
            return value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
        }
    }

    /// <summary>One immutable reading. Capture adapters must explicitly verify standard ratio semantics.</summary>
    internal sealed class NumericNeedFact
    {
        public string NeedDefName { get; }
        public string DomainId { get; }
        public string AdapterId { get; }
        public Fact<float> CurrentLevel { get; }
        public Fact<float> MaximumLevel { get; }
        public Fact<bool> IsFrozen { get; }
        public Fact<string> VanillaCategory { get; }
        public bool StandardRatioSupported { get; }
        public string RawUnitId { get; }
        public long FactRevision { get; }
        public int CapturedTick { get; }
        /// <summary>
        /// 原版 <c>Need.ShowOnNeedList</c>：false 表示**原版需求页不显示**该项
        /// （如 RoomSize、Suppression、未激活的 Chemical、Disabled 的 Indoors/Outdoors）。
        /// 采集侧不据此过滤（设计要求"保留 Unsupported 清单"），但消费方须据此与常规需求区分呈现。
        /// </summary>
        public bool ShowOnNeedList { get; }

        public NumericNeedFact(string needDefName, string domainId, string adapterId,
            Fact<float> currentLevel, Fact<float> maximumLevel, bool standardRatioSupported,
            string rawUnitId, long factRevision, int capturedTick, Fact<bool> isFrozen = default,
            Fact<string> vanillaCategory = default, bool showOnNeedList = true)
        {
            if (string.IsNullOrWhiteSpace(needDefName)) throw new ArgumentException("A stable need ID is required.", nameof(needDefName));
            if (string.IsNullOrWhiteSpace(domainId)) throw new ArgumentException("A domain ID is required.", nameof(domainId));
            if (string.IsNullOrWhiteSpace(adapterId)) throw new ArgumentException("An adapter ID is required.", nameof(adapterId));
            if (factRevision < 0) throw new ArgumentOutOfRangeException(nameof(factRevision));
            if (capturedTick < 0) throw new ArgumentOutOfRangeException(nameof(capturedTick));
            NeedDefName = needDefName;
            DomainId = domainId;
            AdapterId = adapterId;
            CurrentLevel = currentLevel;
            MaximumLevel = maximumLevel;
            StandardRatioSupported = standardRatioSupported;
            RawUnitId = string.IsNullOrWhiteSpace(rawUnitId) ? null : rawUnitId;
            FactRevision = factRevision;
            CapturedTick = capturedTick;
            IsFrozen = isFrozen;
            VanillaCategory = vanillaCategory;
            ShowOnNeedList = showOnNeedList;
        }
    }

    internal readonly struct NeedTarget
    {
        public TargetState State { get; }
        public Fact<float> Ratio { get; }
        public long Revision { get; }

        public NeedTarget(TargetState state, Fact<float> ratio, long revision)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            State = state;
            Ratio = ratio;
            Revision = revision;
        }
    }

    /// <summary>A current gap, not a risk score, forecast, item count or monetary amount.
    /// CapturedTick and FactRevision refer only to the supplied numeric reading.</summary>
    internal sealed class CurrentNeedGap
    {
        public string RequirementKey { get; }
        public string BeneficiaryId { get; }
        public string DomainId { get; }
        public TargetState TargetState { get; }
        public Fact<float> ActualRatio { get; }
        public Fact<float> TargetRatio { get; }
        public Fact<float> RelativeTargetGap { get; }
        public Fact<float> AbsoluteRatioGap { get; }
        public Fact<float> RawLevelShortfall { get; }
        public string RawUnitId { get; }
        public long FactRevision { get; }
        public long TargetRevision { get; }
        public int CapturedTick { get; }

        public CurrentNeedGap(string beneficiaryId, NumericNeedFact reading, NeedTarget target,
            Fact<float> actualRatio, Fact<float> targetRatio, Fact<float> relativeGap,
            Fact<float> absoluteGap, Fact<float> rawShortfall)
        {
            RequirementKey = RequirementIdentity.ForNeed(beneficiaryId, reading.NeedDefName, RequirementIdentity.LevelComponent);
            BeneficiaryId = beneficiaryId;
            DomainId = reading.DomainId;
            TargetState = target.State;
            ActualRatio = actualRatio;
            TargetRatio = targetRatio;
            RelativeTargetGap = relativeGap;
            AbsoluteRatioGap = absoluteGap;
            RawLevelShortfall = rawShortfall;
            RawUnitId = reading.RawUnitId;
            FactRevision = reading.FactRevision;
            TargetRevision = target.Revision;
            CapturedTick = reading.CapturedTick;
        }
    }
}
