using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Production;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Governance;
using PersonalChronicle.Domain.PawnEconomy.Production;

namespace PersonalChronicle.Tests
{
    /// <summary>U6-A (M10 "authorised, not yet started"): the ledger is the truth source for the restock
    /// input, must survive a reload without rewriting receipts, must never lock up forever, and the
    /// category-level convergence must never let one quantity be covered by two buckets.</summary>
    [TestFixture]
    public sealed class ProductionAuthorisationTests
    {
        [Test] public void EffectiveNotStartedNeverCountsTheSameQuantityTwice()
        {
            // M10 完整闭环（2026-10-10）：收敛式由两桶变三桶 —— 授权 − 已交付 − 在制（同一量只扣一次）。
            Assert.That(ProductionAuthorisationRules.EffectiveNotStarted(7, 0, 0), Is.EqualTo(7));
            Assert.That(ProductionAuthorisationRules.EffectiveNotStarted(7, 0, 3), Is.EqualTo(4),
                "work already observed in progress reduces the not-started bucket");
            Assert.That(ProductionAuthorisationRules.EffectiveNotStarted(7, 4, 0), Is.EqualTo(3),
                "quantity already delivered against real completions reduces it too");
            Assert.That(ProductionAuthorisationRules.EffectiveNotStarted(7, 4, 3), Is.EqualTo(0),
                "delivered and wip are subtracted together and never overlap");
            Assert.That(ProductionAuthorisationRules.EffectiveNotStarted(7, 0, 7), Is.EqualTo(0));
            Assert.That(ProductionAuthorisationRules.EffectiveNotStarted(7, 0, 9), Is.EqualTo(0), "never negative");
            // 反向对照：只翻转在制读数，结果必须不同 —— 否则这条断言没有牙齿
            Assert.That(ProductionAuthorisationRules.EffectiveNotStarted(7, 0, 0),
                Is.Not.EqualTo(ProductionAuthorisationRules.EffectiveNotStarted(7, 0, 7)));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ProductionAuthorisationRules.EffectiveNotStarted(-1, 0, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ProductionAuthorisationRules.EffectiveNotStarted(1, -1, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ProductionAuthorisationRules.EffectiveNotStarted(1, 0, -1));
        }

        [Test] public void ADeliveredAuthorisationIsConsumedAndUnrelatedProductionsNeverTouchIt()
        {
            // M10 完整闭环：真实完成事件按**工单稳定键**核销 —— 授权量被消耗；无关工单与异品类产出都不许动它。
            var ledger = new RestockProposalLedger();
            var proposal = new ProductionProposal("1", "Steel", 30, null);
            Assert.That(ledger.TryAccept(proposal, 10, "Bench:12#Bill_45", out _, out _, out _), Is.True);
            Assert.That(ledger.NetOutstandingAmount("1", "Steel"), Is.EqualTo(30), "nothing delivered yet");
            Assert.That(ledger.RecordDelivery("Bench:12#Bill_45", "Steel", 10), Is.EqualTo(1), "the bound bill must consume it");
            Assert.That(ledger.DeliveredAmount("1", "Steel"), Is.EqualTo(10));
            Assert.That(ledger.NetOutstandingAmount("1", "Steel"), Is.EqualTo(20), "delivery consumes the authorisation");
            Assert.That(ledger.RecordDelivery("Bench:9#Bill_1", "Steel", 10), Is.EqualTo(0), "an unrelated bill never consumes it");
            Assert.That(ledger.RecordDelivery("Bench:12#Bill_45", "Wood", 10), Is.EqualTo(0), "a different category never consumes it");
            Assert.That(ledger.DeliveredAmount("1", "Steel"), Is.EqualTo(10), "refused deliveries change nothing");
            Assert.That(ledger.RecordDelivery("Bench:12#Bill_45", "Steel", 25), Is.EqualTo(1));
            Assert.That(ledger.NetOutstandingAmount("1", "Steel"), Is.EqualTo(0), "over-delivery never goes negative");
        }

        [Test] public void AnUnboundRowIsNeverConsumedAndAV1SaveLoadsAsUndelivered()
        {
            // M10 完整闭环：v1 旧档行没有工单绑定 ⇒ null/0 是**显式语义**（未绑定、未交付），且永不被核销。
            var row = new ProductionAuthorisationSaveData.Row
            { ProposalId = "1|Steel|10", Receipt = "restock-v1:1|Steel|10", AcceptedTick = 5 };
            Assert.That(row.TargetBillKey, Is.Null, "a v1 row loads unbound, never migrated by guess");
            Assert.That(row.DeliveredAmount, Is.EqualTo(0L));
            Assert.That(ProductionAuthorisationSaveData.SchemaVersion, Is.EqualTo(2));
            Assert.That(ProductionAuthorisationSaveData.MaximumRows, Is.EqualTo(16), "the single bound is unchanged");
            var ledger = new RestockProposalLedger();
            Assert.That(ledger.ImportRows(new[] { new RestockProposalRow(row.ProposalId, row.Receipt, row.AcceptedTick) }), Is.EqualTo(1));
            Assert.That(ledger.RecordDelivery("Bench:12#Bill_45", "Steel", 5), Is.EqualTo(0),
                "an unbound (legacy) row can never be consumed by a completion event");
        }

        [Test] public void ReceiptsSurviveAReloadAndReplayTheOriginalReceipt()
        {
            var live = new RestockProposalLedger();
            var proposal = new ProductionProposal("1", "Steel", 7, null);
            Assert.That(live.TryAccept(proposal, 100, out string first, out _, out _), Is.True);
            var restored = new RestockProposalLedger();
            Assert.That(restored.ImportRows(live.ExportRows()), Is.EqualTo(1));
            Assert.That(restored.TryAccept(proposal, 500, out string second, out bool replay, out _), Is.True);
            Assert.That(replay, Is.True, "the same proposal after a reload is still a replay");
            Assert.That(second, Is.EqualTo(first), "a reload must not rewrite a receipt the player already holds");
            Assert.That(restored.Count, Is.EqualTo(1));
        }

        [Test] public void CorruptRowsAreDroppedAndTheBoundNeverGrows()
        {
            var rows = new System.Collections.Generic.List<RestockProposalRow>
            {
                new RestockProposalRow("1|Steel|5", RestockProposalLedger.ReceiptPrefix + "1|Steel|5", 10),
                new RestockProposalRow("no-separators", "restock-v1:no-separators", 20),
                new RestockProposalRow("1|Steel|0", "restock-v1:zero", 30),
                new RestockProposalRow("1|Steel|5", "restock-v1:duplicate", 40),
                new RestockProposalRow(" ", "restock-v1:blank", 50),
                new RestockProposalRow("2|Steel|5", null, 60)
            };
            var ledger = new RestockProposalLedger();
            Assert.That(ledger.ImportRows(rows), Is.EqualTo(1), "only the structurally valid row survives");
            Assert.That(ledger.Count, Is.EqualTo(1));
            var oversize = new System.Collections.Generic.List<RestockProposalRow>();
            for (int i = 0; i < RestockProposalLedger.MaximumReceipts + 5; i++)
                oversize.Add(new RestockProposalRow("1|Cat" + i + "|5", "restock-v1:x" + i, 100 + i));
            var bounded = new RestockProposalLedger();
            Assert.That(bounded.ImportRows(oversize), Is.EqualTo(RestockProposalLedger.MaximumReceipts));
            Assert.That(bounded.Count, Is.EqualTo(RestockProposalLedger.MaximumReceipts));
        }

        [Test] public void AuthorisedAmountSumsOnlyTheRequestedCategory()
        {
            var ledger = new RestockProposalLedger();
            Assert.That(ledger.TryAccept(new ProductionProposal("1", "Steel", 5, null), 1, out _, out _, out _), Is.True);
            Assert.That(ledger.TryAccept(new ProductionProposal("1", "Steel", 7, null), 2, out _, out _, out _), Is.True);
            Assert.That(ledger.TryAccept(new ProductionProposal("1", "Meal", 9, null), 3, out _, out _, out _), Is.True);
            Assert.That(ledger.TryAccept(new ProductionProposal("2", "Steel", 11, null), 4, out _, out _, out _), Is.True);
            Assert.That(ledger.AuthorisedAmount("1", "Steel"), Is.EqualTo(12), "only the same map and category sum");
            Assert.That(ledger.AuthorisedAmount("1", "Meal"), Is.EqualTo(9));
            Assert.That(ledger.AuthorisedAmount("2", "Steel"), Is.EqualTo(11));
            Assert.That(ledger.AuthorisedAmount("1", "Missing"), Is.EqualTo(0));
        }

        [Test] public void ReleaseRemovesExactlyOneRowAndAllowsAFreshAuthorisation()
        {
            var ledger = new RestockProposalLedger();
            var proposal = new ProductionProposal("1", "Steel", 5, null);
            Assert.That(ledger.TryAccept(proposal, 1, out _, out _, out _), Is.True);
            Assert.That(ledger.TryRelease(proposal.ProposalId), Is.True);
            Assert.That(ledger.Count, Is.EqualTo(0));
            Assert.That(ledger.TryRelease(proposal.ProposalId), Is.False, "releasing a missing row is a no-op");
            Assert.That(ledger.TryAccept(proposal, 2, out _, out bool replay, out _), Is.True);
            Assert.That(replay, Is.False, "after a release the same proposal is a fresh authorisation");
        }

        [Test] public void ExpiryIsDeterministicAndRespectsTheAgeBoundary()
        {
            var ledger = new RestockProposalLedger();
            Assert.That(ledger.TryAccept(new ProductionProposal("1", "Steel", 5, null), 1000, out _, out _, out _), Is.True);
            Assert.That(ledger.TrimExpired(1000 + ProductionAuthorisationRules.MaximumAuthorisationAgeTicks - 1,
                ProductionAuthorisationRules.MaximumAuthorisationAgeTicks), Is.EqualTo(0), "inside the window nothing expires");
            Assert.That(ledger.TrimExpired(1000 + ProductionAuthorisationRules.MaximumAuthorisationAgeTicks,
                ProductionAuthorisationRules.MaximumAuthorisationAgeTicks), Is.EqualTo(0),
                "exactly at the bound the row is not yet expired（判据是 `>` 而非 `>=`，等值样本必须钉住）");
            Assert.That(ledger.Count, Is.EqualTo(1));
            Assert.That(ledger.TrimExpired(1000 + ProductionAuthorisationRules.MaximumAuthorisationAgeTicks + 1,
                ProductionAuthorisationRules.MaximumAuthorisationAgeTicks), Is.EqualTo(1), "outside the window the row expires");
            Assert.That(ledger.Count, Is.EqualTo(0), "an expired row must not lock the ledger forever");
            // 反向对照：tick 回退（读档/回退时间）绝不能让任何行过期
            var rewound = new RestockProposalLedger();
            Assert.That(rewound.TryAccept(new ProductionProposal("1", "Steel", 5, null), 9000, out _, out _, out _), Is.True);
            Assert.That(rewound.TrimExpired(10, ProductionAuthorisationRules.MaximumAuthorisationAgeTicks), Is.EqualTo(0));
            Assert.That(rewound.Count, Is.EqualTo(1));
        }

        [Test] public void AFullLedgerSurvivesAReloadAndStillRefuses()
        {
            var ledger = new RestockProposalLedger();
            for (int i = 0; i < RestockProposalLedger.MaximumReceipts; i++)
                Assert.That(ledger.TryAccept(new ProductionProposal("1", "Cat" + i, 5, null), 10 + i, out _, out _, out _), Is.True);
            var restored = new RestockProposalLedger();
            restored.ImportRows(ledger.ExportRows());
            Assert.That(restored.Count, Is.EqualTo(RestockProposalLedger.MaximumReceipts));
            Assert.That(restored.TryAccept(new ProductionProposal("1", "Overflow", 1, null), 999, out _, out _, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo(ProductionProposalReasons.ReceiptCapacityReached),
                "capacity survives the reload: it is never reported as 'not wired'");
        }

        // CHG-20261012-ECON-CONTRACTIZE · B2：`UpdatingOnePolicyFieldCannotSilentlyClearTheServicePrice`
        // **已删除** —— 它守的是政策存档 `InstitutionPolicySaveData.Apply` 的"整份提交"语义
        //（改工资时若漏传服务价就会静默清零，故第 9/10 参故意不给默认值，让漏传无法编译）。
        // B2 之后该存储整体退役：服务价改由**体检中心建筑字段**承载，只有自己的 gizmo 会写它
        //（不存在"一次提交多个字段"的路径 ⇒ 这类误清零在结构上不可能发生）。
        // ⚠ 若将来重建"一次提交多字段"的设置面，必须重新引入同类断言（显式读回 + 无默认值）。
    }
}
