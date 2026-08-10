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
    public sealed class ArchiveMainTabWindow : MainTabWindow
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
                        Target = NavTargetOfCategory(link.CategoryKey)
                    });
                }
            }
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
            cachedProductionLines = new List<ReadModels.ProductionLineView>();
            cachedProductionSummary = new ProductionSummaryView(0, 0f, -1L, new List<ProductionTypeView>());
            cachedWorkIntensity = new WorkIntensityView(
                WorkIntensityEvaluation.Undefined(null, "builtin"), null);
            cachedIntensityWorkTypes = new List<WorkIntensityWorkTypeView>();
            cachedTiers = new List<WorkIntensityTierView>();
            nextBadgeRefreshTick = 0L;
            cachedBadgeObjectId = string.Empty;
            cachedDeathKiller = null;
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

        private void DrawHomeContent(Rect inner, IArchiveService service)
        {
            float contentHeight = ComputeHomeHeight(inner.width);
            float viewHeight = Mathf.Max(inner.height, contentHeight);
            Rect viewRect = new Rect(inner.x, inner.y, inner.width - 16f, viewHeight);

            Widgets.BeginScrollView(inner, ref homeScroll, viewRect);

            float y = viewRect.y + 4f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 28f),
                "PersonalChronicle.UI.ColonyArchive".Translate().ToString());
            y += 30f;

            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.SecondaryText;
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 18f),
                "PersonalChronicle.UI.ArchiveHomeDesc".Translate().ToString());
            GUI.color = prevColor;
            Text.Font = GameFont.Small;
            y += 26f;

            // v4.0: view selector (B dashboard vs E chronicle timeline).
            y = DrawHomeViewTabs(viewRect, y, service);

            if (homeViewMode == HomeViewMode.Timeline)
            {
                DrawHomeTimeline(viewRect, y, service);
                Widgets.EndScrollView();
                return;
            }

            y = DrawHomeKpi(viewRect, y);
            y += 20f;

            float leftWidth = viewRect.width * 0.62f;
            float rightWidth = viewRect.width - leftWidth - 16f;
            Rect leftRect = new Rect(viewRect.x, y, leftWidth, viewHeight - (y - viewRect.y));
            Rect rightRect = new Rect(viewRect.x + leftWidth + 16f, y, rightWidth, viewHeight - (y - viewRect.y));

            DrawRecentHistory(leftRect, service);
            DrawImportantArchives(rightRect, service);

            Widgets.EndScrollView();
        }

        private float DrawHomeViewTabs(Rect viewRect, float y, IArchiveService service)
        {
            float tabWidth = 150f;
            float gap = 8f;
            float x = viewRect.x;
            string[] labels = new[]
            {
                "PersonalChronicle.UI.HomeKpiView".Translate().ToString(),
                "PersonalChronicle.UI.HomeTimelineView".Translate().ToString()
            };
            HomeViewMode[] modes = new[] { HomeViewMode.Kpi, HomeViewMode.Timeline };
            float startY = y;
            for (int i = 0; i < labels.Length; i++)
            {
                Rect tabRect = new Rect(x, y, tabWidth, HomeViewTabHeight);
                bool selected = homeViewMode == modes[i];
                ArchiveUiStyle.DrawSelectedNavigation(tabRect, selected);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(tabRect, labels[i]);
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonInvisible(tabRect) && !selected)
                {
                    homeViewMode = modes[i];
                    PersistHomeViewMode(service);
                    // v4.5.3: switching the home presentation immediately pulls the
                    // view-scoped cache (e.g. the full event stream for Timeline) so
                    // the new view is never blank until the next throttled refresh.
                    nextRefreshTick = 0L;
                    RefreshNow(service);
                }
                x += tabWidth + gap;
            }
            return startY + HomeViewTabHeight + 12f;
        }

        private void PersistHomeViewMode(IArchiveService service)
        {
            service.SetHomeViewMode((int)homeViewMode);
        }

        private void DrawHomeTimeline(Rect viewRect, float y, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
            if (cachedTimelineEvents == null || cachedTimelineEvents.Count == 0)
            {
                Text.Font = GameFont.Small;
                GUI.color = ArchiveUiStyle.SecondaryText;
                Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 24f),
                    "PersonalChronicle.UI.NoTimelineEvents".Translate().ToString());
                return;
            }
            // cachedTimelineEvents is already sorted ascending by Tick at
            // cache-rebuild time; reuse directly (no per-frame allocation/sort).
            IReadOnlyList<ChronicleEvent> ordered = cachedTimelineEvents;
            float spineX = viewRect.x + 14f;
            float nodeX = spineX + 18f;
            float rowH = 54f;
            float currentY = y;
            bool left = true;
            for (int i = 0; i < ordered.Count; i++)
            {
                ChronicleEvent ev = ordered[i];
                if (ev == null)
                {
                    continue;
                }
                if (ChronicleEventImportance.Resolve(ev) < ChronicleImportance.Important)
                {
                    // Timeline shows only key milestones (keeps large saves calm).
                    continue;
                }
                string typeKey = ev.TypeKey;
                string icon = EventTypeToGlyph(typeKey);
                Color color = EventTypeToColor(typeKey);
                string title = EventName(ev);
                string date = GenDate.DateReadoutStringAt(ev.Tick, Vector2.zero);

                // compute card geometry first (connector needs it).
                const float cardGap = 16f;
                const float minCardW = 40f;
                float availableW = Mathf.Max(0f, viewRect.width - nodeX - viewRect.x - 24f);
                float cardW = Mathf.Max(minCardW, availableW / 2f - cardGap / 2f);
                float cardX = left ? nodeX : nodeX + cardW + cardGap;
                Rect cardRect = new Rect(cardX, currentY + 4f, cardW, rowH - 8f);

                // spine segment
                GUI.color = ArchiveUiStyle.TimelineSpine;
                Widgets.DrawLineVertical(spineX, currentY, rowH);
                // node + horizontal connector
                GUI.color = color;
                Rect nodeRect = new Rect(spineX - 5f, currentY + rowH / 2f - 5f, 10f, 10f);
                GUI.DrawTexture(nodeRect, BaseContent.WhiteTex);
                float connectorStart = left ? nodeRect.xMax : cardRect.xMax;
                float connectorEnd = left ? cardRect.x : nodeRect.x;
                float connectorLen = Mathf.Max(0f, connectorEnd - connectorStart);
                Widgets.DrawLineHorizontal(connectorStart, currentY + rowH / 2f, connectorLen);
                GUI.color = prevColor;

                ArchiveUiStyle.DrawPanel(cardRect);
                Rect cardInner = cardRect.ContractedBy(6f);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(cardInner.x, cardInner.y, cardInner.width, 20f), icon + " " + title);
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.SecondaryText;
                Widgets.Label(new Rect(cardInner.x, cardInner.y + 22f, cardInner.width, 16f), date);
                GUI.color = prevColor;
                if (Widgets.ButtonInvisible(cardRect))
                {
                    OpenEventDetail(service, ev);
                }
                left = !left;
                currentY += rowH;
            }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        private static float ComputeHomeHeight(float width)
        {
            // tabs + KPI groups (2 section titles + 3 large cards + 5 small cards) + two columns.
            float tabsHeight = HomeViewTabHeight + 12f;
            float kpiHeight = 86f + 58f + 2 * 22f + 2 * 16f;
            float columnsHeight = 6 * TimelineRowHeight + 40f + 4 * 70f + 60f;
            return 4f + 30f + 26f + tabsHeight + kpiHeight + 20f + columnsHeight + 20f;
        }

        private float DrawHomeKpi(Rect viewRect, float y)
        {
            const float groupGap = 16f;
            const float cardGap = 10f;

            // ---------- Real-time indicators (3 large cards, green accent) ----------
            DrawSectionTitle(viewRect, ref y, "PersonalChronicle.UI.HomeRealtimeGroup".Translate().ToString());
            const float liveHeight = 86f;
            int liveCount = 3;
            float liveCardW = Mathf.Max(40f, (viewRect.width - (liveCount - 1) * cardGap) / liveCount);
            DrawHomeMetricCard(
                new Rect(viewRect.x, y, liveCardW, liveHeight),
                "PersonalChronicle.UI.HomeLiveColonists".Translate().ToString(),
                cachedLiveColonistCount.ToString(),
                "PersonalChronicle.UI.HomeColonistBreakdown"
                    .Translate(cachedLiveFreeCount, cachedLiveSlaveCount, cachedLivePrisonerCount)
                    .ToString(),
                ArchiveUiStyle.Alive,
                isLarge: true);
            DrawHomeMetricCard(
                new Rect(viewRect.x + liveCardW + cardGap, y, liveCardW, liveHeight),
                "PersonalChronicle.UI.HomeServiceDays".Translate().ToString(),
                cachedServiceDays.ToString(),
                "PersonalChronicle.UI.HomeServiceSince".Translate().ToString(),
                ArchiveUiStyle.Accent,
                isLarge: true);
            DrawHomeMetricCard(
                new Rect(viewRect.x + 2 * (liveCardW + cardGap), y, liveCardW, liveHeight),
                "PersonalChronicle.UI.HomeLivePawns".Translate().ToString(),
                cachedActivePawnCount.ToString(),
                "PersonalChronicle.UI.HomeSnapshotBreakdown"
                    .Translate(cachedActivePawnCount, cachedArchivedPawnCount)
                    .ToString(),
                ArchiveUiStyle.Text,
                isLarge: true);
            y += liveHeight;

            y += groupGap;

            // ---------- Archive library (5 small cards) ----------
            DrawSectionTitle(viewRect, ref y, "PersonalChronicle.UI.HomeArchiveGroup".Translate().ToString());
            const float archiveHeight = 58f;
            int archiveCount = 5;
            float archiveCardW = Mathf.Max(40f, (viewRect.width - (archiveCount - 1) * cardGap) / archiveCount);
            DrawHomeMetricCard(
                new Rect(viewRect.x, y, archiveCardW, archiveHeight),
                "PersonalChronicle.UI.Category.Pawn".Translate().ToString(),
                cachedCategoryObjects.GetCount(ArchiveCategoryKeys.Pawn).ToString(),
                null,
                ArchiveUiStyle.Text,
                isLarge: false);
            DrawHomeMetricCard(
                new Rect(viewRect.x + archiveCardW + cardGap, y, archiveCardW, archiveHeight),
                "PersonalChronicle.UI.Category.Thing".Translate().ToString(),
                cachedCategoryObjects.GetCount(ArchiveCategoryKeys.Thing).ToString(),
                null,
                ArchiveUiStyle.Text,
                isLarge: false);
            DrawHomeMetricCard(
                new Rect(viewRect.x + 2 * (archiveCardW + cardGap), y, archiveCardW, archiveHeight),
                "PersonalChronicle.UI.Category.Battle".Translate().ToString(),
                cachedCategoryObjects.GetCount(ArchiveCategoryKeys.Battle).ToString(),
                null,
                ArchiveUiStyle.Text,
                isLarge: false);
            DrawHomeMetricCard(
                new Rect(viewRect.x + 3 * (archiveCardW + cardGap), y, archiveCardW, archiveHeight),
                "PersonalChronicle.UI.Category.Location".Translate().ToString(),
                cachedCategoryObjects.GetCount(ArchiveCategoryKeys.Location).ToString(),
                null,
                ArchiveUiStyle.Text,
                isLarge: false);
            DrawHomeMetricCard(
                new Rect(viewRect.x + 4 * (archiveCardW + cardGap), y, archiveCardW, archiveHeight),
                "PersonalChronicle.UI.ArchivedRecords".Translate().ToString(),
                cachedArchivedPawnCount.ToString(),
                null,
                ArchiveUiStyle.Muted,
                isLarge: false);
            y += archiveHeight;

            return y;
        }

        private static void DrawHomeMetricCard(Rect rect, string label, string value, string subLabel, Color accent, bool isLarge)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                ArchiveUiStyle.DrawPanel(rect);
                float pad = isLarge ? 12f : 8f;
                Rect inner = rect.ContractedBy(pad);

                // Label — Tiny, CJK line-height >= 18f.
                Text.Font = GameFont.Tiny;
                const float labelH = 18f;
                GUI.color = UITheme.Muted;
                Widgets.Label(new Rect(inner.x, inner.y, inner.width, labelH), label);

                // Value — Medium, CJK line-height >= 28f (small cards get 28f, large 32f).
                Text.Font = GameFont.Medium;
                float valueH = isLarge ? 32f : 28f;
                float valueY = isLarge ? inner.y + 24f : inner.y + 22f;
                GUI.color = accent;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(inner.x, valueY, inner.width, valueH), value);
                Text.Anchor = TextAnchor.UpperLeft;

                if (!string.IsNullOrEmpty(subLabel))
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = UITheme.Muted;
                    float subY = isLarge ? inner.y + 56f : inner.y + 50f;
                    Widgets.Label(new Rect(inner.x, subY, inner.width, 18f), subLabel);
                }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        private void DrawRecentHistory(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.RecentHistory".Translate().ToString());

            if (cachedRecentLines.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoEvents".Translate().ToString());
                return;
            }

            for (int i = 0; i < cachedRecentLines.Count; i++)
            {
                RecentLineView line = cachedRecentLines[i];
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                DrawEventRow(row, line.DateText, line.TitleText, line.TypeText);
                if (line.Event != null && Widgets.ButtonInvisible(row))
                {
                    OpenEventDetail(service, line.Event);
                }
                y += TimelineRowHeight;
            }
        }

        private void DrawImportantArchives(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.ImportantArchives".Translate().ToString());

            if (cachedImportantCards.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoEvents".Translate().ToString());
                return;
            }

            // 64f = CJK-safe three-line card (Tiny 18 + Small 24 + Tiny 18 = 60f + 4f pad).
            const float cardHeight = 64f;
            const float cardGap = UITheme.GridGap;
            for (int i = 0; i < cachedImportantCards.Count; i++)
            {
                ImportantCardView card = cachedImportantCards[i];
                Rect cardRect = new Rect(rect.x, y, rect.width, cardHeight);
                ArchiveUiStyle.DrawCard(cardRect, ArchiveUiStyle.Accent);

                Color prevColor = GUI.color;
                GameFont prevFont = Text.Font;
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Accent;
                Widgets.Label(new Rect(cardRect.x + UITheme.CardPadX, cardRect.y + 4f, cardRect.width - UITheme.CardPadX * 2f, 18f), card.TagLabel);

                Text.Font = GameFont.Small;
                GUI.color = ArchiveUiStyle.Text;
                Widgets.Label(new Rect(cardRect.x + UITheme.CardPadX, cardRect.y + 22f, cardRect.width - UITheme.CardPadX * 2f, 24f), card.Label);

                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(cardRect.x + UITheme.CardPadX, cardRect.y + 46f, cardRect.width - UITheme.CardPadX * 2f, 18f), card.SubLabel);
                GUI.color = prevColor;
                Text.Font = prevFont;

                if (card.Target != NavTarget.None && Widgets.ButtonInvisible(cardRect))
                {
                    NavigateTarget(service, card.Target, card.StableId, null);
                }
                y += cardHeight + cardGap;
            }
        }

        // ---- Overview ----------------------------------------------------------

        /// <summary>
        /// v4.11 P0: Overview › Battle cards. Each card shows the three captured
        /// elements — trigger date, raid force size and repulse duration — drawn
        /// through the Design System (UIComponents.Card + UIComponents.Label with
        /// UITheme tokens; Font/color/anchor pairing is handled inside the
        /// components, never in the window). Battles are stat-only: not clickable
        /// into a detail view. Dimensions are the single source shared with
        /// ComputeOverviewHeight to keep the scroll region height honest.
        /// </summary>
        private const float BattleCardWidth = 220f;
        private const float BattleCardHeight = 104f;

        private float DrawBattleOverviewCards(Rect viewRect, float startY, List<ArchiveObject> objects, float gap)
        {
            int perRow = Mathf.Max(1, (int)((viewRect.width + gap) / (BattleCardWidth + gap)));

            for (int i = 0; i < objects.Count; i++)
            {
                BattleObject battle = objects[i] as BattleObject;
                if (battle == null)
                {
                    continue;
                }
                int col = i % perRow;
                int row = i / perRow;
                Rect card = new Rect(
                    viewRect.x + col * (BattleCardWidth + gap),
                    startY + row * (BattleCardHeight + gap),
                    BattleCardWidth, BattleCardHeight);

                ArchiveUiStyle.DrawCard(card, ArchiveCardAccent(battle));
                float x = card.x + UITheme.CardPadX;
                float w = card.width - UITheme.CardPadX * 2f;
                float y = card.y + UITheme.CardPadY;

                // Category caption (Tiny).
                UIComponents.Label(new Rect(x, y, w, UITheme.FontBodyLineHeight),
                    "PersonalChronicle.UI.Battle".Translate().ToString(), UITheme.FontLabel, ArchiveUiStyle.Muted);
                y += UITheme.FontBodyLineHeight;

                // Battle title (Small).
                UIComponents.Label(new Rect(x, y, w, UITheme.FontBodyLineHeight),
                    ObjectDisplayLabel(battle), UITheme.FontBody, ArchiveUiStyle.Info);
                y += UITheme.FontBodyLineHeight;

                // Three-element rows (Tiny, row height ≥18f per rimworld-ui-standards
                // §4; FontBodyLineHeight=22f is the nearest CJK-safe token).
                string dateText = battle.StartTick > 0L
                    ? RimWorld.GenDate.DateReadoutStringAt(battle.StartTick, UnityEngine.Vector2.zero)
                    : "PersonalChronicle.UI.UnknownDate".Translate().ToString();
                string raidText = battle.RaidCount > 0
                    ? "PersonalChronicle.UI.BattleRaidCount".Translate(battle.RaidCount).ToString()
                    : "—";
                UIComponents.Label(new Rect(x, y, w, UITheme.FontBodyLineHeight),
                    dateText, UITheme.FontLabel, ArchiveUiStyle.Muted);
                y += UITheme.FontBodyLineHeight;

                UIComponents.Label(new Rect(x, y, w, UITheme.FontBodyLineHeight),
                    raidText + "   " + BattleDurationText(battle), UITheme.FontLabel, ArchiveUiStyle.Muted);
            }

            int rows = (objects.Count - 1) / perRow + 1;
            return rows * (BattleCardHeight + gap) + 14f;
        }

        // ---- v4.13 location atlas overview cards ----
        private const float LocationCardWidth = 230f;
        private const float LocationCardHeight = 140f;

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

                // Identity: kind badge + name.
                string kindText = LocationKindText(loc);
                UIComponents.Label(new Rect(x, y, w - 60f, UITheme.FontBodyLineHeight),
                    kindText, UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(x + w - 56f, y, 56f, UITheme.FontBodyLineHeight),
                    loc.IsPlayerHome ? "PersonalChronicle.UI.LocKind.Player".Translate().ToString()
                        : (loc.DeinitTick != -1L ? "PersonalChronicle.UI.LocLifeRuined".Translate().ToString() : ""),
                    UITheme.FontLabel, UITheme.Muted);
                y += UITheme.FontBodyLineHeight;

                UIComponents.Label(new Rect(x, y, w, UITheme.FontBodyLineHeight),
                    ObjectDisplayLabel(loc), UITheme.FontBody, ArchiveUiStyle.Info);
                y += UITheme.FontBodyLineHeight;

                // Ownership / geography / commerce / lifecycle — small lines use
                // 20f rect height (≥18f CJK-safe minimum per rimworld-ui-standards
                // §4) with a 22f step so long localized strings never clip.
                UIComponents.Label(new Rect(x, y, w, 20f),
                    LocationFactionText(loc), UITheme.FontLabel, ArchiveUiStyle.Muted);
                y += 22f;

                // Geography tags (compact single line; may wrap).
                string geo = LocationGeoText(loc);
                if (!string.IsNullOrEmpty(geo))
                {
                    UIComponents.Label(new Rect(x, y, w, 20f), geo, UITheme.FontLabel, ArchiveUiStyle.Muted);
                    y += 22f;
                }

                // Commerce chip.
                string trade = LocationTradeText(loc);
                if (!string.IsNullOrEmpty(trade))
                {
                    UIComponents.Label(new Rect(x, y, w, 20f), trade, UITheme.FontLabel,
                        loc.CanTrade ? UITheme.Accent : ArchiveUiStyle.Muted);
                    y += 22f;
                }

                // Lifecycle.
                UIComponents.Label(new Rect(x, y, w, 20f),
                    LocationLifeText(loc), UITheme.FontLabel, ArchiveUiStyle.Muted);
                y += 22f;

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
            List<ChronicleEvent> ordered = (events == null)
                ? new List<ChronicleEvent>()
                : events.Where(e => e != null).OrderByDescending(e => e.Tick).ToList();
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
            if (loc.DeinitReason == "Destroyed")
            {
                return "PersonalChronicle.UI.LocDeinit.Destroyed".Translate().ToString();
            }
            if (loc.DeinitReason == "Abandoned")
            {
                return "PersonalChronicle.UI.LocDeinit.Abandoned".Translate().ToString();
            }
            return "PersonalChronicle.UI.LocLifeRuined".Translate().ToString();
        }

        /// <summary>Repulse duration text: EndTick - StartTick, or "ongoing" while the raid is not yet repulsed. Sub-day durations fall back to hours so short raids don't show "0 天".</summary>
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
            long ticks = battle.EndTick - battle.StartTick;
            long days = ticks / RimWorld.GenDate.TicksPerDay;
            if (days >= 1L)
            {
                return "PersonalChronicle.UI.BattleDuration".Translate(days).ToString();
            }
            long hours = ticks / RimWorld.GenDate.TicksPerHour;
            if (hours >= 1L)
            {
                return "PersonalChronicle.UI.BattleDurationHours".Translate(hours).ToString();
            }
            long minutes = ticks / 60L;
            return "PersonalChronicle.UI.BattleDurationMins".Translate(minutes).ToString();
        }

        private void DrawOverviewContent(Rect inner, IArchiveService service)
        {
            float contentHeight = ComputeOverviewHeight(inner.width);
            float viewHeight = Mathf.Max(inner.height, contentHeight);
            Rect viewRect = new Rect(inner.x, inner.y, inner.width - 16f, viewHeight);

            Widgets.BeginScrollView(inner, ref overviewScroll, viewRect);

            float y = viewRect.y + 4f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 28f),
                "PersonalChronicle.UI.OverviewTitle".Translate().ToString());
            y += 30f;

            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.SecondaryText;
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 18f),
                "PersonalChronicle.UI.OverviewDesc".Translate().ToString());
            GUI.color = prevColor;
            Text.Font = GameFont.Small;
            y += 28f;

            for (int i = 0; i < CategoryKeys.Length; i++)
            {
                string key = CategoryKeys[i];
                if (!string.IsNullOrEmpty(overviewCategoryFilter) && overviewCategoryFilter != key)
                {
                    continue;
                }
                if (!cachedCategoryObjects.TryGetValue(key, out List<ArchiveObject> objects) || objects.Count == 0)
                {
                    continue;
                }
                y = DrawOverviewSection(viewRect, y, key, objects, service);
            }

            Widgets.EndScrollView();
        }

        private float ComputeOverviewHeight(float width)
        {
            float height = 4f + 28f + 28f;
            const float gap = 12f;

            for (int i = 0; i < CategoryKeys.Length; i++)
            {
                string key = CategoryKeys[i];
                if (!string.IsNullOrEmpty(overviewCategoryFilter) && overviewCategoryFilter != key)
                {
                    continue;
                }
                if (!cachedCategoryObjects.TryGetValue(key, out List<ArchiveObject> objects) || objects.Count == 0)
                {
                    continue;
                }
                // v4.11 P0: Battle cards are larger (three-element layout) than the
                // generic 190x70 cards, so size the row math per category. Battle
                // dimensions reuse the shared constants so the scroll height can
                // never drift from the drawn card size.
                float cardWidth = key == ArchiveCategoryKeys.Battle ? BattleCardWidth : 190f;
                float cardHeight = key == ArchiveCategoryKeys.Battle ? BattleCardHeight : 70f;
                int perRow = Mathf.Max(1, (int)((width + gap) / (cardWidth + gap)));
                height += 30f; // section title
                int rows = (objects.Count - 1) / perRow + 1;
                height += rows * (cardHeight + gap);
                height += 14f;
            }
            return height + 20f;
        }

        private float DrawOverviewSection(Rect viewRect, float y, string categoryKey, List<ArchiveObject> objects, IArchiveService service)
        {
            Text.Font = GameFont.Small;
            // P4-4: formatted via translation key (no hardcoded " · " glue).
            // 人物分类额外附当前活读人口数，直接回应"人物须捕捉当前殖民者数量"。
            string title = "PersonalChronicle.UI.SectionTitleCount"
                .Translate(CategoryLabel(categoryKey), objects.Count)
                .ToString();
            if (categoryKey == ArchiveCategoryKeys.Pawn)
            {
                title = title + "PersonalChronicle.UI.OverviewLiveSuffix"
                    .Translate(cachedLiveColonistCount).ToString();
            }
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 22f), title);
            y += 28f;

            // v4.11 P0: the Battle category renders richer, three-element cards
            // (trigger date / force size / repulse duration) instead of the
            // generic stat-only card. The data is captured by LinkRaidLords +
            // the Lord.Notify_PawnLost patch.
            if (categoryKey == ArchiveCategoryKeys.Battle)
            {
                return y + DrawBattleOverviewCards(viewRect, y, objects, 12f);
            }

            // v4.13 P1: the Location category renders atlas cards (identity /
            // ownership / geography / lifecycle / commerce) with inline expansion
            // of the place's chronicle. Data derives from the LocationObject
            // snapshot + the read model; no live world queries in the window.
            // Engine APIs verified against 1.6 by reflection — enabled.
            if (categoryKey == ArchiveCategoryKeys.Location)
            {
                return y + DrawLocationOverviewCards(viewRect, y, objects, 12f, service);
            }

            const float cardWidth = 190f;
            const float cardHeight = 70f;
            const float gap2 = 12f;
            int perRow = Mathf.Max(1, (int)((viewRect.width + gap2) / (cardWidth + gap2)));

            // Def-driven clickability: only non-StatOnly categories are
            // navigable (Pawn/Thing drill into detail; Battle/Location are
            // stats-only). service is non-null on every call path (guarded in
            // DoWindowContents) — the null check below is defensive only.
            bool clickable = service != null
                && service.GetCategoryBehavior(categoryKey) != ArchiveDepthBehavior.StatOnly;
            for (int i = 0; i < objects.Count; i++)
            {
                ArchiveObject obj = objects[i];
                int col = i % perRow;
                int row = i / perRow;
                Rect card = new Rect(
                    viewRect.x + col * (cardWidth + gap2),
                    y + row * (cardHeight + gap2),
                    cardWidth, cardHeight);

                ArchiveUiStyle.DrawCard(card, ArchiveCardAccent(obj));
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(card.x + UITheme.CardPadX, card.y + 4f, card.width - UITheme.CardPadX * 2f, 16f),
                    clickable ? CategoryLabel(categoryKey) : "PersonalChronicle.UI.StatsOnlyNote".Translate().ToString());
                GUI.color = prevColor;

                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(card.x + UITheme.CardPadX, card.y + 22f, card.width - UITheme.CardPadX * 2f, 20f), ObjectDisplayLabel(obj));

                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(card.x + UITheme.CardPadX, card.y + 44f, card.width - UITheme.CardPadX * 2f, 18f), ObjectSubLabel(obj));
                GUI.color = prevColor;

                if (clickable && Widgets.ButtonInvisible(card))
                {
                    if (categoryKey == ArchiveCategoryKeys.Pawn)
                    {
                        OpenPawnDetail(service, obj.StableId);
                    }
                    else
                    {
                        OpenWeaponDetail(service, obj.StableId);
                    }
                }
            }
            Text.Font = GameFont.Small;

            int rows = (objects.Count - 1) / perRow + 1;
            return y + rows * (cardHeight + gap2) + 14f;
        }

        // ---- Detail ------------------------------------------------------------

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
            DrawDetailPanel(viewRect, service);
            Widgets.EndScrollView();
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
                return panelH + 8f + relationCount * 8f + socialEvents * (TimelineRowHeight + 2f)
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
            float healthH = 26f + 80f + 8f + 56f + 8f + 18f + eventsH + 6f + 22f + 12f;
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
        private void DrawPawnOverview(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            if (!(cachedDetailObject is PawnObject pawn))
            {
                return;
            }

            // v4.6.1: contribution-archive layout (matches contribution-archive-overview.html).
            // 0) Cover header: portrait + name + role description + stamp + verdict.
            // 1) Ledger header: 3-cell榨取总账 (output value / work hours / weekly yield).
            // 2) Output ledger: ProgressBar rows by production type.
            // 3) Health residual: 4 StatCells + 3 dim bars + depreciation event log + verdict.
            y = DrawCoverHeader(rect, y, pawn, service);
            y = DrawLedger(rect, y + UITheme.BlockGap, pawn);
            y = DrawOutputLedger(rect, y + UITheme.BlockGap);
            y = DrawHealthValuation(rect, y + UITheme.BlockGap, cachedHealth);
        }

        // ---- v4.4 Overview derived draw helpers ----

        private const float LifeTimelineNodeSize = 18f;

        private static float DrawLifeTimeline(Rect rect, float y, IReadOnlyList<ReadModels.LifePhaseView> phases)
        {
            if (phases == null || phases.Count == 0)
            {
                return DrawEmptyLine(rect, y, "PersonalChronicle.UI.NoEvents");
            }
            float x = rect.x + 6f;
            float textW = rect.width - 6f - (LifeTimelineNodeSize + 10f);
            // Pre-compute dynamic node heights with real font line-heights so long
            // Chinese titles wrap and never overlap (must match UIComponents.TimelineNode).
            float[] heights = new float[phases.Count];
            float totalH = 0f;
            for (int i = 0; i < phases.Count; i++)
            {
                ReadModels.LifePhaseView p = phases[i];
                float titleH = Text.CalcHeight(p.PhaseKey.Translate().ToString(), textW);
                float block = titleH;
                if (!string.IsNullOrEmpty(p.DateText)) block += Text.CalcHeight(p.DateText, textW) + UITheme.SpaceXxs;
                if (!string.IsNullOrEmpty(p.SubText)) block += Text.CalcHeight(p.SubText, textW) + UITheme.SpaceXxs;
                float h = Mathf.Max(LifeTimelineNodeSize, block) + UITheme.SpaceXs;
                heights[i] = h;
                totalH += h;
            }

            // Vertical spine connecting the centers of all nodes.
            float spineX = x + LifeTimelineNodeSize / 2f;
            float firstCenterY = y + 2f + LifeTimelineNodeSize / 2f;
            float lastCenterY = y + totalH - heights[phases.Count - 1] + 2f + LifeTimelineNodeSize / 2f;
            float spineH = Mathf.Max(0f, lastCenterY - firstCenterY);
            Color prevColor = GUI.color;
            GUI.color = UITheme.TimelineSpine;
            Widgets.DrawLineVertical(spineX, firstCenterY, spineH);
            GUI.color = prevColor;

            float ny = y;
            for (int i = 0; i < phases.Count; i++)
            {
                ReadModels.LifePhaseView p = phases[i];
                Color dot = TimelinePhaseColor(p.Kind);
                ny = UIComponents.TimelineNode(
                    new Rect(x, ny, rect.width - 6f, heights[i]), ny,
                    p.PhaseKey.Translate().ToString(), p.DateText, p.SubText, dot, out _, p.IconKey);
            }
            return y + totalH;
        }

        private static Color TimelinePhaseColor(ReadModels.LifePhaseKind kind)
        {
            switch (kind)
            {
                case ReadModels.LifePhaseKind.Origin:
                case ReadModels.LifePhaseKind.Join:
                    return UITheme.TimelineJoin;
                case ReadModels.LifePhaseKind.Death:
                    return UITheme.TimelineDeath;
                case ReadModels.LifePhaseKind.Unknown:
                    return UITheme.Dead;
                default:
                    return UITheme.Info;
            }
        }

        private static float DrawCareerBars(Rect rect, float y, IReadOnlyList<ReadModels.CareerBarView> bars)
        {
            if (bars == null || bars.Count == 0)
            {
                return DrawEmptyLine(rect, y, "PersonalChronicle.UI.NoWorkData");
            }
            float rowH = 24f;
            for (int i = 0; i < bars.Count; i++)
            {
                ReadModels.CareerBarView b = bars[i];
                string tag = b.IsPrimary ? "PersonalChronicle.UI.CareerPrimary".Translate().ToString()
                    : b.IsSecondary ? "PersonalChronicle.UI.CareerSecondary".Translate().ToString() : "";
                Color tagColor = b.IsPrimary ? UITheme.Accent : UITheme.SecondaryText;
                UIComponents.Label(new Rect(rect.x, y, 150f, 18f),
                    b.WorkTypeLabel + (tag != "" ? " (" + tag + ")" : ""),
                    GameFont.Tiny, tagColor);
                // bar track (width driven by share %)
                float barX = rect.x + 156f;
                float barW = rect.width - 156f - 96f;
                UIComponents.ProgressBar(new Rect(barX, y + (rowH - UITheme.ProgressbarH) / 2f, barW, UITheme.ProgressbarH), b.Share01, UITheme.Accent);
                UIComponents.Label(new Rect(rect.x + rect.width - 92f, y, 90f, 18f),
                    FormatWorkHours(b.Ticks) + " · " + (int)(b.Share01 * 100) + "%",
                    GameFont.Tiny, UITheme.Muted);
                y += rowH;
            }
            return y;
        }

        private static float DrawFootprintLedger(Rect rect, float y, ReadModels.FootprintLedgerView led)
        {
            if (led == null || led.PlaceCount == 0)
            {
                return DrawEmptyLine(rect, y, "PersonalChronicle.UI.NoPlaceHistory");
            }
            // summary row
            float cardW = (rect.width - UITheme.GridGap * 2f) / 3f;
            float cardH = UIComponents.StatCellMinHeight;
            UIComponents.StatCell(new Rect(rect.x, y, cardW, cardH),
                "PersonalChronicle.UI.FootprintPlaces".Translate().ToString(), led.PlaceCount.ToString());
            UIComponents.StatCell(new Rect(rect.x + cardW + UITheme.GridGap, y, cardW, cardH),
                "PersonalChronicle.UI.FootprintHome".Translate().ToString() + " · " + led.HomeDays + "d",
                led.HomePlaceText != null ? led.HomePlaceText : "—");
            UIComponents.StatCell(new Rect(rect.x + 2f * (cardW + UITheme.GridGap), y, cardW, cardH),
                "PersonalChronicle.UI.FootprintExpeditions".Translate().ToString(), led.ExpeditionCount.ToString());
            y += cardH + UITheme.GridGap;
            // stays (already sorted longest-first)
            float rowH = 22f;
            int maxRows = Mathf.Min(led.Stays.Count, 6);
            for (int i = 0; i < maxRows; i++)
            {
                ReadModels.FootstepView s = led.Stays[i];
                string icon = s.IsWorldTile ? "🌍" : "🏕️";
                UIComponents.Label(new Rect(rect.x, y, 20f, 18f), icon, GameFont.Small, UITheme.Text);
                UIComponents.Label(new Rect(rect.x + 22f, y, rect.width - 110f, 18f),
                    s.PlaceText + (s.IsHome ? " 〔" + "PersonalChronicle.UI.FootprintHomeTag".Translate().ToString() + "〕" : ""),
                    GameFont.Small, s.IsHome ? UITheme.Accent : UITheme.Text);
                UIComponents.Label(new Rect(rect.x + rect.width - 86f, y, 84f, 18f), s.DwellText,
                    GameFont.Tiny, UITheme.Muted);
                y += rowH;
            }
            return y;
        }

        private static float DrawMilestoneGrid(Rect rect, float y, IReadOnlyList<ReadModels.MilestoneView> ms)
        {
            if (ms == null || ms.Count == 0)
            {
                return DrawEmptyLine(rect, y, "PersonalChronicle.UI.NoMilestones");
            }
            float cardW = (rect.width - 8f) / 2f;
            int perRow = 2;
            // First pass: compute adaptive card height from real line-heights.
            float maxH = 56f;
            for (int i = 0; i < ms.Count; i++)
            {
                ReadModels.MilestoneView m = ms[i];
                float titleH = Text.CalcHeight(m.TitleText, cardW - 40f);
                float subH = Text.CalcHeight(m.DateText + " · " + m.SubText, cardW - 40f);
                float h = 6f + 20f + 6f + titleH + 2f + subH + 6f;
                if (h > maxH) maxH = h;
            }
            float cardH = maxH;
            for (int i = 0; i < ms.Count; i++)
            {
                ReadModels.MilestoneView m = ms[i];
                float cx = rect.x + (i % perRow) * (cardW + UITheme.GridGap);
                float cy = y + (i / perRow) * (cardH + UITheme.GridGap);
                UIComponents.Card(new Rect(cx, cy, cardW, cardH), UITheme.Border);
                UIComponents.Label(new Rect(cx + UITheme.CardPadX, cy + 6f, 20f, 20f), m.IconKey, GameFont.Medium, UITheme.Text);
                float titleH = Text.CalcHeight(m.TitleText, cardW - 40f);
                UIComponents.Label(new Rect(cx + 32f, cy + 6f, cardW - 40f, titleH), m.TitleText,
                    GameFont.Small, UITheme.Text);
                UIComponents.Label(new Rect(cx + 32f, cy + 6f + 20f + 2f, cardW - 40f,
                        cardH - (6f + 20f + 2f + 6f)),
                    m.DateText + " · " + m.SubText, GameFont.Tiny, UITheme.Muted);
            }
            int rows = (ms.Count + perRow - 1) / perRow;
            return y + rows * (cardH + UITheme.GridGap);
        }

        private static float DrawKeyEventStream(Rect rect, float y, IReadOnlyList<ReadModels.KeyEventView> evs)
        {
            if (evs == null || evs.Count == 0)
            {
                return DrawEmptyLine(rect, y, "PersonalChronicle.UI.NoEvents");
            }
            for (int i = 0; i < evs.Count; i++)
            {
                ReadModels.KeyEventView e = evs[i];
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                UIComponents.Rule(new Rect(row.x, row.y + 1f, 3f, TimelineRowHeight - 2f),
                    e.IsHighlight ? UITheme.Accent : UITheme.BorderSoft);
                UIComponents.Label(new Rect(row.x + 10f, row.y + 4f, 120f, 18f), e.DateText,
                    GameFont.Tiny, UITheme.Dim);
                UIComponents.Label(new Rect(row.x + 136f, row.y + 4f, row.width - 136f - 70f, 20f),
                    (e.IsHighlight ? "✦ " : "") + e.TitleText, GameFont.Small, UITheme.Text);
                UIComponents.Label(new Rect(row.x + row.width - 66f, row.y + 4f, 64f, 18f), e.TypeText,
                    GameFont.Tiny, UITheme.Muted);
                UIComponents.Rule(new Rect(row.x, row.yMax - 1f, row.width, 1f), UITheme.BorderSoft);
                y += TimelineRowHeight;
            }
            return y;
        }

        private static float DrawEmptyLine(Rect rect, float y, string key)
        {
            UIComponents.Label(new Rect(rect.x, y, rect.width, 22f), key.Translate().ToString(),
                GameFont.Small, UITheme.Muted);
            return y + 26f;
        }

        /// <summary>
        /// P2: overview KPI uses the same combat caches as CombatLog tab.
        /// </summary>
        private void CountCombatKpis(out int battleCount, out int killCount)
        {
            battleCount = cachedBattleLines != null ? cachedBattleLines.Count : 0;
            killCount = cachedKillLines != null ? cachedKillLines.Count : 0;
        }

        private int CountLinkedPawns()
        {
            int n = 0;
            for (int i = 0; i < cachedLinkedObjects.Count; i++)
            {
                if (!string.IsNullOrEmpty(cachedLinkedObjects[i].StableId)
                    && cachedLinkedObjects[i].CategoryKey == ArchiveCategoryKeys.Pawn)
                {
                    n++;
                }
            }
            return n;
        }

        private int CountSignificantRelations(PawnObject pawn)
        {
            if (pawn != null && pawn.Relations != null)
            {
                int n = 0;
                for (int i = 0; i < pawn.Relations.Count; i++)
                {
                    SignificantRelation r = pawn.Relations[i];
                    if (r != null && r.IsActive)
                    {
                        n++;
                    }
                }
                if (n > 0)
                {
                    return n;
                }
            }
            // Fall back to event-co-occurrence people when no snapshots yet.
            return CountLinkedPawns();
        }

        /// <summary>
        /// Renders the last <paramref name="maxRows"/> place visits (newest first).
        /// </summary>
        private float DrawPlaceHistoryTable(Rect rect, float y, PawnObject pawn, int maxRows)
        {
            if (pawn == null || pawn.PlaceHistory == null || pawn.PlaceHistory.Count == 0)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = UITheme.SecondaryText;
                Widgets.Label(new Rect(rect.x, y, rect.width, 18f),
                    "PersonalChronicle.UI.NoPlaceHistory".Translate().ToString());
                GUI.color = prevColor;
                return y + 22f;
            }
            Text.Font = GameFont.Tiny;
            GUI.color = UITheme.SecondaryText;
            Widgets.Label(new Rect(rect.x + 6f, y, rect.width * 0.4f, 16f),
                "PersonalChronicle.UI.PlaceName".Translate().ToString());
            Widgets.Label(new Rect(rect.x + rect.width * 0.42f, y, rect.width * 0.28f, 16f),
                "PersonalChronicle.UI.PlaceEnter".Translate().ToString());
            Widgets.Label(new Rect(rect.x + rect.width * 0.72f, y, rect.width * 0.26f, 16f),
                "PersonalChronicle.UI.PlaceLeave".Translate().ToString());
            GUI.color = prevColor;
            y += 18f;
            int shown = 0;
            for (int i = pawn.PlaceHistory.Count - 1; i >= 0 && shown < maxRows; i--)
            {
                PlaceVisit v = pawn.PlaceHistory[i];
                if (v == null)
                {
                    continue;
                }
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x + 6f, y, rect.width * 0.4f, 20f), FormatPlaceKey(v));
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(rect.x + rect.width * 0.42f, y + 2f, rect.width * 0.28f, 18f), FormatDate(v.EnterTick));
                string leave = v.IsOpen
                    ? "PersonalChronicle.UI.PlaceStillThere".Translate().ToString()
                    : FormatDate(v.LeaveTick);
                Widgets.Label(new Rect(rect.x + rect.width * 0.72f, y + 2f, rect.width * 0.26f, 18f), leave);
                y += 22f;
                shown++;
            }
            return y;
        }

        private static string FormatPlaceKey(PlaceVisit v)
        {
            if (v == null || string.IsNullOrEmpty(v.PlaceKey))
            {
                return "—";
            }
            if (v.PlaceKind == "Caravan" || v.PlaceKey.StartsWith("tile:"))
            {
                string tile = v.PlaceKey.StartsWith("tile:") ? v.PlaceKey.Substring(5) : v.PlaceKey;
                return "PersonalChronicle.UI.PlacesWorldTile".Translate(tile).ToString();
            }
            return BiomeLabel(v.PlaceKey);
        }

        private static string FormatWorkHours(long ticks)
        {
            float hours = ticks / (float)RimWorld.GenDate.TicksPerHour;
            return hours.ToString("0.0") + " h";
        }

        private static string FormatMarketValue(float value)
        {
            return "PersonalChronicle.UI.MarketValueFormat".Translate(FormatSilver(value)).ToString();
        }

        // v4.6.8: abbreviate large silver values with K/M/B magnitude suffixes
        // (e.g. 1840 → "1.8K", 2_300_000 → "2.3M"). Keeps the unit sub-label intact.
        private static string FormatSilver(float value)
        {
            if (value < 1000f)
            {
                return Mathf.RoundToInt(value).ToString();
            }
            float scaled;
            string suffix;
            if (value >= 1e9f) { scaled = value / 1e9f; suffix = "B"; }
            else if (value >= 1e6f) { scaled = value / 1e6f; suffix = "M"; }
            else { scaled = value / 1e3f; suffix = "K"; }
            return scaled.ToString("0.##") + suffix;
        }

        private static void DrawMetricCard(Rect rect, string label, string value, string subLabel)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            try
            {
                ArchiveUiStyle.DrawCard(rect, ArchiveUiStyle.Info);
                bool large = rect.height >= 80f;
                // CJK-safe line heights: Tiny >= 18f, Medium >= 28f. For a 72f card
                // the block is 8 + 18 + 28 + 18 = 72f exactly.
                float labelH = 18f;
                float valueH = 28f;
                float subLabelH = 18f;
                float labelY = rect.y + UITheme.CardPadY;
                float valueY = labelY + labelH + (large ? 4f : 0f);
                float subLabelY = valueY + valueH + (large ? 4f : 0f);

                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(rect.x + UITheme.CardPadX, labelY, rect.width - UITheme.CardPadX * 2f, labelH), label);
                Text.Font = GameFont.Medium;
                GUI.color = ArchiveUiStyle.Text;
                Widgets.Label(new Rect(rect.x + UITheme.CardPadX, valueY, rect.width - UITheme.CardPadX * 2f, valueH), value);
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(rect.x + UITheme.CardPadX, subLabelY, rect.width - UITheme.CardPadX * 2f, subLabelH), subLabel);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
            }
        }

        private static string BackstoryLabel(PawnObject pawn)
        {
            if (pawn == null)
            {
                return string.Empty;
            }
            string child = BackstoryDefLabel(pawn.ChildhoodBackstoryDefName);
            string adult = BackstoryDefLabel(pawn.AdulthoodBackstoryDefName);
            if (string.IsNullOrEmpty(child) && string.IsNullOrEmpty(adult))
            {
                return "—";
            }
            if (string.IsNullOrEmpty(child))
            {
                return adult;
            }
            if (string.IsNullOrEmpty(adult))
            {
                return child;
            }
            return child + " → " + adult;
        }

        private static string BackstoryDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            BackstoryDef def = DefDatabase<BackstoryDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                // Prefer Def label; fall back to defName (title fields vary by version).
                if (!string.IsNullOrEmpty(def.label))
                {
                    return def.label;
                }
                string titled = def.TitleFor(Gender.Male);
                if (!string.IsNullOrEmpty(titled))
                {
                    return titled;
                }
            }
            return defName;
        }

        private void DrawPawnCombat(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            if (cachedDetailObject is PawnObject pawn && pawn.IsArchived)
            {
                DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.DeathDossier".Translate().ToString());
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.DeathDate".Translate().ToString(), FormatDate(pawn.DeathTick));
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.DeathCause".Translate().ToString(), CauseLabel(pawn.DeathCauseKey));
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Killer".Translate().ToString(),
                    string.IsNullOrEmpty(cachedDeathKiller) ? "PersonalChronicle.UI.UnknownDate".Translate().ToString() : cachedDeathKiller);
                y += 10f;
            }

            // v4.3: faction-codex cards (kills aggregated by faction).
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.FactionCodexTitle".Translate().ToString());
            y = DrawFactionCodex(rect, y, service);
        }

        private void DrawWeaponCombat(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.FactionCodexTitle".Translate().ToString());
            y = DrawFactionCodex(rect, y, service);
        }

        // ===== v4.3: faction-codex (each card = one faction; click to expand kills inline) =====

        private const float FactionCodexGap = 12f;
        /// <summary>v4.3: rows visible in the expanded detail viewport (inner scrollbar).</summary>
        private const int FactionCodexPreviewRows = 5;

        private float DrawFactionCodex(Rect rect, float y, IArchiveService service)
        {
            if (cachedFactionCodex == null || cachedFactionCodex.Count == 0)
            {
                // Restore font/colour before returning: RimWorld's Text.Font and GUI.color
                // are global IMGUI state, and leaking them corrupts every widget drawn after us.
                GameFont prevEmptyFont = Verse.Text.Font;
                Color prevEmptyColor = GUI.color;
                Verse.Text.Font = GameFont.Small;
                GUI.color = ArchiveUiStyle.Text;
                Widgets.Label(new Rect(rect.x, y, rect.width, FactionCodexEmptyRowHeight),
                    "PersonalChronicle.UI.NoKillRecords".Translate().ToString());
                GUI.color = prevEmptyColor;
                Verse.Text.Font = prevEmptyFont;
                return y + FactionCodexEmptyRowHeight + 4f;
            }

            int cols = rect.width >= FactionCodexTwoColumnWidth ? 2 : 1;
            float cardW = (rect.width - FactionCodexGap * (cols - 1)) / cols;
            int drawnInRow = 0;
            float rowStartY = y;
            float rowMaxH = 0f;

            for (int i = 0; i < cachedFactionCodex.Count; i++)
            {
                FactionCodexView card = cachedFactionCodex[i];
                bool expanded = expandedFactions.Contains(card.FactionKey);
                float cardH = FactionCodexCardHeight(card, expanded);
                float cardX = rect.x + drawnInRow * (cardW + FactionCodexGap);
                Rect cardRect = new Rect(cardX, y, cardW, cardH);
                DrawFactionCodexCard(cardRect, card, expanded, service);
                if (Widgets.ButtonInvisible(cardRect))
                {
                    if (expandedFactions.Contains(card.FactionKey))
                    {
                        expandedFactions.Remove(card.FactionKey);
                    }
                    else
                    {
                        expandedFactions.Add(card.FactionKey);
                    }
                }
                rowMaxH = Mathf.Max(rowMaxH, cardH);
                drawnInRow++;
                if (drawnInRow >= cols)
                {
                    y += rowMaxH + FactionCodexGap;
                    drawnInRow = 0;
                    rowMaxH = 0f;
                }
            }
            if (drawnInRow > 0)
            {
                y += rowMaxH;
            }
            return y;
        }

        /// <summary>
        /// Row pitch inside the expanded detail viewport (row height + separator).
        /// </summary>
        private const float FactionCodexRowPitch = TimelineRowHeight + 2f;

        /// <summary>
        /// Height of a codex card. MUST stay in sync with the layout in
        /// <see cref="DrawFactionCodexCard"/> — both derive from the same constants so the
        /// expanded viewport can never overflow the card background.
        /// Layout: padding → header → stats → gap → bar → padding [→ gap + viewport].
        /// </summary>
        private static float FactionCodexCardHeight(FactionCodexView card, bool expanded)
        {
            float h = FactionCodexPadding
                + FactionCodexHeaderHeight + UITheme.GridGap
                + FactionCodexStatHeight + UITheme.GridGap
                + FactionCodexBarHeight
                + FactionCodexPadding;
            if (expanded && card.MemberLines != null && card.MemberLines.Count > 0)
            {
                // Fixed 5-row viewport: no matter how many kills, the card keeps its
                // height and the rows scroll inside it.
                h += 8f + FactionCodexPreviewRows * FactionCodexRowPitch;
            }
            return h;
        }

        private void DrawFactionCodexCard(Rect rect, FactionCodexView card, bool expanded, IArchiveService service)
        {
            // Snapshot every piece of global IMGUI state we are about to touch,
            // and restore all three before returning (see the tail of this method).
            Color previous = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            TextAnchor prevAnchor = Text.Anchor;

            Color accent = ArchiveUiStyle.FactionAccent(card.Kind);
            ArchiveUiStyle.DrawCard(rect, accent);

            // Header: dot + name + kind badge + relation badge.
            float hx = rect.x + FactionCodexPadding;
            float hy = rect.y + FactionCodexPadding;
            Widgets.DrawBoxSolid(new Rect(hx, hy, FactionCodexDotSize, FactionCodexDotSize), accent);
            // Name column stops before the relation badge so long faction names
            // (or a long third-party mod label) can never overlap it.
            float nameX = hx + FactionCodexDotSize + 6f;
            float nameW = Mathf.Max(0f, rect.xMax - FactionCodexPadding - FactionCodexRelationWidth - 6f - nameX);
            Text.Anchor = TextAnchor.MiddleLeft;
            Verse.Text.Font = GameFont.Small;
            GUI.color = ArchiveUiStyle.Text;
            Widgets.Label(new Rect(nameX, hy - 1f, nameW, 18f), card.DisplayName);
            Verse.Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Muted;
            Widgets.Label(new Rect(nameX, hy + 15f, nameW, 16f), FactionKindLabel(card.Kind));
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = RelationColor(card.RelationKey);
            Widgets.Label(new Rect(rect.xMax - FactionCodexPadding - FactionCodexRelationWidth, hy, FactionCodexRelationWidth, 18f),
                RelationLabel(card.RelationKey));
            Text.Anchor = TextAnchor.UpperLeft;

            // 4 stat cells. statsY matches FactionCodexCardHeight: padding + header + gap.
            float statsY = rect.y + FactionCodexPadding + FactionCodexHeaderHeight + UITheme.GridGap;
            float innerW = rect.width - FactionCodexPadding * 2f;
            float statW = (innerW - FactionCodexGap * 3) / 4f;
            float statX = rect.x + FactionCodexPadding;
            DrawFactionStat(new Rect(statX, statsY, statW, FactionCodexStatHeight), card.KillCount.ToString(), "PersonalChronicle.UI.StatKills".Translate().ToString(), ArchiveUiStyle.Dead);
            DrawFactionStat(new Rect(statX + statW + FactionCodexGap, statsY, statW, FactionCodexStatHeight), card.RaidCount.ToString(), "PersonalChronicle.UI.StatRaids".Translate().ToString(), ArchiveUiStyle.TimelineBattle);
            DrawFactionStat(new Rect(statX + (statW + FactionCodexGap) * 2, statsY, statW, FactionCodexStatHeight), card.BattleCount.ToString(), "PersonalChronicle.UI.StatBattles".Translate().ToString(), ArchiveUiStyle.Info);
            // 4th cell: player card shows real losses; enemy cards cannot attribute
            // our losses from kill events (victim is the enemy), so show "—" to avoid a misleading 0.
            string lossText = card.Kind == ArchiveUiStyle.FactionCodexKind.Player
                ? card.OurLossCount.ToString()
                : "—";
            DrawFactionStat(new Rect(statX + (statW + FactionCodexGap) * 3, statsY, statW, FactionCodexStatHeight), lossText, "PersonalChronicle.UI.StatOurLosses".Translate().ToString(), ArchiveUiStyle.TimelineDeath);

            // Composition bar (victim-kind breakdown), segmented by proportion.
            float barY = statsY + FactionCodexStatHeight + UITheme.GridGap;
            Rect barRect = new Rect(statX, barY, innerW, FactionCodexBarHeight);
            Widgets.DrawBoxSolid(barRect, ArchiveUiStyle.BorderSoft);
            if (card.Composition != null && card.KillCount > 0)
            {
                float segX = barRect.x;
                for (int ci = 0; ci < card.Composition.Count; ci++)
                {
                    int cnt = card.Composition[ci].Value;
                    float segW = barRect.width * ((float)cnt / card.KillCount);
                    Color segColor = CompositionColor(ci);
                    Widgets.DrawBoxSolid(new Rect(segX, barY, Mathf.Max(0f, segW - 1f), FactionCodexBarHeight), segColor);
                    segX += segW;
                }
            }
            else
            {
                Widgets.DrawBoxSolid(barRect, accent);
            }

            // Expanded kill detail: fixed-height viewport with an inner scrollbar
            // (all rows scrollable inside the card; card height stays constant).
            if (expanded && card.MemberLines != null && card.MemberLines.Count > 0)
            {
                float dy = barY + FactionCodexBarHeight + UITheme.GridGap;
                float rowH = FactionCodexRowPitch;
                float viewH = FactionCodexPreviewRows * rowH;
                float contentH = card.MemberLines.Count * rowH;
                Rect viewRect = new Rect(statX, dy, innerW, viewH);
                // Reserve right edge for the scrollbar when content overflows.
                Rect contentRect = new Rect(0f, 0f, viewRect.width - (contentH > viewH ? FactionCodexScrollbarWidth : 0f), contentH);
                if (!expandedScroll.TryGetValue(card.FactionKey, out Vector2 scrollPos))
                {
                    scrollPos = Vector2.zero;
                }

                Widgets.BeginScrollView(viewRect, ref scrollPos, contentRect);
                try
                {
                    Verse.Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    for (int r = 0; r < card.MemberLines.Count; r++)
                    {
                        CombatLineView kv = card.MemberLines[r];
                        float rowY = r * rowH;
                        Rect row = new Rect(0f, rowY, contentRect.width, TimelineRowHeight);
                        // Title column is what remains after the date column and the weapon column.
                        float titleW = row.width - FactionCodexTitleColOffset - FactionCodexSubColWidth - 4f;
                        GUI.color = ArchiveUiStyle.Muted;
                        Widgets.Label(new Rect(row.x, row.y, FactionCodexDateColWidth, TimelineRowHeight), kv.DateText);
                        GUI.color = ArchiveUiStyle.Text;
                        Widgets.Label(new Rect(row.x + FactionCodexTitleColOffset, row.y, Mathf.Max(0f, titleW), TimelineRowHeight), kv.TitleText);
                        GUI.color = ArchiveUiStyle.Accent;
                        Widgets.Label(new Rect(row.xMax - FactionCodexSubColWidth, row.y, FactionCodexSubColWidth, TimelineRowHeight), kv.SubText);
                    }
                }
                finally
                {
                    // EndScrollView must always run: an escaped exception would leave
                    // Unity's GUI clip stack unbalanced and break every later window.
                    Widgets.EndScrollView();
                }
                expandedScroll[card.FactionKey] = scrollPos;
            }

            GUI.color = previous;
            Verse.Text.Font = prevFont;
            Text.Anchor = prevAnchor;
        }

        private static void DrawFactionStat(Rect rect, string number, string label, Color numColor)
        {
            // v4.5.4: reuse the shared StatCell so every KPI cell shares one
            // renderer and left-aligned rhythm (was a centred hand-rolled twin).
            UIComponents.StatCell(rect, label, number, numColor);
        }

        private static string FactionKindLabel(ArchiveUiStyle.FactionCodexKind kind)
        {
            switch (kind)
            {
                case ArchiveUiStyle.FactionCodexKind.Enemy: return "PersonalChronicle.UI.FactionKindEnemy".Translate().ToString();
                case ArchiveUiStyle.FactionCodexKind.Mechanoid: return "PersonalChronicle.UI.FactionKindMechanoid".Translate().ToString();
                case ArchiveUiStyle.FactionCodexKind.Animal: return "PersonalChronicle.UI.FactionKindAnimal".Translate().ToString();
                default: return "PersonalChronicle.UI.FactionKindUnknown".Translate().ToString();
            }
        }

        private static string RelationLabel(string relationKey)
        {
            if (relationKey == FactionRelationHostile) return "PersonalChronicle.UI.FactionRelHostile".Translate().ToString();
            if (relationKey == FactionRelationNeutral) return "PersonalChronicle.UI.FactionRelNeutral".Translate().ToString();
            if (relationKey == FactionRelationAlly) return "PersonalChronicle.UI.FactionRelAlly".Translate().ToString();
            return "PersonalChronicle.UI.FactionRelUnresolved".Translate().ToString();
        }

        private static readonly Color[] CompositionPalette = new Color[]
        {
            ArchiveUiStyle.TimelineBattle,
            ArchiveUiStyle.Info,
            ArchiveUiStyle.Alive,
            ArchiveUiStyle.Muted
        };

        private static Color CompositionColor(int index)
        {
            return CompositionPalette[index % CompositionPalette.Length];
        }

        private static Color RelationColor(string relationKey)
        {
            if (relationKey == FactionRelationHostile) return ArchiveUiStyle.Dead;
            if (relationKey == FactionRelationAlly) return ArchiveUiStyle.Alive;
            return ArchiveUiStyle.Muted;
        }

        private float DrawCombatLineList(Rect rect, float y, IArchiveService service, List<CombatLineView> lines, string emptyKey)
        {
            if (lines == null || lines.Count == 0)
            {
                GameFont prevFont = Verse.Text.Font;
                Verse.Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, FactionCodexEmptyRowHeight), emptyKey.Translate().ToString());
                Verse.Text.Font = prevFont;
                return y + FactionCodexEmptyRowHeight + 4f;
            }
            for (int i = 0; i < lines.Count; i++)
            {
                CombatLineView line = lines[i];
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                DrawEventRow(row, line.DateText, line.TitleText, line.SubText);
                if ((line.Target != NavTarget.None || line.TargetEvent != null) && Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, line.Target, line.StableId, line.TargetEvent);
                }
                y += TimelineRowHeight;
            }
            return y;
        }

        private void DrawWeaponCraft(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            if (!string.IsNullOrEmpty(cachedCraftCrafterId))
            {
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Crafter".Translate().ToString(),
                    string.IsNullOrEmpty(cachedCraftCrafterLabel) ? cachedCraftCrafterId : cachedCraftCrafterLabel);
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.CraftedAt".Translate().ToString(), FormatDate(cachedCraftTick));
                y += 10f;
            }
            else
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoCraftRecord".Translate().ToString());
                y += 28f;
            }

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Timeline".Translate().ToString());
            for (int i = 0; i < cachedDetailEvents.Count; i++)
            {
                EventLineView line = cachedDetailEvents[i];
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                DrawEventRow(row, line.DateText, line.NameText, line.ParamsText);
                y += TimelineRowHeight;
            }
        }

        private void DrawPawnItems(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.HeldItems".Translate().ToString());

            int count = 0;
            for (int i = 0; i < cachedLinkedObjects.Count; i++)
            {
                LinkedObjectView link = cachedLinkedObjects[i];
                if (link.CategoryKey != ArchiveCategoryKeys.Thing)
                {
                    continue;
                }
                count++;
                Rect row = new Rect(rect.x, y, rect.width, RowHeight - 4f);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x + 6f, row.y + 4f, row.width - 200f, 22f), link.Label);
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.SecondaryText;
                Widgets.Label(new Rect(row.x + row.width - 196f, row.y + 6f, 190f, 18f), link.CategoryLabel);
                GUI.color = prevColor;
                Text.Font = GameFont.Small;
                if (link.Target != NavTarget.None && Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, link.Target, link.StableId, null);
                }
                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
                y += RowHeight + 2f;
            }

            if (count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoRelated".Translate().ToString());
            }
        }

        // P3-2: Pawn/Weapon stat panels were identical — merged into one.
        private void DrawDetailStats(Rect rect)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Stats".Translate().ToString());

            DrawStatCell(new Rect(rect.x, y, 150f, 60f), "PersonalChronicle.UI.EventCount".Translate().ToString(), cachedDetailEvents.Count);
            DrawStatCell(new Rect(rect.x + 160f, y, 150f, 60f), "PersonalChronicle.UI.LinkedCount".Translate().ToString(), cachedLinkedObjects.Count);
            y += 70f;

            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.FirstEvent".Translate().ToString(),
                cachedDetailEvents.Count > 0 ? cachedDetailEvents[0].DateText : "PersonalChronicle.UI.UnknownDate".Translate().ToString());
            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.LastEvent".Translate().ToString(),
                cachedDetailEvents.Count > 0 ? cachedDetailEvents[cachedDetailEvents.Count - 1].DateText : "PersonalChronicle.UI.UnknownDate".Translate().ToString());
        }

        private void DrawWeaponOverview(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            if (!(cachedDetailObject is ThingObject thing))
            {
                return;
            }

            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Type".Translate().ToString(), ThingDefLabel(thing.ThingDefName));
            if (!string.IsNullOrEmpty(cachedCraftCrafterId))
            {
                string crafter = string.IsNullOrEmpty(cachedCraftCrafterLabel) ? cachedCraftCrafterId : cachedCraftCrafterLabel;
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Crafter".Translate().ToString(), crafter);
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.CraftedAt".Translate().ToString(), FormatDate(cachedCraftTick));
            }
            else
            {
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Crafter".Translate().ToString(),
                    "PersonalChronicle.UI.NoCraftRecord".Translate().ToString());
            }
            y += 10f;

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Timeline".Translate().ToString());
            int start = Mathf.Max(0, cachedDetailEvents.Count - 4);
            for (int i = start; i < cachedDetailEvents.Count; i++)
            {
                EventLineView line = cachedDetailEvents[i];
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                DrawEventRow(row, line.DateText, line.NameText, line.ParamsText);
                y += TimelineRowHeight;
            }
            if (cachedDetailEvents.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoEvents".Translate().ToString());
            }
        }

        // ---- Detail: live-read tabs (Skills / Health / Relations) --------------

        private void DrawSkills(Rect rect, IArchiveService service)
        {
            Pawn pawn = service.GetLivePawn(detailObjectId);
            if (pawn == null || pawn.skills == null || pawn.skills.skills == null)
            {
                DrawNoLiveData(rect);
                return;
            }

            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Skills".Translate().ToString());

            List<SkillRecord> skills = pawn.skills.skills;
            for (int i = 0; i < skills.Count; i++)
            {
                SkillRecord skill = skills[i];
                if (skill == null || skill.def == null)
                {
                    continue;
                }
                Rect row = new Rect(rect.x, y, rect.width, 24f);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x, row.y, 140f, 20f), skill.def.label);

                Rect bar = new Rect(row.x + 148f, row.y + 4f, row.width - 148f - 56f, 14f);
                Widgets.FillableBar(bar, Mathf.Clamp01(skill.Level / 20f));

                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x + row.width - 52f, row.y, 48f, 20f), skill.Level.ToString());
                y += 28f;
            }
        }

        private void DrawHealth(Rect rect, IArchiveService service)
        {
            Pawn pawn = service.GetLivePawn(detailObjectId);
            if (pawn == null || pawn.health == null)
            {
                DrawNoLiveData(rect);
                return;
            }

            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.OverallHealth".Translate().ToString());

            float summary = pawn.health.summaryHealth != null ? pawn.health.summaryHealth.SummaryHealthPercent : 1f;
            string healthLabel;
            if (summary > HealthGoodThreshold)
            {
                healthLabel = "PersonalChronicle.UI.HealthGood".Translate().ToString();
            }
            else if (summary > HealthInjuredThreshold)
            {
                healthLabel = "PersonalChronicle.UI.HealthInjured".Translate().ToString();
            }
            else
            {
                healthLabel = "PersonalChronicle.UI.HealthCritical".Translate().ToString();
            }
            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Status".Translate().ToString(), healthLabel);
            y += 8f;

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Hediffs".Translate().ToString());

            List<Hediff> hediffs = pawn.health.hediffSet != null ? pawn.health.hediffSet.hediffs : null;
            if (hediffs == null || hediffs.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoHediffs".Translate().ToString());
                return;
            }

            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff == null)
                {
                    continue;
                }
                string label = hediff.LabelBase;
                if (string.IsNullOrEmpty(label) && hediff.def != null)
                {
                    label = hediff.def.label;
                }
                if (string.IsNullOrEmpty(label))
                {
                    continue;
                }
                string part = string.Empty;
                if (hediff.Part != null && hediff.Part.def != null)
                {
                    part = hediff.Part.def.label;
                }
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x + 4f, row.y + 3f, row.width - 200f, 20f), label);
                Text.Font = GameFont.Tiny;
                GUI.color = UITheme.SecondaryText;
                Widgets.Label(new Rect(row.x + row.width - 196f, row.y + 6f, 192f, 18f), part);
                GUI.color = prevColor;
                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
                y += TimelineRowHeight;
            }
        }

        private void DrawRelations(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Relations".Translate().ToString());

            List<RelationRowView> rows = BuildRelationRows(service);
            if (rows.Count == 0)
            {
                UIComponents.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoRelations".Translate(), UITheme.FontBody, UITheme.Text);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                RelationRowView row = rows[i];
                Rect rowRect = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                UIComponents.Label(new Rect(rowRect.x + 4f, rowRect.y + 3f, rowRect.width - 260f, 20f),
                    row.OtherLabel, UITheme.FontBody, UITheme.Text);
                UIComponents.Label(new Rect(rowRect.x + rowRect.width - 256f, rowRect.y + 6f, 180f, 18f),
                    row.RelationLabel, UITheme.FontLabel, UITheme.SecondaryText);
                UIComponents.Label(new Rect(rowRect.x + rowRect.width - 72f, rowRect.y + 6f, 68f, 18f),
                    row.StatusLabel, UITheme.FontLabel, UITheme.SecondaryText);
                Widgets.DrawLineHorizontal(rowRect.x, rowRect.yMax, rowRect.width);
                y += TimelineRowHeight;
            }
        }

        private struct RelationRowView
        {
            public string OtherLabel;
            public string RelationLabel;
            public string StatusLabel;
        }

        /// <summary>
        /// v4.6: merge live direct relations with archived snapshots so the Social
        /// tab shows initial ties (spouse/parent/friend at join time) even for dead
        /// or departed pawns. Live state wins when both sources describe the same pair.
        /// </summary>
        private List<RelationRowView> BuildRelationRows(IArchiveService service)
        {
            List<RelationRowView> rows = new List<RelationRowView>();
            HashSet<string> seen = new HashSet<string>();
            PawnObject record = service.GetObject(detailObjectId) as PawnObject;
            Pawn livePawn = service.GetLivePawn(detailObjectId);

            // 1) Live relations (current state).
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
                    rows.Add(new RelationRowView
                    {
                        OtherLabel = rel.otherPawn.LabelShort,
                        RelationLabel = RelationLabelFor(rel.def, rel.otherPawn),
                        StatusLabel = rel.otherPawn.Dead
                            ? "PersonalChronicle.UI.Dead".Translate().ToString()
                            : "PersonalChronicle.UI.Alive".Translate().ToString()
                    });
                }
            }

            // 2) Archived relations (historical / initial ties, includes dead/departed pawns).
            if (record?.Relations != null)
            {
                for (int i = 0; i < record.Relations.Count; i++)
                {
                    SignificantRelation rel = record.Relations[i];
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
                    rows.Add(new RelationRowView
                    {
                        OtherLabel = !string.IsNullOrEmpty(rel.OtherLabel) ? rel.OtherLabel : rel.OtherStableId,
                        RelationLabel = RelationLabelFor(def, otherLive),
                        StatusLabel = status
                    });
                }
            }

            return rows;
        }

        private static string MakeRelationKey(string relationDefName, string otherStableId)
        {
            return (relationDefName ?? string.Empty) + "::" + (otherStableId ?? string.Empty);
        }

        private static string RelationLabelFor(PawnRelationDef def, Pawn otherPawn)
        {
            if (def == null)
            {
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

        private void DrawNoLiveData(Rect rect)
        {
            Text.Font = GameFont.Small;
            GUI.color = UITheme.SecondaryText;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f),
                "PersonalChronicle.UI.NoLiveData".Translate().ToString());
            GUI.color = prevColor;
        }

        // ---- Detail: production tab (LD-1) -----------------------------------

        private void DrawProductionTab(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Tab.Production".Translate().ToString());

            if (cachedProductionLines.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoProduction".Translate().ToString());
                return;
            }

            // Column headers (all translated; no hardcoded copy).
            Text.Font = GameFont.Tiny;
            GUI.color = UITheme.SecondaryText;
            Widgets.Label(new Rect(rect.x + 6f, y, rect.width - 300f, 18f),
                "PersonalChronicle.UI.ProductionType".Translate().ToString());
            Widgets.Label(new Rect(rect.x + rect.width - 280f, y, 90f, 18f),
                "PersonalChronicle.UI.ProductionCount".Translate().ToString());
            Widgets.Label(new Rect(rect.x + rect.width - 190f, y, 180f, 18f),
                "PersonalChronicle.UI.ProductionLastTime".Translate().ToString());
            GUI.color = prevColor;
            y += 22f;

            for (int i = 0; i < cachedProductionLines.Count; i++)
            {
                ReadModels.ProductionLineView line = cachedProductionLines[i];
                Rect row = new Rect(rect.x, y, rect.width, RowHeight - 4f);

                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x + 6f, row.y + 4f, row.width - 300f, 22f), line.Label);

                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(row.x + row.width - 280f, row.y + 6f, 90f, 18f), line.Count.ToString());
                Widgets.Label(new Rect(row.x + row.width - 190f, row.y + 6f, 180f, 18f), FormatDate(line.LastTick));

                // Click → jump to the thing's detail (Weapon/Thing nav target).
                if (Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, NavTarget.Weapon, line.StableId, null);
                }
                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
                y += RowHeight + 2f;
            }
        }

        // ---- Detail: live tabs (LD-2/3/4) ------------------------------------

        /// <summary>
        /// v4.0 Career: intensity summary + work ledger + production + skill archive.
        /// </summary>
        private void DrawCareerTab(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            DrawWorkIntensityHeader(rect, ref y, service);
            DrawWorkIntensityCards(rect, ref y);

            // Production summary: aggregate cards first, detailed type cards
            // second. Routine craft events are no longer the only source of
            // career totals.
            y += 8f;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Tab.Production".Translate().ToString());
            if (cachedProductionSummary == null || cachedProductionSummary.Types.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoProduction".Translate().ToString());
                y += 28f;
            }
            else
            {
                IReadOnlyList<ProductionTypeView> types = cachedProductionSummary.Types;
                float summaryGap = 8f;
                float summaryWidth = (rect.width - summaryGap) / 2f;
                DrawMetricCard(new Rect(rect.x, y, summaryWidth, 72f),
                    "PersonalChronicle.UI.ProductionValue".Translate().ToString(),
                    FormatMarketValue(cachedProductionSummary.TotalMarketValue),
                    "PersonalChronicle.UI.ProductionQuantity".Translate(
                        cachedProductionSummary.TotalQuantity).ToString());
                DrawMetricCard(new Rect(rect.x + summaryWidth + summaryGap, y, summaryWidth, 72f),
                    "PersonalChronicle.UI.ProductionCount".Translate().ToString(),
                    cachedProductionSummary.TotalQuantity.ToString(),
                    "PersonalChronicle.UI.ProductionTypeCount".Translate(types.Count).ToString());
                y += 86f;
                for (int i = 0; i < types.Count; i++)
                {
                    ProductionTypeView type = types[i];
                    float width = (rect.width - UITheme.GridGap) / 2f;
                    float x = rect.x + (i % 2) * (width + UITheme.GridGap);
                    float cardY = y + (i / 2) * 58f;
                    ArchiveUiStyle.DrawCard(new Rect(x, cardY, width, 50f), ArchiveUiStyle.Accent);
                    Text.Font = GameFont.Small;
                    Widgets.Label(new Rect(x + UITheme.CardPadX, cardY + 6f, width - UITheme.CardPadX * 2f, 20f), ProductionDefLabel(type.DefName));
                    Text.Font = GameFont.Tiny;
                    GUI.color = ArchiveUiStyle.Muted;
                    Widgets.Label(new Rect(x + UITheme.CardPadX, cardY + 28f, width - UITheme.CardPadX * 2f, 16f),
                        "PersonalChronicle.UI.ProductionCard".Translate(type.Quantity, FormatMarketValue(type.MarketValue)).ToString());
                    GUI.color = prevColor;
                }
                y += ((types.Count + 1) / 2) * 58f;
            }
        }

        private void DrawWorkIntensityHeader(Rect rect, ref float y, IArchiveService service)
        {
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.CareerSummary".Translate().ToString());
            DrawWorkIntensityHero(rect, y, rect.width, service);
            y += 92f + 8f;

            WorkIntensityView intensity = cachedWorkIntensity;
            bool hasTier = intensity != null && intensity.IsDefined;
            IWorkIntensityService intensityService = service as IWorkIntensityService;
            if (intensityService == null)
            {
                return;
            }
            IReadOnlyList<WorkIntensityTierView> tiers = cachedTiers;
            if (tiers == null || tiers.Count == 0)
            {
                return;
            }
            float rungWidth = (rect.width - (tiers.Count - 1) * 2f) / tiers.Count;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;
            try
            {
                for (int i = 0; i < tiers.Count; i++)
                {
                    WorkIntensityTierView tier = tiers[i];
                    Rect rung = new Rect(rect.x + i * (rungWidth + 2f), y, rungWidth, 28f);
                    Color color = ParseIntensityColor(tier.ColorHex);
                    bool current = intensity != null && intensity.IsDefined
                        && intensity.TierDefName == tier.DefName;
                    Widgets.DrawBoxSolid(rung, current ? color : UITheme.WithAlpha(color, 0.28f));
                    if (current)
                    {
                        ArchiveUiStyle.DrawBorder(rung, ArchiveUiStyle.Text);
                    }
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = current ? ArchiveUiStyle.Text : ArchiveUiStyle.Muted;
                    Widgets.Label(rung, tier.DisplayCode ?? tier.DefName);
                    GUI.color = prevColor;
                    Text.Anchor = TextAnchor.UpperLeft;
                    TooltipHandler.TipRegion(rung,
                        TranslateIntensityKey(tier.LabelKey, tier.DisplayCode ?? tier.DefName));
                }
            }
            finally
            {
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
                GUI.color = prevColor;
            }
            y += 36f;
        }

        /// <summary>
        /// Shared intensity hero card used by both Overview (v4.5.2) and Career tab.
        /// </summary>
        private void DrawWorkIntensityHero(Rect rect, float y, float width, IArchiveService service)
        {
            WorkIntensityView intensity = cachedWorkIntensity;
            bool hasTier = intensity != null && intensity.IsDefined;
            bool isEstimated = hasTier && intensity.IsEstimated;
            Color tierColor = ParseIntensityColor(intensity != null ? intensity.ColorHex : null);
            Rect hero = new Rect(rect.x, y, width, 92f);
            ArchiveUiStyle.DrawPanel(hero, ArchiveUiStyle.PanelRaised);

            string statusKey = isEstimated
                ? "PersonalChronicle.UI.Intensity.Estimated"
                : "PersonalChronicle.UI.Intensity.Actual";
            string badgeCode = hasTier
                ? TranslateIntensityKey(intensity.DisplayCode,
                    "PersonalChronicle.UI.NotAvailable".Translate().ToString())
                : "PersonalChronicle.UI.Intensity.SampleInsufficient".Translate().ToString();
            string badgeStatus = hasTier
                ? statusKey.Translate().ToString()
                : string.Empty;
            Rect badge = new Rect(hero.x + 8f, hero.y + 8f, 170f, hero.height - 16f);
            ArchiveUiStyle.DrawBadge(badge, string.Empty, hasTier ? tierColor : ArchiveUiStyle.Muted);
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;
            try
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(badge.x + 8f, badge.y + 14f, badge.width - 16f, 26f),
                    hasTier && !string.IsNullOrEmpty(intensity.LabelKey)
                        ? TranslateIntensityKey(intensity.LabelKey, intensity.DisplayCode)
                        : "PersonalChronicle.UI.Intensity.SampleInsufficient".Translate().ToString());
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Text.Anchor = TextAnchor.MiddleCenter;
                string badgeSubtitle = hasTier
                    ? badgeStatus + " · " + badgeCode
                    : "PersonalChronicle.UI.Intensity.ObservedWindow".Translate(
                        FormatDays(intensity != null ? intensity.ObservedDays : 0d)).ToString();
                Widgets.Label(new Rect(badge.x + 8f, badge.y + 44f, badge.width - 16f, 20f), badgeSubtitle);
                GUI.color = prevColor;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            finally
            {
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
                GUI.color = prevColor;
            }

            float gap = 8f;
            float statsX = badge.xMax + 10f;
            float statsWidth = hero.xMax - statsX - 8f;
            float cellWidth = (statsWidth - gap * 2f) / 3f;
            DrawMetricCard(new Rect(statsX, hero.y + 8f, cellWidth, 84f),
                "PersonalChronicle.UI.TotalWorkHours".Translate().ToString(),
                FormatHours(intensity != null ? intensity.TotalHours : 0d),
                "PersonalChronicle.UI.Intensity.ObservedWindow".Translate(
                    FormatDays(intensity != null ? intensity.ObservedDays : 0d)).ToString());
            DrawMetricCard(new Rect(statsX + cellWidth + gap, hero.y + 8f, cellWidth, 84f),
                "PersonalChronicle.UI.Intensity.Daily".Translate().ToString(),
                FormatHours(intensity != null ? intensity.DailyHours : 0d),
                "PersonalChronicle.UI.Intensity.MonthlyEst".Translate(
                    FormatHours(intensity != null ? intensity.MonthlyHours : 0d)).ToString());
            DrawMetricCard(new Rect(statsX + 2f * (cellWidth + gap), hero.y + 8f, cellWidth, 84f),
                "PersonalChronicle.UI.Intensity.Weekly".Translate().ToString(),
                FormatHours(intensity != null ? intensity.WeeklyHours : 0d),
                BuildIntensityRelativeLabel(intensity));
        }

        // ---- 健康残值 · 资产折旧 (window renders only; derivation in ReadModels) ----

        private float DrawHealthValuation(Rect rect, float y, ReadModels.HealthView h)
        {
            // v4.6.1: full HTML-style layout — 4 StatCells + 3 dim bars + event log + verdict.
            float headH = 26f;
            float statRowH = 80f;
            float dimRowH = 16f;
            float dimBlockH = dimRowH * 3f + 8f;
            float eventHeaderH = 18f;
            int evCount = h.Events != null ? Mathf.Min(h.Events.Count, 6) : 0;
            // v4.6.3: 事件行高 22f 适配中文 GameFont.Tiny。
            float eventsH = evCount > 0 ? (evCount * 22f + 4f) : 18f;
            float blockH = headH + statRowH + 8f + dimBlockH + 8f + eventHeaderH + eventsH + 6f;
            Rect block = new Rect(rect.x, y, rect.width, blockH);

            UIComponents.DrawSubsectionHeader(block.TopPartPixels(headH),
                "PersonalChronicle.UI.HealthValuation.Title");

            if (!h.IsDefined)
            {
                Rect empty = new Rect(block.x + UITheme.CardPadX, block.y + headH + UITheme.GridGap, block.width - UITheme.CardPadX * 2f, 28f);
                Color prevColor = GUI.color;
                GameFont prevFont = Text.Font;
                TextAnchor prevAnchor = Text.Anchor;
                GUI.color = UITheme.Dim;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.Label(empty, "PersonalChronicle.UI.HealthValuation.NoData".Translate().ToString());
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
                return block.yMax;
            }

            Color accent = h.IsImpaired ? UITheme.Blood : UITheme.Alive;

            // === Row 1: 4 StatCells (silver value / composite score / body% / weekly yield) ===
            float statX = block.x + UITheme.CardPadX;
            float statGap = UITheme.GridGap;
            float statW = (block.width - UITheme.CardPadX * 2f - statGap * 3f) / 4f;
            float statY = block.y + headH + 6f;
            UIComponents.StatCell(new Rect(statX, statY, statW, statRowH),
                "PersonalChronicle.UI.HealthValuation.SilverValue".Translate().ToString(),
                FormatSilver(h.SilverValue),
                h.IsImpaired ? UITheme.Blood : UITheme.PillGold,
                "PersonalChronicle.UI.HealthValuation.BaseValue".Translate(FormatSilver(h.BaseSilverValue)).ToString());
            UIComponents.StatCell(new Rect(statX + (statW + statGap), statY, statW, statRowH),
                "PersonalChronicle.UI.HealthValuation.Score".Translate().ToString(),
                Mathf.RoundToInt(h.HealthScore).ToString(),
                accent);
            UIComponents.StatCell(new Rect(statX + 2f * (statW + statGap), statY, statW, statRowH),
                "PersonalChronicle.UI.HealthValuation.Body".Translate().ToString(),
                Mathf.RoundToInt(h.BodyPercent * 100f).ToString() + "%",
                accent);
            UIComponents.StatCell(new Rect(statX + 3f * (statW + statGap), statY, statW, statRowH),
                "PersonalChronicle.UI.HealthValuation.WeeklyYield".Translate().ToString(),
                FormatSilver(h.WeeklySilverEstimate),
                UITheme.PillGold,
                "PersonalChronicle.UI.Ledger.WeeklyUnit".Translate().ToString(),
                inlineSubLabel: true);

            // === Row 2: 3 dim bars (Body / Spirit / Youth) ===
            float dimY = statY + statRowH + 8f;
            DrawHealthDimBar(new Rect(statX, dimY, block.width - UITheme.CardPadX * 2f, dimRowH),
                "PersonalChronicle.UI.HealthValuation.Dim.Body".Translate().ToString(),
                h.BodyIntegrityScore, h.BodyFactors, "PersonalChronicle.UI.HealthValuation.Dim.Body");
            DrawHealthDimBar(new Rect(statX, dimY + dimRowH, block.width - UITheme.CardPadX * 2f, dimRowH),
                "PersonalChronicle.UI.HealthValuation.Dim.Spirit".Translate().ToString(),
                h.SpiritScore, h.SpiritFactors, "PersonalChronicle.UI.HealthValuation.Dim.Spirit");
            DrawHealthDimBar(new Rect(statX, dimY + 2f * dimRowH, block.width - UITheme.CardPadX * 2f, dimRowH),
                "PersonalChronicle.UI.HealthValuation.Dim.Youth".Translate().ToString(),
                h.YouthScore, h.YouthFactors, "PersonalChronicle.UI.HealthValuation.Dim.Youth");

            // === Row 3: depreciation event log ===
            float evY = dimY + dimBlockH + 4f;
            UIComponents.Label(new Rect(statX, evY, block.width - UITheme.CardPadX * 2f, eventHeaderH),
                "PersonalChronicle.UI.HealthValuation.TipHeader".Translate().ToString(),
                GameFont.Tiny, UITheme.SecondaryText);
            if (evCount == 0)
            {
                UIComponents.Label(new Rect(statX, evY + eventHeaderH, block.width - UITheme.CardPadX * 2f, 18f),
                    "PersonalChronicle.UI.HealthValuation.NoEvents".Translate().ToString(),
                    GameFont.Tiny, UITheme.Dim);
            }
            else
            {
                // 中文 GameFont.Tiny 行高 ≈ 22f，用 22f 避免字体重叠截断。
                const float lineH = 22f;
                for (int i = 0; i < evCount; i++)
                {
                    ReadModels.HealthEventView e = h.Events[i];
                    if (e == null) continue;
                    string impact = e.Impact == 0 ? "" : ("  " + FormatSilver(e.Impact) + " 银");
                    string tag = string.IsNullOrEmpty(e.TagText) ? "" : ("  [" + e.TagText + "]");
                    string line = e.DateText + "  " + e.Description + impact + tag;
                    UIComponents.Label(new Rect(statX, evY + eventHeaderH + i * lineH,
                        block.width - UITheme.CardPadX * 2f, lineH),
                        line,
                        GameFont.Tiny,
                        e.Impact < 0 ? UITheme.Blood : UITheme.Muted,
                        TextAnchor.MiddleLeft);
                }
            }

            // Per-element hover tips are handled by DrawHealthDimBar (factors) and
            // the event rows below. Do NOT register a whole-block tooltip here, or
            // it swallows the per-dimension tooltips.
            return block.yMax;
        }

        private static void DrawHealthDimBar(Rect rect, string label, float score01to100,
            IReadOnlyList<ReadModels.HealthFactorView> factors, string dimKey)
        {
            // v4.6.5: fixed-size progress bar matching the output-ledger layout:
            // label | bar | value area | pct. This prevents the health bar from
            // stretching across the whole row.
            float labelW = 80f;
            float pctW = 50f;
            float barX = rect.x + labelW;
            float barW = rect.width - labelW - pctW - UITheme.ProgressbarValueW;
            UIComponents.Label(new Rect(rect.x, rect.y, labelW, rect.height),
                label,
                GameFont.Tiny, UITheme.SecondaryText,
                TextAnchor.MiddleLeft);
            float barH = UITheme.ProgressbarH;
            float share = Mathf.Clamp01(score01to100 / 100f);
            bool low = share < 0.3f;
            Color fill = low ? UITheme.Blood : UITheme.PillGold;
            Rect bar = new Rect(barX, rect.y + (rect.height - barH) / 2f, barW, barH);
            UIComponents.ProgressBar(bar, share, fill);
            UIComponents.Label(new Rect(rect.x + rect.width - pctW, rect.y, pctW, rect.height),
                Mathf.RoundToInt(score01to100) + "%",
                GameFont.Tiny, low ? UITheme.Blood : UITheme.Text,
                TextAnchor.MiddleRight);

            // Per-dimension hover tip listing all positive/negative factors.
            if (factors != null && factors.Count > 0)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine(label);
                for (int i = 0; i < factors.Count; i++)
                {
                    ReadModels.HealthFactorView f = factors[i];
                    if (f == null) continue;
                    string tag = f.IsPositive ? "✓" : "✗";
                    string impact = f.Impact == 0 ? "" : (" (" + (f.Impact > 0 ? "+" : "") + f.Impact + ")");
                    sb.AppendLine("  " + tag + " " + f.LabelText + impact);
                }
                TooltipHandler.TipRegion(rect, sb.ToString().TrimEnd());
            }
        }

        // ---- 榨取总账 / 产出核销 (contribution-archive layout, v4.6.1) ----

        /// <summary>
        /// v4.6.2: Cover header (matches contribution-archive-overview.html .cover block).
        /// Portrait + name + role description + in-service stamp + one-line verdict.
        /// All visual goes through UITheme tokens; portrait is drawn via the native
        /// PortraitsCache (3D colonist render). Falls back to a placeholder box if
        /// no live pawn is resolvable.
        /// </summary>
        private float DrawCoverHeader(Rect rect, float y, PawnObject pawn, IArchiveService service)
        {
            float portraitW = 60f;
            float portraitH = 75f;
            float portraitPad = 4f;
            float rowH = portraitH + 8f; // includes vertical breathing room

            // Tier medal inline row (.tier-inline in the design spec) grows the
            // header when a confirmed/estimated tier exists.
            WorkIntensityView intensity = cachedWorkIntensity;
            float tierRowH = (intensity != null && intensity.IsDefined) ? 20f : 0f;
            rowH += tierRowH;

            Rect portraitRect = new Rect(rect.x, y, portraitW, portraitH);
            Rect infoRect = new Rect(
                portraitRect.xMax + portraitPad + 6f,
                y,
                rect.width - portraitW - portraitPad - 6f,
                rowH);

            // ---- Portrait ----
            Pawn livePawn = service != null ? service.GetLivePawn(pawn.StableId) : null;
            DrawPortraitOrPlaceholder(portraitRect, livePawn);

            // ---- Tier medal inline (.tier-inline in spec) ----
            // Drawn at the top of the info column; the name/role/days block shifts down
            // by tierRowH so they never overlap the medal row.
            if (tierRowH > 0f)
            {
                DrawTierMedalInline(new Rect(infoRect.x, infoRect.y, infoRect.width, tierRowH), intensity);
            }

            // ---- Name + role description ----
            float contentY = infoRect.y + tierRowH;
            UIComponents.Label(new Rect(infoRect.x, contentY + 2f, infoRect.width, 26f),
                ObjectDisplayLabel(pawn),
                GameFont.Medium, UITheme.Text,
                TextAnchor.UpperLeft);

            string roleDesc = BuildCoverRoleDescription(pawn);
            UIComponents.Label(new Rect(infoRect.x, contentY + 30f, infoRect.width, 18f),
                roleDesc,
                GameFont.Tiny, UITheme.SecondaryText,
                TextAnchor.UpperLeft);

            string daysText = BuildCoverDaysText(pawn);
            UIComponents.Label(new Rect(infoRect.x, contentY + 50f, infoRect.width, 18f),
                daysText,
                GameFont.Tiny, UITheme.Muted,
                TextAnchor.UpperLeft);

            // ---- In-service days text + stamp on the same row ----
            // v4.6.5: stamp is placed to the right of "在册 X 日" instead of the
            // bottom row, preventing overlap with the days line.
            bool alive = !pawn.IsArchived;
            string stampKey = alive
                ? "PersonalChronicle.UI.Cover.StampAlive"
                : "PersonalChronicle.UI.Cover.StampDead";
            string stampText = stampKey.Translate().ToString();
            float stampW = Mathf.Max(60f, Text.CalcSize(stampText).x + 18f);
            float stampH = 20f;

            float daysY = contentY + 50f;
            Vector2 daysSize = Text.CalcSize(daysText);
            Rect daysRect = new Rect(infoRect.x, daysY, daysSize.x + 4f, 18f);
            UIComponents.Label(daysRect, daysText,
                GameFont.Tiny, UITheme.Muted,
                TextAnchor.MiddleLeft);

            Rect stampRect = new Rect(
                daysRect.xMax + 6f,
                daysY + (18f - stampH) / 2f,
                stampW,
                stampH);
            UIComponents.Pill(stampRect, stampText, alive ? UITheme.Alive : UITheme.Dead);

            // ---- Incomplete badge (mid-install: JoinTick=-1) ----
            if (pawn.JoinTick < 0L)
            {
                string incomplete = "PersonalChronicle.UI.Cover.Incomplete".Translate().ToString();
                float badgeW = Mathf.Min(infoRect.width, Text.CalcSize(incomplete).x + 16f);
                Rect badgeRect = new Rect(infoRect.x, daysY - 22f, badgeW, 18f);
                DrawIncompleteBadge(badgeRect, incomplete);
            }

            return y + rowH;
        }

        private static void DrawPortraitOrPlaceholder(Rect rect, Pawn livePawn)
        {
            // Native portrait frame (blood-tinted border, dark fill, dim label when missing).
            Color prevColor = GUI.color;
            Color prevFontColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                Widgets.DrawBoxSolid(rect, UITheme.Panel);
                Widgets.DrawBox(rect);

                bool hasPortrait = false;
                if (livePawn != null)
                {
                    // Use the native PortraitsCache (RimWorld 1.6). Catch any null
                    // return / RenderTexture issue by falling through to placeholder.
                    RenderTexture portrait = PortraitsCache.Get(
                        livePawn, new Vector2(rect.width, rect.height), Rot4.South);
                    if (portrait != null)
                    {
                        GUI.DrawTexture(rect, portrait);
                        hasPortrait = true;
                    }
                }
                // Placeholder caption ("人物画像 / PortraitsCache") shows ONLY when no
                // live pawn / render is available — never over a real 3D portrait.
                if (!hasPortrait)
                {
                    string label = "PersonalChronicle.UI.Cover.PortraitLabel".Translate().ToString();
                    string sub = "PersonalChronicle.UI.Cover.PortraitLabelSub".Translate().ToString();
                    GUI.color = UITheme.Dim;
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(rect, label);
                    Rect subRect = new Rect(rect.x, rect.yMax - 14f, rect.width, 12f);
                    Widgets.Label(subRect, sub);
                }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        private static void DrawIncompleteBadge(Rect rect, string label)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                GUI.color = UITheme.BadgeIncompleteFill;
                Widgets.DrawBoxSolid(rect, UITheme.BadgeIncompleteFill);
                GUI.color = UITheme.Blood;
                Widgets.DrawBox(rect);
                GUI.color = UITheme.Blood;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, label);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        /// <summary>
        /// v4.6.4: tier medal inline row (.tier-inline in the design spec).
        /// Renders a square medal (tier display code on tier-coloured fill) beside
        /// the tier title, an "预估/实际" tag and the projected daily hours. Never
        /// called when the tier is undefined — the row collapses to zero height.
        /// </summary>
        private static void DrawTierMedalInline(Rect rect, WorkIntensityView intensity)
        {
            if (intensity == null || !intensity.IsDefined)
            {
                return;
            }
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                // ---- Medal square (tier colour fill + display code) ----
                float medal = Mathf.Min(rect.height, 18f);
                Rect medalRect = new Rect(rect.x, rect.y + (rect.height - medal) / 2f, medal, medal);
                Color tierColor = UITheme.Accent;
                if (!string.IsNullOrEmpty(intensity.ColorHex)
                    && ColorUtility.TryParseHtmlString(intensity.ColorHex, out Color parsed))
                {
                    tierColor = parsed;
                }
                GUI.color = tierColor;
                Widgets.DrawBoxSolid(medalRect, tierColor);
                GUI.color = UITheme.Window;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(medalRect, intensity.DisplayCode ?? string.Empty);

                // ---- Title + tag + projected daily ----
                float textX = medalRect.xMax + 6f;
                float textW = rect.xMax - textX;
                string title = string.IsNullOrEmpty(intensity.LabelKey)
                    ? string.Empty
                    : intensity.LabelKey.Translate().ToString();
                string tag = (intensity.IsEstimated
                    ? "PersonalChronicle.UI.Intensity.Estimated"
                    : "PersonalChronicle.UI.Intensity.Actual").Translate().ToString();
                string daily = intensity.DailyHours > 0d
                    ? string.Format("≈ {0} h/天", intensity.DailyHours.ToString("0.0"))
                    : string.Empty;
                string line = string.IsNullOrEmpty(daily)
                    ? string.Format("{0} · {1}", title, tag)
                    : string.Format("{0} · {1} · {2}", title, tag, daily);

                GUI.color = UITheme.Text;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(textX, rect.y, textW, rect.height), line);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        private string BuildCoverRoleDescription(PawnObject pawn)
        {
            string role = RoleLabel(pawn.Role);
            string faction = FactionLabel(pawn);
            string background = BuildCoverBackground(pawn);
            if (!string.IsNullOrEmpty(faction) && !string.IsNullOrEmpty(background))
            {
                return "PersonalChronicle.UI.Cover.RoleFormat".Translate(role, faction, background).ToString();
            }
            if (string.IsNullOrEmpty(faction) && !string.IsNullOrEmpty(background))
            {
                return "PersonalChronicle.UI.Cover.RoleFormatNoFaction".Translate(role, background).ToString();
            }
            if (!string.IsNullOrEmpty(faction) && string.IsNullOrEmpty(background))
            {
                return "PersonalChronicle.UI.Cover.RoleFormatNoBackground".Translate(role, faction).ToString();
            }
            return "PersonalChronicle.UI.Cover.RoleFormatMinimal".Translate(role).ToString();
        }

        private string BuildCoverBackground(PawnObject pawn)
        {
            string child = ResolveBackstoryLabel(pawn.ChildhoodBackstoryDefName);
            string adult = ResolveBackstoryLabel(pawn.AdulthoodBackstoryDefName);
            if (!string.IsNullOrEmpty(child) && !string.IsNullOrEmpty(adult))
            {
                return child + " / " + adult;
            }
            if (!string.IsNullOrEmpty(child)) return child;
            if (!string.IsNullOrEmpty(adult)) return adult;
            return "";
        }

        private static string ResolveBackstoryLabel(string backstoryDefName)
        {
            if (string.IsNullOrEmpty(backstoryDefName)) return "";
            var def = DefDatabase<BackstoryDef>.GetNamedSilentFail(backstoryDefName);
            return def != null ? def.title : backstoryDefName;
        }

        private string BuildCoverDaysText(PawnObject pawn)
        {
            if (pawn.JoinTick < 0L)
            {
                return "PersonalChronicle.UI.Cover.DaysUnknown".Translate().ToString();
            }
            long endTick = pawn.IsArchived && pawn.DeathTick > 0L
                ? pawn.DeathTick
                : Find.TickManager.TicksGame;
            long days = (endTick - pawn.JoinTick) / RimWorld.GenDate.TicksPerDay;
            if (days <= 0L) days = 0L;
            string key = pawn.IsArchived
                ? "PersonalChronicle.UI.Cover.DaysToDeath"
                : "PersonalChronicle.UI.Cover.DaysKnown";
            return key.Translate(days.ToString()).ToString();
        }

        private float DrawLedger(Rect rect, float y, PawnObject pawn)
        {
            // Header row + 3 StatCells (already realised output, work hours, weekly average).
            float headH = 26f;
            float cellH = UIComponents.StatCellMinHeight;
            float gap = UITheme.GridGap;
            float cellW = (rect.width - UITheme.CardPadX * 2f - gap * 2f) / 3f;

            UIComponents.DrawSubsectionHeader(new Rect(rect.x, y, rect.width, headH),
                "PersonalChronicle.UI.Ledger.Title");

            float rowY = y + headH + UITheme.SpaceXxs;
            float cellX = rect.x + UITheme.CardPadX;

            bool known = pawn.JoinTick >= 0L;
            string unknown = "PersonalChronicle.UI.Ledger.UnknownValue".Translate().ToString();

            // Already realised output (silver).
            string realisedValue = known && cachedProductionSummary != null && cachedProductionSummary.TotalMarketValue > 0f
                ? FormatSilver(cachedProductionSummary.TotalMarketValue)
                : (known ? "0" : unknown);
            string realisedUnit = "PersonalChronicle.UI.Ledger.OutputValueUnit".Translate().ToString();
            UIComponents.StatCell(new Rect(cellX, rowY, cellW, cellH),
                "PersonalChronicle.UI.Ledger.OutputValue".Translate().ToString(),
                realisedValue,
                realisedUnit,
                inlineSubLabel: true);

            // Total work hours — value carries the numeric, subLabel carries the unit
            // (避免 "10.7 h" + subLabel "h" 的单位重复).
            string workValue = known
                ? (GetCachedTotalWorkTicks() / (float)RimWorld.GenDate.TicksPerHour).ToString("0.0")
                : unknown;
            UIComponents.StatCell(new Rect(cellX + (cellW + gap), rowY, cellW, cellH),
                "PersonalChronicle.UI.Ledger.TotalWork".Translate().ToString(),
                workValue,
                "PersonalChronicle.UI.Ledger.TotalWorkUnit".Translate().ToString(),
                inlineSubLabel: true);

            // Weekly average yield (estimated silver per week from the health evaluator).
            string weeklyValue = known && cachedHealth != null && cachedHealth.IsDefined
                ? FormatSilver(cachedHealth.WeeklySilverEstimate) : unknown;
            UIComponents.StatCell(new Rect(cellX + 2f * (cellW + gap), rowY, cellW, cellH),
                "PersonalChronicle.UI.Ledger.Weekly".Translate().ToString(),
                weeklyValue,
                "PersonalChronicle.UI.Ledger.WeeklyUnit".Translate().ToString(),
                inlineSubLabel: true);

            return rowY + cellH;
        }

        private long GetCachedTotalWorkTicks()
        {
            // Use the cachedWorkIntensity view as a proxy; falling back to 0 is fine.
            if (cachedWorkIntensity == null || !cachedWorkIntensity.IsDefined) return 0L;
            double hours = cachedWorkIntensity.TotalHours;
            return (long)(hours * 2500d); // RimWorld 1h ≈ 2500 ticks
        }

        private float DrawOutputLedger(Rect rect, float y)
        {
            // Header + ProgressBar rows by production type. Empty state shows placeholder.
            float headH = 26f;
            UIComponents.DrawSubsectionHeader(new Rect(rect.x, y, rect.width, headH),
                "PersonalChronicle.UI.OutputLedger.Title");
            float bodyY = y + headH + 4f;

            IReadOnlyList<ProductionTypeView> types = cachedProductionSummary != null
                ? cachedProductionSummary.Types : null;
            float totalValue = cachedProductionSummary != null
                ? cachedProductionSummary.TotalMarketValue : 0f;

            if (types == null || types.Count == 0 || totalValue <= 0f)
            {
                UIComponents.Label(new Rect(rect.x + UITheme.CardPadX, bodyY, rect.width - UITheme.CardPadX * 2f, 22f),
                    "PersonalChronicle.UI.OutputLedger.Empty".Translate().ToString(),
                    GameFont.Tiny, UITheme.Dim);
                return bodyY + 24f;
            }

            // Sort descending by value for the most-extracted types first.
            List<ProductionTypeView> sorted = new List<ProductionTypeView>(types);
            sorted.Sort((a, b) => b.MarketValue.CompareTo(a.MarketValue));
            int take = Mathf.Min(5, sorted.Count);
            const float rowH = 22f;
            float rowY = bodyY;
            for (int i = 0; i < take; i++)
            {
                ProductionTypeView t = sorted[i];
                if (t == null) continue;
                float share = t.MarketValue / totalValue;
                string label = ResolveProductionTypeLabel(t.DefName);
                string valueText = FormatSilver(t.MarketValue)
                    + " " + "PersonalChronicle.UI.Ledger.OutputValueUnit".Translate().ToString();
                string pctText = Mathf.RoundToInt(share * 100f) + "%";
                DrawProductionRow(new Rect(rect.x + UITheme.CardPadX, rowY, rect.width - UITheme.CardPadX * 2f, rowH),
                    label, valueText, pctText, share);
                rowY += rowH;
            }
            return rowY;
        }

        private static void DrawProductionRow(Rect rect, string label, string valueText, string pctText, float share)
        {
            float labelW = 80f;
            float pctW = 50f;
            float barX = rect.x + labelW;
            float barW = rect.width - labelW - pctW - UITheme.ProgressbarValueW;
            UIComponents.Label(new Rect(rect.x, rect.y, labelW, rect.height),
                label,
                GameFont.Tiny, UITheme.Text,
                TextAnchor.MiddleLeft);
            float barH = UITheme.ProgressbarH;
            Rect bar = new Rect(barX, rect.y + (rect.height - barH) / 2f, barW, barH);
            UIComponents.ProgressBar(bar, Mathf.Clamp01(share), UITheme.Blood);
            UIComponents.Label(new Rect(bar.xMax + 4f, rect.y, UITheme.ProgressbarValueW, rect.height),
                valueText,
                GameFont.Tiny, UITheme.SecondaryText,
                TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(rect.x + rect.width - pctW, rect.y, pctW, rect.height),
                pctText,
                GameFont.Tiny, UITheme.Muted,
                TextAnchor.MiddleRight);
        }

        private static string ResolveProductionTypeLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return "—";
            // v4.6.5: production rows are aggregated by ThingCategory. Try the
            // category def first, then fall back to the item def for uncategorised
            // (or legacy) entries.
            var cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(defName);
            if (cat != null) return cat.label != null ? cat.label : defName;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null) return def.label != null ? def.label : defName;
            return defName;
        }

        private void DrawWorkIntensityCards(Rect rect, ref float y)
        {
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.WorkTime".Translate().ToString());
            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Muted;
            Widgets.Label(new Rect(rect.x, y, rect.width, 28f),
                "PersonalChronicle.UI.WorkTimeFootnote".Translate().ToString());
            GUI.color = prevColor;
            y += 32f;

            if (cachedIntensityWorkTypes == null || cachedIntensityWorkTypes.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoWorkTimeData".Translate().ToString());
                y += 28f;
                return;
            }

            const float cardGap = 8f;
            const float cardHeight = 112f;
            float cardWidth = (rect.width - cardGap) / 2f;
            for (int i = 0; i < cachedIntensityWorkTypes.Count; i++)
            {
                WorkIntensityWorkTypeView row = cachedIntensityWorkTypes[i];
                if (row == null)
                {
                    continue;
                }
                float x = rect.x + (i % 2) * (cardWidth + cardGap);
                float cardY = y + (i / 2) * (cardHeight + cardGap);
                Color accent = WorkTypeColor(row.WorkTypeDefName);
                Rect card = new Rect(x, cardY, cardWidth, cardHeight);
                ArchiveUiStyle.DrawCard(card, accent);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(card.x + 10f, card.y + 10f, card.width - 100f, 22f),
                    WorkTypeLabel(row.WorkTypeDefName));
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(card.x + card.width - 92f, card.y + 30f, 80f, 28f),
                    FormatWorkHours(row.Ticks));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                string rank = row.Rank > 0
                    ? "PersonalChronicle.UI.Intensity.Rank".Translate(row.Rank, row.PopulationCount).ToString()
                    : "PersonalChronicle.UI.Intensity.RankUnknown".Translate().ToString();
                Widgets.Label(new Rect(card.x + UITheme.CardPadX, card.y + 64f, card.width - UITheme.CardPadX * 2f, 18f), rank);
                Widgets.Label(new Rect(card.x + UITheme.CardPadX, card.y + 84f, card.width - UITheme.CardPadX * 2f, 18f),
                    "PersonalChronicle.UI.Intensity.WorkShare".Translate(
                        Mathf.RoundToInt(row.Share01 * 100f),
                        Mathf.RoundToInt(row.RelativeToMaximum01 * 100f)).ToString());
                GUI.color = prevColor;
                Widgets.FillableBar(new Rect(card.x + UITheme.CardPadX, card.y + 106f, card.width - UITheme.CardPadX * 2f, 6f),
                    Mathf.Clamp01(row.Share01));
            }
            Text.Font = GameFont.Small;
            int rows = (cachedIntensityWorkTypes.Count + 1) / 2;
            y += rows * (cardHeight + cardGap);
        }

        private static string BuildIntensityRelativeLabel(WorkIntensityView intensity)
        {
            if (intensity == null || intensity.RelativeRatio <= 0d)
            {
                return "PersonalChronicle.UI.Intensity.RelativeUnavailable".Translate().ToString();
            }
            if (intensity.IsOverloaded)
            {
                return "PersonalChronicle.UI.Intensity.Overload".Translate().ToString();
            }
            if (intensity.IsSignificantlyIdle)
            {
                return "PersonalChronicle.UI.Intensity.Slack".Translate().ToString();
            }
            return "PersonalChronicle.UI.Intensity.Relative".Translate(
                intensity.RelativeRatio.ToString("0.00")).ToString();
        }

        private static string TranslateIntensityKey(string key, string fallback)
        {
            return string.IsNullOrEmpty(key)
                ? (fallback ?? string.Empty)
                : key.Translate().ToString();
        }

        private static Color ParseIntensityColor(string hex)
        {
            Color color;
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out color))
            {
                return color;
            }
            return ArchiveUiStyle.Info;
        }

        private static string FormatHours(double hours)
        {
            return "PersonalChronicle.UI.Hours".Translate(hours.ToString("0.0")).ToString();
        }

        private static string FormatDays(double days)
        {
            return days.ToString("0.0");
        }

        private static Color WorkTypeColor(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return ArchiveUiStyle.Info;
            }
            int hash = StringComparer.Ordinal.GetHashCode(defName) & 0x7fffffff;
            float hue = (hash % 360) / 360f;
            Color color = Color.HSVToRGB(hue, 0.38f, 0.82f);
            color.a = 1f;
            return color;
        }

        private static string SkillDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            SkillDef def = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            return defName;
        }

        /// <summary>
        /// v3.1 P3 Social: significant relation snapshots + social events + co-occurrence.
        /// </summary>
        private void DrawSocialTab(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            PawnObject pawn = cachedDetailObject as PawnObject;

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.RelationNetwork".Translate().ToString());
            y = DrawSocialNetwork(rect, y, pawn, service);

            y += 8f;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.RelationEvents".Translate().ToString());
            int socialEvents = 0;
            for (int i = 0; i < cachedDetailRawEvents.Count; i++)
            {
                ChronicleEvent ev = cachedDetailRawEvents[i];
                if (ev == null || !IsSocialEvent(ev))
                {
                    continue;
                }
                socialEvents++;
                string action = string.Empty;
                string rel = string.Empty;
                if (ev.Params != null)
                {
                    ev.Params.TryGetValue(ChronicleEventParams.RelationAction, out action);
                    ev.Params.TryGetValue(ChronicleEventParams.Relation, out rel);
                }
                string title = FormatSocialEventTitle(action, rel, ev, service);
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                DrawEventRow(row, FormatDate(ev.Tick), title, FormatParams(ev.Params));
                if (Widgets.ButtonInvisible(row))
                {
                    OpenEventDetail(service, ev);
                }
                y += TimelineRowHeight;
            }
            if (socialEvents == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoRelationEvents".Translate().ToString());
                y += 28f;
            }

            y += 8f;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Intertwined".Translate().ToString());
            int count = 0;
            for (int i = 0; i < cachedLinkedObjects.Count; i++)
            {
                LinkedObjectView link = cachedLinkedObjects[i];
                if (link.CategoryKey != ArchiveCategoryKeys.Pawn)
                {
                    continue;
                }
                count++;
                Rect row = new Rect(rect.x, y, rect.width, RowHeight - 4f);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x + 6f, row.y + 4f, row.width - 200f, 22f), link.Label);
                Text.Font = GameFont.Tiny;
                GUI.color = UITheme.SecondaryText;
                Widgets.Label(new Rect(row.x + row.width - 196f, row.y + 6f, 190f, 18f),
                    "PersonalChronicle.UI.SharedEvents".Translate().ToString());
                GUI.color = prevColor;
                if (link.Target != NavTarget.None && Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, link.Target, link.StableId, null);
                }
                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
                y += RowHeight + 2f;
            }
            if (count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoSocialData".Translate().ToString());
            }
        }

        private float DrawSocialNetwork(Rect rect, float y, PawnObject pawn, IArchiveService service)
        {
            const float basePanelHeight = 246f;
            // Max visible relation nodes on the graph. The grid slot pool supports
            // 26 positions; 24 leaves headroom and keeps the auto-fit readable.
            const float maxNodeCount = 24f;
            Rect panel = new Rect(rect.x, y, rect.width, basePanelHeight);
            ArchiveUiStyle.DrawPanel(panel, ArchiveUiStyle.PanelRaised);

            List<SocialNodeView> nodes = new List<SocialNodeView>();
            HashSet<string> seen = new HashSet<string>();
            if (pawn != null && pawn.Relations != null)
            {
                // Rank first, then cap, so closest ties always win a slot.
                List<SignificantRelation> ranked = new List<SignificantRelation>();
                for (int i = 0; i < pawn.Relations.Count; i++)
                {
                    SignificantRelation candidate = pawn.Relations[i];
                    if (candidate != null && !string.IsNullOrEmpty(candidate.OtherStableId))
                    {
                        ranked.Add(candidate);
                    }
                }
                ranked.Sort((a, b) => SocialNodeRank(a).CompareTo(SocialNodeRank(b)));

                for (int i = 0; i < ranked.Count && nodes.Count < maxNodeCount; i++)
                {
                    SignificantRelation relation = ranked[i];
                    if (!seen.Add(relation.OtherStableId))
                    {
                        continue;
                    }
                    ArchiveObject other = service.GetObject(relation.OtherStableId);
                    nodes.Add(new SocialNodeView
                    {
                        StableId = relation.OtherStableId,
                        Label = other != null ? ObjectDisplayLabel(other) : relation.OtherLabel,
                        RelationLabel = RelationDefLabel(relation.RelationDefName),
                        RelationDefName = relation.RelationDefName,
                        Active = relation.IsActive
                    });
                }
            }
            if (nodes.Count == 0)
            {
                for (int i = 0; i < cachedLinkedObjects.Count && nodes.Count < maxNodeCount; i++)
                {
                    LinkedObjectView link = cachedLinkedObjects[i];
                    if (link.CategoryKey != ArchiveCategoryKeys.Pawn || !seen.Add(link.StableId))
                    {
                        continue;
                    }
                    nodes.Add(new SocialNodeView
                    {
                        StableId = link.StableId,
                        Label = link.Label,
                        RelationLabel = "PersonalChronicle.UI.SharedEvents".Translate().ToString(),
                        RelationDefName = string.Empty,
                        Active = true
                    });
                }
            }

            // Scroll-wheel zoom: scale the whole graph around the panel centre so
            // dense relations separate instead of overlapping. Consume the scroll
            // event so it does not also scroll the surrounding detail view.
            if (nodes.Count > 0 && panel.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.ScrollWheel)
                {
                    float delta = Event.current.delta.y;
                    if (delta != 0f)
                    {
                        socialNetworkZoomTouched = true;
                        socialNetworkZoom = Mathf.Clamp(
                            socialNetworkZoom * (delta < 0f ? 1.1f : 0.9f), 0.6f, 2.4f);
                        Event.current.Use();
                    }
                }
            }

            // Importance-driven grid slots: spouses occupy the symmetric left/right
            // anchor (the "extension centre"), parents sit above, children below,
            // siblings on the sides and friends/rivals/others on the outer ring.
            // All relation cards share the SAME size — only positions change by
            // tier — so the layout reads as a calm family tree rather than a
            // weighted importance chart.
            (int col, int row)[] slots = GridSlotsFor(nodes);
            int maxAbsCol = 0;
            int maxAbsRow = 0;
            for (int s = 0; s < slots.Length; s++)
            {
                if (Mathf.Abs(slots[s].col) > maxAbsCol) maxAbsCol = Mathf.Abs(slots[s].col);
                if (Mathf.Abs(slots[s].row) > maxAbsRow) maxAbsRow = Mathf.Abs(slots[s].row);
            }

            // Grid spacing scales with the card size AND zoom so gaps grow together
            // with the cards: cards never overlap at any zoom level. The gap ratio
            // (30% of card size) plus a 22f minimum keeps the relation-row text
            // from spilling into the next card vertically.
            const float gapRatio = 0.30f;
            const float minNodeGap = 22f;
            float baseNodeW = Mathf.Min(160f, Mathf.Max(110f, panel.width * 0.22f));
            float baseCenterW = 176f;
            float baseCenterH = 50f;
            float baseNodeH = 60f;

            // First entry (not yet scrolled): auto-fit the whole graph inside the
            // panel so every relation is visible without dragging. Only the
            // horizontal extent is constrained by the panel width — the panel
            // height already grows to fit the vertical grid (panelHeight below),
            // so fitY must NOT cap against the fixed 246f base height (which would
            // shrink dense graphs into unreadable clutter).
            // The 0.2f floor lets even very narrow panels shrink a dense graph far
            // enough that no node overflows horizontally; a higher floor would
            // leave outer columns clipped on small windows. At 0.2f cards are
            // small but remain readable and never clip outside the panel.
            float zoom = socialNetworkZoom;
            if (!socialNetworkZoomTouched && nodes.Count > 0)
            {
                float fitW = (maxAbsCol * 2 + 1) * (baseNodeW * (1f + gapRatio) + minNodeGap)
                    + baseNodeW * 2f;
                float fitX = (panel.width - 24f) / Mathf.Max(1f, fitW);
                zoom = Mathf.Clamp(fitX, 0.2f, 1f);
                socialNetworkZoom = zoom;
            }

            float nodeWidth = baseNodeW * zoom;
            float centerCardW = baseCenterW * zoom;
            float centerCardH = baseCenterH * zoom;
            float nodeCardH = baseNodeH * zoom;

            // Gap scales with zoom so enlarged cards keep a proportional gap:
            // spacing = cardSize + gap, gap = cardSize*gapRatio + minNodeGap*zoom.
            float colSpacing = nodeWidth + Mathf.Max(nodeWidth * gapRatio, minNodeGap * zoom);
            float rowSpacing = nodeCardH + Mathf.Max(nodeCardH * gapRatio, minNodeGap * zoom);

            // Grow the panel vertically with zoom AND with the grid extent so the
            // outermost nodes stay inside its frame.
            float panelHeight = Mathf.Max(
                basePanelHeight,
                basePanelHeight * zoom,
                (maxAbsRow * 2 + 1) * rowSpacing + nodeCardH + 32f);
            panel = new Rect(panel.x, panel.y, panel.width, panelHeight);
            ArchiveUiStyle.DrawPanel(panel, ArchiveUiStyle.PanelRaised);

            Vector2 center = new Vector2(panel.center.x, panel.center.y) + socialNetworkPan;
            Rect centerRect = new Rect(
                center.x - centerCardW / 2f,
                center.y - centerCardH / 2f,
                centerCardW,
                centerCardH);

            // Pre-compute node positions/rects so we can tell whether the mouse
            // is over an interactive card before deciding whether to pan.
            List<Vector2> nodeCenters = new List<Vector2>(nodes.Count);
            List<Rect> nodeRects = new List<Rect>(nodes.Count);
            for (int i = 0; i < nodes.Count; i++)
            {
                (int col, int row) slot = slots[i];
                Vector2 nodeCenter = new Vector2(
                    center.x + slot.col * colSpacing,
                    center.y + slot.row * rowSpacing);
                Rect nodeRect = new Rect(
                    nodeCenter.x - nodeWidth / 2f,
                    nodeCenter.y - nodeCardH / 2f,
                    nodeWidth,
                    nodeCardH);
                nodeCenters.Add(nodeCenter);
                nodeRects.Add(nodeRect);
            }

            // Left-drag pan: move the graph when dragging on empty panel background.
            // Only pan if the cursor is not over the centre card or a node card; node
            // clicks are handled by Widgets.ButtonInvisible below.
            bool mouseOverInteractive = panel.Contains(Event.current.mousePosition)
                && (centerRect.Contains(Event.current.mousePosition)
                    || nodeRects.Any(r => r.Contains(Event.current.mousePosition)));
            if (!mouseOverInteractive && panel.Contains(Event.current.mousePosition)
                && Event.current.type == EventType.MouseDrag
                && Event.current.button == 0)
            {
                socialNetworkPan += Event.current.delta;
                Event.current.Use();
            }

            // Draw orthogonal (horizontal + vertical) links from centre-card edge to
            // each relation-card edge. All relation cards share the same width/height
            // so the elbow points stay aligned on the card midline; link thickness is
            // bumped for active ties to read as smoother, calmer strokes.
            for (int i = 0; i < nodes.Count; i++)
            {
                Vector2 nodeCenter = nodeCenters[i];
                Color linkColor = nodes[i].Active ? ArchiveUiStyle.Accent : ArchiveUiStyle.Border;
                float linkThickness = nodes[i].Active ? 2.5f : 1f;
                DrawOrthogonalLink(center, nodeCenter, linkColor, linkThickness);
            }

            if (nodes.Count == 0)
            {
                // 空态只画提示文字，不画中心卡（分支在中心卡绘制之前提前返回）。
                UIComponents.Label(panel, "PersonalChronicle.UI.NoSignificantRelations".Translate().ToString(),
                    UITheme.FontBody, UITheme.Muted, TextAnchor.MiddleCenter);
                return panel.yMax + UITheme.SpaceXs;
            }

            ArchiveUiStyle.DrawCard(centerRect, ArchiveUiStyle.Accent);
            UIComponents.Label(centerRect, pawn != null ? ObjectDisplayLabel(pawn) : "—",
                UITheme.FontBody, UITheme.Text, TextAnchor.MiddleCenter);

            for (int i = 0; i < nodes.Count; i++)
            {
                Rect nodeRect = nodeRects[i];
                ArchiveUiStyle.DrawCard(nodeRect, nodes[i].Active ? ArchiveUiStyle.Info : ArchiveUiStyle.Muted);
                // 中文行高经验：节点标签（Small）≥22f，关系标签（Tiny）≥18f；
                // 经 UIComponents.Label 渲染，font/color/anchor 内部配对恢复。
                UIComponents.Label(new Rect(nodeRect.x + UITheme.CardPadX, nodeRect.y + UITheme.SpaceXxs,
                    nodeRect.width - UITheme.CardPadX * 2f, UITheme.FontBodyLineHeight * zoom),
                    nodes[i].Label, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleCenter);
                UIComponents.Label(new Rect(nodeRect.x + UITheme.CardPadX, nodeRect.y + UITheme.SpaceLg * zoom,
                    nodeRect.width - UITheme.CardPadX * 2f, 18f * zoom),
                    nodes[i].RelationLabel, UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleCenter);
                if (Widgets.ButtonInvisible(nodeRect))
                {
                    NavigateTarget(service, NavTarget.Pawn, nodes[i].StableId, null);
                }
            }

            return panel.yMax + UITheme.SpaceXs;
        }

        /// <summary>
        /// Returns the (col, row) grid slots for a given relation count. Slots are
        /// returned in the order they should be filled (importance-ranked by caller),
        /// importance-ranked by caller), so position 0 is always the most prominent
        /// tie. Slots are exact integer offsets — DrawSocialNetwork multiplies them
        /// by colSpacing / rowSpacing so every node lands on a grid intersection.
        /// All relation cards share the SAME size — only positions change by tier:
        /// spouses occupy the symmetric left/right anchor (the "extension centre"),
        /// parents sit above, children below, siblings on the sides and
        /// friends/rivals/others on the outer ring.
        /// </summary>
        private static (int col, int row)[] GridSlotsFor(List<SocialNodeView> nodes)
        {
            if (nodes.Count == 0)
            {
                return System.Array.Empty<(int, int)>();
            }

            // Slot pools, one per relation tier. When a pool is exhausted the
            // remaining nodes fall back to the outer ring; same size everywhere.
            var spouseSlots = new (int, int)[]
            {
                (-1, 0), ( 1, 0) // 夫妻对称左右
            };
            var parentSlots = new (int, int)[]
            {
                ( 0, -1), (-1, -1), ( 1, -1), // 父母辈往上
                (-2, -1), ( 2, -1)
            };
            var childSlots = new (int, int)[]
            {
                ( 0, 1), (-1, 1), ( 1, 1), // 子女辈往下
                (-2, 1), ( 2, 1)
            };
            var siblingSlots = new (int, int)[]
            {
                (-2, 0), ( 2, 0) // 平辈左右
            };
            var outerSlots = new (int, int)[]
            {
                (-1, -2), ( 1, -2), (-1, 2), ( 1, 2),
                (-2, -2), ( 2, -2), (-2, 2), ( 2, 2),
                ( 0, -2), ( 0, 2), (-3, 0), ( 3, 0)
            };

            var result = new (int col, int row)[nodes.Count];
            var used = new HashSet<(int, int)>();
            int[] cursors = new int[5]; // spouse / parent / child / sibling / outer
            for (int i = 0; i < nodes.Count; i++)
            {
                SocialRelationTier tier = SocialRelationTierOf(nodes[i].RelationDefName);
                (int col, int row) slot;
                switch (tier)
                {
                    case SocialRelationTier.Spouse:
                        slot = TakeNext(spouseSlots, ref cursors[0], used);
                        break;
                    case SocialRelationTier.Parent:
                        slot = TakeNext(parentSlots, ref cursors[1], used);
                        break;
                    case SocialRelationTier.Child:
                        slot = TakeNext(childSlots, ref cursors[2], used);
                        break;
                    case SocialRelationTier.Sibling:
                        slot = TakeNext(siblingSlots, ref cursors[3], used);
                        break;
                    default:
                        slot = TakeNext(outerSlots, ref cursors[4], used);
                        break;
                }
                result[i] = slot;
            }
            return result;
        }

        private static (int col, int row) TakeNext(
            (int col, int row)[] pool, ref int cursor, HashSet<(int, int)> used)
        {
            // 优先用池内未被占用的槽位；池耗尽则在外圈兜底，保证不重叠。
            for (int k = cursor; k < pool.Length; k++)
            {
                if (used.Add((pool[k].col, pool[k].row)))
                {
                    cursor = k + 1;
                    return pool[k];
                }
            }
            cursor = pool.Length;
            for (int ring = 1; ring < 8; ring++)
            {
                for (int dc = -ring; dc <= ring; dc++)
                {
                    for (int dr = -ring; dr <= ring; dr++)
                    {
                        if (Mathf.Abs(dc) != ring && Mathf.Abs(dr) != ring)
                        {
                            continue;
                        }
                        if (used.Add((dc, dr)))
                        {
                            return (dc, dr);
                        }
                    }
                }
            }
            return (0, 0);
        }

        private enum SocialRelationTier
        {
            Other,
            Spouse,
            Parent,
            Child,
            Sibling
        }

        private static SocialRelationTier SocialRelationTierOf(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return SocialRelationTier.Other;
            }
            if (defName.Contains("Spouse") || defName.Contains("Lover") || defName.Contains("Fiance"))
            {
                return SocialRelationTier.Spouse;
            }
            if (defName.Contains("Parent") || defName.Contains("Mother") || defName.Contains("Father")
                || defName.Contains("Stepparent") || defName.Contains("InLaw")
                || defName.Contains("Grandparent") || defName.Contains("Uncle") || defName.Contains("Aunt"))
            {
                return SocialRelationTier.Parent;
            }
            if (defName.Contains("Child") || defName.Contains("Son") || defName.Contains("Daughter")
                || defName.Contains("Grandchild") || defName.Contains("Nephew") || defName.Contains("Niece"))
            {
                return SocialRelationTier.Child;
            }
            if (defName.Contains("Sibling") || defName.Contains("Brother") || defName.Contains("Sister")
                || defName.Contains("Cousin") || defName.Contains("Half"))
            {
                return SocialRelationTier.Sibling;
            }
            return SocialRelationTier.Other;
        }

        /// <summary>
        /// Draws an orthogonal Z-shaped link between two card centres. The line
        /// starts at card A's midpoint and ends at card B's midpoint — NOT from
        /// the card edges. Both vertical axes run along card A's X midpoint
        /// (center.x) and card B's X midpoint (nodeCenter.x); the two vertical
        /// segments are joined by one horizontal segment at the mid-height.
        /// A small 5f chamfer at each 90° turn keeps the corner smooth.
        /// </summary>
        private void DrawOrthogonalLink(Vector2 center, Vector2 nodeCenter, Color color, float thickness)
        {
            float sx = Mathf.Sign(nodeCenter.x - center.x);
            float sy = Mathf.Sign(nodeCenter.y - center.y);
            float midY = (center.y + nodeCenter.y) / 2f;
            // 拐角圆角过渡随卡片缩放：放大时圆角弧度按比例增大，避免尖刺感。
            float radius = 10f * Mathf.Max(0.4f, socialNetworkZoom);

            // 垂直段 1：沿卡片 A 的 X 中点（center.x）从 A 中心向下/上到 midY 附近
            Vector2 v1Start = new Vector2(center.x, center.y);
            Vector2 v1End = new Vector2(center.x, midY - sy * radius);
            // 斜角 1：从垂直段平滑转入水平段
            Vector2 c1End = new Vector2(center.x + sx * radius, midY);
            // 水平段：从 A 的 X 中点横跨到 B 的 X 中点（midY 高度）
            Vector2 hEnd = new Vector2(nodeCenter.x - sx * radius, midY);
            // 斜角 2：从水平段平滑转入垂直段
            Vector2 c2End = new Vector2(nodeCenter.x, midY + sy * radius);
            // 垂直段 2：沿卡片 B 的 X 中点（nodeCenter.x）到 B 中心
            Vector2 v2End = new Vector2(nodeCenter.x, nodeCenter.y);

            Widgets.DrawLine(v1Start, v1End, color, thickness);
            Widgets.DrawLine(v1End, c1End, color, thickness);
            Widgets.DrawLine(c1End, hEnd, color, thickness);
            Widgets.DrawLine(hEnd, c2End, color, thickness);
            Widgets.DrawLine(c2End, v2End, color, thickness);
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
                d.ServiceDays.ToString() + " " + "PersonalChronicle.UI.DaysUnit".Translate().ToString());
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

        private string CurrentTabKey()
        {
            string[] tabs = cachedDetailObject is ThingObject ? WeaponTabKeys : PawnTabKeys;
            if (detailTabIndex >= 0 && detailTabIndex < tabs.Length)
            {
                return tabs[detailTabIndex];
            }
            return "Overview";
        }

        // ---- Detail: timeline (shared) ----------------------------------------

        private void DrawDetailTimeline(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            float y = DrawTimelineToolbar(rect, rect.y);
            int visibleCount = 0;
            for (int i = 0; i < cachedDetailEvents.Count; i++)
            {
                EventLineView line = cachedDetailEvents[i];
                if (line.Event == null || !ShouldShowTimelineEvent(line.Event))
                {
                    continue;
                }
                visibleCount++;
                float descHeight = string.IsNullOrEmpty(line.DescriptionText)
                    ? 0f
                    : Text.CalcHeight(line.DescriptionText, rect.width - 8f) + 4f;
                float chipsHeight = line.Chips != null && line.Chips.Count > 0 ? ChipRowHeight : 0f;
                float rowHeight = TimelineRowHeight + descHeight + chipsHeight;
                Rect row = new Rect(rect.x, y, rect.width, rowHeight);

                ArchiveUiStyle.DrawSectionMarker(
                    new Rect(row.x, row.y + 3f, 4f, row.height - 6f),
                    ImportanceColor(ChronicleEventImportance.Resolve(line.Event)));
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(row.x + 4f, row.y + 3f, 150f, 18f), line.DateText);

                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x + 158f, row.y + 3f, row.width - 158f - 190f, 20f), line.NameText);

                Text.Font = GameFont.Tiny;
                GUI.color = UITheme.SecondaryText;
                Widgets.Label(new Rect(row.x + row.width - 186f, row.y + 3f, 182f, 18f), line.ParamsText);
                GUI.color = prevColor;

                float cy = row.y + TimelineRowHeight;
                if (descHeight > 0f)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = UITheme.SecondaryText;
                    Widgets.Label(new Rect(row.x + 4f, cy, row.width - 8f, descHeight), line.DescriptionText);
                    GUI.color = prevColor;
                    cy += descHeight;
                }
                if (chipsHeight > 0f)
                {
                    DrawChips(new Rect(row.x + 4f, cy, row.width - 8f, ChipRowHeight), line.Chips, service);
                }

                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
                y += rowHeight;
            }
            if (visibleCount == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 24f),
                    "PersonalChronicle.UI.NoTimelineMatches".Translate().ToString());
            }
        }

        private float DrawTimelineToolbar(Rect rect, float y)
        {
            Rect bar = new Rect(rect.x, y, rect.width, 34f);
            ArchiveUiStyle.DrawCard(bar, ArchiveUiStyle.Info);
            float x = bar.x + 8f;
            float toggleWidth = Mathf.Min(145f, Mathf.Max(90f, (bar.width - 230f) / 3f));
            timelineShowCareer = DrawTimelineToggle(
                new Rect(x, bar.y + 5f, toggleWidth, 24f),
                timelineShowCareer,
                "PersonalChronicle.UI.TimelineCareerLayer".Translate().ToString());
            x += toggleWidth + 4f;
            timelineShowCombat = DrawTimelineToggle(
                new Rect(x, bar.y + 5f, toggleWidth, 24f),
                timelineShowCombat,
                "PersonalChronicle.UI.TimelineCombatLayer".Translate().ToString());
            x += toggleWidth + 4f;
            timelineShowSocial = DrawTimelineToggle(
                new Rect(x, bar.y + 5f, toggleWidth, 24f),
                timelineShowSocial,
                "PersonalChronicle.UI.TimelineSocialLayer".Translate().ToString());

            Rect importance = new Rect(bar.xMax - 210f, bar.y + 5f, 202f, 24f);
            ArchiveUiStyle.DrawBadge(importance, ImportanceFilterLabelText(), ImportanceColor(timelineMinimumImportance));
            if (Widgets.ButtonInvisible(importance))
            {
                int next = ((int)timelineMinimumImportance + 1) % ((int)ChronicleImportance.Critical + 1);
                timelineMinimumImportance = (ChronicleImportance)next;
            }
            return y + 42f;
        }

        private static bool DrawTimelineToggle(Rect rect, bool enabled, string label)
        {
            Color prevColor = GUI.color;
            ArchiveUiStyle.DrawSelectedNavigation(rect, enabled);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = enabled ? ArchiveUiStyle.Text : ArchiveUiStyle.Muted;
            Widgets.Label(new Rect(rect.x + 8f, rect.y, rect.width - 16f, rect.height), label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = prevColor;
            if (Widgets.ButtonInvisible(rect))
            {
                enabled = !enabled;
            }
            return enabled;
        }

        private bool ShouldShowTimelineEvent(ChronicleEvent ev)
        {
            if (ev == null || ChronicleEventImportance.Resolve(ev) < timelineMinimumImportance)
            {
                return false;
            }
            if (IsSocialEvent(ev))
            {
                return timelineShowSocial;
            }
            if (IsDeathEvent(ev) || IsBattleEvent(ev))
            {
                return timelineShowCombat;
            }
            if (IsCraftEvent(ev) || IsBuiltEvent(ev) || ev.TypeKey == ChronicleEventType.Join)
            {
                return timelineShowCareer;
            }
            return true;
        }

        private static string ImportanceLabel(ChronicleImportance importance)
        {
            switch (importance)
            {
                case ChronicleImportance.Critical:
                    return "PersonalChronicle.UI.ImportanceCritical".Translate().ToString();
                case ChronicleImportance.Important:
                    return "PersonalChronicle.UI.ImportanceImportant".Translate().ToString();
                case ChronicleImportance.Normal:
                    return "PersonalChronicle.UI.ImportanceNormal".Translate().ToString();
                default:
                    return "PersonalChronicle.UI.ImportanceRoutine".Translate().ToString();
            }
        }

        private string ImportanceFilterLabelText()
        {
            return "PersonalChronicle.UI.TimelineImportance".Translate(ImportanceLabel(timelineMinimumImportance)).ToString();
        }

        private static Color ImportanceColor(ChronicleImportance importance)
        {
            switch (importance)
            {
                case ChronicleImportance.Critical:
                    return ArchiveUiStyle.Dead;
                case ChronicleImportance.Important:
                    return ArchiveUiStyle.Accent;
                case ChronicleImportance.Normal:
                    return ArchiveUiStyle.Info;
                default:
                    return ArchiveUiStyle.Muted;
            }
        }

        private void DrawChips(Rect rect, List<ChipView> chips, IArchiveService service)
        {
            Color prevColor = GUI.color;
            float x = rect.x;
            Text.Font = GameFont.Tiny;
            for (int i = 0; i < chips.Count; i++)
            {
                if (x >= rect.xMax)
                {
                    break;
                }
                ChipView chip = chips[i];
                float chipWidth = Mathf.Min(Text.CalcSize(chip.Label).x + 16f, rect.xMax - x);
                Rect chipRect = new Rect(x, rect.y, chipWidth, ChipRowHeight - 4f);
                if (chip.Target != NavTarget.None)
                {
                    Widgets.DrawHighlightIfMouseover(chipRect);
                }
                GUI.color = chip.Target != NavTarget.None
                    ? UITheme.PillBlue
                    : UITheme.SecondaryText;
                Widgets.Label(chipRect, chip.Label);
                GUI.color = prevColor;
                if (chip.Target != NavTarget.None && Widgets.ButtonInvisible(chipRect))
                {
                    NavigateTarget(service, chip.Target, chip.StableId, null);
                }
                x = chipRect.xMax + 6f;
            }
        }

        // ---- Event view --------------------------------------------------------

        private void DrawEventContent(Rect inner, IArchiveService service)
        {
            Color prevColor = GUI.color;
            if (cachedEventDetail == null)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(inner.x, inner.y + 10f, inner.width, 24f),
                    "PersonalChronicle.UI.NoEventSelected".Translate().ToString());
                return;
            }

            float contentHeight = ComputeEventHeight(inner.width);
            float viewHeight = Mathf.Max(inner.height, contentHeight);
            Rect viewRect = new Rect(inner.x, inner.y, inner.width - 16f, viewHeight);

            Widgets.BeginScrollView(inner, ref eventScroll, viewRect);

            float y = viewRect.y + 4f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 28f),
                "PersonalChronicle.UI.Events".Translate().ToString());
            y += 30f;

            Text.Font = GameFont.Tiny;
            GUI.color = UITheme.SecondaryText;
            string desc = FormatDate(cachedEventDetail.Tick);
            if (cachedEventDetail.Primary != null && !string.IsNullOrEmpty(cachedEventDetail.Primary.LabelSnapshot))
            {
                desc = desc + " · " + cachedEventDetail.Primary.LabelSnapshot;
            }
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 18f), desc);
            GUI.color = prevColor;
            y += 26f;

            // Association network tree panel.
            Rect treePanel = new Rect(viewRect.x, y, viewRect.width, 34f + cachedEventTree.Count * 22f + 12f);
            ArchiveUiStyle.DrawPanel(treePanel, ArchiveUiStyle.PanelRaised);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(treePanel.x + 10f, treePanel.y + 6f, treePanel.width - 20f, 22f),
                "PersonalChronicle.UI.RelatedNetwork".Translate().ToString());

            float ty = treePanel.y + 32f;
            DrawTree(viewRect.x + 10f, ty, treePanel.width - 20f, service);
            y = treePanel.yMax + 18f;

            // Event description.
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 22f),
                "PersonalChronicle.UI.EventDescription".Translate().ToString());
            y += 28f;

            Rect descBox = new Rect(viewRect.x, y, viewRect.width, 90f);
            UIComponents.TintedBox(descBox, UITheme.OverlayWhite04);
            DrawBorder(descBox, ArchiveUiStyle.Border);
            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.SecondaryText;
            Widgets.Label(new Rect(descBox.x + 12f, descBox.y + 10f, descBox.width - 24f, descBox.height - 20f),
                string.IsNullOrEmpty(cachedEventDescription)
                    ? "PersonalChronicle.UI.NoEvents".Translate().ToString()
                    : cachedEventDescription);
            GUI.color = prevColor;
            Text.Font = GameFont.Small;

            Widgets.EndScrollView();
        }

        private float ComputeEventHeight(float width)
        {
            // v4.5.4: named constants mirror DrawEventContent's layout — keep in
            // sync when the draw path changes.
            const float topPad = 4f;
            const float titleH = 28f;
            const float titleGap = 26f;
            const float treeHeaderH = 34f;
            const float treeRowH = 22f;
            const float treePad = 12f;
            const float treeGap = 18f;
            const float descTitleH = 22f;
            const float descGap = 28f;
            const float descBoxH = 90f;
            const float bottomPad = 20f;
            float treeHeight = treeHeaderH + Mathf.Max(1, cachedEventTree.Count) * treeRowH + treePad;
            return topPad + titleH + titleGap + treeHeight + treeGap
                + descTitleH + descGap + descBoxH + bottomPad;
        }

        private void DrawTree(float x, float y, float width, IArchiveService service)
        {
            const float indent = 18f;
            const float stub = 12f;
            const float rowHeight = 22f;

            Text.Font = GameFont.Tiny;
            for (int i = 0; i < cachedEventTree.Count; i++)
            {
                TreeLineView line = cachedEventTree[i];
                float rowY = y + i * rowHeight;

                if (line.Depth > 0)
                {
                    float stubX = line.Depth == 1
                        ? x + indent
                        : x + (line.Depth - 1) * indent + stub;
                    Widgets.DrawLineHorizontal(stubX, rowY + rowHeight / 2f, stub);
                }

                // Branch (depth 1 with children) draws a vertical connector down
                // to its last leaf.
                if (line.Depth == 1 && i + 1 < cachedEventTree.Count && cachedEventTree[i + 1].Depth > 1)
                {
                    int last = i + 1;
                    while (last + 1 < cachedEventTree.Count && cachedEventTree[last + 1].Depth > 1)
                    {
                        last++;
                    }
                    float vx = x + indent + stub;
                    float fromY = rowY + rowHeight / 2f;
                    float toY = y + last * rowHeight + rowHeight / 2f;
                    Widgets.DrawLineVertical(vx, fromY, toY - fromY);
                }

                float labelX = x + line.Depth * indent + stub + 6f;
                Rect labelRect = new Rect(labelX, rowY, width - (labelX - x), rowHeight);
                GUI.color = line.Target != NavTarget.None
                    ? UITheme.PillBlue
                    : UITheme.SecondaryText;
                Widgets.Label(labelRect, line.Label);
                GUI.color = prevColor;

                if (line.Target != NavTarget.None)
                {
                    Widgets.DrawHighlightIfMouseover(labelRect);
                    if (Widgets.ButtonInvisible(labelRect))
                    {
                        NavigateTarget(service, line.Target, line.StableId, line.TargetEvent);
                    }
                }
            }
        }

        // ---- Shared UI helpers -------------------------------------------------

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
            Log.Warning("PersonalChronicle: missing def for display: " + defName);
        }

        private static string FormatDate(long tick)
        {
            if (tick <= 0L)
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
