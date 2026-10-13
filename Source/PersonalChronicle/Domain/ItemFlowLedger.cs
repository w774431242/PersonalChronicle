using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 物品级产消账本（**殖民地级**，2026-09-20，`REQ-MVS-013` / `CHG-MVS-ITEMFLOW-001`）。
    ///
    /// <para><b>用途</b>：为市场面板展开行（二级成员物品）的「生产 / 消费 / 净值」三列提供
    /// **按 ThingDef 记账的实测读数** —— 在此之前物品级供需在数据层不存在（品种粒度），
    /// 三列只能画 <c>—</c>（详见 `EconomyMarketPanelSnapshot.EconomyMarketMember` 类注释）。</para>
    ///
    /// <para><b>口径（用户 2026-09-20 裁决「建议用月或者季」）</b>：4 个季度槽滚动
    /// （1 季 = <see cref="GenDate.TicksPerQuadrum"/> = 15 天；2 季 = 30 天 ≈ 原版一个「月」）。
    /// 展示值 = <see cref="TryRatePerDay"/>：<c>(当前季 + 上一季件数) / 覆盖天数</c>，
    /// 覆盖天数 = <c>min(当前季已过天数 + 15, 自账本建立以来的天数, 30)</c>。
    /// 分母取实际覆盖天数而非固定 30，避免新殖民地/新物品被"用 30 天作分母"人为低估。</para>
    ///
    /// <para><b>为什么新建本类而不复用既有累加器</b>（ARCH 评审 D-01 记录）：
    /// <see cref="ProductionAccumulator"/> / <see cref="ConsumptionAccumulator"/> 都是
    /// **per-pawn**（挂在 <c>PawnObject</c> 上）且只有**累计量**（季度桶只记**市值**，不记件数）；
    /// 本需求四要素 =「殖民地级 + 物品级 + **件数** + **季窗**」——四者都是新数据面。</para>
    ///
    /// <para><b>为什么不复用 <see cref="QuadrumBuckets"/></b>（ARCH 评审 B-03 记录）：
    /// 那个工具是「**单个** <c>List&lt;float&gt;</c> + 拷贝移位」，服务"每 pawn 一条曲线"；
    /// 本类是「**每 defName 一条曲线**」的字典族，滚动必须 O(1)（引用旋转）而非 O(keys) 拷贝 ——
    /// 两者数据结构与目的都不同，不构成"同功能两处实现"。旋转不变量由单测钉死：
    /// 8 个槽实例恒互异、最老槽清零、`shift ≥ 4` 全清。</para>
    ///
    /// <para><b>稀疏</b>：只记出现过的 defName（不物化全表）；单槽键数有界
    /// （<see cref="MaxKeysPerSlot"/>，超出即拒绝写入 + 每 <see cref="OverflowWarnIntervalTicks"/>
    /// 至多一条日志 —— 有界性是 PERF 硬约束，不得改为无界增长）。</para>
    ///
    /// <para><b>已知局限（如实登记，勿当缺陷重报）</b>：① 窗口内没有"停产多久"的信息，
    /// 旧产物滚出 4 季即归零；② 本账本**只覆盖已捕获的产消路径**（制造/烹饪/屠宰/采矿/
    /// 伐木收获/建造产物/摄入），建造耗材、`GetAdditionalHarvestYield` 附加产出、
    /// 车间"额外产出/耗材返还"不经这些钩子 —— 因此 `0` 可能同时意味着"未覆盖"与"真无产出"
    /// （口径说明由市场面板副标承载）。</para>
    ///
    /// <para><b>对齐纪律（ARCH 评审 B-02）</b>：写入（<see cref="Record"/>）与
    /// **读取**（<see cref="TryRatePerDay"/>）两路都会调用 <see cref="Advance"/> ——
    /// 只在写入时对齐的话，一个闲置了 n 季的殖民地会把 n 季前的件数留在"当前季"
    /// ⇒ 读数「停在过去」（与 <c>ProductionAccumulator.AdvanceQuadrum</c> 同一纪律）。</para>
    /// </summary>
    public sealed class ItemFlowLedger : IExposable
    {
        /// <summary>季度槽数（4 季 = 1 年；近 1 季/近 2 季读数都从中取）。</summary>
        public const int QuadrumSlots = 4;

        /// <summary>
        /// 单槽键数上限（有界性硬约束，PERF）。原版可交易 ThingDef ≈ 405，
        /// 加 Mod 物品留足余量；超出后**拒绝新键**（已存在的键照常累加）。
        /// </summary>
        public const int MaxKeysPerSlot = 1024;

        /// <summary>参与日均的最近窗口数（2 季 ≈ 30 天 ≈ 原版一个「月」）。</summary>
        public const int RecentWindowCount = 2;

        /// <summary>溢出告警的最小间隔（tick）—— 沿用 `Patch_PawnTakeDamage` 的节流范式。</summary>
        public const int OverflowWarnIntervalTicks = 6000;

        /// <summary>生产槽（槽 3 = 当前季；槽 0 = 最老）。</summary>
        private Dictionary<string, int>[] productionSlots;

        /// <summary>消费槽（同上）。</summary>
        private Dictionary<string, int>[] consumptionSlots;

        /// <summary>流向槽单槽键数上限（有界性硬约束；一个键 = 一对 material→product）。</summary>
        public const int MaxFlowKeysPerSlot = 2048;

        /// <summary>
        /// 流向键分隔符。
        ///
        /// <para>⚠ <b>2026-10-02 P0 存档事故修复</b>：原值为 <c>'\u0001'</c>，它是 <b>XML 1.0 非法字符</b>。
        /// 该键经 <c>Scribe_Collections.Look(..., LookMode.Value, LookMode.Value)</c> 落盘时，
        /// <c>XmlWellFormedWriter</c> 抛 <c>ArgumentException</c>（<c>hexadecimal value 0x01, is an invalid character</c>）
        /// 并把 writer 置为 error 态，随后<b>所有</b>写入（含 <c>finally</c> 里的 <c>ExitNode</c>）都抛
        /// <c>InvalidOperationException: The Writer is closed or in error state</c> ——
        /// <b>整个存档保存失败</b>（已用同构造的 XmlWriter 离线复现：文件被截断在含非法字符的元素处）。</para>
        ///
        /// <para>新值 <c>'|'</c> 的选择依据：<c>'|'</c> 在 XML <b>元素名</b>中非法 ⇒ 任何 <c>defName</c>
        /// （均源自 XML 元素名）都不可能包含它 ⇒ 拼接仍无歧义，同时它是合法的 XML 文本字符。</para>
        /// </summary>
        private const char FlowKeySeparator = '|';

        /// <summary>
        /// 旧版分隔符（<c>'\u0001'</c>）：仅用于读档迁移 —— 改写成
        /// <see cref="FlowKeySeparator"/> 而非当普通非法字符抹掉，以保留键语义。
        /// </summary>
        private const char LegacyFlowKeySeparator = '\u0001';

        /// <summary>其它 XML 非法字符的替换字符（清洗是兜底：宁可键变形，也不能让存档失败）。</summary>
        private const char IllegalKeyCharReplacement = '_';

        /// <summary>
        /// **流向槽**（2026-09-20，`REQ-MVS-018`）：键 = <c>material|product</c>（扁平化；
        /// 分隔符见 <see cref="FlowKeySeparator"/>，2026-10-02 由 <c>'\u0001'</c> 改为 <c>'|'</c>），
        /// 值 = 该季度内 <c>material</c> 被 <c>product</c> 的生产**消耗**的件数。
        ///
        /// <para><b>为什么扁平化而不是嵌套字典</b>：<c>Scribe_Collections.Look</c> 的
        /// <c>LookMode.Value</c> **不支持嵌套字典**（第二层会静默写不出、读不回 —— 存档级事故）。
        /// 扁平键既可直接 Scribe，又保持 O(1) 写入。</para>
        ///
        /// <para><b>语义（本工程唯一的"流向"真值来源）</b>：数据来自
        /// <c>GenRecipe.MakeRecipeProducts</c> 的 <c>ingredients</c> 实参（该方法的唯一调用点 =
        /// <c>Toils_Recipe</c>，覆盖工作台/简易点/屠宰/熔炼等全部配方路径）。
        /// 原版**不追踪流向** ⇒ 这是"中心物品流向各下游"的唯一 ② 实测来源。</para>
        ///
        /// <para><b>覆盖局限（如实登记）</b>：只覆盖**配方消耗**；采掘/收获/建造耗材/摄入
        /// 不产生流向记录 ⇒ 查无记录 ≠ 真无流向。</para>
        /// </summary>
        private Dictionary<string, int>[] flowSlots;

        /// <summary>当前季绝对序号（<c>tick / GenDate.TicksPerQuadrum</c>）；−1 = 账本尚未建立。</summary>
        public long QuadrumAnchor = -1L;

        /// <summary>账本建立时刻（分母"账本年龄"的来源；−1 = 未建立）。</summary>
        public long StartTick = -1L;

        /// <summary>溢出告警节流（会话内瞬态，不落盘）。</summary>
        [Unsaved]
        private long lastOverflowWarnTick = -1L;

        public ItemFlowLedger()
        {
            productionSlots = NewSlots();
            consumptionSlots = NewSlots();
            flowSlots = NewSlots();
        }

        /// <summary>
        /// 记一笔**原料流向**：<paramref name="material"/> 被 <paramref name="product"/> 的生产
        /// 消耗了 <paramref name="qty"/> 件（2026-09-20，`REQ-MVS-018`）。
        /// 非法输入静默忽略（调用方在只读钩子里，不得抛）。
        /// </summary>
        public void RecordMaterialFlow(string material, string product, int qty, long tick)
        {
            if (string.IsNullOrEmpty(material) || string.IsNullOrEmpty(product) || qty <= 0)
            {
                return;
            }
            EnsureSlots();
            Advance(tick / GenDate.TicksPerQuadrum);
            if (StartTick < 0L)
            {
                StartTick = tick;
            }
            Dictionary<string, int> current = flowSlots[QuadrumSlots - 1];
            // 清洗：material / product 理论上是 defName（本应安全），但存档有一条硬约束 ——
            // 「宁可键变形，也不能让一个非法字符把整次保存炸掉」（2026-10-02 P0 事故教训）。
            string key = SanitizeKey(material) + FlowKeySeparator + SanitizeKey(product);
            int old;
            if (!current.TryGetValue(key, out old))
            {
                if (current.Count >= MaxFlowKeysPerSlot)
                {
                    WarnOverflow(tick);
                    return;
                }
                old = 0;
            }
            current[key] = old + qty;
        }

        /// <summary>
        /// 取某物品作为**原料**时，近两季流向各产物的件数（**降序**；同值按产物 defName 序号序兜底，
        /// 保证跨会话确定性）。
        ///
        /// <para><c>false</c> = 无任何流向记录 —— ⚠ 这**不等于**"该物品没有流向"（覆盖局限见
        /// <see cref="flowSlots"/> 注释），调用方必须按"无数据"处理，不得当作 0。</para>
        ///
        /// <para>⚠ 本方法会顺带 <see cref="Advance"/>（幂等）——读取路径也要对齐（同
        /// <see cref="TryRatePerDay"/> 的纪律）。</para>
        /// </summary>
        public bool TryMaterialFlows(string material, long nowTick, List<KeyValuePair<string, int>> result)
        {
            if (result == null || string.IsNullOrEmpty(material) || nowTick < 0L)
            {
                return false;
            }
            result.Clear();
            EnsureSlots();
            if (QuadrumAnchor < 0L || StartTick < 0L)
            {
                return false;
            }
            Advance(nowTick / GenDate.TicksPerQuadrum);

            string prefix = material + FlowKeySeparator;
            Dictionary<string, int> totals = new Dictionary<string, int>(System.StringComparer.Ordinal);
            for (int i = QuadrumSlots - RecentWindowCount; i < QuadrumSlots; i++)
            {
                foreach (KeyValuePair<string, int> pair in flowSlots[i])
                {
                    if (pair.Key == null || !pair.Key.StartsWith(prefix, System.StringComparison.Ordinal))
                    {
                        continue;
                    }
                    string product = pair.Key.Substring(prefix.Length);
                    int old;
                    totals.TryGetValue(product, out old);
                    totals[product] = old + pair.Value;
                }
            }
            if (totals.Count == 0)
            {
                return false;
            }
            foreach (KeyValuePair<string, int> pair in totals)
            {
                result.Add(pair);
            }
            result.Sort((a, b) =>
            {
                if (a.Value != b.Value)
                {
                    return b.Value - a.Value; // 件数降序
                }
                return string.CompareOrdinal(a.Key ?? string.Empty, b.Key ?? string.Empty);
            });
            return true;
        }

        private static Dictionary<string, int>[] NewSlots()
        {
            Dictionary<string, int>[] slots = new Dictionary<string, int>[QuadrumSlots];
            for (int i = 0; i < QuadrumSlots; i++)
            {
                slots[i] = new Dictionary<string, int>();
            }
            return slots;
        }

        /// <summary>
        /// 记一笔物品流。<paramref name="production"/> true = 生产、false = 消费。
        /// 非法输入（空键 / 非正数量）静默忽略（调用方在只读钩子里，不得抛）。
        /// </summary>
        public void Record(string defName, int qty, long tick, bool production)
        {
            if (string.IsNullOrEmpty(defName) || qty <= 0)
            {
                return;
            }
            EnsureSlots();
            Advance(tick / GenDate.TicksPerQuadrum);
            if (StartTick < 0L)
            {
                StartTick = tick;
            }

            Dictionary<string, int>[] slots = production ? productionSlots : consumptionSlots;
            Dictionary<string, int> current = slots[QuadrumSlots - 1];
            int old;
            if (!current.TryGetValue(defName, out old))
            {
                if (current.Count >= MaxKeysPerSlot)
                {
                    WarnOverflow(tick);
                    return;
                }
                old = 0;
            }
            current[defName] = old + qty;
        }

        /// <summary>
        /// 把 8 个槽对齐到 <paramref name="quadrumIndex"/>（幂等；写入与读取路径都要调用）。
        /// 跨 1~3 季 = 引用旋转 + 最老槽 <c>Clear()</c> 复用（O(1) 指针交换）；
        /// 跨 ≥4 季 = 旋转 4 次（等价于全清）。
        /// </summary>
        public void Advance(long quadrumIndex)
        {
            EnsureSlots();
            if (QuadrumAnchor < 0L)
            {
                QuadrumAnchor = quadrumIndex;
                return;
            }
            long shift = quadrumIndex - QuadrumAnchor;
            if (shift <= 0L)
            {
                return;
            }
            int rotations = shift >= QuadrumSlots ? QuadrumSlots : (int)shift;
            for (int r = 0; r < rotations; r++)
            {
                RotateOne(productionSlots);
                RotateOne(consumptionSlots);
                RotateOne(flowSlots); // REQ-MVS-018：流向槽同规滚动（三族槽必须同步，否则窗口错位）
            }
            QuadrumAnchor = quadrumIndex;
        }

        private static void RotateOne(Dictionary<string, int>[] slots)
        {
            Dictionary<string, int> oldest = slots[0];
            for (int i = 0; i < slots.Length - 1; i++)
            {
                slots[i] = slots[i + 1];
            }
            slots[slots.Length - 1] = oldest;
            oldest.Clear();
        }

        /// <summary>
        /// 取某物品近两季的日均速率（件/日）。
        ///
        /// <para><c>false</c> = 账本尚未建立（新档 / 从未记账）—— 调用方**不填数字**（画 `—`）。
        /// <c>true</c> = 有读数（**可能为 0**：近期确无产出，属真值）。</para>
        ///
        /// <para>⚠ 本方法会顺带 <see cref="Advance"/>（幂等）—— 读取路径也做对齐，理由见类注释。</para>
        /// </summary>
        public bool TryRatePerDay(string defName, bool production, long nowTick, out float perDay)
        {
            perDay = 0f;
            if (string.IsNullOrEmpty(defName) || nowTick < 0L)
            {
                return false;
            }
            EnsureSlots();
            if (QuadrumAnchor < 0L || StartTick < 0L)
            {
                return false; // 账本未建立 ⇒ 无读数（不是 0 件/日）
            }
            Advance(nowTick / GenDate.TicksPerQuadrum);

            Dictionary<string, int>[] slots = production ? productionSlots : consumptionSlots;
            int total = 0;
            for (int i = QuadrumSlots - RecentWindowCount; i < QuadrumSlots; i++)
            {
                int value;
                if (slots[i].TryGetValue(defName, out value))
                {
                    total += value;
                }
            }

            long ticksPerQuadrum = GenDate.TicksPerQuadrum;
            long currentQuadrum = nowTick / ticksPerQuadrum;
            float elapsedDaysInQuadrum = (nowTick - currentQuadrum * ticksPerQuadrum) / (float)GenDate.TicksPerDay;
            float coveredDays = elapsedDaysInQuadrum
                + (RecentWindowCount - 1) * (ticksPerQuadrum / (float)GenDate.TicksPerDay);
            float ageDays = (nowTick - StartTick) / (float)GenDate.TicksPerDay;
            if (ageDays < coveredDays)
            {
                coveredDays = ageDays; // 账本比窗口年轻 ⇒ 只按实际年龄平均（防"用 30 天作分母"低估）
            }
            if (coveredDays < 1f)
            {
                coveredDays = 1f; // 下界 1 天：建立当天不产生除零/放大
            }
            perDay = total / coveredDays;
            return true;
        }

        /// <summary>槽/引用归一（新档、读档、旧档都要过一遍；幂等）。</summary>
        private void EnsureSlots()
        {
            if (productionSlots == null || productionSlots.Length != QuadrumSlots)
            {
                productionSlots = NewSlots();
            }
            if (consumptionSlots == null || consumptionSlots.Length != QuadrumSlots)
            {
                consumptionSlots = NewSlots();
            }
            if (flowSlots == null || flowSlots.Length != QuadrumSlots)
            {
                // 旧档没有流向槽 ⇒ 独立空槽（不回填、不猜）；读路径据此返回 false（画 `—`）。
                flowSlots = NewSlots();
            }
            for (int i = 0; i < QuadrumSlots; i++)
            {
                if (productionSlots[i] == null)
                {
                    productionSlots[i] = new Dictionary<string, int>();
                }
                if (consumptionSlots[i] == null)
                {
                    consumptionSlots[i] = new Dictionary<string, int>();
                }
                if (flowSlots[i] == null)
                {
                    flowSlots[i] = new Dictionary<string, int>();
                }
            }
        }

        private void WarnOverflow(long nowTick)
        {
            if (lastOverflowWarnTick >= 0L && nowTick - lastOverflowWarnTick < OverflowWarnIntervalTicks)
            {
                return;
            }
            lastOverflowWarnTick = nowTick;
            LogOverflowThrottled();
        }

        /// <summary>
        /// 溢出日志。**必须隔离到独立方法**：<c>Verse.Log</c> 是 ECall —— 在非 Unity 宿主
        /// （离线单测进程）调用会抛 <c>SecurityException</c>（ECall 铁律，本工程已多次踩）。
        /// 故 <c>[NoInlining]</c> 隔离 + 局部吞掉：游戏内正常输出，离线宿主静默降级，
        /// **不得影响记账语义**（溢出本就只是"拒绝新键"）。
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void LogOverflowThrottled()
        {
            try
            {
                ChronicleLog.Warning(ChronicleLog.Category.Save,
                    "[MVS][ItemFlow] ledger slot full (" + MaxKeysPerSlot + " keys); further new items are not recorded this quadrum.");
            }
            catch (System.Exception)
            {
                // 非 Unity 宿主无 ECall 宿主 ⇒ 吞掉（测试进程）；游戏内不会走到这里。
            }
        }

        public void ExposeData()
        {
            EnsureSlots();
            // ⚠ 存档硬约束（2026-10-02 P0 事故修复）：必须在 Scribe 之前把槽键清洗为 XML 合法字符。
            // 键里只要混入一个非法字符，XmlWellFormedWriter 立即进入 error 态 ⇒
            // **整个存档保存失败**（不是丢一条数据，而是全盘失败、文件被截断）。
            // 保存前就地清洗同时修正内存态，保证「内存键 == 存档键」，读取路径无需再做映射。
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                SanitizeSlotKeys(productionSlots);
                SanitizeSlotKeys(consumptionSlots);
                SanitizeSlotKeys(flowSlots);
            }
            Scribe_Values.Look(ref StartTick, "startTick", -1L);
            Scribe_Values.Look(ref QuadrumAnchor, "quadrumAnchor", -1L);
            // 8 个槽逐个 Scribe（键名稳定；改名会丢历史数据）。
            for (int i = 0; i < QuadrumSlots; i++)
            {
                Scribe_Collections.Look(ref productionSlots[i], "prod" + i, LookMode.Value, LookMode.Value);
                Scribe_Collections.Look(ref consumptionSlots[i], "cons" + i, LookMode.Value, LookMode.Value);
                // REQ-MVS-018 流向槽：扁平键（material|product）⇒ LookMode.Value 可直接序列化。
                // 旧档无该键 ⇒ null ⇒ EnsureSlots 归一为空槽（幂等，不改既有语义）。
                Scribe_Collections.Look(ref flowSlots[i], "flow" + i, LookMode.Value, LookMode.Value);
            }
            // 读档后归一（缺键/损坏 ⇒ 独立空槽；保证旋转不变量"8 实例互异"）。
            EnsureSlots();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 旧档兼容：历史键里的 '\u0001' 改写为新分隔符，其它非法字符抹为 '_'。
                // （理论上旧档写不进含 '\u0001' 的键 —— 那会让整次保存失败 —— 此处是幂等兜底。）
                SanitizeSlotKeys(productionSlots);
                SanitizeSlotKeys(consumptionSlots);
                SanitizeSlotKeys(flowSlots);
            }
        }

        /// <summary>
        /// 清洗一个槽族里所有键（就地改写，幂等）。
        ///
        /// <para>为什么必须在**保存前**做：XML 非法字符会让 <c>XmlWellFormedWriter</c> 进入 error 态，
        /// 之后连 <c>ExitNode</c> 都失败 ⇒ 整次存档报废（2026-10-02 P0 事故：流向键用了 <c>'\u0001'</c>）。</para>
        ///
        /// <para>改写规则：旧分隔符 <c>'\u0001'</c> → <see cref="FlowKeySeparator"/>（保留语义）；
        /// 其它非法字符 → <see cref="IllegalKeyCharReplacement"/>。若两个键清洗后重名则<b>合并计数</b>。</para>
        /// </summary>
        private static void SanitizeSlotKeys(Dictionary<string, int>[] slots)
        {
            if (slots == null)
            {
                return;
            }
            for (int i = 0; i < slots.Length; i++)
            {
                Dictionary<string, int> slot = slots[i];
                if (slot == null || slot.Count == 0)
                {
                    continue;
                }
                List<KeyValuePair<string, string>> renames = null;
                List<KeyValuePair<string, int>> incoming = null;
                foreach (KeyValuePair<string, int> pair in slot)
                {
                    string fixedKey = SanitizeKey(pair.Key);
                    if (fixedKey == null || string.Equals(fixedKey, pair.Key, System.StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (renames == null)
                    {
                        renames = new List<KeyValuePair<string, string>>();
                        incoming = new List<KeyValuePair<string, int>>();
                    }
                    renames.Add(new KeyValuePair<string, string>(pair.Key, fixedKey));
                    incoming.Add(new KeyValuePair<string, int>(fixedKey, pair.Value));
                }
                if (renames == null)
                {
                    continue;
                }
                // 先摘除旧键，再合并写入（避免"新名 == 某个待删旧名"时被误删）。
                for (int r = 0; r < renames.Count; r++)
                {
                    slot.Remove(renames[r].Key);
                }
                for (int r = 0; r < incoming.Count; r++)
                {
                    int old;
                    slot.TryGetValue(incoming[r].Key, out old);
                    slot[incoming[r].Key] = old + incoming[r].Value;
                }
            }
        }

        /// <summary>把字符串改为 XML 合法文本（无需改写时**原样返回同一引用**，避免无谓分配）。</summary>
        private static string SanitizeKey(string key)
        {
            if (key == null)
            {
                return null;
            }
            bool dirty = false;
            for (int i = 0; i < key.Length; i++)
            {
                if (!IsLegalXmlChar(key[i]))
                {
                    dirty = true;
                    break;
                }
            }
            if (!dirty)
            {
                return key;
            }
            char[] buffer = key.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                char c = buffer[i];
                if (IsLegalXmlChar(c))
                {
                    continue;
                }
                buffer[i] = c == LegacyFlowKeySeparator ? FlowKeySeparator : IllegalKeyCharReplacement;
            }
            return new string(buffer);
        }

        /// <summary>
        /// XML 1.0 合法字符判定（<c>#x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD]</c>）。
        /// 代理对（D800–DFFF）交给 <c>XmlWriter</c> 自行处理，不在此拦截。
        /// </summary>
        private static bool IsLegalXmlChar(char c)
        {
            if (c == '\t' || c == '\n' || c == '\r')
            {
                return true;
            }
            if (c < ' ')
            {
                return false;
            }
            return c != '\uFFFE' && c != '\uFFFF';
        }
    }
}
