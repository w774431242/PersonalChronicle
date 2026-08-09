using System.Collections.Generic;
using PersonalChronicle.Application;

namespace PersonalChronicle.Api.DomainProviders.Builtin
{
    /// <summary>
    /// Built-in battle evaluator (P2). Judges significance purely from the supplied
    /// <see cref="BattleAppraisalInput.DataKeys"/> (data keys only — no localized
    /// text), so it needs no global state. Third-party mods register a
    /// higher-priority <see cref="IBattleProvider"/> to override the verdict.
    /// </summary>
    public sealed class BuiltinBattleProvider : IBattleProvider
    {
        private readonly IArchiveService service;

        public BuiltinBattleProvider(IArchiveService service)
        {
            this.service = service;
        }

        public string ProviderId { get { return "PersonalChronicle.Builtin.Battle"; } }
        public int Priority { get { return 0; } }
        public string ContractVersion { get { return "1"; } }
        public IReadOnlyCollection<string> Capabilities
        {
            get { return new List<string> { BattleCapabilities.Battle }; }
        }

        public bool TryAppraise(BattleAppraisalInput input, out BattleAppraisal appraisal)
        {
            appraisal = BattleAppraisal.Empty;
            if (input == null || string.IsNullOrEmpty(input.BattleId))
            {
                return false;
            }
            // Significant when the engagement resolved to a decisive (non-trivial)
            // outcome. The active-battle check is intentionally left to higher-priority
            // providers / the canonical service; the built-in only classifies on keys.
            string outcome = TryGet(input.DataKeys, "outcome");
            bool significant = !string.IsNullOrEmpty(outcome)
                && outcome != "trivial" && outcome != "skirmish";
            int weight = significant ? 1 : 0;
            appraisal = new BattleAppraisal(isDefined: true, isSignificant: significant, weight: weight);
            return true;
        }

        private static string TryGet(IReadOnlyDictionary<string, string> map, string key)
        {
            if (map == null || key == null)
            {
                return null;
            }
            return map.TryGetValue(key, out string v) ? v : null;
        }
    }
}
