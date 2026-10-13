using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 「工序/岗位标准表」的<b>反查索引</b>（`CHG-20261003-WORKSTATION`）。
    ///
    /// <para><b>为什么需要它</b>：L1 设备联动要回答「这台设备属于哪些工序行」。若每次都遍历
    /// 全部 <see cref="WorkshopProcessDef"/> 再线性比 <c>expectedDeviceDefNames</c>，就是
    /// O(设备数 × 工序行数 × 设备名数) 的三重循环 —— 属 PERF-001「热路径无全库扫描」。
    /// 这里在首次使用时建一次 <b>设备 defName → 工序行</b> 字典，之后 O(1) 命中。</para>
    ///
    /// <para><b>缓存失效</b>：Def 是<b>进程内静态数据</b>（游戏启动时一次性加载，存档不改变它）
    /// ⇒ 索引建一次即可，<b>无需失效逻辑</b>。但仍提供 <see cref="Invalidate"/> 以备将来
    /// 运行时重载 Def（DevMode 热重载）时调用 —— 宁可留一个显式口子，也不让"缓存不可失效"
    /// 成为后来者的隐性陷阱（ARC-004 缓存可重建）。</para>
    ///
    /// <para><b>纯查询层</b>：本类只做 Def → Def 索引，<b>不碰任何 pawn 状态</b>
    /// （写入纪律见 <c>MapComponent_WorkshopManager.WorkDemote.cs</c>）。</para>
    /// </summary>
    public static class WorkshopProcessCatalog
    {
        private static readonly Dictionary<string, List<WorkshopProcessDef>> ByDevice =
            new Dictionary<string, List<WorkshopProcessDef>>();

        private static readonly List<WorkshopProcessDef> EmptyRows = new List<WorkshopProcessDef>();

        /// <summary>索引是否已建（诊断 / 单测用）。</summary>
        public static bool IsBuilt
        {
            get { return ByDevice.Count > 0 || HasNoRows(); }
        }

        private static bool HasNoRows()
        {
            List<WorkshopProcessDef> all = DefDatabase<WorkshopProcessDef>.AllDefsListForReading;
            return all == null || all.Count == 0;
        }

        /// <summary>清空索引（Def 热重载后调用；下次查询自动重建）。</summary>
        public static void Invalidate()
        {
            ByDevice.Clear();
        }

        /// <summary>
        /// 取「以该设备为锚点」的工序行（永不为 null；无命中返回空表）。
        /// </summary>
        public static List<WorkshopProcessDef> ProcessesForDevice(string deviceDefName)
        {
            if (string.IsNullOrEmpty(deviceDefName))
            {
                return EmptyRows;
            }
            EnsureBuilt();
            List<WorkshopProcessDef> rows;
            return ByDevice.TryGetValue(deviceDefName, out rows) && rows != null
                ? rows
                : EmptyRows;
        }

        private static void EnsureBuilt()
        {
            if (ByDevice.Count > 0)
            {
                return;
            }
            List<WorkshopProcessDef> all = DefDatabase<WorkshopProcessDef>.AllDefsListForReading;
            if (all == null)
            {
                return;
            }
            for (int i = 0; i < all.Count; i++)
            {
                WorkshopProcessDef row = all[i];
                if (row == null)
                {
                    continue;
                }
                List<string> devices = row.DeviceNames();
                for (int d = 0; d < devices.Count; d++)
                {
                    string defName = devices[d];
                    if (string.IsNullOrEmpty(defName))
                    {
                        continue;
                    }
                    List<WorkshopProcessDef> bucket;
                    if (!ByDevice.TryGetValue(defName, out bucket) || bucket == null)
                    {
                        bucket = new List<WorkshopProcessDef>();
                        ByDevice[defName] = bucket;
                    }
                    if (!bucket.Contains(row))
                    {
                        bucket.Add(row);
                    }
                }
            }
        }
    }
}