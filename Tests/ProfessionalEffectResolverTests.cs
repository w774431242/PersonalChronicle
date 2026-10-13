using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain.Profession;
using RimWorld;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// P3 专业效果解析器纯逻辑测试（可离线）。
    /// 覆盖：ResolveSpeedFactor / ResolveQualityLevels / ClampQuality / RecipeMatches。
    /// 仅测纯函数；ProfessionalSkillDef 等依赖 Verse Def 的类离线无法实例化，用最小数据构造。
    /// </summary>
    [TestFixture]
    public class ProfessionalEffectResolverTests
    {
        private const double Epsilon = 1e-6;

        private static ProfessionalEffectDef MakeEffect(string defName, ProfessionalEffectKind kind, float value)
        {
            return new ProfessionalEffectDef
            {
                defName = defName,
                kind = kind,
                value = value,
            };
        }

        private static ProfessionalSkillDef MakeSkill(string defName, string effectDefName, List<string> recipes)
        {
            return new ProfessionalSkillDef
            {
                defName = defName,
                effectDefNames = new List<string> { effectDefName },
                practiceRecipeDefNames = recipes,
            };
        }

        private static Dictionary<string, ProfessionalEffectDef> EffectIndex(params ProfessionalEffectDef[] defs)
        {
            Dictionary<string, ProfessionalEffectDef> map = new Dictionary<string, ProfessionalEffectDef>();
            foreach (ProfessionalEffectDef d in defs)
            {
                map[d.defName] = d;
            }
            return map;
        }

        private static ProfessionalSkillData SkillWithLevel(string defName, int level)
        {
            return new ProfessionalSkillData { skillDefName = defName, level = level };
        }

        // ------------------------------------------------------------------
        // ResolveSpeedFactor
        // ------------------------------------------------------------------

        [Test]
        public void ResolveSpeedFactor_NoSkill_ReturnsOne()
        {
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData>();
            Dictionary<string, ProfessionalEffectDef> effects = EffectIndex(MakeEffect("E1", ProfessionalEffectKind.WorkSpeed, 0.03f));
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                MakeSkill("S1", "E1", new List<string> { "Make_A" }),
            };
            Assert.AreEqual(1f, ProfessionalEffectResolver.ResolveSpeedFactor(skills, skillDefs, effects, "Make_A"), Epsilon);
        }

        [Test]
        public void ResolveSpeedFactor_SkillLevelZero_ReturnsOne()
        {
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData> { SkillWithLevel("S1", 0) };
            Dictionary<string, ProfessionalEffectDef> effects = EffectIndex(MakeEffect("E1", ProfessionalEffectKind.WorkSpeed, 0.03f));
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                MakeSkill("S1", "E1", new List<string> { "Make_A" }),
            };
            Assert.AreEqual(1f, ProfessionalEffectResolver.ResolveSpeedFactor(skills, skillDefs, effects, "Make_A"), Epsilon);
        }

        [Test]
        public void ResolveSpeedFactor_HitRecipe_AppliesBonus()
        {
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData> { SkillWithLevel("S1", 5) };
            Dictionary<string, ProfessionalEffectDef> effects = EffectIndex(MakeEffect("E1", ProfessionalEffectKind.WorkSpeed, 0.03f));
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                MakeSkill("S1", "E1", new List<string> { "Make_A" }),
            };
            Assert.AreEqual(1f + ProfessionalEffectResolver.ScaleByLevel(0.03f, 5, 50, 0f), ProfessionalEffectResolver.ResolveSpeedFactor(skills, skillDefs, effects, "Make_A"), Epsilon);
        }

        [Test]
        public void ResolveSpeedFactor_MissRecipe_ReturnsOne()
        {
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData> { SkillWithLevel("S1", 5) };
            Dictionary<string, ProfessionalEffectDef> effects = EffectIndex(MakeEffect("E1", ProfessionalEffectKind.WorkSpeed, 0.03f));
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                MakeSkill("S1", "E1", new List<string> { "Make_A" }),
            };
            // recipe 不在白名单 → 零影响
            Assert.AreEqual(1f, ProfessionalEffectResolver.ResolveSpeedFactor(skills, skillDefs, effects, "Make_B"), Epsilon);
        }

        [Test]
        public void ResolveSpeedFactor_NullRecipe_NoContext_ReturnsOne()
        {
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData> { SkillWithLevel("S1", 5) };
            Dictionary<string, ProfessionalEffectDef> effects = EffectIndex(MakeEffect("E1", ProfessionalEffectKind.WorkSpeed, 0.03f));
            // 技能白名单非空时，recipe 为 null → 保守不加成
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                MakeSkill("S1", "E1", new List<string> { "Make_A" }),
            };
            Assert.AreEqual(1f, ProfessionalEffectResolver.ResolveSpeedFactor(skills, skillDefs, effects, null), Epsilon);
        }

        [Test]
        public void ResolveSpeedFactor_EmptyRecipeWhitelist_AllRelevant()
        {
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData> { SkillWithLevel("S1", 5) };
            Dictionary<string, ProfessionalEffectDef> effects = EffectIndex(MakeEffect("E1", ProfessionalEffectKind.WorkSpeed, 0.03f));
            // 空白名单 = 全部相关
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                MakeSkill("S1", "E1", new List<string>()),
            };
            Assert.AreEqual(1f + ProfessionalEffectResolver.ScaleByLevel(0.03f, 5, 50, 0f), ProfessionalEffectResolver.ResolveSpeedFactor(skills, skillDefs, effects, "AnyRecipe"), Epsilon);
        }

        [Test]
        public void ResolveSpeedFactor_MultiSkill_Additive()
        {
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData>
            {
                SkillWithLevel("S1", 5),
                SkillWithLevel("S2", 3),
            };
            Dictionary<string, ProfessionalEffectDef> effects = EffectIndex(
                MakeEffect("E1", ProfessionalEffectKind.WorkSpeed, 0.03f),
                MakeEffect("E2", ProfessionalEffectKind.WorkSpeed, 0.02f));
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                MakeSkill("S1", "E1", new List<string>()),
                MakeSkill("S2", "E2", new List<string>()),
            };
            // lv5/50 × 0.03 + lv3/50 × 0.02 = 0.003 + 0.0012 → 1.0042
            Assert.AreEqual(1f + ProfessionalEffectResolver.ScaleByLevel(0.03f, 5, 50, 0f) + ProfessionalEffectResolver.ScaleByLevel(0.02f, 3, 50, 0f), ProfessionalEffectResolver.ResolveSpeedFactor(skills, skillDefs, effects, "AnyRecipe"), Epsilon);
        }

        // ------------------------------------------------------------------
        // 差异化特化点（2026-08-19）：技能级效果覆盖 effectOverrides
        // ------------------------------------------------------------------

        private static ProfessionalSkillDef MakeSkillWithOverride(string defName, string effectDefName, List<string> recipes, EffectOverride ov)
        {
            ProfessionalSkillDef skill = MakeSkill(defName, effectDefName, recipes);
            if (ov != null)
            {
                skill.effectOverrides = new List<EffectOverride> { ov };
            }
            return skill;
        }

        private static List<ProfessionalRatingDef> MasterRating()
        {
            return new List<ProfessionalRatingDef>
            {
                new ProfessionalRatingDef { defName = "R_Master", minLevel = 45, workSpeedWeight = 0.10f, qualityBiasWeight = 0.06f, order = 0 }
            };
        }

        [Test]
        public void ResolveSpeedFactor_EffectOverride_ReplacesSharedValue()
        {
            // 共享 E1=0.03，S1 覆盖为 0.05 → 该技能 +5%（其他引用 E1 的技能不受影响）
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData> { SkillWithLevel("S1", 5) };
            Dictionary<string, ProfessionalEffectDef> effects = EffectIndex(MakeEffect("E1", ProfessionalEffectKind.WorkSpeed, 0.03f));
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                MakeSkillWithOverride("S1", "E1", new List<string>(), new EffectOverride { effectDefName = "E1", hasValue = true, value = 0.05f }),
            };
            Assert.AreEqual(1f + ProfessionalEffectResolver.ScaleByLevel(0.05f, 5, 50, 0f), ProfessionalEffectResolver.ResolveSpeedFactor(skills, skillDefs, effects, "AnyRecipe"), Epsilon);
        }

        [Test]
        public void ResolveSpeedFactor_EffectOverride_RatingScaleScalesWeight()
        {
            // 大师评级速度权重 0.10 × scale 0.5 = 0.05 → 0.03 × (1+0.05) = 0.0315
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData> { SkillWithLevel("S1", 50) };
            Dictionary<string, ProfessionalEffectDef> effects = EffectIndex(MakeEffect("E1", ProfessionalEffectKind.WorkSpeed, 0.03f));
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                MakeSkillWithOverride("S1", "E1", new List<string>(), new EffectOverride { effectDefName = "E1", ratingWeightScale = 0.5f }),
            };
            Assert.AreEqual(1.0315f, ProfessionalEffectResolver.ResolveSpeedFactor(skills, skillDefs, effects, "AnyRecipe", MasterRating()), 1e-4);
        }

        [Test]
        public void ResolveSpeedFactor_EffectOverride_ZeroScaleDisablesRatingBonus()
        {
            // ratingWeightScale=0 → 该效果不随评级加成（仍保留共享基础值 0.03）
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData> { SkillWithLevel("S1", 50) };
            Dictionary<string, ProfessionalEffectDef> effects = EffectIndex(MakeEffect("E1", ProfessionalEffectKind.WorkSpeed, 0.03f));
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                MakeSkillWithOverride("S1", "E1", new List<string>(), new EffectOverride { effectDefName = "E1", ratingWeightScale = 0f }),
            };
            Assert.AreEqual(1.03f, ProfessionalEffectResolver.ResolveSpeedFactor(skills, skillDefs, effects, "AnyRecipe", MasterRating()), Epsilon);
        }

        [Test]
        public void ResolveSpeedFactor_OverrideForOtherEffect_DoesNotAffectThisOne()
        {
            // 覆盖项指向其他效果（E2）→ E1 仍用共享值（回退路径）
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData> { SkillWithLevel("S1", 5) };
            Dictionary<string, ProfessionalEffectDef> effects = EffectIndex(MakeEffect("E1", ProfessionalEffectKind.WorkSpeed, 0.03f));
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                MakeSkillWithOverride("S1", "E1", new List<string>(), new EffectOverride { effectDefName = "E2", hasValue = true, value = 0.09f }),
            };
            Assert.AreEqual(1f + ProfessionalEffectResolver.ScaleByLevel(0.03f, 5, 50, 0f), ProfessionalEffectResolver.ResolveSpeedFactor(skills, skillDefs, effects, "AnyRecipe"), Epsilon);
        }

        [Test]
        public void ResolveQualityLevels_EffectOverride_ReplacesSharedValue()
        {
            // 共享 E1=1 档，S1 覆盖为 0（无品质加成）→ 方向差异化：只速度不品质
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData> { SkillWithLevel("S1", 5) };
            Dictionary<string, ProfessionalEffectDef> effects = EffectIndex(MakeEffect("E1", ProfessionalEffectKind.QualityBias, 1f));
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                MakeSkillWithOverride("S1", "E1", new List<string>(), new EffectOverride { effectDefName = "E1", hasValue = true, value = 0f }),
            };
            Assert.AreEqual(0, ProfessionalEffectResolver.ResolveQualityLevels(skills, skillDefs, effects, "AnyRecipe"));
        }

        // ------------------------------------------------------------------
        // ResolveQualityLevels
        // ------------------------------------------------------------------

        [Test]
        public void ResolveQualityLevels_HitRecipe_ReturnsOneLevel()
        {
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData> { SkillWithLevel("S1", 5) };
            Dictionary<string, ProfessionalEffectDef> effects = EffectIndex(MakeEffect("E1", ProfessionalEffectKind.QualityBias, 1f));
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                MakeSkill("S1", "E1", new List<string> { "Make_A" }),
            };
            Assert.AreEqual(1, ProfessionalEffectResolver.ResolveQualityLevels(skills, skillDefs, effects, "Make_A"));
        }

        [Test]
        public void ResolveQualityLevels_NoSkillOrMiss_ReturnsZero()
        {
            Dictionary<string, ProfessionalEffectDef> effects = EffectIndex(MakeEffect("E1", ProfessionalEffectKind.QualityBias, 1f));
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef>
            {
                MakeSkill("S1", "E1", new List<string> { "Make_A" }),
            };
            // 无技能
            Assert.AreEqual(0, ProfessionalEffectResolver.ResolveQualityLevels(
                new List<ProfessionalSkillData>(), skillDefs, effects, "Make_A"));
            // recipe 未命中
            Assert.AreEqual(0, ProfessionalEffectResolver.ResolveQualityLevels(
                new List<ProfessionalSkillData> { SkillWithLevel("S1", 5) }, skillDefs, effects, "Make_B"));
        }

        // ------------------------------------------------------------------
        // ClampQuality（2026-08-19 验收 P2-5：签名改 int 索引，Domain 去 RimWorld 依赖）
        // ------------------------------------------------------------------

        [Test]
        public void ClampQuality_NormalPlusOne_Good()
        {
            Assert.AreEqual((int)QualityCategory.Good, ProfessionalEffectResolver.ClampQuality((int)QualityCategory.Normal, 1));
        }

        [Test]
        public void ClampQuality_LegendaryPlusTwo_Clamped()
        {
            Assert.AreEqual((int)QualityCategory.Legendary, ProfessionalEffectResolver.ClampQuality((int)QualityCategory.Legendary, 2));
        }

        [Test]
        public void ClampQuality_AwfulMinusOne_Clamped()
        {
            Assert.AreEqual((int)QualityCategory.Awful, ProfessionalEffectResolver.ClampQuality((int)QualityCategory.Awful, -1));
        }

        [Test]
        public void ClampQuality_MasterworkMinusTwo_Good()
        {
            Assert.AreEqual((int)QualityCategory.Good, ProfessionalEffectResolver.ClampQuality((int)QualityCategory.Masterwork, -2));
        }

        // ------------------------------------------------------------------
        // RecipeMatches
        // ------------------------------------------------------------------

        [Test]
        public void RecipeMatches_EmptyWhitelist_AllRelevant()
        {
            ProfessionalSkillDef skill = MakeSkill("S1", "E1", new List<string>());
            Assert.IsTrue(ProfessionalEffectResolver.RecipeMatches(skill, "AnyRecipe", null));
            Assert.IsTrue(ProfessionalEffectResolver.RecipeMatches(skill, null, null));
        }

        [Test]
        public void RecipeMatches_NullRecipeWithWhitelist_False()
        {
            ProfessionalSkillDef skill = MakeSkill("S1", "E1", new List<string> { "Make_A" });
            Assert.IsFalse(ProfessionalEffectResolver.RecipeMatches(skill, null, null));
        }

        [Test]
        public void RecipeMatches_WorkTypeWhitelist_MatchesWorkType()
        {
            ProfessionalSkillDef skill = MakeSkill("S1", "E1", new List<string>());
            skill.practiceWorkTypeDefNames = new System.Collections.Generic.List<string> { "Smithing" };
            // 双白名单皆空才全相关；有 WorkType 白名单时按 workType 判定
            Assert.IsFalse(ProfessionalEffectResolver.RecipeMatches(skill, "AnyRecipe", null));
            Assert.IsTrue(ProfessionalEffectResolver.RecipeMatches(skill, null, "Smithing"));
            Assert.IsFalse(ProfessionalEffectResolver.RecipeMatches(skill, null, "Crafting"));
        }

        [Test]
        public void ResolveContributions_IncludesStatBonusWhenNoWorkSpeed()
        {
            ProfessionalEffectDef effect = new ProfessionalEffectDef
            {
                defName = "E1",
                kind = ProfessionalEffectKind.StatBonus,
                value = 0.05f,
                statDefName = "ConstructionSpeed"
            };
            ProfessionalSkillDef skill = MakeSkill("S1", "E1", new List<string>());
            skill.practiceWorkTypeDefNames = new List<string> { "Construction" };
            List<ProfessionalEffectResolver.SkillBonusContribution> list =
                ProfessionalEffectResolver.ResolveContributions(
                    new List<ProfessionalSkillData> { SkillWithLevel("S1", 8) },
                    new List<ProfessionalSkillDef> { skill },
                    EffectIndex(effect),
                    null,
                    null);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(ProfessionalEffectResolver.ScaleByLevel(0.05f, 8, 50, 0f), list[0].GenericBonus, Epsilon);
            Assert.AreEqual("ConstructionSpeed", list[0].GenericStatDefName);
            Assert.IsTrue(list[0].ActiveNow);
        }

        [Test]
        public void ResolveStatBonus_NullRecipe_AppliesWorkTypeWhitelistedSkill()
        {
            // 通用 Stat 评估没有制造上下文；带 practiceWorkTypeDefNames 的技能仍应注入 pawn 面板。
            ProfessionalEffectDef effect = new ProfessionalEffectDef
            {
                defName = "E1",
                kind = ProfessionalEffectKind.StatBonus,
                value = 0.05f,
                statDefName = "ConstructionSpeed"
            };
            ProfessionalSkillDef skill = MakeSkill("S1", "E1", new List<string>());
            skill.practiceWorkTypeDefNames = new List<string> { "Construction" };
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData> { SkillWithLevel("S1", 10) };
            float factor = ProfessionalEffectResolver.ResolveStatBonus(
                skills,
                new List<ProfessionalSkillDef> { skill },
                EffectIndex(effect),
                ProfessionalEffectKind.StatBonus,
                "ConstructionSpeed",
                null);
            Assert.AreEqual(1f + ProfessionalEffectResolver.ScaleByLevel(0.05f, 10, 50, 0f), factor, Epsilon);
        }

        [Test]
        public void ResolveStatBonus_GrowsWithLevel()
        {
            ProfessionalEffectDef effect = new ProfessionalEffectDef
            {
                defName = "E1",
                kind = ProfessionalEffectKind.StatBonus,
                value = 0.10f,
                statDefName = "MiningSpeed"
            };
            ProfessionalSkillDef skill = MakeSkill("S1", "E1", new List<string>());
            Dictionary<string, ProfessionalEffectDef> effects = EffectIndex(effect);
            List<ProfessionalSkillDef> skillDefs = new List<ProfessionalSkillDef> { skill };
            float low = ProfessionalEffectResolver.ResolveStatBonus(
                new List<ProfessionalSkillData> { SkillWithLevel("S1", 10) },
                skillDefs, effects, ProfessionalEffectKind.StatBonus, "MiningSpeed", null);
            float high = ProfessionalEffectResolver.ResolveStatBonus(
                new List<ProfessionalSkillData> { SkillWithLevel("S1", 50) },
                skillDefs, effects, ProfessionalEffectKind.StatBonus, "MiningSpeed", null);
            Assert.AreEqual(1f + ProfessionalEffectResolver.ScaleByLevel(0.10f, 10, 50, 0f), low, Epsilon);
            Assert.AreEqual(1f + ProfessionalEffectResolver.ScaleByLevel(0.10f, 50, 50, 0f), high, Epsilon);
        }

        [Test]
        public void ScaleByLevel_CapsAtMaxLevel()
        {
            Assert.AreEqual(0.10f, ProfessionalEffectResolver.ScaleByLevel(0.10f, 50, 50, 0f), Epsilon);
            Assert.AreEqual(0.10f, ProfessionalEffectResolver.ScaleByLevel(0.10f, 80, 50, 0f), Epsilon);
            Assert.AreEqual(0f, ProfessionalEffectResolver.ScaleByLevel(0.10f, 0, 50, 0f), Epsilon);
            Assert.AreEqual(0.11f, ProfessionalEffectResolver.ScaleByLevel(0.10f, 50, 50, 0.10f), Epsilon);
        }
    }
}
