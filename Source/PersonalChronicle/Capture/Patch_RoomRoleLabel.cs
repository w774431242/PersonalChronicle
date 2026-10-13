using HarmonyLib;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// v1.1.4 房间级展示层覆盖：Postfix on <c>Room.GetRoomRoleLabel</c>。
    ///
    /// 原版房间没有个体名字，显示名由 <c>Room.GetRoomRoleLabel()</c> 返回房间角色
    /// （如「卧室」），再在外层拼上拥有者变成「Luddel的卧室」。本 Postfix 拦截该返回值，
    /// 按优先级覆盖：
    ///   1. RoomNameOverrides[pawnId:role] — 整间房昵称（房间级，per-pawn）
    ///   2. RoomTypeOverrides[pawnId:role] — 类型名替换（房间级，per-pawn，仅 UI 替换）
    ///   3. RoomNameOverrides[:role]       — 类型关联兜底：工坊类房间无 owner，
    ///      取"任意给该 role 起过名的殖民者"的昵称（房间共享，显示首个自定义名）
    ///   4. RoomRoleAliases[role]         — 类型级全局（同类型全图一致）
    ///   5. 原版 RoomRoleDef.LabelCap      — 兜底
    ///
    /// 房间归属标识：取第一个玩家派系拥有者的 <c>GetUniqueLoadID()</c>。
    /// 工坊类房间（厨房/工坊/温室等）无 Owners，pawnId 为空，走 3) 类型关联兜底。
    /// （注：曾尝试 Find.Selector.SingleSelectedThing 取当前殖民者，但右键房间信息
    ///  面板时 selector 选中的是房间/建筑而非殖民者，恒为 null，故废弃该方案。）
    /// </summary>
    [HarmonyPatch(typeof(Room), "GetRoomRoleLabel")]
    public static class Patch_RoomRoleLabel
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

                // 房间归属：取第一个玩家派系拥有者（卧室/囚室有 owner）。
                // 工坊类房间（厨房/工坊/温室等）无 Owners → pawnId 为空，走类型关联兜底。
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
                string suffix = ":" + role.defName;

                // 1) 房间级整间房昵称（per-pawn）
                if (!string.IsNullOrEmpty(pawnId) && component.RoomNameOverrides != null
                    && component.RoomNameOverrides.Count > 0)
                {
                    string key = pawnId + suffix;
                    if (component.RoomNameOverrides.TryGetValue(key, out alias)
                        && !string.IsNullOrWhiteSpace(alias))
                    {
                        __result = alias;
                        return;
                    }
                }

                // 2) 房间级类型名替换（per-pawn）
                if (!string.IsNullOrEmpty(pawnId) && component.RoomTypeOverrides != null
                    && component.RoomTypeOverrides.Count > 0)
                {
                    string key = pawnId + suffix;
                    if (component.RoomTypeOverrides.TryGetValue(key, out alias)
                        && !string.IsNullOrWhiteSpace(alias))
                    {
                        __result = alias;
                        return;
                    }
                }

                // 3) 类型关联兜底：工坊等无 owner 房间，取任意给该 role 起过名的殖民者昵称。
                //    遍历 RoomNameOverrides，命中 key 以 ":role.defName" 结尾的首个非空值。
                if (string.IsNullOrEmpty(pawnId) && component.RoomNameOverrides != null
                    && component.RoomNameOverrides.Count > 0)
                {
                    foreach (var kv in component.RoomNameOverrides)
                    {
                        if (kv.Key != null && kv.Key.EndsWith(suffix, System.StringComparison.Ordinal)
                            && !string.IsNullOrWhiteSpace(kv.Value))
                        {
                            __result = kv.Value;
                            return;
                        }
                    }
                }

                // 4) 类型级全局别名
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
