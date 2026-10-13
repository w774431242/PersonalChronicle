using System;
using System.Collections.Generic;
using System.Threading;
using PersonalChronicle.Application.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy.Perception
{
    internal sealed partial class VanillaPawnFactCapture : IPawnPerceptionSource
    {
        private readonly PerceptionSettings settings;
        private readonly IVanillaLifeExtension extension;
        private readonly VanillaNeedRegistry registry;
        private readonly int ownerThread;
        private readonly Dictionary<string, Pawn> index = new Dictionary<string, Pawn>(StringComparer.Ordinal);
        private readonly Dictionary<string, Pawn> census = new Dictionary<string, Pawn>(StringComparer.Ordinal);
        private readonly Dictionary<string, SubjectLocation> departed = new Dictionary<string, SubjectLocation>(StringComparer.Ordinal);
        private readonly HashSet<string> diagnostics = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<Map> maps = new List<Map>();
        private int mapCursor, censusOmitted, getterErrors;
        private long nextCensus;
        private bool censusRunning, censusComplete;
        public VanillaPawnFactCapture(PerceptionSettings settings, IVanillaLifeExtension extension)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.extension = extension ?? throw new ArgumentNullException(nameof(extension));
            ownerThread = Thread.CurrentThread.ManagedThreadId;
            registry = new VanillaNeedRegistry(id => DefDatabase<NeedDef>.GetNamedSilentFail(id)?.needClass, Diagnose);
            extension.RegisterNeeds(registry);
        }
        private void MainThread()
        { if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Pawn perception is main-thread only."); }
        public void Reset()
        {
            MainThread(); index.Clear(); census.Clear(); departed.Clear(); maps.Clear(); diagnostics.Clear();
            mapCursor = censusOmitted = getterErrors = 0; nextCensus = 0; censusRunning = false;
        }
        private static bool Eligible(Pawn pawn) => pawn != null && !pawn.Dead && pawn.IsFreeNonSlaveColonist
            && pawn.RaceProps.Humanlike && !pawn.IsQuestLodger();
        public PopulationUpdate PollPopulation(int tick)
        {
            MainThread();
            if (!censusRunning)
            {
                if (tick < nextCensus) return null;
                maps.Clear(); census.Clear(); censusOmitted = 0; censusComplete = true; mapCursor = 0;
                var currentMaps = Find.Maps;
                for (int i = 0; i < currentMaps.Count && i < PerceptionSettings.MaximumMaps; i++) maps.Add(currentMaps[i]);
                if (currentMaps.Count > maps.Count) { censusComplete = false; censusOmitted += currentMaps.Count - maps.Count; }
                censusRunning = true;
            }
            if (mapCursor < maps.Count)
            {
                Map map = maps[mapCursor++];
                try
                {
                    if (Find.Maps.Contains(map))
                    {
                        // FreeColonists traverses held Pawns. Copy IDs/references immediately, never retain its temporary list.
                        var population = map.mapPawns.FreeColonists;
                        int examined = Math.Min(population.Count, settings.MaximumSubjects);
                        censusOmitted += population.Count - examined;
                        for (int i = 0; i < examined; i++)
                        {
                            var pawn = population[i];
                            if (!Eligible(pawn) || pawn.MapHeld != map) continue;
                            string id = pawn.GetUniqueLoadID();
                            if (census.Count >= settings.MaximumSubjects && !census.ContainsKey(id)) { censusOmitted++; continue; }
                            census[id] = pawn;
                        }
                    }
                }
                catch (Exception error) { censusComplete = false; Diagnose("Population", error); }
                if (mapCursor < maps.Count) return null;
            }
            // Keep a still-present Pawn that crossed maps during the non-atomic directory cycle.
            departed.Clear();
            foreach (var pair in index)
            {
                if (census.ContainsKey(pair.Key)) continue;
                try
                {
                    if (Eligible(pair.Value) && pair.Value.MapHeld != null && Find.Maps.Contains(pair.Value.MapHeld))
                    {
                        if (census.Count < settings.MaximumSubjects) census[pair.Key] = pair.Value;
                        else { censusComplete = false; censusOmitted++; }
                    }
                    else if (departed.Count < settings.MaximumSubjects)
                        departed[pair.Key] = pair.Value.Dead ? SubjectLocation.Dead : SubjectLocation.OffMap;
                }
                catch (Exception error) { censusComplete = false; Diagnose("PopulationDeparture", error); }
            }
            index.Clear();
            var subjects = new List<PerceptionSubject>(census.Count);
            foreach (var pair in census)
            {
                var pawn = pair.Value;
                if (!Eligible(pawn) || pawn.MapHeld == null || !Find.Maps.Contains(pawn.MapHeld)) continue;
                index[pair.Key] = pawn; subjects.Add(new PerceptionSubject(pair.Key, pawn.MapHeld.uniqueID));
            }
            maps.Clear(); census.Clear(); censusRunning = false; nextCensus = (long)tick + settings.PopulationInterval;
            return new PopulationUpdate(subjects, censusComplete && censusOmitted == 0, censusOmitted, departed);
        }
        public void Release(string pawnId) { MainThread(); index.Remove(pawnId); census.Remove(pawnId); departed.Remove(pawnId); }
        internal bool TryGetPlanningPawn(string pawnId, int mapId, out Pawn pawn)
        {
            MainThread();
            if (index.TryGetValue(pawnId, out pawn) && Eligible(pawn) && pawn.MapHeld?.uniqueID == mapId
                && Find.Maps.Contains(pawn.MapHeld)) return true;
            pawn = null; return false;
        }
        public PawnCaptureResult Capture(PawnCaptureRequest request)
        {
            MainThread();
            if (!index.TryGetValue(request.PawnId, out var pawn))
            {
                if (departed.TryGetValue(request.PawnId, out var location))
                    return new PawnCaptureResult(Group(new PawnContextFact(location, -1), request));
                return new PawnCaptureResult(new FactGroup<PawnContextFact>(CaptureState.Error, null,
                    request.Tick, request.Revision, "PopulationUnavailable"));
            }
            if (pawn.Dead || !Eligible(pawn) || pawn.MapHeld == null || !Find.Maps.Contains(pawn.MapHeld))
            {
                var terminal = new PawnContextFact(pawn.Dead ? SubjectLocation.Dead : SubjectLocation.OffMap, -1);
                index.Remove(request.PawnId);
                return new PawnCaptureResult(Group(terminal, request));
            }
            int before = getterErrors;
            var context = new PawnContextFact(pawn.Spawned ? SubjectLocation.SpawnedMap : SubjectLocation.HeldOnMap,
                pawn.MapHeld.uniqueID, Read(() => pawn.PositionHeld.x, "CellX"), Read(() => pawn.PositionHeld.z, "CellZ"),
                Read(() => pawn.DevelopmentalStage.ToString(), "DevelopmentalStage"), Read(() => pawn.Downed, "Downed"),
                Read(() => pawn.Drafted, "Drafted"), Read(() => pawn.InMentalState, "MentalState"),
                pawn.CurJob == null ? Fact<string>.NotApplicable("NoCurrentJob") : Read(() => pawn.CurJob.def.defName, "Job"),
                Read(() => pawn.CurJob?.playerForced == true, "PlayerForced"),
                pawn.timetable == null ? Fact<string>.NotApplicable("NoTimetable") : Read(() => pawn.timetable.CurrentAssignment.defName, "Timetable"),
                Read(() => pawn.LabelShortCap.ToString(), "DisplayName"));
            var contextGroup = Group(context, request, getterErrors != before);
            List<Need> needs = null;
            bool needsFailed = false;
            if ((request.Groups & (PerceptionGroups.Needs | PerceptionGroups.Body)) != 0)
            {
                try { needs = pawn.needs?.AllNeeds; }
                catch (Exception error) { needsFailed = true; Diagnose("AllNeeds", error); }
            }
            return new PawnCaptureResult(contextGroup,
                (request.Groups & PerceptionGroups.Needs) != 0 ? CaptureGroup(() => CaptureNeeds(needs, request, needsFailed), request, "Needs") : null,
                (request.Groups & PerceptionGroups.Body) != 0 ? CaptureGroup(() => CaptureBody(pawn, needs, request, needsFailed), request, "Body") : null,
                (request.Groups & PerceptionGroups.Relationships) != 0 ? CaptureGroup(() => CaptureRelationships(pawn, request), request, "Relationships") : null);
        }
        private FactGroup<NeedFactSet> CaptureNeeds(List<Need> needs, PawnCaptureRequest request, bool failed)
        {
            if (needs == null) return new FactGroup<NeedFactSet>(failed ? CaptureState.Error : CaptureState.Unknown,
                null, request.Tick, request.Revision, failed ? "NeedTrackerFailed" : "NeedTrackerUnavailable");
            var readings = new List<NumericNeedFact>(); var actual = new List<string>();
            var unsupported = new List<string>(); var errors = new List<string>();
            Fact<float> mood = Fact<float>.NotApplicable("MoodAbsent");
            int count = Math.Min(needs.Count, PerceptionSettings.MaximumNeeds);
            for (int i = 0; i < count; i++)
            {
                var need = needs[i]; string id = need?.def?.defName;
                if (string.IsNullOrWhiteSpace(id)) { errors.Add("MissingNeedIdentity"); continue; }
                actual.Add(id);
                try
                {
                    var reading = registry.ReadNumeric(need, request.Revision, request.Tick);
                    var ratio = CurrentNeedProjector.Project(request.PawnId, reading, default).ActualRatio;
                    if (registry.IsObservedMood(need)) mood = ratio;
                    else readings.Add(reading);
                    if (!reading.StandardRatioSupported || ratio.State == FactState.Unsupported) unsupported.Add(id);
                    if (reading.CurrentLevel.State == FactState.Error || reading.MaximumLevel.State == FactState.Error
                        || reading.VanillaCategory.State == FactState.Error || ratio.State == FactState.Error) errors.Add(id);
                }
                catch (Exception error) { errors.Add(id); Diagnose(id + ".capture", error); }
            }
            int truncated = needs.Count - count;
            return Group(new NeedFactSet(readings, actual, unsupported, errors, mood), request,
                truncated != 0 || unsupported.Count != 0 || errors.Count != 0, truncated);
        }
        private FactGroup<RelationshipFactSet> CaptureRelationships(Pawn pawn, PawnCaptureRequest request)
        {
            if (pawn.relations == null) return new FactGroup<RelationshipFactSet>(CaptureState.Unknown, null,
                request.Tick, request.Revision, "RelationshipTrackerUnavailable");
            var records = new List<RelationshipFact>(); int truncated = 0; bool partial = false;
            try
            {
                var relations = pawn.relations.DirectRelations;
                int count = Math.Min(relations.Count, PerceptionSettings.MaximumRelations); truncated = relations.Count - count;
                for (int i = 0; i < count; i++)
                {
                    var edge = relations[i];
                    if (edge?.def == null || edge.otherPawn == null) { partial = true; continue; }
                    records.Add(new RelationshipFact(edge.def.defName, edge.otherPawn.GetUniqueLoadID(), edge.startTicks));
                }
            }
            catch (Exception error) { partial = true; Diagnose("Relationships", error); }
            return Group(new RelationshipFactSet(records), request, partial || truncated != 0, truncated);
        }
        private static FactGroup<T> Group<T>(T data, PawnCaptureRequest request, bool partial = false, int truncated = 0) where T : class
            => new FactGroup<T>(partial ? CaptureState.Partial : CaptureState.Complete, data,
                request.Tick, request.Revision, partial ? "PartialCapture" : "Observed", truncated);
        private FactGroup<T> CaptureGroup<T>(Func<FactGroup<T>> capture, PawnCaptureRequest request, string name) where T : class
        {
            try { return capture(); }
            catch (Exception error)
            {
                getterErrors++; Diagnose(name + ".group", error);
                return new FactGroup<T>(CaptureState.Error, null, request.Tick, request.Revision, "GroupCaptureFailed");
            }
        }
        private Fact<T> Read<T>(Func<T> getter, string key)
        {
            try { return Fact<T>.Known(getter()); }
            catch (Exception error) { getterErrors++; Diagnose(key, error); return Fact<T>.Error("GetterFailed"); }
        }
        private void Diagnose(string key, Exception error)
        {
            if (diagnostics.Count >= PerceptionSettings.MaximumDiagnostics || !diagnostics.Add(key)) return;
            ChronicleLog.Warning("EconomyPerception", key + ": " + error.GetType().Name);
        }
    }
}
