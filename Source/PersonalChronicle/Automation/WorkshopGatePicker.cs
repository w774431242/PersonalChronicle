using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 技能门槛选择器的**唯一实现**（两级 FloatMenu：技能 → 最低等级；顶部「清除门槛」）。
    ///
    /// <para>回调式设计：只负责选值，写回哪个对象由调用方决定。</para>
    ///
    /// <para>⚠ <b>当前未接线（2026-10-02）</b>：用户实测结论「班次只需要岗位、优先级就行。名额、
    /// 门槛等等无作用」⇒ 技能门槛已从班次编辑器与班次表撤下，本类暂时没有调用点。
    /// <b>刻意保留而非删除</b>：本工程源码目录未纳入版本控制，删除不可恢复；且
    /// <c>WorkshopShift.RequiredSkill/MinSkillLevel</c> 字段仍在（旧档照常读取），
    /// 若将来恢复该功能，接线即可。</para>
    /// </summary>
    internal static class WorkshopGatePicker
    {
        private const string KeyPrefix = "PersonalChronicle.Automation.Tab.";

        /// <summary>可选门槛等级档位。</summary>
        private static readonly int[] Levels = { 0, 5, 10, 15, 20 };

        /// <summary>
        /// 打开门槛选择流程。<paramref name="onApply"/> 在玩家选定后回调：
        /// 选「清除门槛」时传 <c>(null, 0)</c>。
        /// </summary>
        public static void Open(string title, Action<SkillDef, int> onApply)
        {
            if (onApply == null)
            {
                return;
            }
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption((KeyPrefix + "GateClear").Translate().ToString(),
                    () => onApply(null, 0))
            };
            List<SkillDef> skills = DefDatabase<SkillDef>.AllDefsListForReading;
            for (int i = 0; i < skills.Count; i++)
            {
                SkillDef skill = skills[i];
                if (skill == null || string.IsNullOrEmpty(skill.label))
                {
                    continue;
                }
                SkillDef captured = skill;
                options.Add(new FloatMenuOption(captured.label, () => OpenLevels(captured, onApply)));
            }
            Find.WindowStack.Add(new FloatMenu(options, title));
        }

        private static void OpenLevels(SkillDef skill, Action<SkillDef, int> onApply)
        {
            var options = new List<FloatMenuOption>();
            for (int i = 0; i < Levels.Length; i++)
            {
                int level = Levels[i];
                options.Add(new FloatMenuOption("≥ " + level, () => onApply(skill, level)));
            }
            Find.WindowStack.Add(new FloatMenu(options, skill.label));
        }

        /// <summary>门槛显示文本（无门槛显示 —；供排班行与编辑器共用）。</summary>
        public static string LabelOf(SkillDef skill, int minLevel)
        {
            return skill == null ? "—" : skill.label + " ≥" + minLevel;
        }
    }
}
