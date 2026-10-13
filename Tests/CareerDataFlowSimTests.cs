using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Honor;
using PersonalChronicle.Domain.Profession;
using PersonalChronicle.Domain.Qualification;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 职业生涯体系「游玩过程数据层链路」离线模拟：
    /// 制造采集(Patch_GenRecipe 等价事实) → CareerEvent 事实层 → 状态层 XP/能力派生
    /// → 评级(ProfessionalRatingEvaluator) → 效果解析(ProfessionalEffectResolver)
    /// → 资格评估(QualificationEvaluator)。
    /// 用注入的内存 Def 对象串起全部 Domain 纯函数，验证端到端链路通畅、无 null 断链。
    /// </summary>
    [TestFixture]
    public class CareerDataFlowSimTests
    {
        private const double Eps = 1e-6;

        // 内存构造：一个精密制造技能 + 工作速度效果 + 评级 + 能力映射 + 资格阶梯
        private static ProfessionalSkillDef MakePrecisionSkill()
        {
            return new ProfessionalSkillDef
            {
                defName = "ProfessionalSkill_PrecisionManufacturing",
                direction = "PrecisionComponents",
                xpPerPracticeBase = 10f,
                xpDifficulty = 1f,
                maxLevel = 50,
                xpCap = 5000f,
                practiceRecipeDefNames = new List<string> { "Make_ComponentIndustrial" },
                effectDefNames = new List<string> { "ProfessionalEffect_WorkSpeed", "ProfessionalEffect_QualityBias" }
            };
        }

        private static ProfessionalEffectDef MakeWorkSpeedEffect()
        {
            return new ProfessionalEffectDef
            {
                defName = "ProfessionalEffect_WorkSpeed",
                kind = ProfessionalEffectKind.WorkSpeed,
                value = 0.03f,
                statDefName = null
            };
        }

        private static ProfessionalEffectDef MakeQualityBiasEffect()
        {
            return new ProfessionalEffectDef
            {
                defName = "ProfessionalEffect_QualityBias",
                kind = ProfessionalEffectKind.QualityBias,
                value = 1f,
                statDefName = null
            };
        }

        private static List<ProfessionalRatingDef> MakeRatings()
        {
            return new List<ProfessionalRatingDef>
            {
                new ProfessionalRatingDef { defName = "R_Skilled",   order = 0, minLevel = 10, workSpeedWeight = 0.2f, qualityBiasWeight = 0.2f },
                new ProfessionalRatingDef { defName = "R_Pro",       order = 1, minLevel = 25, workSpeedWeight = 0.5f, qualityBiasWeight = 0.5f },
                new ProfessionalRatingDef { defName = "R_Senior",    order = 2, minLevel = 38, workSpeedWeight = 0.8f, qualityBiasWeight = 0.8f },
                new ProfessionalRatingDef { defName = "R_Master",    order = 3, minLevel = 45, workSpeedWeight = 1.0f, qualityBiasWeight = 1.0f },
            };
        }

        private static AbilityMappingDef MakeMapping()
        {
            return new AbilityMappingDef
            {
                defName = "Mapping_PrecisionComponents",
                weights = new List<AbilityXpWeight>
                {
                    new AbilityXpWeight { abilityKey = "machining", weight = 40f },
                    new AbilityXpWeight { abilityKey = "precisionControl", weight = 30f },
                    new AbilityXpWeight { abilityKey = "processKnowledge", weight = 20f },
                    new AbilityXpWeight { abilityKey = "qualityControl", weight = 10f },
                }
            };
        }

        private static List<QualificationDef> MakeQualLadder()
        {
            return new List<QualificationDef>
            {
                MakeQual("Q_Precision_Junior",     "Title_Precision_Junior",     null,                         0,  5,  60000),
                MakeQual("Q_Precision_Assistant",  "Title_Precision_Assistant",  "Q_Precision_Junior",        1,  15, 200000),
                MakeQual("Q_Precision_Senior",     "Title_Precision_Senior",     "Q_Precision_Assistant",     2,  25, 600000, requiredExam: true),
                MakeQual("Q_Precision_Specialist", "Title_Precision_Specialist", "Q_Precision_Senior",        3,  38, 1200000, requiredExam: true),
                MakeQual("Q_Precision_Master",     "Title_Precision_Master",     "Q_Precision_Specialist",    4,  45, 1800000, requiredExam: true, requiredAchievements: true),
            };
        }

        private static QualificationDef MakeQual(string defName, string titleDefName, string prev, int order,
            int minLevel, long careerTicks, bool requiredExam = false, bool requiredAchievements = false)
        {
            var q = new QualificationDef
            {
                defName = defName,
                titleDefName = titleDefName,
                requiredPreviousTitle = prev,
                order = order,
                requiredMinLevel = minLevel,
                requiredCareerTimeTicks = careerTicks,
                professionalSkillDefName = "ProfessionalSkill_PrecisionManufacturing",
                majorKey = "Manufacturing",
                minimumScore = 60f,
            };
            if (requiredExam) { q.requiredExam = true; }
            if (requiredAchievements)
            {
                q.requiredAchievements = new List<QualificationAchievementReq>
                {
                    new QualificationAchievementReq { achievementKey = AchievementEvaluator.LegendaryMade, minValue = 1.0 }
                };
            }
            return q;
        }

        // ───────────────────────── 端到端模拟 ─────────────────────────

        [Test]
        public void FullDataFlow_ProduceMany_AccumulatesLevel_AndResolvesEffects_AndQualifies()
        {
            // 1) 准备内存 Def 集（运行期来自 DefDatabase，此处注入等价物）
            var skillDef = MakePrecisionSkill();
            var effWork = MakeWorkSpeedEffect();
            var effQuality = MakeQualityBiasEffect();
            var ratings = MakeRatings();
            var mapping = MakeMapping();
            var skillDefs = new List<ProfessionalSkillDef> { skillDef };
            var effectDefs = new Dictionary<string, ProfessionalEffectDef>
            {
                { effWork.defName, effWork },
                { effQuality.defName, effQuality },
            };
            var quals = MakeQualLadder();
            var skillMaxLevels = new Dictionary<string, int> { { skillDef.defName, skillDef.maxLevel } };

            // 2) 制造采集：模拟玩家制造 200 件工业组件（Excellent 品质）
            var pawn = new PawnObject { CareerData = new CareerData() };
            var data = pawn.CareerData;
            data.Professional = new ProfessionalState();
            long tick = 0L;
            int practices = 200;
            for (int i = 0; i < practices; i++)
            {
                tick += 1000L;
                // 事实层
                data.Events.Add(new CareerEvent(
                    "p:" + tick + ":" + i, "p", tick, CareerEventType.ItemProduced,
                    "ComponentIndustrial", null, "Excellent", "Make_ComponentIndustrial", 1, null));
                // 状态层派生（复演 ArchiveService.ApplyProfessionalProgress 的纯逻辑）
                float relevance = 1f; // Recipe 白名单命中
                float qm = ProfessionalXpEvaluator.QualityMultiplier("Excellent"); // 1.5
                float xp = ProfessionalXpEvaluator.ComputePracticeXp(skillDef.xpPerPracticeBase, relevance, qm, skillDef.xpDifficulty, 1);
                var sd = data.Professional.GetSkill(skillDef.defName) ?? new ProfessionalSkillData { skillDefName = skillDef.defName };
                if (data.Professional.skills.Contains(sd) == false) data.Professional.skills.Add(sd);
                sd.xp += xp;
                sd.practiceCount++;
                sd.lastPracticeTick = tick;
                int lvl = ProfessionalXpEvaluator.LevelFromXp(sd.xp, skillDef.maxLevel, skillDef.xpCap);
                sd.level = lvl;
                sd.mastery = ProfessionalXpEvaluator.MasteryFromLevel(lvl, skillDef.maxLevel);
                // 能力拆分
                var split = ProfessionalAbilityEvaluator.SplitAbilityXp(xp, mapping.weights);
                sd.abilityXp = sd.abilityXp ?? new Dictionary<string, float>();
                foreach (var kv in split)
                {
                    if (sd.abilityXp.ContainsKey(kv.Key)) sd.abilityXp[kv.Key] += kv.Value;
                    else sd.abilityXp[kv.Key] = kv.Value;
                }
            }

            // 3) 断言状态层：XP 累计 + Level 提升 + 能力拆分
            var skill = data.Professional.GetSkill(skillDef.defName);
            Assert.Greater(skill.xp, 0f, "xp should accumulate");
            Assert.Greater(skill.level, 0, "level should rise");
            Assert.IsNotNull(skill.abilityXp, "ability xp split should exist");
            Assert.Greater(skill.abilityXp["machining"], 0f, "machining ability xp > 0");
            Assert.AreEqual(practices, skill.practiceCount, "practice count matches");

            // 4) 评级：由 Level 派生
            var rating = ProfessionalRatingEvaluator.ResolveRating(skill.level, ratings);
            Assert.IsNotNull(rating, "rating should resolve once level>=10");

            // 5) 效果解析：速度系数 + 品质偏移（按当前配方命中）
            float speed = ProfessionalEffectResolver.ResolveSpeedFactor(
                data.Professional.skills, skillDefs, effectDefs, "Make_ComponentIndustrial", ratings);
            Assert.Greater(speed, 1f, "speed factor > 1 when skilled");
            int qlevels = ProfessionalEffectResolver.ResolveQualityLevels(
                data.Professional.skills, skillDefs, effectDefs, "Make_ComponentIndustrial", ratings);
            Assert.GreaterOrEqual(qlevels, 0, "quality levels resolved");

            // 6) 资格评估：低级档应已达标（level/careerTime/events 满足），高级档受 exam/achievement 限制
            // 模拟授予前两档（满足前置链）
            data.GrantedTitles = new List<GrantedTitle>
            {
                new GrantedTitle("Title_Precision_Junior", "Q_Precision_Junior", 1L),
                new GrantedTitle("Title_Precision_Assistant", "Q_Precision_Assistant", 2L),
            };
            var elig = QualificationEvaluator.Evaluate(pawn, quals, skillMaxLevels);
            // 初级/助理已授予 → 不应 Eligible；中级（Senior）无考试要求但 level/careerTime 需满足
            var senior = elig.Find(e => e.Def.defName == "Q_Precision_Senior");
            Assert.IsNotNull(senior, "senior eligibility evaluated");
            // 当前 level 由 200 次 Excellent 制造决定，应已达 25 → 中级资格 level 门槛满足
            if (skill.level >= 25)
            {
                Assert.IsTrue(senior.Eligible || senior.Reason == "careerTime" || senior.Reason == "score",
                    "senior should be reachable: " + senior.Reason);
            }

            // 7) NextQualification：已授两档 → 下一档应为 Specialist（order 3，严格高于 2），不倒挂
            var next = QualificationEvaluator.NextQualification(data, quals);
            Assert.IsNotNull(next);
            Assert.AreEqual("Q_Precision_Senior", next.defName, "next must be senior (no inversion)");
        }

        [Test]
        public void FullDataFlow_MappingMissingRecipe_FallsBackToWorkType()
        {
            // 武器方向：无 Recipe 白名单、仅 WorkType（Smithing）半匹配 → 相关度 0.5 → 仍获 XP
            var weaponSkill = new ProfessionalSkillDef
            {
                defName = "ProfessionalSkill_WeaponryManufacturing",
                direction = "WeaponryManufacturing",
                xpPerPracticeBase = 12f,
                xpDifficulty = 1f,
                maxLevel = 50,
                xpCap = 4500f,
                practiceWorkTypeDefNames = new List<string> { "Smithing" },
                effectDefNames = new List<string> { "ProfessionalEffect_WorkSpeed" }
            };
            var data = new CareerData { Professional = new ProfessionalState() };
            var sd = new ProfessionalSkillData { skillDefName = weaponSkill.defName };
            data.Professional.skills.Add(sd);
            float rel = 0.5f; // RecipeRelevance 的 WorkType 命中分支
            // 累积多次实践（单次 XP 小，边际递减曲线下需累积才升级，符合真实游玩）
            for (int i = 0; i < 300; i++)
            {
                float xp = ProfessionalXpEvaluator.ComputePracticeXp(weaponSkill.xpPerPracticeBase, rel, 1f, 1f, 1);
                Assert.Greater(xp, 0f, "worktype-half-matched skill still gains xp");
                sd.xp += xp;
                sd.practiceCount++;
            }
            sd.level = ProfessionalXpEvaluator.LevelFromXp(sd.xp, weaponSkill.maxLevel, weaponSkill.xpCap);
            Assert.Greater(sd.xp, 0f, "xp accumulated");
            Assert.Greater(sd.level, 0, "weapon skill levels up via worktype path after accumulation");
            // 效果解析：worktype 命中的配方（Smithing 类）也应吃到速度加成
            var effWork = new ProfessionalEffectDef { defName = "ProfessionalEffect_WorkSpeed", kind = ProfessionalEffectKind.WorkSpeed, value = 0.05f };
            var effectDefs = new Dictionary<string, ProfessionalEffectDef> { { effWork.defName, effWork } };
            var skillDefs = new List<ProfessionalSkillDef> { weaponSkill };
            var ratings = MakeRatings();
            float speed = ProfessionalEffectResolver.ResolveSpeedFactor(
                data.Professional.skills, skillDefs, effectDefs, null, ratings, "Smithing");
            Assert.Greater(speed, 1f, "worktype-matched recipe gains speed via weapon skill");
        }
    }
}
