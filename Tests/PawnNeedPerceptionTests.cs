using System;
using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class PawnNeedPerceptionTests
    {
        private static NumericNeedFact Reading(float current = .4f, float maximum = 1f,
            string unit = "nutrition", bool supported = true, long revision = 7, int tick = 150)
            => new NumericNeedFact("Food", "food", "vanilla.food", Fact<float>.Known(current),
                Fact<float>.Known(maximum), supported, unit, revision, tick);

        private static NeedTarget Target(float ratio = .8f, long revision = 11)
            => new NeedTarget(TargetState.Enabled, Fact<float>.Known(ratio), revision);

        private static float Value(Fact<float> fact)
        {
            Assert.That(fact.TryGetValue(out float value), Is.True, fact.ReasonCode);
            return value;
        }

        [Test] public void DefaultFactIsUnknownAndCannotBeReadAsObservedZero()
        {
            var fact = default(Fact<float>);
            Assert.That(fact.State, Is.EqualTo(FactState.Unknown));
            Assert.That(fact.ReasonCode, Is.EqualTo(PerceptionReasons.Uninitialized));
            Assert.That(fact.TryGetValue(out _), Is.False);
            Assert.That(Value(Fact<float>.Known(0f)), Is.Zero);
        }

        [TestCase(FactState.Unknown)] [TestCase(FactState.NotApplicable)]
        [TestCase(FactState.Unsupported)] [TestCase(FactState.Error)]
        public void UnavailableFactsRetainReasonAndHideValue(int state)
        {
            var fact = Fact<float>.Unavailable((FactState)state, "source.failure");
            Assert.That(fact.State, Is.EqualTo((FactState)state));
            Assert.That(fact.ReasonCode, Is.EqualTo("source.failure"));
            Assert.That(fact.TryGetValue(out _), Is.False);
        }

        [Test] public void NullCannotBeAnObservedReference()
            => Assert.Throws<ArgumentNullException>(() => Fact<string>.Known(null));

        [TestCase(FactState.Known)] [TestCase((FactState)99)]
        public void UnavailableFactoryRejectsKnownAndUnknownEnumValues(int state)
            => Assert.Throws<ArgumentOutOfRangeException>(() => Fact<float>.Unavailable((FactState)state, "reason"));

        [Test] public void UnavailableRequiresAnExplanation()
            => Assert.Throws<ArgumentException>(() => Fact<float>.Unknown(" "));

        [TestCase(.4f, 1f, .8f, .5f, .4f, .4f)]
        [TestCase(.6f, 2f, .8f, .625f, .5f, 1f)]
        [TestCase(.9f, 1f, .8f, 0f, 0f, 0f)]
        [TestCase(0f, 2f, .8f, 1f, .8f, 1.6f)]
        [TestCase(1f, 1f, 1f, 0f, 0f, 0f)]
        public void GapPreservesRatioAndRawUnits(float current, float maximum, float target,
            float relative, float absolute, float raw)
        {
            var result = CurrentNeedProjector.Project("pawn", Reading(current, maximum), Target(target));
            Assert.That(Value(result.RelativeTargetGap), Is.EqualTo(relative).Within(.000001f));
            Assert.That(Value(result.AbsoluteRatioGap), Is.EqualTo(absolute).Within(.000001f));
            Assert.That(Value(result.RawLevelShortfall), Is.EqualTo(raw).Within(.000001f));
            Assert.That(result.RawUnitId, Is.EqualTo("nutrition"));
        }

        [Test] public void MissingTargetDoesNotEraseObservedActualRatio()
        {
            var result = CurrentNeedProjector.Project("pawn", Reading(), default);
            Assert.That(Value(result.ActualRatio), Is.EqualTo(.4f));
            Assert.That(result.RelativeTargetGap.State, Is.EqualTo(FactState.Unknown));
            Assert.That(result.RelativeTargetGap.ReasonCode, Is.EqualTo(PerceptionReasons.TargetMissing));
        }

        [TestCase(TargetState.Disabled, PerceptionReasons.TargetDisabled)]
        [TestCase(TargetState.NotApplicable, PerceptionReasons.TargetNotApplicable)]
        public void DisabledAndInapplicableTargetsAreNotSatisfiedGoals(int state, string reason)
        {
            var result = CurrentNeedProjector.Project("pawn", Reading(), new NeedTarget((TargetState)state, default, 3));
            Assert.That(Value(result.ActualRatio), Is.EqualTo(.4f));
            Assert.That(result.RelativeTargetGap.State, Is.EqualTo(FactState.NotApplicable));
            Assert.That(result.RawLevelShortfall.ReasonCode, Is.EqualTo(reason));
        }

        [TestCase(0f)] [TestCase(-1f)] [TestCase(1.1f)]
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidTargetCannotCreateAUsableGap(float target)
        {
            var result = CurrentNeedProjector.Project("pawn", Reading(), Target(target));
            Assert.That(result.RelativeTargetGap.State, Is.EqualTo(FactState.Error));
            Assert.That(result.RelativeTargetGap.ReasonCode, Is.EqualTo(PerceptionReasons.InvalidTargetRatio));
            Assert.That(Value(result.ActualRatio), Is.EqualTo(.4f));
        }

        [Test] public void UnknownTargetEnumFailsClosed()
        {
            var result = CurrentNeedProjector.Project("pawn", Reading(), new NeedTarget((TargetState)99, Fact<float>.Known(.8f), 1));
            Assert.That(result.RelativeTargetGap.ReasonCode, Is.EqualTo(PerceptionReasons.InvalidTargetState));
            Assert.That(result.RelativeTargetGap.State, Is.EqualTo(FactState.Error));
        }

        [TestCase(float.NaN, 1f)] [TestCase(float.PositiveInfinity, 1f)]
        [TestCase(.4f, float.NaN)] [TestCase(.4f, float.PositiveInfinity)]
        public void NonFiniteReadingsAreErrorsRatherThanZero(float current, float maximum)
        {
            var result = CurrentNeedProjector.Project("pawn", Reading(current, maximum), Target());
            Assert.That(result.RelativeTargetGap.State, Is.EqualTo(FactState.Error));
            Assert.That(result.RelativeTargetGap.ReasonCode, Is.EqualTo(PerceptionReasons.NonFiniteReading));
            Assert.That(result.ActualRatio.TryGetValue(out _), Is.False);
        }

        [TestCase(0f)] [TestCase(-1f)]
        public void NonPositiveMaximumCannotBeDivided(float maximum)
        {
            var result = CurrentNeedProjector.Project("pawn", Reading(.4f, maximum), Target());
            Assert.That(result.RawLevelShortfall.ReasonCode, Is.EqualTo(PerceptionReasons.NonPositiveMaximum));
        }

        [TestCase(-.1f)] [TestCase(1.1f)]
        public void OutOfRangeSemanticsAreUnsupportedRatherThanSilentlyClamped(float current)
        {
            var reading = Reading(current);
            var result = CurrentNeedProjector.Project("pawn", reading, Target());
            Assert.That(result.ActualRatio.State, Is.EqualTo(FactState.Unsupported));
            Assert.That(result.ActualRatio.ReasonCode, Is.EqualTo(PerceptionReasons.OutsideStandardRange));
            Assert.That(Value(reading.CurrentLevel), Is.EqualTo(current));
        }

        [TestCase(-.000001f, 0f)] [TestCase(1.000001f, 1f)]
        public void TinyBoundaryNoiseIsNormalizedOnlyInProjection(float current, float expectedRatio)
        {
            var reading = Reading(current);
            var result = CurrentNeedProjector.Project("pawn", reading, Target(1f));
            Assert.That(Value(result.ActualRatio), Is.EqualTo(expectedRatio));
            Assert.That(Value(result.RawLevelShortfall), Is.EqualTo(1f - expectedRatio).Within(.000001f));
            Assert.That(Value(reading.CurrentLevel), Is.EqualTo(current));
        }

        [Test] public void UnsupportedAdapterCannotDriveStandardRatioEconomics()
        {
            var result = CurrentNeedProjector.Project("pawn", Reading(supported: false), Target());
            Assert.That(result.RelativeTargetGap.State, Is.EqualTo(FactState.Unsupported));
            Assert.That(result.RelativeTargetGap.ReasonCode, Is.EqualTo(PerceptionReasons.UnsupportedNumericSemantics));
        }

        [Test] public void MissingRawUnitDoesNotPreventValidRatioProjection()
        {
            var result = CurrentNeedProjector.Project("pawn", Reading(unit: null), Target());
            Assert.That(Value(result.RelativeTargetGap), Is.EqualTo(.5f));
            Assert.That(result.RawLevelShortfall.State, Is.EqualTo(FactState.Unsupported));
            Assert.That(result.RawLevelShortfall.ReasonCode, Is.EqualTo(PerceptionReasons.RawUnitUnknown));
        }

        [TestCase(FactState.Unknown)] [TestCase(FactState.NotApplicable)]
        [TestCase(FactState.Unsupported)] [TestCase(FactState.Error)]
        public void ReadingFailurePropagatesWithoutInventingAGap(int state)
        {
            var reading = new NumericNeedFact("Food", "food", "vanilla.food",
                Fact<float>.Unavailable((FactState)state, "read.failure"), Fact<float>.Known(1f), true, "nutrition", 7, 150);
            var result = CurrentNeedProjector.Project("pawn", reading, Target());
            Assert.That(result.RelativeTargetGap.State, Is.EqualTo((FactState)state));
            Assert.That(result.RawLevelShortfall.ReasonCode, Is.EqualTo("read.failure"));
        }

        [Test] public void MissingMaximumAndMissingEnabledTargetRemainUnknown()
        {
            var reading = new NumericNeedFact("Food", "food", "vanilla.food", Fact<float>.Known(.4f),
                Fact<float>.Unknown("max.unread"), true, "nutrition", 7, 150);
            var result = CurrentNeedProjector.Project("pawn", reading, Target());
            Assert.That(result.ActualRatio.ReasonCode, Is.EqualTo("max.unread"));
            var missingTarget = CurrentNeedProjector.Project("pawn", Reading(), new NeedTarget(TargetState.Enabled, default, 3));
            Assert.That(missingTarget.RelativeTargetGap.State, Is.EqualTo(FactState.Unknown));
        }

        [Test] public void FrozenStateIsUnknownAndDoesNotInventAPrediction()
        {
            var reading = Reading();
            Assert.That(reading.IsFrozen.State, Is.EqualTo(FactState.Unknown));
            Assert.That(Value(CurrentNeedProjector.Project("pawn", reading, Target()).RelativeTargetGap), Is.EqualTo(.5f));
        }

        [Test] public void FactAndTargetRevisionsRemainSeparateWithoutChangingIdentity()
        {
            var first = CurrentNeedProjector.Project("pawn", Reading(), Target());
            var second = CurrentNeedProjector.Project("pawn", Reading(revision: 8, tick: 300), Target(.9f, 12));
            Assert.That(second.RequirementKey, Is.EqualTo(first.RequirementKey));
            Assert.That(second.FactRevision, Is.EqualTo(8));
            Assert.That(second.TargetRevision, Is.EqualTo(12));
            Assert.That(second.CapturedTick, Is.EqualTo(300));
            Assert.That(first.FactRevision, Is.EqualTo(7));
            Assert.That(Value(first.TargetRatio), Is.EqualTo(.8f));
            Assert.That(second.RelativeTargetGap.TryGetValue(out _), Is.True);
        }

        [Test] public void BeneficiaryNeedAndComponentHaveDistinctStableIdentities()
        {
            Assert.That(RequirementIdentity.ForNeed("a:b", "c", "level"),
                Is.Not.EqualTo(RequirementIdentity.ForNeed("a", "b:c", "level")));
            Assert.That(RequirementIdentity.ForNeed("a", "Food", "level"),
                Is.Not.EqualTo(RequirementIdentity.ForNeed("b", "Food", "level")));
            Assert.That(RequirementIdentity.ForNeed("a", "Food", "level"),
                Is.Not.EqualTo(RequirementIdentity.ForNeed("a", "Rest", "level")));
            Assert.That(RequirementIdentity.ForNeed("a", "Food", "level"),
                Is.Not.EqualTo(RequirementIdentity.ForNeed("a", "Food", "event")));
        }

        [Test] public void VerySmallFiniteMaximumDoesNotOverflowRatioArithmetic()
        {
            var result = CurrentNeedProjector.Project("pawn", Reading(float.Epsilon, float.Epsilon), Target(1f));
            Assert.That(Value(result.ActualRatio), Is.EqualTo(1f));
            Assert.That(Value(result.RawLevelShortfall), Is.Zero);
            var outside = CurrentNeedProjector.Project("pawn", Reading(float.MaxValue, float.Epsilon), Target());
            Assert.That(outside.ActualRatio.State, Is.EqualTo(FactState.Unsupported));
        }

        [Test] public void VeryLargeFiniteMaximumKeepsValidRawUnits()
        {
            var result = CurrentNeedProjector.Project("pawn", Reading(0f, float.MaxValue), Target(1f));
            Assert.That(Value(result.RelativeTargetGap), Is.EqualTo(1f));
            Assert.That(Value(result.RawLevelShortfall), Is.EqualTo(float.MaxValue));
        }

        [Test] public void InvalidIdentityAndNegativeVersionsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => CurrentNeedProjector.Project(" ", Reading(), Target()));
            Assert.Throws<ArgumentNullException>(() => CurrentNeedProjector.Project("pawn", null, Target()));
            Assert.Throws<ArgumentOutOfRangeException>(() => Reading(revision: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Reading(tick: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Target(revision: -1));
        }
    }
}
