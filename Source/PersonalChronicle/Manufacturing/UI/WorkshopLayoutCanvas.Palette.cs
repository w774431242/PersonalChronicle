using System.Collections.Generic;
using RimWorld;
using Verse;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Application.Workshop;

namespace PersonalChronicle.Manufacturing.UI
{
    internal static partial class WorkshopLayoutCanvas
    {
        /// <summary>
        /// 收集托盘条目（真实车间区）。
        ///
        /// <para>经 Application 层受控契约 <see cref="WorkshopPaletteAccess"/> 取数，
        /// 不再直读 Automation/Domain 内部实现（GOV-005 分层红线；2026-09-21 制造审计修复 ·
        /// CHG-MFG-AUDIT-001 · A-1）。内部读取（2 秒节流扫描 / 设备读数 / 当班岗位 /
        /// 技能评分 / 生产配方）统一收敛到 Automation 侧 <c>WorkshopPaletteAccessBinder</c>。</para>
        ///
        /// <para>降级：契约未注入 / map 为空 / 委托异常 → 空托盘（不影响绘制）。</para>
        /// </summary>
        internal static void CollectPalette(Map map, List<PaletteEntry> into)
        {
            WorkshopPaletteAccess.Collect(map, into);
        }
    }
}
