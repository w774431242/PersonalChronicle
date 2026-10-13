using System;
using PersonalChronicle.Domain.PawnEconomy.Finance;

namespace PersonalChronicle.Domain.PawnEconomy.Employment
{
    /// <summary>
    /// CHG-20261012-ECON-CONTRACTIZE：「工作合同弹窗」的**起草规则**（纯函数，离线可断言）。
    ///
    /// <para><b>为什么必须纯函数</b>：数量 / 时段 / 薪资直接决定**资金承诺** —— Posted（已派发）合同
    /// 计入国库预留（见 <c>EmploymentSaveData.SumOutstandingCommitments</c>），推荐价又是"参考原版数据"的
    /// 唯一算术口径。与 <see cref="ItemValuation"/> 同一纪律：本类**不引用任何 RimWorld 类型**，
    /// 调用方读 <c>Thing.MarketValue</c> 后把数值传进来（推荐价采样与工作类型遍历在 Economy 层）。</para>
    ///
    /// <para><b>为什么"推荐价"要带 <c>fromMarket</c> 出参</b>：无产出物 / 市价未知时必须回落基准价，
    /// 而 UI 要**如实标注**这是"基准价"而非"市价"（诚实性：不得把默认值伪装成市价）。</para>
    /// </summary>
    internal static class WorkContractRules
    {
        /// <summary>起草期时长（3 游戏日 = 180000 tick）：无人承接的 Posted 合同在此之后可被裁剪
        /// （配套 <c>ReadExpiredContractIds</c> 的 Posted 扩展）。</summary>
        public const int DraftLifetimeTicks = 180000;
        /// <summary>承接后的合同期时长（3 游戏日）。</summary>
        public const int ContractLifetimeTicks = 180000;
        /// <summary>推荐单价的**劳动分成**：产出物市价 × 该系数（用户裁决「给一个推荐市价，玩家可自主拟定工薪」）。</summary>
        public const double RecommendedShareOfMarket = 0.5d;
        /// <summary>推荐价不可得（无产出 / 市价未知）时的**基准价**（5.00 银/件，minor 单位）——
        /// UI 必须标注"基准价"，不得伪装成市价。</summary>
        public const long RecommendedBaseMinor = 500L;
        public const int MinimumJobs = 1;
        public const int MaximumJobs = 100000;
        /// <summary>弹窗时段的默认初值（8–18；旧档合同的缺省是 0/0 = 全天，两者语义不同、各有出处）。</summary>
        public const int DefaultWindowStartHour = 8, DefaultWindowEndHour = 18;

        /// <summary>合同与授权读面共用时段规则；旧档 0/0 全天，跨夜支持，非法存档值拒绝授权。</summary>
        public static bool AllowsHour(int hour, int start, int end)
        {
            if (start < 0 || start > 24 || end < 0 || end > 24) return false;
            int h = ((hour % 24) + 24) % 24;
            return start == end || (start < end ? h >= start && h < end : h >= start || h < end);
        }

        /// <summary>推荐单件工资（minor）。<paramref name="outputMarketValueSilver"/> = 该工作**每件产出物市价之和**（银）；
        /// ≤0 / NaN / 无穷 ⇒ 回落 <see cref="RecommendedBaseMinor"/> 且 <paramref name="fromMarket"/> = false。</summary>
        public static long RecommendUnitWageMinor(double outputMarketValueSilver, out bool fromMarket)
        {
            fromMarket = false;
            if (double.IsNaN(outputMarketValueSilver) || double.IsInfinity(outputMarketValueSilver)
                || outputMarketValueSilver <= 0d) return RecommendedBaseMinor;
            long minor = ItemValuation.UnitPriceMinor((float)(outputMarketValueSilver * RecommendedShareOfMarket));
            if (minor <= 0L) return RecommendedBaseMinor;
            fromMarket = true;
            return minor;
        }

        /// <summary>数量钳制：<c>min(可派发, 上限)</c> 内取 <paramref name="jobs"/>；<paramref name="availableJobs"/> ≤ 0 ⇒ 0（不可起草）。</summary>
        public static int ClampJobs(int jobs, int availableJobs)
        {
            if (availableJobs <= 0) return 0;
            int cap = availableJobs < MaximumJobs ? availableJobs : MaximumJobs;
            if (jobs < MinimumJobs) return MinimumJobs;
            return jobs > cap ? cap : jobs;
        }

        /// <summary>时段步进（0–24 环绕；步进粒度 1 小时）。
        /// 0 与 24 都可表示"整点边界"，环绕时把 24 归一为 0，避免出现 24 与 0 并存的两个同义值。</summary>
        public static int StepHour(int hour, int direction)
        {
            int next = hour + (direction >= 0 ? 1 : -1);
            if (next < 0) next = 24;
            if (next > 24) next = 0;
            return next;
        }

        /// <summary>起草校验（弹窗「确定」前的唯一判据）。
        /// <paramref name="reason"/> 为**稳定 ID**（呈现层负责翻译，GOV-009），null = 通过。</summary>
        public static bool ValidateDraft(int jobs, long unitWageMinor, int startHour, int endHour,
            int availableJobs, long treasuryAvailableMinor, long outstandingMinor, out string reason)
        {
            if (availableJobs <= 0) { reason = "NoDemand"; return false; }
            int cap = availableJobs < MaximumJobs ? availableJobs : MaximumJobs;
            if (jobs < MinimumJobs || jobs > cap) { reason = "JobsOutOfRange"; return false; }
            if (unitWageMinor <= 0L || unitWageMinor > MinorCurrency.Maximum) { reason = "WageOutOfRange"; return false; }
            if (startHour < 0 || startHour > 24 || endHour < 0 || endHour > 24) { reason = "WindowOutOfRange"; return false; }
            long commit = SaturatedMultiply(unitWageMinor, jobs);
            long free = treasuryAvailableMinor - outstandingMinor;
            if (free < 0L) free = 0L;
            if (commit > free) { reason = "InsufficientBacking"; return false; }
            reason = null;
            return true;
        }

        /// <summary>饱和乘法（承诺额只用于比较：溢出时按上限处理，绝不让乘法回绕成负数）。</summary>
        public static long SaturatedMultiply(long amountMinor, int count)
        {
            if (amountMinor <= 0L || count <= 0) return 0L;
            if (amountMinor > long.MaxValue / count) return long.MaxValue;
            return amountMinor * count;
        }

        /// <summary>推荐价的**采样有界**契约：弹窗打开时最多读取这么多件产出物样本（PERF-001：演示/界面路径不得无界遍历）。</summary>
        public const int MaximumPriceSamples = 8;
    }
}
