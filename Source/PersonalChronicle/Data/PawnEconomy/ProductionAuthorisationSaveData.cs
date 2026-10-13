using System;
using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Data.PawnEconomy
{
    /// <summary>Additive save node for M10 production authorisations (the "authorised, not yet started"
    /// bucket). A save without this node loads as an empty ledger, which is the authoritative "nothing
    /// authorised" answer — never a migrated or invented authorisation. Rows are opaque: the proposal id
    /// and receipt are stored verbatim and validated by the economy bridge, never rewritten here.</summary>
    internal sealed class ProductionAuthorisationSaveData : IExposable
    {
        /// <summary>M10 完整闭环（2026-10-10）：v2 新增工单绑定键与已交付量。v1 档的这两项分别读作
        /// null / 0 —— 即"未绑定、未交付"，属**显式语义**而非猜测（SAVE-003）。</summary>
        public const int SchemaVersion = 2;
        /// <summary>Single bound shared with the runtime ledger: the save layer never invents a second
        /// ceiling, so a full ledger stays full across a reload.</summary>
        public const int MaximumRows = 16;

        public sealed class Row : IExposable
        {
            public string ProposalId;
            public string Receipt;
            public int AcceptedTick;
            /// <summary>M10 闭环：受理时绑定到的真实工单稳定键；null = 未绑定（v1 旧档行）。</summary>
            public string TargetBillKey;
            /// <summary>M10 闭环：按真实完成事件核销的累计交付量；v1 旧档读作 0。</summary>
            public long DeliveredAmount;
            public void ExposeData()
            {
                Scribe_Values.Look(ref ProposalId, "proposal");
                Scribe_Values.Look(ref Receipt, "receipt");
                Scribe_Values.Look(ref AcceptedTick, "acceptedTick");
                Scribe_Values.Look(ref TargetBillKey, "bill");
                Scribe_Values.Look(ref DeliveredAmount, "delivered", 0L);
                if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
                // 负交付量只可能来自手改存档：夹回 0，绝不产出负数（收敛式以 0 为下界）。
                if (DeliveredAmount < 0L) DeliveredAmount = 0L;
            }
        }

        private int version = SchemaVersion;
        private List<Row> rows = new List<Row>();
        public void ExposeData()
        {
            Scribe_Values.Look(ref version, "version", SchemaVersion);
            Scribe_Collections.Look(ref rows, "authorisations", LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            if (rows == null) rows = new List<Row>();
            rows.RemoveAll(row => row == null || string.IsNullOrWhiteSpace(row.ProposalId) || string.IsNullOrWhiteSpace(row.Receipt));
            while (rows.Count > MaximumRows) rows.RemoveAt(0);   // insertion order is age order here
        }
        public int Version => version;
        public int Count => rows == null ? 0 : rows.Count;
        /// <summary>Bridge entry point: replaces the container content with the live rows (already bounded
        /// by the ledger) and re-applies the single bound.</summary>
        public void ReplaceRows(IReadOnlyList<Row> replacement)
        {
            if (rows == null) rows = new List<Row>();
            rows.Clear();
            if (replacement != null)
                for (int i = 0; i < replacement.Count; i++) if (replacement[i] != null) rows.Add(replacement[i]);
            while (rows.Count > MaximumRows) rows.RemoveAt(0);
        }
        /// <summary>Verbatim rows for the bridge. Null rows never reach here (filtered at load).</summary>
        public IReadOnlyList<Row> ValidRows => rows == null ? (IReadOnlyList<Row>)Array.Empty<Row>() : rows.AsReadOnly();
    }
}
