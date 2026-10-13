using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间区：玩家用设计器圈定的一片区域，作为自动化设备的归属边界。
    ///
    /// 第一版只负责「划定 + 命名 + 查询」，不含任何生产逻辑、不 tick、不存额外字段
    /// （存档完全走 <see cref="Zone"/> 基类：label / color / cells / hidden）。
    ///
    /// <para><b>⭐ 车间格抗占位</b>：本类 override <see cref="RemoveCell"/>，使车间的格**不会**
    /// 因被设备/墙体/地板/货架占用而被原版掉格（详见该成员注释与 EXC-2026-003）。
    /// 没有这条，放置设备会把车间抠出空洞、切断后整块消失，甚至让车间被自动注销。</para>
    /// 后续加工设备与传送带通过 <see cref="WorkshopZoneUtility.WorkshopAt"/> 判定所属车间，
    /// 这里刻意不引用任何尚未实现的类型，避免半成品时期的循环依赖。
    /// </summary>
    public class Zone_Workshop : Zone
    {
        /// <summary>
        /// 工业色板。多个车间并排时靠色相区分；alpha 复用原版 zone 的透明度常量，
        /// 保证与仓库区/种植区的观感一致。
        /// </summary>
        private static readonly Color[] Palette =
        {
            new Color(0.95f, 0.62f, 0.20f),
            new Color(0.86f, 0.45f, 0.25f),
            new Color(0.78f, 0.72f, 0.30f),
            new Color(0.55f, 0.76f, 0.45f),
            new Color(0.45f, 0.70f, 0.82f),
            new Color(0.70f, 0.55f, 0.78f)
        };

        /// <summary>
        /// 原版 zone 的填充透明度。<c>ZoneColorUtility.ZoneOpacity</c> 对外不可访问，
        /// 这里取与原版一致的经验值（肉眼与仓库区/种植区无差别）。
        /// </summary>
        private const float ZoneOpacity = 0.15f;

        /// <summary>
        /// 色板索引单调自增计数（**ARC-008 受控静态状态**，2026-09-21 补标记 ·
        /// CHG-SHOP-AUDIT-001 · A-6）：仅在 <see cref="NextZoneColor"/> 里读一次/每次创建；
        /// 不持久化、不入 Scribe、无跨档语义（读档后重新自增，撞色属可接受损失）；
        /// 有界性 = int 自增（溢出周期不可达）。
        /// </summary>
        private static int nextColorIndex;

        /// <summary>Scribe 反序列化专用构造，不要手动调用。</summary>
        public Zone_Workshop()
        {
        }

        public Zone_Workshop(string baseLabel, ZoneManager zoneManager)
            : base(baseLabel, zoneManager)
        {
        }

        /// <summary>
        /// <see cref="Zone"/> 唯一的 abstract 成员，只在 zone 创建时读取一次（基类
        /// 构造里赋给 <c>color</c>）。色板索引是单调自增的静态计数，不持久化 ——
        /// 这与原版 <c>ZoneColorUtility</c> 的行为一致，读档后新建车间可能撞色，
        /// 属于可接受损失（颜色不影响任何逻辑）。
        /// </summary>
        protected override Color NextZoneColor
        {
            get
            {
                Color c = Palette[nextColorIndex % Palette.Length];
                nextColorIndex++;
                c.a = ZoneOpacity;
                return c;
            }
        }

        /// <summary>
        /// 车间覆盖层的渲染队列。原版所有 zone（<see cref="Zone"/> 的 <c>CreateMaterial</c>）与
        /// 区域高亮（<c>Verse.CellBoolDrawer</c>）都用 <b>3600</b>；同队列内 Unity 按相机距离排序，
        /// 并列时退化为提交顺序 —— 而 zone 的 submesh 是按 <c>Verse.SectionLayer_Zones</c> 的
        /// **栅格扫描首次命中顺序**逐个创建的（每材质一次独立 <c>Graphics.DrawMesh</c>）
        /// ⇒ 谁先被扫到谁在下面，与玩家圈定顺序无关，视觉不稳定。
        ///
        /// <para>提高到 3601 让车间覆盖层**恒定压在其它 zone / 区域之上**：车间与种植区、
        /// 仓库区等重叠时（<c>ZoneManager.AddZoneGridCell</c> 单格被后来的 zone 覆盖，但先来者
        /// 的 <c>cells</c> 仍含该格 ⇒ 两层叠加绘制）仍能看清车间范围；同时低于 Unity Overlay
        /// 队列（4000）与所有 UI，不会盖住角色/物品/界面。</para>
        /// </summary>
        private const int ZoneOverlayRenderQueue = 3601;

        /// <summary>
        /// 覆盖层材质：与原版同一着色器、同一套颜色量化（<c>(Color32)color</c>），但改用
        /// <see cref="SolidColorMaterials.NewSolidColorMaterial"/> 建**独立材质**（不走
        /// <c>SimpleSolidColorMaterial</c> 的按色共享缓存）再抬高 renderQueue ——
        /// 避免污染被其它 zone 共用的池化材质（那些材质原版自己会写成 3600）。
        /// </summary>
        protected override Material CreateMaterial()
        {
            Color quantized = (Color32)color;
            Material material = SolidColorMaterials.NewSolidColorMaterial(quantized, ShaderDatabase.SolidColor);
            material.renderQueue = ZoneOverlayRenderQueue;
            return material;
        }

        public override string GetInspectString()
        {
            string own = "PersonalChronicle.Automation.Zone.Inspect".Translate(CellCount).ToString();
            string inherited = base.GetInspectString();
            // 基类会输出火灾等状态警告，必须保留而不是覆盖。
            return string.IsNullOrEmpty(inherited) ? own : inherited + "\n" + own;
        }

        /// <summary>
        /// 功能键契约。基类 <see cref="Zone.GetGizmos"/> 默认已提供「重命名 / 隐藏 /
        /// 删除 / 缩小区域 / 智能选中」等全部 zone 通用按钮，车间通过继承自动拿到
        /// —— 截图验证右下角（Y 缩小区域、X 删除）与右上角小图标（重命名）均生效。
        ///
        /// 本 override 的目的不是重新实现，而是**显式占位扩展点**：未来车间专属 gizmo
        /// （区内设备统计、配方选择、批量补料/出料）一律在此处 <c>yield return</c>，
        /// 禁止散落到其它位置。
        /// </summary>
        /// <summary>
        /// 管理键的实例级缓存。
        ///
        /// 检视面板在选中期间<b>每帧</b>调用 <see cref="GetGizmos"/>，若每次都 new
        /// 会形成持续 GC 分配——表现为选中区域瞬间掉帧。gizmo 无随选中变化的状态，
        /// 缓存单例安全。
        /// </summary>
        private Command_Action manageGizmo;

        /// <summary>
        /// 车间人员名单：**玩家手动指派**，不是检测进入 zone 的 pawn。
        /// UI（管理面板人员页）的添加/移除都经 <see cref="AddMember"/> /
        /// <see cref="RemoveMember"/>；随存档持久化。
        /// </summary>
        private List<Pawn> members = new List<Pawn>();

        /// <summary>手动指派的车间人员名单（可能含已死亡占位，渲染前清理）。</summary>
        public List<Pawn> Members => members;

        /// <summary>
        /// CHG-20261004-POSTOVERRIDE · 玩家手动岗位覆盖：pawn ThingID → 岗位 defName。
        ///
        /// <para><b>口径（2026-10-04 玩家决策）</b>：<b>先由系统自动分配，玩家再手动覆盖。</b>
        /// 本字典只存「玩家显式指定过」的项；缺项 ⇒ 跟随 <c>WorkshopPostAssigner</c> 的自动结果。
        /// 这样未指定者行为完全不变，不破坏现有平衡。</para>
        ///
        /// <para><b>刻意只影响岗位显示，不影响工作类型。</b>玩家的口径是
        /// 「玩家只分配岗位，系统自动分配工作类型」——工作类型由班次的
        /// <c>shift.TaskType</c> 经 <c>WorkTypeDefNamesOf</c> 决定，与本字典<b>不得</b>耦合，
        /// 否则手动改岗会连带改掉玩家没打算改的格位。</para>
        ///
        /// <para>用 ThingID 字符串键（Value 模式）而非 Pawn 引用：跨档稳定、无需处理
        /// 已卸载 pawn 的引用失效。键清洗见 <see cref="ExposeData"/> 的 PostLoadInit。</para>
        /// </summary>
        private Dictionary<string, string> postOverrides = new Dictionary<string, string>();

        /// <summary>取该成员的玩家手动岗位 defName；null = 未覆盖（跟随自动）。</summary>
        public string GetPostOverride(Pawn pawn)
        {
            if (pawn == null || pawn.ThingID.NullOrEmpty() || postOverrides == null) { return null; }
            return postOverrides.TryGetValue(pawn.ThingID, out string defName)
                ? (defName.NullOrEmpty() ? null : defName)
                : null;
        }

        /// <summary>
        /// 设置 / 清除该成员的岗位覆盖（管理面板人员页调用）。
        /// <paramref name="postDefName"/> 传 null 或空串 = 清除覆盖、回到「跟随自动」。
        /// </summary>
        public void SetPostOverride(Pawn pawn, string postDefName)
        {
            if (pawn == null || pawn.ThingID.NullOrEmpty()) { return; }
            if (postOverrides == null) { postOverrides = new Dictionary<string, string>(); }
            if (postDefName.NullOrEmpty())
            {
                postOverrides.Remove(pawn.ThingID);
            }
            else
            {
                postOverrides[pawn.ThingID] = postDefName;
            }
        }

        /// <summary>
        /// CHG-20261004-POSTOVERRIDE · 读档自愈：丢弃不可用的岗位覆盖项。
        ///
        /// <para><b>三种失效</b>：① 键或值为空（脏写入）；② 岗位 Def 已不可解析
        /// （第三方移除 / 改名）—— 丢弃后该成员自动回落到分配器结果，不留死引用；
        /// ③ 人已离开车间名单或已死亡 —— 覆盖项已无意义，不清理会让存档单调膨胀。</para>
        ///
        /// <para>读档路径每档至多一次，属低频操作（与本工程 Attendance/Performance 的
        /// <c>Prune</c> 同一档位）。</para>
        /// </summary>
        private void PrunePostOverrides()
        {
            if (postOverrides == null || postOverrides.Count == 0) { return; }
            List<string> doomed = null;
            foreach (KeyValuePair<string, string> kv in postOverrides)
            {
                bool drop = kv.Key.NullOrEmpty() || kv.Value.NullOrEmpty();
                if (!drop)
                {
                    drop = WorkshopPostCatalog.PostOf(kv.Value) == null;
                }
                if (!drop)
                {
                    Pawn found = null;
                    for (int i = 0; i < members.Count; i++)
                    {
                        Pawn m = members[i];
                        if (m != null && m.ThingID == kv.Key) { found = m; break; }
                    }
                    drop = found == null || found.Dead || found.Destroyed;
                }
                if (drop)
                {
                    if (doomed == null) { doomed = new List<string>(); }
                    doomed.Add(kv.Key);
                }
            }
            if (doomed == null) { return; }
            for (int i = 0; i < doomed.Count; i++)
            {
                postOverrides.Remove(doomed[i]);
            }
        }

        /// <summary>
        /// 车间类型（蓝图 WorkshopTypeDef，落地管线 Step 3）。
        /// null = 尚未指定类型 → 不提供任何加成。
        /// </summary>
        private WorkshopTypeDef workshopType;

        /// <summary>Scribe 用类型 defName（Def 按 defName 解析，不直接序列化实例）。</summary>
        private string workshopTypeDefName;

        /// <summary>本车间的类型（未指定为 null）。</summary>
        public WorkshopTypeDef WorkshopType => workshopType;

        /// <summary>设置/清空车间类型（管理面板调用）。</summary>
        public void SetWorkshopType(WorkshopTypeDef def)
        {
            workshopType = def;
            workshopTypeDefName = def != null ? def.defName : null;
        }

        /// <summary>
        /// CHG-20261004-AUTOTYPE-FIX：玩家手动点过「清除类型」⇒ 不再自动回设。
        ///
        /// <para><b>为什么必须落盘</b>（2026-10-04 审计 P1-2）：该语义最初写成管理面板的
        /// <c>WorkshopManageView</c> 实例字段，而 <c>Dialog_WorkshopManage</c> <b>每次开窗都
        /// new 一个 View</b> ⇒ 玩家关窗再开，抑制丢失、类型被自动判定设回去，
        /// 与界面承诺（"清除"）直接矛盾。<b>玩家意图属于车间数据，不属于某次会话的视图状态</b>。</para>
        ///
        /// <para><b>加性可选字段</b>：旧档没有本节点 ⇒ 读回 <c>false</c>（= 允许自动判定），
        /// 与旧行为一致 ⇒ <b>不需要 bump SchemaVersion、不需要迁移</b>（SAVE-002）。</para>
        /// </summary>
        private bool autoTypeSuppressed;

        /// <summary>玩家是否已手动清除并拒绝自动回设（见字段注释）。</summary>
        public bool AutoTypeSuppressed
        {
            get { return autoTypeSuppressed; }
            set { autoTypeSuppressed = value; }
        }

        /// <summary>
        /// 本车间是否已执行过**一次自动接管**（CHG-20261005-TAKEOVERUI；2026-10-05 玩家裁定
        /// 「接管原版工作默认开启」）。
        ///
        /// <para><b>为什么是"一次性"而不是"每次都跑"</b>：<c>TakeOverVanillaWork</c> 只跳过
        /// 档位已为 0 的项，**不区分"PC 取消的"与"玩家刚手动勾回的"** ⇒ 若每次都跑，
        /// 玩家勾回的项会在下次开面板被再次取消，「解除接管」形同虚设。
        /// 因此：首次（新建车间 / 读档后第一次打开面板）自动接管一次并置本标记，之后完全由玩家掌控。</para>
        ///
        /// <para><b>与「名册变更自动接管」并存</b>：<c>WorkshopManageView.OnRosterChanged</c> 那条路径
        /// （成员编入班组时自动接管）<b>不受本标记限制</b> —— 名册真的变了，新人需要被处理。</para>
        ///
        /// <para><b>加性可选字段</b>：旧档缺键读回 <c>false</c> ⇒ 读档后首次开面板补做一次，
        /// 与"新车间"同行为 ⇒ 不需要 Migration（SAVE-004）。</para>
        /// </summary>
        private bool autoTakeoverDone;

        /// <summary>是否已执行过一次性自动接管（见字段注释）。</summary>
        public bool AutoTakeoverDone
        {
            get { return autoTakeoverDone; }
            set { autoTakeoverDone = value; }
        }

        /// <summary>
        /// 班组编制（企业化排班，2026-09-11）：轮班作业组列表，按
        /// <see cref="PersonalChronicle.Domain.WorkshopCrewCycle"/> 的倒班表轮换班次。
        /// 空 = 该车间不启用班组轮转（沿用自定义池/人员名单的手工排班）。
        /// </summary>
        private List<WorkshopCrew> crews = new List<WorkshopCrew>();

        /// <summary>倒班相位（天）：允许不同车间错峰交接班；0 = 与游戏日同步。</summary>
        private int cycleAnchorDay;

        /// <summary>
        /// 本车间采用的作息策略 Def 名（CHG-20261003-WORKSHOP-SPLIT · 修成因 A）。
        ///
        /// <para><b>加性可选字段</b>：<c>null</c> = 用缺省档（
        /// <see cref="WorkshopRestPolicyDef.DefaultPolicyDefName"/>，即 FixedPerShift）。
        /// <b>旧存档没有这个节点</b>，读档后即为缺省行为（= 新建车间），
        /// 因此<b>不需要 bump SchemaVersion、也不需要迁移</b>（SAVE-002）。</para>
        ///
        /// <para><b>为什么按 Def 名而不是枚举存档</b>：GOV-009 只认稳定 ID；
        /// Def 被第三方移除时 <see cref="WorkshopDutyCoordinator.PolicyOf"/> 按名查不到
        /// ⇒ 回落缺省，<b>不抛异常、不丢档</b>（读档路径硬要求）。</para>
        /// </summary>
        private string restPolicyDefName;

        /// <summary>本车间的作息策略 Def 名（null = 缺省档；见字段注释）。</summary>
        public string RestPolicyDefName
        {
            get { return restPolicyDefName; }
            set { restPolicyDefName = value; }
        }

        /// <summary>班组编制（可能为空列表；非 null）。</summary>
        public List<WorkshopCrew> Crews
        {
            get
            {
                if (crews == null)
                {
                    crews = new List<WorkshopCrew>();
                }
                return crews;
            }
        }

        /// <summary>倒班相位（天）；UI 可调，用于错峰。</summary>
        public int CycleAnchorDay
        {
            get { return cycleAnchorDay; }
            set { cycleAnchorDay = value; }
        }

        // ═══════════ CHG-20261005-TRENDBOARD：设备使用波峰 7×24（2026-10-05 玩家批准候选 1）═══════════
        //
        // 概览页「大组件」首个图表的数据源：回答"这个车间一天里的**什么时候**最忙"。
        //
        // 为什么放车间（而不是全局台账）：波峰是**空间属性**（哪个车间、哪些设备、几点忙），
        // 而台账（WorkshopDeviceLedger）是 MapComponent 级的全图容器 ⇒ 只有 Zone 能区分车间。
        //
        // 存储：7 行 × 24 列 = 168 个 int（单位 = 采样累计 tick，显示时除 GenDate.TicksPerHour）。
        //   · 行 = 绝对天 mod 7（0..6），每行记自己的绝对天（hourlyRowDays）用于校验；
        //   · 列 = 游戏内小时（GenLocalDate.HourOfDay，0..23）。
        // ⇒ 与台账「逐日窗」同一条不变式：**写入按 mod 定位、读取按槽内自带的天过滤**，
        //   跨日自然滚动、旧档缺省为空（PostLoadInit 补齐）⇒ 加性可选字段，无需迁移（SAVE-004）。
        // 有界性：168 + 7 个 int 恒定，不随游戏时长增长（审计 PERF 红线）。

        /// <summary>趋势窗天数（= 台账 <c>DeviceStatEntry.ProducedWindowDays</c> 的 7，两者同窗才能按天对齐）。</summary>
        public const int TrendDayCount = 7;

        /// <summary>一天的小时数（绘制列数；用 GenDate.HoursPerDay 同值常量，避免两处 24 漂移）。</summary>
        public const int TrendHourCount = 24;

        /// <summary>7 行 × 24 列的机时采样（槽 = 行 mod 7 × 24 + 小时）。</summary>
        private List<int> hourlyRunTicks = new List<int>();

        /// <summary>每行当前对应的绝对天（校验用；不匹配 = 该行为空槽）。</summary>
        private List<int> hourlyRowDays = new List<int>();

        /// <summary>
        /// 累加一次运行采样（由 MapComponent 每 60 tick 对**每台运行中设备**各调一次
        /// ⇒ 该小时的值 = 本车间所有设备在该小时的合计机时）。
        /// </summary>
        public void AddHourlyRun(int day, int hour, long ticks)
        {
            if (ticks <= 0L || hour < 0 || hour >= TrendHourCount)
            {
                return;
            }
            int row = RowOf(day);
            EnsureHourly();
            if (hourlyRowDays[row] != day)
            {
                // 该行还停留在上一轮（7 天前）⇒ 整行清零再启用，避免旧数据混入新一天。
                int from = row * TrendHourCount;
                for (int i = 0; i < TrendHourCount; i++)
                {
                    hourlyRunTicks[from + i] = 0;
                }
                hourlyRowDays[row] = day;
            }
            int slot = row * TrendHourCount + hour;
            long sum = hourlyRunTicks[slot] + ticks;
            hourlyRunTicks[slot] = sum > int.MaxValue ? int.MaxValue : (int)sum;
        }

        /// <summary>指定绝对天 + 小时的机时（采样 tick；无记录 ⇒ 0）。</summary>
        public int HourlyRunAt(int day, int hour)
        {
            if (hour < 0 || hour >= TrendHourCount)
            {
                return 0;
            }
            EnsureHourly();
            int row = RowOf(day);
            return hourlyRowDays[row] == day ? hourlyRunTicks[row * TrendHourCount + hour] : 0;
        }

        /// <summary>环形行号（含负天守卫；与台账 SlotOf 同构）。</summary>
        private static int RowOf(int day)
        {
            int row = day % TrendDayCount;
            return row < 0 ? row + TrendDayCount : row;
        }

        /// <summary>补齐 7×24 缓冲（幂等；读档后由 ExposeData 的 PostLoadInit 调用）。</summary>
        private void EnsureHourly()
        {
            if (hourlyRunTicks == null)
            {
                hourlyRunTicks = new List<int>();
            }
            if (hourlyRowDays == null)
            {
                hourlyRowDays = new List<int>();
            }
            int total = TrendDayCount * TrendHourCount;
            while (hourlyRunTicks.Count < total)
            {
                hourlyRunTicks.Add(0);
            }
            while (hourlyRowDays.Count < TrendDayCount)
            {
                hourlyRowDays.Add(int.MinValue);   // 永不等于任何真实天数 ⇒ 空行
            }
        }

        /// <summary>
        /// 趋势数据是否已有任何采样（概览页空态判定用：全空 ⇒ 显示"暂无运行采样"，
        /// 不画一张全暗的网格假装有数据 —— LAND-009 诚实口径）。
        /// </summary>
        public bool HasHourlyTrend()
        {
            EnsureHourly();
            for (int i = 0; i < hourlyRunTicks.Count; i++)
            {
                if (hourlyRunTicks[i] > 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>新增班组（自动按 A/B/C… 命名，重名则顺延）。</summary>
        public WorkshopCrew AddCrew()
        {
            List<WorkshopCrew> list = Crews;
            int index = list.Count;
            string name;
            do
            {
                name = ((char)('A' + index % 26)).ToString();
                index++;
            }
            while (HasCrewNamed(name) && index < 1000);
            var crew = new WorkshopCrew(name);
            list.Add(crew);
            return crew;
        }

        public void RemoveCrew(WorkshopCrew crew)
        {
            if (crew != null && Crews.Remove(crew))
            {
                // 轮转相位保持不变：班组数变化只影响后续轮转，不需补偿。
            }
        }

        /// <summary>
        /// 把人员编入班组（<b>单一编制不变式</b>）：一人同时只能属一个班组 ——
        /// 编入目标组前先从其它班组移出。
        /// <para>背景（实机 2026-09-25）：Cella 同时挂在 B/C 两组 —— 旧入口只做"本组查重"，
        /// 而倒班轮转会把同一人按两组口径重复排班 / 重复考勤。</para>
        /// </summary>
        public void AssignPawnToCrew(WorkshopCrew target, Pawn pawn)
        {
            if (target == null || pawn == null)
            {
                return;
            }
            List<WorkshopCrew> list = Crews;
            for (int i = 0; i < list.Count; i++)
            {
                WorkshopCrew crew = list[i];
                if (crew != null && !ReferenceEquals(crew, target))
                {
                    crew.Remove(pawn);
                }
            }
            target.Add(pawn);
        }

        /// <summary>
        /// 旧存档修复：清除「一人多组」的历史脏数据（每人保留<b>最先编入</b>的班组）。
        /// 读档（<c>PostLoadInit</c>）路径调用；写入口已由 <see cref="AssignPawnToCrew"/> 堵住。
        /// </summary>
        public void DedupeCrewMemberships()
        {
            List<WorkshopCrew> list = Crews;
            HashSet<Pawn> seen = new HashSet<Pawn>();
            for (int i = 0; i < list.Count; i++)
            {
                WorkshopCrew crew = list[i];
                if (crew == null || crew.Members == null)
                {
                    continue;
                }
                for (int j = crew.Members.Count - 1; j >= 0; j--)
                {
                    Pawn pawn = crew.Members[j];
                    if (pawn == null || !seen.Add(pawn))
                    {
                        crew.Members.RemoveAt(j);
                    }
                }
            }
        }

        private bool HasCrewNamed(string name)
        {
            List<WorkshopCrew> list = Crews;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].Name == name)
                {
                    return true;
                }
            }
            return false;
        }

        // ---- 倒班例外（调休 / 请假 / 顶班）与考勤（Phase 2，2026-09-11）--------

        private List<WorkshopCrewException> crewExceptions = new List<WorkshopCrewException>();
        private WorkshopAttendance attendance = new WorkshopAttendance();
        private WorkshopPerformance performance = new WorkshopPerformance();

        /// <summary>人均日目标产量（生产率分母）：100% = 一天内每人产出该件数。玩家可调。</summary>
        private int perMemberDailyTarget = 10;

        /// <summary>倒班例外表（只存例外项；正常班表不占存档）。</summary>
        public List<WorkshopCrewException> CrewExceptions
        {
            get
            {
                if (crewExceptions == null)
                {
                    crewExceptions = new List<WorkshopCrewException>();
                }
                return crewExceptions;
            }
        }

        /// <summary>考勤台账（实际人时 / 加班人时，按日累计）。</summary>
        public WorkshopAttendance Attendance
        {
            get
            {
                if (attendance == null)
                {
                    attendance = new WorkshopAttendance();
                }
                return attendance;
            }
        }

        /// <summary>按人绩效台账（在岗人时 / 产量件数，按 pawn × 日累计），班组绩效区块读取。</summary>
        public WorkshopPerformance Performance
        {
            get
            {
                if (performance == null)
                {
                    performance = new WorkshopPerformance();
                }
                return performance;
            }
        }

        /// <summary>人均日目标产量（生产率分母，默认 10 件/人/日）。</summary>
        public int PerMemberDailyTarget
        {
            get
            {
                return perMemberDailyTarget <= 0 ? 10 : perMemberDailyTarget;
            }
            set
            {
                perMemberDailyTarget = Mathf.Clamp(value, 1, 999);
            }
        }

        /// <summary>某天某班组的例外类型（0 = 无；见 <see cref="PersonalChronicle.Domain.WorkshopCrewCycle"/>）。</summary>
        public int ExceptionKindOf(int day, int crewIndex)
        {
            List<WorkshopCrewException> list = CrewExceptions;
            for (int i = 0; i < list.Count; i++)
            {
                WorkshopCrewException item = list[i];
                if (item != null && item.Day == day && item.CrewIndex == crewIndex)
                {
                    return item.Kind;
                }
            }
            return PersonalChronicle.Domain.WorkshopCrewCycle.ExceptionNone;
        }

        /// <summary>点格子循环：正常 → 请假 → 顶班 → 正常（返回新类型）。</summary>
        public int CycleException(int day, int crewIndex)
        {
            int next = PersonalChronicle.Domain.WorkshopCrewCycle.NextException(ExceptionKindOf(day, crewIndex));
            List<WorkshopCrewException> list = CrewExceptions;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                WorkshopCrewException item = list[i];
                if (item == null || (item.Day == day && item.CrewIndex == crewIndex))
                {
                    list.RemoveAt(i);
                }
            }
            if (next != PersonalChronicle.Domain.WorkshopCrewCycle.ExceptionNone)
            {
                list.Add(new WorkshopCrewException { Day = day, CrewIndex = crewIndex, Kind = next });
            }
            return next;
        }

        /// <summary>把某天的例外摊成"班组序号 → 类型"数组（供 Domain 纯函数消费）。</summary>
        public int[] ExceptionKindsFor(int day, int crewCount)
        {
            var kinds = new int[crewCount < 0 ? 0 : crewCount];
            List<WorkshopCrewException> list = CrewExceptions;
            for (int i = 0; i < list.Count; i++)
            {
                WorkshopCrewException item = list[i];
                if (item != null && item.Day == day && item.CrewIndex >= 0 && item.CrewIndex < kinds.Length)
                {
                    kinds[item.CrewIndex] = item.Kind;
                }
            }
            return kinds;
        }

        /// <summary>裁剪过期例外（保留昨天起的记录）与过期考勤。</summary>
        public void PruneScheduleHistory(int today)
        {
            List<WorkshopCrewException> list = CrewExceptions;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                WorkshopCrewException item = list[i];
                if (item == null || item.Day < today - 1)
                {
                    list.RemoveAt(i);
                }
            }
            Attendance.Prune(today);
            Performance.Prune(today);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref members, "members", LookMode.Reference);
            Scribe_Values.Look(ref workshopTypeDefName, "workshopTypeDefName");
            // CHG-20261004-AUTOTYPE-FIX：玩家清除类型后的抑制标志（加性可选：缺键 = false，
            // 旧档读回即"允许自动判定"，与旧行为一致 ⇒ 无需迁移）。
            Scribe_Values.Look(ref autoTypeSuppressed, "autoTypeSuppressed", false);
            // CHG-20261005-TAKEOVERUI：一次性自动接管标记（加性可选字段：缺键 = false
            // ⇒ 读档后首次开面板补做一次，与"新车间"同行为 ⇒ 无需迁移，SAVE-004）。
            Scribe_Values.Look(ref autoTakeoverDone, "autoTakeoverDone", false);
            Scribe_Collections.Look(ref crews, "crews", LookMode.Deep);
            Scribe_Values.Look(ref cycleAnchorDay, "cycleAnchorDay", 0);
            // CHG-20261003-WORKSHOP-SPLIT：作息策略（加性可选字段，缺省 = FixedPerShift）。
            Scribe_Values.Look(ref restPolicyDefName, "restPolicyDefName");
            Scribe_Collections.Look(ref crewExceptions, "crewExceptions", LookMode.Deep);
            Scribe_Deep.Look(ref attendance, "attendance");
            Scribe_Deep.Look(ref performance, "performance");
            Scribe_Values.Look(ref perMemberDailyTarget, "perMemberDailyTarget", 10);
            // CHG-20261004-POSTOVERRIDE：手动岗位覆盖（加性可选字段，缺省 = 空 = 全跟随自动）。
            // Value 模式 + ThingID 字符串键：跨档稳定，不需要处理已卸载 pawn 的引用失效。
            Scribe_Collections.Look(ref postOverrides, "pcPostOverrides", LookMode.Value);
            // CHG-20261005-TRENDBOARD：7×24 设备使用波峰（加性可选字段：缺省为空列表
            // ⇒ 旧档照常加载，PostLoadInit 补齐 168+7 槽 ⇒ 无需 bump SchemaVersion、无需迁移，SAVE-004）。
            Scribe_Collections.Look(ref hourlyRunTicks, "pcHourlyRunTicks", LookMode.Value);
            Scribe_Collections.Look(ref hourlyRowDays, "pcHourlyRowDays", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (postOverrides == null)
                {
                    postOverrides = new Dictionary<string, string>();
                }
                PrunePostOverrides();
                EnsureHourly();
            }
            {
                if (members == null)
                {
                    members = new List<Pawn>();
                }
                if (crews == null)
                {
                    crews = new List<WorkshopCrew>();
                }
                // 旧存档修复（2026-09-25）：清除「一人多组」历史脏数据（写入口已堵住）。
                DedupeCrewMemberships();
                if (crewExceptions == null)
                {
                    crewExceptions = new List<WorkshopCrewException>();
                }
                if (attendance == null)
                {
                    attendance = new WorkshopAttendance();
                }
                if (performance == null)
                {
                    performance = new WorkshopPerformance();
                }
                workshopType = string.IsNullOrEmpty(workshopTypeDefName)
                    ? null
                    : DefDatabase<WorkshopTypeDef>.GetNamedSilentFail(workshopTypeDefName);
                // 2026-10-02 审计（P2-13）：解析失败此前**完全静默** ⇒ 第三方 Def 被移除 /
                // defName 拼错时，车间类型无声丢失（该车间所有加成失效，界面无任何线索）。
                // 读档路径每档至多一次，属低频诊断信息。
                if (!string.IsNullOrEmpty(workshopTypeDefName) && workshopType == null)
                {
                    Log.Warning("[PersonalChronicle][Automation] Workshop zone references an unknown "
                        + "WorkshopTypeDef (workshop type / all bonuses disabled): " + workshopTypeDefName);
                }
                // 自持格集是派生数据、不入存档：读档后强制重建（cells 由 Scribe 直接灌入，
                // 不会走 AddCell hook）。
                ownedCells.Clear();
                indexedCellCount = -1;
            }
        }

        public void AddMember(Pawn pawn)
        {
            if (pawn != null && !members.Contains(pawn))
            {
                members.Add(pawn);
            }
        }

        public void RemoveMember(Pawn pawn)
        {
            members.Remove(pawn);
        }

        // ---- 车间自持格集（归属判定不依赖原版 zoneGrid 反查，见 EXC-2026-005）------------

        /// <summary>
        /// 车间成员格的 O(1) 快表。**派生数据，不入存档** —— 权威仍是基类
        /// <c>Zone.cells</c>（它本身就"自持"：别的 zone 覆盖格子网格时只改
        /// <c>ZoneManager.zoneGrid</c> 的指针，**不会**动我们的 <c>cells</c> 列表）。
        ///
        /// <para>存在意义：原版 <c>ZoneManager.ZoneAt(cell)</c> 是"最后圈定者胜"，
        /// 因此仓库区/种植区压在车间之上时，<c>ZoneAt</c> 会返回那些 zone
        /// ⇒ 用 <c>ZoneAt</c> 反查车间归属的代码（<c>WorkshopZoneUtility.WorkshopAt</c>、
        /// 车间存活判定）会对重叠格失效。自持格集让归属判定只问车间自己。</para>
        /// </summary>
        private readonly HashSet<IntVec3> ownedCells = new HashSet<IntVec3>();

        /// <summary>
        /// 索引已同步到的 <c>cells.Count</c>；**负值 = 索引未建/已失效**。
        /// 作为廉价一致性检查：任何没走 hook 的批量变更（Scribe 读档直接灌 <c>cells</c>、
        /// 原版 rebuild）都会造成计数不一致 ⇒ 下次查询自动重建，自愈且无需 Harmony。
        /// </summary>
        private int indexedCellCount = -1;

        /// <summary>
        /// 该格是否属于本车间（**自持判定**，与 <c>ZoneManager.ZoneAt</c> 无关，O(1)）。
        /// 注意与基类 <c>Zone.ContainsCell</c> 的区别：后者是 O(cells) 线性扫描，
        /// 而归属于设备扫描/岗位探测会被反复查询。
        /// </summary>
        public bool OwnsCell(IntVec3 c)
        {
            EnsureOwnedCellsCurrent();
            return ownedCells.Contains(c);
        }

        /// <summary>
        /// 原版 <c>AddCell</c> 完成后同步索引。基类在"格已被 zone 不可重叠物占用"或
        /// "重复加格"时会提前 return（计数不变），故用 <c>cells.Count</c> 前后比较
        /// 判成功（O(1)，不额外做线性 Contains）。
        /// </summary>
        public override void AddCell(IntVec3 c)
        {
            int countBefore = cells.Count;
            base.AddCell(c);
            if (cells.Count > countBefore)
            {
                NotifyCellAdded(c, countBefore);
            }
        }

        /// <summary>重建自持格集（仅在计数不一致时触发，O(cells)）。</summary>
        private void EnsureOwnedCellsCurrent()
        {
            if (indexedCellCount == cells.Count)
            {
                return;
            }
            ownedCells.Clear();
            for (int i = 0; i < cells.Count; i++)
            {
                ownedCells.Add(cells[i]);
            }
            indexedCellCount = cells.Count;
        }

        /// <summary>增量同步：仅当索引原本与变更前计数一致时才可信，否则标记失效待重建。</summary>
        private void NotifyCellAdded(IntVec3 c, int countBefore)
        {
            if (indexedCellCount != countBefore)
            {
                indexedCellCount = -1;
                return;
            }
            ownedCells.Add(c);
            indexedCellCount = cells.Count;
        }

        /// <summary>见 <see cref="NotifyCellAdded"/>（对称实现）。</summary>
        private void NotifyCellRemoved(IntVec3 c, int countBefore)
        {
            if (indexedCellCount != countBefore)
            {
                indexedCellCount = -1;
                return;
            }
            ownedCells.Remove(c);
            indexedCellCount = cells.Count;
        }

        // ---- 车间格抗占位（受控偏离，见 docs/设计文档/架构/EXC-2026-003.md）--------------

        /// <summary>
        /// 显式删除放行位：<see cref="Delete(bool)"/> 与 <see cref="RemoveCellExplicitly"/> 期间为 true。
        /// 非持久化（瞬时状态，读档后必为 false ⇒ 默认恢复"抗占位"拦截）。
        /// </summary>
        private bool allowCellRemoval;

        /// <summary>
        /// ⭐ 车间格抗占位：**只在"本格被 zone 不可重叠物占用"这一种情形拒绝掉格**，其余情形
        /// 完全保持原版行为（等价于让车间区拥有"活动区"那样的抗占位属性）。
        ///
        /// <para><b>为什么需要</b>：原版 <c>Thing.SpawnSetup</c> 末尾
        /// <c>if (!def.CanOverlapZones) map.zoneManager.Notify_NoZoneOverlapThingSpawned(this)</c>
        /// 会对该 Thing 的 <c>OccupiedRect()</c> 每格调 <see cref="Zone.RemoveCell"/>，再
        /// <c>Zone.CheckContiguous()</c>。而 <c>ThingDef.CanOverlapZones</c> 对这些情形为 false：
        /// ① <c>building.SupportsPlants</c>；② <c>passability == Impassable</c>（非植物/非 CompTransporter）；
        /// ③ <c>surfaceType >= 1</c>（地板/铺面）；④ <c>thingClass</c> 实现 <c>ISlotGroupParent</c>（货架等）；
        /// ⑤ 显式 <c>&lt;canOverlapZones&gt;false&lt;/canOverlapZones&gt;</c>（**原版全部工作台继承的
        /// <c>BenchBase</c> 正是这一条**）。蓝图/框架再委托给 <c>entityDefToBuild</c>。</para>
        ///
        /// <para><b>不拦的后果（已实测）</b>：车间被设备/墙/地板抠出空洞；被切成两半时
        /// <c>CheckContiguous</c> 会把不与 <c>cells[0]</c> 连通的另一半整块删掉；全被吃光时
        /// <c>Zone.RemoveCell</c> 内的 <c>cells.Count == 0 → Deregister()</c> 让整个车间消失；
        /// 同时 <c>ZoneAt(device.Position)</c> 不再命中 ⇒ 设备归属、车间统计、俯视图一并失效。</para>
        ///
        /// <para><b>本 override 为何足够</b>：原版所有掉格路径都是 <c>callvirt Zone.RemoveCell</c>
        /// （<c>Zone.Delete</c> / <c>Zone.CheckContiguous</c> /
        /// <c>ZoneManager.Notify_NoZoneOverlapThingSpawned</c> / <c>WaterBodyTracker</c> /
        /// <c>Designator_ZoneDelete</c> / <c>GravshipPlacementUtility</c>），虚派发必然命中本类，
        /// 无需 Harmony。删除整区与玩家擦格走 <see cref="CanDropCell"/> 的放行分支，故不影响
        /// 原生「删除区域 / 缩小区域」工具。</para>
        ///
        /// <para><b>已知取舍</b>：① 车间格现在可以存在被建筑占用的格（与"zone 只含空闲格"的原版
        /// 不变式有意偏离）；② 水体/飞船着陆清场若落在"被占用的车间格"上会被拒绝（边缘情形）；
        /// ③ <c>Zone.AddCell</c> 仍会拒绝把已被占用的格划入（<c>cellsShuffled</c> 为 private、
        /// <c>ZoneManager.AddZoneGridCell</c> 为 internal ⇒ 子类无法重写绕过），故"先放设备
        /// 后划车间"时那几格仍进不来，正确顺序是先划区、后放设备。</para>
        /// </summary>
        public override void RemoveCell(IntVec3 c)
        {
            if (!CanDropCell(c))
            {
                return;
            }
            int countBefore = cells.Count;
            base.RemoveCell(c);
            if (cells.Count < countBefore)
            {
                NotifyCellRemoved(c, countBefore);
            }
        }

        /// <summary>
        /// 删除整区：临时放行抗占位拦截。原版 <c>Delete</c> 逐格 <c>RemoveCell</c> 直到空，
        /// 若不放行，被设备/地板压住的格子删不掉 ⇒ 留下删不净的残块。
        /// </summary>
        public override void Delete(bool playSound)
        {
            allowCellRemoval = true;
            try
            {
                base.Delete(playSound);
            }
            finally
            {
                allowCellRemoval = false;
            }
            // ⚠ NODEMOTE-AUDIT（P1 修复）：**删车间必须触发一次成员差集对账**。
            //   为什么：对账原本只挂在面板 Refresh / OnRosterChanged 上，而 Delete 是原版路径
            //   （全无 Manager 通知）⇒ 删区后成员虽已不在册，但若玩家**没开面板就直接存档**，
            //   运行态快照 lastWorkshopMembers 不入档 ⇒ 读档后差集基准被重置 ⇒
            //   这批人的自动隐藏与接管**永久无从还原**（差集再也命中不到他们）。
            //   这里是玩家动作路径（非 tick、非绘制），语义与"开一次面板"完全一致。
            //   ⚠ 放在 finally 之后：此时 zone 已真正从 ZoneManager 移除，
            //     成员口径（AllWorkshopMemberPawns 遍历 AllWorkshopZones）自然已把它排除。
            MapComponent_WorkshopManager.Get(Map)?.ReconcileAutoHideOnLeave();
        }

        /// <summary>
        /// 程序化显式移除单格（放行抗占位拦截）。车间缩格/搬迁等自研逻辑一律走这里，
        /// 禁止直接 <c>base.RemoveCell</c> 之外的隐式路径。
        /// </summary>
        public void RemoveCellExplicitly(IntVec3 c)
        {
            allowCellRemoval = true;
            try
            {
                // 走本类 override（而非 base）以确保自持格集同步更新。
                RemoveCell(c);
            }
            finally
            {
                allowCellRemoval = false;
            }
        }

        /// <summary>
        /// 该格是否允许从车间移除。
        /// 放行：① 我方显式删除；② 玩家正用原版「删除区域/缩小区域」设计器擦格。
        /// 其余情形仅当格上**没有** zone 不可重叠物时才放行（= 只有原版"设备占格"通知被拦）。
        /// </summary>
        private bool CanDropCell(IntVec3 c)
        {
            if (allowCellRemoval || IsErasingZonesByPlayer())
            {
                return true;
            }
            return !HasZoneBlockingThing(c);
        }

        /// <summary>
        /// 格上是否存在让 zone 无法重叠的物。判据与原版 <c>ThingDef.CanOverlapZones</c> 同源
        /// （直接问 def，不自行复制那 5 条规则，避免版本漂移）。
        /// </summary>
        private bool HasZoneBlockingThing(IntVec3 c)
        {
            Map map = Map;
            if (map == null || !c.InBounds(map))
            {
                return false;
            }
            List<Thing> things = map.thingGrid.ThingsListAt(c);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing != null && thing.def != null && !thing.def.CanOverlapZones)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 玩家是否正在用原版区域删除设计器擦格。用**当前设计器类型**判定而非监听事件：
        /// 原版擦格只发生在该设计器被选中并拖拽期间（<c>Designator_ZoneDelete_Shrink</c>
        /// 是它的子类，一并覆盖）。开销 = 一次类型判断，只在 RemoveCell 调用时执行。
        /// </summary>
        private static bool IsErasingZonesByPlayer()
        {
            DesignatorManager manager = Find.DesignatorManager;
            return manager != null && manager.SelectedDesignator is Designator_ZoneDelete;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            // 车间管理键：唯一的车间专属按钮，打开管理面板。
            if (manageGizmo == null)
            {
                manageGizmo = new Command_Action
                {
                    defaultLabel = "PersonalChronicle.Automation.Zone.Manage".Translate().ToString(),
                    defaultDesc = "PersonalChronicle.Automation.Zone.ManageDesc".Translate().ToString(),
                    icon = TexButton.OpenStatsReport,
                    action = OpenManageDialog
                };
            }
            yield return manageGizmo;
        }

        /// <summary>
        /// 快照在点击瞬间算好再交给窗口，窗口不持有活的 zone 引用 ——
        /// 这样玩家在面板打开期间删除车间也不会读到已销毁对象。
        /// </summary>
        private void OpenManageDialog()
        {
            if (Map == null)
            {
                return;
            }
            // 开窗去重：同一车间只保留一个管理面板，重复点击改为置顶既有窗口。
            // Find.WindowStack.Windows 是现成列表，不做分配。
            IList<Window> windows = Find.WindowStack.Windows;
            for (int i = 0; i < windows.Count; i++)
            {
                if (windows[i] is Dialog_WorkshopManage existing && existing.OwnerZone == this)
                {
                    Find.WindowStack.TryRemove(existing, false);
                    break;
                }
            }
            // 直接把活引用交给弹窗：管理台需要「刷新」反映开窗期间的新设备；
            // 车间被删除时由窗口自行检测并关闭（view.IsZoneAlive）。
            Find.WindowStack.Add(new Dialog_WorkshopManage(this));
        }

        /// <summary>
        /// 检视 Tab 的<b>静态空集合</b>。本方法是每帧入口（选中期间检视面板逐帧枚举），
        /// 返回常量数组而非 <c>Enumerable.Empty</c> / <c>yield break</c>，避免每帧
        /// 分配迭代器对象（项目掉帧教训：每帧入口禁止 new 重对象）。
        /// </summary>
        private static readonly InspectTabBase[] NoTabs = new InspectTabBase[0];

        /// <summary>
        /// 车间<b>不提供任何检视 Tab</b>（PM 拍板：管理入口只有 gizmo 弹窗）。
        ///
        /// ⚠️ 必须显式 override，不能"不写"从而透传基类：
        /// <b>1.6 根本没有 <c>ZoneDef</c> 类型</b>（程序集扫描 <c>ZoneDef</c> 命中数 0，
        /// 全量原版 Def 亦无 ZoneDef 节点；检视 Tab 是硬编码类如 <c>ITab_Storage</c>，
        /// 不是 Def 驱动）。因此"配个 ZoneDef 就能修"是伪方案。
        /// 基类 <see cref="Zone.GetInspectTabs"/> 的返回值可能为 <c>null</c>，而检视面板
        /// 会对其直接 <c>foreach</c> → 选中车间后每帧 NRE（Player.log 铁证：面板空白
        /// + FPS 骤降）。显式返回空集合一次性根除该风险，且不依赖任何基类行为。
        /// </summary>
        public override IEnumerable<InspectTabBase> GetInspectTabs()
        {
            return NoTabs;
        }
    }
}
