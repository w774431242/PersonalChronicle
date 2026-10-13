using PersonalChronicle.Archive;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>
    /// Partial of <see cref="PersonnelArchivePanel"/> ——**嵌入档渲染入口**（预览 <c>#v-archive</c> 形态）。
    ///
    /// <para>独立档案馆窗口、首页与侧栏已移除。本入口只负责人员中心内部的
    /// 分类浏览及档案详情，不依赖原版主标签页生命周期。</para>
    ///
    /// <para><b>几何真值来自预览 CSS</b>（LAND-001 先量后写；括号内为 HTML/CSS 行号）：
    /// <c>.arc-hd{height:56px;padding:0 13px}</c>(337-339) ·
    /// <c>.arc-bd{padding:10px}</c>(347) · <c>.arc-main{gap:9px;padding:10px}</c>(361-362) ·
    /// <c>.ovkpi</c>(291) · <c>.ovcard</c>(299-301) · <c>.ovcard .stack{height:7px}</c>(310) ·
    /// 表格 <c>th{font-size:9px;padding:5px 7px}</c>/<c>td{padding:3.5px 7px}</c>(254-259)。</para>
    ///
    /// <para><b>明确不画</b>：<c>DrawHeader</c> / <c>DrawSidebar</c> / <c>DrawCompactNav</c>。
    /// 下钻（人物/武器/事件详情、手册）仍复用既有绘制 —— 嵌入档不是只读展板。</para>
    /// </summary>
    public sealed partial class PersonnelArchivePanel
    {
        /// <summary>`.arc-hd` 头部高（预览 56px = <see cref="UITheme.HeaderHeight"/>，同源）。</summary>
        private const float EmbeddedHeaderHeight = 56f;

        /// <summary>`.seal` 圆印边长（预览 28×28；圆角降级为直角，登记 B 类）。</summary>
        private const float EmbeddedSealSize = 28f;

        /// <summary>`.arc-hd{padding:0 13px}`。</summary>
        private const float EmbeddedHeaderPadX = 13f;

        /// <summary>`.arc-bd{padding:10px}` —— 容器四周内缩。</summary>
        private const float EmbeddedBodyPadding = 10f;

        /// <summary>
        /// `.arc-main{padding:10px}` —— **第二层**内缩（预览是 `arc-bd` → `arc-main` 两层嵌套，
        /// 实测内容宽 1810.4 = 1852 − 10×2(bd) − 10×2(main)）。旧实现只缩了一层
        /// ⇒ 内容比预览**每侧宽 10px**，分类卡也随之变宽（1:1 实测修正）。
        /// </summary>
        private const float EmbeddedMainPadding = 10f;

        /// <summary>`.arc-main{gap:9px}` —— 区块间距。</summary>
        private const float EmbeddedBlockGap = 9f;

        /// <summary>滚动条预留宽（`.arc-main{overflow:auto}`，估算与绘制必须同源）。</summary>
        private const float EmbeddedScrollbarWidth = 16f;

        /// <summary>嵌入档最小可渲染尺寸；不足则如实提示（LAND-006 降级档 4）。</summary>
        private const float EmbeddedMinWidth = 320f;
        private const float EmbeddedMinHeight = 200f;

        /// <summary>
        /// 嵌入档入口：自画一层 <c>.arc</c> 容器（arc-hd + arc-bd/arc-main），
        /// **不画** Header / Sidebar / CompactNav。由 <c>PersonnelArchiveHost</c> 在 GUI 组内驱动。
        /// </summary>
        internal void DrawEmbeddedView(Rect rect, bool pawnDetailView = false)
        {
            IArchiveService service = PersonalChronicleMod.ArchiveService;
            UIComponents.Panel(rect);
            if (service == null)
            {
                DrawNoService(rect);
                return;
            }
            if (rect.width < EmbeddedMinWidth || rect.height < EmbeddedMinHeight)
            {
                UIComponents.Label(rect.ContractedBy(10f), "PersonalChronicle.UI.Archive.TooSmall".Translate().ToString(),
                    GameFont.Small, UITheme.Muted, TextAnchor.MiddleCenter);
                return;
            }
            RefreshCacheIfNeeded(service);

            // ══ 「人物档案」tab（预览 `#v-pawn`）══════════════════════════════════
            // ⚠ 2026-09-15 落地：此前总控的 tab 1 / tab 2 **共用本入口** ⇒ 两者恒落到
            //   `DrawEmbeddedOverview`（分类浏览），表现为「人物档案 tab 与档案总览内容相同」
            //   （用户报「剩余 2 个 tab 页内容未落地」的直接原因）。
            //   现在 tab 2 走本分支：**跳过 arc-hd**（预览 `#v-pawn` 顶部是 Hero 带，不是档案馆标题），
            //   直接画**详情页**（Hero 带 + 5 子 Tab：总览 / 生涯 / 战斗履历 / 社会关系 / 健康）。
            // 两个人员子页共用当前面板的选中对象；参数区分渲染模式，避免切换人物子页
            // 时覆写“档案总览”的浏览落点。
            if (pawnDetailView)
            {
                if (cachedDetailObject == null)
                {
                    // 【2026-09-16 实机反馈修复】此前未选中就直接画空态 ⇒ 用户点开「人物档案」
                    //   看到的是**两行提示 + 整屏空白**（预览 `#v-pawn` 则永远显示一份完整档案）
                    //   ⇒ 观感等同「该 tab 没落地」。
                    //   现在：**自动落点默认对象**（本 tab 的第一眼必须有内容），做法是调用
                    //   既有 public 入口 `RequestPawnDetail(stableId)` —— **唯一**被允许写选区的地方
                    //   （`PawnTable.cs` 点行走的就是它），仍然不直接写 `view` / `detailObjectId` 字段。
                    if (TryAutoPickPawnForDetail())
                    {
                        DrawDetailContent(rect.ContractedBy(EmbeddedBodyPadding), service);
                        return;
                    }
                    // 一个可归档对象都没有（空档）：才画空态说明（如实，不伪造）。
                    DrawEmbeddedPawnEmpty(rect);
                    return;
                }
                DrawDetailContent(rect.ContractedBy(EmbeddedBodyPadding), service);
                return;
            }

            Rect header = new Rect(rect.x, rect.y, rect.width, EmbeddedHeaderHeight);
            DrawEmbeddedHeader(header);
            // `.arc-hd{border-bottom:1px solid var(--accent)}`
            UIComponents.Rule(new Rect(rect.x, header.yMax - 1f, rect.width, UITheme.RuleHeight), UITheme.Accent);

            Rect body = new Rect(rect.x, header.yMax, rect.width,
                Mathf.Max(0f, rect.height - EmbeddedHeaderHeight));
            // 两层内缩：`.arc-bd{padding:10px}` + `.arc-main{padding:10px}`（实测合计 20/侧）。
            Rect inner = body.ContractedBy(EmbeddedBodyPadding + EmbeddedMainPadding);

            switch (view)
            {
                case MainView.PawnDetail:
                case MainView.WeaponDetail:
                    DrawDetailContent(inner, service);
                    return;
                case MainView.EventDetail:
                    DrawEventContent(inner, service);
                    return;
                case MainView.Manual:
                    DrawManualContent(inner);
                    return;
                default:
                    DrawEmbeddedOverview(inner, service);
                    return;
            }
        }

        /// <summary>
        /// 「人物档案」tab 的**默认落点**：本 tab 首次绘制且尚无选中对象时，取人物分类的第 1 个对象
        /// 作为默认展示对象（预览 `#v-pawn` 永远有一份档案）。
        ///
        /// <para>⚠ 只调既有 public 入口 <see cref="RequestPawnDetail"/>（点行同款），**不**直接写
        /// <c>view</c> / <c>detailObjectId</c> 字段，保持人员中心内部导航一致。
        /// 返回 true = 已成功落点（调用方随即画详情页）。</para>
        /// </summary>
        private bool TryAutoPickPawnForDetail()
        {
            List<ArchiveObject> pawns;
            if (!cachedCategoryObjects.TryGetValue(ArchiveCategoryKeys.Pawn, out pawns)
                || pawns == null || pawns.Count == 0)
            {
                return false;
            }
            string stableId = pawns[0] != null ? pawns[0].StableId : null;
            if (string.IsNullOrEmpty(stableId))
            {
                return false;
            }
            RequestPawnDetail(stableId);
            return cachedDetailObject != null;
        }

        /// <summary>
        /// 「人物档案」tab 的**空态**（仅在**一个可归档对象都没有**时出现）：如实说明
        /// （在「档案总览」的人物分类里点选一行即可）。不画假数据、不回落成分类浏览。
        /// </summary>
        private void DrawEmbeddedPawnEmpty(Rect rect)
        {
            Rect box = rect.ContractedBy(EmbeddedBodyPadding);
            UIComponents.Label(new Rect(box.x, box.y + rect.height * 0.35f, box.width, 44f),
                "PersonalChronicle.UI.Archive.PawnDetailEmpty".Translate().ToString(),
                GameFont.Small, UITheme.Muted, TextAnchor.UpperCenter);
        }

        /// <summary>
        /// 物品行点击的落点：打开该物品的档案。
        /// **复用既有 <c>OpenWeaponDetail</c>**（它已负责缓存重建与视图切换），不重写物品档案绘制。
        /// </summary>
        internal void RequestWeaponDetail(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return;
            }
            IArchiveService service = PersonalChronicleMod.ArchiveService;
            if (service == null)
            {
                return;
            }
            OpenWeaponDetail(service, stableId);
        }

        /// <summary>
        /// `arc-hd`：`[档 seal] [殖民地名 15px/800]「档案总览 · 分类浏览」徽章 …… [返回键]`。
        /// 标题**只在这里出现一次**（预览注释「只消除标题重复三次」，HTML 1012-1013）。
        /// 主题开关由外层总控页头 Shell.cs 统一承载，本页头不再重复（单一来源）。
        /// </summary>
        private void DrawEmbeddedHeader(Rect rect)
        {
            float x = rect.x + EmbeddedHeaderPadX;
            float midY = rect.y + rect.height * 0.5f;

            // 印章（预览 `.seal` 28×28 圆 + 1.5px accent 边；引擎无圆角 ⇒ 直角正方形，B 类降级）
            Rect seal = new Rect(x, midY - EmbeddedSealSize * 0.5f, EmbeddedSealSize, EmbeddedSealSize);
            Widgets.DrawBoxSolid(seal, UITheme.PanelRaised);
            UIComponents.Border(seal, UITheme.Accent);
            UIComponents.Label(seal, "PersonalChronicle.UI.Archive.Seal".Translate().ToString(),
                GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleCenter);
            x = seal.xMax + 9f;

            // 标题 15px/800 → GameFont.Small（LAND-007：13–15px 归 Small 档）
            float titleWidth = Mathf.Min(320f, Mathf.Max(60f, rect.width * 0.32f));
            UIComponents.Label(new Rect(x, midY - UITheme.FontBodyLineHeight * 0.5f, titleWidth,
                    UITheme.FontBodyLineHeight),
                UIComponents.TruncateToWidth("PersonalChronicle.UI.ColonyArchive".Translate().ToString(),
                    titleWidth, GameFont.Small),
                GameFont.Small, UITheme.Text, TextAnchor.MiddleLeft);
            x += titleWidth + 10f;

            // 口径徽章（预览 `.bdg`「档案总览 · 分类浏览」= Key `…UI.OverviewDesc`）
            float badgeWidth = Mathf.Min(220f, Mathf.Max(0f, rect.width - (x - rect.x) - 150f));
            if (badgeWidth > 60f)
            {
                UIComponents.Badge(new Rect(x, midY - 9f, badgeWidth, 18f),
                    "PersonalChronicle.UI.OverviewDesc".Translate().ToString(), UITheme.Dim);
            }

            // 主题开关已移除：外层总控页头 Shell.cs 已承载主题开关（单一来源，避免重复控件 / 重复宽度预留）。
            // 本嵌入档页头只保留标题 / 徽章 / 返回键，不再为主题开关预留右端宽度（LAND-007）。

            // ══ 详情视图的**返回键**（2026-09-16 用户报「档案总览点击 pawn 档案时无返回键」）══
            // 根因：嵌入档**不画 Header / Sidebar / CompactNav**（见 DrawEmbeddedView 注释）⇒
            //   在「档案总览」pane 里点 pawn 行 → `RequestPawnDetail` 把 `view` 切成 PawnDetail，
            //   此后**没有任何回退入口**（切走 pane 再切回，`view` 字段仍是详情、不重置）⇒ 卡死。
            // 落点 = 档案总览分类浏览，**保留当前分类筛选**（`overviewCategoryFilter` 原样回传）。
            // 形态与总控头部返回键同款：`Widgets.ButtonText` + 实测文本宽 + 悬停提示。
            if (view == MainView.PawnDetail || view == MainView.WeaponDetail
                || view == MainView.EventDetail)
            {
                string backLabel = "PersonalChronicle.UI.Back".Translate().ToString();
                GameFont prevFont = Verse.Text.Font;
                Verse.Text.Font = GameFont.Small;
                float backWidth = Verse.Text.CalcSize(backLabel).x + 24f;
                Verse.Text.Font = prevFont;
                Rect back = new Rect(rect.xMax - EmbeddedHeaderPadX - backWidth,
                    midY - UIComponents.ThemeSwitchHeight * 0.5f, backWidth,
                    UIComponents.ThemeSwitchHeight);
                // 右端放不下（窄档）或会压到左端标题 / 徽章 ⇒ 不画（宁可缺键，也不画重叠控件）。
                if (back.x > rect.x + 8f)
                {
                    if (Widgets.ButtonText(back, backLabel))
                    {
                        GoOverview(overviewCategoryFilter);
                    }
                    TooltipHandler.TipRegion(back, "PersonalChronicle.UI.Archive.BackTip".Translate().ToString());
                }
            }
        }

        /// <summary>嵌入档主体：① 档案统计带 → ② 四分类卡阵列 → ③ 明细 pane（预览 1032-1261）。</summary>
        private void DrawEmbeddedOverview(Rect inner, IArchiveService service)
        {
            float y = inner.y;
            // 统计带高 = 4 卡 + 时段条区块（预览 `.ovstat` 是「4 KPI + 时限条」的竖排）。
            y = DrawEmbeddedStats(
                new Rect(inner.x, y, inner.width, EmbeddedStatStripHeight + EmbeddedTimelineBlockHeight),
                service);
            y += EmbeddedBlockGap;
            y = DrawEmbeddedCategoryCards(
                new Rect(inner.x, y, inner.width, EmbeddedCategoryCardHeight), service);
            y += EmbeddedBlockGap;

            Rect pane = new Rect(inner.x, y, inner.width, Mathf.Max(0f, inner.yMax - y));
            DrawEmbeddedPane(pane, service);
        }

        /// <summary>
        /// 明细 pane：同一时刻**只画一个分类**（预览 `.catpane` 切换语义，HTML 1083/1136/1171/1214）。
        /// 人物走新 12 列表；物品/战役/地点复用既有区块绘制（战役 250×150 与地点 250×176
        /// 已与预览一致 ⇒ 不重写，避免同功能两处实现）。
        /// </summary>
        private void DrawEmbeddedPane(Rect pane, IArchiveService service)
        {
            if (pane.width < 40f || pane.height < 40f)
            {
                return;
            }
            string key = string.IsNullOrEmpty(overviewCategoryFilter)
                ? ArchiveCategoryKeys.Pawn
                : overviewCategoryFilter;
            List<ArchiveObject> objects;
            if (!cachedCategoryObjects.TryGetValue(key, out objects) || objects == null || objects.Count == 0)
            {
                UIComponents.Label(pane, "PersonalChronicle.UI.NoCategoryData".Translate().ToString(),
                    GameFont.Small, UITheme.Dim, TextAnchor.UpperLeft);
                return;
            }
            if (key == ArchiveCategoryKeys.Pawn)
            {
                DrawEmbeddedPawnTable(pane, objects, service);
                return;
            }
            if (key == ArchiveCategoryKeys.Thing)
            {
                // 物品：7 列明细表（预览 #cat-thing，HTML 1136-1168）
                DrawEmbeddedThingTable(pane, objects, service);
                return;
            }

            float contentWidth = Mathf.Max(1f, pane.width - EmbeddedScrollbarWidth);
            float contentHeight = Mathf.Max(pane.height, EmbeddedPaneHeight(contentWidth, key, objects));
            Rect viewRect = new Rect(pane.x, pane.y, contentWidth, contentHeight);
            Widgets.BeginScrollView(pane, ref embeddedPaneScroll, viewRect);
            try
            {
                DrawOverviewSection(new Rect(viewRect.x, viewRect.y + 2f, viewRect.width, contentHeight),
                    viewRect.y + 2f, key, objects, service);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>非人物 pane 的内容高（复用既有 section 的行数公式，估算与绘制同源）。</summary>
        private float EmbeddedPaneHeight(float width, string categoryKey, List<ArchiveObject> objects)
        {
            float cardWidth = 190f;
            float cardHeight = 70f;
            if (categoryKey == ArchiveCategoryKeys.Battle)
            {
                cardWidth = BattleCardWidth;
                cardHeight = BattleCardHeight;
            }
            else if (categoryKey == ArchiveCategoryKeys.Location)
            {
                cardWidth = LocationCardWidth;
                cardHeight = LocationCardHeight;
            }
            // 卡片网格间距 = **10**（预览 `.bcard/.lcard` 外层 `display:grid;gap:10px`，实测 10）。
            const float gap = 10f;
            int perRow = Mathf.Max(1, (int)((width + gap) / (cardWidth + gap)));
            int rows = (objects.Count - 1) / perRow + 1;
            float height = 30f + rows * (cardHeight + gap) + 14f;
            if (categoryKey == ArchiveCategoryKeys.Location)
            {
                height += LocationKpiStripHeight + 8f;
            }
            else if (categoryKey == ArchiveCategoryKeys.Battle)
            {
                height += BattleKpiStripHeight + 8f;
            }
            return height + 8f;
        }
    }
}
