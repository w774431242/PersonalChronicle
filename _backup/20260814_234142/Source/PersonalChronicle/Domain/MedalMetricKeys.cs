namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Stable metric identifiers consumed by <see cref="MedalDef.metricKey"/>.
    /// Mirror of the §6 数据源映射 (workTime/productionQuantity/...). Centralizing
    /// here prevents bare string keys from leaking into evaluator/UI code.
    /// </summary>
    public static class MedalMetricKeys
    {
        /// <summary>累计工时（tick）→ <see cref="PawnObject.WorkTime.TotalWorkTicks"/>。</summary>
        public const string WorkTime = "workTime";

        /// <summary>累计产出件数 → <see cref="PawnObject.Production.TotalQuantity"/>。</summary>
        public const string ProductionQuantity = "productionQuantity";

        /// <summary>累计产出市场价值（银）→ <see cref="PawnObject.Production.TotalMarketValue"/>。</summary>
        public const string ProductionSilver = "productionSilver";

        /// <summary>累计击杀（近战+远程）→ MeleeKills + RangedKills。</summary>
        public const string Kills = "kills";

        /// <summary>累计对敌伤害 → <see cref="PawnObject.DamageDealtTotal"/>。</summary>
        public const string DamageDealt = "damageDealt";

        /// <summary>累计参与战役 → <see cref="PawnObject.ParticipatedBattles"/>。</summary>
        public const string ParticipatedBattles = "participatedBattles";

        /// <summary>累计消耗银 → <see cref="PawnObject.Consumption.TotalSilver"/>。</summary>
        public const string ConsumptionSilver = "consumptionSilver";

        /// <summary>阶段二（Thing 载体）：传承持有者任数 → ThingObject.HolderRecords。</summary>
        public const string HeirloomHolders = "heirloomHolders";

        /// <summary>阶段二（Thing 载体）：历代击杀 → ThingObject 传承链聚合。</summary>
        public const string LegacyKills = "legacyKills";
    }
}
