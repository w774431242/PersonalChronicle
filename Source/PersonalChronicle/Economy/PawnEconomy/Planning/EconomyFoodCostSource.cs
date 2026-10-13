using System;
using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy.Perception;
using PersonalChronicle.Application.PawnEconomy.Planning;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Planning;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy.Planning
{
    /// <summary>M7/M5 priced food for one pawn and window. Nutrition need is the pawn's own remaining
    /// nutrition plus an explicitly assumed consumption over the window; carried food is deducted as
    /// owned coverage at the same unit price, never as cash.
    ///
    /// <para><b>CHG-20261012-ECON-CONTRACTIZE（用户裁决「公有化后不需要主动设置售价，直接采用原版物品市场
    /// 价格」）</b>：价源由"机构政策报价 / 人工设定的每营养单价"改为**公有储物里食物的市价派生**
    /// （<c>最低(单件市价 ÷ 该件营养)</c>）。旧注释"never a MarketValue substitute"所代表的旧裁决
    /// 已被本批用户裁决**显式取代**；"未报价"概念随之消失 —— 储物里没有可食物才是真的没价（如实）。</para></summary>
    internal sealed class EconomyFoodCostSource : IRequirementCostSource
    {
        /// <summary>Prototype bound for the coverage read; a carried-food total above this is clipped and
        /// the slice still stays bounded (documented approximation, not a measured inventory value).</summary>
        private const float CarriedNutritionCeiling = 100f;
        private readonly GameComponent_PawnEconomy owner;
        public EconomyFoodCostSource(GameComponent_PawnEconomy owner) { this.owner = owner ?? throw new ArgumentNullException(nameof(owner)); }

        public IReadOnlyList<RequirementCostSlice> ReadCosts(PerceptionSubject subject, int windowStart, int windowEnd, out bool complete, out string incompleteReason)
        {
            complete = false;
            incompleteReason = "CostEstimateUnavailable";
            var rows = new List<RequirementCostSlice>();
            if (windowEnd <= windowStart) return rows;
            if (!owner.TryGetPlanningPawn(subject.PawnId, subject.MapId, out var pawn) || pawn == null || pawn.Dead || !pawn.Spawned) return rows;
            var food = pawn.needs?.food;
            if (food == null) return rows;
            // CHG-20261012：价源 = **公有储物里食物的市价派生每营养价**（详见类注释）。
            // 政策不再参与定价（政策整页删除）；没有可食物 ⇒ 报价未知，不是生活成本为零。
            // LR-04（经理复核 §10.2）：无价**不是单一状态** —— 把扫描期的拒绝分支细分出来（只读计数，
            // 不新增遍历），否则玩家只看到「等待报价」，会被误导去找并不存在的人工定价入口。
            // 账本组件缺失时 `scan` 保持 default（Refusal=None ⇒ 兜底码），不得因空条件而漏赋值。
            var storage = GameComponent_EconomyStorage.Current;
            FoodQuoteScan scan = default;
            long price = storage == null ? 0L : storage.LowestFoodQuote(pawn, out scan);
            // Missing supply/quote is not evidence that two days of food cost zero.
            if (price <= 0) { incompleteReason = RefusalReason(scan); return rows; }
            double consumed = (double)food.FoodFallPerTickAssumingCategory(HungerCategory.Fed) * (windowEnd - windowStart);
            double needed = Math.Max(0d, (double)food.MaxLevel - food.CurLevel + consumed);
            if (needed <= 0d) { complete = true; return rows; }
            // ECON-R09 裁定（2026-10-10，已批准）：价格按**精确营养**计价、总额四舍五入到最小货币单位、下限 1。
            // 此前按 ceil(needed) 的整单位计价 ⇒ 营养 0.05 与 0.9 的缺口同价（预算与实际扣款虽"一致"却整体偏离真实价值）。
            // 三处（预算 / 报价 / 实际扣款）必须走同一个计价式，不得各自取整。
            long amount = NutritionPrice(needed, price);
            MinorCurrency.Validate(amount);
            var coverage = new List<CostCoverage>();
            double carriedNutrition = Math.Min(needed, CarriedNutrition(pawn));
            long covered = carriedNutrition > 0d ? NutritionPrice(carriedNutrition, price) : 0L;
            MinorCurrency.Validate(covered);
            if (covered > 0) coverage.Add(new CostCoverage("carriedFood:" + subject.PawnId, CoverageKind.OwnedOrPaid, covered));
            bool immediate = food.CurLevelPercentage < PlanningPolicy.FoodNativeYield;
            rows.Add(new RequirementCostSlice("Food", windowStart, windowEnd, true, immediate, Fact<long>.Known(amount), coverage));
            complete = true;
            return rows;
        }

        /// <summary>
        /// LR-04：扫描拒绝分支 → **稳定原因码**（只做映射，不做判定；未知分支回落兜底码）。
        /// 码只用于呈现（`Reason.*` 文案族），**不得**作为配色以外的行为判据（GOV-009 同源纪律）。
        /// </summary>
        internal static string RefusalReason(FoodQuoteScan scan) => scan.Refusal switch
        {
            FoodQuoteRefusal.Unpriced => "FoodQuoteUnpriced",
            FoodQuoteRefusal.FoodUnreachable => "FoodQuoteFoodUnreachable",
            FoodQuoteRefusal.PendingTrade => "FoodQuoteFoodPendingTrade",
            FoodQuoteRefusal.OwnershipNotPublic => "FoodQuoteOwnershipNotPublic",
            FoodQuoteRefusal.NotEdible => "FoodQuoteNotEdible",
            FoodQuoteRefusal.NoFood => "FoodQuoteNoFood",
            FoodQuoteRefusal.SourceUnreachable => "FoodQuoteSourceUnreachable",
            FoodQuoteRefusal.ZoneNotEnabled => "FoodQuoteZoneNotEnabled",
            FoodQuoteRefusal.NoPublicStorage => "FoodQuoteNoPublicStorage",
            _ => "CostEstimateUnavailable"
        };

        /// <summary>
        /// ECON-R09 裁定的**唯一计价式**：精确营养 × 每营养价，四舍五入到最小货币单位，下限 1 单位。
        /// 下限保证小额缺口仍可交易（否则营养 0.05 的交易会被取整成 0 而永远不可买）；
        /// 近 <c>long</c> 上限返回上限，由调用方按"报价溢出"拒绝。
        /// **预算、报价展示与实际扣款三处必须调用本式** —— 各自取整会让"看到的价"与"扣的钱"分叉。
        /// </summary>
        internal static long NutritionPrice(double nutrition, long pricePerNutritionMinor)
        {
            if (nutrition <= 0d || pricePerNutritionMinor <= 0) return 0L;
            double raw = nutrition * pricePerNutritionMinor;
            if (raw >= long.MaxValue) return long.MaxValue;
            return Math.Max(1L, (long)Math.Round(raw, MidpointRounding.AwayFromZero));
        }

        private static float CarriedNutrition(Pawn pawn)
        {
            float total = 0f;
            var inventory = pawn.inventory?.innerContainer;
            if (inventory != null)
                for (int i = 0; i < inventory.Count; i++)
                {
                    var thing = inventory[i];
                    if (thing?.def?.IsNutritionGivingIngestible != true) continue;
                    if (GameComponent_EconomyStorage.Current?.HasOwnedFood(thing, pawn) == false) continue;
                    // ECON-R09：随身的"可覆盖营养"同样只算**买方真能吃**的食物（与购买/结算同一判据源）——
                    // 否则预算会高估覆盖，把不可食的库存当成"已经有了"。
                    if (pawn.RaceProps == null || !pawn.RaceProps.CanEverEat(thing.def)) continue;
                    total += FoodUtility.GetNutrition(pawn, thing, thing.def);
                    if (total >= CarriedNutritionCeiling) return CarriedNutritionCeiling;
                }
            var carried = pawn.carryTracker?.CarriedThing;
            if (carried?.def?.IsNutritionGivingIngestible == true && pawn.RaceProps != null && pawn.RaceProps.CanEverEat(carried.def)
                && GameComponent_EconomyStorage.Current?.HasOwnedFood(carried, pawn) != false)
                total += FoodUtility.GetNutrition(pawn, carried, carried.def);
            return Math.Min(total, CarriedNutritionCeiling);
        }
    }
}
