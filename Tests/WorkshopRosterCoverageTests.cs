using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Automation;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 排班覆盖度（<see cref="WorkshopRosterCoverage"/>）离线单测 —— 月度排班图的数据源。
    ///
    /// <para>契约：① 与 <see cref="WorkshopHours.InRange"/> 同口径（跨午夜/全天）；
    /// ② 同一小时的多条排班**累加**；③ 空档统计准确；④ 人时 = 每小时人数之和；
    /// ⑤ 色深归一化在空档时为 0、且非空档有下限（避免"1 人"在峰值很高时几乎看不见）。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopRosterCoverageTests
    {
        private static List<RosterSlot> Slots(params RosterSlot[] slots)
        {
            return new List<RosterSlot>(slots);
        }

        [Test]
        public void HourlyCoverage_DayShift_MarksExactlyItsHours()
        {
            int[] hourly = WorkshopRosterCoverage.HourlyCoverage(Slots(new RosterSlot(8, 16, 2)));
            for (int hour = 0; hour < 24; hour++)
            {
                int expected = hour >= 8 && hour < 16 ? 2 : 0;
                Assert.AreEqual(expected, hourly[hour], "小时 " + hour);
            }
            Assert.AreEqual(16, WorkshopRosterCoverage.ManHoursPerDay(hourly));
            Assert.AreEqual(16, WorkshopRosterCoverage.EmptyHours(hourly));
        }

        [Test]
        public void HourlyCoverage_CrossMidnight_Wraps()
        {
            int[] hourly = WorkshopRosterCoverage.HourlyCoverage(Slots(new RosterSlot(20, 8, 1)));
            Assert.AreEqual(1, hourly[20]);
            Assert.AreEqual(1, hourly[23]);
            Assert.AreEqual(1, hourly[0]);
            Assert.AreEqual(1, hourly[7]);
            Assert.AreEqual(0, hourly[8]);
            Assert.AreEqual(0, hourly[19]);
            Assert.AreEqual(12, WorkshopRosterCoverage.ManHoursPerDay(hourly));
            Assert.AreEqual(12, WorkshopRosterCoverage.EmptyHours(hourly));
        }

        [Test]
        public void HourlyCoverage_AllDay_StartEqualsEnd()
        {
            int[] hourly = WorkshopRosterCoverage.HourlyCoverage(Slots(new RosterSlot(6, 6, 1)));
            Assert.AreEqual(0, WorkshopRosterCoverage.EmptyHours(hourly));
            Assert.AreEqual(24, WorkshopRosterCoverage.ManHoursPerDay(hourly));
        }

        [Test]
        public void HourlyCoverage_OverlappingSlots_Accumulate()
        {
            // 两班倒（每班 1 人 ⇒ 每小时 1 人）+ 一条固定班在 08–12 再加 1 人
            // ⇒ 08..11 有 2 人，其余有人的小时 1 人；全天无空档（两班倒覆盖 24h）。
            int[] hourly = WorkshopRosterCoverage.HourlyCoverage(Slots(
                new RosterSlot(8, 20, 1), new RosterSlot(20, 8, 1), new RosterSlot(8, 12, 1)));
            Assert.AreEqual(2, hourly[8]);
            Assert.AreEqual(2, hourly[11]);
            Assert.AreEqual(1, hourly[12]);
            Assert.AreEqual(1, hourly[20]);
            Assert.AreEqual(1, hourly[0]);
            Assert.AreEqual(0, WorkshopRosterCoverage.EmptyHours(hourly));
            // 12（白班）+ 12（夜班，跨午夜）+ 4（加派）= 28 人时
            Assert.AreEqual(28, WorkshopRosterCoverage.ManHoursPerDay(hourly));
        }

        [Test]
        public void HourlyCoverage_ZeroOrNegativeCount_IsIgnored()
        {
            int[] hourly = WorkshopRosterCoverage.HourlyCoverage(Slots(
                new RosterSlot(0, 0, 0), new RosterSlot(0, 0, -3)));
            Assert.AreEqual(0, WorkshopRosterCoverage.ManHoursPerDay(hourly));
            Assert.AreEqual(24, WorkshopRosterCoverage.EmptyHours(hourly));
        }

        [Test]
        public void EmptyOrNullInput_IsAllEmpty()
        {
            Assert.AreEqual(24, WorkshopRosterCoverage.EmptyHours(null));
            Assert.AreEqual(0, WorkshopRosterCoverage.ManHoursPerDay(null));
            Assert.AreEqual(0, WorkshopRosterCoverage.MaxCoverage(null));
            int[] hourly = WorkshopRosterCoverage.HourlyCoverage(null);
            Assert.AreEqual(24, hourly.Length);
            Assert.AreEqual(24, WorkshopRosterCoverage.EmptyHours(hourly));
        }

        [Test]
        public void MaxCoverage_FindsPeak()
        {
            int[] hourly = WorkshopRosterCoverage.HourlyCoverage(Slots(
                new RosterSlot(8, 20, 4), new RosterSlot(20, 8, 1)));
            Assert.AreEqual(4, WorkshopRosterCoverage.MaxCoverage(hourly));
        }

        [Test]
        public void IntensityOf_EmptyIsZero_AndHasLowerBound()
        {
            Assert.AreEqual(0f, WorkshopRosterCoverage.IntensityOf(0, 8));
            Assert.AreEqual(0f, WorkshopRosterCoverage.IntensityOf(3, 0));
            Assert.AreEqual(1f, WorkshopRosterCoverage.IntensityOf(8, 8));
            // 1/8 = 0.125 < 0.35 ⇒ 抬到下限，保证"有人"在图上看得见。
            Assert.AreEqual(0.35f, WorkshopRosterCoverage.IntensityOf(1, 8));
        }
    }
}
