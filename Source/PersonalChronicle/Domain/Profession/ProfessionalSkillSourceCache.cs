using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 原版技能名 → 职业技能映射缓存（V2.1 原版链路采集专用）。
    /// 构建自 DefDatabase（ProfessionalSkillDef.sourceSkills），运行时 Def 稳定，
    /// 首次访问懒构建；Warmup 在 [StaticConstructorOnStartup] 时预热，保证 Def 已填充。
    /// 用于 SkillRecord.Learn 高频采集的首道过滤：技能无职业命中即零成本返回。
    /// </summary>
    public static class ProfessionalSkillSourceCache
    {
        private static Dictionary<string, List<ProfessionalSkillDef>> _map;
        private static bool _built;

        /// <summary>预热（DefDatabase 就绪后调用）。</summary>
        public static void Warmup()
        {
            if (!_built)
            {
                Build();
            }
        }

        /// <summary>按原版技能 defName 取映射的职业技能列表；无匹配返回 null。</summary>
        public static List<ProfessionalSkillDef> Get(string vanillaSkillName)
        {
            if (!_built)
            {
                Build();
            }
            if (string.IsNullOrEmpty(vanillaSkillName) || _map == null)
            {
                return null;
            }
            _map.TryGetValue(vanillaSkillName, out List<ProfessionalSkillDef> list);
            return list;
        }

        private static void Build()
        {
            _map = new Dictionary<string, List<ProfessionalSkillDef>>();
            List<ProfessionalSkillDef> defs = DefDatabase<ProfessionalSkillDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
            {
                ProfessionalSkillDef def = defs[i];
                if (def == null || def.sourceSkills == null)
                {
                    continue;
                }
                for (int j = 0; j < def.sourceSkills.Count; j++)
                {
                    string skill = def.sourceSkills[j];
                    if (string.IsNullOrEmpty(skill))
                    {
                        continue;
                    }
                    if (!_map.TryGetValue(skill, out List<ProfessionalSkillDef> list))
                    {
                        list = new List<ProfessionalSkillDef>();
                        _map[skill] = list;
                    }
                    if (!list.Contains(def))
                    {
                        list.Add(def);
                    }
                }
            }
            _built = true;
        }
    }
}
