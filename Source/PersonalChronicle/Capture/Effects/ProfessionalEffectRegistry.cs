using System.Collections.Generic;
using PersonalChronicle;
using PersonalChronicle.Domain.Profession;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture.Effects
{
    /// <summary>
    /// 专业效果注册表 + StatPart 注入器（V2.0 §11 适配层，速度效果）。
    ///
    /// 启动（[StaticConstructorOnStartup]）时：
    /// 1. 索引全部 ProfessionalEffectDef（按 defName）→ 供 Service 查询；
    /// 2. 收集所有声明 WorkSpeed 效果的技能的"白名单配方"，取其 recipe.workSpeedStat 去重；
    /// 3. 对每个目标 StatDef 注入一个 <see cref="StatPart_ProfessionalWorkSpeed"/>（防重复）。
    ///
    /// 设计依据（探针核验）：RecipeDef.workSpeedStat 为 public；StatDef.parts 为 public
    /// List&lt;StatPart&gt; 可注入；制造上下文（Job.bill.recipe 等）全 public。不 Patch tick 扣减点。
    /// </summary>
    // ARC-008 受控静态状态声明：本类静态字段为「进程级只读缓存 + 受控可变索引」，属规范允许的
    // 受控单例（非游戏状态/Save/UI 状态）。生命周期：Def 加载后 [StaticConstructorOnStartup] 构建索引，
    // Def reload 时由 EnsureInjected()/Clear+重建应对；无界增长风险已通过 Def 数量上限天然约束。
    [StaticConstructorOnStartup]
    public static class ProfessionalEffectRegistry
    {
        /// <summary>全部 ProfessionalEffectDef 索引（defName → Def），供 Resolver 查询。受控静态缓存（ARC-008）。</summary>
        public static readonly Dictionary<string, ProfessionalEffectDef> EffectDefs =
            new Dictionary<string, ProfessionalEffectDef>();

        /// <summary>全部 ProfessionalRatingDef 列表（供 Resolver 评级加权查询）。受控静态缓存（ARC-008）。</summary>
        public static readonly List<ProfessionalRatingDef> RatingDefs = new List<ProfessionalRatingDef>();

        /// <summary>已注入 StatPart 的 StatDef 集合（防重复注入，reload 安全）。受控静态状态（ARC-008）。</summary>
        private static readonly HashSet<StatDef> InjectedStats = new HashSet<StatDef>();

        static ProfessionalEffectRegistry()
        {
            BuildEffectIndex();
            InjectWorkSpeedStatParts();
        }

        /// <summary>
        /// 索引全部效果 Def（按 defName）。重复 defName 后者覆盖（Def 加载顺序保证最终定义生效）。
        /// </summary>
        private static void BuildEffectIndex()
        {
            EffectDefs.Clear();
            List<ProfessionalEffectDef> all = DefDatabase<ProfessionalEffectDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && !string.IsNullOrEmpty(all[i].defName))
                {
                    EffectDefs[all[i].defName] = all[i];
                }
            }
            // 评级 Def 索引（P4 评级加权）；readonly 字段用 Clear+AddRange 原地更新
            RatingDefs.Clear();
            RatingDefs.AddRange(DefDatabase<ProfessionalRatingDef>.AllDefsListForReading);
        }

        /// <summary>
        /// 收集目标 StatDef：遍历所有声明 WorkSpeed 效果的技能 → 其白名单配方 → workSpeedStat 去重。
        /// 供静态构造注入与 <see cref="EnsureInjected"/> 幂等重校验共用（Def reload 后 stat 实例被引擎
        /// 重建，必须重新收集而非复用旧引用）。
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
                    if (EffectDefs.TryGetValue(skillDef.effectDefNames[j], out ProfessionalEffectDef effectDef)
                        && effectDef != null && effectDef.kind == ProfessionalEffectKind.WorkSpeed)
                    {
                        hasWorkSpeed = true;
                        break;
                    }
                }
                if (!hasWorkSpeed)
                {
                    continue;
                }
                // 2026-08-19 验收 P3-1 防御：空白名单技能会按"全部 RecipeDef 相关"注入全量
                // workSpeedStat（P3-A §6.1 明确只注入白名单配方）。XP 相关度语义保留
                // 空白=全相关，但注入面须显式告警，避免未来漏配白名单造成全局 stat 污染。
                if (skillDef.practiceRecipeDefNames == null || skillDef.practiceRecipeDefNames.Count == 0)
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
            StatPart_ProfessionalWorkSpeed part = new StatPart_ProfessionalWorkSpeed();
            part.parentStat = stat;
            part.priority = 100f; // 在多数原生 StatPart 之后评估，避免与基础系数竞争
            stat.parts.Add(part);
            InjectedStats.Add(stat);
        }

        /// <summary>
        /// 幂等重注入（2026-08-19 验收 P3-2 修复）：DevMode Def reload 后引擎重建 StatDef.parts，
        /// 本 Mod 的 StatPart 丢失且静态构造不会重跑。由 ChronicleGameComponent 在 reconcile 节流
        /// 中周期调用：重新收集目标并按 parts 实例检测缺失项补注入；已存在则零操作。
        /// 成本：reconcile 频率（600 tick 一次）下全量 Def 扫描量级可忽略。
        /// </summary>
        public static void EnsureInjected()
        {
            if (EffectDefs.Count == 0)
            {
                // 静态构造异常时序防御（Def 未加载完成）：重建索引后继续
                BuildEffectIndex();
                if (EffectDefs.Count == 0)
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
                for (int i = 0; i < stat.parts.Count; i++)
                {
                    if (stat.parts[i] is StatPart_ProfessionalWorkSpeed)
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
        /// 取技能白名单对应的 RecipeDef 集合（空白名单 = 全部 RecipeDef）。
        /// </summary>
        private static List<RecipeDef> CollectRecipes(ProfessionalSkillDef skillDef)
        {
            if (skillDef.practiceRecipeDefNames == null || skillDef.practiceRecipeDefNames.Count == 0)
            {
                return DefDatabase<RecipeDef>.AllDefsListForReading;
            }
            List<RecipeDef> result = new List<RecipeDef>();
            for (int i = 0; i < skillDef.practiceRecipeDefNames.Count; i++)
            {
                RecipeDef recipe = DefDatabase<RecipeDef>.GetNamed(skillDef.practiceRecipeDefNames[i], false);
                if (recipe != null)
                {
                    result.Add(recipe);
                }
            }
            return result;
        }
    }
}
