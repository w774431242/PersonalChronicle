using System.Collections.Generic;
using System.Collections.ObjectModel;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Profession;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// [ARC-013] <see cref="ChronicleGameComponent"/> 的「纯辅助计算」切片
    /// （ResolveDefaultJoinTick / AddResearchSpeedBonus / GetResearchSpeedFactor）。
    ///
    /// 原 ChronicleGameComponent.cs 单文件 857 行，超出 [ARC-013] 「单文件 ≤ 800 行」上限。
    /// Save 契约铁律：ExposeData / FinalizeInit / MigrateLegacyData 全家与全部字段声明
    /// 保留主文件；本文件仅物理搬移无 Scribe 副作用的纯方法，零 Save schema 改动。
    /// 同 partial 类成员天然互见，调用点无需任何改动。
    /// </summary>
    public sealed partial class ChronicleGameComponent : GameComponent
    {
        /// <summary>
        /// 默认加入时刻的单一权威入口（v5.x 简化）。
        ///
        /// 根因：AddObject 对已存在 StableId 直接拒绝（不更新 JoinTick），谁先建档
        /// 谁决定 JoinTick，而建档入口有多个（Backfill / reconcile / 社交补录 /
        /// 捕获补录 / 死亡兜底）。统一在此裁决，避免各入口各自为政导致"新档开局
        /// 殖民者被 -1 定格"。
        ///
        /// 判定只用"加入/发现当天"粒度，不做任何时间窗口推断：
        ///   - 读档会话（cameFromLoad）→ 存档缺失、后来发现的存活人口，归到
        ///     发现/加入当天的起点（不再用 -1 显示"中途加入"）。
        ///   - 新档会话 → 开局殖民者，加入日即第 1 天（tick 0）。
        /// 显式加入（OnColonistJoined）仍走真实 TicksGame，不经过这里。
        /// </summary>
        internal long ResolveDefaultJoinTick()
        {
            if (cameFromLoad)
            {
                int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
                if (now > 0)
                {
                    // 当天起点用游戏日数系统：DayTick = 当天内已过 tick，减去即当天
                    // 零点。避免手工 TicksPerDay 取模。
                    long dayStart = now - (long)RimWorld.GenDate.DayTick(now, 0f);
                    return dayStart;
                }
                return 0L;
            }
            return 0L;
        }

        /// <summary>
        /// 累加殖民地全局某方向的科研速度加成（百分点，叠加）。论文引用研究文献时调用。
        /// 无 component 时静默跳过（设计预期：application 层已校验）。
        /// </summary>
        public static void AddResearchSpeedBonus(ChronicleGameComponent component, string majorKey, float percent)
        {
            if (component == null || string.IsNullOrEmpty(majorKey) || percent <= 0f) return;
            if (component.ResearchSpeedBonusByMajor == null)
            {
                component.ResearchSpeedBonusByMajor = new Dictionary<string, float>();
            }
            float cur = 0f;
            component.ResearchSpeedBonusByMajor.TryGetValue(majorKey, out cur);
            component.ResearchSpeedBonusByMajor[majorKey] = cur + percent;
        }

        /// <summary>
        /// 读取殖民地全局某方向的科研速度加成倍率（1.0 + Σ百分点/100）。无加成返回 1.0。
        /// 供 UI/科研增益展示读取（仅 mod 内消费；不改原生 ResearchManager）。
        /// </summary>
        public static float GetResearchSpeedFactor(ChronicleGameComponent component, string majorKey)
        {
            if (component == null || string.IsNullOrEmpty(majorKey)) return 1f;
            float bonus = 0f;
            if (component.ResearchSpeedBonusByMajor != null
                && component.ResearchSpeedBonusByMajor.TryGetValue(majorKey, out float v))
            {
                bonus = v;
            }
            return 1f + bonus / 100f;
        }
    }
}
