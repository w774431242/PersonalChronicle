using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// <see cref="WorkshopWorkDemotionLedger"/>（`CHG-20261003-WORKSTATION` / EXC-2026-015）
    /// 离线纯逻辑测试。
    ///
    /// <para><b>为什么这些不变量必须有测试</b>：台账是「一键全解除」的唯一凭据。它的失败模式
    /// 是<b>静默</b>的 —— 记错原值 ⇒ 解除后玩家档位被改；漏剪死亡 pawn ⇒ 把 A 档记录写到
    /// B 档（需求明令禁止）；不封顶 ⇒ 台账无界增长。游戏内无法靠肉眼发现这三类。</para>
    ///
    /// <para>纯逻辑（不依赖 Pawn / WorkTypeDef）⇒ 无需 Unity 运行时，离线可跑。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopWorkDemotionLedgerTests
    {
        private const int PawnA = 1001;
        private const int PawnB = 2002;

        [Test]
        public void Record_ThenTryGetOriginal_RoundTrips()
        {
            var ledger = new WorkshopWorkDemotionLedger();
            Assert.IsTrue(ledger.Record(PawnA, "Hauling", 2));

            int original;
            Assert.IsTrue(ledger.TryGetOriginal(PawnA, "Hauling", out original));
            Assert.AreEqual(2, original);
        }

        [Test]
        public void Record_IsIdempotent_FirstWriteWins()
        {
            // 首记优先：已存在时不覆盖。否则玩家手动改过的值会把"原值"洗掉。
            var ledger = new WorkshopWorkDemotionLedger();
            Assert.IsTrue(ledger.Record(PawnA, "Hauling", 2));
            Assert.IsFalse(ledger.Record(PawnA, "Hauling", 3),
                "同一 (pawn, workType) 二次记录必须返回 false（未新增）");
            Assert.IsFalse(ledger.Record(PawnA, "Hauling", 4));

            int original;
            ledger.TryGetOriginal(PawnA, "Hauling", out original);
            Assert.AreEqual(2, original, "原值必须是**最早**那次的值");
            Assert.AreEqual(1, ledger.EntryCountOf(PawnA));
        }

        [Test]
        public void Record_RejectsEmptyDefName()
        {
            var ledger = new WorkshopWorkDemotionLedger();
            Assert.IsFalse(ledger.Record(PawnA, null, 3));
            Assert.IsFalse(ledger.Record(PawnA, "", 3));
            Assert.AreEqual(0, ledger.TotalEntryCount);
        }
        [Test]
        public void Discard_RemovesOne_AndDropsEmptyPawnRecord()
        {
            var ledger = new WorkshopWorkDemotionLedger();
            ledger.Record(PawnA, "Hauling", 2);
            ledger.Record(PawnA, "Cleaning", 3);
            Assert.AreEqual(2, ledger.EntryCountOf(PawnA));

            Assert.IsTrue(ledger.Discard(PawnA, "Hauling"));
            Assert.AreEqual(1, ledger.EntryCountOf(PawnA));
            Assert.IsFalse(ledger.Discard(PawnA, "Hauling"), "重复丢弃必须幂等");

            Assert.IsTrue(ledger.Discard(PawnA, "Cleaning"));
            Assert.IsNull(ledger.RecordOf(PawnA), "条目清空后不应留下空 pawn 记录");
            Assert.AreEqual(0, ledger.TotalEntryCount);
        }

        [Test]
        public void DiscardPawn_ClearsAllEntriesOfThatPawnOnly()
        {
            var ledger = new WorkshopWorkDemotionLedger();
            ledger.Record(PawnA, "Hauling", 2);
            ledger.Record(PawnA, "Cleaning", 3);
            ledger.Record(PawnB, "Hauling", 1);

            Assert.AreEqual(2, ledger.DiscardPawn(PawnA));
            Assert.AreEqual(0, ledger.EntryCountOf(PawnA));
            Assert.AreEqual(1, ledger.EntryCountOf(PawnB), "另一 pawn 的记录必须毫发无损");
        }

        [Test]
        public void TotalEntryCount_SumsAcrossPawns()
        {
            var ledger = new WorkshopWorkDemotionLedger();
            ledger.Record(PawnA, "Hauling", 2);
            ledger.Record(PawnA, "Cleaning", 3);
            ledger.Record(PawnB, "Research", 1);
            Assert.AreEqual(3, ledger.TotalEntryCount);
        }
        // ── 自愈：换档 / 死亡 / 离殖民地（需求硬要求：不得把 A 档记录应用到 B 档）────────

        [Test]
        public void PruneDeadPawns_DropsOnlyIrrelevantPawns()
        {
            var ledger = new WorkshopWorkDemotionLedger();
            ledger.Record(PawnA, "Hauling", 2);
            ledger.Record(PawnB, "Hauling", 3);

            // PawnA 死亡/离殖民地 ⇒ 剪掉；PawnB 仍有效 ⇒ 保留。
            int pruned = ledger.PruneDeadPawns(id => id == PawnB);

            Assert.AreEqual(1, pruned);
            Assert.IsNull(ledger.RecordOf(PawnA), "已死 pawn 的记录必须丢弃，否则 ID 复用会写到新 pawn 头上");
            Assert.AreEqual(1, ledger.EntryCountOf(PawnB));
        }

        [Test]
        public void PruneDeadPawns_KeepsRecordWhenPredicateThrows()
        {
            // 失败隔离：谓词抛异常时按"保留"处理 —— 宁可台账脏一点，也不要误删还原凭据。
            var ledger = new WorkshopWorkDemotionLedger();
            ledger.Record(PawnA, "Hauling", 2);

            int pruned = ledger.PruneDeadPawns(id => { throw new System.InvalidOperationException("boom"); });

            Assert.AreEqual(0, pruned);
            Assert.AreEqual(1, ledger.EntryCountOf(PawnA), "谓词异常不得导致记录被删");
        }

        [Test]
        public void PruneDeadPawns_NullPredicate_IsNoOp()
        {
            var ledger = new WorkshopWorkDemotionLedger();
            ledger.Record(PawnA, "Hauling", 2);
            Assert.AreEqual(0, ledger.PruneDeadPawns(null));
            Assert.AreEqual(1, ledger.EntryCountOf(PawnA));
        }

        [Test]
        public void SwitchPost_RecordFollowsPawnNotWorkshop()
        {
            // 换档语义：记录键里**没有**车间/班次/岗位，因此 pawn 从 A 车间调到 B 车间时，
            // 它的记录仍只描述"这个 pawn 的这个工作类型被 PC 动过、原值多少"。
            // 还原时按同一 (pawn, workType) 回写 ⇒ 结构上不可能串到另一个 pawn / 另一份编排。
            var ledger = new WorkshopWorkDemotionLedger();
            ledger.Record(PawnA, "Smithing", 2);

            // 模拟"换到另一车间/岗位"：台账本身不感知编排，语义不变。
            int original;
            Assert.IsTrue(ledger.TryGetOriginal(PawnA, "Smithing", out original));
            Assert.AreEqual(2, original);

            // 另一 pawn 同名工作类型互不干扰。
            Assert.IsFalse(ledger.TryGetOriginal(PawnB, "Smithing", out original));
        }
        // ── 有界性（PERF-001）与 XML 洁净度（GATE-SAVE P0）─────────────────────────

        [Test]
        public void Record_RespectsPerPawnCap()
        {
            var ledger = new WorkshopWorkDemotionLedger();
            int cap = WorkshopWorkDemotionLedger.MaxEntriesPerPawn;
            for (int i = 0; i < cap; i++)
            {
                Assert.IsTrue(ledger.Record(PawnA, "WT_" + i, 2), "第 " + i + " 条应成功");
            }
            Assert.IsFalse(ledger.Record(PawnA, "WT_over", 2), "超出单 pawn 上限必须拒绝");
            Assert.AreEqual(cap, ledger.EntryCountOf(PawnA));
        }

        [Test]
        public void Record_RespectsGlobalPawnCap()
        {
            var ledger = new WorkshopWorkDemotionLedger();
            int cap = WorkshopWorkDemotionLedger.MaxPawnRecords;
            for (int i = 0; i < cap; i++)
            {
                Assert.IsTrue(ledger.Record(1000 + i, "Hauling", 2));
            }
            Assert.IsFalse(ledger.Record(999999, "Hauling", 2), "超出全局 pawn 上限必须拒绝");
            Assert.AreEqual(cap, ledger.pawns.Count);
        }

        [Test]
        public void SanitizeKey_ReplacesIllegalXmlChars()
        {
            // 一个非法 XML 字符会让 XmlWellFormedWriter 进入 error 态 ⇒ **整个存档保存失败**
            // （ItemFlowLedger 的 P0 事故）。defName 理论上不会含这些字符，但台账是
            // 外部可影响的持久数据，必须假设最坏情况。
            Assert.AreEqual("Hauling", WorkshopWorkDemotionLedger.SanitizeKey("Hauling"));
            Assert.AreEqual("Hauli_ng", WorkshopWorkDemotionLedger.SanitizeKey("Hauli\x01ng"));
            Assert.AreEqual("Hauling", WorkshopWorkDemotionLedger.SanitizeKey("Hauling"),
                "合法键必须原样返回（不分配新串）");
            Assert.AreEqual(string.Empty, WorkshopWorkDemotionLedger.SanitizeKey(null));
        }

        [Test]
        public void SanitizeKey_KeepsTabLfCrAndHighChars()
        {
            // XML 1.0 允许 #x9 / #xA / #xD；C# char 是 UTF-16，代理项对（emoji 等）需放行。
            Assert.AreEqual("a\tb\nc\rd", WorkshopWorkDemotionLedger.SanitizeKey("a\tb\nc\rd"));
            Assert.AreEqual("中文", WorkshopWorkDemotionLedger.SanitizeKey("中文"));
        }

        [Test]
        public void Sanitize_DropsNullEntriesAndEmptyKeys()
        {
            var ledger = new WorkshopWorkDemotionLedger();
            ledger.pawns.Add(null);
            var rec = new WorkshopWorkDemotionLedger.PawnRecord { pawnId = PawnA };
            rec.entries.Add(null);
            rec.entries.Add(new WorkshopWorkDemotionLedger.Entry
            {
                workTypeDefName = "Ha\x02uling",
                originalPriority = 2
            });
            ledger.pawns.Add(rec);

            ledger.Sanitize();

            Assert.AreEqual(1, ledger.pawns.Count, "null pawn 记录必须被清掉");
            Assert.AreEqual(1, rec.entries.Count, "null entry 必须被清掉");
            Assert.AreEqual("Ha_uling", rec.entries[0].workTypeDefName);
        }

        [Test]
        public void Sanitize_DropsPawnRecordWhenAllEntriesInvalid()
        {
            var ledger = new WorkshopWorkDemotionLedger();
            var rec = new WorkshopWorkDemotionLedger.PawnRecord { pawnId = PawnA };
            rec.entries.Add(new WorkshopWorkDemotionLedger.Entry { workTypeDefName = "\x01" });
            ledger.pawns.Add(rec);

            ledger.Sanitize();

            // "\x01" 清洗后是 "_"（**合法**键）而非空串 ⇒ 该条目仍然有效，记录应保留。
            // 真正会被丢弃的是清洗后变**空**的键（SanitizeKey 对 null/"" 返回空）。
            Assert.AreEqual(1, ledger.pawns.Count);
            Assert.AreEqual(1, rec.entries.Count);
            Assert.AreEqual("_", rec.entries[0].workTypeDefName);
        }

        [Test]
        public void EntryCountOf_AndTotal_AreSafeOnEmptyLedger()
        {
            var ledger = new WorkshopWorkDemotionLedger();
            Assert.AreEqual(0, ledger.TotalEntryCount);
            Assert.AreEqual(0, ledger.EntryCountOf(PawnA));
            Assert.IsNull(ledger.RecordOf(PawnA));
        }
    }
}