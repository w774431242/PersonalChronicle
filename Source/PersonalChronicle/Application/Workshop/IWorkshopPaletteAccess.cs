using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using PersonalChronicle.Application.ReadModels;

namespace PersonalChronicle.Application.Workshop
{
    /// <summary>
    /// 「车间托盘」受控访问契约（Application 层静态门面）。
    ///
    /// <para><b>为什么存在</b>：制造中心 UI 曾经直接调用 Automation/Domain 内部实现
    /// （<c>WorkshopZoneUtility</c> / <c>WorkshopDeviceReader</c>(internal) / <c>WorkshopStats</c> /
    /// <c>MapComponent_WorkshopManager</c> / <c>WorkshopDutyScoring</c>），违反 GOV-005 分层红线
    /// （UI → Automation/Domain 直读）。本契约把这些内部读取收敛为 Application 层受控静态钩子，
    /// 实现由 Automation 在启动时经 <c>WorkshopPaletteAccessBinder</c> 注入（ARC-008，与
    /// <c>WorkshopBonusAccess</c> 同范式；2026-09-21 制造审计修复 · CHG-MFG-AUDIT-001 · A-1）。</para>
    ///
    /// <para><b>降级</b>：未注入 / <paramref name="map"/> 为空 / 委托异常 → 清空 <paramref name="into"/>
    /// （空托盘），不影响绘制（PERF-001 失败隔离）。</para>
    /// </summary>
    internal static class WorkshopPaletteAccess
    {
        private static Action<Map, List<PaletteEntry>> collectSource;
        private static Action clearCacheSource;

        /// <summary>绑定实现委托（幂等；两个委托均可为 null）。</summary>
        public static void SetSource(Action<Map, List<PaletteEntry>> collect, Action clearCache)
        {
            collectSource = collect;
            clearCacheSource = clearCache;
        }

        /// <summary>清空 2 秒扫描缓存（换档 / 关窗时调用，避免跨局残留；委托未注入则空操作）。</summary>
        public static void ClearCache()
        {
            clearCacheSource?.Invoke();
        }

        /// <summary>从地图收集托盘条目（真实车间区）。未注入 / map 空 / 异常 → into 清空。</summary>
        public static void Collect(Map map, List<PaletteEntry> into)
        {
            into.Clear();
            Action<Map, List<PaletteEntry>> fn = collectSource;
            if (fn == null || map == null)
            {
                return;
            }
            try
            {
                fn(map, into);
            }
            catch (Exception)
            {
                into.Clear();
            }
        }
    }
}
