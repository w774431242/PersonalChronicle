using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// Partial of <see cref="ArchiveMainTabWindow"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveMainTabWindow : MainTabWindow
    {

        private void RefreshCacheIfNeeded(IArchiveService service)
        {
            // v4.5.3: the global DataRevision bumps every WorkSampleIntervalTicks
            // (2s) via MarkChanged(), so it is NOT a safe gate on its own — comparing
            // it would force a full rebuild on every window tick, defeating the
            // CacheRefreshInterval throttle (RimWorld manual: never recompute in the
            // draw path; throttle expensive rebuilds). The throttle gate (tick-based)
            // is authoritative; revision is only tracked so RefreshNow can refresh
            // view-scoped caches when the underlying data actually changed.
            if (GenTicks.TicksGame < nextRefreshTick)
            {
                return;
            }
            nextRefreshTick = GenTicks.TicksGame + CacheRefreshInterval;
            RefreshNow(service);
        }

        private void RefreshNow(IArchiveService service)
        {
            // v4.0: mirror persisted home view preference from the game component via service.
            int mode = service.GetHomeViewMode();
            homeViewMode = mode == (int)HomeViewMode.Timeline ? HomeViewMode.Timeline : HomeViewMode.Kpi;
            // P2-6/P3: every section's query + sort + null-guard is delegated to the
            // read-model provider. The window only consumes immutable snapshots and
            // applies translation/formatting (its own translation context).
            long revision = service.GetDataRevision();
            RebuildCategoryCacheViaProvider(service, revision);
            ReadModels.HomeSnapshot home = uiDataProvider.BuildHome(service, revision);
            RebuildPawnCounts(home);
            RebuildRecentLines(home);
            RebuildImportantCards(home);
            // v4.5.3: view-scoped cache short-circuits. Rebuilding the full detail
            // graph (BuildDetail + ScanCombatData + production + intensity + links)
            // on every throttled refresh is wasted work while the user is on Home /
            // Overview / Event views, which never render it. NavigateTarget already
            // force-rebuilds the detail cache on tab entry, so the previous snapshot
            // stays valid until the user returns to a detail tab.
            bool detailViewActive = view == MainView.PawnDetail || view == MainView.WeaponDetail;
            if (detailViewActive)
            {
                RebuildDetailCache(service, revision);
            }
            RebuildEventCache(service, revision);
            // v4.16: 职业档案总览仅在 Career 视图激活时构建（其余视图不渲染它）。
            if (view == MainView.Career)
            {
                RefreshCareerCache(service);
            }
            // v4.5.3: only pull the full event stream when the Home timeline is the
            // active presentation; the KPI dashboard never reads cachedTimelineEvents.
            if (homeViewMode == HomeViewMode.Timeline)
            {
                // Sorted once at cache-rebuild time via the Read Model (never per-frame
                // in DrawHomeTimeline, and the ordering/null-guard lives in the snapshot
                // builder, not the window — design doc §7.4).
                cachedTimelineEvents = uiDataProvider.BuildTimelineEvents(service, revision);
            }
        }

        private void RebuildCategoryCacheViaProvider(IArchiveService service, long revision)
        {
            cachedCategoryObjects.Clear();
            // One builder call per category; ordering/null-guards live in the
            // read-model provider, not in the window. (P2-6)
            AddCategoryFromProvider(service, ArchiveCategoryKeys.Pawn, revision);
            AddCategoryFromProvider(service, ArchiveCategoryKeys.Thing, revision);
            AddCategoryFromProvider(service, ArchiveCategoryKeys.Battle, revision);
            AddCategoryFromProvider(service, ArchiveCategoryKeys.Location, revision);
        }

        private void AddCategoryFromProvider(IArchiveService service, string categoryKey, long revision)
        {
            ReadModels.OverviewSnapshot snap = uiDataProvider.BuildOverview(service, categoryKey, revision);
            List<ArchiveObject> objects;
            if (snap.CategoryObjects.TryGetValue(categoryKey, out objects) && objects != null)
            {
                cachedCategoryObjects[categoryKey] = objects;
            }
            else
            {
                cachedCategoryObjects[categoryKey] = new List<ArchiveObject>();
            }
            // v4.14: cache the location KPI strip + event counts for the Overview render.
            if (categoryKey == ArchiveCategoryKeys.Location)
            {
                cachedLocationKpis = snap.LocationKpis ?? new ReadModels.LocationKpisView();
                cachedLocationEventCounts = snap.LocationEventCounts
                    ?? new Dictionary<string, int>();
            }
            // v4.14: cache the Battle KPI strip + per-battle card aggregates.
            else if (categoryKey == ArchiveCategoryKeys.Battle)
            {
                cachedBattleKpis = snap.BattleKpis ?? new ReadModels.BattleKpisView();
            }
        }

        private void RebuildPawnCounts(ReadModels.HomeSnapshot home)
        {
            // All counts come from the read-model snapshot — the window renders only,
            // no business logic here (P2-6/P3). Active/archived are the archive
            // snapshot convention (DeathTick); the live colonist count is the
            // independent live-read path with its own 600-tick cache.
            cachedActivePawnCount = home.ActivePawnCount;
            cachedArchivedPawnCount = home.ArchivedPawnCount;
            cachedLiveColonistCount = home.LiveColonistCount;
            cachedLiveFreeCount = home.LiveFreeCount;
            cachedLiveSlaveCount = home.LiveSlaveCount;
            cachedLivePrisonerCount = home.LivePrisonerCount;
            cachedServiceDays = home.ServiceDays;
        }

        private void RebuildRecentLines(ReadModels.HomeSnapshot home)
        {
            cachedRecentLines = new List<RecentLineView>();

            // RecentEvents is already sorted descending by tick and null-guarded by
            // the read-model provider (P3). The window only formats.
            IReadOnlyList<ChronicleEvent> recent = home.RecentEvents;
            if (recent == null || recent.Count == 0)
            {
                return;
            }

            for (int i = 0; i < recent.Count; i++)
            {
                ChronicleEvent ev = recent[i];
                if (ev == null || ev.Primary == null || string.IsNullOrEmpty(ev.Primary.LabelSnapshot))
                {
                    continue;
                }
                cachedRecentLines.Add(new RecentLineView
                {
                    DateText = FormatDate(ev.Tick),
                    TitleText = EventName(ev),
                    TypeText = ev.Primary.LabelSnapshot,
                    Event = ev
                });
            }
        }

        private void RebuildImportantCards(ReadModels.HomeSnapshot home)
        {
            cachedImportantCards = new List<ImportantCardView>();

            // ImportantObjects is already sorted by event count descending and
            // null-guarded by the read-model provider (P3). Battle/Location have no
            // detail view, so they never appear here.
            IReadOnlyList<ArchiveObject> important = home.ImportantObjects;
            if (important == null || important.Count == 0)
            {
                return;
            }

            int take = System.Math.Min(important.Count, ImportantCardCount);
            for (int i = 0; i < take; i++)
            {
                ArchiveObject obj = important[i];
                if (obj == null)
                {
                    continue;
                }
                cachedImportantCards.Add(new ImportantCardView
                {
                    Label = ObjectDisplayLabel(obj),
                    SubLabel = ObjectSubLabel(obj),
                    TagLabel = CategoryLabel(obj.CategoryKey),
                    Target = NavTargetOfCategory(obj.CategoryKey),
                    StableId = obj.StableId
                });
            }
        }

        private void RebuildDetailCache(IArchiveService service, long revision)
        {
            ClearDetailCache();
            if (service == null
                || (view != MainView.PawnDetail && view != MainView.WeaponDetail)
                || string.IsNullOrEmpty(detailObjectId))
            {
                return;
            }

            // P3: event history + object resolution come from the read-model
            // provider so the window never issues the raw query/sort itself.
            ReadModels.DetailSnapshot detail = uiDataProvider.BuildDetail(service, detailObjectId, revision);
            cachedDetailObject = detail.DetailObject;
            // v4.4: map derived overview views (read-model is the single source).
            cachedLifePhases = detail.LifePhases ?? new List<ReadModels.LifePhaseView>();
            cachedCareerBars = detail.CareerBars ?? new List<ReadModels.CareerBarView>();
            cachedFootprint = detail.Footprint ?? new ReadModels.FootprintLedgerView();
            cachedMilestones = detail.Milestones ?? new List<ReadModels.MilestoneView>();
            cachedKeyEvents = detail.KeyEvents ?? new List<ReadModels.KeyEventView>();
            cachedHealth = detail.Health ?? new ReadModels.HealthView();
            // v1.1.4: medal wall (勋章墙) — read-model snapshot, window renders only.
            cachedMedals = detail.Medals ?? new List<ReadModels.MedalView>();
            cachedLegacy = detail.Legacy ?? new ReadModels.LegacyView();
            cachedOrigin = detail.Origin ?? new ReadModels.ThingOriginView();
            cachedMakerChain = detail.MakerChain ?? new ReadModels.MakerChainView();
            cachedCoUse = detail.CoUse ?? new ReadModels.CoUseView();
            cachedDecommission = detail.Decommission ?? new ReadModels.DecommissionView();
            cachedRelations = detail.Relations ?? new List<ReadModels.RelationView>();
            if (cachedDetailObject == null)
            {
                // Object vanished (data cleaned up): safe fallback to overview.
                view = MainView.Overview;
                overviewCategoryFilter = null;
                return;
            }

            // v4.5.4: RawEvents is already sorted ascending + null-free from the
            // Read Model; the window no longer re-queries or re-orders.
            IReadOnlyList<ChronicleEvent> events = detail.RawEvents;
            if (events != null && events.Count > 0)
            {
                List<ChronicleEvent> sorted = new List<ChronicleEvent>(events);
                cachedDetailRawEvents = sorted;
                cachedDetailEvents = new List<EventLineView>(sorted.Count);
                for (int i = 0; i < sorted.Count; i++)
                {
                    ChronicleEvent ev = sorted[i];
                    cachedDetailEvents.Add(new EventLineView
                    {
                        DateText = FormatDate(ev.Tick),
                        NameText = EventName(ev),
                        ParamsText = FormatParams(ev.Params),
                        DescriptionText = EventDescription(ev),
                        Chips = BuildChips(ev, service),
                        Event = ev,
                        Importance = ChronicleEventImportance.Resolve(ev)
                    });
                }
                ScanCombatData(service);
            }

            // v4.6.5: production ledger comes from the Read Model (BuildDetail),
            // no per-rebuild aggregation in the window.
            cachedProductionLines = new List<ReadModels.ProductionLineView>(
                detail.ProductionLines ?? (IReadOnlyList<ReadModels.ProductionLineView>)new List<ReadModels.ProductionLineView>());
            cachedProductionSummary = service.GetProductionSummary(detailObjectId);
            IWorkIntensityService intensityService = service as IWorkIntensityService;
            if (intensityService != null && cachedDetailObject is PawnObject)
            {
                // v4.5.2: left badge refresh throttled to 30 game-days. We only
                // re-pull from the service at that cadence (or on object switch);
                // the derivation itself is filtered upstream. The work-type
                // breakdown stays current with the normal cache cadence.
                if (detailObjectId != cachedBadgeObjectId
                    || GenTicks.TicksGame >= nextBadgeRefreshTick)
                {
                    cachedWorkIntensity = intensityService.GetWorkIntensity(detailObjectId);
                    cachedTiers = intensityService.GetIntensityTiers();
                    nextBadgeRefreshTick = GenTicks.TicksGame + BadgeRefreshIntervalTicks;
                    cachedBadgeObjectId = detailObjectId;
                }
                cachedIntensityWorkTypes = intensityService.GetWorkTypeBreakdown(
                    detailObjectId,
                    includeZeroWorkTypes: false);
            }

            IReadOnlyList<ArchiveObject> linked = service.GetLinkedObjects(detailObjectId);
            if (linked != null)
            {
                cachedLinkedObjects = new List<LinkedObjectView>(linked.Count);
                for (int i = 0; i < linked.Count; i++)
                {
                    ArchiveObject link = linked[i];
                    if (link == null)
                    {
                        continue;
                    }
                    cachedLinkedObjects.Add(new LinkedObjectView
                    {
                        StableId = link.StableId,
                        Label = ObjectDisplayLabel(link),
                        CategoryLabel = CategoryLabel(link.CategoryKey),
                        CategoryKey = link.CategoryKey,
                        Target = NavTargetOfCategory(link.CategoryKey),
                        // v4.14: shared-fate count = how many detail events reference
                        // this linked object (Primary or Subjects).
                        SharedCount = CountEventReferences(link.StableId)
                    });
                }
            }
        }

        private int CountEventReferences(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return 0;
            }
            int count = 0;
            for (int i = 0; i < cachedDetailRawEvents.Count; i++)
            {
                ChronicleEvent ev = cachedDetailRawEvents[i];
                if (ev == null)
                {
                    continue;
                }
                if (ev.Primary != null && ev.Primary.StableId == stableId)
                {
                    count++;
                    continue;
                }
                if (ev.Subjects != null)
                {
                    for (int s = 0; s < ev.Subjects.Count; s++)
                    {
                        ObjectRef sub = ev.Subjects[s];
                        if (sub != null && sub.StableId == stableId)
                        {
                            count++;
                            break;
                        }
                    }
                }
            }
            return count;
        }

        private void ClearDetailCache()
        {
            cachedDetailObject = null;
            cachedDetailRawEvents = new List<ChronicleEvent>();
            cachedDetailEvents = new List<EventLineView>();
            cachedLinkedObjects = new List<LinkedObjectView>();
            cachedCombatLines = new List<CombatLineView>();
            cachedKillLines = new List<CombatLineView>();
            cachedBattleLines = new List<CombatLineView>();
            cachedFactionCodex = new List<FactionCodexView>();
            expandedFactions.Clear();
            expandedScroll.Clear();
            // v4.14: overview card expansions are per-object state — clear them on
            // any detail-object switch so a stale expanded set never leaks across
            // objects (same rule as expandedFactions).
            expandedLocations.Clear();
            expandedBattles.Clear();
            cachedProductionLines = new List<ReadModels.ProductionLineView>();
            cachedProductionSummary = new ProductionSummaryView(0, 0f, -1L, new List<ProductionTypeView>());
            cachedWorkIntensity = new WorkIntensityView(
                WorkIntensityEvaluation.Undefined(null, "builtin"), null);
            cachedIntensityWorkTypes = new List<WorkIntensityWorkTypeView>();
            cachedTiers = new List<WorkIntensityTierView>();
            nextBadgeRefreshTick = 0L;
            cachedBadgeObjectId = string.Empty;
            cachedDeathKiller = null;
            cachedDeathBattleLabel = null;
            cachedCraftCrafterId = null;
            cachedCraftCrafterLabel = null;
            cachedCraftTick = -1L;
            cachedLifePhases = new List<ReadModels.LifePhaseView>();
            cachedCareerBars = new List<ReadModels.CareerBarView>();
            cachedFootprint = new ReadModels.FootprintLedgerView();
            cachedMilestones = new List<ReadModels.MilestoneView>();
            cachedKeyEvents = new List<ReadModels.KeyEventView>();
            cachedHealth = new ReadModels.HealthView();
            cachedMedals = new List<ReadModels.MedalView>();
            cachedLegacy = new ReadModels.LegacyView();
            cachedOrigin = new ReadModels.ThingOriginView();
            cachedMakerChain = new ReadModels.MakerChainView();
            cachedCoUse = new ReadModels.CoUseView();
            cachedDecommission = new ReadModels.DecommissionView();
            cachedRelations = new List<ReadModels.RelationView>();
            legacyExpanded = false;
        }

        private static List<ChipView> BuildChips(ChronicleEvent ev, IArchiveService service)
        {
            List<ChipView> chips = new List<ChipView>();
            if (ev == null)
            {
                return chips;
            }
            if (ev.Primary != null && !string.IsNullOrEmpty(ev.Primary.StableId))
            {
                AddChip(chips, ev.Primary, service);
            }
            if (ev.Subjects != null)
            {
                for (int i = 0; i < ev.Subjects.Count; i++)
                {
                    ObjectRef s = ev.Subjects[i];
                    if (s == null || string.IsNullOrEmpty(s.StableId))
                    {
                        continue;
                    }
                    AddChip(chips, s, service);
                }
            }
            return chips;
        }

        private static void AddChip(List<ChipView> chips, ObjectRef r, IArchiveService service)
        {
            if (chips.Count >= MaxChipsPerEvent)
            {
                return;
            }
            chips.Add(new ChipView
            {
                Label = ResolveRefLabel(r, service),
                Target = NavTargetOfCategory(r.CategoryKey),
                StableId = r.StableId
            });
        }

        private void ScanCombatData(IArchiveService service)
        {
            cachedCombatLines = new List<CombatLineView>();
            cachedKillLines = new List<CombatLineView>();
            cachedBattleLines = new List<CombatLineView>();
            cachedFactionCodex = new List<FactionCodexView>();
            if (cachedDetailRawEvents == null)
            {
                return;
            }
            bool isWeapon = cachedDetailObject is ThingObject;
            string selfId = detailObjectId;
            HashSet<string> seenBattles = new HashSet<string>();

            for (int i = 0; i < cachedDetailRawEvents.Count; i++)
            {
                ChronicleEvent ev = cachedDetailRawEvents[i];
                if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
                {
                    continue;
                }
                if (IsDeathEvent(ev))
                {
                    string killerText = string.Empty;
                    if (ev.Params != null)
                    {
                        string k;
                        if (ev.Params.TryGetValue(ChronicleEventParams.Killer, out k) && !string.IsNullOrEmpty(k))
                        {
                            killerText = k;
                        }
                    }
                    bool isOwnDeath = !isWeapon
                        && ev.Primary != null
                        && !string.IsNullOrEmpty(ev.Primary.StableId)
                        && ev.Primary.StableId == selfId;

                    if (isOwnDeath)
                    {
                        cachedDeathKiller = killerText;
                        // Battles linked on the death event (participant via death).
                        CollectBattleLinesFromEvent(ev, service, seenBattles);
                        // v4.14: the death-dossier "关联战役" row.
                        cachedDeathBattleLabel = FirstBattleLabelOnEvent(ev, service);
                    }
                    else if (isWeapon)
                    {
                        // Weapon as Subject on death → kill with this weapon.
                        // v4.3: combat detail shows only the weapon used (no killer name).
                        string victimLabel = ev.Primary != null
                            ? ResolveRefLabel(ev.Primary, service)
                            : EventName(ev);
                        string weaponLabel = FindWeaponLabelOnEvent(ev, service);
                        CombatLineView kill = new CombatLineView
                        {
                            DateText = FormatDate(ev.Tick),
                            TitleText = victimLabel,
                            SubText = string.IsNullOrEmpty(weaponLabel)
                                ? "PersonalChronicle.UI.KpiKills".Translate().ToString()
                                : weaponLabel,
                            Target = ev.Primary != null ? NavTargetOfCategory(ev.Primary.CategoryKey) : NavTarget.None,
                            StableId = ev.Primary != null ? ev.Primary.StableId : null,
                            TargetEvent = ev
                        };
                        cachedKillLines.Add(kill);
                        cachedCombatLines.Add(kill);
                    }
                    else
                    {
                        // Pawn is killer (Subject edge) or otherwise associated → kill credit.
                        // v4.3: combat detail shows only the weapon used (no killer name, no assist name).
                        string victimLabel = ev.Primary != null
                            ? ResolveRefLabel(ev.Primary, service)
                            : EventName(ev);
                        string weaponLabel = FindWeaponLabelOnEvent(ev, service);
                        CombatLineView kill = new CombatLineView
                        {
                            DateText = FormatDate(ev.Tick),
                            TitleText = victimLabel,
                            SubText = string.IsNullOrEmpty(weaponLabel)
                                ? "PersonalChronicle.UI.KpiKills".Translate().ToString()
                                : weaponLabel,
                            Target = ev.Primary != null ? NavTargetOfCategory(ev.Primary.CategoryKey) : NavTarget.None,
                            StableId = ev.Primary != null ? ev.Primary.StableId : null,
                            TargetEvent = ev
                        };
                        cachedKillLines.Add(kill);
                        cachedCombatLines.Add(kill);
                        CollectBattleLinesFromEvent(ev, service, seenBattles);
                    }
                }
                else if (IsBattleEvent(ev))
                {
                    string battleKey = ev.Primary != null ? ev.Primary.StableId : ("ev:" + ev.Id);
                    if (!string.IsNullOrEmpty(battleKey) && !seenBattles.Add(battleKey))
                    {
                        // already listed
                    }
                    else
                    {
                        CombatLineView battleLine = new CombatLineView
                        {
                            DateText = FormatDate(ev.Tick),
                            TitleText = EventName(ev),
                            SubText = "PersonalChronicle.UI.BattleParticipants".Translate().ToString(),
                            Target = NavTarget.Event,
                            StableId = null,
                            TargetEvent = ev
                        };
                        cachedBattleLines.Add(battleLine);
                        cachedCombatLines.Add(battleLine);
                    }
                }
                else if (IsCraftEvent(ev))
                {
                    if (cachedCraftTick < 0L)
                    {
                        cachedCraftTick = ev.Tick;
                    }
                    if (cachedCraftCrafterId == null && ev.Subjects != null)
                    {
                        for (int s = 0; s < ev.Subjects.Count; s++)
                        {
                            ObjectRef sub = ev.Subjects[s];
                            if (sub != null && sub.CategoryKey == ArchiveCategoryKeys.Pawn && !string.IsNullOrEmpty(sub.StableId))
                            {
                                cachedCraftCrafterId = sub.StableId;
                                cachedCraftCrafterLabel = ResolveRefLabel(sub, service);
                                break;
                            }
                        }
                    }
                }
            }

            BuildFactionCodex(service);
        }

        private void BuildFactionCodex(IArchiveService service)
        {
            cachedFactionCodex = new List<FactionCodexView>();
            if (cachedKillLines == null || cachedKillLines.Count == 0)
            {
                return;
            }

            Dictionary<string, FactionCodexView> byKey =
                new Dictionary<string, FactionCodexView>();
            Dictionary<string, HashSet<string>> battleIdsByFaction =
                new Dictionary<string, HashSet<string>>();

            for (int i = 0; i < cachedKillLines.Count; i++)
            {
                CombatLineView kv = cachedKillLines[i];
                ChronicleEvent ev = kv.TargetEvent;
                string victimStableId = ev != null && ev.Params.TryGetValue(ChronicleEventParams.VictimStableId, out string vsid) ? vsid : null;
                bool victimIsArchive = false;
                if (victimStableId != null && service != null
                    && service.GetObject(victimStableId) is PawnObject victimPawn)
                {
                    victimIsArchive = victimPawn.IsArchived;
                }

                string factionKey;
                string displayName;
                ArchiveUiStyle.FactionCodexKind kind;
                string relationKey;

                bool unknownKiller = ev != null
                    && ev.Params.TryGetValue(ChronicleEventParams.Killer, out string killerLabel)
                    && killerLabel == ChronicleEventParams.UnknownKillerLabel;

                string victimFactionDef = ev != null && ev.Params.TryGetValue(ChronicleEventParams.VictimFactionDefName, out string vfd) ? vfd : null;
                string victimFactionLabel = ev != null && ev.Params.TryGetValue(ChronicleEventParams.VictimFactionLabel, out string vfl) ? vfl : null;
                string victimCategory = ev != null && ev.Params.TryGetValue(ChronicleEventParams.VictimCategory, out string vcat) ? vcat : null;

                if (unknownKiller && string.IsNullOrEmpty(victimFactionDef))
                {
                    factionKey = FactionBucketUnknown;
                    displayName = "PersonalChronicle.UI.FactionUnknown".Translate().ToString();
                    kind = ArchiveUiStyle.FactionCodexKind.Unknown;
                    relationKey = FactionRelationUnresolved;
                }
                else if (victimIsArchive)
                {
                    factionKey = FactionBucketPlayer;
                    displayName = "PersonalChronicle.UI.FactionPlayer".Translate().ToString();
                    kind = ArchiveUiStyle.FactionCodexKind.Player;
                    relationKey = FactionRelationAlly;
                }
                else if (!string.IsNullOrEmpty(victimFactionDef))
                {
                    factionKey = victimFactionDef;
                    displayName = !string.IsNullOrEmpty(victimFactionLabel)
                        ? victimFactionLabel
                        : FactionDefLabel(victimFactionDef);
                    bool isMechanoid = victimCategory == ChronicleEventParams.VictimCategoryMechanoid;
                    bool isAnimal = victimCategory == ChronicleEventParams.VictimCategoryAnimal;
                    if (isMechanoid)
                    {
                        kind = ArchiveUiStyle.FactionCodexKind.Mechanoid;
                    }
                    else if (isAnimal)
                    {
                        kind = ArchiveUiStyle.FactionCodexKind.Animal;
                    }
                    else
                    {
                        kind = ArchiveUiStyle.FactionCodexKind.Enemy;
                    }
                    relationKey = FactionRelationHostile;
                }
                else
                {
                    // No faction: animal → wild bucket, otherwise generic factionless.
                    if (victimCategory == ChronicleEventParams.VictimCategoryAnimal)
                    {
                        factionKey = FactionBucketWild;
                        displayName = "PersonalChronicle.UI.FactionWild".Translate().ToString();
                        kind = ArchiveUiStyle.FactionCodexKind.Animal;
                    }
                    else
                    {
                        factionKey = FactionBucketFactionless;
                        displayName = "PersonalChronicle.UI.FactionFactionless".Translate().ToString();
                        kind = ArchiveUiStyle.FactionCodexKind.Unknown;
                    }
                    relationKey = FactionRelationNeutral;
                }

                if (!byKey.TryGetValue(factionKey, out FactionCodexView card))
                {
                    card = new FactionCodexView
                    {
                        FactionKey = factionKey,
                        DisplayName = displayName,
                        Kind = kind,
                        RelationKey = relationKey,
                        KillCount = 0,
                        RaidCount = 0,
                        BattleCount = 0,
                        OurLossCount = 0,
                        MemberLines = new List<CombatLineView>()
                    };
                    byKey[factionKey] = card;
                    battleIdsByFaction[factionKey] = new HashSet<string>();
                }

                card.KillCount++;
                card.MemberLines.Add(kv);
                if (victimIsArchive)
                {
                    // Our losses: a kill event whose victim is one of our archived pawns.
                    card.OurLossCount++;
                }

                // Battle participation: collect distinct battle subjects from the event.
                if (ev != null && ev.Subjects != null
                    && battleIdsByFaction.TryGetValue(factionKey, out HashSet<string> battleIds))
                {
                    for (int s = 0; s < ev.Subjects.Count; s++)
                    {
                        ObjectRef sub = ev.Subjects[s];
                        if (sub != null && sub.CategoryKey == ArchiveCategoryKeys.Battle
                            && !string.IsNullOrEmpty(sub.StableId))
                        {
                            battleIds.Add(sub.StableId);
                        }
                    }
                }
            }

            cachedFactionCodex = new List<FactionCodexView>(byKey.Values);
            // Build composition (victim kind breakdown) per card, apply distinct battle counts, then sort.
            for (int c = 0; c < cachedFactionCodex.Count; c++)
            {
                FactionCodexView cv = cachedFactionCodex[c];
                if (battleIdsByFaction.TryGetValue(cv.FactionKey, out HashSet<string> battleIds))
                {
                    cv.BattleCount = battleIds.Count;
                    cv.RaidCount = battleIds.Count;
                }
                Dictionary<string, int> kindCounts = new Dictionary<string, int>();
                for (int m = 0; m < cv.MemberLines.Count; m++)
                {
                    ChronicleEvent mev = cv.MemberLines[m].TargetEvent;
                    // Composition keys are grouping buckets only (never resolved as a Def).
                    // Use a synthetic sentinel instead of a translation key so the slot
                    // keeps a single, honest meaning: "a PawnKindDef name, or unknown".
                    string kindDef = mev != null && mev.Params.TryGetValue(ChronicleEventParams.VictimKindDefName, out string kd) && !string.IsNullOrEmpty(kd)
                        ? kd
                        : FactionBucketUnknown;
                    if (!kindCounts.TryGetValue(kindDef, out int kc))
                    {
                        kindCounts[kindDef] = 0;
                    }
                    kindCounts[kindDef]++;
                }
                cv.Composition = new List<KeyValuePair<string, int>>(kindCounts);
                cachedFactionCodex[c] = cv;
            }
            // Sort: by KillCount desc, player card pinned to bottom.
            cachedFactionCodex.Sort((a, b) =>
            {
                bool aPlayer = a.FactionKey == FactionBucketPlayer;
                bool bPlayer = b.FactionKey == FactionBucketPlayer;
                if (aPlayer != bPlayer)
                {
                    return aPlayer ? 1 : -1;
                }
                return b.KillCount.CompareTo(a.KillCount);
            });
        }

        private void CollectBattleLinesFromEvent(ChronicleEvent ev, IArchiveService service, HashSet<string> seenBattles)
        {
            if (ev == null || ev.Subjects == null)
            {
                return;
            }
            for (int s = 0; s < ev.Subjects.Count; s++)
            {
                ObjectRef sub = ev.Subjects[s];
                if (sub == null || sub.CategoryKey != ArchiveCategoryKeys.Battle
                    || string.IsNullOrEmpty(sub.StableId))
                {
                    continue;
                }
                if (!seenBattles.Add(sub.StableId))
                {
                    continue;
                }
                string battleTitle = sub.StableId;
                ArchiveObject battleObj = service.GetObject(sub.StableId);
                if (battleObj is BattleObject battle && !string.IsNullOrEmpty(battle.IncidentDefName))
                {
                    battleTitle = IncidentDefLabel(battle.IncidentDefName);
                }
                CombatLineView battleLine = new CombatLineView
                {
                    DateText = FormatDate(ev.Tick),
                    TitleText = battleTitle,
                    SubText = "PersonalChronicle.UI.BattleParticipants".Translate().ToString(),
                    Target = NavTarget.Event,
                    StableId = null,
                    TargetEvent = ev
                };
                cachedBattleLines.Add(battleLine);
                cachedCombatLines.Add(battleLine);
            }
        }

        private static string FirstBattleLabelOnEvent(ChronicleEvent ev, IArchiveService service)
        {
            if (ev == null || ev.Subjects == null)
            {
                return null;
            }
            for (int s = 0; s < ev.Subjects.Count; s++)
            {
                ObjectRef sub = ev.Subjects[s];
                if (sub == null || sub.CategoryKey != ArchiveCategoryKeys.Battle
                    || string.IsNullOrEmpty(sub.StableId))
                {
                    continue;
                }
                ArchiveObject battleObj = service != null ? service.GetObject(sub.StableId) : null;
                if (battleObj is BattleObject battle && !string.IsNullOrEmpty(battle.IncidentDefName))
                {
                    return IncidentDefLabel(battle.IncidentDefName);
                }
                return sub.StableId;
            }
            return null;
        }

        private static string FindWeaponLabelOnEvent(ChronicleEvent ev, IArchiveService service)
        {
            if (ev == null || ev.Subjects == null)
            {
                return null;
            }
            for (int i = 0; i < ev.Subjects.Count; i++)
            {
                ObjectRef sub = ev.Subjects[i];
                if (sub != null && sub.CategoryKey == ArchiveCategoryKeys.Thing)
                {
                    return ResolveRefLabel(sub, service);
                }
            }
            return null;
        }

        private static string ThingDefNameFromStableId(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return string.Empty;
            }
            int colon = stableId.IndexOf(':');
            if (colon <= 0)
            {
                return stableId;
            }
            return stableId.Substring(0, colon);
        }

        private void RebuildEventCache(IArchiveService service, long revision)
        {
            cachedEventTree = new List<TreeLineView>();
            cachedEventDescription = string.Empty;
            if (service == null || view != MainView.EventDetail || cachedEventDetail == null)
            {
                return;
            }

            cachedEventDescription = EventDescription(cachedEventDetail);

            List<ObjectRef> pawnSubjects = new List<ObjectRef>();
            List<ObjectRef> thingSubjects = new List<ObjectRef>();
            List<ObjectRef> battleSubjects = new List<ObjectRef>();
            List<ObjectRef> locationSubjects = new List<ObjectRef>();
            if (cachedEventDetail.Subjects != null)
            {
                for (int i = 0; i < cachedEventDetail.Subjects.Count; i++)
                {
                    ObjectRef s = cachedEventDetail.Subjects[i];
                    if (s == null)
                    {
                        continue;
                    }
                    if (s.CategoryKey == ArchiveCategoryKeys.Pawn)
                    {
                        pawnSubjects.Add(s);
                    }
                    else if (s.CategoryKey == ArchiveCategoryKeys.Thing)
                    {
                        thingSubjects.Add(s);
                    }
                    else if (s.CategoryKey == ArchiveCategoryKeys.Battle)
                    {
                        battleSubjects.Add(s);
                    }
                    else if (s.CategoryKey == ArchiveCategoryKeys.Location)
                    {
                        locationSubjects.Add(s);
                    }
                }
            }

            ObjectRef primary = cachedEventDetail.Primary;
            List<BranchView> branches = new List<BranchView>();

            if (primary != null && primary.CategoryKey == ArchiveCategoryKeys.Pawn && !string.IsNullOrEmpty(primary.StableId))
            {
                BranchView b = new BranchView();
                b.HeaderKey = "PersonalChronicle.UI.Character";
                b.Leaves = new List<LeafView>();
                b.Leaves.Add(new LeafView
                {
                    Label = ResolveRefLabel(primary, service),
                    Target = NavTarget.Pawn,
                    StableId = primary.StableId
                });
                branches.Add(b);
            }

            if (cachedEventDetail.Params != null
                && cachedEventDetail.Params.TryGetValue(ChronicleEventParams.Killer, out string killer)
                && !string.IsNullOrEmpty(killer))
            {
                BranchView b = new BranchView();
                b.HeaderKey = "PersonalChronicle.UI.Killer";
                b.Leaves = new List<LeafView>();
                b.Leaves.Add(new LeafView { Label = killer, Target = NavTarget.None, StableId = null });
                branches.Add(b);
            }

            if (cachedEventDetail.Params != null
                && cachedEventDetail.Params.TryGetValue(ChronicleEventParams.Assist, out string assist)
                && !string.IsNullOrEmpty(assist))
            {
                BranchView b = new BranchView();
                b.HeaderKey = "PersonalChronicle.UI.Assist";
                b.Leaves = new List<LeafView>();
                b.Leaves.Add(new LeafView { Label = assist, Target = NavTarget.None, StableId = null });
                branches.Add(b);
            }

            if (thingSubjects.Count > 0)
            {
                branches.Add(BuildBranch("PersonalChronicle.UI.Weapon", thingSubjects, NavTarget.Weapon, service));
            }
            if (battleSubjects.Count > 0)
            {
                branches.Add(BuildBranch("PersonalChronicle.UI.Battle", battleSubjects, NavTarget.None, service));
            }
            if (locationSubjects.Count > 0)
            {
                branches.Add(BuildBranch("PersonalChronicle.UI.Location", locationSubjects, NavTarget.None, service));
            }

            List<ObjectRef> participants = new List<ObjectRef>();
            for (int i = 0; i < pawnSubjects.Count; i++)
            {
                if (primary == null || pawnSubjects[i].StableId != primary.StableId)
                {
                    participants.Add(pawnSubjects[i]);
                }
            }
            if (participants.Count > 0)
            {
                branches.Add(BuildBranch("PersonalChronicle.UI.Participants", participants, NavTarget.Pawn, service));
            }

            // Follow-up events: later events of the primary object. The Where/OrderBy/
            // Take derivation now lives in the Read Model (ArchiveUiDataProvider.BuildEvent),
            // not in the window cache-rebuild path. (P2-7)
            List<ChronicleEvent> followups = new List<ChronicleEvent>(
                uiDataProvider.BuildEvent(service, cachedEventDetail, revision).FollowupEvents);
            if (followups.Count > 0)
            {
                BranchView b = new BranchView();
                b.HeaderKey = "PersonalChronicle.UI.FollowupEvents";
                b.Leaves = new List<LeafView>();
                for (int i = 0; i < followups.Count; i++)
                {
                    b.Leaves.Add(new LeafView
                    {
                        Label = EventName(followups[i]),
                        Target = NavTarget.Event,
                        StableId = null,
                        TargetEvent = followups[i]
                    });
                }
                branches.Add(b);
            }

            // Root line + branch/leaf lines. Connectors are drawn as GUI lines
            // in the render pass, so font glyph support is not required.
            cachedEventTree.Add(new TreeLineView
            {
                Depth = 0,
                Prefix = string.Empty,
                Label = EventName(cachedEventDetail),
                Target = NavTarget.None
            });
            for (int bi = 0; bi < branches.Count; bi++)
            {
                BranchView b = branches[bi];
                cachedEventTree.Add(new TreeLineView
                {
                    Depth = 1,
                    Prefix = string.Empty,
                    Label = b.HeaderKey.Translate().ToString(),
                    Target = NavTarget.None
                });
                for (int li = 0; li < b.Leaves.Count; li++)
                {
                    LeafView leaf = b.Leaves[li];
                    cachedEventTree.Add(new TreeLineView
                    {
                        Depth = 2,
                        Prefix = string.Empty,
                        Label = leaf.Label,
                        Target = leaf.Target,
                        StableId = leaf.StableId,
                        TargetEvent = leaf.TargetEvent
                    });
                }
            }
        }

        private static BranchView BuildBranch(string headerKey, List<ObjectRef> refs, NavTarget target, IArchiveService service)
        {
            BranchView b = new BranchView();
            b.HeaderKey = headerKey;
            b.Leaves = new List<LeafView>();
            for (int i = 0; i < refs.Count; i++)
            {
                ObjectRef r = refs[i];
                if (r == null)
                {
                    continue;
                }
                b.Leaves.Add(new LeafView
                {
                    Label = ResolveRefLabel(r, service),
                    Target = target,
                    StableId = r.StableId
                });
            }
            return b;
        }

        /// <summary>
        /// v4.16: 构建殖民地级职业档案总览行（Read Model 派生，排序/过滤归属 Provider）。
        /// 列表点击 → OpenPawnDetail 直接进入该殖民者的职业档案 Tab。
        /// </summary>
        private void RefreshCareerCache(IArchiveService service)
        {
            IReadOnlyList<ReadModels.CareerOverviewRowView> rows =
                uiDataProvider.BuildCareerOverview(service, service.GetDataRevision());
            cachedCareerRows = rows == null
                ? new List<ReadModels.CareerOverviewRowView>()
                : new List<ReadModels.CareerOverviewRowView>(rows);
        }

        /// <summary>
        /// v1.1.4 手动授勋后强制重建详情缓存（勋章墙快照）。绕过 CacheRefreshInterval
        /// 节流门控，使刚授予的勋章在关闭 Dialog 后立即反映到勋章墙，无需等下一次
        /// 节流刷新。仅当正处于详情视图时重建（与 RefreshNow 的 view 判定一致）。
        /// </summary>
        public void NotifyManualAward()
        {
            IArchiveService service = PersonalChronicleMod.ArchiveService;
            if (service == null)
            {
                return;
            }
            long revision = service.GetDataRevision();
            if (view == MainView.PawnDetail || view == MainView.WeaponDetail)
            {
                RebuildDetailCache(service, revision);
            }
        }
    }
}
