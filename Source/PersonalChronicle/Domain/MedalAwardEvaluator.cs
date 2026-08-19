using System.Collections.Generic;
using PersonalChronicle.Domain.Honor;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 勋章判定引擎（Domain 纯逻辑，零副作用）。阶段一范围：kind=Threshold 且
    /// ownerType=Pawn 的阈值类勋章。调用方负责把 <see cref="MedalEvaluationResult.NewAwards"/>
    /// 写入 <see cref="PawnObject.AddGrantedMedal"/>（本类不写任何持久化字段）。
    ///
    /// 对齐 §6.9 等级规则：授予记录为 append-only 历史（青铜→银→金逐级追加），
    /// 「只显示当前档」由 UI/ReadModel 按称号取最高已达档位处理，本类不淘汰低档。
    /// </summary>
    public static class MedalAwardEvaluator
    {
        /// <summary>
        /// 判定某殖民者全部阈值勋章（从 DefDatabase 取定义，运行期入口）。
        /// </summary>
        public static MedalEvaluationResult EvaluatePawn(PawnObject pawn)
        {
            List<MedalDef> defs = DefDatabase<MedalDef>.AllDefsListForReading;
            return EvaluatePawn(pawn, defs);
        }

        /// <summary>
        /// 判定入口（可注入 defs，供 NUnit 纯逻辑测试；测试环境无 DefDatabase）。
        /// </summary>
        public static MedalEvaluationResult EvaluatePawn(PawnObject pawn, IEnumerable<MedalDef> defs)
        {
            MedalEvaluationResult result = new MedalEvaluationResult();
            if (pawn == null || defs == null)
            {
                return result;
            }
            foreach (MedalDef def in defs)
            {
                if (def == null)
                {
                    continue;
                }
                if (def.kind == MedalKind.Threshold && def.ownerType == MedalOwner.Pawn)
                {
                    double value;
                    bool applicable = TryReadPawnMetric(pawn, def.metricKey, out value);
                    bool met = applicable && value >= (double)def.threshold;
                    bool granted = ContainsDefName(pawn.GrantedMedals, def.defName);
                    result.Items.Add(new MedalEvaluation
                    {
                        Def = def,
                        CurrentValue = value,
                        IsApplicable = applicable,
                        IsMet = met,
                        IsGranted = granted,
                        IsNewAward = met && !granted
                    });
                }
                else if (def.kind == MedalKind.Achievement && def.ownerType == MedalOwner.Pawn)
                {
                    // P8 扩展路径（D-M1）：读 CareerEvent 聚合成就键，不破坏 Threshold 路径。
                    Dictionary<string, double> achievements = AchievementEvaluator.Aggregate(pawn);
                    double value = achievements.TryGetValue(def.achievementKey, out double v) ? v : 0.0;
                    bool applicable = !string.IsNullOrEmpty(def.achievementKey);
                    bool met = applicable && value >= (double)def.achievementThreshold;
                    bool granted = ContainsDefName(pawn.GrantedMedals, def.defName);
                    result.Items.Add(new MedalEvaluation
                    {
                        Def = def,
                        CurrentValue = value,
                        IsApplicable = applicable,
                        IsMet = met,
                        IsGranted = granted,
                        IsNewAward = met && !granted
                    });
                }
                // 其他 kind（Rank / Thing 类）本阶段不处理，沿用既有行为跳过。
            }
            result.Items.Sort(delegate (MedalEvaluation a, MedalEvaluation b)
            {
                return a.Def.order.CompareTo(b.Def.order);
            });
            return result;
        }

        /// <summary>
        /// 把 medalDef.metricKey 翻译为 PawnObject 累计值。返回 false 表示该
        /// metricKey 当前阶段不支持（阶段二 Thing/工坊聚合键），该勋章视为不可判定。
        /// </summary>
        public static bool TryReadPawnMetric(PawnObject pawn, string metricKey, out double value)
        {
            value = 0.0;
            if (pawn == null || string.IsNullOrEmpty(metricKey))
            {
                return false;
            }
            switch (metricKey)
            {
                case MedalMetricKeys.WorkTime:
                    value = pawn.WorkTime != null ? (double)pawn.WorkTime.TotalWorkTicks : 0.0;
                    return true;
                case MedalMetricKeys.ProductionQuantity:
                    value = pawn.Production != null ? (double)pawn.Production.TotalQuantity : 0.0;
                    return true;
                case MedalMetricKeys.ProductionSilver:
                    value = pawn.Production != null ? (double)pawn.Production.TotalMarketValue : 0.0;
                    return true;
                case MedalMetricKeys.Kills:
                    value = (double)(pawn.MeleeKills + pawn.RangedKills);
                    return true;
                case MedalMetricKeys.DamageDealt:
                    value = (double)pawn.DamageDealtTotal;
                    return true;
                case MedalMetricKeys.ParticipatedBattles:
                    value = (double)pawn.ParticipatedBattles;
                    return true;
                case MedalMetricKeys.ConsumptionSilver:
                    value = pawn.Consumption != null ? (double)pawn.Consumption.TotalSilver : 0.0;
                    return true;
                default:
                    return false;
            }
        }

        private static bool ContainsDefName(List<string> defNames, string defName)
        {
            if (defNames == null || string.IsNullOrEmpty(defName))
            {
                return false;
            }
            return defNames.Contains(defName);
        }
    }

    /// <summary>
    /// 单枚勋章的判定结果（运行期派生，不持久化）。IsNewAward = 达标且尚未授予，
    /// 是调用方应追加到 GrantedMedals 的唯一条件。
    /// </summary>
    public sealed class MedalEvaluation
    {
        public MedalDef Def;
        public double CurrentValue;
        /// <summary>ownerType=Pawn 且 metricKey 已被当前阶段支持。</summary>
        public bool IsApplicable;
        public bool IsMet;
        public bool IsGranted;
        public bool IsNewAward;
    }

    /// <summary>一次 EvaluatePawn 的完整结果集合（按 MedalDef.order 升序）。</summary>
    public sealed class MedalEvaluationResult
    {
        public List<MedalEvaluation> Items = new List<MedalEvaluation>();

        /// <summary>达标且未授予的勋章（待写入 GrantedMedals）。</summary>
        public List<MedalEvaluation> NewAwards
        {
            get
            {
                List<MedalEvaluation> list = new List<MedalEvaluation>();
                for (int i = 0; i < Items.Count; i++)
                {
                    if (Items[i].IsNewAward)
                    {
                        list.Add(Items[i]);
                    }
                }
                return list;
            }
        }
    }
}
