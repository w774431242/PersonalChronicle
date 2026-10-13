using PersonalChronicle.Archive;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>
    /// In-window player handbook. Layout is card-based; copy is entirely keyed.
    /// Measure and draw share <see cref="LayoutManual"/> so CJK wrap height cannot drift.
    /// </summary>
    public sealed partial class PersonnelArchivePanel
    {
        private static readonly string[] ManualSectionIds =
        {
            "Concepts", "Pawn", "Career", "Honor", "Intensity",
            "Battle", "Place", "Legacy", "Health", "Social", "Settings"
        };

        private const float ManualCardPad = 12f;
        private const float ManualCardGap = 10f;
        private const float ManualBulletH = 22f;
        private const int ManualMaxBullets = 6;

        private void DrawManualContent(Rect inner)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            bool prevWrap = Text.WordWrap;
            Text.WordWrap = true;
            Text.Anchor = TextAnchor.UpperLeft;

            float width = Mathf.Max(120f, inner.width - 16f);
            Rect measureRect = new Rect(inner.x, inner.y, width, 4096f);
            float contentH = LayoutManual(measureRect, false);
            Rect viewRect = new Rect(inner.x, inner.y, width, Mathf.Max(inner.height, contentH));
            Widgets.BeginScrollView(inner, ref manualScroll, viewRect);
            try
            {
                LayoutManual(viewRect, true);
            }
            finally
            {
                Widgets.EndScrollView();
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
                Text.WordWrap = prevWrap;
            }
        }

        private static float LayoutManual(Rect view, bool draw)
        {
            float y = view.y + 8f;
            float w = view.width;

            if (draw)
            {
                UIComponents.Label(new Rect(view.x, y, w, 28f),
                    T("Title"), GameFont.Medium, UITheme.Text);
            }
            y += 32f;

            y = LayoutLead(view, y, draw);
            y = LayoutHow(view, y, draw);

            for (int i = 0; i < ManualSectionIds.Length; i++)
            {
                y = LayoutSectionCard(view, y, ManualSectionIds[i], draw);
            }
            return (y - view.y) + 12f;
        }

        private static float LayoutLead(Rect view, float y, bool draw)
        {
            string lead = T("Lead");
            float innerW = view.width - ManualCardPad * 2f;
            float leadH = WrappedHeight(lead, innerW, GameFont.Small);
            float cardH = leadH + ManualCardPad * 2f;
            if (draw)
            {
                Rect card = new Rect(view.x, y, view.width, cardH);
                UIComponents.Card(card, UITheme.Accent);
                UIComponents.Label(
                    new Rect(card.x + ManualCardPad, card.y + ManualCardPad, innerW, leadH),
                    lead, GameFont.Small, UITheme.Text);
            }
            return y + cardH + ManualCardGap;
        }

        private static float LayoutHow(Rect view, float y, bool draw)
        {
            string[] steps = { T("How.L1"), T("How.L2"), T("How.L3") };
            float gap = 8f;
            bool row = view.width >= 540f;
            float colW = row ? (view.width - gap * 2f) / 3f : view.width;
            float colH = 0f;
            for (int i = 0; i < 3; i++)
            {
                float innerW = colW - ManualCardPad * 2f;
                float h = 18f + WrappedHeight(steps[i], innerW, GameFont.Tiny) + ManualCardPad;
                if (h > colH) colH = h;
            }
            if (draw)
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = row ? view.x + i * (colW + gap) : view.x;
                    float cy = row ? y : y + i * (colH + 6f);
                    Rect card = new Rect(x, cy, colW, colH);
                    UIComponents.Card(card, UITheme.Info);
                    UIComponents.Label(
                        new Rect(card.x + ManualCardPad, card.y + 6f, colW - ManualCardPad * 2f, 16f),
                        T("How.N" + (i + 1)), GameFont.Tiny, UITheme.Accent);
                    UIComponents.Label(
                        new Rect(card.x + ManualCardPad, card.y + 22f, colW - ManualCardPad * 2f, colH - 28f),
                        steps[i], GameFont.Tiny, UITheme.Text);
                }
            }
            float blockH = row ? colH : colH * 3f + 12f;
            return y + blockH + ManualCardGap;
        }

        private static float LayoutSectionCard(Rect view, float y, string id, bool draw)
        {
            string title = Sec(id, "Title");
            string blurb = Sec(id, "Blurb");
            float innerW = view.width - ManualCardPad * 2f;
            float titleH = 22f;
            float blurbH = WrappedHeight(blurb, innerW, GameFont.Tiny);
            int bullets = 0;
            float bulletsH = 0f;
            for (int n = 1; n <= ManualMaxBullets; n++)
            {
                string key = "PersonalChronicle.UI.Manual." + id + ".L" + n;
                if (!key.CanTranslate())
                {
                    break;
                }
                bullets++;
                bulletsH += ManualBulletH;
            }
            float cardH = ManualCardPad + titleH + 4f + blurbH + 6f + bulletsH + ManualCardPad;
            if (draw)
            {
                Rect card = new Rect(view.x, y, view.width, cardH);
                UIComponents.Card(card, UITheme.Accent);
                float cy = card.y + ManualCardPad;
                UIComponents.Label(new Rect(card.x + ManualCardPad, cy, innerW, titleH),
                    title, GameFont.Small, UITheme.Text);
                cy += titleH + 2f;
                UIComponents.Label(new Rect(card.x + ManualCardPad, cy, innerW, blurbH),
                    blurb, GameFont.Tiny, UITheme.Muted);
                cy += blurbH + 6f;
                for (int n = 1; n <= bullets; n++)
                {
                    string line = "·  " + Sec(id, "L" + n);
                    UIComponents.Label(
                        new Rect(card.x + ManualCardPad, cy, innerW, ManualBulletH),
                        line, GameFont.Small, UITheme.SecondaryText);
                    cy += ManualBulletH;
                }
            }
            return y + cardH + ManualCardGap;
        }

        private static float WrappedHeight(string text, float width, GameFont font)
        {
            if (string.IsNullOrEmpty(text) || width <= 8f)
            {
                return font == GameFont.Tiny ? 18f : UITheme.FontBodyLineHeight;
            }
            GameFont prev = Text.Font;
            bool wrap = Text.WordWrap;
            Text.Font = font;
            Text.WordWrap = true;
            float raw = Text.CalcHeight(text, width);
            float line = Mathf.Max(1f, Text.LineHeight);
            Text.Font = prev;
            Text.WordWrap = wrap;
            float token = font == GameFont.Tiny ? 18f : UITheme.FontBodyLineHeight;
            int lines = Mathf.Max(1, Mathf.CeilToInt((raw + 0.01f) / line));
            return Mathf.Max(raw + 4f, lines * token);
        }

        private static string T(string part)
        {
            return ("PersonalChronicle.UI.Manual." + part).Translate().ToString();
        }

        private static string Sec(string id, string part)
        {
            return ("PersonalChronicle.UI.Manual." + id + "." + part).Translate().ToString();
        }
    }
}
