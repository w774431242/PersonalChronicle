using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间管理内容层 —— <c>Dialog_WorkshopManage</c>（大型弹窗）的布局实现。
    ///
    /// <para>⚠ CHG-20261005-WSDOCFIX：原文写"与 <c>ITab_WorkshopManage</c>（检视 Tab）共用"
    /// —— 该类**全工程无定义**（只有本处与 <c>Dialog_WorkshopManage</c> 两处 cref 引用它），
    /// 属已删除功能的残留引用；同时"概览/设备/配方/设置"是旧 4 页描述，
    /// **现为 7 页**（概览 / 设备 / 人员 / 排班 / 班次表 / 岗位 / 设置，见 <c>Page</c> 枚举）。</para>
    ///
    /// 布局范式（对标原版 ITab_Storage）：
    /// 左侧分类导航（7 页）+ 右侧内容区 + 底部操作条。
    ///
    /// 数据纪律：只消费 <see cref="WorkshopStats"/> 快照与
    /// <see cref="WorkshopStats.ScanDevices"/> 设备清单（Read Model），
    /// 不在此做额外聚合；打开/刷新时统计一次，不做每帧扫描。
    ///
    /// 绘制纪律：颜色/字体一律走 <see cref="UITheme"/> + <see cref="UIComponents"/>，
    /// 禁止散落 GUI.color / new Color；每个方法 &lt; 60 行。
    /// </summary>
    internal sealed partial class WorkshopManageView
    {
        private enum Page { Overview, Devices, Members, Schedule, Shifts, Posts, Settings }

        private const float SidebarW = 140f;
        private const float Gap = 8f;
        private const float RowH = 30f;
        private const float StatRowH = 64f;
        private const float UsageCardH = 46f;
        private const float HeaderH = 26f;
        private const float FooterH = 38f;
        private const float SearchH = 26f;
        private const float Pad = 8f;
        private const float ButtonW = 84f;

        /// <summary>「开启手动优先级」按钮宽（状态行右侧；同类按钮宽先例见 AddBtnW / RemoveBtnW 与 WorkPrio* 键组）。</summary>
        private const float WorkPrioBtnW = 160f;

        /// <summary>原版工作优先级状态行高（排班页与设置页共用，含说明与可选按钮）。</summary>
        private const float WorkPrioRowH = 26f;

        // 设备表三列的宽度占比（名称 / 位置 / 状态）。
        // CHG-20261004-UIROWS（2026-10-04 玩家实机截图修复）：姓名列原为 0.50，四个定宽列相加 0.98
        // 后再叠加 removeColShare（RemoveBtnW/宽 约 0.037）=> 活动列宽为负（约 -0.017）
        // => TableHeader 的 Mathf.Max(0f, w) 把「活动」表头压成 0 宽（截图中该列标题整个消失），
        //    行内 actW 同样为负 => 活动文本永不绘制。且 0.50 的姓名列在 3~5 字姓名后留下大片空白。
        // 现改为「四列定宽 + 活动列吃余量 + 最小可读宽降级」，见 DrawMembers。
        private const float ColNameShare = 0.22f;
        // 2026-10-02 减法审计：ColPosShare / ColMemberW / ColHoursW / ColTaskW / ColGateW /
        // ColPriorityW / RemoveColW / TaskTypeCount / RotationModeCount 经全工程检索**零引用**
        // （残留自早期排班页重构），已删；列宽改为在使用处以局部常量表达。

        /// <summary>人员页「今日岗位」列宽占比（表头与行共用同一组，防错位）。</summary>
        private const float ColDutyShare = 0.20f;

        /// <summary>人员页「主技能」列宽占比。</summary>
        private const float ColSkillShare = 0.18f;

        /// <summary>
        /// 人员页「岗位」列宽占比（2026-10-03 用户定责新增）。
        /// <para>用户要求「岗位自动分配结果显示在**人员** tab」⇒ 这里给每人一个岗位列；
        /// 宽度从<b>活动</b>列扣（活动文本最长、且已有预截断，最能承受压缩）。</para>
        /// </summary>
        private const float ColPostShare = 0.20f;

        /// <summary>
        /// 「活动」列的<b>最小可读宽</b>：余量不足此值即<b>整列隐藏</b>（表头与行同步），
        /// 而不是画负宽矩形。依据组件绘制规范铁律 4「先算空间预算，不够就降级，不要画崩」。
        /// </summary>
        private const float ActMinW = 60f;

        /// <summary>
        /// 行内控件的垂直内缩。CHG-20261004-UIROWS：此前移除键写死 <c>3f</c>（行高魔法数），
        /// 岗位 chip 若另取一个值就会与它基线不齐 ⇒ 抽成常量由两者共用，
        /// 既消除魔数（组件绘制规范铁律 1）又保证同行元素对齐。
        /// </summary>
        private const float RowCtrlInset = 3f;

        /// <summary>
        /// 人员页两列的<b>刷新期快照</b>（pawn → 文本）：绘制路径只读字典，零计算。
        ///
        /// <para>2026-10-02：审计指出"每成员每帧 <c>TopSkillsOf</c> + <c>ActivityText</c>"
        /// 是排班页的 P1 —— 人员页新增列时不复制该模式，统一在 <c>Refresh()</c> 期算一次。</para>
        /// </summary>
        private readonly Dictionary<Pawn, string> memberDutyCache = new Dictionary<Pawn, string>();

        /// <inheritdoc cref="memberDutyCache"/>
        private readonly Dictionary<Pawn, string> memberSkillCache = new Dictionary<Pawn, string>();

        /// <summary>
        /// 设备页行文本的<b>刷新期快照</b>（thingIDNumber → [工单, 产出"今日/本周", 运行时长]）。
        ///
        /// <para>2026-10-02 减法审计：这三格原先在<b>每帧每可见行</b>各算一次
        /// （<c>CurrentBillText</c> 遍历 BillStack + <c>RepeatInfoText</c> 拼串、两次台账查询 + 整数拼串、
        /// <c>RuntimeText</c> 翻译 + 两次 <c>ToString</c>）—— 是设备页最大的每帧开销来源。
        /// 现在统一在 <c>Refresh()</c> 期算好，绘制路径只做一次字典查找（PERF-002）。</para>
        /// </summary>
        private readonly Dictionary<int, string[]> deviceRowTextCache = new Dictionary<int, string[]>();

        /// <summary>重建设备页行文本快照（<c>Refresh()</c> 期调用；幂等）。</summary>
        private void BuildDeviceRowTextCache()
        {
            deviceRowTextCache.Clear();
            if (Manager == null || devices == null)
            {
                return;
            }
            int today = GenDate.DaysPassed;
            // 操作员读数（CHG-20261005-DEVCOLS）：与概览页/调色板**同一读法**
            // （WorkshopDeviceReader.CollectActiveWorkers，唯一实现）—— 本页不另写一套
            // "谁在哪台设备上"的判定，否则同一屏两处会显示不同的人（UI-009 无法自证）。
            Dictionary<Thing, Pawn> activeWorkers = zone != null && zone.Map != null
                ? WorkshopDeviceReader.CollectActiveWorkers(zone.Map)
                : null;
            for (int i = 0; i < devices.Count; i++)
            {
                Thing thing = devices[i];
                if (thing == null)
                {
                    continue;
                }
                long runTicks = Manager.DeviceLedger.RunTicksOf(thing);
                int producedTotal = Manager.DeviceLedger.ProducedTotalOf(thing);
                Pawn worker = null;   // 显式初始化：短路求值下 out 可能未执行（CS0165）
                bool manned = activeWorkers != null && activeWorkers.TryGetValue(thing, out worker)
                    && worker != null && !worker.Destroyed;
                // 下标：0 工单 / 1 产出"今日/本周" / 2 累计运行 / 3 累计产值 / 4 建立时间
                //       / 5 效率 / 6 别名 / 7 操作员
                deviceRowTextCache[thing.thingIDNumber] = new[]
                {
                    CurrentBillText(thing),
                    Manager.DeviceLedger.ProducedOnDayOf(thing, today)
                        + " / " + Manager.DeviceLedger.ProducedInWindowOf(thing, today),
                    RuntimeText(runTicks),
                    ValueText(Manager.DeviceLedger.ProducedValueOf(thing)),
                    BornText(Manager.DeviceLedger.FirstSeenTickOf(thing)),
                    EfficiencyText(producedTotal, runTicks),
                    Manager.AliasOf(thing),
                    manned ? worker.LabelShort : "--"
                };
            }
        }

        /// <summary>累计产值文本（银；0 ⇒ "—"，不谎报"有产值 0"）。</summary>
        private static string ValueText(float value)
        {
            return value > 0.5f ? value.ToString("N0", CultureInfo.InvariantCulture) : "--";
        }

        /// <summary>
        /// 建立时间文本（"第 N 天"，N = <c>GenDate.DaysPassedAt</c>）。
        /// <para>用"第 N 天"而不是绝对日期：与游戏内日程表读法一致，且不依赖日期格式与语言。
        /// 旧档无记录（0）⇒ "—"，不谎报"今天建立"。</para>
        /// </summary>
        private static string BornText(long firstSeenTick)
        {
            return firstSeenTick > 0L
                ? T1("ColBornDay", GenDate.DaysPassedAt((int)firstSeenTick))
                : "--";
        }

        /// <summary>效率 = 累计产出 ÷ 累计运行小时（件/机时，列名带单位）；无运行 ⇒ "--"。</summary>
        private static string EfficiencyText(int producedTotal, long runTicks)
        {
            if (runTicks <= 0L)
            {
                return "--";
            }
            float hours = runTicks / (float)GenDate.TicksPerHour;
            return hours <= 0.01f
                ? "--"
                : (producedTotal / hours).ToString("0.0", CultureInfo.InvariantCulture);
        }

        // ⚠ CHG-20261005-COLTUNE（2026-10-05 玩家要求「移除耐久」）：原 `DurabilityText` /
        // `DurabilityTone` 两个方法**已删除** —— 它们只服务耐久列。
        // 若将来要恢复该列：文本取 `HitPoints / MaxHitPoints` 的百分比，
        // 配色直接调 `RUIF.TableFmt.BandColor(ratio)`（框架三档阈值，勿再自写一套）。

        /// <summary>取该设备预构建的行文本；未命中（快照过期 / 非本车间）返回 null。</summary>
        private string[] DeviceRowTextOf(Thing thing)
        {
            if (thing == null)
            {
                return null;
            }
            string[] row;
            return deviceRowTextCache.TryGetValue(thing.thingIDNumber, out row) ? row : null;
        }

        private const string KeyPrefix = "PersonalChronicle.Automation.Tab.";

        /// <summary>
        /// CHG-20261004-IDLE-FIX：设备数变化时重涂一次作息表。
        /// <para>为什么必须做：冗余判定是"名单里排在设备数之后的人"，
        /// 设备增减会改变这个边界；不重涂则界面显示「设备 3」而实际切分停在「设备 1」
        /// 的时刻（摸鱼判定与界面数字不一致 = 玩家无法自证，违反 UI-009）。</para>
        ///
        /// <para><b>⚠ 2026-10-04 审计 P1-1 修复</b>：快照原先存在本 View 的实例字段上
        /// （<c>lastIdleCapacity</c>），而 <c>Dialog_WorkshopManage</c> <b>每次开窗都 new 一个
        /// View</b> ⇒ 字段初始化即归零 ⇒ <b>每次打开面板都判定"设备数变化"并重涂全班组作息表</b>
        /// （静默覆盖玩家手排）。现改为向 <see cref="MapComponent_WorkshopManager"/> 取运行态快照
        /// （每图一个、跨开窗存活、按 zone.ID 区分）。</para>
        ///
        /// <para>低频：设备数变化才触发（放/拆设备），并复用 Refresh 期已扫好的
        /// <c>devices</c> 快照，不额外扫描。</para>
        /// </summary>
        private void SyncIdleSplitIfDeviceCountChanged()
        {
            if (zone == null || Manager == null)
            {
                return;
            }
            int capacity = devices != null ? devices.Count : -1;
            if (!Manager.ConsumeIdleSplitCapacityChange(zone, capacity))
            {
                return;
            }
            Manager.SyncVanillaTimetables(zone);
        }

        private readonly Zone_Workshop zone;

        /// <summary>底部「关闭」按钮的回调：弹窗传 <c>Close</c>，ITab 传 <c>CloseTab</c>。</summary>
        private readonly Action closeAction;

        private Page page = Page.Overview;
        private Vector2 scroll = Vector2.zero;

        /// <summary>设置页类型网格的滚动位（类型多 / 窗口矮时启用，与其它页滚动位分离）。</summary>
        private Vector2 settingsScroll = Vector2.zero;
        private WorkshopStats stats;
        private readonly List<Thing> devices = new List<Thing>();

        // ═══ CHG-20261005-TABLESORT：设备表三态排序 ═══
        // 只存"点了哪列、什么方向"；**重排在 Refresh 期做一次**（见 ApplyDeviceSort），
        // 绘制路径只读 —— 排序放进每帧入口就是每帧 O(n log n) + 台账查询。

        /// <summary>当前排序列（-1 = 不排序，保持设备扫描原序）。</summary>
        private int deviceSortCol = -1;

        /// <summary>排序方向（三态循环：无 → 升 → 降 → 无）。</summary>
        private RUIF.SortState deviceSortState = RUIF.SortState.None;

        // ⚠ CHG-20261005-COLTUNE（2026-10-05 玩家要求「移除搜索框」）：原搜索缓存三件
        // （filteredCache / filteredFor / filteredDirty）与 RebuildFiltered **一并删除** ——
        // 设备页取值路径现在只有 devices 本身（顺序由 Refresh 期的 ApplyDeviceSort 决定）。

        // 人员页数据源 = zone.Members（玩家手动指派名单，随存档持久化），
        // 不是检测进入 zone 的 pawn（PM 拍板，2026-09-04）。
        private const float AddBtnW = 96f;
        private const float RemoveBtnW = 32f;

        /// <summary>最近一次 <see cref="Refresh"/> 算出的快照（供宿主窗口标题栏读取）。</summary>
        public WorkshopStats Stats => stats;

        public WorkshopManageView(Zone_Workshop zone, Action closeAction)
        {
            this.zone = zone;
            this.closeAction = closeAction;
        }

        /// <summary>重算快照与设备清单（打开时与底部「刷新」按钮的唯一入口）。</summary>
        public void Refresh()
        {
            devices.Clear();
            if (zone != null && zone.Map != null)
            {
                devices.AddRange(WorkshopStats.ScanDevices(zone));
            }
            // CHG-20261005-TABLESORT：排序只在这里做一次（点表头 ⇒ 直接调本方法 ⇒ 重排一次）。
            ApplyDeviceSort();
            // 设备清单只扫一遍，快照与侧栏计数共用同一次结果。
            stats = WorkshopStats.For(zone, devices);
            BuildFitBodyText();
            // 设备页 KPI 横幅（Refresh 期构建；绘制零翻译/零拼接 —— GOV-010/PERF-002）。
            // 产出口径（2026-10-02 用户要求「将产出分为每日和每周」）：横幅给本周合计，
            // 行内给「今日 / 本周」两个读数；两者都取自逐日缓冲，与 RollDay 调用时机无关。
            int running = 0, faulted = 0, producedWeek = 0;
            int today = GenDate.DaysPassed;
            for (int i = 0; i < devices.Count; i++)
            {
                string sk = ResolveStateKey(devices[i]);
                if (sk == PersonalChronicle.Domain.EquipmentStateResolver.KeyRun
                    || sk == PersonalChronicle.Domain.EquipmentStateResolver.KeyAgRunning)
                {
                    running++;
                }
                else if (sk == PersonalChronicle.Domain.EquipmentStateResolver.KeyFault)
                {
                    faulted++;
                }
                producedWeek += Manager != null
                    ? Manager.DeviceLedger.ProducedInWindowOf(devices[i], today) : 0;
            }
            devicesBanner = T("Devices") + " " + devices.Count
                + " · " + T("KpiRunning") + " " + running
                + " · " + T("KpiFaulted") + " " + faulted
                + " · " + T("KpiProducedWindow")
                + " " + producedWeek;
            PurgeDeadMembers();
            // 2026-10-02 人员页补列（减法审计配套）：「今日岗位 / 主技能」两列在刷新期算一次，
            // 绘制路径只读字典 —— 不把计算放进每帧路径（PERF-002）。
            RebuildMemberInfoCache();
            // 岗位页缓存（CHG-20261003-POSTS · Phase 1）：同样是刷新期算一次、绘制路径只读。
            // 分工在 Domain.WorkshopPostAssigner（纯函数、可离线单测），本方法只做取数与编排。
            BuildPostRows();
            // 接管台账读数（CHG-20261003-TAKEOVER）：决定「解除」按钮是否可用。
            takeoverLedgerEntries = Current.Game != null
                ? Current.Game.GetComponent<PersonalChronicle.Data.ChronicleGameComponent>()
                    .TakeoverEntryCount
                : 0;
            // CHG-20261005-DEVCOLS：设备别名补发（幂等、只在刷新期）—— 设备页/概览/详情卡
            // 用它区分同名设备；放在行文本快照**之前**，快照里直接取别名。
            Manager?.EnsureDeviceAliases(devices);
            // 2026-10-02 减法审计：设备页三格行文本同样移到刷新期（每帧每可见行省一次
            // BillStack 遍历 + 两次台账查询 + 一次翻译 + 多次 ToString）。
            BuildDeviceRowTextCache();
            // 车间专属工位状态行文本（CHG-20261003 / EXC-2026-015）：
            // 同样在 Refresh 期算一次，绘制路径只读字符串（PERF-002）。
            BuildWorkStationStatusText();
            // 设备/工单变化 → 重放一次「车间专属工位」（CHG-20261003 · L1）。
            // 写在 Refresh 期（事件驱动），**不在 tick 里**；tick 只置只读脏标记。
            Manager?.SyncWorkStationIfDirty();
            // CHG-20261004-IDLE：设备数变化 ⇒ 冗余切分（Work / 摸鱼）过期 ⇒ 重涂一次作息表。
            SyncIdleSplitIfDeviceCountChanged();
            // ★ CHG-20261005-EVERWORK（2026-10-05 玩家实机「两班倒人员不上班」根治）：
            //   打开面板就对全车间成员补一次 workSettings 初始化（幂等、不写档位）。
            //   为什么必须挂在开面板：另外两条会初始化的路径都覆盖不全 ——
            //   SyncVanillaWorkSettings 只在排班/名册变更时跑；SyncWorkStationForZone 只处理
            //   "当前在班"的人；而玩家读档后常常不改任何配置 ⇒ 那两条都不会跑到
            //   ⇒ 人「当班却不动」，直到手动点一次「接管原版工作」（唯一对全体无条件初始化的路径）。
            //   放在 Refresh 期（事件驱动、非 tick、非绘制），与既有对账通道同一纪律。
            Manager?.EnsureWorkSettingsInitialized(zone);
            // ★ CHG-20261005-TAKEOVERUI（2026-10-05 玩家裁定「接管原版工作默认开启」）：
            //   本车间**首次**打开面板即自动接管一次（一次性；之后完全由玩家掌控）。
            //   · 顺序刻意在 EnsureWorkSettingsInitialized **之后**：先保证工作设置可用，
            //     再取消勾选车间类工作 —— 反过来的话，取消勾选会落在未初始化的 workSettings 上
            //     （原版对未初始化的 workSettings 写档位会 Log.Error 且不生效）。
            //   · 为什么必须加这一步：既有的自动接管只挂在 OnRosterChanged（排班/名册变更），
            //     读档后不改配置就不会触发 ⇒ 玩家仍得手点一次才"生效"（与 EverWork 同一缺口）。
            //   · 一次性而非每次：见 Zone_Workshop.AutoTakeoverDone 注释（保护玩家手改）。
            if (zone != null && !zone.AutoTakeoverDone)
            {
                Manager?.SyncVanillaWorkTakeover(zone);
                zone.AutoTakeoverDone = true;
            }
            // ★ CHG-20261005-NODEMOTE（2026-10-05 玩家批准 A 方案）：**旧档一次性迁移**。
            //   降档通道已移除 ⇒ 旧存档里可能残留"被压到最低档（4）"的工作项，而原出口
            //   （「一键全解除」）已随链删除 ⇒ 不处理的话这些项会**永远卡在 4 档**。
            //   这里每次开面板调一次 RestoreAllDemotedWork()：它**幂等且自清理**
            //   （还原后即从台账移除该条；台账为空时函数立刻返回）⇒ **无需新增"已迁移"标记字段**。
            //   两道既有闸门仍生效：人已死/离队 ⇒ 丢弃记录；该项当前值已被玩家改过 ⇒ 跳过还原。
            Manager?.RestoreAllDemotedWork();
            // ★ CHG-20261005-NODEMOTE（A 方案的核心）：**移出车间即恢复**。
            //   ① 先 SyncAutoHide：把本轮在册成员补进自动隐藏集合（收新人）——
            //      ⚠ NODEMOTE-AUDIT（P2 修复）：Refresh 原先**不调** SyncAutoHide，
            //      而人员页「添加人员」入口不触发 OnRosterChanged ⇒ 经该入口入册的成员
            //      在下次名册事件前不会被隐藏/接管（且反复开关面板也不补救）。
            //      在这里补上 ⇒ 覆盖**所有**入册路径（Refresh 是开面板的唯一入口）。
            //   ② 再 Reconcile：与上一轮快照做差集 ⇒ 已移出者：从自动隐藏集合移除
            //      （面板恢复显示）+ 按人解除接管。快照在 Manager（不入档）⇒ 读档后首次
            //      对账差集为空，不会误判。顺序必须是"先收新人、再放旧人"。
            Manager?.SyncAutoHideWorkTabColonists(zone);
            Manager?.ReconcileAutoHideOnLeave();
            // CHG-20261005-OVERVIEW-IND：概览页的工业化读数（仪表/灯阵）在 Refresh 期算好，
            // 绘制路径只取值（skill 反模式：绘制期不做聚合与翻译）。
            BuildOverviewReadings();
            // 单一编制兜底（2026-09-26）：旧会话/旧版本遗留的「一人多组」在开窗刷新时自愈
            //（每人保留最先编入的班组）。写入口已由 Zone_Workshop.AssignPawnToCrew 堵住。
            zone.DedupeCrewMemberships();
            // 自动按设备风格占比判定车间类型。
            // 审计修复（2026-09-30）：原在 DrawSettings 首帧执行 ⇒ 绘制路径写持久数据 + 触发重扫。
            // CHG-20261004-AUTOTYPE（2026-10-04）：触发条件从"会话内只试一次"改为
            // **设备数变化时重试** —— 旧实现下"先开面板（当时没设备）再放设备"永远不会判定，
            // 岗位页会一直显示「尚未实现」（用户实机：设备 3 台、岗位全未实现、无从下手）。
            // 玩家手动清除过类型则不再回设（autoTypeSuppressedByPlayer，见清除按钮）。
            // ⚠ 2026-10-04 审计 P1-1/P1-2 修复：抑制标志改读**车间数据**（持久化，跨会话有效），
            //   设备数快照改向 Manager 取（运行态，跨开窗存活）—— 两者原先都在 View 实例上，
            //   导致「关窗即忘」。
            // CHG-20261004-WSSETTINGS：自动判定可全局关闭（默认开）；关闭后必须手动在
            // 本页指定类型（等价于逐车间 autoTypeSuppressed 的全局版）。
            ChronicleSettings typeSettings = PersonalChronicle.PersonalChronicleMod.Settings;
            bool autoTypeAllowed = typeSettings == null || typeSettings.AutoDetectWorkshopType;
            if (autoTypeAllowed && !zone.AutoTypeSuppressed && zone.WorkshopType == null)
            {
                int capacity = devices != null ? devices.Count : -1;
                if (Manager == null || Manager.ConsumeAutoTypeCapacityChange(zone, capacity))
                {
                    WorkshopTypeDef best = DominantWorkshopType();
                    if (best != null)
                    {
                        SetWorkshopType(best);
                    }
                }
            }
            // 旧档自愈（2026-10-02 · EXC-2026-012）：打开管理面板时对账一次「原版工作面板勾选」——
            // 本模组在旧会话/旧版本里从未写过 workSettings，仅靠事件触发会让老存档一直带着
            // "排了班却不动手"的旧状态。此处是玩家主动操作（非读档副作用），写入仍为幂等「仅 0→默认档」。
            if (Manager != null)
            {
                Manager.SyncVanillaWorkSettings(zone);
            }
        }

        /// <summary>
        /// 清理人员名单里已死亡/已删除的引用（<c>LookMode.Reference</c> 读档后会被置 null）。
        ///
        /// <para>2026-09-11 体检 P1-3：这段清理原先写在人员的**绘制**路径里，等于每帧改写持久字段
        /// <c>zone.Members</c>，并为 <c>RemoveAll</c> 每帧分配一个委托闭包。现移入刷新路径
        /// （开窗 / 手动刷新 / 排班变更后），绘制只读快照。</para>
        /// </summary>
        private void PurgeDeadMembers()
        {
            if (zone == null || zone.Members == null)
            {
                return;
            }
            zone.Members.RemoveAll(p => p == null || p.Destroyed);
            // 头像预热表同步清理（2026-10-02）：否则死引用会一直占着 HashSet 条目。
            warmedPortraits.RemoveWhere(p => p == null || p.Destroyed);
        }

        /// <summary>
        /// 车间是否仍然有效（未被删除/未清空）。弹窗宿主据此自动关闭，
        /// 避免开窗期间删车间后一直显示陈旧数据。
        ///
        /// <para>判据：① Map 为 null（跨图/卸载）；② cells 已被清空（原版删除整区会逐格清空
        /// ⇒ <c>Zone.RemoveCell</c> 内自动 Deregister）；③ 已不在 <c>AllZones</c> 中
        /// （兜住任何未清空就注销的路径）。</para>
        ///
        /// <para><b>刻意不再用 <c>ZoneAt(Cells[0]) == zone</c></b>：那是"最后圈定者胜"的网格反查，
        /// 玩家把仓库区/种植区压在车间之上（含压住首格）时会误判车间已死并自动关窗
        /// （见 EXC-2026-005）。改动后判据只依赖车间自身状态，O(#zones) 且结果稳定。</para>
        /// </summary>
        public bool IsZoneAlive()
        {
            // 委托判活唯一入口（与 MapComponent_WorkshopManager.PurgeDeadReferences 同源）。
            return WorkshopZoneUtility.IsAlive(zone);
        }

        /// <summary>绘制整个内容层。宿主只给一个 Rect，其余全部由本类负责。</summary>
        public void Draw(Rect rect)
        {
            if (!IsZoneAlive())
            {
                UIComponents.Label(rect, T("Gone"),
                    UITheme.FontBody, UITheme.Muted, TextAnchor.MiddleCenter);
                return;
            }

            DrawSidebar(new Rect(rect.x, rect.y, SidebarW, rect.height));
            float contentX = rect.x + SidebarW + Gap;
            DrawContent(new Rect(contentX, rect.y, rect.width - SidebarW - Gap, rect.height));
        }

        // ---- 左侧分类导航 ----------------------------------------------------
        private void DrawSidebar(Rect rect)
        {
            UIComponents.Panel(rect);
            int deviceCount = stats != null ? stats.DeviceCount : 0;
            float y = rect.y + Pad;
            y = SidebarRow(rect, y, T("Overview"), Page.Overview, -1);
            y = SidebarRow(rect, y, T("Devices"), Page.Devices, deviceCount);
            y = SidebarRow(rect, y, T("Members"), Page.Members, -1);
            y = SidebarRow(rect, y, T("Schedule"), Page.Schedule, -1);
            // 班次表（2026-10-02 新增）：排班页收敛为「一卡一班次」后，全班次的集中管理入口。
            y = SidebarRow(rect, y, T("ShiftTable"), Page.Shifts, -1);
            // 岗位页（CHG-20261003-POSTS · Phase 1）：只读展示成员 × 岗位的建议分配。
            y = SidebarRow(rect, y, T("Posts"), Page.Posts, -1);
            SidebarRow(rect, y, T("Settings"), Page.Settings, -1);
        }

        /// <summary>侧栏行。count ≥ 0 时在右侧渲染数量角标（如设备数）。</summary>
        private float SidebarRow(Rect sidebar, float y, string label, Page target, int count)
        {
            Rect row = new Rect(sidebar.x + Pad, y, sidebar.width - Pad * 2f, RowH);
            if (UIComponents.SidebarItem(row, label, page == target))
            {
                page = target;
                scroll = Vector2.zero;
            }
            if (count >= 0)
            {
                Rect numRect = new Rect(row.xMax - 44f, y, 40f, RowH);
                UIComponents.Label(numRect, count.ToString(),
                    UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleRight);
            }
            return y + RowH + 4f;
        }

        // ---- 右侧内容区 ------------------------------------------------------
        private void DrawContent(Rect rect)
        {
            float footerY = rect.yMax - FooterH;
            Rect body = new Rect(rect.x, rect.y, rect.width, footerY - rect.y - Gap);

            switch (page)
            {
                case Page.Overview: DrawOverview(body); break;
                case Page.Devices: DrawDevices(body); break;
                case Page.Members: DrawMembers(body); break;
                case Page.Schedule: DrawSchedule(body); break;
                case Page.Shifts: DrawShiftTable(body); break;
                case Page.Posts: DrawPostsPage(body); break;
                case Page.Settings: DrawSettings(body); break;
            }

            DrawFooter(new Rect(rect.x, footerY, rect.width, FooterH));
        }

        /// <summary>
        /// 概览页（重设计版）：三卡横排 KPI 行 + 空间占用条 + 教育性提示。
        /// 卡片 accent 三色区分语义：格数=Accent / 空地=Alive / 设备=Info。
        /// </summary>
        /// <summary>
        /// 概览页入口。
        ///
        /// <para><b>CHG-20261005-OVERVIEW-IND</b>（2026-10-05 玩家批准）：按"工业化 / 数字化 /
        /// 智能化"视角重做 —— 原来的「KPI 三卡（格数/空地/设备）+ 空间占用条 + 匹配度一行」
        /// 换成**仪表台 + 设备状态灯阵 + 匹配（缺失单独成行）**，实现见
        /// <see cref="DrawOverviewIndustrial"/>（partial：WorkshopManageView.Overview.cs）。</para>
        ///
        /// <para>旧实现的问题（玩家截图实证）：① 顶部标题已写"格数/空地/设备"，下面三张大卡
        /// 重复同一组数；② 下方 60% 空白；③ 匹配度只有一行计数，而缺失设备名其实已算好却没露出来。</para>
        /// </summary>
        private void DrawOverview(Rect rect)
        {
            DrawOverviewIndustrial(rect);
        }

        // ⚠ CHG-20261005-OVERVIEW-IND：原 DrawKpiCard 已删除 —— 它画的三项（格数/空地/设备）
        //   与窗口标题重复，且被仪表台取代（表盘给比例、值行给精确计数）。

        // ⚠ CHG-20261005-OVERVIEW-IND：原 DrawOccupancyCard 已删除 —— 占用率改由仪表台
        //   第一张表（GaugeOccupancy）表达，且补上了玩家读不懂的百分比与口径（占用/总格）。

        /// <summary>
        /// 设备匹配度卡：把 <see cref="WorkshopTypeDef.expectedDeviceDefNames"/>（本工序的
        /// 原生锚点设备）与区内实际设备比对，显示「匹配 / 多余 / 缺失」。
        ///
        /// <para>LAND-009 诚实口径：车间未指定类型、或该类型<b>未声明</b>期望设备
        /// （调试台/质检台/维修机等尚未落地）时显示"未声明期望设备"，<b>不</b>显示为"缺失"。</para>
        /// </summary>
        /// <summary>设备匹配卡正文（Refresh 期构建，绘制期只读）。</summary>
        private string fitBodyText;

        /// <summary>
        /// 预构建匹配卡正文（与 <see cref="DrawFitCard"/> 的口径完全一致）。
        /// 放在 Refresh 而非绘制路径：避免每帧翻译 ×2 + <c>string.Join</c> 拼接（审计 P2）。
        /// </summary>
        private void BuildFitBodyText()
        {
            if (stats == null)
            {
                fitBodyText = string.Empty;
                return;
            }
            if (!stats.HasExpectedDevices)
            {
                fitBodyText = T("FitUnspecified");
                return;
            }
            fitBodyText = T1("FitMatched", stats.MatchedDevices)
                + " · " + T1("FitExtra", stats.ExtraDevices)
                + " · " + (stats.MissingDeviceLabels.Count == 0
                    ? T("FitNoMissing")
                    : (KeyPrefix + "FitMissing").Translate(
                        stats.MissingDeviceLabels.Count,
                        string.Join(", ", stats.MissingDeviceLabels)).ToString());
        }

        // ⚠ CHG-20261005-OVERVIEW-IND：原 DrawFitCard 已由 DrawFitSection 取代（后者把
        //   「缺失设备名」单独成行显示）。BuildFitBodyText 仍在使用（新绘制段复用它）。

        // ═══════════ 设备表列定义（CHG-20261005-DEVCOLS）═══════════
        //
        // 表头与行**共用**下面这一份 share 数组 —— 原先两处各写一份，是历史「表头与行错位 /
        // 活动列负宽」事故的根源（见文件顶部 ColNameShare 处的注释）。

        /// <summary>
        /// 每列「可读下限」（px）。CHG-20261005-RUIFTABLE：窄档阈值由此**推导**，不再写死 900。
        ///
        /// <para>CHG-20261005-COLTUNE2：由 88 提到 <b>100</b> —— 88 时门槛是 <c>10 × 88 = 880</c>，
        /// 而玩家窗口约 895px ⇒ 仍在走全档，10 列平摊后各列过挤（实机截图可见）。</para>
        ///
        /// <para>CHG-20261005-COLTUNE3（移除「效率 / 位置」）：列数降到 <b>8</b>
        /// ⇒ 门槛随之变为 <c>8 × 100 = 800</c>，玩家那个约 895px 的窗口**回到全档（8 列）**、
        /// 每列约 105px；只有更窄的窗口才降到 7 列（再隐藏「建立时间」）。</para>
        /// </summary>
        private const float DeviceMinColW = 100f;

        /// <summary>
        /// 列间统一留白（CHG-20261005-RUIFTABLE：与 RUIF TableFmt.ColumnSplit 的 gap 同源）。
        ///
        /// <para>⚠ <b>CHG-20261005-COLAUDIT（2026-10-05 玩家报「列宽没变化」的审计修复）</b>：
        /// 核实 RUIF 源码后确认 <c>ColumnSplit</c> 的语义是 ——
        /// 前 n−1 列 = <c>shares[i] × 表宽</c>（<b>gap 完全不影响它们</b>），
        /// <b>末列 = 余宽 − (n−1) × gap</b>（gap 全部由末列承担）。因此本值 4 → 2 的效果是：</para>
        /// <list type="bullet">
        /// <item>前 7 列宽度<b>一字不变</b>（与 gap 无关）；</item>
        /// <item>末列「建立时间」由 <c>0.06W − 28</c> 变为 <c>0.06W − 14</c>
        /// ⇒ 960px 表宽下从约 <b>28px</b> 提升到约 <b>44px</b>
        /// —— 该列此前被压到几乎不可读（"第 9 天"需要约 56px）。</item>
        /// </list>
        /// <para>⇒ 这是一次"只救末列、不动其余"的零风险修正。真正的比例重分（若要）需先由玩家
        /// 裁定目标形态 —— 列宽比例是主观设计，反复猜配置只会让两边都累。</para>
        /// </summary>
        private const float DeviceColGap = 2f;

        /// <summary>
        /// 设备表列宽 · 全档（**10 列**，总和 = 1）。
        /// <para>CHG-20261005-COLTUNE（2026-10-05 玩家要求「移除耐久、优化列宽列高」）：
        /// 耐久列移除 ⇒ 份额按"文本列给足、数值列够用"重分。
        /// 移除后<b>全档与窄档的前 7 列完全同号</b>（名称/操作员/状态/制作/产出/运行/产值），
        /// 只有末尾三列分叉 —— 见 <c>DrawDeviceRow</c> 的下标注释。</para>
        /// </summary>
        private static readonly float[] DeviceColSharesFull =
        {
            0.23f,    // 名称（别名 · 设备名 · 品质）—— 文本列，给足以免截断
            0.10f,    // 操作员
            0.075f,   // 状态（"•运行 / •待命" 很短）
            0.21f,    // 当前制作（工单名可很长，如「烹饪简单食物 x4 永远」）
            0.11f,    // 产出（今日 / 本周）
            0.105f,   // 累计运行
            0.11f,    // 累计产值
            // ⚠ CHG-20261005-COLAUDIT：**本项是"名义值"** —— RUIF `ColumnSplit` 令末列
            //   **吃余宽**（`widths[n−1] = 表宽 − Σ前n−1 − (n−1)×gap`），**不读本值**
            //   ⇒ 改这一项对画面**毫无影响**（这正是"改了列宽却看不出变化"的一个真实来源）。
            //   末列真宽 = (1 − Σ前7)×W − (n−1)×gap；要加宽它只能**减小前 7 项**或**减小 gap**。
            0.06f     // 建立时间（"第 9 天"）—— 名义值，仅供文档/校验
        };

        /// <summary>
        /// 设备行行高。CHG-20261005-COLTUNE：**设备页专用**（原先用全局 <c>RowH = 30</c>）。
        /// <para>玩家要求「优化列高」：设备行内容只有单行文本，30 在 20 号字下留白过多；
        /// 26 能在同样高度里多显示约一屏半的行。</para>
        /// <para>⚠ 刻意**不动**全局 <c>RowH</c>：人员页 / 班次表等也用它，改它属越界变更。</para>
        /// </summary>
        private const float DeviceRowH = 26f;

        /// <summary>
        /// 窄档阈值：**由"每列可读下限"推导**（CHG-20261005-RUIFTABLE），不再写死 900。
        ///
        /// <para><b>当前 = 全档列数 × <see cref="DeviceMinColW"/> = 8 × 100 = 800</b>
        /// （全档 8 列 / 窄档 7 列，见 <see cref="DeviceColSharesFull"/>）。</para>
        ///
        /// <para>⚠ <b>CHG-20261005-COLAUDIT（审计修正）</b>：原文写"全档 11 列 ⇒ 11 × 88 = 968"，
        /// 那是 COLTUNE 之前的旧参数 —— 列数已 11 → 10 → 8、下限已 88 → 100，
        /// **注释漂移会让下一个人按 968 估分档点，判断与实际相反**。故改为只描述表达式、
        /// 不再写死数字（要数字就现算：列数变了它自己会变）。</para>
        ///
        /// <para>⚠ 声明顺序有意义：本字段读 <see cref="DeviceColSharesFull"/>，必须排在其后
        /// （静态字段按声明顺序初始化，放前面会拿到 null）。</para>
        /// </summary>
        private static readonly float DeviceCompactWidth = DeviceColSharesFull.Length * DeviceMinColW;

        /// <summary>
        /// 设备表列宽 · 窄档（8 列：抽掉「建立时间 / 效率」两列，份额并回名称与制作，总和 = 1）。
        ///
        /// <para>CHG-20261005-COLTUNE2：按"实际内容需要"重分 —— 名称给到 0.22（含品质徽标）、
        /// 位置给到 0.075（`98,0,113` 在 895px 下约 67px，刚好不截断），
        /// 其余按原文/数值的字符宽度递减。</para>
        ///
        /// <para>列序与全档一致（只是少了末尾两列）：名称/操作员/状态/当前制作/产出/运行/产值/位置。</para>
        /// </summary>
        private static readonly float[] DeviceColSharesCompact =
        {
            0.25f,    // 名称（含品质徽标）
            0.11f,    // 操作员
            0.08f,    // 状态
            0.22f,    // 当前制作
            0.12f,    // 产出（今日 / 本周）
            0.11f,    // 累计运行
            0.11f     // 累计产值
        };

        /// <summary>
        /// 设备表**列样式**（全档）——CHG-20261005-RUIFTABLE：表头与数据格**共用同一份锚点**，
        /// 由 <see cref="RUIF.TableFmt.ColumnAlignOf"/> 解析；两边读同一个数组 ⇒ 结构上不可能错位。
        ///
        /// <para>规则：文本列左对齐、数值列右对齐、弱信息列 <c>Dim</c>（框架的颜色语义）。
        /// 与 RUIF <c>StatTable</c> 的列样式是同一套约定（"预览数值列表头亦右对齐"）。</para>
        /// </summary>
        private static readonly RUIF.StatCol[] DeviceColStylesFull =
        {
            new RUIF.StatCol { Align = TextAnchor.MiddleLeft },                   // 名称
            new RUIF.StatCol { Align = TextAnchor.MiddleLeft, Dim = true },       // 操作员
            new RUIF.StatCol { Align = TextAnchor.MiddleLeft },                   // 状态
            new RUIF.StatCol { Align = TextAnchor.MiddleLeft },                   // 当前制作
            new RUIF.StatCol { Align = TextAnchor.MiddleRight },                  // 产出
            new RUIF.StatCol { Align = TextAnchor.MiddleRight },                  // 累计运行
            new RUIF.StatCol { Align = TextAnchor.MiddleRight },                  // 累计产值
            new RUIF.StatCol { Align = TextAnchor.MiddleRight, Dim = true }       // 建立时间
        };

        /// <summary>设备表**列样式**（窄档：再去掉「建立时间」后逐项对应）。</summary>
        private static readonly RUIF.StatCol[] DeviceColStylesCompact =
        {
            new RUIF.StatCol { Align = TextAnchor.MiddleLeft },                   // 名称
            new RUIF.StatCol { Align = TextAnchor.MiddleLeft, Dim = true },       // 操作员
            new RUIF.StatCol { Align = TextAnchor.MiddleLeft },                   // 状态
            new RUIF.StatCol { Align = TextAnchor.MiddleLeft },                   // 当前制作
            new RUIF.StatCol { Align = TextAnchor.MiddleRight },                  // 产出
            new RUIF.StatCol { Align = TextAnchor.MiddleRight },                  // 累计运行
            new RUIF.StatCol { Align = TextAnchor.MiddleRight }                   // 累计产值
        };

        /// <summary>
        /// 列宽缓冲（<b>绘制期零分配</b>）：<see cref="RUIF.TableFmt.ColumnSplit"/> 是"写入调用方缓冲"
        /// 的设计，这里复用一个实例数组（长度 ≥ 全档列数，取 16 留余量）。
        /// </summary>
        private readonly float[] deviceColWidths = new float[16];

        /// <summary>
        /// 设备表表头（与 share / 样式数组逐项对应）。
        /// <para>CHG-20261005-COLTUNE3（2026-10-05 玩家要求「移除 效率 / 位置」）：
        /// 全档 8 列、窄档 7 列，**只差「建立时间」一列**。</para>
        /// </summary>
        private static string[] DeviceHeaders(bool compact)
        {
            return compact
                ? new[] { T("ColName"), T("ColWorker"), T("ColState"), T("ColBill"),
                          T("ColOutputWindow"), T("ColRuntime"), T("ColValue") }
                : new[] { T("ColName"), T("ColWorker"), T("ColState"), T("ColBill"),
                          T("ColOutputWindow"), T("ColRuntime"), T("ColValue"), T("ColBorn") };
        }

        private void DrawDevices(Rect rect)
        {
            // CHG-20261005-COLTUNE（2026-10-05 玩家要求「移除搜索框」）：
            // 一个车间的设备是十几台量级、页面已有 KPI 横幅与排序 ⇒ 搜索框收益为零，
            // 却常年占 26px 垂直空间并把 KPI 行往下挤。故整个输入行移除，
            // KPI 横幅直接顶到页面顶部。
            // ⚠ 过滤机制（FilteredDevices 及其缓存）保留：排序与空态判断仍经它取值。

            // 深度优化（2026-10-01）：KPI 横幅（Refresh 期构建，绘制零拼接）——
            // 设备 / 运行 / 故障 / 今日产量一行扫完，填补旧版"单行低密度 + 大片空白"。
            Rect banner = new Rect(rect.x, rect.y, rect.width, 22f);
            UIComponents.Panel(banner);
            RUIF.Draw.Box(new Rect(banner.x, banner.y, 3f, banner.height), UITheme.Accent);
            UIComponents.Label(new Rect(banner.x + 10f, banner.y, banner.width - 12f, banner.height),
                devicesBanner ?? string.Empty, UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);

            Rect headRect = new Rect(rect.x, banner.yMax + 2f, rect.width, HeaderH);
            // CHG-20261005-DEVCOLS（玩家 2026-10-05 指定）→ 经 COLTUNE/2/3 收敛后的**现状：8 列**：
            //   名称 | 操作员 | 状态 | 当前制作 | 产出(每日/每周) | 累计运行 | 累计产值 | 建立时间
            // ⚠ CHG-20261005-WSDOCFIX（注释漂移修正）：原注释写的是 DEVCOLS 当时的 10 列
            //   （含「效率 / 位置」，耐久则在更早的 DURCOL 加、COLTUNE 删）—— 4 批迭代后注释
            //   被甩在后面。**本次扫描中即有子代理据此误报"设备页 10 列"**，故与实现同步。
            // ⚠ 列宽与行绘制**共用同一份 share 数组**（原先表头与 DrawDeviceRow 各自写一份——
            //   正是历史「表头与行错位 / 负宽」事故的根源，这次一并收口，GOV-005）。
            bool compact = rect.width < DeviceCompactWidth;
            // CHG-20261005-TABLESORT：点列名 → 三态排序（升 → 降 → 无）。
            // 排序**不在这里做**（绘制路径）：只更新状态后调 Refresh ⇒ 重排发生在 Refresh 期。
            if (UIComponents.TableHeader(headRect, DeviceHeaders(compact),
                    compact ? DeviceColSharesCompact : DeviceColSharesFull,
                    compact ? DeviceColStylesCompact : DeviceColStylesFull,
                    DeviceColGap, ref deviceSortCol, ref deviceSortState) >= 0)
            {
                Refresh();
            }

            List<Thing> rows = FilteredDevices();
            float listY = headRect.yMax + UITheme.SpaceXxs;
            Rect outRect = new Rect(rect.x, listY, rect.width, rect.yMax - listY - Gap);

            if (rows.Count == 0)
            {
                UIComponents.EmptyState(outRect, T("NoDevices"), T("DevicesEmptySub"));
                return;
            }

            Rect viewRect = new Rect(0f, 0f, outRect.width - UITheme.ScrollbarThickness,
                rows.Count * DeviceRowH);
            // 审计修复（2026-09-30）：出栈进 finally —— 单行绘制抛异常会污染滚动栈，
            // 表现为整窗渲染崩溃且每帧刷错（同设置页既有写法）。
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            try
            {
                // D2 降级：只画可视区间（IMGUI 无自动裁剪，全量画 = 溢出被后画元素遮盖 + 每帧白烧）。
                int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / DeviceRowH) - 1);
                int visible = Mathf.CeilToInt(outRect.height / DeviceRowH) + 2;
                int last = Mathf.Min(rows.Count, first + visible);
                for (int i = first; i < last; i++)
                {
                    DrawDeviceRow(new Rect(0f, i * DeviceRowH, viewRect.width, DeviceRowH), rows[i], i);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawDeviceRow(Rect rect, Thing thing, int index)
        {
            UIComponents.ZebraRow(rect, index, true);   // 整行可点（跳转镜头并选中设备）

            // CHG-20261005-RUIFTABLE（2026-10-05 玩家要求「基于 RUIF 框架模组，优化设备页。
            // 对齐等等前端优化」）：列宽与锚点**都取自同一份定义**，与表头天然一致 ——
            //   · 列宽 = RUIF.TableFmt.ColumnSplit（统一 gap、末列吃余宽）；
            //   · 锚点 = RUIF.TableFmt.ColumnAlignOf(styles, i)（表头读的是**同一个数组**）。
            // 原先两处各自硬编码 TextAnchor、各自算列宽（还带 numPad 偏移）⇒ 表头与数字必然错位。
            bool compact = rect.width < DeviceCompactWidth;
            float[] shares = compact ? DeviceColSharesCompact : DeviceColSharesFull;
            RUIF.StatCol[] styles = compact ? DeviceColStylesCompact : DeviceColStylesFull;
            float[] w = deviceColWidths;
            RUIF.TableFmt.ColumnSplit(rect, shares, DeviceColGap, w);

            // 列下标 —— CHG-20261005-COLTUNE3（移除「效率 / 位置」）后，
            // 全档 8 列与窄档 7 列的**前 7 列完全同号**，只差末尾的「建立时间」：
            //   全档 0名称 1操作员 2状态 3制作 4产出 5运行 6产值 7建立（8 列）
            //   窄档 0名称 1操作员 2状态 3制作 4产出 5运行 6产值      （7 列）
            const int iName = 0;
            const int iWorker = 1;
            const int iState = 2;
            const int iBill = 3;
            const int iOut = 4;
            const int iRun = 5;
            const int iValue = 6;
            int iBorn = compact ? -1 : 7;
            float x = rect.x;
            string stateKey = ResolveStateKey(thing);
            string stateText = EquipmentStateText.TranslationKeyOf(stateKey).Translate().ToString();

            // 行文本取刷新期快照（下标见 BuildDeviceRowTextCache）：0 工单 / 1 产出 / 2 运行
            // / 3 产值 / 4 建立 / 5 效率 / 6 别名 / 7 操作员。
            // 未命中（新建设施尚未刷新）⇒ 逐格回落，不留空。
            string[] rowText = DeviceRowTextOf(thing);
            string billText = rowText != null ? rowText[0] : CurrentBillText(thing);
            string outText = rowText != null ? rowText[1] : "--";
            string runText = rowText != null
                ? rowText[2]
                : RuntimeText(Manager != null ? Manager.DeviceLedger.RunTicksOf(thing) : 0L);
            string valueText = rowText != null && rowText.Length > 3 ? rowText[3] : "--";
            string bornText = rowText != null && rowText.Length > 4 ? rowText[4] : "--";
            // ⚠ CHG-20261005-COLTUNE3：「效率」列（rowText[5]）已移除 ⇒ 不再解包该格。
            //   快照里仍会生成它（BuildDeviceRowTextCache 未改）—— 无害，且若将来恢复该列可直接取用。
            string workerText = rowText != null && rowText.Length > 7 ? rowText[7] : "--";

            // ① 名称（别名 · 设备名）+ 品质徽标。
            // 别名让同名设备可区分（原版建筑不可改名，见 MapComponent_WorkshopManager.AliasOf 注释）。
            // ⚠ 每列文本都必须**预截断**（2026-10-02 · UI-009）：`UIComponents.Label` 不做截断
            //（它只在悬停时补 tooltip），而定宽列的动态文本一旦超宽就会溢出压住右邻列 ——
            // 用户实机反馈「有些文字被挤兑」的根因。
            const float NameReserve = 46f;   // 名称右侧留给品质徽标
            // CHG-20261005-DEVRENAME-RUIF2（2026-10-05 玩家要求）：
            //   ① 移除行尾「改名」按钮（原 O-3 裁决作废）⇒ 名称列恢复全宽（不再被按钮吃掉 56px）；
            //   ② 改为**点击设备名称**弹改名对话框；
            //   ③ 名称不再带 A/B/C… 自动编号（见 MapComponent_WorkshopManager.DisplayNameOf）。
            float nameW = w[iName];
            float qualityX = x + nameW - NameReserve;
            Rect nameRect = new Rect(x + UITheme.SpaceXxs, rect.y,
                Mathf.Max(20f, qualityX - x - UITheme.SpaceXxs), rect.height);
            string nameText = Manager != null
                ? Manager.DisplayNameOf(thing)
                : WorkshopStats.DeviceLabelOf(thing);
            // 可点提示：悬停时整块名称区高亮（原版标准可点affordance）。
            bool canRename = Manager != null;
            if (canRename)
            {
                Widgets.DrawHighlightIfMouseover(nameRect);
            }
            UIComponents.Label(nameRect,
                UIComponents.TruncateToWidth(nameText, nameRect.width, UITheme.FontBody, nameRect),
                UITheme.FontBody, UITheme.Text, RUIF.TableFmt.ColumnAlignOf(styles, iName));
            CompQuality qualityComp = thing.TryGetComp<CompQuality>();
            if (qualityComp != null && nameW > NameReserve + 24f)
            {
                UIComponents.Label(
                    new Rect(qualityX, rect.y, NameReserve, rect.height),
                    qualityComp.Quality.GetLabel().ToString(), UITheme.FontLabel,
                    QualityTone(qualityComp.Quality), TextAnchor.MiddleRight);
            }
            // ⚠ 命中区**必须画在整行 Widgets.ButtonInvisible 之前**：后者会吃掉 MouseUp，
            //   若名字的可点判定在其后就永远点不到（Gate-UI P1-9 同一根因）。
            //   命中即 return ⇒ 本帧不再触发"跳转镜头并选中"。
            Rect renameTipRect = new Rect(0f, 0f, 0f, 0f);
            if (canRename)
            {
                if (Widgets.ButtonInvisible(nameRect))
                {
                    OpenDeviceRenameDialog(thing);
                    return;
                }
                renameTipRect = nameRect;
            }
            x += nameW + DeviceColGap;

            // ② 操作员（CHG-20261005-DEVCOLS：玩家指定插在设备名称右侧）。
            Rect workerRect = new Rect(x, rect.y, w[iWorker], rect.height);
            UIComponents.Label(workerRect,
                UIComponents.TruncateToWidth(workerText, workerRect.width, UITheme.FontBody, workerRect),
                UITheme.FontBody, UITheme.Info, RUIF.TableFmt.ColumnAlignOf(styles, iWorker));
            x += w[iWorker] + DeviceColGap;

            // ③ 状态：色点 + 文本（色点固定在列首，文本按列的锚点走）。
            Rect stateRect = new Rect(x, rect.y, w[iState], rect.height);
            Color prev = GUI.color;
            GUI.color = StateColor(stateKey);
            Widgets.DrawBoxSolid(new Rect(stateRect.x + 2f,
                stateRect.y + stateRect.height * 0.5f - 2.5f, 5f, 5f), Color.white);
            GUI.color = prev;
            Rect stateTextRect = new Rect(stateRect.x + 10f, stateRect.y,
                Mathf.Max(10f, stateRect.width - 12f), rect.height);
            UIComponents.Label(stateTextRect,
                UIComponents.TruncateToWidth(stateText, stateTextRect.width, UITheme.FontBody, stateTextRect),
                UITheme.FontBody, StateColor(stateKey), RUIF.TableFmt.ColumnAlignOf(styles, iState));
            x += w[iState] + DeviceColGap;

            // ④ 当前制作：原版 BillStack 首个应做工单（工单名可很长，如「烹饪简单食物 x4 永远」）。
            Rect billRect = new Rect(x, rect.y, w[iBill], rect.height);
            UIComponents.Label(billRect,
                UIComponents.TruncateToWidth(billText, billRect.width, UITheme.FontBody, billRect),
                UITheme.FontBody, UITheme.Text, RUIF.TableFmt.ColumnAlignOf(styles, iBill));
            x += w[iBill] + DeviceColGap;

            // ⑤ 产出（今日 / 本周）：数值列，锚点由列样式决定（表头同源）。
            Rect outRect = new Rect(x, rect.y, w[iOut], rect.height);
            UIComponents.Label(outRect,
                UIComponents.TruncateToWidth(outText, outRect.width, UITheme.FontBody, outRect),
                UITheme.FontBody, UITheme.Muted, RUIF.TableFmt.ColumnAlignOf(styles, iOut));
            x += w[iOut] + DeviceColGap;

            // ⑥ 累计运行。
            Rect runRect = new Rect(x, rect.y, w[iRun], rect.height);
            UIComponents.Label(runRect,
                UIComponents.TruncateToWidth(runText, runRect.width, UITheme.FontBody, runRect),
                UITheme.FontBody, UITheme.Muted, RUIF.TableFmt.ColumnAlignOf(styles, iRun));
            x += w[iRun] + DeviceColGap;

            // ⑦ 累计产值（CHG-20261005-DEVCOLS：玩家指定插在累计运行右侧）。
            Rect valueRect = new Rect(x, rect.y, w[iValue], rect.height);
            UIComponents.Label(valueRect,
                UIComponents.TruncateToWidth(valueText, valueRect.width, UITheme.FontBody, valueRect),
                UITheme.FontBody, UITheme.Warn, RUIF.TableFmt.ColumnAlignOf(styles, iValue));
            x += w[iValue] + DeviceColGap;

            if (iBorn >= 0)
            {
                // ⑧ 建立时间（"第 N 天"；旧档无记录 ⇒ "--"）。窄档隐藏 ⇒ 由行悬停提示补齐。
                Rect bornRect = new Rect(x, rect.y, w[iBorn], rect.height);
                UIComponents.Label(bornRect,
                    UIComponents.TruncateToWidth(bornText, bornRect.width, UITheme.FontLabel, bornRect),
                    UITheme.FontLabel, UITheme.Muted, RUIF.TableFmt.ColumnAlignOf(styles, iBorn));
            }

            // 点击行：跳转镜头并选中设备（原版行为；Automation → Verse 允许）。
            if (Widgets.ButtonInvisible(rect))
            {
                CameraJumper.TryJumpAndSelect(thing);
            }
            // 2026-10-02 减法审计：原写法在**绘制时立即拼串**（每可见设备每帧一次），
            // 改用 TipSignal 延迟求值 —— 只在真正悬停时才拼（同 Schedule.cs / Shifts.cs 既有先例）。
            //
            // CHG-20261005-COLTUNE2/-3：窄档会隐藏「建立时间」列 ⇒ 在悬停提示里**补齐**，
            // 保证"列数降级"不等于"信息丢失"（LAND-009 不藏信息）；全档可见时不重复补。
            // （「效率 / 位置」已按玩家要求在 COLTUNE3 中整列移除；
            //   位置仍出现在本提示的原生行 `thing.Position` 里。）
            bool bornHidden = iBorn < 0;
            TooltipHandler.TipRegion(rect,
                new TipSignal(
                    () =>
                    {
                        // CHG-20261005-DEVRENAME：悬停主标题改用**有效名**（同 §8.3 统一口径），
                        // 否则改名后「行内显示新名、悬停显示旧名」。
                        string tip = (Manager != null
                                ? Manager.EffectiveNameOf(thing)
                                : WorkshopStats.DeviceLabelOf(thing))
                            + "\n" + stateText
                            + "\n" + thing.Position;
                        if (bornHidden)
                        {
                            tip += "\n" + T("ColBorn") + ": " + bornText;
                        }
                        return tip;
                    },
                    thing.thingIDNumber));
            // CHG-20261005-DEVRENAME：改名按钮的悬停提示**必须在整行 TipRegion 之后注册**，
            // 否则会被整行的提示盖住（Gate-UI P1-9）。
            if (renameTipRect.width > 0f)
            {
                TooltipHandler.TipRegion(renameTipRect,
                    new TipSignal(() => T("DeviceRename.ButtonTip"), thing.thingIDNumber ^ 0x52454E));
            }
        }

        /// <summary>
        /// 打开设备改名对话框（CHG-20261005-DEVRENAME / REQ-WORKSHOP-001）。
        ///
        /// <para>复用既有 <see cref="PersonalChronicle.Archive.Dialog_RenameWorkplace"/>（M-2 默认）：
        /// 其加性重载提供 ① 提交前校验（重名/超长 ⇒ 拒绝且<b>不关窗</b>、保留输入）
        /// ② 提示参数通道（把当前默认名注入 <c>{0}</c>）。</para>
        ///
        /// <para>比较集用 <see cref="FilteredDevices"/> 的<b>同源 fresh 列表</b>
        /// （与设备页显示同一份扫描结果）；本方法<b>不</b>另触发全图扫描（PERF-001）。</para>
        /// </summary>
        private void OpenDeviceRenameDialog(Thing thing)
        {
            if (thing == null || Manager == null || thing.Destroyed)
            {
                return;
            }
            List<Thing> comparison = FilteredDevices();
            string current = Manager.DeviceLedger != null
                ? Manager.DeviceLedger.CustomNameOf(thing)
                : null;
            string defaultName = WorkshopStats.DeviceLabelOf(thing);
            Find.WindowStack.Add(new PersonalChronicle.Archive.Dialog_RenameWorkplace(
                "PersonalChronicle.Automation.Tab.DeviceRename.Title",
                "PersonalChronicle.Automation.Tab.DeviceRename.Hint",
                current,
                name =>
                {
                    MapComponent_WorkshopManager.DeviceRenameOutcome outcome =
                        Manager.TryRenameDevice(thing, name, comparison);
                    HandleRenameOutcome(outcome, name);
                },
                name =>
                {
                    // 预校验：即时反馈且不关窗（真正的写入校验仍在 Manager 唯一写口里）。
                    if (thing.Destroyed)
                    {
                        return new AcceptanceReport(T("DeviceRename.ErrGone"));
                    }
                    if (name != null
                        && name.Length > PersonalChronicle.Domain.WorkshopDeviceNaming.MaxLength)
                    {
                        return new AcceptanceReport(
                            "PersonalChronicle.Automation.Tab.DeviceRename.ErrTooLong".Translate(
                                PersonalChronicle.Domain.WorkshopDeviceNaming.MaxLength));
                    }
                    if (name != null && comparison != null)
                    {
                        for (int i = 0; i < comparison.Count; i++)
                        {
                            Thing other = comparison[i];
                            if (other == null || other == thing)
                            {
                                continue;
                            }
                            if (PersonalChronicle.Domain.WorkshopDeviceNaming.Conflicts(
                                    name, Manager.EffectiveNameOf(other)))
                            {
                                return new AcceptanceReport(
                                    "PersonalChronicle.Automation.Tab.DeviceRename.ErrDuplicate".Translate(
                                        Manager.DisplayNameOf(other)));
                            }
                        }
                    }
                    return AcceptanceReport.WasAccepted;
                },
                defaultName));
        }

        /// <summary>改名提交后的提示与刷新（拒绝类结果在对话框预校验已挡住，此处只兜底竞态）。</summary>
        private void HandleRenameOutcome(
            MapComponent_WorkshopManager.DeviceRenameOutcome outcome, string name)
        {
            switch (outcome)
            {
                case MapComponent_WorkshopManager.DeviceRenameOutcome.Ok:
                    // 名称列走 Refresh 期文本快照 ⇒ 必须刷新才能即时反映新名。
                    Refresh();
                    Messages.Message(
                        name == null
                            ? T("DeviceRename.Cleared")
                            : "PersonalChronicle.Automation.Tab.DeviceRename.Done".Translate(name),
                        MessageTypeDefOf.TaskCompletion, false);
                    break;
                case MapComponent_WorkshopManager.DeviceRenameOutcome.DeviceGone:
                    Messages.Message(T("DeviceRename.ErrGone"),
                        MessageTypeDefOf.RejectInput, false);
                    break;
                case MapComponent_WorkshopManager.DeviceRenameOutcome.NotTracked:
                    Messages.Message(T("DeviceRename.ErrNotTracked"),
                        MessageTypeDefOf.RejectInput, false);
                    break;
                case MapComponent_WorkshopManager.DeviceRenameOutcome.TooLong:
                    Messages.Message(
                        "PersonalChronicle.Automation.Tab.DeviceRename.ErrTooLong".Translate(
                            PersonalChronicle.Domain.WorkshopDeviceNaming.MaxLength),
                        MessageTypeDefOf.RejectInput, false);
                    break;
                case MapComponent_WorkshopManager.DeviceRenameOutcome.Duplicate:
                    Messages.Message(T("DeviceRename.ErrDuplicateGeneric"),
                        MessageTypeDefOf.RejectInput, false);
                    break;
            }
        }

        /// <summary>品质档配色（原版 GetLabel 本地化文本着色：低档警示、高档渐亮）。</summary>
        private static Color QualityTone(QualityCategory quality)
        {
            switch (quality)
            {
                case QualityCategory.Awful:
                case QualityCategory.Poor:
                    return UITheme.Threat;
                case QualityCategory.Good:
                    return UITheme.Alive;
                case QualityCategory.Excellent:
                    return UITheme.Info;
                case QualityCategory.Masterwork:
                case QualityCategory.Legendary:
                    return UITheme.Accent;
                default:
                    return UITheme.Muted;
            }
        }

        /// <summary>设备页 KPI 横幅文本（Refresh 期构建）。</summary>
        private string devicesBanner;

        /// <summary>设备当前制作文本：首个应做工单标签（+ 重复信息）；无单显示占位。</summary>
        private static string CurrentBillText(Thing thing)
        {
            try
            {
                IBillGiver giver = thing as IBillGiver;
                if (giver == null || giver.BillStack == null)
                {
                    return "—";
                }
                Bill current = giver.BillStack.FirstShouldDoNow;
                if (current != null)
                {
                    Bill_Production production = current as Bill_Production;
                    string repeat = production != null ? production.RepeatInfoText : string.Empty;
                    return repeat.NullOrEmpty() ? current.LabelCap : current.LabelCap + " " + repeat;
                }
                return giver.BillStack.Count > 0 ? T("DeviceBillsSuspended") : "—";
            }
            catch (Exception)
            {
                return "—";
            }
        }

        /// <summary>运行时长文本：&lt;48h 显示小时，跨天显示 "Xd Yh"。</summary>
        private static string RuntimeText(long ticks)
        {
            float hours = ticks / (float)GenDate.TicksPerHour;
            // 审计修复（2026-09-30 · P3）：单位走 Keyed（旧代码硬编码 "h"/"d"），
            // 数值格式化补 InvariantCulture（旧实现受当前 culture 影响小数分隔符）。
            if (hours < 48f)
            {
                return (KeyPrefix + "RuntimeHours").Translate(
                    hours.ToString("0.#", CultureInfo.InvariantCulture)).ToString();
            }
            int days = (int)(hours / 24f);
            int restHours = (int)hours % 24;
            return (KeyPrefix + "RuntimeDaysHours").Translate(
                days.ToString(CultureInfo.InvariantCulture),
                restHours.ToString(CultureInfo.InvariantCulture)).ToString();
        }

        /// <summary>
        /// 设备实时状态键。复用制造中心 ② 的<b>单一真源</b>
        /// <see cref="EquipmentStateResolver"/>（纯函数，已有离线单测），
        /// 与车间俯视图/设备详情完全同源，不另立口径。
        /// </summary>
        private static string ResolveStateKey(Thing thing)
        {
            // 唯一实现（2026-09-11 Phase 2 抽取）：设备自报优先 → 原版 comp 反推 → 异常隔离。
            // 设备页 / 倒班表稼动率 / 交接点检表三处同源。
            return WorkshopDeviceStateResolver.KeyOf(thing);
        }

        /// <summary>状态键 → 语义色（仅取色，文案一律走 <see cref="EquipmentStateResolver"/>）。</summary>
        private static Color StateColor(string stateKey)
        {
            switch (stateKey)
            {
                case EquipmentStateResolver.KeyRun: return UITheme.Alive;
                case EquipmentStateResolver.KeyFault: return UITheme.Dead;
                case EquipmentStateResolver.KeyOff: return UITheme.Dim;
                default: return UITheme.Muted;
            }
        }

        /// <summary>
        /// 重建人员页的「今日岗位 / 主技能」快照（<c>Refresh()</c> 期调用；幂等）。
        ///
        /// <para>2026-10-02：人员页原先只有「姓名 / 当前活动」，玩家无法一眼看出
        /// "这个人归哪个班、能不能干活"。新增两列：① <b>今日岗位</b> = 所属班组 + 其指派班次的时段
        /// （读已持久化的 <c>crew.AssignedShifts</c>，零计算）；② <b>主技能</b> = 最强技能与等级。
        /// 两者都在此算好，绘制路径只做字典查找（PERF-002：不把计算放进每帧路径）。</para>
        /// </summary>
        private void RebuildMemberInfoCache()
        {
            memberDutyCache.Clear();
            memberSkillCache.Clear();
            if (zone == null || zone.Members == null)
            {
                return;
            }
            var crewLabels = new Dictionary<WorkshopCrew, string>();
            List<WorkshopCrew> crews = zone.Crews;
            for (int i = 0; i < crews.Count; i++)
            {
                if (crews[i] != null)
                {
                    crewLabels[crews[i]] = BuildCrewDutyLabel(crews[i]);
                }
            }
            List<Pawn> members = zone.Members;
            for (int i = 0; i < members.Count; i++)
            {
                Pawn pawn = members[i];
                if (pawn == null || pawn.Destroyed)
                {
                    continue;
                }
                string duty = T("Unassigned");
                for (int c = 0; c < crews.Count; c++)
                {
                    WorkshopCrew crew = crews[c];
                    // 一人多组（读档遗留）取首个 —— 与 Zone_Workshop.DedupeCrewMemberships 同口径。
                    if (crew != null && crew.Members != null && crew.Members.Contains(pawn))
                    {
                        duty = crewLabels[crew];
                        break;
                    }
                }
                memberDutyCache[pawn] = duty;
                string skill = WorkshopPawnAccess.TopSkillsOf(pawn, 1);
                memberSkillCache[pawn] = string.IsNullOrEmpty(skill) ? T("NoSkill") : skill;
            }
        }

        /// <summary>班组 → 「白班 · 08:00 – 20:00」；未指派班次时只显班组名。</summary>
        private string BuildCrewDutyLabel(WorkshopCrew crew)
        {
            if (crew == null)
            {
                return string.Empty;
            }
            string label = crew.Name ?? string.Empty;
            if (crew.AssignedShifts == null)
            {
                return label;
            }
            for (int i = 0; i < crew.AssignedShifts.Count; i++)
            {
                WorkshopShift shift = crew.AssignedShifts[i];
                if (shift == null || (zone != null && shift.TargetZone != zone))
                {
                    continue;
                }
                return label + " · " + WorkshopHours.LabelOf(shift.StartHour, shift.EndHour);
            }
            return label;
        }

        // ---- 人员页：玩家手动指派名单（持久化在 zone.Members）-----------------
        private void DrawMembers(Rect rect)
        {
            // 只读快照：死引用清理已移入 Refresh()→PurgeDeadMembers()（2026-09-11 体检 P1-3，
            // 此前在绘制路径每帧改写持久字段 zone.Members）。
            List<Pawn> roster = zone.Members;

            // 顶部行：提示（左）+ 添加人员按钮（右）。
            Rect hintRect = new Rect(rect.x, rect.y, rect.width - AddBtnW - Gap, SearchH);
            UIComponents.Label(hintRect, T("MembersHint"),
                UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);
            Rect addBtn = new Rect(rect.xMax - AddBtnW, rect.y, AddBtnW, SearchH);
            if (UIComponents.ThemedButton(addBtn, T("AddMember")))
            {
                OpenAddMemberMenu();
            }

            Rect headRect = new Rect(rect.x, rect.y + SearchH + Gap, rect.width, HeaderH);
            float removeColShare = RemoveBtnW / rect.width;
            // CHG-20261004-UIROWS：活动列吃余量，但余量不足 ActMinW 即**整列隐藏**
            // （表头与行必须用同一判定，否则出现「有标题没内容」或反之）。绝不画负宽。
            float actShare = 1f - (ColNameShare + ColDutyShare + ColSkillShare + ColPostShare) - removeColShare;
            bool showActivity = rect.width * Mathf.Max(0f, actShare) >= ActMinW;
            UIComponents.TableHeader(headRect,
                new[]
                {
                    T("ColMemberName"), T("ColDuty"), T("ColSkill"),
                    T("ColPost"), showActivity ? T("ColActivity") : string.Empty, string.Empty
                },
                new[]
                {
                    ColNameShare, ColDutyShare, ColSkillShare, ColPostShare,
                    showActivity ? Mathf.Max(0f, actShare) : 0f, removeColShare
                });

            float listY = headRect.yMax + UITheme.SpaceXxs;
            Rect outRect = new Rect(rect.x, listY, rect.width, rect.yMax - listY - Gap);
            if (roster.Count == 0)
            {
                UIComponents.EmptyState(outRect, T("NoMembers"), T("MembersEmptySub"));
                return;
            }

            Rect viewRect = new Rect(0f, 0f, outRect.width - UITheme.ScrollbarThickness,
                roster.Count * RowH);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            try
            {
                // D2 降级：只画可视区间（IMGUI 不裁剪，全量画既溢出又白烧）。
                int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowH) - 1);
                int visible = Mathf.CeilToInt(outRect.height / RowH) + 2;
                int last = Mathf.Min(roster.Count, first + visible);
                for (int i = first; i < last; i++)
                {
                    if (roster[i] == null)
                    {
                        continue;
                    }
                    // 审计修复（2026-09-30）：绘制路径**不直接改持久名单** —— 点击只登记待移除，
                    // 帧末走域入口 `Zone_Workshop.RemoveMember` + `OnRosterChanged`（旧代码
                    // 裸 RemoveAt 且不通知 ⇒ 调度计划不重算、与班组删除路径两套写法）。
                    if (DrawMemberRow(new Rect(0f, i * RowH, viewRect.width, RowH), roster[i], i, showActivity)
                        && pendingRemoveMember == null)
                    {
                        pendingRemoveMember = roster[i];
                    }
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }

            if (pendingRemoveMember != null)
            {
                Pawn removing = pendingRemoveMember;
                pendingRemoveMember = null;
                if (zone != null)
                {
                    zone.RemoveMember(removing);
                    OnRosterChanged();
                }
            }
        }

        /// <summary>返回 true 表示该行「移除」按钮被点击（调用方按索引删一行）。</summary>
        /// <param name="showActivity">
        /// 「活动」列是否可见 —— 由 <see cref="DrawMembers"/> 依余量预算<b>统一</b>判定后传入，
        /// 表头与行共用同一结论，杜绝「有标题没内容 / 有内容没标题」。CHG-20261004-UIROWS。
        /// </param>
        private bool DrawMemberRow(Rect rect, Pawn pawn, int index, bool showActivity)
        {
            UIComponents.ZebraRow(rect, index);

            // 名单清理后仍可能有相邻 tick 内销毁的成员，行绘制前做兜底。
            if (pawn == null || pawn.Destroyed)
            {
                UIComponents.Label(rect, "--", UITheme.FontBody, UITheme.Muted, TextAnchor.MiddleLeft);
                return false;
            }

            float nameW = rect.width * ColNameShare;
            float dutyW = rect.width * ColDutyShare;
            float skillW = rect.width * ColSkillShare;
            // 2026-10-03 用户定责：新增「岗位」列，宽度自活动列扣除（活动文本已有预截断，最能承压）。
            float postW = rect.width * ColPostShare;
            // CHG-20261004-UIROWS：活动列宽由调用方按余量预算决定并 clamp，
            // 此处只消费结论（不再自己用减法凑宽度 —— 那正是负宽的来源）。
            float actW = showActivity
                ? Mathf.Max(0f, rect.width - nameW - dutyW - skillW - postW - RemoveBtnW - Gap)
                : 0f;
            // 预截断（2026-10-02 · UI-009）：活动文本来自原版 job report（长度不可控），右侧紧邻
            // 移除按钮，不截断必然压过去。
            Rect mNameRect = new Rect(rect.x + UITheme.SpaceXxs, rect.y, nameW - 8f, rect.height);
            UIComponents.Label(mNameRect,
                UIComponents.TruncateToWidth(pawn.LabelShortCap, mNameRect.width,
                    UITheme.FontBody, mNameRect),
                UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft);
            // 2026-10-02 新增两列：今日岗位 / 主技能（Refresh 期算好，这里只读字典 + 预截断，零计算）。
            memberDutyCache.TryGetValue(pawn, out string dutyText);
            Rect mDutyRect = new Rect(rect.x + nameW, rect.y, dutyW - 6f, rect.height);
            UIComponents.Label(mDutyRect,
                UIComponents.TruncateToWidth(dutyText ?? string.Empty, mDutyRect.width,
                    UITheme.FontLabel, mDutyRect),
                UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);
            memberSkillCache.TryGetValue(pawn, out string skillText);
            Rect mSkillRect = new Rect(rect.x + nameW + dutyW, rect.y, skillW - 6f, rect.height);
            UIComponents.Label(mSkillRect,
                UIComponents.TruncateToWidth(skillText ?? string.Empty, mSkillRect.width,
                    UITheme.FontLabel, mSkillRect),
                UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);
            // CHG-20261004-POSTOVERRIDE：岗位列改为**可点击**（此前是纯只读 Label，
            // 玩家无法在人员页指定岗位）。口径 = 先自动分配、再允许手动覆盖：
            // 缺省显示分配器结果；手动指定的项追加 "*" 标记。
            //
            // 控件选型：走 `UIComponents.ThemedButton` + FloatMenu，与工程既有同类选择器
            // **完全同范式**（`Dialog_WorkshopShiftEditor.cs:144` 的岗位选择器）。
            // ⛔ 不用原版 `Widgets.ButtonText` —— 它是琥珀色默认皮肤，与本工程深色令牌主题
            // 不一致，且属组件绘制规范里的「同功能两处实现」反模式。
            // `ThemedButton` 的令牌来自 `UITheme`，而 `UITheme` 在 RUIF 可用时会被
            // `RuifBridge` 同步为 RUIF 令牌（`RuifBridge.cs:91`）⇒ 视觉语言仍源自 RUIF。
            string postText = PostLabelOfPawn(pawn);
            string postLabel = string.IsNullOrEmpty(postText) ? T("PostUnassigned") : postText;
            if (IsPostOverridden(pawn)) { postLabel = postLabel + " *"; }
            // CHG-20261004-UIROWS：整格铺满的按钮让岗位列变成三条等宽「色块柱」，笨重且与表头
            // 视觉错位（`TableHeader` 硬编码 MiddleLeft，而 `ThemedButton` 是 MiddleCenter）。
            // 改为**宽度自适应内容**的 chip：宽度贴合文字后居中与左排在视觉上重合 ⇒ 与表头对齐，
            // 整列呼吸感回归；`ColPostShare` 降级为 chip 的可用上限。
            // 测宽走框架度量（组件绘制规范铁律 1：禁止手写宽度魔数）。
            // ⚠ `Text.CalcSize` 依赖当前 `Text.Font`，必须**成对**设置/还原 ——
            //   沿用工程既有范式（`WorkshopManageView.Schedule.cs:1127`、`UIComponents.cs:214`）。
            //   注意 `CalcSize` 无缓存重载（1.6 只有 1 参版），故不可省这一步。
            GameFont prevChipFont = Verse.Text.Font;
            Verse.Text.Font = UITheme.FontLabel;
            float chipTextW = Verse.Text.CalcSize(postLabel).x;
            Verse.Text.Font = prevChipFont;
            float chipW = Mathf.Min(postW - UITheme.SpaceSm, chipTextW + UITheme.SpaceXs * 2f);
            // 垂直几何与右侧移除键完全一致（RowCtrlInset 内缩），保证同行元素基线齐平。
            Rect chipRect = new Rect(rect.x + nameW + dutyW + skillW + UITheme.SpaceXxs,
                rect.y + RowCtrlInset, chipW, RowH - RowCtrlInset * 2f);
            if (UIComponents.ThemedButton(chipRect, postLabel))
            {
                OpenPostOverrideMenu(pawn);
            }
            if (showActivity)
            {
                Rect mActRect = new Rect(rect.x + nameW + dutyW + skillW + postW, rect.y, actW, rect.height);
                UIComponents.Label(mActRect,
                    UIComponents.TruncateToWidth(ActivityText(pawn), mActRect.width,
                        UITheme.FontBody, mActRect),
                    UITheme.FontBody, UITheme.Muted, TextAnchor.MiddleLeft);
            }

            Rect rmBtn = new Rect(rect.xMax - RemoveBtnW, rect.y + RowCtrlInset,
                RemoveBtnW, RowH - RowCtrlInset * 2f);
            return UIComponents.ThemedButton(rmBtn, T("RemoveMark"));
        }

        /// <summary>人员当前活动：摸鱼优先标注，其次精神崩溃，否则用当前工作的报告文本。</summary>
        /// <remarks>
        /// CHG-20261004-IDLE：摸鱼标注**排在精神崩溃之前**是有意的 —— 摸鱼时段的人按设计
        /// 就不会有值勤 Job，若不先标注就会显示成"--"或原版娱乐报告（如"在玩象棋"），
        /// 玩家看不出"这是车间判定他没工位"，会以为系统又坏了。
        /// 判定读作息表当前格（<see cref="MapComponent_WorkshopManager.IsOnIdleAssignment"/>），
        /// 与值勤 Job 的下发条件同源。
        /// </remarks>
        private static string ActivityText(Pawn pawn)
        {
            if (MapComponent_WorkshopManager.IsOnIdleAssignment(pawn))
            {
                return T("ColActivityIdle");
            }
            if (pawn.InMentalState)
            {
                return pawn.MentalStateDef.LabelCap;
            }
            Job job = pawn.CurJob;
            return job != null ? job.GetReport(pawn) : "--";
        }

        /// <summary>添加人员：FloatMenu 列出本地图尚未指派的自由殖民者。</summary>
        private void OpenAddMemberMenu()
        {
            var options = new List<FloatMenuOption>();
            List<Pawn> colonists = zone.Map.mapPawns.FreeColonists;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn candidate = colonists[i];
                if (!zone.Members.Contains(candidate))
                {
                    options.Add(new FloatMenuOption(
                        candidate.LabelShortCap, () => zone.AddMember(candidate)));
                }
            }
            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption(T("NoCandidates"), null));
            }
            Find.WindowStack.Add(new FloatMenu(options, T("AddMember")));
        }

        // ---- 排班页：固定班 + 轮岗班 ------------------------------------------
        // 人员来源 = zone.Members（玩家手动指派名单），与人员页联动：
        // 成员页增删即时反映到可排班候选池，无需两处维护。

        /// <summary>本图的车间调度器（槽位/轮岗规则的持有者）。</summary>
        private MapComponent_WorkshopManager Manager
        {
            get
            {
                return zone != null && zone.Map != null
                    ? MapComponent_WorkshopManager.Get(zone.Map)
                    : null;
            }
        }

        /// <summary>排班页第三区块已于 2026-09-25 改为「绩效」，原三档视图 scheduleViewMode 移除。</summary>

        /// <summary>
        /// 班组/班次编制变化后的失效通知：重算调度计划 + 同步原版时间管制表 +
        /// 联动原版工作面板勾选 + 刷新视图快照。
        ///
        /// <para>2026-10-02 新增「联动原版工作面板」（EXC-2026-012，仅 Enable）：旧行为下即便
        /// 排好班，车间值勤工作类型在原版工作面板里仍是未勾选状态 ⇒ <c>JobGiver_Work</c> 不会
        /// 派值勤 Job，成员表现为「安排了班次却闲置」。本调用是该写入的<b>唯一</b>事件入口。</para>
        /// </summary>
        private void OnRosterChanged()
        {
            Manager?.MarkDutyPlanDirty();
            Manager?.SyncVanillaTimetables(zone);
            Manager?.SyncVanillaWorkSettings(zone);
            // 「加入车间即隐藏原版工作」= 排班/名册变更时自动接管（CHG-20261003-TAKEOVER-R2）。
            // 复用既有的排班事件路径，**不新增触发点**（EXC-2026-016 F4 已相应修订）。
            // 幂等：已是 0 的工作会被跳过；台账首记优先，不会越写越低。
            // 硬红线（搬运/清洁/医疗/灭火）与自由人兜底在 TakeOverVanillaWork 内部把关。
            Manager?.SyncVanillaWorkTakeover(zone);
            // CHG-20261004-AUTOHIDE：「加入车间即自动隐藏」——把车间全体成员（人员名单 ∪ 班组编制）
            // 写入【工作】面板隐藏名单，玩家不必再逐个点 gizmo 隐藏。
            // 与接管同一事件入口、同一份成员口径 ⇒ 不可能出现"隐藏了却仍在原版工作"的分叉状态。
            Manager?.SyncAutoHideWorkTabColonists(zone);
            // CHG-20261005-NODEMOTE：名册变更同样要跑差集对账（此刻最清楚"谁被移出了"）。
            // ⚠ NODEMOTE-AUDIT（P2 修复）：原先此处直调 + 紧随其后的 Refresh() 内再调一次
            //   ⇒ 每次名册事件白跑一整轮成员收集与分配（第二次差集恒空）。
            //   现只保留 Refresh 内的那一次 —— 它同时覆盖"开面板"与"名册变更"两条路径
            //   （本方法末尾就调 Refresh），且顺序已保证 SyncAutoHide 在前。
            Refresh();
        }

        /// <summary>
        /// 按当前排序列/方向重排 <see cref="devices"/>（CHG-20261005-TABLESORT）。
        ///
        /// <para><b>为什么在 Refresh 期而不是绘制期</b>：绘制每帧都跑，排序（O(n log n)，
        /// 且比较器要查台账）放进去就是每帧白烧；Refresh 只在开面板 / 点刷新 / 点表头时触发。</para>
        ///
        /// <para><b>排序键</b>：文本列取 Refresh 期快照（不拼串、不查台账）；数值列取台账原始值
        /// —— <b>不解析显示文本</b>（"1,080" / "13 天 21 小时" 这类格式化字符串按数字比较会全错）。
        /// 降序由外层取负，不各列写一套。</para>
        ///
        /// <para><b>稳定性</b>：同值时按 <c>thingIDNumber</c> 兜底 ⇒ 反复刷新不会让同值行乱跳。</para>
        /// </summary>
        private void ApplyDeviceSort()
        {
            if (deviceSortCol < 0 || deviceSortState == RUIF.SortState.None || devices.Count < 2)
            {
                return;
            }
            WorkshopDeviceLedger ledger = Manager != null ? Manager.DeviceLedger : null;
            int today = GenDate.DaysPassed;
            bool desc = deviceSortState == RUIF.SortState.Desc;
            // 比较器里现算键：设备数是几十量级 ⇒ n log n 次廉价取值，比预生成键数组更省事
            // （预生成要多两份临时数组与一次额外遍历）。
            devices.Sort((a, b) =>
            {
                int cmp = CompareDevicesBySort(a, b, ledger, today);
                return desc ? -cmp : cmp;
            });
        }

        /// <summary>设备排序比较器（键的口径见 <see cref="ApplyDeviceSort"/>）。</summary>
        private int CompareDevicesBySort(Thing a, Thing b, WorkshopDeviceLedger ledger, int today)
        {
            int cmp;
            // CHG-20261005-COLTUNE：移除耐久列后「当前制作」由 4 号变 3 号 ⇒ 文本列集合随之调整。
            if (deviceSortCol == 0 || deviceSortCol == 1
                || deviceSortCol == 2 || deviceSortCol == 3)
            {
                cmp = string.CompareOrdinal(DeviceSortTextOf(a, deviceSortCol),
                    DeviceSortTextOf(b, deviceSortCol));
            }
            else
            {
                cmp = DeviceSortNumber(a, ledger, today)
                    .CompareTo(DeviceSortNumber(b, ledger, today));
            }
            // 同值兜底：按 thingID 稳定（否则每次刷新同值行的相对顺序会跳）。
            return cmp != 0 ? cmp : a.thingIDNumber.CompareTo(b.thingIDNumber);
        }

        /// <summary>
        /// 文本列的排序键（<b>全部取自 Refresh 期快照</b>）。
        /// <para>名称列用**有效名**。⚠ CHG-20261005-DEVRENAME-RUIF2（2026-10-05 玩家要求移除自动编号）：
        /// 原先名称列取别名（A/B/C…），但该列现在显示的已是不带编号的有效名 ⇒
        /// 若排序仍按别名，就成了"显示的是名字、排序按看不见的字母"，玩家无法预期。</para>
        /// </summary>
        private string DeviceSortTextOf(Thing thing, int col)
        {
            string[] rowText = DeviceRowTextOf(thing);
            switch (col)
            {
                case 0:
                    return Manager != null ? Manager.EffectiveNameOf(thing) : WorkshopStats.DeviceLabelOf(thing);
                case 1:
                    return rowText != null && rowText.Length > 7 ? rowText[7] : string.Empty;
                case 2:
                    return ResolveStateKey(thing);
                default:   // 当前制作
                    return rowText != null ? rowText[0] : string.Empty;
            }
        }

        /// <summary>数值列的排序键（台账原始值；无记录按 0，效率/耐久无数据按 -1 排最低）。</summary>
        private float DeviceSortNumber(Thing thing, WorkshopDeviceLedger ledger, int today)
        {
            // 数值列下标（CHG-20261005-COLTUNE：移除耐久后 4..9 依次前移一格）
            switch (deviceSortCol)
            {
                case 4:   // 产出 —— CHG-20261005-SORTWEEK（2026-10-05 玩家要求「产出排序以本周数量为准」）：
                    // 改按列显示的**后半段**排序（即 `今日 / 本周` 里的"本周"），
                    // 口径与列显示**同源** = ProducedInWindowOf（设备页「产出」列后半段与顶部 KPI
                    // 横幅用的就是它）⇒ 不会出现"显示 168、却按 8（今日）排"的名实错位。
                    // ⚠ 列标题写「本周」而底层是 **5 天滚动窗口**（2026-10-02 由"今日产量"改的口径）——
                    //   这处名实不符是既有口径，本批只对齐排序、不动显示（改标题会牵动语言文件）。
                    return ledger != null ? ledger.ProducedInWindowOf(thing, today) : 0f;
                case 5:   // 累计运行（tick）
                    return ledger != null ? ledger.RunTicksOf(thing) : 0f;
                case 6:   // 累计产值
                    return ledger != null ? ledger.ProducedValueOf(thing) : 0f;
                case 7:   // 建立时间（台账首次看到的 tick）
                    return ledger != null ? ledger.FirstSeenTickOf(thing) : 0f;
                default:  // 不该到达（文本列由 CompareDevicesBySort 提前分流）；返回 0 保证比较器可用
                    return 0f;
            }
        }

        /// <summary>
        /// 设备页要显示的行（CHG-20261005-COLTUNE：搜索框移除后**不再过滤**，
        /// 直接返回 <see cref="devices"/> —— 其顺序已由 Refresh 期的 <c>ApplyDeviceSort</c> 定好）。
        /// <para>保留方法名与调用点：将来若重新需要过滤/分组，这里是唯一入口
        /// （不要在各页面里各写一遍取行逻辑）。</para>
        /// </summary>
        private List<Thing> FilteredDevices()
        {
            return devices;
        }

        // ---- 配方页：区内设备的工单（BillStack）--------------------------------

        // ---- 设置页：车间类型（基于环世界原版生产设备风格分类）----------------

        // CHG-20261004-AUTOTYPE：原先这里是 `bool autoTypeTried`「每次开窗口仅尝试一次」
        // （手动清除后靠"试过就不试"的副作用不再回设）。该写法导致"先开面板（无设备）
        // 再放设备"永不判定 ⇒ 岗位页恒为「尚未实现」。现由
        // `lastAutoTypeCapacity`（设备数快照，变化即重试）+ `autoTypeSuppressedByPlayer`
        // （显式表达"玩家清除过"）替代，字段已移除。

        /// <summary>人员页「移除」点击的待执行目标（绘制期只登记，帧末执行）。</summary>
        private Pawn pendingRemoveMember;

        /// <summary>
        /// 原版「工作」面板联动的常驻披露行（EXC-2026-012 + EXC-2026-013 的硬要求）。
        ///
        /// <para>为什么需要它：车间排班会把车间类型映射的原版工作提升到最高档，但原版<b>只在
        /// 「手动优先级」模式下读取档位</b> —— 简单模式下所有已启用工作等价
        /// （<c>Pawn_WorkSettings.GetPriority</c> 恒返回 3），「车间工作优先度最高」不可达。
        /// 此处常驻显示当前模式；未开启时给出一键开启出口。</para>
        ///
        /// <para>纪律：改设置的唯一入口在 Manager（<c>EnableManualWorkPriorities</c>），
        /// 本方法只读状态 + 发命令（MATRIX-003 / UI-003）。</para>
        /// </summary>
        private void DrawWorkPriorityNotice(Rect row)
        {
            PlaySettings play = Find.PlaySettings;
            bool manual = play != null && play.useWorkPriorities;

            // 预截断 + 截断补偿（UI-009）：工程内 Label **不**做截断（它只补悬停），溢出会被
            // 随后绘制的元素遮盖 ⇒ 调用方必须先截断；带 tipRect 的重载在结构上保证"截断即补偿原文"。
            Rect textRect = new Rect(row.x, row.y,
                Mathf.Max(0f, manual ? row.width : row.width - WorkPrioBtnW - Gap), row.height);
            string text = manual ? T("WorkPrioManualOn") : T("WorkPrioManualOff");
            UIComponents.Label(textRect,
                UIComponents.TruncateToWidth(text, textRect.width, UITheme.FontLabel, textRect),
                UITheme.FontLabel, manual ? UITheme.Muted : UITheme.Warn, TextAnchor.MiddleLeft);

            if (!manual)
            {
                if (UIComponents.ThemedButton(
                        new Rect(row.xMax - WorkPrioBtnW, row.y, WorkPrioBtnW, row.height),
                        T("WorkPrioEnable")))
                {
                    // UI 只发命令：改设置的唯一入口在 Manager（MATRIX-003 / UI-003）。
                    Manager?.EnableManualWorkPriorities();
                }
            }
            // 2026-10-02 减法审计：原写法每帧取词（T() = KeyPrefix 拼接 + Translate）；
            // 0x505249 = "PRI"，仅作 TipSignal 的缓存键（文本在 lambda 里延迟求值）。
            TooltipHandler.TipRegion(row,
                new TipSignal(() => T("WorkPrioHint"), 0x505249));
        }

        /// <summary>
        /// 车间类型选择页。类型决定本车间提供的<b>专向风格 buff</b>（工作速度 / 品质 / 研究速度，
        /// 由 <see cref="WorkshopTypeDef.workSpeedBonusPct"/> 等字段定义，显示数值 = 生效数值）。
        /// 未指定类型时：自动按「车间内设备风格占比最高」的类型设置；否则展示 10 个原版设备风格类型。
        /// </summary>
        private void DrawSettings(Rect rect)
        {
            if (!IsZoneAlive())
            {
                return;
            }
            // 注：自动按设备风格占比判定类型已移至 Refresh()（审计修复：绘制路径不得写持久数据）。
            WorkshopTypeDef cur = zone.WorkshopType;
            // 头部：当前类型 + 固定 buff 摘要（数值即 Resolver 生效数值，显示 = 生效）。
            string headBuff = cur != null ? TypeBuffSummary(cur) : string.Empty;
            UIComponents.Label(new Rect(rect.x, rect.y, rect.width, HeaderH),
                T("TypeCurrent") + (cur != null ? cur.label : T("TypeNone"))
                    + (string.IsNullOrEmpty(headBuff) ? string.Empty : " · " + headBuff),
                UITheme.FontBody, UITheme.Muted, TextAnchor.MiddleLeft);

            List<WorkshopTypeDef> types = DefDatabase<WorkshopTypeDef>.AllDefsListForReading;
            int typeCount = 0;
            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] != null)
                {
                    typeCount++;
                }
            }
            int rowCount = (typeCount + 1) / 2;
            // 副行（buff 摘要）行高走 RUIF 度量（禁行高魔数）。
            float subH = RuifBridge.StatRowHeight();
            // 内容高 = 头 + 类型网格 + 「清除类型」+ 「工作优先级联动」状态行
            // + 「车间专属工位」状态行（CHG-20261003 新增，EXC-015 的可逆出口）。
            // 内容高 = 头 + 类型网格 + 「清除类型」+ 「工作优先级联动」状态行
            // + 「车间专属工位」状态行（CHG-20261003，EXC-015 的可逆出口）
            // + 「接管原版工作」行（CHG-20261003-TAKEOVER，EXC-2026-016 的接管/解除出口）。
            float contentH = HeaderH + Gap + rowCount * (RowH + subH + 2f + Gap)
                + RowH + Gap + RowH + Gap + RowH + Gap + RowH;

            // 空间预算（铁律 4）：类型多 / 窗口矮时降级为滚动（D2），绝不硬挤或画到容器外；
            // 出栈进 finally（滚动栈失衡会污染整窗渲染）。
            Rect view = rect;
            bool scrollOn = contentH > rect.height;
            if (scrollOn)
            {
                Rect inner = new Rect(0f, 0f, rect.width - 20f, contentH);
                Widgets.BeginScrollView(rect, ref settingsScroll, inner);
                view = inner;
            }
            try
            {
                float colW = (view.width - Gap) / 2f;
                float rowStep = RowH + subH + 2f;
                float top = view.y + HeaderH + Gap;
                int drawn = 0;
                for (int i = 0; i < types.Count; i++)
                {
                    WorkshopTypeDef type = types[i];
                    if (type == null)
                    {
                        continue;
                    }
                    float x = view.x + (drawn % 2) * (colW + Gap);
                    float y = top + (drawn / 2) * rowStep;
                    drawn++;
                    Rect btn = new Rect(x, y, colW, RowH);
                    if (UIComponents.ThemedButton(btn, type.label))
                    {
                        SetWorkshopType(type);
                    }
                    if (cur == type)
                    {
                        // 选中态由调用方表达（组件零状态）：accent 边框 + 左侧色条。
                        UIComponents.Border(btn, UITheme.Accent);
                        RUIF.Draw.Box(new Rect(btn.x, btn.y, 3f, btn.height), UITheme.Accent);
                    }
                    // 副行：类型固定 buff（如「工作速度 +10%」），数值即生效数值。
                    string sub = TypeBuffSummary(type);
                    if (!string.IsNullOrEmpty(sub))
                    {
                        UIComponents.Label(new Rect(x + UITheme.SpaceXxs, y + RowH + 1f, colW - 8f, subH),
                            sub, UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);
                    }
                }

                // 「清除类型」：整行通栏（旧实现只占左半列，右侧留白失衡）。
                if (UIComponents.ThemedButton(new Rect(view.x, top + rowCount * rowStep, view.width, RowH),
                        T("TypeClear")))
                {
                    SetWorkshopType(null);
                }

                // 原版工作优先级联动状态行（EXC-2026-012 + EXC-2026-013 的玩家可见披露 + 出口）。
                float noticeY = top + rowCount * rowStep + RowH + Gap;
                DrawWorkPriorityNotice(new Rect(view.x, noticeY, view.width, RowH));

                // 车间专属工位状态行 + 一键全解除（CHG-20261003 / EXC-2026-015）。
                DrawWorkStationStatusRow(new Rect(view.x, noticeY + RowH + Gap,
                    view.width, RowH));

                // 「接管原版工作」行（CHG-20261003-TAKEOVER · Phase 2 / EXC-2026-016）。
                // **默认关闭**：只有玩家点按钮才发起接管；不点就永远不动原版工作面板。
                DrawTakeoverRow(new Rect(view.x, noticeY + (RowH + Gap) * 2f, view.width, RowH));
            }
            finally
            {
                if (scrollOn)
                {
                    Widgets.EndScrollView();
                }
            }
        }

        /// <summary>
        /// 车间内设备风格占比最高的类型：按各类型 <c>expectedDeviceDefNames</c> 统计命中设备数，
        /// 取命中最多者。无命中（如全为自动化/传送带段等无锚点设备）返回 null。
        /// </summary>
        private WorkshopTypeDef DominantWorkshopType()
        {
            if (devices == null || devices.Count == 0)
            {
                return null;
            }
            WorkshopTypeDef best = null;
            int bestCount = 0;
            foreach (WorkshopTypeDef type in DefDatabase<WorkshopTypeDef>.AllDefsListForReading)
            {
                if (type == null || type.expectedDeviceDefNames == null || type.expectedDeviceDefNames.Count == 0)
                {
                    continue;
                }
                int c = 0;
                for (int i = 0; i < devices.Count; i++)
                {
                    Thing d = devices[i];
                    if (d != null && WorkshopDeviceIdentity.IsAccepted(type, d.def.defName))
                    {
                        c++;
                    }
                }
                if (c > bestCount)
                {
                    bestCount = c;
                    best = type;
                }
            }
            return bestCount > 0 ? best : null;
        }

        /// <summary>
        /// 类型固定 buff 摘要（按钮副行与页头）：工作速度 +X% / 品质 +X / 研究速度 +X%。
        /// 数值与 <see cref="WorkshopBonusResolver.Resolve"/> 的生效数值一致（显示 = 生效）。
        /// </summary>
        /// <summary>
        /// 类型 buff 摘要缓存（按 Def 实例）。Def 在进程内稳定 ⇒ 只算一次，
        /// 避免每帧 × 每类型 `new List` + 翻译（审计 P2·GOV-010）。
        /// </summary>
        private static readonly Dictionary<WorkshopTypeDef, string> typeBuffCache =
            new Dictionary<WorkshopTypeDef, string>();

        /// <summary>缓存建立时的语言标识（2026-10-02 审计 P1-K）：切换语言 ⇒ 缓存失效。</summary>
        private static string typeBuffLang = string.Empty;

        private static string TypeBuffSummary(WorkshopTypeDef type)
        {
            if (type == null)
            {
                return string.Empty;
            }
            // 2026-10-02 审计修复（P1-K）：本缓存此前**不随语言切换失效** ⇒ 运行期切语言后
            // 设置页每个类型的副行仍显示旧语言。语言标识与工程内四处先例同源
            // （同文件 MeasuredButtonW / Shifts.EnsureShiftHeader / UIComponents.Loc / RuifBridge）：
            // LanguageDatabase.activeLanguage.folderName。
            string lang = LanguageDatabase.activeLanguage != null
                ? LanguageDatabase.activeLanguage.folderName : string.Empty;
            if (!string.Equals(lang, typeBuffLang, System.StringComparison.Ordinal))
            {
                typeBuffCache.Clear();
                typeBuffLang = lang;
            }
            string cached;
            if (typeBuffCache.TryGetValue(type, out cached))
            {
                return cached;
            }
            var parts = new List<string>();
            if (type.workSpeedBonusPct != 0f)
            {
                parts.Add(T1("TypeBuffWork", (int)type.workSpeedBonusPct));
            }
            if (type.qualityBonus != 0)
            {
                parts.Add(T1("TypeBuffQuality", type.qualityBonus));
            }
            if (type.researchSpeedBonusPct != 0f)
            {
                parts.Add(T1("TypeBuffResearch", (int)type.researchSpeedBonusPct));
            }
            string text = string.Join(" · ", parts);
            typeBuffCache[type] = text;
            return text;
        }

        /// <summary>设置车间类型（切换分类 / 设备匹配 / 排班口径与该类型的固定 buff）。</summary>
        /// <remarks>
        /// CHG-20261004-AUTOTYPE：<paramref name="def"/> 为 null = 玩家点了「清除类型」
        /// ⇒ 显式记下"不要再自动回设"。这条语义原先靠 <c>autoTypeTried</c>「试过就不试」的副作用
        /// 捎带实现，一旦把自动判定改成"设备数变化可重试"就会失效（放台机器又被自动设回去），
        /// 故改为显式字段（GOV-005：同一语义只保留一处表达）。
        /// </remarks>
        private void SetWorkshopType(WorkshopTypeDef def)
        {
            if (zone == null)
            {
                return;
            }
            if (def == null)
            {
                // P1-2 修复：抑制标志写进**车间数据**（随存档持久化），而不是 View 实例
                // —— 后者关窗即失效，玩家下次开面板类型又被自动设回去。
                zone.AutoTypeSuppressed = true;
            }
            zone.SetWorkshopType(def);
            MapComponent_WorkshopManager manager = zone.Map != null
                ? MapComponent_WorkshopManager.Get(zone.Map)
                : null;
            if (manager != null)
            {
                manager.RecalcBonusForZone(zone);
                // 类型决定岗位评分的主技能（WorkshopDutyScoring），必须重算调度计划。
                manager.MarkDutyPlanDirty();
            }
            // 概览「工序设备匹配」卡读的是 stats 快照（期望设备来自类型）⇒ 不刷新会显示旧类型结果
            // （2026-09-11 体检 P1-2）。
            Refresh();
        }

        // ---- 底部操作条 ------------------------------------------------------
        private void DrawFooter(Rect rect)
        {
            UIComponents.Rule(new Rect(rect.x, rect.y, rect.width, 1f), UITheme.BorderSoft);

            int count = stats != null ? stats.DeviceCount : 0;
            string hint = KeyPrefix + "FooterHint";
            Rect hintRect = new Rect(rect.x, rect.y + 7f,
                rect.width - ButtonW * 2f - Gap * 2f, 24f);
            UIComponents.Label(hintRect, hint.Translate(count).ToString(),
                UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);

            Rect refreshRect = new Rect(rect.xMax - ButtonW * 2f - Gap, rect.y + 7f, ButtonW, 24f);
            if (UIComponents.ThemedButton(refreshRect, T("Refresh")))
            {
                Refresh();
            }

            if (closeAction == null)
            {
                return;
            }
            Rect closeRect = new Rect(rect.xMax - ButtonW, rect.y + 7f, ButtonW, 24f);
            if (UIComponents.ThemedButton(closeRect, T("Close")))
            {
                closeAction();
            }
        }

        private static string T(string suffix)
        {
            return (KeyPrefix + suffix).Translate().ToString();
        }

        /// <summary>带 1 个整型占位符的取词（走 <c>Translate(NamedArgument)</c>，避开 1.6 已过时的 params object[] 重载）。</summary>
        private static string T1(string suffix, int arg)
        {
            return (KeyPrefix + suffix).Translate(arg).ToString();
        }

        /// <summary>带 1 个字符串占位符的取词（同 <see cref="T1(string, int)"/> 的重载）。</summary>
        private static string T1(string suffix, string arg)
        {
            return (KeyPrefix + suffix).Translate(arg).ToString();
        }
    }
}
