using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using NUnit.Framework;
using PersonalChronicle.Capture;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Economy.PawnEconomy;
using Verse;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class LaborOutputCaptureTests
    {
        [Test]
        public void OutOverloadMatchesInstalledEngineSignature()
        {
            var method = Patch_GenPlaceOutput.TargetMethod();
            Assert.That(method, Is.Not.Null);
            Assert.That(method.GetParameters()[4].ParameterType, Is.EqualTo(typeof(Thing).MakeByRefType()));
            Assert.That(method.GetParameters()[5].ParameterType, Is.EqualTo(typeof(Action<Thing, int>)));
        }

        [Test]
        public void MergedCallbackRecordsOnlyIncrementAndPreservesOriginalCallback()
        {
            var carrier = (Thing)FormatterServices.GetUninitializedObject(typeof(Thing));
            carrier.stackCount = 65; // 原库存55 + 本次真实吸收10。
            int originalCalls = 0, captured = 0;
            Thing capturedThing = null;
            var wrapped = Patch_GenPlaceOutput.WrapPlacedAction((thing, amount) =>
            {
                originalCalls++; Assert.That(thing, Is.SameAs(carrier)); Assert.That(amount, Is.EqualTo(10));
            }, (thing, amount) => { capturedThing = thing; captured += amount; });
            wrapped(carrier, 10);
            Assert.That(originalCalls, Is.EqualTo(1));
            Assert.That(capturedThing, Is.SameAs(carrier));
            Assert.That(captured, Is.EqualTo(10), "never record the merged stack total65");
        }

        [Test]
        public void MultipleRealPlacementCallbacksPreserveEachCarrierAndDelta()
        {
            var first = (Thing)FormatterServices.GetUninitializedObject(typeof(Thing));
            var second = (Thing)FormatterServices.GetUninitializedObject(typeof(Thing));
            var callbacks = new List<Tuple<Thing, int>>();
            var wrapped = Patch_GenPlaceOutput.WrapPlacedAction(null,
                (thing, amount) => callbacks.Add(Tuple.Create(thing, amount)));
            wrapped(first, 5); wrapped(second, 20); wrapped(first, 3);
            Assert.That(callbacks.Count, Is.EqualTo(3));
            Assert.That(callbacks[0].Item1, Is.SameAs(first));
            Assert.That(callbacks[1].Item1, Is.SameAs(second));
            Assert.That(callbacks[0].Item2 + callbacks[1].Item2 + callbacks[2].Item2, Is.EqualTo(28));
        }

        [Test]
        public void OriginalCallbackExceptionsRetainOriginalSemantics()
        {
            bool observed = false;
            var error = new InvalidOperationException("original callback failed");
            var wrapped = Patch_GenPlaceOutput.WrapPlacedAction((_, __) => throw error, (_, __) => observed = true);
            Assert.That(Assert.Throws<InvalidOperationException>(() => wrapped(null, 10)), Is.SameAs(error));
            Assert.That(observed, Is.True);
        }

        [Test]
        public void CaptureFailureCannotPreventOriginalCallback()
        {
            int originalCalls = 0;
            var wrapped = Patch_GenPlaceOutput.WrapPlacedAction((_, __) => originalCalls++,
                (_, __) => throw new InvalidOperationException("capture unavailable"));
            Assert.DoesNotThrow(() => wrapped(null, 10));
            Assert.That(originalCalls, Is.EqualTo(1));
        }

        [Test]
        public void EvidenceIdentityMatchesAuthoritativeCompletionAndCannotBindAnotherTargetOrTick()
        {
            var evidence = new WorkEvidence("contract:1", "vanilla:PlantCutting:Plant42:p1", "VanillaWork", 100, 1, "job", 100);
            string id = LaborOutputCaptureRules.EvidenceIdFor("contract:1", "PlantCutting", "Plant42", "p1", 100);
            Assert.That(id, Is.EqualTo(evidence.EvidenceId));
            Assert.That(LaborOutputCaptureRules.MatchesEvidence("contract:1", "ChopWood", id, "contract:1", "ChopWood", id), Is.True);
            Assert.That(LaborOutputCaptureRules.MatchesEvidence("contract:1", "ChopWood", id, "contract:2", "ChopWood", id), Is.False);
            Assert.That(LaborOutputCaptureRules.MatchesEvidence("contract:1", "ChopWood", id, "contract:1", "Harvest", id), Is.False);
            Assert.That(LaborOutputCaptureRules.MatchesEvidence("contract:1", "ChopWood", id, "contract:1", "ChopWood",
                LaborOutputCaptureRules.EvidenceIdFor("contract:1", "PlantCutting", "Plant43", "p1", 100)), Is.False);
            Assert.That(LaborOutputCaptureRules.MatchesEvidence("contract:1", "ChopWood", id, "contract:1", "ChopWood",
                LaborOutputCaptureRules.EvidenceIdFor("contract:1", "PlantCutting", "Plant42", "p1", 101)), Is.False);
        }

        [TestCase(0, 65, false)]
        [TestCase(-1, 65, false)]
        [TestCase(10, 65, true)]
        [TestCase(28, 28, true)]
        [TestCase(66, 65, false)]
        public void InvalidOrMissingPlacementDeltaIsNeverInferred(int quantity, int actualTotal, bool expected)
            => Assert.That(LaborOutputCaptureRules.ValidPlacedIncrement(quantity, actualTotal), Is.EqualTo(expected));

        [Test]
        public void OneEvidenceWithTwoRealCarriersHasTwoStableBatchIdentities()
        {
            string first = LaborOutputCaptureRules.BatchIdFor("c1", "e1", "WoodLog", 10);
            string second = LaborOutputCaptureRules.BatchIdFor("c1", "e1", "WoodLog", 20);
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first, Is.EqualTo(LaborOutputCaptureRules.BatchIdFor("c1", "e1", "WoodLog", 10)));
        }
    }
}
