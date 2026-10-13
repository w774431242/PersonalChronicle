using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 岗位标准表索引（<c>CHG-20261003-POSTS</c> · Phase 1）。
    ///
    /// <para><b>职责</b>：把 <c>Defs/WorkshopPosts.xml</c> 的岗位行变成可按多种口径查询的索引 ——
    /// 全部岗位 / 按值勤任务类型 / 按承接的原版 WorkTypeDef 反查 / 按 defName 直取。
    /// <b>不在此层做分配决策</b>（那是 <c>Domain.WorkshopPostAssigner</c> 纯函数的职责）。</para>
    ///
    /// <para><b>建表时机</b>：首次使用时建一次，随后只读；排班/名册变更可 <see cref="Invalidate"/>
    /// 强制重建。范式照抄 <c>WorkshopProcessCatalog</c>（同工程先例）。</para>
    ///
    /// <para><b>COMP-011 可观测性</b>：映射写错（或目标 Def 未加载）若完全静默，
    /// 「岗位承接的工作没勾上」将无从排查；故每个未解析 defName <b>只告警一次</b>。</para>
    /// </summary>
    public static class WorkshopPostCatalog
    {
        private static List<WorkshopPostDef> all;
        private static readonly Dictionary<string, WorkshopPostDef> byDefName =
            new Dictionary<string, WorkshopPostDef>();
        private static readonly Dictionary<string, List<WorkshopPostDef>> byTaskType =
            new Dictionary<string, List<WorkshopPostDef>>();
        private static readonly Dictionary<string, List<WorkshopPostDef>> byWorkType =
            new Dictionary<string, List<WorkshopPostDef>>();

        private static readonly HashSet<string> unresolvedWarned = new HashSet<string>();
        private static readonly List<WorkshopPostDef> EmptyPosts = new List<WorkshopPostDef>();

        private static void EnsureBuilt()
        {
            if (all != null)
            {
                return;
            }
            all = new List<WorkshopPostDef>();
            byDefName.Clear();
            byTaskType.Clear();
            byWorkType.Clear();
            unresolvedWarned.Clear();

            List<WorkshopPostDef> defs = DefDatabase<WorkshopPostDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
            {
                WorkshopPostDef def = defs[i];
                if (def == null)
                {
                    continue;
                }
                all.Add(def);
                byDefName[def.defName] = def;

                if (!string.IsNullOrEmpty(def.dutyTaskTypeKey))
                {
                    List<WorkshopPostDef> bucket;
                    if (!byTaskType.TryGetValue(def.dutyTaskTypeKey, out bucket))
                    {
                        bucket = new List<WorkshopPostDef>();
                        byTaskType[def.dutyTaskTypeKey] = bucket;
                    }
                    bucket.Add(def);
                }

                List<string> covered = def.CoveredWorkTypeNames();
                for (int n = 0; n < covered.Count; n++)
                {
                    string name = covered[n];
                    if (Resolve(name) == null)
                    {
                        continue;   // 缺失 ⇒ 映射不成立（已告警一次），不污染反查索引
                    }
                    List<WorkshopPostDef> bucket;
                    if (!byWorkType.TryGetValue(name, out bucket))
                    {
                        bucket = new List<WorkshopPostDef>();
                        byWorkType[name] = bucket;
                    }
                    bucket.Add(def);
                }
            }
        }

        /// <summary>强制重建索引（排班/名册或 Def 重载后调用）。</summary>
        public static void Invalidate()
        {
            all = null;
        }

        /// <summary>全部岗位（Def 声明序，稳定）。永不为 null。</summary>
        public static List<WorkshopPostDef> AllPosts()
        {
            EnsureBuilt();
            return all;
        }

        /// <summary>按 defName 直取（<c>null</c> = 未定义；<b>不告警</b>，属正常查询未命中）。</summary>
        public static WorkshopPostDef PostOf(string defName)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(defName))
            {
                return null;
            }
            WorkshopPostDef def;
            return byDefName.TryGetValue(defName, out def) ? def : null;
        }

        /// <summary>承接指定原版 WorkTypeDef 的岗位（永不为 null）。</summary>
        public static List<WorkshopPostDef> PostsCovering(string workTypeDefName)
        {
            EnsureBuilt();
            List<WorkshopPostDef> bucket;
            if (!string.IsNullOrEmpty(workTypeDefName)
                && byWorkType.TryGetValue(workTypeDefName, out bucket))
            {
                return bucket;
            }
            return EmptyPosts;
        }

        /// <summary>某个值勤任务类型下的岗位（永不为 null）。</summary>
        public static List<WorkshopPostDef> PostsOfTaskType(string taskTypeKey)
        {
            EnsureBuilt();
            List<WorkshopPostDef> bucket;
            if (!string.IsNullOrEmpty(taskTypeKey) && byTaskType.TryGetValue(taskTypeKey, out bucket))
            {
                return bucket;
            }
            return EmptyPosts;
        }

        /// <summary>
        /// 该 defName 在 1.6 是否对应一个真实存在的原版 <c>WorkTypeDef</c>。
        /// <para>供 Phase 4 闸门与 UI 诊断：映射写错时让玩家看得到"勾上了却没生效"的原因。</para>
        /// </summary>
        public static bool WorkTypeExists(string workTypeDefName)
        {
            return Resolve(workTypeDefName) != null;
        }

        /// <summary>defName → WorkTypeDef；缺失时每个 defName 只告警一次（COMP-011）。</summary>
        private static WorkTypeDef Resolve(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return null;
            }
            WorkTypeDef def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(defName);
            if (def == null && unresolvedWarned.Add(defName))
            {
                Verse.Log.Warning("[PersonalChronicle] Workshop post covered work type not found"
                    + " (skipped): " + defName);
            }
            return def;
        }
    }
}
