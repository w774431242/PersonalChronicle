using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Linq;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Data;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Governance;
using PersonalChronicle.Economy.PawnEconomy;
using Verse;

namespace PersonalChronicle.Tests
{
    public sealed partial class PawnSilverReserveTests
    {
        private TreasuryOperationsSaveData RoundTripOperations(TreasuryOperationsSaveData data, Action<XElement> edit = null)
        {
            string path = PathForTest(); Scribe.saver.InitSaving(path, "savegame"); Scribe_Deep.Look(ref data, "operations"); Scribe.saver.FinalizeSaving();
            if (edit != null)
            {
                var xml = XDocument.Load(path); edit(xml.Root.Element("operations"));
                using (var writer = System.Xml.XmlWriter.Create(path, new System.Xml.XmlWriterSettings { NewLineHandling = System.Xml.NewLineHandling.None })) xml.Save(writer);
            }
            TreasuryOperationsSaveData loaded = null; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "operations"); Scribe.loader.FinalizeLoading();
            return loaded;
        }
        private static TreasuryDelivery Delivery(int id = 1, ReserveMovementKind kind = ReserveMovementKind.Deposit, string command = "courier")
            => new TreasuryDelivery(id, command, kind, TreasuryDeliveryPhase.Waiting, "pawn", "bank", 0, "silver", null, 25, 5000, 0, 0);
        private static LegacyCutoverPlan Cutover(LegacySettlementArchive first, LegacySettlementArchive last, IEnumerable<SettlementEntry> entries = null,
            TreasuryOperationsSaveData operations = null) => LegacyCutoverPlan.Build(first.Hash, first.Report, first.Claims, last.Hash, last.Report, last.Claims,
                entries ?? Array.Empty<SettlementEntry>(), operations?.SnapshotHash, operations?.ReadDecisions());
        private static LegacySettlementArchive Archive(EconomyState state) => new LegacySettlementArchive(PawnEconomySaveData.Encode(state), "test");
        [Test] public void B242_PreparedWithdrawalLocksFundsBeforeAnyMovement()
        {
            var host = HostFunded(); var party = new Holder(); var stack = Contents(Reserve(host)).Single();
            var intent = HostIntent(host, party, stack, 25, "withdraw", ReserveMovementKind.Withdrawal);
            Assert.That(host.PreparePhysical(intent).Code, Is.EqualTo(SettlementCode.Prepared));
            Assert.That(host.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(10000));
            Assert.That(host.ReadAccount("Treasury").ReservedMinor, Is.EqualTo(2500)); Assert.That(party.Items, Is.Empty);
            Assert.That(host.DeliverPreparedPhysical(intent, party.Items).Code, Is.EqualTo(SettlementCode.Finalized));
            Assert.That(host.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(7500)); Assert.That(host.ReadAccount("Treasury").ReservedMinor, Is.Zero);
            Assert.That(party.Items.Single().stackCount, Is.EqualTo(25));
            Assert.That(host.DeliverPreparedPhysical(intent, party.Items).Code, Is.EqualTo(SettlementCode.Replay)); Assert.That(party.Items.Single().stackCount, Is.EqualTo(25));
            Assert.That(host.ReadAudit().DeficitMinor, Is.Zero);
        }
        [Test] public void B242_PreparedWithdrawalCannotChangeBodyOrSpendHeldFunds()
        {
            var host = HostFunded(); var party = new Holder(); var stack = Contents(Reserve(host)).Single();
            var intent = HostIntent(host, party, stack, 80, "withdraw", ReserveMovementKind.Withdrawal); host.PreparePhysical(intent);
            var changed = new SettlementIntent(intent.CommandId, intent.ExpectedVersion, new ReserveMovementIntent(intent.CommandId,
                ReserveMovementKind.Withdrawal, intent.Movement.ExpectedVersion, party.Id, stack.GetUniqueLoadID(), 70));
            Assert.That(host.DeliverPreparedPhysical(changed, party.Items).Code, Is.EqualTo(SettlementCode.CommandConflict)); Assert.That(party.Items, Is.Empty);
            Assert.That(host.PreparePhysical(HostIntent(host, party, stack, 25, "second", ReserveMovementKind.Withdrawal)).Code, Is.EqualTo(SettlementCode.NotReady));
            Assert.That(host.ReadAccount("Treasury").AvailableMinor, Is.EqualTo(2000));
        }
        [Test] public void B242_CancelBeforeMovementReleasesHoldAndNeverResurrectsCommand()
        {
            var host = HostFunded(); var party = new Holder(); var stack = Contents(Reserve(host)).Single();
            var intent = HostIntent(host, party, stack, 25, "withdraw", ReserveMovementKind.Withdrawal); host.PreparePhysical(intent);
            Assert.That(host.CancelUnmovedPhysical(intent.CommandId).Code, Is.EqualTo(SettlementCode.Cancelled));
            Assert.That(host.ReadAccount("Treasury").ReservedMinor, Is.Zero); Assert.That(host.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(10000));
            Assert.That(host.DeliverPreparedPhysical(intent, party.Items).Entry.Phase, Is.EqualTo(SettlementPhase.Cancelled)); Assert.That(party.Items, Is.Empty);
        }
        [Test] public void B242_LoadBeforeMovementCancelsPreparedWithdrawalWithoutLosingReserve()
        {
            var host = HostFunded(); var party = new Holder(); var stack = Contents(Reserve(host)).Single();
            var intent = HostIntent(host, party, stack, 25, "withdraw", ReserveMovementKind.Withdrawal); host.PreparePhysical(intent);
            var loaded = RoundTripHost(host); Assert.That(loaded.ReadAudit().Fault, Is.Null);
            Assert.That(loaded.ReadCommand("withdraw").Phase, Is.EqualTo(SettlementPhase.Cancelled));
            Assert.That(loaded.ReadAccount("Treasury").ReservedMinor, Is.Zero); Assert.That(loaded.ReadAudit().ActualReserveMinor, Is.EqualTo(10000));
            Assert.That(loaded.DeliverPreparedPhysical(intent, party.Items).Entry.Phase, Is.EqualTo(SettlementPhase.Cancelled));
        }
        [Test] public void B242_PhysicalProofPreventsCancellationOrDoubleWithdrawalAfterLoad()
        {
            var host = HostFunded(); var party = new Holder(); var stack = Contents(Reserve(host)).Single();
            var intent = HostIntent(host, party, stack, 25, "withdraw", ReserveMovementKind.Withdrawal); host.PreparePhysical(intent);
            host.DeliverPreparedPhysical(intent, party.Items); Assert.That(host.CancelUnmovedPhysical("withdraw").Code, Is.EqualTo(SettlementCode.NotReady));
            var loaded = RoundTripHost(host); Assert.That(loaded.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(7500));
            Assert.That(loaded.DeliverPreparedPhysical(intent, party.Items).Entry.Phase, Is.EqualTo(SettlementPhase.Finalized));
            Assert.That(party.Items.Single().stackCount, Is.EqualTo(25)); Assert.That(loaded.ReadAudit().Fault, Is.Null);
        }
        [Test] public void B242_PawnCarryTrackerUsesCanonicalPawnIdentityForActualTransfer()
        {
            var pawn = (Pawn)FormatterServices.GetUninitializedObject(typeof(Pawn)); pawn.def = silverDef; pawn.thingIDNumber = 876;
            var carry = new Pawn_CarryTracker(pawn); pawn.carryTracker = carry;
            var silver = ThingMaker.MakeThing(silverDef); silver.stackCount = 25; Assert.That(carry.innerContainer.TryAdd(silver, false), Is.True);
            var host = new PawnSettlementSaveHost(); string id = "actual-carry";
            var intent = new SettlementIntent(id, host.Version, new ReserveMovementIntent(id, ReserveMovementKind.Deposit,
                host.ReserveVersion, pawn.GetUniqueLoadID(), silver.GetUniqueLoadID(), 25));
            Assert.That(host.ExecutePhysical(intent, carry.innerContainer).Code, Is.EqualTo(SettlementCode.Finalized));
            Assert.That(carry.innerContainer, Is.Empty); Assert.That(host.ReadAudit().ActualReserveMinor, Is.EqualTo(2500));
            Assert.That(host.ReadPhysicalEvidence(id).HasExactQuantityProof, Is.True);
        }
        [Test] public void B242_WrongCarrierFailsWithoutIssuingFunds()
        {
            var host = new PawnSettlementSaveHost(); var holder = new Holder { Id = "wrong" }; var silver = Silver(holder, 25);
            var intent = new SettlementIntent("wrong-carrier", 0, new ReserveMovementIntent("wrong-carrier", ReserveMovementKind.Deposit, 0, "pawn", silver.GetUniqueLoadID(), 25));
            Assert.That(host.ExecutePhysical(intent, holder.Items).Entry.Phase, Is.EqualTo(SettlementPhase.Cancelled));
            Assert.That(host.ReadAccount("Treasury").BalanceMinor, Is.Zero); Assert.That(holder.Items.Single().stackCount, Is.EqualTo(25));
        }
        [Test] public void B242_DeliveryRequiresPreviouslyPreparedWithdrawal()
        {
            var host = HostFunded(); var party = new Holder(); var stack = Contents(Reserve(host)).Single();
            Assert.That(host.DeliverPreparedPhysical(HostIntent(host, party, stack, 25, "not-prepared", ReserveMovementKind.Withdrawal), party.Items).Code, Is.EqualTo(SettlementCode.NotReady));
            Assert.That(host.Version, Is.EqualTo(1)); Assert.That(party.Items, Is.Empty);
        }
        [Test] public void B242_OperationsMissingFieldsDefaultEmptyAndDoNotOpenAccounts()
        {
            var loaded = RoundTripOperations(new TreasuryOperationsSaveData(), node => { node.Element("payload").Remove(); node.Element("encoding").Remove(); });
            Assert.That(loaded.ReadOrders(), Is.Empty); Assert.That(loaded.ReadDecisions(), Is.Empty); Assert.That(loaded.Revision, Is.Zero); Assert.That(loaded.Fault, Is.Null);
        }
        [Test] public void B242_OperationsRoundTripCarriesIdentityTerminalStateAndDecisions()
        {
            var data = new TreasuryOperationsSaveData(); Assert.That(data.Add(Delivery()), Is.True);
            data.Update(Delivery().With(TreasuryDeliveryPhase.Carrying, "split-silver")); data.Update(data.Find(1).With(TreasuryDeliveryPhase.Completed));
            data.Acknowledge("hash", "current:EconomyOwnership:9", LegacyDisposition.RetainOwnership);
            var loaded = RoundTripOperations(data); var order = loaded.Find(1);
            Assert.That(order.Phase, Is.EqualTo(TreasuryDeliveryPhase.Completed)); Assert.That(order.CarryKey, Is.EqualTo("split-silver")); Assert.That(order.SourceKey, Is.EqualTo("silver"));
            Assert.That(loaded.ReadDecisions()["current:EconomyOwnership:9"], Is.EqualTo(LegacyDisposition.RetainOwnership));
            Assert.That(loaded.SnapshotHash, Is.EqualTo("hash")); Assert.That(loaded.Revision, Is.EqualTo(data.Revision));
        }
        [Test] public void B242_OperationsEnforcesOnePendingAndCannotRewriteTerminalIdentity()
        {
            var data = new TreasuryOperationsSaveData(); data.Add(Delivery()); Assert.That(data.Add(Delivery(2, command: "second")), Is.False);
            data.Update(Delivery().With(TreasuryDeliveryPhase.Cancelled)); data.Update(Delivery().With(TreasuryDeliveryPhase.Completed));
            Assert.That(data.Find(1).Phase, Is.EqualTo(TreasuryDeliveryPhase.Cancelled)); Assert.That(data.Add(Delivery(2, command: "second")), Is.True);
            Assert.That(data.Add(Delivery(3, command: "courier")), Is.False);
        }
        [Test] public void B242_CarryIdentityAndPhaseCannotBeReversed()
        {
            var data = new TreasuryOperationsSaveData(); data.Add(Delivery()); data.Update(Delivery().With(TreasuryDeliveryPhase.Carrying, "carried"));
            Assert.Throws<InvalidOperationException>(() => data.Update(data.Find(1).With(TreasuryDeliveryPhase.Waiting)));
            Assert.Throws<InvalidOperationException>(() => data.Update(data.Find(1).With(TreasuryDeliveryPhase.Completed, "another")));
        }
        [TestCase("<TreasuryOperations schema=\"99\" revision=\"0\" />")]
        [TestCase("<TreasuryOperations schema=\"1\" revision=\"0\" extra=\"保留\" />")]
        [TestCase("<TreasuryOperations schema=\"1\" revision=\"0\"><Unknown /></TreasuryOperations>")]
        [TestCase("<TreasuryOperations schema=\"1\" revision=\"9223372036854775807\" />")]
        [TestCase("<broken>")]
        [TestCase("<!DOCTYPE x [<!ENTITY e 'x'>]><TreasuryOperations schema=\"1\" revision=\"0\">&e;</TreasuryOperations>")]
        public void B242_UnknownOperationsPreserveExactWireAndFreezeCommands(string raw)
        {
            var loaded = RoundTripOperations(new TreasuryOperationsSaveData(), node => WriteWireText(node, "payload", raw));
            string wire = Field<string>(loaded, "wire"); Assert.That(loaded.Fault, Is.Not.Null); Assert.That(loaded.Add(Delivery()), Is.False);
            Assert.That(loaded.Acknowledge("hash", "key", LegacyDisposition.PreserveHistory), Is.False);
            Assert.That(Field<string>(RoundTripOperations(loaded), "wire"), Is.EqualTo(wire));
        }
        [TestCase("future", "保留\nwire")][TestCase("base64-utf8-v1", "%%%")]
        public void B242_UnknownOperationsEncodingRetainsRaw(string encoding, string wire)
        {
            var data = RoundTripOperations(new TreasuryOperationsSaveData(), node => { node.Element("encoding").Value = encoding; node.Element("payload").Value = wire; });
            Assert.That(data.Fault, Is.Not.Null); var again = RoundTripOperations(data); Assert.That(Field<string>(again, "wire"), Is.EqualTo(wire));
        }
        [Test] public void B242_FinalClaimsDeductPreviouslyFundedOriginalClaimAndRejectDoubleFunding()
        {
            var state = LegacyRights(); var original = Archive(state); var host = HostFunded(200); host.CaptureLegacy(original.Raw, "initial");
            var balance = original.Claims.Single(x => x.Kind == "Balance");
            Assert.That(host.AllocateLegacy(new SettlementIntent("old-part", host.Version, balance.Id, balance.LegacyParty, 4000)).Code, Is.EqualTo(SettlementCode.Finalized));
            state.All<EconomyAccount>().Single().Income++; state.Revision++; var latest = Archive(state); host.CaptureCutover(latest.Raw);
            var plan = Cutover(original, latest, host.ReadEntries()); var current = plan.Cash.Single(x => x.Claim.Kind == "Balance");
            Assert.That(current.Remaining, Is.EqualTo(6000)); Assert.That(plan.Fault, Is.Null);
            Assert.That(host.AllocateCutover(new SettlementIntent("too-much", host.Version, current.Claim.Id, "public", 6001)).Code, Is.EqualTo(SettlementCode.InvalidClaim));
            var command = new SettlementIntent("remaining", host.Version, current.Claim.Id, "public", 6000);
            Assert.That(host.AllocateCutover(command).Code, Is.EqualTo(SettlementCode.Finalized)); Assert.That(host.AllocateCutover(command).Code, Is.EqualTo(SettlementCode.Replay));
            Assert.That(host.ReadAccount("public").BalanceMinor, Is.EqualTo(10000)); Assert.That(host.ReadAudit().ActualReserveMinor, Is.EqualTo(20000));
            var loaded = RoundTripHost(host); Assert.That(loaded.ReadAudit().Fault, Is.Null); Assert.That(loaded.ReadAccount("public").BalanceMinor, Is.EqualTo(10000));
        }
        [Test] public void B242_LatestClaimsCannotBeFundedWithoutRealTreasuryOrToAnotherParty()
        {
            var state = LegacyRights(); var raw = Archive(state); var host = new PawnSettlementSaveHost(); host.CaptureLegacy(raw.Raw, "initial"); host.CaptureCutover(raw.Raw);
            var claim = raw.Claims.First();
            Assert.That(host.AllocateCutover(new SettlementIntent("no-funds", 0, claim.Id, claim.LegacyParty, claim.AmountMinor)).Code, Is.EqualTo(SettlementCode.InsufficientFunds));
            Assert.That(host.AllocateCutover(new SettlementIntent("wrong-owner", 0, claim.Id, "thief", 1)).Code, Is.EqualTo(SettlementCode.InvalidClaim));
            Assert.That(host.Version, Is.Zero); Assert.That(host.ReadAudit().TotalLiabilitiesMinor, Is.Zero);
        }
        [TestCase(true)][TestCase(false)]
        public void B242_PreviouslyFundedClaimMissingOrReducedInFinalStateIsConflict(bool missing)
        {
            var state = LegacyRights(); var original = Archive(state); var host = HostFunded(200); host.CaptureLegacy(original.Raw, "initial");
            var wage = original.Claims.Single(x => x.Kind == "UnpaidWage"); host.AllocateLegacy(new SettlementIntent("wage-part", host.Version, wage.Id, wage.LegacyParty, 500));
            var receipt = state.All<EconomyReceipt>().First(x => x.Type == "Wage"); if (missing) receipt.Paid = true; else receipt.Amount = 100;
            state.Revision++; var latest = Archive(state); var plan = Cutover(original, latest, host.ReadEntries());
            Assert.That(plan.Fault, Is.EqualTo(missing ? "LegacyAllocationMissingFinalClaim" : "LegacyAllocationExceedsFinalClaim"));
            Assert.That(host.ReadAccount(wage.LegacyParty).BalanceMinor, Is.EqualTo(500));
        }
        [Test] public void B242_OriginalAndLatestSameHashDoNotCountAllocationTwice()
        {
            var raw = Archive(LegacyRights()); var host = HostFunded(200); host.CaptureLegacy(raw.Raw, "initial");
            var claim = raw.Claims.First(); host.AllocateLegacy(new SettlementIntent("partial", host.Version, claim.Id, claim.LegacyParty, 100));
            var plan = Cutover(raw, raw, host.ReadEntries()); Assert.That(plan.Fault, Is.Null); Assert.That(plan.Cash.Single(x => x.Claim.Id == claim.Id).Allocated, Is.EqualTo(100));
        }
        [Test] public void B242_AllNineRecordKindsReceiveExplicitTreatmentsAndSourceRemainsFrozen()
        {
            var state = LegacyRights(); string before = PawnEconomySaveData.Encode(state); var raw = Archive(state); var data = new TreasuryOperationsSaveData();
            var plan = Cutover(raw, raw, operations: data); Assert.That(plan.Items.Select(x => x.Record.Kind).Distinct().Count(), Is.EqualTo(9));
            Assert.That(plan.Items.Single(x => x.Record.Kind == "EconomyOrder").Disposition, Is.EqualTo(LegacyDisposition.CancelOrderReleaseReservation));
            Assert.That(plan.Items.Single(x => x.Record.Kind == "EconomyContract").Disposition, Is.EqualTo(LegacyDisposition.EndContractPreserveWage));
            Assert.That(plan.Items.Single(x => x.Record.Kind == "EconomyOwnership").Disposition, Is.EqualTo(LegacyDisposition.RetainOwnership));
            foreach (var item in plan.Items) Assert.That(data.Acknowledge(raw.Hash, item.Key, item.Disposition), Is.True);
            long version = data.Revision; var first = plan.Items.First(); Assert.That(data.Acknowledge(raw.Hash, first.Key, first.Disposition), Is.True); Assert.That(data.Revision, Is.EqualTo(version));
            Assert.That(Cutover(raw, raw, operations: RoundTripOperations(data)).Items.All(x => x.Acknowledged), Is.True);
            Assert.That(Cutover(raw, raw, operations: data).Complete, Is.False, "Acknowledgements cannot substitute for actual funds.");
            Assert.That(PawnEconomySaveData.Encode(state), Is.EqualTo(before));
        }
        [Test] public void B242_DecisionsCannotMoveToAnotherSnapshotOrAdoptWrongTreatment()
        {
            var state = LegacyRights(); var before = Archive(state); var data = new TreasuryOperationsSaveData();
            var item = Cutover(before, before).Items.First(); data.Acknowledge(before.Hash, item.Key, LegacyDisposition.RetainOwnership);
            Assert.That(Cutover(before, before, operations: data).Fault, Is.EqualTo("CutoverDecisionBindingChanged"));
            state.Revision++; var latest = Archive(state); Assert.That(Cutover(before, latest, operations: data).Fault, Is.EqualTo("CutoverDecisionBindingChanged"));
            Assert.That(data.Acknowledge(latest.Hash, "other", LegacyDisposition.PreserveHistory), Is.False);
        }
        [TestCase(true)][TestCase(false)]
        public void B242_PaidUnusedAssetsCannotLoseOrDuplicateOwnership(bool missing)
        {
            var state = LegacyRights(); var before = Archive(state); var owner = state.All<EconomyOwnership>().Single();
            if (missing) state.Records.Remove(owner); else state.Add(new EconomyOwnership { AssetKey = owner.AssetKey, OwnerKey = "other" });
            var plan = Cutover(before, Archive(state)); Assert.That(plan.Fault, Is.EqualTo(missing ? "AcquiredAssetOwnershipMissing" : "ConflictingAssetOwnership"));
        }
        [Test] public void B242_DifferencesAndOriginalOnlyRecordsRemainExplicitlyVisible()
        {
            var state = LegacyRights(); var before = Archive(state); state.Records.Remove(state.All<EconomyReport>().Single());
            state.All<EconomyReceipt>().First(x => x.Type == "Wage").Paid = true; state.Revision++;
            var latest = Archive(state); var plan = Cutover(before, latest);
            Assert.That(plan.Fault, Is.Null); Assert.That(plan.Items.Single(x => x.Record.Kind == "EconomyReport").OriginalOnly, Is.True);
            Assert.That(plan.Items.Any(x => x.Record.Kind == "EconomyReceipt" && x.Changed), Is.True); Assert.That(plan.Cash.Any(x => x.Claim.Kind == "UnpaidWage"), Is.False);
            state.All<EconomyPerson>().Single().AcquiredAsset = null; state.Records.Remove(state.All<EconomyOwnership>().Single());
            var removedTitle = Cutover(before, Archive(state)).Items.Single(x => x.Record.Kind == "EconomyOwnership");
            Assert.That(removedTitle.OriginalOnly, Is.True); Assert.That(removedTitle.Disposition, Is.EqualTo(LegacyDisposition.PreserveHistory));
        }
        [Test] public void B242_CompleteOfflineCutoverScribeKeepsFundsRightsAndGameAcceptanceGate()
        {
            var component = new GameComponent_PawnEconomy(null); var state = LegacyRights(); Field<PawnEconomySaveData>(component, "save").State = state;
            SetField(component, "settlement", HostFunded(200)); component.ApplyInstitutionModeSetting(true);
            var plan = component.ReadCutoverPlan(); Assert.That(plan.Fault, Is.Null);
            foreach (var claim in plan.Cash)
                Assert.That(component.ExecuteTreasuryCommand("Treasury.FundClaim", 0, claim.Claim.Id, null, component.QueryTreasuryStamp()), Is.True);
            foreach (var item in plan.Items)
                Assert.That(component.ExecuteTreasuryCommand("Treasury.Acknowledge", 0, item.Key, null, component.QueryTreasuryStamp()), Is.True);
            Assert.That(component.ReadCutoverPlan().Complete, Is.True); Assert.That(component.QueryInstitutionMode().Blockers, Is.EquivalentTo(new[] { "SaveAcceptance" }));
            Assert.That(component.QueryInstitutionMode().Phase, Is.EqualTo(InstitutionPhase.Preparing)); Assert.That(component.QueryInstitutionMode().NewCommitmentsAllowed, Is.False);
            string path = PathForTest(); Scribe.saver.InitSaving(path, "savegame"); component.ExposeData(); Scribe.saver.FinalizeSaving();
            var loaded = new GameComponent_PawnEconomy(null); Scribe.loader.InitLoading(path); loaded.ExposeData(); Scribe.loader.FinalizeLoading();
            loaded.ApplyInstitutionModeSetting(true); Assert.That(loaded.ReadCutoverPlan().Complete, Is.True);
            var host = Field<PawnSettlementSaveHost>(loaded, "settlement"); Assert.That(host.ReadAccount("public").BalanceMinor, Is.EqualTo(10000));
            Assert.That(host.ReadAccount("public").ReservedMinor, Is.Zero); Assert.That(host.ReadAccount("pawn").BalanceMinor, Is.EqualTo(500));
            Assert.That(Field<PawnEconomySaveData>(loaded, "save").State.All<EconomyAccount>().Single().Reserved, Is.EqualTo(600));
            // FA-03（2026-10-10 审计）：cutover 已完成、在途承诺为空 ⇒ 托管余额与专用储备是**正常保留资产**，
            // 关闭后不得卡在 Draining（原断言把"账上有钱"当成必须清空的未完成事项，正是本条审计缺陷）。
            loaded.ApplyInstitutionModeSetting(false);
            Assert.That(loaded.QueryInstitutionMode().Phase, Is.EqualTo(InstitutionPhase.Off));
            Assert.That(loaded.QueryInstitutionMode().HasOutstandingRights, Is.False);
        }
        [Test] public void B242_CommandsRejectOffFaultStaleAndUnknownVerbWithoutAnyFunding()
        {
            var component = new GameComponent_PawnEconomy(null); var stamp = component.QueryTreasuryStamp();
            Assert.That(component.ExecuteTreasuryCommand("Treasury.Deposit", 25, "bank", "pawn", stamp), Is.False);
            component.ApplyInstitutionModeSetting(true); Assert.That(component.ExecuteTreasuryCommand("Treasury.Deposit", 25, "bank", "pawn", stamp), Is.False);
            Assert.That(component.ExecuteTreasuryCommand("Treasury.Unknown", 0, null, null, component.QueryTreasuryStamp()), Is.False);
            Assert.That(Field<PawnSettlementSaveHost>(component, "settlement").ReadAccount("Treasury").BalanceMinor, Is.Zero);
        }
        [Test] public void B242_CutoverWireHashTamperingPreservesRawAndBlocksMoney()
        {
            var host = HostFunded(); var raw = Archive(LegacyRights()); host.CaptureLegacy(raw.Raw, "initial"); host.CaptureCutover(raw.Raw);
            var loaded = RoundTripHost(host, node => node.Element("legacyCutoverHash").Value = "tampered");
            Assert.That(loaded.ReadAudit().Fault, Is.Not.Null); Assert.That(loaded.ReadAudit().TotalLiabilitiesMinor, Is.Null);
            Assert.That(loaded.ReadAudit().ActualReserveMinor, Is.EqualTo(10000));
            Assert.That(Field<string>(RoundTripHost(loaded), "cutoverHash"), Is.EqualTo("tampered"));
        }
        [Test] public void B242_ControllerRecoversCompletedPhysicalWithdrawalWithoutMovingItAgain()
        {
            var host = HostFunded(); var party = new Holder(); var stack = Contents(Reserve(host)).Single();
            var intent = HostIntent(host, party, stack, 25, "controller-withdraw", ReserveMovementKind.Withdrawal);
            var ops = new TreasuryOperationsSaveData(); ops.Add(new TreasuryDelivery(1, intent.CommandId, ReserveMovementKind.Withdrawal,
                TreasuryDeliveryPhase.Waiting, party.Id, "bank", 0, stack.GetUniqueLoadID(), null, 25, 5000, intent.ExpectedVersion, intent.Movement.ExpectedVersion));
            host.PreparePhysical(intent); host.DeliverPreparedPhysical(intent, party.Items);
            var component = new GameComponent_PawnEconomy(null); SetField(component, "settlement", RoundTripHost(host));
            SetField(component, "treasuryOperations", RoundTripOperations(ops)); component.ApplyInstitutionModeSetting(true);
            var loaded = Field<TreasuryOperationsSaveData>(component, "treasuryOperations");
            Assert.That(loaded.Find(1).Phase, Is.EqualTo(TreasuryDeliveryPhase.Completed));
            Assert.That(loaded.Find(1).CarryKey, Is.EqualTo(party.Items.Single().GetUniqueLoadID())); Assert.That(loaded.Fault, Is.Null);
            Assert.That(party.Items.Single().stackCount, Is.EqualTo(25));
            Assert.That(Field<PawnSettlementSaveHost>(component, "settlement").ReadAccount("Treasury").BalanceMinor, Is.EqualTo(7500));
        }
        [Test] public void B242_ControllerOrphanCompletedOrderFreezesWithoutFabricatingProof()
        {
            var ops = new TreasuryOperationsSaveData(); ops.Add(Delivery()); ops.Update(Delivery().With(TreasuryDeliveryPhase.Completed));
            var component = new GameComponent_PawnEconomy(null); SetField(component, "treasuryOperations", ops); component.ApplyInstitutionModeSetting(true);
            Assert.That(ops.Fault, Is.EqualTo("TreasuryDeliveryBindingConflict"));
            Assert.That(component.QueryInstitutionMode().Phase, Is.EqualTo(InstitutionPhase.Faulted));
            Assert.That(RoundTripOperations(ops).Fault, Is.EqualTo(ops.Fault)); Assert.That(Field<PawnSettlementSaveHost>(component, "settlement").Version, Is.Zero);
        }
        [Test] public void B242_ControllerBeforePhysicalDeliveryLoadsCancelledAndReleasesHeldFunds()
        {
            var host = HostFunded(); var party = new Holder(); var stack = Contents(Reserve(host)).Single();
            var intent = HostIntent(host, party, stack, 25, "before-carry", ReserveMovementKind.Withdrawal); host.PreparePhysical(intent);
            var ops = new TreasuryOperationsSaveData(); ops.Add(new TreasuryDelivery(1, intent.CommandId, ReserveMovementKind.Withdrawal,
                TreasuryDeliveryPhase.Waiting, party.Id, "bank", 0, stack.GetUniqueLoadID(), null, 25, 5000, intent.ExpectedVersion, intent.Movement.ExpectedVersion));
            var component = new GameComponent_PawnEconomy(null); SetField(component, "settlement", RoundTripHost(host));
            SetField(component, "treasuryOperations", RoundTripOperations(ops)); component.ApplyInstitutionModeSetting(true);
            Assert.That(Field<TreasuryOperationsSaveData>(component, "treasuryOperations").Find(1).Phase, Is.EqualTo(TreasuryDeliveryPhase.Cancelled));
            Assert.That(Field<PawnSettlementSaveHost>(component, "settlement").ReadAccount("Treasury").ReservedMinor, Is.Zero);
            Assert.That(Field<PawnSettlementSaveHost>(component, "settlement").ReadAudit().ActualReserveMinor, Is.EqualTo(10000));
        }
        [Test] public void B242_ControllerCannotClaimAnotherCommandsMovementByReusingItsName()
        {
            var host = HostFunded(); var ops = new TreasuryOperationsSaveData(); ops.Add(Delivery(command: "deposit").With(TreasuryDeliveryPhase.Completed));
            var component = new GameComponent_PawnEconomy(null); SetField(component, "settlement", host); SetField(component, "treasuryOperations", ops);
            component.ApplyInstitutionModeSetting(true); Assert.That(component.QueryInstitutionMode().Phase, Is.EqualTo(InstitutionPhase.Faulted));
            Assert.That(host.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(10000)); Assert.That(host.ReadPhysicalEvidence("deposit").Intent.SilverCount, Is.EqualTo(100));
        }
        [Test] public void B242_UnknownOperationsFreezeModeAndRejectAdoption()
        {
            var ops = RoundTripOperations(new TreasuryOperationsSaveData(), node => WriteWireText(node, "payload", "<future />"));
            var component = new GameComponent_PawnEconomy(null); SetField(component, "treasuryOperations", ops); component.ApplyInstitutionModeSetting(true);
            Assert.That(component.QueryInstitutionMode().Phase, Is.EqualTo(InstitutionPhase.Faulted));
            Assert.That(component.ExecuteTreasuryCommand("Treasury.Acknowledge", 0, "anything", null, component.QueryTreasuryStamp()), Is.False);
        }
    }
}
