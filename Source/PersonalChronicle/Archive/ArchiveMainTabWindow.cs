using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
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
    ///   Pawn detail tabs (5): Overview · Timeline · Career · CombatLog · Social.
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

        // P4-1: health summary thresholds (SummaryHealthPercent bands).
        private const float HealthGoodThreshold = 0.6f;
        private const float HealthInjuredThreshold = 0.3f;

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

        // ---- Cached read views (rebuilt only in RefreshNow) ----
        private readonly Dictionary<string, List<ArchiveObject>> cachedCategoryObjects =
            new Dictionary<string, List<ArchiveObject>>();
        private List<RecentLineView> cachedRecentLines = new List<RecentLineView>();
        private List<ImportantCardView> cachedImportantCards = new List<ImportantCardView>();
        private int cachedActivePawnCount;
        private int cachedArchivedPawnCount;
        private int cachedLiveColonistCount;
        private int cachedLiveFreeCount;
        private int cachedLiveSlaveCount;
        private int cachedLivePrisonerCount;

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
        private List<ProductionLineView> cachedProductionLines = new List<ProductionLineView>();
        private ProductionSummaryView cachedProductionSummary =
            new ProductionSummaryView(0, 0f, -1L, new List<ProductionTypeView>());
        private WorkIntensityView cachedWorkIntensity =
            new WorkIntensityView(WorkIntensityEvaluation.Undefined(null, "builtin"), null);
        private IReadOnlyList<WorkIntensityWorkTypeView> cachedIntensityWorkTypes =
            new List<WorkIntensityWorkTypeView>();
        private string cachedDeathKiller;
        private string cachedCraftCrafterId;
        private string cachedCraftCrafterLabel;
        private long cachedCraftTick = -1L;

        // Event view cache.
        private List<TreeLineView> cachedEventTree = new List<TreeLineView>();
        private string cachedEventDescription = string.Empty;

        private long nextRefreshTick;
        private long cachedDataRevision = -1L;

        // Timeline presentation state. These are read-model filters only; they
        // never change what the capture layer persists.
        private bool timelineShowCareer = true;
        private bool timelineShowCombat = true;
        private bool timelineShowSocial = true;
        // Normal is the default: Join/Death/Battle/Social remain visible while
        // routine craft/build rows stay collapsed into career aggregates.
        private ChronicleImportance timelineMinimumImportance = ChronicleImportance.Normal;

        // v3.1: five biography tabs (Stats removed; Work/Skills/… merged into Career/Social).
        private static readonly string[] PawnTabKeys =
        {
            "Overview", "Timeline", "Career", "CombatLog", "Social"
        };

        // v3.1: four item tabs (Stats/Craft merged into Overview; Holders → Custody).
        private static readonly string[] WeaponTabKeys =
        {
            "Overview", "Timeline", "CombatLog", "Custody"
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
            long revision = service.GetDataRevision();
            if (GenTicks.TicksGame < nextRefreshTick && revision == cachedDataRevision)
            {
                return;
            }
            nextRefreshTick = GenTicks.TicksGame + CacheRefreshInterval;
            RefreshNow(service);
            cachedDataRevision = revision;
        }

        private void RefreshNow(IArchiveService service)
        {
            RebuildCategoryCache(service);
            RebuildPawnCounts(service);
            RebuildRecentLines(service);
            RebuildImportantCards(service);
            RebuildDetailCache(service);
            RebuildEventCache(service);
        }

        private void RebuildCategoryCache(IArchiveService service)
        {
            cachedCategoryObjects.Clear();
            cachedCategoryObjects[ArchiveCategoryKeys.Pawn] = service.GetObjectsOfCategory(ArchiveCategoryKeys.Pawn).ToList();
            cachedCategoryObjects[ArchiveCategoryKeys.Thing] = service.GetObjectsOfCategory(ArchiveCategoryKeys.Thing).ToList();
            cachedCategoryObjects[ArchiveCategoryKeys.Battle] = service.GetObjectsOfCategory(ArchiveCategoryKeys.Battle).ToList();
            cachedCategoryObjects[ArchiveCategoryKeys.Location] = service.GetObjectsOfCategory(ArchiveCategoryKeys.Location).ToList();
        }

        private void RebuildPawnCounts(IArchiveService service)
        {
            // All counts come from service contracts — the window renders only,
            // no business logic here (P2-6). Active/archived are the archive
            // snapshot convention (DeathTick); the live colonist count is the
            // independent live-read path with its own 600-tick cache.
            cachedActivePawnCount = service.GetActiveSnapshotCount();
            cachedArchivedPawnCount = service.GetArchivedSnapshotCount();
            cachedLiveColonistCount = service.GetLiveColonistCount();
            service.GetLiveColonistCounts(
                out cachedLiveFreeCount, out cachedLiveSlaveCount, out cachedLivePrisonerCount);
        }

        private void RebuildRecentLines(IArchiveService service)
        {
            cachedRecentLines = new List<RecentLineView>();

            IReadOnlyList<ChronicleEvent> recent = service.GetRecentEvents(RecentRecordCount);
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

        private void RebuildImportantCards(IArchiveService service)
        {
            cachedImportantCards = new List<ImportantCardView>();

            // Honest "important" proxy: the Pawn/Thing objects with the most
            // events (most active in the archive). Battle/Location have no
            // detail view, so they never appear here.
            List<CandidateObject> candidates = new List<CandidateObject>();
            CollectCandidates(ArchiveCategoryKeys.Pawn, service, candidates);
            CollectCandidates(ArchiveCategoryKeys.Thing, service, candidates);

            List<CandidateObject> sorted = candidates
                .OrderByDescending(c => c.EventCount)
                .Take(ImportantCardCount)
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                ArchiveObject obj = sorted[i].Object;
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

        private static void CollectCandidates(string categoryKey, IArchiveService service, List<CandidateObject> candidates)
        {
            // P3-6: single query (the old code called GetObjectsOfCategory twice).
            IReadOnlyList<ArchiveObject> objects = service.GetObjectsOfCategory(categoryKey);
            if (objects == null || objects.Count == 0)
            {
                return;
            }
            for (int i = 0; i < objects.Count; i++)
            {
                ArchiveObject obj = objects[i];
                if (obj == null)
                {
                    continue;
                }
                int count = 0;
                IReadOnlyList<ChronicleEvent> events = service.GetEventsFor(obj.StableId);
                if (events != null)
                {
                    count = events.Count;
                }
                candidates.Add(new CandidateObject { Object = obj, EventCount = count });
            }
        }

        private void RebuildDetailCache(IArchiveService service)
        {
            ClearDetailCache();
            if ((view != MainView.PawnDetail && view != MainView.WeaponDetail)
                || string.IsNullOrEmpty(detailObjectId))
            {
                return;
            }

            cachedDetailObject = service.GetObject(detailObjectId);
            if (cachedDetailObject == null)
            {
                // Object vanished (data cleaned up): safe fallback to overview.
                view = MainView.Overview;
                overviewCategoryFilter = null;
                return;
            }

            IReadOnlyList<ChronicleEvent> events = service.GetEventsFor(detailObjectId);
            if (events != null && events.Count > 0)
            {
                List<ChronicleEvent> sorted = events.OrderBy(ev => ev.Tick).ToList();
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

            // These read models are independent of event history. A pawn with
            // sampled work but no captured event must still render Career data.
            BuildProductionCache();
            cachedProductionSummary = service.GetProductionSummary(detailObjectId);
            IWorkIntensityService intensityService = service as IWorkIntensityService;
            if (intensityService != null && cachedDetailObject is PawnObject)
            {
                cachedWorkIntensity = intensityService.GetWorkIntensity(detailObjectId);
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
            cachedProductionLines = new List<ProductionLineView>();
            cachedProductionSummary = new ProductionSummaryView(0, 0f, -1L, new List<ProductionTypeView>());
            cachedWorkIntensity = new WorkIntensityView(
                WorkIntensityEvaluation.Undefined(null, "builtin"), null);
            cachedIntensityWorkTypes = new List<WorkIntensityWorkTypeView>();
            cachedDeathKiller = null;
            cachedCraftCrafterId = null;
            cachedCraftCrafterLabel = null;
            cachedCraftTick = -1L;
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
                        string victimLabel = ev.Primary != null
                            ? ResolveRefLabel(ev.Primary, service)
                            : EventName(ev);
                        CombatLineView kill = new CombatLineView
                        {
                            DateText = FormatDate(ev.Tick),
                            TitleText = victimLabel,
                            SubText = string.IsNullOrEmpty(killerText)
                                ? "PersonalChronicle.UI.KpiKills".Translate().ToString()
                                : killerText,
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
        /// LD-1: aggregates the detail object's Crafted/Built events into a
        /// per-ThingDef table (item type, count, last tick). Classification is
        /// Def-driven via IsCraftEvent/IsBuiltEvent — never TypeKey substrings.
        /// The ThingDefName is parsed from the Primary ObjectRef stable id
        /// ("&lt;defName&gt;:&lt;thingIDNumber&gt;", the shape ArchiveService writes).
        /// Runs on the refresh cadence (RebuildDetailCache), never per frame.
        /// </summary>
        private void BuildProductionCache()
        {
            cachedProductionLines = new List<ProductionLineView>();
            if (cachedDetailRawEvents == null)
            {
                return;
            }
            Dictionary<string, ProductionLineView> byDef = new Dictionary<string, ProductionLineView>();
            for (int i = 0; i < cachedDetailRawEvents.Count; i++)
            {
                ChronicleEvent ev = cachedDetailRawEvents[i];
                if (ev == null || (!IsCraftEvent(ev) && !IsBuiltEvent(ev)))
                {
                    continue;
                }
                ObjectRef primary = ev.Primary;
                if (primary == null || string.IsNullOrEmpty(primary.StableId))
                {
                    continue;
                }
                string defName = ThingDefNameFromStableId(primary.StableId);
                if (string.IsNullOrEmpty(defName))
                {
                    continue;
                }
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
                        Label = ThingDefLabel(defName),
                        Count = 1,
                        LastTick = ev.Tick,
                        StableId = primary.StableId
                    };
                }
            }
            cachedProductionLines = byDef.Values.OrderByDescending(line => line.LastTick).ToList();
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

        private void RebuildEventCache(IArchiveService service)
        {
            cachedEventTree = new List<TreeLineView>();
            cachedEventDescription = string.Empty;
            if (view != MainView.EventDetail || cachedEventDetail == null)
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

            // Follow-up events: later events of the primary object (honest
            // derivation from the event index, capped at 3).
            List<ChronicleEvent> followups = new List<ChronicleEvent>();
            if (primary != null && !string.IsNullOrEmpty(primary.StableId))
            {
                IReadOnlyList<ChronicleEvent> evs = service.GetEventsFor(primary.StableId);
                if (evs != null)
                {
                    followups = evs
                        .Where(e => e != null && e.Tick > cachedEventDetail.Tick)
                        .OrderBy(e => e.Tick)
                        .Take(3)
                        .ToList();
                }
            }
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

        private void OpenPawnDetail(IArchiveService service, string stableId)
        {
            detailObjectId = stableId;
            view = MainView.PawnDetail;
            detailTabIndex = 0;
            cachedEventDetail = null;
            detailScroll = Vector2.zero;
            RebuildDetailCache(service);
        }

        private void OpenWeaponDetail(IArchiveService service, string stableId)
        {
            detailObjectId = stableId;
            view = MainView.WeaponDetail;
            detailTabIndex = 0;
            cachedEventDetail = null;
            detailScroll = Vector2.zero;
            RebuildDetailCache(service);
        }

        private void OpenEventDetail(IArchiveService service, ChronicleEvent ev)
        {
            cachedEventDetail = ev;
            view = MainView.EventDetail;
            detailObjectId = null;
            ClearDetailCache();
            eventScroll = Vector2.zero;
            RebuildEventCache(service);
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
            ArchiveUiStyle.DrawRule(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), ArchiveUiStyle.Accent);
        }

        private void DrawSidebar(Rect rect, IArchiveService service)
        {
            ArchiveUiStyle.DrawPanel(rect);
            Rect inner = rect.ContractedBy(10f);

            const float itemHeight = 30f;
            const float itemGap = 3f;
            float y = inner.y;

            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Dim;
            Widgets.Label(new Rect(inner.x, y, inner.width, 18f),
                "PersonalChronicle.UI.Navigation".Translate().ToString());
            GUI.color = Color.white;
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
            GUI.color = Color.white;
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
            GUI.color = Color.white;
            y += 20f;

            y = DrawSidebarTool(inner, y, itemHeight, itemGap, "PersonalChronicle.UI.Favorites");
            y = DrawSidebarTool(inner, y, itemHeight, itemGap, "PersonalChronicle.UI.Milestones");
            DrawSidebarTool(inner, y, itemHeight, itemGap, "PersonalChronicle.UI.Search");
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
            Rect item = new Rect(inner.x, y, inner.width, height);
            ArchiveUiStyle.DrawSelectedNavigation(item, false);
            GUI.color = ArchiveUiStyle.Dim;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(item.x + 13f, item.y + 7f, item.width - 20f, 20f), labelKey.Translate().ToString());
            GUI.color = Color.white;
            TooltipHandler.TipRegion(item, "PersonalChronicle.UI.ToolsPlaceholder".Translate().ToString());
            return y + height + gap;
        }

        private static bool DrawSidebarItem(float x, float y, float width, float height, string label, string countText, bool selected)
        {
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
                Text.Anchor = TextAnchor.UpperLeft;
            }
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
            GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 18f),
                "PersonalChronicle.UI.ArchiveHomeDesc".Translate().ToString());
            GUI.color = Color.white;
            y += 26f;

            y = DrawHomeStats(viewRect, y);
            y += 20f;

            float leftWidth = viewRect.width * 0.62f;
            float rightWidth = viewRect.width - leftWidth - 16f;
            Rect leftRect = new Rect(viewRect.x, y, leftWidth, viewHeight - (y - viewRect.y));
            Rect rightRect = new Rect(viewRect.x + leftWidth + 16f, y, rightWidth, viewHeight - (y - viewRect.y));

            DrawRecentHistory(leftRect, service);
            DrawImportantArchives(rightRect, service);

            Widgets.EndScrollView();
        }

        private static float ComputeHomeHeight(float width)
        {
            // stats strip (2 rows max) + two columns.
            float statsHeight = 150f;
            float columnsHeight = 6 * TimelineRowHeight + 40f + 4 * 70f + 60f;
            return 4f + 30f + 26f + statsHeight + 20f + columnsHeight + 20f;
        }

        private float DrawHomeStats(Rect viewRect, float y)
        {
            float statHeight = 64f;
            float gap = 8f;
            int perRow = 6;
            float statWidth = (viewRect.width - (perRow - 1) * gap) / perRow;

            DrawStatCell(new Rect(viewRect.x, y, statWidth, statHeight),
                CategoryLabel(ArchiveCategoryKeys.Pawn), cachedCategoryObjects.GetCount(ArchiveCategoryKeys.Pawn));
            DrawStatCell(new Rect(viewRect.x + (statWidth + gap), y, statWidth, statHeight),
                CategoryLabel(ArchiveCategoryKeys.Thing), cachedCategoryObjects.GetCount(ArchiveCategoryKeys.Thing));
            DrawStatCell(new Rect(viewRect.x + 2 * (statWidth + gap), y, statWidth, statHeight),
                CategoryLabel(ArchiveCategoryKeys.Battle), cachedCategoryObjects.GetCount(ArchiveCategoryKeys.Battle));
            DrawStatCell(new Rect(viewRect.x + 3 * (statWidth + gap), y, statWidth, statHeight),
                CategoryLabel(ArchiveCategoryKeys.Location), cachedCategoryObjects.GetCount(ArchiveCategoryKeys.Location));
            DrawStatCell(new Rect(viewRect.x + 4 * (statWidth + gap), y, statWidth, statHeight),
                "PersonalChronicle.UI.CurrentColonists".Translate().ToString(), cachedLiveColonistCount,
                "PersonalChronicle.UI.LiveBreakdown"
                    .Translate(cachedLiveFreeCount, cachedLiveSlaveCount, cachedLivePrisonerCount)
                    .ToString());
            string pawnRecordsSubLabel =
                "PersonalChronicle.UI.ActiveRecords".Translate().ToString() + " " + cachedActivePawnCount
                + " · " + "PersonalChronicle.UI.ArchivedRecords".Translate().ToString() + " " + cachedArchivedPawnCount;
            DrawStatCell(new Rect(viewRect.x + 5 * (statWidth + gap), y, statWidth, statHeight),
                "PersonalChronicle.UI.ArchivePawnRecords".Translate().ToString(),
                cachedActivePawnCount + cachedArchivedPawnCount, pawnRecordsSubLabel);
            return y + statHeight;
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

            const float cardHeight = 58f;
            const float cardGap = 8f;
            for (int i = 0; i < cachedImportantCards.Count; i++)
            {
                ImportantCardView card = cachedImportantCards[i];
                Rect cardRect = new Rect(rect.x, y, rect.width, cardHeight);
                ArchiveUiStyle.DrawCard(cardRect, ArchiveUiStyle.Accent);

                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Accent;
                Widgets.Label(new Rect(cardRect.x + 10f, cardRect.y + 4f, cardRect.width - 20f, 16f), card.TagLabel);
                GUI.color = Color.white;

                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(cardRect.x + 10f, cardRect.y + 20f, cardRect.width - 20f, 20f), card.Label);

                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(cardRect.x + 10f, cardRect.y + 40f, cardRect.width - 20f, 16f), card.SubLabel);
                GUI.color = Color.white;

                if (card.Target != NavTarget.None && Widgets.ButtonInvisible(cardRect))
                {
                    NavigateTarget(service, card.Target, card.StableId, null);
                }
                y += cardHeight + cardGap;
            }
        }

        // ---- Overview ----------------------------------------------------------

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
            GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 18f),
                "PersonalChronicle.UI.OverviewDesc".Translate().ToString());
            GUI.color = Color.white;
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
            const float cardWidth = 190f;
            const float cardHeight = 70f;
            const float gap = 12f;
            int perRow = Mathf.Max(1, (int)((width + gap) / (cardWidth + gap)));

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

            const float cardWidth = 190f;
            const float cardHeight = 70f;
            const float gap = 12f;
            int perRow = Mathf.Max(1, (int)((viewRect.width + gap) / (cardWidth + gap)));

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
                    viewRect.x + col * (cardWidth + gap),
                    y + row * (cardHeight + gap),
                    cardWidth, cardHeight);

                ArchiveUiStyle.DrawCard(card, ArchiveCardAccent(obj));
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(card.x + 8f, card.y + 4f, card.width - 16f, 16f),
                    clickable ? CategoryLabel(categoryKey) : "PersonalChronicle.UI.StatsOnlyNote".Translate().ToString());
                GUI.color = Color.white;

                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(card.x + 8f, card.y + 22f, card.width - 16f, 20f), ObjectDisplayLabel(obj));

                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(card.x + 8f, card.y + 44f, card.width - 16f, 18f), ObjectSubLabel(obj));
                GUI.color = Color.white;

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

            int rows = (objects.Count - 1) / perRow + 1;
            return y + rows * (cardHeight + gap) + 14f;
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
                    280f + intensityRows * 94f + productionSummaryHeight
                    + productionRows * 58f + 16 * 26f);
            }
            if (tab == "CombatLog")
            {
                return 260f + cachedKillLines.Count * (TimelineRowHeight + 2f)
                    + cachedBattleLines.Count * (TimelineRowHeight + 2f);
            }
            if (tab == "Social")
            {
                int relationCount = cachedDetailObject is PawnObject pawn && pawn.Relations != null
                    ? pawn.Relations.Count : 0;
                int socialEvents = cachedDetailRawEvents.Count(IsSocialEvent);
                return 300f + relationCount * 8f + socialEvents * (TimelineRowHeight + 2f)
                    + cachedLinkedObjects.Count * 24f;
            }
            // Overview contains live skills/health/relations and may vary by
            // DLC or mod; leave generous room and keep the child scrollable.
            return Mathf.Max(panel.height, 920f);
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

            return y + 54f;
        }

        private static void DrawMetaLine(Rect rect, float y, string text)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            Widgets.Label(new Rect(rect.x, y, rect.width, 18f), text);
            GUI.color = Color.white;
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
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(tab, label);
                Text.Anchor = TextAnchor.UpperLeft;
                if (selected)
                {
                    ArchiveUiStyle.DrawRule(new Rect(tab.x, tab.yMax - 2f, tab.width, 2f), ArchiveUiStyle.Accent);
                }
                if (Widgets.ButtonInvisible(tab))
                {
                    detailTabIndex = i;
                    detailScroll = Vector2.zero;
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
                case "Custody":
                    DrawHoldersTab(panel, service);
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
        private void DrawPawnOverview(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            if (!(cachedDetailObject is PawnObject pawn))
            {
                return;
            }

            // ---- Headline KPI (6 cells) ----
            IReadOnlyList<WorkTimeStatView> workStats = service.GetWorkTimeStats(detailObjectId);
            long totalWorkTicks = 0L;
            string primaryWork = "—";
            if (workStats != null && workStats.Count > 0)
            {
                for (int i = 0; i < workStats.Count; i++)
                {
                    totalWorkTicks += workStats[i].Ticks;
                }
                primaryWork = WorkTypeLabel(workStats[0].WorkTypeDefName);
            }
            int productionCount = cachedProductionSummary != null
                ? cachedProductionSummary.TotalQuantity
                : 0;
            int battleCount = 0;
            int killCount = 0;
            CountCombatKpis(out battleCount, out killCount);
            int relationCount = CountSignificantRelations(pawn);

            float gap = 8f;
            int perRow = 6;
            float cellW = (rect.width - (perRow - 1) * gap) / perRow;
            float cellH = 64f;
            DrawStatCell(new Rect(rect.x, y, cellW, cellH),
                "PersonalChronicle.UI.EventCount".Translate().ToString(), cachedDetailEvents.Count);
            DrawStatCell(new Rect(rect.x + (cellW + gap), y, cellW, cellH),
                "PersonalChronicle.UI.TotalWorkHours".Translate().ToString(),
                (int)(totalWorkTicks / RimWorld.GenDate.TicksPerHour), primaryWork);
            DrawStatCell(new Rect(rect.x + 2 * (cellW + gap), y, cellW, cellH),
                "PersonalChronicle.UI.Tab.Production".Translate().ToString(), productionCount,
                cachedProductionSummary != null
                    ? FormatMarketValue(cachedProductionSummary.TotalMarketValue)
                    : "—");
            DrawStatCell(new Rect(rect.x + 3 * (cellW + gap), y, cellW, cellH),
                "PersonalChronicle.UI.KpiBattles".Translate().ToString(), battleCount);
            DrawStatCell(new Rect(rect.x + 4 * (cellW + gap), y, cellW, cellH),
                "PersonalChronicle.UI.KpiKills".Translate().ToString(), killCount);
            DrawStatCell(new Rect(rect.x + 5 * (cellW + gap), y, cellW, cellH),
                "PersonalChronicle.UI.KpiRelations".Translate().ToString(), relationCount);
            y += cellH + 12f;

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            Widgets.Label(new Rect(rect.x, y, rect.width, 18f),
                "PersonalChronicle.UI.ArchiveDataNote".Translate().ToString());
            GUI.color = Color.white;
            y += 22f;

            // ---- Lifecycle ----
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Lifecycle".Translate().ToString());
            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Status".Translate().ToString(),
                pawn.IsArchived
                    ? "PersonalChronicle.UI.Dead".Translate().ToString()
                    : "PersonalChronicle.UI.Alive".Translate().ToString());
            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Kind".Translate().ToString(), KindLabel(pawn));
            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Faction".Translate().ToString(), FactionLabel(pawn));
            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Backstory".Translate().ToString(), BackstoryLabel(pawn));
            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.JoinDate".Translate().ToString(), FormatDate(pawn.JoinTick));
            if (pawn.JoinTick > 0L && !pawn.IsArchived)
            {
                int days = Mathf.Max(0, (int)((Find.TickManager.TicksGame - pawn.JoinTick)
                    / (long)RimWorld.GenDate.TicksPerDay));
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.DaysInColony".Translate().ToString(), days.ToString());
            }
            else if (pawn.JoinTick > 0L && pawn.DeathTick > 0L)
            {
                int days = Mathf.Max(0, (int)((pawn.DeathTick - pawn.JoinTick)
                    / (long)RimWorld.GenDate.TicksPerDay));
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.DaysInColony".Translate().ToString(), days.ToString());
            }
            if (pawn.IsArchived)
            {
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.DeathDate".Translate().ToString(), FormatDate(pawn.DeathTick));
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.DeathCause".Translate().ToString(), CauseLabel(pawn.DeathCauseKey));
                if (!string.IsNullOrEmpty(cachedDeathKiller))
                {
                    y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Killer".Translate().ToString(), cachedDeathKiller);
                }
            }

            // ---- Career blurb ----
            y += 6f;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.CareerSummary".Translate().ToString());
            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.PrimaryWork".Translate().ToString(), primaryWork);
            string secondary = workStats != null && workStats.Count > 1
                ? WorkTypeLabel(workStats[1].WorkTypeDefName) : "—";
            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.SecondaryWork".Translate().ToString(), secondary);
            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.TotalWorkHours".Translate().ToString(),
                FormatWorkHours(totalWorkTicks));

            // ---- Footprint (P3: place history ledger) ----
            y += 6f;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Footprint".Translate().ToString());
            string place = !string.IsNullOrEmpty(pawn.PrimaryPlaceDefName)
                ? BiomeLabel(pawn.PrimaryPlaceDefName)
                : "PersonalChronicle.UI.UnknownPlace".Translate().ToString();
            LocationInfo liveLoc = service.GetLiveLocation(detailObjectId);
            if (liveLoc != null && liveLoc.Kind == LocationKind.Map && !string.IsNullOrEmpty(liveLoc.MapDefName))
            {
                place = BiomeLabel(liveLoc.MapDefName);
            }
            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.PrimaryPlace".Translate().ToString(), place);
            int placeCount = pawn.PlaceHistory != null ? pawn.PlaceHistory.Count : 0;
            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.PlaceVisitCount".Translate().ToString(), placeCount.ToString());
            y = DrawPlaceHistoryTable(rect, y, pawn, 4);

            // ---- Key events (last 3) ----
            y += 6f;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.KeyEvents".Translate().ToString());
            int start = Mathf.Max(0, cachedDetailEvents.Count - 3);
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
                GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
                Widgets.Label(new Rect(rect.x, y, rect.width, 18f),
                    "PersonalChronicle.UI.NoPlaceHistory".Translate().ToString());
                GUI.color = Color.white;
                return y + 22f;
            }
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            Widgets.Label(new Rect(rect.x + 6f, y, rect.width * 0.4f, 16f),
                "PersonalChronicle.UI.PlaceName".Translate().ToString());
            Widgets.Label(new Rect(rect.x + rect.width * 0.42f, y, rect.width * 0.28f, 16f),
                "PersonalChronicle.UI.PlaceEnter".Translate().ToString());
            Widgets.Label(new Rect(rect.x + rect.width * 0.72f, y, rect.width * 0.26f, 16f),
                "PersonalChronicle.UI.PlaceLeave".Translate().ToString());
            GUI.color = Color.white;
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
            return "PersonalChronicle.UI.MarketValueFormat".Translate(value.ToString("0.0")).ToString();
        }

        private static void DrawMetricCard(Rect rect, string label, string value, string subLabel)
        {
            ArchiveUiStyle.DrawCard(rect, ArchiveUiStyle.Info);
            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Muted;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 16f), label);
            Text.Font = GameFont.Medium;
            GUI.color = ArchiveUiStyle.Text;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 24f, rect.width - 20f, 26f), value);
            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Muted;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 51f, rect.width - 20f, 16f), subLabel);
            GUI.color = Color.white;
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

            // P2: kills then battles (same source as overview KPI).
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.KillRecords".Translate().ToString());
            y = DrawCombatLineList(rect, y, service, cachedKillLines, "PersonalChronicle.UI.NoKillRecords");
            y += 8f;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.BattleRecords".Translate().ToString());
            y = DrawCombatLineList(rect, y, service, cachedBattleLines, "PersonalChronicle.UI.NoBattleRecords");
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            Widgets.Label(new Rect(rect.x, y + 4f, rect.width, 18f),
                "PersonalChronicle.UI.CombatFooter".Translate(cachedBattleLines.Count, cachedKillLines.Count).ToString());
            GUI.color = Color.white;
        }

        private void DrawWeaponCombat(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.KillRecords".Translate().ToString());
            y = DrawCombatLineList(rect, y, service, cachedKillLines, "PersonalChronicle.UI.NoKillRecords");
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            Widgets.Label(new Rect(rect.x, y + 4f, rect.width, 18f),
                "PersonalChronicle.UI.CombatFooter".Translate(cachedBattleLines.Count, cachedKillLines.Count).ToString());
            GUI.color = Color.white;
        }

        private float DrawCombatLineList(Rect rect, float y, IArchiveService service, List<CombatLineView> lines, string emptyKey)
        {
            if (lines == null || lines.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f), emptyKey.Translate().ToString());
                return y + 26f;
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
                GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
                Widgets.Label(new Rect(row.x + row.width - 196f, row.y + 6f, 190f, 18f), link.CategoryLabel);
                GUI.color = Color.white;
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
                GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
                Widgets.Label(new Rect(row.x + row.width - 196f, row.y + 6f, 192f, 18f), part);
                GUI.color = Color.white;
                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
                y += TimelineRowHeight;
            }
        }

        private void DrawRelations(Rect rect, IArchiveService service)
        {
            Pawn pawn = service.GetLivePawn(detailObjectId);
            if (pawn == null || pawn.relations == null || pawn.relations.DirectRelations == null)
            {
                DrawNoLiveData(rect);
                return;
            }

            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Relations".Translate().ToString());

            List<DirectPawnRelation> relations = pawn.relations.DirectRelations;
            if (relations.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoRelations".Translate().ToString());
                return;
            }

            for (int i = 0; i < relations.Count; i++)
            {
                DirectPawnRelation rel = relations[i];
                if (rel == null)
                {
                    continue;
                }
                string otherName = rel.otherPawn != null ? rel.otherPawn.LabelShort : string.Empty;
                string relLabel = rel.def != null && !string.IsNullOrEmpty(rel.def.label) ? rel.def.label : (rel.def != null ? rel.def.defName : string.Empty);
                string status = rel.otherPawn != null && rel.otherPawn.Dead
                    ? "PersonalChronicle.UI.Dead".Translate().ToString()
                    : "PersonalChronicle.UI.Alive".Translate().ToString();

                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x + 4f, row.y + 3f, row.width - 260f, 20f), otherName);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
                Widgets.Label(new Rect(row.x + row.width - 256f, row.y + 6f, 180f, 18f), relLabel);
                Widgets.Label(new Rect(row.x + row.width - 72f, row.y + 6f, 68f, 18f), status);
                GUI.color = Color.white;
                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
                y += TimelineRowHeight;
            }
        }

        private void DrawNoLiveData(Rect rect)
        {
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f),
                "PersonalChronicle.UI.NoLiveData".Translate().ToString());
            GUI.color = Color.white;
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
            GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            Widgets.Label(new Rect(rect.x + 6f, y, rect.width - 300f, 18f),
                "PersonalChronicle.UI.ProductionType".Translate().ToString());
            Widgets.Label(new Rect(rect.x + rect.width - 280f, y, 90f, 18f),
                "PersonalChronicle.UI.ProductionCount".Translate().ToString());
            Widgets.Label(new Rect(rect.x + rect.width - 190f, y, 180f, 18f),
                "PersonalChronicle.UI.ProductionLastTime".Translate().ToString());
            GUI.color = Color.white;
            y += 22f;

            for (int i = 0; i < cachedProductionLines.Count; i++)
            {
                ProductionLineView line = cachedProductionLines[i];
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
                    float width = (rect.width - 8f) / 2f;
                    float x = rect.x + (i % 2) * (width + 8f);
                    float cardY = y + (i / 2) * 58f;
                    ArchiveUiStyle.DrawCard(new Rect(x, cardY, width, 50f), ArchiveUiStyle.Accent);
                    Text.Font = GameFont.Small;
                    Widgets.Label(new Rect(x + 10f, cardY + 6f, width - 20f, 20f), ThingDefLabel(type.DefName));
                    Text.Font = GameFont.Tiny;
                    GUI.color = ArchiveUiStyle.Muted;
                    Widgets.Label(new Rect(x + 10f, cardY + 28f, width - 20f, 16f),
                        "PersonalChronicle.UI.ProductionCard".Translate(type.Quantity, FormatMarketValue(type.MarketValue)).ToString());
                    GUI.color = Color.white;
                }
                y += ((types.Count + 1) / 2) * 58f;
            }
        }

        private void DrawWorkIntensityHeader(Rect rect, ref float y, IArchiveService service)
        {
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.CareerSummary".Translate().ToString());
            WorkIntensityView intensity = cachedWorkIntensity;
            bool hasTier = intensity != null && intensity.IsDefined;
            bool isEstimated = hasTier && intensity.IsEstimated;
            Color tierColor = ParseIntensityColor(intensity != null ? intensity.ColorHex : null);
            Rect hero = new Rect(rect.x, y, rect.width, 92f);
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
            ArchiveUiStyle.DrawBadge(badge,
                string.IsNullOrEmpty(badgeStatus) ? badgeCode : badgeStatus + " · " + badgeCode,
                hasTier ? tierColor : ArchiveUiStyle.Muted);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(badge.x + 8f, badge.y + 30f, badge.width - 16f, 22f),
                hasTier && !string.IsNullOrEmpty(intensity.LabelKey)
                    ? TranslateIntensityKey(intensity.LabelKey, intensity.DisplayCode)
                    : "PersonalChronicle.UI.Intensity.SampleInsufficient".Translate().ToString());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Muted;
            Widgets.Label(new Rect(badge.x + 8f, badge.y + 57f, badge.width - 16f, 16f),
                hasTier && !string.IsNullOrEmpty(intensity.TagKey)
                    ? TranslateIntensityKey(intensity.TagKey,
                        "PersonalChronicle.UI.Intensity.ObservedWindow".Translate().ToString())
                    : "PersonalChronicle.UI.Intensity.ObservedWindow".Translate(
                        FormatDays(intensity != null ? intensity.ObservedDays : 0d)).ToString());
            GUI.color = Color.white;

            float gap = 8f;
            float statsX = badge.xMax + 10f;
            float statsWidth = hero.xMax - statsX - 8f;
            float cellWidth = (statsWidth - gap * 2f) / 3f;
            DrawMetricCard(new Rect(statsX, hero.y + 8f, cellWidth, 76f),
                "PersonalChronicle.UI.TotalWorkHours".Translate().ToString(),
                FormatHours(intensity != null ? intensity.TotalHours : 0d),
                "PersonalChronicle.UI.Intensity.ObservedWindow".Translate(
                    FormatDays(intensity != null ? intensity.ObservedDays : 0d)).ToString());
            DrawMetricCard(new Rect(statsX + cellWidth + gap, hero.y + 8f, cellWidth, 76f),
                "PersonalChronicle.UI.Intensity.Daily".Translate().ToString(),
                FormatHours(intensity != null ? intensity.DailyHours : 0d),
                "PersonalChronicle.UI.Intensity.MonthlyEst".Translate(
                    FormatHours(intensity != null ? intensity.MonthlyHours : 0d)).ToString());
            DrawMetricCard(new Rect(statsX + 2f * (cellWidth + gap), hero.y + 8f, cellWidth, 76f),
                "PersonalChronicle.UI.Intensity.Weekly".Translate().ToString(),
                FormatHours(intensity != null ? intensity.WeeklyHours : 0d),
                BuildIntensityRelativeLabel(intensity));
            y += hero.height + 8f;

            IWorkIntensityService intensityService = service as IWorkIntensityService;
            if (intensityService == null)
            {
                return;
            }
            IReadOnlyList<WorkIntensityTierView> tiers = intensityService.GetIntensityTiers();
            if (tiers == null || tiers.Count == 0)
            {
                return;
            }
            float rungWidth = (rect.width - (tiers.Count - 1) * 2f) / tiers.Count;
            for (int i = 0; i < tiers.Count; i++)
            {
                WorkIntensityTierView tier = tiers[i];
                Rect rung = new Rect(rect.x + i * (rungWidth + 2f), y, rungWidth, 28f);
                Color color = ParseIntensityColor(tier.ColorHex);
                bool current = intensity != null && intensity.IsDefined
                    && intensity.TierDefName == tier.DefName;
                Widgets.DrawBoxSolid(rung, current ? color : new Color(color.r, color.g, color.b, 0.28f));
                if (current)
                {
                    ArchiveUiStyle.DrawBorder(rung, ArchiveUiStyle.Text);
                }
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = current ? ArchiveUiStyle.Text : ArchiveUiStyle.Muted;
                Widgets.Label(rung, tier.DisplayCode ?? tier.DefName);
                GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
                TooltipHandler.TipRegion(rung,
                    TranslateIntensityKey(tier.LabelKey, tier.DisplayCode ?? tier.DefName));
        }
            y += 36f;
        }

        private void DrawWorkIntensityCards(Rect rect, ref float y)
        {
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.WorkTime".Translate().ToString());
            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Muted;
            Widgets.Label(new Rect(rect.x, y, rect.width, 28f),
                "PersonalChronicle.UI.WorkTimeFootnote".Translate().ToString());
            GUI.color = Color.white;
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
            const float cardHeight = 86f;
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
                Widgets.Label(new Rect(card.x + 10f, card.y + 7f, card.width - 100f, 20f),
                    WorkTypeLabel(row.WorkTypeDefName));
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(card.x + card.width - 92f, card.y + 5f, 80f, 24f),
                    FormatWorkHours(row.Ticks));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                string rank = row.Rank > 0
                    ? "PersonalChronicle.UI.Intensity.Rank".Translate(row.Rank, row.PopulationCount).ToString()
                    : "PersonalChronicle.UI.Intensity.RankUnknown".Translate().ToString();
                Widgets.Label(new Rect(card.x + 10f, card.y + 31f, card.width - 20f, 16f), rank);
                Widgets.Label(new Rect(card.x + 10f, card.y + 49f, card.width - 20f, 16f),
                    "PersonalChronicle.UI.Intensity.WorkShare".Translate(
                        Mathf.RoundToInt(row.Share01 * 100f),
                        Mathf.RoundToInt(row.RelativeToMaximum01 * 100f)).ToString());
                GUI.color = Color.white;
                Widgets.FillableBar(new Rect(card.x + 10f, card.y + 70f, card.width - 20f, 6f),
                    Mathf.Clamp01(row.Share01));
            }
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
                GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
                Widgets.Label(new Rect(row.x + row.width - 196f, row.y + 6f, 190f, 18f),
                    "PersonalChronicle.UI.SharedEvents".Translate().ToString());
                GUI.color = Color.white;
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
            const float panelHeight = 246f;
            Rect panel = new Rect(rect.x, y, rect.width, panelHeight);
            ArchiveUiStyle.DrawPanel(panel, ArchiveUiStyle.PanelRaised);

            List<SocialNodeView> nodes = new List<SocialNodeView>();
            HashSet<string> seen = new HashSet<string>();
            if (pawn != null && pawn.Relations != null)
            {
                for (int i = pawn.Relations.Count - 1; i >= 0 && nodes.Count < 8; i--)
                {
                    SignificantRelation relation = pawn.Relations[i];
                    if (relation == null || string.IsNullOrEmpty(relation.OtherStableId)
                        || !seen.Add(relation.OtherStableId))
                    {
                        continue;
                    }
                    ArchiveObject other = service.GetObject(relation.OtherStableId);
                    nodes.Add(new SocialNodeView
                    {
                        StableId = relation.OtherStableId,
                        Label = other != null ? ObjectDisplayLabel(other) : relation.OtherLabel,
                        RelationLabel = RelationDefLabel(relation.RelationDefName),
                        Active = relation.IsActive
                    });
                }
            }
            if (nodes.Count == 0)
            {
                for (int i = 0; i < cachedLinkedObjects.Count && nodes.Count < 8; i++)
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
                        Active = true
                    });
                }
            }

            Vector2 center = new Vector2(panel.center.x, panel.y + 123f);
            float nodeWidth = Mathf.Min(160f, Mathf.Max(110f, panel.width * 0.22f));
            float radiusX = Mathf.Max(88f, (panel.width - nodeWidth) * 0.38f);
            float radiusY = 82f;
            for (int i = 0; i < nodes.Count; i++)
            {
                float angle = -Mathf.PI / 2f + (Mathf.PI * 2f * i / nodes.Count);
                Vector2 nodeCenter = new Vector2(
                    center.x + Mathf.Cos(angle) * radiusX,
                    center.y + Mathf.Sin(angle) * radiusY);
                Widgets.DrawLine(center, nodeCenter,
                    nodes[i].Active ? ArchiveUiStyle.Accent : ArchiveUiStyle.Border,
                    nodes[i].Active ? 2f : 1f);
            }

            Rect centerRect = new Rect(center.x - 88f, center.y - 25f, 176f, 50f);
            ArchiveUiStyle.DrawCard(centerRect, ArchiveUiStyle.Accent);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(centerRect, pawn != null ? ObjectDisplayLabel(pawn) : "—");
            Text.Anchor = TextAnchor.UpperLeft;

            for (int i = 0; i < nodes.Count; i++)
            {
                float angle = -Mathf.PI / 2f + (Mathf.PI * 2f * i / nodes.Count);
                Vector2 nodeCenter = new Vector2(
                    center.x + Mathf.Cos(angle) * radiusX,
                    center.y + Mathf.Sin(angle) * radiusY);
                Rect nodeRect = new Rect(nodeCenter.x - nodeWidth / 2f, nodeCenter.y - 23f, nodeWidth, 46f);
                ArchiveUiStyle.DrawCard(nodeRect, nodes[i].Active ? ArchiveUiStyle.Info : ArchiveUiStyle.Muted);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(nodeRect.x + 6f, nodeRect.y + 4f, nodeRect.width - 12f, 20f), nodes[i].Label);
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(nodeRect.x + 6f, nodeRect.y + 24f, nodeRect.width - 12f, 16f), nodes[i].RelationLabel);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonInvisible(nodeRect))
                {
                    NavigateTarget(service, NavTarget.Pawn, nodes[i].StableId, null);
                }
            }
            if (nodes.Count == 0)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(panel, "PersonalChronicle.UI.NoSignificantRelations".Translate().ToString());
                Text.Anchor = TextAnchor.UpperLeft;
            }
            return panel.yMax + 8f;
        }

        private static string RelationDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return "—";
            }
            PawnRelationDef def = DefDatabase<PawnRelationDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            return defName;
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
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.PlacesCurrent".Translate().ToString());

            LocationInfo info = service.GetLiveLocation(detailObjectId);
            if (info == null || info.Kind == LocationKind.None)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoLocationData".Translate().ToString());
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
                Widgets.Label(new Rect(rect.x, y + 26f, rect.width, 40f),
                    "PersonalChronicle.UI.NoLocationExplanation".Translate().ToString());
                GUI.color = Color.white;
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

        private void DrawHoldersTab(Rect rect, IArchiveService service)
        {
            // v3.1 Custody tab (was Holders): current holder + history when available.
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.CurrentHolder".Translate().ToString());

            // Live holder first; degraded to archived snapshot / history tail.
            Pawn holder = service.GetCurrentHolder(detailObjectId);
            string holderStableId = null;
            string holderLabel = null;
            ThingObject thingObj = service.GetObject(detailObjectId) as ThingObject;
            if (holder != null)
            {
                holderStableId = holder.GetUniqueLoadID();
                holderLabel = holder.LabelShort;
            }
            else if (thingObj != null && !string.IsNullOrEmpty(thingObj.CurrentHolderId))
            {
                holderStableId = thingObj.CurrentHolderId;
                ArchiveObject holderObj = service.GetObject(holderStableId);
                holderLabel = holderObj != null ? ObjectDisplayLabel(holderObj) : holderStableId;
            }

            if (string.IsNullOrEmpty(holderLabel))
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoHolder".Translate().ToString());
                y += 28f;
            }
            else
            {
                Rect row = new Rect(rect.x, y, rect.width, RowHeight - 4f);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x + 6f, row.y + 4f, row.width - 12f, 22f), holderLabel);
                if (!string.IsNullOrEmpty(holderStableId) && Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, NavTarget.Pawn, holderStableId, null);
                }
                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
                y += RowHeight + 4f;
            }

            // P2: persisted holder history (craft/kill capture).
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.HolderHistory".Translate().ToString());
            if (thingObj == null || thingObj.HolderHistory == null || thingObj.HolderHistory.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoHolderHistory".Translate().ToString());
                return;
            }
            for (int i = thingObj.HolderHistory.Count - 1; i >= 0; i--)
            {
                ObjectRef href = thingObj.HolderHistory[i];
                if (href == null || string.IsNullOrEmpty(href.StableId))
                {
                    continue;
                }
                string label = !string.IsNullOrEmpty(href.LabelSnapshot)
                    ? href.LabelSnapshot
                    : href.StableId;
                ArchiveObject resolved = service.GetObject(href.StableId);
                if (resolved != null)
                {
                    label = ObjectDisplayLabel(resolved);
                }
                Rect hrow = new Rect(rect.x, y, rect.width, RowHeight - 4f);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(hrow.x + 6f, hrow.y + 4f, hrow.width - 12f, 22f), label);
                if (Widgets.ButtonInvisible(hrow))
                {
                    NavigateTarget(service, NavTarget.Pawn, href.StableId, null);
                }
                Widgets.DrawLineHorizontal(hrow.x, hrow.yMax, hrow.width);
                y += RowHeight + 2f;
            }
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
            float y = rect.y;
            string featureName = TabLabel(CurrentTabKey());

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, y, rect.width, 28f), featureName);
            y += 36f;

            Rect box = new Rect(rect.x, y, rect.width, 110f);
            GUI.color = new Color(1f, 1f, 1f, 0.04f);
            Widgets.DrawBoxSolid(box, Color.white);
            GUI.color = Color.white;
            DrawBorder(box, new Color(0.45f, 0.45f, 0.45f, 1f));

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(box.x + 14f, box.y + 14f, box.width - 28f, 24f),
                "PersonalChronicle.UI.NoCaptureYet".Translate().ToString());
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            Widgets.Label(new Rect(box.x + 14f, box.y + 46f, box.width - 28f, 48f),
                "PersonalChronicle.UI.NoCaptureExplanation".Translate().ToString());
            GUI.color = Color.white;
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
                GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
                Widgets.Label(new Rect(row.x + row.width - 186f, row.y + 3f, 182f, 18f), line.ParamsText);
                GUI.color = Color.white;

                float cy = row.y + TimelineRowHeight;
                if (descHeight > 0f)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
                    Widgets.Label(new Rect(row.x + 4f, cy, row.width - 8f, descHeight), line.DescriptionText);
                    GUI.color = Color.white;
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
            ArchiveUiStyle.DrawSelectedNavigation(rect, enabled);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = enabled ? ArchiveUiStyle.Text : ArchiveUiStyle.Muted;
            Widgets.Label(new Rect(rect.x + 8f, rect.y, rect.width - 16f, rect.height), label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
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
                    ? new Color(0.36f, 0.62f, 0.83f, 1f)
                    : new Color(0.72f, 0.72f, 0.72f, 1f);
                Widgets.Label(chipRect, chip.Label);
                GUI.color = Color.white;
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
            GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            string desc = FormatDate(cachedEventDetail.Tick);
            if (cachedEventDetail.Primary != null && !string.IsNullOrEmpty(cachedEventDetail.Primary.LabelSnapshot))
            {
                desc = desc + " · " + cachedEventDetail.Primary.LabelSnapshot;
            }
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 18f), desc);
            GUI.color = Color.white;
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
            GUI.color = new Color(1f, 1f, 1f, 0.04f);
            Widgets.DrawBoxSolid(descBox, Color.white);
            GUI.color = Color.white;
            DrawBorder(descBox, new Color(0.45f, 0.45f, 0.45f, 1f));
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.78f, 0.78f, 0.78f, 1f);
            Widgets.Label(new Rect(descBox.x + 12f, descBox.y + 10f, descBox.width - 24f, descBox.height - 20f),
                string.IsNullOrEmpty(cachedEventDescription)
                    ? "PersonalChronicle.UI.NoEvents".Translate().ToString()
                    : cachedEventDescription);
            GUI.color = Color.white;

            Widgets.EndScrollView();
        }

        private float ComputeEventHeight(float width)
        {
            float treeHeight = 34f + Mathf.Max(1, cachedEventTree.Count) * 22f + 12f;
            return 4f + 28f + 26f + treeHeight + 18f + 22f + 28f + 90f + 20f;
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
                    ? new Color(0.36f, 0.62f, 0.83f, 1f)
                    : new Color(0.78f, 0.78f, 0.78f, 1f);
                Widgets.Label(labelRect, line.Label);
                GUI.color = Color.white;

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
            Text.Font = GameFont.Small;
            ArchiveUiStyle.DrawSectionMarker(new Rect(rect.x, y + 4f, 4f, 14f));
            Widgets.Label(new Rect(rect.x + 10f, y, 210f, 22f), title);
            ArchiveUiStyle.DrawRule(
                new Rect(rect.x + 230f, y + 11f, Mathf.Max(0f, rect.width - 230f), 1f),
                ArchiveUiStyle.Border);
            y += 30f;
        }

        private static void DrawEventRow(Rect row, string dateText, string titleText, string typeText)
        {
            Color previous = GUI.color;
            GUI.color = new Color(ArchiveUiStyle.Info.r, ArchiveUiStyle.Info.g, ArchiveUiStyle.Info.b, 0.5f);
            Widgets.DrawBoxSolid(new Rect(row.x, row.y + 1f, 2f, Mathf.Max(1f, row.height - 2f)), GUI.color);
            GUI.color = previous;

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

            ArchiveUiStyle.DrawRule(new Rect(row.x, row.yMax - 1f, row.width, 1f), ArchiveUiStyle.BorderSoft);
        }

        private static float DrawDetailRow(float x, float y, float width, string label, string value)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.72f, 0.72f, 0.72f, 1f);
            Widgets.Label(new Rect(x, y, 150f, 20f), label);
            GUI.color = Color.white;

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(x + 156f, y, width - 156f, 22f), value);
            return y + 26f;
        }

        private static void DrawStatCell(Rect rect, string label, int value)
        {
            ArchiveUiStyle.DrawCard(rect, ArchiveUiStyle.Accent);
            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Muted;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 7f, rect.width - 18f, 16f), label);
            Text.Font = GameFont.Medium;
            GUI.color = ArchiveUiStyle.Text;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 25f, rect.width - 18f, 26f), value.ToString());
            GUI.color = Color.white;
        }

        /// <summary>
        /// Stat cell with a Tiny-font breakdown line under the value (fits the
        /// existing 64f cell: value ends at y+50, sub-label occupies y+48..y+64).
        /// Original signature untouched — this is an append-only overload.
        /// </summary>
        private static void DrawStatCell(Rect rect, string label, int value, string subLabel)
        {
            DrawStatCell(rect, label, value);
            if (string.IsNullOrEmpty(subLabel))
            {
                return;
            }
            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Dim;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 49f, rect.width - 18f, 16f), subLabel);
            GUI.color = Color.white;
        }

        private static readonly Color AlivePill = new Color(0.42f, 0.81f, 0.56f, 1f);
        private static readonly Color DeadPill = new Color(0.94f, 0.38f, 0.38f, 1f);
        private static readonly Color BluePill = new Color(0.36f, 0.62f, 0.83f, 1f);

        private static float DrawPill(float x, float y, string label, Color color)
        {
            Text.Font = GameFont.Tiny;
            float width = Text.CalcSize(label).x + 18f;
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
                    return new Color(0.95f, 0.74f, 0.26f, 1f);
                case PawnRole.Prisoner:
                    return new Color(0.86f, 0.46f, 0.46f, 1f);
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

        private struct ProductionLineView
        {
            public string DefName;
            public string Label;
            public int Count;
            public long LastTick;
            public string StableId;
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

        private struct CandidateObject
        {
            public ArchiveObject Object;
            public int EventCount;
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
