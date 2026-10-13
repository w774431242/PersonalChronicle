using System;
using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 稳定降序排序（<see cref="StableOrder"/>）离线单测 —— B 域体检补的缺口。
    ///
    /// <para>契约：同分元素**保持原有相对次序**（这是班组"同优先级按定义顺序"承诺的实现基础；
    /// <c>List&lt;T&gt;.Sort</c> 是不稳定排序，做不到这一点）。</para>
    /// </summary>
    [TestFixture]
    public sealed class StableOrderTests
    {
        private sealed class Item
        {
            public string Name;
            public int Key;
        }

        private static List<Item> Items(params string[] namesAndKeys)
        {
            var list = new List<Item>();
            for (int i = 0; i + 1 < namesAndKeys.Length; i += 2)
            {
                list.Add(new Item { Name = namesAndKeys[i], Key = int.Parse(namesAndKeys[i + 1]) });
            }
            return list;
        }

        [Test]
        public void Sort_OrdersByKeyDescending()
        {
            List<int> list = new List<int> { 1, 5, 3, 5 };
            StableOrder.SortByDescending(list, x => x);
            CollectionAssert.AreEqual(new[] { 5, 5, 3, 1 }, list);
        }

        [Test]
        public void Sort_EqualKeys_KeepOriginalRelativeOrder()
        {
            List<Item> list = Items("a", "1", "b", "1", "c", "0", "d", "1", "e", "2");
            StableOrder.SortByDescending(list, i => i.Key);
            CollectionAssert.AreEqual(new[] { "e", "a", "b", "d", "c" },
                list.ConvertAll(i => i.Name));
        }

        [Test]
        public void Sort_AllKeysEqual_IsIdentity()
        {
            List<Item> list = Items("a", "7", "b", "7", "c", "7", "d", "7");
            StableOrder.SortByDescending(list, i => i.Key);
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, list.ConvertAll(i => i.Name));
        }

        [Test]
        public void Sort_NullOrTooShortList_IsNoOp()
        {
            StableOrder.SortByDescending<Item>(null, i => i.Key);
            var empty = new List<Item>();
            StableOrder.SortByDescending(empty, i => i.Key);
            List<Item> single = Items("only", "3");
            StableOrder.SortByDescending(single, i => i.Key);
            Assert.AreEqual(0, empty.Count);
            Assert.AreEqual("only", single[0].Name);
        }

        [Test]
        public void Sort_NullKeySelector_IsNoOp()
        {
            var list = new List<int> { 1, 3, 2 };
            StableOrder.SortByDescending(list, (Func<int, int>)null);
            CollectionAssert.AreEqual(new[] { 1, 3, 2 }, list);
        }

        [Test]
        public void Sort_NegativeKeys_StillDescending()
        {
            // 技能评分可能为 -1（技能不可用），必须排在 0 之后而不是被丢弃。
            List<int> list = new List<int> { -1, 0, -1, 5 };
            StableOrder.SortByDescending(list, x => x);
            CollectionAssert.AreEqual(new[] { 5, 0, -1, -1 }, list);
        }
    }
}
