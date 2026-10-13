using System;
using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 系统中心注册表（CenterRegistry，规范书 §2）：调度层与 UI 只认注册表，不认具体实现。
    ///
    /// <para>注册表本身<b>无状态语义</b>：它只把 <see cref="SystemCenterDef"/> 按
    /// 「预留 / 初始 × 档位升序」分区排序后供读取。排序规则集中在
    /// <see cref="Partition"/> 这一纯函数里（无引擎依赖，可离线单测），
    /// <see cref="All"/> 等属性只负责从 DefDatabase 取源并缓存。</para>
    ///
    /// <para>缓存失效靠 Def 数量比对：Def 只在启动期加载，数量变化即视为重载
    /// （重开档 / 开发期热改 Def 列表），此时重建。</para>
    /// </summary>
    public static class SystemCenterRegistry
    {
        private static readonly List<SystemCenterDef> cachedAll = new List<SystemCenterDef>();
        private static readonly List<SystemCenterDef> cachedInitial = new List<SystemCenterDef>();
        private static readonly List<SystemCenterDef> cachedReserved = new List<SystemCenterDef>();
        private static int cachedSourceCount = -1;

        /// <summary>全部已注册中心（档位升序）。</summary>
        public static IReadOnlyList<SystemCenterDef> All
        {
            get { EnsureBuilt(); return cachedAll; }
        }

        /// <summary>初始中心（<see cref="SystemCenterDef.reserved"/> == false）。</summary>
        public static IReadOnlyList<SystemCenterDef> InitialCenters
        {
            get { EnsureBuilt(); return cachedInitial; }
        }

        /// <summary>预留中心（ID 与档位已登记，玩法待对齐）。</summary>
        public static IReadOnlyList<SystemCenterDef> ReservedCenters
        {
            get { EnsureBuilt(); return cachedReserved; }
        }

        /// <summary>按稳定 defName 取中心；不存在返回 null（不抛异常）。</summary>
        public static SystemCenterDef GetNamed(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return null;
            }
            try
            {
                return DefDatabase<SystemCenterDef>.GetNamedSilentFail(defName);
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Ui, "SystemCenter lookup failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>丢弃缓存，下次访问重新从 DefDatabase 构建。</summary>
        public static void Invalidate()
        {
            cachedSourceCount = -1;
        }

        /// <summary>
        /// 纯函数：把任意中心集合分区（初始 / 预留）并排序。
        /// 排序规则 = 档位升序（数值小者先，规范书 §3.4），同档位按 defName 序
        /// 保证顺序稳定（跨语言、跨载入一致）。
        /// 单测入口：不触碰 DefDatabase，可在无引擎环境运行。
        /// </summary>
        public static void Partition(
            IEnumerable<SystemCenterDef> source,
            List<SystemCenterDef> all,
            List<SystemCenterDef> initial,
            List<SystemCenterDef> reserved)
        {
            if (all == null || initial == null || reserved == null)
            {
                return;
            }
            all.Clear();
            initial.Clear();
            reserved.Clear();
            if (source == null)
            {
                return;
            }

            foreach (SystemCenterDef def in source)
            {
                if (def == null)
                {
                    continue;
                }
                all.Add(def);
                if (def.reserved)
                {
                    reserved.Add(def);
                }
                else
                {
                    initial.Add(def);
                }
            }

            all.Sort(CompareCenters);
            initial.Sort(CompareCenters);
            reserved.Sort(CompareCenters);
        }

        private static int CompareCenters(SystemCenterDef a, SystemCenterDef b)
        {
            int byTier = a.priorityTier.CompareTo(b.priorityTier);
            if (byTier != 0)
            {
                return byTier;
            }
            return string.CompareOrdinal(a.defName ?? string.Empty, b.defName ?? string.Empty);
        }

        private static void EnsureBuilt()
        {
            List<SystemCenterDef> source;
            try
            {
                source = DefDatabase<SystemCenterDef>.AllDefsListForReading;
            }
            catch (Exception ex)
            {
                // DefDatabase 在启动完成后才可用；早期访问按「无中心」处理，不抛给 UI。
                ChronicleLog.Warning(ChronicleLog.Category.Ui, "SystemCenter registry unavailable: " + ex.Message);
                source = null;
            }

            int count = source != null ? source.Count : 0;
            if (cachedSourceCount == count)
            {
                return;
            }

            Partition(source, cachedAll, cachedInitial, cachedReserved);
            cachedSourceCount = count;
        }
    }
}
