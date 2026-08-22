using HarmonyLib;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// v1.1.4 房间级展示层覆盖：Postfix on <c>Room.GetRoomRoleLabel</c>。
    ///
    /// 原版房间没有个体名字，显示名由 <c>Room.GetRoomRoleLabel()</c> 返回房间角色
    /// （如「卧室」），再在外层拼上拥有者变成「Luddel的卧室」。本 Postfix 拦截该返回值，
    /// 按四级优先级覆盖：
    ///   1. RoomNameOverrides[pawnId:role] — 整间房昵称（最高，房间级）
    ///   2. RoomTypeOverrides[pawnId:role] — 类型名替换（房间级，仅 UI 替换）
    ///   3. RoomRoleAliases[role]         — 类型级全局（同类型全图一致）
    ///   4. 原版 RoomRoleDef.LabelCap      — 兜底
    ///
    /// 房间归属标识：取第一个玩家派系拥有者的 <c>GetUniqueLoadID()</c>。
    /// </summary>
    [HarmonyPatch(typeof(Room), "GetRoomRoleLabel")]
    internal static class Patch_RoomRoleLabel
    {
        static void Postfix(Room __instance, ref string __result)
        {
            try
            {
                if (__instance == null || __result == null) return;

                var component = Current.Game?.GetComponent<Data.ChronicleGameComponent>();
                if (component == null) return;

                RoomRoleDef role = __instance.Role;
                if (role == null) return;

                // 房间归属：取第一个玩家派系拥有者。
                Pawn owner = null;
                foreach (Pawn p in __instance.Owners)
                {
                    if (p != null && p.Faction != null && p.Faction.IsPlayer)
                    {
                        owner = p;
                        break;
                    }
                }
                string pawnId = owner != null ? owner.GetUniqueLoadID() : null;

                string alias = null;

                // 1) 房间级整间房昵称
                if (!string.IsNullOrEmpty(pawnId) && component.RoomNameOverrides != null
                    && component.RoomNameOverrides.Count > 0)
                {
                    string key = pawnId + ":" + role.defName;
                    if (component.RoomNameOverrides.TryGetValue(key, out alias)
                        && !string.IsNullOrWhiteSpace(alias))
                    {
                        __result = alias;
                        return;
                    }
                }

                // 2) 房间级类型名替换
                if (!string.IsNullOrEmpty(pawnId) && component.RoomTypeOverrides != null
                    && component.RoomTypeOverrides.Count > 0)
                {
                    string key = pawnId + ":" + role.defName;
                    if (component.RoomTypeOverrides.TryGetValue(key, out alias)
                        && !string.IsNullOrWhiteSpace(alias))
                    {
                        __result = alias;
                        return;
                    }
                }

                // 3) 类型级全局别名
                if (component.RoomRoleAliases != null && component.RoomRoleAliases.Count > 0)
                {
                    if (component.RoomRoleAliases.TryGetValue(role.defName, out alias)
                        && !string.IsNullOrWhiteSpace(alias))
                    {
                        __result = alias;
                    }
                }
            }
            catch (System.Exception)
            {
                // never break the call site
            }
        }
    }
}
