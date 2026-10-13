using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Application.PawnEconomy.Perception
{
    internal sealed class PerceptionSubject
    {
        public string PawnId { get; }
        public int MapId { get; }
        public PerceptionSubject(string pawnId, int mapId)
        {
            if (string.IsNullOrWhiteSpace(pawnId)) throw new ArgumentException("A pawn ID is required.", nameof(pawnId));
            PawnId = pawnId; MapId = mapId;
        }
    }
    internal sealed class PopulationUpdate
    {
        public IReadOnlyList<PerceptionSubject> Subjects { get; }
        public bool Complete { get; }
        public int OmittedCount { get; }
        public IReadOnlyDictionary<string, SubjectLocation> Departures { get; }
        public PopulationUpdate(IEnumerable<PerceptionSubject> subjects, bool complete, int omitted = 0,
            IDictionary<string, SubjectLocation> departures = null)
        {
            Subjects = PerceptionCollections.Freeze(subjects); Complete = complete; OmittedCount = omitted;
            Departures = new System.Collections.ObjectModel.ReadOnlyDictionary<string, SubjectLocation>(
                departures == null ? new Dictionary<string, SubjectLocation>() : new Dictionary<string, SubjectLocation>(departures));
        }
    }
    internal sealed class PawnCaptureRequest
    {
        public string PawnId { get; }
        public long SessionEpoch { get; }
        public long Revision { get; }
        public int Tick { get; }
        public PerceptionGroups Groups { get; }
        public PawnCaptureRequest(string pawnId, long epoch, long revision, int tick, PerceptionGroups groups)
        { PawnId = pawnId; SessionEpoch = epoch; Revision = revision; Tick = tick; Groups = groups; }
    }
    internal sealed class PawnCaptureResult
    {
        public FactGroup<PawnContextFact> Context { get; }
        public FactGroup<NeedFactSet> Needs { get; }
        public FactGroup<BodyLifeFactSet> Body { get; }
        public FactGroup<RelationshipFactSet> Relationships { get; }
        public PawnCaptureResult(FactGroup<PawnContextFact> context, FactGroup<NeedFactSet> needs = null,
            FactGroup<BodyLifeFactSet> body = null, FactGroup<RelationshipFactSet> relationships = null)
        { Context = context; Needs = needs; Body = body; Relationships = relationships; }
    }
    internal interface IPawnPerceptionSource
    {
        void Reset();
        // One directory work unit per call. Null means the cycle is pending/not due.
        PopulationUpdate PollPopulation(int tick);
        PawnCaptureResult Capture(PawnCaptureRequest request);
        void Release(string pawnId);
    }
    internal sealed class PerceptionSettings
    {
        public int FastInterval { get; }
        public int BodyInterval { get; }
        public int RelationshipInterval { get; }
        public int PopulationInterval { get; }
        public int MaximumCapturesPerTick { get; }
        public int MaximumSubjects { get; }
        public int MaximumRetired { get; }
        public double CaptureBudgetMilliseconds { get; }
        public const int MaximumMaps = 64, MaximumNeeds = 64, MaximumHediffs = 128;
        public const int MaximumApparel = 64, MaximumRelations = 128, MaximumDiagnostics = 32;
        public PerceptionSettings(int fast = 150, int body = 250, int relationships = 600,
            int population = 600, int maxCaptures = 8, int maxSubjects = 4096, int maxRetired = 512,
            double budgetMilliseconds = 2d)
        {
            if (fast < 1 || body < 1 || relationships < 1 || population < 1 || maxCaptures < 1
                || maxSubjects < 1 || maxRetired < 0 || budgetMilliseconds <= 0
                || double.IsNaN(budgetMilliseconds) || double.IsInfinity(budgetMilliseconds))
                throw new ArgumentOutOfRangeException(nameof(fast));
            FastInterval = fast; BodyInterval = body; RelationshipInterval = relationships;
            PopulationInterval = population; MaximumCapturesPerTick = maxCaptures;
            MaximumSubjects = maxSubjects; MaximumRetired = maxRetired; CaptureBudgetMilliseconds = budgetMilliseconds;
        }
    }
    internal sealed class PerceptionDiagnostics
    {
        public int ActiveSubjects { get; }
        public int RetiredSubjects { get; }
        public long Captures { get; }
        public long CaptureErrors { get; }
        public long BudgetExceededTicks { get; }
        public double LastCaptureMilliseconds { get; }
        public double LastPopulationMilliseconds { get; }
        public long OldestDueAgeTicks { get; }
        public int PopulationTick { get; }
        public bool PopulationComplete { get; }
        public int OmittedSubjects { get; }
        public PerceptionDiagnostics(int active, int retired, long captures, long errors, long overruns,
            double captureMs, double populationMs, long oldestDue, int populationTick, bool complete, int omitted)
        {
            ActiveSubjects = active; RetiredSubjects = retired; Captures = captures; CaptureErrors = errors;
            BudgetExceededTicks = overruns; LastCaptureMilliseconds = captureMs; LastPopulationMilliseconds = populationMs;
            OldestDueAgeTicks = oldestDue; PopulationTick = populationTick; PopulationComplete = complete; OmittedSubjects = omitted;
        }
    }
}
