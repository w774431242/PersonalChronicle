using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 当前殖民地人口角色枚举。存档持久化走 PawnRecord.Role（int 落盘）。
    /// </summary>
    public enum PawnRole
    {
        None = 0,
        FreeColonist = 1,
        Slave = 2,
        Prisoner = 3
    }

    /// <summary>
    /// 活读人口单元：角色 + 对应的运行时 Pawn 引用。
    /// </summary>
    public sealed class ColonyMember
    {
        public Pawn Pawn;
        public PawnRole Role;
    }

    /// <summary>
    /// 活读殖民地人口扫描器——"当前殖民地人口"语义的单一来源。
    ///
    /// 覆盖范围（活读事实，非历史快照）：
    ///   - 地图上当前殖民地成员（自由殖民者 / 奴隶 / 囚犯），取自 RimWorld
    ///     的 *Spawned 精选列表 FreeColonistsSpawned + PrisonersOfColonySpawned
    ///     + SlavesOfColonySpawned（不用 AllPawns / 基础 FreeColonists，因为
    ///     它们含未落格的玩家派系人类，会虚高计数）。
    ///   - 世界商队（含载具商队 VehicleCaravan）成员
    /// 明确排除：
    ///   - 已死亡 / 被摧毁 的 pawn
    ///   - 非人类种族
    ///   - Anomaly 亚人类（mutant，consideredSubhuman）
    ///   - 纯世界 pawn（Find.WorldPawns.AllPawnsAlive）——太宽，会把其他殖民地 /
    ///     任务 / 世界对象的玩家派系 pawn 误计为当前人口（8≠3 根因），已移除。
    ///
    /// 角色分类由 TryClassify / ClassifyRole 单一实现；Data 回填、Application
    /// 活读、Capture 死亡归档共用，禁止在其它层复制谓词（口径分裂 = P1 红线）。
    /// </summary>
    public static class ChronicleColonistScanner
    {
        /// <summary>
        /// 分类当前殖民地人口的一员并输出其角色，**不检查死亡/销毁**（供死亡
        /// 归档时调用——此时 pawn 已 Dead，但 Faction / IsPrisonerOfColony /
        /// IsSlave 仍可判定其生前角色）。
        ///
        /// 判定顺序（RimWorld 1.6 实测语义）：
        ///   1. 基本门控：null / 非人类 / 亚人类 → None
        ///   2. 囚犯：IsPrisonerOfColony 为真 → Prisoner
        ///      （囚犯 Faction 是其原派系，非玩家派系，故先判定）
        ///   3. 玩家派系内：IsSlave → Slave，否则 FreeColonist
        /// </summary>
        public static PawnRole ClassifyRole(Pawn pawn)
        {
            if (pawn == null || !pawn.RaceProps.Humanlike)
            {
                return PawnRole.None;
            }
            if (DlcStatus.IsSubhuman(pawn))
            {
                return PawnRole.None;
            }
            if (pawn.IsPrisonerOfColony)
            {
                return PawnRole.Prisoner;
            }
            if (pawn.IsSlaveOfColony
                || (pawn.Faction != null && pawn.Faction.IsPlayer && pawn.IsSlave))
            {
                return PawnRole.Slave;
            }
            if (pawn.Faction != null && pawn.Faction.IsPlayer
                && (pawn.IsColonist || pawn.IsColonistPlayerControlled))
            {
                return PawnRole.FreeColonist;
            }
            return PawnRole.None;
        }

        /// <summary>
        /// 把 pawn 归类为当前殖民地人口的一员（活读），并输出其角色。
        /// 在 ClassifyRole 之上追加 死亡/销毁 门控——已死的 pawn 不再算作当前人口。
        /// 不通过则返回 false 且 role = None。
        /// </summary>
        public static bool TryClassify(Pawn pawn, out PawnRole role)
        {
            role = PawnRole.None;
            if (pawn == null || pawn.Dead || pawn.Destroyed)
            {
                return false;
            }
            role = ClassifyRole(pawn);
            return role != PawnRole.None;
        }

        /// <summary>
        /// Classifies a pawn only when it is a member of the live colony
        /// population: a spawned colonist/prisoner/slave on a loaded map or a
        /// pawn currently inside a player caravan.
        ///
        /// This is deliberately separate from TryClassify. SetFaction and
        /// death capture need role-only classification at lifecycle edges,
        /// while social/combat/production capture must not accept unspawned
        /// scenario-editor candidates that merely have the player faction.
        /// </summary>
        public static bool TryClassifyCurrent(Pawn pawn, out PawnRole role)
        {
            role = PawnRole.None;
            if (!TryClassify(pawn, out role))
            {
                return false;
            }

            if (pawn.Spawned && pawn.Map != null && pawn.Map.mapPawns != null)
            {
                MapPawns mapPawns = pawn.Map.mapPawns;
                if (ContainsPawn(mapPawns.FreeColonistsSpawned, pawn)
                    || ContainsPawn(mapPawns.PrisonersOfColonySpawned, pawn)
                    || ContainsPawn(mapPawns.SlavesOfColonySpawned, pawn))
                {
                    return true;
                }
            }

            if (Find.World != null && Find.WorldObjects != null)
            {
                List<Caravan> caravans = Find.WorldObjects.Caravans;
                for (int i = 0; i < caravans.Count; i++)
                {
                    Caravan caravan = caravans[i];
                    if (caravan != null && ContainsPawn(caravan.PawnsListForReading, pawn))
                    {
                        return true;
                    }
                }
            }

            role = PawnRole.None;
            return false;
        }

        /// <summary>
        /// 枚举当前所有殖民地人口——地图 + 商队两源合并，按 thingIDNumber 去重
        /// （同一 pawn 可同时被地图与商队引用；VF 双注册防御）。
        /// 返回每帧新列表（调用方拥有）。未通过 TryClassify 的 pawn 不入列。
        /// </summary>
        public static List<ColonyMember> EnumerateCurrentPeople()
        {
            List<ColonyMember> result = new List<ColonyMember>();
            HashSet<int> seen = new HashSet<int>();

            // 源 1：每张已加载地图的"当前殖民地成员"精选列表（权威口径）。
            // 注意：必须用 *Spawned 变体，而非 AllPawns 或基础 FreeColonists /
            // PrisonersOfColony——基础列表含 AllPawnsUnspawned（未落格的玩家派系
            // 人类：冷冻舱 / 运输舱 / 在途 pawn），会把非在场的成员误计为"当前
            // 人口"（8≠3 的根因）。*Spawned 变体 = 玩家实际在场上看到的成员。
            //   FreeColonistsSpawned     : 自由殖民者（玩家派系、人类、非囚犯、非奴隶）
            //   PrisonersOfColonySpawned : 殖民地囚犯
            //   SlavesOfColonySpawned     : 殖民地奴隶（RimWorld 仅提供 Spawned 版）
            // 三者并集 = 当前在场殖民地人口；角色仍由单一谓词 ClassifyRole 赋值
            // （P1 红线：禁止在别处复制口径）。
            List<Map> maps = Find.Maps;
            if (maps != null)
            {
                for (int i = 0; i < maps.Count; i++)
                {
                    Map map = maps[i];
                    if (map == null || map.mapPawns == null)
                    {
                        continue;
                    }
                    // Spawned lists are intentional here. During scenario
                    // setup, MapPawns.PawnsInFaction also contains candidate
                    // pawns created by the starting-pawn editor that never
                    // became part of the running colony.
                    AddFromList(result, seen, map.mapPawns.FreeColonistsSpawned);
                    AddFromList(result, seen, map.mapPawns.PrisonersOfColonySpawned);
                    AddFromList(result, seen, map.mapPawns.SlavesOfColonySpawned);
                }
            }

            // 源 2：世界商队（成员是世界 pawn，不在地图上；VehicleCaravan 同此列表）。
            if (Find.World != null && Find.WorldObjects != null)
            {
                List<Caravan> caravans = Find.WorldObjects.Caravans;
                for (int i = 0; i < caravans.Count; i++)
                {
                    Caravan caravan = caravans[i];
                    if (caravan == null)
                    {
                        continue;
                    }
                    AddFromList(result, seen, caravan.PawnsListForReading);
                }
            }

            return result;
        }

        /// <summary>
        /// 诊断用：返回当前活读人口的逐项明细（名称 / 角色 / 派系 / 是否落格 /
        /// 是否囚犯 / 是否奴隶 / thingIDNumber）。仅在 mod 设置 DebugLiveCount
        /// 开启时由 ArchiveService 输出到日志，用于排查"人数不符"。
        /// </summary>
        public static string DumpLivePopulation()
        {
            List<ColonyMember> people = EnumerateCurrentPeople();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("[PersonalChronicle] Live population dump (count=" + people.Count + "):");
            for (int i = 0; i < people.Count; i++)
            {
                ColonyMember m = people[i];
                Pawn p = m.Pawn;
                if (p == null)
                {
                    sb.AppendLine("  #" + i + " <null pawn>");
                    continue;
                }
                string name = p.Name != null ? p.Name.ToStringFull : p.LabelShort;
                string faction = p.Faction != null && p.Faction.def != null
                    ? p.Faction.def.defName
                    : "(none)";
                sb.AppendLine("  #" + i
                    + " name=" + name
                    + " role=" + m.Role
                    + " faction=" + faction
                    + " spawned=" + p.Spawned
                    + " prisoner=" + p.IsPrisonerOfColony
                    + " slave=" + p.IsSlave
                    + " id=" + p.thingIDNumber);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 把列表中的 pawn 逐个交给 AddIfNew（空列表安全）。
        /// </summary>
        private static void AddFromList(List<ColonyMember> result, HashSet<int> seen, List<Pawn> pawns)
        {
            if (pawns == null)
            {
                return;
            }
            for (int i = 0; i < pawns.Count; i++)
            {
                AddIfNew(result, seen, pawns[i]);
            }
        }

        /// <summary>
        /// 把 <paramref name="pawn"/> 作为 ColonyMember 追加到 <paramref name="result"/>
        /// 当且仅当通过 TryClassify 且本次去重未见过（thingIDNumber 跨两源去重）。
        /// </summary>
        private static void AddIfNew(List<ColonyMember> result, HashSet<int> seen, Pawn pawn)
        {
            PawnRole role;
            if (!TryClassify(pawn, out role))
            {
                return;
            }
            if (seen.Add(pawn.thingIDNumber))
            {
                result.Add(new ColonyMember { Pawn = pawn, Role = role });
            }
        }

        private static bool ContainsPawn(List<Pawn> pawns, Pawn target)
        {
            if (pawns == null || target == null)
            {
                return false;
            }
            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i] == target)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
