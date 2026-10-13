using PersonalChronicle.MasterControl.Population;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// Shared base for the archive window's per-view panels. Extracted from the
    /// former god-class <c>PersonnelArchivePanel</c> (BUG-BASE-01 / BASE-005) so
    /// that pure drawing helpers and state-recovery scaffolding live in one place
    /// instead of being copy-pasted across every panel.
    ///
    /// Panels receive their data through <c>IArchiveService</c> + the read-model
    /// snapshots and must not reach into the window's private navigation/scroll
    /// state directly — those stay on the shell and are passed in as arguments.
    /// </summary>
    public abstract class ArchivePanelBase
    {
        /// <summary>
        /// Family-tree link between two cards. Endpoints sit on card edges (never
        /// centres). Axis-aligned pairs collapse to a single segment; offset pairs
        /// use an L through the gap so the line does not cut through a third card.
        /// No 45° chamfer — Widgets.DrawLine diagonals read as arrowheads.
        /// <paramref name="doubleStroke"/> draws a parallel pair (spouse).
        /// </summary>
        public static void DrawFamilyTreeLink(Rect from, Rect to, Color color, float thickness, bool doubleStroke)
        {
            const float axisEpsilon = 6f;
            float dx = to.center.x - from.center.x;
            float dy = to.center.y - from.center.y;
            bool sameCol = Mathf.Abs(dx) < axisEpsilon;
            bool sameRow = Mathf.Abs(dy) < axisEpsilon;

            if (sameCol)
            {
                Vector2 a;
                Vector2 b;
                if (dy < 0f)
                {
                    a = new Vector2(from.center.x, from.y);
                    b = new Vector2(to.center.x, to.yMax);
                }
                else
                {
                    a = new Vector2(from.center.x, from.yMax);
                    b = new Vector2(to.center.x, to.y);
                }
                Stroke(a, b, color, thickness, doubleStroke, new Vector2(2.5f, 0f));
                return;
            }

            if (sameRow)
            {
                Vector2 a;
                Vector2 b;
                if (dx < 0f)
                {
                    a = new Vector2(from.x, from.center.y);
                    b = new Vector2(to.xMax, to.center.y);
                }
                else
                {
                    a = new Vector2(from.xMax, from.center.y);
                    b = new Vector2(to.x, to.center.y);
                }
                Stroke(a, b, color, thickness, doubleStroke, new Vector2(0f, 2.5f));
                return;
            }

            // Offset relative: vertical from the facing edge of `from` to the
            // facing y of `to`, then horizontal along that edge. Gap walk, no midY.
            if (dy < 0f)
            {
                Vector2 a = new Vector2(from.center.x, from.y);
                Vector2 corner = new Vector2(from.center.x, to.yMax);
                Vector2 b = new Vector2(to.center.x, to.yMax);
                Segment(a, corner, color, thickness);
                Segment(corner, b, color, thickness);
            }
            else
            {
                Vector2 a = new Vector2(from.center.x, from.yMax);
                Vector2 corner = new Vector2(from.center.x, to.y);
                Vector2 b = new Vector2(to.center.x, to.y);
                Segment(a, corner, color, thickness);
                Segment(corner, b, color, thickness);
            }
        }

        /// <summary>
        /// **裁剪/矩阵安全的线段**：沿线用小方块串联（与 <c>WorkshopLayoutCanvas.DrawPolyline</c> 同法）。
        ///
        /// <para>为什么不用两点版 <c>Widgets.DrawLine(a, b, …)</c>：它用 <c>Matrix4x4.TRS</c> + GL
        /// 画旋转矩形，**不感知 <c>GUI.matrix</c>**（本项目 2026-09-13 反编译取证）⇒ 一旦落进
        /// <c>GUI.BeginGroup</c> / <c>GUI.BeginClip</c> 作用域就整体错位（偏移 = 组/裁剪框原点）。
        /// 档案馆现在会被总控中心**嵌入绘制**（在自己的组里），故连线一律走本实现：
        /// <c>Widgets.DrawBoxSolid</c> 走 GUI 贴图路径，天然跟随矩阵与裁剪。
        /// 只服务家族树连线，段数 = 长度 / 步长（步长 ≥ 2px），量级个位数～数十，不构成热路径负担。</para>
        /// </summary>
        private static void Segment(Vector2 a, Vector2 b, Color color, float thickness)
        {
            float half = Mathf.Max(0.5f, thickness * 0.5f);
            float length = Vector2.Distance(a, b);
            if (length < 0.5f)
            {
                Widgets.DrawBoxSolid(new Rect(a.x - half, a.y - half, half * 2f, half * 2f), color);
                return;
            }
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(2f, half * 2f)));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                Widgets.DrawBoxSolid(new Rect(p.x - half, p.y - half, half * 2f, half * 2f), color);
            }
        }

        private static void Stroke(Vector2 a, Vector2 b, Color color, float thickness, bool doubleStroke, Vector2 offset)
        {
            if (doubleStroke)
            {
                float t = thickness < 1.5f ? 1f : thickness * 0.7f;
                Segment(a + offset, b + offset, color, t);
                Segment(a - offset, b - offset, color, t);
            }
            else
            {
                Segment(a, b, color, thickness);
            }
        }

        /// <summary>
        /// Snapshot the four mutable GUI state fields so a panel's drawing can be
        /// wrapped in try/finally and always restored — the v3.0 UI pairing rule.
        /// v4.17 体检：补 Text.WordWrap（ShadowLabel 会修改它，旧快照遗漏）。
        /// </summary>
        /// <summary>
        /// Snapshot the four mutable GUI state fields so a panel's drawing can be
        /// wrapped in try/finally and always restored — the v3.0 UI pairing rule.
        /// v4.17 体检：补 Text.WordWrap（ShadowLabel 会修改它，旧快照遗漏）。
        /// 公开为 static 工具，供非继承的 IMGUI 窗口（如 Dialog_OverviewFields）复用同一配对约定。
        /// </summary>
        public static (Color color, GameFont font, TextAnchor anchor, bool wordWrap) CaptureGuiState()
        {
            return (GUI.color, Text.Font, Text.Anchor, Text.WordWrap);
        }

        /// <summary>Restore the mutable GUI state fields captured earlier.</summary>
        public static void RestoreGuiState((Color color, GameFont font, TextAnchor anchor, bool wordWrap) state)
        {
            GUI.color = state.color;
            Text.Font = state.font;
            Text.Anchor = state.anchor;
            Text.WordWrap = state.wordWrap;
        }
    }
}
