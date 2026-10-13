using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Per-pawn cumulative work-time ledger (archive career data).
    /// Key = WorkTypeDef.defName; values are game ticks spent in that work type.
    /// Filled by low-frequency GameComponent sampling — never work priorities.
    /// </summary>
    public class WorkTimeAccumulator : IExposable
    {
        public Dictionary<string, long> TicksByWorkType = new Dictionary<string, long>();
        public Dictionary<string, long> LastTickByWorkType = new Dictionary<string, long>();
        public long TotalWorkTicks;
        /// <summary>
        /// **近 8 季工时（h）滚动桶**（段 0 = 最老、段 7 = 当前季）——「产出 · 工时」双轴图
        /// 的工时序列数据源（预览 <c>#v-pawn</c> 总览子页 capChart）。
        /// <para>⚠ 旧档兼容：v5.x 之前无此字段 ⇒ 默认全 0（历史工时不可追溯，曲线自本版起
        /// 积累并如实画空态），无需 Migration。</para>
        /// </summary>
        public List<float> WorkHoursByQuadrum = QuadrumBuckets.NewBuffer();
        /// <summary>段 7 对应的**绝对季度序号**（<c>tick / GenDate.TicksPerQuadrum</c>）；-1 = 尚未写入。</summary>
        public long QuadrumAnchor = -1L;
        /// <summary>
        /// First tick for which this ledger observed work. This is the safe
        /// fallback for legacy/backfilled records whose JoinTick is unknown.
        /// </summary>
        public long FirstObservedTick = -1L;

        public WorkTimeAccumulator()
        {
        }

        /// <summary>
        /// Adds sample ticks for a work type and records the sample wall-clock tick.
        /// </summary>
        public void AddSample(string workTypeDefName, long sampleTicks, long gameTick)
        {
            if (string.IsNullOrEmpty(workTypeDefName) || sampleTicks <= 0L)
            {
                return;
            }
            if (TicksByWorkType == null)
            {
                TicksByWorkType = new Dictionary<string, long>();
            }
            if (LastTickByWorkType == null)
            {
                LastTickByWorkType = new Dictionary<string, long>();
            }
            if (FirstObservedTick < 0L)
            {
                FirstObservedTick = gameTick;
            }
            long prev;
            if (!TicksByWorkType.TryGetValue(workTypeDefName, out prev))
            {
                prev = 0L;
            }
            TicksByWorkType[workTypeDefName] = prev + sampleTicks;
            LastTickByWorkType[workTypeDefName] = gameTick;
            TotalWorkTicks += sampleTicks;
            AccumulateQuadrum(gameTick, sampleTicks);
        }

        /// <summary>
        /// 把一段工时记入「近 8 季」滚动桶（段 7 = 当前季）。
        /// 季度序号 = <c>gameTick / GenDate.TicksPerQuadrum</c>；跨季时整体左移、右侧补 0
        /// （与 <see cref="ProductionAccumulator"/> 的市值桶同一套 <see cref="QuadrumBuckets"/> 逻辑）。
        /// </summary>
        private void AccumulateQuadrum(long gameTick, long sampleTicks)
        {
            float hours = sampleTicks / (float)GenDate.TicksPerHour;
            if (hours <= 0f)
            {
                return;
            }
            QuadrumBuckets.Advance(WorkHoursByQuadrum, ref QuadrumAnchor, gameTick / GenDate.TicksPerQuadrum);
            WorkHoursByQuadrum[QuadrumBuckets.Slots - 1] += hours;
        }

        /// <summary>读取路径对齐入口（快照构建期调用，闲置 n 季后末段不再滞留旧值）。</summary>
        public void AdvanceQuadrum(long quadrumIndex)
        {
            QuadrumBuckets.Advance(WorkHoursByQuadrum, ref QuadrumAnchor, quadrumIndex);
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref TicksByWorkType, "ticksByWorkType", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref LastTickByWorkType, "lastTickByWorkType", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref TotalWorkTicks, "totalWorkTicks", 0L);
            Scribe_Values.Look(ref FirstObservedTick, "firstObservedTick", -1L);
            // v5.x：近 8 季工时滚动桶（旧档无此键 ⇒ 默认空列表 ⇒ 归一为 8 个 0 + Anchor −1）。
            Scribe_Collections.Look(ref WorkHoursByQuadrum, "workHoursByQuadrum", LookMode.Value);
            Scribe_Values.Look(ref QuadrumAnchor, "quadrumAnchor", -1L);
            QuadrumBuckets.EnsureBuffer(WorkHoursByQuadrum);
            if (TicksByWorkType == null)
            {
                TicksByWorkType = new Dictionary<string, long>();
            }
            if (LastTickByWorkType == null)
            {
                LastTickByWorkType = new Dictionary<string, long>();
            }
        }
    }
}
