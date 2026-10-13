using System;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class EconomyPurchaseTests
    {
        private static SettlementEntry Deposit(string id, int silver, long expected)
            => new SettlementEntry(new SettlementIntent(id, expected,
                new ReserveMovementIntent(id, ReserveMovementKind.Deposit, 0, "party", "stack", silver)), SettlementPhase.Finalized);
        private static FundedSettlementLedger FundedWithWage()
            => FundedSettlementLedger.Restore(new[]
            {
                Deposit("dep", 100, 0),
                new SettlementEntry(SettlementIntent.ForWage("wage:o1", 1, "o1", "Human_7", 3000), SettlementPhase.Finalized)
            });

        [Test] public void APurchaseIsFundedByTheBuyerAndConservesTheTotal()
        {
            var ledger = FundedWithWage();
            Assert.That(ledger.ReadAccount("Human_7").BalanceMinor, Is.EqualTo(3000));
            var purchase = SettlementIntent.ForPurchase("purchase:1", ledger.Version, "order:1", "Human_7", 1000);
            Assert.That(purchase.Kind, Is.EqualTo(SettlementKind.Purchase));
            Assert.That(purchase.PayerAccount, Is.EqualTo("Human_7"));
            Assert.That(purchase.TargetAccount, Is.EqualTo(SettlementLedgerNames.Treasury));
            Assert.That(ledger.Prepare(purchase).Code, Is.EqualTo(SettlementCode.Prepared));
            Assert.That(ledger.CompletePurchase("purchase:1").Code, Is.EqualTo(SettlementCode.Finalized));
            Assert.That(ledger.ReadAccount("Human_7").BalanceMinor, Is.EqualTo(2000));
            Assert.That(ledger.ReadAccount(SettlementLedgerNames.Treasury).BalanceMinor, Is.EqualTo(8000));
            Assert.That(ledger.TotalMinor, Is.EqualTo(10000), "a purchase moves existing money, it never creates any");
        }
        [Test] public void BuyerFundsNotTreasuryFundsGateThePurchase()
        {
            var ledger = FundedWithWage();
            Assert.That(ledger.Prepare(SettlementIntent.ForPurchase("purchase:2", ledger.Version, "order:2", "Human_7", 5000)).Code,
                Is.EqualTo(SettlementCode.InsufficientFunds));
            Assert.That(ledger.Prepare(SettlementIntent.ForPurchase("purchase:3", ledger.Version, "order:3", "Human_9", 100)).Code,
                Is.EqualTo(SettlementCode.InsufficientFunds), "an account that holds nothing cannot buy");
            Assert.That(ledger.ReadAccount("Human_7").BalanceMinor, Is.EqualTo(3000));
        }
        [Test] public void APurchaseMustTargetTheTreasuryAndOnlyItsOwnCompletionPathWorks()
        {
            Assert.Throws<ArgumentException>(() => new SettlementIntent("bad", 0, "order", "OtherAccount", 100, SettlementKind.Purchase, "Human_7"));
            Assert.Throws<ArgumentException>(() => new SettlementIntent("bad2", 0, "order", SettlementLedgerNames.Treasury, 100, SettlementKind.Purchase, null));
            var ledger = FundedWithWage();
            Assert.That(ledger.Prepare(SettlementIntent.ForPurchase("purchase:4", ledger.Version, "order:4", "Human_7", 500)).Code,
                Is.EqualTo(SettlementCode.Prepared));
            Assert.That(ledger.CompleteTransfer("purchase:4").Code, Is.EqualTo(SettlementCode.NotReady),
                "a purchase must not use the wage completion path");
            Assert.That(ledger.CompletePurchase("purchase:4").Code, Is.EqualTo(SettlementCode.Finalized));
        }
        [Test] public void RepeatingAPurchaseCommandNeverChargesTwice()
        {
            var ledger = FundedWithWage();
            var purchase = SettlementIntent.ForPurchase("purchase:5", ledger.Version, "order:5", "Human_7", 700);
            Assert.That(ledger.Prepare(purchase).Code, Is.EqualTo(SettlementCode.Prepared));
            Assert.That(ledger.Prepare(purchase).Code, Is.EqualTo(SettlementCode.Replay));
            Assert.That(ledger.CompletePurchase("purchase:5").Code, Is.EqualTo(SettlementCode.Finalized));
            Assert.That(ledger.ReadAccount("Human_7").BalanceMinor, Is.EqualTo(2300));
            Assert.That(ledger.Prepare(purchase).Code, Is.EqualTo(SettlementCode.Replay));
            Assert.That(ledger.CompletePurchase("purchase:5").Code, Is.EqualTo(SettlementCode.Replay));
            Assert.That(ledger.ReadAccount("Human_7").BalanceMinor, Is.EqualTo(2300), "a replay must never charge a second time");
        }
        [Test] public void CodecRoundTripPreservesAPurchaseAndItsPayer()
        {
            var ledger = FundedWithWage();
            Assert.That(ledger.Prepare(SettlementIntent.ForPurchase("purchase:6", ledger.Version, "order:6", "Human_7", 400)).Code,
                Is.EqualTo(SettlementCode.Prepared));
            Assert.That(ledger.CompletePurchase("purchase:6").Code, Is.EqualTo(SettlementCode.Finalized));
            var loaded = SettlementLedgerCodec.Decode(SettlementLedgerCodec.Encode(ledger, null, null));
            Assert.That(loaded.Fault, Is.Null);
            var row = loaded.Ledger.Read("purchase:6");
            Assert.That(row.Intent.Kind, Is.EqualTo(SettlementKind.Purchase));
            Assert.That(row.Intent.PayerAccount, Is.EqualTo("Human_7"));
            Assert.That(loaded.Ledger.ReadAccount("Human_7").BalanceMinor, Is.EqualTo(2600));
            Assert.That(loaded.Ledger.ReadAccount(SettlementLedgerNames.Treasury).BalanceMinor, Is.EqualTo(7400));
            Assert.That(loaded.Ledger.TotalMinor, Is.EqualTo(ledger.TotalMinor));
        }
        [Test] public void FoodPriceUsesExactNutritionWithAFloorOfOneMinorUnit()
        {
            // ECON-R09 裁定（2026-10-10）：唯一计价式 = 精确营养 × 每营养价 → 四舍五入到最小货币单位 → 下限 1。
            // 预算、报价展示与实际扣款三处必须同式；这条测试钉住"式"本身。
            Assert.That(PersonalChronicle.Economy.PawnEconomy.Planning.EconomyFoodCostSource.NutritionPrice(0.9d, 100),
                Is.EqualTo(90), "exact nutrition must not be rounded up to a whole unit");
            Assert.That(PersonalChronicle.Economy.PawnEconomy.Planning.EconomyFoodCostSource.NutritionPrice(0.05d, 100),
                Is.EqualTo(5), "a tiny portion is priced by its real nutrition");
            Assert.That(PersonalChronicle.Economy.PawnEconomy.Planning.EconomyFoodCostSource.NutritionPrice(0.004d, 100),
                Is.EqualTo(1), "a gap below half a minor unit must still be tradeable (floor of one)");
            Assert.That(PersonalChronicle.Economy.PawnEconomy.Planning.EconomyFoodCostSource.NutritionPrice(1.5d, 101),
                Is.EqualTo(152), "rounding happens on the total, away from zero");
            Assert.That(PersonalChronicle.Economy.PawnEconomy.Planning.EconomyFoodCostSource.NutritionPrice(0d, 100),
                Is.EqualTo(0), "no nutrition means no price");
        }
        [Test] public void AReplayedChargeExposesItsRealPhaseSoCallersCanAvoidDoubleConsumingACap()
        {
            // ECON-R03 判据基础：上层（照护额度）只能凭 `Entry.Phase` 区分"钱确实扣过一次"（Finalized）
            // 与"只是被 Prepare 过 / 已取消"。若重放不暴露阶段，任何上层都无法避免重复消耗授权额度 ——
            // 这正是修复前"钱包只扣一次、授权额度被重复扣"的成因。
            var ledger = FundedWithWage();
            var intent = SettlementIntent.ForPurchase("care:e1", ledger.Version, "e1", "Human_7", 400);
            Assert.That(ledger.Prepare(intent).Code, Is.EqualTo(SettlementCode.Prepared));
            var inFlight = ledger.Prepare(intent);
            Assert.That(inFlight.Code, Is.EqualTo(SettlementCode.Replay));
            Assert.That(inFlight.Entry.Phase, Is.EqualTo(SettlementPhase.Prepared),
                "an in-flight charge must never read as finalized");
            Assert.That(ledger.ReadAccount("Human_7").BalanceMinor, Is.EqualTo(3000),
                "an in-flight charge must not move money");

            // 正向对照：只有 Finalized 才允许调用方认定"该额度已消耗"，且扣款恰好发生一次。
            var done = FundedWithWage();
            var finalizedIntent = SettlementIntent.ForPurchase("care:e2", done.Version, "e2", "Human_7", 400);
            Assert.That(done.Prepare(finalizedIntent).Code, Is.EqualTo(SettlementCode.Prepared));
            Assert.That(done.CompletePurchase("care:e2").Code, Is.EqualTo(SettlementCode.Finalized));
            var finalized = done.Prepare(finalizedIntent);
            Assert.That(finalized.Code, Is.EqualTo(SettlementCode.Replay));
            Assert.That(finalized.Entry.Phase, Is.EqualTo(SettlementPhase.Finalized),
                "only a finalized charge may let the caller treat the cap as already consumed");
            Assert.That(done.ReadAccount("Human_7").BalanceMinor, Is.EqualTo(2600),
                "the charged amount leaves the payer exactly once even after replays");
        }
    }
}
