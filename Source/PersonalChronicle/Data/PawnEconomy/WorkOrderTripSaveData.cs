using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Verse;

namespace PersonalChronicle.Data.PawnEconomy
{
    /// <summary>Stable pickup proof and native delivery progress; no Pawn/Map/Job objects are saved.</summary>
    public sealed class WorkOrderTripSaveData : IExposable
    {
        public const int CurrentSchema = 1;
        public const int MaximumRows = 2048;
        public sealed class Row
        {
            public string Worker, Contract, Order, WorkType, Target;
            public int Map, Job, Expected, Delivered;
            internal Row Copy() => (Row)MemberwiseClone();
        }
        private readonly Dictionary<string, Row> rows = new Dictionary<string, Row>(StringComparer.Ordinal);
        private string payload;
        public bool Blocked { get; private set; }
        public string BlockReason { get; private set; }
        public string RetainedPayload => payload;
        public int Count => rows.Count;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && !Blocked) payload = ExportPayload();
            Scribe_Values.Look(ref payload, "payload", null);
            if (Scribe.mode == LoadSaveMode.LoadingVars) ImportPayload(payload);
        }

        public bool TryGet(string worker, int job, out Row row)
        {
            row = null;
            if (Blocked || worker == null || !rows.TryGetValue(worker, out Row value) || value.Job != job) return false;
            row = value.Copy();
            return true;
        }
        public bool Upsert(Row row)
        {
            if (Blocked || !Valid(row) || (!rows.ContainsKey(row.Worker) && rows.Count >= MaximumRows)) return false;
            rows[row.Worker] = row.Copy(); // New jobs replace the worker's cancelled old trip, without payment.
            return true;
        }
        public bool AddDelivered(string worker, int job, int added)
        {
            if (Blocked || added <= 0 || worker == null || !rows.TryGetValue(worker, out Row row) || row.Job != job) return false;
            row.Delivered = (int)Math.Min(row.Expected, (long)row.Delivered + added);
            return true;
        }
        public bool Remove(string worker, int job)
        {
            if (Blocked || worker == null || !rows.TryGetValue(worker, out Row row) || row.Job != job) return false;
            return rows.Remove(worker);
        }
        public void RemoveInactive(Func<Row, bool> active)
        {
            if (Blocked || active == null) return;
            foreach (string key in rows.Keys.ToArray()) if (!active(rows[key].Copy())) rows.Remove(key);
        }

        public string ExportPayload()
        {
            if (Blocked) return payload;
            return new XElement("workOrderTrips", new XAttribute("schema", CurrentSchema), rows.Values
                .OrderBy(r => r.Worker, StringComparer.Ordinal).Select(r => new XElement("trip",
                    new XAttribute("worker", r.Worker), new XAttribute("contract", r.Contract),
                    new XAttribute("order", r.Order), new XAttribute("work", r.WorkType), new XAttribute("target", r.Target),
                    new XAttribute("map", r.Map), new XAttribute("job", r.Job),
                    new XAttribute("expected", r.Expected), new XAttribute("delivered", r.Delivered))))
                .ToString(SaveOptions.DisableFormatting);
        }
        public void ImportPayload(string raw)
        {
            payload = raw;
            rows.Clear();
            Blocked = false;
            BlockReason = null;
            if (raw == null) return; // Old saves without the section have no pickup proof.
            try
            {
                if (raw.Length > 4 * 1024 * 1024) throw new InvalidOperationException("payload exceeds bounded size");
                XDocument document;
                using (var reader = XmlReader.Create(new System.IO.StringReader(raw), new XmlReaderSettings {
                    DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) document = XDocument.Load(reader);
                XElement root = document.Root;
                if (root?.Name != "workOrderTrips" || (int?)root.Attribute("schema") != CurrentSchema)
                    throw new InvalidOperationException("unknown trip schema");
                var loaded = new Dictionary<string, Row>(StringComparer.Ordinal);
                foreach (XElement element in root.Elements())
                {
                    if (element.Name != "trip" || loaded.Count >= MaximumRows) throw new InvalidOperationException("invalid trip collection");
                    var row = new Row { Worker = (string)element.Attribute("worker"), Contract = (string)element.Attribute("contract"),
                        Order = (string)element.Attribute("order"), WorkType = (string)element.Attribute("work"), Target = (string)element.Attribute("target"),
                        Map = (int)element.Attribute("map"), Job = (int)element.Attribute("job"),
                        Expected = (int)element.Attribute("expected"), Delivered = (int)element.Attribute("delivered") };
                    if (!Valid(row) || loaded.ContainsKey(row.Worker)) throw new InvalidOperationException("invalid or duplicate trip row");
                    loaded.Add(row.Worker, row);
                }
                foreach (var pair in loaded) rows.Add(pair.Key, pair.Value);
            }
            catch (Exception ex)
            {
                Blocked = true;
                BlockReason = ex.Message;
                rows.Clear(); // Retain the raw payload, never manufacture valid evidence from a broken row.
            }
        }
        private static bool Valid(Row row)
        {
            return row != null && Stable(row.Worker) && Stable(row.Contract) && Stable(row.Target)
                && row.Order == "Haul" && row.WorkType == "Hauling" && row.Job >= 0 && row.Map >= 0
                && row.Expected > 0 && row.Delivered >= 0 && row.Delivered <= row.Expected;
        }
        private static bool Stable(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 256;
    }
}
