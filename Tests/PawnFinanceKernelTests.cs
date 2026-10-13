using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Finance;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class PawnFinanceKernelTests
    {
        private static ShadowTransactionKernel Book(long balance = 1000, long reserved = 0, int capacity = 4096)
            => new ShadowTransactionKernel(new[] { new FinancialAccountSnapshot("public", balance, reserved),
                new FinancialAccountSnapshot("pawn", 0), new FinancialAccountSnapshot("shop", 0) }, capacity);
        private static FinancialIntent Pay(ShadowTransactionKernel book, string id = "wage", long amount = 100,
            long expiry = 100, string resource = "worker:pawn", string debit = "public", string credit = "pawn")
            => new FinancialIntent(id, book.Version, expiry, new[] { new FinancialTransferLeg(debit, credit, amount) },
                resource == null ? null : new[] { resource });
        private static FinancialAccountSnapshot Account(ShadowTransactionKernel book, string id = "public")
            => book.ReadAccounts().Single(a => a.Id == id);

        [TestCase(0, 0)] [TestCase(1, 100)] [TestCase(10000000000L, 1000000000000L)]
        public void SilverUsesExactIntegerMinorUnits(long silver, long minor)
            => Assert.That(MinorCurrency.FromSilver(silver), Is.EqualTo(minor));
        [TestCase(-1)] [TestCase(10000000001L)] [TestCase(long.MaxValue)]
        public void SilverConversionRejectsInvalidOrOverflowingInput(long silver)
            => Assert.Throws<ArgumentOutOfRangeException>(() => MinorCurrency.FromSilver(silver));
        [TestCase(0, 0, 0)] [TestCase(99, 0, 99)] [TestCase(101, 1, 1)]
        public void WithdrawableWholeSilverDoesNotRoundAwayFractions(long minor, long whole, long remainder)
        { Assert.That(MinorCurrency.WholeSilver(minor), Is.EqualTo(whole)); Assert.That(MinorCurrency.Remainder(minor), Is.EqualTo(remainder)); }

        [Test]
        public void PreparationAndCommitKeepCurrencyConservedAndPreserveForeignReservations()
        {
            var book = Book(reserved: 50); var before = book.ReadAccounts(); var intent = Pay(book);
            Assert.That(book.Prepare(intent, 0).Code, Is.EqualTo(FinancialResultCode.Applied));
            Assert.That(Account(book).ReservedMinor, Is.EqualTo(150)); Assert.That(book.IsResourceHeld("worker:pawn"), Is.True);
            Assert.That(book.Commit("wage", book.Version, 1, "job:123").Accepted, Is.True);
            Assert.That(Account(book).BalanceMinor, Is.EqualTo(900)); Assert.That(Account(book).ReservedMinor, Is.EqualTo(50));
            Assert.That(Account(book, "pawn").BalanceMinor, Is.EqualTo(100)); Assert.That(book.IsResourceHeld("worker:pawn"), Is.False);
            Assert.That(book.ReadAccounts().Sum(a => a.BalanceMinor), Is.EqualTo(before.Sum(a => a.BalanceMinor)));
            Assert.That(before[1].BalanceMinor, Is.EqualTo(1000), "Earlier snapshot must not change");
            Assert.That(book.CanExecuteInGame, Is.False); Assert.That(book.BackingVerified, Is.False);
        }
        [Test]
        public void MultiLegBundleCannotFinanceItsOwnOutgoingPaymentWithPromisedIncomingMoney()
        {
            var book = Book();
            var intent = new FinancialIntent("round", 0, 10, new[] { new FinancialTransferLeg("public", "pawn", 100),
                new FinancialTransferLeg("pawn", "shop", 100) }, new[] { "bed:1" });
            Assert.That(book.Prepare(intent, 0).Code, Is.EqualTo(FinancialResultCode.InsufficientFunds));
            Assert.That(book.Version, Is.Zero); Assert.That(Account(book).ReservedMinor, Is.Zero);
            Assert.That(book.IsResourceHeld("bed:1"), Is.False); Assert.That(book.ReadTransaction("round"), Is.Null);
        }
        [Test]
        public void SumOfSeveralDebitsCannotOverspendOneAccount()
        {
            var book = Book(); var intent = new FinancialIntent("split", 0, 10, new[] {
                new FinancialTransferLeg("public", "pawn", 600), new FinancialTransferLeg("public", "shop", 600) });
            Assert.That(book.Prepare(intent, 0).Code, Is.EqualTo(FinancialResultCode.InsufficientFunds));
            Assert.That(Account(book).ReservedMinor, Is.Zero);
        }
        [Test]
        public void DifferentNeedsCannotReserveTheSameAvailableMoney()
        {
            var book = Book(); book.Prepare(Pay(book, "food", 700, resource: "food:1"), 0);
            Assert.That(book.Prepare(Pay(book, "bed", 400, resource: "bed:1"), 0).Code, Is.EqualTo(FinancialResultCode.InsufficientFunds));
            Assert.That(Account(book).ReservedMinor, Is.EqualTo(700)); Assert.That(book.IsResourceHeld("bed:1"), Is.False);
        }
        [Test]
        public void ResourceConflictRejectsWholeBundleAndLeavesFundsUntouched()
        {
            var book = Book(); book.Prepare(Pay(book, "first"), 0); long version = book.Version;
            Assert.That(book.Prepare(Pay(book, "second"), 0).Code, Is.EqualTo(FinancialResultCode.ResourceBusy));
            Assert.That(Account(book).ReservedMinor, Is.EqualTo(100)); Assert.That(book.Version, Is.EqualTo(version));
        }
        [Test]
        public void MissingCreditAccountCannotCreateAnAccountOrReserveFunds()
        {
            var book = Book();
            Assert.That(book.Prepare(Pay(book, credit: "absent"), 0).Code, Is.EqualTo(FinancialResultCode.MissingAccount));
            Assert.That(book.ReadAccounts().Count, Is.EqualTo(3)); Assert.That(Account(book).ReservedMinor, Is.Zero);
        }
        [Test]
        public void CreditHeadroomIsReservedAcrossBundlesAndCommitOrderCannotOverflow()
        {
            var book = new ShadowTransactionKernel(new[] { new FinancialAccountSnapshot("a", 1000),
                new FinancialAccountSnapshot("b", 1000), new FinancialAccountSnapshot("sink", MinorCurrency.Maximum - 150) });
            var first = Pay(book, debit: "a", credit: "sink", resource: null); book.Prepare(first, 0);
            Assert.That(book.Prepare(Pay(book, "second", debit: "b", credit: "sink", resource: null), 0).Code, Is.EqualTo(FinancialResultCode.AmountLimit));
            Assert.That(book.Commit("wage", book.Version, 1, "e1").Accepted, Is.True);
            Assert.That(Account(book, "sink").BalanceMinor, Is.EqualTo(MinorCurrency.Maximum - 50));
        }
        [Test]
        public void CancellationReleasesReservedCreditHeadroomForAnotherPayment()
        {
            var book = new ShadowTransactionKernel(new[] { new FinancialAccountSnapshot("a", 1000),
                new FinancialAccountSnapshot("b", 1000), new FinancialAccountSnapshot("sink", MinorCurrency.Maximum - 100) });
            book.Prepare(Pay(book, "first", debit: "a", credit: "sink", resource: null), 0);
            book.Cancel("first", book.Version, 0);
            Assert.That(book.Prepare(Pay(book, "second", debit: "b", credit: "sink", resource: null), 0).Accepted, Is.True);
            book.Commit("second", book.Version, 1, "e"); Assert.That(Account(book, "sink").BalanceMinor, Is.EqualTo(MinorCurrency.Maximum));
        }
        [Test]
        public void SeveralCreditReservationsMayCommitInReverseOrder()
        {
            var book = new ShadowTransactionKernel(new[] { new FinancialAccountSnapshot("a", 1000),
                new FinancialAccountSnapshot("b", 1000), new FinancialAccountSnapshot("sink", MinorCurrency.Maximum - 200) });
            book.Prepare(Pay(book, "first", debit: "a", credit: "sink", resource: null), 0);
            book.Prepare(Pay(book, "second", debit: "b", credit: "sink", resource: null), 0);
            Assert.That(book.Commit("second", book.Version, 1, "e2").Accepted, Is.True);
            Assert.That(book.Commit("first", book.Version, 1, "e1").Accepted, Is.True);
            Assert.That(Account(book, "sink").BalanceMinor, Is.EqualTo(MinorCurrency.Maximum));
        }
        [Test]
        public void OldSnapshotVersionsCannotCancelNewReservationsOrReverseCommittedPayments()
        {
            var book = Book(); book.Prepare(Pay(book), 0);
            Assert.That(book.Cancel("wage", 0, 1).Code, Is.EqualTo(FinancialResultCode.StaleVersion));
            Assert.That(Account(book).ReservedMinor, Is.EqualTo(100)); book.Commit("wage", book.Version, 1, "e");
            Assert.That(book.Cancel("wage", book.Version, 1).Code, Is.EqualTo(FinancialResultCode.InvalidPhase));
            Assert.That(Account(book).BalanceMinor, Is.EqualTo(900));
        }
        [Test]
        public void UnknownAndAlreadyExpiredCommandsDoNotCreateJournalEntries()
        {
            var book = Book();
            Assert.That(book.Commit("absent", 0, 5, "e").Code, Is.EqualTo(FinancialResultCode.UnknownCommand));
            Assert.That(book.Cancel("absent", 0, 5).Code, Is.EqualTo(FinancialResultCode.UnknownCommand));
            Assert.That(book.Prepare(Pay(book, expiry: 5), 5).Code, Is.EqualTo(FinancialResultCode.Expired));
            Assert.That(book.ReadTransaction("wage"), Is.Null); Assert.That(book.Version, Is.Zero);
        }
        [Test]
        public void StalePrepareAndCommitAreRejectedWithoutMutatingHolds()
        {
            var book = Book(); var first = Pay(book); var stale = Pay(book, "stale", resource: null); book.Prepare(first, 0);
            Assert.That(book.Prepare(stale, 0).Code, Is.EqualTo(FinancialResultCode.StaleVersion));
            Assert.That(book.Commit("wage", 0, 1, "e").Code, Is.EqualTo(FinancialResultCode.StaleVersion));
            Assert.That(Account(book).BalanceMinor, Is.EqualTo(1000)); Assert.That(Account(book).ReservedMinor, Is.EqualTo(100));
        }
        [Test]
        public void RepeatedPrepareAndCommitDoNotDoubleReserveOrDoubleCharge()
        {
            var book = Book(); var intent = Pay(book); book.Prepare(intent, 0);
            Assert.That(book.Prepare(intent, 1).Code, Is.EqualTo(FinancialResultCode.Replay)); Assert.That(book.Version, Is.EqualTo(1));
            book.Commit("wage", 1, 2, "e");
            Assert.That(book.Commit("wage", 0, 3, "e").Code, Is.EqualTo(FinancialResultCode.Replay));
            Assert.That(book.Version, Is.EqualTo(2)); Assert.That(Account(book).BalanceMinor, Is.EqualTo(900));
            Assert.That(book.Prepare(intent, 4).Transaction.Phase, Is.EqualTo(FinancialPhase.Committed));
        }
        [Test]
        public void SameCommandIdWithDifferentBodyIsRejectedEvenAfterCompletion()
        {
            var book = Book(); book.Prepare(Pay(book), 0); book.Cancel("wage", 1, 0);
            Assert.That(book.Prepare(Pay(book, amount: 200), 1).Code, Is.EqualTo(FinancialResultCode.CommandConflict));
            Assert.That(Account(book).ReservedMinor, Is.Zero);
        }
        [Test]
        public void ExpiryBoundaryReleasesMoneyAndResourceWithoutCommitting()
        {
            var book = Book(); book.Prepare(Pay(book, expiry: 5), 0);
            Assert.That(book.Commit("wage", 1, 5, "e").Code, Is.EqualTo(FinancialResultCode.Expired));
            Assert.That(Account(book).BalanceMinor, Is.EqualTo(1000)); Assert.That(Account(book).ReservedMinor, Is.Zero);
            Assert.That(book.IsResourceHeld("worker:pawn"), Is.False);
            Assert.That(book.Commit("wage", 2, 6, "e").Code, Is.EqualTo(FinancialResultCode.InvalidPhase));
        }
        [Test]
        public void ReplayingExpiredPrepareDoesNotRenewClaim()
        {
            var book = Book(); var intent = Pay(book, expiry: 5); book.Prepare(intent, 0);
            Assert.That(book.Prepare(intent, 5).Transaction.Phase, Is.EqualTo(FinancialPhase.Expired));
            Assert.That(Account(book).ReservedMinor, Is.Zero); Assert.That(book.Prepare(intent, 6).Version, Is.EqualTo(2));
        }
        [Test]
        public void BulkExpiryAndCancellationReleaseExactlyOnce()
        {
            var book = Book(); book.Prepare(Pay(book, "a", expiry: 5, resource: "a"), 0);
            book.Prepare(Pay(book, "b", expiry: 10, resource: "b"), 0); book.Expire(5);
            Assert.That(Account(book).ReservedMinor, Is.EqualTo(100));
            Assert.That(book.Cancel("b", 3, 6).Accepted, Is.True);
            Assert.That(book.Cancel("b", 0, 7).Code, Is.EqualTo(FinancialResultCode.Replay));
            book.Expire(11); Assert.That(Account(book).ReservedMinor, Is.Zero); Assert.That(book.Version, Is.EqualTo(4));
        }
        [Test]
        public void TimeReversalCannotReopenAnExpiredClaim()
        {
            var book = Book(); var intent = Pay(book, expiry: 5); book.Prepare(intent, 0); book.Expire(5);
            Assert.That(book.Prepare(intent, 1).Code, Is.EqualTo(FinancialResultCode.ClockReversed));
            Assert.That(book.ReadTransaction("wage").Phase, Is.EqualTo(FinancialPhase.Expired));
        }
        [Test]
        public void EvidenceCannotBeEmptyChangedOnReplayOrReusedAcrossTransactions()
        {
            var book = Book(); book.Prepare(Pay(book, "a", resource: "a"), 0); book.Prepare(Pay(book, "b", resource: "b"), 0);
            Assert.That(book.Commit("a", 2, 1, " ").Code, Is.EqualTo(FinancialResultCode.MissingEvidence));
            book.Commit("a", 2, 1, "job:1");
            Assert.That(book.Commit("a", 3, 1, "job:2").Code, Is.EqualTo(FinancialResultCode.EvidenceConflict));
            Assert.That(book.Commit("b", 3, 1, "job:1").Code, Is.EqualTo(FinancialResultCode.EvidenceConflict));
            Assert.That(Account(book).BalanceMinor, Is.EqualTo(900)); Assert.That(Account(book).ReservedMinor, Is.EqualTo(100));
        }
        [Test]
        public void FullJournalRejectsNewCommandsButAllowsExistingOnesToFinishAndReplay()
        {
            var book = Book(capacity: 1); var first = Pay(book); book.Prepare(first, 0);
            Assert.That(book.Prepare(Pay(book, "extra", resource: null), 0).Code, Is.EqualTo(FinancialResultCode.CapacityReached));
            Assert.That(book.Commit("wage", 1, 1, "e").Accepted, Is.True);
            Assert.That(book.Prepare(first, 2).Code, Is.EqualTo(FinancialResultCode.Replay));
            Assert.That(book.Prepare(Pay(book, "extra", resource: null), 2).Code, Is.EqualTo(FinancialResultCode.CapacityReached));
        }
        [Test]
        public void CallerCollectionsAndReturnedSnapshotsCannotMutateKernel()
        {
            var input = new List<FinancialAccountSnapshot> { new FinancialAccountSnapshot("public", 1000), new FinancialAccountSnapshot("pawn", 0) };
            var book = new ShadowTransactionKernel(input); input.Clear(); var keys = new[] { "a" };
            var intent = new FinancialIntent("x", 0, 10, new[] { new FinancialTransferLeg("public", "pawn", 100) }, keys);
            keys[0] = "b"; book.Prepare(intent, 0);
            Assert.That(book.IsResourceHeld("a"), Is.True); Assert.That(book.IsResourceHeld("b"), Is.False);
            Assert.Throws<NotSupportedException>(() => ((IList<FinancialAccountSnapshot>)book.ReadAccounts()).Clear());
        }
        [Test]
        public void CrossThreadReadsAndCommandsAreRejected()
        {
            var book = Book(); var intent = Pay(book);
            var error = Task.Run(() => Assert.Throws<InvalidOperationException>(() => book.Prepare(intent, 0))).GetAwaiter().GetResult();
            Assert.That(error.Message, Is.EqualTo("WrongThread")); Assert.That(book.Version, Is.Zero);
            Task.Run(() => Assert.Throws<InvalidOperationException>(() => book.ReadAccounts())).GetAwaiter().GetResult();
        }
        [Test]
        public void IndependentMultiLegPaymentsApplyBothDebitAndCreditSidesAtomically()
        {
            var book = new ShadowTransactionKernel(new[] { new FinancialAccountSnapshot("public", 1000),
                new FinancialAccountSnapshot("pawn", 100), new FinancialAccountSnapshot("shop", 0) });
            var intent = new FinancialIntent("bundle", 0, 10, new[] { new FinancialTransferLeg("public", "pawn", 200),
                new FinancialTransferLeg("pawn", "shop", 50) }, new[] { "worker:1", "food:1" });
            book.Prepare(intent, 0); book.Commit("bundle", 1, 1, "bundle:e");
            Assert.That(book.ReadAccounts().Select(a => a.BalanceMinor), Is.EqualTo(new long[] { 250, 800, 50 }));
            Assert.That(book.ReadAccounts().Sum(a => a.BalanceMinor), Is.EqualTo(1100));
            Assert.That(book.ReadAccounts().Sum(a => a.ReservedMinor), Is.Zero);
        }
        [Test]
        public void DeterministicMixedHistoryPreservesConservationAndReservationBounds()
        {
            var book = new ShadowTransactionKernel(new[] { new FinancialAccountSnapshot("a", 10000, 7),
                new FinancialAccountSnapshot("b", 10000, 11), new FinancialAccountSnapshot("c", 10000) });
            var random = new Random(4821); string[] ids = { "a", "b", "c" };
            for (int step = 0; step < 500; step++)
            {
                int from = random.Next(3), to = (from + 1 + random.Next(2)) % 3;
                string command = "tx:" + step;
                var intent = new FinancialIntent(command, book.Version, step + 5,
                    new[] { new FinancialTransferLeg(ids[from], ids[to], random.Next(1, 1000)) }, new[] { "slot:" + random.Next(5) });
                var prepared = book.Prepare(intent, step);
                if (prepared.Accepted && step % 3 == 0) book.Commit(command, book.Version, step, "proof:" + step);
                else if (prepared.Accepted && step % 3 == 1) book.Cancel(command, book.Version, step);
                book.Expire(step);
                Assert.That(book.ReadAccounts().Sum(a => a.BalanceMinor), Is.EqualTo(30000));
                Assert.That(book.ReadAccounts().All(a => a.ReservedMinor >= 0 && a.ReservedMinor <= a.BalanceMinor), Is.True);
            }
            book.Expire(1000); Assert.That(book.ReadAccounts().Sum(a => a.ReservedMinor), Is.EqualTo(18));
        }
        [Test]
        public void InvalidBundlesAndDuplicateAccountsFailBeforeAnyStateIsCreated()
        {
            Assert.Throws<ArgumentException>(() => new FinancialAccountSnapshot("p", 1, 2));
            Assert.Throws<ArgumentException>(() => new FinancialTransferLeg("p", "p", 1));
            Assert.Throws<ArgumentException>(() => new FinancialTransferLeg("p", "q", 0));
            Assert.Throws<ArgumentException>(() => new FinancialIntent("x", 0, 10,
                new[] { new FinancialTransferLeg("p", "q", 1) }, new[] { "r", "r" }));
            Assert.Throws<ArgumentException>(() => new ShadowTransactionKernel(new[] {
                new FinancialAccountSnapshot("p", 1), new FinancialAccountSnapshot("p", 2) }));
        }
    }
}
