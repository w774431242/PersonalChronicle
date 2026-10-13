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
        public const string ThemeSteampunk = "steampunk";// 蒸汽朋克

        /// <summary>All selectable theme ids, in display order (button cycle order).</summary>
        internal static readonly string[] ThemeIds = { ThemeEpic, ThemeGothic, ThemeSteampunk };

        /// <summary>Text shadow colour used by <see cref="UIComponents.ShadowLabel"/>.
        /// Centralised here (Design System Layer 1) so the shadow tint is a single
        /// token instead of a hard-coded literal in the drawing code (ASSET-002).
        /// v4.17 体检：readonly 字段（旧 getter 每次 new Color，虽无堆分配但属散落色值）。</summary>
        internal static readonly Color Shadow = new Color(0f, 0f, 0f, 0.85f);

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
            // ⚠ 2026-09-19（CHG-20260919-ECON-UI-010）：改走语言级缓存。
            // 键是**动态拼**出来的（`Theme.Xxx`）—— `UIComponents.Loc` 自带容量兜底
            // （超出即清表，绝不无界增长）且随语言失效 ⇒ 安全。
            string translated = UIComponents.Loc(key);
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
        /// 把任意主题 id 规范化为 <see cref="ThemeIds"/> 中的合法值（未知 / null ⇒ <c>ThemeIds[0]</c>）。
        /// ⚠ 2026-09-15（审计 UI-001）：存档里的主题 id 若来自旧版本 / 第三方写入，可能不在当前
        /// 主题集内；规范化后 `Apply` 的「已应用状态」与「实际调色板」才一致（无则点击循环会错位）。
        /// </summary>
        internal static string NormalizeThemeId(string themeId)
        {
            if (string.IsNullOrEmpty(themeId))
            {
                return ThemeIds[0];
            }
            return Array.IndexOf(ThemeIds, themeId) >= 0 ? themeId : ThemeIds[0];
        }

        /// <summary>
        /// Applies the named palette to every mutable colour token and re-syncs the
        /// <see cref="ArchiveUiStyle"/> forwards. Unknown id silently keeps the current
        /// palette (so a corrupt save never bricks the UI). Safe to call every frame;
        /// it only overwrites colours, never allocates.
        /// </summary>
        internal static void Apply(string themeId)
        {
            // ⚠ 2026-09-15（审计 UI-001 修复）：**未知 id 先规范化为合法值** ——
            // 旧实现「调色板回落 Epic、但 `ActiveThemeId` 仍写入未知值」⇒ 会出现
            // ① 状态与显示不一致；② `NextThemeId(未知)` 走 `idx < 0 → ThemeIds[0]`，
            // 若当前恰是第一个主题则点击**表现为无反应**（用户可感知的交互缺陷）。
            string normalized = NormalizeThemeId(themeId);
            Palette p;
            switch (normalized)
            {
                case ThemeGothic: p = Palette.Gothic; break;
                case ThemeSteampunk: p = Palette.Steampunk; break;
                default: p = Palette.Epic; break;
            }
            Assign(p);
            ActiveThemeId = normalized;

            // ⭐ 2026-09-12 RUIF 软依赖（改造 P1「令牌层」）：
            //   装了 RUIF ⇒ 用其**统一令牌**覆盖同名令牌（字段名一个不改 ⇒ 全部调用点零改动即完成换皮）；
            //   没装 RUIF ⇒ 上面 Assign 的三套自有主题照常生效（回落路径，GATE-COMP）。
            if (RuifBridge.TryGetTokens(out RuifBridge.TokenSet ruif))
            {
                ruifTokensApplied = true;
                Window = ruif.Window;
                Panel = ruif.Panel;
                PanelRaised = ruif.PanelRaised;
                Card = ruif.Card;
                CardHover = ruif.CardHover;
                Border = ruif.Border;
                BorderSoft = ruif.BorderSoft;
                BorderHair = ruif.BorderHair;
                Text = ruif.Text;
                Muted = ruif.Muted;
                Dim = ruif.Dim;
                Accent = ruif.Accent;
                Info = ruif.Info;
                Alive = ruif.Alive;
                Dead = ruif.Dead;
                Warn = ruif.Warn;
                // 强调色柔化档跟随（与上面 Epic 分支同式）
                AccentSoft = new Color(ruif.Accent.r, ruif.Accent.g, ruif.Accent.b, 0.28f);
            }

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

        /// <summary>RUIF 令牌是否已成功套用（置位后不再重复尝试）。</summary>
        private static bool ruifTokensApplied;

        /// <summary>
        /// **RUIF 晚于 PC 加载时的补应用入口**（由 <see cref="RuifBridge"/> 在探测成功那一刻回调）。
        ///
        /// <para>启动期 <see cref="Apply"/> 执行时 RUIF 往往**尚未进入 AppDomain**（1.6 的 Mod 类创建
        /// 按类型枚举顺序、不是加载顺序）⇒ 那一刻套用的是本工程调色板，且**不会自动重试** ——
        /// 实机表现就是"装/没装 RUIF 看起来一样"。本方法在 RUIF 就绪后**幂等补一次**。</para>
        /// </summary>
        internal static void ReapplyForRuif()
        {
            if (ruifTokensApplied)
            {
                return;
            }
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

        /// <summary>
        /// 画布对齐吸附的参考线色（车间俯视编排画布拖拽 Shift 时显示）。
        ///
        /// <para>**刻意做成派生属性而非 Apply 里赋值的字段**：它完全由 <see cref="Accent"/> 决定，
        /// 派生写法保证任何主题切换（含 RUIF 晚加载的补应用、ThemeSwitch 内嵌切换）都
        /// 自动同步，**不可能出现"切了主题参考线还是旧颜色"**。同时把「0.85 alpha」这个
        /// 魔法值收在 Theme 令牌里，调用方不再散落 <c>new Color(...)</c>（GATE-UI 第 3 条）。</para>
        /// </summary>
        internal static Color AlignGuide => WithAlpha(Accent, 0.85f);

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

        // ---- v1.1.5: professional title tier tint (员/助/中/副高/正高) ----
        // 用户定稿（2026-08-22）：职称 5 档色值对齐 RimWorld 品质色阶
        // （QualityColors 风格：Awful→Legendary 的常规映射），跨主题固定不随皮肤漂移，
        // 让玩家在任意主题下都凭色一眼识别档位。固定十六进制：
        //   员级=灰  助理=白  中级=绿  副高=蓝  正高=金
        internal static readonly Color TitleTier0 = new Color(0.502f, 0.502f, 0.502f, 1f); // #808080 员级灰
        internal static readonly Color TitleTier1 = new Color(1.000f, 1.000f, 1.000f, 1f); // #FFFFFF 助理白
        internal static readonly Color TitleTier2 = new Color(0.298f, 0.686f, 0.314f, 1f); // #4CAF50 中级绿
        internal static readonly Color TitleTier3 = new Color(0.133f, 0.588f, 0.953f, 1f); // #2196F3 副高蓝
        internal static readonly Color TitleTier4 = new Color(1.000f, 0.757f, 0.027f, 1f); // #FFC107 正高金

        /// <summary>职称档位色（固定品质色阶，跨主题一致）。tier 越界回退 Tier0 灰。</summary>
        internal static Color TitleTierColor(int tier)
        {
            switch (tier)
            {
                case 0: return TitleTier0;
                case 1: return TitleTier1;
                case 2: return TitleTier2;
                case 3: return TitleTier3;
                case 4: return TitleTier4;
                default: return TitleTier0;
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
                // ⚠ 2026-09-19（审计 §2.6）：旧值 (0.784,0.353,0.290) 与同主题 `dead`
                // (0.753,0.278,0.196) 的距离仅 **0.12** —— 两个**语义不同**的令牌几乎同色
                // ⇒ 任何区分"死亡/失败"与"威胁/危险"的界面都传达不出差异。
                // ⇒ 改为**偏橙的危险色**（保留"警示"语义，与深红的 dead 拉开到 **0.25**），
                //   同时与纯红的 `warn`(1.0,0,0) 距离 0.55，三者互不混淆。
                threat: new Color(0.878f, 0.494f, 0.196f, 1f),
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
                // ⚠ 2026-09-19（审计 §2.7）：旧值 (0.361,0.329,0.408) 对 `card`(0.086,0.086,0.110)
                // 的 **WCAG 对比度仅 ≈2.5:1**（按相对亮度公式复算确认），远低于小字号文本要求的 4.5:1
                // ⇒ Gothic 主题下次要文字**基本读不清**。
                // ⇒ 提到 (0.540,0.490,0.610)：对 `card` ≈ **4.7:1**、对 `panel` ≈ **4.9:1**（均达标），
                //   色相仍是紫族（与 Epic 的紫灰 dim 同族），在近黑底上仍属"次级"视觉档。
                dim: new Color(0.540f, 0.490f, 0.610f, 1f),
                accent: new Color(0.659f, 0.196f, 0.196f, 1f),
                info: new Color(0.478f, 0.478f, 0.690f, 1f),
                alive: new Color(0.373f, 0.604f, 0.416f, 1f),
                dead: new Color(0.784f, 0.251f, 0.251f, 1f),
                warn: new Color(1.000f, 0.100f, 0.100f, 1f),
                // ⚠ 2026-09-19（审计 §2.6）：旧值与同主题 `dead` **逐通道完全相同**
                // (0.784,0.251,0.251) ⇒ 两个语义不同的令牌在界面上**不可能被区分**。
                // ⇒ 改为偏橙的危险色（与 dead 距离 **0.36**、与纯红 `warn`(1.0,0.1,0.1) 距离 0.36）。
                threat: new Color(0.902f, 0.435f, 0.180f, 1f),
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
                // ⚠ 2026-09-19（审计 §2.7 **续**）：审计只点了 Gothic 的 `dim`，但按同一公式复算，
                // **本主题同样不达标**：该值对 `card`(0.125,0.114,0.078) ≈ **2.92:1**，
                // 在 `WithAlpha(dim, 0.15)` 的徽章底上更是 ≈ **2.65:1**（`Embedded.cs:226` 的口径徽章
                // 正是"dim 文字画在 dim 底色上"）。
                // ⇒ 提到 (0.545,0.525,0.425)：对 `card` ≈ **4.6:1**、对 `panel` ≈ **4.7:1**（均达标），
                //   色相仍是黄铜族（与主题一致），在深底上仍属"次级"视觉档。
                // 📌 **审计没点到不等于达标** —— 同一缺陷要按公式**逐主题**过一遍。
                dim: new Color(0.545f, 0.525f, 0.425f, 1f),
                accent: new Color(0.290f, 0.604f, 0.478f, 1f),
                info: new Color(0.478f, 0.627f, 0.816f, 1f),
                alive: new Color(0.498f, 0.792f, 0.541f, 1f),
                dead: new Color(0.722f, 0.353f, 0.290f, 1f),
                warn: new Color(1.000f, 0.300f, 0.100f, 1f),
                // ⚠ 2026-09-19（审计 §2.6）：旧值与同主题 `dead` **逐通道完全相同**
                // (0.722,0.353,0.290) ⇒ 同上，语义被颜色抹平。
                // ⇒ 改为偏橙的危险色（与 dead 距离 **0.26**、与橙 `warn`(1.0,0.3,0.1) 距离 0.26）。
                threat: new Color(0.878f, 0.510f, 0.196f, 1f),
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
