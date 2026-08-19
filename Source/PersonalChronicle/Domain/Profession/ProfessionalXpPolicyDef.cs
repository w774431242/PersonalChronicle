using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 专业技能 XP 策略 Def（V2.0 §7 品质系数表 Def 化，P3 治理 P3 级硬编码标注）。
    /// 数据驱动铁律：品质系数不再硬编码在 C# switch，改由 XML 定义。
    /// <see cref="ProfessionalXpEvaluator.QualityMultiplier(string, IReadOnlyList{QualityXpEntry})"/>
    /// 优先消费本表；无匹配或未定义时回退内建表（向后兼容旧 Def 缺失场景）。
    /// </summary>
    public sealed class ProfessionalXpPolicyDef : Def
    {
        /// <summary>品质系数表（qualityName → multiplier，如 Legendary→5）。</summary>
        public List<QualityXpEntry> qualityMultipliers = new List<QualityXpEntry>();
    }

    /// <summary>品质系数条目（纯数据，无 Verse 依赖，可离线单测）。</summary>
    public sealed class QualityXpEntry
    {
        /// <summary>QualityCategory 的 name（如 "Legendary"），语言无关稳定键。</summary>
        public string qualityName;

        /// <summary>品质系数（V2.0 §7：优秀×1.5 / 大师×3 / 传奇×5）。</summary>
        public float multiplier;
    }
}
