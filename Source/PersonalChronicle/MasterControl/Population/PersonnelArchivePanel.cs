using PersonalChronicle.Archive;
using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;
using ReadModels = PersonalChronicle.Application.ReadModels;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>人员系统中心内部的档案面板；不继承 Window，不注册独立主标签页。
    /// 复用档案查询、缓存与详情渲染，选中及滚动状态由人员中心实例持有。</summary>
    public sealed partial class PersonnelArchivePanel
    {
        private enum MainView
        {
            Overview,
            PawnDetail,
            WeaponDetail,
            EventDetail,
            Manual
        }

        private enum NavTarget
        {
            None,
            Pawn,
            Weapon,
            Event
        }

        private const float Gap = ArchiveUiStyle.Gap;
        private const float RowHeight = 40f;
        private const float TimelineRowHeight = 34f;
        private const float ChipRowHeight = 26f;
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
        private MainView view = MainView.Overview;
        private string detailObjectId;
        private int detailTabIndex;
        private string overviewCategoryFilter;
        private ChronicleEvent cachedEventDetail;

        // ---- Scroll state (one per view; tab switches keep detail scroll) ----
        private Vector2 overviewScroll;
        private Vector2 detailScroll;
        private Vector2 eventScroll;
        private Vector2 manualScroll;
        /// <summary>Scroll-wheel zoom factor for the social network graph (1 = default).</summary>
        private float socialNetworkZoom = 1f;
        /// <summary>Whether the user has scrolled the social graph (disables auto-fit).</summary>
        private bool socialNetworkZoomTouched;
        /// <summary>Left-drag pan offset for the social network graph.</summary>
        private Vector2 socialNetworkPan = Vector2.zero;

        // ---- Cached read views (rebuilt only in RefreshNow) ----
        private readonly Dictionary<string, List<ArchiveObject>> cachedCategoryObjects =
            new Dictionary<string, List<ArchiveObject>>();
        // ⚠ 2026-09-16（方案 A / 1:1）：`expandedLocations` / `expandedBattles` 两个「点击展开」状态集
        // **已删除** —— 预览 `.lcard` / `.bcard` 的内联编年史与伤亡行是**恒定内联**在卡内（无需点击），
        // 卡高 228 / 222.5 已把该区计入。GATE-UI：失效的 UI 状态不应留在窗口上。
        /// <summary>v4.14: location atlas KPI strip (8 cells), aggregated by the Read Model.</summary>
        private ReadModels.LocationKpisView cachedLocationKpis = new ReadModels.LocationKpisView();
        /// <summary>v4.14: per-location event counts for the atlas card sub-line.</summary>
        private Dictionary<string, int> cachedLocationEventCounts = new Dictionary<string, int>();
        /// <summary>v4.14: Battle KPI strip + per-battle card aggregates (Read Model).</summary>
        private ReadModels.BattleKpisView cachedBattleKpis = new ReadModels.BattleKpisView();
        /// <summary>v4.14: significant-relation table rows (importantRel), Read Model.</summary>
        private IReadOnlyList<ReadModels.RelationView> cachedRelations = new List<ReadModels.RelationView>();

        // ---- v4.x 档案总览筛选 ----
        /// <summary>Overview 页面人物筛选模式：0=全部 1=在世 2=已死亡(含归档) 3=已归档(待销毁)。</summary>
        private int overviewPawnFilter = 0;
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
        /// <summary>v5.x：职业事实计数（9 类）—— mirrors <c>DetailSnapshot.FactCounts</c>（生涯子页消费）。</summary>
        private ReadModels.CareerFactCounts cachedFactCounts = new ReadModels.CareerFactCounts();
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

        // P2-6: read-model provider. All section query+sort+null-guard logic is
        // delegated here; the window only consumes immutable snapshots.
        private ReadModels.IArchiveUiDataProvider uiDataProvider = new ReadModels.ArchiveUiDataProvider();

        // v3.1: biography tabs (Stats removed; Work/Skills/… merged into Career/Social).
        // v4.6.6: Timeline tab removed from the Pawn detail view per request
        // (pawn timeline is surfaced via the Home chronicle timeline instead).
        // CHG-20260914-002：补第 5 个 Tab「健康」（预览 #v-pawn 1318 行「末项承载原版健康
        // 模块」，与总 Tab 数 5 一致；绘制见 PersonnelArchivePanel.PawnHealth.cs，数据复用
        // 既有 cachedHealth —— 不重算口径）。
        private static readonly string[] PawnTabKeys =
        {
            "Overview", "Career", "CombatLog", "Social", "Health"
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

        /// <summary>
        /// 设计基准尺寸（1100×720）。数值来源 = <see cref="ArchiveUiStyle.DesignWidth"/> /
        /// <see cref="ArchiveUiStyle.DesignHeight"/> —— 总控中心的嵌入空间预算读同一组常量，
        /// 避免「原版窗口」与「嵌入档」各自维护一套数字（LAND-006 单一来源）。
        /// </summary>




        /// <summary>
        /// <see cref="DoWindowContents"/> 的实体（拆出来只为给外层套 try/catch，**逻辑一字未改**）。
        /// </summary>


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
        /// <summary>
        /// 地点卡高 = **228**（预览 `.lcard` 实测；含四行 `drow` + 「内联编年史」说明行与 `evrow` 行区）。
        /// ⚠ 旧值 176 只够「卡头 + 四行 + 三格生命周期」，编年史只能靠**点击展开的卡下面板**（方案 A 已删）。
        /// </summary>
        private const float LocationCardHeight = 228f;
        /// <summary>
        /// 地点 KPI 条行高 = **48**（预览 `.kstrip` 单格实测 **46.4**；与战役条同一紧凑档）。
        /// ⚠ 旧值 64 是 `StatCell`（值用 `Medium`）的硬下限 —— 两条 KPI 条都比预览高 39%。
        /// 与渲染器**同源**：<see cref="UIComponents.StatCellCompactHeight"/>。
        /// </summary>
        private const float LocationKpiStripHeight = UIComponents.StatCellCompactHeight;

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


        // ---- Cached view records (preformatted text, no per-frame work) --------



        /// <summary>v4.17 体检：时间线单行预格式化视图（缓存刷新时构建，绘制零分配）。</summary>


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
