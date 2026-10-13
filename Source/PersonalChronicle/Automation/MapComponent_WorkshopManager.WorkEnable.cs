using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 排班 → 原版「工作」面板联动（2026-10-02）。
    ///
    /// <para><b>要解决的现象</b>（用户实机反馈）：「安排了班次和班组后不会自动安排原版的 work 工作，
    /// 导致 pawn 闲置」——车间值勤 Job（<c>PC_WorkshopOperation</c>）在 Def 里是
    /// <c>alwaysStartActive=false</c>，玩家不在原版工作面板手动勾选就永远不会被
    /// <c>JobGiver_Work</c> 排到；此时即便时间管制表已被
    /// <see cref="SyncVanillaTimetables"/> 写成 Work，成员也只是"处在工作时段却无事可做"。</para>
    ///
    /// <para><b>写入范围（EXC-2026-012 + EXC-2026-013 硬约束，勿扩大）</b>：</para>
    /// <list type="bullet">
    /// <item><b>值勤类型收敛到默认档</b>（<c>PC_WorkshopOperation</c>）：目标档位恒为
    /// <see cref="DutyWorkPriority"/>（原版默认档 3）—— <c>0 → 3</c> 补勾选，
    /// 被错误提升或手改的 <c>1 / 2 / 4 → 3</c> 拉回；值勤<b>绝不参与</b>最高档提升
    /// （2026-10-02 实机 bug 修复：它曾被误提升到 1，导致映射工作被禁用的人只会站岗）。</item>
    /// <item><b>映射类型提升到最高档</b>（EXC-2026-013）：车间类型 Def 声明的原版工作类型统一收敛到
    /// <see cref="TopWorkPriority"/>（<c>0 → 1</c> 含重开、<c>2/3/4 → 1</c> 覆盖；已是 1 则跳过）。</item>
    /// <item><b>简单模式不写档位</b>：<c>!Find.PlaySettings.useWorkPriorities</c> 时引擎不读档位 ⇒ 降级为仅 Enable。</item>
    /// <item><b>绝不反向</b>：不调用 <c>Disable</c> / <c>SetPriority(0)</c>，不重排其它工作类型的相对次序。</item>
    /// <item><b>仅排班相关人员</b>：班组成员（无班组时回落车间名单）+ 班次显式候选池。</item>
    /// <item><b>仅事件驱动</b>：由管理面板的排班/名册变更触发；<b>禁</b> tick、禁绘制路径、禁 JobGiver 内写。</item>
    /// <item>写入类型受限于：值勤类型 <c>PC_WorkshopOperation</c> ∪ 车间类型 Def 在
    /// <c>Defs/WorkshopTypes.xml</c> 里声明的岗位映射（C# 侧无任何 defName 业务字面量）。</item>
    /// <item><b>全局设置只在玩家点击时改</b>：<see cref="EnableManualWorkPriorities"/> 由面板按钮触发，
    /// 不自动改写（EXC-2026-013 · Alternatives rejected 5）。</item>
    /// </list>
    ///
    /// <para>守卫镜像原版 <c>Pawn_WorkSettings</c>：<c>workSettings != null</c>、先
    /// <c>EnableAndInitializeIfNotAlreadyInitialized()</c>、且跳过 <c>pawn.WorkTypeIsDisabled</c>
    /// （原版对 disabled 类型写非 0 会 <c>Log.Error</c>）。</para>
    /// </summary>
    public partial class MapComponent_WorkshopManager
    {
        /// <summary>岗位值勤工作类型（Defs/Automation_WorkType.xml）。</summary>
        private const string DutyWorkTypeDefName = "PC_WorkshopOperation";

        /// <summary>
        /// 原版工作面板的**最高**档位（EXC-2026-013）。
        ///
        /// <para>原版 <c>Pawn_WorkSettings</c> 只定义了 <c>LowestPriority = 4</c> 与
        /// <c>DefaultPriority = 3</c>，**没有** <c>HighestPriority</c> 常量 —— 最高档就是字面量 `1`
        /// （工作面板以 <c>priority != 1</c> 判定"是否已是最高"）。因此这里必须给它一个命名常量，
        /// 不得在业务代码里散落 `1`（GOV-006）。</para>
        /// </summary>
        private const int TopWorkPriority = 1;

        /// <summary>
        /// 值勤类型（<c>PC_WorkshopOperation</c>）应保持的档位 = 原版默认档。
        ///
        /// <para>依据 <c>Defs/Automation_WorkType.xml:9-19</c>：值勤是「没有本职工作时才回车间值勤 /
        /// 看护产线」的<b>兜底</b>，配合其 <c>naturalPriority = 350</c>（低于建造 900 / 采矿 600 /
        /// 种植 700 / 砍伐 500 / 锻造 470，高于搬运 300 / 清洁 200 / 研究 100）。把它顶到最高档会
        /// 复现 Def 注释记载的 P0 反模式：成员一整天站着值勤、产出链断裂。</para>
        /// </summary>
        private const int DutyWorkPriority = Pawn_WorkSettings.DefaultPriority;

        /// <summary>Def 解析缓存（Def 是静态数据；解析失败时下次调用重试，不做永久否定缓存）。</summary>
        private WorkTypeDef dutyWorkType;

        private readonly HashSet<Pawn> workSyncSeen = new HashSet<Pawn>();
        private readonly List<Pawn> workSyncScope = new List<Pawn>();
        private readonly List<WorkTypeDef> workSyncTypes = new List<WorkTypeDef>();

        /// <summary>已告警过的未解析 defName（每个只告警一次，避免刷日志）。</summary>
        private readonly HashSet<string> unresolvedWorkTypeWarned = new HashSet<string>();

        /// <summary>
        /// 按「值勤类型 + 岗位映射」联动原版工作面板：值勤**收敛到默认档**、映射类型**提升到最高档**。
        /// 幂等，可重复调用（同时也是把历史错误档位拉回设计值的自愈入口）。
        /// 由 <c>WorkshopManageView.OnRosterChanged</c> / <c>Refresh</c> 在排班类变更后调用。
        /// </summary>
        public void SyncVanillaWorkSettings(Zone_Workshop zone)
        {
            if (zone == null)
            {
                return;
            }
            CollectWorkSyncScope(zone);
            if (workSyncScope.Count == 0)
            {
                return;
            }
            CollectWorkSyncTypes(zone);
            // 映射为空**且**值勤类型不可解析时才真正无事可做；否则仍要跑一遍（至少收敛值勤档位）。
            // 2026-10-02：此前 workSyncTypes 里始终含值勤类型，该提前返回不会挡住值勤；把值勤移出
            // 列表后必须显式放行，否则「未指定车间类型」的车间连值勤都不会被勾选（G3 回归）。
            if (workSyncTypes.Count == 0 && DutyWorkType() == null)
            {
                // 仍要走一趟状态装配：该形态下没有任何工作需要提权（不给任何工作提权），
                // 但 L4 状态行需要如实报出"保留工作 0 项"，让玩家看出是"车间没配类型"而非"功能坏了"。
                // ⚠ CHG-20261005-NODEMOTE：此处原写"走一趟降档通道把其余工作压到 4"—— 降档已移除，
                //   现在**不会**再改动任何工作档位（其余工作保持玩家原值）。
                SyncWorkStationForZone(zone);
                return;
            }
            for (int i = 0; i < workSyncScope.Count; i++)
            {
                // CHG-20261004-SHIFTGATE：离班成员不自动提权。
                // 根因（PERFDIAG 标注 P0）：本方法由面板 Refresh / OnRosterChanged 驱动，
                // 对**全部**成员写值勤 3 / 映射 1，不看是否在班；而 ApplyOffShiftWorkTypes
                // 每整点把离班者写 0 ⇒ 玩家开面板→写 3、到整点→写 0，无限 0/3 互跳。
                // 加门控后：在班者稳定 3/1，离班者稳定 0，互跳根除。
                if (ShouldSkipAutoEnableForShift(workSyncScope[i], zone)) { continue; }
                EnableWorkTypes(workSyncScope[i]);
            }
            // 状态装配：**必须在 EnableWorkTypes 之后** —— 它统计的是"本轮是否产生了提权写入"
            // 与各计数读数，放在写入之后才能反映真实结果。
            // ⚠ CHG-20261005-NODEMOTE：此处原写"降档通道（EXC-2026-015）必须在 Enable 之后，
            //   否则会把刚打开的类型又压回 4"—— 降档已移除 ⇒ 不存在该顺序风险，调用仅为读数。
            SyncWorkStationForZone(zone);
        }

        /// <summary>
        /// CHG-20261004-SHIFTGATE · 班次门控：该pawn 当前是否<b>不应</b>被自动提权。
        ///
        /// <para><b>判据必须与 <see cref="RunOffShiftPass"/> / <see cref="DisableForCrew"/>
        /// 严格同口径</b>：只看「已编入本 zone 的班组」+「该班组已排班」+「当前无任一班次在班」。
        /// 两条链口径一旦分叉就会错配——门控认为在班（不写）而离班链认为离班（写 0），
        /// 岗位会无故失效；反向则等于互跳没修。</para>
        ///
        /// <para><b>返回 true = 跳过提权</b>。以下情形返回 false（照旧提权）：
        /// 未编入任何班组 / 班组未排班。<b>关键取舍</b>：班次候选池（<c>shift.Pool</c>）
        /// 里但未编入班组的人<b>不受门控</b>——离班链根本不碰他们（只遍历 <c>crew.Members</c>），
        /// 若门控也一并关掉，就成了「没人写 0、也没有档位可写」的黑洞。</para>
        /// </summary>
        private bool ShouldSkipAutoEnableForShift(Pawn pawn, Zone_Workshop zone)
        {
            if (pawn == null || map == null || zone == null || zone.Crews == null) { return false; }
            List<WorkshopCrew> crews = zone.Crews;
            int hour = GenLocalDate.HourOfDay(map);
            for (int c = 0; c < crews.Count; c++)
            {
                WorkshopCrew crew = crews[c];
                if (crew == null || crew.Members == null
                    || crew.AssignedShifts == null || crew.AssignedShifts.Count == 0)
                {
                    continue;
                }
                bool isMember = false;
                for (int m = 0; m < crew.Members.Count; m++)
                {
                    if (crew.Members[m] == pawn) { isMember = true; break; }
                }
                if (!isMember) { continue; }
                // 已排班：本班组任一班次在班 → 照旧提权；全部离班 → 跳过（交给离班链写 0）。
                for (int s = 0; s < crew.AssignedShifts.Count; s++)
                {
                    if (HourInShift(hour, crew.AssignedShifts[s])) { return false; }
                }
                return true;
            }
            return false;
        }

        /// <summary>参与排班的人员集合 = 班组成员（或未编组时的车间名单）+ 班次显式候选池。</summary>
        private void CollectWorkSyncScope(Zone_Workshop zone)
        {
            workSyncScope.Clear();
            workSyncSeen.Clear();
            List<WorkshopCrew> crews = zone.Crews;
            for (int c = 0; c < crews.Count; c++)
            {
                WorkshopCrew crew = crews[c];
                if (crew == null || crew.Members == null)
                {
                    continue;
                }
                for (int m = 0; m < crew.Members.Count; m++)
                {
                    AddWorkSyncCandidate(crew.Members[m]);
                }
            }
            // 未建班组 ⇒ 与倒班/派工同一回落口径（整份车间人员名单）。
            if (crews.Count == 0 && zone.Members != null)
            {
                for (int m = 0; m < zone.Members.Count; m++)
                {
                    AddWorkSyncCandidate(zone.Members[m]);
                }
            }
            // 班次自定义候选池（玩家可能只用班次池、不建班组）。
            for (int i = 0; i < shifts.Count; i++)
            {
                WorkshopShift shift = shifts[i];
                if (shift == null || shift.TargetZone != zone || shift.Pool == null)
                {
                    continue;
                }
                for (int p = 0; p < shift.Pool.Count; p++)
                {
                    AddWorkSyncCandidate(shift.Pool[p]);
                }
            }
        }

        private void AddWorkSyncCandidate(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || !workSyncSeen.Add(pawn))
            {
                return;
            }
            workSyncScope.Add(pawn);
        }

        /// <summary>本次应勾选的原版工作类型 = 本车间班次岗位映射（去重，保序）。</summary>
        /// <remarks>
        /// ⚠ <b>值勤类型刻意不入列表</b>（2026-10-02 实机 bug 修复）：旧实现把
        /// <c>PC_WorkshopOperation</c> 一并塞进本列表，于是它在 <see cref="EnableWorkTypes"/> 的
        /// 提升循环里被设成最高档 —— 与 EXC-2026-013「值勤类型不参与提升」直接冲突。
        ///
        /// <para>实机后果（用户 2026-10-02 反馈「有些 pawn 值勤而不工作」）：原版
        /// <c>JobGiver_Work</c> 按 <c>naturalPriority + (4 − 档位) × 100000</c> 排序取值，第一个能产出
        /// Job 的 WorkGiver 胜出。值勤被顶到 1 档后权重 300000，只要某人的<b>映射工作被背景/技能禁用</b>
        /// （如驾驶员禁烹饪 ⇒ 该类型被 <c>WorkTypeIsDisabled</c> 守卫跳过、保持 0 档），他面板里就只剩
        /// 「车间」一项高优先级 ⇒ 一出空闲就去站岗，永不干活。</para>
        ///
        /// <para>值勤一律走 <see cref="EnableWorkTypes"/> 的①通道（收敛到
        /// <see cref="DutyWorkPriority"/>）。</para>
        /// </remarks>
        private void CollectWorkSyncTypes(Zone_Workshop zone)
        {
            workSyncTypes.Clear();
            WorkshopTypeDef typeDef = zone.WorkshopType;
            if (typeDef == null)
            {
                // 未指定车间类型 ⇒ 无岗位映射可用，只保证值勤链可用（G3 仍成立）。
                return;
            }
            for (int i = 0; i < shifts.Count; i++)
            {
                WorkshopShift shift = shifts[i];
                if (shift == null || shift.TargetZone != zone)
                {
                    continue;
                }
                List<string> names = typeDef.WorkTypeDefNamesOf(shift.TaskType);
                for (int n = 0; n < names.Count; n++)
                {
                    WorkTypeDef mapped = ResolveWorkType(names[n]);
                    if (mapped != null && !workSyncTypes.Contains(mapped))
                    {
                        workSyncTypes.Add(mapped);
                    }
                }
            }
        }

        /// <summary>
        /// defName → WorkTypeDef（缺失静默跳过，但每个 defName 记**一次**诊断）。
        ///
        /// <para>COMP-011 可观测性：映射表写错（或目标 Def 未加载）时若完全静默，
        /// 「岗位选了却没勾上」将无从排查；这里对每个未解析的 defName 只告警一次，
        /// 不构成热路径日志（本方法仅事件驱动调用，且去重集合为实例级）。</para>
        /// </summary>
        private WorkTypeDef ResolveWorkType(string defName)
        {
            WorkTypeDef def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(defName);
            if (def == null && !string.IsNullOrEmpty(defName) && unresolvedWorkTypeWarned.Add(defName))
            {
                Log.Warning("[PersonalChronicle] Workshop duty work-type mapping not found (skipped): "
                    + defName);
            }
            return def;
        }

        private WorkTypeDef DutyWorkType()
        {
            if (dutyWorkType == null)
            {
                // 解析失败不做永久否定缓存（下次调用重试）；诊断走 ResolveWorkType 的去重告警。
                dutyWorkType = ResolveWorkType(DutyWorkTypeDefName);
            }
            return dutyWorkType;
        }

        /// <summary>
        /// 确保本车间**全体成员**的 <c>workSettings</c> 已初始化（CHG-20261005-EVERWORK，2026-10-05）。
        ///
        /// <para><b>为什么需要这个独立通道</b>：原版 <c>JobGiver_Work.GetPriority</c> 的**第一道**闸门是
        /// <c>if (pawn.workSettings == null || !pawn.workSettings.EverWork) return 0f;</c>
        /// ⇒ 未初始化的人即使作息表写着「工作」也**拿不到工作 Job**。玩家实机症状：
        /// 两班倒人员「当班却不动」，而点一次「接管原版工作」就突然正常
        /// —— 因为那是 PC 里**唯一对全体成员无条件初始化**的路径（审计结论，2026-10-05）。</para>
        ///
        /// <para><b>本方法只做初始化，不写任何档位</b>：原版
        /// <c>EnableAndInitializeIfNotAlreadyInitialized()</c> 对已初始化者是**空操作**（幂等）。
        /// 因此它不违反「禁 tick / 禁绘制路径写 workSettings」的纪律 —— 由面板
        /// <c>Refresh</c> 与作息表同步路径在**事件驱动**时机调用，且不产生任何档位改写语义。</para>
        ///
        /// <para>成员口径 = <c>AllWorkshopMemberPawns(zone)</c>（人员名单 ∪ 班组编制，唯一实现）
        /// ⇒ 与接管 / 自动隐藏 / 作息表写入完全同一份成员视图，不会出现"某条链认为他在车间、
        /// 另一条链漏了他"的分叉。</para>
        /// </summary>
        public void EnsureWorkSettingsInitialized(Zone_Workshop zone)
        {
            if (zone == null)
            {
                return;
            }
            List<Pawn> scope = AllWorkshopMemberPawns(zone);
            for (int i = 0; i < scope.Count; i++)
            {
                Pawn pawn = scope[i];
                if (pawn == null || pawn.Destroyed || pawn.Dead)
                {
                    continue;
                }
                Pawn_WorkSettings settings = pawn.workSettings;
                if (settings != null)
                {
                    settings.EnableAndInitializeIfNotAlreadyInitialized();
                }
            }
        }

        /// <summary>
        /// 对单个 pawn 应用「值勤类型 Enable + 映射类型提升到最高档」。
        ///
        /// <para>两条通道的差别（EXC-2026-012 vs EXC-2026-013）：</para>
        /// <list type="bullet">
        /// <item><b>值勤类型</b>（<c>PC_WorkshopOperation</c>）：仅 Enable（<c>0 → 3</c>），<b>不提升</b> ——
        /// 依据 <c>Defs/Automation_WorkType.xml:9-19</c>（顶到最高会"一直站着值勤、不去干活"）。</item>
        /// <item><b>映射的原版类型</b>：Enable + 提升到 <see cref="TopWorkPriority"/>（<c>0 → 1</c>
        /// 与 <c>2/3/4 → 1</c> 统一收敛）。</item>
        /// </list>
        ///
        /// <para><b>简单模式守卫</b>：<c>!Find.PlaySettings.useWorkPriorities</c> 时原版引擎不读档位
        /// （<c>GetPriority</c> 对任何已启用工作恒返回 3），此时只做 Enable、<b>不做档位写入</b> ——
        /// 既避免无意义写入，也让幂等判断不会因"读回来的值恒为 3"而失效。开启途径见面板状态行。</para>
        /// </summary>
        private void EnableWorkTypes(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead)
            {
                return;
            }
            Pawn_WorkSettings settings = pawn.workSettings;
            if (settings == null)
            {
                return;
            }
            settings.EnableAndInitializeIfNotAlreadyInitialized();

            // ① 值勤类型：收敛到默认档（2026-10-02 实机 bug 修复）。
            //    旧实现只在 <= 0 时写默认档，同时值勤又出现在②的提升列表里 ⇒ ②紧接着把它改成最高档，
            //    「仅 Enable、不提升」的语义被完全覆盖（实机表现：所有排班成员的「车间」列都变成 1）。
            //    现在主动收敛：0 → 3（补勾选）、1/2/4 → 3（把被错误提升的值拉回设计档位）。
            //    这不是新增授权 —— EXC-2026-013 明文规定「值勤类型不参与提升」，本改动正是执行该边界。
            WorkTypeDef duty = DutyWorkType();
            if (duty != null && !pawn.WorkTypeIsDisabled(duty)
                && settings.GetPriority(duty) != DutyWorkPriority)
            {
                settings.SetPriority(duty, DutyWorkPriority);
            }

            // ② 车间类型映射的原版工作类型：Enable + 提升到最高档。
            PlaySettings play = Find.PlaySettings;
            bool prioritiesEffective = play != null && play.useWorkPriorities;
            for (int i = 0; i < workSyncTypes.Count; i++)
            {
                WorkTypeDef type = workSyncTypes[i];
                // 原版守卫：disabled（技能缺失/背景禁用）类型写非 0 会 Log.Error。
                if (type == null || pawn.WorkTypeIsDisabled(type))
                {
                    continue;
                }
                if (!prioritiesEffective)
                {
                    // 简单模式：档位不被引擎读取 ⇒ 只保证启用（不做无意义写入）。
                    if (settings.GetPriority(type) <= 0)
                    {
                        settings.SetPriority(type, Pawn_WorkSettings.DefaultPriority);
                    }
                    continue;
                }
                // 已是最高档则跳过（幂等）；0 / 2 / 3 / 4 统一收敛到最高档。
                if (settings.GetPriority(type) != TopWorkPriority)
                {
                    settings.SetPriority(type, TopWorkPriority);
                }
            }
        }

        /// <summary>
        /// 由玩家在管理面板点击触发（EXC-2026-013 要求的披露出口）：开启原版「手动优先级」。
        ///
        /// <para>为什么需要这一步：车间档位提升只在<b>手动优先级</b>模式下被引擎读取；简单模式下
        /// 所有已启用工作等价（<c>Pawn_WorkSettings.GetPriority</c> 恒返回 3），"优先度最高"不可达。</para>
        ///
        /// <para>写法严格镜像原版（<c>MainTabWindow_Work.cs:69-78</c>）：写设置 → 对全部玩家 pawn 调
        /// <c>Notify_UseWorkPrioritiesChanged()</c> 置脏其工作链缓存 → 立即对本图各车间重放一次档位提升
        /// （否则要等下一次排班变更才生效）。</para>
        ///
        /// <para><b>刻意不自动调用</b>：该设置是全局的（随存档持久化）且改变所有殖民者工作面板的形态，
        /// 属玩家决策；自动改写命中 E3「不可回滚 + 全局性改写」，需另行登记与批准
        /// （见 EXC-2026-013 · Alternatives rejected 5）。</para>
        /// </summary>
        public void EnableManualWorkPriorities()
        {
            PlaySettings settings = Find.PlaySettings;
            if (settings == null || settings.useWorkPriorities)
            {
                return;
            }
            settings.useWorkPriorities = true;
            List<Pawn> all = PawnsFinder.AllMapsWorldAndTemporary_Alive;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn other = all[i];
                if (other != null && other.Faction == Faction.OfPlayer && other.workSettings != null)
                {
                    other.workSettings.Notify_UseWorkPrioritiesChanged();
                }
            }
            // 翻转后立即重放：映射类型此刻才具备"提升"的意义。
            List<Zone_Workshop> zones = AllWorkshopZones();
            for (int i = 0; i < zones.Count; i++)
            {
                SyncVanillaWorkSettings(zones[i]);
            }
        }

        // ---- CHG-20261004-WSSETTINGS · 在班自动回写（消除「每天手动开面板点一次」）----

        /// <summary>上次执行"在班回写"的游戏内小时（<c>-1</c> = 未记账）。</summary>
        private int lastRestoreHour = -1;

        /// <summary>
        /// 按<b>游戏内小时</b>对"当前应到岗"的成员补齐被清 0 的车间类工作档位。
        ///
        /// <para><b>为什么必须有</b>（玩家实测痛点 2026-10-04：「接管/车间工作好像每天都要
        /// 手动点一次」）。取证结论：真正每天抖动的是<b>不对称的写链</b> ——
        /// <c>ApplyOffShiftWorkTypes</c> 在 <c>MapComponentTick</c> 里<b>每游戏小时</b>把
        /// 车间类工作类型写 0（离班），而"在班回写"此前只挂在管理面板的
        /// <c>Refresh</c> / <c>OnRosterChanged</c> 上（<c>SyncVanillaWorkSettings</c> 的
        /// 调用点里<b>没有任何 tick 触发面</b>）⇒ 班次一切换，档位停在 0
        /// （连值勤 Job 都排不出来），<b>直到玩家开一次面板</b>。本方法补齐对称性。</para>
        ///
        /// <para><b>为什么只补 0</b>：<c>EnableWorkTypes</c> 的语义是"0/2/3/4 统一收敛到最高档"，
        /// 若每小时照搬会<b>覆盖玩家在面板里手调的档位</b>；本方法只把仍为 0 的补到目标档
        /// ⇒ 幂等，且不夺走玩家的手动调整。</para>
        ///
        /// <para><b>节拍</b>：与离班同一门控（<c>GenLocalDate.HourOfDay</c> 变化才跑一次），
        /// 由 <c>MapComponentTick</c> 驱动（60 tick 一轮）⇒ 不在热路径。</para>
        /// </summary>
        private void RestoreOnShiftWorkTypes()
        {
            ChronicleSettings settings = PersonalChronicle.PersonalChronicleMod.Settings;
            if (settings == null || !settings.AutoRestoreOnShift)
            {
                lastRestoreHour = -1;   // 关闭期间不记账 ⇒ 重新开启后立即补跑一次
                return;
            }
            int hour = GenLocalDate.HourOfDay(map);
            if (hour == lastRestoreHour)
            {
                return;   // 每游戏小时最多一次（与 OffShift 同节拍）
            }
            lastRestoreHour = hour;

            EnsureDutyPlan();
            if (dutyPlan.Count == 0)
            {
                return;   // 当前小时无人应到岗
            }

            WorkTypeDef duty = DutyWorkType();
            bool prioritiesEffective = Find.PlaySettings != null && Find.PlaySettings.useWorkPriorities;
            int mappedTarget = prioritiesEffective
                ? TopWorkPriority
                : Pawn_WorkSettings.DefaultPriority;

            List<Zone_Workshop> zones = AllWorkshopZones();
            for (int z = 0; z < zones.Count; z++)
            {
                Zone_Workshop zone = zones[z];
                if (zone == null)
                {
                    continue;
                }
                CollectWorkSyncTypes(zone);
                if (workSyncTypes.Count == 0)
                {
                    continue;
                }
                CollectWorkSyncScope(zone);
                for (int i = 0; i < workSyncScope.Count; i++)
                {
                    Pawn pawn = workSyncScope[i];
                    if (pawn == null || pawn.Dead || pawn.Destroyed
                        || pawn.workSettings == null || !dutyPlan.ContainsKey(pawn))
                    {
                        continue;   // 只处理"当前小时在该车间应到岗"的人
                    }
                    Pawn_WorkSettings ws = pawn.workSettings;
                    for (int t = 0; t < workSyncTypes.Count; t++)
                    {
                        WorkTypeDef type = workSyncTypes[t];
                        if (type == null || pawn.WorkTypeIsDisabled(type))
                        {
                            continue;
                        }
                        if (ws.GetPriority(type) <= 0)
                        {
                            ws.SetPriority(type, mappedTarget);
                        }
                    }
                    if (duty != null && !pawn.WorkTypeIsDisabled(duty)
                        && ws.GetPriority(duty) <= 0)
                    {
                        ws.SetPriority(duty, DutyWorkPriority);
                    }
                }
            }
        }
    }
}
