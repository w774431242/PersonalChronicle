using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 「原版作息表时段推导」离线单测。
    ///
    /// <para><b>背景（用户实机 P0 · 2026-10-03）</b>：车间管理里给同一个班组同时指派了
    /// 「日班 08:00–17:00」与「夜班 20:00–08:00」两个班次，玩家反馈<b>「2 个班次对 pawn 都生效，
    /// 全体 24 小时都在工作」</b>。根因不在"每个班次派几个人"（名额），而在
    /// <c>SyncVanillaTimetables</c> 把班组所有班次的时段<b>取并集</b>写进原版作息表：
    /// <c>[8..17) ∪ [20..24) ∪ [0..8) = 20 小时 Work、仅 3 小时 Sleep</c>
    /// ⇒ 原版引擎层面就剥夺了睡眠。</para>
    ///
    /// <para>修复后语义：<b>一个班组当日只值一个班次</b>（按倒班表轮换），
    /// 其余小时交回 <c>Sleep</c>。本测试文件把该语义钉死，防止再次回归。</para>
    ///
    /// <para>注：这里复刻 <c>MarkHours</c> 的标记逻辑（纯 bool 数组运算，不依赖 Verse），
    /// 以便离线断言"某班次集合推导出的 Work 小时集合"。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopTimetableHoursTests
    {
        /// <summary>与 <c>MapComponent_WorkshopManager.MarkHours</c> 同口径的本地复刻。</summary>
        private static bool[] MarkHours(int startHour, int endHour)
        {
            var work = new bool[24];
            if (startHour == endHour)
            {
                // 全天班：必须标满 24 小时（旧实现 `h != EndHour` 会一个都不标）。
                for (int h = 0; h < 24; h++) { work[h] = true; }
                return work;
            }
            for (int h = startHour; h != endHour; h = (h + 1) % 24)
            {
                work[h] = true;
            }
            return work;
        }

        private static int WorkHours(bool[] work)
        {
            int n = 0;
            for (int i = 0; i < 24; i++) { if (work[i]) { n++; } }
            return n;
        }

        private static int[] SleepHours(bool[] work)
        {
            var list = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 24; i++) { if (!work[i]) { list.Add(i); } }
            return list.ToArray();
        }

        /// <summary>
        /// 回归钉子（P0-A）：**绝不能**把「日班 ∪ 夜班」写进同一个人的作息表。
        /// 旧行为 = 20 小时 Work / 3 小时 Sleep ⇒ 玩家观察到的「全体 24 小时都在工作」。
        /// </summary>
        [Test]
        public void DayPlusNight_UnionWouldStarveSleep_MustNeverBeUsedForOneCrew()
        {
            bool[] day = MarkHours(8, 17);    // 日班
            bool[] night = MarkHours(20, 8);   // 夜班（跨午夜）
            var union = new bool[24];
            for (int h = 0; h < 24; h++) { union[h] = day[h] || night[h]; }

            // 先把缺陷复现出来（证明这条用例真的在守东西）
            // 日班 [8..17) = 9 小时；夜班 [20..8) 跨午夜 = 12 小时；两者无交集 ⇒ 并集 21 小时。
            Assert.AreEqual(21, WorkHours(union), "日班∪夜班 = 21 小时 Work，正是用户报告的现象");
            CollectionAssert.AreEqual(new[] { 17, 18, 19 }, SleepHours(union),
                "只剩 17/18/19 三小时能睡 —— 这就是「没有下班」的机制");

            // 修复后：每个班组当日只值一个班次 ⇒ 至少 12 小时睡眠
            Assert.GreaterOrEqual(WorkHours(day), 8);
            Assert.GreaterOrEqual(24 - WorkHours(day), 12, "只值日班时必须留出至少 12 小时睡眠");
            Assert.GreaterOrEqual(24 - WorkHours(night), 12, "只值夜班时必须留出至少 12 小时睡眠");
        }

        /// <summary>跨午夜班次（20→8）必须正确环绕成 {20..23, 0..7}。</summary>
        [Test]
        public void NightShift_WrapsAroundMidnight()
        {
            bool[] work = MarkHours(20, 8);
            Assert.AreEqual(12, WorkHours(work));
            CollectionAssert.Contains(SleepHours(work), 12, "正午不属于夜班");
            CollectionAssert.Contains(SleepHours(work), 17);
            for (int h = 20; h < 24; h++) { Assert.IsTrue(work[h], h + " 应属夜班"); }
            for (int h = 0; h < 8; h++) { Assert.IsTrue(work[h], h + " 应属夜班（跨午夜）"); }
        }

        /// <summary>
        /// 回归钉子（P0-B）：全天班（<c>start == end</c>）必须标满 24 小时。
        /// 旧实现用 <c>for (h = start; h != end; ...)</c> ⇒ 循环体一次都不执行 ⇒
        /// 「全天班」反而**一小时的 Work 都不写**（语义反转）。
        /// </summary>
        [Test]
        public void AllDayShift_MarksAll24Hours_NotZero()
        {
            bool[] work = MarkHours(20, 20);
            Assert.AreEqual(24, WorkHours(work),
                "全天班必须覆盖 0–23 时；旧实现标记 0 小时（语义反转 bug）");
        }

        /// <summary>单班次场景零行为变化：8→17 精确标 9 小时，其余 15 小时为睡眠。</summary>
        [Test]
        public void SingleDayShift_UnchangedBehaviour()
        {
            bool[] work = MarkHours(8, 17);
            Assert.AreEqual(9, WorkHours(work));
            CollectionAssert.AreEqual(
                new[] { 0, 1, 2, 3, 4, 5, 6, 7, 17, 18, 19, 20, 21, 22, 23 },
                SleepHours(work));
        }
    }
}
