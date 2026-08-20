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
        // v1.1.4 UI 优化：移除角色信息头（DrawHeader），顶部改 3 卡布局（房间/工坊/类型），
        // 每卡主区点击改名、位置副行点击镜头跳转。TabHeight 640 避免 ITab 向上展开时顶部被屏幕/窗口边界遮盖；
        // 六宫格内部仍用 ScrollView 滚动兜底，内容不会溢出。
        private const float TabWidth = 560f;
        private const float TabHeight = 640f;
        private const float Pad = UITheme.PanelPadding;
        private const float ButtonH = 34f;
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
            // v4.17 体检：Tab 尺寸屏幕自适应（与 ITab_Pawn_Career 同款兜底）——
            // 低分辨率/高 UI 缩放下固定 560×640 会把顶边推出屏幕。
            float w = Mathf.Min(TabWidth, Verse.UI.screenWidth - 24f);
            float h = Mathf.Min(TabHeight, Verse.UI.screenHeight - 240f);
            if (Mathf.Abs(size.x - w) > 1f || Mathf.Abs(size.y - h) > 1f)
            {
                size = new Vector2(w, h);
            }

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

            // v1.1.4 UI 重构：移除角色信息头，顶部改为 3 卡布局（房间/工坊/类型）。
            // 每卡主区点击改名，右侧 ▶ 点击镜头跳转。
            // 3 卡在 outer 顶部下移 4px 留出视觉间隔；SixGrid 紧跟 3 卡下沿。
            float residenceBarH = ResidenceBarH;
            float residenceBarY = outer.y + 4f;
            try
            {
                DrawResidenceBar(outer, residenceBarY, pawn, snap);
            }
            catch (Exception ex)
            {
                Log.WarningOnce("PersonalChronicle: DrawResidenceBar failed: " + ex.Message, 0x5C11B0);
            }
            float y = residenceBarY + residenceBarH + UITheme.SpaceXs;

            // v1.1.4 布局修复：footer 固定贴底（outer.yMax - ButtonH），六宫格高度 =
            // 实际可用空间（footerY - y - gap）。SixGrid 内部 BeginScrollView 自动滚动，
            // 空间不足时滚动查看，空间充足时完整显示——任何分辨率都不再遮挡/悬空。
            float footerY = outer.yMax - ButtonH;
            float gridTop = y;
            float gridH = Mathf.Max(0f, footerY - y - UITheme.SpaceXs);
            UIComponents.KpiCell[] cells = BuildCells(snap);
            UIComponents.SixGrid(new Rect(outer.x, gridTop, outer.width, gridH), cells, ref sixScroll);

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

        // ---- v1.1.4 劳模住所/工坊检测：顶部 3 卡布局（房间 / 工坊 / 类型） ----
        // 只消费 DetailSnapshot 已解析的展示文本（WorkplaceView/ResidenceView）。
        // 每卡：主区点击改名，右侧 ▶ 箭头点击镜头跳转（CameraJumper.TryJump）。
        // 坐标本身不显示给玩家（隐藏 v1.1.4 用户要求），▶ 暗示可点击跳转。
        // 左色条语义：房间=Alive 绿、工坊=PillGold、类型=Accent。
        // 卡间距与下方 SixGrid 的 KpiGap 一致（10f），保证 3 卡与六宫格列线对齐。
        private static readonly float ResidenceBarH = 44f;

        private float DrawResidenceBar(Rect outer, float y, Pawn pawn, DetailSnapshot snap)
        {
            string noRec = "--";
            IArchiveService service = PersonalChronicleMod.ArchiveService;

            bool homeOk = snap != null && snap.Residence != null && !snap.Residence.IsEmpty
                && !string.IsNullOrEmpty(snap.Residence.RoomRoleLabel);
            bool workOk = snap != null && snap.Workplace != null && !snap.Workplace.IsEmpty;
            bool typeOk = snap != null && snap.Residence != null && !snap.Residence.IsEmpty
                && !string.IsNullOrEmpty(snap.Residence.RoomTypeName);

            string homeLabel = homeOk ? snap.Residence.RoomRoleLabel : noRec;
            string workLabel = (workOk && !string.IsNullOrEmpty(snap.Workplace.BuildingLabel))
                ? snap.Workplace.BuildingLabel
                : noRec;
            string typeLabel = typeOk ? snap.Residence.RoomTypeName : noRec;

            // 位置副行文案：有坐标显示 "x,y"（可点击跳转），无坐标显示 "--"（不可点击）。
            string homeSub = PositionText(snap, true);
            bool homeSubClick = homeOk && CanJump(snap.Residence.MapIndex, snap.Residence.Cell);
            string workSub = PositionText(snap, false);
            bool workSubClick = workOk && CanJump(snap.Workplace.MapIndex, snap.Workplace.Cell);

            // v1.1.4 对齐六宫格：3 卡宽度 = SixGrid 内每张卡宽 = (outer.width - Scrollbar - 2*KpiGap)/3，
            // 3 卡与下方 2 行六宫格的列线垂直对齐。
            float sixInnerW = outer.width - UITheme.ScrollbarThickness;
            float cardW = (sixInnerW - UIComponents.KpiGap * 2f) / 3f;
            float gap = UIComponents.KpiGap;
            Rect homeRect = new Rect(outer.x, y, cardW, ResidenceBarH);
            Rect workRect = new Rect(outer.x + cardW + gap, y, cardW, ResidenceBarH);
            Rect typeRect = new Rect(outer.x + (cardW + gap) * 2f, y, cardW, ResidenceBarH);

            string pawnId = pawn != null ? pawn.GetUniqueLoadID() : null;

            // 房间卡：点击改名（RoomNameOverrides），位置副行点击跳转住所。
            if (UIComponents.ClickableCard(homeRect,
                "PersonalChronicle.UI.Residence.Label".Translate().ToString(),
                homeLabel, homeSub, UITheme.Alive, homeSubClick, out bool homeJump))
            {
                if (homeOk && service != null && pawnId != null
                    && !string.IsNullOrEmpty(snap.Residence.RoomRoleDefName))
                {
                    cachedRevision = -1L;
                    Find.WindowStack.Add(new Dialog_RenameWorkplace(
                        "PersonalChronicle.UI.RenameRoomName.Title",
                        "PersonalChronicle.UI.RenameRoomName.Hint",
                        homeLabel,
                        name => service.SetRoomName(pawnId, snap.Residence.RoomRoleDefName, name)));
                }
            }
            if (homeJump)
            {
                JumpTo(snap.Residence.MapIndex, snap.Residence.Cell);
            }

            // 工坊卡：点击改名（BuildingAliases），位置副行点击跳转工坊。
            if (UIComponents.ClickableCard(workRect,
                "PersonalChronicle.UI.Workplace.Label".Translate().ToString(),
                workLabel, workSub, UITheme.PillGold, workSubClick, out bool workJump))
            {
                if (workOk && service != null && !string.IsNullOrEmpty(snap.Workplace.BuildingStableId))
                {
                    cachedRevision = -1L;
                    Find.WindowStack.Add(new Dialog_RenameWorkplace(
                        "PersonalChronicle.UI.RenameWorkplace.Title",
                        "PersonalChronicle.UI.RenameWorkplace.Hint",
                        workLabel,
                        name => service.SetBuildingAlias(snap.Workplace.BuildingStableId, name)));
                }
            }
            if (workJump)
            {
                JumpTo(snap.Workplace.MapIndex, snap.Workplace.Cell);
            }

            // 类型卡：点击改类型名（RoomTypeOverrides），位置副行点击跳转住所（同房间）。
            if (UIComponents.ClickableCard(typeRect,
                "PersonalChronicle.UI.RoomType.Label".Translate().ToString(),
                typeLabel, homeSub, UITheme.Accent, homeSubClick, out bool typeJump))
            {
                if (typeOk && service != null && pawnId != null
                    && !string.IsNullOrEmpty(snap.Residence.RoomRoleDefName))
                {
                    cachedRevision = -1L;
                    Find.WindowStack.Add(new Dialog_RenameWorkplace(
                        "PersonalChronicle.UI.RenameRoomType.Title",
                        "PersonalChronicle.UI.RenameRoomType.Hint",
                        typeLabel,
                        name => service.SetRoomTypeName(pawnId, snap.Residence.RoomRoleDefName, name)));
                }
            }
            if (typeJump)
            {
                JumpTo(snap.Residence.MapIndex, snap.Residence.Cell);
            }

            return y + ResidenceBarH;
        }

        /// <summary>位置副行文案：住所=房间中心坐标，工坊=工坊坐标；无效显示 "--"。</summary>
        private static string PositionText(DetailSnapshot snap, bool isHome)
        {
            int mapIndex = isHome ? snap.Residence.MapIndex : snap.Workplace.MapIndex;
            IntVec3 cell = isHome ? snap.Residence.Cell : snap.Workplace.Cell;
            if (mapIndex >= 0 && cell.IsValid)
            {
                return cell.x + "," + cell.z;
            }
            return "--";
        }

        /// <summary>坐标是否可跳转：mapIndex 有效、格子有效、地图存在。</summary>
        private static bool CanJump(int mapIndex, IntVec3 cell)
        {
            if (mapIndex < 0 || !cell.IsValid) return false;
            foreach (Map m in Find.Maps)
            {
                if (m != null && m.Index == mapIndex) return true;
            }
            return false;
        }

        /// <summary>镜头跳转到指定地图坐标（CameraJumper.TryJump，失败静默）。</summary>
        private static void JumpTo(int mapIndex, IntVec3 cell)
        {
            if (mapIndex < 0 || !cell.IsValid) return;
            Map map = null;
            foreach (Map m in Find.Maps)
            {
                if (m != null && m.Index == mapIndex) { map = m; break; }
            }
            if (map == null || !cell.InBounds(map)) return;
            CameraJumper.TryJump(cell, map);
        }

        // ---- Content -----------------------------------------------------------
        // The digest draws only the six-cell KPI grid (enriched v6.6). The grid has
        // its own internal scroll view, so the tab never wraps it in a second scroller.

        // ---- Footer ------------------------------------------------------------

        private void DrawFooter(Rect rect, Pawn pawn)
        {
            // v1.1.4 布局调整：移除「打开完整档案馆」按钮，为后续功能预留空白空间。
            // 仅在开发者模式下保留测试按钮（DevTestButtons 内部门控）。
            // Footer 矩形仍占用 ButtonH 高度，但不再画「打开完整档案馆」入口——用户可
            // 通过主菜单「档案馆」按钮直达。
            if (Prefs.DevMode && pawn != null)
            {
                float testW = Mathf.Min(rect.width * 0.32f, 130f);
                Rect testRect = new Rect(rect.x, rect.y, testW, rect.height);
                DevTestButtons.DrawButton(testRect, pawn);
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
            // 固定 3 个进度条：不足 3 职业时用 "--"/0 占位；无数据（未定义/老存档）也保持 3 个空槽，布局稳定。
            {
                List<UIComponents.KpiBar> bars = new List<UIComponents.KpiBar>();
                if (snap.CareerBars != null)
                {
                    for (int i = 0; i < snap.CareerBars.Count && bars.Count < 3; i++)
                    {
                        CareerBarView b = snap.CareerBars[i];
                        if (b == null) continue;
                        string tag = b.IsPrimary ? "PersonalChronicle.UI.Kpi.Career".Translate().ToString()
                            : (b.IsSecondary ? "PersonalChronicle.UI.SecondaryWork".Translate().ToString() : string.Empty);
                        float hours = (float)b.Ticks / 2500f;
                        bars.Add(new UIComponents.KpiBar
                        {
                            Caption = b.WorkTypeLabel + " · " + Mathf.RoundToInt(hours).ToString() + "h · " + (int)(b.Share01 * 100f) + "%",
                            Share01 = b.Share01,
                            Tag = tag
                        });
                    }
                }
                while (bars.Count < 3)
                {
                    bars.Add(new UIComponents.KpiBar { Caption = "--", Share01 = 0f });
                }
                workBars = bars.ToArray();
            }

            // ===== ② 产出：累计产值(大值) + 大值旁 inline(产量·种类) + KPI行(周产出/净值) + 产值贡献前3 =====
            // 统一基准为工时宫格：大字 + inline + 2 行 KPI + caption + 3 progress bars。
            bool prodOk = snap.ProductionTotal > 0 || snap.ProductionSilverValue > 0f;
            string prodVal = prodOk ? Mathf.RoundToInt(snap.ProductionSilverValue).ToString() : "--";
            // inline 指标：维持产量(件) + 新增种类数(类)，均来自真实累加器累计。
            string prodKinds = (snap.ProductionTypeViews != null && snap.ProductionTypeViews.Count > 0)
                ? snap.ProductionTypeViews.Count.ToString() + " " + "PersonalChronicle.UI.Kpi.Cat".Translate().ToString()
                : "0 " + "PersonalChronicle.UI.Kpi.Cat".Translate().ToString();
            string prodInline = (prodOk && snap.ProductionTotal > 0)
                ? snap.ProductionTotal + " " + pieces + " · " + prodKinds
                : prodKinds;
            // 周产出/净值：近 7 / 30 天滚动窗口的折算银币。无数据时显示 0 银。
            string weekVal = Mathf.RoundToInt(snap.WeeklyProductionSilver).ToString() + " " + silver;
            string monthVal = Mathf.RoundToInt(snap.MonthlyProductionSilver).ToString() + " " + silver;
            UIComponents.KpiRow[] prodRows = new UIComponents.KpiRow[]
            {
                new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Kpi.WeekOutput".Translate().ToString(), Value = weekVal },
                new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Kpi.NetValue".Translate().ToString(), Value = monthVal },
            };
            UIComponents.KpiBar[] prodBars = null;
            // 真实数据源：ProductionTypeViews 已按产值降序（来自 ProductionAccumulator 持久化累计）。
            // 不再重扫事件流；caption 形如「武器 · 1200 银 · 35%」，与工时 bars 对齐。
            // 固定 3 个进度条：不足 3 类时用 "--"/0 占位，保持布局稳定（与击杀宫格一致）。
            {
                List<UIComponents.KpiBar> bars = new List<UIComponents.KpiBar>();
                if (prodOk && snap.ProductionTypeViews != null && snap.ProductionTypeViews.Count > 0)
                {
                    float totalVal = 0f;
                    for (int i = 0; i < snap.ProductionTypeViews.Count; i++) totalVal += snap.ProductionTypeViews[i].MarketValue;
                    int n = Mathf.Min(3, snap.ProductionTypeViews.Count);
                    for (int i = 0; i < n; i++)
                    {
                        ProductionTypeView t = snap.ProductionTypeViews[i];
                        string catLabel = string.IsNullOrEmpty(t.DefName)
                            ? "—"
                            : (DefDatabase<ThingCategoryDef>.GetNamedSilentFail(t.DefName)?.LabelCap ?? t.DefName);
                        bars.Add(new UIComponents.KpiBar
                        {
                            Caption = catLabel + " · " + Mathf.RoundToInt(t.MarketValue).ToString() + " " + silver
                                + " · " + (totalVal > 0f ? Mathf.RoundToInt(t.MarketValue / totalVal * 100f) : 0) + "%",
                            Share01 = totalVal > 0f ? t.MarketValue / totalVal : 0f
                        });
                    }
                }
                while (bars.Count < 3)
                {
                    bars.Add(new UIComponents.KpiBar { Caption = "--", Share01 = 0f });
                }
                prodBars = bars.ToArray();
            }

            // ===== ③ 击杀：总数(大值, 同行=猎物种类) + KPI条(参战战役/生涯伤害) + 击杀构成前3种族 =====
            // v1.1.4 统一基准：猎物种类并入大字同行 inline（与产出"件·类"一致），KPI 行收敛为 2 行；
            // 战斗风格已移除（MeleeKillRatio 等持久化字段保留，仅 UI 不再展示）。
            bool killOk = snap.Kills > 0;
            string killVal = killOk ? snap.Kills.ToString() : "--";
            int raceKinds = (snap.KillsByFaction != null) ? snap.KillsByFaction.Count : 0;
            // inline：猎物种类（同行右对齐），与产出宫格"产量·种类"同一视觉位。
            string killInline = raceKinds.ToString() + " " + "PersonalChronicle.UI.Kpi.Kind".Translate().ToString();
            string battleParticipated = snap.ParticipatedBattles > 0 ? snap.ParticipatedBattles.ToString() : "0";
            string damageDealt = snap.DamageDealtTotal > 0f ? Mathf.RoundToInt(snap.DamageDealtTotal).ToString() : "0";
            UIComponents.KpiRow[] killRows = killOk
                ? new UIComponents.KpiRow[]
                {
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Kpi.BattlesFought".Translate().ToString(), Value = battleParticipated },
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Kpi.DamageDealt".Translate().ToString(), Value = damageDealt },
                }
                : null;
            UIComponents.KpiBar[] killBars = null;
            // 固定 3 个进度条：不足 3 种族时用 "--"/0 占位（与产出宫格一致），布局稳定，
            // 无数据时仍展示 3 个空槽，与有数据时布局一致。
            {
                List<UIComponents.KpiBar> bars = new List<UIComponents.KpiBar>();
                if (killOk && snap.KillsByFaction != null && snap.KillsByFaction.Count > 0)
                {
                    List<KillByFactionView> top = new List<KillByFactionView>(snap.KillsByFaction);
                    top.Sort((a, b) => b.Count.CompareTo(a.Count));
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
                }
                while (bars.Count < 3)
                {
                    bars.Add(new UIComponents.KpiBar { Caption = "--", Share01 = 0f });
                }
                killBars = bars.ToArray();
            }

            // ===== ④ 损耗：大值=累计消耗(银) + 同行=日均损耗 + KPI行(周损耗/日均损耗) + 构成前3类目 =====
            // v1.1.4 替换战役宫格：人物每日消耗品（食物/药品/成瘾品等）按 Def.BaseMarketValue 计价。
            // 数据源 ConsumptionAccumulator（持久化，Thing.Ingested 捕获，不写事件流）。
            // colony 级战役数据保留，完整档案馆「战役」分类仍可查看。
            bool consOk = snap.ConsumptionTotalSilver > 0f;
            string consVal = consOk ? Mathf.RoundToInt(snap.ConsumptionTotalSilver).ToString() : "--";
            string consDaily = Mathf.RoundToInt(snap.DailyConsumptionSilver).ToString() + " " + silver;
            string consDailyInline = "PersonalChronicle.UI.Kpi.DailyAverage".Translate().ToString() + " " + consDaily;
            UIComponents.KpiRow[] consRows = consOk
                ? new UIComponents.KpiRow[]
                {
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Kpi.WeekConsume".Translate().ToString(), Value = Mathf.RoundToInt(snap.WeeklyConsumptionSilver).ToString() + " " + silver },
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Kpi.DailyConsume".Translate().ToString(), Value = consDaily },
                }
                : null;
            UIComponents.KpiBar[] consBars = null;
            // 损耗构成前 3 类目（SilverByCategory 已按银币降序）；固定 3 条，不足用 "--"/0 占位。
            {
                List<UIComponents.KpiBar> bars = new List<UIComponents.KpiBar>();
                float totalVal = snap.ConsumptionTotalSilver;
                if (consOk && snap.ConsumptionTypeViews != null && snap.ConsumptionTypeViews.Count > 0)
                {
                    int n = Mathf.Min(3, snap.ConsumptionTypeViews.Count);
                    for (int i = 0; i < n; i++)
                    {
                        ConsumptionTypeView t = snap.ConsumptionTypeViews[i];
                        string catLabel = string.IsNullOrEmpty(t.CategoryDefName)
                            ? "—"
                            : (DefDatabase<ThingCategoryDef>.GetNamedSilentFail(t.CategoryDefName)?.LabelCap ?? t.CategoryDefName);
                        bars.Add(new UIComponents.KpiBar
                        {
                            Caption = catLabel + " · " + Mathf.RoundToInt(t.Silver).ToString() + " " + silver
                                + " · " + (totalVal > 0f ? Mathf.RoundToInt(t.Silver / totalVal * 100f) : 0) + "%",
                            Share01 = totalVal > 0f ? t.Silver / totalVal : 0f
                        });
                    }
                }
                while (bars.Count < 3)
                {
                    bars.Add(new UIComponents.KpiBar { Caption = "--", Share01 = 0f });
                }
                consBars = bars.ToArray();
            }

            // ===== ⑤ 足迹：地点数(大值+同行主驻地天数) + KPI行(主驻地/远征) + 停留时长前3进度条 =====
            // v1.1.4 统一基准：KPI 行收敛为 2 行（主驻地/远征），停留 Top2 行式改为进度条。
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
                }
                : null;
            UIComponents.KpiBar[] footBars = null;
            // 停留时长前 3 地点（Stays 已按 DwellTicks 降序）；固定 3 条，不足用 "--"/0 占位。
            {
                List<UIComponents.KpiBar> bars = new List<UIComponents.KpiBar>();
                float totalDwell = 0f;
                if (footOk && snap.Footprint.Stays != null && snap.Footprint.Stays.Count > 0)
                {
                    for (int i = 0; i < snap.Footprint.Stays.Count; i++)
                    {
                        if (snap.Footprint.Stays[i] != null && snap.Footprint.Stays[i].DwellTicks > 0L)
                        {
                            totalDwell += snap.Footprint.Stays[i].DwellTicks;
                        }
                    }
                    int n = Mathf.Min(3, snap.Footprint.Stays.Count);
                    for (int i = 0; i < n; i++)
                    {
                        FootstepView s = snap.Footprint.Stays[i];
                        if (s == null) continue;
                        float share = (totalDwell > 0f && s.DwellTicks > 0L) ? (float)s.DwellTicks / totalDwell : 0f;
                        bars.Add(new UIComponents.KpiBar
                        {
                            Caption = s.PlaceText + " · " + s.DwellText + " · " + Mathf.RoundToInt(share * 100f) + "%",
                            Share01 = share
                        });
                    }
                }
                while (bars.Count < 3)
                {
                    bars.Add(new UIComponents.KpiBar { Caption = "--", Share01 = 0f });
                }
                footBars = bars.ToArray();
            }

            // ===== ⑥ 神器传承：武器名(大值+同行历代击杀) + 标题右侧锻造者 + KPI行(代数/当代击杀) + 传承链进度条 =====
            // v1.1.4：锻造者从 KPI 行移到「神器传承」标题右侧（TitleSide）；传承链由行式改为进度条（击杀占比）。
            bool legacyOk = snap.Legacy != null && !snap.Legacy.IsEmpty;
            string legacyVal = (legacyOk && !string.IsNullOrEmpty(snap.Legacy.TitleText)) ? snap.Legacy.TitleText : "--";
            string legacyTotal = (legacyOk && snap.Legacy.TotalKills > 0)
                ? snap.Legacy.TotalKills + " " + "PersonalChronicle.UI.Kpi.Unit.Kills".Translate().ToString()
                : string.Empty;
            string legacyForger = (legacyOk && !string.IsNullOrEmpty(snap.Legacy.CreatedByText))
                ? snap.Legacy.CreatedByText
                : string.Empty;
            UIComponents.KpiRow[] legacyRows = legacyOk
                ? new UIComponents.KpiRow[]
                {
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Legacy.Gen".Translate().ToString(), Value = snap.Legacy.GenCount.ToString() + " " + "PersonalChronicle.UI.Legacy.GenUnit".Translate().ToString() },
                    new UIComponents.KpiRow { Label = "PersonalChronicle.UI.Legacy.CurrentKills".Translate().ToString(), Value = LegacyCurrentKills(snap.Legacy).ToString() + " " + "PersonalChronicle.UI.Kpi.Unit.Kills".Translate().ToString() },
                }
                : null;
            UIComponents.KpiBar[] legacyBars = null;
            // 传承链进度条：各代击杀占比（Share01 = 该代击杀 / 总击杀），Tag 右侧显示击杀数。
            // 固定 3 条：不足 3 代用 "--"/0 占位；无传承（IsEmpty）时也保持 3 条空槽，布局稳定。
            {
                List<UIComponents.KpiBar> bars = new List<UIComponents.KpiBar>();
                if (legacyOk && snap.Legacy.Holders != null && snap.Legacy.Holders.Count > 0)
                {
                    // 传承链：按各代击杀数降序取前 3（最高击杀优先），排名前缀显式标注名次。
                    List<LegacyHolderView> ranked = new List<LegacyHolderView>(snap.Legacy.Holders);
                    ranked.Sort((a, b) => (b != null ? b.KillCount : 0) - (a != null ? a.KillCount : 0));
                    int totalKills = snap.Legacy.TotalKills;
                    if (totalKills <= 0)
                    {
                        for (int i = 0; i < ranked.Count; i++)
                            if (ranked[i] != null) totalKills += ranked[i].KillCount;
                    }
                    int topN = Mathf.Min(3, ranked.Count);
                    for (int i = 0; i < topN; i++)
                    {
                        LegacyHolderView h = ranked[i];
                        if (h == null) continue;
                        string gen = h.IsCurrent ? "PersonalChronicle.UI.Legacy.Current".Translate().ToString()
                            : (h.IsFirst ? "PersonalChronicle.UI.Legacy.First".Translate().ToString() : "PersonalChronicle.UI.Legacy.Next".Translate().ToString());
                        string rank = "PersonalChronicle.UI.Legacy.ChainRank".Translate(i + 1).ToString();
                        float share = (totalKills > 0) ? (float)h.KillCount / (float)totalKills : 0f;
                        bars.Add(new UIComponents.KpiBar
                        {
                            Caption = rank + " · " + (h.HolderText ?? noRec) + " · " + gen + " · " + Mathf.RoundToInt(share * 100f) + "%",
                            Share01 = share,
                            Tag = h.KillCount + " " + "PersonalChronicle.UI.Kpi.Unit.Kills".Translate().ToString()
                        });
                    }
                }
                while (bars.Count < 3)
                {
                    bars.Add(new UIComponents.KpiBar { Caption = "--", Share01 = 0f });
                }
                legacyBars = bars.ToArray();
            }

            return new UIComponents.KpiCell[]
            {
                new UIComponents.KpiCell { KindKey = "work",   TitleKey = "PersonalChronicle.UI.Kpi.WorkTitle",   Value = workVal, Unit = workOk ? totalHoursUnit : null, InlineMetric = workRank, Rows = workRows, Bars = workBars },
                new UIComponents.KpiCell { KindKey = "prod",   TitleKey = "PersonalChronicle.UI.Kpi.ProdTitle",   Value = prodVal, Unit = prodOk ? silver : null, InlineMetric = prodInline, Rows = prodRows, Bars = prodBars },
                new UIComponents.KpiCell { KindKey = "kill",   TitleKey = "PersonalChronicle.UI.Kpi.Kill",   Value = killVal, Unit = killOk ? "PersonalChronicle.UI.Kpi.Unit.Kills".Translate().ToString() : null, InlineMetric = killOk ? killInline : null, Rows = killRows, Bars = killBars },
                new UIComponents.KpiCell { KindKey = "consume", TitleKey = "PersonalChronicle.UI.Kpi.ConsumeTitle", Value = consVal, Unit = consOk ? silver : null, InlineMetric = consOk ? consDailyInline : null, Rows = consRows, Bars = consBars },
                new UIComponents.KpiCell { KindKey = "foot",   TitleKey = "PersonalChronicle.UI.Kpi.FootTitle",   Value = footVal, Unit = footOk ? "PersonalChronicle.UI.InspectTab.Places".Translate().ToString() : null, InlineMetric = footDays, Rows = footRows, Bars = footBars },
                new UIComponents.KpiCell { KindKey = "legacy", TitleKey = "PersonalChronicle.UI.Kpi.Legacy", TitleSide = legacyForger, Value = legacyVal, InlineMetric = legacyTotal, Rows = legacyRows, Bars = legacyBars },
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
