namespace PersonalChronicle.Domain.PawnEconomy.Finance
{
    /// <summary>
    /// **按物品自身价值定价**（2026-10-10 用户裁决 · 最小改动）。
    ///
    /// <para><b>规则</b>：商店每件物品的售价 = **该件物品的价值**（原版 <c>Thing.MarketValue</c>，单位银），
    /// 换算到内部最小货币单位（<see cref="MinorCurrency.PerSilver"/> = 100）；
    /// 只有当价值**不可用**（≤0 / NaN，例如模组物品未定价）时才回落到机构挂牌的衣物价（政策价从此是**兜底价**，
    /// 不再是定价本身）。</para>
    ///
    /// <para><b>为什么是纯函数</b>：这是**资金流出**的金额来源，必须能离线断言（四舍五入口径、下限、
    /// 回落、上限钳制、NaN）；也因此它不引用任何 RimWorld 类型 —— 调用方读 <c>MarketValue</c> 后把 float 传进来。</para>
    ///
    /// <para><b>不抛异常</b>：换算结果一律钳到 <see cref="MinorCurrency"/> 合法域（[0, Maximum]），
    /// 避免异常从购买/绘制路径逃出去（该域外的值会由 <see cref="MinorCurrency.Validate"/> 抛出）。</para>
    /// </summary>
    public static class ItemValuation
    {
        /// <summary>正价值物品的**最低价**（1 minor = 0.01 银）：与既有"总额四舍五入到最小货币单位、下限 1"口径一致，
        /// 避免极低价物品变成免费。</summary>
        public const long MinimumPriceMinor = 1L;

        /// <summary>单件售价（最小货币单位）。<paramref name="marketValueSilver"/> = 原版市场价值（银），
        /// <paramref name="fallbackMinor"/> = 价值不可用时的兜底价（机构政策价，可为 0）。</summary>
        public static long UnitPriceMinor(float marketValueSilver, long fallbackMinor = 0L)
        {
            long price = fallbackMinor;
            if (marketValueSilver > 0f)
            {
                double minor = (double)marketValueSilver * MinorCurrency.PerSilver;
                if (minor >= MinorCurrency.Maximum) price = MinorCurrency.Maximum;
                else
                {
                    price = (long)System.Math.Round(minor, System.MidpointRounding.AwayFromZero);
                    if (price < MinimumPriceMinor) price = MinimumPriceMinor;
                }
            }
            if (price < 0L) price = 0L;
            if (price > MinorCurrency.Maximum) price = MinorCurrency.Maximum;
            return price;
        }
    }
}
