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
    /// Partial of <see cref="ArchiveService"/> — 地点查询，与重命名/别名的服务层转发。
    /// </summary>
    /// <remarks>
    /// · 职责：查询 pawn 的实时地点（地图 / 商队 / 无）与物品的当前持有者；
    ///   并把工位名、建筑别名、房间名与房间类型名的读写转发给 ChronicleGameComponent。
    /// · 调用方：Read Model 装配、UI 重命名交互、地点相关卡片。
    /// · 铁律：
    ///   - 本文件是纯转发层，不含任何业务判定。所有规则都在 ChronicleGameComponent 的对应方法中
    ///     （见 ChronicleGameComponent.Aliases.cs）。新增或调整读写行为请改那边，不要在这里加逻辑。
    ///   - 每个转发方法都用 try/catch 兜底并记录一次警告：这些调用来自玩家输入与 UI 交互，
    ///     单次失败不应打断界面。删除 try/catch 会让异常冒泡进 UI 的帧绘制。
    /// · 约定：参数非法时，写入方法静默返回、查询方法返回 null，一律不抛异常；
    ///   数据组件缺失时同样按空结果处理。
    /// </remarks>
    public sealed partial class ArchiveService : IArchiveService, IWorkIntensityService, IWorkTimeCaptureService, IArchiveQueryService, IArchiveEventSink
    {

        /// <summary>
        /// 查询 pawn 当前所处的地点。
        /// </summary>
        /// <param name="stableId">pawn 稳定 ID。</param>
        /// <returns>在地图上返回 LocationKind.Map 与生物群系名；
        /// 在商队中返回 LocationKind.Caravan 与地块 ID；否则为 LocationKind.None。</returns>
        /// <remarks>
        /// 1.6 适配要点：Map 没有直接的 def 属性，只能取 map.Biome.defName 作为地图标识；
        /// Pawn 也没有 IsWorldPawn 属性，判断其是否在世界地图上只能查它是否属于某个商队。
        /// 判定顺序（地图优先于商队）不可调换：在地图上时应以地图为准。
        /// </remarks>
        public LocationInfo GetLiveLocation(string stableId)
        {
            Pawn pawn = GetLivePawn(stableId);
            if (pawn == null)
            {
                return new LocationInfo(LocationKind.None, null, -1);
            }
            Map map = pawn.Map;
            if (map != null)
            {
                // 1.6: Map.Biome (BiomeDef) — Map has no direct `def` property.
                string mapDefName = map.Biome != null ? map.Biome.defName : null;
                return new LocationInfo(LocationKind.Map, mapDefName, -1);
            }
            // Off-map: check world caravans (1.6: Pawn has no IsWorldPawn —
            // presence in a caravan's PawnsListForReading is the check).
            int tile = WorldCaravanTile(pawn);
            if (tile >= 0)
            {
                return new LocationInfo(LocationKind.Caravan, null, tile);
            }
            return new LocationInfo(LocationKind.None, null, -1);
        }

        /// <summary>
        /// 查询物品当前的持有人：沿持有链向上查找最近的 pawn。
        /// </summary>
        /// <param name="stableId">物品稳定 ID。</param>
        /// <returns>持有该物品的 pawn；无人持有或物品不存在时返回 null。</returns>
        /// <remarks>
        /// 沿 IThingHolder.ParentHolder 逐级上溯，并显式处理装备与服装两种追踪器：
        /// 1.6 的 IThingHolder 接口没有 Parent 属性，装备/服装追踪器不会自动上溯到 pawn，
        /// 不显式处理会导致"装备中或穿着中的物品"查不到持有人。
        /// 上溯深度上限 32 是防御性设计，用于避免异常的持有链成环时死循环。
        /// </remarks>
        public Pawn GetCurrentHolder(string stableId)
        {
            Thing thing = FindLiveThing(stableId);
            if (thing == null)
            {
                return null;
            }
            IThingHolder holder = thing.ParentHolder;
            int depth = 0;
            while (holder != null && depth < 32)
            {
                Pawn pawn = holder as Pawn;
                if (pawn != null)
                {
                    return pawn;
                }
                // Explicit tracker handling: IThingHolder.ParentHolder is the only
                // upward link in 1.6 (no `Parent` property on the interface).
                Pawn_EquipmentTracker equipment = holder as Pawn_EquipmentTracker;
                if (equipment != null && equipment.pawn != null)
                {
                    return equipment.pawn;
                }
                Pawn_ApparelTracker apparel = holder as Pawn_ApparelTracker;
                if (apparel != null && apparel.pawn != null)
                {
                    return apparel.pawn;
                }
                holder = holder.ParentHolder;
                depth++;
            }
            return null;
        }

        /// <summary>
        /// 设置 pawn 工位的自定义名称（转发到数据层）。
        /// </summary>
        /// <param name="pawnStableId">pawn 稳定 ID；为空时静默返回。</param>
        /// <param name="customName">自定义名；空或空白表示清除覆盖。</param>
        /// <remarks>失败只记录警告而不抛出：来自 UI 的改名不应打断界面。
        /// 真实写入与"升级为全局建筑别名"的语义见 ChronicleGameComponent.SetWorkplaceCustomName。</remarks>
        public void SetWorkplaceCustomName(string pawnStableId, string customName)
        {
            if (string.IsNullOrEmpty(pawnStableId))
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                component.SetWorkplaceCustomName(pawnStableId, customName);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to set workplace custom name for " + pawnStableId + ": " + ex.Message);
            }
        }

        /// <summary>
        /// 设置建筑的全局别名（转发到数据层）。
        /// </summary>
        /// <param name="buildingStableId">建筑稳定 ID；为空时静默返回。</param>
        /// <param name="customName">别名；空或空白表示删除。</param>
        /// <remarks>会同步改写原版 Label 覆盖，副作用说明见 ChronicleGameComponent.SetBuildingAlias。</remarks>
        public void SetBuildingAlias(string buildingStableId, string customName)
        {
            if (string.IsNullOrEmpty(buildingStableId))
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                component.SetBuildingAlias(buildingStableId, customName);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to set building alias for " + buildingStableId + ": " + ex.Message);
            }
        }

        /// <summary>
        /// 设置房间角色的全局别名（转发到数据层）。
        /// </summary>
        /// <param name="roomRoleDefName">房间角色 defName；为空时静默返回。</param>
        /// <param name="customName">别名；空或空白表示删除。</param>
        /// <remarks>粒度是"角色"，一次改名会影响所有该角色的房间。</remarks>
        public void SetRoomRoleAlias(string roomRoleDefName, string customName)
        {
            if (string.IsNullOrEmpty(roomRoleDefName))
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                component.SetRoomRoleAlias(roomRoleDefName, customName);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to set room role alias for " + roomRoleDefName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// 设置某个 pawn 在特定房间角色下的房间名（转发到数据层）。
        /// </summary>
        /// <param name="pawnStableId">pawn 稳定 ID。</param>
        /// <param name="roomRoleDefName">房间角色 defName。</param>
        /// <param name="customName">房间名；空或空白表示删除覆盖。</param>
        /// <remarks>任一键为空时静默返回，不写入任何条目。</remarks>
        public void SetRoomName(string pawnStableId, string roomRoleDefName, string customName)
        {
            if (string.IsNullOrEmpty(pawnStableId) || string.IsNullOrEmpty(roomRoleDefName))
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                component.SetRoomName(pawnStableId, roomRoleDefName, customName);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to set room name for " + pawnStableId + ":" + roomRoleDefName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// 读取某个 pawn 在特定房间角色下的房间名。
        /// </summary>
        /// <param name="pawnStableId">pawn 稳定 ID。</param>
        /// <param name="roomRoleDefName">房间角色 defName。</param>
        /// <returns>覆盖名；无覆盖、参数非法、组件缺失或发生异常时返回 null。</returns>
        /// <remarks>返回 null 表示"没有覆盖"，调用方应回退到原版房间名。</remarks>
        public string GetRoomName(string pawnStableId, string roomRoleDefName)
        {
            if (string.IsNullOrEmpty(pawnStableId) || string.IsNullOrEmpty(roomRoleDefName))
            {
                return null;
            }
            try
            {
                ChronicleGameComponent component = Component;
                return component != null ? component.GetRoomName(pawnStableId, roomRoleDefName) : null;
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to get room name for " + pawnStableId + ":" + roomRoleDefName + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 设置某个 pawn 在特定房间角色下的房间类型名（转发到数据层）。
        /// </summary>
        /// <param name="pawnStableId">pawn 稳定 ID。</param>
        /// <param name="roomRoleDefName">房间角色 defName。</param>
        /// <param name="customName">房间类型名；空或空白表示删除覆盖。</param>
        /// <remarks>与 SetRoomName 共用 "pawnStableId:roomRoleDefName" 键格式，但落在不同字典，互不干扰。</remarks>
        public void SetRoomTypeName(string pawnStableId, string roomRoleDefName, string customName)
        {
            if (string.IsNullOrEmpty(pawnStableId) || string.IsNullOrEmpty(roomRoleDefName))
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                component.SetRoomTypeName(pawnStableId, roomRoleDefName, customName);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to set room type name for " + pawnStableId + ":" + roomRoleDefName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// 读取某个 pawn 在特定房间角色下的房间类型名。
        /// </summary>
        /// <param name="pawnStableId">pawn 稳定 ID。</param>
        /// <param name="roomRoleDefName">房间角色 defName。</param>
        /// <returns>覆盖名；无覆盖、参数非法、组件缺失或发生异常时返回 null。</returns>
        public string GetRoomTypeName(string pawnStableId, string roomRoleDefName)
        {
            if (string.IsNullOrEmpty(pawnStableId) || string.IsNullOrEmpty(roomRoleDefName))
            {
                return null;
            }
            try
            {
                ChronicleGameComponent component = Component;
                return component != null ? component.GetRoomTypeName(pawnStableId, roomRoleDefName) : null;
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to get room type name for " + pawnStableId + ":" + roomRoleDefName + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 读取建筑的全局别名。
        /// </summary>
        /// <param name="buildingStableId">建筑稳定 ID。</param>
        /// <returns>别名；无别名、参数非法、组件缺失或发生异常时返回 null。</returns>
        public string GetBuildingAlias(string buildingStableId)
        {
            if (string.IsNullOrEmpty(buildingStableId))
            {
                return null;
            }
            try
            {
                ChronicleGameComponent component = Component;
                return component != null ? component.GetBuildingAlias(buildingStableId) : null;
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to read building alias for " + buildingStableId + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 读取房间角色的全局别名。
        /// </summary>
        /// <param name="roomRoleDefName">房间角色 defName。</param>
        /// <returns>别名；无别名、参数非法、组件缺失或发生异常时返回 null。</returns>
        public string GetRoomRoleAlias(string roomRoleDefName)
        {
            if (string.IsNullOrEmpty(roomRoleDefName))
            {
                return null;
            }
            try
            {
                ChronicleGameComponent component = Component;
                return component != null ? component.GetRoomRoleAlias(roomRoleDefName) : null;
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to read room role alias for " + roomRoleDefName + ": " + ex.Message);
                return null;
            }
        }


    }
}
