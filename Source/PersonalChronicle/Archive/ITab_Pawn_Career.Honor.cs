// ITab_Pawn_Career partial：勋章子页（勋章墙 + 荣誉贡献结构 + 最近荣誉事件）。
// ARC-013 文件治理，物理切片零契约改动；见主文件 ITab_Pawn_Career.cs 类文档。
using System;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.ReadModels;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    public partial class ITab_Pawn_Career
    {
        // ================= 子页：勋章 =================
        private void DrawHonorTab(Rect rect, Pawn pawn, DetailSnapshot snap)
        {
            Rect view = new Rect(rect.x, rect.y, rect.width, rect.height);
            float innerW = view.width - UITheme.ScrollbarThickness;
            float contentH = EstimateHonorH(snap, innerW);
            contentH = Mathf.Max(contentH, view.height);
            Widgets.BeginScrollView(view, ref scroll, new Rect(view.x, view.y, innerW, contentH));
            try
            {
                float y = view.y;

                // ---- 荣誉勋章墙 ----
                UIComponents.SectionTitle(new Rect(view.x, y, innerW, 0f), y,
                    "PersonalChronicle.UI.Career.Honor.Title".Translate().ToString());
                y += UITheme.SectionTitleHeight;

                // v4.17 体检：先按行数建墙再画图标——旧实现先画单行背景、循环内才
                // 动态增高，第 2+ 行勋章画在面板背景外（悬空于滚动背景上）。
                List<MedalView> granted = CollectGrantedMedals(snap);
                int perRow = Mathf.Max(1, (int)((innerW - 8f) / (MedIconW + 8f)));
                int rows = granted.Count == 0 ? 1 : Mathf.CeilToInt(granted.Count / (float)perRow);
                Rect wall = new Rect(view.x, y, innerW,
                    MedIconH + 12f + (rows - 1) * (MedIconH + 8f));
                UIComponents.Panel(wall, UITheme.Panel);
                float ix = wall.x + 8f;
                float iy = wall.y + 6f;
                if (granted.Count == 0)
                {
                    UIComponents.Label(new Rect(wall.x + 10f, wall.y + 8f, wall.width - 24f, 20f),
                        "PersonalChronicle.UI.Career.Honor.Empty".Translate().ToString(),
                        UITheme.FontBody, UITheme.Muted);
                }
                for (int i = 0; i < granted.Count; i++)
                {
                    MedalView m = granted[i];
                    if (ix + MedIconW + 8f > wall.xMax)
                    {
                        ix = wall.x + 8f;
                        iy += MedIconH + 8f;
                    }
                    DrawMedalIcon(new Rect(ix, iy, MedIconW, MedIconH), m);
                    ix += MedIconW + 8f;
                }
                // 「＋」授勋入口（对齐 HTML .medal-add）。
                Rect addRect = new Rect(ix + 2f, iy + 4f, 36f, MedIconH - 8f);
                Color prevColor = GUI.color;
                GameFont prevFont = Verse.Text.Font;
                TextAnchor prevAnchor = Verse.Text.Anchor;
                try
                {
                    UIComponents.TintedBox(addRect, UITheme.PanelRaised);
                    UIComponents.Border(addRect, UITheme.PillGold);
                    Verse.Text.Font = GameFont.Medium;
                    Verse.Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = UITheme.PillGold;
                    Widgets.Label(addRect, "＋");
                    GUI.color = prevColor;
                    Verse.Text.Font = prevFont;
                    Verse.Text.Anchor = prevAnchor;
                    TooltipHandler.TipRegion(addRect, "PersonalChronicle.UI.Career.Honor.AddTip".Translate().ToString());
                    if (Widgets.ButtonInvisible(addRect))
                    {
                        ChronicleGameComponent component = Current.Game != null
                            ? Current.Game.GetComponent<ChronicleGameComponent>() : null;
                        if (component != null && pawn != null)
                        {
                            IArchiveService service = PersonalChronicleMod.ArchiveService;
                            Find.WindowStack.Add(new Dialog_ManualMedalAward(pawn, component,
                                () => EnsureSnapshot(service, pawn)));
                        }
                    }
                }
                finally
                {
                    GUI.color = prevColor;
                    Verse.Text.Font = prevFont;
                    Verse.Text.Anchor = prevAnchor;
                }
                y += wall.height + UITheme.SpaceSm;

                // ---- 荣誉贡献结构（真实事实计数）----
                UIComponents.SectionTitle(new Rect(view.x, y, innerW, 0f), y,
                    "PersonalChronicle.UI.Career.Honor.Contrib".Translate().ToString());
                y += UITheme.SectionTitleHeight;
                Rect contrib = new Rect(view.x, y, innerW, 6f * 26f + 8f);
                UIComponents.Panel(contrib, UITheme.Panel);
                PawnObject po = snap != null ? snap.DetailObject as PawnObject : null;
                // UI-001：事实计数统一消费快照 FactCounts，不再直查 Domain（移除 CountEvents）。
                CareerFactCounts fc = snap != null ? snap.FactCounts : null;
                float cy = contrib.y + 6f;
                cy = SnapshotRow(contrib, cy, "PersonalChronicle.UI.Career.Honor.Contrib.Made".Translate().ToString(),
                    (fc != null ? fc.ItemProduced : 0).ToString());
                cy = SnapshotRow(contrib, cy, "PersonalChronicle.UI.Career.Honor.Contrib.Research".Translate().ToString(),
                    (fc != null ? fc.ResearchCompleted : 0).ToString());
                cy = SnapshotRow(contrib, cy, "PersonalChronicle.UI.Career.Honor.Contrib.Built".Translate().ToString(),
                    (fc != null ? fc.ConstructionCompleted : 0).ToString());
                cy = SnapshotRow(contrib, cy, "PersonalChronicle.UI.Career.Honor.Contrib.Books".Translate().ToString(),
                    (po != null && po.CareerData != null && po.CareerData.Books != null ? po.CareerData.Books.Count : 0).ToString());
                SnapshotRow(contrib, cy, "PersonalChronicle.UI.Career.Honor.Contrib.Medal".Translate().ToString(),
                    (fc != null ? fc.MedalGranted : 0).ToString());
                y += contrib.height + UITheme.SpaceSm;

                // ---- 最近荣誉事件（真实 TitleGranted/MedalGranted 事实）----
                UIComponents.SectionTitle(new Rect(view.x, y, innerW, 0f), y,
                    "PersonalChronicle.UI.Career.Honor.Events".Translate().ToString());
                y += UITheme.SectionTitleHeight;
                List<CareerEvent> honorEvents = CollectHonorEvents(po);
                if (honorEvents.Count == 0)
                {
                    UIComponents.Label(new Rect(view.x, y, innerW, 22f),
                        "PersonalChronicle.UI.Career.Honor.Events.Empty".Translate().ToString(),
                        UITheme.FontBody, UITheme.Muted);
                    y += 26f;
                }
                else
                {
                    // 名称查表：MedalView 已派生 UI.Medal.<defName>.Label 的翻译文案，
                    // CareerEvent 只携带 defName，借查表避免回到 MedalDef.LabelCap 的 raw defName。
                    Dictionary<string, string> labelMap = BuildMedalLabelMap(snap);
                    int shown = 0;
                    for (int i = 0; i < honorEvents.Count && shown < 8; i++)
                    {
                        CareerEvent ev = honorEvents[i];
                        string date = "PersonalChronicle.UI.Career.Honor.Event.Year".Translate(
                            GenDate.Year(ev.Tick, 0f).ToString()).ToString();
                        string text = HonorEventText(ev, labelMap);
                        Rect row = new Rect(view.x, y, innerW, 24f);
                        UIComponents.Label(new Rect(row.x, row.y, 70f, 20f), date, UITheme.FontLabel, UITheme.Dim);
                        UIComponents.Label(new Rect(row.x + 70f, row.y, innerW - 70f, 20f),
                            UIComponents.TruncateToWidth(text, innerW - 70f, UITheme.FontLabel),
                            UITheme.FontLabel, UITheme.Text);
                        y += 26f;
                        shown++;
                    }
                }
                y += UITheme.SpaceMd;
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawMedalIcon(Rect rect, MedalView m)
        {
            UIComponents.TintedBox(rect, UITheme.Panel);
            Color tierColor = UITheme.MedalTierColor(m.Tier);
            Texture2D tex = TryLoadMedalIcon(m);
            if (tex != null)
            {
                GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 12f), tex, ScaleMode.ScaleToFit);
            }
            else
            {
                UIComponents.Border(rect, tierColor);
                UIComponents.Label(new Rect(rect.x, rect.y + 14f, rect.width, 26f),
                    FirstGlyph(m.Label), UITheme.FontBody, tierColor, TextAnchor.MiddleCenter);
            }
            // 档位角标（首字：铜/白/金，经 TierLabel 翻译键派生）。
            string tierGlyph = FirstChar(TierLabel(m.Tier));
            UIComponents.Badge(new Rect(rect.xMax - 16f, rect.y - 4f, 16f, 14f), tierGlyph, tierColor);
            string nl2 = "\n";
            string tip = (m.Label ?? m.DefName ?? "--") + " · "
                + TierLabel(m.Tier) + nl2
                + "PersonalChronicle.UI.Career.Honor.Detail.Current".Translate() + "：" + FormatValue(m.CurrentValue)
                + " / " + "PersonalChronicle.UI.Career.Honor.Detail.Threshold".Translate() + "：" + FormatValue(m.Threshold);
            TooltipHandler.TipRegion(rect, new TipSignal(tip));
            if (Widgets.ButtonInvisible(rect))
            {
                Find.WindowStack.Add(new Dialog_MedalDetail(m));
            }
        }

        private static Texture2D TryLoadMedalIcon(MedalView m)
        {
            if (m == null || string.IsNullOrEmpty(m.DefName)) return null;
            MedalDef def = DefDatabase<MedalDef>.GetNamedSilentFail(m.DefName);
            if (def == null || string.IsNullOrEmpty(def.iconPath)) return null;
            return ContentFinder<Texture2D>.Get(def.iconPath, false);
        }

        private List<MedalView> CollectGrantedMedals(DetailSnapshot snap)
        {
            List<MedalView> result = new List<MedalView>();
            if (snap == null || snap.Medals == null) return result;
            for (int i = 0; i < snap.Medals.Count; i++)
            {
                MedalView m = snap.Medals[i];
                if (m != null && m.IsGranted)
                {
                    result.Add(m);
                }
            }
            return result;
        }

        private List<CareerEvent> CollectHonorEvents(PawnObject po)
        {
            List<CareerEvent> result = new List<CareerEvent>();
            if (po == null || po.CareerData == null || po.CareerData.Events == null) return result;
            for (int i = po.CareerData.Events.Count - 1; i >= 0; i--)
            {
                CareerEvent ev = po.CareerData.Events[i];
                if (ev == null) continue;
                if (string.Equals(ev.EventType, CareerEventType.MedalGranted, StringComparison.Ordinal)
                    || string.Equals(ev.EventType, CareerEventType.TitleGranted, StringComparison.Ordinal))
                {
                    result.Add(ev);
                }
            }
            return result;
        }

        private string HonorEventText(CareerEvent ev, Dictionary<string, string> labelMap)
        {
            if (ev == null) return "--";
            if (string.Equals(ev.EventType, CareerEventType.MedalGranted, StringComparison.Ordinal))
            {
                string name = LookupLabel(labelMap, ev.DefName, () =>
                {
                    MedalDef def = DefDatabase<MedalDef>.GetNamedSilentFail(ev.DefName);
                    return def != null ? def.LabelCap : null;
                });
                return "PersonalChronicle.UI.Career.Honor.Event.Medal".Translate(name ?? "--");
            }
            if (string.Equals(ev.EventType, CareerEventType.TitleGranted, StringComparison.Ordinal))
            {
                string name = LookupLabel(labelMap, ev.DefName, () =>
                {
                    ProfessionalTitleDef def = DefDatabase<ProfessionalTitleDef>.GetNamedSilentFail(ev.DefName);
                    return def != null ? def.LabelCap : null;
                });
                return "PersonalChronicle.UI.Career.Honor.Event.Title".Translate(name ?? "--");
            }
            return ev.EventType ?? "--";
        }

        /// <summary>勋章/职称 defName → 翻译后中文标签；优先 MedalView.Label，无则回退到 Def.LabelCap。</summary>
        private static Dictionary<string, string> BuildMedalLabelMap(DetailSnapshot snap)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            if (snap != null && snap.Medals != null)
            {
                for (int i = 0; i < snap.Medals.Count; i++)
                {
                    MedalView mv = snap.Medals[i];
                    if (mv == null || string.IsNullOrEmpty(mv.DefName)) continue;
                    if (!string.IsNullOrEmpty(mv.Label) && !map.ContainsKey(mv.DefName))
                    {
                        map[mv.DefName] = mv.Label;
                    }
                }
            }
            return map;
        }

        private static string LookupLabel(Dictionary<string, string> map, string key, System.Func<string> fallback)
        {
            if (!string.IsNullOrEmpty(key) && map != null && map.TryGetValue(key, out string v) && !string.IsNullOrEmpty(v))
            {
                return v;
            }
            string f = fallback != null ? fallback() : null;
            return string.IsNullOrEmpty(f) ? null : f;
        }

        private static float EstimateHonorH(DetailSnapshot snap, float innerW)
        {
            // 勋章墙：按已授予勋章数估算行数，避免动态换行后被裁切。
            // 布局：起始 x=+8，每格 MedIconW+8，换行条件 ix+MedIconW+8 > wall.xMax。
            int grantedCount = 0;
            if (snap != null && snap.Medals != null)
            {
                for (int i = 0; i < snap.Medals.Count; i++)
                {
                    MedalView m = snap.Medals[i];
                    if (m != null && m.IsGranted) grantedCount++;
                }
            }
            int perRow = Mathf.Max(1, (int)((innerW - 8f) / (MedIconW + 8f)));
            int rows = grantedCount == 0 ? 1 : Mathf.CeilToInt(grantedCount / (float)perRow);
            float wallH = MedIconH + 12f + (rows - 1) * (MedIconH + 8f);
            float h = 30f + wallH + 8f;                  // 勋章墙
            h += 30f + 6f * 26f + 8f;                    // 贡献结构
            // 最近荣誉事件：最多 8 行，空则 1 行。
            int honorCount = 0;
            if (snap != null && snap.DetailObject is PawnObject po
                && po.CareerData != null && po.CareerData.Events != null)
            {
                for (int i = po.CareerData.Events.Count - 1; i >= 0; i--)
                {
                    CareerEvent ev = po.CareerData.Events[i];
                    if (ev == null) continue;
                    if (string.Equals(ev.EventType, CareerEventType.MedalGranted, StringComparison.Ordinal)
                        || string.Equals(ev.EventType, CareerEventType.TitleGranted, StringComparison.Ordinal))
                    {
                        honorCount++;
                    }
                }
            }
            float eventsH = honorCount == 0 ? 26f : Mathf.Min(honorCount, 8) * 26f;
            h += 30f + eventsH + 8f;                     // 最近荣誉事件
            return h + UITheme.SpaceMd * 3f;
        }
    }
}
