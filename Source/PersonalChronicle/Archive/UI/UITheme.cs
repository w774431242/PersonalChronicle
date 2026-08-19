using System;
using PersonalChronicle.Domain;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive.UI
{
    /// <summary>
    /// v4.5+ UI Design System — Layer 1: design tokens.
    ///
    /// Single source of truth for every presentational primitive the archive UI
    /// uses. No drawing happens here; no game state is read. Windows and
    /// <see cref="UIComponents"/> consume these tokens so the whole mod can be
    /// re-skinned from one place.
    ///
    /// Since v1.1.4 the colour tokens are <b>mutable statics</b>: <see cref="Apply"/>
    /// overwrites the whole palette at runtime so the UI can be re-skinned while the
    /// game is running. Field <b>names</b> are stable (zero call-site churn); only the
    /// values change. All tokens default to the Epic (史诗奇幻) palette, so a caller
    /// that never applies a theme still sees the default RPG look.
    ///
    /// Font is intentionally <b>not</b> part of the theme (v1.1.4 final): RimWorld
    /// cannot reliably load arbitrary TTF files (no byte[] Font ctor) and OS-font
    /// swapping proved fragile, so the archive UI uses the vanilla Verse fonts. Theme
    /// switching is colour-only.
    /// </summary>
    internal static class UITheme
    {
        // ---- Theme registry ---------------------------------------------------
        /// <summary>Stable id of the active theme. Mirrors <c>ChronicleSettings.ThemeId</c>.</summary>
        internal static string ActiveThemeId = ThemeEpic;

        public const string ThemeEpic = "epic";          // 经典史诗奇幻（默认）
        public const string ThemeGothic = "gothic";      // 暗黑哥特
        public const string ThemeWuxia = "wuxia";        // 东方武侠水墨（浅色）
        public const string ThemeSteampunk = "steampunk";// 蒸汽朋克

        /// <summary>All selectable theme ids, in display order (button cycle order).</summary>
        internal static readonly string[] ThemeIds = { ThemeEpic, ThemeGothic, ThemeWuxia, ThemeSteampunk };

        /// <summary>Text shadow colour used by <see cref="UIComponents.ShadowLabel"/>.
        /// Centralised here (Design System Layer 1) so the shadow tint is a single
        /// token instead of a hard-coded literal in the drawing code (ASSET-002).</summary>
        internal static Color Shadow { get { return new Color(0f, 0f, 0f, 0.85f); } }

        /// <summary>
        /// Returns the localised display label for a theme id (translation key
        /// <c>PersonalChronicle.UI.Theme.&lt;CapitalisedId&gt;</c>). Falls back to the
        /// raw id when the key is missing — keeps the UI usable even if the language
        /// file is incomplete.
        /// </summary>
        internal static string GetDisplayName(string themeId)
        {
            if (string.IsNullOrEmpty(themeId)) return "";
            string key = "PersonalChronicle.UI.Theme." + char.ToUpper(themeId[0]) + (themeId.Length > 1 ? themeId.Substring(1) : "");
            string translated = key.Translate().ToString();
            return string.IsNullOrEmpty(translated) || translated == key ? themeId : translated;
        }

        /// <summary>Returns the theme id following <paramref name="current"/> in cycle order.</summary>
        internal static string NextThemeId(string current)
        {
            int idx = Array.IndexOf(ThemeIds, current);
            if (idx < 0) return ThemeIds[0];
            return ThemeIds[(idx + 1) % ThemeIds.Length];
        }

        /// <summary>
        /// Applies the named palette to every mutable colour token and re-syncs the
        /// <see cref="ArchiveUiStyle"/> forwards. Unknown id silently keeps the current
        /// palette (so a corrupt save never bricks the UI). Safe to call every frame;
        /// it only overwrites colours, never allocates.
        /// </summary>
        internal static void Apply(string themeId)
        {
            Palette p;
            switch (themeId)
            {
                case ThemeGothic: p = Palette.Gothic; break;
                case ThemeWuxia: p = Palette.Wuxia; break;
                case ThemeSteampunk: p = Palette.Steampunk; break;
                default: p = Palette.Epic; break;
            }
            Assign(p);
            ActiveThemeId = themeId;

            // v1.1.5: texture-backed themes need stronger nav highlight so the
            // selected sidebar item is visible against the panel texture.
            if (themeId == ThemeEpic)
                AccentSoft = new Color(p.Accent.r, p.Accent.g, p.Accent.b, 0.28f);

            // Re-sync the legacy forwarding shell so its static fields track the
            // newly active palette (its fields are non-readonly since v1.1.4).
            ArchiveUiStyle.RefreshFromTheme();
        }

        /// <summary>Re-asserts the current palette (used at startup after Def load).</summary>
        internal static void RefreshCurrent()
        {
            Apply(ActiveThemeId);
        }

        private static void Assign(Palette p)
        {
            Window = p.Window;
            Panel = p.Panel;
            PanelRaised = p.PanelRaised;
            Card = p.Card;
            CardHover = p.CardHover;

            Border = p.Border;
            BorderSoft = p.BorderSoft;
            BorderHair = p.BorderHair;

            Text = p.Text;
            Muted = p.Muted;
            Dim = p.Dim;
            SecondaryText = p.SecondaryText;

            Accent = p.Accent;
            AccentSoft = p.AccentSoft;
            Info = p.Info;
            Alive = p.Alive;
            Dead = p.Dead;
            Blood = p.Blood;

            Warn = p.Warn;
            Threat = p.Threat;
            FactionAlly = p.FactionAlly;
            FactionHostile = p.FactionHostile;
            FactionNeutral = p.FactionNeutral;

            OverlayWhite04 = p.OverlayWhite04;
            InfoSoft = p.InfoSoft;
            BadgeIncompleteFill = p.BadgeIncompleteFill;

            TimelineSpine = p.TimelineSpine;
            TimelineJoin = p.TimelineJoin;
            TimelineDeath = p.TimelineDeath;
            TimelineBattle = p.TimelineBattle;
            TimelineSocial = p.TimelineSocial;
            TimelineCraft = p.TimelineCraft;
            TimelineBuilt = p.TimelineBuilt;
            TimelineOther = p.TimelineOther;

            PillBlue = p.PillBlue;
            PillGold = p.PillGold;
            PillRed = p.PillRed;
            PillGreen = p.PillGreen;
        }

        // ---- Layer 0: surfaces (background → raised) ----
        internal static Color Window;
        internal static Color Panel;
        internal static Color PanelRaised;
        internal static Color Card;
        internal static Color CardHover;

        // ---- Layer 1: borders (graded by information weight) ----
        /// <summary>Primary outline for panels/cards (strongest).</summary>
        internal static Color Border;
        /// <summary>Soft divider / secondary outline.</summary>
        internal static Color BorderSoft;
        /// <summary>Hairline rule between rows (weakest).</summary>
        internal static Color BorderHair;

        // ---- Layer 2: text ----
        internal static Color Text;
        internal static Color Muted;
        internal static Color Dim;
        /// <summary>Alias kept for legacy call sites; mirrors Muted.</summary>
        internal static Color SecondaryText;

        // ---- Layer 3: semantic accents ----
        internal static Color Accent;
        internal static Color AccentSoft;
        internal static Color Info;
        internal static Color Alive;
        internal static Color Dead;
        /// <summary>Blood-crimson accent for impaired health / depreciation warnings.</summary>
        internal static Color Blood;

        // ---- Layer 3b: native warning / threat / faction relations ----
        internal static Color Warn;
        internal static Color Threat;
        internal static Color FactionAlly;
        internal static Color FactionHostile;
        internal static Color FactionNeutral;

        // ---- Layer 3c: translucent overlays / alpha helpers ----
        /// <summary>White 4% overlay for placeholder / empty-state boxes.</summary>
        internal static Color OverlayWhite04;
        /// <summary>Info accent at 50% alpha (event-row accent bar).</summary>
        internal static Color InfoSoft;
        /// <summary>Dark fill for the "mid-install / incomplete" badge.</summary>
        internal static Color BadgeIncompleteFill;

        /// <summary>Returns <paramref name="c"/> with its alpha overridden (RGB kept).</summary>
        internal static Color WithAlpha(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);

        // ---- Layer 4: timeline node tints (per event kind) ----
        internal static Color TimelineSpine;
        internal static Color TimelineJoin;
        internal static Color TimelineDeath;
        internal static Color TimelineBattle;
        internal static Color TimelineSocial;
        internal static Color TimelineCraft;
        internal static Color TimelineBuilt;
        internal static Color TimelineOther;

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
        internal const float CardPadX = 12f;
        internal const float CardPadY = 8f;
        internal const float GridGap = 8f;
        internal const float BlockGap = 12f;
        internal const float ProgressbarH = 12f;
        internal const float ProgressbarValueW = 116f;

        // ---- State color resolver (semantic) ----
        internal static Color StateColor(bool positive, bool negative)
        {
            if (positive) return Alive;
            if (negative) return Dead;
            return Info;
        }

        // ---- Pill/tag tints ----
        internal static Color PillBlue;
        internal static Color PillGold;
        internal static Color PillRed;
        internal static Color PillGreen;

        // ---- v4.15: single semantic->tone mapping for six digest cells ----
        internal static Color TintForEventKind(string kindKey)
        {
            switch (kindKey)
            {
                case "work": return PillGold;
                case "prod": return PillGreen;
                case "kill": return PillRed;
                case "battle": return PillBlue;
                case "consume": return PillBlue;
                case "foot": return Info;
                case "rel": return Alive;
                case "career": return PillGold;
                case "home": return Info;
                case "legacy": return PillRed;
                case "health": return Alive;
                default: return Accent;
            }
        }

        // ---- v1.1.4: medal tier tint (gold/silver/bronze), derived from palette ----
        internal static Color MedalTierColor(MedalTier tier)
        {
            switch (tier)
            {
                case MedalTier.Gold: return PillGold;
                case MedalTier.Silver: return SecondaryText;
                case MedalTier.Bronze: return TimelineCraft;
                default: return SecondaryText;
            }
        }

        /// <summary>
        /// A complete colour palette. Palettes are immutable; only the UITheme
        /// statics mutate when one is applied.
        /// </summary>
        private readonly struct Palette
        {
            internal readonly Color Window, Panel, PanelRaised, Card, CardHover;
            internal readonly Color Border, BorderSoft, BorderHair;
            internal readonly Color Text, Muted, Dim, SecondaryText;
            internal readonly Color Accent, AccentSoft, Info, Alive, Dead, Blood;
            internal readonly Color Warn, Threat, FactionAlly, FactionHostile, FactionNeutral;
            internal readonly Color OverlayWhite04, InfoSoft, BadgeIncompleteFill;
            internal readonly Color TimelineSpine, TimelineJoin, TimelineDeath, TimelineBattle,
                TimelineSocial, TimelineCraft, TimelineBuilt, TimelineOther;
            internal readonly Color PillBlue, PillGold, PillRed, PillGreen;

            internal Palette(
                Color window, Color panel, Color raised, Color card, Color hover,
                Color border, Color borderSoft, Color hair,
                Color text, Color muted, Color dim,
                Color accent, Color info, Color alive, Color dead,
                Color warn, Color threat,
                Color ally, Color hostile, Color neutral,
                Color timelineSpine, Color timelineJoin, Color timelineDeath, Color timelineBattle,
                Color timelineSocial, Color timelineCraft, Color timelineBuilt)
            {
                Window = window; Panel = panel; PanelRaised = raised; Card = card; CardHover = hover;
                Border = border; BorderSoft = borderSoft; BorderHair = hair;
                Text = text; Muted = muted; Dim = dim;
                SecondaryText = muted;                       // alias mirrors Muted
                Accent = accent;
                AccentSoft = new Color(accent.r, accent.g, accent.b, 0.15f);
                Info = info; Alive = alive; Dead = dead;
                Blood = dead;                                // blood-crimson alias
                Warn = warn; Threat = threat;
                FactionAlly = ally; FactionHostile = hostile; FactionNeutral = neutral;
                OverlayWhite04 = new Color(1f, 1f, 1f, 0.04f);
                InfoSoft = new Color(info.r, info.g, info.b, 0.5f);
                BadgeIncompleteFill = new Color(0.16f, 0.13f, 0.11f, 1f);
                TimelineSpine = timelineSpine; TimelineJoin = timelineJoin;
                TimelineDeath = timelineDeath; TimelineBattle = timelineBattle;
                TimelineSocial = timelineSocial; TimelineCraft = timelineCraft;
                TimelineBuilt = timelineBuilt; TimelineOther = Color.gray;
                // Pill tints derived from the palette's core accents so every theme
                // keeps a coherent tag system without inventing foreign hues.
                PillBlue = info;
                PillGold = accent;
                PillRed = dead;
                PillGreen = alive;
            }

            /// <summary>Epic 史诗奇幻（默认）：暗紫深蓝底 + 鎏金描边 + 衬线风格。
            /// v1.1.5: tuned for texture overlay — text/muted brightened for
            /// parchment readability; pill tints shifted to warm fantasy palette
            /// (amber/crimson/emerald/bronze) instead of modern blue/red.</summary>
            internal static readonly Palette Epic = new Palette(
                window: new Color(0.055f, 0.075f, 0.133f, 1f),
                panel: new Color(0.082f, 0.106f, 0.180f, 1f),
                raised: new Color(0.110f, 0.137f, 0.220f, 1f),
                card: new Color(0.102f, 0.129f, 0.212f, 1f),
                hover: new Color(0.145f, 0.176f, 0.267f, 1f),
                border: new Color(0.541f, 0.467f, 0.251f, 1f),
                borderSoft: new Color(0.235f, 0.251f, 0.376f, 1f),
                hair: new Color(0.196f, 0.212f, 0.320f, 1f),
                text: new Color(0.945f, 0.922f, 0.847f, 1f),       // +bright for texture
                muted: new Color(0.720f, 0.694f, 0.600f, 1f),      // +bright (was 0.60)
                dim: new Color(0.490f, 0.478f, 0.553f, 1f),        // +bright (was 0.42)
                accent: new Color(0.831f, 0.686f, 0.216f, 1f),      // gold unchanged
                info: new Color(0.855f, 0.706f, 0.341f, 1f),        // amber-gold (was blue)
                alive: new Color(0.459f, 0.773f, 0.498f, 1f),       // emerald (was mint)
                dead: new Color(0.753f, 0.278f, 0.196f, 1f),        // deep crimson (was coral)
                warn: new Color(1.000f, 0.000f, 0.000f, 1f),
                threat: new Color(0.784f, 0.353f, 0.290f, 1f),
                ally: new Color(0.400f, 1.000f, 0.500f, 1f),
                hostile: new Color(1.000f, 0.250f, 0.200f, 1f),
                neutral: new Color(0.350f, 0.700f, 1.000f, 1f),
                timelineSpine: new Color(0.55f, 0.50f, 0.38f, 1f),   // warmer brass
                timelineJoin: new Color(0.459f, 0.773f, 0.498f, 1f), // emerald
                timelineDeath: new Color(0.753f, 0.278f, 0.196f, 1f), // deep crimson
                timelineBattle: new Color(0.855f, 0.706f, 0.341f, 1f), // amber-gold
                timelineSocial: new Color(0.588f, 0.494f, 0.737f, 1f), // amethyst
                timelineCraft: new Color(0.79f, 0.66f, 0.38f, 1f),
                timelineBuilt: new Color(0.55f, 0.53f, 0.62f, 1f));

            /// <summary>Gothic 暗黑哥特：墨黑底 + 血红点缀（血源/暗黑破坏神风）。</summary>
            internal static readonly Palette Gothic = new Palette(
                window: new Color(0.039f, 0.039f, 0.051f, 1f),
                panel: new Color(0.071f, 0.071f, 0.086f, 1f),
                raised: new Color(0.110f, 0.110f, 0.133f, 1f),
                card: new Color(0.086f, 0.086f, 0.110f, 1f),
                hover: new Color(0.125f, 0.110f, 0.145f, 1f),
                border: new Color(0.290f, 0.188f, 0.220f, 1f),
                borderSoft: new Color(0.165f, 0.133f, 0.188f, 1f),
                hair: new Color(0.125f, 0.102f, 0.145f, 1f),
                text: new Color(0.812f, 0.784f, 0.847f, 1f),
                muted: new Color(0.518f, 0.463f, 0.557f, 1f),
                dim: new Color(0.361f, 0.329f, 0.408f, 1f),
                accent: new Color(0.659f, 0.196f, 0.196f, 1f),
                info: new Color(0.478f, 0.478f, 0.690f, 1f),
                alive: new Color(0.373f, 0.604f, 0.416f, 1f),
                dead: new Color(0.784f, 0.251f, 0.251f, 1f),
                warn: new Color(1.000f, 0.100f, 0.100f, 1f),
                threat: new Color(0.784f, 0.251f, 0.251f, 1f),
                ally: new Color(0.400f, 0.900f, 0.500f, 1f),
                hostile: new Color(1.000f, 0.200f, 0.200f, 1f),
                neutral: new Color(0.500f, 0.500f, 0.900f, 1f),
                timelineSpine: new Color(0.30f, 0.28f, 0.38f, 1f),
                timelineJoin: new Color(0.37f, 0.60f, 0.42f, 1f),
                timelineDeath: new Color(0.78f, 0.25f, 0.25f, 1f),
                timelineBattle: new Color(0.66f, 0.23f, 0.23f, 1f),
                timelineSocial: new Color(0.48f, 0.42f, 0.78f, 1f),
                timelineCraft: new Color(0.60f, 0.48f, 0.29f, 1f),
                timelineBuilt: new Color(0.36f, 0.35f, 0.45f, 1f));

            /// <summary>Wuxia 东方武侠水墨：宣纸米白底 + 墨色 + 朱红印章（浅色主题）。</summary>
            internal static readonly Palette Wuxia = new Palette(
                window: new Color(0.910f, 0.886f, 0.816f, 1f),
                panel: new Color(0.871f, 0.839f, 0.749f, 1f),
                raised: new Color(0.835f, 0.800f, 0.710f, 1f),
                card: new Color(0.886f, 0.855f, 0.769f, 1f),
                hover: new Color(0.800f, 0.761f, 0.663f, 1f),
                border: new Color(0.227f, 0.200f, 0.169f, 1f),
                borderSoft: new Color(0.435f, 0.400f, 0.341f, 1f),
                hair: new Color(0.380f, 0.345f, 0.290f, 1f),
                text: new Color(0.169f, 0.149f, 0.125f, 1f),
                muted: new Color(0.373f, 0.345f, 0.298f, 1f),
                dim: new Color(0.478f, 0.447f, 0.392f, 1f),
                accent: new Color(0.651f, 0.227f, 0.180f, 1f),
                info: new Color(0.227f, 0.416f, 0.541f, 1f),
                alive: new Color(0.310f, 0.478f, 0.333f, 1f),
                dead: new Color(0.541f, 0.184f, 0.184f, 1f),
                warn: new Color(0.700f, 0.100f, 0.050f, 1f),
                threat: new Color(0.541f, 0.184f, 0.184f, 1f),
                ally: new Color(0.200f, 0.600f, 0.300f, 1f),
                hostile: new Color(0.700f, 0.200f, 0.150f, 1f),
                neutral: new Color(0.200f, 0.450f, 0.700f, 1f),
                timelineSpine: new Color(0.42f, 0.38f, 0.30f, 1f),
                timelineJoin: new Color(0.31f, 0.48f, 0.33f, 1f),
                timelineDeath: new Color(0.54f, 0.18f, 0.18f, 1f),
                timelineBattle: new Color(0.65f, 0.23f, 0.18f, 1f),
                timelineSocial: new Color(0.29f, 0.35f, 0.48f, 1f),
                timelineCraft: new Color(0.54f, 0.42f, 0.16f, 1f),
                timelineBuilt: new Color(0.42f, 0.42f, 0.35f, 1f));

            /// <summary>Steampunk 蒸汽朋克：黄铜棕 + 宝石绿（羞辱/雾都孤儿风）。</summary>
            internal static readonly Palette Steampunk = new Palette(
                window: new Color(0.078f, 0.071f, 0.051f, 1f),
                panel: new Color(0.110f, 0.102f, 0.071f, 1f),
                raised: new Color(0.145f, 0.133f, 0.094f, 1f),
                card: new Color(0.125f, 0.114f, 0.078f, 1f),
                hover: new Color(0.176f, 0.157f, 0.110f, 1f),
                border: new Color(0.541f, 0.478f, 0.227f, 1f),
                borderSoft: new Color(0.290f, 0.259f, 0.188f, 1f),
                hair: new Color(0.235f, 0.208f, 0.145f, 1f),
                text: new Color(0.878f, 0.839f, 0.682f, 1f),
                muted: new Color(0.604f, 0.565f, 0.471f, 1f),
                dim: new Color(0.416f, 0.400f, 0.322f, 1f),
                accent: new Color(0.290f, 0.604f, 0.478f, 1f),
                info: new Color(0.478f, 0.627f, 0.816f, 1f),
                alive: new Color(0.498f, 0.792f, 0.541f, 1f),
                dead: new Color(0.722f, 0.353f, 0.290f, 1f),
                warn: new Color(1.000f, 0.300f, 0.100f, 1f),
                threat: new Color(0.722f, 0.353f, 0.290f, 1f),
                ally: new Color(0.350f, 0.850f, 0.450f, 1f),
                hostile: new Color(1.000f, 0.300f, 0.200f, 1f),
                neutral: new Color(0.400f, 0.650f, 1.000f, 1f),
                timelineSpine: new Color(0.42f, 0.38f, 0.26f, 1f),
                timelineJoin: new Color(0.50f, 0.79f, 0.54f, 1f),
                timelineDeath: new Color(0.72f, 0.35f, 0.29f, 1f),
                timelineBattle: new Color(0.85f, 0.53f, 0.23f, 1f),
                timelineSocial: new Color(0.48f, 0.54f, 0.82f, 1f),
                timelineCraft: new Color(0.79f, 0.66f, 0.28f, 1f),
                timelineBuilt: new Color(0.48f, 0.48f, 0.35f, 1f));
        }
    }
}
