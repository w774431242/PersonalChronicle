using Verse;

namespace PersonalChronicle
{
    /// <summary>
    /// Centralized logging wrapper that enforces the v3.0 BASE-009 convention:
    /// every line is prefixed with <c>[PersonalChronicle][Category]</c> so Player.log
    /// can be filtered by mod and severity. Replaces the previously scattered
    /// <c>Log.Warning("PersonalChronicle: ...")</c> calls (BUG-BASE-02).
    /// </summary>
    public static class ChronicleLog
    {
        /// <summary>Well-known log categories used across the mod.</summary>
        public static class Category
        {
            public const string Capture = "Capture";
            public const string Archive = "Archive";
            public const string Provider = "Provider";
            public const string Save = "Save";
            public const string Compatibility = "Compatibility";
            public const string Ui = "Ui";
            public const string Mod = "Mod";
        }

        public static void Info(string category, string message)
        {
            Log.Message("[PersonalChronicle][" + category + "] " + message);
        }

        public static void Warning(string category, string message)
        {
            Log.Warning("[PersonalChronicle][" + category + "] " + message);
        }

        public static void Error(string category, string message)
        {
            Log.Error("[PersonalChronicle][" + category + "] " + message);
        }

        /// <summary>Compatibility-layer diagnostics (third-party mod integration).</summary>
        public static void Compatibility(string message)
        {
            Log.Warning("[PersonalChronicle][" + Category.Compatibility + "] " + message);
        }

        /// <summary>Save / schema migration statistics.</summary>
        public static void Save(string message)
        {
            Log.Message("[PersonalChronicle][" + Category.Save + "] " + message);
        }
    }
}
