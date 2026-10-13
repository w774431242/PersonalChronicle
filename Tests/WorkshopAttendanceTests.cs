using NUnit.Framework;
using PersonalChronicle.Automation;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 车间考勤台账（<see cref="WorkshopAttendance"/>）离线单测 —— Phase 2（考勤与加班）。
    ///
    /// <para>契约：① 同一天多次值勤累加；② 加班只累加正数部分；③ 未记录的日期返回 0；
    /// ④ 区间求和正确；⑤ 裁剪只保留最近 <c>RetainDays</c> 天（含"未来日期"一并裁掉，
    /// 防止时间线回退时留下幽灵数据）。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopAttendanceTests
    {
        private const int Hour = 2500;

        [Test]
        public void AddDuty_AccumulatesWithinTheSameDay()
        {
            var ledger = new WorkshopAttendance();
            ledger.AddDuty(100, 4 * Hour, 0);
            ledger.AddDuty(100, 3 * Hour, 0);
            Assert.AreEqual(7L * Hour, ledger.ActualTicksOn(100));
            Assert.AreEqual(0L, ledger.OvertimeTicksOn(100));
        }

        [Test]
        public void AddDuty_RecordsOnlyPositiveOvertime()
        {
            var ledger = new WorkshopAttendance();
            ledger.AddDuty(100, 8 * Hour, 2 * Hour);
            ledger.AddDuty(100, 8 * Hour, -5);      // 异常负数应被忽略
            Assert.AreEqual(16L * Hour, ledger.ActualTicksOn(100));
            Assert.AreEqual(2L * Hour, ledger.OvertimeTicksOn(100));
        }

        [Test]
        public void AddDuty_NonPositiveTicks_IsIgnored()
        {
            var ledger = new WorkshopAttendance();
            ledger.AddDuty(100, 0, 0);
            ledger.AddDuty(100, -10, 0);
            Assert.AreEqual(0L, ledger.ActualTicksOn(100));
        }

        [Test]
        public void UnknownDay_ReadsZero()
        {
            var ledger = new WorkshopAttendance();
            ledger.AddDuty(100, Hour, 0);
            Assert.AreEqual(0L, ledger.ActualTicksOn(99));
            Assert.AreEqual(0L, ledger.OvertimeTicksOn(99));
        }

        [Test]
        public void RangeSum_CoversInclusiveDays()
        {
            var ledger = new WorkshopAttendance();
            ledger.AddDuty(100, Hour, 0);
            ledger.AddDuty(101, 2 * Hour, Hour);
            ledger.AddDuty(102, 3 * Hour, 0);
            ledger.AddDuty(103, 4 * Hour, 0);
            Assert.AreEqual(6L * Hour, ledger.ActualTicksInRange(100, 3));
            Assert.AreEqual(1L * Hour, ledger.OvertimeTicksInRange(100, 3));
            Assert.AreEqual(0L, ledger.ActualTicksInRange(200, 3));
        }

        [Test]
        public void Prune_DropsOldAndFutureDays()
        {
            var ledger = new WorkshopAttendance();
            int today = 200;
            ledger.AddDuty(today, Hour, 0);
            ledger.AddDuty(today - WorkshopAttendance.RetainDays + 1, Hour, 0);   // 边界内，保留
            ledger.AddDuty(today - WorkshopAttendance.RetainDays, Hour, 0);       // 过期，裁掉
            ledger.AddDuty(today + 1, Hour, 0);                                   // 未来（时间线回退），裁掉
            ledger.Prune(today);

            Assert.AreEqual(Hour, ledger.ActualTicksOn(today));
            Assert.AreEqual(Hour, ledger.ActualTicksOn(today - WorkshopAttendance.RetainDays + 1));
            Assert.AreEqual(0L, ledger.ActualTicksOn(today - WorkshopAttendance.RetainDays));
            Assert.AreEqual(0L, ledger.ActualTicksOn(today + 1));
        }
    }
}
