using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Meal;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 值勤协调器（Automation 层 · 三模块拆分的<b>唯一引擎写入口</b>）。
    ///
    /// <para><b>模块边界（GOV-004）</b>：本类是<b>唯一</b>允许写原版
    /// <c>Pawn.timetable</c> 的地方，也是唯一负责"下发 / 收工值勤 Job"编排的地方。
    /// <b>所有判定逻辑都在 Domain</b>（<see cref="WorkshopDutyRules"/> /
    /// <see cref="WorkshopShiftPolicy"/> / <see cref="WorkshopRosterRules"/>）——
    /// 本类只做「投影 → 调 Domain → 翻译回引擎对象」的三步编排。</para>
    ///
    /// <para><b>为什么单独成类</b>：拆分前，作息表写入、值勤 Job 生命周期、考勤结算
    /// 三件事散在 <c>MapComponent_WorkshopManager.Schedule.cs</c>（980 行）与
    /// <c>JobDriver_WorkshopDuty.cs</c> 两个文件里互相回调。
    /// 收拢后，"谁写了 timetable / 谁收的工"只有一个答案，便于审计（EXC-2026-013
    /// 「绝不碰玩家自己设的档位」这条红线的执行点也收敛到一处）。</para>
    ///
    /// <para><b>实例生命周期</b>：由 <see cref="MapComponent_WorkshopManager"/> 持有
    /// （<c>readonly</c>，一张地图一个），<b>不持久化</b>（可由排班数据完全重建）。</para>
    /// </summary>
    internal sealed class WorkshopDutyCoordinator
    {
        private readonly MapComponent_WorkshopManager manager;

        public WorkshopDutyCoordinator(MapComponent_WorkshopManager manager)
        {
            this.manager = manager;
        }

        /// <summary>
        /// 取该地图的作息策略档位。
        ///
        /// <para><b>取法（三级降级，绝不抛异常）</b>：车间存档里记的 Def 名
        /// ⇒ 全局缺省 Def ⇒ <see cref="WorkshopRestPolicyDef.DefaultPolicyDefName"/>
        /// ⇒ 硬兜底 <see cref="WorkshopRestPolicyKind.FixedPerShift"/>。
        /// 玩家删了 Def / 旧档没写过 ⇒ 都落到缺省（= 新建车间行为，零行为突变）。</para>
        /// </summary>
        public static WorkshopRestPolicyDef PolicyOf(Zone_Workshop zone)
        {
            string defName = zone != null ? zone.RestPolicyDefName : null;
            if (!string.IsNullOrEmpty(defName))
            {
                WorkshopRestPolicyDef byZone = DefDatabase<WorkshopRestPolicyDef>.GetNamedSilentFail(defName);
                if (byZone != null)
                {
                    return byZone;
                }
            }
            // CHG-20261004-WSSETTINGS：Mod 设置级的全局默认（新增一层，位于"车间自身选择"之后、
            // "Def 缺省"之前）。玩家诉求是一次配置新车间策略，而不是逐车间在排班页重复点。
            // ⚠ 默认值 = Def 缺省（PC_WorkshopRestPolicy_Fixed）⇒ 未改动设置时与旧行为**逐字一致**。
            ChronicleSettings globalSettings = PersonalChronicle.PersonalChronicleMod.Settings;
            string globalDefName = globalSettings != null ? globalSettings.WorkshopDefaultRestPolicy : null;
            if (!string.IsNullOrEmpty(globalDefName))
            {
                WorkshopRestPolicyDef bySetting =
                    DefDatabase<WorkshopRestPolicyDef>.GetNamedSilentFail(globalDefName);
                if (bySetting != null)
                {
                    return bySetting;
                }
                // 设置里存了一个已不存在的 Def 名（第三方移除 / 改名）⇒ 继续降级，不抛异常。
            }
            WorkshopRestPolicyDef fallback = DefDatabase<WorkshopRestPolicyDef>.GetNamedSilentFail(
                WorkshopRestPolicyDef.DefaultPolicyDefName);
            return fallback;
        }

        /// <summary>该车间生效的策略语义（已规范化，越界回落默认档）。</summary>
        public static WorkshopRestPolicyKind KindOf(Zone_Workshop zone)
        {
            WorkshopRestPolicyDef def = PolicyOf(zone);
            return def != null
                ? WorkshopShiftPolicy.Normalize(def.policy)
                : WorkshopRestPolicyKind.FixedPerShift;
        }
        /// <summary>
        /// 把车间班组推导写进<b>原版作息表</b>（<b>本类是唯一写入点</b> —— MATRIX-003）。
        ///
        /// <para><b>按策略分派（修成因 A 的落点）</b>：</para>
        /// <list type="bullet">
        /// <item><b>FixedPerShift（默认）</b>：每个班组写入<b>其绑定班次</b>的固定时段，
        /// <b>不按天变化</b> ⇒ 无 12 小时翻转。绑定由
        /// <see cref="WorkshopRosterRules.ResolveFixedShiftId"/> 决定（只指派 1 班时零新增存储）。</item>
        /// <item><b>NaturalRest</b>：<b>完全不写</b>（明确承诺不碰玩家手排）。</item>
        /// <item><b>RotateDaily</b>：旧行为（按当日轮值），保留作对照。</item>
        /// </list>
        ///
        /// <para><b>「无班次计划 ⇒ 不动玩家手排」的不变式</b>在三条分支上都保留
        /// （<c>hasPlan == false</c> ⇒ <c>continue</c>）—— 这是 EXC-2026-013 的执行点。</para>
        /// </summary>
        /// <param name="zone">目标车间。</param>
        /// <param name="multiShiftCrewsOnly">
        /// 只处理"被指派 &ge;2 个班次"的班组（跨天重放专用；单班次班组跨天无变化，
        /// 让它被重放属于「无收益的破坏性写入」）。
        /// </param>
        public void SyncVanillaTimetables(Zone_Workshop zone, bool multiShiftCrewsOnly)
        {
            if (zone == null || zone.Crews == null)
            {
                return;
            }
            // ★ CHG-20261005-EVERWORK：**在任何策略分支之前**先补齐全体成员的 workSettings 初始化。
            //   为什么要放在这里而不是等 ApplyWorkHoursToCrew：
            //   ① NaturalRest 策略下本方法会提前 return（不写作息表），但成员仍然要能工作
            //      ⇒ 初始化不能挂在"是否写表"的下游；
            //   ② multiShiftCrewsOnly 会跳过单班次班组 ⇒ 挂在写入循环里会漏掉那些人；
            //   ③ 本方法是所有作息表同步路径（开面板/改配置/跨天重放）的**汇聚点**
            //      ⇒ 挂在这里 = 一次覆盖全部入口（GOV-005 单一实现）。
            //   幂等：原版方法对已初始化者是空操作。
            manager?.EnsureWorkSettingsInitialized(zone);
            WorkshopRestPolicyKind kind = KindOf(zone);
            // NaturalRest：不写任何作息表（策略显式承诺）。
            if (!WorkshopShiftPolicy.WritesTimetable(kind))
            {
                return;
            }

            List<WorkshopShift> cycleShifts = CollectCycleShifts(zone);
            int dayIndex = GenDate.DaysPassed;
            List<WorkshopCrew> crews = zone.Crews;
            // 2026-10-04 审计 P3：设备容量在循环外<b>只算一次</b>（每次 = 遍历 zone.Cells 全格扫描）。
            // 原实现由 ApplyWorkHoursToCrew 内部按班组各算一次 ⇒ O(班组数) 次全格扫描；
            // 容量是车间级量，与班组无关，故提到循环外（同轮内多班组共享同一结果）。
            int deviceCapacity = DeviceCapacityOf(zone);

            for (int c = 0; c < crews.Count; c++)
            {
                WorkshopCrew crew = crews[c];
                if (crew == null || crew.Members == null)
                {
                    continue;
                }
                if (multiShiftCrewsOnly && CountAssignedShifts(zone, crew) < 2)
                {
                    continue;   // 单班次班组：跨天无变化 ⇒ 不动它的作息表（保护玩家手排）
                }

                bool[] work;
                bool[] mealHours;   // CHG-20261004-SHIFTMEAL：该班组的用餐小时集合
                if (kind == WorkshopRestPolicyKind.FixedPerShift)
                {
                    if (!TryBuildFixedWorkHours(zone, crew, c, cycleShifts, out work))
                    {
                        continue;   // 无班次计划 ⇒ 不动玩家手排
                    }
                    // 固定班：用餐小时 = 该班组各**指派班次**餐段（∩ 各自工作时段）的并集
                    //（CHG-20261005-MEALBYSHIFT：不再回落全局三餐；见 AddMealHoursOfShift）。
                    mealHours = BuildCrewMealHours(crew, null);
                }
                else
                {
                    // RotateDaily：旧口径（当日轮值），保留作对照。
                    WorkshopShift effective = EffectiveShiftOfCrewForDay(zone, crew, c, cycleShifts, dayIndex);
                    if (effective == null && CountAssignedShifts(zone, crew) == 0 && cycleShifts.Count == 0)
                    {
                        continue;   // 真·无计划 ⇒ 不动玩家手排
                    }
                    work = new bool[24];
                    if (effective != null)
                    {
                        WorkshopDutyRules.MarkWorkHours(effective.StartHour, effective.EndHour, work);
                    }
                    // 轮值：优先用**当日实际生效**班次的餐段。
                    mealHours = BuildCrewMealHours(crew, effective);
                }

                ApplyWorkHoursToCrew(deviceCapacity, crew, work, mealHours);
            }
        }

        /// <summary>
        /// 固定绑定策略：算出该班组应写入的 24 位 Work 表。
        /// <returns><c>false</c> = 无班次计划（调用方应保持玩家手排不动）。</returns>
        private static bool TryBuildFixedWorkHours(Zone_Workshop zone, WorkshopCrew crew, int crewIndex,
            List<WorkshopShift> cycleShifts, out bool[] work)
        {
            work = null;
            List<WorkshopShift> assigned = CollectAssignedShifts(zone, crew);
            WorkshopShift bound = null;
            if (assigned.Count > 0)
            {
                // 显式指派 ⇒ 走固定绑定（只指派 1 班时天然固定，零新增存储）。
                bound = ResolveFixedShift(crew, assigned);
            }
            else if (cycleShifts.Count > 0)
            {
                // 未显式指派：沿用旧口径（按班组序位取一个倒班班次），保持既有行为不变。
                bound = cycleShifts[crewIndex % cycleShifts.Count];
            }
            if (bound == null)
            {
                return false;
            }
            work = new bool[24];
            WorkshopDutyRules.MarkWorkHours(bound.StartHour, bound.EndHour, work);
            return true;
        }

        /// <summary>
        /// 固定绑定解析（Domain 判定 + 引擎侧回写持久化）。
        /// <para>回写是<b>必要</b>的：绑定一旦算出就应跨存档保持，否则每次读档重算、
        /// 一致性依赖"ID 升序"这一隐式约定而非显式记录。</para>
        /// </summary>
        private static WorkshopShift ResolveFixedShift(WorkshopCrew crew, List<WorkshopShift> assigned)
        {
            var view = new WorkshopRosterRules.CrewRoster { FixedShiftId = crew.FixedShiftId };
            var ids = new List<int>(assigned.Count);
            for (int i = 0; i < assigned.Count; i++)
            {
                ids.Add(assigned[i].LoadId);
            }
            // GOV-009：只按稳定 ID 升序 ⇒ "取第一个"在多语言/多存档下完全一致。
            ids.Sort();
            int boundId = WorkshopRosterRules.ResolveFixedShiftId(view, ids);
            for (int i = 0; i < assigned.Count; i++)
            {
                if (assigned[i].LoadId == boundId)
                {
                    if (crew.FixedShiftId != boundId)
                    {
                        crew.FixedShiftId = boundId;   // 回写持久化（加性字段）
                    }
                    return assigned[i];
                }
            }
            return assigned.Count > 0 ? assigned[0] : null;
        }

        /// <summary>把 Work 表写进班组全体成员的原版作息表（timetable 写入口）。</summary>
        /// <remarks>
        /// <para><b>CHG-20261004-IDLE 冗余员工兜底</b>（玩家诉求 2026-10-04：「员工数量多于设备
        /// 导致员工原地发呆，并显示在区域上班中，需要【娱乐】进行兜底」）：</para>
        /// <para>本方法按<b>设备容量</b>把班组切成两段，前 N 人（N = 车间设备数）照旧写
        /// <c>TimeAssignmentDefOf.Work</c>（他们拿得到值勤 Job），其余写 <c>PC_Idle</c>
        /// —— 该时段 <c>allowJoy=true</c>，于是原版 <c>JobGiver_GetJoy</c> 自然接管，
        /// 冗余员工在班次内去做娱乐，而不是站着发呆。</para>
        /// <para><b>为什么必须动时段而不只是动 Job</b>：原版 <c>Work</c> 时段是
        /// <c>allowJoy=false</c> + <c>allowRest=false</c>（Core XML 实证）⇒ 身处 Work 时段的
        /// pawn <b>既不能娱乐也不能睡</b>，不管 PC 发不发值勤 Job 都只能站着。
        /// 「能不能娱乐」是<b>时段类型的字段</b>，不是 Job 的属性。</para>
        /// <para><b>容量口径</b>：取 <see cref="WorkshopStats.ScanDevices"/> 的数量，
        /// 与概览页「设备 N」同一入口 ⇒ 玩家在界面看到的数字就是判定依据（可解释、可自证）。</para>
        /// <para><b>性能</b>：本方法只在排班/设备变更等<b>事件驱动</b>路径调用（非 tick、
        /// 非绘制），故这里扫一次设备可接受；不引入每帧开销。</para>
        /// </remarks>
        private static void ApplyWorkHoursToCrew(int capacity, WorkshopCrew crew, bool[] work,
            bool[] mealHours)
        {
            if (crew == null || crew.Members == null || work == null)
            {
                return;
            }
            // CHG-20261004-WSSETTINGS：摸鱼切分可关闭（默认开）。关闭 ⇒ idle 置 null
            // ⇒ 全员按原样写 Work 时段（= 退回摸鱼功能之前的语义）。
            ChronicleSettings idleSettings = PersonalChronicle.PersonalChronicleMod.Settings;
            TimeAssignmentDef idle = (idleSettings == null || idleSettings.IdleSplitEnabled)
                ? ResolveIdleTimeAssignment()
                : null;

            // 只有"真的存在摸鱼时段且确实超编"时才改语义；其余情况逐字保持既有行为。
            // CHG-20261004-MEALASSIGN（玩家诉求 2026-10-04：「对于自动分配管制，
            // 给三餐时间管制进行配置」）：三餐时段也写进管制表。
            //
            // 为什么必须在这里做：本方法就是 PC 唯一的作息表写入口，而它原先只写
            // Work / Sleep / PC_Idle ⇒ 玩家在【管制】表手涂的「用餐」格子会在下一次
            // 排班变更 / 设备增减 / 跨天重放时被**冲掉**（实机现象：三餐配置看不见了）。
            //
            // 两个来源并集（口径与 MapComponent_MealSchedule.DesiredPriorityOf 一致，GOV-005）：
            //   ① 该格**已经是**「用餐」（玩家手涂）⇒ 保留，不覆盖；
            //   ② 落在 mealHours 内 ⇒ 自动涂上。
            //
            // CHG-20261004-SHIFTMEAL（2026-10-04 玩家需求「班次设置表…添加用餐时间段设置」）：
            // mealHours 由 BuildCrewMealHours 按**班次级**算出 —— 该班组的班次配了「班中餐」
            // 就用它（倒班制下白班/夜班餐点本就不同）。
            //
            // ⚠ CHG-20261005-MEALBYSHIFT（2026-10-05 玩家要求「删除全局三餐 mod 设置，
            //   统一口径以班次设置中的三餐时间执行」）—— 本段两处口径收窄：
            //   ① 删除"没配则回落全局三餐"（全局设置已整节删除，餐点唯一来源 = 班次）；
            //   ② mealHours 现在**已由 AddMealHoursOfShift 与各班组自己的工作时段取过交集**
            //      ⇒ 下面这条"只在工作时间生效"的边界依旧成立，且不再需要"用餐优先于睡眠"
            //      来做兜底（见下方判定顺序注释）。
            //
            // 口径边界：只在**工作时间**（work[h] == true）内生效；非工作时间照旧写 Sleep
            // —— 吃饭不挤占睡眠，也让「班次决定作息」这条既有承诺保持可预期。
            TimeAssignmentDef meal = MapComponent_MealSchedule.ResolveMealTimeAssignment();

            int workerIndex = 0;
            for (int m = 0; m < crew.Members.Count; m++)
            {
                Pawn member = crew.Members[m];
                if (member == null || member.Destroyed || member.timetable == null)
                {
                    continue;
                }
                // ★ CHG-20261005-EVERWORK（2026-10-05 玩家实机「两班倒人员不上班」的根因修复）：
                //
                // 原版 JobGiver_Work.GetPriority 的**第一道**闸门是
                //     if (pawn.workSettings == null || !pawn.workSettings.EverWork) return 0f;
                // —— workSettings **从未初始化**的人，即使作息表写着「工作」也**永远拿不到工作 Job**
                //（该闸门逐字记在本工程 Patch_JobGiver_Work_GetPriority.cs 的注释里）。
                //
                // 为什么必须在这里补：PC 另外两条会初始化 workSettings 的路径**覆盖不全**——
                //   · SyncVanillaWorkSettings 只在「排班/名册变更」事件触发；
                //   · SyncWorkStationForZone 只处理 dutyPlan 里「当前在班」的人
                //     （MapComponent_WorkshopManager.WorkEnable.cs:453 的 ContainsKey 过滤）；
                //   · 只有手动点「接管原版工作」对**全体成员**无条件初始化
                //     （MapComponent_WorkshopManager.WorkTakeover.cs:220）
                // ⇒ 玩家实测症状：读档后不改配置、也不点接管时「当班却不动」；
                //   点一次接管就"突然正常"（2026-10-05 审计确认）。
                //
                // 口径：作息表写入 = "安排他上班"的声明 ⇒ 这里有责任保证他的工作设置可用。
                // 本方法是事件驱动路径（非 tick、非绘制），与既有通道同规：
                // workSettings != null 守卫 + 幂等初始化（原版方法自带"已初始化则跳过"）。
                Pawn_WorkSettings memberSettings = member.workSettings;
                if (memberSettings != null)
                {
                    memberSettings.EnableAndInitializeIfNotAlreadyInitialized();
                }
                bool redundant = idle != null && capacity >= 0 && workerIndex >= capacity;
                workerIndex++;
                TimeAssignmentDef onDuty = redundant ? idle : TimeAssignmentDefOf.Work;
                List<TimeAssignmentDef> slots = member.timetable.times;
                for (int h = 0; h < 24; h++)
                {
                    TimeAssignmentDef target;
                    // ⚠ CHG-20261005-MEALPRIO（2026-10-05 玩家实机「在班次中设置了对应的三餐时间，
                    //   重新修改了班组，但最终无法正常执行」的**根因修复**）：
                    //
                    // 原判定顺序把 `!work[h] ⇒ Sleep` 放在**用餐之前** ⇒ **落在非工作时段
                    // （或跨越工作/睡眠边界）的餐点永远不会被涂成「用餐」** —— 玩家在班次里配好的
                    // 三餐被静默吞掉，管制表上看不到用餐格（实机截图实证：白班行只有工作/睡眠两色）。
                    //
                    // 现改为「**用餐最高优先**」：餐点是玩家**显式配置**的（班次字段或全局窗口），
                    // 显式配置必须压过"非工作时段"这条**隐式默认**。
                    //
                    // ⚠ 代价照实说：若把某班次的餐点配到该成员的睡眠时段（如夜班配在正午），
                    //   他会在睡眠时段被要求去吃饭 —— 但那是**玩家自己配的**，属预期行为；
                    //   不加这个优先级，就只剩"餐点必须恰好落在工作时段内才生效"这种不可预测的语义。
                    if (meal != null && IsMealHour(mealHours, slots, h, meal))
                    {
                        target = meal;
                    }
                    else if (!work[h])
                    {
                        target = TimeAssignmentDefOf.Sleep;
                    }
                    else
                    {
                        target = onDuty;
                    }
                    // 幂等写入：值没变就不写。两个好处 ——
                    // ① 免掉原版 SetAssignment 的 Postfix 唤醒（Patch_PawnColumnWorker_Timetable
                    //    在涂到 PC_Meal 时会唤醒一次全图重算，批量重涂不该触发几十次）；
                    // ② 「手涂保留」的判定本来就要读旧值，读一次即可，不额外开销。
                    // 索引口径与原版一致（times 恒为 24 项，原版 SetAssignment 也是直接 set_Item）。
                    if (h >= slots.Count || slots[h] != target)
                    {
                        member.timetable.SetAssignment(h, target);
                    }
                }
            }
        }

        /// <summary>摸鱼时段类型的 defName（<c>Defs/Automation_TimeAssignment.xml</c>）。</summary>
        /// <summary>
        /// CHG-20261004-MEALASSIGN / -SHIFTMEAL：该小时对这名成员是否应涂成「用餐」时段。
        /// 两个来源并集 ——
        /// ① 表里<b>已经是</b>用餐（玩家手涂 ⇒ 保留，不被车间排班冲掉）；
        /// ② 落在 <paramref name="mealHours"/> 内（该班组的班中餐/全局三餐窗口，
        ///    见 <see cref="BuildCrewMealHours"/>）。
        /// </summary>
        private static bool IsMealHour(bool[] mealHours, List<TimeAssignmentDef> slots,
            int hour, TimeAssignmentDef meal)
        {
            if (meal != null && slots != null && hour >= 0 && hour < slots.Count && slots[hour] == meal)
            {
                return true;
            }
            return mealHours != null && hour >= 0 && hour < mealHours.Length && mealHours[hour];
        }

        /// <summary>
        /// CHG-20261004-SHIFTMEAL：算出该班组应涂成「用餐」的小时集合（24 位）。
        ///
        /// <para><b>逐班次</b>合并（语义可解释、无隐式优先级）：对每个相关班次 ——
        /// 配了「班中餐」⇒ 用它的三个**时间点**；没配 ⇒ 该班次不贡献餐点。</para>
        ///
        /// <para>⚠ CHG-20261005-MEALBYSHIFT：原先还有第三条"没配 ⇒ 回落全局三餐窗口"，
        /// 已随全局三餐设置一并删除（餐点唯一来源 = 班次配置）。同批把并集限制在
        /// "各班次自己的工作时段内"，修掉倒班班组白班餐点污染夜班人员的问题
        /// —— 详见 <see cref="AddMealHoursOfShift"/> 的注释。</para>
        ///
        /// <para><paramref name="effective"/> 是 RotateDaily 口径下"当日实际生效"的班次；
        /// FixedPerShift 口径传 null（改由该班组的全部指派班次决定）。</para>
        ///
        /// <para><b>性能</b>：24 位数组 + 至多"班次数×3 段"的区间展开，且只在排班/设备/跨天等
        /// 事件驱动路径调用（非 tick、非绘制）。</para>
        /// </summary>
        private static bool[] BuildCrewMealHours(WorkshopCrew crew, WorkshopShift effective)
        {
            var hours = new bool[24];
            if (crew != null && crew.AssignedShifts != null)
            {
                for (int i = 0; i < crew.AssignedShifts.Count; i++)
                {
                    AddMealHoursOfShift(crew.AssignedShifts[i], hours);
                }
            }
            AddMealHoursOfShift(effective, hours);   // null 安全；与上面重复时幂等
            return hours;
        }

        /// <summary>
        /// 把一个班次的用餐小时并入 <paramref name="hours"/>。
        ///
        /// <para><b>CHG-20261005-MEALBYSHIFT</b>（2026-10-05 玩家要求"删除全局三餐 mod 设置，
        /// 统一口径以班次设置中的三餐时间执行"）—— 本方法两条口径同时收窄：</para>
        ///
        /// <list type="number">
        /// <item><b>不再回落全局</b>：全局三餐设置已整节删除（旧行为是"未配班中餐 ⇒ 用全局窗口"）。
        /// 现在<b>没配 = 该班次无餐点</b>，全工程只剩一处餐点来源（UI-009 可自证）；</item>
        /// <item><b>不对餐点做"工作时段"过滤</b>（CHG-20261005-MEALHOUR-FIX，2026-10-05 玩家实机）：
        /// 曾试过"餐点必须落在该班次工作时段内"以防串台，但跨午夜班次（20:00–08:00）配的
        /// "上班前早餐 19:00"会被静默丢弃 ⇒ 玩家看到"清理管制后自动重涂，早餐没对齐"。
        /// 餐点是玩家**显式意图**，不丢弃。</item>
        /// </list>
        ///
        /// <para>⚠ <b>已知边界（如实登记）</b>：若一个班组<b>同时指派了多个班次</b>（倒班），
        /// 本方法取的是<b>并集</b> ⇒ 该班组成员会在全部班次的餐点格上都被涂「用餐」
        /// （例如白班 7/12/17 与夜班 19/00/06 同时生效）。这是"班组 → 班次"多对多带来的
        /// 语义限制；正确解法是"逐人按其**当日实际班次**取餐点"，需要"人 → 班次"的映射
        /// （当前数据模型没有）。<b>单班次班组不受影响</b>。</para>
        ///
        /// <para>⚠ CHG-20261005-MEALHOUR（同日玩家要求「三餐不要时间段，改为时间选择，
        /// 选择单个时间」）：餐点由"段"改为**单点** ⇒ 逐项从"展开一段"变成"看一个格"。</para>
        /// </summary>
        private static void AddMealHoursOfShift(WorkshopShift shift, bool[] hours)
        {
            if (shift == null || hours == null || !shift.HasShiftMeal)
            {
                return;   // 未配班中餐 ⇒ 该班次不贡献餐点（不再回落全局，见注释 ①）
            }
            MarkMealHour(hours, shift.MealBreakfastHour);
            MarkMealHour(hours, shift.MealLunchHour);
            MarkMealHour(hours, shift.MealDinnerHour);
        }

        /// <summary>
        /// 把**单个**餐点小时并入 <paramref name="hours"/>（CHG-20261005-MEALHOUR）。
        ///
        /// <para><b>为什么不限制在"该班次的工作时段内"</b>（CHG-20261005-MEALHOUR-FIX，
        /// 2026-10-05 玩家实机修正）：上一批给餐点加了"必须落在班次工作时段内"的过滤，
        /// 本意是防倒班班组串台；但玩家立刻踩到边界 ——
        /// <b>跨午夜班次（20:00–08:00）配的"早餐 19:00"被静默丢弃</b>
        /// （玩家心智是"上班前先吃饭"），表现为"清理管制后自动重涂，早餐没对齐"。</para>
        ///
        /// <para>结论：<b>餐点是玩家显式配置的意图，不做时段过滤</b>。
        /// 串台问题的真正来源是"班组把多个班次的餐点<b>取并集</b>"
        /// （见 <see cref="BuildCrewMealHours"/>）—— 那是"一个班组多个班次"的语义问题，
        /// 该在那里解决，而不是靠丢弃玩家配置来掩盖。</para>
        ///
        /// <para>落在工作时段外的餐点由 <c>ApplyWorkHoursToCrew</c> 的判定顺序落到「用餐」：
        /// 该顺序里「用餐」<b>优先于「睡眠」</b> ⇒ "上班前吃饭"能正常落地
        /// （这也是那条优先级现在**必须**保留的原因）。</para>
        ///
        /// <para><c>hour &lt; 0</c>（该餐关闭）⇒ 不涂。</para>
        /// </summary>
        private static void MarkMealHour(bool[] hours, int hour)
        {
            if (hours == null || hour < 0 || hour >= hours.Length)
            {
                return;
            }
            hours[hour] = true;
        }

        internal const string IdleTimeAssignmentDefName = "PC_Idle";

        /// <summary>
        /// 车间"工位"容量 = 车间内设备数；<c>-1</c> = 未知（取不到 zone/map）⇒ 调用方不得据此判定冗余
        /// （宁可不摸鱼，也不能把在岗的人误判成冗余）。
        /// </summary>
        private static int DeviceCapacityOf(Zone_Workshop zone)
        {
            if (zone == null || zone.Map == null)
            {
                return -1;
            }
            List<Thing> devices = WorkshopStats.ScanDevices(zone);
            return devices != null ? devices.Count : -1;
        }

        private static TimeAssignmentDef cachedIdleAssignment;
        private static bool warnedMissingIdleAssignment;

        /// <summary>
        /// 解析摸鱼时段类型（启动期一次性缓存；Def 缺失 ⇒ 返回 null 并只告警一次，
        /// 调用方随即退回"全员写 Work"的旧行为 —— 缺 Def 不得改变既有语义）。
        /// </summary>
        internal static TimeAssignmentDef ResolveIdleTimeAssignment()
        {
            if (cachedIdleAssignment != null)
            {
                return cachedIdleAssignment;
            }
            cachedIdleAssignment = DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(
                IdleTimeAssignmentDefName);
            if (cachedIdleAssignment == null && !warnedMissingIdleAssignment)
            {
                warnedMissingIdleAssignment = true;
                Log.Warning("[PersonalChronicle] Idle time assignment not found (redundant "
                    + "workers will keep standing duty as before): " + IdleTimeAssignmentDefName);
            }
            return cachedIdleAssignment;
        }
        /// <summary>本车间参与倒班的班次（<c>UseCrewCycle</c> 且指向本车间）。</summary>
        private static List<WorkshopShift> CollectCycleShifts(Zone_Workshop zone)
        {
            var list = new List<WorkshopShift>();
            List<WorkshopShift> all = zone.Map != null
                ? MapComponent_WorkshopManager.Get(zone.Map).Shifts
                : null;
            if (all == null)
            {
                return list;
            }
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].UseCrewCycle && all[i].TargetZone == zone)
                {
                    list.Add(all[i]);
                }
            }
            return list;
        }

        /// <summary>该班组被<b>显式指派</b>到本车间的班次（引用口径，按 ID 升序返回）。</summary>
        private static List<WorkshopShift> CollectAssignedShifts(Zone_Workshop zone, WorkshopCrew crew)
        {
            var list = new List<WorkshopShift>();
            if (crew.AssignedShifts == null)
            {
                return list;
            }
            for (int i = 0; i < crew.AssignedShifts.Count; i++)
            {
                WorkshopShift s = crew.AssignedShifts[i];
                if (s != null && s.TargetZone == zone)
                {
                    list.Add(s);
                }
            }
            list.Sort((a, b) => a.LoadId.CompareTo(b.LoadId));
            return list;
        }

        /// <summary>该班组被显式指派到本车间的班次数量。</summary>
        private static int CountAssignedShifts(Zone_Workshop zone, WorkshopCrew crew)
        {
            return CollectAssignedShifts(zone, crew).Count;
        }

        /// <summary>
        /// <b>RotateDaily 专用</b>：该班组<b>当日</b>应值的班次（保留旧口径作对照）。
        /// <para>取法与派工同源：显式指派多班时按倒班表（含调休/请假例外）反查当日轮到的那个；
        /// 未显式指派时按班组序位取一个倒班班次。</para>
        /// </summary>
        private WorkshopShift EffectiveShiftOfCrewForDay(Zone_Workshop zone, WorkshopCrew crew,
            int crewIndex, List<WorkshopShift> cycleShifts, int dayIndex)
        {
            List<WorkshopShift> assigned = CollectAssignedShifts(zone, crew);
            if (assigned.Count == 0)
            {
                return cycleShifts.Count == 0 ? null : cycleShifts[crewIndex % cycleShifts.Count];
            }
            if (assigned.Count == 1)
            {
                return assigned[0];
            }
            // 被指派多班 => 轮换：当日只值其中一个。
            int shiftCount;
            if (!managerShiftCount(zone, out shiftCount) || shiftCount <= 0)
            {
                // 拿不到序位数（未启用倒班）=> 无法判断当日轮值，退回"第一个"，
                // 宁可少写一段 Work，也不取并集（并集 = 无眠）。
                return assigned[0];
            }
            int crewCount = zone.Crews != null ? zone.Crews.Count : 0;
            int phase = dayIndex - zone.CycleAnchorDay;
            int[] kinds = zone.ExceptionKindsFor(dayIndex, crewCount);
            for (int i = 0; i < assigned.Count; i++)
            {
                WorkshopShift s = assigned[i];
                if (s == null || s.ShiftSlotIndex < 0)
                {
                    continue;
                }
                if (WorkshopCrewCycle.CrewOfShiftSlot(phase, s.ShiftSlotIndex, crewCount,
                    shiftCount, kinds) == crewIndex)
                {
                    return s;
                }
            }
            return null;   // 当日轮休 / 例外导致无人 => 整天 Sleep（正确：休息日就该睡）
        }

        /// <summary>本车间参与倒班的班次序位数量（无则 false）。</summary>
        private bool managerShiftCount(Zone_Workshop zone, out int count)
        {
            count = 0;
            if (zone == null || zone.Map == null)
            {
                return false;
            }
            MapComponent_WorkshopManager mgr = MapComponent_WorkshopManager.Get(zone.Map);
            if (mgr == null)
            {
                return false;
            }
            List<WorkshopShift> all = mgr.Shifts;
            for (int i = 0; i < all.Count; i++)
            {
                WorkshopShift shift = all[i];
                // ⚠ 深度审计 X2（2026-10-04）：条件原为 `shift.TargetZone == zone`（写反）。
                // 本方法语义是"**本车间**参与倒班的班次序位数量"，原写法却把本车间全部排除、
                // 只统计**其它车间**的班次 ⇒ shiftCount 取到 0 或他车间的值
                // ⇒ 调用点（:432 → WorkshopCrewCycle.CrewOfShiftSlot）的轮转基数错误
                // ⇒ 班组被轮到错误的班次（"人莫名其妙值错班"的同源问题）。
                // 修复为 `!=`：只保留本车间、启用转班、有序位的班次。
                if (shift == null || !shift.UseCrewCycle || shift.TargetZone != zone
                    || shift.ShiftSlotIndex < 0)
                {
                    continue;
                }
                if (shift.ShiftSlotIndex + 1 > count)
                {
                    count = shift.ShiftSlotIndex + 1;
                }
            }
            return count > 0;
        }
    }
}
