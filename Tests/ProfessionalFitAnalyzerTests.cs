// 专业适配分析器单测（对齐前端原型 Node 校验项 + 工程回归）：
//   · 12 专业权重和 = 100
//   · skillBase 边际递减（18→19 增益远小于 2→3）
//   · 五维公式 / 归一化 / 排序 / 优势短板 / 空输入不崩
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
    }
}
