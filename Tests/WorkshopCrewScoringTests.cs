using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// S3-4 前置：胜任度评分纯函数（<see cref="WorkshopCrewScoring"/>）的离线单测。
    /// 口径：技能 60 + 激情 20 + 心情 20；不可用 ⇒ -1。
    /// </summary>
    [TestFixture]
    public class WorkshopCrewScoringTests
    {
        [Test]
        public void FullMarks_Level20_Major_MoodFull()
        {
            Assert.AreEqual(100, WorkshopCrewScoring.Score(20, 2, 1f, true));
        }

        [Test]
        public void SkillOnly_Level12_NoPassion_ZeroMood()
        {
            // 12 × 3 = 36
            Assert.AreEqual(36, WorkshopCrewScoring.Score(12, 0, 0f, true));
        }

        [Test]
        public void PassionTiers_MinorIsTen_MajorIsTwenty()
        {
            Assert.AreEqual(10, WorkshopCrewScoring.Score(0, 1, 0f, true));
            Assert.AreEqual(20, WorkshopCrewScoring.Score(0, 2, 0f, true));
        }

        [Test]
        public void MoodContributes_UpToTwenty()
        {
            Assert.AreEqual(10, WorkshopCrewScoring.Score(0, 0, 0.5f, true));
            Assert.AreEqual(20, WorkshopCrewScoring.Score(0, 0, 1f, true));
        }

        [Test]
        public void ClampsOutOfRangeInputs()
        {
            Assert.AreEqual(100, WorkshopCrewScoring.Score(99, 5, 2f, true));
            Assert.AreEqual(0, WorkshopCrewScoring.Score(-5, 0, -1f, true));
        }

        [Test]
        public void Unavailable_ReturnsMinusOne()
        {
            Assert.AreEqual(WorkshopCrewScoring.Unavailable,
                WorkshopCrewScoring.Score(20, 2, 1f, false));
        }

        [Test]
        public void MaxScoreConstant_MatchesFullMarks()
        {
            Assert.AreEqual(WorkshopCrewScoring.MaxScore,
                WorkshopCrewScoring.Score(20, 2, 1f, true));
        }
    }
}
