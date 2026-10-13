using System.Collections.Generic;
using PersonalChronicle;
using PersonalChronicle.Domain.Profession;
using RimWorld;
using Verse;

namespace PersonalChronicle.Effects
{
    /// <summary>
    /// 专业效果 → RimWorld StatPart 注入器（V2.0 §11「RimWorldAdapter」+ §21 顶层 Effects/ 目录）。
    ///
    /// [GOV-006 评审 2026-08-29] 原 Capture/Effects/ProfessionalEffectRegistry 混合了两类职责：
    ///   ①效果 Def 索引（纯数据，已析出至 Domain/Profession/ProfessionalEffectIndex）
    ///   ②StatPart 注入（RimWorld 适配，本类）。
    /// 混合导致 Application 为取索引而反向依赖 Capture、且本类所在的 Capture 又反向依赖 Application，
    /// 双向违反分层单向。按 V2.0 §11（Effect → EffectResolver → RimWorldAdapter 必须独立）拆分后：
    ///   Effects（适配层）→ Application → Domain —— 全部为合法下行依赖。
    ///
    /// 启动（[StaticConstructorOnStartup]）时：
    /// 1. 幂等重建 Def 索引（规避与 ProfessionalEffectIndex 静态构造的先后顺序不确定性）；
    /// 2. 收集所有声明 WorkSpeed 效果的技能的"白名单配方"，取其 recipe.workSpeedStat 去重；
    /// 3. 对每个目标 StatDef 注入一个 StatPart（防重复）。
    ///
    /// 设计依据（探针核验）：RecipeDef.workSpeedStat 为 public；StatDef.parts 为 public
    /// List&lt;StatPart&gt; 可注入；制造上下文（Job.bill.recipe 等）全 public。不 Patch tick 扣减点。
    ///
    /// ARC-008 受控静态状态声明：InjectedStats 为「进程级受控可变索引」（非游戏状态/Save/UI 状态）。
    /// 生命周期：Def 加载后构建；Def reload 时由 EnsureInjected() 幂等补注入；
    /// 增长受 Def 数量上限天然约束。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ProfessionalEffectStatPartInjector
    {
        /// <summary>已注入 StatPart 的 StatDef 集合（防重复注入，reload 安全）。受控静态状态（ARC-008）。</summary>
        private static readonly HashSet<StatDef> InjectedStats = new HashSet<StatDef>();

        static ProfessionalEffectStatPartInjector()
        {
            // 两个 [StaticConstructorOnStartup] 类的静态构造触发顺序由引擎决定，
            // 此处显式幂等重建索引，确保注入时索引一定已就绪。
            ProfessionalEffectIndex.Rebuild();
            InjectWorkSpeedStatParts();
        }

        /// <summary>
        /// 收集目标 StatDef：遍历所有声明效果的技能 →
        ///   WorkSpeed → 白名单配方的 workSpeedStat；
        ///   StatBonus/CombatStat/YieldBonus → 技能声明 effectDefNames 中 statDefName 指向的官方 Stat。
        /// 供静态构造注入与 <see cref="EnsureInjected"/> 幂等重校验共用（Def reload 后 stat 实例被引擎
        /// 重建，必须重新收集而非复用旧引用）。
        /// 2026-08-22 v2.0：支持多维加成（StatBonus/CombatStat/YieldBonus）的 statDefName 目标注入。
        /// </summary>
        private static HashSet<StatDef> CollectTargetStats()
        {
            HashSet<StatDef> targets = new HashSet<StatDef>();
            List<ProfessionalSkillDef> skillDefs = DefDatabase<ProfessionalSkillDef>.AllDefsListForReading;
            for (int i = 0; i < skillDefs.Count; i++)
            {
                ProfessionalSkillDef skillDef = skillDefs[i];
                if (skillDef == null || skillDef.effectDefNames == null)
                {
                    continue;
                }
                bool hasWorkSpeed = false;
                for (int j = 0; j < skillDef.effectDefNames.Count; j++)
                {
                    if (ProfessionalEffectIndex.EffectDefs.TryGetValue(skillDef.effectDefNames[j], out ProfessionalEffectDef effectDef))
                    {
                        if (effectDef == null)
                        {
                            continue;
                        }
                        if (effectDef.kind == ProfessionalEffectKind.WorkSpeed)
                        {
                            hasWorkSpeed = true;
                        }
                        else if ((effectDef.kind == ProfessionalEffectKind.StatBonus
                                  || effectDef.kind == ProfessionalEffectKind.CombatStat
                                  || effectDef.kind == ProfessionalEffectKind.YieldBonus)
                                 && !string.IsNullOrEmpty(effectDef.statDefName))
                        {
                            // 通用 Stat 目标：按 statDefName 注入（防重复）
                            StatDef targetStat = DefDatabase<StatDef>.GetNamedSilentFail(effectDef.statDefName);
                            if (targetStat != null)
                            {
                                targets.Add(targetStat);
                            }
                        }
                    }
                }
                if (!hasWorkSpeed)
                {
                    continue;
                }
                // 2026-08-19 验收 P3-1 防御：空白名单技能会按"全部 RecipeDef 相关"注入全量
                // workSpeedStat（P3-A §6.1 明确只注入白名单配方）。XP 相关度语义保留
                // 空白=全相关，但注入面须显式告警，避免未来漏配白名单造成全局 stat 污染。
                // 2026-08-22 P0 批1：practiceWorkTypeDefNames（工程/武器等动态配方场景）视为
                // 有效白名单，不再走"全量注入"分支，也不误刷 EMPTY 告警。
                if ((skillDef.practiceRecipeDefNames == null || skillDef.practiceRecipeDefNames.Count == 0)
                    && (skillDef.practiceWorkTypeDefNames == null || skillDef.practiceWorkTypeDefNames.Count == 0))
                {
                    ChronicleLog.Warning(ChronicleLog.Category.Capture,
                        "[CAREER] ProfessionalSkillDef " + skillDef.defName
                        + " declares WorkSpeed effect but has EMPTY practiceRecipeDefNames: "
                        + "StatPart will be injected into ALL recipe workSpeedStats. "
                        + "Recommend adding an explicit recipe whitelist (V2.0 §12/P3-A §6.1).");
                }
                List<RecipeDef> recipes = CollectRecipes(skillDef);
                for (int k = 0; k < recipes.Count; k++)
                {
                    RecipeDef recipe = recipes[k];
                    if (recipe != null && recipe.workSpeedStat != null)
                    {
                        targets.Add(recipe.workSpeedStat);
                    }
                }
            }
            return targets;
        }

        /// <summary>
        /// 静态构造注入（进程启动一次）。此后由 <see cref="EnsureInjected"/> 负责 DevMode Def reload
        /// 后的幂等补注入（reload 会重建 StatDef.parts，本 Mod 的 StatPart 会丢失）。
        /// </summary>
        private static void InjectWorkSpeedStatParts()
        {
            HashSet<StatDef> targets = CollectTargetStats();
            foreach (StatDef stat in targets)
            {
                InjectInto(stat);
            }
        }

        private static void InjectInto(StatDef stat)
        {
            if (stat == null || InjectedStats.Contains(stat))
            {
                return;
            }
            if (stat.parts == null)
            {
                stat.parts = new List<StatPart>();
            }
            // 2026-08-22 v2.0：按 stat 是否被 WorkSpeed 效果声明决定注入类型——
            // WorkSpeed 目标（工作台 workSpeedStat）注入 StatPart_ProfessionalWorkSpeed（制造上下文）；
            // 其余（StatBonus/CombatStat/YieldBonus 的 statDefName 目标）注入通用 StatPart_ProfessionalStatBonus。
            bool isWorkSpeedTarget = IsWorkSpeedTargetStat(stat);
            if (isWorkSpeedTarget)
            {
                StatPart_ProfessionalWorkSpeed part = new StatPart_ProfessionalWorkSpeed();
                part.parentStat = stat;
                part.priority = 100f; // 在多数原生 StatPart 之后评估，避免与基础系数竞争
                stat.parts.Add(part);
            }
            else
            {
                StatPart_ProfessionalStatBonus part = new StatPart_ProfessionalStatBonus();
                part.parentStat = stat;
                part.priority = 100f;
                stat.parts.Add(part);
            }
            InjectedStats.Add(stat);
        }

        /// <summary>
        /// 判断目标 Stat 是否为 WorkSpeed 效果声明（注入制造上下文 StatPart）。
        /// 2026-08-22 v2.0：按 effectDefNames 中 WorkSpeed 效果的 statDefName 判定；
        /// 兼容旧版（仅 recipe.workSpeedStat 收集，无 statDefName）→ 视为 WorkSpeed 目标。
        /// </summary>
        private static bool IsWorkSpeedTargetStat(StatDef stat)
        {
            if (stat == null)
            {
                return false;
            }
            List<ProfessionalSkillDef> skillDefs = DefDatabase<ProfessionalSkillDef>.AllDefsListForReading;
            for (int i = 0; i < skillDefs.Count; i++)
            {
                ProfessionalSkillDef skillDef = skillDefs[i];
                if (skillDef == null || skillDef.effectDefNames == null)
                {
                    continue;
                }
                for (int j = 0; j < skillDef.effectDefNames.Count; j++)
                {
                    if (!ProfessionalEffectIndex.EffectDefs.TryGetValue(skillDef.effectDefNames[j], out ProfessionalEffectDef effectDef)
                        || effectDef == null || effectDef.kind != ProfessionalEffectKind.WorkSpeed)
                    {
                        continue;
                    }
                    if (string.Equals(effectDef.statDefName, stat.defName, System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                    if (string.IsNullOrEmpty(effectDef.statDefName))
                    {
                        List<RecipeDef> recipes = CollectRecipes(skillDef);
                        for (int k = 0; k < recipes.Count; k++)
                        {
                            if (recipes[k] != null && recipes[k].workSpeedStat == stat)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 幂等重注入（2026-08-19 验收 P3-2 修复）：DevMode Def reload 后引擎重建 StatDef.parts，
        /// 本 Mod 的 StatPart 丢失且静态构造不会重跑。由 ChronicleGameComponent 在 reconcile 节流
        /// 中周期调用：重新收集目标并按 parts 实例检测缺失项补注入；已存在则零操作。
        /// 成本：reconcile 频率（600 tick 一次）下全量 Def 扫描量级可忽略。
        /// </summary>
        public static void EnsureInjected()
        {
            if (ProfessionalEffectIndex.EffectDefs.Count == 0)
            {
                // 静态构造异常时序防御（Def 未加载完成）：重建索引后继续
                ProfessionalEffectIndex.Rebuild();
                if (ProfessionalEffectIndex.EffectDefs.Count == 0)
                {
                    return;
                }
            }
            HashSet<StatDef> targets = CollectTargetStats();
            foreach (StatDef stat in targets)
            {
                if (stat == null || stat.parts == null)
                {
                    InjectInto(stat);
                    continue;
                }
                bool has = false;
                bool wantWorkSpeed = IsWorkSpeedTargetStat(stat);
                for (int i = 0; i < stat.parts.Count; i++)
                {
                    StatPart p = stat.parts[i];
                    if (wantWorkSpeed && p is StatPart_ProfessionalWorkSpeed)
                    {
                        has = true;
                        break;
                    }
                    if (!wantWorkSpeed && p is StatPart_ProfessionalStatBonus)
                    {
                        has = true;
                        break;
                    }
                }
                if (!has)
                {
                    InjectInto(stat);
                }
            }
        }

        /// <summary>
        /// 取技能白名单对应的 RecipeDef 集合（双白名单皆空 = 全部 RecipeDef）。
        /// P0 批1（2026-08-22）：支持 practiceWorkTypeDefNames——按 recipe.requiredGiverWorkType
        /// 匹配收集（工程/武器等动态配方场景，静态 Recipe 白名单不可行）。
        /// </summary>
        private static List<RecipeDef> CollectRecipes(ProfessionalSkillDef skillDef)
        {
            bool hasRecipeList = skillDef.practiceRecipeDefNames != null && skillDef.practiceRecipeDefNames.Count > 0;
            bool hasWorkTypeList = skillDef.practiceWorkTypeDefNames != null && skillDef.practiceWorkTypeDefNames.Count > 0;
            if (!hasRecipeList && !hasWorkTypeList)
            {
                return DefDatabase<RecipeDef>.AllDefsListForReading;
            }
            List<RecipeDef> result = new List<RecipeDef>();
            List<RecipeDef> all = DefDatabase<RecipeDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                RecipeDef recipe = all[i];
                if (recipe == null)
                {
                    continue;
                }
                if (hasRecipeList && skillDef.practiceRecipeDefNames.Contains(recipe.defName))
                {
                    result.Add(recipe);
                    continue;
                }
                if (hasWorkTypeList && recipe.requiredGiverWorkType != null
                    && skillDef.practiceWorkTypeDefNames.Contains(recipe.requiredGiverWorkType.defName))
                {
                    result.Add(recipe);
                }
            }
            return result;
        }
    }
}
