using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 库存蜡烛序列（`SDD-LCE-003` §3）——**正确性对账**（预览 v5「数据演进回放」的同构承接）。
    ///
    /// <para>两条独立实现路径互证：</para>
    /// <list type="bullet">
    /// <item>在线增量：<see cref="StockSeries.AppendSample"/>（开=首样本 · 高/低=极值 · 收=最新）</item>
    /// <item>批式参考：测试内 <see cref="Reference"/>（逐桶 首 / max / min / 末 一次算完）</item>
    /// </list>
    /// 二者逐桶全等 ⇒ 定义正确；再叠加边界样例矩阵（平线 doji / 长上影 / 单向下跌 / 清零）。
    /// </summary>
    [TestFixture]
    public sealed class StockSeriesTests
    {
        // ---------------- ① 统一周期基准断言（SDD §2） ----------------

        [Test]
        public void TierBasis_IsSingleSourceAndDivisible()
        {
            Assert.AreEqual(EconomyPeriodTier.TicksPerDay, 24L * EconomyPeriodTier.TicksPerHour60, "24 × 60分 = 日（跨度常量仍成立；60分 档已退役）");
            Assert.AreEqual(EconomyPeriodTier.TicksPerWeek, 7L * EconomyPeriodTier.TicksPerDay, "7 日 = 周");
            Assert.AreEqual(EconomyPeriodTier.TicksPerMonth, 15L * EconomyPeriodTier.TicksPerDay, "15 日 = 月（quadrum）");
            Assert.AreEqual(EconomyPeriodTier.TicksPerQuarter, 2L * EconomyPeriodTier.TicksPerMonth, "季 = 2 月");
            Assert.AreEqual(EconomyPeriodTier.TicksPerYear, 4L * EconomyPeriodTier.TicksPerMonth, "年 = 4 月");
            Assert.AreEqual(EconomyPeriodTier.TicksPerYear, 2L * EconomyPeriodTier.TicksPerQuarter, "年 = 2 季");
            Assert.AreEqual(EconomyPeriodTier.DefaultTier, EconomyPeriodTier.Resolve(-1), "越界槽位归一到默认档");

            // ---- 2026-09-21 用户裁决：退役 60分 档 + 全档无上限 ----
            Assert.AreEqual(6, EconomyPeriodTier.Count, "槽位数含退役 60分 槽位（= 存档形状，勿改）");
            Assert.AreEqual(5, EconomyPeriodTier.DisplayCount, "显示五档（日/周/月/季/年）");
            Assert.AreEqual(EconomyPeriodTier.Day, EconomyPeriodTier.BaseTier, "基准档 = 日（采样与上卷入口）");
            Assert.AreEqual(0, EconomyPeriodTier.RetentionOf(EconomyPeriodTier.Hour60), "退役槽位保留上限 = 0 ⇒ 读档清空");
            for (int t = EconomyPeriodTier.Day; t < EconomyPeriodTier.Count; t++)
            {
                Assert.AreEqual(EconomyPeriodTier.Unbounded, EconomyPeriodTier.RetentionOf(t), "显示档 " + t + " = 无上限（全历史保留）");
            }
            for (int d = 0; d < EconomyPeriodTier.DisplayCount; d++)
            {
                Assert.AreEqual(d, EconomyPeriodTier.DisplayIndexOf(EconomyPeriodTier.TierAt(d)), "显示索引 ↔ 槽位互逆：" + d);
            }
            Assert.AreEqual(EconomyPeriodTier.DefaultTier, EconomyPeriodTier.TierAt(-1), "未选（−1）⇒ 默认档");
            Assert.AreEqual(EconomyPeriodTier.DefaultTier, EconomyPeriodTier.TierAt(99), "越界显示索引 ⇒ 默认档");
            Assert.AreEqual(0, EconomyPeriodTier.DisplayIndexOf(EconomyPeriodTier.Hour60), "退役槽位无显示位 ⇒ 归 0");
        }

        [Test]
        public void Boundary_FloorsToBucketStart()
        {
            Assert.AreEqual(0L, EconomyPeriodTier.BoundaryOf(EconomyPeriodTier.Day, 59999L));
            Assert.AreEqual(60000L, EconomyPeriodTier.BoundaryOf(EconomyPeriodTier.Day, 60000L));
            Assert.AreEqual(60000L, EconomyPeriodTier.BoundaryOf(EconomyPeriodTier.Day, 60000L + 1234L));
            Assert.AreEqual(900000L, EconomyPeriodTier.BoundaryOf(EconomyPeriodTier.Month, 900000L + 1L));
        }

        // ---------------- ② 在线增量 == 批式参考 ----------------

        /// <summary>
        /// 高档对账（2026-09-21 裁决后）：基准档（日）封桶直喂 **周/月/季/年** ⇒
        /// ①周档可与批式参考逐桶全等；②月/季/年此时未封桶 ⇒ 进行中蜡烛 = 已上卷日蜡烛的包络
        /// （结合律的直接证据：与"逐级喂入"逐项相同）。
        /// </summary>
        [Test]
        public void OnlineAggregation_MatchesBatchReference_AllUpperTiers()
        {
            // 9 个游戏日（240 点/日 = 1 日节奏）：日封 8 桶 ⇒ 上卷 8 根日蜡烛进高档
            const int Days = 9;
            const int PerDay = 240;
            List<float> samples = Synthesize(Days * PerDay, 250L);
            StockSeries series = Feed(0L, 250L, samples);
            int fedCount = (Days - 1) * PerDay;   // 已上卷的样本数

            // ① 周：真批式参考逐桶全等（含已封桶）
            AssertClosedEqual(series, EconomyPeriodTier.Week,
                Reference(samples, 250L, EconomyPeriodTier.TicksOf(EconomyPeriodTier.Week)));

            // ② 月/季/年：进行中蜡烛 = 已上卷样本的包络（开首 / 高低极值 / 收末 / 样本数求和）
            float hi = samples[0], lo = samples[0];
            for (int i = 1; i < fedCount; i++)
            {
                if (samples[i] > hi) { hi = samples[i]; }
                if (samples[i] < lo) { lo = samples[i]; }
            }
            int[] formingOnly = { EconomyPeriodTier.Month, EconomyPeriodTier.Quarter, EconomyPeriodTier.Year };
            foreach (int tier in formingOnly)
            {
                int n = series.CountOf(tier);
                StockCandle forming = series.CandleAt(tier, n - 1);
                Assert.IsNotNull(forming, "槽位 " + tier + " 进行中蜡烛");
                Assert.AreEqual(samples[0], forming.Open, 1e-6, "槽位 " + tier + " 开 = 首个上卷样本");
                Assert.AreEqual(hi, forming.High, 1e-6, "槽位 " + tier + " 高 = 已上卷极值");
                Assert.AreEqual(lo, forming.Low, 1e-6, "槽位 " + tier + " 低 = 已上卷极值");
                Assert.AreEqual(samples[fedCount - 1], forming.Close, 1e-6, "槽位 " + tier + " 收 = 末个上卷样本");
                Assert.AreEqual(fedCount, forming.Samples, "槽位 " + tier + " 样本数 = 已上卷总量");
            }
        }

        [Test]
        public void OnlineAggregation_MatchesBatchReference_Daily()
        {
            // 3 个游戏日：240 点/日
            List<float> samples = Synthesize(3 * 240, 250L);
            StockSeries series = Feed(0L, 250L, samples);

            List<StockCandle> expected = Reference(samples, 250L, EconomyPeriodTier.TicksOf(EconomyPeriodTier.Day));
            AssertClosedEqual(series, EconomyPeriodTier.Day, expected);
        }

        // ---------------- ③ 边界样例矩阵 ----------------

        [Test]
        public void EdgeCase_FlatBucket_IsDoji()
        {
            StockSeries series = new StockSeries();
            long start = EconomyPeriodTier.BoundaryOf(EconomyPeriodTier.Day, 100000L);
            for (int i = 0; i < 10; i++)
            {
                series.AppendSample(start + i * 250L, 42f);
            }
            StockCandle c = series.CandleAt(EconomyPeriodTier.Day, 0);
            Assert.IsNotNull(c);
            Assert.AreEqual(42f, c.Open, "开 = 首样本");
            Assert.AreEqual(42f, c.High, "高 = 极大");
            Assert.AreEqual(42f, c.Low, "低 = 极小");
            Assert.AreEqual(42f, c.Close, "收 = 末样本");
            Assert.AreEqual(10, c.Samples, "样本数");
            Assert.IsTrue(c.IsFlat, "平线 = doji（绘制期按最小实体 2px 处理）");
        }

        [Test]
        public void EdgeCase_Spike_MakesLongUpperWick()
        {
            StockSeries series = new StockSeries();
            long start = 0L;
            series.AppendSample(start, 100f);
            series.AppendSample(start + 250L, 100f);
            series.AppendSample(start + 500L, 260f);   // 单点尖峰
            series.AppendSample(start + 750L, 100f);
            StockCandle c = series.CandleAt(EconomyPeriodTier.Day, 0);
            Assert.AreEqual(100f, c.Open);
            Assert.AreEqual(260f, c.High, "上影 = 尖峰");
            Assert.AreEqual(100f, c.Low);
            Assert.AreEqual(100f, c.Close, "尖峰不是收 ⇒ 长上影、实体扁");
            Assert.IsFalse(c.IsFlat);
        }

        [Test]
        public void EdgeCase_MonotonicDescent_OpenEqualsHigh()
        {
            StockSeries series = new StockSeries();
            long start = 0L;
            float[] values = { 90f, 80f, 70f, 60f, 50f };
            for (int i = 0; i < values.Length; i++)
            {
                series.AppendSample(start + i * 250L, values[i]);
            }
            StockCandle c = series.CandleAt(EconomyPeriodTier.Day, 0);
            Assert.AreEqual(90f, c.Open, "开 = 高（单向下跌）");
            Assert.AreEqual(90f, c.High);
            Assert.AreEqual(50f, c.Low, "低 = 收（无下影）");
            Assert.AreEqual(50f, c.Close);
        }

        [Test]
        public void EdgeCase_ZeroStock_StaysZeroAndDeltaIsHonest()
        {
            StockSeries series = new StockSeries();
            for (int i = 0; i < 10; i++)
            {
                series.AppendSample(i * 250L, 0f);
            }
            StockCandle c = series.CandleAt(EconomyPeriodTier.Day, 0);
            Assert.AreEqual(0f, c.Open);
            Assert.AreEqual(0f, c.High);
            Assert.AreEqual(0f, c.Low);
            Assert.AreEqual(0f, c.Close);

            float pct;
            Assert.IsFalse(series.TryDeltaPct(EconomyPeriodTier.Day, out pct), "首根开 = 0 ⇒ 不编造百分比（LAND-009）");
        }

        // ---------------- ④ 保留策略（2026-09-21 裁决：全档无上限 + 退役槽位清空） ----------------

        /// <summary>
        /// **无上限**（用户 2026-09-21 裁决「蜡烛线无限保存」）：日档喂满 3 游戏年（180 日）
        /// ⇒ 一根不丢（最老仍可读）；退役的 60分 槽位恒空（读写两路径都不落入）。
        /// </summary>
        [Test]
        public void Retention_UnboundedKeepsAllHistory_AndRetiredHourSlotStaysEmpty()
        {
            StockSeries series = new StockSeries();
            const int Days = 3 * 60;                                  // 3 游戏年
            for (int d = 0; d < Days; d++)
            {
                series.AppendSample(d * EconomyPeriodTier.TicksPerDay, 100f + d);
            }

            Assert.AreEqual(Days, series.CountOf(EconomyPeriodTier.Day), "无上限 ⇒ 日档全历史保留（已封 + 进行中）");
            StockCandle first = series.CandleAt(EconomyPeriodTier.Day, 0);
            Assert.AreEqual(100f, first.Open, "最老一根仍在（未被裁剪）");
            Assert.AreEqual(0, series.CountOf(EconomyPeriodTier.Hour60), "退役 60分 槽位恒空");
        }

        // ---------------- ⑤ 上卷语义 ----------------

        /// <summary>
        /// 日档 = 当日**原始样本**的包络（基准档 = 日，样本直落日档）。
        /// 60分 档退役前该日蜡烛由"小时蜡烛上卷"而来 —— 两者逐项等价（包络结合律）。
        /// </summary>
        [Test]
        public void Rollup_DayCandle_EnvelopesDailySamples()
        {
            StockSeries series = new StockSeries();
            // 一整天：24 小时 × 10 点，数量 = hourIndex + offset（每小时两点：低点 = h，高点 = h + 5）
            for (int hour = 0; hour < 24; hour++)
            {
                long hourStart = (long)hour * EconomyPeriodTier.TicksOf(EconomyPeriodTier.Hour60);
                for (int i = 0; i < 10; i++)
                {
                    float v = hour * 10f + (i == 5 ? 5f : 0f);
                    series.AppendSample(hourStart + i * 250L, v);
                }
            }
            // ⚠ 末小时仍是「进行中桶」⇒ 尚未上卷进日档（预览"进行中桶"同义）。
            // 再喂一个跨日采样点（第 2 日起点）⇒ 封掉末小时 + 封掉第 1 日，得到完整日蜡烛。
            series.AppendSample(EconomyPeriodTier.TicksPerDay, 240f);

            StockCandle day = series.CandleAt(EconomyPeriodTier.Day, 0);
            Assert.IsNotNull(day, "第 1 日已封桶");
            Assert.AreEqual(0f, day.Open, "开 = 首个小时的开");
            Assert.AreEqual(230f, day.Close, "收 = 末小时末个样本（23×10 + 0；尖峰在 i=5，不是末点）");
            Assert.AreEqual(235f, day.High, "高 = 小时极值上包络（末小时尖峰 235）");
            Assert.AreEqual(0f, day.Low, "低 = 小时极值下包络");
            Assert.AreEqual(240, day.Samples, "样本数 = 24 小时 × 10 点");
        }

        /// <summary>
        /// 上卷图回归（两次修正后的终态）：**高档不得等待低档逐级封桶** ——
        /// ①原次级链（`月→季`/`季→年`）让季须等 15 游戏日、年须等 30 游戏日（实机"月以上长期空态"）；
        /// ②60分 退役后基准档 = 日。终态：**日封桶即喂 周/月/季/年 ⇒ 1 游戏日内所有高档都有进行中蜡烛**；
        /// 包络结合律保证与"逐级喂入"逐项等价。
        /// </summary>
        [Test]
        public void Rollup_AllUpperTiers_AppearOnFirstDayClose()
        {
            StockSeries series = new StockSeries();
            // 第 0 日：10 个点（低 100 / 尖峰 105 / 末点 103）；第 1 日首个点触发封桶 + 上卷
            for (int i = 0; i < 10; i++)
            {
                series.AppendSample(i * 250L, i == 9 ? 103f : (i == 5 ? 105f : 100f));
            }
            series.AppendSample(EconomyPeriodTier.TicksPerDay, 101f);

            int[] upper = { EconomyPeriodTier.Week, EconomyPeriodTier.Month, EconomyPeriodTier.Quarter, EconomyPeriodTier.Year };
            foreach (int tier in upper)
            {
                string name = "槽位 " + tier;
                Assert.AreEqual(1, series.CountOf(tier), name + "：基准档封桶后即应有 1 根（进行中）");
                StockCandle candle = series.CandleAt(tier, 0);
                Assert.IsNotNull(candle, name + " 蜡烛不得为 null");
                Assert.AreEqual(100f, candle.Open, name + " 开 = 首个样本");
                Assert.AreEqual(105f, candle.High, name + " 高 = 日内极值");
                Assert.AreEqual(100f, candle.Low, name + " 低 = 日内极值");
                Assert.AreEqual(103f, candle.Close, name + " 收 = 末个样本");
                Assert.AreEqual(10, candle.Samples, name + " 样本数 = 该日 10 点");
            }

            // 日档（基准档）：1 根已封 + 1 根进行中
            Assert.AreEqual(2, series.CountOf(EconomyPeriodTier.Day), "日档 = 1 已封 + 1 进行中");
            Assert.AreEqual(103f, series.CandleAt(EconomyPeriodTier.Day, 0).Close, "已封日蜡烛收 = 第 0 日末样本");

            // 同源不变式：此刻各高档同值（都由同一根日蜡烛喂入）—— 等价性的可执行证据
            StockCandle week = series.CandleAt(EconomyPeriodTier.Week, 0);
            StockCandle year = series.CandleAt(EconomyPeriodTier.Year, 0);
            Assert.AreEqual(week.Open, year.Open, "同源：周与年同值");
            Assert.AreEqual(week.Samples, year.Samples, "同源：样本数一致");
        }

        // ---------------- ⑥ 集合惰建与空态 ----------------

        [Test]
        public void Store_LazyAllocAndEmptyIsHonest()
        {
            StockSeriesStore store = new StockSeriesStore();
            Assert.IsFalse(store.HasAnyData, "新建集合 = 空态");
            Assert.IsNull(store.Get("Steel"), "未追踪 ⇒ null（不是空序列）");

            StockSeries steel = store.GetOrCreate("Steel");
            Assert.IsNotNull(steel);
            Assert.AreSame(steel, store.GetOrCreate("Steel"), "同键复用（惰建）");
            steel.AppendSample(0L, 12f);
            Assert.IsTrue(store.HasAnyData);

            Assert.IsNull(new StockSeriesStore().Get("Steel"), "“查无起点”不得画 0 值蜡烛");
        }

        /// <summary>
        /// 有界性硬约束（`MaxTrackedDefs`）回归：灌满 ⇒ 拒绝新键；已存在的键照常写入。
        /// ⚠ 该用例成立的前提 = `WarnOverflow` 已做离线隔离（否则无 TickManager/Log 宿主会抛）。
        /// </summary>
        [Test]
        public void Store_RejectsNewKeysBeyondCap()
        {
            StockSeriesStore store = new StockSeriesStore();
            int cap = StockSeriesStore.MaxTrackedDefs;
            for (int i = 0; i < cap; i++)
            {
                Assert.IsNotNull(store.GetOrCreate("Def" + i), "前 " + cap + " 个键应可建");
            }
            Assert.IsNull(store.GetOrCreate("Overflow"), "超出有界键上限 ⇒ 拒绝新键（不得无界增长）");
            Assert.IsNotNull(store.Get("Def0"), "已达上限不影响既有键");

            StockSeries existing = store.Get("Def0");
            existing.AppendSample(0L, 7f);
            Assert.IsTrue(store.HasAnyData);
            Assert.AreEqual(cap, store.Count, "键数停在上限，未越界");
        }

        // ---------------- 工具 ----------------

        /// <summary>确定性样本流（与预览 mulberry32 同构的线性同余；仅测试用）。</summary>
        private static List<float> Synthesize(int count, long stepTicks)
        {
            uint seed = 12345u;
            List<float> values = new List<float>(count);
            for (int i = 0; i < count; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                float r = (seed >> 8) / 16777216f;
                values.Add(100f + r * 400f);
            }
            return values;
        }

        /// <summary>按 <paramref name="stepTicks"/> 逐点喂入（起点 0）。</summary>
        private static StockSeries Feed(long startTick, long stepTicks, List<float> values)
        {
            StockSeries series = new StockSeries();
            for (int i = 0; i < values.Count; i++)
            {
                series.AppendSample(startTick + i * stepTicks, values[i]);
            }
            return series;
        }

        /// <summary>批式参考：按桶跨度分组，取 [首个, max, min, 末个]。</summary>
        private static List<StockCandle> Reference(List<float> values, long stepTicks, long bucketSpan)
        {
            List<StockCandle> closed = new List<StockCandle>();
            Dictionary<long, List<float>> buckets = new Dictionary<long, List<float>>();
            List<long> order = new List<long>();
            for (int i = 0; i < values.Count; i++)
            {
                long tick = i * stepTicks;
                long bucket = tick - (tick % bucketSpan);
                List<float> list;
                if (!buckets.TryGetValue(bucket, out list))
                {
                    list = new List<float>();
                    buckets.Add(bucket, list);
                    order.Add(bucket);
                }
                list.Add(values[i]);
            }
            // 末桶 = 进行中桶（与实现一致）⇒ 只比对已封桶
            for (int k = 0; k < order.Count - 1; k++)
            {
                List<float> list = buckets[order[k]];
                float open = list[0], high = list[0], low = list[0];
                for (int j = 1; j < list.Count; j++)
                {
                    if (list[j] > high) high = list[j];
                    if (list[j] < low) low = list[j];
                }
                closed.Add(new StockCandle(open, high, low, list[list.Count - 1], list.Count));
            }
            return closed;
        }

        /// <summary>逐桶全等对账（开/高/低/收/样本数）。</summary>
        private static void AssertClosedEqual(StockSeries series, int tier, List<StockCandle> expected)
        {
            int closedCount = series.CountOf(tier) - 1;   // 末根 = 进行中桶
            Assert.AreEqual(expected.Count, closedCount, "已封桶数量一致");
            for (int i = 0; i < expected.Count; i++)
            {
                StockCandle actual = series.CandleAt(tier, i);
                Assert.IsNotNull(actual, "第 " + i + " 根不应为 null");
                Assert.AreEqual(expected[i].Open, actual.Open, 1e-6, "第 " + i + " 根 开");
                Assert.AreEqual(expected[i].High, actual.High, 1e-6, "第 " + i + " 根 高");
                Assert.AreEqual(expected[i].Low, actual.Low, 1e-6, "第 " + i + " 根 低");
                Assert.AreEqual(expected[i].Close, actual.Close, 1e-6, "第 " + i + " 根 收");
                Assert.AreEqual(expected[i].Samples, actual.Samples, "第 " + i + " 根 样本数");
            }
        }
    }
}
