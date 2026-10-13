using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 一根蜡烛（**库存数量** OHLC，`SDD-LCE-003` §3）。
    ///
    /// <para><b>定义（与预览 v7 同构，勿另立解释）</b>：开 = 桶内**首个**样本；
    /// 高/低 = 桶内**极值**；收 = 桶内**最新**样本。<c>Samples</c> = 该桶的样本数
    /// （0 表示"桶已建但无样本"，绘制期按 doji 处理）。</para>
    ///
    /// <para><b>为什么是 class 而不是 struct</b>：本类要被 <c>Scribe_Collections.Look</c>
    /// 以 <c>LookMode.Deep</c> 递归序列化（struct 走不了 Deep）；每品种 ≤ 280 根、
    /// 属**封桶/读档**路径而非每帧路径，对象开销可接受（PERF-001）。</para>
    /// </summary>
    public sealed class StockCandle : IExposable
    {
        /// <summary>开（桶内首个样本）。</summary>
        public float Open;

        /// <summary>高（桶内极大）。</summary>
        public float High;

        /// <summary>低（桶内极小）。</summary>
        public float Low;

        /// <summary>收（桶内最新样本）。</summary>
        public float Close;

        /// <summary>桶内样本数（0 = 无样本）。</summary>
        public int Samples;

        /// <summary>空构造（Scribe / 池化用；**不是**合法蜡烛，需先喂首个样本）。</summary>
        public StockCandle()
        {
        }

        /// <summary>以首个样本建桶：开 = 高 = 低 = 收 = <paramref name="value"/>。</summary>
        public StockCandle(float value)
        {
            Open = value;
            High = value;
            Low = value;
            Close = value;
            Samples = 1;
        }

        /// <summary>由既有蜡烛复制（上卷起始桶用：开不变、高低收继承）。</summary>
        public StockCandle(float open, float high, float low, float close, int samples)
        {
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Samples = samples;
        }

        /// <summary>并入一个**原始样本**：收更新，高/低取极值（开<b>永远不变</b>）。</summary>
        public void Merge(float value)
        {
            if (value > High)
            {
                High = value;
            }
            if (value < Low)
            {
                Low = value;
            }
            Close = value;
            Samples++;
        }

        /// <summary>并入一根**下级蜡烛**（上卷）：极值取包络、收继承、样本数累加（开<b>不变</b>）。</summary>
        public void Merge(StockCandle other)
        {
            if (other == null)
            {
                return;
            }
            if (other.High > High)
            {
                High = other.High;
            }
            if (other.Low < Low)
            {
                Low = other.Low;
            }
            Close = other.Close;
            Samples += other.Samples;
        }

        /// <summary>是否"平线"（开 = 高 = 低 = 收）—— doji 判据，绘制期按最小实体处理。</summary>
        public bool IsFlat
        {
            get { return Open == High && High == Low && Low == Close; }
        }

        /// <summary>Scribe：四个值 + 样本数（扁平，无嵌套）。</summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref Open, "o", 0f);
            Scribe_Values.Look(ref High, "h", 0f);
            Scribe_Values.Look(ref Low, "l", 0f);
            Scribe_Values.Look(ref Close, "c", 0f);
            Scribe_Values.Look(ref Samples, "n", 0);
        }
    }
}
