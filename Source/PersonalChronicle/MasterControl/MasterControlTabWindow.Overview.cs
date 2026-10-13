using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl
{
    /// <summary>
    /// Partial of <see cref="MasterControlTabWindow"/> ——中心注册表视图（总控中心主视图）：
    /// 殖民地现状四格 + 中心卡片网格（初始 / 预留两组）。**本视图就是路由入口**：
    /// 每张卡片 = 一个系统中心的入口，点进去是该中心自己的页面；不设侧栏导航。
    ///
    /// <para>预留中心与「数据未接入」的中心都只显示清单态（无 KPI）——用样例数值填充会掩盖
    /// 「玩法未落地 / 数据源没接上」这一事实，观测面必须诚实（规范书 P6 / LAND-009）。
    /// 徽标三态（运行中 / 数据未接入 / 预留）由 Read Model 的 <see cref="SystemCenterWiring"/> 决定。</para>
    /// </summary>
    public sealed partial class MasterControlTabWindow
    {
        private void DrawRegistryContent(Rect inner)
        {
            float width = Mathf.Max(1f, inner.width - UITheme.ScrollbarThickness);
            Rect viewRect;
            BeginContentScroll(inner, ComputeRegistryHeight(width), out viewRect);
            // 引导锚点（CHG-20261006-TOUR2）：注册表**内容区**整体 → 屏幕坐标（页头锚点在 Shell 上报）。
            // 用视口矩形而非单张卡片：卡片在滚动内容里，逐卡上报需要把组内局部坐标换算回窗口坐标，
            // 而"整块列表区"足够表达"这里每张卡都是一个中心"这一步。
            if (TourAnchorRegistry.Active)
            {
                TourAnchorRegistry.Report(TourAnchorRegistry.AnchorContentArea, ToScreenRect(inner));
            }
            try
            {
                IReadOnlyList<SystemCenterCardView> initial = InitialCards;
                IReadOnlyList<SystemCenterCardView> reserved = ReservedCards;
                int total = (initial != null ? initial.Count : 0) + (reserved != null ? reserved.Count : 0);
                int columns = CenterGridColumns(viewRect.width);

                float y = viewRect.y + 4f;
                y = DrawPageTitle(viewRect, y,
                    Translate("PersonalChronicle.UI.MasterControl.Overview.Title"),
                    Translate("PersonalChronicle.UI.MasterControl.Overview.Api", total),
                    Translate("PersonalChronicle.UI.MasterControl.PageIntro"));

                y = DrawSectionHeader(viewRect, y,
                    Translate("PersonalChronicle.UI.MasterControl.Section.InitialCenters"));
                DrawCenterGrid(viewRect, y, initial, columns);
                y += GridBlockHeight(initial != null ? initial.Count : 0,
                    columns, CenterCardHeight, CenterCardGap) + SectionGap;

                y = DrawSectionHeader(viewRect, y,
                    Translate("PersonalChronicle.UI.MasterControl.Section.ReservedCenters"));
                DrawCenterGrid(viewRect, y, reserved, columns);
                y += GridBlockHeight(reserved != null ? reserved.Count : 0,
                    columns, CenterCardHeight, CenterCardGap) + SectionGap;

                DrawNoticeBox(viewRect, y, Translate("PersonalChronicle.UI.MasterControl.PageFooter"));
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>中心卡片网格（列数由 <see cref="CenterGridColumns"/> 按可用宽推导；行数由调用方用同一列数估算高度）。</summary>
        private void DrawCenterGrid(Rect rect, float y, IReadOnlyList<SystemCenterCardView> cards, int columns)
        {
            if (cards == null || cards.Count == 0)
            {
                UIComponents.Label(new Rect(rect.x, y, rect.width, LineRowHeight),
                    Translate("PersonalChronicle.UI.MasterControl.Empty"), GameFont.Small, UITheme.Dim);
                return;
            }

            float gap = CenterCardGap;
            int safeColumns = Mathf.Max(1, columns);
            float cardWidth = Mathf.Max(140f,
                (rect.width - gap * (safeColumns - 1)) / safeColumns);
            for (int i = 0; i < cards.Count; i++)
            {
                int row = i / safeColumns;
                int col = i % safeColumns;
                Rect cardRect = new Rect(
                    rect.x + col * (cardWidth + gap),
                    y + row * (CenterCardHeight + gap),
                    cardWidth, CenterCardHeight);
                DrawCenterCard(cardRect, cards[i]);
            }
        }

        /// <summary>单张中心卡片：状态徽标 / 名称 / 稳定 ID + 档位 / 职责 / 依赖 + 首项 KPI。</summary>
        private void DrawCenterCard(Rect rect, SystemCenterCardView card)
        {
            if (card == null)
            {
                return;
            }

            UIComponents.Card(rect, card.Reserved ? UITheme.Dim : UITheme.Accent);
            Widgets.DrawHighlightIfMouseover(rect);

            float pad = UITheme.CardPadX;
            float innerWidth = Mathf.Max(20f, rect.width - pad * 2f);

            // 状态徽标（右上，宽度实测并夹到卡片 42% 以内，长状态文案不撑破卡片）。
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            float badgeWidth = Mathf.Clamp(Text.CalcSize(card.StatusText).x + 16f, 40f, innerWidth * 0.42f);
            Text.Font = prevFont;
            UIComponents.Badge(new Rect(rect.xMax - pad - badgeWidth, rect.y + 6f, badgeWidth, 18f),
                card.StatusText, StatusColor(card.Wiring));

            float titleWidth = Mathf.Max(40f, innerWidth - badgeWidth - 6f);
            UIComponents.Label(new Rect(rect.x + pad, rect.y + 5f, titleWidth, 22f),
                UIComponents.TruncateToWidth(card.Label, titleWidth, GameFont.Small),
                GameFont.Small, UITheme.Text);

            string sub = Translate2("PersonalChronicle.UI.MasterControl.Card.Sub",
                card.DefName, card.Tier);
            // ⚠ 副标题行高 16 会让文字下缘溢出、被下面 43f 起画的「自持情况」行压住 ⇒ 补足 18 并下移 2px。
            UIComponents.Label(new Rect(rect.x + pad, rect.y + 26f, innerWidth, PopulationRowHeight),
                UIComponents.TruncateToWidth(sub, innerWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim);

            UIComponents.Label(new Rect(rect.x + pad, rect.y + 46f, innerWidth, PopulationRowHeight),
                UIComponents.TruncateToWidth(card.OwnText, innerWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Muted);

            // 底行：左侧依赖摘要，右侧首项 KPI（无数据则整段留空，不画假数值）。
            string head = card.HeadKpiValue;
            float headWidth = 0f;
            if (!string.IsNullOrEmpty(head))
            {
                string headText = Translate2("PersonalChronicle.UI.MasterControl.Card.HeadKpi",
                    card.HeadKpiLabel, head);
                Text.Font = GameFont.Tiny;
                headWidth = Mathf.Min(Text.CalcSize(headText).x + 8f, innerWidth * 0.45f);
                Text.Font = prevFont;
                UIComponents.Label(new Rect(rect.xMax - pad - headWidth, rect.y + 62f, headWidth, 18f),
                    UIComponents.TruncateToWidth(headText, headWidth, GameFont.Tiny),
                    GameFont.Tiny, ToneColor(card.HeadKpiTone), TextAnchor.MiddleRight);
            }

            float dependsWidth = Mathf.Max(20f, innerWidth - (headWidth > 0f ? headWidth + 8f : 0f));
            UIComponents.Label(new Rect(rect.x + pad, rect.y + 62f, dependsWidth, 18f),
                UIComponents.TruncateToWidth(card.DependsText, dependsWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim);

            TooltipHandler.TipRegion(rect, card.TooltipText);
            // TOUR2 起不再抑制跳转：引导改为"允许玩家自由操作 + 条件自动推进"，
            // 点卡片进制造中心本身就是引导期望的动作（步骤 6 的完成条件就是它）。
            if (Widgets.ButtonInvisible(rect))
            {
                GoCenter(card.DefName);
            }
        }

    }
}
