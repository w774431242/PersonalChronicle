using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Care;

namespace PersonalChronicle.Tests
{
    /// <summary>U6-B1：七门必须由**具名事实**构造（旧实现的 settlement/interruptionRecovery/uiExplanation
    /// 是 const 与字面 true，永远为真 ⇒ 什么都证明不了）。本夹具钉住映射的**可证伪**边界：
    /// 没有原版过程的类型永不可提升；证据没接通就不得声称交付证据；账本不可读就不得声称结算。</summary>
    [TestFixture]
    public sealed class ServiceEvidenceTests
    {
        private static readonly ServiceKind[] AllKinds =
        { ServiceKind.Examination, ServiceKind.Bed, ServiceKind.Treatment, ServiceKind.Childcare, ServiceKind.Education };

        [Test] public void NoVanillaProcessMeansTheGatesCanNeverBeClaimed()
        {
            var readiness = ServiceReadiness.FromFacts(ServiceKind.Examination, ServiceEvidenceStatus.NotDeliverable,
                executorCapable: true, pricePublished: true, ledgerReadable: true);
            Assert.That(readiness.VanillaAccess, Is.False, "没有原版过程 ⇒ 访问门不成立");
            Assert.That(readiness.DeliveryEvidence, Is.False, "没有交付证据面 ⇒ 交付门不成立");
            Assert.That(readiness.Missing(), Does.Contain("VanillaAccess"));
            Assert.That(readiness.Missing(), Does.Contain("DeliveryEvidence"));
            // 反向对照：同一组其它事实上，只把证据面换成 CaptureWired，缺失列表必须真的变化
            var wired = ServiceReadiness.FromFacts(ServiceKind.Treatment, ServiceEvidenceStatus.CaptureWired,
                executorCapable: true, pricePublished: true, ledgerReadable: true);
            Assert.That(wired.Missing(), Is.Empty, "过程已接通 + 执行者有能力 + 已发布价格 + 账本可读 ⇒ 七门全过");
        }

        [Test] public void AProcessThatExistsButIsNotCapturedIsNotDeliveryEvidence()
        {
            var readiness = ServiceReadiness.FromFacts(ServiceKind.Childcare, ServiceEvidenceStatus.ProcessExistsCaptureNotWired,
                executorCapable: true, pricePublished: true, ledgerReadable: true);
            Assert.That(readiness.VanillaAccess, Is.True, "过程存在 ⇒ 访问门成立（将来可证）");
            Assert.That(readiness.DeliveryEvidence, Is.False, "摄取未接 ⇒ **不得**声称交付证据");
            Assert.That(readiness.Missing(), Does.Contain("DeliveryEvidence"));
        }

        [Test] public void AnUnreadableLedgerBlocksSettlementAndRecoveryForEveryKind()
        {
            foreach (var kind in AllKinds)
            {
                var readiness = ServiceReadiness.FromFacts(kind, ServiceEvidenceStatus.CaptureWired,
                    executorCapable: true, pricePublished: true, ledgerReadable: false);
                Assert.That(readiness.Settlement, Is.False, kind.ToString());
                Assert.That(readiness.InterruptionRecovery, Is.False, kind.ToString());
                Assert.That(readiness.Missing(), Does.Contain("Settlement"));
                Assert.That(readiness.Missing(), Does.Contain("InterruptionRecovery"));
            }
        }

        [Test] public void AnIncapableExecutorOrAnUnpublishedPriceBlocksTheGates()
        {
            var noExecutor = ServiceReadiness.FromFacts(ServiceKind.Treatment, ServiceEvidenceStatus.CaptureWired,
                executorCapable: false, pricePublished: true, ledgerReadable: true);
            Assert.That(noExecutor.CapacityAndHours, Is.False, "没有可执行的医生 ⇒ 容量门不成立（不是'有个人'就算）");
            var noPrice = ServiceReadiness.FromFacts(ServiceKind.Treatment, ServiceEvidenceStatus.CaptureWired,
                executorCapable: true, pricePublished: false, ledgerReadable: true);
            Assert.That(noPrice.PriceCap, Is.False, "未发布价格 ⇒ 价格门不成立");
        }

        [Test] public void EveryPublishedKindHasAnExplanationSurface()
        {
            foreach (var kind in AllKinds)
                Assert.That(ServiceEvidenceRules.HasExplanationSurface(kind), Is.True, kind.ToString());
            Assert.That(ServiceEvidenceRules.HasExplanationSurface((ServiceKind)99), Is.False,
                "未发布的类型不得主张解释面（否则新增类型会静默继承一个从未渲染的行）");
        }
    }
}
