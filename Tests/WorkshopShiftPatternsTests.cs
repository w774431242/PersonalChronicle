using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Automation;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 班制目录（<see cref="WorkshopShiftPatterns"/>）离线单测（2026-09-11 用户需求：两班倒/三班倒）。
    ///
    /// <para>钉死的契约：① 每个班制的窗口**互不重叠**；② 两班倒与三班倒**恰好覆盖 24 小时**
    /// （这是"倒班"的定义，也是 UI 声称"全天无空档"的依据）；③ 常白班**不覆盖**全天；
    /// ④ 窗口小时数按跨午夜正确计算；⑤ 建议名额按人数/班次均分且至少 1。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopShiftPatternsTests
    {
        [Test]
        public void Catalog_ContainsDayTwoAndThreeShiftPatterns()
        {
            List<ShiftPattern> all = WorkshopShiftPatterns.All;
            Assert.IsNotNull(all);
            Assert.GreaterOrEqual(all.Count, 4);
            Assert.IsNotNull(WorkshopShiftPatterns.ByKey(WorkshopShiftPatterns.KeyDayShift));
            Assert.IsNotNull(WorkshopShiftPatterns.ByKey(WorkshopShiftPatterns.KeyTwoShift));
            Assert.IsNotNull(WorkshopShiftPatterns.ByKey(WorkshopShiftPatterns.KeyThreeShift6));
            Assert.IsNotNull(WorkshopShiftPatterns.ByKey(WorkshopShiftPatterns.KeyThreeShift8));
            Assert.IsNull(WorkshopShiftPatterns.ByKey("nope"));
            Assert.IsNull(WorkshopShiftPatterns.ByKey(null));
        }

        [Test]
        public void TwoAndThreeShift_CoverWholeDay()
        {
            Assert.IsTrue(WorkshopShiftPatterns.CoversWholeDay(
                WorkshopShiftPatterns.ByKey(WorkshopShiftPatterns.KeyTwoShift)));
            Assert.IsTrue(WorkshopShiftPatterns.CoversWholeDay(
                WorkshopShiftPatterns.ByKey(WorkshopShiftPatterns.KeyThreeShift6)));
            Assert.IsTrue(WorkshopShiftPatterns.CoversWholeDay(
                WorkshopShiftPatterns.ByKey(WorkshopShiftPatterns.KeyThreeShift8)));
        }

        [Test]
        public void DayShift_DoesNotCoverWholeDay()
        {
            ShiftPattern day = WorkshopShiftPatterns.ByKey(WorkshopShiftPatterns.KeyDayShift);
            Assert.AreEqual(1, day.ShiftCount);
            Assert.AreEqual(8, day.CoveredHoursPerDay);
            Assert.IsFalse(WorkshopShiftPatterns.CoversWholeDay(day));
        }

        [Test]
        public void EveryPattern_WindowsAreDisjointAndCoverTheirHours()
        {
            for (int p = 0; p < WorkshopShiftPatterns.All.Count; p++)
            {
                ShiftPattern pattern = WorkshopShiftPatterns.All[p];
                bool[] covered = new bool[24];
                int expected = 0;
                for (int w = 0; w < pattern.Windows.Count; w++)
                {
                    ShiftWindow window = pattern.Windows[w];
                    expected += WorkshopShiftPatterns.HoursOf(window);
                    for (int hour = 0; hour < 24; hour++)
                    {
                        if (!WorkshopHours.InRange(hour, window.StartHour, window.EndHour))
                        {
                            continue;
                        }
                        Assert.IsFalse(covered[hour],
                            pattern.Key + " 的窗口在 " + hour + " 点重叠");
                        covered[hour] = true;
                    }
                }
                Assert.AreEqual(expected, pattern.CoveredHoursPerDay, pattern.Key + " 小时数不符");
            }
        }

        [Test]
        public void HoursOf_HandlesMidnightAndAllDay()
        {
            Assert.AreEqual(8, WorkshopShiftPatterns.HoursOf(new ShiftWindow(16, 0)));
            Assert.AreEqual(12, WorkshopShiftPatterns.HoursOf(new ShiftWindow(20, 8)));
            Assert.AreEqual(8, WorkshopShiftPatterns.HoursOf(new ShiftWindow(22, 6)));
            Assert.AreEqual(24, WorkshopShiftPatterns.HoursOf(new ShiftWindow(6, 6)));
        }

        [Test]
        public void SuggestedRequiredCount_SplitsPoolAndNeverZero()
        {
            Assert.AreEqual(2, WorkshopShiftPatterns.SuggestedRequiredCount(6, 3));
            Assert.AreEqual(3, WorkshopShiftPatterns.SuggestedRequiredCount(6, 2));
            Assert.AreEqual(1, WorkshopShiftPatterns.SuggestedRequiredCount(2, 3));
            Assert.AreEqual(1, WorkshopShiftPatterns.SuggestedRequiredCount(0, 3));
            Assert.AreEqual(1, WorkshopShiftPatterns.SuggestedRequiredCount(5, 0));
        }
    }
}
