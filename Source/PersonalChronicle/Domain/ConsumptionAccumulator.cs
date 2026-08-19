using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Persistent consumption summary for a pawn（损耗宫格数据源）。
    /// 与 <see cref="ProductionAccumulator"/> 对称：累计各类目消耗银币 + 按天银币桶。
    ///
    /// 设计决策：进食/用药是高频事件，若逐次写入 ChronicleEvent 会无限膨胀事件流，
    /// 故损耗不写事件，由本累加器直接持久化：周损耗/日均损耗从 <see cref="SilverByDay"/>
    /// 按天桶计算，类目构成从 <see cref="SilverByCategory"/> 计算。
    /// </summary>
    public sealed class ConsumptionAccumulator : IExposable
    {
        /// <summary>
        /// 按类目 defName 聚合的累计银币。类目 = ThingDef.FirstThingCategory.defName，
        /// 无一级类目时回落 "Other"。用于损耗构成前 3 类目进度条。
        /// </summary>
        public Dictionary<string, float> SilverByCategory = new Dictionary<string, float>();

        /// <summary>
        /// 按天（TicksGame / TicksPerDay 取整）聚合的银币，仅保留最近
        /// <see cref="MaxDayRetention"/> 天（Add 时裁剪）。用于周损耗/日均损耗滚动窗口。
        /// </summary>
        public Dictionary<long, float> SilverByDay = new Dictionary<long, float>();

        /// <summary>累计损耗银币总额。</summary>
        public float TotalSilver;

        /// <summary>最近一次消耗的 game tick（-1 = 从未消耗）。</summary>
        public long LastConsumeTick = -1L;

        private const int MaxDayRetention = 60;

        public void Add(string categoryDefName, float silver, long gameTick)
        {
            if (silver <= 0f)
            {
                return;
            }
            if (SilverByCategory == null)
            {
                SilverByCategory = new Dictionary<string, float>();
            }
            if (SilverByDay == null)
            {
                SilverByDay = new Dictionary<long, float>();
            }
            float old;
            SilverByCategory.TryGetValue(categoryDefName, out old);
            SilverByCategory[categoryDefName] = old + silver;
            TotalSilver += silver;
            long day = gameTick / 60000L; // = GenDate.TicksPerDay；Domain 层不引 RimWorld
            float oldDay;
            SilverByDay.TryGetValue(day, out oldDay);
            SilverByDay[day] = oldDay + silver;
            if (gameTick > LastConsumeTick)
            {
                LastConsumeTick = gameTick;
            }
            PruneDays(day);
        }

        /// <summary>
        /// 自 <paramref name="sinceTick"/>（含）以来累计的银币。窗口按天取整（sinceTick 所在天计入）。
        /// </summary>
        public float SilverSince(long sinceTick)
        {
            if (SilverByDay == null || SilverByDay.Count == 0)
            {
                return 0f;
            }
            long sinceDay = sinceTick / 60000L;
            float sum = 0f;
            foreach (KeyValuePair<long, float> kv in SilverByDay)
            {
                if (kv.Key >= sinceDay)
                {
                    sum += kv.Value;
                }
            }
            return sum;
        }

        private void PruneDays(long currentDay)
        {
            if (SilverByDay == null || SilverByDay.Count == 0)
            {
                return;
            }
            List<long> stale = null;
            foreach (KeyValuePair<long, float> kv in SilverByDay)
            {
                if (currentDay - kv.Key > MaxDayRetention)
                {
                    if (stale == null)
                    {
                        stale = new List<long>();
                    }
                    stale.Add(kv.Key);
                }
            }
            if (stale != null)
            {
                for (int i = 0; i < stale.Count; i++)
                {
                    SilverByDay.Remove(stale[i]);
                }
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref SilverByCategory, "silverByCategory", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SilverByDay, "silverByDay", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref TotalSilver, "totalSilver", 0f);
            Scribe_Values.Look(ref LastConsumeTick, "lastConsumeTick", -1L);
            if (SilverByCategory == null)
            {
                SilverByCategory = new Dictionary<string, float>();
            }
            if (SilverByDay == null)
            {
                SilverByDay = new Dictionary<long, float>();
            }
        }
    }
}
