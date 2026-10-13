using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using PersonalChronicle.Domain.PawnEconomy;
using Verse;

namespace PersonalChronicle.Data
{
    public sealed class PawnEconomyEnvelope
    {
        public int Schema = 1, Revision;
        public bool Enabled, Funded;
        public long Sequence;
        [XmlArrayItem(typeof(EconomyAccount)), XmlArrayItem(typeof(EconomyPerson)),
         XmlArrayItem(typeof(EconomyInstitution)), XmlArrayItem(typeof(EconomyPosition)),
         XmlArrayItem(typeof(EconomyContract)), XmlArrayItem(typeof(EconomyOrder)),
         XmlArrayItem(typeof(EconomyReceipt)), XmlArrayItem(typeof(EconomyReport)), XmlArrayItem(typeof(EconomyOwnership))]
        public List<EconomyRecord> Records = new List<EconomyRecord>();
    }

    /// <summary>Versioned primitive payload; unsupported or damaged payloads are preserved verbatim.</summary>
    public sealed class PawnEconomySaveData : IExposable
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(PawnEconomyEnvelope));
        public EconomyState State = new EconomyState();
        private string payload;
        // Read only during LoadingVars: never re-encode this as a purported pre-recovery backup.
        internal string OriginalPayload => payload;
        public static string Encode(EconomyState state)
        {
            var dto = new PawnEconomyEnvelope { Schema = state.Schema, Revision = state.Revision,
                Enabled = state.Enabled, Funded = state.Funded, Sequence = state.Sequence, Records = state.Records };
            using (var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture))
            { Serializer.Serialize(writer, dto); return writer.ToString(); }
        }
        public static EconomyState Decode(string text)
        {
            if (string.IsNullOrEmpty(text)) return new EconomyState();
            try
            {
                using (var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings
                    { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16000000 }))
                {
                    var dto = (PawnEconomyEnvelope)Serializer.Deserialize(reader);
                    var state = new EconomyState { Schema = dto.Schema, Revision = dto.Revision,
                        Enabled = dto.Enabled, Funded = dto.Funded, Sequence = dto.Sequence };
                    state.Records.AddRange(dto.Records ?? new List<EconomyRecord>());
                    if (dto.Schema > EconomyState.CurrentSchema) state.Fault = "UnsupportedSchema";
                    return state;
                }
            }
            catch (Exception) { return new EconomyState { Fault = "InvalidPayload" }; }
        }
        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && State.Ready) payload = Encode(State);
            Scribe_Values.Look(ref payload, "payload");
            if (Scribe.mode == LoadSaveMode.LoadingVars) State = Decode(payload);
        }
    }
}
