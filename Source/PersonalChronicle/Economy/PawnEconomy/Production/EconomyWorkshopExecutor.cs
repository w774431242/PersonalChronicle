using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Application.PawnEconomy.Production;
using PersonalChronicle.Automation;
using PersonalChronicle.Domain.PawnEconomy.Production;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy.Production
{
    /// <summary>M10 executor contract against the existing workshop runtime. Capacity is read from the
    /// live workshop manager and vanilla bill stacks; proposals are accepted into a bounded in-memory
    /// receipt ledger only. The class has no write path into bills or schedules: the existing workshop
    /// executor remains the single executor (no second runner).</summary>
    internal sealed class EconomyWorkshopExecutor : IWorkshopExecutorContract
    {
        private readonly RestockProposalLedger ledger = new RestockProposalLedger();
        public int ReceiptCount => ledger.Count;
        public int ReplayCount => ledger.Replays;
        public IReadOnlyList<string> Receipts => ledger.Accepted;
        /// <summary>The live authorisation ledger: the persistence bridge exports and restores exactly
        /// this instance, so there is never a second truth beside the save data.</summary>
        internal RestockProposalLedger Ledger => ledger;

        /// <summary>Authorised, not yet started quantity for one map|category (ledger sum; the observed
        /// work in progress is converged by the caller so nothing is subtracted twice).</summary>
        internal long AuthorisedAmount(int mapId, string categoryKey)
            => ledger.AuthorisedAmount(mapId.ToString(CultureInfo.InvariantCulture), categoryKey);

        /// <summary>Deterministic age trim of the persisted ledger (see rules for the prototype bound).</summary>
        internal int TrimExpired(int now) => ledger.TrimExpired(now, ProductionAuthorisationRules.MaximumAuthorisationAgeTicks);

        internal bool TryRelease(string proposalId) => ledger.TryRelease(proposalId);

        private static int CurrentTick => Verse.Find.TickManager?.TicksGame ?? 0;

        public WorkshopCapacityReading ReadCapacity(int mapId)
        {
            var map = ProductionMapLookup.Find(mapId);
            if (map == null) return WorkshopCapacityReading.Unreadable("MapUnavailable");
            var manager = MapComponent_WorkshopManager.Get(map);
            if (manager == null) return WorkshopCapacityReading.Unreadable("WorkshopManagerUnavailable");
            int shifts = 0, gap = 0;
            var list = manager.Shifts;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var shift = list[i];
                    if (shift == null) continue;
                    shifts++;
                    int staffing = manager.ShiftStaffingOf(shift), filled = manager.FilledCountOf(shift);
                    if (staffing > filled) gap += staffing - filled;
                }
            }
            return WorkshopCapacityReading.Of(shifts, gap, CountOpenBills(map), manager.ActiveWorkerCount);
        }

        /// <summary>
        /// M10 完整闭环（2026-10-10）：受理提案时**先真实建单、再记账** —— 授权必须绑定既有合法工单载体
        /// （审计原文："授权必须绑定既有合法 bill/工坊执行"）。建单失败即拒绝受理，绝不记下没有载体的账。
        /// 幂等重放不重复建单（先查账）。写单只走原版 <c>BillStack.AddBill</c>，仍然没有第二个 runner。
        /// </summary>
        public bool TrySubmitRestockProposal(ProductionProposal proposal, out string receipt, out bool replay, out string reason)
        {
            receipt = null; replay = false; reason = null;
            if (proposal == null) { reason = ProductionProposalReasons.Invalid; return false; }
            // 账内已有 ⇒ 交回既有路径回原回执（TryAccept 自会置 replay 并计数），**绝不重复建单**。
            if (ledger.Has(proposal.ProposalId))
                return ledger.TryAccept(proposal, CurrentTick, out receipt, out replay, out reason);
            string billKey = TryCreateBill(proposal, out string failure);
            if (billKey == null)
            { reason = string.IsNullOrEmpty(failure) ? ProductionAuthorisationReasons.ExecutorUnavailable : failure; return false; }
            return ledger.TryAccept(proposal, CurrentTick, billKey, out receipt, out replay, out reason);
        }

        /// <summary>M10 完整闭环：真实完成事件 → 授权账核销（返回被更新的行数；无匹配为 0，属绝大多数事件）。</summary>
        internal int ReportDelivery(string billKey, string categoryKey, long produced)
            => ledger.RecordDelivery(billKey, categoryKey, produced);

        /// <summary>M10 完整闭环：未交付的授权净额（授权量 − 已交付量），读数收敛用。</summary>
        internal long NetOutstandingAmount(int mapId, string categoryKey)
            => ledger.NetOutstandingAmount(mapId.ToString(CultureInfo.InvariantCulture), categoryKey);

        /// <summary>M10 完整闭环：某品类已按真实完成事件核销的累计交付量。</summary>
        internal long DeliveredAmount(int mapId, string categoryKey)
            => ledger.DeliveredAmount(mapId.ToString(CultureInfo.InvariantCulture), categoryKey);

        /// <summary>
        /// 为该品类找**合法执行载体**并建一条带数量的真实工单：存活工坊 → 其设备 → 产出该品类的配方。
        /// 返回新工单的稳定键；失败返回 null 并给出原因码（调用方据此拒绝受理）。
        /// </summary>
        private string TryCreateBill(ProductionProposal proposal, out string failure)
        {
            failure = null;
            if (!int.TryParse(proposal.MapKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mapId) || mapId < 0)
            { failure = ProductionAuthorisationReasons.ExecutorUnavailable; return null; }
            var map = ProductionMapLookup.Find(mapId);
            if (map == null) { failure = ProductionAuthorisationReasons.ExecutorUnavailable; return null; }
            var bench = FindBenchFor(map, proposal.CategoryKey, out var recipe);
            if (bench == null || recipe == null) { failure = "ProductionNoSuitableBench"; return null; }
            int count = proposal.ShortfallMinor > int.MaxValue ? int.MaxValue : (int)proposal.ShortfallMinor;
            var bill = WorkshopBillActions.TryAddBillOn(bench, recipe, count, out string message);
            if (bill == null)
            { failure = string.IsNullOrEmpty(message) ? ProductionAuthorisationReasons.ExecutorUnavailable : message; return null; }
            return BillKeyOf(bench, bill);
        }

        /// <summary>
        /// 找一台**属于存活工坊**且能产出目标品类的工作台（有界扫描，复用读数的设备上限）。
        /// 只认工坊内的设备 —— 授权载体必须是既有合法工坊执行位（报告口径），空地工作台不算。
        /// </summary>
        private static Building_WorkTable FindBenchFor(Map map, string categoryKey, out RecipeDef recipe)
        {
            recipe = null;
            if (map?.listerBuildings?.allBuildingsColonist == null || string.IsNullOrWhiteSpace(categoryKey)) return null;
            var buildings = map.listerBuildings.allBuildingsColonist;
            int scanned = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (++scanned > EconomyProductionReadoutSource.MaximumDeviceScan) break;
                if (!(buildings[i] is Building_WorkTable table) || table.def == null) continue;
                if (WorkshopZoneUtility.WorkshopAt(table.Position, map) == null) continue;
                var all = table.def.AllRecipes;
                if (all == null) continue;
                for (int r = 0; r < all.Count; r++)
                {
                    var candidate = all[r];
                    if (candidate?.products == null) continue;
                    for (int p = 0; p < candidate.products.Count; p++)
                    {
                        var product = candidate.products[p];
                        if (product?.thingDef?.defName == categoryKey && product.count > 0)
                        { recipe = candidate; return table; }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 工单稳定键：<c>&lt;建筑 defName&gt;:&lt;thingIDNumber&gt;#&lt;bill 的持久标识&gt;</c>。
        /// 不持对象引用（跨读档安全），与 GOV-009 一致 —— 完成事件侧用同一式重算即可回匹配。
        /// 用 <c>Bill.GetUniqueLoadID()</c> 而非自造序号：它正是工资证据去重键所用的标识
        /// （`EconomyEmployment`），并随存档持久化，故跨读档稳定。
        /// </summary>
        internal static string BillKeyOf(Thing bench, Bill bill)
            => bench?.def == null || bill == null ? null
                : bench.def.defName + ":" + bench.thingIDNumber.ToString(CultureInfo.InvariantCulture)
                  + "#" + bill.GetUniqueLoadID();

        public bool TryReadReceipt(string proposalId, out string receipt) => ledger.TryReadReceipt(proposalId, out receipt);

        private static int CountOpenBills(Map map)
        {
            var buildings = map.listerBuildings?.allBuildingsColonist;
            if (buildings == null) return 0;
            int bills = 0, scanned = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (++scanned > EconomyProductionReadoutSource.MaximumDeviceScan) break;
                var stack = (buildings[i] as IBillGiver)?.BillStack;
                if (stack == null) continue;
                int limit = Math.Min(stack.Count, EconomyProductionReadoutSource.MaximumBillsPerDevice);
                for (int b = 0; b < limit; b++)
                {
                    var bill = stack[b];
                    if (bill != null && !bill.DeletedOrDereferenced) bills++;
                }
            }
            return bills;
        }
    }
}
