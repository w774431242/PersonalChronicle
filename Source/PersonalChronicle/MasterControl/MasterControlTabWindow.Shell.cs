using System.Collections.Generic;
using System.Text;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using RUIF;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl
{
    /// <summary>
    /// Partial of <see cref="MasterControlTabWindow"/> ——外壳（页头 / 视图分发）
    /// 与跨视图共用的绘制原语。所有绘制只消费 <see cref="UITheme"/> 令牌与
    /// <see cref="UIComponents"/> 组件，不出现裸色值（UI 规范）。
    /// </summary>
    public sealed partial class MasterControlTabWindow
    {
        // ---- 页头 ----

        /// <summary>
        /// 页头 = 标题 + 副标题（随视图变化）+ **嵌入式视图 tab 条** + 右侧导航按钮组。
        /// 总控中心是「路由入口」，导航入口全部收在页头，不再占用侧栏（内容区吃满宽度）。
        ///
        /// <para><b>2026-09-16（用户需求「这一排 tab 的空间占位太大，建议嵌入式」）</b>：人员中心的
        /// 4 枚视图 tab 由内容区一行**改为嵌入本页头**——位置在标题右侧、按钮组左侧，与按钮组垂直居中。
        /// 页头总高（<see cref="UITheme.HeaderHeight"/>）**不变**（占的是原本就空着的水平带），
        /// 内容区因此再省一行。预算与降级阶梯见 <see cref="DrawHeaderPopulationViewTabs"/>。</para>
        /// </summary>
        private void DrawHeader(Rect rect)
        {
            float reserved = DrawHeaderButtons(rect);
            float titleWidth = Mathf.Max(120f, rect.width - reserved - 16f);
            UIComponents.Label(new Rect(rect.x + 2f, rect.y + 6f, titleWidth, 28f),
                Translate("PersonalChronicle.UI.MasterControl.Title"), GameFont.Medium, UITheme.Accent);
            UIComponents.Label(new Rect(rect.x + 3f, rect.y + 32f, titleWidth, 18f),
                HeaderSubtitle(), GameFont.Tiny, UITheme.Dim);
            // 嵌入式视图 tab：必须在标题绘制后调用 —— 预算按标题**实测字宽**推进（UI-009：既不压标题也不压按钮）。
            DrawHeaderPopulationViewTabs(rect, reserved, titleWidth);
            UIComponents.Rule(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), UITheme.Accent);
        }

        /// <summary>副标题：注册表页说明职责，中心页显示当前中心（兼作面包屑）。</summary>
        private string HeaderSubtitle()
        {
            if (view == ViewKind.CenterDetail)
            {
                return cachedDetail != null && !cachedDetail.IsEmpty
                    ? Translate("PersonalChronicle.UI.MasterControl.Detail.Crumb", cachedDetail.Card.Label)
                    : Translate("PersonalChronicle.UI.MasterControl.Detail.Missing");
            }
            return Translate("PersonalChronicle.UI.MasterControl.Subtitle");
        }

        /// <summary>
        /// 页头右侧按钮组（从右往左：主题 / 调度总线 / 返回），返回实际占用的宽度，
        /// 供标题区扣减 —— 避免长标题压到按钮上（UI-009）。
        /// </summary>
        /// <summary>
        /// 页头标题预留宽上限：超出即交给右侧控件（长标题不挤压按钮，LAND-009/UI-009）。
        /// </summary>
        private const float HeaderTitleReserveMax = 320f;

        private float DrawHeaderButtons(Rect rect)
        {
            const float btnHeight = 26f;
            const float btnGap = 8f;
            float y = rect.y + 14f;
            float right = rect.xMax - 10f;

            // 主题（最右，常驻）：与档案馆**共用同一实现** <see cref="UIComponents.ThemeSwitch"/>
            // （分段控件：标签段 + 1px 分隔 + 值段；几何按「可用宽」驱动，自带 D1/D2/D6 降级）。
            // ⛔ 旧实现把「标签 · 主题名」拼成**单串**再整体测量，且 `Clamp(w,120,260)` 固定最小宽 + 右锚 ⇒
            //    ① 两控件紧贴，被读成「主题暗黑哥特」一个词（缺段间分隔）；
            //    ② 可用宽不足时 x 变负、被外层组裁掉 ⇒ 实机「题 · 暗黑哥特」（**文字被硬切，红线**）。
            float themeAreaX = rect.x + Mathf.Min(rect.width * 0.42f, HeaderTitleReserveMax);
            Rect themeArea = new Rect(themeAreaX, y, Mathf.Max(0f, right - themeAreaX), btnHeight);
            float themeUsed;
            UIComponents.ThemeSwitch(themeArea, out themeUsed);
            // 引导锚点：主题控件**实际占位**（守门 U-3 —— `themeArea` 是"可用区"，宽窗下可达上千像素，
            // 拿它当锚点会把整排页头控件一起罩进高亮框）。`ThemeSwitch` 右锚、返回实际用量 `themeUsed`；
            // 未绘制（themeUsed ≈ 0）时上报 0 宽矩形 ⇒ 引导层按"锚点不可用"降级为居中气泡。
            float themeActualWidth = Mathf.Max(0f, themeUsed);
            if (TourAnchorRegistry.Active)
            {
                TourAnchorRegistry.Report(TourAnchorRegistry.AnchorThemeSwitch,
                    ToScreenRect(new Rect(themeArea.xMax - themeActualWidth, themeArea.y,
                        themeActualWidth, themeArea.height)));
            }
            right = themeArea.xMax - themeUsed - btnGap;

            // ── 新手教程（**常驻**；2026-10-06 用户需求「这个界面创建一个新手教程按钮，基于 RUIF」）──
            // 用 RUIF `ButtonAtoms.PrimaryButton`（六态由组件出：默认 / 悬停 / 按下 / 禁用 + 焦点环通路），
            // 几何 = 字宽实测 + 24 内距（与同排「返回」同式）；可用宽不足时**整件跳过**而非硬挤
            // （与 `ButtonAtoms` 的 D6「不足最小可用宽度即省略该件」同规，绝不画到容器外）。
            // 位置：主题左侧 ⇒ 自右向左的顺序为 主题 / 新手教程 / 返回 / 测试数据。
            string guideLabel = Translate("PersonalChronicle.UI.Guide.Button");
            // ⚠ 预算必须与组件**同字号**：RUIF `ButtonAtoms` 内部用 `Tokens.FontLabel`（Tiny）绘制标签，
            //   故这里也按 Tiny 实测；再加 `ButtonAtoms.FullWidthPad`（32f = D0「完整标签」冻结阈值）
            //   ⇒ 恒走 D0 完整档，不会落进 D1（那样组件会**另挂一条 label tooltip**，与本处 ButtonTip 冗余；
            //   守门 P3-1 即此）。同排「返回」用 Small 属原版按钮族，字号差为既有观感差异（已登记）。
            //   ⚠ 32f 的出处 = RUIF 方案 §5.6 的 D0 冻结阈值（源码工作副本里的 `ButtonAtoms.FullWidthPad`）；
            //   该常量在**当前已发布的 RUIF.dll 中尚未导出**（引用会 CS0117）⇒ 按值取 32，
            //   待 RUIF 发布后再换回常量引用（届时本注释与值一并替换）。
            float guideWidth = MeasureWidth(guideLabel, Tokens.FontLabel) + 32f;
            if (right - guideWidth >= themeArea.x)
            {
                Rect guideRect = new Rect(right - guideWidth, y, guideWidth, btnHeight);
                // 引导锚点：本按钮 → 屏幕坐标（引导层在独立窗口里读，见 TourAnchorRegistry）
                if (TourAnchorRegistry.Active)
                {
                    TourAnchorRegistry.Report(TourAnchorRegistry.AnchorHeaderGuide,
                        ToScreenRect(guideRect));
                }
                if (ButtonAtoms.PrimaryButton(guideRect, guideLabel))
                {
                    Find.WindowStack.Add(new Window_NewcomerGuide());
                }
                TooltipHandler.TipRegion(guideRect, Translate("PersonalChronicle.UI.Guide.ButtonTip"));
                right = guideRect.x - btnGap;
            }

            // 返回注册表（仅在中心详情页出现）
            if (view != ViewKind.Registry)
            {
                string backLabel = Translate("PersonalChronicle.UI.MasterControl.Header.Back");
                float backWidth = MeasureWidth(backLabel, GameFont.Small) + 24f;
                Rect backRect = new Rect(right - backWidth, y, backWidth, btnHeight);
                if (Widgets.ButtonText(backRect, backLabel))
                {
                    GoRegistry();
                }
                TooltipHandler.TipRegion(backRect, Translate("PersonalChronicle.UI.MasterControl.Header.BackTip"));
                right = backRect.x - btnGap;
            }

            // 开发者「测试数据」按钮（仅 DevMode；自右向左的最后一个）。已合并旧「重随机」：
            // **每次「关→开」都换一批新的随机的人员概览快照**（用户需求「它只有一个固定数据而不是随机数据」）。
            // 实现见 MasterControlTabWindow.PopulationViews.cs（DrawDevToggleButton / NextDevSeed）。
            // ⚠ 2026-09-17：概览视图里**整条 tab 行**（4 枚视图 tab + 本开关）由 ①「人员规模与构成」
            // 面板的标题条托管（用户需求「嵌入面板，节省组件空间」）⇒ 页头**不再重复画**。
            // 判据与视图 tab 条**同一来源**（PopulationViewTabsHostedByScalePanel）⇒ 无 1 帧错位、
            // 也不可能一处画一处不画。
            if (!PopulationViewTabsHostedByScalePanel(rect.width))
            {
                right = DrawDevToggleButton(y, btnHeight, btnGap, right);
            }

            return rect.xMax - right;
        }

        private static float MeasureWidth(string text, GameFont font)
        {
            GameFont prevFont = Text.Font;
            Text.Font = font;
            float width = Text.CalcSize(text).x;
            Text.Font = prevFont;
            return width;
        }

        // ---- 视图分发 ----

        private void DrawContent(Rect rect)
        {
            UIComponents.Panel(rect);
            Rect inner = rect.ContractedBy(UITheme.PanelPadding);
            switch (view)
            {
                case ViewKind.CenterDetail:
                    DrawDetailContent(inner);
                    break;
                default:
                    DrawRegistryContent(inner);
                    break;
            }
        }

        /// <summary>开启滚动视口；调用方必须用 try/finally 配对 <c>EndScrollView</c>。</summary>
        private void BeginContentScroll(Rect inner, float contentHeight, out Rect viewRect)
        {
            viewRect = new Rect(inner.x, inner.y,
                Mathf.Max(1f, inner.width - UITheme.ScrollbarThickness),
                Mathf.Max(inner.height, contentHeight));
            Widgets.BeginScrollView(inner, ref contentScroll, viewRect);
        }

        // ---- 共用绘制原语 ----

        /// <summary>页头尾部留白（标题块与下一区块之间）。</summary>
        private const float PageHeaderTailGap = 12f;

        /// <summary>
        /// 页头区块总高 = 标题 + api 行 + <b>说明提示条</b> + 尾部留白。
        ///
        /// <para><b>现行消费方 = 注册表页 <c>ComputeRegistryHeight</c>（+ <c>DrawPageTitle</c>，
        /// 见 <c>Overview.cs</c>）</b>。<b>中心详情页已不再消费</b>：2026-09-16 布局优化移除了
        /// 内容区页头块（面包屑 + 大标题 + api 行 + 说明条）；同日第二刀把 4 视图 tab 条也**嵌入本页头**
        /// ⇒ 详情页内容区不再有任何 tab 行占位（专用符号 <c>DetailHeaderTabsHeight</c> 恒 0，仅页头
        /// 宽度不足的**兜底档**回落到 28；见 <c>MasterControlTabWindow.Detail.cs</c>）。
        /// 计高与绘制仍须同源（LAND-002）。</para>
        /// </summary>
        internal static float PageHeaderBlockHeight()
        {
            return PageTitleHeight + PageLineHeight + NoticeBoxHeight + PageHeaderTailGap;
        }

        private static float DrawPageTitle(Rect rect, float y, string title, string api, string intro)
        {
            UIComponents.Label(new Rect(rect.x, y, rect.width, 28f), title, GameFont.Medium, UITheme.Text);
            y += PageTitleHeight;
            UIComponents.Label(new Rect(rect.x, y, rect.width, 20f), api, GameFont.Tiny, UITheme.Info);
            y += PageLineHeight;
            // 说明行改「**提示条容器**」（复用既有 <see cref="DrawNoticeBox"/>：3px 强调竖标 + 半透明底 +
            // Small 文本），不再是一行裸 dim 文本（实机：说明与标题挤在一起、无可辨识边界）。
            y = DrawNoticeBox(rect, y, intro);
            return y + PageHeaderTailGap;
        }

        private static float DrawSectionHeader(Rect rect, float y, string title)
        {
            return DrawSectionTitle(rect, y, title);
        }

        /// <summary>
        /// 分区标题（<c>h3.sec</c> 同款：琥珀 <c>▍</c> + 灰色加粗小字）。
        ///
        /// <para><b>注意（LAND-001 第三次实测）</b>：预览最终版有
        /// <c>#sop&gt;h3.sec{display:none}</c>（HTML 558）——①~⑦ 的分区标题在最终布局里
        /// <b>全部不显示</b>，因此制造中心详情页**不再调用本函数**（面板直接排在 pane 栅格）。
        /// 保留实现供注册表页等仍需要分区标题的视图使用；`WorkshopTitleGap` 与 `PlainTitleHeight`
        /// 由该路径消费。</para>
        ///
        /// <para>预览原文：<c>h3.sec{margin:8px 0 9px;font-size:12px;color:var(--dim);
        /// letter-spacing:2px;font-weight:800}</c> + <c>::before{content:"▍";color:var(--amber)}</c>。</para>
        /// </summary>
        private static float DrawPlainTitle(Rect rect, float y, string title)
        {
            return DrawSectionTitle(rect, y, title);
        }

        /// <summary>
        /// 分区标题 —— **按预览 <c>h3.sec</c> 的 CSS 参数 1:1 实现**（LAND-001 实测）：
        /// <code>
        /// h3.sec        { margin:8px 0 -2px; font-size:12px; color:var(--dim); letter-spacing:2px; font-weight:800 }
        /// h3.sec::before{ content:"▍"; color:var(--amber) }
        /// #sop>h3.sec   { grid-column:span 12 }
        /// </code>
        /// ⇒ <b>琥珀色 ▍ 竖标 + 灰色加粗小字</b>，**没有分隔横线**。
        /// （v1 那套"强调色块 + 右延横线"并非预览元素，已废弃。）
        /// </summary>
        private static float DrawSectionTitle(Rect rect, float y, string title)
        {
            const string marker = "▍";
            float markerWidth = MeasureWidth(marker, GameFont.Small) + 4f;
            UIComponents.Label(new Rect(rect.x, y, markerWidth, PlainTitleHeight),
                marker, GameFont.Small, UITheme.Accent);
            UIComponents.Label(new Rect(rect.x + markerWidth, y, Mathf.Max(20f, rect.width - markerWidth),
                    PlainTitleHeight),
                UIComponents.TruncateToWidth(title, Mathf.Max(20f, rect.width - markerWidth), GameFont.Small),
                GameFont.Small, UITheme.Dim);
            return y + PlainTitleHeight;
        }

        /// <summary>标签 + 值的一行（值右对齐，超宽截断）。列表值用 <c> · </c> 拼接。</summary>
        private static float DrawTagLine(Rect rect, float y, string label, List<string> values)
        {
            const float labelWidth = 150f;
            UIComponents.Label(new Rect(rect.x, y, labelWidth, LineRowHeight), label,
                GameFont.Tiny, UITheme.SecondaryText);
            string text = JoinTags(values);
            UIComponents.Label(new Rect(rect.x + labelWidth, y,
                    Mathf.Max(0f, rect.width - labelWidth), LineRowHeight),
                UIComponents.TruncateToWidth(text, Mathf.Max(0f, rect.width - labelWidth), GameFont.Small),
                GameFont.Small, text == EmptyText ? UITheme.Dim : UITheme.Text);
            return y + LineRowHeight;
        }

        /// <summary>空集合占位文案（同一份翻译键，避免各处自造「暂无」）。</summary>
        private static string EmptyText
        {
            get { return Translate("PersonalChronicle.UI.MasterControl.Tags.None"); }
        }

        private static string JoinTags(List<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return EmptyText;
            }
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (string.IsNullOrEmpty(values[i]))
                {
                    continue;
                }
                if (builder.Length > 0)
                {
                    builder.Append(" · ");
                }
                builder.Append(values[i]);
            }
            return builder.Length > 0 ? builder.ToString() : EmptyText;
        }

        /// <summary>提示条（预留 / 未接入 / 页脚说明）：左边框强调 + 半透明底 + 自动换行。</summary>
        private static float DrawNoticeBox(Rect rect, float y, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return y;
            }
            Rect box = new Rect(rect.x, y, rect.width, NoticeBoxHeight);
            UIComponents.TintedBox(box, UITheme.OverlayWhite04);
            Widgets.DrawBoxSolid(new Rect(box.x, box.y, 3f, box.height), UITheme.Info);

            // ⚠ 2026-09-19（审计 §2.5）：**计高是两行、绘制却只有单行**。
            //   `NoticeBoxHeight = 44`（≈ 2 × Small 行高），而原来走的是 `UIComponents.Label`
            //   —— 那个组件**强制 `WordWrap = false`**（它自己的注释写明"预期单行"）
            //   ⇒ 本条文案**只能画一行，超出部分被硬切**，留白的 22px 白空着。
            //   ⇒ 改为**按设计画两行**：显式开 WordWrap（并自行恢复，不影响其他模块），
            //     高度仍用常数 44（**计高侧一字未动** ⇒ 不引入计高/绘制漂移，LAND-002）。
            //   ⇒ 若两行仍装不下，**挂悬停给全文**（截断补偿，审计 §3.2）：宁可给出口，不可硬切。
            Rect textRect = new Rect(box.x + 12f, box.y + 2f, box.width - 20f, box.height - 4f);
            bool prevWrap = Text.WordWrap;
            Text.WordWrap = true;
            try
            {
                Widgets.Label(textRect, text);
            }
            finally
            {
                Text.WordWrap = prevWrap;
            }
            if (Text.CalcHeight(text, textRect.width) > textRect.height)
            {
                TooltipHandler.TipRegion(box, text);
            }
            return y + NoticeBoxHeight;
        }
    }
}
