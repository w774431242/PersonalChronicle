using System.Collections.Generic;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// CHG-20261005-REDLINE · 「人 → 岗位」自动分配的**单一实现**（Automation 层）。
    ///
    /// <para><b>为什么要下沉到这一层</b>：岗位分配结果现在有两个消费者 ——
    /// ① 岗位页与「按岗位落地」（原先在 UI 层自己算）；
    /// ② <b>接管闸门</b>（要判断"某工作类型是否有**在岗的专属岗位持有者**"，
    /// 见 <see cref="WorkshopTakeover.IsTakeoverAllowed(string, bool, bool)"/>）。
    /// 若两处各算一遍，同一批成员可能得出**不同的岗位** ⇒ 出现"页面显示他是物料员工、
    /// 接管却按别的岗位判定"这种自相矛盾的裁决（GOV-005 / UI-009）。</para>
    ///
    /// <para><b>纯读</b>：只读技能 / 能力 / 岗位 Def / 车间成员，零写入。写入由调用方决定
    /// （落地写档位、接管写 0）。</para>
    ///
    /// <para><b>玩家手动覆盖</b>（<c>zone.GetPostOverride</c>）不在这里叠加 —— 那是"覆盖"而非
    /// "分配"，由消费方按需调用 <see cref="ResolvePostDefNameOf"/> 合并。</para>
    /// </summary>
    public static class WorkshopPostAssignmentService
    {
        // 岗位表引用的技能 / 能力 Def（首次调用时解析一次；DefDatabase 加载后不变 ⇒ 可静态缓存）。
        private static readonly List<SkillDef> skillDefs = new List<SkillDef>();
        private static readonly List<PawnCapacityDef> capacityDefs = new List<PawnCapacityDef>();
        private static bool defsResolved;

        /// <summary>
        /// 算出「成员 → 岗位 defName」映射（未分配到岗位的成员不出现在字典里）。
        /// </summary>
        /// <param name="members">候选人（通常 = 本车间在编成员）。</param>
        public static Dictionary<Pawn, string> AssignOf(List<Pawn> members)
        {
            var result = new Dictionary<Pawn, string>();
            if (members == null || members.Count == 0)
            {
                return result;
            }
            EnsureDefsResolved();

            List<WorkshopPostAssigner.Candidate> candidates = BuildCandidates(members);
            List<WorkshopPostAssigner.PostSpec> specs = BuildSpecs();
            Dictionary<string, string> assign;
            WorkshopPostAssigner.Assign(candidates, specs, out assign);

            for (int i = 0; i < candidates.Count; i++)
            {
                WorkshopPostAssigner.Candidate c = candidates[i];
                if (c == null || string.IsNullOrEmpty(c.CurrentPostDefName))
                {
                    continue;
                }
                Pawn pawn = FindByStableId(members, c.Id);
                if (pawn != null)
                {
                    result[pawn] = c.CurrentPostDefName;
                }
            }
            return result;
        }

        /// <summary>岗位页与运行时共享的在编班组成员顺序，确保自动岗位不因候选池不同而分叉。</summary>
        public static void CollectRosterMembers(Zone_Workshop zone, List<Pawn> result, HashSet<Pawn> seen)
        {
            result.Clear();
            seen.Clear();
            if (zone?.Crews == null) return;
            foreach (var crew in zone.Crews)
                if (crew?.Members != null)
                    foreach (var pawn in crew.Members)
                        if (pawn != null && !pawn.Destroyed && seen.Add(pawn)) result.Add(pawn);
        }

        /// <summary>
        /// 该工作类型在本车间是否有**在岗的专属岗位持有者** ——
        /// 即：存在岗位承接它 <b>且</b> 该岗位在本车间确实有人担任。
        ///
        /// <para>这是 <see cref="WorkshopTakeover.MandatoryKeep"/> 敏感项
        /// （搬运 / 清洁 / 医疗 / 灭火）的**唯一放行条件**（CHG-20261005-REDLINE）。</para>
        /// </summary>
        public static bool HasStaffedPostCovering(string workTypeDefName, List<Pawn> members,
            Zone_Workshop zone, Dictionary<Pawn, string> assigned)
        {
            if (string.IsNullOrEmpty(workTypeDefName) || members == null || members.Count == 0)
            {
                return false;
            }
            List<WorkshopPostDef> posts = WorkshopPostCatalog.PostsCovering(workTypeDefName);
            if (posts == null || posts.Count == 0)
            {
                return false;   // 没有任何岗位承接它 ⇒ 无人替代 ⇒ 不放行
            }
            for (int i = 0; i < members.Count; i++)
            {
                if (IsHolderOfWorkType(members[i], workTypeDefName, zone, assigned))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 该成员是否担任"承接了该工作类型"的岗位。
        /// <para>接管写入时用它**豁免岗位持有者本人**（否则连负责搬运的人也被清零 ⇒ 断料）。</para>
        /// </summary>
        public static bool IsHolderOfWorkType(Pawn pawn, string workTypeDefName,
            Zone_Workshop zone, Dictionary<Pawn, string> assigned)
        {
            if (pawn == null || string.IsNullOrEmpty(workTypeDefName))
            {
                return false;
            }
            WorkshopPostDef post = WorkshopPostCatalog.PostOf(
                ResolvePostDefNameOf(pawn, zone, assigned));
            if (post == null)
            {
                return false;
            }
            List<string> covered = post.CoveredWorkTypeNames();
            for (int i = 0; i < covered.Count; i++)
            {
                if (covered[i] == workTypeDefName)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 取该成员的岗位 defName：<b>玩家显式覆盖 &gt; 自动分配结果 &gt; null</b>。
        /// <para>与岗位页显示同一口径（覆盖项失效时静默回落自动，不让死 defName 卡住成员）。</para>
        /// </summary>
        public static string ResolvePostDefNameOf(Pawn pawn, Zone_Workshop zone,
            Dictionary<Pawn, string> assigned)
        {
            if (pawn == null)
            {
                return null;
            }
            if (zone != null)
            {
                string manual = zone.GetPostOverride(pawn);
                if (!manual.NullOrEmpty() && WorkshopPostCatalog.PostOf(manual) != null)
                {
                    return manual;
                }
            }
            string auto;
            if (assigned != null && assigned.TryGetValue(pawn, out auto) && !string.IsNullOrEmpty(auto))
            {
                return auto;
            }
            return null;
        }

        // ---- 内部：Def 解析与候选构造（原先散在 UI 层，现收口于此）----------------

        private static void EnsureDefsResolved()
        {
            if (defsResolved)
            {
                return;
            }
            defsResolved = true;
            List<WorkshopPostDef> posts = WorkshopPostCatalog.AllPosts();
            for (int i = 0; i < posts.Count; i++)
            {
                WorkshopPostDef def = posts[i];
                if (def == null)
                {
                    continue;
                }
                List<string> skills = def.SkillNames();
                for (int n = 0; n < skills.Count; n++)
                {
                    SkillDef sd = DefDatabase<SkillDef>.GetNamedSilentFail(skills[n]);
                    if (sd != null && !skillDefs.Contains(sd))
                    {
                        skillDefs.Add(sd);
                    }
                }
                List<string> caps = def.RequiredCapacityNames();
                for (int n = 0; n < caps.Count; n++)
                {
                    PawnCapacityDef cd = DefDatabase<PawnCapacityDef>.GetNamedSilentFail(caps[n]);
                    if (cd != null && !capacityDefs.Contains(cd))
                    {
                        capacityDefs.Add(cd);
                    }
                }
            }
        }

        private static List<WorkshopPostAssigner.Candidate> BuildCandidates(List<Pawn> members)
        {
            var list = new List<WorkshopPostAssigner.Candidate>(members.Count);
            for (int i = 0; i < members.Count; i++)
            {
                Pawn pawn = members[i];
                if (pawn == null)
                {
                    continue;
                }
                var c = new WorkshopPostAssigner.Candidate
                {
                    Id = pawn.GetUniqueLoadID(),
                };
                if (pawn.skills != null)
                {
                    for (int s = 0; s < skillDefs.Count; s++)
                    {
                        SkillDef sd = skillDefs[s];
                        SkillRecord rec = pawn.skills.GetSkill(sd);
                        c.Skills[sd.defName] = rec != null ? rec.Level : 0;
                    }
                }
                Pawn_HealthTracker health = pawn.health;
                if (health != null && health.capacities != null)
                {
                    for (int s = 0; s < capacityDefs.Count; s++)
                    {
                        PawnCapacityDef cd = capacityDefs[s];
                        // 1.6 程序集已核实：只有 GetLevel，没有 GetCapacity / PawnCapacityTracker 列表。
                        c.Capacities[cd.defName] = health.capacities.GetLevel(cd) > 0f;
                    }
                }
                Pawn_WorkSettings settings = pawn.workSettings;
                if (settings != null)
                {
                    List<WorkTypeDef> types = DefDatabase<WorkTypeDef>.AllDefsListForReading;
                    for (int s = 0; s < types.Count; s++)
                    {
                        WorkTypeDef type = types[s];
                        c.WorkPriorities[type.defName] = settings.GetPriority(type);
                    }
                }
                list.Add(c);
            }
            return list;
        }

        private static List<WorkshopPostAssigner.PostSpec> BuildSpecs()
        {
            var list = new List<WorkshopPostAssigner.PostSpec>();
            List<WorkshopPostDef> posts = WorkshopPostCatalog.AllPosts();
            for (int i = 0; i < posts.Count; i++)
            {
                WorkshopPostDef def = posts[i];
                if (def == null)
                {
                    continue;
                }
                list.Add(new WorkshopPostAssigner.PostSpec
                {
                    DefName = def.defName,
                    CoveredWorkTypeDefNames = def.CoveredWorkTypeNames(),
                    SkillDefNames = def.SkillNames(),
                    MinSkillLevel = def.minSkillLevel,
                    RequiredCapacityDefNames = def.RequiredCapacityNames(),
                    MaxHolders = 1,
                });
            }
            return list;
        }

        /// <summary>按 unique load id 回查 pawn（<b>不告警</b>：pawn 消亡是正常情形）。</summary>
        private static Pawn FindByStableId(List<Pawn> members, string uniqueLoadId)
        {
            if (string.IsNullOrEmpty(uniqueLoadId) || members == null)
            {
                return null;
            }
            for (int i = 0; i < members.Count; i++)
            {
                Pawn p = members[i];
                if (p != null && p.GetUniqueLoadID() == uniqueLoadId)
                {
                    return p;
                }
            }
            return null;
        }
    }
}
