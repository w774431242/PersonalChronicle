using System;
using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Automation;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>
    /// 编排画布的**对齐吸附**接线（CHG-MFG-ALIGN-001 · M2）。
    ///
    /// <para><b>职责边界</b>：本文件只做 5 件事 —— ① 采样设计坐标 ② 枚举邻居 ③ 调 Domain 纯函数
    /// ④ 应用结果 ⑤ 画参考线。**吸附数学一律不写在这里**（唯一入口
    /// <see cref="CanvasAlignment.Resolve"/>），阈值比较也只允许出现在 Domain 侧。</para>
    ///
    /// <para><b>坐标系</b>：设计单位，原点居中，+X→右 / +Y→下，允许负值，不取整。
    /// 参考线绘制在 <c>GUI.BeginClip</c> 内 ⇒ 中心点要减去裁剪框原点
    /// （与 <c>DrawLayoutChain</c> 同式）。</para>
    ///
    /// <para><b>零分配</b>：<c>AlignBuf</c> / <c>AlignGuides</c> 是 <see cref="CanvasState"/> 上的
    /// 预分配缓冲，每帧只 <c>Clear()</c> + <c>Add(struct)</c>。</para>
    /// </summary>
    internal static partial class WorkshopLayoutCanvas
    {
        /// <summary>一条对齐参考线（保存**设计坐标**，绘制时才乘缩放系数）。</summary>
        internal struct CanvasAlignGuide
        {
            /// <summary><c>true</c> = 垂直线（X 轴对齐）；<c>false</c> = 水平线（Y 轴对齐）。</summary>
            internal bool Vertical;

            /// <summary>命中的设计坐标 —— 参考线就画在这条线上。</summary>
            internal float LineDesign;

            /// <summary>命中来源（用于视觉权重：中心线比边线更重）。</summary>
            internal CanvasAlignKind Kind;
        }

        private static float AlignHalfWidth
        {
            get { return DeviceCardWidth * 0.5f; }
        }

        private static float AlignHalfHeight
        {
            get { return DeviceCardHeight * 0.5f; }
        }

        // 参考线线宽（像素）。**中心线比边线更重** —— 业界 snap guide 惯例：
        // 中心对齐是比"边线齐平"更强的意图，视觉权重应当区分开。
        private const float AlignGuideThicknessCenter = 1.5f;
        private const float AlignGuideThicknessEdge = 1f;

        /// <summary>
        /// 把画布上**其它**设备卡投影成 AABB 候选（每帧一次）。
        ///
        /// <para><b>排除式去重（正确性不依赖调用顺序）</b>：车间内的卡与独立设备层可能指向同一台设备
        /// （<c>HandleDeviceCardDrag</c> 的 MouseDown 会把车间内的卡并入 <c>DetachedList</c>），
        /// 若不排除就会让同一张卡以两种身份进入候选 ⇒ 幽灵吸附线。因此这里与渲染循环
        /// （<c>DrawCanvasBody</c> 内 <c>IndexOfDetached(stableId) &gt;= 0 → continue</c>）
        /// 用**同一条排除规则**，并额外排除被拖动的卡自身。</para>
        ///
        /// <para><b>不缓存</b>：跨帧缓存要同时盯住 6 条失效路径（<c>ClearSession</c> /
        /// <c>EnsureSessionScope</c> / <c>UndoLast</c> / <c>ClearAll</c> / <c>SetDetached</c> /
        /// <c>RemoveDetached</c>），漏一条就是幽灵吸附线。每帧重算的代价仅一次投影，
        /// 换来的是**无跨帧状态、无失效面</。</para>
        /// </summary>
        /// <summary>
        /// 车间**在册设备卡**是否参与命中 / 吸附 —— <b>单一排除真源</b>。
        /// </summary>
        /// <param name="stableId">设备稳定 id。</param>
        /// <param name="excludeId">要额外排除的 id（通常是被拖动的卡自身）。</param>
        /// <remarks>
        /// 三条排除规则集中在此，供<b>四个</b>调用点共用，避免各写一份而漂移：
        /// ① 渲染循环（车间内卡绘制）② <c>FindBlockAt</c>（车间块命中）
        /// ③ 设备卡吸附邻居投影 ④ 车间块 AABB 并集。
        /// <para>其中 <c>IndexOfDetached &gt;= 0 → 排除</c> 是关键：车间内的卡被拖一次就
        /// 永久转为独立设备层成员，若不排除就会与独立层<b>同卡双身份</b> ⇒ 幽灵吸附线 /
        /// 重复绘制。空车间（无在册卡）由此自然不产生任何候选。</para>
        /// </remarks>
        private static bool IsZoneDeviceAlignable(string stableId, string excludeId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return false;
            }
            if (stableId == excludeId)
            {
                return false;
            }
            return IndexOfDetached(stableId) < 0;
        }

        /// <summary>
        /// 某车间<b>在册设备卡的 AABB 并集</b>（设计单位）—— 车间块参与命中与吸附的唯一几何。
        /// </summary>
        /// <returns>
        /// <c>false</c> = 该车间无在册设备卡（空车间 / 车间区已不存在 / 下标越界）⇒
        /// 既不产生候选线，也不可被命中。<c>true</c> 时 <paramref name="bounds"/> 有效。
        /// </returns>
        /// <remarks>
        /// <b>为什么必须用设备卡并集、而不是 200×120 锚点框</b>（<c>BlockWidth/BlockHeight</c>）：
        /// 锚点框<b>不绘制任何视觉</b>（画布不出现 zone 容器），而 2026-10-02 审计 P1-2 已把
        /// 命中区从锚点框改为设备卡并集（理由：锚点框会在空白处产生隐形热区、点一下就误拖车间）。
        /// 若吸附按锚点框、命中按设备卡 ⇒ <b>「吸到了看不见、点得到但吸不上」的双源漂移</b>。
        /// 本方法让命中与吸附共用同一份几何，从根上消除该漂移。
        /// </remarks>
        private static bool TryGetZoneDeviceBounds(CanvasState state,
            List<PaletteEntry> palette, int zoneIndex, out CanvasAlignRect bounds)
        {
            bounds = default(CanvasAlignRect);
            if (zoneIndex < 0 || zoneIndex >= state.PlacedZoneIds.Count)
            {
                return false;
            }
            int paletteIndex = PaletteIndexOf(palette, state.PlacedZoneIds[zoneIndex]);
            if (paletteIndex < 0)
            {
                return false;   // 车间区已不存在（被删 / 换图）
            }
            List<DeviceEntry> devices = palette[paletteIndex].Devices;
            if (devices == null)
            {
                return false;
            }

            Vector2 anchor = state.PlacedPos[zoneIndex];
            float halfW = AlignHalfWidth;
            float halfH = AlignHalfHeight;
            bool any = false;
            CanvasAlignRect acc = default(CanvasAlignRect);
            for (int d = 0; d < devices.Count; d++)
            {
                if (!IsZoneDeviceAlignable(devices[d].StableId, deviceCardDragId))
                {
                    continue;
                }
                Vector2 p = DeviceGridPos(anchor, d);
                CanvasAlignRect card = CanvasAlignRect.FromCenter(p.x, p.y, halfW, halfH);
                if (!any)
                {
                    acc = card;
                    any = true;
                }
                else
                {
                    acc = acc.Union(card);   // 并集运算在 Domain（可离线单测），UI 不自写一份
                }
            }
            if (!any)
            {
                return false;   // 空车间：在画布上不可见 ⇒ 不产生候选线（否则是幽灵吸附）
            }
            bounds = acc;
            return true;
        }

        private static void BuildAlignNeighbors(ref CanvasState state, List<PaletteEntry> palette)
        {
            List<CanvasAlignRect> buf = state.AlignBuf;
            buf.Clear();

            // ① 独立设备层（已脱离车间、位置随存档）
            for (int i = 0; i < DetachedList.Count; i++)
            {
                string id = DetachedList[i].StableId;
                if (string.IsNullOrEmpty(id) || id == deviceCardDragId)
                {
                    continue;
                }
                Vector2 p = DetachedList[i].Position;
                buf.Add(CanvasAlignRect.FromCenter(p.x, p.y, AlignHalfWidth, AlignHalfHeight));
            }

            // ② 车间在册设备卡（跳过已转为独立卡的 —— 与渲染循环同源判定）
            for (int z = 0; z < state.PlacedZoneIds.Count; z++)
            {
                int paletteIndex = PaletteIndexOf(palette, state.PlacedZoneIds[z]);
                if (paletteIndex < 0)
                {
                    continue;   // 车间区已不存在（被删 / 换图）
                }
                List<DeviceEntry> devices = palette[paletteIndex].Devices;
                if (devices == null)
                {
                    continue;
                }
                Vector2 zoneDesign = state.PlacedPos[z];
                for (int d = 0; d < devices.Count; d++)
                {
                    if (!IsZoneDeviceAlignable(devices[d].StableId, deviceCardDragId))
                    {
                        continue;
                    }
                    Vector2 p = DeviceGridPos(zoneDesign, d);
                    buf.Add(CanvasAlignRect.FromCenter(p.x, p.y, AlignHalfWidth, AlignHalfHeight));
                }
            }
        }

        /// <summary>
        /// 拖动<b>车间块</b>时的邻居投影：独立设备卡（逐张） + <b>其它车间</b>的设备卡并集（每块一条）。
        /// </summary>
        /// <param name="skipZoneIndex">正在拖动的车间下标（自身不作为候选）。</param>
        /// <remarks>
        /// <b>为什么车间用并集而不是逐张卡</b>：拖车间块的语义是「整块对齐」，
        /// 玩家参照的是块的<b>外轮廓</b>；逐张卡会让块的内部卡边缘也变成候选线，
        /// 出现「块对齐到块内部某张卡」这种视觉上说不通的结果。
        /// <para>空车间由 <see cref="TryGetZoneDeviceBounds"/> 返回 false 自然被跳过
        /// ⇒ 吸附到不可见目标是幽灵吸附，必须避免。</para>
        /// </remarks>
        private static void BuildZoneAlignNeighbors(ref CanvasState state,
            List<PaletteEntry> palette, int skipZoneIndex)
        {
            List<CanvasAlignRect> buf = state.AlignBuf;
            buf.Clear();

            // ① 独立设备层（逐张：它们是画布上独立可见的对象）
            for (int i = 0; i < DetachedList.Count; i++)
            {
                string id = DetachedList[i].StableId;
                if (string.IsNullOrEmpty(id) || id == deviceCardDragId)
                {
                    continue;
                }
                Vector2 p = DetachedList[i].Position;
                buf.Add(CanvasAlignRect.FromCenter(p.x, p.y, AlignHalfWidth, AlignHalfHeight));
            }

            // ② 其它车间的设备卡并集（每块一条；空车间不产生候选）
            for (int z = 0; z < state.PlacedZoneIds.Count; z++)
            {
                if (z == skipZoneIndex)
                {
                    continue;
                }
                CanvasAlignRect bounds;
                if (TryGetZoneDeviceBounds(state, palette, z, out bounds))
                {
                    buf.Add(bounds);
                }
            }
        }

        /// <summary>
        /// 车间块拖拽的统一落点（<b>M3</b>）：按 Shift 时把整块吸附到邻近对象。
        /// </summary>
        /// <param name="design">跟手算出的新锚点（未吸附）。</param>
        /// <remarks>
        /// <b>Delta 为什么能直接加到锚点上</b>：设备卡并集随锚点<b>刚性平移</b>
        /// （<c>DeviceGridPos</c> 是锚点的固定偏移），所以并集 AABB 的中心与锚点同步平移。
        /// <see cref="CanvasAlignment.Resolve"/> 返回的是「让 AABB 落到候选线上的平移量」，
        /// 把它加到锚点 ⇒ 整块与 AABB 同步平移 ⇒ 结果正确。此处传入的 bounds 用
        /// <b>吸附前</b>的锚点算出即可（只用到形状与相对量）。
        ///
        /// <para><b>与设备卡拖拽的一处刻意差异</b>：<c>HandleDragAndDrop</c> 的 MouseUp
        /// <b>不再移动</b>（只清 <c>DragIndex</c>），因此不存在设备卡侧「松手弹回未吸附值」
        /// 的问题，无需在 MouseUp 重解。</para>
        /// </remarks>
        private static void ResolveAndMoveZone(float k, ref CanvasState state, int zoneIndex,
            List<PaletteEntry> palette, Vector2 design)
        {
            state.AlignGuides.Clear();

            Vector2 anchor = design;
            Event current = Event.current;
            bool wantSnap = state.SnapEnabled && current != null && current.shift;
            if (wantSnap)
            {
                CanvasAlignRect self;
                if (TryGetZoneDeviceBounds(state, palette, zoneIndex, out self))
                {
                    BuildZoneAlignNeighbors(ref state, palette, zoneIndex);
                    if (state.AlignBuf.Count > 0)
                    {
                        float threshold = CanvasAlignment.ScreenThresholdToDesign(
                            CanvasAlignment.SnapScreenPx, k,
                            CanvasAlignment.MinSnapDesign, CanvasAlignment.MaxSnapDesign);

                        float selfHalfW = (self.MaxX - self.MinX) * 0.5f;
                        float selfHalfH = (self.MaxY - self.MinY) * 0.5f;
                        CanvasAlignResult r = CanvasAlignment.Resolve(
                            state.AlignBuf, -1, self.CenterX, self.CenterY,
                            selfHalfW, selfHalfH, threshold, false, 0f, false);

                        if (r.X.Snapped)
                        {
                            anchor.x += r.X.Delta;
                            state.AlignGuides.Add(new CanvasAlignGuide
                            {
                                Vertical = true,
                                LineDesign = r.X.Line,
                                Kind = r.X.Kind,
                            });
                        }
                        if (r.Y.Snapped)
                        {
                            anchor.y += r.Y.Delta;
                            state.AlignGuides.Add(new CanvasAlignGuide
                            {
                                Vertical = false,
                                LineDesign = r.Y.Line,
                                Kind = r.Y.Kind,
                            });
                        }
                    }
                }
            }

            state.PlacedPos[zoneIndex] = anchor;
        }

        /// <summary>
        /// 设备卡拖拽的**统一落点**（MouseDrag 与 MouseUp 必须共用）。
        ///
        /// <para><b>为什么 MouseUp 也必须走这里</b>：原实现 MouseUp 用
        /// <c>DesignAt(mousePosition) + grab</c> 绝对换算，而 MouseDrag 走 delta 累加 ——
        /// 两者在浮点上可能不同。若只在 Drag 吸附，松手瞬间会出现「位置弹回未吸附值」，
        /// 玩家看到的是吸附位、存进档的是未吸附位。共用本方法 ⇒ <b>落档值 ≡ 视觉值</b>。</para>
        ///
        /// <para><b>Shift 实时读取、不锁存</b>：拖到一半才按下 / 松开 Shift 立即生效
        /// （Figma / PS 惯例）。MouseDown 帧**不读** Shift —— 只判命中与起拖，
        /// 避免「按下时没按 Shift 就整趟吸不上」的陷阱。</para>
        ///
        /// <para>M2 阶段网格吸附与原点对齐**均为关闭**（分别留待 M5 / 后续批次）。</para>
        /// </summary>
        private static Vector2 ResolveAndMoveDevice(float k, Vector2 center, ref CanvasState state,
            string stableId, Vector2 mouseScreen, List<PaletteEntry> palette)
        {
            Vector2 target = DesignAt(mouseScreen, center, k) + deviceCardGrabOffset;

            state.AlignGuides.Clear();

            Event current = Event.current;
            bool wantSnap = state.SnapEnabled && current != null && current.shift;
            if (wantSnap)
            {
                BuildAlignNeighbors(ref state, palette);
                if (state.AlignBuf.Count > 0)
                {
                    float threshold = CanvasAlignment.ScreenThresholdToDesign(
                        CanvasAlignment.SnapScreenPx, k,
                        CanvasAlignment.MinSnapDesign, CanvasAlignment.MaxSnapDesign);

                    CanvasAlignResult r = CanvasAlignment.Resolve(
                        state.AlignBuf, -1, target.x, target.y,
                        AlignHalfWidth, AlignHalfHeight, threshold,
                        false, 0f, false);

                    if (r.X.Snapped)
                    {
                        target.x += r.X.Delta;
                        state.AlignGuides.Add(new CanvasAlignGuide
                        {
                            Vertical = true,
                            LineDesign = r.X.Line,
                            Kind = r.X.Kind,
                        });
                    }
                    if (r.Y.Snapped)
                    {
                        target.y += r.Y.Delta;
                        state.AlignGuides.Add(new CanvasAlignGuide
                        {
                            Vertical = false,
                            LineDesign = r.Y.Line,
                            Kind = r.Y.Kind,
                        });
                    }
                }
            }

            MoveDetached(stableId, target);
            return target;   // 调用方据此落档 ⇒ 存档值与视觉值同源
        }

        /// <summary>
        /// 绘制对齐参考线（overlay）。
        ///
        /// <para><b>插入点</b>：必须在 <c>HandleDeviceCardDrag</c> / <c>HandleDragAndDrop</c>
        /// 之后调用 —— 否则参考线是上一帧的陈旧数据，肉眼可见「线不跟手」。</para>
        ///
        /// <para><b>残留清除</b>：IMGUI 的 <c>Repaint</c> / <c>Layout</c> 帧也会重绘整棵 GUI，
        /// 若只在 <c>MouseDrag</c> 帧写缓冲、其它帧不清，<c>Repaint</c> 帧会画出**冻结的残线**。
        /// 判据用**拖拽态</b> <c>deviceCardDragId</c>（而非单看 <c>Event.type</c>）——
        /// 松手那一帧 <c>deviceCardDragId</c> 已被清空，参考线随之消失，符合预期。</para>
        ///
        /// <para><b>裁剪安全</b>：走既有 <see cref="DrawPolyline"/>（<c>DrawBoxSolid</c> 串联）。
        /// ⛔ 禁用两点版 <c>Widgets.DrawLine</c> —— 它走 <c>Matrix4x4.TRS</c> + GL，
        /// 不感知 <c>GUI.matrix</c>，在 <c>BeginClip</c> 内必错位。</para>
        /// </summary>
        private static void DrawAlignGuides(Rect rect, Vector2 center, float k,
            ref CanvasState state)
        {
            // 两种拖拽（设备卡 / 车间块）任一在位才画；两者都结束 ⇒ 清残留。
            if (string.IsNullOrEmpty(deviceCardDragId) && state.DragIndex < 0)
            {
                state.AlignGuides.Clear();
                return;
            }
            if (state.AlignGuides.Count == 0)
            {
                return;
            }

            GUI.BeginClip(rect);
            try
            {
                Vector2 clipCenter = new Vector2(center.x - rect.x, center.y - rect.y);
                Color color = UITheme.AlignGuide;
                for (int i = 0; i < state.AlignGuides.Count; i++)
                {
                    CanvasAlignGuide guide = state.AlignGuides[i];
                    float thickness = guide.Kind == CanvasAlignKind.NeighborCenter
                        ? AlignGuideThicknessCenter
                        : AlignGuideThicknessEdge;
                    if (guide.Vertical)
                    {
                        float sx = clipCenter.x + guide.LineDesign * k;
                        DrawPolyline(new Vector2(sx, 0f), new Vector2(sx, rect.height), color,
                            thickness);
                    }
                    else
                    {
                        float sy = clipCenter.y + guide.LineDesign * k;
                        DrawPolyline(new Vector2(0f, sy), new Vector2(rect.width, sy), color,
                            thickness);
                    }
                }
            }
            finally
            {
                GUI.EndClip();
            }
        }

        /// <summary>
        /// 画布上是否还有**别的**设备卡可供对齐（纯查询，无副作用）。
        ///
        /// <para>排除规则与 <see cref="BuildAlignNeighbors"/> 同源：跳过被拖动的卡自身、
        /// 跳过已转为独立卡的车间在册卡。这样提示文案「无可对齐对象」才是**如实**的
        /// （否则会出现「提示说有目标、拖过去却不吸」的骗人状态）。</para>
        /// </summary>
        internal static bool HasAlignTarget(CanvasState state, List<PaletteEntry> palette)
        {
            for (int i = 0; i < DetachedList.Count; i++)
            {
                string id = DetachedList[i].StableId;
                if (!string.IsNullOrEmpty(id) && id != deviceCardDragId)
                {
                    return true;
                }
            }
            // 车间块（M3）：任一车间有在册设备卡即算有目标 —— 判定与吸附共用
            // TryGetZoneDeviceBounds，空车间自然不算。
            for (int z = 0; z < state.PlacedZoneIds.Count; z++)
            {
                CanvasAlignRect ignored;
                if (TryGetZoneDeviceBounds(state, palette, z, out ignored))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 「对齐吸附」开关在工具条上的文案 + 提示（宿主 <c>#lbar</c> 调用）。
        ///
        /// <para><b>不静默失效</b>：画布上没有其它卡片时开关**仍可点击**，只把提示换成
        /// <c>SnapTipNoTarget</c>，让玩家知道「为什么没反应」。</para>
        /// </summary>
        internal static string AlignSnapLabel
        {
            get { return Translate("PersonalChronicle.UI.Layout.ToggleSnap"); }
        }

        /// <summary>吸附提示：随「画布上是否还有别的卡」切换措辞（GATE-UI 状态完备）。</summary>
        internal static string AlignSnapTip(bool hasAlignTarget)
        {
            return Translate(hasAlignTarget
                ? "PersonalChronicle.UI.Layout.SnapTip"
                : "PersonalChronicle.UI.Layout.SnapTipNoTarget");
        }
    }
}
