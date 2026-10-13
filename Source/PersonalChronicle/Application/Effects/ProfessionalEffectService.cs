using System.Collections.Generic;
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
            string recipeWorkType = recipe != null && recipe.requiredGiverWorkType != null
                ? recipe.requiredGiverWorkType.defName : null;
            return ProfessionalEffectResolver.ResolveSpeedFactor(
                skills,
                DefDatabase<ProfessionalSkillDef>.AllDefsListForReading,
                EffectMap(),
                recipeDefName,
                RatingList(),
                recipeWorkType,
                WorkshopRatingShift.Of(pawn));
        }

        /// <summary>
        /// 通用 Stat 加成倍率（2026-08-22 v2.0）：只聚合当前主修方向技能在目标 StatDef 上的
        /// 通用 kind（StatBonus/CombatStat/YieldBonus），各 kind 独立聚合后相乘，并按等级成长。
        /// 供 StatPart_ProfessionalStatBonus（Pawn 常驻 Stat）与 Patch_GenRecipe 成品覆盖调用。
        /// </summary>
        public static float GetGenericStatFactor(Pawn pawn, string statDefName)
        {
            if (pawn == null || string.IsNullOrEmpty(statDefName))
            {
                return 1f;
            }
            List<ProfessionalSkillData> skills = GetPrimaryDirectionSkills(pawn);
            if (skills == null)
            {
                return 1f;
            }
            float factor = 1f;
            // 在岗车间的评级档位偏移（未上岗/无类型 → 0），三个 kind 共用同一次取值。
            int ratingTierShift = WorkshopRatingShift.Of(pawn);
            factor *= ResolveStatBonusKind(skills, ProfessionalEffectKind.StatBonus, statDefName, 0f, null, ratingTierShift);
            factor *= ResolveStatBonusKind(skills, ProfessionalEffectKind.CombatStat, statDefName, 0f, null, ratingTierShift);
            factor *= ResolveStatBonusKind(skills, ProfessionalEffectKind.YieldBonus, statDefName, 0f, null, ratingTierShift);
            return factor;
        }

        private static float ResolveStatBonusKind(List<ProfessionalSkillData> skills,
            ProfessionalEffectKind kind, string statDefName, float ratingWeightKind, RecipeDef recipe,
            int ratingTierShift)
        {
            string recipeDefName = recipe != null ? recipe.defName : null;
            string recipeWorkType = recipe != null && recipe.requiredGiverWorkType != null
                ? recipe.requiredGiverWorkType.defName : null;
            return ProfessionalEffectResolver.ResolveStatBonus(
                skills,
                DefDatabase<ProfessionalSkillDef>.AllDefsListForReading,
                EffectMap(),
                kind,
                statDefName,
                recipeDefName,
                RatingList(),
                recipeWorkType,
                ratingWeightKind,
                ratingTierShift);
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
            string recipeWorkType = recipe != null && recipe.requiredGiverWorkType != null
                ? recipe.requiredGiverWorkType.defName : null;
            return ProfessionalEffectResolver.ResolveQualityLevels(
                skills,
                DefDatabase<ProfessionalSkillDef>.AllDefsListForReading,
                EffectMap(),
                recipeDefName,
                RatingList(),
                recipeWorkType,
                WorkshopRatingShift.Of(pawn));
        }

        /// <summary>
        /// 效果 Def 索引 —— **单一真源** `Domain/Profession/ProfessionalEffectIndex`。
        /// ⚠ 2026-09-15（审计 P1 修复）：**删除回落到旧 `Capture.Effects.ProfessionalEffectRegistry`
        /// 的兜底分支** —— 该分支是 `Application → Capture.Effects` 反向依赖与
        /// `Application ⇄ Capture.Effects` 循环依赖的最后残留（旧 Registry 已整体删除）。
        /// 两个字段均为 readonly 且已初始化 ⇒ 无需 null 兜底。
        /// </summary>
        private static Dictionary<string, ProfessionalEffectDef> EffectMap()
        {
            return ProfessionalEffectIndex.EffectDefs;
        }

        private static List<ProfessionalRatingDef> RatingList()
        {
            return ProfessionalEffectIndex.RatingDefs;
        }

        /// <summary>
        /// 从 pawn 取专业技能状态列表（经组件 → PawnObject.CareerData.Professional）。
        /// 任意环节缺失返回 null（零效果，不崩）。
        /// </summary>
        private static List<ProfessionalSkillData> GetSkills(Pawn pawn)
        {
            return GetEffectiveSkills(pawn);
        }

        private static int cachedSkillTick = -1;
        private static Dictionary<int, List<ProfessionalSkillData>> skillCache;

        /// <summary>
        /// 当前主修方向下的生效技能。未选主修时回退全部生效技能。
        /// 常驻 Stat 加成（GetGenericStatFactor / 属性面板）只走这条，避免 FillAll 灌满 48 技能后
        /// 把非主修方向的 StatBonus 泄漏到全局工作效率等字段。
        /// </summary>
        public static List<ProfessionalSkillData> GetPrimaryDirectionSkills(Pawn pawn)
        {
            return FilterToPrimaryDirection(pawn, GetEffectiveSkills(pawn));
        }

        /// <summary>
        /// 生效中的专业技能（档案 XP 等级与原版 sourceSkills 等级取高）。
        /// 字段面板 / StatPart 共用：殖民者还没攒够职业 XP 时，也能按原版技能立刻看到数值加成。
        /// </summary>
        public static List<ProfessionalSkillData> GetEffectiveSkills(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }
            int tick = Current.Game != null && Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (skillCache == null)
            {
                skillCache = new Dictionary<int, List<ProfessionalSkillData>>();
            }
            if (cachedSkillTick != tick)
            {
                skillCache.Clear();
                cachedSkillTick = tick;
            }
            int id = pawn.thingIDNumber;
            if (skillCache.TryGetValue(id, out List<ProfessionalSkillData> cached))
            {
                return cached;
            }
            List<ProfessionalSkillData> built = BuildEffectiveSkills(pawn);
            skillCache[id] = built;
            return built;
        }

        private static List<ProfessionalSkillData> BuildEffectiveSkills(Pawn pawn)
        {
            List<ProfessionalSkillData> stored = null;
            if (Current.Game != null)
            {
                ChronicleGameComponent component = Current.Game.GetComponent<ChronicleGameComponent>();
                PawnObject pawnObject = component != null
                    ? component.GetObject(pawn.GetUniqueLoadID()) as PawnObject
                    : null;
                if (pawnObject != null && pawnObject.CareerData != null && pawnObject.CareerData.Professional != null)
                {
                    stored = pawnObject.CareerData.Professional.skills;
                }
            }

            List<ProfessionalSkillDef> defs = DefDatabase<ProfessionalSkillDef>.AllDefsListForReading;
            List<ProfessionalSkillData> result = new List<ProfessionalSkillData>();
            HashSet<string> seen = new HashSet<string>();
            if (stored != null)
            {
                for (int i = 0; i < stored.Count; i++)
                {
                    ProfessionalSkillData src = stored[i];
                    if (src == null || string.IsNullOrEmpty(src.skillDefName) || !seen.Add(src.skillDefName))
                    {
                        continue;
                    }
                    int vanilla = VanillaSourceLevel(pawn, src.skillDefName);
                    int level = src.level > vanilla ? src.level : vanilla;
                    if (level < 1)
                    {
                        continue;
                    }
                    result.Add(CopyWithLevel(src, level));
                }
            }
            if (defs != null)
            {
                for (int i = 0; i < defs.Count; i++)
                {
                    ProfessionalSkillDef def = defs[i];
                    if (def == null || string.IsNullOrEmpty(def.defName) || seen.Contains(def.defName))
                    {
                        continue;
                    }
                    int vanilla = VanillaSourceLevel(pawn, def);
                    if (vanilla < 1)
                    {
                        continue;
                    }
                    seen.Add(def.defName);
                    result.Add(new ProfessionalSkillData
                    {
                        skillDefName = def.defName,
                        level = vanilla
                    });
                }
            }
            return result.Count == 0 ? null : result;
        }

        private static ProfessionalSkillData CopyWithLevel(ProfessionalSkillData src, int level)
        {
            if (src.level == level)
            {
                return src;
            }
            return new ProfessionalSkillData
            {
                skillDefName = src.skillDefName,
                xp = src.xp,
                level = level,
                mastery = src.mastery,
                firstAcquiredTick = src.firstAcquiredTick,
                lastPracticeTick = src.lastPracticeTick,
                practiceCount = src.practiceCount
            };
        }

        /// <summary>按职业技能 defName 取原版 sourceSkills 的最高等级。</summary>
        public static int VanillaSourceLevel(Pawn pawn, string professionalSkillDefName)
        {
            if (string.IsNullOrEmpty(professionalSkillDefName))
            {
                return 0;
            }
            ProfessionalSkillDef def = DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(professionalSkillDefName);
            return VanillaSourceLevel(pawn, def);
        }

        public static int VanillaSourceLevel(Pawn pawn, ProfessionalSkillDef def)
        {
            if (pawn == null || pawn.skills == null || def == null || def.sourceSkills == null)
            {
                return 0;
            }
            int best = 0;
            for (int i = 0; i < def.sourceSkills.Count; i++)
            {
                string name = def.sourceSkills[i];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(name);
                if (skillDef == null)
                {
                    continue;
                }
                SkillRecord rec = pawn.skills.GetSkill(skillDef);
                if (rec != null && rec.Level > best)
                {
                    best = rec.Level;
                }
            }
            return best;
        }

        private static List<ProfessionalSkillData> FilterToPrimaryDirection(Pawn pawn, List<ProfessionalSkillData> skills)
        {
            if (skills == null || skills.Count == 0 || pawn == null)
            {
                return skills;
            }
            HashSet<string> allowed = PrimaryDirectionSkillNames(pawn);
            if (allowed == null || allowed.Count == 0)
            {
                return skills;
            }
            List<ProfessionalSkillData> filtered = new List<ProfessionalSkillData>();
            for (int i = 0; i < skills.Count; i++)
            {
                ProfessionalSkillData skill = skills[i];
                if (skill != null && !string.IsNullOrEmpty(skill.skillDefName) && allowed.Contains(skill.skillDefName))
                {
                    filtered.Add(skill);
                }
            }
            return filtered.Count == 0 ? null : filtered;
        }

        private static HashSet<string> PrimaryDirectionSkillNames(Pawn pawn)
        {
            if (Current.Game == null)
            {
                return null;
            }
            ChronicleGameComponent component = Current.Game.GetComponent<ChronicleGameComponent>();
            PawnObject pawnObject = component != null
                ? component.GetObject(pawn.GetUniqueLoadID()) as PawnObject
                : null;
            string pd = pawnObject != null && pawnObject.CareerData != null && pawnObject.CareerData.Professional != null
                ? pawnObject.CareerData.Professional.primaryDirection
                : null;
            if (string.IsNullOrEmpty(pd))
            {
                return null;
            }
            HashSet<string> set = new HashSet<string>();
            ProfessionalDirectionDef dir = DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(pd);
            if (dir != null && dir.skillDefNames != null)
            {
                for (int i = 0; i < dir.skillDefNames.Count; i++)
                {
                    if (!string.IsNullOrEmpty(dir.skillDefNames[i]))
                    {
                        set.Add(dir.skillDefNames[i]);
                    }
                }
            }
            if (set.Count == 0)
            {
                set.Add(pd);
                ProfessionalSkillDef asSkill = DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(pd);
                if (asSkill != null && !string.IsNullOrEmpty(asSkill.direction))
                {
                    ProfessionalDirectionDef d2 = DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(asSkill.direction);
                    if (d2 != null && d2.skillDefNames != null)
                    {
                        for (int i = 0; i < d2.skillDefNames.Count; i++)
                        {
                            if (!string.IsNullOrEmpty(d2.skillDefNames[i]))
                            {
                                set.Add(d2.skillDefNames[i]);
                            }
                        }
                    }
                }
            }
            return set.Count == 0 ? null : set;
        }
    }
}
