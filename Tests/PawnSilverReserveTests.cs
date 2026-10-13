using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using Verse;

namespace PersonalChronicle.Tests
{
    [TestFixture, NonParallelizable]
    public sealed partial class PawnSilverReserveTests
    {
        private sealed class Holder : IThingHolder, ILoadReferenceable, IThingHolderEvents<Thing>
        {
            public readonly ThingOwner<Thing> Items;
            public IThingHolder ParentHolder { get; set; }
            public string Id = "recipient";
            public Action Added;
            public Action Removed;
            public Holder(bool single = false) { Items = new ThingOwner<Thing>(this, single, LookMode.Deep); }
            public string GetUniqueLoadID() => Id;
            public ThingOwner GetDirectlyHeldThings() => Items;
            public void GetChildHolders(List<IThingHolder> outChildren) { }
            public void Notify_ItemAdded(Thing item) => Added?.Invoke();
            public void Notify_ItemRemoved(Thing item) => Removed?.Invoke();
        }
        private Harmony shim;
        private ThingDef previousSilver, silverDef;
        private static UniqueIDsManager offlineIds;
        private static readonly List<string> errors = new List<string>();
        private static readonly List<Map> offlineMaps = new List<Map>();
        private static readonly FactionManager offlineFactions = new FactionManager();
        private static List<Type> offlineTypes;
        private ModContentPack testPack;
        private readonly List<System.Collections.DictionaryEntry> staleTypeMisses = new List<System.Collections.DictionaryEntry>();
        private readonly List<string> files = new List<string>();
        [OneTimeSetUp]
        public void Start()
        {
            shim = new Harmony("personalchronicle.tests.silverreserve");
            foreach (string name in new[] { "Error", "Warning", "Message" })
            {
                var method = typeof(Log).GetMethod(name, new[] { typeof(string) });
                shim.Patch(method, prefix: new HarmonyMethod(typeof(PawnSilverReserveTests).GetMethod(nameof(CaptureLog), BindingFlags.NonPublic | BindingFlags.Static)));
            }
            foreach (string name in new[] { "Start", "End" })
            {
                var method = typeof(DeepProfiler).GetMethod(name, name == "Start" ? new[] { typeof(string) } : Type.EmptyTypes);
                shim.Patch(method, prefix: new HarmonyMethod(typeof(PawnSilverReserveTests).GetMethod(nameof(Skip), BindingFlags.NonPublic | BindingFlags.Static)));
            }
            offlineIds = new UniqueIDsManager();
            shim.Patch(typeof(Find).GetProperty(nameof(Find.UniqueIDsManager)).GetGetMethod(), prefix:
                new HarmonyMethod(typeof(PawnSilverReserveTests).GetMethod(nameof(Ids), BindingFlags.NonPublic | BindingFlags.Static)));
            shim.Patch(typeof(Find).GetProperty(nameof(Find.Maps)).GetGetMethod(), prefix:
                new HarmonyMethod(typeof(PawnSilverReserveTests).GetMethod(nameof(Maps), BindingFlags.NonPublic | BindingFlags.Static)));
            shim.Patch(typeof(Find).GetProperty(nameof(Find.FactionManager)).GetGetMethod(), prefix:
                new HarmonyMethod(typeof(PawnSilverReserveTests).GetMethod(nameof(Factions), BindingFlags.NonPublic | BindingFlags.Static)));
            // CLR 4 cannot load some game types with default interface implementations.
            // Supply the compatible assembly inventory, without changing Scribe or transfer methods.
            Type[] compatible;
            try { compatible = typeof(Thing).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException problem)
            {
                compatible = problem.Types.Where(SafeForInventory).ToArray();
                Assert.That(problem.LoaderExceptions.All(x => x is TypeLoadException), Is.True);
                TestContext.Progress.WriteLine("Offline engine type inventory excludes " + problem.LoaderExceptions.Length + " CLR-incompatible entries.");
            }
            offlineTypes = compatible.Concat(typeof(SettlementReservePrototype).Assembly.GetTypes()).Distinct().ToList();
            // Use the actual mod inventory. Its getter may have been inlined before this fixture.
            testPack = (ModContentPack)FormatterServices.GetUninitializedObject(typeof(ModContentPack));
            testPack.assemblies = new ModAssemblyHandler(testPack);
            testPack.assemblies.loadedAssemblies.Add(typeof(SettlementReservePrototype).Assembly);
            LoadedModManager.RunningModsListForReading.Add(testPack);
            // Earlier standalone fixtures may have cached a miss before any mod was registered.
            // GenTypes.ClearCache does not clear typeCache. Remove only our mod's null entries,
            // then restore those entries on teardown; do not change the engine lookup behavior.
            var typeCache = (System.Collections.IDictionary)typeof(GenTypes).GetField("typeCache", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            foreach (System.Collections.DictionaryEntry entry in typeCache)
            {
                string name = (string)entry.Key.GetType().GetField("typeName").GetValue(entry.Key);
                if (entry.Value == null && name.StartsWith("PersonalChronicle.", StringComparison.Ordinal)) staleTypeMisses.Add(entry);
            }
            foreach (var entry in staleTypeMisses) typeCache.Remove(entry.Key);
            shim.Patch(typeof(GenTypes).GetProperty(nameof(GenTypes.AllTypes)).GetGetMethod(), prefix:
                new HarmonyMethod(typeof(PawnSilverReserveTests).GetMethod(nameof(TypeInventory), BindingFlags.NonPublic | BindingFlags.Static)));
            GenTypes.ClearCache();
            Assert.That(GenTypes.GetTypeInAnyAssembly(typeof(SettlementReservePrototype).FullName), Is.EqualTo(typeof(SettlementReservePrototype)));
            previousSilver = ThingDefOf.Silver;
            // Minimal offline Def fixture using the game's Silver stack limit; no full Def bootstrap is claimed.
            var xml = XDocument.Load("E:/SteamLibrary/steamapps/common/RimWorld/Data/Core/Defs/ThingDefs_Items/Items_Resource_Stuff.xml");
            var silverXml = xml.Root.Elements("ThingDef").Single(x => (string)x.Element("defName") == "Silver");
            // BuildableDef's normal constructor initializes Unity textures. The fixture avoids that
            // constructor; all Thing/ThingOwner/ThingMaker/Scribe methods below remain original.
            silverDef = MinimalDef("PC_ReserveTest_Silver", (int)silverXml.Element("stackLimit"));
            DefDatabase<ThingDef>.Add(silverDef); ThingDefOf.Silver = silverDef;
        }
        private static bool Skip() => false;
        private static bool Ids(ref UniqueIDsManager __result) { __result = offlineIds; return false; }
        private static bool Maps(ref List<Map> __result) { __result = offlineMaps; return false; }
        private static bool Factions(ref FactionManager __result) { __result = offlineFactions; return false; }
        private static bool TypeInventory(ref List<Type> __result) { __result = offlineTypes; return false; }
        private static bool SafeForInventory(Type type)
        {
            if (type == null) return false;
            try { return type.Namespace != null && type.FullName != null && (type.DeclaringType == null || type.DeclaringType.FullName != null); }
            catch (TypeLoadException) { return false; }
        }
        private static ThingDef MinimalDef(string name, int stackLimit = 500)
        {
            var def = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            def.defName = name; def.category = ThingCategory.Item; def.thingClass = typeof(Thing);
            def.stackLimit = stackLimit; def.destroyable = true; return def;
        }
        private static bool CaptureLog(string text, MethodBase __originalMethod)
        { if (__originalMethod.Name == "Error") errors.Add(text); return false; }
        [OneTimeTearDown]
        public void Stop()
        {
            ThingDefOf.Silver = previousSilver;
            try
            {
                if (silverDef != null) typeof(DefDatabase<ThingDef>).GetMethod("Remove", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { silverDef });
            }
            finally
            {
                if (testPack != null) LoadedModManager.RunningModsListForReading.Remove(testPack);
                var typeCache = (System.Collections.IDictionary)typeof(GenTypes).GetField("typeCache", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                foreach (var entry in staleTypeMisses) typeCache[entry.Key] = entry.Value;
                staleTypeMisses.Clear();
                GenTypes.ClearCache(); shim.UnpatchAll(shim.Id); offlineIds = null; offlineTypes = null;
            }
        }
        [SetUp] public void Setup() { errors.Clear(); files.Clear(); }
        [TearDown] public void Cleanup()
        {
            Scribe.ForceStop(); foreach (string path in files) if (File.Exists(path)) File.Delete(path);
            Assert.That(errors, Is.Empty, "Unexpected engine/Scribe errors; host shims must not hide failed loading.");
        }
        private Thing Silver(Holder owner, int count = 100)
        {
            var thing = ThingMaker.MakeThing(silverDef); thing.stackCount = count;
            Assert.That(owner.Items.TryAdd(thing, false), Is.True); return thing;
        }
        private static ThingOwner Contents(SettlementReservePrototype reserve) => ((IThingHolder)reserve).GetDirectlyHeldThings();
        private ReserveMovementIntent Intent(SettlementReservePrototype reserve, Holder party, Thing source, int count = 10,
            string id = "deposit", ReserveMovementKind kind = ReserveMovementKind.Deposit)
            => new ReserveMovementIntent(id, kind, reserve.ReadAudit().Version, party.Id, source.GetUniqueLoadID(), count);
        private SettlementReservePrototype Funded(int count = 100)
        {
            var reserve = new SettlementReservePrototype(); var party = new Holder(); var source = Silver(party, count);
            Assert.That(reserve.Execute(Intent(reserve, party, source, count), party.Items).Code, Is.EqualTo(ReserveResultCode.Completed));
            return reserve;
        }
        private string PathForTest() { string path = Path.Combine(Path.GetTempPath(), "pc_reserve_" + Guid.NewGuid().ToString("N") + ".xml"); files.Add(path); return path; }
        private SettlementReservePrototype RoundTrip(SettlementReservePrototype reserve)
        {
            string path = PathForTest(); Scribe.saver.InitSaving(path, "savegame"); Scribe_Deep.Look(ref reserve, "reserve"); Scribe.saver.FinalizeSaving();
            SettlementReservePrototype loaded = null; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "reserve"); Scribe.loader.FinalizeLoading();
            Assert.That(loaded, Is.Not.Null); return loaded;
        }
        private static void Metadata(SettlementReservePrototype reserve, string payload)
        {
            typeof(SettlementReservePrototype).GetField("metadata", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(reserve, payload);
            typeof(SettlementReservePrototype).GetField("preserveRaw", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(reserve, true);
        }

        [Test]
        public void EmptyReserveHasNoMoneyNoParentAndNoGameplayCapability()
        {
            var reserve = new SettlementReservePrototype(); var audit = reserve.ReadAudit();
            Assert.That(audit.ActualSilver, Is.Zero); Assert.That(audit.RecordedSilver, Is.Zero); Assert.That(audit.CanMove, Is.True);
            Assert.That(reserve.ParentHolder, Is.Null); Assert.That(reserve.CanExecuteInGame, Is.False); Assert.That(audit.CanExecuteInGame, Is.False);
            Assert.That((object)reserve, Is.Not.InstanceOf<Thing>().And.Not.InstanceOf<IHaulSource>());
        }
        [TestCase(10)] [TestCase(100)]
        public void ActualDepositSplitsOrTransfersWholeStackAndProvesBothEnds(int count)
        {
            var reserve = new SettlementReservePrototype(); var owner = new Holder(); var source = Silver(owner);
            string originalId = source.GetUniqueLoadID(); var result = reserve.Execute(Intent(reserve, owner, source, count), owner.Items);
            Assert.That(result.Code, Is.EqualTo(ReserveResultCode.Completed)); Assert.That(result.Evidence.HasExactQuantityProof, Is.True);
            Assert.That(result.Evidence.CompletedMinor, Is.EqualTo(count * 100));
            Assert.That(owner.Items.Sum(x => x.stackCount), Is.EqualTo(100 - count)); Assert.That(reserve.ReadAudit().ActualSilver, Is.EqualTo(count));
            Assert.That(result.Evidence.Intent.SourceThingId, Is.EqualTo(originalId));
            Assert.That(Contents(reserve)[0].holdingOwner, Is.SameAs(Contents(reserve))); Assert.That(Contents(reserve)[0].Spawned, Is.False);
            if (count == 100) Assert.That(result.Evidence.ResultThingId, Is.EqualTo(originalId));
            else Assert.That(result.Evidence.ResultThingId, Is.Not.EqualTo(originalId));
        }
        [Test]
        public void SeparateDepositsNeverMergeAndReceiptsRetainLineage()
        {
            var reserve = new SettlementReservePrototype(); var owner = new Holder(); var source = Silver(owner);
            var first = reserve.Execute(Intent(reserve, owner, source), owner.Items);
            var second = reserve.Execute(Intent(reserve, owner, source, id: "second"), owner.Items);
            Assert.That(Contents(reserve).Count, Is.EqualTo(2)); Assert.That(reserve.ReadAudit().ActualSilver, Is.EqualTo(20));
            Assert.That(first.Evidence.ResultThingId, Is.Not.EqualTo(second.Evidence.ResultThingId));
        }
        [Test]
        public void WithdrawalMovesExistingSilverToRecipientAndConservesTotal()
        {
            var reserve = Funded(); var owner = new Holder(); var source = Contents(reserve)[0];
            var result = reserve.Execute(Intent(reserve, owner, source, 25, "withdraw", ReserveMovementKind.Withdrawal), owner.Items);
            Assert.That(result.Code, Is.EqualTo(ReserveResultCode.Completed)); Assert.That(result.Evidence.HasExactQuantityProof, Is.True);
            Assert.That(reserve.ReadAudit().ActualSilver, Is.EqualTo(75)); Assert.That(reserve.ReadAudit().RecordedSilver, Is.EqualTo(75));
            Assert.That(owner.Items.Sum(x => x.stackCount), Is.EqualTo(25)); Assert.That(result.Evidence.CompletedMinor, Is.EqualTo(2500));
        }
        [Test]
        public void RepeatedCommandReturnsEvidenceWithoutMovingCoinsAgain()
        {
            var reserve = new SettlementReservePrototype(); var owner = new Holder(); var source = Silver(owner);
            var command = Intent(reserve, owner, source); reserve.Execute(command, owner.Items);
            Assert.That(reserve.Execute(command, null).Code, Is.EqualTo(ReserveResultCode.Replay));
            Assert.That(reserve.ReadAudit().Version, Is.EqualTo(1)); Assert.That(reserve.ReadAudit().ActualSilver, Is.EqualTo(10));
            Assert.That(reserve.Execute(Intent(reserve, owner, source, 20), owner.Items).Code, Is.EqualTo(ReserveResultCode.CommandConflict));
        }
        [Test]
        public void StaleVersionAndWrongCounterpartyDoNotTouchObjects()
        {
            var reserve = new SettlementReservePrototype(); var owner = new Holder(); var source = Silver(owner);
            var stale = Intent(reserve, owner, source, id: "stale"); reserve.Execute(Intent(reserve, owner, source), owner.Items);
            Assert.That(reserve.Execute(stale, owner.Items).Code, Is.EqualTo(ReserveResultCode.StaleVersion));
            var wrong = new ReserveMovementIntent("wrong", ReserveMovementKind.Deposit, 1, "other", source.GetUniqueLoadID(), 10);
            Assert.That(reserve.Execute(wrong, owner.Items).Code, Is.EqualTo(ReserveResultCode.InvalidCounterparty));
            Assert.That(source.stackCount, Is.EqualTo(90)); Assert.That(reserve.ReadAudit().Version, Is.EqualTo(1));
        }
        [Test]
        public void InvalidSourceAndNonSilverCannotProduceDepositEvidence()
        {
            var reserve = new SettlementReservePrototype(); var owner = new Holder(); var source = Silver(owner);
            var otherDef = MinimalDef("not-silver");
            source.def = otherDef;
            Assert.That(reserve.Execute(Intent(reserve, owner, source), owner.Items).Code, Is.EqualTo(ReserveResultCode.InvalidSource));
            Assert.That(reserve.ReadAudit().RecordedSilver, Is.Zero);
        }
        [Test]
        public void OverdrawAndOrdinaryDestinationRefusalLeaveJournalAndSilverUnchanged()
        {
            var reserve = Funded(); var owner = new Holder(true); Silver(owner, 1); var source = Contents(reserve)[0];
            Assert.That(reserve.Execute(Intent(reserve, owner, source, 101, "over", ReserveMovementKind.Withdrawal), owner.Items).Code,
                Is.EqualTo(ReserveResultCode.InsufficientSilver));
            Assert.That(reserve.Execute(Intent(reserve, owner, source, 5, "full", ReserveMovementKind.Withdrawal), owner.Items).Code,
                Is.EqualTo(ReserveResultCode.DestinationRejected));
            Assert.That(reserve.ReadAudit().Version, Is.EqualTo(1)); Assert.That(reserve.ReadAudit().ActualSilver, Is.EqualTo(100));
            Assert.That(reserve.ReadMovement("full"), Is.Null);
        }
        [Test]
        public void SelfOrNestedReserveTransferIsRejected()
        {
            var reserve = Funded(); var child = new Holder { ParentHolder = reserve }; var source = Silver(child);
            Assert.That(reserve.Execute(Intent(reserve, child, source, id: "child"), child.Items).Code, Is.EqualTo(ReserveResultCode.InvalidCounterparty));
            var own = new ReserveMovementIntent("self", ReserveMovementKind.Deposit, 1, "reserve", Contents(reserve)[0].GetUniqueLoadID(), 1);
            Assert.That(reserve.Execute(own, Contents(reserve)).Code, Is.EqualTo(ReserveResultCode.InvalidCounterparty));
        }
        [Test]
        public void DestroyedSilverReportsDeficitWithoutReducingRecordedNetDeposits()
        {
            var reserve = Funded(); Contents(reserve)[0].Destroy(); var audit = reserve.ReadAudit();
            Assert.That(audit.ActualSilver, Is.Zero); Assert.That(audit.RecordedSilver, Is.EqualTo(100));
            Assert.That(audit.DeficitMinor, Is.EqualTo(10000)); Assert.That(audit.CanMove, Is.False);
            var owner = new Holder(); var source = Silver(owner);
            Assert.That(reserve.Execute(Intent(reserve, owner, source, id: "after-loss"), owner.Items).Code, Is.EqualTo(ReserveResultCode.NotReady));
            Assert.That(source.stackCount, Is.EqualTo(100));
        }
        [Test]
        public void ExternalAdditionIsUnrecordedSurplusAndCannotCreateMoney()
        {
            var reserve = new SettlementReservePrototype(); var owner = new Holder(); var source = Silver(owner);
            owner.Items.TryTransferToContainer(source, Contents(reserve), 10, false);
            Assert.That(reserve.ReadAudit().DifferenceSilver, Is.EqualTo(10)); Assert.That(reserve.ReadAudit().RecordedSilver, Is.Zero);
            Assert.That(reserve.Execute(Intent(reserve, owner, source), owner.Items).Code, Is.EqualTo(ReserveResultCode.NotReady));
        }
        [Test]
        public void ReentrantRecipientCallbackCannotExecuteAnotherMovement()
        {
            var reserve = Funded(); var owner = new Holder(); var source = Contents(reserve)[0]; ReserveResultCode? nested = null;
            var intent = Intent(reserve, owner, source, 5, "withdraw", ReserveMovementKind.Withdrawal);
            owner.Added = () => nested = reserve.Execute(intent, owner.Items).Code;
            Assert.That(reserve.Execute(intent, owner.Items).Code, Is.EqualTo(ReserveResultCode.Completed));
            Assert.That(nested, Is.EqualTo(ReserveResultCode.Busy)); Assert.That(reserve.ReadAudit().Version, Is.EqualTo(2));
        }
        [Test]
        public void CallbackFailureAfterPhysicalTransferIsIsolatedAndNotReplayedOrRefunded()
        {
            var reserve = Funded(); var owner = new Holder(); var source = Contents(reserve)[0];
            var intent = Intent(reserve, owner, source, 5, "withdraw", ReserveMovementKind.Withdrawal);
            owner.Added = () => throw new InvalidOperationException("recipient callback failed");
            var result = reserve.Execute(intent, owner.Items);
            Assert.That(result.Code, Is.EqualTo(ReserveResultCode.ReconciliationRequired)); Assert.That(result.Evidence.CompletedMinor, Is.Null);
            Assert.That(owner.Items.Sum(x => x.stackCount), Is.EqualTo(5)); Assert.That(reserve.ReadAudit().ActualSilver, Is.EqualTo(95));
            Assert.That(reserve.ReadAudit().RecordedSilver, Is.EqualTo(100)); Assert.That(reserve.Execute(intent, owner.Items).Code, Is.EqualTo(ReserveResultCode.Replay));
            Assert.That(reserve.ReadAudit().CanMove, Is.False);
        }
        [Test]
        public void QuantityTamperingDuringCallbackDoesNotFinalizeTheMovement()
        {
            var reserve = Funded(); var owner = new Holder(); var source = Contents(reserve)[0];
            owner.Added = () => owner.Items[0].stackCount--;
            var result = reserve.Execute(Intent(reserve, owner, source, 5, "withdraw", ReserveMovementKind.Withdrawal), owner.Items);
            Assert.That(result.Code, Is.EqualTo(ReserveResultCode.ReconciliationRequired));
            Assert.That(result.Evidence.CounterpartyAfter, Is.EqualTo(4)); Assert.That(result.Evidence.ReserveAfter, Is.EqualTo(95));
            Assert.That(result.Evidence.CompletedMinor, Is.Null);
        }
        [Test]
        public void CrossThreadAccessIsRejected()
        {
            var reserve = new SettlementReservePrototype();
            Task.Run(() => Assert.Throws<InvalidOperationException>(() => reserve.ReadAudit())).GetAwaiter().GetResult();
        }
        [TestCase(0, 100, false, 0, 0)] [TestCase(99, 100, false, 0, 0)] [TestCase(100, 100, true, 1, 0)]
        [TestCase(101, 200, true, 1, 100)] [TestCase(200, 199, false, 0, 0)]
        public void WithdrawalMathUsesAvailableTreasuryFundsAndKeepsFractions(long request, long available, bool success, long coins, long after)
        {
            Assert.That(ReserveWithdrawalRules.TryPlan(request, available, out var plan), Is.EqualTo(success));
            if (success) { Assert.That(plan.SilverCount, Is.EqualTo(coins)); Assert.That(plan.AvailableAfterMinor, Is.EqualTo(after));
                Assert.That(plan.DebitedMinor + plan.UnconvertedRequestMinor, Is.EqualTo(request)); }
            else Assert.That(plan, Is.Null);
        }
        [Test]
        public void RealScribeRestoresSilverOwnerIdentityAndReplayEvidence()
        {
            var reserve = Funded(); var initial = reserve.ReadAudit(); var loaded = RoundTrip(reserve); var restored = loaded.ReadAudit();
            Assert.That(restored.CanMove, Is.True); Assert.That(restored.ActualSilver, Is.EqualTo(100)); Assert.That(restored.RecordedSilver, Is.EqualTo(100));
            Assert.That(restored.Stacks[0].ThingId, Is.EqualTo(initial.Stacks[0].ThingId)); Assert.That(Contents(loaded)[0].holdingOwner, Is.SameAs(Contents(loaded)));
            Assert.That(Contents(loaded).Owner, Is.SameAs(loaded));
            Assert.That(loaded.Execute(loaded.ReadMovement("deposit").Intent, null).Code, Is.EqualTo(ReserveResultCode.Replay));
            var owner = new Holder(); Assert.That(loaded.Execute(Intent(loaded, owner, Contents(loaded)[0], 10, "w", ReserveMovementKind.Withdrawal), owner.Items).Code,
                Is.EqualTo(ReserveResultCode.Completed));
        }
        [Test]
        public void RealScribeKeepsUncertainMovementWithoutRedoingIt()
        {
            var reserve = Funded(); var owner = new Holder(); owner.Added = () => throw new InvalidOperationException();
            reserve.Execute(Intent(reserve, owner, Contents(reserve)[0], 5, "w", ReserveMovementKind.Withdrawal), owner.Items);
            var loaded = RoundTrip(reserve);
            Assert.That(loaded.ReadAudit().ActualSilver, Is.EqualTo(95)); Assert.That(loaded.ReadAudit().RecordedSilver, Is.EqualTo(100));
            Assert.That(loaded.ReadMovement("w").Phase, Is.EqualTo(ReserveMovementPhase.ReconciliationRequired)); Assert.That(loaded.ReadAudit().CanMove, Is.False);
        }
        [TestCase("<ReserveJournal schema=\"999\" version=\"9\"><Future 内容=\"保留\" /></ReserveJournal>")]
        [TestCase("<broken")]
        public void FutureOrDamagedMetadataIsPreservedVerbatimAcrossRealScribeResaves(string payload)
        {
            var reserve = Funded(); Metadata(reserve, payload); var loaded = RoundTrip(reserve); var again = RoundTrip(loaded);
            var stored = typeof(SettlementReservePrototype).GetField("metadata", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(again);
            Assert.That(stored, Is.EqualTo(payload)); Assert.That(again.ReadAudit().ActualSilver, Is.EqualTo(100));
            Assert.That(again.ReadAudit().RecordedSilver, Is.Null); Assert.That(again.ReadAudit().CanMove, Is.False);
        }
        [Test]
        public void MissingPrototypeSaveSegmentDefaultsToEmptyWithoutFunding()
        {
            string path = PathForTest(); File.WriteAllText(path, "<savegame />"); SettlementReservePrototype reserve = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref reserve, "reserve"); Scribe.loader.FinalizeLoading();
            reserve = reserve ?? new SettlementReservePrototype(); Assert.That(reserve.ReadAudit().ActualSilver, Is.Zero);
            Assert.That(reserve.ReadAudit().RecordedSilver, Is.Zero);
        }
        [Test]
        public void MissingMetadataWithCoinsIsQuarantinedInsteadOfCredited()
        {
            var reserve = Funded(); Metadata(reserve, null); var loaded = RoundTrip(reserve);
            Assert.That(loaded.ReadAudit().RecordedSilver, Is.Zero); Assert.That(loaded.ReadAudit().ActualSilver, Is.EqualTo(100));
            Assert.That(loaded.ReadAudit().CanMove, Is.False);
        }
        [Test]
        public void PreparedJournalRestoresBlockedWithoutMovingOrCreditingSilver()
        {
            var intent = new ReserveMovementIntent("unfinished", ReserveMovementKind.Deposit, 0, "recipient", "Thing_Silver1", 10);
            string payload = ReserveJournalCodec.Encode(1, null, new[] { new ReserveMovementEvidence(intent, ReserveMovementPhase.Prepared, 0, null, 100, null) });
            var reserve = new SettlementReservePrototype(); Metadata(reserve, payload); var loaded = RoundTrip(reserve);
            Assert.That(loaded.ReadMovement("unfinished").Phase, Is.EqualTo(ReserveMovementPhase.Prepared));
            Assert.That(loaded.ReadAudit().ActualSilver, Is.Zero); Assert.That(loaded.ReadAudit().RecordedSilver, Is.Zero);
            Assert.That(loaded.ReadAudit().CanMove, Is.False);
        }
        [Test]
        public void CodecRejectsInventedCompletedEvidenceDuplicateIdsAndUnknownFields()
        {
            var reserve = Funded(); var entry = reserve.ReadMovement("deposit");
            string good = ReserveJournalCodec.Encode(1, null, new[] { entry });
            foreach (string bad in new[] { good.Replace("reserveAfter=\"100\"", "reserveAfter=\"99\""),
                good.Replace("version=\"1\"", "version=\"2\""), good.Replace("<Move ", "<Move unknown=\"1\" "),
                ReserveJournalCodec.Encode(2, null, new[] { entry, entry }) })
            {
                var result = ReserveJournalCodec.Decode(bad); Assert.That(result.PreserveRaw, Is.True); Assert.That(result.Fault, Is.EqualTo("InvalidReservePayload"));
            }
        }
        [Test]
        public void ReadAuditDoesNotMutateReserveAndPublishedStacksStayImmutable()
        {
            var reserve = Funded(); var before = reserve.ReadAudit(); reserve.ReadAudit();
            Assert.That(reserve.ReadAudit().Version, Is.EqualTo(before.Version));
            Contents(reserve)[0].stackCount = 99; Assert.That(before.Stacks[0].SilverCount, Is.EqualTo(100));
            Assert.Throws<NotSupportedException>(() => ((IList<ReserveStackSnapshot>)before.Stacks).Clear());
        }
        [Test]
        public void CompletedWithdrawalSurvivesScribeAndDoesNotReissueOnReplay()
        {
            var reserve = Funded(); var owner = new Holder();
            var command = Intent(reserve, owner, Contents(reserve)[0], 5, "w", ReserveMovementKind.Withdrawal);
            reserve.Execute(command, owner.Items); var loaded = RoundTrip(reserve);
            Assert.That(loaded.ReadAudit().RecordedSilver, Is.EqualTo(95)); Assert.That(loaded.ReadAudit().ActualSilver, Is.EqualTo(95));
            Assert.That(loaded.Execute(command, owner.Items).Code, Is.EqualTo(ReserveResultCode.Replay));
            Assert.That(owner.Items.Sum(x => x.stackCount), Is.EqualTo(5));
        }
        [Test]
        public void RealScribeKeepsLossAsDeficitWithoutAutomaticWriteOff()
        {
            var reserve = Funded(); Contents(reserve)[0].Destroy(); var loaded = RoundTrip(reserve);
            Assert.That(loaded.ReadAudit().RecordedSilver, Is.EqualTo(100)); Assert.That(loaded.ReadAudit().ActualSilver, Is.Zero);
            Assert.That(loaded.ReadAudit().DeficitMinor, Is.EqualTo(10000)); Assert.That(loaded.ReadAudit().CanMove, Is.False);
        }
        [Test]
        public void JournalCapacityNeverEvictsReplayProtectionOrMovesUnrecordedCoins()
        {
            var reserve = new SettlementReservePrototype(); var owner = new Holder(); var source = Silver(owner, 1);
            ReserveMovementIntent first = null;
            for (int i = 0; i < ReserveJournalCodec.MaximumEntries; i++)
            {
                var intent = new ReserveMovementIntent("old:" + i,
                    i % 2 == 0 ? ReserveMovementKind.Deposit : ReserveMovementKind.Withdrawal, i, owner.Id, source.GetUniqueLoadID(), 1);
                if (i == 0) first = intent;
                Assert.That(reserve.Execute(intent, owner.Items).Code, Is.EqualTo(ReserveResultCode.Completed));
            }
            Assert.That(reserve.ReadAudit().RecordedSilver, Is.Zero);
            Assert.That(reserve.Execute(Intent(reserve, owner, source, 1, "new"), owner.Items).Code, Is.EqualTo(ReserveResultCode.CapacityReached));
            Assert.That(source.holdingOwner, Is.SameAs(owner.Items)); Assert.That(source.stackCount, Is.EqualTo(1));
            Assert.That(reserve.Execute(first, null).Code, Is.EqualTo(ReserveResultCode.Replay));
        }
        [Test]
        public void InvalidHeldOwnershipIsReportedAsUnknownInsteadOfAZeroBalance()
        {
            var reserve = Funded(); Contents(reserve)[0].holdingOwner = null;
            Assert.That(reserve.ReadAudit().ActualSilver, Is.Null); Assert.That(reserve.ReadAudit().RecordedSilver, Is.EqualTo(100));
            Assert.That(reserve.ReadAudit().CanMove, Is.False);
        }
        [Test]
        public void SavingFromMovementCallbackIsRefusedAndProducesUncertainEvidence()
        {
            var reserve = Funded(); var owner = new Holder();
            owner.Added = () => reserve.ExposeData();
            var result = reserve.Execute(Intent(reserve, owner, Contents(reserve)[0], 1, "w", ReserveMovementKind.Withdrawal), owner.Items);
            Assert.That(result.Code, Is.EqualTo(ReserveResultCode.ReconciliationRequired)); Assert.That(result.Evidence.CompletedMinor, Is.Null);
            Assert.That(reserve.ReadAudit().ActualSilver, Is.EqualTo(99));
        }
        [Test]
        public void CommandsWhileScribeIsSavingAreRejectedBeforeTransfer()
        {
            var reserve = new SettlementReservePrototype(); var owner = new Holder(); var source = Silver(owner);
            string path = PathForTest(); Scribe.saver.InitSaving(path, "savegame");
            Assert.That(reserve.Execute(Intent(reserve, owner, source), owner.Items).Code, Is.EqualTo(ReserveResultCode.NotReady));
            Scribe.saver.FinalizeSaving(); Assert.That(reserve.ReadAudit().ActualSilver, Is.Zero);
        }
        [Test]
        public void DtdAndInvalidEnumMetadataAreQuarantinedAsRawText()
        {
            foreach (string payload in new[] { "<!DOCTYPE x [<!ENTITY e 'bad'>]><ReserveJournal schema='1' version='0'>&e;</ReserveJournal>",
                "<ReserveJournal schema='1' version='1'><Move id='x' kind='99' expected='0' party='p' source='s' count='1' phase='Completed' reserveBefore='0' partyBefore='1'/></ReserveJournal>" })
            {
                var result = ReserveJournalCodec.Decode(payload); Assert.That(result.PreserveRaw, Is.True); Assert.That(result.Fault, Is.EqualTo("InvalidReservePayload"));
            }
        }
        [TestCase(-1)] [TestCase(long.MaxValue)]
        public void InvalidEvidenceQuantitiesCannotOverflowDifferenceOrMoneyMath(long count)
        {
            var intent = new ReserveMovementIntent("x", ReserveMovementKind.Deposit, 0, "p", "s", 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReserveMovementEvidence(intent, ReserveMovementPhase.Completed, count, 1, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReserveAuditSnapshot(0, count, 0, null, Array.Empty<ReserveStackSnapshot>()));
        }
        [Test]
        public void MaximumWithdrawalMathUsesLongWithoutNarrowingOrLosingMinorUnits()
        {
            Assert.That(ReserveWithdrawalRules.TryPlan(MinorCurrency.Maximum, MinorCurrency.Maximum, out var plan), Is.True);
            Assert.That(plan.SilverCount, Is.EqualTo(10000000000L)); Assert.That(plan.DebitedMinor, Is.EqualTo(MinorCurrency.Maximum));
            Assert.That(plan.AvailableAfterMinor, Is.Zero);
        }
        [Test]
        public void DetachedSourceAfterRemovalCallbackFailureIsSavedWithoutDepositCredit()
        {
            var reserve = new SettlementReservePrototype(); var owner = new Holder(); var source = Silver(owner);
            var intent = Intent(reserve, owner, source, 100);
            owner.Removed = () => throw new InvalidOperationException("source callback failed");
            var result = reserve.Execute(intent, owner.Items);
            Assert.That(result.Code, Is.EqualTo(ReserveResultCode.ReconciliationRequired));
            Assert.That(result.Evidence.CompletedMinor, Is.Null); Assert.That(result.Evidence.Error, Does.Contain("DetachedSourceSafeguarded"));
            Assert.That(source.holdingOwner, Is.SameAs(Contents(reserve))); Assert.That(owner.Items, Is.Empty);
            var loaded = RoundTrip(reserve); Assert.That(loaded.ReadAudit().ActualSilver, Is.EqualTo(100));
            Assert.That(loaded.ReadAudit().RecordedSilver, Is.Zero); Assert.That(loaded.ReadAudit().CanMove, Is.False);
            Assert.That(loaded.ReadMovement("deposit").ResultThingId, Is.EqualTo(source.GetUniqueLoadID()));
        }
        [Test]
        public void FabricatedSecondaryContainerWithSameOwnerCannotImpersonateItsCanonicalInventory()
        {
            var reserve = new SettlementReservePrototype(); var owner = new Holder(); var source = Silver(owner);
            var secondary = new ThingOwner<Thing>(owner); owner.Items.TryTransferToContainer(source, secondary, 100, false);
            Assert.That(reserve.Execute(Intent(reserve, owner, source, 100), secondary).Code, Is.EqualTo(ReserveResultCode.InvalidCounterparty));
            Assert.That(source.holdingOwner, Is.SameAs(secondary)); Assert.That(reserve.ReadAudit().ActualSilver, Is.Zero);
        }
    }
}
