using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间岗位技能评分 —— <b>唯一实现</b>（体检 P2-⑥ 的统一口径）。
    ///
    /// <para><b>为何要统一</b>：班组名额分配（<c>MapComponent_WorkshopManager.DutyScoreFor</c>）
    /// 过去走「门槛技能 → 车间类型主技能 → Crafting」，而轮岗 SkillBased 模式
    /// （<c>RotationRule.BestSkillIndex</c>）**恒用 Crafting** ⇒ 同一玩家预期
    /// （"技能最高者优先上岗"）会因排班种类不同而给出不同的人选。</para>
    ///
    /// <para><b>取值优先级</b>：① 显式门槛技能（班组/固定班可设）→
    /// ② 车间类型主技能（<c>WorkshopTypeDef.skillDefNames[0]</c>）→ ③ 原版 <c>Crafting</c>。</para>
    ///
    /// <para><b>返回值</b>：技能等级；技能缺失、被禁用或 pawn 非法 → <c>-1</c>
    /// （排在所有可用者之后，但**不抛异常**且仍参与排序）。</para>
    ///
    /// <para>评分只用于"同池内谁优先上岗"，不参与任何加成数值计算。</para>
    /// </summary>
    public static class WorkshopDutyScoring
    {
        /// <summary>本车间用于评分的技能（口径见类注释）。</summary>
        public static SkillDef SkillOf(Zone_Workshop zone, SkillDef requiredSkill)
        {
            if (requiredSkill != null)
            {
                return requiredSkill;
            }
            if (zone != null && zone.WorkshopType != null)
            {
                List<string> names = zone.WorkshopType.skillDefNames;
                if (names != null && names.Count > 0 && !string.IsNullOrEmpty(names[0]))
                {
                    SkillDef fromType = DefDatabase<SkillDef>.GetNamedSilentFail(names[0]);
                    if (fromType != null)
                    {
                        return fromType;
                    }
                }
            }
            return SkillDefOf.Crafting;
        }

        /// <summary>评分（-1 = 不可用；见类注释）。</summary>
        public static int ScoreOf(Zone_Workshop zone, SkillDef requiredSkill, Pawn pawn)
        {
            if (pawn == null || pawn.skills == null)
            {
                return -1;
            }
            SkillDef skill = SkillOf(zone, requiredSkill);
            if (skill == null)
            {
                return -1;
            }
            SkillRecord record = pawn.skills.GetSkill(skill);
            return record != null && !record.TotallyDisabled ? record.Level : -1;
        }
    }
}
