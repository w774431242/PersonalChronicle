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
    /// <summary>
    /// Partial of <see cref="PersonnelArchivePanel"/> —see main file for the class doc.
    /// </summary>
    public sealed partial class PersonnelArchivePanel
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
            // P2-6/P3: every section's query + sort + null-guard is delegated to the
            // read-model provider. The window only consumes immutable snapshots and
            // applies translation/formatting (its own translation context).
            long revision = service.GetDataRevision();
            RebuildCategoryCacheViaProvider(service, revision);
            ReadModels.HomeSnapshot home = uiDataProvider.BuildHome(service, revision);
            RebuildPawnCounts(home);
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
            // v5.x：职业事实计数（9 类）—— 逐字段镜像快照。此前 RebuildDetailCache **漏镜像**该字段，
            //   导致生涯子页「职业事实计数」组只能绕过读模型直查 Domain（违反「UI 只消费」），
            //   或干脆整组缺失。补齐后 UI 只消费本缓存（GATE-DATA：缓存可从权威数据重建）。
            cachedFactCounts = detail.FactCounts ?? new ReadModels.CareerFactCounts();
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

            if (cachedDetailObject is PawnObject heroPawn)
            {
                // v5.x：Hero 带 7 格指标带行文本（缓存构建期一次拼好 ⇒ 绘制路径零查询）。
                RebuildHeroStats(heroPawn, GenTicks.TicksGame, service);
                // v5.x：残值折旧曲线序列（由折旧事件反推；同样只在缓存构建期算）。
                cachedDepreciationSeries = BuildDepreciationSeries(cachedHealth);
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
            // ⚠ 2026-09-16（方案 A）：`expandedLocations` / `expandedBattles` 两个展开集已删除
            // （内联改恒定绘制，无需展开状态）⇒ 此处不再清理展开集。
            // 按对象的编年史/伤亡缓存仍由 DataRevision 失效（见 cachedChronicleRev / cachedCasualtyRev）。
            cachedProductionLines = new List<ReadModels.ProductionLineView>();
            cachedProductionSummary = new ProductionSummaryView(0, 0f, -1L, new List<ProductionTypeView>());
            cachedHeroStats = default(HeroStatLine);
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
            cachedDepreciationSeries = System.Array.Empty<float>();
            cachedMedals = new List<ReadModels.MedalView>();
            cachedLegacy = new ReadModels.LegacyView();
            cachedOrigin = new ReadModels.ThingOriginView();
            cachedMakerChain = new ReadModels.MakerChainView();
            cachedCoUse = new ReadModels.CoUseView();
            cachedDecommission = new ReadModels.DecommissionView();
            cachedRelations = new List<ReadModels.RelationView>();
            cachedFactCounts = new ReadModels.CareerFactCounts();
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
