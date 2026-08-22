// ITab_Pawn_Career partial：履历子页（工作经历 resume-block + 工坊汇总 + 当前就职）。
// ARC-013 文件治理，物理切片零契约改动；见主文件 ITab_Pawn_Career.cs 类文档。
using System;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.ReadModels;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    public partial class ITab_Pawn_Career
    {
        // ================= 子页：履历 =================
        private void DrawCareerResumeTab(Rect rect, DetailSnapshot snap)
        {
            // 原生 BeginScrollView 用法（对齐 ArchiveMainTabWindow）：不自定义背景，
            // 让 rimworld 窗口默认背景生效——避免自定义 TintedBox 干扰造成的白屏。
            Rect view = new Rect(rect.x, rect.y, rect.width, rect.height);
            float innerW = view.width - 16f;
            float contentH = EstimateResumeH(snap);
            contentH = Mathf.Max(contentH, view.height);
            Widgets.BeginScrollView(view, ref scroll, new Rect(view.x, view.y, innerW, contentH));
            try
            {
                float y = view.y + 4f;
                UIComponents.SectionTitle(new Rect(view.x, y, innerW, 0f), y,
                    "PersonalChronicle.UI.Career.Resume.Title".Translate().ToString());
                y += UITheme.SectionTitleHeight;

                if (snap.WorkExperiences == null || snap.WorkExperiences.Count == 0)
                {
                    UIComponents.Label(new Rect(view.x, y, innerW, 40f),
                        "PersonalChronicle.UI.Career.Resume.Empty".Translate().ToString(),
                        UITheme.FontBody, UITheme.Muted);
                    y += 48f;
                }
                else
                {
                    for (int i = 0; i < snap.WorkExperiences.Count; i++)
                    {
                        y = DrawResumeBlock(view, y, innerW, snap.WorkExperiences[i]);
                    }
                }
                y += UITheme.SpaceSm;

                // two-col：工坊汇总 + 当前就职（并排同一 y）。
                float colW = (innerW - UITheme.Gap) / 2f;
                UIComponents.SectionTitle(new Rect(view.x, y, innerW, 0f), y,
                    "PersonalChronicle.UI.Career.Resume.Summary".Translate().ToString());
                y += UITheme.SectionTitleHeight;
                float colStartY = y;
                float sumEndY = DrawSummaryCol(view, colStartY, colW, snap);
                float curEndY = DrawCurrentCol(view, colStartY, colW, snap);
                y = Mathf.Max(sumEndY, curEndY) + UITheme.SpaceMd;
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static float EstimateResumeH(DetailSnapshot snap)
        {
            float h = 30f;
            if (snap.WorkExperiences != null)
            {
                for (int i = 0; i < snap.WorkExperiences.Count; i++)
                {
                    if (snap.WorkExperiences[i] == null) continue;
                    h += 22f + 20f + UITheme.SpaceXs + 8f;
                }
            }
            // v4.17 体检：尾部按绘制路径逐段对齐（旧 30+30+8*26+SpaceMd=284 少算约 222px）：
            // SpaceSm(12) + 汇总 SectionTitle(30) + 两列面板各 (8*26+8+SpaceXs) + SpaceMd(16)。
            h += UITheme.SpaceSm
                + UITheme.SectionTitleHeight
                + 2f * (8f * 26f + 8f + UITheme.SpaceXs)
                + UITheme.SpaceMd;
            return h;
        }

        // 紧凑工作经历卡：时间轴竖线 + 一行标题（工坊 + 时间区间右对齐）+ 一行 meta（房间·使用·成果）
        private float DrawResumeBlock(Rect outer, float y, float width, WorkExperienceView w)
        {
            float blockH = 22f + 20f + UITheme.SpaceXs;
            Rect block = new Rect(outer.x, y, width, blockH);
            // 时间轴竖线（左）
            Rect accent = new Rect(block.x, block.y + 2f, 2f, blockH - 4f);
            Widgets.DrawBoxSolid(accent, UITheme.PillGold);

            float innerX = block.x + 10f;
            float innerW = width - 14f;
            string org = w.WorkplaceLabel ?? "--";
            string period = !string.IsNullOrEmpty(w.PeriodText) ? w.PeriodText : "--";
            UIComponents.Label(new Rect(innerX, block.y, innerW * 0.62f, 22f),
                UIComponents.TruncateToWidth(org, innerW * 0.62f, UITheme.FontBody),
                UITheme.FontBody, UITheme.Text);
            // 时间区间：右对齐小字，强调时间维度
            UIComponents.Label(new Rect(innerX + innerW * 0.62f, block.y, innerW * 0.38f, 22f),
                UIComponents.TruncateToWidth(period, innerW * 0.38f, UITheme.FontLabel),
                UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleRight);

            // meta 行：房间 · 使用 · 成果（一行合并，紧凑）
            float subY = block.y + 24f;
            string ach = (w.Achievements != null && w.Achievements.Count > 0)
                ? " · " + string.Join("，", w.Achievements)
                : "";
            string meta = "PersonalChronicle.UI.Career.Resume.Room".Translate(
                string.IsNullOrEmpty(w.RoomRoleLabel) ? "--" : w.RoomRoleLabel)
                + " · " + "PersonalChronicle.UI.Career.Resume.Uses".Translate(w.UseCount.ToString())
                + ach;
            UIComponents.Label(new Rect(innerX, subY, innerW, 18f),
                UIComponents.TruncateToWidth(meta, innerW, UITheme.FontLabel),
                UITheme.FontLabel, UITheme.Muted);
            return y + blockH + 8f;
        }

        private float DrawSummaryCol(Rect view, float y, float colW, DetailSnapshot snap)
        {
            Rect panel = new Rect(view.x, y, colW, 8f * 26f + 8f);
            UIComponents.Panel(panel, UITheme.Panel);
            PawnObject po = snap != null ? snap.DetailObject as PawnObject : null;
            int workshops = snap != null && snap.WorkExperiences != null ? snap.WorkExperiences.Count : 0;
            int made = 0;
            if (snap != null && snap.WorkExperiences != null)
            {
                for (int i = 0; i < snap.WorkExperiences.Count; i++)
                {
                    if (snap.WorkExperiences[i] != null) made += snap.WorkExperiences[i].ProducedCount;
                }
            }
            // UI-001：事实计数统一消费快照 FactCounts，不再直查 Domain（移除 CountEvents）。
            CareerFactCounts fc = snap != null ? snap.FactCounts : null;
            int built = fc != null ? fc.ConstructionCompleted : 0;
            int researched = fc != null ? fc.ResearchCompleted : 0;
            int books = po != null && po.CareerData != null && po.CareerData.Books != null ? po.CareerData.Books.Count : 0;
            float ry = panel.y + 6f;
            ry = SnapshotRow(panel, ry, "PersonalChronicle.UI.Career.Resume.Summary.Workshops".Translate().ToString(), workshops.ToString());
            ry = SnapshotRow(panel, ry, "PersonalChronicle.UI.Career.Resume.Summary.Made".Translate().ToString(), made.ToString());
            ry = SnapshotRow(panel, ry, "PersonalChronicle.UI.Career.Resume.Summary.Built".Translate().ToString(), built.ToString());
            ry = SnapshotRow(panel, ry, "PersonalChronicle.UI.Career.Resume.Summary.Research".Translate().ToString(), researched.ToString());
            SnapshotRow(panel, ry, "PersonalChronicle.UI.Career.Resume.Summary.Books".Translate().ToString(), books.ToString());
            return panel.yMax + UITheme.SpaceXs;
        }

        private float DrawCurrentCol(Rect view, float y, float colW, DetailSnapshot snap)
        {
            Rect panel = new Rect(view.x + colW + UITheme.Gap, y, colW, 8f * 26f + 8f);
            UIComponents.Panel(panel, UITheme.Panel);
            PawnObject po = snap != null ? snap.DetailObject as PawnObject : null;
            string workshop = "--";
            string room = "--";
            int useCount = 0;
            long lastTick = -1L;
            if (po != null && po.Workplace != null && !po.Workplace.IsEmpty)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(po.Workplace.BuildingDefName);
                workshop = def != null ? def.LabelCap : po.Workplace.BuildingDefName;
                RoomRoleDef rr = !string.IsNullOrEmpty(po.Workplace.RoomRoleDefName)
                    ? DefDatabase<RoomRoleDef>.GetNamedSilentFail(po.Workplace.RoomRoleDefName) : null;
                room = rr != null ? rr.LabelCap : "--";
                useCount = po.Workplace.UseCount;
                lastTick = po.Workplace.LastUsedTick;
            }
            string lastUsed = lastTick >= 0L ? GenDate.Year(lastTick, 0f).ToString() : "--";
            float ry = panel.y + 6f;
            ry = SnapshotRow(panel, ry, "PersonalChronicle.UI.Career.Resume.Current.Workshop".Translate().ToString(), workshop);
            ry = SnapshotRow(panel, ry, "PersonalChronicle.UI.Career.Resume.Current.Room".Translate().ToString(), room);
            ry = SnapshotRow(panel, ry, "PersonalChronicle.UI.Career.Resume.Current.Uses".Translate().ToString(), useCount.ToString());
            SnapshotRow(panel, ry, "PersonalChronicle.UI.Career.Resume.Current.LastUsed".Translate().ToString(), lastUsed);
            return panel.yMax + UITheme.SpaceXs;
        }

        private float SnapshotRow(Rect panel, float y, string label, string value)
        {
            UIComponents.Label(new Rect(panel.x + 8f, y, panel.width * 0.6f, 20f),
                UIComponents.TruncateToWidth(label, panel.width * 0.6f, UITheme.FontLabel),
                UITheme.FontLabel, UITheme.Muted);
            UIComponents.Label(new Rect(panel.x + panel.width * 0.6f, y, panel.width * 0.4f - 8f, 20f),
                UIComponents.TruncateToWidth(value, panel.width * 0.4f - 8f, UITheme.FontBody),
                UITheme.FontBody, UITheme.Text, TextAnchor.UpperRight);
            return y + 26f;
        }
    }
}
