using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Economy.PawnEconomy;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// CHG-20261011-WORKCAT（S1）：原版**工作识别注册表**的离线断言。
    ///
    /// <para>锁定三件事：①覆盖的就是"Orders 菜单里那 8 张单"（不多不少）
    /// ②**接线状态如实**（S3 八项完成证据已接入 —— 若某天某人悄悄把
    /// <c>Wired</c> 改成 true 而完成信号并未接入，玩家会"干完活拿不到钱"且毫无提示，这条断言即拦它）
    /// ③按工作类型归并的组正确（"已派发 / 可派发"只有在这一级才精确）。</para>
    /// </summary>
    [TestFixture]
    public class VanillaOrderCatalogueTests
    {
        [Test] public void S1_CatalogueCoversTheEightVanillaOrders()
        {
            var keys = new List<string>();
            for (int i = 0; i < VanillaOrderCatalogue.Orders.Length; i++) keys.Add(VanillaOrderCatalogue.Orders[i].Key);
            Assert.That(VanillaOrderCatalogue.Orders.Length, Is.EqualTo(8), "Orders 菜单共 8 张单");
            foreach (string expected in new[] { "Mine", "ChopWood", "CutPlants", "Harvest", "Hunt", "Slaughter", "Tame", "Haul" })
                Assert.That(keys, Does.Contain(expected), "缺订单：" + expected);
            Assert.That(new List<string>(new HashSet<string>(keys)).Count, Is.EqualTo(keys.Count), "订单 Key 必须唯一");
        }

        [Test] public void S1_WiredFlagsMatchTheRealCompletionHooks()
        {
            // S3: PlantCollected, death/execution, successful DoRecruit and actual hauling delivery.
            var wired = new List<string>();
            for (int i = 0; i < VanillaOrderCatalogue.Orders.Length; i++)
            {
                var order = VanillaOrderCatalogue.Orders[i];
                Assert.That(string.IsNullOrEmpty(order.WorkTypeDefName), Is.False, "订单必须声明工作类型：" + order.Key);
                if (order.Wired) wired.Add(order.Key);
            }
            Assert.That(wired, Is.EquivalentTo(new[] { "Mine", "ChopWood", "CutPlants", "Harvest", "Hunt", "Slaughter", "Tame", "Haul" }),
                "接线状态必须与真实完成信号一致（改 Wired 前先接线，否则玩家会干完活拿不到钱）");
        }

        [Test] public void S1_SingleDesignationOrdersDeclareTheirDefName()
        {
            for (int i = 0; i < VanillaOrderCatalogue.Orders.Length; i++)
            {
                var order = VanillaOrderCatalogue.Orders[i];
                if (order.Backlog != OrderBacklogKind.SingleDesignation) continue;
                Assert.That(string.IsNullOrEmpty(order.DesignationDefName), Is.False,
                    "单指定口径必须给出 defName：" + order.Key);
            }
        }

        [Test] public void S1_GroupsMergeAdjacentSameWorkTypeOrders()
        {
            var groups = VanillaOrderCatalogue.Groups;
            Assert.That(groups.Length, Is.EqualTo(5), "5 个玩家订单工种：Mining / PlantCutting / Hunting / Handling / Hauling");
            var byType = new Dictionary<string, VanillaOrderGroup>();
            for (int i = 0; i < groups.Length; i++)
            {
                Assert.That(byType.ContainsKey(groups[i].WorkTypeDefName), Is.False, "同组不得重复：" + groups[i].WorkTypeDefName);
                byType[groups[i].WorkTypeDefName] = groups[i];
            }
            Assert.That(byType["PlantCutting"].Orders.Length, Is.EqualTo(3), "伐木 + 割除 + 玩家指定收获同属 PlantCutting");
            Assert.That(byType["Handling"].Orders.Length, Is.EqualTo(2), "宰杀 + 驯服同属 Handling");
            Assert.That(byType["PlantCutting"].Wired, Is.True, "PlantCutting 两项都已接线");
            Assert.That(byType["Handling"].Wired, Is.True);
            Assert.That(byType.ContainsKey("Growing"), Is.False, "Growing 自动收获不得伪装成玩家订单");
            Assert.That(byType["Hauling"].Wired, Is.True);
        }
    }
}
