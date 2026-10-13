using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PersonalChronicle.Application.Effects;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间全局状态与调度器（参考文档 §5.2，按 1.6 与本项目架构改造）：
    /// - 持有 <see cref="WorkshopSlot"/>（固定班）与 <see cref="RotationRule"/>（轮岗班），
    ///   随存档持久化；
    /// - 每 <see cref="CheckIntervalTicks"/> tick 一次低频检查（参考文档 60 tick 方案，
    ///   禁每 tick 遍历 pawn）；
    /// - 轮岗到点推进 <see cref="RotationRule.Advance"/>；
    /// - 在岗名单（activeWorkers）是<b>运行态</b>，由 JobDriver 上岗/收工回报，不持久化
    ///   （设计文档 §10：不保存可从运行重建的数据）。
    ///
    /// 自动岗位在用餐/休息/下班边界收尾；玩家强制任务、征召及紧急服务保留原版控制。
    /// 岗位运行通过原版工作链调度，Job 开始/结束登记考勤，地图低频轮询用于读档自愈。
    /// </summary>
    public partial class MapComponent_WorkshopManager : MapComponent
    {
        private const int CheckIntervalTicks = 60;

        /// <summary>
        /// 统一调度计划的兜底重建周期（检查间隔数，20 × 60 tick = 20 游戏秒）。
        ///
        /// <para>2026-10-02 起替代旧的"每个检查间隔无条件重建"：正常路径由
        /// <see cref="ReconcileDutyPlan"/> 的可观测失配与排班变更事件驱动，本常量只兜住
        /// 「外部状态恢复」这类无法观测的变化（倒地者起身 / 远征归队 / 新殖民者加入）。</para>
        /// </summary>
        private const int DutyPlanFallbackIntervals = 20;

        private const string DutyJobDefName = "PC_WorkshopDuty";

        private List<WorkshopSlot> slots = new List<WorkshopSlot>();
        private List<RotationRule> rotations = new List<RotationRule>();

        /// <summary>班组班次（玩家自定义人数与时段的"名额制"排班）。随存档持久化。</summary>
        private List<WorkshopShift> shifts = new List<WorkshopShift>();

        // ---- 统一调度计划缓存 -------------------------------------------------
        // 把「固定班 / 班组 / 轮岗」三类排班合成一份 pawn → 车间 的上岗名单：
        // ① 避免 WorkGiver 每次扫描为每个 pawn 各算一遍（原实现是每查询全表线性扫）；
        // ② 避免"两处都算他该上岗"的冲突（同一 pawn 只归一个岗位）。
        // 失效时机：小时变化、任一排班增删改、每 CheckIntervalTicks 兜底（UI 直接改字段也最多滞后 1 秒）。
        private Dictionary<Pawn, Zone_Workshop> dutyPlan;
        private int dutyPlanHour = -1;
        // （2026-10-04 深度审计：原 `dutyPlanBuiltTick` 只写不读 —— 失效判定用的是
        //   dutyPlanHour / dutyPlanDirty / CheckIntervalTicks 兜底，该字段无消费点，已删除。）
        private bool dutyPlanDirty = true;
        private int dutyPlanFallbackQuota;

        /// <summary>当前正在履行岗位的 pawn → 车间。运行态，不持久化。</summary>
        private readonly Dictionary<Pawn, Zone_Workshop> activeWorkers = new Dictionary<Pawn, Zone_Workshop>();

        /// <summary>pawn → 本次值勤开始 tick（Phase 2：收工时算时长并入考勤台账）。运行态。</summary>
        private readonly Dictionary<Pawn, int> dutyStartTicks = new Dictionary<Pawn, int>();

        /// <summary>
        /// pawn → 上岗时解析好的车间加成快照（落地管线 Step 4：缓存层）。
        /// 只在 NotifyDutyStarted 算一次，之后 StatPart/生产挂载点直接读取 ——
        /// 这是"性能低成本"的关键：热路径只做一次字典取值 + 一次乘法，不做任何解析。
        /// 运行态，不持久化（可由 zone 类型 + pawn 状态重建）。
        /// </summary>
        private readonly Dictionary<Pawn, WorkshopEffectiveBonus> bonusCache = new Dictionary<Pawn, WorkshopEffectiveBonus>();

        /// <summary>车间 → 岗位站位格缓存。车间变更时失效。</summary>
        private readonly Dictionary<Zone_Workshop, IntVec3> dutySpots = new Dictionary<Zone_Workshop, IntVec3>();

        /// <summary>
        /// 班组 → 本次计划**实际分配到的人数**。运行态，随计划重建。
        ///
        /// <para>体检 P2-④：班组行过去显示 <c>PlannedDutyCountOf(zone)</c>（= 全车间计划人数），
        /// 于是同一车间的每个班组行显示的都是同一个数字（且混入了固定班/轮岗来源），
        /// 既不准、又是每帧每行一次 O(计划) 全表扫。改成按来源计数后是 O(1) 取值。</para>
        /// </summary>
        private readonly Dictionary<WorkshopShift, int> shiftFill = new Dictionary<WorkshopShift, int>();

        /// <summary>
        /// pawn → 本次计划的任务类型。运行态，随计划重建。
        /// 消费点：JobDriver 收工时按任务类型上报工时（<c>WorkshopTaskType</c> 过去只被 UI 编辑、
        /// 无任何消费 —— 体检 P3）。
        /// </summary>
        private readonly Dictionary<Pawn, WorkshopTaskType> dutyTaskType = new Dictionary<Pawn, WorkshopTaskType>();


        /// <summary>
        /// pawn → 本次计划命中的<b>班次</b>（CHG-20261003-WORKSHOP-SPLIT · 修成因 B）。
        ///
        /// <para><b>为什么需要它</b>：主动收工判定需要"他当前值的是哪个班次的窗口"，
        /// 而 <c>dutyPlan</c> 只存 pawn → 车间，<b>丢了班次这一层</b>。
        /// 没有它就只能拿车间级信息猜窗口，收工判定会失准。</para>
        ///
        /// <para>运行态，随计划重建（与 <see cref="dutyTaskType"/> 同生命周期）。
        /// 无班次来源（固定班/轮岗）⇒ 该 pawn 不在此表。</para>
        /// </summary>
        private readonly Dictionary<Pawn, WorkshopShift> dutyShiftOf = new Dictionary<Pawn, WorkshopShift>();
        private int quota;
        private JobDef dutyJobDef;

        /// <summary>设备台账（运行时长/产出，按 thingIDNumber，随存档）。设备页读取。</summary>
        private WorkshopDeviceLedger deviceLedger = new WorkshopDeviceLedger();
        private int lastLedgerDay = -1;

        /// <summary>
        /// 设备清单缓存（zone.ID → 设备表）。<b>每 <see cref="DeviceScanRebuildIntervals"/> 个检查间隔
        /// 重建一次</b>（跨日亦清空）—— 2026-10-02 审计（P2-1）修正了此前"注释说每 20 间隔、
        /// 实现只在跨日清空"的漂移：当日新放置 / 拆除的设备原本对运行时长与产出<b>滞后可达一游戏日</b>。
        /// </summary>
        private readonly Dictionary<int, List<Thing>> deviceScanCache = new Dictionary<int, List<Thing>>();

        /// <summary>设备清单缓存的重建周期（检查间隔数；20 × 60 tick = 20 游戏秒）。</summary>
        private const int DeviceScanRebuildIntervals = 20;

        /// <summary>设备清单缓存的间隔计数器。</summary>
        private int deviceScanTick;

        /// <summary>
        /// 本轮设备扫描的 ID 集合（2026-10-02 审计 P1-D）：设备台账淘汰（<see cref="WorkshopDeviceLedger.PruneNotIn"/>）
        /// 的依据 —— 每次扫描先 Clear 再收集，循环末尾据此淘汰台账中不再存在的条目。
        /// 复用同一实例，避免每 60 tick 分配。
        /// </summary>
        private readonly HashSet<int> deviceScanIds = new HashSet<int>();

        /// <summary>
        /// 「车间专属工位」设备通道指纹（CHG-20261003 · L1）。<b>0 = 尚未建立基线</b>。
        /// 纯运行态，不落盘（读档后首轮只建基线、不触发写入）。
        /// </summary>
        private long deviceChannelFingerprint;

        /// <summary>设备/工单指纹变化标记。消费方见 <c>SyncWorkStationIfDirty</c>。</summary>
        private bool deviceChannelDirty;

        /// <summary>设备台账只读入口。</summary>
        public WorkshopDeviceLedger DeviceLedger => deviceLedger ?? (deviceLedger = new WorkshopDeviceLedger());


        /// <summary>
        /// 值勤协调器（CHG-20261003-WORKSHOP-SPLIT · 三模块拆分的值勤侧）。
        /// <b>唯一</b>写原版作息表 / 编排值勤 Job 的地方；<b>不持久化</b>
        /// （可由排班数据完全重建），故惰性创建。
        /// </summary>
        private WorkshopDutyCoordinator dutyCoordinator;

        /// <summary>值勤协调器（惰性创建；一张地图一个实例）。</summary>
        internal WorkshopDutyCoordinator DutyCoordinator
        {
            get { return dutyCoordinator ?? (dutyCoordinator = new WorkshopDutyCoordinator(this)); }
        }
        public List<WorkshopSlot> Slots => slots;
        public List<RotationRule> Rotations => rotations;
        public List<WorkshopShift> Shifts => shifts;

        public MapComponent_WorkshopManager(Map map) : base(map)
        {
        }

        /// <summary>
        /// 弱键缓存：Map → 本组件。
        /// 用 <see cref="ConditionalWeakTable"/> 而非普通 Dictionary —— 键是弱引用，
        /// Map 被销毁后条目自动回收，<b>不会阻止地图被 GC</b>（普通静态字典会泄漏整张地图）。
        /// </summary>
        private static readonly ConditionalWeakTable<Map, MapComponent_WorkshopManager> Cache =
            new ConditionalWeakTable<Map, MapComponent_WorkshopManager>();

        /// <summary>
        /// 惰性创建回调：<b>先查后建</b>。
        /// 必须先 GetComponent —— 读档时组件已由 Scribe 反序列化进 map.components，
        /// 若直接 new 会产生第二个实例（排班/轮岗被拆成两份，实机表现为"排了班但不生效"）。
        /// 静态只读委托，调用处零分配。
        /// </summary>
        private static readonly ConditionalWeakTable<Map, MapComponent_WorkshopManager>.CreateValueCallback Create =
            m =>
            {
                MapComponent_WorkshopManager comp = m.GetComponent<MapComponent_WorkshopManager>();
                if (comp == null)
                {
                    comp = new MapComponent_WorkshopManager(m);
                    m.components.Add(comp);
                }
                return comp;
            };

        /// <summary>
        /// 取（必要时创建）该地图的车间管理器 —— <b>全局唯一入口</b>。
        ///
        /// ⚠️ 此前所有调用点都写 <c>map.GetComponent&lt;MapComponent_WorkshopManager&gt;()</c>，
        /// 但 1.6 无 MapComponentDef 且本类<b>没有任何创建点</b>，导致运行时恒为 null：
        /// 上岗/工时/品质/产出/精度/耐久等全部加成静默失效（编译与单测均发现不了）。
        /// 凡需本组件一律走本方法，禁止再直接 GetComponent。
        ///
        /// 性能：首次之后为一次哈希表命中（O(1)），省掉每次遍历 map.components 的类型扫描。
        /// </summary>
        public static MapComponent_WorkshopManager Get(Map map)
        {
            return map == null ? null : Cache.GetValue(map, Create);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref slots, "slots", LookMode.Deep);
            Scribe_Collections.Look(ref rotations, "rotations", LookMode.Deep);
            Scribe_Collections.Look(ref shifts, "shifts", LookMode.Deep);
            Scribe_Deep.Look(ref deviceLedger, "deviceLedger");
            // CHG-20261005-DEVCOLS：设备别名（加性可选字段：缺省为空 ⇒ 旧档照常加载，
            // 刷新期由 EnsureDeviceAliases 从 A 开始补发，不会与已发字母冲突）。
            Scribe_Collections.Look(ref deviceAliases, "deviceAliases", LookMode.Value);
            Scribe_Values.Look(ref aliasNextIndex, "aliasNextIndex", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (deviceAliases == null)
                {
                    deviceAliases = new Dictionary<int, string>();
                }
                if (slots == null)
                {
                    slots = new List<WorkshopSlot>();
                }
                if (rotations == null)
                {
                    rotations = new List<RotationRule>();
                }
                if (shifts == null)
                {
                    shifts = new List<WorkshopShift>();
                }
                if (deviceLedger == null)
                {
                    deviceLedger = new WorkshopDeviceLedger();
                }
                MarkDutyPlanDirty();
                PurgeDeadReferences();
                // 2026-10-02：班次 ID 补号 + 班组指派的引用列表按 ID 还原。
                // 放在 PurgeDeadReferences 之后 —— 先清掉失效班次，再编号与解析。
                EnsureShiftIdsAndAssignments();
                // 读档后 thingIDNumber 会重新分配：若沿用旧登记表，新造出的物品可能
                // 撞上旧 ID 从而被错误地加上精度/耐久。故载入即清空（宁可不加，不可错加）。
                // 已知取舍：已存档成品的精度/耐久加成不跨存档保留（运行态标记，不持久化）。
                WorkshopProductRegistry.Clear();
            }
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            quota++;
            if (quota < CheckIntervalTicks)
            {
                return;
            }
            quota = 0;
            // 兜底对账（2026-10-02 · PERF-001 修复）：旧实现在这里**无条件** MarkDutyPlanDirty()，
            // 使得统一调度计划在「有任何查询者」（排班页绘制 / WorkGiver）时以每游戏秒一次全量重建，
            // 稳定缓存被系统性破坏。现在改为可观测失配才标脏，见 ReconcileDutyPlan。
            ReconcileDutyPlan();
            PurgeDeadReferences();
            PruneScheduleHistory();
            ProcessRotations();
            // CHG-20261004-PERF（2026-10-04 性能审计 §0.A/🟢#2）：本图车间列表**每轮只取一次**。
            // 原先 AccumulateDeviceRuntime 与 DetectDeviceChannelChange 各自调一次
            // AllWorkshopZones（= 每次 new List + 扫 map.zoneManager.AllZones）⇒ 每 60 tick
            // 白白多一次分配与全表扫描。本方法内部不再自行取列表。
            List<Zone_Workshop> zones = AllWorkshopZones();
            AccumulateDeviceRuntime(zones);
            // CHG-20261003（L1 设备/工单联动）：本轮只做**只读指纹比对**，脏标记在
            // 车间之外被消费（SyncWorkStationIfDirty，由面板刷新/排班事件驱动写入）。
            DetectDeviceChannelChange(zones);
            // CHG-20261005-OFFSHIFT：离班自动取消班组工作类型（内部按游戏小时门控，不是每 tick）。
            ApplyOffShiftWorkTypes();
            // CHG-20261004-WSSETTINGS：在班自动回写（补齐离班清 0 后的档位）。
            // ⚠ 必须紧跟在离班之后：两者共用"游戏内小时变化"节拍，语义上是**一对**——
            //   只写 0 不回写，就是玩家实测的「每天得开一次面板才恢复」。
            RestoreOnShiftWorkTypes();
            UpdateWorkshopDutyRuntime();
        }

        /// <summary>
        /// 取该车间的设备清单，<b>同一轮内多处复用</b> <see cref="deviceScanCache"/>
        /// （20 个检查间隔 ≈ 20 游戏秒重建一次）。
        ///
        /// <para><b>2026-10-04 审计 P3 收口</b>：本方法的两个调用者
        /// （<see cref="AccumulateDeviceRuntime"/> 与 <see cref="DetectDeviceChannelChange"/>）
        /// 原本<b>各自扫一遍</b> —— 后者直接调 <c>WorkshopStats.ScanDevices</c> 绕过缓存，
        /// 同一 tick 内对每个车间重复全格扫描一次。收口后：
        /// ① 每车间每轮只扫一次；
        /// ② 两者看到<b>同一份</b>设备视图，消除"台账已计入新设备、脏指纹还没发现"的窗口。</para>
        /// </summary>
        private List<Thing> DevicesCached(Zone_Workshop zone)
        {
            List<Thing> devices;
            if (!deviceScanCache.TryGetValue(zone.ID, out devices) || devices == null)
            {
                devices = WorkshopStats.ScanDevices(zone);
                deviceScanCache[zone.ID] = devices;
            }
            return devices;
        }

        // ═══════════ 设备别名（CHG-20261005-DEVCOLS，2026-10-05 玩家诉求）═══════════
        //
        // 场景：13 台「炉灶（电力）」在设备页里完全同名，玩家无法区分谁是谁。
        //
        // ⚠ 为什么不改 Thing 的真实显示名（已取证）：原版 Verse.Building **无任何 Rename 成员**，
        //   Thing.GetCustomLabelNoCount 是 virtual 且不可从外部写入 ——「运行时改建筑名」在原版 API
        //   上不存在。硬改（反射写私有字段 / Harmony 覆盖显示名）会污染存档并影响其它 Mod 的读名路径。
        //   故采用**本系统内别名**：只在 PC 的车间界面（设备页 / 概览 / 详情卡）显示。
        //
        // 分配规则：按「建立时间」（台账首次看到 = DeviceStatEntry.firstSeenTick）排序，
        //   依次给 A、B、C…Z；超过 26 个用 A2、B2…。已分配的别名**一经落盘即固定**
        //   —— 设备拆除也不回收字母、不重排，避免"新设备顶着旧别名"的混淆。
        //
        // 有界性：条目数 = 历史设备总数（每图一个字典；玩家建造量自然有界）。
        //   不随扫描轮次增长，也不随游戏天数增长。

        /// <summary>设备别名（thingIDNumber → "A" / "B" …；见本区块注释）。</summary>
        private Dictionary<int, string> deviceAliases = new Dictionary<int, string>();

        /// <summary>
        /// 下一个待发字母的序号（只增不减；落盘 ⇒ 读档后不会重发已用字母）。
        /// 独立于字典 Count，这样将来若要清理已拆设备条目也不会造成别名重号。
        /// </summary>
        private int aliasNextIndex;

        /// <summary>设备别名（未分配 ⇒ 空串，调用方回落到设备本名）。</summary>
        public string AliasOf(Thing device)
        {
            if (device == null || deviceAliases == null)
            {
                return string.Empty;
            }
            string alias;
            return deviceAliases.TryGetValue(device.thingIDNumber, out alias) ? alias : string.Empty;
        }

        /// <summary>
        /// 为尚未分配别名的设备补发（由 <c>WorkshopManageView.Refresh</c> 调用 —— 刷新期一次，
        /// <b>不在 tick 里</b>、也不在绘制路径里）。同轮内按建立时间排序，使早建的设备拿到靠前的字母。
        /// <para>幂等：已有别名的设备不参与、不重排。</para>
        /// </summary>
        public void EnsureDeviceAliases(List<Thing> devices)
        {
            if (devices == null || devices.Count == 0)
            {
                return;
            }
            if (deviceAliases == null)
            {
                deviceAliases = new Dictionary<int, string>();
            }
            List<Thing> pending = null;
            for (int i = 0; i < devices.Count; i++)
            {
                Thing d = devices[i];
                if (d == null || d.Destroyed || deviceAliases.ContainsKey(d.thingIDNumber))
                {
                    continue;
                }
                if (pending == null)
                {
                    pending = new List<Thing>();
                }
                pending.Add(d);
            }
            if (pending == null)
            {
                return;
            }
            // 建立时间升序（缺失 = 0 的旧档设备按 thingID 稳定排序，保证同轮结果可复现）。
            pending.Sort((a, b) =>
            {
                long ta = DeviceLedger.FirstSeenTickOf(a);
                long tb = DeviceLedger.FirstSeenTickOf(b);
                if (ta != tb)
                {
                    return ta.CompareTo(tb);
                }
                return a.thingIDNumber.CompareTo(b.thingIDNumber);
            });
            for (int i = 0; i < pending.Count; i++)
            {
                deviceAliases[pending[i].thingIDNumber] = AliasFor(aliasNextIndex++);
            }
        }

        /// <summary>序号 → 别名（0→A … 25→Z、26→A2、27→B2 …）。</summary>
        private static string AliasFor(int index)
        {
            if (index < 0)
            {
                index = 0;
            }
            char letter = (char)('A' + index % 26);
            int round = index / 26;
            return round == 0 ? letter.ToString() : letter + (round + 1).ToString();
        }

        /// <summary>
        /// 设备在 PC 界面里的显示名。
        ///
        /// <para><b>2026-10-05 变更（玩家要求「针对设备页面的自动编号 abc 移除」）</b>：本方法<b>不再拼别名前缀</b>，
        /// 直接返回 <see cref="EffectiveNameOf"/>。原因：真改名落地后，A/B/C… 自动编号的原始用途
        ///（2026-10-05 DEVCOLS 时「13 台同名炉灶无法区分」）已被「玩家自定义名 + 禁止重名」取代，
        /// 编号反而成了噪声。</para>
        ///
        /// <para>⚠ <see cref="AliasOf"/> 与落盘字段 <c>deviceAliases</c> <b>保留</b>（删字段会动存档 schema）；
        /// 它现在<b>不再进入任何显示路径</b>。将来要恢复只需改回本方法与方法名注释。</para>
        /// </summary>
        public string DisplayNameOf(Thing device)
        {
            return EffectiveNameOf(device);
        }

        /// <summary>
        /// 设备**有效显示名**（CHG-20261005-DEVRENAME）：回落链
        /// <b>车间自定义名 ?? 档案覆盖 ?? 原版默认名</b>（REQ-WORKSHOP-005 / P0-B）。
        ///
        /// <para><b>PC 自绘界面统一经本方法取设备名</b>（不再各自调 <c>WorkshopStats.DeviceLabelOf</c>），
        /// 否则会出现同屏两名（GOV-005：同一事实只能有一个取数口径）。</para>
        ///
        /// <para>返回值<b>不含别名前缀</b> —— 别名是 PC 内的稳定标识，不进原版 UI（M-1 默认）。
        /// 档案覆盖段与 Capture 的标签补丁同源（只读），故原版 UI 与 PC 界面显示一致。</para>
        /// </summary>
        public string EffectiveNameOf(Thing device)
        {
            if (device == null)
            {
                return string.Empty;
            }
            string custom = DeviceLedger != null ? DeviceLedger.CustomNameOf(device) : null;
            if (!string.IsNullOrEmpty(custom))
            {
                return custom;
            }
            string archive = ArchiveOverrideOf(device.thingIDNumber);
            if (!string.IsNullOrEmpty(archive))
            {
                return archive;
            }
            return WorkshopStats.DeviceLabelOf(device);
        }

        /// <summary>
        /// 读 Archive 的档案覆盖表（<b>只读</b>；无则 null）。用途仅为回落链第二段。
        /// <para>⚠ 全限定类型名：本 partial 文件不引入 Data 命名空间的 using，避免为一个只读访问扩大引用面。</para>
        /// </summary>
        private static string ArchiveOverrideOf(int thingId)
        {
            if (thingId <= 0 || Current.Game == null)
            {
                return null;
            }
            PersonalChronicle.Data.ChronicleGameComponent component =
                Current.Game.GetComponent<PersonalChronicle.Data.ChronicleGameComponent>();
            if (component == null || component.BuildingLabelOverrides == null)
            {
                return null;
            }
            string name;
            return component.BuildingLabelOverrides.TryGetValue(thingId, out name) ? name : null;
        }

        /// <summary>设备改名的结果（供 UI 决定是否关窗、给出哪条提示）。</summary>
        public enum DeviceRenameOutcome
        {
            Ok,
            /// <summary>不是车间已追踪设备（台账无条目）。</summary>
            NotTracked,
            /// <summary>超长（<c>WorkshopDeviceNaming.MaxLength</c>）。</summary>
            TooLong,
            /// <summary>同一张地图内已有同名设备（O-2 裁决：比较键 = 有效名，Trim + OrdinalIgnoreCase）。</summary>
            Duplicate,
            /// <summary>设备已拆除 / 不存在。</summary>
            DeviceGone,
        }

        /// <summary>
        /// 设置车间设备自定义名 —— <b>唯一写口</b>（CHG-20261005-DEVRENAME）。
        ///
        /// <para><b>校验顺序</b>（任一失败即拒绝，且<b>不落任何数据</b>）：
        /// ① 设备存在且未拆；② 属车间已追踪设备（台账条目存在 —— 由设备扫描路径的 <c>Touch</c> 建立，
        /// 与设备页同口径）；③ 归一化（含 <b>XML 非法字符清洗</b>，P0-A —— 本值经 Scribe 写进存档 XML，
        /// 非法控制字符会导致<b>整档保存失败</b>）；④ 长度限；⑤ 同图重名。</para>
        ///
        /// <para>⚠ 归一化在<b>此处</b>完成并先于任何写入 ⇒ 保证「内存值 == 存档值」，读路径不再二次清洗。</para>
        ///
        /// <para>比较集由调用方传入（视图 Refresh 期的 fresh 设备列表）—— 本方法<b>不自行</b>触发全图扫描，
        /// 避免玩家点一次「确定」就隐式跑一次重活（PERF-001）。</para>
        /// </summary>
        /// <param name="device">目标设备。</param>
        /// <param name="rawName">玩家输入的原始名（未归一化）；空/纯空白 = 清除。</param>
        /// <param name="comparisonSet">同图待比较的设备列表（可 null = 跳过重名校验）。</param>
        public DeviceRenameOutcome TryRenameDevice(Thing device, string rawName, List<Thing> comparisonSet)
        {
            if (device == null || device.Destroyed)
            {
                return DeviceRenameOutcome.DeviceGone;
            }
            WorkshopDeviceLedger ledger = DeviceLedger;
            if (ledger == null || !ledger.IsTracked(device))
            {
                return DeviceRenameOutcome.NotTracked;
            }
            string normalized = PersonalChronicle.Domain.WorkshopDeviceNaming.Normalize(rawName);
            if (!PersonalChronicle.Domain.WorkshopDeviceNaming.IsWithinLength(normalized))
            {
                return DeviceRenameOutcome.TooLong;
            }
            // 重名校验：清空（normalized == null）不受限（O-2 裁决）。
            if (normalized != null && comparisonSet != null)
            {
                for (int i = 0; i < comparisonSet.Count; i++)
                {
                    Thing other = comparisonSet[i];
                    if (other == null || other == device)
                    {
                        continue;
                    }
                    if (PersonalChronicle.Domain.WorkshopDeviceNaming.Conflicts(
                            normalized, EffectiveNameOf(other)))
                    {
                        return DeviceRenameOutcome.Duplicate;
                    }
                }
            }
            ledger.SetCustomNameOf(device, normalized);
            return DeviceRenameOutcome.Ok;
        }

        /// <summary>
        /// 设备运行时长采样（2026-09-25）：每个检查间隔对全部车间设备判一次"运行中"
        /// （口径与设备页/稼动率同源：WorkshopDeviceStateResolver），命中即累计间隔 tick。
        /// 设备清单按车间缓存（每 20 个间隔重建一次），避免每间隔全车间扫格。
        /// </summary>
        private void AccumulateDeviceRuntime(List<Zone_Workshop> zones)
        {
            int today = GenDate.DaysPassed;
            // CHG-20261005-TRENDBOARD：本轮"游戏内小时"取一次，供车间 7×24 波峰采样。
            // 与离班/排班判定同源（GenLocalDate.HourOfDay）—— 不另写换算，避免与班次口径漂移。
            int hour = GenLocalDate.HourOfDay(map);
            if (today != lastLedgerDay)
            {
                lastLedgerDay = today;
                DeviceLedger.RollDay(today);
                deviceScanCache.Clear();
            }
            // CHG-20261004-PERF：改接参（原自行调 AllWorkshopZones ⇒ 每轮多一次分配+全表扫描）。
            // 2026-10-02 审计修复（P1-D）：收集本轮实际在场设备，循环末尾据此淘汰台账条目
            //（旧实现只增不删 ⇒ 无界增长；读档后旧 ID 条目会挂错设备）。
            deviceScanIds.Clear();
            // 2026-10-02 审计修复（P2-1）：设备清单缓存按注释语义**每 20 个间隔重建**
            //（20 游戏秒）—— 原实现只在跨日清空，注释与实现漂移，导致当日新放置 / 拆除的设备
            // 对运行时长与产出**滞后可达一游戏日**。
            if (++deviceScanTick >= DeviceScanRebuildIntervals)
            {
                deviceScanTick = 0;
                deviceScanCache.Clear();
            }
            for (int z = 0; z < zones.Count; z++)
            {
                Zone_Workshop zone = zones[z];
                List<Thing> devices = DevicesCached(zone);
                for (int i = 0; i < devices.Count; i++)
                {
                    Thing device = devices[i];
                    if (device == null || device.Destroyed)
                    {
                        continue;
                    }
                    // 2026-10-02：无论是否在运行都先登记条目 —— 产量归账以「条目是否存在」判定
                    // 该工作台是否属于车间（WorkshopDeviceLedger.IsTracked）；若只在"运行中"才建条目，
                    // 一台已被扫描、但整轮都没被判为运行中的设备会漏记首批产出。
                    deviceScanIds.Add(device.thingIDNumber);
                    // CHG-20261005-DEVCOLS：「建立时间」= 台账首次看到它的时刻（MarkSeen 幂等，
                    // 只记第一次；旧档既有条目在升级后首轮扫描补记"现在"，之后固定不变）。
                    // 每设备一次 Find.TickManager 访问可忽略（设备数有界、本路径每 60 tick 一轮）。
                    DeviceLedger.Touch(device, Find.TickManager.TicksGame);
                    if (WorkshopDeviceStateResolver.IsRunning(WorkshopDeviceStateResolver.KeyOf(device)))
                    {
                        DeviceLedger.AddRunTicks(device, CheckIntervalTicks);
                        // CHG-20261005-TRENDBOARD（2026-10-05 玩家批准候选 3/4）：逐台逐日机时。
                        // 与上一行**同一次**"运行中"判定，不重复判态 —— 两处各判一次就会因
                        // 缓存状态差异出现"总量 ≠ 逐日之和"的自相矛盾读数（UI-009 可自证）。
                        DeviceLedger.AddRunTicksOnDay(device, today, CheckIntervalTicks);
                        // 候选 1：车间级 7×24 波峰 —— 每台运行设备各贡献一个采样间隔
                        // ⇒ 该小时的值 = 本车间所有设备在该小时的合计机时。
                        zone.AddHourlyRun(today, hour, CheckIntervalTicks);
                    }
                }
            }
            // 2026-10-02 审计修复（P1-D）：淘汰本轮不在场的设备条目 —— 有界性闭合，
            // 且读档后旧 ID 条目（若 ID 重分配假设成立）会在首轮扫描被清除，不会挂错设备。
            DeviceLedger.PruneNotIn(deviceScanIds);
        }
        /// <summary>
        /// 设备/工单变化 → 触发「车间专属工位」重算（CHG-20261003 / EXC-2026-015 · L1）。
        ///
        /// <para><b>为什么这里不是"在 tick 里写"</b>：本方法只做<b>只读指纹比对</b>并置一个
        /// <b>脏标记</b>；真正的 <c>workSettings</c> 写入由 <see cref="SyncWorkStationIfDirty"/>
        /// 在<b>本方法之外</b>执行（见其注释的触发边界）。这与 EXC-2026-013 「禁 tick 写
        /// workSettings」的区分点在于：<b>tick 内不产生任何 SetPriority 调用</b>。</para>
        ///
        /// <para><b>指纹口径</b>：各车间「设备数 + 设备 defName 的顺序无关哈希 + BillStack 条数」。
        /// 覆盖：设备放置/拆除/替换（进出车间）、工单增删。三者任一变化即置脏。</para>
        /// </summary>
        private void DetectDeviceChannelChange(List<Zone_Workshop> zones)
        {
            long fingerprint = 1469598103934665603L; // FNV-1a 64 偏移基
            for (int z = 0; z < zones.Count; z++)
            {
                Zone_Workshop zone = zones[z];
                if (!WorkshopZoneUtility.IsAlive(zone))
                {
                    continue;
                }
                // 2026-10-04 审计 P3：改用同一轮内的共享缓存（本方法紧随
                // AccumulateDeviceRuntime 之后被调用，缓存此刻必然已填好），
                // 消除每车间每轮重复的全格扫描。
                List<Thing> devices = DevicesCached(zone);
                if (devices == null)
                {
                    continue;
                }
                fingerprint = (fingerprint ^ devices.Count) * 1099511628211L;
                for (int i = 0; i < devices.Count; i++)
                {
                    Thing device = devices[i];
                    if (device == null || device.def == null)
                    {
                        continue;
                    }
                    foreach (char ch in device.def.defName)
                    {
                        fingerprint = (fingerprint ^ ch) * 1099511628211L;
                    }
                    if (device is IBillGiver bg && bg.BillStack != null)
                    {
                        fingerprint = (fingerprint ^ bg.BillStack.Count) * 1099511628211L;
                    }
                }
            }
            if (deviceChannelFingerprint == 0L)
            {
                // 首次建立基线（**不**置脏）：无历史可比，不该在启动瞬间触发一轮写入。
                deviceChannelFingerprint = fingerprint;
                return;
            }
            if (deviceChannelFingerprint != fingerprint)
            {
                deviceChannelFingerprint = fingerprint;
                deviceChannelDirty = true;
            }
        }
        private List<Zone_Workshop> AllWorkshopZones()
        {
            var result = new List<Zone_Workshop>();
            List<Zone> all = map.zoneManager.AllZones;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is Zone_Workshop workshop && workshop.Cells != null)
                {
                    result.Add(workshop);
                }
            }
            return result;
        }

        // ---- 查询 ---------------------------------------------------------------

        /// <summary>
        /// 该 pawn 按固定班 / 班组 / 轮岗现在是否应当上岗（不看是否已到场）。
        /// O(1)：查统一调度计划（<see cref="dutyPlan"/>）。
        /// </summary>
        public bool IsScheduledForDuty(Pawn pawn)
        {
            return DutyZoneFor(pawn) != null;
        }

        /// <summary>
        /// 该 pawn 命中的目标车间。优先级：<b>固定班 &gt; 班组 &gt; 轮岗</b>；
        /// 同类之间按 <c>Priority</c> 大者优先，同一 pawn 只归一个岗位。
        /// O(1)：命中缓存好的统一调度计划（失效时机见 <see cref="dutyPlan"/> 注释）。
        /// </summary>
        public Zone_Workshop DutyZoneFor(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned)
            {
                return null;
            }
            EnsureDutyPlan();
            Zone_Workshop zone;
            return dutyPlan.TryGetValue(pawn, out zone) ? zone : null;
        }

        // ---- 统一调度计划 -------------------------------------------------------

        /// <summary>标记计划失效（排班增删改、读档、轮岗推进，以及每个检查间隔的兜底）。</summary>
        public void MarkDutyPlanDirty()
        {
            dutyPlanDirty = true;
        }

        private void PruneScheduleHistory()
        {
            int today = GenDate.DaysPassed;
            List<Zone> allZones = map.zoneManager.AllZones;
            for (int i = 0; i < allZones.Count; i++)
            {
                Zone_Workshop workshop = allZones[i] as Zone_Workshop;
                if (workshop != null)
                {
                    workshop.PruneScheduleHistory(today);
                }
            }
        }

        // ---- 加成查询（热路径只做字典取值，零解析）----------------------------

        /// <summary>
        /// 当前在岗人数（运行态字典的计数视图；供 KPI 摘要与 UI 使用 —— 2026-09-22 指标重拟）。
        /// O(1)。
        /// </summary>
        public int ActiveWorkerCount => activeWorkers.Count;

        /// <summary>
        /// 该 pawn 当前作息表里被标成 <c>Work</c> 的小时数（2026-10-03 · GATE-UI 可观测性）。
        ///
        /// <para><b>为什么需要它</b>：本工程把班次写进<b>原版作息表</b>，而作息表
        /// <b>随存档持久化</b>（Cecil 取证：<c>Pawn_TimetableTracker.ExposeData</c> 里有
        /// <c>Scribe_Collections.Look(ref times, "times", …)</c>）⇒ 一旦被写坏，玩家侧只看到
        /// "他不睡觉"，**看不到作息表被写成什么样**，导致三次修复都只能靠推测。
        /// 本读数把"作息表实际状态"变成可核对的一行字，是该类问题的自证入口。</para>
        ///
        /// <para>复杂度 O(24)，供 Refresh 期读数，<b>不得</b>进绘制热路径。</para>
        /// </summary>
        public static int WorkHoursOf(Pawn pawn)
        {
            if (pawn == null || pawn.timetable == null)
            {
                return 0;
            }
            int n = 0;
            for (int h = 0; h < 24; h++)
            {
                if (pawn.timetable.GetAssignment(h) == TimeAssignmentDefOf.Work)
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// 「幽灵在岗」人数（2026-10-03）：已登记在 <c>activeWorkers</c>、但**既不在当前小时的
        /// <c>dutyPlan</c> 里、也没有正在跑的值勤 Job**的人数。
        ///
        /// <para><b>为什么要有这个读数</b>（用户实机「到点了还卡在值勤」）：幽灵条目会让在岗 KPI 虚高，
        /// 并且 <c>TryGetDutyJob</c> 的 <c>activeWorkers.ContainsKey</c> 早退会让该 pawn
        /// <b>再也拿不到值勤 Job</b>。低频对账会自愈（<c>SelfHealGhostDuty</c>），
        /// 本属性把"有幽灵"这件事暴露到 UI，让它不再静默。</para>
        ///
        /// <para>复杂度 O(在岗人数)，仅供 Refresh 期读数，<b>不得</b>进绘制热路径。</para>
        /// </summary>
        public int GhostDutyCount
        {
            get
            {
                if (activeWorkers.Count == 0)
                {
                    return 0;
                }
                EnsureDutyPlan();
                int ghosts = 0;
                foreach (KeyValuePair<Pawn, Zone_Workshop> pair in activeWorkers)
                {
                    Pawn pawn = pair.Key;
                    if (pawn == null)
                    {
                        continue;
                    }
                    Job cur = pawn.CurJob;
                    bool onDutyJob = cur != null && cur.def != null
                        && cur.def.defName == DutyJobDefName;
                    if (!onDutyJob && !dutyPlan.ContainsKey(pawn))
                    {
                        ghosts++;
                    }
                }
                return ghosts;
            }
        }

        /// <summary>
        /// 当前小时「应到岗」人数（<c>dutyPlan</c> 规模，O(1)）。
        /// 与 <see cref="ActiveWorkerCount"/> 并列显示即可看出"该来的人没来 / 不该来的人还在"。
        /// </summary>
        public int DutyPlanCount
        {
            get
            {
                EnsureDutyPlan();
                return dutyPlan != null ? dutyPlan.Count : 0;
            }
        }

        /// <summary>该 pawn 当前上岗的车间（未上岗 → null）。O(1) 字典取值。</summary>
        public Zone_Workshop ActiveZoneOf(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }
            Zone_Workshop zone;
            return activeWorkers.TryGetValue(pawn, out zone) ? zone : null;
        }

        /// <summary>
        /// 该 pawn 的车间加成快照（未上岗/未缓存 → 中性加成，零影响）。
        /// StatPart 与生产挂载点一律走这里。
        /// </summary>
        public WorkshopEffectiveBonus BonusOf(Pawn pawn)
        {
            // 2026-09-26 加成体系恢复（专向风格固定 buff）：上岗时由 RecalcBonusForZone
            // 写入缓存，这里只读缓存；未在册 → 中性（与"上岗才吃加成"的设计一致）。
            if (pawn == null)
            {
                return WorkshopEffectiveBonus.Neutral;
            }
            WorkshopEffectiveBonus bonus;
            return bonusCache.TryGetValue(pawn, out bonus) ? bonus : WorkshopEffectiveBonus.Neutral;
        }

        // ---- 「工作时」加成（2026-09-25 加成体系移除）------------------------
        // 原「工作时」加成缓存（WorkingBonusEntry / workingBonusCache）已随加成体系停用移除：
        // WorkingBonusOf 恒返回中性，无需在岗缓存。保留 workingBonusCache 的清理位（下方
        // PurgeDeadReferences）一并移除。

        /// <summary>
        /// 该 pawn「当前工作所在」的车间：<b>在岗车间优先</b>，否则其**物理位置**所在的车间。
        ///
        /// <para><b>为什么需要它（2026-09-11 体检 P0）</b>：生产加成过去只认 `activeWorkers`
        /// （值勤登记），而值勤 Job 是"站岗（Never 完成）"、执行配方时必须先收工
        /// （收工即清缓存）⇒ **"在岗"与"生产"互斥**，产出类六个因子在正常玩法下不可达。
        /// 现按蓝图 §7.2 的更宽松语义扩展：站在车间里干活也算"在车间环境加成下操作"。</para>
        /// </summary>
        public Zone_Workshop WorkingZoneOf(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return null;
            }
            Zone_Workshop duty = ActiveZoneOf(pawn);
            Zone_Workshop zone = duty ?? WorkshopZoneUtility.WorkshopAt(pawn.Position, pawn.Map);
            return zone != null && zone.WorkshopType != null ? zone : null;
        }

        /// <summary>
        /// 该 pawn 的「工作时加成」。2026-09-25 重设为<b>专向风格固定 buff</b>：
        /// 车间类型声明 workSpeedBonusPct / qualityBonus / researchSpeedBonusPct，
        /// 由 <see cref="WorkshopBonusResolver.Resolve"/> 直接换算（无技能缩放，显示=生效）。
        /// 解析极轻量（仅算术、无遍历/反射/分配），故不在每帧热路径缓存。
        /// </summary>
        public WorkshopEffectiveBonus WorkingBonusOf(Pawn pawn)
        {
            Zone_Workshop zone = WorkingZoneOf(pawn);
            if (zone == null || zone.WorkshopType == null)
            {
                return WorkshopEffectiveBonus.Neutral;
            }
            return WorkshopBonusResolver.Resolve(zone.WorkshopType, pawn);
        }

        /// <summary>
        /// 该车间当前的<b>吞吐加成</b>（物流车间 <c>throughputFactor</c>）：取本车间在岗人员
        /// 已缓存值中的最大值；无人在岗 → 1（无加成，与"上岗才吃加成"的既有设计一致）。
        ///
        /// <para>存在意义：传送带等<b>无操作者设备</b>无法走 <see cref="BonusOf"/>（按 pawn 缓存），
        /// 但仍需按所属车间消费吞吐因子 —— 这是 <c>throughputFactor</c> 的落地消费点。
        /// 性能：O(在岗人数) 且只比大小；调用点必须低频（传送带每次 TickInterval 一次）。</para>
        /// </summary>
        public float ThroughputFactorOf(Zone_Workshop zone)
        {
            // 2026-09-26 加成体系恢复：取本车间在岗人员缓存值中的最大吞吐因子。
            if (zone == null || activeWorkers.Count == 0)
            {
                return 1f;
            }
            float best = 1f;
            foreach (KeyValuePair<Pawn, Zone_Workshop> pair in activeWorkers)
            {
                if (pair.Value != zone)
                {
                    continue;
                }
                WorkshopEffectiveBonus bonus;
                if (bonusCache.TryGetValue(pair.Key, out bonus) && bonus.throughputFactor > best)
                {
                    best = bonus.throughputFactor;
                }
            }
            return best;
        }

        /// <summary>
        /// 重算某车间内全部在岗人员的加成（车间类型被改时调用）。
        /// 只遍历该车间的在岗人员，规模 = 车间人数，开销可忽略。
        /// </summary>
        public void RecalcBonusForZone(Zone_Workshop zone)
        {
            if (zone == null)
            {
                return;
            }
            foreach (KeyValuePair<Pawn, Zone_Workshop> pair in activeWorkers)
            {
                if (pair.Key != null && pair.Value == zone)
                {
                    bonusCache[pair.Key] = WorkshopBonusResolver.Resolve(zone.WorkshopType, pair.Key);
                }
            }
        }

        // ---- 排班与轮岗的增删（管理面板调用）--------------------------------------

        public void AddSlot(WorkshopSlot slot)
        {
            if (slot != null && !slots.Contains(slot))
            {
                slots.Add(slot);
                MarkDutyPlanDirty();
            }
        }

        public void RemoveSlot(WorkshopSlot slot)
        {
            if (slot != null && slots.Remove(slot))
            {
                MarkDutyPlanDirty();
            }
        }

        public void AddRotation(RotationRule rule)
        {
            if (rule != null && !rotations.Contains(rule))
            {
                if (rule.NextRotateTick <= 0)
                {
                    rule.NextRotateTick = Find.TickManager.TicksGame + rule.IntervalTicks;
                }
                rotations.Add(rule);
                MarkDutyPlanDirty();
            }
        }

        public void RemoveRotation(RotationRule rule)
        {
            if (rule != null && rotations.Remove(rule))
            {
                MarkDutyPlanDirty();
            }
        }

        /// <summary>新增班组班次（名额制排班）。顺带分配持久化 ID（<see cref="WorkshopShift.LoadId"/>）。</summary>
        public void AddShift(WorkshopShift shift)
        {
            if (shift != null && !shifts.Contains(shift))
            {
                if (shift.LoadId < 0)
                {
                    shift.LoadId = NextShiftLoadId();
                }
                shifts.Add(shift);
                MarkDutyPlanDirty();
            }
        }

        /// <summary>
        /// 下一个可用的班次 ID（2026-10-02）：当前全部班次的最大值 +1；空列表从 1 开始。
        /// 只在「新增班次」与「读档补号」时调用（低频），不做缓存以免与载入顺序打架。
        /// </summary>
        private int NextShiftLoadId()
        {
            int max = 0;
            if (shifts != null)
            {
                for (int i = 0; i < shifts.Count; i++)
                {
                    WorkshopShift shift = shifts[i];
                    if (shift != null && shift.LoadId > max)
                    {
                        max = shift.LoadId;
                    }
                }
            }
            return max + 1;
        }

        /// <summary>按持久化 ID 找班次（班组指派的解析入口）；找不到返回 null。</summary>
        public WorkshopShift ShiftById(int loadId)
        {
            if (loadId < 0 || shifts == null)
            {
                return null;
            }
            for (int i = 0; i < shifts.Count; i++)
            {
                WorkshopShift shift = shifts[i];
                if (shift != null && shift.LoadId == loadId)
                {
                    return shift;
                }
            }
            return null;
        }

        /// <summary>
        /// 读档补齐（2026-10-02）：① 给缺失 ID 的班次编号；② 把各班组的
        /// <c>AssignedShiftIds</c> 解析回 <c>AssignedShifts</c> 引用列表。
        ///
        /// <para>必须在 <c>PostLoadInit</c> 调用（此时班次列表与班组都已加载完毕）。
        /// 幂等，可重复调用。</para>
        ///
        /// <para>为什么不在 <see cref="WorkshopCrew.ExposeData"/> 里做：那时 <c>LoadingVars</c> 阶段
        /// 本管理器的班次列表还没加载，按 ID 找不到对象。</para>
        /// </summary>
        public void EnsureShiftIdsAndAssignments()
        {
            if (shifts == null)
            {
                shifts = new List<WorkshopShift>();
            }
            for (int i = 0; i < shifts.Count; i++)
            {
                WorkshopShift shift = shifts[i];
                if (shift != null && shift.LoadId < 0)
                {
                    shift.LoadId = NextShiftLoadId();
                }
            }
            // 反向重建引用列表：ID 是唯一真值来源（AssignedShifts 不持久化）。
            List<Zone> allZones = map != null && map.zoneManager != null ? map.zoneManager.AllZones : null;
            if (allZones == null)
            {
                return;
            }
            for (int z = 0; z < allZones.Count; z++)
            {
                Zone_Workshop zone = allZones[z] as Zone_Workshop;
                if (zone == null || zone.Crews == null)
                {
                    continue;
                }
                for (int c = 0; c < zone.Crews.Count; c++)
                {
                    WorkshopCrew crew = zone.Crews[c];
                    if (crew == null)
                    {
                        continue;
                    }
                    if (crew.AssignedShifts == null)
                    {
                        crew.AssignedShifts = new List<WorkshopShift>();
                    }
                    else
                    {
                        crew.AssignedShifts.Clear();
                    }
                    if (crew.AssignedShiftIds == null)
                    {
                        continue;
                    }
                    for (int s = 0; s < crew.AssignedShiftIds.Count; s++)
                    {
                        WorkshopShift shift = ShiftById(crew.AssignedShiftIds[s]);
                        if (shift != null && !crew.AssignedShifts.Contains(shift))
                        {
                            crew.AssignedShifts.Add(shift);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 移除班组班次，并<b>解除各班组对它的显式指派</b>。
        ///
        /// <para>2026-10-02（班次表功能）：旧实现只从 <see cref="shifts"/> 里删对象，
        /// 班组 <c>AssignedShifts</c> 里的引用会残留 —— 表现为"班次已删，但班组卡片仍显示它"
        /// （<c>PrimaryShiftOf</c> 直接读指派列表），以及读档时产生 null 项。
        /// 清理由班次归属的车间反向完成，UI 侧只发一条命令。</para>
        ///
        /// <para>2026-10-02 修复：改走 <see cref="WorkshopCrew.UnassignShift"/> ——
        /// 它会<b>同时</b>清理引用列表与持久化 ID 列表，否则存档里会残留悬空 ID。</para>
        /// </summary>
        public void RemoveShift(WorkshopShift shift)
        {
            if (shift == null || !shifts.Remove(shift))
            {
                return;
            }
            Zone_Workshop owner = shift.TargetZone;
            if (owner != null && owner.Crews != null)
            {
                List<WorkshopCrew> crews = owner.Crews;
                for (int c = 0; c < crews.Count; c++)
                {
                    if (crews[c] != null)
                    {
                        crews[c].UnassignShift(shift);
                    }
                }
            }
            MarkDutyPlanDirty();
        }

        /// <summary>立即执行一次轮岗（管理面板「立即轮岗」按钮）。</summary>
        public void ForceRotation(RotationRule rule)
        {
            if (rule == null)
            {
                return;
            }
            rule.Advance();
            rule.NextRotateTick = Find.TickManager.TicksGame + rule.IntervalTicks;
            MarkDutyPlanDirty();
        }

        /// <summary>
        /// 引用清理：pawn 死亡/删除、车间被删后，槽位与轮岗池里的悬空引用全部剔除。
        /// LookMode.Reference 读档后死亡 pawn 会置 null，车间删除则 Map 变 null。
        /// </summary>
        public void PurgeDeadReferences()
        {
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                WorkshopSlot slot = slots[i];
                if (slot == null || slot.AssignedPawn == null || slot.AssignedPawn.Destroyed
                    || !WorkshopZoneUtility.IsAlive(slot.TargetZone))
                {
                    slots.RemoveAt(i);
                }
            }
            for (int i = rotations.Count - 1; i >= 0; i--)
            {
                RotationRule rule = rotations[i];
                if (rule == null || !WorkshopZoneUtility.IsAlive(rule.TargetZone))
                {
                    rotations.RemoveAt(i);
                    continue;
                }
                if (rule.Pool != null)
                {
                    rule.Pool.RemoveAll(p => p == null || p.Destroyed);
                }
                // 下标一律按「有效池」校正（体检 P2-⑨）：池被清空 → 回落车间名单 → 从首位重新开始。
                // 旧代码按自定义池长度夹取，池打空时"夹取基准 ≠ 实际取值来源"。
                rule.ClampActiveIndex();
            }
            for (int i = shifts.Count - 1; i >= 0; i--)
            {
                WorkshopShift shift = shifts[i];
                if (shift == null || !WorkshopZoneUtility.IsAlive(shift.TargetZone))
                {
                    shifts.RemoveAt(i);
                    continue;
                }
                if (shift.Pool != null)
                {
                    shift.Pool.RemoveAll(p => p == null || p.Destroyed);
                }
            }
            // 在岗名单里的死亡 pawn 一并剔除。
            List<Pawn> stale = null;
            foreach (KeyValuePair<Pawn, Zone_Workshop> pair in activeWorkers)
            {
                if (pair.Key == null || pair.Key.Destroyed || !pair.Key.Spawned)
                {
                    if (stale == null)
                    {
                        stale = new List<Pawn>();
                    }
                    stale.Add(pair.Key);
                }
            }
            if (stale != null)
            {
                for (int i = 0; i < stale.Count; i++)
                {
                    activeWorkers.Remove(stale[i]);
                    bonusCache.Remove(stale[i]);
                    // 2026-10-02 审计修复：考勤起点同样要清 —— 它原先只在 NotifyDutyEnded 里删，
                    // 死亡/离图的成员会永久残留在该字典中（有界性漏洞），且条目会随 Pawn 引用一起滞留。
                    dutyStartTicks.Remove(stale[i]);
                }
            }
            // 站位格缓存同样要清（体检 P2-⑧）：它是以 Zone_Workshop 为键的强引用，
            // 此前 purge 不处理 ⇒ 玩家删车间后缓存条目永久滞留。
            if (dutySpots.Count > 0)
            {
                List<Zone_Workshop> staleSpots = null;
                foreach (KeyValuePair<Zone_Workshop, IntVec3> pair in dutySpots)
                {
                    if (!WorkshopZoneUtility.IsAlive(pair.Key))
                    {
                        if (staleSpots == null)
                        {
                            staleSpots = new List<Zone_Workshop>();
                        }
                        staleSpots.Add(pair.Key);
                    }
                }
                if (staleSpots != null)
                {
                    for (int i = 0; i < staleSpots.Count; i++)
                    {
                        dutySpots.Remove(staleSpots[i]);
                    }
                }
            }
            // 班组编制的死引用（企业化排班，2026-09-11）：车间还在、组员已死/被删 ⇒ 从编制剔除。
            List<Zone> allZones = map.zoneManager.AllZones;
            for (int i = 0; i < allZones.Count; i++)
            {
                Zone_Workshop workshop = allZones[i] as Zone_Workshop;
                if (workshop == null)
                {
                    continue;
                }
                List<WorkshopCrew> crews = workshop.Crews;
                for (int c = 0; c < crews.Count; c++)
                {
                    if (crews[c] != null)
                    {
                        crews[c].PurgeDead();
                    }
                }
                // 单一编制兜底（2026-09-26）：「一人多组」的旧会话遗留数据在周期清理路径自愈
                //（读档去重只覆盖载入场景；不重开存档也要能修）。
                workshop.DedupeCrewMemberships();
            }
            // 2026-10-02 审计修复（P1-A）：旧实现在这里**无条件** MarkDutyPlanDirty()，而本方法由
            // MapComponentTick 每 60 tick 调用一次 ⇒ 调度计划每游戏秒被强制重建，
            // ReconcileDutyPlan 的「可观测失配才标脏」被完全抵消（PERF-003 声明失真）。
            // 现在不再在此标脏：真正影响计划的死引用（pawn 死亡 / 车间删除）都会被
            // ReconcileDutyPlan 的失配检测观测到（IsDutyEligible / IsAlive），读档路径则由
            // ExposeData 自行无条件标脏 —— 本方法两种调用路径都不需要它。
        }

        // ---- 内部 ---------------------------------------------------------------

        private void ProcessRotations()
        {
            if (rotations.Count == 0)
            {
                return;
            }
            int now = Find.TickManager.TicksGame;
            int hour = GenLocalDate.HourOfDay(map);
            for (int i = 0; i < rotations.Count; i++)
            {
                RotationRule rule = rotations[i];
                // 门槛必须看「有效池」而不是「自定义池」（体检 P1-B1）：
                // Pool 留空 = 回落车间人员名单（RotationRule.EffectivePool），而旧代码读
                // rule.Pool.Count < 2 —— 从未填充过的池恒为 0 ⇒ 定时轮岗永不推进，
                // 表现为"轮岗退化成名单首位永久值班"，只有「立即轮岗」能换人。
                List<Pawn> pool = rule != null ? rule.EffectivePool : null;
                if (pool == null || pool.Count < 2)
                {
                    continue;
                }
                if (!WorkshopHours.InRange(hour, rule.StartHour, rule.EndHour))
                {
                    // 班次外重置换班时钟：进班时从零开始计时，避免追帧连跳。
                    if (rule.NextRotateTick < now)
                    {
                        rule.NextRotateTick = now + rule.IntervalTicks;
                    }
                    continue;
                }
                if (now < rule.NextRotateTick)
                {
                    continue;
                }
                rule.Advance();
                rule.NextRotateTick = now + rule.IntervalTicks;
                MarkDutyPlanDirty();   // 换人后立即重算上岗名单（否则要等一个检查间隔）
            }
        }

        /// <summary>车间岗位站位格：区内第一个可站立格（实例级缓存）。</summary>
        private IntVec3 GetDutySpot(Zone_Workshop zone)
        {
            if (zone == null || zone.Map == null)
            {
                return IntVec3.Invalid;
            }
            Map map = zone.Map;
            if (dutySpots.TryGetValue(zone, out IntVec3 cached)
                && cached.IsValid
                && cached.InBounds(map)
                && cached.Standable(map)
                && WorkshopZoneUtility.WorkshopAt(cached, map) == zone)
            {
                return cached;
            }
            List<IntVec3> cells = zone.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].Standable(map))
                {
                    dutySpots[zone] = cells[i];
                    return cells[i];
                }
            }
            return IntVec3.Invalid;
        }
    }
}
