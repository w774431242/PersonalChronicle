using System;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.ReadModels;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// v4.6 pawn inspect tab ("档案"). Adds a per-pawn archive digest to the
    /// vanilla inspect pane so players can read a colonist's chronicle without
    /// opening the full Archive main tab.
    ///
    /// Boundary contract (architecture §3.1 / UI standards §5):
    ///   * This tab NEVER queries + sorts + null-guards on its own. It consumes a
    ///     <see cref="DetailSnapshot"/> produced by <see cref="IArchiveUiDataProvider"/>,
    ///     exactly like <see cref="ArchiveMainTabWindow"/> does.
    ///   * The snapshot is rebuilt only when the selected pawn changes or the
    ///     service data revision moves, never per-frame in the draw path.
    ///   * All drawing goes through <see cref="UIComponents"/> + <see cref="UITheme"/>;
    ///     no raw GUI.color / new Color in this file.
    /// </summary>
    public class ITab_Pawn_Chronicle : ITab
    {
        // ---- Layout metrics (CJK-safe; see UI standards §4) ----
        private const float TabWidth = 460f;
        private const float TabHeight = 510f;
        private const float Pad = UITheme.PanelPadding;
        private const float HeaderH = 52f;
        private const float StatRowH = 64f;
        private const float RowH = 26f;
        private const float SmallRowH = 20f;
        private const float SectionH = UITheme.SectionTitleHeight;
        private const float ButtonH = 30f;
        private const float ScrollbarW = 18f;
        /// <summary>Max key-event rows rendered; the full list lives in the main tab.</summary>
        private const int MaxEventRows = 12;
        /// <summary>Max milestone cards rendered in the digest.</summary>
        private const int MaxMilestones = 4;
        /// <summary>Max social-relation rows rendered in the digest.</summary>
        private const int MaxRelationRows = 5;

        // ---- Cached read view (rebuilt only on pawn / revision change) ----
        private readonly ArchiveUiDataProvider uiDataProvider = new ArchiveUiDataProvider();
        private DetailSnapshot cachedSnapshot;
        private string cachedPawnId;
        private long cachedRevision = -1L;
        private Vector2 scroll;

        public ITab_Pawn_Chronicle()
        {
            size = new Vector2(TabWidth, TabHeight);
            labelKey = "PersonalChronicle.UI.InspectTab";
            tutorTag = "PersonalChronicleArchive";
        }

        /// <summary>
        /// The inspect pane hands us either the pawn itself or its corpse. Resolve
        /// both so a dead colonist's archive stays reachable.
        /// </summary>
        private Pawn SelPawnSafe
        {
            get
            {
                Thing thing = SelThing;
                Pawn pawn = thing as Pawn;
                if (pawn != null)
                {
                    return pawn;
                }
                Corpse corpse = thing as Corpse;
                return corpse != null ? corpse.InnerPawn : null;
            }
        }

        /// <summary>
        /// Only show for pawns the archive actually tracks (player-side humanlikes).
        /// Keeps the tab off raiders/animals where it would always be empty.
        /// </summary>
        public override bool IsVisible
        {
            get
            {
                try
                {
                    Pawn pawn = SelPawnSafe;
                    if (pawn == null || pawn.RaceProps == null || !pawn.RaceProps.Humanlike)
                    {
                        return false;
                    }
                    IArchiveService service = PersonalChronicleMod.ArchiveService;
                    if (service == null)
                    {
                        return false;
                    }
                    // Visible when the archive knows this pawn, or when it is a
                    // current player-faction member (archive fills in over time).
                    string stableId = pawn.GetUniqueLoadID();
                    if (service.GetObject(stableId) != null)
                    {
                        return true;
                    }
                    return pawn.Faction != null && pawn.Faction.IsPlayer;
                }
                catch (Exception ex)
                {
                    Log.WarningOnce(
                        "PersonalChronicle: ITab_Pawn_Chronicle.IsVisible failed: " + ex.Message,
                        0x5C11A1);
                    return false;
                }
            }
        }

        protected override void FillTab()
        {
            Pawn pawn = SelPawnSafe;
            IArchiveService service = PersonalChronicleMod.ArchiveService;
            Rect outer = new Rect(0f, 0f, size.x, size.y).ContractedBy(Pad);

            if (pawn == null || service == null)
            {
                UIComponents.Label(outer, "PersonalChronicle.UI.NoService".Translate(),
                    UITheme.FontBody, UITheme.Muted);
                return;
            }

            EnsureSnapshot(service, pawn);
            DetailSnapshot snap = cachedSnapshot;
            if (snap == null)
            {
                UIComponents.Label(outer, "PersonalChronicle.UI.NoService".Translate(),
                    UITheme.FontBody, UITheme.Muted);
                return;
            }

            float y = outer.y;
            y = DrawHeader(outer, y, pawn, snap);
            y += UITheme.SpaceXs;

            // Footer button is pinned to the bottom; the digest scrolls above it.
            float footerY = outer.yMax - ButtonH;
            Rect viewport = new Rect(outer.x, y, outer.width, Mathf.Max(0f, footerY - y - UITheme.SpaceXs));
            float contentH = MeasureContent(snap);
            Rect contentRect = new Rect(0f, 0f, viewport.width - ScrollbarW, contentH);

            Widgets.BeginScrollView(viewport, ref scroll, contentRect);
            DrawContent(contentRect, snap);
            Widgets.EndScrollView();

            DrawFooter(new Rect(outer.x, footerY, outer.width, ButtonH), pawn);
        }

        // ---- Snapshot lifecycle ------------------------------------------------

        /// <summary>
        /// Rebuilds the read-model snapshot only when the selection or the data
        /// revision changes, so the draw path stays allocation-light.
        /// </summary>
        private void EnsureSnapshot(IArchiveService service, Pawn pawn)
        {
            try
            {
                string stableId = pawn.GetUniqueLoadID();
                long revision = service.GetDataRevision();
                if (cachedSnapshot != null && cachedPawnId == stableId && cachedRevision == revision)
                {
                    return;
                }
                if (cachedPawnId != stableId)
                {
                    scroll = Vector2.zero;
                }
                cachedSnapshot = uiDataProvider.BuildDetail(service, stableId, revision);
                cachedPawnId = stableId;
                cachedRevision = revision;
            }
            catch (Exception ex)
            {
                Log.WarningOnce(
                    "PersonalChronicle: chronicle tab snapshot build failed: " + ex.Message,
                    0x5C11A2);
                cachedSnapshot = null;
            }
        }

        // ---- Header ------------------------------------------------------------

        private float DrawHeader(Rect outer, float y, Pawn pawn, DetailSnapshot snap)
        {
            Rect header = new Rect(outer.x, y, outer.width, HeaderH);
            bool archived = IsArchived(snap);
            UIComponents.Card(header, archived ? UITheme.Dead : UITheme.Alive);

            float textX = header.x + UITheme.CardPadX;
            float textW = header.width - UITheme.CardPadX * 2f - 76f;
            UIComponents.Label(new Rect(textX, header.y + 6f, textW, 24f),
                pawn.LabelShortCap, UITheme.FontBody, UITheme.Text);

            string sub = BuildHeaderSubtitle(pawn, snap);
            UIComponents.Label(new Rect(textX, header.y + 28f, textW, 18f),
                sub, UITheme.FontLabel, UITheme.Muted);

            Rect pill = new Rect(header.xMax - 72f, header.y + 12f, 60f, 22f);
            UIComponents.Pill(pill,
                archived ? "PersonalChronicle.UI.Dead".Translate() : "PersonalChronicle.UI.Alive".Translate(),
                archived ? UITheme.Dead : UITheme.Alive);

            return header.yMax;
        }

        private static bool IsArchived(DetailSnapshot snap)
        {
            PawnObject pawnObject = snap.DetailObject as PawnObject;
            return pawnObject != null && pawnObject.IsArchived;
        }

        /// <summary>
        /// Subtitle line: the pawn's current role/title. Falls back to the archived
        /// display name when the live story tracker is unavailable (e.g. corpses of
        /// pawns whose story data was stripped).
        /// </summary>
        private static string BuildHeaderSubtitle(Pawn pawn, DetailSnapshot snap)
        {
            if (pawn.story != null && !string.IsNullOrEmpty(pawn.story.TitleShortCap))
            {
                return pawn.story.TitleShortCap;
            }
            ArchiveObject archived = snap.DetailObject;
            if (archived != null && !string.IsNullOrEmpty(archived.LabelSnapshot))
            {
                return archived.LabelSnapshot;
            }
            return "PersonalChronicle.UI.UnknownDate".Translate().ToString();
        }

        // ---- Content -----------------------------------------------------------

        /// <summary>
        /// Pre-computes the scroll content height. Kept in lockstep with
        /// <see cref="DrawContent"/> so the two never disagree.
        /// </summary>
        private float MeasureContent(DetailSnapshot snap)
        {
            float h = 0f;
            h += StatRowH + UITheme.BlockGap;

            int milestoneCount = Mathf.Min(MaxMilestones, CountOf(snap.Milestones));
            h += SectionH;
            h += milestoneCount > 0 ? milestoneCount * (RowH + UITheme.SpaceXxs) : SmallRowH;
            h += UITheme.BlockGap;

            int eventCount = Mathf.Min(MaxEventRows, CountOf(snap.KeyEvents));
            h += SectionH;
            h += eventCount > 0 ? eventCount * (RowH + UITheme.SpaceXxs) : SmallRowH;
            h += UITheme.BlockGap;

            int relationCount = Mathf.Min(MaxRelationRows, CountOf(snap.Relations));
            h += SectionH;
            h += relationCount > 0 ? relationCount * (RowH + UITheme.SpaceXxs) : SmallRowH;
            return h;
        }

        private void DrawContent(Rect rect, DetailSnapshot snap)
        {
            float y = rect.y;

            // --- Stat strip: service span / recorded events / milestones ---
            float cellW = (rect.width - UITheme.GridGap * 2f) / 3f;
            UIComponents.StatCell(new Rect(rect.x, y, cellW, StatRowH),
                "PersonalChronicle.UI.InspectTab.Events".Translate(),
                CountOf(snap.RawEvents).ToString());
            UIComponents.StatCell(new Rect(rect.x + cellW + UITheme.GridGap, y, cellW, StatRowH),
                "PersonalChronicle.UI.Milestones".Translate(),
                CountOf(snap.Milestones).ToString());
            UIComponents.StatCell(new Rect(rect.x + (cellW + UITheme.GridGap) * 2f, y, cellW, StatRowH),
                "PersonalChronicle.UI.InspectTab.Places".Translate(),
                snap.Footprint != null ? snap.Footprint.PlaceCount.ToString() : "0");
            y += StatRowH + UITheme.BlockGap;

            // --- Milestones ---
            UIComponents.SectionTitle(rect, y, "PersonalChronicle.UI.Milestones".Translate());
            y += SectionH;
            y = DrawMilestones(rect, y, snap);
            y += UITheme.BlockGap;

            // --- Key events ---
            UIComponents.SectionTitle(rect, y, "PersonalChronicle.UI.KeyEvents".Translate());
            y += SectionH;
            y = DrawKeyEvents(rect, y, snap);
            y += UITheme.BlockGap;

            // --- Social relations (initial + live) ---
            UIComponents.SectionTitle(rect, y, "PersonalChronicle.UI.Relations".Translate());
            y += SectionH;
            DrawRelations(rect, y, snap);
        }

        private float DrawMilestones(Rect rect, float y, DetailSnapshot snap)
        {
            IReadOnlyList<MilestoneView> list = snap.Milestones;
            if (CountOf(list) == 0)
            {
                UIComponents.Label(new Rect(rect.x, y, rect.width, SmallRowH),
                    "PersonalChronicle.UI.NoMilestones".Translate(), UITheme.FontLabel, UITheme.Dim);
                return y + SmallRowH;
            }

            int count = Mathf.Min(MaxMilestones, list.Count);
            for (int i = 0; i < count; i++)
            {
                MilestoneView m = list[i];
                if (m == null)
                {
                    continue;
                }
                Rect row = new Rect(rect.x, y, rect.width, RowH);
                UIComponents.Card(row, TintForKind(m.KindKey));

                float dateW = 92f;
                float textX = row.x + UITheme.CardPadX;
                UIComponents.Label(new Rect(textX, row.y + 4f, row.width - dateW - UITheme.CardPadX * 2f, 18f),
                    m.TitleText, UITheme.FontLabel, UITheme.Text);
                UIComponents.Label(new Rect(row.xMax - dateW - UITheme.CardPadX, row.y + 4f, dateW, 18f),
                    m.DateText, UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleRight);

                y += RowH + UITheme.SpaceXxs;
            }
            return y;
        }

        private float DrawKeyEvents(Rect rect, float y, DetailSnapshot snap)
        {
            IReadOnlyList<KeyEventView> list = snap.KeyEvents;
            if (CountOf(list) == 0)
            {
                UIComponents.Label(new Rect(rect.x, y, rect.width, SmallRowH),
                    "PersonalChronicle.UI.NoEvents".Translate(), UITheme.FontLabel, UITheme.Dim);
                return y + SmallRowH;
            }

            int count = Mathf.Min(MaxEventRows, list.Count);
            for (int i = 0; i < count; i++)
            {
                KeyEventView e = list[i];
                if (e == null)
                {
                    continue;
                }
                Rect row = new Rect(rect.x, y, rect.width, RowH);
                if (i % 2 == 1)
                {
                    UIComponents.TintedBox(row, UITheme.OverlayWhite04);
                }

                float dateW = 92f;
                UIComponents.Label(new Rect(row.x + UITheme.SpaceXxs, row.y + 4f, dateW, 18f),
                    e.DateText, UITheme.FontLabel, UITheme.Muted);

                float titleX = row.x + dateW + UITheme.SpaceXs;
                UIComponents.Label(new Rect(titleX, row.y + 4f, row.xMax - titleX - UITheme.SpaceXxs, 18f),
                    e.TitleText, UITheme.FontLabel,
                    e.IsHighlight ? UITheme.Accent : UITheme.Text);

                y += RowH + UITheme.SpaceXxs;
            }
            return y;
        }

        private void DrawRelations(Rect rect, float y, DetailSnapshot snap)
        {
            IReadOnlyList<RelationView> list = snap.Relations;
            if (CountOf(list) == 0)
            {
                UIComponents.Label(new Rect(rect.x, y, rect.width, SmallRowH),
                    "PersonalChronicle.UI.NoRelations".Translate(), UITheme.FontLabel, UITheme.Dim);
                return;
            }

            int count = Mathf.Min(MaxRelationRows, list.Count);
            for (int i = 0; i < count; i++)
            {
                RelationView r = list[i];
                if (r == null)
                {
                    continue;
                }
                Rect row = new Rect(rect.x, y, rect.width, RowH);
                if (i % 2 == 1)
                {
                    UIComponents.TintedBox(row, UITheme.OverlayWhite04);
                }

                float labelW = Mathf.Min(90f, row.width * 0.35f);
                UIComponents.Label(new Rect(row.x + UITheme.SpaceXxs, row.y + 4f, labelW, 18f),
                    r.RelationLabel, UITheme.FontLabel, UITheme.Muted);

                float nameX = row.x + labelW + UITheme.SpaceXs;
                UIComponents.Label(new Rect(nameX, row.y + 4f,
                    row.xMax - nameX - 56f - UITheme.SpaceXxs, 18f),
                    r.OtherLabel, UITheme.FontLabel, UITheme.Text);

                UIComponents.Label(new Rect(row.xMax - 56f - UITheme.SpaceXxs, row.y + 4f, 56f, 18f),
                    r.StatusLabel, UITheme.FontLabel,
                    r.IsLive ? UITheme.Alive : UITheme.Dim, TextAnchor.MiddleRight);

                y += RowH + UITheme.SpaceXxs;
            }
        }

        // ---- Footer ------------------------------------------------------------

        private void DrawFooter(Rect rect, Pawn pawn)
        {
            if (!Widgets.ButtonText(rect, "PersonalChronicle.UI.InspectTab.OpenFull".Translate()))
            {
                return;
            }
            try
            {
                MainButtonDef def = DefDatabase<MainButtonDef>.GetNamedSilentFail("PersonalChronicleArchive");
                if (def == null || def.TabWindow == null)
                {
                    return;
                }
                ArchiveMainTabWindow window = def.TabWindow as ArchiveMainTabWindow;
                if (window != null)
                {
                    window.RequestPawnDetail(pawn.GetUniqueLoadID());
                }
                Find.MainTabsRoot.SetCurrentTab(def);
            }
            catch (Exception ex)
            {
                Log.Warning("PersonalChronicle: failed to open archive from inspect tab: " + ex.Message);
            }
        }

        // ---- Helpers -----------------------------------------------------------

        private static int CountOf<T>(IReadOnlyList<T> list)
        {
            return list == null ? 0 : list.Count;
        }

        /// <summary>
        /// Maps a milestone's event kind to a timeline accent token. Mirrors the
        /// main window's tinting so both surfaces read consistently.
        /// </summary>
        private static Color TintForKind(string kindKey)
        {
            if (string.IsNullOrEmpty(kindKey))
            {
                return UITheme.TimelineOther;
            }
            if (kindKey == ChronicleEventKind.Join.ToString()) return UITheme.TimelineJoin;
            if (kindKey == ChronicleEventKind.Death.ToString()) return UITheme.TimelineDeath;
            if (kindKey == ChronicleEventKind.Battle.ToString()) return UITheme.TimelineBattle;
            if (kindKey == ChronicleEventKind.Social.ToString()) return UITheme.TimelineSocial;
            if (kindKey == ChronicleEventKind.Craft.ToString()) return UITheme.TimelineCraft;
            if (kindKey == ChronicleEventKind.Built.ToString()) return UITheme.TimelineBuilt;
            return UITheme.TimelineOther;
        }
    }
}
