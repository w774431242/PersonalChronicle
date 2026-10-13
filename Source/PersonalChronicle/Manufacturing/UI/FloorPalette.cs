using UnityEngine;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>
    /// 制造中心平面图 / 设备卡专用色板（集中令牌，消除散落裸 <see cref="Color"/> 字面量；ASSET-002 ·
    /// 2026-09-21 制造审计修复 · CHG-MFG-AUDIT-001 · U-3）。
    ///
    /// <para>半透明背板类（Slab / LegendFill / RailFill / HazardBase）是画布压暗与警示条底色，
    /// 集中命名以便主题切换时单点维护；品质档位色（DeviceTier1/5/6）对应预览 <c>.qh</c> 7 色阶中
    /// 原版未走 <see cref="UITheme"/> 的三档。</para>
    /// </summary>
    internal static class FloorPalette
    {
        /// <summary>平面图板缝背板（半透明压暗）。</summary>
        internal static readonly Color Slab = new Color(0f, 0f, 0f, 0.26f);

        /// <summary>图例背板（半透明压暗）。</summary>
        internal static readonly Color LegendFill = new Color(0f, 0f, 0f, 0.45f);

        /// <summary>侧栏背板（深蓝近黑，预览 .siderail）。</summary>
        internal static readonly Color RailFill = new Color(0.035f, 0.05f, 0.066f, 0.92f);

        /// <summary>黄黑警示条底色（近黑；预览 --hz 斜条）。</summary>
        internal static readonly Color HazardBase = new Color(0.06f, 0.06f, 0.06f, 1f);

        /// <summary>面板级警示斜纹着色（白底强 alpha；<see cref="DrawHazardStrip"/>）。</summary>
        internal static readonly Color HazardTintStrong = new Color(1f, 1f, 1f, 0.85f);

        /// <summary>逐设备警示条着色（白底弱 alpha；设备卡 hazard 条）。</summary>
        internal static readonly Color HazardTint = new Color(1f, 1f, 1f, 0.45f);

        /// <summary>品质档位 1（灰白）。</summary>
        internal static readonly Color DeviceTier1 = new Color(0.6f, 0.64f, 0.68f);

        /// <summary>品质档位 5（紫）。</summary>
        internal static readonly Color DeviceTier5 = new Color(0.85f, 0.55f, 1f);

        /// <summary>品质档位 6（金）。</summary>
        internal static readonly Color DeviceTier6 = new Color(1f, 0.82f, 0.24f);
    }
}
