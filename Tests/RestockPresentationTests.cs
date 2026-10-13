using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Production;

namespace PersonalChronicle.Tests
{
    /// <summary>U6-A3：补货评估的展示投影必须有牙齿 —— 只有真实 `Shortfall` 才显示数值；未知输入
    /// **绝不**显示 0（会被读成"量到零"）；展示有界且确定性；原因码是稳定 ID。</summary>
    [TestFixture]
    public sealed class RestockPresentationTests
    {
        private static RestockEvaluation Shortfall(string key, long amount)
            => RestockEvaluation.ShortfallFor(key, amount);
        private static RestockEvaluation Unknown(string key, string reason)
            => RestockEvaluation.Unknown(key, reason);
        private static RestockEvaluation Covered(string key) => RestockEvaluation.Covered(key);

        [Test] public void OnlyARealShortfallProducesANumber()
        {
            RestockPresentation.Describe(Shortfall("Steel", 200), out bool hasNumber, out long amount, out string reason);
            Assert.That(hasNumber, Is.True);
            Assert.That(amount, Is.EqualTo(200));
            Assert.That(reason, Is.EqualTo(RestockReasons.Shortfall));

            RestockPresentation.Describe(Unknown("Steel", "DemandDomainNotWired"), out hasNumber, out amount, out reason);
            Assert.That(hasNumber, Is.False, "未知输入不得显示数值");
            Assert.That(amount, Is.Zero);
            Assert.That(reason, Is.EqualTo("DemandDomainNotWired"), "原因码按稳定 ID 原样传出，由表现层映射");
        }

        [Test] public void UnknownAndInconsistentRowsAreNeverRenderedAsZero()
        {
            foreach (var evaluation in new[]
            {
                Unknown("Steel", "DemandDomainNotWired"),
                Unknown("Steel", "ReadoutUnavailable"),
                RestockEvaluation.Inconsistent("Steel", RestockReasons.Overflow)
            })
            {
                RestockPresentation.Describe(evaluation, out bool hasNumber, out long amount, out _);
                Assert.That(hasNumber, Is.False, evaluation.ReasonCode);
                Assert.That(amount, Is.Zero, "数值位必须保持 0 且**不显示**，绝不让 0 冒充读数");
            }
            // 反向对照：同一行改成真有缺口，可显示性必须翻转（证明上面不是恒真断言）
            RestockPresentation.Describe(Shortfall("Steel", 1), out bool shortfallHasNumber, out _, out _);
            Assert.That(shortfallHasNumber, Is.True);
        }

        [Test] public void SelectionIsBoundedDeterministicAndReportsTheTotal()
        {
            var all = new List<RestockEvaluation>();
            for (int i = 0; i < 32; i++) all.Add(Unknown("Cat" + i.ToString("D2"), "DemandDomainNotWired"));
            var shown = RestockPresentation.Select(all, RestockPresentation.MaximumRowsShown, out int total);
            Assert.That(total, Is.EqualTo(32), "总数必须如实报出，供 UI 显示 N/总 M");
            Assert.That(shown.Count, Is.EqualTo(RestockPresentation.MaximumRowsShown), "展示有界");
            Assert.That(shown[0].CategoryKey, Is.EqualTo("Cat00"), "Ordinal 序取前 N");
            Assert.That(shown[shown.Count - 1].CategoryKey, Is.EqualTo("Cat03"));

            // 反向对照：输入顺序颠倒后结果必须完全相同（确定性，不得依赖集合顺序）
            all.Reverse();
            var again = RestockPresentation.Select(all, RestockPresentation.MaximumRowsShown, out int totalAgain);
            Assert.That(totalAgain, Is.EqualTo(32));
            for (int i = 0; i < shown.Count; i++) Assert.That(again[i].CategoryKey, Is.EqualTo(shown[i].CategoryKey));
        }

        [Test] public void EmptyOrNonPositiveSelectionReturnsNothingRatherThanAFabricatedRow()
        {
            Assert.That(RestockPresentation.Select(null, 4, out int total0), Is.Empty);
            Assert.That(total0, Is.Zero);
            Assert.That(RestockPresentation.Select(new List<RestockEvaluation>(), 4, out _), Is.Empty);
            var rows = new List<RestockEvaluation> { Covered("Steel") };
            Assert.That(RestockPresentation.Select(rows, 0, out int total1), Is.Empty);
            Assert.That(total1, Is.EqualTo(1), "上限为 0 时不展示，但总数仍如实报出");
        }
    }
}
