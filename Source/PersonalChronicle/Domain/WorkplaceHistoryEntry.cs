using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    ///  workplaces the pawn has used (one entry per distinct workbench instance,
    /// keyed by <see cref="BuildingStableId"/>). Append-only and de-duplicated by
    /// the capture path so the left-click "all workrooms" popup can list the
    /// pawn's historical benches first, then supplement with other benches on the
    /// current map.
    ///
    /// Only stable keys + coordinates are persisted; display labels are resolved
    /// live from <c>DefDatabase</c> / alias tables at read time (mirrors
    /// <see cref="WorkplaceSnapshot"/> contract).
    /// </summary>
    public sealed class WorkplaceHistoryEntry : IExposable
    {
        /// <summary>Building_WorkTable ThingDef.defName（稳定键）。</summary>
        public string BuildingDefName;

        /// <summary>工坊实例稳定键（defName:thingIDNumber，与 WorkplaceSnapshot.BuildingStableId 同义）。</summary>
        public string BuildingStableId;

        /// <summary>工坊所在房间角色 RoomRoleDef.defName（可能为 null = 户外/未知）。</summary>
        public string RoomRoleDefName;

        /// <summary>所在地图索引（-1 = 未记录）。</summary>
        public int MapIndex = -1;

        /// <summary>工坊坐标（无效 = 未记录）。</summary>
        public IntVec3 Cell;

        /// <summary>累计使用次数（来自捕获累加）。</summary>
        public int UseCount;

        /// <summary>最近一次使用 tick（-1 = 从未）。</summary>
        public long LastUsedTick = -1L;

        public WorkplaceHistoryEntry()
        {
        }

        public WorkplaceHistoryEntry(string buildingDefName, string buildingStableId,
            string roomRoleDefName, int mapIndex, IntVec3 cell, long gameTick)
        {
            BuildingDefName = buildingDefName;
            BuildingStableId = buildingStableId;
            RoomRoleDefName = roomRoleDefName;
            MapIndex = mapIndex;
            Cell = cell;
            UseCount = 1;
            LastUsedTick = gameTick;
        }

        /// <summary>合并一次新使用（累加计数 + 刷新坐标/房间/时间）。</summary>
        public void MergeUse(string roomRoleDefName, int mapIndex, IntVec3 cell, long gameTick)
        {
            if (!string.IsNullOrEmpty(roomRoleDefName)) RoomRoleDefName = roomRoleDefName;
            if (mapIndex >= 0 && cell.IsValid)
            {
                MapIndex = mapIndex;
                Cell = cell;
            }
            UseCount++;
            if (gameTick > LastUsedTick) LastUsedTick = gameTick;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref BuildingDefName, "buildingDefName");
            Scribe_Values.Look(ref BuildingStableId, "buildingStableId");
            Scribe_Values.Look(ref RoomRoleDefName, "roomRoleDefName");
            Scribe_Values.Look(ref MapIndex, "mapIndex", -1);
            Scribe_Values.Look(ref Cell, "cell");
            Scribe_Values.Look(ref UseCount, "useCount", 0);
            Scribe_Values.Look(ref LastUsedTick, "lastUsedTick", -1L);
        }
    }
}
