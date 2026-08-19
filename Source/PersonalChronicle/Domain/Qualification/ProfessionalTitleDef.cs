using Verse;

namespace PersonalChronicle.Domain.Qualification
{
    /// <summary>
    /// P7 职称定义（数据驱动）。资格满足后授予，回写 CareerEvent(TitleGranted)。
    /// </summary>
    public sealed class ProfessionalTitleDef : Def
    {
        /// <summary>关联资格（QualificationDef.defName）。</summary>
        public string qualificationDefName;

        /// <summary>关联专业技能（ProfessionalSkillDef.defName）。</summary>
        public string professionalSkillDefName;

        /// <summary>职称阶梯排序。</summary>
        public int order;

        /// <summary>默认自动授予（D-T1：默认 true，留 autoGrant 开关供 PM 调整）。</summary>
        public bool autoGrant = true;
    }
}
