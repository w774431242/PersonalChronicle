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
    /// Partial of ChronicleGameComponent — 别名/覆盖表读写，以及地点键与当前工种的解析。
    /// </summary>
    /// <remarks>
    /// · 职责：维护建筑别名、房间角色别名、房间名、房间类型四张重命名表与原版 Label 覆盖表；
    ///   并提供地点键（生物群系 / 商队地块）与当前工种（workGiver）的解析。
    /// · 调用方：重命名交互（Dialog / ITab）、Read Model 装配、地点与工时采样。
    /// · 禁止：本文件是上述字典的唯一写入口。外部不得直接改字典字段，
    ///   否则会漏掉 MarkChanged（存档不落盘）与 Label 覆盖同步。
    /// · 关键约定：
    ///   - 所有 Set 方法统一做"空或空白 → null → 删除条目"的归一化，因此清空名字等于移除覆盖，而不是写入空串。
    ///   - 内容未变化时返回 false 且不调用 MarkChanged，避免产生无意义的存档脏标记。
    ///   - BuildingStableId 非空时，工位重命名升级为写入全局建筑别名表（key = 建筑 stableId），
    ///     名称对该建筑全局生效而非仅对单个 pawn —— 这是有意的升级语义，勿"修正"回只改 pawn 侧。
    ///   - 改建筑别名会同步写 BuildingLabelOverrides，靠 ParseThingIdFromStableId 从 stableId 反解 thingIDNumber。
    /// · 待清理：部分方法内 hasExisting 变量赋值后未被使用（无副作用，列为 P1 统一清理项）。
    /// · Scribe 与版本迁移见 ChronicleGameComponent.cs。
    /// </remarks>
    public sealed partial class ChronicleGameComponent : GameComponent
    {

        /// <summary>
        /// 为 pawn 的工位设置自定义名称。
        /// </summary>
        /// <param name="pawnStableId">pawn 稳定 ID。</param>
        /// <param name="customName">自定义名；空或空白表示清除覆盖。</param>
        /// <returns>发生变化返回 true；参数非法、无工位、或新旧值相同时返回 false。</returns>
        /// <remarks>
        /// 优先走全局建筑别名（BuildingStableId 非空时），名称对该建筑全局生效。
        /// 只有缺失建筑 ID 的退化情况才回落到 pawn.Workplace.CustomName（仅影响该 pawn）。
        /// </remarks>
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

        /// <summary>
        /// 设置建筑的全局别名，并同步原版 Label 覆盖。
        /// </summary>
        /// <param name="buildingStableId">建筑稳定 ID（尾部含 thingIDNumber）。</param>
        /// <param name="customName">别名；空或空白表示删除该别名。</param>
        /// <returns>发生变化返回 true；新旧值相同时返回 false。</returns>
        /// <remarks>
        /// 副作用：会调用 SetBuildingLabelOverride 改写 BuildingLabelOverrides，让原版 UI 也显示别名。
        /// 删除别名时同步移除覆盖，否则会残留旧 Label 造成"已改名但界面仍显示旧名"。
        /// </remarks>
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

        /// <summary>
        /// 从建筑 stableId 反解 thingIDNumber：取最后一个冒号之后的数字段。
        /// </summary>
        /// <param name="buildingStableId">建筑稳定 ID。</param>
        /// <returns>解析成功返回正整数 thingIDNumber；格式不符或非数字时返回 0。</returns>
        /// <remarks>调用方必须对 0 跳过 Label 覆盖同步：0 不是合法 thingID，写入会污染覆盖表。</remarks>
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

        /// <summary>
        /// 写入或移除单个建筑的原版 Label 覆盖。
        /// </summary>
        /// <param name="thingId">建筑 thingIDNumber（须为正数）。</param>
        /// <param name="label">覆盖文案；null 表示移除覆盖。</param>
        /// <remarks>本方法只改字典、不调用 MarkChanged：脏标记由 SetBuildingAlias 统一负责，避免重复标记。</remarks>
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

        /// <summary>
        /// 读取建筑的全局别名。
        /// </summary>
        /// <param name="buildingStableId">建筑稳定 ID。</param>
        /// <returns>别名；无别名、参数非法或字典尚未初始化时返回 null。</returns>
        internal string GetBuildingAlias(string buildingStableId)
        {
            if (string.IsNullOrEmpty(buildingStableId) || BuildingAliases == null)
            {
                return null;
            }
            string alias;
            return BuildingAliases.TryGetValue(buildingStableId, out alias) ? alias : null;
        }

        /// <summary>
        /// 设置房间角色（RoomRole defName）的全局别名。
        /// </summary>
        /// <param name="roomRoleDefName">房间角色 defName。</param>
        /// <param name="customName">别名；空或空白表示删除。</param>
        /// <returns>发生变化返回 true；新旧值相同时返回 false。</returns>
        /// <remarks>粒度是"角色"而非单个房间，因此一次改名会影响所有该角色的房间。</remarks>
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

        /// <summary>
        /// 读取房间角色的全局别名。
        /// </summary>
        /// <param name="roomRoleDefName">房间角色 defName。</param>
        /// <returns>别名；不存在时返回 null。</returns>
        internal string GetRoomRoleAlias(string roomRoleDefName)
        {
            if (string.IsNullOrEmpty(roomRoleDefName) || RoomRoleAliases == null)
            {
                return null;
            }
            string alias;
            return RoomRoleAliases.TryGetValue(roomRoleDefName, out alias) ? alias : null;
        }

        /// <summary>
        /// 设置某个 pawn 在特定房间角色下的房间名覆盖。
        /// </summary>
        /// <param name="pawnStableId">pawn 稳定 ID。</param>
        /// <param name="roomRoleDefName">房间角色 defName。</param>
        /// <param name="customName">房间名；空或空白表示删除覆盖。</param>
        /// <returns>发生变化返回 true；新旧值相同时返回 false。</returns>
        /// <remarks>复合键格式为 "pawnStableId:roomRoleDefName"，
        /// 与 SetRoomTypeName 键格式相同但落在不同的字典，两者互不干扰。</remarks>
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

        /// <summary>
        /// 读取某个 pawn 在特定房间角色下的房间名覆盖。
        /// </summary>
        /// <returns>覆盖名；不存在时返回 null（调用方应回退到原版房间名）。</returns>
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

        /// <summary>
        /// 设置某个 pawn 在特定房间角色下的房间类型名覆盖。
        /// </summary>
        /// <param name="pawnStableId">pawn 稳定 ID。</param>
        /// <param name="roomRoleDefName">房间角色 defName。</param>
        /// <param name="customName">房间类型名；空或空白表示删除覆盖。</param>
        /// <returns>发生变化返回 true；新旧值相同时返回 false。</returns>
        /// <remarks>与 SetRoomName 共用 "pawnStableId:roomRoleDefName" 键格式，可同时设置且互不影响。</remarks>
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

        /// <summary>
        /// 读取某个 pawn 在特定房间角色下的房间类型名覆盖。
        /// </summary>
        /// <returns>覆盖名；不存在时返回 null（调用方应回退到角色默认名）。</returns>
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

        /// <summary>
        /// 解析 pawn 当前所处地点的持久化键。
        /// </summary>
        /// <param name="pawn">目标 pawn。</param>
        /// <param name="placeKind">输出地点种类：地图（KindMap）或商队地块（KindCaravan）。</param>
        /// <returns>地图内返回生物群系 defName；商队返回带前缀的 tileId；无法判定返回 null。</returns>
        /// <remarks>
        /// 商队分支只持久化 tileId 而不是 PlanetTile 本身 —— 1.6 的 Caravan.Tile 是 PlanetTile 结构体，
        /// 直接存会引入不适合长期存档的类型。前缀常量见 PlaceVisitKeys.TileKeyPrefix。
        /// 判定顺序（地图优先于商队）不可调换：在地图上时商队列表里可能仍残留引用。
        /// </remarks>
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

        /// <summary>
        /// 记录 pawn 的"主要地点"，只接受地图生物群系。
        /// </summary>
        /// <param name="record">目标 pawn 归档对象。</param>
        /// <param name="pawn">存活 pawn。</param>
        /// <remarks>刻意排除商队地块：商队是临时状态，若计入会让"主要地点"随商队移动而漂移，
        /// 失去档案意义。修改前先确认是否真的需要覆盖这一约束。</remarks>
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

        /// <summary>
        /// 从当前 job 解析其所属工种。
        /// </summary>
        /// <param name="pawn">目标 pawn。</param>
        /// <returns>job.workGiverDef.workType；无 job 或 workGiver 未声明工种时返回 null。</returns>
        /// <remarks>
        /// 只认 workGiverDef.workType，刻意不做 job.def 侧的推断：部分 job 仅在 workGiver 上携带工种，
        /// 猜测式兜底会把非工作 job（如休息、就医）算成工时。返回 null 时采样方跳过本次采样即可。
        /// </remarks>
        private static WorkTypeDef ResolveCurrentWorkType(Pawn pawn)
        {
            if (pawn == null || pawn.CurJob == null)
            {
                return null;
            }
            Job job = pawn.CurJob;
            if (job.workGiverDef != null && job.workGiverDef.workType != null)
            {
                // CHG-20261004-MEALSCHEDULE：三餐作息类型**不计入工时**。
                // 吃饭不是工作：若放行，档案馆会出现"用餐"工时行，并且每 2500 tick 派生一条
                // CareerEvent WorkCompleted（= 每小时"完成一次工作"）⇒ 职业档案被吃饭刷满。
                // 单点在此排除，采样主循环（SampleWorkTimeAndPlaces）与
                // AddWorkTimeSample 两个下游入口同时受益（GOV-005：不重复实现）。
                if (job.workGiverDef.workType.defName
                    == Domain.Meal.MealScheduleDefaults.MealWorkTypeDefName)
                {
                    return null;
                }
                return job.workGiverDef.workType;
            }
            // Some jobs carry workType on the def indirectly via workGiver only.
            return null;
        }


    }
}
