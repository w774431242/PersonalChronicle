using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>
    /// 车间俯视平面图（制造中心 ② 主视图）—— **1:1 复刻预览框架** 的绘制组件。
    ///
    /// <para>框架（网格底纹 / 厂房外壳 / 柱网 / 母线管廊 / 通道 / 支持功能区 / 12 个车间格 /
    /// 图例 / 底注）是**固定图元**，逐一取自预览 <c>buildFloor()</c> 的设计坐标（见
    /// <see cref="WorkshopFloorLayout"/>），与地图无关；设备与人员由
    /// <see cref="WorkshopFloorSnapshot"/> 按"第几格第几台"对位填进去。</para>
    ///
    /// <para>本类是 partial：<br/>
    /// <c>WorkshopFloorPlan.cs</c> —— 入口、调色板、绘制原语（线/虚线/圆点/平铺/文字）；<br/>
    /// <c>WorkshopFloorPlan.Layers.cs</c> —— 各图层绘制。</para>
    ///
    /// <para>规矩：颜色一律取自 <see cref="UITheme"/> 令牌（ASSET-002 禁裸色值，例外仅"警示斜纹"这类
    /// 与主题无关的危险色，已在 <see cref="WorkshopFloorTextures"/> 里单独登记）；绘制路径零 new、
    /// 零 LINQ、零全库扫描（PERF-001）；所有文字用 <see cref="UIComponents.TruncateToWidth"/> 兜底。</para>
    /// </summary>
    internal static partial class WorkshopFloorPlan
    {
        /// <summary>
        /// 缩放范围：25% – 400%。下限放宽到 25%（大车间/小窗口时能把整图压到很小看全局），
        /// 上限提到 400%（几何放大后字体会升档，放得越大看得越清，见 DrawContext 的字体档）。
        /// </summary>
        internal const float MinZoom = 0.25f;
        internal const float MaxZoom = 4f;

        /// <summary>
        /// 视图态（缩放 / 平移）——**UI 瞬时状态**，由窗口持有，不入存档（GATE-UI）。
        /// Pan 单位 = 屏幕像素，坐标系原点在视口左上角。
        /// </summary>
        internal struct FloorPlanViewState
        {
            public float Zoom;
            public Vector2 Pan;

            /// <summary>
            /// 当前高亮的工艺路线下标；<c>-1</c> = 全部同等显示。
            /// 只有一条路线时无意义（不画切换条）。也属视图态，不入存档。
            /// </summary>
            public int RouteIndex;

            /// <summary>选中的车间格（-1 = 未选）。点击选中属 UI 瞬时态，不入存档（GATE-UI）。</summary>
            public int SelectedSlot;

            /// <summary>选中的设备（-1 = 未选）；同时 <see cref="SelectedSlot"/> 指向其所在格。</summary>
            public int SelectedDevice;

            /// <summary>本次按下-抬起之间是否发生过拖拽（用于区分"点击选中"与"拖拽平移"）。</summary>
            public bool Dragged;

            /// <summary>默认视图：100%、不偏移、高亮第一条路线、无选中。</summary>
            internal static FloorPlanViewState Default()
            {
                return new FloorPlanViewState
                {
                    Zoom = 1f,
                    Pan = Vector2.zero,
                    RouteIndex = 0,
                    SelectedSlot = -1,
                    SelectedDevice = -1
                };
            }
        }

        /// <summary>
        /// 绘制整块俯视图。返回**实际占用的高度**（视口等比，宽度吃满参数 <paramref name="rect"/>）。
        /// 调用方负责画区块标题/工具条与空态提示。
        /// </summary>
        /// <param name="rect">可用区域（左上角起算，宽度即平面图宽度）。</param>
        /// <param name="snapshot">数据快照；null 直接返回 0。</param>
        /// <param name="time">时间相位（秒），驱动故障灯闪烁。</param>
        /// <param name="view">视图态（缩放 / 横向平移就地修改）。</param>
        /// <param name="clickedDevice">本帧新选中的设备下标；无点击 = -1（调用方据此弹详情，D20）。</param>
        internal static float Draw(Rect rect, WorkshopFloorSnapshot snapshot, float time,
            ref FloorPlanViewState view, out int clickedDevice, float railGutter = 0f)
        {
            clickedDevice = -1;
            if (snapshot == null || snapshot.NoMap || rect.width <= 1f)
            {
                return 0f;
            }

            // 预览事实（LAND-001）：`.floorview{width:100%;overflow:hidden}` +
            // `.floorview svg{display:block;width:100%;height:auto}` ⇒ 平面图是**流式等比元素**，
            // 高度由宽度决定、随页面流动。100% 缩放时 = 浏览器里的 `width:100%` ⇒ 真 1:1。
            //
            // 之前那套「按视口 contain + 垂直居中」的机制（v1）正是黑屏/错位的根源：
            // 视口高被算成"可见区高 − 常数"，而 ② 之上还有顶栏/标题/①KPI/cmdbar，
            // 于是平面图被推到首屏之外。现已整体删除，改为与预览同构的流式布局。
            // rect = **视口**（预览 .floorview：flex:1 1 auto + min-height:0 + overflow:hidden）。
            // 内容按宽等比（svg width:100%;height:auto）铺开、左上角对齐、被视口裁剪；
            // 「适配」把整图缩进视口（.zoombar 语义）。
            float width = rect.width;
            float k = width * view.Zoom / WorkshopFloorLayout.CanvasW;
            float contentWidth = WorkshopFloorLayout.CanvasW * k;
            float contentHeight = WorkshopFloorLayout.CanvasH * k;
            // 2026-09-11 新预览：平面图居中、两侧 `.siderail` **并排**（不再覆盖画布）=>
            // 画布区按 railGutter 左右内缩，而**轨道仍画在外框**上。
            Rect canvasRect = railGutter > 0f
                ? new Rect(rect.x + railGutter, rect.y,
                    Mathf.Max(60f, rect.width - railGutter * 2f), rect.height)
                : rect;
            Rect frame = canvasRect;
            float maxPanX = Mathf.Max(0f, contentWidth - width);
            float maxPanY = Mathf.Max(0f, contentHeight - rect.height);
            HandleInput(frame, maxPanX, maxPanY, ref view);
            view.Pan = new Vector2(Mathf.Clamp(view.Pan.x, 0f, maxPanX),
                Mathf.Clamp(view.Pan.y, 0f, maxPanY));

            Palette palette = Palette.Create();

            // 裁剪到平面图占位框（= 预览 `.floorview overflow:hidden`）。
            // 100% 缩放时 contentWidth == width 且 pan == 0 ⇒ 裁剪不产生任何视觉差异。
            GUI.BeginClip(frame);
            try
            {
                // 裁剪区内坐标以框左上角为原点
                Rect canvas = new Rect(-view.Pan.x, -view.Pan.y, contentWidth, contentHeight);
                DrawContext ctx = DrawContext.Create(canvas, k, palette);
                ctx.SelectedSlot = view.SelectedSlot;
                ctx.SelectedDevice = view.SelectedDevice;

                Widgets.DrawBoxSolid(canvas, palette.Backdrop);
                // 预览 buildFloor()：`el('rect',{width:1500,height:600,fill:'url(#grid)'})`，
                // pattern 为 48×48、stroke .7、opacity .26 ⇒ 这里取 GridStep / GridOpacity 两常量。
                DrawTiled(canvas, WorkshopFloorTextures.Grid, WorkshopFloorLayout.GridStep,
                    UITheme.WithAlpha(palette.Gridline, WorkshopFloorLayout.GridOpacity));
                UIComponents.Border(canvas, palette.LegendLine);

                DrawShell(ctx);
                DrawColumnGrid(ctx);
                DrawBusbar(ctx);
                DrawAisles(ctx);
                DrawSupportZones(ctx);
                DrawBays(ctx, snapshot);
                DrawRoutes(ctx, snapshot, time, view.RouteIndex);
                DrawDevices(ctx, snapshot, time);
                // 基線 §5c-B/D13：S-00 线尾控制台绘制已删 —— 建筑 PC_ControlTerminal 本 mod 尚未落地，
                // 预览却给了会亮的灯（冒充数据）、文案硬编码「线尾控制台」≠ def label「管控终端」。
                DrawWorkers(ctx, snapshot);
                // 画布内图例**已删除**：新预览 2026-09-11 把整组图例迁入 `#cmdbar` 的 `.lgbar`
                // （基線 §0a「新增 .lgbar」），两边同时画会重复出现两份 LEGEND。
            }
            finally
            {
                GUI.EndClip();
            }

            // 两侧轨道（预览 .siderail{position:absolute;top:8px;bottom:8px;width:126px}）
            // 轨道贴外框（与画布并排）；旧长页路径无自由画布 palette ⇒ 设备库显示空态
            DrawRails(rect, snapshot, palette, null);

            // 点击选中（设备优先于格；拖拽过则不选中）
            HandleSelection(frame, snapshot, ref view, out clickedDevice);

            // 浮动缩放条**已移除**：PM 实机裁决把它移到顶栏 `#cmdbar`（见
            // `MasterControlTabWindow.DrawCmdBarZoom`）—— 浮层会压住平面图内容，且两处控件重复。
            // 预览原为 `.zoombar{right:16;bottom:18}`，此项为本工程**有意偏差**（已登记）。

            // 平面图边框（画在裁剪外，缩放后边框不被裁掉）
            UIComponents.Border(frame, palette.LegendLine);

            // 警示斜纹条**不在这里画**：预览里 .hazard 位于 .ftabs 之下（面板最底部），
            // 由 MasterControlTabWindow 在 tab 栏之后调用 DrawHazardStrip 绘制（层级顺序 1:1）。
            return frame.height;
        }

        /// <summary>
        /// 平面图占用高度 = 设计高 × (框宽 × 缩放 / 设计宽)。
        /// 与调用方的高度估算**必须同式**（改这里就要改调用方）。
        /// </summary>
        /// <summary>
        /// 面板底部警示斜纹条（预览 <c>.hazard{height:8px}</c>）：画在 tab 栏之下，
        /// 由调用方在正确层级位置调用（本类不自动画，以复现预览顺序 panes → .ftabs → .hazard）。
        /// </summary>
        internal static void DrawHazardStrip(Rect rect)
        {
            DrawTiled(rect, WorkshopFloorTextures.Hazard, WorkshopFloorLayout.HazardStep,
                FloorPalette.HazardTintStrong);
        }

        internal static float PlanHeight(float width, float zoom)
        {
            return WorkshopFloorLayout.CanvasH
                * (width * Mathf.Max(0.05f, zoom) / WorkshopFloorLayout.CanvasW);
        }

        /// <summary>
        /// 输入：滚轮缩放（以鼠标横向位置为锚点）+ 左键/中键拖拽**横向**平移（纵向交给页面滚动）。
        /// 仅在鼠标位于平面图框内时响应。
        /// </summary>
        private static void HandleInput(Rect frame, float maxPanX, float maxPanY, ref FloorPlanViewState view)
        {
            Event current = Event.current;
            if (current == null || !Mouse.IsOver(frame))
            {
                return;
            }

            if (current.type == EventType.ScrollWheel && !Mathf.Approximately(current.delta.y, 0f))
            {
                // 滚轮向下 = 缩小（delta.y > 0），一次约 10%
                float factor = current.delta.y > 0f ? 0.9f : 1.111f;
                float previous = view.Zoom;
                float next = Mathf.Clamp(previous * factor, MinZoom, MaxZoom);
                if (!Mathf.Approximately(next, previous))
                {
                    // 让鼠标下的那一点在缩放前后保持不动
                    float anchorX = (current.mousePosition.x - frame.x + view.Pan.x) / Mathf.Max(0.01f, previous);
                    view.Zoom = next;
                    view.Pan = new Vector2(
                        anchorX * next - (current.mousePosition.x - frame.x),
                        Mathf.Clamp(view.Pan.y, 0f, maxPanY));
                }
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0)
            {
                view.Dragged = false;
                return;
            }

            if (current.type == EventType.MouseDrag && (current.button == 0 || current.button == 2))
            {
                if (maxPanX > 0f || maxPanY > 0f)
                {
                    view.Pan = new Vector2(
                        Mathf.Clamp(view.Pan.x - current.delta.x, 0f, maxPanX),
                        Mathf.Clamp(view.Pan.y - current.delta.y, 0f, maxPanY));
                }
                view.Dragged = true;
                current.Use();
            }
        }

        /// <summary>
        /// 点击选中（预览 <c>selZone()</c> / <c>selMachine()</c> 的等价物）：
        /// **设备优先于格**，点空白清空。仅在"按下-抬起之间没拖动过"时生效，
        /// 这样左键拖拽平移不会误触选中（预览用 <c>_panned</c> 标记做同样的事）。
        /// </summary>
        private static void HandleSelection(Rect viewport, WorkshopFloorSnapshot snapshot,
            ref FloorPlanViewState view, out int clickedDevice, float railGutter = 0f)
        {
            clickedDevice = -1;
            Event current = Event.current;
            if (current == null || snapshot == null)
            {
                return;
            }
            if (current.type == EventType.MouseUp && current.button == 0 && !view.Dragged
                && Mouse.IsOver(viewport))
            {
                Vector2 mouse = current.mousePosition;

                // 1) 设备（后画的在上，倒序遍历）
                List<FloorDeviceView> devices = snapshot.Devices;
                if (devices != null)
                {
                    for (int i = devices.Count - 1; i >= 0; i--)
                    {
                        FloorDeviceView device = devices[i];
                        if (device == null)
                        {
                            continue;
                        }
                        Rect rect = MachineScreenRect(viewport, view, device.Slot, device.Index);
                        if (rect.Contains(mouse))
                        {
                            view.SelectedDevice = i;
                            view.SelectedSlot = device.Slot;
                            // 预览 selMachine() → renderDeviceDetail() + openModal()：
                            // 选中设备即弹详情窗（D20）。
                            clickedDevice = i;
                            current.Use();
                            return;
                        }
                    }
                }

                // 2) 车间格
                for (int slot = 0; slot < WorkshopFloorSnapshot.BaySlots; slot++)
                {
                    Rect rect = BayScreenRect(viewport, view, slot);
                    if (rect.Contains(mouse))
                    {
                        view.SelectedSlot = slot;
                        view.SelectedDevice = -1;
                        current.Use();
                        return;
                    }
                }

                // 3) 点空白清空
                view.SelectedSlot = -1;
                view.SelectedDevice = -1;
            }
        }

        /// <summary>
        /// 格 / 设备的**屏幕矩形**（供命中测试；与 <see cref="Draw"/> 同一套几何换算：
        /// 画布左上角对齐框左上角，缩放只作用于设计坐标，横向再减平移量）。
        /// </summary>
        internal static Rect BayScreenRect(Rect frame, FloorPlanViewState view, int slot)
        {
            return DesignToScreen(frame, view, WorkshopFloorLayout.BayRect(slot));
        }

        internal static Rect MachineScreenRect(Rect frame, FloorPlanViewState view, int slot, int index)
        {
            return DesignToScreen(frame, view, WorkshopFloorLayout.MachineRect(slot, index));
        }

        private static Rect DesignToScreen(Rect frame, FloorPlanViewState view, Rect design)
        {
            float k = CurrentScale(frame.width, view);
            return new Rect(
                frame.x - view.Pan.x + design.x * k,
                frame.y + design.y * k,
                design.width * k,
                design.height * k);
        }

        /// <summary>当前每设计单位对应的像素数（= fit × zoom）。</summary>
        /// <summary>
        /// 当前缩放系数（设计单位 → 屏幕像素）：<c>k = 框宽 × zoom / 设计宽</c>。
        /// 与 <see cref="Draw"/> 同式；100% 缩放时 = 浏览器 `width:100%` 的比例。
        /// </summary>
        internal static float CurrentScale(float width, FloorPlanViewState view)
        {
            return width * view.Zoom / WorkshopFloorLayout.CanvasW;
        }

        /// <summary>按倍率缩放（工具条按钮用；横向以内容中心为锚点）。</summary>
        internal static void ZoomBy(ref FloorPlanViewState view, float factor, float width)
        {
            float previous = view.Zoom;
            float next = Mathf.Clamp(previous * factor, MinZoom, MaxZoom);
            if (Mathf.Approximately(next, previous))
            {
                return;
            }

            // 内容中心的屏幕位置在缩放前后保持不动（横向只有一条轴，纵向由页面滚动接管）
            float scale = next / previous;
            float centerX = width * 0.5f;
            view.Pan = new Vector2((view.Pan.x + centerX) * scale - centerX, 0f);
            view.Zoom = next;
        }

        private static float Measure(string text)
        {
            GameFont previous = Text.Font;
            Text.Font = GameFont.Tiny;
            float width = Text.CalcSize(text).x;
            Text.Font = previous;
            return width;
        }

        /// <summary>回到 100% 且不偏移。</summary>
        /// <summary>「适配」：把整图（1500×780 设计坐标）缩进当前视口 —— 预览 <c>.zoombar</c> 的语义。</summary>
        internal static void ResetView(ref FloorPlanViewState view, float width, float height)
        {
            float fit = Mathf.Min(width / WorkshopFloorLayout.CanvasW,
                height / WorkshopFloorLayout.CanvasH);
            view.Zoom = Mathf.Clamp(fit, MinZoom, MaxZoom);
            view.Pan = Vector2.zero;
        }

        // ---- 调色板：预览 CSS 变量 → UITheme 令牌（唯一映射点，禁止散落色值）----

        private struct Palette
        {
            internal Color Backdrop;
            internal Color Slab;
            internal Color Wall;
            internal Color WallInner;
            internal Color Column;
            internal Color Gridline;
            internal Color Busbar;
            internal Color AisleFill;
            internal Color AisleLine;
            internal Color SupFill;
            internal Color SupLine;
            internal Color BayFill;
            internal Color BayFillHighlight;
            internal Color BayFillSelected;
            internal Color BayCorner;
            internal Color BayStroke;
            internal Color BayHover;
            internal Color BayBadge;
            internal Color BayBadgeText;
            internal Color MachineBody;
            internal Color MachineFound;
            internal Color MachineStroke;
            internal Color MachineStrokeHighlight;
            internal Color Bolt;
            internal Color LampRun;
            internal Color LampIdle;
            internal Color LampFault;
            internal Color LampOff;
            /// <summary>预览 <c>--warn</c>：用于 <c>.rbar .ss</c> 安全库存刻度线与告警态。</summary>
            internal Color LampWarn;
            internal Color ProgressTrack;
            internal Color ProgressFill;
            internal Color LegendFill;
            internal Color LegendLine;
            internal Color RailFill;
            internal Color RailTitle;
            internal Color TextStrong;
            internal Color TextDim;
            internal Color WorkerFill;
            internal Color WorkerSupervisor;
            internal Color WorkerText;

            internal static Palette Create()
            {
                return new Palette
                {
                    Backdrop = UITheme.Window,
                    Slab = FloorPalette.Slab,
                    Wall = UITheme.Border,
                    WallInner = UITheme.WithAlpha(UITheme.BorderSoft, 0.8f),
                    Column = UITheme.PanelRaised,
                    Gridline = UITheme.BorderHair,
                    Busbar = UITheme.WithAlpha(UITheme.Info, 0.45f),
                    AisleFill = UITheme.WithAlpha(UITheme.Accent, 0.08f),
                    AisleLine = UITheme.WithAlpha(UITheme.Accent, 0.75f),
                    SupFill = UITheme.WithAlpha(UITheme.BorderSoft, 0.12f),
                    SupLine = UITheme.BorderSoft,
                    BayFill = UITheme.WithAlpha(UITheme.Info, 0.06f),
                    BayFillHighlight = UITheme.WithAlpha(UITheme.Info, 0.12f),
                    // 预览 .zone.sel{fill:amber 8%}
                    BayFillSelected = UITheme.WithAlpha(UITheme.Accent, 0.14f),
                    BayCorner = UITheme.Border,
                    // 预览 .zone{stroke:var(--line2);dasharray:7 4} / .zone:hover{stroke:var(--amber)}
                    BayStroke = UITheme.WithAlpha(UITheme.BorderSoft, 0.9f),
                    BayHover = UITheme.Accent,
                    BayBadge = UITheme.Info,
                    BayBadgeText = UITheme.WithAlpha(UITheme.Window, 0.95f),
                    MachineBody = UITheme.PanelRaised,
                    MachineFound = UITheme.WithAlpha(UITheme.Card, 0.7f),
                    MachineStroke = UITheme.BorderSoft,
                    MachineStrokeHighlight = UITheme.Info,
                    Bolt = UITheme.Dim,
                    LampRun = UITheme.Alive,
                    LampIdle = UITheme.Dim,
                    LampFault = UITheme.Dead,
                    LampOff = UITheme.WithAlpha(UITheme.Dim, 0.5f),
                    LampWarn = UITheme.Threat,
                    ProgressTrack = UITheme.BorderHair,
                    ProgressFill = UITheme.Alive,
                    LegendFill = FloorPalette.LegendFill,
                    LegendLine = UITheme.BorderSoft,
                    // 预览 .siderail：linear-gradient(180deg,rgba(9,13,17,.95),rgba(9,13,17,.84))
                    RailFill = FloorPalette.RailFill,
                    RailTitle = UITheme.Accent,
                    TextStrong = UITheme.Text,
                    TextDim = UITheme.Dim,
                    WorkerFill = UITheme.Accent,
                    WorkerSupervisor = UITheme.Info,
                    WorkerText = UITheme.WithAlpha(UITheme.Window, 0.95f)
                };
            }
        }

        // ---- 设计坐标 → 屏幕坐标 ----

        private static Rect R(Rect canvas, float k, float x, float y, float w, float h)
        {
            return new Rect(canvas.x + x * k, canvas.y + y * k, w * k, h * k);
        }

        private static Vector2 P(Rect canvas, float k, float x, float y)
        {
            return new Vector2(canvas.x + x * k, canvas.y + y * k);
        }

        private static float S(float designLength, float k)
        {
            return Mathf.Max(1f, designLength * k);
        }

        // ---- 绘制原语 ----

        /// <summary>平铺贴图（<c>GUI.DrawTextureWithTexCoords</c> + Repeat；<paramref name="tint"/> 含透明度）。</summary>
        private static void DrawTiled(Rect rect, Texture2D tile, float tileSize, Color tint)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }
            Color prev = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(rect, tile,
                new Rect(0f, 0f, rect.width / tileSize, rect.height / tileSize));
            GUI.color = prev;
        }

        /// <summary>实心圆（白盘贴图染色）。半径单位 = 屏幕像素。</summary>
        private static void DrawDisc(Vector2 center, float radius, Color color)
        {
            if (radius <= 0.5f)
            {
                return;
            }
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f),
                WorkshopFloorTextures.Disc);
            GUI.color = prev;
        }

        /// <summary>虚线（预览 <c>stroke-dasharray</c>）：按 dash/gap 逐段画。</summary>
        private static void DrawDashedLine(Vector2 from, Vector2 to, Color color, float width,
            float dash, float gap)
        {
            float total = Vector2.Distance(from, to);
            if (total <= 0.01f || dash <= 0.01f)
            {
                return;
            }
            Vector2 dir = (to - from) / total;
            float step = dash + Mathf.Max(0.5f, gap);
            for (float offset = 0f; offset < total; offset += step)
            {
                float segment = Mathf.Min(dash, total - offset);
                if (segment <= 0.01f)
                {
                    break;
                }
                Widgets.DrawLine(from + dir * offset, from + dir * (offset + segment), color, width);
            }
        }

        /// <summary>虚线边框（预览 <c>.zone{stroke-dasharray:7 4}</c>）：4 条边各自分段。</summary>
        private static void DrawDashedFrame(Rect rect, Color color, float width, float dash, float gap)
        {
            DrawDashedLine(new Vector2(rect.x, rect.y), new Vector2(rect.xMax, rect.y), color, width, dash, gap);
            DrawDashedLine(new Vector2(rect.x, rect.yMax), new Vector2(rect.xMax, rect.yMax), color, width, dash, gap);
            DrawDashedLine(new Vector2(rect.x, rect.y), new Vector2(rect.x, rect.yMax), color, width, dash, gap);
            DrawDashedLine(new Vector2(rect.xMax, rect.y), new Vector2(rect.xMax, rect.yMax), color, width, dash, gap);
        }

        /// <summary>直角边框（预览 <c>rect stroke</c> 的 4 条边）。</summary>
        private static void DrawFrame(Rect rect, Color color, float width)
        {
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, width), color);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, width, rect.height), color);
            Widgets.DrawBoxSolid(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        /// <summary>四角 L 形标记（预览 <c>zonecorner</c>，臂长 20 设计单位）。</summary>
        private static void DrawCornerMarks(Rect rect, float k, Color color, float width)
        {
            float arm = S(WorkshopFloorLayout.CornerArm, k);
            float w = Mathf.Max(1f, width);
            // 左上 / 右上 / 左下 / 右下，各两条线
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, arm, w), color);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, w, arm), color);
            Widgets.DrawBoxSolid(new Rect(rect.xMax - arm, rect.y, arm, w), color);
            Widgets.DrawBoxSolid(new Rect(rect.xMax - w, rect.y, w, arm), color);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - w, arm, w), color);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - arm, w, arm), color);
            Widgets.DrawBoxSolid(new Rect(rect.xMax - arm, rect.yMax - w, arm, w), color);
            Widgets.DrawBoxSolid(new Rect(rect.xMax - w, rect.yMax - arm, w, arm), color);
        }

        /// <summary>设计坐标的一行文字矩形：<paramref name="centerY"/> 是**行中心**，行高取真实字高。</summary>
        private static Rect DesignLine(DrawContext ctx, float x, float centerY, float width, float lineHeight)
        {
            return new Rect(
                ctx.Canvas.x + x * ctx.K,
                ctx.Canvas.y + centerY * ctx.K - lineHeight * 0.5f,
                Mathf.Max(1f, width * ctx.K),
                lineHeight);
        }

        private static float LineOf(DrawContext ctx, GameFont font)
        {
            return font == GameFont.Small ? ctx.SmallLine : ctx.TinyLine;
        }

        /// <summary>文字（左对齐；调用方给设计坐标，y = 行中心）。</summary>
        private static void DrawText(DrawContext ctx, float x, float centerY, float maxWidth,
            string text, GameFont font, Color color)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 1f)
            {
                return;
            }
            Rect rect = DesignLine(ctx, x, centerY, maxWidth, LineOf(ctx, font));
            UIComponents.Label(rect, UIComponents.TruncateToWidth(text, rect.width, font),
                font, color, TextAnchor.MiddleLeft);
        }

        /// <summary>右对齐文字（设计坐标里给"右边界"）。</summary>
        private static void DrawTextRight(DrawContext ctx, float rightX, float centerY, float maxWidth,
            string text, GameFont font, Color color)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 1f)
            {
                return;
            }
            float line = LineOf(ctx, font);
            Rect rect = DesignLine(ctx, rightX - maxWidth, centerY, maxWidth, line);
            UIComponents.Label(rect, UIComponents.TruncateToWidth(text, rect.width, font),
                font, color, TextAnchor.MiddleRight);
        }

        /// <summary>居中文字（设计坐标）。</summary>
        private static void DrawTextCentered(DrawContext ctx, float centerX, float centerY, float maxWidth,
            string text, GameFont font, Color color)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 1f)
            {
                return;
            }
            float line = LineOf(ctx, font);
            Rect rect = DesignLine(ctx, centerX - maxWidth * 0.5f, centerY, maxWidth, line);
            UIComponents.Label(rect, UIComponents.TruncateToWidth(text, rect.width, font),
                font, color, TextAnchor.MiddleCenter);
        }

        /// <summary>
        /// 屏幕像素直排（**堆叠布局专用**：行距必须用真实字高的像素值，
        /// 不能拿设计坐标乘 k —— 那正是实机叠字的根因）。
        /// </summary>
        private static void DrawTextPx(Rect rect, string text, GameFont font, Color color,
            TextAnchor anchor)
        {
            if (string.IsNullOrEmpty(text) || rect.width <= 1f)
            {
                return;
            }
            UIComponents.Label(rect, UIComponents.TruncateToWidth(text, rect.width, font),
                font, color, anchor);
        }

        /// <summary>
        /// 文本像素宽度（用于**流式布局**：预览 <c>.rqual{flex-wrap:wrap}</c> 之类按内容宽度排布，
        /// 必须真实量宽才能决定换行点；字体固定像素、不随 k 缩放，故不受缩放影响）。
        /// </summary>
        private static float MeasureWidth(string text, GameFont font)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }
            GameFont previous = Text.Font;
            Text.Font = font;
            float width = Text.CalcSize(text).x;
            Text.Font = previous;
            return width;
        }
    }
}
