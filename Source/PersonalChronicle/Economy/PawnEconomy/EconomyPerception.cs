using System;
using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Application.PawnEconomy.Perception;
using PersonalChronicle.Compatibility.Dlc;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Economy.PawnEconomy.Perception;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy
    {
        // Rebuildable, instance-owned observation cache. Deliberately not exposed to Scribe.
        private PerceptionCoordinator perception;
        private VanillaPawnFactCapture perceptionSource;
        private bool perceptionInitializationFailed;
        private readonly Game perceptionGame;
        private object perceptionInitializationToken;
        private void InvalidatePerception()
        {
            perceptionInitializationToken = null;
            perception = null; perceptionSource = null; perceptionInitializationFailed = false;
            InvalidateShadowPlanning();
            ResetNeedSpeech();
        }
        private void QueuePerceptionInitialization()
        {
            InvalidatePerception();
            var token = new object();
            perceptionInitializationToken = token;
            // FinalizeInit may run on the long-event loading worker. Bind ownership only on the main thread.
            LongEventHandler.ExecuteWhenFinished(() => TryInitializePerception(token));
        }
        private void TryInitializePerception(object token)
        {
            if (!UnityData.IsInMainThread || !ReferenceEquals(token, perceptionInitializationToken)
                || !ReferenceEquals(Verse.Current.Game, perceptionGame) || perceptionGame == null
                || perception != null || perceptionInitializationFailed) return;
            perceptionInitializationToken = null;
            InitializePerception();
        }
        private void InitializePerception()
        {
            perception = null; perceptionInitializationFailed = false;
            try
            {
                var settings = new PerceptionSettings();
                var source = new VanillaPawnFactCapture(settings, new PawnLifeBiotechAdapter());
                perception = new PerceptionCoordinator(source, settings, NewPerceptionEpoch);
                perceptionSource = source;
            }
            catch (Exception error)
            {
                perceptionInitializationFailed = true;
                ChronicleLog.Warning("EconomyPerception", "Initialization: " + error.GetType().Name);
            }
        }
        private static long NewPerceptionEpoch()
        {
            // Session nonce, not a persistent ID or a global mutable counter.
            long value = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0) & long.MaxValue;
            return value == 0 ? 1 : value;
        }
        private void TickPerception()
        {
            if (!ReferenceEquals(Verse.Current.Game, perceptionGame)) return;
            // Fallback if a loader invoked ExecuteWhenFinished before returning to the main thread.
            if (perceptionInitializationToken != null) TryInitializePerception(perceptionInitializationToken);
            if (perception != null && Find.TickManager != null) perception.Tick(Find.TickManager.TicksGame);
        }
        internal long PerceptionSessionEpoch => perception?.SessionEpoch ?? 0;
        internal IReadOnlyList<PerceptionSubject> ObservedSubjects => perception?.Subjects ?? Array.Empty<PerceptionSubject>();
        internal PerceptionDiagnostics PerceptionDiagnostics => perception?.Diagnostics;
        internal FactQueryResult QueryPawnFacts(PerceptionReadRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (perception == null || Find.TickManager == null)
                return new FactQueryResult(perceptionInitializationFailed ? PerceptionReadiness.Error : PerceptionReadiness.Loading,
                    perceptionInitializationFailed ? "PerceptionInitializationFailed" : "GameLoading", false);
            return perception.QueryFacts(request, Find.TickManager.TicksGame);
        }
        internal void MarkPerceptionDirty(string pawnId, PerceptionGroups groups) => perception?.MarkDirty(pawnId, groups);
        internal ObservationQueryResult QueryPawnObservation(PerceptionReadRequest request,
            IReadOnlyDictionary<string, NeedTarget> targets)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return perception == null || Find.TickManager == null
                ? new ObservationQueryResult(QueryPawnFacts(request), null)
                : perception.QueryObservation(request, Find.TickManager.TicksGame, targets);
        }
        internal Fact<EconomyAccountSnapshot> ReadEconomyAccount(string pawnId)
        { perception?.AssertOwnerThread(); return EconomyReadQueries.ReadAccount(State, pawnId); }
        internal Fact<EconomyPersonSnapshot> ReadEconomyPerson(string pawnId)
        { perception?.AssertOwnerThread(); return EconomyReadQueries.ReadPerson(State, pawnId); }
    }
}
