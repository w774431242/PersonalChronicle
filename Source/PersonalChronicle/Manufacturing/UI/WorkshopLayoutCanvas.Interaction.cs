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
    /// 画布交互：车间块/设备卡拖拽、跟手移动、撤销快照与清空。
    ///
    /// <para>纯物理切片（CHG-MFG-ALIGN-001 · GATE-ARCH ARC-013 处置）：自 WorkshopLayoutCanvas.cs
    /// 原样搬移，**零契约改动** —— 类名、成员可见性、方法签名、存档 schema、翻译键均不变。</para>
    /// </summary>
    internal static partial class WorkshopLayoutCanvas
    {
        // ---- 车间块拖拽 ----

        /// <summary>
        /// 整块车间拖拽。
        ///
        /// <para>⚠ 2026-10-02 审计 P1-2：命中区**不再**用"不绘制任何视觉"的 200×120 锚点框
        /// （那会在空白处产生隐形热区、点一下就误拖车间），改为**该车间实际设备卡的并集**。
        /// 命中设备卡本身时由 <see cref="HandleDeviceCardDrag"/> 优先处理，本方法直接放弃。</para>
        ///
        /// <para>CHG-MFG-ALIGN-001 · M3：MouseDrag 改走 <see cref="ResolveAndMoveZone"/>
        /// （按住 Shift 时整块对齐到邻近对象），替换原来的裸 delta 累加。
        /// MouseUp <b>保持不移动</b>（只清 <c>DragIndex</c>），因此不存在设备卡侧
        /// 「松手瞬间弹回未吸附值」的问题，无需在 MouseUp 重解。</para>
        /// </summary>
        private static void HandleDragAndDrop(Rect rect, List<PaletteEntry> palette, float k,
            Vector2 center, ref CanvasState state, string hitDeviceId)
        {
            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0 && Mouse.IsOver(rect))
            {
                // ⚠ BUG-2026-0001 · F-2：悬停着**设备卡**时**显式拒绝**起拖车间块。
                //   `FindBlockAt` 只排除被悬停的那**一张**卡，同块其余卡仍可命中 ⇒ 若不隔离，
                //   按在卡片上会连带把整块车间拖走。
                //   此前该意图仅靠「卡片 ButtonInvisible 抢走 MouseDown」**被动**成立；
                //   Device.cs 释放按下后必须在此显式编码，否则语义反转。
                state.DragIndex = string.IsNullOrEmpty(hitDeviceId)
                    ? FindBlockAt(state, palette, rect, current.mousePosition, k, center, hitDeviceId)
                    : -1;
                if (state.DragIndex >= 0)
                {
                    Snapshot(ref state);   // 拖拽前存快照（撤销用）
                    state.AlignGuides.Clear();   // 拖拽刚开始，本帧尚无吸附结果 ⇒ 清残线
                }
                return;
            }

            if (current.type == EventType.MouseDrag && state.DragIndex >= 0)
            {
                int zoneIndex = state.DragIndex;
                Vector2 design = state.PlacedPos[zoneIndex];
                Vector2 moved = new Vector2(
                    design.x + current.delta.x / Mathf.Max(0.01f, k),
                    design.y + current.delta.y / Mathf.Max(0.01f, k));
                ResolveAndMoveZone(k, ref state, zoneIndex, palette, moved);
                current.Use();
                return;
            }

            if (current.type == EventType.MouseUp && state.DragIndex >= 0)
            {
                state.LastAction = state.AlignGuides.Count > 0
                    ? Translate("PersonalChronicle.UI.Layout.SnapGuideAligned")
                    : Translate("PersonalChronicle.UI.Layout.Moved");
                state.DragIndex = -1;
                current.Use();
            }
        }

        /// <summary>
        /// 车间块命中：返回 <c>PlacedZoneIds</c> 下标（-1 = 未命中）。
        /// 命中区 = 该车间**实际设备卡**的并集（不含已被拖离的独立设备卡，也不含空车间）。
        ///
        /// <para>CHG-MFG-ALIGN-001 · M3：先用 <see cref="TryGetZoneDeviceBounds"/> 的并集做
        /// **快速排除**，再用逐卡精确测试。两者共用同一份几何与同一条排除真源
        /// （<see cref="IsZoneDeviceAlignable"/>），因此与 M3 的吸附几何<strong>同源</strong>。
        /// </para>
        ///
        /// <para><b>⚠ 逐卡精确测试绝不可用并集 AABB 替代</b>：并集含卡间空隙，
        /// 替换会重新引入 P1-2（空白处隐形热区、点一下就误拖车间）。并集只作 early-out。</para>
        /// </summary>
        private static int FindBlockAt(CanvasState state, List<PaletteEntry> palette, Rect viewport,
            Vector2 mouse, float k, Vector2 center, string hitDeviceId)
        {
            for (int i = state.PlacedZoneIds.Count - 1; i >= 0; i--)
            {
                CanvasAlignRect bounds;
                if (!TryGetZoneDeviceBounds(state, palette, i, out bounds))
                {
                    continue;   // 空车间 / 车间区已不存在（与逐卡循环同源，不可命中）
                }
                // 快速排除：并集矩形（屏幕坐标）都不含鼠标 ⇒ 其中的卡也不可能含。
                Rect unionRect = new Rect(
                    center.x + bounds.MinX * k, center.y + bounds.MinY * k,
                    (bounds.MaxX - bounds.MinX) * k, (bounds.MaxY - bounds.MinY) * k);
                if (!unionRect.Contains(mouse))
                {
                    continue;
                }

                int paletteIndex = PaletteIndexOf(palette, state.PlacedZoneIds[i]);
                if (paletteIndex < 0)
                {
                    continue;
                }
                List<DeviceEntry> devices = palette[paletteIndex].Devices;
                if (devices == null)
                {
                    continue;
                }
                Vector2 design = state.PlacedPos[i];
                for (int d = 0; d < devices.Count; d++)
                {
                    if (!IsZoneDeviceAlignable(devices[d].StableId, hitDeviceId))
                    {
                        continue;
                    }
                    Rect card = DeviceCardBox(center, k, DeviceGridPos(design, d));
                    if (card.Contains(mouse))
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        // ---- 设备卡拖拽（2026-10-02 审计 P1-1：三种上下文此前**全部**无拖动命中）----

        /// <summary>正在拖动的设备卡（stableId；<c>null</c> = 未拖动）。</summary>
        private static string deviceCardDragId;

        /// <summary>拖动抓取点相对卡心的偏移（设计单位）。</summary>
        private static Vector2 deviceCardGrabOffset;

        /// <summary>
        /// 「点击 vs 拖拽」的判定阈值（<b>屏幕</b>像素的平方）。
        /// </summary>
        /// <remarks>
        /// BUG-2026-0001 · S-1 的功能补偿：编排态卡体不再消费按下（见 `Device.cs`），
        /// 于是「按下后几乎没动」必须能被识别为**点击**（开设备详情）而不是被丢弃。
        /// 取 <b>4px</b> —— 与 `WorkshopFloorPlan.Rails` 的 <c>RailClickSlopPx = 4f</c> 同值同义，
        /// 避免「同一次点击在设备库判为选中、在画布判为拖拽」的手感分裂。
        /// </remarks>
        private const float CardClickSlopSq = 16f;   // 4px × 4px

        /// <summary>
        /// 设备卡拖拽。命中卡（含**车间内**的卡）⇒ 按下即把它并入独立设备层并跟手移动，
        /// 松手落档。返回 <c>true</c> = 本帧已由设备卡接管（车间块拖拽不再参与）。
        ///
        /// <para>CHG-MFG-ALIGN-001：MouseDrag 与 MouseUp 统一走
        /// <see cref="ResolveAndMoveDevice"/>（Shift 吸附的唯一入口），保证松手落档值 ≡ 视觉值；
        /// MouseUp 额外补一次 <see cref="SaveDetached"/>，修掉「拖完不关窗就丢位移」的既有缺口。</para>
        /// </summary>
        private static bool HandleDeviceCardDrag(Rect rect, List<PaletteEntry> palette, float k,
            Vector2 center, ref CanvasState state, Vector2 mouseScreen, string hoverDeviceId,
            Vector2 hoverDesign, Vector2 grabOffset, out string clickedId)
        {
            clickedId = null;
            Event current = Event.current;
            if (current == null)
            {
                return deviceCardDragId != null;
            }

            if (current.type == EventType.MouseDown && current.button == 0 && Mouse.IsOver(rect)
                && !string.IsNullOrEmpty(hoverDeviceId))
            {
                Snapshot(ref state);
                deviceCardDragId = hoverDeviceId;
                deviceCardGrabOffset = grabOffset;
                state.CardPressPos = current.mousePosition;   // 供 MouseUp 判「点击 vs 拖拽」
                state.AlignGuides.Clear();   // 拖拽刚开始，本帧尚无吸附结果 ⇒ 清掉上一趟的残线
                // 车间内的卡第一次被拖 ⇒ 并入独立设备层（此后按 stableId 持久化，与"从设备库拖出"同义）。
                if (IndexOfDetached(hoverDeviceId) < 0)
                {
                    DetachedList.Add(new DetachedDevice
                    {
                        StableId = hoverDeviceId,
                        Position = hoverDesign
                    });
                }
                state.DragIndex = -1;
                current.Use();
                return true;
            }

            if (string.IsNullOrEmpty(deviceCardDragId))
            {
                return false;
            }

            if (current.type == EventType.MouseDrag)
            {
                ResolveAndMoveDevice(k, center, ref state, deviceCardDragId, current.mousePosition,
                    palette);
                current.Use();
                return true;
            }

            if (current.type == EventType.MouseUp)
            {
                string id = deviceCardDragId;
                deviceCardDragId = null;
                Vector2 finalDesign = ResolveAndMoveDevice(k, center, ref state, id,
                    current.mousePosition, palette);
                // ⚠ 既有缺口修复（CHG-MFG-ALIGN-001 · R-1）：原实现拖拽全程只改内存，
                //   真正写档要等关窗 MirrorDetachedToSave ⇒ 拖完不关窗就丢位移。
                //   吸附把交互变成「反复微调」，丢位移从偶发变常态 ⇒ 每次松手补写一次。
                //   写入入口自带 |Δ|<0.01 幂等守卫，重复调用零脏写；频率 1 次/拖拽结束（非每帧）。
                SaveDetached(id, finalDesign);
                // BUG-2026-0001 · S-1 的功能补偿：编排态卡体不再消费按下（见 Device.cs 注释），
                //   故「按下后指针几乎没动」要判为**点击**并上报，保留「点卡看详情」入口。
                if (((current.mousePosition - state.CardPressPos).sqrMagnitude)
                    <= CardClickSlopSq)
                {
                    clickedId = id;
                }
                state.LastAction = state.AlignGuides.Count > 0
                    ? Translate("PersonalChronicle.UI.Layout.SnapGuideAligned")
                    : Translate("PersonalChronicle.UI.Layout.DeviceMoved");
                current.Use();
                return true;
            }

            return true;
        }

        /// <summary>跟手移动（**不落档**：拖拽过程每帧都写存档是热路径脏写）。</summary>
        private static void MoveDetached(string stableId, Vector2 design)
        {
            int index = IndexOfDetached(stableId);
            if (index < 0)
            {
                return;
            }
            DetachedDevice moved = DetachedList[index];
            moved.Position = design;
            DetachedList[index] = moved;
        }

        // ---- 撤销（M2：UndoToast 语义，落地为"撤销上一次操作" + 底部文案）----

        /// <summary>撤销快照里的「独立设备」副本（`DetachedList` 本身是静态的 ⇒ 快照也放静态）。</summary>
        private static readonly List<DetachedDevice> UndoDetached = new List<DetachedDevice>();

        /// <summary>
        /// 拍撤销快照（含独立设备副本）。**只有本方法会把 <c>UndoValid</c> 置 true** ——
        /// 撤销按钮的可见性由此单一来源决定（P0-3）。
        /// </summary>
        private static void Snapshot(ref CanvasState state)
        {
            state.UndoOrder.Clear();
            state.UndoOrder.AddRange(state.PlacedZoneIds);
            state.UndoPos.Clear();
            state.UndoPos.AddRange(state.PlacedPos);
            // 独立设备一起进快照：「清空画布」会把它们一起清掉 ⇒ 必须能撤销回来。
            UndoDetached.Clear();
            UndoDetached.AddRange(DetachedList);
            state.UndoValid = true;
        }

        /// <summary>
        /// 建立**基线快照**（读档完成后由宿主调用一次）。
        ///
        /// <para>⚠ 2026-10-02 审计 P0-3：读档只填 <c>PlacedZoneIds</c>、<c>UndoOrder</c> 恒空，
        /// 而旧撤销门禁是 <c>UndoOrder.Count &gt; 0 || PlacedOrder.Count &gt; 0</c>
        /// ⇒ 点一次撤销即把布局全清 + 触发删档。基线快照让"撤销"在读档后第一次点击时
        /// **回退到刚读进来的状态**（安全空操作），而不是抹掉它。</para>
        /// </summary>
        internal static void EstablishBaseline(ref CanvasState state)
        {
            Snapshot(ref state);
        }

        /// <summary>撤销是否可执行（两处按钮门禁的统一来源）。</summary>
        internal static bool CanUndo(CanvasState state)
        {
            return state.UndoValid;
        }

        /// <summary>
        /// 撤销上一步（预览 `UndoToast` 的等价物）。
        /// <c>internal</c>：供 ② 外壳 `#lbar` 的「撤销」按钮调用（与编排页签同一实现）。
        /// </summary>
        internal static void UndoLast(ref CanvasState state)
        {
            if (!state.UndoValid)
            {
                state.LastAction = Translate("PersonalChronicle.UI.Layout.NothingToUndo");
                return;
            }
            state.PlacedZoneIds.Clear();
            state.PlacedZoneIds.AddRange(state.UndoOrder);
            state.PlacedPos.Clear();
            state.PlacedPos.AddRange(state.UndoPos);
            // 只有在**确实持有快照**时才动设备清单：否则会把玩家刚摆好的设备整体抹掉。
            DetachedList.Clear();
            DetachedList.AddRange(UndoDetached);
            // 撤销会收回/恢复设备 ⇒ 位置必须同步进档（否则下次打开又回到撤销前的位置）。
            MirrorDetachedToSave();
            // 快照已消费 ⇒ 再点一次无意义（门禁关闭，玩家不会误触"再抹一遍"）。
            state.UndoValid = false;
            state.LastAction = Translate("PersonalChronicle.UI.Layout.Undone");
        }

        /// <summary>
        /// 清空画布（预览「清空画布」= 全部车间收回托盘 + 独立设备收回，可撤销）。
        /// <c>internal</c>：供 ② 外壳 `#lbar` 调用。
        /// </summary>
        internal static void ClearAll(ref CanvasState state)
        {
            if (state.PlacedZoneIds.Count == 0 && DetachedList.Count == 0)
            {
                return;
            }
            Snapshot(ref state);
            state.PlacedZoneIds.Clear();
            state.PlacedPos.Clear();
            // 「清空画布」是**显式用户动作** ⇒ 这里才可以删存档里的设备项
            // （差集式反删已移除，见 MirrorDetachedToSave 的 P0-1 说明）。
            var removed = new List<string>(DetachedList.Count);
            for (int i = 0; i < DetachedList.Count; i++)
            {
                removed.Add(DetachedList[i].StableId);
            }
            DetachedList.Clear();
            for (int i = 0; i < removed.Count; i++)
            {
                try
                {
                    PersonalChronicle.Application.Manufacturing.ManufacturingLayoutService
                        .RemoveDevice(removed[i]);
                }
                catch (System.Exception ex)
                {
                    Log.ErrorOnce("[PC] 清空画布时删除设备位置失败：" + ex.Message, 20261002);
                }
            }
            state.FlashIndex = -1;
            state.LastAction = Translate("PersonalChronicle.UI.Layout.Cleared");
        }
    }
}
