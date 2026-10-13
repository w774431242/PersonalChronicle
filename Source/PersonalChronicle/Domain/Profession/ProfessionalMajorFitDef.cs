using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>单条专业权重条目（技能 → 权重），供 ProfessionalMajorFitDef.fitWeights 列表使用。</summary>
    public class FitWeightEntry
    {
        /// <summary>原版 SkillDef.defName。</summary>
        public string skillDefName;
        /// <summary>该技能在本专业的权重（同专业所有条目权重和必须 = 100）。</summary>
        public int weight;
    }

    /// <summary>
    /// 一级专业 → 原版技能权重矩阵（铁律 6.5：权重数据移入 Def，.cs 不再硬编码）。
    /// 对应 XML：Defs/ProfessionalMajorFit.xml。每行 fitWeights 权重和必须 = 100。
    /// </summary>
    public class ProfessionalMajorFitDef : Def
    {
        /// <summary>一级专业稳定键（对应 ProfessionalMajor 常量）。</summary>
        public string major;
        /// <summary>该专业各原版技能权重（权重和 = 100）。</summary>
        public List<FitWeightEntry> fitWeights = new List<FitWeightEntry>();
    }

    /// <summary>
    /// 品质档 → 代表值权重（铁律 6.5 迁移）。对应 XML：Defs/ProfessionalQualityWeight.xml。
    /// 键 qualityCategory = QualityCategory 名（Normal/Good/Excellent/Masterwork/Legendary）。
    /// </summary>
    public class ProfessionalQualityWeightDef : Def
    {
        public string qualityCategory;
        public int weight;
    }
}
