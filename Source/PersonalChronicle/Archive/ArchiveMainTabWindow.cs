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
        /// <summary>v1.1.4: medal wall (勋章墙) for overview — mirrors DetailSnapshot.Medals.</summary>
        private IReadOnlyList<ReadModels.MedalView> cachedMedals = new List<ReadModels.MedalView>();
        /// <summary>v4.7: legacy chain (传承) for equipment detail — mirrors DetailSnapshot.Legacy.</summary>
        private ReadModels.LegacyView cachedLegacy = new ReadModels.LegacyView();
        // v4.9: equipment legacy extension (溯源 / 工坊署名链 / 同袍共用 / 退役仪式) —
        // mirrors DetailSnapshot; all read-model derived, never recomputed here.
        private ReadModels.ThingOriginView cachedOrigin = new ReadModels.ThingOriginView();
        private ReadModels.MakerChainView cachedMakerChain = new ReadModels.MakerChainView();
        private ReadModels.CoUseView cachedCoUse = new ReadModels.CoUseView();
        private ReadModels.DecommissionView cachedDecommission = new ReadModels.DecommissionView();
        /// <summary>
        /// v4.17: 职业档案视图（嵌入个人档案「生涯」tab）。
        /// 职业档案导航移除后，职业身份/下一职称/资格状态由本快照承载；
        /// 镜像 DetailSnapshot.CareerOverview（Read Model 派生，窗口只消费）。
        /// </summary>
        private ReadModels.CareerOverviewView cachedCareerOverview = new ReadModels.CareerOverviewView();

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









        /// <summary>v4.14: counts how many cached detail events reference a stable id
        /// (as Primary or in Subjects) — the "共同事件数" for the intertwined list.</summary>




        /// <summary>
        /// P2 combat cache: splits kills vs battle participation; death dossier
        /// fields for own death. Weapon detail = all deaths involving the weapon
        /// as kill rows. Pawn detail = own death (dossier) + kills of others +
        /// battles (from Battle events and Battle subject edges).
        /// </summary>

        /// <summary>
        /// v4.3: aggregate cachedKillLines into faction-codex cards.
        /// Faction key derivation (priority): unknown-killer bucket → player-death →
        /// victimFactionDef → wild (animal, no faction) → factionless (no faction, non-animal).
        /// Victim faction/category are read from event Params snapshotted at record time
        /// (external victims are never archived), with graceful fallback for old saves.
        /// </summary>


        /// <summary>
        /// v4.14: first battle label attached to an event's Subjects (death dossier
        /// "关联战役" row). Returns null when the event carries no battle edge.
        /// </summary>


        /// <summary>
        /// Parses the ThingDefName out of a thing stable id. Shape written by
        /// ArchiveService: "&lt;defName&gt;:&lt;thingIDNumber&gt;". Returns the
        /// raw id unchanged when the shape is unexpected (defensive).
        /// </summary>



        // ---- Navigation --------------------------------------------------------



        /// <summary>
        /// v4.6: public navigation entry used by the pawn inspect tab
        /// (<see cref="ITab_Pawn_Chronicle"/>) to deep-link into a pawn's detail
        /// view. Resolves the service itself so external callers stay decoupled
        /// from the service singleton.
        /// </summary>





        // ---- Layout -----------------------------------------------------------







        // ---- Home --------------------------------------------------------------


        /// <summary>
        /// v4.11 P0: Overview › Battle cards — significance pill + threat tag +
        /// name + sub-line (date · N participants · duration) + three metric cells
        /// (force / kills / losses) + roster chips + inline casualty expansion.
        /// All data comes from the Read-Model snapshot (cachedBattleKpis) — the
        /// window renders only, never re-derives aggregates (v4.3 boundary).
        /// </summary>

        /// <summary>v4.14: threat tag label (ThreatBig=大规模威胁 / ThreatSmall=小规模威胁).</summary>

        /// <summary>
        /// v4.14: roster chips — participant pawn labels, clickable to open the
        /// pawn detail. Collapses to "N 人参战" text when no live resolver.
        /// </summary>

        /// <summary>
        /// v4.14: inline casualty expansion — kill/loss lines derived from the
        /// battle-scoped Death events (Read Model already aggregated counts; the
        /// lines come from the same event stream via the service).
        /// </summary>

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

        /// <summary>
        /// v4.13 P1: Overview › Location atlas cards. Each card shows the five
        /// snapshot layers (identity / ownership / geography / lifecycle /
        /// commerce) through the Design System (UITheme tokens only, no raw
        /// colors). Clicking a card toggles the inline chronicle expansion
        /// (this place's event stream). The window consumes the LocationObject
        /// snapshot — no live world queries in the draw path.
        /// </summary>

        /// <summary>v4.14: canonical location kind key for the card (player/settle/quest/unknown).</summary>

        /// <summary>v4.14: established-year short text ("5501").</summary>

        /// <summary>v4.14: dwell ticks (deinit or now minus established).</summary>

        /// <summary>Chronicle panel: this location's event stream (most recent first, capped).</summary>

        /// <summary>Location card accent by lifecycle state (Design System tokens only).</summary>

        /// <summary>Kind text (data-driven; never a hardcoded defName string).</summary>

        /// <summary>Faction text (我方 / 无主 / 派系领地).</summary>

        /// <summary>Geography tag line (biome · hill · coast · pollution · temp).</summary>


        /// <summary>Commerce line (可交易 + sell categories + permit).</summary>

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


        /// <summary>Repulse duration text: EndTick - StartTick, or "ongoing" while the raid is not yet repulsed. Uses the shared span formatter so short raids show hours/minutes and long ones years/quadrums.</summary>


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
                // v4.17: 顶部职业档案区块（职业身份/下一职称/资格状态）高度。
                float profileH = CareerProfileBlockHeight();
                return Mathf.Max(980f,
                    profileH + 280f + intensityRows * 120f + productionSummaryHeight
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
            // v1.1.4: +medal wall (勋章墙) after health valuation block.
            float medalH = MedalWallHeight(cachedMedals);
            return Mathf.Max(panel.height, coverH + 12f + ledgerH + outputH + healthH
                + UITheme.BlockGap + medalH);
        }

        private static float TimelineLineHeight(EventLineView line, float width)
        {
            float descHeight = string.IsNullOrEmpty(line.DescriptionText)
                ? 0f
                : Text.CalcHeight(line.DescriptionText, Mathf.Max(120f, width - 8f)) + 4f;
            float chipsHeight = line.Chips != null && line.Chips.Count > 0 ? ChipRowHeight : 0f;
            return TimelineRowHeight + descHeight + chipsHeight;
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


        /// <summary>
        /// Display priority for the social network graph: partners and blood kin
        /// outrank opinion-derived ties, and ended ties sink last. Needed because
        /// the node list is capped at 8 — without ranking, the capture order
        /// (direct → implied → opinion) would let acquaintances crowd out family.
        /// </summary>



        // v4.7 Legacy (传承) tab — the ownership-transfer chain for equipment.
        // Consumes the read-model snapshot (cachedLegacy) only: no queries, no
        // sorting, no null-guards here. Layout: epithet → verdict → summary KPI →
        // holder table (collapsible past a small cap). All drawing via UIComponents.

        // ---- v4.9 equipment legacy extension tabs (溯源 / 同袍 / 退役) ----

        /// <summary>
        /// 溯源 (Origin): where the thing came from + the maker-chain double
        /// narrative. Consumes only read-model derived views; no raw queries here.
        /// </summary>

        /// <summary>
        /// 同袍共用网络 (CoUse): colonists who used this equipment in parallel with
        /// the current holder, ranked by shared tenure with a share bar.
        /// </summary>

        /// <summary>
        /// 退役仪式 (Decommission): the thing's death record — last holder, last
        /// place, service days, final battle, retire date.
        /// </summary>




        // ---- Detail: placeholder tabs -----------------------------------------







        /// <summary>
        /// Stat cell with a Tiny-font breakdown line under the value. Forwards to
        /// the shared UIComponents.StatCell so all KPI cells share one renderer.
        /// </summary>

        private static Color AlivePill => UITheme.PillGreen;
        private static Color DeadPill => UITheme.Dead;
        private static Color BluePill => UITheme.PillBlue;




        // ---- Display helpers ---------------------------------------------------

        private static readonly string[] CategoryKeys =
        {
            ArchiveCategoryKeys.Pawn,
            ArchiveCategoryKeys.Thing,
            ArchiveCategoryKeys.Battle,
            ArchiveCategoryKeys.Location
        };




        /// <summary>角色本地化标签（自由殖民者 / 奴隶 / 囚犯）。</summary>

        /// <summary>角色徽标颜色：自由=绿 / 奴隶=琥珀 / 囚犯=砖红。</summary>






        /// <summary>
        /// v4.13: localized label for a chronicle event type. Driven by the
        /// ChronicleEventDef taxonomy (LabelCap), never magic per-typeKey strings;
        /// unknown defs fall back to the generic EvOther translation.
        /// </summary>

        /// <summary>
        /// v4.0 timeline glyph per event kind. Driven by ChronicleEventKind so it stays
        /// data-coherent with the Def taxonomy; unknown kinds fall back to a neutral dot.
        /// No magic per-typeKey strings — only the four canonical kinds are branched.
        /// </summary>

        /// <summary>
        /// v4.0 timeline node color by kind (mirrors Def taxonomy, no per-typeKey magic).
        /// </summary>




        /// <summary>
        /// Label for a production card. The production summary is aggregated by
        /// ThingCategory (e.g. "WeaponsRanged") since v4.6.5, so the defName may be
        /// either a concrete ThingDef or a ThingCategoryDef — resolve both before
        /// falling back to the raw key. A category key that no longer exists (mod
        /// removed) degrades to the raw defName without red-text.
        /// </summary>



        /// <summary>
        /// Missing-Def detection helpers (LD-7, P4-6): when an archived object
        /// references a Def that no longer exists (e.g. the user uninstalled
        /// Medieval Overhaul after the object was archived), the UI must render
        /// the raw defName as a placeholder instead of crashing. Each missing
        /// defName is logged at most once per session so the log surfaces the
        /// drift without spam.
        /// </summary>
        private static readonly HashSet<string> loggedMissingDefs = new HashSet<string>();




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
}
