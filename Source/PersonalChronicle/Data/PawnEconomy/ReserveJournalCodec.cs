using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using PersonalChronicle.Domain.PawnEconomy.Finance;

namespace PersonalChronicle.Data.PawnEconomy
{
    internal sealed class ReserveJournalLoad
    {
        public int Schema { get; }
        public long Version { get; }
        public string Fault { get; }
        public bool PreserveRaw { get; }
        public IReadOnlyList<ReserveMovementEvidence> Entries { get; }
        public ReserveJournalLoad(int schema, long version, string fault, bool preserveRaw, IEnumerable<ReserveMovementEvidence> entries)
        { Schema = schema; Version = version; Fault = fault; PreserveRaw = preserveRaw; Entries = Array.AsReadOnly(entries.ToArray()); }
    }
    internal static class ReserveJournalCodec
    {
        public const int CurrentSchema = 1, MaximumEntries = 4096;
        private static object Value(long? value) => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : null;
        public static string Encode(long version, string fault, IEnumerable<ReserveMovementEvidence> entries)
        {
            return new XElement("ReserveJournal", new XAttribute("schema", CurrentSchema), new XAttribute("version", version),
                fault == null ? null : new XAttribute("fault", fault), entries.Select(e =>
                    new XElement("Move", new XAttribute("id", e.Intent.CommandId), new XAttribute("kind", e.Intent.Kind),
                        new XAttribute("expected", e.Intent.ExpectedVersion), new XAttribute("party", e.Intent.CounterpartyId),
                        new XAttribute("source", e.Intent.SourceThingId), new XAttribute("count", e.Intent.SilverCount),
                        new XAttribute("phase", e.Phase), new XAttribute("reserveBefore", e.ReserveBefore),
                        e.ReserveAfter.HasValue ? new XAttribute("reserveAfter", Value(e.ReserveAfter)) : null,
                        new XAttribute("partyBefore", e.CounterpartyBefore),
                        e.CounterpartyAfter.HasValue ? new XAttribute("partyAfter", Value(e.CounterpartyAfter)) : null,
                        e.ResultThingId == null ? null : new XAttribute("result", e.ResultThingId),
                        e.ApiReturnedCount.HasValue ? new XAttribute("returned", e.ApiReturnedCount.Value) : null,
                        e.Error == null ? null : new XAttribute("error", e.Error)))).ToString(SaveOptions.DisableFormatting);
        }
        public static ReserveJournalLoad Decode(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return new ReserveJournalLoad(CurrentSchema, 0, null, false, Array.Empty<ReserveMovementEvidence>());
            try
            {
                using (var reader = XmlReader.Create(new StringReader(payload), new XmlReaderSettings
                { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16000000 }))
                {
                    var root = XElement.Load(reader);
                    if (root.Name != "ReserveJournal") throw new FormatException("InvalidRoot");
                    int schema = (int)root.Attribute("schema");
                    if (schema != CurrentSchema) return new ReserveJournalLoad(schema, 0, "UnsupportedReserveSchema", true, Array.Empty<ReserveMovementEvidence>());
                    if (root.Attributes().Any(x => !new[] { "schema", "version", "fault" }.Contains(x.Name.LocalName) || x.Name.NamespaceName.Length != 0))
                        throw new FormatException("UnknownJournalField");
                    long version = (long)root.Attribute("version");
                    if (version < 0 || root.Elements().Any(x => x.Name != "Move")) throw new FormatException("InvalidJournal");
                    var rows = root.Elements("Move").Take(MaximumEntries + 1).ToArray();
                    if (rows.Length > MaximumEntries) throw new FormatException("JournalLimit");
                    var entries = new List<ReserveMovementEvidence>(); var ids = new HashSet<string>(StringComparer.Ordinal);
                    long recorded = 0; bool unresolved = false;
                    foreach (var row in rows)
                    {
                        if (row.HasElements || row.Attributes().Any(x => !new[] { "id", "kind", "expected", "party", "source", "count", "phase",
                            "reserveBefore", "reserveAfter", "partyBefore", "partyAfter", "result", "returned", "error" }.Contains(x.Name.LocalName)
                            || x.Name.NamespaceName.Length != 0)) throw new FormatException("UnknownMovementField");
                        var kind = (ReserveMovementKind)Enum.Parse(typeof(ReserveMovementKind), (string)row.Attribute("kind"));
                        var intent = new ReserveMovementIntent((string)row.Attribute("id"), kind, (long)row.Attribute("expected"),
                            (string)row.Attribute("party"), (string)row.Attribute("source"), (int)row.Attribute("count"));
                        var phase = (ReserveMovementPhase)Enum.Parse(typeof(ReserveMovementPhase), (string)row.Attribute("phase"));
                        var evidence = new ReserveMovementEvidence(intent, phase, (long)row.Attribute("reserveBefore"),
                            (long?)row.Attribute("reserveAfter"), (long)row.Attribute("partyBefore"), (long?)row.Attribute("partyAfter"),
                            (string)row.Attribute("result"), (int?)row.Attribute("returned"), (string)row.Attribute("error"));
                        if (!ids.Add(intent.CommandId) || !Enum.IsDefined(typeof(ReserveMovementPhase), phase)
                            || intent.ExpectedVersion != entries.Count || evidence.ReserveBefore < 0 || evidence.CounterpartyBefore < 0
                            || evidence.ReserveAfter < 0 || evidence.CounterpartyAfter < 0 || unresolved || evidence.ReserveBefore != recorded
                            || evidence.ReserveBefore > MinorCurrency.Maximum / MinorCurrency.PerSilver
                            || evidence.CounterpartyBefore > MinorCurrency.Maximum / MinorCurrency.PerSilver
                            || evidence.ReserveAfter > MinorCurrency.Maximum / MinorCurrency.PerSilver
                            || evidence.CounterpartyAfter > MinorCurrency.Maximum / MinorCurrency.PerSilver)
                            throw new FormatException("InvalidJournalEvidence");
                        if (phase == ReserveMovementPhase.Completed)
                        {
                            if (!evidence.HasExactQuantityProof || string.IsNullOrWhiteSpace(evidence.ResultThingId) || evidence.Error != null)
                                throw new FormatException("MissingQuantityProof");
                            recorded = checked(recorded + (kind == ReserveMovementKind.Deposit ? intent.SilverCount : -intent.SilverCount));
                            MinorCurrency.FromSilver(recorded);
                        }
                        else unresolved = true;
                        entries.Add(evidence);
                    }
                    if (version != entries.Count) throw new FormatException("VersionMismatch");
                    string fault = (string)root.Attribute("fault");
                    if (unresolved && string.IsNullOrEmpty(fault)) fault = "UnresolvedMovement";
                    return new ReserveJournalLoad(schema, version, fault, false, entries);
                }
            }
            catch (Exception error) when (error is XmlException || error is FormatException || error is ArgumentException
                || error is OverflowException || error is InvalidOperationException || error is NullReferenceException)
            { return new ReserveJournalLoad(0, 0, "InvalidReservePayload", true, Array.Empty<ReserveMovementEvidence>()); }
        }
    }
}
