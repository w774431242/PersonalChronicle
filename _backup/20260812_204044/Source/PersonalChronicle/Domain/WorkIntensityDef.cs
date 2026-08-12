using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Data-driven display/evaluation tier for career work intensity. The
    /// evaluator only uses minimumDailyHours; labels and colors remain Def
    /// data so another mod can patch or add tiers without changing C#.
    /// </summary>
    public sealed class WorkIntensityTierDef : Def
    {
        public string tierKey;
        public string displayCode;
        public float minimumDailyHours;
        public string labelKey;
        public string tagKey;
        public string colorHex;
        public int order;
    }

    /// <summary>
    /// Global policy for work-intensity evaluation. The built-in policy is
    /// selected by this stable name; a missing policy uses safe defaults
    /// instead of throwing during a save load.
    /// </summary>
    public sealed class WorkIntensityPolicyDef : Def
    {
        public const string DefaultPolicyDefName = "PersonalChronicleWorkIntensityPolicy";

        public int minimumSampleDays = 5;
        public float overloadRatio = 1.5f;
        public float slackRatio = 0.5f;
        public List<string> tierDefNames = new List<string>();
    }
}
