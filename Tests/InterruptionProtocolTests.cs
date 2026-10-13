using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Learning;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// M9 §5 收口：中断定格 × §4 分类的**合成判决**。
    /// 断言三条边界：已结算不算失败；让路/适配零记录；票据失效恰好一条。
    /// </summary>
    [TestFixture]
    public sealed class InterruptionProtocolTests
    {
        [Test] public void SettledInterruptionNeverRecordsEvenWithAnAngryReason()
        {
            var v = InterruptionProtocolRules.Resolve(true, true, true, "ActualDefault");
            Assert.That(v.Stage, Is.EqualTo(InterruptionStage.Settled));
            Assert.That(v.RecordFailure, Is.False, "钱物两清的中断不是任何一方的违约");
            Assert.That(v.CountsAsServiceFailure, Is.False);
        }

        [Test] public void YieldGetsZeroRecordsDespiteIncompleteProgress()
        {
            var v = InterruptionProtocolRules.Resolve(false, false, false, FailureClassRules.ReasonProtectedTimetable);
            Assert.That(v.Stage, Is.EqualTo(InterruptionStage.NotExecuted));
            Assert.That(v.RecordFailure, Is.False, "§4：让路不计服务失败、不进退避账");
        }

        [Test] public void LostReservationRecordsExactlyOncePerEvaluation()
        {
            var v = InterruptionProtocolRules.Resolve(false, false, false, FailureClassRules.ReasonReservationLost);
            Assert.That(v.RecordFailure, Is.True);
            Assert.That(v.CountsAsServiceFailure, Is.False, "版本失效不是供应方违约");
            Assert.That(v.RouteToReconciliation, Is.False);
        }

        [Test] public void UnknownReasonStaysSilent()
        {
            var v = InterruptionProtocolRules.Resolve(false, false, false, "TotallyUnknownCode");
            Assert.That(v.RecordFailure, Is.False, "未知码归 GetterError：宁可少记，不要错记");
        }

        [Test] public void DeliveredButUnsettledRoutesToReconciliation()
        {
            var v = InterruptionProtocolRules.Resolve(true, false, false, "TransactionUnknown");
            Assert.That(v.Stage, Is.EqualTo(InterruptionStage.Delivered));
            Assert.That(v.RecordFailure, Is.True);
            Assert.That(v.RouteToReconciliation, Is.True, "钱物归属存疑：交对账，不重放付款");
        }

        [Test] public void ConsumedStageIsFurthestBeforeSettled()
        {
            var v = InterruptionProtocolRules.Resolve(true, true, false, "TransactionUnknown");
            Assert.That(v.Stage, Is.EqualTo(InterruptionStage.Consumed));
            Assert.That(v.RecordFailure, Is.True, "已交付已使用但未结算：仍有差异要记");
        }

        [Test] public void ReasonConstantsMatchTheAuthoritativeTable()
        {
            Assert.That(FailureClassRules.Classify(FailureClassRules.ReasonReservationLost), Is.EqualTo(FailureClass.StaleVersion));
            Assert.That(FailureClassRules.Classify(FailureClassRules.ReasonProtectedTimetable), Is.EqualTo(FailureClass.ProtectedAction));
        }

        [Test] public void StockShortageStillBacksOffOnce()
        {
            var v = InterruptionProtocolRules.Resolve(false, false, false, "NoFoodInStock");
            Assert.That(v.RecordFailure, Is.True, "缺货进慢速退避（等补货事件），但不计服务失败");
            Assert.That(v.CountsAsServiceFailure, Is.False);
        }
    }
}
