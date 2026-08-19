using System;
using System.Collections.Generic;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 专业技能 XP 纯函数（V2.0 §7 / P2-A §5）。Domain 层，无 Verse/DefDatabase 依赖，可离线单测。
    ///
    /// 公式：ProfessionalXP(单次实践) = 基础实践值 × Recipe相关度 × 品质系数 × 难度系数 × 数量系数
    /// 等级曲线：L(n) = maxLevel × (1 − (1 − xp/xpCap)^0.4) —— 前期快、后期慢（V2.0 §13 软上限哲学）。
    /// </summary>
    public static class ProfessionalXpEvaluator
    {
        /// <summary>
        /// 品质系数表（V2.0 §7 示例）。P3 治理：优先从 <see cref="ProfessionalXpPolicyDef"/> 数据表读取
        /// （<see cref="QualityMultiplier(string, IReadOnlyList{QualityXpEntry})"/>），本方法为内建兜底表，
        /// 仅在未提供 Def 表或无匹配条目时使用（向后兼容）。
        /// </summary>
        public static float QualityMultiplier(string qualityName)
        {
            if (string.IsNullOrEmpty(qualityName))
            {
                return 1f;
            }
            switch (qualityName)
            {
                case "Legendary": return 5f;
                case "Masterwork": return 3f;
                case "Excellent": return 1.5f;
                case "Good": return 1.2f;
                default: return 1f; // Normal/Awful/Poor/Shoddy 等
            }
        }

        /// <summary>
        /// 品质系数表（数据驱动）：优先在 Def 表（<see cref="ProfessionalXpPolicyDef.qualityMultipliers"/>）
        /// 查找匹配条目；无表/无匹配回退内建表。Domain 纯函数（entries 由调用方注入，不依赖 DefDatabase）。
        /// </summary>
        public static float QualityMultiplier(string qualityName, IReadOnlyList<QualityXpEntry> entries)
        {
            if (!string.IsNullOrEmpty(qualityName) && entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    QualityXpEntry e = entries[i];
                    if (e != null && string.Equals(e.qualityName, qualityName, System.StringComparison.Ordinal))
                    {
                        return e.multiplier;
                    }
                }
            }
            return QualityMultiplier(qualityName);
        }

        /// <summary>
        /// 单次实践 XP 总额。
        /// </summary>
        /// <param name="baseValue">ProfessionalSkillDef.xpPerPracticeBase。</param>
        /// <param name="recipeRelevance">0.0~1.0（白名单匹配=1.0 / 仅 WorkType 匹配=0.5 / 无匹配=0）。</param>
        /// <param name="qualityMultiplier">品质系数（QualityMultiplier）。</param>
        /// <param name="difficulty">难度系数（第一版 1.0）。</param>
        /// <param name="quantity">产出数量（≥1，上限 4）。</param>
        public static float ComputePracticeXp(
            float baseValue, float recipeRelevance, float qualityMultiplier,
            float difficulty, int quantity)
        {
            if (baseValue <= 0f)
            {
                return 0f;
            }
            if (recipeRelevance <= 0f)
            {
                return 0f;
            }
            float q = quantity < 1 ? 1f : (quantity > 4 ? 4f : quantity);
            float d = difficulty <= 0f ? 1f : difficulty;
            float qm = qualityMultiplier <= 0f ? 1f : qualityMultiplier;
            float rel = recipeRelevance > 1f ? 1f : recipeRelevance;
            return baseValue * rel * qm * d * q;
        }

        /// <summary>
        /// XP → Level（边际递减）。返回 [0, maxLevel] 整数等级。
        /// </summary>
        public static int LevelFromXp(float xp, int maxLevel, float xpCap)
        {
            if (maxLevel <= 0)
            {
                return 0;
            }
            if (xp <= 0f || xpCap <= 0f)
            {
                return 0;
            }
            float t = Math.Min(1f, xp / xpCap);
            double levelF = maxLevel * (1d - Math.Pow(1d - t, 0.4d));
            int level = (int)Math.Floor(levelF);
            return level < 0 ? 0 : (level > maxLevel ? maxLevel : level);
        }

        /// <summary>
        /// Level → Mastery（0~100）。第一版按 level/maxLevel 线性归一（levelCurve 可后续 Def 化覆写）。
        /// </summary>
        public static float MasteryFromLevel(int level, int maxLevel)
        {
            if (maxLevel <= 0 || level <= 0)
            {
                return 0f;
            }
            float m = (float)level / maxLevel * 100f;
            return m > 100f ? 100f : m;
        }
    }
}
