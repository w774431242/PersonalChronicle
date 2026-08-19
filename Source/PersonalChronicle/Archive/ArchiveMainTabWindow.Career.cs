using System;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.ReadModels;
using PersonalChronicle.Archive.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// Partial of ArchiveMainTabWindow — v4.16 职业档案总览视图。
    /// 入口位于侧边栏；列表行点击 → OpenPawnDetail(tabIndex=1) 直接进入人物职业档案 Tab，
    /// 解决人物 inspect ITab 长款空间不足、难以承载完整职业生涯档案的问题。
    /// </summary>
    public sealed partial class ArchiveMainTabWindow
    {
        /// <summary>单行高度（含上下留白）。中文行高 ≥18f 经验值，行内 3 段文字布局。</summary>
        private const float CareerRowH = 72f;
        private const float CareerRowGap = 10f;

        private void DrawCareerContent(Rect inner, IArchiveService service)
        {
            Color prevColor = GUI.color;
            float headerH = 28f + 22f + 20f;   // 标题 + 副标题 + 间隔
            float colHeaderH = 26f;            // 列头
            float listH = cachedCareerRows.Count > 0
                ? cachedCareerRows.Count * (CareerRowH + CareerRowGap)
                : 120f;                        // 空态占位
            float contentHeight = headerH + colHeaderH + listH + 24f;
            float viewHeight = Mathf.Max(inner.height, contentHeight);
            Rect viewRect = new Rect(inner.x, inner.y, inner.width - 16f, viewHeight);

            Widgets.BeginScrollView(inner, ref careerScroll, viewRect);
            try
            {
                float y = viewRect.y + 4f;

                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 28f),
                    "PersonalChronicle.UI.CareerOverview".Translate().ToString());
                y += 28f;

                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.SecondaryText;
                Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 18f),
                    "PersonalChronicle.UI.CareerOverviewDesc".Translate().ToString());
                GUI.color = prevColor;
                Text.Font = GameFont.Small;
                y += 22f + 20f;

                // 列头
                DrawCareerColumnHeader(viewRect, y);
                y += colHeaderH;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.DrawLine(new Vector2(viewRect.x, y), new Vector2(viewRect.x + viewRect.width, y),
                    ArchiveUiStyle.Muted, UITheme.RuleHeight);
                GUI.color = prevColor;
                y += 8f;

                if (cachedCareerRows.Count == 0)
                {
                    Text.Font = GameFont.Small;
                    GUI.color = ArchiveUiStyle.SecondaryText;
                    Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 60f),
                        "PersonalChronicle.UI.CareerEmpty".Translate().ToString());
                    GUI.color = prevColor;
                    return;
                }

                for (int i = 0; i < cachedCareerRows.Count; i++)
                {
                    CareerOverviewRowView row = cachedCareerRows[i];
                    if (row == null)
                    {
                        continue;
                    }
                    Rect rowRect = new Rect(viewRect.x, y, viewRect.width, CareerRowH);
                    DrawCareerRow(rowRect, row, i);
                    if (Widgets.ButtonInvisible(rowRect))
                    {
                        OpenPawnDetail(service, row.StableId, 1);
                        return;
                    }
                    y += CareerRowH + CareerRowGap;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static void DrawCareerColumnHeader(Rect viewRect, float y)
        {
            float col1 = viewRect.width * 0.20f;   // 姓名
            float col2 = viewRect.width * 0.24f;   // 身份/方向
            float col3 = viewRect.width * 0.22f;   // 主技能 / 工时
            float col4 = viewRect.width - col1 - col2 - col3; // 下一职称进度

            Color prev = GUI.color;
            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.SecondaryText;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(new Rect(viewRect.x, y, col1 - 8f, 20f),
                "PersonalChronicle.UI.ColPawn".Translate().ToString());
            Widgets.Label(new Rect(viewRect.x + col1, y, col2 - 8f, 20f),
                "PersonalChronicle.UI.ColRole".Translate().ToString());
            Widgets.Label(new Rect(viewRect.x + col1 + col2, y, col3 - 8f, 20f),
                "PersonalChronicle.UI.ColSkill".Translate().ToString());
            Widgets.Label(new Rect(viewRect.x + col1 + col2 + col3, y, col4 - 8f, 20f),
                "PersonalChronicle.UI.ColNextTitle".Translate().ToString());
            GUI.color = prev;
            Text.Font = GameFont.Small;
        }

        /// <summary>绘制单行：卡片背景 + 四列信息（姓名 / 身份方向 / 主技能工时 / 下一职称进度条）。</summary>
        private void DrawCareerRow(Rect rowRect, CareerOverviewRowView row, int index)
        {
            Color prev = GUI.color;
            // 卡片背景（点击态高亮由 ButtonInvisible 负责，这里仅静态底）
            GUI.color = ArchiveUiStyle.Card;
            Widgets.DrawBoxSolid(rowRect, ArchiveUiStyle.Card);
            GUI.color = prev;

            float padX = 12f;
            float col1 = rowRect.width * 0.20f;
            float col2 = rowRect.width * 0.24f;
            float col3 = rowRect.width * 0.22f;
            float col4 = rowRect.width - col1 - col2 - col3;

            float x = rowRect.x + padX;
            float cy = rowRect.y + 10f;

            // 列1：姓名（主）+ 身份标注
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = ArchiveUiStyle.Text;
            Widgets.Label(new Rect(x, cy, col1 - padX - 8f, 24f), row.Name ?? "?");
            GUI.color = prev;

            // 列2：身份 / 方向
            Text.Font = GameFont.Tiny;
            if (string.IsNullOrEmpty(row.RoleName))
            {
                GUI.color = ArchiveUiStyle.SecondaryText;
                Widgets.Label(new Rect(x + col1, cy, col2 - 8f, 24f),
                    "PersonalChronicle.UI.NoTitle".Translate().ToString());
            }
            else
            {
                GUI.color = ArchiveUiStyle.Accent;
                Widgets.Label(new Rect(x + col1, cy, col2 - 8f, 22f), row.RoleName);
                GUI.color = ArchiveUiStyle.SecondaryText;
                Widgets.Label(new Rect(x + col1, cy + 22f, col2 - 8f, 18f), row.RoleDesc ?? string.Empty);
            }
            GUI.color = prev;

            // 列3：主技能 / 工时
            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Text;
            Widgets.Label(new Rect(x + col1 + col2, cy, col3 - 8f, 22f), row.SkillText ?? "--");
            GUI.color = ArchiveUiStyle.SecondaryText;
            Widgets.Label(new Rect(x + col1 + col2, cy + 22f, col3 - 8f, 18f), row.HoursText ?? "--");
            GUI.color = prev;

            // 列4：下一职称 + 进度条
            float progX = x + col1 + col2 + col3;
            float barW = col4 - 8f;
            Text.Font = GameFont.Tiny;
            if (string.IsNullOrEmpty(row.NextTitle))
            {
                GUI.color = ArchiveUiStyle.SecondaryText;
                Widgets.Label(new Rect(progX, cy, barW, 22f),
                    row.HasData ? "PersonalChronicle.UI.TitleMaxed".Translate().ToString()
                                : "PersonalChronicle.UI.NoCareerData".Translate().ToString());
            }
            else
            {
                GUI.color = ArchiveUiStyle.Text;
                Widgets.Label(new Rect(progX, cy, barW, 22f), row.NextTitle);
                // 进度条
                float barY = cy + 26f;
                float barH = 10f;
                Rect barRect = new Rect(progX, barY, barW, barH);
                UIComponents.ProgressBar(barRect, (float)row.Progress / 100f, ArchiveUiStyle.Accent);
            }
            GUI.color = prev;
            Text.Font = GameFont.Small;
        }
    }
}
