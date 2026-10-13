using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using PersonalChronicle.Domain.PawnEconomy;

namespace PersonalChronicle.Application.PawnEconomy.Finance
{
    public sealed class LegacyRecordSnapshot
    {
        public long? Id { get; }
        public string Kind { get; }
        // Invariant round-trip scalar text; null is preserved, never converted to empty/zero.
        public IReadOnlyDictionary<string, string> Fields { get; }
        public IReadOnlyList<string> VisibleConditions { get; }
        internal LegacyRecordSnapshot(EconomyRecord record, Dictionary<string, string> fields, IList<string> conditions)
        {
            Id = record?.Id; Kind = record?.GetType().Name;
            Fields = new ReadOnlyDictionary<string, string>(fields);
            VisibleConditions = conditions == null ? null : Array.AsReadOnly(conditions.ToArray());
        }
    }
    public sealed class LegacyReconciliationIssue
    {
        public string Code { get; }
        public long? RecordId { get; }
        public string Detail { get; }
        internal LegacyReconciliationIssue(string code, long? recordId = null, string detail = null)
        { Code = code; RecordId = recordId; Detail = detail; }
    }
    public sealed class LegacyReconciliationReport
    {
        public int Schema { get; }
        public int Revision { get; }
        public long Sequence { get; }
        public bool Enabled { get; }
        public bool Funded { get; }
        public string Fault { get; }
        public int SourceCount { get; }
        public bool Truncated => Records.Count != SourceCount;
        // These are bounded financial/identity diagnostics, not complete legacy business validation.
        public bool DiagnosticChecksPassed => Issues.Count == 0;
        public bool BackingVerified => false;
        public bool CanActivateNewLedger => false;
        public IReadOnlyList<LegacyRecordSnapshot> Records { get; }
        public IReadOnlyList<LegacyReconciliationIssue> Issues { get; }
        // Diagnostic raw sums, including malformed values. Inspect Issues before interpreting them.
        public decimal RecordedBalancesMinor { get; }
        public decimal RecordedReservationsMinor { get; }
        public decimal HeldOrderAmountsMinor { get; }
        public decimal UnpaidWagesMinor { get; }
        public decimal OpeningAllocationsMinor { get; }
        public decimal CommittedOrderAmountsMinor { get; }
        internal LegacyReconciliationReport(EconomyState state, IList<LegacyRecordSnapshot> records,
            IList<LegacyReconciliationIssue> issues)
        {
            Schema = state.Schema; Revision = state.Revision; Sequence = state.Sequence;
            Enabled = state.Enabled; Funded = state.Funded; Fault = state.Fault; SourceCount = state.Records.Count;
            Records = Array.AsReadOnly(records.ToArray()); Issues = Array.AsReadOnly(issues.ToArray());
            // Only rows captured in this report contribute; a truncated report is never migration-ready.
            RecordedBalancesMinor = Sum("EconomyAccount", "Balance");
            RecordedReservationsMinor = Sum("EconomyAccount", "Reserved");
            HeldOrderAmountsMinor = Sum("EconomyOrder", "Amount", row => row.Fields["FundsHeld"] == "True");
            UnpaidWagesMinor = Sum("EconomyReceipt", "Amount", row => row.Fields["Type"] == "Wage" && row.Fields["Paid"] == "False");
            OpeningAllocationsMinor = Sum("EconomyReceipt", "Amount", row => row.Fields["Type"] == "OpeningAllocation");
            CommittedOrderAmountsMinor = Sum("EconomyOrder", "Amount", row => row.Fields["Status"] == "Committed");
        }
        private decimal Sum(string kind, string field, Func<LegacyRecordSnapshot, bool> filter = null)
            => Records.Where(r => r.Kind == kind && (filter == null || filter(r)))
                .Sum(r => decimal.Parse(r.Fields[field], CultureInfo.InvariantCulture));
    }
    /// <summary>Observation of the current legacy revision, NOT a pre-load backup or migration authorization.</summary>
    public static class LegacyReconciler
    {
        public const int MaximumRecords = 100000;
        public static LegacyReconciliationReport Capture(EconomyState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var issues = new List<LegacyReconciliationIssue>(); var rows = new List<LegacyRecordSnapshot>();
            void Issue(string code, EconomyRecord record = null, string detail = null)
                => issues.Add(new LegacyReconciliationIssue(code, record?.Id, detail));
            if (state.Schema != EconomyState.CurrentSchema) Issue("UnsupportedSchema");
            if (!string.IsNullOrEmpty(state.Fault)) Issue("LegacyFault", detail: state.Fault);
            if (state.Records.Count > MaximumRecords) Issue("RecordLimit");
            var records = state.Records.Take(MaximumRecords).ToArray();
            var ids = new HashSet<long>(); var accounts = new Dictionary<string, EconomyAccount>(StringComparer.Ordinal);
            var held = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                rows.Add(Copy(record));
                if (record == null) { Issue("NullRecord"); continue; }
                if (record.Id <= 0 || !ids.Add(record.Id)) Issue("InvalidOrDuplicateId", record);
                if (record.Id > state.Sequence) Issue("SequenceBehindRecord", record);
                switch (record)
                {
                    case EconomyAccount a:
                        if (!EconomyRules.Valid(a)) Issue("InvalidAccount", a);
                        if (string.IsNullOrWhiteSpace(a.Key)) Issue("MissingAccountKey", a);
                        else if (accounts.ContainsKey(a.Key)) Issue("DuplicateAccountKey", a, a.Key);
                        else accounts.Add(a.Key, a);
                        break;
                    case EconomyOrder o:
                        if (!Enum.IsDefined(typeof(EconomyOrderStatus), o.Status)
                            || !Enum.IsDefined(typeof(EconomyOrderKind), o.OrderKind)) Issue("InvalidOrderTypeOrPhase", o);
                        if (o.Amount < 0 || o.Amount > EconomyRules.MaxAmount) Issue("InvalidOrderAmount", o);
                        if (string.IsNullOrWhiteSpace(o.ClientKey) || string.IsNullOrWhiteSpace(o.PayerKey)
                            || string.IsNullOrWhiteSpace(o.SellerKey)) Issue("MissingOrderParty", o);
                        if (o.FundsHeld)
                        {
                            if (!o.Pending) Issue("TerminalOrderStillHeld", o);
                            if (o.Amount == 0 || o.PayerKey == o.SellerKey) Issue("InvalidHeldAmountOrSelfPayment", o);
                            if (string.IsNullOrWhiteSpace(o.PayerKey)) Issue("MissingHeldPayer", o);
                            else { held.TryGetValue(o.PayerKey, out var amount); held[o.PayerKey] = amount + o.Amount; }
                        }
                        break;
                    case EconomyReceipt r:
                        if (r.Amount < 0 || r.Amount > EconomyRules.MaxAmount) Issue("InvalidReceiptAmount", r);
                        break;
                    case EconomyPerson _: case EconomyInstitution _: case EconomyPosition _:
                    case EconomyContract _: case EconomyReport _: case EconomyOwnership _: break;
                    default: Issue("UnsupportedRecordType", record); break;
                }
            }
            foreach (var account in accounts.Values)
            {
                held.TryGetValue(account.Key, out var amount);
                if (amount != account.Reserved) Issue("ReservationMismatch", account, account.Key);
            }
            foreach (var key in held.Keys)
                if (!accounts.ContainsKey(key)) Issue("HeldPayerNotFound", detail: key);
            return new LegacyReconciliationReport(state, rows, issues);
        }
        private static LegacyRecordSnapshot Copy(EconomyRecord record)
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal); IList<string> conditions = null;
            void Field(string key, object value) => fields.Add(key, value is float f ? f.ToString("R", CultureInfo.InvariantCulture)
                : value is IFormattable formatted ? formatted.ToString(null, CultureInfo.InvariantCulture) : value?.ToString());
            switch (record)
            {
                case EconomyAccount a:
                    Field(nameof(a.Key), a.Key); Field(nameof(a.Balance), a.Balance); Field(nameof(a.Reserved), a.Reserved);
                    Field(nameof(a.Income), a.Income); Field(nameof(a.Expense), a.Expense); break;
                case EconomyPerson p:
                    Field(nameof(p.PawnKey), p.PawnKey); Field(nameof(p.Autonomy), p.Autonomy); Field(nameof(p.Goal), p.Goal);
                    Field(nameof(p.NextDecisionTick), p.NextDecisionTick); Field(nameof(p.AcquiredAsset), p.AcquiredAsset); Field(nameof(p.Reason), p.Reason); break;
                case EconomyInstitution i:
                    Field(nameof(i.AnchorKey), i.AnchorKey); Field(nameof(i.DoctorKey), i.DoctorKey); Field(nameof(i.MapId), i.MapId);
                    Field(nameof(i.ZoneId), i.ZoneId); Field(nameof(i.InstitutionKind), i.InstitutionKind); Field(nameof(i.Open), i.Open);
                    Field(nameof(i.RequiresExam), i.RequiresExam); Field(nameof(i.StartHour), i.StartHour); Field(nameof(i.EndHour), i.EndHour);
                    Field(nameof(i.Capacity), i.Capacity); Field(nameof(i.Price), i.Price); Field(nameof(i.Wage), i.Wage); Field(nameof(i.Revision), i.Revision); break;
                case EconomyPosition p:
                    Field(nameof(p.InstitutionId), p.InstitutionId); Field(nameof(p.MapId), p.MapId); Field(nameof(p.ZoneId), p.ZoneId);
                    Field(nameof(p.StartHour), p.StartHour); Field(nameof(p.EndHour), p.EndHour); Field(nameof(p.Capacity), p.Capacity);
                    Field(nameof(p.Wage), p.Wage); Field(nameof(p.Open), p.Open); Field(nameof(p.RequiresExam), p.RequiresExam); Field(nameof(p.Revision), p.Revision); break;
                case EconomyContract c:
                    Field(nameof(c.PositionId), c.PositionId); Field(nameof(c.PawnKey), c.PawnKey); Field(nameof(c.Active), c.Active);
                    Field(nameof(c.LastProductionJobId), c.LastProductionJobId); break;
                case EconomyOrder o:
                    Field(nameof(o.OrderKind), o.OrderKind); Field(nameof(o.Status), o.Status); Field(nameof(o.ClientKey), o.ClientKey);
                    Field(nameof(o.ProviderKey), o.ProviderKey); Field(nameof(o.AssetKey), o.AssetKey); Field(nameof(o.PayerKey), o.PayerKey);
                    Field(nameof(o.SellerKey), o.SellerKey); Field(nameof(o.Reason), o.Reason); Field(nameof(o.InstitutionId), o.InstitutionId);
                    Field(nameof(o.PositionId), o.PositionId); Field(nameof(o.Amount), o.Amount); Field(nameof(o.MapId), o.MapId);
                    Field(nameof(o.ExpiresTick), o.ExpiresTick); Field(nameof(o.Progress), o.Progress); Field(nameof(o.LastProgressTick), o.LastProgressTick);
                    Field(nameof(o.ClientPulse), o.ClientPulse); Field(nameof(o.ProviderPulse), o.ProviderPulse);
                    Field(nameof(o.FundsHeld), o.FundsHeld); Field(nameof(o.ProviderPaid), o.ProviderPaid); break;
                case EconomyReceipt r:
                    Field(nameof(r.Key), r.Key); Field(nameof(r.Type), r.Type); Field(nameof(r.SubjectKey), r.SubjectKey);
                    Field(nameof(r.FromKey), r.FromKey); Field(nameof(r.ToKey), r.ToKey); Field(nameof(r.Amount), r.Amount);
                    Field(nameof(r.Tick), r.Tick); Field(nameof(r.Paid), r.Paid); break;
                case EconomyReport r:
                    Field(nameof(r.PawnKey), r.PawnKey); Field(nameof(r.DoctorKey), r.DoctorKey); Field(nameof(r.OrderId), r.OrderId);
                    Field(nameof(r.Tick), r.Tick); Field(nameof(r.ExpiresTick), r.ExpiresTick); Field(nameof(r.Moving), r.Moving);
                    Field(nameof(r.Manipulation), r.Manipulation); Field(nameof(r.Consciousness), r.Consciousness);
                    conditions = r.VisibleConditions; break;
                case EconomyOwnership o: Field(nameof(o.AssetKey), o.AssetKey); Field(nameof(o.OwnerKey), o.OwnerKey); break;
            }
            return new LegacyRecordSnapshot(record, fields, conditions);
        }
    }
}
