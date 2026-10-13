using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 总控制中心注册表分区排序的离线单测。
    ///
    /// <para>只测 <see cref="SystemCenterRegistry.Partition"/> 这一纯函数（不触碰
    /// DefDatabase）：分区与排序规则是「同一 tick 谁先执行」的显示口径，
    /// 一旦漂移会表现为侧栏/卡片顺序随载入变化，属于可复现 bug，值得钉死。</para>
    /// </summary>
    [TestFixture]
    public sealed class SystemCenterRegistryTests
    {
        private static SystemCenterDef Make(string defName, int tier, bool reserved)
        {
            return new SystemCenterDef
            {
                defName = defName,
                label = defName,
                priorityTier = tier,
                reserved = reserved
            };
        }

        [Test]
        public void Partition_SplitsReserved_AndSortsByTier()
        {
            List<SystemCenterDef> source = new List<SystemCenterDef>
            {
                Make("beta", 20, false),
                Make("alpha", 10, false),
                Make("reserved_b", 25, true),
                Make("reserved_a", 5, true)
            };
            List<SystemCenterDef> all = new List<SystemCenterDef>();
            List<SystemCenterDef> initial = new List<SystemCenterDef>();
            List<SystemCenterDef> reserved = new List<SystemCenterDef>();

            SystemCenterRegistry.Partition(source, all, initial, reserved);

            Assert.AreEqual(4, all.Count, "all 应包含全部中心");
            Assert.AreEqual(2, initial.Count, "initial 只收 reserved=false");
            Assert.AreEqual(2, reserved.Count, "reserved 只收 reserved=true");

            Assert.AreEqual("alpha", initial[0].defName, "初始中心按档位升序");
            Assert.AreEqual("beta", initial[1].defName);
            Assert.AreEqual("reserved_a", reserved[0].defName, "预留中心同样按档位升序");
            Assert.AreEqual("reserved_b", reserved[1].defName);
            Assert.AreEqual("reserved_a", all[0].defName, "all 为全局档位升序（预留也参与）");
        }

        [Test]
        public void Partition_SameTier_SortsByDefName()
        {
            List<SystemCenterDef> source = new List<SystemCenterDef>
            {
                Make("zulu", 15, false),
                Make("alpha", 15, false),
                Make("mike", 15, false)
            };
            List<SystemCenterDef> all = new List<SystemCenterDef>();
            List<SystemCenterDef> initial = new List<SystemCenterDef>();
            List<SystemCenterDef> reserved = new List<SystemCenterDef>();

            SystemCenterRegistry.Partition(source, all, initial, reserved);

            Assert.AreEqual("alpha", all[0].defName);
            Assert.AreEqual("mike", all[1].defName);
            Assert.AreEqual("zulu", all[2].defName);
        }

        [Test]
        public void Partition_NullAndNullEntries_AreTolerated()
        {
            List<SystemCenterDef> source = new List<SystemCenterDef>
            {
                null,
                Make("only", 1, false),
                null
            };
            List<SystemCenterDef> all = new List<SystemCenterDef>();
            List<SystemCenterDef> initial = new List<SystemCenterDef>();
            List<SystemCenterDef> reserved = new List<SystemCenterDef>();

            SystemCenterRegistry.Partition(source, all, initial, reserved);

            Assert.AreEqual(1, all.Count);
            Assert.AreEqual(1, initial.Count);
            Assert.AreEqual("only", initial[0].defName);

            SystemCenterRegistry.Partition(null, all, initial, reserved);
            Assert.AreEqual(0, all.Count, "null 源应清空输出而不是抛异常");
            Assert.AreEqual(0, initial.Count);
            Assert.AreEqual(0, reserved.Count);
        }

        [Test]
        public void Partition_DoesNotMutateSourceOrder()
        {
            List<SystemCenterDef> source = new List<SystemCenterDef>
            {
                Make("beta", 20, false),
                Make("alpha", 10, false)
            };
            List<SystemCenterDef> all = new List<SystemCenterDef>();
            List<SystemCenterDef> initial = new List<SystemCenterDef>();
            List<SystemCenterDef> reserved = new List<SystemCenterDef>();

            SystemCenterRegistry.Partition(source, all, initial, reserved);

            Assert.AreEqual("beta", source[0].defName, "不得就地排序 DefDatabase 源列表");
            Assert.AreEqual("alpha", source[1].defName);
        }
    }
}
