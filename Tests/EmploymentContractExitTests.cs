using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Employment;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// M6「合理退出」纯规则。每条断言都对应"什么情况下会失败"：
    /// 顺序颠倒 / 幂等丢失 / null 崩溃 / 自然结束与供给失败混淆 ⇒ 必挂。
    /// </summary>
    [TestFixture]
    public sealed class EmploymentContractExitTests
    {
        private static WorkContract Contract(int signed = 100, int expires = 60000, int maxJobs = 1, int completed = 0)
            => new WorkContract("contract:1", "pawn:1", "inst:1", "inst:1/2/3", new[] { "Crafting" }, 500, maxJobs,
                signed, expires, ContractState.Active, completed);

        [Test] public void LiveContractWithOpenInstitutionAndServeablePositionKeepsRunning()
            => Assert.That(ContractExitRules.ShouldExit(Contract(), 1000, true, true), Is.EqualTo(ContractExitCode.None),
                "条件都正常时不得结束合同");

        [Test] public void ExpiryIsReportedBeforeAnyOtherReason()
        {
            // 顺序断言：同时过期 + 停业 + 岗位没了时，必须报"到期" —— 自然结束优先于供给侧原因。
            Assert.That(ContractExitRules.ShouldExit(Contract(expires: 500), 600, false, false),
                Is.EqualTo(ContractExitCode.Expired));
        }

        [Test] public void ExpiryBoundaryMatchesCanAcceptEvidence()
        {
            // now == ExpiresTick 仍可记一次工（与 CanAcceptEvidence 同边界），now 超出才结束。
            Assert.That(ContractExitRules.ShouldExit(Contract(expires: 500), 500, true, true), Is.EqualTo(ContractExitCode.None));
            Assert.That(ContractExitRules.ShouldExit(Contract(expires: 500), 501, true, true), Is.EqualTo(ContractExitCode.Expired));
        }

        [Test] public void JobCapEndsTheContract()
            => Assert.That(ContractExitRules.ShouldExit(Contract(completed: 1), 1000, true, true),
                Is.EqualTo(ContractExitCode.JobCapReached));

        [Test] public void ClosedInstitutionEndsTheContractEvenWhenThePositionLooksAlive()
        {
            // EMP-07 的核心：制度关闭必须释放岗位，不能因为区域还在就继续占着工作类型。
            Assert.That(ContractExitRules.ShouldExit(Contract(), 1000, false, true),
                Is.EqualTo(ContractExitCode.InstitutionClosed));
        }

        [Test] public void UnavailablePositionEndsTheContract()
            => Assert.That(ContractExitRules.ShouldExit(Contract(), 1000, true, false),
                Is.EqualTo(ContractExitCode.PositionUnavailable));

        [Test] public void AlreadyFinishedContractIsNeverExitedTwice()
        {
            // 幂等：已 Terminated 的合同在任何条件下都返回 None，否则重复复查会重复释放所有权记录。
            var closed = new WorkContract("contract:1", "pawn:1", "inst:1", "inst:1/2/3", new[] { "Crafting" }, 500, 1,
                100, 60000, ContractState.Terminated, 0);
            Assert.That(ContractExitRules.ShouldExit(closed, 99999, false, false), Is.EqualTo(ContractExitCode.None));
        }

        [Test] public void NullContractIsSafeAndEndsNothing()
            => Assert.That(ContractExitRules.ShouldExit(null, 1000, false, false), Is.EqualTo(ContractExitCode.None));

        [Test] public void NaturalEndsAreDistinguishedFromSupplyFailures()
        {
            Assert.That(ContractExitRules.IsNaturalEnd(ContractExitCode.Expired), Is.True);
            Assert.That(ContractExitRules.IsNaturalEnd(ContractExitCode.JobCapReached), Is.True);
            Assert.That(ContractExitRules.IsNaturalEnd(ContractExitCode.InstitutionClosed), Is.False,
                "停业不是自然结束，应提示玩家");
            Assert.That(ContractExitRules.IsNaturalEnd(ContractExitCode.PositionUnavailable), Is.False);
            Assert.That(ContractExitRules.IsNaturalEnd(ContractExitCode.None), Is.False);
        }
    }
}
