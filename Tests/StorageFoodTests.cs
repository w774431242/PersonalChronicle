using System;
using System.Linq;
using System.IO;
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
    [TestFixture]
    public sealed class StorageFoodTests
    {
        [Test] public void PublicFoodRequiresAFinalizableFundedPurchaseWhenEconomyIsRequested()
        {
            Assert.That(StorageFoodRules.CanConsume(StorageRightKind.Public, null, "A", false, true, true, 90, 90), Is.True);
            Assert.That(StorageFoodRules.CanConsume(StorageRightKind.Public, null, "A", false, true, true, 89, 90), Is.False);
            Assert.That(StorageFoodRules.CanConsume(StorageRightKind.Public, null, "A", false, true, false, 900, 90), Is.False);
            Assert.That(StorageFoodRules.CanConsume(StorageRightKind.Public, null, "A", false, true, true, 900, 0), Is.False);
            Assert.That(StorageFoodRules.CanConsume(StorageRightKind.Public, null, "A", true, true, true, 900, 90), Is.False);
        }
        [Test] public void PrivateFoodBelongsOnlyToItsOwnerEvenWithTheEconomyDisabled()
        {
            Assert.That(StorageFoodRules.CanConsume(StorageRightKind.Private, "A", "A", false, true, false, 0, 0), Is.True);
            Assert.That(StorageFoodRules.CanConsume(StorageRightKind.Private, "A", "B", false, false, true, 1000, 0), Is.False);
            Assert.That(StorageFoodRules.CanConsume(StorageRightKind.Private, null, "A", false, false, true, 1000, 0), Is.False);
            Assert.That(StorageFoodRules.CanConsume(StorageRightKind.Private, "A", "A", true, false, true, 1000, 0), Is.False);
        }
        [Test] public void OrdinaryStockAndDisabledPublicFeesKeepVanillaAccess()
        {
            Assert.That(StorageFoodRules.CanConsume(StorageRightKind.Unclassified, null, "A", false, true, false, 0, 0), Is.True);
            Assert.That(StorageFoodRules.CanConsume(StorageRightKind.Public, null, "A", false, false, false, 0, 0), Is.True);
            Assert.That(StorageFoodRules.CanConsume((StorageRightKind)99, null, "A", false, false, true, 1000, 1), Is.False);
        }
        [Test] public void StacksOfDifferentOwnersOrPendingPurchasesCannotMerge()
        {
            Assert.That(StorageFoodRules.CanMerge(StorageRightKind.Private, "A", "s", StorageRightKind.Private, "A", "t", false), Is.True);
            Assert.That(StorageFoodRules.CanMerge(StorageRightKind.Private, "A", "s", StorageRightKind.Private, "B", "t", false), Is.False);
            Assert.That(StorageFoodRules.CanMerge(StorageRightKind.Private, "A", "s", StorageRightKind.Unclassified, null, null, false), Is.False);
            Assert.That(StorageFoodRules.CanMerge(StorageRightKind.Private, "A", "s", StorageRightKind.Public, null, "s", false), Is.False);
            Assert.That(StorageFoodRules.CanMerge(StorageRightKind.Public, null, "s", StorageRightKind.Public, null, "t", false), Is.False);
            Assert.That(StorageFoodRules.CanMerge(StorageRightKind.Public, null, "s", StorageRightKind.Public, null, "s", true), Is.False);
        }
        [TestCase(.3f, .1f, 50, 0, 3)] // Double division before rounding would incorrectly buy four.
        [TestCase(.9f, .05f, 75, 0, 18)]
        [TestCase(.9f, .9f, 10, 0, 1)]
        [TestCase(2f, .9f, 10, 0, 3)]
        [TestCase(2f, .9f, 2, 0, 2)]
        [TestCase(2f, .9f, 10, 1, 1)]
        [TestCase(0f, .9f, 10, 0, 1)]
        public void PurchaseQuantityMatchesVanillaMealLimits(float wanted, float nutrition, int stack, int limit, int expected)
            => Assert.That(StorageFoodRules.UnitsForMeal(wanted, nutrition, stack, limit), Is.EqualTo(expected));
        [Test] public void InvalidOrEmptyPortionsCannotCreateAPurchase()
        {
            Assert.That(StorageFoodRules.UnitsForMeal(float.NaN, .9f, 10, 0), Is.Zero);
            Assert.That(StorageFoodRules.UnitsForMeal(.9f, float.NaN, 10, 0), Is.Zero);
            Assert.That(StorageFoodRules.UnitsForMeal(.9f, float.PositiveInfinity, 10, 0), Is.Zero);
            Assert.That(StorageFoodRules.UnitsForMeal(.9f, 0f, 10, 0), Is.Zero);
            Assert.That(StorageFoodRules.UnitsForMeal(.9f, .9f, 0, 0), Is.Zero);
        }
        [Test] public void SplitRetainsOwnerAndPendingTransactionWithoutChangingTheSource()
        {
            var component = new GameComponent_EconomyStorage(null);
            var source = new Thing { thingIDNumber = 101 };
            var child = new Thing { thingIDNumber = 102 };
            var right = new StoredItemRight { Item = source, Kind = StorageRightKind.Private, OwnerKey = "A",
                SourceKey = "s", PricePerNutritionMinor = 100, PendingCommand = "cmd", PendingBuyer = "A", PendingUnits = 1 };
            component.CopyRights(right, source, child);
            var copied = component.RightFor(child);
            Assert.That(copied.OwnerKey, Is.EqualTo("A"));
            Assert.That(copied.PendingCommand, Is.EqualTo("cmd"));
            copied.OwnerKey = "B";
            Assert.That(right.OwnerKey, Is.EqualTo("A"));
        }
        [Test] public void PublicIdentitySurvivesAnAbsorptionIntoUnclassifiedStock()
        {
            var component = new GameComponent_EconomyStorage(null);
            var result = new Thing { thingIDNumber = 103, stackCount = 5 };
            component.AfterMerge(result, new StoredItemRight { Kind = StorageRightKind.Public, SourceKey = "s", PricePerNutritionMinor = 200 }, 3);
            Assert.That(component.RightFor(result).Kind, Is.EqualTo(StorageRightKind.Public));
            Assert.That(component.RightFor(result).PricePerNutritionMinor, Is.EqualTo(200));
        }
        [Test] public void UnpaidCarriedPublicFoodCannotAbsorbAlreadyBoughtInventoryFood()
        {
            var component = new GameComponent_EconomyStorage(null);
            var unpaid = new Thing { thingIDNumber = 104 };
            var bought = new Thing { thingIDNumber = 105 };
            component.CopyRights(new StoredItemRight { Item = unpaid, Kind = StorageRightKind.Public, SourceKey = "s" }, unpaid, unpaid);
            Assert.That(component.CanMerge(unpaid, bought), Is.False);
            Assert.That(component.CanMerge(bought, unpaid), Is.False);
        }
    }
    [TestFixture, NonParallelizable]
    public sealed class StorageFoodHarmonyTests
    {
        private static GameComponent_EconomyStorage inventoryStorage;
        [Test] public void DeniedFirstInventoryFoodDoesNotHideTheOwnersLaterFood()
        {
            var harmony = new Harmony("personalchronicle.tests.storagefood.inventory");
            try
            {
                inventoryStorage = new GameComponent_EconomyStorage(null);
                harmony.Patch(AccessTools.PropertyGetter(typeof(GameComponent_EconomyStorage), nameof(GameComponent_EconomyStorage.Current)),
                    prefix: new HarmonyMethod(typeof(StorageFoodHarmonyTests), nameof(StorageForInventoryTest)));
                harmony.Patch(AccessTools.PropertyGetter(typeof(ThingDef), nameof(ThingDef.IsNutritionGivingIngestible)),
                    prefix: new HarmonyMethod(typeof(StorageFoodHarmonyTests), nameof(EligibleForInventoryTest)));
                harmony.Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.IngestibleNow)),
                    prefix: new HarmonyMethod(typeof(StorageFoodHarmonyTests), nameof(EligibleForInventoryTest)));
                harmony.Patch(AccessTools.Method(typeof(FoodUtility), nameof(FoodUtility.WillEat),
                    new[] { typeof(Pawn), typeof(Thing), typeof(Pawn), typeof(bool), typeof(bool) }),
                    prefix: new HarmonyMethod(typeof(StorageFoodHarmonyTests), nameof(EligibleForInventoryTest)));
                harmony.Patch(AccessTools.Method(typeof(FoodUtility), nameof(FoodUtility.NutritionForEater)),
                    prefix: new HarmonyMethod(typeof(StorageFoodHarmonyTests), nameof(NutritionForInventoryTest)));
                harmony.CreateClassProcessor(typeof(Patch_StoredFoodInventorySearch)).Patch();
                var pawnDef = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
                pawnDef.defName = "TestPawn";
                var pawn = new Pawn { thingIDNumber = 201, def = pawnDef };
                pawn.inventory = new Pawn_InventoryTracker(pawn);
                var foodDef = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
                foodDef.defName = "TestFood";
                foodDef.ingestible = new IngestibleProperties { preferability = FoodPreferability.MealSimple };
                var foreign = new Thing { thingIDNumber = 202, def = foodDef, stackCount = 1 };
                var owned = new Thing { thingIDNumber = 203, def = foodDef, stackCount = 1 };
                pawn.inventory.innerContainer.InnerListForReading.Add(foreign);
                pawn.inventory.innerContainer.InnerListForReading.Add(owned);
                inventoryStorage.CopyRights(new StoredItemRight { Kind = StorageRightKind.Private, OwnerKey = "OtherPawn" }, foreign, foreign);
                inventoryStorage.CopyRights(new StoredItemRight { Kind = StorageRightKind.Private, OwnerKey = pawn.GetUniqueLoadID() }, owned, owned);
                Assert.That(FoodUtility.BestFoodInInventory(pawn, null, FoodPreferability.NeverForNutrition, FoodPreferability.MealLavish, 0f, true, true), Is.SameAs(owned));
                Assert.That(FoodUtility.BestFoodInInventory(pawn, pawn, FoodPreferability.MealFine, FoodPreferability.MealLavish, 0f, true, true), Is.Null);
                Assert.That(FoodUtility.BestFoodInInventory(pawn, pawn, FoodPreferability.NeverForNutrition, FoodPreferability.MealLavish, 2f, true, true), Is.Null);
            }
            finally { harmony.UnpatchAll(harmony.Id); inventoryStorage = null; }
        }
        private static bool StorageForInventoryTest(ref GameComponent_EconomyStorage __result) { __result = inventoryStorage; return false; }
        private static bool EligibleForInventoryTest(ref bool __result) { __result = true; return false; }
        private static bool NutritionForInventoryTest(ref float __result) { __result = 1f; return false; }
        [Test] public void AllStorageAdaptersAndConsumptionCapturePatchTheRealGameSignatures()
        {
            var harmony = new Harmony("personalchronicle.tests.storagefood.adapters");
            var adapters = typeof(Patch_ThingIngested).Assembly.GetTypes()
                .Where(t => t.Namespace == typeof(Patch_ThingIngested).Namespace && t.Name.StartsWith("Patch_Stored", StringComparison.Ordinal)).ToArray();
            Assert.That(adapters.Length, Is.EqualTo(11));
            try
            {
                foreach (var adapter in adapters) Assert.That(() => harmony.CreateClassProcessor(adapter).Patch(), Throws.Nothing, adapter.Name);
                Assert.That(() => harmony.CreateClassProcessor(typeof(Patch_ThingIngested)).Patch(), Throws.Nothing);
            }
            finally { harmony.UnpatchAll(harmony.Id); }
        }
    }
    [TestFixture, NonParallelizable]
    public sealed class StorageFoodScribeTests
    {
        [Test] public void RealScribePreservesOwnershipQuoteAndPendingPurchaseFields()
        {
            var harmony = new Harmony("personalchronicle.tests.storagefood.scribe");
            string path = Path.Combine(Path.GetTempPath(), "pc_stored_food_" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                var skip = new HarmonyMethod(typeof(StorageFoodScribeTests).GetMethod(nameof(Skip), BindingFlags.NonPublic | BindingFlags.Static));
                foreach (string name in new[] { "Error", "Warning", "Message" })
                {
                    var method = AccessTools.Method(typeof(Log), name, new[] { typeof(string) });
                    if (method != null) harmony.Patch(method, prefix: skip);
                }
                harmony.Patch(AccessTools.Method(typeof(DeepProfiler), "Start", new[] { typeof(string) }), prefix: skip);
                harmony.Patch(AccessTools.Method(typeof(DeepProfiler), "End"), prefix: skip);
                var original = new StoredItemRight { Kind = StorageRightKind.Public, SourceKey = "public_1", OwnerKey = "A",
                    PricePerNutritionMinor = 200, PendingCommand = "purchase:1", PendingBuyer = "A", PendingUnits = 3 };
                Scribe.saver.InitSaving(path, "savegame"); Scribe_Deep.Look(ref original, "ownership"); Scribe.saver.FinalizeSaving();
                StoredItemRight loaded = null;
                Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "ownership"); Scribe.loader.FinalizeLoading();
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.Kind, Is.EqualTo(StorageRightKind.Public));
                Assert.That(loaded.SourceKey, Is.EqualTo("public_1"));
                Assert.That(loaded.OwnerKey, Is.EqualTo("A"));
                Assert.That(loaded.PricePerNutritionMinor, Is.EqualTo(200));
                Assert.That(loaded.PendingCommand, Is.EqualTo("purchase:1"));
                Assert.That(loaded.PendingBuyer, Is.EqualTo("A"));
                Assert.That(loaded.PendingUnits, Is.EqualTo(3));
            }
            finally { Scribe.ForceStop(); if (File.Exists(path)) File.Delete(path); harmony.UnpatchAll(harmony.Id); }
        }
        private static bool Skip() => false;
    }
}
