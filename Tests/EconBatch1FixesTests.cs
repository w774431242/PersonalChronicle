using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Application.PawnEconomy.Governance;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Governance;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 经济 AI 审计第 1 批（可启动批）的**离线可证伪面**。
    /// 覆盖：FA-01 零余额账户（账本 + 编解码）、FA-02 逐能力判据（纯规则）、
    /// FA-03 关闭后重开的阶段语义、FA-23 故障只能由显式核验解除（域规则 + 存档属性往返）。
    /// 需要真实 Pawn / 地图 / 存档宿主的场景（FA-10/FA-11 的运行时分支、Faulted 重审命令的宿主判据）
    /// 属 GameVerified，本文件不冒充覆盖。
    /// </summary>
    [TestFixture]
    public sealed class EconBatch1EnrollmentTests
    {
        [Test] public void UnenrolledAccountStaysUnknownInsteadOfZero()
        {
            var ledger = new FundedSettlementLedger();
            Assert.That(ledger.ReadAccount("Pawn_1"), Is.Null, "未登记 ≠ 零余额：未知必须是未知（M5）");
            Assert.That(ledger.EnrollAccount("Pawn_1"), Is.True);
            Assert.That(ledger.EnrollAccount("Pawn_1"), Is.False, "登记幂等");
            Assert.That(ledger.ReadAccount("Pawn_1").AvailableMinor, Is.Zero);
            Assert.That(ledger.Version, Is.Zero, "登记不占条目容量、不动版本（并发期望版本不受影响）");
            Assert.That(ledger.TotalMinor, Is.Zero, "登记不发钱");
        }
        [Test] public void EnrollmentSurvivesTheCodecAndNeverOverwritesABalance()
        {
            var ledger = FundedSettlementLedger.Restore(new[]
            {
                new SettlementEntry(new SettlementIntent("dep", 0,
                    new ReserveMovementIntent("dep", ReserveMovementKind.Deposit, 0, "party", "stack", 100)), SettlementPhase.Finalized),
                new SettlementEntry(SettlementIntent.ForWage("wage:o1", 1, "o1", "Human_7", 2500), SettlementPhase.Finalized),
            });
            ledger.EnrollAccount("Pawn_9");
            var loaded = SettlementLedgerCodec.Decode(SettlementLedgerCodec.Encode(ledger, null, null));
            Assert.That(loaded.PreserveRaw, Is.False);
            Assert.That(loaded.Ledger.ReadAccount("Pawn_9").AvailableMinor, Is.Zero);
            Assert.That(loaded.Ledger.ReadAccount("Human_7").AvailableMinor, Is.EqualTo(2500), "既有余额不被登记抹掉");
            Assert.That(loaded.Ledger.EnrollAccount("Human_7"), Is.False, "已有账户（含转入建立的）拒绝重复登记");
            Assert.That(loaded.Ledger.ReadAccount("Human_7").AvailableMinor, Is.EqualTo(2500));
        }
        [Test] public void LegacyPayloadWithoutEnrollmentsLoadsAsUnknown()
        {
            var loaded = SettlementLedgerCodec.Decode("<SettlementLedger schema=\"1\" version=\"0\" />");
            Assert.That(loaded.PreserveRaw, Is.False, "旧档没有登记元素是合法状态，不判坏档");
            Assert.That(loaded.Ledger.ReadAccount("Pawn_1"), Is.Null);
        }
        [Test] public void EnrollmentIsBoundedAndRefusesRatherThanSilentlyDropping()
        {
            var ledger = new FundedSettlementLedger();
            for (int i = 0; i < FundedSettlementLedger.MaximumEnrolledAccounts; i++)
                Assert.That(ledger.EnrollAccount("Pawn_" + i), Is.True);
            Assert.That(ledger.EnrollAccount("Pawn_overflow"), Is.False, "越过上限必须拒绝（可观察），不得静默丢弃已登记账户");
        }
    }

    // CHG-20261012-ECON-CONTRACTIZE · B2：`EconBatch1CapabilityTests` 夹具**已删除** —— 它断言的是
    // 机构能力徽章的五级原因码（未配置 / 未挂牌 / 停业 / 非营业时段 / 制度未开放），
    // 而这些读数全部建立在已被净删的机构政策之上（卡片亦改为单行只读）。
    // 制度阶段（下面 `EconBatch1InstitutionPhaseTests`）不受影响，继续保留。

    [TestFixture]
    public sealed class EconBatch1InstitutionPhaseTests
    {
        private static InstitutionReadiness Ready() => new InstitutionReadiness(true, true, true, true, true, true, true);

        [Test] public void ClosingWithOnlyRetainedAssetsReopensToActive()
        {
            var service = new InstitutionModeService(new InstitutionModeState(InstitutionPhase.Active, true, 1));
            service.Observe(false, Ready(), false);
            Assert.That(service.State.Phase, Is.EqualTo(InstitutionPhase.Off),
                "只有正常余额/储备时关闭 ⇒ 直接 Off，不再进 Draining");
            service.Observe(true, Ready(), false);
            Assert.That(service.State.Phase, Is.EqualTo(InstitutionPhase.Active),
                "账本健康且无未完成事项 ⇒ 可重新开启，不要求余额归零（FA-03）");
        }

        [Test] public void UnfinishedRightsStillKeepDrainingAcrossReopen()
        {
            var service = new InstitutionModeService(new InstitutionModeState(InstitutionPhase.Active, true, 1));
            service.Observe(false, Ready(), true);
            Assert.That(service.State.Phase, Is.EqualTo(InstitutionPhase.Draining), "有在途承诺 ⇒ 必须清退");
            service.Observe(true, Ready(), true);
            Assert.That(service.State.Phase, Is.EqualTo(InstitutionPhase.Draining),
                "在途承诺未清完就重开 ⇒ 仍 Draining（fail-closed 语义不变）");
        }

        [Test] public void FaultedNeverAdvancesOnItsOwnEvidence()
        {
            var service = new InstitutionModeService(new InstitutionModeState(InstitutionPhase.Active, true, 1));
            service.Observe(true, new InstitutionReadiness(true, true, true, true, true, true, true, "Backing"), false);
            Assert.That(service.State.Phase, Is.EqualTo(InstitutionPhase.Faulted));
            service.Observe(true, Ready(), false);
            Assert.That(service.State.Phase, Is.EqualTo(InstitutionPhase.Faulted),
                "故障源消失也不自动离开 Faulted：只能由显式重新核验解除（FA-23）");
        }

        [Test] public void ExplicitClearRequiresFaultedStateAndKeepsRevisionMonotonic()
        {
            var data = new InstitutionModeSaveData();
            Assert.That(data.ClearFaultAfterReverification("Backing"), Is.False, "非故障态拒绝");
            data.State = new InstitutionModeState(InstitutionPhase.Faulted, true, 5, "Backing");
            Assert.That(data.ClearFaultAfterReverification(null), Is.False, "没有故障码可解除时拒绝");
            Assert.That(data.ClearFaultAfterReverification("Backing"), Is.True);
            Assert.That(data.State.Phase, Is.EqualTo(InstitutionPhase.Preparing));
            Assert.That(data.State.Fault, Is.Null);
            Assert.That(data.State.Revision, Is.EqualTo(6), "revision +1 即持久审计痕迹");
            Assert.That(data.ReverifiedFault, Is.EqualTo("Backing"));
            Assert.That(data.ClearFaultAfterReverification("Backing"), Is.False, "重复核验幂等拒绝");
        }

        [Test] public void ReverifiedFaultSurvivesTheModePayloadAndOldPayloadsStayValid()
        {
            var data = new InstitutionModeSaveData();
            data.State = new InstitutionModeState(InstitutionPhase.Faulted, true, 5, "Backing");
            Assert.That(data.ClearFaultAfterReverification("Backing"), Is.True);
            string payload = (string)typeof(InstitutionModeSaveData)
                .GetMethod("Encode", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(data, null);

            var probe = new InstitutionModeSaveData();
            typeof(InstitutionModeSaveData).GetMethod("Decode", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(probe, new object[] { payload });
            Assert.That(probe.ReverifiedFault, Is.EqualTo("Backing"));
            Assert.That(probe.State.Revision, Is.EqualTo(6));

            // 旧档（无 reverifiedFault 属性）必须照常加载，且不因此被判定为未知字段。
            var legacy = new InstitutionModeSaveData();
            typeof(InstitutionModeSaveData).GetMethod("Decode", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(legacy, new object[] { "<InstitutionMode schema=\"1\" phase=\"Preparing\" desired=\"true\" revision=\"2\" />" });
            Assert.That(legacy.ReverifiedFault, Is.Null);
            Assert.That(legacy.State.Phase, Is.EqualTo(InstitutionPhase.Preparing));
        }
    }
}
