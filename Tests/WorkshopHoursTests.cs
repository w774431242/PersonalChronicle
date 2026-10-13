using NUnit.Framework;
using PersonalChronicle.Automation;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 车间时段判定（<see cref="WorkshopHours"/>）离线单测 —— B 域体检补的缺口。
    ///
    /// <para>钉死的契约：① <c>start == end</c> = 全天；② <c>end &lt; start</c> = 跨午夜；
    /// ③ 区间为左闭右开（<c>hour ∈ [start, end)</c>，跨午夜时 = <c>[start,24) ∪ [0,end)</c>）；
    /// ④ 非法小时（&lt;0 或 &gt;23）恒 false，不抛异常。固定班 / 班组 / 轮岗 / 时段编辑器
    /// 全部共用这一处实现，所以这些边界就是整条排班链的边界。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopHoursTests
    {
        [Test]
        public void InRange_SameStartAndEnd_MeansAllDay()
        {
            Assert.IsTrue(WorkshopHours.InRange(0, 6, 6));
            Assert.IsTrue(WorkshopHours.InRange(6, 6, 6));
            Assert.IsTrue(WorkshopHours.InRange(23, 6, 6));
            Assert.IsTrue(WorkshopHours.InRange(12, 0, 0));
        }

        [Test]
        public void InRange_DayShift_IncludesStartButExcludesEnd()
        {
            Assert.IsTrue(WorkshopHours.InRange(6, 6, 18));
            Assert.IsTrue(WorkshopHours.InRange(17, 6, 18));
            Assert.IsFalse(WorkshopHours.InRange(18, 6, 18));
            Assert.IsFalse(WorkshopHours.InRange(5, 6, 18));
            Assert.IsFalse(WorkshopHours.InRange(23, 6, 18));
        }

        [Test]
        public void InRange_CrossMidnight_WrapsAround()
        {
            Assert.IsTrue(WorkshopHours.InRange(18, 18, 6));
            Assert.IsTrue(WorkshopHours.InRange(23, 18, 6));
            Assert.IsTrue(WorkshopHours.InRange(0, 18, 6));
            Assert.IsTrue(WorkshopHours.InRange(5, 18, 6));
            Assert.IsFalse(WorkshopHours.InRange(6, 18, 6));
            Assert.IsFalse(WorkshopHours.InRange(12, 18, 6));
        }

        [Test]
        public void InRange_InvalidHour_IsAlwaysFalse()
        {
            Assert.IsFalse(WorkshopHours.InRange(-1, 0, 0));
            Assert.IsFalse(WorkshopHours.InRange(24, 0, 0));
            Assert.IsFalse(WorkshopHours.InRange(-1, 18, 6));
        }

        [Test]
        public void CrossesMidnight_OnlyWhenEndIsEarlierAndNotAllDay()
        {
            Assert.IsTrue(WorkshopHours.CrossesMidnight(18, 6));
            Assert.IsFalse(WorkshopHours.CrossesMidnight(6, 18));
            Assert.IsFalse(WorkshopHours.CrossesMidnight(6, 6));
            Assert.IsFalse(WorkshopHours.CrossesMidnight(0, 0));
        }

        [Test]
        public void LabelOf_PadsToTwoDigits()
        {
            Assert.AreEqual("06:00 – 18:00", WorkshopHours.LabelOf(6, 18));
            Assert.AreEqual("00:00 – 00:00", WorkshopHours.LabelOf(0, 0));
            Assert.AreEqual("23:00 – 05:00", WorkshopHours.LabelOf(23, 5));
        }
    }
}
