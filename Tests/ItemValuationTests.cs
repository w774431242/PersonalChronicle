using System.IO;
using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Finance;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// CHG-20261010-VALUE-PRICE（用户裁决：按物品自身价值作为售价）的离线可证伪面。
    ///
    /// <para>只断言**纯函数**：价值优先于兜底值、四舍五入口径、下限、不可用时的回落、上限钳制与不抛异常。
    /// 真实结算（prepare/complete 是否按该金额扣款）属 GameVerified，本文件不冒充覆盖。</para>
    ///
    /// <para><b>CHG-20261012 · B2</b>：兜底值不再来自机构政策（政策价整体退役）—— 生产调用点一律只传
    /// 物品市价，兜底参数只为"函数自身的通用契约"保留（见 <see cref="UnusableValueFallsBackToTheExplicitFallback"/>）。</para>
    /// </summary>
    [TestFixture]
    public sealed class ItemValuationTests
    {
        /// <summary>★ 反向对照：显式兜底 6000（60.00 银）而物品价值 45 银 ⇒ 必须按物品价值 4500，不得落回 6000。</summary>
        [Test] public void PositiveValueWinsOverTheExplicitFallback()
        {
            Assert.That(ItemValuation.UnitPriceMinor(45f, 6000L), Is.EqualTo(4500L));
            Assert.That(ItemValuation.UnitPriceMinor(45f, 6000L), Is.Not.EqualTo(6000L),
                "只要物品有正价值，价格就必须来自物品价值而不是兜底值");
        }

        [Test] public void ValueIsRoundedToTheNearestMinorUnit()
        {
            Assert.That(ItemValuation.UnitPriceMinor(1.234f, 0L), Is.EqualTo(123L));
            Assert.That(ItemValuation.UnitPriceMinor(1.235f, 0L), Is.EqualTo(124L), "0.005 银必须进位（AwayFromZero）");
            Assert.That(ItemValuation.UnitPriceMinor(123.456f, 0L), Is.EqualTo(12346L));
        }

        [Test] public void TinyPositiveValueCostsAtLeastOneMinorUnit()
            => Assert.That(ItemValuation.UnitPriceMinor(0.0001f, 0L), Is.EqualTo(ItemValuation.MinimumPriceMinor));

        [Test] public void UnusableValueFallsBackToTheExplicitFallback()
        {
            Assert.That(ItemValuation.UnitPriceMinor(0f, 6000L), Is.EqualTo(6000L), "零价值 → 显式兜底值");
            Assert.That(ItemValuation.UnitPriceMinor(-5f, 6000L), Is.EqualTo(6000L), "负价值 → 显式兜底值");
            Assert.That(ItemValuation.UnitPriceMinor(float.NaN, 6000L), Is.EqualTo(6000L), "NaN 不得变成价格");
            Assert.That(ItemValuation.UnitPriceMinor(0f, 0L), Is.EqualTo(0L),
                "两边都不可用 ⇒ 0（上层据此报 PriceUnavailable，未知不当价）");
            // B2：生产调用点**不再传兜底值**（政策价退役）⇒ 价值不可用一律得 0，由上层如实拒绝。
            Assert.That(ItemValuation.UnitPriceMinor(0f), Is.EqualTo(0L), "不传兜底 ⇒ 不可用即 0");
            Assert.That(ItemValuation.UnitPriceMinor(float.NaN), Is.EqualTo(0L), "不传兜底 ⇒ NaN 即 0");
        }

        [Test] public void AbsurdValueIsClampedAndNeverThrows()
        {
            long max = ItemValuation.UnitPriceMinor(float.MaxValue, 0L);
            Assert.That(max, Is.EqualTo(MinorCurrency.Maximum));
            Assert.That(ItemValuation.UnitPriceMinor(float.PositiveInfinity, 0L), Is.EqualTo(MinorCurrency.Maximum));
            Assert.DoesNotThrow(() => MinorCurrency.Validate(max), "钳制后的金额必须落在货币合法域内");
        }

        /// <summary>
        /// ★ **稽核断言（调用点契约）**：售价必须取自**该件物品**（`item.MarketValue`）——
        /// 纯函数测试只能证明算式正确，**不能**证明调用点传进来的是物品价值（否则「按市场价格」是空口结论）。
        /// B2 起政策价整体退役 ⇒ 调用点**不得再传任何兜底值**（价值不可用就如实拒绝，見 EconomyConsumption）。
        /// 文件不存在同样变红（防改名/搬目录后稽核失效）。
        /// </summary>
        [Test] public void BothPriceSitesReadTheItemMarketValue()
        {
            string root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../.."));
            string folder = Path.Combine(root, "Source", "PersonalChronicle", "Economy", "PawnEconomy");
            var expectations = new[]
            {
                new { File = "EconomyConsumption.cs", Expected = "UnitPriceMinor(item.MarketValue)" },
                new { File = "EconomyCommands.cs", Expected = "UnitPriceMinor(item.MarketValue)" },
                new { File = "EconomyWorkspaceQueries.cs", Expected = "UnitPriceMinor(item.MarketValue)" },
            };
            foreach (var expectation in expectations)
            {
                string path = Path.Combine(folder, expectation.File);
                Assert.That(File.Exists(path), Is.True, "找不到价格调用点文件（稽核失效）：" + path);
                string text = File.ReadAllText(path);
                Assert.That(text.Contains(expectation.Expected), Is.True,
                    expectation.File + " 未按该件物品的市场价值定价；期望片段：" + expectation.Expected);
                // B2：任何"政策价 / 兜底价"的复用都视为回归（政策价已整体退役，兜底参数只为函数契约保留）。
                Assert.That(text.Contains("UnitPriceMinor(policy."), Is.False,
                    expectation.File + " 不得再传政策价作为定价或兜底（政策价已整体退役）");
                Assert.That(text.Contains("UnitPriceMinor(item.MarketValue, fallbackPrice)"), Is.False,
                    expectation.File + " 不得再传局部兜底价（B2：不可用即拒，不发明价格）");
            }
        }
    }
}
