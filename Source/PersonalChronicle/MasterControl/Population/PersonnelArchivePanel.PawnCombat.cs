using PersonalChronicle.Archive;
using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>
    /// Partial of PersonnelArchivePanel — PawnDetail 的绘制分片（BUG-BASE-01 重构）。
    /// </summary>
    /// <remarks>
    /// · 职责：绘制 pawn 与武器详情的若干分区——死亡档案、派系图鉴、打造记录、持有物品、
    ///   武器概览 KPI、技能条、健康与伤情、关系列表。
    /// · 调用方：Shell / Details 的分区调度（详情页切换分区时进入本文件的方法）。
    /// · 铁律（绘制态配对，勿改）：
    ///   - 任何改动 GUI.color / Text.Font / Text.Anchor 的绘制方法，都必须在进入时保存旧值、
    ///     并在 finally 中原样恢复。漏恢复会污染后续所有 UI（v4.17 体检已修过 3 处此类泄漏）。
    ///   - 绘制路径只消费 cached* 的 Read Model 快照，禁止在此重新派生、排序或做业务判定。
    /// · 约定：
    ///   - 文案一律走 Translate()，键前缀 PersonalChronicle.UI.*；禁止硬编码中文。
    ///   - 尺寸与颜色一律走 UITheme / UIComponents / ArchiveUiStyle 令牌，不得直接 new Color 或写魔法数。
    ///   - 无数据时必须给出明确的空态文案，而不是留白或直接 return 什么都不画。
    /// · 注意：文件名沿用 PawnCombat，但实际也承载武器、技能、健康与关系分区的绘制，勿被文件名误导。
    /// </remarks>
    public sealed partial class PersonnelArchivePanel
    {

        /// <summary>
        /// 统计战斗 KPI：参战次数与击杀数。
        /// </summary>
        /// <param name="battleCount">输出战役行数。</param>
        /// <param name="killCount">输出击杀行数。</param>
        /// <remarks>直接读缓存列表的长度，不做重新派生——绘制路径禁止反推业务数据。</remarks>
        private void CountCombatKpis(out int battleCount, out int killCount)
        {
            battleCount = cachedBattleLines != null ? cachedBattleLines.Count : 0;
            killCount = cachedKillLines != null ? cachedKillLines.Count : 0;
        }

        /// <summary>
        /// 统计关联对象中属于 pawn 分类的数量。
        /// </summary>
        /// <returns>关联的 pawn 数。</returns>
        /// <remarks>主要用作无关系快照时的降级人数，见 CountSignificantRelations。</remarks>
        private int CountLinkedPawns()
        {
            int n = 0;
            for (int i = 0; i < cachedLinkedObjects.Count; i++)
            {
                if (!string.IsNullOrEmpty(cachedLinkedObjects[i].StableId)

                    && cachedLinkedObjects[i].CategoryKey == ArchiveCategoryKeys.Pawn)
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// 统计有效（IsActive）的重要关系数。
        /// </summary>
        /// <param name="pawn">pawn 归档对象；可为 null。</param>
        /// <returns>有效关系数；无快照或全部非活跃时，降级为关联 pawn 数。</returns>
        /// <remarks>降级分支是有意的：早期存档没有关系快照，
        /// 此时以"事件共现过的 pawn 数"作为近似值，比直接显示 0 更有信息量。</remarks>
        private int CountSignificantRelations(PawnObject pawn)
        {
            if (pawn != null && pawn.Relations != null)
            {
                int n = 0;
                for (int i = 0; i < pawn.Relations.Count; i++)
                {
                    SignificantRelation r = pawn.Relations[i];
                    if (r != null && r.IsActive)
                    {
                        n++;
                    }
                }
                if (n > 0)
                {
                    return n;
                }
            }
            // Fall back to event-co-occurrence people when no snapshots yet.
            return CountLinkedPawns();
        }

        /// <summary>
        /// 绘制战斗履历子页（预览 #v-pawn t3，HTML 1698-1797）：整页 <c>1fr 1fr</c> 双栏 ——
        /// 左栏 = 阵亡档案（<c>IsArchived</c> 门控）→ 个人战斗累计 → 战功趋势；
        /// 右栏 = 阵营图鉴 → 说明。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="service">归档服务。</param>
        /// <remarks>
        /// 双栏写法照抄 <see cref="DrawPawnOverview"/>：
        /// <c>colW = Mathf.Max(160f, (rect.width - UITheme.GridGap) * 0.5f)</c>，两栏各自从自己的 y 起画。
        /// 本页双栏之下没有整宽内容（总览页的勋章墙），因此不需要求两栏最大高、也无需把区域高回传
        /// —— 这是与总览页唯一的有意差异。
        /// </remarks>
        private void DrawPawnCombat(Rect rect, IArchiveService service)
        {
            float colGap = UITheme.GridGap;
            float colW = Mathf.Max(160f, (rect.width - colGap) * 0.5f);
            Rect leftCol = new Rect(rect.x, rect.y, colW, 0f);
            Rect rightCol = new Rect(rect.x + colW + colGap, rect.y,
                Mathf.Max(160f, rect.width - colW - colGap), 0f);

            float leftBottom = DrawPawnDeathDossier(leftCol);                                // 预览 1699-1711
            leftBottom = DrawPawnCombatTotals(leftCol, leftBottom + UITheme.BlockGap);        // 预览 1712-1721
            DrawPawnCombatTrend(leftCol, leftBottom + UITheme.BlockGap, service);             // 预览 1723-1749

            float rightBottom = DrawPawnCombatCodex(rightCol, service);                       // 预览 1751-1787
            DrawPawnCombatNotes(rightCol, rightBottom + UITheme.BlockGap);                    // 预览 1788-1795
        }

        /// <summary>
        /// 阵亡档案（预览 t3 HTML 1701-1710）：逝世时间 / 死亡原因 / 凶手 / 关联战役。
        /// </summary>
        /// <param name="rect">左栏区域。</param>
        /// <returns>绘制后的 y。</returns>
        /// <remarks>
        /// 整块受 <c>pawn.IsArchived</c> 门控：存活 pawn 没有死亡日期与死因，必须整块跳过
        /// （预览左侧示例人物在世，该区块实机不绘制，仅示意字段）。
        /// "关联战役"行只在死亡事件挂了 battle 边（cachedDeathBattleLabel 非空）时才显示。
        /// </remarks>
        private float DrawPawnDeathDossier(Rect rect)
        {
            float y = rect.y;
            if (cachedDetailObject is PawnObject pawn && pawn.IsArchived)
            {
                DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.DeathDossier".Translate().ToString());
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.DeathDate".Translate().ToString(), FormatDate(pawn.DeathTick));
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.DeathCause".Translate().ToString(), CauseLabel(pawn.DeathCauseKey));
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Killer".Translate().ToString(),
                    string.IsNullOrEmpty(cachedDeathKiller) ? "PersonalChronicle.UI.UnknownDate".Translate().ToString() : cachedDeathKiller);
                // v4.14: 关联战役（死亡事件挂 battle 边时显示）。
                if (!string.IsNullOrEmpty(cachedDeathBattleLabel))
                {
                    y = DrawDetailRow(rect.x, y, rect.width,
                        "PersonalChronicle.UI.DeathBattle".Translate().ToString(),
                        cachedDeathBattleLabel);
                }
                y += 10f;
            }
            return y;
        }

        /// <summary>
        /// 个人战斗累计（预览 t3 HTML 1712-1721）：3 格统计（累计击杀 / 参战战役 / 生涯伤害）
        /// + 2 行明细（近战 / 远程击杀 · 近战击杀占比）。
        /// </summary>
        /// <param name="rect">左栏区域。</param>
        /// <param name="y">起始 y。</param>
        /// <returns>绘制后的 y。</returns>
        /// <remarks>
        /// 数据源全部是**已缓存的持久化累计字段**，绘制路径不重算：
        /// 累计击杀 / 参战战役 走 <see cref="CountCombatKpis"/>（cachedKillLines / cachedBattleLines 长度），
        /// 生涯伤害与近战 / 远程击杀走 <see cref="PawnObject.DamageDealtTotal"/> /
        /// <see cref="PawnObject.MeleeKills"/> / <see cref="PawnObject.RangedKills"/>。
        /// 3 格复用健康页的 <c>.stat3 .statc</c> 渲染器（<see cref="DrawHealthStatCell"/>，格高 46），
        /// 避免在同一 partial 类里再写一份等价组件。
        /// 占比在无任何击杀时显示 "—" 而不是快照里的 0.5 中性占位 —— 0 击杀画 50% 会误导（LAND-009）。
        /// </remarks>
        private float DrawPawnCombatTotals(Rect rect, float y)
        {
            if (!(cachedDetailObject is PawnObject pawn))
            {
                return y;
            }

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Combat.Totals".Translate().ToString());

            CountCombatKpis(out int battleCount, out int killCount);
            float gap = UITheme.GridGap;
            float cellW = (rect.width - gap * 2f) / 3f;
            float cellH = CombatStatCellHeight;
            DrawHealthStatCell(new Rect(rect.x, y, cellW, cellH),
                "PersonalChronicle.UI.Combat.TotalKills".Translate().ToString(),
                killCount.ToString(), UITheme.Dead);
            DrawHealthStatCell(new Rect(rect.x + cellW + gap, y, cellW, cellH),
                "PersonalChronicle.UI.Kpi.BattlesFought".Translate().ToString(),
                battleCount.ToString(), UITheme.TimelineBattle);
            DrawHealthStatCell(new Rect(rect.x + (cellW + gap) * 2f, y, cellW, cellH),
                "PersonalChronicle.UI.Kpi.DamageDealt".Translate().ToString(),
                Mathf.RoundToInt(pawn.DamageDealtTotal).ToString("N0"), UITheme.Info);
            y += cellH + gap;

            int melee = pawn.MeleeKills;
            int ranged = pawn.RangedKills;
            int meleeRangedTotal = melee + ranged;
            y = DrawDetailRow(rect.x, y, rect.width,
                "PersonalChronicle.UI.Combat.MeleeRangedKills".Translate().ToString(),
                melee.ToString() + " / " + ranged.ToString());
            string ratio = meleeRangedTotal > 0
                ? Mathf.RoundToInt(100f * melee / meleeRangedTotal).ToString() + "%"
                : "—";
            y = DrawDetailRow(rect.x, y, rect.width,
                "PersonalChronicle.UI.Combat.MeleeRatio".Translate().ToString(), ratio);
            return y;
        }

        /// <summary>
        /// 战功趋势（预览 t3 HTML 1723-1749）：累计击杀曲线 + 口径说明。
        /// </summary>
        /// <param name="rect">左栏区域。</param>
        /// <param name="y">起始 y。</param>
        /// <param name="service">归档服务（仅供趋势序列的 revision 守卫读取数据版本）。</param>
        /// <returns>绘制后的 y。</returns>
        /// <remarks>
        /// <b>阶梯语义（2026-09-16 已落地真阶梯）</b>：预览是 <c>Charts.Step</c>（离散事件不宜画折线）。
        /// RUIF **无 StepChart**，而 RUIF 是硬依赖 + 禁自研组件（GOV-007）⇒ 不改 RUIF、不自研组件，
        /// 改用**重复采样**（`s0,s0,s1,s1,…`）+ 现有 `LineChart`：顺序连线即「水平段 + 垂直跳变」，
        /// 与 `Charts.Step` 同形。采样在构建期完成（<see cref="BuildCombatTrendSeries"/>）。
        /// 无逐期序列（新档 / 窗口内零击杀 / RUIF 不可用）时画空槽 + 如实空态，绝不编造曲线（LAND-009）。
        /// </remarks>
        private float DrawPawnCombatTrend(Rect rect, float y, IArchiveService service)
        {
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Combat.Trend".Translate().ToString());

            int totalKills = cachedKillLines != null ? cachedKillLines.Count : 0;
            Rect summary = new Rect(rect.x, y, rect.width, UITheme.FontBodyLineHeight);
            float sx = DrawChartSummaryValue(summary, summary.x,
                "PersonalChronicle.UI.Combat.TotalKills".Translate().ToString(), UITheme.Dim);
            DrawChartSummaryValue(summary, sx, totalKills.ToString("N0"), UITheme.Dead);

            // 容器底色先画，内容后画（RUIF 图表与标签都叠在其上）。
            const float plotH = 52f;
            Rect box = new Rect(rect.x, summary.yMax + UITheme.SpaceXxs, rect.width, plotH + 12f);
            Widgets.DrawBoxSolid(box, UITheme.Window);
            UIComponents.Border(box, UITheme.BorderSoft);
            Rect plot = new Rect(box.x + 6f, box.y + 6f, Mathf.Max(8f, box.width - 12f), plotH);

            float[] series = CombatTrendSeries(service);
            // 【阶梯语义】预览 t3 是 `Charts.Step`（离散事件不宜画折线）。RUIF **无 StepChart**，
            // 且 RUIF 是硬依赖 + 禁自研组件（GOV-007）⇒ 不改 RUIF、不加自研图表，
            // 改用**重复采样 + `LineChart`**：序列已按 (s0,s0,s1,s1,…) 排布 ⇒ 顺序连线即
            // 「水平段 + 垂直跳变」= 与 `Charts.Step` 同形的真阶梯；`dots=false` 避免重复点显形。
            bool chartDrawn = cachedCombatTrendHasData
                && series != null
                && series.Length >= 2
                && RuifBridge.TryLineChart(plot, series, false, true);
            if (!chartDrawn)
            {
                UIComponents.Label(plot, "—", GameFont.Small, UITheme.Dim, TextAnchor.MiddleCenter);
            }

            Rect caption = new Rect(rect.x, box.yMax + 2f, rect.width, 18f);
            UIComponents.Label(caption,
                "PersonalChronicle.UI.Combat.Trend.Caption".Translate().ToString(),
                GameFont.Tiny, UITheme.Dim, TextAnchor.UpperLeft);
            Rect scope = new Rect(rect.x, caption.yMax, rect.width, 18f);
            UIComponents.Label(scope,
                (cachedCombatTrendHasData
                    ? "PersonalChronicle.UI.Combat.Trend.Scope"
                    : "PersonalChronicle.UI.Combat.Trend.None").Translate().ToString(),
                GameFont.Tiny, UITheme.Dim, TextAnchor.UpperLeft);
            return scope.yMax;
        }

        /// <summary>阵营图鉴分组（预览 t3 HTML 1751-1787）：分组标题 + 图鉴卡列表。</summary>
        /// <param name="rect">右栏区域。</param>
        /// <param name="service">归档服务。</param>
        /// <returns>绘制后的 y。</returns>
        private float DrawPawnCombatCodex(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.FactionCodexTitle".Translate().ToString());
            return DrawFactionCodex(rect, y, service);
        }

        /// <summary>
        /// 说明（预览 t3 HTML 1788-1795）：一组 <see cref="GameFont.Tiny"/> 说明文字，如实写明
        /// 本页的数据口径与未接入项。
        /// </summary>
        /// <param name="rect">右栏区域。</param>
        /// <param name="y">起始 y。</param>
        /// <returns>绘制后的 y。</returns>
        /// <remarks>
        /// 正文是一段（可含换行）的说明文案，必须允许自动换行 ⇒ 这里不经过
        /// <see cref="UIComponents.Label"/>（它会把 <c>Text.WordWrap</c> 钉死为 false），
        /// 而是直接 <c>Widgets.Label</c> 并在 finally 里原样恢复 WordWrap / Font / Anchor / GUI.color
        /// （与 <c>DrawManualContent</c> 同款收尾，铁律 2）。
        /// </remarks>
        private float DrawPawnCombatNotes(Rect rect, float y)
        {
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Combat.Notes.Title".Translate().ToString());

            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            bool prevWrap = Text.WordWrap;
            try
            {
                GUI.color = UITheme.Dim;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                Widgets.Label(new Rect(rect.x, y, rect.width, CombatNotesBodyHeight),
                    "PersonalChronicle.UI.Combat.Notes".Translate().ToString());
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
                Text.WordWrap = prevWrap;
            }
            return y + CombatNotesBodyHeight;
        }

        /// <summary>个人战斗累计 3 格统计的格高（预览 <c>.stat3 .statc</c> 实测；与健康页同款渲染器）。</summary>
        private const float CombatStatCellHeight = 46f;

        /// <summary>说明正文的预留高度（6 × Tiny 行高 18 · 行高是字体度量，铁律 1）。</summary>
        private const float CombatNotesBodyHeight = 108f;

        // ---- 战功趋势序列（累计击杀 · 近 8 季滚动桶）----------------------------------
        // 绘制路径零查询 / 零遍历 / 零 new（铁律 3）：序列只在「数据版本变化 或 击杀缓存实例被
        // 重建」时构建一次（与 Battle.cs CasualtyLines 同构的 revision 守卫）；绘制只消费 float[]。
        /// <summary>
        /// 战功趋势序列 —— **阶梯采样**（长度 = 2 × 桶数；每个累计样本重复一次）。
        /// 见 <see cref="BuildCombatTrendSeries"/> 的「重复采样」说明。
        /// </summary>
        private float[] cachedCombatTrendSeries = System.Array.Empty<float>();
        private bool cachedCombatTrendHasData;
        private List<CombatLineView> cachedCombatTrendSource;
        private long cachedCombatTrendRevision = -1L;

        /// <summary>取战功趋势序列（命中缓存则零成本返回）。</summary>
        private float[] CombatTrendSeries(IArchiveService service)
        {
            long revision = service != null ? service.GetDataRevision() : 0L;
            // 双重守卫：revision 覆盖「同一对象的数据增量」，缓存实例身份覆盖「detail 对象切换 /
            // 节流刷新重建」—— ScanCombatData 每次都 new List，身份变化即代表旧序列已过期。
            if (cachedCombatTrendRevision == revision
                && ReferenceEquals(cachedCombatTrendSource, cachedKillLines))
            {
                return cachedCombatTrendSeries;
            }
            cachedCombatTrendRevision = revision;
            cachedCombatTrendSource = cachedKillLines;
            BuildCombatTrendSeries();
            return cachedCombatTrendSeries;
        }

        /// <summary>
        /// 把 <c>cachedKillLines</c> 按季（<see cref="GenDate.TicksPerQuadrum"/>）分桶为
        /// 「近 <c>QuadrumBuckets.Slots</c> 季的<b>累计</b>击杀」序列 —— 口径与总览「产出 · 工时」
        /// 的 8 季滚动窗一致，单调不减（与预览的累计阶梯同语义）。
        /// 窗口外的击杀不计入：图即「近 8 季」口径，由口径行如实说明。
        /// </summary>
        private void BuildCombatTrendSeries()
        {
            int slots = QuadrumBuckets.Slots;
            float[] series = new float[slots];
            if (cachedKillLines != null && cachedKillLines.Count > 0 && GenDate.TicksPerQuadrum > 0)
            {
                long currentQuadrum = GenTicks.TicksAbs / GenDate.TicksPerQuadrum;
                long firstQuadrum = currentQuadrum - (slots - 1);
                for (int i = 0; i < cachedKillLines.Count; i++)
                {
                    ChronicleEvent ev = cachedKillLines[i].TargetEvent;
                    // ev.Tick 是绝对 tick（FormatDate 直接喂 GenDate.DateReadoutStringAt），
                    // 与 GenTicks.TicksAbs 同一时间轴，可直接与季宽相除。
                    if (ev == null || ev.Tick < 0L)
                    {
                        continue;
                    }
                    long index = ev.Tick / GenDate.TicksPerQuadrum - firstQuadrum;
                    if (index < 0L || index >= slots)
                    {
                        continue;
                    }
                    series[index] += 1f;
                }
                for (int i = 1; i < slots; i++)
                {
                    series[i] += series[i - 1];
                }
            }

            // 【阶梯采样】把累计序列转成**真阶梯**入参：每个样本重复一次
            // （`s0,s0,s1,s1,…`）⇒ `LineChart` 顺序连线即「水平段 + 垂直跳变」，
            // 与预览 `Charts.Step` 同形（离散事件不应画成跨期斜线）。
            // ⚠ 转换只在构建期做一次（绘制期零分配、零遍历 · 铁律 3 / PERF-001）。
            float[] step = new float[slots * 2];
            for (int i = 0; i < slots; i++)
            {
                step[i * 2] = series[i];
                step[i * 2 + 1] = series[i];
            }
            cachedCombatTrendSeries = step;
            cachedCombatTrendHasData = false;
            for (int i = 0; i < slots; i++)
            {
                if (series[i] > 0f)
                {
                    cachedCombatTrendHasData = true;
                    break;
                }
            }
        }

        /// <summary>
        /// 绘制打造记录分区：制造者、打造时间，以及完整时间轴。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="service">归档服务；本方法当前未直接使用，保留以统一分区方法签名。</param>
        /// <remarks>
        /// 无打造记录时画 NoCraftRecord 空态，但仍继续绘制时间轴，以保证分区高度稳定不跳动。
        /// 绘制态在 finally 中恢复：空态分支会把 Text.Font 改成 Small，不恢复会泄漏到后续分区。
        /// </remarks>
        private void DrawWeaponCraft(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            try
            {
            float y = rect.y;
            if (!string.IsNullOrEmpty(cachedCraftCrafterId))
            {
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Crafter".Translate().ToString(),
                    string.IsNullOrEmpty(cachedCraftCrafterLabel) ? cachedCraftCrafterId : cachedCraftCrafterLabel);
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.CraftedAt".Translate().ToString(), FormatDate(cachedCraftTick));
                y += 10f;
            }
            else
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoCraftRecord".Translate().ToString());
                y += 28f;
            }

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Timeline".Translate().ToString());
            for (int i = 0; i < cachedDetailEvents.Count; i++)
            {
                EventLineView line = cachedDetailEvents[i];
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                DrawEventRow(row, line.DateText, line.NameText, line.ParamsText);
                y += TimelineRowHeight;
            }
            }
            finally
            {
                // v4.17 体检：恢复字体（空态分支 Text.Font=Small 泄漏）。
                GUI.color = prevColor;
                Text.Font = prevFont;
            }
        }

        /// <summary>
        /// 绘制持有物品分区：只列出 Thing 分类的关联对象。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="service">归档服务，用于行点击后的导航跳转。</param>
        /// <remarks>
        /// 行内会临时切换字体与颜色绘制次要信息，随后立即还原为 prevColor / Small；
        /// 整块再由 finally 兜底恢复一次，双重保险防止绘制态泄漏。
        /// 只有 link.Target 不是 None 的行才挂可点击热区，避免死链接。
        /// </remarks>
        private void DrawPawnItems(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            try
            {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.HeldItems".Translate().ToString());

            int count = 0;
            for (int i = 0; i < cachedLinkedObjects.Count; i++)
            {
                LinkedObjectView link = cachedLinkedObjects[i];
                if (link.CategoryKey != ArchiveCategoryKeys.Thing)
                {
                    continue;
                }
                count++;
                Rect row = new Rect(rect.x, y, rect.width, RowHeight - 4f);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x + 6f, row.y + 4f, row.width - 200f, 22f), link.Label);
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.SecondaryText;
                Widgets.Label(new Rect(row.x + row.width - 196f, row.y + 6f, 190f, 18f), link.CategoryLabel);
                GUI.color = prevColor;
                Text.Font = GameFont.Small;
                if (link.Target != NavTarget.None && Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, link.Target, link.StableId, null);
                }
                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
                y += RowHeight + 2f;
            }

            if (count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoRelated".Translate().ToString());
            }
            }
            finally
            {
                // v4.17 体检：恢复字体（空态/行内 Text.Font 泄漏）。
                GUI.color = prevColor;
                Text.Font = prevFont;
            }
        }

        /// <summary>
        /// 绘制武器概览分区：四格 KPI，加类型/制造者详情与最近 4 条时间轴。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="service">归档服务。</param>
        /// <remarks>
        /// KPI 数值全部来自缓存的 Read Model 视图，绘制路径不重新统计。
        /// 时间轴刻意只取末尾 4 条（start 为 Count 减 4）：概览分区高度有限，
        /// 完整列表在打造记录分区中展示。改动这里的截断逻辑前先确认分区高度预算。
        /// 详情对象不是 ThingObject 时直接 return，调用方需保证进入前已切换正确的对象。
        /// </remarks>
        private void DrawWeaponOverview(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            if (!(cachedDetailObject is ThingObject thing))
            {
                return;
            }

            // v4.14: KPI 4 cells (events / kills / holders / crafter) matching the
            // preview's weapon Overview. Counters come from the cached Read-Model
            // views (never re-derived in the draw path).
            float kpiH = UIComponents.StatCellMinHeight;
            float gap = UITheme.GridGap;
            float kpiW = (rect.width - UITheme.CardPadX * 2f - gap * 3f) / 4f;
            float kpiX = rect.x + UITheme.CardPadX;
            string eventsValue = cachedDetailEvents.Count.ToString();
            string killsValue = cachedKillLines.Count.ToString();
            string holdersValue = cachedLegacy != null ? cachedLegacy.GenCount.ToString() : "—";
            string crafterValue = string.IsNullOrEmpty(cachedCraftCrafterLabel)
                ? (string.IsNullOrEmpty(cachedCraftCrafterId) ? "—" : cachedCraftCrafterId)
                : cachedCraftCrafterLabel;
            UIComponents.StatCell(new Rect(kpiX, y, kpiW, kpiH),
                "PersonalChronicle.UI.KpiEvents".Translate().ToString(), eventsValue);
            UIComponents.StatCell(new Rect(kpiX + (kpiW + gap), y, kpiW, kpiH),
                "PersonalChronicle.UI.KpiKills".Translate().ToString(), killsValue);
            UIComponents.StatCell(new Rect(kpiX + 2f * (kpiW + gap), y, kpiW, kpiH),
                "PersonalChronicle.UI.KpiHolders".Translate().ToString(), holdersValue);
            UIComponents.StatCell(new Rect(kpiX + 3f * (kpiW + gap), y, kpiW, kpiH),
                "PersonalChronicle.UI.KpiCrafter".Translate().ToString(), crafterValue);
            y += kpiH + UITheme.BlockGap;

            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Type".Translate().ToString(), ThingDefLabel(thing.ThingDefName));
            if (!string.IsNullOrEmpty(cachedCraftCrafterId))
            {
                string crafter = string.IsNullOrEmpty(cachedCraftCrafterLabel) ? cachedCraftCrafterId : cachedCraftCrafterLabel;
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Crafter".Translate().ToString(), crafter);
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.CraftedAt".Translate().ToString(), FormatDate(cachedCraftTick));
            }
            else
            {
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Crafter".Translate().ToString(),
                    "PersonalChronicle.UI.NoCraftRecord".Translate().ToString());
            }
            y += 10f;

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Timeline".Translate().ToString());
            int start = Mathf.Max(0, cachedDetailEvents.Count - 4);
            for (int i = start; i < cachedDetailEvents.Count; i++)
            {
                EventLineView line = cachedDetailEvents[i];
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                DrawEventRow(row, line.DateText, line.NameText, line.ParamsText);
                y += TimelineRowHeight;
            }
            if (cachedDetailEvents.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoEvents".Translate().ToString());
            }
        }

        /// <summary>
        /// 绘制技能分区：技能名、等级进度条与数值。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="service">归档服务，用于取存活 pawn。</param>
        /// <remarks>
        /// 依赖存活 pawn 的实时技能数据，因此已归档（死亡或离场）的 pawn 走 DrawNoLiveData 空态，
        /// 而不是绘制归档快照——技能快照属于"技能档案"分区的职责，两者不要混用。
        /// 进度条按 20 级满级归一化（skill.Level / 20f），与 RimWorld 的技能等级上限一致。
        /// </remarks>
        private void DrawSkills(Rect rect, IArchiveService service)
        {
            Pawn pawn = service.GetLivePawn(detailObjectId);
            if (pawn == null || pawn.skills == null || pawn.skills.skills == null)
            {
                DrawNoLiveData(rect);
                return;
            }

            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Skills".Translate().ToString());

            List<SkillRecord> skills = pawn.skills.skills;
            for (int i = 0; i < skills.Count; i++)
            {
                SkillRecord skill = skills[i];
                if (skill == null || skill.def == null)
                {
                    continue;
                }
                Rect row = new Rect(rect.x, y, rect.width, 24f);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x, row.y, 140f, 20f), skill.def.label);

                Rect bar = new Rect(row.x + 148f, row.y + 4f, row.width - 148f - 56f, 14f);
                Widgets.FillableBar(bar, Mathf.Clamp01(skill.Level / 20f));

                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x + row.width - 52f, row.y, 48f, 20f), skill.Level.ToString());
                y += 28f;
            }
        }

        /// <summary>
        /// 绘制健康分区：总体状态标签与伤情（Hediff）列表。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="service">归档服务，用于取存活 pawn。</param>
        /// <remarks>
        /// 状态由 SummaryHealthPercent 与两个阈值常量比较得出（良好 / 受伤 / 危急），
        /// 阈值常量在本类字段中定义，勿在此写魔法数。
        /// 本方法同时保存并恢复 GUI.color、Text.Font、Text.Anchor 三项绘制态，
        /// 是本文件改动绘制态最多的方法，缺恢复任何一项都会污染后续分区。
        /// </remarks>
        private void DrawHealth(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
            Pawn pawn = service.GetLivePawn(detailObjectId);
            if (pawn == null || pawn.health == null)
            {
                DrawNoLiveData(rect);
                return;
            }

            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.OverallHealth".Translate().ToString());

            float summary = pawn.health.summaryHealth != null ? pawn.health.summaryHealth.SummaryHealthPercent : 1f;
            string healthLabel;
            if (summary > HealthGoodThreshold)
            {
                healthLabel = "PersonalChronicle.UI.HealthGood".Translate().ToString();
            }
            else if (summary > HealthInjuredThreshold)
            {
                healthLabel = "PersonalChronicle.UI.HealthInjured".Translate().ToString();
            }
            else
            {
                healthLabel = "PersonalChronicle.UI.HealthCritical".Translate().ToString();
            }
            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Status".Translate().ToString(), healthLabel);
            y += 8f;

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Hediffs".Translate().ToString());

            List<Hediff> hediffs = pawn.health.hediffSet != null ? pawn.health.hediffSet.hediffs : null;
            if (hediffs == null || hediffs.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoHediffs".Translate().ToString());
                return;
            }

            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff == null)
                {
                    continue;
                }
                string label = hediff.LabelBase;
                if (string.IsNullOrEmpty(label) && hediff.def != null)
                {
                    label = hediff.def.label;
                }
                if (string.IsNullOrEmpty(label))
                {
                    continue;
                }
                string part = string.Empty;
                if (hediff.Part != null && hediff.Part.def != null)
                {
                    part = hediff.Part.def.label;
                }
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x + 4f, row.y + 3f, row.width - 200f, 20f), label);
                Text.Font = GameFont.Tiny;
                GUI.color = UITheme.SecondaryText;
                Widgets.Label(new Rect(row.x + row.width - 196f, row.y + 6f, 192f, 18f), part);
                GUI.color = prevColor;
                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
                y += TimelineRowHeight;
            }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        /// <summary>
        /// 绘制关系分区：他人、关系名、状态三列。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="service">归档服务。</param>
        /// <remarks>行数据由 BuildRelationRows 统一装配，本方法只负责布局与空态，
        /// 不要在绘制循环里补充过滤或排序逻辑。</remarks>
        private void DrawRelations(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Relations".Translate().ToString());

            List<RelationRowView> rows = BuildRelationRows(service);
            if (rows.Count == 0)
            {
                UIComponents.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoRelations".Translate(), UITheme.FontBody, UITheme.Text);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                RelationRowView row = rows[i];
                Rect rowRect = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                UIComponents.Label(new Rect(rowRect.x + 4f, rowRect.y + 3f, rowRect.width - 260f, 20f),
                    row.OtherLabel, UITheme.FontBody, UITheme.Text);
                UIComponents.Label(new Rect(rowRect.x + rowRect.width - 256f, rowRect.y + 6f, 180f, 18f),
                    row.RelationLabel, UITheme.FontLabel, UITheme.SecondaryText);
                UIComponents.Label(new Rect(rowRect.x + rowRect.width - 72f, rowRect.y + 6f, 68f, 18f),
                    row.StatusLabel, UITheme.FontLabel, UITheme.SecondaryText);
                Widgets.DrawLineHorizontal(rowRect.x, rowRect.yMax, rowRect.width);
                y += TimelineRowHeight;
            }
        }

        /// <summary>
        /// 装配关系行数据：先取存活 pawn 的实时关系，再补归档关系。
        /// </summary>
        /// <param name="service">归档服务。</param>
        /// <returns>去重后的关系行列表；无关系时返回空列表（不是 null）。</returns>
        /// <remarks>
        /// 两段式取值：实时关系反映当前状态，归档关系保留已死亡或离场者的历史纽带，两者缺一不可。
        /// 跨两段用 MakeRelationKey 配 HashSet 去重，避免同一对关系出现两行。
        /// 社交噪音由 SocialRelationFilter.IsSignificant 过滤，只保留有意义的关系。
        /// </remarks>
        private List<RelationRowView> BuildRelationRows(IArchiveService service)
        {
            List<RelationRowView> rows = new List<RelationRowView>();
            HashSet<string> seen = new HashSet<string>();
            PawnObject record = service.GetObject(detailObjectId) as PawnObject;
            Pawn livePawn = service.GetLivePawn(detailObjectId);

            // 1) Live relations (current state).
            if (livePawn?.relations?.DirectRelations != null)
            {
                List<DirectPawnRelation> directRelations = livePawn.relations.DirectRelations;
                for (int i = 0; i < directRelations.Count; i++)
                {
                    DirectPawnRelation rel = directRelations[i];
                    if (rel?.def == null || rel.otherPawn == null)
                    {
                        continue;
                    }
                    if (!SocialRelationFilter.IsSignificant(rel.def))
                    {
                        continue;
                    }
                    string otherId = rel.otherPawn.GetUniqueLoadID();
                    string key = MakeRelationKey(rel.def.defName, otherId);
                    if (!seen.Add(key))
                    {
                        continue;
                    }
                    rows.Add(new RelationRowView
                    {
                        OtherLabel = rel.otherPawn.LabelShort,
                        RelationLabel = RelationLabelFor(rel.def, rel.otherPawn),
                        StatusLabel = rel.otherPawn.Dead
                            ? "PersonalChronicle.UI.Dead".Translate().ToString()
                            : "PersonalChronicle.UI.Alive".Translate().ToString()
                    });
                }
            }

            // 2) Archived relations (historical / initial ties, includes dead/departed pawns).
            if (record?.Relations != null)
            {
                for (int i = 0; i < record.Relations.Count; i++)
                {
                    SignificantRelation rel = record.Relations[i];
                    if (rel == null)
                    {
                        continue;
                    }
                    string key = MakeRelationKey(rel.RelationDefName, rel.OtherStableId);
                    if (!seen.Add(key))
                    {
                        continue;
                    }
                    Pawn otherLive = service.GetLivePawn(rel.OtherStableId);
                    bool otherDead = otherLive != null && otherLive.Dead;
                    string status = otherDead
                        ? "PersonalChronicle.UI.Dead".Translate().ToString()
                        : (rel.IsActive
                            ? "PersonalChronicle.UI.Alive".Translate().ToString()
                            : "PersonalChronicle.UI.RelEnded".Translate().ToString());
                    PawnRelationDef def = DefDatabase<PawnRelationDef>.GetNamedSilentFail(rel.RelationDefName);
                    rows.Add(new RelationRowView
                    {
                        OtherLabel = !string.IsNullOrEmpty(rel.OtherLabel) ? rel.OtherLabel : rel.OtherStableId,
                        RelationLabel = RelationLabelFor(def, otherLive),
                        StatusLabel = status
                    });
                }
            }

            return rows;
        }

        /// <summary>
        /// 生成关系去重键，格式为"关系 def 名::对方 stableId"。
        /// </summary>
        /// <param name="relationDefName">关系 defName。</param>
        /// <param name="otherStableId">对方 pawn 稳定 ID。</param>
        /// <returns>去重键。</returns>
        /// <remarks>两侧都做了 null 归一化为 empty，保证 null 与空串不会生成两个不同的键。</remarks>
        private static string MakeRelationKey(string relationDefName, string otherStableId)
        {
            return (relationDefName ?? string.Empty) + "::" + (otherStableId ?? string.Empty);
        }

        /// <summary>
        /// 取关系的显示名，优先按对方性别取性别化标签。
        /// </summary>
        /// <param name="def">关系定义；为 null 时返回空串。</param>
        /// <param name="otherPawn">对方 pawn；为 null 时跳过性别化分支。</param>
        /// <returns>性别化标签优先，其次 def.label，最后退到 defName。</returns>
        /// <remarks>对方已离场或查不到 pawn 时拿不到性别，此时自然降级到通用标签，属预期行为。</remarks>
        private static string RelationLabelFor(PawnRelationDef def, Pawn otherPawn)
        {
            if (def == null)
            {
                return string.Empty;
            }
            if (otherPawn != null)
            {
                string gendered = def.GetGenderSpecificLabel(otherPawn);
                if (!string.IsNullOrEmpty(gendered))
                {
                    return gendered;
                }
            }
            if (!string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            return def.defName;
        }


    }
}
