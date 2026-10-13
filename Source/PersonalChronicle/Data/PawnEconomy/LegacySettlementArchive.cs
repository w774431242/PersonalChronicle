using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;

namespace PersonalChronicle.Data.PawnEconomy
{
    /// <summary>Immutable original payload, not the legacy working state after recovery.</summary>
    internal sealed class LegacySettlementArchive
    {
        public string Raw { get; }
        public string Source { get; }
        public string Hash { get; }
        public string Fault { get; }
        public IReadOnlyList<LegacyCashClaim> Claims { get; }
        public LegacyReconciliationReport Report { get; }
        public LegacySettlementArchive(string raw, string source)
        {
            Raw = raw; Source = source;
            using (var sha = SHA256.Create()) Hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw ?? ""))).Replace("-", "");
            var claims = new List<LegacyCashClaim>(); Claims = claims.AsReadOnly();
            if (string.IsNullOrEmpty(raw)) return;
            try
            {
                // Unlike the legacy reader, explicit migration must not silently ignore new fields.
                var serializer = new XmlSerializer(typeof(PawnEconomyEnvelope)); bool unknown = false;
                serializer.UnknownNode += (sender, args) => unknown = true;
                using (var reader = XmlReader.Create(new StringReader(raw), new XmlReaderSettings
                { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16000000 }))
                    serializer.Deserialize(reader);
                if (unknown) { Fault = "UnknownLegacyFields"; return; }
                var state = PawnEconomySaveData.Decode(raw); Report = LegacyReconciler.Capture(state);
                if (!state.Ready || !Report.DiagnosticChecksPassed || Report.Truncated) { Fault = "LegacyDiagnosticsFailed"; return; }
                foreach (var account in state.All<EconomyAccount>().Where(x => x.Balance > 0))
                    claims.Add(new LegacyCashClaim(Hash + ":balance:" + account.Id.ToString(CultureInfo.InvariantCulture), account.Id, "Balance", account.Key, account.Balance));
                foreach (var wage in state.All<EconomyReceipt>().Where(x => x.Type == "Wage" && !x.Paid && x.Amount > 0))
                    claims.Add(new LegacyCashClaim(Hash + ":wage:" + wage.Id.ToString(CultureInfo.InvariantCulture), wage.Id, "UnpaidWage", wage.ToKey, wage.Amount));
            }
            catch (Exception error) when (!(error is OutOfMemoryException) && !(error is StackOverflowException))
            { claims.Clear(); Fault = "InvalidLegacyArchive:" + error.GetType().Name; }
        }
    }
}
