using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 行为→能力映射的权重项（非 Def，随 AbilityMappingDef 持久化于 Def 数据）。
    /// </summary>
    public sealed class AbilityXpWeight
    {
        /// <summary>能力维度 key（machining/precisionControl/processKnowledge/...）。</summary>
        public string abilityKey;

        /// <summary>
        /// 该维度获得本次实践 XP 的比例（0~100；可 >100 表示加权，归一化后按 100 分配）。
        /// </summary>
        public float weight;
    }

    /// <summary>
    /// 行为→能力映射矩阵 Def（V2.0 §8 / P2-A §6.1）。
    /// 每次制造实践按 RecipeDef 命中映射，将技能 XP 按权重拆分到各能力维度独立累加。
    /// </summary>
    public sealed class AbilityMappingDef : Def
    {
        /// <summary>匹配的原版 RecipeDef（空 = 全部制造类）。</summary>
        public List<string> recipeDefNames = new List<string>();

        /// <summary>匹配的 WorkTypeDef（制造 = crafting）。</summary>
        public string workTypeDefName;

        /// <summary>各能力维度权重（和为 100）。</summary>
        public List<AbilityXpWeight> weights = new List<AbilityXpWeight>();

        /// <summary>映射语义键（如 "PrecisionMachinery"）。</summary>
        public string mappingKey;
    }
}
