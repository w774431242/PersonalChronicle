using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Effects
{
    /// <summary>
    /// 车间 StatPart 注入器（落地管线 Step 5 配套）。
    ///
    /// 与 <see cref="ProfessionalEffectStatPartInjector"/> 同构：启动时把各 StatPart
    /// 注入到目标 StatDef（去重、防重复、Def reload 后幂等补注入）。
    ///
    /// 三类目标，按 stat 名分派不同 Part：
    ///   · 配方 workSpeedStat + WorkSpeedGlobal → <see cref="StatPart_WorkshopWorkSpeed"/>（工时）
    ///   · MaxHitPoints                          → <see cref="StatPart_WorkshopMaxHp"/>（耐久）
    ///   · AccuracyTouch/Short/Medium/Long       → <see cref="StatPart_WorkshopAccuracy"/>（精度）
    ///
    /// 只注入一次，之后运行期零额外开销——是否生效由 StatPart 内部判断
    /// （是否上岗 / 该成品是否登记），与 Def 本身无关。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class WorkshopStatPartInjector
    {
        /// <summary>
        /// 已注入的 StatDef 集合（防重复，reload 安全）。
        ///
        /// <para><b>ARC-008 受控静态状态声明</b>（2026-09-21 补 · CHG-SHOP-AUDIT-001 · A-6）：
        /// 进程级**只增不放**索引，容量 = 目标 StatDef 集（配方的 workSpeedStat ∪ 六个原版
        /// 成品 stat）⇒ 天然有界；元素为原版 `StatDef` 引用（Def 生命周期与进程一致，
        /// 无 GC 顾虑）；不入 Scribe（无存档污染）；Def reload 后 `StatDef.parts` 被重建的
        /// 情形由 <see cref="EnsureInjected"/> 幂等补偿，故无需其它生命周期钩子。</para>
        /// </summary>
        private static readonly HashSet<StatDef> InjectedStats = new HashSet<StatDef>();

        /// <summary>
        /// 加成注入总开关（<b>当前 = 启用</b>）。
        ///
        /// <para>⚠ <b>CHG-20261005-WSDOCFIX（注释漂移修正）</b>：原文写"2026-09-25 加成体系移除"
        /// —— 那是当时的**临时**状态，此后已恢复启用而注释未同步，读起来像是"当前已停用"。
        /// <b>事实依据</b>：<c>Defs/WorkshopTypes.xml</c> 的 9 个车间类型都填了加成实值
        /// （<c>workSpeedBonusPct</c> 10~12、<c>qualityBonus</c> 1、研发车间 <c>researchSpeedBonusPct</c> 20）
        /// ⇒ 置 false 会让这些配置**静默失效**，故当前必须为 true。</para>
        ///
        /// <para>置 false 的效果：不再把任何车间 StatPart 注入原版 StatDef，
        /// 生产速度 / 耐久 / 精度 / 良品 / 研究速度等因子全部停用（脚手架保留，可一键开关）。</para>
        /// </summary>
        private static readonly bool Enabled = true;

        /// <summary>成品类 stat 的 defName（耐久 + 四档精度的原版名）。</summary>
        private static readonly string[] ProductStatNames =
        {
            "MaxHitPoints",
            "AccuracyTouch",
            "AccuracyShort",
            "AccuracyMedium",
            "AccuracyLong",
            "ResearchSpeed"
        };

        static WorkshopStatPartInjector()
        {
            InjectAll();
        }

        private static HashSet<StatDef> CollectTargetStats()
        {
            HashSet<StatDef> targets = new HashSet<StatDef>();

            // ① 工时：全部配方的 workSpeedStat + 全局工作速度
            List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDef recipe = recipes[i];
                if (recipe != null && recipe.workSpeedStat != null)
                {
                    targets.Add(recipe.workSpeedStat);
                }
            }
            StatDef global = DefDatabase<StatDef>.GetNamedSilentFail("WorkSpeedGlobal");
            if (global != null)
            {
                targets.Add(global);
            }

            // ② 成品：耐久 + 精度
            for (int i = 0; i < ProductStatNames.Length; i++)
            {
                StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(ProductStatNames[i]);
                if (stat != null)
                {
                    targets.Add(stat);
                }
            }
            return targets;
        }

        /// <summary>该 stat 应该挂哪一类 StatPart。</summary>
        private static Type ExpectedPartType(StatDef stat)
        {
            if (stat == null)
            {
                return null;
            }
            if (string.Equals(stat.defName, "MaxHitPoints", StringComparison.Ordinal))
            {
                return typeof(StatPart_WorkshopMaxHp);
            }
            if (stat.defName != null && stat.defName.StartsWith("Accuracy", StringComparison.Ordinal))
            {
                return typeof(StatPart_WorkshopAccuracy);
            }
            if (string.Equals(stat.defName, "ResearchSpeed", StringComparison.Ordinal))
            {
                return typeof(StatPart_WorkshopResearchSpeed);
            }
            return typeof(StatPart_WorkshopWorkSpeed);
        }

        private static void InjectAll()
        {
            HashSet<StatDef> targets = CollectTargetStats();
            foreach (StatDef stat in targets)
            {
                InjectInto(stat);
            }
        }

        private static void InjectInto(StatDef stat)
        {
            if (!Enabled)
            {
                return;
            }
            if (stat == null || InjectedStats.Contains(stat))
            {
                return;
            }
            Type partType = ExpectedPartType(stat);
            if (partType == null)
            {
                return;
            }
            if (stat.parts == null)
            {
                stat.parts = new List<StatPart>();
            }
            StatPart part = (StatPart)Activator.CreateInstance(partType);
            part.parentStat = stat;
            // 101 = 排在职业加成(100)之后，两者乘算叠加
            part.priority = 101f;
            stat.parts.Add(part);
            InjectedStats.Add(stat);
        }

        /// <summary>
        /// 幂等重注入：DevMode Def reload 会重建 StatDef.parts 导致 StatPart 丢失。
        /// 由 ChronicleGameComponent 的 reconcile 节流周期调用；已存在则零操作。
        /// </summary>
        public static void EnsureInjected()
        {
            HashSet<StatDef> targets = CollectTargetStats();
            foreach (StatDef stat in targets)
            {
                if (stat == null)
                {
                    continue;
                }
                Type expected = ExpectedPartType(stat);
                if (stat.parts == null)
                {
                    InjectInto(stat);
                    continue;
                }
                bool has = false;
                for (int i = 0; i < stat.parts.Count; i++)
                {
                    if (stat.parts[i] != null && stat.parts[i].GetType() == expected)
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
    }
}
