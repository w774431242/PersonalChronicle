using NUnit.Framework;
using PersonalChronicle.Domain;
using RimWorld;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// <see cref="ProductionAccumulator"/> 的**近 8 季产出市值滚动桶**（v4.18 新增持久化字段）：
    /// 同季累加 · 跨季左移 · 超窗清零 · 新实例/旧档默认全 0。
    /// 这是「存档兼容 + 累计贡献趋势」的关键路径，缺测试就会在某次重构里静默错位。
    /// </summary>
    [TestFixture]
    public class ProductionAccumulatorTests
    {
        /// <summary>每季 tick 数（与 <see cref="GenDate.TicksPerQuadrum"/> 一致性由专门用例守护）。</summary>
        private const long Q = 900000L;

        [Test]
        public void QuadrumTicks_MatchGameConstant()
        {
            // 桶的季度长度必须与游戏一致：不一致会让「段 7 = 当前季」错位，曲线整体偏移一季。
            Assert.AreEqual(Q, (long)GenDate.TicksPerQuadrum);
        }

        [Test]
        public void FreshInstance_IsAllZero_AndAnchorUnset()
        {
            ProductionAccumulator acc = new ProductionAccumulator();

            Assert.AreEqual(-1L, acc.QuadrumAnchor, "新实例尚未写入时序锚点");
            Assert.AreEqual(ProductionAccumulator.QuadrumSlots, acc.MarketValueByQuadrum.Count);
            for (int i = 0; i < ProductionAccumulator.QuadrumSlots; i++)
            {
                Assert.AreEqual(0f, acc.MarketValueByQuadrum[i], 0.001f, "旧档/新档默认必须全 0（不伪造历史）");
            }
        }

        [Test]
        public void SameQuadrum_AccumulatesIntoLastSlot()
        {
            ProductionAccumulator acc = new ProductionAccumulator();
            acc.Add("Steel", 10, 100f, 3 * Q + 5000L);
            acc.Add("Steel", 5, 250f, 3 * Q + 9000L);

            for (int i = 0; i < ProductionAccumulator.QuadrumSlots - 1; i++)
            {
                Assert.AreEqual(0f, acc.MarketValueByQuadrum[i], 0.001f, "非当前季段应为 0");
            }
            Assert.AreEqual(350f, acc.MarketValueByQuadrum[ProductionAccumulator.QuadrumSlots - 1], 0.001f,
                "同季多次产出应累加到末段（= 当前季）");
        }

        [Test]
        public void AdvanceQuadrum_ShiftsBucketsLeft()
        {
            ProductionAccumulator acc = new ProductionAccumulator();
            acc.Add("Steel", 1, 100f, 0L);       // 季 0 → 末段
            acc.Add("Steel", 1, 200f, 2 * Q);    // 跨 2 季 → 旧值左移 2 段

            Assert.AreEqual(100f, acc.MarketValueByQuadrum[5], 0.001f, "季 0 的产出应左移到段 5");
            Assert.AreEqual(0f, acc.MarketValueByQuadrum[6], 0.001f, "中间空季必须补 0，不得留旧值");
            Assert.AreEqual(200f, acc.MarketValueByQuadrum[7], 0.001f, "当前季产出落在末段");
        }

        [Test]
        public void AdvanceQuadrum_BeyondWindow_ClearsAll()
        {
            ProductionAccumulator acc = new ProductionAccumulator();
            acc.Add("Steel", 1, 500f, 0L);
            acc.AdvanceQuadrum(20L);   // 跨 20 季 > 8 槽 ⇒ 整个窗口清空

            for (int i = 0; i < ProductionAccumulator.QuadrumSlots; i++)
            {
                Assert.AreEqual(0f, acc.MarketValueByQuadrum[i], 0.001f, "超出 8 季窗口的数据必须清零");
            }
        }

        [Test]
        public void AdvanceQuadrum_IsIdempotent_ForSameOrEarlierQuadrum()
        {
            ProductionAccumulator acc = new ProductionAccumulator();
            acc.Add("Steel", 1, 120f, 4 * Q);

            acc.AdvanceQuadrum(4L);   // 同季重复对齐
            acc.AdvanceQuadrum(3L);   // 更早的季（不应倒移）

            Assert.AreEqual(120f, acc.MarketValueByQuadrum[ProductionAccumulator.QuadrumSlots - 1], 0.001f);
            Assert.AreEqual(4L, acc.QuadrumAnchor);
        }
    }
}
