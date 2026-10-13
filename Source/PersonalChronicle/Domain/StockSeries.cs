using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 单个品种的**库存数量**蜡烛序列（显示五档 + 退役槽位 + 进行中桶，`SDD-LCE-003` §3）。
    ///
    /// <para><b>在线增量聚合</b>：<see cref="AppendSample"/> 只更新**基准档（日）**的进行中桶；
    /// 桶边界越过即封桶 → 按上卷图（`日 → 周/月/季/年`）把已封蜡烛并入父档的
    /// 进行中桶 —— **不做跨档重复采样**（REQ §4.1 性能策略）。</para>
    ///
    /// <para><b>保留（2026-09-21 用户裁决「无上限」）</b>：日/周/月/季/年 = **全历史保留**
    /// （<see cref="EconomyPeriodTier.Unbounded"/>）；退役的 60分 槽位在 <see cref="Ensure"/> 中
    /// 被清空。代价与约束（体积线性增长 / 绘制抽稀 / EXC）见
    /// `docs/设计文档/功能模块/经济体系/评估_蜡烛线无界保存-2026-09-21.md`。</para>
    ///
    /// <para><b>对齐纪律（B-02 同款）</b>：写入（<see cref="AppendSample"/>）与
    /// **读取**（<see cref="CandleAt"/> / <see cref="TryDeltaPct"/>）都会经过
    /// <see cref="Ensure"/>；只在写入时建环的话，读档/新档场景会拿到 null 环。</para>
    ///
    /// <para><b>诚实（LAND-009）</b>：没有数据的档返回 0 根 / `false`，**不编造 0 值蜡烛**；
    /// 进行中桶会随读数返回（图右侧"当前蜡烛"），与预览的"进行中桶"同义。</para>
    /// </summary>
    public sealed class StockSeries : IExposable
    {
        /// <summary>一档的已封蜡烛环（最老在前）。</summary>
        private sealed class TierRing : IExposable
        {
            public List<StockCandle> Candles = new List<StockCandle>();

            /// <summary>入环并按上限裁剪（封桶频率低 ⇒ 一次 O(n) 移位可接受，n ≤ 60）。</summary>
            public void Add(StockCandle candle, int cap)
            {
                if (cap <= 0 || candle == null)
                {
                    return;
                }
                if (Candles.Count >= cap)
                {
                    Candles.RemoveAt(0);
                }
                Candles.Add(candle);
            }

            /// <summary>
            /// 仅按上限裁剪（**不新增**）。供 <c>Ensure()</c> 在读档后调用 ——
            /// ⚠ 不可写成 <see cref="Add"/>(null, cap)：Add 对 null 直接 return，
            /// 会让"读档后再裁剪一次"变成 no-op（GATE-DATA EXC-1 已登记）。
            /// </summary>
            public void Trim(int cap)
            {
                if (cap <= 0)
                {
                    Candles.Clear();
                    return;
                }
                while (Candles.Count > cap)
                {
                    Candles.RemoveAt(0);
                }
            }

            public void ExposeData()
            {
                Scribe_Collections.Look(ref Candles, "k", LookMode.Deep);
                if (Candles == null)
                {
                    Candles = new List<StockCandle>();
                }
            }
        }

        /// <summary>一档的进行中桶（未封桶；<c>Has=false</c> = 无进行中桶）。</summary>
        private sealed class TierForming : IExposable
        {
            public bool Has;
            public long Bucket;
            public StockCandle Candle;

            public void ExposeData()
            {
                Scribe_Values.Look(ref Has, "has", false);
                Scribe_Values.Look(ref Bucket, "bucket", 0L);
                Scribe_Deep.Look(ref Candle, "candle");
            }
        }

        private List<TierRing> rings = new List<TierRing>();
        private List<TierForming> forming = new List<TierForming>();

        /// <summary>
        /// 并入一个采样点（<c>absTick</c> = **TicksAbs**；<c>value</c> = 该品种库存数量）。
        /// 低档先封、高档后封（上卷图全部升序 ⇒ 一趟即可）。
        /// </summary>
        public void AppendSample(long absTick, float value)
        {
            if (value < 0f)
            {
                value = 0f;
            }
            Ensure();

            for (int t = 0; t < EconomyPeriodTier.Count; t++)
            {
                TierForming f = forming[t];
                if (!f.Has)
                {
                    continue;
                }
                if (f.Bucket != EconomyPeriodTier.BoundaryOf(t, absTick))
                {
                    CloseTier(t, f);
                }
            }

            FeedSample(EconomyPeriodTier.BaseTier, absTick, value);
        }

        /// <summary>
        /// 封桶：入环 + 上卷进**全部高档**（父档桶号按"已封桶的末刻"算，避免跨档错挂）。
        ///
        /// <para><b>上卷图（2026-09-21 两次修正后的终态）</b>：`日 → 周/月/季/年`
        /// —— 基准档（<see cref="EconomyPeriodTier.BaseTier"/> = 日）封桶时**直喂全部高档**。
        /// ①（实机反馈）原次级链（`月→季`；`季→年`）会让季须等 15 游戏日、年须等 30 游戏日
        /// ⇒ 「月以上长期空态」；②（用户裁决）60分 档退役后，基准档下移为日，
        /// **所有高档在 1 游戏日内出现进行中蜡烛**。</para>
        ///
        /// <para><b>为什么等价</b>：OHLC 包络满足结合律 —— 季/年由日直接喂 ≡ 由月/季逐级喂，
        /// 开（首喂）· 高/低（极值）· 收（末喂）· 样本数（求和）逐项相同；且喂入按时间升序，
        /// 桶内首末位置不变。**成本**：每基准桶封桶 ≤ 4 次 O(1) 归并（典型 100 品种 ⇒ 每日 400 次，
        /// 可忽略），仍**不做跨档重复采样**（喂的是已封蜡烛，不重扫原始样本）。</para>
        /// </summary>
        private void CloseTier(int tier, TierForming f)
        {
            rings[tier].Add(f.Candle, EconomyPeriodTier.RetentionOf(tier));
            long lastTick = f.Bucket + EconomyPeriodTier.TicksOf(tier) - 1L;

            if (tier == EconomyPeriodTier.BaseTier)
            {
                for (int parent = EconomyPeriodTier.BaseTier + 1; parent < EconomyPeriodTier.Count; parent++)
                {
                    Feed(parent, f.Candle, lastTick);
                }
            }

            f.Has = false;
            f.Candle = null;
        }

        /// <summary>把一根已封蜡烛并入父档的进行中桶（同桶则极值包络，跨桶则另起）。</summary>
        private void Feed(int parentTier, StockCandle candle, long refTick)
        {
            if (candle == null)
            {
                return;
            }
            long bucket = EconomyPeriodTier.BoundaryOf(parentTier, refTick);
            TierForming f = forming[parentTier];
            if (!f.Has || f.Bucket != bucket)
            {
                f.Has = true;
                f.Bucket = bucket;
                f.Candle = new StockCandle(candle.Open, candle.High, candle.Low, candle.Close, candle.Samples);
                return;
            }
            f.Candle.Merge(candle);
        }

        /// <summary>把原始样本并入（仅基准档调用）。</summary>
        private void FeedSample(int tier, long absTick, float value)
        {
            long bucket = EconomyPeriodTier.BoundaryOf(tier, absTick);
            TierForming f = forming[tier];
            if (!f.Has || f.Bucket != bucket)
            {
                f.Has = true;
                f.Bucket = bucket;
                f.Candle = new StockCandle(value);
                return;
            }
            f.Candle.Merge(value);
        }

        /// <summary>该档的蜡烛数（已封 + 进行中桶）。</summary>
        public int CountOf(int tier)
        {
            int t = EconomyPeriodTier.Resolve(tier);
            Ensure();
            return rings[t].Candles.Count + (forming[t].Has ? 1 : 0);
        }

        /// <summary>
        /// 取第 <paramref name="indexFromOldest"/> 根（0 = 最老；末根 = 进行中桶，若有）。
        /// 越界或空 ⇒ `null`（调用方按空态处理，不得画 0 值蜡烛）。
        /// </summary>
        public StockCandle CandleAt(int tier, int indexFromOldest)
        {
            int t = EconomyPeriodTier.Resolve(tier);
            Ensure();
            List<StockCandle> closed = rings[t].Candles;
            if (indexFromOldest < 0 || indexFromOldest >= closed.Count + (forming[t].Has ? 1 : 0))
            {
                return null;
            }
            if (indexFromOldest == closed.Count)
            {
                return forming[t].Candle;
            }
            return closed[indexFromOldest];
        }

        /// <summary>该档最后一根的收（无数据 ⇒ `false`）。</summary>
        public bool TryLastClose(int tier, out float close)
        {
            close = 0f;
            int n = CountOf(tier);
            if (n <= 0)
            {
                return false;
            }
            StockCandle last = CandleAt(tier, n - 1);
            if (last == null)
            {
                return false;
            }
            close = last.Close;
            return true;
        }

        /// <summary>
        /// 该档窗口涨跌幅（`%`）：(末根收 − 首根开) / 首根开 × 100 —— 热力图色阶口径。
        /// `false` = 无数据 或 首根开为 0（**不编造**百分比，LAND-009）。
        /// </summary>
        public bool TryDeltaPct(int tier, out float pct)
        {
            pct = 0f;
            int n = CountOf(tier);
            if (n <= 0)
            {
                return false;
            }
            StockCandle first = CandleAt(tier, 0);
            StockCandle last = CandleAt(tier, n - 1);
            if (first == null || last == null || first.Open <= 0f)
            {
                return false;
            }
            pct = (last.Close - first.Open) / first.Open * 100f;
            return true;
        }

        /// <summary>是否**任何档**有数据（空态判据）。</summary>
        public bool HasAnyData
        {
            get
            {
                Ensure();
                for (int t = 0; t < EconomyPeriodTier.Count; t++)
                {
                    if (rings[t].Candles.Count > 0 || forming[t].Has)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>六档环/进行中桶归一（构造后、读档后都过一遍）+ 上限裁剪。</summary>
        private void Ensure()
        {
            if (rings == null)
            {
                rings = new List<TierRing>(EconomyPeriodTier.Count);
            }
            if (forming == null)
            {
                forming = new List<TierForming>(EconomyPeriodTier.Count);
            }
            while (rings.Count < EconomyPeriodTier.Count)
            {
                rings.Add(new TierRing());
            }
            while (forming.Count < EconomyPeriodTier.Count)
            {
                forming.Add(new TierForming());
            }
            for (int t = 0; t < EconomyPeriodTier.Count; t++)
            {
                // 读档后按档位策略收敛：
                //   · 退役槽位（60分）= 上限 0 ⇒ 清空（迁移语义，见 EconomyPeriodTier 类头）
                //   · 显示五档 = Unbounded ⇒ Trim 循环永不进入（全历史保留）
                rings[t].Trim(EconomyPeriodTier.RetentionOf(t));
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref rings, "rings", LookMode.Deep);
            Scribe_Collections.Look(ref forming, "forming", LookMode.Deep);
            Ensure();
        }
    }
}
