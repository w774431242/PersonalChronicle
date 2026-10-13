using System;
using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Application;
using PersonalChronicle.Application.Workshop;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>
    /// **自由编排画布**（新版预览 `#fp-layout`「自由编排 · 托盘 / 几何指标」的落地）。
    ///
    /// <para>范式（实测自新版预览）：画布**原点在中央 (0,0)**、+X→右 / +Y↓下、**无格位、无吸附、
    /// 无边界**（内容超出即扩框）；车间初始在**托盘**里，玩家拖/点放到画布上。预览明确写着
    /// 「车间位置是**视图编排**，不移动游戏世界里的 Zone/建筑」——因此这里只维护**视图状态**。</para>
    ///
    /// <para><b>与只读俯视的关系</b>（PM 裁决 A）：只读俯视（<see cref="WorkshopFloorPlan"/>）继续展示
    /// 真实的固定平面；本画布是**同一份车间数据的另一种视图**（自由摆放），两者互不替代。</para>
    ///
    /// <para><b>托盘数据源</b>：已划出的 <see cref="Zone_Workshop"/>（真实数据，`zone.label`）。
    /// 未划车间时画布为空并提示先去游戏里划区 —— 不编造"12 个工序车间"这类预览样例值（LAND-009）。</para>
    ///
    /// <para><b>持久化</b>：M1 为**会话内视图状态**（不入存档），界面明示；是否入档待 PM 裁决。</para>
    /// </summary>
    internal static partial class WorkshopLayoutCanvas
    {
        // ---- 几何（设计单位；与预览一致的 ± 范围由刻度决定）----
        internal const float DesignExtent = 450f;      // ±450（900 宽，与新版 viewBox 一致）
        internal const float AxisStep = 100f;          // 刻度间隔

        /// <summary>缩放范围（用户实机反馈「25% 下限不够」⇒ 放宽到 8%；上限仍 400%）。</summary>
        internal const float MinZoom = 0.08f;
        internal const float MaxZoom = 4f;
        internal const float BlockWidth = 200f;        // 车间块设计宽
        internal const float BlockHeight = 120f;       // 车间块设计高
        /// <summary>Save 结构预览的 schema 版本（对应预览 `WorkshopLayoutPlan schemaVersion`）。</summary>
        internal const int LayoutSchemaVersion = 1;

        private const float PaletteHeight = 24f;
        private const float PaletteChipHeight = 20f;
        private const float PaletteChipGap = 4f;

        /// <summary>画布视图态（缩放/平移/选中）——会话内状态，不入存档。</summary>
        internal struct CanvasState
        {
            public float Zoom;
            public Vector2 Pan;

            /// <summary>托盘里被选中、待放到画布的车间**下标**（-1 = 未选中）。</summary>
            public int PickedIndex;

            /// <summary>
            /// 已放到画布上的车间，承载的是 **<c>Zone.ID</c>**（稳定身份），**不是托盘下标**。
            ///
            /// <para>⚠ 2026-10-02 审计 P1-3：原实现存托盘下标，而托盘来自
            /// <c>AllWorkshops</c>（按 <c>zoneManager.AllZones</c> 原始序 yield，**无排序**）
            /// ⇒ 玩家增删车间后下标整体位移，<c>MirrorLayoutToSave</c> 会把坐标写到**别的车间**
            /// 并把原车间判为"已收回"删档。存档侧本来就用 ZoneId ⇒ 内存侧对齐存档侧。</para>
            /// </summary>
            public List<int> PlacedZoneIds;
            public List<Vector2> PlacedPos;

            // ---- M2 ----
            /// <summary>正在拖拽的已放置项下标（-1 = 未拖拽）。</summary>
            public int DragIndex;

            /// <summary>坐标网格开关（预览的「坐标网格」，默认关 = 纯自由画布）。</summary>
            public bool ShowGrid;

            /// <summary>叠加标注开关（预览的 `DensitySwitch`：叠加工序号 / 坐标）。</summary>
            public bool ShowAnnotations;

            /// <summary>上一步快照（撤销用；会话内，不入存档）。</summary>
            public List<int> UndoOrder;
            public List<Vector2> UndoPos;

            /// <summary>
            /// 撤销快照**是否有效**（= 存在一份可回退的状态）。
            ///
            /// <para>⚠ 2026-10-02 审计 P0-3：原实现用
            /// <c>UndoOrder.Count &gt; 0 || PlacedOrder.Count &gt; 0</c> 当撤销门禁，
            /// 而"从存档加载"的布局只填 <c>PlacedOrder</c>、<c>UndoOrder</c> 恒空
            /// ⇒ 点一次撤销即把三项全部置空 + 触发差集删档。改为**显式有效位**：
            /// 只有 <see cref="WorkshopLayoutCanvas.Snapshot"/> 真正拍过快照才为 true
            /// （读档后由宿主 <c>EstablishBaseline</c> 立即拍一次基线）。</para>
            /// </summary>
            public bool UndoValid;

            /// <summary>最近一次操作文案（用于底部提示）。</summary>
            public string LastAction;

            /// <summary>
            /// 闪烁定位目标（托盘下标；-1 = 无）—— 预览 `FlashOverlay` 语义
            /// （`#cline` 点 chip 时让对应车间块闪一圈）。
            /// </summary>
            public int FlashIndex;

            /// <summary>
            /// 闪烁截止时刻（**BCL 时钟** `DateTime.UtcNow.Ticks`）。
            /// 铁律：绘制路径禁用 `Time.realtimeSinceStartup`（ECall 隔离要求），一律 BCL 时钟。
            /// </summary>
            public long FlashUntilTicks;

            // ---- 对齐吸附（CHG-MFG-ALIGN-001 · M2）----
            //
            // ⚠ struct 别名铁律：下面两个 `List<T>` 在 `CanvasState` 被按值拷贝后**仍共享同一实例**。
            //   因此只允许原地 `Clear()` + `Add()`；**任何位置都不得**写成
            //   `state.AlignBuf = new List<...>()` —— 那是在值拷贝上赋值，写入会静默丢失。
            //   （本 struct 同时存在按值传参与 `in` 传参两种调用形态，两者都受此约束。）

            /// <summary>
            /// 对齐吸附总开关（编辑器偏好；拖拽时还需按住 Shift 才生效）。
            /// 会话内状态，**不入存档**。
            /// </summary>
            public bool SnapEnabled;

            /// <summary>邻居投影的复用缓冲（每帧 Clear + Add，不重新分配）。</summary>
            public List<CanvasAlignRect> AlignBuf;

            /// <summary>本帧命中的参考线（≤2 条：一垂直 + 一水平），供 overlay 绘制。</summary>
            public List<CanvasAlignGuide> AlignGuides;

            /// <summary>
            /// 设备卡按下时的鼠标位置（屏幕坐标）—— 供 MouseUp 判「点击 vs 拖拽」。
            /// </summary>
            /// <remarks>
            /// BUG-2026-0001 · S-1：编排态卡体不再消费 <c>MouseDown</c>，拖拽起点改由
            /// <c>HandleDeviceCardDrag</c> 独占 ⇒ 需记住按下位置才能在松手时区分
            /// 「点击开详情」与「拖动移位」。放本 struct 而非新静态字段 —— 它已是画布瞬时态归宿。
            /// </remarks>
            public Vector2 CardPressPos;

            internal static CanvasState Create()
            {
                return new CanvasState
                {
                    Zoom = 1f,
                    Pan = Vector2.zero,
                    PickedIndex = -1,
                    DragIndex = -1,
                    PlacedZoneIds = new List<int>(),
                    PlacedPos = new List<Vector2>(),
                    UndoOrder = new List<int>(),
                    UndoPos = new List<Vector2>(),
                    UndoValid = false,
                    LastAction = string.Empty,
                    FlashIndex = -1,
                    FlashUntilTicks = 0L,
                    // 吸附默认**开**（Shift 是附加手势，不是隐藏功能）；两个缓冲预建空表。
                    SnapEnabled = true,
                    AlignBuf = new List<CanvasAlignRect>(),
                    AlignGuides = new List<CanvasAlignGuide>(),
                    CardPressPos = Vector2.zero,
                };
            }
        }

        // 注：「车间托盘」绘制模型（DeviceEntry / PaletteEntry）与 2 秒扫描缓存已迁移至
        // Application.ReadModels.WorkshopPaletteModels + Automation.Workshop.WorkshopPaletteAccessBinder，
        // UI 经 Application.Workshop.WorkshopPaletteAccess 受控契约取数（GOV-005；CHG-MFG-AUDIT-001 · A-1）。
        // ⚠ 2026-10-03 订正：本块原文写「本文件不再持有任何 Automation/Domain 引用」是**错的** ——
        //   本文件 `:7` 仍有 `using PersonalChronicle.Domain;`（且 UI→Domain 是本工程既有普遍范式，
        //   矩阵 Presentation→Core = ✓）。准确表述是：**Automation 侧直读已清零**；
        //   Domain 仍承载**离线纯函数类型**（如 WorkshopCanvasAlignment，由 NUnit 直接调用）。
        //   同行原引的「ARC-011 缓解」规则号也误引 —— 静态可变状态是 **ARC-008**（EXC-2026-011 已更正）。

        /// <summary>
        /// 绘制编排页（托盘 + 画布 + 几何指标）。返回占用高度。
        ///
        /// <para>布局：`托盘`（一行 chips）→ `坐标轴画布`（吃剩余高度）→ `几何指标` 一行。</para>
        /// </summary>
        internal static float Draw(Rect rect, List<PaletteEntry> palette, ref CanvasState state,
            float time)
        {
            if (rect.width < 40f || rect.height < 60f)
            {
                return 0f;
            }

            float y = DrawPalette(new Rect(rect.x, rect.y, rect.width, PaletteHeight), palette,
                ref state);
            y += 4f;

            float metricsHeight = 20f;
            float canvasHeight = Mathf.Max(60f, rect.yMax - y - metricsHeight - 4f);
            Rect canvas = new Rect(rect.x, y, rect.width, canvasHeight);
            DrawCanvasBody(canvas, palette, ref state, time, true, out _);
            y = canvas.yMax + 4f;

            DrawMetricsRow(new Rect(rect.x, y, rect.width, metricsHeight), palette, ref state);
            return canvas.yMax - rect.y + 4f + metricsHeight;
        }

        /// <summary>
        /// 闪烁定位（预览 `FlashOverlay`）：让指定托盘下标的车间块闪一圈。
        /// 供**画面外**的控件调用（如 ② 外壳 `#cline` 的工序链 chip 点击）。
        /// </summary>
        internal static void Flash(ref CanvasState state, int paletteIndex)
        {
            state.FlashIndex = paletteIndex;
            state.FlashUntilTicks = DateTime.UtcNow.Ticks + TimeSpan.TicksPerMillisecond * 1900L;
        }

        /// <summary>闪烁是否仍在进行（BCL 时钟，绘制路径安全）。</summary>
        private static bool IsFlashing(in CanvasState state)
        {
            return state.FlashIndex >= 0 && DateTime.UtcNow.Ticks < state.FlashUntilTicks;
        }

        // ---- 托盘 ----

        /// <summary>
        /// 托盘行（预览 `#lbar .ptray` 与编排页签的「托盘 · 待放置」同源）。
        /// <c>internal</c>：供 ② 外壳 `#lbar` 直接复用同一实现（避免两套托盘逻辑）。
        /// </summary>
        internal static float DrawPalette(Rect rect, List<PaletteEntry> palette, ref CanvasState state)
        {
            UIComponents.Rule(new Rect(rect.x, rect.yMax, rect.width, 1f), UITheme.BorderSoft);
            float x = rect.x;
            string cap = Translate("PersonalChronicle.UI.Layout.Palette");
            float capWidth = Measure(cap) + 8f;
            UIComponents.Label(new Rect(x, rect.y, capWidth, rect.height), cap,
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            x += capWidth;

            if (palette == null || palette.Count == 0)
            {
                UIComponents.Label(new Rect(x, rect.y, rect.width - (x - rect.x), rect.height),
                    Translate("PersonalChronicle.UI.Layout.NoWorkshop"), GameFont.Tiny,
                    UITheme.Muted, TextAnchor.MiddleLeft);
                return rect.yMax + 3f;
            }

            for (int i = 0; i < palette.Count; i++)
            {
                bool placed = IsPlaced(state, palette[i].ZoneId);
                string label = palette[i].Label + (placed ? " ×" : string.Empty);
                float width = Mathf.Min(Measure(label) + 20f, rect.width * 0.22f);
                if (x + width > rect.xMax)
                {
                    break;
                }
                Rect chip = new Rect(x, rect.y + 1f, width, PaletteChipHeight);
                bool picked = state.PickedIndex == i;
                Color edge = placed ? UITheme.Dim : (picked ? UITheme.Accent : UITheme.BorderSoft);
                if (picked)
                {
                    Widgets.DrawBoxSolid(chip, UITheme.WithAlpha(UITheme.Accent, 0.18f));
                }
                else
                {
                    Widgets.DrawHighlightIfMouseover(chip);
                }
                UIComponents.Border(chip, edge);
                UIComponents.Label(new Rect(chip.x + 3f, chip.y, chip.width - 6f, chip.height),
                    UIComponents.TruncateToWidth(label, chip.width - 6f, GameFont.Tiny),
                    GameFont.Tiny, placed ? UITheme.Dim : UITheme.Text, TextAnchor.MiddleCenter);
                if (Widgets.ButtonInvisible(chip))
                {
                    if (placed)
                    {
                        // 已放置 ⇒ 点托盘收起（预览的「点 × 收回」）
                        Unplace(ref state, palette[i].ZoneId);
                    }
                    else
                    {
                        state.PickedIndex = picked ? -1 : i;
                    }
                }
                TooltipHandler.TipRegion(chip, Translate(placed
                    ? "PersonalChronicle.UI.Layout.ChipPlacedTip"
                    : "PersonalChronicle.UI.Layout.ChipTip"));
                x += width + PaletteChipGap;
            }
            return rect.yMax + 3f;
        }

        // ---- 画布（坐标轴 + 空态 + 已放置车间块）----

        /// <summary>
        /// 画布体（坐标轴 + 空态 + 已放置车间卡 + 可选交互）。
        ///
        /// <para><b>⭐ 这是 ② 主画布的**唯一**绘制路径</b>（2026-09-12 统一）：查看态与编排态画的是
        /// **同一块画布**，差别只在 <paramref name="interactive"/> —— 预览的 `layoutMode` 也只改交互，
        /// 不改画布内容。此前查看态走 `WorkshopFloorPlan`（固定 12 格厂房）是**两块不同的画布**，
        /// 与预览不符（用户实机指正）。</para>
        /// </summary>
        /// <param name="interactive">true = 编排态（可拖拽/落点/收回/画把手）；false = 查看态（只读）。</param>
        internal static void DrawCanvasBody(Rect rect, List<PaletteEntry> palette,
            ref CanvasState state, float time, bool interactive, out string clickedDeviceStableId)
        {
            clickedDeviceStableId = null;
            EnsureSessionScope();
            Widgets.DrawBoxSolid(rect, UITheme.WithAlpha(UITheme.Text, 0.03f));
            UIComponents.Border(rect, UITheme.BorderSoft);
            Widgets.DrawHighlightIfMouseover(rect);

            float k = Scale(rect, state);
            Vector2 center = new Vector2(rect.center.x + state.Pan.x, rect.center.y + state.Pan.y);
            // 本帧光标（GUI 空间）。⚠ 统一用 `Event.current.mousePosition`：
            //   `UI.MousePositionOnUIInverted` 是屏幕空间换算值，在窗口内两者不同源
            //   （原实现混用两套 ⇒ ghost 首帧反向跳位、缩放行读数与实际落点不一致）。
            Vector2 mouseDesign = Event.current != null ? Event.current.mousePosition : Vector2.zero;

            // 坐标网格（预览的「坐标网格」开关，默认关）
            if (state.ShowGrid)
            {
                DrawGrid(rect, k, center);
            }

            // 滚轮缩放（两态都可缩放；预览 L12：镜头锁原点 ⇒ 绕**原点**展开，不以光标为锚点）
            HandleWheelZoom(rect, ref state);
            // 中键平移（用户指示）
            HandleMiddlePan(rect, ref state);

            // 坐标轴：十字 + 刻度 + 标签（原点居中，(0,0) 在中央）
            DrawAxes(rect, k, center);

            // ① 工序链路：**先画**（在设备卡之下）+ **裁剪**（否则线段会随卡片一起跑出画布）
            GUI.BeginClip(rect);
            try
            {
                // clip 内坐标系以裁剪框左上角为原点 ⇒ 中心点同步平移
                DrawLayoutChain(new Vector2(center.x - rect.x, center.y - rect.y), k, state);
            }
            finally
            {
                GUI.EndClip();
            }

            // ⚠ 顺序（2026-10-02 审计修复）：**设备库落点必须在设备卡绘制之前**处理。
            //   原实现把它放在最后，于是"拖到已有卡上松手"会先被该卡的 ButtonInvisible 消费掉 MouseUp
            //   （连带弹出设备详情），落点被静默丢弃 ⇒ 摆过一张之后就"怎么拖都不行"。
            //   前置后：落在已有卡上 = 改位该卡（更符合直觉），且不会误开详情窗。
            // ⚠ BUG-2026-0001 · S-2：查看态须完全不响应落点（`interactive` 契约），但仍要
            //   `CancelDeviceDrag()` —— 否则编辑态拖到一半切查看态会把静态拖拽态卡死在 Active。
            if (interactive)
            {
                HandleDeviceDrop(rect, center, k, ref state);
            }
            else
            {
                CancelDeviceDrag();
            }
            if (deviceDrag.Active)
            {
                DrawDragGhost(deviceDragMousePos, deviceDrag.Label);
            }

            // 命中登记：设备卡的悬停 / 拖拽 / 单卡收回统一在本帧的绘制循环里收集，
            // **不另写一份布局数学**（两处算卡位必然漂移）。
            bool hoverCard = false;
            string hoverDeviceId = null;
            Vector2 hoverDeviceDesign = Vector2.zero;
            Vector2 hoverDeviceGrab = Vector2.zero;   // 抓取点相对卡心的偏移（设计单位）

            // ---- 已放置车间块（承载 = Zone.ID；托盘下标每帧反查）----
            for (int i = 0; i < state.PlacedZoneIds.Count; i++)
            {
                int paletteIndex = PaletteIndexOf(palette, state.PlacedZoneIds[i]);
                if (paletteIndex < 0)
                {
                    continue;   // 车间区已不存在（被删 / 换图）⇒ 不画（存档项由 Mirror 清理）
                }
                Vector2 design = state.PlacedPos[i];
                Rect block = new Rect(
                    center.x + design.x * k - BlockWidth * k * 0.5f,
                    center.y + design.y * k - BlockHeight * k * 0.5f,
                    BlockWidth * k, BlockHeight * k);
                // ⭐ 画布**不出现 zone 容器**（用户实机要求 2026-09-12 · 数十台设备防挤兑）：
                //   锚点框 `block` **不绘制任何视觉**（原 `.zcard` 投影/底/边框/顶条/工序号徽/名称 全部移除）；
                //   改为「**每台设备一张独立卡**」（网格排布），所属车间以**角标**标注在设备右上角 ⇒
                //   设备再多也不会被收纳进车间框，且**每台都可点击**唤出弹窗。
                PaletteEntry entry = palette[paletteIndex];
                List<DeviceEntry> devices = entry.Devices;
                if (devices == null || devices.Count == 0)
                {
                    continue;
                }
                for (int d = 0; d < devices.Count; d++)
                {
                    DeviceEntry device = devices[d];
                    if (string.IsNullOrEmpty(device.StableId))
                    {
                        continue;
                    }
                    // ⚠️ 手动摆过的（从设备库拖出 / 已被拖离本车间）**不在本循环画** ——
                    //   本循环以「该车间已在画布上」为前提，而「清空画布」会把全部车间收回托盘
                    //   ⇒ 独立设备会整个消失。独立设备一律交给下面的「独立设备层」单独绘制。
                    if (IndexOfDetached(device.StableId) >= 0)
                    {
                        continue;
                    }
                    Vector2 devicePos = DeviceGridPos(design, d);
                    Rect deviceBox = DeviceCardBox(center, k, devicePos);
                    if (!deviceBox.Overlaps(rect))
                    {
                        continue;
                    }
                    // 命中判定用**裁剪后**的矩形（见 DrawClippedDeviceCard 内的 intersect）：
                    // 画布外那部分不可见，就不该能被点/悬停/弹提示。
                    Rect hitBox = IntersectRect(deviceBox, rect);
                    bool cardHover = hitBox.width > 1f && hitBox.height > 1f && Mouse.IsOver(hitBox);
                    if (cardHover)
                    {
                        hoverCard = true;
                        hoverDeviceId = device.StableId;
                        hoverDeviceDesign = devicePos;
                        hoverDeviceGrab = devicePos - DesignAt(mouseDesign, center, k);
                    }
                    bool flash = state.FlashIndex == paletteIndex && IsFlashing(state);
                    if (DrawClippedDeviceCard(rect, deviceBox, device, cardScaleOf(k), time, cardHover,
                            flash, showZoneBadge: true, interactive: interactive,
                            removeClicked: out bool removeHit))
                    {
                        clickedDeviceStableId = device.StableId;
                    }
                    if (removeHit)
                    {
                        RemoveDetached(device.StableId);
                    }
                    // 定位闪烁已并入 `DrawClippedDeviceCard`（`flash` 参数）—— 与卡片同受画布裁剪，
                    // 否则闪烁描边会跟着卡片一起画到画布外（改造 P3 的 RUIF PulseDot/FlashOverlay 语义不变）。
                }

                // 工人圆点 / 车间 meta / 车间的「×」与拖拽把手 **全部移除** —— 画布已无 zone 容器
                // （车间收回改在「编排」页签的托盘里做）。仅保留「叠加标注」：在锚点处显示该车间绝对坐标。
                if (state.ShowAnnotations && block.Overlaps(rect))
                {
                    UIComponents.Label(new Rect(block.x, block.y, Mathf.Max(40f, block.width), 14f),
                        Translate2("PersonalChronicle.UI.Layout.BlockAt",
                            Mathf.RoundToInt(design.x), Mathf.RoundToInt(design.y)),
                        GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                }
            }

            // 独立设备层（**不依赖车间是否已放置**）：清空画布后仍能把设备拖出来并**看得见** —— 实机 bug 修复点。
            string detachedClicked = DrawDetachedLayer(rect, center, k, palette, interactive,
                ref hoverCard, ref hoverDeviceId, ref hoverDeviceDesign, ref hoverDeviceGrab,
                out string removedDeviceId, mouseDesign);
            if (!string.IsNullOrEmpty(detachedClicked))
            {
                clickedDeviceStableId = detachedClicked;
            }
            if (!string.IsNullOrEmpty(removedDeviceId))
            {
                RemoveDetached(removedDeviceId);
            }

            if (interactive)
            {
                // ① 设备卡拖拽（**优先于**车间拖拽）：命中设备卡就拖设备，没命中才考虑整块车间。
                //    ⚠ 2026-10-02 审计 P1-1/P1-2：原实现既没有设备卡命中，又拿"不绘制任何视觉"的
                //    200×120 锚点框当唯一拖拽热区 ⇒ 设备卡放下即固定 + 空白处会误拖车间。
                if (!HandleDeviceCardDrag(rect, palette, k, center, ref state, mouseDesign,
                        hoverDeviceId, hoverDeviceDesign, hoverDeviceGrab, out string cardClicked))
                {
                    HandleDragAndDrop(rect, palette, k, center, ref state, hoverDeviceId);
                }
                else if (!string.IsNullOrEmpty(cardClicked))
                {
                    // BUG-2026-0001 · F-3：编排态「按下未移动」= 点击 ⇒ 开详情（保住原入口）。
                    clickedDeviceStableId = cardClicked;
                }
            }

            DrawAlignGuides(rect, center, k, ref state);   // 对齐参考线（CHG-MFG-ALIGN-001）

            // 闪烁到期自动失效（避免过期下标残留）
            if (state.FlashIndex >= 0 && !IsFlashing(state))
            {
                state.FlashIndex = -1;
            }

            if (state.PlacedZoneIds.Count == 0 && DetachedList.Count == 0)
            {
                DrawEmptyHint(rect, palette, interactive);
            }

            // 落点放置：先点托盘 → 再点画布（**仅编排态**：查看态是只读观测面）
            Event current = Event.current;
            if (interactive && current != null && current.type == EventType.MouseUp && current.button == 0
                && state.PickedIndex >= 0 && Mouse.IsOver(rect))
            {
                int pickedIndex = state.PickedIndex;
                if (pickedIndex >= 0 && pickedIndex < palette.Count)
                {
                    Place(ref state, palette[pickedIndex].ZoneId,
                        DesignAt(current.mousePosition, center, k));
                }
                state.PickedIndex = -1;
                current.Use();
            }

            // 设备库「点选 → 点画布」放置（**仅编排态**，2026-10-02 审计 P1-7 补的降级入口）。
            // ⚠ BUG-2026-0001 · S-2：原文写「两态都可用」，与 `interactive` 的只读契约矛盾；
            //   查看态还要**清掉陈旧点选**，否则切回编排态会凭空落一台设备。
            if (!interactive)
            {
                devicePlacePick = null;
            }
            else if (current != null && current.type == EventType.MouseUp && current.button == 0
                && devicePlacePick != null && Mouse.IsOver(rect))
            {
                PlaceDevice(ref state, devicePlacePick, DesignAt(current.mousePosition, center, k));
                devicePlacePick = null;
                current.Use();
            }
        }

        /// <summary>屏幕矩形 → 设计坐标（原点居中约定；所有落点/命中统一走这里，避免两套换算）。</summary>
        private static Vector2 DesignAt(Vector2 screen, Vector2 center, float k)
        {
            float scale = Mathf.Max(0.01f, k);
            return new Vector2((screen.x - center.x) / scale, (screen.y - center.y) / scale);
        }

        /// <summary>设备卡屏幕矩形（几何 ×k，尺寸封顶 <see cref="MaxDeviceCardScale"/>）。</summary>
        private static Rect DeviceCardBox(Vector2 center, float k, Vector2 designPos)
        {
            float cardScale = cardScaleOf(k);
            return new Rect(
                center.x + designPos.x * k - DeviceCardWidth * cardScale * 0.5f,
                center.y + designPos.y * k - DeviceCardHeight * cardScale * 0.5f,
                DeviceCardWidth * cardScale, DeviceCardHeight * cardScale);
        }

        /// <summary>卡片缩放档：几何随 k 展开，但**尺寸封顶**（高倍下不再出现满卡空白）。</summary>
        private static float cardScaleOf(float k)
        {
            return Mathf.Min(k, MaxDeviceCardScale);
        }

        /// <summary>两矩形交集（IMGUI 无内建；空交集返回零尺寸）。</summary>
        internal static Rect IntersectRect(Rect a, Rect b)
        {
            float xMin = Mathf.Max(a.xMin, b.xMin);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMax = Mathf.Min(a.yMax, b.yMax);
            if (xMax <= xMin || yMax <= yMin)
            {
                return new Rect(xMin, yMin, 0f, 0f);
            }
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        /// <summary>托盘下标 ← ZoneId（**每帧反查**，不缓存下标 ⇒ 增删车间不会错绑）。</summary>
        internal static int PaletteIndexOf(List<PaletteEntry> palette, int zoneId)
        {
            if (palette == null)
            {
                return -1;
            }
            for (int i = 0; i < palette.Count; i++)
            {
                if (palette[i].ZoneId == zoneId)
                {
                    return i;
                }
            }
            return -1;
        }

        private static void DrawAxes(Rect rect, float k, Vector2 center)
        {
            Color axis = UITheme.WithAlpha(UITheme.BorderSoft, 0.9f);
            Widgets.DrawLine(new Vector2(rect.x, center.y), new Vector2(rect.xMax, center.y), axis, 1f);
            Widgets.DrawLine(new Vector2(center.x, rect.y), new Vector2(center.x, rect.yMax), axis, 1f);

            for (float d = -DesignExtent; d <= DesignExtent + 0.1f; d += AxisStep)
            {
                if (Mathf.Abs(d) < 0.1f)
                {
                    continue;
                }
                float sx = center.x + d * k;
                float sy = center.y + d * k;
                // ⭐ 刻度**标签最小像素间距**守卫（2026-09-12 深度诊断）：
                //   刻度步长 = `AxisStep(100 设计单位) × k`；窗口宽 1400、缩放 8% 时仅 ≈12px，
                //   48px 宽的标签会**逐格重叠**成一串"小方块"（缩略图上看就像彩色方块阵）。
                //   ⇒ 间距不足时**只画刻度短线、不画标签**（数字读数另有 #zoom-bar 的光标坐标）。
                const float MinLabelStepPx = 56f;
                bool labelFits = AxisStep * k >= MinLabelStepPx;
                if (sx > rect.x + 4f && sx < rect.xMax - 4f)
                {
                    Widgets.DrawLine(new Vector2(sx, center.y - 3f), new Vector2(sx, center.y + 3f),
                        axis, 1f);
                    if (labelFits)
                    {
                        UIComponents.Label(new Rect(sx - 24f, center.y + 2f, 48f, 14f),
                            Mathf.RoundToInt(d).ToString(), GameFont.Tiny, UITheme.Muted,
                            TextAnchor.MiddleCenter);
                    }
                }
                if (sy > rect.y + 4f && sy < rect.yMax - 14f)
                {
                    Widgets.DrawLine(new Vector2(center.x - 3f, sy), new Vector2(center.x + 3f, sy),
                        axis, 1f);
                    if (labelFits)
                    {
                        UIComponents.Label(new Rect(center.x + 4f, sy - 7f, 48f, 14f),
                            Mathf.RoundToInt(d).ToString(), GameFont.Tiny, UITheme.Muted,
                            TextAnchor.MiddleLeft);
                    }
                }
            }

            Widgets.DrawBoxSolid(new Rect(center.x - 3f, center.y - 3f, 6f, 6f), UITheme.Accent);
            UIComponents.Label(new Rect(center.x + 6f, center.y - 18f, 120f, 15f),
                Translate("PersonalChronicle.UI.Layout.Origin"), GameFont.Tiny, UITheme.Accent,
                TextAnchor.MiddleLeft);
        }

        /// <summary>
        /// 空态提示。
        ///
        /// <para>⚠ 2026-10-02 审计 P1-7：原文案在**所有上下文**都教用户做做不到的事 ——
        /// 「点托盘车间 → 画布落点 · 点块的 × 收回」里的三个动作在主画布上全部不可达
        /// （托盘只存在于底栏「编排」页签的小画布、车间块的 × 已被移除）。
        /// 现在按 <paramref name="interactive"/> 分模式给出**当态真的做得到**的操作。</para>
        /// </summary>
        private static void DrawEmptyHint(Rect rect, List<PaletteEntry> palette, bool interactive)
        {
            bool hasPalette = palette != null && palette.Count > 0;
            UIComponents.Label(new Rect(rect.x, rect.center.y - 34f, rect.width, 20f), "▦",
                GameFont.Medium, UITheme.Dim, TextAnchor.MiddleCenter);
            UIComponents.Label(new Rect(rect.x, rect.center.y - 8f, rect.width, 18f),
                Translate(hasPalette
                    ? "PersonalChronicle.UI.Layout.Empty"
                    : "PersonalChronicle.UI.Layout.EmptyNoZone"),
                GameFont.Tiny, UITheme.Text, TextAnchor.MiddleCenter);
            UIComponents.Label(new Rect(rect.x, rect.center.y + 10f, rect.width, 16f),
                Translate(hasPalette
                    ? (interactive
                        ? "PersonalChronicle.UI.Layout.EmptyHintEdit"
                        : "PersonalChronicle.UI.Layout.EmptyHintView")
                    : "PersonalChronicle.UI.Layout.EmptyHintNoZone"),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleCenter);
            UIComponents.Label(new Rect(rect.x, rect.center.y + 26f, rect.width, 16f),
                Translate("PersonalChronicle.UI.Layout.CoordHint"), GameFont.Tiny, UITheme.Muted,
                TextAnchor.MiddleCenter);
        }

        /// <summary>
        /// 滚轮缩放（预览滚轮行为）。引擎无"以光标为锚点"的必要 —— 预览 L12 已锁镜头中心为原点，
        /// 故缩放直接改 `Zoom`，原点保持在画布中心。
        /// </summary>
        private static void HandleWheelZoom(Rect rect, ref CanvasState state)
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.ScrollWheel
                || Mathf.Approximately(current.delta.y, 0f) || !Mouse.IsOver(rect))
            {
                return;
            }
            float factor = current.delta.y > 0f ? 0.9f : 1.111f;
            state.Zoom = Mathf.Clamp(state.Zoom * factor, MinZoom, MaxZoom);
            current.Use();
        }

        // ---- 交互：中键平移 + 从设备库拖出设备（会话内）----

        /// <summary>中键平移是否进行中（用户指示：平移键用鼠标中键）。</summary>
        private static bool middlePanActive;

        /// <summary>从设备库拖出的载荷（跨控件共享：左轨发起 → 画布落点）。</summary>
        private struct DeviceDragPayload
        {
            internal bool Active;
            internal string StableId;
            internal string Label;
        }

        private static DeviceDragPayload deviceDrag;
        private static Vector2 deviceDragMousePos;

        /// <summary>
        /// 「点选待放置」的设备 stableId（<c>null</c> = 无）；2026-10-02 审计 P1-7 新增的
        /// **拖放等价入口**：设备库单击一行 → 点画布落点。拖放对部分玩家不可发现
        /// （入口是隐藏页签、视口窄时整条左轨消失）。
        /// </summary>
        private static string devicePlacePick;

        /// <summary>待放置设备的显示名（供宿主画状态提示）。</summary>
        private static string devicePlacePickLabel;

        /// <summary>脱离车间的设备（**会话内**；稳定键 = stableId）。</summary>
        internal struct DetachedDevice
        {
            internal string StableId;
            internal Vector2 Position;
        }

        /// <summary>
        /// 已脱离车间、独立摆放的设备（**位置随存档**：<c>ChronicleGameComponent.mfgDeviceLayout</c>，
        /// 稳定键 = stableId）。
        /// <para>⚠ 2026-10-02 审计 P0-1/P0-2：内存清单是**渲染源 + 持久化源**，必须与存档保持可重建关系
        /// —— 作用域变化或关窗（<see cref="ClearSession"/> 置 <c>detachedReloadPending</c>）后
        /// 由 <see cref="EnsureSessionScope"/> 强制重读；未成功重读前禁止任何删除写入。</para>
        /// </summary>
        private static readonly List<DetachedDevice> DetachedList = new List<DetachedDevice>();

        /// <summary>
        /// 会话作用域的所属 <c>Game</c> 实例（2026-09-21 审计修复 · CHG-SHOP-AUDIT-001 · A-4）。
        /// 比较引用即可判定"换档 / 新开一局"，一行成本；离线（引擎未起）时跳过判断。
        /// </summary>
        private static object sessionGame;

        /// <summary>
        /// 会话作用域的所属 <c>Map</c>（2026-10-02 审计 P1-5）。
        /// <para>原实现只比 <c>Current.Game</c> ⇒ **同局换地图**（<c>MainButtonDef.Notify_SwitchedMap</c>
        /// 同样会触发窗口 <c>PreClose</c>）时旧图的设备清单会残留，而托盘已换成新图 ⇒
        /// <c>FindDevice</c> 全部失配、画布空白。两级引用比较，成本仍是 O(1)。</para>
        /// </summary>
        private static Map sessionMap;

        /// <summary>
        /// **需要从存档重读设备清单**的标志（2026-10-02 审计 P0-2）。
        ///
        /// <para>⛔ 这是"设备卡保存不住"的根因闸门：<c>PreClose</c> 会 <see cref="ClearSession"/>
        /// 清空内存清单，而窗口实例是 <c>MainButtonDef.tabWindowInt</c> **缓存单例**
        /// （Cecil 取证：切标签只 <c>WindowStack.TryRemove</c> → <c>PreClose</c>，<b>不销毁实例</b>），
        /// 于是宿主侧 <c>layoutLoadedFromSave</c> 永不复位、<see cref="EnsureSessionScope"/>
        /// 又因 <c>Current.Game</c> 引用未变而判定"作用域没变" ⇒ <b>重开窗口不重读</b> ⇒ 画布空白。
        /// 现在 <see cref="ClearSession"/> 置位本标志 ⇒ 下一次绘制入口必定重读。</para>
        /// </summary>
        private static bool detachedReloadPending = true;

        /// <summary>
        /// 设备清单是否已从存档**成功**加载（2026-10-02 审计 P0-1）。
        /// <para>⛔ 未加载时**禁止任何删除写入**：旧实现里"空清单 ⇒ 差集删除存档全部设备条目"
        /// （<c>MirrorDetachedToSave</c>）会把玩家刚摆好的布局在关窗/撤销时抹掉，且不可回滚。</para>
        /// </summary>
        private static bool detachedLoaded;

        /// <summary>
        /// 会话作用域守卫：**换档 / 换地图 / 关窗重开**时清理全部会话级静态态并**重读存档**。
        /// 由 <see cref="DrawCanvasBody"/> 每次绘制入口调用（O(1) 引用比较 + 一个 bool）。
        /// </summary>
        internal static void EnsureSessionScope()
        {
            object game;
            try
            {
                game = Current.Game;
            }
            catch (System.Exception)
            {
                return;
            }
            Map map = null;
            try
            {
                map = Find.CurrentMap;
            }
            catch (System.Exception)
            {
                map = null;
            }
            if (ReferenceEquals(sessionGame, game) && ReferenceEquals(sessionMap, map)
                && !detachedReloadPending)
            {
                return;
            }
            sessionGame = game;
            sessionMap = map;
            ClearVolatileSessionState();
            // 作用域已变（含关窗重开）⇒ 从**该档**重读设备卡位置（位置随存档，2026-09-26）。
            detachedReloadPending = false;
            LoadDetachedFromSave();
        }

        /// <summary>
        /// 清理会话级**易失**静态态：独立设备清单 + 撤销快照 + 拖拽载荷 + 点选载荷
        /// + 中键平移标志 + 2 秒扫描缓存。**不触碰** <c>CanvasState</c>（那是窗口实例态，
        /// 随窗口自然消亡；其持久部分由 <c>MasterControlTabWindow</c> 经 Application 门面写存档）。
        /// </summary>
        private static void ClearVolatileSessionState()
        {
            DetachedList.Clear();
            UndoDetached.Clear();
            deviceDrag = default;
            devicePlacePick = null;
            deviceCardDragId = null;
            detachedLoaded = false;
            middlePanActive = false;
            WorkshopPaletteAccess.ClearCache();
        }

        /// <summary>
        /// 会话结束钩子（**关窗**，由 <c>MasterControlTabWindow.PreClose</c> 调用）。
        ///
        /// <para>⚠ 2026-10-02 审计 P0-2：清空内存清单的同时**必须置位
        /// <see cref="detachedReloadPending"/>**，否则重开窗口时 <see cref="EnsureSessionScope"/>
        /// 会因作用域引用未变而跳过重读 ⇒ 存档里有、画布上空白（用户观感＝丢档）。</para>
        /// </summary>
        internal static void ClearSession()
        {
            ClearVolatileSessionState();
            detachedReloadPending = true;
        }

        /// <summary>左轨「设备库」按下某设备行时调用：开始拖出（画布随后接管落点）。</summary>
        internal static void BeginDeviceDrag(string stableId, string label)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return;
            }
            deviceDrag = new DeviceDragPayload { Active = true, StableId = stableId, Label = label };
            // 统一用 GUI 空间坐标（与落点换算同源；原用 `UI.MousePositionOnUIInverted`
            // 是屏幕空间换算值 ⇒ ghost 首帧与光标反向跳位）。
            deviceDragMousePos = Event.current != null
                ? Event.current.mousePosition
                : Verse.UI.MousePositionOnUIInverted;
        }

        /// <summary>
        /// 左轨「设备库」**单击**（未拖动即松手）某设备行时调用：进入"待放置"点选态。
        /// <para>2026-10-02 审计 P1-7：拖放入口是**隐藏页签**且视口窄时整条轨道消失，
        /// 大量玩家找不到 ⇒ 补一条等价的"点一行 → 点画布"路径（**仅编排态**；
        /// BUG-2026-0001 · S-2：原写「两态都可用」与 `interactive` 只读契约矛盾）。</para>
        /// </summary>
        internal static void PickDeviceForPlace(string stableId, string label)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return;
            }
            devicePlacePick = devicePlacePick == stableId ? null : stableId;
            devicePlacePickLabel = label;
        }

        /// <summary>当前待放置的设备（<c>null</c> = 无）；供宿主画状态提示。</summary>
        internal static string PendingDevicePick
        {
            get { return devicePlacePick; }
        }

        private static int IndexOfDetached(string stableId)
        {
            for (int i = 0; i < DetachedList.Count; i++)
            {
                if (DetachedList[i].StableId == stableId)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool IsDetached(string stableId)
        {
            return !string.IsNullOrEmpty(stableId) && IndexOfDetached(stableId) >= 0;
        }

        /// <summary>
        /// 放置/移动一台独立设备并**立即落档**（写内存中的 GameComponent；真正写盘随游戏存档）。
        /// </summary>
        private static void SetDetached(string stableId, Vector2 design)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return;
            }
            int index = IndexOfDetached(stableId);
            if (index >= 0)
            {
                DetachedDevice moved = DetachedList[index];
                moved.Position = design;
                DetachedList[index] = moved;
            }
            else
            {
                DetachedList.Add(new DetachedDevice { StableId = stableId, Position = design });
            }
            // 位置随存档（2026-09-26）：UI 经 **Application 门面**写（ARC-001：UI 不直触 Data）。
            SaveDetached(stableId, design);
        }

        /// <summary>
        /// 收回一台设备（**显式用户动作**：点卡上的 ×）：删内存项 + 删存档项。
        /// <para>这是**唯一**允许删除存档设备条目的入口之一（另一个是
        /// <see cref="ClearAll"/> 的"清空画布"）。差集式反删已移除（P0-1）。</para>
        /// </summary>
        private static void RemoveDetached(string stableId)
        {
            int index = IndexOfDetached(stableId);
            if (index < 0)
            {
                return;
            }
            DetachedList.RemoveAt(index);
            try
            {
                PersonalChronicle.Application.Manufacturing.ManufacturingLayoutService
                    .RemoveDevice(stableId);
            }
            catch (System.Exception ex)
            {
                Log.ErrorOnce("[PC] 设备卡位置删除失败：" + ex.Message, 20261002);
            }
        }

        // ---- 设备卡位置持久化（2026-09-26）----------------------------

        private static readonly List<string> detachedTempIds = new List<string>();
        private static readonly List<Vector2> detachedTempPos = new List<Vector2>();

        /// <summary>写入/更新一台设备的位置（失败只记一次日志，不影响绘制）。</summary>
        private static void SaveDetached(string stableId, Vector2 design)
        {
            try
            {
                PersonalChronicle.Application.Manufacturing.ManufacturingLayoutService
                    .SaveDevice(stableId, design);
            }
            catch (System.Exception ex)
            {
                Log.ErrorOnce("[PC] 设备卡位置写入存档失败：" + ex.Message, 20260926);
            }
        }

        /// <summary>
        /// 从该档读入设备卡位置（**幂等**：每次调用都从存档重建内存清单）。
        ///
        /// <para><b>调用时机</b>：由 <see cref="EnsureSessionScope"/> 在**作用域变化或
        /// <see cref="detachedReloadPending"/> 置位**时统一调用 —— 覆盖「换档 / 换地图 / 关窗重开」
        /// 三种情况。这是 P0-2 的修复点：旧实现在宿主侧用 `layoutLoadedFromSave` 一次性闩锁挡住重读，
        /// 而窗口实例是 <c>MainButtonDef.tabWindowInt</c> 缓存单例 ⇒ 切标签重开必然读到空清单。</para>
        ///
        /// <para>读失败 ⇒ 回落空清单并保持 <c>detachedLoaded = false</c>（后续禁止删除写入）。</para>
        /// </summary>
        internal static void LoadDetachedFromSave()
        {
            detachedLoaded = false;
            try
            {
                PersonalChronicle.Application.Manufacturing.ManufacturingLayoutService
                    .LoadDevicesInto(detachedTempIds, detachedTempPos);
                DetachedList.Clear();
                for (int i = 0; i < detachedTempIds.Count; i++)
                {
                    if (string.IsNullOrEmpty(detachedTempIds[i]))
                    {
                        continue;
                    }
                    DetachedList.Add(new DetachedDevice
                    {
                        StableId = detachedTempIds[i],
                        Position = detachedTempPos[i]
                    });
                }
                detachedLoaded = true;
            }
            catch (System.Exception ex)
            {
                Log.ErrorOnce("[PC] 设备卡位置读取存档失败（回落默认网格）：" + ex.Message, 20260927);
            }
        }

        /// <summary>
        /// 关窗前强制镜像一次（自动保存收口）：关窗走 <see cref="ClearSession"/> 会清空内存清单，
        /// 若此时还有未落档的变更就会丢失 ⇒ 由宿主 <c>PreClose</c> 在 <c>ClearSession</c> **之前**调用。
        /// </summary>
        internal static void PersistBeforeClose()
        {
            MirrorDetachedToSave();
        }

        /// <summary>
        /// 把独立设备清单**写入**存档（幂等；只写不删）。
        ///
        /// <para>⛔ 2026-10-02 审计 P0-1：原实现在这里做"差集删除"（档里有、清单上没有 ⇒ 删档）。
        /// 由于内存清单会被 <see cref="ClearSession"/> 清空且一度无法重读（P0-2），
        /// 该差集删除会在**关窗 / 撤销 / 清空画布**时把玩家摆好的设备布局整体抹掉且不可回滚。
        /// 现在删除只允许由**显式用户动作**驱动：单卡 ×（<see cref="RemoveDetached"/>）
        /// 与「清空画布」（<see cref="ClearAll"/>）。</para>
        ///
        /// <para>未成功加载存档（<c>detachedLoaded == false</c>）时**整体跳过**：宁可不同步，
        /// 也不能拿一份空清单去覆盖权威数据。</para>
        /// </summary>
        private static void MirrorDetachedToSave()
        {
            if (!detachedLoaded)
            {
                return;
            }
            try
            {
                for (int i = 0; i < DetachedList.Count; i++)
                {
                    PersonalChronicle.Application.Manufacturing.ManufacturingLayoutService
                        .SaveDevice(DetachedList[i].StableId, DetachedList[i].Position);
                }
            }
            catch (System.Exception ex)
            {
                Log.ErrorOnce("[PC] 设备卡位置镜像存档失败：" + ex.Message, 20260928);
            }
        }

        /// <summary>在托盘（车间）里按 stableId 找设备条目（找不到返回空条目）。</summary>
        private static DeviceEntry FindDevice(List<PaletteEntry> palette, string stableId)
        {
            if (palette == null || string.IsNullOrEmpty(stableId))
            {
                return default;
            }
            for (int i = 0; i < palette.Count; i++)
            {
                List<DeviceEntry> devices = palette[i].Devices;
                if (devices == null)
                {
                    continue;
                }
                for (int d = 0; d < devices.Count; d++)
                {
                    if (devices[d].StableId == stableId)
                    {
                        return devices[d];
                    }
                }
            }
            return default;
        }

        /// <summary>拖放预览（ghost）：跟随光标的标签框（用户反馈"拖放无预览效果"）。</summary>
        private static void DrawDragGhost(Vector2 pos, string label)
        {
            string text = string.IsNullOrEmpty(label) ? "?" : label;
            float width = Mathf.Min(Measure(text) + 18f, 160f);
            Rect box = new Rect(pos.x + 12f, pos.y - 8f, width, 18f);
            Widgets.DrawBoxSolid(box, UITheme.WithAlpha(UITheme.Window, 0.92f));
            UIComponents.Border(box, UITheme.Info);
            UIComponents.Label(box, UIComponents.TruncateToWidth(text, box.width - 6f, GameFont.Tiny),
                GameFont.Tiny, UITheme.Info, TextAnchor.MiddleCenter);
        }

        /// <summary>中键平移（用户指示）：按住中键拖动 ⇒ 改 `Pan`（屏幕像素）。</summary>
        private static void HandleMiddlePan(Rect rect, ref CanvasState state)
        {
            Event current = Event.current;
            if (current == null)
            {
                return;
            }
            if (current.type == EventType.MouseDown && current.button == 2 && Mouse.IsOver(rect))
            {
                middlePanActive = true;
                current.Use();
                return;
            }
            if (!middlePanActive)
            {
                return;
            }
            if (current.type == EventType.MouseDrag)
            {
                state.Pan += current.delta;
                current.Use();
                return;
            }
            if (current.type == EventType.MouseUp && current.button == 2)
            {
                middlePanActive = false;
                current.Use();
            }
        }

        /// <summary>
        /// 设备卡的设计尺寸 —— **真源 = <see cref="WorkshopFloorLayout"/>**（预览 `MW=104 MH=60`，LAND-001）。
        ///
        /// <para>此前这里是自造的 `124×36` 扁条：与预览的 104×60（比例 1.73）不成比例，
        /// 且**装不下**名称 / 状态 / 工单·功率 / 作业者四行 + 进度条（实机截图：满卡空白 + 页脚贴边）。</para>
        /// </summary>
        internal const float DeviceCardWidth = WorkshopFloorLayout.MachineW;
        internal const float DeviceCardHeight = WorkshopFloorLayout.MachineH;

        /// <summary>
        /// 设备卡的**屏幕尺寸上限**（倍率）—— 与"分档信息密度"配套。
        ///
        /// <para>引擎字体是固定像素，卡片几何却随 <c>k</c> 放大：放大到 3.22× 时卡变成 335×193，
        /// 而内容行数是固定的 ⇒ 下面大片空白（实机截图）。这里的处理是
        /// **「卡越大 → 行档越多」+「行档用满即封顶」**：
        /// 卡片放大到 <c>2.5</c>（260×150）时**七行全部解锁**（名称 / 工单·功率 / 状态 / 作业者 /
        /// 岗位 / 技能 / 生产 + 进度条，填充 ≈94%），再大就不再变大（只有位置按 k 展开）。</para>
        /// </summary>
        internal const float MaxDeviceCardScale = 2.5f;

        /// <summary>设备卡的行档上限（= 行池长度；见 <see cref="DrawDeviceCard"/> 的行序表）。</summary>
        internal const int MaxDeviceCardRows = 8;

        /// <summary>行 5「岗位」最多列几个当班人名（卡宽有限，超出只显示前几个）。</summary>
        private const int MaxDutyNames = 3;

        /// <summary>
        /// 默认网格间距（设计单位）。列距 = 卡片宽 + 16（预览相邻机箱 `MachineLocalX = {18,138}` 差 120 的同一间距）；
        /// 行距 = 卡片高 + 12（保证两行卡片之间留出呼吸，不至于贴边）。
        /// </summary>
        internal const float DeviceGridGapX = 16f;
        internal const float DeviceGridGapY = 12f;

        private static bool DrawToggle(float left, float y, ref float x, float gap, string label,
            bool on)
        {
            float width = Measure(label) + 16f;
            x -= width;
            Rect rect = new Rect(x, y, width, 16f);
            if (on)
            {
                Widgets.DrawBoxSolid(rect, UITheme.WithAlpha(UITheme.Accent, 0.18f));
            }
            UIComponents.Border(rect, on ? UITheme.Accent : UITheme.BorderSoft);
            UIComponents.Label(new Rect(rect.x + 2f, rect.y, rect.width - 4f, rect.height), label,
                GameFont.Tiny, on ? UITheme.Accent : UITheme.Dim, TextAnchor.MiddleCenter);
            bool clicked = Widgets.ButtonInvisible(rect);
            x -= gap;
            return clicked;
        }

        /// <summary>
        /// Save 结构预览明细（预览的 `WorkshopLayoutPlan` 结构）。
        ///
        /// <para>⚠ 2026-10-02 审计 P0-4：原实现**只列车间块**、<c>entries</c> 也只数车间
        /// ⇒ 玩家把设备摆上画布后，悬停仍显示 <c>entries: 0</c>，直接被误读成"根本没存"。
        /// 现在**两个持久化容器都如实列出**：<c>mfgLayout</c>（车间，按 ZoneId）
        /// 与 <c>mfgDeviceLayout</c>（设备卡，按 stableId）。</para>
        /// </summary>
        private static string BuildSavePreview(CanvasState state, List<PaletteEntry> palette)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            int deviceCount = 0;
            for (int i = 0; i < DetachedList.Count; i++)
            {
                if (!string.IsNullOrEmpty(DetachedList[i].StableId))
                {
                    deviceCount++;
                }
            }
            builder.Append(Translate("PersonalChronicle.UI.Layout.SaveTipTitle"))
                .Append('\n')
                .AppendFormat("schemaVersion: {0} · entries: {1} · devices: {2}", LayoutSchemaVersion,
                    state.PlacedZoneIds.Count, deviceCount)
                .Append('\n')
                .Append(Translate("PersonalChronicle.UI.Layout.SaveTipScope"));
            for (int i = 0; i < state.PlacedZoneIds.Count; i++)
            {
                int paletteIndex = PaletteIndexOf(palette, state.PlacedZoneIds[i]);
                string name = paletteIndex >= 0 && palette != null
                    ? palette[paletteIndex].Label
                    : Translate1("PersonalChronicle.UI.Layout.ZoneIdFallback",
                        state.PlacedZoneIds[i]);
                builder.Append('\n').Append("- [").Append("mfgLayout").Append("] ")
                    .Append(name)
                    .Append(" @ (")
                    .AppendFormat("{0}, {1}", Mathf.RoundToInt(state.PlacedPos[i].x),
                        Mathf.RoundToInt(state.PlacedPos[i].y))
                    .Append(')');
            }
            for (int i = 0; i < DetachedList.Count; i++)
            {
                if (string.IsNullOrEmpty(DetachedList[i].StableId))
                {
                    continue;
                }
                DeviceEntry entry = FindDevice(palette, DetachedList[i].StableId);
                builder.Append('\n').Append("- [").Append("mfgDeviceLayout").Append("] ")
                    .Append(string.IsNullOrEmpty(entry.StableId)
                        ? DetachedList[i].StableId
                        : entry.Label)
                    .Append(" @ (")
                    .AppendFormat("{0}, {1}",
                        Mathf.RoundToInt(DetachedList[i].Position.x),
                        Mathf.RoundToInt(DetachedList[i].Position.y))
                    .Append(')');
            }
            return builder.ToString();
        }

        /// <summary>供宿主（② 外壳 `#lbar` / 缩放行）复用同一份 Save 明细，避免两处口径分裂。</summary>
        internal static string SavePreviewText(ref CanvasState state, List<PaletteEntry> palette)
        {
            return BuildSavePreview(state, palette);
        }

    }
}
