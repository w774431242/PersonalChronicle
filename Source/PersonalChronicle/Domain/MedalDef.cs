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
        Rank,
        /// <summary>
        /// P8 成就类：读取 <see cref="AchievementEvaluator"/> 聚合的成就键
        /// （CareerEvent 事实派生），匹配 achievementKey + threshold（D-M1 扩展路径，
        /// 不破坏现有 Threshold/Rank）。
        /// </summary>
        Achievement
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
        /// Achievement medals (kind=Achievement)：绑定的成就键（由
        /// <see cref="AchievementEvaluator"/> 从 CareerEvent 事实聚合，如 "LegendaryMade"）。
        /// </summary>
        public string achievementKey;

        /// <summary>Achievement medals：成就键所需达到的累计值门槛。</summary>
        public float achievementThreshold;

        /// <summary>
        /// Optional reference to a MedalBuffDef. Empty = display-only medal
        /// with no buff channel.
        /// </summary>
        public string buffDefName;

        /// <summary>
        /// Optional texture path relative to <c>Textures/</c> (e.g. "Medals/LaborModel")
        /// for the medal wall icon. Empty = UI falls back to a tier-coloured
        /// placeholder tile. Data-driven so other mods can ship art via XML.
        /// </summary>
        public string iconPath;

        /// <summary>Display ordering (ascending).</summary>
        public int order;

        /// <summary>
        /// defName（<c>Medal_&lt;系&gt;_&lt;称号&gt;_&lt;Tier&gt;</c>，下划线分隔）去掉 tier
        /// 后缀 → 称号分组键（如 Medal_Labor_Model）。供 ReadModel 归并（只显示当前档）
        /// 与授勋公告（无材质称号翻译键）共用，避免各自复制解析逻辑。
        /// 按已知 <see cref="MedalTier"/> 后缀识别，不再依赖分隔符（defName 用下划线）。
        /// </summary>
        public static string SeriesKeyOf(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            // Tier 后缀长度固定（Bronze=6 / Silver=6 / Gold=4），且 defName 以
            // "_<Tier>" 结尾。倒序匹配避免称号名恰好含 tier 子串。
            if (defName.EndsWith("_Gold"))
            {
                return defName.Substring(0, defName.Length - "_Gold".Length);
            }
            if (defName.EndsWith("_Bronze") || defName.EndsWith("_Silver"))
            {
                return defName.Substring(0, defName.Length - "_Bronze".Length);
            }
            return defName;
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
