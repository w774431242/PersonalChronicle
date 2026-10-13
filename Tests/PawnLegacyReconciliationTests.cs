using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Data;
using PersonalChronicle.Domain.PawnEconomy;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class PawnLegacyReconciliationTests
    {
        private static EconomyState Fixture()
        {
            var state = new EconomyState { Enabled = true, Funded = true };
            state.Add(new EconomyAccount { Key = "public", Balance = 100000, Reserved = 200 });
            state.Add(new EconomyPerson { PawnKey = "pawn", AcquiredAsset = "衣服:1", Reason = "paid-unused" });
            state.Add(new EconomyInstitution { AnchorKey = "institution", DoctorKey = "doctor", MapId = 4, ZoneId = 3 });
            state.Add(new EconomyPosition { InstitutionId = 3, Wage = 123, MapId = 4 });
            state.Add(new EconomyContract { PositionId = 4, PawnKey = "pawn", LastProductionJobId = 482 });
            state.Add(new EconomyOrder { OrderKind = EconomyOrderKind.Purchase, PayerKey = "public", ClientKey = "pawn",
                SellerKey = "institution:institution", Amount = 200, FundsHeld = true, Status = EconomyOrderStatus.Prepared,
                AssetKey = "衣服:2", InstitutionId = 3, PositionId = 4, MapId = 4, ExpiresTick = 500 });
            state.Add(new EconomyReceipt { Key = "opening", Type = "OpeningAllocation", ToKey = "public", Amount = 100000, Paid = true });
            state.Add(new EconomyReceipt { Key = "wage:482", Type = "Wage", FromKey = "public", ToKey = "pawn", Amount = 123, Paid = false });
            state.Add(new EconomyReport { PawnKey = "pawn", DoctorKey = "doctor", OrderId = 6, Moving = .8234567f,
                Manipulation = .25f, Consciousness = 1f, VisibleConditions = new List<string> { "旧伤", null } });
            state.Add(new EconomyOwnership { AssetKey = "衣服:1", OwnerKey = "pawn" });
            state.Add(new EconomyOrder { OrderKind = EconomyOrderKind.Examination, ClientKey = "pawn", PayerKey = "public",
                SellerKey = "institution:institution", ProviderKey = "doctor", Status = EconomyOrderStatus.Committed,
                ProviderPaid = false, Amount = 500, InstitutionId = 3 });
            return state;
        }
        private static string Scalar(object value) => value is float number ? number.ToString("R", CultureInfo.InvariantCulture)
            : value is IFormattable formatted ? formatted.ToString(null, CultureInfo.InvariantCulture) : value?.ToString();

        [Test]
        public void EverySerializedFieldOfAllNineRecordKindsIsRetainedWithIdentity()
        {
            var state = Fixture(); var report = LegacyReconciler.Capture(state);
            Assert.That(report.DiagnosticChecksPassed, Is.True); Assert.That(report.Records.Count, Is.EqualTo(state.Records.Count));
            Assert.That(report.Records.Select(r => r.Kind).Distinct().Count(), Is.EqualTo(9));
            foreach (var record in state.Records)
            {
                var row = report.Records.Single(r => r.Id == record.Id); Assert.That(row.Kind, Is.EqualTo(record.GetType().Name));
                var fields = record.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => f.Name != "Id").ToArray();
                Assert.That(row.Fields.Count + (record is EconomyReport ? 1 : 0), Is.EqualTo(fields.Length));
                foreach (var field in fields)
                {
                    if (field.Name == "VisibleConditions") Assert.That(row.VisibleConditions, Is.EqualTo(field.GetValue(record)));
                    else Assert.That(row.Fields[field.Name], Is.EqualTo(Scalar(field.GetValue(record))), record.GetType().Name + "." + field.Name);
                }
            }
            Assert.That(report.Schema, Is.EqualTo(state.Schema)); Assert.That(report.Revision, Is.EqualTo(state.Revision));
            Assert.That(report.Sequence, Is.EqualTo(state.Sequence)); Assert.That(report.Enabled, Is.True); Assert.That(report.Funded, Is.True);
        }
        [Test]
        public void ReconciliationDoesNotMutateOrRepairTheLegacyPayload()
        {
            var state = Fixture(); string before = PawnEconomySaveData.Encode(state);
            var report = LegacyReconciler.Capture(state); LegacyReconciler.Capture(state);
            Assert.That(PawnEconomySaveData.Encode(state), Is.EqualTo(before));
            Assert.That(report.RecordedBalancesMinor, Is.EqualTo(100000)); Assert.That(report.RecordedReservationsMinor, Is.EqualTo(200));
            Assert.That(report.HeldOrderAmountsMinor, Is.EqualTo(200)); Assert.That(report.UnpaidWagesMinor, Is.EqualTo(123));
            Assert.That(report.OpeningAllocationsMinor, Is.EqualTo(100000)); Assert.That(report.CommittedOrderAmountsMinor, Is.EqualTo(500));
            Assert.That(report.BackingVerified, Is.False); Assert.That(report.CanActivateNewLedger, Is.False);
        }
        [Test]
        public void SnapshotsAndNestedCollectionsDoNotFollowLegacyMutations()
        {
            var state = Fixture(); var report = LegacyReconciler.Capture(state);
            state.All<EconomyAccount>().Single().Balance = 1; state.All<EconomyReport>().Single().VisibleConditions.Clear();
            state.All<EconomyPerson>().Single().AcquiredAsset = null; state.Revision++; state.Records.Clear();
            Assert.That(report.RecordedBalancesMinor, Is.EqualTo(100000)); Assert.That(report.SourceCount, Is.EqualTo(11));
            Assert.That(report.Records.Single(r => r.Kind == "EconomyReport").VisibleConditions.Count, Is.EqualTo(2));
            var person = report.Records.Single(r => r.Kind == "EconomyPerson"); Assert.That(person.Fields["AcquiredAsset"], Is.EqualTo("衣服:1"));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)person.Fields).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<LegacyRecordSnapshot>)report.Records).Clear());
        }
        [TestCase(0)] [TestCase(2)] [TestCase(int.MaxValue)]
        public void UnsupportedSchemaPreservesKnownRowsButCannotAuthorizeMigration(int schema)
        {
            var state = Fixture(); state.Schema = schema; state.Fault = "UnsupportedSchema";
            var report = LegacyReconciler.Capture(state);
            Assert.That(report.Issues.Select(i => i.Code), Does.Contain("UnsupportedSchema"));
            Assert.That(report.Fault, Is.EqualTo("UnsupportedSchema")); Assert.That(report.Records.Count, Is.EqualTo(11));
            Assert.That(report.CanActivateNewLedger, Is.False);
        }
        [Test]
        public void EmptyLedgerDoesNotCreateAccountsOrOpeningMoney()
        {
            var state = new EconomyState(); var report = LegacyReconciler.Capture(state);
            Assert.That(report.DiagnosticChecksPassed, Is.True); Assert.That(report.RecordedBalancesMinor, Is.Zero);
            Assert.That(state.Records, Is.Empty); Assert.That(state.Funded, Is.False); Assert.That(report.BackingVerified, Is.False);
        }
        [Test]
        public void InvalidAmountsRemainVisibleAndDoNotOverflowAggregateMath()
        {
            var state = new EconomyState();
            state.Add(new EconomyAccount { Key = "a", Balance = long.MaxValue, Reserved = -1 });
            state.Add(new EconomyAccount { Key = "b", Balance = long.MaxValue });
            state.Add(new EconomyReceipt { Type = "Wage", Amount = -20 });
            var report = LegacyReconciler.Capture(state);
            Assert.That(report.RecordedBalancesMinor, Is.EqualTo(2m * long.MaxValue)); Assert.That(report.UnpaidWagesMinor, Is.EqualTo(-20));
            Assert.That(report.Issues.Select(i => i.Code), Does.Contain("InvalidAccount").And.Contain("InvalidReceiptAmount"));
            Assert.That(report.Records[0].Fields["Reserved"], Is.EqualTo("-1"));
        }
        [Test]
        public void DuplicateIdsAccountKeysAndSequenceDefectsAreReportedWithoutDiscardingRows()
        {
            var state = new EconomyState { Sequence = 0 };
            state.Records.Add(new EconomyAccount { Id = 3, Key = "same", Balance = 10 });
            state.Records.Add(new EconomyAccount { Id = 3, Key = "same", Balance = 20 });
            state.Records.Add(null);
            var report = LegacyReconciler.Capture(state);
            Assert.That(report.Issues.Select(i => i.Code), Does.Contain("InvalidOrDuplicateId").And.Contain("DuplicateAccountKey")
                .And.Contain("SequenceBehindRecord").And.Contain("NullRecord"));
            Assert.That(report.Records.Count, Is.EqualTo(3)); Assert.That(report.RecordedBalancesMinor, Is.EqualTo(30));
        }
        [Test]
        public void ReservationMismatchMissingPayerAndTerminalHoldAreExplicit()
        {
            var state = Fixture(); state.All<EconomyAccount>().Single().Reserved = 300;
            state.Add(new EconomyOrder { Status = EconomyOrderStatus.Committed, FundsHeld = true, Amount = 9, PayerKey = "missing" });
            state.Add(new EconomyOrder { FundsHeld = true, Amount = 1 });
            var report = LegacyReconciler.Capture(state);
            Assert.That(report.Issues.Select(i => i.Code), Does.Contain("ReservationMismatch").And.Contain("HeldPayerNotFound")
                .And.Contain("TerminalOrderStillHeld").And.Contain("MissingHeldPayer"));
            Assert.That(report.RecordedReservationsMinor, Is.EqualTo(300)); Assert.That(report.HeldOrderAmountsMinor, Is.EqualTo(210));
        }
        [Test]
        public void InvalidOrderPhaseAndAmountAreRetainedForDiagnosis()
        {
            var state = new EconomyState(); state.Add(new EconomyOrder { Amount = -1, Status = (EconomyOrderStatus)999 });
            var report = LegacyReconciler.Capture(state);
            Assert.That(report.Issues.Select(i => i.Code), Does.Contain("InvalidOrderTypeOrPhase").And.Contain("InvalidOrderAmount"));
            Assert.That(report.Records.Single().Fields["Status"], Is.EqualTo("999"));
        }
        [Test]
        public void NullAndEmptyHealthListsRemainDifferentAndFloatTextIsCultureIndependent()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var state = new EconomyState(); state.Add(new EconomyReport { Moving = .12345679f, VisibleConditions = null });
                state.Add(new EconomyReport { VisibleConditions = new List<string>() });
                var report = LegacyReconciler.Capture(state);
                Assert.That(report.Records[0].VisibleConditions, Is.Null); Assert.That(report.Records[1].VisibleConditions, Is.Empty);
                Assert.That(float.Parse(report.Records[0].Fields["Moving"], CultureInfo.InvariantCulture), Is.EqualTo(.12345679f));
            }
            finally { CultureInfo.CurrentCulture = originalCulture; }
        }
        [Test]
        public void CaptureAfterOldRecoveryExplicitlyObservesCurrentCancelledState()
        {
            var state = Fixture(); var before = LegacyReconciler.Capture(state);
            new EconomyService(state).RecoverAfterLoad(); var after = LegacyReconciler.Capture(state);
            Assert.That(before.Records.Single(r => r.Id == 6).Fields["Status"], Is.EqualTo("Prepared"));
            Assert.That(after.Records.Single(r => r.Id == 6).Fields["Status"], Is.EqualTo("Cancelled"));
            Assert.That(after.RecordedReservationsMinor, Is.Zero); Assert.That(before.RecordedReservationsMinor, Is.EqualTo(200));
        }
        [Test]
        public void OversizedLedgerIsExplicitlyTruncatedAndNeverMigrationReady()
        {
            var state = new EconomyState { Sequence = LegacyReconciler.MaximumRecords + 1 };
            for (int i = 1; i <= LegacyReconciler.MaximumRecords + 1; i++) state.Records.Add(new EconomyOwnership { Id = i, OwnerKey = "p", AssetKey = "a:" + i });
            var report = LegacyReconciler.Capture(state);
            Assert.That(report.Truncated, Is.True); Assert.That(report.Records.Count, Is.EqualTo(LegacyReconciler.MaximumRecords));
            Assert.That(report.Issues.Select(i => i.Code), Does.Contain("RecordLimit")); Assert.That(report.CanActivateNewLedger, Is.False);
        }
    }
}
