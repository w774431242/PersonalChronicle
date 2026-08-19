using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain.Profession;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// P4 专业评级纯逻辑测试（可离线）。
    /// 覆盖：ResolveRating 派生（阈值/封顶）、Resolver 接 Rating 权重（叠加）、
    ///       无 RatingDef 退回 P3 兼容。
    /// </summary>
    [TestFixture]
    public class ProfessionalRatingTests
    {
        private const double Epsilon = 1e-6;

        private static ProfessionalRatingDef MakeRating(string defName, int minLevel, float speedW, float qualityW, int order)
        {
            return new ProfessionalRatingDef
            {
                defName = defName,
                minLevel = minLevel,
                workSpeedWeight = speedW,
                qualityBiasWeight = qualityW,
                order = order,
            };
        }

        private static List<ProfessionalRatingDef> StandardRatings()
        {
            // order: 越高级越小（0=大师封顶）
            return new List<ProfessionalRatingDef>
            {
                MakeRating("Rating_Proficient", 10, 0.03f, 0.00f, 3),
                MakeRating("Rating_Specialist", 25, 0.05f, 0.02f, 2),
                MakeRating("Rating_Senior", 38, 0.08f, 0.04f, 1),
                MakeRating("Rating_Master", 45, 0.10f, 0.06f, 0),
            };
        }

        // ------------------------------------------------------------------
        // ResolveRating 派生
        // ------------------------------------------------------------------

        [Test]
        public void ResolveRating_LevelZero_ReturnsNull()
        {
            Assert.IsNull(ProfessionalRatingEvaluator.ResolveRating(0, StandardRatings()));
        }

        [Test]
        public void ResolveRating_BelowProficient_ReturnsNull()
        {
            Assert.IsNull(ProfessionalRatingEvaluator.ResolveRating(9, StandardRatings()));
        }

        [Test]
        public void ResolveRating_AtProficientThreshold_ReturnsProficient()
        {
            ProfessionalRatingDef r = ProfessionalRatingEvaluator.ResolveRating(10, StandardRatings());
            Assert.IsNotNull(r);
            Assert.AreEqual("Rating_Proficient", r.defName);
        }

        [Test]
        public void ResolveRating_AtSenior_ReturnsSeniorNotProficient()
        {
            // order 最小者优先（最高档）
            ProfessionalRatingDef r = ProfessionalRatingEvaluator.ResolveRating(40, StandardRatings());
            Assert.IsNotNull(r);
            Assert.AreEqual("Rating_Senior", r.defName);
        }

        [Test]
        public void ResolveRating_MaxLevel_ClampedAtMaster()
        {
            ProfessionalRatingDef r = ProfessionalRatingEvaluator.ResolveRating(50, StandardRatings());
            Assert.IsNotNull(r);
            Assert.AreEqual("Rating_Master", r.defName);
        }

        [Test]
        public void ResolveRating_NoRatingDefs_ReturnsNull()
        {
            Assert.IsNull(ProfessionalRatingEvaluator.ResolveRating(50, new List<ProfessionalRatingDef>()));
            Assert.IsNull(ProfessionalRatingEvaluator.ResolveRating(50, null));
        }

        // ------------------------------------------------------------------
        // Resolver 接 Rating 权重（叠加：value × (1 + ratingWeight)）
        // ------------------------------------------------------------------

        [Test]
        public void ResolveSpeedFactor_WithMasterRating_AppliesWeight()
        {
            // 技能 level=50 → 大师(weight=0.10)；effect value=0.03 → 0.03×(1+0.10)=0.033
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData>
            {
                new ProfessionalSkillData { skillDefName = "S1", level = 50 },
            };
            Dictionary<string, ProfessionalEffectDef> effects = new Dictionary<string, ProfessionalEffectDef>
            {
                { "E1", new ProfessionalEffectDef { defName = "E1", kind = ProfessionalEffectKind.WorkSpeed, value = 0.03f } },
            };
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                new ProfessionalSkillDef { defName = "S1", effectDefNames = new List<string> { "E1" }, practiceRecipeDefNames = new List<string>() },
            };
            // 1 + 0.033 = 1.033
            Assert.AreEqual(1.033f, ProfessionalEffectResolver.ResolveSpeedFactor(skills, skillDefs, effects, "Any", StandardRatings()), 1e-5);
        }

        [Test]
        public void ResolveSpeedFactor_NoRatingDefs_FallsBackToP3()
        {
            // ratingDefs=null → ratingWeight=0 → 仅 value=0.03 → 1.03
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData>
            {
                new ProfessionalSkillData { skillDefName = "S1", level = 50 },
            };
            Dictionary<string, ProfessionalEffectDef> effects = new Dictionary<string, ProfessionalEffectDef>
            {
                { "E1", new ProfessionalEffectDef { defName = "E1", kind = ProfessionalEffectKind.WorkSpeed, value = 0.03f } },
            };
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                new ProfessionalSkillDef { defName = "S1", effectDefNames = new List<string> { "E1" }, practiceRecipeDefNames = new List<string>() },
            };
            Assert.AreEqual(1.03f, ProfessionalEffectResolver.ResolveSpeedFactor(skills, skillDefs, effects, "Any", null), 1e-5);
            Assert.AreEqual(1.03f, ProfessionalEffectResolver.ResolveSpeedFactor(skills, skillDefs, effects, "Any"), 1e-5);
        }

        [Test]
        public void ResolveSpeedFactor_NoRatingLevelBelowThreshold_NoWeight()
        {
            // level=5 未达熟练 → 无评级 → 仅 value=0.03 → 1.03
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData>
            {
                new ProfessionalSkillData { skillDefName = "S1", level = 5 },
            };
            Dictionary<string, ProfessionalEffectDef> effects = new Dictionary<string, ProfessionalEffectDef>
            {
                { "E1", new ProfessionalEffectDef { defName = "E1", kind = ProfessionalEffectKind.WorkSpeed, value = 0.03f } },
            };
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                new ProfessionalSkillDef { defName = "S1", effectDefNames = new List<string> { "E1" }, practiceRecipeDefNames = new List<string>() },
            };
            Assert.AreEqual(1.03f, ProfessionalEffectResolver.ResolveSpeedFactor(skills, skillDefs, effects, "Any", StandardRatings()), 1e-5);
        }

        [Test]
        public void ResolveQualityLevels_WithSeniorRating_AppliesWeight()
        {
            // level=40 → 高级(qualityW=0.04)；effect value=1 → 1×(1+0.04)=1.04 → int=1
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData>
            {
                new ProfessionalSkillData { skillDefName = "S1", level = 40 },
            };
            Dictionary<string, ProfessionalEffectDef> effects = new Dictionary<string, ProfessionalEffectDef>
            {
                { "E1", new ProfessionalEffectDef { defName = "E1", kind = ProfessionalEffectKind.QualityBias, value = 1f } },
            };
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                new ProfessionalSkillDef { defName = "S1", effectDefNames = new List<string> { "E1" }, practiceRecipeDefNames = new List<string>() },
            };
            Assert.AreEqual(1, ProfessionalEffectResolver.ResolveQualityLevels(skills, skillDefs, effects, "Any", StandardRatings()));
        }

        [Test]
        public void ResolveQualityLevels_WithMasterRating_QualityWeightDoubles()
        {
            // level=50 → 大师(qualityW=0.06)；effect value=1 → 1×1.06=1.06 → int=1（仍 1 档）
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData>
            {
                new ProfessionalSkillData { skillDefName = "S1", level = 50 },
            };
            Dictionary<string, ProfessionalEffectDef> effects = new Dictionary<string, ProfessionalEffectDef>
            {
                { "E1", new ProfessionalEffectDef { defName = "E1", kind = ProfessionalEffectKind.QualityBias, value = 1f } },
            };
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                new ProfessionalSkillDef { defName = "S1", effectDefNames = new List<string> { "E1" }, practiceRecipeDefNames = new List<string>() },
            };
            Assert.AreEqual(1, ProfessionalEffectResolver.ResolveQualityLevels(skills, skillDefs, effects, "Any", StandardRatings()));
        }

        [Test]
        public void ResolveQualityLevels_NoRatingDefs_FallsBackToP3()
        {
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData>
            {
                new ProfessionalSkillData { skillDefName = "S1", level = 50 },
            };
            Dictionary<string, ProfessionalEffectDef> effects = new Dictionary<string, ProfessionalEffectDef>
            {
                { "E1", new ProfessionalEffectDef { defName = "E1", kind = ProfessionalEffectKind.QualityBias, value = 1f } },
            };
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                new ProfessionalSkillDef { defName = "S1", effectDefNames = new List<string> { "E1" }, practiceRecipeDefNames = new List<string>() },
            };
            Assert.AreEqual(1, ProfessionalEffectResolver.ResolveQualityLevels(skills, skillDefs, effects, "Any", null));
        }
    }
}
