using NUnit.Framework;
using PersonalChronicle.Domain;
using RimWorld;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// <see cref="ItemFlowLedger"/>（`REQ-MVS-013` 物品级产消账本）离线纯逻辑测试：
    /// 4 季滚动的不变量（槽互异 / 最老槽清零 / 超窗全清）· 日均的覆盖天数口径 ·
    /// **读写双路径对齐**（闲置后读数必须归零，而不是"停在过去"—— ARCH 评审 B-02）·
    /// 产/消两族互不串扰 · 非法输入不建立账本。
    /// </summary>
    [TestFixture]
    public class ItemFlowLedgerTests
    {
        /// <summary>每季 tick（与 <see cref="GenDate.TicksPerQuadrum"/> 的一致性由专门用例守护）。</summary>
        private const long Q = 900000L;

        /// <summary>每天 tick。</summary>
        private const long D = 60000L;

        [Test]
        public void GameConstants_MatchLedgerExpectations()
        {
            // 口径公式把"季"写死为 GenDate.TicksPerQuadrum：不一致会让窗口边界整体错位。
            Assert.AreEqual(Q, (long)GenDate.TicksPerQuadrum);
            Assert.AreEqual(D, (long)GenDate.TicksPerDay);
        }

        // ── 流向记账（REQ-MVS-018，2026-09-20：为桑基流向图提供真实份额）────────────

        [Test]
        public void MaterialFlows_NoRecord_ReturnsFalse_NotZero()
        {
            ItemFlowLedger ledger = new ItemFlowLedger();
            var result = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>();
            Assert.IsFalse(ledger.TryMaterialFlows("Steel", 10 * Q, result),
                "无流向记录 ⇒ false（**不等于“无流向”** —— 只覆盖配方消耗路径，调用方不得当作 0）");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void MaterialFlows_AccumulateAndSortDescending()
        {
            ItemFlowLedger ledger = new ItemFlowLedger();
            ledger.RecordMaterialFlow("Steel", "Gun", 5, 0L);
            ledger.RecordMaterialFlow("Steel", "Gun", 3, 0L);
            ledger.RecordMaterialFlow("Steel", "Armor", 20, 0L);

            var result = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>();
            Assert.IsTrue(ledger.TryMaterialFlows("Steel", 1 * Q, result));
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("Armor", result[0].Key, "必须按件数降序（主要流向在前）");
            Assert.AreEqual(20, result[0].Value);
            Assert.AreEqual("Gun", result[1].Key);
            Assert.AreEqual(8, result[1].Value, "同一 material→product 多次记录必须累加");

            // 前缀匹配不得误命中别的 material（键是 material + 分隔符 + product）
            Assert.IsFalse(ledger.TryMaterialFlows("Stee", 1 * Q, result));
        }

        [Test]
        public void MaterialFlows_RotateOut_WithQuadrumWindow()
        {
            ItemFlowLedger ledger = new ItemFlowLedger();
            ledger.RecordMaterialFlow("Steel", "Gun", 7, 0L);
            var result = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>();
            Assert.IsTrue(ledger.TryMaterialFlows("Steel", 0L, result));

            // 跨 4 季 ⇒ 全清（与产消两族槽同规：三族窗口必须同步滚动，否则读数"停在过去"）
            Assert.IsFalse(ledger.TryMaterialFlows("Steel", 5 * Q, result),
                "滚出窗口的流向必须归零");
        }

        [Test]
        public void FreshLedger_ReportsNoRate()
        {
            ItemFlowLedger ledger = new ItemFlowLedger();
            float rate;
            bool ok = ledger.TryRatePerDay("Steel", true, 10 * Q, out rate);

            Assert.IsFalse(ok, "账本未建立 ⇒ 无读数（false，而不是 0）");
            Assert.AreEqual(0f, rate, 0.0001f);
            Assert.AreEqual(-1L, ledger.QuadrumAnchor);
            Assert.AreEqual(-1L, ledger.StartTick);
        }

        [Test]
        public void Record_EstablishesLedger_AndAccumulates()
        {
            ItemFlowLedger ledger = new ItemFlowLedger();
            ledger.Record("Steel", 10, 3 * Q + 2 * D, true);
            ledger.Record("Steel", 5, 3 * Q + 4 * D, true);

            Assert.AreEqual(3L, ledger.QuadrumAnchor, "记录应把锚点推进到当前季");
            Assert.AreEqual(3 * Q + 2 * D, ledger.StartTick, "StartTick = 首次记录时刻");

            float rate;
            // 读取时刻 = 同一季第 4 天：覆盖天数 = min(4 + 15, 账本年龄 2 天) = 2 ⇒ 15/2 = 7.5
            bool ok = ledger.TryRatePerDay("Steel", true, 3 * Q + 4 * D, out rate);
            Assert.IsTrue(ok);
            Assert.AreEqual(7.5f, rate, 0.0001f, "分母必须是实际覆盖天数（账本年龄封顶），不是固定 30");
        }

        [Test]
        public void Rate_DividesByLedgerAge_NotByFixedWindow()
        {
            ItemFlowLedger ledger = new ItemFlowLedger();
            // 账本建立 10 天前，期间共 20 件 ⇒ 年龄封顶下应为 2/日（而非 20/30 = 0.67）。
            long start = 5 * Q + 3 * D;
            ledger.Record("Steel", 20, start, true);

            float rate;
            bool ok = ledger.TryRatePerDay("Steel", true, start + 10 * D, out rate);
            Assert.IsTrue(ok);
            Assert.AreEqual(2f, rate, 0.0001f);
        }

        [Test]
        public void TwoQuadrumWindow_SumsCurrentAndPrevious()
        {
            ItemFlowLedger ledger = new ItemFlowLedger();
            // 季 7 记 15 件；跨到季 8 再记 15 件 ⇒ 近两季窗口合计 30。
            ledger.Record("Steel", 15, 7 * Q, true);
            ledger.Record("Steel", 15, 8 * Q, true);

            float rate;
            // 读取时刻 = 季 8 第 0 天：覆盖 = min(0 + 15, 年龄 = 180 天) = 15 ⇒ 30/15 = 2
            bool ok = ledger.TryRatePerDay("Steel", true, 8 * Q, out rate);
            Assert.IsTrue(ok);
            Assert.AreEqual(2f, rate, 0.0001f, "近两季（≈30 天窗口）的件数都应计入");
        }

        [Test]
        public void ReadPath_AlignsAcrossIdleQuadrums_OldFlowDropsOut()
        {
            ItemFlowLedger ledger = new ItemFlowLedger();
            ledger.Record("Steel", 100, 2 * Q + 3 * D, true);

            float rate;
            // 闲置 5 季后再读：读取路径也必须对齐（否则把 5 季前的 100 件当"近期"画出来）。
            bool ok = ledger.TryRatePerDay("Steel", true, 7 * Q + 3 * D, out rate);
            Assert.IsTrue(ok, "账本已建立 ⇒ 有读数");
            Assert.AreEqual(0f, rate, 0.0001f, "跨 ≥4 季 ⇒ 数据滚出窗口，读数必须归零（不是停在过去）");
            Assert.AreEqual(7L, ledger.QuadrumAnchor, "读取路径也要推进锚点");
        }

        [Test]
        public void Advance_BeyondWindow_ClearsAll()
        {
            ItemFlowLedger ledger = new ItemFlowLedger();
            ledger.Record("Steel", 42, 0L, true);
            ledger.Advance(20L);

            float rate;
            bool ok = ledger.TryRatePerDay("Steel", true, 20 * Q + D, out rate);
            Assert.IsTrue(ok);
            Assert.AreEqual(0f, rate, 0.0001f, "超出 4 季窗口的数据必须清零");
        }

        [Test]
        public void ProductionAndConsumption_AreIndependent()
        {
            ItemFlowLedger ledger = new ItemFlowLedger();
            ledger.Record("Steel", 30, 0L, true);
            ledger.Record("Steel", 12, 0L, false);

            float production;
            float consumption;
            Assert.IsTrue(ledger.TryRatePerDay("Steel", true, 0L, out production));
            Assert.IsTrue(ledger.TryRatePerDay("Steel", false, 0L, out consumption));
            Assert.AreEqual(30f, production, 0.0001f, "同刻读取 ⇒ 分母下界 1 天");
            Assert.AreEqual(12f, consumption, 0.0001f, "生产与消费必须各自独立累加，不得串扰");
        }

        [Test]
        public void EstablisedLedger_CanReportZero_AsTrue()
        {
            ItemFlowLedger ledger = new ItemFlowLedger();
            ledger.Record("Steel", 5, 0L, true);

            float rate;
            // 另一物品从未出现：账本已建立 ⇒ true（真值 0），调用方据此画 0 而不是 `—`。
            bool ok = ledger.TryRatePerDay("Silver", true, D, out rate);
            Assert.IsTrue(ok, "账本建立后，未出现过的物品读数是 0（真值），不是 false");
            Assert.AreEqual(0f, rate, 0.0001f);
        }

        [Test]
        public void InvalidInputs_AreIgnored_AndDoNotEstablishLedger()
        {
            ItemFlowLedger ledger = new ItemFlowLedger();
            ledger.Record(null, 5, 0L, true);
            ledger.Record(string.Empty, 5, 0L, true);
            ledger.Record("Steel", 0, 0L, true);
            ledger.Record("Steel", -3, 0L, true);

            Assert.AreEqual(-1L, ledger.StartTick, "非法输入不得建立账本");
            float rate;
            Assert.IsFalse(ledger.TryRatePerDay("Steel", true, 0L, out rate));
        }

        [Test]
        public void SlotKeys_AreBoundedByMaxKeysPerSlot()
        {
            ItemFlowLedger ledger = new ItemFlowLedger();
            for (int i = 0; i < ItemFlowLedger.MaxKeysPerSlot + 5; i++)
            {
                ledger.Record("item_" + i, 1, 0L, true);
            }

            // 超限后的新键被拒绝（已存在键照常累加）—— 可观测证据：重记一个已存在的键仍生效。
            ledger.Record("item_0", 7, 0L, true);
            float rate;
            Assert.IsTrue(ledger.TryRatePerDay("item_0", true, 0L, out rate));
            Assert.AreEqual(8f, rate, 0.0001f, "已有键在槽满后仍可累加（有界只拒绝新键）");
        }
    }
}
