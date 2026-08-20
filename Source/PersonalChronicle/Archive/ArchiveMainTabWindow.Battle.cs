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

        private float DrawBattleKpiStrip(Rect viewRect, float y, ReadModels.BattleKpisView kpi)
        {
            if (kpi == null)
            {
                return 0f;
            }
            int n = 5;
            float gap = 6f;
            float cellW = (viewRect.width - gap * (n - 1)) / n;
            string[] labels =
            {
                "PersonalChronicle.UI.BattleKpiTotal".Translate().ToString(),
                "PersonalChronicle.UI.BattleKpiDecisive".Translate().ToString(),
                "PersonalChronicle.UI.BattleKpiKills".Translate().ToString(),
                "PersonalChronicle.UI.BattleKpiLosses".Translate().ToString(),
                "PersonalChronicle.UI.BattleKpiRoster".Translate().ToString()
            };
            int[] values = { kpi.Total, kpi.Decisive, kpi.Kills, kpi.Losses, kpi.Roster };
            Color[] accents =
            {
                UITheme.Text, UITheme.Accent, UITheme.Alive, UITheme.Dead, UITheme.Info
            };
            for (int i = 0; i < n; i++)
            {
                Rect cell = new Rect(viewRect.x + i * (cellW + gap), y, cellW, BattleKpiStripHeight);
                UIComponents.StatCell(cell, labels[i], values[i].ToString(), accents[i]);
            }
            return BattleKpiStripHeight;
        }

        private float DrawBattleOverviewCards(Rect viewRect, float startY, List<ArchiveObject> objects, float gap, IArchiveService service)
        {
            int perRow = Mathf.Max(1, (int)((viewRect.width + gap) / (BattleCardWidth + gap)));
            float yCursor = startY;

            for (int i = 0; i < objects.Count; i++)
            {
                BattleObject battle = objects[i] as BattleObject;
                if (battle == null)
                {
                    continue;
                }
                int col = i % perRow;
                int row = i / perRow;
                float cardTop = startY + row * (BattleCardHeight + gap);
                Rect card = new Rect(
                    viewRect.x + col * (BattleCardWidth + gap),
                    cardTop,
                    BattleCardWidth, BattleCardHeight);
                bool expanded = expandedBattles.Contains(battle.StableId);

                // Read-Model card aggregate (falls back to field values when absent).
                ReadModels.BattleCardView agg = cachedBattleKpis != null
                    && cachedBattleKpis.Cards != null
                    && cachedBattleKpis.Cards.TryGetValue(battle.StableId, out ReadModels.BattleCardView v)
                    ? v : null;
                int kills = agg != null ? agg.Kills : 0;
                int losses = agg != null ? agg.Losses : 0;
                int participants = agg != null ? agg.Participants
                    : (battle.ParticipantIds != null ? battle.ParticipantIds.Count : 0);
                bool significant = agg != null ? agg.IsSignificant : false;
                string threatKey = agg != null ? agg.ThreatKey : battle.ThreatKey;

                Color accent = significant ? UITheme.Accent : UITheme.Muted;
                ArchiveUiStyle.DrawCard(card, accent);
                float x = card.x + UITheme.CardPadX;
                float w = card.width - UITheme.CardPadX * 2f;
                float y = card.y + UITheme.CardPadY;

                // 1) Category row: threat tag + significance pill.
                string threatText = BattleThreatText(threatKey);
                if (!string.IsNullOrEmpty(threatText))
                {
                    UIComponents.Badge(new Rect(x, y, 60f, 16f), threatText,
                        threatKey == "ThreatBig" ? UITheme.Accent : UITheme.Info);
                }
                string pillText = significant
                    ? "PersonalChronicle.UI.BattleCardDecisive".Translate().ToString()
                    : "PersonalChronicle.UI.BattleCardSkirmish".Translate().ToString();
                UIComponents.Badge(new Rect(x + 56f, y, 56f, 16f), pillText,
                    significant ? UITheme.Accent : UITheme.Muted);
                y += 20f;

                // 2) Battle title.
                UIComponents.Label(new Rect(x, y, w, 20f),
                    ObjectDisplayLabel(battle), UITheme.FontBody, ArchiveUiStyle.Info);
                y += 22f;

                // 3) Sub-line: date · N participants · duration.
                string dateText = battle.StartTick > 0L
                    ? RimWorld.GenDate.DateReadoutStringAt(battle.StartTick, UnityEngine.Vector2.zero)
                    : "PersonalChronicle.UI.UnknownDate".Translate().ToString();
                string sub = dateText + " · "
                    + "PersonalChronicle.UI.BattleParticipantsN".Translate(participants).ToString()
                    + " · " + BattleDurationText(battle);
                UIComponents.Label(new Rect(x, y, w, 18f), sub, UITheme.FontLabel, ArchiveUiStyle.Muted);
                y += 20f;

                // 4) Three metric cells (force / kills / losses).
                float cellGap = 4f;
                float cellW = (w - cellGap * 2f) / 3f;
                UIComponents.Label(new Rect(x, y, cellW, 14f),
                    "PersonalChronicle.UI.BattleMetricRaid".Translate().ToString(),
                    UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(x, y + 14f, cellW, 20f),
                    battle.RaidCount > 0 ? battle.RaidCount.ToString() : "—",
                    UITheme.FontBody, UITheme.Text);
                UIComponents.Label(new Rect(x + cellW + cellGap, y, cellW, 14f),
                    "PersonalChronicle.UI.BattleMetricKills".Translate().ToString(),
                    UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(x + cellW + cellGap, y + 14f, cellW, 20f),
                    kills.ToString(), UITheme.FontBody, UITheme.Alive);
                UIComponents.Label(new Rect(x + 2f * (cellW + cellGap), y, cellW, 14f),
                    "PersonalChronicle.UI.BattleMetricLosses".Translate().ToString(),
                    UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(x + 2f * (cellW + cellGap), y + 14f, cellW, 20f),
                    losses.ToString(), UITheme.FontBody,
                    losses > 0 ? UITheme.Dead : UITheme.Text);
                y += 36f;

                // 5) Roster chips (participant names; folded when many).
                y = DrawBattleRosterChips(x, y, w, battle, service);

                // Click toggles the inline casualty expansion.
                if (Widgets.ButtonInvisible(card))
                {
                    if (expanded)
                    {
                        expandedBattles.Remove(battle.StableId);
                    }
                    else
                    {
                        expandedBattles.Add(battle.StableId);
                    }
                }

                // 6) Inline casualty expansion (kill/loss lines from the event stream).
                if (expanded)
                {
                    Rect panel = new Rect(card.x, cardTop + BattleCardHeight + 2f,
                        BattleCardWidth, 0f);
                    float ph = DrawBattleCasualtyPanel(panel, battle, service);
                    yCursor = Mathf.Max(yCursor, cardTop + BattleCardHeight + 2f + ph + gap);
                }
                else
                {
                    yCursor = Mathf.Max(yCursor, cardTop + BattleCardHeight + gap);
                }
            }
            return yCursor - startY + 14f;
        }

        private static string BattleThreatText(string threatKey)
        {
            if (threatKey == "ThreatBig")
            {
                return "PersonalChronicle.UI.BattleTagBig".Translate().ToString();
            }
            if (threatKey == "ThreatSmall")
            {
                return "PersonalChronicle.UI.BattleTagSmall".Translate().ToString();
            }
            return string.Empty;
        }

        private float DrawBattleRosterChips(float x, float y, float w, BattleObject battle, IArchiveService service)
        {
            List<string> ids = battle.ParticipantIds;
            if (ids == null || ids.Count == 0)
            {
                return y;
            }
            const float chipH = 16f;
            float step = 20f;
            int shown = 0;
            float chipX = x;
            const float chipMax = 3;
            for (int i = 0; i < ids.Count && shown < chipMax; i++)
            {
                Pawn pawn = service != null ? service.GetLivePawn(ids[i]) : null;
                string label = pawn != null ? pawn.LabelShort
                    : (ids[i].Length > 10 ? ids[i].Substring(0, 10) : ids[i]);
                float chipW = Mathf.Min(w - (chipX - x), Verse.Text.CalcSize(label).x + 8f);
                if (chipW <= 20f)
                {
                    break;
                }
                UIComponents.Badge(new Rect(chipX, y, chipW, chipH), label, UITheme.BorderSoft);
                chipX += chipW + 4f;
                shown++;
            }
            if (ids.Count > shown)
            {
                string more = "PersonalChronicle.UI.BattleRosterMore".Translate(ids.Count).ToString();
                UIComponents.Badge(new Rect(chipX, y, Mathf.Min(w - (chipX - x), Verse.Text.CalcSize(more).x + 8f), chipH),
                    more, UITheme.Muted);
            }
            return y + step;
        }

        // v4.17 体检（审计 P1-3）：伤亡行按对象事件索引查询 + 缓存（仿 Location
        // cachedChronicleByLoc 模式）——旧实现每帧 service.GetAllEvents() 全库扫描。
        private readonly Dictionary<string, List<ChronicleEvent>> cachedCasualtyByBattle =
            new Dictionary<string, List<ChronicleEvent>>();
        private long cachedCasualtyRev = -1L;

        /// <summary>该战役的伤亡 Death 事件（最多 maxRows 行；revision 变化时整体失效）。</summary>
        private List<ChronicleEvent> CasualtyLines(BattleObject battle, IArchiveService service)
        {
            if (battle == null || service == null || string.IsNullOrEmpty(battle.StableId))
            {
                return null;
            }
            long rev = service.GetDataRevision();
            if (rev != cachedCasualtyRev)
            {
                cachedCasualtyByBattle.Clear();
                cachedCasualtyRev = rev;
            }
            if (cachedCasualtyByBattle.TryGetValue(battle.StableId, out List<ChronicleEvent> lines))
            {
                return lines;
            }
            lines = new List<ChronicleEvent>();
            // GetEventsFor 按对象索引（Primary/Subjects 双匹配）——含 subjects=该战役的
            // Death 事件，等价于旧全库扫描结果，但只读单对象事件流。
            IReadOnlyList<ChronicleEvent> evs = service.GetEventsFor(battle.StableId);
            if (evs != null)
            {
                for (int i = 0; i < evs.Count; i++)
                {
                    ChronicleEvent ev = evs[i];
                    if (ev != null && ev.TypeKey == ChronicleEventType.Death)
                    {
                        lines.Add(ev);
                    }
                }
            }
            cachedCasualtyByBattle[battle.StableId] = lines;
            return lines;
        }

        private float DrawBattleCasualtyPanel(Rect rect, BattleObject battle, IArchiveService service)
        {
            const float rowH = 20f;
            const int maxRows = 6;
            if (service == null || battle == null)
            {
                return 0f;
            }
            List<ChronicleEvent> lines = CasualtyLines(battle, service);
            if (lines == null || lines.Count == 0)
            {
                UIComponents.Label(new Rect(rect.x + UITheme.CardPadX, rect.y + 2f,
                    rect.width - UITheme.CardPadX * 2f, rowH),
                    "PersonalChronicle.UI.BattleNoCasualties".Translate(), UITheme.FontLabel, ArchiveUiStyle.Muted);
                return rowH + 4f;
            }
            int n = Mathf.Min(lines.Count, maxRows);
            float total = n * rowH + 4f;
            UIComponents.Card(new Rect(rect.x, rect.y, rect.width, total), UITheme.BorderSoft);
            float yy = rect.y + 2f;
            for (int i = 0; i < n; i++)
            {
                ChronicleEvent ev = lines[i];
                string date = ev.Tick > 0L
                    ? GenDate.DateReadoutStringAt(ev.Tick, UnityEngine.Vector2.zero) : "—";
                bool kill = ev.Params != null
                    && ev.Params.TryGetValue(ChronicleEventParams.CombatRole, out string role)
                    && role == ChronicleEventParams.CombatRoleKill;
                string title = kill
                    ? "PersonalChronicle.UI.BattleLineKill".Translate().ToString()
                    : "PersonalChronicle.UI.BattleLineLoss".Translate().ToString();
                UIComponents.Label(new Rect(rect.x + 10f, yy, 86f, 18f), date, UITheme.FontLabel, ArchiveUiStyle.Muted);
                UIComponents.Label(new Rect(rect.x + 100f, yy, rect.width - 110f, 18f), title,
                    UITheme.FontBody, kill ? UITheme.Alive : UITheme.Dead);
                yy += rowH;
            }
            return total;
        }


    }
}
