using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// Pawn 职业状态容器（P2-A §4.2）。落 <see cref="CareerData.Professional"/>（Q1 决策）。
    /// 承载专业技能列表 / 主方向 / 快速统计；由 CareerEvent 事实派生，append-only。
    /// </summary>
    public sealed class ProfessionalState : IExposable
    {
        /// <summary>当前主方向（玩家选择，V2.0 §9 推荐≠强制）。</summary>
        public string primaryDirection;

        /// <summary>已获得的专业技能（按 defName 唯一）。</summary>
        public List<ProfessionalSkillData> skills = new List<ProfessionalSkillData>();

        /// <summary>总实践计数（跨技能快速统计）。</summary>
        public Dictionary<string, int> practiceCountBySkill = new Dictionary<string, int>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref primaryDirection, "primaryDirection");
            Scribe_Collections.Look(ref skills, "skills", LookMode.Deep);
            Scribe_Collections.Look(ref practiceCountBySkill, "practiceCountBySkill", LookMode.Value, LookMode.Value);
            if (skills == null) skills = new List<ProfessionalSkillData>();
            if (practiceCountBySkill == null) practiceCountBySkill = new Dictionary<string, int>();
        }

        /// <summary>按 defName 取技能状态；无则返回 null。</summary>
        public ProfessionalSkillData GetSkill(string skillDefName)
        {
            if (string.IsNullOrEmpty(skillDefName) || skills == null)
            {
                return null;
            }
            for (int i = 0; i < skills.Count; i++)
            {
                if (string.Equals(skills[i].skillDefName, skillDefName, System.StringComparison.Ordinal))
                {
                    return skills[i];
                }
            }
            return null;
        }
    }
}
