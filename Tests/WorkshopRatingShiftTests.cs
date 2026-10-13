using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain.Profession;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 评级<b>档位偏移</b>的离线单测 —— 这是车间 <c>ratingOffset</c> 的消费语义所在。
    ///
    /// <para>钉死的契约：① <c>order</c> 越小档位越高；② 偏移 0 返回原对象（零开销、零行为变化）；
    /// ③ 正偏移升档并在顶档夹住、负偏移降档并在底档夹住；④ **未达评级门槛（null）时即使正偏移
    /// 也保持 null** —— 车间环境不得凭空授予评级，只能放大已有评级；⑤ 不改动传入列表。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopRatingShiftTests
    {
        private static ProfessionalRatingDef Rating(int order, int minLevel)
        {
            return new ProfessionalRatingDef { order = order, minLevel = minLevel };
        }

        /// <summary>四档（order 0 最高 → 3 最低），与 Defs 里的评级 Def 同构。</summary>
        private static List<ProfessionalRatingDef> Defs()
        {
            return new List<ProfessionalRatingDef>
            {
                Rating(0, 1),
                Rating(1, 8),
                Rating(2, 16),
                Rating(3, 24)
            };
        }

        [Test]
        public void Shift_NullCurrent_StaysNullEvenWithPositiveShift()
        {
            List<ProfessionalRatingDef> defs = Defs();
            Assert.IsNull(ProfessionalRatingEvaluator.ShiftRating(null, 2, defs));
        }

        [Test]
        public void Shift_ZeroTiers_ReturnsSameInstance()
        {
            List<ProfessionalRatingDef> defs = Defs();
            ProfessionalRatingDef current = defs[2];
            Assert.AreSame(current, ProfessionalRatingEvaluator.ShiftRating(current, 0, defs));
        }

        [Test]
        public void Shift_Positive_MovesOneTierHigher()
        {
            List<ProfessionalRatingDef> defs = Defs();
            Assert.AreSame(defs[1], ProfessionalRatingEvaluator.ShiftRating(defs[2], 1, defs));
        }

        [Test]
        public void Shift_Positive_ClampsAtTopTier()
        {
            List<ProfessionalRatingDef> defs = Defs();
            Assert.AreSame(defs[0], ProfessionalRatingEvaluator.ShiftRating(defs[1], 5, defs));
        }

        [Test]
        public void Shift_Negative_MovesLower()
        {
            List<ProfessionalRatingDef> defs = Defs();
            Assert.AreSame(defs[3], ProfessionalRatingEvaluator.ShiftRating(defs[1], -2, defs));
        }

        [Test]
        public void Shift_Negative_ClampsAtBottomTier()
        {
            List<ProfessionalRatingDef> defs = Defs();
            Assert.AreSame(defs[3], ProfessionalRatingEvaluator.ShiftRating(defs[2], -9, defs));
        }

        [Test]
        public void Shift_NullOrEmptyDefs_ReturnsCurrent()
        {
            ProfessionalRatingDef current = Rating(1, 8);
            Assert.AreSame(current, ProfessionalRatingEvaluator.ShiftRating(current, 1, null));
            Assert.AreSame(current, ProfessionalRatingEvaluator.ShiftRating(current, 1, new List<ProfessionalRatingDef>()));
        }

        [Test]
        public void Shift_CurrentNotInList_ReturnsCurrent()
        {
            List<ProfessionalRatingDef> defs = Defs();
            ProfessionalRatingDef foreign = Rating(1, 8);
            Assert.AreSame(foreign, ProfessionalRatingEvaluator.ShiftRating(foreign, 1, defs));
        }

        [Test]
        public void Shift_DoesNotMutateInputList()
        {
            List<ProfessionalRatingDef> defs = Defs();
            Assert.AreSame(defs[0], ProfessionalRatingEvaluator.ShiftRating(defs[3], 3, defs));
            Assert.AreEqual(4, defs.Count);
            Assert.AreEqual(0, defs[0].order);
            Assert.AreEqual(3, defs[3].order);
        }
    }
}
