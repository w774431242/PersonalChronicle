using System.Collections.Generic;
using PersonalChronicle.Capture.Effects;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Profession;
using RimWorld;
using Verse;

namespace PersonalChronicle.Application.Effects
{
    /// <summary>
    /// 专业效果组合层（V2.0 §11 第三层，介于 Resolver 与 Adapter 之间）。
    /// 负责把"游戏对象（Pawn/Recipe/Product）"翻译成 Resolver 所需的纯数据：
    /// 查询 pawn 的 <see cref="ProfessionalState"/>、汇总 Def 索引、按 recipe 解析效果值。
    ///
    /// 本层不直接触碰原版制造逻辑（StatPart / Postfix 属 Capture/Effects 适配层职责），
    /// 只对外暴露两个查询：<see cref="GetSpeedFactor"/> 与 <see cref="GetQualityBiasLevels"/>。
    /// </summary>
    public static class ProfessionalEffectService
    {
        /// <summary>
        /// 制造速度乘算系数。StatPart 适配层在评估 pawn.GetStatValue(workSpeedStat) 时调用。
        /// </summary>
        /// <param name="pawn">制造者（可为 null → 1.0）。</param>
        /// <param name="recipe">当前配方（可为 null → 不判定 recipe）。</param>
        /// <returns>速度乘算系数（≥ 1.0）。</returns>
        public static float GetSpeedFactor(Pawn pawn, RecipeDef recipe)
        {
            if (pawn == null)
            {
                return 1f;
            }
            List<ProfessionalSkillData> skills = GetSkills(pawn);
            if (skills == null)
            {
                return 1f;
            }
            string recipeDefName = recipe != null ? recipe.defName : null;
            return ProfessionalEffectResolver.ResolveSpeedFactor(
                skills,
                DefDatabase<ProfessionalSkillDef>.AllDefsListForReading,
                ProfessionalEffectRegistry.EffectDefs,
                recipeDefName,
                ProfessionalEffectRegistry.RatingDefs);
        }

        /// <summary>
        /// 品质整档偏移量。Patch_GenRecipe Postfix 在产出成品时调用。
        /// </summary>
        /// <param name="pawn">制造者（可为 null → 0）。</param>
        /// <param name="recipe">当前配方（可为 null → 0）。</param>
        /// <returns>品质档位偏移（可正可负，0 = 无偏移）。</returns>
        public static int GetQualityBiasLevels(Pawn pawn, RecipeDef recipe)
        {
            if (pawn == null)
            {
                return 0;
            }
            List<ProfessionalSkillData> skills = GetSkills(pawn);
            if (skills == null)
            {
                return 0;
            }
            string recipeDefName = recipe != null ? recipe.defName : null;
            return ProfessionalEffectResolver.ResolveQualityLevels(
                skills,
                DefDatabase<ProfessionalSkillDef>.AllDefsListForReading,
                ProfessionalEffectRegistry.EffectDefs,
                recipeDefName,
                ProfessionalEffectRegistry.RatingDefs);
        }

        /// <summary>
        /// 从 pawn 取专业技能状态列表（经组件 → PawnObject.CareerData.Professional）。
        /// 任意环节缺失返回 null（零效果，不崩）。
        /// </summary>
        private static List<ProfessionalSkillData> GetSkills(Pawn pawn)
        {
            if (pawn == null || Current.Game == null)
            {
                return null;
            }
            ChronicleGameComponent component = Current.Game.GetComponent<ChronicleGameComponent>();
            if (component == null)
            {
                return null;
            }
            PawnObject pawnObject = component.GetObject(pawn.GetUniqueLoadID()) as PawnObject;
            if (pawnObject == null || pawnObject.CareerData == null || pawnObject.CareerData.Professional == null)
            {
                return null;
            }
            List<ProfessionalSkillData> skills = pawnObject.CareerData.Professional.skills;
            return skills;
        }
    }
}
