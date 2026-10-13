using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Production;

namespace PersonalChronicle.Application.PawnEconomy.Production
{
    /// <summary>M10 workshop capacity reading: real readings from the existing workshop runtime. Every
    /// field is a <see cref="Fact{T}"/>; an unreadable workshop yields unknown fields, never zeros that
    /// would look like "no gap".</summary>
    internal readonly struct WorkshopCapacityReading
    {
        public Fact<int> ShiftCount { get; }
        public Fact<int> StaffingGap { get; }
        public Fact<int> OpenBills { get; }
        public Fact<int> ActiveWorkers { get; }
        public WorkshopCapacityReading(Fact<int> shiftCount, Fact<int> staffingGap, Fact<int> openBills, Fact<int> activeWorkers)
        {
            ShiftCount = shiftCount; StaffingGap = staffingGap; OpenBills = openBills; ActiveWorkers = activeWorkers;
        }
        public static WorkshopCapacityReading Unreadable(string reason)
            => new WorkshopCapacityReading(Fact<int>.Unknown(reason), Fact<int>.Unknown(reason),
                Fact<int>.Unknown(reason), Fact<int>.Unknown(reason));
        public static WorkshopCapacityReading Of(int shifts, int gap, int bills, int workers)
            => new WorkshopCapacityReading(Fact<int>.Known(shifts), Fact<int>.Known(gap),
                Fact<int>.Known(bills), Fact<int>.Known(workers));
    }

    /// <summary>M10 restock proposal: a request to cover a real computed shortfall. It is a proposal
    /// only — the existing workshop executor stays the single executor for bills and schedules.</summary>
    internal sealed class ProductionProposal
    {
        public string MapKey { get; }
        public string CategoryKey { get; }
        public long ShortfallMinor { get; }
        public string ReasonCode { get; }
        public ProductionProposal(string mapKey, string categoryKey, long shortfallMinor, string reasonCode)
        {
            MapKey = MinorCurrency.Required(mapKey);
            CategoryKey = MinorCurrency.Required(categoryKey);
            if (shortfallMinor <= 0) throw new ArgumentOutOfRangeException(nameof(shortfallMinor));
            MinorCurrency.Validate(shortfallMinor);
            ShortfallMinor = shortfallMinor;
            ReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? ProductionProposalReasons.Restock : reasonCode;
        }
        public string ProposalId => MapKey + "|" + CategoryKey + "|" + ShortfallMinor.ToString(CultureInfo.InvariantCulture);
    }

    internal static class ProductionProposalReasons
    {
        public const string Restock = "Restock";
        public const string SinkNotWired = "ExecutorSinkNotWired";
        public const string Duplicate = "ProposalDuplicate";
        public const string ReceiptCapacityReached = "ProposalCapacityReached";
        public const string Invalid = "ProposalInvalid";
        public const string ExecutorFailure = "ExecutorFailure";
    }

    /// <summary>One persisted authorisation row. The receipt is opaque: written once, stored verbatim
    /// and returned unchanged, so a reload can never rewrite what a player already holds.</summary>
    internal sealed class RestockProposalRow
    {
        public string ProposalId { get; }
        public string Receipt { get; }
        /// <summary>Game tick the proposal was accepted at; 0 means unknown (legacy row) and never expires.</summary>
        public int AcceptedTick { get; }
        /// <summary>
        /// M10 完整闭环（2026-10-10）：受理时落到**真实工单**上的稳定键
        /// （<c>&lt;建筑 defName&gt;:&lt;thingIDNumber&gt;#&lt;bill.uniqueId&gt;</c>）。
        /// null/空 = 未绑定（v1 旧档行）—— 未绑定行**不参与交付核销**，只能靠年龄裁剪退出。
        /// </summary>
        public string TargetBillKey { get; }
        /// <summary>M10 完整闭环：已按真实完成事件核销的累计数量（0 = 尚未交付）。</summary>
        public long DeliveredAmount { get; }
        public RestockProposalRow(string proposalId, string receipt, int acceptedTick,
            string targetBillKey = null, long deliveredAmount = 0L)
        {
            ProposalId = proposalId; Receipt = receipt; AcceptedTick = acceptedTick;
            TargetBillKey = targetBillKey;
            DeliveredAmount = deliveredAmount < 0L ? 0L : deliveredAmount;
        }
        /// <summary>返还有交付进展的新行 —— 行本身不可变（回执一经写入永不改写），核销即整体替换。</summary>
        public RestockProposalRow WithDelivery(long produced)
        {
            if (produced <= 0L) return this;
            long next = DeliveredAmount > long.MaxValue - produced ? long.MaxValue : DeliveredAmount + produced;
            return new RestockProposalRow(ProposalId, Receipt, AcceptedTick, TargetBillKey, next);
        }
    }

    /// <summary>Bounded ledger for accepted proposals (M10 "authorised, not yet started"). It stores
    /// receipts only: accepting a proposal never creates a bill and never touches a schedule. A
    /// repeated proposal (same map, category and amount) answers with the original receipt and never
    /// counts twice — including across a save/load round trip, because the receipt is derived from the
    /// proposal id alone (a persisted insertion counter would drift on reload).</summary>
    internal sealed class RestockProposalLedger
    {
        public const int MaximumReceipts = 16;
        public const string ReceiptPrefix = "restock-v1:";
        private readonly Dictionary<string, RestockProposalRow> rows = new Dictionary<string, RestockProposalRow>(StringComparer.Ordinal);
        private readonly List<string> order = new List<string>();
        public int Count => rows.Count;
        public int Replays { get; private set; }
        /// <summary>Accepted proposal ids in acceptance order.</summary>
        public IReadOnlyList<string> Accepted
        {
            get { return order.AsReadOnly(); }
        }
        /// <summary>未绑定重载：受理不带工单键的提案（测试与旧路径）。生产路径应使用带 <c>targetBillKey</c> 的重载。</summary>
        public bool TryAccept(ProductionProposal proposal, int now, out string receipt, out bool replay, out string reason)
            => TryAccept(proposal, now, null, out receipt, out replay, out reason);

        /// <summary>
        /// Accepts one proposal. <paramref name="targetBillKey"/> 是受理时**已真实建单**的工单稳定键
        /// （M10 完整闭环：授权必须绑定既有合法工单载体 ⇒ 调用方应在建单失败时就拒绝受理，绝不记下无载体的账）。
        /// 传 null 表示未绑定（保留给测试与旧档案路径）。
        /// </summary>
        public bool TryAccept(ProductionProposal proposal, int now, string targetBillKey,
            out string receipt, out bool replay, out string reason)
        {
            receipt = null; replay = false; reason = null;
            if (proposal == null) { reason = ProductionProposalReasons.Invalid; return false; }
            if (rows.TryGetValue(proposal.ProposalId, out var existing))
            { receipt = existing.Receipt; replay = true; Replays++; return true; }
            if (rows.Count >= MaximumReceipts) { reason = ProductionProposalReasons.ReceiptCapacityReached; return false; }
            receipt = ReceiptPrefix + proposal.ProposalId;
            rows[proposal.ProposalId] = new RestockProposalRow(proposal.ProposalId, receipt, now, targetBillKey);
            order.Add(proposal.ProposalId);
            return true;
        }
        /// <summary>M10 完整闭环：判断某提案是否已在账内（受理路径据此避免重复建单）。</summary>
        public bool Has(string proposalId)
            => !string.IsNullOrWhiteSpace(proposalId) && rows.ContainsKey(proposalId);
        public bool TryReadReceipt(string proposalId, out string receipt)
        {
            receipt = null;
            if (string.IsNullOrWhiteSpace(proposalId) || !rows.TryGetValue(proposalId, out var row)) return false;
            receipt = row.Receipt;
            return receipt != null;
        }
        /// <summary>Releases exactly one authorisation (revoked by the player, or superseded). Releasing a
        /// missing row is a no-op that reports false and never throws.</summary>
        public bool TryRelease(string proposalId)
        {
            if (string.IsNullOrWhiteSpace(proposalId) || !rows.Remove(proposalId)) return false;
            order.Remove(proposalId);
            return true;
        }
        /// <summary>Deterministic age trim: expired rows are removed so a persisted ledger that refuses
        /// new rows at capacity can never lock up forever. Returns the number of rows removed.</summary>
        public int TrimExpired(int now, int maximumAgeTicks)
        {
            int removed = 0;
            for (int i = order.Count - 1; i >= 0; i--)
            {
                var id = order[i];
                if (!rows.TryGetValue(id, out var row)) { order.RemoveAt(i); continue; }
                if (!ProductionAuthorisationRules.IsExpired(row.AcceptedTick, now, maximumAgeTicks)) continue;
                rows.Remove(id); order.RemoveAt(i); removed++;
            }
            return removed;
        }
        /// <summary>Authorised quantity for one map|category, summed over the ledger. Malformed ids (only
        /// possible from a hand-edited save) are ignored and never counted.</summary>
        public long AuthorisedAmount(string mapKey, string categoryKey)
        {
            if (string.IsNullOrWhiteSpace(mapKey) || string.IsNullOrWhiteSpace(categoryKey)) return 0;
            long sum = 0;
            for (int i = 0; i < order.Count; i++)
            {
                if (!rows.TryGetValue(order[i], out var row)) continue;
                if (!TryParseProposalId(row.ProposalId, out string map, out string category, out long amount)) continue;
                if (!string.Equals(map, mapKey, StringComparison.Ordinal)
                    || !string.Equals(category, categoryKey, StringComparison.Ordinal)) continue;
                sum = ProductionAuthorisationRules.Sum(sum, amount);
            }
            return sum;
        }
        /// <summary>
        /// M10 完整闭环：按工单稳定键回匹配授权行并按**真实产出量**累加已交付量。
        /// 返回被更新的行数（0 = 该工单不属于任何授权，属绝大多数完成事件，零成本）。
        /// 同一工单只核销到它自己那一行；品类不符的产出不计入（多产物配方）。
        /// </summary>
        public int RecordDelivery(string billKey, string categoryKey, long produced)
        {
            if (string.IsNullOrWhiteSpace(billKey) || string.IsNullOrWhiteSpace(categoryKey) || produced <= 0L) return 0;
            for (int i = 0; i < order.Count; i++)
            {
                if (!rows.TryGetValue(order[i], out var row)) continue;
                if (string.IsNullOrEmpty(row.TargetBillKey)
                    || !string.Equals(row.TargetBillKey, billKey, StringComparison.Ordinal)) continue;
                if (!TryParseProposalId(row.ProposalId, out _, out string rowCategory, out _)) continue;
                if (!string.Equals(rowCategory, categoryKey, StringComparison.Ordinal)) continue;
                rows[order[i]] = row.WithDelivery(produced);
                return 1;
            }
            return 0;
        }

        /// <summary>M10 完整闭环：某地图|品类上**已交付**的累计量（0 = 无交付，非"未知"）。</summary>
        public long DeliveredAmount(string mapKey, string categoryKey)
        {
            if (string.IsNullOrWhiteSpace(mapKey) || string.IsNullOrWhiteSpace(categoryKey)) return 0;
            long sum = 0;
            for (int i = 0; i < order.Count; i++)
            {
                if (!rows.TryGetValue(order[i], out var row)) continue;
                if (!TryParseProposalId(row.ProposalId, out string map, out string category, out _)) continue;
                if (!string.Equals(map, mapKey, StringComparison.Ordinal)
                    || !string.Equals(category, categoryKey, StringComparison.Ordinal)) continue;
                sum = ProductionAuthorisationRules.Sum(sum, row.DeliveredAmount);
            }
            return sum;
        }

        /// <summary>M10 完整闭环：**未交付**的授权净额（授权量 − 已交付量），供读数收敛使用。</summary>
        public long NetOutstandingAmount(string mapKey, string categoryKey)
        {
            long authorised = AuthorisedAmount(mapKey, categoryKey);
            long delivered = DeliveredAmount(mapKey, categoryKey);
            return authorised <= delivered ? 0L : authorised - delivered;
        }

        /// <summary>Verbatim rows for the save bridge, in acceptance order.</summary>
        public IReadOnlyList<RestockProposalRow> ExportRows()
        {
            var list = new List<RestockProposalRow>(order.Count);
            for (int i = 0; i < order.Count; i++) if (rows.TryGetValue(order[i], out var row)) list.Add(row);
            return list.AsReadOnly();
        }
        /// <summary>Load path: clears first, then imports structurally valid rows up to the single bound,
        /// keeping the newest by accepted tick. Invalid rows are dropped, never repaired, and a corrupt
        /// row can never fail the load.</summary>
        public int ImportRows(IReadOnlyList<RestockProposalRow> imported)
        {
            rows.Clear(); order.Clear();
            if (imported == null) return 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var valid = new List<RestockProposalRow>();
            for (int i = 0; i < imported.Count; i++)
            {
                var row = imported[i];
                if (row == null || string.IsNullOrWhiteSpace(row.ProposalId) || string.IsNullOrWhiteSpace(row.Receipt)) continue;
                if (!TryParseProposalId(row.ProposalId, out _, out _, out _)) continue;
                if (!seen.Add(row.ProposalId)) continue;
                valid.Add(row);
            }
            valid.Sort((a, b) => a.AcceptedTick.CompareTo(b.AcceptedTick));
            int skip = valid.Count > MaximumReceipts ? valid.Count - MaximumReceipts : 0;
            int kept = 0;
            for (int i = skip; i < valid.Count; i++)
            {
                rows[valid[i].ProposalId] = valid[i];
                order.Add(valid[i].ProposalId);
                kept++;
            }
            return kept;
        }
        /// <summary>Canonical proposal id format: mapKey|categoryKey|amount (invariant). Anything else is
        /// refused: a malformed row must never be counted as an authorisation.</summary>
        public static bool TryParseProposalId(string proposalId, out string mapKey, out string categoryKey, out long amount)
        {
            mapKey = null; categoryKey = null; amount = 0;
            if (string.IsNullOrWhiteSpace(proposalId)) return false;
            // ⚠ 必须走 `Split(char[])` 重载：`Split(char, StringSplitOptions)` 是 netstandard2.1 才有的重载，
            // 编译期能过、net48 运行时抛 MissingMethodException（本工程已踩过一次同类 BCL 面陷阱）。
            var parts = proposalId.Split(new[] { '|' });
            if (parts.Length != 3) return false;
            if (string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1])) return false;
            if (!long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out amount) || amount <= 0) return false;
            mapKey = parts[0]; categoryKey = parts[1];
            return true;
        }
    }

    /// <summary>M10 executor contract. Read capacity from the existing workshop runtime and accept
    /// restock proposals as bookkeeping only. The contract deliberately has no execute method: bills
    /// and schedules remain owned by the existing workshop executor (no second runner).</summary>
    internal interface IWorkshopExecutorContract
    {
        WorkshopCapacityReading ReadCapacity(int mapId);
        bool TrySubmitRestockProposal(ProductionProposal proposal, out string receipt, out bool replay, out string reason);
    }

    /// <summary>Safe zero for an unwired executor: nothing readable, nothing accepted.</summary>
    internal sealed class UnwiredWorkshopExecutor : IWorkshopExecutorContract
    {
        public WorkshopCapacityReading ReadCapacity(int mapId) => WorkshopCapacityReading.Unreadable(ProductionProposalReasons.SinkNotWired);
        public bool TrySubmitRestockProposal(ProductionProposal proposal, out string receipt, out bool replay, out string reason)
        { receipt = null; replay = false; reason = ProductionProposalReasons.SinkNotWired; return false; }
    }

    /// <summary>Guarded entry points: contract failures degrade to "unreadable" / "refused" with a stable
    /// reason code and never propagate an exception into a tick.</summary>
    internal static class WorkshopExecutorGuard
    {
        public static WorkshopCapacityReading ReadCapacity(IWorkshopExecutorContract contract, int mapId)
        {
            if (contract == null || mapId < 0) return WorkshopCapacityReading.Unreadable(ProductionProposalReasons.SinkNotWired);
            try { return contract.ReadCapacity(mapId); }
            catch (Exception) { return WorkshopCapacityReading.Unreadable(ProductionProposalReasons.ExecutorFailure); }
        }
        public static bool TrySubmit(IWorkshopExecutorContract contract, ProductionProposal proposal,
            out string receipt, out bool replay, out string reason)
        {
            receipt = null; replay = false;
            reason = null;
            if (contract == null || proposal == null) { reason = ProductionProposalReasons.Invalid; return false; }
            try
            {
                if (contract.TrySubmitRestockProposal(proposal, out receipt, out replay, out string contractReason))
                    return true;
                // The contract reports why it refused; only a completed-but-unwired sink falls back to
                // SinkNotWired. A full ledger must never be reported as "not wired".
                reason = string.IsNullOrEmpty(contractReason)
                    ? (replay ? ProductionProposalReasons.Duplicate : ProductionProposalReasons.SinkNotWired)
                    : contractReason;
                return false;
            }
            catch (Exception) { reason = ProductionProposalReasons.ExecutorFailure; receipt = null; replay = false; return false; }
        }
    }
}
