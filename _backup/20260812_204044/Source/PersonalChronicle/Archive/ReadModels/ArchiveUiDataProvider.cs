using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Api;
using PersonalChronicle.Api.DomainProviders;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;
using UnityEngine;

namespace PersonalChronicle.Archive.ReadModels
{
    /// <summary>
    /// Default <see cref="IArchiveUiDataProvider"/> (P2-6). Centralizes every
    /// section's "query + sort + filter + null-guard" responsibility so the main
    /// window's draw path only consumes immutable snapshots. The window still owns
    /// translation/formatting (its translation context), but never issues raw
    /// <c>IArchiveService</c> ordering LINQ — that lives here.
    ///
    /// Failure isolation: a null service or a throwing query yields an empty snapshot
    /// rather than propagating into the UI draw loop.
    /// </summary>
    public sealed class ArchiveUiDataProvider : IArchiveUiDataProvider
    {
        public HomeSnapshot BuildHome(IArchiveService service, long revision)
        {
            HomeSnapshot snap = new HomeSnapshot { BuiltFromRevision = revision, IsEmpty = true };
            if (service == null)
            {
                return snap;
            }
            snap.ActivePawnCount = service.GetActiveSnapshotCount();
            snap.ArchivedPawnCount = service.GetArchivedSnapshotCount();
            snap.LiveColonistCount = service.GetLiveColonistCount();
            int free, slave, prisoner;
            service.GetLiveColonistCounts(out free, out slave, out prisoner);
            snap.LiveFreeCount = free;
            snap.LiveSlaveCount = slave;
            snap.LivePrisonerCount = prisoner;
            snap.ServiceDays = service.GetServiceDays();

            IReadOnlyList<ChronicleEvent> recent = service.GetRecentEvents(20);
            if (recent != null && recent.Count > 0)
            {
                snap.RecentEvents = recent.Where(e => e != null).ToList();
            }

            // Honest "important" proxy: the objects (Pawn + Thing) with the most
            // events. Mirrors the window's CollectCandidates ordering. Battle/Location
            // are excluded because they have no detail view.
            List<ArchiveObject> candidates = new List<ArchiveObject>();
            CollectCandidatesByEvents(service, ArchiveCategoryKeys.Pawn, candidates);
            CollectCandidatesByEvents(service, ArchiveCategoryKeys.Thing, candidates);
            // Prefetch counts once: every Sort comparison would otherwise issue a
            // GetEventsFor query against the store per object pair.
            Dictionary<string, int> countCache = new Dictionary<string, int>();
            candidates.Sort((a, b) =>
            {
                int byCount = EventCount(service, countCache, b.StableId)
                    .CompareTo(EventCount(service, countCache, a.StableId));
                // Deterministic tie-breaker: identical event counts must not reorder
                // between sessions (List<T>.Sort is unstable). StableId is a stable key.
                return byCount != 0 ? byCount : string.CompareOrdinal(a.StableId, b.StableId);
            });
            snap.ImportantObjects = candidates.Take(6).ToList();

            snap.IsEmpty = snap.RecentEvents.Count == 0 && snap.ImportantObjects.Count == 0;
            return snap;
        }

        public OverviewSnapshot BuildOverview(IArchiveService service, string categoryKey, long revision)
        {
            OverviewSnapshot snap = new OverviewSnapshot { BuiltFromRevision = revision, IsEmpty = true };
            if (service == null || string.IsNullOrEmpty(categoryKey))
            {
                return snap;
            }
            IReadOnlyList<ArchiveObject> objects = service.GetObjectsOfCategory(categoryKey);
            if (objects != null && objects.Count > 0)
            {
                Dictionary<string, int> countCache = new Dictionary<string, int>();
                snap.CategoryObjects[categoryKey] = objects
                    .Where(o => o != null)
                    .OrderByDescending(o => EventCount(service, countCache, o.StableId))
                    .ThenBy(o => o.StableId, System.StringComparer.Ordinal)
                    .ToList();
                // v4.14: Location atlas KPI strip + per-location event counts —
                // aggregated here (Read Model), the window renders only.
                if (categoryKey == ArchiveCategoryKeys.Location)
                {
                    snap.LocationKpis = BuildLocationKpis(snap.CategoryObjects[categoryKey]);
                    snap.LocationEventCounts = BuildLocationEventCounts(
                        service, countCache, snap.CategoryObjects[categoryKey]);
                }
                // v4.14: Battle KPI strip + per-battle card aggregates — Read Model.
                else if (categoryKey == ArchiveCategoryKeys.Battle)
                {
                    snap.BattleKpis = BuildBattleKpis(service, snap.CategoryObjects[categoryKey]);
                }
            }
            snap.IsEmpty = snap.CategoryObjects.Count == 0;
            return snap;
        }

        /// <summary>
        /// v4.14: aggregates the 5-cell Battle KPI strip + per-battle card views
        /// from the BattleObject snapshot list. Kills/losses come from the
        /// battle-scoped Death events (the battle stable id appears in the event
        /// Subjects). Significance delegates to <see cref="IBattleProvider"/>.
        /// Pure Read-Model aggregation — the window never re-derives these.
        /// </summary>
        private static BattleKpisView BuildBattleKpis(
            IArchiveService service, List<ArchiveObject> objects)
        {
            BattleKpisView kpi = new BattleKpisView();
            if (service == null || objects == null || objects.Count == 0)
            {
                return kpi;
            }
            // Kill vs our-loss discrimination: a Death event whose Subject
            // contains the battle AND whose Params carry the victim category.
            // Our losses = victim faction is the player's; kills = CombatRole kill
            // (victim is an enemy). Reuse the same event stream per battle.
            Dictionary<string, List<ChronicleEvent>> eventsByBattle =
                new Dictionary<string, List<ChronicleEvent>>(System.StringComparer.Ordinal);
            for (int i = 0; i < objects.Count; i++)
            {
                BattleObject battle = objects[i] as BattleObject;
                if (battle == null || string.IsNullOrEmpty(battle.StableId))
                {
                    continue;
                }
                kpi.Total++;
                BattleCardView card = new BattleCardView
                {
                    RaidCount = battle.RaidCount,
                    Participants = battle.ParticipantIds != null ? battle.ParticipantIds.Count : 0,
                    ThreatKey = battle.ThreatKey,
                    IsSignificant = ResolveBattleSignificance(service, battle)
                };
                // Battle-scoped Death events (this battle in Subjects).
                List<ChronicleEvent> evs;
                if (!eventsByBattle.TryGetValue(battle.StableId, out evs))
                {
                    evs = CollectBattleEvents(service, battle.StableId);
                    eventsByBattle[battle.StableId] = evs;
                }
                for (int e = 0; e < evs.Count; e++)
                {
                    ChronicleEvent ev = evs[e];
                    if (ev == null)
                    {
                        continue;
                    }
                    bool isKill = IsBattleKillEvent(ev);
                    if (isKill)
                    {
                        card.Kills++;
                    }
                    else
                    {
                        card.Losses++;
                    }
                }
                if (card.IsSignificant)
                {
                    kpi.Decisive++;
                }
                kpi.Kills += card.Kills;
                kpi.Losses += card.Losses;
                kpi.Roster += card.Participants;
                kpi.Cards[battle.StableId] = card;
            }
            return kpi;
        }

        /// <summary>
        /// v4.14: collects the events whose Subjects reference this battle stable id.
        /// </summary>
        private static List<ChronicleEvent> CollectBattleEvents(
            IArchiveService service, string battleStableId)
        {
            List<ChronicleEvent> result = new List<ChronicleEvent>();
            if (service == null || string.IsNullOrEmpty(battleStableId))
            {
                return result;
            }
            IReadOnlyList<ChronicleEvent> all = service.GetAllEvents();
            if (all == null)
            {
                return result;
            }
            for (int i = 0; i < all.Count; i++)
            {
                ChronicleEvent ev = all[i];
                if (ev == null || ev.Subjects == null)
                {
                    continue;
                }
                for (int s = 0; s < ev.Subjects.Count; s++)
                {
                    ObjectRef sub = ev.Subjects[s];
                    if (sub != null
                        && sub.CategoryKey == ArchiveCategoryKeys.Battle
                        && sub.StableId == battleStableId)
                    {
                        result.Add(ev);
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// v4.14: distinguishes a kill (our colony killed an enemy) from an own
        /// loss. Only Death-type events are counted (kills and losses are both
        /// Death events; the kill role is distinguished by the CombatRole param).
        /// Non-Death events attached to the battle (e.g. the battle start event's
        /// subject edges) are never counted as casualties.
        /// </summary>
        private static bool IsBattleKillEvent(ChronicleEvent ev)
        {
            if (ev == null || ev.TypeKey != ChronicleEventType.Death || ev.Params == null)
            {
                return false;
            }
            if (ev.Params.TryGetValue(ChronicleEventParams.CombatRole, out string role)
                && role == ChronicleEventParams.CombatRoleKill)
            {
                return true;
            }
            // No explicit kill role → our side died (a colony pawn Death event
            // linked to this battle via AttachCombatSubjects).
            return false;
        }

        /// <summary>
        /// v4.14: significance via the battle provider chain (IBattleProvider).
        /// Falls back to threat-key (ThreatBig = significant) when no provider
        /// is registered or the provider is undefined.
        /// </summary>
        private static bool ResolveBattleSignificance(IArchiveService service, BattleObject battle)
        {
            if (service == null || battle == null)
            {
                return false;
            }
            bool defined = false;
            bool significant = false;
            try
            {
                PersonalChronicle.Api.IPersonalChronicleApi api;
                if (PersonalChronicle.Api.PersonalChronicleApi.TryGet(out api)
                    && api != null && api.Providers != null)
                {
                    Dictionary<string, string> dataKeys = new Dictionary<string, string>
                    {
                        { "outcome", battle.ThreatKey ?? string.Empty }
                    };
                    BattleAppraisalInput input =
                        new BattleAppraisalInput(battle.StableId, dataKeys);
                    api.Providers.ForEach<IBattleProvider>(
                        provider =>
                        {
                            if (defined || provider == null)
                            {
                                return;
                            }
                            BattleAppraisal appraisal;
                            if (provider.TryAppraise(input, out appraisal))
                            {
                                defined = true;
                                significant = appraisal.IsSignificant;
                            }
                        });
                }
            }
            catch (System.Exception)
            {
                defined = false;
            }
            if (!defined)
            {
                return battle.ThreatKey == "ThreatBig";
            }
            return significant;
        }

        /// <summary>
        /// v4.14: aggregates the 8-cell Location KPI strip from the snapshot list.
        /// Kind classification reuses <see cref="ResolveLocationKindKey"/> so the
        /// card and the strip always agree. Pure Read-Model aggregation — the
        /// window never re-derives these counters.
        /// </summary>
        private static LocationKpisView BuildLocationKpis(List<ArchiveObject> objects)
        {
            LocationKpisView kpi = new LocationKpisView();
            if (objects == null || objects.Count == 0)
            {
                return kpi;
            }
            HashSet<string> factions = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < objects.Count; i++)
            {
                LocationObject loc = objects[i] as LocationObject;
                if (loc == null)
                {
                    continue;
                }
                kpi.Total++;
                string kind = ResolveLocationKindKey(loc);
                if (kind == "player") kpi.Home++;
                else if (kind == "quest") kpi.Quest++;
                else if (kind == "settle") kpi.Settle++;
                if (loc.DeinitTick != -1L) kpi.Ruined++;
                if (loc.CanTrade)
                {
                    kpi.Tradable++;
                    if (!string.IsNullOrEmpty(loc.PermitRequiredDefName)) kpi.Permit++;
                }
                if (loc.IsPlayerHome)
                {
                    factions.Add("player");
                }
                else if (!string.IsNullOrEmpty(loc.FactionDefName))
                {
                    factions.Add(loc.FactionDefName);
                }
            }
            kpi.Factions = factions.Count;
            return kpi;
        }

        /// <summary>
        /// v4.14: per-location event counts for the atlas card sub-line, reusing
        /// the same count cache as the overview ordering (no extra store queries).
        /// </summary>
        private static Dictionary<string, int> BuildLocationEventCounts(
            IArchiveService service, Dictionary<string, int> countCache, List<ArchiveObject> objects)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            if (service == null || objects == null || objects.Count == 0)
            {
                return counts;
            }
            for (int i = 0; i < objects.Count; i++)
            {
                LocationObject loc = objects[i] as LocationObject;
                if (loc == null || string.IsNullOrEmpty(loc.StableId))
                {
                    continue;
                }
                counts[loc.StableId] = EventCount(service, countCache, loc.StableId);
            }
            return counts;
        }

        /// <summary>
        /// v4.14: canonical location kind key — "player"/"settle"/"quest"/"unknown".
        /// Single source of truth for both the card (LocationDetailView) and the
        /// KPI strip aggregation; never duplicated in the window.
        /// </summary>
        private static string ResolveLocationKindKey(LocationObject loc)
        {
            if (loc == null)
            {
                return "unknown";
            }
            if (loc.IsPlayerHome)
            {
                return "player";
            }
            if (!string.IsNullOrEmpty(loc.WorldObjectDefName))
            {
                if (loc.WorldObjectDefName.IndexOf("Settlement", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "settle";
                }
                if (loc.WorldObjectDefName.IndexOf("Quest", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || loc.WorldObjectDefName.IndexOf("Site", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "quest";
                }
            }
            return "unknown";
        }

        public DetailSnapshot BuildDetail(IArchiveService service, string detailObjectId, long revision)
        {
            DetailSnapshot snap = new DetailSnapshot { BuiltFromRevision = revision, IsEmpty = true };
            if (service == null || string.IsNullOrEmpty(detailObjectId))
            {
                return snap;
            }
            snap.DetailObject = service.GetObject(detailObjectId);
            IReadOnlyList<ChronicleEvent> events = service.GetEventsFor(detailObjectId);
            // v4.5.4: ordering / null-guard belong to the Read Model (v4.3 boundary) —
            // the window consumes the snapshot already sorted ascending by tick.
            snap.RawEvents = (events == null)
                ? (IReadOnlyList<ChronicleEvent>)new List<ChronicleEvent>()
                : events.Where(e => e != null).OrderBy(e => e.Tick).ToList();
            snap.IsEmpty = snap.RawEvents.Count == 0;

            // v4.6.5: production ledger derived here (Read Model), not in the window.
            // Group crafted/built events by defName, keep latest, sort by recency.
            snap.ProductionLines = BuildProductionLines(events);

            // v4.4: derive Pawn Overview content. All derivation is centralized here
            // (architecture §3.1 G layer) — the window only consumes these views.
            // Failure isolation: any throw yields empty derived lists, not a broken UI.
            try
            {
                if (snap.DetailObject is PawnObject pawn)
                {
                    snap.LifePhases = BuildLifePhases(pawn);
                    snap.CareerBars = BuildCareerBars(service, detailObjectId);
                    snap.Footprint = BuildFootprint(pawn);
                    snap.Milestones = BuildMilestones(events);
                    snap.Health = BuildHealth(service, detailObjectId);
                    snap.Relations = BuildRelations(service, pawn);
                    // v4.15 condense tab core KPI: aggregate the six digest cells here
                    // in the Read Model only — the ITab renders, never computes.
                    BuildDetailCoreKpis(snap, service, detailObjectId, pawn);
                }
                else if (snap.DetailObject is ThingObject thing)
                {
                    // v4.7: legacy chain (传承) for equipment — ownership-transfer
                    // generations, creator, verdict, holder table.
                    snap.Legacy = BuildLegacy(service, thing, events);
                    // v4.9: equipment legacy extension — 溯源 / 工坊署名链 /
                    // 同袍共用 / 退役仪式. All read-model derived, empty-safe.
                    snap.Origin = BuildOrigin(service, thing, events);
                    snap.MakerChain = BuildMakerChain(service, thing, snap.Origin);
                    snap.CoUse = BuildCoUse(service, thing, events);
                    snap.Decommission = BuildDecommission(service, thing, events);
                }
                else if (snap.DetailObject is LocationObject location)
                {
                    // v4.13 location atlas: identity/ownership/geography/lifecycle/
                    // commerce, all read-model derived (the window only renders).
                    snap.Location = BuildLocation(location);
                }
                snap.KeyEvents = BuildKeyEvents(events);
            }
            catch (System.Exception ex)
            {
                // Derivation must never break the detail view; fall back to empty.
                Log.Warning("[PersonalChronicle] Overview derivation failed for "
                    + detailObjectId + ": " + ex.Message);
                snap.LifePhases = new List<LifePhaseView>();
                snap.CareerBars = new List<CareerBarView>();
                snap.Footprint = new FootprintLedgerView();
                snap.Milestones = new List<MilestoneView>();
                snap.KeyEvents = new List<KeyEventView>();
                snap.Health = new HealthView();
                snap.Legacy = new LegacyView();
                snap.Relations = new List<RelationView>();
            }
            return snap;
        }

        // ---- v4.15 condense tab core KPI (Read Model only) ----
        // Aggregates the six digest cells consumed by ITab_Pawn_Chronicle. Every
        // counter is computed once here, never in the draw path.
        private static void BuildDetailCoreKpis(
            DetailSnapshot snap, IArchiveService service, string stableId, PawnObject pawn)
        {
            if (snap == null || pawn == null) return;

            // 工时: reuse the work-intensity evaluator via IWorkIntensityService.
            if (service != null && !string.IsNullOrEmpty(stableId))
            {
                IWorkIntensityService intensityService = service as IWorkIntensityService;
                if (intensityService != null)
                {
                    snap.WorkIntensity = intensityService.GetWorkIntensity(stableId); // null when undefined
                }
                ProductionSummaryView prod = service.GetProductionSummary(stableId);
                snap.ProductionTotal = (prod == null) ? 0 : prod.TotalQuantity;
                snap.ProductionSilverValue = (prod == null) ? 0f : prod.TotalMarketValue;
                snap.ProductionCategories = BuildProductionCategories(snap.ProductionLines);
            }

            // 击杀: Death events where this pawn is the killer (CombatRole == kill).
            // Counts the total and groups by victim faction/category for the digest.
            string killerLabel = (pawn.LabelShort ?? string.Empty).Trim();
            int kills = 0;
            Dictionary<string, int> byFaction = new Dictionary<string, int>();
            foreach (ChronicleEvent e in snap.RawEvents)
            {
                if (e == null || e.TypeKey != ChronicleEventType.Death) continue;
                if (!e.Params.TryGetValue(ChronicleEventParams.CombatRole, out string role)
                    || role != ChronicleEventParams.CombatRoleKill) continue;
                if (!e.Params.TryGetValue(ChronicleEventParams.Killer, out string killer)
                    || !string.Equals((killer ?? string.Empty).Trim(), killerLabel, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                kills++;
                // Group key: victim faction label when known, else victim category
                // (humanlike enemies are usually factioned; mechs/animals fall back).
                string group = null;
                if (e.Params.TryGetValue(ChronicleEventParams.VictimFactionLabel, out string vfl)
                    && !string.IsNullOrEmpty(vfl))
                {
                    group = vfl;
                }
                else if (e.Params.TryGetValue(ChronicleEventParams.VictimCategory, out string vcat)
                    && !string.IsNullOrEmpty(vcat))
                {
                    group = VictimCategoryLabel(vcat);
                }
                if (string.IsNullOrEmpty(group)) group = ChronicleEventParams.UnknownKillerLabel.Translate().ToString();
                int prev;
                byFaction.TryGetValue(group, out prev);
                byFaction[group] = prev + 1;
            }
            snap.Kills = kills;
            snap.KillsByFaction = BuildKillsByFaction(byFaction);

            // 战役: colony-level Battle events are not bound to a single pawn, so the
            // digest shows the colony's battle count as this pawn's era context.
            int battles = 0;
            foreach (ChronicleEvent e in snap.RawEvents)
            {
                if (e != null && e.TypeKey == ChronicleEventType.Battle) battles++;
            }
            snap.BattleCount = battles;

            // 战役 KPI 条（歼敌/损失/参战规模/重大战局）：colony 级，作为该人物的时代背景。
            // 复用 Overview 的 Battle 分类聚合，窗口只消费。
            IReadOnlyList<ArchiveObject> battleObjects = (service != null)
                ? service.GetObjectsOfCategory(ArchiveCategoryKeys.Battle)
                : null;
            if (battleObjects != null)
            {
                snap.BattleKpis = BuildBattleKpis(service, battleObjects.ToList());
            }

            // 传承: relations that read as a living descendant (child/offspring).
            int offspring = 0;
            foreach (RelationView r in snap.Relations)
            {
                if (r == null || !r.IsLive) continue;
                string label = r.RelationLabel ?? string.Empty;
                if (label.IndexOf("子", System.StringComparison.Ordinal) >= 0
                    || label.IndexOf("女", System.StringComparison.Ordinal) >= 0
                    || label.IndexOf("后代", System.StringComparison.Ordinal) >= 0
                    || label.IndexOf("嗣", System.StringComparison.Ordinal) >= 0
                    || label.IndexOf("child", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || label.IndexOf("offspring", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || label.IndexOf("son", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || label.IndexOf("daughter", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    offspring++;
                }
            }
            snap.LegacyOffspring = offspring;
        }

        // ---- v4.4 Pawn Overview derivation (Read Model only) ----

        private static IReadOnlyList<LifePhaseView> BuildLifePhases(PawnObject pawn)
        {
            List<LifePhaseView> phases = new List<LifePhaseView>();
            if (pawn == null) return phases;

            // Origin (backstory) — always present as the narrative root.
            phases.Add(new LifePhaseView
            {
                PhaseKey = "PersonalChronicle.UI.LifePhase.Origin",
                IconKey = "🌱",
                DateText = null,
                SubText = BackstoryText(pawn),
                IsUnknown = false,
                Kind = LifePhaseKind.Origin
            });

            // Join — only when we have a real join tick (mid-install JoinTick=-1 skips).
            // Note: tick 0 is valid for pawns generated at the very start of a new colony.
            if (pawn.JoinTick >= 0L)
            {
                phases.Add(new LifePhaseView
                {
                    PhaseKey = "PersonalChronicle.UI.LifePhase.Join",
                    IconKey = "🚪",
                    DateText = FormatDateLocal(pawn.JoinTick),
                    SubText = FactionText(pawn),
                    IsUnknown = false,
                    Kind = LifePhaseKind.Join
                });
            }
            else
            {
                phases.Add(new LifePhaseView
                {
                    PhaseKey = "PersonalChronicle.UI.LifePhase.JoinUnknown",
                    IconKey = "⚠",
                    DateText = null,
                    SubText = "PersonalChronicle.UI.LifePhase.JoinUnknownSub".Translate().ToString(),
                    IsUnknown = true,
                    Kind = LifePhaseKind.Unknown
                });
            }

            // Active span.
            long activeEnd = pawn.IsArchived && pawn.DeathTick > 0L
                ? pawn.DeathTick
                : Find.TickManager.TicksGame;
            string activeDate = null;
            if (pawn.JoinTick >= 0L && activeEnd > pawn.JoinTick)
            {
                activeDate = FormatDateLocal(pawn.JoinTick) + " → " + FormatDateLocal(activeEnd)
                    + " (" + SpanText.Format(activeEnd - pawn.JoinTick) + ")";
            }
            string activeSub = pawn.IsArchived
                ? "PersonalChronicle.UI.LifePhase.ActiveSub.Archived".Translate().ToString()
                : "PersonalChronicle.UI.LifePhase.ActiveSub.Alive".Translate().ToString();
            phases.Add(new LifePhaseView
            {
                PhaseKey = "PersonalChronicle.UI.LifePhase.Active",
                IconKey = "⏳",
                DateText = activeDate,
                SubText = activeSub,
                IsUnknown = false,
                Kind = LifePhaseKind.Active
            });

            // Death — only when archived.
            if (pawn.IsArchived)
            {
                phases.Add(new LifePhaseView
                {
                    PhaseKey = "PersonalChronicle.UI.LifePhase.Death",
                    IconKey = "💀",
                    DateText = FormatDateLocal(pawn.DeathTick),
                    SubText = DeathText(pawn),
                    IsUnknown = false,
                    Kind = LifePhaseKind.Death
                });
            }
            return phases;
        }

        private static IReadOnlyList<CareerBarView> BuildCareerBars(IArchiveService service, string id)
        {
            List<CareerBarView> bars = new List<CareerBarView>();
            if (service == null) return bars;
            IReadOnlyList<WorkTimeStatView> stats = service.GetWorkTimeStats(id);
            if (stats == null || stats.Count == 0) return bars;

            long total = 0L;
            for (int i = 0; i < stats.Count; i++) total += stats[i].Ticks;
            if (total <= 0L) return bars;

            // Top 5 by ticks.
            List<WorkTimeStatView> top = new List<WorkTimeStatView>(stats);
            top.Sort((a, b) => b.Ticks.CompareTo(a.Ticks));
            int take = System.Math.Min(5, top.Count);
            for (int i = 0; i < take; i++)
            {
                WorkTimeStatView w = top[i];
                bars.Add(new CareerBarView
                {
                    WorkTypeLabel = WorkTypeLabelLocal(w.WorkTypeDefName),
                    Ticks = w.Ticks,
                    Share01 = (float)w.Ticks / total,
                    IsPrimary = i == 0,
                    IsSecondary = i == 1
                });
            }
            return bars;
        }

        private static FootprintLedgerView BuildFootprint(PawnObject pawn)
        {
            FootprintLedgerView led = new FootprintLedgerView();
            if (pawn == null || pawn.PlaceHistory == null || pawn.PlaceHistory.Count == 0)
            {
                return led;
            }
            List<FootstepView> stays = new List<FootstepView>();
            int homeIdx = -1;
            long homeDays = -1L;
            int expeditions = 0;
            long now = Find.TickManager.TicksGame;

            for (int i = 0; i < pawn.PlaceHistory.Count; i++)
            {
                PlaceVisit v = pawn.PlaceHistory[i];
                if (v == null) continue;
                bool isWorld = v.PlaceKind == PlaceVisitKeys.KindCaravan
                    || (v.PlaceKey != null && v.PlaceKey.StartsWith(PlaceVisitKeys.TileKeyPrefix, System.StringComparison.Ordinal));
                if (isWorld) expeditions++;
                long enter = v.EnterTick > 0L ? v.EnterTick : -1L;
                long leave = v.IsOpen ? now : (v.LeaveTick > 0L ? v.LeaveTick : -1L);
                long dwellTicks = (enter > 0L && leave > 0L && leave >= enter) ? (leave - enter) : -1L;
                long days = dwellTicks >= 0L ? (long)RimWorld.GenDate.TicksToDays((int)dwellTicks) : -1L;
                if (days > homeDays) { homeDays = days; homeIdx = stays.Count; }
                stays.Add(new FootstepView
                {
                    PlaceText = PlaceTextLocal(v),
                    IsWorldTile = isWorld,
                    DwellText = dwellTicks >= 0L ? SpanText.Format(dwellTicks) : "PersonalChronicle.UI.UnknownDate".Translate().ToString(),
                    DwellTicks = dwellTicks,
                    IsHome = false
                });
            }

            // Longest dwell first (raw tick span, never string parsing).
            stays.Sort((a, b) => b.DwellTicks.CompareTo(a.DwellTicks));
            if (homeIdx >= 0 && homeIdx < stays.Count) stays[homeIdx].IsHome = true;

            led.PlaceCount = pawn.PlaceHistory.Count;
            led.HomePlaceText = homeIdx >= 0 ? stays[homeIdx].PlaceText : null;
            led.HomeDays = homeDays >= 0 ? (int)homeDays : 0;
            led.ExpeditionCount = expeditions;
            led.Stays = stays;
            return led;
        }

        // ---- v4.13 location atlas derivation (Read Model only) ----

        /// <summary>
        /// Derives the location detail view (identity / ownership / geography /
        /// lifecycle / commerce) from a LocationObject. Pure data-key derivation —
        /// the window owns translation/formatting. Empty-safe.
        /// </summary>
        private static LocationDetailView BuildLocation(LocationObject loc)
        {
            LocationDetailView view = new LocationDetailView();
            if (loc == null)
            {
                return view;
            }
            view.EstablishedTick = loc.EstablishedTick;
            view.IsActive = loc.DeinitTick == -1L;
            view.DeinitReasonKey = view.IsActive ? null : loc.DeinitReason;
            view.FactionDefName = loc.FactionDefName;
            view.BiomeDefName = loc.MapDefName;

            // Kind key: player home / faction settlement / quest site / unknown.
            view.KindKey = ResolveLocationKindKey(loc);

            // Hill key.
            if (string.IsNullOrEmpty(loc.Hilliness))
            {
                view.HillKey = null;
            }
            else if (loc.Hilliness == "Flat") view.HillKey = "flat";
            else if (loc.Hilliness == "Hilly") view.HillKey = "hilly";
            else if (loc.Hilliness == "Mountainous") view.HillKey = "mountain";
            else if (loc.Hilliness == "Impassable") view.HillKey = "impassable";
            else view.HillKey = null;

            view.IsCoastal = loc.IsCoastal;
            view.IsPolluted = loc.Pollution > 0.001f;
            view.AvgTempC = loc.AvgTempC;

            // Commerce.
            view.CanTrade = loc.CanTrade;
            view.PermitDefName = loc.PermitRequiredDefName;
            if (loc.TradeKindKeys != null)
            {
                view.TradeKindKeys = loc.TradeKindKeys;
            }
            return view;
        }

        private static IReadOnlyList<MilestoneView> BuildMilestones(IReadOnlyList<ChronicleEvent> events)
        {
            List<MilestoneView> ms = new List<MilestoneView>();
            if (events == null || events.Count == 0) return ms;

            // One representative per kind; Other excluded (avoids noise).
            Dictionary<string, ChronicleEvent> best = new Dictionary<string, ChronicleEvent>();
            for (int i = 0; i < events.Count; i++)
            {
                ChronicleEvent ev = events[i];
                if (ev == null) continue;
                ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
                if (def == null) continue;
                if (def.kind == ChronicleEventKind.Other) continue;
                int imp = (int)ChronicleEventImportance.Resolve(ev);
                string kind = def.kind.ToString();
                if (!best.TryGetValue(kind, out ChronicleEvent cur)
                    || imp > (int)ChronicleEventImportance.Resolve(cur))
                {
                    best[kind] = ev;
                }
            }
            foreach (var kv in best)
            {
                ChronicleEvent ev = kv.Value;
                ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
                ms.Add(new MilestoneView
                {
                    IconKey = EventGlyph(def),
                    TitleText = EventTitleLocal(ev),
                    DateText = FormatDateLocal(ev.Tick),
                    SubText = EventSubLocal(ev),
                    KindKey = kv.Key,
                    RawTick = ev.Tick
                });
            }
            // Chronological order; unknown dates sink to the end.
            ms.Sort((a, b) => a.RawTick.CompareTo(b.RawTick));
            return ms;
        }

        private static IReadOnlyList<KeyEventView> BuildKeyEvents(IReadOnlyList<ChronicleEvent> events)
        {
            List<KeyEventView> list = new List<KeyEventView>();
            if (events == null) return list;
            // Deduplicate by (TypeKey, Tick) to avoid duplicate death/battle records
            // from multiple capture points (e.g. Death recorded twice in the same tick).
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < events.Count; i++)
            {
                ChronicleEvent ev = events[i];
                if (ev == null) continue;
                string dedup = (ev.TypeKey ?? "") + ":" + ev.Tick;
                if (!seen.Add(dedup)) continue;
                ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
                if (def == null) continue;
                int kindWeight = KindWeight(def.kind);
                int imp = (int)ChronicleEventImportance.Resolve(ev);
                int salience = kindWeight + imp;
                list.Add(new KeyEventView
                {
                    IconKey = EventGlyph(def),
                    DateText = FormatDateLocal(ev.Tick),
                    TitleText = EventTitleLocal(ev),
                    TypeText = KindLabelLocal(def.kind),
                    IsHighlight = salience >= 90,
                    Salience = salience,
                    RawTick = ev.Tick
                });
            }
            // Top 3 by salience, then chronological.
            list.Sort((a, b) => b.Salience.CompareTo(a.Salience));
            if (list.Count > 3) list.RemoveRange(3, list.Count - 3);
            list.Sort((a, b) => a.RawTick.CompareTo(b.RawTick));
            return list;
        }

        // ---- 健康残值 · 资产折旧 (derivation only; window renders HealthView) ----

        private static HealthView BuildHealth(IArchiveService service, string stableId)
        {
            HealthView empty = new HealthView();
            if (service == null || string.IsNullOrEmpty(stableId)) return empty;
            Pawn live = service.GetLivePawn(stableId);
            if (live == null)
            {
                Log.Warning($"[PersonalChronicle] BuildHealth: no live pawn for stableId={stableId}.");
                return empty;
            }

            HealthValuationPolicyDef policy = DefDatabase<HealthValuationPolicyDef>.GetNamedSilentFail(
                HealthValuationPolicyDef.DefaultPolicyDefName);
            if (policy == null)
            {
                Log.Warning($"[PersonalChronicle] BuildHealth: HealthValuationPolicyDef '{HealthValuationPolicyDef.DefaultPolicyDefName}' not loaded. Check Defs/HealthValuation.xml uses <defName> element.");
                return empty;
            }

            HealthValuationResult r = HealthValuationEvaluator.Evaluate(live, policy);
            if (!r.IsDefined) return empty;

            List<HealthFactorView> factors = new List<HealthFactorView>();
            for (int i = 0; i < r.Factors.Count; i++)
            {
                HealthFactor f = r.Factors[i];
                if (f == null) continue;
                string label = string.IsNullOrEmpty(f.LabelKey)
                    ? "—"
                    : f.LabelKey.Translate().ToString();
                factors.Add(new HealthFactorView
                {
                    IsPositive = f.IsPositive,
                    LabelText = label,
                    Impact = Mathf.RoundToInt(f.Impact)
                });
            }

            List<HealthFactorView> bodyFactors = BuildHealthFactorViews(r.BodyFactors);
            List<HealthFactorView> spiritFactors = BuildHealthFactorViews(r.SpiritFactors);
            List<HealthFactorView> youthFactors = BuildHealthFactorViews(r.YouthFactors);

            List<HealthEventView> eventViews = new List<HealthEventView>();
            for (int i = 0; i < r.Events.Count; i++)
            {
                HealthDepreciationEvent e = r.Events[i];
                if (e == null) continue;
                string desc = ResolveEventDescription(e);
                string tag = string.IsNullOrEmpty(e.TagKey)
                    ? ""
                    : e.TagKey.Translate().ToString();
                string dateText = e.RawTick > 0L
                    ? GenDate.DateReadoutStringAt(e.RawTick, Vector2.zero)
                    : "PersonalChronicle.UI.UnknownDate".Translate().ToString();
                eventViews.Add(new HealthEventView
                {
                    DateText = dateText,
                    Description = desc,
                    TagText = tag,
                    RawDefName = e.RawDefName,
                    Impact = Mathf.RoundToInt(e.Impact),
                    RawTick = e.RawTick
                });
            }

            return new HealthView
            {
                IsDefined = true,
                HealthScore = r.HealthScore,
                BodyPercent = r.BodyPercent,
                AgeYears = r.AgeYears,
                SilverValue = Mathf.RoundToInt(r.SilverValue),
                BaseSilverValue = Mathf.RoundToInt(r.BaseSilverValue),
                WeeklySilverEstimate = Mathf.RoundToInt(r.WeeklySilverEstimate),
                IsPrime = r.IsPrime,
                IsImpaired = r.IsImpaired,
                BodyIntegrityScore = r.BodyIntegrityScore,
                SpiritScore = r.SpiritScore,
                YouthScore = r.YouthScore,
                BodyFactors = bodyFactors,
                SpiritFactors = spiritFactors,
                YouthFactors = youthFactors,
                Factors = factors,
                Events = eventViews,
                // v4.14: data-driven one-line verdict (健康残值结论). Thresholds
                // mirror the impaired/prime semantics of the evaluator — no UI
                // hardcoding, translation keys carry the text.
                VerdictText = BuildHealthVerdict(r.HealthScore, r.IsPrime, r.IsImpaired)
            };
        }

        /// <summary>v4.14: health-residual verdict line (data-driven, localized).</summary>
        private static string BuildHealthVerdict(float score, bool isPrime, bool isImpaired)
        {
            if (isImpaired)
            {
                return HealthValuationKeys.VerdictImpaired.Translate().ToString();
            }
            if (isPrime)
            {
                return HealthValuationKeys.VerdictPrime.Translate().ToString();
            }
            if (score >= 40f)
            {
                return HealthValuationKeys.VerdictFair.Translate().ToString();
            }
            return HealthValuationKeys.VerdictDepleted.Translate().ToString();
        }

        private static List<HealthFactorView> BuildHealthFactorViews(
            IReadOnlyList<HealthFactor> src)
        {
            List<HealthFactorView> list = new List<HealthFactorView>();
            if (src == null) return list;
            for (int i = 0; i < src.Count; i++)
            {
                HealthFactor f = src[i];
                if (f == null) continue;
                string label = string.IsNullOrEmpty(f.LabelKey)
                    ? "—"
                    : f.LabelKey.Translate().ToString();
                list.Add(new HealthFactorView
                {
                    IsPositive = f.IsPositive,
                    LabelText = label,
                    Impact = Mathf.RoundToInt(f.Impact)
                });
            }
            return list;
        }

        private static string ResolveEventDescription(HealthDepreciationEvent e)
        {
            // Prefer explicit translation (Event.{defName}). If that key is missing
            // the translator returns the key verbatim, so fall back to the human-readable
            // HediffDef.label (e.g. "腰损" for BadBack). As a last resort, sanitise the
            // raw defName so we never expose a full translation key like
            // "PersonalChronicle.UI.HealthValuation.Event.Scratch" in the UI.
            if (!string.IsNullOrEmpty(e.LabelKey))
            {
                string translated = e.LabelKey.Translate().ToString();
                bool usable = !string.IsNullOrWhiteSpace(translated)
                    && !string.Equals(translated, e.LabelKey, System.StringComparison.Ordinal);
                if (usable)
                {
                    return translated;
                }
            }
            if (!string.IsNullOrEmpty(e.RawDefName))
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(e.RawDefName);
                if (def != null && !string.IsNullOrEmpty(def.label))
                {
                    return def.label;
                }
                return SanitizeDefNameForDisplay(e.RawDefName);
            }
            return "—";
        }

        private static string SanitizeDefNameForDisplay(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return "—";
            }
            // Drop known prefixes and split CamelCase / snake_case into readable words.
            string cleaned = defName;
            string[] prefixes = new[]
            {
                HealthValuationKeys.EventPrefix,
            };
            for (int i = 0; i < prefixes.Length; i++)
            {
                if (cleaned.StartsWith(prefixes[i], System.StringComparison.Ordinal))
                {
                    cleaned = cleaned.Substring(prefixes[i].Length);
                    break;
                }
            }
            cleaned = cleaned.Replace('_', ' ');
            if (string.IsNullOrEmpty(cleaned))
            {
                return defName;
            }
            // Simple CamelCase split: insert space before uppercase letters (except first).
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < cleaned.Length; i++)
            {
                char c = cleaned[i];
                if (i > 0 && char.IsUpper(c) && char.IsLower(cleaned[i - 1]))
                {
                    sb.Append(' ');
                }
                sb.Append(c);
            }
            string result = sb.ToString();
            if (string.IsNullOrWhiteSpace(result))
            {
                return defName;
            }
            return result;
        }

        // kind weight table (architecture: weights centralized here, tunable)
        private static int KindWeight(ChronicleEventKind kind)
        {
            switch (kind)
            {
                case ChronicleEventKind.Death: return 100;
                case ChronicleEventKind.Join: return 90;
                case ChronicleEventKind.Battle: return 60;
                case ChronicleEventKind.Social: return 50;
                case ChronicleEventKind.Craft: return 40;
                case ChronicleEventKind.Built: return 35;
                default: return 10;
            }
        }

        // ---- local helpers (data-neutral; use Verse translation, NOT UI types) ----

        private static string FormatDateLocal(long tick)
        {
            // tick 0 是新档第 1 天（开局殖民者 JoinTick=0 即此），是合法日期；
            // 仅 -1（未知哨兵）才显示"未知"。
            if (tick < 0L) return "PersonalChronicle.UI.UnknownDate".Translate().ToString();
            return RimWorld.GenDate.DateReadoutStringAt(tick, UnityEngine.Vector2.zero);
        }

        private static string WorkTypeLabelLocal(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return "—";
            var def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(defName);
            return def != null ? def.labelShort != null ? def.labelShort : def.defName : defName;
        }

        private static string PlaceTextLocal(PlaceVisit v)
        {
            if (v == null) return "—";
            if (v.PlaceKind == PlaceVisitKeys.KindCaravan
                || (v.PlaceKey != null && v.PlaceKey.StartsWith(PlaceVisitKeys.TileKeyPrefix, System.StringComparison.Ordinal)))
            {
                string tile = v.PlaceKey != null
                    && v.PlaceKey.StartsWith(PlaceVisitKeys.TileKeyPrefix, System.StringComparison.Ordinal)
                    ? v.PlaceKey.Substring(PlaceVisitKeys.TileKeyPrefix.Length)
                    : v.PlaceKey;
                return "PersonalChronicle.UI.PlacesWorldTile".Translate(tile).ToString();
            }
            var biome = DefDatabase<BiomeDef>.GetNamedSilentFail(v.PlaceKey);
            return biome != null ? biome.LabelCap : (v.PlaceKey ?? "—");
        }

        /// <summary>
        /// Resolves a biome defName to its localized label; when the string is not a
        /// resolvable defName (e.g. a pre-v4.9.1 save storing a raw label), returns it
        /// unchanged so both record formats display.
        /// </summary>
        private static string ResolveBiomeLabelOrRaw(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "—") return "—";
            var biome = DefDatabase<BiomeDef>.GetNamedSilentFail(value);
            return biome != null ? biome.LabelCap : value;
        }

        private static string BackstoryText(PawnObject pawn)
        {
            if (pawn == null || string.IsNullOrEmpty(pawn.KindDefName)) return "—";
            var def = DefDatabase<PawnKindDef>.GetNamedSilentFail(pawn.KindDefName);
            return def != null ? def.LabelCap : pawn.KindDefName;
        }

        private static string FactionText(PawnObject pawn)
        {
            if (pawn == null || string.IsNullOrEmpty(pawn.FactionDefName)) return "—";
            var def = DefDatabase<FactionDef>.GetNamedSilentFail(pawn.FactionDefName);
            return def != null ? def.LabelCap : pawn.FactionDefName;
        }

        private static string DeathText(PawnObject pawn)
        {
            if (pawn == null) return "—";
            string cause = string.IsNullOrEmpty(pawn.DeathCauseKey) ? "" : pawn.DeathCauseKey.Translate().ToString();
            return string.IsNullOrEmpty(cause) ? "—" : cause;
        }

        private static string EventGlyph(ChronicleEventDef def)
        {
            if (def == null) return "•";
            switch (def.kind)
            {
                case ChronicleEventKind.Join: return "🚪";
                case ChronicleEventKind.Death: return "💀";
                case ChronicleEventKind.Battle: return "⚔️";
                case ChronicleEventKind.Craft: return "🔨";
                case ChronicleEventKind.Built: return "🏛️";
                case ChronicleEventKind.Social: return "💞";
                default: return "•";
            }
        }

        private static string EventTitleLocal(ChronicleEvent ev)
        {
            if (ev == null) return "—";
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            if (def != null && !string.IsNullOrEmpty(def.labelKey))
            {
                string t = def.labelKey.Translate().ToString();
                if (!string.IsNullOrEmpty(t)) return t;
            }
            return ev.TypeKey ?? "—";
        }

        private static string EventSubLocal(ChronicleEvent ev)
        {
            if (ev == null) return "";
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            if (def != null && !string.IsNullOrEmpty(def.descriptionKey))
            {
                string d = def.descriptionKey.Translate().ToString();
                if (!string.IsNullOrEmpty(d)) return d;
            }
            return "";
        }

        private static string KindLabelLocal(ChronicleEventKind kind)
        {
            return ("PersonalChronicle.UI.Ev" + kind.ToString()).Translate().ToString();
        }

        public EventSnapshot BuildEvent(IArchiveService service, ChronicleEvent ev, long revision)
        {
            EventSnapshot snap = new EventSnapshot { BuiltFromRevision = revision, IsEmpty = true, Event = ev };
            if (ev == null)
            {
                return snap;
            }
            snap.IsEmpty = false;
            // v4.6.5: follow-up events derived here (Read Model), not in the window.
            // Later events of the same primary object, capped at 3, ascending by tick.
            if (service != null && ev.Primary != null && !string.IsNullOrEmpty(ev.Primary.StableId))
            {
                IReadOnlyList<ChronicleEvent> evs = service.GetEventsFor(ev.Primary.StableId);
                if (evs != null)
                {
                    snap.FollowupEvents = evs
                        .Where(e => e != null && e.Tick > ev.Tick)
                        .OrderBy(e => e.Tick)
                        .Take(3)
                        .ToList();
                }
            }
            return snap;
        }

        /// <summary>
        /// Full timeline event stream, sorted ascending by <see cref="ChronicleEvent.Tick"/>
        /// and null-guarded. The Home timeline only consumes this snapshot; the ordering
        /// and null-filtering never leak into the window's draw path (design doc §7.4).
        /// </summary>
        public IReadOnlyList<ChronicleEvent> BuildTimelineEvents(IArchiveService service, long revision)
        {
            if (service == null)
            {
                return System.Array.Empty<ChronicleEvent>();
            }
            IReadOnlyList<ChronicleEvent> all = service.GetAllEvents();
            if (all == null)
            {
                return System.Array.Empty<ChronicleEvent>();
            }
            return all
                .Where(e => e != null)
                .OrderBy(e => e.Tick)
                .ToList();
        }

        /// <summary>
        /// Event count for a stable id, memoized so sort comparators never issue
        /// repeated <see cref="IArchiveService.GetEventsFor"/> queries.
        /// </summary>
        private static int EventCount(IArchiveService service, Dictionary<string, int> cache, string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return 0;
            }
            if (cache.TryGetValue(stableId, out int cached))
            {
                return cached;
            }
            IReadOnlyList<ChronicleEvent> evs = service.GetEventsFor(stableId);
            int c = evs == null ? 0 : evs.Count;
            cache[stableId] = c;
            return c;
        }

        // ---- v4.6.5 production ledger (Read Model only; window maps the snapshot) ----

        private static IReadOnlyList<ProductionLineView> BuildProductionLines(
            IReadOnlyList<ChronicleEvent> events)
        {
            List<ProductionLineView> lines = new List<ProductionLineView>();
            if (events == null || events.Count == 0) return lines;
            Dictionary<string, ProductionLineView> byDef = new Dictionary<string, ProductionLineView>();
            for (int i = 0; i < events.Count; i++)
            {
                ChronicleEvent ev = events[i];
                if (ev == null || !IsProductionEvent(ev)) continue;
                ObjectRef primary = ev.Primary;
                if (primary == null || string.IsNullOrEmpty(primary.StableId)) continue;
                string defName = ThingDefNameFromStableId(primary.StableId);
                if (string.IsNullOrEmpty(defName)) continue;
                if (byDef.TryGetValue(defName, out ProductionLineView line))
                {
                    line.Count++;
                    if (ev.Tick > line.LastTick)
                    {
                        line.LastTick = ev.Tick;
                        line.StableId = primary.StableId;
                    }
                    byDef[defName] = line;
                }
                else
                {
                    byDef[defName] = new ProductionLineView
                    {
                        DefName = defName,
                        Label = ThingDefLabelLocal(defName),
                        Count = 1,
                        LastTick = ev.Tick,
                        StableId = primary.StableId
                    };
                }
            }
            List<ProductionLineView> result = new List<ProductionLineView>(byDef.Values);
            // v6.6: aggregate per-line market value (Def.MarketValue * Count) so the
            // 产出 cell can show value-contribution bars (Read Model only).
            for (int i = 0; i < result.Count; i++)
            {
                ProductionLineView line = result[i];
                ThingDef td = (!string.IsNullOrEmpty(line.DefName)) ? DefDatabase<ThingDef>.GetNamed(line.DefName, false) : null;
                float unit = (td != null) ? td.BaseMarketValue : 0f;
                line.Value = unit * line.Count;
                result[i] = line;
            }
            result.Sort((a, b) => b.LastTick.CompareTo(a.LastTick));
            return result;
        }

        /// <summary>
        /// v4.15 condense-tab: group production lines by their first-level
        /// <see cref="ThingCategoryDef"/> (official one-level category) and return
        /// the top categories by aggregated count. The resulting labels are already
        /// localized (via <see cref="ThingCategoryDef.LabelCap"/>) so the window
        /// stays free of any category-name hardcoding. Categories in the
        /// "plants/corpses" exclusion set (per design doc §C) are dropped to keep
        /// the badge group meaningful.
        /// </summary>
        private static IReadOnlyList<ProductionCategoryView> BuildProductionCategories(
            IReadOnlyList<ProductionLineView> lines)
        {
            List<ProductionCategoryView> empty = new List<ProductionCategoryView>();
            if (lines == null || lines.Count == 0) return empty;

            // First-level category defName → aggregated count.
            Dictionary<string, int> byCat = new Dictionary<string, int>();
            foreach (ProductionLineView line in lines)
            {
                if (line == null || string.IsNullOrEmpty(line.DefName)) continue;
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(line.DefName);
                if (def == null) continue;
                ThingCategoryDef cat = def.FirstThingCategory;
                if (cat == null) continue;
                string key = cat.defName;
                // Skip non-meaningful categories for the digest badge group.
                if (key == "Plants" || key == "Corpses" || key == "Corpse" || key == "Chunks") continue;
                int prev;
                byCat.TryGetValue(key, out prev);
                byCat[key] = prev + line.Count;
            }
            if (byCat.Count == 0) return empty;

            List<ProductionCategoryView> cats = new List<ProductionCategoryView>(byCat.Count);
            foreach (var kv in byCat)
            {
                ThingCategoryDef cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(kv.Key);
                if (cat == null) continue;
                cats.Add(new ProductionCategoryView
                {
                    Label = cat.LabelCap,
                    Count = kv.Value
                });
            }
            // Largest categories first; cap to the top 4 so the badge group stays compact.
            cats.Sort((a, b) => b.Count.CompareTo(a.Count));
            if (cats.Count > 4) cats.RemoveRange(4, cats.Count - 4);
            return cats;
        }

        /// <summary>
        /// v4.15 condense-tab: convert the per-group kill counts (already keyed by
        /// localized faction/category label) into the digest badge list, largest
        /// first and capped to the top 4 so the 击杀 cell stays compact.
        /// </summary>
        private static IReadOnlyList<KillByFactionView> BuildKillsByFaction(Dictionary<string, int> byFaction)
        {
            List<KillByFactionView> empty = new List<KillByFactionView>();
            if (byFaction == null || byFaction.Count == 0) return empty;
            List<KillByFactionView> list = new List<KillByFactionView>(byFaction.Count);
            foreach (var kv in byFaction)
            {
                list.Add(new KillByFactionView { Label = kv.Key, Count = kv.Value });
            }
            list.Sort((a, b) => b.Count.CompareTo(a.Count));
            if (list.Count > 4) list.RemoveRange(4, list.Count - 4);
            return list;
        }

        /// <summary>
        /// v4.15 condense-tab: maps the stored victim-category key to a localized
        /// label for the 击杀 cell badge group. Tokens, not hardcoded display text.
        /// </summary>
        private static string VictimCategoryLabel(string victimCategory)
        {
            if (victimCategory == ChronicleEventParams.VictimCategoryMechanoid)
            {
                return "PersonalChronicle.UI.FactionKindMechanoid".Translate().ToString();
            }
            if (victimCategory == ChronicleEventParams.VictimCategoryAnimal)
            {
                return "PersonalChronicle.UI.FactionKindAnimal".Translate().ToString();
            }
            // Humanlike victims without a known faction fall back to the generic label.
            return "PersonalChronicle.UI.FactionKindUnknown".Translate().ToString();
        }

        private static bool IsProductionEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey)) return false;
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && (def.kind == ChronicleEventKind.Craft || def.kind == ChronicleEventKind.Built);
        }

        // ---- v4.7 Legacy chain (传承) derivation ----

        private static LegacyView BuildLegacy(IArchiveService service, ThingObject thing, IReadOnlyList<ChronicleEvent> events)
        {
            LegacyView view = new LegacyView { IsEmpty = true };
            if (service == null || thing == null)
            {
                return view;
            }
            // Work on a local record list: the Read Model never writes back to the
            // Domain object (boundary rule). Pre-v4.7 saves fall back to the legacy
            // HolderHistory, materialized into local HolderRecords.
            List<HolderRecord> records;
            if (thing.HolderRecords != null && thing.HolderRecords.Count > 0)
            {
                records = new List<HolderRecord>(thing.HolderRecords.Count);
                for (int i = 0; i < thing.HolderRecords.Count; i++)
                {
                    HolderRecord src = thing.HolderRecords[i];
                    if (src == null) continue;
                    records.Add(new HolderRecord(src.StableId, src.LabelSnapshot, src.StartTick, src.IsFirst, src.Kind));
                }
            }
            else if (thing.HolderHistory != null && thing.HolderHistory.Count > 0)
            {
                records = new List<HolderRecord>(thing.HolderHistory.Count);
                for (int i = 0; i < thing.HolderHistory.Count; i++)
                {
                    ObjectRef h = thing.HolderHistory[i];
                    if (h == null || string.IsNullOrEmpty(h.StableId)) continue;
                    records.Add(new HolderRecord(
                        h.StableId,
                        h.LabelSnapshot ?? h.StableId,
                        -1L,
                        i == 0,
                        HolderRecord.HolderKindOwn));
                }
            }
            else
            {
                return view;
            }
            if (records.Count == 0)
            {
                return view;
            }

            // Kill events of this weapon: Death events whose subject is the weapon.
            List<ChronicleEvent> killEvents = new List<ChronicleEvent>();
            if (events != null)
            {
                for (int i = 0; i < events.Count; i++)
                {
                    ChronicleEvent ev = events[i];
                    if (ev == null || ev.TypeKey != ChronicleEventType.Death) continue;
                    // GetEventsFor(weaponId) already returns only events linked to
                    // this weapon; Death events among them are kills by it.
                    killEvents.Add(ev);
                }
            }

            // Assign an exclusive [start, end) window per record so kill events can
            // be bucketed by tenure without double counting. Computed on the local
            // copies. Pre-v4.9 legacy records may have StartTick < 0 (no stored time);
            // give them a derived start (the previous record's end, or 0 for the
            // first) so a kill is still attributed to exactly one holder instead of
            // falling into every record and inflating totalKills.
            long now = Find.TickManager.TicksGame;
            long[] starts = new long[records.Count];
            long[] ends = new long[records.Count];
            for (int i = 0; i < records.Count; i++)
            {
                HolderRecord rec = records[i];
                if (rec == null) continue;
                long end = (i + 1 < records.Count && records[i + 1] != null)
                    ? records[i + 1].StartTick
                    : now;
                if (end < 0L) end = now;
                ends[i] = end;
                long start = rec.StartTick;
                if (start < 0L)
                {
                    start = (i > 0) ? ends[i - 1] : 0L;
                    if (start > end) start = end;
                }
                starts[i] = start;
            }

            List<LegacyHolderView> holders = new List<LegacyHolderView>();
            int genCounter = 0;
            int totalKills = 0;
            string currentHolderText = null;
            string currentHolderId = thing.CurrentHolderId;
            if (!string.IsNullOrEmpty(currentHolderId))
            {
                ArchiveObject cur = service.GetObject(currentHolderId);
                currentHolderText = cur != null ? ObjectLabelLocal(cur) : currentHolderId;
            }

            for (int i = 0; i < records.Count; i++)
            {
                HolderRecord rec = records[i];
                if (rec == null) continue;
                bool isLoan = rec.Kind == HolderRecord.HolderKindLoan;
                long startTick = starts[i];
                long endTick = ends[i];
                int kills = CountKillsInWindow(killEvents, startTick, endTick);
                totalKills += kills;

                HolderRecord firstRec = records[0];
                LegacyHolderView h = new LegacyHolderView
                {
                    HolderStableId = rec.StableId,
                    HolderText = ResolveHolderLabel(service, rec),
                    Generation = isLoan ? 0 : ++genCounter,
                    IsFirst = rec.IsFirst,
                    IsLoan = isLoan,
                    IsCurrent = !string.IsNullOrEmpty(rec.StableId) && rec.StableId == currentHolderId,
                    FromText = FormatDateLocal(rec.StartTick),
                    ToText = endTick > 0L && endTick >= rec.StartTick
                        ? FormatDateLocal(endTick)
                        : "PersonalChronicle.UI.Legacy.Now".Translate().ToString(),
                    DurationText = DurationTextLocal(rec.StartTick, endTick),
                    KillCount = kills,
                    CreatedByText = firstRec != null
                        ? ResolveHolderLabel(service, firstRec)
                        : null,
                    CreatedAtText = firstRec != null
                        ? FormatDateLocal(firstRec.StartTick)
                        : null,
                    RemarkText = isLoan
                        ? "PersonalChronicle.UI.Legacy.LoanRemark".Translate().ToString()
                        : null
                };
                holders.Add(h);
            }

            int genCount = genCounter;
            string createdBy = holders.Count > 0 ? holders[0].HolderText : null;
            string createdAt = holders.Count > 0 ? holders[0].FromText : null;

            view.TitleText = LegacyTitleText(thing.ThingDefName);
            view.VerdictText = LegacyVerdictText(genCount, totalKills);
            view.CreatedByText = createdBy;
            view.CreatedAtText = createdAt;
            view.GenCount = genCount;
            view.TotalKills = totalKills;
            view.CurrentHolderText = currentHolderText;
            view.Holders = holders;
            view.IsEmpty = holders.Count == 0;
            return view;
        }

        // ---- v4.9 equipment legacy extension (溯源 / 工坊署名链 / 同袍共用 / 退役仪式) ----

        /// <summary>
        /// 溯源 (origin): derives where the thing came from by scanning the event
        /// stream. Craft/Built → "craft" (maker = first craft subject); otherwise
        /// if the thing has battle/death records the origin is at least "battle"
        /// (field-tested). Every label is localized here; the window never sorts.
        /// </summary>
        private static ThingOriginView BuildOrigin(
            IArchiveService service, ThingObject thing, IReadOnlyList<ChronicleEvent> events)
        {
            ThingOriginView view = new ThingOriginView { IsEmpty = true };
            if (service == null || thing == null || events == null)
            {
                return view;
            }
            // First relevant event decides the origin kind.
            for (int i = 0; i < events.Count; i++)
            {
                ChronicleEvent ev = events[i];
                if (ev == null || string.IsNullOrEmpty(ev.TypeKey)) continue;
                ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
                if (def == null) continue;
                if (def.kind == ChronicleEventKind.Craft || def.kind == ChronicleEventKind.Built)
                {
                    view.KindText = "PersonalChronicle.UI.Origin.Craft".Translate().ToString();
                    view.KindKey = "craft";
                    if (ev.Subjects != null)
                    {
                        for (int s = 0; s < ev.Subjects.Count; s++)
                        {
                            ObjectRef sub = ev.Subjects[s];
                            if (sub != null && sub.CategoryKey == ArchiveCategoryKeys.Pawn
                                && !string.IsNullOrEmpty(sub.StableId))
                            {
                                view.FromStableId = sub.StableId;
                                view.FromText = ResolveObjectLabel(service, sub);
                                break;
                            }
                        }
                    }
                    if (string.IsNullOrEmpty(view.FromText))
                    {
                        view.FromText = "PersonalChronicle.UI.UnknownDate".Translate().ToString();
                    }
                    // v4.14: 来源地点 = the location/battle subject on the craft
                    // event (workshop / field / expedition). Pure subject-edge
                    // resolution, no invented text.
                    view.WhereText = ResolveOriginPlaceLabel(service, ev);
                    view.IsEmpty = false;
                    return view;
                }
                if (def.kind == ChronicleEventKind.Battle || def.kind == ChronicleEventKind.Death)
                {
                    // Field-tested in combat before any craft record → battle origin.
                    view.KindText = "PersonalChronicle.UI.Origin.Battle".Translate().ToString();
                    view.KindKey = "battle";
                    view.IsEmpty = false;
                    return view;
                }
            }
            return view;
        }

        /// <summary>
        /// v4.14: resolves the place/battle label attached to an event's Subjects
        /// (the origin "where"). Returns null when the event carries no location
        /// or battle edge.
        /// </summary>
        private static string ResolveOriginPlaceLabel(IArchiveService service, ChronicleEvent ev)
        {
            if (ev == null || ev.Subjects == null)
            {
                return null;
            }
            for (int s = 0; s < ev.Subjects.Count; s++)
            {
                ObjectRef sub = ev.Subjects[s];
                if (sub == null)
                {
                    continue;
                }
                if (sub.CategoryKey == ArchiveCategoryKeys.Location
                    || sub.CategoryKey == ArchiveCategoryKeys.Battle)
                {
                    if (!string.IsNullOrEmpty(sub.LabelSnapshot))
                    {
                        return sub.LabelSnapshot;
                    }
                    ArchiveObject obj = service != null ? service.GetObject(sub.StableId) : null;
                    if (obj != null && !string.IsNullOrEmpty(obj.LabelSnapshot))
                    {
                        return obj.LabelSnapshot;
                    }
                    return sub.StableId;
                }
            }
            return null;
        }

        /// <summary>
        /// 工坊署名链 (maker chain): the crafter's later fate. Reads the maker's
        /// PawnRecord: if the maker later died and the death event lists this thing
        /// as a subject edge, show the "died by own creation" double narrative.
        /// </summary>
        private static MakerChainView BuildMakerChain(
            IArchiveService service, ThingObject thing, ThingOriginView origin)
        {
            MakerChainView view = new MakerChainView { IsEmpty = true };
            if (service == null || thing == null || origin == null || origin.IsEmpty)
            {
                return view;
            }
            string makerId = origin.FromStableId;
            if (string.IsNullOrEmpty(makerId))
            {
                return view;
            }
            ArchiveObject maker = service.GetObject(makerId);
            if (maker is PawnObject pawn)
            {
                view.MakerStableId = makerId;
                view.MakerText = string.IsNullOrEmpty(pawn.LabelSnapshot)
                    ? pawn.StableId
                    : pawn.LabelSnapshot;
                view.IsEmpty = false;
                if (pawn.IsArchived)
                {
                    // Died after crafting — check whether this thing dealt the blow.
                    IReadOnlyList<ChronicleEvent> makerEvents = service.GetEventsFor(makerId);
                    if (makerEvents != null)
                    {
                        for (int i = 0; i < makerEvents.Count; i++)
                        {
                            ChronicleEvent ev = makerEvents[i];
                            if (ev == null || ev.TypeKey != ChronicleEventType.Death) continue;
                            if (HasSubjectThing(ev, thing.StableId))
                            {
                                view.MakerDiedByOwn = true;
                                break;
                            }
                        }
                    }
                }
            }
            return view;
        }

        /// <summary>
        /// 同袍共用网络 (co-use): colonists who used this equipment in parallel
        /// with the current holder. Computed from HolderRecords tenure overlap —
        /// two records overlap when the later start < the earlier end. The window
        /// only renders the derived rows (share % of the longest sharer).
        /// </summary>
        private static CoUseView BuildCoUse(
            IArchiveService service, ThingObject thing, IReadOnlyList<ChronicleEvent> events)
        {
            CoUseView view = new CoUseView();
            if (service == null || thing == null
                || thing.HolderRecords == null || thing.HolderRecords.Count < 2)
            {
                view.IsEmpty = true;
                return view;
            }
            List<HolderRecord> records = new List<HolderRecord>();
            for (int i = 0; i < thing.HolderRecords.Count; i++)
            {
                HolderRecord rec = thing.HolderRecords[i];
                if (rec == null || string.IsNullOrEmpty(rec.StableId)) continue;
                records.Add(rec);
            }
            if (records.Count < 2)
            {
                view.IsEmpty = true;
                return view;
            }
            // Build tenures with exclusive end ticks (same derivation as BuildLegacy).
            long now = Find.TickManager.TicksGame;
            List<long> ends = new List<long>(records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                ends.Add(i + 1 < records.Count
                    ? records[i + 1].StartTick
                    : now);
            }
            string currentHolderId = thing.CurrentHolderId;
            Dictionary<string, CoUseRowView> byPawn = new Dictionary<string, CoUseRowView>();
            for (int i = 0; i < records.Count; i++)
            {
                HolderRecord rec = records[i];
                // Exclude the current sole holder: co-use is about parallel sharers.
                if (!string.IsNullOrEmpty(currentHolderId) && rec.StableId == currentHolderId) continue;
                for (int j = i + 1; j < records.Count; j++)
                {
                    HolderRecord other = records[j];
                    if (other == null) continue;
                    long overlapStart = System.Math.Max(rec.StartTick, other.StartTick);
                    long overlapEnd = System.Math.Min(ends[i], ends[j]);
                    if (overlapEnd > overlapStart)
                    {
                        long overlapTicks = overlapEnd - overlapStart;
                        int days = (int)GenDate.TicksToDays((int)overlapTicks);
                        if (days <= 0) continue;
                        if (!byPawn.TryGetValue(other.StableId, out CoUseRowView row))
                        {
                            row = new CoUseRowView
                            {
                                PawnStableId = other.StableId,
                                PawnText = ResolvePawnLabel(service, other),
                                SharedDays = 0
                            };
                            byPawn[other.StableId] = row;
                        }
                        row.SharedDays += days;
                    }
                }
            }
            if (byPawn.Count == 0)
            {
                view.IsEmpty = true;
                return view;
            }
            List<CoUseRowView> rows = new List<CoUseRowView>(byPawn.Values);
            rows.Sort((a, b) => b.SharedDays.CompareTo(a.SharedDays));
            int maxDays = rows.Count > 0 ? rows[0].SharedDays : 0;
            for (int r = 0; r < rows.Count; r++)
            {
                rows[r].SharePercent = maxDays > 0 ? (int)(rows[r].SharedDays * 100f / maxDays) : 0;
            }
            view.Rows = rows;
            view.IsEmpty = false;
            return view;
        }

        /// <summary>
        /// 退役仪式 (decommission): the thing's death record. Reads the persisted
        /// DecommissionSnapshot on the domain object; no record → empty state.
        /// </summary>
        private static DecommissionView BuildDecommission(
            IArchiveService service, ThingObject thing, IReadOnlyList<ChronicleEvent> events)
        {
            DecommissionView view = new DecommissionView { HasRecord = false };
            if (thing == null || thing.Decommission == null || thing.Decommission.IsEmpty)
            {
                return view;
            }
            Domain.DecommissionRecord rec = thing.Decommission;
            view.HasRecord = true;
            view.LastHolderStableId = rec.LastHolderStableId;
            view.LastHolderText = !string.IsNullOrEmpty(rec.LastHolderLabel)
                ? rec.LastHolderLabel
                : (!string.IsNullOrEmpty(rec.LastHolderStableId)
                    ? ResolveObjectLabel(service, rec.LastHolderStableId)
                    : "—");
            // LastPlaceLabel stores a language-independent biome defName (older saves
            // may hold a localized label); resolve to a label, falling back to the raw
            // string so both old and new records display.
            view.LastPlaceText = !string.IsNullOrEmpty(rec.LastPlaceLabel)
                ? ResolveBiomeLabelOrRaw(rec.LastPlaceLabel)
                : "—";
            view.ServiceDays = rec.ServiceDays;
            view.LastBattleText = !string.IsNullOrEmpty(rec.LastBattleLabel)
                ? rec.LastBattleLabel
                : "—";
            view.DateText = rec.Tick > 0L
                ? GenDate.DateReadoutStringAt(rec.Tick, Vector2.zero)
                : "PersonalChronicle.UI.UnknownDate".Translate().ToString();
            return view;
        }

        private static bool HasSubjectThing(ChronicleEvent ev, string thingStableId)
        {
            if (ev == null || string.IsNullOrEmpty(thingStableId) || ev.Subjects == null)
            {
                return false;
            }
            for (int i = 0; i < ev.Subjects.Count; i++)
            {
                ObjectRef sub = ev.Subjects[i];
                if (sub != null && sub.CategoryKey == ArchiveCategoryKeys.Thing
                    && sub.StableId == thingStableId)
                {
                    return true;
                }
            }
            return false;
        }

        private static string ResolveObjectLabel(IArchiveService service, ObjectRef objRef)
        {
            if (objRef == null) return "—";
            return ResolveObjectLabel(service, objRef.StableId);
        }

        private static string ResolveObjectLabel(IArchiveService service, string stableId)
        {
            if (string.IsNullOrEmpty(stableId)) return "—";
            if (service != null)
            {
                ArchiveObject obj = service.GetObject(stableId);
                if (obj != null)
                {
                    if (!string.IsNullOrEmpty(obj.LabelSnapshot)) return obj.LabelSnapshot;
                    if (obj is ThingObject t) return ThingDefLabelLocal(t.ThingDefName);
                    if (obj is PawnObject p) return string.IsNullOrEmpty(p.LabelSnapshot) ? p.StableId : p.LabelSnapshot;
                    return obj.StableId;
                }
            }
            return stableId;
        }

        private static string ResolvePawnLabel(IArchiveService service, HolderRecord rec)
        {
            if (rec == null) return "—";
            if (!string.IsNullOrEmpty(rec.LabelSnapshot)) return rec.LabelSnapshot;
            if (service != null && !string.IsNullOrEmpty(rec.StableId))
            {
                ArchiveObject obj = service.GetObject(rec.StableId);
                if (obj is PawnObject pawn)
                {
                    return string.IsNullOrEmpty(pawn.LabelSnapshot) ? pawn.StableId : pawn.LabelSnapshot;
                }
            }
            return rec.StableId ?? "—";
        }

        private static int CountKillsInWindow(List<ChronicleEvent> killEvents, long start, long end)
        {
            if (killEvents == null || killEvents.Count == 0) return 0;
            int count = 0;
            for (int i = 0; i < killEvents.Count; i++)
            {
                ChronicleEvent ev = killEvents[i];
                if (ev == null) continue;
                // v4.9.1: use half-open [start, end) so the boundary tick where the
                // next holder takes over (end == next StartTick) is attributed to the
                // new holder only, not double-counted on both sides. Previously a kill
                // at the exact transfer tick was tallied twice (totalKills inflated
                // and the previous holder's KillCount wrongly ticked up).
                if (start >= 0L && ev.Tick < start) continue;
                if (end >= 0L && ev.Tick >= end) continue;
                count++;
            }
            return count;
        }

        private static string ResolveHolderLabel(IArchiveService service, HolderRecord rec)
        {
            if (rec == null) return "—";
            if (!string.IsNullOrEmpty(rec.LabelSnapshot)) return rec.LabelSnapshot;
            if (service != null && !string.IsNullOrEmpty(rec.StableId))
            {
                ArchiveObject obj = service.GetObject(rec.StableId);
                if (obj != null) return ObjectLabelLocal(obj);
            }
            return rec.StableId ?? "—";
        }

        private static string ObjectLabelLocal(ArchiveObject obj)
        {
            if (obj == null) return "—";
            if (!string.IsNullOrEmpty(obj.LabelSnapshot)) return obj.LabelSnapshot;
            return obj.StableId ?? "—";
        }

        private static string DurationTextLocal(long start, long end)
        {
            if (start < 0L)
            {
                return "PersonalChronicle.UI.UnknownDate".Translate().ToString();
            }
            if (end < start)
            {
                return "PersonalChronicle.UI.Legacy.Now".Translate().ToString();
            }
            return SpanText.Format(end - start);
        }

        /// <summary>Data-driven epithet (传承称号) from the thing def; tokenized so
        /// the UI stays zero-hardcoded (see Defs/Legacy.xml when shipped).</summary>
        private static string LegacyTitleText(string thingDefName)
        {
            if (string.IsNullOrEmpty(thingDefName)) return null;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
            if (def == null) return null;
            // Epithets are authored per weapon tier via a legacy epithet map keyed
            // by ThingCategoryDef (not by concrete defName), keeping this data-driven.
            ThingCategoryDef cat = def.FirstThingCategory;
            if (cat != null && LegacyEpithetCatalog.TryGetValue(cat.defName, out string epithetKey))
            {
                return epithetKey.Translate().ToString();
            }
            return null;
        }

        /// <summary>Epithet catalog: weapon category → translation key. Tokens, not
        /// hardcoded display strings; extend via XML (see LegacyEpithetDef when shipped).</summary>
        private static readonly Dictionary<string, string> LegacyEpithetCatalog =
            new Dictionary<string, string>
            {
                { "Weapons", "PersonalChronicle.UI.Legacy.EpithetWeapons" },
                { "Apparel", "PersonalChronicle.UI.Legacy.EpithetApparel" }
            };

        private static string LegacyVerdictText(int genCount, int totalKills)
        {
            if (genCount <= 0)
            {
                return "PersonalChronicle.UI.Legacy.VerdictNoGen".Translate().ToString();
            }
            if (totalKills >= 3)
            {
                return "PersonalChronicle.UI.Legacy.VerdictBlooded".Translate().ToString();
            }
            if (totalKills > 0)
            {
                return "PersonalChronicle.UI.Legacy.VerdictTasted".Translate().ToString();
            }
            if (genCount >= 2)
            {
                return "PersonalChronicle.UI.Legacy.VerdictPassed".Translate().ToString();
            }
            return "PersonalChronicle.UI.Legacy.VerdictFresh".Translate().ToString();
        }

        private static string ThingDefNameFromStableId(string stableId)
        {
            if (string.IsNullOrEmpty(stableId)) return string.Empty;
            int colon = stableId.IndexOf(':');
            return colon >= 0 ? stableId.Substring(0, colon) : stableId;
        }

        private static string ThingDefLabelLocal(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return "—";
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return def != null && !string.IsNullOrEmpty(def.label) ? def.label : defName;
        }

        private static void CollectCandidatesByEvents(
            IArchiveService service, string categoryKey, List<ArchiveObject> candidates)
        {
            if (service == null || string.IsNullOrEmpty(categoryKey) || candidates == null)
            {
                return;
            }
            IReadOnlyList<ArchiveObject> objects = service.GetObjectsOfCategory(categoryKey);
            if (objects == null)
            {
                return;
            }
            for (int i = 0; i < objects.Count; i++)
            {
                ArchiveObject obj = objects[i];
                if (obj != null && !string.IsNullOrEmpty(obj.StableId))
                {
                    candidates.Add(obj);
                }
            }
        }

        // ---- v4.6 social ties (Read Model; merges live state + archived snapshots) ----

        private static IReadOnlyList<RelationView> BuildRelations(IArchiveService service, PawnObject pawn)
        {
            List<RelationView> rows = new List<RelationView>();
            HashSet<string> seen = new HashSet<string>();
            if (service == null || pawn == null)
            {
                return rows;
            }

            Pawn livePawn = service.GetLivePawn(pawn.StableId);

            // 1) Current live relations.
            if (livePawn?.relations?.DirectRelations != null)
            {
                List<DirectPawnRelation> directRelations = livePawn.relations.DirectRelations;
                for (int i = 0; i < directRelations.Count; i++)
                {
                    DirectPawnRelation rel = directRelations[i];
                    if (rel?.def == null || rel.otherPawn == null)
                    {
                        continue;
                    }
                    if (!SocialRelationFilter.IsSignificant(rel.def))
                    {
                        continue;
                    }
                    string otherId = rel.otherPawn.GetUniqueLoadID();
                    string key = MakeRelationKey(rel.def.defName, otherId);
                    if (!seen.Add(key))
                    {
                        continue;
                    }
                    rows.Add(new RelationView
                    {
                        OtherLabel = rel.otherPawn.LabelShort,
                        RelationLabel = RelationLabelFor(rel.def, rel.otherPawn),
                        StatusLabel = rel.otherPawn.Dead
                            ? "PersonalChronicle.UI.Dead".Translate().ToString()
                            : "PersonalChronicle.UI.Alive".Translate().ToString(),
                        IsLive = true
                    });
                }
            }

            // 2) Archived relations (initial ties captured at join, plus historical changes).
            if (pawn.Relations != null)
            {
                for (int i = 0; i < pawn.Relations.Count; i++)
                {
                    SignificantRelation rel = pawn.Relations[i];
                    if (rel == null)
                    {
                        continue;
                    }
                    string key = MakeRelationKey(rel.RelationDefName, rel.OtherStableId);
                    if (!seen.Add(key))
                    {
                        continue;
                    }
                    Pawn otherLive = service.GetLivePawn(rel.OtherStableId);
                    bool otherDead = otherLive != null && otherLive.Dead;
                    string status = otherDead
                        ? "PersonalChronicle.UI.Dead".Translate().ToString()
                        : (rel.IsActive
                            ? "PersonalChronicle.UI.Alive".Translate().ToString()
                            : "PersonalChronicle.UI.RelEnded".Translate().ToString());
                    PawnRelationDef def = DefDatabase<PawnRelationDef>.GetNamedSilentFail(rel.RelationDefName);
                    rows.Add(new RelationView
                    {
                        OtherLabel = !string.IsNullOrEmpty(rel.OtherLabel) ? rel.OtherLabel : rel.OtherStableId,
                        RelationLabel = RelationLabelFor(def, otherLive, rel.RelationDefName),
                        StatusLabel = status,
                        IsLive = false
                    });
                }
            }

            // Stable presentation order: partners → blood kin → friends → rivals →
            // ended ties. Sorting belongs to the Read Model, never to the window.
            // Labels are resolved once here rather than per comparison, since a
            // sort evaluates the comparator O(n log n) times.
            string endedLabel = "PersonalChronicle.UI.RelEnded".Translate().ToString();
            string friendLabel = FriendLabel();
            string rivalLabel = RivalLabel();
            rows.Sort((a, b) =>
            {
                int ra = RelationSortRank(a, endedLabel, friendLabel, rivalLabel);
                int rb = RelationSortRank(b, endedLabel, friendLabel, rivalLabel);
                if (ra != rb)
                {
                    return ra.CompareTo(rb);
                }
                return string.Compare(a?.OtherLabel ?? string.Empty,
                    b?.OtherLabel ?? string.Empty, System.StringComparison.CurrentCulture);
            });

            return rows;
        }

        /// <summary>
        /// Presentation rank for the social list. Ended ties always sink to the
        /// bottom so the current social circle reads first.
        /// </summary>
        private static int RelationSortRank(
            RelationView row, string endedLabel, string friendLabel, string rivalLabel)
        {
            if (row == null)
            {
                return 99;
            }
            if (row.StatusLabel == endedLabel)
            {
                return 50;
            }
            string label = row.RelationLabel ?? string.Empty;
            if (label == friendLabel)
            {
                return 20;
            }
            if (label == rivalLabel)
            {
                return 30;
            }
            return row.IsLive ? 0 : 10;
        }

        private static string FriendLabel()
        {
            return "PersonalChronicle.Relation.Friend".Translate().ToString();
        }

        private static string RivalLabel()
        {
            return "PersonalChronicle.Relation.Rival".Translate().ToString();
        }

        private static string MakeRelationKey(string relationDefName, string otherStableId)
        {
            return (relationDefName ?? string.Empty) + "::" + (otherStableId ?? string.Empty);
        }

        private static string RelationLabelFor(PawnRelationDef def, Pawn otherPawn)
        {
            return RelationLabelFor(def, otherPawn, null);
        }

        /// <summary>
        /// Resolves a display label. <paramref name="relationDefName"/> lets
        /// synthesized opinion ties (friend/rival) resolve to a translated label
        /// even though they have no backing PawnRelationDef.
        /// </summary>
        private static string RelationLabelFor(PawnRelationDef def, Pawn otherPawn, string relationDefName)
        {
            if (def == null)
            {
                if (relationDefName == SocialRelationFilter.FriendRelationKey)
                {
                    return FriendLabel();
                }
                if (relationDefName == SocialRelationFilter.RivalRelationKey)
                {
                    return RivalLabel();
                }
                return string.Empty;
            }
            if (otherPawn != null)
            {
                string gendered = def.GetGenderSpecificLabel(otherPawn);
                if (!string.IsNullOrEmpty(gendered))
                {
                    return gendered;
                }
            }
            if (!string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            return def.defName;
        }
    }
}
