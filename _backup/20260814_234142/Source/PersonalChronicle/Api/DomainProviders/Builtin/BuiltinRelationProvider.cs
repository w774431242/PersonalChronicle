using System.Collections.Generic;
using PersonalChronicle.Application;

namespace PersonalChronicle.Api.DomainProviders.Builtin
{
    /// <summary>
    /// Built-in relation evaluator (P2). Judges significance purely from the supplied
    /// <see cref="RelationAppraisalInput.DataKeys"/>, so it needs no global state.
    /// Third-party mods register a higher-priority <see cref="IRelationProvider"/> to
    /// override the verdict.
    /// </summary>
    public sealed class BuiltinRelationProvider : IRelationProvider
    {
        private readonly IArchiveService service;

        public BuiltinRelationProvider(IArchiveService service)
        {
            this.service = service;
        }

        public string ProviderId { get { return "PersonalChronicle.Builtin.Relation"; } }
        public int Priority { get { return 0; } }
        public string ContractVersion { get { return "1"; } }
        public IReadOnlyCollection<string> Capabilities
        {
            get { return new List<string> { RelationCapabilities.Relation }; }
        }

        public bool TryAppraise(RelationAppraisalInput input, out RelationAppraisal appraisal)
        {
            appraisal = RelationAppraisal.Empty;
            if (input == null || string.IsNullOrEmpty(input.A) || string.IsNullOrEmpty(input.B))
            {
                return false;
            }
            // Significant when the relation carries at least one named relationDef.
            string relationDefs = TryGet(input.DataKeys, "relationDefs");
            bool significant = !string.IsNullOrEmpty(relationDefs);
            appraisal = new RelationAppraisal(isDefined: true, isSignificant: significant);
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
