using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 住所快照（v1.1.4 劳模住所/工坊检测 · 方案 B 定期快照）。
    ///
    /// 每 N tick 对活着的建档殖民者采样 <c>pawn.ownership.OwnedRoom.Role</c>
    /// （RoomRoleDef，如 Bedroom/Barracks），只存 RoomRoleDef.defName 稳定键；
    /// UI 层实时解析 <c>DefDatabase&lt;RoomRoleDef&gt;.LabelCap</c> 显示（「卧室」等）。
    ///
    /// v1.1.4 UI 拓展：同时记录房间中心坐标（MapIndex + Cell），供 ITab「定位跳转」
    /// （CameraJumper.TryJump）。坐标只是采样时的快照，房间被拆/搬后可失效，
    /// 点击定位时对失效坐标做兜底（无 Map 或越界则仅提示不跳转）。
    /// </summary>
    public sealed class ResidenceSnapshot : IExposable
    {
        /// <summary>住所房间角色 RoomRoleDef.defName（null = 暂无/无归属房间）。</summary>
        public string RoomRoleDefName;

        /// <summary>最近一次采样确认在住所的 game tick（-1 = 从未确认）。</summary>
        public long LastSeenTick = -1L;

        /// <summary>住所所在地图索引（Map.Index；-1 = 未记录）。</summary>
        public int MapIndex = -1;

        /// <summary>住所中心坐标（IntVec3；x/z 为 0 且 MapIndex=-1 表示未记录）。</summary>
        public IntVec3 Cell;

        public bool IsEmpty
        {
            get { return string.IsNullOrEmpty(RoomRoleDefName); }
        }

        public void RecordSeen(string roomRoleDefName, long gameTick, int mapIndex = -1, IntVec3 cell = default(IntVec3))
        {
            if (string.IsNullOrEmpty(roomRoleDefName))
            {
                return;
            }
            RoomRoleDefName = roomRoleDefName;
            if (gameTick > LastSeenTick)
            {
                LastSeenTick = gameTick;
            }
            // 记录最近一次有效坐标（MapIndex 有效且格子非零时才更新，避免采样到空地清空旧值）。
            if (mapIndex >= 0 && cell.IsValid)
            {
                MapIndex = mapIndex;
                Cell = cell;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref RoomRoleDefName, "roomRoleDefName");
            Scribe_Values.Look(ref LastSeenTick, "lastSeenTick", -1L);
            Scribe_Values.Look(ref MapIndex, "mapIndex", -1);
            Scribe_Values.Look(ref Cell, "cell");
        }
    }
}
