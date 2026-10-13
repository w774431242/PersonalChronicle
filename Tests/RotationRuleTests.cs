using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Automation;
using Verse;
// 原版另有 Verse.RotationMode（同名）⇒ 用别名消歧，避免 CS0104。
using RotationMode = PersonalChronicle.Automation.RotationMode;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 轮岗规则（<see cref="RotationRule"/>）下标机离线单测 —— B 域体检补的缺口。
    ///
    /// <para>离线约束：无法构造 <see cref="Pawn"/>，因此用 <c>null</c> 占位元素驱动
    /// **下标机**（Advance / Current / ClampActiveIndex / EffectivePoolCount）。
    /// 评分分支（SkillBased 选人）在池内全为 null 时按"首个"退化 —— 这正是要钉住的行为。</para>
    ///
    /// <para>背景（体检 P1-B1）：推进门槛过去读 <c>Pool.Count</c>，而自定义池从无编辑入口 ⇒
    /// 定时轮岗永不推进。修复后门槛看 <c>EffectivePool</c>（池空回落车间人员名单），
    /// 所以"有效池规模"就是整条轮岗链的关键量。</para>
    /// </summary>
    [TestFixture]
    public sealed class RotationRuleTests
    {
        /// <summary>建一条只有自定义池（无车间）的规则；元素用 null 占位，只测下标。</summary>
        private static RotationRule RuleWithPool(RotationMode mode, int size)
        {
            var rule = new RotationRule { Mode = mode };
            rule.Pool = new List<Pawn>();
            for (int i = 0; i < size; i++)
            {
                rule.Pool.Add(null);
            }
            return rule;
        }

        [Test]
        public void RoundRobin_AdvancesSequentiallyAndWraps()
        {
            RotationRule rule = RuleWithPool(RotationMode.RoundRobin, 3);
            Assert.AreEqual(0, rule.ActiveIndex);
            rule.Advance();
            Assert.AreEqual(1, rule.ActiveIndex);
            rule.Advance();
            Assert.AreEqual(2, rule.ActiveIndex);
            rule.Advance();
            Assert.AreEqual(0, rule.ActiveIndex);
        }

        [Test]
        public void PriorityBased_AlwaysResetsToFirst()
        {
            RotationRule rule = RuleWithPool(RotationMode.PriorityBased, 3);
            rule.ActiveIndex = 2;
            rule.Advance();
            Assert.AreEqual(0, rule.ActiveIndex);
        }

        [Test]
        public void SkillBased_WhenNoScorablePawn_FallsBackToFirstIndex()
        {
            // 池内全是 null（离线占位）→ 评分为 -1（不可用）→ 取首位，不抛异常。
            RotationRule rule = RuleWithPool(RotationMode.SkillBased, 2);
            rule.ActiveIndex = 1;
            rule.Advance();
            Assert.AreEqual(0, rule.ActiveIndex);
        }

        [Test]
        public void Advance_EmptyPoolAndNoZone_ResetsIndexToZero()
        {
            var rule = new RotationRule { Mode = RotationMode.RoundRobin };
            Assert.AreEqual(0, rule.EffectivePoolCount);
            rule.ActiveIndex = 5;
            rule.Advance();
            Assert.AreEqual(0, rule.ActiveIndex);
            Assert.IsNull(rule.Current);
        }

        [Test]
        public void ClampActiveIndex_OutOfRangeOrNegative_ResetsToZero()
        {
            RotationRule rule = RuleWithPool(RotationMode.RoundRobin, 2);
            rule.ActiveIndex = 9;
            rule.ClampActiveIndex();
            Assert.AreEqual(0, rule.ActiveIndex);

            rule.ActiveIndex = -3;
            rule.ClampActiveIndex();
            Assert.AreEqual(0, rule.ActiveIndex);
        }

        [Test]
        public void ClampActiveIndex_PoolEmptied_ResetsToZero()
        {
            // 体检 P2-⑨：purge 把池清空后，下标基准必须跟着"有效池"（此处回落为 0 规模）走。
            RotationRule rule = RuleWithPool(RotationMode.RoundRobin, 3);
            rule.ActiveIndex = 2;
            rule.Pool.Clear();
            rule.ClampActiveIndex();
            Assert.AreEqual(0, rule.ActiveIndex);
        }

        [Test]
        public void EffectivePoolCount_FallsBackToZoneRoster_WhenPoolEmpty()
        {
            var rule = new RotationRule { Pool = new List<Pawn>() };
            // 无车间 ⇒ 无法回落 ⇒ 0（有空车间时回落 zone.Members，需引擎实例，离线不测）。
            Assert.AreEqual(0, rule.EffectivePoolCount);
            rule.Pool.Add(null);
            rule.Pool.Add(null);
            Assert.AreEqual(2, rule.EffectivePoolCount);
        }

        [Test]
        public void Current_OutOfRangeIndex_ReadsFirstEntryWithoutMutating()
        {
            RotationRule rule = RuleWithPool(RotationMode.RoundRobin, 2);
            rule.ActiveIndex = 7;
            Assert.IsNull(rule.Current);   // 池首位是 null 占位 → 说明取的是首位而不是越界抛异常
            Assert.AreEqual(7, rule.ActiveIndex);
        }
    }
}
