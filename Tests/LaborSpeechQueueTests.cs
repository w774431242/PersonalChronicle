using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class LaborSpeechQueueTests
    {
        private static bool Visible(LaborSpeechQueue.Entry entry) => true;
        [Test] public void BusinessReplayIsDeduplicatedAfterPlaybackWithoutTickDependency()
        {
            var queue = new LaborSpeechQueue();
            Assert.That(queue.Enqueue("p", "payment-42", "Paid", null, null, 2), Is.True);
            queue.Consume(queue.Peek(Visible));
            queue.Observe(999, Visible);
            Assert.That(queue.Enqueue("p", "payment-42", "Paid", null, null, 2), Is.False);
            Assert.That(queue.DuplicateCount, Is.EqualTo(1));
        }
        [Test] public void TwoPaymentsInOneFrameRemainTwoEventsInBusinessOrder()
        {
            var queue = new LaborSpeechQueue();
            queue.Enqueue("p", "payment-42", "Paid", null, new object[] { "8.40" }, 2);
            queue.Enqueue("p", "payment-43", "Paid", null, new object[] { "4.20" }, 2);
            var first = queue.Peek(Visible);
            Assert.That(first.Args[0], Is.EqualTo("8.40")); queue.Consume(first);
            Assert.That(queue.Peek(Visible).EventId, Is.EqualTo("payment-43"));
        }
        [Test] public void PendingIdentitySurvivesRecentRingEviction()
        {
            var queue = new LaborSpeechQueue(recentLimit: 1);
            queue.Enqueue("p", "first", "Verify", null, null, 0);
            queue.Enqueue("p", "second", "Paid", null, null, 2);
            Assert.That(queue.Enqueue("p", "first", "Verify", null, null, 0), Is.False);
        }
        [Test] public void RealBusinessSequenceFitsAndVerificationIsNotImmediatelyLost()
        {
            var queue = new LaborSpeechQueue();
            foreach (string state in new[] { "Verify", "AwaitPayment", "Submitted", "Paid" })
                Assert.That(queue.Enqueue("p", state, state, null, null, state == "Paid" ? 2 : 0), Is.True);
            Assert.That(queue.Peek(Visible).Key, Is.EqualTo("Verify"));
            Assert.That(queue.OverflowCount, Is.Zero);
        }
        [Test] public void OverflowProtectsFinancialFeedbackAndReportsLoss()
        {
            var queue = new LaborSpeechQueue(perPawnLimit: 2);
            queue.Enqueue("p", "paid", "Paid", null, null, 2);
            queue.Enqueue("p", "failure", "NoFunds", null, null, 2);
            Assert.That(queue.Enqueue("p", "waiting", "Waiting", null, null, 0), Is.False);
            Assert.That(queue.OverflowCount, Is.EqualTo(1));
            Assert.That(queue.Peek(Visible).Key, Is.EqualTo("Paid"));
        }
        [Test] public void GloballyFullQueueAllowsFinancialFeedbackToReplaceLowPriorityPawn()
        {
            var queue = new LaborSpeechQueue(pawnLimit: 1);
            queue.Enqueue("a", "wait", "Waiting", null, null, 0);
            Assert.That(queue.Enqueue("b", "paid", "Paid", null, null, 2), Is.True);
            Assert.That(queue.PawnCount, Is.EqualTo(1));
            Assert.That(queue.Peek(Visible).PawnId, Is.EqualTo("b"));
            Assert.That(queue.OverflowCount, Is.EqualTo(1));
        }
        [Test] public void HiddenOffMapOrSleepingEventsDoNotConsumeVisibleLifetime()
        {
            var queue = new LaborSpeechQueue(visibleLifetime: 10);
            queue.Enqueue("p", "sign", "Signed", null, null, 1);
            queue.Observe(0, entry => false); queue.Observe(10000, entry => false);
            Assert.That(queue.Peek(entry => false), Is.Null);
            queue.Observe(10001, Visible);
            Assert.That(queue.Peek(Visible), Is.Not.Null);
            queue.Observe(10010, Visible);
            Assert.That(queue.Peek(Visible), Is.Not.Null);
            queue.Observe(10011, Visible);
            Assert.That(queue.Peek(Visible), Is.Null);
            Assert.That(queue.ExpiredCount, Is.EqualTo(1));
        }
        [Test] public void PeekDoesNotConsumeAndMissingEligibilityCannotLoseEvent()
        {
            var queue = new LaborSpeechQueue();
            queue.Enqueue("p", "event", "Signed", null, null, 1);
            Assert.That(queue.Peek(entry => false), Is.Null);
            var entry = queue.Peek(Visible);
            Assert.That(queue.Peek(Visible), Is.SameAs(entry));
            Assert.That(queue.Consume(entry), Is.True);
            Assert.That(queue.Consume(entry), Is.False);
        }
        [Test] public void EnqueuedArgumentsAreAStableSnapshot()
        {
            var queue = new LaborSpeechQueue(); var args = new object[] { "8.40" };
            queue.Enqueue("p", "paid", "Paid", null, args, 2); args[0] = "999";
            Assert.That(queue.Peek(Visible).Args[0], Is.EqualTo("8.40"));
        }
        [Test] public void FreshEventDoesNotInheritEarlierEventsVisibleWait()
        {
            var queue = new LaborSpeechQueue(visibleLifetime: 10);
            queue.Enqueue("p", "old", "Waiting", null, null, 0); queue.Observe(0, Visible);
            queue.Enqueue("p", "new", "Paid", null, null, 2); queue.Observe(10, Visible);
            Assert.That(queue.Peek(Visible).EventId, Is.EqualTo("new"));
            Assert.That(queue.Peek(Visible).VisibleWait, Is.Zero);
        }
        [Test] public void ClearStartsNewPresentationSessionWithoutOldDeduplication()
        {
            var queue = new LaborSpeechQueue(); queue.Enqueue("p", "event", "Signed", null, null, 1);
            queue.Clear();
            Assert.That(queue.PawnCount, Is.Zero);
            Assert.That(queue.Enqueue("p", "event", "Signed", null, null, 1), Is.True);
        }
        [Test] public void PermanentlyInvalidSubjectCannotExhaustTerminalPrioritySlots()
        {
            var queue = new LaborSpeechQueue(pawnLimit: 1);
            queue.Enqueue("dead", "paid", "Paid", "dead", null, 2);
            Assert.That(queue.Enqueue("new", "paid", "Paid", "alive", null, 2), Is.False);
            queue.DiscardWhere(entry => (string)entry.Payload == "dead");
            Assert.That(queue.DiscardedCount, Is.EqualTo(1));
            Assert.That(queue.Enqueue("new", "paid", "Paid", "alive", null, 2), Is.True);
        }
        [Test] public void PermanentInvalidationPreservesSleepingOtherMapAndCaravanSubjects()
        {
            var queue = new LaborSpeechQueue();
            foreach (string status in new[] { "dead", "sleep", "otherMap", "caravan" })
                queue.Enqueue(status, "event", "Paid", status, null, 2);
            queue.DiscardWhere(entry => (string)entry.Payload == "dead");
            queue.Observe(0, entry => false); queue.Observe(10000, entry => false);
            Assert.That(queue.PawnCount, Is.EqualTo(3));
            Assert.That(queue.DiscardedCount, Is.EqualTo(1));
            foreach (string status in new[] { "sleep", "otherMap", "caravan" })
                Assert.That(queue.Peek(entry => entry.PawnId == status), Is.Not.Null);
        }
    }
}
