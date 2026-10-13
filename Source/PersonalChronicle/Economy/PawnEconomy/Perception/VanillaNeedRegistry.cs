using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy.Perception
{
    internal interface IVanillaLifeExtension
    {
        void RegisterNeeds(VanillaNeedRegistry registry);
        Fact<bool> VisiblePregnancy(IReadOnlyList<Hediff> visible, bool complete);
    }
    internal sealed class VanillaNeedRegistry
    {
        private sealed class Adapter
        {
            public Type Type;
            public string Domain, Unit;
            public bool DefinitionVerified, Condition;
            public Func<Need, string> Category;
        }
        private readonly Dictionary<string, Adapter> adapters = new Dictionary<string, Adapter>(StringComparer.Ordinal);
        private readonly Func<string, Type> definitionType;
        private readonly Action<string, Exception> diagnostic;
        public long ReadErrors { get; private set; }
        public const string MoodId = "Mood";
        public VanillaNeedRegistry(Func<string, Type> definitionType, Action<string, Exception> diagnostic)
        {
            this.definitionType = definitionType ?? throw new ArgumentNullException(nameof(definitionType));
            this.diagnostic = diagnostic;
            // Fixed original-game mappings, validated against the loaded NeedDef and exact runtime type.
            Register("Food", typeof(Need_Food), "food", "nutrition", n => ((Need_Food)n).CurCategory.ToString());
            Register("Rest", typeof(Need_Rest), "rest", "NeedLevel", n => ((Need_Rest)n).CurCategory.ToString());
            Register("Joy", typeof(Need_Joy), "joy", "NeedLevel", n => ((Need_Joy)n).CurCategory.ToString());
            Register("Comfort", typeof(Need_Comfort), "comfort", "NeedLevel", n => ((Need_Comfort)n).CurCategory.ToString(), true);
            Register("Beauty", typeof(Need_Beauty), "beauty", "NeedLevel", n => ((Need_Beauty)n).CurCategory.ToString(), true);
            Register("RoomSize", typeof(Need_RoomSize), "roomSize", "NeedLevel", n => ((Need_RoomSize)n).CurCategory.ToString(), true);
            Register("Outdoors", typeof(Need_Outdoors), "outdoors", "NeedLevel", n => ((Need_Outdoors)n).CurCategory.ToString());
            Register("Indoors", typeof(Need_Indoors), "indoors", "NeedLevel", null);
            Register(MoodId, typeof(Need_Mood), "mood", "NeedLevel", null);
        }
        public void Register(string id, Type type, string domain, string unit, Func<Need, string> category, bool condition = false)
        {
            adapters.Add(id, new Adapter { Type = type, Domain = domain, Unit = unit,
                DefinitionVerified = definitionType(id) == type, Category = category, Condition = condition });
        }
        private Adapter Match(Need need)
            => need?.def != null && adapters.TryGetValue(need.def.defName, out var adapter)
                && adapter.DefinitionVerified && adapter.Type == need.GetType() ? adapter : null;
        public NumericNeedFact ReadNumeric(Need need, long revision, int tick)
        {
            if (need?.def == null || string.IsNullOrWhiteSpace(need.def.defName)) throw new ArgumentException("Need identity missing.", nameof(need));
            Adapter adapter = Match(need);
            string id = need.def.defName;
            return new NumericNeedFact(id, adapter?.Domain ?? "special", adapter == null ? "unadapted" : "vanilla." + id,
                Read(() => need.CurLevel, id + ".current"), Read(() => need.MaxLevel, id + ".maximum"), adapter != null,
                adapter?.Unit, revision, tick, Fact<bool>.Unknown("ProtectedFreezeNotAdapted"),
                adapter?.Category == null ? Fact<string>.NotApplicable("NoAdaptedCategory") : Read(() => adapter.Category(need), id + ".category"),
                need.ShowOnNeedList);
        }
        public bool IsObservedMood(Need need) => need?.def?.defName == MoodId && Match(need) != null;
        public bool HasCondition(Need need) => Match(need)?.Condition == true;
        public bool IsConditionCandidate(Need need) => need?.def != null
            && adapters.TryGetValue(need.def.defName, out var adapter) && adapter.Condition;
        public Fact<float> ReadCondition(Need need, long revision, int tick)
        {
            if (!HasCondition(need)) return Fact<float>.Unsupported("ConditionNotAdapted");
            var reading = new NumericNeedFact(need.def.defName, "condition", "vanilla.condition",
                Read(() => need.CurInstantLevel, need.def.defName + ".condition"),
                Read(() => need.MaxLevel, need.def.defName + ".conditionMaximum"), true, "NeedLevel", revision, tick);
            return CurrentNeedProjector.Project("observation", reading, default).ActualRatio;
        }
        private Fact<T> Read<T>(Func<T> getter, string key)
        {
            try { return Fact<T>.Known(getter()); }
            catch (Exception error) { ReadErrors++; diagnostic?.Invoke(key, error); return Fact<T>.Error("GetterFailed"); }
        }
    }
}
