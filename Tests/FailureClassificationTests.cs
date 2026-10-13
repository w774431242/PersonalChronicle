using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Learning;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// M9 §4 七类失败分类与处置。每类断言的是**与其他类的差异** ——
    /// 若把所有失败混成一个布尔，这些差异全部消失，而症状（证据不足被当成不可靠、
    /// 对账问题被重试、制度闭锁被无限重试）在实机上极难归因。
    /// </summary>
    [TestFixture]
    public sealed class FailureClassificationTests
    {
        [Test] public void OnlyActualDefaultCountsAsAServiceFailure()
        {
            Assert.That(FailureClassRules.IsServiceFailure("ActualDefault"), Is.True);
            Assert.That(FailureClassRules.IsServiceFailure("RepeatedCancellation"), Is.True);
            // §6 验收："供应方未知/截止未知 ⇒ 显示证据不足，不记失败或满信誉"。
            Assert.That(FailureClassRules.IsServiceFailure("NoPublishedPrice"), Is.False);
            // §4："ProtectedAction/OutOfScope ⇒ 不计服务失败"。
            Assert.That(FailureClassRules.IsServiceFailure("NativeControlOrSafety"), Is.False);
            // §6 验收："征召/紧急保护 ⇒ 不计供应方失败"。
            Assert.That(FailureClassRules.IsServiceFailure("NativeFoodFirst"), Is.False);
        }

        [Test] public void UnknownReasonsAreNeverPunished()
        {
            // 宁少记、不错记：未知原因归 GetterError，既不退避也不记服务失败。
            var disposition = FailureClassRules.DispositionOf(FailureClassRules.Classify("SomeThirdPartyCode"));
            Assert.That(disposition.Backoff, Is.False);
            Assert.That(disposition.CountsAsServiceFailure, Is.False);
            Assert.That(FailureClassRules.Classify(null), Is.EqualTo(FailureClass.GetterError));
        }

        [Test] public void QualificationFailuresAreStructuralAndShortagesAreNot()
        {
            // 制度闭锁换时机无用 ⇒ 结构性受阻；缺钱缺货只是等事件，不是结构问题。
            Assert.That(FailureClassRules.DispositionOf(FailureClassRules.Classify("EmploymentNotPermitted")).Structural, Is.True);
            Assert.That(FailureClassRules.DispositionOf(FailureClassRules.Classify("InsufficientFunds")).Structural, Is.False);
            Assert.That(FailureClassRules.DispositionOf(FailureClassRules.Classify("InsufficientFunds")).AwaitsEvent, Is.True,
                "缺钱要等工资/补货等相关事件，而不是只死等冷却到点");
        }

        [Test] public void ReconciliationFailuresAreRoutedToTheirOwningModules()
        {
            var settlement = FailureClassRules.DispositionOf(FailureClassRules.Classify("PaymentConflict"));
            Assert.That(settlement.RouteToReconciliation, Is.True,
                "§4：交 M7/M11 对账，不重放付款 —— 本模块不得自行重试");
            var handOver = FailureClassRules.DispositionOf(FailureClassRules.Classify("HandOverFailed"));
            Assert.That(handOver.RouteToReconciliation, Is.True, "交接失败是交付不匹配，不是供应方违约");
        }

        [Test] public void ProtectedActionsDoNotEnterTheBackoffLedger()
        {
            foreach (string reason in new[] { "NativeControlOrSafety", "OutOfScope", "ProtectedTimetable", "OutsideHours" })
                Assert.That(FailureClassRules.IsBackoff(reason), Is.False,
                    "让路不是失败，进退避账会让玩家在紧急保护后看到无意义的冷却：" + reason);
        }

        [Test] public void RealShortagesAndDefaultsDoEnterTheBackoffLedger()
        {
            Assert.That(FailureClassRules.IsBackoff("NoFoodInStock"), Is.True);
            Assert.That(FailureClassRules.IsBackoff("InsufficientFunds"), Is.True);
            Assert.That(FailureClassRules.IsBackoff("ActualDefault"), Is.True);
            Assert.That(FailureClassRules.IsBackoff("StaleRevision"), Is.True);
        }

        [Test] public void EveryClassNameIsReachableSoTheTableHasNoDeadRow()
        {
            // 防"表里有类但没有任何原因码能落进去" —— 那等于该类的处置永远不会生效。
            var reached = new System.Collections.Generic.HashSet<FailureClass>();
            foreach (string reason in new[]
            {
                "NativeControlOrSafety", "StaleRevision", "InsufficientFunds", "EmploymentNotPermitted",
                "PaymentConflict", "ActualDefault", "NoPublishedPrice"
            })
                reached.Add(FailureClassRules.Classify(reason));
            Assert.That(reached.Count, Is.EqualTo(7), "七类都必须至少有一个原因码可落入");
        }
    }
}
