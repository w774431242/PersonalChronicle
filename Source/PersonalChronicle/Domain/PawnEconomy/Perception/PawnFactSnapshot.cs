using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PersonalChronicle.Domain.PawnEconomy.Perception
{
    [Flags]
    internal enum PerceptionGroups { None = 0, Context = 1, Needs = 2, Body = 4, Relationships = 8, All = 15 }
    internal enum CaptureState { Unknown = 0, Complete = 1, Partial = 2, NotApplicable = 3, Error = 4 }
    internal enum SubjectLocation { Unknown = 0, SpawnedMap = 1, HeldOnMap = 2, OffMap = 3, Dead = 4 }

    internal static class PerceptionCollections
    {
        public static ReadOnlyCollection<T> Freeze<T>(IEnumerable<T> source)
            => new List<T>(source ?? Array.Empty<T>()).AsReadOnly();
    }

    internal sealed class FactGroup<T> where T : class
    {
        public CaptureState State { get; }
        public T Data { get; }
        public int CapturedTick { get; }
        public long Revision { get; }
        public string ReasonCode { get; }
        public int TruncatedCount { get; }
        public FactGroup(CaptureState state, T data, int tick, long revision, string reason, int truncated = 0)
        {
            if (state != CaptureState.Unknown && state != CaptureState.Complete && state != CaptureState.Partial
                && state != CaptureState.NotApplicable && state != CaptureState.Error) throw new ArgumentOutOfRangeException(nameof(state));
            if (tick < 0 || revision < 0 || truncated < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            if ((state == CaptureState.Complete || state == CaptureState.Partial) && data == null)
                throw new ArgumentNullException(nameof(data));
            State = state; Data = data; CapturedTick = tick; Revision = revision;
            ReasonCode = reason ?? PerceptionReasons.Uninitialized; TruncatedCount = truncated;
        }
        public static FactGroup<T> Unknown() => new FactGroup<T>(CaptureState.Unknown, null, 0, 0, PerceptionReasons.Uninitialized);
    }

    internal sealed class PawnContextFact
    {
        public SubjectLocation Location { get; }
        public int MapId { get; }
        public Fact<int> CellX { get; }
        public Fact<int> CellZ { get; }
        public Fact<string> DevelopmentalStage { get; }
        public Fact<bool> Downed { get; }
        public Fact<bool> Drafted { get; }
        public Fact<bool> MentalState { get; }
        public Fact<string> JobDefName { get; }
        public Fact<bool> PlayerForced { get; }
        public Fact<string> TimeAssignmentDefName { get; }
        public Fact<string> DisplayName { get; }
        public PawnContextFact(SubjectLocation location, int mapId, Fact<int> cellX = default, Fact<int> cellZ = default,
            Fact<string> developmentalStage = default, Fact<bool> downed = default, Fact<bool> drafted = default,
            Fact<bool> mentalState = default, Fact<string> jobDefName = default,
            Fact<bool> playerForced = default, Fact<string> timeAssignmentDefName = default, Fact<string> displayName = default)
        {
            Location = location; MapId = mapId; CellX = cellX; CellZ = cellZ; DevelopmentalStage = developmentalStage;
            Downed = downed; Drafted = drafted; MentalState = mentalState; JobDefName = jobDefName;
            PlayerForced = playerForced; TimeAssignmentDefName = timeAssignmentDefName;
            DisplayName = displayName;
        }
    }

    internal sealed class NeedFactSet
    {
        public IReadOnlyList<NumericNeedFact> Needs { get; }
        public IReadOnlyList<string> ActualNeedIds { get; }
        public IReadOnlyList<string> UnsupportedNeedIds { get; }
        public IReadOnlyList<string> FailedNeedIds { get; }
        public Fact<float> MoodRatio { get; }
        public NeedFactSet(IEnumerable<NumericNeedFact> needs, IEnumerable<string> actual,
            IEnumerable<string> unsupported, IEnumerable<string> failed, Fact<float> moodRatio)
        {
            Needs = PerceptionCollections.Freeze(needs); ActualNeedIds = PerceptionCollections.Freeze(actual);
            UnsupportedNeedIds = PerceptionCollections.Freeze(unsupported); FailedNeedIds = PerceptionCollections.Freeze(failed);
            MoodRatio = moodRatio;
        }
    }

    internal sealed class VisibleHediffFact
    {
        public string HediffId { get; }
        public string DefName { get; }
        public string BodyPartId { get; }
        public Fact<float> Severity { get; }
        public Fact<bool> Tendable { get; }
        public VisibleHediffFact(string id, string defName, string partId, Fact<float> severity, Fact<bool> tendable)
        { HediffId = id; DefName = defName; BodyPartId = partId; Severity = severity; Tendable = tendable; }
    }
    internal sealed class ApparelFact
    {
        public string ItemId { get; }
        public string DefName { get; }
        public Fact<float> DurabilityRatio { get; }
        public ApparelFact(string id, string defName, Fact<float> durability)
        { ItemId = id; DefName = defName; DurabilityRatio = durability; }
    }
    internal sealed class NeedConditionFact
    {
        public string NeedDefName { get; }
        public Fact<float> CurrentConditionRatio { get; }
        public int CapturedTick { get; }
        public long Revision { get; }
        public NeedConditionFact(string needId, Fact<float> ratio, int tick, long revision)
        { NeedDefName = needId; CurrentConditionRatio = ratio; CapturedTick = tick; Revision = revision; }
    }
    internal sealed class BedFact
    {
        public string BedId { get; }
        public Fact<int> MapId { get; }
        public BedFact(string id, Fact<int> mapId) { BedId = id; MapId = mapId; }
    }
    internal sealed class BodyLifeFactSet
    {
        public IReadOnlyList<VisibleHediffFact> VisibleHediffs { get; }
        public IReadOnlyList<ApparelFact> Apparel { get; }
        public IReadOnlyList<NeedConditionFact> Conditions { get; }
        public CaptureState HealthState { get; }
        public CaptureState ApparelState { get; }
        public CaptureState ConditionsState { get; }
        public int UnexaminedHealthCount { get; }
        public int UnexaminedApparelCount { get; }
        public Fact<float> AmbientTemperature { get; }
        public Fact<float> ComfortableMinimum { get; }
        public Fact<float> ComfortableMaximum { get; }
        public Fact<BedFact> OwnedBed { get; }
        public Fact<BedFact> CurrentBed { get; }
        public Fact<bool> VisiblePregnancy { get; }
        public BodyLifeFactSet(IEnumerable<VisibleHediffFact> health, IEnumerable<ApparelFact> apparel,
            IEnumerable<NeedConditionFact> conditions, Fact<float> temperature, Fact<float> minimum,
            Fact<float> maximum, Fact<BedFact> ownedBed, Fact<BedFact> currentBed, Fact<bool> visiblePregnancy,
            CaptureState healthState = CaptureState.Unknown, CaptureState apparelState = CaptureState.Unknown,
            CaptureState conditionsState = CaptureState.Unknown, int unexaminedHealth = 0, int unexaminedApparel = 0)
        {
            VisibleHediffs = PerceptionCollections.Freeze(health); Apparel = PerceptionCollections.Freeze(apparel);
            Conditions = PerceptionCollections.Freeze(conditions); AmbientTemperature = temperature;
            ComfortableMinimum = minimum; ComfortableMaximum = maximum;
            OwnedBed = ownedBed; CurrentBed = currentBed; VisiblePregnancy = visiblePregnancy;
            HealthState = healthState; ApparelState = apparelState; ConditionsState = conditionsState;
            UnexaminedHealthCount = unexaminedHealth; UnexaminedApparelCount = unexaminedApparel;
        }
    }
    internal sealed class RelationshipFact
    {
        public string RelationDefName { get; }
        public string OtherPawnId { get; }
        public int StartTick { get; }
        public RelationshipFact(string relation, string other, int startTick)
        { RelationDefName = relation; OtherPawnId = other; StartTick = startTick; }
    }
    internal sealed class RelationshipFactSet
    {
        public IReadOnlyList<RelationshipFact> DirectRelations { get; }
        public RelationshipFactSet(IEnumerable<RelationshipFact> relations)
        { DirectRelations = PerceptionCollections.Freeze(relations); }
    }
    internal sealed class PawnFactSnapshot
    {
        public string PawnId { get; }
        public long SessionEpoch { get; }
        public long ObservationRevision { get; }
        public long PublicationBatchId { get; }
        public FactGroup<PawnContextFact> Context { get; }
        public FactGroup<NeedFactSet> Needs { get; }
        public FactGroup<BodyLifeFactSet> Body { get; }
        public FactGroup<RelationshipFactSet> Relationships { get; }
        public PawnFactSnapshot(string pawnId, long epoch, long revision, long publicationBatch,
            FactGroup<PawnContextFact> context = null, FactGroup<NeedFactSet> needs = null,
            FactGroup<BodyLifeFactSet> body = null, FactGroup<RelationshipFactSet> relationships = null)
        {
            PawnId = pawnId; SessionEpoch = epoch; ObservationRevision = revision; PublicationBatchId = publicationBatch;
            Context = context ?? FactGroup<PawnContextFact>.Unknown(); Needs = needs ?? FactGroup<NeedFactSet>.Unknown();
            Body = body ?? FactGroup<BodyLifeFactSet>.Unknown(); Relationships = relationships ?? FactGroup<RelationshipFactSet>.Unknown();
        }
    }
}
