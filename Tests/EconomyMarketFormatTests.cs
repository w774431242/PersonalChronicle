using NUnit.Framework;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Economy;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 市场面板**读数规则**的离线单测（<see cref="EconomyMarketFormat"/>）。
    ///
    /// <para>这些规则决定"屏幕上显示的数字对不对"：四舍五入、精度阶梯、归一化区间、
    /// 涨跌配色方向。它们**不抛异常**，所以错了只会安静地显示一个看着合理的错数 ——
    /// 这正是必须钉死的类型。</para>
    ///
    /// <para>纯逻辑、零引擎依赖（<c>SystemCenterTone</c> 是本工程枚举，非 Unity 类型）。</para>
    /// </summary>
    [TestFixture]
    public sealed class EconomyMarketFormatTests
    {
        // ── FormatRate：速率精度阶梯 ─────────────────────────────────────────────

        [Test]
        public void FormatRate_Zero_IsPlainZero()
        {
            Assert.AreEqual("0", EconomyMarketFormat.FormatRate(0f));
        }

        [Test]
        public void FormatRate_RoundsToInteger_NoDecimals()
        {
            // 2026-09-20 用户裁决「取整」：三级精度阶梯**已被否决** —— 任何输入都不得出现小数点。
            Assert.AreEqual("1", EconomyMarketFormat.FormatRate(1.25f));
            Assert.AreEqual("13", EconomyMarketFormat.FormatRate(12.5f)); // 远离零舍入
            Assert.AreEqual("4,280", EconomyMarketFormat.FormatRate(4280f));
        }

        [Test]
        public void FormatQuantity_PromotesLargeValues_ToLanguageScale()
        {
            // 中文「万」/10⁴
            Assert.AreEqual("1.2万", EconomyMarketFormat.FormatQuantity(12345f, "万", 10000f));
            // 英文「k」/10³（同一数值、不同语言包 ⇒ 不同升格，这正是它必须走 Keyed 的原因）
            Assert.AreEqual("12.3k", EconomyMarketFormat.FormatQuantity(12345f, "k", 1000f));
            // 阈值以下不升格（退回千分位整数）
            Assert.AreEqual("9,999", EconomyMarketFormat.FormatQuantity(9999f, "万", 10000f));
            // 参数缺失（键缺 / 解析失败）⇒ **不升格**，降级可解释
            Assert.AreEqual("12,345", EconomyMarketFormat.FormatQuantity(12345f, null, 0f));
            Assert.AreEqual(EconomyMarketFormat.Dash, EconomyMarketFormat.FormatQuantity(float.NaN, "万", 10000f));
        }

        [Test]
        public void FormatRate_KeepsSign()
        {
            // 净值为负 = 净出口，符号必须保留（丢掉符号会让"净进口/净出口"整个反向）
            Assert.AreEqual("-13", EconomyMarketFormat.FormatRate(-12.5f));
            Assert.AreEqual("-4,280", EconomyMarketFormat.FormatRate(-4280f));
        }

        [Test]
        public void FormatRate_NonFinite_BecomesDash_NotNaN()
        {
            Assert.AreEqual(EconomyMarketFormat.Dash, EconomyMarketFormat.FormatRate(float.NaN));
            Assert.AreEqual(EconomyMarketFormat.Dash, EconomyMarketFormat.FormatRate(float.PositiveInfinity));
            Assert.AreEqual(EconomyMarketFormat.Dash, EconomyMarketFormat.FormatRate(float.NegativeInfinity));
        }

        // ── FormatPercent：因子 → 百分比 ─────────────────────────────────────────

        [Test]
        public void FormatPercent_NeutralIs100()
        {
            Assert.AreEqual("100%", EconomyMarketFormat.FormatPercent(1f));
        }

        [Test]
        public void FormatPercent_RoundsToWholePercent()
        {
            Assert.AreEqual("118%", EconomyMarketFormat.FormatPercent(1.18f));
            Assert.AreEqual("150%", EconomyMarketFormat.FormatPercent(1.5f));
            Assert.AreEqual("50%", EconomyMarketFormat.FormatPercent(0.5f));
        }

        [Test]
        public void FormatPercent_NonFinite_BecomesDash()
        {
            Assert.AreEqual(EconomyMarketFormat.Dash, EconomyMarketFormat.FormatPercent(float.NaN));
            Assert.AreEqual(EconomyMarketFormat.Dash, EconomyMarketFormat.FormatPercent(float.PositiveInfinity));
        }

        // ── NormalizeFactor：线性归一化 + clamp ─────────────────────────────────

        [Test]
        public void NormalizeFactor_LinearBoundaries()
        {
            // 区间 [0.5, 2.5]（MvsConstants）：中点 1.5 ⇒ 0.5
            Assert.AreEqual(0f, EconomyMarketFormat.NormalizeFactor(0.5f, 0.5f, 2.5f), 0.0001f);
            Assert.AreEqual(0.5f, EconomyMarketFormat.NormalizeFactor(1.5f, 0.5f, 2.5f), 0.0001f);
            Assert.AreEqual(1f, EconomyMarketFormat.NormalizeFactor(2.5f, 0.5f, 2.5f), 0.0001f);
        }

        [Test]
        public void NormalizeFactor_PreservesRatio_NoLogCompression()
        {
            // 显示压缩判据：2:1 在显示上必须仍是 2:1。
            // 取 f 使 (f - min) / span 与另一点成 2:1 关系，验证实现是恒等仿射。
            float min = 0.5f, max = 2.5f, span = max - min; // 2.0
            float a = min + 0.1f * span; // 0.2
            float b = min + 0.2f * span; // 0.4
            float na = EconomyMarketFormat.NormalizeFactor(a, min, max);
            float nb = EconomyMarketFormat.NormalizeFactor(b, min, max);
            Assert.AreEqual(2f, nb / na, 0.0001f,
                "归一化必须是线性（恒等仿射）—— 任何让 2:1 变 1:1 的压缩都不可接受");
        }

        [Test]
        public void NormalizeFactor_ClampsOutOfRange()
        {
            Assert.AreEqual(0f, EconomyMarketFormat.NormalizeFactor(-999f, 0.5f, 2.5f), 0.0001f);
            Assert.AreEqual(1f, EconomyMarketFormat.NormalizeFactor(999f, 0.5f, 2.5f), 0.0001f);
        }

        [Test]
        public void NormalizeFactor_DegenerateRange_IsNeutral_NoDivideByZero()
        {
            // 区间退化 ⇒ 中性 0.5，绝不除零产生 NaN（NaN 会污染整个 band 数组）
            Assert.AreEqual(0.5f, EconomyMarketFormat.NormalizeFactor(1f, 2.5f, 0.5f), 0.0001f);
            Assert.AreEqual(0.5f, EconomyMarketFormat.NormalizeFactor(1f, 1f, 1f), 0.0001f);
        }

        // ── ToneForFactor：涨红跌绿（中式约定）─────────────────────────────────

        [Test]
        public void ToneForFactor_RiseIsRed_FallIsGreen()
        {
            // ⚠ 中式约定：涨 ⇒ Bad（红）/ 跌 ⇒ Good（绿）。与欧美相反，有意为之。
            Assert.AreEqual(SystemCenterTone.Bad, EconomyMarketFormat.ToneForFactor(1.5f, 1f),
                "涨价必须是红（Bad）—— 这是中国股市约定");
            Assert.AreEqual(SystemCenterTone.Good, EconomyMarketFormat.ToneForFactor(0.5f, 1f),
                "跌价必须是绿（Good）—— 这是中国股市约定");
        }

        [Test]
        public void ToneForFactor_NeutralBand_SuppressesNoise()
        {
            // ±1% 中性带：带内一律 Flat，避免噪声抖动被画成涨跌
            Assert.AreEqual(SystemCenterTone.Flat, EconomyMarketFormat.ToneForFactor(1f, 1f));
            Assert.AreEqual(SystemCenterTone.Flat, EconomyMarketFormat.ToneForFactor(1.005f, 1f));
            Assert.AreEqual(SystemCenterTone.Flat, EconomyMarketFormat.ToneForFactor(0.995f, 1f));
        }

        [Test]
        public void ToneForFactor_BandEdges_AreExclusive()
        {
            // 恰好 ±1% 仍为 Flat（严格大于/小于才越带）
            Assert.AreEqual(SystemCenterTone.Flat, EconomyMarketFormat.ToneForFactor(1.01f, 1f));
            Assert.AreEqual(SystemCenterTone.Flat, EconomyMarketFormat.ToneForFactor(0.99f, 1f));
            // 略微越带即变色
            Assert.AreEqual(SystemCenterTone.Bad, EconomyMarketFormat.ToneForFactor(1.011f, 1f));
            Assert.AreEqual(SystemCenterTone.Good, EconomyMarketFormat.ToneForFactor(0.989f, 1f));
        }

        [Test]
        public void ToneForFactor_NonFinite_IsFlat()
        {
            Assert.AreEqual(SystemCenterTone.Flat, EconomyMarketFormat.ToneForFactor(float.NaN, 1f));
            Assert.AreEqual(SystemCenterTone.Flat, EconomyMarketFormat.ToneForFactor(1f, float.NaN));
        }

        // ── IsFinite ────────────────────────────────────────────────────────────

        [Test]
        public void IsFinite_RejectsNaNAndInfinity()
        {
            Assert.IsTrue(EconomyMarketFormat.IsFinite(0f));
            Assert.IsTrue(EconomyMarketFormat.IsFinite(-123.456f));
            Assert.IsFalse(EconomyMarketFormat.IsFinite(float.NaN));
            Assert.IsFalse(EconomyMarketFormat.IsFinite(float.PositiveInfinity));
            Assert.IsFalse(EconomyMarketFormat.IsFinite(float.NegativeInfinity));
        }

        // ── 回归：分界点上的精度切换不得产生跳变 ─────────────────────────────

        [Test]
        public void FormatRate_RoundingBoundaries_AreStable()
        {
            // 取整语义下的边界：小数部分**不得泄漏**（三级精度阶梯已按用户裁决移除）
            Assert.AreEqual("10", EconomyMarketFormat.FormatRate(9.99f));
            Assert.AreEqual("10", EconomyMarketFormat.FormatRate(10f));
            Assert.AreEqual("1,000", EconomyMarketFormat.FormatRate(999.9f));
            Assert.AreEqual("1,000", EconomyMarketFormat.FormatRate(1000f));
            // 半值**远离零**（12.5→13 / -12.5→-13）：默认银行家舍入会给出 12，读数上像"少了一件"
            Assert.AreEqual("13", EconomyMarketFormat.FormatRate(12.5f));
            Assert.AreEqual("-13", EconomyMarketFormat.FormatRate(-12.5f));
        }

        [Test]
        public void FormatSigned_SubHalf_IsFlatZero()
        {
            // 取整后 |值| < 0.5 即视为持平（±0）—— 否则会出现"显示 ±0、实际 0.4"的错位
            Assert.AreEqual("±0", EconomyMarketFormat.FormatSigned(0.4f));
            Assert.AreEqual("±0", EconomyMarketFormat.FormatSigned(-0.4f));
            Assert.AreEqual("+1", EconomyMarketFormat.FormatSigned(0.6f));
        }
    }

    /// <summary>
    /// **RUIF 列宽契约回归**（列宽比例归一化桥）。
    ///
    /// <para>⚠ 2026-09-20（CHG-LCE-001）：原 `EconomyMarketColumnShareTests`（市场面板 1:1 列宽，
    /// 引用 <c>EconomyMarketPanel.Shares</c>）随 MVS 市场体系整体删除；本夹具保留**仍在用**的
    /// <c>RuifBridge.NormalizeShares</c> 契约测试（调用点 = 人口中心三张表等），
    /// 改以自带样本数组为输入（不再引用已删除的面板常量）。</para>
    /// </summary>
    [TestFixture]
    public sealed class RuifBridgeColumnShareTests
    {
        /// <summary>综合比例样本（品种 : 四指标量级，Σ≠1 —— 正是归一化要处理的形态）。</summary>
        private static readonly float[] SampleShares = { 4f, 1f, 1f, 1f, 1.001f };

        /// <summary>
        /// **RUIF 列宽契约回归**（2026-09-18 实机根因）：<c>TableFmt.ColumnSplit</c> 是
        /// <c>widths[i] = shares[i] * rect.width</c> —— **直接乘、不除 Σ**。
        /// 传 <c>Σ ≠ 1</c> 的比例时首列宽度会超过表宽、把整行吃光，其余列落到 <c>rect</c> 之外
        /// **整体不可见**（实机表现：市场表只剩品种列、四个数值列一个墨迹都没有）。
        /// 修法 = <c>RuifBridge.NormalizeShares</c> 收口；本测试把该契约钉死，
        /// 避免以后有人"为了好看"把 shares 换成任意比例又踩回去。
        /// </summary>
        [Test]
        public void RuifBridge_NormalizesShares_ToUnitSum_PreservingRatio()
        {
            float[] normalized = (float[])PersonalChronicle.Archive.UI.RuifBridge
                .NormalizeShares(SampleShares).Clone();
            Assert.AreEqual(SampleShares.Length, normalized.Length, "归一化不得改变列数");

            float sum = 0f;
            for (int i = 0; i < normalized.Length; i++)
            {
                sum += normalized[i];
            }
            Assert.AreEqual(1f, sum, 0.0001f, "归一化后 Σ 必须为 1（RUIF 按占比直接相乘）");

            // 断言「归一化**保持原位比例**」，而不是「等于某个具体占比」——
            // 前者才是 NormalizeShares 的契约（且与输入取值无关）。
            float total = 0f;
            for (int i = 0; i < SampleShares.Length; i++)
            {
                total += SampleShares[i];
            }
            for (int i = 0; i < normalized.Length; i++)
            {
                Assert.AreEqual(SampleShares[i] / total, normalized[i], 0.0005f,
                    "归一化后第 " + i + " 列的占比偏离输入比例 —— 归一化改变了列宽比例");
            }
        }

        /// <summary>
        /// **向后兼容**：已归一化的调用点（人口中心三张表，Σ = 1.00）结果必须**逐列不变**
        /// —— 桥层的归一化对它们是恒等运算（除以 1）。
        /// </summary>
        [Test]
        public void RuifBridge_NormalizesShares_IsIdentityForUnitSum()
        {
            // 取自 MasterControlTabWindow.Population.CoreHonorTableShares（名称·数量·占比）
            float[] alreadyUnit = { 0.48f, 0.22f, 0.30f };
            float[] normalized = (float[])PersonalChronicle.Archive.UI.RuifBridge
                .NormalizeShares(alreadyUnit).Clone();
            for (int i = 0; i < alreadyUnit.Length; i++)
            {
                Assert.AreEqual(alreadyUnit[i], normalized[i], 0.0005f,
                    "Σ=1 的入参必须原样返回（否则人口中心三张表会莫名变宽/变窄）");
            }
        }

        [Test]
        public void SharesRatio_IsPreservedByNormalization_NoDisplayCompression()
        {
            // 「显示压缩判据」的原意 = **不允许任何中间环节悄悄改变列宽比例**
            // （2026-09-18 的实机缺陷正是 shares 被当占比直接乘、首列吃光整行、其余列落到表外）。
            // 它守的是：归一化前后「首列 : 次列」的比值不变。
            float sampledRatio = SampleShares[0] / SampleShares[1];
            float[] normalized = (float[])PersonalChronicle.Archive.UI.RuifBridge
                .NormalizeShares(SampleShares).Clone();
            float normalizedRatio = normalized[0] / normalized[1];
            Assert.AreEqual(sampledRatio, normalizedRatio, 0.02f,
                "归一化改变了「首列 : 次列」的比例 —— 违反显示压缩判据");
        }
    }
}
