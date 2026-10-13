using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Profession;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Application.SystemCenters.Dev
{
    /// <summary>
    /// **开发者测试数据**：生成一份「随机但结构完整」的人员概览快照，供总控页头的
    /// 「测试数据」按钮（仅 <c>Prefs.DevMode</c>）在空档 / 新档上检验**全部前端组件的完整形态**
    /// （环形 / 面积图 / 柱图 / 卡片 / chips / 榜单 / 空柱骨架之外的真实满值）。
    ///
    /// <para><b>边界（重要）</b>：
    /// ① 只产出 POCO 快照 —— <b>不写存档、不改档案、不产生事件、不改 Def</b>；
    /// ② 只由 UI 在 dev 开关打开时调用，**不进入 Provider 的真实构建路径**（关掉即恢复真实数据）；
    /// ③ 文案统一带「样例」前缀的 Keyed 模板，避免与真实读数混淆。</para>
    ///
    /// <para><b>2026-09-16「优化测试数据」（用户需求：它只有一个固定数据而不是随机数据）</b>：
    /// 全字段改为 <see cref="System.Random"/> 驱动（同 <c>seed</c> 同结果 ⇒ 可复现截图），并**逐项自洽**
    /// —— 任何两个数之间能算的关系都必须算得通，否则界面会同时显示互相矛盾的两组数：</para>
    /// <list type="bullet">
    /// <item><c>Free + Slave + Prisoner == Live</c>；<c>Male + Female == Live</c>；</item>
    /// <item>身份 × 性别交叉：<c>ΣIdentityMale == Male</c>、<c>ΣIdentityFemale == Female</c>，且每身份男 ≤ 该身份人数；</item>
    /// <item>各 shares = 计数 / 分母（身份 / 性别分母统一 = 现役人数，与 Provider 同口径）；</item>
    /// <item>人均 = 总量 / 人数（工时人均分母 = 有工时记录者数 <see cref="PopulationOverviewSnapshot.LaborContributors"/>）；</item>
    /// <item>绩效每行 8 季桶由该行总量切分 ⇒ <c>Σ桶 == 行总量</c>、<c>峰值季 ≥ 平均季产</c>、
    /// <c>参与季数 == 非零桶数</c>（且 ≤ 8）、Top-3 趋势末点 = 该行总量；</item>
    /// <item>榜 / 分布各行之和 == 对应合计（勋章榜 Σ == 已授予、方向 Σ == 已设定人数、梯队 Σ == 梯队总数）；</item>
    /// <item>分身份存量趋势末点 == 当前三态人数（与「身份构成」面板同源）；</item>
    /// <item>在册 / 归档口径与真实 Provider 一致（在册 = 现役成员档案数）。</item>
    /// </list>
    /// <para><b>规模取「真殖民地感」</b>：现役 4~24 / 服务天数 120~4000 / 事件总量 100~4000 / 归档 0~8；
    /// 不产出 0 人、负值或荒谬比例。</para>
    ///
    /// <para><b>文本口径</b>：百分比 / 工时 / 紧凑数值一律走与 Provider 逐字同形的格式化
    /// （<c>F1 %</c> 无空格、<c>N1</c> 工时、<c>1.2K / 1.2k</c> 紧凑），
    /// 榜 / 分布行也分「分布口径（占比 = /总和）」与「榜口径（条宽 = /最大值）」两种 —— 与 Provider 一致。</para>
    /// </summary>
    public static class DevPopulationFixtures
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>身份格数（自由 / 奴隶 / 囚犯；与快照 <c>IdentityMale</c> / <c>IdentityFemale</c> 对齐）。</summary>
        private const int IdentityCount = 3;

        /// <summary>趋势桶数（近 8 季；与 <c>ProductionAccumulator.QuadrumSlots</c> 同值）。</summary>
        private const int TrendBucketCount = 8;

        /// <summary>构建随机快照（同一 <paramref name="seed"/> 结果稳定，便于复现截图）。</summary>
        public static PopulationOverviewSnapshot BuildRandom(int seed)
        {
            System.Random rng = new System.Random(seed);
            PopulationOverviewSnapshot s = new PopulationOverviewSnapshot();

            // ---- 规模与构成（自洽：Free+Slave+Prisoner == Live；Male+Female == Live）----
            int live = rng.Next(4, 25);                                 // 4..24
            int slave = rng.Next(0, Mathf.Max(2, live / 2 + 1));        // 0..live/2
            int prisoner = rng.Next(0, Mathf.Max(2, live / 4 + 1));     // 0..live/4
            if (slave + prisoner >= live)
            {
                // 不变量守卫（上面的上界已保证不触发）：宁可少一个囚犯，也不让 Free 变负数。
                prisoner = Mathf.Max(0, live - slave - 1);
            }
            int free = live - slave - prisoner;
            int male = rng.Next(0, live + 1);
            s.LiveColonists = live;
            s.FreeColonists = free;
            s.SlaveColonists = slave;
            s.PrisonerColonists = prisoner;
            s.MaleColonists = male;
            s.FemaleColonists = live - male;
            // 在册口径与真实 Provider 一致：在册 = 现役成员档案数；归档 = 已归档档案数（可 0）。
            s.ActiveRecords = live;
            s.ArchivedRecords = rng.Next(0, 9);
            s.ServiceDays = rng.Next(120, 4001);
            s.EventTotal = rng.Next(100, 4001);
            s.FreeShare01 = Share(free, live);
            s.SlaveShare01 = Share(slave, live);
            s.PrisonerShare01 = Share(prisoner, live);
            s.MaleShare01 = Share(male, live);
            s.FemaleShare01 = Share(live - male, live);

            FillCapacities(s, rng);

            // ---- 劳动力 ----
            FillLabor(s, rng, live);

            // ---- 绩效（含每行 8 季桶 ⇒ 三列派生自洽）----
            FillPerformance(s, rng, live);

            // ---- 档案活跃度趋势（近 8 季事件数；桶值随事件总量缩放）----
            int activityCap = Mathf.Max(2, s.EventTotal / 6);
            int[] activity = new int[TrendBucketCount];
            int activityMax = 0;
            for (int i = 0; i < activity.Length; i++)
            {
                activity[i] = rng.Next(0, activityCap);
                if (activity[i] > activityMax)
                {
                    activityMax = activity[i];
                }
            }
            s.ActivityTrend = activity;
            s.ActivityTrendMax = activityMax;

            // ---- 派生指标（与真实路径同一套 Key；占比一律 = 计数 / 分母）----
            s.DerivedReady = true;
            int outputPerCapita = rng.Next(10, 90);
            AddDerived(s, "OutputPerCapita", outputPerCapita.ToString(Inv), outputPerCapita / 100f);
            int topDependency = rng.Next(15, 45);
            AddDerived(s, "TopDependency", topDependency.ToString(Inv) + "%", topDependency / 100f);
            int equipped = rng.Next(0, live + 1);
            AddDerived(s, "Equipped", CountPair(equipped, live), Share(equipped, live));
            int bedded = rng.Next(0, live + 1);
            AddDerived(s, "Bedded", CountPair(bedded, live), Share(bedded, live));
            int fed = rng.Next(0, live + 1);
            AddDerived(s, "Fed", CountPair(fed, live), Share(fed, live));
            int moodRisk = rng.Next(0, live + 1);
            AddDerived(s, "MoodRisk", CountPair(moodRisk, live), Share(moodRisk, live));
            int wounded = rng.Next(0, live + 1);
            AddDerived(s, "Wounded", CountPair(wounded, live), Share(wounded, live));
            int skillDefTotal = Mathf.Max(1, DefDatabase<SkillDef>.DefCount);
            int skillCover = rng.Next(1, skillDefTotal + 1);
            AddDerived(s, "SkillCover", CountPair(skillCover, skillDefTotal),
                skillCover / (float)skillDefTotal);

            // ---- 信仰（Meme / 理念 / 戒律榜）----
            s.IdeologyEnabled = true;
            int none = rng.Next(0, Mathf.Max(2, live / 6 + 1));         // 0..live/6
            int withIdeo = live - none;
            s.IdeologyNoneCount = none;
            // 环心 = 全人口（含无信仰）—— 与 Provider / 预览 ②「全人口信仰结构」同口径。
            s.IdeologyPopulationTotal = live;
            List<string> devMemes = DefLabels<MemeDef>(rng.Next(2, 4),
                "PersonalChronicle.Dev.SampleGeneric");
            s.IdeologyMemeChips.AddRange(devMemes);
            s.IdeologyMemeText = string.Join(" · ", devMemes.ToArray());
            // 理念名是运行时生成的（`Ideo` 不是 Def）⇒ 用 Keyed 样例占位，不冒充真实理念名。
            List<string> ideoNames = new List<string>(2);
            for (int i = 0; i < 2; i++)
            {
                ideoNames.Add("PersonalChronicle.Dev.SampleIdeo"
                    .Translate((NamedArgument)(i + 1)).ToString());
            }
            // 理念分布 = 有理念者按 2 个理念切分（Σ == withIdeo ⇒ 与「无信仰」一起正好是环心）。
            int mainIdeo = withIdeo >= 2 ? rng.Next(1, withIdeo) : withIdeo;
            AddDistributionRows(s.Ideologies, ideoNames, new[] { mainIdeo, withIdeo - mainIdeo });
            // 戒律榜（榜口径；每项人数 ≤ 现役人数）。
            AddTopRows(s.Precepts, DefLabels<PreceptDef>(5, "PersonalChronicle.Dev.SamplePrecept"),
                RandomCountsDescending(rng, 5, live));

            // ---- 荣誉 ----
            // 勋章 Def 目录规模（**不是随机量**：它是 Def 目录的事实读法，正式目录为空时保留一个可读下界）。
            s.MedalDefTotal = Mathf.Max(12, DefDatabase<MedalDef>.DefCount);
            s.MedalGold = rng.Next(0, 6);
            s.MedalSilver = rng.Next(0, 8);
            s.MedalBronze = rng.Next(0, 14);
            s.MedalAwarded = s.MedalGold + s.MedalSilver + s.MedalBronze;   // 合计 = 各行之和
            // 勋章榜 = 已授予枚数在各勋章 Def 间切分（Σ == MedalAwarded ⇒ 与金/银/铜读数对得上）。
            AddTopRows(s.MedalTop, DefLabels<MedalDef>(5, "PersonalChronicle.Dev.SampleMedal"),
                SplitDescending(rng, s.MedalAwarded, 5));
            // 职称分布：Σ == 有职称者数（≤ 现役人数），与 Provider FillDistribution 同形。
            AddDistributionRows(s.Titles,
                DefLabels<ProfessionalTitleDef>(3, "PersonalChronicle.Dev.SampleTitle"),
                SplitDescending(rng, rng.Next(0, live + 1), 3));

            // ---- 派系与外交（含科技等级）----
            // 科技等级走原版 `ToStringHuman()`（与真实路径同源；dev 不再用枚举名，否则会掩盖
            // 「界面出现 Industrial/Spacer」这类本地化缺陷）。
            TechLevel[] techPool =
            {
                TechLevel.Spacer, TechLevel.Industrial, TechLevel.Neolithic,
                TechLevel.Medieval, TechLevel.Industrial, TechLevel.Spacer
            };
            int factionCount = rng.Next(3, 7);                          // 3..6（含本殖民地）
            List<string> factionNames = DefLabels<FactionDef>(factionCount - 1,
                "PersonalChronicle.Dev.SampleFaction");
            Dictionary<string, int> techCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < factionCount; i++)
            {
                if (i == 0)
                {
                    // 首家 = 本殖民地（与 Provider 一致：关系 / 好感留空、不计入科技分布）。
                    s.Factions.Add(new PopulationFactionRow
                    {
                        Label = "PersonalChronicle.UI.MasterControl.Population.FactionSelf"
                            .Translate().ToString(),
                        RelationText = string.Empty,
                        GoodwillText = string.Empty,
                        Tone = SystemCenterTone.Good,
                        Goodwill01 = 0.5f,
                        TechText = string.Empty
                    });
                    continue;
                }
                int goodwill = rng.Next(-100, 101);
                string techText = techPool[rng.Next(0, techPool.Length)]
                    .ToStringHuman().CapitalizeFirst();
                s.Factions.Add(new PopulationFactionRow
                {
                    Label = factionNames[i - 1],
                    TechText = techText,
                    RelationText = goodwill >= 40
                        ? "PersonalChronicle.UI.MasterControl.Population.RelationAlly"
                            .Translate().ToString()
                        : (goodwill <= -40
                            ? "PersonalChronicle.UI.MasterControl.Population.RelationHostile"
                                .Translate().ToString()
                            : "PersonalChronicle.UI.MasterControl.Population.RelationNeutral"
                                .Translate().ToString()),
                    GoodwillText = (goodwill >= 0 ? "+" : string.Empty) + goodwill.ToString(Inv),
                    Tone = goodwill >= 40
                        ? SystemCenterTone.Good
                        : (goodwill <= -40 ? SystemCenterTone.Bad : SystemCenterTone.Flat),
                    Goodwill01 = Mathf.Clamp01((goodwill + 100f) / 200f)
                });
                int seen;
                techCounts.TryGetValue(techText, out seen);
                techCounts[techText] = seen + 1;
            }
            // ③ 底部 drow：科技层级分布（与 Provider TechSummaryText 同形：按数量降序取前 2）。
            s.FactionTechSummaryText = TechSummaryText(techCounts, 2);

            // ---- 专业分布 ----
            // 分母 = 现役成员（Provider 的 SpecializationTotal 逐现役成员累加）。
            s.SpecializationTotal = live;
            s.SpecializationSet = rng.Next(0, live + 1);
            s.SpecializationUnsetText = Mathf.Max(0, s.SpecializationTotal - s.SpecializationSet)
                .ToString(Inv);
            List<string> gapLabels = DefLabels<ProfessionalDirectionDef>(2,
                "PersonalChronicle.Dev.SampleGap");
            if (gapLabels.Count >= 2)
            {
                s.SpecializationGapText = "PersonalChronicle.UI.MasterControl.Population.PairSlash"
                    .Translate((NamedArgument)gapLabels[0], (NamedArgument)gapLabels[1]).ToString();
            }
            // 方向分布：Σ == 已设定主方向人数（与 ⑧ 的「未设定 N」互补）。
            AddDistributionRows(s.Directions,
                DefLabels<ProfessionalDirectionDef>(6, "PersonalChronicle.Dev.SampleDirection"),
                SplitDescending(rng, s.SpecializationSet, 6));

            // ---- 技能梯队（4 档，Σ == SkillTierTotal；档位顺序固定「专家→学徒」，不按人数排）----
            int[] tiers = SplitInts(rng, rng.Next(live, live * 3 + 1), 4);
            s.SkillTierTotal = Sum(tiers);
            s.SkillExpert = tiers[0];
            s.SkillSkilled = tiers[1];
            s.SkillNovice = tiers[2];
            s.SkillApprentice = tiers[3];
            AddTier(s, "PersonalChronicle.UI.Population.Tier.Expert", tiers[0]);
            AddTier(s, "PersonalChronicle.UI.Population.Tier.Skilled", tiers[1]);
            AddTier(s, "PersonalChronicle.UI.Population.Tier.Novice", tiers[2]);
            AddTier(s, "PersonalChronicle.UI.Population.Tier.Apprentice", tiers[3]);

            // ---- 补充图表测试数据（让「测试数据」能覆盖所有 RUIF 图表形态）----
            FillDevCharts(s, rng, live);

            s.BuiltFromRevision = -1L;
            s.IsEmpty = false;
            return s;
        }

        /// <summary>
        /// 补齐**图表方向**的随机字段（真实路径由 Provider 采集，dev 预览在此手写等价随机值，
        /// 以便空档 / 新档也能完整检验：性别金字塔 / 工效行 / 入档趋势单线 / 分身份趋势三系列）。
        /// 与真实路径同口径：交叉计数按身份逐行拆（子计与身份总数一致）；趋势为**累计**单调增。
        /// </summary>
        private static void FillDevCharts(PopulationOverviewSnapshot s, System.Random rng, int live)
        {
            // ① 性别金字塔交叉计数（自由 / 奴隶 / 囚犯 × 男 / 女）：把「男」按身份**带帽**切分
            //    ⇒ 每身份男 ≤ 该身份人数、Σ男 == Male、Σ女 == Female（金字塔与性别占比环同源，不会互斥）。
            int[] idTotals = { s.FreeColonists, s.SlaveColonists, s.PrisonerColonists };
            int[] idMale = SplitCapped(rng, s.MaleColonists, idTotals);
            for (int i = 0; i < IdentityCount; i++)
            {
                s.IdentityMale[i] = idMale[i];
                s.IdentityFemale[i] = idTotals[i] - idMale[i];
            }

            // ② 工效（意识 × 操作，0.6~1.0）：真实路径无样本才为负，dev 恒给正以测试「数值行」形态。
            s.WorkEfficiency01 = 0.6f + (float)rng.NextDouble() * 0.4f;

            // ③ 入档趋势（近 8 季**累计**入档，单调增；单系列折线用）。
            int joinCap = Mathf.Max(2, live / 2 + 1);
            int[] jt = new int[TrendBucketCount];
            int running = 0;
            int jmax = 0;
            for (int i = 0; i < TrendBucketCount; i++)
            {
                running += rng.Next(1, joinCap);
                jt[i] = running;
                if (running > jmax)
                {
                    jmax = running;
                }
            }
            s.JoinTrend = jt;
            s.JoinTrendMax = jmax;

            // ④ 分身份存量趋势（三系列，各单调不减、**末点 = 当前三态人数** ⇒ 与「身份构成」面板同源）。
            s.IdentityTrendFree = CumulativeEndingAt(rng, s.FreeColonists);
            s.IdentityTrendSlave = CumulativeEndingAt(rng, s.SlaveColonists);
            s.IdentityTrendPrisoner = CumulativeEndingAt(rng, s.PrisonerColonists);
            s.IdentityTrendReady = true;

            // 量程（预览 ① col1 `#classTrendRange`）+ x 轴逐段标签：与 Provider 同一套 Keyed 模板。
            int min = int.MaxValue;
            int max = int.MinValue;
            AccumulateRange(s.IdentityTrendFree, ref min, ref max);
            AccumulateRange(s.IdentityTrendSlave, ref min, ref max);
            AccumulateRange(s.IdentityTrendPrisoner, ref min, ref max);
            if (min <= max)
            {
                s.IdentityTrendRangeText = "PersonalChronicle.UI.MasterControl.Population.Trend.Range"
                    .Translate((NamedArgument)min, (NamedArgument)max).ToString();
            }
            s.IdentityTrendBucketLabels = BuildQuadrumAxisLabels(AllTrendSlots);
        }

        /// <summary>全段索引（0..7）—— 身份趋势的**逐段**标签（与 Provider AllTrendSlots 同值）。</summary>
        private static readonly int[] AllTrendSlots = { 0, 1, 2, 3, 4, 5, 6, 7 };

        /// <summary>绩效趋势的 4 个 x 轴刻度（与 Provider TrendAxisSlots 同值）。</summary>
        private static readonly int[] PerformanceTrendAxisSlots = { 0, 2, 5, 7 };

        // ------------------------------------------------------------------ 内部

        private static void FillCapacities(PopulationOverviewSnapshot s, System.Random rng)
        {
            List<string> labels = DefLabels<PawnCapacityDef>(11, "PersonalChronicle.Dev.SampleCapacity");
            float sum = 0f;
            for (int i = 0; i < labels.Count; i++)
            {
                float level = Mathf.Clamp01(0.55f + (float)rng.NextDouble() * 0.45f);
                sum += level;
                s.Capacities.Add(new PopulationCapacityRow
                {
                    Label = labels[i],
                    Level01 = level,
                    LevelText = FmtPercent(level)
                });
            }
            s.CapacityAverage01 = labels.Count > 0 ? sum / labels.Count : -1f;
            s.CapacityAverageText = labels.Count > 0 ? FmtPercent(s.CapacityAverage01) : string.Empty;
        }

        /// <summary>
        /// 劳动力：工时按工种随机（量级随现役人数缩放），<c>LaborContributors</c> = 有工时记录者数 ⇒
        /// <b>人均 = 总量 / 人数</b>（与 Provider 的「分母 = 有工时记录者」同口径）。
        /// </summary>
        private static void FillLabor(PopulationOverviewSnapshot s, System.Random rng, int live)
        {
            List<string> labels = DefLabels<WorkTypeDef>(12, "PersonalChronicle.Dev.SampleWork");
            long[] hours = new long[labels.Count];
            long total = 0L;
            int low = 120 + live * 40;
            int high = 1200 + live * 160;
            for (int i = 0; i < labels.Count; i++)
            {
                hours[i] = rng.Next(low, high);
                total += hours[i];
            }
            Array.Sort(hours);
            Array.Reverse(hours);
            for (int i = 0; i < labels.Count; i++)
            {
                s.Labor.Add(new PopulationLaborRow
                {
                    Label = labels[i],
                    HoursText = FmtHours(hours[i]),
                    // 柱顶短数值（与 Provider `FormatCompactHours` 同形：≥1000 走 "1.8k"）。
                    ShortText = FmtCompactHours(hours[i]),
                    Share01 = total > 0L ? hours[i] / (float)total : 0f,
                    // 占全殖民地占比（详细列表「占比」列）——与 Provider 同形（"P1"）。
                    ShareText = total > 0L ? (hours[i] / (float)total).ToString("P1", Inv) : string.Empty
                });
            }
            // 分母 = 有工时记录的现役成员（1..live）⇒ 人均口径与 Provider 完全一致。
            s.LaborContributors = rng.Next(1, live + 1);
            s.LaborTotalText = FmtHours(total);
            s.LaborPerCapitaText = FmtHours(total / (double)s.LaborContributors);
            long top3 = labels.Count >= 3 ? hours[0] + hours[1] + hours[2] : total;
            s.LaborTopThreeText = total > 0L ? FmtPercent(top3 / (float)total) : string.Empty;
        }

        /// <summary>
        /// ⑥ 人员绩效：按**产出市值**排行（与 Provider 同口径 —— 不做银币折算）。
        ///
        /// <para>每行的 8 季桶由「该行总量」随机切分 ⇒ 三列派生完全自洽：
        /// <c>Σ桶 == 行总量</c>、<c>平均季产 = 总量 ÷ 参与季数</c>、<c>峰值季 = 桶内最高（⇒ ≥ 平均季产）</c>、
        /// <c>参与季数 = 非零桶数（≤ 8）</c>。行序 = 产出市值降序（与 Provider 一样是排行榜）。</para>
        /// </summary>
        private static void FillPerformance(PopulationOverviewSnapshot s, System.Random rng, int live)
        {
            List<string> names = DefLabels<PawnKindDef>(6, "PersonalChronicle.Dev.SamplePawn");
            int rows = names.Count;
            if (rows <= 0)
            {
                return;
            }
            long[] values = new long[rows];
            long[][] buckets = new long[rows][];
            int[] active = new int[rows];
            for (int i = 0; i < rows; i++)
            {
                values[i] = rng.Next(400, 5000) + (long)live * 120L;
                buckets[i] = SplitTotal(rng, values[i], TrendBucketCount, out active[i]);
            }
            long total = 0L;
            for (int i = 0; i < rows; i++)
            {
                total += values[i];
            }
            int[] order = SortedDescendingIndex(values);
            long top = values[order[0]];
            for (int r = 0; r < rows; r++)
            {
                int i = order[r];
                long peak = 0L;
                for (int q = 0; q < buckets[i].Length; q++)
                {
                    if (buckets[i][q] > peak)
                    {
                        peak = buckets[i][q];
                    }
                }
                long avg = active[i] > 0 ? values[i] / active[i] : 0L;
                // 工时先取出复用（同一 rng 调用次数不变 ⇒ **随机序列与既有铺满检查结果一致**）。
                int hours = rng.Next(200, 1900);
                s.Performance.Add(new PopulationRankRow
                {
                    Label = names[i],
                    ValueText = values[i].ToString("N0", Inv),
                    NoteText = FmtHours(hours) + " h",
                    Share01 = top > 0L ? Mathf.Clamp01(values[i] / (float)top) : 0f,
                    // 占**全体**的占比（详细列表「占比」列；与 Share01 的「/ 最大值」口径不同）。
                    ShareText = total > 0L ? FmtPercent(values[i] / (float)total) : string.Empty,
                    // ⚠ **数值列必须与文本一起填**（2026-09-17）：UI 的**排序键**只读它们、绝不解析文本。
                    //   漏填 ⇒ 全为 0 ⇒ 表头点击「有箭标但行序不动」（用户实测报的正是这条）。
                    Value = values[i],
                    Hours = hours,
                    // 季桶派生三列（与 Provider FillQuadrumDerived 同形、同自洽关系）。
                    AvgQuadrumText = FmtCompact(avg),
                    PeakQuadrumText = FmtCompact(peak),
                    QuadrumCountText = "PersonalChronicle.UI.MasterControl.Population.Table.QuadrumCountValue"
                        .Translate((NamedArgument)active[i], (NamedArgument)TrendBucketCount).ToString(),
                    AvgQuadrum = avg,
                    PeakQuadrum = peak,
                    QuadrumCount = active[i]
                });
            }
            s.PerformanceTotalText = total.ToString("N0", Inv);
            // 人均产出（分母 = 有产出的成员数，与排行同口径；预览 ⑥ 合计行的中间项）。
            s.PerformancePerCapitaText = (total / (float)rows).ToString("N0", Inv);
            long top3 = 0L;
            for (int i = 0; i < rows && i < 3; i++)
            {
                top3 += values[order[i]];
            }
            s.PerformanceTop3ShareText = total > 0L ? FmtPercent(top3 / (float)total) : string.Empty;
            FillPerformanceTrend(s, names, order, buckets, rows);
        }

        /// <summary>
        /// dev：累计贡献趋势 = Top 3 行各自 8 季桶的**前缀和**（末点 = 该行总量 ⇒ 与排行数字对得上）。
        /// </summary>
        private static void FillPerformanceTrend(
            PopulationOverviewSnapshot s, List<string> names, int[] order, long[][] buckets, int rows)
        {
            int seriesCount = Mathf.Min(3, rows);
            if (seriesCount <= 0)
            {
                return;
            }
            float[][] series = new float[seriesCount][];
            string[] trendNames = new string[seriesCount];
            float cap = 0f;
            for (int p = 0; p < seriesCount; p++)
            {
                int i = order[p];
                float[] cumulative = new float[TrendBucketCount];
                long running = 0L;
                for (int q = 0; q < TrendBucketCount; q++)
                {
                    running += buckets[i][q];
                    cumulative[q] = running;
                }
                series[p] = cumulative;
                trendNames[p] = names[i];
                if (running > cap)
                {
                    cap = running;
                }
            }
            s.PerformanceTrendNames = trendNames;
            s.PerformanceTrendSeries = series;
            s.PerformanceTrendCapText = FmtCompact(cap);
            // x 轴 4 刻度：与 Provider 同一套 Keyed 模板与算法。
            s.PerformanceTrendAxisLabels = BuildQuadrumAxisLabels(PerformanceTrendAxisSlots);
        }

        // ------------------------------------------------------------------ 文本与几何工具

        /// <summary>占比 = 计数 / 总数（构建期已 clamp 到 0..1；分母 ≤ 0 记 0）。</summary>
        private static float Share(int count, int total)
        {
            return total > 0 ? Mathf.Clamp01(count / (float)total) : 0f;
        }

        /// <summary>「N / 总数」计数对（与真实路径同形的计数对文本）。</summary>
        private static string CountPair(int count, int total)
        {
            return count.ToString(Inv) + " / " + total.ToString(Inv);
        }

        /// <summary>百分比文本（F1 + <b>无空格</b> = 与 Provider <c>FormatPercent</c> 逐字同形，避免 "31.9 %"）。</summary>
        private static string FmtPercent(float share01)
        {
            return (share01 * 100f).ToString("F1", Inv) + "%";
        }

        /// <summary>工时文本（N1 = 与 Provider <c>FormatHours</c> 同形）。</summary>
        private static string FmtHours(double hours)
        {
            return hours.ToString("N1", Inv);
        }

        /// <summary>柱顶短数值（≥1000 走 "2.4k" = 与 Provider <c>FormatCompactHours</c> 同形）。</summary>
        private static string FmtCompactHours(double hours)
        {
            return hours >= 1000.0
                ? (hours / 1000.0).ToString("F1", Inv) + "k"
                : hours.ToString("N0", Inv);
        }

        /// <summary>紧凑数值（≥1000 走 "2.4K" = 与 Provider <c>FormatCompact</c> 同形）。</summary>
        private static string FmtCompact(double value)
        {
            return value >= 1000.0
                ? (value / 1000.0).ToString("F1", Inv) + "K"
                : value.ToString("N0", Inv);
        }

        /// <summary>
        /// 把 <paramref name="total"/> 随机切成 <paramref name="slots"/> 个**非负**桶
        /// （权重可 0 ⇒ 该季「未参与」），余数归权重最大桶（**不**破坏 0 权重桶的语义）；
        /// 返回时 <c>Σ桶 == total</c>，<paramref name="nonzero"/> = 非零桶数。
        /// </summary>
        private static long[] SplitTotal(System.Random rng, long total, int slots, out int nonzero)
        {
            int n = Mathf.Max(1, slots);
            long[] buckets = new long[n];
            nonzero = 0;
            if (total <= 0L)
            {
                return buckets;
            }
            int[] weights = new int[n];
            int weightSum = 0;
            int heaviest = 0;
            for (int i = 0; i < n; i++)
            {
                int w = rng.Next(0, 4);          // 0..3；0 = 该季无产出
                weights[i] = w;
                weightSum += w;
                if (w > weights[heaviest])
                {
                    heaviest = i;
                }
            }
            if (weightSum <= 0)
            {
                weights[heaviest] = 1;           // 全 0 ⇒ 保底：至少一季有产出
                weightSum = 1;
            }
            long assigned = 0L;
            for (int i = 0; i < n; i++)
            {
                buckets[i] = total * weights[i] / weightSum;
                assigned += buckets[i];
            }
            buckets[heaviest] += total - assigned;
            for (int i = 0; i < n; i++)
            {
                if (buckets[i] > 0L)
                {
                    nonzero++;
                }
            }
            return buckets;
        }

        /// <summary>切成 <paramref name="slots"/> 个非负整数（Σ == total，**保序**：用于「档位」这类有序格）。</summary>
        private static int[] SplitInts(System.Random rng, int total, int slots)
        {
            int ignored;
            long[] values = SplitTotal(rng, Mathf.Max(0, total), slots, out ignored);
            int[] result = new int[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                result[i] = (int)values[i];
            }
            return result;
        }

        /// <summary>同 <see cref="SplitInts"/>，但按值**降序**返回（用于「榜 / 分布」形态）。</summary>
        private static int[] SplitDescending(System.Random rng, int total, int slots)
        {
            int[] result = SplitInts(rng, total, slots);
            Array.Sort(result);
            Array.Reverse(result);
            return result;
        }

        /// <summary>把 <paramref name="total"/> 按 <paramref name="caps"/> 逐格**带帽**切分（Σ == total，每格 ≤ cap）。</summary>
        private static int[] SplitCapped(System.Random rng, int total, int[] caps)
        {
            int n = caps != null ? caps.Length : 0;
            int[] result = new int[n];
            if (n == 0)
            {
                return result;
            }
            int remaining = Mathf.Clamp(total, 0, Sum(caps));
            for (int i = 0; i < n; i++)
            {
                int room = Mathf.Min(caps[i], remaining);
                int take = room > 0 ? rng.Next(0, room + 1) : 0;
                result[i] = take;
                remaining -= take;
            }
            // 随机偏小可能留下余量 ⇒ 前向回填（Σcap ≥ total 保证一定放得下）。
            for (int i = 0; i < n && remaining > 0; i++)
            {
                int room = caps[i] - result[i];
                if (room <= 0)
                {
                    continue;
                }
                int add = Mathf.Min(room, remaining);
                result[i] += add;
                remaining -= add;
            }
            return result;
        }

        /// <summary><paramref name="count"/> 个「0..<paramref name="max"/>」随机计数，按值降序（榜单形态）。</summary>
        private static int[] RandomCountsDescending(System.Random rng, int count, int max)
        {
            int n = Mathf.Max(1, count);
            int cap = Mathf.Max(0, max);
            int[] result = new int[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = cap > 0 ? rng.Next(0, cap + 1) : 0;
            }
            Array.Sort(result);
            Array.Reverse(result);
            return result;
        }

        /// <summary>按值降序返回下标（榜行序 = Provider 的「按产出市值降序」）。</summary>
        private static int[] SortedDescendingIndex(long[] values)
        {
            int[] order = new int[values.Length];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }
            Array.Sort(order, (a, b) => values[b].CompareTo(values[a]));
            return order;
        }

        /// <summary>构造「单调不减、**末点 = <paramref name="final"/>**」的 8 段存量趋势（回推 = 逐季有人加入）。</summary>
        private static float[] CumulativeEndingAt(System.Random rng, int final)
        {
            float[] series = new float[TrendBucketCount];
            int value = Mathf.Max(0, final);
            int step = Mathf.Max(1, value / 5 + 1);
            for (int i = TrendBucketCount - 1; i >= 0; i--)
            {
                series[i] = value;
                value -= rng.Next(0, step + 1);
                if (value < 0)
                {
                    value = 0;
                }
            }
            return series;
        }

        /// <summary>把一条趋势并入 [min, max] 量程（四舍五入到整数人数）。</summary>
        private static void AccumulateRange(float[] series, ref int min, ref int max)
        {
            if (series == null)
            {
                return;
            }
            for (int i = 0; i < series.Length; i++)
            {
                int value = Mathf.RoundToInt(series[i]);
                if (value < min)
                {
                    min = value;
                }
                if (value > max)
                {
                    max = value;
                }
            }
        }

        /// <summary>当前**绝对季度序号**（tick / 每季 tick）；无 tick 管理器或每季 tick 为 0 时返回 -1。</summary>
        private static long CurrentQuadrumIndex()
        {
            TickManager ticks = Find.TickManager;
            return ticks != null && GenDate.TicksPerQuadrum > 0
                ? ticks.TicksAbs / GenDate.TicksPerQuadrum
                : -1L;
        }

        /// <summary>
        /// x 轴季度标签（段索引 → 绝对季度 → 年 + 季名；构建期格式化，绘制期零翻译）。
        /// 与 Provider 的 <c>BuildQuadrumAxisLabels</c> **同一套 Keyed 模板与算法**（段 7 = 当前季）；
        /// 无时钟的理论路径回落固定样本窗口，避免出现负年份。
        /// </summary>
        private static string[] BuildQuadrumAxisLabels(int[] slots)
        {
            long current = CurrentQuadrumIndex();
            string[] labels = new string[slots.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int year;
                string seasonName;
                if (current >= 0L)
                {
                    long quadrum = current - (TrendBucketCount - 1 - slots[i]);
                    int season = (int)(((quadrum % 4) + 4) % 4);
                    seasonName = ("PersonalChronicle.UI.MasterControl.Population.Performance.Quadrum."
                        + season).Translate().ToString();
                    year = GenDate.Year(quadrum * GenDate.TicksPerQuadrum, 0f);
                }
                else
                {
                    year = 5501 + slots[i] / 4;
                    seasonName = ("PersonalChronicle.UI.MasterControl.Population.Performance.Quadrum."
                        + (slots[i] % 4)).Translate().ToString();
                }
                labels[i] = "PersonalChronicle.UI.MasterControl.Population.Performance.AxisLabel"
                    .Translate((NamedArgument)year, (NamedArgument)seasonName).ToString();
            }
            return labels;
        }

        /// <summary>
        /// 「科技层级 → 数量」汇总成一行（最多 <paramref name="max"/> 项，按数量降序；
        /// 分隔符与书写顺序全走 Keyed）—— 与 Provider <c>TechSummaryText</c> 逐字同形。
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
                    (NamedArgument)rows[i].Value.ToString(Inv)).ToString();
                text = string.IsNullOrEmpty(text)
                    ? item
                    : "PersonalChronicle.UI.MasterControl.Population.PairDot".Translate(
                        (NamedArgument)text, (NamedArgument)item).ToString();
            }
            return text;
        }

        private static void AddDerived(PopulationOverviewSnapshot s, string key, string value, float share01)
        {
            s.Derived.Add(new PopulationCountRow
            {
                Label = ("PersonalChronicle.UI.MasterControl.Population.Derived." + key).Translate().ToString(),
                ValueText = value,
                Share01 = share01
            });
        }

        /// <summary>
        /// **分布榜**（与 Provider <c>FillDistribution</c> 同形，用于理念 / 职称 / 方向）：
        /// 占比 = 计数 / **总和**（<c>Share01</c> 与 <c>ShareText</c> 同源），行值 = Keyed <c>PairDot</c>。
        /// </summary>
        private static void AddDistributionRows(
            List<PopulationCountRow> target, List<string> labels, int[] counts)
        {
            if (target == null || labels == null || counts == null)
            {
                return;
            }
            int total = Sum(counts);
            int n = Mathf.Min(labels.Count, counts.Length);
            for (int i = 0; i < n; i++)
            {
                float share = total > 0 ? Mathf.Clamp01(counts[i] / (float)total) : 0f;
                string shareText = FmtPercent(share);
                target.Add(new PopulationCountRow
                {
                    Label = labels[i],
                    Count = counts[i],
                    ShareText = shareText,
                    Share01 = share,
                    ValueText = "PersonalChronicle.UI.MasterControl.Population.PairDot"
                        .Translate((NamedArgument)counts[i], (NamedArgument)shareText).ToString()
                });
            }
        }

        /// <summary>
        /// **榜**（与 Provider <c>FillTopRows</c> 同形，用于戒律榜 / 勋章榜）：<c>Share01</c> = 计数 /
        /// **最大值**（只作条宽），<b><c>ShareText</c> 留空</b>（Provider 不产出该列），行值 = 计数原文。
        /// </summary>
        private static void AddTopRows(List<PopulationCountRow> target, List<string> labels, int[] counts)
        {
            if (target == null || labels == null || counts == null)
            {
                return;
            }
            int max = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > max)
                {
                    max = counts[i];
                }
            }
            int n = Mathf.Min(labels.Count, counts.Length);
            for (int i = 0; i < n; i++)
            {
                target.Add(new PopulationCountRow
                {
                    Label = labels[i],
                    Count = counts[i],
                    ValueText = counts[i].ToString(Inv),
                    Share01 = max > 0 ? counts[i] / (float)max : 0f
                });
            }
        }

        private static int Sum(int[] values)
        {
            int total = 0;
            for (int i = 0; i < values.Length; i++)
            {
                total += values[i];
            }
            return total;
        }

        /// <summary>技能梯队一行（占比 = 该档 / <c>SkillTierTotal</c>，与 Provider <c>AddSkillTier</c> 同形）。</summary>
        private static void AddTier(PopulationOverviewSnapshot s, string labelKey, int count)
        {
            float share = Share(count, s.SkillTierTotal);
            string shareText = FmtPercent(share);
            s.SkillTiers.Add(new PopulationCountRow
            {
                Label = labelKey.Translate().ToString(),
                Count = count,
                ShareText = shareText,
                Share01 = share,
                ValueText = "PersonalChronicle.UI.MasterControl.Population.PairDot".Translate(
                    (NamedArgument)count, (NamedArgument)shareText).ToString()
            });
        }

        /// <summary>取 Def 显示名（不足则用 Keyed 样例占位；绝不返回空串）。</summary>
        private static List<string> DefLabels<T>(int count, string fallbackKey) where T : Def
        {
            List<string> labels = new List<string>(count);
            List<T> defs = DefDatabase<T>.AllDefsListForReading;
            for (int i = 0; i < count; i++)
            {
                string label = null;
                if (defs != null && i < defs.Count && defs[i] != null)
                {
                    label = defs[i].LabelCap.ToString();
                }
                if (string.IsNullOrEmpty(label))
                {
                    label = fallbackKey.Translate((NamedArgument)(i + 1)).ToString();
                }
                labels.Add(label);
            }
            return labels;
        }
    }
}
