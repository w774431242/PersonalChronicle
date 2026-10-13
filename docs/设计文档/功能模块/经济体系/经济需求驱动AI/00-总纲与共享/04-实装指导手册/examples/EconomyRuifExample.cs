// Documentation-only specimen. All strings/rows are prepared outside OnGUI.
using RUIF;
using UnityEngine;

namespace PawnEconomy.HandbookExamples
{
    public sealed class EconomyPageExample
    {
        public string[] Tabs = new string[0];
        public string[] Headers = new string[0];
        public string[][] Rows = new string[0][];
        public float[] Shares = new float[0];
        public string EmptyTitle = "";
        public string EmptyExplanation = "";
        public string BalanceLabel = "";
        public string BalanceText = "";
        public string PauseActionLabel = "";
        public bool Ready;
        public bool CommandAllowed;
    }

    public sealed class EconomyRuifRendererExample
    {
        private int tabIndex;

        // Returns an intent. The host dispatches a versioned command afterwards.
        // It must not change balances, goals, or ownership during drawing.
        public bool Draw(Rect area, EconomyPageExample page)
        {
            var column = RUIF.Layout.Column(area, 8f);
            NavAtomsB.LayerSwitch(column.Take(28f), page.Tabs, ref tabIndex);
            if (!page.Ready)
            {
                FeedbackAtoms.EmptyState(column.Rest, page.EmptyTitle, page.EmptyExplanation);
                return false;
            }
            PanelAtoms.StatRow(column.Take(26f), page.BalanceLabel, page.BalanceText);
            bool requestPause = ButtonAtoms.PrimaryButton(column.Take(30f),
                page.PauseActionLabel, page.CommandAllowed);
            // Rows must already be paginated; StatTable supplies no pagination.
            DataAtoms.StatTable(column.Rest, page.Headers, page.Rows, page.Shares, 24f);
            return requestPause;
        }
    }
}
