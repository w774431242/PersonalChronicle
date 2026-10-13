using System.Collections.Generic;
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
            /// <summary>自动化车间 / 农业设备的运行诊断。</summary>
            public const string Automation = "Automation";
        }

        /// <summary>节流器：key → 上次放行的 tick。</summary>
        private static readonly Dictionary<string, int> throttleTicks = new Dictionary<string, int>();

        /// <summary>
        /// 高频日志节流（性能体检 P0-3）：同一 key 在 <paramref name="intervalTicks"/> 内只放行一次。
        /// 调用方务必写成 <c>if (ChronicleLog.Throttled(...)) ChronicleLog.Info(...)</c>——
        /// 先判断再拼接，否则字符串照拼，省不掉成本。
        /// ⚠️ key 必须是编译期常量字面量，不要拼字符串（那就白节流了）。
        /// </summary>
        public static bool Throttled(string key, int intervalTicks)
        {
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (throttleTicks.TryGetValue(key, out int last) && now - last < intervalTicks)
            {
                return false;
            }
            throttleTicks[key] = now;
            return true;
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
