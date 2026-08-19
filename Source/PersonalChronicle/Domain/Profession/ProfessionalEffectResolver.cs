using System.Collections.Generic;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 专业效果解析层（V2.0 §10/§11 第二层）。
    /// 纯逻辑、零 Verse/RimWorld 依赖（仅靠 string 键与 int 索引），可离线 NUnit 单测。
    ///
    /// 输入：pawn 的专业技能状态（<see cref="ProfessionalState"/> 的技能列表）
    ///       + 声明拥有的效果 Def（<see cref="ProfessionalEffectDef"/>） + 当前配方 defName。
    /// 输出：速度乘算修正系数 / 品质整档偏移量。
    ///
    /// 设计铁律（领域设计 §3.3 + §8）：
    /// - 效果强度不直接读 Level 硬算，经 Rating 档位聚合（P4）后生效。
    /// - P3 基础链路：value 固定值；P4 升级为 value × (1 + ratingWeight)（叠加模式，
    ///   ratingWeight 来自 ProfessionalRatingDef，由 Level 派生，软上限封顶）。
    /// - 向后兼容：ratingDefs 为 null 或空 → ratingWeight=0 → 退回 P3 行为（仅 value），不影响 P3 验收。
    /// - 生效条件：pawn 拥有声明了该效果的技能，且 level ≥ 1，且 recipe 命中该技能白名单。
    /// - 不反向写回任何事实层。
    /// </summary>
    public static class ProfessionalEffectResolver
    {
        /// <summary>
        /// 速度修正系数：1 + Σ 命中效果的 (value × (1 + ratingWeight))（加法叠加多个技能效果）。
        /// 未命中返回 1.0（无修正）。
        /// </summary>
        /// <param name="skills">pawn 已获得的专业技能状态（可空）。</param>
        /// <param name="skillDefs">全部 ProfessionalSkillDef（用于取 effectDefNames + 白名单）。</param>
        /// <param name="effectDefs">全部 ProfessionalEffectDef（按 defName 索引取 value/kind）。</param>
        /// <param name="recipeDefName">当前配方 defName（可空 = 不判定 recipe，全部命中）。</param>
        /// <param name="ratingDefs">全部 ProfessionalRatingDef（可空 = 退回 P3 行为，ratingWeight=0）。</param>
        /// <returns>速度乘算系数（≥ 1.0）。</returns>
        public static float ResolveSpeedFactor(
            List<ProfessionalSkillData> skills,
            List<ProfessionalSkillDef> skillDefs,
            Dictionary<string, ProfessionalEffectDef> effectDefs,
            string recipeDefName,
            List<ProfessionalRatingDef> ratingDefs = null)
        {
            float bonus = 0f;
            if (skills == null || skillDefs == null || effectDefs == null)
            {
                return 1f;
            }
            for (int i = 0; i < skills.Count; i++)
            {
                ProfessionalSkillData skillData = skills[i];
                if (skillData == null || skillData.level < 1)
                {
                    continue;
                }
                ProfessionalSkillDef skillDef = FindSkillDef(skillDefs, skillData.skillDefName);
                if (skillDef == null)
                {
                    continue;
                }
                if (!RecipeMatches(skillDef, recipeDefName))
                {
                    continue;
                }
                if (skillDef.effectDefNames == null)
                {
                    continue;
                }
                ProfessionalRatingDef rating = ProfessionalRatingEvaluator.ResolveRating(skillData.level, ratingDefs);
                float ratingWeight = rating != null ? rating.workSpeedWeight : 0f;
                for (int j = 0; j < skillDef.effectDefNames.Count; j++)
                {
                    if (effectDefs.TryGetValue(skillDef.effectDefNames[j], out ProfessionalEffectDef effectDef))
                    {
                        if (effectDef != null && effectDef.kind == ProfessionalEffectKind.WorkSpeed)
                        {
                            // 2026-08-19 差异化特化点：技能级覆盖（数值 + 评级权重缩放），缺省回退共享值
                            float baseValue = effectDef.value;
                            float rw = ratingWeight;
                            EffectOverride ov = FindOverride(skillDef, effectDef.defName);
                            if (ov != null)
                            {
                                if (ov.hasValue)
                                {
                                    baseValue = ov.value;
                                }
                                if (ov.ratingWeightScale != 1f)
                                {
                                    rw *= ov.ratingWeightScale;
                                }
                            }
                            bonus += baseValue * (1f + rw);
                        }
                    }
                }
            }
            return 1f + bonus;
        }

        /// <summary>
        /// 品质整档偏移量：Σ 命中 QualityBias 效果的 value × (1 + ratingWeight)（int 截断，加法叠加）。
        /// 未命中返回 0。
        /// </summary>
        /// <returns>品质档位偏移（可正可负）。</returns>
        public static int ResolveQualityLevels(
            List<ProfessionalSkillData> skills,
            List<ProfessionalSkillDef> skillDefs,
            Dictionary<string, ProfessionalEffectDef> effectDefs,
            string recipeDefName,
            List<ProfessionalRatingDef> ratingDefs = null)
        {
            int levels = 0;
            if (skills == null || skillDefs == null || effectDefs == null)
            {
                return 0;
            }
            for (int i = 0; i < skills.Count; i++)
            {
                ProfessionalSkillData skillData = skills[i];
                if (skillData == null || skillData.level < 1)
                {
                    continue;
                }
                ProfessionalSkillDef skillDef = FindSkillDef(skillDefs, skillData.skillDefName);
                if (skillDef == null)
                {
                    continue;
                }
                if (!RecipeMatches(skillDef, recipeDefName))
                {
                    continue;
                }
                if (skillDef.effectDefNames == null)
                {
                    continue;
                }
                ProfessionalRatingDef rating = ProfessionalRatingEvaluator.ResolveRating(skillData.level, ratingDefs);
                float ratingWeight = rating != null ? rating.qualityBiasWeight : 0f;
                for (int j = 0; j < skillDef.effectDefNames.Count; j++)
                {
                    if (effectDefs.TryGetValue(skillDef.effectDefNames[j], out ProfessionalEffectDef effectDef))
                    {
                        if (effectDef != null && effectDef.kind == ProfessionalEffectKind.QualityBias)
                        {
                            // 2026-08-19 差异化特化点：技能级覆盖（数值 + 评级权重缩放），缺省回退共享值
                            float baseValue = effectDef.value;
                            float rw = ratingWeight;
                            EffectOverride ov = FindOverride(skillDef, effectDef.defName);
                            if (ov != null)
                            {
                                if (ov.hasValue)
                                {
                                    baseValue = ov.value;
                                }
                                if (ov.ratingWeightScale != 1f)
                                {
                                    rw *= ov.ratingWeightScale;
                                }
                            }
                            levels += (int)(baseValue * (1f + rw));
                        }
                    }
                }
            }
            return levels;
        }

        /// <summary>
        /// 品质档位 clamp（Domain 纯函数，替代 nonpub QualityUtility.AddLevels）。
        /// 2026-08-19 验收 P2-5 修复：签名改 int 索引（彻底去除 RimWorld.QualityCategory 依赖，
        /// 保持 Domain 零 Verse/RimWorld）；调用方在适配层做枚举转换。
        /// QualityCategory 枚举索引顺序：Awful=0 Poor=1 Normal=2 Good=3
        /// Excellent=4 Masterwork=5 Legendary=6。
        /// </summary>
        public static int ClampQuality(int currentIndex, int levels)
        {
            int index = currentIndex + levels;
            if (index < 0)
            {
                index = 0;
            }
            if (index > 6)
            {
                index = 6;
            }
            return index;
        }

        /// <summary>
        /// recipe 是否命中技能实践白名单（与 ArchiveService.RecipeRelevance 语义一致）。
        /// 空白名单 = 全相关（返回 true）。
        /// </summary>
        public static bool RecipeMatches(ProfessionalSkillDef skillDef, string recipeDefName)
        {
            if (skillDef == null)
            {
                return false;
            }
            if (skillDef.practiceRecipeDefNames == null || skillDef.practiceRecipeDefNames.Count == 0)
            {
                return true;
            }
            if (string.IsNullOrEmpty(recipeDefName))
            {
                // recipe 未知时保守返回 false（避免无上下文误加成）。
                return false;
            }
            return skillDef.practiceRecipeDefNames.Contains(recipeDefName);
        }

        private static ProfessionalSkillDef FindSkillDef(List<ProfessionalSkillDef> skillDefs, string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return null;
            }
            for (int i = 0; i < skillDefs.Count; i++)
            {
                if (string.Equals(skillDefs[i].defName, defName, System.StringComparison.Ordinal))
                {
                    return skillDefs[i];
                }
            }
            return null;
        }

        /// <summary>
        /// 取技能对某效果 Def 的覆盖项（2026-08-19 差异化特化点机制）；无覆盖返回 null（回退共享值）。
        /// </summary>
        private static EffectOverride FindOverride(ProfessionalSkillDef skillDef, string effectDefName)
        {
            if (skillDef == null || skillDef.effectOverrides == null || skillDef.effectOverrides.Count == 0
                || string.IsNullOrEmpty(effectDefName))
            {
                return null;
            }
            for (int i = 0; i < skillDef.effectOverrides.Count; i++)
            {
                EffectOverride ov = skillDef.effectOverrides[i];
                if (ov != null && string.Equals(ov.effectDefName, effectDefName, System.StringComparison.Ordinal))
                {
                    return ov;
                }
            }
            return null;
        }
    }
}
