using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Kind of medal evaluation. Threshold medals grant automatically once a
    /// cumulative metric passes the threshold; Rank medals require a streak of
    /// top-N placements across settlement periods (phase 2).
    /// </summary>
    public enum MedalKind
    {
        Threshold,
        Rank
    }

    /// <summary>
    /// Carrier the medal is attached to. Pawn = colonist archive records,
    /// Workplace = a workbench instance (aggregated usage), Thing = an
    /// equipment legacy chain.
    /// </summary>
    public enum MedalOwner
    {
        Pawn,
        Workplace,
        Thing
    }

    /// <summary>
    /// Material tier stacked onto a title. Bronze/Silver/Gold follow an
    /// escalating threshold ladder; evaluation shows only the highest tier
    /// reached, never all three at once.
    /// </summary>
    public enum MedalTier
    {
        Bronze,
        Silver,
        Gold
    }

    /// <summary>
    /// Data-driven medal definition. The evaluator only reads kind/ownerType/
    /// metricKey/threshold; labels and descriptions are derived from the
    /// defName via UI.Medal.&lt;defName&gt;.Label/.Desc so another mod can add
    /// medals purely through XML + translation keys.
    /// </summary>
    public sealed class MedalDef : Def
    {
        /// <summary>Threshold or Rank evaluation.</summary>
        public MedalKind kind;

        /// <summary>Carrier type: Pawn / Workplace / Thing.</summary>
        public MedalOwner ownerType;

        /// <summary>Material tier stacked onto the title (Bronze/Silver/Gold).</summary>
        public MedalTier tier;

        /// <summary>
        /// Metric binding consumed by the evaluator (workTime / productionQuantity /
        /// productionSilver / kills / damageDealt / participatedBattles /
        /// consumptionSilver / heirloomHolders / legacyKills). PawnObject fields
        /// for threshold medals, aggregation keys for workplace medals.
        /// </summary>
        public string metricKey;

        /// <summary>Threshold medals: the cumulative value that must be reached.</summary>
        public float threshold;

        /// <summary>Rank medals: top-N placements counted as a qualifying period.</summary>
        public int rankTopN = 3;

        /// <summary>Rank medals: consecutive qualifying periods required.</summary>
        public int streakPeriods = 3;

        /// <summary>
        /// Optional reference to a MedalBuffDef. Empty = display-only medal
        /// with no buff channel.
        /// </summary>
        public string buffDefName;

        /// <summary>Display ordering (ascending).</summary>
        public int order;

        /// <summary>
        /// defName（<c>Medal.&lt;系&gt;.&lt;称号&gt;.&lt;Tier&gt;</c>）去掉 tier 后缀 →
        /// 称号分组键（如 Medal.Labor.Model）。供 ReadModel 归并（只显示当前档）与
        /// 授勋公告（无材质称号翻译键）共用，避免各自复制解析逻辑。
        /// </summary>
        public static string SeriesKeyOf(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            int idx = defName.LastIndexOf('.');
            return idx > 0 ? defName.Substring(0, idx) : defName;
        }
    }

    /// <summary>
    /// Buff attached to a medal via MedalDef.buffDefName. Dual channel:
    /// channel A injects a vanilla StatDef offset (phase 3, requires reflection
    /// verification); channel B only shows the displayBonus text in the archive
    /// UI. An empty statDefName means display-only — the intended phase-1 state.
    /// </summary>
    public sealed class MedalBuffDef : Def
    {
        /// <summary>Vanilla StatDef defName for channel A injection. Empty = not injected.</summary>
        public string statDefName;

        /// <summary>Additive offset applied to the stat (e.g. 0.03 = +3%).</summary>
        public float statOffset;

        /// <summary>Channel B display bonus text key (UI.Medal.Buff.*).</summary>
        public string displayBonus;
    }
}
