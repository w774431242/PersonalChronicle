using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间区查询入口。后续加工设备 / 传送带判断「我属于哪个车间」一律走这里，
    /// 禁止各处自己遍历 <c>zoneManager.AllZones</c>（避免判断口径漂移）。
    /// </summary>
    public static class WorkshopZoneUtility
    {
        /// <summary>
        /// 返回覆盖该格的车间；不在任何车间内则为 null。
        ///
        /// <para><b>两级判定（自持格集，见 EXC-2026-005）</b>：</para>
        /// <list type="number">
        /// <item><b>快路径</b>：原版格子网格命中车间 ⇒ 直接返回（O(1)，无重叠时的权威，
        /// 也是绝大多数调用走的分支）；</item>
        /// <item><b>慢路径</b>：网格没指向车间（被仓库区/种植区等**后圈定**的 zone 覆盖，
        /// 或该格已不属于任何 zone）⇒ 回退查各车间**自持格集**。倒序遍历
        /// <c>AllZones</c>（≈ 登记顺序）= 最近登记者优先，与原版
        /// <c>ZoneManager.AddZoneGridCell</c>"后者覆盖 zoneGrid"的语义保持一致。</item>
        /// </list>
        ///
        /// <para>性能：慢路径 = 车间数 × O(1) 哈希查找（车间通常个位数），且只在重叠格上触发；
        /// 快路径仍是一次数组索引。禁止在每帧入口反复调用（调用方按需缓存）。</para>
        /// </summary>
        public static Zone_Workshop WorkshopAt(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map))
            {
                return null;
            }
            if (map.zoneManager.ZoneAt(cell) is Zone_Workshop direct)
            {
                return direct;
            }
            List<Zone> zones = map.zoneManager.AllZones;
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                if (zones[i] is Zone_Workshop workshop && workshop.OwnsCell(cell))
                {
                    return workshop;
                }
            }
            return null;
        }

        /// <summary>
        /// 班次当前「预计可上岗人数」（2026-10-02 审计 P1-B 的<b>唯一实现</b>）：与派工同源 ——
        /// 多班次车间未指派 = 0（空岗），不再回落到全员。
        ///
        /// <para><b>为什么收敛到这里</b>：名额展示（甘特图 / 覆盖条 / 运营面板）此前各自读
        /// <c>WorkshopShift.EffectivePool</c>（池空无条件回落全员），与派工口径
        /// （<c>MapComponent_WorkshopManager.FallbackPool</c>：仅单班次车间回落全员）不一致，
        /// 出现「实际空岗 0 人、界面显示满员」。展示侧一律走本方法。</para>
        ///
        /// <para>⚠ 内部会构建临时字典 —— 仅供 Refresh / 低频展示路径调用，<b>不得</b>进绘制热路径。</para>
        /// </summary>
        public static int ShiftStaffingOf(WorkshopShift shift)
        {
            if (shift == null)
            {
                return 0;
            }
            Zone_Workshop zone = shift.TargetZone;
            Map map = zone != null ? zone.Map : null;
            MapComponent_WorkshopManager manager = map != null ? MapComponent_WorkshopManager.Get(map) : null;
            return manager != null ? manager.ShiftStaffingOf(shift) : 0;
        }

        /// <summary>
        /// 车间是否仍然存活（**判活唯一入口**，防各处口径漂移）：非 null、Map 非空、仍有格、
        /// 且仍在 <c>ZoneManager.AllZones</c> 里。
        ///
        /// <para><b>为什么不能再用 <c>TargetZone.Map == null</c></b>：原版 <c>Zone.Deregister()</c>
        /// 只从 allZones 移除，**不置空 <c>zone.zoneManager</c>** ⇒ 已注销的 zone 其 <c>Map</c>
        /// 仍非 null，判死会失败（实机表现：删掉车间后管理弹窗不关、排班/轮岗残留不被清理）。</para>
        /// </summary>
        public static bool IsAlive(Zone_Workshop zone)
        {
            if (zone == null || zone.Map == null || zone.CellCount == 0)
            {
                return false;
            }
            return zone.Map.zoneManager.AllZones.Contains(zone);
        }

        /// <summary>枚举地图上的全部车间。</summary>
        public static IEnumerable<Zone_Workshop> AllWorkshops(Map map)
        {
            if (map == null)
            {
                yield break;
            }
            List<Zone> zones = map.zoneManager.AllZones;
            for (int i = 0; i < zones.Count; i++)
            {
                Zone_Workshop workshop = zones[i] as Zone_Workshop;
                if (workshop != null)
                {
                    yield return workshop;
                }
            }
        }
    }
}
