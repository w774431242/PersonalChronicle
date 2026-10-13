using System.Collections.Generic;
using System.Globalization;           // CultureInfo：图表读数按不变文化格式化（不随语言切换变小数点）
using PersonalChronicle.Archive.UI;   // UIComponents / UITheme（与其它 partial 同一套）
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间管理 · 概览页（CHG-20261005-OVERVIEW-IND，2026-10-05 玩家批准）。
    ///
    /// <para><b>定位修正（玩家原话）</b>：「这是单个车间区的概览，所以不需要太宏观，
    /// 以工业化、数字化、智能化视角去优化」⇒ 本页做成该车间的 <b>HMI 面板</b>，
    /// 回答四个工厂级问题：有没有在跑 / 跑得多满 / 哪台不正常 / 还差什么设备。</para>
    ///
    /// <para><b>用 RUIF 工业组件族的真实契约</b>（Cecil + 源码取证，2026-10-05）：</para>
    /// <list type="bullet">
    /// <item><c>RUIF.Industrial.Gauge(r, v, lo, hi, label, warn01, dead01)</c> —— **弧形表盘**
    /// （弧 + 刻度 + 指针），轴心在 <c>r.yMax-6</c>、半径 = <c>min(width*0.42, 高度余量)</c>
    /// ⇒ <b>高度不足时它静默不画</b>（源码 <c>radius &gt; 2f</c> 判定），故选型给足高度；</item>
    /// <item><c>RUIF.Industrial.LedMatrix(r, statuses, cols)</c> —— <c>statuses[i]</c> 语义
    /// <b>0 熄 / 1 正常 / 2 警告 / 3 故障</b>（≥2 自动加描边）；格子由 <c>cols</c> 自动排；</item>
    /// <item>两者都是"贴图/纯色块"级绘制，无隐藏状态、无返回值（纯展示件，不需命中检测）。</item>
    /// </list>
    ///
    /// <para><b>性能</b>：全部读数在 <see cref="BuildOverviewReadings"/>（Refresh 期）算好，
    /// 绘制路径只做取值与画；状态判定走 <c>WorkshopDeviceStateResolver.KeyOf</c>（唯一实现）。</para>
    /// </summary>
    internal sealed partial class WorkshopManageView
    {
        // ── 结构常量（高度由组件契约反推：Gauge 需容纳标题带 + 弧半径）──────────
        private const float GaugeH = 84f;        // 表盘区高（< Gauge 的最小可画高度时静默不画）
        private const float GaugeSubH = 18f;     // 表盘下方精确值行（"3 / 12"）
        private const float LedCellMax = 22f;    // 灯格上限边长（IndustrialFmt.LedCell 内部再算）
        private const float LedMinW = 180f;      // 灯阵与图例分栏的最小左栏宽

        // ── CHG-20261005-TRENDBOARD 大组件区（2026-10-05 玩家批准候选 1 / 3 / 4）──
        //
        // 高度由"组件契约 + 标签行"反推（skill 铁律 1：尺寸取自度量，禁手写行高魔数）：
        //   Peak = 标题 18 + 网格 96 + 列标签 18 + 图例/脚注 18
        //   Eff  = 标题 18 + 图表 96 + 图例 18
        // 每次绘制前先判空态：全空 ⇒ 只画标题 + "暂无采样"（LAND-009，不画假网格）。
        private const float PeakGridH = 96f;
        // 逐行累加（单行文本行高 18 = UITheme.FontLabel 的引擎行高）：
        //   Peak = 标题 22 + 网格 96 + 小时标签 18 + 脚注 18 + 底边距 8
        //   Eff  = 标题 22 + 图表 140 + 图例 18 + 底边距 8
        // ⚠ 8f 是区块底边距（与 UITheme.SpaceXs 同值）：常量表达式要求编译期字面量，
        //    而 UITheme 的间距是运行时只读值 ⇒ 此处只能写字面量，改 UITheme 时要同步这里。
        //
        // ⚠ CHG-20261005-OVERVIEW-FITCHART（2026-10-05 玩家两轮反馈「纵坐标间隔太小 / 文本遮盖挤兑」）：
        //   ① 第一轮在 PC 侧按 RUIF 的标签几何留了「刻度带 12 + 右缩 34」来绕开 ——
        //      但那只是**绕开**，根因在框架：`Charts.GridH` 把标签画在绘图区**内侧且贴右缘**，
        //      最高刻度还会顶出上边界；`StackedBarChartV` 更是连 `PlotArea` 都不走。
        //   ② 第二轮**由 PM 授权直接修改 RUIF 本体**：`GridH` 改为把标签画在绘图区
        //      **右侧外部**的轴列（`Charts.AxisLabelW = 34`）、以刻度线垂直居中；
        //      `PlotArea` 预留该轴列 + 上下各半行；`StackedBarChartV` 也改走 `PlotArea`。
        //   ③ ⇒ PC 侧的 `TickBandH` / `TickLabelW` **全部撤除**（框架已负责），
        //      只把图表高度抬到 **140**：框架内缩 12px 后净绘图区 128px，
        //      6 档刻度（`NiceTicks(target=4)` 的上界）间距 ≈ 25.6px，不再挤兑。
        //   ⚠ 要在图外自画与柱/线**逐列对齐**的内容（本页的「天标签」就是），
        //      必须用 `RUIF.Charts.PlotRectOf(rect)` 取绘图区，**不能**拿 rect 直接算 ——
        //      否则会整体错开一个轴列宽（2026-10-05 实证）。
        private const float ChartPlotH = 140f;
        private const float PeakSectionH = 22f + PeakGridH + 18f + 18f + 8f;
        private const float EffSectionH = 22f + ChartPlotH + 18f + 8f;
        /// <summary>左侧"天"标签列宽（"6 天前"的最宽情形 + 间距）。</summary>
        private const float PeakDayLabelW = 48f;
        /// <summary>堆叠柱最多几个系列（含"其它"）：<c>Tokens.Series</c> 是 6 色循环，超过即重复上色。</summary>
        private const int BottleSeriesMax = 6;

        /// <summary>大组件区总高（<see cref="OverviewContentHeight"/> 与 <see cref="DrawOverviewBody"/> 共用）。</summary>
        private const float OvBigBlockH = PeakSectionH + Gap + EffSectionH;

        // ── Refresh 期读数（绘制期只读；全部在这里算，绘制路径零分配）──────────
        private int[] ovLedStatuses = new int[0];   // 逐台设备的灯码（0/1/2/3）
        private string[] ovLedNames = new string[0];// 逐台设备显示名（悬停用）
        private int ovDeviceTotal;
        private int ovDeviceRunning;
        private int ovDeviceManned;                 // 有操作员在设备上的台数
        private int ovOccupiedCells;
        private int ovTotalCells;
        private int ovFreeCells;
        // B 批（2026-10-05）：台账读数（今日产出达成 + 逐台运行时长，后者供瓶颈带）
        private long[] ovDeviceRunTicks = new long[0];
        private int ovProducedToday;
        private int ovProduceTarget;

        // ── 大组件区读数（CHG-20261005-TRENDBOARD）──────────────────────────
        /// <summary>候选 1：7 行（天，上旧下新）× 24 列（小时）的机时网格（单位：机时）。</summary>
        private readonly float[,] ovPeakGrid =
            new float[Zone_Workshop.TrendDayCount, Zone_Workshop.TrendHourCount];
        /// <summary>峰值时段（列合计最大的小时；-1 = 无采样）。</summary>
        private int ovPeakBestHour = -1;
        /// <summary>峰值时段合计机时（小时）。</summary>
        private float ovPeakBestHours;
        /// <summary>是否有任何运行采样（空态判定：不画全暗网格假装有数据）。</summary>
        private bool ovHasRunTrend;
        /// <summary>候选 3：逐日机时 / 逐日产出（归一化前的绝对值，机时单位 = 小时）。</summary>
        private float[] ovDayRunHours = new float[Zone_Workshop.TrendDayCount];
        private float[] ovDayProduced = new float[Zone_Workshop.TrendDayCount];
        /// <summary>候选 3 的入参：两条归一化序列（各自峰值 = 100）。绘制期零分配。</summary>
        private float[][] ovEffSeries;
        private readonly Color[] ovEffColors = new Color[2];
        /// <summary>候选 3 是否有可画数据（产出或机时任一非零）。</summary>
        private bool ovHasEffData;
        /// <summary>候选 4 的入参：<c>[系列][天]</c> 机时（Top N 设备 + "其它"），长度 = <see cref="ovBottleSeriesCount"/>。</summary>
        private float[][] ovBottleSeries = new float[0][];
        private string[] ovBottleNames = new string[BottleSeriesMax];
        private int ovBottleSeriesCount;

        // C 批（2026-10-05）：近 N 日产出趋势 + 「工作台有人率」口径修正
        /// <summary>
        /// 趋势天数 —— 与 <c>DeviceStatEntry.ProducedWindowDays</c>（7）对齐。
        /// <para>⚠ 2026-10-05 CHG-20261005-TRENDBOARD：由 5 改为 7。台账产出窗本身就是 7 天
        /// （旧注释写 5 是漂移），改 7 后本页趋势与设备页「本周」读数同窗 ——
        /// 同一批数据在两处显示不同天数，玩家无法自证（UI-009）。</para>
        /// </summary>
        private const int OvTrendDays = Zone_Workshop.TrendDayCount;
        private readonly int[] ovDailyProduced = new int[OvTrendDays];
        /// <summary>需要人操作的原版工作台总数（<c>IBillGiver</c>；自动化自研设备不计入）。</summary>
        private int ovMannedTotal;
        // ⚠ CHG-20261005-WSDOCFIX：原 `ovDailyTotal`（趋势盘合计值）已随趋势盘一起清理，
        //   详见 DrawOverviewBody 顶部说明。

        /// <summary>
        /// Refresh 期构建工业化读数（由 <c>Refresh</c> 调用）。
        /// <para>状态判定与设备详情页同源（<c>WorkshopDeviceStateResolver.KeyOf</c>）——
        /// 不在本页另写一套阈值，避免"同机不同态"。</para>
        /// </summary>
        internal void BuildOverviewReadings()
        {
            ovDeviceTotal = 0;
            ovDeviceRunning = 0;
            ovDeviceManned = 0;
            ovOccupiedCells = stats != null ? stats.OccupiedCells : 0;
            ovTotalCells = stats != null ? stats.CellCount : 0;
            ovFreeCells = stats != null ? stats.FreeCells : 0;

            List<Thing> list = devices;
            if (list == null || list.Count == 0)
            {
                ovLedStatuses = new int[0];
                ovLedNames = new string[0];
                return;
            }

            // "谁在哪台设备上" —— 与车间调色板/设备详情同一读法（唯一实现）。
            Dictionary<Thing, Pawn> active = zone != null && zone.Map != null
                ? WorkshopDeviceReader.CollectActiveWorkers(zone.Map)
                : null;

            if (ovLedStatuses.Length != list.Count)
            {
                ovLedStatuses = new int[list.Count];
                ovLedNames = new string[list.Count];
            }

            for (int i = 0; i < list.Count; i++)
            {
                Thing thing = list[i];
                if (thing == null || thing.Destroyed)
                {
                    ovLedStatuses[i] = 0;          // 已销毁 ⇒ 熄
                    ovLedNames[i] = string.Empty;
                    continue;
                }
                string stateKey = WorkshopDeviceStateResolver.KeyOf(thing);
                ovLedStatuses[i] = LedCodeOf(stateKey);
                ovLedNames[i] = Manager != null
                    ? Manager.EffectiveNameOf(thing)
                    : WorkshopStats.DeviceLabelOf(thing);   // CHG-20261005-DEVRENAME：概览 LED 同口径（否则同屏两名）
                ovDeviceTotal++;
                if (stateKey == EquipmentStateResolver.KeyRun)
                {
                    ovDeviceRunning++;
                }
                // 「有人率」只统计**需要人操作的原版工作台**（IBillGiver）：自研自动化设备
                // （自动农场/传送带等）本来就无人值守，把它们算进分母会让读数恒为 0%
                // —— 实机截图实证：设备运转率 100%、今日产出 31 件，而"有人率"显示 0%，
                // 两者并列会误导（2026-10-05 玩家截图核对时发现）。
                Pawn worker;
                bool manned = active != null && active.TryGetValue(thing, out worker)
                    && worker != null && !worker.Destroyed;
                if (thing is IBillGiver)
                {
                    ovMannedTotal++;
                    if (manned)
                    {
                        ovDeviceManned++;
                    }
                }
            }

            // ── B 批：产出达成 + 逐台运行时长（瓶颈带）──────────────────────────
            //
            // 数据全部来自既有设备台账（`Manager.DeviceLedger`，与设备页同一读法）：
            //   · 今日产出 = Σ ProducedTodayOf（"东西是否在这台设备上被造出来"，与设备页同口径）；
            //   · 运行时长 = RunTicksOf（每 60 tick 采样累加）⇒ 用它做**相对占比**即为
            //     BottleneckRibbon 需要的 stageTimes（跑得越久 = 越忙 = 瓶颈候选）。
            //
            // 目标口径：每人每日目标 × 车间成员数（"全员都达标时的产量"）。用成员数而非在岗数，
            // 是因为概览页不掌握"本车间在岗人数"（ActiveWorkerCount 是全图口径）⇒ 宁可把目标
            // 说成"全员目标"（可在文案里写明），也不用混合口径凑一个数。
            WorkshopDeviceLedger ledger = Manager != null ? Manager.DeviceLedger : null;
            int members = zone != null && zone.Members != null ? zone.Members.Count : 0;
            int perMember = zone != null ? zone.PerMemberDailyTarget : 10;
            ovProduceTarget = Mathf.Max(1, members * Mathf.Max(1, perMember));
            ovProducedToday = 0;
            if (ovDeviceRunTicks.Length != list.Count)
            {
                ovDeviceRunTicks = new long[list.Count];
            }
            for (int i = 0; i < list.Count; i++)
            {
                Thing thing = list[i];
                long ticks = ledger != null && thing != null ? ledger.RunTicksOf(thing) : 0L;
                ovDeviceRunTicks[i] = ticks;
                if (ledger != null && thing != null)
                {
                    ovProducedToday += ledger.ProducedTodayOf(thing);
                }
            }

            // 瓶颈带缓冲也在这里建好（绘制路径只读不分配 —— skill 反模式：绘制期 new）。
            // 段数上限 16 = BottleneckRibbon 的契约（它复用内部缓冲，超出的段不会被画）。
            BuildBottleneckBuffers(list);

            // ── 逐日产出（近 7 日）────────────────────────────────────────────
            // 数据来自台账（ProducedOnDayOf 直接查逐日槽位，跨日自然归零）⇒ 零新增数据。
            // ⚠ CHG-20261005-WSDOCFIX：这些读数原服务"趋势盘"，玩家 2026-10-05 移除该盘后
            //   改由**大组件区（候选 3：逐日机时 / 逐日产出）**消费 ⇒ 保留计算，
            //   只删掉"仅喂图表"的缓冲填充（ovDailyTotal / ovDailyValues / ovDailyLabels）。
            int today = GenDate.DaysPassed;
            for (int d = 0; d < OvTrendDays; d++)
            {
                ovDailyProduced[d] = 0;
            }
            for (int i = 0; i < list.Count; i++)
            {
                Thing thing = list[i];
                if (ledger == null || thing == null)
                {
                    continue;
                }
                for (int d = 0; d < OvTrendDays; d++)
                {
                    ovDailyProduced[d] += ledger.ProducedOnDayOf(thing, today - (OvTrendDays - 1 - d));
                }
            }
            // 天标签缓冲（⚠ **不是趋势盘专属** —— CHG-20261005-WSDOCFIX 澄清）：**瓶颈趋势图**
            //   （DrawBottleTrend）直接读它补类目标签（RUIF 柱状图契约：组件不自画类目，
            //   否则 7 天里只有 1 天有采样时玩家会以为图坏了）⇒ 本次清理**保留**本缓冲，
            //   仅 `ovDailyValues` 与 `ovDailyTotal` 随趋势盘删除。
            if (ovDailyLabels.Length != OvTrendDays)
            {
                ovDailyLabels = new string[OvTrendDays];
            }
            for (int d = 0; d < OvTrendDays; d++)
            {
                int ago = OvTrendDays - 1 - d;
                ovDailyLabels[d] = ago == 0 ? T("TrendToday") : T1("TrendDaysAgo", ago);
            }

            // 大组件区（CHG-20261005-TRENDBOARD）：与上面同一次 Refresh 内算完。
            BuildBigBlockReadings(list, ledger, today);
        }

        /// <summary>
        /// 大组件区读数（CHG-20261005-TRENDBOARD，2026-10-05 玩家批准候选 1 / 3 / 4）。
        ///
        /// <para>全部在 Refresh 期算好（绘制路径只读、零分配）：</para>
        /// <list type="number">
        /// <item><b>候选 1</b>：车间级 7×24 波峰（<see cref="Zone_Workshop.HourlyRunAt"/>；
        /// 采样写在设备运行路径，本页只读 ⇒ 不在绘制期访问 Zone 的写侧）；</item>
        /// <item><b>候选 3</b>：逐日机时（台账逐台聚合）与逐日产出（复用 <c>ovDailyProduced</c>），
        /// 各自归一到 0-100 ⇒ 两条曲线共用一条 y 轴（<c>LineChart</c> 契约：共享上界）；</item>
        /// <item><b>候选 4</b>：逐台逐日机时 → Top（<see cref="BottleSeriesMax"/>-1）台 + "其它"
        /// 堆叠系列（<c>Tokens.Series</c> 仅 6 色，超出的并入"其它"而不是循环复用同色）。</item>
        /// </list>
        /// </summary>
        private void BuildBigBlockReadings(List<Thing> list, WorkshopDeviceLedger ledger, int today)
        {
            int days = Zone_Workshop.TrendDayCount;

            // ── 候选 1：7×24 波峰网格 ─────────────────────────────────────
            ovHasRunTrend = zone != null && zone.HasHourlyTrend();
            ovPeakBestHour = -1;
            ovPeakBestHours = 0f;
            for (int d = 0; d < days; d++)
            {
                int day = today - (days - 1 - d);
                for (int h = 0; h < Zone_Workshop.TrendHourCount; h++)
                {
                    ovPeakGrid[d, h] = zone != null
                        ? zone.HourlyRunAt(day, h) / (float)GenDate.TicksPerHour
                        : 0f;
                }
            }
            // 峰值时段按**列合计**取：回答"通常几点最忙"，而不是"某一天几点最忙"。
            for (int h = 0; h < Zone_Workshop.TrendHourCount; h++)
            {
                float colSum = 0f;
                for (int d = 0; d < days; d++)
                {
                    colSum += ovPeakGrid[d, h];
                }
                if (colSum > ovPeakBestHours)
                {
                    ovPeakBestHours = colSum;
                    ovPeakBestHour = h;
                }
            }
            if (ovPeakBestHours <= 0f)
            {
                ovPeakBestHour = -1;   // 无采样 ⇒ 不显示"最忙时段"，不虚报
            }

            // ── 候选 3/4：逐日机时（逐台聚合）+ Top-N 设备 ────────────────
            int n = Mathf.Min(list.Count, BottleSeriesMax - 1);   // 留 1 个系列位给"其它"
            int[] topIndex = new int[BottleSeriesMax - 1];
            long[] topRun = new long[BottleSeriesMax - 1];
            for (int k = 0; k < topIndex.Length; k++)
            {
                topIndex[k] = -1;
            }
            long otherRunTotal = 0L;
            for (int d = 0; d < days; d++)
            {
                ovDayRunHours[d] = 0f;
                ovDayProduced[d] = d < ovDailyProduced.Length ? ovDailyProduced[d] : 0f;
            }
            for (int i = 0; i < list.Count; i++)
            {
                Thing thing = list[i];
                if (ledger == null || thing == null)
                {
                    continue;
                }
                long total = 0L;
                for (int d = 0; d < days; d++)
                {
                    int ticks = ledger.RunTicksOnDay(thing, today - (days - 1 - d));
                    total += ticks;
                    ovDayRunHours[d] += ticks / (float)GenDate.TicksPerHour;
                }
                if (total <= 0L || n <= 0)
                {
                    continue;
                }
                // Top-N 插入（n ≤ 5，手写插入优于排序 + 额外容器分配）
                int pos = -1;
                for (int k = 0; k < n; k++)
                {
                    if (total > topRun[k])
                    {
                        pos = k;
                        break;
                    }
                }
                if (pos < 0)
                {
                    otherRunTotal += total;
                    continue;
                }
                otherRunTotal += topRun[n - 1];   // 被挤出者并入"其它"（未填满时为 0，无害）
                for (int k = n - 1; k > pos; k--)
                {
                    topRun[k] = topRun[k - 1];
                    topIndex[k] = topIndex[k - 1];
                }
                topRun[pos] = total;
                topIndex[pos] = i;
            }

            int seriesCount = n + (otherRunTotal > 0L ? 1 : 0);
            if (seriesCount <= 0 || !ovHasRunTrend)
            {
                ovBottleSeriesCount = 0;   // 空态：图不画（避免空网格 + 空柱两重噪音）
                ovHasEffData = false;
                return;
            }
            if (ovBottleSeries.Length != seriesCount)
            {
                ovBottleSeries = new float[seriesCount][];
                for (int j = 0; j < seriesCount; j++)
                {
                    ovBottleSeries[j] = new float[days];
                }
            }
            for (int j = 0; j < n; j++)
            {
                Thing thing = topIndex[j] >= 0 && topIndex[j] < list.Count ? list[topIndex[j]] : null;
                // CHG-20261005-COMPFIX：图例用**带别名**的显示名 —— 原先 4 台「炉灶（电力）」
                // 在图例里完全一样，玩家无法把柱段与设备对上（别名须贯穿所有设备名出现处）。
                ovBottleNames[j] = thing != null
                    ? (Manager != null ? Manager.DisplayNameOf(thing) : WorkshopStats.DeviceLabelOf(thing))
                    : string.Empty;
                for (int d = 0; d < days; d++)
                {
                    ovBottleSeries[j][d] = ledger != null && thing != null
                        ? ledger.RunTicksOnDay(thing, today - (days - 1 - d)) / (float)GenDate.TicksPerHour
                        : 0f;
                }
            }
            if (seriesCount > n)
            {
                ovBottleNames[n] = T("BottleTrendOther");
                for (int d = 0; d < days; d++)
                {
                    float shown = 0f;
                    for (int j = 0; j < n; j++)
                    {
                        shown += ovBottleSeries[j][d];
                    }
                    ovBottleSeries[n][d] = Mathf.Max(0f, ovDayRunHours[d] - shown);
                }
            }
            ovBottleSeriesCount = seriesCount;

            // ── 候选 3：归一化双序列（各自峰值 = 100）────────────────────
            float prodMax = 0f;
            float runMax = 0f;
            for (int d = 0; d < days; d++)
            {
                if (ovDayProduced[d] > prodMax)
                {
                    prodMax = ovDayProduced[d];
                }
                if (ovDayRunHours[d] > runMax)
                {
                    runMax = ovDayRunHours[d];
                }
            }
            ovHasEffData = prodMax > 0f || runMax > 0f;
            if (ovEffSeries == null || ovEffSeries.Length < 2
                || ovEffSeries[0] == null || ovEffSeries[0].Length != days)
            {
                ovEffSeries = new float[2][];
                ovEffSeries[0] = new float[days];
                ovEffSeries[1] = new float[days];
            }
            for (int d = 0; d < days; d++)
            {
                ovEffSeries[0][d] = prodMax > 0f ? ovDayProduced[d] / prodMax * 100f : 0f;
                ovEffSeries[1][d] = runMax > 0f ? ovDayRunHours[d] / runMax * 100f : 0f;
            }
            // 双线配色（CHG-20261005-EFFCOLOR，2026-10-05 玩家反馈"两条线对比不足"）：
            // 原用 Series(0)=Accent (0.31,0.60,0.94) 与 Series(1)=Info (0.48,0.72,0.91)
            // —— 两者属**同一蓝色系**（只差明度/饱和），深底上叠在一起几乎分不清。
            //
            // 现改为色相**互补**的一对（仍全部走 Tokens 令牌，不裸写 Color —— skill 红线 1）：
            //   产出 = Accent（天蓝，本页主色 ⇒ "产出是主角"的既有视觉关系不变）
            //   机时 = Warn  （橙黄；与蓝在色轮上接近对立 ⇒ 6 色板里对比最强的一对，
            //                 且"蓝 vs 橙"对红绿色盲同样可辨，优于"蓝 vs 绿"）
            // ⚠ 图例色块与曲线读的是**同一对** ovEffColors（见 DrawEfficiencySection）
            //   ⇒ 换色只需改这一处，图例自动跟随，不会出现"图例一个色、线另一个色"。
            ovEffColors[0] = RUIF.Tokens.Accent;
            ovEffColors[1] = RUIF.Tokens.Warn;
        }

        /// <summary>
        /// 构建瓶颈带入参（运行时长 + 瓶颈段下标）；段数按组件契约截断到 16。
        ///
        /// <para><b>CHG-20261005-COMPFIX（2026-10-05 玩家"检查组件问题"）</b>：本方法原先还填
        /// <c>bnLabels</c>=设备名并交给组件 —— 但取证发现 <c>Industrial.BottleneckRibbon</c>
        /// <b>会自己在段宽 &gt;24px 的段内画 <c>stageLabels[i]</c></b>（<c>MiddleCenter</c> 且
        /// <c>Draw.Label</c> <b>不裁剪</b>）⇒ 设备名溢出压住相邻段（玩家截图里条带右侧那段
        /// 孤立白字「待机（电力）」就是它）。故**不再传标签**，改由本页在下方自画注脚
        /// （这样名字还能带上设备别名，见 <see cref="DrawBottleneckSection"/>）。</para>
        /// </summary>
        private void BuildBottleneckBuffers(List<Thing> list)
        {
            int count = list != null ? list.Count : 0;
            int n = Mathf.Min(count, 16);
            if (bnLabels == null || bnLabels.Length < n)
            {
                bnLabels = new string[16];
                bnTimes = new float[16];
            }
            ovRunTicksTotal = 0L;
            ovBottleneckCount = n;
            for (int i = 0; i < n; i++)
            {
                Thing thing = list[i];
                bnLabels[i] = thing != null
                    ? (Manager != null ? Manager.DisplayNameOf(thing) : WorkshopStats.DeviceLabelOf(thing))
                    : string.Empty;
                long t = i < ovDeviceRunTicks.Length ? ovDeviceRunTicks[i] : 0L;
                bnTimes[i] = t;
                ovRunTicksTotal += t;
            }
        }

        private long ovRunTicksTotal;
        private int ovBottleneckCount;

        /// <summary>
        /// 设备状态键 → LED 灯码（<c>Industrial.LedColor</c> 的口径：0 熄 / 1 正常 / 2 警告 / 3 故障）。
        /// <para>口径与设备详情页的 <c>StateColor</c> 逐项对齐：run=绿、fault=红、
        /// starved/缺电/缺燃料=警告、idle/off=熄。</para>
        /// </summary>
        private static int LedCodeOf(string stateKey)
        {
            if (stateKey == EquipmentStateResolver.KeyRun)
            {
                return 1;
            }
            if (stateKey == EquipmentStateResolver.KeyFault)
            {
                return 3;
            }
            if (stateKey == "starved"
                || stateKey == EquipmentStateResolver.KeyAgNoPower
                || stateKey == EquipmentStateResolver.KeyAgNoFuel)
            {
                return 2;
            }
            return 0;   // idle / off / AgOff / 未知 ⇒ 熄（不虚报告警）
        }

        // ═══════════════════════════ 绘制 ═══════════════════════════

        /// <summary>
        /// 概览页主体（工业化版）。
        ///
        /// <para><b>为什么加滚动</b>（2026-10-05 C 批）：四张仪表 + 趋势 + 灯阵 + 负载 + 匹配
        /// 的累计高度已超过窗口可用高（实测约 380px）⇒ 不加滚动就会有内容被裁。
        /// <c>Layout.EndScroll()</c> 放 <c>finally</c>（skill 铁律 3：出栈必须放 finally）。</para>
        /// </summary>
        private void DrawOverviewIndustrial(Rect rect)
        {
            if (stats == null)
            {
                UIComponents.EmptyState(rect, UIComponents.Loc(KeyPrefix + "OverviewEmptyTitle"),
                    UIComponents.Loc(KeyPrefix + "OverviewEmptySub"));
                return;
            }

            float contentH = OverviewContentHeight();
            try
            {
                Rect view = RUIF.Layout.BeginScroll(rect, contentH, ref ovScroll, true);
                DrawOverviewBody(view);
            }
            finally
            {
                RUIF.Layout.EndScroll();
            }
        }

        /// <summary>滚动容器内的滚动位置（视图会话状态，不入存档）。</summary>
        private Vector2 ovScroll = Vector2.zero;

        /// <summary>
        /// 概览内容总高（用于滚动范围）。逐段累加，与 <see cref="DrawOverviewBody"/> 的绘制顺序
        /// **必须同步**（改一处忘了另一处会造成"滚不到底/底部空白"）。
        /// </summary>
        private static float OverviewContentHeight()
        {
            return GaugeH + GaugeSubH + Gap      // 仪表台（4 张）
                 + LedSectionHeight() + Gap      // 灯阵
                 + 62f + Gap                     // 瓶颈带
                 + OvBigBlockH;                  // 大组件区（候选 1 + 3 + 4，见 DrawBigBlocks）
        }

        private void DrawOverviewBody(Rect rect)
        {
            float y = rect.y;

            // ⚠ CHG-20261005-WSDOCFIX：趋势盘（逐日产出）已按承诺**清理完毕** ——
            //   玩家 2026-10-05 决定移除（"产出趋势可以删除"），上一轮保留方法体作对照，
            //   本批删除 `DrawTrendSection` 与其专属缓冲（`ovDailyTotal` / `ovDailyValues` /
            //   `ovDailyLabels`）。**`ovDailyProduced` 保留** —— 已改由大组件区（候选 3）消费。
            //   语言键 `Trend*` 保留（恢复趋势盘可直接复用，且不动语言文件）。

            // ① 仪表台（四表横排：跑得多满 / 有几台在跑 / 有没有人盯着 / 今天产了多少）
            float cardW = (rect.width - Gap * 3f) / 4f;
            DrawGaugeCard(new Rect(rect.x, y, cardW, GaugeH + GaugeSubH),
                T("GaugeOccupancy"), ovTotalCells > 0 ? ovOccupiedCells : 0f,
                0f, Mathf.Max(1f, ovTotalCells),
                CounterText(ovOccupiedCells, ovTotalCells), UITheme.Accent);
            DrawGaugeCard(new Rect(rect.x + (cardW + Gap), y, cardW, GaugeH + GaugeSubH),
                T("GaugeDeviceRun"), ovDeviceRunning,
                0f, Mathf.Max(1f, ovDeviceTotal),
                CounterText(ovDeviceRunning, ovDeviceTotal), UITheme.Alive);
            DrawGaugeCard(new Rect(rect.x + (cardW + Gap) * 2f, y, cardW, GaugeH + GaugeSubH),
                T("GaugeDeviceManned"), ovDeviceManned,
                0f, Mathf.Max(1f, ovMannedTotal),
                CounterText(ovDeviceManned, ovMannedTotal), UITheme.Info);
            // B 批：产出达成（今日产出 / 全员目标；表盘给比例、值行给精确件数）
            DrawGaugeCard(new Rect(rect.x + (cardW + Gap) * 3f, y, cardW, GaugeH + GaugeSubH),
                T("GaugeProduce"), ovProducedToday,
                0f, Mathf.Max(1f, ovProduceTarget),
                CounterText(ovProducedToday, ovProduceTarget), UITheme.Warn);
            y += GaugeH + GaugeSubH + Gap;

            // ② 设备状态灯阵（+ 右侧图例）
            float ledH = LedSectionHeight();
            DrawLedSection(new Rect(rect.x, y, rect.width, ledH));
            y += ledH + Gap;

            // ③ 瓶颈带（B 批：按累计运行时长占比 —— 跑得最久的设备就是瓶颈）
            float bnH = 62f;
            DrawBottleneckSection(new Rect(rect.x, y, rect.width, bnH));
            y += bnH + Gap;

            // ⚠ 设备匹配段：玩家 2026-10-05 决定移除（"工序设备匹配可以删除"）。
            //   方法体 DrawFitSection + BuildFitBodyText 保留一轮作对照，下版随标注清理。

            // ④ 大组件区（CHG-20261005-TRENDBOARD）：设备使用波峰 + 瓶颈演变 + 稼动vs产出。
            //    ⚠ 顺序与高度必须与 OverviewContentHeight 同步（不同步 ⇒ 滚不到底/底部空白）。
            DrawBigBlocks(new Rect(rect.x, y, rect.width, OvBigBlockH));

            // ⑤ 教育性提示：只在"没有任何设备"时出现（避免常驻噪音）
            if (ovDeviceTotal == 0)
            {
                UIComponents.Label(new Rect(rect.x, rect.yMax - 22f, rect.width, 22f),
                    T("OverviewHint"), UITheme.FontLabel, UITheme.Muted, TextAnchor.LowerLeft);
            }
        }

        private static string CounterText(int v, int max)
        {
            return v + " / " + max;
        }

        /// <summary>
        /// 单张仪表卡：标题（在 Gauge 的标题带内）+ 弧形表盘 + 精确值行。
        /// <para>⚠ <c>Gauge</c> 自画 label（顶部带）、弧、刻度与指针；本方法只提供 rect 与阈值，
        /// 再补一行"3 / 12"的精确值（表盘给比例、数值给精确，二者互补）。</para>
        /// </summary>
        private void DrawGaugeCard(Rect rect, string label, float v, float lo, float hi,
            string counter, Color accent)
        {
            UIComponents.Card(rect, accent);
            // 表盘：给足高度（契约：高度不足 radius ≤ 2 ⇒ 静默不画）
            RUIF.Industrial.Gauge(new Rect(rect.x, rect.y, rect.width, GaugeH),
                v, lo, hi, label, 0.6f, 0.85f);
            UIComponents.Label(new Rect(rect.x, rect.yMax - GaugeSubH, rect.width, GaugeSubH),
                counter, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleCenter);
        }

        /// <summary>
        /// 灯阵段高：标题 22 + 两行灯 2×(格 22+间距 4) + 图例区 40（4 行 × 18 中的两行给右栏留位）。
        /// <para>⚠ 2026-10-05 修正：初版只留 18 呼吸 ⇒ 内容区 62px，而右侧图例是 **4 行 × 18 = 72px**
        /// ⇒ 末行会画到容器外（skill 铁律 3：IMGUI 无裁剪，溢出必被后画元素盖住/画到界外）。</para>
        /// <para>不按设备数动态行高：行数变化会让下方内容跳动；超出两行的设备由
        /// <c>LedMatrix</c> 按行数自行压缩格子边长（不会溢出）。</para>
        /// </summary>
        private static float LedSectionHeight()
        {
            return 22f + 2f * (LedCellMax + 4f) + 40f;
        }

        /// <summary>
        /// 瓶颈带（B 批）：各设备的**累计运行时长占比**。
        ///
        /// <para><b>为什么用运行时长而不是"工序耗时"</b>：本工程没有逐工序耗时统计，而台账的
        /// <c>RunTicks</c> 是每 60 tick 真实采样的累加值 ⇒ "谁跑得最久"就是"谁最忙"，
        /// 这正是瓶颈的工程定义（BottleneckRibbon 会把最大段标红 + 下方自动注脚）。</para>
        ///
        /// <para>设备数 &gt; 16 时按前 16 台截断（组件契约：段数上限 16、复用内部缓冲）。</para>
        /// </summary>
        private void DrawBottleneckSection(Rect rect)
        {
            UIComponents.Panel(rect);
            float padX = UITheme.SpaceXs;
            UIComponents.Label(new Rect(rect.x + padX, rect.y + UITheme.SpaceXxs, 280f, 18f),
                T("BottleneckTitle"), UITheme.FontLabel, UITheme.Muted);

            int n = ovBottleneckCount;
            // 无设备 / 全为 0（刚放上设备还没采样到）⇒ 明确说"暂无运行数据"，
            // 不画一条均匀的假带（LAND-009 诚实口径）。
            if (n == 0 || ovDeviceTotal == 0 || ovRunTicksTotal <= 0L)
            {
                UIComponents.Label(new Rect(rect.x + padX, rect.y + 22f, rect.width - padX * 2f, 18f),
                    T("BottleneckNoData"), UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
                return;
            }

            // ⚠ 宽度预留（2026-10-05 玩家截图实测"有溢出现象"）：BottleneckRibbon 的源码
            //   逐段 `w = max(1, r.width*share - 2)` 后 `x += w + 2` ⇒
            //   ① 每段额外吃 2px 间距、② 小段被抬到最小 1px、③ 末段还会多一次 +2
            //   ⇒ 累计宽度必然超过 r.width（段数越多越明显）。
            //   这里按"段数 × 2 + 4"预留富余，保证最右端不越出面板。
            float reserve = ovBottleneckCount * 2f + 4f;
            var strip = new Rect(rect.x + padX, rect.y + 20f,
                Mathf.Max(20f, rect.width - padX * 2f - reserve),
                rect.height - 20f - UITheme.SpaceXs);
            // CHG-20261005-COMPFIX2：标签**传回**（含设备别名）—— 框架侧已把段内标签改为
            // Draw.EllipsisLabel（放不下补省略号、永不越界）⇒ 既能看到"哪台占哪段"，
            // 又不会再溢出压邻段。注脚由组件自画（它取 stageLabels[bottleneck]）
            // ⇒ 注脚同样带别名，且与"最大段标红描边"出自同一判定，不会出现两处口径不一致。
            RUIF.Industrial.BottleneckRibbon(strip, bnLabels, bnTimes, UITheme.Info);
        }

        // ⚠ CHG-20261005-COMPFIX2：原 BottleneckNameOf（自画注脚取设备名）与 T2（双占位取词）
        //   已删除 —— 注脚回归组件自画（它取 stageLabels[bottleneck]，而 bnLabels 本身就是
        //   带别名的显示名）⇒ 本页不再需要单独取名与双参取词（减法审计）。

        // 逐段设备名（**带别名**）与运行时长。CHG-20261005-COMPFIX2：标签恢复传回组件 ——
        // 框架侧已把段内标签改为 EllipsisLabel（放不下补省略号、永不越界）⇒ 既能看到
        // "哪台占哪段"，也不会再溢出压邻段；注脚仍由组件自画（因此注脚同样带别名）。
        private string[] bnLabels;
        private float[] bnTimes;

        // ⚠ CHG-20261005-WSDOCFIX：原 `DrawTrendSection`（逐日产出趋势盘，走 RUIF Charts.BarChartV）
        //   与其入参缓冲 `ovDailyValues` / `ovDailyLabels` **已删除** ——
        //   玩家 2026-10-05 决定移除该盘（"产出趋势可以删除"），上一轮保留一轮作对照，本批清理完毕。
        //
        //   若将来恢复：图表入参可直接取 `ovDailyProduced`（仍在，当前供大组件区「候选 3」消费）；
        //   标题 / 合计 / 空态 / 类目语言键 `TrendTitle` `TrendTotal` `TrendNoData` `TrendToday`
        //   `TrendDaysAgo` **均保留**，无需动语言文件。
        //
        //   ⚠ **`ovDailyLabels` 例外：它没有随趋势盘删除** —— 瓶颈趋势图（`DrawBottleTrend`）
        //   直接消费它补"天标签"。教训记一笔：删除前必须按**符号**（而不是"看起来属于哪块 UI"）
        //   查引用；本批即因此在编译期被兜住一次（CS0103 ×2）。

        /// <summary>
        /// 天标签（D-6 … 今天）。消费者：瓶颈趋势图 <c>DrawBottleTrend</c>
        /// （RUIF 柱状图契约不自画类目 ⇒ 须调用方补；趋势盘已删，见上方说明）。
        /// </summary>
        private string[] ovDailyLabels = new string[0];

        /// <summary>设备状态灯阵 + 图例（左阵右例；灯码语义见 <c>Industrial.LedColor</c>）。</summary>
        private void DrawLedSection(Rect rect)
        {
            UIComponents.Panel(rect);
            float padX = UITheme.SpaceXs;
            UIComponents.Label(new Rect(rect.x + padX, rect.y + UITheme.SpaceXxs, 260f, 18f),
                T("LedTitle"), UITheme.FontLabel, UITheme.Muted);

            Rect body = new Rect(rect.x + padX, rect.y + 22f,
                rect.width - padX * 2f, rect.height - 22f - UITheme.SpaceXs);
            bool wide = body.width >= LedMinW + 150f;
            Rect matrix = wide
                ? new Rect(body.x, body.y, body.width - 150f, body.height)
                : body;

            if (ovDeviceTotal == 0)
            {
                UIComponents.Label(new Rect(matrix.x, matrix.y, matrix.width, 18f),
                    T("LedNoDevice"), UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
            }
            else
            {
                // ⚠ 关键几何（IndustrialFmt.LedCell 源码 :83-91）：`cw = r.width / cols` ——
                //   它把**整块 rect 均分**成 cols×rows 格。所以这里必须**按格子大小反推 rect**，
                //   绝不能给满宽：初版给满宽 ⇒ 3 台设备画成三个 233×52 的大色块（玩家截图实证）。
                int cols = Mathf.Max(1, Mathf.Min(
                    Mathf.FloorToInt(matrix.width / (LedCellMax + 4f)),
                    Mathf.Max(1, ovLedStatuses.Length)));
                int rows = Mathf.CeilToInt(ovLedStatuses.Length / (float)cols);
                rows = Mathf.Min(rows, 2);   // 段高只按两行预留；超出由 LedCell 自行压缩行高
                var grid = new Rect(matrix.x, matrix.y,
                    cols * (LedCellMax + 4f), rows * (LedCellMax + 4f));
                RUIF.Industrial.LedMatrix(grid, ovLedStatuses, cols);
            }

            if (wide)
            {
                // 图例：四色 + 计数（数字化：颜色与数字一一对应）
                float lx = body.xMax - 142f;
                float ly = body.y;
                DrawLedLegendRow(ref ly, lx, 0, T("LedIdle"),
                    CountLed(0));
                DrawLedLegendRow(ref ly, lx, 1, T("LedRun"),
                    CountLed(1));
                DrawLedLegendRow(ref ly, lx, 2, T("LedWarn"),
                    CountLed(2));
                DrawLedLegendRow(ref ly, lx, 3, T("LedFault"),
                    CountLed(3));
            }
        }

        private void DrawLedLegendRow(ref float y, float x, int status, string label, int count)
        {
            float h = 18f;
            RUIF.Draw.Box(new Rect(x, y + 5f, 8f, 8f), RUIF.Industrial.LedColor(status));
            UIComponents.Label(new Rect(x + 12f, y, 130f, h),
                label + " · " + count, UITheme.FontLabel,
                status >= 2 ? UITheme.Warn : UITheme.Muted, TextAnchor.MiddleLeft);
            y += h;
        }

        private int CountLed(int status)
        {
            int n = 0;
            for (int i = 0; i < ovLedStatuses.Length; i++)
            {
                if (ovLedStatuses[i] == status)
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// 设备匹配段：第一行「匹配 N · 多余 N」，第二行**单独**列出缺失设备名（橙色）。
        /// <para>数据来自 <c>stats.MissingDeviceLabels</c>（快照已算好，本页只负责显示 ——
        /// 旧实现把缺失名挤在正文同一行里，缺口为 0 时玩家根本看不到有这项）。</para>
        /// </summary>
        private void DrawFitSection(Rect rect, float width)
        {
            UIComponents.Panel(rect);
            float padX = UITheme.SpaceXs;
            UIComponents.Label(new Rect(rect.x + padX, rect.y + UITheme.SpaceXxs, 200f, 18f),
                T("FitTitle"), UITheme.FontLabel, UITheme.Muted);

            string body = fitBodyText ?? string.Empty;
            UIComponents.Label(new Rect(rect.x + padX, rect.y + 20f, rect.width - padX * 2f, 18f),
                body, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft);

            if (stats.HasExpectedDevices && stats.MissingDeviceLabels.Count > 0)
            {
                UIComponents.Label(new Rect(rect.x + padX, rect.y + 38f, rect.width - padX * 2f, 18f),
                    T1("FitMissingNames", string.Join(" / ", stats.MissingDeviceLabels)),
                    UITheme.FontLabel, UITheme.Warn, TextAnchor.MiddleLeft);
            }
        }

        // ═══════════ 大组件区（CHG-20261005-TRENDBOARD，2026-10-05 玩家批准候选 1 / 3 / 4）═══════════
        //
        // 三件的组件契约均取自 RUIF 源码逐行核对（skill 步 1「取证不可跳过」）：
        //   · Charts.HeatmapGrid(r, float[行,列]) —— 按网格内最大值归一，逐格 HeatColor 画 cw-1 × ch-1，
        //     **不画轴/刻度/标签**（Charts.cs:458-474）⇒ 行列标签、色阶图例由本页补；
        //   · Charts.StackedBarChartV(r, float[系列][列]) —— 共享 Nice 网格 + 逐列自底堆叠
        //     Tokens.Series(j)；**不画类目标签/图例**（Charts.cs:227-262）⇒ 由本页补；
        //   · Charts.LineChart(r, float[系列][列], Color[], dots, grid) —— 多系列共享 y 上界
        //     （Charts.cs:282-307）⇒ 两量纲必须先归一化，见 DrawEfficiencySection 注释。

        /// <summary>
        /// 大组件区：上 = 设备使用波峰（候选 1）；下 = 左「瓶颈演变」+ 右「稼动 vs 产出」（候选 4 / 3）。
        /// <para>各段高度由 <c>rect.height</c> 反推（自适应），滚动范围由 <see cref="OvBigBlockH"/> 给出。</para>
        /// </summary>
        private void DrawBigBlocks(Rect rect)
        {
            DrawPeakSection(new Rect(rect.x, rect.y, rect.width, PeakSectionH));
            float y = rect.y + PeakSectionH + Gap;
            float halfW = (rect.width - Gap) / 2f;
            DrawBottleTrendSection(new Rect(rect.x, y, halfW, EffSectionH));
            DrawEfficiencySection(new Rect(rect.x + halfW + Gap, y, halfW, EffSectionH));
        }

        /// <summary>
        /// 候选 1：设备使用波峰（7 日 × 24 小时热力网格）。
        ///
        /// <para><b>为什么用列合计找峰值</b>：单看某一天会受"那天恰好停工"干扰；
        /// 列合计回答的是排班关心的问题 ——「<b>通常</b>几点最忙」（可用于把点检/维护排到低谷）。</para>
        /// </summary>
        private void DrawPeakSection(Rect rect)
        {
            UIComponents.Panel(rect);
            float padX = UITheme.SpaceXs;
            UIComponents.Label(new Rect(rect.x + padX, rect.y + UITheme.SpaceXxs,
                    Mathf.Max(80f, rect.width - padX * 2f - 220f), 18f),
                T("PeakTitle"), UITheme.FontLabel, UITheme.Muted);

            if (!ovHasRunTrend || ovPeakBestHour < 0)
            {
                // LAND-009 空态：不画一张全暗网格假装"有数据但很低"。
                UIComponents.Label(new Rect(rect.x + padX, rect.y + 26f,
                        rect.width - padX * 2f, 18f),
                    T("PeakNoData"), UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
                return;
            }

            // 右上角：峰值时段 + 合计机时（图给形状、数给准确值 —— UI-009 可自证）。
            string best = T1("PeakBest", ovPeakBestHour) + " · "
                + T1("PeakBestHours", ovPeakBestHours.ToString("0.0", CultureInfo.InvariantCulture));
            UIComponents.Label(new Rect(rect.xMax - padX - 210f, rect.y + UITheme.SpaceXxs, 210f, 18f),
                best, UITheme.FontLabel, UITheme.Text, TextAnchor.MiddleRight);

            float labelH = 18f;
            float gridH = Mathf.Max(40f, rect.height - 22f - labelH - labelH - UITheme.SpaceXs);
            Rect grid = new Rect(rect.x + padX + PeakDayLabelW, rect.y + 22f,
                Mathf.Max(40f, rect.width - padX * 2f - PeakDayLabelW - 64f), gridH);
            RUIF.Charts.HeatmapGrid(grid, ovPeakGrid);

            // 行标签（上旧下新）—— 复用趋势页既有 Key（同语义不另造词）。
            float rowH = grid.height / Zone_Workshop.TrendDayCount;
            for (int d = 0; d < Zone_Workshop.TrendDayCount; d++)
            {
                int ago = Zone_Workshop.TrendDayCount - 1 - d;
                UIComponents.Label(new Rect(rect.x + padX, grid.y + d * rowH,
                        PeakDayLabelW - 4f, rowH),
                    ago == 0 ? T("TrendToday") : T1("TrendDaysAgo", ago),
                    UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleRight);
            }
            // 列标签：每 6 小时一个（24 个全标必然重叠；6 小时间隔是本宽度的可读上限）。
            for (int h = 0; h < Zone_Workshop.TrendHourCount; h += 6)
            {
                float cx = grid.x + (h + 0.5f) / Zone_Workshop.TrendHourCount * grid.width;
                UIComponents.Label(new Rect(cx - 12f, grid.yMax + 1f, 24f, labelH),
                    h.ToString(CultureInfo.InvariantCulture),
                    UITheme.FontLabel, UITheme.Dim, TextAnchor.UpperCenter);
            }

            // 色阶图例（少 → 多）：逐格取**组件自己的**色映射（ChartsFmt.HeatColor），
            // 不在本页另写一套插值（skill 红线 1：配色必须来自组件）。
            float lx = grid.xMax + 10f;
            const int swatches = 5;
            // 纵向色阶：上 = 多（深色），下 = 少（浅色）—— 与热力图"越亮越忙"的读法一致。
            // 顺序修正（2026-10-05）：原实现把两个标签都堆在色块上方（少/多/色块），
            // 玩家截图里读不出方向；现改为「标签在两端、色块居中渐变」。
            UIComponents.Label(new Rect(lx, grid.y - 2f, 60f, labelH),
                T("PeakLegendMore"), UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
            for (int i = 0; i < swatches; i++)
            {
                RUIF.Draw.Box(new Rect(lx, grid.y + 16f + i * 12f, 14f, 9f),
                    RUIF.ChartsFmt.HeatColor((swatches - i) / (float)swatches));
            }
            UIComponents.Label(new Rect(lx, grid.y + 16f + swatches * 12f + 1f, 60f, labelH),
                T("PeakLegendLess"), UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);

            UIComponents.Label(new Rect(rect.x + padX, rect.yMax - labelH - UITheme.SpaceXxs,
                    rect.width - padX * 2f, labelH),
                T("PeakFooter"), UITheme.FontLabel, UITheme.Dim, TextAnchor.LowerLeft);
        }

        /// <summary>
        /// 候选 4：瓶颈演变（近 7 日各设备机时堆叠柱）。
        /// <para><b>色序纪律</b>：系列色由组件按 <c>Tokens.Series(j)</c> 取，本页图例用**同一函数同一序**
        /// ⇒ 图例与柱体逐项对应；系列数已在数据层截到 6（含"其它"），不触发 6 色循环复用。</para>
        /// </summary>
        private void DrawBottleTrendSection(Rect rect)
        {
            UIComponents.Panel(rect);
            float padX = UITheme.SpaceXs;
            float labelH = 18f;
            UIComponents.Label(new Rect(rect.x + padX, rect.y + UITheme.SpaceXxs,
                    rect.width - padX * 2f, 18f),
                T("BottleTrendTitle"), UITheme.FontLabel, UITheme.Muted);

            if (ovBottleSeriesCount <= 0)
            {
                UIComponents.Label(new Rect(rect.x + padX, rect.y + 26f,
                        rect.width - padX * 2f, 18f),
                    T("BottleTrendNoData"), UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
                return;
            }

            // 高度分配：标题 22 + 柱图 + 天标签 18 + 图例 18 + 底边距
            // （轴列与上下各半行标签由 RUIF 的 PlotArea 在内部预留 ⇒ PC 不再自行内缩）
            float plotH = Mathf.Max(40f, rect.height - 22f - labelH - labelH - UITheme.SpaceXs);
            Rect plot = new Rect(rect.x + padX, rect.y + 22f,
                Mathf.Max(40f, rect.width - padX * 2f), plotH);
            RUIF.Charts.StackedBarChartV(plot, ovBottleSeries);

            // 天标签（CHG-20261005-COMPFIX）：柱状图**不画类目标签**（组件契约），而 7 天里
            // 目前只有 1 天有采样 ⇒ 不标"是哪一天"玩家会以为图坏了。与波峰图共用同一套标签文案。
            // ⚠ 逐列对齐：柱几何由 RUIF 在 `PlotArea`（比传入 rect **窄一个轴列**、上下各内缩
            //   半行标签）上计算，故天标签必须按**同一个矩形**取列宽 —— 拿传入 rect 直接算会整体
            //   错开一个轴列宽（34px）。`PlotRectOf` 是框架为此公开的对齐入口。
            Rect barArea = RUIF.Charts.PlotRectOf(plot);
            float colW = barArea.width / Zone_Workshop.TrendDayCount;
            for (int d = 0; d < Zone_Workshop.TrendDayCount && d < ovDailyLabels.Length; d++)
            {
                UIComponents.Label(
                    new Rect(barArea.x + d * colW, barArea.yMax + 1f, colW, labelH),
                    ovDailyLabels[d], UITheme.FontLabel, UITheme.Dim, TextAnchor.UpperCenter);
            }

            // 图例（色块 + 设备名，一行最多 4 项；名字按列宽预截断，不溢出）
            float ly = rect.yMax - labelH - UITheme.SpaceXxs;
            int legendN = Mathf.Min(ovBottleSeriesCount, 4);
            float itemW = (rect.width - padX * 2f) / Mathf.Max(1, legendN);
            for (int j = 0; j < legendN; j++)
            {
                float ix = rect.x + padX + j * itemW;
                RUIF.Draw.Box(new Rect(ix, ly + 5f, 8f, 8f), RUIF.Tokens.Series(j));
                string name = j < ovBottleNames.Length ? ovBottleNames[j] : string.Empty;
                float textW = Mathf.Max(16f, itemW - 14f);
                UIComponents.Label(new Rect(ix + 12f, ly, textW, labelH),
                    UIComponents.TruncateToWidth(name, textW, UITheme.FontLabel),
                    UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
            }
        }

        /// <summary>
        /// 候选 3：稼动 vs 产出（双序列折线，各自峰值 = 100）。
        ///
        /// <para><b>为什么必须归一化</b>：<c>LineChart</c> 的多系列契约是<b>共享 y 上界</b>
        /// （hi = 各系列最大值）—— 件数与机时量纲不同，直接同图会让小量纲那条贴底不可读。
        /// 归一到"各自峰值 = 100"后，两条线比较的是<b>形状相关性</b>（跑得多的日子是否也产得多），
        /// 这正是"稼动 ≠ 产出"要回答的问题；绝对读数在标题行与仪表台给出。</para>
        /// </summary>
        private void DrawEfficiencySection(Rect rect)
        {
            UIComponents.Panel(rect);
            float padX = UITheme.SpaceXs;
            float labelH = 18f;
            // 标题 + 脚注**同一行**（左标题 / 右脚注）：脚注原与下方图例同排 ⇒ 两段文字在
            // 半宽面板里必然交叠（2026-10-05 玩家截图实证「右四运动·左四新」交错乱码）。
            // skill 铁律 3：IMGUI 无裁剪，溢出必被后画元素盖住 —— 这里改为各行独占自己的 rect。
            UIComponents.Label(new Rect(rect.x + padX, rect.y + UITheme.SpaceXxs,
                    rect.width - padX * 2f - 200f, 18f),
                T("EffTitle"), UITheme.FontLabel, UITheme.Muted);
            UIComponents.Label(new Rect(rect.xMax - padX - 200f, rect.y + UITheme.SpaceXxs, 200f, 18f),
                T("EffFooter"), UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleRight);

            if (!ovHasEffData || ovEffSeries == null)
            {
                UIComponents.Label(new Rect(rect.x + padX, rect.y + 26f,
                        rect.width - padX * 2f, 18f),
                    T("EffNoData"), UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
                return;
            }

            // 高度分配：标题 22 + 折线 + 图例 18 + 底边距（轴列与上下半行标签由 RUIF 内部预留）
            float plotH = Mathf.Max(40f, rect.height - 22f - labelH - UITheme.SpaceXs);
            Rect plot = new Rect(rect.x + padX, rect.y + 22f,
                Mathf.Max(40f, rect.width - padX * 2f), plotH);
            RUIF.Charts.LineChart(plot, ovEffSeries, ovEffColors, dots: true, grid: true);

            float ly = rect.yMax - labelH - UITheme.SpaceXxs;
            RUIF.Draw.Box(new Rect(rect.x + padX, ly + 5f, 8f, 8f), ovEffColors[0]);
            UIComponents.Label(new Rect(rect.x + padX + 12f, ly, 56f, labelH),
                T("EffLegendProduced"), UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
            RUIF.Draw.Box(new Rect(rect.x + padX + 70f, ly + 5f, 8f, 8f), ovEffColors[1]);
            UIComponents.Label(new Rect(rect.x + padX + 82f, ly, 56f, labelH),
                T("EffLegendRun"), UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
        }
    }
}
