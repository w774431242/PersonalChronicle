using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 班组名额分配（「自定义排班人数」的核心语义）离线单测。
    ///
    /// <para>钉死的契约：① <c>requiredCount == 0</c>（未配置）⇒ <b>按默认名额 1 人</b>
    /// （<b>2026-10-03 语义修正</b>：旧契约是"≤0 = 不限人数"，导致多班次车间
    /// <b>每个班次都吃光整份名单</b>，"到点不下班"）；② <c>requiredCount &lt; 0</c>
    /// = 显式「不限名额」= 全部合格者上岗；③ 名额不足时按评分高者优先；
    /// ④ 同分时取池内更靠前者（**稳定性** —— 否则岗位会来回换人）；
    /// ⑤ 不合格者永不入选；⑥ 空池/空标记返回空表（不抛异常）。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopShiftPlannerTests
    {
        private static List<bool> Flags(params bool[] values)
        {
            return new List<bool>(values);
        }

        private static List<int> Scores(params int[] values)
        {
            return new List<int>(values);
        }

        /// <summary>
        /// 回归钉子（2026-10-03 · 用户实机 P0「到点了还卡在值勤、不下班睡觉」）：
        /// 缺省名额**必须**是 1，绝不能是"全部合格者"。
        /// 旧语义下夜班成员会被日班一起拉上岗 ⇒ 表现为永不下班。
        /// </summary>
        [Test]
        public void Select_ZeroCount_UsesDefaultHeadcount_NotAllEligible()
        {
            List<int> picked = WorkshopShiftPlanner.SelectDutyIndexes(
                Flags(true, true, true), Scores(1, 2, 3), 0);
            Assert.AreEqual(1, WorkshopShiftPlanner.DefaultHeadcount,
                "缺省名额必须是 1（一个班次一个人），这是三班倒语义的前提");
            CollectionAssert.AreEqual(new[] { 2 }, picked,
                "未配置名额(0) 时只能取评分最高的 1 人，不得全选");
        }

        [Test]
        public void Select_NegativeCount_MeansExplicitUnlimited()
        {
            // 显式「不限名额」仍可用（哨兵 < 0），行为与旧「不限」一致。
            List<int> picked = WorkshopShiftPlanner.SelectDutyIndexes(Flags(true, true), null, -5);
            CollectionAssert.AreEqual(new[] { 0, 1 }, picked);
            Assert.IsTrue(WorkshopShiftPlanner.IsUnlimited(-5));
            Assert.IsFalse(WorkshopShiftPlanner.IsUnlimited(0));
        }

        [Test]
        public void Select_CountLimitsToBestScores()
        {
            List<int> picked = WorkshopShiftPlanner.SelectDutyIndexes(
                Flags(true, true, true), Scores(5, 9, 7), 2);
            CollectionAssert.AreEqual(new[] { 1, 2 }, picked);
        }

        [Test]
        public void Select_EqualScores_KeepsPoolOrder()
        {
            List<int> picked = WorkshopShiftPlanner.SelectDutyIndexes(
                Flags(true, true, true, true), Scores(3, 3, 3, 3), 2);
            CollectionAssert.AreEqual(new[] { 0, 1 }, picked);
        }

        [Test]
        public void Select_IneligibleNeverPicked_EvenWithHighScore()
        {
            List<int> picked = WorkshopShiftPlanner.SelectDutyIndexes(
                Flags(false, true, false, true), Scores(100, 1, 100, 2), 3);
            CollectionAssert.AreEqual(new[] { 3, 1 }, picked);
        }

        [Test]
        public void Select_CountExceedsCandidates_ReturnsAllEligible()
        {
            List<int> picked = WorkshopShiftPlanner.SelectDutyIndexes(
                Flags(true, false, true), Scores(1, 1, 1), 10);
            CollectionAssert.AreEqual(new[] { 0, 2 }, picked);
        }

        [Test]
        public void Select_NoEligibleOrEmptyInput_ReturnsEmpty()
        {
            Assert.AreEqual(0, WorkshopShiftPlanner.SelectDutyIndexes(Flags(false, false), Scores(1, 2), 3).Count);
            Assert.AreEqual(0, WorkshopShiftPlanner.SelectDutyIndexes(Flags(), Scores(), 3).Count);
            Assert.AreEqual(0, WorkshopShiftPlanner.SelectDutyIndexes(null, null, 3).Count);
        }

        [Test]
        public void Select_ScoresShorterThanFlags_TreatedAsZero()
        {
            List<int> picked = WorkshopShiftPlanner.SelectDutyIndexes(
                Flags(true, true, true), Scores(0, -1), 1);
            CollectionAssert.AreEqual(new[] { 0 }, picked);
        }

        /// <summary>
        /// 端到端回归（2026-10-03 · 用户实机 P0）：还原"第一车间 · 日班 08–17 + 夜班 20–08，
        /// 三名成员 Sky/Gill/Max，Max 挂夜班"这个真实场景。
        ///
        /// <para>缺陷形态：旧的"缺省 = 不限名额"让<b>日班</b>把整份名单吃光，于是 11:00
        /// （属日班窗口）夜班成员 Max 也被选上岗 ⇒ 他"到点不下班、不回床"，而人员页仍显示
        /// 他的「今日岗位 = 夜班」。</para>
        ///
        /// <para>本用例钉死修复后的行为：每个班次窗口各自只取 1 人 ⇒ 同一时刻不同班次
        /// 不会把同一批人全部拉上岗；且名额互不串（先到先得）。</para>
        /// </summary>
        [Test]
        public void MultiShift_DayAndNight_DoNotPullWholeRosterIntoEachWindow()
        {
            // 成员池：Sky(种植6) / Gill(射击9) / Max(手工9)
            var roster = new List<string> { "Sky", "Gill", "Max" };
            var skills = new Dictionary<string, int> { { "Sky", 6 }, { "Gill", 9 }, { "Max", 9 } };

            // ① 日班 08–17 在 11:00 命中，缺省名额 ⇒ 只取 1 人（该车间技能最高者 Gill）
            var dayFlags = new List<bool> { true, true, true };
            var dayScores = new List<int>();
            foreach (string name in roster) { dayScores.Add(skills[name]); }
            var dayPicked = WorkshopShiftPlanner.SelectDutyIndexes(dayFlags, dayScores, 0);
            Assert.AreEqual(1, dayPicked.Count,
                "日班窗口内只应上 1 人（旧语义会全选 3 人 => 夜班成员被日班拉上岗）");
            Assert.AreEqual("Gill", roster[dayPicked[0]]);

            // ② 夜班 20–08 在 11:00 **不命中** ⇒ 该班次根本不参与选人（时段门在调用方）
            Assert.IsFalse(InRangeLikeWorkshopTime(11, 20, 8),
                "夜班 20:00–08:00 在 11:00 必须判为不在窗口内");
            Assert.IsTrue(InRangeLikeWorkshopTime(23, 20, 8), "夜班在 23:00 必须命中");
            Assert.IsTrue(InRangeLikeWorkshopTime(3, 20, 8), "夜班跨午夜 3:00 必须命中");

            // ③ 夜班窗口内同样只上 1 人
            var nightPicked = WorkshopShiftPlanner.SelectDutyIndexes(dayFlags, dayScores, 0);
            Assert.AreEqual(1, nightPicked.Count);
        }

        /// <summary>
        /// 与 <c>Domain.WorkshopTime.InRange</c> 同口径的极简复刻（跨午夜环绕）。
        /// 单测项目不便引用 Verse 程序集，故在此按同一契约本地复算一次，
        /// 目的是把「时段门」与「名额门」两个判据一起钉住。
        /// </summary>
        private static bool InRangeLikeWorkshopTime(int hour, int start, int end)
        {
            if (start == end) { return true; }
            return start < end
                ? hour >= start && hour < end
                : hour >= start || hour < end;
        }
    }
}
