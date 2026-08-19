using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain.Qualification
{
    /// <summary>
    /// P5 资格定义（V2.0 §14 字段）。数据驱动铁律：C# 端禁止硬编码资格门槛，
    /// 全部经 DefDatabase 读取。评价引擎（QualificationEvaluator）只读本 Def + 事实派生。
    /// </summary>
    public sealed class QualificationDef : Def
    {
        /// <summary>关联 ProfessionalSkillDef.defName（如 精密制造）。</summary>
        public string professionalSkillDefName;

        /// <summary>授予目标职称（ProfessionalTitleDef.defName，P7）。</summary>
        public string titleDefName;

        /// <summary>专业等级下限。</summary>
        public int requiredMinLevel;

        /// <summary>职业时长下限（tick）。</summary>
        public long requiredCareerTimeTicks;

        /// <summary>事实门槛：EventType → 最少次数。</summary>
        public List<QualificationEventReq> requiredEvents = new List<QualificationEventReq>();

        /// <summary>成就键门槛（P8 产出，AchievementEvaluator 的键）。</summary>
        public List<QualificationAchievementReq> requiredAchievements = new List<QualificationAchievementReq>();

        /// <summary>前置职称 defName（可空）。</summary>
        public string requiredPreviousTitle;

        /// <summary>是否要求实践+理论考试通过。</summary>
        public bool requiredExam;

        /// <summary>是否要求论文通过。</summary>
        public bool requiredThesis;

        /// <summary>是否要求答辩通过。</summary>
        public bool requiredDefense;

        /// <summary>综合评分门槛（0~100）。</summary>
        public float minimumScore;

        /// <summary>资格阶梯排序。</summary>
        public int order;
    }

    /// <summary>资格的事实门槛项。</summary>
    public sealed class QualificationEventReq
    {
        public string eventType;
        public int minCount;
    }

    /// <summary>资格的成就门槛项（P8 AchievementEvaluator 输出键）。</summary>
    public sealed class QualificationAchievementReq
    {
        public string achievementKey;
        public double minValue;
    }
}
