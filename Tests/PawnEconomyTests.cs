using System;
using System.Linq;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Data;
using PersonalChronicle.Domain.PawnEconomy;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class PawnEconomyTests
    {
        private EconomyState state;
        private EconomyService service;
        private EconomyInstitution shop;
        [SetUp] public void Setup()
        {
            state = new EconomyState(); service = new EconomyService(state);
            shop = state.Add(new EconomyInstitution { AnchorKey = "shop", InstitutionKind = InstitutionKind.Shop, MapId = 3 });
        }
        private void Enabled() { Assert.That(service.Enable(true)); }
        private EconomyOrder Buy(string pawn = "p1", string asset = "a1") => service.Prepare(EconomyOrderKind.Purchase,
            pawn, shop, pawn, 6000, 100, asset);
        private void Fund(string pawn, long amount = 10000)
        { Assert.That(EconomyRules.Transfer(service.Account("public"), service.Account(pawn), amount)); }
        private long Total() => state.All<EconomyAccount>().Sum(a => a.Balance);

        [Test] public void OldSaveStartsDisabledWithoutMoney()
        { var old = PawnEconomySaveData.Decode(null); Assert.That(old.Enabled, Is.False); Assert.That(old.Records, Is.Empty); }
        [Test] public void OpeningAllocationOccursOnceAcrossDisableAndReload()
        {
            Enabled(); service.Enable(false); service.Enable(true);
            var loaded = PawnEconomySaveData.Decode(PawnEconomySaveData.Encode(state)); var next = new EconomyService(loaded);
            next.RecoverAfterLoad(); next.Enable(false); next.Enable(true);
            Assert.That(next.Account("public").Balance, Is.EqualTo(EconomyRules.OpeningBudget));
            Assert.That(loaded.All<EconomyReceipt>().Count(r => r.Key == "opening"), Is.EqualTo(1));
        }
        [Test] public void PurchaseReservesThenConservesMoneyAndCommitsOnce()
        {
            Enabled(); Fund("p1"); var order = Buy();
            Assert.That(service.Account("p1").Reserved, Is.EqualTo(6000));
            Assert.That(service.Account("p1").Available, Is.EqualTo(4000));
            Assert.That(service.Commit(order, 200)); Assert.That(service.Commit(order, 201));
            Assert.That(service.Account("p1").Balance, Is.EqualTo(4000));
            Assert.That(service.Account("p1").Reserved, Is.Zero); Assert.That(Total(), Is.EqualTo(EconomyRules.OpeningBudget));
            Assert.That(state.All<EconomyReceipt>().Count(r => r.Key == "order:" + order.Id), Is.EqualTo(1));
        }
        [Test] public void CancellationReleasesFundsOnceWithoutMinting()
        {
            Enabled(); Fund("p1"); var order = Buy(); Assert.That(service.Cancel(order, "Interrupted"));
            Assert.That(service.Cancel(order, "Interrupted"), Is.False); Assert.That(service.Commit(order, 200), Is.False);
            Assert.That(service.Account("p1").Balance, Is.EqualTo(10000)); Assert.That(service.Account("p1").Reserved, Is.Zero);
            Assert.That(Total(), Is.EqualTo(EconomyRules.OpeningBudget));
        }
        [Test] public void DisableCancelsPendingAndReleasesFunds()
        { Enabled(); Fund("p1"); var order = Buy(); service.Enable(false); Assert.That(order.Status, Is.EqualTo(EconomyOrderStatus.Cancelled)); Assert.That(service.Account("p1").Reserved, Is.Zero); }
        [Test] public void ActorAndAssetReservationsRejectDoubleBooking()
        {
            Enabled(); Fund("p1"); Fund("p2"); Assert.That(Buy(), Is.Not.Null);
            Assert.That(Buy("p1", "a2"), Is.Null); Assert.That(Buy("p2", "a1"), Is.Null);
            Assert.That(Buy("p2", "a2"), Is.Not.Null);
        }
        [Test] public void InsufficientFundsDoesNotCreateOrder()
        { Enabled(); Assert.That(Buy(), Is.Null); Assert.That(state.All<EconomyOrder>(), Is.Empty); }
        [TestCase(-1L)] [TestCase(1000000000001L)]
        public void IllegalPriceCannotReserve(long amount)
        { Enabled(); Assert.That(service.Prepare(EconomyOrderKind.Purchase, "p", shop, "p", amount, 0), Is.Null); }
        [Test] public void ExpiredOrderCannotCommit()
        { Enabled(); Fund("p1"); var order = Buy(); Assert.That(service.Commit(order, order.ExpiresTick), Is.False); Assert.That(order.Pending); service.Cancel(order, "Expired"); Assert.That(Total(), Is.EqualTo(EconomyRules.OpeningBudget)); }
        [Test] public void ReloadCancelsPreparedOrdersButKeepsCompletedReceipt()
        {
            Enabled(); Fund("p1"); var order = Buy(); service.Commit(order, 200); Fund("p2"); Buy("p2", "a2");
            var loaded = PawnEconomySaveData.Decode(PawnEconomySaveData.Encode(state)); var next = new EconomyService(loaded); next.RecoverAfterLoad();
            Assert.That(loaded.Ready); Assert.That(loaded.All<EconomyOrder>().Count(o => o.Status == EconomyOrderStatus.Committed), Is.EqualTo(1));
            Assert.That(loaded.All<EconomyOrder>().Count(o => o.Pending), Is.Zero); Assert.That(loaded.All<EconomyAccount>().Sum(a => a.Reserved), Is.Zero);
            Assert.That(loaded.All<EconomyAccount>().Sum(a => a.Balance), Is.EqualTo(EconomyRules.OpeningBudget));
        }
        [Test] public void ProductionPaysOnlyOnceAndWatermarkSurvivesHistoryTrimAndSave()
        {
            Enabled(); var position = state.Add(new EconomyPosition { Wage = 2000, Capacity = 1 });
            var contract = state.Add(new EconomyContract { PositionId = position.Id, PawnKey = "p1" });
            Assert.That(service.CompleteProduction(contract, 30, 100), Is.Not.Null);
            Assert.That(service.CompleteProduction(contract, 30, 101), Is.Null); Assert.That(service.CompleteProduction(contract, 29, 102), Is.Null);
            state.Records.RemoveAll(r => r is EconomyReceipt receipt && receipt.Type == "Wage");
            var loaded = PawnEconomySaveData.Decode(PawnEconomySaveData.Encode(state)); var next = new EconomyService(loaded); next.RecoverAfterLoad();
            Assert.That(next.CompleteProduction(loaded.Find<EconomyContract>(contract.Id), 30, 103), Is.Null);
            Assert.That(next.Account("p1").Balance, Is.EqualTo(2000));
        }
        [Test] public void InsufficientPayrollProducesOnePayableThenStopsFurtherClaims()
        {
            Enabled(); Fund("other", EconomyRules.OpeningBudget);
            var position = state.Add(new EconomyPosition { Wage = 2000 }); var contract = state.Add(new EconomyContract { PositionId = position.Id, PawnKey = "worker" });
            var receipt = service.CompleteProduction(contract, 10, 100); Assert.That(receipt.Paid, Is.False); Assert.That(position.Open, Is.False);
            Assert.That(service.CompleteProduction(contract, 11, 101), Is.Null);
            Assert.That(EconomyRules.Transfer(service.Account("other"), service.Account("public"), 2000));
            Assert.That(service.SettlePayables(), Is.EqualTo(1)); Assert.That(service.SettlePayables(), Is.Zero);
            Assert.That(service.Account("worker").Balance, Is.EqualTo(2000)); Assert.That(Total(), Is.EqualTo(EconomyRules.OpeningBudget));
        }
        [Test] public void EmploymentReservationHonorsCapacity()
        {
            Enabled(); var p = state.Add(new EconomyPosition { Wage = 2000, Capacity = 1 });
            Assert.That(service.HasVacancy(p));
            var order = service.Prepare(EconomyOrderKind.Employment, "p1", shop, "p1", 0, 100, position: p.Id);
            Assert.That(service.HasVacancy(p), Is.False); Assert.That(service.Sign(order, 200), Is.Not.Null);
            Assert.That(service.Sign(order, 201), Is.Null); Assert.That(state.All<EconomyContract>().Count(), Is.EqualTo(1));
        }
        [Test] public void ExamFeeAndDoctorWageAreConservedAndIdempotent()
        {
            Enabled(); var clinic = state.Add(new EconomyInstitution { AnchorKey = "clinic", InstitutionKind = InstitutionKind.ExamCenter });
            var order = service.Prepare(EconomyOrderKind.Examination, "patient", clinic, "public", 6000, 100, provider: "doctor");
            Assert.That(service.PayDoctor(order, 150), Is.False); Assert.That(service.Commit(order, 200));
            Assert.That(service.PayDoctor(order, 200)); Assert.That(service.PayDoctor(order, 201));
            Assert.That(service.Account("doctor").Balance, Is.EqualTo(3600)); Assert.That(service.Account(clinic.AccountKey).Balance, Is.EqualTo(2400));
            Assert.That(Total(), Is.EqualTo(EconomyRules.OpeningBudget));
        }
        [Test] public void DoctorCannotBeOwnClientOrDoubleBooked()
        {
            Enabled(); var clinic = state.Add(new EconomyInstitution { AnchorKey = "clinic", InstitutionKind = InstitutionKind.ExamCenter });
            Assert.That(service.Prepare(EconomyOrderKind.Examination, "doctor", clinic, "public", 6000, 0, provider: "doctor"), Is.Null);
            Assert.That(service.Prepare(EconomyOrderKind.Examination, "patient", clinic, "public", 6000, 0, provider: "doctor"), Is.Not.Null);
            Assert.That(Buy("doctor"), Is.Null);
        }
        [Test] public void RoundTripPreservesEveryRecordTypeAndOwnership()
        {
            Enabled(); service.Person("p").AcquiredAsset = "realApparel42";
            var pos = state.Add(new EconomyPosition { InstitutionId = shop.Id, MapId = 3, ZoneId = 7, Wage = 2000, RequiresExam = true });
            state.Add(new EconomyContract { PositionId = pos.Id, PawnKey = "p", LastProductionJobId = 50 });
            state.Add(new EconomyReport { PawnKey = "p", DoctorKey = "d", Moving = .75f, Manipulation = 1.2f, Consciousness = .9f,
                Tick = 900, ExpiresTick = 60900, VisibleConditions = new System.Collections.Generic.List<string> { "Scar" } });
            state.Add(new EconomyOwnership { AssetKey = "realApparel42", OwnerKey = "p" });
            Fund("p"); var order = Buy("p", "otherApparel"); service.Commit(order, 200);
            var loaded = PawnEconomySaveData.Decode(PawnEconomySaveData.Encode(state));
            Assert.That(loaded.Ready); Assert.That(loaded.Records.Count, Is.EqualTo(state.Records.Count));
            Assert.That(loaded.All<EconomyReport>().Single().Manipulation, Is.EqualTo(1.2f));
            Assert.That(loaded.All<EconomyReport>().Single().VisibleConditions, Is.EqualTo(new[] { "Scar" }));
            Assert.That(loaded.All<EconomyOwnership>().Single().AssetKey, Is.EqualTo("realApparel42"));
            Assert.That(loaded.All<EconomyContract>().Single().LastProductionJobId, Is.EqualTo(50));
        }
        [Test] public void HigherSchemaIsReadOnlyAndCannotIssueMoney()
        {
            state.Schema = 999; var loaded = PawnEconomySaveData.Decode(PawnEconomySaveData.Encode(state));
            var next = new EconomyService(loaded); next.RecoverAfterLoad(); Assert.That(loaded.Fault, Is.EqualTo("UnsupportedSchema"));
            Assert.That(next.Enable(true), Is.False); Assert.That(loaded.All<EconomyAccount>(), Is.Empty);
        }
        [Test] public void CorruptPayloadIsFaulted()
        { var loaded = PawnEconomySaveData.Decode("<broken>"); new EconomyService(loaded).RecoverAfterLoad(); Assert.That(loaded.Fault, Is.EqualTo("InvalidPayload")); }
        [Test] public void NegativeSavedOrderCannotReleaseNegativeMoney()
        {
            Enabled(); Fund("p1"); var order = Buy(); order.Amount = -6000;
            service.RecoverAfterLoad(); Assert.That(state.Fault, Is.EqualTo("InvalidLedger"));
            Assert.That(service.Account("p1").Reserved, Is.EqualTo(6000));
        }
        [Test] public void MismatchedReservationsFaultBeforeRefund()
        {
            Enabled(); Fund("p1"); Buy(); service.Account("p1").Reserved--;
            service.RecoverAfterLoad(); Assert.That(state.Fault, Is.EqualTo("ReservationMismatch"));
            Assert.That(service.Account("p1").Reserved, Is.EqualTo(5999));
        }
        [Test] public void JournalBoundsDoNotDeleteUnpaidEntitlements()
        {
            Enabled(); for (int i = 0; i < 700; i++) service.Receipt("test:" + i, "Wage", "p", "public", "p", 2000, i, i != 0);
            service.TrimHistory(); Assert.That(state.All<EconomyReceipt>().Count(), Is.EqualTo(514));
            Assert.That(state.All<EconomyReceipt>().Single(r => !r.Paid).Key, Is.EqualTo("test:0"));
        }
        [TestCase(23, 18, 6, true)] [TestCase(6, 18, 6, false)] [TestCase(12, 0, 0, true)] [TestCase(18, 6, 18, false)]
        public void HoursIncludeStartExcludeEndAndWrapMidnight(int hour, int start, int end, bool expected)
        { Assert.That(EconomyRules.InHours(hour, start, end), Is.EqualTo(expected)); }
        [Test] public void TransfersRejectOverflowAndPreserveOriginalBalances()
        {
            var from = new EconomyAccount { Balance = 100 }; var to = new EconomyAccount { Balance = EconomyRules.MaxAmount };
            Assert.That(EconomyRules.Transfer(from, to, 1), Is.False); Assert.That(from.Balance, Is.EqualTo(100));
            to.Balance = 0; from.Expense = long.MaxValue; Assert.That(EconomyRules.Transfer(from, to, 1), Is.False);
        }
    }
}
