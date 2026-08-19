using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 工作场所快照（v1.1.4 劳模住所/工坊检测 · 方案 A+B 组合落地）。
    ///
    /// 只持久化 <b>稳定键（defName）</b>，绝不持久化 label 字符串 —— 玩家对建筑
    /// 手动改名后，UI 层每次实时解析 <c>DefDatabase&lt;ThingDef&gt;.LabelCap</c> 即可
    /// 正确显示新名（改名正确变更契约）。翻译随语言切换自适应。
    ///
    /// 方案 A：捕获 <c>Bill_Production.Notify_IterationCompleted</c> → billGiver
    /// （Building_WorkTable）→ RecordUse 累加 UseCount / 刷新 LastUsedTick。
    /// 工坊名（defName）是唯一稳定的身份键，与 ThingObject.WeakId 语义一致。
    /// </summary>
    public sealed class WorkplaceSnapshot : IExposable
    {
        /// <summary>工坊建筑 ThingDef.defName（稳定键；null = 未捕获到工作场所）。</summary>
        public string BuildingDefName;

        /// <summary>
        /// v1.1.4 工坊实例稳定键（<c>defName:thingIDNumber</c>，与 ThingObject.WeakId 同语义）。
        /// 用于工坊实例全局别名（BuildingAliases 表）的反查；旧存档可能为 null（回落 BuildingDefName 维度）。
        /// </summary>
        public string BuildingStableId;

        /// <summary>累计使用（制造完成迭代）次数。</summary>
        public int UseCount;

        /// <summary>最近一次使用的 game tick（-1 = 从未使用）。</summary>
        public long LastUsedTick = -1L;

        /// <summary>工坊所在房间角色 RoomRoleDef.defName（可选；null = 未知/户外）。</summary>
        public string RoomRoleDefName;

        /// <summary>
        /// v1.1.4 建筑别名（per-pawn 兼容槽）：早期版本为人物维度工坊自定义名。
        /// 自全局别名表（BuildingAliases，按工坊实例共享）落地后仅作旧存档兼容回退；
        /// 新改名一律写入全局表，本字段保持 null。
        /// </summary>
        public string CustomName;

        /// <summary>工坊所在地图索引（Map.Index；-1 = 未记录）。</summary>
        public int MapIndex = -1;

        /// <summary>工坊所在位置（IntVec3；无效 = 未记录）。</summary>
        public IntVec3 Cell;

        public bool IsEmpty
        {
            get { return string.IsNullOrEmpty(BuildingDefName); }
        }

        public void RecordUse(string buildingDefName, string buildingStableId, string roomRoleDefName, long gameTick)
        {
            if (string.IsNullOrEmpty(buildingDefName))
            {
                return;
            }
            BuildingDefName = buildingDefName;
            BuildingStableId = string.IsNullOrEmpty(buildingStableId) ? null : buildingStableId;
            RoomRoleDefName = string.IsNullOrEmpty(roomRoleDefName) ? null : roomRoleDefName;
            UseCount++;
            if (gameTick > LastUsedTick)
            {
                LastUsedTick = gameTick;
            }
        }

        /// <summary>
        /// v1.1.4 UI 拓展：记录工坊坐标（捕获时由调用方传入），供 ITab「定位跳转」。
        /// 仅当坐标有效时更新，避免用旧坐标覆盖。
        /// </summary>
        public void RecordLocation(int mapIndex, IntVec3 cell)
        {
            if (mapIndex >= 0 && cell.IsValid)
            {
                MapIndex = mapIndex;
                Cell = cell;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref BuildingDefName, "buildingDefName");
            Scribe_Values.Look(ref BuildingStableId, "buildingStableId");
            Scribe_Values.Look(ref UseCount, "useCount", 0);
            Scribe_Values.Look(ref LastUsedTick, "lastUsedTick", -1L);
            Scribe_Values.Look(ref RoomRoleDefName, "roomRoleDefName");
            Scribe_Values.Look(ref CustomName, "customName");
            Scribe_Values.Look(ref MapIndex, "mapIndex", -1);
            Scribe_Values.Look(ref Cell, "cell");
        }
    }
}
