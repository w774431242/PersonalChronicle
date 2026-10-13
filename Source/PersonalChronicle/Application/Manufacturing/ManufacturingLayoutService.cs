using System.Collections.Generic;
using PersonalChronicle.Data;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Application.Manufacturing
{
    /// <summary>
    /// 编排布局的**应用层门面**（读/写 <see cref="ChronicleGameComponent"/> 里的持久化布局）。
    ///
    /// <para><b>分层</b>：UI <b>不得</b>直接触碰 <c>Data</c>（ARC-001 单向依赖）。因此读写只经本门面：
    /// <c>UI → Application → Data</c>（Application→Data 是本工程既有方向，如 <c>ArchiveService</c>）。
    /// 布局数据本身是"存档范围内的视图编排状态"，不是玩法数值（不改 Zone/建筑）。</para>
    /// </summary>
    public static class ManufacturingLayoutService
    {
        /// <summary>
        /// 取存档组件；无游戏/无组件时返回 null（调用方须判空，静默降级为"不持久化"）。
        /// <para><b>注意</b>：`ChronicleGameComponent` 是 <b>GameComponent</b>（挂在 <c>Game</c> 上），
        /// **不是** <c>WorldComponent</c> ⇒ 必须用 <c>Current.Game.GetComponent&lt;T&gt;()</c>，
        /// 用 <c>Find.World.GetComponent</c> 会编译不过（CS0311）。</para>
        /// </summary>
        private static ChronicleGameComponent Component
        {
            get
            {
                if (Current.Game == null)
                {
                    return null;
                }
                return Current.Game.GetComponent<ChronicleGameComponent>();
            }
        }

        /// <summary>读出全部布局项到 <paramref name="into"/>（zoneId + 坐标）；无数据时清空。</summary>
        public static void LoadInto(List<int> zoneIds, List<Vector2> positions)
        {
            zoneIds.Clear();
            positions.Clear();
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return;
            }
            List<ChronicleGameComponent.LayoutEntry> list = component.ManufacturingLayout;
            for (int i = 0; i < list.Count; i++)
            {
                ChronicleGameComponent.LayoutEntry entry = list[i];
                if (entry == null || entry.ZoneId < 0)
                {
                    continue;
                }
                zoneIds.Add(entry.ZoneId);
                positions.Add(new Vector2(entry.X, entry.Y));
            }
        }

        /// <summary>写入/更新一项（同 zoneId 覆盖）。</summary>
        public static void Save(int zoneId, Vector2 position)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return;
            }
            component.SetLayoutEntry(zoneId, position.x, position.y);
        }

        // ---- 设备卡布局（2026-09-26：独立设备位置随存档）--------------------

        /// <summary>读出全部设备布局项（stableId + 坐标）；无数据时清空。</summary>
        public static void LoadDevicesInto(List<string> stableIds, List<Vector2> positions)
        {
            stableIds.Clear();
            positions.Clear();
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return;
            }
            List<ChronicleGameComponent.DeviceLayoutEntry> list = component.ManufacturingDeviceLayout;
            for (int i = 0; i < list.Count; i++)
            {
                ChronicleGameComponent.DeviceLayoutEntry entry = list[i];
                if (entry == null || string.IsNullOrEmpty(entry.StableId))
                {
                    continue;
                }
                stableIds.Add(entry.StableId);
                positions.Add(new Vector2(entry.X, entry.Y));
            }
        }

        /// <summary>读出档里已存的设备稳定 ID 集（用于"删掉已收回的项"）。</summary>
        public static void LoadDeviceIds(List<string> into)
        {
            into.Clear();
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return;
            }
            List<ChronicleGameComponent.DeviceLayoutEntry> list = component.ManufacturingDeviceLayout;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && !string.IsNullOrEmpty(list[i].StableId))
                {
                    into.Add(list[i].StableId);
                }
            }
        }

        /// <summary>写入/更新一台设备的位置（同 stableId 覆盖）。</summary>
        public static void SaveDevice(string stableId, Vector2 position)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return;
            }
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return;
            }
            component.SetDeviceLayoutEntry(stableId, position.x, position.y);
        }

        /// <summary>移除一台设备的位置记录（收回车间 / 清空画布）。</summary>
        public static void RemoveDevice(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return;
            }
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return;
            }
            component.RemoveDeviceLayoutEntry(stableId);
        }

        /// <summary>移除一项。</summary>
        public static void Remove(int zoneId)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return;
            }
            component.RemoveLayoutEntry(zoneId);
        }
    }
}
