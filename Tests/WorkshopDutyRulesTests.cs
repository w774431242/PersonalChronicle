using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 值勤收工判定离线单测（CHG-20261003-WORKSHOP-SPLIT · 修成因 B）。
    ///
    /// <para><b>背景（用户实机确认）</b>：旧实现用
    /// <c>JobDriver.FailOn(() =&gt; !IsScheduledForDuty(pawn))</c> 做"到点即收工"，
    /// 而调度计划<b>按小时重建</b> ⇒ 跨小时那一瞬若人仍在岗而窗口已过，Job 被立刻打断
    /// ⇒ <b>同一班内反复开工/收工</b>。</para>
    ///
    /// <para><b>钉死的核心契约</b>：① 仍在班次窗口内 ⇒ <b>永不</b>收工（这是修掉跨小时误打断的关键）；
    /// ② 已到点但未达最小连续时长 ⇒ <b>不打断</b>（避免"开工 5 分钟就被踢"）；
    /// ③ 到点 <b>且</b>已达最小时长 ⇒ 收工；④ 跨午夜班要能正确环绕。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopDutyRulesTests
    {
        [Test]
        public void InsideWindow_NeverEnds_ThisIsTheCrossHourFix()
        {
            // 日班 08-17。逐小时断言"仍在窗口内就不收工"。
            // 旧实现在跨小时（09:00）时因计划重建而误判不在岗 ⇒ 打断 Job（成因 B）。
            for (int h = 8; h < 17; h++)
            {
                Assert.IsFalse(WorkshopDutyRules.ShouldEndDuty(8, h, 8, 17, 1),
                    "第 " + h + " 点仍在日班窗口内，绝不能收工（跨小时误打断的回归钉子）");
            }
        }

        [Test]
        public void AfterWindow_ReachedMinHours_EndsDuty()
        {
            // 17 点已过窗口（[8,17) 左闭右开），且已连续 9 小时 >= 1 ⇒ 收工。
            Assert.IsTrue(WorkshopDutyRules.ShouldEndDuty(8, 17, 8, 17, 1),
                "到点且已达最小连续时长 ⇒ 应收工");
        }

        [Test]
        public void AfterWindow_NotYetMinHours_DoesNotInterrupt()
        {
            // 关键语义：18 点才开工（走到工位花了 1 小时），最小连续时长 3 小时。
            // 19 点时已到点但只连续值勤 1 小时 < 3 ⇒ **不打断**。
            Assert.IsFalse(WorkshopDutyRules.ShouldEndDuty(18, 19, 8, 17, 3),
                "未达最小连续时长时不得收工（否则表现为开工 5 分钟就被踢）");
            // 21 点时已连续 3 小时 ⇒ 收工。
            Assert.IsTrue(WorkshopDutyRules.ShouldEndDuty(18, 21, 8, 17, 3),
                "已达最小连续时长 ⇒ 收工");
        }

        [Test]
        public void NightShift_WrapsAroundMidnight()
        {
            // 夜班 20-08（跨午夜）。
            for (int h = 20; h < 24; h++)
            {
                Assert.IsFalse(WorkshopDutyRules.ShouldEndDuty(20, h, 20, 8, 1),
                    "第 " + h + " 点属夜班窗口");
            }
            for (int h = 0; h < 8; h++)
            {
                Assert.IsFalse(WorkshopDutyRules.ShouldEndDuty(20, h, 20, 8, 1),
                    "第 " + h + " 点属夜班窗口（跨午夜环绕）");
            }
            Assert.IsTrue(WorkshopDutyRules.ShouldEndDuty(20, 8, 20, 8, 1),
                "08 点已过夜班窗口 ⇒ 收工");
        }

        [Test]
        public void HoursSince_WrapsAroundMidnight()
        {
            Assert.AreEqual(0, WorkshopDutyRules.HoursSince(8, 8), "同小时 = 0");
            Assert.AreEqual(1, WorkshopDutyRules.HoursSince(8, 9));
            Assert.AreEqual(2, WorkshopDutyRules.HoursSince(23, 1), "23 点上岗、1 点查询 = 2 小时（跨午夜）");
            Assert.AreEqual(3, WorkshopDutyRules.HoursSince(22, 1));
        }

        [Test]
        public void NightShift_JustStarted_CrossesMidnightWithoutEarlyEnd()
        {
            // 真实边界：20 点上岗夜班，00 点查询（跨午夜）。
            // 若 HoursSince 不环绕，会算成 0-20=-20 ⇒ 判定逻辑可能误收工。
            Assert.IsFalse(WorkshopDutyRules.ShouldEndDuty(20, 0, 20, 8, 4),
                "跨午夜仍在夜班窗口内 ⇒ 不得收工（HoursSince 环绕的正确性）");
        }

        [Test]
        public void AllDayShift_StaysOnDutyAllDay()
        {
            // 全天班（start == end）：WorkshopTime.InRange 恒真 ⇒ 永不收工。
            for (int h = 0; h < 24; h++)
            {
                Assert.IsFalse(WorkshopDutyRules.ShouldEndDuty(0, h, 8, 8, 1),
                    "全天班在第 " + h + " 点仍在窗口内");
            }
        }

        [Test]
        public void MarkWorkHours_AllDayShift_MarksAll24()
        {
            // 回归钉子：旧实现 for(h=start; h!=end;) 在全天班时一个都不标 ⇒ 语义反转。
            var work = new bool[24];
            WorkshopDutyRules.MarkWorkHours(8, 8, work);
            Assert.AreEqual(24, WorkshopDutyRules.CountWorkHours(work),
                "全天班必须标满 24 小时（旧实现 for(h=start; h!=end;) 一个都不标 = 语义反转）");
        }

        [Test]
        public void MarkWorkHours_SingleShift_MarksExactlyItsWindow()
        {
            var work = new bool[24];
            WorkshopDutyRules.MarkWorkHours(8, 17, work);
            Assert.AreEqual(9, WorkshopDutyRules.CountWorkHours(work));
            Assert.IsTrue(work[8], "08 点属日班");
            Assert.IsFalse(work[7], "07 点不属日班");
            Assert.IsFalse(work[17], "17 点不属日班（左闭右开）");
        }
    }
}
