using System.Collections.Generic;
using System.Collections.ObjectModel;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// [ARC-013] <see cref="ChronicleGameComponent"/> 的「工作台/住所采样」切片
    /// （SampleWorkTimeAndPlaces / SamplePlaceVisit / SampleResidence）。
    ///
    /// 原 ChronicleGameComponent.Sampling.cs 单文件 942 行，超出 [ARC-013] 上限。
    /// Save 契约铁律：GameComponentTick 调度与全部字段保留原文件；本文件仅物理搬移
    /// 三个纯采样方法（由 Tick 周期调用，无 Scribe 副作用），零 Save schema 改动。
    /// </summary>
    public sealed partial class ChronicleGameComponent : GameComponent
    {
        private void SampleWorkTimeAndPlaces(long gameTick)
        {
            if (Objects == null || Objects.Count == 0)
            {
                return;
            }
            // One live scan → stableId map (avoid O(n²) per-object rescans).
            Dictionary<string, Pawn> liveById = new Dictionary<string, Pawn>();
            List<ColonyMember> live = ChronicleColonistScanner.EnumerateCurrentPeople();
            for (int i = 0; i < live.Count; i++)
            {
                ColonyMember m = live[i];
                if (m == null || m.Pawn == null)
                {
                    continue;
                }
                string id = m.Pawn.GetUniqueLoadID();
                if (!string.IsNullOrEmpty(id) && !liveById.ContainsKey(id))
                {
                    liveById[id] = m.Pawn;
                }
            }

            for (int i = 0; i < Objects.Count; i++)
            {
                PawnObject record = Objects[i] as PawnObject;
                if (record == null || record.IsArchived || string.IsNullOrEmpty(record.StableId))
                {
                    continue;
                }
                Pawn pawn;
                if (!liveById.TryGetValue(record.StableId, out pawn) || pawn == null || pawn.Dead || pawn.Downed)
                {
                    continue;
                }
                bool changed = false;
                WorkTypeDef workType = ResolveCurrentWorkType(pawn);
                if (workType != null)
                {
                    if (record.WorkTime == null)
                    {
                        record.WorkTime = new WorkTimeAccumulator();
                    }
                    record.WorkTime.AddSample(workType.defName, WorkSampleCreditTicks, gameTick);
                    changed = true;
                    // P1 CareerEvent WorkCompleted：每累计 2500 工作tick（=游戏 1 小时）派生
                    // 一条事实（服务内无状态推导，幂等；差额不足一个 quantum 时零写入）。
                    IArchiveService careerSvc = PersonalChronicleMod.ArchiveService;
                    if (careerSvc != null)
                    {
                        careerSvc.RecordWorkCompletedQuantum(record.StableId, gameTick);
                    }
                }
                // P3: place enter/leave ledger (always, even when idle).
                if (SamplePlaceVisit(record, pawn, gameTick))
                {
                    changed = true;
                }
                // v1.1.4 方案 B：定期采样住所（pawn.ownership.OwnedRoom.Role），
                // 只存 RoomRoleDef.defName 稳定键（UI 实时解析 label，改名/翻译自适应）。
                if (SampleResidence(record, pawn, gameTick))
                {
                    changed = true;
                }
                if (changed)
                {
                    MarkChanged();
                }
            }
        }

        private static bool SamplePlaceVisit(PawnObject record, Pawn pawn, long gameTick)
        {
            if (record == null || pawn == null)
            {
                return false;
            }
            string kind;
            string key = ResolvePlaceKey(pawn, out kind);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            if (record.PlaceHistory == null)
            {
                record.PlaceHistory = new List<PlaceVisit>();
            }
            PlaceVisit open = null;
            for (int i = record.PlaceHistory.Count - 1; i >= 0; i--)
            {
                PlaceVisit v = record.PlaceHistory[i];
                if (v != null && v.IsOpen)
                {
                    open = v;
                    break;
                }
            }
            if (open != null && open.PlaceKey == key)
            {
                // Still here — keep primary in sync.
                if (kind == PlaceVisitKeys.KindMap)
                {
                    if (record.PrimaryPlaceDefName != key)
                    {
                        record.PrimaryPlaceDefName = key;
                        return true;
                    }
                }
                return false;
            }
            if (open != null)
            {
                open.LeaveTick = gameTick;
            }
            record.PlaceHistory.Add(new PlaceVisit(key, kind, gameTick));
            // Cap history length to avoid unbounded growth (keeps last 32 stays).
            const int maxVisits = 32;
            while (record.PlaceHistory.Count > maxVisits)
            {
                record.PlaceHistory.RemoveAt(0);
            }
            if (kind == PlaceVisitKeys.KindMap)
            {
                record.PrimaryPlaceDefName = key;
            }
            return true;
        }

        private static bool SampleResidence(PawnObject record, Pawn pawn, long gameTick)
        {
            if (record == null || pawn == null || pawn.ownership == null)
            {
                return false;
            }
            Room room = pawn.ownership.OwnedRoom;
            RoomRoleDef role = (room != null) ? room.Role : null;
            string roleDefName = (role != null) ? role.defName : null;
            if (string.IsNullOrEmpty(roleDefName))
            {
                // 无归属房间角色：保留上次快照（不覆盖、不误清）。
                return false;
            }
            if (record.Residence == null)
            {
                record.Residence = new ResidenceSnapshot();
            }
            if (record.Residence.RoomRoleDefName == roleDefName
                && record.Residence.LastSeenTick == gameTick)
            {
                return false;
            }
            // 房间中心坐标：用房间 extents 中心近似（Room.ExtentsClose.CenterCell）。
            IntVec3 center = default(IntVec3);
            int mapIndex = -1;
            if (room.CellCount > 0)
            {
                center = room.ExtentsClose.CenterCell;
                if (pawn.Map != null)
                {
                    mapIndex = pawn.Map.Index;
                }
            }
            bool changed = record.Residence.RoomRoleDefName != roleDefName;
            record.Residence.RecordSeen(roleDefName, gameTick, mapIndex, center);
            return changed;
        }
    }
}
