using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using Verse;

namespace PersonalChronicle.Tests
{
    /// <summary>Real ThingMaker/SplitOff/ThingOwner transfers; only engine logging, profiler and host IDs/maps are supplied.</summary>
    [TestFixture, NonParallelizable]
    public sealed class LaborPhysicalTransferTests
    {
        private sealed class Holder : IThingHolder
        {
            internal readonly ThingOwner<Thing> Items;
            public Holder() { Items = new ThingOwner<Thing>(this, false, LookMode.Deep); }
            public IThingHolder ParentHolder => null;
            public ThingOwner GetDirectlyHeldThings() => Items;
            public void GetChildHolders(List<IThingHolder> outChildren) { }
        }

        private Harmony host;
        private static UniqueIDsManager ids;
        private static readonly List<Map> maps = new List<Map>();
        private static readonly List<string> errors = new List<string>();
        private ThingDef itemDef;

        [OneTimeSetUp]
        public void Start()
        {
            host = new Harmony("personalchronicle.tests.laborphysicaltransfer");
            foreach (string name in new[] { "Error", "Warning", "Message" })
                host.Patch(AccessTools.Method(typeof(Log), name, new[] { typeof(string) }),
                    prefix: new HarmonyMethod(typeof(LaborPhysicalTransferTests), nameof(CaptureLog)));
            host.Patch(AccessTools.Method(typeof(DeepProfiler), "Start", new[] { typeof(string) }),
                prefix: new HarmonyMethod(typeof(LaborPhysicalTransferTests), nameof(Skip)));
            host.Patch(AccessTools.Method(typeof(DeepProfiler), "End"),
                prefix: new HarmonyMethod(typeof(LaborPhysicalTransferTests), nameof(Skip)));
            ids = new UniqueIDsManager();
            host.Patch(AccessTools.PropertyGetter(typeof(Find), nameof(Find.UniqueIDsManager)),
                prefix: new HarmonyMethod(typeof(LaborPhysicalTransferTests), nameof(ReadIds)));
            host.Patch(AccessTools.PropertyGetter(typeof(Find), nameof(Find.Maps)),
                prefix: new HarmonyMethod(typeof(LaborPhysicalTransferTests), nameof(ReadMaps)));
            // BuildableDef initializes textures in its constructor. A minimal Def avoids that Unity dependency;
            // the actual Thing and all container/transfer/splitting methods remain original and unpatched.
            itemDef = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            itemDef.defName = "PC_LaborPhysicalTransfer_Item";
            itemDef.category = ThingCategory.Item;
            itemDef.thingClass = typeof(Thing);
            itemDef.stackLimit = 75;
            itemDef.destroyable = true;
        }
        private static bool Skip() => false;
        private static bool CaptureLog(string text, MethodBase __originalMethod)
        { if (__originalMethod.Name == "Error") errors.Add(text); return false; }
        private static bool ReadIds(ref UniqueIDsManager __result) { __result = ids; return false; }
        private static bool ReadMaps(ref List<Map> __result) { __result = maps; return false; }
        [SetUp] public void SetUp() { errors.Clear(); }
        [TearDown] public void TearDown() { Assert.That(errors, Is.Empty, "Logging shims must not conceal a failed physical transfer."); }
        [OneTimeTearDown] public void Stop() { host?.UnpatchAll(host.Id); ids = null; maps.Clear(); errors.Clear(); }

        private Thing Item(int count)
        {
            var thing = ThingMaker.MakeThing(itemDef);
            thing.stackCount = count;
            return thing;
        }
        private static int Count(ThingOwner owner)
        {
            int total = 0;
            foreach (Thing thing in owner) total += thing.stackCount;
            return total;
        }

        [Test]
        public void ExactPartialDeliveryMovesSevenFromRealSeventeenStackWithoutConsumingTheRest()
        {
            var source = new Holder();
            var station = new Building_EconomyInstitution();
            var stack = Item(17);
            Assert.That(source.Items.TryAdd(stack, false), Is.True);
            Assert.That(station.DeliveryContainer.TryAdd(Item(5), false), Is.True);
            int originalId = stack.thingIDNumber;
            int before = Count(station.DeliveryContainer);

            int accepted = station.AcceptLaborDelivery(stack, 7);

            Assert.That(accepted, Is.EqualTo(7));
            Assert.That(Count(station.DeliveryContainer) - before, Is.EqualTo(7));
            Assert.That(Count(source.Items), Is.EqualTo(10));
            Assert.That(stack.stackCount, Is.EqualTo(10));
            Assert.That(stack.Destroyed, Is.False);
            Assert.That(stack.holdingOwner, Is.SameAs(source.Items));
            Assert.That(stack.thingIDNumber, Is.EqualTo(originalId));
            Assert.That(Count(source.Items) + Count(station.DeliveryContainer), Is.EqualTo(22), "Physical quantities are conserved.");
        }

        [Test]
        public void FullCounterRejectsDeliveryBeforeSplittingOrRemovingTheSource()
        {
            var source = new Holder();
            var station = new Building_EconomyInstitution();
            for (int i = 0; i < Building_EconomyInstitution.DeliveryCapacity; i++)
                Assert.That(station.DeliveryContainer.TryAdd(Item(1), false), Is.True);
            var stack = Item(17);
            Assert.That(source.Items.TryAdd(stack, false), Is.True);
            int originalId = stack.thingIDNumber;

            Assert.That(station.DeliveryContainer.Count, Is.EqualTo(256));
            Assert.That(station.DeliveryRoom, Is.Zero);
            Assert.That(station.AcceptLaborDelivery(stack, 7), Is.Zero);
            Assert.That(stack.stackCount, Is.EqualTo(17));
            Assert.That(stack.holdingOwner, Is.SameAs(source.Items));
            Assert.That(stack.thingIDNumber, Is.EqualTo(originalId));
            Assert.That(stack.Destroyed, Is.False);
            Assert.That(source.Items.Count, Is.EqualTo(1));
            Assert.That(Count(station.DeliveryContainer), Is.EqualTo(256));
        }
    }
}
