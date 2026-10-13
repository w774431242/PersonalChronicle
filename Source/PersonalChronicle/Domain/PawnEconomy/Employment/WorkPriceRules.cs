using System;

namespace PersonalChronicle.Domain.PawnEconomy.Employment
{
    /// <summary>Two-level quotes for NEW contracts only. Zero means inherit, never a zero wage.</summary>
    internal static class WorkPriceRules
    {
        public const long MaximumMinor = 100000L; // 1000 silver, same ceiling as the contract editor
        public static bool Valid(long minor) => minor >= 0 && minor <= MaximumMinor;
        public static long Resolve(long workDefault, long orderOverride, long recommendation)
        {
            if (Valid(orderOverride) && orderOverride > 0) return orderOverride;
            if (Valid(workDefault) && workDefault > 0) return workDefault;
            return recommendation > 0 ? recommendation : WorkContractRules.RecommendedBaseMinor;
        }
        public static string Position(string work, string anchor, string order = null)
            => "vanilla:" + work + ":" + anchor + (string.IsNullOrEmpty(order) ? "" : ":" + order);
        public static bool Parse(string position, out string work, out string anchor, out string order)
        {
            work = anchor = order = null;
            if (position == null || !position.StartsWith("vanilla:", StringComparison.Ordinal)) return false;
            var parts = position.Split(new[] { ':' });
            if (parts.Length < 3 || parts.Length > 4 || parts[1].Length == 0 || parts[2].Length == 0) return false;
            if (parts.Length == 4 && parts[3].Length == 0) return false;
            work = parts[1]; anchor = parts[2]; order = parts.Length == 4 ? parts[3] : null;
            return true;
        }
        // Old three-part contracts cover the whole work type. Count these against EVERY order's capacity.
        public static bool Reserves(string position, string work, string anchor, string order)
            => Parse(position, out string pw, out string pa, out string po)
                && pw == work && pa == anchor && (string.IsNullOrEmpty(order) || po == null || po == order);
        public static string OrderForJob(string job, bool tree)
        {
            switch (job)
            {
                case "Mine": return "Mine";
                case "HarvestDesignated": return tree ? "ChopWood" : "Harvest";
                case "CutPlantDesignated": return "CutPlants";
                case "Hunt": return "Hunt";
                case "Slaughter": return "Slaughter";
                case "Tame": return "Tame";
                case "HaulToCell":
                case "HaulToContainer": return "Haul";
                default: return null;
            }
        }
    }
}
