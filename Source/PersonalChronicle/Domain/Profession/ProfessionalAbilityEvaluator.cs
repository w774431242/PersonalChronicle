using System.Collections.Generic;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 专业能力成长纯函数（V2.0 §8 / P2-A §6）。Domain 层，可离线单测。
    /// 每次实践按「行为→能力映射矩阵」把技能 XP 按权重拆分到各能力维度独立累加。
    /// </summary>
    public static class ProfessionalAbilityEvaluator
    {
        /// <summary>
        /// 将一次实践的技能 XP 总额按权重表拆分到各能力维度。
        /// 返回 skillDefName → abilityXp 增量的字典（未归一化的原始权重，直接乘 totalXp）。
        /// 权重和可 >100（加权）；无权重项时返回空（不分配）。
        /// </summary>
        public static Dictionary<string, float> SplitAbilityXp(float totalXp, List<AbilityXpWeight> weights)
        {
            var result = new Dictionary<string, float>();
            if (totalXp <= 0f || weights == null || weights.Count == 0)
            {
                return result;
            }
            float weightSum = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] != null && !string.IsNullOrEmpty(weights[i].abilityKey))
                {
                    weightSum += weights[i].weight;
                }
            }
            if (weightSum <= 0f)
            {
                return result;
            }
            for (int i = 0; i < weights.Count; i++)
            {
                AbilityXpWeight w = weights[i];
                if (w == null || string.IsNullOrEmpty(w.abilityKey))
                {
                    continue;
                }
                result[w.abilityKey] = totalXp * (w.weight / weightSum);
            }
            return result;
        }
    }
}
