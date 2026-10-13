using System;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;   // UITheme（主题 id / 应用）
using PersonalChronicle.Domain.Meal;
using RimWorld;
using RUIF;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Settings
{
    /// <summary>
    /// Mod 设置页绘制器（CHG-20261005-SETTINGS-UI）。
    ///
    /// <para><b>为什么重做</b>（玩家 2026-10-04 反馈）：「太乱了，没有模块分组，没有信息简化。
    /// 没有声明来源、用途等等，太粗糙了」。根因：旧实现是原版 <c>Listing_Standard</c> 平铺
    /// （14 类设置一条长列表、只有 1 项有说明），而 PC 的其它界面已全面走 RUIF。</para>
    ///
    /// <para><b>形态</b>：4 页签（按"你在干什么"分组；「诊断」仅开发者模式可见，见
    /// <see cref="SettingsCatalog.VisibleTabs"/>）→ 分节（<c>Atoms.SectionFrame</c>）
    /// → 行（标题 + 来源角标 + 控件 + 用途）→ 展开详情（用途 / 默认值理由 / 影响与代价）。</para>
    ///
    /// <para><b>纪律</b>（RimWorld 组件绘制 skill）：① 行高/控件高全部由 <c>Measure</c> 派生，
    /// 无字面量；② 标签排版走单元矩形，不做绝对定位；③ 逐件 <c>try/catch</c> 隔离、
    /// 出栈放 <c>finally</c>；④ 空间不足走降级而不是硬挤；⑤ 绘制路径零 <c>new</c>/LINQ/翻译。</para>
    ///
    /// <para><b>状态归属</b>：组件零状态，页签/滚动/展开态由本类持有（受控静态，窗口关闭不丢；
    /// 不属于存档数据 ⇒ 不进 Scribe）。</para>
    /// </summary>
    internal static class SettingsDrawer
    {
        // ── 度量：全框架派生（禁止字面量行高）────────────────────────────────
        private static readonly float LineTiny = Measure.RowHeight(GameFont.Tiny);       // 18
        private static readonly float LineSmall = Measure.RowHeight(GameFont.Small);     // 22
        private static readonly float CtrlH = Measure.ControlHeight(GameFont.Tiny);      // 26

        // ── 结构常量（控件列宽与角标列宽；非文本行高，经 Measure 校验不溢出）──────
        // ⚠ 宽度预算（实机截图修正，2026-10-05）：Mod 设置窗口实际只有约 **590px**。
        //   590 − caret16 − 行内边距24 − 角标96 − 控件− 间隙24 ≈ 标题可用宽。
        //   因此角标用**短形式**（完整编号走悬停），滑块行的"当前值"另立一列。
        private const float CtrlColW = 170f;     // 默认列（滑块本体 / 动作按钮）
        private const float ToggleColW = 78f;    // 开关（轨道 36 + 6 + "开"）
        private const float SegColW = 300f;      // 分段单选（三段短标签）
        private const float DualStepColW = 310f; // 两个步进器（各约 152：− 值 +）
        private const float ValueColW = 64f;     // 滑块当前值列（容得下 "5000"；56 时 "1000" 已贴边）
        private const float NoteColW = 76f;      // 只读行的"（只读）"标记
        private const float BadgeColW = 96f;     // 短角标列（1004·SHIFTMEAL ≈ 78px）
        private const float CaretW = 16f;
        private const float MinPanelW = 420f;    // 降级阈值（skill 07 册：低于此不画控件）
        private const float MinPanelH = 240f;

        // ── 分节几何：与 RUIF `Atoms.SectionFrame` 的绘制契约对齐 ──────────────────
        /// <summary>
        /// `SectionFrame` 把标题画在 <c>outer.y</c> **上方 8px**（fieldset 式断口），
        /// 内容区则从 <c>outer.y + FrameInset</c> 起。故分节顶部必须预留
        /// <see cref="SectionTitleLead"/> 的标题带，否则第一行会顶到标题上
        /// （2026-10-05 实机裁图：标题与首卡仅约 2px 间隙 = 挤兑）。
        /// </summary>
        private const float SectionTitleLead = 10f;

        /// <summary>
        /// `Atoms.SectionFrame` 的内缩量（<c>Layout.Inset(outer, SpaceXs, SpaceXs + 4)</c>
        /// ⇒ 上下各 <c>SpaceXs + 4 = 12</c>）。行必须从 <c>outer.y + FrameInset</c> 起画。
        /// </summary>
        private static float FrameInset => Tokens.SpaceXs + 4f;

        /// <summary>分节顶部预留（标题带 + frame 上内缩）；尾部只需 frame 下内缩。</summary>
        private static float SectionLead => SectionTitleLead + FrameInset;

        // ── UI 会话状态（归调用方；受控静态，见类注释）─────────────────────────
        private static SettingsTab tab = SettingsTab.Archive;
        private static float tabIndicatorX;
        private static Vector2 scroll = Vector2.zero;
        private static readonly HashSet<SettingId> expanded = new HashSet<SettingId>();
        private static readonly HashSet<string> failed = new HashSet<string>();
        private static float cachedContentH = -1f;
        private static float cachedContentW = -1f;
        private static SettingsTab cachedTab = (SettingsTab)(-1);
        private static int expandedStamp = -1;   // 展开集合的版本戳（失效内容高缓存）

        private static readonly Func<float, string> IntFormat = v => v.ToString("F0");
        private static readonly Func<float, string> SilverFormat = v => v.ToString("F0");

        /// <summary>
        /// 设置页入口（由 <c>ChronicleSettings.DoSettingsWindowContents</c> 转发）。
        /// <para>⚠ 方法名**不能**叫 <c>Draw</c>：类内会遮蔽 <c>RUIF.Draw</c> 静态类，
        /// 导致后面每一处 <c>Draw.Box(...)</c> 报 CS0119（"方法在给定上下文中无效"）。</para>
        /// </summary>
        public static void Paint(Rect inRect, ChronicleSettings settings)
        {
            if (settings == null)
            {
                return;
            }
            SettingsText.EnsureFresh(out bool langChanged);
            if (langChanged)
            {
                InvalidatePlan();
            }

            // 空间断言（铁律 4）：不够就画降级态，不硬挤。
            if (inRect.width < MinPanelW || inRect.height < MinPanelH)
            {
                Draw.EllipsisLabel(inRect, SettingsText.T("TooSmall"), Tokens.FontLabel,
                    Tokens.Warn, TextAnchor.MiddleCenter);
                return;
            }

            // ① 页签栏
            // ⚠ CHG-20261005-SETTINGS（§7-8）：可见页集合随 Prefs.DevMode 变化 ⇒
            //   页签下标与 SettingsTab 不再同值，**不能直接 cast**；一律经 VisibleTabs 映射。
            SettingsTab[] tabs = SettingsCatalog.VisibleTabs;
            int cur = IndexOfTab(tabs, tab);
            if (cur < 0)
            {
                // 当前页已不可见（例：刚关掉开发者模式、仍停在"诊断"）⇒ 钳制回首页，
                // 否则会拿着旧下标去取 RowSet，画出与页签不符的内容。
                tab = tabs[0];
                cur = 0;
                scroll = Vector2.zero;
                InvalidatePlan();
            }
            var tabRect = new Rect(inRect.x, inRect.y, inRect.width, LineSmall);
            int sel = cur;
            NavAtoms.TabBar(tabRect, SettingsText.Tabs, ref sel, ref tabIndicatorX);
            if (sel != cur)
            {
                tab = tabs[sel];
                scroll = Vector2.zero;      // 换页回到顶部（避免"新页停在半空"）
                InvalidatePlan();
            }

            // ② 内容区（页签下方整块）
            var body = new Rect(inRect.x, tabRect.yMax + Tokens.SpaceSm, inRect.width,
                Mathf.Max(0f, inRect.yMax - tabRect.yMax - Tokens.SpaceSm));
            if (body.height <= LineSmall)
            {
                return;
            }

            float contentH = ContentHeight(settings, body.width);
            Rect view;
            try
            {
                view = Layout.BeginScroll(body, contentH, ref scroll, true);
                PaintPage(view, settings);
            }
            finally
            {
                // 铁律 3：出栈必须放 finally，否则异常会让整局 GUI 卡死。
                Layout.EndScroll();
            }
        }

        /// <summary>页签在可见集合中的下标（不在集合内返回 −1）。</summary>
        private static int IndexOfTab(SettingsTab[] tabs, SettingsTab t)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == t)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>内容总高（滚动范围）—— 按可用宽缓存，展开态变化才重算。</summary>
        private static float ContentHeight(ChronicleSettings s, float width)
        {
            if (cachedContentH >= 0f
                && Mathf.Abs(cachedContentW - width) < 0.5f
                && cachedTab == tab
                && expandedStamp == expanded.Count)
            {
                return cachedContentH;
            }
            float h = Tokens.PanelPadding;
            IReadOnlyList<SettingDef> rows = SettingsCatalog.RowsOf(tab);
            string section = null;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].SectionKey != section)
                {
                    if (section != null)
                    {
                        h += FrameInset;              // 上一节 frame 的下内缩
                        h += Tokens.BlockGap;
                    }
                    section = rows[i].SectionKey;
                    // ⚠ 前端参数修复：原用 `SectionTitleHeight + Gap = 42` 预留，而 PaintPage 实际
                    //   从 `outer.y + FrameInset` 起画行 ⇒ 每节多算约 30px 幽灵高度（滚到底却已无内容）。
                    //   现改为与绘制严格一致：顶部 SectionLead（标题带 + 上内缩）。
                    h += SectionLead;
                }
                h += RowHeight(rows[i], s);
            }
            if (section != null)
            {
                h += FrameInset;      // 末节 frame 的下内缩
            }
            cachedContentH = h + Tokens.PanelPadding;
            cachedContentW = width;
            cachedTab = tab;
            expandedStamp = expanded.Count;
            return cachedContentH;
        }

        private static void InvalidatePlan()
        {
            cachedContentH = -1f;
        }

        /// <summary>作息策略「按天轮换」的 def 名（稳定 ID，GOV-009；与分段控件读写同源）。</summary>
        private const string RestPolicyRotate = "PC_WorkshopRestPolicy_Rotate";

        /// <summary>
        /// 「新车间作息策略 = 按天轮换」时该行要多占一行警告（GATE-UI 复核 P2-1）。
        /// <para>旧实现（<c>ChronicleSettings.LegacySettingsContents</c>）会在该行下方显示
        /// <c>Automation.RestPolicy.Rotate.Warning</c>；新绘制层曾漏掉这一行，而
        /// <c>Detail.RestPolicy</c> 文案仍承诺「选它后会在下方显示警告」⇒ 文案虚诺 + 披露缺失。
        /// 此处按文案把警告补回（按天轮换会翻转睡眠节律，属既有披露要求）。</para>
        /// </summary>
        private static bool HasRotateWarning(SettingDef def, ChronicleSettings s)
        {
            return def.Id == SettingId.DefaultRestPolicy
                && s != null
                && s.WorkshopDefaultRestPolicy == RestPolicyRotate;
        }

        private static float RowHeight(SettingDef def, ChronicleSettings s)
        {
            float h = CtrlH;
            if (!string.IsNullOrEmpty(def.UseKey))
            {
                h += LineTiny;
            }
            if (IsExpanded(def) && !string.IsNullOrEmpty(def.DetailKey))
            {
                h += LineTiny;
            }
            if (HasRotateWarning(def, s))
            {
                h += LineTiny;
            }
            return h + Tokens.SpaceXs;
        }

        private static bool IsExpanded(SettingDef def)
        {
            return !string.IsNullOrEmpty(def.UseKey) && expanded.Contains(def.Id);
        }

        // ═══════════════════════════ 绘制 ═══════════════════════════

        private static void PaintPage(Rect view, ChronicleSettings s)
        {
            float y = view.y;
            float x = view.x;
            float w = view.width;

            IReadOnlyList<SettingDef> rows = SettingsCatalog.RowsOf(tab);
            string section = null;
            float sectionOuterY = y;      // frame 的 outer.y（= 首行 y − FrameInset）
            int sectionStart = 0;

            for (int i = 0; i <= rows.Count; i++)
            {
                bool boundary = i == rows.Count || rows[i].SectionKey != section;
                if (boundary && section != null)
                {
                    // frame 高度 = 行总高 + 上下内缩（SectionFrame 自己画标题、边框与断口）
                    PaintSection(new Rect(x, sectionOuterY, w, (y - sectionOuterY) + FrameInset),
                        section, rows, sectionStart, i, s);
                    y += FrameInset;      // 与 ContentHeight 对称：尾部下内缩
                }
                if (boundary && i < rows.Count)
                {
                    if (section != null)
                    {
                        y += Tokens.BlockGap;
                    }
                    section = rows[i].SectionKey;
                    sectionStart = i;
                    // 标题带（SectionTitleLead）留给 SectionFrame 画标题，再下移 frame 上内缩到首行
                    sectionOuterY = y + SectionTitleLead;
                    y = sectionOuterY + FrameInset;
                }
                if (i < rows.Count)
                {
                    y += RowHeight(rows[i], s);
                }
            }
        }

        private static void PaintSection(Rect outer, string sectionKey, IReadOnlyList<SettingDef> rows,
            int from, int to, ChronicleSettings s)
        {
            // SectionFrame 内部用 Text.CalcSize 测量、却用 Tiny 绘制 ⇒ 调用前统一字档
            // （skill 01 册 §4.1：不统一会按 Small 测量 ⇒ 断口偏宽约 20%）。
            Rect inner;
            using (Draw.Font(Tokens.FontLabel))
            {
                inner = Atoms.SectionFrame(outer, SettingsText.T(sectionKey));
            }

            float y = inner.y;
            for (int i = from; i < to; i++)
            {
                float h = RowHeight(rows[i], s);
                var row = new Rect(inner.x, y, inner.width, h - Tokens.SpaceXs);
                try
                {
                    PaintRow(row, rows[i], s);
                }
                catch (Exception e)
                {
                    // 逐件隔离：单行异常不得逃出 DoWindowContents（否则 GUI 卡死）。
                    if (failed.Add(rows[i].TitleKey))
                    {
                        Log.ErrorOnce("[PersonalChronicle] 设置行「" + rows[i].TitleKey + "」绘制异常，已隔离：\n" + e,
                            rows[i].TitleKey.GetHashCode());
                    }
                    Draw.Border(row, Tokens.Dead);
                    Draw.EllipsisLabel(row, SettingsText.T("RowFailed"), Tokens.FontLabel,
                        Tokens.Dead, TextAnchor.MiddleLeft);
                }
                y += h;
            }
        }

        private static void PaintRow(Rect row, SettingDef def, ChronicleSettings s)
        {
            bool hover = Mouse.IsOver(row);

            // 层次 1：行底（悬停态用 CardHover —— 六态里的 hover 表达）
            Draw.Box(row, hover ? Tokens.CardHover : Tokens.Card);

            // 层次 2：标题行（左起：展开箭头 → 标题 → 角标 → 控件）
            var head = new Rect(row.x, row.y, row.width, CtrlH);
            float cursor = head.x + Tokens.CardPadX;

            // ⚠ GATE-UI 复核 P3-a：判据必须是 **DetailKey**（展开后真正多画的那一行）。
            //   旧判据 `DetailKey || UseKey` 会让 9 个只有「用途」没有「详情」的行也画出箭头，
            //   点了箭头却什么都不展开（死箭头）。
            bool hasDetail = !string.IsNullOrEmpty(def.DetailKey);
            if (hasDetail)
            {
                var caret = new Rect(cursor, head.y + (head.height - LineTiny) * 0.5f, CaretW, LineTiny);
                StatusAtoms.Caret(caret, expanded.Contains(def.Id), Tokens.Muted);
                if (Widgets.ButtonInvisible(caret))
                {
                    if (!expanded.Remove(def.Id))
                    {
                        expanded.Add(def.Id);
                    }
                    InvalidatePlan();
                }
                cursor += CaretW;
            }

            float ctrlCol = CtrlWidthOf(def);
            bool sliderRow = def.Kind == SettingKind.IntSlider || def.Kind == SettingKind.FloatSlider;
            // rightUsed = 控件本身 + 滑块行的值列（角标必须排在它左边，否则会与值列重叠）
            float rightUsed = ctrlCol + (sliderRow ? ValueColW + Tokens.SpaceSm : 0f);
            float rightBudget = rightUsed + BadgeColW + Tokens.Gap * 2f;
            // ⚠ 前端参数修复：控件列此前直接贴到行右边缘（xMax）⇒ 右侧 0 内边距，
            //   与左侧 CardPadX(12) 不对称，实机裁图可见分段控件压到卡片描边上。
            //   统一以 `right = 行右缘 − CardPadX` 作为右侧锚点，标题/角标/控件共用。
            float right = head.xMax - Tokens.CardPadX;
            float titleW = Mathf.Max(40f, right - cursor - rightBudget);

            var titleRect = new Rect(cursor, head.y, titleW, head.height);
            Draw.EllipsisLabel(titleRect, SettingsText.T(def.TitleKey), Tokens.FontBody,
                Tokens.Text, TextAnchor.MiddleLeft);

            if (!string.IsNullOrEmpty(def.Badge))
            {
                var badgeRect = new Rect(right - rightUsed - Tokens.Gap - BadgeColW, head.y,
                    BadgeColW, head.height);
                Draw.EllipsisLabel(badgeRect, def.Badge, Tokens.FontLabel, ToneColor(def.Tone),
                    TextAnchor.MiddleRight);
                if (!string.IsNullOrEmpty(def.BadgeFull))
                {
                    // 列表只放短形式（590px 下完整编号会把标题挤到截断）⇒ 完整来源走悬停。
                    TooltipHandler.TipRegion(badgeRect, new TipSignal(def.BadgeFull,
                        def.BadgeFull.GetHashCode()));
                }
            }

            // 层次 3：控件（右对齐固定列；控件宽是设计常量，不是文本行高）
            var ctrlRect = new Rect(right - ctrlCol, head.y + (head.height - CtrlH) * 0.5f,
                ctrlCol, CtrlH);
            PaintControl(ctrlRect, def, s);

            // 用途一行（信息简化：默认就能看到"这项干什么"）
            float y = head.yMax;
            if (!string.IsNullOrEmpty(def.UseKey))
            {
                var useRect = new Rect(row.x + Tokens.CardPadX, y, row.width - Tokens.CardPadX * 2f, LineTiny);
                string use = SettingsText.T(def.UseKey);
                Draw.EllipsisLabel(useRect, use, Tokens.FontLabel, Tokens.Dim, TextAnchor.MiddleLeft);
                // ⚠ GATE-UI 复核 P3-b：用途行在 590px 下必然截断（尤其长句 Note 行），
                //   此前无悬停全文 ⇒ 玩家看不到完整说明。与详情行一致补上。
                TooltipHandler.TipRegion(useRect, new TipSignal(use, def.TitleKey.GetHashCode() + 17));
                y += LineTiny;
            }

            // 展开详情（信息完整：默认值理由 / 影响与代价），截断可见 + 悬停给全文
            if (IsExpanded(def) && !string.IsNullOrEmpty(def.DetailKey))
            {
                var detailRect = new Rect(row.x + Tokens.CardPadX, y,
                    row.width - Tokens.CardPadX * 2f, LineTiny);
                string detail = SettingsText.T(def.DetailKey);
                Draw.EllipsisLabel(detailRect, detail, Tokens.FontLabel, Tokens.Muted,
                    TextAnchor.MiddleLeft);
                TooltipHandler.TipRegion(detailRect, new TipSignal(detail, def.TitleKey.GetHashCode() + 7));
                y += LineTiny;      // 占位推进（行高已由 RowHeight 计入，否则会与警告行重叠）
            }

            // 作息策略「按天轮换」的警告（P2-1）：Detail 文案承诺"下方显示警告"，此处兑现。
            if (HasRotateWarning(def, s))
            {
                var warnRect = new Rect(row.x + Tokens.CardPadX, y,
                    row.width - Tokens.CardPadX * 2f, LineTiny);
                string warn = SettingsText.Raw("PersonalChronicle.Automation.RestPolicy.Rotate.Warning");
                Draw.EllipsisLabel(warnRect, warn, Tokens.FontLabel, Tokens.Warn, TextAnchor.MiddleLeft);
                TooltipHandler.TipRegion(warnRect,
                    new TipSignal(warn, def.TitleKey.GetHashCode() + 13));
            }

            // 1px 分隔线放行末（层次更清楚，且不会盖住文字 —— 铁律 3）
            Draw.LineH(row.x, row.yMax - 1f, row.width, Tokens.BorderHair);
        }

        /// <summary>控件分派：**读值 → 画控件 → 命中则写值**（绘制后立刻命中检测，见第一原则）。</summary>
        private static void PaintControl(Rect rect, SettingDef def, ChronicleSettings s)
        {
            switch (def.Kind)
            {
                case SettingKind.Toggle:
                {
                    bool on = GetToggle(def.Id, s);
                    if (InputAtoms.ToggleSwitch(rect, ref on))
                    {
                        SetToggle(def.Id, s, on);
                    }
                    break;
                }
                case SettingKind.IntSlider:
                case SettingKind.FloatSlider:
                {
                    // ⚠ 不用 InputAtoms.SliderWithValue：它把当前值画在 rect **上方 18px**
                    //   （源码 362-378 行：`sliderRect.y - 18f`），在本行的布局里会顶到标题行，
                    //   玩家看到的是"只有 min/max 两个数字、没有当前值"（实机截图问题）。
                    //   这里改用同一底层控件（原版 Widgets.HorizontalSlider，RUIF 也是这么调的），
                    //   当前值自画在滑块**左侧**的独立列 ⇒ 不越行、恒定可见。
                    float v = GetNumber(def.Id, s);
                    // ⚠ D1 修复（CHG-20261005-SETTINGS）：子项受父开关门控。
                    //   旧实现有 `if (AutoArchiveEnabled) { … }`，新绘制层曾丢失该门控 ⇒
                    //   关掉「自动归档」后两个阈值滑块仍可拖（改的是不生效的值，属于误导）。
                    if (!RowEnabled(def.Id, s))
                    {
                        PaintDisabledSlider(rect, def, v);
                        break;
                    }
                    float before = v;
                    // ⚠ 前端参数修复（2026-10-05 实机裁图）：**不能**用 5 参重载
                    //   `HorizontalSlider(rect, ref v, FloatRange, label, roundTo)` ——
                    //   反编译源码 `Verse/Widgets.cs:1938-1942` 显示它把 `range.TrueMin/TrueMax`
                    //   **强制**当作左右对齐标签传下去（`trueMin.ToString(), trueMax.ToString()`），
                    //   而且带标签时会 `rect.y += Mathf.Round((rect.height - 10f) / 2f)` 把轨道压到列底。
                    //   实机后果：轨道上方多出 min/max 两个数字，与本行左侧的「当前值列」在相邻两行上
                    //   挤成 `v1 · 200 / 72` 三数并列（视觉挤兑，且 72 易被误读成当前值）。
                    //   改调 9 参重载：左右标签显式传 null（只画轨道），
                    //   `middleAlignment = true` 让轨道在控件列内**垂直居中**。
                    v = Widgets.HorizontalSlider(rect, v, MinOf(def.Id), MaxOf(def.Id),
                        true, null, null, null, 1f);
                    // 范围信息不丢：改从悬停拿（RangeTip 按行缓存 ⇒ 绘制期零分配）
                    TooltipHandler.TipRegion(rect, new TipSignal(
                        SettingsText.RangeTip(def.TitleKey, MinOf(def.Id), MaxOf(def.Id)),
                        def.TitleKey.GetHashCode() + 19));
                    var valueRect = new Rect(rect.x - ValueColW - Tokens.SpaceSm, rect.y,
                        ValueColW, rect.height);
                    Draw.EllipsisLabel(valueRect,
                        def.Kind == SettingKind.IntSlider ? IntFormat(v) : SilverFormat(v),
                        Tokens.FontLabel, Tokens.Text, TextAnchor.MiddleRight);
                    if (Mathf.Abs(v - before) > 0.0001f)
                    {
                        SetNumber(def.Id, s, Mathf.Round(v));
                    }
                    break;
                }
                // ⚠ CHG-20261005-MEALBYSHIFT：原 `case SettingKind.DualIntStep`
                //   （一行两段的"起 / 止"小时步进器，专供全局三餐窗口）已删除 ——
                //   全局三餐设置整批移除后，目录里已无任何 DualIntStep 行。
                //   `SettingKind.DualIntStep` 枚举值本身保留（无副作用；删枚举会牵动
                //   目录/校验两处，收益为零）。
                case SettingKind.Segmented:
                {
                    string[] options = SettingsText.Options(def);
                    if (options == null || options.Length == 0)
                    {
                        break;
                    }
                    int idx = SegmentedIndexOf(def.Id, s);
                    if (InputAtoms.Segmented(rect, options, ref idx))
                    {
                        SetSegmentedIndex(def.Id, s, idx);
                    }
                    break;
                }
                case SettingKind.Action:
                {
                    if (def.Id == SettingId.HiddenWorkTab)
                    {
                        PaintHiddenWorkTabAction(rect);
                    }
                    // CHG-20261012-WORKGATE · W3：白名单可视化窗口 + 诊断落 Player.log（用户点名要的"快速检测"按钮）。
                    else if (def.Id == SettingId.WorkGateWhitelist)
                    {
                        if (Widgets.ButtonText(rect, SettingsText.T("Use.WorkGateWhitelist"), true, false, true))
                            Find.WindowStack.Add(new Dialog_WorkGateWhitelist());
                    }
                    else if (def.Id == SettingId.WorkGateLog)
                    {
                        if (Widgets.ButtonText(rect, SettingsText.T("Use.WorkGateLog"), true, false, true))
                            WorkGateDiagnostics.DumpToLog();
                    }
                    break;
                }
                default:
                {
                    // Note：无控件（只读说明行）。
                    Draw.EllipsisLabel(rect, SettingsText.T("ReadOnly"), Tokens.FontLabel,
                        Tokens.Dim, TextAnchor.MiddleRight);
                    break;
                }
            }
        }

        /// <summary>
        /// 行是否可交互（父控关系）。目前唯一一处：「低价值档案自动归档」关闭 ⇒
        /// 价值阈值 / 保留天数两行只读（D1：旧实现有该门控，新绘制层曾丢失）。
        /// </summary>
        private static bool RowEnabled(SettingId id, ChronicleSettings s)
        {
            if (id == SettingId.AutoArchiveSilver || id == SettingId.AutoArchivePurgeDays)
            {
                return s.AutoArchiveEnabled;
            }
            return true;
        }

        /// <summary>
        /// 门控关闭时的滑块行：**不画真滑块** —— 原版 <c>Widgets.HorizontalSlider</c>
        /// 不认 <c>GUI.enabled</c>，画出来就是能拖的。改为「静态轨 + 降透明度读数 + 悬停说明」，
        /// 语义是"当前不生效"，而不是"控件坏了"（GATE-UI 状态完备）。
        /// </summary>
        private static void PaintDisabledSlider(Rect rect, SettingDef def, float v)
        {
            var track = new Rect(rect.x, rect.y + rect.height * 0.5f - 1f, rect.width, 2f);
            Draw.Box(track, Tokens.BorderHair);
            var valueRect = new Rect(rect.x - ValueColW - Tokens.SpaceSm, rect.y,
                ValueColW, rect.height);
            Draw.EllipsisLabel(valueRect,
                def.Kind == SettingKind.IntSlider ? IntFormat(v) : SilverFormat(v),
                Tokens.FontLabel, Tokens.Dim, TextAnchor.MiddleRight);
            string tip = SettingsText.T("NeedParentOn");
            TooltipHandler.TipRegion(rect, new TipSignal(tip, def.TitleKey.GetHashCode() + 11));
        }

        private static void PaintHiddenWorkTabAction(Rect rect)
        {
            Data.ChronicleGameComponent comp = Current.Game != null
                ? Current.Game.GetComponent<Data.ChronicleGameComponent>()
                : null;
            // ⚠ NODEMOTE-AUDIT（P1）：必须用**并集去重计数** —— 原取 HiddenWorkTabColonists.Count
            // 只数了"玩家手动"那一半，而分账后 PC 自动隐藏才是常态 ⇒ 仅自动隐藏时这里恒为 0，
            // 于是按钮**不渲染**、还显示"无隐藏成员"，逃生舱形同虚设。
            int count = comp != null ? comp.HiddenWorkTabCount : 0;
            if (count <= 0)
            {
                Draw.EllipsisLabel(rect, SettingsText.T("HiddenWorkTabNone"),
                    Tokens.FontLabel, Tokens.Dim, TextAnchor.MiddleRight);
                return;
            }
            string label = string.Format(
                SettingsText.Raw("PersonalChronicle.WorkTab.Settings.ClearHidden"), count);
            if (Widgets.ButtonText(rect, label, true, false, true))
            {
                int removed = comp.ClearHiddenFromWorkTab();
                Messages.Message(string.Format(
                    SettingsText.Raw("PersonalChronicle.WorkTab.Settings.HiddenCleared"), removed),
                    MessageTypeDefOf.NeutralEvent, false);
            }
        }

        // ═══════════════════════════ 值读写分派 ═══════════════════════════

        private static bool GetToggle(SettingId id, ChronicleSettings s)
        {
            switch (id)
            {
                case SettingId.Recording: return s.EnableRecording;
                case SettingId.PawnNeedSpeech: return s.EnablePawnNeedSpeech;
                case SettingId.EconomicInstitution: return s.EnablePawnEconomicInstitution;
                case SettingId.AutoArchiveEnabled: return s.AutoArchiveEnabled;
                // CHG-20261005-SETTINGS-REMOVE2：SettingId.OffShiftCancel 分支已删除
                //（「离班停工」默认化 ⇒ 恒开，不再有开关行；见 EXC-2026-026）。
                // CHG-20261005-SETTINGS（§7-1）：SettingId.AutoRestoreOnShift 分支已删除
                //（该项默认化 ⇒ 恒开，不再有开关行）。
                case SettingId.IdleSplitEnabled: return s.IdleSplitEnabled;
                case SettingId.WorkContractGate: return s.EnableWorkContractGate;
                case SettingId.AutoDetectWorkshopType: return s.AutoDetectWorkshopType;
                case SettingId.ReplayTimetables: return s.ReplayTimetablesOnDayChange;
                // CHG-20261005-MEALBYSHIFT：SettingId.MealEnabled 分支已删除（该设置项整项移除）。
                case SettingId.PerfProbe: return s.EnablePerfProbe;
                case SettingId.DebugLiveCount: return s.DebugLiveCount;
                default: return s.EnableStartupSelfTest;
            }
        }

        private static void SetToggle(SettingId id, ChronicleSettings s, bool on)
        {
            switch (id)
            {
                case SettingId.Recording: s.EnableRecording = on; break;
                case SettingId.PawnNeedSpeech: s.EnablePawnNeedSpeech = on; break;
                case SettingId.EconomicInstitution:
                    s.EnablePawnEconomicInstitution = on;
                    Economy.PawnEconomy.GameComponent_PawnEconomy.Current?.ApplyInstitutionModeSetting(on);
                    break;
                case SettingId.AutoArchiveEnabled: s.AutoArchiveEnabled = on; break;
                // CHG-20261005-SETTINGS-REMOVE2：SettingId.OffShiftCancel 分支已删除。
                // CHG-20261005-SETTINGS（§7-1）：SettingId.AutoRestoreOnShift 分支已删除。
                case SettingId.IdleSplitEnabled: s.IdleSplitEnabled = on; break;
                case SettingId.WorkContractGate:
                    s.EnableWorkContractGate = on;
                    // 一变就重建作业表（否则最长要等 250 tick 才生效）。
                    PersonalChronicle.WorkAuthorityInvalidator.InvalidateAll();
                    break;
                case SettingId.AutoDetectWorkshopType: s.AutoDetectWorkshopType = on; break;
                case SettingId.ReplayTimetables: s.ReplayTimetablesOnDayChange = on; break;
                // CHG-20261005-MEALBYSHIFT：SettingId.MealEnabled 分支已删除（该设置项整项移除）。
                case SettingId.PerfProbe: s.EnablePerfProbe = on; break;
                case SettingId.DebugLiveCount: s.DebugLiveCount = on; break;
                default: s.EnableStartupSelfTest = on; break;
            }
        }

        private static float GetNumber(SettingId id, ChronicleSettings s)
        {
            switch (id)
            {
                case SettingId.MaxEventsPerPawn: return s.MaxEventsPerPawn;
                case SettingId.AutoArchiveSilver: return s.AutoArchiveSilverThreshold;
                default: return s.AutoArchivePurgeDays;
            }
        }

        private static void SetNumber(SettingId id, ChronicleSettings s, float v)
        {
            switch (id)
            {
                case SettingId.MaxEventsPerPawn: s.MaxEventsPerPawn = (int)v; break;
                case SettingId.AutoArchiveSilver: s.AutoArchiveSilverThreshold = v; break;
                default: s.AutoArchivePurgeDays = (int)v; break;
            }
        }

        // ── 取值范围（集中在此，避免绘制路径出现魔法数字）────
        // ⚠ CHG-20261005-SETTINGS（§7-4）：事件上限的下限由 10 上调到 72。
        //   依据 `Application/ArchiveService.Battle.Helpers.cs:40-42`：留存上限按重要度分档 ——
        //   routine 类 = max(24, max/3)，其余 = max。于是：
        //     ① max < 24  ⇒ routine 预算(24) **反超**总上限，语义倒挂（既有缺陷 D6）；
        //     ② 24 ≤ max < 72 ⇒ routine 被固定压在 24，"routine 占三分之一"的声明失真；
        //     ③ max ≥ 72  ⇒ max/3 ≥ 24，声明比例才真正成立。
        //   72 = 3×24 即这一临界点，故取作下限（默认值 200 不变）。
        private const float EventsPerPawnMin = 72f;
        private const float EventsPerPawnMax = 1000f;
        private const float SilverMin = 0f;
        private const float SilverMax = 5000f;
        private const float PurgeDaysMin = 7f;
        private const float PurgeDaysMax = 365f;

        private static float MinOf(SettingId id)
        {
            switch (id)
            {
                case SettingId.MaxEventsPerPawn: return EventsPerPawnMin;
                case SettingId.AutoArchiveSilver: return SilverMin;
                default: return PurgeDaysMin;
            }
        }

        private static float MaxOf(SettingId id)
        {
            switch (id)
            {
                case SettingId.MaxEventsPerPawn: return EventsPerPawnMax;
                case SettingId.AutoArchiveSilver: return SilverMax;
                default: return PurgeDaysMax;
            }
        }

        // ⚠ CHG-20261005-MEALBYSHIFT：原 `MealIndexOf`（把 SettingId 映射到三餐下标）已删除
        //   —— 它唯一服务的是全局三餐那三个 `DualIntStep` 行，那些行已整批移除。

        private static int SegmentedIndexOf(SettingId id, ChronicleSettings s)
        {
            // CHG-20261005-MEALBYSHIFT：原 SettingId.MealPriority 分支已删除（设置项整项移除）。
            if (id == SettingId.DefaultRestPolicy)
            {
                if (s.WorkshopDefaultRestPolicy == "PC_WorkshopRestPolicy_Natural") return 1;
                if (s.WorkshopDefaultRestPolicy == "PC_WorkshopRestPolicy_Rotate") return 2;
                return 0;
            }
            // Theme
            int idx = Array.IndexOf(UITheme.ThemeIds, s.ThemeId);
            return idx < 0 ? 0 : idx;
        }

        private static void SetSegmentedIndex(SettingId id, ChronicleSettings s, int idx)
        {
            // CHG-20261005-MEALBYSHIFT：原 SettingId.MealPriority 分支已删除（设置项整项移除）。
            if (id == SettingId.DefaultRestPolicy)
            {
                s.WorkshopDefaultRestPolicy = idx == 1
                    ? "PC_WorkshopRestPolicy_Natural"
                    : (idx == 2 ? "PC_WorkshopRestPolicy_Rotate" : "PC_WorkshopRestPolicy_Fixed");
                return;
            }
            if (idx >= 0 && idx < UITheme.ThemeIds.Length && UITheme.ThemeIds[idx] != s.ThemeId)
            {
                s.ThemeId = UITheme.ThemeIds[idx];
                UITheme.Apply(s.ThemeId);
            }
        }

        /// <summary>
        /// 控件列宽（按形态选择；每个值都由"该控件内部几何"推出，不是随手数字）：
        /// <list type="bullet">
        /// <item>Toggle = 78：<c>ToggleSwitch</c> 轨道 36 + 标签间距 6 + "开"（Tiny 2 字 ≈ 24）；</item>
        /// <item>DualIntStep = 310：两个 <c>Stepper</c> 各约 152（− 26 + 值 + + 26）
        /// —— 半宽（66）会让数值只剩 14px 可画，实测读数看不清（2026-10-05 截图问题）；</item>
        /// <item>Segmented = 300：三段短标签（最长"自然休息"≈ 56 + 内边距）。</item>
        /// </list>
        /// </summary>
        private static float CtrlWidthOf(SettingDef def)
        {
            switch (def.Kind)
            {
                case SettingKind.Toggle: return ToggleColW;
                case SettingKind.DualIntStep: return DualStepColW;
                case SettingKind.Segmented: return SegColW;
                case SettingKind.Note: return NoteColW;
                default: return CtrlColW;
            }
        }

        private static Color ToneColor(SettingTone tone)
        {
            switch (tone)
            {
                case SettingTone.New: return Tokens.Alive;
                case SettingTone.Fix: return Tokens.Info;
                case SettingTone.Experimental: return Tokens.Warn;
                default: return Tokens.Dim;
            }
        }
    }
}
