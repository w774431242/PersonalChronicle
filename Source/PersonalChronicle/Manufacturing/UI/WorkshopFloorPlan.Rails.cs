using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>
    /// Partial of <see cref="WorkshopFloorPlan"/> —— **两侧轨道**（左「原料库」/ 右「成品库」）。
    ///
    /// <para>对齐预览：`#rail-left` / `#rail-right` 是 <c>.siderail</c>——
    /// <b>absolute 覆盖在平面图之上</b>（<c>width:126px; top/bottom:8px</c>），不是并排占位。
    /// 所以这里也画成覆盖层：宽 <see cref="RailWidthDefault"/>（窄视口按比例收窄），上下内缩 8px。</para>
    ///
    /// <para>结构与预览 <c>buildRails()</c> 同源：左 = 页签 → 标题 → 收货月台/IQC 小框 → 分区头
    /// → 原料行（ABC 角标 + 名称 + 水位条 + 数值）；右 = 标题 → 在库件数 → Top 成品
    /// → 品质分布 → 打包/发货小框。</para>
    ///
    /// <para><b>2026-10-06 布局优化（用户截图：文案被硬切 / 空态无解释）</b>：
    /// ① 行高与列宽**一律由字体档派生**（原写死 15/16/17/22，全部小于 Tiny 真实行高 18 ⇒ 文字被挤压；
    /// 且宽轨升档后行高不变 ⇒ 行重叠）；② 默认宽 126 → <see cref="RailWidthDefault"/>（Tiny 档下
    /// 中文混排文案放不下是硬切根因，画布让位 `RailGutter` 由本常量派生）；③ 全部截断走
    /// <see cref="UIComponents.TruncateToWidth(string,float,GameFont,Rect)"/>（**结构上不可能只截断不补偿**）；
    /// ④ 补齐预览缺失项：`rsechead` 分区头、`rabc` 需求分档角标、空态说明、列表溢出提示。</para>
    /// </summary>
    internal static partial class WorkshopFloorPlan
    {
        // ================== 尺寸 ==================

        /// <summary>
        /// 轨道默认宽（预览 <c>.siderail{width:126px}</c>）。
        ///
        /// <para>⚠ 2026-10-06：126 是预览的**演示值**（预览字号是 8–9.5px 连续缩放 `--rfs`），
        /// 而引擎只有三档字号、最窄档 Tiny 的中文混排文案在 126−14 内放不下 ⇒ 实机出现
        /// 「STOCK 原料 · 全地图库存」被硬切。默认值抬到 172（Tiny→Small 的档位阈值 126×1.35 之上），
        /// 并让画布让位 <c>RailGutter</c> 由本常量派生（见 MasterControlTabWindow.AppShell）。</para>
        /// </summary>
        internal const float RailWidthDefault = 172f;

        /// <summary>可拉伸范围（预览 `railW[side] = clamp(w, 126, 360)`）。</summary>
        private const float RailWidthMin = 126f;
        private const float RailWidthMax = 360f;

        /// <summary>预览 <c>--rfs</c> 的参考宽：列宽（名称 42 / 数值 34）按 <c>轨道宽 / 126</c> 缩放。</summary>
        private const float RailRefWidth = 126f;

        /// <summary>轨道内缩（预览 <c>top/bottom/left/right: 8px</c>）。</summary>
        private const float RailInset = 8f;

        private const float RailPadX = 7f;
        private const float RailPadY = 8f;
        private const float RailGap = 4f;
        /// <summary>预览 <c>.rbar{height:8px}</c>。</summary>
        private const float RailBarHeight = 8f;
        /// <summary>预览 <c>.rmn{width:42px}</c>——物料名窄列（超出省略 + 悬停显全文）。</summary>
        private const float RailNameWidth = 42f;
        /// <summary>预览 <c>.rv{min-width:34px}</c>——数值右对齐列。</summary>
        private const float RailValueWidth = 34f;
        /// <summary>列表行的最小水位条宽（低于此值从名称列让位，D3 降级）。</summary>
        private const float RailMinBarWidth = 12f;

        /// <summary>视口窄于此时不画轨道（否则会盖掉整个平面图；LAND-005 窄档降级）。</summary>
        private const float RailMinViewportWidth = 420f;

        /// <summary>左轨页签是否停在「设备库」（预览 `#rail-left .rtabs`；会话内）。</summary>
        private static bool leftRailShowsDevices;

        /// <summary>
        /// 会话内轨道宽（预览 `railW.left/right`；**UI 瞬时态**，不入存档，GATE-UI）。
        /// 与预览一致：不是存档内容，重开窗口回默认。
        /// </summary>
        private static float railWidthLeft = RailWidthDefault;
        private static float railWidthRight = RailWidthDefault;

        /// <summary>正在拖的把手（0 无 / 1 左 / 2 右）。</summary>
        private static int railDragSide;

        /// <summary>轨道内字体档（预览 `--rfs` → 引擎三档，LAND-007）。每帧按当前宽算一次。</summary>
        private static GameFont RailFont = GameFont.Tiny;

        // ================== 度量（一律由字体档派生 · 禁手写行高字面量）==================

        /// <summary>
        /// 轨道内单行文本高（Tiny=18 / Small=22 / Medium=30，取引擎字体度量）。
        ///
        /// <para>⚠ 2026-10-06：原实现把行高写死成 15 / 16 / 17 / 22 四个字面量，**全部小于 Tiny 的
        /// 真实行高 18** ⇒ 文字被垂直挤压；轨道拉宽后字号档升到 Small/Medium，行高仍不变 ⇒ 行重叠。
        /// 现在一律走本方法：字号档变 ⇒ 行高变（铁律「尺寸取自框架度量」）。</para>
        /// </summary>
        private static float RailLine()
        {
            return Text.LineHeightOf(RailFont);
        }

        /// <summary>列表行距（预览 <c>.rmat</c> 行 + 2px 呼吸）。</summary>
        private static float RailRowPitch()
        {
            return RailLine() + 2f;
        }

        /// <summary>预览 <c>.rdock{padding:3px 4px}</c>：单行 + 上下各 3px。</summary>
        private static float RailChipHeight()
        {
            return RailLine() + 6f;
        }

        /// <summary>预览 <c>--rfs = 轨道宽 / 126</c>：列宽按同比例缩放（夹在 1–1.8 之间）。</summary>
        private static float RailScale(float railWidth)
        {
            return Mathf.Clamp(railWidth / RailRefWidth, 1f, 1.8f);
        }

        /// <summary>宽度 → 字体档（预览 `--rfs` 连续缩放；引擎只有三档，按比例折算）。</summary>
        private static GameFont RailFontOf(float width)
        {
            float ratio = width / RailRefWidth;
            if (ratio >= 1.9f)
            {
                return GameFont.Medium;
            }
            return ratio >= 1.35f ? GameFont.Small : GameFont.Tiny;   // 默认档（最窄时）
        }

        /// <summary>
        /// **只画两侧轨道**（原料库 / 成品库），不画任何平面几何。
        ///
        /// <para>2026-09-12：② 主画布统一为「自由编排画布」后，查看态仍需左右轨道 —— 轨道贴**外框**、
        /// 与画布**并排**（不覆盖），且其内容（原料条 / 成品件数）与画布坐标系无关，故可独立复用。</para>
        /// </summary>
        internal static void DrawRailsOnly(Rect viewport, WorkshopFloorSnapshot snapshot,
            List<PaletteEntry> devicePalette)
        {
            if (snapshot == null || snapshot.NoMap)
            {
                return;
            }
            if (viewport.width < RailMinViewportWidth || viewport.height < 120f)
            {
                // ⚠ 2026-10-02 审计 P1-7：此前**静默 return** ⇒ 视口不够宽时左轨（含设备库 =
                //   唯一的"从设备库放置设备"入口）整条消失，玩家只看到一块空画布。
                //   现在明确告知"轨道已收窄、放置入口不可用"，不再静默吞掉。
                DrawRailsHiddenNotice(viewport);
                return;
            }
            DrawRails(viewport, snapshot, Palette.Create(), devicePalette);
        }

        /// <summary>轨道被窄视口隐藏时的提示条（GATE-UI：不静默降级）。</summary>
        private static void DrawRailsHiddenNotice(Rect viewport)
        {
            float width = Mathf.Max(40f, viewport.width - 20f);
            Rect rect = new Rect(viewport.x + 10f, viewport.y + 6f, width,
                Text.LineHeightOf(GameFont.Tiny));
            // 4 参重载：被截断即自动挂原文悬停（结构上不可能只截断不补偿）
            UIComponents.Label(rect,
                UIComponents.TruncateToWidth(T("PersonalChronicle.UI.Layout.RailHiddenTip"),
                    width, GameFont.Tiny, rect),
                GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
        }

        /// <summary>
        /// 复位轨道会话态（**关窗 / 换档**，2026-10-02 审计 P1-8）。
        /// 原实现这些静态字段**无任何清理点** ⇒ 换档后残留上一局选中的「设备库」页签与轨道宽度。
        /// </summary>
        internal static void ResetRailSession()
        {
            leftRailShowsDevices = false;
            railWidthLeft = RailWidthDefault;
            railWidthRight = RailWidthDefault;
            railDragSide = 0;
            // 2026-10-06（守门 P4-4）：设备库行的**按下态**一并复位 —— 否则「按下未松手就换档 / 关窗」
            // 会把上一局的按下残留带进下一局（下次进画布即幽灵落点）。
            railPressId = null;
            railPressPos = Vector2.zero;
        }

        private static void DrawRails(Rect viewport, WorkshopFloorSnapshot snapshot, Palette palette,
            List<PaletteEntry> devicePalette)
        {
            if (viewport.width < RailMinViewportWidth || viewport.height < 120f)
            {
                return;
            }
            // 预览：宽可拖（126–360），窄视口按比例夹一次
            float cap = Mathf.Min(RailWidthMax, viewport.width * 0.34f);
            // ⚠ 2026-10-02 审计 P1-11 / 2026-10-06 守门复核（**注释如实化**）：
            //   画布让位 `RailGutter` 现已由轨道**默认宽**派生（172 + 10 = 182），而轨道本身仍可被拖到
            //   360 ⇒ 拖宽超过 182 时轨道会盖住画布左侧（最多 178px），该区域的落点判定与绘制不一致
            //   （画布先绘、轨道后绘，画布侧 `Mouse.IsOver` 仍可能判真）。本处 cap 只保证
            //   「轨道不吃掉视口一半以上」这一条；**彻底闭合需把画布让位改成运行时值**，属独立变更，
            //   已在变更标注登记为已知偏差 —— 不得在此宣称"绝不侵入画布"。
            float usable = Mathf.Max(RailWidthMin, viewport.width * 0.5f);
            cap = Mathf.Min(cap, usable);
            float leftWidth = Mathf.Clamp(railWidthLeft, RailWidthMin, Mathf.Max(RailWidthMin, cap));
            float rightWidth = Mathf.Clamp(railWidthRight, RailWidthMin, Mathf.Max(RailWidthMin, cap));
            float height = viewport.height - RailInset * 2f;
            Rect left = new Rect(viewport.x + RailInset, viewport.y + RailInset, leftWidth, height);
            Rect right = new Rect(viewport.xMax - RailInset - rightWidth, left.y, rightWidth, height);

            int hoveredHandle = HandleRailResize(viewport, left, right);
            RailFont = RailFontOf(Mathf.Max(leftWidth, rightWidth));

            DrawLeftRail(left, snapshot, palette, devicePalette);
            DrawDepotRail(right, snapshot, palette);
            // 把手高亮**必须画在轨道之后**（轨道底色会盖掉先前绘制的内容）。
            DrawRailHandleHint(left, right, hoveredHandle);
        }

        /// <summary>
        /// 内缘把手拖拽（预览 `.rhandle{width:8px;cursor:ew-resize}`；范围 126–360）。
        /// 宽度写进模块级静态字段 ⇒ 会话内保持、重开窗口回默认（预览 `railW` 同语义）。
        /// </summary>
        /// <returns>鼠标当前悬停的把手（0 无 / 1 左 / 2 右），供绘制期画高亮。</returns>
        private static int HandleRailResize(Rect viewport, Rect left, Rect right)
        {
            const float handle = 8f;
            // 把手贴**内缘**（预览 `.siderail.left .rhandle{right:-4px}`）
            Rect leftHandle = new Rect(left.xMax - handle * 0.5f, left.y, handle, left.height);
            Rect rightHandle = new Rect(right.x - handle * 0.5f, right.y, handle, right.height);
            int hovered = Mouse.IsOver(leftHandle) ? 1 : (Mouse.IsOver(rightHandle) ? 2 : 0);

            Event current = Event.current;
            if (current == null)
            {
                return hovered;
            }
            if (current.type == EventType.MouseDown && current.button == 0)
            {
                if (leftHandle.Contains(current.mousePosition))
                {
                    railDragSide = 1;
                    current.Use();
                }
                else if (rightHandle.Contains(current.mousePosition))
                {
                    railDragSide = 2;
                    current.Use();
                }
                return railDragSide;
            }
            if (railDragSide == 0)
            {
                return hovered;
            }
            if (current.type == EventType.MouseDrag)
            {
                if (railDragSide == 1)
                {
                    railWidthLeft = Mathf.Clamp(current.mousePosition.x - viewport.x - RailInset,
                        RailWidthMin, RailWidthMax);
                }
                else
                {
                    railWidthRight = Mathf.Clamp(viewport.xMax - RailInset - current.mousePosition.x,
                        RailWidthMin, RailWidthMax);
                }
                current.Use();
                return railDragSide;
            }
            if (current.type == EventType.MouseUp)
            {
                railDragSide = 0;
                current.Use();
            }
            return hovered;
        }

        /// <summary>
        /// 把手悬停 / 拖拽高亮（预览 `.rhandle:hover{background:linear-gradient(...)}`）。
        /// 引擎画不了渐变 ⇒ 用 1px 强调色竖线 + 悬停提示（B 类降级，登记于变更标注）。
        /// </summary>
        private static void DrawRailHandleHint(Rect left, Rect right, int hovered)
        {
            if (railDragSide != 0)
            {
                hovered = railDragSide;
            }
            if (hovered == 0)
            {
                return;
            }
            Rect rail = hovered == 1 ? left : right;
            float x = hovered == 1 ? left.xMax - 1f : right.x;
            Widgets.DrawBoxSolid(new Rect(x, rail.y, 2f, rail.height),
                UITheme.WithAlpha(UITheme.Info, 0.75f));
            TooltipHandler.TipRegion(new Rect(
                    hovered == 1 ? left.xMax - 4f : right.x, rail.y, 8f, rail.height),
                T("PersonalChronicle.UI.Layout.RailResizeTip"));
        }

        // ================== 左轨：原料库 / 设备库 ==================

        /// <summary>左轨：**原料库 / 设备库**两个页签（预览 `#rail-left .rtabs`）+ 内容。</summary>
        private static void DrawLeftRail(Rect rail, WorkshopFloorSnapshot snapshot, Palette palette,
            List<PaletteEntry> devicePalette)
        {
            DrawRailSurface(rail, palette);

            float x = rail.x + RailPadX;
            float width = rail.width - RailPadX * 2f;
            float y = rail.y + RailPadY;

            // 页签行（`.rtabs`：两个等宽小按钮；预览的设备库页签带待放置计数）
            float tabHeight = RailLine() + RailGap;
            float tabWidth = (width - RailGap) * 0.5f;
            int deviceCount = CountPaletteDevices(devicePalette);
            DrawRailTab(new Rect(x, y, tabWidth, tabHeight),
                T("PersonalChronicle.UI.Layout.RailTab.Raw"), !leftRailShowsDevices);
            DrawRailTab(new Rect(x + tabWidth + RailGap, y, tabWidth, tabHeight),
                deviceCount > 0
                    ? T("PersonalChronicle.UI.Layout.RailTab.Devices") + " (" + deviceCount + ")"
                    : T("PersonalChronicle.UI.Layout.RailTab.Devices"),
                leftRailShowsDevices);
            y += tabHeight + RailGap;

            Rect body = new Rect(x, y, width, Mathf.Max(10f, rail.yMax - RailPadY - y));
            if (leftRailShowsDevices)
            {
                DrawDeviceLibrary(body, devicePalette);
                return;
            }
            DrawRawPane(body, snapshot, palette, rail.width);
        }

        /// <summary>轨道底板（预览 `.siderail`：底色 + 1px 外框；渐变与圆角是引擎缺口，B 类降级）。</summary>
        private static void DrawRailSurface(Rect rail, Palette palette)
        {
            Widgets.DrawBoxSolid(rail, palette.RailFill);
            UIComponents.Border(rail, palette.LegendLine);
        }

        /// <summary>调色板里的设备总数（页签计数用；与画布同源，零新增契约）。</summary>
        private static int CountPaletteDevices(List<PaletteEntry> palette)
        {
            if (palette == null)
            {
                return 0;
            }
            int total = 0;
            for (int i = 0; i < palette.Count; i++)
            {
                List<DeviceEntry> devices = palette[i].Devices;
                if (devices != null)
                {
                    total += devices.Count;
                }
            }
            return total;
        }

        /// <summary>原料库页（预览 `#rp-raws`）：标题 → 收货/IQC → 分区头 → 原料行。</summary>
        private static void DrawRawPane(Rect area, WorkshopFloorSnapshot snapshot, Palette palette,
            float railWidth)
        {
            // 极矮视口保护（2026-10-06 守门 P3-2）：头块（标题 + 双小框 + 分区头）约 4 行 + 2 个小框高，
            // 给不够时**整体不画内容**（只留底板）—— IMGUI 无裁剪，溢出会盖住轨道下方的行。
            if (area.height < RailLine() * 4f + RailChipHeight() * 2f)
            {
                return;
            }

            float x = area.x;
            float width = area.width;
            float y = DrawRailTitle(x, area.y, width, palette,
                T("PersonalChronicle.UI.Floor.Raw.Title"),
                T("PersonalChronicle.UI.Floor.Raw.Stencil"),
                T("PersonalChronicle.UI.Floor.Raw.StencilTip"));

            y = DrawRailChip(new Rect(x, y, width, RailChipHeight()),
                T("PersonalChronicle.UI.Floor.Raw.Dock"), palette.Busbar, palette) + RailGap;
            y = DrawRailChip(new Rect(x, y, width, RailChipHeight()),
                T("PersonalChronicle.UI.Floor.Raw.Iqc"), palette.BayBadge, palette) + RailGap;

            y = DrawRailSection(x, y, width,
                T("PersonalChronicle.UI.Floor.Raw.Section"), palette,
                T("PersonalChronicle.UI.Floor.Raw.AbcTip"));

            List<FloorRawItemView> items = snapshot.RawItems;
            if (items == null || items.Count == 0)
            {
                // 空态（LAND-009：不画假数据，但**必须**说清"为什么空"与"怎么办"）——
                // 原实现只有一行 dim 文案，玩家读起来像"面板坏了"。原因 + 引导两行（两条键都保留使用）。
                DrawRailNote(x, y, width, area.yMax,
                    T("PersonalChronicle.UI.Floor.Raw.Empty"), palette.TextDim);
                DrawRailNote(x, y + RailLine(), width, area.yMax,
                    T("PersonalChronicle.UI.Floor.Raw.EmptyHint"), palette.TextDim);
                return;
            }

            float pitch = RailRowPitch();
            int capacity = Mathf.Max(0, Mathf.FloorToInt(Mathf.Max(0f, area.yMax - y) / pitch));
            // 溢出必须**明示**（GATE-UI 不静默截断）：预留一行给「还有 N 项」。
            int hidden = Mathf.Max(0, items.Count - capacity);
            if (hidden > 0)
            {
                capacity = Mathf.Max(0, capacity - 1);
                hidden = items.Count - capacity;
            }
            int count = Mathf.Min(capacity, items.Count);
            float scale = RailScale(railWidth);
            for (int i = 0; i < count; i++)
            {
                DrawRawRow(new Rect(x, y + i * pitch, width, RailLine()), items[i], palette, scale);
            }
            // 溢出提示同样受容器底约束（行高由字体档派生，极矮档可能没位置 ⇒ 不画到容器外）
            float moreY = y + count * pitch;
            if (hidden > 0 && moreY + RailLine() <= area.yMax)
            {
                DrawRailText(x, moreY, width,
                    T1("PersonalChronicle.UI.Floor.Raw.More", hidden), palette.TextDim,
                    TextAnchor.MiddleLeft, null);
            }
        }

        /// <summary>
        /// 一行原料（预览 `.rmat`）：`[ABC 角标] [名称] [水位条] [数值]`。
        /// 列宽按 <c>--rfs</c> 比例缩放，空间不足时从名称列让位（D3），绝不重叠。
        /// </summary>
        private static void DrawRawRow(Rect row, FloorRawItemView item, Palette palette, float scale)
        {
            float line = row.height;
            float gap = 5f;   // 预览 .rmat{gap:5px}
            float abcWidth = MeasureWidth("A", RailFont) + 7f;   // 预览 .rabc{padding:0 3px;border:1px}
            float nameWidth = RailNameWidth * scale;
            float valueWidth = RailValueWidth * scale;
            float barWidth = row.width - abcWidth - gap * 3f - nameWidth - valueWidth;
            if (barWidth < RailMinBarWidth)
            {
                // 降级：名称列让位给水位条（下限 24px，仍可读两三个汉字 + 悬停显全文）
                nameWidth = Mathf.Max(24f, nameWidth - (RailMinBarWidth - barWidth));
                barWidth = row.width - abcWidth - gap * 3f - nameWidth - valueWidth;
            }
            if (barWidth <= 0f)
            {
                // 极窄轨道（用户把轨道拖到 126 且字号已升档）：只画名称，不硬挤（D6）。
                DrawRailText(row.x, row.y, row.width, item.Name,
                    item.Low ? palette.LampFault : palette.TextStrong, TextAnchor.MiddleLeft,
                    item.Name + " · " + item.CountText + "\n" + item.Note);
                return;
            }

            float cursor = row.x;
            if (!string.IsNullOrEmpty(item.TierKey))
            {
                // 预览 .rabc：1px 描边小徽标，A=bad / B=warn / C=dim（口径见 AbcTip）
                Color tierColor = item.TierKey == "A"
                    ? palette.LampFault
                    : (item.TierKey == "B" ? palette.LampWarn : palette.TextDim);
                Rect badge = new Rect(cursor, row.y + 2f, abcWidth, Mathf.Max(10f, line - 4f));
                DrawFrame(badge, tierColor, 1f);
                UIComponents.Label(badge, item.TierKey, RailFont, tierColor, TextAnchor.MiddleCenter);
            }
            cursor += abcWidth + gap;

            UIComponents.Label(new Rect(cursor, row.y, nameWidth, line),
                UIComponents.TruncateToWidth(item.Name, nameWidth, RailFont), RailFont,
                item.Low ? palette.LampFault : palette.TextStrong, TextAnchor.MiddleLeft);
            cursor += nameWidth + gap;

            // 水位条（预览 .rbar{height:8px} + 30% 安全库存刻度 .ss）
            Rect bar = new Rect(cursor, row.y + (line - RailBarHeight) * 0.5f, barWidth, RailBarHeight);
            Widgets.DrawBoxSolid(bar, palette.ProgressTrack);
            Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(item.Ratio01), bar.height),
                item.Low ? palette.LampFault : palette.ProgressFill);
            Widgets.DrawBoxSolid(new Rect(bar.x + bar.width * 0.3f, bar.y - 1f, 2f, bar.height + 2f),
                palette.LampWarn);
            cursor += barWidth + gap;

            UIComponents.Label(new Rect(cursor, row.y, valueWidth, line),
                UIComponents.TruncateToWidth(item.CountText, valueWidth, RailFont), RailFont,
                item.Low ? palette.LampFault : palette.TextDim, TextAnchor.MiddleRight);

            TooltipHandler.TipRegion(row, item.Name + " · " + item.CountText + "\n" + item.Note);
        }

        /// <summary>
        /// 设备库（预览 `#rp-devs`）：按车间分组的**真实设备清单**（状态点 + 名称 + 溢出提示）。
        /// 数据与自由画布同源（`WorkshopLayoutCanvas.CollectPalette` 的节流结果）⇒ 零新增契约。
        /// </summary>
        private static void DrawDeviceLibrary(Rect area, List<PaletteEntry> palette)
        {
            float line = RailLine();
            float y = area.y;
            int total = CountPaletteDevices(palette);
            y = DrawRailSection(area.x, y, area.width,
                T1("PersonalChronicle.UI.Layout.RailTab.DeviceSection", total), null,
                T("PersonalChronicle.UI.Layout.RailTab.DeviceSectionTip"));

            if (palette == null || palette.Count == 0)
            {
                DrawRailNote(area.x, y, area.width, area.yMax,
                    T("PersonalChronicle.UI.Layout.RailTab.NoWorkshop"), UITheme.Muted);
                return;
            }
            if (total == 0)
            {
                DrawRailNote(area.x, y, area.width, area.yMax,
                    T("PersonalChronicle.UI.Layout.RailTab.NoDevice"), UITheme.Muted);
                return;
            }

            // 交互行高 = 文本行 + 4（预览 .drow2{padding:1.5px 3px}；同时满足可点行高的最低要求）
            float rowHeight = line + 4f;
            float headHeight = line + 2f;
            float rowBottom = area.yMax - rowHeight;   // 底部留一行给"还有 N 台"提示
            int hidden = 0;
            string pending = WorkshopLayoutCanvas.PendingDevicePick;
            for (int i = 0; i < palette.Count; i++)
            {
                PaletteEntry entry = palette[i];
                if (entry.Devices == null || entry.Devices.Count == 0)
                {
                    continue;
                }
                if (y + headHeight > rowBottom)
                {
                    hidden += entry.Devices.Count;
                    continue;
                }
                // 分组头（预览 .dlib-h：车间名 + 虚线底）
                DrawRailText(area.x, y, area.width,
                    entry.Label + " · " + entry.Devices.Count, UITheme.Accent,
                    TextAnchor.MiddleLeft, entry.Label);
                DrawDashedLine(new Vector2(area.x, y + line + 1f),
                    new Vector2(area.xMax, y + line + 1f), UITheme.BorderSoft, 1f, 2f, 2f);
                y += headHeight;
                for (int d = 0; d < entry.Devices.Count; d++)
                {
                    if (y + rowHeight > rowBottom)
                    {
                        hidden++;
                        continue;
                    }
                    DeviceEntry device = entry.Devices[d];
                    Rect row = new Rect(area.x, y, area.width, rowHeight);
                    bool picked = pending == device.StableId;
                    if (picked)
                    {
                        Widgets.DrawBoxSolid(row, UITheme.WithAlpha(UITheme.Info, 0.20f));
                        UIComponents.Border(row, UITheme.Info);
                    }
                    else
                    {
                        Widgets.DrawHighlightIfMouseover(row);
                    }
                    Widgets.DrawBoxSolid(new Rect(area.x + 2f, y + rowHeight * 0.35f, 7f, 7f),
                        WorkshopLayoutCanvas.LampColorOf(device.StateKey));
                    UIComponents.Label(new Rect(area.x + 11f, y, Mathf.Max(16f, area.width - 11f), rowHeight),
                        UIComponents.TruncateToWidth(device.Label, Mathf.Max(16f, area.width - 11f), RailFont),
                        RailFont, picked ? UITheme.Info : UITheme.Text, TextAnchor.MiddleLeft);
                    HandleDeviceLibraryRowInput(row, device, picked);
                    TooltipHandler.TipRegion(row,
                        device.Label
                        + (string.IsNullOrEmpty(device.SubLine) ? string.Empty : "\n" + device.SubLine)
                        + "\n" + T(picked
                            ? "PersonalChronicle.UI.Layout.RailTab.PickedTip"
                            : "PersonalChronicle.UI.Layout.RailTab.DragTip"));
                    y += rowHeight;
                }
                y += 3f;
            }
            if (hidden > 0)
            {
                // 溢出明示（GATE-UI 大列表降级）：不静默吞设备。
                DrawRailText(area.x, area.yMax - rowHeight, area.width,
                    T1("PersonalChronicle.UI.Layout.RailTab.MoreDevices", hidden), UITheme.Muted,
                    TextAnchor.MiddleLeft, null);
            }
        }

        /// <summary>设备库行的按下位置（区分「拖出」与「点选」；会话内）。</summary>
        private static Vector2 railPressPos;

        /// <summary>设备库行是否被按下的 stableId（会话内）。</summary>
        private static string railPressId;

        /// <summary>判定「拖动」与「点击」的位移阈值（屏幕像素）。</summary>
        private const float RailClickSlopPx = 4f;

        /// <summary>
        /// 设备库行的输入：<b>按下</b>⇒起拖；<b>抬起且几乎没移动</b>⇒切换"点选待放置"。
        /// 两条路径互斥；状态自持（不依赖画布侧的绘制顺序）。
        /// 2026-10-02 审计 P1-7：拖放入口不可发现（隐藏页签 / 窄视口整条消失）⇒ 补等价点选路径。
        /// </summary>
        private static void HandleDeviceLibraryRowInput(Rect row, DeviceEntry device, bool picked)
        {
            if (string.IsNullOrEmpty(device.StableId))
            {
                return;
            }
            Event ev = Event.current;
            if (ev == null)
            {
                return;
            }
            if (ev.type == EventType.MouseDown && ev.button == 0 && Mouse.IsOver(row))
            {
                railPressId = device.StableId;
                railPressPos = ev.mousePosition;
                WorkshopLayoutCanvas.BeginDeviceDrag(device.StableId, device.Label);
                return;
            }
            if (ev.type == EventType.MouseUp && ev.button == 0 && railPressId == device.StableId
                && Mouse.IsOver(row)
                && (ev.mousePosition - railPressPos).sqrMagnitude <= RailClickSlopPx * RailClickSlopPx)
            {
                railPressId = null;
                WorkshopLayoutCanvas.PickDeviceForPlace(device.StableId, device.Label);
            }
        }

        // ================== 右轨：成品库 ==================

        /// <summary>右轨：成品库（在库件数 → Top 成品 → 品质分布 → 打包/发货）。</summary>
        private static void DrawDepotRail(Rect rail, WorkshopFloorSnapshot snapshot, Palette palette)
        {
            DrawRailSurface(rail, palette);

            float x = rail.x + RailPadX;
            float width = rail.width - RailPadX * 2f;
            float y = rail.y + RailPadY;
            float chipHeight = RailChipHeight();
            float mediumLine = Text.LineHeightOf(GameFont.Medium);
            float bottom = rail.yMax - RailPadY - chipHeight * 2f - RailGap * 2f;
            // 极矮视口保护（与左轨同规，守门 P3-2）：标题（2 行）+ 大数字（Medium）+ 底部两个小框
            // 都放不下时**不画内容**（只留底板），避免溢出盖住轨道下方的行。
            if (bottom < y + RailLine() * 2f + mediumLine)
            {
                return;
            }

            y = DrawRailTitle(x, y, width, palette,
                T("PersonalChronicle.UI.Floor.Depot.Title"),
                T("PersonalChronicle.UI.Floor.Depot.Stencil"),
                T("PersonalChronicle.UI.Floor.Depot.StencilTip"));

            FloorDepotView depot = snapshot.Depot;
            // 预览 .rbig{font-size:20px;line-height:1.1}——在库件数用最大档（行高同样取字体度量）
            DrawRailTextFont(x, y, width, depot != null ? depot.TotalText : string.Empty,
                GameFont.Medium, palette.RailTitle, TextAnchor.MiddleLeft, null);
            y += mediumLine;

            if (depot == null || depot.Empty)
            {
                DrawRailNote(x, y, width, bottom,
                    T("PersonalChronicle.UI.Floor.Depot.Empty"), palette.TextDim);
            }
            else
            {
                List<string> top = depot.TopItems;
                float pitch = RailRowPitch();
                bool hasChips = depot.QualityChips != null && depot.QualityChips.Count > 0;
                int capacity = Mathf.Max(0, Mathf.FloorToInt(Mathf.Max(0f,
                    bottom - y - (hasChips ? RailLine() + RailGap : 0f)) / pitch));
                int listHidden = top != null ? Mathf.Max(0, top.Count - capacity) : 0;
                if (listHidden > 0)
                {
                    capacity = Mathf.Max(0, capacity - 1);
                    listHidden = Mathf.Max(0, top.Count - capacity);
                }
                int count = top != null ? Mathf.Min(capacity, top.Count) : 0;
                for (int i = 0; i < count; i++)
                {
                    DrawRailText(x, y + i * pitch, width, top[i], palette.TextStrong,
                        TextAnchor.MiddleLeft, null);
                }
                y += count * pitch;
                if (listHidden > 0)
                {
                    DrawRailText(x, y, width,
                        T1("PersonalChronicle.UI.Floor.Depot.More", listHidden), palette.TextDim,
                        TextAnchor.MiddleLeft, null);
                    y += pitch;
                }

                if (hasChips)
                {
                    y = DrawRailSection(x, y, width,
                        T("PersonalChronicle.UI.Floor.Depot.Section"), palette,
                        T("PersonalChronicle.UI.Floor.Depot.SectionTip"));
                    // 品质分布：预览 .rqual{flex-wrap:wrap;gap:3px}——多行铺满，超出容量即停
                    float chipX = x;
                    float chipBottom = bottom;
                    for (int i = 0; i < depot.QualityChips.Count; i++)
                    {
                        string chip = depot.QualityChips[i];
                        float chipW = MeasureWidth(chip, RailFont) + 4f;
                        if (chipX + chipW > x + width && chipX > x)
                        {
                            chipX = x;
                            y += RailLine();
                        }
                        if (y + RailLine() > chipBottom)
                        {
                            break;
                        }
                        DrawRailText(chipX, y, chipW, chip, palette.TextDim,
                            TextAnchor.MiddleLeft, null);
                        chipX += chipW;
                    }
                }
            }

            // 底部：打包 / 发货（预览 .rdock 固定在轨道底）
            float dockY = rail.yMax - RailPadY - chipHeight * 2f - RailGap;
            DrawRailChip(new Rect(x, dockY, width, chipHeight),
                T("PersonalChronicle.UI.Floor.Depot.Packing"), palette.SupLine, palette);
            DrawRailChip(new Rect(x, dockY + chipHeight + RailGap, width, chipHeight),
                T("PersonalChronicle.UI.Floor.Depot.Ship"), palette.LampRun, palette);
        }

        // ================== 轨道小工具 ==================

        /// <summary>轨道页签按钮（预览 `.rtabs button`）：选中 = 强调色描边 + 15% 填充。</summary>
        private static void DrawRailTab(Rect rect, string label, bool on)
        {
            if (on)
            {
                Widgets.DrawBoxSolid(rect, UITheme.WithAlpha(UITheme.Info, 0.15f));   // 预览 color-mix(15%)
            }
            else
            {
                Widgets.DrawHighlightIfMouseover(rect);
            }
            UIComponents.Border(rect, on ? UITheme.Info : UITheme.BorderSoft);
            // 4 参重载：被截断即自动挂原文悬停（页签的「设备库 (N)」在窄档会被截，原文含计数）
            Rect textRect = new Rect(rect.x + 3f, rect.y, Mathf.Max(10f, rect.width - 6f), rect.height);
            UIComponents.Label(textRect,
                UIComponents.TruncateToWidth(label, textRect.width, RailFont, textRect),
                RailFont, on ? UITheme.Info : UITheme.Dim, TextAnchor.MiddleCenter);
            if (Widgets.ButtonInvisible(rect) && !on)
            {
                leftRailShowsDevices = !leftRailShowsDevices;
            }
        }

        /// <summary>轨道标题（预览 `.railtitle` 主行 + `span` 口径行）。</summary>
        private static float DrawRailTitle(float x, float y, float width, Palette palette,
            string title, string stencil, string stencilTip)
        {
            float line = RailLine();
            DrawRailText(x, y, width, title, palette.RailTitle, TextAnchor.MiddleLeft, null);
            y += line;
            DrawRailText(x, y, width, stencil, palette.TextDim, TextAnchor.MiddleLeft, stencilTip);
            return y + line + 2f;
        }

        /// <summary>分区头（预览 `.rsechead`：dim 小字 + 1px 虚线底）——原实现完全缺失。</summary>
        private static float DrawRailSection(float x, float y, float width, string text,
            Palette? palette, string tip)
        {
            float line = RailLine();
            DrawRailText(x, y, width, text,
                palette.HasValue ? palette.Value.TextDim : UITheme.Dim, TextAnchor.MiddleLeft, tip);
            float ruleY = y + line + 1f;
            DrawDashedLine(new Vector2(x, ruleY), new Vector2(x + width, ruleY),
                UITheme.BorderSoft, 1f, 2f, 2f);
            return ruleY + 1f + RailGap * 0.5f;
        }

        /// <summary>小框（预览 `.rdock`：1px 虚线框 + 单行居中 + 3px 上下内距）。</summary>
        private static float DrawRailChip(Rect rect, string label, Color color, Palette palette)
        {
            DrawDashedFrame(rect, color, 1f, 2f, 2f);
            UIComponents.Label(rect,
                UIComponents.TruncateToWidth(label, Mathf.Max(10f, rect.width - 8f), RailFont, rect),
                RailFont, color, TextAnchor.MiddleCenter);
            return rect.yMax;
        }

        /// <summary>
        /// 单行轨道文本（**行高由字体档派生** + 截断补偿：真的被截断时悬停显示原文）。
        /// </summary>
        /// <param name="tip">额外悬停提示（非空时优先，用于口径说明）；为空则截断时自动挂原文。</param>
        /// <returns>该行底部 y。</returns>
        private static float DrawRailText(float x, float y, float width, string text, Color color,
            TextAnchor anchor, string tip)
        {
            float line = RailLine();
            DrawRailTextAt(new Rect(x, y, width, line), text, RailFont, color, anchor, tip);
            return y + line;
        }

        /// <summary>同上，但允许指定字体档（成品库的 20px 在库件数用 Medium）。</summary>
        private static float DrawRailTextFont(float x, float y, float width, string text,
            GameFont font, Color color, TextAnchor anchor, string tip)
        {
            DrawRailTextAt(new Rect(x, y, width, Text.LineHeightOf(font)), text, font, color, anchor, tip);
            return y + Text.LineHeightOf(font);
        }

        private static bool DrawRailTextAt(Rect rect, string text, GameFont font, Color color,
            TextAnchor anchor, string tip)
        {
            if (rect.width <= 1f)
            {
                return false;
            }
            if (!string.IsNullOrEmpty(text))
            {
                string shown = string.IsNullOrEmpty(tip)
                    ? UIComponents.TruncateToWidth(text, rect.width, font, rect)   // 截断即自动挂原文
                    : UIComponents.TruncateToWidth(text, rect.width, font);
                UIComponents.Label(rect, shown, font, color, anchor);
            }
            if (!string.IsNullOrEmpty(tip))
            {
                TooltipHandler.TipRegion(rect, tip);
            }
            return true;
        }

        /// <summary>
        /// 多行说明（空态 / 引导）。文案自带 <c>\n</c>（Keyed 已支持），逐行绘制并在高度不足时停止
        /// ——**绝不画到容器外**（IMGUI 无裁剪，溢出必被后画元素遮盖）。
        /// </summary>
        private static void DrawRailNote(float x, float y, float width, float bottom, string text,
            Color color)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            float line = RailLine();
            string[] lines = SplitNote(text);
            for (int i = 0; i < lines.Length; i++)
            {
                if (y + line > bottom)
                {
                    break;
                }
                DrawRailText(x, y, width, lines[i], color, TextAnchor.MiddleLeft, null);
                y += line;
            }
        }

        // 折行结果缓存（同文本同语言只切一次；绘制路径不重复分配）。
        private static string railNoteCacheKey;
        private static string[] railNoteCacheLines;

        private static string[] SplitNote(string text)
        {
            if (string.Equals(railNoteCacheKey, text, System.StringComparison.Ordinal)
                && railNoteCacheLines != null)
            {
                return railNoteCacheLines;
            }
            railNoteCacheKey = text;
            railNoteCacheLines = text.Split('\n');
            return railNoteCacheLines;
        }
    }
}
