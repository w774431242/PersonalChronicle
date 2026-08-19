using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// Partial of <see cref="ArchiveMainTabWindow"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveMainTabWindow : MainTabWindow
    {

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
                // value tint via StatCell's valueColor overload.
                UIComponents.StatCell(cell, labels[i], values[i].ToString(), accents[i]);
            }
            return LocationKpiStripHeight;
        }

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
                bool expanded = expandedLocations.Contains(loc.StableId);

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

                // Click toggles the inline chronicle expansion.
                if (Widgets.ButtonInvisible(card))
                {
                    if (expanded)
                    {
                        expandedLocations.Remove(loc.StableId);
                    }
                    else
                    {
                        expandedLocations.Add(loc.StableId);
                    }
                }

                // Inline chronicle expansion (this place's events), drawn below
                // the card as a full-width panel. Consumes the snapshot's event
                // stream (read model) — no sorting in the window.
                if (expanded)
                {
                    Rect panel = new Rect(card.x, cardTop + LocationCardHeight + 2f,
                        LocationCardWidth, 0f);
                    float ph = DrawLocationChroniclePanel(panel, loc, service);
                    yCursor = Mathf.Max(yCursor, cardTop + LocationCardHeight + 2f + ph + gap);
                }
                else
                {
                    yCursor = Mathf.Max(yCursor, cardTop + LocationCardHeight + gap);
                }
            }
            return yCursor - startY + 14f;
        }

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

        private static string LocationEstablishedYearText(LocationObject loc)
        {
            if (loc == null || loc.EstablishedTick <= 0L)
            {
                return "—";
            }
            return GenDate.Year(loc.EstablishedTick, 0f).ToString();
        }

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
        private readonly Dictionary<string, List<ChronicleEvent>> cachedChronicleByLoc = new Dictionary<string, List<ChronicleEvent>>();
        private long cachedChronicleRev = -1L;
        private float DrawLocationChroniclePanel(Rect rect, LocationObject loc, IArchiveService service)
        {
            const float rowH = 20f;
            const int maxRows = 6;
            if (service == null)
            {
                return 0f;
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
            if (ordered.Count == 0)
            {
                UIComponents.Label(new Rect(rect.x + UITheme.CardPadX, rect.y + 2f,
                    rect.width - UITheme.CardPadX * 2f, rowH),
                    "PersonalChronicle.UI.LocNoChronicle".Translate(), UITheme.FontLabel, ArchiveUiStyle.Muted);
                return rowH + 4f;
            }
            int n = Mathf.Min(ordered.Count, maxRows);
            float total = n * rowH + 4f;
            UIComponents.Card(new Rect(rect.x, rect.y, rect.width, total), UITheme.BorderSoft);
            float yy = rect.y + 2f;
            for (int i = 0; i < n; i++)
            {
                ChronicleEvent ev = ordered[i];
                string date = ev.Tick > 0L
                    ? GenDate.DateReadoutStringAt(ev.Tick, UnityEngine.Vector2.zero)
                    : "—";
                string title = EventName(ev);
                UIComponents.Label(new Rect(rect.x + 10f, yy, 86f, 18f), date, UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(rect.x + 100f, yy, rect.width - 110f, 18f), title, UITheme.FontBody, UITheme.Text);
                yy += rowH;
            }
            return total;
        }

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
