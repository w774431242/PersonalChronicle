using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Capture;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using Verse;

namespace PersonalChronicle.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class PublicStockpileTests
    {
        private Harmony harmony;
        private static GameComponent_EconomyStorage storage;
        private static Map map;
        private static Dictionary<Zone_Stockpile, List<Thing>> items;
        private Zone_Stockpile zone;
        private static Thing Item(int id) => new Thing { thingIDNumber = id, stackCount = 2,
            def = new ThingDefForTest().Value };
        private sealed class ThingDefForTest
        {
            internal readonly ThingDef Value = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            internal ThingDefForTest() { Value.category = ThingCategory.Item; }
        }
        [SetUp] public void Setup()
        {
            storage = new GameComponent_EconomyStorage(null);
            map = (Map)FormatterServices.GetUninitializedObject(typeof(Map));
            map.thingGrid = (ThingGrid)FormatterServices.GetUninitializedObject(typeof(ThingGrid));
            items = new Dictionary<Zone_Stockpile, List<Thing>>();
            zone = new Zone_Stockpile { ID = 901 };
            items[zone] = new List<Thing>();
            harmony = new Harmony("personalchronicle.tests.publicstockpile");
            harmony.Patch(AccessTools.PropertyGetter(typeof(GameComponent_EconomyStorage), "Current"),
                prefix: new HarmonyMethod(typeof(PublicStockpileTests), nameof(CurrentStorage)));
            harmony.Patch(AccessTools.PropertyGetter(typeof(Zone), "Map"),
                prefix: new HarmonyMethod(typeof(PublicStockpileTests), nameof(ZoneMap)));
            harmony.Patch(AccessTools.PropertyGetter(typeof(SlotGroup), "HeldThings"),
                prefix: new HarmonyMethod(typeof(PublicStockpileTests), nameof(HeldThings)));
        }
        [TearDown] public void Cleanup()
        {
            harmony.UnpatchAll(harmony.Id);
            storage = null; map = null; items = null;
        }
        private static bool CurrentStorage(ref GameComponent_EconomyStorage __result) { __result = storage; return false; }
        private static bool ZoneMap(ref Map __result) { __result = map; return false; }
        private static bool HeldThings(SlotGroup __instance, ref IEnumerable<Thing> __result)
        {
            __result = __instance.parent is Zone_Stockpile z && items.TryGetValue(z, out var list) ? list : Enumerable.Empty<Thing>();
            return false;
        }
        [Test] public void NewAndRecreatedZonesDefaultOffAndReadsDoNotAllocateSettings()
        {
            Assert.That(PublicStockpileSettings.Read(zone), Is.Null);
            var food = Item(901); items[zone].Add(food);
            storage.ReceivePublicStockpile(zone, food);
            Assert.That(storage.RightFor(food), Is.Null);
            Assert.That(PublicStockpileSettings.Read(zone), Is.Null);
            storage.SetPublicStockpile(zone, true);
            var replacement = new Zone_Stockpile { ID = zone.ID };
            Assert.That(PublicStockpileSettings.Read(replacement), Is.Null);
        }
        [Test] public void EnablingImmediatelyRegistersExistingStockAndDisablingPreservesItsRights()
        {
            var food = Item(902); items[zone].Add(food);
            storage.SetPublicStockpile(zone, true);
            var right = storage.RightFor(food);
            Assert.That(right.Kind, Is.EqualTo(StorageRightKind.Public));
            Assert.That(right.SourceKey, Is.EqualTo(zone.GetUniqueLoadID()));
            storage.SetPublicStockpile(zone, false);
            Assert.That(storage.RightFor(food), Is.SameAs(right));
            var later = Item(903);
            storage.ReceivePublicStockpile(zone, later);
            Assert.That(storage.RightFor(later), Is.Null);
        }
        [Test] public void PreviouslyPaidFoodKeepsItsOwnerAndIsNotChargedTwice()
        {
            var food = Item(904); items[zone].Add(food);
            storage.CopyRights(new StoredItemRight { Kind = StorageRightKind.Private, OwnerKey = "buyer" }, food, food);
            storage.SetPublicStockpile(zone, true);
            Assert.That(storage.RightFor(food).Kind, Is.EqualTo(StorageRightKind.Private));
            Assert.That(storage.RightFor(food).OwnerKey, Is.EqualTo("buyer"));
            Assert.That(storage.CanStorePublicStockpile(zone, food), Is.False);
            storage.SetPublicStockpile(zone, false);
            Assert.That(storage.CanStorePublicStockpile(zone, food), Is.True);
        }
        [Test] public void ReceiveAdapterRegistersNewArrivalsAndPreservesPendingDelivery()
        {
            harmony.CreateClassProcessor(typeof(Patch_PublicStockpileReceive)).Patch();
            storage.SetPublicStockpile(zone, true);
            var food = Item(905);
            zone.Notify_ReceivedThing(food);
            Assert.That(storage.RightFor(food).SourceKey, Is.EqualTo(zone.GetUniqueLoadID()));
            var pending = storage.RightFor(food);
            pending.PendingCommand = "payment";
            Assert.That(storage.CanStorePublicStockpile(zone, food), Is.False);
        }
        // CHG-20261012-ECON-CONTRACTIZE：`RequotingAffectsMovedStockButNeverUnrelatedZones` 已删除 ——
        // 它测的是"人工改价"（「设定食品售价」）对已搬动库存的传播语义，该功能已被用户裁决整体移除
        //（定价改为食物自身 MarketValue 派生，见 EconomyStorage.QuotedMealPrice / LowestFoodQuote）。
        [Test] public void RelocatingPublicStockAdoptsDestinationQuoteWhileSplitsKeepOwnership()
        {
            var food = Item(908);
            storage.ReceivePublicSource("old", 50, food);
            storage.SetPublicStockpile(zone, true);
            storage.ReceivePublicStockpile(zone, food);
            var split = Item(909);
            storage.CopyRights(storage.RightFor(food), food, split);
            Assert.That(storage.RightFor(split).SourceKey, Is.EqualTo(zone.GetUniqueLoadID()));
            Assert.That(storage.CanMerge(food, Item(910)), Is.False, "Carried unpaid stock cannot reclassify ordinary food.");
        }
        [Test] public void ActivationIgnoresPawnsBuildingsAndDestroyedStock()
        {
            var building = Item(911); building.def.category = ThingCategory.Building;
            Assert.That(storage.ReceivePublicSource("zone", 100, building), Is.Null);
            Assert.That(storage.ReceivePublicSource("zone", 100, null), Is.Null);
        }
        [Test] public void ShopProjectsActualPublicMapStockAndExcludesPrivatePendingAndSilver()
        {
            StubScribeLogging();
            harmony.Patch(AccessTools.PropertyGetter(typeof(Find), nameof(Find.Maps)),
                prefix: new HarmonyMethod(typeof(PublicStockpileTests), nameof(FixtureMaps)));
            map.listerThings = (ListerThings)FormatterServices.GetUninitializedObject(typeof(ListerThings));
            var lists = new List<Thing>[128];
            var food = Item(930); var shirt = Item(931); var privateGoods = Item(932); var pending = Item(933); var silver = Item(934);
            var originalSilver = ThingDefOf.Silver;
            try
            {
                ThingDefOf.Silver = silver.def;
                lists[(int)ThingRequestGroup.Everything] = new List<Thing> { food, shirt, privateGoods, pending, silver };
                AccessTools.Field(typeof(ListerThings), "listsByGroup").SetValue(map.listerThings, lists);
                foreach (var item in lists[(int)ThingRequestGroup.Everything])
                {
                    AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(item, (sbyte)0);
                    storage.ReceivePublicSource("warehouse", 0, item);
                }
                storage.AssignPurchasedItem(privateGoods, "buyer");
                storage.RightFor(pending).PendingCommand = "payment";
                var shop = new Building_EconomyInstitution();
                AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(shop, (sbyte)0);
                var counterItem = Item(935); shop.Stock.InnerListForReading.Add(counterItem);
                var economy = new GameComponent_PawnEconomy(null);
                var before = storage.RightFor(food);
                Assert.That(food.Spawned, Is.True, "offline item state");
                Assert.That(storage.IsColonyPublic(food), Is.True, "registered public item");
                Assert.That(map.listerThings.AllThings.Count, Is.EqualTo(5), "real all-things list");
                Assert.That(storage.PublicMapItems(map).ToArray(), Is.EquivalentTo(new[] { food, shirt, silver }), "storage projection");
                Assert.That(economy.ShopInventory(shop).ToArray(), Is.EquivalentTo(new[] { food, shirt, counterItem }));
                Assert.That(economy.QueryPendingPublicSilver(map), Is.EqualTo(2));
                Assert.That(economy.QueryPublicFundsTotal(), Is.Zero, "floor silver does not issue ledger money before a real deposit");
                Assert.That(storage.RightFor(food), Is.SameAs(before));
                Assert.That(storage.IsColonyPublic(privateGoods), Is.False);
                Assert.That(storage.IsColonyPublic(pending), Is.False);
            }
            finally { ThingDefOf.Silver = originalSilver; }
        }
        private static bool FixtureMaps(ref List<Map> __result) { __result = new List<Map> { map }; return false; }
        [Test] public void ApparelAccessProtectsThePaidOwnerAndBlocksUnresolvedDelivery()
        {
            var pawnDef = new ThingDefForTest().Value; pawnDef.defName = "Human";
            var wearer = new Pawn { def = pawnDef, thingIDNumber = 940 };
            var apparel = new Apparel { def = new ThingDefForTest().Value, thingIDNumber = 941 };
            var economy = new GameComponent_PawnEconomy(null);
            storage.AssignPurchasedItem(apparel, "OtherPawn");
            Assert.That(economy.BeforeStoredApparelWear(wearer, apparel), Is.False);
            Assert.That(economy.CanSelectStoredApparel(wearer, apparel), Is.False);
            storage.AssignPurchasedItem(apparel, wearer.GetUniqueLoadID());
            Assert.That(economy.BeforeStoredApparelWear(wearer, apparel), Is.True);
            storage.RightFor(apparel).PendingCommand = "pending";
            Assert.That(economy.BeforeStoredApparelWear(wearer, apparel), Is.False);
        }
        [Test] public void AllSixAdaptersPatchTheInstalledGamesRealSignatures()
        {
            var adapters = typeof(Patch_PublicStockpileSave).Assembly.GetTypes()
                .Where(t => t.Namespace == typeof(Patch_PublicStockpileSave).Namespace && t.Name.StartsWith("Patch_PublicStockpile")).ToArray();
            Assert.That(adapters.Length, Is.EqualTo(6));
            foreach (var adapter in adapters) Assert.That(() => harmony.CreateClassProcessor(adapter).Patch(), Throws.Nothing, adapter.Name);
        }
        [TestCase(true)] [TestCase(false)]
        public void RealScribeRestoresTheToggleAndPriceOnTheActualZone(bool enabled)
        {
            string path = Path.Combine(Path.GetTempPath(), "pc_public_zone_" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                StubScribeLogging();
                var settings = PublicStockpileSettings.Edit(zone);
                settings.Enabled = enabled; settings.PricePerNutritionMinor = 500;
                Scribe.saver.InitSaving(path, "savegame"); PublicStockpileSettings.Expose(zone); Scribe.saver.FinalizeSaving();
                var restored = new Zone_Stockpile { ID = zone.ID };
                Scribe.loader.InitLoading(path); PublicStockpileSettings.Expose(restored); Scribe.loader.FinalizeLoading();
                Assert.That(PublicStockpileSettings.Read(restored).Enabled, Is.EqualTo(enabled));
                Assert.That(PublicStockpileSettings.Read(restored).PricePerNutritionMinor, Is.EqualTo(500));
                Assert.That(PublicStockpileSettings.Read(restored), Is.Not.SameAs(settings));
            }
            finally { Scribe.ForceStop(); if (File.Exists(path)) File.Delete(path); }
        }
        [Test] public void OldSaveWithoutPublicFieldsKeepsVanillaStorage()
        {
            string path = Path.Combine(Path.GetTempPath(), "pc_old_zone_" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                StubScribeLogging();
                File.WriteAllText(path, "<savegame />");
                Scribe.loader.InitLoading(path); PublicStockpileSettings.Expose(zone); Scribe.loader.FinalizeLoading();
                Assert.That(PublicStockpileSettings.Read(zone), Is.Null);
            }
            finally { Scribe.ForceStop(); if (File.Exists(path)) File.Delete(path); }
        }
        private void StubScribeLogging()
        {
            var skip = new HarmonyMethod(typeof(PublicStockpileTests), nameof(Skip));
            foreach (string name in new[] { "Error", "Warning", "Message" })
                harmony.Patch(AccessTools.Method(typeof(Log), name, new[] { typeof(string) }), prefix: skip);
            harmony.Patch(AccessTools.Method(typeof(DeepProfiler), "Start", new[] { typeof(string) }), prefix: skip);
            harmony.Patch(AccessTools.Method(typeof(DeepProfiler), "End"), prefix: skip);
        }
        private static bool Skip() => false;
        [Test] public void ExpansionAdapterRegistersExistingItemsInTheAddedCellOnlyWhenEnabled()
        {
            harmony.Patch(AccessTools.Method(typeof(Zone_Stockpile), "AddCell"),
                prefix: new HarmonyMethod(typeof(PublicStockpileTests), nameof(Skip)));
            harmony.Patch(AccessTools.Method(typeof(ThingGrid), "ThingsListAt", new[] { typeof(IntVec3) }),
                prefix: new HarmonyMethod(typeof(PublicStockpileTests), nameof(CellThings)));
            harmony.CreateClassProcessor(typeof(Patch_PublicStockpileExpand)).Patch();
            storage.SetPublicStockpile(zone, true);
            var cell = new IntVec3(2, 0, 2);
            zone.Cells.Add(cell);
            var food = Item(912); items[zone].Add(food);
            zone.AddCell(cell);
            Assert.That(storage.RightFor(food).Kind, Is.EqualTo(StorageRightKind.Public));
            storage.SetPublicStockpile(zone, false);
            var later = Item(913); items[zone].Add(later);
            zone.AddCell(cell);
            Assert.That(storage.RightFor(later), Is.Null);
        }
        private static bool CellThings(ref List<Thing> __result) { __result = items.Values.First(); return false; }
    }
}
