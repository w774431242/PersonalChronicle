using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Care;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// M8「家庭与照护协作」的四角色纪律：**防止同一受益人的同一项需求被重复授权**，
    /// 以及受益者不得兼任执行者。每条断言都对应"什么情况下会失败"：
    /// 防重漏掉 / 到期授权仍占名额 / 不同服务类型互相阻塞 / 未指派执行者被误拒 ⇒ 必挂。
    /// </summary>
    [TestFixture]
    public sealed class CareCoordinationTests
    {
        private static CareAuthorization Grant(ServiceKind kind = ServiceKind.Childcare, string beneficiary = "child:1",
            string executor = "adult:1", int created = 100, int expires = 60000)
            => new CareAuthorization("care:1", kind, beneficiary, "guardian:1", executor, "payer:1",
                5000, created, expires);

        // ── 防止儿童需求重复（签发侧防重）─────────────────────────────────────────────

        [Test] public void FirstGrantOfAKindIsAlwaysIssued()
            => Assert.That(CareRules.CanIssue(null, "child:1", ServiceKind.Childcare, 1000), Is.EqualTo(CareCode.Applied));

        [Test] public void SecondActiveGrantOfTheSameKindForTheSameChildIsRefused()
        {
            Assert.That(CareRules.CanIssue(Grant(), "child:1", ServiceKind.Childcare, 1000),
                Is.EqualTo(CareCode.AlreadyAuthorized));
        }

        [Test] public void ExpiredGrantDoesNotHoldTheSlot()
        {
            // 到期授权不占名额：否则儿童换服务期后永远签不出新授权。
            Assert.That(CareRules.CanIssue(Grant(expires: 500), "child:1", ServiceKind.Childcare, 501),
                Is.EqualTo(CareCode.Applied));
            Assert.That(CareRules.CanIssue(Grant(expires: 500), "child:1", ServiceKind.Childcare, 500),
                Is.EqualTo(CareCode.AlreadyAuthorized), "边界同 IsExpired：now == ExpiresTick 仍算有效");
        }

        [Test] public void DifferentKindOrDifferentBeneficiaryNeverBlocks()
        {
            Assert.That(CareRules.CanIssue(Grant(ServiceKind.Childcare), "child:1", ServiceKind.Education, 1000),
                Is.EqualTo(CareCode.Applied), "照护与学习是两项需求，互不占位");
            Assert.That(CareRules.CanIssue(Grant(beneficiary: "child:1"), "child:2", ServiceKind.Childcare, 1000),
                Is.EqualTo(CareCode.Applied), "另一个孩子的同名需求不受影响");
        }

        // ── 四角色：受益者不得兼任执行者 ───────────────────────────────────────────────

        [Test] public void BeneficiaryCannotBeItsOwnExecutor()
        {
            Assert.That(CareRules.CanExecute("child:1", "child:1"), Is.EqualTo(CareCode.ExecutorUnavailable));
        }

        [Test] public void UnnamedExecutorIsNotAnUnavailableExecutor()
        {
            // "没指定执行者"与"执行者不可用"是两件事；混为一谈会拒掉所有只登记付款方的授权。
            Assert.That(CareRules.CanExecute("child:1", null), Is.EqualTo(CareCode.Applied));
            Assert.That(CareRules.CanExecute("child:1", "  "), Is.EqualTo(CareCode.Applied));
        }

        [Test] public void ADistinctExecutorPassesTheGate()
            => Assert.That(CareRules.CanExecute("child:1", "adult:1"), Is.EqualTo(CareCode.Applied));

        // ── 既有纪律不得回退 ──────────────────────────────────────────────────────────

        [Test] public void ChargingStillRequiresTheExplicitPayerGrant()
        {
            var grant = Grant();
            Assert.That(CareRules.CanCharge(grant, "payer:1", 100, 1000), Is.EqualTo(CareCode.Applied));
            Assert.That(CareRules.CanCharge(grant, "someoneElse", 100, 1000), Is.EqualTo(CareCode.PayerNotAuthorized));
            Assert.That(CareRules.CanCharge(grant, "payer:1", 6000, 1000), Is.EqualTo(CareCode.CapExceeded));
            Assert.That(CareRules.CanCharge(grant, "payer:1", 100, 60001), Is.EqualTo(CareCode.Expired));
        }

        [Test] public void CapacityAndHoursUnprovenKeepsTheServiceOnTheVanillaPath()
        {
            // M7/M8：少一道门就不得报价；这里同时钉住"未证明"不会把能力提升为可收费。
            var readiness = new ServiceReadiness(true, true, false, true, true, true, true);
            Assert.That(readiness.AllProven, Is.False);
            Assert.That(ServiceCapabilityRules.Evaluate(ServiceCapability.PublicVanilla, readiness),
                Is.Not.EqualTo(ServiceCapability.ReadyPaid));
            Assert.That(ServiceCapabilityRules.MayCharge(ServiceCapability.PublicVanilla), Is.False);
        }
    }
}
