using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>Immutable full record snapshot. Resizing and scrolling have no runtime command references.</summary>
    internal sealed class Window_LaborRecord : Window
    {
        private readonly string record;
        private readonly string title;
        private Vector2 scroll;
        private float measuredWidth = -1f, measuredHeight;
        private string flowed;
        public override Vector2 InitialSize => new Vector2(760f, 640f);
        internal Window_LaborRecord(string record)
        {
            this.record = record ?? string.Empty;
            title = LaborPresentationRows.Text("Record");
            doCloseX = true; draggable = true; resizeable = true;
            absorbInputAroundWindow = true; closeOnClickedOutside = false;
        }
        public override void DoWindowContents(Rect inRect)
        {
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            Color oldColor = GUI.color;
            try { DrawRecord(inRect); }
            finally
            {
                Text.Font = oldFont; Text.Anchor = oldAnchor; Text.WordWrap = oldWrap; GUI.color = oldColor;
            }
        }
        private void DrawRecord(Rect inRect)
        {
            float header = RUIF.Measure.RowHeight(GameFont.Medium) + RUIF.Tokens.SpaceMd;
            RUIF.Draw.Label(new Rect(inRect.x, inRect.y, inRect.width, header), title, GameFont.Medium, RUIF.Tokens.Text, TextAnchor.UpperLeft);
            var body = new Rect(inRect.x, inRect.y + header, inRect.width, Mathf.Max(1f, inRect.height - header));
            float width = Mathf.Max(1f, body.width - RUIF.Tokens.ScrollbarThickness - RUIF.Tokens.SpaceXs);
            if (!Mathf.Approximately(width, measuredWidth))
            {
                measuredWidth = width;
                flowed = NeedDashboardPanel.Flow(record, width, GameFont.Small);
                measuredHeight = NeedDashboardPanel.Wrapped(flowed, width, GameFont.Small);
            }
            Widgets.BeginScrollView(body, ref scroll, new Rect(0f, 0f, width, Mathf.Max(body.height, measuredHeight)));
            try { RUIF.Draw.Label(new Rect(0f, 0f, width, measuredHeight), flowed, GameFont.Small, RUIF.Tokens.Text, TextAnchor.UpperLeft); }
            finally { Widgets.EndScrollView(); }
        }
    }
}
