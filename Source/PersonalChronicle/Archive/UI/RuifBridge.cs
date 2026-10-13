using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive.UI
{
    /// <summary>
    /// **RUIF 接入桥**（2026-09-12 接入，方案见 `系统中心蓝图/待落地项/制造系统中心_RUIF改造方案.md`）。
    ///
    /// <para><b>依赖性质（事实陈述 · 2026-09-16 校正）</b>：PC 对 RUIF 是<strong>硬依赖</strong> ——
    /// <c>About/About.xml</c> 的 <c>modDependencies</c> + <c>loadAfter</c>，且
    /// <c>Source/PersonalChronicle.csproj</c> 对 RUIF 程序集为**无条件 <c>&lt;Reference&gt;</c>**
    /// ⇒ 缺 RUIF 时**编译期即失败**（该场景不在支持范围内，本桥不为其提供任何降级路径）。</para>
    ///
    /// <para><b>本桥的意义 = 版本门控 + 原子异常降级</b>，覆盖两类<strong>同装</strong>边界：
    /// ① **版本漂移** —— RUIF 已装但低于某 API 的引入版本（走降级阶梯 D1 / D2）；
    /// ② **加载时序** —— PC 的 Mod 构造期 RUIF 可能尚未进入 <c>AppDomain</c>
    /// （见 <see cref="Available"/> 的节流重试）。</para>
    ///
    /// <para>⚠ <b>守卫的真实边界</b>：版本敏感类型若被写在**声明级类型位置**（字段 / 形参 /
    /// 返回 / 局部变量），其解析在**类型加载 / JIT 期**发生，**早于**本桥返回 <c>false</c> ⇒
    /// 该写法下 D1 / D2 均**不可达**。降级阶梯的真实可达范围以
    /// `docs/设计文档/架构/ADR-AMD-1-StatTable跨库边界与降级契约.md` §2 / §4 为准。</para>
    ///
    /// <para><b>两条硬性纪律</b>（RUIF 官方 `Core/Api.cs` 文档已明示，且与本工程 ECall 铁律同源）：</para>
    /// <list type="number">
    /// <item><b>探测必须用反射扫 AppDomain</b> —— 一旦直接引用 <c>RUIF.*</c> 类型，框架未装时会在
    /// <b>JIT 期</b>抛 <c>FileNotFoundException</c>/<c>TypeLoadException</c>，<b>根本走不到 try/catch</b>；</item>
    /// <item><b>所有触碰 RUIF 类型的代码必须收进同一个 <c>[MethodImpl(NoInlining)]</c> 方法</b> ——
    /// 否则会被内联回调用方，等于没隔离（本工程在 ECall 上已两次踩过同样的坑）。</item>
    /// </list>
    ///
    /// <para><b>跨边界只传值类型</b>：<see cref="TokenSet"/> 是纯 <c>Color</c> 结构 ⇒ 不会把 RUIF 的
    /// 引用类型泄漏到 PC 侧对象图（避免"RUIF 卸载/版本变化"引发的悬空引用）。</para>
    /// </summary>
    internal static class RuifBridge
    {
        private static bool probedOnce;
        private static bool available;
        private static long lastProbeTicks;
        private static bool probeLogged;

        /// <summary>
        /// 探测命中的 RUIF 程序集（<see cref="Probe"/> 缓存）。
        /// 供版本门控读取 `RUIF.Api.VersionString`（编译期常量 ⇒ 不触发类型初始化，见 <see cref="ReadVersionAtLeast"/>）。
        /// </summary>
        private static Assembly ruifAssembly;

        /// <summary>未确认可用时的**重试间隔**（秒）。</summary>
        private const float RetryIntervalSeconds = 2f;

        /// <summary>
        /// RUIF 是否可用。
        ///
        /// <para><b>⭐ 2026-09-12 根因修复（此前"探测一次 + 永久缓存 false"是致命缺陷）</b>：
        /// RimWorld 的 `LoadedModManager.CreateModClasses()` 按 **typeof(Mod).InstantiableDescendantsAndSelf()
        /// 的类型枚举顺序** 创建 Mod 实例，**不是按加载顺序**（RUIF 官方 `Core/Api.cs` 已明示）。
        /// 而 PC 的 `UITheme.Apply()` 可能在 **Mod 构造期**就被调用 —— 那一刻 RUIF 完全可能**尚未加载**、
        /// 不在 `AppDomain` 里 ⇒ 首次探测返回 false 并被**永久缓存** ⇒ **此后所有 RUIF 功能全部失效**
        /// （令牌/状态灯/闪烁/时序条/页签/仪表盘一并静默失效，且无任何日志）。</para>
        ///
        /// <para><b>修法</b>：确认可用后永久缓存 `true`；未确认时按 <see cref="RetryIntervalSeconds"/>
        /// **节流重试** ⇒ RUIF 无论何时加载，最迟 2 秒后即被识别（代价：未装 RUIF 时每 2 秒一次
        /// `AppDomain` 扫描，可用 `assembly.GetType(name, false)` 的 try/catch 兜底，开销可忽略）。</para>
        /// </summary>
        internal static bool Available
        {
            get
            {
                if (available)
                {
                    return true;   // 已确认 ⇒ 不再探测
                }
                long now = DateTime.UtcNow.Ticks;
                if (probedOnce
                    && now - lastProbeTicks < (long)(TimeSpan.TicksPerSecond * RetryIntervalSeconds))
                {
                    return false;  // 节流窗口内沿用上次结果
                }
                probedOnce = true;
                lastProbeTicks = now;
                available = Probe();
                if (available)
                {
                    LogProbeOnce();
                    // ⭐ RUIF 就绪的那一刻**立刻补应用一次令牌** —— 启动期 `UITheme.Apply()` 时
                    //   RUIF 往往还没进 AppDomain（Mod 类创建顺序不定）⇒ 那时套的是本工程调色板；
                    //   此处幂等补一次，保证"令牌层"最终一定生效。
                    UITheme.ReapplyForRuif();
                }
                return available;
            }
        }

        /// <summary>
        /// 探测成功只记一次日志（DevMode 门控）。
        /// ⚠ 必须 try/catch 容错包装 —— 启动期路径上的日志若抛异常会外溢成 <c>TypeInitializationException</c>
        /// 并影响其它 Mod（本工程既有铁律）。
        /// </summary>
        private static void LogProbeOnce()
        {
            if (probeLogged)
            {
                return;
            }
            probeLogged = true;
            try
            {
                if (Prefs.DevMode)
                {
                    Verse.Log.Message("[PersonalChronicle] RUIF detected — RUIF-backed UI enabled.");
                }
            }
            catch
            {
                // 启动期日志失败不得外溢
            }
        }

        /// <summary>
        /// 反射扫 <c>AppDomain</c> 查找 <c>RUIF.Api</c> 类型 —— **全程不引用 RUIF 类型**
        /// （这是版本门控唯一安全的探测方式）。
        /// </summary>
        private static bool Probe()
        {
            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    Assembly assembly = assemblies[i];
                    if (assembly == null)
                    {
                        continue;
                    }
                    if (assembly.GetType("RUIF.Api", throwOnError: false) != null)
                    {
                        ruifAssembly = assembly;   // 供版本门控（仅"修复版"才启用受缺陷影响的组件）
                        return true;
                    }
                }
            }
            catch
            {
                // 探测本身失败 ⇒ 视为不可用（不抛：绝不能因为"可选增强"把主界面带崩）
            }
            return false;
        }

        /// <summary>
        /// RUIF 令牌快照（**值类型**；跨隔离边界安全）。
        ///
        /// <para>只收**与 PC `UITheme` 同名同义**的令牌 —— 实测 `RUIF.Tokens` 与 PC 令牌命名高度重合，
        /// 因此可以"保留 PC 字段名、替换取值来源"，从而实现**全局换令牌而调用点零改动**。</para>
        /// </summary>
        internal struct TokenSet
        {
            internal Color Window;
            internal Color Panel;
            internal Color PanelRaised;
            internal Color Card;
            internal Color CardHover;
            internal Color Border;
            internal Color BorderSoft;
            internal Color BorderHair;
            internal Color Text;
            internal Color Muted;
            internal Color Dim;
            internal Color Accent;
            internal Color Info;
            internal Color Alive;
            internal Color Dead;
            internal Color Warn;
        }

        /// <summary>
        /// **RUIF 工业仪表盘**可用的最低版本。
        ///
        /// <para><b>第一轮（build `0912v`）</b>：`Industrial.Gauge` 的分区弧走 `Draw.Rotate`，v1.0.1 及更早用
        /// `GUIUtility.RotateAroundPivot`（pivot 按「旋转前的 GUI 空间」解释）⇒ 在本工程窗口的
        /// 平移/裁剪作用域下 pivot 整体错位，45×N 个 8×3 色块甩成横跨画布的彩色弧
        /// （实机截图为证；消费者侧"套 `GUI.BeginClip`"的规避亦实测证伪）。
        /// **RUIF v1.0.2 已把 `Draw.Rotate` 改为"局部空间后乘矩阵"**，故当时把闸门定为 1.0.2。</para>
        ///
        /// <para><b>第二轮（build `0913a`，本闸门提升的真正原因）</b>：v1.1.0 把表盘从 `Draw.Rotate` 改为
        /// **贴图像素烘焙**（为绕开 GUI/GL 矩阵），但烘焙时把 **GUI 空间（y 向下为正）** 的坐标原样写进了
        /// **纹理缓冲（y=0 在左下）** ⇒ **整张表盘沿 y 轴镜像**：实机表现为「开口朝上的 ∪ + 指针甩到卡片顶部
        /// + 读数与弧压叠」，即用户报的「**组件倒置**」；同一版表盘**也没有任何刻度**。
        /// 该缺陷在**已发布的 v1.1.0 字节里**，无法靠消费侧规避 ⇒ 闸门提升到 **v1.1.1**（RUIF 修该根因的版本）。
        /// 装机 RUIF 低于该版本时**一律回落** PC 自绘表盘（`AppShell.DrawKpiDial`，同几何且带刻度），
        /// 从而「装/不装 RUIF、装哪一版」都不会再出现倒置或裸弧。</para>
        ///
        /// <para><b>第三轮（build `0913b`）</b>：v1.1.1 已修好倒置与"没有刻度"，但**几何仍与标签抢位** ——
        /// 半径取 <c>r.height−20</c>、轴心 <c>r.yMax−8</c> ⇒ 弧顶落在 <c>r.y+12</c>，
        /// 与 14px 高的左上标签带**重叠 2px**；标签左对齐、长度越过卡片中线时**末字正好压在外圈弧顶**
        /// （弧为不透明亮色 ⇒ 文字读不出来），即用户报的「**标题被遮挡**」；
        /// 同一版刻度只有 12.5% 一格，相邻量级读不出来。v1.1.2 把「弧顶 ≤ 标题带下沿」做成几何约束
        /// （<c>GaugeLabelBand = 15</c>）、刻度细化为 5% 一格（21 位）、标签按可用宽截断，
        /// 并把越界的缓存键槽（阈值 6085 → 11 位装不下）改用 <c>TexCache.KeyData</c>。
        /// ⇒ 闸门再抬到 **`1.1.2`**。</para>
        /// </summary>
        internal const string GaugeFixedVersion = "1.1.2";

        /// <summary>
        /// **RUIF 工业仪表盘**（P4，用户批准「KPI 换指针表盘」的受控偏离）。
        /// <paramref name="v01"/> 为 0–1 占比；标签画在表盘左上。
        ///
        /// <para><b>前置</b>：RUIF 可用 **且版本 ≥ <see cref="GaugeFixedVersion"/>**（见该常量注释）；
        /// 组件缺失 / 版本过旧 / 绘制异常 ⇒ 返回 false，调用方<span>回落</span> PC 自绘等价表盘
        /// （`AppShell.DrawKpiDial`，几何同式且不使用任何 GUI 旋转）。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryDrawGauge(Rect rect, float v01, string label)
        {
            if (!Available || !VersionAtLeast(GaugeFixedVersion))
            {
                return false;
            }
            try
            {
                RUIF.Industrial.Gauge(rect, v01, 0f, 1f, label);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ══════════════════ 图表桥接（2026-09-15 · 用户要求一/二）══════════════════
        //
        // **契约变更**：RUIF 自 2026-09-15 起为**前置依赖（硬依赖）**，组件方向内容**必须引用 RUIF**、
        // 禁止自研组件 ⇒ 以下方法**不提供自研回落**：RUIF 不可用时记一次节流错误日志并返回 false
        // （调用方**不画**，而不是退化到自绘图）。
        //
        // RUIF 图表契约（DevDocs/API参考.md §RUIF.Charts）：
        //   ① 颜色只经令牌 —— 传 null 即由 RUIF 取 Series 系列色（禁裸 new Color）；
        //   ② **图表不读 Mouse/Event** —— 交互（悬停提示等）由调用方在 rect 上自行判定；
        //   ③ 零贴图直绘；④ 刻度缓冲区框架内静态复用（每帧零堆分配）。

        /// <summary>
        /// **多系列折线**（`RUIF.Charts.LineChart` 多系列重载）—— 分身份存量趋势（三条人数线）。
        /// <paramref name="colors"/> 长度不足时回落 Series 色板。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryLineChartMulti(Rect rect, float[][] series, Color[] colors, bool dots, bool grid)
        {
            if (!Available || series == null || series.Length == 0 || rect.width < 8f)
            {
                return false;
            }
            try
            {
                RUIF.Charts.LineChart(rect, series, colors, dots, grid);
                return true;
            }
            catch (Exception ex)
            {
                LogChartFailure("LineChartMulti", ex);
                return false;
            }
        }

        /// <summary>
        /// **统计表 · D1 旧版**（5 参 `RUIF.DataAtoms.StatTable`）—— 表头（`PanelRaised` 底 + 左对齐）
        /// + 斑马纹行 + 比例列宽。
        ///
        /// <para><b>降级阶梯的 D1 落点</b>（ADR-AMD-1 §2 D-3）：该 5 参签名在 **1.1.2 与 1.2.0 都存在**
        /// （1.2.0 起 5 参是保留的兼容重载）⇒ 本方法**双通**、不属版本敏感，可在
        /// <see cref="TryStatTableEx"/> 的 D1 分支安全调用。行为 = 全列左对齐、无逐列样式、
        /// 无阈值带色（信息不丢，仅版式降级）。</para>
        ///
        /// <para>⚠ 行为契约（已核对 RUIF 源码 `Components/DataAtoms.cs` + `TableAtoms.cs`）：
        /// ① 不读 <c>Mouse</c>/<c>Event</c>（交互由调用方在 rect 上自判）；
        /// ② 行越出 <paramref name="rect"/> 底边即 **break** ⇒ 调用方必须按
        /// <c>(1 + rows) * rowH</c> 预留高度，否则表尾被静默截断；
        /// ③ 列宽 <c>widths[i] = shares[i] * rect.width</c>（末列吃余宽）
        /// ⇒ <c>shares</c> 必须是**占比**；本桥已在 <see cref="NormalizeShares"/> 里归一化，
        /// 故调用方传任意比例也安全。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryStatTableLegacy(Rect rect, string[] headers, string[][] rows, float[] shares, float rowH)
        {
            if (!Available || headers == null || headers.Length == 0 || rect.width < 24f)
            {
                return false;
            }
            try
            {
                RUIF.DataAtoms.StatTable(rect, headers, rows, shares, rowH);
                return true;
            }
            catch (Exception ex)
            {
                LogChartFailure("StatTable", ex);
                return false;
            }
        }

        /// <summary>
        /// **排行榜**（`RUIF.DataAtoms.RankList`）—— 名次徽记（前三有样式，`DataFmt.RankMedal`）
        /// + 姓名（72px）+ 数值条（前三 Accent / 其余 Info）+ 数值右对齐；行高 = rect 高 / n。
        /// <para>⚠ 固定列宽（22 + 72）⇒ 窄于 ~200px 会挤压数值条，调用方需自判宽度。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryRankList(Rect rect, string[] names, float[] values, float maxValue)
        {
            if (!Available || names == null || names.Length == 0 || rect.width < 200f)
            {
                return false;
            }
            try
            {
                RUIF.DataAtoms.RankList(rect, names, values, maxValue);
                return true;
            }
            catch (Exception ex)
            {
                LogChartFailure("RankList", ex);
                return false;
            }
        }

        /// <summary>面积图（`RUIF.Charts.AreaChart`）—— 能力均值 / 活跃度趋势。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryAreaChart(Rect rect, float[] values)
        {
            if (!Available || values == null || values.Length < 2 || rect.width < 8f)
            {
                return false;
            }
            try
            {
                RUIF.Charts.AreaChart(rect, values, null);
                return true;
            }
            catch (Exception ex)
            {
                LogChartFailure("AreaChart", ex);
                return false;
            }
        }

        /// <summary>
        /// **能力条 / 容量条**（`RUIF.ProgressAtoms.CapacityBar`）—— 轨道（`BorderSoft`）+ 按比例填充
        /// + 溢出警示段（`used &gt; capacity` 时右侧画 4 段 `Dead` 短块）。
        ///
        /// <para>⚠ RUIF 的**默认取色与「能力水平」语义相反**（`ratio &gt; 0.85 ? Warn : Alive` —— 那是给
        /// 「容量占用」用的）⇒ 本工程调用点**一律显式传 <paramref name="fill"/>**（各视图自己的语义色阶）。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryCapacityBar(Rect rect, float used, float capacity, Color fill)
        {
            if (!Available || rect.width < 4f || rect.height < 1f)
            {
                return false;
            }
            try
            {
                RUIF.ProgressAtoms.CapacityBar(rect, used, capacity, fill);
                return true;
            }
            catch (Exception ex)
            {
                LogChartFailure("CapacityBar", ex);
                return false;
            }
        }

        /// <summary>折线图（`RUIF.Charts.LineChart`，自带点与网格）—— 多序列趋势的每一序列。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryLineChart(Rect rect, float[] values, bool dots, bool grid)
        {
            if (!Available || values == null || values.Length < 2 || rect.width < 8f)
            {
                return false;
            }
            try
            {
                RUIF.Charts.LineChart(rect, values, null, dots, grid);
                return true;
            }
            catch (Exception ex)
            {
                LogChartFailure("LineChart", ex);
                return false;
            }
        }

        /// <summary>
        /// 环形图（`RUIF.Charts.DonutChart`）。`colors` = **逐段语义色**（2026-09-15 对 RUIF 的修复：
        /// 原签名只能按索引吃 Series 色板 ⇒ 「自由绿/奴隶红/囚犯黄」这类语义色无法表达）；
        /// `centerLabel` 可为 null。`colorAlignToHole` 语义见 RUIF 侧注释。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryDonutChart(Rect rect, float[] values, float hole01, string centerLabel,
            Color[] colors = null, float centerLabelY01 = 0.5f)
        {
            if (!Available || values == null || values.Length == 0 || rect.width < 24f)
            {
                return false;
            }
            try
            {
                RUIF.Charts.DonutChart(rect, values, hole01, centerLabel, colors, centerLabelY01);
                return true;
            }
            catch (Exception ex)
            {
                LogChartFailure("DonutChart", ex);
                return false;
            }
        }

        /// <summary>竖柱图（`RUIF.Charts.BarChartV`）—— 劳动力 12 工种 / 专业分布。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryBarChartV(Rect rect, float[] values, string[] labels)
        {
            if (!Available || values == null || values.Length == 0 || rect.width < 20f)
            {
                return false;
            }
            try
            {
                RUIF.Charts.BarChartV(rect, values, labels, null);
                return true;
            }
            catch (Exception ex)
            {
                LogChartFailure("BarChartV", ex);
                return false;
            }
        }

        /// <summary>
        /// **统计表 · 列样式 / 阈值带色列** 可用的最低 RUIF 版本（`DataAtoms.StatTable` **10 参扩展重载**）。
        /// <para>RUIF v1.2.0 起 `StatTable` **新增一个 10 参重载**
        /// （`StatCol[] cols` / `int bandCol` / `float bandRef` / `float[] bandValues` / `string[] bandTexts`
        /// 均**无默认值**）；**旧 5 参签名作为真正的方法保留**并委托到该实现（单一实现体）
        /// —— 故 1.1.2 编译的消费者在 1.2.0 下**不会** `MissingMethodException`。</para>
        /// <para>⚠ 可选参数只是**编译期**特性、调用点会把默认值展开进 IL ⇒ 不能靠"新参数全默认"
        /// 充当二进制兼容；必须保留 5 参方法本体。</para>
        /// </summary>
        internal const string StatTableColsVersion = "1.2";

        /// <summary>
        /// D0 列样式缓冲（**类型必须是 <c>object</c>**）。
        ///
        /// <para><b>为什么不写 <c>RUIF.StatCol[]</c></b>（ADR-AMD-1 §2 D-1 / §3 #2）：字段类型属「类型位置」，
        /// 会在**类型加载期**解析 <c>RUIF.StatCol</c> ⇒ 与 <c>available</c> / 版本门控 / try-catch 全无关，
        /// 1.1.2 下直接 `TypeInitializationException`。用 <c>object</c> 把类型引用推迟到
        /// <see cref="TryStatTableV12"/>（版本门控通过后才会被调用）体内，字段本身零 RUIF 类型。</para>
        /// <para>有界（长度 = 列数，≤ 8）；绘制期复用 ⇒ 零分配（PERF-001）。IMGUI 主线程独占，无并发。</para>
        /// </summary>
        private static object statColBuf;

        /// <summary>
        /// **统计表的行高真值**（<c>Measure.RowHeight(Tokens.FontLabel)</c>），缓存于
        /// <see cref="statRowHeight"/>。
        ///
        /// <para><b>为什么必须有这个方法</b>（<c>GAP-UI-ROWH-15</c> 根因）：
        /// <c>DataAtoms.StatTable</c> 的 IL 在行循环前做
        /// <c>minH = Mathf.Min(rowH, Measure.RowHeight(Tokens.FontLabel))</c>
        /// ⇒ 传进来的 <c>rowH</c> **小于**这个真值时会被**静默压扁**（行内色带与文字重叠）。
        /// 此前经济面板硬编码 <c>18f</c>，在默认字体下恰好等于真值所以没露馅；
        /// 一旦玩家改字体（大字体 / 小字体）真值漂移，该常量立刻变成"压扁"或"虚高"。</para>
        ///
        /// <para><b>RUIF 不可用时的回落</b>：返回 <see cref="FallbackRowHeight"/>
        /// （= 默认字体下的实测值）。硬依赖下这是防御性分支，不属支持场景。</para>
        ///
        /// <para>⚠ 缓存：`Measure.RowHeight` 内部走 <c>Text.LineHeightOf</c>，其结果在中文字体
        /// 热切换后才会变；本缓存**自失效**（见下）⇒ 不必每帧调用（绘制热路径）。</para>
        ///
        /// <para><b>⚠ 2026-09-19（审计 §2.8 续）</b>：原文档写着"缓存在 <see cref="InvalidateRowHeight"/>
        /// 显式清"，而 <c>InvalidateRowHeight</c> **全工程零调用方** ⇒ 缓存实际上**永远无法失效**
        /// —— <b>文档承诺了一个不存在的入口</b>。行高真值一旦取错（玩家改字体 / 改 UI 缩放 /
        /// 切语言），本会话内**不会再修正**，且症状是"行被压扁"这种看不出原因的样子。
        /// ⇒ 把失效键**放进缓存自己**：主题 / 当前语言 / UI 缩放任一变化即清
        /// （这三个正是真值的全部输入：字体由语言与缩放决定，令牌由主题决定）。</para>
        ///
        /// <para>⚠ **零分配**（PERF-001）：主题 id 是常量串、语言是对象引用 ⇒ 只做**引用比较**，
        /// 不拼字符串；UI 缩放是 float 比较。显式入口 <see cref="InvalidateRowHeight"/> 保留
        /// （幂等），但**不再是唯一入口**。</para>
        /// </summary>
        internal static float StatRowHeight()
        {
            object themeRef = UITheme.ActiveThemeId;
            object langRef = Verse.LanguageDatabase.activeLanguage;
            float scale = Prefs.UIScale;
            if (!ReferenceEquals(themeRef, statRowHeightThemeRef)
                || !ReferenceEquals(langRef, statRowHeightLangRef)
                || scale != statRowHeightScale)
            {
                statRowHeight = 0f;
                statRowHeightThemeRef = themeRef;
                statRowHeightLangRef = langRef;
                statRowHeightScale = scale;
            }

            if (statRowHeight > 0f)
            {
                return statRowHeight;
            }
            if (!Available)
            {
                return FallbackRowHeight;
            }
            float h = ReadStatRowHeight();
            if (h <= 0f)
            {
                return FallbackRowHeight;
            }
            statRowHeight = h;
            return h;
        }

        /// <summary>清除行高缓存（字体 / UI 缩放变更后调用；幂等）。</summary>
        internal static void InvalidateRowHeight()
        {
            statRowHeight = 0f;
        }

        /// <summary>
        /// 默认字体下的行高回落值。**这是实测常量、不是估算**：
        /// `Measure.RowHeight(FontLabel)` = `Text.LineHeightOf(DegradeTiny(FontLabel, TinyFontSupported))`，
        /// 在中文默认字体下 = 18f（与 `_scripts/calib_rowH.js` 对预览侧的标定结果一致）。
        /// </summary>
        internal const float FallbackRowHeight = 18f;

        /// <summary>行高真值缓存（`object` 字段纪律同此，但此处是 float ⇒ 无类型位置风险）。</summary>
        private static float statRowHeight;

        /// <summary>
        /// 行高缓存的**失效键**（审计 §2.8 续）：主题 / 语言 / UI 缩放。
        /// ⚠ 用引用/值比较而非拼字符串 —— 本方法在绘制热路径上（PERF-001 零分配）。
        /// </summary>
        private static object statRowHeightThemeRef;
        private static object statRowHeightLangRef;
        private static float statRowHeightScale = -1f;

        /// <summary>
        /// **列宽占比缓冲**（绘制期复用 ⇒ 零分配，PERF-001）。
        /// <c>float[]</c> ⇒ 无 RUIF 类型位置风险（同 <see cref="statColBuf"/> 的理由）。
        /// </summary>
        private static float[] shareBuf;

        /// <summary>
        /// 把调用方给的**任意比例**归一化成 RUIF 要求的**占比**（Σ = 1）。
        ///
        /// <para><b>⚠ 这一步是 2026-09-18 实机缺陷的根因修复</b>（经济市场表"只剩品种列、四个数值列
        /// 一个墨迹都没有"）：<c>RUIF.TableFmt.ColumnSplit</c> 的实现是
        /// <c>widths[i] = Mathf.Max(0, shares[i] * row.width)</c>、末列 <c>= row.width - used</c>
        /// —— 它把 <c>shares[i]</c> 当**占比**直接乘，**不做归一化**。
        /// 经济市场表按"任意比例"理解，传了 <c>2.385 : 1 : 1 : 1 : 1.001</c>，
        /// 于是首列宽 = 2.385 × 表宽 ≈ 2100px **把整行吃光**，其余四列被推到 <c>rect</c> 之外、
        /// 整体落在画布外 ⇒ **完全不可见**（表头同样只有第一列）。</para>
        ///
        /// <para><b>为什么收口在桥而不是让每个调用方自己记得 Σ=1</b>：
        /// ① 与 D2 自绘路径（调用方按 <c>share / Σshares</c> 切列）**语义一致** ⇒ D0/D2 观感不漂移；
        /// ② 已归一化的调用点（人口中心三张表 Σ = 1.00）**结果不变**（除以 1），向后兼容；
        /// ③ 一处收口 ⇒ 同类缺陷不再靠"每个调用方都记得"来防。</para>
        ///
        /// <para>返回**缓冲**（长度 ≥ 入参），RUIF 只读前 <c>headers.Length</c> 项 ⇒ 无副作用。
        /// 入参为 null / 全 0 ⇒ 返回 null / 均分（RUIF 侧对 null 走 <c>MakeEven</c>）。</para>
        /// </summary>
        internal static float[] NormalizeShares(float[] shares)
        {
            if (shares == null || shares.Length == 0)
            {
                return null;
            }
            if (shareBuf == null || shareBuf.Length < shares.Length)
            {
                shareBuf = new float[shares.Length];
            }
            float total = 0f;
            for (int i = 0; i < shares.Length; i++)
            {
                total += shares[i] > 0f ? shares[i] : 0f;
            }
            if (total <= 0f)
            {
                for (int i = 0; i < shares.Length; i++)
                {
                    shareBuf[i] = 1f / shares.Length;
                }
                return shareBuf;
            }
            for (int i = 0; i < shares.Length; i++)
            {
                shareBuf[i] = (shares[i] > 0f ? shares[i] : 0f) / total;
            }
            return shareBuf;
        }

        /// <summary>
        /// 真正触碰 <c>RUIF.Measure</c> 的唯一位置 —— <b>必须 NoInlining</b>
        /// （同 <see cref="ReadTokens"/> 的理由：RUIF 类型只允许出现在版本门控/可用门控之后的方法体内）。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float ReadStatRowHeight()
        {
            try
            {
                return RUIF.Measure.RowHeight(RUIF.Tokens.FontLabel);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// **统计表（扩展）· 降级阶梯分派点**：逐列对齐 / Dim 前景 + 阈值带色列
        /// （预览 v7 `.coreB` 的 8 列表 1:1 落地）。
        ///
        /// <para><b>本方法（含签名）不得出现任何 RUIF 类型</b>（ADR-AMD-1 §2 D-1 / §3 #2）：
        /// 形参一律框架内类型；RUIF 类型只存在于 <see cref="TryStatTableV12"/>（版本门控通过后
        /// 才被调用）与 <see cref="TryStatTableLegacy"/>（双通）的**方法体内**。</para>
        ///
        /// <para><b>降级阶梯</b>：</para>
        /// <list type="bullet">
        /// <item><b>D0</b> —— RUIF ≥ <see cref="StatTableColsVersion"/> ⇒ <see cref="TryStatTableV12"/>
        /// （含逐列样式 / 带色列），返回 true，调用方不再画。</item>
        /// <item><b>D1</b> —— RUIF 可用但 &lt; 1.2（或版本读不出）⇒ <see cref="TryStatTableLegacy"/>
        /// （5 参 · 左对齐 · 无带色，信息不丢但版式降级），并记**一次性**日志；仍返回 true。</item>
        /// <item><b>D2</b> —— <c>StatTable</c> 调用抛异常（或探测不可用：硬依赖下不应发生，仅防御性守卫）
        /// ⇒ 返回 false，调用方走既有 PC 同版式自绘回落。</item>
        /// </list>
        ///
        /// <para>⚠ 行为契约同 <see cref="TryStatTableLegacy"/>（不读 Mouse/Event；行越出 rect 底边即 break
        /// ⇒ 调用方按 <c>(1 + rows) * rowH</c> 预留）。<paramref name="aligns"/> 编码 <c>0 = 左 / 1 = 右</c>；
        /// <paramref name="dims"/> = 该列是否降调前景。</para>
        ///
        /// <para><b><paramref name="shares"/> 允许传任意比例</b>（Σ 不必为 1）：桥内部按
        /// <see cref="NormalizeShares"/> 归一化到占比后再交给 RUIF —— 因为 RUIF 的
        /// <c>ColumnSplit</c> 是**直接相乘**（见该方法注释里的实机根因）。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryStatTableEx(Rect rect, string[] headers, string[][] rows, float[] shares, float rowH,
            int[] aligns, bool[] dims, int bandCol, float bandRef, float[] bandValues, string[] bandTexts)
        {
            if (!Available || headers == null || headers.Length == 0 || rect.width < 24f)
            {
                return false;   // D2：RUIF 不可用 ⇒ 调用方自绘回落（硬依赖下的防御性守卫，非支持场景）
            }
            float[] effShares = NormalizeShares(shares);
            if (VersionAtLeast(StatTableColsVersion))
            {
                try
                {
                    TryStatTableV12(rect, headers, rows, effShares, rowH,
                        aligns, dims, bandCol, bandRef, bandValues, bandTexts);
                    return true;   // D0
                }
                catch (Exception ex)
                {
                    LogChartFailure("StatTableEx", ex);
                    return false;
                }
            }
            // D1：RUIF < 1.2 —— 5 参 StatTable（左对齐、无带色）。
            LogStatTableBelowVersionOnce();
            try
            {
                TryStatTableLegacy(rect, headers, rows, effShares, rowH);
                return true;
            }
            catch (Exception ex)
            {
                LogChartFailure("StatTable", ex);
                return false;
            }
        }

        /// <summary>
        /// **D0 实现**（RUIF ≥ 1.2 的 10 参 `StatTable`）—— **本方法体内是唯一允许出现
        /// `RUIF.StatCol` 的地方**。
        ///
        /// <para>安全性来自两点（ADR-AMD-1 §2 D-1）：① <c>[MethodImpl(NoInlining)]</c> 保证不被内联回
        /// 分派体；② 调用点被版本门控包住 ⇒ 仅在 RUIF ≥ 1.2 时才 JIT 本方法，此时
        /// <c>RUIF.StatCol</c> 必然可解析。</para>
        /// <para>把框架内类型翻译成 <c>RUIF.StatCol</c>：<paramref name="aligns"/>（0=左 / 1=右）、
        /// <paramref name="dims"/>；列缓冲复用 <c>statColBuf</c> ⇒ 绘制期零分配（PERF-001）。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TryStatTableV12(Rect rect, string[] headers, string[][] rows, float[] shares, float rowH,
            int[] aligns, bool[] dims, int bandCol, float bandRef, float[] bandValues, string[] bandTexts)
        {
            int n = headers.Length;
            RUIF.StatCol[] cols = statColBuf as RUIF.StatCol[];
            if (cols == null || cols.Length < n)
            {
                cols = new RUIF.StatCol[n];
                statColBuf = cols;
            }
            for (int i = 0; i < n; i++)
            {
                cols[i].Align = aligns != null && i < aligns.Length && aligns[i] == 1
                    ? TextAnchor.MiddleRight
                    : TextAnchor.MiddleLeft;
                cols[i].Dim = dims != null && i < dims.Length && dims[i];
            }
            RUIF.DataAtoms.StatTable(rect, headers, rows, shares, rowH,
                cols, bandCol, bandRef, bandValues, bandTexts);
        }

        /// <summary>“RUIF 版本低于带色列表要求”只记一次（避免每帧刷屏）。</summary>
        private static void LogStatTableBelowVersionOnce()
        {
            if (!ChronicleLog.Throttled("ruif_statTable_below_" + StatTableColsVersion, 600000))
            {
                return;
            }
            ChronicleLog.Error(ChronicleLog.Category.Ui,
                "RUIF < " + StatTableColsVersion + ": StatTable falls back to the 5-arg StatTable "
                + "(left-aligned, no threshold band). Upgrade RUIF to " + StatTableColsVersion + "+ for 1:1 rendering.");
        }

        /// <summary>图表绘制失败的节流日志（每图种每 60s 一条，避免每帧刷屏）。</summary>
        private static void LogChartFailure(string chart, Exception ex)
        {
            if (ChronicleLog.Throttled("ruif_chart_fail_" + chart, 60000))
            {
                ChronicleLog.Error(ChronicleLog.Category.Ui,
                    "RUIF " + chart + " failed (RUIF is a required dependency): " + ex);
            }
        }

        /// <summary>RUIF 版本字面量缓存（**只反射一次**；见 <see cref="CachedVersionRaw"/>）。</summary>
        private static string cachedVersionRaw;
        private static bool versionProbed;

        /// <summary>
        /// RUIF 版本是否 ≥ <paramref name="minVersion"/>（如 <c>"1.0.2"</c>）。
        ///
        /// <para><b>⚠ 反射只做一次</b>（2026-09-18 · GATE-PERF）：此前每次调用都走
        /// <c>Assembly.GetType</c> + <c>GetField</c> + <c>GetRawConstantValue</c> + 两次
        /// <c>Version.TryParse</c>；而 <c>StatTable</c> 的版本门控在**绘制路径**上
        /// （市场面板每帧一次）⇒ 每帧 1 次反射 + 2 个小对象，违反 PERF-001「热路径不做反射」。
        /// 版本串在进程生命周期内不会变（程序集不换），故首次读入后长期缓存。</para>
        /// </summary>
        internal static bool VersionAtLeast(string minVersion)
        {
            if (!Available)
            {
                return false;
            }
            string raw = CachedVersionRaw();
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }
            Version actual;
            Version min;
            return Version.TryParse(raw, out actual)
                && Version.TryParse(minVersion, out min)
                && actual >= min;
        }

        /// <summary>
        /// 版本字面量的**一次性**读取（结果缓存）。读取失败也置位 —— 失败重试放在
        /// <see cref="Available"/> 的节流探测里，不在热路径重复反射。
        /// </summary>
        private static string CachedVersionRaw()
        {
            if (!versionProbed)
            {
                versionProbed = true;
                cachedVersionRaw = ReadVersionRaw();
            }
            return cachedVersionRaw;
        }

        /// <summary>
        /// 唯一读取 RUIF 版本字面量的位置 —— 同 <see cref="ReadTokens"/>，**必须 NoInlining**
        /// （否则会被内联回调用方，把"引用 RUIF 类型"重新暴露给主路径）。
        ///
        /// <para>读的是 `RUIF.Api.VersionString`（<c>const</c> ⇒ 编译期常量）：
        /// `FieldInfo.GetRawConstantValue()` **不触发类型初始化**，因此即便 RUIF 未来引入可选依赖，
        /// 也不会在版本探测这一步被带崩。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ReadVersionRaw()
        {
            try
            {
                Assembly assembly = ruifAssembly;
                Type api = assembly != null ? assembly.GetType("RUIF.Api", throwOnError: false) : null;
                FieldInfo field = api != null ? api.GetField("VersionString") : null;
                return field != null ? field.GetRawConstantValue() as string : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// **RUIF 脉冲状态点**（P3：设备卡状态灯）。<paramref name="t"/> 为相位（秒，任意外延）。
        /// RUIF 缺失/异常 ⇒ false，调用方回落实心方块。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        /// <summary>
        /// **RUIF 数值步进器**（批次 5）：`InputAtoms.Stepper(rect, ref value, min, max, step)` → 是否变化。
        /// 已 Cecil 核实存在（`InputAtoms.Stepper`，宽 ≥ 60；左钮 26 + 中 Rest + 右钮绝对定位）。
        /// RUIF 缺失/异常 ⇒ false，调用方回落原版 `ButtonText` 加减。
        /// </summary>
        internal static bool TryStepper(Rect rect, ref int value, int min, int max, int step)
        {
            if (!Available)
            {
                return false;
            }
            try
            {
                return RUIF.InputAtoms.Stepper(rect, ref value, min, max, step);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryPulseDot(Rect rect, Color color, float t)
        {
            if (!Available)
            {
                return false;
            }
            try
            {
                RUIF.StatusAtoms.PulseDot(rect, color, t);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ═══════════ CHG-20261005-DEVRENAME-RUIF（2026-10-05 玩家要求「改名弹窗必须基于 RUIF」）═══════════
        //
        // 目标：把自制 IMGUI 弹窗换成 RUIF 的**窗口骨架 + 按钮原子**。
        //
        // ⚠ 为什么必须走桥（而不是像 `RUIF.Draw` 那样直用）：
        //   `WindowFrame` / `ButtonAtoms` 都是 **RUIF v1.3.0 新增面**（CHANGELOG「v1.3.0 · 互动组件统一化」
        //   明写这两个文件为**新建**），而本 Mod 的 `About.xml` 只声明 **≥1.2.0**
        //   ⇒ 直用会在 1.2.x 玩家机上抛类型加载异常。故沿 `GaugeFixedVersion` / `StatTableColsVersion`
        //   既有纪律：**版本常量 + `VersionAtLeast` 门控 + `NoInlining` 隔离 + 调用方回落（B1）**。
        //
        // ⚠ 另有一条会造成"编译不过"的历史坑：ADR-AMD-2 写的 `InputAtoms.Button(..., ButtonKind)`
        //   在 v1.3.0 **并不存在**，实装是 `ButtonAtoms.PrimaryButton(..., ButtonTone)` —— 本桥按**实装**写。

        /// <summary>窗口骨架与按钮原子所属的 RUIF 版本（v1.3.0 新建面）。</summary>
        internal const string ChromeAtomsVersion = "1.3";

        /// <summary>
        /// 窗口骨架 + 按钮原子这一"1.3.0 面"是否可用。构造期（算窗高）与绘制期各查一次即可，不必缓存。
        /// </summary>
        internal static bool ChromeAtomsReady
        {
            get { return Available && VersionAtLeast(ChromeAtomsVersion); }
        }

        /// <summary>
        /// **RUIF 窗口骨架**（`WindowFrame.Draw`，v1.3.0）：只画标题条与两条分隔线，交出内容区/底栏矩形。
        /// <para>⚠ 它**不画窗口底/外框**（框架红线：背景归宿主 `Verse.Window`）⇒ 调用方仍须自己铺底色。</para>
        /// <para>RUIF 缺失 / &lt;1.3 / 异常 ⇒ 返回 false 且两个 out 为默认值，调用方自绘标题并自行排版（B1 回落）。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryWindowFrame(Rect rect, string title, int footerButtons,
            out Rect contentRect, out Rect footerRect)
        {
            contentRect = default(Rect);
            footerRect = default(Rect);
            if (!ChromeAtomsReady)
            {
                return false;
            }
            try
            {
                return TryWindowFrameV13(rect, title, footerButtons, out contentRect, out footerRect);
            }
            catch
            {
                contentRect = default(Rect);
                footerRect = default(Rect);
                return false;
            }
        }

        /// <summary>唯一允许出现 <c>RUIF.WindowFrame</c> 类型的位置（v1.3.0）。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryWindowFrameV13(Rect rect, string title, int footerButtons,
            out Rect contentRect, out Rect footerRect)
        {
            RUIF.WindowFrame.Draw(rect, title, footerButtons, out contentRect, out footerRect);
            return true;
        }

        /// <summary>
        /// **RUIF 底栏按钮矩形**（`WindowFmt.FooterButtonRect`，v1.3.0；主按钮 = 末位 = 最右）。
        /// <para>与非 RUIF 路径共用同一套"两枚按钮对半分"语义：<paramref name="index"/> 0 = 取消、1 = 确定。</para>
        /// <para>不可用 ⇒ 返回 false 且 <paramref name="buttonRect"/> 为默认值。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryFooterButtonRect(Rect footerRect, int count, int index, out Rect buttonRect)
        {
            buttonRect = default(Rect);
            if (!ChromeAtomsReady)
            {
                return false;
            }
            try
            {
                buttonRect = TryFooterButtonRectV13(footerRect, count, index);
                return true;
            }
            catch
            {
                buttonRect = default(Rect);
                return false;
            }
        }

        /// <summary>唯一允许出现 <c>RUIF.WindowFmt</c> 类型的位置（v1.3.0）。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Rect TryFooterButtonRectV13(Rect footerRect, int count, int index)
        {
            return RUIF.WindowFmt.FooterButtonRect(footerRect, count, index);
        }

        /// <summary>
        /// 窗口骨架的几何（v1.3.0）。**全部为值类型** ⇒ 可安全跨桥，不把 RUIF 引用类型带进 PC 对象图
        /// （同 <see cref="TokenSet"/> 的既有纪律）。
        /// <para>用途：宿主窗口要在**构造期**算窗高（<c>InitialSize</c>），那一处不能靠绘制期的 out 参数。</para>
        /// </summary>
        internal struct ChromeMetric
        {
            /// <summary>骨架可用（title/contentInset/footer 三项有效）。</summary>
            public bool Ready;
            public float TitleHeight;
            public float ContentInset;
            public float FooterHeight;
        }

        /// <summary>读窗口骨架几何；不可用 ⇒ <c>Ready = false</c>，调用方用自己的常量。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ChromeMetric ChromeMetrics()
        {
            if (!ChromeAtomsReady)
            {
                return new ChromeMetric();
            }
            try
            {
                return ChromeMetricsV13();
            }
            catch
            {
                return new ChromeMetric();
            }
        }

        /// <summary>唯一允许出现 <c>RUIF.WindowFmt</c> 常量的位置（v1.3.0）。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ChromeMetric ChromeMetricsV13()
        {
            ChromeMetric m = new ChromeMetric();
            m.TitleHeight = RUIF.WindowFmt.TitleHeight;
            m.ContentInset = RUIF.WindowFmt.ContentInset;
            m.FooterHeight = RUIF.WindowFmt.FooterHeight;
            m.Ready = true;
            return m;
        }

        /// <summary>
        /// **RUIF 按钮原子**（`ButtonAtoms.PrimaryButton`，v1.3.0）→ 是否被点击。
        ///
        /// <para>⚠ <paramref name="tone"/> 用 <b>int 编码</b>（0 Default / 1 Accent / 2 Danger / 3 Warn）：
        /// `RUIF.ButtonTone` <b>不得</b>出现在本类的字段/形参/返回/局部 —— 声明级位置会在类型加载期解析，
        /// 早于门控返回 false（`RuifBridge` 顶部纪律 4），等于门控失效。</para>
        ///
        /// <para>RUIF 缺失 / &lt;1.3 / 异常 ⇒ 返回 false 且 <paramref name="handled"/> = false，
        /// 调用方回落本工程既有 `UIComponents.ThemedButton`（B1）。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryButton(Rect rect, string label, int tone, bool enabled, out bool handled)
        {
            handled = false;
            if (!ChromeAtomsReady)
            {
                return false;
            }
            try
            {
                handled = true;
                return TryButtonV13(rect, label, tone, enabled);
            }
            catch
            {
                handled = false;
                return false;
            }
        }

        /// <summary>唯一允许出现 <c>RUIF.ButtonAtoms</c> / <c>RUIF.ButtonTone</c> 类型的位置（v1.3.0）。
        /// <para>刻意**不引入 ButtonTone 局部变量** —— 每个分支直接返回，保持"类型只在调用表达式里出现"。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryButtonV13(Rect rect, string label, int tone, bool enabled)
        {
            switch (tone)
            {
                case 1:
                    return RUIF.ButtonAtoms.PrimaryButton(rect, label, enabled, RUIF.ButtonTone.Accent);
                case 2:
                    return RUIF.ButtonAtoms.PrimaryButton(rect, label, enabled, RUIF.ButtonTone.Danger);
                case 3:
                    return RUIF.ButtonAtoms.PrimaryButton(rect, label, enabled, RUIF.ButtonTone.Warn);
                default:
                    return RUIF.ButtonAtoms.PrimaryButton(rect, label, enabled, RUIF.ButtonTone.Default);
            }
        }

        /// <summary>
        /// **RUIF 闪烁描边**（P3：画布设备卡/芯片的闪烁定位）。<paramref name="t"/> 为相位（秒）。
        /// RUIF 缺失/异常 ⇒ false，调用方回落实心描边。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryFlashOverlay(Rect rect, float t, Color color, float thickness)
        {
            if (!Available)
            {
                return false;
            }
            try
            {
                RUIF.PanelAtoms.FlashOverlay(rect, t, color, thickness);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// **RUIF 轨迹亮度**（P5：时序条"越靠近最新越亮"）。纯函数，返回值经 <paramref name="alpha"/> 传出。
        /// RUIF 缺失/异常 ⇒ false，调用方回落固定亮度。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryTrailAlpha(int index, int head, int segments, out float alpha)
        {
            alpha = 0f;
            if (!Available)
            {
                return false;
            }
            try
            {
                alpha = RUIF.StatusFmt.TrailAlpha(index, head, segments);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// **RUIF 页签条**（P5/P7：弹窗 7 页签 / 底栏 7 页签）。宽度自适应 + 溢出折叠 + 底部指示条。
        /// ⚠ <paramref name="tabs"/> 必须是**缓存数组**（每帧 `ToArray()` 是托管分配，破 PERF-001）。
        /// RUIF 缺失/异常 ⇒ false，调用方回落本工程页签绘制。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryTabBar(Rect rect, string[] tabs, ref int sel, ref float indicatorX)
        {
            if (!Available || tabs == null || tabs.Length == 0)
            {
                return false;
            }
            try
            {
                RUIF.NavAtoms.TabBar(rect, tabs, ref sel, ref indicatorX);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>RUIF **键值行**（<c>PanelAtoms.StatRow</c>）：label + value + 可选占比条。</summary>
        /// <remarks>
        /// ⚠ 调用纪律：RUIF 该原子的 <c>labelRatio / gap / barW</c> 是**内部常量**、不可配
        /// ⇒ 本桥**只用于「无头像、无来源徽标」的简单行**。需要叠加装饰的行必须走
        /// <c>UiAtoms.StatRow</c> 的手绘分支，否则装饰会与 RUIF 的内部布局打架。
        /// </remarks>
        /// <param name="bar01">占比 0..1；<c>null</c> = 不画条。</param>
        /// <param name="barColor">条色；<c>null</c> = 用 RUIF 令牌默认色。</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryStatRow(Rect rect, string label, string value,
            float? bar01, Color? barColor)
        {
            if (!Available)
            {
                return false;
            }
            try
            {
                RUIF.PanelAtoms.StatRow(rect, label ?? string.Empty, value ?? string.Empty,
                    bar01, barColor);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>RUIF **分区头**（<c>PanelAtoms.SectionBar</c>）：标题 + 计数 + 折叠态。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TrySectionBar(Rect rect, string title, string count, bool expanded)
        {
            if (!Available)
            {
                return false;
            }
            try
            {
                RUIF.PanelAtoms.SectionBar(rect, title ?? string.Empty, count ?? string.Empty, expanded);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>RUIF **内嵌面板**（<c>Shapes.InsetPanel</c>）：统一卡片/分区底板。</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool TryInsetPanel(Rect rect)
        {
            if (!Available)
            {
                return false;
            }
            try
            {
                RUIF.Shapes.InsetPanel(rect);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>取 RUIF 令牌；RUIF 缺失或读取异常 ⇒ 返回 false，调用方**回落**本工程调色板。</summary>
        internal static bool TryGetTokens(out TokenSet set)
        {
            set = default;
            if (!Available)
            {
                return false;
            }
            return ReadTokens(out set);
        }

        /// <summary>
        /// 真正触碰 <c>RUIF.Tokens</c> 的唯一位置 —— <b>必须 NoInlining</b>，
        /// 否则 JIT 会把它内联进 <see cref="TryGetTokens"/>，让"引用 RUIF 类型"这件事重新暴露给主路径。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ReadTokens(out TokenSet set)
        {
            set = default;
            try
            {
                set.Window = RUIF.Tokens.Window;
                set.Panel = RUIF.Tokens.Panel;
                set.PanelRaised = RUIF.Tokens.PanelRaised;
                set.Card = RUIF.Tokens.Card;
                set.CardHover = RUIF.Tokens.CardHover;
                set.Border = RUIF.Tokens.Border;
                set.BorderSoft = RUIF.Tokens.BorderSoft;
                set.BorderHair = RUIF.Tokens.BorderHair;
                set.Text = RUIF.Tokens.Text;
                set.Muted = RUIF.Tokens.Muted;
                set.Dim = RUIF.Tokens.Dim;
                set.Accent = RUIF.Tokens.Accent;
                set.Info = RUIF.Tokens.Info;
                set.Alive = RUIF.Tokens.Alive;
                set.Dead = RUIF.Tokens.Dead;
                set.Warn = RUIF.Tokens.Warn;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
