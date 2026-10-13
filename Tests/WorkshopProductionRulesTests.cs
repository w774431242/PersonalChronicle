using NUnit.Framework;
using PersonalChronicle.Domain.Workshop;
using RimWorld;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 车间产出效果判定（<see cref="WorkshopProductionRules"/>）离线单测。
    ///
    /// <para>这些判定原内联在 <c>Capture/Patch_GenRecipe</c> 的 Postfix（不可离线验证），
    /// 2026-09-21（CHG-SHOP-AUDIT-001 · A-2）抽取为纯函数后补齐契约钉死：
    /// 概率语义（roll &lt; p 触发）、品质只拦 Awful/Poor、阈值方向与旧实现逐位一致。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopProductionRulesTests
    {
        // ---- 额外产出（yieldFactor：>1 = 按 factor−1 概率额外产出一件）----------------

        [Test]
        public void SpawnExtra_NeutralFactor_NeverTriggers()
        {
            Assert.IsFalse(WorkshopProductionRules.ShouldSpawnExtraProduct(1f, 0f));
            Assert.IsFalse(WorkshopProductionRules.ShouldSpawnExtraProduct(1f, 0.999f));
        }

        [Test]
        public void SpawnExtra_BelowOneFactor_NeverTriggers()
        {
            // 产出因子 <1（减产）不应触发"额外产出"（旧实现只在 >1 时掷骰）。
            Assert.IsFalse(WorkshopProductionRules.ShouldSpawnExtraProduct(0.8f, 0f));
        }

        [Test]
        public void SpawnExtra_ProbabilityEqualsFactorMinusOne()
        {
            float p = 1.2f - 1f;
            Assert.IsTrue(WorkshopProductionRules.ShouldSpawnExtraProduct(1.2f, p - 0.001f));
            Assert.IsFalse(WorkshopProductionRules.ShouldSpawnExtraProduct(1.2f, p));
            Assert.IsFalse(WorkshopProductionRules.ShouldSpawnExtraProduct(1.2f, p + 0.001f));
        }

        // ---- 主料返还（stuffCostFactor：<1 = 按 1−factor 概率返还一份主料）------------

        [Test]
        public void Refund_NeutralFactor_NeverTriggers()
        {
            Assert.IsFalse(WorkshopProductionRules.ShouldRefundIngredient(1f, 0f));
        }

        [Test]
        public void Refund_ZeroFactor_DoesNotDivideByWhole()
        {
            // 因子 0（异常数据）不应触发"必定返还"的极端语义（旧实现要求 >0）。
            Assert.IsFalse(WorkshopProductionRules.ShouldRefundIngredient(0f, 0f));
        }

        [Test]
        public void Refund_ProbabilityEqualsOneMinusFactor()
        {
            float p = 1f - 0.9f;
            Assert.IsTrue(WorkshopProductionRules.ShouldRefundIngredient(0.9f, p - 0.001f));
            Assert.IsFalse(WorkshopProductionRules.ShouldRefundIngredient(0.9f, p));
        }

        // ---- 劣质回炉（goodRateFactor：>1 = 按 factor−1 概率把 Awful/Poor 提到 Normal）----

        [Test]
        public void Salvage_OnlyLiftsAwfulAndPoor()
        {
            float roll = 0f; // 必定命中概率
            Assert.AreEqual((int)QualityCategory.Normal,
                WorkshopProductionRules.SalvageQualityTargetIfTriggered((int)QualityCategory.Awful, 1.25f, roll));
            Assert.AreEqual((int)QualityCategory.Normal,
                WorkshopProductionRules.SalvageQualityTargetIfTriggered((int)QualityCategory.Poor, 1.25f, roll));
            // Normal 及以上不动（-1 = 不提升）。
            Assert.AreEqual(-1,
                WorkshopProductionRules.SalvageQualityTargetIfTriggered((int)QualityCategory.Normal, 1.25f, roll));
            Assert.AreEqual(-1,
                WorkshopProductionRules.SalvageQualityTargetIfTriggered((int)QualityCategory.Legendary, 1.25f, roll));
        }

        [Test]
        public void Salvage_NeutralFactor_NeverLifts()
        {
            Assert.AreEqual(-1, WorkshopProductionRules.SalvageQualityTargetIfTriggered(
                (int)QualityCategory.Awful, 1f, 0f));
        }

        [Test]
        public void Salvage_ProbabilityEqualsFactorMinusOne()
        {
            float p = 1.25f - 1f;
            Assert.AreEqual((int)QualityCategory.Normal, WorkshopProductionRules.SalvageQualityTargetIfTriggered(
                (int)QualityCategory.Poor, 1.25f, p - 0.001f));
            Assert.AreEqual(-1, WorkshopProductionRules.SalvageQualityTargetIfTriggered(
                (int)QualityCategory.Poor, 1.25f, p));
        }

        // ---- 成品加成登记（耐久/精度任一有加成才登记）--------------------------------

        [Test]
        public void Register_RequiresAtLeastOneFactor()
        {
            Assert.IsFalse(WorkshopProductionRules.ShouldRegisterProductBonus(1f, 1f));
            Assert.IsTrue(WorkshopProductionRules.ShouldRegisterProductBonus(1.5f, 1f));
            Assert.IsTrue(WorkshopProductionRules.ShouldRegisterProductBonus(1f, 1.12f));
            Assert.IsTrue(WorkshopProductionRules.ShouldRegisterProductBonus(1.5f, 1.12f));
        }
    }
}
