using PersonalChronicle.Archive;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>
    /// Partial of <see cref="PersonnelArchivePanel"/> ——嵌入档的**统计带**与**四分类卡阵列**
    /// （预览 `#v-archive` 的 ① ② 两层，HTML 1032-1078）。
    ///
    /// <para><b>诚实性（LAND-009）</b>：预览的 4 张统计卡里「事件总量」与「四段时限带」都需要
    /// 全量事件扫描 / 历史分期口径（工程无 O(1) 事件计数，热路径禁用 <c>GetAllEvents</c>）⇒
    /// 本实现只画**有口径的 4 项**（在册 / 已归档 / 档案跨度 / 档案对象），
    /// 预览的时限带**不画**并登记为缺口，绝不填样例数值。</para>
    ///
    /// <para><b>几何（预览 CSS）</b>：`.ovkpi{padding:6px 10px;border-left:2px solid accent}`(291) ·
    /// `.ovcard{padding:8px 10px;gap:5px}`(299) · `.ovcard .big{font-size:23px}`(306) ·
    /// `.ovcard .stack{height:7px}`(310) · `.ovcard .lg{font-size:9px}`(312)。</para>
    /// </summary>
    public sealed partial class PersonnelArchivePanel
    {
        /// <summary>统计带高：卡 47（`.ovkpi` = padding 6×2 + 标签 18 + 数值 22 ≈ 52，取 47 与预览卡高一致）。</summary>
        /// <summary>
        /// ① 统计带 4 卡卡高 —— **49**（预览实测 `.ovkpi` **49.4**；`padding:6px 10px`）。
        /// 旧值 47 比预览矮 2.4px。
        /// </summary>
        private const float EmbeddedStatStripHeight = 49f;

        /// <summary>`.ovkpis{gap:9px}` / `.ovcards{gap:9px}`。</summary>
        private const float EmbeddedCardGap = 9f;

        /// <summary>时段条区块高 = 标签行 16 + 条 9（预览 `.ovtime{column;gap:9px}` + `.tb{height:9px}`）。</summary>
        /// <summary>
        /// 时段分布块高 —— **18**（预览 `.ovtime` 实测**单行 13.8**；实机按 Tiny 行高下限取 18）。
        /// 旧值 25 是「标签行 16 + 条 9」两行的合计 —— 形态与预览不符（预览是同行）。
        /// </summary>
        private const float EmbeddedTimelineBlockHeight = 18f;

        /// <summary>时段条本身高（预览 `.tb{height:9px}`）。</summary>
        private const float EmbeddedTimelineBarHeight = 9f;

        /// <summary>`.ovcard` 卡高 = padding 8×2 + 标题行 18 + 大数字 30 + 堆叠条 7 + 图例 18 ≈ 92。</summary>
        private const float EmbeddedCategoryCardHeight = 92f;

        /// <summary>`.ovcard .stack{height:7px}`。</summary>
        private const float EmbeddedStackBarHeight = 7f;

        // ---- ① 档案统计带（.ovstat / .ovkpis）---------------------------------

        private float DrawEmbeddedStats(Rect rect, IArchiveService service)
        {
            // 取数规则（PERF P0 修复）：全部走 **120 tick 节流缓存**（RebuildPawnCounts）。
            // 旧实现每帧调 GetActiveSnapshotCount / GetArchivedSnapshotCount（各 O(N) 全对象
            // 扫描 + 新 List）与 GetServiceDays（内部 GetAllEvents 全量复制）—— 60fps 下每秒
            // 复制上百 KB。绘制路径必须零遍历、零复制（PERF-001/002）。
            int active = cachedActivePawnCount;
            int archived = cachedArchivedPawnCount;
            int days = cachedServiceDays;
            // 事件总量 = O(1) 计数（Data 层维护 nextEventId），是唯一允许每帧取的服务值。
            int events = service != null ? service.GetEventCount() : 0;

            float cardWidth = (rect.width - EmbeddedCardGap * 3f) * 0.25f;
            // 卡片高 = EmbeddedStatStripHeight（47）—— 不是 rect.height（= 47 + 时段条 25 = 72）：
            // 传 rect.height 会让卡片下沿压住时段条（UI P0 修复）。
            float cardHeight = EmbeddedStatStripHeight;

            // v5.x（对照清单 P3）：预览 `.ovkpi .s` 角标子标签（HTML 1125-1128）——
            // 口径全部来自已有缓存字段，不新增查询。
            int freeColonists;
            int slaveColonists;
            int prisonerColonists;
            service.GetLiveColonistCounts(out freeColonists, out slaveColonists, out prisonerColonists);
            int pawnTotal = active + archived;
            string activeSub = "PersonalChronicle.UI.Archive.Kpi.Sub.Active".Translate(
                (NamedArgument)freeColonists, (NamedArgument)slaveColonists,
                (NamedArgument)prisonerColonists).ToString();
            string archivedSub = pawnTotal > 0
                ? "PersonalChronicle.UI.Archive.Kpi.Sub.Archived".Translate(
                    (NamedArgument)Mathf.RoundToInt(archived * 100f / pawnTotal)).ToString()
                : string.Empty;
            string spanSub = "PersonalChronicle.UI.Archive.Kpi.Sub.SpanUnit".Translate().ToString();
            int objectTotal = CountAllCategoryObjects();
            string eventsSub = objectTotal > 0
                ? "PersonalChronicle.UI.Archive.Kpi.Sub.Events".Translate(
                    (NamedArgument)(events / (float)objectTotal).ToString("0")).ToString()
                : string.Empty;

            DrawEmbeddedStatCard(new Rect(rect.x, rect.y, cardWidth, cardHeight),
                "PersonalChronicle.UI.MasterControl.Kpi.Archive.Active".Translate().ToString(),
                active.ToString(), UITheme.Accent, activeSub);
            DrawEmbeddedStatCard(new Rect(rect.x + cardWidth + EmbeddedCardGap, rect.y, cardWidth, cardHeight),
                "PersonalChronicle.UI.MasterControl.Kpi.Archive.Archived".Translate().ToString(),
                archived.ToString(), UITheme.Muted, archivedSub);
            DrawEmbeddedStatCard(new Rect(rect.x + (cardWidth + EmbeddedCardGap) * 2f, rect.y, cardWidth, cardHeight),
                "PersonalChronicle.UI.Archive.Stat.Span".Translate().ToString(),
                (days / (float)GenDate.DaysPerYear).ToString("F1"), UITheme.Info, spanSub);
            DrawEmbeddedStatCard(new Rect(rect.x + (cardWidth + EmbeddedCardGap) * 3f, rect.y, cardWidth, cardHeight),
                "PersonalChronicle.UI.Archive.Stat.Events".Translate().ToString(),
                events.ToString(), UITheme.Dim, eventsSub);

            DrawEmbeddedTimeline(new Rect(rect.x, rect.y + EmbeddedStatStripHeight, rect.width,
                EmbeddedTimelineBlockHeight), service);
            return rect.yMax;
        }

        // ---- 档案时段分布（预览 `.ovtime` 的可算等价）----------------------------
        //
        // 预览那四段是**叙事分期**（落地 / 开拓 / 战事 / 当下）—— 工程没有对应史实划分，
        // 用「入册时间等分」替代会改变语义 ⇒ 本实现按**入库时间跨度四分**如实呈现，
        // 并把口径写进标签（不冒充叙事分期；LAND-009）。

        private float[] embeddedTimelineShares;
        private int embeddedTimelineSamples;
        private long embeddedTimelineRevision = -1L;

        /// <summary>时段条左端季标签（缓存期格式化，如「5492 春」）。</summary>
        private string embeddedTimelineStartText = string.Empty;

        /// <summary>时段条右端季标签（当前季）。</summary>
        private string embeddedTimelineEndText = string.Empty;

        /// <summary>
        /// 时段分布条（预览 `.ovtime`）—— **单行**：`[首季] [条] [末季] [标签]`（实测行高 13.8、
        /// 条宽 1525 撑满、条高 9）。旧实现是「标签行 + 下方条」**两行**（块高 25）—— 与预览形态不符。
        /// <para>⚠ 缺口如实：预览该行末尾是「落地 2.2y · 开拓 2.9y · 战事 2.6y · 当下 1.6y」
        /// （**四段命名 + 时长**）；本工程四段只是「按对象起算时间四等分」（无业务命名依据）
        /// ⇒ **不编造段名**，右端只画既有标签（`Archive.Timeline.Label`）。</para>
        /// </summary>
        private void DrawEmbeddedTimeline(Rect rect, IArchiveService service)
        {
            EnsureEmbeddedTimeline(service);
            const float rowH = 18f;          // Tiny 行高下限
            const float gap = 8f;
            Text.Font = GameFont.Tiny;
            float startW = string.IsNullOrEmpty(embeddedTimelineStartText)
                ? 0f
                : Text.CalcSize(embeddedTimelineStartText).x + 2f;
            float endW = string.IsNullOrEmpty(embeddedTimelineEndText)
                ? 0f
                : Text.CalcSize(embeddedTimelineEndText).x + 2f;
            string legend = "PersonalChronicle.UI.Archive.Timeline.Label".Translate().ToString();
            float legendW = Mathf.Min(rect.width * 0.34f, Text.CalcSize(legend).x + 2f);
            float barW = Mathf.Max(40f, rect.width - startW - endW - legendW - gap * 3f);

            float textY = rect.y + (rowH - 18f) * 0.5f;
            if (startW > 0f)
            {
                UIComponents.Label(new Rect(rect.x, textY, startW, 18f),
                    UIComponents.TruncateToWidth(embeddedTimelineStartText, startW, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
            }
            Rect bar = new Rect(rect.x + startW + (startW > 0f ? gap : 0f),
                rect.y + (rowH - EmbeddedTimelineBarHeight) * 0.5f, barW, EmbeddedTimelineBarHeight);
            Widgets.DrawBoxSolid(bar, UITheme.BorderSoft);
            if (embeddedTimelineSamples > 0)
            {
                Color[] colors = { UITheme.Muted, UITheme.Info, UITheme.Accent, UITheme.Alive };
                float x = bar.x;
                for (int i = 0; i < 4; i++)
                {
                    float width = i == 3
                        ? Mathf.Max(0f, bar.xMax - x)
                        : bar.width * Mathf.Clamp01(embeddedTimelineShares[i]);
                    if (width > 0.5f)
                    {
                        Widgets.DrawBoxSolid(new Rect(x, bar.y, width, bar.height), colors[i]);
                    }
                    x += width;
                }
            }
            // 悬停给「覆盖 N 个对象 · 左=最老 右=最近」（只在悬停时拼串）。
            if (Mouse.IsOver(bar))
            {
                TooltipHandler.TipRegion(bar,
                    "PersonalChronicle.UI.Archive.Timeline.Caption".Translate(
                        (NamedArgument)embeddedTimelineSamples).ToString());
            }
            if (endW > 0f)
            {
                UIComponents.Label(new Rect(bar.xMax + gap, textY, endW, 18f),
                    UIComponents.TruncateToWidth(embeddedTimelineEndText, endW, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
            }
            if (legendW > 4f)
            {
                UIComponents.Label(new Rect(rect.xMax - legendW, textY, legendW, 18f),
                    UIComponents.TruncateToWidth(legend, legendW, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleRight);
            }
        }

        /// <summary>时段分布按 Archive 自己的数据修订号缓存（与统计带同节奏，不跨模块取数）。</summary>
        private void EnsureEmbeddedTimeline(IArchiveService service)
        {
            long revision = service != null ? service.GetDataRevision() : 0L;
            if (embeddedTimelineRevision == revision && embeddedTimelineShares != null)
            {
                return;
            }
            embeddedTimelineRevision = revision;
            if (embeddedTimelineShares == null)
            {
                embeddedTimelineShares = new float[4];
            }
            embeddedTimelineSamples = 0;
            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            if (now <= 0L || service == null)
            {
                return;
            }
            List<long> ages = new List<long>();
            long maxAge = 0L;
            for (int c = 0; c < CategoryKeys.Length; c++)
            {
                List<ArchiveObject> objects;
                if (!cachedCategoryObjects.TryGetValue(CategoryKeys[c], out objects) || objects == null)
                {
                    continue;
                }
                for (int i = 0; i < objects.Count; i++)
                {
                    long start = SpanStartTickOf(objects[i]);
                    if (start <= 0L || now <= start)
                    {
                        continue;
                    }
                    long age = now - start;
                    ages.Add(age);
                    if (age > maxAge)
                    {
                        maxAge = age;
                    }
                }
            }
            if (ages.Count == 0 || maxAge <= 0L)
            {
                return;
            }
            int[] buckets = new int[4];
            for (int i = 0; i < ages.Count; i++)
            {
                int index = (int)(ages[i] * 4L / maxAge);
                if (index > 3)
                {
                    index = 3;
                }
                buckets[3 - index]++;   // 段 0 = 最老
            }
            for (int i = 0; i < 4; i++)
            {
                embeddedTimelineShares[i] = buckets[i] / (float)ages.Count;
            }
            embeddedTimelineSamples = ages.Count;
            // 时段条两端季标签（预览 `.ovtime` 的「5492 春 … 5501 春」）——**缓存期**格式化，
            // 绘制期零翻译、零 tick 运算（PERF-001）。最老 = now − 最大跨度，最新 = 当前季。
            embeddedTimelineStartText = QuadrumLabelAt(now - maxAge);
            embeddedTimelineEndText = QuadrumLabelAt(now);
        }

        /// <summary>
        /// tick → 「5499 秋」形式的季度标签（复用性能趋势卡的两条既有 Keyed：
        /// `…Population.Performance.AxisLabel`（`{0} {1}`）与 `…Performance.Quadrum.{0..3}`）。
        /// 两者均为**通用季名/年季模板**，非性能卡专属语义 ⇒ 直接复用而不复制键（GOV-010 仍经本地化管道）。
        /// </summary>
        private static string QuadrumLabelAt(long tick)
        {
            if (tick <= 0L || GenDate.TicksPerQuadrum <= 0)
            {
                return string.Empty;
            }
            long quadrum = tick / GenDate.TicksPerQuadrum;
            int season = (int)(((quadrum % 4) + 4) % 4);
            string seasonName = ("PersonalChronicle.UI.MasterControl.Population.Performance.Quadrum." + season)
                .Translate().ToString();
            return "PersonalChronicle.UI.MasterControl.Population.Performance.AxisLabel"
                .Translate((NamedArgument)GenDate.Year(tick, 0f), (NamedArgument)seasonName).ToString();
        }

        /// <summary>对象起算 tick（无该口径返回 -1 ⇒ 不参与统计，不猜、不用 0 冒充最老）。</summary>
        private static long SpanStartTickOf(ArchiveObject obj)
        {
            PawnObject pawn = obj as PawnObject;
            if (pawn != null)
            {
                return pawn.JoinTick;
            }
            ThingObject thing = obj as ThingObject;
            if (thing != null)
            {
                return thing.CreatedTick;
            }
            BattleObject battle = obj as BattleObject;
            if (battle != null)
            {
                return battle.StartTick;
            }
            return -1L;
        }

        /// <summary>
        /// 单张统计卡：左侧 2px 强调竖标 + 标签（Tiny/Dim）+ 数值（Small）+ 角标子标签（Tiny/Muted，
        /// 预览 `.ovkpi .s`；空串不画）。
        /// </summary>
        private static void DrawEmbeddedStatCard(Rect rect, string label, string value, Color accent,
            string sub = null)
        {
            Widgets.DrawBoxSolid(rect, UITheme.Card);
            UIComponents.Border(rect, UITheme.BorderSoft);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y + 1f, 2f, rect.height - 2f), accent);
            float inner = Mathf.Max(0f, rect.width - 18f);
            // ⚠ 行高铁律：Tiny 文本 rect 高度必须 ≥ 18（旧值 16 ⇒ 文字上下溢出被邻行压住）。
            UIComponents.Label(new Rect(rect.x + 9f, rect.y + 5f, inner, 18f),
                UIComponents.TruncateToWidth(label, inner, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, TextAnchor.UpperLeft);
            Text.Font = GameFont.Small;
            float valueW = Text.CalcSize(value).x;
            UIComponents.Label(new Rect(rect.x + 9f, rect.y + 21f, inner, 22f),
                UIComponents.TruncateToWidth(value, inner, GameFont.Small),
                GameFont.Small, UITheme.Text, TextAnchor.UpperLeft);
            if (!string.IsNullOrEmpty(sub))
            {
                float subX = rect.x + 9f + valueW + 5f;
                if (subX < rect.xMax - 4f)
                {
                    UIComponents.Label(new Rect(subX, rect.y + 24f, Mathf.Max(0f, rect.xMax - 4f - subX), 18f),
                        UIComponents.TruncateToWidth(sub, rect.xMax - 4f - subX, GameFont.Tiny),
                        GameFont.Tiny, UITheme.Muted, TextAnchor.UpperLeft);
                }
            }
        }

        private int CountAllCategoryObjects()
        {
            int total = 0;
            for (int i = 0; i < CategoryKeys.Length; i++)
            {
                List<ArchiveObject> objects;
                if (cachedCategoryObjects.TryGetValue(CategoryKeys[i], out objects) && objects != null)
                {
                    total += objects.Count;
                }
            }
            return total;
        }

        // ---- ② 四分类卡阵列（.ovcards）---------------------------------------

        private float DrawEmbeddedCategoryCards(Rect rect, IArchiveService service)
        {
            int count = CategoryKeys.Length;
            if (count <= 0)
            {
                return rect.yMax;
            }
            float cardWidth = (rect.width - EmbeddedCardGap * (count - 1)) * (1f / count);
            int total = CountAllCategoryObjects();
            // 卡内聚合（分段计数 / 图例文本 / 深度徽章）按「数据修订 × 语言」预计算 ——
            // 旧实现每帧对 4 个分类各做一次 O(N) 分项遍历 + GetCategoryBehavior（LINQ）
            // + Translate 拼串（PERF P1 修复）。绘制期只读本缓存。
            EnsureEmbeddedCardAggregates(service, total);
            string current = string.IsNullOrEmpty(overviewCategoryFilter)
                ? ArchiveCategoryKeys.Pawn
                : overviewCategoryFilter;

            for (int i = 0; i < count; i++)
            {
                string key = CategoryKeys[i];
                Rect card = new Rect(rect.x + (cardWidth + EmbeddedCardGap) * i, rect.y, cardWidth, rect.height);
                List<ArchiveObject> objects;
                if (!cachedCategoryObjects.TryGetValue(key, out objects) || objects == null)
                {
                    objects = EmptyObjectList;
                }
                EmbeddedCardAgg agg;
                if (!embeddedCardAggCache.TryGetValue(key, out agg))
                {
                    agg = new EmbeddedCardAgg();
                    agg.Legend = string.Empty;
                    agg.DepthBadge = string.Empty;
                }
                bool selected = string.Equals(current, key, System.StringComparison.Ordinal);
                if (DrawEmbeddedCategoryCard(card, i, key, objects, agg, selected))
                {
                    // 点击 = 切换明细 pane（预览 `data-cat` → `selectCat`）。
                    GoOverview(key);
                }
            }
            return rect.yMax;
        }

        private static readonly List<ArchiveObject> EmptyObjectList = new List<ArchiveObject>();

        /// <summary>分类卡预计算聚合（构建于缓存刷新，绘制期零遍历/零翻译/零拼接）。</summary>
        private struct EmbeddedCardAgg
        {
            public bool HasBreakdown;
            public int Seg1;
            public int Seg2;
            public int Seg3;
            /// <summary>第四段（**仅地点卡使用**：家园 / 派系据点 / 任务点 / 其它）。</summary>
            public int Seg4;
            /// <summary>true = 该卡走 4 段堆叠条（地点专用路径；其余分类仍 3 段）。</summary>
            public bool HasFourthSeg;
            public float Share01;
            public string Legend;
            public string DepthBadge;
        }

        private Dictionary<string, EmbeddedCardAgg> embeddedCardAggCache =
            new Dictionary<string, EmbeddedCardAgg>(System.StringComparer.Ordinal);
        private long embeddedCardAggRevision = -1L;
        private string embeddedCardAggLanguage = string.Empty;

        private void EnsureEmbeddedCardAggregates(IArchiveService service, int total)
        {
            long revision = service != null ? service.GetDataRevision() : 0L;
            string language = LanguageDatabase.activeLanguage != null
                ? LanguageDatabase.activeLanguage.folderName
                : string.Empty;
            if (embeddedCardAggRevision == revision
                && string.Equals(embeddedCardAggLanguage, language, System.StringComparison.Ordinal))
            {
                return;
            }
            embeddedCardAggRevision = revision;
            embeddedCardAggLanguage = language;
            for (int i = 0; i < CategoryKeys.Length; i++)
            {
                string key = CategoryKeys[i];
                List<ArchiveObject> objects;
                if (!cachedCategoryObjects.TryGetValue(key, out objects) || objects == null)
                {
                    objects = EmptyObjectList;
                }
                EmbeddedCardAgg agg = new EmbeddedCardAgg();
                ArchiveDepthBehavior behavior = service != null
                    ? service.GetCategoryBehavior(key)
                    : ArchiveDepthBehavior.Record;
                agg.DepthBadge = DepthBadgeText(behavior);
                if (key == ArchiveCategoryKeys.Location)
                {
                    // 地点走**四段专用路径**（家园 / 派系据点 / 任务点 / 其它）—— 既有 3 段管道不动，
                    // 其余分类仍 3 段（避免为一个分类改签名而牵动全部调用点）。
                    int home;
                    int factionSettlement;
                    int questSite;
                    int other;
                    CountLocationBreakdown4(objects, out home, out factionSettlement, out questSite, out other);
                    agg.HasBreakdown = true;
                    agg.HasFourthSeg = true;
                    agg.Seg1 = home;
                    agg.Seg2 = factionSettlement;
                    agg.Seg3 = questSite;
                    agg.Seg4 = other;
                    agg.Legend = "PersonalChronicle.UI.Archive.Card.LocationBreakdown4".Translate(
                        (NamedArgument)home, (NamedArgument)factionSettlement,
                        (NamedArgument)questSite, (NamedArgument)other).ToString();
                    embeddedCardAggCache[key] = agg;
                    continue;
                }
                int seg1;
                int seg2;
                int seg3;
                if (CountBreakdown(key, objects, out seg1, out seg2, out seg3))
                {
                    agg.HasBreakdown = true;
                    agg.Seg1 = seg1;
                    agg.Seg2 = seg2;
                    agg.Seg3 = seg3;
                    agg.Legend = BreakdownTextKey(key).Translate(
                        (NamedArgument)seg1, (NamedArgument)seg2, (NamedArgument)seg3).ToString();
                }
                else
                {
                    agg.Share01 = total > 0 ? Mathf.Clamp01(objects.Count / (float)total) : 0f;
                    agg.Legend = "PersonalChronicle.UI.Archive.Card.Share".Translate(
                        (NamedArgument)objects.Count, (NamedArgument)total).ToString();
                }
                embeddedCardAggCache[key] = agg;
            }
        }

        /// <summary>单张分类卡；返回是否被点击。</summary>
        private bool DrawEmbeddedCategoryCard(Rect rect, int index, string categoryKey,
            List<ArchiveObject> objects, EmbeddedCardAgg agg, bool selected)
        {
            Widgets.DrawBoxSolid(rect, selected ? UITheme.PanelRaised : UITheme.Card);
            UIComponents.Border(rect, selected ? UITheme.Accent : UITheme.BorderSoft);
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            float padX = 10f;
            float inner = Mathf.Max(0f, rect.width - padX * 2f);
            float y = rect.y + 8f;

            // 标题行：`#01` 编号（mono/dim）+ 名称（12px/800）+ 右侧深度行为徽章
            // 【1:1 实测】预览标题行内高 22（名称 Small 12px / 编号 Tiny 9px / 徽章高 13.9）；
            //   旧实现用 16 高 —— **Small 文本 rect 16 < 行高 22**，文字下半截被大数字行盖住（行高铁律）。
            string number = "#" + (index + 1).ToString("D2");
            UIComponents.Label(new Rect(rect.x + padX, y, 26f, 22f), number,
                GameFont.Tiny, UITheme.Dim, TextAnchor.UpperLeft);
            // 【1:1 实测】预览 `.ovcard .dep` 徽章**按内容宽**（实测 32.1 / 43.3 / 35.7 × 13.9）；
            //   旧实现写死 58 × 15 ⇒ 总是偏宽。此处「内容 + 左右各 6」。
            Text.Font = GameFont.Tiny;
            float depWidth = Mathf.Min(inner * 0.4f, Text.CalcSize(agg.DepthBadge).x + 12f);
            float nameWidth = Mathf.Max(20f, inner - 26f - depWidth - 6f);
            UIComponents.Label(new Rect(rect.x + padX + 26f, y, nameWidth, 22f),
                UIComponents.TruncateToWidth(CategoryLabel(categoryKey), nameWidth, GameFont.Small),
                GameFont.Small, UITheme.Text, TextAnchor.UpperLeft);
            UIComponents.Badge(new Rect(rect.xMax - padX - depWidth, y + 3f, depWidth, 14f),
                agg.DepthBadge, UITheme.Info);
            y += 22f;

            // 大数字（预览 `.big` 23px → Medium 档）+ 单位词（预览 `.ovcard .row .unit`：
            // 人物/物品 = 对象 · 战役 = 场 · 地点 = 处）。块高取 **Medium 行高 30**
            //（旧值 28 < 30 ⇒ 数字底部被堆叠条压住 2px）。
            string count = objects.Count.ToString();
            UIComponents.Label(new Rect(rect.x + padX, y, inner, 30f), count,
                GameFont.Medium, UITheme.Text, TextAnchor.UpperLeft);
            Text.Font = GameFont.Medium;
            float countW = Text.CalcSize(count).x;
            float unitX = rect.x + padX + countW + 6f;
            if (unitX < rect.xMax - padX)
            {
                UIComponents.Label(new Rect(unitX, y + 12f, Mathf.Max(0f, rect.xMax - padX - unitX), 18f),
                    CategoryUnitKey(categoryKey).Translate().ToString(),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.UpperLeft);
            }
            y += 30f;

            // 堆叠条（预览 `.stack`：段宽 = 构成占比，每段一色）+ 图例行（聚合来自预计算缓存）
            Rect stack = new Rect(rect.x + padX, y, inner, EmbeddedStackBarHeight);
            Widgets.DrawBoxSolid(stack, UITheme.BorderSoft);
            if (agg.HasFourthSeg)
            {
                // 地点：**四段**堆叠条（家园 / 派系据点 / 任务点 / 其它）—— 预览 ④ 段等价。
                Widgets.DrawBoxSolid(new Rect(stack.x, stack.y,
                    stack.width * ShareOf(agg.Seg1, agg), stack.height), UITheme.Accent);
                float x4 = stack.x + stack.width * ShareOf(agg.Seg1, agg);
                Widgets.DrawBoxSolid(new Rect(x4, stack.y,
                    stack.width * ShareOf(agg.Seg2, agg), stack.height), UITheme.Info);
                x4 += stack.width * ShareOf(agg.Seg2, agg);
                Widgets.DrawBoxSolid(new Rect(x4, stack.y,
                    stack.width * ShareOf(agg.Seg3, agg), stack.height), UITheme.Alive);
                x4 += stack.width * ShareOf(agg.Seg3, agg);
                Widgets.DrawBoxSolid(new Rect(x4, stack.y, Mathf.Max(0f, stack.xMax - x4), stack.height),
                    UITheme.Dim);
            }
            else if (agg.HasBreakdown)
            {
                // 有真实分项口径的分类 ⇒ 画预览的三段堆叠条（段宽 = 构成占比）。
                Color color1;
                Color color2;
                Color color3;
                BreakdownColors(categoryKey, out color1, out color2, out color3);
                DrawThreeSegmentStack(stack, agg.Seg1, agg.Seg2, agg.Seg3, color1, color2, color3);
            }
            else
            {
                // 无分项口径的分类 ⇒ 单段占比带并如实说明，绝不编造分段（LAND-009）。
                if (agg.Share01 > 0f)
                {
                    Widgets.DrawBoxSolid(new Rect(stack.x, stack.y, stack.width * agg.Share01, stack.height),
                        selected ? UITheme.Accent : UITheme.Muted);
                }
            }
            y += EmbeddedStackBarHeight + 4f;

            // 图例行：预览 `.lg` 实测高 13.9（9px）⇒ 实机按 Tiny 行高下限 18（旧值 16 溢出）。
            UIComponents.Label(new Rect(rect.x + padX, y, inner, 18f),
                UIComponents.TruncateToWidth(agg.Legend, inner, GameFont.Tiny),
                GameFont.Tiny, UITheme.Muted, TextAnchor.UpperLeft);

            return Widgets.ButtonInvisible(rect);
        }

        /// <summary>
        /// 分类的内部构成口径（三段）。**每段都来自真实字段**，无口径的分类返回 false
        /// （调用方退化为单段占比带并如实说明）。
        ///
        /// <list type="bullet">
        /// <item>人物：在世 / 已死亡 / 已归档（`DeathTick` × `ArchiveStatus`）</item>
        /// <item>物品：在用 / 已退役 / 已毁（`Decommission` 是否为 null × `DestroyedTick`）</item>
        /// <item>战役：**威胁等级**分布（`ThreatKey`：ThreatBig / ThreatSmall / 未标注）
        ///        —— 工程没有战役胜负字段，故不按「决胜/惨胜/失利」分（预览那三段无数据支撑）</item>
        /// <item>地点：家园 / 派系据点 / 其它（`IsPlayerHome` × `FactionDefName`）</item>
        /// </list>
        /// </summary>
        private static bool CountBreakdown(string categoryKey, List<ArchiveObject> objects,
            out int seg1, out int seg2, out int seg3)
        {
            seg1 = 0;
            seg2 = 0;
            seg3 = 0;
            if (objects == null)
            {
                return false;
            }
            if (categoryKey == ArchiveCategoryKeys.Pawn)
            {
                CountPawnBreakdown(objects, out seg1, out seg2, out seg3);
                return true;
            }
            for (int i = 0; i < objects.Count; i++)
            {
                if (categoryKey == ArchiveCategoryKeys.Thing)
                {
                    ThingObject thing = objects[i] as ThingObject;
                    if (thing == null)
                    {
                        continue;
                    }
                    if (thing.DestroyedTick > 0L)
                    {
                        seg3++;
                    }
                    else if (thing.Decommission != null)
                    {
                        seg2++;
                    }
                    else
                    {
                        seg1++;
                    }
                }
                else if (categoryKey == ArchiveCategoryKeys.Battle)
                {
                    BattleObject battle = objects[i] as BattleObject;
                    if (battle == null)
                    {
                        continue;
                    }
                    if (battle.ThreatKey == "ThreatBig")
                    {
                        seg1++;
                    }
                    else if (battle.ThreatKey == "ThreatSmall")
                    {
                        seg2++;
                    }
                    else
                    {
                        seg3++;
                    }
                }
                else if (categoryKey == ArchiveCategoryKeys.Location)
                {
                    LocationObject location = objects[i] as LocationObject;
                    if (location == null)
                    {
                        continue;
                    }
                    if (location.IsPlayerHome)
                    {
                        seg1++;
                    }
                    else if (!string.IsNullOrEmpty(location.FactionDefName))
                    {
                        seg2++;
                    }
                    else
                    {
                        seg3++;
                    }
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>分类 → 三段图例的 Keyed 文案（与 <see cref="CountBreakdown"/> 的分段顺序一一对应）。</summary>
        private static string BreakdownTextKey(string categoryKey)
        {
            switch (categoryKey)
            {
                case ArchiveCategoryKeys.Pawn:
                    return "PersonalChronicle.UI.Archive.Card.PawnBreakdown";
                case ArchiveCategoryKeys.Thing:
                    return "PersonalChronicle.UI.Archive.Card.ThingBreakdown";
                case ArchiveCategoryKeys.Battle:
                    return "PersonalChronicle.UI.Archive.Card.BattleBreakdown";
                default:
                    return "PersonalChronicle.UI.Archive.Card.LocationBreakdown";
            }
        }

        /// <summary>段占比（分母 = 四段之和；全 0 时返回 0，绝不除零）。</summary>
        private static float ShareOf(int value, EmbeddedCardAgg agg)
        {
            int total = agg.Seg1 + agg.Seg2 + agg.Seg3 + agg.Seg4;
            return total > 0 ? value / (float)total : 0f;
        }

        /// <summary>
        /// 地点四段（预览 4 段堆叠条等价）：家园 / 派系据点 / 任务点 / 其它。
        /// 判据全部来自**持久化字段**：<c>IsPlayerHome</c> + <c>WorldObjectDefName</c>
        /// （原版 <c>Settlement</c> / <c>Site</c> / <c>Camp</c> 等 WorldObjectDef 名，v4.13 起已快照）。
        /// </summary>
        private static void CountLocationBreakdown4(List<ArchiveObject> objects,
            out int home, out int factionSettlement, out int questSite, out int other)
        {
            home = 0;
            factionSettlement = 0;
            questSite = 0;
            other = 0;
            if (objects == null)
            {
                return;
            }
            for (int i = 0; i < objects.Count; i++)
            {
                LocationObject location = objects[i] as LocationObject;
                if (location == null)
                {
                    continue;
                }
                if (location.IsPlayerHome)
                {
                    home++;
                    continue;
                }
                string worldObject = location.WorldObjectDefName ?? string.Empty;
                if (worldObject.IndexOf("Settlement", System.StringComparison.Ordinal) >= 0)
                {
                    factionSettlement++;
                }
                else if (worldObject.IndexOf("Site", System.StringComparison.Ordinal) >= 0
                    || worldObject.IndexOf("Camp", System.StringComparison.Ordinal) >= 0)
                {
                    questSite++;
                }
                else
                {
                    other++;
                }
            }
        }

        /// <summary>人物分类的内部构成（在世 / 已死亡 / 已归档）——预览人物卡的堆叠条分段口径。</summary>
        private static void CountPawnBreakdown(List<ArchiveObject> objects, out int alive, out int dead, out int archived)
        {
            alive = 0;
            dead = 0;
            archived = 0;
            if (objects == null)
            {
                return;
            }
            for (int i = 0; i < objects.Count; i++)
            {
                PawnObject pawn = objects[i] as PawnObject;
                if (pawn == null)
                {
                    continue;
                }
                if (pawn.DeathTick <= 0L)
                {
                    alive++;
                }
                else if (pawn.ArchiveStatus == PawnArchiveStatus.Active)
                {
                    dead++;
                }
                else
                {
                    archived++;
                }
            }
        }

        /// <summary>
        /// 三段堆叠条（段宽 = 占比；顺序拼接而非绝对定位 —— 引擎无 `left/width` 定位）。
        /// 颜色由调用方给：各分类的段语义不同（在世/阵亡 ≠ 在用/退役），**不能共用一套色阶**。
        /// </summary>
        private static void DrawThreeSegmentStack(Rect stack, int first, int second, int third,
            Color color1, Color color2, Color color3)
        {
            int total = first + second + third;
            if (total <= 0 || stack.width <= 0f)
            {
                return;
            }
            float w1 = stack.width * first / total;
            float w2 = stack.width * second / total;
            float w3 = Mathf.Max(0f, stack.width - w1 - w2);
            if (w1 > 0f)
            {
                Widgets.DrawBoxSolid(new Rect(stack.x, stack.y, w1, stack.height), color1);
            }
            if (w2 > 0f)
            {
                Widgets.DrawBoxSolid(new Rect(stack.x + w1, stack.y, w2, stack.height), color2);
            }
            if (w3 > 0f)
            {
                Widgets.DrawBoxSolid(new Rect(stack.x + w1 + w2, stack.y, w3, stack.height), color3);
            }
        }

        /// <summary>各分类三段堆叠条的配色（语义色，非单色阶 —— 反劣质清单第 5 条）。</summary>
        private static void BreakdownColors(string categoryKey, out Color color1, out Color color2, out Color color3)
        {
            switch (categoryKey)
            {
                case ArchiveCategoryKeys.Thing:
                    color1 = UITheme.Accent;   // 在用
                    color2 = UITheme.Muted;    // 已退役
                    color3 = UITheme.Dead;     // 已毁
                    return;
                case ArchiveCategoryKeys.Battle:
                    color1 = UITheme.Threat;   // 大规模
                    color2 = UITheme.Info;     // 小规模
                    color3 = UITheme.Dim;      // 未标注
                    return;
                case ArchiveCategoryKeys.Location:
                    color1 = UITheme.Accent;   // 家园
                    color2 = UITheme.Info;     // 派系据点
                    color3 = UITheme.Dim;      // 其它
                    return;
                default:
                    color1 = UITheme.Alive;    // 在世
                    color2 = UITheme.Dead;     // 阵亡
                    color3 = UITheme.Dim;      // 归档
                    return;
            }
        }

        /// <summary>分类 → 大数字旁单位词的 Keyed（预览 `.ovcard .unit`，对照清单 P3）。</summary>
        private static string CategoryUnitKey(string categoryKey)
        {
            switch (categoryKey)
            {
                case ArchiveCategoryKeys.Battle:
                    return "PersonalChronicle.UI.Archive.Card.Unit.Battle";
                case ArchiveCategoryKeys.Location:
                    return "PersonalChronicle.UI.Archive.Card.Unit.Location";
                case ArchiveCategoryKeys.Thing:
                    return "PersonalChronicle.UI.Archive.Card.Unit.Thing";
                default:
                    return "PersonalChronicle.UI.Archive.Card.Unit.Pawn";
            }
        }

        /// <summary>深度行为 → 徽章文案（**按行为枚举取值，不看 label**，GOV-009）。</summary>
        private static string DepthBadgeText(ArchiveDepthBehavior behavior)
        {
            switch (behavior)
            {
                case ArchiveDepthBehavior.StatOnly:
                    return "PersonalChronicle.UI.Archive.Depth.StatOnly".Translate().ToString();
                case ArchiveDepthBehavior.Event:
                    return "PersonalChronicle.UI.Archive.Depth.Event".Translate().ToString();
                default:
                    return "PersonalChronicle.UI.Archive.Depth.Record".Translate().ToString();
            }
        }
    }
}
