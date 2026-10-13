using System;
using System.Collections.Generic;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 专业技能 XP 纯函数（V2.0 §7 / P2-A §5）。Domain 层，无 Verse/DefDatabase 依赖，可离线单测。
    ///
    /// 公式：ProfessionalXP(单次实践) = 基础实践值 × 活动权重 × Recipe相关度 × 品质系数 × 难度系数 × 数量系数(衰减)
    /// 等级曲线（通用参数化，V2.0+ 推荐）：xpAtLevel(n) = xpCap × (n/maxLevel)^curvePower ——
    ///   升级成本递增（classic RPG），curvePower=1.6 温和递增；curvePower=1 线性；curvePower&lt;1 前期快。
    /// 兼容旧曲线：LevelFromXp(xp, maxLevel, xpCap) 3 参保留 V2.0 §7 边际递减公式
    ///   L = maxLevel × (1 − (1 − xp/xpCap)^0.4)（仅供迁移/旧逻辑）。
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
        /// <param name="quantity">产出数量（≥1，上限 4；按 q^0.8 衰减，削弱批量刷级）。</param>
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
            // 数量系数衰减 q^0.8：×4 批量不再 ×4 收益，抑制"专做批量配方刷级"。
            // 全程 double 运算、仅最终结果一次 float 舍入（与单测基准 (float)(10f*Math.Pow(n,0.8)) 口径一致，避免 1-ULP 误差）。
            return (float)((double)baseValue * rel * qm * d * Math.Pow(q, 0.8d));
        }

        /// <summary>
        /// 通用活动→XP 折算（V2.0+ 通用通道）：任意职业行为统一入口。
        /// 制造仍走 <see cref="ComputePracticeXp"/>（含相关度/品质），其他活动以本函数兜底。
        /// </summary>
        /// <param name="baseValue">ProfessionalSkillDef.xpPerPracticeBase。</param>
        /// <param name="activityWeight">活动类型权重（Work/Research/Construct/Author/Harvest，来自 ProfessionalXpPolicyDef）。</param>
        /// <param name="relevance">活动与技能相关度 0~1。</param>
        /// <param name="qualityMultiplier">品质系数（无品质=1）。</param>
        /// <param name="difficulty">难度系数。</param>
        public static float ComputeActivityXp(
            float baseValue, float activityWeight, float relevance,
            float qualityMultiplier, float difficulty)
        {
            if (baseValue <= 0f || activityWeight <= 0f || relevance <= 0f)
            {
                return 0f;
            }
            float rel = relevance > 1f ? 1f : relevance;
            float d = difficulty <= 0f ? 1f : difficulty;
            float qm = qualityMultiplier <= 0f ? 1f : qualityMultiplier;
            return baseValue * activityWeight * rel * qm * d;
        }

        /// <summary>
        /// 通用参数化曲线：给定等级 n，返回累计 XP 阈值。
        /// xpAtLevel(n) = xpCap × (n/maxLevel)^curvePower。n=0 → 0；n=maxLevel → xpCap。
        /// </summary>
        public static float XpAtLevel(int level, int maxLevel, float xpCap, float curvePower)
        {
            if (maxLevel <= 0 || xpCap <= 0f || level <= 0)
            {
                return 0f;
            }
            if (level >= maxLevel)
            {
                return xpCap;
            }
            float p = curvePower <= 0f ? 1f : curvePower;
            float t = (float)level / maxLevel;
            return xpCap * (float)Math.Pow(t, p);
        }

        /// <summary>
        /// 升级所需 XP：从 level 升到 level+1 的缺口。
        /// need(n) = xpAtLevel(n+1) − xpAtLevel(n)；满级返回 0。
        /// </summary>
        public static float NeedForNext(int level, int maxLevel, float xpCap, float curvePower)
        {
            if (maxLevel <= 0 || level < 0 || level >= maxLevel)
            {
                return 0f;
            }
            float next = XpAtLevel(level + 1, maxLevel, xpCap, curvePower);
            float cur = XpAtLevel(level, maxLevel, xpCap, curvePower);
            return Math.Max(0f, next - cur);
        }

        /// <summary>
        /// 通用参数化曲线反函数：XP → Level（升级成本递增，curvePower&gt;1 时越顶级越难）。
        /// 返回 [0, maxLevel] 整数等级。
        /// </summary>
        public static int LevelFromXp(float xp, int maxLevel, float xpCap, float curvePower)
        {
            if (maxLevel <= 0)
            {
                return 0;
            }
            if (xp <= 0f || xpCap <= 0f)
            {
                return 0;
            }
            float p = curvePower <= 0f ? 1f : curvePower;
            float t = Math.Min(1f, xp / xpCap);
            double levelF = maxLevel * Math.Pow(t, 1d / p);
            int level = (int)Math.Floor(levelF);
            return level < 0 ? 0 : (level > maxLevel ? maxLevel : level);
        }

        /// <summary>
        /// 通用参数化曲线：距下一等级还差多少 XP（基于真实曲线，非线性近似）。
        /// </summary>
        public static float XpToNext(float currentXp, int maxLevel, float xpCap, float curvePower)
        {
            if (maxLevel <= 0 || xpCap <= 0f)
            {
                return 0f;
            }
            int cur = LevelFromXp(currentXp, maxLevel, xpCap, curvePower);
            if (cur >= maxLevel)
            {
                return 0f;
            }
            float need = NeedForNext(cur, maxLevel, xpCap, curvePower);
            float atCur = XpAtLevel(cur, maxLevel, xpCap, curvePower);
            return Math.Max(0f, need - (currentXp - atCur));
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
