using Verse;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 专业评级档位 Def（V2.0 §13 / P4-A）。
    /// 评级由专业能力 Level 经软上限派生（无考试门槛，对齐「评级≠资格」），
    /// 提供少量额外效果权重，作用于 P3 已落地的 ProfessionalEffectDef.value。
    ///
    /// 软上限/递减（V2.0 §13 红线）：顶档（大师）后即使 Level 继续涨也不再增，
    /// 避免数值失控。
    /// </summary>
    public sealed class ProfessionalRatingDef : Def
    {
        /// <summary>触发本评级所需的最低专业能力 Level（含）。</summary>
        public int minLevel = 1;

        /// <summary>制造速度额外乘算权重（叠加模式：最终 = P3.value × (1 + workSpeedWeight)）。
        /// 仅当技能拥有 WorkSpeed 效果时生效。0 = 无额外速度加成。</summary>
        public float workSpeedWeight = 0f;

        /// <summary>品质偏置额外乘算权重（叠加模式：最终档位 = P3.value × (1 + qualityBiasWeight) 取整）。
        /// 仅当技能拥有 QualityBias 效果时生效。0 = 无额外品质加成。</summary>
        public float qualityBiasWeight = 0f;

        /// <summary>展示排序（越小越高级，用于取「最高档」时比较）。</summary>
        public int order;
    }
}
