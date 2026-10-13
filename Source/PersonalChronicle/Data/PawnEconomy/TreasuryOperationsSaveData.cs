using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using Verse;

namespace PersonalChronicle.Data.PawnEconomy
{
    internal sealed class TreasuryOperationsSaveData : IExposable
    {
        private string wire, encoding;
        private bool preserveRaw;
        private readonly List<TreasuryDelivery> orders = new List<TreasuryDelivery>();
        private readonly Dictionary<string, LegacyDisposition> decisions = new Dictionary<string, LegacyDisposition>(StringComparer.Ordinal);
        public string Fault { get; private set; }
        public string SnapshotHash { get; private set; }
        public long Revision { get; private set; }
        public IReadOnlyList<TreasuryDelivery> ReadOrders() => orders.AsReadOnly();
        public IReadOnlyDictionary<string, LegacyDisposition> ReadDecisions() => new System.Collections.ObjectModel.ReadOnlyDictionary<string, LegacyDisposition>(decisions);
        public TreasuryDelivery Pending => orders.FirstOrDefault(x => x.Pending);
        public TreasuryDelivery Find(int id) => orders.FirstOrDefault(x => x.Id == id);
        public int NextId => orders.Count + 1;
        public void Freeze(string fault)
        {
            if (Fault != null) return;
            Fault = MinorCurrency.Required(fault); if (Revision != long.MaxValue) Revision++;
        }
        public bool Add(TreasuryDelivery order)
        {
            if (Fault != null || order == null || order.Id != NextId || Pending != null || orders.Count >= TreasuryDelivery.MaximumOrders
                || orders.Any(x => x.CommandId == order.CommandId) || Revision == long.MaxValue) return false;
            orders.Add(order); Revision++; return true;
        }
        public void Update(TreasuryDelivery order)
        {
            var previous = order == null ? null : Find(order.Id);
            if (Fault != null || previous == null || !previous.Pending || Revision == long.MaxValue) return;
            if (previous.CommandId != order.CommandId || previous.PawnKey != order.PawnKey || previous.BankKey != order.BankKey
                || previous.Kind != order.Kind || previous.SourceKey != order.SourceKey || previous.Count != order.Count
                || previous.MapId != order.MapId || previous.LedgerVersion != order.LedgerVersion || previous.ReserveVersion != order.ReserveVersion
                || previous.ExpiresTick != order.ExpiresTick) throw new InvalidOperationException("DeliveryIdentityChanged");
            if (previous.CarryKey != null && previous.CarryKey != order.CarryKey
                || previous.Phase == TreasuryDeliveryPhase.Carrying && order.Phase == TreasuryDeliveryPhase.Waiting) throw new InvalidOperationException("DeliveryPhaseReversed");
            orders[order.Id - 1] = order; Revision++;
        }
        // Called only at actual delivery, before any deposit ledger row or reserve proof exists.
        // Purchases/wages during a haul may advance the ledger; the physical delivery identity stays fixed.
        internal bool RebaseUnpreparedDeposit(int id, long ledgerVersion, long reserveVersion)
        {
            var order = Find(id);
            if (Fault != null || Revision == long.MaxValue || order?.Kind != ReserveMovementKind.Deposit
                || order.Phase != TreasuryDeliveryPhase.Carrying || ledgerVersion < order.LedgerVersion || reserveVersion < order.ReserveVersion) return false;
            orders[id - 1] = new TreasuryDelivery(order.Id, order.CommandId, order.Kind, order.Phase, order.PawnKey,
                order.BankKey, order.MapId, order.SourceKey, order.CarryKey, order.Count, order.ExpiresTick, ledgerVersion, reserveVersion);
            Revision++; return true;
        }
        public bool Acknowledge(string snapshotHash, string key, LegacyDisposition disposition)
        {
            if (Fault != null || Revision == long.MaxValue || string.IsNullOrEmpty(snapshotHash) || string.IsNullOrEmpty(key)
                || decisions.Count >= LegacyCutoverPlan.MaximumItems || !Enum.IsDefined(typeof(LegacyDisposition), disposition)) return false;
            if (SnapshotHash != null && SnapshotHash != snapshotHash) return false;
            if (decisions.TryGetValue(key, out var old)) return old == disposition;
            SnapshotHash = snapshotHash; decisions.Add(key, disposition); Revision++; return true;
        }
        private string Encode() => new XElement("TreasuryOperations", new XAttribute("schema", 1), new XAttribute("revision", Revision),
            Fault == null ? null : new XAttribute("fault", Fault),
            SnapshotHash == null ? null : new XAttribute("snapshotHash", SnapshotHash),
            orders.Select(o => new XElement("Delivery", new XAttribute("id", o.Id), new XAttribute("command", o.CommandId),
                new XAttribute("kind", o.Kind), new XAttribute("phase", o.Phase), new XAttribute("pawn", o.PawnKey),
                new XAttribute("bank", o.BankKey), new XAttribute("map", o.MapId), new XAttribute("source", o.SourceKey),
                o.CarryKey == null ? null : new XAttribute("carry", o.CarryKey), new XAttribute("count", o.Count),
                new XAttribute("expires", o.ExpiresTick), new XAttribute("ledgerVersion", o.LedgerVersion), new XAttribute("reserveVersion", o.ReserveVersion))),
            decisions.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new XElement("Decision", new XAttribute("key", x.Key), new XAttribute("disposition", x.Value))))
            .ToString(SaveOptions.DisableFormatting);
        private static void Fields(XElement node, params string[] names)
        {
            if (node.Attributes().Any(x => x.Name.NamespaceName.Length != 0 || !names.Contains(x.Name.LocalName))
                || node.Nodes().OfType<XText>().Any(x => !string.IsNullOrWhiteSpace(x.Value))) throw new FormatException("UnknownTreasuryField");
        }
        private void Decode(string raw)
        {
            orders.Clear(); decisions.Clear(); SnapshotHash = null; Revision = 0; Fault = null; preserveRaw = false;
            if (string.IsNullOrEmpty(raw)) return;
            using (var reader = XmlReader.Create(new StringReader(raw), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8000000 }))
            {
                var root = XElement.Load(reader); Fields(root, "schema", "revision", "snapshotHash", "fault");
                if (root.Name != "TreasuryOperations" || (int)root.Attribute("schema") != 1) throw new FormatException("UnknownTreasurySchema");
                long revision = (long)root.Attribute("revision"); if (revision < 0 || revision == long.MaxValue) throw new FormatException("InvalidTreasuryRevision");
                SnapshotHash = (string)root.Attribute("snapshotHash");
                Fault = (string)root.Attribute("fault"); if (Fault != null) MinorCurrency.Required(Fault);
                foreach (var node in root.Elements())
                {
                    if (node.HasElements) throw new FormatException("UnknownTreasuryChild");
                    if (node.Name == "Delivery")
                    {
                        Fields(node, "id", "command", "kind", "phase", "pawn", "bank", "map", "source", "carry", "count", "expires", "ledgerVersion", "reserveVersion");
                        var order = new TreasuryDelivery((int)node.Attribute("id"), (string)node.Attribute("command"),
                            (ReserveMovementKind)Enum.Parse(typeof(ReserveMovementKind), (string)node.Attribute("kind")),
                            (TreasuryDeliveryPhase)Enum.Parse(typeof(TreasuryDeliveryPhase), (string)node.Attribute("phase")),
                            (string)node.Attribute("pawn"), (string)node.Attribute("bank"), (int)node.Attribute("map"), (string)node.Attribute("source"),
                            (string)node.Attribute("carry"), (int)node.Attribute("count"), (int)node.Attribute("expires"), (long)node.Attribute("ledgerVersion"), (long)node.Attribute("reserveVersion"));
                        if (order.Id != orders.Count + 1 || orders.Any(x => x.CommandId == order.CommandId)) throw new FormatException("DuplicateTreasuryOrder");
                        orders.Add(order);
                    }
                    else if (node.Name == "Decision")
                    {
                        Fields(node, "key", "disposition"); string key = MinorCurrency.Required((string)node.Attribute("key"));
                        var disposition = (LegacyDisposition)Enum.Parse(typeof(LegacyDisposition), (string)node.Attribute("disposition"));
                        if (!Enum.IsDefined(typeof(LegacyDisposition), disposition) || decisions.ContainsKey(key)
                            || decisions.Count >= LegacyCutoverPlan.MaximumItems || string.IsNullOrEmpty(SnapshotHash)) throw new FormatException("InvalidCutoverDecision");
                        decisions.Add(key, disposition);
                    }
                    else throw new FormatException("UnknownTreasuryElement");
                }
                if (orders.Count(x => x.Pending) > 1 || revision < orders.Count + decisions.Count) throw new FormatException("InvalidTreasurySequence");
                Revision = revision;
            }
        }
        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && !preserveRaw)
            { wire = Convert.ToBase64String(Encoding.UTF8.GetBytes(Encode())); encoding = "base64-utf8-v1"; }
            // Unknown future wire need not itself be base64. Wrap it for lossless XML newline handling.
            string persistedWire = wire; bool wireEnvelope = preserveRaw;
            if (Scribe.mode == LoadSaveMode.Saving && wireEnvelope && wire != null)
                persistedWire = Convert.ToBase64String(Encoding.UTF8.GetBytes(wire));
            Scribe_Values.Look(ref persistedWire, "payload"); Scribe_Values.Look(ref encoding, "encoding");
            Scribe_Values.Look(ref wireEnvelope, "wireEnvelope", false);
            if (Scribe.mode != LoadSaveMode.LoadingVars) return;
            wire = persistedWire;
            try
            {
                if (wireEnvelope && wire != null) wire = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(wire));
                if (encoding != null && encoding != "base64-utf8-v1") throw new FormatException("UnknownTreasuryEncoding");
                Decode(encoding == null ? wire : wire == null ? null : new UTF8Encoding(false, true).GetString(Convert.FromBase64String(wire)));
            }
            catch (Exception error) when (error is XmlException || error is FormatException || error is ArgumentException
                || error is OverflowException || error is InvalidOperationException || error is NullReferenceException)
            { orders.Clear(); decisions.Clear(); preserveRaw = true; Fault = "UnknownTreasuryOperations"; }
        }
    }
}
