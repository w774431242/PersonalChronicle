using System.Collections.Generic;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 车间岗位自动分配（CHG-20261003-POSTS · Phase 1）——纯逻辑、无 Verse 依赖、可离线单测。
    ///
    /// <para>规则（现实规模化工厂通行做法）：① 技能门槛不足则出局；② 能力门槛不足则出局；
    /// ③ 按<b>已承接的原版工作类型档位之和</b>计分——用工作档位而非技能等级，是为与玩家
    /// 「工作面板里勾了就是能做」的直觉同源；④ 稳定排序：同分取候选人索引更小者
    /// （<c>List.Sort</c> 不稳定，必须显式比较索引，否则岗位随集合增删来回跳人）。</para>
    ///
    /// <para><b>Phase 1 边界</b>：只产出"建议岗位"供 UI 只读展示，
    /// <b>不写任何原版工作状态</b>（不改 Pawn_WorkSettings / timetable）。</para>
    /// </summary>
    public static class WorkshopPostAssigner
    {
        /// <summary>候选人可评估输入（与 Verse 解耦）。</summary>
        public sealed class Candidate
        {
            /// <summary>稳定标识（pawn unique load id）。</summary>
            public string Id = string.Empty;
            /// <summary>技能等级表（缺失视为不可用）。</summary>
            public IDictionary<string, int> Skills = new Dictionary<string, int>();
            /// <summary>能力是否达标。</summary>
            public IDictionary<string, bool> Capacities = new Dictionary<string, bool>();
            /// <summary>各原版工作类型上的档位（未勾选 = 0）。</summary>
            public IDictionary<string, int> WorkPriorities = new Dictionary<string, int>();
            /// <summary>分配结果：岗位 defName（空 = 未分配）。</summary>
            public string CurrentPostDefName = string.Empty;
            /// <summary>
            /// 已获得的岗位数（会话内计数）。
            /// <para><b>为什么需要它</b>：<c>MaxHolders</c> 只限制「每岗几人」，
            /// <b>不限制「每人几岗」</b> ⇒ 3 个人抢 9 个岗时，最会干活的那位会被
            /// <b>所有</b>岗位同时认领（实机：Sky 一人拿下 7 岗）。现实中一个工人只有
            /// <b>一个主岗</b>，兼岗是例外而非常态。</para>
            /// </summary>
            public int AssignedPostCount;
        }

        /// <summary>岗位可评估输入（对应 WorkshopPostDef 的相关字段）。</summary>
        public sealed class PostSpec
        {
            /// <summary>岗位 defName（稳定 ID）。</summary>
            public string DefName = string.Empty;
            /// <summary>承接的原版工作类型 defName 列表。</summary>
            public List<string> CoveredWorkTypeDefNames = new List<string>();
            /// <summary>门槛技能 defName 列表（MinSkillLevel 为 0 时不生效）。</summary>
            public List<string> SkillDefNames = new List<string>();
            /// <summary>门槛等级。</summary>
            public int MinSkillLevel;
            /// <summary>所需能力 defName 列表（空 = 不设门槛）。</summary>
            public List<string> RequiredCapacityDefNames = new List<string>();
            /// <summary>最多由几人承担（1 = 专岗，对应"一个工位一个人"）。</summary>
            public int MaxHolders = 1;
        }

        private struct Scored
        {
            internal readonly int Score;
            internal readonly int Index;

            internal Scored(int score, int index)
            {
                Score = score;
                Index = index;
            }
        }

        /// <summary>
        /// 为一批候选人分配岗位（<b>默认一人一岗</b>）。
        /// <para>保留此重载是为了让"一个工人一个主岗"成为<b>默认</b>语义；
        /// 需要兼岗时显式调用 4 参重载并说明理由。</para>
        /// </summary>
        /// <returns>被分配到至少一个岗位的人数。</returns>
        public static int Assign(IList<Candidate> candidates, IList<PostSpec> posts,
            out Dictionary<string, string> outResult)
        {
            return Assign(candidates, posts, DefaultMaxPostsPerCandidate, out outResult);
        }

        /// <summary>默认每人数岗上限：<b>1</b>（一个工人一个主岗，兼岗是例外而非常态）。</summary>
        public const int DefaultMaxPostsPerCandidate = 1;

        /// <summary>
        /// 为一批候选人分配岗位。
        /// </summary>
        /// <param name="candidates">候选人（就地回填 <see cref="Candidate.CurrentPostDefName"/>）。</param>
        /// <param name="posts">岗位规格。</param>
        /// <param name="maxPostsPerCandidate">
        /// <b>每人最多承担几个岗位</b>（1 = 一岗一人）。
        /// <para>⚠ 2026-10-03 实机修复：此前<b>没有</b>此约束，<c>MaxHolders</c> 只管"每岗几人"，
        /// 于是最会干活的候选人会把所有岗位都认领（实机：3 人 9 岗，Sky 独拿 7 岗）。
        /// 真实工厂里一个工人只有<b>一个主岗</b>，兼岗需显式传 &gt; 1。</para>
        /// </param>
        /// <param name="outResult">defName → 承担者 Id（多人逗号分隔，稳定序）。</param>
        /// <returns>被分配到至少一个岗位的人数。</returns>
        public static int Assign(IList<Candidate> candidates, IList<PostSpec> posts,
            int maxPostsPerCandidate, out Dictionary<string, string> outResult)
        {
            outResult = new Dictionary<string, string>();
            if (candidates == null || posts == null || candidates.Count == 0 || posts.Count == 0)
            {
                return 0;
            }
            int assigned = 0;
            for (int m = 0; m < posts.Count; m++)
            {
                PostSpec post = posts[m];
                if (post == null || string.IsNullOrEmpty(post.DefName) || post.MaxHolders <= 0)
                {
                    continue;
                }
                var scored = new List<Scored>();
                for (int i = 0; i < candidates.Count; i++)
                {
                    Candidate c = candidates[i];
                    if (c == null)
                    {
                        continue;
                    }
                    // 每人岗位数已达上限 ⇒ 不再参与本岗位竞争（一人一主岗的默认语义）
                    if (maxPostsPerCandidate > 0 && c.AssignedPostCount >= maxPostsPerCandidate)
                    {
                        continue;
                    }
                    int score;
                    if (Eligible(c, post, out score))
                    {
                        scored.Add(new Scored(score, i));
                    }
                }
                if (scored.Count == 0)
                {
                    continue;
                }
                scored.Sort((a, b) => a.Score != b.Score
                    ? b.Score.CompareTo(a.Score)
                    : a.Index.CompareTo(b.Index));
                int limit = post.MaxHolders < scored.Count ? post.MaxHolders : scored.Count;
                var holders = new List<string>(limit);
                for (int k = 0; k < limit; k++)
                {
                    Candidate c = candidates[scored[k].Index];
                    if (string.IsNullOrEmpty(c.CurrentPostDefName))
                    {
                        assigned++;
                    }
                    c.CurrentPostDefName = post.DefName;
                    c.AssignedPostCount++;
                    holders.Add(c.Id);
                }
                outResult[post.DefName] = string.Join(",", holders.ToArray());
            }
            return assigned;
        }
        /// <summary>原版工作档位的最低值（<c>Pawn_WorkSettings.LowestPriority</c> = 4，1.6 实证）。</summary>
        private const int PawnLowestPriority = 4;

        /// <summary>资格（技能 + 能力门槛）与评分（承接工作类型档位之和）。</summary>
        private static bool Eligible(Candidate c, PostSpec post, out int score)
        {
            score = 0;
            if (post.MinSkillLevel > 0 && post.SkillDefNames.Count > 0)
            {
                bool anyPresent = false;
                bool reached = false;
                for (int i = 0; i < post.SkillDefNames.Count; i++)
                {
                    int level;
                    if (!c.Skills.TryGetValue(post.SkillDefNames[i], out level))
                    {
                        continue;   // 该技能未持有 ⇒ 换下一个门槛技能
                    }
                    anyPresent = true;
                    if (level >= post.MinSkillLevel)
                    {
                        reached = true;
                        break;
                    }
                }
                if (!anyPresent || !reached)
                {
                    return false;   // 门槛技能全缺 或 都不足 ⇒ 出局
                }
            }
            for (int i = 0; i < post.RequiredCapacityDefNames.Count; i++)
            {
                bool ok;
                if (!c.Capacities.TryGetValue(post.RequiredCapacityDefNames[i], out ok) || !ok)
                {
                    return false;
                }
            }
            for (int i = 0; i < post.CoveredWorkTypeDefNames.Count; i++)
            {
                int priority;
                if (c.WorkPriorities.TryGetValue(post.CoveredWorkTypeDefNames[i], out priority)
                    && priority > 0)
                {
                    // ⚠ 方向（2026-10-03 修正）：原版档位 **1 = 最高、4 = 最低**（Pawn_WorkSettings），
                    //   所以"档位数字相加"会把**最该做的人排到最后**。必须按倒序权重计分：
                    //   权重 = LowestPriority(4) + 1 - 档位 ⇒ 1 档得 4 分、4 档得 1 分、未勾选(0) 得 0。
                    score += (PawnLowestPriority + 1) - priority;
                }
            }
            return true;
        }
    }
}