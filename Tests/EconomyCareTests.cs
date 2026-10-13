using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Care;
using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Care;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using Verse;

namespace PersonalChronicle.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class EconomyCareTests
    {
        private Harmony shim;
        private string path;
        [OneTimeSetUp] public void Start()
        {
            shim = new Harmony("personalchronicle.tests.care");
            foreach (string name in new[] { "Error", "Warning", "Message" }) Patch(typeof(Log), name, typeof(string));
            Patch(typeof(DeepProfiler), "Start", typeof(string)); Patch(typeof(DeepProfiler), "End");
        }
        private void Patch(Type type, string name, params Type[] args)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, args, null);
            if (method != null) shim.Patch(method, prefix: new HarmonyMethod(typeof(EconomyCareTests).GetMethod(nameof(Skip), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        private static bool Skip() => false;
        [OneTimeTearDown] public void Stop() => shim.UnpatchAll(shim.Id);
        [SetUp] public void Setup() { path = Path.Combine(Path.GetTempPath(), "pc_care_" + Guid.NewGuid().ToString("N") + ".xml"); }
        [TearDown] public void Cleanup() { Scribe.ForceStop(); if (File.Exists(path)) File.Delete(path); }

        private static CreateCareRequest Request(string payer = "Human_parent", bool adult = true, string guardian = null,
            long cap = 1000, int expiry = 1000, long expectedRevision = 0)
            => new CreateCareRequest { AuthorizationId = "care:1", Kind = ServiceKind.Treatment, BeneficiaryKey = "Human_child",
                GuardianKey = guardian, ExecutorKey = "Human_doctor", PayerKey = payer, BeneficiaryIsAdult = adult,
                MaxAmountMinor = cap, CreatedTick = 10, ExpiresTick = expiry, ExpectedRevision = expectedRevision };
        private static ServiceReadiness Readiness(bool fault = false)
            => new ServiceReadiness(true, true, true, true, true, true, true, fault ? "OverCapacity" : null);

        [Test] public void ARelationshipAloneNeverAuthorizesPayment()
        {
            var service = new CareService(new CareSaveData());
            Assert.That(service.Create(Request(payer: null)), Is.EqualTo(CareCode.PayerNotAuthorized));
            Assert.That(service.Create(Request(guardian: "Human_parent")), Is.EqualTo(CareCode.Applied));
            Assert.That(service.Charge("care:1", "Human_spouse", 100, 20), Is.EqualTo(CareCode.PayerNotAuthorized),
                "only the explicitly granted payer may be charged, never a relative");
            Assert.That(service.Charge("care:1", "Human_parent", 100, 20), Is.EqualTo(CareCode.Applied));
            Assert.That(service.ReadAuthorization("care:1").ChargedMinor, Is.EqualTo(100));
        }
        [Test] public void AMinorNeedsANamedGuardianAndAnAdultDoesNot()
        {
            var adult = new CareService(new CareSaveData());
            Assert.That(adult.Create(Request(adult: true)), Is.EqualTo(CareCode.Applied),
                "an adult may act for itself without a named guardian");
            var minorWithoutGuardian = new CareService(new CareSaveData());
            Assert.That(minorWithoutGuardian.Create(Request(adult: false)), Is.EqualTo(CareCode.InvalidCommand));
            var minorWithGuardian = new CareService(new CareSaveData());
            Assert.That(minorWithGuardian.Create(Request(adult: false, guardian: "Human_parent")), Is.EqualTo(CareCode.Applied));
            Assert.That(minorWithGuardian.ReadAuthorization("care:1").GuardianKey, Is.EqualTo("Human_parent"));
        }
        [Test] public void ChargesStayInsideTheGrantedCapAndStopAfterExpiry()
        {
            var service = new CareService(new CareSaveData());
            service.Create(Request(cap: 1000, expiry: 100));
            Assert.That(service.Charge("care:1", "Human_parent", 600, 20), Is.EqualTo(CareCode.Applied));
            Assert.That(service.Charge("care:1", "Human_parent", 500, 20), Is.EqualTo(CareCode.CapExceeded));
            Assert.That(service.Charge("care:1", "Human_parent", 400, 20), Is.EqualTo(CareCode.Applied));
            Assert.That(service.Charge("care:1", "Human_parent", 1, 101), Is.EqualTo(CareCode.Expired));
            Assert.That(service.Charge("care:missing", "Human_parent", 1, 20), Is.EqualTo(CareCode.AuthorizationUnknown));
        }
        [Test] public void ACapabilityOnlyChargesWhenEveryGateIsProven()
        {
            var service = new CareService(new CareSaveData());
            var partial = service.EvaluateCapability(ServiceKind.Treatment,
                new ServiceReadiness(true, true, true, true, true, true, false));
            Assert.That(partial.State.Capability, Is.EqualTo(ServiceCapability.Preparing));
            Assert.That(partial.Persisted, Is.True);
            Assert.That(partial.State.Blockers, Contains.Item("UiExplanation"));
            Assert.That(service.MayCharge(ServiceKind.Treatment), Is.False, "a partial proof never buys a price");
            var ready = service.EvaluateCapability(ServiceKind.Treatment, Readiness());
            Assert.That(ready.State.Capability, Is.EqualTo(ServiceCapability.ReadyPaid));
            Assert.That(service.MayCharge(ServiceKind.Treatment), Is.True);
            var faulted = service.EvaluateCapability(ServiceKind.Treatment, new ServiceReadiness(true, true, true, true, true, true, true, "OverCapacity"));
            Assert.That(faulted.State.Capability, Is.EqualTo(ServiceCapability.Suspended));
            Assert.That(service.MayCharge(ServiceKind.Treatment), Is.False, "a suspended capability must not keep charging");
            Assert.That(service.ReadCapability(ServiceKind.Bed), Is.Null, "untouched services stay absent, i.e. vanilla public");
        }
        [Test] public void TheSameGrantIdReplaysOrConflictsButIsNeverRewritten()
        {
            var service = new CareService(new CareSaveData());
            Assert.That(service.Create(Request(cap: 1000)), Is.EqualTo(CareCode.Applied));
            Assert.That(service.Create(Request(cap: 1000)), Is.EqualTo(CareCode.Replay));
            Assert.That(service.Create(Request(cap: 9000)), Is.EqualTo(CareCode.CommandConflict));
            Assert.That(service.ReadAuthorization("care:1").MaxAmountMinor, Is.EqualTo(1000), "the granted cap is never rewritten");
            Assert.That(service.Revoke("care:1"), Is.EqualTo(CareCode.Applied));
            Assert.That(service.ReadAuthorization("care:1"), Is.Null);
        }
        [Test] public void DeliveryEvidenceNeedsAnAuthorizationAndIsIdempotent()
        {
            var service = new CareService(new CareSaveData());
            Assert.That(service.AcceptCareAct(ServiceKind.Examination, "Human_doctor", "Human_child", 50), Is.EqualTo(CareCode.AuthorizationUnknown),
                "an unauthorized beneficiary is not recorded at all");
            var grant = Request(guardian: "Human_parent");
            grant.Kind = ServiceKind.Examination;
            Assert.That(service.Create(grant), Is.EqualTo(CareCode.Applied));
            Assert.That(service.AcceptCareAct(ServiceKind.Examination, "Human_doctor", "Human_child", 50), Is.EqualTo(CareCode.Applied));
            Assert.That(service.AcceptCareAct(ServiceKind.Examination, "Human_doctor", "Human_child", 50), Is.EqualTo(CareCode.Replay),
                "the same delivered act is never counted twice");
            Assert.That(service.AcceptCareAct(ServiceKind.Examination, "Human_doctor", "Human_child", 51), Is.EqualTo(CareCode.Applied),
                "a later delivered act is its own evidence");
        }
        [Test] public void APaidActMovesThePayersOwnMoneyOnceAndOnlyOnce()
        {
            var ledger = PersonalChronicle.Application.PawnEconomy.Finance.FundedSettlementLedger.Restore(new[]
            {
                new SettlementEntry(new SettlementIntent("dep", 0, new ReserveMovementIntent("dep", ReserveMovementKind.Deposit, 0, "party", "stack", 100)), SettlementPhase.Finalized),
                new SettlementEntry(SettlementIntent.ForWage("wage:o1", 1, "o1", "Human_parent", 3000), SettlementPhase.Finalized)
            });
            var service = new CareService(new CareSaveData());
            var grant = Request(guardian: "Human_parent", cap: 1000);
            grant.Kind = ServiceKind.Examination;
            Assert.That(service.Create(grant), Is.EqualTo(CareCode.Applied));
            const string evidence = "care-evidence:Human_child|Human_doctor|50";
            var intent = SettlementIntent.ForPurchase("care:" + evidence, ledger.Version, evidence, "Human_parent", 400);
            Assert.That(ledger.Prepare(intent).Code, Is.EqualTo(SettlementCode.Prepared));
            Assert.That(ledger.CompletePurchase(intent.CommandId).Code, Is.EqualTo(SettlementCode.Finalized));
            Assert.That(service.Charge("care:1", "Human_parent", 400, 60), Is.EqualTo(CareCode.Applied));
            Assert.That(ledger.ReadAccount("Human_parent").BalanceMinor, Is.EqualTo(2600));
            Assert.That(ledger.ReadAccount(SettlementLedgerNames.Treasury).BalanceMinor, Is.EqualTo(7400));
            Assert.That(ledger.TotalMinor, Is.EqualTo(10000), "a care act moves existing money, it creates none");
            Assert.That(ledger.Prepare(intent).Code, Is.EqualTo(SettlementCode.Replay), "the evidence-keyed command never charges twice");
            Assert.That(ledger.CompletePurchase(intent.CommandId).Code, Is.EqualTo(SettlementCode.Replay));
            Assert.That(ledger.ReadAccount("Human_parent").BalanceMinor, Is.EqualTo(2600));
        }        [Test] public void EvidenceIsScopedToItsOwnServiceKind()
        {
            var service = new CareService(new CareSaveData());
            var treatment = Request(guardian: "Human_parent");
            treatment.Kind = ServiceKind.Treatment;
            Assert.That(service.Create(treatment), Is.EqualTo(CareCode.Applied));
            Assert.That(service.AcceptCareAct(ServiceKind.Examination, "Human_doctor", "Human_child", 70),
                Is.EqualTo(CareCode.AuthorizationUnknown),
                "a treatment grant never authorizes examination evidence");
            Assert.That(service.AcceptCareAct(ServiceKind.Treatment, "Human_doctor", "Human_child", 70), Is.EqualTo(CareCode.Applied));
        }        [Test] public void ThePriceGateKeepsTheCapabilityUnproven()
        {
            var service = new CareService(new CareSaveData());
            var state = service.EvaluateCapability(ServiceKind.Examination,
                new ServiceReadiness(vanillaAccess: true, deliveryEvidence: true, capacityAndHours: true,
                    priceCap: false, settlement: true, interruptionRecovery: true, uiExplanation: true));
            Assert.That(state.State.Capability, Is.EqualTo(ServiceCapability.Preparing));
            Assert.That(state.State.Blockers, Contains.Item("PriceCap"));
            Assert.That(service.MayCharge(ServiceKind.Examination), Is.False,
                "care delivery evidence alone must never buy a price");
        }        [Test] public void CareStateSurvivesASaveRoundTripAndAMissingNodeGrantsNothing()
        {
            var store = new CareSaveData();
            var service = new CareService(store);
            service.Create(Request(guardian: "Human_parent"));
            service.Charge("care:1", "Human_parent", 250, 20);
            service.EvaluateCapability(ServiceKind.Treatment, Readiness());
            Scribe.saver.InitSaving(path, "savegame"); Scribe_Deep.Look(ref store, "pawnEconomicCare"); Scribe.saver.FinalizeSaving();
            CareSaveData loaded = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "pawnEconomicCare"); Scribe.loader.FinalizeLoading();
            var reloaded = new CareService(loaded);
            Assert.That(reloaded.ReadAuthorization("care:1").ChargedMinor, Is.EqualTo(250));
            Assert.That(reloaded.MayCharge(ServiceKind.Treatment), Is.True);
            File.WriteAllText(path, "<?xml version=\"1.0\"?><savegame />");
            CareSaveData missing = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref missing, "pawnEconomicCare"); Scribe.loader.FinalizeLoading();
            missing = missing ?? new CareSaveData();
            var empty = new CareService(missing);
            Assert.That(empty.Authorizations, Is.EqualTo(0), "a save without the node grants no care authorization at all");
            Assert.That(empty.Charge("care:1", "Human_parent", 1, 20), Is.EqualTo(CareCode.AuthorizationUnknown));
            Assert.That(empty.MayCharge(ServiceKind.Treatment), Is.False, "no capability state means the vanilla public path");
        }
    }
}
