using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 作息策略纯逻辑离线单测（CHG-20261003-WORKSHOP-SPLIT · 修成因 A）。
    ///
    /// <para><b>钉死的核心契约</b>：策略枚举的语义边界必须稳定 ——
    /// ① <c>FixedPerShift</c> 是默认档；② <c>NaturalRest</c> <b>不写</b>原版作息表；
    /// ③ 只有 <c>RotateDaily</c> 会翻转睡眠时段（UI 警示的唯一依据）；
    /// ④ 越界/未知枚举值必须<b>回落默认档而不是抛异常</b>（存档里可能有过期值）。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopRestPolicyTests
    {
        [Test]
        public void DefaultKind_IsFixedPerShift_NotRotateDaily()
        {
            // 回归钉子：默认档必须是 FixedPerShift。
            // 旧默认行为 = RotateDaily（按当日轮换改写作息表）⇒ 正是成因 A 的根因。
            Assert.AreEqual(WorkshopRestPolicyKind.FixedPerShift,
                WorkshopShiftPolicy.Normalize(default(WorkshopRestPolicyKind)),
                "默认策略必须是 FixedPerShift（岗位永久绑定一班，不按天翻转）");
        }

        [Test]
        public void WritesTimetable_NaturalRestIsFalse_OthersTrue()
        {
            Assert.IsFalse(WorkshopShiftPolicy.WritesTimetable(
                WorkshopRestPolicyKind.NaturalRest),
                "NaturalRest 的定义就是不写原版作息表（不得覆盖玩家手排）");
            Assert.IsTrue(WorkshopShiftPolicy.WritesTimetable(
                WorkshopRestPolicyKind.FixedPerShift));
            Assert.IsTrue(WorkshopShiftPolicy.WritesTimetable(
                WorkshopRestPolicyKind.RotateDaily));
        }

        [Test]
        public void FlipsSleepAcrossDays_OnlyRotateDaily()
        {
            // UI 的警示条件（GOV-010 强制）只能由这一条驱动。
            Assert.IsTrue(WorkshopShiftPolicy.FlipsSleepAcrossDays(
                WorkshopRestPolicyKind.RotateDaily),
                "RotateDaily 按当日轮换改写作息表 ⇒ 必然翻转睡眠时段，必须警示");
            Assert.IsFalse(WorkshopShiftPolicy.FlipsSleepAcrossDays(
                WorkshopRestPolicyKind.FixedPerShift),
                "FixedPerShift 不按天变化 ⇒ 不得警示（否则误报）");
            Assert.IsFalse(WorkshopShiftPolicy.FlipsSleepAcrossDays(
                WorkshopRestPolicyKind.NaturalRest));
        }

        [Test]
        public void Normalize_UnknownValue_FallsBackToDefault_NoThrow()
        {
            // 存档里可能残留被移除的档位值；读档路径绝不能抛异常。
            Assert.AreEqual(WorkshopRestPolicyKind.FixedPerShift,
                WorkshopShiftPolicy.Normalize((WorkshopRestPolicyKind)99));
            Assert.AreEqual(WorkshopRestPolicyKind.FixedPerShift,
                WorkshopShiftPolicy.Normalize((WorkshopRestPolicyKind)255));
        }

        [Test]
        public void NormalizeMinContinuousHours_ClampsToSafeRange()
        {
            Assert.AreEqual(WorkshopShiftPolicy.DefaultMinContinuousHours,
                WorkshopShiftPolicy.NormalizeMinContinuousHours(0),
                "未配置（0）必须走缺省值，而不是 0（0 会让到点即收工，等于没修）");
            Assert.AreEqual(WorkshopShiftPolicy.DefaultMinContinuousHours,
                WorkshopShiftPolicy.NormalizeMinContinuousHours(-3));
            Assert.AreEqual(3, WorkshopShiftPolicy.NormalizeMinContinuousHours(3));
            Assert.AreEqual(24, WorkshopShiftPolicy.NormalizeMinContinuousHours(99),
                "上限 24（全天班不可能连续更久）");
        }
    }
}