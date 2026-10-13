using System;
using System.Collections.Generic;
using PersonalChronicle.Application.Effects;
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

                // P0 书籍证据捕获（复用 P1 采集点，零新增 Patch，对齐 D-E1 范式）：
                // 制造产物若为本 mod 考试书籍（PC_Exam_Textbook/SkillBook/TheoryBook/ResearchDoc），
                // 同步构造 BookEvidence 存入 CareerData.Books（理论证据，V2.0 §17）并回写 BookProduced 事实。
                TryRecordBookEvidence(pawnObject, product, worker, quality, tick);

                // V2.1：专业技能 XP/能力不再由制造事实派生（recipe 白名单次数制实测无效），
                // 统一改用原版技能经验链路：Patch SkillRecord.Learn → RecordSkillLearn（本文件下方）。
                // 本方法仅保留 ItemProduced 事实 ledger / 书籍证据 / 实践考试三条线。

                // P5 实践考试证据捕获（D-E1 复用 P1 采集点，零新增 Patch）：
                // 若本次制造命中进行中的实践考试，记入证据并评分。
                RecordExamProduced(pawnObject, recipe != null ? recipe.defName : null, quality, tick);

                // P1-BUG：制造产出必须同步写入 ProductionAccumulator，否则产出宫格
                // （ITab_Pawn_Chronicle 六宫格②）因 GetProductionSummary 读不到累加器
                // 而始终显示 "--"。原实现只写了 CareerData.Events 事实 ledger，漏写累加器。
                // 与 ArchiveService.Battle.cs 的建造捕获保持一致：累加 quantity 与总市值。
                int prodQty = Math.Max(1, product.stackCount);
                float unitValue = product.MarketValue;
                if (float.IsNaN(unitValue) || float.IsInfinity(unitValue) || unitValue < 0f)
                {
                    unitValue = 0f;
                }
                component.AddProduction(pawnId, product.def.defName, prodQty, unitValue * prodQty, tick);

                component.MarkChanged();

                // P1 验证通道（非 ITab）：仅 DevMode 输出，便于从 Player.log 确认
                // 制造→CareerEvent 闭环，不污染正式玩家日志。
                if (Prefs.DevMode)
                {
                    string recipeName = recipe != null ? recipe.defName : "NULL";
                    ChronicleColonistScanner.TryClassifyCurrent(worker, out PawnRole dbgRole);
                    ChronicleLog.Info(ChronicleLog.Category.Capture,
                        "[CAREER] ItemProduced recorded: pawn=" + pawnId
                        + " def=" + product.def.defName
                        + " recipe=" + recipeName
                        + " quality=" + (quality ?? "null")
                        + " qty=" + quantity
                        + " role=" + dbgRole
                        + " hasProfessional=" + (pawnObject.CareerData.Professional != null)
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
        /// P0 书籍证据捕获：制造产物为本 mod 考试书籍（PC_Exam_Textbook/SkillBook/TheoryBook/ResearchDoc）时，
        /// 写 BookProduced 事实 + 构造 BookEvidence 存入 CareerData.Books（理论证据，V2.0 §17）。
        /// 复用 P1 制造采集点（零新增 Harmony Patch，与 RecordExamProduced 同范式）。
        /// 防重复：按 eventId（pawn:tick:thingIDNumber）天然唯一；Books 追加不判重（与 DevTest 一致）。
        /// </summary>
        private static void TryRecordBookEvidence(PawnObject pawnObject, Thing product, Pawn worker, string quality, long tick)
        {
            try
            {
                if (pawnObject == null || pawnObject.CareerData == null || product == null || product.def == null)
                {
                    return;
                }
                // 仅本 mod 考试书籍视为"著作"证据（原版 Book 由贸易/任务获得，无玩家制造路径）。
                string defName = product.def.defName;
                string bookField = null;
                if (defName.StartsWith("PC_Exam_Textbook", System.StringComparison.Ordinal))
                {
                    bookField = "Textbook";
                }
                else if (defName.StartsWith("PC_Exam_SkillBook", System.StringComparison.Ordinal))
                {
                    bookField = "SkillBook";
                }
                else if (defName.StartsWith("PC_Exam_TheoryBook", System.StringComparison.Ordinal))
                {
                    bookField = "TheoryBook";
                }
                else if (defName.StartsWith("PC_Exam_ResearchDoc", System.StringComparison.Ordinal))
                {
                    bookField = "ResearchDoc";
                }
                else
                {
                    return;
                }

                string pawnId = worker.GetUniqueLoadID();
                string eventId = pawnId + ":" + tick + ":" + product.thingIDNumber + ":book";
                pawnObject.CareerData.Events.Add(new CareerEvent(
                    eventId, pawnId, tick, CareerEventType.BookProduced,
                    defName, null, quality, null, product.stackCount, null));
                IncrementRecordCount(pawnObject.CareerData, CareerEventType.BookProduced);

                RecordBookEvidence(
                    pawnObject, product.ThingID, pawnId, defName, quality, bookField, tick);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive,
                    "failed to record book evidence: " + ex.Message);
            }
        }

        /// <summary>
        /// V2.1 职业 XP 采集（原版链路）：一次原版技能经验注入 → 职业技能 XP。
        ///
        /// 由 <see cref="Patch_SkillRecord_Learn"/>（Postfix on SkillRecord.Learn）调用。
        /// actualXp = 原版增量 × LearnRateFactor（Passion/学习速率修正，已由 Patch 计算），
        /// 本方法再乘本 mod XpGrowth 效果系数，经 ProfessionalSkillDef.sourceSkills 映射累加。
        ///
        /// 铁律：
        /// - 只累加、永不减少 → 无每日上限、无衰退（append-only）。
        /// - 仅更新 <see cref="CareerData.Professional"/>（状态），绝不反向写回 CareerEvent 事实。
        /// - 匹配走 ProfessionalSkillSourceCache（sourceSkills），数据驱动无硬编码。
        /// </summary>
        public void RecordSkillLearn(Pawn pawn, string vanillaSkillName, float actualXp, long tick, bool countPractice = true)
        {
            if (!IsRecordingEnabled() || pawn == null || string.IsNullOrEmpty(vanillaSkillName) || actualXp <= 0f)
            {
                return;
            }
            try
            {
                // 仅玩家派系 chronicle 相关 humanlike（与既有捕获一致，防污染/防重复）
                if (!ChronicleColonistScanner.TryClassifyCurrent(pawn, out _))
                {
                    return;
                }
                // 首道过滤（缓存已由 Patch 层查过一次，此处再查是防御性，成本为字典查找）
                List<ProfessionalSkillDef> matched = ProfessionalSkillSourceCache.Get(vanillaSkillName);
                if (matched == null || matched.Count == 0)
                {
                    return;
                }
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                PawnObject pawnObject = EnsurePawnArchivedForCapture(component, pawn);
                if (pawnObject == null || pawnObject.CareerData == null)
                {
                    return;
                }
                CareerData data = pawnObject.CareerData;
                if (data.Professional == null)
                {
                    data.Professional = new ProfessionalState();
                }

                // P0 v2.0 学习系数（XpGrowth）：技能声明 XpBonus 效果 → 该技能获得经验 × (1 + Σvalue)。
                float xpGrowthFactor = ProfessionalEffectService.GetGenericStatFactor(pawn, "XpGrowth");

                for (int i = 0; i < matched.Count; i++)
                {
                    ProfessionalSkillDef skillDef = matched[i];
                    if (skillDef == null)
                    {
                        continue;
                    }
                    float xp = actualXp * xpGrowthFactor;

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
                    // 只增不减：无每日上限、无衰退（append-only 铁律）
                    skillData.xp += xp;
                    // V2.0+ 通用体系：精确来源拆分（key=原版技能 defName），供浮层来源占比计算。
                    skillData.AddXpBySource(vanillaSkillName, xp);
                    // practiceCount：仅大额经验（≥1）记一次实践，滤掉工作每 tick 微量增量，避免数值爆炸。
                    // v2.3 整件结算：制造完成一次性注入时 countPractice=false（实践由 RecordCraftCompleted 独家记）
                    if (countPractice && xp >= 1f)
                    {
                        skillData.practiceCount++;
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
                    }
                    skillData.lastPracticeTick = tick;
                    int newLevel = ProfessionalXpEvaluator.LevelFromXp(skillData.xp, skillDef.maxLevel, skillDef.xpCap, ProfessionalXpPolicyDef.DefaultCurvePower);
                    if (newLevel > skillData.level)
                    {
                        skillData.level = newLevel;
                    }
                    skillData.mastery = ProfessionalXpEvaluator.MasteryFromLevel(skillData.level, skillDef.maxLevel);

                    // 能力维度独立 XP（方向映射矩阵）
                    ApplyAbilityXpForSkill(skillData, skillDef, xp);
                }
                component.MarkChanged();
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive,
                    "failed to record skill learn professional progress: " + ex.Message);
            }
        }

        /// <summary>
        /// V2.3 制造完成实践统计（v2.1 的 XP 结算已由「整件结算制」取代）。
        ///
        /// 由 <see cref="Patch_BillProduction"/>（Postfix on Bill_Production.Notify_IterationCompleted）
        /// 调用——所有工作台/简易制作点制造完成的统一回调（含 mod 自定义制造，
        /// 覆盖 GenRecipe.PostProcessProduct 之外的长尾路径）。
        ///
        /// v2.3 变更：制造经验不再按 workAmount/150 自造公式结算（旧 bonus），改由
        /// Patch_BillProduction 先 Flush <see cref="CraftXpSettlement"/> 中拦截缓存的原版
        /// tick 经验 → 原生 pawn.skills.Learn(workSkill, total) 一次性注入 → 原版公式
        /// （Passion/学习速率）自动生效 → 本 service 的 <see cref="RecordSkillLearn"/>
        /// 分流职业 XP。产出导向、无双重计数。本方法仅保留「制造即实践」统计
        /// （practiceCount / practiceCountBySkill / lastPracticeTick），XP 归零。
        ///
        /// 铁律（与 RecordSkillLearn 一致）：
        /// - 只累加、永不减少 → 无每日上限、无衰退（append-only）。
        /// - 仅更新 <see cref="CareerData.Professional"/>（状态），绝不反向写回 CareerEvent 事实。
        /// - 匹配走 ProfessionalSkillSourceCache（recipe.workSkill → sourceSkills），数据驱动。
        /// </summary>
        public void RecordCraftCompleted(Pawn worker, RecipeDef recipe, long tick)
        {
            if (!IsRecordingEnabled() || worker == null || recipe == null)
            {
                return;
            }
            if (recipe.workSkill == null || recipe.workAmount <= 0)
            {
                return;
            }
            try
            {
                // 仅玩家派系 chronicle 相关 humanlike（与既有捕获一致，防污染/防重复）
                if (!ChronicleColonistScanner.TryClassifyCurrent(worker, out _))
                {
                    return;
                }
                // 配方工作技能 → 职业技能映射（缓存字典查找；无命中零成本返回）
                List<ProfessionalSkillDef> matched = ProfessionalSkillSourceCache.Get(recipe.workSkill.defName);
                if (matched == null || matched.Count == 0)
                {
                    return;
                }
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                PawnObject pawnObject = EnsurePawnArchivedForCapture(component, worker);
                if (pawnObject == null || pawnObject.CareerData == null)
                {
                    return;
                }
                CareerData data = pawnObject.CareerData;
                if (data.Professional == null)
                {
                    data.Professional = new ProfessionalState();
                }

                // v2.3 整件结算制：制造 XP 已在 Patch_BillProduction 经原生 pawn.skills.Learn
                // 一次性注入并分流（RecordSkillLearn），此处不再重复加 XP（无双重计数）。
                // 仅保留「制造即实践」统计：实践计数 / 分技能计数 / 时间戳。
                for (int i = 0; i < matched.Count; i++)
                {
                    ProfessionalSkillDef skillDef = matched[i];
                    if (skillDef == null)
                    {
                        continue;
                    }
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
                    // 制造完成记一次实践（与"大额经验记实践"口径互补，制造即实践）
                    skillData.practiceCount++;
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
                    skillData.lastPracticeTick = tick;
                    int newLevel = ProfessionalXpEvaluator.LevelFromXp(skillData.xp, skillDef.maxLevel, skillDef.xpCap, ProfessionalXpPolicyDef.DefaultCurvePower);
                    if (newLevel > skillData.level)
                    {
                        skillData.level = newLevel;
                    }
                    skillData.mastery = ProfessionalXpEvaluator.MasteryFromLevel(skillData.level, skillDef.maxLevel);

                    // v2.3：能力维度 XP 由整件注入分流（RecordSkillLearn → ApplyAbilityXpForSkill），
                    // 此处不再重复贡献（制造即实践仅记实践计数）。

                    if (Prefs.DevMode)
                    {
                        ChronicleLog.Info(ChronicleLog.Category.Capture,
                            "[CAREER] CraftCompleted(via Bill_Production): skill=" + skillDef.defName
                            + " recipe=" + recipe.defName
                            + " workAmount=" + recipe.workAmount
                            + " practice=" + skillData.practiceCount
                            + " level=" + skillData.level);
                    }
                }
                component.MarkChanged();
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive,
                    "failed to record craft completed professional progress: " + ex.Message);
            }
        }

        /// <summary>
        /// 能力维度 XP 拆分：按技能方向的 AbilityMappingDef（Mapping_&lt;direction&gt;）的权重表
        /// 把一次原版技能经验增量分配到 abilityXp。无方向映射/无权重表时不分配。
        /// V2.1：原版链路采集无 recipe 上下文，移除 recipe 回退分支（FindMappingForRecipe 已废弃）。
        /// </summary>
        private static void ApplyAbilityXpForSkill(ProfessionalSkillData skillData, ProfessionalSkillDef skillDef, float xp)
        {
            if (skillData == null || xp <= 0f || skillDef == null || string.IsNullOrEmpty(skillDef.direction))
            {
                return;
            }
            AbilityMappingDef mapping = DefDatabase<AbilityMappingDef>.GetNamedSilentFail("Mapping_" + skillDef.direction);
            if (mapping == null || mapping.weights == null || mapping.weights.Count == 0)
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
        /// BE-005 建造事实源：完成一座建筑（采集点 <see cref="PersonalChronicle.Capture.Patch_GenConstruct"/>，
        /// Postfix on Frame.CompleteConstruction(Pawn)，每座建筑恰好一次）。
        /// 写 CareerEventType.ConstructionCompleted 事实 + RecordCountByType 聚合。
        /// 铁律同 RecordCareerProduced：只记事实，不写评价；建筑不入装备归档（与 OnThingBuilt 分工）。
        /// </summary>
        public void RecordCareerConstruction(Pawn worker, ThingDef builtDef, string builtStableId, long tick)
        {
            if (!IsRecordingEnabled() || worker == null || builtDef == null)
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
                if (!ChronicleColonistScanner.TryClassifyCurrent(worker, out _))
                {
                    return;
                }
                PawnObject pawnObject = EnsurePawnArchivedForCapture(component, worker);
                if (pawnObject == null || pawnObject.CareerData == null)
                {
                    return;
                }

                string pawnId = worker.GetUniqueLoadID();
                string eventId = pawnId + ":" + tick + ":" + (builtStableId ?? builtDef.defName) + ":con";

                CareerEvent ev = new CareerEvent(
                    eventId,
                    pawnId,
                    tick,
                    CareerEventType.ConstructionCompleted,
                    builtDef.defName,
                    null,
                    null,
                    null,
                    1,
                    null);

                pawnObject.CareerData.Events.Add(ev);
                IncrementRecordCount(pawnObject.CareerData, CareerEventType.ConstructionCompleted);
                component.MarkChanged();
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive,
                    "failed to record construction career event: " + ex.Message);
            }
        }

        /// <summary>
        /// BE-005 研究事实源：完成一项研究（采集点 Patch_ResearchFinishProject，
        /// Postfix on ResearchManager.FinishProject——引擎所有研究完成路径的统一出口，
        /// 1.6 签名 (ResearchProjectDef, bool, Pawn, bool)，第三参即完成研究的研究者）。
        /// 防重复：同一研究项目只记一次（防御 dev/多 mod 重复触发 FinishProject）。
        /// </summary>
        public void RecordCareerResearch(Pawn researcher, string researchDefName, long tick)
        {
            if (!IsRecordingEnabled() || researcher == null || string.IsNullOrEmpty(researchDefName))
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
                if (!ChronicleColonistScanner.TryClassifyCurrent(researcher, out _))
                {
                    return;
                }
                PawnObject pawnObject = EnsurePawnArchivedForCapture(component, researcher);
                if (pawnObject == null || pawnObject.CareerData == null)
                {
                    return;
                }
                CareerData data = pawnObject.CareerData;

                // 防重复：同项目已记过则跳过（FinishProject 理论单次，此处防御性兜底）。
                List<CareerEvent> events = data.Events;
                for (int i = 0; i < events.Count; i++)
                {
                    CareerEvent e = events[i];
                    if (e != null
                        && string.Equals(e.EventType, CareerEventType.ResearchCompleted, System.StringComparison.Ordinal)
                        && string.Equals(e.DefName, researchDefName, System.StringComparison.Ordinal))
                    {
                        return;
                    }
                }

                string pawnId = researcher.GetUniqueLoadID();
                string eventId = pawnId + ":" + tick + ":" + researchDefName + ":res";

                CareerEvent ev = new CareerEvent(
                    eventId,
                    pawnId,
                    tick,
                    CareerEventType.ResearchCompleted,
                    researchDefName,
                    null,
                    null,
                    null,
                    1,
                    null);

                data.Events.Add(ev);
                IncrementRecordCount(data, CareerEventType.ResearchCompleted);
                component.MarkChanged();
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive,
                    "failed to record research career event: " + ex.Message);
            }
        }

        /// <summary>
        /// BE-005 基础劳作事实源：WorkCompleted 量子派生（采集点 ChronicleGameComponent
        /// 工时采样器）。每累计 <see cref="WorkCompletedQuantumTicks"/>（=游戏 1 小时）工作
        /// 记 1 条事实，与 UI「每事件=1h」口径一致（CareerSpanBreakdown 每事件记 1h）。
        ///
        /// 无状态推导：应发事件数 = TotalWorkTicks / quantum（向下取整），已发事件数取自
        /// RecordCountByType[WorkCompleted]——两者均为持久化数据，存档重载后自动对齐，
        /// 零新增 Save schema、零运行时字段。逐次调用幂等（差额不足一个 quantum 时零写入）。
        /// </summary>
        public void RecordWorkCompletedQuantum(string pawnStableId, long tick)
        {
            if (!IsRecordingEnabled() || string.IsNullOrEmpty(pawnStableId))
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
                PawnObject pawnObject = component.GetObject(pawnStableId) as PawnObject;
                if (pawnObject == null || pawnObject.IsArchived || pawnObject.CareerData == null
                    || pawnObject.WorkTime == null)
                {
                    return;
                }
                CareerData data = pawnObject.CareerData;
                long totalTicks = pawnObject.WorkTime.TotalWorkTicks;
                if (totalTicks <= 0L)
                {
                    return;
                }
                int done = 0;
                if (data.RecordCountByType != null)
                {
                    data.RecordCountByType.TryGetValue(CareerEventType.WorkCompleted, out done);
                }
                long due = totalTicks / WorkCompletedQuantumTicks;
                int emitted = 0;
                while (done < due)
                {
                    done++;
                    emitted++;
                    string eventId = pawnStableId + ":" + tick + ":work:" + done;
                    Dictionary<string, string> meta = new Dictionary<string, string>();
                    meta["ticks"] = WorkCompletedQuantumTicks.ToString();
                    data.Events.Add(new CareerEvent(
                        eventId,
                        pawnStableId,
                        tick,
                        CareerEventType.WorkCompleted,
                        null,
                        null,
                        null,
                        null,
                        1,
                        meta));
                    IncrementRecordCount(data, CareerEventType.WorkCompleted);
                }
                if (emitted > 0)
                {
                    component.MarkChanged();
                }
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive,
                    "failed to record work completed quantum: " + ex.Message);
            }
        }

        /// <summary>WorkCompleted 量子：2500 tick = 游戏 1 小时（对齐 UI 每事件 1h 语义）。</summary>
        internal const long WorkCompletedQuantumTicks = 2500L;

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
