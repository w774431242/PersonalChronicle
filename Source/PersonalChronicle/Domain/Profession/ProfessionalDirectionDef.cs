using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 二级职业方向 Def（V2.0 §3 / P2-A §3.1）。
    /// 一级专业（Profession，12 类）下的方向（制造类 = 精密/武器/装备/工业）。
    /// 数据驱动：所有职业规则进 Def，C# 禁止硬编码 defName。
    ///
    /// 差异化特化点（2026-08-19）：方向不再是纯分组标签，携带特化语义与说明，
    /// 供 UI 展示方向差异与未来专属机制挂载（效果数值差异由技能级
    /// <see cref="ProfessionalSkillDef.effectOverrides"/> 承担，见 P2-A §9）。
    /// </summary>
    public sealed class ProfessionalDirectionDef : Def
    {
        /// <summary>所属一级专业稳定键（如 "Manufacturing"）。</summary>
        public string profession;

        /// <summary>该方向下的专业技能 defName 列表。</summary>
        public List<string> skillDefNames = new List<string>();

        /// <summary>UI 区分色（可选，纯展示）。</summary>
        public string colorHex;

        /// <summary>方向名翻译键（UI 展示；缺省回退 defName）。</summary>
        public string labelKey;

        /// <summary>
        /// 方向特化点语义键（如 Quality / Throughput / Material / Volume）。
        /// 机器可读的特化标识，供 UI 徽标与未来专属机制挂载；不得用于业务判定。
        /// </summary>
        public string specializationKey;

        /// <summary>方向特化点说明翻译键（一句话说明该方向"特化在哪"，UI 展示方向差异）。</summary>
        public string specializationDescKey;

        /// <summary>展示排序（升序）。</summary>
        public int order;
    }
}
