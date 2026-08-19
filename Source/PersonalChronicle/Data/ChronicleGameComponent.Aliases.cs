using System.Collections.Generic;
using System.Collections.ObjectModel;
using PersonalChronicle.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Data
{
    /// <summary>Partial of ChronicleGameComponent 鈥?see main file for Scribe/migration.</summary>
    public sealed partial class ChronicleGameComponent : GameComponent
    {

        internal bool SetWorkplaceCustomName(string pawnStableId, string customName)
        {
            if (string.IsNullOrEmpty(pawnStableId))
            {
                return false;
            }
            PawnObject pawn = GetObject(pawnStableId) as PawnObject;
            if (pawn == null || pawn.Workplace == null || pawn.Workplace.IsEmpty)
            {
                return false;
            }
            // 升级语义：新写入直接进全局工坊实例别名表（key = BuildingStableId）。
            if (!string.IsNullOrEmpty(pawn.Workplace.BuildingStableId))
            {
                return SetBuildingAlias(pawn.Workplace.BuildingStableId, customName);
            }
            string normalized = string.IsNullOrWhiteSpace(customName) ? null : customName.Trim();
            if (string.Equals(pawn.Workplace.CustomName, normalized, System.StringComparison.Ordinal))
            {
                return false;
            }
            pawn.Workplace.CustomName = normalized;
            MarkChanged();
            return true;
        }

        internal bool SetBuildingAlias(string buildingStableId, string customName)
        {
            if (string.IsNullOrEmpty(buildingStableId))
            {
                return false;
            }
            if (BuildingAliases == null)
            {
                BuildingAliases = new Dictionary<string, string>();
            }
            string normalized = string.IsNullOrWhiteSpace(customName) ? null : customName.Trim();
            string existing;
            bool hasExisting = BuildingAliases.TryGetValue(buildingStableId, out existing);
            if (string.Equals(existing, normalized, System.StringComparison.Ordinal))
            {
                return false;
            }
            if (normalized == null)
            {
                BuildingAliases.Remove(buildingStableId);
            }
            else
            {
                BuildingAliases[buildingStableId] = normalized;
            }
            // v1.1.4 同步更新原版 Label 覆盖表（从 stableId 解析 thingIDNumber）。
            int thingId = ParseThingIdFromStableId(buildingStableId);
            if (thingId > 0)
            {
                SetBuildingLabelOverride(thingId, normalized);
            }
            MarkChanged();
            return true;
        }

        private static int ParseThingIdFromStableId(string buildingStableId)
        {
            if (string.IsNullOrEmpty(buildingStableId)) return 0;
            int lastColon = buildingStableId.LastIndexOf(':');
            if (lastColon < 0 || lastColon >= buildingStableId.Length - 1) return 0;
            string idPart = buildingStableId.Substring(lastColon + 1);
            int result;
            if (int.TryParse(idPart, out result)) return result;
            return 0;
        }

        private void SetBuildingLabelOverride(int thingId, string label)
        {
            if (BuildingLabelOverrides == null)
            {
                BuildingLabelOverrides = new Dictionary<int, string>();
            }
            if (label == null)
            {
                BuildingLabelOverrides.Remove(thingId);
            }
            else
            {
                BuildingLabelOverrides[thingId] = label;
            }
        }

        internal string GetBuildingAlias(string buildingStableId)
        {
            if (string.IsNullOrEmpty(buildingStableId) || BuildingAliases == null)
            {
                return null;
            }
            string alias;
            return BuildingAliases.TryGetValue(buildingStableId, out alias) ? alias : null;
        }

        internal bool SetRoomRoleAlias(string roomRoleDefName, string customName)
        {
            if (string.IsNullOrEmpty(roomRoleDefName))
            {
                return false;
            }
            if (RoomRoleAliases == null)
            {
                RoomRoleAliases = new Dictionary<string, string>();
            }
            string normalized = string.IsNullOrWhiteSpace(customName) ? null : customName.Trim();
            string existing;
            bool hasExisting = RoomRoleAliases.TryGetValue(roomRoleDefName, out existing);
            if (string.Equals(existing, normalized, System.StringComparison.Ordinal))
            {
                return false;
            }
            if (normalized == null)
            {
                RoomRoleAliases.Remove(roomRoleDefName);
            }
            else
            {
                RoomRoleAliases[roomRoleDefName] = normalized;
            }
            MarkChanged();
            return true;
        }

        internal string GetRoomRoleAlias(string roomRoleDefName)
        {
            if (string.IsNullOrEmpty(roomRoleDefName) || RoomRoleAliases == null)
            {
                return null;
            }
            string alias;
            return RoomRoleAliases.TryGetValue(roomRoleDefName, out alias) ? alias : null;
        }

        internal bool SetRoomName(string pawnStableId, string roomRoleDefName, string customName)
        {
            if (string.IsNullOrEmpty(pawnStableId) || string.IsNullOrEmpty(roomRoleDefName))
            {
                return false;
            }
            if (RoomNameOverrides == null)
            {
                RoomNameOverrides = new Dictionary<string, string>();
            }
            string key = pawnStableId + ":" + roomRoleDefName;
            string normalized = string.IsNullOrWhiteSpace(customName) ? null : customName.Trim();
            string existing;
            bool hasExisting = RoomNameOverrides.TryGetValue(key, out existing);
            if (string.Equals(existing, normalized, System.StringComparison.Ordinal))
            {
                return false;
            }
            if (normalized == null)
            {
                RoomNameOverrides.Remove(key);
            }
            else
            {
                RoomNameOverrides[key] = normalized;
            }
            MarkChanged();
            return true;
        }

        internal string GetRoomName(string pawnStableId, string roomRoleDefName)
        {
            if (string.IsNullOrEmpty(pawnStableId) || string.IsNullOrEmpty(roomRoleDefName)
                || RoomNameOverrides == null)
            {
                return null;
            }
            string key = pawnStableId + ":" + roomRoleDefName;
            string alias;
            return RoomNameOverrides.TryGetValue(key, out alias) ? alias : null;
        }

        internal bool SetRoomTypeName(string pawnStableId, string roomRoleDefName, string customName)
        {
            if (string.IsNullOrEmpty(pawnStableId) || string.IsNullOrEmpty(roomRoleDefName))
            {
                return false;
            }
            if (RoomTypeOverrides == null)
            {
                RoomTypeOverrides = new Dictionary<string, string>();
            }
            string key = pawnStableId + ":" + roomRoleDefName;
            string normalized = string.IsNullOrWhiteSpace(customName) ? null : customName.Trim();
            string existing;
            bool hasExisting = RoomTypeOverrides.TryGetValue(key, out existing);
            if (string.Equals(existing, normalized, System.StringComparison.Ordinal))
            {
                return false;
            }
            if (normalized == null)
            {
                RoomTypeOverrides.Remove(key);
            }
            else
            {
                RoomTypeOverrides[key] = normalized;
            }
            MarkChanged();
            return true;
        }

        internal string GetRoomTypeName(string pawnStableId, string roomRoleDefName)
        {
            if (string.IsNullOrEmpty(pawnStableId) || string.IsNullOrEmpty(roomRoleDefName)
                || RoomTypeOverrides == null)
            {
                return null;
            }
            string key = pawnStableId + ":" + roomRoleDefName;
            string alias;
            return RoomTypeOverrides.TryGetValue(key, out alias) ? alias : null;
        }

        private static string ResolvePlaceKey(Pawn pawn, out string placeKind)
        {
            placeKind = null;
            if (pawn == null)
            {
                return null;
            }
            if (pawn.Map != null && pawn.Map.Biome != null)
            {
                placeKind = PlaceVisitKeys.KindMap;
                return pawn.Map.Biome.defName;
            }
            // Off-map caravan tile (same scan sources as live location).
            List<Caravan> caravans = Find.WorldObjects != null ? Find.WorldObjects.Caravans : null;
            if (caravans != null)
            {
                for (int i = 0; i < caravans.Count; i++)
                {
                    Caravan c = caravans[i];
                    if (c == null || c.PawnsListForReading == null)
                    {
                        continue;
                    }
                    if (c.PawnsListForReading.Contains(pawn))
                    {
                        placeKind = PlaceVisitKeys.KindCaravan;
                        // 1.6: Caravan.Tile is PlanetTile — persist tileId only.
                        return PlaceVisitKeys.TileKeyPrefix + c.Tile.tileId;
                    }
                }
            }
            return null;
        }

        private static void CapturePrimaryPlace(PawnObject record, Pawn pawn)
        {
            if (record == null || pawn == null)
            {
                return;
            }
            string kind;
            string key = ResolvePlaceKey(pawn, out kind);
            if (!string.IsNullOrEmpty(key) && kind == PlaceVisitKeys.KindMap)
            {
                record.PrimaryPlaceDefName = key;
            }
        }

        private static WorkTypeDef ResolveCurrentWorkType(Pawn pawn)
        {
            if (pawn == null || pawn.CurJob == null)
            {
                return null;
            }
            Job job = pawn.CurJob;
            if (job.workGiverDef != null && job.workGiverDef.workType != null)
            {
                return job.workGiverDef.workType;
            }
            // Some jobs carry workType on the def indirectly via workGiver only.
            return null;
        }


    }
}
