using NUnit.Framework;
using PersonalChronicle.Application.Effects;
using PersonalChronicle.Automation;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 车间成品加成登记表（<see cref="WorkshopProductRegistry"/>）离线单测
    /// —— C 域体检补的缺口（此前完全无测试）。
    ///
    /// <para>契约：① 无加成不登记（不占条目）；② 只登记提升的维度，另一维保持中性 1f；
    /// ③ <c>Clear</c> 清空；④ 超上限（512）整表清空后旧条目失效、新条目在表内（有界保护）。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopProductRegistryTests
    {
        private const int MaxEntries = 512;

        [SetUp]
        public void SetUp()
        {
            WorkshopProductRegistry.Clear();
        }

        [Test]
        public void Mark_WithoutBonus_IsNotRegistered()
        {
            WorkshopProductRegistry.Mark(1001, 1f, 1f);
            WorkshopProductBonus bonus;
            Assert.IsFalse(WorkshopProductRegistry.TryGet(1001, out bonus));
        }

        [Test]
        public void Mark_ThenTryGet_ReturnsBothFactors()
        {
            WorkshopProductRegistry.Mark(1002, 1.25f, 1.10f);
            WorkshopProductBonus bonus;
            Assert.IsTrue(WorkshopProductRegistry.TryGet(1002, out bonus));
            Assert.AreEqual(1.25f, bonus.maxHpFactor, 0.0001f);
            Assert.AreEqual(1.10f, bonus.accuracyFactor, 0.0001f);
        }

        [Test]
        public void Mark_HpOnly_LeavesAccuracyNeutral()
        {
            WorkshopProductRegistry.Mark(1003, 1.20f, 1f);
            WorkshopProductBonus bonus;
            Assert.IsTrue(WorkshopProductRegistry.TryGet(1003, out bonus));
            Assert.AreEqual(1.20f, bonus.maxHpFactor, 0.0001f);
            Assert.AreEqual(1f, bonus.accuracyFactor, 0.0001f);
        }

        [Test]
        public void TryGet_UnknownIdIsFalse()
        {
            WorkshopProductBonus bonus;
            Assert.IsFalse(WorkshopProductRegistry.TryGet(999999, out bonus));
        }

        [Test]
        public void Clear_RemovesEverything()
        {
            WorkshopProductRegistry.Mark(1004, 1.2f, 1f);
            WorkshopProductRegistry.Clear();
            WorkshopProductBonus bonus;
            Assert.IsFalse(WorkshopProductRegistry.TryGet(1004, out bonus));
        }

        [Test]
        public void OverCapacity_ClearsOldEntriesAndKeepsNewOne()
        {
            for (int i = 0; i < MaxEntries; i++)
            {
                WorkshopProductRegistry.Mark(2000 + i, 1.2f, 1f);
            }
            WorkshopProductBonus bonus;
            Assert.IsTrue(WorkshopProductRegistry.TryGet(2000, out bonus), "上限内应能取到");

            // 第 MaxEntries+1 条触发"整表清空"（有界保护）⇒ 旧条目全部失效，新条目在表内。
            WorkshopProductRegistry.Mark(9999, 1.2f, 1f);
            Assert.IsFalse(WorkshopProductRegistry.TryGet(2000, out bonus), "清表后旧条目应失效");
            Assert.IsTrue(WorkshopProductRegistry.TryGet(9999, out bonus), "清表后新条目应在表内");
        }
    }
}
