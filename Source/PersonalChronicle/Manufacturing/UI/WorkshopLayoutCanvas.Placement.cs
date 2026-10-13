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
    /// 车间放置状态操作：是否已放置 / 放置 / 收回 / 放置独立设备（会话内）。
    ///
    /// <para>纯物理切片（CHG-MFG-ALIGN-001 · GATE-ARCH ARC-013 处置）：自 WorkshopLayoutCanvas.cs
    /// 原样搬移，**零契约改动** —— 类名、成员可见性、方法签名、存档 schema、翻译键均不变。</para>
    /// </summary>
    internal static partial class WorkshopLayoutCanvas
    {
        // ---- 状态操作（会话内）----

        /// <summary>该车间是否已放到画布上（按 **ZoneId** 判定，与托盘顺序无关）。</summary>
        private static bool IsPlaced(CanvasState state, int zoneId)
        {
            return state.PlacedZoneIds.Contains(zoneId);
        }

        private static void Place(ref CanvasState state, int zoneId, Vector2 design)
        {
            if (zoneId < 0 || IsPlaced(state, zoneId))
            {
                return;
            }
            Snapshot(ref state);   // 撤销用快照
            state.PlacedZoneIds.Add(zoneId);
            state.PlacedPos.Add(design);
            state.LastAction = Translate("PersonalChronicle.UI.Layout.Placed_Action");
        }

        private static void Unplace(ref CanvasState state, int zoneId)
        {
            int index = state.PlacedZoneIds.IndexOf(zoneId);
            if (index < 0)
            {
                return;
            }
            Snapshot(ref state);
            state.PlacedZoneIds.RemoveAt(index);
            state.PlacedPos.RemoveAt(index);
            state.LastAction = Translate("PersonalChronicle.UI.Layout.Unplaced");
        }

        /// <summary>
        /// 放置一台独立设备（设备库落点 / 点选落点共用），带撤销快照与状态反馈。
        /// </summary>
        private static void PlaceDevice(ref CanvasState state, string stableId, Vector2 design)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return;
            }
            Snapshot(ref state);
            SetDetached(stableId, design);
            state.LastAction = Translate("PersonalChronicle.UI.Layout.DevicePlaced");
        }
    }
}
