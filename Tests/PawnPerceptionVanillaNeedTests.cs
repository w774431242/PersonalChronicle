using System;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Economy.PawnEconomy.Perception;
using RimWorld;
using Verse;

namespace PersonalChronicle.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class PawnPerceptionVanillaNeedTests
    {
        private Harmony shim;
        [OneTimeSetUp] public void Start()
        {
            // Need's static initializer asks ContentFinder for two bar textures. The offline host has no
            // Unity thread/resources; its normal null-return path only needs a managed log shim.
            // Real Need level/category getters and ContentFinder remain unpatched.
            shim = new Harmony("personalchronicle.tests.perceptionneed");
            foreach (var name in new[] { "Error", "Warning" })
                shim.Patch(typeof(Log).GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null),
                    prefix: new HarmonyMethod(typeof(PawnPerceptionVanillaNeedTests).GetMethod(nameof(NoLog), BindingFlags.NonPublic | BindingFlags.Static)));
        }
        private static bool NoLog() => false;
        [OneTimeTearDown] public void Stop() => shim?.UnpatchAll(shim.Id);
        private sealed class ThrowingNeed : Need
        {
            public ThrowingNeed() : base(null) { }
            public override float CurLevel { get => throw new InvalidOperationException("third-party getter"); set { } }
            public override float MaxLevel => 2f;
            public override void NeedInterval() { }
        }
        private sealed class DerivedRest : Need_Rest { public DerivedRest() : base(null) { } }
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static NeedDef Def(string id) { var def = Raw<NeedDef>(); def.defName = id; return def; }
        private static VanillaNeedRegistry Registry(Type restType, Action<string, Exception> diagnostic = null)
            => new VanillaNeedRegistry(id => id == "Rest" ? restType : null, diagnostic);
        [Test] public void ActualVanillaRestGetterAndCategoryAreObservedWithoutMutation()
        {
            var rest = Raw<Need_Rest>(); rest.def = Def("Rest");
            typeof(Need).GetField("curLevelInt", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(rest, .4f);
            var reading = Registry(typeof(Need_Rest)).ReadNumeric(rest, 7, 150);
            Assert.That(reading.StandardRatioSupported, Is.True);
            Assert.That(reading.CurrentLevel.TryGetValue(out var current), Is.True); Assert.That(current, Is.EqualTo(.4f));
            Assert.That(reading.MaximumLevel.TryGetValue(out var maximum), Is.True); Assert.That(maximum, Is.EqualTo(1f));
            Assert.That(reading.VanillaCategory.TryGetValue(out _), Is.True);
            Assert.That(reading.IsFrozen.State, Is.EqualTo(FactState.Unknown)); Assert.That(rest.CurLevel, Is.EqualTo(.4f));
        }
        [Test] public void ThirdPartySubclassDoesNotInheritVerifiedOriginalSemantics()
        {
            var rest = Raw<DerivedRest>(); rest.def = Def("Rest");
            var reading = Registry(typeof(Need_Rest)).ReadNumeric(rest, 1, 0);
            Assert.That(reading.StandardRatioSupported, Is.False);
            Assert.That(reading.AdapterId, Is.EqualTo("unadapted"));
        }
        [Test] public void ChangedLoadedNeedDefinitionInvalidatesAdapter()
        {
            var rest = Raw<Need_Rest>(); rest.def = Def("Rest");
            Assert.That(Registry(typeof(DerivedRest)).ReadNumeric(rest, 1, 0).StandardRatioSupported, Is.False);
        }
        [Test] public void ThrowingThirdPartyGetterIsIsolatedAndOtherFieldsRemainReadable()
        {
            var need = Raw<ThrowingNeed>(); need.def = Def("CustomNeed"); int errors = 0;
            var registry = Registry(typeof(Need_Rest), (key, error) => errors++);
            var reading = registry.ReadNumeric(need, 1, 0);
            Assert.That(reading.CurrentLevel.State, Is.EqualTo(FactState.Error));
            Assert.That(reading.MaximumLevel.TryGetValue(out var max), Is.True); Assert.That(max, Is.EqualTo(2));
            Assert.That(errors, Is.EqualTo(1)); Assert.That(registry.ReadErrors, Is.EqualTo(1));
        }
        [Test] public void PlaceholderBaseConditionGetterIsNeverUsedForRest()
        {
            var rest = Raw<Need_Rest>(); rest.def = Def("Rest"); var registry = Registry(typeof(Need_Rest));
            Assert.That(registry.HasCondition(rest), Is.False);
            Assert.That(registry.ReadCondition(rest, 1, 0).State, Is.EqualTo(FactState.Unsupported));
        }
    }
}
