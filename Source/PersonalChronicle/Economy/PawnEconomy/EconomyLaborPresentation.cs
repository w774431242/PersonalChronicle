using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    internal enum LaborWageGroup { PendingSubmission, PendingPayment, Arrears, Paid }

    internal static class LaborContractProjection
    {
        internal const int ColumnCount = 12, ActionColumn = 11;
        internal static EconomyRow Expand(EconomyRow row, string stage, long stageSort, long delivered,
            long remaining, string station, string facts, string history)
        {
            if (row.Cells == null || row.Cells.Length != VanillaContractRow.ColumnCount)
                throw new ArgumentException("LaborContractBaseColumnMismatch", nameof(row));
            var cells = new string[ColumnCount];
            var sorts = new long[ColumnCount];
            Array.Copy(row.Cells, cells, VanillaContractRow.ActionColumnIndex);
            if (row.CellSorts != null) Array.Copy(row.CellSorts, sorts, VanillaContractRow.ActionColumnIndex);
            cells[7] = stage; cells[8] = delivered.ToString(CultureInfo.InvariantCulture);
            cells[9] = remaining.ToString(CultureInfo.InvariantCulture); cells[10] = station;
            cells[ActionColumn] = row.Cells[VanillaContractRow.ActionColumnIndex];
            sorts[7] = stageSort; sorts[8] = delivered; sorts[9] = remaining;
            return new EconomyRow(row.Text + "\n" + facts, row.Action, row.Verb, row.Id, row.Key, row.Progress,
                cells, sorts, row.Tag, row.Search, row.Detail + "\n" + facts + "\n" + history, row.Compose, row.Revoke);
        }
    }

    /// <summary>Pure projection of authoritative rights and physical batches. Never changes eligibility or funds.</summary>
    internal sealed class LaborUiTotals
    {
        public long Delivered { get; }
        public long Remaining { get; }
        public long DueMinor { get; }
        public long PaidMinor { get; }
        public LaborUiTotals(string contractId, IEnumerable<ProductBatch> batches, IEnumerable<WageObligation> wages)
        {
            foreach (var batch in batches ?? Array.Empty<ProductBatch>())
            {
                if (batch == null || batch.ContractId != contractId) continue;
                Delivered += batch.AcceptedQuantity; Remaining += batch.RemainingQuantity;
            }
            foreach (var wage in wages ?? Array.Empty<WageObligation>())
            {
                if (wage == null || wage.ContractId != contractId) continue;
                DueMinor += wage.AmountMinor - wage.PaidMinor; PaidMinor += wage.PaidMinor;
            }
        }
        public static LaborWageGroup Group(WageObligation wage)
        {
            if (wage.State == ObligationState.Paid && wage.PaidMinor == wage.AmountMinor
                && !string.IsNullOrEmpty(wage.TxId)) return LaborWageGroup.Paid;
            if (wage.Eligibility == WageEligibility.PendingSubmission || wage.Eligibility == WageEligibility.Blocked
                || wage.Eligibility == WageEligibility.Unknown) return LaborWageGroup.PendingSubmission;
            return wage.State == ObligationState.Shortfall ? LaborWageGroup.Arrears : LaborWageGroup.PendingPayment;
        }
    }

    internal static class LaborPresentationRows
    {
        internal static string Text(string key, params object[] arguments)
        {
            string value = EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual.UI." + key, key);
            return arguments.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, arguments);
        }
        private static string Money(long amount) => (amount / 100m).ToString("0.00", CultureInfo.CurrentCulture);
        public static List<NeedTextRow> Compact(LaborWorkflow flow, IReadOnlyList<ProductBatch> batches,
            IReadOnlyList<WageObligation> wages, string station)
        {
            var rows = new List<NeedTextRow>(4);
            if (flow == null) return rows;
            var totals = new LaborUiTotals(flow.ContractId, batches, wages);
            string stage = LaborVisualText.StageLabel(flow.Stage);
            if (!string.IsNullOrEmpty(flow.ContractId)) stage += " · " + LaborFlowRules.ShortCode(flow.ContractId)
                + " · " + LaborVisualText.ModeLabel(flow.Mode);
            if (flow.Hold != FlowHold.None) stage += " · " + LaborVisualText.HoldLabel(flow.Hold)
                + " · " + LaborVisualText.ReasonLabel(flow.ReasonCode);
            rows.Add(new NeedTextRow(Text("Stage"), stage));
            rows.Add(new NeedTextRow(Text("Delivered"), Text("DeliveryValue", totals.Delivered, totals.Remaining)
                + " · " + Text("WageValue", Money(totals.DueMinor), Money(totals.PaidMinor))));
            rows.Add(new NeedTextRow(Text("Station"), (string.IsNullOrEmpty(station) ? Text("NoStation") : station)
                + " · " + LaborVisualText.NextActionLabel(flow.NextAction)));
            return rows;
        }
        public static List<NeedTextRow> History(IReadOnlyList<LaborFlowEvent> events)
        {
            var rows = new List<NeedTextRow>();
            rows.Add(new NeedTextRow(Text("History"), events == null || events.Count == 0 ? Text("HistoryEmpty") : string.Empty));
            if (events == null) return rows;
            // Saved chain is oldest-first. Display most recent first and retain stable IDs in the detail value.
            for (int index = events.Count - 1; index >= Math.Max(0, events.Count - 64); index--)
            {
                var e = events[index];
                if (e == null) continue;
                string action = EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual.History.Action." + e.ActionCode, e.ActionCode);
                string result = string.IsNullOrEmpty(e.ResultCode) ? Text("Result.None")
                    : EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual.History.Result." + e.ResultCode, e.ResultCode);
                string product = string.IsNullOrEmpty(e.ProductDefName) ? Text(e.Quantity > 0 ? "ProductSeeEvidence" : "NoProduct")
                    : DefDatabase<ThingDef>.GetNamedSilentFail(e.ProductDefName)?.LabelCap.ToString() ?? e.ProductDefName;
                string value = Text("HistoryValue", LaborVisualText.StageLabel(e.Stage), action, product,
                    e.Quantity, Money(e.AmountMinor), result);
                if (e.Hold != FlowHold.None) value += " · " + LaborVisualText.HoldLabel(e.Hold);
                if (!string.IsNullOrEmpty(e.ReasonCode)) value += " · " + LaborVisualText.ReasonLabel(e.ReasonCode);
                if (!string.IsNullOrEmpty(e.NextAction)) value += " · " + LaborVisualText.NextActionLabel(e.NextAction);
                if (!string.IsNullOrEmpty(e.CurrentStationKey)) value += "\n" + Text("Station") + ": " + e.CurrentStationKey;
                value += "\n" + e.EventId;
                if (!string.IsNullOrEmpty(e.EvidenceId)) value += " · " + e.EvidenceId;
                if (!string.IsNullOrEmpty(e.SubmissionId)) value += " · " + e.SubmissionId;
                rows.Add(new NeedTextRow(Text("HistoryTick", e.Tick), value));
            }
            return rows;
        }
    }

    public sealed partial class GameComponent_PawnEconomy
    {
        internal const int LaborContractColumnCount = LaborContractProjection.ColumnCount;
        internal string LaborStationLabel(string stationKey)
        {
            if (string.IsNullOrEmpty(stationKey)) return LaborPresentationRows.Text("NoStation");
            var station = anchors.FirstOrDefault(a => a != null && a.Spawned && a.GetUniqueLoadID() == stationKey);
            return station?.LabelCap.ToString() ?? LaborPresentationRows.Text("NoStation");
        }
        internal IReadOnlyList<LaborFlowEvent> QueryLaborHistory(string workflowId) => LaborFlow.ReadHistory(workflowId);
        internal string LaborHistoryDetail(LaborWorkflow flow)
        {
            if (flow == null) return string.Empty;
            return string.Join("\n", LaborPresentationRows.History(LaborFlow.ReadHistory(flow.WorkflowId))
                .Select(row => row.Label + ": " + row.Value));
        }
        private List<EconomyRow> QueryVisualContractRows(Map map, string anchorKey)
        {
            var rows = QueryVanillaContractRows(map, anchorKey);
            // Completed/expired contracts can retain delivery rights. They stay visible until the player can
            // inspect those rights; the active-work contract index alone cannot represent pending settlement.
            var shown = new HashSet<string>(rows.Where(r => r.Key != null).Select(r => r.Key), StringComparer.Ordinal);
            if (map != null)
                foreach (var wage in map.mapPawns.FreeColonists.SelectMany(p => QueryPayables(p.GetUniqueLoadID())))
                {
                    if (shown.Contains(wage.ContractId)) continue;
                    var contract = Employment.ReadContract(wage.ContractId);
                    if (contract == null || contract.InstitutionKey != anchorKey || !IsVanillaOpportunity(contract.PositionKey)) continue;
                    shown.Add(contract.ContractId);
                    string pawn = PawnName(contract.PawnKey);
                    string work = ContractWorkLabel(contract, contract.WorkTypeDefNames.FirstOrDefault() ?? string.Empty);
                    long paid = QueryPaidMinorFor(contract.ContractId), due = QueryUnpaidMinorFor(contract.ContractId);
                    rows.Add(VanillaContractRow.Compose(contract.ContractId, pawn, work, contract.CompletedJobs,
                        contract.MaxJobs, paid, due, contract.WagePerJobMinor, Money(paid), Money(due),
                        ContractRowText(pawn, work, contract.CompletedJobs, contract.MaxJobs, contract.WagePerJobMinor,
                            Money(paid), Money(due)), ContractDetailText(contract), pawn + " " + contract.ContractId, null));
                }
            var output = new List<EconomyRow>(rows.Count);
            foreach (var row in rows)
            {
                if (row.Cells == null) { output.Add(row); continue; }
                var flow = QueryLaborWorkflow(row.Key);
                var totals = new LaborUiTotals(row.Key, LaborFlow.ReadBatchesFor(row.Key), Employment.ReadObligationsForContract(row.Key));
                string stage = flow == null ? LaborVisualText.StageLabel(LaborFlowStage.None) : LaborVisualText.StageLabel(flow.Stage);
                if (flow != null && flow.Hold != FlowHold.None) stage += " · " + LaborVisualText.HoldLabel(flow.Hold);
                string station = LaborStationLabel(flow?.CurrentStationKey);
                string facts = LaborPresentationRows.Text("Stage") + ": " + stage + " · "
                    + LaborPresentationRows.Text("DeliveryValue", totals.Delivered, totals.Remaining) + " · "
                    + LaborPresentationRows.Text("Station") + ": " + station;
                if (flow != null) facts += " · " + LaborVisualText.ReasonLabel(flow.ReasonCode)
                    + " · " + LaborVisualText.NextActionLabel(flow.NextAction);
                output.Add(LaborContractProjection.Expand(row, stage, flow == null ? 0 : (long)flow.Stage,
                    totals.Delivered, totals.Remaining, station, facts, LaborHistoryDetail(flow)));
            }
            return output;
        }
        private List<WorkspaceTableSection> BuildLaborWageSections(Map map)
        {
            var sections = new List<WorkspaceTableSection>(4);
            var all = map.mapPawns.FreeColonists.SelectMany(p => QueryPayables(p.GetUniqueLoadID()))
                .GroupBy(w => w.ObligationId).Select(g => g.First()).ToList();
            // Per-query caches die with this snapshot. The authoritative 4096-obligation bound also bounds
            // the distinct contract keys; there is no static state or cache carried to a later refresh.
            var flowsByContract = new Dictionary<string, LaborWorkflow>(StringComparer.Ordinal);
            var batchesByContract = new Dictionary<string, IReadOnlyList<ProductBatch>>(StringComparer.Ordinal);
            var historyByContract = new Dictionary<string, string>(StringComparer.Ordinal);
            var issuerByContract = new Dictionary<string, string>(StringComparer.Ordinal);
            var serviceByContract = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (LaborWageGroup group in Enum.GetValues(typeof(LaborWageGroup)))
            {
                var rows = new List<EconomyRow>();
                foreach (var wage in all.Where(w => LaborUiTotals.Group(w) == group).OrderByDescending(w => w.DueTick))
                {
                    if (!flowsByContract.TryGetValue(wage.ContractId, out var flow))
                    {
                        flow = QueryLaborWorkflow(wage.ContractId);
                        flowsByContract.Add(wage.ContractId, flow);
                        batchesByContract.Add(wage.ContractId, LaborFlow.ReadBatchesFor(wage.ContractId));
                        historyByContract.Add(wage.ContractId, LaborHistoryDetail(flow));
                        issuerByContract.Add(wage.ContractId, LaborStationLabel(Employment.ReadContract(wage.ContractId)?.InstitutionKey));
                        serviceByContract.Add(wage.ContractId, LaborStationLabel(flow?.CurrentStationKey));
                    }
                    var batches = batchesByContract[wage.ContractId].Where(b => b.EvidenceId == wage.EvidenceId);
                    var total = new LaborUiTotals(wage.ContractId, batches, new[] { wage });
                    string name = PawnName(wage.PayeeKey), shortId = LaborFlowRules.ShortCode(wage.ContractId);
                    string status = LaborVisualText.EligibilityLabel(wage.Eligibility);
                    if (flow != null) status = LaborVisualText.StageLabel(flow.Stage) + " · " + status;
                    if (flow != null && flow.Hold != FlowHold.None) status += " · " + LaborVisualText.HoldLabel(flow.Hold)
                        + " · " + LaborVisualText.ReasonLabel(flow.ReasonCode);
                    string[] cells = { name, shortId, status, total.Delivered.ToString(CultureInfo.InvariantCulture),
                        total.Remaining.ToString(CultureInfo.InvariantCulture), Money(total.DueMinor), Money(total.PaidMinor), serviceByContract[wage.ContractId] };
                    string detail = LaborPresentationRows.Text("Contract") + ": " + wage.ContractId + "\n" + wage.ObligationId
                        + "\n" + wage.EvidenceId + "\n" + wage.SubmissionId + "\n" + (wage.TxId ?? string.Empty)
                        + "\n" + LaborPresentationRows.Text("Station") + ": " + issuerByContract[wage.ContractId]
                        + "\n" + historyByContract[wage.ContractId];
                    rows.Add(new EconomyRow(string.Join(" · ", cells), key: wage.ObligationId, cells: cells,
                        cellSorts: new[] { 0L, 0L, (long)wage.Eligibility, total.Delivered, total.Remaining, total.DueMinor, total.PaidMinor, 0L },
                        search: name + " " + wage.ContractId + " " + wage.EvidenceId, detail: detail));
                }
                if (rows.Count == 0) rows.Add(Hint(LaborPresentationRows.Text("Empty")));
                var columns = new List<EconomyTableColumn>
                {
                    ContractColumn("pawn", "Workspace.Contract.Col.Pawn", 100),
                    ContractColumn("contract", "LaborVisual.UI.Contract", 95),
                    ContractColumn("stage", "LaborVisual.UI.Stage", 125),
                    ContractColumn("delivered", "LaborVisual.UI.Delivered", 80, true),
                    ContractColumn("remaining", "LaborVisual.UI.Remaining", 80, true),
                    ContractColumn("due", "LaborVisual.UI.WageDue", 90, true),
                    ContractColumn("paid", "Workspace.Contract.Paid", 90, true),
                    ContractColumn("station", "LaborVisual.UI.Station", 125)
                };
                sections.Add(new WorkspaceTableSection("wage-" + group, LaborPresentationRows.Text("Group." + group)
                    + " (" + all.Count(w => LaborUiTotals.Group(w) == group).ToString(CultureInfo.InvariantCulture) + ")",
                    new EconomyTable(columns, null, null, -1, searchable: true), rows));
            }
            return sections;
        }
    }
}
