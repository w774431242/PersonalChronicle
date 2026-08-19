using System.Collections.Generic;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Profession;
using RimWorld;
using Verse;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// P1 CAREER-001 职业事实层写入（CareerEvent ledger）。
    ///
    /// 设计铁律（PM 任务单 BE-002/BE-003/BE-005/BE-006）：
    /// - 只记录**事实**（人物/时间/物品Def/品质/技能），绝不写任何评价字段。
    /// - CareerEvent 与现有通用 ChronicleEvent 解耦（事实 ledger 独立于渲染事件流）。
    /// - 数据经 PawnObject.CareerData 持久化（Scribe，append-only，不 bump schema）。
    /// - 调用方须保证 worker 为玩家派系 chronicle 相关 humanlike，避免重复记录与污染。
    /// </summary>
    public sealed partial class ArchiveService
    {
        /// <summary>
        /// BE-005 第一个真实原版事件源：制造产出物品。
        /// 捕获原版 GenRecipe.PostProcessProduct 的成品，派生 ItemProduced 事实。
        /// </summary>
        public void RecordCareerProduced(Thing product, RecipeDef recipe, Pawn worker)
        {
            if (!IsRecordingEnabled() || product == null || product.def == null || worker == null)
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                // 仅记录玩家派系、chronicle 相关 humanlike（与现有捕获一致，防污染/防重复）
                if (!ChronicleColonistScanner.TryClassifyCurrent(worker, out _))
                {
                    return;
                }

                PawnObject pawnObject = EnsurePawnArchivedForCapture(component, worker);
                if (pawnObject == null || pawnObject.CareerData == null)
                {
                    return;
                }

                // 品质：从成品 CompQuality 取（无品质物品如原材料/食物为 null）
                string quality = null;
                if (product.TryGetQuality(out QualityCategory qc))
                {
                    quality = qc.ToString();
                }

                // 技能：P1 制造源技能映射（RecipeDef.requiredSkills 在 RimWorld 1.6 已移除）
                // P2 技能证据改由 sourceSkills 白名单经 ProfessionalSkillDef 判定，此处仍留空
                // （技能归属在 ApplyProfessionalProgress 内经 Def 匹配，不在此硬编码）。
                string skillDefName = null;

                long tick = Find.TickManager.TicksGame;
                string pawnId = worker.GetUniqueLoadID();
                // EventId 稳定可去重：pawn:tick:thingID（同 tick 同物多件也唯一）
                string eventId = pawnId + ":" + tick + ":" + product.thingIDNumber;

                // D2 决策落地：事实层记录 RecipeDefName + Quantity（旧事件为 null/1 兼容）
                string recipeDefName = recipe != null ? recipe.defName : null;
                int quantity = product.stackCount;

                CareerEvent ev = new CareerEvent(
                    eventId,
                    pawnId,
                    tick,
                    CareerEventType.ItemProduced,
                    product.def.defName,
                    skillDefName,
                    quality,
                    recipeDefName,
                    quantity,
                    null);

                pawnObject.CareerData.Events.Add(ev);
                IncrementRecordCount(pawnObject.CareerData, CareerEventType.ItemProduced);

                // P2 状态层派生：事实 → 专业技能 XP/能力（不污染事实，可随时重算）
                ApplyProfessionalProgress(pawnObject, recipe, quality, quantity, tick);

                // P5 实践考试证据捕获（D-E1 复用 P1 采集点，零新增 Patch）：
                // 若本次制造命中进行中的实践考试，记入证据并评分。
                RecordExamProduced(pawnObject, recipe != null ? recipe.defName : null, quality, tick);

                component.MarkChanged();

                // P1 验证通道（非 ITab）：仅 DevMode 输出，便于从 Player.log 确认
                // 制造→CareerEvent 闭环，不污染正式玩家日志。
                if (Prefs.DevMode)
                {
                    ChronicleLog.Info(ChronicleLog.Category.Capture,
                        "[CAREER] ItemProduced recorded: pawn=" + pawnId
                        + " def=" + product.def.defName
                        + " quality=" + (quality ?? "null")
                        + " eventId=" + eventId
                        + " totalEvents=" + pawnObject.CareerData.Events.Count);
                }
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive,
                    "failed to record career produced event for " + (product.def != null ? product.def.defName : "null") + ": " + ex.Message);
            }
        }

        /// <summary>
        /// P2 状态层派生：一次制造实践 → 专业技能 XP + 能力维度 XP（V2.0 §7/§8）。
        ///
        /// 仅更新 <see cref="CareerData.Professional"/>（状态），绝不反向写回 CareerEvent 事实。
        /// 匹配逻辑（数据驱动，无硬编码）：
        ///   1. 遍历全部 ProfessionalSkillDef，recipe 命中 practiceRecipeDefNames → 获得 XP；
        ///   2. XP = base × relevance × 品质系数 × difficulty × quantity；
        ///   3. 能力拆分：AbilityMappingDef 按 recipe 命中 → weights 拆分。
        /// </summary>
        private static void ApplyProfessionalProgress(PawnObject pawnObject, RecipeDef recipe, string quality, int quantity, long tick)
        {
            if (pawnObject == null || pawnObject.CareerData == null || recipe == null)
            {
                return;
            }
            try
            {
                CareerData data = pawnObject.CareerData;
                if (data.Professional == null)
                {
                    data.Professional = new ProfessionalState();
                }

                // P3 治理：品质系数表数据驱动（ProfessionalXpPolicyDef），无 Def 时 Evaluator 回退内建表。
                IReadOnlyList<QualityXpEntry> xpPolicy = ResolveXpPolicy();

                List<ProfessionalSkillDef> skillDefs = DefDatabase<ProfessionalSkillDef>.AllDefsListForReading;
                for (int i = 0; i < skillDefs.Count; i++)
                {
                    ProfessionalSkillDef skillDef = skillDefs[i];
                    if (skillDef == null)
                    {
                        continue;
                    }
                    float relevance = RecipeRelevance(skillDef, recipe);
                    if (relevance <= 0f)
                    {
                        continue;
                    }

                    float xp = ProfessionalXpEvaluator.ComputePracticeXp(
                        skillDef.xpPerPracticeBase,
                        relevance,
                        ProfessionalXpEvaluator.QualityMultiplier(quality, xpPolicy),
                        skillDef.xpDifficulty,
                        quantity);

                    ProfessionalSkillData skillData = data.Professional.GetSkill(skillDef.defName);
                    if (skillData == null)
                    {
                        skillData = new ProfessionalSkillData { skillDefName = skillDef.defName };
                        data.Professional.skills.Add(skillData);
                    }
                    if (skillData.firstAcquiredTick <= 0L)
                    {
                        skillData.firstAcquiredTick = tick;
                    }
                    skillData.xp += xp;
                    skillData.practiceCount++;
                    skillData.lastPracticeTick = tick;
                    int newLevel = ProfessionalXpEvaluator.LevelFromXp(skillData.xp, skillDef.maxLevel, skillDef.xpCap);
                    if (newLevel > skillData.level)
                    {
                        skillData.level = newLevel;
                    }
                    skillData.mastery = ProfessionalXpEvaluator.MasteryFromLevel(skillData.level, skillDef.maxLevel);

                    // 能力维度独立 XP（行为→能力映射矩阵）
                    ApplyAbilityXp(skillData, recipe, xp);

                    // 跨技能快速统计
                    if (data.Professional.practiceCountBySkill == null)
                    {
                        data.Professional.practiceCountBySkill = new Dictionary<string, int>();
                    }
                    if (data.Professional.practiceCountBySkill.ContainsKey(skillDef.defName))
                    {
                        data.Professional.practiceCountBySkill[skillDef.defName]++;
                    }
                    else
                    {
                        data.Professional.practiceCountBySkill[skillDef.defName] = 1;
                    }

                    if (Prefs.DevMode)
                    {
                        ChronicleLog.Info(ChronicleLog.Category.Capture,
                            "[CAREER] ProfessionalXP: skill=" + skillDef.defName
                            + " xp=" + xp.ToString("0.0")
                            + " relevance=" + relevance.ToString("0.00")
                            + " level=" + skillData.level);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive,
                    "failed to apply professional progress: " + ex.Message);
            }
        }

        /// <summary>
        /// 解析 XP 品质系数策略：取 DefDatabase 中第一个含品质条目的 <see cref="ProfessionalXpPolicyDef"/>
        /// （数据驱动，不硬编码 defName）。无策略 Def 时返回 null → Evaluator 回退内建表。
        /// </summary>
        private static IReadOnlyList<QualityXpEntry> ResolveXpPolicy()
        {
            List<ProfessionalXpPolicyDef> policies = DefDatabase<ProfessionalXpPolicyDef>.AllDefsListForReading;
            for (int i = 0; i < policies.Count; i++)
            {
                ProfessionalXpPolicyDef p = policies[i];
                if (p != null && p.qualityMultipliers != null && p.qualityMultipliers.Count > 0)
                {
                    return p.qualityMultipliers;
                }
            }
            return null;
        }

        /// <summary>
        /// Recipe 相关度：白名单命中=1.0；无白名单（空=全部相关）=1.0；仅 WorkType 匹配=0.5；
        /// 无匹配=0（不获得该技能 XP）。数据驱动，无硬编码技能名。
        /// </summary>
        private static float RecipeRelevance(ProfessionalSkillDef skillDef, RecipeDef recipe)
        {
            if (skillDef == null || recipe == null)
            {
                return 0f;
            }
            if (skillDef.practiceRecipeDefNames == null || skillDef.practiceRecipeDefNames.Count == 0)
            {
                // 空白名单 = 不限制配方（全部相关）
                return 1f;
            }
            if (skillDef.practiceRecipeDefNames.Contains(recipe.defName))
            {
                return 1f;
            }
            // WorkType 半匹配：配方产物的 requiredGiverWorkType（1.6 反射核验：Verse.RecipeDef.requiredGiverWorkType）
            // 命中该技能的 WorkType 白名单 → 0.5（P2 首版仅 Smithing 类，经 Def 判定）
            if (skillDef.practiceWorkTypeDefNames != null && skillDef.practiceWorkTypeDefNames.Count > 0
                && recipe.requiredGiverWorkType != null
                && skillDef.practiceWorkTypeDefNames.Contains(recipe.requiredGiverWorkType.defName))
            {
                return 0.5f;
            }
            return 0f;
        }

        /// <summary>
        /// 能力维度 XP 拆分：按 AbilityMappingDef（recipe 命中）的权重表把本次技能 XP 分配到 abilityXp。
        /// </summary>
        private static void ApplyAbilityXp(ProfessionalSkillData skillData, RecipeDef recipe, float xp)
        {
            if (skillData == null || recipe == null || xp <= 0f)
            {
                return;
            }
            AbilityMappingDef mapping = FindMappingForRecipe(recipe.defName);
            if (mapping == null)
            {
                return;
            }
            Dictionary<string, float> split = ProfessionalAbilityEvaluator.SplitAbilityXp(xp, mapping.weights);
            if (split == null || split.Count == 0)
            {
                return;
            }
            if (skillData.abilityXp == null)
            {
                skillData.abilityXp = new Dictionary<string, float>();
            }
            foreach (KeyValuePair<string, float> kv in split)
            {
                if (string.IsNullOrEmpty(kv.Key))
                {
                    continue;
                }
                if (skillData.abilityXp.ContainsKey(kv.Key))
                {
                    skillData.abilityXp[kv.Key] += kv.Value;
                }
                else
                {
                    skillData.abilityXp[kv.Key] = kv.Value;
                }
            }
        }

        /// <summary>
        /// 按 recipe defName 找能力映射 Def（第一个命中；无则 null）。
        /// </summary>
        private static AbilityMappingDef FindMappingForRecipe(string recipeDefName)
        {
            if (string.IsNullOrEmpty(recipeDefName))
            {
                return null;
            }
            List<AbilityMappingDef> mappings = DefDatabase<AbilityMappingDef>.AllDefsListForReading;
            for (int i = 0; i < mappings.Count; i++)
            {
                AbilityMappingDef m = mappings[i];
                if (m == null || m.recipeDefNames == null)
                {
                    continue;
                }
                if (m.recipeDefNames.Count == 0 || m.recipeDefNames.Contains(recipeDefName))
                {
                    return m;
                }
            }
            return null;
        }

        /// <summary>
        /// BE-003 事实聚合计数（非评价）：按 EventType 累加，供 Debug/前端快速统计。
        /// 不写入任何评价结果字段。
        /// </summary>
        private static void IncrementRecordCount(CareerData data, string eventType)
        {
            if (data.RecordCountByType == null)
            {
                data.RecordCountByType = new Dictionary<string, int>();
            }
            if (data.RecordCountByType.ContainsKey(eventType))
            {
                data.RecordCountByType[eventType]++;
            }
            else
            {
                data.RecordCountByType[eventType] = 1;
            }
        }
    }
}
