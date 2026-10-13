using RimWorld;

namespace PersonalChronicle.Domain.Workshop
{
    /// <summary>
    /// 车间产出效果判定（纯函数，可离线单测）。
    ///
    /// <para><b>为什么单独存在（2026-09-21 审计修复 · CHG-SHOP-AUDIT-001 · A-2）</b>：
    /// 这些判定原散落在 <c>Capture/Patch_GenRecipe</c> 的 Postfix 内（与 <c>Rand</c>、
    /// <c>Thing</c>、<c>CompQuality</c> 交织），无法离线验证。现把"要不要触发/提升到哪一档"
    /// 抽成纯函数：调用方（<c>Application/Workshop/WorkshopProductionEffects</c>）只负责
    /// 掷骰（<c>Rand.Value</c>）与引擎交互（生成物品 / 设定品质）。</para>
    ///
    /// <para>阈值与行为逐位对齐原 Patch 实现（等价性见 CHG-SHOP-AUDIT-001 变更标注）。</para>
    /// </summary>
    public static class WorkshopProductionRules
    {
        /// <summary>因子"明显大于 1"阈值（与全工程其他加成判定一致）。</summary>
        private const float FactorAboveOne = 1.0001f;

        /// <summary>耗材因子"明显小于 1"阈值（原 Patch 实现为 0.999f）。</summary>
        private const float CostBelowOne = 0.999f;

        /// <summary>是否需要登记成品加成（耐久/精度任一有加成）。</summary>
        public static bool ShouldRegisterProductBonus(float maxHpFactor, float accuracyFactor)
        {
            return maxHpFactor > FactorAboveOne || accuracyFactor > FactorAboveOne;
        }

        /// <summary>
        /// 额外产出判定：产出因子 &gt; 1 时按 (factor − 1) 概率触发一次同规格额外产出。
        /// <paramref name="roll01"/> = 调用方掷出的 [0,1) 随机数。
        /// </summary>
        public static bool ShouldSpawnExtraProduct(float yieldFactor, float roll01)
        {
            return yieldFactor > FactorAboveOne && roll01 < yieldFactor - 1f;
        }

        /// <summary>
        /// 主料返还判定：耗材因子 &lt; 1 时按 (1 − factor) 概率返还一份主料。
        /// <paramref name="roll01"/> = 调用方掷出的 [0,1) 随机数。
        /// </summary>
        public static bool ShouldRefundIngredient(float stuffCostFactor, float roll01)
        {
            return stuffCostFactor > 0f && stuffCostFactor < CostBelowOne
                && roll01 < 1f - stuffCostFactor;
        }

        /// <summary>
        /// 劣质回炉目标档：成品为 Awful/Poor 时按 (goodRateFactor − 1) 概率提升到 Normal。
        /// 返回 -1 = 不提升；返回 ≥0 = 目标品质索引（QualityCategory 值）。
        /// <paramref name="roll01"/> = 调用方掷出的 [0,1) 随机数。
        /// </summary>
        public static int SalvageQualityTargetIfTriggered(int currentQualityIndex, float goodRateFactor, float roll01)
        {
            if (goodRateFactor <= FactorAboveOne)
            {
                return -1;
            }
            // 只拦劣质尾（Awful/Poor）；Normal 及以上不动。
            if (currentQualityIndex > (int)QualityCategory.Poor)
            {
                return -1;
            }
            if (roll01 >= goodRateFactor - 1f)
            {
                return -1;
            }
            return (int)QualityCategory.Normal;
        }
    }
}
