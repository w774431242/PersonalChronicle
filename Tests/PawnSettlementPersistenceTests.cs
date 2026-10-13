using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Text;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Data;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Economy.PawnEconomy;
using Verse;

namespace PersonalChronicle.Tests
{
    // Shares the existing strict offline Scribe fixture; core Thing/Scribe methods remain original.
    public sealed partial class PawnSilverReserveTests
    {
        private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        private static void SetField(object value, string name, object content) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, content);
        private static SettlementReservePrototype Reserve(PawnSettlementSaveHost host) => Field<SettlementReservePrototype>(host, "reserve");
        private SettlementIntent HostIntent(PawnSettlementSaveHost host, Holder party, Thing source, int count = 10,
            string id = "deposit", ReserveMovementKind kind = ReserveMovementKind.Deposit)
            => new SettlementIntent(id, host.Version, new ReserveMovementIntent(id, kind, host.ReserveVersion, party.Id, source.GetUniqueLoadID(), count));
        private PawnSettlementSaveHost HostFunded(int count = 100)
        {
            var host = new PawnSettlementSaveHost(); var party = new Holder(); var source = Silver(party, count);
            Assert.That(host.ExecutePhysical(HostIntent(host, party, source, count), party.Items).Code, Is.EqualTo(SettlementCode.Finalized)); return host;
        }
        private PawnSettlementSaveHost RoundTripHost(PawnSettlementSaveHost host, Action<XElement> edit = null)
        {
            string path = PathForTest(); Scribe.saver.InitSaving(path, "savegame"); Scribe_Deep.Look(ref host, "settlement"); Scribe.saver.FinalizeSaving();
            if (edit != null) { var file = XDocument.Load(path); edit(file.Root.Element("settlement")); file.Save(path); }
            PawnSettlementSaveHost loaded = null; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "settlement"); Scribe.loader.FinalizeLoading();
            Assert.That(loaded, Is.Not.Null); return loaded;
        }
        private static void EditLedger(XElement host, Action<XElement> edit)
        { var root = XElement.Parse(WireText(host, "metadata")); edit(root); WriteWireText(host, "metadata", root.ToString(SaveOptions.DisableFormatting)); }
        private static string WireText(XElement host, string field) => Encoding.UTF8.GetString(Convert.FromBase64String(host.Element(field).Value));
        private static void WriteWireText(XElement host, string field, string text) => host.Element(field).Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        private static EconomyState LegacyRights()
        {
            var state = new EconomyState { Enabled = true, Funded = true };
            state.Add(new EconomyAccount { Key = "public", Balance = 10000, Reserved = 600 });
            state.Add(new EconomyPerson { PawnKey = "pawn", AcquiredAsset = "Apparel_345", Goal = EconomicGoalStatus.AcquiredPendingUse });
            state.Add(new EconomyOrder { OrderKind = EconomyOrderKind.Purchase, Status = EconomyOrderStatus.Active, ClientKey = "pawn", PayerKey = "public", SellerKey = "shop", AssetKey = "Apparel_999", Amount = 600, FundsHeld = true });
            state.Add(new EconomyReceipt { Key = "production:7:90", Type = "Wage", SubjectKey = "pawn", FromKey = "public", ToKey = "pawn", Amount = 500, Paid = false });
            state.Add(new EconomyReceipt { Key = "opening", Type = "OpeningAllocation", Amount = 100000, Paid = true });
            state.Add(new EconomyOwnership { AssetKey = "Apparel_345", OwnerKey = "pawn" });
            state.Add(new EconomyContract { PawnKey = "pawn", PositionId = 8, LastProductionJobId = 90 });
            state.Add(new EconomyInstitution { AnchorKey = "institution", InstitutionKind = InstitutionKind.Shop });
            state.Add(new EconomyPosition { InstitutionId = 8, Wage = 500 });
            state.Add(new EconomyReport { PawnKey = "pawn", DoctorKey = "doctor", VisibleConditions = new System.Collections.Generic.List<string> { "scar" } });
            return state;
        }
        [Test] public void B23_EmptyHostCannotIssueOpeningMoneyOrCreateAccountsByReading()
        {
            var host = RoundTripHost(new PawnSettlementSaveHost()); var audit = host.ReadAudit();
            Assert.That(audit.Fault, Is.Null); Assert.That(audit.TotalLiabilitiesMinor, Is.Zero); Assert.That(audit.ActualReserveMinor, Is.Zero);
            Assert.That(host.ReadAccount("pawn"), Is.Null); Assert.That(host.CanExecuteInGame, Is.False); Assert.That(audit.CanExecuteInGame, Is.False);
        }
        [Test] public void B23_WholeHostRoundTripBindsPhysicalSilverAndCreditExactlyOnce()
        {
            var host = new PawnSettlementSaveHost(); var party = new Holder(); var source = Silver(party);
            var command = HostIntent(host, party, source, 27); host.ExecutePhysical(command, party.Items);
            var loaded = RoundTripHost(host); Assert.That(loaded.ReadAudit().Fault, Is.Null);
            Assert.That(loaded.ReadAudit().TotalLiabilitiesMinor, Is.EqualTo(2700)); Assert.That(loaded.ReadAudit().ActualReserveMinor, Is.EqualTo(2700));
            Assert.That(loaded.ExecutePhysical(command, null).Code, Is.EqualTo(SettlementCode.Replay));
            Assert.That(loaded.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(2700)); Assert.That(party.Items.Sum(x => x.stackCount), Is.EqualTo(73));
            Assert.That(Contents(Reserve(loaded))[0].holdingOwner, Is.SameAs(Contents(Reserve(loaded))));
        }
        [Test] public void B23_WithdrawalIsTreasuryFundedAndReplayDoesNotDeliverTwice()
        {
            var host = HostFunded(); var party = new Holder(); var stack = Contents(Reserve(host))[0];
            var command = HostIntent(host, party, stack, 25, "withdraw", ReserveMovementKind.Withdrawal);
            Assert.That(host.ExecutePhysical(command, party.Items).Code, Is.EqualTo(SettlementCode.Finalized));
            var loaded = RoundTripHost(host); Assert.That(loaded.ReadAudit().Fault, Is.Null);
            Assert.That(loaded.ExecutePhysical(command, null).Code, Is.EqualTo(SettlementCode.Replay));
            Assert.That(loaded.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(7500)); Assert.That(party.Items.Sum(x => x.stackCount), Is.EqualTo(25));
        }
        [Test] public void B23_ConflictingCommandAndStaleVersionsNeverMoveOrIssue()
        {
            var host = HostFunded(); var party = new Holder(); var source = Silver(party);
            var conflict = new SettlementIntent("deposit", 0, new ReserveMovementIntent("deposit", ReserveMovementKind.Deposit, 0, party.Id, source.GetUniqueLoadID(), 99));
            Assert.That(host.ExecutePhysical(conflict, party.Items).Code, Is.EqualTo(SettlementCode.CommandConflict));
            var stale = new SettlementIntent("stale", 0, new ReserveMovementIntent("stale", ReserveMovementKind.Deposit, 1, party.Id, source.GetUniqueLoadID(), 1));
            Assert.That(host.ExecutePhysical(stale, party.Items).Code, Is.EqualTo(SettlementCode.StaleVersion));
            Assert.That(host.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(10000)); Assert.That(source.stackCount, Is.EqualTo(100));
        }
        [Test] public void B23_PreflightFailureCancelsHoldButRetainsCommandTombstone()
        {
            var host = HostFunded(); var full = new Holder(true); Silver(full, 1); var stack = Contents(Reserve(host))[0];
            var command = HostIntent(host, full, stack, 25, "refused", ReserveMovementKind.Withdrawal);
            Assert.That(host.ExecutePhysical(command, full.Items).Code, Is.EqualTo(SettlementCode.Cancelled));
            var loaded = RoundTripHost(host); Assert.That(loaded.ReadAccount("Treasury").ReservedMinor, Is.Zero);
            Assert.That(loaded.ReadAudit().Fault, Is.Null); Assert.That(loaded.ExecutePhysical(command, null).Entry.Phase, Is.EqualTo(SettlementPhase.Cancelled));
        }
        [Test] public void B23_CompletedPhysicalDepositWithPreparedLedgerRecoversOnce()
        {
            var loaded = RoundTripHost(HostFunded(), node => EditLedger(node, ledger => ledger.Element("Command").SetAttributeValue("phase", "Prepared")));
            Assert.That(loaded.ReadAudit().Fault, Is.Null); Assert.That(loaded.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(10000));
            loaded.Recover(); var again = RoundTripHost(loaded); Assert.That(again.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(10000));
            Assert.That(again.ReadCommand("deposit").Phase, Is.EqualTo(SettlementPhase.Finalized));
        }
        [Test] public void B23_CompletedPhysicalWithdrawalWithPreparedLedgerRecoversOnce()
        {
            var host = HostFunded(); var party = new Holder(); var command = HostIntent(host, party, Contents(Reserve(host))[0], 25, "withdraw", ReserveMovementKind.Withdrawal);
            host.ExecutePhysical(command, party.Items);
            var loaded = RoundTripHost(host, node => EditLedger(node, ledger => ledger.Elements("Command").Last().SetAttributeValue("phase", "Prepared")));
            Assert.That(loaded.ReadAudit().Fault, Is.Null); Assert.That(loaded.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(7500));
            Assert.That(loaded.ReadAccount("Treasury").ReservedMinor, Is.Zero); Assert.That(party.Items.Sum(x => x.stackCount), Is.EqualTo(25));
        }
        [Test] public void B23_PreparedWithoutPhysicalMovementCancelsWithoutRetry()
        {
            var host = HostFunded(); var party = new Holder(); var command = HostIntent(host, party, Contents(Reserve(host))[0], 25, "unmoved", ReserveMovementKind.Withdrawal);
            Assert.That(Field<FundedSettlementLedger>(host, "ledger").Prepare(command).Code, Is.EqualTo(SettlementCode.Prepared));
            Assert.That(host.ReadAccount("Treasury").ReservedMinor, Is.EqualTo(2500));
            var loaded = RoundTripHost(host); Assert.That(loaded.ReadCommand("unmoved").Phase, Is.EqualTo(SettlementPhase.Cancelled));
            Assert.That(loaded.ReadAccount("Treasury").AvailableMinor, Is.EqualTo(10000)); Assert.That(party.Items, Is.Empty);
            Assert.That(loaded.ExecutePhysical(command, party.Items).Code, Is.EqualTo(SettlementCode.Replay));
        }
        [Test] public void B23_UncertainWithdrawalPreservesLockedMoneyAndObjectsAcrossSave()
        {
            var host = HostFunded(); var party = new Holder(); party.Added = () => throw new InvalidOperationException("callback");
            var command = HostIntent(host, party, Contents(Reserve(host))[0], 25, "uncertain", ReserveMovementKind.Withdrawal);
            Assert.That(host.ExecutePhysical(command, party.Items).Code, Is.EqualTo(SettlementCode.ReconciliationRequired));
            var loaded = RoundTripHost(host); Assert.That(loaded.ReadAudit().Fault, Is.Not.Null);
            Assert.That(loaded.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(10000)); Assert.That(loaded.ReadAccount("Treasury").ReservedMinor, Is.EqualTo(2500));
            Assert.That(loaded.ReadAudit().DeficitMinor, Is.EqualTo(2500)); Assert.That(party.Items.Sum(x => x.stackCount), Is.EqualTo(25));
            Assert.That(loaded.ExecutePhysical(command, party.Items).Code, Is.EqualTo(SettlementCode.Replay));
        }
        [Test] public void B23_ReserveDestructionReportsGapWithoutWritingOffBalances()
        {
            var host = HostFunded(); Contents(Reserve(host))[0].Destroy(); var loaded = RoundTripHost(host);
            Assert.That(loaded.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(10000)); Assert.That(loaded.ReadAudit().DeficitMinor, Is.EqualTo(10000));
            Assert.That(loaded.ReadAudit().Fault, Is.Not.Null);
            var party = new Holder(); var silver = Silver(party); Assert.That(loaded.ExecutePhysical(HostIntent(loaded, party, silver, id: "new"), party.Items).Code, Is.EqualTo(SettlementCode.NotReady));
        }
        [Test] public void B23_OrphanReserveProofCannotBeConsumedAsMoney()
        {
            var host = new PawnSettlementSaveHost(); var party = new Holder(); var silver = Silver(party);
            Reserve(host).Execute(Intent(Reserve(host), party, silver), party.Items);
            var loaded = RoundTripHost(host); Assert.That(loaded.ReadAudit().Fault, Is.EqualTo("OrphanOrConflictingReserveProof"));
            Assert.That(loaded.ReadAccount("Treasury").BalanceMinor, Is.Zero); Assert.That(loaded.ReadAudit().ActualReserveMinor, Is.EqualTo(1000));
        }
        [Test] public void B23_FinalizedWithoutProofAndConflictingProofFreeze()
        {
            var missing = RoundTripHost(HostFunded(), node => { node.Element("reserve").Element("metadata").Value = ReserveJournalCodec.Encode(0, null, Array.Empty<ReserveMovementEvidence>()); });
            Assert.That(missing.ReadAudit().Fault, Is.EqualTo("FinalizedWithoutPhysicalProof"));
            var conflict = RoundTripHost(HostFunded(), node => EditLedger(node, ledger => ledger.Element("Command").SetAttributeValue("party", "changed")));
            Assert.That(conflict.ReadAudit().Fault, Is.EqualTo("OrphanOrConflictingReserveProof"));
        }
        [TestCase("<SettlementLedger schema=\"99\"><未来 内容=\"保留\" /></SettlementLedger>")]
        [TestCase("<broken>")]
        [TestCase("<SettlementLedger schema=\"1\" version=\"0\" unknown=\"保留\" />")]
        public void B23_UnknownLedgerPreservesRawAndPhysicalReserveWithUnknownLiability(string raw)
        {
            var loaded = RoundTripHost(HostFunded(), node => WriteWireText(node, "metadata", raw));
            Assert.That(loaded.ReadAudit().TotalLiabilitiesMinor, Is.Null); Assert.That(loaded.ReadAudit().ActualReserveMinor, Is.EqualTo(10000));
            Assert.That(loaded.ReadAccount("Treasury"), Is.Null); var again = RoundTripHost(loaded);
            Assert.That(Field<string>(again, "metadata"), Is.EqualTo(raw)); Assert.That(again.ReadAudit().ActualReserveMinor, Is.EqualTo(10000));
        }
        [Test] public void B23_LegacyArchiveRetainsAllRightsAndDoesNotFundTreasury()
        {
            var host = new PawnSettlementSaveHost(); string raw = PawnEconomySaveData.Encode(LegacyRights()); host.CaptureLegacy(raw, "BeforeLegacyRecovery");
            host.CaptureLegacy("overwritten", "other"); var loaded = RoundTripHost(host); var original = loaded.ReadLegacyArchive();
            Assert.That(original.Raw, Is.EqualTo(raw)); Assert.That(original.Fault, Is.Null); Assert.That(original.Report.Records.Count, Is.EqualTo(10));
            Assert.That(original.Report.HeldOrderAmountsMinor, Is.EqualTo(600)); Assert.That(original.Report.UnpaidWagesMinor, Is.EqualTo(500));
            Assert.That(original.Claims.Count, Is.EqualTo(2)); Assert.That(original.Claims.Sum(x => x.AmountMinor), Is.EqualTo(10500));
            Assert.That(PawnEconomySaveData.Decode(original.Raw).All<EconomyPerson>().Single().AcquiredAsset, Is.EqualTo("Apparel_345"));
            Assert.That(loaded.ReadAccount("Treasury").BalanceMinor, Is.Zero);
        }
        [Test] public void B23_ExplicitLegacyAllocationUsesOnlyFundedTreasuryAndIsBounded()
        {
            var host = HostFunded(); host.CaptureLegacy(PawnEconomySaveData.Encode(LegacyRights()), "BeforeLegacyRecovery");
            var claim = host.ReadLegacyArchive().Claims.Single(x => x.Kind == "UnpaidWage");
            var intent = new SettlementIntent("allocate", host.Version, claim.Id, "Pawn:pawn", 350);
            Assert.That(host.AllocateLegacy(intent).Code, Is.EqualTo(SettlementCode.Finalized));
            Assert.That(host.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(9650)); Assert.That(host.ReadAccount("Pawn:pawn").BalanceMinor, Is.EqualTo(350));
            Assert.That(host.AllocateLegacy(new SettlementIntent("overclaim", host.Version, claim.Id, "Pawn:pawn", 151)).Code, Is.EqualTo(SettlementCode.InvalidClaim));
            Assert.That(host.AllocateLegacy(new SettlementIntent("remap", host.Version, claim.Id, "Pawn:other", 150)).Code, Is.EqualTo(SettlementCode.InvalidClaim));
            Assert.That(host.AllocateLegacy(new SettlementIntent("remaining", host.Version, claim.Id, "Pawn:pawn", 150)).Code, Is.EqualTo(SettlementCode.Finalized));
            var loaded = RoundTripHost(host); Assert.That(loaded.ReadAudit().Fault, Is.Null); Assert.That(loaded.ReadAudit().TotalLiabilitiesMinor, Is.EqualTo(10000));
            Assert.That(loaded.AllocateLegacy(intent).Code, Is.EqualTo(SettlementCode.Replay)); Assert.That(loaded.ReadAccount("Pawn:pawn").BalanceMinor, Is.EqualTo(500));
        }
        [Test] public void B23_UnfundedMigrationIsRejectedWithoutOpeningTargetAccount()
        {
            var host = new PawnSettlementSaveHost(); host.CaptureLegacy(PawnEconomySaveData.Encode(LegacyRights()), "BeforeLegacyRecovery");
            var claim = host.ReadLegacyArchive().Claims.First();
            Assert.That(host.AllocateLegacy(new SettlementIntent("unfunded", 0, claim.Id, "Pawn:pawn", 100)).Code, Is.EqualTo(SettlementCode.InsufficientFunds));
            Assert.That(host.Version, Is.Zero); Assert.That(host.ReadAccount("Pawn:pawn"), Is.Null);
        }
        [Test] public void B23_TreasuryWithdrawalCannotSpendMoneyAlreadyAllocatedToPawn()
        {
            var host = HostFunded(); host.CaptureLegacy(PawnEconomySaveData.Encode(LegacyRights()), "BeforeLegacyRecovery");
            var claim = host.ReadLegacyArchive().Claims.First(); host.AllocateLegacy(new SettlementIntent("all", host.Version, claim.Id, "Pawn:pawn", 10000));
            var party = new Holder(); Assert.That(host.ExecutePhysical(HostIntent(host, party, Contents(Reserve(host))[0], 1, "withdraw", ReserveMovementKind.Withdrawal), party.Items).Code,
                Is.EqualTo(SettlementCode.InsufficientFunds)); Assert.That(host.ReadAudit().ActualReserveMinor, Is.EqualTo(10000));
        }
        [TestCase("<broken>")]
        [TestCase("<PawnEconomyEnvelope><Schema>999</Schema><Extra>保留</Extra></PawnEconomyEnvelope>")]
        [TestCase("<PawnEconomyEnvelope><Schema>1</Schema><Extra>保留</Extra></PawnEconomyEnvelope>")]
        public void B23_UnknownLegacyArchiveIsPreservedWithoutClaims(string raw)
        {
            var host = HostFunded(); host.CaptureLegacy(raw, "BeforeLegacyRecovery"); var loaded = RoundTripHost(host);
            Assert.That(loaded.ReadLegacyArchive().Raw, Is.EqualTo(raw)); Assert.That(loaded.ReadLegacyArchive().Claims, Is.Empty);
            Assert.That(loaded.ReadLegacyArchive().Fault, Is.Not.Null); Assert.That(RoundTripHost(loaded).ReadLegacyArchive().Raw, Is.EqualTo(raw));
        }
        [Test] public void B23_ArchiveTamperingFreezesAllNewWrites()
        {
            var host = HostFunded(); host.CaptureLegacy(PawnEconomySaveData.Encode(LegacyRights()), "BeforeLegacyRecovery");
            var loaded = RoundTripHost(host, node => WriteWireText(node, "legacyOriginalPayload", WireText(node, "legacyOriginalPayload") + " "));
            Assert.That(loaded.ReadAudit().Fault, Is.EqualTo("LegacyArchiveHashMismatch")); Assert.That(loaded.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(10000));
        }
        [Test] public void B23_GameComponentArchivesOriginalPayloadAndPreservesLegacyOnLoad()
        {
            string path = PathForTest(); string raw = PawnEconomySaveData.Encode(LegacyRights());
            new XDocument(new XElement("savegame", new XElement("pawnEconomicInstitutions", new XElement("payload", raw)))).Save(path);
            var component = new GameComponent_PawnEconomy(null);
            Scribe.loader.InitLoading(path); component.ExposeData(); Scribe.loader.FinalizeLoading();
            // In a full Game, Game.ExposeData invokes component.ExposeData for every Scribe phase.
            // The standalone root isn't registered as a saveable, so invoke its exact post-load hook.
            Scribe.mode = LoadSaveMode.PostLoadInit; component.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
            var original = Field<PawnSettlementSaveHost>(component, "settlement").ReadLegacyArchive();
            var current = Field<PawnEconomySaveData>(component, "save").State;
            Assert.That(original.Raw, Is.EqualTo(raw)); Assert.That(original.Source, Is.EqualTo("BeforeLegacyRecovery"));
            Assert.That(PawnEconomySaveData.Decode(original.Raw).All<EconomyOrder>().Single().Status, Is.EqualTo(EconomyOrderStatus.Active));
            // B2.4.1 retires the old automatic cancellation hook; history is now frozen in place.
            Assert.That(current.All<EconomyOrder>().Single().Status, Is.EqualTo(EconomyOrderStatus.Active)); Assert.That(current.All<EconomyAccount>().Single().Reserved, Is.EqualTo(600));
            string next = PathForTest(); Scribe.saver.InitSaving(next, "savegame"); component.ExposeData(); Scribe.saver.FinalizeSaving();
            Assert.That(WireText(XDocument.Load(next).Root.Element("pawnEconomicSettlement"), "legacyOriginalPayload"), Is.EqualTo(original.Raw));
        }
        [Test] public void B23_GameComponentFirstSavePersistsArchiveInSameSave()
        {
            var component = new GameComponent_PawnEconomy(null); Field<PawnEconomySaveData>(component, "save").State = LegacyRights();
            string path = PathForTest(); Scribe.saver.InitSaving(path, "savegame"); component.ExposeData(); Scribe.saver.FinalizeSaving();
            var node = XDocument.Load(path).Root.Element("pawnEconomicSettlement");
            Assert.That(node.Element("legacyCaptured").Value, Is.EqualTo("True")); Assert.That(node.Element("legacyArchiveSource").Value, Is.EqualTo("FirstSaveSnapshot"));
            Assert.That(WireText(node, "legacyOriginalPayload"), Does.Contain("Apparel_345"));
        }
        [Test] public void B23_WrongThreadCannotQueryOrExecuteCommands()
        {
            var host = HostFunded(); Assert.That(Task.Run(() => Assert.Throws<InvalidOperationException>(() => host.ReadAudit())).GetAwaiter().GetResult(), Is.Not.Null);
            Assert.That(host.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(10000));
        }
        [Test] public void B23_SerializedSaveBoundaryCanMoveFromLoadingWorkerToSavingThread()
        {
            var host = Task.Run(() => new PawnSettlementSaveHost()).GetAwaiter().GetResult();
            var loaded = RoundTripHost(host); Assert.That(loaded.ReadAudit().Fault, Is.Null); Assert.That(loaded.ReadAudit().TotalLiabilitiesMinor, Is.Zero);
        }
        [Test] public void B23_ReentrantSaveIsRejectedAndCannotHideUncertainMovement()
        {
            var host = new PawnSettlementSaveHost(); var party = new Holder(); var silver = Silver(party);
            party.Removed = () => host.ExposeData();
            var result = host.ExecutePhysical(HostIntent(host, party, silver, 100), party.Items);
            Assert.That(result.Code, Is.EqualTo(SettlementCode.ReconciliationRequired)); var loaded = RoundTripHost(host);
            Assert.That(loaded.ReadAccount("Treasury").BalanceMinor, Is.Zero); Assert.That(loaded.ReadAudit().Fault, Is.Not.Null);
            Assert.That(Contents(Reserve(loaded)).Sum(x => x.stackCount), Is.EqualTo(100));
        }
        [Test] public void B23_RawTextEncodingPreservesMixedNewlinesAndUnicodeExactly()
        {
            const string raw = "未来档\n仅LF\r仅CR\r\nCRLF\t<&> 家庭权益";
            var host = new PawnSettlementSaveHost(); host.CaptureLegacy(raw, "BeforeLegacyRecovery");
            var loaded = RoundTripHost(host); Assert.That(loaded.ReadLegacyArchive().Raw, Is.EqualTo(raw));
            Assert.That(loaded.ReadAudit().Fault, Is.Null); Assert.That(RoundTripHost(loaded).ReadLegacyArchive().Raw, Is.EqualTo(raw));
            const string future = "<SettlementLedger schema=\"99\">\n未知字段\r\n保留\r</SettlementLedger>";
            var unknown = RoundTripHost(host, node => WriteWireText(node, "metadata", future));
            Assert.That(Field<string>(RoundTripHost(unknown), "metadata"), Is.EqualTo(future));
        }
        [TestCase("unsupported-v9", "future-wire")]
        [TestCase("base64-utf8-v1", "invalid-%%%")]
        public void B23_UnknownOrInvalidTextEncodingPreservesWireAndPhysicalObjects(string encoding, string wire)
        {
            var loaded = RoundTripHost(HostFunded(), node => { node.Element("metadataEncoding").Value = encoding; node.Element("metadata").Value = wire; });
            Assert.That(loaded.ReadAudit().TotalLiabilitiesMinor, Is.Null); Assert.That(loaded.ReadAudit().ActualReserveMinor, Is.EqualTo(10000));
            Assert.That(loaded.ReadLegacyArchive(), Is.Null); var again = RoundTripHost(loaded);
            Assert.That(Field<string>(again, "metadataWire"), Is.EqualTo(wire)); Assert.That(Field<string>(again, "metadataEncoding"), Is.EqualTo(encoding));
        }
        [Test] public void B23_WithdrawalLeavesFractionalTreasuryBalanceAndPawnBackingIntact()
        {
            var host = HostFunded(); host.CaptureLegacy(PawnEconomySaveData.Encode(LegacyRights()), "BeforeLegacyRecovery");
            var wage = host.ReadLegacyArchive().Claims.Single(x => x.Kind == "UnpaidWage"); host.AllocateLegacy(new SettlementIntent("fraction", host.Version, wage.Id, "Pawn:pawn", 350));
            var party = new Holder(); Assert.That(host.ExecutePhysical(HostIntent(host, party, Contents(Reserve(host))[0], 96, "withdraw", ReserveMovementKind.Withdrawal), party.Items).Code,
                Is.EqualTo(SettlementCode.Finalized));
            var loaded = RoundTripHost(host); Assert.That(loaded.ReadAudit().Fault, Is.Null); Assert.That(loaded.ReadAccount("Treasury").BalanceMinor, Is.EqualTo(50));
            Assert.That(loaded.ReadAccount("Pawn:pawn").BalanceMinor, Is.EqualTo(350)); Assert.That(loaded.ReadAudit().ActualReserveMinor, Is.EqualTo(400));
        }
        [Test] public void B23_PersistedAllocationBeyondArchivedEntitlementFreezes()
        {
            var host = HostFunded(); host.CaptureLegacy(PawnEconomySaveData.Encode(LegacyRights()), "BeforeLegacyRecovery");
            var wage = host.ReadLegacyArchive().Claims.Single(x => x.Kind == "UnpaidWage"); host.AllocateLegacy(new SettlementIntent("wage", host.Version, wage.Id, "Pawn:pawn", 500));
            var loaded = RoundTripHost(host, node => EditLedger(node, ledger => ledger.Elements("Command").Last().SetAttributeValue("amount", 501)));
            Assert.That(loaded.ReadAudit().Fault, Is.EqualTo("InvalidLegacyAllocation")); Assert.That(loaded.ReadAudit().TotalLiabilitiesMinor, Is.EqualTo(10000));
        }
        [Test] public void B23_PreparedReserveStageNeverBecomesDepositCredit()
        {
            var host = new PawnSettlementSaveHost(); var party = new Holder(); var source = Silver(party);
            var command = HostIntent(host, party, source); Field<FundedSettlementLedger>(host, "ledger").Prepare(command);
            var prepared = new ReserveMovementEvidence(command.Movement, ReserveMovementPhase.Prepared, 0, null, 100, null);
            Metadata(Reserve(host), ReserveJournalCodec.Encode(1, null, new[] { prepared }));
            var loaded = RoundTripHost(host); Assert.That(loaded.ReadCommand("deposit").Phase, Is.EqualTo(SettlementPhase.ReconciliationRequired));
            Assert.That(loaded.ReadAccount("Treasury").BalanceMinor, Is.Zero); Assert.That(loaded.ReadAudit().Fault, Is.Not.Null); Assert.That(source.stackCount, Is.EqualTo(100));
        }
    }

    [TestFixture]
    public sealed class PawnSettlementLedgerTests
    {
        private static SettlementIntent Deposit(string id = "d", long version = 0) => new SettlementIntent(id, version, new ReserveMovementIntent(id, ReserveMovementKind.Deposit, version, "party", "silver", 1));
        private static ReserveMovementEvidence Proof(SettlementIntent intent) => new ReserveMovementEvidence(intent.Movement, ReserveMovementPhase.Completed, 0, 1, 1, 0, "silverResult", 1);
        [Test] public void OnlyCompletedMatchingQuantityProofCanIssueCredit()
        {
            var ledger = new FundedSettlementLedger(); var command = Deposit(); ledger.Prepare(command);
            Assert.That(ledger.CompletePhysical("d", null).Code, Is.EqualTo(SettlementCode.ReconciliationRequired)); Assert.That(ledger.TotalMinor, Is.Zero);
            Assert.That(ledger.CompletePhysical("d", Proof(command)).Code, Is.EqualTo(SettlementCode.Replay)); Assert.That(ledger.TotalMinor, Is.Zero);
        }
        [Test] public void OnlyOnePendingCommandAndGrossWithdrawalHoldAreAllowed()
        {
            var ledger = new FundedSettlementLedger(); var d = Deposit(); ledger.Prepare(d); ledger.CompletePhysical("d", Proof(d));
            var w = new SettlementIntent("w", 1, new ReserveMovementIntent("w", ReserveMovementKind.Withdrawal, 1, "party", "silverResult", 1));
            Assert.That(ledger.Prepare(w).Code, Is.EqualTo(SettlementCode.Prepared)); Assert.That(ledger.ReadAccount("Treasury").AvailableMinor, Is.Zero);
            Assert.That(ledger.Prepare(Deposit("new", 2)).Code, Is.EqualTo(SettlementCode.NotReady));
            ledger.Cancel("w", "refused"); Assert.That(ledger.ReadAccount("Treasury").AvailableMinor, Is.EqualTo(100));
        }
        [Test] public void CapacityRetainsAllTombstonesIncludingEarliestCancellation()
        {
            var ledger = new FundedSettlementLedger();
            for (int i = 0; i < FundedSettlementLedger.MaximumEntries; i++) { var d = Deposit("d" + i, i); ledger.Prepare(d); ledger.Cancel(d.CommandId, "fixture"); }
            Assert.That(ledger.Prepare(Deposit("overflow", ledger.Version)).Code, Is.EqualTo(SettlementCode.CapacityReached));
            var loaded = SettlementLedgerCodec.Decode(SettlementLedgerCodec.Encode(ledger, null, null)); Assert.That(loaded.PreserveRaw, Is.False);
            Assert.That(loaded.Ledger.Prepare(Deposit("d0", 0)).Code, Is.EqualTo(SettlementCode.Replay)); Assert.That(loaded.Ledger.TotalMinor, Is.Zero);
        }
        [TestCase("<SettlementLedger schema=\"1\" version=\"1\"><Command id=\"x\" expected=\"0\" kind=\"777\" amount=\"1\" phase=\"Finalized\" /></SettlementLedger>")]
        [TestCase("<SettlementLedger schema=\"1\" version=\"1\"><Command id=\"x\" expected=\"0\" kind=\"Withdrawal\" amount=\"100\" phase=\"Finalized\" reserveVersion=\"0\" party=\"p\" source=\"s\" count=\"1\" /></SettlementLedger>")]
        [TestCase("<!DOCTYPE x [<!ENTITY e 'unsafe'>]><SettlementLedger schema=\"1\" version=\"0\">&e;</SettlementLedger>")]
        public void InvalidPersistenceNeverReconstructsSpendableFunds(string raw)
        { var loaded = SettlementLedgerCodec.Decode(raw); Assert.That(loaded.PreserveRaw, Is.True); Assert.That(loaded.Ledger.TotalMinor, Is.Zero); }
    }
}
