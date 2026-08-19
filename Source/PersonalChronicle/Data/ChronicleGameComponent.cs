using System.Collections.Generic;
using System.Collections.ObjectModel;
using PersonalChronicle.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// Persistent storage for the chronicle. Owns the raw lists that are saved to
    /// the savegame; exposes read-only query views and internal write entry points
    /// used only by the application service layer.
    ///
    /// v2.1 model: canonical list is <see cref="Objects"/> (polymorphic
    /// ArchiveObject). The v0.2 "pawns" list is still read for old saves and
    /// re-written as a downgrade mirror (PawnObject → PawnRecord projection),
    /// so rolling back to v0.2 keeps the pawn archive intact. One-time migration
    /// is gated by <see cref="SchemaVersion"/> (idempotent: 0 = legacy).
    /// </summary>
    public sealed partial class ChronicleGameComponent : GameComponent
    {

        /// <summary>
        /// v4.11 P0: raid Lord loadID → active BattleObject.StableId（跨存档防污染：
        /// FinalizeInit 时清空）。本表原驻 Application.ArchiveService，按 MATRIX-010 治理
        /// 下沉到 Data 层（Application 经本类访问，依赖方向恢复单向）。
        /// </summary>
        internal static readonly Dictionary<int, string> RaidLordToBattle = new Dictionary<int, string>();

        /// <summary>清空 Lord→战役链接表（跨存档防污染；FinalizeInit 调用）。</summary>
        internal static void ResetRaidLordLinks()
        {
            RaidLordToBattle.Clear();
        }
        public List<ArchiveObject> Objects = new List<ArchiveObject>();
        public List<ChronicleEvent> Events = new List<ChronicleEvent>();
        public long NextEventId;

        /// <summary>
        /// v1.1.4 建筑别名（全局，按工坊实例共享）：key = <c>defName:thingIDNumber</c>
        /// （WorkplaceSnapshot.BuildingStableId），value = 玩家自定义工坊名。
        /// 任何使用该工坊的劳模档案都显示此名。append-only，旧档 null-safe。
        /// </summary>
        public Dictionary<string, string> BuildingAliases = new Dictionary<string, string>();

        /// <summary>
        /// v1.1.4 房间类型别名（全局）：key = RoomRoleDef.defName（如 Bedroom），
        /// value = 自定义类型名（如「员工宿舍」）。mod 内部展示层覆盖，不改原版 Def。
        /// append-only，旧档 null-safe。
        /// </summary>
        public Dictionary<string, string> RoomRoleAliases = new Dictionary<string, string>();

        /// <summary>
        /// v1.1.4 原版建筑 Label 运行时覆盖：key = thingIDNumber（int），value = 自定义建筑名。
        /// 由 <see cref="Patch_BuildingLabel"/> Harmony Postfix 在 <c>ThingWithComcs.get_Label</c>
        /// 中查询并拦截返回值。持久化以便读档后恢复。
        /// </summary>
        public Dictionary<int, string> BuildingLabelOverrides = new Dictionary<int, string>();

        /// <summary>
        /// v1.1.4 房间级自定义名（集中于 ITab 改名，无需家具）：key = <c>pawnStableId:RoomRoleDefName</c>，
        /// value = 自定义房间显示名（如「员工宿舍」）。粒度到「某殖民者的某类型房间」，
        /// 因此「Dweeb 的卧室」与「他人卧室」互不干扰。由 <see cref="Patch_RoomRoleLabel"/>
        /// Harmony Postfix 在 <c>Room.GetRoomRoleLabel</c> 中按当前房间拥有者+类型查表覆盖。
        /// </summary>
        public Dictionary<string, string> RoomNameOverrides = new Dictionary<string, string>();

        /// <summary>
        /// v1.1.4 房间级类型名替换（底层 Role 不变）：key = <c>pawnStableId:RoomRoleDefName</c>，
        /// value = custom type name (e.g. Workshop -> Forge type). Game logic follows the original Role (bed ownership, research speed unaffected); only the UI label is replaced. Per pawn + per role.
        /// </summary>
        public Dictionary<string, string> RoomTypeOverrides = new Dictionary<string, string>();

        /// <summary>
        /// v4.0: persisted home overview view preference (Kpi dashboard vs Chronicle
        /// timeline). Default Kpi; old saves that never wrote this field fall back
        /// to Kpi safely via the Scribe default value. Mirrored by the main-tab
        /// window; never a source of archive truth.
        /// </summary>
        public int HomeViewMode = 0;

        /// <summary>
        /// Runtime-only monotonic token. It is deliberately not serialized:
        /// save/load rebuilds the indexes and forces every UI read model to
        /// refresh naturally. The token is not archive data.
        /// </summary>
        public long DataRevision { get; private set; }

        /// <summary>
        /// 0 = legacy v0.2 schema, 1 = v3 archive schema, 2 = v4 scope
        /// migration, 3 = roster prune, 4 = initial social relation backfill
        /// (current).
        /// Persisted so the one-time migration never runs twice.
        /// </summary>
        public long SchemaVersion;

        /// <summary>
        /// Per-pawn count of consecutive relation scans that found nothing new.
        /// Runtime-only throttle state: rebuilding it after load costs a few
        /// extra scans, so it is deliberately not persisted.
        /// </summary>
        [Unsaved]
        private Dictionary<string, int> relationScanMissStreak = new Dictionary<string, int>();

        /// <summary>
        /// v5.x 修复：新档开局误判为"中途加入"的根因诊断。
        /// TicksGame<=0 不可靠（新建殖民地场景初始化会推进 tick，FinalizeInit 时
        /// TicksGame 常 > 0）。改用"是否读档加载"作为权威信号：读档时 ExposeData 会
        /// 以 PostLoadInit 模式执行，新档不会。该标记 [Unsaved]，仅在 FinalizeInit
        /// 当帧使用一次——读档当次现存人口才是"中途补录"(JoinTick=-1)，新档现存人口
        /// 一律按开局(JoinTick=0)处理。
        /// </summary>
        [Unsaved]
        private bool cameFromLoad;

        /// <summary>
        /// Consecutive empty scans after which a pawn's social graph is treated
        /// as settled. Small enough that a fresh colony still converges within
        /// the first minute of play.
        /// </summary>
        private const int RelationScanSettleThreshold = 3;

        /// <summary>
        /// v0.2 compatibility slot. On load: populated from old saves. On save:
        /// rebuilt as a downgrade mirror (PawnObjects projected to PawnRecords,
        /// whose class name v0.2 can deserialize). Never a source of truth.
        /// </summary>
        private List<PawnRecord> legacyPawns = new List<PawnRecord>();

        [Unsaved]
        private Dictionary<string, ArchiveObject> objectsByStableId = new Dictionary<string, ArchiveObject>();

        [Unsaved]
        private Dictionary<string, List<ChronicleEvent>> eventsByObject = new Dictionary<string, List<ChronicleEvent>>();

        /// <summary>
        /// Reconcile throttle window: GameComponentTick runs one reconcile pass
        /// at most every 600 ticks (10 seconds at 60 tps), matching the live-read
        /// cache cadence from the approved 统计活读化修复方案.
        /// </summary>
        private const long ReconcileIntervalTicks = 600L;

        /// <summary>
        /// 默认加入时刻的单一权威入口（v5.x 简化）。
        ///
        /// 根因：AddObject 对已存在 StableId 直接拒绝（不更新 JoinTick），谁先建档
        /// 谁决定 JoinTick，而建档入口有多个（Backfill / reconcile / 社交补录 /
        /// 捕获补录 / 死亡兜底）。统一在此裁决，避免各入口各自为政导致"新档开局
        /// 殖民者被 -1 定格"。
        ///
        /// 判定只用"加入/发现当天"粒度，不做任何时间窗口推断：
        ///   - 读档会话（cameFromLoad）→ 存档缺失、后来发现的存活人口，归到
        ///     发现/加入当天的起点（不再用 -1 显示"中途加入"）。
        ///   - 新档会话 → 开局殖民者，加入日即第 1 天（tick 0）。
        /// 显式加入（OnColonistJoined）仍走真实 TicksGame，不经过这里。
        /// </summary>
        internal long ResolveDefaultJoinTick()
        {
            if (cameFromLoad)
            {
                int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
                if (now > 0)
                {
                    // 当天起点用游戏日数系统：DayTick = 当天内已过 tick，减去即当天
                    // 零点。避免手工 TicksPerDay 取模。
                    long dayStart = now - (long)RimWorld.GenDate.DayTick(now, 0f);
                    return dayStart;
                }
                return 0L;
            }
            return 0L;
        }

        /// <summary>
        /// v3.1 career work-time sampling interval (~2s at 60 tps). Independent of
        /// reconcile — low-frequency, no Harmony on Job hot paths.
        /// </summary>
        private const long WorkSampleIntervalTicks = 120L;

        /// <summary>Ticks attributed to a work type per successful sample.</summary>
        private const long WorkSampleCreditTicks = 120L;

        /// <summary>
        /// Confirmation window for reconcile-archived colonists. A candidate must
        /// be present in two consecutive reconcile passes before it is archived —
        /// guards against temporary reinforcements (VE reinforcements / Medieval
        /// mercenaries) being permanently archived. A candidate absent from any
        /// pass is pruned and must restart the window from scratch.
        ///
        /// [Unsaved]: never persisted. A save/load boundary clears the window, so
        /// confirmation restarts cold on the new save — conservative, acceptable.
        /// </summary>
        [Unsaved]
        private HashSet<string> reconcileCandidates = new HashSet<string>();

        // 1.6: GameComponent base has a parameterless constructor. Empty ctor body,
        // do NOT chain : base(game) (CS1729).
        public ChronicleGameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref SchemaVersion, "schemaVersion", 0L);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                legacyPawns = BuildLegacyPawnMirror();
            }
            Scribe_Collections.Look(ref legacyPawns, "pawns", LookMode.Deep);
            Scribe_Collections.Look(ref Objects, "objects", LookMode.Deep);
            Scribe_Collections.Look(ref Events, "events", LookMode.Deep);
            Scribe_Values.Look(ref NextEventId, "nextEventId", 0L);
            Scribe_Values.Look(ref HomeViewMode, "homeViewMode", 0);
            // v1.1.4 建筑别名 / 房间类型别名（全局表，append-only）。
            Scribe_Collections.Look(ref BuildingAliases, "buildingAliases", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref RoomRoleAliases, "roomRoleAliases", LookMode.Value, LookMode.Value);
            // v1.1.4 原版建筑 Label 运行时覆盖（key=thingIDNumber）。
            Scribe_Collections.Look(ref BuildingLabelOverrides, "buildingLabelOverrides", LookMode.Value, LookMode.Value);
            // v1.1.4 房间级自定义名（key=pawnStableId:RoomRoleDefName）。
            Scribe_Collections.Look(ref RoomNameOverrides, "roomNameOverrides", LookMode.Value, LookMode.Value);
            // v1.1.4 房间级类型名替换（key=pawnStableId:RoomRoleDefName）。
            Scribe_Collections.Look(ref RoomTypeOverrides, "roomTypeOverrides", LookMode.Value, LookMode.Value);
            if (legacyPawns == null)
            {
                legacyPawns = new List<PawnRecord>();
            }
            if (Objects == null)
            {
                Objects = new List<ArchiveObject>();
            }
            if (Events == null)
            {
                Events = new List<ChronicleEvent>();
            }
            if (BuildingAliases == null)
            {
                BuildingAliases = new Dictionary<string, string>();
            }
            if (RoomRoleAliases == null)
            {
                RoomRoleAliases = new Dictionary<string, string>();
            }
            if (BuildingLabelOverrides == null)
            {
                BuildingLabelOverrides = new Dictionary<int, string>();
            }
            if (RoomNameOverrides == null)
            {
                RoomNameOverrides = new Dictionary<string, string>();
            }
            if (RoomTypeOverrides == null)
            {
                RoomTypeOverrides = new Dictionary<string, string>();
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                DataRevision = 0L;
                // 读档加载信号：仅当次 FinalizeInit 用于区分"中途补录"与"开局殖民者"。
                cameFromLoad = true;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();

            // Static capture-side accumulators survive a save/load cycle because they live
            // on the assembly, not on the Game. Clear them here (FinalizeInit runs for both
            // "new game" and "load game") so a previous session's pawns can never leak into
            // this one's assist attribution.
            Capture.Patch_PawnTakeDamage.Reset();
            // v4.11 P0: clear the Lord→battle link map so a prior session's (loadID-scoped)
            // links cannot pollute the freshly loaded save. Ongoing battles are re-linked
            // below (after the raid Lords are re-instantiated from the save).
            ResetRaidLordLinks();

            if (SchemaVersion < 1L)
            {
                MigrateLegacyData();
                SchemaVersion = 1L;
            }
            RebuildIndexes();
            if (SchemaVersion < 2L)
            {
                // v4.0 scope correction: older builds could backfill scenario
                // editor candidates during FinalizeInit. Remove only empty,
                // alive, unknown-join artifacts that are absent from the real
                // current population; preserve any object with history.
                if (PruneInitialRosterArtifacts())
                {
                    SchemaVersion = 2L;
                    RebuildIndexes();
                }
            }
            if (SchemaVersion < 3L)
            {
                // v4.0.1: the previous migration preserved candidates that had
                // already received a relation event. Those events could also
                // be emitted while the scenario roster was being initialized,
                // so event presence is not proof of colony membership. The
                // stricter one-time cleanup removes only alive, unknown-join
                // records absent from the current population; explicit joins,
                // dead pawns and current members remain untouched.
                if (PruneUnconfirmedRosterArtifacts())
                {
                    SchemaVersion = 3L;
                    RebuildIndexes();
                }
            }
            BackfillExistingColonists();
            if (SchemaVersion < 4L)
            {
                // Initial social relations used to be captured from DirectRelations
                // only, and only at the instant a pawn was first archived, so most
                // pre-upgrade saves have an empty social graph. BackfillExistingColonists
                // above already replayed the (now three-source) capture; only latch
                // the schema once the result is actually populated, otherwise the
                // periodic reconcile keeps retrying on later ticks.
                if (AllArchivedColonistsHaveRelations())
                {
                    SchemaVersion = 4L;
                }
            }
            if (SchemaVersion < 5L)
            {
                // v4.14 地点档案修复：旧版 Source2 把整个世界的 settlement + quest
                // site 全量建档（新档即 267 个），违反设计文档"地点 = 玩家亲自
                // 到访/建立/参与过"。一次性收敛：保留（a）玩家本家（IsPlayerHome）
                // 与（b）有任何事件历史的地点；其余全部关闭（DeinitReason="Unvisited"）。
                // 这与派系关系漂移、quest site 类名无关，是幂等的稳健清理。
                int closed = CloseStaleLocations();
                SchemaVersion = 5L;
                if (closed > 0)
                {
                    ChronicleLog.Save("v4.14 地点收敛，关闭 " + closed
                        + " 个未到访地点记录。");
                }
            }
            if (SchemaVersion < 6L)
            {
                // v1.1.4 勋章 defName 规范化：旧版 defName 含点号
                // （Medal.Labor.Model.Bronze），RimWorld 仅允许字母数字/下划线/短横线，
                // 非法 defName 会导致 Def 加载报红且 DefDatabase 查不到。一次性把旧档
                // PawnObject.GrantedMedals 里带点号的 defName 重写为下划线形式
                // （Medal_Labor_Model_Bronze），与 Defs/MedalDefs.xml 对齐，旧档勋章
                // 才能继续匹配 Def。幂等：新档 defName 已无点号，映射字典查不到即跳过。
                int remapped = MigrateLegacyMedalDefNames();
                SchemaVersion = 6L;
                if (remapped > 0)
                {
                    ChronicleLog.Save("v1.1.4 勋章 defName 规范化，重写 " + remapped
                        + " 条旧档勋章记录。");
                }
            }
            // v4.11 P0: RemainingRaidCount is [Unsaved]; rebuild it from the persisted
            // RaidCount for any still-ongoing battle so a loaded save can resume the
            // repulse countdown if its raid Lords are still alive.
            ResetBattleRaidCounters();
            // Re-link any still-ongoing battle to its (now re-instantiated) raid Lords
            // so the repulse countdown can continue after a load.
            RelinkOngoingBattles();
            MarkChanged();
        }

        /// <summary>
        /// Periodic reconcile (difference-fill against the live colony): every
        /// ReconcileIntervalTicks, enumerate live chronicle colonists via the
        /// shared Domain scanner and backfill-archive anyone not yet in the
        /// archive. This is the safety net for join paths Patch_SetFaction cannot
        /// see (Biotech pregnancy births, xenogerm conversion, mod recruitment
        /// paths).
        ///
        /// 只增不删: absence is NEVER treated as leaving — a cryptosleep colonist
        /// stays inside AllPawnsAlive and would otherwise pollute the timeline
        /// with a false death; death archiving stays exclusively with
        /// Patch_PawnKill.
        /// </summary>

        /// <summary>
        /// Credits WorkSampleCreditTicks to each living archived colonist whose
        /// current job resolves to a WorkTypeDef. Dead/archived pawns are skipped
        /// (career ledger freezes on death). Failures are silent per-pawn.
        /// </summary>

        /// <summary>
        /// P3: if the pawn's current place key differs from the open visit,
        /// close the previous stay and open a new one. Updates PrimaryPlaceDefName.
        /// </summary>

        /// <summary>
        /// v1.1.4 方案 B：采样建档殖民者当前住所房间角色（pawn.ownership.OwnedRoom.Role）。
        /// 只存 RoomRoleDef.defName 稳定键；无归属房间（null / 商队 / 户外）时不覆盖旧值，
        /// 保留最近一次确认过的住所。值变化时返回 true（触发 MarkChanged）。
        /// v1.1.4 UI 拓展：同步记录房间中心坐标（供 ITab 定位跳转）。
        /// </summary>

        /// <summary>
        /// v1.1.4 方案 A：工作场所使用记录（捕获层传入 Building_WorkTable 的 defName）。
        /// 数据层拥有 mutation 规则；只存 defName 稳定键（玩家改名后 UI 实时解析新 label）。
        /// </summary>

        /// <summary>
        /// v1.1.4 建筑别名（旧 per-pawn 兼容）：为指定殖民者的工作场所设置/清除自定义名。
        /// 新语义已由 <see cref="SetBuildingAlias"/>（工坊实例全局共享）取代；本方法保留
        /// 只为旧存档数据兼容——写入后优先全局别名表展示。实际展示路径见
        /// <see cref="GetBuildingAlias"/>。
        /// </summary>

        /// <summary>
        /// v1.1.4 工坊实例全局别名：key = <c>defName:thingIDNumber</c>（BuildingStableId）。
        /// 设置/清除某台工坊的自定义名，任何使用该工坊的劳模档案共享。
        /// 同时写入 <see cref="BuildingLabelOverrides"/> 以覆盖原版 <c>Thing.Label</c> 显示。
        /// </summary>

        /// <summary>
        /// 从 <c>"defName:thingIDNumber"</c> 格式的 stableId 解析 thingIDNumber。
        /// 解析失败返回 0。
        /// </summary>

        /// <summary>
        /// 设置/清除原版建筑 Label 运行时覆盖（key=thingIDNumber）。
        /// 由 <see cref="Patch_BuildingLabel"/> Postfix 在 <c>ThingWithComps.get_Label</c> 中查询。
        /// </summary>

        /// <summary>
        /// v1.1.4 工坊实例全局别名读取（null = 未改名，回落默认名）。
        /// </summary>

        /// <summary>
        /// v1.1.4 房间类型别名：key = RoomRoleDef.defName（如 Bedroom）。
        /// 设置/清除某房间类型的自定义显示名（如「员工宿舍」），mod 内部展示层覆盖。
        /// </summary>

        /// <summary>
        /// v1.1.4 房间类型别名读取（null = 未改名，回落 RoomRoleDef.LabelCap）。
        /// </summary>

        /// <summary>
        /// v1.1.4 房间级自定义名：key = <c>pawnStableId:RoomRoleDefName</c>。
        /// 设置/清除「某殖民者的某类型房间」的显示名（如「Dweeb 的卧室」→「员工宿舍」），
        /// 粒度到个人+类型，互不干扰。由 <see cref="Patch_RoomRoleLabel"/> 在
        /// <c>Room.GetRoomRoleLabel</c> 中按当前房间拥有者查表覆盖原版显示。
        /// </summary>

        /// <summary>
        /// v1.1.4 房间级自定义名读取（null = 未改名，回落类型级别名 / RoomRoleDef.LabelCap）。
        /// </summary>

        /// <summary>
        /// v1.1.4 房间级类型名替换（底层 Role 不变）：key = <c>pawnStableId:RoomRoleDefName</c>。
        /// 设置「某殖民者的某类型房间」的类型显示名（如「工作间」→「工坊类型」），
        /// 仅 UI 显示替换，游戏逻辑（床归属、研究加速等）不受影响。
        /// </summary>

        /// <summary>
        /// v1.1.4 房间级类型名替换读取（null = 未替换，回落类型级 / RoomRoleDef.LabelCap）。
        /// </summary>




        // ---- Read-only queries (public surface for the service layer) ----



        /// <summary>
        /// Events referencing a stable id. P3-5: returns a defensive read-only
        /// view — the caller may read/iterate but never mutate the live index.
        /// (Element objects are still shared references, as elsewhere in the
        /// model; the view only guards list structure.)
        /// </summary>




        /// <summary>
        /// Global cross-category event stream: the most recent
        /// <paramref name="count"/> events across ALL object types, Tick
        /// descending (newest first). Returns a defensive snapshot — the live
        /// Events list is never exposed to callers.
        ///
        /// Performance: on-demand full sort. Events are appended in tick order
        /// at capture time, but legacy migration / backfill can leave them
        /// unsorted, so a sort is the correct implementation. The Overview UI
        /// consumes this on a throttled refresh cadence (~2s), never per-frame —
        /// at colonist-scale data volume this is negligible.
        /// </summary>

        // ---- Internal write entry points (service layer only) ----


        /// <summary>
        /// Adds a production aggregate to a chronicle pawn. This is a data-layer
        /// mutation so the application service never has to reach into storage
        /// collections directly.
        /// </summary>

        /// <summary>
        /// v1.1.4 损耗宫格：累加一份消耗品银币到 ConsumptionAccumulator（类目聚合 + 按天桶）。
        /// 数据层拥有 mutation 规则；不写事件流（进食高频，避免事件膨胀）。
        /// </summary>

        /// <summary>
        /// Public integration samples enter through IWorkTimeCaptureService and
        /// are committed here so the data layer owns mutation/revision rules.
        /// </summary>



        /// <summary>Marks a runtime archive mutation for read-model invalidation.</summary>

        // ---- One-time legacy migration (schemaVersion 0 → 1) ----

        private void MigrateLegacyData()
        {
            // v0.2 Pawns → PawnObjects, field-by-field. Dedupe against Objects so
            // a partially migrated save is never double-written.
            HashSet<string> existing = new HashSet<string>();
            for (int i = 0; i < Objects.Count; i++)
            {
                ArchiveObject obj = Objects[i];
                if (obj != null && !string.IsNullOrEmpty(obj.StableId))
                {
                    existing.Add(obj.StableId);
                }
            }

            if (legacyPawns != null)
            {
                for (int i = 0; i < legacyPawns.Count; i++)
                {
                    PawnRecord legacy = legacyPawns[i];
                    if (legacy == null || string.IsNullOrEmpty(legacy.StableId))
                    {
                        continue;
                    }
                    if (existing.Contains(legacy.StableId))
                    {
                        continue;
                    }
                    Objects.Add(ConvertLegacyPawn(legacy));
                    existing.Add(legacy.StableId);
                }
            }

            // Legacy events: PawnStableId → Primary (no snapshot existed → null).
            for (int i = 0; i < Events.Count; i++)
            {
                ChronicleEvent ev = Events[i];
                if (ev == null)
                {
                    continue;
                }
                if (ev.Subjects == null)
                {
                    ev.Subjects = new List<ObjectRef>();
                }
                if (ev.Primary == null && !string.IsNullOrEmpty(ev.PawnStableId))
                {
                    ev.Primary = new ObjectRef(ArchiveCategoryKeys.Pawn, ev.PawnStableId, null);
                }
            }
        }

        /// <summary>
        /// v1.1.4 勋章 defName 规范化迁移：旧档 PawnObject.GrantedMedals 存的是带点号的
        /// 旧 defName（Medal.Labor.Model.Bronze），与新 Defs/MedalDefs.xml（下划线形式）
        /// 不匹配会导致 DefDatabase 查不到、勋章墙失效。一次性按映射表重写。
        /// 幂等：新档 defName 已无点号，字典查不到原样保留。
        /// </summary>
        private int MigrateLegacyMedalDefNames()
        {
            Dictionary<string, string> map = new Dictionary<string, string>
            {
                { "Medal.Labor.Model.Bronze", "Medal_Labor_Model_Bronze" },
                { "Medal.Labor.Model.Silver", "Medal_Labor_Model_Silver" },
                { "Medal.Labor.Model.Gold", "Medal_Labor_Model_Gold" },
                { "Medal.Labor.Worker.Bronze", "Medal_Labor_Worker_Bronze" },
                { "Medal.Labor.Worker.Silver", "Medal_Labor_Worker_Silver" },
                { "Medal.Labor.Worker.Gold", "Medal_Labor_Worker_Gold" },
                { "Medal.Labor.TechAce.Bronze", "Medal_Labor_TechAce_Bronze" },
                { "Medal.Labor.TechAce.Silver", "Medal_Labor_TechAce_Silver" },
                { "Medal.Labor.TechAce.Gold", "Medal_Labor_TechAce_Gold" },
                { "Medal.Combat.Hero.Bronze", "Medal_Combat_Hero_Bronze" },
                { "Medal.Combat.Hero.Silver", "Medal_Combat_Hero_Silver" },
                { "Medal.Combat.Hero.Gold", "Medal_Combat_Hero_Gold" },
                { "Medal.Combat.FirstClass.Bronze", "Medal_Combat_FirstClass_Bronze" },
                { "Medal.Combat.FirstClass.Silver", "Medal_Combat_FirstClass_Silver" },
                { "Medal.Combat.FirstClass.Gold", "Medal_Combat_FirstClass_Gold" },
                { "Medal.Combat.Enlistee.Bronze", "Medal_Combat_Enlistee_Bronze" },
                { "Medal.Combat.Enlistee.Silver", "Medal_Combat_Enlistee_Silver" },
                { "Medal.Combat.Enlistee.Gold", "Medal_Combat_Enlistee_Gold" },
                { "Medal.Support.Quartermaster.Bronze", "Medal_Support_Quartermaster_Bronze" },
                { "Medal.Support.Quartermaster.Silver", "Medal_Support_Quartermaster_Silver" },
                { "Medal.Support.Quartermaster.Gold", "Medal_Support_Quartermaster_Gold" },
                { "Medal.Legacy.Heirloom.Bronze", "Medal_Legacy_Heirloom_Bronze" },
                { "Medal.Legacy.Heirloom.Silver", "Medal_Legacy_Heirloom_Silver" },
                { "Medal.Legacy.Heirloom.Gold", "Medal_Legacy_Heirloom_Gold" },
                { "Medal.Legacy.KillerBlade.Bronze", "Medal_Legacy_KillerBlade_Bronze" },
                { "Medal.Legacy.KillerBlade.Silver", "Medal_Legacy_KillerBlade_Silver" },
                { "Medal.Legacy.KillerBlade.Gold", "Medal_Legacy_KillerBlade_Gold" },
            };

            int remapped = 0;
            if (Objects == null)
            {
                return remapped;
            }
            for (int i = 0; i < Objects.Count; i++)
            {
                PawnObject pawnObject = Objects[i] as PawnObject;
                if (pawnObject == null || pawnObject.GrantedMedals == null)
                {
                    continue;
                }
                for (int j = 0; j < pawnObject.GrantedMedals.Count; j++)
                {
                    string oldName = pawnObject.GrantedMedals[j];
                    if (oldName != null && map.TryGetValue(oldName, out string newName))
                    {
                        pawnObject.GrantedMedals[j] = newName;
                        remapped++;
                    }
                }
            }
            return remapped;
        }

        /// <summary>
        /// v4.14 地点档案收敛：关闭所有"玩家未实际到访"的地点记录。
        /// 保留规则（与设计文档"地点 = 玩家亲自到访/建立/参与过"一致）：
        ///   (a) 玩家本家（IsPlayerHome == true）—— 自己的基地永远在册；
        ///   (b) 有任何编年史事件历史的地点 —— 玩家参与过（战斗/贸易/事件）。
        /// 其余（敌对方/友好/中立 settlement、未到访的 quest site、无事件世界物体）
        /// 一律关闭（DeinitReason="Unvisited"）。幂等：已关闭的（DeinitTick!=-1）跳过。
        /// 与派系关系漂移、quest site 类名无关，是稳健的一次性清理。
        /// </summary>


        /// <summary>
        /// Projects the PawnObjects into plain PawnRecords so a save loaded by
        /// v0.2 (which cannot instantiate the PawnObject class) keeps its pawn
        /// archive. Pure projection — never a source of truth.
        /// </summary>

        // ---- Index / backfill ----


        /// <summary>
        /// v4.11 P0: <see cref="BattleObject.RemainingRaidCount"/> is [Unsaved], so
        /// after a save/load it must be rebuilt from the persisted
        /// <see cref="BattleObject.RaidCount"/>. Only ongoing battles (EndTick still
        /// -1) with a captured force size get a countdown; battles with no linked
        /// Lord force (RaidCount &lt;= 0) are left to the ClosePreviousBattle fallback.
        /// </summary>

        /// <summary>
        /// v4.11 P0: after a load, re-associate any still-ongoing battle (EndTick
        /// still -1, RaidCount &gt; 0) with its raid Lord(s) so the repulse countdown
        /// resumes. The Lords were re-instantiated from the save; LinkRaidLords only
        /// links hostile Lords with pawns that are not already linked, so calling it
        /// here simply re-establishes the link map and refreshes RaidCount/Remaining.
        /// </summary>

        /// <summary>
        /// Registers an event under every object it references (Primary first,
        /// then each Subject). Also backfills Primary from the legacy field as a
        /// defensive invariant — Primary/Subjects are the only edge source.
        /// </summary>




        /// <summary>
        /// Re-runs the initial-relation snapshot against an already archived pawn.
        ///
        /// Needed because relations are not reliably available at the moment a
        /// pawn is first archived: on a fresh colony the scenario's relation
        /// workers can finish after GameComponent.FinalizeInit, and before this
        /// method existed a pawn archived with an empty relation list never got
        /// a second chance (AddObject early-returns for known StableIds).
        ///
        /// Safe to call repeatedly: the capture is additive and de-duplicates on
        /// (relation, other pawn), and ended relations are never resurrected.
        /// </summary>

        /// <param name="force">
        /// Bypasses the settle throttle. Used by one-shot paths (load/join) where
        /// the cost is paid once; the periodic reconcile must not force.
        /// </param>

        /// <summary>
        /// v1.1.4 UI 拓展：reconcile 时同步该殖民者当前装备的主武器到 ThingObject。
        /// 解决"角色手持武器但神器传承卡显示 --"问题（之前仅在战斗/制造时记录，无战斗时无数据）。
        /// 每次 reconcile 检测装备变化：
        ///   - 新装备/换装备 → 注册 ThingObject + 更新 CurrentHolderId + 加 HolderRecord
        ///   - 无变化或无装备 → 不动
        /// </summary>

        /// <summary>
        /// True once a pawn has produced no new relations for several consecutive
        /// scans, meaning its social graph has settled and the periodic reconcile
        /// can skip the expensive re-derivation.
        /// </summary>


        /// <summary>
        /// Reports whether every currently archived colonist already carries at
        /// least one social tie, used to decide when the schema 4 relation
        /// backfill may be considered complete.
        ///
        /// The actual backfill work is done by <see cref="BackfillExistingColonists"/>
        /// (and the periodic reconcile); this only inspects the result. Latching
        /// the schema purely on "the backfill ran" would be wrong, because on load
        /// the relation workers may not have populated the graph yet — the pass
        /// would find nothing, mark the save migrated, and never retry.
        /// </summary>




        /// <summary>
        /// "Chronicle colonist" semantics: single source of truth lives in
        /// <see cref="ChronicleColonistScanner"/>. Backfill calls the shared
        /// predicate — never copy it here (口径分裂 = P1).
        /// </summary>

        /// <summary>
        /// Fallback join event for colonists discovered by the reconcile safety
        /// net rather than Pawn.SetFaction. The live scan proves presence but
        /// not the historical join tick, so the event records detection time
        /// while keeping JoinTick unknown (-1) on the snapshot.
        /// </summary>

        /// <summary>
        /// v4.0 home overview (E / chronicle-timeline view): returns ALL events for
        /// timeline rendering. Caller must filter by Importance / sort as needed; this
        /// is a defensive snapshot of the live Events list, never the list itself.
        /// Consumed on the throttled home refresh cadence, never per-frame.
        /// </summary>
    }
}
