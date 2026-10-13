using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PersonalChronicle.Application.Effects;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Automation
{
    public partial class MapComponent_WorkshopManager : MapComponent
    {
        private void EnsureDutyPlan()
        {
            // 2026-10-02 审计修复（P1-A）：删除旧的「距上次重建满 60 tick 就强制重建」条款 ——
            // 跨小时已有 dutyPlanHour == hour 覆盖，该条款只会让稳定态每游戏秒重建一次
            //（在职 pawn 的 FailOn 每 tick 走到这里），把 ReconcileDutyPlan 的优化完全抵消。
            // 计划重建现在只由三类事件驱动：① 排班/名册/轮岗变更（MarkDutyPlanDirty）；
            // ② 跨小时；③ ReconcileDutyPlan 观测到计划内成员失去资格（含 20 间隔兜底重建）。
            int hour = GenLocalDate.HourOfDay(map);
            if (!dutyPlanDirty && dutyPlan != null && dutyPlanHour == hour)
            {
                return;
            }
            RebuildDutyPlan(hour);
        }

        /// <summary>
        /// 低频对账（2026-10-02 · PERF-001/PERF-003 修复）：只在<b>可观测到计划已不自洽</b>时标脏。
        ///
        /// <para>旧实现在每个检查间隔（1 游戏秒）无条件 <c>MarkDutyPlanDirty()</c>，于是只要有任何
        /// 查询者（排班页绘制、WorkGiver），计划就每秒全量重建一次 —— 稳定缓存被系统性破坏
        /// （UI 打开时表现为周期性掉帧，「点开排班页卡一下」的主要来源之一）。</para>
        ///
        /// <para>现在的标脏条件（全部 O(计划规模)，计划通常 &lt; 20 人）：</para>
        /// <list type="bullet">
        /// <item>计划为空/已标脏/跨小时（时段命中集合会变）；</item>
        /// <item>计划内某人失去上岗资格（倒地/死亡/精神崩溃/离图/敌对）—— 必须立刻让出名额；</item>
        /// <item>计划指向的车间已死。</item>
        /// </list>
        ///
        /// <para>「外部恢复」（倒地者起身、远征归队、新殖民者加入）无法由本函数观测到，
        /// 由每 <see cref="DutyPlanFallbackIntervals"/> 个间隔（20 秒）一次的兜底重建覆盖；
        /// 单次重建成本为微秒级，玩家不可感知，而稳态重建次数相对旧实现下降 95% 以上。</para>
        /// </summary>
        private void ReconcileDutyPlan()
        {
            int hour = GenLocalDate.HourOfDay(map);
            if (dutyPlan == null || dutyPlanDirty || dutyPlanHour != hour)
            {
                MarkDutyPlanDirty();
                return;
            }
            foreach (KeyValuePair<Pawn, Zone_Workshop> pair in dutyPlan)
            {
                Pawn pawn = pair.Key;
                if (pawn == null || pawn.Destroyed || !IsDutyEligible(pawn))
                {
                    MarkDutyPlanDirty();
                    return;
                }
            }
            foreach (KeyValuePair<Pawn, Zone_Workshop> pair in dutyPlan)
            {
                if (!WorkshopZoneUtility.IsAlive(pair.Value))
                {
                    MarkDutyPlanDirty();
                    return;
                }
            }
            dutyPlanFallbackQuota++;
            if (dutyPlanFallbackQuota >= DutyPlanFallbackIntervals)
            {
                dutyPlanFallbackQuota = 0;
                MarkDutyPlanDirty();
            }
            SelfHealGhostDuty();
            ReplayTimetablesOnDayChange();
        }

        /// <summary>上次重放原版作息表时的游戏天数（跨天才重放，避免高频覆盖玩家手排）。</summary>
        private int timetableReplayDay = -1;

        /// <summary>
        /// <summary>
        /// 跨天时重放原版作息表。
        ///
        /// <para><b>为什么仍需每日重放</b>：<see cref="SyncVanillaTimetables"/> 是
        /// <b>事件驱动</b>（仅排班/名册变更时调用），而策略 RotateDaily 下作息表
        /// <b>按当日轮值</b>推导 ⇒ 倒班表一跨天就换班，不同步重放会导致
        /// "作息表停在昨天的班次上"：该上夜班的人被写成 Sleep（值勤 Job 排不出来）、
        /// 该休的人被写成 Work（有觉不睡）。</para>
        ///
        /// <para><b>默认策略 FixedPerShift 下重放的是"修复"而非"轮换"</b>：
        /// 作息不再按天变化，但<b>旧存档已被写坏的作息表仍需每日重写一遍才能纠正</b>
        /// （用户实机确认的成因 A 的历史残留）。这正是本方法保留的价值。</para>
        ///
        /// <para><b>限流</b>：只在 <c>GenDate.DaysPassed</c> 变化时跑一次（每天最多 1 次），
        /// 不进 tick 热路径；写入仍受 <c>timetable != null</c> 守卫。</para>
        /// </summary>
        private void ReplayTimetablesOnDayChange()
        {
            // CHG-20261004-WSSETTINGS：跨天重放可关闭（默认开）。它保留"修历史残留"的兜底，
            // 但会覆写在编成员的手排作息 ⇒ 想完全掌控手排的玩家可关掉。
            ChronicleSettings replaySettings = PersonalChronicle.PersonalChronicleMod.Settings;
            if (replaySettings != null && !replaySettings.ReplayTimetablesOnDayChange)
            {
                return;
            }
            int today = GenDate.DaysPassed;
            if (today == timetableReplayDay)
            {
                return;
            }
            // ⚠ 2026-10-03（自审修复）：**首次进入也必须重放**。
            //   旧实现首日 set-and-return ⇒ 每个存档的第一次对账什么都不做，
            //   而 `SyncVanillaTimetables` 又是事件驱动（玩家不点排班页就不跑）
            //   ⇒ 旧版并集写坏的作息表**永远不会被修复**（用户实机第三次反馈"仍未解决"的直接原因）。
            //   作用域由 multiShiftCrewsOnly 限定（只动被指派 ≥2 班次的班组），
            //   而"并集写坏"恰好也只发生在这些班组 ⇒ 修复范围与受损范围**精确重合**，不扩大。
            timetableReplayDay = today;
            List<Zone_Workshop> zones = AllWorkshopZones();
            for (int i = 0; i < zones.Count; i++)
            {
                // ⚠ 2026-10-03（**推翻上一版范围决策**）：改为重放**全部**在编班组
                //   （multiShiftCrewsOnly = false），不再只限「被指派 ≥2 班次」的班组。
                //
                //   上一版只修多班次班组，理由是"并集写坏只发生在那里"。但该前提**未经证实**：
                //   若玩家是"每班组 1 个班次"（人员页显示每人单一班次即是此形态），
                //   并集 ≡ 单班 ⇒ 旧代码写的是正确值 ⇒ **真正吃掉睡眠的可能是别的路径**
                //   （① 固定槽位 / ③ 轮岗，或玩家手排本身）。
                //   在无法从代码单侧断定受损子集时，把修复范围对齐到**甲口径本身**
                //   （"PC 管理在编班组员的作息"）才是完整且自洽的：
                //   写单班次班组的作息不是"额外破坏"，而是**本功能的既有承诺**
                //   （值勤需要 Work 小时、睡眠需要 Sleep 小时）。
                //   代价（玩家手排在编人员被覆写）已由甲口径明确承担：移出班组即可收回控制权。
                SyncVanillaTimetables(zones[i], false);
            }
        }

        /// <summary>
        /// 幽灵在岗自愈（2026-10-03 · 用户实机「到点了还卡在值勤、不下班睡觉」）。
        ///
        /// <para><b>要解决的静默故障链</b>：<c>NotifyDutyEnded</c> 只在值勤 Job 的
        /// <c>AddFinishAction</c> 里注销在岗（见 <c>JobDriver_WorkshopDuty.OnDutyFinished</c>）。
        /// 而 Job 可能<b>不经过</b>该收尾就被换掉 —— 被玩家手动打断、被其他 Job 抢占、
        /// 读档时 <c>curToilIndex</c> 续跑异常等。这时：</para>
        /// <list type="bullet">
        /// <item><c>activeWorkers</c> 留下**幽灵条目** ⇒ <see cref="ActiveWorkerCount"/> 虚高、
        /// 设备页「在岗」显示幽灵、<c>bonusCache</c> 常驻不释放；</item>
        /// <item>更糟：<see cref="TryGetDutyJob"/> 的 <c>activeWorkers.ContainsKey(pawn)</c>
        /// 早退会让该 pawn <b>再也拿不到值勤 Job</b>（真正该上岗时反而站着不动）。</item>
        /// </list>
        ///
        /// <para><b>判据（两道都要满足才注销，宁可漏不可错杀）</b>：① pawn 当前
        /// <b>没有</b>正在跑的值勤 Job；② pawn <b>不在</b>当前小时的 <c>dutyPlan</c> 里。
        /// 两者同时成立才说明"登记表与现实脱节"，此时强制走一次正常收工路径
        /// （含考勤与工时结算），保证账目不丢。</para>
        ///
        /// <para><b>放在低频对账里而非 tick</b>：本方法遍历 <c>activeWorkers</c>（通常 &lt; 20 人），
        /// 每 <see cref="DutyPlanFallbackIntervals"/> 个检查间隔才跑一次，不构成热路径
        /// （PERF-001）；修好后即为幂等空转。</para>
        /// </summary>
        private void SelfHealGhostDuty()
        {
            if (activeWorkers.Count == 0 || dutyPlan == null)
            {
                return;
            }
            List<Pawn> ghosts = null;
            foreach (KeyValuePair<Pawn, Zone_Workshop> pair in activeWorkers)
            {
                Pawn pawn = pair.Key;
                if (pawn == null)
                {
                    continue;
                }
                Job cur = pawn.CurJob;
                bool onDutyJob = cur != null && cur.def != null && cur.def.defName == DutyJobDefName;
                if (onDutyJob || dutyPlan.ContainsKey(pawn))
                {
                    continue;   // 正在值勤 或 计划内 ⇒ 登记表自洽
                }
                if (ghosts == null)
                {
                    ghosts = new List<Pawn>(2);
                }
                ghosts.Add(pawn);
            }
            if (ghosts == null)
            {
                return;
            }
            for (int i = 0; i < ghosts.Count; i++)
            {
                NotifyDutyEnded(ghosts[i]);
            }
        }

        /// <summary>
        /// 重建上岗名单：固定班（点名）→ 班组（名额，按 <see cref="WorkshopShiftPlanner"/> 分配）→ 轮岗。
        /// 先占者胜：被更高优先级来源占用的 pawn 不会被后续来源再次安排。
        /// </summary>
        private void RebuildDutyPlan(int hour)
        {
            dutyPlanHour = hour;
            dutyPlanDirty = false;
            if (dutyPlan == null)
            {
                dutyPlan = new Dictionary<Pawn, Zone_Workshop>();
            }
            else
            {
                dutyPlan.Clear();
            }
            shiftFill.Clear();
            dutyShiftOf.Clear();   // CHG-20261003-WORKSHOP-SPLIT：班次归属随计划重建
            dutyTaskType.Clear();

            // ① 固定班：同一 pawn 多个槽位时取优先级最高者（同分取先定义者）。
            for (int i = 0; i < slots.Count; i++)
            {
                WorkshopSlot slot = slots[i];
                if (!SlotIsActive(slot, hour))
                {
                    continue;
                }
                Pawn pawn = slot.AssignedPawn;
                if (dutyPlan.ContainsKey(pawn))
                {
                    continue;
                }
                WorkshopSlot best = BestSlotFor(pawn, hour);
                if (best != null)
                {
                    dutyPlan[pawn] = best.TargetZone;
                    dutyTaskType[pawn] = best.TaskType;
                }
            }

            // ② 班组：按优先级降序分配名额；已被固定班占用的 pawn 直接跳过。
            //    企业化倒班（2026-09-11）：启用班组轮转的班次，当日候选池 = 当日排到该班次序位的班组成员。
            Dictionary<Zone_Workshop, int> rotationSlotCounts = CollectRotationSlotCounts();
            // 2026-10-02：多班次车间禁止「池空 ⇒ 回落整份车间名单」——否则每个班次都覆盖全员，
            // 任何小时都有人命中 ⇒ 值勤 Job 永不收工（等于没有下班）。见 CollectZoneShiftCounts。
            Dictionary<Zone_Workshop, int> zoneShiftCounts = CollectZoneShiftCounts();
            int dayIndex = GenDate.DaysPassed;
            List<WorkshopShift> active = CollectActiveShifts(hour);
            for (int s = 0; s < active.Count; s++)
            {
                WorkshopShift shift = active[s];
                int shiftCountOfZone;
                if (shift.TargetZone == null
                    || !zoneShiftCounts.TryGetValue(shift.TargetZone, out shiftCountOfZone))
                {
                    shiftCountOfZone = 1;   // 未统计到（理论不会发生）⇒ 维持原回落行为
                }
                List<Pawn> pool = ResolveShiftPool(shift, dayIndex, rotationSlotCounts,
                    shiftCountOfZone <= 1);
                if (pool == null || pool.Count == 0)
                {
                    continue;
                }
                var eligible = new List<bool>(pool.Count);
                var scores = new List<int>(pool.Count);
                for (int i = 0; i < pool.Count; i++)
                {
                    Pawn candidate = pool[i];
                    bool ok = IsDutyEligible(candidate) && shift.MeetsSkill(candidate);
                    eligible.Add(ok);
                    scores.Add(ok ? DutyScoreFor(shift, candidate) : 0);
                }
                List<int> picked = WorkshopShiftPlanner.SelectDutyIndexes(eligible, scores, shift.RequiredCount);
                int filled = 0;
                for (int i = 0; i < picked.Count; i++)
                {
                    Pawn pawn = pool[picked[i]];
                    if (pawn == null || dutyPlan.ContainsKey(pawn))
                    {
                        continue;
                    }
                    dutyPlan[pawn] = shift.TargetZone;
                    dutyTaskType[pawn] = shift.TaskType;
                    dutyShiftOf[pawn] = shift;   // 收工判定需要班次窗口
                    filled++;
                }
                shiftFill[shift] = filled;
            }

            // ③ 轮岗：只补空缺（固定班/班组已安排的不动）。
            for (int i = 0; i < rotations.Count; i++)
            {
                RotationRule rule = rotations[i];
                if (rule == null || !rule.Matches(rule.Current, hour))
                {
                    continue;
                }
                Pawn current = rule.Current;
                if (current == null || !IsDutyEligible(current) || dutyPlan.ContainsKey(current))
                {
                    continue;
                }
                dutyPlan[current] = rule.TargetZone;
                // 轮岗规则没有任务类型字段（一个岗位一类轮换），按生产计。
                dutyTaskType[current] = WorkshopTaskType.Production;
            }
        }

        /// <summary>
        /// 上岗资格（体检 P2-⑦）：必须是<b>本图在编</b>的玩家派系殖民者且当前可工作。
        ///
        /// <para>旧实现只查 <c>Spawned</c> ⇒ 跨图远征/商队成员、被俘者、访客、倒地者、
        /// 精神崩溃者都会被算进"命中人数"并**占住名额**（同池里健康的 pawn 反而落选）。
        /// 资格收窄后，这类 pawn 会在恢复可工作时由下一次计划重建自动回到岗位（最多滞后
        /// 一个检查间隔），不需要额外唤醒逻辑。</para>
        /// </summary>
        private bool IsDutyEligible(Pawn pawn)
        {
            return pawn != null && !pawn.Destroyed && !pawn.Dead
                && pawn.Spawned && pawn.Map == map
                && pawn.Faction == Faction.OfPlayer
                && !pawn.Downed && !pawn.InMentalState;
        }

        private bool SlotIsActive(WorkshopSlot slot, int hour)
        {
            return slot != null && slot.AssignedPawn != null && IsDutyEligible(slot.AssignedPawn)
                && slot.Matches(slot.AssignedPawn, hour);
        }

        private WorkshopSlot BestSlotFor(Pawn pawn, int hour)
        {
            WorkshopSlot best = null;
            for (int i = 0; i < slots.Count; i++)
            {
                WorkshopSlot slot = slots[i];
                if (!SlotIsActive(slot, hour) || slot.AssignedPawn != pawn)
                {
                    continue;
                }
                if (best == null || slot.Priority > best.Priority)
                {
                    best = slot;
                }
            }
            return best;
        }

        /// <summary>
        /// 命中当前时段且指向存活车间的班组，按 <see cref="WorkshopShift.Priority"/> 降序
        /// （<b>同分保持定义顺序</b>）。稳定排序由 <see cref="StableOrder"/> 提供 ——
        /// 旧实现用 <c>List.Sort</c>（不稳定）：注释写的"同分保持定义顺序"并不成立，
        /// 同优先级班组争抢同一 pawn 的人选会随列表增删漂移（体检 P2-⑤）。
        /// </summary>
        private List<WorkshopShift> CollectActiveShifts(int hour)
        {
            var list = new List<WorkshopShift>();
            for (int i = 0; i < shifts.Count; i++)
            {
                WorkshopShift shift = shifts[i];
                if (shift != null && WorkshopZoneUtility.IsAlive(shift.TargetZone) && shift.InTime(hour))
                {
                    list.Add(shift);
                }
            }
            StableOrder.SortByDescending(list, s => s.Priority);
            return list;
        }

        /// <summary>本车间的班次序位数量（参与班组轮转的班次，取序位最大值 +1；无则 0）。</summary>
        private Dictionary<Zone_Workshop, int> CollectRotationSlotCounts()
        {
            var counts = new Dictionary<Zone_Workshop, int>();
            for (int i = 0; i < shifts.Count; i++)
            {
                WorkshopShift shift = shifts[i];
                if (shift == null || !shift.UseCrewCycle || shift.TargetZone == null || shift.ShiftSlotIndex < 0)
                {
                    continue;
                }
                int current;
                counts.TryGetValue(shift.TargetZone, out current);
                if (shift.ShiftSlotIndex + 1 > current)
                {
                    counts[shift.TargetZone] = shift.ShiftSlotIndex + 1;
                }
            }
            return counts;
        }

        /// <summary>
        /// 本车间**班次总数**（2026-10-02）：用于判定「候选池空 ⇒ 回落到整份车间名单」是否安全。
        ///
        /// <para><b>为什么必须收窄</b>（用户实机反馈「下班后无法离开车间」）：多班次车间若沿用该回落，
        /// 每个班次的候选池都会是全体成员，而名额默认 <c>0</c> = 不限人数 ⇒ 任何小时都有人命中排班
        /// ⇒ <c>IsScheduledForDuty</c> 恒真 ⇒ 值勤 Job 的 <c>FailOn</c> 永不命中 ⇒
        /// <b>24 小时连续值勤</b>，班次之间根本不可能发生交接。</para>
        ///
        /// <para>收窄后的语义：多班次车间必须显式说明「谁上哪个班」—— 在班组设置里勾选该班次
        /// （<c>crew.AssignedShifts</c>），或使用旧档遗留的自定义候选池；两者都没有 ⇒ 该班次空岗
        /// （排班页会提示）。单班次车间维持原回落行为（老档与"一个班全员上"的直觉不变）。</para>
        /// </summary>
        private Dictionary<Zone_Workshop, int> CollectZoneShiftCounts()
        {
            var counts = new Dictionary<Zone_Workshop, int>();
            List<WorkshopShift> all = Shifts;
            for (int i = 0; i < all.Count; i++)
            {
                WorkshopShift shift = all[i];
                if (shift == null || shift.TargetZone == null)
                {
                    continue;
                }
                int old;
                counts.TryGetValue(shift.TargetZone, out old);
                counts[shift.TargetZone] = old + 1;
            }
            return counts;
        }

        /// <summary>
        /// 倒班解析（企业化排班核心）：启用班组轮转的班次，当日候选池 =
        /// 「当日被排到 <see cref="WorkshopShift.ShiftSlotIndex"/> 班次序位」的班组编制成员；
        /// 该班组当天休息 ⇒ 返回 null（本班次当天停班）。
        ///
        /// <para>未启用轮转 / 未指定序位 / 未编组 ⇒ 回落既有行为（自定义池 → 车间名单），
        /// 但「池空 → 整份车间名单」这一步**仅限单班次车间**（见 <see cref="CollectZoneShiftCounts"/>）。</para>
        /// </summary>
        private static List<Pawn> ResolveShiftPool(WorkshopShift shift, int dayIndex,
            Dictionary<Zone_Workshop, int> rotationSlotCounts, bool allowZoneWideFallback)
        {
            if (shift == null)
            {
                return null;
            }
            Zone_Workshop zone = shift.TargetZone;
            List<Pawn> basePool;
            int shiftCount;
            if (!shift.UseCrewCycle || zone == null || shift.ShiftSlotIndex < 0
                || rotationSlotCounts == null
                || !rotationSlotCounts.TryGetValue(zone, out shiftCount)
                || shiftCount <= 0)
            {
                basePool = FallbackPool(shift, zone, allowZoneWideFallback);
            }
            else
            {
                List<WorkshopCrew> crews = zone.Crews;
                int crewCount = crews.Count;
                if (crewCount <= 0)
                {
                    basePool = FallbackPool(shift, zone, allowZoneWideFallback);
                }
                else
                {
                    int phase = dayIndex - zone.CycleAnchorDay;
                    // 考虑调休/请假/顶班例外（Phase 2）：与倒班表 UI 同源，保证"看到的即派的"。
                    int[] kinds = zone.ExceptionKindsFor(dayIndex, crewCount);
                    int crewIndex = WorkshopCrewCycle.CrewOfShiftSlot(
                        phase, shift.ShiftSlotIndex, crewCount, shiftCount, kinds);
                    basePool = (crewIndex >= 0 && crewIndex < crewCount)
                        ? crews[crewIndex].Members
                        : null;
                }
            }
            // 显式指派（班组设置里勾选的班次）：把"被指派负责本班次的班组"成员并入候选池，
            // 与倒班表轮转叠加（二者都满足者优先）。不勾选则只走倒班表（basePool 原逻辑不变）。
            if (zone != null && zone.Crews != null)
            {
                var extra = new List<Pawn>();
                for (int c = 0; c < zone.Crews.Count; c++)
                {
                    WorkshopCrew crew = zone.Crews[c];
                    if (crew == null || crew.AssignedShifts == null || !crew.AssignedShifts.Contains(shift))
                    {
                        continue;
                    }
                    List<Pawn> members = crew.Members;
                    for (int m = 0; m < members.Count; m++)
                    {
                        Pawn pawn = members[m];
                        if (pawn != null && !pawn.Destroyed && !extra.Contains(pawn))
                        {
                            extra.Add(pawn);
                        }
                    }
                }
                if (extra.Count > 0)
                {
                    if (basePool == null)
                    {
                        basePool = new List<Pawn>();
                    }
                    for (int i = 0; i < extra.Count; i++)
                    {
                        if (!basePool.Contains(extra[i]))
                        {
                            basePool.Add(extra[i]);
                        }
                    }
                }
            }
            // ⚠ 深度审计 X1（2026-10-04）：**必须返回副本**。
            //
            // basePool 可能是 crews[i].Members / zone.Members / shift.Pool 这类
            // **持久化集合的活引用**（见本方法 :462-464 与 FallbackPool:527），
            // 而本方法末尾（:489-502）还会向它 Add「显式指派班次」的成员
            // ⇒ 等于"查询函数顺手改写了存档名册"：落盘后表现为
            // **「殖民者莫名被添加到其他班组」**（玩家 2026-10-04 报过三次、我此前找不到自动编组入口
            // 就是因为它根本不是一个入口，而是这里的副作用），
            // 且 DedupeCrewMemberships 只能治标 —— 下一次排班又追加一次。
            //
            // 复制后返回：唯一调用点（RebuildDutyPlan:259 的只读遍历）与 public ResolvePoolFor
            // 都不再可能写回名册。返回 null 语义保持不变。
            return basePool != null ? new List<Pawn>(basePool) : null;
        }

        /// <summary>
        /// 候选池回落（2026-10-02 收窄）：自定义池（玩家显式指派；UI 已撤下但旧档可能有值）
        /// <b>始终</b>生效；「池空 ⇒ 回落整份车间名单」<b>仅</b>在
        /// <paramref name="allowZoneWideFallback"/>（= 该车间只有这一个班次）时允许。
        ///
        /// <para>多班次车间回落到全员，会让每个班次都覆盖所有人 ⇒ 等于没有下班
        /// （详见 <see cref="CollectZoneShiftCounts"/>）。此时该班次改由「班组设置里勾选本班次的班组」
        /// 供给人员（见 <see cref="ResolveShiftPool"/> 末尾的显式指派对并集），两者都没有 ⇒ 空岗。</para>
        /// </summary>
        private static List<Pawn> FallbackPool(WorkshopShift shift, Zone_Workshop zone,
            bool allowZoneWideFallback)
        {
            if (shift != null && shift.Pool != null && shift.Pool.Count > 0)
            {
                return shift.Pool;
            }
            if (!allowZoneWideFallback)
            {
                return null;
            }
            return zone != null ? zone.Members : null;
        }

        /// <summary>
        /// 班次当前实际可用的候选池（2026-10-02 审计 P1-B）：与派工<b>同源</b>的解析入口 ——
        /// 多班次车间不回落到全员（见 <see cref="FallbackPool"/>），未指派 ⇒ null（空岗）。
        ///
        /// <para><b>为什么必须有它</b>：名额/覆盖率展示此前读 <c>WorkshopShift.EffectivePool</c>
        /// （池空无条件回落全员），与派工口径不一致 ⇒ "实际空岗 0 人、界面显示满员"。
        /// 展示侧（甘特图 / 覆盖条 / 运营面板）一律改走本方法。</para>
        ///
        /// <para>⚠ 每次调用会构建两个临时字典 —— 仅供低频展示路径（Refresh / 延迟求值的悬停）
        /// 使用，<b>不得</b>进绘制热路径；确需高频时应在调用方缓存。</para>
        /// </summary>
        public List<Pawn> ResolvePoolFor(WorkshopShift shift)
        {
            if (shift == null)
            {
                return null;
            }
            int zoneShiftCount;
            if (shift.TargetZone == null
                || !CollectZoneShiftCounts().TryGetValue(shift.TargetZone, out zoneShiftCount))
            {
                zoneShiftCount = 1;
            }
            return ResolveShiftPool(shift, GenDate.DaysPassed,
                CollectRotationSlotCounts(), zoneShiftCount <= 1);
        }

        /// <summary>
        /// 班次当前「预计可上岗人数」（2026-10-02 审计 P1-B）：<c>RequiredCount &gt; 0</c> 用它；
        /// 否则按 <see cref="ResolvePoolFor"/> 解析出的池人数（空岗 ⇒ 0）。
        /// </summary>
        public int ShiftStaffingOf(WorkshopShift shift)
        {
            if (shift == null)
            {
                return 0;
            }
            if (shift.RequiredCount > 0)
            {
                return shift.RequiredCount;
            }
            List<Pawn> pool = ResolvePoolFor(shift);
            return pool != null ? pool.Count : 0;
        }

        /// <summary>
        /// 该班次当前是否**有人可用**（排班页可发现性提示用，2026-10-02）。
        ///
        /// <para>返回 <c>false</c> 的三种情形：① 多班次车间且没有班组勾选本班次、也没有自定义候选池
        /// （该班次空岗）；② 班组轮转命中「当日休息」；③ 车间人员名单为空。</para>
        /// </summary>
        public bool HasResolvablePool(WorkshopShift shift)
        {
            List<Pawn> pool = ResolvePoolFor(shift);
            return pool != null && pool.Count > 0;
        }

        /// <summary>
        /// 名额分配评分：与轮岗 SkillBased **共用** <see cref="WorkshopDutyScoring"/>
        /// （门槛技能 → 车间类型主技能 → Crafting；-1 = 技能不可用）。
        /// 只用于"同池内谁优先上岗"，不影响任何加成数值。
        /// </summary>
        private static int DutyScoreFor(WorkshopShift shift, Pawn pawn)
        {
            return WorkshopDutyScoring.ScoreOf(
                shift != null ? shift.TargetZone : null,
                shift != null ? shift.RequiredSkill : null,
                pawn);
        }

        /// <summary>
        /// 该班组本次计划实际分配到的上岗人数（O(1)；班组行"命中 M 人"用）。
        /// 体检 P2-④：旧 UI 用的是"该车间计划总数"，所有班组行显示同一个数字且每帧全表扫。
        /// </summary>
        public int FilledCountOf(WorkshopShift shift)
        {
            if (shift == null)
            {
                return 0;
            }
            EnsureDutyPlan();
            return shiftFill.TryGetValue(shift, out int filled) ? filled : 0;
        }

        /// <summary>
        /// 绘制路径专用的只读版本（2026-10-02 · PERF-003）：与 <see cref="FilledCountOf"/> 的唯一差别是
        /// <b>不主动 Ensure</b> —— 计划已有结果时是纯字典读取，绘制帧永远不会触发计划重建。
        ///
        /// <para>旧实现让 chip 每帧调用 <c>FilledCountOf</c>（<c>EnsureDutyPlan</c>），于是"每帧绘制"
        /// 与"每秒失效"叠加成周期性重建；拆开后重建只发生在逻辑路径（WorkGiver / 排班变更 / 对账）。</para>
        ///
        /// <para>首帧尚未有任何计划（玩家从未派工）时补建一次，避免 chip 显示空白计数。</para>
        /// </summary>
        public int FilledCountOfCached(WorkshopShift shift)
        {
            if (shift == null)
            {
                return 0;
            }
            if (dutyPlan == null)
            {
                EnsureDutyPlan();
            }
            return shiftFill.TryGetValue(shift, out int filled) ? filled : 0;
        }

        /// <summary>
        /// 该 pawn 本次计划的任务类型（工时上报按任务分类消费 <see cref="WorkshopTaskType"/>；
        /// 无计划/轮岗 → <see cref="WorkshopTaskType.Production"/>）。O(1)。
        /// </summary>
        public WorkshopTaskType TaskTypeOf(Pawn pawn)
        {
            if (pawn == null)
            {
                return WorkshopTaskType.Production;
            }
            EnsureDutyPlan();
            return dutyTaskType.TryGetValue(pawn, out WorkshopTaskType type)
                ? type
                : WorkshopTaskType.Production;
        }

        /// <summary>
        /// WorkGiver 的唯一入口：命中排班且尚未在岗时返回岗位 Job，否则 null。
        /// </summary>
        public Job TryGetDutyJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Drafted || pawn.InMentalState)
            {
                return null;
            }
            // CHG-20261004-IDLE：作息表当前格是「摸鱼」时段 ⇒ 不下发值勤 Job。
            // 该时段的语义是"本班次没有你的工位，去娱乐"（allowJoy=true，由原版
            // JobGiver_GetJoy 兜底）；若这里照发值勤 Job，他会被立刻叫回车间站着，
            // 摸鱼判定会被值勤 Job 覆盖掉，等于没改。见
            // WorkshopDutyCoordinator.ApplyWorkHoursToCrew 的容量切分。
            if (!CanExecuteDuty(pawn))
            {
                return null;
            }
            Zone_Workshop zone = DutyZoneFor(pawn);
            if (zone == null || zone.Map == null)
            {
                return null;
            }
            Job production = TryGetWorkshopBillJob(pawn, zone);
            if (production != null) return production;
            if (!ShouldWaitForWorkshopBill(pawn, zone)) return null;
            IntVec3 spot = GetAvailableDutySpot(pawn, zone);
            if (!spot.IsValid)
            {
                return null;
            }
            if (!pawn.CanReach(spot, PathEndMode.OnCell, Danger.Deadly))
            {
                return null;
            }
            if (dutyJobDef == null)
            {
                dutyJobDef = DefDatabase<JobDef>.GetNamed(DutyJobDefName, false);
                if (dutyJobDef == null)
                {
                    return null;
                }
            }
            return JobMaker.MakeJob(dutyJobDef, spot);
        }

        /// <summary>
        /// CHG-20261004-IDLE：该 pawn 此刻是否处于「摸鱼」时段（作息表当前格 = <c>PC_Idle</c>）。
        ///
        /// <para>这是<b>唯一真相</b>：时段是持久化的、玩家可在【管制】表里直观看与改，
        /// 也是原版 <c>JobGiver_GetJoy</c> / <c>GetRest</c> 判断"能否娱乐/睡"的依据。
        /// UI 的「摸鱼中…」标注与值勤 Job 的下发都读它，不另设状态位（避免两处口径漂移）。</para>
        /// </summary>
        public static bool IsOnIdleAssignment(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }
            Pawn_TimetableTracker tracker = pawn.timetable;
            if (tracker == null)
            {
                return false;
            }
            TimeAssignmentDef current = tracker.CurrentAssignment;
            return current != null
                && current.defName == WorkshopDutyCoordinator.IdleTimeAssignmentDefName;
        }

        /// <summary>
        /// CHG-20261004-EMPLOYEE-ACCESS：该 pawn 是否因「车间员工身份」而被允许从事一个
        /// <b>被原版禁用</b>的工作类型（背景故事 DisallowedWorkTags / 特质 / 健康）。
        ///
        /// <para><b>玩家诉求</b>（2026-10-04）：「成为员工后，我需要忽略【无法从事】」——
        /// 被排进车间的人若因背景故事禁用艺术/烹饪，<c>EnableWorkTypes</c> 的
        /// <c>WorkTypeIsDisabled</c> 守卫会跳过该类型 ⇒ 岗位映射永远勾不上 ⇒ 员工不干活。</para>
        ///
        /// <para><b>范围刻意极窄</b>（本判定经 <c>Pawn.WorkTypeIsDisabled</c> 的 62 个消费点被放大，
        /// 见 <c>Capture/Patch_Pawn_WorkTypeIsDisabled.cs</c>），三者缺一即交还原版：</para>
        /// <list type="number">
        /// <item>该 pawn 是某车间的<b>在编成员</b>（人员页名单 <c>zone.Members</c>）；</item>
        /// <item>该车间<b>已指定类型</b>（未指定 ⇒ 无映射 ⇒ 不构成授权）；</item>
        /// <item>该工作类型确实是该车间类型的<b>映射类型</b>（4 个岗位语义的并集）。</item>
        /// </list>
        /// <para>⇒ 不改写"背景故事限制"的全局语义，只承认<b>玩家主动把人排进某类车间</b>
        /// 这一次显式授权；把人移出车间即立即恢复原版限制（无需额外开关）。</para>
        /// </summary>
        public static bool EmployeeAccessAllows(Pawn pawn, WorkTypeDef workType)
        {
            if (pawn == null || workType == null || pawn.Map == null || pawn.Dead || pawn.Destroyed)
            {
                return false;
            }
            MapComponent_WorkshopManager mgr = Get(pawn.Map);
            if (mgr == null)
            {
                return false;
            }
            List<Zone_Workshop> zones = mgr.AllWorkshopZones();
            for (int z = 0; z < zones.Count; z++)
            {
                Zone_Workshop zone = zones[z];
                if (zone == null || zone.WorkshopType == null)
                {
                    continue;
                }
                List<Pawn> members = zone.Members;
                if (members == null || !members.Contains(pawn))
                {
                    continue;
                }
                if (WorkshopTypeMapsWorkType(zone.WorkshopType, workType))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// CHG-20261004-EMPLOYEE-ACCESS：该工作类型是否被<b>背景故事或特质</b>禁用
        /// ——即「<b>来历性</b>」限制，正是玩家要求车间身份可以覆盖的那一类。
        ///
        /// <para><b>为什么必须做正向确认，而不是"排除其他来源"</b>（2026-10-04 修正）：
        /// 原版"无法从事"共有 <b>9 类来源</b>（IL 实证，<c>Pawn.GetReasonsForDisabledWorkType</c>）：
        /// 背景故事 <c>WorkDisabledByBackstory</c> / 特质 <c>WorkDisabledByTrait</c> /
        /// 帝国爵位 <c>WorkDisabledByRoyalTitle</c> / 意识形态角色 <c>WorkDisabledRole</c> /
        /// 变异体 / 任务 <c>WorkDisabledByQuest</c> / 奴隶 <c>WorkDisabledSlave</c> /
        /// 健康 <c>WorkDisabledHealth</c> / 年龄 <c>WorkDisabledAge</c>。
        /// 而 <c>GetDisabledWorkTypes</c> 返回的是<b>并集</b>（无法区分来源）
        /// ⇒ 若按并集放行，儿童（年龄）、伤员（健康）、奴隶、任务受限者都会被车间身份放行 ——
        /// 那超出了玩家诉求且有害。正向只认背景/特质，其余 7 类天然不会被放行。</para>
        ///
        /// <para>口径与 IL 一致：背景遍历 <c>story.AllBackstories</c> 的 <c>DisabledWorkTypes</c>；
        /// 特质遍历 <c>story.traits.allTraits</c>（跳过 <c>Suppressed</c> 的特质）。</para>
        /// </summary>
        public static bool DisabledByStoryOrTrait(Pawn pawn, WorkTypeDef workType)
        {
            if (pawn == null || workType == null)
            {
                return false;
            }
            Pawn_StoryTracker story = pawn.story;
            if (story == null)
            {
                return false;
            }
            List<BackstoryDef> backstories = story.AllBackstories;
            if (backstories != null)
            {
                for (int i = 0; i < backstories.Count; i++)
                {
                    BackstoryDef backstory = backstories[i];
                    if (backstory == null)
                    {
                        continue;
                    }
                    List<WorkTypeDef> storyDisabled = backstory.DisabledWorkTypes;
                    if (storyDisabled != null && storyDisabled.Contains(workType))
                    {
                        return true;
                    }
                }
            }
            TraitSet traits = story.traits;
            if (traits == null || traits.allTraits == null)
            {
                return false;
            }
            List<Trait> all = traits.allTraits;
            for (int i = 0; i < all.Count; i++)
            {
                Trait trait = all[i];
                if (trait.Suppressed)
                {
                    continue;
                }
                List<WorkTypeDef> traitDisabled = trait.GetDisabledWorkTypes();
                if (traitDisabled != null && traitDisabled.Contains(workType))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>该车间类型的任一岗位语义是否映射到此工作类型（四类并集）。</summary>
        private static bool WorkshopTypeMapsWorkType(WorkshopTypeDef typeDef, WorkTypeDef workType)
        {
            if (typeDef == null || workType == null)
            {
                return false;
            }
            return NamesContainWorkType(typeDef.WorkTypeDefNamesOf(WorkshopTaskType.Production), workType)
                || NamesContainWorkType(typeDef.WorkTypeDefNamesOf(WorkshopTaskType.Maintenance), workType)
                || NamesContainWorkType(typeDef.WorkTypeDefNamesOf(WorkshopTaskType.QualityCheck), workType)
                || NamesContainWorkType(typeDef.WorkTypeDefNamesOf(WorkshopTaskType.Standby), workType);
        }

        private static bool NamesContainWorkType(List<string> names, WorkTypeDef workType)
        {
            if (names == null)
            {
                return false;
            }
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i] == workType.defName)
                {
                    return true;
                }
            }
            return false;
        }

        // ---- 会话级"上次按什么设备数对账过"快照（CHG-20261004-IDLE-FIX）------------------
        //
        // ⚠ 为什么放在 MapComponent 而不是管理面板（2026-10-04 审计 P1-1）：
        //   `Dialog_WorkshopManage` **每次开窗都 new 一个 WorkshopManageView**，View 实例字段
        //   初始化即归零 ⇒ 若把快照放 View，"设备数 != -1" 恒成立 ⇒ **每次打开面板都会重涂
        //   全班组作息表**（覆盖玩家手排）。MapComponent 每张地图一个、跨开窗存活 ⇒ 语义正确。
        //
        // 按 zone.ID 记账（多车间互不干扰）。**不进存档**：读档/重进游戏后首次对账一次，
        // 与既有「开窗即对账」口径一致；这里只消除"同一次游戏会话内反复重涂"。

        private readonly Dictionary<int, int> idleSplitCapacityByZone = new Dictionary<int, int>();
        private readonly Dictionary<int, int> autoTypeCapacityByZone = new Dictionary<int, int>();

        /// <summary>
        /// 设备容量切分是否需要重涂（比较并推进运行态快照）。
        /// <returns><c>true</c> = 本次会话内该车间的设备数与上次不同（或首次）⇒ 调用方应重涂。</returns>
        /// </summary>
        public bool ConsumeIdleSplitCapacityChange(Zone_Workshop zone, int capacity)
        {
            if (zone == null || capacity < 0)
            {
                return false;
            }
            int id = zone.ID;
            if (idleSplitCapacityByZone.TryGetValue(id, out int last) && last == capacity)
            {
                return false;
            }
            idleSplitCapacityByZone[id] = capacity;
            return true;
        }

        /// <summary>
        /// 自动车间类型判定是否需要再试一次（设备数变化即重试一次，首次必然尝试）。
        /// <returns><c>true</c> = 应当尝试判定（调用方仍需自查 <c>zone.WorkshopType == null</c>）。</returns>
        /// </summary>
        public bool ConsumeAutoTypeCapacityChange(Zone_Workshop zone, int capacity)
        {
            if (zone == null || capacity < 0)
            {
                return false;
            }
            int id = zone.ID;
            if (autoTypeCapacityByZone.TryGetValue(id, out int last) && last == capacity)
            {
                return false;
            }
            autoTypeCapacityByZone[id] = capacity;
            return true;
        }

        // ---- 在岗登记（JobDriver 调用）-------------------------------------------

        public void NotifyDutyStarted(Pawn pawn, Zone_Workshop zone)
        {
            if (pawn == null || zone == null)
            {
                return;
            }
            if (activeWorkers.TryGetValue(pawn, out Zone_Workshop existing))
            {
                if (existing == zone) return;
                NotifyDutyEnded(pawn);
            }
            activeWorkers[pawn] = zone;
            // 考勤起点（Phase 2）：收工时按此算值勤时长与加班。
            dutyStartTicks[pawn] = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            // 上岗时解析一次加成并缓存：此后工作速度/生产加成都直接读缓存。
            bonusCache[pawn] = WorkshopBonusResolver.Resolve(zone.WorkshopType, pawn);
        }

        /// <summary>
        /// 收工：注销在岗，并把本次值勤时长记入**车间考勤台账**（Phase 2）。
        /// 加班 = 超出当班班次时长的部分（现实里的超班时值勤）。
        /// </summary>
        public void NotifyDutyEnded(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }
            Zone_Workshop zone;
            if (activeWorkers.TryGetValue(pawn, out zone) && zone != null && zone.Map != null)
            {
                int start;
                if (dutyStartTicks.TryGetValue(pawn, out start))
                {
                    int now = Find.TickManager != null ? Find.TickManager.TicksGame : start;
                    long worked = now - start;
                    long shiftTicks = ShiftTicksFor(zone, GenLocalDate.HourOfDay(zone.Map));
                    long overtime = worked > shiftTicks ? worked - shiftTicks : 0L;
                    zone.Attendance.AddDuty(GenDate.DaysPassed, worked, overtime);
                    // 按人绩效台账（2026-09-25）：同一结算点拆一层"到人"，供班组/人员出勤率。
                    zone.Performance.AddDuty(pawn, GenDate.DaysPassed, worked);
                }
            }
            dutyStartTicks.Remove(pawn);
            activeWorkers.Remove(pawn);
            bonusCache.Remove(pawn);
        }

        /// <summary>
        /// 产出登记（<c>Patch_GenRecipe</c> 调用）：成品出炉时归账到它的<b>工作台</b>
        /// （设备台账：每日 / 每周）与<b>车间绩效</b>（生产率分子）。
        ///
        /// <para><b>两条口径刻意不同</b>（2026-10-02 用户实机反馈后修正）：</para>
        /// <list type="bullet">
        /// <item><b>设备台账</b>：只看"东西是否在这台设备上被造出来" —— <b>不要求</b>工人处于本系统值勤。
        /// 旧实现要求 <c>activeWorkers</c> 命中，于是"玩家直接派殖民者去做原版工单"这一最常见的玩法
        /// 产出的东西一件都不计数（设备页产出列恒 <c>0 / 0</c>）。设备产出是物理事实。</item>
        /// <item><b>车间绩效</b>：仍按「在岗」口径（<c>activeWorkers</c>）—— 该台账衡量的是
        /// "本系统排班时段内的劳动产出"，把非排班时段/非排班人员的零散产出计入会污染生产率读数。</item>
        /// </list>
        ///
        /// <para>归账范围（<see cref="WorkshopDeviceLedger.IsTracked"/>）：只记已被车间设备扫描追踪的工作台，
        /// 避免车间之外的工作台污染设备台账（条目由 <c>AccumulateDeviceRuntime</c> 逐轮 <c>Touch</c> 保证）。</para>
        /// </summary>
        public void NotifyProductMade(Pawn worker, Thing product)
        {
            if (worker == null)
            {
                return;
            }
            Job cur = worker.CurJob;
            Thing bench = cur != null ? cur.GetTarget(TargetIndex.A).Thing : null;
            if (bench != null && DeviceLedger.IsTracked(bench))
            {
                DeviceLedger.AddProduced(bench, GenDate.DaysPassed, 1);
                // CHG-20261005-DEVCOLS：「累计产值」与件数**同一次归账**写入（同一 bench、同一时刻）
                // —— 两处各判一次"是不是车间设备"就会出现"件数有、产值无"的自相矛盾读数。
                // 空产物（非人制造 / 已被回收）⇒ 只记件数；MarketValue 含品质与材料修正，
                // 与玩家在物品栏看到的同一个数（UI-009 可自证）。
                if (product != null)
                {
                    DeviceLedger.AddProducedValue(bench, product.MarketValue);
                }
            }
            Zone_Workshop zone;
            if (activeWorkers.TryGetValue(worker, out zone) && zone != null)
            {
                zone.Performance.AddProduced(worker, GenDate.DaysPassed, 1);
            }
        }

        /// <summary>
        /// 把班次窗口写入<b>原版时间管制表</b>（2026-09-25 用户需求：设置完成班次/班组后
        /// 原版排班表自动对应）。
        ///
        /// <para><b>CHG-20261003-WORKSHOP-SPLIT：实现已下沉到
        /// <see cref="WorkshopDutyCoordinator"/>（值勤模块 = 唯一 timetable 写入口）。</b>
        /// 本方法保留为<b>薄转发</b>，以免改动既有调用点（MATRIX-003 纪律不变：
        /// UI 仍只发命令，写入点仍唯一）。</para>
        ///
        /// <para><b>口径由车间作息策略决定</b>（见 <see cref="WorkshopRestPolicyDef"/>）：
        /// 默认 FixedPerShift（岗位永久绑定一个班次，<b>不按天翻转</b>）；
        /// NaturalRest 完全不写；RotateDaily 为旧行为（会翻转睡眠时段）。</para>
        /// </summary>
        public void SyncVanillaTimetables(Zone_Workshop zone)
        {
            DutyCoordinator.SyncVanillaTimetables(zone, false);
        }

        /// <summary>
        /// 跨天重放入口（<b>只处理"被指派 ≥2 个班次"的班组</b>）。
        ///
        /// <para>单班次班组跨天无变化，让它被重放属于「无收益的破坏性写入」
        ///（会连玩家<b>手排</b>的时段一起踩掉），违反 EXC-2026-013 红线。</para>
        /// </summary>
        private void SyncVanillaTimetables(Zone_Workshop zone, bool multiShiftCrewsOnly)
        {
            DutyCoordinator.SyncVanillaTimetables(zone, multiShiftCrewsOnly);
        }

        // ---- 主动收工判定（CHG-20261003-WORKSHOP-SPLIT · 修成因 B）----------------
        // JobDriver 不再靠 FailOn 做"到点收工"（FailOn 不可撤销，无法表达"未达最小时长则
        // 暂不收工"），改为主动求值本区段。判定本体在 Domain.WorkshopDutyRules（纯函数、
        // 可离线单测）；本区段只做「读引擎状态 → 调 Domain → 返回 bool」的桥接。

        /// <summary>
        /// 该 pawn 现在<b>是否应当收工</b>（值勤模块判定的引擎侧入口；JobDriver 每秒调一次）。
        /// </summary>
        /// <param name="pawn">在岗 pawn。</param>
        /// <param name="dutyStartTick">本次上岗的 tick（JobDriver 传入）。</param>
        /// <returns><c>true</c> = 应 EndJobWith(Succeeded)；<c>false</c> = 继续值勤。</returns>
        public bool ShouldEndDutyNow(Pawn pawn, int dutyStartTick)
        {
            if (pawn == null || !pawn.Spawned || map == null)
            {
                return true;   // 离图/销毁：立即收工（硬边界）
            }
            Zone_Workshop zone;
            if (!activeWorkers.TryGetValue(pawn, out zone) || zone == null)
            {
                return true;   // 不在岗（幽灵/已被换班）：交回硬边界处理
            }

            // 用餐、睡眠、休息和下班优先于最小连续值勤时长。
            return !CanExecuteDuty(pawn) || DutyZoneFor(pawn) != zone;
        }

        /// <summary>该车间生效的最小连续值勤时长（读 Def；缺省见 Domain 常量）。</summary>
        private static int DutyMinContinuousHours(Zone_Workshop zone)
        {
            WorkshopRestPolicyDef def = WorkshopDutyCoordinator.PolicyOf(zone);
            return def != null ? def.MinContinuousHours
                : Domain.WorkshopShiftPolicy.DefaultMinContinuousHours;
        }

        /// <summary>把 tick 换算成"上岗当时的游戏内小时"（跨天按 24 取模）。</summary>
        private int HourOfTick(int tick)
        {
            if (Find.TickManager == null)
            {
                return GenLocalDate.HourOfDay(map);
            }
            long day = tick / GenDate.TicksPerDay;
            long hourOfDay = ((tick - day * GenDate.TicksPerDay) / GenDate.TicksPerHour) % 24;
            return (int)hourOfDay;
        }

        /// <summary>
        /// 该 pawn 当前值勤所属的班次（来自调度计划；<b>无则 null</b>）。
        /// <para>跨小时时计划会重建，本方法取<b>最新</b>计划；判定本体
        /// （WorkshopDutyRules.ShouldEndDuty）已把"仍在窗口内"作为继续条件，
        /// 故计划重建不会导致误收工。</para>
        /// </summary>
        private WorkshopShift DutyShiftOf(Pawn pawn)
        {
            EnsureDutyPlan();
            return dutyShiftOf.TryGetValue(pawn, out WorkshopShift shift) ? shift : null;
        }

        /// <summary>
        /// 该车间当前班次的时长（tick），用于判定加班。
        ///
        /// <para>口径：命中本车间当前小时的班次 ⇒ 该班次窗口小时数 × <c>TicksPerHour</c>；
        /// 未命中（真空档）⇒ 回落 8 小时。</para>
        ///
        /// <para>⚠ CHG-20261005-WSDOCFIX：本方法的 doc 注释此前**漂在文件中部**
        /// （<c>SyncVanillaTimetables</c> 之后），而此处顶着的是 <c>Zone_Workshop.PruneScheduleHistory</c>
        /// 的注释（复制粘贴错位，两者毫无关系）—— 已各自归位。</para>
        /// </summary>
        private long ShiftTicksFor(Zone_Workshop zone, int hour)
        {
            for (int i = 0; i < shifts.Count; i++)
            {
                WorkshopShift shift = shifts[i];
                if (shift == null || shift.TargetZone != zone)
                {
                    continue;
                }
                if (WorkshopHours.InRange(hour, shift.StartHour, shift.EndHour))
                {
                    int hours = WorkshopShiftPatterns.HoursOf(
                        new ShiftWindow(shift.StartHour, shift.EndHour));
                    return (long)hours * GenDate.TicksPerHour;
                }
            }
            return 8L * GenDate.TicksPerHour;
        }

    }
}
