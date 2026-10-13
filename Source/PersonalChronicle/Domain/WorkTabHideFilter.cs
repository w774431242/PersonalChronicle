using System.Collections.Generic;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// CHG-20261004-WORKTAB-HIDE · 纯逻辑：判断某个 ThingID 是否应从工作优先级面板隐藏。
    ///
    /// <para>AI-004 / GOV-003 边界：本类<strong>不持有任何状态</strong>、不引用 Pawn / Def / UI，
    /// 因此可离线单测；隐藏名单本体在 <c>Data.ChronicleGameComponent</c>（存档层）。</para>
    ///
    /// <para>为什么不用 LINQ：<c>get_Pawns</c> 在 PawnTable 每次重算时调用，大殖民地动辄上百行；
    /// 名单为空时由调用方早退（零分配），非空时才走手写循环（PERF-001 / PERF-004）。</para>
    /// </summary>
    public static class WorkTabHideFilter
    {
        /// <summary>名单硬上限（PERF-001 有界性：防止无界增长拖慢每次表格重算）。</summary>
        public const int MaxHiddenColonists = 256;

        /// <summary>
        /// 两集合的**并集去重计数**（CHG-20261005-NODEMOTE-AUDIT，可离线单测）。
        ///
        /// <para>隐藏名单分账为「玩家手动 ∪ PC 自动」后，任何"有多少人处于隐藏状态"的判断
        /// 都必须走并集去重 —— 同一人可能同时出现在两个集合里（旧档项归入手动集、之后又因
        /// 加入车间被自动隐藏）⇒ 直接相加会虚高，且会让设置页「恢复显示全部殖民者」的
        /// 门控失效（分账后"仅自动隐藏"是常态，只数手动集恒为 0 ⇒ 按钮不渲染）。</para>
        ///
        /// <para><b>零分配</b>：设置页绘制路径会调用它，故不 new 集合 ——
        /// 用 "manual.Count + 自动集中不在手动集的数量" 直接算。</para>
        /// </summary>
        public static int CountUnion(HashSet<string> manual, HashSet<string> auto)
        {
            int n = manual != null ? manual.Count : 0;
            if (auto == null || auto.Count == 0)
            {
                return n;   // 常态短路
            }
            foreach (string id in auto)
            {
                if (manual == null || !manual.Contains(id))
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// 命中判定。名单为空 ⇒ 绝大多数情况直接返回 false（调用方据此早退，不分配任何集合）。
        /// </summary>
        public static bool ShouldHide(string thingId, HashSet<string> hidden)
        {
            if (hidden == null || hidden.Count == 0) return false;
            if (string.IsNullOrEmpty(thingId)) return false;
            return hidden.Contains(thingId);
        }

        /// <summary>
        /// 存档键洁净化。复用 <see cref="WorkshopWorkDemotionLedger.SanitizeKey"/>——
        /// 同一个 P0 教训：非法XML 字符会让 <c>XmlWellFormedWriter</c> 进入 error 态，
        /// 之后连 <c>ExitNode</c> 都失败 ⇒ <strong>整次存档报废</strong>（2026-10-02 事故）。
        /// 同一套判定只留一份实现，避免两处漂移（GOV-005 契约不重复实现）。
        /// </summary>
        public static string SanitizeKey(string thingId)
        {
            return WorkshopWorkDemotionLedger.SanitizeKey(thingId);
        }
    }
}
