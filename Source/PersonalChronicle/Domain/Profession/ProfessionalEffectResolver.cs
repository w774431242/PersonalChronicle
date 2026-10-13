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
    /// - 效果强度随专业等级逐级成长：value × (level / maxLevel) × (1 + ratingWeight)。
    /// - ratingWeight 来自 ProfessionalRatingDef（由 Level 派生，软上限封顶）；无评级则为 0。
    /// - 向后兼容：ratingDefs 为 null 或空 → ratingWeight=0，仍按等级比例生效。
    /// - 生效条件：pawn 拥有声明了该效果的技能，且 level ≥ 1，且 recipe 命中该技能白名单。
    /// - 不反向写回任何事实层。
    /// </summary>
    public static class ProfessionalEffectResolver
    {
        /// <summary>
        /// 速度修正系数：1 + Σ 命中效果的 (value × level/maxLevel × (1 + ratingWeight))。
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
            List<ProfessionalRatingDef> ratingDefs = null,
            string recipeWorkTypeDefName = null,
            int ratingTierShift = 0)
        {
            // 委托 ResolveContributions（单一真源）：只累加本次命中的技能，口径与浮层明细完全一致。
            List<SkillBonusContribution> contributions =
                ResolveContributions(skills, skillDefs, effectDefs, recipeDefName, recipeWorkTypeDefName,
                    ratingDefs, ratingTierShift);
            float bonus = 0f;
            for (int i = 0; i < contributions.Count; i++)
            {
                SkillBonusContribution c = contributions[i];
                if (c != null && c.ActiveNow)
                {
                    bonus += c.SpeedBonus;
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
            List<ProfessionalRatingDef> ratingDefs = null,
            string recipeWorkTypeDefName = null,
            int ratingTierShift = 0)
        {
            // 委托 ResolveContributions（单一真源）：只累加本次命中的技能，口径与浮层明细完全一致。
            List<SkillBonusContribution> contributions =
                ResolveContributions(skills, skillDefs, effectDefs, recipeDefName, recipeWorkTypeDefName,
                    ratingDefs, ratingTierShift);
            int levels = 0;
            for (int i = 0; i < contributions.Count; i++)
            {
                SkillBonusContribution c = contributions[i];
                if (c != null && c.ActiveNow)
                {
                    levels += c.QualityLevels;
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
        /// 通用 Stat 加成倍率：1 + Σ 命中技能（声明了指定 kind + statDefName 的效果）的
        /// (value × level/maxLevel × (1+ratingWeight))。
        /// 供 StatBonus/CombatStat/YieldBonus/XpBonus 共用（kind 由调用方限定）。
        /// - isReduction=true：返回 1 - Σvalue（"越低越好" Stat 的乘算减少）。
        /// - 未命中返回 1.0（无修正）。
        /// 2026-08-22 P0 v2.0 新增（多维加成前置）。
        /// </summary>
        public static float ResolveStatBonus(
            List<ProfessionalSkillData> skills,
            List<ProfessionalSkillDef> skillDefs,
            Dictionary<string, ProfessionalEffectDef> effectDefs,
            ProfessionalEffectKind kind,
            string targetStatDefName,
            string recipeDefName,
            List<ProfessionalRatingDef> ratingDefs = null,
            string recipeWorkTypeDefName = null,
            float ratingWeightKind = 0f,
            int ratingTierShift = 0)
        {
            if (skills == null || skillDefs == null || effectDefs == null || string.IsNullOrEmpty(targetStatDefName))
            {
                return 1f;
            }
            float sum = 0f;
            bool anyReduction = false;
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
                // 通用 Stat（GetGenericStatFactor）无制造上下文：recipe 双空时不套白名单，
                // 否则带 practiceWorkTypeDefNames 的技能永远无法把 ConstructionSpeed 等加成注入 pawn 面板。
                bool requireRecipe = !string.IsNullOrEmpty(recipeDefName)
                    || !string.IsNullOrEmpty(recipeWorkTypeDefName);
                if (requireRecipe && !RecipeMatches(skillDef, recipeDefName, recipeWorkTypeDefName))
                {
                    continue;
                }
                if (skillDef.effectDefNames == null)
                {
                    continue;
                }
                ProfessionalRatingDef rating = EffectiveRating(skillData.level, ratingDefs, ratingTierShift);
                float ratingWeight = 0f;
                if (rating != null)
                {
                    ratingWeight = rating.workSpeedWeight > 0f ? rating.workSpeedWeight : ratingWeightKind;
                }
                int maxLevel = skillDef.maxLevel > 0 ? skillDef.maxLevel : 50;
                for (int j = 0; j < skillDef.effectDefNames.Count; j++)
                {
                    if (effectDefs.TryGetValue(skillDef.effectDefNames[j], out ProfessionalEffectDef effectDef))
                    {
                        if (effectDef != null && effectDef.kind == kind
                            && string.Equals(effectDef.statDefName, targetStatDefName, System.StringComparison.Ordinal))
                        {
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
                            if (effectDef.isReduction)
                            {
                                anyReduction = true;
                            }
                            sum += ScaleByLevel(baseValue, skillData.level, maxLevel, rw);
                        }
                    }
                }
            }
            if (sum <= 0f)
            {
                return 1f;
            }
            return anyReduction ? (1f - sum) : (1f + sum);
        }

        /// <summary>
        /// recipe 是否命中技能实践白名单（与 ArchiveService.RecipeRelevance 语义一致）。
        /// Recipe 白名单命中 → true；无 Recipe 白名单但有 WorkType 白名单 → 按 workType 判定；
        /// 双白名单皆空 = 全相关（true）。
        /// 修复 2026-08-22（P0 批1）：原实现 Recipe 白名单为空即 return true，
        /// 导致 practiceWorkTypeDefNames（工程/武器等动态配方场景）永不生效。
        /// </summary>
        public static bool RecipeMatches(ProfessionalSkillDef skillDef, string recipeDefName, string recipeWorkTypeDefName)
        {
            if (skillDef == null)
            {
                return false;
            }
            bool hasRecipeList = skillDef.practiceRecipeDefNames != null && skillDef.practiceRecipeDefNames.Count > 0;
            bool hasWorkTypeList = skillDef.practiceWorkTypeDefNames != null && skillDef.practiceWorkTypeDefNames.Count > 0;
            if (!hasRecipeList && !hasWorkTypeList)
            {
                return true;
            }
            if (hasRecipeList && !string.IsNullOrEmpty(recipeDefName)
                && skillDef.practiceRecipeDefNames.Contains(recipeDefName))
            {
                return true;
            }
            if (hasWorkTypeList && !string.IsNullOrEmpty(recipeWorkTypeDefName)
                && skillDef.practiceWorkTypeDefNames.Contains(recipeWorkTypeDefName))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 本技能的有效评级：先按等级解析档位，再叠加<b>环境档位偏移</b>
        /// （车间 <c>ratingOffset</c>；唯一真源 <see cref="ProfessionalRatingEvaluator.ShiftRating"/>）。
        /// 偏移为 0（绝大多数调用）时零额外开销。
        /// </summary>
        private static ProfessionalRatingDef EffectiveRating(
            int level, List<ProfessionalRatingDef> ratingDefs, int ratingTierShift)
        {
            ProfessionalRatingDef rating = ProfessionalRatingEvaluator.ResolveRating(level, ratingDefs);
            return ratingTierShift == 0
                ? rating
                : ProfessionalRatingEvaluator.ShiftRating(rating, ratingTierShift, ratingDefs);
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
        /// <summary>
        /// 单技能的加成明细（P10 属性面板浮层用，2026-08-30 新增）。
        /// 只承载"该技能贡献了多少"，不承载最终聚合——聚合由调用方对
        /// <see cref="ActiveNow"/> 过滤后求和（口径与 ResolveSpeedFactor/ResolveQualityLevels 一致）。
        /// </summary>
        public sealed class SkillBonusContribution
        {
            /// <summary>ProfessionalSkillDef.defName。</summary>
            public string SkillDefName;

            /// <summary>该技能当前等级（level &lt; 1 的不会产生明细）。</summary>
            public int Level;

            /// <summary>当前评级 Def 名（ProfessionalRatingDef.defName，无则 null）。</summary>
            public string RatingDefName;

            /// <summary>速度加成量（小数：0.06 = +6%）；非乘算因子，需 +1 才是因子。</summary>
            public float SpeedBonus;

            /// <summary>品质档位偏移（int 截断后，可正可负）。</summary>
            public int QualityLevels;

            /// <summary>常驻 Stat 加成量（StatBonus/CombatStat/YieldBonus 合计，0.05 = +5%）。</summary>
            public float GenericBonus;

            /// <summary>常驻加成主目标 StatDef.defName（无则 null）。</summary>
            public string GenericStatDefName;

            /// <summary>当前 recipe / 工作类型是否命中该技能实践白名单（false = 本次不生效）。</summary>
            public bool ActiveNow;
        }

        /// <summary>
        /// 逐技能加成明细（单一真源）。ResolveSpeedFactor / ResolveQualityLevels 均委托本方法，
        /// 避免"浮层明细"与"实际注入值"两套公式漂移（GOV-006）。
        /// </summary>
        /// <param name="recipeDefName">当前配方（可空）；recipeWorkTypeDefName 与之一并用于白名单判定。</param>
        /// <param name="recipeWorkTypeDefName">当前工作类型 WorkTypeDef.defName（可空）。</param>
        /// <returns>每条 = 一个有效技能的贡献；无有效技能返回空列表。</returns>
        public static List<SkillBonusContribution> ResolveContributions(
            List<ProfessionalSkillData> skills,
            List<ProfessionalSkillDef> skillDefs,
            Dictionary<string, ProfessionalEffectDef> effectDefs,
            string recipeDefName,
            string recipeWorkTypeDefName,
            List<ProfessionalRatingDef> ratingDefs = null,
            int ratingTierShift = 0)
        {
            List<SkillBonusContribution> list = new List<SkillBonusContribution>();
            if (skills == null || skillDefs == null || effectDefs == null)
            {
                return list;
            }
            for (int i = 0; i < skills.Count; i++)
            {
                ProfessionalSkillData skillData = skills[i];
                if (skillData == null || skillData.level < 1)
                {
                    continue;
                }
                ProfessionalSkillDef skillDef = FindSkillDef(skillDefs, skillData.skillDefName);
                if (skillDef == null || skillDef.effectDefNames == null)
                {
                    continue;
                }
                ProfessionalRatingDef rating = EffectiveRating(skillData.level, ratingDefs, ratingTierShift);
                float rwSpeedBase = rating != null ? rating.workSpeedWeight : 0f;
                float rwQualityBase = rating != null ? rating.qualityBiasWeight : 0f;
                int maxLevel = skillDef.maxLevel > 0 ? skillDef.maxLevel : 50;

                float speed = 0f;
                int quality = 0;
                float generic = 0f;
                string genericStat = null;
                bool hasPawnStat = false;
                bool hasCraftEffect = false;   // 有制造效果（WorkSpeed/QualityBias）⇒ 属 B3 门控适用范围
                for (int j = 0; j < skillDef.effectDefNames.Count; j++)
                {
                    if (!effectDefs.TryGetValue(skillDef.effectDefNames[j], out ProfessionalEffectDef effectDef)
                        || effectDef == null)
                    {
                        continue;
                    }
                    float baseValue = effectDef.value;
                    EffectOverride ov = FindOverride(skillDef, effectDef.defName);
                    if (ov != null && ov.hasValue)
                    {
                        baseValue = ov.value;
                    }

                    if (effectDef.kind == ProfessionalEffectKind.WorkSpeed)
                    {
                        hasCraftEffect = true;
                        float rw = rwSpeedBase;
                        if (ov != null && ov.ratingWeightScale != 1f) rw *= ov.ratingWeightScale;
                        speed += ScaleByLevel(baseValue, skillData.level, maxLevel, rw);
                    }
                    else if (effectDef.kind == ProfessionalEffectKind.QualityBias)
                    {
                        hasCraftEffect = true;
                        float rw = rwQualityBase;
                        if (ov != null && ov.ratingWeightScale != 1f) rw *= ov.ratingWeightScale;
                        quality += (int)(baseValue * (1f + rw));
                    }
                    else if (effectDef.kind == ProfessionalEffectKind.StatBonus
                        || effectDef.kind == ProfessionalEffectKind.CombatStat
                        || effectDef.kind == ProfessionalEffectKind.YieldBonus)
                    {
                        generic += ScaleByLevel(baseValue, skillData.level, maxLevel, rwSpeedBase);
                        if (genericStat == null)
                        {
                            genericStat = effectDef.statDefName;
                        }
                        hasPawnStat = true;
                    }
                }
                if (speed <= 0f && quality == 0 && generic <= 0f)
                {
                    continue;
                }
                list.Add(new SkillBonusContribution
                {
                    SkillDefName = skillData.skillDefName,
                    Level = skillData.level,
                    RatingDefName = rating != null ? rating.defName : null,
                    SpeedBonus = speed,
                    QualityLevels = quality,
                    GenericBonus = generic,
                    GenericStatDefName = genericStat,
                    // B3 门控修复（2026-10-01 自检 FAIL）：声明了练习白名单（配方/工作类型）的技能，
                    //   其**制造效果**（WorkSpeed/QualityBias）都必须由白名单命中判定 —— 否则"只应在
                    //   练某配方时生效"的加成会变成永久被动（数值失衡，实机 [SelfTest] FAIL B3）。
                    //   ⚠ 2026-10-02 补丁（CHG-20261002-UNITTEST-FIX）：**纯常驻 Stat 技能在双空上下文**
                    //   （无 recipe 无工作类型）保持生效 —— 对齐真源 GetGenericStatFactor → ResolveStatBonus
                    //   的"双空不套白名单"注入路径；否则浮层显示"不生效"而 pawn 面板实际已注入（GOV-006 漂移）。
                    //   无白名单的技能维持原设计：常驻 Stat 无上下文也生效。
                    ActiveNow = (hasPawnStat && !IsWhitelisted(skillDef))
                        || (string.IsNullOrEmpty(recipeDefName) && string.IsNullOrEmpty(recipeWorkTypeDefName)
                            && hasPawnStat && !hasCraftEffect)
                        || RecipeMatches(skillDef, recipeDefName, recipeWorkTypeDefName)
                });
            }
            return list;
        }

        /// <summary>
        /// 等级成长：XML value 为满级强度，当前强度 = value × (level/maxLevel) × (1 + ratingWeight)。
        /// level &lt; 1 返回 0；maxLevel 非法时按 50。
        /// </summary>
        /// <summary>
        /// 技能是否声明了练习白名单（配方 / 工作类型任一非空）—— 白名单技能全效果需上下文门控。
        /// </summary>
        private static bool IsWhitelisted(ProfessionalSkillDef skillDef)
        {
            return skillDef != null
                && ((skillDef.practiceRecipeDefNames != null && skillDef.practiceRecipeDefNames.Count > 0)
                    || (skillDef.practiceWorkTypeDefNames != null && skillDef.practiceWorkTypeDefNames.Count > 0));
        }
        /// <summary>
        /// 等级成长曲线（V2.0 沉浸感优化）：用标准正态 CDF 的累积形状替代线性，
        /// 使前期快速成长、中段冲高、满级平滑封顶（钟形 S 曲线），解决线性曲线低级/中级
        /// 加成几乎不可见的「太平」问题。归一到 [0,1]：t=0→0，t=1(满级)→1。
        /// GrowthSigma 控制陡峭度：越小越陡、前期成长越强；0.38 为平衡值，PM 可按手感微调。
        /// 单条最终加成 = baseValue × NormalProgress(level/maxLevel) × (1 + ratingWeight)。
        /// </summary>
        private const float GrowthSigma = 0.38f;

        public static float ScaleByLevel(float baseValue, int level, int maxLevel, float ratingWeight)
        {
            if (level < 1)
            {
                return 0f;
            }
            int cap = maxLevel > 0 ? maxLevel : 50;
            float t = (float)level / cap;
            float progress = NormalProgress(t);
            float rw = ratingWeight < 0f ? 0f : ratingWeight;
            return baseValue * progress * (1f + rw);
        }

        /// <summary>正态成长比例：标准正态 CDF Φ(t/σ) 归一到 [0,1]（t=0→0，t=1→1）。</summary>
        private static float NormalProgress(float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            float f = NormalCdf01(t / GrowthSigma);
            float f0 = NormalCdf01(0f);
            float f1 = NormalCdf01(1f / GrowthSigma);
            float p = (f - f0) / (f1 - f0);
            if (p < 0f) p = 0f;
            if (p > 1f) p = 1f;
            return p;
        }

        /// <summary>标准正态 CDF：Φ(x) = 0.5·(1 + erf(x/√2))，erf 用 Abramowitz–Stegun 7.1.26 近似。</summary>
        private static float NormalCdf01(float x)
        {
            return 0.5f * (1f + Erf(x * 0.70710678f));
        }

        private static float Erf(float x)
        {
            float sign = x < 0f ? -1f : 1f;
            x = x < 0f ? -x : x;
            float t = 1f / (1f + 0.3275911f * x);
            float y = 1f - (((((1.061405429f * t - 1.453152027f) * t) + 1.421413741f) * t
                - 0.284496736f) * t + 0.254829592f) * t * (float)System.Math.Exp(-x * x);
            return sign * y;
        }

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
