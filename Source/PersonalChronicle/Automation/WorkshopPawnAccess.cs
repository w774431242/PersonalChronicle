using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// **稳定 ID → Pawn 通道**（S3-4 头像 / S3-5 派工的共同前置 · 2026-09-23）。
    ///
    /// <para><b>为什么必须有这条通道</b>：Read Model 快照**不得携带 <see cref="Pawn"/> 引用**
    /// （跨层 / 跨档不安全 —— 全工程既有纪律；Thing 侧走
    /// <c>WorkshopBillActions.FindByStableId</c>）。故快照只带 <c>thingIDNumber</c>，
    /// 需要在窗口层拿到 Pawn 时（画头像 / 写派工）由本类找回。</para>
    ///
    /// <para><b>性能</b>：线性查找，n = 在场自由殖民者数（通常 &lt; 30）；只在**绘制排名行**或
    /// **点击派工**时调用（非 tick 热路径）⇒ 符合 PERF-001。找不到（死亡 / 离图 / 快照与实体不符）
    /// ⇒ 返回 null，调用方按"无头像 / 不可派工"处理，不抛异常。</para>
    /// </summary>
    internal static class WorkshopPawnAccess
    {
        /// <summary>按 <c>thingIDNumber</c> 在地图在场自由殖民者中查找（找不到 ⇒ null）。</summary>
        internal static Pawn FindByThingId(Map map, int thingId)
        {
            if (map == null || thingId <= 0)
            {
                return null;
            }
            List<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn != null && pawn.thingIDNumber == thingId)
                {
                    return pawn;
                }
            }
            return null;
        }

        // ---- 最强技能摘要（班组卡片 / 班组设置共用，2026-10-01）----------------

        // 每帧缓存：技能等级帧内不变 ⇒ 同一 pawn 的字符串一帧只拼一次
        //（班组卡片成员行 + 班组设置行都会调，避免每帧重复拼串分配）。
        private static readonly Dictionary<int, string> topSkillsCache = new Dictionary<int, string>();
        private static int topSkillsFrame = -1;

        /// <summary>
        /// pawn 的前 <paramref name="count"/> 个最强技能，形如「射击 12++ · 采矿 9+」。
        /// 排序：等级降序，同级按热情 高 &gt; 低；<c>TotallyDisabled</c> 的技能跳过；
        /// 热情标记：<c>++</c> = 强热情（橙焰）、<c>+</c> = 弱热情（绿焰）。
        /// 无可用技能（如全禁用）⇒ 空串（调用方不占位）。
        /// </summary>
        internal static string TopSkillsOf(Pawn pawn, int count = 2)
        {
            if (pawn?.skills?.skills == null || count <= 0)
            {
                return string.Empty;
            }
            if (topSkillsFrame != UnityEngine.Time.frameCount)
            {
                topSkillsFrame = UnityEngine.Time.frameCount;
                topSkillsCache.Clear();
            }
            if (topSkillsCache.TryGetValue(pawn.thingIDNumber, out string cached))
            {
                return cached;
            }

            // 两遍选择取 top-N：零排序分配（热路径友好；技能表 ≤ 13 项，两遍足够）。
            SkillRecord first = null;
            SkillRecord second = null;
            List<SkillRecord> all = pawn.skills.skills;
            for (int i = 0; i < all.Count; i++)
            {
                SkillRecord record = all[i];
                if (record == null || record.TotallyDisabled)
                {
                    continue;
                }
                bool beatsFirst = first == null
                    || record.Level > first.Level
                    || (record.Level == first.Level && record.passion > first.passion);
                if (beatsFirst)
                {
                    second = first;
                    first = record;
                }
                else if (second == null
                    || record.Level > second.Level
                    || (record.Level == second.Level && record.passion > second.passion))
                {
                    second = record;
                }
            }

            string text = JoinSkill(first);
            if (count >= 2 && second != null)
            {
                text += (text.Length > 0 ? " · " : string.Empty) + JoinSkill(second);
            }
            topSkillsCache[pawn.thingIDNumber] = text;
            return text;
        }

        private static string JoinSkill(SkillRecord record)
        {
            if (record == null || record.def == null)
            {
                return string.Empty;
            }
            string passion = record.passion == Passion.Major ? "++"
                : record.passion == Passion.Minor ? "+" : string.Empty;
            return record.def.skillLabel + " " + record.Level + passion;
        }
    }
}
