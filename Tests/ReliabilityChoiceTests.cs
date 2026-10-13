using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Learning;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// M9 可靠性取舍。三条边界各有用例：中性偏好不改变选择 / 证据不足不得当作不可靠 /
    /// 无替代时不得放弃收入。任何一条缺失都会让"有限取舍"变形。
    /// </summary>
    [TestFixture]
    public sealed class ReliabilityChoiceTests
    {
        [Test] public void ANeutralPreferenceNeverChangesTheChoice()
        {
            Assert.That(ReliabilityChoiceRules.Avoid(ReliabilityChoiceRules.NeutralPreference, true), Is.False,
                "0.5 是显式中性默认，不是已测量人格 —— 中性不得改变任何选择");
            Assert.That(ReliabilityChoiceRules.Avoid(0.4f, true), Is.False);
        }

        [Test] public void OnlyAConfirmedLowConfidenceRecordIsAvoided()
        {
            // 证据不足（Unproven）时 WageCredibility.IsLowConfidence 为 false ⇒ 不得避开。
            Assert.That(ReliabilityChoiceRules.Avoid(0.9f, false), Is.False,
                "不得把『没有证据』当成『不可靠』");
            Assert.That(ReliabilityChoiceRules.Avoid(0.9f, true), Is.True);
        }

        [Test] public void TheTradeoffIsLimitedAndNeverGivesUpIncome()
        {
            // 无替代候选 ⇒ 不切换（仍签约，只标已知风险）；有替代 ⇒ 切换。
            Assert.That(ReliabilityChoiceRules.ShouldSwitchToAlternative(0.9f, true, alternativeAvailable: false), Is.False,
                "以『求稳』之名放弃收入不是设计的『有限取舍』");
            Assert.That(ReliabilityChoiceRules.ShouldSwitchToAlternative(0.9f, true, alternativeAvailable: true), Is.True);
        }

        [Test] public void SwitchingRequiresBothAPreferenceAndARealAlternative()
        {
            // 四象限：偏好不足 / 当前非低可信度 / 无替代，任一不成立都不切换。
            Assert.That(ReliabilityChoiceRules.ShouldSwitchToAlternative(ReliabilityChoiceRules.NeutralPreference, true, true), Is.False);
            Assert.That(ReliabilityChoiceRules.ShouldSwitchToAlternative(0.9f, false, true), Is.False);
            Assert.That(ReliabilityChoiceRules.ShouldSwitchToAlternative(0.4f, true, true), Is.False);
            Assert.That(ReliabilityChoiceRules.ShouldSwitchToAlternative(0.9f, true, true), Is.True);
        }
    }
}
