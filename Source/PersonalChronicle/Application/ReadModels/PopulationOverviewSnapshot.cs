using System.Collections.Generic;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// 能力均值明细行（原版 <c>PawnCapacityDef</c>，活读、不入档）—— 预览 ① 面板的「11 项能力条」。
    /// </summary>
    public sealed class PopulationCapacityRow
    {
        /// <summary>能力名（构建期已本地化）。</summary>
        public string Label = string.Empty;

        /// <summary>殖民地均值 0..1+（原版 <c>GetLevel</c> 可 &gt; 1，如实保留不 clamp）。</summary>
        public float Level01;

        /// <summary>展示文本（构建期已格式化，如 "93.7%"）。</summary>
        public string LevelText = string.Empty;
    }

    /// <summary>个人绩效排行行（预览 ⑥ 的净贡献排行：条宽按相对最大值归一）。</summary>
    public sealed class PopulationRankRow
    {
        /// <summary>姓名（档案显示名）。</summary>
        public string Label = string.Empty;

        /// <summary>产出市值（构建期已格式化，如 "6,980"）。</summary>
        public string ValueText = string.Empty;

        /// <summary>附注（工时等，构建期已格式化）。</summary>
        public string NoteText = string.Empty;

        /// <summary>相对**榜首**的占比（0..1；UI 只按它画条）。</summary>
        public float Share01;

        /// <summary>
        /// 占**全体**的占比文本（构建期已格式化，如 "31.9%"）—— 详细列表的「占比」列用。
        /// ⚠ 与 <see cref="Share01"/> 口径不同：后者是相对榜首归一（只用于条宽）。
        /// </summary>
        public string ShareText = string.Empty;

        // ── 绩效详细列表的 8 列扩展（v6 预览形态 · 2026-09-15 落地）──
        // 三个量全部由 `PerfCandidate.QuadrumValue`（8 季滚动桶）**直接推导** ⇒ 零新增采集。

        /// <summary>平均季产（累计产出市值 ÷ **有产出的季数**；无产出季不计入分母）。</summary>
        public string AvgQuadrumText = string.Empty;

        /// <summary>峰值季（8 季桶中的最高单季产出市值）。</summary>
        public string PeakQuadrumText = string.Empty;

        /// <summary>参与季数（8 季桶中**非零**的季数）。</summary>
        public string QuadrumCountText = string.Empty;

        // ── 逐列**数值**（2026-09-17 新增：绩效表表头点击排序）────────────────────────
        // 背景：此前本行只有**已格式化文本** + `Share01`（相对榜首）⇒ UI 无法做数值排序
        //   （按文本解析 "1.1K" / "779.0 时" / "6 / 8 季" 属脆弱胶水，明确不做）。
        // 契约：① 与同名 `*Text` 字段**同源同刻**填充（构建期一次算完，UI 只读）；
        //   ② 本快照**不持久化**（GOV-003 活读/派生）⇒ 新增字段**无需 Migration**；
        //   ③ UI 只用它们做**排序键**，绝不自行格式化（格式化仍走 `*Text`）。

        /// <summary>产出市值（原始值；`ValueText` 的数值形式）。</summary>
        public float Value;

        /// <summary>工时（原始值；`NoteText` 的数值形式）。</summary>
        public float Hours;

        /// <summary>平均季产（原始值；`AvgQuadrumText` 的数值形式）。</summary>
        public float AvgQuadrum;

        /// <summary>峰值季（原始值；`PeakQuadrumText` 的数值形式）。</summary>
        public float PeakQuadrum;

        /// <summary>参与季数（原始值；`QuadrumCountText` 的数值形式）。</summary>
        public float QuadrumCount;
    }

    /// <summary>派系外交行（预览 ③：派系名 + 关系 + 好感度）。</summary>
    public sealed class PopulationFactionRow
    {
        /// <summary>派系名（原版 <c>Faction.Name</c>，玩家自建的显示为「本殖民地」）。</summary>
        public string Label = string.Empty;

        /// <summary>关系文案（敌对 / 中立 / 盟友，构建期已本地化）。</summary>
        public string RelationText = string.Empty;

        /// <summary>好感度（-100..100，构建期已格式化带符号）。</summary>
        public string GoodwillText = string.Empty;

        /// <summary>语气（UI 只做语气 → 颜色映射）。</summary>
        public SystemCenterTone Tone = SystemCenterTone.Flat;

        /// <summary>好感度在 -100..100 上的位置（0..1）；条只按它画。</summary>
        public float Goodwill01 = 0.5f;

        /// <summary>科技等级（原版 <c>FactionDef.techLevel</c> 枚举名；玩家派系为空串）。</summary>
        public string TechText = string.Empty;
    }

    /// <summary>工种工时聚合行（殖民地级，不是某个人的维度）。</summary>
    public sealed class PopulationLaborRow
    {
        public string Label = string.Empty;
        public string HoursText = string.Empty;

        /// <summary>
        /// 柱顶**短数值**（构建期格式化：≥1000 走「2.4k」，与预览 `.barv` 的柱顶标注同形）。
        /// 柱宽不足时 UI 不画它（LAND-004 降级），但数值本身始终不算在绘制期。
        /// </summary>
        public string ShortText = string.Empty;

        /// <summary>占全殖民地的占比文本（构建期已格式化，如 "24.3%"）—— 详细列表的「占比」列用。</summary>
        public string ShareText = string.Empty;

        public float Share01;
    }

    /// <summary>计数分布行（勋章档位 / 职称 / 主方向共用）。</summary>
    public sealed class PopulationCountRow
    {
        public string Label = string.Empty;
        public int Count;

        /// <summary>占比文本（构建期已格式化，如 "66.7%"）。</summary>
        public string ShareText = string.Empty;

        /// <summary>占比 0..1（UI 只按它画条，不重算）。</summary>
        public float Share01;

        /// <summary>
        /// 拼好的展示值（如 "3 · 33.3%"）—— 用 Keyed 模板在**构建期**拼，
        /// 绘制期零拼接（书写顺序交给语言文件，不在代码里写死分隔符）。
        /// </summary>
        public string ValueText = string.Empty;
    }

    /// <summary>
    /// 人员系统中心「概览」快照（预览 #v-overview 的殖民地级聚合层）。
    ///
    /// 与所有系统中心快照同规（08c DL 契约）：字段全部预本地化、预格式化，
    /// 绘制路径零翻译、零聚合、零中心名判断。
    ///
    /// 诚实性（LAND-009）：没有数据源的派生指标（预览「派生属性 · 本 Mod」面板的人均产出率 /
    /// 单点依赖 / 情绪风险人等）一律不进入快照，由 UI 画占位说明，绝不拿样例数值填充。
    /// 本快照不入存档：全部字段活读或由已持久化数据派生（GOV-003）。
    /// </summary>
    public sealed class PopulationOverviewSnapshot : ArchiveSectionSnapshot
    {
        // ---- 规模与构成 ----
        public int LiveColonists;
        public int FreeColonists;
        public int SlaveColonists;
        public int PrisonerColonists;
        public int MaleColonists;
        public int FemaleColonists;

        /// <summary>
        /// **身份 × 性别 交叉计数**（预览 ① col2：性别金字塔 / 性比 / 分身份明细表，HTML 670-691）。
        /// index：0 = 自由民 · 1 = 奴隶 · 2 = 囚犯（顺序 = <see cref="IdentityOrder"/>，程序逻辑只按下标，GOV-009）。
        /// 与 <see cref="MaleColonists"/> / <see cref="FemaleColonists"/> 同源同循环累加（不新增遍历）。
        /// </summary>
        public int[] IdentityMale = new int[3];
        public int[] IdentityFemale = new int[3];

        /// <summary>
        /// 工效 = 意识均值 × 操作均值（预览 ① col3「工效 = 意识 × 操作」，HTML 703）。
        /// &lt; 0 = 无样本（UI 画「—」，不画 0 假值）。
        /// </summary>
        public float WorkEfficiency01 = -1f;
        public int ActiveRecords;
        public int ArchivedRecords;
        public int ServiceDays;

        /// <summary>事件总数（<c>GetEventCount()</c> O(1)；预览统计带第 4 张卡）。</summary>
        public int EventTotal;

        // 注：档案时段分布**不属于本快照** —— 它是 Archive 视图自己的数据（承载区统计带），
        // 由 Archive 层按自己的 revision 缓存自算，避免跨模块取数（ARC-005）。

        /// <summary>身份占比（构建期已 clamp 到 0..1；分母 = 现役殖民者数）。
        /// UI 只按它画条，不重算 —— 阈值与分母口径集中在 Provider（08c DL 契约）。</summary>
        public float FreeShare01;
        public float SlaveShare01;
        public float PrisonerShare01;

        /// <summary>
        /// 性别占比（分母**统一 = 现役殖民者数**，与身份占比同分母）。
        /// 若改用「男+女」做分母，无档案 / 性别未登记的成员会让同面板的百分比互相矛盾。
        /// </summary>
        public float MaleShare01;
        public float FemaleShare01;

        /// <summary>
        /// 技能梯队 4 档（按每人**最高专业技能等级**归档：专家 / 熟练 / 入门 / 学徒）——
        /// 预览 ⑧ 专业分布面板的梯队条。顺序固定为「高→低」，**不按人数排序**（档位是有序量）。
        /// </summary>
        public List<PopulationCountRow> SkillTiers = new List<PopulationCountRow>();

        /// <summary>参与梯队统计的人数（= 四档之和；0 时 UI 画空而不是四条 0）。</summary>
        public int SkillTierTotal;

        /// <summary>专家档人数（≥15 级）。</summary>
        public int SkillExpert;

        /// <summary>熟练档人数（10–14 级）。</summary>
        public int SkillSkilled;

        /// <summary>入门档人数（5–9 级）。</summary>
        public int SkillNovice;

        /// <summary>学徒档人数（&lt; 5 级，含仅有技能记录未升级者）。</summary>
        public int SkillApprentice;

        /// <summary>综合能力均值（11 项原版 capacity 等权）；小于 0 = 无活体可读，UI 画「—」。</summary>
        public float CapacityAverage01 = -1f;

        public string CapacityAverageText = string.Empty;

        /// <summary>能力明细（殖民地均值，按原版 Def 顺序）—— 概览 ① 面板的能力条列表。</summary>
        public List<PopulationCapacityRow> Capacities = new List<PopulationCapacityRow>();

        // ---- 劳动力 ----
        public List<PopulationLaborRow> Labor = new List<PopulationLaborRow>();
        public string LaborTotalText = string.Empty;
        public string LaborPerCapitaText = string.Empty;
        public string LaborTopThreeText = string.Empty;

        /// <summary>
        /// 人均工时的分母 = 有工时记录的现役成员数（与既有 ArchiveService.Work.Aggregate
        /// 的「排除未干活者」同口径）；0 表示无任何工时记录，UI 画空而非 0.0。
        /// </summary>
        public int LaborContributors;

        // ---- 人员绩效（预览 ⑥ 的排行 + 累计贡献趋势）----
        public List<PopulationRankRow> Performance = new List<PopulationRankRow>();

        /// <summary>全殖民地产出市值合计（构建期已格式化）。</summary>
        public string PerformanceTotalText = string.Empty;

        /// <summary>人均产出市值（分母 = 有产出的成员数，与排行同口径；构建期已格式化）。</summary>
        public string PerformancePerCapitaText = string.Empty;

        /// <summary>前 3 名产出市值占合计的比（构建期已格式化，如 "61.2%"）。</summary>
        public string PerformanceTop3ShareText = string.Empty;

        /// <summary>
        /// **累计贡献趋势**的三系列名（索引 = 系列序，与 <see cref="PerformanceTrendSeries"/> 同序；
        /// 长度 0 = 无趋势数据，UI 画空态说明）。预览 ⑥，HTML 946-948。
        /// </summary>
        public string[] PerformanceTrendNames = new string[0];

        /// <summary>
        /// Top N 每人**近 8 季累计产出市值**（<c>float[系列][8]</c>，段 0 = 最老、段 7 = 当前季）。
        /// 原值来自 <c>ProductionAccumulator.MarketValueByQuadrum</c> 的逐季累加（真实数据，非采样）。
        /// </summary>
        public float[][] PerformanceTrendSeries = new float[0][];

        /// <summary>趋势卡 y 上界展示文本（构建期格式化，如 "2.4K"）。</summary>
        public string PerformanceTrendCapText = string.Empty;

        /// <summary>x 轴季度标签（4 个，构建期已按语言格式化，如 "5499 秋"）。</summary>
        public string[] PerformanceTrendAxisLabels = new string[0];

        /// <summary>
        /// 档案活跃度趋势（近 8 季事件数，段 0 = 最老）。**为什么不是「产出/工时累计」**：
        /// 预览那条曲线需要历史采样 ⇒ Save 变更；本工程已有的事件索引天然带 tick，
        /// 故以**真实事件时间序列**表达趋势（同为时间序列，口径写进标签；缺口如实登记）。
        /// </summary>
        public int[] ActivityTrend = new int[8];

        /// <summary>活跃度柱的最大值（0 = 无数据，UI 不画柱）。</summary>
        public int ActivityTrendMax;

        /// <summary>
        /// **入档趋势**（近 8 季，段 0 = 最老）：每季 <c>Join</c> 事件的**累计**值（真实数据，零存档变更）。
        ///
        /// <para>口径说明（LAND-009）：预览 ① col1 的「身份人数趋势」是**按身份的存量**三系列；工程
        /// 事件模型无「离档 / 身份变更」语义（已核实 <c>ChronicleEventKind</c>）⇒ 三系列不可回推。
        /// 故以「**累计入档**」单线如实呈现，并在 UI 标题写明口径，**不冒充**分身份存量。</para>
        /// </summary>
        public int[] JoinTrend = new int[8];

        /// <summary>入档趋势最大值（0 = 无数据，UI 画空态说明）。</summary>
        public int JoinTrendMax;

        /// <summary>
        /// **分身份存量趋势**（按季回推，段 0 = 最老）：自由 / 奴隶 / 囚犯的每季末在册人数。
        /// 由带身份事件（Join/Leave/RoleChanged）逆向回推 —— 真实数据；**仅从本机制上线起积累**。
        /// </summary>
        /// <summary>⚠ 用 float 存（人数为整数，但 RUIF ``LineChart`` 多系列收 ``float[]``）。</summary>
        public float[] IdentityTrendFree = new float[8];
        public float[] IdentityTrendSlave = new float[8];
        public float[] IdentityTrendPrisoner = new float[8];

        /// <summary>分身份趋势是否有数据（false ⇒ UI 画空态说明，不画 0 假象）。</summary>
        public bool IdentityTrendReady;

        /// <summary>
        /// 分身份趋势卡的**量程文本**（构建期格式化，如「0–9 人」）—— 预览 ① col1 趋势卡的
        /// `#classTrendRange`。空 = 无数据（UI 不画量程，不画 0–0）。
        /// </summary>
        public string IdentityTrendRangeText = string.Empty;

        /// <summary>
        /// 分身份趋势的**逐段季度标签**（**8 个**，段 0 = 最老、段 7 = 当前季，构建期已按语言
        /// 格式化，如「5500 秋」）。UI 取首 / 中 / 末三个画 x 轴（预览 ① col1 的
        /// `#classTrendAxis`），并用同一数组拼悬停气泡（避免绘制期翻译）。长度 0 = 无数据。
        /// </summary>
        public string[] IdentityTrendBucketLabels = new string[0];

        // ---- 派系与外交（预览 ③）----
        public List<PopulationFactionRow> Factions = new List<PopulationFactionRow>();

        /// <summary>
        /// 已知派系的**科技层级分布**（构建期拼好，如「工业 3 · 新石器 1」；空 = 无数据）。
        /// 预览 ③ 底部 drow（「科技层级 / 文化 CultureDef」）的真实等价 —— 本工程无 CultureDef
        /// 读取面 ⇒ 只汇总可观测的科技等级，文化部分按缺口登记（不编造）。
        /// </summary>
        public string FactionTechSummaryText = string.Empty;

        // ---- ④ 派生指标（预览 ④）----
        /// <summary>派生指标行（口径**真实可算**的那部分；每一项都能在代码里复算）。</summary>
        public List<PopulationCountRow> Derived = new List<PopulationCountRow>();

        /// <summary>true = 有活体可算（false 时面板画无活体说明，不画 0 假值）。</summary>
        public bool DerivedReady;

        // ---- ② 戒律榜 / ⑦ 勋章榜 ----
        /// <summary>戒律榜（全体有理念者的 Precept 聚合，按人数降序前 5）。</summary>
        public List<PopulationCountRow> Precepts = new List<PopulationCountRow>();

        /// <summary>勋章榜（全体档案的已授予勋章，按枚数降序前 5）。</summary>
        public List<PopulationCountRow> MedalTop = new List<PopulationCountRow>();

        /// <summary>
        /// 采集期**失败的面板数**（面板级失败隔离的计数，0 = 全部成功）。
        /// UI 在概览区顶部如实提示「N 项采集失败（详见日志）」—— 不静默、不假装完整（LAND-009）。
        /// </summary>
        public int FailedSections;

        // ---- 信仰与理念（预览 ②；需 Ideology 资料片，未启用时如实提示）----
        /// <summary>false = 未启用 Ideology 资料片（面板画「未启用」提示，不画空列表）。</summary>
        public bool IdeologyEnabled;

        /// <summary>理念分布（每行 = 一个理念：名称 + 成员数 + 占比）。</summary>
        public List<PopulationCountRow> Ideologies = new List<PopulationCountRow>();

        /// <summary>无信仰成员数（未选理念者；0 时 UI 不显示该行）。</summary>
        public int IdeologyNoneCount;

        /// <summary>
        /// **全人口数**（有理念者 + 无信仰）= 信仰环的环心与分母（预览 ② 环心 12 同口径：
        /// 环画的是「全人口信仰结构」，含无信仰段，而不是「有理念者的理念分布」）。
        /// </summary>
        public int IdeologyPopulationTotal;

        /// <summary>主要理念价值（取人数最多理念的 Meme 名称，前 3 个；构建期已拼好）。空 = 无数据。</summary>
        public string IdeologyMemeText = string.Empty;

        /// <summary>
        /// 核心理念（MemeDef）**逐项**名称（前 3 个）—— 供「核心理念」chips 一行绘制。
        /// ⚠ 与 <see cref="IdeologyMemeText"/> **同源同序**（后者是它的「 · 」拼接版）；
        /// 之所以额外存一份列表，是为了**绘制期零分配**（PERF-001：不在绘制路径里 split 字符串）。
        /// </summary>
        public List<string> IdeologyMemeChips = new List<string>();

        // ---- 荣誉 ----
        public int MedalAwarded;
        public int MedalGold;
        public int MedalSilver;
        public int MedalBronze;
        public int MedalDefTotal;
        public List<PopulationCountRow> Titles = new List<PopulationCountRow>();

        // ---- 专业分布 ----
        public int SpecializationSet;
        public int SpecializationTotal;
        public List<PopulationCountRow> Directions = new List<PopulationCountRow>();

        /// <summary>未设定主方向的人数（构建期格式化；预览 ⑧ chips 末尾的「未设定 N」）。</summary>
        public string SpecializationUnsetText = string.Empty;

        /// <summary>
        /// **无人主攻的方向**（构建期拼好，如「采矿 / 烹饪」；空 = 无缺口或无方向 Def）。
        /// 预览 ⑧ 底部 drow「方向缺口」—— 由「全部方向 Def − 已用方向」真实作差得出。
        /// </summary>
        public string SpecializationGapText = string.Empty;

    }
}
