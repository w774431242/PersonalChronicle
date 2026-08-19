// 勋章详情对话框（对齐 docs/UI预览/人物档案视窗/职业档案Tab预览.html 勋章详情 Dialog：
// 大图 + 标题 + 档位 pill + 归属/当前值/阈值/进度 + 描述 + 增益）。
// 只读消费 ReadModel 已派生的 MedalView（窗口不重判定）；无图时档位色占位块兜底。
using PersonalChronicle.Archive.ReadModels;
using PersonalChronicle.Domain;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    public class Dialog_MedalDetail : Window
    {
        private const float WinW = 380f;
        private const float WinH = 540f;
        private const float PadX = 14f;
        private const float BadgeW = 84f;
        private const float BadgeH = 96f;

        private readonly MedalView medal;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(WinW, WinH);

        /// <param name="medal">ReadModel 已判定/翻译的勋章视图（MedalView）。</param>
        public Dialog_MedalDetail(MedalView medal)
        {
            this.medal = medal;
            scroll = Vector2.zero;
            doCloseX = true;
            doCloseButton = false;
            closeOnAccept = false;
            closeOnCancel = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (medal == null)
            {
                return;
            }
            float y = inRect.y + 10f;
            // ---- 顶部：大图 + 标题 + 档位 pill ----
            Rect badge = new Rect(inRect.x + PadX, y, BadgeW, BadgeH);
            DrawBadge(badge);
            Rect titleRect = new Rect(badge.xMax + 12f, y + 6f, inRect.width - PadX - 12f - badge.width, 26f);
            UIComponents.Label(titleRect,
                UIComponents.TruncateToWidth(medal.Label ?? medal.DefName ?? "--", titleRect.width, UITheme.FontValue),
                UITheme.FontValue, UITheme.Text);
            Rect pillRect = new Rect(titleRect.x, titleRect.yMax + 6f, 150f, 22f);
            UIComponents.Pill(pillRect, TierLabel(medal.Tier) + " · " + MetricLabel(), UITheme.MedalTierColor(medal.Tier));
            y += BadgeH + 10f;

            // ---- 详情行（归属/当前值/阈值/进度/所需职称/defName）----
            Rect rows = new Rect(inRect.x + PadX, y, inRect.width - PadX * 2f, 150f);
            float ry = rows.y;
            ry = DetailRow(rows, ry, "PersonalChronicle.UI.Career.Honor.Detail.Owner".Translate().ToString(),
                "PersonalChronicle.UI.Career.Honor.Detail.OwnerPawn".Translate().ToString());
            ry = DetailRow(rows, ry, "PersonalChronicle.UI.Career.Honor.Detail.Current".Translate().ToString(),
                FormatValue(medal.CurrentValue));
            ry = DetailRow(rows, ry, "PersonalChronicle.UI.Career.Honor.Detail.Threshold".Translate().ToString(),
                FormatValue(medal.Threshold));
            bool reached = medal.Threshold > 0.0 && medal.CurrentValue >= medal.Threshold;
            ry = DetailRow(rows, ry, "PersonalChronicle.UI.Career.Honor.Detail.Progress".Translate().ToString(),
                Mathf.RoundToInt(medal.Progress * 100f) + "%（"
                + (reached
                    ? "PersonalChronicle.UI.Career.Honor.Detail.Reached".Translate().ToString()
                    : "PersonalChronicle.UI.Career.Honor.Detail.NotReached".Translate().ToString()) + "）");
            ry = DetailRow(rows, ry, "PersonalChronicle.UI.Career.Honor.Detail.Require".Translate().ToString(),
                "PersonalChronicle.UI.Career.Honor.Detail.NoRequire".Translate().ToString());
            DetailRow(rows, ry, "defName", medal.DefName ?? "--");
            y += 160f;

            // ---- 描述 ----
            float descH = 60f;
            Rect desc = new Rect(inRect.x + PadX, y, inRect.width - PadX * 2f, descH);
            UIComponents.TintedBox(desc, UITheme.Panel);
            UIComponents.Label(new Rect(desc.x + 8f, desc.y + 6f, desc.width - 16f, descH - 12f),
                medal.Desc ?? "--", UITheme.FontLabel, UITheme.Muted);
            y += descH + 8f;

            // ---- 增益 ----
            string buff = medal.BuffText;
            if (string.IsNullOrEmpty(buff))
            {
                buff = "PersonalChronicle.UI.Career.Honor.Detail.NoBuff".Translate().ToString();
            }
            Rect buffBox = new Rect(inRect.x + PadX, y, inRect.width - PadX * 2f, 34f);
            UIComponents.TintedBox(buffBox, UITheme.Alive);
            UIComponents.Label(new Rect(buffBox.x + 8f, buffBox.y + 8f, buffBox.width - 16f, 20f),
                "PersonalChronicle.UI.Career.Honor.Detail.Buff".Translate() + "：" + buff,
                UITheme.FontBody, UITheme.Alive);
        }

        private float DetailRow(Rect outer, float y, string key, string value)
        {
            UIComponents.Label(new Rect(outer.x, y, 110f, 22f), key, UITheme.FontBody, UITheme.Muted);
            UIComponents.Label(new Rect(outer.x + 110f, y, outer.width - 110f, 22f),
                UIComponents.TruncateToWidth(value ?? "--", outer.width - 110f, UITheme.FontBody),
                UITheme.FontBody, UITheme.Text, TextAnchor.UpperRight);
            return y + 25f;
        }

        private void DrawBadge(Rect rect)
        {
            Texture2D tex = TryLoadIcon();
            UIComponents.TintedBox(rect, UITheme.Panel);
            if (tex != null)
            {
                GUI.DrawTexture(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f), tex, ScaleMode.ScaleToFit);
            }
            else
            {
                // 档位色占位：称号首字 + 边框色（对齐 HTML .no-img 占位语义）。
                Color tierColor = UITheme.MedalTierColor(medal.Tier);
                UIComponents.Border(rect, tierColor);
                string glyph = FirstGlyph(medal.Label);
                UIComponents.Label(new Rect(rect.x, rect.y + 30f, rect.width, 40f), glyph,
                    UITheme.FontValue, tierColor, TextAnchor.MiddleCenter);
            }
        }

        private Texture2D TryLoadIcon()
        {
            if (string.IsNullOrEmpty(medal.DefName)) return null;
            MedalDef def = DefDatabase<MedalDef>.GetNamedSilentFail(medal.DefName);
            if (def == null || string.IsNullOrEmpty(def.iconPath)) return null;
            Texture2D tex = ContentFinder<Texture2D>.Get(def.iconPath, false);
            return tex;
        }

        private static string TierLabel(MedalTier tier)
        {
            switch (tier)
            {
                case MedalTier.Bronze: return "PersonalChronicle.UI.Career.Honor.Tier.Bronze".Translate().ToString();
                case MedalTier.Silver: return "PersonalChronicle.UI.Career.Honor.Tier.Silver".Translate().ToString();
                default: return "PersonalChronicle.UI.Career.Honor.Tier.Gold".Translate().ToString();
            }
        }

        private static string MetricLabel()
        {
            return "PersonalChronicle.UI.Career.Honor.Detail.Metric".Translate().ToString();
        }

        private static string FormatValue(double v)
        {
            if (v >= 10000.0) return (v / 1000.0).ToString("0.0") + "K";
            if (v == (long)v) return ((long)v).ToString();
            return v.ToString("0.##");
        }

        private static string FirstGlyph(string label)
        {
            if (string.IsNullOrEmpty(label)) return "?";
            return label.Substring(0, 1);
        }
    }
}
