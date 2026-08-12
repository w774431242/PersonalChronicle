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
    /// Main-tab archive window (v3.1). Five shell views + detail tabs:
    ///
    ///   Shell   — header + left sidebar + main content.
    ///   Home / Overview / Event — unchanged shell behaviour.
    ///   Pawn detail tabs (4): Overview · Career · CombatLog · Social.
    ///   Weapon detail tabs (4): Overview · Timeline · CombatLog · Custody.
    ///
    /// Archive theme: historical facts and cumulative stats only — not a
    /// vanilla character-panel mirror (no work priorities, no live hediff list).
    /// Career work time comes from sampled WorkTimeAccumulator via IArchiveService.
    /// All user-visible copy uses PersonalChronicle.UI.* keys.
    /// </summary>
    public sealed partial class ArchiveMainTabWindow : MainTabWindow
    {
        private enum MainView
        {
            Home,
            Overview,
            PawnDetail,
            WeaponDetail,
            EventDetail
        }

        private enum NavTarget
        {
            None,
            Pawn,
            Weapon,
            Event
        }

        private const float HeaderHeight = ArchiveUiStyle.HeaderHeight;
        private const float SidebarWidth = ArchiveUiStyle.SidebarWidth;
        private const float Gap = ArchiveUiStyle.Gap;
        private const float RowHeight = 40f;
        private const float TimelineRowHeight = 34f;
        private const float ChipRowHeight = 26f;
        private const int RecentRecordCount = 6;
        private const int ImportantCardCount = 4;
        private const int MaxChipsPerEvent = 3;
        private const long CacheRefreshInterval = 120L;
        // v4.5.2: left badge (intensity tier) refresh throttle — mapped from the
        // per-30-game-day cadence the design calls for, expressed in ticks.
        // Computation/derivation is filtered upstream; this only controls pull cadence.
        private const long BadgeRefreshIntervalTicks = 30L * GenDate.TicksPerDay;

        // P4-1: health summary thresholds (SummaryHealthPercent bands).
        private const float HealthGoodThreshold = 0.6f;
        private const float HealthInjuredThreshold = 0.3f;

        // ---- v4.3 faction codex: synthetic bucket keys ----
        // These are NOT FactionDef defNames. They are internal aggregation buckets for
        // kills that cannot be attributed to a real faction. The "__x__" shape is chosen
        // so it can never collide with a real defName from vanilla or any third-party mod.
        private const string FactionBucketUnknown = "__unknown__";
        private const string FactionBucketPlayer = "__player__";
        private const string FactionBucketWild = "__wild__";
        private const string FactionBucketFactionless = "__factionless__";

        // ---- v4.3 faction codex: relation keys (drive badge label + colour) ----
        private const string FactionRelationHostile = "hostile";
        private const string FactionRelationNeutral = "neutral";
        private const string FactionRelationAlly = "ally";
        private const string FactionRelationUnresolved = "unresolved";

        // ---- v4.3 faction codex: layout metrics ----
        /// <summary>Panel width at/above which the codex switches from 1 to 2 columns.</summary>
        private const float FactionCodexTwoColumnWidth = 720f;
        private const float FactionCodexPadding = 10f;
        private const float FactionCodexHeaderHeight = 26f;
        private const float FactionCodexStatHeight = 64f;
        private const float FactionCodexBarHeight = 6f;
        private const float FactionCodexDateColWidth = 96f;
        private const float FactionCodexTitleColOffset = 100f;
        private const float FactionCodexSubColWidth = 116f;
        private const float FactionCodexScrollbarWidth = 16f;
        private const float FactionCodexEmptyRowHeight = 22f;
        private const float FactionCodexDotSize = 12f;
        /// <summary>Reserved width of the relation badge at the card's top-right.</summary>
        private const float FactionCodexRelationWidth = 62f;

        // ---- Navigation state ----
        private MainView view = MainView.Home;
        private string detailObjectId;
        private int detailTabIndex;
        private string overviewCategoryFilter;
        private ChronicleEvent cachedEventDetail;

        // ---- Scroll state (one per view; tab switches keep detail scroll) ----
        private Vector2 homeScroll;
        private Vector2 overviewScroll;
        private Vector2 detailScroll;
        private Vector2 eventScroll;
        /// <summary>Scroll-wheel zoom factor for the social network graph (1 = default).</summary>
        private float socialNetworkZoom = 1f;
        /// <summary>Whether the user has scrolled the social graph (disables auto-fit).</summary>
        private bool socialNetworkZoomTouched;
        /// <summary>Left-drag pan offset for the social network graph.</summary>
        private Vector2 socialNetworkPan = Vector2.zero;

        // ---- Cached read views (rebuilt only in RefreshNow) ----
        private readonly Dictionary<string, List<ArchiveObject>> cachedCategoryObjects =
            new Dictionary<string, List<ArchiveObject>>();
        /// <summary>v4.13: location atlas cards expanded inline (click toggles).</summary>
        private readonly HashSet<string> expandedLocations = new HashSet<string>();
        /// <summary>v4.14: battle cards expanded inline (casualty detail toggles).</summary>
        private readonly HashSet<string> expandedBattles = new HashSet<string>();
        /// <summary>v4.14: location atlas KPI strip (8 cells), aggregated by the Read Model.</summary>
        private ReadModels.LocationKpisView cachedLocationKpis = new ReadModels.LocationKpisView();
        /// <summary>v4.14: per-location event counts for the atlas card sub-line.</summary>
        private Dictionary<string, int> cachedLocationEventCounts = new Dictionary<string, int>();
        /// <summary>v4.14: Battle KPI strip + per-battle card aggregates (Read Model).</summary>
        private ReadModels.BattleKpisView cachedBattleKpis = new ReadModels.BattleKpisView();
        /// <summary>v4.14: significant-relation table rows (importantRel), Read Model.</summary>
        private IReadOnlyList<ReadModels.RelationView> cachedRelations = new List<ReadModels.RelationView>();
        private List<RecentLineView> cachedRecentLines = new List<RecentLineView>();
        private List<ImportantCardView> cachedImportantCards = new List<ImportantCardView>();
        private int cachedActivePawnCount;
        private int cachedArchivedPawnCount;
        private int cachedLiveColonistCount;
        private int cachedLiveFreeCount;
        private int cachedLiveSlaveCount;
        private int cachedLivePrisonerCount;
        private int cachedServiceDays;

        // Detail cache (shared by PawnDetail / WeaponDetail).
        private ArchiveObject cachedDetailObject;
        private List<ChronicleEvent> cachedDetailRawEvents = new List<ChronicleEvent>();
        private List<EventLineView> cachedDetailEvents = new List<EventLineView>();
        private List<LinkedObjectView> cachedLinkedObjects = new List<LinkedObjectView>();
        private List<CombatLineView> cachedCombatLines = new List<CombatLineView>();
        /// <summary>P2: kills attributed to this detail object (pawn killer or weapon).</summary>
        private List<CombatLineView> cachedKillLines = new List<CombatLineView>();
        /// <summary>P2: battle participation rows only.</summary>
        private List<CombatLineView> cachedBattleLines = new List<CombatLineView>();
        /// <summary>v4.3: faction-codex cards aggregated from cachedKillLines.</summary>
        private List<FactionCodexView> cachedFactionCodex = new List<FactionCodexView>();
        /// <summary>v4.3: faction keys whose kill detail is expanded inline.</summary>
        private HashSet<string> expandedFactions = new HashSet<string>();
        /// <summary>v4.7: expand the full legacy holder table (default collapsed past 5 rows).</summary>
        private bool legacyExpanded;
        /// <summary>v4.3: per-faction scroll position inside the expanded kill-detail viewport.</summary>
        private Dictionary<string, Vector2> expandedScroll = new Dictionary<string, Vector2>();
        private List<ReadModels.ProductionLineView> cachedProductionLines = new List<ReadModels.ProductionLineView>();
        private ProductionSummaryView cachedProductionSummary =
            new ProductionSummaryView(0, 0f, -1L, new List<ProductionTypeView>());
        private WorkIntensityView cachedWorkIntensity =
            new WorkIntensityView(WorkIntensityEvaluation.Undefined(null, "builtin"), null);
        private IReadOnlyList<WorkIntensityWorkTypeView> cachedIntensityWorkTypes =
            new List<WorkIntensityWorkTypeView>();
        // L1: intensity tier ladder (Def-driven, stable across rebuilds) is pulled
        // once at the throttled badge cadence instead of every draw frame.
        private IReadOnlyList<WorkIntensityTierView> cachedTiers =
            new List<WorkIntensityTierView>();
        private string cachedDeathKiller;
        /// <summary>v4.14: battle label attached to the pawn's own death event (death dossier row).</summary>
        private string cachedDeathBattleLabel;
        private string cachedCraftCrafterId;
        private string cachedCraftCrafterLabel;
        private long cachedCraftTick = -1L;

        // v4.4: Pawn Overview derived content (mirrors DetailSnapshot; populated by
        // RebuildDetailCache from the read-model, never recomputed in the draw path).
        private IReadOnlyList<ReadModels.LifePhaseView> cachedLifePhases = new List<ReadModels.LifePhaseView>();
        private IReadOnlyList<ReadModels.CareerBarView> cachedCareerBars = new List<ReadModels.CareerBarView>();
        private ReadModels.FootprintLedgerView cachedFootprint = new ReadModels.FootprintLedgerView();
        private IReadOnlyList<ReadModels.MilestoneView> cachedMilestones = new List<ReadModels.MilestoneView>();
        private IReadOnlyList<ReadModels.KeyEventView> cachedKeyEvents = new List<ReadModels.KeyEventView>();
        private ReadModels.HealthView cachedHealth = new ReadModels.HealthView();
        /// <summary>v4.7: legacy chain (传承) for equipment detail — mirrors DetailSnapshot.Legacy.</summary>
        private ReadModels.LegacyView cachedLegacy = new ReadModels.LegacyView();
        // v4.9: equipment legacy extension (溯源 / 工坊署名链 / 同袍共用 / 退役仪式) —
        // mirrors DetailSnapshot; all read-model derived, never recomputed here.
        private ReadModels.ThingOriginView cachedOrigin = new ReadModels.ThingOriginView();
        private ReadModels.MakerChainView cachedMakerChain = new ReadModels.MakerChainView();
        private ReadModels.CoUseView cachedCoUse = new ReadModels.CoUseView();
        private ReadModels.DecommissionView cachedDecommission = new ReadModels.DecommissionView();

        // Event view cache.
        private List<TreeLineView> cachedEventTree = new List<TreeLineView>();
        private string cachedEventDescription = string.Empty;

        private long nextRefreshTick;
        // v4.5.2: throttle for the left intensity badge. Only the *pull cadence*
        // is throttled; derivation/computation lives in the service and is not
        // re-run here. Mapped to 30 game-days via BadgeRefreshIntervalTicks.
        private long nextBadgeRefreshTick;
        private string cachedBadgeObjectId = string.Empty;

        // Timeline presentation state. These are read-model filters only; they
        // never change what the capture layer persists.
        private bool timelineShowCareer = true;
        private bool timelineShowCombat = true;
        private bool timelineShowSocial = true;
        // Normal is the default: Join/Death/Battle/Social remain visible while
        // routine craft/build rows stay collapsed into career aggregates.
        private ChronicleImportance timelineMinimumImportance = ChronicleImportance.Normal;

        // v4.0: home overview view selector (B dashboard vs E chronicle timeline).
        // Mirrored into the persistent component so the choice survives restarts.
        private enum HomeViewMode : int
        {
            Kpi = 0,
            Timeline = 1
        }
        private HomeViewMode homeViewMode = HomeViewMode.Kpi;
        private const float HomeViewTabHeight = 38f;
        private IReadOnlyList<ChronicleEvent> cachedTimelineEvents;

        // P2-6: read-model provider. All section query+sort+null-guard logic is
        // delegated here; the window only consumes immutable snapshots.
        private ReadModels.IArchiveUiDataProvider uiDataProvider = new ReadModels.ArchiveUiDataProvider();

        // v3.1: biography tabs (Stats removed; Work/Skills/… merged into Career/Social).
        // v4.6.6: Timeline tab removed from the Pawn detail view per request
        // (pawn timeline is surfaced via the Home chronicle timeline instead).
        private static readonly string[] PawnTabKeys =
        {
            "Overview", "Career", "CombatLog", "Social"
        };

        // v4.7: item tabs — Custody (流转) renamed to Legacy (传承), the ownership
        // transfer chain view. v4.9: equipment legacy extension — 溯源(Origin) /
        // 同袍共用(CoUse) / 退役仪式(Decommission) join the tab set; Timeline and
        // CombatLog are retired for equipment (their content is folded into
        // Overview / Legacy / the new tabs).
        private static readonly string[] WeaponTabKeys =
        {
            "Overview", "Origin", "Legacy", "CoUse", "Decommission"
        };

        public override Vector2 RequestedTabSize
        {
            get { return new Vector2(1100f, 720f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            IArchiveService service = PersonalChronicleMod.ArchiveService;
            if (service == null)
            {
                DrawNoService(inRect);
                return;
            }

            RefreshCacheIfNeeded(service);

            Rect headerRect = new Rect(0f, 0f, inRect.width, HeaderHeight);
            Rect sidebarRect = new Rect(0f, HeaderHeight + Gap, SidebarWidth, inRect.height - HeaderHeight - Gap);
            Rect contentRect = new Rect(
                SidebarWidth + Gap, HeaderHeight + Gap,
                inRect.width - SidebarWidth - Gap, inRect.height - HeaderHeight - Gap);

            DrawHeader(headerRect);
            DrawSidebar(sidebarRect, service);
            DrawContent(contentRect, service);
        }

        // ---- Cache management -------------------------------------------------

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

        /// <summary>v4.14: counts how many cached detail events reference a stable id
        /// (as Primary or in Subjects) — the "共同事件数" for the intertwined list.</summary>
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

        /// <summary>
        /// P2 combat cache: splits kills vs battle participation; death dossier
        /// fields for own death. Weapon detail = all deaths involving the weapon
        /// as kill rows. Pawn detail = own death (dossier) + kills of others +
        /// battles (from Battle events and Battle subject edges).
        /// </summary>
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

        /// <summary>
        /// v4.3: aggregate cachedKillLines into faction-codex cards.
        /// Faction key derivation (priority): unknown-killer bucket → player-death →
        /// victimFactionDef → wild (animal, no faction) → factionless (no faction, non-animal).
        /// Victim faction/category are read from event Params snapshotted at record time
        /// (external victims are never archived), with graceful fallback for old saves.
        /// </summary>
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

        /// <summary>
        /// v4.14: first battle label attached to an event's Subjects (death dossier
        /// "关联战役" row). Returns null when the event carries no battle edge.
        /// </summary>
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

        /// <summary>
        /// Parses the ThingDefName out of a thing stable id. Shape written by
        /// ArchiveService: "&lt;defName&gt;:&lt;thingIDNumber&gt;". Returns the
        /// raw id unchanged when the shape is unexpected (defensive).
        /// </summary>
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

        // ---- Navigation --------------------------------------------------------

        private void GoHome()
        {
            view = MainView.Home;
            detailObjectId = null;
            overviewCategoryFilter = null;
            cachedEventDetail = null;
            ClearDetailCache();
            homeScroll = Vector2.zero;
        }

        private void GoOverview(string categoryFilter)
        {
            view = MainView.Overview;
            overviewCategoryFilter = categoryFilter;
            detailObjectId = null;
            cachedEventDetail = null;
            ClearDetailCache();
            overviewScroll = Vector2.zero;
        }

        /// <summary>
        /// v4.6: public navigation entry used by the pawn inspect tab
        /// (<see cref="ITab_Pawn_Chronicle"/>) to deep-link into a pawn's detail
        /// view. Resolves the service itself so external callers stay decoupled
        /// from the service singleton.
        /// </summary>
        public void RequestPawnDetail(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return;
            }
            IArchiveService service = PersonalChronicleMod.ArchiveService;
            if (service == null)
            {
                return;
            }
            OpenPawnDetail(service, stableId);
        }

        private void OpenPawnDetail(IArchiveService service, string stableId)
        {
            if (service == null || string.IsNullOrEmpty(stableId))
            {
                return;
            }
            detailObjectId = stableId;
            view = MainView.PawnDetail;
            detailTabIndex = 0;
            cachedEventDetail = null;
            detailScroll = Vector2.zero;
            RebuildDetailCache(service, service.GetDataRevision());
        }

        private void OpenWeaponDetail(IArchiveService service, string stableId)
        {
            if (service == null || string.IsNullOrEmpty(stableId))
            {
                return;
            }
            detailObjectId = stableId;
            view = MainView.WeaponDetail;
            detailTabIndex = 0;
            cachedEventDetail = null;
            detailScroll = Vector2.zero;
            RebuildDetailCache(service, service.GetDataRevision());
        }

        private void OpenEventDetail(IArchiveService service, ChronicleEvent ev)
        {
            if (service == null || ev == null)
            {
                return;
            }
            cachedEventDetail = ev;
            view = MainView.EventDetail;
            detailObjectId = null;
            ClearDetailCache();
            eventScroll = Vector2.zero;
            RebuildEventCache(service, service.GetDataRevision());
        }

        private void NavigateTarget(IArchiveService service, NavTarget target, string stableId, ChronicleEvent targetEvent)
        {
            switch (target)
            {
                case NavTarget.Pawn:
                    if (!string.IsNullOrEmpty(stableId))
                    {
                        OpenPawnDetail(service, stableId);
                    }
                    break;
                case NavTarget.Weapon:
                    if (!string.IsNullOrEmpty(stableId))
                    {
                        OpenWeaponDetail(service, stableId);
                    }
                    break;
                case NavTarget.Event:
                    if (targetEvent != null)
                    {
                        OpenEventDetail(service, targetEvent);
                    }
                    break;
            }
        }

        // ---- Layout -----------------------------------------------------------

        private void DrawHeader(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x + 2f, rect.y + 9f, 300f, 32f),
                "PersonalChronicle.UI.ColonyArchive".Translate().ToString());
            Text.Font = GameFont.Small;
            ArchiveUiStyle.DrawRule(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), ArchiveUiStyle.Accent);
        }

        private void DrawSidebar(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            ArchiveUiStyle.DrawPanel(rect);
            Rect inner = rect.ContractedBy(10f);

            const float itemHeight = 30f;
            const float itemGap = 3f;
            float y = inner.y;

            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Dim;
            Widgets.Label(new Rect(inner.x, y, inner.width, 18f),
                "PersonalChronicle.UI.Navigation".Translate().ToString());
            GUI.color = prevColor;
            y += 20f;

            if (DrawSidebarItem(inner.x, y, inner.width, itemHeight,
                "PersonalChronicle.UI.HomeOverview".Translate().ToString(), null, view == MainView.Home))
            {
                GoHome();
            }
            y += itemHeight + itemGap;
            if (DrawSidebarItem(inner.x, y, inner.width, itemHeight,
                "PersonalChronicle.UI.AllArchives".Translate().ToString(), null,
                view == MainView.Overview && string.IsNullOrEmpty(overviewCategoryFilter)))
            {
                GoOverview(null);
            }
            y += itemHeight + itemGap + 8f;

            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Dim;
            Widgets.Label(new Rect(inner.x, y, inner.width, 18f),
                "PersonalChronicle.UI.Categories".Translate().ToString());
            GUI.color = prevColor;
            y += 20f;

            y = DrawSidebarCategory(inner, y, itemHeight, itemGap, ArchiveCategoryKeys.Pawn, service);
            y = DrawSidebarCategory(inner, y, itemHeight, itemGap, ArchiveCategoryKeys.Thing, service);
            y = DrawSidebarCategory(inner, y, itemHeight, itemGap, ArchiveCategoryKeys.Battle, service);
            y = DrawSidebarCategory(inner, y, itemHeight, itemGap, ArchiveCategoryKeys.Location, service);
            y += 8f;

            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Dim;
            Widgets.Label(new Rect(inner.x, y, inner.width, 18f),
                "PersonalChronicle.UI.Tools".Translate().ToString());
            GUI.color = prevColor;
            y += 20f;

            y = DrawSidebarTool(inner, y, itemHeight, itemGap, "PersonalChronicle.UI.Favorites");
            y = DrawSidebarTool(inner, y, itemHeight, itemGap, "PersonalChronicle.UI.Milestones");
            DrawSidebarTool(inner, y, itemHeight, itemGap, "PersonalChronicle.UI.Search");
            GUI.color = prevColor;
            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
        }

        private float DrawSidebarCategory(Rect inner, float y, float height, float gap, string categoryKey, IArchiveService service)
        {
            int count = 0;
            if (cachedCategoryObjects.TryGetValue(categoryKey, out List<ArchiveObject> objects))
            {
                count = objects.Count;
            }
            bool selected = view == MainView.Overview && overviewCategoryFilter == categoryKey;
            // P4-4: formatted via translation key (no hardcoded " (n)" glue).
            string label = "PersonalChronicle.UI.CategoryCountLabel"
                .Translate(CategoryLabel(categoryKey), count)
                .ToString();
            if (DrawSidebarItem(inner.x, y, inner.width, height, label, null, selected))
            {
                GoOverview(categoryKey);
            }
            return y + height + gap;
        }

        private float DrawSidebarTool(Rect inner, float y, float height, float gap, string labelKey)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            Rect item = new Rect(inner.x, y, inner.width, height);
            ArchiveUiStyle.DrawSelectedNavigation(item, false);
            GUI.color = ArchiveUiStyle.Dim;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(item.x + 13f, item.y + 7f, item.width - 20f, 20f), labelKey.Translate().ToString());
            GUI.color = prevColor;
            Text.Font = prevFont;
            TooltipHandler.TipRegion(item, "PersonalChronicle.UI.ToolsPlaceholder".Translate().ToString());
            return y + height + gap;
        }

        private static bool DrawSidebarItem(float x, float y, float width, float height, string label, string countText, bool selected)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            Rect rect = new Rect(x, y, width, height);
            ArchiveUiStyle.DrawSelectedNavigation(rect, selected);
            Text.Font = GameFont.Small;
            if (string.IsNullOrEmpty(countText))
            {
                Widgets.Label(new Rect(rect.x + 13f, rect.y + 7f, rect.width - 20f, 20f), label);
            }
            else
            {
                Widgets.Label(new Rect(rect.x + 13f, rect.y + 7f, rect.width - 60f, 20f), label);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(rect.x + rect.width - 48f, rect.y + 7f, 38f, 20f), countText);
            }
            Text.Anchor = prevAnchor;
            Text.Font = prevFont;
            GUI.color = prevColor;
            return Widgets.ButtonInvisible(rect);
        }

        private void DrawContent(Rect rect, IArchiveService service)
        {
            ArchiveUiStyle.DrawPanel(rect);
            Rect inner = rect.ContractedBy(ArchiveUiStyle.PanelPadding);

            switch (view)
            {
                case MainView.Home:
                    DrawHomeContent(inner, service);
                    break;
                case MainView.Overview:
                    DrawOverviewContent(inner, service);
                    break;
                case MainView.PawnDetail:
                case MainView.WeaponDetail:
                    DrawDetailContent(inner, service);
                    break;
                case MainView.EventDetail:
                    DrawEventContent(inner, service);
                    break;
            }
        }

        // ---- Home --------------------------------------------------------------

        private float DrawBattleKpiStrip(Rect viewRect, float y, ReadModels.BattleKpisView kpi)
        {
            if (kpi == null)
            {
                return 0f;
            }
            int n = 5;
            float gap = 6f;
            float cellW = (viewRect.width - gap * (n - 1)) / n;
            string[] labels =
            {
                "PersonalChronicle.UI.BattleKpiTotal".Translate().ToString(),
                "PersonalChronicle.UI.BattleKpiDecisive".Translate().ToString(),
                "PersonalChronicle.UI.BattleKpiKills".Translate().ToString(),
                "PersonalChronicle.UI.BattleKpiLosses".Translate().ToString(),
                "PersonalChronicle.UI.BattleKpiRoster".Translate().ToString()
            };
            int[] values = { kpi.Total, kpi.Decisive, kpi.Kills, kpi.Losses, kpi.Roster };
            Color[] accents =
            {
                UITheme.Text, UITheme.Accent, UITheme.Alive, UITheme.Dead, UITheme.Info
            };
            for (int i = 0; i < n; i++)
            {
                Rect cell = new Rect(viewRect.x + i * (cellW + gap), y, cellW, BattleKpiStripHeight);
                UIComponents.StatCell(cell, labels[i], values[i].ToString(), accents[i]);
            }
            return BattleKpiStripHeight;
        }

        /// <summary>
        /// v4.11 P0: Overview › Battle cards — significance pill + threat tag +
        /// name + sub-line (date · N participants · duration) + three metric cells
        /// (force / kills / losses) + roster chips + inline casualty expansion.
        /// All data comes from the Read-Model snapshot (cachedBattleKpis) — the
        /// window renders only, never re-derives aggregates (v4.3 boundary).
        /// </summary>
        private float DrawBattleOverviewCards(Rect viewRect, float startY, List<ArchiveObject> objects, float gap, IArchiveService service)
        {
            int perRow = Mathf.Max(1, (int)((viewRect.width + gap) / (BattleCardWidth + gap)));
            float yCursor = startY;

            for (int i = 0; i < objects.Count; i++)
            {
                BattleObject battle = objects[i] as BattleObject;
                if (battle == null)
                {
                    continue;
                }
                int col = i % perRow;
                int row = i / perRow;
                float cardTop = startY + row * (BattleCardHeight + gap);
                Rect card = new Rect(
                    viewRect.x + col * (BattleCardWidth + gap),
                    cardTop,
                    BattleCardWidth, BattleCardHeight);
                bool expanded = expandedBattles.Contains(battle.StableId);

                // Read-Model card aggregate (falls back to field values when absent).
                ReadModels.BattleCardView agg = cachedBattleKpis != null
                    && cachedBattleKpis.Cards != null
                    && cachedBattleKpis.Cards.TryGetValue(battle.StableId, out ReadModels.BattleCardView v)
                    ? v : null;
                int kills = agg != null ? agg.Kills : 0;
                int losses = agg != null ? agg.Losses : 0;
                int participants = agg != null ? agg.Participants
                    : (battle.ParticipantIds != null ? battle.ParticipantIds.Count : 0);
                bool significant = agg != null ? agg.IsSignificant : false;
                string threatKey = agg != null ? agg.ThreatKey : battle.ThreatKey;

                Color accent = significant ? UITheme.Accent : UITheme.Muted;
                ArchiveUiStyle.DrawCard(card, accent);
                float x = card.x + UITheme.CardPadX;
                float w = card.width - UITheme.CardPadX * 2f;
                float y = card.y + UITheme.CardPadY;

                // 1) Category row: threat tag + significance pill.
                string threatText = BattleThreatText(threatKey);
                if (!string.IsNullOrEmpty(threatText))
                {
                    UIComponents.Badge(new Rect(x, y, 60f, 16f), threatText,
                        threatKey == "ThreatBig" ? UITheme.Accent : UITheme.Info);
                }
                string pillText = significant
                    ? "PersonalChronicle.UI.BattleCardDecisive".Translate().ToString()
                    : "PersonalChronicle.UI.BattleCardSkirmish".Translate().ToString();
                UIComponents.Badge(new Rect(x + 56f, y, 56f, 16f), pillText,
                    significant ? UITheme.Accent : UITheme.Muted);
                y += 20f;

                // 2) Battle title.
                UIComponents.Label(new Rect(x, y, w, 20f),
                    ObjectDisplayLabel(battle), UITheme.FontBody, ArchiveUiStyle.Info);
                y += 22f;

                // 3) Sub-line: date · N participants · duration.
                string dateText = battle.StartTick > 0L
                    ? RimWorld.GenDate.DateReadoutStringAt(battle.StartTick, UnityEngine.Vector2.zero)
                    : "PersonalChronicle.UI.UnknownDate".Translate().ToString();
                string sub = dateText + " · "
                    + "PersonalChronicle.UI.BattleParticipantsN".Translate(participants).ToString()
                    + " · " + BattleDurationText(battle);
                UIComponents.Label(new Rect(x, y, w, 18f), sub, UITheme.FontLabel, ArchiveUiStyle.Muted);
                y += 20f;

                // 4) Three metric cells (force / kills / losses).
                float cellGap = 4f;
                float cellW = (w - cellGap * 2f) / 3f;
                UIComponents.Label(new Rect(x, y, cellW, 14f),
                    "PersonalChronicle.UI.BattleMetricRaid".Translate().ToString(),
                    UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(x, y + 14f, cellW, 20f),
                    battle.RaidCount > 0 ? battle.RaidCount.ToString() : "—",
                    UITheme.FontBody, UITheme.Text);
                UIComponents.Label(new Rect(x + cellW + cellGap, y, cellW, 14f),
                    "PersonalChronicle.UI.BattleMetricKills".Translate().ToString(),
                    UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(x + cellW + cellGap, y + 14f, cellW, 20f),
                    kills.ToString(), UITheme.FontBody, UITheme.Alive);
                UIComponents.Label(new Rect(x + 2f * (cellW + cellGap), y, cellW, 14f),
                    "PersonalChronicle.UI.BattleMetricLosses".Translate().ToString(),
                    UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(x + 2f * (cellW + cellGap), y + 14f, cellW, 20f),
                    losses.ToString(), UITheme.FontBody,
                    losses > 0 ? UITheme.Dead : UITheme.Text);
                y += 36f;

                // 5) Roster chips (participant names; folded when many).
                y = DrawBattleRosterChips(x, y, w, battle, service);

                // Click toggles the inline casualty expansion.
                if (Widgets.ButtonInvisible(card))
                {
                    if (expanded)
                    {
                        expandedBattles.Remove(battle.StableId);
                    }
                    else
                    {
                        expandedBattles.Add(battle.StableId);
                    }
                }

                // 6) Inline casualty expansion (kill/loss lines from the event stream).
                if (expanded)
                {
                    Rect panel = new Rect(card.x, cardTop + BattleCardHeight + 2f,
                        BattleCardWidth, 0f);
                    float ph = DrawBattleCasualtyPanel(panel, battle, service);
                    yCursor = Mathf.Max(yCursor, cardTop + BattleCardHeight + 2f + ph + gap);
                }
                else
                {
                    yCursor = Mathf.Max(yCursor, cardTop + BattleCardHeight + gap);
                }
            }
            return yCursor - startY + 14f;
        }

        /// <summary>v4.14: threat tag label (ThreatBig=大规模威胁 / ThreatSmall=小规模威胁).</summary>
        private static string BattleThreatText(string threatKey)
        {
            if (threatKey == "ThreatBig")
            {
                return "PersonalChronicle.UI.BattleTagBig".Translate().ToString();
            }
            if (threatKey == "ThreatSmall")
            {
                return "PersonalChronicle.UI.BattleTagSmall".Translate().ToString();
            }
            return string.Empty;
        }

        /// <summary>
        /// v4.14: roster chips — participant pawn labels, clickable to open the
        /// pawn detail. Collapses to "N 人参战" text when no live resolver.
        /// </summary>
        private float DrawBattleRosterChips(float x, float y, float w, BattleObject battle, IArchiveService service)
        {
            List<string> ids = battle.ParticipantIds;
            if (ids == null || ids.Count == 0)
            {
                return y;
            }
            const float chipH = 16f;
            float step = 20f;
            int shown = 0;
            float chipX = x;
            const float chipMax = 3;
            for (int i = 0; i < ids.Count && shown < chipMax; i++)
            {
                Pawn pawn = service != null ? service.GetLivePawn(ids[i]) : null;
                string label = pawn != null ? pawn.LabelShort
                    : (ids[i].Length > 10 ? ids[i].Substring(0, 10) : ids[i]);
                float chipW = Mathf.Min(w - (chipX - x), Verse.Text.CalcSize(label).x + 8f);
                if (chipW <= 20f)
                {
                    break;
                }
                UIComponents.Badge(new Rect(chipX, y, chipW, chipH), label, UITheme.BorderSoft);
                chipX += chipW + 4f;
                shown++;
            }
            if (ids.Count > shown)
            {
                string more = "PersonalChronicle.UI.BattleRosterMore".Translate(ids.Count).ToString();
                UIComponents.Badge(new Rect(chipX, y, Mathf.Min(w - (chipX - x), Verse.Text.CalcSize(more).x + 8f), chipH),
                    more, UITheme.Muted);
            }
            return y + step;
        }

        /// <summary>
        /// v4.14: inline casualty expansion — kill/loss lines derived from the
        /// battle-scoped Death events (Read Model already aggregated counts; the
        /// lines come from the same event stream via the service).
        /// </summary>
        private float DrawBattleCasualtyPanel(Rect rect, BattleObject battle, IArchiveService service)
        {
            const float rowH = 20f;
            const int maxRows = 6;
            if (service == null || battle == null)
            {
                return 0f;
            }
            IReadOnlyList<ChronicleEvent> all = service.GetAllEvents();
            List<ChronicleEvent> lines = new List<ChronicleEvent>();
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    ChronicleEvent ev = all[i];
                    if (ev == null || ev.Subjects == null || ev.TypeKey != ChronicleEventType.Death)
                    {
                        continue;
                    }
                    for (int s = 0; s < ev.Subjects.Count; s++)
                    {
                        ObjectRef sub = ev.Subjects[s];
                        if (sub != null
                            && sub.CategoryKey == ArchiveCategoryKeys.Battle
                            && sub.StableId == battle.StableId)
                        {
                            lines.Add(ev);
                            break;
                        }
                    }
                }
            }
            if (lines.Count == 0)
            {
                UIComponents.Label(new Rect(rect.x + UITheme.CardPadX, rect.y + 2f,
                    rect.width - UITheme.CardPadX * 2f, rowH),
                    "PersonalChronicle.UI.BattleNoCasualties".Translate(), UITheme.FontLabel, ArchiveUiStyle.Muted);
                return rowH + 4f;
            }
            int n = Mathf.Min(lines.Count, maxRows);
            float total = n * rowH + 4f;
            UIComponents.Card(new Rect(rect.x, rect.y, rect.width, total), UITheme.BorderSoft);
            float yy = rect.y + 2f;
            for (int i = 0; i < n; i++)
            {
                ChronicleEvent ev = lines[i];
                string date = ev.Tick > 0L
                    ? GenDate.DateReadoutStringAt(ev.Tick, UnityEngine.Vector2.zero) : "—";
                bool kill = ev.Params != null
                    && ev.Params.TryGetValue(ChronicleEventParams.CombatRole, out string role)
                    && role == ChronicleEventParams.CombatRoleKill;
                string title = kill
                    ? "PersonalChronicle.UI.BattleLineKill".Translate().ToString()
                    : "PersonalChronicle.UI.BattleLineLoss".Translate().ToString();
                UIComponents.Label(new Rect(rect.x + 10f, yy, 86f, 18f), date, UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(rect.x + 100f, yy, rect.width - 110f, 18f), title,
                    UITheme.FontBody, kill ? UITheme.Alive : UITheme.Dead);
                yy += rowH;
            }
            return total;
        }

        // ---- v4.13 location atlas overview cards ----
        private const float LocationCardWidth = 250f;
        private const float LocationCardHeight = 176f;
        /// <summary>v4.14: KPI strip row height (StatCell minimum).</summary>
        private const float LocationKpiStripHeight = 64f;

        /// <summary>
        /// v4.14: Location atlas KPI strip (8 cells) — total / player home / quest
        /// sites / faction cities / ruined + tradable / permit-required / distinct
        /// factions. Counters come from the Read-Model snapshot; this method only
        /// renders (v4.3 boundary). Uses <see cref="UIComponents.StatCell"/>.
        /// </summary>
        private float DrawLocationKpiStrip(Rect viewRect, float y, ReadModels.LocationKpisView kpi)
        {
            if (kpi == null)
            {
                return 0f;
            }
            int n = 8;
            float gap = 6f;
            float cellW = (viewRect.width - gap * (n - 1)) / n;
            string[] labels =
            {
                "PersonalChronicle.UI.LocKpiTotal".Translate().ToString(),
                "PersonalChronicle.UI.LocKpiHome".Translate().ToString(),
                "PersonalChronicle.UI.LocKpiQuest".Translate().ToString(),
                "PersonalChronicle.UI.LocKpiSettle".Translate().ToString(),
                "PersonalChronicle.UI.LocKpiRuined".Translate().ToString(),
                "PersonalChronicle.UI.LocKpiTradable".Translate().ToString(),
                "PersonalChronicle.UI.LocKpiPermit".Translate().ToString(),
                "PersonalChronicle.UI.LocKpiFactions".Translate().ToString()
            };
            int[] values = { kpi.Total, kpi.Home, kpi.Quest, kpi.Settle, kpi.Ruined,
                kpi.Tradable, kpi.Permit, kpi.Factions };
            Color[] accents =
            {
                UITheme.Text, UITheme.Accent, UITheme.Info, UITheme.Info,
                UITheme.Dead, UITheme.Alive, UITheme.Warn, UITheme.Text
            };
            for (int i = 0; i < n; i++)
            {
                Rect cell = new Rect(viewRect.x + i * (cellW + gap), y, cellW, LocationKpiStripHeight);
                // value tint via StatCell's valueColor overload.
                UIComponents.StatCell(cell, labels[i], values[i].ToString(), accents[i]);
            }
            return LocationKpiStripHeight;
        }

        /// <summary>
        /// v4.13 P1: Overview › Location atlas cards. Each card shows the five
        /// snapshot layers (identity / ownership / geography / lifecycle /
        /// commerce) through the Design System (UITheme tokens only, no raw
        /// colors). Clicking a card toggles the inline chronicle expansion
        /// (this place's event stream). The window consumes the LocationObject
        /// snapshot — no live world queries in the draw path.
        /// </summary>
        private float DrawLocationOverviewCards(Rect viewRect, float startY, List<ArchiveObject> objects, float gap, IArchiveService service)
        {
            int perRow = Mathf.Max(1, (int)((viewRect.width + gap) / (LocationCardWidth + gap)));
            float yCursor = startY;
            for (int i = 0; i < objects.Count; i++)
            {
                LocationObject loc = objects[i] as LocationObject;
                if (loc == null)
                {
                    continue;
                }
                int col = i % perRow;
                int row = i / perRow;
                float cardTop = startY + row * (LocationCardHeight + gap);
                Rect card = new Rect(
                    viewRect.x + col * (LocationCardWidth + gap),
                    cardTop,
                    LocationCardWidth, LocationCardHeight);
                bool expanded = expandedLocations.Contains(loc.StableId);

                Color accent = LocationCardAccent(loc);
                ArchiveUiStyle.DrawCard(card, accent);
                float x = card.x + UITheme.CardPadX;
                float w = card.width - UITheme.CardPadX * 2f;
                float y = card.y + UITheme.CardPadY;

                // 1) Category row: kind Pill + faction + ruined corner dot.
                string kindKey = LocationKindKey(loc);
                Color pillColor = kindKey == "player" ? UITheme.Accent
                    : kindKey == "settle" ? UITheme.Info
                    : kindKey == "quest" ? UITheme.Warn : UITheme.Muted;
                float pillW = 54f;
                UIComponents.Badge(new Rect(x, y, pillW, 16f), LocationKindText(loc), pillColor);
                UIComponents.Label(new Rect(x + pillW + 6f, y, w - pillW - 6f, 16f),
                    LocationFactionText(loc), UITheme.FontLabel, ArchiveUiStyle.Muted);
                if (loc.DeinitTick != -1L)
                {
                    UIComponents.Label(new Rect(x + w - 40f, y, 40f, 16f),
                        "PersonalChronicle.UI.LocLifeRuined".Translate().ToString(),
                        UITheme.FontLabel, UITheme.Dead);
                }
                y += 20f;

                // 2) Name.
                UIComponents.Label(new Rect(x, y, w, 20f),
                    ObjectDisplayLabel(loc), UITheme.FontBody, ArchiveUiStyle.Info);
                y += 22f;

                // 3) Sub-line: established · dwell · events (Read-Model counts).
                int evCount = loc.StableId != null && cachedLocationEventCounts != null
                    && cachedLocationEventCounts.TryGetValue(loc.StableId, out int evN) ? evN : 0;
                string sub = "PersonalChronicle.UI.LocSubLine".Translate(
                    LocationEstablishedYearText(loc), evCount).ToString();
                UIComponents.Label(new Rect(x, y, w, 18f), sub, UITheme.FontLabel, ArchiveUiStyle.Muted);
                y += 20f;

                // 4) Geography chips (single wrapped line).
                string geo = LocationGeoText(loc);
                if (!string.IsNullOrEmpty(geo))
                {
                    UIComponents.Label(new Rect(x, y, w, 18f), geo, UITheme.FontLabel, ArchiveUiStyle.Muted);
                    y += 20f;
                }

                // 5) Commerce chip.
                string trade = LocationTradeText(loc);
                if (!string.IsNullOrEmpty(trade))
                {
                    UIComponents.Label(new Rect(x, y, w, 18f), trade, UITheme.FontLabel,
                        loc.CanTrade ? UITheme.Accent : ArchiveUiStyle.Muted);
                    y += 20f;
                }

                // 6) Lifecycle three-cell row (established / status / dwell).
                float cellGap = 4f;
                float cellW = (w - cellGap * 2f) / 3f;
                string est = loc.EstablishedTick > 0L
                    ? GenDate.DateReadoutStringAt(loc.EstablishedTick, UnityEngine.Vector2.zero) : "—";
                string status = loc.DeinitTick != -1L
                    ? LocationDeinitText(loc) : "PersonalChronicle.UI.LocLifeActive".Translate().ToString();
                string dwell = loc.EstablishedTick > 0L
                    ? ReadModels.SpanText.Format(CurrentDwellTicks(loc)) : "—";
                UIComponents.Label(new Rect(x, y, cellW, 16f),
                    "PersonalChronicle.UI.LocLifeEstablished".Translate().ToString(),
                    UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(x, y + 14f, cellW, 18f), est,
                    UITheme.FontLabel, UITheme.Text);
                UIComponents.Label(new Rect(x + cellW + cellGap, y, cellW, 16f),
                    "PersonalChronicle.UI.LocLifeStatus".Translate().ToString(),
                    UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(x + cellW + cellGap, y + 14f, cellW, 18f), status,
                    UITheme.FontLabel,
                    loc.DeinitTick != -1L ? UITheme.Dead : UITheme.Alive);
                UIComponents.Label(new Rect(x + 2f * (cellW + cellGap), y, cellW, 16f),
                    "PersonalChronicle.UI.LocLifeDwell".Translate().ToString(),
                    UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(x + 2f * (cellW + cellGap), y + 14f, cellW, 18f), dwell,
                    UITheme.FontLabel, UITheme.Text);

                // Click toggles the inline chronicle expansion.
                if (Widgets.ButtonInvisible(card))
                {
                    if (expanded)
                    {
                        expandedLocations.Remove(loc.StableId);
                    }
                    else
                    {
                        expandedLocations.Add(loc.StableId);
                    }
                }

                // Inline chronicle expansion (this place's events), drawn below
                // the card as a full-width panel. Consumes the snapshot's event
                // stream (read model) — no sorting in the window.
                if (expanded)
                {
                    Rect panel = new Rect(card.x, cardTop + LocationCardHeight + 2f,
                        LocationCardWidth, 0f);
                    float ph = DrawLocationChroniclePanel(panel, loc, service);
                    yCursor = Mathf.Max(yCursor, cardTop + LocationCardHeight + 2f + ph + gap);
                }
                else
                {
                    yCursor = Mathf.Max(yCursor, cardTop + LocationCardHeight + gap);
                }
            }
            return yCursor - startY + 14f;
        }

        /// <summary>v4.14: canonical location kind key for the card (player/settle/quest/unknown).</summary>
        private static string LocationKindKey(LocationObject loc)
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

        /// <summary>v4.14: established-year short text ("5501").</summary>
        private static string LocationEstablishedYearText(LocationObject loc)
        {
            if (loc == null || loc.EstablishedTick <= 0L)
            {
                return "—";
            }
            return GenDate.Year(loc.EstablishedTick, 0f).ToString();
        }

        /// <summary>v4.14: dwell ticks (deinit or now minus established).</summary>
        private static long CurrentDwellTicks(LocationObject loc)
        {
            if (loc == null || loc.EstablishedTick <= 0L)
            {
                return -1L;
            }
            long end = loc.DeinitTick > 0L ? loc.DeinitTick
                : (Find.TickManager != null ? Find.TickManager.TicksGame : 0L);
            return end > loc.EstablishedTick ? (end - loc.EstablishedTick) : -1L;
        }

        /// <summary>Chronicle panel: this location's event stream (most recent first, capped).</summary>
        private float DrawLocationChroniclePanel(Rect rect, LocationObject loc, IArchiveService service)
        {
            const float rowH = 20f;
            const int maxRows = 6;
            if (service == null)
            {
                return 0f;
            }
            IReadOnlyList<ChronicleEvent> events = service.GetEventsFor(loc.StableId);
            // v4.14: 此地编年史升序排列（最早 → 最近），符合设计文档 v4.12
            // "编年史升序排列由 Read Model 聚合"——窗口仅消费，不重排事件流。
            List<ChronicleEvent> ordered = (events == null)
                ? new List<ChronicleEvent>()
                : events.Where(e => e != null).OrderBy(e => e.Tick).ToList();
            if (ordered.Count == 0)
            {
                UIComponents.Label(new Rect(rect.x + UITheme.CardPadX, rect.y + 2f,
                    rect.width - UITheme.CardPadX * 2f, rowH),
                    "PersonalChronicle.UI.LocNoChronicle".Translate(), UITheme.FontLabel, ArchiveUiStyle.Muted);
                return rowH + 4f;
            }
            int n = Mathf.Min(ordered.Count, maxRows);
            float total = n * rowH + 4f;
            UIComponents.Card(new Rect(rect.x, rect.y, rect.width, total), UITheme.BorderSoft);
            float yy = rect.y + 2f;
            for (int i = 0; i < n; i++)
            {
                ChronicleEvent ev = ordered[i];
                string date = ev.Tick > 0L
                    ? GenDate.DateReadoutStringAt(ev.Tick, UnityEngine.Vector2.zero)
                    : "—";
                string title = EventName(ev);
                UIComponents.Label(new Rect(rect.x + 10f, yy, 86f, 18f), date, UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(rect.x + 100f, yy, rect.width - 110f, 18f), title, UITheme.FontBody, UITheme.Text);
                yy += rowH;
            }
            return total;
        }

        /// <summary>Location card accent by lifecycle state (Design System tokens only).</summary>
        private static Color LocationCardAccent(LocationObject loc)
        {
            if (loc == null)
            {
                return UITheme.Border;
            }
            if (loc.DeinitTick != -1L)
            {
                return UITheme.Muted;
            }
            if (loc.IsPlayerHome)
            {
                return UITheme.Accent;
            }
            return UITheme.Info;
        }

        /// <summary>Kind text (data-driven; never a hardcoded defName string).</summary>
        private static string LocationKindText(LocationObject loc)
        {
            if (loc == null)
            {
                return string.Empty;
            }
            if (loc.IsPlayerHome)
            {
                return "PersonalChronicle.UI.LocKind.Player".Translate().ToString();
            }
            string defName = loc.WorldObjectDefName;
            if (!string.IsNullOrEmpty(defName)
                && defName.IndexOf("Settlement", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "PersonalChronicle.UI.LocKind.Settle".Translate().ToString();
            }
            if (!string.IsNullOrEmpty(defName)
                && (defName.IndexOf("Quest", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || defName.IndexOf("Site", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return "PersonalChronicle.UI.LocKind.Quest".Translate().ToString();
            }
            return "PersonalChronicle.UI.LocKind.Unknown".Translate().ToString();
        }

        /// <summary>Faction text (我方 / 无主 / 派系领地).</summary>
        private static string LocationFactionText(LocationObject loc)
        {
            if (loc == null)
            {
                return string.Empty;
            }
            if (loc.IsPlayerHome)
            {
                return "PersonalChronicle.UI.LocFactionPlayer".Translate().ToString();
            }
            if (string.IsNullOrEmpty(loc.FactionDefName))
            {
                return "PersonalChronicle.UI.LocFactionNone".Translate().ToString();
            }
            return "PersonalChronicle.UI.LocFactionOther".Translate().ToString();
        }

        /// <summary>Geography tag line (biome · hill · coast · pollution · temp).</summary>
        private static string LocationGeoText(LocationObject loc)
        {
            if (loc == null)
            {
                return string.Empty;
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(loc.MapDefName))
            {
                string biomeLabel = loc.MapDefName;
                BiomeDef biomeDef = DefDatabase<BiomeDef>.GetNamedSilentFail(loc.MapDefName);
                if (biomeDef != null)
                {
                    biomeLabel = biomeDef.LabelCap;
                }
                sb.Append("PersonalChronicle.UI.LocTagBiome".Translate().ToString()).Append(" · ").Append(biomeLabel);
            }
            if (!string.IsNullOrEmpty(loc.Hilliness))
            {
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(LocationHillText(loc));
            }
            if (loc.IsCoastal)
            {
                if (sb.Length > 0) sb.Append("   ");
                sb.Append("PersonalChronicle.UI.LocTagCoast".Translate().ToString());
            }
            if (loc.Pollution > 0.001f)
            {
                if (sb.Length > 0) sb.Append("   ");
                sb.Append("PersonalChronicle.UI.LocTagPolluted".Translate().ToString());
            }
            if (!float.IsNaN(loc.AvgTempC))
            {
                if (sb.Length > 0) sb.Append("   ");
                sb.Append("PersonalChronicle.UI.LocTemp".Translate((int)loc.AvgTempC).ToString());
            }
            return sb.ToString();
        }

        private static string LocationHillText(LocationObject loc)
        {
            if (loc == null || string.IsNullOrEmpty(loc.Hilliness))
            {
                return string.Empty;
            }
            switch (loc.Hilliness)
            {
                case "Flat": return "PersonalChronicle.UI.LocHillFlat".Translate().ToString();
                case "Hilly": return "PersonalChronicle.UI.LocHillHilly".Translate().ToString();
                case "Mountainous": return "PersonalChronicle.UI.LocHillMountain".Translate().ToString();
                case "Impassable": return "PersonalChronicle.UI.LocHillImpassable".Translate().ToString();
                default: return loc.Hilliness;
            }
        }

        /// <summary>Commerce line (可交易 + sell categories + permit).</summary>
        private static string LocationTradeText(LocationObject loc)
        {
            if (loc == null)
            {
                return string.Empty;
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(loc.CanTrade
                ? "PersonalChronicle.UI.TradeCan".Translate().ToString()
                : "PersonalChronicle.UI.TradeNo".Translate().ToString());
            if (loc.TradeKindKeys != null && loc.TradeKindKeys.Count > 0)
            {
                sb.Append(" · ");
                for (int i = 0; i < loc.TradeKindKeys.Count && i < 3; i++)
                {
                    if (i > 0) sb.Append(" / ");
                    sb.Append(LocationTradeCategoryText(loc.TradeKindKeys[i]));
                }
            }
            if (!string.IsNullOrEmpty(loc.PermitRequiredDefName))
            {
                sb.Append(" · ").Append("PersonalChronicle.UI.TradePermit"
                    .Translate("PersonalChronicle.UI.TradePermitName".Translate().ToString()).ToString());
            }
            return sb.ToString();
        }

        private static string LocationTradeCategoryText(string key)
        {
            switch (key)
            {
                case "res": return "PersonalChronicle.UI.TradeCat.Res".Translate().ToString();
                case "cloth": return "PersonalChronicle.UI.TradeCat.Cloth".Translate().ToString();
                case "food": return "PersonalChronicle.UI.TradeCat.Food".Translate().ToString();
                case "drug": return "PersonalChronicle.UI.TradeCat.Drug".Translate().ToString();
                case "weapon": return "PersonalChronicle.UI.TradeCat.Weapon".Translate().ToString();
                case "armor": return "PersonalChronicle.UI.TradeCat.Armor".Translate().ToString();
                case "implant": return "PersonalChronicle.UI.TradeCat.Implant".Translate().ToString();
                case "tech": return "PersonalChronicle.UI.TradeCat.Tech".Translate().ToString();
                default: return key;
            }
        }

        /// <summary>Lifecycle line (落成 tick · 状态 · 驻留).</summary>
        private static string LocationLifeText(LocationObject loc)
        {
            if (loc == null)
            {
                return string.Empty;
            }
            string est = loc.EstablishedTick > 0L
                ? "PersonalChronicle.UI.LocLifeEstablished".Translate().ToString() + " " + GenDate.DateReadoutStringAt(loc.EstablishedTick, UnityEngine.Vector2.zero)
                : "PersonalChronicle.UI.LocLifeEstablished".Translate().ToString();
            string status = loc.DeinitTick != -1L
                ? LocationDeinitText(loc)
                : "PersonalChronicle.UI.LocLifeActive".Translate().ToString();
            return est + "   " + status;
        }

        private static string LocationDeinitText(LocationObject loc)
        {
            if (loc == null || loc.DeinitTick == -1L)
            {
                return "PersonalChronicle.UI.LocLifeActive".Translate().ToString();
            }
            if (loc.DeinitReason == PlaceVisitKeys.DeinitReasonDestroyed)
            {
                return "PersonalChronicle.UI.LocDeinit.Destroyed".Translate().ToString();
            }
            if (loc.DeinitReason == PlaceVisitKeys.DeinitReasonAbandoned)
            {
                return "PersonalChronicle.UI.LocDeinit.Abandoned".Translate().ToString();
            }
            return "PersonalChronicle.UI.LocLifeRuined".Translate().ToString();
        }

        /// <summary>Repulse duration text: EndTick - StartTick, or "ongoing" while the raid is not yet repulsed. Uses the shared span formatter so short raids show hours/minutes and long ones years/quadrums.</summary>
        private static string BattleDurationText(BattleObject battle)
        {
            if (battle == null || battle.StartTick < 0L)
            {
                return "—";
            }
            if (battle.EndTick < 0L || battle.EndTick < battle.StartTick)
            {
                return "PersonalChronicle.UI.BattleOngoing".Translate().ToString();
            }
            return ReadModels.SpanText.Format(battle.EndTick - battle.StartTick);
        }

        private void DrawDetailContent(Rect inner, IArchiveService service)
        {
            if (cachedDetailObject == null)
            {
                GoOverview(null);
                return;
            }

            // Keep the object identity and tab bar fixed. Only the selected
            // child container scrolls, so every tab gets an independent safe
            // viewport instead of being clipped by a guessed total height.
            float y = inner.y + 4f;
            y = DrawObjectHeader(inner, y, service);
            y = DrawTabBar(inner, y, service);

            Rect panelRect = new Rect(
                inner.x,
                y,
                inner.width,
                Mathf.Max(60f, inner.yMax - y));
            float contentHeight = ComputeDetailPanelHeight(panelRect);
            float viewWidth = Mathf.Max(1f, panelRect.width - 16f);
            Rect viewRect = new Rect(
                panelRect.x,
                panelRect.y,
                viewWidth,
                Mathf.Max(panelRect.height, contentHeight));

            ArchiveUiStyle.DrawPanel(panelRect, ArchiveUiStyle.PanelRaised);
            Widgets.BeginScrollView(panelRect, ref detailScroll, viewRect);
            try
            {
                DrawDetailPanel(viewRect, service);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private float ComputeDetailPanelHeight(Rect panel)
        {
            string tab = CurrentTabKey();
            if (tab == "Timeline")
            {
                float h = 48f;
                for (int i = 0; i < cachedDetailEvents.Count; i++)
                {
                    EventLineView line = cachedDetailEvents[i];
                    if (line.Event == null || !ShouldShowTimelineEvent(line.Event))
                    {
                        continue;
                    }
                    h += TimelineLineHeight(line, panel.width);
                }
                return h + 20f;
            }
            if (tab == "Career")
            {
                int productionTypes = cachedProductionSummary != null && cachedProductionSummary.Types != null
                    ? cachedProductionSummary.Types.Count : 0;
                int intensityRows = cachedIntensityWorkTypes != null
                    ? (cachedIntensityWorkTypes.Count + 1) / 2 : 0;
                int productionRows = (productionTypes + 1) / 2;
                float productionSummaryHeight = productionTypes > 0 ? 86f : 0f;
                return Mathf.Max(980f,
                    280f + intensityRows * 120f + productionSummaryHeight
                    + productionRows * 58f + 16 * 26f);
            }
            if (tab == "CombatLog")
            {
                float h = 260f;
                for (int i = 0; i < cachedFactionCodex.Count; i++)
                {
                    FactionCodexView card = cachedFactionCodex[i];
                    bool expanded = expandedFactions.Contains(card.FactionKey);
                    h += FactionCodexCardHeight(card, expanded) + 12f;
                }
                return h;
            }
            if (tab == "Social")
            {
                int relationCount = cachedDetailObject is PawnObject pawn && pawn.Relations != null
                    ? pawn.Relations.Count : 0;
                int socialEvents = cachedDetailRawEvents.Count(IsSocialEvent);
                // The network panel grows with zoom and with the grid extent (outer
                // nodes must stay inside). Estimate the actual panel height using
                // the same rowSpacing math as DrawSocialNetwork so nothing is cut.
                float zoom = Mathf.Max(socialNetworkZoom, 0.4f);
                float baseNodeW = Mathf.Min(160f, Mathf.Max(110f, panel.width * 0.22f));
                float baseNodeH = 60f;
                float rowSpacing = baseNodeH * zoom
                    + Mathf.Max(baseNodeH * zoom * 0.30f, 22f * zoom);
                int maxAbsRow = relationCount <= 2 ? 1 : 2;
                float panelH = Mathf.Max(
                    246f, 246f * zoom,
                    (maxAbsRow * 2 + 1) * rowSpacing + baseNodeH * zoom + 32f);
                // v4.14: + importantRel table (header + rows) height.
                int relRows = cachedRelations != null ? cachedRelations.Count : 0;
                float relTableH = (cachedRelations != null && cachedRelations.Count > 0)
                    ? 20f + relRows * 20f + 8f : 28f;
                return panelH + 8f + relTableH + relationCount * 8f
                    + socialEvents * (TimelineRowHeight + 2f)
                    + cachedLinkedObjects.Count * 24f;
            }
            if (tab == "Legacy")
            {
                // Epithet + verdict + summary KPI + current-holder + table header +
                // (rows up to the cap, expanded when toggled) + spacing. Per-row
                // height is measured from the From/To text widths (v4.9.1: CJK date
                // strings can wrap to 2 lines), matching the adaptive logic in
                // DrawLegacyTab.
                ReadModels.LegacyView legacy = cachedLegacy ?? new ReadModels.LegacyView();
                float h = 26f; // epithet
                if (!string.IsNullOrEmpty(legacy.TitleText)) h += UITheme.FontBodyLineHeight + 8f;
                if (!string.IsNullOrEmpty(legacy.VerdictText)) h += UITheme.FontBodyLineHeight * 2f + UITheme.SpaceSm;
                if (legacy.IsEmpty) return h + 26f;
                h += UITheme.SectionTitleHeight + UIComponents.StatCellMinHeight + UITheme.SpaceSm; // summary
                h += 26f; // current holder row
                h += UITheme.SectionTitleHeight; // holders title
                // Table column widths must mirror DrawLegacyTab to keep the height
                // estimate aligned with the actual rendering. DrawLegacyTab renders
                // into viewRect (panel.width minus the 16f scrollbar), so the remark
                // column here must use the same reduced width or the estimate drifts.
                float wColGen = 46f;
                float wColHolder = 100f;
                float wColFrom = 110f;
                float wColTo = 110f;
                float wColDur = 74f;
                float wColKills = 54f;
                float wColRemark = (panel.width - 16f)
                    - (wColGen + wColHolder + wColFrom + wColTo + wColDur + wColKills);
                if (wColRemark < 80f) wColRemark = 80f;
                float baseRowH = UITheme.FontBodyLineHeight + 6f;
                int shownRows = legacy.Holders != null
                    ? Mathf.Min(legacy.Holders.Count, legacyExpanded ? int.MaxValue : 5)
                    : 0;
                for (int i = 0; i < shownRows; i++)
                {
                    ReadModels.LegacyHolderView hr = legacy.Holders[i];
                    if (hr == null) { h += baseRowH; continue; }
                    // v4.9.1: pin the font to Tiny and measure at the render width
                    // (col width minus the 4f inner padding) — same as DrawLegacyTab.
                    GameFont measurePrevFont = Verse.Text.Font;
                    Verse.Text.Font = GameFont.Tiny;
                    float fromRenderW = Mathf.Max(1f, wColFrom - 4f);
                    float toRenderW = Mathf.Max(1f, wColTo - 4f);
                    float fromH = !string.IsNullOrEmpty(hr.FromText)
                        ? Verse.Text.CalcHeight(hr.FromText, fromRenderW)
                        : baseRowH;
                    float toH = !string.IsNullOrEmpty(hr.ToText)
                        ? Verse.Text.CalcHeight(hr.ToText, toRenderW)
                        : baseRowH;
                    Verse.Text.Font = measurePrevFont;
                    float rowH = Mathf.Max(baseRowH, Mathf.Max(fromH, toH) + 6f);
                    if (rowH > baseRowH) rowH += 4f;
                    h += rowH;
                }
                if (legacy.Holders != null && legacy.Holders.Count > 5) h += 30f; // toggle
                return Mathf.Max(panel.height, h + 12f);
            }
            if (tab == "Origin")
            {
                ReadModels.ThingOriginView origin = cachedOrigin ?? new ReadModels.ThingOriginView();
                ReadModels.MakerChainView maker = cachedMakerChain ?? new ReadModels.MakerChainView();
                if (origin.IsEmpty) return Mathf.Max(panel.height, 120f);
                float h = UITheme.SectionTitleHeight; // title
                h += 30f; // kind pill
                h += UITheme.FontBodyLineHeight + 8f; // from row
                h += UITheme.FontBodyLineHeight + 8f; // where row
                if (!string.IsNullOrEmpty(origin.NoteText)) h += UITheme.FontBodyLineHeight * 2f + UITheme.SpaceSm;
                if (!maker.IsEmpty)
                {
                    h += UITheme.SectionTitleHeight + UITheme.FontBodyLineHeight + 8f;
                }
                return Mathf.Max(panel.height, h + 12f);
            }
            if (tab == "CoUse")
            {
                ReadModels.CoUseView coUse = cachedCoUse ?? new ReadModels.CoUseView();
                if (coUse.IsEmpty) return Mathf.Max(panel.height, 120f);
                float h = UITheme.SectionTitleHeight + 22f; // title + hint
                int rows = coUse.Rows != null ? coUse.Rows.Count : 0;
                h += rows * (UITheme.FontBodyLineHeight + 8f);
                return Mathf.Max(panel.height, h + 12f);
            }
            if (tab == "Decommission")
            {
                ReadModels.DecommissionView d = cachedDecommission ?? new ReadModels.DecommissionView();
                if (!d.HasRecord) return Mathf.Max(panel.height, 120f);
                float h = UITheme.SectionTitleHeight; // title
                h += 26f; // stamp
                // DrawDecommissionTab renders 5 DetailRow rows (holder / place /
                // days / battle / date); DrawDetailRow advances by 26f each. Use the
                // real row height and count all 5 so the Date row is never clipped.
                h += 26f * 5f;
                return Mathf.Max(panel.height, h + 12f);
            }
            // Overview: cover(83, +20 when a tier medal row is shown) + ledger(98) + output(≥36) + health(~250) + spacing.
            bool tierShown = cachedWorkIntensity != null && cachedWorkIntensity.IsDefined;
            float coverH = 83f + (tierShown ? 20f : 0f);
            float ledgerH = 26f + UIComponents.StatCellMinHeight + 8f;
            int typeRows = cachedProductionSummary != null && cachedProductionSummary.Types != null
                ? Mathf.Min(cachedProductionSummary.Types.Count, 5) : 0;
            if (typeRows == 0) typeRows = 1; // empty placeholder
            float outputH = 26f + typeRows * 22f + 8f;
            int evCount = cachedHealth != null && cachedHealth.Events != null
                ? Mathf.Min(cachedHealth.Events.Count, 6) : 0;
            float eventsH = evCount > 0 ? (evCount * 22f + 4f) : 18f;
            // v4.14: +34f verdict blurb under the health valuation block.
            bool verdictShown = cachedHealth != null && !string.IsNullOrEmpty(cachedHealth.VerdictText);
            float healthH = 26f + 80f + 8f + 56f + 8f + 18f + eventsH + 6f + 22f + 12f
                + (verdictShown ? 34f : 0f);
            return Mathf.Max(panel.height, coverH + 12f + ledgerH + outputH + healthH);
        }

        private static float TimelineLineHeight(EventLineView line, float width)
        {
            float descHeight = string.IsNullOrEmpty(line.DescriptionText)
                ? 0f
                : Text.CalcHeight(line.DescriptionText, Mathf.Max(120f, width - 8f)) + 4f;
            float chipsHeight = line.Chips != null && line.Chips.Count > 0 ? ChipRowHeight : 0f;
            return TimelineRowHeight + descHeight + chipsHeight;
        }

        private float DrawObjectHeader(Rect rect, float y, IArchiveService service)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, y, rect.width, 28f), ObjectDisplayLabel(cachedDetailObject));
            y += 32f;

            if (cachedDetailObject is PawnObject pawn)
            {
                PawnRole displayRole = pawn.Role;
                Pawn livePawn = service.GetLivePawn(pawn.StableId);
                if (ChronicleColonistScanner.TryClassify(livePawn, out PawnRole liveRole))
                {
                    displayRole = liveRole;
                }
                float px = DrawPill(rect.x, y, pawn.IsArchived
                        ? "PersonalChronicle.UI.Dead".Translate().ToString()
                        : "PersonalChronicle.UI.Alive".Translate().ToString(),
                    pawn.IsArchived ? DeadPill : AlivePill);
                DrawPill(px, y, RoleLabel(displayRole), RolePillColor(displayRole));
                string meta = KindLabel(pawn);
                if (!string.IsNullOrEmpty(FactionLabel(pawn)))
                {
                    meta = meta + " · " + FactionLabel(pawn);
                }
                meta = meta + " · " + "PersonalChronicle.UI.JoinDate".Translate().ToString() + " " + FormatDate(pawn.JoinTick);
                DrawMetaLine(rect, y + 30f, meta);
            }
            else if (cachedDetailObject is ThingObject thing)
            {
                DrawPill(rect.x, y, CategoryLabel(thing.CategoryKey), BluePill);
                DrawMetaLine(rect, y + 30f, ThingDefLabel(thing.ThingDefName));
            }
            else
            {
                DrawPill(rect.x, y, CategoryLabel(cachedDetailObject.CategoryKey), BluePill);
                DrawMetaLine(rect, y + 30f, ObjectSubLabel(cachedDetailObject));
            }

            Text.Font = GameFont.Small;
            return y + 54f;
        }

        private static void DrawMetaLine(Rect rect, float y, string text)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.SecondaryText;
            Widgets.Label(new Rect(rect.x, y, rect.width, 18f), text);
            GUI.color = prevColor;
            Text.Font = prevFont;
        }

        private float DrawTabBar(Rect rect, float y, IArchiveService service)
        {
            string[] tabs = cachedDetailObject is ThingObject ? WeaponTabKeys : PawnTabKeys;
            if (detailTabIndex >= tabs.Length)
            {
                detailTabIndex = 0;
            }

            // v3.1: fewer tabs (5/4) — use equal flex width, min clickable ~72.
            float tabWidth = Mathf.Max(72f, (rect.width - 6f) / tabs.Length - 2f);
            const float tabHeight = 28f;
            const float tabGap = 2f;

            Text.Font = GameFont.Small;
            for (int i = 0; i < tabs.Length; i++)
            {
                Rect tab = new Rect(rect.x + i * (tabWidth + tabGap), y, tabWidth, tabHeight);
                bool selected = i == detailTabIndex;
                ArchiveUiStyle.DrawSelectedNavigation(tab, selected);
                string label = TabLabel(tabs[i]);
                UIComponents.Label(tab, label, GameFont.Small,
                    selected ? UITheme.Text : UITheme.Muted, TextAnchor.MiddleCenter);
                if (selected)
                {
                    UIComponents.Rule(new Rect(tab.x, tab.yMax - 2f, tab.width, 2f), UITheme.Accent);
                }
                if (Widgets.ButtonInvisible(tab))
                {
                    detailTabIndex = i;
                    detailScroll = Vector2.zero;
                    socialNetworkZoom = 1f;
                    socialNetworkZoomTouched = false;
                    socialNetworkPan = Vector2.zero;
                }
            }

            ArchiveUiStyle.DrawRule(new Rect(rect.x, y + tabHeight + 2f, rect.width, 1f), ArchiveUiStyle.Border);
            return y + tabHeight + 8f;
        }

        private void DrawDetailPanel(Rect panel, IArchiveService service)
        {
            string[] tabs = cachedDetailObject is ThingObject ? WeaponTabKeys : PawnTabKeys;
            string tab = detailTabIndex >= 0 && detailTabIndex < tabs.Length ? tabs[detailTabIndex] : "Overview";
            bool isWeapon = cachedDetailObject is ThingObject;

            switch (tab)
            {
                case "Overview":
                    if (isWeapon)
                    {
                        DrawWeaponOverview(panel, service);
                    }
                    else
                    {
                        DrawPawnOverview(panel, service);
                    }
                    break;
                case "Timeline":
                    DrawDetailTimeline(panel, service);
                    break;
                case "Career":
                    DrawCareerTab(panel, service);
                    break;
                case "CombatLog":
                    if (isWeapon)
                    {
                        DrawWeaponCombat(panel, service);
                    }
                    else
                    {
                        DrawPawnCombat(panel, service);
                    }
                    break;
                case "Social":
                    DrawSocialTab(panel, service);
                    break;
                case "Legacy":
                    DrawLegacyTab(panel, service);
                    break;
                case "Origin":
                    DrawOriginTab(panel, service);
                    break;
                case "CoUse":
                    DrawCoUseTab(panel, service);
                    break;
                case "Decommission":
                    DrawDecommissionTab(panel, service);
                    break;
                default:
                    DrawCapturePlaceholder(panel);
                    break;
            }
        }

        // ---- Detail: pawn tabs ------------------------------------------------

        /// <summary>
        /// v3.1 archive cover: KPI strip + lifecycle + career blurb + footprint + key events.
        /// Stats tab content is absorbed here (no separate Stats button).
        /// </summary>
        /// <summary>
        /// v4.4 Pawn Overview (content-reset, layout B two-column on wide screens).
        /// Consumes only the read-model derived views (cachedLifePhases / cachedCareerBars
        /// / cachedFootprint / cachedMilestones / cachedKeyEvents). No raw query/sort here.
        /// </summary>
        private void DrawOrthogonalLink(Vector2 center, Vector2 nodeCenter, Color color, float thickness)
        {
            ArchivePanelBase.DrawOrthogonalLink(center, nodeCenter, color, thickness, socialNetworkZoom);
        }

        private static string RelationDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return "—";
            }
            // Opinion-derived ties are synthesized by the archive and have no
            // backing PawnRelationDef; without this the raw key would be shown.
            if (defName == SocialRelationFilter.FriendRelationKey)
            {
                return "PersonalChronicle.Relation.Friend".Translate().ToString();
            }
            if (defName == SocialRelationFilter.RivalRelationKey)
            {
                return "PersonalChronicle.Relation.Rival".Translate().ToString();
            }
            PawnRelationDef def = DefDatabase<PawnRelationDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            return defName;
        }

        /// <summary>
        /// Display priority for the social network graph: partners and blood kin
        /// outrank opinion-derived ties, and ended ties sink last. Needed because
        /// the node list is capped at 8 — without ranking, the capture order
        /// (direct → implied → opinion) would let acquaintances crowd out family.
        /// </summary>
        private static int SocialNodeRank(SignificantRelation relation)
        {
            if (relation == null)
            {
                return 99;
            }
            if (!relation.IsActive)
            {
                return 50;
            }
            string defName = relation.RelationDefName;
            if (defName == SocialRelationFilter.FriendRelationKey)
            {
                return 20;
            }
            if (defName == SocialRelationFilter.RivalRelationKey)
            {
                return 30;
            }
            return 0;
        }

        private string FormatSocialEventTitle(string action, string relationDefName, ChronicleEvent ev, IArchiveService service)
        {
            string relLabel = RelationDefLabel(relationDefName);
            string other = string.Empty;
            if (ev != null && ev.Subjects != null)
            {
                for (int i = 0; i < ev.Subjects.Count; i++)
                {
                    ObjectRef s = ev.Subjects[i];
                    if (s != null && s.CategoryKey == ArchiveCategoryKeys.Pawn)
                    {
                        other = ResolveRefLabel(s, service);
                        break;
                    }
                }
            }
            if (action == ChronicleEventParams.RelationActionEnded)
            {
                return "PersonalChronicle.UI.SocialEndedTitle".Translate(relLabel, other).ToString();
            }
            return "PersonalChronicle.UI.SocialFormedTitle".Translate(relLabel, other).ToString();
        }

        private void DrawPlacesTab(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.PlacesCurrent".Translate().ToString());

            LocationInfo info = service.GetLiveLocation(detailObjectId);
            if (info == null || info.Kind == LocationKind.None)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoLocationData".Translate().ToString());
                Text.Font = GameFont.Tiny;
                GUI.color = UITheme.SecondaryText;
                Widgets.Label(new Rect(rect.x, y + 26f, rect.width, 40f),
                    "PersonalChronicle.UI.NoLocationExplanation".Translate().ToString());
                GUI.color = prevColor;
                return;
            }

            if (info.Kind == LocationKind.Map)
            {
                string place = string.IsNullOrEmpty(info.MapDefName)
                    ? "PersonalChronicle.UI.UnknownPlace".Translate().ToString()
                    : BiomeLabel(info.MapDefName);
                y = DrawDetailRow(rect.x, y, rect.width,
                    "PersonalChronicle.UI.PlacesMap".Translate().ToString(), place);
            }
            else if (info.Kind == LocationKind.Caravan)
            {
                y = DrawDetailRow(rect.x, y, rect.width,
                    "PersonalChronicle.UI.PlacesCaravan".Translate().ToString(),
                    "PersonalChronicle.UI.PlacesWorldTile".Translate(info.WorldTile).ToString());
            }
        }

        // v4.7 Legacy (传承) tab — the ownership-transfer chain for equipment.
        // Consumes the read-model snapshot (cachedLegacy) only: no queries, no
        // sorting, no null-guards here. Layout: epithet → verdict → summary KPI →
        // holder table (collapsible past a small cap). All drawing via UIComponents.
        private void DrawLegacyTab(Rect rect, IArchiveService service)
        {
            ReadModels.LegacyView legacy = cachedLegacy ?? new ReadModels.LegacyView();
            float y = rect.y;
            float w = rect.width;

            // 传承称号 (epithet).
            if (!string.IsNullOrEmpty(legacy.TitleText))
            {
                UIComponents.Label(
                    new Rect(rect.x, y, w, UITheme.FontBodyLineHeight + 4f),
                    legacy.TitleText, GameFont.Small, UITheme.Accent);
                y += UITheme.FontBodyLineHeight + 8f;
            }

            // 传承评价 (verdict).
            if (!string.IsNullOrEmpty(legacy.VerdictText))
            {
                UIComponents.Label(
                    new Rect(rect.x, y, w, UITheme.FontBodyLineHeight * 2f),
                    legacy.VerdictText, GameFont.Small, UITheme.Text);
                y += UITheme.FontBodyLineHeight * 2f + UITheme.SpaceSm;
            }

            if (legacy.IsEmpty)
            {
                UIComponents.Label(
                    new Rect(rect.x, y, w, 22f),
                    "PersonalChronicle.UI.Legacy.Empty".Translate().ToString(),
                    GameFont.Small, UITheme.Muted);
                return;
            }

            // 传承概要 (summary KPI).
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Legacy.Summary".Translate().ToString());
            float cellW = (w - UITheme.SpaceXs * 3f) / 4f;
            float cellH = UIComponents.StatCellMinHeight;
            Rect c1 = new Rect(rect.x, y, cellW, cellH);
            Rect c2 = new Rect(c1.xMax + UITheme.SpaceXs, y, cellW, cellH);
            Rect c3 = new Rect(c2.xMax + UITheme.SpaceXs, y, cellW, cellH);
            Rect c4 = new Rect(c3.xMax + UITheme.SpaceXs, y, cellW, cellH);
            UIComponents.StatCell(c1, "PersonalChronicle.UI.Legacy.CreatedBy".Translate().ToString(),
                legacy.CreatedByText ?? "—");
            UIComponents.StatCell(c2, "PersonalChronicle.UI.Legacy.CreatedAt".Translate().ToString(),
                legacy.CreatedAtText ?? "—");
            UIComponents.StatCell(c3, "PersonalChronicle.UI.Legacy.Gen".Translate().ToString(),
                legacy.GenCount.ToString(),
                "PersonalChronicle.UI.Legacy.GenNote".Translate().ToString());
            UIComponents.StatCell(c4, "PersonalChronicle.UI.Legacy.TotalKills".Translate().ToString(),
                legacy.TotalKills.ToString());
            y += cellH + UITheme.SpaceSm;

            // Current holder line.
            y = DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Legacy.CurrentHolder".Translate().ToString(),
                legacy.CurrentHolderText ?? "—");

            // 历届持有者 (holder table), collapsible past a cap.
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Legacy.Holders".Translate().ToString());
            if (legacy.Holders == null || legacy.Holders.Count == 0)
            {
                UIComponents.Label(new Rect(rect.x, y, w, 22f),
                    "PersonalChronicle.UI.Legacy.NoHolders".Translate().ToString(),
                    GameFont.Small, UITheme.Muted);
                return;
            }

            const int cap = 5;
            bool expanded = legacyExpanded;
            IReadOnlyList<ReadModels.LegacyHolderView> shown = expanded
                ? legacy.Holders
                : SubList(legacy.Holders, cap);

            // Header row.
            // v4.9.1: rebalanced widths so 2-line CJK date strings ("5500年 翠象 翠1天")
            // have room to wrap instead of being clipped. Wider From/To (110f), Dur
            // (74f), Kills (54f); remark narrows to consume the rest. Floor at 80f so
            // very narrow layouts still keep a usable remark column.
            float colGen = 46f;
            float colHolder = 100f;
            float colFrom = 110f;
            float colTo = 110f;
            float colDur = 74f;
            float colKills = 54f;
            float colRemark = w - (colGen + colHolder + colFrom + colTo + colDur + colKills);
            if (colRemark < 80f) colRemark = 80f;
            string[] headerKeys =
            {
                "PersonalChronicle.UI.Legacy.ColGen",
                "PersonalChronicle.UI.Legacy.ColHolder",
                "PersonalChronicle.UI.Legacy.ColFrom",
                "PersonalChronicle.UI.Legacy.ColTo",
                "PersonalChronicle.UI.Legacy.ColDur",
                "PersonalChronicle.UI.Legacy.ColKills",
                "PersonalChronicle.UI.Legacy.ColRemark"
            };
            float[] colW = { colGen, colHolder, colFrom, colTo, colDur, colKills, colRemark };
            float x = rect.x;
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            for (int i = 0; i < headerKeys.Length; i++)
            {
                UIComponents.Label(new Rect(x, y, colW[i], UITheme.FontBodyLineHeight),
                    headerKeys[i].Translate().ToString(), GameFont.Tiny, UITheme.Muted);
                x += colW[i];
            }
            y += UITheme.FontBodyLineHeight + 2f;
            UIComponents.Rule(new Rect(rect.x, y, w, 1f), UITheme.BorderSoft);
            y += 3f;

            // Data rows.
            for (int i = 0; i < shown.Count; i++)
            {
                ReadModels.LegacyHolderView h = shown[i];
                if (h == null) continue;
                // v4.9.1: row height must accommodate 2-line CJK From/To text. Use the
                // largest of (single-line baseline, measured From/To height) so short
                // rows stay compact while long dates get full room without clipping.
                float singleLineRowH = UITheme.FontBodyLineHeight + 6f;
                // v4.9.1: CalcHeight is relative to the CURRENT Text.Font, but the
                // From/To cells render in GameFont.Tiny. Pin the font to Tiny for the
                // measurement (and restore) so multi-line height matches the actual
                // glyph box instead of drifting with the caller's font state.
                GameFont measurePrevFont = Verse.Text.Font;
                Verse.Text.Font = GameFont.Tiny;
                // Measure at the ACTUAL render width (col width minus the 4f inner
                // padding the label rects use), otherwise a wider measurement width
                // under-reports wrap lines and the row can clip the last line.
                float fromRenderW = Mathf.Max(1f, colFrom - 4f);
                float toRenderW = Mathf.Max(1f, colTo - 4f);
                float fromH = !string.IsNullOrEmpty(h.FromText)
                    ? Verse.Text.CalcHeight(h.FromText, fromRenderW)
                    : singleLineRowH;
                float toH = !string.IsNullOrEmpty(h.ToText)
                    ? Verse.Text.CalcHeight(h.ToText, toRenderW)
                    : singleLineRowH;
                Verse.Text.Font = measurePrevFont;
                float rowH = Mathf.Max(singleLineRowH, Mathf.Max(fromH, toH) + 6f);
                // Plus a small bottom inset so two-line rows breathe.
                if (rowH > singleLineRowH) rowH += 4f;
                Rect row = new Rect(rect.x, y, w, rowH);

                if (h.IsCurrent)
                {
                    // v4.9.1: Accent (NameColor) at 0.18 alpha — the global AccentSoft
                    // token sits at 0.15 which fades on dark panels; bumping this row's
                    // tint slightly keeps the current holder unmistakably highlighted
                    // without leaving the industrial-terminal palette. Rendered via the
                    // TintedBox component so the window never hand-edits GUI.color.
                    UIComponents.TintedBox(row, UITheme.WithAlpha(UITheme.Accent, 0.18f));
                }

                // Generation cell: loan rows show the loan badge instead of a gen.
                if (h.IsLoan)
                {
                    UIComponents.Badge(
                        new Rect(rect.x, y + 2f, 42f, UITheme.FontBodyLineHeight - 2f),
                        "PersonalChronicle.UI.Legacy.Loan".Translate().ToString(),
                        UITheme.Dim);
                }
                else
                {
                    UIComponents.Label(new Rect(rect.x + 2f, y, colGen - 2f, rowH),
                        h.Generation > 0
                            ? "PersonalChronicle.UI.Legacy.GenN".Translate(h.Generation).ToString()
                            : "—",
                        GameFont.Small, UITheme.Text);
                }

                // Holder: label + first badge. Inner padding 4f gives the badge
                // breathing room from the holder text (was -6f touching the badge).
                UIComponents.Label(new Rect(rect.x + colGen + 4f, y, colHolder - 4f, rowH),
                    h.HolderText ?? "—", GameFont.Small, UITheme.Text);
                if (h.IsFirst)
                {
                    UIComponents.Badge(
                        new Rect(rect.x + colGen + colHolder - 62f, y + 2f, 56f, UITheme.FontBodyLineHeight - 2f),
                        "PersonalChronicle.UI.Legacy.First".Translate().ToString(),
                        UITheme.Accent);
                }

                // From / To / Duration / Kills. Inner padding 4f so CJK date strings
                // don't touch the previous column's edge.
                UIComponents.Label(new Rect(rect.x + colGen + colHolder + 4f, y, colFrom - 4f, rowH),
                    h.FromText ?? "—", GameFont.Tiny, UITheme.Muted);
                UIComponents.Label(new Rect(rect.x + colGen + colHolder + colFrom + 4f, y, colTo - 4f, rowH),
                    h.ToText ?? "—", GameFont.Tiny, UITheme.Muted);
                UIComponents.Label(new Rect(rect.x + colGen + colHolder + colFrom + colTo + 4f, y, colDur - 4f, rowH),
                    h.DurationText ?? "—", GameFont.Tiny, UITheme.Muted);
                // Kills cell: non-zero kills rendered in Alive green at Medium size
                // so the data point stands out; zero stays in Muted Small to avoid
                // visual noise. Row height must accommodate the Medium line height
                // (~28f empirical) so the digit isn't clipped.
                GameFont killFont = h.KillCount > 0 ? GameFont.Medium : GameFont.Small;
                Color killColor = h.KillCount > 0 ? UITheme.Alive : UITheme.Muted;
                if (killFont == GameFont.Medium && rowH < 28f + 4f) rowH = 28f + 4f;
                UIComponents.Label(new Rect(rect.x + colGen + colHolder + colFrom + colTo + colDur + 4f, y, colKills - 4f, rowH),
                    h.KillCount.ToString(), killFont, killColor);

                // Remark (loan note) — fits in the remaining column with 4f right padding.
                UIComponents.Label(new Rect(rect.x + colGen + colHolder + colFrom + colTo + colDur + colKills + 4f,
                    y, colRemark - 8f, rowH),
                    h.RemarkText ?? "—", GameFont.Tiny, UITheme.Dim);

                // Click to navigate to the holder pawn.
                if (!string.IsNullOrEmpty(h.HolderStableId) && Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, NavTarget.Pawn, h.HolderStableId, null);
                }
                UIComponents.Rule(new Rect(rect.x, row.yMax - 1f, w, 1f), UITheme.BorderHair);
                y += rowH;
            }
            GUI.color = prevColor;
            Verse.Text.Font = prevFont;

            // Collapse / expand toggle.
            if (legacy.Holders.Count > cap)
            {
                y += UITheme.SpaceXxs;
                string toggle = expanded
                    ? "PersonalChronicle.UI.Legacy.Collapse".Translate().ToString()
                    : "PersonalChronicle.UI.Legacy.ExpandAll".Translate(legacy.Holders.Count).ToString();
                Rect btn = new Rect(rect.x, y, w, 24f);
                if (Widgets.ButtonText(btn, toggle, true, false, true))
                {
                    legacyExpanded = !legacyExpanded;
                }
                y += 30f;
            }
        }

        // ---- v4.9 equipment legacy extension tabs (溯源 / 同袍 / 退役) ----

        /// <summary>
        /// 溯源 (Origin): where the thing came from + the maker-chain double
        /// narrative. Consumes only read-model derived views; no raw queries here.
        /// </summary>
        private void DrawOriginTab(Rect rect, IArchiveService service)
        {
            ReadModels.ThingOriginView origin = cachedOrigin ?? new ReadModels.ThingOriginView();
            ReadModels.MakerChainView maker = cachedMakerChain ?? new ReadModels.MakerChainView();
            float y = rect.y;
            float w = rect.width;

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Origin.Title".Translate().ToString());
            if (origin.IsEmpty)
            {
                UIComponents.Label(new Rect(rect.x, y, w, 22f),
                    "PersonalChronicle.UI.Origin.Empty".Translate().ToString(),
                    GameFont.Small, UITheme.Muted);
                return;
            }

            // Kind pill (来源 chip).
            UIComponents.Pill(new Rect(rect.x, y, 120f, 22f),
                origin.KindText ?? "—", UITheme.Accent);
            y += 30f;

            // From / Where rows.
            if (!string.IsNullOrEmpty(origin.FromStableId))
            {
                Rect row = new Rect(rect.x, y, w, UITheme.FontBodyLineHeight + 4f);
                DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Origin.From".Translate().ToString(),
                    origin.FromText ?? "—");
                if (Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, NavTarget.Pawn, origin.FromStableId, null);
                }
                y += UITheme.FontBodyLineHeight + 8f;
            }
            else
            {
                y = DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Origin.From".Translate().ToString(),
                    origin.FromText ?? "—");
            }
            if (!string.IsNullOrEmpty(origin.WhereText))
            {
                y = DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Origin.Where".Translate().ToString(),
                    origin.WhereText);
            }
            if (!string.IsNullOrEmpty(origin.NoteText))
            {
                y += UITheme.SpaceXs;
                UIComponents.Label(new Rect(rect.x, y, w, UITheme.FontBodyLineHeight * 2f),
                    origin.NoteText, GameFont.Small, UITheme.Muted);
                y += UITheme.FontBodyLineHeight * 2f + UITheme.SpaceSm;
            }

            // 工坊署名链 (maker chain): the crafter's later fate.
            if (!maker.IsEmpty)
            {
                DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Origin.MakerChain".Translate().ToString());
                Rect row = new Rect(rect.x, y, w, UITheme.FontBodyLineHeight + 4f);
                string makerText = maker.MakerText ?? "—";
                UIComponents.Label(new Rect(rect.x, y, w - 130f, UITheme.FontBodyLineHeight),
                    makerText, GameFont.Small, UITheme.Text);
                if (maker.MakerDiedByOwn)
                {
                    UIComponents.Badge(
                        new Rect(rect.x + w - 126f, y, 122f, UITheme.FontBodyLineHeight),
                        "PersonalChronicle.UI.Origin.MakerDiedByOwn".Translate().ToString(),
                        UITheme.Threat);
                }
                if (!string.IsNullOrEmpty(maker.MakerStableId) && Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, NavTarget.Pawn, maker.MakerStableId, null);
                }
                y += UITheme.FontBodyLineHeight + 10f;
            }
        }

        /// <summary>
        /// 同袍共用网络 (CoUse): colonists who used this equipment in parallel with
        /// the current holder, ranked by shared tenure with a share bar.
        /// </summary>
        private void DrawCoUseTab(Rect rect, IArchiveService service)
        {
            ReadModels.CoUseView coUse = cachedCoUse ?? new ReadModels.CoUseView();
            float y = rect.y;
            float w = rect.width;

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.CoUse.Title".Translate().ToString());
            UIComponents.Label(new Rect(rect.x, y, w, UITheme.FontBodyLineHeight),
                "PersonalChronicle.UI.CoUse.Hint".Translate().ToString(),
                GameFont.Tiny, UITheme.Muted);
            y += UITheme.FontBodyLineHeight + 6f;

            if (coUse.IsEmpty || coUse.Rows == null || coUse.Rows.Count == 0)
            {
                UIComponents.Label(new Rect(rect.x, y, w, 22f),
                    "PersonalChronicle.UI.CoUse.Empty".Translate().ToString(),
                    GameFont.Small, UITheme.Muted);
                return;
            }

            float colName = 140f;
            float colDays = 60f;
            float colBar = w - colName - colDays - 16f;
            for (int i = 0; i < coUse.Rows.Count; i++)
            {
                ReadModels.CoUseRowView rowView = coUse.Rows[i];
                if (rowView == null) continue;
                float rowH = UITheme.FontBodyLineHeight + 4f;
                Rect row = new Rect(rect.x, y, w, rowH);
                UIComponents.Label(new Rect(rect.x, y, colName, rowH),
                    rowView.PawnText ?? "—", GameFont.Small, UITheme.Text);
                UIComponents.Label(new Rect(rect.x + colName, y, colDays, rowH),
                    rowView.SharedDays.ToString(), GameFont.Tiny, UITheme.Muted);
                Rect bar = new Rect(rect.x + colName + colDays + 8f, y + 4f, colBar, 10f);
                UIComponents.ProgressBar(bar, Mathf.Clamp01(rowView.SharePercent / 100f), UITheme.Accent);
                if (!string.IsNullOrEmpty(rowView.PawnStableId) && Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, NavTarget.Pawn, rowView.PawnStableId, null);
                }
                UIComponents.Rule(new Rect(rect.x, row.yMax, w, 1f), UITheme.BorderHair);
                y += rowH + 4f;
            }
        }

        /// <summary>
        /// 退役仪式 (Decommission): the thing's death record — last holder, last
        /// place, service days, final battle, retire date.
        /// </summary>
        private void DrawDecommissionTab(Rect rect, IArchiveService service)
        {
            ReadModels.DecommissionView d = cachedDecommission ?? new ReadModels.DecommissionView();
            float y = rect.y;
            float w = rect.width;

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Decommission.Title".Translate().ToString());
            if (!d.HasRecord)
            {
                UIComponents.Label(new Rect(rect.x, y, w, 22f),
                    "PersonalChronicle.UI.Decommission.Empty".Translate().ToString(),
                    GameFont.Small, UITheme.Muted);
                return;
            }

            // Retire stamp.
            UIComponents.Pill(new Rect(rect.x, y, 120f, 22f),
                "PersonalChronicle.UI.Decommission.Stamp".Translate().ToString(), UITheme.Dim);
            y += 30f;

            Rect holderRow = new Rect(rect.x, y, w, UITheme.FontBodyLineHeight + 4f);
            y = DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Decommission.LastHolder".Translate().ToString(),
                d.LastHolderText ?? "—");
            if (!string.IsNullOrEmpty(d.LastHolderStableId) && Widgets.ButtonInvisible(holderRow))
            {
                NavigateTarget(service, NavTarget.Pawn, d.LastHolderStableId, null);
            }
            y = DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Decommission.Place".Translate().ToString(),
                d.LastPlaceText ?? "—");
            y = DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Decommission.ServiceDays".Translate().ToString(),
                d.ServiceDays > 0
                    ? d.ServiceDays + " " + "PersonalChronicle.UI.DaysUnit".Translate().ToString()
                    : "—");
            y = DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Decommission.LastBattle".Translate().ToString(),
                d.LastBattleText ?? "—");
            y = DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Decommission.Date".Translate().ToString(),
                d.DateText ?? "—");
        }

        private static IReadOnlyList<ReadModels.LegacyHolderView> SubList(
            IReadOnlyList<ReadModels.LegacyHolderView> list, int cap)
        {
            if (list == null || list.Count <= cap) return list;
            List<ReadModels.LegacyHolderView> sub = new List<ReadModels.LegacyHolderView>(cap);
            for (int i = 0; i < cap; i++) sub.Add(list[i]);
            return sub;
        }

        private static string WorkTypeLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            WorkTypeDef def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            if (def == null)
            {
                LogMissingDefOnce(defName);
            }
            return defName;
        }

        private static string BiomeLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            BiomeDef def = DefDatabase<BiomeDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            if (def == null)
            {
                LogMissingDefOnce(defName);
            }
            return defName;
        }

        // ---- Detail: placeholder tabs -----------------------------------------

        private void DrawCapturePlaceholder(Rect rect)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                float y = rect.y;
                string featureName = TabLabel(CurrentTabKey());

                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(rect.x, y, rect.width, 28f), featureName);
                y += 36f;

                Rect box = new Rect(rect.x, y, rect.width, 110f);
                UIComponents.TintedBox(box, UITheme.OverlayWhite04);
                DrawBorder(box, UITheme.BorderSoft);

                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(box.x + 14f, box.y + 14f, box.width - 28f, 24f),
                    "PersonalChronicle.UI.NoCaptureYet".Translate().ToString());
                Text.Font = GameFont.Tiny;
                GUI.color = UITheme.SecondaryText;
                Widgets.Label(new Rect(box.x + 14f, box.y + 46f, box.width - 28f, 48f),
                    "PersonalChronicle.UI.NoCaptureExplanation".Translate().ToString());
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        private static void DrawNoService(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, inRect.height / 2f - 14f, inRect.width, 28f),
                "PersonalChronicle.UI.NoService".Translate().ToString());
        }

        private static void DrawSectionTitle(Rect rect, ref float y, string title)
        {
            UIComponents.SectionTitle(rect, y, title);
            y += UITheme.SectionTitleHeight;
        }

        private static void DrawEventRow(Rect row, string dateText, string titleText, string typeText)
        {
            Color previous = GUI.color;
            GameFont prevFont = Text.Font;
            UIComponents.TintedBox(
                new Rect(row.x, row.y + 1f, 2f, Mathf.Max(1f, row.height - 2f)),
                UITheme.InfoSoft);

            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Dim;
            Widgets.Label(new Rect(row.x + 8f, row.y + 4f, 146f, 18f), dateText);

            Text.Font = GameFont.Small;
            GUI.color = ArchiveUiStyle.Text;
            Widgets.Label(new Rect(row.x + 162f, row.y + 4f, row.width - 162f - 190f, 20f), titleText);

            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Muted;
            Widgets.Label(new Rect(row.x + row.width - 186f, row.y + 4f, 182f, 18f), typeText);
            GUI.color = previous;
            Text.Font = prevFont;

            ArchiveUiStyle.DrawRule(new Rect(row.x, row.yMax - 1f, row.width, 1f), ArchiveUiStyle.BorderSoft);
        }

        private static float DrawDetailRow(float x, float y, float width, string label, string value)
        {
            UIComponents.Label(new Rect(x, y, 150f, 20f), label, GameFont.Tiny, UITheme.SecondaryText);
            UIComponents.Label(new Rect(x + 156f, y, width - 156f, 22f), value, GameFont.Small, UITheme.Text);
            return y + 26f;
        }

        private static void DrawStatCell(Rect rect, string label, int value)
        {
            UIComponents.StatCell(rect, label, value.ToString());
        }

        /// <summary>
        /// Stat cell with a Tiny-font breakdown line under the value. Forwards to
        /// the shared UIComponents.StatCell so all KPI cells share one renderer.
        /// </summary>
        private static void DrawStatCell(Rect rect, string label, int value, string subLabel)
        {
            UIComponents.StatCell(rect, label, value.ToString(), subLabel);
        }

        private static readonly Color AlivePill = UITheme.PillGreen;
        private static readonly Color DeadPill = UITheme.Dead;
        private static readonly Color BluePill = UITheme.PillBlue;

        private static float DrawPill(float x, float y, string label, Color color)
        {
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            float width = Text.CalcSize(label).x + 18f;
            Text.Font = prevFont;
            Rect rect = new Rect(x, y, width, 20f);
            ArchiveUiStyle.DrawBadge(rect, label, color);
            return x + width + 6f;
        }

        private static void DrawBorder(Rect rect, Color color)
        {
            ArchiveUiStyle.DrawBorder(rect, color);
        }

        private static string TabLabel(string tabKey)
        {
            return ("PersonalChronicle.UI.Tab." + tabKey).Translate().ToString();
        }

        // ---- Display helpers ---------------------------------------------------

        private static readonly string[] CategoryKeys =
        {
            ArchiveCategoryKeys.Pawn,
            ArchiveCategoryKeys.Thing,
            ArchiveCategoryKeys.Battle,
            ArchiveCategoryKeys.Location
        };

        private static string CategoryLabel(string categoryKey)
        {
            ArchiveCategoryDef def = DefDatabase<ArchiveCategoryDef>.AllDefs
                .FirstOrDefault(d => d.categoryKey == categoryKey);
            if (def != null && !def.label.NullOrEmpty())
            {
                return def.label;
            }
            return ("PersonalChronicle.UI.Category." + categoryKey).Translate().ToString();
        }

        private static string ObjectDisplayLabel(ArchiveObject obj)
        {
            if (obj == null)
            {
                return string.Empty;
            }
            if (obj is PawnObject pawn)
            {
                return string.IsNullOrEmpty(pawn.LabelShort) ? pawn.StableId : pawn.LabelShort;
            }
            if (obj is ThingObject thing)
            {
                return ThingDefLabel(thing.ThingDefName);
            }
            if (obj is BattleObject battle)
            {
                return IncidentDefLabel(battle.IncidentDefName);
            }
            if (obj is LocationObject location)
            {
                if (!string.IsNullOrEmpty(location.CellLabel))
                {
                    return location.CellLabel;
                }
                if (!string.IsNullOrEmpty(location.LabelSnapshot))
                {
                    return location.LabelSnapshot;
                }
                return location.StableId;
            }
            return !string.IsNullOrEmpty(obj.LabelSnapshot) ? obj.LabelSnapshot : obj.StableId;
        }

        private static string ObjectSubLabel(ArchiveObject obj)
        {
            if (obj is PawnObject pawn)
            {
                string life = pawn.IsArchived
                    ? "PersonalChronicle.UI.Dead".Translate().ToString()
                    : "PersonalChronicle.UI.Alive".Translate().ToString();
                return life + " · " + RoleLabel(pawn.Role);
            }
            if (obj is LocationObject location)
            {
                return location.MapId;
            }
            return string.Empty;
        }

        /// <summary>角色本地化标签（自由殖民者 / 奴隶 / 囚犯）。</summary>
        private static string RoleLabel(PawnRole role)
        {
            switch (role)
            {
                case PawnRole.Slave:
                    return "PersonalChronicle.UI.RoleSlave".Translate().ToString();
                case PawnRole.Prisoner:
                    return "PersonalChronicle.UI.RolePrisoner".Translate().ToString();
                default:
                    return "PersonalChronicle.UI.RoleFreeColonist".Translate().ToString();
            }
        }

        /// <summary>角色徽标颜色：自由=绿 / 奴隶=琥珀 / 囚犯=砖红。</summary>
        private static Color RolePillColor(PawnRole role)
        {
            switch (role)
            {
                case PawnRole.Slave:
                    return UITheme.PillGold;
                case PawnRole.Prisoner:
                    return UITheme.PillRed;
                default:
                    return AlivePill;
            }
        }

        private static Color ArchiveCardAccent(ArchiveObject obj)
        {
            PawnObject pawn = obj as PawnObject;
            if (pawn != null)
            {
                return RolePillColor(pawn.Role);
            }
            return obj != null && obj.CategoryKey == ArchiveCategoryKeys.Pawn
                ? ArchiveUiStyle.Info
                : ArchiveUiStyle.Accent;
        }

        private static string ResolveRefLabel(ObjectRef r, IArchiveService service)
        {
            if (r == null)
            {
                return string.Empty;
            }
            if (!string.IsNullOrEmpty(r.LabelSnapshot))
            {
                return r.LabelSnapshot;
            }
            if (service != null && !string.IsNullOrEmpty(r.StableId))
            {
                ArchiveObject o = service.GetObject(r.StableId);
                if (o != null)
                {
                    return ObjectDisplayLabel(o);
                }
            }
            return r.StableId ?? string.Empty;
        }

        private static NavTarget NavTargetOfCategory(string categoryKey)
        {
            switch (categoryKey)
            {
                case ArchiveCategoryKeys.Pawn:
                    return NavTarget.Pawn;
                case ArchiveCategoryKeys.Thing:
                    return NavTarget.Weapon;
                default:
                    return NavTarget.None;
            }
        }

        private static string EventName(ChronicleEvent ev)
        {
            if (ev == null)
            {
                return string.Empty;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            if (def != null && !string.IsNullOrEmpty(def.labelKey))
            {
                return def.labelKey.Translate().ToString();
            }
            return ev.TypeKey;
        }

        private static string EventDescription(ChronicleEvent ev)
        {
            if (ev == null)
            {
                return string.Empty;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            if (def == null || string.IsNullOrEmpty(def.descriptionKey))
            {
                return string.Empty;
            }
            return def.descriptionKey.Translate().ToString();
        }

        /// <summary>
        /// v4.13: localized label for a chronicle event type. Driven by the
        /// ChronicleEventDef taxonomy (LabelCap), never magic per-typeKey strings;
        /// unknown defs fall back to the generic EvOther translation.
        /// </summary>
        private static string ChronicleEventTypeLabel(string typeKey)
        {
            if (string.IsNullOrEmpty(typeKey))
            {
                return "PersonalChronicle.UI.EvOther".Translate().ToString();
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(typeKey);
            if (def != null && !string.IsNullOrEmpty(def.LabelCap))
            {
                return def.LabelCap;
            }
            return "PersonalChronicle.UI.EvOther".Translate().ToString();
        }

        /// <summary>
        /// v4.0 timeline glyph per event kind. Driven by ChronicleEventKind so it stays
        /// data-coherent with the Def taxonomy; unknown kinds fall back to a neutral dot.
        /// No magic per-typeKey strings — only the four canonical kinds are branched.
        /// </summary>
        private static string EventTypeToGlyph(string typeKey)
        {
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(typeKey);
            if (def == null)
            {
                return "•";
            }
            switch (def.kind)
            {
                case ChronicleEventKind.Join:
                    return "✚";
                case ChronicleEventKind.Death:
                    return "✝";
                case ChronicleEventKind.Battle:
                    return "⚔";
                case ChronicleEventKind.Social:
                    return "❖";
                case ChronicleEventKind.Craft:
                    return "⚒";
                case ChronicleEventKind.Built:
                    return "▣";
                case ChronicleEventKind.Other:
                default:
                    return "•";
            }
        }

        /// <summary>
        /// v4.0 timeline node color by kind (mirrors Def taxonomy, no per-typeKey magic).
        /// </summary>
        private static Color EventTypeToColor(string typeKey)
        {
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(typeKey);
            if (def == null)
            {
                return ArchiveUiStyle.TimelineOther;
            }
            switch (def.kind)
            {
                case ChronicleEventKind.Join:
                    return ArchiveUiStyle.TimelineJoin;
                case ChronicleEventKind.Death:
                    return ArchiveUiStyle.TimelineDeath;
                case ChronicleEventKind.Battle:
                    return ArchiveUiStyle.TimelineBattle;
                case ChronicleEventKind.Social:
                    return ArchiveUiStyle.TimelineSocial;
                case ChronicleEventKind.Craft:
                    return ArchiveUiStyle.TimelineCraft;
                case ChronicleEventKind.Built:
                    return ArchiveUiStyle.TimelineBuilt;
                case ChronicleEventKind.Other:
                default:
                    return ArchiveUiStyle.TimelineOther;
            }
        }

        private static string KindLabel(PawnRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.KindDefName))
            {
                return string.Empty;
            }
            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(record.KindDefName);
            if (kindDef != null && !string.IsNullOrEmpty(kindDef.label))
            {
                return kindDef.label;
            }
            if (kindDef == null)
            {
                LogMissingDefOnce(record.KindDefName);
            }
            return record.KindDefName;
        }

        private static string FactionLabel(PawnRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.FactionDefName))
            {
                return string.Empty;
            }
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(record.FactionDefName);
            if (factionDef != null && !string.IsNullOrEmpty(factionDef.label))
            {
                return factionDef.label;
            }
            if (factionDef == null)
            {
                LogMissingDefOnce(record.FactionDefName);
            }
            return record.FactionDefName;
        }

        private static string ThingDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            if (def == null)
            {
                LogMissingDefOnce(defName);
            }
            // Missing Def (e.g. third-party mod removed after the object was
            // archived): fall back to the raw defName — never crash, never red-text.
            return defName;
        }

        /// <summary>
        /// Label for a production card. The production summary is aggregated by
        /// ThingCategory (e.g. "WeaponsRanged") since v4.6.5, so the defName may be
        /// either a concrete ThingDef or a ThingCategoryDef — resolve both before
        /// falling back to the raw key. A category key that no longer exists (mod
        /// removed) degrades to the raw defName without red-text.
        /// </summary>
        private static string ProductionDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            ThingCategoryDef cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(defName);
            if (cat != null && !string.IsNullOrEmpty(cat.label))
            {
                return cat.label;
            }
            // Neither resolved: it is a genuine missing def (third-party mod
            // uninstalled) — log once and degrade to the raw key.
            LogMissingDefOnce(defName);
            return defName;
        }

        private static string IncidentDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            if (def == null)
            {
                LogMissingDefOnce(defName);
            }
            return defName;
        }

        private static string FactionDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            return defName;
        }

        /// <summary>
        /// Missing-Def detection helpers (LD-7, P4-6): when an archived object
        /// references a Def that no longer exists (e.g. the user uninstalled
        /// Medieval Overhaul after the object was archived), the UI must render
        /// the raw defName as a placeholder instead of crashing. Each missing
        /// defName is logged at most once per session so the log surfaces the
        /// drift without spam.
        /// </summary>
        private static readonly HashSet<string> loggedMissingDefs = new HashSet<string>();

        private static void LogMissingDefOnce(string defName)
        {
            if (string.IsNullOrEmpty(defName) || loggedMissingDefs.Contains(defName))
            {
                return;
            }
            loggedMissingDefs.Add(defName);
            // Warning (not Error): expected after mod removal; no red-text.
            ChronicleLog.Warning(ChronicleLog.Category.Ui, "missing def for display: " + defName);
        }

        private static string FormatDate(long tick)
        {
            // tick 0 是新档第 1 天（开局殖民者 JoinTick=0 即此），是合法日期；
            // 仅 -1（未知哨兵）才显示"未知"。
            if (tick < 0L)
            {
                return "PersonalChronicle.UI.UnknownDate".Translate().ToString();
            }
            return GenDate.DateReadoutStringAt(tick, Vector2.zero);
        }

        private static string CauseLabel(string deathCauseKey)
        {
            if (string.IsNullOrEmpty(deathCauseKey))
            {
                return string.Empty;
            }
            return deathCauseKey.Translate().ToString();
        }

        private static string FormatParams(IReadOnlyDictionary<string, string> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return string.Empty;
            }
            List<string> parts = new List<string>(parameters.Count);
            foreach (KeyValuePair<string, string> pair in parameters)
            {
                if (string.IsNullOrEmpty(pair.Value))
                {
                    continue;
                }
                // Known params render through a translation mapping; unknown
                // keys are filtered out (zero hardcoded copy in code).
                if (pair.Key == ChronicleEventParams.Killer)
                {
                    parts.Add("PersonalChronicle.UI.KillerWithName".Translate(pair.Value).ToString());
                }
                else if (pair.Key == ChronicleEventParams.Relation)
                {
                    parts.Add(RelationDefLabel(pair.Value));
                }
                else if (pair.Key == ChronicleEventParams.RelationAction)
                {
                    if (pair.Value == ChronicleEventParams.RelationActionFormed)
                    {
                        parts.Add("PersonalChronicle.UI.RelActionFormed".Translate().ToString());
                    }
                    else if (pair.Value == ChronicleEventParams.RelationActionEnded)
                    {
                        parts.Add("PersonalChronicle.UI.RelActionEnded".Translate().ToString());
                    }
                }
                else if (pair.Key == ChronicleEventParams.Victim)
                {
                    parts.Add("PersonalChronicle.UI.VictimWithName".Translate(pair.Value).ToString());
                }
            }
            return string.Join("  ", parts.ToArray());
        }

        private static bool IsSocialEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
            {
                return false;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && def.kind == ChronicleEventKind.Social;
        }

        private static bool IsDeathEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
            {
                return false;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && def.kind == ChronicleEventKind.Death;
        }

        private static bool IsBattleEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
            {
                return false;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && def.kind == ChronicleEventKind.Battle;
        }

        private static bool IsCraftEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
            {
                return false;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && def.kind == ChronicleEventKind.Craft;
        }

        private static bool IsBuiltEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
            {
                return false;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && def.kind == ChronicleEventKind.Built;
        }

        // ---- Cached view records (preformatted text, no per-frame work) --------

        private struct RecentLineView
        {
            public string DateText;
            public string TitleText;
            public string TypeText;
            public ChronicleEvent Event;
        }

        private struct EventLineView
        {
            public string DateText;
            public string NameText;
            public string ParamsText;
            public string DescriptionText;
            public List<ChipView> Chips;
            public ChronicleEvent Event;
            public ChronicleImportance Importance;
        }

        private struct ChipView
        {
            public string Label;
            public NavTarget Target;
            public string StableId;
        }

        private struct LinkedObjectView
        {
            public string StableId;
            public string Label;
            public string CategoryLabel;
            public string CategoryKey;
            public NavTarget Target;
            /// <summary>v4.14: co-occurrence count — how many detail events reference
            /// this linked object (shared-fate counter shown in the intertwined list).</summary>
            public int SharedCount;
        }

        private struct ImportantCardView
        {
            public string Label;
            public string SubLabel;
            public string TagLabel;
            public NavTarget Target;
            public string StableId;
        }

        private struct CombatLineView
        {
            public string DateText;
            public string TitleText;
            public string SubText;
            public NavTarget Target;
            public string StableId;
            public ChronicleEvent TargetEvent;
        }

        /// <summary>v4.3: one faction-codex card = one faction, aggregated from kill lines.</summary>
        private sealed class FactionCodexView
        {
            public string FactionKey;          // grouping key
            public string DisplayName;         // shown in card header
            public ArchiveUiStyle.FactionCodexKind Kind;
            public string RelationKey;         // hostile / neutral / ally / unresolved
            public int KillCount;
            public int RaidCount;
            public int BattleCount;
            public int OurLossCount;           // member lines where this faction killed us (unused for enemy; for __player__ = deaths)
            public List<CombatLineView> MemberLines;
            public List<KeyValuePair<string, int>> Composition; // victim-kind label -> count
        }

        private struct TreeLineView
        {
            public int Depth;
            public string Prefix;
            public string Label;
            public NavTarget Target;
            public string StableId;
            public ChronicleEvent TargetEvent;
        }

        private struct SocialNodeView
        {
            public string StableId;
            public string Label;
            public string RelationLabel;
            public string RelationDefName;
            public bool Active;
        }

        private struct BranchView
        {
            public string HeaderKey;
            public List<LeafView> Leaves;
        }

        private struct LeafView
        {
            public string Label;
            public NavTarget Target;
            public string StableId;
            public ChronicleEvent TargetEvent;
        }

    }

    internal static class ArchiveCacheExtensions
    {
        public static int GetCount(this Dictionary<string, List<ArchiveObject>> dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out List<ArchiveObject> list) && list != null)
            {
                return list.Count;
            }
            return 0;
        }
    }
}
