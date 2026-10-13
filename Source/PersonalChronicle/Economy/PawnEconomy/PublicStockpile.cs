using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    // State lives on the actual vanilla zone and is saved inside that zone's XML.
    // Weak keys cannot leak settings across games or across deleted/recreated zones.
    internal sealed class PublicStockpileSettings : IExposable
    {
        private static readonly ConditionalWeakTable<Zone_Stockpile, PublicStockpileSettings> states
            = new ConditionalWeakTable<Zone_Stockpile, PublicStockpileSettings>();
        public bool Enabled;
        public long PricePerNutritionMinor;
        public PublicStockpileSettings() { }
        internal static PublicStockpileSettings Read(Zone_Stockpile zone)
            => zone != null && states.TryGetValue(zone, out var state) ? state : null;
        internal static PublicStockpileSettings Edit(Zone_Stockpile zone) => states.GetOrCreateValue(zone);
        internal static void Expose(Zone_Stockpile zone)
        {
            var state = Read(zone);
            Scribe_Deep.Look(ref state, "personalChroniclePublicStorage");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                states.Remove(zone);
                if (state != null) states.Add(zone, state);
            }
        }
        public void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", false);
            Scribe_Values.Look(ref PricePerNutritionMinor, "pricePerNutritionMinor", 0L);
        }
        internal static IEnumerable<Gizmo> Gizmos(Zone_Stockpile zone, IEnumerable<Gizmo> original)
        {
            foreach (var gizmo in original) yield return gizmo;
            var storage = GameComponent_EconomyStorage.Current;
            if (storage == null) yield break;
            yield return new Command_Toggle
            {
                defaultLabel = "PersonalChronicle.Storage.PublicZone".Translate(),
                defaultDesc = "PersonalChronicle.Storage.PublicZoneHelp".Translate(),
                icon = TexCommand.ForbidOff,
                isActive = () => Read(zone)?.Enabled == true,
                toggleAction = () => storage.SetPublicStockpile(zone, Read(zone)?.Enabled != true),
                // Each zone has its own state; a grouped toggle must not invert unlike states together.
                groupKey = zone.ID
            };
            // CHG-20261012-ECON-CONTRACTIZE（用户裁决「公有化后不需要主动设置售价，直接采用原版物品市场价格」）：
            // 「设定食品售价」入口**已移除** —— 定价由食物自身的 MarketValue 派生（见 EconomyStorage 的
            // QuotedMealPrice / LowestFoodQuote）；保留入口会变成"改了没效果的按钮"（F4 禁项）。
        }
    }

    public sealed partial class GameComponent_EconomyStorage
    {
        internal void SetPublicStockpile(Zone_Stockpile zone, bool enabled)
        {
            PublicStockpileSettings.Edit(zone).Enabled = enabled;
            if (enabled) RegisterPublicStockpile(zone);
            // Deactivation deliberately preserves rights, including stock carried outside the zone.
        }
        // CHG-20261012-ECON-CONTRACTIZE：SetPublicStockpilePrice 已删除 —— 人工设价入口整体移除
        //（定价改为食物自身 MarketValue 派生；旧字段保留仅为旧档可加载，见 PublicStockpileSettings.ExposeData）。
        internal void RegisterPublicStockpile(Zone_Stockpile zone)
        {
            if (PublicStockpileSettings.Read(zone)?.Enabled != true || zone.Map == null) return;
            foreach (var item in zone.GetSlotGroup().HeldThings.ToArray()) ReceivePublicStockpile(zone, item);
        }
        internal void ReceivePublicStockpile(Zone_Stockpile zone, Thing item)
        {
            var state = PublicStockpileSettings.Read(zone);
            if (state?.Enabled == true)
                ReceivePublicSource(zone.GetUniqueLoadID(), state.PricePerNutritionMinor, item);
        }
        internal StoredItemRight ReceivePublicSource(string source, long price, Thing item)
        {
            if (item == null || item.Destroyed || item.def?.category != ThingCategory.Item) return null;
            if (index.TryGetValue(item, out var existing))
            {
                // Paid/private stock already in the zone keeps its owner. Pending delivery keeps its quote.
                if (existing.Kind == StorageRightKind.Public && existing.PendingCommand == null)
                { existing.SourceKey = source; existing.PricePerNutritionMinor = price; }
                return existing;
            }
            if (rights.Count >= MaximumRights) return null;
            var right = new StoredItemRight { Item = item, Kind = StorageRightKind.Public,
                SourceKey = source, PricePerNutritionMinor = price };
            rights.Add(right); index[item] = right;
            return right;
        }
        internal bool CanStorePublicStockpile(Zone_Stockpile zone, Thing item)
        {
            if (PublicStockpileSettings.Read(zone)?.Enabled != true) return true;
            var right = RightFor(item);
            return right?.PendingCommand == null && right?.Kind != StorageRightKind.Private
                && (right != null || rights.Count < MaximumRights);
        }
        private static bool TryPublicSourceAt(Thing item, out string source, out long price)
        {
            source = null; price = 0;
            if (item?.Spawned != true || item.def?.category != ThingCategory.Item) return false;
            var parent = item.Position.GetSlotGroup(item.Map)?.parent;
            if (parent is Building_EconomyStorage store && !store.Private)
            { source = store.GetUniqueLoadID(); price = store.PricePerNutritionMinor; return true; }
            if (parent is Zone_Stockpile zone && PublicStockpileSettings.Read(zone)?.Enabled == true)
            { source = zone.GetUniqueLoadID(); price = PublicStockpileSettings.Read(zone).PricePerNutritionMinor; return true; }
            return false;
        }
    }
}
