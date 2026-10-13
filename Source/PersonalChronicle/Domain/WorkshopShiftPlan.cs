using System.Collections.Generic;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 班组「上岗名额」分配（**纯函数，可离线单测**）。
    ///
    /// <para>语义：车间班组（<c>WorkshopShift</c>）声明「时段 + 需要几个人」；本函数决定
    /// 这批名额归谁 —— 先按 <paramref name="eligible"/>（技能门槛、在岗可用性等）过滤，
    /// 再按 <paramref name="scores"/> 降序取前 <paramref name="requiredCount"/> 名。</para>
    ///
    /// <para>稳定性：同分时取<b>索引更小</b>者（= 候选池内的先后顺序），保证同一存档、
    /// 同一时刻反复求值得到同一批人（否则会表现为"岗位来回换人"）。</para>
    /// </summary>
    public static class WorkshopShiftPlanner
    {
        /// <summary>
        /// <paramref name="requiredCount"/> ≤ 0 时启用的**默认名额**（GOV-006 登记：
        /// 分类 = 班次缺省人数；来源 = 用户三班倒意图，见下；生命周期 = 常量）。
        ///
        /// <para><b>为什么不再是"全部合格者上岗"（2026-10-03 · 用户实机 P0）</b>：
        /// 旧语义下 <c>requiredCount &lt;= 0</c> ⇒ <c>limit = candidates.Count</c> ⇒
        /// <b>每个班次都把整份候选池吃光</b>。多班次车间因此出现"名义三班倒、实际同一批人
        /// 全时段在岗"：夜班成员在日班时段（08–17）也会被选上岗，于是他"到点不下班、
        /// 也不回床睡觉"，而人员页只显示他的「今日岗位 = 夜班」，UI 与实际完全对不上
        /// （`RebuildDutyPlan` 是逐班次独立选人的，先占者胜）。</para>
        ///
        /// <para>现在缺省按 <b>1 人</b> 办（一个班次一个人），这与玩家"Sky/Gill 日班、
        /// Max 夜班"的显式三班倒意图一致。真正想要"多不限"的玩家可把班次人数显式填大，
        /// 或打开 <see cref="UnlimitedHeadcountFlag"/>。</para>
        /// </summary>
        public const int DefaultHeadcount = 1;

        /// <summary>
        /// 「不限名额」显式标记的哨兵值（存进 <c>WorkshopShift.RequiredCount</c>）。
        /// <para>用负数表达而不是 0：0 已经表示"未配置 ⇒ 走 <see cref="DefaultHeadcount"/>"，
        /// 若仍让 0 表示不限，就等于把这个缺陷原样留回去。</para>
        /// </summary>
        public const int UnlimitedHeadcountFlag = -1;

        /// <summary>班次是否处于「不限名额」模式（<c>RequiredCount &lt; 0</c>）。</summary>
        public static bool IsUnlimited(int requiredCount)
        {
            return requiredCount < 0;
        }

        /// <summary>
        /// 选出上岗者的候选池索引。
        /// </summary>
        /// <param name="eligible">逐索引的合格标记（长度 = 候选池长度）。</param>
        /// <param name="scores">逐索引的评分（越大越优先；可为 null = 全部同分）。</param>
        /// <param name="requiredCount">名额上限。
        /// <b>0 = 未配置</b> ⇒ 走 <see cref="DefaultHeadcount"/>（1 人）；
        /// <b>&lt; 0</b> ⇒ 显式「不限名额」，全部合格者上岗；
        /// <b>&gt; 0</b> ⇒ 取前 N 名。</param>
        /// <returns>被选中者的索引列表（按上岗优先级排序）；无合格者时为空表。</returns>
        public static List<int> SelectDutyIndexes(IList<bool> eligible, IList<int> scores, int requiredCount)
        {
            var picked = new List<int>();
            if (eligible == null || eligible.Count == 0)
            {
                return picked;
            }

            var candidates = new List<int>();
            for (int i = 0; i < eligible.Count; i++)
            {
                if (eligible[i])
                {
                    candidates.Add(i);
                }
            }
            if (candidates.Count == 0)
            {
                return picked;
            }

            candidates.Sort((a, b) =>
            {
                int sa = scores != null && a < scores.Count ? scores[a] : 0;
                int sb = scores != null && b < scores.Count ? scores[b] : 0;
                // 高分在前；同分保持池内顺序（List.Sort 不稳定，故显式比较索引）。
                return sa != sb ? sb.CompareTo(sa) : a.CompareTo(b);
            });

            // ⚠ 2026-10-03：0 不再等价"全选"。见 DefaultHeadcount 的说明。
            int limit = IsUnlimited(requiredCount)
                ? candidates.Count
                : (requiredCount <= 0 ? DefaultHeadcount : requiredCount);
            if (limit > candidates.Count)
            {
                limit = candidates.Count;
            }
            for (int i = 0; i < limit; i++)
            {
                picked.Add(candidates[i]);
            }
            return picked;
        }
    }
}
