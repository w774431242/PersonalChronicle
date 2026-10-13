using System;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Planning;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>M7 first consumption slice: buying one food item across a real counter. Every step is
    /// evidence-based — a published price, real stock in the institution's own holder, the buyer's own
    /// reserve-backed cash, and the pawn physically at the counter. Nothing is teleported and nothing is
    /// charged without a hand-over; a failed hand-over cancels the prepared payment instead of keeping it.</summary>
    public sealed partial class GameComponent_PawnEconomy
    {
        /// <summary>Prototype counter range: the pawn must already be standing at the institution. The
        /// B4/B5 job-based counter visit replaces this distance check, it is not a substitute for it.</summary>
        private const float CounterRangeCells = 8f;

        internal bool TryPurchaseFoodAtCounter(Pawn pawn, int now, out PurchaseAttempt attempt)
        {
            attempt = default(PurchaseAttempt);
            if (pawn == null || pawn.Map == null) { attempt = new PurchaseAttempt("PawnUnknown", false, false); return false; }
            var food = pawn.needs?.food;
            if (food == null) { attempt = new PurchaseAttempt("NoFoodNeed", false, false); return false; }
            if (food.CurLevelPercentage >= PlanningPolicy.FoodNativeYield) { attempt = new PurchaseAttempt("NativeFoodFirst", false, false); return false; }
            // CHG-20261012-ECON-CONTRACTIZE · B2（用户裁决「按原版市价按件」+ 机构政策整体退役）：
            // 柜台不再由"谁发布了食品价"决定 —— 由**机构类型**直接选定（商店里 anchor 键最小者，确定性单一投影）；
            // **营业时段闸门随政策一并退役**（时段概念已无权威来源，保留即造假）。
            var anchor = LowestCounter(pawn.Map.uniqueID, InstitutionKind.Shop);
            if (anchor == null || !anchor.Spawned) { attempt = new PurchaseAttempt("CounterUnavailable", false, false); return false; }
            if (pawn.Position.DistanceTo(anchor.Position) > CounterRangeCells
                || !pawn.CanReach(anchor, PathEndMode.Touch, Danger.Deadly)) { attempt = new PurchaseAttempt("NotAtCounter", false, false); return false; }
            var stock = anchor.Stock;
            var item = FirstFood(stock, pawn, out float _);
            if (item == null) { attempt = new PurchaseAttempt("NoFoodInStock", false, false); return false; }
            // 价源 = **该件食物自身的原版市价**（与显示侧 `EconomyCommands`、储物侧 `QuotedMealPrice` 同一函数
            // `ItemValuation.UnitPriceMinor` ⇒ 显示价 == 结算价）；价值不可得（≤0）⇒ 如实拒绝，绝不发明价格（M5）。
            long amount = ItemValuation.UnitPriceMinor(item.MarketValue);
            if (amount <= 0) { attempt = new PurchaseAttempt("PriceUnavailable", false, false); return false; }
            var account = settlement.ReadAccount(pawn.GetUniqueLoadID());
            if (account == null || account.AvailableMinor < amount) { attempt = new PurchaseAttempt("InsufficientFunds", false, false); return false; }
            string command = "purchase:" + pawn.GetUniqueLoadID() + "|" + item.GetUniqueLoadID() + "|" + now.ToString();
            var prepared = settlement.PreparePurchase(SettlementIntent.ForPurchase(command, settlement.Version, command,
                pawn.GetUniqueLoadID(), amount, InstitutionFundingKey(anchor)));
            if (prepared.Code != SettlementCode.Prepared) { attempt = new PurchaseAttempt(prepared.Code.ToString(), false, false); return false; }
            if (!TryHandOver(pawn, stock, item, out Thing moved))
            {
                // 实物可能已离库但没落到买方：先按凭证追回，追不回就是不守恒，绝不取消扣款。
                if (moved != null && !ReturnToStock(pawn, stock, moved))
                {
                    Log.Warning("[PC][Economy] hand-over failed and the moved unit could not be returned; left pending reconciliation (debit not cancelled).");
                    attempt = new PurchaseAttempt("TransactionUnknown", true, false);
                    return false;
                }
                settlement.CancelPurchase(command, "ItemUnavailable");
                attempt = new PurchaseAttempt("HandOverFailed", false, false);
                return false;
            }
            var completed = settlement.CompletePurchase(command);
            if (completed.Code != SettlementCode.Finalized)
            {
                // The goods already moved: put **the actual moved unit** back, and cancel the prepared
                // debit only once the return is proven. When the goods cannot be proven back, the pair
                // goods-with-buyer / nothing-charged must NOT be turned into a no-delivery cancellation
                // (ECON-R02) - the prepared transaction is left pending reconciliation instead.
                if (!ReturnToStock(pawn, stock, moved))
                {
                    Log.Warning("[PC][Economy] purchase completion failed and the moved unit could not be returned; left pending reconciliation (debit not cancelled).");
                    attempt = new PurchaseAttempt("TransactionUnknown", true, false);
                    return false;
                }
                settlement.CancelPurchase(command, "CompletionFailed:" + completed.Code);
                attempt = new PurchaseAttempt("Settlement" + completed.Code, false, false);
                return false;
            }
            GameComponent_EconomyStorage.Current?.AssignPurchasedItem(moved, pawn.GetUniqueLoadID());
            attempt = new PurchaseAttempt(null, true, true);
            return true;
        }
        /// <summary>CHG-20261012-ECON-CONTRACTIZE · B2：政策退役后柜台改由**机构类型**直接选定 ——
        /// 指定类型里取 anchor 键最小者（ordinal；确定性单一投影，多台从不相加/取平均）。
        /// <c>null</c> = 本图没有该类型的在售机构 ⇒ 调用方如实拒绝，绝不发明一个柜台。</summary>
        private Building_EconomyInstitution LowestCounter(int mapId, InstitutionKind kind)
        {
            Building_EconomyInstitution best = null;
            string bestKey = null;
            var rows = QueryInstitutions(mapId);
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Kind != kind) continue;
                if (rows[i].Anchor?.Faction != Faction.OfPlayer) continue;
                if (best == null || StringComparer.Ordinal.Compare(rows[i].AnchorKey, bestKey) < 0)
                { best = rows[i].Anchor; bestKey = rows[i].AnchorKey; }
            }
            return best;
        }
        private static Thing FirstFood(ThingOwner stock, Pawn eater, out float nutrition)
        {
            nutrition = 0f;
            if (stock == null || stock.Count == 0) return null;
            for (int i = 0; i < stock.Count; i++)
            {
                var thing = stock[i];
                if (thing?.def?.IsNutritionGivingIngestible != true || thing.Destroyed) continue;
                var right = GameComponent_EconomyStorage.Current?.RightFor(thing);
                if (right?.Kind == PersonalChronicle.Domain.PawnEconomy.Perception.StorageRightKind.Private || right?.PendingCommand != null) continue;
                // ECON-R09：用原版食用资格（食性）筛选库存 —— 否则会把买方根本不吃的物品也算作"可买的一餐"，
                // 报价与购买都会指向一个无法改善缺口的对象（与 MealFoodSearch 同一判据源）。
                if (eater?.RaceProps == null || !eater.RaceProps.CanEverEat(thing.def)) continue;
                float value = FoodUtility.GetNutrition(eater, thing, thing.def);
                if (value <= 0f) continue;
                nutrition = value;
                return thing;
            }
            return null;
        }
        /// <summary>
        /// Real hand-over: exactly one unit leaves the institution's own holder and must land in the
        /// buyer's inventory. <paramref name="moved"/> 带出**实际被移动的那一件**（堆栈分裂/合堆后的真实身份），
        /// 调用方必须用它回滚 —— 原始堆栈引用在堆栈 &gt; 1 或背包合堆后已不再指向买方持有的物件
        /// （ECON-R02：用原引用回滚 ⇒ 找不到 ⇒ 回滚恒熔断）。
        /// <paramref name="moved"/> 非空而返回 false 表示"实物已离库但未落到买方" ⇒ 必须在取消扣款前追回。
        /// </summary>
        private static bool TryHandOver(Pawn pawn, ThingOwner stock, Thing item, out Thing moved)
        {
            moved = null;
            var inventory = pawn.inventory?.innerContainer;
            if (inventory == null) return false;
            var taken = stock.Take(item, 1);
            if (taken == null) return false;
            if (inventory.TryAdd(taken, false)) { moved = taken; return true; }
            // 落不进买方背包：先尝试放回原库存；放不回也必须把真实身份报出去，绝不遗留在无持有者状态。
            if (!stock.TryAdd(taken, true)) moved = taken;
            return false;
        }
        /// <summary>
        /// 把**实际移动的那一件**（<paramref name="moved"/> 凭证）放回机构库存。仅当"实物确已回到库存"
        /// 才返回 true —— 调用方据此决定是否允许取消扣款（ECON-R02：撤回不明时必须进待对账）。
        /// 任何失败路径都保证该件仍有明确持有者（库存或买方背包），不静默创建/销毁。
        /// </summary>
        private static bool ReturnToStock(Pawn pawn, ThingOwner stock, Thing moved)
        {
            if (stock == null || moved == null || moved.Destroyed) return false;
            var inventory = pawn?.inventory?.innerContainer;
            if (moved.holdingOwner == stock) return true;      // 已在库存（交割阶段已放回）
            if (inventory == null || moved.holdingOwner != inventory)
            {
                // 不在买方背包也不在库存 ⇒ 持有者不明，不得断言已撤回。
                Log.Warning("[PC][Economy] a moved unit is neither with the buyer nor in the stock; left for reconciliation.");
                return false;
            }
            var back = inventory.Take(moved, 1);
            if (back == null) return false;
            if (stock.TryAdd(back, true)) return true;
            if (!back.Destroyed) inventory.TryAdd(back, true); // 放不回库存 ⇒ 退回买方背包，持有者保持明确
            return false;
        }
        /// <summary>
        /// 闭环 §6 第 5–7 条：在公告售货点购买**一件公共衣物** —— 真实挂牌价、机构自持库存、买方自有现金、
        /// 人必须站在柜台，到店交割后统一结算。与食品链同构，差别只在最后一步：
        /// 结算完成后再**请求原版穿戴逻辑**；穿戴失败**不回滚交易**（§6 明令「保留已购买衣物」），
        /// 而是如实上报 <c>PendingUse</c>，供目标显示「已购，待使用」。只有真的穿上（原版状态变化）
        /// 才算缺口改善证据（§6 第 7 条）。
        /// </summary>
        internal bool TryPurchaseApparelAtCounter(Pawn pawn, int now, out PurchaseAttempt attempt)
        {
            attempt = default(PurchaseAttempt);
            if (pawn == null || pawn.Map == null) { attempt = new PurchaseAttempt("PawnUnknown", false, false); return false; }
            if (pawn.apparel == null) { attempt = new PurchaseAttempt("NoApparelTracker", false, false); return false; }
            // B2：与食品链同款 —— 柜台由机构类型选定（商店 anchor 键最小者）；营业时段闸门随政策退役。
            var anchor = LowestCounter(pawn.Map.uniqueID, InstitutionKind.Shop);
            if (anchor == null || !anchor.Spawned) { attempt = new PurchaseAttempt("CounterUnavailable", false, false); return false; }
            if (pawn.Position.DistanceTo(anchor.Position) > CounterRangeCells
                || !pawn.CanReach(anchor, PathEndMode.Touch, Danger.Deadly)) { attempt = new PurchaseAttempt("NotAtCounter", false, false); return false; }
            var stock = anchor.Stock;
            var item = FirstWearableApparel(stock, pawn);
            if (item == null) { attempt = new PurchaseAttempt("NoApparelInStock", false, false); return false; }
            // 售价 = **该件物品自身的原版市价**（政策挂牌价整体退役 ⇒ 不再有第二价源、也不再有兜底价：
            // 价值不可用就如实拒绝，显示侧 `BuildStock` 走同一函数 ⇒ 显示价 == 结算价）。
            long amount = ItemValuation.UnitPriceMinor(item.MarketValue);
            if (amount <= 0) { attempt = new PurchaseAttempt("PriceUnavailable", false, false); return false; }
            var account = settlement.ReadAccount(pawn.GetUniqueLoadID());
            if (account == null || account.AvailableMinor < amount) { attempt = new PurchaseAttempt("InsufficientFunds", false, false); return false; }
            string command = "apparel:" + pawn.GetUniqueLoadID() + "|" + item.GetUniqueLoadID() + "|" + now.ToString();
            var prepared = settlement.PreparePurchase(SettlementIntent.ForPurchase(command, settlement.Version, command,
                pawn.GetUniqueLoadID(), amount, InstitutionFundingKey(anchor)));
            if (prepared.Code != SettlementCode.Prepared) { attempt = new PurchaseAttempt(prepared.Code.ToString(), false, false); return false; }
            if (!TryHandOver(pawn, stock, item, out Thing moved))
            {
                if (moved != null && !ReturnToStock(pawn, stock, moved))
                {
                    Log.Warning("[PC][Economy] apparel hand-over failed and the moved unit could not be returned; left pending reconciliation (debit not cancelled).");
                    attempt = new PurchaseAttempt("TransactionUnknown", true, false);
                    return false;
                }
                settlement.CancelPurchase(command, "ItemUnavailable");
                attempt = new PurchaseAttempt("HandOverFailed", false, false);
                return false;
            }
            var completed = settlement.CompletePurchase(command);
            if (completed.Code != SettlementCode.Finalized)
            {
                // 与食品链同一纪律（ECON-R02）：按**实际移动凭证**追回；追不回则保留待对账，绝不取消扣款。
                if (!ReturnToStock(pawn, stock, moved))
                {
                    Log.Warning("[PC][Economy] apparel purchase completion failed and the moved unit could not be returned; left pending reconciliation (debit not cancelled).");
                    attempt = new PurchaseAttempt("TransactionUnknown", true, false);
                    return false;
                }
                settlement.CancelPurchase(command, "CompletionFailed:" + completed.Code);
                attempt = new PurchaseAttempt("Settlement" + completed.Code, false, false);
                return false;
            }
            // 交易已结算：此后穿戴成功与否都**不回滚**（§6 第 6 条）。失败仅如实标注为待使用。
            GameComponent_EconomyStorage.Current?.AssignPurchasedItem(moved, pawn.GetUniqueLoadID());
            bool wornNow = TryWearFromInventory(pawn, moved);
            attempt = wornNow ? new PurchaseAttempt(null, true, true) : new PurchaseAttempt("PendingUse", true, true);
            return true;
        }
        /// <summary>挑一件**合法、可穿戴**的库存衣物：必须是 Apparel、穿戴部位匹配、且不是尸体衣物
        /// （§6 第 4 条明令"合法、可穿戴"是缺口的前提）。</summary>
        private static Thing FirstWearableApparel(ThingOwner stock, Pawn wearer)
        {
            if (stock == null || stock.Count == 0 || wearer == null) return null;
            for (int i = 0; i < stock.Count; i++)
            {
                var thing = stock[i];
                if (thing == null || thing.Destroyed) continue;
                var right = GameComponent_EconomyStorage.Current?.RightFor(thing);
                if (right?.Kind == PersonalChronicle.Domain.PawnEconomy.Perception.StorageRightKind.Private || right?.PendingCommand != null) continue;
                var apparel = thing as Apparel;
                if (apparel == null || apparel.WornByCorpse) continue;
                if (!ApparelUtility.HasPartsToWear(wearer, apparel.def)) continue;
                if (!FillsAWearableGap(wearer, apparel)) continue;
                return thing;
            }
            return null;
        }
        /// <summary>
        /// 闭环 §6 第 4 条的**缺口判据**：该件衣物对本人是否构成缺口 —— 同类（同 def）尚未穿着才算。
        /// 判据取"原版着装实况"而非自建清单：不重复买同款、不在已有的完好衣物上再花钱，
        /// 且**不使用**任何推测性的磨损模型（无法核实的东西不猜）。
        /// </summary>
        private static bool FillsAWearableGap(Pawn wearer, Apparel candidate)
        {
            var worn = wearer?.apparel?.WornApparel;
            if (worn == null || candidate?.def == null) return false;
            for (int i = 0; i < worn.Count; i++)
            {
                var current = worn[i];
                if (current == null || current.def != candidate.def) continue;
                return false;   // 同类已在穿 ⇒ 不是缺口
            }
            return true;
        }
        /// <summary>把背包里的衣物交给**原版穿戴逻辑**（`Pawn_ApparelTracker.Wear`）。
        /// 任何失败路径都把衣物**放回背包**，绝不凭空销毁（§6：穿戴失败时保留已购买衣物）；
        /// 返回是否确实穿上了（以原版 tracker 的实况为准，不用"调用过 Wear"冒充成功）。</summary>
        private static bool TryWearFromInventory(Pawn pawn, Thing item)
        {
            var inventory = pawn?.inventory?.innerContainer;
            var apparel = item as Apparel;
            if (inventory == null || apparel == null || pawn.apparel == null || !inventory.Contains(apparel)) return false;
            var taken = inventory.Take(apparel, 1) as Apparel;
            if (taken == null) return false;
            try
            {
                pawn.apparel.Wear(taken, true);
                if (pawn.apparel.WornApparel.Contains(taken)) return true;
            }
            catch (Exception)
            {
                // 原版穿戴抛错时落回库存；下面统一放回，不吞掉状态。
            }
            if (!taken.Destroyed && !taken.Spawned) inventory.TryAdd(taken, true);
            return false;
        }
    }

    /// <summary>
    /// M9 §5（CHG-20261009-ECON-M9-S5）：一次购买尝试**实际推进到的证据面**。
    /// 阶段信号全部来自真实对象与 M11 账本（不来自 JobCondition）；也不把"付钱"当成"吃到"
    /// —— 食用是其后独立发生的原版行为，本尝试在无持久证据前恒不推断"已消费"。
    /// </summary>
    internal readonly struct PurchaseAttempt
    {
        public string ReasonCode { get; }
        /// <summary>货已实际到买方且未回收。</summary>
        public bool Delivered { get; }
        /// <summary>恒 false：本尝试不推断"已吃"（§3："付钱等于吃饭"不可推断）。</summary>
        public bool Consumed { get; }
        /// <summary>M11 结算行已 Finalized。</summary>
        public bool Settled { get; }
        public PurchaseAttempt(string reasonCode, bool delivered, bool settled)
        {
            ReasonCode = reasonCode; Delivered = delivered; Settled = settled; Consumed = false;
        }
    }
}
