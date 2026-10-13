using System;
using PersonalChronicle.Application.ReadModels;

namespace PersonalChronicle.Economy
{
    /// <summary>
    /// 经济中心 · **市场面板的纯格式 / 归一化规则**（无引擎依赖、可离线单测）。
    ///
    /// <para><b>为什么单独成类</b>：这些规则此前是 <c>BuiltinEconomyProvider.MarketPanel</c> 的
    /// <c>private static</c> 方法，**测不到** —— 而它们恰恰是"面板读数对不对"的全部判据：
    /// 只要百分比四舍五入、速率精度或归一化区间漂移，屏幕上就会显示一个**看着合理但错误**的数字，
    /// 比崩溃更难发现。抽出来是为了让它们进测试网。</para>
    ///
    /// <para><b>三条被钉死的语义</b>：</para>
    /// <list type="number">
    /// <item><b>未接线 ≠ 中性</b>：读不到因子时必须显示占位符，**不得显示 <c>100%</c>**
    /// —— 那会让人误以为读到了中性值，而实际是没读到（<see cref="Dash"/>）。</item>
    /// <item><b>归一化必须线性</b>：显示压缩判据 —— 任何让 2:1 在显示上变 1:1 的压缩
    /// 都不可接受（log 刻度、百分比下限都因此被否决）。本映射是恒等仿射，比例严格保持。</item>
    /// <item><b>涨红跌绿</b>：中式约定（<see cref="SystemCenterTone.Bad"/> = 红 = 涨价）。
    /// 中性带 ±1% 避免噪声抖动被画成涨跌。</item>
    /// </list>
    /// </summary>
    internal static class EconomyMarketFormat
    {
        /// <summary>未接线读数的统一占位符（LAND-009：验证不了的读数不臆造）。</summary>
        internal const string Dash = "—";

        /// <summary>中性带宽度（±1%）。因子落在 <c>[0.99, 1.01]</c> 内视为持平。</summary>
        internal const float NeutralBand = 0.01f;

        /// <summary>
        /// 展示口径的一个「月」= **30 天 = 2 个季度**（与 <c>ItemFlowLedger</c> 的 2 季窗口
        /// 精确对齐）—— 2026-09-20 用户裁决：「统一产能速率改为月或者季，取整」。
        ///
        /// <para>选「月」而不是「季」的理由：账本窗口本身就是 2 季 = 30 天 ⇒ "月"无需再折算一次。</para>
        /// </summary>
        internal const float DaysPerMonth = 30f;

        /// <summary>件/日 ⇒ 件/月（口径换算，**单一实现体** —— 各调用点各乘一次必然漂移）。</summary>
        internal static float PerDayToPerMonth(float perDay)
        {
            return perDay * DaysPerMonth;
        }

        /// <summary>
        /// 数量 / 速率格式化：**取整 + 千分位**（2026-09-20 用户裁决「取整」）。
        ///
        /// <para>⚠ **三级精度阶梯（≥10 一位小数 / &lt;10 两位小数）已被用户否决** ——
        /// 它会让同一列出现"150 / 51.8 / 9.99"三种粒度。大数的可读性改由
        /// <see cref="FormatQuantity"/> 的**单位升格**承担，不再靠小数表达精度。</para>
        ///
        /// <para>取整用**远离零**舍入（<c>AwayFromZero</c>）：.NET 默认的银行家舍入会让
        /// <c>12.5 → 12</c>，读数上像"凭空少了一件"，经济面板不该有这种争议。</para>
        ///
        /// <para>非有限值（NaN / ±∞）⇒ <see cref="Dash"/>（不显示 <c>NaN</c>）。</para>
        /// </summary>
        internal static string FormatRate(float value)
        {
            if (!IsFinite(value))
            {
                return Dash;
            }
            return Math.Round(value, MidpointRounding.AwayFromZero)
                .ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 数量格式化 + **大数单位升格**（用户裁决：「可以添加重量单位自动更换更高的重量单位」）。
        ///
        /// <para>后缀与除数**由语言包给出**（中「万」/10⁴、英「k」/10³）—— 中文习惯"万"、
        /// 英文习惯"k"，同一个阈值与除数**不通用**，故绝不能硬编码在代码里。
        /// 两者任一缺失（<paramref name="scaleSuffix"/> 空 / <paramref name="scaleDivisor"/> ≤ 1）
        /// ⇒ **不升格**，退回千分位整数（降级可解释，不是静默改口径）。</para>
        ///
        /// <para>升格后保留**一位小数**（<c>1.2万</c> / <c>12.3k</c>）：此时那一位承载的是
        /// 千位量级信息，与"取整"的可读性目标不冲突（用户要的是"别显示 51.8 这种噪声"）。</para>
        /// </summary>
        internal static string FormatQuantity(float value, string scaleSuffix, float scaleDivisor)
        {
            if (!IsFinite(value))
            {
                return Dash;
            }
            if (scaleDivisor > 1f && !string.IsNullOrEmpty(scaleSuffix)
                && Math.Abs(value) >= scaleDivisor)
            {
                return (value / scaleDivisor).ToString("0.#",
                    System.Globalization.CultureInfo.InvariantCulture) + scaleSuffix;
            }
            return FormatRate(value);
        }

        /// <summary>因子 → 百分比文本（如 <c>118%</c>）。非有限 ⇒ <see cref="Dash"/>。</summary>
        internal static string FormatPercent(float factor)
        {
            if (!IsFinite(factor))
            {
                return Dash;
            }
            return (factor * 100f).ToString("0") + "%";
        }

        /// <summary>
        /// 带符号的差额文本（预览 <c>.dt</c> 的净值单元格口径）。
        ///
        /// <para><b>符号约定（与预览图例一致）</b>：图例原文是「差额 −净进口 · +净出口」
        /// ⇒ 传进来的应当是 <c>供应 − 需求</c>（即"净出口为正"）。
        /// 预览 JS 用 <c>−(需求−供应)</c> 表达同一件事，此处统一为**调用方先算好方向**。</para>
        ///
        /// <para>持平用 <c>±0</c>（预览 <c>netSign = '±'</c>）；负号用 U+2212（与预览同字符）。</para>
        /// </summary>
        internal static string FormatSigned(float value)
        {
            if (!IsFinite(value))
            {
                return Dash;
            }
            if (Math.Abs(value) < 0.5f)
            {
                return "±0"; // 取整后即为 0 ⇒ "持平"阈值与取整语义同步（否则 0.4 会显示成非零）
            }
            return (value > 0f ? "+" : "\u2212") + FormatRate(Math.Abs(value));
        }

        /// <summary>带符号的数量（净值列）：升格规则同 <see cref="FormatQuantity"/>。</summary>
        internal static string FormatQuantitySigned(float value, string scaleSuffix, float scaleDivisor)
        {
            if (!IsFinite(value))
            {
                return Dash;
            }
            if (Math.Abs(value) < 0.5f)
            {
                return "±0";
            }
            return (value > 0f ? "+" : "\u2212")
                + FormatQuantity(Math.Abs(value), scaleSuffix, scaleDivisor);
        }

        /// <summary>相对价指数文本（消费/生产，基线 <c>1.00</c>，如 <c>1.60</c>）。</summary>
        internal static string FormatIndex(float factor)
        {
            return IsFinite(factor) ? factor.ToString("0.00") : Dash;
        }

        /// <summary>
        /// 涨幅文本（预览 <c>.chg</c>：<c>▲+60%</c> / <c>▼-40%</c> / 中性带内 <c>±0%</c>）。
        /// 相对 <paramref name="neutralFactor"/> 计算；非有限 ⇒ 空串（调用方不画）。
        /// </summary>
        internal static string FormatChange(float factor, float neutralFactor)
        {
            if (!IsFinite(factor) || !IsFinite(neutralFactor) || neutralFactor <= 0f)
            {
                return string.Empty;
            }
            float change = (factor / neutralFactor - 1f) * 100f;
            if (Math.Abs(change) < NeutralBand * 100f)
            {
                return "±0%";
            }
            return (change > 0f ? "\u25B2+" : "\u25BC") + change.ToString("0") + "%";
        }

        /// <summary>
        /// 差额方向 → 色调（中式约定：**净进口红 / 净出口绿 / 持平中性**）。
        /// 入参口径同 <see cref="FormatSigned"/>（<c>供应 − 需求</c>）。
        /// </summary>
        internal static SystemCenterTone ToneForBalance(float supplyMinusDemand)
        {
            if (!IsFinite(supplyMinusDemand) || Math.Abs(supplyMinusDemand) < 1e-9f)
            {
                return SystemCenterTone.Flat;
            }
            return supplyMinusDemand > 0f ? SystemCenterTone.Good : SystemCenterTone.Bad;
        }

        /// <summary>数据级别（1/2/3）→ 圈码文本（<c>①</c> / <c>②</c> / <c>③</c>；其它 ⇒ 空）。</summary>
        internal static string LevelMark(int dataLevel)
        {
            switch (dataLevel)
            {
                case 1: return "\u2460";
                case 2: return "\u2461";
                case 3: return "\u2462";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// 因子 → 标尺占比 <c>0..1</c>，线性映射到 <c>[factorMin, factorMax]</c>，并 **clamp**。
        ///
        /// <para><b>线性而非 log</b>：见类注释的显示压缩判据。恒等仿射 ⇒ 比例严格保持。</para>
        /// <para>区间退化（<paramref name="factorMax"/> ≤ <paramref name="factorMin"/>）⇒ 返回
        /// <c>0.5</c>（中性）：宁可显示中性，也不做除零。</para>
        /// </summary>
        internal static float NormalizeFactor(float factor, float factorMin, float factorMax)
        {
            float span = factorMax - factorMin;
            if (span <= 0f)
            {
                return 0.5f;
            }
            float t = (factor - factorMin) / span;
            if (t < 0f)
            {
                t = 0f;
            }
            if (t > 1f)
            {
                t = 1f;
            }
            return t;
        }

        /// <summary>
        /// 因子 → 色调。**中式约定：涨红跌绿**（用户区域规范）。
        ///
        /// <para>⚠ 映射方向容易写反：因子 &gt; 1（涨价）⇒ <see cref="SystemCenterTone.Bad"/>
        /// （红）；因子 &lt; 1（跌价）⇒ <see cref="SystemCenterTone.Good"/>（绿）。
        /// 这与欧美约定相反，是**有意为之**。</para>
        /// </summary>
        internal static SystemCenterTone ToneForFactor(float factor, float neutralFactor)
        {
            if (!IsFinite(factor) || !IsFinite(neutralFactor))
            {
                return SystemCenterTone.Flat;
            }
            if (factor > neutralFactor * (1f + NeutralBand))
            {
                return SystemCenterTone.Bad; // 红 = 涨价
            }
            if (factor < neutralFactor * (1f - NeutralBand))
            {
                return SystemCenterTone.Good; // 绿 = 跌价
            }
            return SystemCenterTone.Flat;
        }

        /// <summary>
        /// 有限性判定（<c>float.IsNaN</c> / <c>IsInfinity</c> 的本地封装）。
        /// 不用 <c>MathUtil</c>：那是引擎侧类型，本类必须保持**零引擎依赖**才能离线测。
        /// </summary>
        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
