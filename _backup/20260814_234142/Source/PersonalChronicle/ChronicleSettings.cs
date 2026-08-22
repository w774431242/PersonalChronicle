using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle
{
    /// <summary>
    /// Mod settings. Persisted via ModSettings; values are read by the service
    /// layer (recording toggle, per-pawn event cap). All labels go through
    /// translation keys.
    /// </summary>
    public sealed class ChronicleSettings : ModSettings
    {
        public bool EnableRecording = true;
        public int MaxEventsPerPawn = 200;
        public bool DebugLiveCount = false;
        /// <summary>Active UI theme id (see <see cref="UITheme"/> presets).</summary>
        public string ThemeId = UITheme.ThemeEpic;

        private const float EventsPerPawnSliderMin = 10f;
        private const float EventsPerPawnSliderMax = 1000f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref EnableRecording, "enableRecording", true);
            Scribe_Values.Look(ref MaxEventsPerPawn, "maxEventsPerPawn", 200);
            Scribe_Values.Look(ref DebugLiveCount, "debugLiveCount", false);
            Scribe_Values.Look(ref ThemeId, "themeId", UITheme.ThemeEpic);
        }

        public void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("PersonalChronicle.Settings.EnableRecording".Translate().ToString(), ref EnableRecording);
            listing.CheckboxLabeled("PersonalChronicle.Settings.DebugLiveCount".Translate().ToString(), ref DebugLiveCount);
            listing.Label("PersonalChronicle.Settings.MaxEventsPerPawn".Translate().ToString() + ": " + MaxEventsPerPawn, -1f);
            MaxEventsPerPawn = (int)listing.Slider((float)MaxEventsPerPawn, EventsPerPawnSliderMin, EventsPerPawnSliderMax);

            // v1.1.4: UI theme selector (mirrors the in-window header button).
            listing.Gap(8f);
            listing.Label("PersonalChronicle.Settings.Theme".Translate().ToString(), -1f);
            int themeIdx = System.Array.IndexOf(UITheme.ThemeIds, ThemeId);
            if (themeIdx < 0) themeIdx = 0;
            for (int i = 0; i < UITheme.ThemeIds.Length; i++)
            {
                string label = UITheme.GetDisplayName(UITheme.ThemeIds[i]);
                bool chosen = listing.RadioButton(label, i == themeIdx);
                if (chosen && i != themeIdx)
                {
                    ThemeId = UITheme.ThemeIds[i];
                    UITheme.Apply(ThemeId);
                }
            }
            listing.End();
        }
    }
}
