using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Api
{
    /// <summary>
    /// Read-only query surface of the archive. Every method returns a read-only
    /// snapshot or live read-only view; no method mutates archive state.
    ///
    /// This is the contract an external mod (or a future UI) should depend on
    /// when it only needs to READ archive data. <see cref="IArchiveService"/>
    /// extends this interface with the write-entry points; the implementation
    /// (ArchiveService) satisfies both.
    /// </summary>
    public interface IArchiveQueryService
    {
        // ---- pawn records ----
        IReadOnlyList<PawnRecord> GetAllRecords();
        IReadOnlyList<PawnRecord> GetActiveRecords();
        IReadOnlyList<PawnRecord> GetArchivedRecords();

        // ---- object / event resolution ----
        ArchiveObject GetObject(string stableId);
        IReadOnlyList<ArchiveObject> GetLinkedObjects(string stableId);
        IReadOnlyList<ArchiveObject> GetObjectsOfCategory(string categoryKey);
        IReadOnlyList<ChronicleEvent> GetEventsFor(string stableId);
        IReadOnlyList<ChronicleEvent> GetRecentEvents(int count);
        IReadOnlyList<ChronicleEvent> GetAllEvents();

        /// <summary>
        /// 事件总数（**O(1)**，不复制、不排序）—— 供热路径（中心快照 / 统计带）使用。
        /// 需要事件内容用 <see cref="GetAllEvents"/>，但那是 O(n) 复制，禁止在节流外的热路径调用。
        /// </summary>
        int GetEventCount();

        /// <summary>
        /// 事件按时间分桶计数（段 0 = 最老 → 段 n-1 = 最近），**O(N) 单次遍历、零排序**。
        /// 用于以时间序列表达活跃度的读数组（绩效面板的趋势柱）。
        /// </summary>
        int[] GetEventBuckets(int bucketCount, long bucketTicks);

        /// <summary>
        /// 按**事件语义类别**分桶计数（段 0 = 最老 → 段 n−1 = 最近），O(N) 单次遍历。
        /// 类别经 <c>ChronicleEventDef.kind</c> 判定（不按 TypeKey 文本猜，GOV-009）。
        /// 用于「每季入档人数」这类分类型历史序列（调用方累计可得存量曲线）。
        /// </summary>
        int[] GetEventBucketsByKind(ChronicleEventKind kind, int bucketCount, long bucketTicks);

        /// <summary>
        /// **分身份存量趋势**（按季回推，段 0 = 最老）：从当前三态人数出发逆向撤销带身份事件，
        /// 每季末快照一次 ⇒ 三条人数线（自由/奴隶/囚犯）。返回 ``[3][bucketCount]``。
        /// 只认带身份的事件；历史（本机制上线前）如实缺失。
        /// </summary>
        int[][] GetIdentityTrends(int bucketCount, long bucketTicks, int[] currentByRole);
        IReadOnlyList<ChronicleEvent> GetProductionEvents(string stableId);
        ArchiveDepthBehavior GetCategoryBehavior(string categoryKey);

        // ---- revision / cache invalidation ----
        long GetDataRevision();

        // ---- live pawn / location ----
        Pawn GetLivePawn(string stableId);
        Pawn GetCurrentHolder(string stableId);
        LocationInfo GetLiveLocation(string stableId);
        BattleObject GetActiveBattle();

        /// <summary>
        /// True when the pawn is currently "on the roster": alive AND a member
        /// of the current colony population (free/slave/prisoner on maps or in
        /// caravans). Dead/archived pawns, former colonists who left the colony,
        /// and unknown stable ids all return false. This is the semantic
        /// opposite of "archived" (DeathTick &gt; 0) — the archive should not
        /// call a live colony member "archived" just because a snapshot exists.
        /// </summary>
        bool IsCurrentlyEnlisted(string stableId);

        // ---- career / work-time views ----
        IReadOnlyList<WorkPriorityView> GetWorkPriorities(string stableId);
        IReadOnlyList<WorkTimeStatView> GetWorkTimeStats(string stableId);
        IReadOnlyList<SkillArchiveView> GetSkillArchive(string stableId);
        ProductionSummaryView GetProductionSummary(string stableId);

        /// <summary>
        /// v1.1.4 损耗宫格：返回该人物消耗品计价累计（累计银币 / 近 7 天 / 近 30 天日均 /
        /// 类目构成）。来源 <see cref="ConsumptionAccumulator"/>（持久化，不扫事件流）。
        /// </summary>
        ConsumptionSummaryView GetConsumptionSummary(string stableId);

        // ---- live stats (home-page counts) ----
        int GetLiveColonistCount();
        void GetLiveColonistCounts(out int free, out int slave, out int prisoner);
        int GetActiveSnapshotCount();
        int GetArchivedSnapshotCount();
        int GetServiceDays();

        // ---- persisted home view mode (read side) ----
        int GetHomeViewMode();

        // ---- v1.1.4 建筑/房间类型别名（读侧） ----

        /// <summary>
        /// 工坊实例全局别名读取（null = 未改名，展示回落默认名）。
        /// key = <c>defName:thingIDNumber</c>（WorkplaceSnapshot.BuildingStableId）。
        /// </summary>
        string GetBuildingAlias(string buildingStableId);

        /// <summary>
        /// 房间类型别名读取（null = 未改名，展示回落 RoomRoleDef.LabelCap）。
        /// key = RoomRoleDef.defName（如 Bedroom）。
        /// </summary>
        string GetRoomRoleAlias(string roomRoleDefName);
    }
}
