using NUnit.Framework;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Learning;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// M9 §6 落盘的核心语义：**Tick 回退 / 读档不得重获已消费的学习额度**。
    /// 这是"有界"之外的第二个硬约束 —— 少了它，玩家反复读档就能无限刷学习额度。
    /// </summary>
    [TestFixture]
    public sealed class LearningPersistenceTests
    {
        private const string Dim = "ReliabilityPreference";

        private static StyleLedger Spent()
        {
            var ledger = new StyleLedger();
            ledger.BeginDay(10);
            ledger.Deliver(Dim, "s1", 5f, 0.5f, true);
            Assert.That(ledger.UsedToday(Dim), Is.GreaterThan(0f), "前置：确实消费了当日额度");
            return ledger;
        }

        [Test] public void TheSameDayNeverRefundsTheSpentBudget()
        {
            var ledger = Spent();
            Assert.That(ledger.BeginDay(10), Is.False, "同一天再次调用不得清空");
            Assert.That(ledger.UsedToday(Dim), Is.GreaterThan(0f), "额度必须仍然被占用");
        }

        [Test] public void ABackwardsTickDoesNotRefundTheSpentBudget()
        {
            // 读档/时间回退 ⇒ 日序号变小（或不变）⇒ 绝不清额度。
            var ledger = Spent();
            Assert.That(ledger.BeginDay(9), Is.False);
            Assert.That(ledger.BeginDay(0), Is.False);
            Assert.That(ledger.BeginDay(-1), Is.False, "时间不可信时同样不清");
            Assert.That(ledger.UsedToday(Dim), Is.GreaterThan(0f));
        }

        [Test] public void OnlyARealNewDayRefundsTheBudget()
        {
            var ledger = Spent();
            Assert.That(ledger.BeginDay(11), Is.True, "真的跨日才清");
            Assert.That(ledger.UsedToday(Dim), Is.Zero);
            Assert.That(ledger.OffsetOf(Dim), Is.GreaterThan(0f), "额度清了，但**已学到的偏移必须保留**");
        }

        [Test] public void RestoreReinstatesEverythingWithoutClearingTheDailyBudget()
        {
            // 恢复走 Restore 而非 BeginDay：读档本身不能等价于"新的一天"。
            var ledger = new StyleLedger();
            ledger.Restore(dayStamp: 42,
                offsets: new[] { new System.Collections.Generic.KeyValuePair<string, float>(Dim, 0.03f) },
                used: new[] { new System.Collections.Generic.KeyValuePair<string, float>(Dim, 0.03f) },
                processedIds: new[] { "s1" });
            Assert.That(ledger.DayStamp, Is.EqualTo(42));
            Assert.That(ledger.OffsetOf(Dim), Is.EqualTo(0.03f).Within(1e-6f));
            Assert.That(ledger.UsedToday(Dim), Is.EqualTo(0.03f).Within(1e-6f), "已消费额度必须一并恢复");
            Assert.That(ledger.Deliver(Dim, "s1", 5f, 0.5f, true), Is.EqualTo(StyleLearningCode.ReplaySummary),
                "去重水位必须跨存读档有效，否则同一份证据会被反复学习");
            Assert.That(ledger.Deliver(Dim, "s2", 5f, 0.5f, true), Is.EqualTo(StyleLearningCode.DailyBudgetExhausted),
                "额度已用满 ⇒ 读档后也不得再学");
        }

        [Test] public void RestoreToleratesMissingOrNegativeValues()
        {
            var ledger = new StyleLedger();
            ledger.Restore(-1, null, null, null);
            Assert.That(ledger.DayStamp, Is.EqualTo(-1));
            ledger.Restore(0, new[] { new System.Collections.Generic.KeyValuePair<string, float>(Dim, -0.5f) },
                new[] { new System.Collections.Generic.KeyValuePair<string, float>(Dim, -2f) },
                new[] { "  ", null });
            Assert.That(ledger.UsedToday(Dim), Is.Zero, "负额度一律钳到 0，绝不产生负等待或负记账");
        }

        [Test] public void TheSaveContainerDeclaresItsIdentityAndBounds()
        {
            // SAVE-001：容器自身版本可识别；§6 各上限均为正数（有界而非无界增长）。
            Assert.That(LearningSaveData.SchemaVersion, Is.GreaterThanOrEqualTo(1));
            Assert.That(LearningSaveData.MaximumProcessedSummaries, Is.GreaterThan(0));
            Assert.That(LearningSaveData.MaximumLedgers, Is.GreaterThan(0));
            Assert.That(LearningSaveData.MaximumRetries, Is.GreaterThan(0));
        }

        [Test] public void FreshRowsCarryUsableDefaultsSoAnOldSaveCanLoad()
        {
            // SAVE-003：新字段必须有默认值 —— 旧档没有这些节点时，构造出的行必须立刻可用。
            var ledgerRow = new LearningSaveData.LedgerRow();
            Assert.That(ledgerRow.DayStamp, Is.EqualTo(-1), "未知日序号用 -1 表达，不是 0（0 是合法日）");
            Assert.That(ledgerRow.OffsetDimensions, Is.Not.Null);
            Assert.That(ledgerRow.ProcessedSummaries, Is.Not.Null);
            var retryRow = new LearningSaveData.RetryRow();
            Assert.That(retryRow.Failures, Is.Zero);
            Assert.That(retryRow.NextTick, Is.Zero);
        }

        [Test] public void RetryStateSurvivesThroughTheContainerRows()
        {
            // 退避账的可序列化形态：键 / 失败数 / 下次可试时刻 / 条件指纹（§2 RetryState 契约）。
            var row = new LearningSaveData.RetryRow
            { Key = "pawn:1|Purchase|Food", Failures = 3, NextTick = 1200, Fingerprint = "r1|s2|500|Adult|7" };
            Assert.That(row.Key, Does.Contain("|"), "键由 Goal/ActionClass/Reason 复合而成");
            Assert.That(row.Failures, Is.EqualTo(3));
            Assert.That(row.Fingerprint, Is.Not.Null, "指纹随行落盘，供诊断与变化比对（自身口径由 RetryBackoffRulesTests 钉住）");
        }
    }
}
