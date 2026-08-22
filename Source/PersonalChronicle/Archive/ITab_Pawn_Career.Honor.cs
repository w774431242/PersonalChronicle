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
        // v4.17 体检（审计 #15）：勋章墙/荣誉事件/标签映射在快照变化时缓存，
        // 旧实现每帧重建 List×2 + Dictionary（PERF-001）。
        private List<MedalView> cachedGrantedMedals;
        private List<CareerEvent> cachedHonorEvents;
        private Dictionary<string, string> cachedMedalLabelMap;
        private long cachedHonorRev = -1L;

        /// <summary>按快照 revision 惰性重建 Honor 页派生数据（绘制路径零分配）。</summary>
        private void EnsureHonorCache(DetailSnapshot snap)
        {
            long rev = snap != null ? snap.BuiltFromRevision : -1L;
            if (rev == cachedHonorRev && cachedGrantedMedals != null)
            {
                return;
            }
            cachedHonorRev = rev;
            cachedGrantedMedals = CollectGrantedMedals(snap);
            PawnObject po = snap != null ? snap.DetailObject as PawnObject : null;
            cachedHonorEvents = CollectHonorEvents(po);
            cachedMedalLabelMap = BuildMedalLabelMap(snap);
        }

        // ================= 子页：勋章 =================
        private void DrawHonorTab(Rect rect, Pawn pawn, DetailSnapshot snap)
        {
            // 原生 BeginScrollView 用法（对齐 ArchiveMainTabWindow）：不自定义背景，
            // 让 rimworld 窗口默认背景生效——避免自定义 TintedBox 干扰造成的白屏。
            Rect view = new Rect(rect.x, rect.y, rect.width, rect.height);
            float innerW = view.width - 16f;
            float contentH = EstimateHonorH(snap, innerW);
            contentH = Mathf.Max(contentH, view.height);
            Widgets.BeginScrollView(view, ref scroll, new Rect(view.x, view.y, innerW, contentH));
            try
            {
                float y = view.y + 4f;

                // ---- 荣誉勋章墙 ----
                UIComponents.SectionTitle(new Rect(view.x, y, innerW, 0f), y,
                    "PersonalChronicle.UI.Career.Honor.Title".Translate().ToString());
                y += UITheme.SectionTitleHeight;

                // v4.17 体检：先按行数建墙再画图标——旧实现先画单行背景、循环内才
                // 动态增高，第 2+ 行勋章画在面板背景外（悬空于滚动背景上）。
                // 数据经 EnsureHonorCache 缓存（每帧不再重建 List/Dictionary）。
                EnsureHonorCache(snap);
                List<MedalView> granted = cachedGrantedMedals;
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

                // ---- 荣誉贡献结构（分段条可视化，一眼看出主导贡献项）----
                UIComponents.SectionTitle(new Rect(view.x, y, innerW, 0f), y,
                    "PersonalChronicle.UI.Career.Honor.Contrib".Translate().ToString());
                y += UITheme.SectionTitleHeight;
                PawnObject po = snap != null ? snap.DetailObject as PawnObject : null;
                CareerFactCounts fc = snap != null ? snap.FactCounts : null;
                int made = fc != null ? fc.ItemProduced : 0;
                int built = fc != null ? fc.ConstructionCompleted : 0;
                int research = fc != null ? fc.ResearchCompleted : 0;
                int books = po != null && po.CareerData != null && po.CareerData.Books != null ? po.CareerData.Books.Count : 0;
                int medals = fc != null ? fc.MedalGranted : 0;
                string[] contribLabels = new string[]
                {
                    "PersonalChronicle.UI.Career.Honor.Contrib.Made".Translate().ToString(),
                    "PersonalChronicle.UI.Career.Honor.Contrib.Built".Translate().ToString(),
                    "PersonalChronicle.UI.Career.Honor.Contrib.Research".Translate().ToString(),
                    "PersonalChronicle.UI.Career.Honor.Contrib.Books".Translate().ToString(),
                    "PersonalChronicle.UI.Career.Honor.Contrib.Medal".Translate().ToString(),
                };
                int[] contribVals = new int[] { made, built, research, books, medals };
                Color[] contribColors = new Color[] { UITheme.PillGold, UITheme.Info, UITheme.Alive, UITheme.Accent, UITheme.PillGreen };
                int total = made + built + research + books + medals;
                Rect contrib = new Rect(view.x, y, innerW, 5f * 30f + 8f);
                UIComponents.Panel(contrib, UITheme.Panel);
                float cy = contrib.y + 6f;
                // 标签 80 + 值 64 + 间隙 8 = 152；bar 严格限宽 + fillW 封顶 80%（避免单项占满）
                float labelW = 80f, valueW = 64f, sideGap = 8f;
                float barFullW = contrib.width - 16f - labelW - valueW - sideGap;
                float fillCap = barFullW * 0.8f;
                for (int ci = 0; ci < 5; ci++)
                {
                    float rowY = cy + ci * 30f;
                    // 标签
                    UIComponents.Label(new Rect(contrib.x + 8f, rowY, labelW, 24f),
                        UIComponents.TruncateToWidth(contribLabels[ci], labelW, UITheme.FontLabel),
                        UITheme.FontLabel, UITheme.Muted);
                    // 分段条
                    float barX = contrib.x + 8f + labelW;
                    Rect barBg = new Rect(barX, rowY + 4f, barFullW, 16f);
                    UIComponents.TintedBox(barBg, UITheme.PanelRaised);
                    // fillW = 占比 × barFullW，封顶 80%（视觉上避免某项"压扁"整行）
                    float ratio = total > 0 ? contribVals[ci] / (float)total : 0f;
                    float fillW = Mathf.Min(barFullW * ratio, fillCap);
                    if (fillW > 1f)
                    {
                        UIComponents.TintedBox(new Rect(barX, rowY + 4f, fillW, 16f), contribColors[ci]);
                    }
                    // 值
                    UIComponents.Label(new Rect(barX + barFullW + 4f, rowY, valueW, 24f),
                        contribVals[ci].ToString(), UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft);
                }
                y += contrib.height + UITheme.SpaceSm;

                // ---- 最近荣誉事件（真实 TitleGranted/MedalGranted 事实；经缓存）----
                UIComponents.SectionTitle(new Rect(view.x, y, innerW, 0f), y,
                    "PersonalChronicle.UI.Career.Honor.Events".Translate().ToString());
                y += UITheme.SectionTitleHeight;
                List<CareerEvent> honorEvents = cachedHonorEvents;
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
                    // v4.17 体检：标签映射经 EnsureHonorCache 缓存，不再每帧重建 Dictionary。
                    Dictionary<string, string> labelMap = cachedMedalLabelMap;
                    int shown = 0;
                    for (int i = 0; i < honorEvents.Count && shown < 8; i++)
                    {
                        CareerEvent ev = honorEvents[i];
                        string text = HonorEventText(ev, labelMap);
                        // 死数据过滤：name 缺失的职称/勋章事件（如"获得职称：--"）不渲染，避免污染时间轴。
                        if (string.IsNullOrEmpty(text)) continue;
                        string date = "PersonalChronicle.UI.Career.Honor.Event.Year".Translate(
                            GenDate.Year(ev.Tick, 0f).ToString()).ToString();
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
            // 档位角标（完整档位短词：青铜/白银/黄金，深色文字 on 档位色底，确保可读）。
            string tierLabel = TierLabel(m.Tier);
            Verse.Text.Font = GameFont.Tiny;
            float badgeW = Verse.Text.CalcSize(tierLabel).x + 8f;
            Verse.Text.Font = GameFont.Small;
            Rect badgeRect = new Rect(rect.xMax - badgeW, rect.y - 2f, badgeW, 14f);
            UIComponents.TintedBox(badgeRect, tierColor);
            UIComponents.Label(badgeRect, tierLabel, UITheme.FontLabel, UITheme.Text, TextAnchor.MiddleCenter);
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
            if (ev == null) return null;
            if (string.Equals(ev.EventType, CareerEventType.MedalGranted, StringComparison.Ordinal))
            {
                string name = LookupLabel(labelMap, ev.DefName, () =>
                {
                    MedalDef def = DefDatabase<MedalDef>.GetNamedSilentFail(ev.DefName);
                    return def != null ? def.LabelCap : null;
                });
                if (string.IsNullOrEmpty(name) || name == "--") return null;
                return "PersonalChronicle.UI.Career.Honor.Event.Medal".Translate(name);
            }
            if (string.Equals(ev.EventType, CareerEventType.TitleGranted, StringComparison.Ordinal))
            {
                string name = LookupLabel(labelMap, ev.DefName, () =>
                {
                    ProfessionalTitleDef def = DefDatabase<ProfessionalTitleDef>.GetNamedSilentFail(ev.DefName);
                    return def != null ? def.LabelCap : null;
                });
                if (string.IsNullOrEmpty(name) || name == "--") return null;
                return "PersonalChronicle.UI.Career.Honor.Event.Title".Translate(name);
            }
            return null;
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
            h += 30f + 5f * 30f + 8f;                    // 贡献结构（分段条）
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
