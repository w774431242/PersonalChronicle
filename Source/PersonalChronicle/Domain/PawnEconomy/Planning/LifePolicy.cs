using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Domain.PawnEconomy.Planning
{
    // Versioned mod policy, not vanilla need consumption or emergency thresholds.
    internal static class PlanningPolicy
    {
        public const int Version = 1, DayTicks = 60000, RefreshTicks = 250, MaximumSubjects = 4096;
        public const int SubjectsPerTick = 2, PopulationRefreshTicks = 600, MaximumSlices = 64, MaximumCandidates = 16;
        public const int LowWaterBasisPoints = 7000, BasisPoints = 10000;
        // v0.5 §40「应急储备」/ v0.7 §6「有限保障期」：下水位之上再加一层应急缓冲，
        // 使补储**更早**触发（不等跌破下水位才动手），但仍以储备目标为上界（不会无止境赚钱）。
        public const int EmergencyReserveBasisPoints = 800;
        public const float FoodEnter = .55f, RestEnter = .45f, JoyEnter = .35f;
        public const float FoodNativeYield = .35f, RestNativeYield = .25f;
        // Prototype publication bounds (playtested later): an opportunity is a one-hour standing offer and
        // a first payment is expected within two local hours of the accepted offer.
        public const int OpportunityLifetimeTicks = 2500, WageTermTicks = 5000;
        // v0.4 §9 饿死防护：同级目标的**等待加权上限**。等待只用于同级排先后，
        // 不得突破等级、合法性、原版控制权或 P0 保护；受阻/冻结/不适用项不累计。
        public const int MaximumWaitWeightTicks = 6000;
        // v0.4 §9「原生活计划不可持续」反馈阈值：连续这么久的 tick 都由基本保障占满时，
        // 应当反馈给玩家（降低可选消费/储蓄、调工时、改服务安排、增加照护分担），而不是无限挤占。
        public const int UnsustainableStreakTicks = 60000;
        // v0.4 §6 消耗速率的下限（比例/tick）：低于此值视为"未在下降"，不给伪时限。
        public const float MinimumDrainRatePerTick = 0.000001f;
        // v0.4 §6 "预计首次有效帮助时刻"目前只有**显式来源**（成本切片时限 / 岗位到账时刻）。
        // 设计还要求"路程 + 排队 + 开始产生效果前的服务时间"，这些必须由接入层读原版位置与服务队列后给出
        // （见 `PlanningSourcePorts` 的端口纪律）；**在没有真实来源之前不填经验值**，宁可保持不可估。
        public const bool HelpEstimateRequiresExplicitSource = true;
    }
    internal sealed class EffectiveTrait
    {
        public string DefName { get; }
        public int Degree { get; }
        public EffectiveTrait(string name, int degree) { DefName = name; Degree = degree; }
    }
    internal sealed class LifePolicy
    {
        public int Version => PlanningPolicy.Version;
        public float LifeStandard { get; }
        public float LaborInclination { get; }
        // 以下六维的**基线**是设计规定的显式中性默认，不是待填的占位 —— 请勿按"常量占位缺陷"处置。
        // 出处：docs/设计文档/功能模块/经济体系/经济需求驱动AI/M02-长期生活规划/02-长期生活规划_代码设计方案.md §3
        //   · "所有维度取 0–1、独立表达，不相加凑成 100%；**0.5 只是显式中性默认，不是已测量人格**"
        //   · 首批特质映射表只批准 4 条（Industriousness→LaborInclination、Ascetic→LifeStandard、
        //     Gourmand→食品体验、NightOwl→夜间窗口）；未被批准的特质一律不得映射，例如
        //     **Greedy 被明确排除**："首版无『储蓄更高』的自动映射，不把原版房间/思想机制误解释成金融人格"。
        // 它们随经历变化走的**不是**特质映射，而是 M9 的有界学习通道：
        //   p = clamp01(base + learnedOffset)，偏移由 StyleLedger 持账、由 StyleLearningRules 限幅。
        public float ReservePreference => Neutral(nameof(ReservePreference));
        public float TimeValue => Neutral(nameof(TimeValue));
        public float FamilyParticipation => Neutral(nameof(FamilyParticipation));
        public float CareerOrientation => Neutral(nameof(CareerOrientation));
        public float ReliabilityPreference => Neutral(nameof(ReliabilityPreference));
        public float SocialLeisure => Neutral(nameof(SocialLeisure));
        /// <summary>该维的 M9 已学偏移；无账本或该维无记录时为 0。</summary>
        public float LearnedOffset(string dimension)
            => learnedOffsets != null && dimension != null && learnedOffsets.TryGetValue(dimension, out float value)
                ? value : 0f;
        /// <summary>显式中性默认 0.5 与已学偏移的合成（合成值恒在 0–1）。</summary>
        private float Neutral(string dimension) => Learning.StyleLearningRules.Effective(.5f, LearnedOffset(dimension));
        public float PreferredWorkHours => 8 + 4 * (LaborInclination - .5f);
        public float RetainedLeisureHours => 2;
        public int ReserveDays => 2;
        public int SavingBasisPoints => 2000;
        public float FoodTarget => .85f + .10f * (LifeStandard - .5f);
        public float RestTarget => .90f;
        public float JoyTarget => .70f;
        public float FoodExperienceBonus { get; }
        public bool PrefersNight { get; }
        public IReadOnlyList<string> Sources { get; }
        private readonly IReadOnlyDictionary<string, float> learnedOffsets;
        public LifePolicy(IEnumerable<EffectiveTrait> traits, IReadOnlyDictionary<string, float> learnedOffsets = null)
        {
            this.learnedOffsets = learnedOffsets;
            float standard = .5f, labor = .5f, food = 0; bool night = false;
            var sources = new List<string> { "NeutralBaseline" };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int examined = 0;
            foreach (var trait in traits ?? Array.Empty<EffectiveTrait>())
            {
                if (++examined > PlanningPolicy.MaximumSlices) throw new ArgumentException("TooManyTraits");
                if (trait == null || !seen.Add(trait.DefName ?? "")) continue;
                // Vanilla IDs + explicit prototype mappings documented in M2; no label matching.
                switch (trait.DefName)
                {
                    case "Industriousness": labor += .05f * Math.Max(-2, Math.Min(2, trait.Degree)); sources.Add("Industriousness"); break;
                    case "Ascetic": standard -= .10f; sources.Add("Ascetic"); break;
                    case "Gourmand": food = .10f; sources.Add("Gourmand"); break;
                    case "NightOwl": night = true; sources.Add("NightOwl"); break;
                }
            }
            // 两个既有维度同样经 M9 合成：特质给**基线**，经历给**偏移**，合成值恒在 0–1。
            LifeStandard = Learning.StyleLearningRules.Effective(standard, LearnedOffset(nameof(LifeStandard)));
            LaborInclination = Learning.StyleLearningRules.Effective(labor, LearnedOffset(nameof(LaborInclination)));
            FoodExperienceBonus = food; PrefersNight = night;
            Sources = PerceptionCollections.Freeze(sources);
        }
        public IReadOnlyDictionary<string, NeedTarget> Targets => new System.Collections.ObjectModel.ReadOnlyDictionary<string, NeedTarget>(
            new Dictionary<string, NeedTarget>(StringComparer.Ordinal) {
                ["Food"] = new NeedTarget(TargetState.Enabled, Fact<float>.Known(FoodTarget), Version),
                ["Rest"] = new NeedTarget(TargetState.Enabled, Fact<float>.Known(RestTarget), Version),
                ["Joy"] = new NeedTarget(TargetState.Enabled, Fact<float>.Known(JoyTarget), Version) });
    }
}
