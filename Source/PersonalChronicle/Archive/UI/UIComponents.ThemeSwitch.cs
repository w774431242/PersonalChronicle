using PersonalChronicle.MasterControl.Population;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive.UI
{
    /// <summary>
    /// <see cref="UIComponents"/> 的「主题开关」切片 —— **总控中心与档案馆的唯一实现**。
    ///
    /// <para><b>它修的是什么（2026-09-13 实机截图）</b>：两个窗口原本各写一套
    /// 「标签 · 主题名」<b>拼成单串</b>再整体测量的按钮
    /// （<c>MasterControlTabWindow.Shell.cs</c> 与 <c>PersonnelArchivePanel.Shell.cs</c> 的 DrawHeader），
    /// 且宽度带 <c>Mathf.Clamp(w, 120f, 260f)</c> 的<b>固定最小宽</b> + 右锚 ⇒
    /// 可用宽不足时 x 变负、被外层 <c>GUI.BeginGroup</c> 从左裁掉。</para>
    /// <para>实机症状②：嵌入档里出现 <c>题 · 暗黑哥特</c>（「主题」被硬切）；症状①：两个控件紧贴，
    /// 被读成「主题暗黑哥特」一个词（缺段间分隔）。本组件把「可用宽 → 档位」做成显式降级链
    /// ⇒ <b>永不越界、永不硬切</b>。</para>
    ///
    /// <para><b>降级链（LAND-006 D1/D4/D6，顺序固定，不得自创）</b>：
    /// D0 完整（标签段 + 1px 分隔 + 值段）→ D1 只值段（标签语义进悬停）→
    /// D2 只图标（值语义进悬停）→ D6 <b>不画</b>（悬停保留，说明当前主题与切换目标）。</para>
    ///
    /// <para><b>键盘焦点态 = B 类降级（登记于此，不假装实现）</b>：IMGUI 未向 Mod 暴露稳定的焦点标识，
    /// 自绘控件拿不到焦点环；其余五态（默认/悬停/按下/选中=值段高亮/禁用底）齐备。</para>
    /// </summary>
    internal static partial class UIComponents
    {
        /// <summary>
        /// 控件高。来源：预览 <c>#top .grp button{padding:3px 9px;line-height:1.25}</c>
        /// （<c>系统中心蓝图/待落地项/制造系统中心预览_RUIF单页统一版.html</c> 页头按钮段）⇒
        /// Tiny 行高 18 + 上下各 3 ≈ 24，引擎整点取 <c>26</c>（与页头「返回」按钮同高 ⇒ 基线对齐）。
        /// </summary>
        internal const float ThemeSwitchHeight = 26f;

        /// <summary>
        /// 段内左右内边距（单侧）。来源：同选择器横向 9px；按 LAND-007 的 10–12 档上调
        /// （CJK 在 Tiny 档下贴边局促）。**只此一处**，实机觉得宽窄只改它。
        /// </summary>
        private const float ThemeSwitchPadX = 12f;

        /// <summary>段间分隔线宽（预览 <c>button+button{border-left:1px}</c> 同款）。</summary>
        private const float ThemeSwitchSepW = 1f;

        /// <summary>图标档控件宽（只画图标时的最小可点宽）。</summary>
        private const float ThemeSwitchIconWidth = 26f;

        /// <summary>图标档字形。取本工程**已在用**的齿轮字形，避免缺字形画成豆腐块。</summary>
        internal const string ThemeSwitchGlyph = "⚙";

        // ---- 文案：按 **主题 + 语言** 失效的**有界**缓存（4 条），绘制路径零 Translate / 零拼接 ----
        // 旧实现两个窗口**每帧**各做一次 Translate + 字符串拼接（PERF-001 绘制路径零分配被违反）。
        // ⚠ 2026-09-15（审计 UI-001 修复）：缓存键补上**语言** —— 只按主题失效时，运行中切换语言后
        // 主题名 / 提示会停留在旧语言（要到下次切主题才刷新）。
        private static string themeCacheKey;
        private static string themeCacheLabel;
        private static string themeCacheValue;
        private static string themeCacheTip;

        /// <summary>
        /// 主题开关（**右锚**于 <paramref name="area"/>）：返回本帧是否被点击；
        /// <paramref name="usedWidth"/> = 实际占用宽（<c>0</c> = D6 未画）。
        /// 点击 = 切到循环顺序里的下一个主题并持久化（与两窗口原行为逐字等价）。
        /// </summary>
        internal static bool ThemeSwitch(Rect area, out float usedWidth)
        {
            usedWidth = 0f;
            EnsureThemeTexts();
            float height = Mathf.Min(area.height, ThemeSwitchHeight);
            if (height <= 0f || area.width <= 0f || string.IsNullOrEmpty(themeCacheValue))
            {
                return false;
            }

            // ---- 可用宽 → 档位（从宽到窄，第一个放得下的即答案）----
            float labelW = ThemeSegmentWidth(themeCacheLabel);
            float valueW = ThemeSegmentWidth(themeCacheValue);
            float fullW = labelW + ThemeSwitchSepW + valueW;
            bool showLabel;
            bool showValue;
            if (fullW <= area.width)
            {
                usedWidth = fullW;
                showLabel = true;
                showValue = true;
            }
            else if (valueW <= area.width)
            {
                usedWidth = valueW;      // D1：标签段放不下 ⇒ 只段值段
                showLabel = false;
                showValue = true;
            }
            else if (ThemeSwitchIconWidth <= area.width)
            {
                usedWidth = ThemeSwitchIconWidth;   // D2：只图标
                showLabel = false;
                showValue = false;
            }
            else
            {
                showLabel = false;
                showValue = false;
            }

            if (usedWidth <= 0f)
            {
                // D6：不画半截控件，但**保留悬停**（至少让玩家读到当前主题与切换目标）。
                TooltipHandler.TipRegion(area, themeCacheTip);
                return false;
            }

            Rect core = new Rect(area.xMax - usedWidth, area.y, usedWidth, height);
            bool hover = Mouse.IsOver(core);
            bool clicked = hover && Widgets.ButtonInvisible(core);
            // 命中检测用**同一个矩形**做，避免"画在一处、检在另一处"的漂移。

            // ---- 五态（颜色全走 UITheme 令牌，无裸色）----
            Widgets.DrawBoxSolid(core, hover ? UITheme.CardHover : UITheme.PanelRaised);
            Border(core, hover ? UITheme.Accent : UITheme.BorderSoft);

            if (showValue)
            {
                // 值段 = 「当前值」高亮段（预览 `.seg2 button.on` 的等价物）
                Rect valueSeg = showLabel
                    ? new Rect(core.x + labelW + ThemeSwitchSepW, core.y, valueW, core.height)
                    : core;
                Widgets.DrawBoxSolid(valueSeg, UITheme.AccentSoft);
                if (showLabel)
                {
                    // 段间 1px 分隔（用 DrawBoxSolid：**两点版 Widgets.DrawLine 走 GL，
                    // 不认 GUI.matrix**，在组/裁剪内会错位 —— 本工程 2026-09-13 已实测）。
                    Widgets.DrawBoxSolid(
                        new Rect(valueSeg.x - ThemeSwitchSepW, core.y, ThemeSwitchSepW, core.height),
                        UITheme.BorderSoft);
                    Label(new Rect(core.x, core.y, labelW, core.height),
                        TruncateToWidth(themeCacheLabel, Mathf.Max(0f, labelW - ThemeSwitchPadX * 2f),
                            GameFont.Tiny),
                        GameFont.Tiny, UITheme.Text, TextAnchor.MiddleCenter);
                }
                Label(valueSeg,
                    TruncateToWidth(themeCacheValue, Mathf.Max(0f, valueSeg.width - ThemeSwitchPadX * 2f),
                        GameFont.Tiny),
                    GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleCenter);
            }
            else
            {
                Label(core, ThemeSwitchGlyph, GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleCenter);
            }

            TooltipHandler.TipRegion(core, themeCacheTip);
            if (clicked)
            {
                ApplyNextTheme();
            }
            return clicked;
        }

        /// <summary>段宽 = 文本实宽 + 两侧内边距；空文本给「两倍内边距」而不是 0（0 宽段画出来像缺一块）。</summary>
        private static float ThemeSegmentWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return ThemeSwitchPadX * 2f;
            }
            GameFont prev = Text.Font;
            Text.Font = GameFont.Tiny;
            try
            {
                return Text.CalcSize(text).x + ThemeSwitchPadX * 2f;
            }
            finally
            {
                Text.Font = prev;
            }
        }

        /// <summary>
        /// 按 <b>主题 + 当前语言</b> 失效的文案缓存（容量固定 ⇒ 有界，无静态泄漏）。
        /// ⚠ 2026-09-15（审计 UI-001 修复）：旧的单键（仅主题）在**运行中切换语言**时不刷新。
        /// </summary>
        private static void EnsureThemeTexts()
        {
            string active = UITheme.ActiveThemeId;
            string lang = Verse.LanguageDatabase.activeLanguage == null
                ? string.Empty
                : Verse.LanguageDatabase.activeLanguage.folderName;
            string cacheKey = active + "|" + lang;
            if (themeCacheLabel != null && themeCacheKey == cacheKey)
            {
                return;
            }
            themeCacheKey = cacheKey;
            string next = UITheme.NextThemeId(active);
            themeCacheLabel = "PersonalChronicle.UI.Theme.Button".Translate().ToString();
            themeCacheValue = UITheme.GetDisplayName(active);
            themeCacheTip = "PersonalChronicle.UI.Theme.ButtonTip".Translate(
                (NamedArgument)UITheme.GetDisplayName(active),
                (NamedArgument)UITheme.GetDisplayName(next)).ToString();
        }

        /// <summary>切换 + 持久化（与两个窗口原实现逐字等价）；切换后立即失效文案缓存。</summary>
        private static void ApplyNextTheme()
        {
            string next = UITheme.NextThemeId(UITheme.ActiveThemeId);
            UITheme.Apply(next);
            if (PersonalChronicleMod.Settings != null)
            {
                PersonalChronicleMod.Settings.ThemeId = next;
                PersonalChronicleMod.Settings.Write();
            }
            themeCacheKey = null;
        }
    }
}
