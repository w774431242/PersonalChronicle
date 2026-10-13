using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using Verse;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class PublicInstitutionFundsTests
    {
        private const string Treasury = SettlementLedgerNames.Treasury;
        private static readonly string Shop = SettlementLedgerNames.Institution("shop");
        private static FundedSettlementLedger Funded() => FundedSettlementLedger.Restore(new[] {
            new SettlementEntry(new SettlementIntent("deposit", 0,
                new ReserveMovementIntent("deposit", ReserveMovementKind.Deposit, 0, "holder", "silver", 100)), SettlementPhase.Finalized) });
        private static SettlementIntent Move(FundedSettlementLedger ledger, string id, string from, string to, long amount)
        {
            var intent = SettlementIntent.ForPublicTransfer(id, ledger.Version, from, to, amount);
            Assert.That(ledger.Prepare(intent).Code, Is.EqualTo(SettlementCode.Prepared));
            Assert.That(ledger.CompleteTransfer(id).Code, Is.EqualTo(SettlementCode.Finalized)); return intent;
        }
        [Test] public void BudgetsAreColonyOwnedSubaccountsWithoutDoubleCounting()
        {
            var ledger = Funded(); var intent = Move(ledger, "allocate", Treasury, Shop, 3000);
            Assert.That(ledger.ReadAccount(Treasury).BalanceMinor, Is.EqualTo(7000));
            Assert.That(ledger.ReadAccount(Shop).BalanceMinor, Is.EqualTo(3000));
            Assert.That(SettlementLedgerNames.PublicTotal(ledger.ReadAccounts()), Is.EqualTo(10000));
            Assert.That(ledger.Prepare(intent).Code, Is.EqualTo(SettlementCode.Replay));
            Assert.That(ledger.TotalMinor, Is.EqualTo(10000));
            Move(ledger, "return", Shop, Treasury, 3000);
            Assert.That(ledger.ReadAccount(Treasury).BalanceMinor, Is.EqualTo(10000));
            Assert.That(ledger.ReadAccount(Shop).BalanceMinor, Is.Zero);
        }
        [Test] public void SubaccountWagesAndShopIncomeMoveExistingMoneyBetweenOwners()
        {
            var ledger = Funded(); Move(ledger, "allocate", Treasury, Shop, 3000);
            var wage = SettlementIntent.ForWage("wage", ledger.Version, "obligation", "pawn", 1500, Shop);
            Assert.That(ledger.Prepare(wage).Code, Is.EqualTo(SettlementCode.Prepared));
            Assert.That(ledger.ReadAccount(Shop).ReservedMinor, Is.EqualTo(1500));
            Assert.That(ledger.ReadAccount(Treasury).ReservedMinor, Is.Zero);
            Assert.That(ledger.CompleteTransfer("wage").Code, Is.EqualTo(SettlementCode.Finalized));
            Assert.That(SettlementLedgerNames.PublicTotal(ledger.ReadAccounts()), Is.EqualTo(8500));
            var sale = SettlementIntent.ForPurchase("sale", ledger.Version, "goods", "pawn", 500, Shop);
            Assert.That(ledger.Prepare(sale).Code, Is.EqualTo(SettlementCode.Prepared));
            Assert.That(ledger.ReadAccount("pawn").AvailableMinor, Is.EqualTo(1000));
            Assert.That(ledger.ReadAccount(Treasury).ReservedMinor, Is.Zero);
            Assert.That(ledger.CompletePurchase("sale").Code, Is.EqualTo(SettlementCode.Finalized));
            Assert.That(ledger.ReadAccount("pawn").BalanceMinor, Is.EqualTo(1000));
            Assert.That(ledger.ReadAccount(Shop).BalanceMinor, Is.EqualTo(2000));
            Assert.That(SettlementLedgerNames.PublicTotal(ledger.ReadAccounts()), Is.EqualTo(9000));
            Assert.That(ledger.TotalMinor, Is.EqualTo(10000));
            var restored = SettlementLedgerCodec.Decode(SettlementLedgerCodec.Encode(ledger, null, null));
            Assert.That(restored.Fault, Is.Null);
            Assert.That(restored.Ledger.Read("wage").Intent.SameAs(wage), Is.True);
            Assert.That(restored.Ledger.Read("sale").Intent.SameAs(sale), Is.True);
            Assert.That(SettlementLedgerNames.PublicTotal(restored.Ledger.ReadAccounts()), Is.EqualTo(9000));
        }
        [Test] public void OtherBudgetsCannotBackAnUnfundedInstitutionTransfer()
        {
            var ledger = Funded(); Move(ledger, "allocate", Treasury, Shop, 3000);
            Assert.That(ledger.Prepare(SettlementIntent.ForPublicTransfer("overspend", ledger.Version, Shop, Treasury, 3001)).Code,
                Is.EqualTo(SettlementCode.InsufficientFunds));
            Assert.That(ledger.Prepare(SettlementIntent.ForPublicTransfer("unknown", ledger.Version,
                SettlementLedgerNames.Institution("absent"), Treasury, 100)).Code, Is.EqualTo(SettlementCode.InsufficientFunds));
            Assert.That(ledger.ReadAccount(SettlementLedgerNames.Institution("absent")), Is.Null);
            Assert.That(ledger.TotalMinor, Is.EqualTo(10000));
        }
        [Test] public void PublicTransferRejectsPrivateEndpointsAndSelfTransfers()
        {
            Assert.Throws<ArgumentException>(() => SettlementIntent.ForPublicTransfer("a", 0, Treasury, "pawn", 100));
            Assert.Throws<ArgumentException>(() => SettlementIntent.ForPublicTransfer("b", 0, "pawn", Shop, 100));
            Assert.Throws<ArgumentException>(() => SettlementIntent.ForPublicTransfer("c", 0, Shop, Shop, 100));
            Assert.That(SettlementLedgerNames.IsPublic("PublicInstitution:"), Is.False);
        }
        [Test] public void PreparedBudgetIsReservedAndCancellationReleasesItWithoutChangingOwnership()
        {
            var ledger = Funded(); Move(ledger, "allocate", Treasury, Shop, 3000);
            var intent = SettlementIntent.ForPublicTransfer("return", ledger.Version, Shop, Treasury, 1200);
            ledger.Prepare(intent);
            Assert.That(ledger.ReadAccount(Shop).AvailableMinor, Is.EqualTo(1800));
            Assert.That(ledger.ReadAccount(Treasury).AvailableMinor, Is.EqualTo(7000));
            var restored = SettlementLedgerCodec.Decode(SettlementLedgerCodec.Encode(ledger, null, null));
            Assert.That(restored.Fault, Is.Null);
            Assert.That(restored.Ledger.ReadAccount(Shop).ReservedMinor, Is.EqualTo(1200));
            ledger.Cancel("return", "NoCommit");
            Assert.That(ledger.ReadAccount(Shop).AvailableMinor, Is.EqualTo(3000));
            Assert.That(SettlementLedgerNames.PublicTotal(ledger.ReadAccounts()), Is.EqualTo(10000));
        }
        [Test] public void FundedLegacyPublicClaimsAreCountedOnceAndCanReturnToTreasury()
        {
            var ledger = Funded();
            var claim = new SettlementIntent("claim", ledger.Version, "legacy-public", "public", 2000);
            Assert.That(ledger.Prepare(claim).Code, Is.EqualTo(SettlementCode.Prepared));
            ledger.CompleteAllocation("claim");
            Assert.That(SettlementLedgerNames.PublicTotal(ledger.ReadAccounts()), Is.EqualTo(10000));
            Move(ledger, "release", "public", Treasury, 2000);
            Assert.That(ledger.ReadAccount(Treasury).BalanceMinor, Is.EqualTo(10000));
            Assert.That(ledger.ReadAccount("public").BalanceMinor, Is.Zero);
            Assert.That(SettlementLedgerCodec.Decode(SettlementLedgerCodec.Encode(ledger, null, null)).Fault, Is.Null);
        }
    }

    public sealed partial class PawnSilverReserveTests
    {
        private static Faction publicBudgetFaction;
        private static bool BudgetFaction(ref Faction __result) { __result = publicBudgetFaction; return false; }
        [Test] public void ControllerBudgetsUseFreshCommandsAndPayrollPreservesOtherInstitutionQuotas()
        {
            var component = new GameComponent_PawnEconomy(null); var host = HostFunded(100);
            SetField(component, "settlement", host); component.ApplyInstitutionModeSetting(true);
            var map = (Map)FormatterServices.GetUninitializedObject(typeof(Map)); map.uniqueID = 770;
            var factionShim = new Harmony("personalchronicle.tests.publicbudgets");
            publicBudgetFaction = (Faction)FormatterServices.GetUninitializedObject(typeof(Faction));
            factionShim.Patch(AccessTools.PropertyGetter(typeof(Faction), nameof(Faction.OfPlayer)),
                prefix: new HarmonyMethod(typeof(PawnSilverReserveTests), nameof(BudgetFaction)));
            offlineMaps.Add(map);
            try
            {
                var bank = BudgetInstitution(770, InstitutionKind.SettlementOffice);
                var labor = BudgetInstitution(771, InstitutionKind.LaborOffice);
                var shop = BudgetInstitution(772, InstitutionKind.Shop);
                var otherLabor = BudgetInstitution(773, InstitutionKind.LaborOffice);
                Field<List<Building_EconomyInstitution>>(component, "anchors").AddRange(new[] { bank, labor, shop, otherLabor });
                foreach (var pair in new[] { Tuple.Create(bank, 10), Tuple.Create(labor, 20), Tuple.Create(shop, 30), Tuple.Create(otherLabor, 10) })
                {
                    string stamp = component.QueryTreasuryStamp();
                    Assert.That(component.ExecuteTreasuryCommand("Treasury.AllocateBudget", pair.Item2, pair.Item1.GetUniqueLoadID(), null, stamp), Is.True);
                    Assert.That(component.ExecuteTreasuryCommand("Treasury.AllocateBudget", pair.Item2, pair.Item1.GetUniqueLoadID(), null, stamp), Is.False, "old UI command cannot allocate twice");
                }
                Assert.That(component.QueryPublicFundsTotal(), Is.EqualTo(10000));
                Assert.That(component.QueryPayrollFundingMinor(map, labor.GetUniqueLoadID()), Is.EqualTo(6000), "other labor and shop budgets stay independent");
                var pawn = new Pawn { def = MinimalDef("BudgetPawn"), thingIDNumber = 774 };
                AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)0);
                var data = Field<EmploymentSaveData>(component, "employment");
                data.WriteContract(new WorkContract("contract", pawn.GetUniqueLoadID(), labor.GetUniqueLoadID(), "work", new[] { "Mining" },
                    500, 4, 0, 5000, ContractState.Active, 0));
                data.WriteObligation(new WageObligation("due", "contract", "evidence", pawn.GetUniqueLoadID(), 500, 0, 0, ObligationState.Due, null));
                Assert.That(component.TrySettlePayable(pawn, "due", 500, 10), Is.EqualTo(EmploymentCode.Applied));
                Assert.That(component.QueryInstitutionFunds(labor), Is.EqualTo(1500), "issuer budget pays first");
                Assert.That(component.QueryInstitutionFunds(shop), Is.EqualTo(3000));
                Assert.That(component.QueryInstitutionFunds(otherLabor), Is.EqualTo(1000));
                Assert.That(host.ReadAccount(SettlementLedgerNames.Treasury).BalanceMinor, Is.EqualTo(3000));
                Assert.That(component.TrySettlePayable(pawn, "due", 500, 10), Is.EqualTo(EmploymentCode.AlreadyPaid));
                Assert.That(component.QueryPublicFundsTotal(), Is.EqualTo(9500));
                string shopKey = component.InstitutionFundingKey(shop);
                Field<List<Building_EconomyInstitution>>(component, "anchors").Remove(shop);
                Assert.That(component.ExecuteTreasuryCommand("Treasury.ReturnBudget", 0, shopKey, null, component.QueryTreasuryStamp()), Is.True,
                    "demolition cannot strand colony-owned money");
                Assert.That(host.ReadAccount(SettlementLedgerNames.Treasury).BalanceMinor, Is.EqualTo(6000));
                Assert.That(component.QueryPublicFundsTotal(), Is.EqualTo(9500));
                Assert.That(host.ReadAudit().ActualReserveMinor, Is.EqualTo(10000));
            }
            finally { offlineMaps.Remove(map); factionShim.UnpatchAll(factionShim.Id); publicBudgetFaction = null; }
        }
        private Building_EconomyInstitution BudgetInstitution(int id, InstitutionKind kind)
        {
            var def = MinimalDef("Institution" + id); def.category = ThingCategory.Building;
            def.modExtensions = new List<DefModExtension> { new EconomyInstitutionExtension { kind = kind } };
            var building = new Building_EconomyInstitution { def = def, thingIDNumber = id };
            AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(building, (sbyte)0);
            AccessTools.Field(typeof(Thing), "factionInt").SetValue(building, publicBudgetFaction);
            return building;
        }
        [Test] public void RealReserveAndScribeBackPublicBudgetsWagesAndShopIncomeExactlyOnce()
        {
            var host = HostFunded(100); string bank = SettlementLedgerNames.Institution("bank");
            var transfer = SettlementIntent.ForPublicTransfer("budget", host.Version, SettlementLedgerNames.Treasury, bank, 4000);
            Assert.That(host.TransferPublic(transfer).Code, Is.EqualTo(SettlementCode.Finalized));
            var wage = SettlementIntent.ForWage("wage", host.Version, "obligation", "pawn", 1000, bank);
            Assert.That(host.PayWage(wage).Code, Is.EqualTo(SettlementCode.Finalized));
            string shop = SettlementLedgerNames.Institution("shop");
            var sale = SettlementIntent.ForPurchase("sale", host.Version, "goods", "pawn", 200, shop);
            Assert.That(host.PreparePurchase(sale).Code, Is.EqualTo(SettlementCode.Prepared));
            Assert.That(host.CompletePurchase("sale").Code, Is.EqualTo(SettlementCode.Finalized));
            var loaded = RoundTripHost(host); var audit = loaded.ReadAudit();
            Assert.That(audit.Fault, Is.Null);
            Assert.That(audit.ActualReserveMinor, Is.EqualTo(10000));
            Assert.That(audit.TotalLiabilitiesMinor, Is.EqualTo(10000));
            Assert.That(SettlementLedgerNames.PublicTotal(audit.Accounts), Is.EqualTo(9200));
            Assert.That(loaded.ReadAccount(bank).BalanceMinor, Is.EqualTo(3000));
            Assert.That(loaded.ReadAccount(shop).BalanceMinor, Is.EqualTo(200));
            Assert.That(loaded.ReadAccount("pawn").BalanceMinor, Is.EqualTo(800));
            Assert.That(loaded.TransferPublic(transfer).Code, Is.EqualTo(SettlementCode.Replay));
            Assert.That(loaded.PayWage(wage).Code, Is.EqualTo(SettlementCode.Replay));
            Assert.That(loaded.CompletePurchase("sale").Code, Is.EqualTo(SettlementCode.Replay));
            Assert.That(loaded.ReadAudit().TotalLiabilitiesMinor, Is.EqualTo(10000));
        }
        [Test] public void SavedPurchaseWaitsForGoodsProofInsteadOfBeingCancelledAndGivenAway()
        {
            var host = HostFunded(); host.PayWage(SettlementIntent.ForWage("wage", host.Version, "obligation", "pawn", 1000));
            var sale = SettlementIntent.ForPurchase("pending", host.Version, "goods", "pawn", 400, SettlementLedgerNames.Institution("shop"));
            host.PreparePurchase(sale);
            var loaded = RoundTripHost(host);
            Assert.That(loaded.ReadAudit().Fault, Is.Null);
            Assert.That(loaded.ReadCommand("pending").Phase, Is.EqualTo(SettlementPhase.Prepared));
            Assert.That(loaded.ReadAccount("pawn").ReservedMinor, Is.EqualTo(400));
            Assert.That(loaded.CompletePurchase("pending").Code, Is.EqualTo(SettlementCode.Finalized));
            Assert.That(loaded.ReadAccount("pawn").BalanceMinor, Is.EqualTo(600));
            Assert.That(loaded.ReadAudit().ActualReserveMinor, Is.EqualTo(10000));
        }
        [Test] public void HaulVersionRefreshPreservesPhysicalIdentityAndSurvivesScribe()
        {
            var operations = new TreasuryOperationsSaveData();
            var original = Delivery().With(TreasuryDeliveryPhase.Carrying, "carried-unit");
            operations.Add(original);
            Assert.That(operations.RebaseUnpreparedDeposit(1, 9, 3), Is.True);
            var restored = RoundTripOperations(operations).Find(1);
            Assert.That(restored.SourceKey, Is.EqualTo(original.SourceKey));
            Assert.That(restored.CarryKey, Is.EqualTo(original.CarryKey));
            Assert.That(restored.PawnKey, Is.EqualTo(original.PawnKey));
            Assert.That(restored.Count, Is.EqualTo(original.Count));
            Assert.That(restored.Intent().ExpectedVersion, Is.EqualTo(9));
            Assert.That(restored.Intent().Movement.ExpectedVersion, Is.EqualTo(3));
            Assert.That(operations.RebaseUnpreparedDeposit(1, 8, 3), Is.False);
            operations.Update(operations.Find(1).With(TreasuryDeliveryPhase.Completed));
            Assert.That(operations.RebaseUnpreparedDeposit(1, 10, 3), Is.False);
        }
    }
}
