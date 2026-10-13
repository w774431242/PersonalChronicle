using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// CHG-20261005-NODEMOTE-AUDIT：隐藏名单**分账**后的并集去重计数（纯逻辑，离线可跑）。
    ///
    /// <para><b>为什么必须测</b>：深度审计发现一个 P1 —— 分账后「仅自动隐藏」是**常态**，
    /// 而设置页「恢复显示全部殖民者」入口沿用了"只数手动集合"的计数 ⇒ 该常态下计数恒为 0
    /// ⇒ 按钮不渲染、还显示"无隐藏成员"，**逃生舱形同虚设**。
    /// 本组用例把"并集去重"这一算法钉死（含同一人同时出现在两集合的去重场景）。</para>
    ///
    /// <para>被测对象是 <see cref="WorkTabHideFilter.CountUnion"/>（纯函数、不触引擎），
    /// 与 <c>ChronicleGameComponent.HiddenWorkTabCount</c> 是同一实现的转调关系（GOV-005）。</para>
    /// </summary>
    [TestFixture]
    public class WorkTabHideFilterTests
    {
        private static HashSet<string> Set(params string[] ids)
        {
            return new HashSet<string>(ids);
        }

        [Test]
        public void CountUnion_NullOrEmpty_ReturnsZero()
        {
            Assert.AreEqual(0, WorkTabHideFilter.CountUnion(null, null));
            Assert.AreEqual(0, WorkTabHideFilter.CountUnion(Set(), Set()));
            Assert.AreEqual(0, WorkTabHideFilter.CountUnion(Set(), null));
        }

        [Test]
        public void CountUnion_AutoOnly_IsCounted()
        {
            // 分账后的**常态**：只有 PC 自动隐藏。
            // 旧实现（只数手动集合）在此恒返 0 ⇒ 恢复入口不渲染 —— 本用例即防该回归。
            Assert.AreEqual(2, WorkTabHideFilter.CountUnion(Set(), Set("A", "B")));
        }

        [Test]
        public void CountUnion_ManualOnly_IsCounted()
        {
            Assert.AreEqual(2, WorkTabHideFilter.CountUnion(Set("A", "B"), Set()));
        }

        [Test]
        public void CountUnion_Overlap_IsCountedOnce()
        {
            // 同一人可能同时在两集合（旧档项归入手动集、之后又因加入车间被自动隐藏）
            // ⇒ 必须去重：并集 = {A, B, C} = **3**，而不是"2 + 2 = 4"。
            Assert.AreEqual(3, WorkTabHideFilter.CountUnion(Set("A", "B"), Set("B", "C")));
            // 完全重合时必须仍是 1（最容易被"相加"写法算成 2）。
            Assert.AreEqual(1, WorkTabHideFilter.CountUnion(Set("A"), Set("A")));
        }

        [Test]
        public void CountUnion_NullManualWithAuto_IsCounted()
        {
            Assert.AreEqual(1, WorkTabHideFilter.CountUnion(null, Set("A")));
        }

        [Test]
        public void ShouldHide_EmptyOrNullSet_ReturnsFalse()
        {
            // 调用方据此早退（工作面板每次重算 × 每殖民者 ⇒ 必须零分配短路）。
            Assert.IsFalse(WorkTabHideFilter.ShouldHide("A", null));
            Assert.IsFalse(WorkTabHideFilter.ShouldHide("A", Set()));
            Assert.IsFalse(WorkTabHideFilter.ShouldHide(null, Set("A")));
            Assert.IsTrue(WorkTabHideFilter.ShouldHide("A", Set("A")));
        }
    }
}
