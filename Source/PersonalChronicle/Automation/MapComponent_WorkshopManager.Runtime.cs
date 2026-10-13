using System.Collections.Generic;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Automation
{
    public partial class MapComponent_WorkshopManager
    {
        // 运行态只重建，不入存档；每 60 tick 与排班对账，避免工作热路径扫描全地图。
        private readonly Dictionary<Pawn, HashSet<string>> runtimeWorkTypes = new Dictionary<Pawn, HashSet<string>>();
        private readonly Dictionary<Pawn, Zone_Workshop> runtimeEnrolledZone = new Dictionary<Pawn, Zone_Workshop>();
        private readonly Dictionary<Zone_Workshop, Dictionary<Pawn, string>> runtimePosts = new Dictionary<Zone_Workshop, Dictionary<Pawn, string>>();
        private int runtimeRosterTick = -1;
        private int runtimePostsTick = -1;

        private static string AssignmentOf(Pawn pawn) => pawn?.timetable?.CurrentAssignment?.defName;

        internal bool CanExecuteDuty(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && pawn.Map == map && !pawn.Dead && !pawn.Downed
                && !pawn.Drafted && !pawn.InMentalState
                && WorkshopDutyRuntimeRules.CanWork(DutyZoneFor(pawn) != null, AssignmentOf(pawn));
        }

        private void AddRuntimeMember(Pawn pawn, Zone_Workshop zone)
        {
            if (pawn == null || zone == null || pawn.Map != map) return;
            if (!runtimeWorkTypes.TryGetValue(pawn, out var names))
                runtimeWorkTypes[pawn] = names = new HashSet<string>();
            names.Add(OffShiftDutyDefName);
            var type = zone.WorkshopType;
            if (type == null) return;
            foreach (WorkshopTaskType task in new[] { WorkshopTaskType.Production, WorkshopTaskType.Maintenance,
                WorkshopTaskType.QualityCheck, WorkshopTaskType.Standby })
                foreach (string name in type.WorkTypeDefNamesOf(task)) names.Add(name);
            foreach (string name in type.DeviceWorkTypeDefNames()) names.Add(name);
        }

        private void EnsureRuntimeRoster()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (runtimeRosterTick >= 0 && now >= runtimeRosterTick
                && now - runtimeRosterTick < WorkshopDutyRuntimeRules.BoundaryCheckTicks) return;
            runtimeRosterTick = now;
            runtimeWorkTypes.Clear();
            foreach (var slot in slots)
                if (slot != null && slot.EconomicContractId == 0) AddRuntimeMember(slot.AssignedPawn, slot.TargetZone);
            foreach (var shift in shifts)
            {
                if (shift == null) continue;
                var pool = shift.EffectivePool;
                if (pool != null) foreach (var pawn in pool) AddRuntimeMember(pawn, shift.TargetZone);
                // 班组轮换可能不使用 shift.Pool：显式纳入已指派该班的班组成员。
                var crews = shift.TargetZone?.Crews;
                if (crews != null) foreach (var crew in crews)
                    if (crew?.AssignedShifts != null && crew.Members != null && crew.AssignedShifts.Contains(shift))
                        foreach (var pawn in crew.Members) AddRuntimeMember(pawn, shift.TargetZone);
            }
            foreach (var rotation in rotations)
                if (rotation?.EffectivePool != null)
                    foreach (var pawn in rotation.EffectivePool) AddRuntimeMember(pawn, rotation.TargetZone);
        }

        private static bool ProtectedService(string name) => name == "Doctor" || name == "Firefighter"
            || name == "Patient" || name == "PatientBedRest"
            || name == "PC_MealBreak";

        private bool IsRuntimeManaged(Pawn pawn, WorkTypeDef type)
        {
            if (pawn == null || type == null || ProtectedService(type.defName)) return false;
            EnsureRuntimeRoster();
            return runtimeWorkTypes.TryGetValue(pawn, out var names) && names.Contains(type.defName);
        }

        /// <summary>合同闸门的只读授权契约：非本 Pawn 托管工种不能借 CanRun 的默认放行获得权限。</summary>
        internal bool AuthorizesScheduledWork(Pawn pawn, WorkTypeDef type)
            => IsRuntimeManaged(pawn, type) && CanExecuteDuty(pawn) && CanRunScheduledWork(pawn, type);

        internal bool CanRunScheduledWork(Pawn pawn, WorkTypeDef type)
        {
            if (pawn == null || type == null || ProtectedService(type.defName)) return true;
            EnsureRuntimeRoster();
            // 管制餐段/休息约束在编人员的所有普通工作，不只是本车间映射的工种。
            // 否则被中断的科研/搬运会绕过映射守卫，立即又开工，错过饭点。
            if (runtimeWorkTypes.ContainsKey(pawn)
                && !WorkshopDutyRuntimeRules.CanWork(true, AssignmentOf(pawn))) return false;
            if (!IsRuntimeManaged(pawn, type)) return true;
            if (!CanExecuteDuty(pawn)) return false;
            if (type.defName == OffShiftDutyDefName) return true;
            var zone = DutyZoneFor(pawn);
            var shopType = zone.WorkshopType;
            var task = RuntimeTaskOf(pawn, zone);
            if (shopType == null || (!shopType.WorkTypeDefNamesOf(task).Contains(type.defName)
                && !(task == WorkshopTaskType.Production && shopType.DeviceWorkTypeDefNames().Contains(type.defName)))) return false;
            return PostAllows(pawn, zone, type);
        }

        private Dictionary<Pawn, string> RuntimePostsOf(Zone_Workshop zone)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (runtimePostsTick < 0 || now < runtimePostsTick || now - runtimePostsTick >= 1200)
            {
                runtimePostsTick = now;
                runtimePosts.Clear();
            }
            if (!runtimePosts.TryGetValue(zone, out var assigned))
            {
                var roster = new List<Pawn>();
                var seen = new HashSet<Pawn>();
                WorkshopPostAssignmentService.CollectRosterMembers(zone, roster, seen);
                runtimePosts[zone] = assigned = WorkshopPostAssignmentService.AssignOf(roster);
            }
            return assigned;
        }

        private bool PostAllows(Pawn pawn, Zone_Workshop zone, WorkTypeDef type)
        {
            if (zone == null || type == null) return false;
            var assigned = RuntimePostsOf(zone);
            string postName = WorkshopPostAssignmentService.ResolvePostDefNameOf(pawn, zone, assigned);
            // 未编入岗位表的固定班/轮岗沿用班次工种；手动岗位与自动岗位使用同一权威解析器。
            return string.IsNullOrEmpty(postName)
                || WorkshopPostAssignmentService.IsHolderOfWorkType(pawn, type.defName, zone, assigned);
        }

        private WorkshopTaskType RuntimeTaskOf(Pawn pawn, Zone_Workshop zone)
        {
            var post = WorkshopPostCatalog.PostOf(WorkshopPostAssignmentService.ResolvePostDefNameOf(
                pawn, zone, RuntimePostsOf(zone)));
            return post != null && System.Enum.TryParse(post.dutyTaskTypeKey, out WorkshopTaskType task)
                ? task : TaskTypeOf(pawn);
        }

        internal bool AllowBillTarget(Pawn pawn, WorkGiver_DoBill giver, Thing target, bool forced)
        {
            if (forced || !IsRuntimeManaged(pawn, giver?.def?.workType)) return true;
            if (!CanRunScheduledWork(pawn, giver.def.workType)) return false;
            var zone = DutyZoneFor(pawn);
            return target != null && target.Spawned && target.Map == map
                && WorkshopZoneUtility.WorkshopAt(target.Position, map) == zone;
        }

        private Job TryGetWorkshopBillJob(Pawn pawn, Zone_Workshop zone)
        {
            if (pawn.workSettings == null || RuntimeTaskOf(pawn, zone) != WorkshopTaskType.Production) return null;
            var devices = DevicesCached(zone);
            // 原版排序、禁用工种、身体能力、配方技能、工单暂停、材料、预约全部保留。
            foreach (var workGiver in pawn.workSettings.WorkGiversInOrderNormal)
            {
                var giver = workGiver as WorkGiver_DoBill;
                if (giver == null || !IsRuntimeManaged(pawn, giver.def.workType)
                    || !CanRunScheduledWork(pawn, giver.def.workType)
                    || pawn.WorkTagIsDisabled(giver.def.workTags) || pawn.WorkTypeIsDisabled(giver.def.workType)
                    || giver.MissingRequiredCapacity(pawn) != null || giver.ShouldSkip(pawn, false)) continue;
                Job best = WorkshopBillSelection.NearestExecutable<Thing, Job>(devices,
                    device => device.Position.DistanceToSquared(pawn.Position), device =>
                {
                    if (device == null || !device.Spawned || device.IsForbidden(pawn)
                        || !giver.ThingIsUsableBillGiver(device)
                        || !AllowBillTarget(pawn, giver, device, false)
                        || !pawn.CanReach(device, giver.PathEndMode, pawn.NormalMaxDanger())) return null;
                    return giver.JobOnThing(pawn, device, false);
                }, candidate => JobMaker.ReturnToPool(candidate));
                if (best != null)
                {
                    best.workGiverDef = giver.def;
                    return best;
                }
            }
            return null;
        }

        private bool ShouldWaitForWorkshopBill(Pawn pawn, Zone_Workshop zone)
        {
            var post = WorkshopPostCatalog.PostOf(WorkshopPostAssignmentService.ResolvePostDefNameOf(
                pawn, zone, RuntimePostsOf(zone)));
            // 非生产岗位交回原版对应工作，不能用待命挡住排在其后的搬运/清洁/科研等服务。
            return post == null || post.dutyTaskTypeKey == "Production";
        }

        private bool IsDutyWait(Job job) => job?.def?.defName == DutyJobDefName;

        internal void NotifyAutomaticWorkshopJobStarted(Pawn pawn)
        {
            var cur = pawn?.CurJob;
            if (cur == null || cur.playerForced || !CanExecuteDuty(pawn)) return;
            if (IsDutyWait(cur) || IsRuntimeManaged(pawn, cur.workGiverDef?.workType))
                NotifyDutyStarted(pawn, DutyZoneFor(pawn));
        }

        private void UpdateWorkshopDutyRuntime()
        {
            EnsureRuntimeRoster();
            var removed = new List<Pawn>();
            foreach (var pawn in runtimeEnrolledZone.Keys)
                if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.Map != map) removed.Add(pawn);
            foreach (var pawn in removed) runtimeEnrolledZone.Remove(pawn);
            foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
            {
                var zone = CanExecuteDuty(pawn) ? DutyZoneFor(pawn) : null;
                var cur = pawn.CurJob;
                var curWorkType = cur?.workGiverDef?.workType;
                bool rosterBreak = runtimeWorkTypes.ContainsKey(pawn)
                    && !WorkshopDutyRuntimeRules.CanWork(true, AssignmentOf(pawn));
                bool managed = IsDutyWait(cur) || IsRuntimeManaged(pawn, curWorkType)
                    || ((runtimeEnrolledZone.ContainsKey(pawn) || rosterBreak) && curWorkType != null
                        && !ProtectedService(curWorkType.defName));
                if (zone == null)
                {
                    runtimeEnrolledZone.Remove(pawn);
                    NotifyDutyEnded(pawn);
                    // 只停止自动车间岗位，玩家强制任务、征召、精神状态及紧急服务交还原版。
                    bool emergency = pawn.Drafted || pawn.InMentalState || pawn.Downed
                        || (cur?.workGiverDef?.emergency ?? false);
                    if (WorkshopDutyRuntimeRules.ShouldInterrupt(managed, cur?.playerForced ?? false,
                        emergency, false, AssignmentOf(pawn)))
                    {
                        // M9 §5（CHG-20261009-ECON-M9-S5）：让路类中断如实上报，由经济层按 §4
                        // 定格为 ProtectedAction 并**零记录**（让路不计服务失败）。
                        ReportEconomicWorkInterruption(pawn,
                            PersonalChronicle.Domain.PawnEconomy.Learning.FailureClassRules.ReasonProtectedTimetable);
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    }
                    continue;
                }
                if (!runtimeEnrolledZone.TryGetValue(pawn, out var previous) || previous != zone)
                {
                    runtimeEnrolledZone[pawn] = zone;
                    CollectRuntimeWorkTypes(pawn, zone);
                    EnableWorkTypes(pawn);
                    WorkshopPostLandingService.Land(map, zone, new List<Pawn> { pawn }, RuntimePostsOf(zone));
                }
                // 换岗/改车间后，旧工单不能继续占用设备或归账到新车间。
                bool wrongPost = curWorkType != null && !CanRunScheduledWork(pawn, curWorkType);
                var billGiver = cur?.workGiverDef?.Worker as WorkGiver_DoBill;
                bool wrongWorkshop = billGiver != null && cur.targetA.HasThing
                    && !AllowBillTarget(pawn, billGiver, cur.targetA.Thing, false);
                if (managed && cur != null && !cur.playerForced && !(cur.workGiverDef?.emergency ?? false)
                    && (wrongPost || wrongWorkshop))
                {
                    // M9 §5（CHG-20261009-ECON-M9-S5）：票据失效类中断（§4 StaleVersion）——
                    // 经济层认定后**恰好写一条**失败并安排有界复核；本层只报告事实。
                    ReportEconomicWorkInterruption(pawn,
                        PersonalChronicle.Domain.PawnEconomy.Learning.FailureClassRules.ReasonReservationLost);
                    NotifyDutyEnded(pawn);
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    continue;
                }
                // 真实原版工作同样登记考勤；吃饭、睡眠、娱乐与其他非岗位任务不算在岗。
                if (managed && !(cur?.playerForced ?? false)) NotifyDutyStarted(pawn, zone);
                else NotifyDutyEnded(pawn);
            }
        }

        private IntVec3 GetAvailableDutySpot(Pawn pawn, Zone_Workshop zone)
        {
            var devices = DevicesCached(zone);
            foreach (var cell in zone.Cells)
            {
                if (!cell.InBounds(map) || !cell.Standable(map)
                    || (cell.GetFirstPawn(map) != null && cell.GetFirstPawn(map) != pawn)
                    || !pawn.CanReserveAndReach(cell, PathEndMode.OnCell, pawn.NormalMaxDanger())) continue;
                bool blocksOperator = false;
                foreach (var device in devices)
                    if (device != null && device.def.hasInteractionCell && device.InteractionCell == cell)
                    { blocksOperator = true; break; }
                if (!blocksOperator) return cell;
            }
            return IntVec3.Invalid;
        }

        private void CollectRuntimeWorkTypes(Pawn pawn, Zone_Workshop zone)
        {
            workSyncTypes.Clear();
            var type = zone.WorkshopType;
            if (type == null) return;
            var task = RuntimeTaskOf(pawn, zone);
            foreach (var name in type.WorkTypeDefNamesOf(task))
            {
                var mapped = ResolveWorkType(name);
                if (mapped != null && !workSyncTypes.Contains(mapped)) workSyncTypes.Add(mapped);
            }
            if (task != WorkshopTaskType.Production) return;
            foreach (var name in type.DeviceWorkTypeDefNames())
            {
                var mapped = ResolveWorkType(name);
                if (mapped != null && !workSyncTypes.Contains(mapped)) workSyncTypes.Add(mapped);
            }
        }
    }
}
