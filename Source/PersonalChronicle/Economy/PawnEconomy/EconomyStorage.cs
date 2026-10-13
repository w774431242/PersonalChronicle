using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Economy.PawnEconomy
{
    internal sealed class StoredItemRight : IExposable
    {
        public StoredItemRight() { }
        public Thing Item;
        public StorageRightKind Kind;
        public string OwnerKey, SourceKey, PendingCommand, PendingBuyer;
        public long PricePerNutritionMinor;
        public int PendingUnits;
        public StoredItemRight Copy(Thing item) => new StoredItemRight { Item = item, Kind = Kind, OwnerKey = OwnerKey,
            SourceKey = SourceKey, PricePerNutritionMinor = PricePerNutritionMinor,
            PendingCommand = PendingCommand, PendingBuyer = PendingBuyer, PendingUnits = PendingUnits };
        public void ExposeData()
        {
            Scribe_References.Look(ref Item, "item");
            Scribe_Values.Look(ref Kind, "kind"); Scribe_Values.Look(ref OwnerKey, "owner");
            Scribe_Values.Look(ref SourceKey, "source"); Scribe_Values.Look(ref PricePerNutritionMinor, "price", 0L);
            Scribe_Values.Look(ref PendingCommand, "pendingPurchase"); Scribe_Values.Look(ref PendingBuyer, "pendingBuyer");
            Scribe_Values.Look(ref PendingUnits, "pendingUnits");
        }
    }
    public sealed partial class GameComponent_EconomyStorage : GameComponent
    {
        private sealed class ItemReferenceComparer : IEqualityComparer<Thing>
        {
            public bool Equals(Thing x, Thing y) => ReferenceEquals(x, y);
            public int GetHashCode(Thing item) => RuntimeHelpers.GetHashCode(item);
        }
        private const int MaximumRights = 16384;
        private List<StoredItemRight> rights = new List<StoredItemRight>();
        private readonly Dictionary<Thing, StoredItemRight> index = new Dictionary<Thing, StoredItemRight>(new ItemReferenceComparer());
        private readonly List<Building_EconomyStorage> stores = new List<Building_EconomyStorage>();
        public static GameComponent_EconomyStorage Current => Verse.Current.Game?.GetComponent<GameComponent_EconomyStorage>();
        public GameComponent_EconomyStorage(Game game) { }
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref rights, "storedItemRights", LookMode.Deep);
            if (rights == null) rights = new List<StoredItemRight>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit) RebuildIndex();
        }
        private void RebuildIndex()
        {
            index.Clear();
            rights.RemoveAll(r => r?.Item == null || r.Item.Destroyed);
            foreach (var right in rights) index[right.Item] = right;
        }
        public override void FinalizeInit()
        {
            RebuildIndex(); stores.Clear();
            foreach (var map in Find.Maps)
            {
                foreach (var store in map.listerBuildings.allBuildingsColonist.OfType<Building_EconomyStorage>()) Register(store);
                foreach (var zone in map.zoneManager.AllZones.OfType<Zone_Stockpile>()) RegisterPublicStockpile(zone);
            }
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (ReferenceEquals(Current, this))
                    foreach (var right in rights.ToArray())
                        if (right.PendingCommand != null) GameComponent_PawnEconomy.Current?.RecoverStoragePurchase(right);
            });
        }
        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 600 != 0) return;
            RebuildIndex(); stores.RemoveAll(s => s == null || !s.Spawned);
        }
        public void Register(Building_EconomyStorage store)
        {
            if (!stores.Contains(store)) stores.Add(store);
            foreach (var item in store.GetSlotGroup().HeldThings.ToArray()) Receive(store, item);
        }
        internal StoredItemRight RightFor(Thing item)
        {
            if (item == null) return null;
            if (index.TryGetValue(item, out var right)) return right;
            if (item.Spawned && item.Position.GetSlotGroup(item.Map)?.parent is Building_EconomyStorage store)
                return new StoredItemRight { Item = item, Kind = store.Private ? StorageRightKind.Private : StorageRightKind.Public,
                    OwnerKey = store.Private ? store.Owner?.GetUniqueLoadID() : null, SourceKey = store.GetUniqueLoadID(),
                    PricePerNutritionMinor = store.PricePerNutritionMinor };
            if (TryPublicSourceAt(item, out string source, out long price))
                return new StoredItemRight { Item = item, Kind = StorageRightKind.Public, SourceKey = source, PricePerNutritionMinor = price };
            return null;
        }
        internal bool CanStore(Building_EconomyStorage store, Thing item)
        {
            if (store.Private && (store.Owner == null || store.Owner.Dead)) return false;
            var right = RightFor(item);
            if (right?.PendingCommand != null) return false;
            if (right?.Kind == StorageRightKind.Private)
                return store.Private && right.OwnerKey == store.Owner.GetUniqueLoadID();
            if (right?.Kind == StorageRightKind.Public && store.Private) return false;
            return right != null || rights.Count < MaximumRights;
        }
        internal StoredItemRight Receive(Building_EconomyStorage store, Thing item)
        {
            if (item == null || item.Destroyed) return null;
            // Existing ownership survives hauling and relocation. Placing unpaid public stock in a
            // private store must never turn it into a free private asset.
            if (index.TryGetValue(item, out var existing))
            {
                if (!store.Private && existing.Kind == StorageRightKind.Public && existing.PendingCommand == null)
                { existing.SourceKey = store.GetUniqueLoadID(); existing.PricePerNutritionMinor = store.PricePerNutritionMinor; }
                return existing;
            }
            if (rights.Count >= MaximumRights) return null;
            var right = new StoredItemRight { Item = item, Kind = store.Private ? StorageRightKind.Private : StorageRightKind.Public,
                OwnerKey = store.Private ? store.Owner?.GetUniqueLoadID() : null, SourceKey = store.GetUniqueLoadID(),
                PricePerNutritionMinor = store.PricePerNutritionMinor };
            rights.Add(right); index[item] = right; return right;
        }
        internal void RefreshQuote(Building_EconomyStorage store)
        {
            foreach (var right in rights)
                if (right.Kind == StorageRightKind.Public && right.SourceKey == store.GetUniqueLoadID() && right.PendingCommand == null)
                    right.PricePerNutritionMinor = store.PricePerNutritionMinor;
        }
        internal void CopyRights(StoredItemRight original, Thing source, Thing split)
        {
            if (split == null || original == null || index.ContainsKey(split)) return;
            var copy = original.Copy(split);
            rights.Add(copy); index[split] = copy;
        }
        internal bool CanMerge(Thing left, Thing right)
        {
            var a = RightFor(left); var b = RightFor(right);
            if (a?.Kind == StorageRightKind.Public && b?.Kind == StorageRightKind.Public && a.SourceKey != b.SourceKey
                && a.PendingCommand == null && b.PendingCommand == null && TryPublicSourceAt(left, out _, out _))
                return true; // Public stock adopts the destination public storage quote.
            if (a?.Kind == StorageRightKind.Public && b == null || b?.Kind == StorageRightKind.Public && a == null)
            {
                // Donation is allowed only at a public shelf. Carried unpaid food cannot absorb
                // already bought or ordinary inventory food and make it chargeable a second time.
                Thing publicItem = a?.Kind == StorageRightKind.Public ? left : right;
                string source = a?.Kind == StorageRightKind.Public ? a.SourceKey : b.SourceKey;
                if (!TryPublicSourceAt(publicItem, out string destinationSource, out _) || destinationSource != source) return false;
            }
            return StorageFoodRules.CanMerge(a?.Kind ?? StorageRightKind.Unclassified, a?.OwnerKey, a?.SourceKey,
                b?.Kind ?? StorageRightKind.Unclassified, b?.OwnerKey, b?.SourceKey, a?.PendingCommand != null || b?.PendingCommand != null);
        }
        internal void AfterMerge(Thing destination, StoredItemRight sourceRight, int previousCount)
        {
            if (sourceRight == null || destination.stackCount <= previousCount || index.ContainsKey(destination)) return;
            var copy = sourceRight.Copy(destination); rights.Add(copy); index[destination] = copy;
        }
        internal bool HasOwnedFood(Thing item, Pawn pawn)
        {
            var right = RightFor(item);
            return right == null || right.PendingCommand == null && right.Kind == StorageRightKind.Private && right.OwnerKey == pawn.GetUniqueLoadID();
        }
        internal bool CanSelectFood(Thing item, Pawn eater)
        {
            var right = RightFor(item);
            if (right == null) return true;
            string eaterKey = eater?.GetUniqueLoadID();
            long price = 0, available = 0;
            var economy = GameComponent_PawnEconomy.Current;
            bool requested = PersonalChronicleMod.Settings?.EnablePawnEconomicInstitution == true;
            bool allowed = false;
            if (right.Kind == StorageRightKind.Public && requested && eater?.IsFreeNonSlaveColonist == true && eater.RaceProps.Humanlike)
            {
                allowed = economy?.StoragePurchasesAllowed == true;
                available = economy?.ReadStorageFoodAccount(eaterKey)?.AvailableMinor ?? 0;
                price = QuotedMealPrice(item, eater, right, out _);
            }
            return StorageFoodRules.CanConsume(right.Kind, right.OwnerKey, eaterKey, right.PendingCommand != null,
                requested, allowed, available, price);
        }
        /// <summary>CHG-20261012（用户裁决「公有化后不需要主动设置售价，直接采用原版物品市场价格」）：
        /// 一份餐的**报价**按**该件自身市价**算 —— 每营养价 = 单件市价 ÷ 该件营养，
        /// 与 <c>NutritionPrice</c> 组合后结算额恒等于"拆分件数 × 单件市价"（按件单价，用户口径）。
        /// 不再读任何人工报价字段（"设定食品售价"入口已移除）；件价不可用 ⇒ 0（不可计价，**绝不白送**）。</summary>
        internal static long QuotedMealPrice(Thing item, Pawn eater, StoredItemRight right, out int units)
        {
            units = 0;
            if (item == null || eater?.needs?.food == null || item.def.IsNutritionGivingIngestible != true
                || item.GetType() != typeof(Thing) && item.GetType() != typeof(ThingWithComps)) return 0;
            float nutrition = FoodUtility.NutritionForEater(eater, item);
            long perNutritionMinor = MarketPerNutritionMinor(item, nutrition);
            if (perNutritionMinor <= 0) return 0;
            units = StorageFoodRules.UnitsForMeal(Math.Max(0f, eater.needs.food.MaxLevel - eater.needs.food.CurLevel),
                nutrition, item.stackCount, item.def.ingestible.maxNumToIngestAtOnce);
            long amount = Planning.EconomyFoodCostSource.NutritionPrice((double)nutrition * units, perNutritionMinor);
            return amount <= MinorCurrency.Maximum ? amount : 0;
        }
        /// <summary>单件市价 ÷ 该件营养（minor）；市价或营养不可用 ⇒ 0（调用方按"不可计价"处理）。</summary>
        private static long MarketPerNutritionMinor(Thing item, float nutritionPerUnit)
        {
            if (item == null || nutritionPerUnit <= 0f) return 0L;
            long unitMinor = PersonalChronicle.Domain.PawnEconomy.Finance.ItemValuation.UnitPriceMinor(item.MarketValue);
            if (unitMinor <= 0L) return 0L;
            double perNutrition = unitMinor / (double)nutritionPerUnit;
            if (perNutrition >= long.MaxValue) return long.MaxValue;
            return Math.Max(1L, (long)Math.Round(perNutrition, MidpointRounding.AwayFromZero));
        }
        /// <summary>公有储物里**最便宜的食物的每营养市价** = min over 可食物堆(单件市价 ÷ 每件营养)。
        /// 与 <see cref="QuotedMealPrice"/>（该件自身价）同源：预算是"最低可及价"，结算是"这一件的价"。
        /// **零人工报价**；没有可食物 ⇒ 0。
        /// <para>LR-04 修正旧注释：无价**不是**"complete 且无切片" —— 调用方必须保持 `complete=false`
        /// （改成 true 会让预算走"目标 0"路径，等于替玩家认定"两天食物不要钱"）。</para>
        /// <param name="scan">只记录**拒绝分支计数**（LR-04），用于把"等待报价"细分成可排查的原因；
        /// 计数一律在**既有循环内**进行，不新增任何遍历、不新增缓存、不落盘。</param></summary>
        internal long LowestFoodQuote(Pawn eater, out FoodQuoteScan scan)
        {
            scan = default;
            if (eater?.Map == null) return 0;
            long best = 0;
            foreach (var store in stores)
            {
                if (store == null || !store.Spawned || store.Map != eater.Map || store.Private) continue;
                scan.StoresSeen++;
                if (!eater.CanReach(store, PathEndMode.Touch, Danger.Deadly)) { scan.StoresUnreachable++; continue; }
                scan.SourcesConsidered++;
                ConsiderHeldFood(store.GetSlotGroup().HeldThings, eater, ref best, ref scan);
            }
            foreach (var zone in eater.Map.zoneManager.AllZones.OfType<Zone_Stockpile>())
            {
                scan.ZonesSeen++;
                var settings = PublicStockpileSettings.Read(zone);
                if (settings?.Enabled != true) { scan.ZonesNotEnabled++; continue; }
                scan.SourcesConsidered++;
                ConsiderHeldFood(zone.GetSlotGroup().HeldThings, eater, ref best, ref scan);
            }
            scan.Refusal = best > 0 ? FoodQuoteRefusal.None : DecideFoodQuoteRefusal(scan);
            return best;
        }
        /// <summary>LR-04：按"最接近可用 → 兜底"的**固定顺序**给出单一主因（确定性；只用于呈现，
        /// 不作行为判据）。每堆在 <see cref="ConsiderHeldFood"/> 中按判定顺序只计一个失败类。</summary>
        internal static FoodQuoteRefusal DecideFoodQuoteRefusal(FoodQuoteScan scan)
            => scan.StacksUnpriced > 0 ? FoodQuoteRefusal.Unpriced
            : scan.StacksUnreachable > 0 ? FoodQuoteRefusal.FoodUnreachable
            : scan.StacksPending > 0 ? FoodQuoteRefusal.PendingTrade
            : scan.StacksOwnershipNotPublic > 0 ? FoodQuoteRefusal.OwnershipNotPublic
            : scan.StacksNotEdible > 0 ? FoodQuoteRefusal.NotEdible
            : scan.ItemsSeen > 0 ? FoodQuoteRefusal.NoFood
            : scan.StoresUnreachable > 0 ? FoodQuoteRefusal.SourceUnreachable
            : scan.SourcesConsidered > 0 ? FoodQuoteRefusal.NoFood
            : scan.ZonesNotEnabled > 0 ? FoodQuoteRefusal.ZoneNotEnabled
            : FoodQuoteRefusal.NoPublicStorage;
        /// <summary>把一堆里**公有可及食物**的每营养市价并入 <paramref name="best"/>（0 = 尚未定价）。
        /// 有界：只读该格已有的东西（与旧实现同源遍历，不做全图扫描）。LR-04：同时累加拒绝分支计数。</summary>
        private void ConsiderHeldFood(IEnumerable<Thing> held, Pawn eater, ref long best, ref FoodQuoteScan scan)
        {
            foreach (var thing in held)
            {
                if (thing?.def?.IsNutritionGivingIngestible != true) continue;
                scan.ItemsSeen++;
                if (eater.RaceProps == null || !eater.RaceProps.CanEverEat(thing.def)) { scan.StacksNotEdible++; continue; }
                var right = RightFor(thing);
                if (right?.Kind != StorageRightKind.Public) { scan.StacksOwnershipNotPublic++; continue; }
                if (right.PendingCommand != null) { scan.StacksPending++; continue; }
                if (!eater.CanReach(thing, PathEndMode.Touch, Danger.Deadly)) { scan.StacksUnreachable++; continue; }
                long perNutrition = MarketPerNutritionMinor(thing, FoodUtility.NutritionForEater(eater, thing));
                if (perNutrition <= 0) { scan.StacksUnpriced++; continue; }
                best = best == 0 ? perNutrition : Math.Min(best, perNutrition);
            }
        }
    }

    /// <summary>LR-04：一次公有食物报价扫描的**拒绝分支计数**（只读诊断容器，不落盘、不进 Save）。
    /// 计数只在既有扫描循环内累加，**不新增任何遍历**。</summary>
    internal struct FoodQuoteScan
    {
        public FoodQuoteRefusal Refusal;
        public int ItemsSeen, StoresSeen, StoresUnreachable, ZonesSeen, ZonesNotEnabled, SourcesConsidered;
        public int StacksNotEdible, StacksOwnershipNotPublic, StacksPending, StacksUnreachable, StacksUnpriced;
    }

    /// <summary>LR-04：公有食物报价被拒的**单一主因**（确定性裁决；只用于呈现，不作行为判据）。</summary>
    internal enum FoodQuoteRefusal
    {
        None = 0, Unpriced, FoodUnreachable, PendingTrade, OwnershipNotPublic, NotEdible, NoFood,
        SourceUnreachable, ZoneNotEnabled, NoPublicStorage
    }
}
