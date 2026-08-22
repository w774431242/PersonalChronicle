using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// Data-driven marker attached to IncidentDefs (via PatchOperation, never by
    /// overwriting vanilla defs) that flags an incident as "battle-grade".
    /// The capture patch forwards only incidents whose extension isBattle == true.
    /// No defName string comparison anywhere — the judgment lives in Def data.
    /// </summary>
    public class IncidentBattleExtension : DefModExtension
    {
        /// <summary>True when this incident should open a Battle archive object.</summary>
        public bool isBattle;
    }
}
