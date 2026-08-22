using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// Shared base for the archive window's per-view panels. Extracted from the
    /// former god-class <c>ArchiveMainTabWindow</c> (BUG-BASE-01 / BASE-005) so
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
        /// Z-shaped 5-segment orthogonal link between two node centres. The vertical
        /// segments align to each card's X midpoint; corners round with zoom so the
        /// line never looks spiky when the social graph is enlarged. Pure geometry —
        /// no window state, safe to call from any panel.
        /// </summary>
        public static void DrawOrthogonalLink(Vector2 center, Vector2 nodeCenter, Color color, float thickness, float zoom = 1f)
        {
            float sx = Mathf.Sign(nodeCenter.x - center.x);
            float sy = Mathf.Sign(nodeCenter.y - center.y);
            float midY = (center.y + nodeCenter.y) / 2f;
            float radius = 10f * Mathf.Max(0.4f, zoom);

            Vector2 v1Start = new Vector2(center.x, center.y);
            Vector2 v1End = new Vector2(center.x, midY - sy * radius);
            Vector2 c1End = new Vector2(center.x + sx * radius, midY);
            Vector2 hEnd = new Vector2(nodeCenter.x - sx * radius, midY);
            Vector2 c2End = new Vector2(nodeCenter.x, midY + sy * radius);
            Vector2 v2End = new Vector2(nodeCenter.x, nodeCenter.y);

            Widgets.DrawLine(v1Start, v1End, color, thickness);
            Widgets.DrawLine(v1End, c1End, color, thickness);
            Widgets.DrawLine(c1End, hEnd, color, thickness);
            Widgets.DrawLine(hEnd, c2End, color, thickness);
            Widgets.DrawLine(c2End, v2End, color, thickness);
        }

        /// <summary>
        /// Snapshot the three mutable GUI state fields so a panel's drawing can be
        /// wrapped in try/finally and always restored — the v3.0 UI pairing rule.
        /// </summary>
        protected static (Color color, GameFont font, TextAnchor anchor) CaptureGuiState()
        {
            return (GUI.color, Text.Font, Text.Anchor);
        }

        /// <summary>Restore the three mutable GUI state fields captured earlier.</summary>
        protected static void RestoreGuiState((Color color, GameFont font, TextAnchor anchor) state)
        {
            GUI.color = state.color;
            Text.Font = state.font;
            Text.Anchor = state.anchor;
        }
    }
}
