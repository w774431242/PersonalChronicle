using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using Verse;

namespace PersonalChronicle.Data.PawnEconomy
{
    internal sealed class WorkPriceSaveData : IExposable
    {
        public const int MaximumRows = 512;
        private bool supported = true;
        private string payload;
        private List<WorkPriceRow> rows = new List<WorkPriceRow>();
        public bool Supported => supported;
        public long Read(string anchor, string work, string order = null)
        {
            if (!Supported) return 0;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Anchor == anchor && rows[i].Work == work && rows[i].Order == (order ?? "")) return rows[i].Minor;
            return 0;
        }
        public bool Set(string anchor, string work, string order, long minor)
        {
            if (!Supported || string.IsNullOrWhiteSpace(anchor) || string.IsNullOrWhiteSpace(work) || !WorkPriceRules.Valid(minor)) return false;
            order = order ?? "";
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Anchor == anchor && rows[i].Work == work && rows[i].Order == order)
                { if (minor == 0) rows.RemoveAt(i); else rows[i].Minor = minor; return true; }
            if (minor == 0) return true;
            if (rows.Count >= MaximumRows) return false;
            rows.Add(new WorkPriceRow { Anchor = anchor, Work = work, Order = order, Minor = minor });
            return true;
        }
        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && Supported)
            {
                var root = new XElement("WorkPrices", new XAttribute("version", 1));
                foreach (var row in rows) root.Add(new XElement("quote", new XAttribute("anchor", row.Anchor),
                    new XAttribute("work", row.Work), new XAttribute("order", row.Order), new XAttribute("minor", row.Minor)));
                payload = root.ToString(SaveOptions.DisableFormatting);
            }
            Scribe_Values.Look(ref payload, "payload");
            if (Scribe.mode != LoadSaveMode.LoadingVars) return;
            rows = new List<WorkPriceRow>(); supported = true;
            if (string.IsNullOrEmpty(payload)) return;
            try
            {
                using (var reader = XmlReader.Create(new StringReader(payload), new XmlReaderSettings
                    { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1000000 }))
                {
                    var root = XElement.Load(reader);
                    if (root.Name != "WorkPrices" || (int?)root.Attribute("version") != 1) { supported = false; return; }
                    foreach (var quote in root.Elements("quote"))
                    {
                        string anchor = (string)quote.Attribute("anchor"), work = (string)quote.Attribute("work"), order = (string)quote.Attribute("order") ?? "";
                        long minor = (long?)quote.Attribute("minor") ?? 0L;
                        if (!Set(anchor, work, order, minor)) throw new FormatException("InvalidWorkQuote");
                    }
                }
            }
            catch (Exception) { rows.Clear(); supported = false; }
            // Future or malformed payload stays byte-for-byte intact on resave; writes fail closed.
        }
    }
    internal sealed class WorkPriceRow
    {
        public string Anchor, Work, Order = "";
        public long Minor;
    }
}
