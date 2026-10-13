// 专业适配分析器单测（内在禀赋主导版公式 + 工程回归）：
//   · 12 专业权重和 = 100
//   · skillBase 弱边际递减（单调 + 端点 + 晚段增益 < 早段）
//   · 六维公式 / 归一化 / 排序 / 优势短板 / 空输入不崩
//   · [2026-09-03 重构锚点] 背景与特性驱动推荐 / 高战斗 pawn 必荐武器类 / 禁用技能归零
using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain.Profession;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public class ProfessionalFitAnalyzerTests
    {
        [Test]
        public void AllTwelveMajorsPresent()
        {
            Assert.That(ProfessionalMajor.All.Count, Is.EqualTo(12));
            Assert.That(ProfessionalFitAnalyzer.MajorWeightsTable.Count, Is.EqualTo(12));
        }

        [Test]
        public void EveryMajorWeightRowSumsTo100()
        {
            foreach (KeyValuePair<string, Dictionary<string, int>> row in ProfessionalFitAnalyzer.MajorWeightsTable)
            {
                int sum = 0;
                foreach (KeyValuePair<string, int> kv in row.Value)
                {
                    sum += kv.Value;
                }
                Assert.That(sum, Is.EqualTo(100), "major " + row.Key);
            }
        }

        [Test]
        public void SkillBaseIsMonotonicWithDiminishingReturns()
        {
            Assert.That(ProfessionalFitAnalyzer.SkillBase(0), Is.EqualTo(0));
            Assert.That(ProfessionalFitAnalyzer.SkillBase(20), Is.EqualTo(100));
            // 四舍五入允许相邻档增益有 ±1 波动，因此只断言单调非降 + 端点增益
            // 显著小于起点增益（严格递减由 SkillBaseLowGainIsSmallerThanEarlyGain 覆盖）。
            int prev = 0;
            for (int l = 1; l <= 20; l++)
            {
                int v = ProfessionalFitAnalyzer.SkillBase(l);
                Assert.That(v, Is.GreaterThanOrEqualTo(prev), "level " + l);
                Assert.That(v, Is.LessThanOrEqualTo(100), "level " + l);
                prev = v;
            }
            int early = ProfessionalFitAnalyzer.SkillBase(5) - ProfessionalFitAnalyzer.SkillBase(4);
            int late = ProfessionalFitAnalyzer.SkillBase(20) - ProfessionalFitAnalyzer.SkillBase(19);
            Assert.That(late, Is.LessThan(early));
        }

        [Test]
        public void SkillBaseLowGainIsSmallerThanEarlyGain()
        {
            int early = ProfessionalFitAnalyzer.SkillBase(3) - ProfessionalFitAnalyzer.SkillBase(2);
            int late = ProfessionalFitAnalyzer.SkillBase(19) - ProfessionalFitAnalyzer.SkillBase(18);
            Assert.That(late, Is.LessThan(early));
        }

        [Test]
        public void PracticeNormClamps()
        {
            Assert.That(ProfessionalFitAnalyzer.PracticeNorm(0), Is.EqualTo(0));
            Assert.That(ProfessionalFitAnalyzer.PracticeNorm(160), Is.EqualTo(50));
            Assert.That(ProfessionalFitAnalyzer.PracticeNorm(320), Is.EqualTo(100));
            Assert.That(ProfessionalFitAnalyzer.PracticeNorm(9999), Is.EqualTo(100));
        }

        [Test]
        public void QualityScoreWeightsAndEmpty()
        {
            Assert.That(ProfessionalFitAnalyzer.QualityScore(null), Is.EqualTo(0));
            Assert.That(ProfessionalFitAnalyzer.QualityScore(new Dictionary<string, int>()), Is.EqualTo(0));
            var d = new Dictionary<string, int> { { "Normal", 180 }, { "Good", 64 }, { "Excellent", 22 }, { "Masterwork", 6 }, { "Legendary", 0 } };
            int s = ProfessionalFitAnalyzer.QualityScore(d);
            Assert.That(s, Is.GreaterThan(40));
            Assert.That(s, Is.LessThan(60));
            Assert.That(s, Is.EqualTo((int)System.Math.Round((180.0 * 40 + 64 * 60 + 22 * 80 + 6 * 95) / (180.0 + 64 + 22 + 6))));
        }

        [Test]
        public void AnalyzeSortsByFitDescending()
        {
            var input = new ProfessionalFitInput();
            input.SkillLevels["Crafting"] = 16;
            input.SkillLevels["Construction"] = 14;
            input.SkillLevels["Intellectual"] = 12;
            input.Practice["Crafting"] = 312;
            input.Practice["Construction"] = 243;
            input.Passion["Crafting"] = 100;
            input.Passion["Construction"] = 50;
            input.QualityCounts["Normal"] = 180;
            input.QualityCounts["Good"] = 64;
            input.QualityCounts["Excellent"] = 22;
            input.QualityCounts["Masterwork"] = 6;

            List<ProfessionalFitResult> results = ProfessionalFitAnalyzer.Analyze(input);
            Assert.That(results.Count, Is.EqualTo(12));
            for (int i = 1; i < results.Count; i++)
            {
                Assert.That(results[i - 1].Fit, Is.GreaterThanOrEqualTo(results[i].Fit));
            }
            // 制造强样本下制造类应排前（Crafting 权重最高）。
            Assert.That(results[0].Major, Is.EqualTo(ProfessionalMajor.Manufacturing));
            Assert.That(results[0].Fit, Is.InRange(0, 100));
        }

        [Test]
        public void AnalyzeEmptyInputNeverThrows()
        {
            List<ProfessionalFitResult> results = ProfessionalFitAnalyzer.Analyze(new ProfessionalFitInput());
            Assert.That(results.Count, Is.EqualTo(12));
            foreach (ProfessionalFitResult r in results)
            {
                Assert.That(r.Fit, Is.InRange(0, 100));
                Assert.That(r.Pros.Count + r.Cons.Count, Is.LessThanOrEqualTo(3));
            }
        }

        [Test]
        public void ProsAndConsFollowThresholds()
        {
            var input = new ProfessionalFitInput();
            // Crafting 权重最高且 20 级 → 制造类 Pros 应含 Crafting。
            input.SkillLevels["Crafting"] = 20;
            input.SkillLevels["Intellectual"] = 5;
            List<ProfessionalFitResult> results = ProfessionalFitAnalyzer.Analyze(input);
            ProfessionalFitResult mfg = results.Find(r => r.Major == ProfessionalMajor.Manufacturing);
            Assert.That(mfg, Is.Not.Null);
            Assert.That(mfg.Pros, Does.Contain("Crafting"));
        }

        [Test]
        public void PassionAndPracticeRaiseScores()
        {
            var baseInput = new ProfessionalFitInput();
            baseInput.SkillLevels["Crafting"] = 10;
            var boosted = new ProfessionalFitInput();
            boosted.SkillLevels["Crafting"] = 10;
            boosted.Passion["Crafting"] = 100;
            boosted.Practice["Crafting"] = 320;

            var a = ProfessionalFitAnalyzer.Analyze(baseInput).Find(r => r.Major == ProfessionalMajor.Manufacturing);
            var b = ProfessionalFitAnalyzer.Analyze(boosted).Find(r => r.Major == ProfessionalMajor.Manufacturing);
            Assert.That(b.Fit, Is.GreaterThan(a.Fit));
            Assert.That(b.PracticeScore, Is.GreaterThan(a.PracticeScore));
        }

        [Test]
        public void GrowthStaysWithinBounds()
        {
            var input = new ProfessionalFitInput { BaseGrowth = 100 };
            input.Passion["Crafting"] = 100;
            input.SkillLevels["Crafting"] = 20;
            input.Practice["Crafting"] = 999;
            var r = ProfessionalFitAnalyzer.Analyze(input).Find(x => x.Major == ProfessionalMajor.Manufacturing);
            Assert.That(r.Fit, Is.LessThanOrEqualTo(100));
            Assert.That(r.GrowthScore, Is.LessThanOrEqualTo(100));
        }

        // ---- [2026-09-03 重构锚点：内在禀赋主导] ----

        [Test]
        public void AptitudeNormClampsAroundNeutral50()
        {
            Assert.That(ProfessionalFitAnalyzer.AptitudeNorm(0), Is.EqualTo(50));
            Assert.That(ProfessionalFitAnalyzer.AptitudeNorm(8), Is.EqualTo(100));
            Assert.That(ProfessionalFitAnalyzer.AptitudeNorm(-8), Is.EqualTo(0));
            Assert.That(ProfessionalFitAnalyzer.AptitudeNorm(999), Is.EqualTo(100));
            Assert.That(ProfessionalFitAnalyzer.AptitudeNorm(-999), Is.EqualTo(0));
        }

        [Test]
        public void HighCombatPawnRecommendsWeaponsOverArt()
        {
            // 高射击(20,热情满)/格斗(17,热情满)/手工(15,热情满)/医疗(16,兴趣) pawn：
            // 艺术仅靠 Artistic 热情不得翻盘 → 武器类必须第一。
            var input = new ProfessionalFitInput();
            input.SkillLevels["Shooting"] = 20;
            input.SkillLevels["Melee"] = 17;
            input.SkillLevels["Crafting"] = 15;
            input.SkillLevels["Medicine"] = 16;
            input.Passion["Shooting"] = 100;
            input.Passion["Melee"] = 100;
            input.Passion["Crafting"] = 100;
            input.Passion["Medicine"] = 50;
            foreach (string s in new[] { "Construction", "Mining", "Cooking", "Plants", "Animals", "Artistic", "Social", "Intellectual" })
            {
                input.SkillLevels[s] = 10;
            }

            List<ProfessionalFitResult> results = ProfessionalFitAnalyzer.Analyze(input);
            Assert.That(results[0].Major, Is.EqualTo(ProfessionalMajor.Weapons),
                "高战斗 pawn 应回归武器类而非艺术类");
        }

        [Test]
        public void BackstoryAndTraitAptitudeCanDriveRecommendation()
        {
            // 全员低等级(8)但背景+特性重仓艺术：内在禀赋应把艺术类顶到第一（aptitude 驱动）。
            var input = new ProfessionalFitInput();
            foreach (string s in ProfessionalFitAnalyzer.VanillaSkillKeys)
            {
                input.SkillLevels[s] = 8;
            }
            input.BackstoryGains["Artistic"] = 8;
            input.TraitGains["Artistic"] = 4;

            List<ProfessionalFitResult> results = ProfessionalFitAnalyzer.Analyze(input);
            Assert.That(results[0].Major, Is.EqualTo(ProfessionalMajor.Art),
                "背景+特性重仓艺术的 pawn 应被荐艺术类（即使当前等级不高）");
            ProfessionalFitResult art = results.Find(r => r.Major == ProfessionalMajor.Art);
            Assert.That(art.BackstoryScore, Is.GreaterThan(70));
            Assert.That(art.TraitScore, Is.GreaterThan(60));
        }

        [Test]
        public void DisabledSkillContributesZero()
        {
            // Artistic 20 级(热情满)但被禁用：艺术类的等级/热情/加成全按 0 计，fit 应显著回落。
            var enabled = new ProfessionalFitInput();
            enabled.SkillLevels["Artistic"] = 20;
            enabled.Passion["Artistic"] = 100;
            var disabled = new ProfessionalFitInput();
            disabled.SkillLevels["Artistic"] = 20;
            disabled.Passion["Artistic"] = 100;
            disabled.DisabledSkills.Add("Artistic");

            var a = ProfessionalFitAnalyzer.Analyze(enabled).Find(r => r.Major == ProfessionalMajor.Art);
            var b = ProfessionalFitAnalyzer.Analyze(disabled).Find(r => r.Major == ProfessionalMajor.Art);
            Assert.That(b.SkillScore, Is.LessThan(a.SkillScore));
            Assert.That(b.PassionScore, Is.LessThan(a.PassionScore));
            Assert.That(b.Fit, Is.LessThan(a.Fit));
        }

        [Test]
        public void NegativeTraitGainsLowerFit()
        {
            var baseInput = new ProfessionalFitInput();
            baseInput.SkillLevels["Social"] = 10;
            var penalized = new ProfessionalFitInput();
            penalized.SkillLevels["Social"] = 10;
            penalized.TraitGains["Social"] = -6;

            var a = ProfessionalFitAnalyzer.Analyze(baseInput).Find(r => r.Major == ProfessionalMajor.Management);
            var b = ProfessionalFitAnalyzer.Analyze(penalized).Find(r => r.Major == ProfessionalMajor.Management);
            Assert.That(b.Fit, Is.LessThan(a.Fit), "负向特性加成应压低对应专业适配分");
        }
    }
}
