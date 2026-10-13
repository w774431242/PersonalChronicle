using PersonalChronicle.MasterControl.Population;
using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Api;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Honor;
using PersonalChronicle.Domain.Profession;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Application.SystemCenters.Builtin
{
    /// <summary>
    /// Partial of <see cref="BuiltinPopulationProvider"/> ——人员系统中心「概览」采集
    /// （预览 <c>#v-overview</c> 的殖民地级聚合层）。
    ///
    /// <para><b>为什么在这里采而不是在 UI 里扫</b>：概览需要的全是「跨人聚合」（工种工时合计、
    /// 能力均值、勋章/职称/主方向分布），档案馆既有实现只有「个人维度 + 4 个计数」。
    /// 聚合口径放进 Provider 才能保证总控概览与档案馆个人页永远同一套数字（08c DL 契约）。</para>
    ///
    /// <para><b>性能（GATE-PERF）</b>：本方法由总控中心的 300 tick 节流门控调用（不是每帧）。
    /// 逐人遍历是 O(人数)，名册与分布列表都有上限常量，不在热路径上无界增长。
    /// 刻意<b>不</b>用 <c>GetAllEvents()</c> 去做「事件总数」：那是全量复制，热路径禁用。</para>
    ///
    /// <para><b>诚实性（LAND-009）</b>：预览「派生属性 · 本 Mod」的 12 项殖民地级指标在本工程
    /// 没有任何聚合口径（<c>OvF.Group.ModSelf</c> 只是 per-pawn 字段分组标签）⇒ 一律不产出，
    /// 由 UI 画占位说明。</para>
    /// </summary>
    public sealed partial class BuiltinPopulationProvider : IPopulationOverviewProvider
    {
        /// <summary>名册展示上限（超出只报总数，不做无界列表）。</summary>


        /// <summary>
        /// 工种工时展示上限 = **12**（预览 `.barv` 正好 12 根柱；原版 WorkTypeDef 主类工种数与此同阶）。
        /// 上限只截断**输出**，采集本身按实际工种数（字典键）——两者不是一回事，别混。
        /// </summary>
        private const int LaborMaxRows = 12;

        /// <summary>职称 / 主方向分布列表展示上限。</summary>
        private const int DistributionMaxRows = 8;

        /// <summary>人员绩效排行展示条数（预览 ⑥ 排行 5–8 条量级）。</summary>
        private const int RankMaxRows = 8;

        /// <summary>派系外交列表展示上限（含本殖民地；超出只计入总数不动列表）。</summary>
        private const int FactionMaxRows = 8;

        // 技能梯队阈值（口径唯一来源；与预览 ⑧「专家≥15 / 熟练10–14 / 入门5–9 / 学徒<5」逐字一致）
        private const int SkillExpertLevel = 15;
        private const int SkillSkilledLevel = 10;
        private const int SkillNoviceLevel = 5;

        /// <summary>
        /// ticks → 小时 / 天：唯一来源 = 原版 <see cref="GenDate"/>。
        /// ⚠ 禁止硬编码 2500 / 60000（工程铁律，见 <c>PersonnelArchivePanel.PawnOverview</c> 同类注释）——
        /// 数值相同，但写死常量迟早会有人改出第二套换算。
        /// </summary>
        private static double TicksPerHour
        {
            get { return GenDate.TicksPerHour; }
        }

        private static long TicksPerDay
        {
            get { return GenDate.TicksPerDay; }
        }

        public PopulationOverviewSnapshot BuildOverview()
        {
            PopulationOverviewSnapshot snapshot = new PopulationOverviewSnapshot();
            if (queries == null)
            {
                snapshot.IsEmpty = true;
                return snapshot;
            }
            try
            {
                snapshot.ActiveRecords = queries.GetActiveSnapshotCount();
                snapshot.ArchivedRecords = queries.GetArchivedSnapshotCount();
                snapshot.ServiceDays = queries.GetServiceDays();
                // 事件总数：走 O(1) 计数（GetEventCount），**不用** GetAllEvents().Count（O(n) 复制）。
                snapshot.EventTotal = queries.GetEventCount();
                snapshot.LiveColonists = queries.GetLiveColonistCount();
                int free;
                int slave;
                int prisoner;
                queries.GetLiveColonistCounts(out free, out slave, out prisoner);
                snapshot.FreeColonists = free;
                snapshot.SlaveColonists = slave;
                snapshot.PrisonerColonists = prisoner;
                snapshot.MedalDefTotal = DefDatabase<MedalDef>.DefCount;

                Dictionary<string, long> labor = new Dictionary<string, long>(StringComparer.Ordinal);
                Dictionary<string, int> titles = new Dictionary<string, int>(StringComparer.Ordinal);
                Dictionary<string, int> directions = new Dictionary<string, int>(StringComparer.Ordinal);

                // ⚠ 名册采集已停用（CHG-002）：名册面板已由承载区的 12 列人物表取代 ——
                //    逐人构造行（9 个字符串 + 事件查询 + Def 遍历）是纯浪费，故不再调用 FillRoster。
                List<PerfCandidate> performers = new List<PerfCandidate>();
                Dictionary<string, int> ideologies = new Dictionary<string, int>(StringComparer.Ordinal);
                // 面板级失败隔离：单个 Fill* 抛异常只丢该面板（计一次失败并记日志），
                // 其余面板照常产出 —— 旧实现是**整体** try/catch，一个派系读取失败就让
                // 整个概览退回空快照（实机现象：概览 8 面板全不显示，只剩承载区）。
                SafeSection(snapshot, "collect",
                    () => Collect(labor, titles, directions, performers, ideologies, snapshot));
                SafeSection(snapshot, "labor", () => FillLabor(snapshot, labor));
                SafeSection(snapshot, "distributions",
                    () => FillDistributions(snapshot, titles, directions));
                SafeSection(snapshot, "performance", () => FillPerformance(snapshot, performers));
                SafeSection(snapshot, "factions", () => FillFactions(snapshot));
                SafeSection(snapshot, "ideology", () => FillIdeology(snapshot, ideologies));
                SafeSection(snapshot, "activity", () => FillActivityTrend(snapshot));
                // 入档趋势（预览 ① col1 趋势卡的可算等价，详见 FillJoinTrend 注释）。
                SafeSection(snapshot, "joinTrend", () => FillJoinTrend(snapshot));
                // 分身份存量趋势（回推，三系列）。依赖 Leave/RoleChanged 事件已采集。
                SafeSection(snapshot, "identityTrends", () => FillIdentityTrends(snapshot));
                // 非产出/工时累计的主序列（活人物与活体列表）：放在 activity 之后，
                // 与 Archive 缓存同节奏（300 ticks），任一子项失败只影响自己。
                SafeSection(snapshot, "derived", () => FillDerived(snapshot));
                SafeSection(snapshot, "precepts", () => FillPrecepts(snapshot));
                SafeSection(snapshot, "medals", () => FillMedalTop(snapshot));
                // 注：档案时段分布**不在本 Provider 计算** —— 它属于 Archive 视图自己的数据
                // （承载区统计带），由 Archive 层自算，避免跨模块读总控快照（ARC-005）。

                snapshot.BuiltFromRevision = queries.GetDataRevision();
                // 无现役成员且无任何可展示数据 ⇒ 空态（UI 不画，不显摆一屏 0）。
                snapshot.IsEmpty = snapshot.LiveColonists == 0 && snapshot.MedalAwarded == 0
                    && snapshot.Labor.Count == 0;
            }
            catch (Exception ex)
            {
                // 失败隔离（08c DL 契约）：异常不进绘制循环，且**不留下半截快照** ——
                // 只置 IsEmpty 会把已填的计数/列表一起交出去，UI 若将来漏判 IsEmpty 就会画出半截数据。
                if (ChronicleLog.Throttled("population_overview_fail", 60000))
                {
                    ChronicleLog.Error(ChronicleLog.Category.Provider, "population overview failed: " + ex);
                }
                snapshot = new PopulationOverviewSnapshot();
                snapshot.IsEmpty = true;
            }
            return snapshot;
        }

        /// <summary>
        /// 面板级失败隔离：单个 <c>Fill*</c> 抛异常只丢该面板并计一次失败（其它面板照常产出），
        /// 由 UI 如实提示「N 项采集失败（详见日志）」。**不要**把整个概览包成一个 try/catch ——
        /// 那样任一子项出错都会让用户看到「概览整块消失」（2026-09-14 实机事故）。
        /// </summary>
        private static void SafeSection(PopulationOverviewSnapshot snapshot, string section,
            System.Action fill)
        {
            try
            {
                fill();
            }
            catch (System.Exception ex)
            {
                snapshot.FailedSections++;
                if (ChronicleLog.Throttled("pop_overview_section_fail_" + section, 60000))
                {
                    ChronicleLog.Error(ChronicleLog.Category.Provider,
                        "population overview section failed: " + section + " :: " + ex);
                }
            }
        }


        /// <summary>逐人遍历：工时 / 勋章 / 职称 / 主方向 / 性别 / 能力均值 一次扫完。</summary>
        /// <summary>绩效候选（轻量排序键：姓名 + 产出市值 + 工时；不在采集期拼展示文本）。</summary>
        private struct PerfCandidate
        {
            public string Name;
            public float Value;
            public long WorkTicks;

            /// <summary>近 8 季产出市值原始值（段 0 = 最老；null = 无滚动桶）。</summary>
            public float[] QuadrumValue;
        }

        private void Collect(Dictionary<string, long> labor,
            Dictionary<string, int> titles, Dictionary<string, int> directions,
            List<PerfCandidate> performers, Dictionary<string, int> ideologies,
            PopulationOverviewSnapshot snapshot)
        {
            labor.Clear();
            performers.Clear();
            ideologies.Clear();
            titles.Clear();
            directions.Clear();

            IReadOnlyList<ArchiveObject> objects = queries.GetObjectsOfCategory(ArchiveCategoryKeys.Pawn);
            if (objects == null)
            {
                return;
            }

            List<PawnCapacityDef> capacityDefs = DefDatabase<PawnCapacityDef>.AllDefsListForReading;
            double[] capacitySums = capacityDefs != null ? new double[capacityDefs.Count] : new double[0];
            int capacitySamples = 0;

            for (int i = 0; i < objects.Count; i++)
            {
                PawnObject pawn = objects[i] as PawnObject;
                if (pawn == null)
                {
                    continue;
                }
                bool enlisted = queries.IsCurrentlyEnlisted(pawn.StableId);
                bool hasWork = AddLaborTicks(pawn, labor);
                CountMedals(pawn, snapshot);
                CountTitles(pawn, titles);

                Pawn live = enlisted ? queries.GetLivePawn(pawn.StableId) : null;
                if (live != null)
                {
                    // 身份 × 性别 交叉计数（预览 ① col2：金字塔 / 性比 / 分身份明细表，HTML 670-691）。
                    // 与性别总数**同循环同源**（不新增遍历）；身份只取三态稳定判定。
                    int identityIndex = live.IsPrisonerOfColony ? 2 : (live.IsSlaveOfColony ? 1 : 0);
                    if (live.gender == Gender.Male)
                    {
                        snapshot.MaleColonists++;
                        snapshot.IdentityMale[identityIndex]++;
                    }
                    else if (live.gender == Gender.Female)
                    {
                        snapshot.FemaleColonists++;
                        snapshot.IdentityFemale[identityIndex]++;
                    }
                    AccumulateCapacities(live, capacityDefs, capacitySums, ref capacitySamples);
                    CountDirection(pawn, live, directions);
                    CountSkillTier(pawn, snapshot);
                    snapshot.SpecializationTotal++;
                    if (HasPrimaryDirection(pawn))
                    {
                        snapshot.SpecializationSet++;
                    }
                    if (hasWork)
                    {
                        // 人均工时分母 = **有工时记录的现役成员**（排除没上过工的），
                        // 与既有 ArchiveService.Work.Aggregate 的「排除未干活者」同口径。
                        snapshot.LaborContributors++;
                    }
                    // 信仰与理念（预览 ②）：经 **Compatibility/Dlc/IdeologyAdapter** 活读。
                    // ⚠ 2026-09-15（审计 COMP-001 / COMP-006 修复）：DLC 类型 `Ideo`/`MemeDef`
                    // **不再出现在本层** —— 适配器只回传值类型（理念名 / Meme 摘要 + 逐项 chips），
                    // 且自带 `ModsConfig.IdeologyActive` 能力探测与 null 保护。
                    string ideoName = Compatibility.Dlc.IdeologyAdapter.IdeologyNameOf(live);
                    if (ideoName != null)
                    {
                        int seen;
                        ideologies.TryGetValue(ideoName, out seen);
                        ideologies[ideoName] = seen + 1;
                        // 理念价值（预览 v11「核心理念」chips）：取首个有理念者的 Meme 名，前 3 个。
                        // 只作**摘要**展示 —— 完整 Meme/Precept 明细需更深读取面，缺口登记。
                        if (string.IsNullOrEmpty(snapshot.IdeologyMemeText))
                        {
                            snapshot.IdeologyMemeText = Compatibility.Dlc.IdeologyAdapter
                                .MemeSummaryOf(live, snapshot.IdeologyMemeChips);
                        }
                    }
                    else if (Compatibility.Dlc.IdeologyAdapter.Available)
                    {
                        snapshot.IdeologyNoneCount++;
                    }
                    // 绩效候选：只收现役且**确产出过市值**的人（未产出者不参与排行，避免一屏 0）。
                    ProductionAccumulator production = pawn.Production;
                    float produced = production != null ? production.TotalMarketValue : 0f;
                    if (produced > 0f)
                    {
                        performers.Add(new PerfCandidate
                        {
                            Name = !string.IsNullOrEmpty(pawn.LabelShort)
                                ? pawn.LabelShort
                                : (pawn.LabelSnapshot ?? "?"),
                            Value = produced,
                            WorkTicks = TotalWorkTicksOf(pawn.WorkTime),
                            QuadrumValue = SnapshotQuadrumValues(production)
                        });
                    }
                }

            }

            FillCapacities(snapshot, capacityDefs, capacitySums, capacitySamples);
            // 工效 = 意识均值 × 操作均值（预览 ① col3，HTML 703）——依赖能力累加结果，故紧随其后。
            FillWorkEfficiency(snapshot, capacityDefs, capacitySums, capacitySamples);
            FillIdentityShares(snapshot);
            FillSkillTiers(snapshot);
        }

        /// <summary>
        /// ⑥ 人员绩效：按**产出市值**排行（不做银币折算 —— 「工时 × 2 银/h」是预览的示例口径，
        /// 引入汇率就等于编造经济假设，故只附工时供对照，排行键始终是可直接观测的产出市值）。
        /// </summary>
        private static void FillPerformance(PopulationOverviewSnapshot snapshot, List<PerfCandidate> performers)
        {
            if (performers.Count == 0)
            {
                return;
            }
            performers.Sort((a, b) => b.Value.CompareTo(a.Value));
            float total = 0f;
            for (int i = 0; i < performers.Count; i++)
            {
                total += performers[i].Value;
            }
            snapshot.PerformanceTotalText = total.ToString("N0", CultureInfo.InvariantCulture);
            // 人均产出（分母 = 有产出的成员数，与排行同口径；预览 ⑥ 合计行的中间项）。
            snapshot.PerformancePerCapitaText = performers.Count > 0
                ? (total / performers.Count).ToString("N0", CultureInfo.InvariantCulture)
                : string.Empty;
            // 前 3 集中度（真实占比；1–2 人时 = 100%）。
            int top3Count = Mathf.Min(3, performers.Count);
            float top3Total = 0f;
            for (int i = 0; i < top3Count; i++)
            {
                top3Total += performers[i].Value;
            }
            // ⚠ 一律走 `FormatPercent`（F1 + 无空格）：InvariantCulture 的 "P1" 会在数字与 `%`
            //    之间插一个空格（"61.2 %"），与同面板其它百分比（"66.7%"）**同屏不同形**
            //    （实机截图可见；2026-09-15 统一）。
            snapshot.PerformanceTop3ShareText = total > 0f
                ? FormatPercent(top3Total / total)
                : string.Empty;
            float max = performers[0].Value;
            int count = performers.Count < RankMaxRows ? performers.Count : RankMaxRows;
            for (int i = 0; i < count; i++)
            {
                PopulationRankRow row = new PopulationRankRow
                {
                    Label = performers[i].Name,
                    ValueText = performers[i].Value.ToString("N0", CultureInfo.InvariantCulture),
                    // 附注 = 工时（可直接观测的原始量；不折算、不加权）—— UI 侧改放**悬停提示**，行内不再占宽。
                    NoteText = FormatHours(performers[i].WorkTicks / TicksPerHour) + " h",
                    Share01 = max > 0f ? Mathf.Clamp01(performers[i].Value / max) : 0f,
                    // 占**全体**的占比（详细列表「占比」列；与 Share01 口径不同，后者只用于条宽）。
                    // ⚠ 同 `PerformanceTop3ShareText`：不用 "P1"（会输出 "31.9 %" 带空格）。
                    ShareText = total > 0f
                        ? FormatPercent(performers[i].Value / total)
                        : string.Empty,
                    // 数值列（2026-09-17：给 UI 做**排序键**；文本仍走 `ValueText` / `NoteText`）。
                    Value = performers[i].Value,
                    Hours = performers[i].WorkTicks / (float)TicksPerHour
                };
                // 绩效详细列表的 8 列扩展（v6 预览形态）：平均季产 / 峰值季 / 参与季数。
                FillQuadrumDerived(row, performers[i].QuadrumValue);
                snapshot.Performance.Add(row);
            }
            FillPerformanceTrend(snapshot, performers, top3Count);
        }

        /// <summary>
        /// **累计贡献趋势**（预览 ⑥，HTML 941-965）：Top 3 每人**近 8 季累计产出市值**三系列。
        /// 原值 = <c>ProductionAccumulator.MarketValueByQuadrum</c>（真实逐季累加，非采样）；
        /// 此处把「每季增量」转成**累计曲线**（与 ① 面板「入档趋势」的累计口径一致）。
        /// 全系列皆为 0（旧档 / 尚无产出）⇒ 不填快照，UI 画空态说明（不画 0 假线）。
        /// </summary>
        /// <summary>
        /// 绩效详细列表的**季桶派生三列**（v6 预览形态 · 2026-09-15 落地）：
        /// 平均季产 / 峰值季 / 参与季数。
        ///
        /// <para>数据源 = <c>PerfCandidate.QuadrumValue</c>（`ProductionAccumulator` 的 8 季滚动桶，
        /// 真实逐季累加）⇒ **零新增采集**。三列全由该桶直接推导：</para>
        /// <list type="bullet">
        /// <item>平均季产 = 累计 ÷ **有产出的季数**（无产出季**不计入分母** —— 否则新人会被摊薄成假低值）</item>
        /// <item>峰值季 = 桶内最高单季</item>
        /// <item>参与季数 = 桶内**非零**季数</item>
        /// </list>
        /// <para>全零（旧档 / 尚无产出）⇒ 三列**留空**，UI 画「—」而非 0 假值（LAND-009）。</para>
        /// </summary>
        private static void FillQuadrumDerived(PopulationRankRow row, float[] quadrumValues)
        {
            if (row == null)
            {
                return;
            }
            if (quadrumValues == null || quadrumValues.Length == 0)
            {
                return;
            }
            float sum = 0f;
            float peak = 0f;
            int active = 0;
            for (int q = 0; q < quadrumValues.Length; q++)
            {
                float value = quadrumValues[q];
                if (value <= 0f)
                {
                    continue;
                }
                sum += value;
                active++;
                if (value > peak)
                {
                    peak = value;
                }
            }
            if (active == 0)
            {
                return;
            }
            row.AvgQuadrumText = FormatCompact(sum / active);
            row.PeakQuadrumText = FormatCompact(peak);
            // 参与季数用 Keyed 模板（预览 v6 形态「8 / 8 季」）⇒ 译文可整体调整，不做拼接。
            row.QuadrumCountText = "PersonalChronicle.UI.MasterControl.Population.Table.QuadrumCountValue"
                .Translate((NamedArgument)active,
                    (NamedArgument)ProductionAccumulator.QuadrumSlots).ToString();
            // **数值与文本同刻填充**（2026-09-17：UI 排序键；绝不解析文本 —— 见 `PopulationRankRow` 契约）。
            row.AvgQuadrum = sum / active;
            row.PeakQuadrum = peak;
            row.QuadrumCount = active;
        }

        private static void FillPerformanceTrend(
            PopulationOverviewSnapshot snapshot, List<PerfCandidate> performers, int seriesCount)
        {
            if (seriesCount <= 0)
            {
                return;
            }
            const int slots = ProductionAccumulator.QuadrumSlots;
            float[][] series = new float[seriesCount][];
            string[] names = new string[seriesCount];
            float max = 0f;
            int ready = 0;
            for (int s = 0; s < seriesCount; s++)
            {
                float[] raw = performers[s].QuadrumValue;
                float[] cumulative = new float[slots];
                float running = 0f;
                if (raw != null)
                {
                    int n = Mathf.Min(slots, raw.Length);
                    for (int q = 0; q < n; q++)
                    {
                        running += raw[q];
                        cumulative[q] = running;
                    }
                }
                if (running > 0f)
                {
                    ready++;
                }
                if (running > max)
                {
                    max = running;
                }
                names[s] = performers[s].Name;
                series[s] = cumulative;
            }
            if (ready == 0)
            {
                return;
            }
            snapshot.PerformanceTrendNames = names;
            snapshot.PerformanceTrendSeries = series;
            snapshot.PerformanceTrendCapText = FormatCompact(max);
            snapshot.PerformanceTrendAxisLabels = BuildQuadrumAxisLabels(TrendAxisSlots);
        }

        /// <summary>近 8 季滚动桶的读取入口：先对齐当前季（幂等），再拷 8 段原始值。</summary>
        private static float[] SnapshotQuadrumValues(ProductionAccumulator production)
        {
            if (production == null)
            {
                return null;
            }
            production.AdvanceQuadrum(CurrentQuadrumIndex());
            int slots = ProductionAccumulator.QuadrumSlots;
            float[] values = new float[slots];
            List<float> buckets = production.MarketValueByQuadrum;
            if (buckets == null)
            {
                return values;
            }
            int count = Mathf.Min(slots, buckets.Count);
            for (int i = 0; i < count; i++)
            {
                values[i] = buckets[i];
            }
            return values;
        }

        /// <summary>当前**绝对季度序号**（tick / 每季 tick；无 tick 管理器时回落 0）。</summary>
        private static long CurrentQuadrumIndex()
        {
            TickManager ticks = Find.TickManager;
            return ticks != null && GenDate.TicksPerQuadrum > 0
                ? ticks.TicksAbs / GenDate.TicksPerQuadrum
                : 0L;
        }

        /// <summary>x 轴取 4 个刻度（与预览 HTML 962-963 的「5499 秋 / 5500 春 / 5500 秋 / 5501 春」同构）。</summary>
        private static readonly int[] TrendAxisSlots = { 0, 2, 5, 7 };

        /// <summary>
        /// 构造 x 轴季度标签（**段索引 → 绝对季度 → 年 + 季名**；构建期格式化，绘制期零翻译）。
        /// 供绩效趋势（4 刻度）与身份趋势（<see cref="IdentityTrendAxisSlots"/> 3 刻度）共用 ——
        /// 两者共用同一 8 季桶窗口，第 7 段恒为当前季。
        /// </summary>
        private static string[] BuildQuadrumAxisLabels(int[] axisSlots)
        {
            if (axisSlots == null || axisSlots.Length == 0)
            {
                return new string[0];
            }
            const int slots = ProductionAccumulator.QuadrumSlots;
            long current = CurrentQuadrumIndex();
            string[] labels = new string[axisSlots.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                // 段 i 的绝对季度序号：段 7 = 当前季，向前推。
                long quadrum = current - (slots - 1 - axisSlots[i]);
                long tick = quadrum * GenDate.TicksPerQuadrum;
                int season = (int)(((quadrum % 4) + 4) % 4);
                string seasonName = ("PersonalChronicle.UI.MasterControl.Population.Performance.Quadrum." + season)
                    .Translate().ToString();
                labels[i] = "PersonalChronicle.UI.MasterControl.Population.Performance.AxisLabel"
                    .Translate((NamedArgument)GenDate.Year(tick, 0f), (NamedArgument)seasonName).ToString();
            }
            return labels;
        }

        /// <summary>紧凑数值（≥1000 走 "1.2K"，与预览 y 上界「2.4K」同形）。</summary>
        private static string FormatCompact(float value)
        {
            return value >= 1000f
                ? (value / 1000f).ToString("F1", CultureInfo.InvariantCulture) + "K"
                : value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// ③ 派系与外交：活读 <c>Find.FactionManager</c>（不持久化）。
        /// 顺序：本殖民地（玩家派系）优先，其余按名称序（稳定、可预期；不按好感度排 —— 那会让列表随外交漂移）。
        /// </summary>
        private static void FillFactions(PopulationOverviewSnapshot snapshot)
        {
            FactionManager manager = Find.FactionManager;
            if (manager == null)
            {
                return;
            }
            List<Faction> all = manager.AllFactionsListForReading;
            if (all == null || all.Count == 0)
            {
                return;
            }
            List<Faction> sorted = new List<Faction>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                // hidden 是 bool?（原版语义：null = 未显式设置）⇒ 用 != true 判，避免误滤。
                if (all[i] != null && !all[i].defeated && all[i].hidden != true)
                {
                    sorted.Add(all[i]);
                }
            }
            sorted.Sort((a, b) =>
            {
                if (a.IsPlayer != b.IsPlayer)
                {
                    return a.IsPlayer ? -1 : 1;
                }
                // 比较器里绝不能直读 Faction.Name：非人类派系的 def.factionNameMaker 可能缺失，
                // 求值会抛 NRE，而比较器内抛异常会让 List.Sort 整个炸掉（连累整个概览）。
                return string.CompareOrdinal(SafeFactionName(a), SafeFactionName(b));
            });
            // 科技层级分布（预览 ③ 底部 drow 的真实等价）：逐派系累加可观测的科技等级。
            Dictionary<string, int> techCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            int count = sorted.Count < FactionMaxRows ? sorted.Count : FactionMaxRows;
            for (int i = 0; i < count; i++)
            {
                Faction faction = sorted[i];
                try
                {
                    bool self = faction.IsPlayer;
                    // 非玩家派系：好感度 / 关系都可能有读取陷阱（无外交关系记录、def 缺失），
                    // 统一放进逐派系 try —— 一个派系读不动只跳过它，不让它拖垮整个概览。
                    float goodwill = self ? 0f : faction.PlayerGoodwill;
                    FactionRelationKind kind = self
                        ? FactionRelationKind.Neutral
                        : faction.PlayerRelationKind;
                    // 科技等级：**必须走原版 `ToStringHuman()`** —— 枚举名是语言无关串，
                    // 直接 ToString 会在界面显示 Industrial / Spacer / Mech（实机事故，2026-09-15）。
                    string techText = self || faction.def == null
                        ? string.Empty
                        : faction.def.techLevel.ToStringHuman().CapitalizeFirst();
                    snapshot.Factions.Add(new PopulationFactionRow
                    {
                        Label = self
                            ? "PersonalChronicle.UI.MasterControl.Population.FactionSelf".Translate().ToString()
                            : SafeFactionName(faction),
                        RelationText = self ? string.Empty : RelationTextOf(kind),
                        GoodwillText = self
                            ? string.Empty
                            : (goodwill >= 0f ? "+" : string.Empty)
                                + goodwill.ToString("F0", CultureInfo.InvariantCulture),
                        Tone = self
                            ? SystemCenterTone.Good
                            : (kind == FactionRelationKind.Hostile
                                ? SystemCenterTone.Bad
                                : (kind == FactionRelationKind.Ally
                                    ? SystemCenterTone.Good
                                    : SystemCenterTone.Flat)),
                        Goodwill01 = Mathf.Clamp01((goodwill + 100f) / 200f),
                        TechText = techText
                    });
                    if (!string.IsNullOrEmpty(techText))
                    {
                        int seen;
                        techCounts.TryGetValue(techText, out seen);
                        techCounts[techText] = seen + 1;
                    }
                }
                catch (System.Exception ex)
                {
                    if (ChronicleLog.Throttled("pop_overview_faction_row_fail", 60000))
                    {
                        ChronicleLog.Error(ChronicleLog.Category.Provider,
                            "faction row skipped (" + SafeFactionName(faction) + "): " + ex);
                    }
                }
            }
            snapshot.FactionTechSummaryText = TechSummaryText(techCounts, 2);
        }

        /// <summary>
        /// 把「名称 → 数量」汇总成一行展示文本（最多 <paramref name="max"/> 项，按数量降序）。
        /// 分隔符与书写顺序全部走 Keyed（`TechCount` = 名 + 数，`PairDot` = 「{0} · {1}」），
        /// 绘制期零拼接。
        /// </summary>
        private static string TechSummaryText(Dictionary<string, int> counts, int max)
        {
            if (counts == null || counts.Count == 0)
            {
                return string.Empty;
            }
            List<KeyValuePair<string, int>> rows = new List<KeyValuePair<string, int>>(counts.Count);
            foreach (KeyValuePair<string, int> pair in counts)
            {
                if (pair.Value > 0)
                {
                    rows.Add(pair);
                }
            }
            if (rows.Count == 0)
            {
                return string.Empty;
            }
            rows.Sort((a, b) => b.Value.CompareTo(a.Value));
            int n = Mathf.Min(max, rows.Count);
            string text = string.Empty;
            for (int i = 0; i < n; i++)
            {
                string item = "PersonalChronicle.UI.MasterControl.Population.TechCount".Translate(
                    (NamedArgument)rows[i].Key,
                    (NamedArgument)rows[i].Value.ToString(CultureInfo.InvariantCulture)).ToString();
                text = string.IsNullOrEmpty(text)
                    ? item
                    : "PersonalChronicle.UI.MasterControl.Population.PairDot".Translate(
                        (NamedArgument)text, (NamedArgument)item).ToString();
            }
            return text;
        }

        /// <summary>
        /// 安全派系名：<c>Faction.Name</c> 对非人类派系可能触发 NameGenerator
        /// （<c>def.factionNameMaker</c> 缺失即抛），<c>faction.def</c> 亦可能为 null
        /// （运行时派系 / mod 卸载残留）⇒ 逐级回落 defName → loadID，**任何情况下不抛**。
        /// </summary>
        private static string SafeFactionName(Faction faction)
        {
            if (faction == null)
            {
                return string.Empty;
            }
            try
            {
                string name = faction.Name;
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
            catch (System.Exception)
            {
                // 名称生成失败 ⇒ 走回落（不留白、不抛）。
            }
            if (faction.def != null && !string.IsNullOrEmpty(faction.def.defName))
            {
                return faction.def.defName;
            }
            return faction.GetUniqueLoadID();
        }

        /// <summary>
        /// ② 信仰与理念（预览 753-816 的分布部分）：**活读** pawn 理念（不持久化）。
        /// 未启用 Ideology 资料片时只置 <c>IdeologyEnabled = false</c>，由 UI 画「未启用」提示
        /// —— 不画空列表、不伪造数据。
        /// <para>⚠ 2026-09-15（审计 COMP-006 修复）：DLC 类型读取已收口进
        /// <c>Compatibility/Dlc/IdeologyAdapter</c>（本方法只见 `string` / `List&lt;string&gt;` / `Dictionary`）——
        /// 原先「不直连 DLC 类型」的声明与实现不符，现已**由结构保证成立**（COMP-001 / GOV-008）。</para>
        /// 预览的 Meme / Precept / 仪式次数不在本轮范围（需更深的 Ideology 读取面，缺口登记）。
        /// </summary>
        private static void FillIdeology(PopulationOverviewSnapshot snapshot, Dictionary<string, int> counts)
        {
            if (!ModsConfig.IdeologyActive)
            {
                snapshot.IdeologyEnabled = false;
                return;
            }
            snapshot.IdeologyEnabled = true;
            List<KeyValuePair<string, int>> rows = new List<KeyValuePair<string, int>>(counts.Count);
            int total = snapshot.IdeologyNoneCount;
            foreach (KeyValuePair<string, int> pair in counts)
            {
                if (pair.Value > 0)
                {
                    rows.Add(pair);
                    total += pair.Value;
                }
            }
            if (total <= 0)
            {
                return;
            }
            rows.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < rows.Count; i++)
            {
                float share = Mathf.Clamp01(rows[i].Value / (float)total);
                string shareText = (share * 100f).ToString("F1", CultureInfo.InvariantCulture) + "%";
                snapshot.Ideologies.Add(new PopulationCountRow
                {
                    Label = rows[i].Key,
                    Count = rows[i].Value,
                    ShareText = shareText,
                    Share01 = share,
                    ValueText = "PersonalChronicle.UI.MasterControl.Population.PairDot".Translate(
                        (NamedArgument)rows[i].Value, (NamedArgument)shareText).ToString()
                });
            }
            // 环心 / 分母 = **全人口**（含无信仰）—— 与预览 ②「全人口信仰结构」同口径；
            // 旧口径只算有理念者 ⇒ 环心比预览少「无信仰」那部分（实机截图 11 vs 12）。
            snapshot.IdeologyPopulationTotal = total;
        }

        /// <summary>
        /// 档案活跃度趋势（近 8 季事件数）——绩效面板的时间序列。
        /// 走 <c>GetEventBuckets</c>（O(N) 单次遍历、零排序零复制），**不用** GetAllEvents（O(n) 复制）。
        /// </summary>
        private void FillActivityTrend(PopulationOverviewSnapshot snapshot)
        {
            const int bucketCount = 8;
            int[] buckets = queries.GetEventBuckets(bucketCount, GenDate.TicksPerQuadrum);
            if (buckets == null || buckets.Length == 0)
            {
                return;
            }
            int max = 0;
            for (int i = 0; i < buckets.Length; i++)
            {
                snapshot.ActivityTrend[i] = buckets[i];
                if (buckets[i] > max)
                {
                    max = buckets[i];
                }
            }
            snapshot.ActivityTrendMax = max;
        }

        /// <summary>
        /// **入档趋势**（近 8 季**累计**入档数）：先由 <c>GetEventBucketsByKind(Join, …)</c> 取每季
        /// 增量（Data 层 O(N) 单次遍历、零排序），再在构建期累计 —— 真实数据、零存档变更。
        /// 与「活跃度」同节奏（300 tick），失败由 <c>SafeSection</c> 隔离。
        /// </summary>
        private void FillJoinTrend(PopulationOverviewSnapshot snapshot)
        {
            const int bucketCount = 8;
            int[] perQuadrum = queries.GetEventBucketsByKind(
                ChronicleEventKind.Join, bucketCount, GenDate.TicksPerQuadrum);
            if (perQuadrum == null || perQuadrum.Length == 0)
            {
                return;
            }
            int running = 0;
            int max = 0;
            int count = Mathf.Min(bucketCount, perQuadrum.Length);
            for (int i = 0; i < count; i++)
            {
                running += perQuadrum[i];
                snapshot.JoinTrend[i] = running;
                if (running > max)
                {
                    max = running;
                }
            }
            snapshot.JoinTrendMax = max;
        }

        /// <summary>
        /// **分身份存量趋势**（按季回推）：以当前三态人数为基准，由
        /// <c>GetIdentityTrends</c> 逆向撤销带身份事件（Join/Leave/RoleChanged），
        /// 得到每季末的自由 / 奴隶 / 囚犯在册人数（真实事件反推）。
        /// </summary>
        private void FillIdentityTrends(PopulationOverviewSnapshot snapshot)
        {
            const int bucketCount = 8;
            int[][] trends = queries.GetIdentityTrends(bucketCount, GenDate.TicksPerQuadrum,
                new[] { snapshot.FreeColonists, snapshot.SlaveColonists, snapshot.PrisonerColonists });
            if (trends == null || trends.Length < 3)
            {
                return;
            }
            for (int i = 0; i < bucketCount && i < trends[0].Length; i++)
            {
                snapshot.IdentityTrendFree[i] = trends[0][i];
                snapshot.IdentityTrendSlave[i] = trends[1][i];
                snapshot.IdentityTrendPrisoner[i] = trends[2][i];
            }
            snapshot.IdentityTrendReady = true;
            // 量程（预览 ① col1 `#classTrendRange` = 「0–9 人」）：取**三系列全体**极值，
            // 在构建期格式化 ⇒ 绘制期零比较、零拼接（PERF-001 / 08c DL 契约）。
            int min = int.MaxValue;
            int max = int.MinValue;
            for (int s = 0; s < 3; s++)
            {
                int[] series = trends[s];
                int n = Mathf.Min(bucketCount, series.Length);
                for (int i = 0; i < n; i++)
                {
                    if (series[i] < min) { min = series[i]; }
                    if (series[i] > max) { max = series[i]; }
                }
            }
            if (min <= max)
            {
                snapshot.IdentityTrendRangeText = "PersonalChronicle.UI.MasterControl.Population.Trend.Range".Translate(
                    (NamedArgument)min, (NamedArgument)max).ToString();
            }
            // 逐段季度标签（8 个）：UI 取首 / 中 / 末画 x 轴、并用于悬停气泡（同一数组 ⇒ 两处同源）。
            snapshot.IdentityTrendBucketLabels = BuildQuadrumAxisLabels(AllTrendSlots);
        }

        /// <summary>全段索引（0..7）—— 身份趋势的**逐段**标签，供 UI 自取 x 轴刻度与悬停气泡。</summary>
        private static readonly int[] AllTrendSlots = { 0, 1, 2, 3, 4, 5, 6, 7 };

        // ⚠ 2026-09-15（审计 COMP-006 修复）：原 `BuildMemeText(Ideo)` / `AppendMemeChips(List<string>, Ideo)`
        // 两个方法（接收 DLC 类型 `Ideo`/`MemeDef`）**已移入 `Compatibility/Dlc/IdeologyAdapter.MemeSummaryOf`
        // (Pawn, List<string>)`** —— Core 不再持有任何 DLC 类型的方法签名。

        private static string RelationTextOf(FactionRelationKind kind)
        {
            switch (kind)
            {
                case FactionRelationKind.Hostile:
                    return "PersonalChronicle.UI.MasterControl.Population.RelationHostile".Translate().ToString();
                case FactionRelationKind.Ally:
                    return "PersonalChronicle.UI.MasterControl.Population.RelationAlly".Translate().ToString();
                default:
                    return "PersonalChronicle.UI.MasterControl.Population.RelationNeutral".Translate().ToString();
            }
        }

        /// <summary>
        /// 累计工时回算（ticks）：<c>TotalWorkTicks</c> 为 0 时按各工种累加
        /// —— 与 <c>ArchiveService.Work.GetTotalWorkTicks</c> 同口径（旧档缺字段时该值为 0）。
        /// </summary>
        private static long TotalWorkTicksOf(WorkTimeAccumulator workTime)
        {
            if (workTime == null)
            {
                return 0L;
            }
            if (workTime.TotalWorkTicks > 0L)
            {
                return workTime.TotalWorkTicks;
            }
            long total = 0L;
            if (workTime.TicksByWorkType != null)
            {
                foreach (KeyValuePair<string, long> pair in workTime.TicksByWorkType)
                {
                    if (pair.Value > 0L)
                    {
                        total += pair.Value;
                    }
                }
            }
            return total;
        }

        /// <summary>
        /// 技能梯队归档：取该人**最高专业技能等级**（<c>ProfessionalSkillData.level</c>）落到 4 档之一。
        /// 无任何技能记录者不参与统计（不虚构「学徒」）。
        /// </summary>
        private static void CountSkillTier(PawnObject pawn, PopulationOverviewSnapshot snapshot)
        {
            if (pawn.CareerData == null || pawn.CareerData.Professional == null
                || pawn.CareerData.Professional.skills == null || pawn.CareerData.Professional.skills.Count == 0)
            {
                return;
            }
            int top = -1;
            List<Domain.Profession.ProfessionalSkillData> skills = pawn.CareerData.Professional.skills;
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] != null && skills[i].level > top)
                {
                    top = skills[i].level;
                }
            }
            if (top < 0)
            {
                return;
            }
            if (top >= SkillExpertLevel)
            {
                snapshot.SkillExpert++;
            }
            else if (top >= SkillSkilledLevel)
            {
                snapshot.SkillSkilled++;
            }
            else if (top >= SkillNoviceLevel)
            {
                snapshot.SkillNovice++;
            }
            else
            {
                snapshot.SkillApprentice++;
            }
        }

        /// <summary>梯队 4 行（顺序固定：专家 → 熟练 → 入门 → 学徒；占比 = 该档 / 有技能者总数）。</summary>
        private static void FillSkillTiers(PopulationOverviewSnapshot snapshot)
        {
            int total = snapshot.SkillExpert + snapshot.SkillSkilled
                + snapshot.SkillNovice + snapshot.SkillApprentice;
            snapshot.SkillTierTotal = total;
            if (total <= 0)
            {
                return;
            }
            AddSkillTier(snapshot, "PersonalChronicle.UI.Population.Tier.Expert", snapshot.SkillExpert, total);
            AddSkillTier(snapshot, "PersonalChronicle.UI.Population.Tier.Skilled", snapshot.SkillSkilled, total);
            AddSkillTier(snapshot, "PersonalChronicle.UI.Population.Tier.Novice", snapshot.SkillNovice, total);
            AddSkillTier(snapshot, "PersonalChronicle.UI.Population.Tier.Apprentice", snapshot.SkillApprentice, total);
        }

        private static void AddSkillTier(PopulationOverviewSnapshot snapshot, string labelKey, int count, int total)
        {
            float share = Mathf.Clamp01(count / (float)total);
            string shareText = (share * 100f).ToString("F1", CultureInfo.InvariantCulture) + "%";
            snapshot.SkillTiers.Add(new PopulationCountRow
            {
                Label = labelKey.Translate().ToString(),
                Count = count,
                ShareText = shareText,
                Share01 = share,
                ValueText = "PersonalChronicle.UI.MasterControl.Population.PairDot".Translate(
                    (NamedArgument)count, (NamedArgument)shareText).ToString()
            });
        }

        /// <summary>身份 / 性别占比（分母统一 = 现役殖民者数；口径只此一处）。</summary>
        private static void FillIdentityShares(PopulationOverviewSnapshot snapshot)
        {
            int total = snapshot.LiveColonists > 0 ? snapshot.LiveColonists : 0;
            snapshot.FreeShare01 = total > 0 ? Mathf.Clamp01((float)snapshot.FreeColonists / total) : 0f;
            snapshot.SlaveShare01 = total > 0 ? Mathf.Clamp01((float)snapshot.SlaveColonists / total) : 0f;
            snapshot.PrisonerShare01 = total > 0 ? Mathf.Clamp01((float)snapshot.PrisonerColonists / total) : 0f;
            // 性别占比与身份占比**同分母**（现役殖民者数）：这样同一面板里几组百分比可以互相加总核对；
            // 若改用「男+女」做分母，无档案 / 性别未登记的成员会让两组百分比对不上。
            snapshot.MaleShare01 = total > 0 ? Mathf.Clamp01((float)snapshot.MaleColonists / total) : 0f;
            snapshot.FemaleShare01 = total > 0 ? Mathf.Clamp01((float)snapshot.FemaleColonists / total) : 0f;
        }

        private static bool HasPrimaryDirection(PawnObject pawn)
        {
            return pawn.CareerData != null && pawn.CareerData.Professional != null
                && !string.IsNullOrEmpty(pawn.CareerData.Professional.primaryDirection);
        }

        /// <summary>
        /// 累加该人各工种工时；返回「是否确有工时记录」（供人均分母使用）。
        /// </summary>
        private static bool AddLaborTicks(PawnObject pawn, Dictionary<string, long> labor)
        {
            if (pawn.WorkTime == null || pawn.WorkTime.TicksByWorkType == null)
            {
                return false;
            }
            bool recorded = false;
            foreach (KeyValuePair<string, long> pair in pawn.WorkTime.TicksByWorkType)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0L)
                {
                    continue;
                }
                long sum;
                labor.TryGetValue(pair.Key, out sum);
                labor[pair.Key] = sum + pair.Value;
                recorded = true;
            }
            return recorded;
        }

        private static void CountMedals(PawnObject pawn, PopulationOverviewSnapshot snapshot)
        {
            if (pawn.GrantedMedals == null)
            {
                return;
            }
            for (int i = 0; i < pawn.GrantedMedals.Count; i++)
            {
                string defName = pawn.GrantedMedals[i];
                if (string.IsNullOrEmpty(defName))
                {
                    continue;
                }
                MedalDef def = DefDatabase<MedalDef>.GetNamedSilentFail(defName);
                if (def == null)
                {
                    continue;
                }
                snapshot.MedalAwarded++;
                switch (def.tier)
                {
                    case MedalTier.Gold: snapshot.MedalGold++; break;
                    case MedalTier.Silver: snapshot.MedalSilver++; break;
                    default: snapshot.MedalBronze++; break;
                }
            }
        }

        private static void CountTitles(PawnObject pawn, Dictionary<string, int> titles)
        {
            if (pawn.CareerData == null || pawn.CareerData.GrantedTitles == null)
            {
                return;
            }
            for (int i = 0; i < pawn.CareerData.GrantedTitles.Count; i++)
            {
                GrantedTitle granted = pawn.CareerData.GrantedTitles[i];
                if (granted == null || string.IsNullOrEmpty(granted.TitleDefName))
                {
                    continue;
                }
                int count;
                titles.TryGetValue(granted.TitleDefName, out count);
                titles[granted.TitleDefName] = count + 1;
            }
        }

        private static void CountDirection(PawnObject pawn, Pawn live, Dictionary<string, int> directions)
        {
            if (live == null || pawn.CareerData == null || pawn.CareerData.Professional == null)
            {
                return;
            }
            string primary = pawn.CareerData.Professional.primaryDirection;
            if (string.IsNullOrEmpty(primary))
            {
                return;
            }
            int count;
            directions.TryGetValue(primary, out count);
            directions[primary] = count + 1;
        }

        /// <summary>
        /// 工效 = 意识均值 × 操作均值（预览 ① col3「工效 = 意识 × 操作」，HTML 703）。
        /// 能力**按 defName 定位**（GOV-009：只认稳定 ID，绝不按 label/翻译文本）；
        /// 两项缺任一项 ⇒ 保持 -1（UI 画「—」，不画 0 假值）。
        /// </summary>
        private static void FillWorkEfficiency(PopulationOverviewSnapshot snapshot,
            List<PawnCapacityDef> defs, double[] sums, int samples)
        {
            if (defs == null || sums == null || samples <= 0)
            {
                return;
            }
            double consciousness = -1.0;
            double manipulation = -1.0;
            int count = Mathf.Min(defs.Count, sums.Length);
            for (int i = 0; i < count; i++)
            {
                PawnCapacityDef def = defs[i];
                if (def == null)
                {
                    continue;
                }
                if (string.Equals(def.defName, "Consciousness", StringComparison.Ordinal))
                {
                    consciousness = sums[i] / samples;
                }
                else if (string.Equals(def.defName, "Manipulation", StringComparison.Ordinal))
                {
                    manipulation = sums[i] / samples;
                }
            }
            if (consciousness >= 0.0 && manipulation >= 0.0)
            {
                snapshot.WorkEfficiency01 = (float)(consciousness * manipulation);
            }
        }

        private static void AccumulateCapacities(Pawn pawn, List<PawnCapacityDef> defs,
            double[] sums, ref int samples)
        {
            if (defs == null || pawn.health == null || pawn.health.capacities == null)
            {
                return;
            }
            for (int i = 0; i < defs.Count; i++)
            {
                sums[i] += pawn.health.capacities.GetLevel(defs[i]);
            }
            samples++;
        }

        private static void FillCapacities(PopulationOverviewSnapshot snapshot,
            List<PawnCapacityDef> defs, double[] sums, int samples)
        {
            if (defs == null || samples <= 0)
            {
                return;
            }
            double total = 0.0;
            for (int i = 0; i < defs.Count; i++)
            {
                float level = (float)(sums[i] / samples);
                total += level;
                // 明细**现在有消费者**（概览 ① 面板的「能力条」列表，预览 730-746）⇒ 重新产出。
                // （上一轮曾因「全链路无人消费」删过它 —— 采集与消费必须同时存在，否则就是死负载。）
                snapshot.Capacities.Add(new PopulationCapacityRow
                {
                    Label = defs[i] != null && !string.IsNullOrEmpty(defs[i].label)
                        ? defs[i].label.CapitalizeFirst()
                        : (defs[i] != null ? defs[i].defName : string.Empty),
                    Level01 = level,
                    LevelText = FormatPercent(level)
                });
            }
            snapshot.CapacityAverage01 = (float)(total / defs.Count);
            snapshot.CapacityAverageText = FormatPercent(snapshot.CapacityAverage01);
        }

        private static void FillLabor(PopulationOverviewSnapshot snapshot, Dictionary<string, long> ticks)
        {
            List<KeyValuePair<string, long>> rows = new List<KeyValuePair<string, long>>(ticks.Count);
            double total = 0.0;
            foreach (KeyValuePair<string, long> pair in ticks)
            {
                if (pair.Value <= 0L)
                {
                    continue;
                }
                rows.Add(pair);
                total += pair.Value / TicksPerHour;
            }
            rows.Sort((a, b) => b.Value.CompareTo(a.Value));
            double topThree = 0.0;
            int count = rows.Count < LaborMaxRows ? rows.Count : LaborMaxRows;
            for (int i = 0; i < count; i++)
            {
                double hours = rows[i].Value / TicksPerHour;
                if (i < 3)
                {
                    topThree += hours;
                }
                WorkTypeDef def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(rows[i].Key);
                snapshot.Labor.Add(new PopulationLaborRow
                {
                    Label = def != null ? def.LabelCap.ToString() : rows[i].Key,
                    HoursText = FormatHours(hours),
                    // 柱顶标注用的短数值（预览 `.barv` 每柱顶部「2.4k」）——构建期格式化。
                    ShortText = FormatCompactHours(hours),
                    Share01 = total > 0.0 ? (float)(hours / total) : 0f,
                    // 占全殖民地的占比文本（详细列表「占比」列）。
                    ShareText = total > 0.0
                        ? (hours / total).ToString("P1", CultureInfo.InvariantCulture)
                        : string.Empty
                });
            }
            snapshot.LaborTotalText = total > 0.0 ? FormatHours(total) : UnknownText();
            // 人均分母 = 有工时记录的现役成员（LaborContributors），不是「现役殖民者数」：
            // 分子是**全体历史成员**的累计工时（殖民地的劳动家底），用现役人数做分母会把
            // 已故/离队者的贡献平摊到在座的人头上，与档案馆既有的「各成员日均算术平均」口径冲突。
            snapshot.LaborPerCapitaText = total > 0.0 && snapshot.LaborContributors > 0
                ? FormatHours(total / snapshot.LaborContributors)
                : string.Empty;
            snapshot.LaborTopThreeText = total > 0.0 ? FormatPercent((float)(topThree / total)) : string.Empty;
        }

        private static void FillDistributions(PopulationOverviewSnapshot snapshot,
            Dictionary<string, int> titles, Dictionary<string, int> directions)
        {
            FillDistribution(snapshot.Titles, titles, defName =>
            {
                ProfessionalTitleDef def = DefDatabase<ProfessionalTitleDef>.GetNamedSilentFail(defName);
                return def != null ? def.LabelCap.ToString() : defName;
            });
            FillDistribution(snapshot.Directions, directions, defName =>
            {
                ProfessionalDirectionDef def = DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(defName);
                return def != null ? def.GetDisplayLabel().Translate().ToString() : defName;
            });
            // ⑧ 专业分布：未设定人数 + 方向缺口（「全部方向 Def − 已用方向」真实作差）。
            FillSpecializationSummary(snapshot, directions);
        }

        /// <summary>
        /// ⑧ 专业分布的补充口径：未设定主方向人数 + **无人主攻的方向**（全部方向 Def 减已用方向）。
        /// 预览 ⑧ 的 chips 末项与底部 drow；方向 Def 缺失时留空（不编造）。
        /// </summary>
        private static void FillSpecializationSummary(
            PopulationOverviewSnapshot snapshot, Dictionary<string, int> directions)
        {
            int unset = snapshot.SpecializationTotal - snapshot.SpecializationSet;
            snapshot.SpecializationUnsetText = Mathf.Max(0, unset).ToString(CultureInfo.InvariantCulture);

            List<ProfessionalDirectionDef> all = DefDatabase<ProfessionalDirectionDef>.AllDefsListForReading;
            if (all == null || all.Count == 0)
            {
                return;
            }
            string gap = string.Empty;
            int shown = 0;
            for (int i = 0; i < all.Count && shown < 2; i++)
            {
                ProfessionalDirectionDef def = all[i];
                if (def == null || string.IsNullOrEmpty(def.defName))
                {
                    continue;
                }
                int used;
                if (directions != null && directions.TryGetValue(def.defName, out used) && used > 0)
                {
                    continue;
                }
                string label = def.GetDisplayLabel().Translate().ToString();
                gap = string.IsNullOrEmpty(gap)
                    ? label
                    : "PersonalChronicle.UI.MasterControl.Population.PairSlash".Translate(
                        (NamedArgument)gap, (NamedArgument)label).ToString();
                shown++;
            }
            snapshot.SpecializationGapText = gap;
        }

        private static void FillDistribution(List<PopulationCountRow> target,
            Dictionary<string, int> counts, Func<string, string> labelOf)
        {
            List<KeyValuePair<string, int>> rows = new List<KeyValuePair<string, int>>(counts.Count);
            foreach (KeyValuePair<string, int> pair in counts)
            {
                if (pair.Value > 0)
                {
                    rows.Add(pair);
                }
            }
            rows.Sort((a, b) => b.Value.CompareTo(a.Value));
            int total = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                total += rows[i].Value;
            }
            int count = rows.Count < DistributionMaxRows ? rows.Count : DistributionMaxRows;
            for (int i = 0; i < count; i++)
            {
                float share = total > 0 ? (float)rows[i].Value / total : 0f;
                string shareText = FormatPercent(share);
                target.Add(new PopulationCountRow
                {
                    Label = labelOf(rows[i].Key),
                    Count = rows[i].Value,
                    ShareText = shareText,
                    Share01 = share,
                    // 值文本在构建期用 Keyed 模板拼好（绘制期零拼接，分隔符顺序交给语言文件）。
                    ValueText = "PersonalChronicle.UI.MasterControl.Population.PairDot".Translate(
                        (NamedArgument)rows[i].Value, (NamedArgument)shareText).ToString()
                });
            }
        }



        private static string UnknownText()
        {
            return "PersonalChronicle.UI.Population.Roster.Unknown".Translate().ToString();
        }

        private static string FormatHours(double hours)
        {
            return hours.ToString("N1", CultureInfo.InvariantCulture);
        }

        /// <summary>柱顶短数值（≥1000 走 "2.4k"，与预览 `.barv` 的柱顶标注同形）。</summary>
        private static string FormatCompactHours(double hours)
        {
            return hours >= 1000.0
                ? (hours / 1000.0).ToString("F1", CultureInfo.InvariantCulture) + "k"
                : hours.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static string FormatPercent(float share01)
        {
            return (share01 * 100f).ToString("F1", CultureInfo.InvariantCulture) + "%";
        }
    }
}
