using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class EconomyWageTransferTests
    {
        private static SettlementEntry Deposit(string id, int silver, long expected, SettlementPhase phase)
            => new SettlementEntry(new SettlementIntent(id, expected,
                new ReserveMovementIntent(id, ReserveMovementKind.Deposit, 0, "party", "stack", silver)), phase);

        [Test] public void WageTransferMovesTreasuryMoneyToTheWorkerExactlyOnce()
        {
            var ledger = FundedSettlementLedger.Restore(new[] { Deposit("dep", 100, 0, SettlementPhase.Finalized) });
            Assert.That(ledger.ReadAccount(SettlementLedgerNames.Treasury).BalanceMinor, Is.EqualTo(10000));
            Assert.That(ledger.Prepare(SettlementIntent.ForWage("wage:o1", ledger.Version, "o1", "Human_7", 3000)).Code,
                Is.EqualTo(SettlementCode.Prepared));
            Assert.That(ledger.CompleteTransfer("wage:o1").Code, Is.EqualTo(SettlementCode.Finalized));
            Assert.That(ledger.ReadAccount(SettlementLedgerNames.Treasury).BalanceMinor, Is.EqualTo(7000));
            Assert.That(ledger.ReadAccount("Human_7").BalanceMinor, Is.EqualTo(3000));
            Assert.That(ledger.TotalMinor, Is.EqualTo(10000), "an internal transfer must conserve the total");
            Assert.That(ledger.CompleteTransfer("wage:o1").Code, Is.EqualTo(SettlementCode.Replay));
            Assert.That(ledger.ReadAccount("Human_7").BalanceMinor, Is.EqualTo(3000), "a replay must never pay twice");
            Assert.That(ledger.Prepare(SettlementIntent.ForWage("wage:o1", 1, "o1", "Human_7", 3000)).Code,
                Is.EqualTo(SettlementCode.Replay), "the obligation id is the dedupe key for the whole command");
            Assert.That(ledger.Prepare(SettlementIntent.ForWage("wage:o1", 1, "o1", "Human_7", 4000)).Code,
                Is.EqualTo(SettlementCode.CommandConflict));
        }
        [Test] public void UnfundedWagesAndNonWageCommandsAreRejected()
        {
            var ledger = FundedSettlementLedger.Restore(new[] { Deposit("dep", 10, 0, SettlementPhase.Finalized) });
            Assert.That(ledger.Prepare(SettlementIntent.ForWage("wage:o2", ledger.Version, "o2", "Human_7", 5000)).Code,
                Is.EqualTo(SettlementCode.InsufficientFunds));
            var allocation = new SettlementIntent("alloc", ledger.Version, "claim", "Human_7", 100);
            Assert.That(ledger.Prepare(allocation).Code, Is.EqualTo(SettlementCode.Prepared));
            Assert.That(ledger.CompleteTransfer("alloc").Code, Is.EqualTo(SettlementCode.NotReady),
                "only wage transfers may use the transfer completion path");
            Assert.That(ledger.CompleteAllocation("alloc").Code, Is.EqualTo(SettlementCode.Finalized));
        }
        [Test] public void CodecRoundTripPreservesWageTransfersAndKeepsTotals()
        {
            var ledger = FundedSettlementLedger.Restore(new[]
            {
                Deposit("dep", 100, 0, SettlementPhase.Finalized),
                new SettlementEntry(SettlementIntent.ForWage("wage:o1", 1, "o1", "Human_7", 2500), SettlementPhase.Finalized)
            });
            var loaded = SettlementLedgerCodec.Decode(SettlementLedgerCodec.Encode(ledger, null, null));
            Assert.That(loaded.Fault, Is.Null);
            Assert.That(loaded.Ledger.Read("wage:o1").Intent.Kind, Is.EqualTo(SettlementKind.WageTransfer));
            Assert.That(loaded.Ledger.ReadAccount("Human_7").BalanceMinor, Is.EqualTo(2500));
            Assert.That(loaded.Ledger.ReadAccount(SettlementLedgerNames.Treasury).BalanceMinor, Is.EqualTo(7500));
            Assert.That(loaded.Ledger.TotalMinor, Is.EqualTo(ledger.TotalMinor));
        }
    }
}
