using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>
    /// Partial of <see cref="WorkshopFloorPlan"/> —— 逐图层绘制（顺序即 z-order，与预览
    /// <c>buildFloor()</c> 的 append 顺序一致）。所有设计坐标常量取自
    /// <see cref="WorkshopFloorLayout"/>，不在本文件写裸数字。
    ///
    /// <para><b>文字容量铁律（LAND-007/008，实机 P1–P5 的根因修复）</b>：
    /// 几何按 k 等比缩放，但引擎字体是固定像素 —— 预览里"设计坐标偏移"的排版在 k &lt; 1 时
    /// 必然叠字。因此本文件所有文字都是<b>按像素可容纳行数排版</b>：
    /// <list type="bullet">
    /// <item><c>k ≥ 0.95</c> → 用预览的设计位置（Designed），此时偏移与字高比例接近 1；</item>
    /// <item>否则 → <b>堆叠行</b>：从盒顶起每行一个 <c>Text.LineHeight</c>，<b>放不下的行不画</b>
    /// （信息进悬停提示），而不是叠着画；</item>
    /// <item>小到放不下一行时，只留几何与灯色。</item>
    /// </list></para>
    /// </summary>
    internal static partial class WorkshopFloorPlan
    {
        /// <summary>本次绘制的上下文：画布 + 缩放 + 调色板 + 文字容量（避免大参数串来串去）。</summary>
        private struct DrawContext
        {
            internal Rect Canvas;
            internal float K;
            internal Palette Palette;
            internal float TinyLine;
            internal float SmallLine;

            /// <summary>
            /// 是否使用"机箱名升档到 Small"（几何足够大时）。**字体档随缩放自适应**，
            /// 但行距永远是真实字高 —— 所以不存在"行距小于字高"的挤兑。
            /// </summary>
            internal bool MachineNameAsSmall;
            internal bool Gridlines;
            internal bool AisleLabels;
            internal bool LegendLabels;
            internal bool LegendNote;
            internal bool Bolts;

            /// <summary>选中的格 / 设备（-1 = 无）；绘制期只读（点击命中测试在 Draw 末尾统一做）。</summary>
            internal int SelectedSlot;
            internal int SelectedDevice;

            internal static DrawContext Create(Rect canvas, float k, Palette palette)
            {
                DrawContext context = new DrawContext
                {
                    Canvas = canvas,
                    K = k,
                    Palette = palette,
                    TinyLine = LineHeight(GameFont.Tiny),
                    SmallLine = LineHeight(GameFont.Small),
                    SelectedSlot = -1,
                    SelectedDevice = -1
                };
                // 机箱名升档：机箱像素宽 ≥132 时用 Small（`Small` 的 6 字标签 ≈ 100px 放得下）
                context.MachineNameAsSmall = WorkshopFloorLayout.MachineW * k >= 132f;
                context.Gridlines = k >= 0.45f;
                context.AisleLabels = true;
                context.LegendLabels = true;
                context.LegendNote = k >= 1.1f;
                context.Bolts = k >= 0.8f;
                return context;
            }

            /// <summary>盒内能放几行 Tiny 文字（<paramref name="boxDesignHeight"/> 是设计高）。</summary>
            internal int TinyRowsIn(float boxDesignHeight)
            {
                float available = boxDesignHeight * K - 4f;
                return Mathf.Max(0, Mathf.FloorToInt(available / Mathf.Max(1f, TinyLine)));
            }
        }

        private static float LineHeight(GameFont font)
        {
            GameFont previous = Text.Font;
            Text.Font = font;
            float height = Text.LineHeight;
            Text.Font = previous;
            return height;
        }

        // ---- L1 厂房外壳 ----

        private static void DrawShell(DrawContext ctx)
        {
            Palette palette = ctx.Palette;
            Rect slab = Design(ctx, WorkshopFloorLayout.Bx, WorkshopFloorLayout.By,
                WorkshopFloorLayout.Bw, WorkshopFloorLayout.Bh);
            Widgets.DrawBoxSolid(slab, palette.Slab);
            DrawFrame(slab, palette.Wall, S(WorkshopFloorLayout.WallThickness, ctx.K));

            float inset = WorkshopFloorLayout.SlabInset;
            Rect inner = Design(ctx, WorkshopFloorLayout.Bx + inset, WorkshopFloorLayout.By + inset,
                WorkshopFloorLayout.Bw - inset * 2f, WorkshopFloorLayout.Bh - inset * 2f);
            DrawFrame(inner, palette.WallInner, Mathf.Max(1f, 1.2f * ctx.K));
        }

        // ---- L2 柱网 ----

        private static void DrawColumnGrid(DrawContext ctx)
        {
            if (!ctx.Gridlines)
            {
                // k 太小时虚线会退化成点状噪声，整层不画（P7）。
                return;
            }
            Palette palette = ctx.Palette;
            float y1 = WorkshopFloorLayout.By + WorkshopFloorLayout.GridlineInset;
            float y2 = WorkshopFloorLayout.By + WorkshopFloorLayout.Bh - WorkshopFloorLayout.GridlineInset;
            float x1 = WorkshopFloorLayout.Bx + WorkshopFloorLayout.GridlineInset;
            float x2 = WorkshopFloorLayout.Bx + WorkshopFloorLayout.Bw - WorkshopFloorLayout.GridlineInset;
            float width = S(0.8f, ctx.K);
            Color line = UITheme.WithAlpha(palette.Gridline, ctx.K < 0.8f ? 0.16f : 0.32f);
            float dash = Mathf.Max(2f, 3f * ctx.K);
            float gap = Mathf.Max(2f, 7f * ctx.K);

            float[] colX = WorkshopFloorLayout.ColX;
            for (int i = 0; i < colX.Length; i++)
            {
                DrawDashedLine(P(ctx, colX[i], y1), P(ctx, colX[i], y2), line, width, dash, gap);
            }
            float[] colY = WorkshopFloorLayout.ColY;
            for (int i = 0; i < colY.Length; i++)
            {
                DrawDashedLine(P(ctx, x1, colY[i]), P(ctx, x2, colY[i]), line, width, dash, gap);
            }

            float size = WorkshopFloorLayout.ColumnSize;
            for (int r = 0; r < colY.Length; r++)
            {
                for (int c = 0; c < colX.Length; c++)
                {
                    Rect column = Design(ctx, colX[c] - size * 0.5f, colY[r] - size * 0.5f, size, size);
                    Widgets.DrawBoxSolid(column, palette.Column);
                    UIComponents.Border(column, palette.WallInner);
                }
            }
        }

        // ---- L3 母线管廊 ----

        private static void DrawBusbar(DrawContext ctx)
        {
            float width = S(2f, ctx.K);
            float left = WorkshopFloorLayout.Bx + WorkshopFloorLayout.BusbarInset;
            float right = WorkshopFloorLayout.Bx + WorkshopFloorLayout.Bw - WorkshopFloorLayout.BusbarInset;
            Widgets.DrawLine(P(ctx, left, WorkshopFloorLayout.BusbarY),
                P(ctx, right, WorkshopFloorLayout.BusbarY), ctx.Palette.Busbar, width);

            float[] stubs = WorkshopFloorLayout.BusbarStubX;
            for (int i = 0; i < stubs.Length; i++)
            {
                Widgets.DrawLine(P(ctx, stubs[i], WorkshopFloorLayout.BusbarY),
                    P(ctx, stubs[i], WorkshopFloorLayout.BusbarStubBottom), ctx.Palette.Busbar, width);
            }
        }

        // ---- L4 通道 ----

        private static void DrawAisles(DrawContext ctx)
        {
            Palette palette = ctx.Palette;
            float dash = Mathf.Max(2f, 10f * ctx.K);
            float gap = Mathf.Max(2f, 8f * ctx.K);
            // 标注是"贴在带下沿的设计坐标文字"（预览 y=224/359/532），不是带内文字 ⇒ 不受带高限制。
            const bool labels = true;

            float[] bands = WorkshopFloorLayout.AisleBandY;
            for (int i = 0; i < bands.Length; i++)
            {
                Rect band = Design(ctx, WorkshopFloorLayout.AisleBandX, bands[i],
                    WorkshopFloorLayout.AisleBandW, WorkshopFloorLayout.AisleBandH);
                Widgets.DrawBoxSolid(band, palette.AisleFill);
                DrawDashedLine(new Vector2(band.x, band.y), new Vector2(band.xMax, band.y),
                    palette.AisleLine, S(1f, ctx.K), dash, gap);
                DrawDashedLine(new Vector2(band.x, band.yMax), new Vector2(band.xMax, band.yMax),
                    palette.AisleLine, S(1f, ctx.K), dash, gap);
                if (labels)
                {
                    DrawTextRight(ctx, WorkshopFloorLayout.AisleLabelRightX,
                        WorkshopFloorLayout.AisleBandTextY[i], 200f,
                        T("PersonalChronicle.UI.Floor.Aisle.Secondary"), GameFont.Tiny, palette.TextDim);
                }
            }

            Rect main = Design(ctx, WorkshopFloorLayout.AisleBandX, WorkshopFloorLayout.MainAisleY,
                WorkshopFloorLayout.AisleBandW, WorkshopFloorLayout.MainAisleH);
            Widgets.DrawBoxSolid(main, palette.AisleFill);
            DrawFrame(main, palette.AisleLine, S(1.6f, ctx.K));
            DrawText(ctx, WorkshopFloorLayout.AisleLabelLeftX,
                WorkshopFloorLayout.MainAisleTextY, 700f,
                T("PersonalChronicle.UI.Floor.Aisle.Main"), GameFont.Tiny, palette.TextDim);
        }

        // ---- L5 支持功能区 ----

        private static void DrawSupportZones(DrawContext ctx)
        {
            Palette palette = ctx.Palette;
            float dash = Mathf.Max(2f, 6f * ctx.K);
            float gap = Mathf.Max(2f, 4f * ctx.K);
            int rows = ctx.TinyRowsIn(WorkshopFloorLayout.SupH);
            if (rows <= 0)
            {
                // 只画框（放不下一行字）
            }

            float[] xs = WorkshopFloorLayout.SupX;
            for (int i = 0; i < xs.Length; i++)
            {
                Rect zone = Design(ctx, xs[i], WorkshopFloorLayout.SupY,
                    WorkshopFloorLayout.SupW, WorkshopFloorLayout.SupH);
                Widgets.DrawBoxSolid(zone, palette.SupFill);
                DrawDashedLine(new Vector2(zone.x, zone.y), new Vector2(zone.xMax, zone.y),
                    palette.SupLine, S(1.2f, ctx.K), dash, gap);
                DrawDashedLine(new Vector2(zone.x, zone.yMax), new Vector2(zone.xMax, zone.yMax),
                    palette.SupLine, S(1.2f, ctx.K), dash, gap);
                DrawDashedLine(new Vector2(zone.xMax, zone.y), new Vector2(zone.xMax, zone.yMax),
                    palette.SupLine, S(1.2f, ctx.K), dash, gap);

                if (rows <= 0)
                {
                    continue;
                }
                string englishName = i < WorkshopFloorLayout.SupEnLabels.Length
                    ? WorkshopFloorLayout.SupEnLabels[i]
                    : string.Empty;
                string chineseName = i < WorkshopFloorLayout.SupNameKeys.Length
                    ? T(WorkshopFloorLayout.SupNameKeys[i])
                    : string.Empty;

                // 从盒顶按真实行高逐行排（**屏幕像素**，不能拿设计坐标乘 k）
                // 预览：`el('text',{x:s.x+10,y:46})` / `y:66` ⇒ 左内缩 10 设计单位
                float padX = S(WorkshopFloorLayout.SupPadX, ctx.K);
                float textWidth = Mathf.Max(8f, zone.width - padX * 2f);
                float top = zone.y + 2f;
                DrawTextPx(new Rect(zone.x + padX, top, textWidth, ctx.TinyLine),
                    chineseName, GameFont.Tiny, palette.TextStrong, TextAnchor.MiddleLeft);
                if (rows >= 2)
                {
                    DrawTextPx(new Rect(zone.x + padX, top + ctx.TinyLine, textWidth, ctx.TinyLine),
                        englishName, GameFont.Tiny, palette.TextDim, TextAnchor.MiddleLeft);
                }
            }
        }

        // ---- L6 车间格 ----

        private static void DrawBays(DrawContext ctx, WorkshopFloorSnapshot snapshot)
        {
            Palette palette = ctx.Palette;
            List<FloorBayView> bays = snapshot.Bays;
            float bayPxHeight = WorkshopFloorLayout.BayH * ctx.K;
            // 格名档位随缩放自适应（几何大 → 用 Small）；行距一律取真实字高，不会挤兑
            GameFont bayNameFont = ctx.K >= 0.5f ? GameFont.Small : GameFont.Tiny;
            float bayNameLine = bayNameFont == GameFont.Small ? ctx.SmallLine : ctx.TinyLine;
            bool drawName = bayPxHeight >= bayNameLine + 3f;
            bool drawMeta = bayPxHeight >= bayNameLine + ctx.TinyLine + 5f;

            for (int slot = 0; slot < WorkshopFloorSnapshot.BaySlots; slot++)
            {
                FloorBayView bay = bays != null && slot < bays.Count ? bays[slot] : null;
                Rect design = WorkshopFloorLayout.BayRect(slot);
                Rect rect = Design(ctx, design.x, design.y, design.width, design.height);
                bool highlight = bay != null && bay.Highlight;

                bool selected = ctx.SelectedSlot == slot;
                Widgets.DrawBoxSolid(rect,
                    selected ? palette.BayFillSelected : (highlight ? palette.BayFillHighlight : palette.BayFill));
                // 预览 .zone：虚线描边（dash 7/4）+ hover 转琥珀色 + .zone.sel 琥珀实线
                DrawDashedFrame(rect, palette.BayStroke, S(1.4f, ctx.K),
                    Mathf.Max(2f, 7f * ctx.K), Mathf.Max(2f, 4f * ctx.K));
                if (selected)
                {
                    DrawFrame(rect, palette.BayHover, S(2f, ctx.K));
                }
                else if (Mouse.IsOver(rect))
                {
                    DrawFrame(rect, palette.BayHover, S(1.8f, ctx.K));
                }
                DrawCornerMarks(rect, ctx.K, palette.BayCorner, S(2.4f, ctx.K));

                float badgeRadius = S(WorkshopFloorLayout.BayNumberRadius, ctx.K);
                float badgeX = design.x + WorkshopFloorLayout.BayNumberOffset;
                float badgeY = design.y + WorkshopFloorLayout.BayNumberOffset;
                DrawDisc(P(ctx, badgeX, badgeY), badgeRadius,
                    highlight ? palette.BayBadge : palette.BayCorner);
                if (badgeRadius >= 6f)
                {
                    DrawTextCentered(ctx, badgeX, badgeY,
                        WorkshopFloorLayout.BayNumberRadius * 2f,
                        bay != null ? bay.Number : (slot + 1).ToString(),
                        GameFont.Tiny, palette.BayBadgeText);
                }

                if (bay == null)
                {
                    continue;
                }

                // 名称 + 元信息：按真实行高从格顶往下排（屏幕像素）
                float padX = S(WorkshopFloorLayout.BayEdgeInset, ctx.K);
                float textWidth = Mathf.Max(8f, rect.width - padX * 2f);
                float top = rect.y + 3f;
                if (drawName)
                {
                    DrawTextPx(new Rect(rect.x + padX, top, textWidth, bayNameLine),
                        bay.Name, bayNameFont, palette.TextStrong, TextAnchor.MiddleLeft);
                }
                if (drawMeta)
                {
                    float metaTop = top + bayNameLine + 1f;
                    float leftWidth = textWidth * 0.62f;
                    DrawTextPx(new Rect(rect.x + padX, metaTop, leftWidth, ctx.TinyLine),
                        BayTypeText(bay), GameFont.Tiny, palette.TextDim, TextAnchor.MiddleLeft);
                    DrawTextPx(new Rect(rect.x + padX + leftWidth, metaTop,
                            textWidth - leftWidth, ctx.TinyLine),
                        bay.Meta, GameFont.Tiny, palette.TextDim, TextAnchor.MiddleRight);
                }
            }
        }

        private static string BayTypeText(FloorBayView bay)
        {
            return string.IsNullOrEmpty(bay.TypeLabel) ? "—" : bay.TypeLabel;
        }

        // ---- L7 设备 ----

        private static void DrawDevices(DrawContext ctx, WorkshopFloorSnapshot snapshot, float time)
        {
            List<FloorDeviceView> devices = snapshot.Devices;
            if (devices == null)
            {
                return;
            }
            Palette palette = ctx.Palette;
            bool blinkOn = Mathf.Repeat(time, 1f) < 0.5f;

            for (int i = 0; i < devices.Count; i++)
            {
                FloorDeviceView device = devices[i];
                if (device == null)
                {
                    continue;
                }
                Rect design = WorkshopFloorLayout.MachineRect(device.Slot, device.Index);
                Rect body = Design(ctx, design.x, design.y, design.width, design.height);
                bool running = device.StateKey == EquipmentStateResolver.KeyRun;

                float inflate = S(WorkshopFloorLayout.FoundInflate, ctx.K);
                Widgets.DrawBoxSolid(new Rect(body.x - inflate, body.y - inflate,
                    body.width + inflate * 2f, body.height + inflate * 2f), palette.MachineFound);

                if (ctx.Bolts)
                {
                    float boltRadius = Mathf.Max(1f, WorkshopFloorLayout.BoltRadius * ctx.K);
                    float inset = WorkshopFloorLayout.BoltInset;
                    float right = design.x + WorkshopFloorLayout.MachineW + inset;
                    float bottom = design.y + WorkshopFloorLayout.MachineH + inset;
                    DrawDisc(P(ctx, design.x - inset, design.y - inset), boltRadius, palette.Bolt);
                    DrawDisc(P(ctx, right, design.y - inset), boltRadius, palette.Bolt);
                    DrawDisc(P(ctx, design.x - inset, bottom), boltRadius, palette.Bolt);
                    DrawDisc(P(ctx, right, bottom), boltRadius, palette.Bolt);
                }

                bool selected = ctx.SelectedDevice == i;
                Widgets.DrawBoxSolid(body, palette.MachineBody);
                DrawFrame(body, selected
                        ? palette.BayHover
                        : (running ? palette.MachineStrokeHighlight : palette.MachineStroke),
                    S(selected ? 2f : 1.5f, ctx.K));

                Rect stripe = Design(ctx,
                    design.x + WorkshopFloorLayout.HazardStripInset,
                    design.y + WorkshopFloorLayout.HazardStripInset,
                    WorkshopFloorLayout.MachineW - WorkshopFloorLayout.HazardStripInset * 2f,
                    WorkshopFloorLayout.HazardStripH);
                if (stripe.height >= 2f)
                {
                    DrawTiled(stripe, WorkshopFloorTextures.Hazard, WorkshopFloorLayout.HazardStep,
                        FloorPalette.HazardTint);
                }

                Color lamp = LampColor(device.StateKey, palette);
                if (device.StateKey == EquipmentStateResolver.KeyFault && !blinkOn)
                {
                    lamp = UITheme.WithAlpha(lamp, 0.25f);
                }
                DrawDisc(P(ctx, design.x + WorkshopFloorLayout.LampOffsetX,
                    design.y + WorkshopFloorLayout.LampOffsetY), S(WorkshopFloorLayout.LampRadius, ctx.K), lamp);

                DrawDeviceText(ctx, device, design, lamp);
                DrawDeviceProgress(ctx, device, design);

                if (!string.IsNullOrEmpty(device.Tooltip))
                {
                    TooltipHandler.TipRegion(body, device.Tooltip);
                }
            }
        }

        /// <summary>
        /// 机箱文字：从盒顶按**真实字高**逐行排（名称 → 状态 → 工单·功率 → 操作员）。
        ///
        /// <para>两条硬规则（实机 P1/P8 + 「缩放时字体挤兑」的修复点）：</para>
        /// <list type="bullet">
        /// <item><b>行距 = 真实字高</b>，绝不使用"设计坐标 × k"的固定偏移（那会在 k≈1 时
        /// 出现 14/12/4px 的行距 &lt; 15px 字高 → 挤兑）；</item>
        /// <item><b>字体档随几何自适应</b>：机箱够大时名称升档 `Small`，但容量仍按真实行高算，
        /// 所以任何缩放下都不会重叠 —— 放不下的行不画（信息进 tooltip）。</item>
        /// </list>
        /// </summary>
        private static void DrawDeviceText(DrawContext ctx, FloorDeviceView device, Rect design, Color lamp)
        {
            Palette palette = ctx.Palette;
            Rect body = Design(ctx, design.x, design.y, WorkshopFloorLayout.MachineW, WorkshopFloorLayout.MachineH);

            GameFont nameFont = ctx.MachineNameAsSmall ? GameFont.Small : GameFont.Tiny;
            float nameLine = nameFont == GameFont.Small ? ctx.SmallLine : ctx.TinyLine;
            float available = body.height - 4f - (device.Progress01 >= 0f
                ? Mathf.Max(2f, WorkshopFloorLayout.ProgressH * ctx.K) + 3f : 0f);
            if (available < nameLine)
            {
                return; // 连名称都放不下 → 只留几何与状态灯
            }
            int rows = 1;
            if (available >= nameLine + ctx.TinyLine)
            {
                rows = 1 + Mathf.FloorToInt((available - nameLine) / ctx.TinyLine);
            }

            float padX = S(WorkshopFloorLayout.TextOffsetX, ctx.K);
            float textWidth = Mathf.Max(8f, body.width - padX - S(4f, ctx.K));
            float x = body.x + Mathf.Max(padX, ProductionDeviceCardArt.Draw(body, device.Card,
                (WorkshopFloorLayout.LampOffsetY + WorkshopFloorLayout.LampRadius) * ctx.K + 3f));
            textWidth = Mathf.Max(8f, body.xMax - x - S(4f, ctx.K));
            float y = body.y + 2f;

            DrawTextPx(new Rect(x, y, textWidth, nameLine),
                device.Name, nameFont, palette.TextStrong, TextAnchor.MiddleLeft);
            y += nameLine;

            int drawn = 1;
            if (drawn < rows && device.Card != null)
            {
                DrawTextPx(new Rect(x, y, textWidth, ctx.TinyLine),
                    device.Card.SpeedText, GameFont.Tiny, palette.TextStrong, TextAnchor.MiddleLeft);
                y += ctx.TinyLine;
                drawn++;
            }
            if (drawn < rows)
            {
                drawn++;
                DrawTextPx(new Rect(x, y, textWidth, ctx.TinyLine),
                    device.StateText, GameFont.Tiny, lamp, TextAnchor.MiddleLeft);
                y += ctx.TinyLine;
            }
            if (drawn < rows)
            {
                drawn++;
                string second = string.IsNullOrEmpty(device.PowerText)
                    ? device.SubText
                    : device.SubText + " · " + device.PowerText;
                DrawTextPx(new Rect(x, y, textWidth, ctx.TinyLine),
                    second, GameFont.Tiny, palette.TextDim, TextAnchor.MiddleLeft);
                y += ctx.TinyLine;
            }
            if (drawn < rows && !string.IsNullOrEmpty(device.OperatorText))
            {
                DrawTextPx(new Rect(x, y, textWidth, ctx.TinyLine),
                    device.OperatorText, GameFont.Tiny, palette.TextDim, TextAnchor.MiddleLeft);
            }
        }

        /// <summary>进度条：只在机箱底部还剩 ≥3px 时画（否则缩成一条线反而脏）。</summary>
        private static void DrawDeviceProgress(DrawContext ctx, FloorDeviceView device, Rect design)
        {
            if (device.Progress01 < 0f)
            {
                return;
            }
            float height = Mathf.Max(2f, WorkshopFloorLayout.ProgressH * ctx.K);
            float y = design.y + WorkshopFloorLayout.MachineH * 0.5f;
            Rect track = Design(ctx,
                design.x + WorkshopFloorLayout.ProgressInsetX, y + WorkshopFloorLayout.MachineH * 0.5f - 2f,
                WorkshopFloorLayout.MachineW - WorkshopFloorLayout.ProgressInsetX * 2f, height / Mathf.Max(0.01f, ctx.K));
            Widgets.DrawBoxSolid(track, ctx.Palette.ProgressTrack);
            Widgets.DrawBoxSolid(new Rect(track.x, track.y,
                track.width * Mathf.Clamp01(device.Progress01), track.height), ctx.Palette.ProgressFill);
        }

        // ---- L8 工人 ----

        private static void DrawWorkers(DrawContext ctx, WorkshopFloorSnapshot snapshot)
        {
            List<FloorWorkerView> workers = snapshot.Workers;
            if (workers == null || ctx.K < 0.6f)
            {
                return;
            }
            Palette palette = ctx.Palette;
            for (int i = 0; i < workers.Count; i++)
            {
                FloorWorkerView worker = workers[i];
                if (worker == null)
                {
                    continue;
                }
                Vector2 design = WorkshopFloorLayout.WorkerCenter(worker.Slot, worker.Index);
                Vector2 center = P(ctx, design.x, design.y);
                float radius = S(WorkshopFloorLayout.WorkerRadius, ctx.K);
                DrawDisc(center, radius, palette.WorkerFill);
                if (radius >= 7f)
                {
                    UIComponents.Label(new Rect(center.x - radius, center.y - radius * 0.6f,
                            radius * 2f, radius * 1.4f),
                        worker.Initial, GameFont.Tiny, palette.WorkerText, TextAnchor.MiddleCenter);
                }
            }
        }

        // ---- L9 图例：**已整组迁入 `#cmdbar` 的 `.lgbar`**（新预览 2026-09-11），
        //      画布内图例的绘制与其常量已删除（两边同时画会出现两份 LEGEND）。

        // ---- 小工具 ----

        private static Rect Design(DrawContext ctx, float x, float y, float w, float h)
        {
            return R(ctx.Canvas, ctx.K, x, y, w, h);
        }

        private static Vector2 P(DrawContext ctx, float x, float y)
        {
            return P(ctx.Canvas, ctx.K, x, y);
        }

        private static Color LampColor(string stateKey, Palette palette)
        {
            if (stateKey == EquipmentStateResolver.KeyRun)
            {
                return palette.LampRun;
            }
            if (stateKey == EquipmentStateResolver.KeyFault)
            {
                return palette.LampFault;
            }
            return stateKey == EquipmentStateResolver.KeyOff ? palette.LampOff : palette.LampIdle;
        }

        /// <summary>
        /// **无参** Keyed 文案（走 <see cref="UIComponents.Loc"/> 的语言级缓存）。
        ///
        /// <para>⚠ 2026-10-06（布局优化同批）：原实现是 <c>key.Translate().ToString()</c> —— 平面图与
        /// 两侧轨道的绘制期翻译点**全部**经本方法，每帧翻译并分配字符串（PERF-002）。
        /// 改走语言级缓存后，一处改动覆盖本类全部无参点。</para>
        /// <para>⚠ 带参版本 <c>T1</c>（定义在 <c>WorkshopFloorPlan.Routes.cs</c>，本 partial 类共用）
        /// **有意不缓存** —— 参数逐次不同。</para>
        /// </summary>
        private static string T(string key)
        {
            return UIComponents.Loc(key);
        }
    }
}
