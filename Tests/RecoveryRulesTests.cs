using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Learning;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// M9 §5 中断去重 与 §6 有界历史裁剪。
    /// 去重断言"一次事故只记一份"；裁剪断言"凭据不可随经验消失"。
    /// </summary>
    [TestFixture]
    public sealed class RecoveryRulesTests
    {
        private static HistoryEntry Row(int tick, bool terminal = true, bool holds = false)
            => new HistoryEntry(terminal, tick, holds);

        [Test] public void OneInterruptionProducesExactlyOneStageNotThree()
        {
            // §5 第 5 条："不能一个中断同时记『未买到、买到未吃、服务失败』三份相同失败"。
            Assert.That(InterruptionRules.Stage(delivered: false, consumed: false, settled: false),
                Is.EqualTo(InterruptionStage.NotExecuted));
            Assert.That(InterruptionRules.Stage(true, false, false), Is.EqualTo(InterruptionStage.Delivered));
            Assert.That(InterruptionRules.Stage(true, true, false), Is.EqualTo(InterruptionStage.Consumed));
            Assert.That(InterruptionRules.Stage(true, true, true), Is.EqualTo(InterruptionStage.Settled),
                "取最靠后为真的一档，而不是每档各记一份");
        }

        [Test] public void ASettledInterruptionIsNotAFailure()
        {
            Assert.That(InterruptionRules.ShouldRecordFailure(InterruptionStage.Settled), Is.False,
                "钱物两清的中断只是行为被打断，不是任何一方的违约");
            Assert.That(InterruptionRules.ShouldRecordFailure(InterruptionStage.NotExecuted), Is.True);
            Assert.That(InterruptionRules.ShouldRecordFailure(InterruptionStage.Delivered), Is.True);
        }

        [Test] public void NothingIsTrimmedWhileWithinTheBound()
        {
            var rows = new List<HistoryEntry> { Row(1), Row(2) };
            Assert.That(BoundedHistoryRules.Trim(rows, BoundedHistoryRules.MaximumRecentOutcomes).Count, Is.Zero);
            Assert.That(BoundedHistoryRules.Trim(null, 4).Count, Is.Zero);
        }

        [Test] public void TerminalRowsAreTrimmedBeforeUnfinishedOnes()
        {
            // 上限 2，投 4 条：最老的两条终态先走，未终态的新条目保留。
            var rows = new List<HistoryEntry> { Row(1), Row(2), Row(3, terminal: false), Row(4, terminal: false) };
            var drop = BoundedHistoryRules.Trim(rows, 2);
            Assert.That(drop.Count, Is.EqualTo(2));
            Assert.That(drop, Does.Contain(0));
            Assert.That(drop, Does.Contain(1));
            Assert.That(drop, Does.Not.Contain(2));
            Assert.That(drop, Does.Not.Contain(3));
        }

        [Test] public void OpenReconciliationIsNeverTrimmedEvenWhenItMeansStayingOverTheBound()
        {
            // §6："未清对账义务和账本去重信息不随经验裁剪" —— 裁掉它会让工资或订单重新可结算。
            var rows = new List<HistoryEntry>
            {
                Row(1, terminal: true, holds: true),
                Row(2, terminal: true, holds: true),
                Row(3, terminal: true, holds: true)
            };
            Assert.That(BoundedHistoryRules.Trim(rows, 1).Count, Is.Zero,
                "宁可超限，也不能丢财务凭据");
        }

        [Test] public void CredentialsAreSkippedAndOthersStillTrimmed()
        {
            var rows = new List<HistoryEntry>
            {
                Row(1, holds: true),      // 凭据：不可裁
                Row(2),                   // 终态、最老的非凭据
                Row(3),                   // 终态
                Row(4, terminal: false)   // 未终态
            };
            var drop = BoundedHistoryRules.Trim(rows, 3);
            Assert.That(drop.Count, Is.EqualTo(1));
            Assert.That(drop[0], Is.EqualTo(1), "凭据被跳过，裁剪落到最老的非凭据终态条目");
        }

        [Test] public void ThePublishedBoundsAreTheOnesUsed()
        {
            Assert.That(BoundedHistoryRules.MaximumRecentOutcomes, Is.EqualTo(64), "§6 原型上限");
            Assert.That(BoundedHistoryRules.MaximumActiveRetries, Is.EqualTo(16), "§6 原型上限");
        }
    }
}
