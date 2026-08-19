using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain.Profession;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// P2-B 专业技能评估器纯逻辑测试（可离线）。
    /// 覆盖：品质系数 / 单次实践 XP 公式 / XP→等级边际递减 / 等级→熟练度 /
    ///       能力 XP 权重拆分 / 方向评分与降序。
    /// 注意：ProfessionalSkillDef 等依赖 Verse Def 的类不做离线实例化，仅测纯函数。
    /// </summary>
    [TestFixture]
    public class ProfessionalEvaluatorTests
    {
        private const double Epsilon = 1e-6;

        // ------------------------------------------------------------------
        // 品质系数 QualityMultiplier
        // ------------------------------------------------------------------

        [Test]
        public void QualityMultiplier_KnownQualities()
        {
            Assert.AreEqual(5f, ProfessionalXpEvaluator.QualityMultiplier("Legendary"), Epsilon);
            Assert.AreEqual(3f, ProfessionalXpEvaluator.QualityMultiplier("Masterwork"), Epsilon);
            Assert.AreEqual(1.5f, ProfessionalXpEvaluator.QualityMultiplier("Excellent"), Epsilon);
            Assert.AreEqual(1.2f, ProfessionalXpEvaluator.QualityMultiplier("Good"), Epsilon);
        }

        [Test]
        public void QualityMultiplier_DefaultQualities_ReturnOne()
        {
            Assert.AreEqual(1f, ProfessionalXpEvaluator.QualityMultiplier("Normal"), Epsilon);
            Assert.AreEqual(1f, ProfessionalXpEvaluator.QualityMultiplier("Awful"), Epsilon);
            Assert.AreEqual(1f, ProfessionalXpEvaluator.QualityMultiplier("Poor"), Epsilon);
            Assert.AreEqual(1f, ProfessionalXpEvaluator.QualityMultiplier("Shoddy"), Epsilon);
        }

        [Test]
        public void QualityMultiplier_NullOrEmpty_ReturnOne()
        {
            Assert.AreEqual(1f, ProfessionalXpEvaluator.QualityMultiplier(null), Epsilon);
            Assert.AreEqual(1f, ProfessionalXpEvaluator.QualityMultiplier(""), Epsilon);
        }

        // ------------------------------------------------------------------
        // P3 治理：数据驱动品质系数表 QualityMultiplier(string, entries)
        // ------------------------------------------------------------------

        [Test]
        public void QualityMultiplier_PolicyEntries_TakePrecedence()
        {
            var entries = new List<QualityXpEntry>
            {
                new QualityXpEntry { qualityName = "Legendary", multiplier = 9f },
                new QualityXpEntry { qualityName = "Good", multiplier = 1.8f }
            };
            Assert.AreEqual(9f, ProfessionalXpEvaluator.QualityMultiplier("Legendary", entries), Epsilon);
            Assert.AreEqual(1.8f, ProfessionalXpEvaluator.QualityMultiplier("Good", entries), Epsilon);
        }

        [Test]
        public void QualityMultiplier_PolicyEntry_Missing_ReturnsBuiltin()
        {
            var entries = new List<QualityXpEntry> { new QualityXpEntry { qualityName = "Legendary", multiplier = 9f } };
            // 表中无 Masterwork → 回退内建表 3
            Assert.AreEqual(3f, ProfessionalXpEvaluator.QualityMultiplier("Masterwork", entries), Epsilon);
            // 表中无 Normal → 回退内建表 1
            Assert.AreEqual(1f, ProfessionalXpEvaluator.QualityMultiplier("Normal", entries), Epsilon);
        }

        [Test]
        public void QualityMultiplier_PolicyEntries_NullOrEmpty_Fallback()
        {
            Assert.AreEqual(5f, ProfessionalXpEvaluator.QualityMultiplier("Legendary", null), Epsilon);
            Assert.AreEqual(5f, ProfessionalXpEvaluator.QualityMultiplier("Legendary", new List<QualityXpEntry>()), Epsilon);
            Assert.AreEqual(1f, ProfessionalXpEvaluator.QualityMultiplier(null, new List<QualityXpEntry>()), Epsilon);
        }

        // ------------------------------------------------------------------
        // 单次实践 XP 公式 ComputePracticeXp
        // ------------------------------------------------------------------

        [Test]
        public void ComputePracticeXp_Baseline_ReturnsBaseTimesRelevance()
        {
            // base=10, rel=1.0, 无品质(1), diff=1, qty=1 → 10
            float xp = ProfessionalXpEvaluator.ComputePracticeXp(10f, 1f, 1f, 1f, 1);
            Assert.AreEqual(10f, xp, Epsilon);
        }

        [Test]
        public void ComputePracticeXp_QualityMultiplier_Applied()
        {
            // Legendary=5 → 10*1*5*1*1 = 50
            float xp = ProfessionalXpEvaluator.ComputePracticeXp(10f, 1f, 5f, 1f, 1);
            Assert.AreEqual(50f, xp, Epsilon);
        }

        [Test]
        public void ComputePracticeXp_Quantity_AppliedAndClamped()
        {
            // qty=3 → 30
            Assert.AreEqual(30f, ProfessionalXpEvaluator.ComputePracticeXp(10f, 1f, 1f, 1f, 3), Epsilon);
            // qty=10 钳到 4 → 40
            Assert.AreEqual(40f, ProfessionalXpEvaluator.ComputePracticeXp(10f, 1f, 1f, 1f, 10), Epsilon);
            // qty=0 视为 1 → 10
            Assert.AreEqual(10f, ProfessionalXpEvaluator.ComputePracticeXp(10f, 1f, 1f, 1f, 0), Epsilon);
        }

        [Test]
        public void ComputePracticeXp_HalfRelevance_WorkTypeOnly()
        {
            // rel=0.5 → 5
            float xp = ProfessionalXpEvaluator.ComputePracticeXp(10f, 0.5f, 1f, 1f, 1);
            Assert.AreEqual(5f, xp, Epsilon);
        }

        [Test]
        public void ComputePracticeXp_ZeroRelevance_ReturnsZero()
        {
            Assert.AreEqual(0f, ProfessionalXpEvaluator.ComputePracticeXp(10f, 0f, 1f, 1f, 1), Epsilon);
        }

        [Test]
        public void ComputePracticeXp_NonPositiveBase_ReturnsZero()
        {
            Assert.AreEqual(0f, ProfessionalXpEvaluator.ComputePracticeXp(0f, 1f, 1f, 1f, 1), Epsilon);
            Assert.AreEqual(0f, ProfessionalXpEvaluator.ComputePracticeXp(-5f, 1f, 1f, 1f, 1), Epsilon);
        }

        [Test]
        public void ComputePracticeXp_InvalidDifficultyOrQuality_DefaultsToOne()
        {
            // difficulty<=0 → 1；qualityMultiplier<=0 → 1
            Assert.AreEqual(10f, ProfessionalXpEvaluator.ComputePracticeXp(10f, 1f, 0f, 0f, 1), Epsilon);
            // relevance>1 钳到 1
            Assert.AreEqual(10f, ProfessionalXpEvaluator.ComputePracticeXp(10f, 5f, 1f, 1f, 1), Epsilon);
        }

        // ------------------------------------------------------------------
        // XP → Level（边际递减曲线）
        // ------------------------------------------------------------------

        [Test]
        public void LevelFromXp_ZeroOrNegative_ReturnsZero()
        {
            Assert.AreEqual(0, ProfessionalXpEvaluator.LevelFromXp(0f, 50, 5000f));
            Assert.AreEqual(0, ProfessionalXpEvaluator.LevelFromXp(-100f, 50, 5000f));
        }

        [Test]
        public void LevelFromXp_AtCap_ReturnsMaxLevel()
        {
            Assert.AreEqual(50, ProfessionalXpEvaluator.LevelFromXp(5000f, 50, 5000f));
            // 超出 cap 仍封顶 maxLevel
            Assert.AreEqual(50, ProfessionalXpEvaluator.LevelFromXp(10000f, 50, 5000f));
        }

        [Test]
        public void LevelFromXp_MarginalDiminishing_HalfCapBelowHalfLevel()
        {
            // 边际递减：xp=2500（cap 一半）时等级应显著低于 25
            int level = ProfessionalXpEvaluator.LevelFromXp(2500f, 50, 5000f);
            Assert.Greater(level, 0);
            Assert.Less(level, 25, "边际递减曲线：前半段经验不应达到满级一半");
        }

        [Test]
        public void LevelFromXp_StrictlyIncreasing()
        {
            int prev = -1;
            for (int xp = 0; xp <= 5000; xp += 250)
            {
                int level = ProfessionalXpEvaluator.LevelFromXp(xp, 50, 5000f);
                Assert.GreaterOrEqual(level, prev, "等级应随 XP 单调不减");
                prev = level;
            }
        }

        [Test]
        public void LevelFromXp_InvalidMaxLevel_ReturnsZero()
        {
            Assert.AreEqual(0, ProfessionalXpEvaluator.LevelFromXp(1000f, 0, 5000f));
            Assert.AreEqual(0, ProfessionalXpEvaluator.LevelFromXp(1000f, -10, 5000f));
            Assert.AreEqual(0, ProfessionalXpEvaluator.LevelFromXp(1000f, 50, 0f));
        }

        // ------------------------------------------------------------------
        // Level → Mastery
        // ------------------------------------------------------------------

        [Test]
        public void MasteryFromLevel_LinearNormalization()
        {
            Assert.AreEqual(0f, ProfessionalXpEvaluator.MasteryFromLevel(0, 50), Epsilon);
            Assert.AreEqual(100f, ProfessionalXpEvaluator.MasteryFromLevel(50, 50), Epsilon);
            Assert.AreEqual(20f, ProfessionalXpEvaluator.MasteryFromLevel(10, 50), Epsilon);
        }

        [Test]
        public void MasteryFromLevel_ClampedAtHundred()
        {
            Assert.AreEqual(100f, ProfessionalXpEvaluator.MasteryFromLevel(99, 50), Epsilon);
        }

        // ------------------------------------------------------------------
        // 能力 XP 拆分 SplitAbilityXp
        // ------------------------------------------------------------------

        private static List<AbilityXpWeight> PrecisionWeights()
        {
            // 与 Defs/ProfessionalSkills.xml Mapping_PrecisionComponents 一致
            return new List<AbilityXpWeight>
            {
                new AbilityXpWeight { abilityKey = "precisionControl", weight = 50f },
                new AbilityXpWeight { abilityKey = "processKnowledge", weight = 30f },
                new AbilityXpWeight { abilityKey = "machining", weight = 15f },
                new AbilityXpWeight { abilityKey = "qualityControl", weight = 5f },
            };
        }

        [Test]
        public void SplitAbilityXp_WeightedSplit_SumsToTotal()
        {
            // 除法后 float 误差约 1e-6 量级，用 1e-3 容差
            const double fpTolerance = 1e-3;
            var split = ProfessionalAbilityEvaluator.SplitAbilityXp(100f, PrecisionWeights());
            Assert.AreEqual(4, split.Count);
            Assert.AreEqual(50f, split["precisionControl"], fpTolerance);
            Assert.AreEqual(30f, split["processKnowledge"], fpTolerance);
            Assert.AreEqual(15f, split["machining"], fpTolerance);
            Assert.AreEqual(5f, split["qualityControl"], fpTolerance);
        }

        [Test]
        public void SplitAbilityXp_WeightsOverHundred_NormalizedProportionally()
        {
            // 权重和 >100（如 100/100），归一化后各得 50%
            const double fpTolerance = 1e-3;
            var weights = new List<AbilityXpWeight>
            {
                new AbilityXpWeight { abilityKey = "a", weight = 100f },
                new AbilityXpWeight { abilityKey = "b", weight = 100f },
            };
            var split = ProfessionalAbilityEvaluator.SplitAbilityXp(80f, weights);
            Assert.AreEqual(40f, split["a"], fpTolerance);
            Assert.AreEqual(40f, split["b"], fpTolerance);
        }

        [Test]
        public void SplitAbilityXp_NonPositiveTotal_ReturnsEmpty()
        {
            Assert.AreEqual(0, ProfessionalAbilityEvaluator.SplitAbilityXp(0f, PrecisionWeights()).Count);
            Assert.AreEqual(0, ProfessionalAbilityEvaluator.SplitAbilityXp(-5f, PrecisionWeights()).Count);
        }

        [Test]
        public void SplitAbilityXp_NullWeightsOrNoWeight_ReturnsEmpty()
        {
            Assert.AreEqual(0, ProfessionalAbilityEvaluator.SplitAbilityXp(100f, null).Count);
            Assert.AreEqual(0, ProfessionalAbilityEvaluator.SplitAbilityXp(100f, new List<AbilityXpWeight>()).Count);
            // 全 0 权重 → 不分配
            var zeros = new List<AbilityXpWeight>
            {
                new AbilityXpWeight { abilityKey = "a", weight = 0f },
            };
            Assert.AreEqual(0, ProfessionalAbilityEvaluator.SplitAbilityXp(100f, zeros).Count);
        }

        // ------------------------------------------------------------------
        // 方向评分 DirectionFit
        // ------------------------------------------------------------------

        private static DirectionFitInput MakeInput(
            Dictionary<string, float> skillLevels = null,
            int practiceCount = 0,
            float averageQuality = 0f,
            Dictionary<string, float> abilityShare = null,
            float averageMastery = 0f)
        {
            return new DirectionFitInput
            {
                SkillDefNames = new List<string> { "ProfessionalSkill_PrecisionManufacturing" },
                SkillLevels = skillLevels ?? new Dictionary<string, float>(),
                PracticeCount = practiceCount,
                AverageQuality = averageQuality,
                AbilityShare = abilityShare,
                AverageMastery = averageMastery,
                DirectionLabel = "Precision",
            };
        }

        [Test]
        public void DirectionFit_NullInput_AllZeroScore()
        {
            DirectionFit fit = ProfessionalDirectionEvaluator.Evaluate(null);
            Assert.AreEqual(0f, fit.Score, Epsilon);
            Assert.AreEqual(0f, fit.SkillFit, Epsilon);
            Assert.AreEqual(0f, fit.PracticeFit, Epsilon);
        }

        [Test]
        public void DirectionFit_NoData_ScoreZero()
        {
            DirectionFit fit = ProfessionalDirectionEvaluator.Evaluate(MakeInput());
            Assert.AreEqual(0f, fit.Score, Epsilon);
            Assert.AreEqual(0f, fit.SkillFit, Epsilon);
            Assert.AreEqual(0f, fit.PracticeFit, Epsilon);
        }

        [Test]
        public void DirectionFit_HighSkillLevel_DrivesSkillFit()
        {
            // Crafting=20 → skillBase=100 → skillFit=100
            var fit = ProfessionalDirectionEvaluator.Evaluate(
                MakeInput(skillLevels: new Dictionary<string, float> { { "Crafting", 20f } }));
            Assert.AreEqual(100f, fit.SkillFit, Epsilon);
            // score = 100*0.35 = 35
            Assert.AreEqual(35f, fit.Score, Epsilon);
        }

        [Test]
        public void DirectionFit_PracticeCount_DrivesPracticeFit()
        {
            // 320 次 = 基准 → 100
            var fit = ProfessionalDirectionEvaluator.Evaluate(
                MakeInput(practiceCount: 320));
            Assert.AreEqual(100f, fit.PracticeFit, Epsilon);
            // 160 次 → 50
            var fitHalf = ProfessionalDirectionEvaluator.Evaluate(
                MakeInput(practiceCount: 160));
            Assert.AreEqual(50f, fitHalf.PracticeFit, Epsilon);
            // 640 次封顶 100
            var fitOver = ProfessionalDirectionEvaluator.Evaluate(
                MakeInput(practiceCount: 640));
            Assert.AreEqual(100f, fitOver.PracticeFit, Epsilon);
        }

        [Test]
        public void DirectionFit_AbilityShare_DrivesContributionFit()
        {
            var fit = ProfessionalDirectionEvaluator.Evaluate(MakeInput(
                abilityShare: new Dictionary<string, float> { { "machining", 0.5f } }));
            Assert.AreEqual(50f, fit.ContributionFit, Epsilon);
        }

        [Test]
        public void DirectionFit_Mastery_DrivesMasteryFit()
        {
            var fit = ProfessionalDirectionEvaluator.Evaluate(MakeInput(averageMastery: 80f));
            Assert.AreEqual(80f, fit.MasteryFit, Epsilon);
        }

        [Test]
        public void DirectionFit_QualityClamped()
        {
            var fit = ProfessionalDirectionEvaluator.Evaluate(MakeInput(averageQuality: 150f));
            Assert.AreEqual(100f, fit.QualityFit, Epsilon);
        }

        [Test]
        public void DirectionFit_Weights_ComposeFullScore()
        {
            // 全满分：skill=100 practice=100 quality=100 contribution=100 mastery=100
            var fit = ProfessionalDirectionEvaluator.Evaluate(MakeInput(
                skillLevels: new Dictionary<string, float> { { "Crafting", 20f } },
                practiceCount: 320,
                averageQuality: 100f,
                abilityShare: new Dictionary<string, float> { { "machining", 1f } },
                averageMastery: 100f));
            Assert.AreEqual(100f, fit.Score, Epsilon);
        }

        [Test]
        public void DirectionFit_EvaluateAll_SortedDescending()
        {
            var low = MakeInput();
            var high = MakeInput(skillLevels: new Dictionary<string, float> { { "Crafting", 20f } });
            List<DirectionFit> list = ProfessionalDirectionEvaluator.EvaluateAll(new[] { low, high });
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(35f, list[0].Score, Epsilon, "高分方向应排第一");
            Assert.AreEqual(0f, list[1].Score, Epsilon);
        }

        [Test]
        public void DirectionFit_EvaluateAll_NullOrEmpty_ReturnsEmpty()
        {
            Assert.AreEqual(0, ProfessionalDirectionEvaluator.EvaluateAll(null).Count);
            Assert.AreEqual(0, ProfessionalDirectionEvaluator.EvaluateAll(new DirectionFitInput[0]).Count);
        }

        // ------------------------------------------------------------------
        // 状态容器 null 安全
        // ------------------------------------------------------------------

        [Test]
        public void ProfessionalState_GetSkill_MissingReturnsNull()
        {
            var state = new ProfessionalState();
            Assert.IsNull(state.GetSkill(null));
            Assert.IsNull(state.GetSkill(""));
            Assert.IsNull(state.GetSkill("UnknownSkill"));
        }
    }
}
