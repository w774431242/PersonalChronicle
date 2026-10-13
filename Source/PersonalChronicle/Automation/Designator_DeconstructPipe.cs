using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 「拆除物流管道」设计器（PM 2026-09-07：隐藏式管道全透明，拆除时完全不可见；
    /// 参考原版「拆除电缆」的覆盖显示思路——激活设计器即高亮显示全部管道段）。
    ///
    /// 与原版 Designator_DeconstructConduit 的对照：
    /// - 原版电缆：CanDesignateThing 按 isPowerConduit 判定 + SelectedUpdate 强制
    ///   电力网格覆盖层本帧显示（OverlayDrawHandler）；
    /// - 我们：管道已回归纯物流（不在电网层），改为 <b>SelectedUpdate 即时绘制</b>
    ///   金黄半透明 plane 高亮全部管道段（Graphics.DrawMesh 每帧即时绘制，不进
    ///   section 缓存、无需 dirty；与原版 RenderAnticipatedWirePieceConnecting 同技法）。
    ///
    /// <para><b>[StaticConstructorOnStartup]</b>（2026-10-14 清理）：本类持有
    /// <c>Material highlightMat</c> 静态字段，必须在主线程加载，否则 Player.log 刷
    /// "Type Designator_DeconstructPipe probably needs a StaticConstructorOnStartup attribute"。</para>
    /// </summary>
    [StaticConstructorOnStartup]
    public class Designator_DeconstructPipe : Designator_Deconstruct
    {
        private static Material highlightMat;

        /// <summary>高亮材质：金黄半透明（管道标识色），惰性构建。</summary>
        private static Material HighlightMat
        {
            get
            {
                if (highlightMat == null)
                {
                    highlightMat = MaterialPool.MatFrom(
                        SolidColorMaterials.NewSolidColorTexture(new Color(1f, 0.82f, 0.25f, 0.5f)),
                        ShaderDatabase.Transparent, Color.white);
                }
                return highlightMat;
            }
        }

        public Designator_DeconstructPipe()
        {
            defaultLabel = "PersonalChronicle.Automation.Pipe.Deconstruct".Translate();
            defaultDesc = "PersonalChronicle.Automation.Pipe.DeconstructDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/Deconstruct", false) ?? TexButton.OpenStatsReport;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            useMouseIcon = true;
            soundSucceeded = SoundDefOf.Designate_Deconstruct;
            hotKey = null;
        }

        /// <summary>只允许拆除本 mod 的管道段（可见 + 隐藏式，thingClass 统一）。</summary>
        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            if (!(t is Building_PipeSegment))
            {
                return false;
            }
            return base.CanDesignateThing(t);
        }

        /// <summary>设计器激活期间：本帧高亮当前地图全部管道段（含隐藏式——拆除时可见）。</summary>
        public override void SelectedUpdate()
        {
            base.SelectedUpdate();
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            MapComponent_PipeNetwork net = MapComponent_PipeNetwork.Get(map);
            IReadOnlyList<Building_PipeSegment> segments = net.AllSegments;
            Material mat = HighlightMat;
            Camera camera = Find.Camera;
            if (camera == null)
            {
                return;
            }
            for (int i = 0; i < segments.Count; i++)
            {
                Building_PipeSegment seg = segments[i];
                if (seg == null || !seg.Spawned || seg.Map != map)
                {
                    continue;
                }
                // 即时绘制（不进 section 缓存）：仅设计器激活帧渲染，取消即消失。
                Graphics.DrawMesh(
                    MeshPool.plane10,
                    seg.TrueCenter(),
                    Quaternion.identity,
                    mat,
                    0);
            }
        }
    }
}
