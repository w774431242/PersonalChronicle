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
        // v6.7: wider/taller container so the 3×2 enriched grid is readable in the
        // inspect pane. The SixGrid component parameters (KpiCardH/KpiGap) are untouched.
        private const float TabWidth = 560f;
        private const float TabHeight = 580f;
        private const float Pad = UITheme.PanelPadding;
        private const float HeaderH = 52f;
        private const float ButtonH = 30f;
        // SixGridH is computed in FillTab from the available body height; do not hard-code.


        // ---- Cached read view (rebuilt only on pawn / revision change) ----
        private readonly ArchiveUiDataProvider uiDataProvider = new ArchiveUiDataProvider();
        private DetailSnapshot cachedSnapshot;
        private string cachedPawnId;
        private long cachedRevision = -1L;
        // v4.15 (extend): the six-cell KPI grid scrolls vertically when more than
        // the visible rows are present (e.g. enriched + added metric cells).
        private Vector2 sixScroll;

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

            // Footer button is pinned to the bottom; the six-cell grid scrolls internally.
            float footerY = outer.yMax - ButtonH;
            float gridH = Mathf.Max(0f, footerY - y - UITheme.SpaceXs);
            UIComponents.KpiCell[] cells = BuildCells(snap);
            UIComponents.SixGrid(new Rect(outer.x, y, outer.width, gridH), cells, ref sixScroll);

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
                    sixScroll = Vector2.zero;
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
        // The digest draws only the six-cell KPI grid (enriched v6.6). The grid has
        // its own internal scroll view, so the tab never wraps it in a second scroller.

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

        // ---- v4.15 six-cell KPI builders (read-only; all data from DetailSnapshot) ----
        // Enriched with v5 sub-info (工时单位+档位, 产出分类 Badge, 主驻地天数)
        // and extended with 4 metric cells (主业/主驻地/传承击杀/健康残值) sourced
        // from DetailSnapshot fields already aggregated in the Read Model.
        private static UIComponents.KpiCell[] BuildCells(DetailSnapshot snap)
        {
            string noRec = "PersonalChronicle.UI.Kpi.NoRecord".Translate().ToString();
            string pieces = "PersonalChronicle.UI.Kpi.Unit.Pieces".Translate().ToString();
            string silver = "PersonalChronicle.UI.Kpi.Unit.Silver".Translate().ToString();
            string totalHoursUnit = "PersonalChronicle.UI.Kpi.Unit.TotalHours".Translate().ToString();

            // ===== ① 工时：累计工时(大值+同行排名) + KPI条(周/日均) + 工时结构(前3职业) =====
            bool workOk = snap.WorkIntensity != null && snap.WorkIntensity.IsDefined;
            string workVal = workOk ? Mathf.RoundToInt((float)snap.WorkIntensity.TotalHours).ToString() : "--";
            string workRank = (workOk && snap.WorkIntensity.ColonyRank > 0 && snap.WorkIntensity.ColonyPopulation > 0)
                ? "PersonalChronicle.UI.Kpi.Rank".Translate(
                    snap.WorkIntensity.ColonyRank, snap.WorkIntensity.ColonyPopulation).ToString()
                : string.Empty;
            UIComponents.KpiRow[] workRows = workOk
                ? new UIComponents.KpiRow[]
                {
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Kpi.WeekHours.S".Translate().ToString(), Value = snap.WorkIntensity.WeeklyHours.ToString("0") + " " + totalHoursUnit },
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Kpi.DayHours.S".Translate().ToString(), Value = snap.WorkIntensity.DailyHours.ToString("0.0") + " " + totalHoursUnit },
                }
                : null;
            UIComponents.KpiBar[] workBars = null;
            if (snap.CareerBars != null)
            {
                List<UIComponents.KpiBar> bars = new List<UIComponents.KpiBar>();
                for (int i = 0; i < snap.CareerBars.Count && bars.Count < 3; i++)
                {
                    CareerBarView b = snap.CareerBars[i];
                    if (b == null) continue;
                    string tag = b.IsPrimary ? "PersonalChronicle.UI.Kpi.Career".Translate().ToString()
                        : (b.IsSecondary ? "PersonalChronicle.UI.SecondaryWork".Translate().ToString() : string.Empty);
                    bars.Add(new UIComponents.KpiBar
                    {
                        Caption = b.WorkTypeLabel + " · " + Mathf.RoundToInt((float)b.Ticks / 2500f).ToString() + "h",
                        Share01 = b.Share01,
                        Tag = tag
                    });
                }
                workBars = bars.Count > 0 ? bars.ToArray() : null;
            }

            // ===== ② 产出：累计价值(大值+同行产量) + KPI条(单产均值/分类数) + 产值贡献前3 =====
            bool prodOk = snap.ProductionTotal > 0 || snap.ProductionSilverValue > 0f;
            string prodVal = prodOk ? Mathf.RoundToInt(snap.ProductionSilverValue).ToString() : "--";
            string prodQty = (prodOk && snap.ProductionTotal > 0) ? snap.ProductionTotal + " " + pieces : string.Empty;
            UIComponents.KpiRow[] prodRows = prodOk
                ? new UIComponents.KpiRow[]
                {
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Kpi.UnitValue".Translate().ToString(), Value = (snap.ProductionTotal > 0 ? Mathf.RoundToInt(snap.ProductionSilverValue / (float)snap.ProductionTotal).ToString() : "0") + " " + silver + "/" + pieces },
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Kpi.CatCount".Translate().ToString(), Value = (snap.ProductionCategories != null ? snap.ProductionCategories.Count : 0).ToString() + " " + "PersonalChronicle.UI.Kpi.Cat".Translate().ToString() },
                }
                : null;
            UIComponents.KpiBar[] prodBars = null;
            if (prodOk && snap.ProductionLines != null && snap.ProductionLines.Count > 0)
            {
                float totalVal = 0f;
                for (int i = 0; i < snap.ProductionLines.Count; i++) totalVal += snap.ProductionLines[i].Value;
                List<ProductionLineView> top = new List<ProductionLineView>(snap.ProductionLines);
                top.Sort((a, b) => b.Value.CompareTo(a.Value));
                List<UIComponents.KpiBar> bars = new List<UIComponents.KpiBar>();
                int n = Mathf.Min(3, top.Count);
                for (int i = 0; i < n; i++)
                {
                    ProductionLineView l = top[i];
                    bars.Add(new UIComponents.KpiBar
                    {
                        Caption = l.Label + " · " + Mathf.RoundToInt(l.Value).ToString() + " " + silver,
                        Share01 = totalVal > 0f ? l.Value / totalVal : 0f
                    });
                }
                prodBars = bars.ToArray();
            }

            // ===== ③ 击杀：总数(大值) + KPI条(猎物种类) + 击杀构成前3种族 =====
            bool killOk = snap.Kills > 0;
            string killVal = killOk ? snap.Kills.ToString() : "--";
            int raceKinds = (snap.KillsByFaction != null) ? snap.KillsByFaction.Count : 0;
            UIComponents.KpiRow[] killRows = killOk
                ? new UIComponents.KpiRow[] { new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Kpi.PreyKinds".Translate().ToString(), Value = raceKinds.ToString() + " " + "PersonalChronicle.UI.Kpi.Kind".Translate().ToString() } }
                : null;
            UIComponents.KpiBar[] killBars = null;
            if (killOk && snap.KillsByFaction != null && snap.KillsByFaction.Count > 0)
            {
                List<KillByFactionView> top = new List<KillByFactionView>(snap.KillsByFaction);
                top.Sort((a, b) => b.Count.CompareTo(a.Count));
                List<UIComponents.KpiBar> bars = new List<UIComponents.KpiBar>();
                int n = Mathf.Min(3, top.Count);
                for (int i = 0; i < n; i++)
                {
                    KillByFactionView f = top[i];
                    bars.Add(new UIComponents.KpiBar
                    {
                        Caption = f.Label + " · " + f.Count,
                        Share01 = snap.Kills > 0 ? (float)f.Count / (float)snap.Kills : 0f
                    });
                }
                killBars = bars.ToArray();
            }

            // ===== ④ 战役：总数(大值+同行重大) + KPI条(歼敌/损失/参战) + 战损构成 =====
            bool battleOk = snap.BattleCount > 0;
            string battleVal = battleOk ? snap.BattleCount.ToString() : "--";
            string battleDecisive = (battleOk && snap.BattleKpis != null && snap.BattleKpis.Decisive > 0)
                ? snap.BattleKpis.Decisive + " " + "PersonalChronicle.UI.Kpi.Divine".Translate().ToString()
                : string.Empty;
            UIComponents.KpiRow[] battleRows = (battleOk && snap.BattleKpis != null)
                ? new UIComponents.KpiRow[]
                {
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Kpi.Kills".Translate().ToString(), Value = snap.BattleKpis.Kills.ToString() },
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Kpi.Losses".Translate().ToString(), Value = snap.BattleKpis.Losses.ToString() },
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Kpi.Roster".Translate().ToString(), Value = snap.BattleKpis.Roster.ToString() },
                }
                : null;
            UIComponents.KpiBar[] battleBars = null;
            if (battleOk && snap.BattleKpis != null && (snap.BattleKpis.Kills + snap.BattleKpis.Losses) > 0)
            {
                int tot = snap.BattleKpis.Kills + snap.BattleKpis.Losses;
                battleBars = new UIComponents.KpiBar[]
                {
                    new UIComponents.KpiBar { Caption = "PersonalChronicle.UI.Kpi.Kills".Translate().ToString() + " · " + snap.BattleKpis.Kills, Share01 = (float)snap.BattleKpis.Kills / (float)tot },
                    new UIComponents.KpiBar { Caption = "PersonalChronicle.UI.Kpi.Losses".Translate().ToString() + " · " + snap.BattleKpis.Losses, Share01 = (float)snap.BattleKpis.Losses / (float)tot },
                };
            }

            // ===== ⑤ 足迹：地点数(大值+同行主驻地天数) + KPI条(主驻地/远征/最长停留) + 停留Top2 =====
            bool footOk = snap.Footprint != null && snap.Footprint.PlaceCount > 0;
            string footVal = footOk ? snap.Footprint.PlaceCount.ToString() : "--";
            string footDays = (footOk && snap.Footprint.HomeDays > 0)
                ? snap.Footprint.HomeDays + " " + "PersonalChronicle.UI.Kpi.Unit.Days".Translate().ToString()
                : string.Empty;
            UIComponents.KpiRow[] footRows = footOk
                ? new UIComponents.KpiRow[]
                {
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Kpi.Home".Translate().ToString(), Value = snap.Footprint.HomePlaceText ?? noRec },
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.FootprintExpeditions".Translate().ToString(), Value = snap.Footprint.ExpeditionCount.ToString() },
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Kpi.LongestStay".Translate().ToString(), Value = (snap.Footprint.HomeDays > 0 ? snap.Footprint.HomeDays.ToString() : "0") + " " + "PersonalChronicle.UI.Kpi.Unit.Days".Translate().ToString() },
                }
                : null;
            UIComponents.KpiRow[] footStays = null;
            if (footOk && snap.Footprint.Stays != null && snap.Footprint.Stays.Count > 0)
            {
                int n = Mathf.Min(2, snap.Footprint.Stays.Count);
                UIComponents.KpiRow[] stays = new UIComponents.KpiRow[n];
                for (int i = 0; i < n; i++)
                {
                    FootstepView s = snap.Footprint.Stays[i];
                    stays[i] = new UIComponents.KpiRow { Label = s.PlaceText ?? noRec, Value = s.DwellText ?? string.Empty };
                }
                // Merge KPI strip + Top2 stays into one row block (card height budget).
                List<UIComponents.KpiRow> merged = new List<UIComponents.KpiRow>(footRows);
                merged.AddRange(stays);
                footStays = merged.ToArray();
            }

            // ===== ⑥ 神器传承：武器名(大值+同行历代击杀) + KPI条(锻造者/代数/当代击杀) + 传承链 =====
            bool legacyOk = snap.Legacy != null && !snap.Legacy.IsEmpty;
            string legacyVal = (legacyOk && !string.IsNullOrEmpty(snap.Legacy.TitleText)) ? snap.Legacy.TitleText : "--";
            string legacyTotal = (legacyOk && snap.Legacy.TotalKills > 0)
                ? snap.Legacy.TotalKills + " " + "PersonalChronicle.UI.Kpi.Unit.Kills".Translate().ToString()
                : string.Empty;
            UIComponents.KpiRow[] legacyRows = legacyOk
                ? new UIComponents.KpiRow[]
                {
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Legacy.ForgedBy".Translate().ToString(), Value = snap.Legacy.CreatedByText ?? noRec },
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Legacy.Gen".Translate().ToString(), Value = snap.Legacy.GenCount.ToString() + " " + "PersonalChronicle.UI.Legacy.GenUnit".Translate().ToString() },
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Legacy.CurrentKills".Translate().ToString(), Value = LegacyCurrentKills(snap.Legacy).ToString() + " " + "PersonalChronicle.UI.Kpi.Unit.Kills".Translate().ToString() },
                }
                : null;
            UIComponents.KpiChain[] legacyChain = null;
            if (legacyOk && snap.Legacy.Holders != null && snap.Legacy.Holders.Count > 0)
            {
                List<UIComponents.KpiChain> chain = new List<UIComponents.KpiChain>();
                foreach (LegacyHolderView h in snap.Legacy.Holders)
                {
                    if (h == null) continue;
                    string gen = h.IsCurrent ? "PersonalChronicle.UI.Legacy.Current".Translate().ToString()
                        : (h.IsFirst ? "PersonalChronicle.UI.Legacy.First".Translate().ToString() : "PersonalChronicle.UI.Legacy.Next".Translate().ToString());
                    chain.Add(new UIComponents.KpiChain
                    {
                        Label = (h.HolderText ?? noRec) + " · " + gen,
                        Value = h.KillCount + " " + "PersonalChronicle.UI.Kpi.Unit.Kills".Translate().ToString()
                    });
                }
                legacyChain = chain.Count > 0 ? chain.ToArray() : null;
            }

            return new UIComponents.KpiCell[]
            {
                new UIComponents.KpiCell { KindKey = "work",   TitleKey = "PersonalChronicle.UI.Kpi.Work",   Value = workVal, Unit = workOk ? totalHoursUnit : null, InlineMetric = workRank, Rows = workRows, Bars = workBars },
                new UIComponents.KpiCell { KindKey = "prod",   TitleKey = "PersonalChronicle.UI.Kpi.Prod",   Value = prodVal, Unit = prodOk ? silver : null, InlineMetric = prodQty, Rows = prodRows, Bars = prodBars },
                new UIComponents.KpiCell { KindKey = "kill",   TitleKey = "PersonalChronicle.UI.Kpi.Kill",   Value = killVal, Unit = killOk ? "PersonalChronicle.UI.Kpi.Unit.Kills".Translate().ToString() : null, Rows = killRows, Bars = killBars },
                new UIComponents.KpiCell { KindKey = "battle", TitleKey = "PersonalChronicle.UI.Kpi.Battle", Value = battleVal, Unit = battleOk ? "PersonalChronicle.UI.Kpi.Unit.Battles".Translate().ToString() : null, InlineMetric = battleDecisive, Rows = battleRows, Bars = battleBars },
                new UIComponents.KpiCell { KindKey = "foot",   TitleKey = "PersonalChronicle.UI.Kpi.Foot",   Value = footVal, Unit = footOk ? "PersonalChronicle.UI.InspectTab.Places".Translate().ToString() : null, InlineMetric = footDays, Rows = footStays ?? footRows },
                new UIComponents.KpiCell { KindKey = "legacy", TitleKey = "PersonalChronicle.UI.Kpi.Legacy", Value = legacyVal, InlineMetric = legacyTotal, Rows = legacyRows, Chain = legacyChain },
            };
        }

        /// <summary>Current holder's kill count in the legacy chain (IsCurrent row).</summary>
        private static int LegacyCurrentKills(LegacyView legacy)
        {
            if (legacy == null || legacy.Holders == null) return 0;
            foreach (LegacyHolderView h in legacy.Holders)
                if (h != null && h.IsCurrent) return h.KillCount;
            return 0;
        }
    }
}
