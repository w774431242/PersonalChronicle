using System;
using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy.Perception
{
    internal sealed partial class VanillaPawnFactCapture
    {
        private FactGroup<BodyLifeFactSet> CaptureBody(Pawn pawn, List<Need> needs, PawnCaptureRequest request, bool needsFailed)
        {
            int before = getterErrors, truncated = 0; bool partial = false, healthComplete = true;
            int healthUnexamined = 0, apparelUnexamined = 0;
            CaptureState healthState = CaptureState.Complete, apparelState = CaptureState.Complete, conditionState = CaptureState.Complete;
            var health = new List<VisibleHediffFact>(); var visible = new List<Hediff>();
            if (pawn.health?.hediffSet == null) { partial = true; healthComplete = false; healthState = CaptureState.Unknown; }
            else
            {
                var hediffs = pawn.health.hediffSet.hediffs;
                int count = Math.Min(hediffs.Count, PerceptionSettings.MaximumHediffs);
                truncated += hediffs.Count - count; healthComplete = count == hediffs.Count;
                healthUnexamined = hediffs.Count - count;
                int healthErrorsBefore = getterErrors;
                for (int i = 0; i < count; i++)
                {
                    var condition = hediffs[i];
                    try
                    {
                        if (condition == null || !condition.Visible) continue;
                        visible.Add(condition);
                        health.Add(new VisibleHediffFact(condition.GetUniqueLoadID(), condition.def.defName,
                            condition.Part == null ? null : condition.Part.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ReadFinite(() => condition.Severity, "HediffSeverity"), Read(() => condition.TendableNow(), "HediffTendable")));
                    }
                    catch (Exception error) { partial = true; healthComplete = false; Diagnose("VisibleHediff", error); }
                }
                if (!healthComplete || healthErrorsBefore != getterErrors) healthState = CaptureState.Partial;
            }
            var apparel = new List<ApparelFact>();
            if (pawn.apparel == null) { partial = true; apparelState = CaptureState.Unknown; }
            else
            {
                var worn = pawn.apparel.WornApparel; int count = Math.Min(worn.Count, PerceptionSettings.MaximumApparel);
                truncated += worn.Count - count;
                apparelUnexamined = worn.Count - count; int apparelErrorsBefore = getterErrors;
                for (int i = 0; i < count; i++)
                {
                    var item = worn[i];
                    try
                    {
                        apparel.Add(new ApparelFact(item.GetUniqueLoadID(), item.def.defName,
                            item.def.useHitPoints ? ReadFinite(() => item.MaxHitPoints > 0 ? (float)item.HitPoints / item.MaxHitPoints
                                : float.NaN, "ApparelDurability") : Fact<float>.NotApplicable("NoHitPoints")));
                    }
                    catch (Exception error) { partial = true; apparelState = CaptureState.Partial; Diagnose("Apparel", error); }
                }
                if (apparelUnexamined != 0 || apparelErrorsBefore != getterErrors) apparelState = CaptureState.Partial;
            }
            var conditions = new List<NeedConditionFact>();
            if (needs == null) { partial = true; conditionState = needsFailed ? CaptureState.Error : CaptureState.Unknown; }
            else
            {
                long beforeReads = registry.ReadErrors;
                int count = Math.Min(needs.Count, PerceptionSettings.MaximumNeeds);
                truncated += needs.Count - count;
                for (int i = 0; i < count; i++)
                    if (registry.IsConditionCandidate(needs[i]))
                    {
                        var ratio = registry.ReadCondition(needs[i], request.Revision, request.Tick);
                        if (ratio.State != FactState.Known) { partial = true; conditionState = CaptureState.Partial; }
                        conditions.Add(new NeedConditionFact(needs[i].def.defName, ratio, request.Tick, request.Revision));
                    }
                partial |= registry.ReadErrors != beforeReads;
                if (needs.Count != count) conditionState = CaptureState.Partial;
            }
            var temperature = pawn.Spawned ? ReadFinite(() => pawn.AmbientTemperature, "AmbientTemperature")
                : Fact<float>.Unknown("HeldEnvironmentUnknown");
            var comfortable = Read(() => pawn.ComfortableTemperatureRange(), "ComfortableTemperature");
            Fact<float> min, max;
            if (comfortable.TryGetValue(out var range))
            {
                min = ReadFinite(() => range.min, "ComfortableMinimum"); max = ReadFinite(() => range.max, "ComfortableMaximum");
                if (range.min > range.max) { min = Fact<float>.Error("InvalidTemperatureRange"); max = min; partial = true; }
            }
            else { min = Fact<float>.Unavailable(comfortable.State, comfortable.ReasonCode); max = min; }
            var owned = CaptureBed(() => pawn.ownership?.OwnedBed, "OwnedBed");
            var current = pawn.Spawned ? CaptureBed(() => pawn.CurrentBed(), "CurrentBed") : Fact<BedFact>.Unknown("HeldBedUnknown");
            var pregnancy = Read(() => extension.VisiblePregnancy(visible, healthComplete), "VisiblePregnancy");
            Fact<bool> pregnancyFact = pregnancy.TryGetValue(out var capturedPregnancy) ? capturedPregnancy
                : Fact<bool>.Unavailable(pregnancy.State, pregnancy.ReasonCode);
            partial |= getterErrors != before || needsFailed || truncated != 0 || !healthComplete
                || temperature.State == FactState.Unknown || current.State == FactState.Unknown;
            return Group(new BodyLifeFactSet(health, apparel, conditions, temperature, min, max,
                owned, current, pregnancyFact, healthState, apparelState, conditionState,
                healthUnexamined, apparelUnexamined), request, partial, truncated);
        }
        private Fact<float> ReadFinite(Func<float> getter, string key)
        {
            var fact = Read(getter, key);
            if (fact.TryGetValue(out float value) && (float.IsNaN(value) || float.IsInfinity(value)))
            { getterErrors++; return Fact<float>.Error(PerceptionReasons.NonFiniteReading); }
            return fact;
        }
        private Fact<BedFact> CaptureBed(Func<Building_Bed> getter, string key)
        {
            try
            {
                var bed = getter();
                return bed == null ? Fact<BedFact>.NotApplicable("NoBed") : Fact<BedFact>.Known(new BedFact(bed.GetUniqueLoadID(),
                    bed.MapHeld == null ? Fact<int>.Unknown("BedOffMap") : Fact<int>.Known(bed.MapHeld.uniqueID)));
            }
            catch (Exception error) { getterErrors++; Diagnose(key, error); return Fact<BedFact>.Error("GetterFailed"); }
        }
    }
}
