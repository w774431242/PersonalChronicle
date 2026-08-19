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
    /// Partial of <see cref="ArchiveService"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveService : IArchiveService, IWorkIntensityService, IWorkTimeCaptureService, IArchiveQueryService, IArchiveEventSink
    {

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
