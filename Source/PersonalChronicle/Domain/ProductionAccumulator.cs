using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Persistent production summary for a pawn. It replaces the need to keep
    /// every routine craft/build event as the only source of career statistics.
    /// </summary>
    public sealed class ProductionAccumulator : IExposable
    {
        /// <summary>
        /// 产出市值滚动桶的槽数（近 8 季，段 0 = 最老、段 7 = 当前季）。
        /// 逻辑实现在共用工具 <see cref="QuadrumBuckets"/>（WorkTime 同用，禁两处实现）。
        /// </summary>
        public const int QuadrumSlots = QuadrumBuckets.Slots;

        public Dictionary<string, int> QuantityByDef = new Dictionary<string, int>();
        public Dictionary<string, float> MarketValueByDef = new Dictionary<string, float>();
        public int TotalQuantity;
        public float TotalMarketValue;
        public long LastProductionTick = -1L;

        /// <summary>
        /// **近 8 季产出市值**（段 0 = 最老、段 7 = 当前季）——「累计贡献趋势」的数据源
        /// （预览 ⑥，HTML 941-965）。写入点 = <see cref="Add"/>；长度恒为
        /// <see cref="QuadrumSlots"/>（<see cref="EnsureQuadrumBuffer"/> 归一）。
        /// <para>⚠ 旧档兼容：v4.x 之前无此字段 ⇒ 默认全 0（历史产量不可追溯，趋势卡如实画空态，
        /// 与「分身份存量趋势」同一登记口径），无需 Migration。</para>
        /// </summary>
        public List<float> MarketValueByQuadrum = new List<float>(new float[QuadrumSlots]);

        /// <summary>段 7 对应的**绝对季度序号**（<c>tick / GenDate.TicksPerQuadrum</c>）；-1 = 尚未写入。</summary>
        public long QuadrumAnchor = -1L;

        public void Add(string defName, int quantity, float marketValue, long gameTick)
        {
            if (string.IsNullOrEmpty(defName) || quantity <= 0)
            {
                return;
            }
            if (QuantityByDef == null)
            {
                QuantityByDef = new Dictionary<string, int>();
            }
            if (MarketValueByDef == null)
            {
                MarketValueByDef = new Dictionary<string, float>();
            }
            int oldQuantity;
            if (!QuantityByDef.TryGetValue(defName, out oldQuantity))
            {
                oldQuantity = 0;
            }
            float oldValue;
            if (!MarketValueByDef.TryGetValue(defName, out oldValue))
            {
                oldValue = 0f;
            }
            QuantityByDef[defName] = oldQuantity + quantity;
            MarketValueByDef[defName] = oldValue + marketValue;
            TotalQuantity += quantity;
            TotalMarketValue += marketValue;
            if (gameTick > LastProductionTick)
            {
                LastProductionTick = gameTick;
            }
            AccumulateQuadrum(gameTick, marketValue);
        }

        /// <summary>
        /// 把一条产出记入「近 8 季」滚动桶（段 7 = 当前季）。
        /// 季度序号 = <c>gameTick / GenDate.TicksPerQuadrum</c>；跨季时整体左移、右侧补 0。
        /// </summary>
        private void AccumulateQuadrum(long gameTick, float marketValue)
        {
            AdvanceQuadrum(gameTick / GenDate.TicksPerQuadrum);
            if (marketValue != 0f)
            {
                MarketValueByQuadrum[QuadrumSlots - 1] += marketValue;
            }
        }

        /// <summary>
        /// 将滚动桶**对齐**到指定绝对季度序号（幂等、可安全重复调用）。
        ///
        /// <para>写入路径（<see cref="Add"/>）与**读取路径**（快照构建期）都要调用：只在对齐写入时，
        /// 一个闲置了 n 季的档案会把 n 季前的高度留在末段 ⇒ 曲线「停在过去」。读取时对齐即修正。
        /// 推进/归一逻辑统一在 <see cref="QuadrumBuckets"/>（与工时桶共用）。</para>
        /// </summary>
        public void AdvanceQuadrum(long quadrumIndex)
        {
            QuadrumBuckets.Advance(MarketValueByQuadrum, ref QuadrumAnchor, quadrumIndex);
        }

        /// <summary>桶长度/引用归一（存档载入后与旧档默认值都要过一遍）。</summary>
        private void EnsureQuadrumBuffer()
        {
            QuadrumBuckets.EnsureBuffer(MarketValueByQuadrum);
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref QuantityByDef, "quantityByDef", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref MarketValueByDef, "marketValueByDef", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref TotalQuantity, "totalQuantity", 0);
            Scribe_Values.Look(ref TotalMarketValue, "totalMarketValue", 0f);
            Scribe_Values.Look(ref LastProductionTick, "lastProductionTick", -1L);
            // v4.18：近 8 季市值滚动桶（旧档无此键 ⇒ 默认空列表 ⇒ 归一为 8 个 0 + Anchor −1）。
            Scribe_Collections.Look(ref MarketValueByQuadrum, "marketValueByQuadrum", LookMode.Value);
            Scribe_Values.Look(ref QuadrumAnchor, "quadrumAnchor", -1L);
            if (QuantityByDef == null)
            {
                QuantityByDef = new Dictionary<string, int>();
            }
            if (MarketValueByDef == null)
            {
                MarketValueByDef = new Dictionary<string, float>();
            }
            EnsureQuadrumBuffer();
        }
    }
}
