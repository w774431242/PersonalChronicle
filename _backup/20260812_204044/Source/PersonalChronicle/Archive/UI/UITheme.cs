using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive.UI
{
    /// <summary>
    /// v4.5 UI Design System — Layer 1: design tokens.
    ///
    /// Single source of truth for every presentational primitive the archive UI
    /// uses. No drawing happens here; no game state is read. Windows and
    /// <see cref="UIComponents"/> consume these tokens so the whole mod can be
    /// re-skinned from one place (e.g. Industrial / Archive / Terminal themes).
    ///
    /// Visual direction: low-saturation industrial terminal — flat fills,
    /// 1px borders, no gradients, no 9-patch, no external fonts. This matches
    /// RimWorld's native Verse UI language instead of fighting it.
    /// </summary>
    internal static class UITheme
    {
        // ---- Layer 0: surfaces (background → raised) ----
        internal static readonly Color Window = new Color(0.067f, 0.094f, 0.110f, 1f);
        internal static readonly Color Panel = new Color(0.094f, 0.129f, 0.149f, 1f);
        internal static readonly Color PanelRaised = new Color(0.125f, 0.165f, 0.188f, 1f);
        internal static readonly Color Card = new Color(0.145f, 0.188f, 0.212f, 1f);
        internal static readonly Color CardHover = new Color(0.190f, 0.235f, 0.263f, 1f);

        // ---- Layer 1: borders (graded by information weight) ----
        /// <summary>Primary outline for panels/cards (strongest).</summary>
        internal static readonly Color Border = new Color(0.270f, 0.325f, 0.357f, 1f);
        /// <summary>Soft divider / secondary outline.</summary>
        internal static readonly Color BorderSoft = new Color(0.180f, 0.231f, 0.255f, 1f);
        /// <summary>Hairline rule between rows (weakest).</summary>
        internal static readonly Color BorderHair = new Color(0.150f, 0.192f, 0.212f, 1f);

        // ---- Layer 2: text ----
        // Calibrated to RimWorld 1.6 Verse.ColoredText (SubtleGrayColor / TextColor).
        internal static readonly Color Text = new Color(0.914f, 0.933f, 0.929f, 1f);
        internal static readonly Color Muted = new Color(0.600f, 0.600f, 0.600f, 1f); // ColoredText.SubtleGrayColor
        internal static readonly Color Dim = new Color(0.443f, 0.502f, 0.525f, 1f);
        /// <summary>Alias kept for legacy call sites; mirrors Muted (ColoredText.SubtleGrayColor).</summary>
        internal static readonly Color SecondaryText = new Color(0.600f, 0.600f, 0.600f, 1f);

        // ---- Layer 3: semantic accents ----
        // Calibrated to RimWorld 1.6 Verse.ColoredText native values.
        internal static readonly Color Accent = new Color(0.816f, 0.608f, 0.380f, 1f); // ColoredText.NameColor
        internal static readonly Color AccentSoft = new Color(0.816f, 0.608f, 0.380f, 0.15f);
        internal static readonly Color Info = new Color(0.584f, 0.816f, 0.988f, 1f); // ColoredText.GeneColor
        internal static readonly Color Alive = new Color(0.570f, 0.900f, 0.690f, 1f); // ColoredText.ExpectationsColor
        internal static readonly Color Dead = new Color(0.831f, 0.435f, 0.408f, 1f); // ColoredText.ThreatColor
        /// <summary>Blood-crimson accent for impaired health / depreciation warnings.</summary>
        internal static readonly Color Blood = new Color(0.831f, 0.435f, 0.408f, 1f);

        // ---- Layer 3b: native warning / threat / faction relations ----
        // Directly sourced from Verse.ColoredText so the archive matches the game's
        // own colour language instead of an approximate hand-picked palette.
        internal static readonly Color Warn = new Color(1.000f, 0.000f, 0.000f, 1f); // ColoredText.WarningColor
        internal static readonly Color Threat = new Color(0.831f, 0.435f, 0.408f, 1f); // ColoredText.ThreatColor
        internal static readonly Color FactionAlly = new Color(0.000f, 1.000f, 0.000f, 1f); // ColoredText.FactionColor_Ally
        internal static readonly Color FactionHostile = new Color(1.000f, 0.200f, 0.200f, 1f); // ColoredText.FactionColor_Hostile
        internal static readonly Color FactionNeutral = new Color(0.000f, 0.749f, 1.000f, 1f); // ColoredText.FactionColor_Neutral

        // ---- Layer 3c: translucent overlays / alpha helpers ----
        /// <summary>White 4% overlay for placeholder / empty-state boxes.</summary>
        internal static readonly Color OverlayWhite04 = new Color(1f, 1f, 1f, 0.04f);
        /// <summary>Info accent at 50% alpha (event-row accent bar).</summary>
        internal static readonly Color InfoSoft = new Color(0.584f, 0.816f, 0.988f, 0.5f);
        /// <summary>Returns <paramref name="c"/> with its alpha overridden (RGB kept).</summary>
        internal static Color WithAlpha(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);
        /// <summary>Dark brown fill for the "mid-install / incomplete" badge.</summary>
        internal static readonly Color BadgeIncompleteFill = new Color(0.16f, 0.13f, 0.11f, 1f);

        // ---- Layer 4: timeline node tints (per event kind) ----
        internal static readonly Color TimelineSpine = new Color(0.4f, 0.4f, 0.4f, 1f);
        internal static readonly Color TimelineJoin = new Color(0.4f, 0.8f, 0.4f, 1f);
        internal static readonly Color TimelineDeath = new Color(0.85f, 0.35f, 0.35f, 1f);
        internal static readonly Color TimelineBattle = new Color(0.9f, 0.5f, 0.2f, 1f);
        internal static readonly Color TimelineSocial = new Color(0.5f, 0.6f, 0.9f, 1f);
        internal static readonly Color TimelineCraft = new Color(0.7f, 0.6f, 0.3f, 1f);
        internal static readonly Color TimelineBuilt = new Color(0.5f, 0.5f, 0.55f, 1f);
        internal static readonly Color TimelineOther = Color.gray;

        // ---- Spacing scale (uniform rhythm; avoids ad-hoc 7/13/19px) ----
        internal const float SpaceXxs = 4f;
        internal const float SpaceXs = 8f;
        internal const float SpaceSm = 12f;
        internal const float SpaceMd = 16f;
        internal const float SpaceLg = 24f;

        // ---- Typography (native Verse fonts only; no external typefaces) ----
        internal const GameFont FontLabel = GameFont.Tiny;
        internal const GameFont FontBody = GameFont.Small;
        internal const GameFont FontValue = GameFont.Medium;
        /// <summary>Small-font line height (CJK-safe empirical value, v4.5).</summary>
        internal const float FontBodyLineHeight = 22f;

        // ---- Component metrics ----
        internal const float HeaderHeight = 56f;
        internal const float SidebarWidth = 208f;
        internal const float Gap = 12f;
        internal const float PanelPadding = 12f;
        internal const float CardGap = 8f;
        internal const float SectionTitleHeight = 30f;
        internal const float RuleHeight = 1f;
        internal const float BorderThickness = 1f;
        /// <summary>Standard scrollbar gutter width (matches Verse scroll view).</summary>
        internal const float ScrollbarThickness = 18f;

        // ---- Card layout rhythm (single source for in-card spacing) ----
        // Left/right inner padding of detail cards (StatCell uses 8; cards use 10/12
        // historically — CardPadX unifies them to the panel padding step).
        internal const float CardPadX = 12f;
        // Top inner padding of detail cards.
        internal const float CardPadY = 8f;
        // Gap between grid columns (KPI cells / stat rows).
        internal const float GridGap = 8f;
        // Vertical gap between top-level content blocks (sections).
        internal const float BlockGap = 12f;
        // v4.6.5: single source of truth for progress-bar height across the whole
        // mod (career bars, output ledger rows, health dimension bars). The value
        // matches the output-ledger bar that drives this baseline.
        internal const float ProgressbarH = 12f;
        // Standard right-hand value area reserved next to every progress bar so that
        // the bar length is fixed regardless of value text width (output ledger and
        // health dimension bars share the same layout).
        internal const float ProgressbarValueW = 116f;

        // ---- State color resolver (semantic) ----
        internal static Color StateColor(bool positive, bool negative)
        {
            if (positive) return Alive;
            if (negative) return Dead;
            return Info;
        }

        // ---- Pill/tag tints (exact legacy values, centralized) ----
        internal static readonly Color PillBlue = new Color(0.36f, 0.62f, 0.83f, 1f);
        internal static readonly Color PillGold = new Color(0.95f, 0.74f, 0.26f, 1f);
        internal static readonly Color PillRed = new Color(0.86f, 0.46f, 0.46f, 1f);
        internal static readonly Color PillGreen = new Color(0.42f, 0.81f, 0.56f, 1f);

        // ---- v4.15: single semantic->tone mapping for six digest cells (P3: the
        // ITab no longer carries if/switch tone logic; all kind->color lives here). ----
        // kindKey maps 1:1 to DetailSnapshot core KPI cell keys. Unknown keys fall
        // back to Accent so the digest never renders an un-toned cell.
        internal static Color TintForEventKind(string kindKey)
        {
            switch (kindKey)
            {
                case "work": return PillGold;   // 集中工时
                case "prod": return PillGreen;  // 产出
                case "kill": return PillRed;    // 击杀
                case "battle": return PillBlue; // 战役
                case "foot": return Info;       // 足迹
                case "rel": return Alive;       // 关系
                case "career": return PillGold; // 主业（工时同源）
                case "home": return Info;       // 主驻地（足迹同源）
                case "legacy": return PillRed;  // 传承击杀（击杀同源）
                case "health": return Alive;    // 健康残值（在世同源）
                default: return Accent;
            }
        }
    }
}
