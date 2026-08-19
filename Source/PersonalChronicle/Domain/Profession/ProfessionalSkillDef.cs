using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 专业技能 Def（V2.0 §4 核心 / P2-A §3.2）。
    /// 方向内的可成长技能（如 精密制造），由原版 Skill/WorkType/Job/Recipe/Research 持续提供成长来源。
    /// 数据驱动铁律（V2.0 §4）：C# 端禁止 if (skill == "ProfessionalSkill_...")。
    /// </summary>
    public sealed class ProfessionalSkillDef : Def
    {
        // —— 定位 ——

        /// <summary>一级专业稳定键（如 "Manufacturing"）。</summary>
        public string profession;

        /// <summary>二级方向稳定键（ProfessionalDirectionDef.defName）。</summary>
        public string direction;

        // —— 证据来源 ——

        /// <summary>原版技能 defName 列表（Crafting/Intellectual…），方向评分的技能证据。</summary>
        public List<string> sourceSkills = new List<string>();

        /// <summary>可提供实践的原版 RecipeDef 白名单（空 = 全部相关配方）。</summary>
        public List<string> practiceRecipeDefNames = new List<string>();

        /// <summary>可提供实践的原版 WorkTypeDef 白名单（空 = 不限制）。</summary>
        public List<string> practiceWorkTypeDefNames = new List<string>();

        // —— 成长 ——

        /// <summary>基础实践值（单次默认，如 10）。</summary>
        public float xpPerPracticeBase = 10f;

        /// <summary>满级所需经验值（XP→Level 曲线参数，边际递减，V2.0 §7）。</summary>
        public float xpCap = 5000f;

        /// <summary>难度系数（V2.0 §7 难度项；第一版固定 1.0，留 Def 扩展位）。</summary>
        public float xpDifficulty = 1f;

        /// <summary>等级软上限（默认 50，配合评级软上限 V2.0 §13）。</summary>
        public int maxLevel = 50;

        // —— 能力与效果 ——

        /// <summary>该技能贡献的能力维度 key（加工/精度/工艺/材料/质控）。</summary>
        public List<string> abilityKeys = new List<string>();

        /// <summary>声明拥有的效果 Def（ProfessionalEffectDef.defName）。</summary>
        public List<string> effectDefNames = new List<string>();

        // —— 资格 ——

        /// <summary>资格标签（未来 P4/P5 用，如 "ManufacturingAdvanced"）。</summary>
        public List<string> qualificationTags = new List<string>();

        // —— 差异化特化点（2026-08-19） ——

        /// <summary>
        /// 技能级效果数值覆盖。ProfessionalEffectDef 为全局共享定义（value 对所有引用技能一致），
        /// 本覆盖让各技能在共享效果之上拥有**差异化数值与评级权重缩放**，打破"共享 Def → 数值雷同"
        /// 的结构限制（二级方向差异化特化点机制，见 P2-A §9 数据蓝图）。
        /// append-only Def 字段，不影响存档。
        /// </summary>
        public List<EffectOverride> effectOverrides = new List<EffectOverride>();
    }

    /// <summary>
    /// 技能级效果覆盖项：对共享 ProfessionalEffectDef 的数值/评级权重做技能级差异化。
    /// - <see cref="hasValue"/> = true 时以 <see cref="value"/> 替代 ProfessionalEffectDef.value；
    /// - <see cref="ratingWeightScale"/> 缩放该效果上的评级权重（1.0 = 不缩放；0 = 评级不加成；
    ///   2 = 双倍评级加成）。
    /// 全字段缺省 = 不覆盖（回退共享值），保证既有技能零影响。
    /// </summary>
    public sealed class EffectOverride
    {
        /// <summary>被覆盖的 ProfessionalEffectDef.defName。</summary>
        public string effectDefName;

        /// <summary>是否覆盖共享 value（true 时用 <see cref="value"/> 替代 Def 值）。</summary>
        public bool hasValue;

        /// <summary>覆盖值（hasValue=true 时生效；WorkSpeed 为乘算修正量如 0.05=+5%，QualityBias 为整档偏移）。</summary>
        public float value;

        /// <summary>评级权重缩放系数（默认 1.0）。</summary>
        public float ratingWeightScale = 1f;
    }
}
