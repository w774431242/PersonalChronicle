using System;
using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class PawnNeedSpeechTests
    {
        private static NumericNeedFact Reading(string id = "Food", float value = .2f, int now = 0,
            bool supported = true, bool shown = true, Fact<float>? current = null, Fact<bool> frozen = default)
            => new NumericNeedFact(id, id.ToLowerInvariant(), "vanilla." + id,
                current ?? Fact<float>.Known(value), Fact<float>.Known(1f), supported,
                "NeedLevel", 1, now, frozen, showOnNeedList: shown);

        [TestCase("Food", .30f)] [TestCase("Rest", .28f)] [TestCase("Joy", .25f)]
        [TestCase("Comfort", .25f)] [TestCase("Outdoors", .20f)]
        public void LowVisibleNeedsProduceTheMatchingGap(string id, float enter)
        {
            var cue = new PawnNeedSpeechState().Observe(new[] { Reading(id, enter) }, 0);
            Assert.That(cue.NeedId, Is.EqualTo(id));
            Assert.That(cue.TranslationKey, Is.EqualTo("PersonalChronicle.NeedSpeech." + id + ".Gap"));
        }
        [Test] public void PersistentGapDoesNotRepeatAfterCooldown()
        {
            var state = new PawnNeedSpeechState();
            Assert.That(state.Observe(new[] { Reading() }, 0), Is.Not.Null);
            Assert.That(state.Observe(new[] { Reading(now: 10000) }, 10000), Is.Null);
        }
        [Test] public void RecoveryRearmsAndSmallFluctuationsDoNot()
        {
            var state = new PawnNeedSpeechState();
            state.Observe(new[] { Reading() }, 0);
            Assert.That(state.Observe(new[] { Reading(value: .32f, now: 1500) }, 1500), Is.Null);
            Assert.That(state.Observe(new[] { Reading(value: .29f, now: 3000) }, 3000), Is.Null);
            Assert.That(state.Observe(new[] { Reading(value: .55f, now: 4500) }, 4500), Is.Null);
            Assert.That(state.Observe(new[] { Reading(now: 6000) }, 6000), Is.Not.Null);
        }
        [Test] public void UrgencyEscalatesOnceAndKeepsItsOwnMinimumCooldown()
        {
            var state = new PawnNeedSpeechState();
            state.Observe(new[] { Reading() }, 0);
            Assert.That(state.Observe(new[] { Reading(value: .05f, now: 299) }, 299), Is.Null);
            Assert.That(state.Observe(new[] { Reading(value: .05f, now: 300) }, 300).TranslationKey,
                Is.EqualTo("PersonalChronicle.NeedSpeech.Food.Urgent"));
            Assert.That(state.Observe(new[] { Reading(value: .01f, now: 2000) }, 2000), Is.Null);
        }
        [TestCase("Food", (int)NeedSpeechAction.Eating)]
        [TestCase("Rest", (int)NeedSpeechAction.Resting)]
        [TestCase("Joy", (int)NeedSpeechAction.Recreation)]
        public void ActionRequiresMatchingActualActivity(string id, int activity)
        {
            var action = (NeedSpeechAction)activity;
            var state = new PawnNeedSpeechState();
            Assert.That(state.Observe(new[] { Reading(id) }, 0).Acting, Is.False);
            Assert.That(state.Observe(new[] { Reading(id, now: 299) }, 299, action), Is.Null);
            Assert.That(state.Observe(new[] { Reading(id, now: 300) }, 300, action).Acting, Is.True);
            Assert.That(state.Observe(new[] { Reading(id, now: 3000) }, 3000, action), Is.Null);
        }
        [Test] public void WrongActivityDoesNotPromiseToEat()
        {
            var cue = new PawnNeedSpeechState().Observe(new[] { Reading() }, 0, NeedSpeechAction.Recreation);
            Assert.That(cue.TranslationKey, Does.EndWith(".Gap"));
        }
        [Test] public void ActualEatingCanBeTheFirstAnnouncement()
        {
            var cue = new PawnNeedSpeechState().Observe(new[] { Reading() }, 0, NeedSpeechAction.Eating);
            Assert.That(cue.TranslationKey, Does.EndWith(".Action"));
        }
        [Test] public void HungryPawnOnFirstObservationDoesNotInventARecoveredGap()
        {
            Assert.That(new PawnNeedSpeechState().Observe(new[] { Reading(value: .50f) }, 0,
                NeedSpeechAction.Eating), Is.Null);
        }
        [Test] public void OngoingGapCanAnnounceActionAboveEntryUntilRecovery()
        {
            var state = new PawnNeedSpeechState();
            state.Observe(new[] { Reading() }, 0);
            Assert.That(state.Observe(new[] { Reading(value: .4f, now: 300) }, 300,
                NeedSpeechAction.Eating).Acting, Is.True);
        }
        [Test] public void SuppressedOrCapacityLimitedDisplayDoesNotConsumeAnAnnouncement()
        {
            var state = new PawnNeedSpeechState();
            Assert.That(state.Observe(new[] { Reading() }, 0, allowSpeech: false), Is.Null);
            Assert.That(state.Observe(new[] { Reading(now: 10) }, 10), Is.Not.Null);
        }
        [Test] public void SuppressedDisplayStillRearmsRecoveredNeeds()
        {
            var state = new PawnNeedSpeechState();
            state.Observe(new[] { Reading() }, 0);
            state.Observe(new[] { Reading(value: .8f, now: 1500) }, 1500, allowSpeech: false);
            Assert.That(state.Observe(new[] { Reading(now: 3000) }, 3000), Is.Not.Null);
        }
        [Test] public void StaleFutureFrozenHiddenAndUnsupportedReadingsDoNotSpeak()
        {
            Assert.That(new PawnNeedSpeechState().Observe(new[] { Reading() }, 301), Is.Null);
            Assert.That(new PawnNeedSpeechState().Observe(new[] { Reading(now: 1) }, 0), Is.Null);
            Assert.That(new PawnNeedSpeechState().Observe(new[] { Reading(frozen: Fact<bool>.Known(true)) }, 0), Is.Null);
            Assert.That(new PawnNeedSpeechState().Observe(new[] { Reading(shown: false) }, 0), Is.Null);
            Assert.That(new PawnNeedSpeechState().Observe(new[] { Reading(supported: false) }, 0), Is.Null);
        }
        [Test] public void UnavailableAndInvalidValuesDoNotBecomeHunger()
        {
            foreach (var value in new[] { Fact<float>.Unknown("Loading"), Fact<float>.Error("Failed"),
                Fact<float>.Known(float.NaN), Fact<float>.Known(float.PositiveInfinity), Fact<float>.Known(-.5f) })
                Assert.That(new PawnNeedSpeechState().Observe(new[] { Reading(current: value) }, 0), Is.Null);
            Assert.That(new PawnNeedSpeechState().Observe(Array.Empty<NumericNeedFact>(), 0), Is.Null);
            Assert.That(new PawnNeedSpeechState().Observe(new[] { Reading("Mood"), Reading("ModThirst") }, 0), Is.Null);
        }
        [Test] public void UrgentFoodTakesPriorityOverRecreationAndOnlyWinnerIsConsumed()
        {
            var state = new PawnNeedSpeechState();
            Assert.That(state.Observe(new[] { Reading("Joy"), Reading(value: .05f) }, 0,
                NeedSpeechAction.Recreation).NeedId, Is.EqualTo("Food"));
            Assert.That(state.Observe(new[] { Reading("Joy", now: 1200), Reading(value: .05f, now: 1200) }, 1200,
                NeedSpeechAction.Recreation).NeedId, Is.EqualTo("Joy"));
        }
        [Test] public void SeparatePawnsAndTimeRollbackHaveSeparateMemory()
        {
            var first = new PawnNeedSpeechState();
            first.Observe(new[] { Reading(now: 10000) }, 10000);
            Assert.That(new PawnNeedSpeechState().Observe(new[] { Reading(now: 10000) }, 10000), Is.Not.Null);
            Assert.That(first.Observe(new[] { Reading(now: 10) }, 10), Is.Not.Null);
        }
        [Test] public void CooldownArithmeticDoesNotOverflowNearTickLimit()
        {
            var state = new PawnNeedSpeechState();
            int now = int.MaxValue - 10;
            Assert.That(state.Observe(new[] { Reading(now: now) }, now), Is.Not.Null);
            Assert.That(state.Observe(new[] { Reading(value: .05f, now: int.MaxValue) }, int.MaxValue), Is.Null);
        }
    }
}
