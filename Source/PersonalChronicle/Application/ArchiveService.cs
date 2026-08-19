using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Api;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// Application-layer orchestrator. Stateless: every call resolves the
    /// ChronicleGameComponent from the active game and delegates to it.
    /// Enforces the recording toggle and the per-pawn event cap.
    ///
    /// v2.1: writes go through the ArchiveObject model (PawnObject + ObjectRef
    /// Primary). The v0.2 PawnStableId legacy field is kept in sync as a shadow
    /// inside ChronicleEvent.ExposeData — the service never writes it directly.
    ///
    /// v4.1: also implements <see cref="IArchiveQueryService"/> (inherited by
    /// IArchiveService) and <see cref="IArchiveEventSink"/> so integrators can
    /// depend on the narrower read/write contracts via the unified API facade.
    /// </summary>
    public sealed partial class ArchiveService : IArchiveService, IWorkIntensityService, IWorkTimeCaptureService, IArchiveQueryService, IArchiveEventSink
    {
        // TypeKey constants live in ChronicleEventType (single source of truth,
        // validated against Defs/Chronicle_Events.xml at startup).

        /// <summary>Cache window for live-pawn resolution (in game ticks).</summary>
        private const long LivePawnCacheWindow = 120L;

        /// <summary>Cache window for the live colonist count (home-page stat; in game ticks).</summary>
        private const long LiveCountCacheWindow = 600L;

        /// <summary>Career aggregate cache window; source invalidation is also
        /// keyed by ChronicleGameComponent.DataRevision.</summary>
        private const long WorkIntensityCacheWindow = 600L;

        /// <summary>Prefix of Thing.GetUniqueLoadID() = "Thing_" + defName + "_" + thingIDNumber.</summary>
        private const string ThingIdPrefix = "Thing_";

        /// <summary>
        /// Live-pawn cache keyed by thingIDNumber (int, zero string alloc per
        /// pawn during rebuild). Game-switch detection compares the resolved
        /// game component; a changed instance means a new game/save load.
        /// [Unsaved]: never persisted.
        /// </summary>
        [Unsaved]
        private ChronicleGameComponent cacheGameComponent;

        [Unsaved]
        private Dictionary<int, Pawn> livePawnCache = new Dictionary<int, Pawn>();

        [Unsaved]
        private long livePawnCacheTick = -1L;

        // ---- v2.4 live colonist count cache ----
        // Independent of livePawnCache: different window (600 vs 120 ticks)
        // and different semantics (count, not per-pawn resolution). A full
        // ChronicleColonistScanner scan runs at most once per window; the home
        // page reads the cached int. Game-switch detection mirrors livePawnCache
        // (component-instance comparison).
        [Unsaved]
        private ChronicleGameComponent liveCountCacheComponent;

        [Unsaved]
        private int cachedLiveColonistCount;

        [Unsaved]
        private int cachedFreeColonistCount;

        [Unsaved]
        private int cachedSlaveCount;

        [Unsaved]
        private int cachedPrisonerCount;

        [Unsaved]
        private long liveCountCacheTick = -1L;

        [Unsaved]
        private readonly IWorkIntensityProviderRegistry workIntensityProviders;

        [Unsaved]
        private WorkIntensityPolicySnapshot workIntensityPolicy;

        [Unsaved]
        private ChronicleGameComponent workIntensityCacheComponent;

        [Unsaved]
        private long workIntensityCacheRevision = -1L;

        [Unsaved]
        private long workIntensityCacheTick = -1L;

        [Unsaved]
        private ColonyWorkAggregateView cachedColonyWorkAggregate;

        [Unsaved]
        private readonly HashSet<string> warnedIntensityProviders = new HashSet<string>();

        public ArchiveService()
            : this(new WorkIntensityProviderRegistry())
        {
        }

        public ArchiveService(IWorkIntensityProviderRegistry providers)
        {
            workIntensityProviders = providers ?? new WorkIntensityProviderRegistry();
        }

        private static ChronicleGameComponent Component
        {
            get
            {
                if (Current.Game == null)
                {
                    return null;
                }
                return Current.Game.GetComponent<ChronicleGameComponent>();
            }
        }

        public IReadOnlyList<PawnRecord> GetAllRecords()
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<PawnRecord>();
            }
            return component.GetAllRecords();
        }

        public IReadOnlyList<PawnRecord> GetActiveRecords()
        {
            IReadOnlyList<PawnRecord> all = GetAllRecords();
            List<PawnRecord> result = new List<PawnRecord>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                if (!all[i].IsArchived)
                {
                    result.Add(all[i]);
                }
            }
            return result;
        }

        public IReadOnlyList<PawnRecord> GetArchivedRecords()
        {
            IReadOnlyList<PawnRecord> all = GetAllRecords();
            List<PawnRecord> result = new List<PawnRecord>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].IsArchived)
                {
                    result.Add(all[i]);
                }
            }
            return result;
        }

        public IReadOnlyList<ChronicleEvent> GetEventsFor(string stableId)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<ChronicleEvent>();
            }
            return component.GetEventsFor(stableId);
        }

        public ArchiveObject GetObject(string stableId)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return null;
            }
            return component.GetObject(stableId);
        }

        public IReadOnlyList<ArchiveObject> GetLinkedObjects(string stableId)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<ArchiveObject>();
            }
            return component.GetLinkedObjects(stableId);
        }

        public IReadOnlyList<ArchiveObject> GetObjectsOfCategory(string categoryKey)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<ArchiveObject>();
            }
            return component.GetObjectsOfCategory(categoryKey);
        }

        /// <summary>
        /// Runtime-only invalidation token for UI read models. The archive
        /// contents remain owned by ChronicleGameComponent; this value only
        /// tells consumers that a fresh snapshot is available.
        /// </summary>
        public long GetDataRevision()
        {
            ChronicleGameComponent component = Component;
            return component != null ? component.DataRevision : -1L;
        }

        /// <summary>
        /// Resolves the live Pawn matching a stable id (null if absent or dead).
        /// UI tabs (Skills/Health/Relations) call this every frame while open;
        /// the O(N) pawn scan is therefore cached: a full rebuild runs at most
        /// once per <see cref="LivePawnCacheWindow"/> ticks, and per-frame
        /// lookups are O(1) dictionary hits (no GetUniqueLoadID allocations).
        ///
        /// The cache is keyed by thingIDNumber parsed from the stableId tail
        /// ("Thing_&lt;defName&gt;_&lt;number&gt;"), matched against
        /// Pawn.thingIDNumber — zero per-pawn string allocation during scans.
        /// Dead/destroyed pawns are evicted on read; a game switch (new
        /// game/load) drops the whole cache via component-instance comparison.
        /// </summary>

        /// <summary>
        /// v5.x "在册"判定：存活 且 属于当前殖民地人口 = 在册。
        ///
        /// 判定不依赖 DeathTick（那是"归档"语义，≠ 在册）。一个存档中已有快照
        /// 但尚未死亡的殖民者，只要还活在本殖民地就应显示"在册"；死亡归档或已
        /// 离开殖民地（被放逐/卖掉/转派系）则不在册。
        ///
        /// 实现：GetLivePawn 已驱逐 dead/destroyed pawn → 能解析到即存活；
        /// TryClassifyCurrent 判定"当前殖民地人口"（地图 spawned 成员 + 商队成员）
        /// → 两条件 AND 即"存活且属于殖民地"。
        /// </summary>

        /// <summary>
        /// Parses a Thing stable id "Thing_&lt;defName&gt;_&lt;thingIDNumber&gt;".
        /// Returns false for anything not in that shape (defensive fallback).
        /// </summary>

        /// <summary>
        /// Full rebuild of the live-pawn map: every non-dead pawn on every map
        /// plus alive world pawns, keyed by thingIDNumber. thingIDNumber is a
        /// per-session unique counter, so int keys cannot collide across pawns.
        /// </summary>

        /// <summary>
        /// Exact-string fallback for stable ids that are not in the standard
        /// "Thing_&lt;defName&gt;_&lt;number&gt;" shape (same logic as the
        /// pre-v2.2 implementation).
        /// </summary>

        public IReadOnlyList<ChronicleEvent> GetRecentEvents(int count)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<ChronicleEvent>();
            }
            return component.GetRecentEvents(count);
        }

        public IReadOnlyList<ChronicleEvent> GetAllEvents()
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<ChronicleEvent>();
            }
            return component.GetAllEvents();
        }

        public ArchiveDepthBehavior GetCategoryBehavior(string categoryKey)
        {
            if (string.IsNullOrEmpty(categoryKey))
            {
                return ArchiveDepthBehavior.Record;
            }
            // Def-driven: categoryKey bridges data keys to ArchiveCategoryDef.
            // Unknown category falls back to Record (conservative default) so a
            // missing Def degrades to archive-record behavior, never to nothing.
            ArchiveCategoryDef def = DefDatabase<ArchiveCategoryDef>.AllDefs
                .FirstOrDefault(d => d.categoryKey == categoryKey);
            if (def == null)
            {
                return ArchiveDepthBehavior.Record;
            }
            return def.behavior;
        }

        /// <summary>
        /// Work priorities for the Work/Places tab. Live path reads
        /// Pawn_WorkSettings (free colonists only — non-free pawns return an
        /// empty list); dead/absent pawns fall back to the archived
        /// PawnObject.WorkSnapshot. Both paths return only Priority &gt; 0 so a
        /// live pawn and its archived snapshot render identically.
        /// </summary>

        /// <summary>
        /// v3.1: cumulative work-time ledger for the Career tab.
        /// </summary>

        // ---- v4.1 work-intensity service ------------------------------------


        /// <summary>
        /// v4.15: ranks <paramref name="ownTicks"/> against every current colony
        /// member's accumulated work ticks. <paramref name="rank"/> is 1-based
        /// (1 = most hours); <paramref name="population"/> is the total member count.
        /// </summary>

















        /// <summary>
        /// v3.1: skill join snapshot vs death (or live) levels.
        /// </summary>

        /// <summary>
        /// v4.0 production summary. The aggregate is persisted on PawnObject so
        /// routine craft events do not need to be replayed by the UI.
        /// </summary>

        /// <summary>
        /// v1.1.4 损耗宫格：返回该人物消耗品计价累计。直接消费
        /// <see cref="ConsumptionAccumulator"/> 持久化数据（不扫事件流）。
        /// 周损耗 = 近 7 天按天桶求和；日均 = 近 30 天按天桶求和 / 30。
        /// </summary>

        /// <summary>
        /// v4.6.5: collapses per-item production rows into per-category rows using
        /// each item's top-level ThingCategory (e.g. "Bow_Wood" -> "Weapons"). Items
        /// with no category definition fall back to their own defName.
        /// </summary>

        /// <summary>
        /// Returns the top-level ThingCategory defName for an item defName
        /// (the category whose parent is null), or null when no category applies.
        /// </summary>

        /// <summary>
        /// Live location of a pawn: on-map (map biome/defName) or in a world
        /// caravan (world tile). None when the pawn is dead, absent or not
        /// resolvable. Matches LocationObject.MapDefName/WorldTile semantics so
        /// the live tab and the archived object describe the same facts.
        /// </summary>

        /// <summary>
        /// Resolves the live Pawn holding the Thing identified by
        /// "defName:thingIDNumber". Walks the IThingHolder parent chain
        /// (pawn.equipment / pawn.apparel / containers) until a Pawn is found;
        /// null for unheld, destroyed or unresolved things.
        /// </summary>

        /// <summary>
        /// Most recent ongoing battle: any BattleObject whose (runtime) EndTick
        /// is still -1, newest first by the latest associated event tick. Null
        /// when no battle is currently recorded as ongoing.
        /// </summary>

        /// <summary>
        /// Semantic filter over GetEventsFor: only Craft/Built events, resolved
        /// through ChronicleEventDef.kind (never TypeKey substring matching).
        /// </summary>

        // ---- live query helpers ----

        /// <summary>World tile of the caravan carrying <paramref name="pawn"/> (-1 if none).</summary>

        /// <summary>
        /// Finds a live, non-destroyed Thing by its stable id "defName:thingIDNumber"
        /// across all loaded maps. Conservative full scan over map.listerThings —
        /// correct and cheap enough for a UI refresh cadence.
        /// </summary>

        /// <summary>Latest event tick associated with a battle (used to pick the newest ongoing one).</summary>


        /// <summary>
        /// Records a colonist join with an explicit role (free colonist / slave /
        /// prisoner). The capture layer (Patch_SetFaction) pre-classifies so the
        /// archive badge matches the join-time reality. Falls back to FreeColonist
        /// when classification is unavailable.
        /// </summary>


        /// <summary>
        /// Records a colonist death. v0.3: <paramref name="extraParams"/> entries
        /// (e.g. ["killer"] = killer pawn LabelShort) are merged into the event's
        /// Params as language-independent identity snapshots — never used for
        /// associations (Primary/Subjects remain the only edge source).
        ///
        /// v3.1 P2: when extraParams contains Killer label, the matching killer
        /// pawn (if resolved by capture) should also be passed via the optional
        /// path in Patch_PawnKill — killer is added as a Pawn Subject edge so
        /// GetEventsFor(killerId) lists this death as a kill.
        /// </summary>

        /// <summary>
        /// Full death write with optional killer pawn for P2 kill-graph edges.
        /// </summary>

        /// <summary>
        /// v3.1 P2: kill by a chronicle colonist of a non-archive victim (raider etc.).
        /// </summary>
        /// <summary>
        /// v3.1 P3: significant social relation formed/ended.
        /// </summary>




        /// <summary>
        /// v6.8 重载：在基础击杀记录之上，累加个人战斗维度到凶手 PawnObject。
        /// 仅当 killer 可解析且为本殖民地人口（落入 UnknownKiller 桶的路径不累加）。
        /// 伤害累加采用补刀 DamageInfo.Amount 近似（Pawn.TakeDamage 账本已在基础路径消费，
        /// 此处补刀伤害作为该次击杀的生涯伤害增量，足够支撑"生涯伤害"展示维度）。
        /// </summary>

        /// <summary>
        /// Shared death/kill edge wiring: weapon Subject, killer Subject, battle
        /// Subject + ParticipantIds, weapon holder history.
        /// </summary>


        /// <summary>
        /// v4.14: maps an IncidentDef.category to a stable data key for
        /// <see cref="BattleObject.ThreatKey"/> — "ThreatBig"/"ThreatSmall"/null.
        /// Data-driven (compares against the IncidentCategoryDefOf constants),
        /// never defName string matching. Null for non-threat or custom battles.
        /// </summary>




        /// <summary>
        /// v1.1.4 损耗宫格：记录该人物消耗一份可摄入物，按 Def.BaseMarketValue 计价累加。
        /// 来源 <c>Thing.Ingested</c> 捕获（Postfix），高频写入不生成事件流。
        /// </summary>

        /// <summary>
        /// v1.1.4 劳模住所/工坊检测（方案 A）：记录殖民者在一台工作台完成一次制造迭代。
        /// 来源 <c>Bill_Production.Notify_IterationCompleted</c> 捕获（Postfix）；低频，
        /// 不写事件流 —— 工作场所是聚合状态而非事件语义，与 ConsumptionAccumulator 同思路。
        /// 只存 defName 稳定键（Building_WorkTable.def.defName + 工坊所在房间 Room.Role.defName）；
        /// 玩家手动改名后 UI 实时解析 LabelCap 显示新名（改名正确变更契约）。
        /// </summary>

        /// <summary>
        /// v1.1.4 建筑别名：为指定殖民者的工作场所设置自定义别名。
        /// <paramref name="customName"/> 为 null/空时清除别名。数据层拥有 mutation 规则；
        /// 无别名时展示层回落 DefDatabase.LabelCap 实时解析名。
        /// </summary>

        /// <summary>
        /// v1.1.4 工坊实例全局别名：key = <c>defName:thingIDNumber</c>（BuildingStableId）。
        /// 设置/清除某台工坊的自定义名，任何使用该工坊的劳模档案共享显示此名。
        /// </summary>

        /// <summary>
        /// v1.1.4 房间类型别名：key = RoomRoleDef.defName（如 Bedroom）。
        /// 设置/清除某房间类型的自定义显示名（mod 内部展示层覆盖，不改原版 Def）。
        /// </summary>

        /// <summary>
        /// v1.1.4 房间级改名（集中于 ITab，无需家具）：key = <c>pawnStableId:RoomRoleDefName</c>。
        /// 设置「某殖民者的某类型房间」的自定义显示名，粒度到个人+类型。
        /// </summary>

        /// <summary>
        /// v1.1.4 房间级自定义名读取（null = 未改名，回落类型级别名 / RoomRoleDef.LabelCap）。
        /// </summary>

        /// <summary>
        /// v1.1.4 房间级类型名替换（底层 Role 不变）：key = <c>pawnStableId:RoomRoleDefName</c>。
        /// </summary>

        /// <summary>
        /// v1.1.4 房间级类型名替换读取（null = 未替换）。
        /// </summary>

        /// <summary>
        /// v1.1.4 工坊实例全局别名读取（null = 未改名）。
        /// </summary>

        /// <summary>
        /// v1.1.4 房间类型别名读取（null = 未改名）。
        /// </summary>

        /// <summary>
        /// v4.9: records an equipment thing's decommission (退役仪式) — the thing's
        /// "death record", captured read-only at destroy time. Never prevents the
        /// destroy; only writes when the thing has an archive object (was ever
        /// registered) and no prior decommission record (idempotent).
        /// </summary>

        /// <summary>
        /// v4.9: place key for a thing being destroyed — the map biome defName when
        /// on a map, "—" otherwise. Stored as a language-independent defName (never a
        /// localized label) so the archived record survives language switches; the
        /// Read Model resolves the biome label at display time via BiomeLabel.
        /// </summary>



        /// <summary>
        /// v3.1 P2: at battle start, attach every live chronicle person as a
        /// Subject edge + ParticipantIds entry (map + caravan roster).
        /// </summary>

        private void AddEvent(ChronicleGameComponent component, string stableId, string labelSnapshot, string typeKey)
        {
            ChronicleEvent ev = BuildPawnEvent(stableId, labelSnapshot, typeKey);
            AddEvent(component, stableId, ev);
        }

        /// <summary>
        /// Marks every currently-ongoing BattleObject (EndTick still -1) as
        /// ended at the current tick, excluding the newly-started one. Without
        /// this, EndTick is never assigned (there is no battle-end capture point
        /// in vanilla), so GetActiveBattle() would report the most recent battle
        /// as ongoing forever and LC-2 would link every death to it.
        /// Event-driven: a new battle start IS the previous battle's end.
        /// </summary>

        /// <summary>
        /// v4.11 P0: links the raid Lord(s) that <c>IncidentWorker_Raid.TryExecuteWorker</c>
        /// just spawned on the map to the active battle, and snapshots the raid force
        /// size. The raid is represented in vanilla by one or more <see cref="Lord"/>s
        /// (e.g. <c>LordJob_AssaultColony</c>) whose <c>ownedPawns</c> are exactly the
        /// enemy raiders. We scan every loaded map's LordManager for hostile,
        /// non-player Lords that are not yet linked, and attribute them to this
        /// battle. This is the precise (no-polling) "force size" capture point: the
        /// Lords exist because TryExecuteWorker ran synchronously before this call.
        ///
        /// Non-Lord threats (infestation, mech cluster, ship part) have no raid Lord,
        /// so RaidCount stays -1 and the battle relies on <see cref="ClosePreviousBattle"/>
        /// (next battle start) as the repulse fallback — consistent with the
        /// archive-only, record-after-process positioning.
        /// </summary>

        /// <summary>
        /// v4.11 P0: a raid pawn left the map for a linked Lord. <paramref name="remainingPawns"/>
        /// is the Lord's authoritative remaining raider count (ownedPawns.Count after
        /// the loss). When it reaches zero, every raider of this Lord is gone and the
        /// battle is finalized (EndTick written). Lords not linked to any battle are
        /// ignored. We use the authoritative count directly rather than decrementing
        /// a counter, so a pawn reported lost more than once (different
        /// PawnLostCondition values) can never over-decrement, and the battle ends
        /// exactly when the last raider leaves — for both single-Lord and multi-Lord
        /// raids.
        /// </summary>

        /// <summary>
        /// Writes EndTick when every linked Lord has lost all its pawns. We track the
        /// battle's runtime RemainingRaidCount as the MINIMUM remaining-raider count
        /// across the linked Lords (a multi-Lord raid is repulsed only once all its
        /// Lords are empty). A Lord is removed from the link map when its raiders hit
        /// zero, so its stale (later non-zero) notifications can never resurrect a
        /// finalized battle. Battles with RaidCount &lt;= 0 (no linked Lord, e.g.
        /// non-Lord threats) are never finalized here — ClosePreviousBattle covers them.
        /// </summary>

        /// <summary>
        /// v4.11 P0: clears the static Lord→battle link map. Called from
        /// ChronicleGameComponent.FinalizeInit on every new game / load so a previous
        /// session's (loadID-scoped) links can never leak into the next save.
        /// </summary>




        /// <summary>
        /// Adds a pawn Subject edge when the worker is a chronicle-relevant pawn.
        /// The worker is a data edge (StableId + label snapshot); its archive
        /// record may or may not exist yet — GetEventsFor still works because the
        /// eventsByObject index registers the edge regardless.
        /// </summary>

        /// <summary>
        /// Registers (or reuses) a ThingObject for a crafted thing / weapon.
        /// StableId is defName:thingIDNumber — valid within the loaded session,
        /// consistent with ThingObject.WeakId semantics (historical snapshot).
        /// </summary>

        /// <summary>
        /// v4.6.5: the "Thing" archive category is scoped to equipment — weapons
        /// and wearable apparel. Buildings, raw materials and food are excluded.
        /// </summary>


        private static bool IsRecordingEnabled()
        {
            return PersonalChronicleMod.Settings == null || PersonalChronicleMod.Settings.EnableRecording;
        }

        /// <summary>
        /// Ensures a live chronicle pawn has an archive object before an event
        /// references it as a killer/worker. This closes the race where a pawn
        /// acts before SetFaction or the reconcile confirmation window has
        /// completed. It never creates a record for a non-chronicle pawn.
        /// </summary>


        /// <summary>
        /// Cross-bucket idempotency: returns true if a death/kill event for the given
        /// victim already exists under ANY killer bucket. Scans the global event list
        /// (kills are rare, so an O(N) pass is acceptable) to prevent duplicate Death
        /// events when the same pawn death is captured by multiple code paths
        /// (e.g. instigator==null hitting both OnPawnDied and OnKillRecorded).
        /// </summary>

        /// <summary>
        /// Current (live-read) colony population count — free colonists, slaves
        /// and prisoners combined. Two-source merge (maps + caravans) via
        /// ChronicleColonistScanner.EnumerateCurrentPeople (single predicate
        /// source of truth), cached for <see cref="LiveCountCacheWindow"/> ticks.
        /// The per-role breakdown is cached alongside (see GetLiveColonistCounts).
        /// Game switch (new game / save load) resets the cache. Defensive: 0 when
        /// no game or component is active. Never triggers reconciliation — the
        /// write path is owned by ChronicleGameComponent's internal tick hook.
        /// </summary>

        /// <summary>
        /// Current (live-read) colony population broken down by role: free
        /// colonists / slaves / prisoners. Shares the same 600-tick cache as
        /// <see cref="GetLiveColonistCount"/>, so calling this never triggers a
        /// second scan within the window. UI renders the per-role split on the
        /// home "current colonists" stat cell.
        /// </summary>

        /// <summary>
        /// Single live scan: populates cachedLiveColonistCount plus the per-role
        /// breakdown from one EnumerateCurrentPeople() pass (no double scan).
        /// </summary>

        /// <summary>
        /// Archive-snapshot convention: active = DeathTick == -1. Counts every
        /// archived PawnObject of the Pawn category (single shared scan with
        /// <see cref="GetArchivedSnapshotCount"/>).
        /// </summary>

        /// <summary>
        /// Archive-snapshot convention: archived = DeathTick &gt; 0. Counts every
        /// archived PawnObject of the Pawn category (single shared scan with
        /// <see cref="GetActiveSnapshotCount"/>).
        /// </summary>

        /// <summary>
        /// One traversal over the archived Pawn category computes both counts
        /// (never two scans). archived = DeathTick &gt; 0 (matches
        /// PawnRecord.IsArchived); everything else counts as active.
        /// </summary>

        /// <summary>
        /// v4.0 home KPI: days since the earliest recorded event or colonist join.
        /// Falls back to the current game tick when the archive is empty so that
        /// freshly-started colonies show 0 rather than a meaningless large number.
        /// </summary>

        /// <summary>
        /// v4.0 home view mode persisted in the game component.
        /// </summary>

        /// <summary>
        /// v4.0 home view mode persisted in the game component.
        /// </summary>

        // ---------------------------------------------------------------------
        // Legacy ↔ unified event sink bridge (v4.1)
        // ---------------------------------------------------------------------

        /// <summary>
        /// v4.1 bridge: routes a fully-formed <see cref="ArchiveEventInput"/> through
        /// the unified <see cref="IArchiveEventSink.TryRecord"/>. This is the single
        /// connection point between the rich legacy write methods and the unified
        /// event contract — subclasses / external callers on the legacy surface use
        /// this instead of duplicating sink logic. Never throws on bad input.
        /// </summary>
        public CaptureResult RecordEvent(ArchiveEventInput input)
        {
            return TryRecord(input);
        }

        // ---------------------------------------------------------------------
        // IArchiveEventSink (v4.1 unified write entry point)
        // ---------------------------------------------------------------------
        // Idempotency cache scoped to the current game session. Not persisted —
        // it only guards against repeated captures of the same logical event
        // within one playthrough.
        private static readonly HashSet<string> SessionDedupKeys = new HashSet<string>();
        private static Game _dedupGame;

        /// <summary>
        /// Unified record entry. Converts <see cref="ArchiveEventInput"/> to a
        /// <see cref="ChronicleEvent"/>, runs idempotency + validity checks, then
        /// delegates to the existing <see cref="AddEvent"/> pipeline (recording
        /// toggle, per-pawn cap, deduplication-by-stableId inside Component).
        /// Never throws on bad input.
        /// </summary>
        public CaptureResult TryRecord(ArchiveEventInput input)
        {
            if (input == null || !input.IsValid)
            {
                return CaptureResult.Rejected;
            }
            if (Current.Game == null || Component == null)
            {
                return CaptureResult.Unavailable;
            }

            // Session-scoped idempotency for explicit dedup keys.
            if (!string.IsNullOrEmpty(input.DeduplicationKey))
            {
                EnsureDedupScope();
                if (SessionDedupKeys.Contains(input.DeduplicationKey))
                {
                    return CaptureResult.Duplicate;
                }
                SessionDedupKeys.Add(input.DeduplicationKey);
            }

            if (!IsRecordingEnabled())
            {
                return CaptureResult.Rejected;
            }

            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(input.EventTypeDefName);
            if (def == null)
            {
                return CaptureResult.Rejected;
            }

            ChronicleEvent ev = ToChronicleEvent(input, def);
            if (ev == null)
            {
                return CaptureResult.Rejected;
            }

            AddEvent(Component, input.Primary.StableId, ev);
            return CaptureResult.Accepted;
        }

        /// <summary>
        /// Resets the session dedup set whenever the active game changes so a new
        /// playthrough is not polluted by a previous session's dedup keys.
        /// </summary>
        private static void EnsureDedupScope()
        {
            Game game = Current.Game;
            if (!ReferenceEquals(game, _dedupGame))
            {
                SessionDedupKeys.Clear();
                _dedupGame = game;
            }
        }

        private ChronicleEvent ToChronicleEvent(ArchiveEventInput input, ChronicleEventDef def)
        {
            long tick = input.Tick > 0 ? input.Tick : (Current.Game?.tickManager?.TicksGame ?? 0);
            if (tick <= 0)
            {
                return null;
            }

            int importanceLevel = (int)(input.Importance
                ?? ChronicleEventImportance.Resolve(def.defName, input.Parameters));

            Dictionary<string, string> parameters = input.Parameters == null
                ? new Dictionary<string, string>()
                : input.Parameters.ToDictionary(kv => kv.Key, kv => kv.Value);

            List<ObjectRef> subjects = null;
            if (input.Subjects != null && input.Subjects.Count > 0)
            {
                subjects = new List<ObjectRef>();
                foreach (ArchiveEntityRef sub in input.Subjects)
                {
                    if (sub != null && sub.IsValid)
                    {
                        subjects.Add(sub.ToObjectRef());
                    }
                }
            }

            return new ChronicleEvent
            {
                SourceId = input.SourceId,
                TypeKey = input.EventTypeDefName,
                Tick = tick,
                ImportanceLevel = importanceLevel,
                Primary = input.Primary.ToObjectRef(),
                Subjects = subjects,
                Params = parameters,
            };
        }
    }
}
