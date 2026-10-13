using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间<b>设备台账</b>（2026-09-25）：按设备（thingIDNumber）累计
    /// <b>运行时长</b>与<b>产出件数</b>（今日 / 累计），供车间管理「设备」页读取。
    ///
    /// <para><b>口径</b>：</para>
    /// <list type="bullet">
    /// <item>运行：与设备页/稼动率<b>同源</b> —— <see cref="WorkshopDeviceStateResolver.KeyOf"/>
    /// 判为"运行中"（设备自报优先 → 原版 comp 反推）即按采样间隔累计 tick；
    /// <item>产出：成品出炉即按其 <b>当前工作台</b>（job target A）归账
    /// （<c>Patch_GenRecipe</c> → manager.<c>NotifyProductMade</c>），
    /// <b>不要求工人处于本系统值勤</b> —— 设备产出是物理事实，与"谁在做、是不是本系统派的"无关
    /// （2026-10-02 修复：旧门槛让"派去做原版工单"的产出永远不计数，设备页产出列恒 <c>0 / 0</c>）；
    /// 仅统计已被车间设备扫描追踪的工作台（见 <see cref="IsTracked"/>）；
    /// <item>「今日 / 本周（<see cref="DeviceStatEntry.ProducedWindowDays"/> 天）」两个读数均由逐日环形缓冲直接给出：
    /// 跨日自然归零、不依赖 <see cref="RollDay"/> 的调用时机；累计产出长期保留（设备数量有限，有界）。
    /// </list>
    /// </summary>
    public sealed class WorkshopDeviceLedger : IExposable
    {
        private Dictionary<int, DeviceStatEntry> entries = new Dictionary<int, DeviceStatEntry>();

        private DeviceStatEntry Entry(Thing thing)
        {
            if (thing == null)
            {
                return null;
            }
            if (entries == null)
            {
                entries = new Dictionary<int, DeviceStatEntry>();
            }
            DeviceStatEntry entry;
            if (!entries.TryGetValue(thing.thingIDNumber, out entry) || entry == null)
            {
                entry = new DeviceStatEntry();
                entries[thing.thingIDNumber] = entry;
            }
            return entry;
        }

        /// <summary>采样到一次"运行中"：累加采样间隔 tick。</summary>
        public void AddRunTicks(Thing device, long ticks)
        {
            if (device == null || ticks <= 0L)
            {
                return;
            }
            Entry(device).AddRunTicks(ticks);
        }

        /// <summary>
        /// 确保该设备在台账中**存在条目**（不改变任何数值）。
        ///
        /// <para>2026-10-02：产量归账以「条目是否存在」判断该工作台是否属于车间（<see cref="IsTracked"/>），
        /// 而条目原先只在「采样到运行中」时建立 ⇒ 一台已被扫描、但整轮都没被判为"运行中"的设备
        /// 会在首次产出时漏记。因此由设备扫描路径无条件 <c>Touch</c> 一次。</para>
        /// </summary>
        public void Touch(Thing device)
        {
            if (device == null || device.Destroyed)
            {
                return;
            }
            Entry(device);
        }

        /// <summary>
        /// 同上，并登记「首次看到时刻」（CHG-20261005-DEVCOLS「建立时间」列）。
        /// <para>由设备扫描路径每轮传入当前 tick；<see cref="DeviceStatEntry.MarkSeen"/> 幂等
        /// ⇒ 每 60 tick 调一次不会改写首次时间。旧档既有条目首次被本重载调用时补记"现在"、
        /// 之后固定不变 —— 比永远显示「—」有用，又不会每天漂移（更不谎报建造时刻）。</para>
        /// </summary>
        public void Touch(Thing device, long nowTick)
        {
            if (device == null || device.Destroyed)
            {
                return;
            }
            Entry(device).MarkSeen(nowTick);
        }

        /// <summary>台账首次看到该设备的 tick（0 = 未知 ⇒ 该列显示「—」）。</summary>
        public long FirstSeenTickOf(Thing device)
        {
            DeviceStatEntry entry = TryGet(device);
            return entry != null ? entry.FirstSeenTick : 0L;
        }

        /// <summary>累计产值（银；CHG-20261005-DEVCOLS）。</summary>
        public float ProducedValueOf(Thing device)
        {
            DeviceStatEntry entry = TryGet(device);
            return entry != null ? entry.ProducedValue : 0f;
        }

        /// <summary>累加一件成品的市场价值（与 <c>AddProduced</c> 在**同一次归账**里调用）。</summary>
        public void AddProducedValue(Thing device, float value)
        {
            if (device == null || value <= 0f)
            {
                return;
            }
            Entry(device).AddProducedValue(value);
        }

        /// <summary>产出归账到指定工作台（今日 + 累计各 +1）。</summary>
        public void AddProduced(Thing device, int day, int count)
        {
            if (device == null || count <= 0)
            {
                return;
            }
            Entry(device).AddProduced(day, count);
        }

        public long RunTicksOf(Thing device)
        {
            DeviceStatEntry entry = TryGet(device);
            return entry != null ? entry.RunTicks : 0L;
        }

        /// <summary>
        /// 指定绝对天的**运行时长**（CHG-20261005-TRENDBOARD；无条目/无记录 ⇒ 0）。
        /// <para>概览页「稼动 vs 产出」与「瓶颈演变」按车间聚合逐台读数
        /// ⇒ <b>不新增第二份车间级逐日存储</b>（避免同一事实两处落盘、两处漂移）。</para>
        /// </summary>
        public int RunTicksOnDay(Thing device, int day)
        {
            DeviceStatEntry entry = TryGet(device);
            return entry != null ? entry.RunTicksOnDay(day) : 0;
        }

        /// <summary>
        /// 累加**指定设备**在指定绝对天的运行时长（由设备运行采样路径逐台调用）。
        /// <para>与 <c>AddRunTicks(device, ticks)</c>（累计总量）成对：同一次"运行中"判定下
        /// 两个入口各记一笔，总量与逐日之和恒等。</para>
        /// </summary>
        public void AddRunTicksOnDay(Thing device, int day, long ticks)
        {
            if (device == null || ticks <= 0L)
            {
                return;
            }
            Entry(device).AddRunTicksOnDay(day,
                ticks > int.MaxValue ? int.MaxValue : (int)ticks);
        }

        public int ProducedTotalOf(Thing device)
        {
            DeviceStatEntry entry = TryGet(device);
            return entry != null ? entry.ProducedTotal : 0;
        }

        public int ProducedTodayOf(Thing device)
        {
            DeviceStatEntry entry = TryGet(device);
            return entry != null ? entry.ProducedToday : 0;
        }

        /// <summary>
        /// 最近 <see cref="DeviceStatEntry.ProducedWindowDays"/> 天（含 <paramref name="today"/>）产出合计。
        /// 设备页「产出」列与顶部 KPI 横幅的口径（2026-10-02 起由「今日产量」改为 5 天窗口）。
        /// </summary>
        public int ProducedInWindowOf(Thing device, int today)
        {
            DeviceStatEntry entry = TryGet(device);
            return entry != null ? entry.ProducedInWindow(today) : 0;
        }

        /// <summary>
        /// 指定「绝对天」的产出（2026-10-02 新增）：设备页「产出」列的**每日**读数，
        /// 即 <c>ProducedOnDayOf(thing, GenDate.DaysPassed)</c>。
        ///
        /// <para>与 <see cref="ProducedTodayOf"/> 的区别：后者读的是 <c>producedToday</c> 累加器，
        /// 其归零依赖管理器调用 <see cref="RollDay"/>；本方法直接查逐日缓冲，
        /// 跨日瞬间即自然归零，不依赖任何外部调用时机。</para>
        /// </summary>
        public int ProducedOnDayOf(Thing device, int day)
        {
            DeviceStatEntry entry = TryGet(device);
            return entry != null ? entry.ProducedOnDay(day) : 0;
        }

        /// <summary>
        /// 该设备是否已被台账追踪（= 属于某次车间设备扫描）。
        /// 供产量归账过滤「车间之外的工作台」—— 非车间设备不应进入设备台账。
        /// </summary>
        public bool IsTracked(Thing device)
        {
            return TryGet(device) != null;
        }

        private DeviceStatEntry TryGet(Thing device)
        {
            DeviceStatEntry entry;
            return device != null && entries != null && entries.TryGetValue(device.thingIDNumber, out entry)
                ? entry
                : null;
        }

        /// <summary>跨日：今日产出归零（由管理器在日变更时调用一次）。</summary>
        public void RollDay(int today)
        {
            if (entries == null)
            {
                return;
            }
            foreach (KeyValuePair<int, DeviceStatEntry> pair in entries)
            {
                if (pair.Value != null)
                {
                    pair.Value.RollDay(today);
                }
            }
        }

        /// <summary>
        /// 淘汰不在给定 ID 集合中的设备条目（2026-10-02 审计 P1-D）。
        ///
        /// <para>旧实现只增不删：设备被拆除后条目永久滞留，随游戏时长<b>无界增长</b>；
        /// 且若「读档后 <c>thingIDNumber</c> 重新分配」的既有假设成立（同文件
        /// <c>WorkshopProductRegistry.Clear()</c> 的注释依据），读档后旧 ID 条目还会
        /// <b>挂到错误的设备上</b>。</para>
        ///
        /// <para>由设备扫描路径（<c>AccumulateDeviceRuntime</c>，每 60 tick）以
        /// 「本轮实际在场设备」调用 —— 同时闭合两个问题：拆除即淘汰（有界），
        /// 读档后首轮扫描即清掉旧 ID（与 <c>WorkshopProductRegistry.Clear()</c> 同一口径）。</para>
        /// </summary>
        public void PruneNotIn(HashSet<int> keepIds)
        {
            if (entries == null || keepIds == null || entries.Count == 0)
            {
                return;
            }
            List<int> stale = null;
            foreach (KeyValuePair<int, DeviceStatEntry> pair in entries)
            {
                if (!keepIds.Contains(pair.Key))
                {
                    if (stale == null)
                    {
                        stale = new List<int>();
                    }
                    stale.Add(pair.Key);
                }
            }
            if (stale == null)
            {
                return;
            }
            for (int i = 0; i < stale.Count; i++)
            {
                // CHG-20261005-DEVRENAME：淘汰带自定义名的条目 ⇒ 同步递减计数
                // （否则计数永久偏大，读路径早退判据失效 = 每帧白跑字典查找）。
                DeviceStatEntry pruned;
                if (entries.TryGetValue(stale[i], out pruned) && pruned != null && pruned.HasCustomName)
                {
                    customNameCount--;
                }
                entries.Remove(stale[i]);
            }
        }

        /// <summary>
        /// 「已改名条目数」（CHG-20261005-DEVRENAME）：<b>运行态、不落盘</b>。
        ///
        /// <para><b>唯一用途</b>：给标签读路径（Capture 单点 Postfix，UI 每帧热读）一个 O(1) 的
        /// 「本图有没有任何自定义名」早退判据，避免热路径每帧做字典查找。</para>
        ///
        /// <para>⚠ <b>漂移即「假阴性 = 原版静默不显示」</b>，故可变点固定为三处、且只有三处：
        /// <see cref="SetCustomNameOf"/>（改名/清空）、<see cref="PruneNotIn"/>（淘汰带名条目）、
        /// <see cref="ExposeData"/> 的 PostLoadInit 全量重算。任何新增写路径都必须同步维护它。</para>
        ///
        /// <para>不落盘的理由：它是可从 <c>entries</c> 完全重建的派生量，落盘就等于制造第二真相源。</para>
        /// </summary>
        private int customNameCount;

        /// <summary>本图已改名条目数（0 = 无任何自定义名 ⇒ 读路径可立即早退）。</summary>
        public int CustomNameCount
        {
            get { return customNameCount; }
        }

        /// <summary>该设备当前的自定义名（无条目 / 未改名 ⇒ null）。</summary>
        public string CustomNameOf(Thing device)
        {
            DeviceStatEntry entry = TryGet(device);
            return entry == null ? null : entry.CustomName;
        }

        /// <summary>
        /// 写入自定义名（<b>入参须已归一化</b>；null = 清除），并同步维护 <see cref="customNameCount"/>。
        /// <para>设备已拆则拒绝（不建条目 —— 避免为消失的设备留幽灵名）。</para>
        /// </summary>
        internal void SetCustomNameOf(Thing device, string normalized)
        {
            if (device == null || device.Destroyed)
            {
                return;
            }
            DeviceStatEntry entry = Entry(device);
            bool had = entry.HasCustomName;
            entry.SetCustomName(normalized);
            bool has = entry.HasCustomName;
            if (!had && has)
            {
                customNameCount++;
            }
            else if (had && !has)
            {
                customNameCount--;
            }
        }

        /// <summary>从 <c>entries</c> 全量重建 <see cref="customNameCount"/>（读档单点，不落盘）。</summary>
        private void RecomputeCustomNameCount()
        {
            int n = 0;
            if (entries != null)
            {
                foreach (KeyValuePair<int, DeviceStatEntry> kv in entries)
                {
                    if (kv.Value != null && kv.Value.HasCustomName)
                    {
                        n++;
                    }
                }
            }
            customNameCount = n;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref entries, "entries", LookMode.Value, LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (entries == null)
                {
                    entries = new Dictionary<int, DeviceStatEntry>();
                }
                // CHG-20261005-DEVRENAME：运行态计数不落盘 ⇒ 读档时全量重建（无第二真相源）。
                RecomputeCustomNameCount();
            }
        }
    }

    /// <summary>单台设备的累计运行与产出。</summary>
    public sealed class DeviceStatEntry : IExposable
    {
        /// <summary>
        /// 「产出」统计窗口长度（天）= 一周：设备页「产出」列与顶部 KPI 横幅使用的滑动周期。
        ///
        /// <para>2026-10-02（用户要求「将产出分为每日和每周」）：定为 7 天，列内同时给出
        /// 「今日」与「本周」两个读数 —— 今日由逐日缓冲直接按天取，不依赖 <see cref="RollDay"/> 的调用时机。</para>
        ///
        /// <para>⚠ 由 5 改 7 对旧档安全：写入槽位只需 <c>day % N</c>，而读取
        /// （<see cref="ProducedInWindow"/>）按每槽自带的<b>绝对天</b>过滤，不依赖槽位映射；
        /// <c>EnsureWindow()</c> 会把旧档的 5 槽补到 7 槽（新槽为空槽，语义为"无记录"）。</para>
        /// </summary>
        public const int ProducedWindowDays = 7;

        private long runTicks;
        private int producedTotal;
        private int producedToday;
        private int producedDay = -1;

        /// <summary>
        /// 逐日产出环形缓冲（槽位 = 绝对天 mod <see cref="ProducedWindowDays"/>）；
        /// <see cref="windowDays"/> 记录每个槽当前对应的绝对天，不匹配即视为空槽。
        /// 新增字段、缺省为空 ⇒ 旧档照常加载（无 Migration，SAVE-004）。
        /// </summary>
        private List<int> windowCounts = new List<int>();
        private List<int> windowDays = new List<int>();

        /// <summary>
        /// 逐日**运行时长**环形缓冲（CHG-20261005-TRENDBOARD，2026-10-05 玩家批准候选 3/4）。
        /// 槽位与 <see cref="windowCounts"/> 完全同构（<c>day % ProducedWindowDays</c> + 槽内绝对天校验）
        /// ⇒ 同一条不变式：跨日自然归零、<b>扩容安全</b>、旧档缺省为空由 <see cref="EnsureWindow"/> 补齐。
        /// <para>单位 = 采样累计 tick（与 <c>runTicks</c> 同源），显示时除 <c>GenDate.TicksPerHour</c>。</para>
        /// </summary>
        private List<int> runWindowCounts = new List<int>();
        private List<int> runWindowDays = new List<int>();

        /// <summary>
        /// 台账**首次看到**该设备的 tick（CHG-20261005-DEVCOLS「建立时间」列的数据源）。
        ///
        /// <para><b>为什么不读原版字段</b>：原版 <c>Thing</c> 不记录"建造完成时刻"
        /// （无 buildTick 之类；建筑只有 def/HitPoints）。本字段语义 = "本台账首次采到它的时刻"，
        /// 对玩家等同于"加入本车间的时间"。</para>
        ///
        /// <para><b>加性可选字段</b>：旧档 = 0 ⇒ 该列显示「—」，不谎报"今天建立"（LAND-009）。</para>
        /// </summary>
        private long firstSeenTick;

        /// <summary>
        /// 累计**产值**（CHG-20261005-DEVCOLS「累计产值」列的数据源，单位 = 银）：
        /// 每件成品出炉按其 <c>MarketValue</c> 累加。
        ///
        /// <para>与 <see cref="producedTotal"/> 在**同一次归账**里写入（同源、不各记一遍）；
        /// 旧档 = 0 ⇒ 从新版起重新累计 —— 历史产出的市场价不可追溯，**不猜测回填**。</para>
        /// </summary>
        private float producedValue;

        public long RunTicks => runTicks;
        public int ProducedTotal => producedTotal;
        public int ProducedToday => producedToday;

        /// <summary>台账首次看到该设备的 tick（0 = 旧档无记录 ⇒ 显示「—」）。</summary>
        public long FirstSeenTick => firstSeenTick;

        /// <summary>累计产值（银）。</summary>
        public float ProducedValue => producedValue;

        /// <summary>记录"首次被看到"（幂等：已有记录不覆盖，读档后不会被重置成新时间）。</summary>
        public void MarkSeen(long tick)
        {
            if (firstSeenTick <= 0L && tick > 0L)
            {
                firstSeenTick = tick;
            }
        }

        /// <summary>累加一件成品的价值（非正/NaN/Inf 直接忽略，不污染读数）。</summary>
        public void AddProducedValue(float value)
        {
            if (value > 0f && !float.IsNaN(value) && !float.IsInfinity(value))
            {
                producedValue += value;
            }
        }

        public void AddRunTicks(long ticks)
        {
            runTicks += ticks;
        }

        public void AddProduced(int day, int count)
        {
            if (producedDay != day)
            {
                producedToday = 0;
                producedDay = day;
            }
            producedToday += count;
            producedTotal += count;
            BumpWindow(day, count);
        }

        /// <summary>
        /// 指定绝对天的产出（逐日缓冲查询；该天无记录 ⇒ 0）。
        ///
        /// <para>2026-10-02 新增：设备页「每日产出」读数。直接按槽位取（<c>day % N</c>）并用槽内
        /// 自带的绝对天校验，因此跨日即自然归零，不依赖 <see cref="RollDay"/> 被调用。</para>
        /// </summary>
        public int ProducedOnDay(int day)
        {
            EnsureWindow();
            int slot = day % ProducedWindowDays;
            if (slot < 0)
            {
                slot += ProducedWindowDays;
            }
            return windowDays[slot] == day ? windowCounts[slot] : 0;
        }

        /// <summary>最近 <see cref="ProducedWindowDays"/> 天（含 <paramref name="today"/>）的产出合计。</summary>
        public int ProducedInWindow(int today)
        {
            EnsureWindow();
            int sum = 0;
            int from = today - ProducedWindowDays + 1;
            for (int slot = 0; slot < ProducedWindowDays; slot++)
            {
                int day = windowDays[slot];
                if (day >= from && day <= today)
                {
                    sum += windowCounts[slot];
                }
            }
            return sum;
        }

        private void BumpWindow(int day, int count)
        {
            EnsureWindow();
            int slot = day % ProducedWindowDays;
            if (slot < 0)
            {
                slot += ProducedWindowDays;
            }
            if (windowDays[slot] != day)
            {
                windowDays[slot] = day;
                windowCounts[slot] = 0;
            }
            windowCounts[slot] += count;
        }

        /// <summary>
        /// 补齐窗口缓冲长度（读档后 / 旧档补齐时调用；幂等，可重复）。
        ///
        /// <para><b>槽位不变式（改窗口长度前必读）</b>：写入槽位由 <c>day % ProducedWindowDays</c>
        /// 定位，而读取按<b>每槽自带的绝对天</b>过滤（不依赖槽位映射）—— 因此<b>扩容</b>（5 → 7）
        /// 安全：新槽为空槽，读路径自然忽略。但<b>缩容</b>会让旧槽错位、导致读数错算；
        /// 若将来要缩短窗口，必须同时做 Migration 重排槽位，或改用单调递增的槽位映射。</para>
        /// </summary>
        /// <summary>
        /// 指定绝对天的**运行时长**（采样 tick；该天无记录 ⇒ 0）。
        ///
        /// <para>CHG-20261005-TRENDBOARD（2026-10-05 玩家批准候选 3「稼动 vs 产出」与候选 4
        /// 「瓶颈演变」）：与 <see cref="ProducedOnDay"/> 完全同构 —— 按槽位取（<c>day % N</c>）
        /// 并用槽内自带的绝对天校验，<b>跨日自然归零，不依赖 <see cref="RollDay"/> 的调用时机</b>。
        /// 显示时除 <c>GenDate.TicksPerHour</c> 换算为"机时"。</para>
        /// </summary>
        public int RunTicksOnDay(int day)
        {
            EnsureWindow();
            int slot = SlotOf(day);
            return runWindowDays[slot] == day ? runWindowCounts[slot] : 0;
        }

        /// <summary>
        /// 累加指定绝对天的运行时长（由采样路径每 60 tick 调用一次）。
        /// <para>与 <c>AddRunTicks(long)</c>（累计总量）并存：总量供瓶颈带「占比」，
        /// 本缓冲供趋势图「逐日」—— 两者口径同为"运行中"的采样累加，不得各判一次状态。</para>
        /// </summary>
        public void AddRunTicksOnDay(int day, int ticks)
        {
            if (ticks <= 0)
            {
                return;
            }
            EnsureWindow();
            int slot = SlotOf(day);
            if (runWindowDays[slot] != day)
            {
                runWindowDays[slot] = day;
                runWindowCounts[slot] = 0;
            }
            runWindowCounts[slot] += ticks;
        }

        /// <summary>
        /// 环形槽位（含负天守卫）。CHG-20261005-TRENDBOARD：原先写法在 ProducedOnDay 与 BumpWindow
        /// 各重复一份（<c>day % N</c> + 负值修正），新增第四个调用点前先收口为单一实现（GOV-005）。
        /// </summary>
        private static int SlotOf(int day)
        {
            int slot = day % ProducedWindowDays;
            return slot < 0 ? slot + ProducedWindowDays : slot;
        }

        /// <summary>
        /// 补齐窗口缓冲长度（读档后 / 旧档补齐时调用；幂等，可重复）。
        ///
        /// <para><b>槽位不变式（改窗口长度前必读）</b>：写入槽位由 <c>day % ProducedWindowDays</c>
        /// 定位，而读取按<b>每槽自带的绝对天</b>过滤（不依赖槽位映射）—— 因此<b>扩容</b>（5 → 7）
        /// 安全：新槽为空槽，读路径自然忽略。但<b>缩容</b>会让旧槽错位、导致读数错算；
        /// 若将来要缩短窗口，必须同时做 Migration 重排槽位，或改用单调递增的槽位映射。</para>
        ///
        /// <para>CHG-20261005-TRENDBOARD：同法补齐<b>运行时长</b>缓冲（产出/机时两条趋势同窗，
        /// 才能在一张图上按天对齐比较）。</para>
        /// </summary>
        private void EnsureWindow()
        {
            if (windowCounts == null)
            {
                windowCounts = new List<int>();
            }
            if (windowDays == null)
            {
                windowDays = new List<int>();
            }
            if (runWindowCounts == null)
            {
                runWindowCounts = new List<int>();
            }
            if (runWindowDays == null)
            {
                runWindowDays = new List<int>();
            }
            while (windowCounts.Count < ProducedWindowDays)
            {
                windowCounts.Add(0);
            }
            while (windowDays.Count < ProducedWindowDays)
            {
                windowDays.Add(int.MinValue);   // 永不等于任何真实天数 ⇒ 空槽
            }
            while (runWindowCounts.Count < ProducedWindowDays)
            {
                runWindowCounts.Add(0);
            }
            while (runWindowDays.Count < ProducedWindowDays)
            {
                runWindowDays.Add(int.MinValue);
            }
        }

        /// <summary>
        /// 车间自定义名（CHG-20261005-DEVRENAME，2026-10-05 玩家诉求）。
        ///
        /// <para><b>加性字段、无需 Migration</b>：缺省 <c>null</c> = 未改名 ⇒ 旧档照常加载；
        /// 显示按回落链「车间名 ?? 档案覆盖 ?? 原版默认名」（REQ-WORKSHOP-005）。</para>
        ///
        /// <para>⚠ <b>写入前必须经 <c>PersonalChronicle.Domain.WorkshopDeviceNaming.Normalize</c></b>
        /// —— 本字段会被 Scribe 写进存档 XML，XML 非法字符会导致<b>整档保存失败</b>（P0-A）。
        /// 唯一合法写口是 <c>MapComponent_WorkshopManager.TryRenameDevice</c>。</para>
        /// </summary>
        private string customName;

        /// <summary>自定义名（null = 未改名）。</summary>
        public string CustomName
        {
            get { return customName; }
        }

        /// <summary>是否已改名（标签读路径的 O(1) 快路径判据）。</summary>
        public bool HasCustomName
        {
            get { return !string.IsNullOrEmpty(customName); }
        }

        /// <summary>写入自定义名（<b>必须已归一化</b>；null = 清除）。</summary>
        public void SetCustomName(string normalized)
        {
            customName = normalized;
        }

        public void RollDay(int today)
        {
            if (producedDay != today)
            {
                producedToday = 0;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref runTicks, "runTicks", 0L);
            Scribe_Values.Look(ref producedTotal, "producedTotal", 0);
            Scribe_Values.Look(ref producedToday, "producedToday", 0);
            Scribe_Values.Look(ref producedDay, "producedDay", -1);
            // 窗口缓冲（2026-10-02 新增）：缺省为空 ⇒ 旧档照常加载，读档后由 EnsureWindow 补齐长度。
            Scribe_Collections.Look(ref windowCounts, "windowCounts", LookMode.Value);
            Scribe_Collections.Look(ref windowDays, "windowDays", LookMode.Value);
            // CHG-20261005-TRENDBOARD：逐日运行时长窗（加性字段：缺省为空 ⇒ 旧档照常加载，
            // PostLoadInit 的 EnsureWindow 补齐长度）。（本行是补登 —— 首次实现时漏了落盘。）
            Scribe_Collections.Look(ref runWindowCounts, "runWindowCounts", LookMode.Value);
            Scribe_Collections.Look(ref runWindowDays, "runWindowDays", LookMode.Value);
            // CHG-20261005-DEVCOLS：建立时间 + 累计产值（加性字段：旧档 = 0 ⇒ 建立时间列显示「—」）。
            Scribe_Values.Look(ref firstSeenTick, "firstSeenTick", 0L);
            Scribe_Values.Look(ref producedValue, "producedValue", 0f);
            // CHG-20261005-DEVRENAME：自定义名（加性字段：缺省 null ⇒ 旧档照常加载，无需 Migration）。
            Scribe_Values.Look(ref customName, "customName", null);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureWindow();
            }
        }
    }
}
