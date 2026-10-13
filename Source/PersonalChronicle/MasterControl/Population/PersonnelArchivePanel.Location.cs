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
using ReadModels = PersonalChronicle.Application.ReadModels;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>
    /// Partial of <see cref="PersonnelArchivePanel"/> — 地点分区的绘制与地点文案解析。
    /// </summary>
    /// <remarks>
    /// · 职责：绘制地点 KPI 条、地点卡片网格与卡片内联的编年史展开面板；
    ///   并提供地点种类、派系、地理标签、商贸、生命周期等展示文案的解析。
    /// · 调用方：地点分区的绘制调度。
    /// · 铁律：
    ///   - 绘制路径禁止 LINQ 全量排序（PERF-001）。地点事件流按 stableId 缓存，
    ///     只在首次构建时排序一次，并在 DataRevision 变化时整体失效重建（DATA-007）。
    ///     把排序挪回绘制循环会直接退化成逐帧 O(n log n)。
    ///   - 颜色与字体一律走 UITheme / ArchiveUiStyle / UIComponents 令牌，不得直接 new Color。
    ///   - 文案一律走 Translate()，禁止硬编码中文。
    /// · 约定：
    ///   - 内联编年史**恒定绘制在卡片内**（预览 `.lcard` 第 5 层，2026-09-16 方案 A）——
    ///     没有「展开状态」，也没有卡下面板；能画几行由卡内剩余高度实算。
    ///   - 行数上限 6（预览约定），超出部分在说明行标注「共 N 条 · 示 M」。
    ///   - "未知/无数据"统一显示破折号；这与 PawnOverview 的 DrawEmptyLine 不同——
    ///     那里是整段空态，这里是行内占位。
    /// </remarks>
    public sealed partial class PersonnelArchivePanel
    {

        /// <summary>
        /// 绘制地点 KPI 条（固定 8 格）。
        /// </summary>
        /// <param name="viewRect">可视区域，决定每格宽度。</param>
        /// <param name="y">起始纵坐标。</param>
        /// <param name="kpi">KPI 快照；为 null 时返回 0，即不占高度。</param>
        /// <returns>KPI 条的高度。</returns>
        /// <remarks>
        /// 标签、数值、强调色三个数组按下标一一对应，格数固定为 8。
        /// 新增一格必须同时改这三个数组与变量 n，漏改会越界或整条错位。
        /// </remarks>
        private float DrawLocationKpiStrip(Rect viewRect, float y, ReadModels.LocationKpisView kpi)
        {
            if (kpi == null)
            {
                return 0f;
            }
            int n = 8;
            float gap = 6f;
            float cellW = (viewRect.width - gap * (n - 1)) / n;
            string[] labels =
            {
                "PersonalChronicle.UI.LocKpiTotal".Translate().ToString(),
                "PersonalChronicle.UI.LocKpiHome".Translate().ToString(),
                "PersonalChronicle.UI.LocKpiQuest".Translate().ToString(),
                "PersonalChronicle.UI.LocKpiSettle".Translate().ToString(),
                "PersonalChronicle.UI.LocKpiRuined".Translate().ToString(),
                "PersonalChronicle.UI.LocKpiTradable".Translate().ToString(),
                "PersonalChronicle.UI.LocKpiPermit".Translate().ToString(),
                "PersonalChronicle.UI.LocKpiFactions".Translate().ToString()
            };
            int[] values = { kpi.Total, kpi.Home, kpi.Quest, kpi.Settle, kpi.Ruined,
                kpi.Tradable, kpi.Permit, kpi.Factions };
            Color[] accents =
            {
                UITheme.Text, UITheme.Accent, UITheme.Info, UITheme.Info,
                UITheme.Dead, UITheme.Alive, UITheme.Warn, UITheme.Text
            };
            for (int i = 0; i < n; i++)
            {
                Rect cell = new Rect(viewRect.x + i * (cellW + gap), y, cellW, LocationKpiStripHeight);
                // 【1:1 实测】预览 `.kstrip` 单格 **46.4** ⇒ 紧凑档（标签 Tiny + 值 Small），
                // 与战役 KPI 条同源；`StatCell`（Medium 值 · 下限 64）比预览高 39%。
                UIComponents.StatCellCompact(cell, labels[i], values[i].ToString(), accents[i]);
            }
            return LocationKpiStripHeight;
        }

        /// <summary>
        /// 绘制地点卡片网格，返回本区占用的总高度。
        /// </summary>
        /// <param name="viewRect">可视区域。</param>
        /// <param name="startY">起始纵坐标。</param>
        /// <param name="objects">地点对象列表；其中的非 LocationObject 会被跳过。</param>
        /// <param name="gap">卡片间距。</param>
        /// <param name="service">归档服务，供展开面板读取事件。</param>
        /// <returns>本区相对 startY 的占用高度。</returns>
        /// <remarks>
        /// 列数按可视宽度自适应且至少 1 列，坐标用 i % perRow 与 i / perRow 排布。
        /// 点击卡片切换内联编年史展开；展开时面板画在卡片正下方，
        /// yCursor 取"卡片底"与"面板底"的较大值，以保证展开不会与下一行卡片重叠。
        /// </remarks>
        private float DrawLocationOverviewCards(Rect viewRect, float startY, List<ArchiveObject> objects, float gap, IArchiveService service)
        {
            int perRow = Mathf.Max(1, (int)((viewRect.width + gap) / (LocationCardWidth + gap)));
            float yCursor = startY;
            for (int i = 0; i < objects.Count; i++)
            {
                LocationObject loc = objects[i] as LocationObject;
                if (loc == null)
                {
                    continue;
                }
                int col = i % perRow;
                int row = i / perRow;
                float cardTop = startY + row * (LocationCardHeight + gap);
                Rect card = new Rect(
                    viewRect.x + col * (LocationCardWidth + gap),
                    cardTop,
                    LocationCardWidth, LocationCardHeight);

                Color accent = LocationCardAccent(loc);
                ArchiveUiStyle.DrawCard(card, accent);
                float x = card.x + UITheme.CardPadX;
                float w = card.width - UITheme.CardPadX * 2f;
                float y = card.y + UITheme.CardPadY;

                // 1) Category row: kind Pill + faction + ruined corner dot.
                string kindKey = LocationKindKey(loc);
                Color pillColor = kindKey == "player" ? UITheme.Accent
                    : kindKey == "settle" ? UITheme.Info
                    : kindKey == "quest" ? UITheme.Warn : UITheme.Muted;
                float pillW = 54f;
                UIComponents.Badge(new Rect(x, y, pillW, 16f), LocationKindText(loc), pillColor);
                UIComponents.Label(new Rect(x + pillW + 6f, y, w - pillW - 6f, 16f),
                    LocationFactionText(loc), UITheme.FontLabel, ArchiveUiStyle.Muted);
                if (loc.DeinitTick != -1L)
                {
                    UIComponents.Label(new Rect(x + w - 40f, y, 40f, 16f),
                        "PersonalChronicle.UI.LocLifeRuined".Translate().ToString(),
                        UITheme.FontLabel, UITheme.Dead);
                }
                y += 20f;

                // 2) Name.
                UIComponents.Label(new Rect(x, y, w, 20f),
                    ObjectDisplayLabel(loc), UITheme.FontBody, ArchiveUiStyle.Info);
                y += 22f;

                // 3) Sub-line: established · dwell · events (Read-Model counts).
                int evCount = loc.StableId != null && cachedLocationEventCounts != null
                    && cachedLocationEventCounts.TryGetValue(loc.StableId, out int evN) ? evN : 0;
                string sub = "PersonalChronicle.UI.LocSubLine".Translate(
                    LocationEstablishedYearText(loc), evCount).ToString();
                UIComponents.Label(new Rect(x, y, w, 18f), sub, UITheme.FontLabel, ArchiveUiStyle.Muted);
                y += 20f;

                // 4) Geography chips (single wrapped line).
                string geo = LocationGeoText(loc);
                if (!string.IsNullOrEmpty(geo))
                {
                    UIComponents.Label(new Rect(x, y, w, 18f), geo, UITheme.FontLabel, ArchiveUiStyle.Muted);
                    y += 20f;
                }

                // 5) Commerce chip.
                string trade = LocationTradeText(loc);
                if (!string.IsNullOrEmpty(trade))
                {
                    UIComponents.Label(new Rect(x, y, w, 18f), trade, UITheme.FontLabel,
                        loc.CanTrade ? UITheme.Accent : ArchiveUiStyle.Muted);
                    y += 20f;
                }

                // 6) Lifecycle three-cell row (established / status / dwell).
                float cellGap = 4f;
                float cellW = (w - cellGap * 2f) / 3f;
                string est = loc.EstablishedTick > 0L
                    ? GenDate.DateReadoutStringAt(loc.EstablishedTick, UnityEngine.Vector2.zero) : "—";
                string status = loc.DeinitTick != -1L
                    ? LocationDeinitText(loc) : "PersonalChronicle.UI.LocLifeActive".Translate().ToString();
                string dwell = loc.EstablishedTick > 0L
                    ? ReadModels.SpanText.Format(CurrentDwellTicks(loc)) : "—";
                UIComponents.Label(new Rect(x, y, cellW, 16f),
                    "PersonalChronicle.UI.LocLifeEstablished".Translate().ToString(),
                    UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(x, y + 14f, cellW, 18f), est,
                    UITheme.FontLabel, UITheme.Text);
                UIComponents.Label(new Rect(x + cellW + cellGap, y, cellW, 16f),
                    "PersonalChronicle.UI.LocLifeStatus".Translate().ToString(),
                    UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(x + cellW + cellGap, y + 14f, cellW, 18f), status,
                    UITheme.FontLabel,
                    loc.DeinitTick != -1L ? UITheme.Dead : UITheme.Alive);
                UIComponents.Label(new Rect(x + 2f * (cellW + cellGap), y, cellW, 16f),
                    "PersonalChronicle.UI.LocLifeDwell".Translate().ToString(),
                    UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(x + 2f * (cellW + cellGap), y + 14f, cellW, 18f), dwell,
                    UITheme.FontLabel, UITheme.Text);

                // 6b) 卡内**恒定内联**编年史（预览 `.lcard` 第 5 层，HTML 1329-1331：说明行 + `evrow`）。
                //     ⚠ 2026-09-16（方案 A / 1:1）：删除「点击卡片 → 卡下展开面板」交互
                //       （原 `expandedLocations` 状态集），改为**无需点击、恒定内联在卡内**；
                //       卡高 228 已把该区计入（旧值 176 + 卡下面板 = 与预览形态不符）。
                //     生命周期三格的下沿 = y + 32 ⇒ 内联区从 y + 34 起。
                DrawLocationChronicleInline(x, y + 34f, w, card, loc, service);
                yCursor = Mathf.Max(yCursor, cardTop + LocationCardHeight + gap);
            }
            return yCursor - startY + 14f;
        }

        /// <summary>
        /// 判定地点的种类键：<c>player</c> / <c>settle</c> / <c>quest</c> / <c>unknown</c>。
        /// </summary>
        /// <param name="loc">地点对象；为 null 时返回 unknown。</param>
        /// <returns>小写种类键，用于配色与文案的分派。</returns>
        /// <remarks>
        /// 依据 WorldObjectDefName 做子串匹配（忽略大小写）而不是比较完整 Def：
        /// 世界对象 Def 可能来自任意 mod，子串匹配能覆盖命名变体。
        /// 判定顺序（本家 → 聚落 → 任务/站点）不可调换。
        /// </remarks>
        private static string LocationKindKey(LocationObject loc)
        {
            if (loc == null)
            {
                return "unknown";
            }
            if (loc.IsPlayerHome)
            {
                return "player";
            }
            if (!string.IsNullOrEmpty(loc.WorldObjectDefName))
            {
                if (loc.WorldObjectDefName.IndexOf("Settlement", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "settle";
                }
                if (loc.WorldObjectDefName.IndexOf("Quest", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || loc.WorldObjectDefName.IndexOf("Site", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "quest";
                }
            }
            return "unknown";
        }

        /// <summary>
        /// 取地点的建立年份文本。
        /// </summary>
        /// <param name="loc">地点对象。</param>
        /// <returns>年份字符串；未建立或对象为空时返回破折号。</returns>
        /// <remarks>EstablishedTick 小于等于 0 视为"未建立"，口径与 CurrentDwellTicks 保持一致。</remarks>
        private static string LocationEstablishedYearText(LocationObject loc)
        {
            if (loc == null || loc.EstablishedTick <= 0L)
            {
                return "—";
            }
            return GenDate.Year(loc.EstablishedTick, 0f).ToString();
        }

        /// <summary>
        /// 计算地点至今的存续时长（ticks）。
        /// </summary>
        /// <param name="loc">地点对象。</param>
        /// <returns>存续 ticks；未建立、或结束时刻不晚于建立时刻时返回 -1。</returns>
        /// <remarks>结束时刻取 DeinitTick（已关闭）或当前游戏 tick。
        /// -1 是"不适用"哨兵值，调用方据此显示破折号而不是 0。</remarks>
        private static long CurrentDwellTicks(LocationObject loc)
        {
            if (loc == null || loc.EstablishedTick <= 0L)
            {
                return -1L;
            }
            long end = loc.DeinitTick > 0L ? loc.DeinitTick
                : (Find.TickManager != null ? Find.TickManager.TicksGame : 0L);
            return end > loc.EstablishedTick ? (end - loc.EstablishedTick) : -1L;
        }

        // v4.14+: 地点编年史按 stableId 缓存（绘制路径禁止 LINQ 全量排序，PERF-001；
        // revision 变化时整体失效，由权威数据重建，符合 DATA-007）。
        /// <summary>
        /// 地点编年史缓存：按地点 stableId 缓存已升序排好的事件流。
        /// </summary>
        /// <remarks>与 cachedChronicleRev 必须配对维护：
        /// 只清字典会让缓存永不失效，只改版本号会让旧数据被反复命中。</remarks>
        private readonly Dictionary<string, List<ChronicleEvent>> cachedChronicleByLoc = new Dictionary<string, List<ChronicleEvent>>();
        /// <summary>
        /// 上述缓存对应的数据版本号；与当前 revision 不一致时整表失效重建。
        /// </summary>
        private long cachedChronicleRev = -1L;
        /// <summary>
        /// 绘制卡片内联的地点编年史面板（最多 6 行），返回面板实际高度。
        /// </summary>
        /// <param name="rect">面板区域；传入时高度为 0，实际高度由内容决定。</param>
        /// <param name="loc">地点对象。</param>
        /// <param name="service">归档服务；为 null 时返回 0。</param>
        /// <returns>面板高度；无服务时为 0。</returns>
        /// <remarks>
        /// 事件流按 stableId 缓存，升序排序只在首次构建时执行一次（PERF-001）；
        /// DataRevision 变化时整表清空重建（DATA-007）。
        /// 不要在绘制循环里对事件排序或过滤，那会让每帧退化成 O(n log n)。
        /// </remarks>
        private void DrawLocationChronicleInline(float x, float y, float w, Rect card, LocationObject loc,
            IArchiveService service)
        {
            const float ruleH = 18f;
            const float rowH = 20f;
            const int maxRows = 6;
            if (service == null || loc == null)
            {
                return;
            }
            long rev = service.GetDataRevision();
            if (rev != cachedChronicleRev)
            {
                cachedChronicleByLoc.Clear();
                cachedChronicleRev = rev;
            }
            List<ChronicleEvent> ordered;
            if (!cachedChronicleByLoc.TryGetValue(loc.StableId, out ordered))
            {
                IReadOnlyList<ChronicleEvent> events = service.GetEventsFor(loc.StableId);
                // 升序排列只在首次构建时执行一次并按 stableId 缓存（窗口不逐帧聚合）。
                ordered = (events == null)
                    ? new List<ChronicleEvent>()
                    : events.Where(e => e != null).OrderBy(e => e.Tick).ToList();
                cachedChronicleByLoc[loc.StableId] = ordered;
            }
            int total = ordered.Count;
            float avail = card.yMax - UITheme.CardPadY - (y + ruleH);
            int capacity = Mathf.Clamp(Mathf.FloorToInt(avail / rowH), 0, maxRows);
            int n = Mathf.Min(total, capacity);
            UIComponents.Label(new Rect(x, y, w, ruleH),
                n < total
                    ? "PersonalChronicle.UI.LocInlineChronicleTrimmed".Translate(
                        (NamedArgument)total, (NamedArgument)n).ToString()
                    : "PersonalChronicle.UI.LocInlineChronicleRule".Translate().ToString(),
                UITheme.FontLabel, ArchiveUiStyle.Muted);
            if (n == 0)
            {
                UIComponents.Label(new Rect(x, y + ruleH, w, rowH),
                    "PersonalChronicle.UI.LocNoChronicle".Translate(), UITheme.FontLabel,
                    ArchiveUiStyle.Muted);
                return;
            }
            float yy = y + ruleH;
            for (int i = 0; i < n; i++)
            {
                ChronicleEvent ev = ordered[i];
                string date = ev.Tick > 0L
                    ? GenDate.DateReadoutStringAt(ev.Tick, UnityEngine.Vector2.zero)
                    : "—";
                UIComponents.Label(new Rect(x, yy, 86f, 18f), date, UITheme.FontLabel,
                    ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(x + 90f, yy, Mathf.Max(0f, w - 90f), 18f), EventName(ev),
                    UITheme.FontBody, UITheme.Text);
                yy += rowH;
            }
        }

        /// <summary>
        /// 取地点卡片的强调色。
        /// </summary>
        /// <param name="loc">地点对象。</param>
        /// <returns>已关闭为 Muted、玩家本家为 Accent、其余为 Info；对象为空时为 Border。</returns>
        /// <remarks>判定顺序（已关闭 → 本家 → 其它）不可调换：
        /// 已关闭的本家应显示为弱化色，而不是继续用强调色。</remarks>
        private static Color LocationCardAccent(LocationObject loc)
        {
            if (loc == null)
            {
                return UITheme.Border;
            }
            if (loc.DeinitTick != -1L)
            {
                return UITheme.Muted;
            }
            if (loc.IsPlayerHome)
            {
                return UITheme.Accent;
            }
            return UITheme.Info;
        }

        /// <summary>
        /// 取地点种类的展示文案（本家 / 聚落 / 任务 / 未知）。
        /// </summary>
        /// <param name="loc">地点对象；为 null 时返回空串。</param>
        /// <returns>翻译后的种类名。</returns>
        /// <remarks>判定规则需与 LocationKindKey 保持一致：两者共用同一套子串匹配，
        /// 改一处要同步另一处，否则卡片配色与种类文案会对不上。</remarks>
        private static string LocationKindText(LocationObject loc)
        {
            if (loc == null)
            {
                return string.Empty;
            }
            if (loc.IsPlayerHome)
            {
                return "PersonalChronicle.UI.LocKind.Player".Translate().ToString();
            }
            string defName = loc.WorldObjectDefName;
            if (!string.IsNullOrEmpty(defName)
                && defName.IndexOf("Settlement", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "PersonalChronicle.UI.LocKind.Settle".Translate().ToString();
            }
            if (!string.IsNullOrEmpty(defName)
                && (defName.IndexOf("Quest", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || defName.IndexOf("Site", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return "PersonalChronicle.UI.LocKind.Quest".Translate().ToString();
            }
            return "PersonalChronicle.UI.LocKind.Unknown".Translate().ToString();
        }

        /// <summary>
        /// 取地点归属派系的展示文案。
        /// </summary>
        /// <param name="loc">地点对象；为 null 时返回空串。</param>
        /// <returns>玩家 / 无派系 / 其它派系 三类文案之一。</returns>
        /// <remarks>只区分"玩家 / 无 / 其它"而不展示具体派系名，是为保持卡片简短；
        /// 具体派系名在详情页展示。</remarks>
        private static string LocationFactionText(LocationObject loc)
        {
            if (loc == null)
            {
                return string.Empty;
            }
            if (loc.IsPlayerHome)
            {
                return "PersonalChronicle.UI.LocFactionPlayer".Translate().ToString();
            }
            if (string.IsNullOrEmpty(loc.FactionDefName))
            {
                return "PersonalChronicle.UI.LocFactionNone".Translate().ToString();
            }
            return "PersonalChronicle.UI.LocFactionOther".Translate().ToString();
        }

        /// <summary>
        /// 拼接地点的地理标签串（生物群系 / 地形 / 海岸 / 污染 / 均温）。
        /// </summary>
        /// <param name="loc">地点对象；为 null 时返回空串。</param>
        /// <returns>以三个空格分隔的标签串；无任何标签时返回空串（调用方据此省掉整行）。</returns>
        /// <remarks>
        /// 生物群系用 DefDatabase 查 BiomeDef 取 LabelCap，查不到时退回原始 defName ——
        /// 这是第三方 mod 被移除后的降级路径，不抛异常。
        /// 均温仅在不是 NaN 时才输出：未采样地点的 AvgTempC 为 NaN，直接格式化会显示成 "NaN"。
        /// </remarks>
        private static string LocationGeoText(LocationObject loc)
        {
            if (loc == null)
            {
                return string.Empty;
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(loc.MapDefName))
            {
                string biomeLabel = loc.MapDefName;
                BiomeDef biomeDef = DefDatabase<BiomeDef>.GetNamedSilentFail(loc.MapDefName);
                if (biomeDef != null)
                {
                    biomeLabel = biomeDef.LabelCap;
                }
                sb.Append("PersonalChronicle.UI.LocTagBiome".Translate().ToString()).Append(" · ").Append(biomeLabel);
            }
            if (!string.IsNullOrEmpty(loc.Hilliness))
            {
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(LocationHillText(loc));
            }
            if (loc.IsCoastal)
            {
                if (sb.Length > 0) sb.Append("   ");
                sb.Append("PersonalChronicle.UI.LocTagCoast".Translate().ToString());
            }
            if (loc.Pollution > 0.001f)
            {
                if (sb.Length > 0) sb.Append("   ");
                sb.Append("PersonalChronicle.UI.LocTagPolluted".Translate().ToString());
            }
            if (!float.IsNaN(loc.AvgTempC))
            {
                if (sb.Length > 0) sb.Append("   ");
                sb.Append("PersonalChronicle.UI.LocTemp".Translate((int)loc.AvgTempC).ToString());
            }
            return sb.ToString();
        }

        /// <summary>
        /// 取地形（Hilliness）的展示文案。
        /// </summary>
        /// <param name="loc">地点对象；为 null 或无地形数据时返回空串。</param>
        /// <returns>翻译后的地形名；遇到未知取值时返回原始字符串。</returns>
        /// <remarks>default 分支返回原始值而不是"未知"是有意的：
        /// 地形取值来自引擎且集合有限，遇到新值应原样暴露以便排查，不要静默吞掉。</remarks>
        private static string LocationHillText(LocationObject loc)
        {
            if (loc == null || string.IsNullOrEmpty(loc.Hilliness))
            {
                return string.Empty;
            }
            switch (loc.Hilliness)
            {
                case "Flat": return "PersonalChronicle.UI.LocHillFlat".Translate().ToString();
                case "Hilly": return "PersonalChronicle.UI.LocHillHilly".Translate().ToString();
                case "Mountainous": return "PersonalChronicle.UI.LocHillMountain".Translate().ToString();
                case "Impassable": return "PersonalChronicle.UI.LocHillImpassable".Translate().ToString();
                default: return loc.Hilliness;
            }
        }

        /// <summary>
        /// 拼接商贸标签串（可否交易 / 交易类别 / 是否需许可）。
        /// </summary>
        /// <param name="loc">地点对象；为 null 时返回空串。</param>
        /// <returns>以 " · " 与 " / " 分隔的标签串。</returns>
        /// <remarks>交易类别最多展示 3 个，避免长列表撑破卡片布局；完整列表在详情页展示。</remarks>
        private static string LocationTradeText(LocationObject loc)
        {
            if (loc == null)
            {
                return string.Empty;
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(loc.CanTrade
                ? "PersonalChronicle.UI.TradeCan".Translate().ToString()
                : "PersonalChronicle.UI.TradeNo".Translate().ToString());
            if (loc.TradeKindKeys != null && loc.TradeKindKeys.Count > 0)
            {
                sb.Append(" · ");
                for (int i = 0; i < loc.TradeKindKeys.Count && i < 3; i++)
                {
                    if (i > 0) sb.Append(" / ");
                    sb.Append(LocationTradeCategoryText(loc.TradeKindKeys[i]));
                }
            }
            if (!string.IsNullOrEmpty(loc.PermitRequiredDefName))
            {
                sb.Append(" · ").Append("PersonalChronicle.UI.TradePermit"
                    .Translate("PersonalChronicle.UI.TradePermitName".Translate().ToString()).ToString());
            }
            return sb.ToString();
        }

        /// <summary>
        /// 拼接地点生命周期文案（建立时间 + 当前状态）。
        /// </summary>
        /// <param name="loc">地点对象；为 null 时返回空串。</param>
        /// <returns>以三个空格分隔的单行文案。</returns>
        /// <remarks>与卡片内的三格生命周期行展示同一份信息，
        /// 但这里是紧凑单行版本，用于空间受限的场景。</remarks>
        private static string LocationLifeText(LocationObject loc)
        {
            if (loc == null)
            {
                return string.Empty;
            }
            string est = loc.EstablishedTick > 0L
                ? "PersonalChronicle.UI.LocLifeEstablished".Translate().ToString() + " " + GenDate.DateReadoutStringAt(loc.EstablishedTick, UnityEngine.Vector2.zero)
                : "PersonalChronicle.UI.LocLifeEstablished".Translate().ToString();
            string status = loc.DeinitTick != -1L
                ? LocationDeinitText(loc)
                : "PersonalChronicle.UI.LocLifeActive".Translate().ToString();
            return est + "   " + status;
        }

        /// <summary>
        /// 取地点关闭原因的展示文案。
        /// </summary>
        /// <param name="loc">地点对象。</param>
        /// <returns>摧毁 / 废弃 / 损毁 三类文案之一；未关闭或对象为空时返回"活跃"。</returns>
        /// <remarks>DeinitTick 为 -1 表示未关闭，此时返回"活跃"而不是空串，
        /// 以保证状态位永远有内容可显示。</remarks>
        private static string LocationDeinitText(LocationObject loc)
        {
            if (loc == null || loc.DeinitTick == -1L)
            {
                return "PersonalChronicle.UI.LocLifeActive".Translate().ToString();
            }
            if (loc.DeinitReason == PlaceVisitKeys.DeinitReasonDestroyed)
            {
                return "PersonalChronicle.UI.LocDeinit.Destroyed".Translate().ToString();
            }
            if (loc.DeinitReason == PlaceVisitKeys.DeinitReasonAbandoned)
            {
                return "PersonalChronicle.UI.LocDeinit.Abandoned".Translate().ToString();
            }
            return "PersonalChronicle.UI.LocLifeRuined".Translate().ToString();
        }

        /// <summary>
        /// 取战役时长的展示文本。
        /// </summary>
        /// <param name="battle">战役对象。</param>
        /// <returns>已结束返回时长；进行中返回"进行中"；对象为空或起始时刻非法时返回破折号。</returns>
        /// <remarks>EndTick 小于 0 或早于 StartTick 一律按"进行中"处理：
        /// 这两种取值都表示战役尚未正常结束，不能当作有效区间去计算差值。</remarks>
        private static string BattleDurationText(BattleObject battle)
        {
            if (battle == null || battle.StartTick < 0L)
            {
                return "—";
            }
            if (battle.EndTick < 0L || battle.EndTick < battle.StartTick)
            {
                return "PersonalChronicle.UI.BattleOngoing".Translate().ToString();
            }
            return ReadModels.SpanText.Format(battle.EndTick - battle.StartTick);
        }


    }
}
