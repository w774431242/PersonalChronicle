using System;

namespace PersonalChronicle.Domain.PawnEconomy.Perception
{
    internal enum StorageRightKind { Unclassified, Public, Private }
    internal static class StorageFoodRules
    {
        public static bool CanConsume(StorageRightKind kind, string owner, string eater, bool pending,
            bool economyRequested, bool transactionsAllowed, long available, long price)
        {
            if (pending || !ValidKind(kind)) return false;
            if (kind == StorageRightKind.Private) return !string.IsNullOrEmpty(owner) && owner == eater;
            if (kind == StorageRightKind.Unclassified || !economyRequested) return true;
            return transactionsAllowed && price > 0 && available >= price;
        }
        public static bool CanMerge(StorageRightKind left, string leftOwner, string leftSource,
            StorageRightKind right, string rightOwner, string rightSource, bool pending)
        {
            if (pending || !ValidKind(left) || !ValidKind(right)) return false;
            if (left == StorageRightKind.Private || right == StorageRightKind.Private)
                return left == right && !string.IsNullOrEmpty(leftOwner) && leftOwner == rightOwner;
            // Unclassified incoming stock may be donated to a public stack; its public identity survives.
            return left != StorageRightKind.Public || right != StorageRightKind.Public || leftSource == rightSource;
        }
        public static int UnitsForMeal(float wanted, float nutrition, int stack, int maxAtOnce)
        {
            if (float.IsNaN(wanted) || float.IsInfinity(wanted) || wanted < 0f || float.IsNaN(nutrition)
                || float.IsInfinity(nutrition) || nutrition <= 0f || stack <= 0) return 0;
            // Mirror vanilla's Single division before CeilToInt, including .3f / .1f == 3f.
            double units = Math.Ceiling(wanted / nutrition);
            int count = (int)Math.Min(stack, Math.Max(1d, units));
            return maxAtOnce > 0 ? Math.Min(count, maxAtOnce) : count;
        }
        private static bool ValidKind(StorageRightKind kind)
            => kind == StorageRightKind.Unclassified || kind == StorageRightKind.Public || kind == StorageRightKind.Private;
    }
}
