using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Domain.PawnEconomy.Planning
{
    internal enum ShadowDecision { WaitingFacts, NativeYield, NativeNeed, WaitingBudget, WaitingOpportunity, SeekWork, MaintainReserve, NoIncomeNeeded, OutOfScope, Error }
    internal sealed class ShadowNeedGoal
    {
        public string NeedId { get; }
        public CurrentNeedGap Gap { get; }
        public bool Active { get; }
        public int Priority { get; }
        /// <summary>
        /// v0.4 §9 饿死防护：该目标**已连续等待**的 tick 数，上限 <see cref="PlanningPolicy.MaximumWaitWeightTicks"/>。
        /// 只在**同级**排先后时使用；受阻/冻结/不适用/未知的项恒为 0（不得无限累积）。
        /// </summary>
        public int WaitingTicks { get; }
        /// <summary>本段等待的起点（0 = 本周期才开始等待）。用于跨周期续算，会话内传递，不落盘。</summary>
        public int ActiveSinceTick { get; }
        /// <summary>
        /// v0.4 §6 **风险时限**：按上一周期读数差分估算，距跌破风险阈值还有多少 tick。
        /// <c>0</c> = 已在风险线内；<c>-1</c> = 不可估（无基线 / 未在下降 / 不适用）—— 按 §6 用类别区间处理，**不制造伪精度**。
        /// </summary>
        public int RiskDeadlineTicks { get; }
        /// <summary>
        /// v0.4 §6 **slack = 风险时限 − 预计首次有效帮助时刻**。帮助时刻只取**可得**来源（该需求自身成本切片的
        /// <c>RequiredByTick</c>）；不可得时为 <c>-1</c>。负 slack 表示赶不上原计划，应改选最快的稳定/减损方案。
        /// </summary>
        public int SlackTicks { get; }
        public ShadowNeedGoal(string id, CurrentNeedGap gap, bool active, int priority,
            int waitingTicks = 0, int activeSinceTick = 0, int riskDeadlineTicks = -1, int slackTicks = -1)
        {
            NeedId = id; Gap = gap; Active = active; Priority = priority; WaitingTicks = waitingTicks;
            ActiveSinceTick = activeSinceTick; RiskDeadlineTicks = riskDeadlineTicks; SlackTicks = slackTicks;
        }
    }
    internal sealed class WorkOpportunity
    {
        public string Id { get; }
        public Fact<bool> Capable { get; }
        public Fact<bool> Executable { get; }
        public Fact<bool> Funded { get; }
        public bool Reachable { get; }
        public int ExpiresTick { get; }
        public int PaysAtTick { get; }
        public long WageMinor { get; }
        public int CurrentWorkPriority { get; }
        // Unit wages earn toward a reserve; they do not promise to cover the
        // entire budget in one payment. Planning never credits expected wages.
        public bool IncrementalIncome { get; }
        public bool PlayerPosted { get; }
        public WorkOpportunity(string id, Fact<bool> capable, Fact<bool> executable, Fact<bool> funded,
            bool reachable, int expires, int paysAt, long wage, int currentWorkPriority,
            bool incrementalIncome = false, bool playerPosted = false)
        {
            Id = MinorCurrency.Required(id); MinorCurrency.Validate(wage);
            Capable = capable; Executable = executable; Funded = funded; Reachable = reachable;
            ExpiresTick = expires; PaysAtTick = paysAt; WageMinor = wage; CurrentWorkPriority = currentWorkPriority;
            IncrementalIncome = incrementalIncome; PlayerPosted = playerPosted;
        }
        // Priority zero is player assignment state, NOT a capability prohibition.
        public bool Eligible(int now, int deadline, long minimumPayment) => Capable.TryGetValue(out bool c) && c && Executable.TryGetValue(out bool e) && e
            && Funded.TryGetValue(out bool f) && f && Reachable && ExpiresTick > now && PaysAtTick >= now && PaysAtTick <= deadline
            && WageMinor > 0 && WageMinor >= minimumPayment;
    }
    /// <summary>
    /// v0.9 §2 M4 的四类候选方案。**免费自救排在最前** —— v0.7 §6 第 5 条要求"免费方案确实同等时允许选免费"。
    /// </summary>
    internal enum PlanOptionKind { NativeSelfHelp, SeekWork, Purchase, Service }
    /// <summary>
    /// 单个候选方案的**可行性 + 不可行原因**。不可行必须给出原因码（复用既有 `Reason.*` 键），
    /// 不得静默省略 —— 玩家要能看到"为什么没选购买/服务"，而不是只看到一个决策结果。
    /// </summary>
    internal sealed class PlanOption
    {
        public PlanOptionKind Kind { get; }
        public bool Feasible { get; }
        /// <summary>不可行原因码（`Reason.*` 后缀）；可行或该方案在本场景不适用时为空串。</summary>
        public string ReasonCode { get; }
        public PlanOption(PlanOptionKind kind, bool feasible, string reasonCode)
        { Kind = kind; Feasible = feasible; ReasonCode = reasonCode ?? string.Empty; }
    }
    internal sealed class ShadowPlanningInput
    {
        public FactQueryResult Facts { get; }
        public LifePolicy Policy { get; }
        public Fact<FinancialAccountSnapshot> Account { get; }
        public IReadOnlyList<RequirementCostSlice> Costs { get; }
        public IReadOnlyList<WorkOpportunity> Opportunities { get; }
        public bool CostsComplete { get; }
        /// <summary>LR-04：成本源给出的**具体拒绝原因码**（无拒绝时为空串）。Domain 只透传，不解释、
        /// 不据此改变任何判定 —— 它只影响"等待报价"的原因文本。</summary>
        public string CostsIncompleteReason { get; }
        public bool OpportunitiesComplete { get; }
        public Fact<bool> NativeProtection { get; }
        public Fact<bool> EmploymentPermitted { get; }
        /// <summary>
        /// v0.9 §2 M9：已被台账判定**低可信度**的候选岗位 id 集合。由 Economy 层映射（它定义 id 格式），
        /// Domain 只收到一份集合、**不解析任何格式**。不填时为空集合（非 null）。
        /// </summary>
        public IReadOnlyCollection<string> LowConfidenceOpportunities { get; }
        public ShadowPlanningInput(FactQueryResult facts, LifePolicy policy, Fact<FinancialAccountSnapshot> account,
            IEnumerable<RequirementCostSlice> costs, bool costsComplete, IEnumerable<WorkOpportunity> opportunities,
            bool opportunitiesComplete, Fact<bool> nativeProtection, Fact<bool> employmentPermitted,
            IEnumerable<string> lowConfidenceOpportunities = null, string costsIncompleteReason = "CostEstimateUnavailable")
        {
            Facts = facts; Policy = policy ?? throw new ArgumentNullException(nameof(policy)); Account = account;
            Costs = Freeze(costs, PlanningPolicy.MaximumSlices); CostsComplete = costsComplete;
            CostsIncompleteReason = string.IsNullOrEmpty(costsIncompleteReason) ? "CostEstimateUnavailable" : costsIncompleteReason;
            Opportunities = Freeze(opportunities, PlanningPolicy.MaximumCandidates); OpportunitiesComplete = opportunitiesComplete;
            NativeProtection = nativeProtection; EmploymentPermitted = employmentPermitted;
            var low = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in lowConfidenceOpportunities ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(id)) low.Add(id);
            LowConfidenceOpportunities = low;
        }
        private static IReadOnlyList<T> Freeze<T>(IEnumerable<T> rows, int limit)
        {
            var copy = new List<T>();
            foreach (var row in rows ?? Array.Empty<T>()) { if (row == null || copy.Count == limit) throw new ArgumentException("PlanningInputCapacity"); copy.Add(row); }
            return copy.AsReadOnly();
        }
    }
    internal sealed class ShadowPlan
    {
        public string PawnId { get; }
        public long Epoch { get; }
        public int MapId { get; }
        public int BuiltTick { get; }
        public long FactsRevision { get; }
        public ShadowDecision Decision { get; }
        public string Reason { get; }
        public string SelectedOpportunity { get; }
        public LifePolicy Policy { get; }
        public LifeBudget Budget { get; }
        public Fact<bool> EmploymentApplicable { get; }
        public IReadOnlyList<ShadowNeedGoal> Goals { get; }
        public IReadOnlyList<string> LastKnownActiveRequirements { get; }
        /// <summary>
        /// 本次评估看到的**候选**岗位（上限 <see cref="PlanningPolicy.MaximumCandidates"/>）。
        /// 只读保留供只读页展示"为什么没选中"；不构成签约、派工或支付授权。
        /// </summary>
        public IReadOnlyList<WorkOpportunity> Opportunities { get; }
        /// <summary>
        /// 本周期的满足度读数快照（按 NeedId）。仅供**下一周期差分**估算消耗速率（§6 风险时限），
        /// 属会话内传递的工作数据：不落盘、不参与存档、不是玩家可见状态。
        /// </summary>
        public IReadOnlyDictionary<string, float> RatioSnapshot { get; }
        /// <summary>
        /// v0.4 §9：连续多少个 tick 由**基本保障**（<see cref="ShadowDecision.NativeNeed"/>）占满时间。
        /// 达到 <see cref="PlanningPolicy.UnsustainableStreakTicks"/> 即应反馈"原生活计划不可持续"。
        /// 只在同会话的连续计划间累计，中断即清零，跨会话不继承（不制造永久压力）。
        /// </summary>
        public int BasicNeedStreakTicks { get; }
        /// <summary>
        /// v0.4 §7 **制度性受阻**的需求键：这些必要项既缺资金、又无法通过劳动补足（补足通道被该需求本身卡住），
        /// 构成"需求 → 资金 → 劳动 → 需求"的环。对它们**不递归创建子目标**，也不驱动"为钱去工作"的死循环。
        /// </summary>
        public IReadOnlyList<string> InstitutionallyBlocked { get; }
        /// <summary>
        /// v0.9 §2 M4 候选方案集（免费自救 / 劳动 / 购买 / 服务）及各自可行性。
        /// 用于解释"为什么没选别的方案"；购买与服务在当前接入状态下**如实为不可行**（尚无价格/服务供给来源）。
        /// </summary>
        public IReadOnlyList<PlanOption> Options { get; }
        public ShadowPlan(string pawn, long epoch, int map, int tick, long revision, ShadowDecision decision,
            string reason, LifePolicy policy, LifeBudget budget, IEnumerable<ShadowNeedGoal> goals = null, string opportunity = null,
            Fact<bool> employmentApplicable = default, IEnumerable<string> lastKnownActive = null,
            IEnumerable<WorkOpportunity> opportunities = null, IEnumerable<KeyValuePair<string, float>> ratioSnapshot = null,
            int basicNeedStreakTicks = 0, IEnumerable<string> institutionallyBlocked = null,
            IEnumerable<PlanOption> options = null)
        {
            PawnId = pawn; Epoch = epoch; MapId = map; BuiltTick = tick; FactsRevision = revision; Decision = decision;
            Reason = reason; Policy = policy; Budget = budget; Goals = PerceptionCollections.Freeze(goals); SelectedOpportunity = opportunity;
            EmploymentApplicable = employmentApplicable;
            Opportunities = PerceptionCollections.Freeze(opportunities);
            var ratios = new Dictionary<string, float>(StringComparer.Ordinal);
            if (ratioSnapshot != null) foreach (var pair in ratioSnapshot) ratios[pair.Key] = pair.Value;
            RatioSnapshot = new System.Collections.ObjectModel.ReadOnlyDictionary<string, float>(ratios);
            BasicNeedStreakTicks = basicNeedStreakTicks;
            InstitutionallyBlocked = PerceptionCollections.Freeze(institutionallyBlocked);
            Options = PerceptionCollections.Freeze(options);
            LastKnownActiveRequirements = PerceptionCollections.Freeze(lastKnownActive ?? Goals.Where(g => g.Active).Select(g => g.Gap.RequirementKey));
        }
    }
    internal static class ShadowPlanBuilder
    {
        public static ShadowPlan Build(string id, long epoch, int map, int now, ShadowPlanningInput input, ShadowPlan previous)
            => BuildCore(id, epoch, map, now, input, previous, null);
        /// <summary>Session start: the persisted latch of the previous session seeds the hysteresis only
        /// when this session has no report for the subject yet, so a live report always wins over save.</summary>
        public static ShadowPlan BuildSeeded(string id, long epoch, int map, int now, ShadowPlanningInput input, ShadowPlanLatch seed)
            => BuildCore(id, epoch, map, now, input, null, seed);
        public static ShadowPlan BuildSeeded(string id, long epoch, int map, int now, ShadowPlanningInput input, ShadowPlan previous, ShadowPlanLatch seed)
            => BuildCore(id, epoch, map, now, input, previous, seed);
        private static ShadowPlan BuildCore(string id, long epoch, int map, int now, ShadowPlanningInput input, ShadowPlan previous, ShadowPlanLatch seed)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            bool same = previous != null && previous.PawnId == id && previous.Epoch == epoch && previous.MapId == map && previous.BuiltTick <= now;
            bool seeded = !same && seed != null && seed.PawnId == id && seed.MapId == map;
            bool carries = same || seeded;
            bool previousRefill = same ? previous.Budget?.LastKnownRefill == true : seeded && seed.RefillLatch;
            var carriedActive = same ? previous.LastKnownActiveRequirements : seeded ? seed.ActiveRequirements : null;
            var facts = input.Facts; var snapshot = facts?.Snapshot;
            var goals = new List<ShadowNeedGoal>();
            long end = now + (long)input.Policy.ReserveDays * PlanningPolicy.DayTicks;
            var budget = end <= int.MaxValue ? LifeBudgetPlanner.Build(input.Account, input.Costs, now, (int)end,
                input.CostsComplete, previousRefill, input.Policy.SavingBasisPoints, input.CostsIncompleteReason)
                : new LifeBudget(BudgetState.Inconsistent, "InvalidWindow");
            var ratios = new Dictionary<string, float>(StringComparer.Ordinal);
            // §7 制度性受阻集合：先声明、在 context 就绪后赋值 —— Result 是局部函数，
            // 其**早期 return 调用点**先于 context 就绪，故变量必须在这些点之前可见。
            var blocked = new List<string>();
            // M4 候选方案集同理：先声明、在 context 就绪后赋值。
            var options = new List<PlanOption>();
            // §6 的"预计首次有效帮助时刻"只取**可得**来源：该需求自身成本切片的 RequiredByTick（这笔钱什么时候要）。
            // 未接入成本来源时该表为空 ⇒ slack 一律不可估（如实，不造伪精度）。
            var helpTick = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var cost in input.Costs ?? Array.Empty<RequirementCostSlice>())
                if (!helpTick.ContainsKey(cost.RequirementKey)) helpTick[cost.RequirementKey] = cost.RequiredByTick;
            ShadowPlan Result(ShadowDecision decision, string reason, string selected = null)
            {
                // v0.4 §9：基本保障连续占满的时长 —— 只在同会话连续计划间累计，中断即清零，跨会话不继承。
                int streak = decision != ShadowDecision.NativeNeed ? 0
                    : same && previous != null ? previous.BasicNeedStreakTicks + Math.Max(0, now - previous.BuiltTick) : 0;
                return new ShadowPlan(id, epoch, map, now,
                    snapshot?.ObservationRevision ?? 0, decision, reason, input.Policy, budget, goals, selected,
                    snapshot?.Context.Data?.DevelopmentalStage.TryGetValue(out string age) == true
                        ? Fact<bool>.Known(age == "Adult") : Fact<bool>.Unknown("AgeUnknown"),
                    facts?.RequiredGroupsUsable != true && carries ? carriedActive : null,
                    input.Opportunities, ratios, streak, blocked,
                    // M4 四方案集**必须随计划出口**（缺陷修复 2026-10-09）：此前 Result 漏传此参，
                    // 导致 ShadowPlan.Options 恒为空 —— 四方案比较从未到达任何消费方
                    // （影子仲裁的可行/不可行行、循环④的真实三态全都读不到数据）。
                    // 早退路径（本函数定义之后的 return 点）此时读到的是 208 行的**初始空列表**，
                    // 这是正确语义：计划在产出方案比较之前就结束了。
                    options);
            }
            if (facts?.Readiness == PerceptionReadiness.OutOfScope) return Result(ShadowDecision.OutOfScope, "OutOfScope");
            if (snapshot == null || !facts.RequiredGroupsUsable || snapshot.PawnId != id || snapshot.SessionEpoch != epoch
                || snapshot.Context.Data?.MapId != map
                // ECON-FIX（2026-10-10 实测定位）：`Partial` 必须与 `Complete` 同等可用 —— 它意味着
                // "该组有数据但部分条目不支持/被截断"（unsupported / failed / truncated），规划消费的是
                // 条目列表本身，缺几条不该判死整份计划。此前只认 `Complete`，实测中因**一个**需求不支持
                // 就让 Needs 恒 Partial ⇒ 三名殖民者永久 WaitingFacts ⇒ 经济完全停摆。
                // 仍严格拒绝 Unknown / Error（那才是真的没数据）。
                || (snapshot.Context.State != CaptureState.Complete && snapshot.Context.State != CaptureState.Partial)
                || (snapshot.Needs.State != CaptureState.Complete && snapshot.Needs.State != CaptureState.Partial)
                || snapshot.Context.CapturedTick > now || snapshot.Needs.CapturedTick > now
                || (long)now - snapshot.Context.CapturedTick > 300 || (long)now - snapshot.Needs.CapturedTick > 300)
                return Result(ShadowDecision.WaitingFacts, "FactsUnavailable");
            var targets = input.Policy.Targets;
            bool urgent = false;
            foreach (var need in snapshot.Needs.Data?.Needs ?? Array.Empty<NumericNeedFact>())
            {
                if (!targets.TryGetValue(need.NeedDefName, out var target)) continue;
                var gap = CurrentNeedProjector.Project(id, need, target);
                float enter = need.NeedDefName == "Food" ? PlanningPolicy.FoodEnter : need.NeedDefName == "Rest" ? PlanningPolicy.RestEnter : PlanningPolicy.JoyEnter;
                float complete = 0;
                bool known = gap.ActualRatio.TryGetValue(out float ratio) && gap.TargetRatio.TryGetValue(out complete);
                bool frozen = need.IsFrozen.TryGetValue(out bool isFrozen) && isFrozen;
                bool wasActive = carries && carriedActive != null && carriedActive.Contains(gap.RequirementKey);
                bool active = known && !frozen && (ratio < enter || wasActive && ratio < complete);
                bool safety = !frozen && known && (need.NeedDefName == "Food" && ratio < PlanningPolicy.FoodNativeYield
                    || need.NeedDefName == "Rest" && ratio < PlanningPolicy.RestNativeYield);
                urgent |= safety;
                // v0.4 §9 饿死防护：等待时长只对**同级且可执行**的目标累计。
                // 冻结 / 未知 / 不适用项不累计（设计明令"受阻项不能不断累计优先级"）；
                // 等待起点从上一周期同目标的连续激活段续算，跨会话（seed）时从 0 重来，不制造永久欠账。
                int waiting = 0, since = active ? now : 0;
                if (active && carries && previous != null && previous.Goals != null)
                {
                    for (int i = 0; i < previous.Goals.Count; i++)
                    {
                        var prior = previous.Goals[i];
                        if (prior.NeedId != need.NeedDefName || !prior.Active) continue;
                        since = prior.ActiveSinceTick > 0 ? prior.ActiveSinceTick : previous.BuiltTick;
                        waiting = Math.Min(PlanningPolicy.MaximumWaitWeightTicks, Math.Max(0, now - since));
                        break;
                    }
                }
                // v0.4 §6：差分估算风险时限与 slack；读数快照留给下一周期用（会话内传递，不落盘）。
                if (known) ratios[need.NeedDefName] = ratio;
                int riskDeadline = RiskDeadline(same ? previous : null, need.NeedDefName, ratio, known, now);
                // v0.4 §6 帮助时刻取**多个可得来源的最小值**（越早到达越乐观）：
                //   ① 该需求自身成本切片的时限（这笔钱什么时候必须到位）
                //   ② 已发布且可行的岗位里**最早的工资到账时刻**（劳动也是"首次有效帮助"的一种形态）
                // 两个来源都缺 ⇒ slack 保持不可估。设计明令"无法可靠预测时限的目标用类别和区间处理"。
                int help = int.MaxValue;
                if (helpTick.TryGetValue(gap.RequirementKey, out int costHelp) && costHelp >= now) help = Math.Min(help, costHelp);
                foreach (var offer in input.Opportunities)
                    if (offer != null && offer.Eligible(now, int.MaxValue, 0) && offer.PaysAtTick >= now)
                        help = Math.Min(help, offer.PaysAtTick);
                int slack = riskDeadline >= 0 && help != int.MaxValue ? riskDeadline - (help - now) : -1;
                goals.Add(new ShadowNeedGoal(need.NeedDefName, gap, active,
                    safety ? 0 : need.NeedDefName == "Joy" ? 2 : 1, waiting, since, riskDeadline, slack));
            }
            RankGoals(goals);
            var context = snapshot.Context.Data;
            // v0.4 §7 前置链环检测：依赖边 = 需求 → 其所需资金。若"补足资金"的通道被基本保障或资格限制卡住，
            // 相关必要项即落入制度性受阻集合（不递归创建子目标，也不驱动"为钱去工作"的死循环）。
            bool labourAvailable = context.DevelopmentalStage.TryGetValue(out string labourStage) && labourStage == "Adult"
                && input.EmploymentPermitted.TryGetValue(out bool permittedLabour) && permittedLabour
                && !goals.Any(g => g.Active && g.Priority <= 1);
            budget.AvailableMinor.TryGetValue(out long availableCash);
            blocked = FindInstitutionallyBlocked(input.Costs, availableCash, labourAvailable);
            bool nativeSelfHelp = goals.Any(g => g.Active && g.Priority == 0);
            bool fundedOffer = false;
            foreach (var offer in input.Opportunities) if (offer != null && offer.Eligible(now, int.MaxValue, 0)) { fundedOffer = true; break; }
            bool permittedForWork = input.EmploymentPermitted.TryGetValue(out bool permittedNow) && permittedNow;
            // v0.9 §2 M4/M7：购买方案的可行性与**精确**不可行原因由纯函数判定（离线可证伪）。
            // 不再硬编码为不可行 —— 那会让"我有现金、机构也已公布报价"的玩家永远看到"购买不可行"。
            bool pricedConsumption = PricedConsumptionFeasible(input.Costs, input.CostsComplete, availableCash, out string purchaseReason);
            options = BuildPlanOptions(nativeSelfHelp, permittedForWork, fundedOffer, pricedConsumption, false, purchaseReason);
            if (urgent || !KnownFalse(context.Drafted) || !KnownFalse(context.PlayerForced) || !KnownFalse(context.Downed)
                || !KnownFalse(context.MentalState) || !KnownFalse(input.NativeProtection)) return Result(ShadowDecision.NativeYield, "NativeControlOrSafety");
            if (goals.Any(g => g.Active && g.Priority <= 1)) return Result(ShadowDecision.NativeNeed, "BasicNeedFirst");
            if (!context.DevelopmentalStage.TryGetValue(out string stage) || stage != "Adult"
                || !input.EmploymentPermitted.TryGetValue(out bool permitted) || !permitted) return Result(ShadowDecision.NativeYield, "EmploymentNotPermitted");
            if (budget.State != BudgetState.Ready) return Result(ShadowDecision.WaitingBudget, budget.Reason);
            if (!budget.RefillActive.TryGetValue(out bool refill) || !refill) return Result(ShadowDecision.NoIncomeNeeded, "ReserveSufficient");
            if (!context.TimeAssignmentDefName.TryGetValue(out string assignment) || assignment != "Anything" && assignment != "Work")
                return Result(ShadowDecision.NativeYield, "ProtectedTimetable");
            int deadline = (int)end; long cumulative = 0, minimumPayment = 0;
            budget.AvailableMinor.TryGetValue(out long cash);
            foreach (var cost in input.Costs.Where(c => c.Necessary).OrderBy(c => c.RequiredByTick))
            {
                cost.TotalCostMinor.TryGetValue(out long total);
                cumulative = checked(cumulative + total - cost.Coverage.Sum(c => c.AmountMinor));
                if (cumulative > cash) { deadline = cost.RequiredByTick; minimumPayment = cumulative - cash; break; }
            }
            var ranked = input.Opportunities.Where(x => x.Eligible(now, deadline, x.IncrementalIncome ? 0 : minimumPayment))
                .OrderBy(x => x.PaysAtTick).ThenByDescending(x => x.PlayerPosted)
                .ThenBy(x => x.Id, StringComparer.Ordinal).ToList();
            // v0.9 §2 M9「在不确定工资/服务与稳定方案之间作**有限**取舍」：
            // 可靠性偏好高于中性的 pawn 优先跳过已被台账判定低可信度的机构；**只避一次** ——
            // 没有替代候选时仍签约，仅以原因码标明这是已知风险（不得因此放弃收入）。
            var best = ranked.Count > 0 ? ranked[0] : null;
            bool underKnownRisk = best != null && input.LowConfidenceOpportunities.Contains(best.Id);
            if (Learning.ReliabilityChoiceRules.ShouldSwitchToAlternative(input.Policy.ReliabilityPreference,
                underKnownRisk, ranked.Count > 1))
            {
                for (int i = 1; i < ranked.Count; i++)
                {
                    if (input.LowConfidenceOpportunities.Contains(ranked[i].Id)) continue;
                    best = ranked[i]; underKnownRisk = false; break;
                }
            }
            if (best == null) return Result(ShadowDecision.WaitingOpportunity, input.OpportunitiesComplete ? "NoFeasibleOpportunity" : "OpportunityNotPublished");
            return Result(ShadowDecision.SeekWork, underKnownRisk ? "FundedOpportunityUnderKnownRisk"
                : best.IncrementalIncome ? "FundedIncrementalOpportunity" : "FundedOpportunity", best.Id);
        }
        private static bool KnownFalse(Fact<bool> fact) => fact.TryGetValue(out bool value) && !value;

        /// <summary>
        /// v0.4 §7 **前置链环检测**（纯函数，离线可证伪）：依赖边为「需求 → 它需要的资金」。
        /// 仅当**劳动通道不可用**（基本保障占满 / 资格不允许）且某必要项的资金缺口无法被现有覆盖抵掉时，
        /// 该需求才构成环 —— 此时既不能靠工作补足、也不该被重复索取，故标为制度性受阻。
        /// 劳动通道可用、资金充足、或该切片非必要 ⇒ 一律**不得**返回受阻（否则会静默压制正常规划）。
        /// </summary>
        internal static List<string> FindInstitutionallyBlocked(
            IReadOnlyList<RequirementCostSlice> costs, long availableMinor, bool labourChannelAvailable)
        {
            var blocked = new List<string>();
            if (labourChannelAvailable || costs == null) return blocked;
            foreach (var cost in costs)
            {
                if (cost == null || !cost.Necessary) continue;
                if (!cost.TotalCostMinor.TryGetValue(out long total)) continue;
                long covered = 0;
                foreach (var cover in cost.Coverage) if (cover != null) covered += cover.AmountMinor;
                if (total - covered <= availableMinor) continue;
                if (!blocked.Contains(cost.RequirementKey)) blocked.Add(cost.RequirementKey);
            }
            return blocked;
        }

        /// <summary>
        /// v0.9 §2 M7 **购买方案的可行性**（纯函数，离线可证伪）：存在**已定价的必要消费**
        /// （同窗同单位的真实报价，绝不拿 MarketValue 或固定餐数顶替），且买家自有现金在扣除已有覆盖
        /// （如随身食物）后仍足以支付。
        ///
        /// 本函数与 <see cref="FindInstitutionallyBlocked"/> 是**同一份数据的正反两面** ——
        /// 那边找"必要但付不起"的切片，这边找"必要且付得起"的切片。两处的遍历与覆盖算法必须永远一致，
        /// 否则会出现"预算说付不起、方案说买得起"的自相矛盾。
        ///
        /// 不可行**必须给精确原因**：<c>CostEstimateUnavailable</c>（成本未读全，不猜）/
        /// <c>NoPricedConsumption</c>（没有任何已公布报价）/ <c>ConsumptionUnaffordable</c>（有报价但付不起）。
        /// 三态合并会让玩家分不清"这里没有报价"与"我有报价但买不起"。
        /// </summary>
        internal static bool PricedConsumptionFeasible(IReadOnlyList<RequirementCostSlice> costs, bool costsComplete,
            long availableCash, out string blockCode)
        {
            if (!costsComplete) { blockCode = "CostEstimateUnavailable"; return false; }
            bool sawUnaffordable = false;
            if (costs != null)
            {
                for (int i = 0; i < costs.Count; i++)
                {
                    var cost = costs[i];
                    if (cost == null || !cost.Necessary) continue;
                    if (!cost.TotalCostMinor.TryGetValue(out long total) || total <= 0) continue;
                    long covered = 0;
                    foreach (var cover in cost.Coverage) if (cover != null) covered += cover.AmountMinor;
                    if (total - covered > availableCash) { sawUnaffordable = true; continue; }
                    blockCode = null;
                    return true;
                }
            }
            blockCode = sawUnaffordable ? "ConsumptionUnaffordable" : "NoPricedConsumption";
            return false;
        }

        /// <summary>
        /// v0.9 §2 M4 **方案比较**（纯函数，离线可证伪）：把四类方案的可行性一次摆清，顺序固定为
        /// 免费自救 → 劳动 → 购买 → 服务（v0.7 §6 第 5 条：免费方案确实同等时允许选免费）。
        /// 不可行的方案**必须带原因码**（复用既有 `Reason.*` 键），不得静默省略 ——
        /// 否则玩家只看到一个决策结果，看不到"为什么没选购买/服务"。
        /// </summary>
        internal static List<PlanOption> BuildPlanOptions(bool nativeSelfHelpAvailable, bool employmentPermitted,
            bool hasFundedOpportunity, bool hasPricedConsumption, bool hasServiceOffer, string purchaseReason = null)
        {
            var options = new List<PlanOption>(4)
            {
                // 免费自救不可行 ≠ 失败：只是尚未跌破原版自给线。仍须给码，否则界面会出现空括号。
                new PlanOption(PlanOptionKind.NativeSelfHelp, nativeSelfHelpAvailable,
                    nativeSelfHelpAvailable ? string.Empty : "SelfHelpNotNeeded"),
                new PlanOption(PlanOptionKind.SeekWork, employmentPermitted && hasFundedOpportunity,
                    !employmentPermitted ? "EmploymentNotPermitted" : hasFundedOpportunity ? string.Empty : "NoFeasibleOpportunity"),
                // v0.9 §2 M7：购买方案的不可行原因由调用方按真实数据三态给出（无报价 / 有报价却付不起 / 成本未读全）。
                // 三态**不可合并** —— 合并后玩家分不清"这里没有报价"和"我有报价但买不起"。
                new PlanOption(PlanOptionKind.Purchase, hasPricedConsumption,
                    hasPricedConsumption ? string.Empty : purchaseReason ?? "NoPricedConsumption"),
                // v0.9 §2 M7：服务仍如实不可行 —— 缺的是**服务供给机制**（价格字段已迁到体检中心建筑
                // 并在 B2 起由 `QueryServicePriceMinor` 消费；但"何时发起照护"的供给链仍不完整），
                // 不是缺岗位市场。借用岗位市场的 OpportunityNotPublished 会说错原因，
                // 把玩家引向错误的排查方向，故用专属码。
                new PlanOption(PlanOptionKind.Service, hasServiceOffer,
                    hasServiceOffer ? string.Empty : "ServiceNotImplemented"),
            };
            return options;
        }

        /// <summary>
        /// v0.4 §6 **风险时限**：用上一周期与本周期的读数差分估算消耗速率，推算距跌破风险线还有多少 tick。
        /// 任一前提不成立（读数未知 / 该需求无风险线 / 无基线 / 时间未推进 / 未在下降）⇒ 返回 <c>-1</c>（不可估）。
        /// 设计明令"无法可靠预测时限的目标用类别和区间处理，**不制造伪精度**"，故此处宁可返回不可估。
        /// </summary>
        private static int RiskDeadline(ShadowPlan previous, string needId, float ratio, bool known, int now)
        {
            if (!known) return -1;
            float threshold = needId == "Food" ? PlanningPolicy.FoodNativeYield
                : needId == "Rest" ? PlanningPolicy.RestNativeYield : 0f;
            if (threshold <= 0f) return -1;
            if (ratio <= threshold) return 0;
            if (previous == null || !previous.RatioSnapshot.TryGetValue(needId, out float prior)) return -1;
            int elapsed = now - previous.BuiltTick;
            if (elapsed <= 0) return -1;
            float rate = (prior - ratio) / elapsed;
            if (rate <= PlanningPolicy.MinimumDrainRatePerTick) return -1;
            double ticks = (ratio - threshold) / rate;
            return ticks >= int.MaxValue ? int.MaxValue : (int)ticks;
        }

        /// <summary>
        /// v0.4 §9 目标排序（纯函数，离线可单测；排序规则是本模块最容易被静默改错的一段）：
        /// ① **先等级** —— 等待加权永远不能突破等级（P2 等再久也排在 P1 之后）；
        /// ② 同级按**等待降序** —— 等得久的先办，缓解长期被挤占；
        /// ③ 等级与等待都一样时用**稳定键**（需求键，缺失时退回 NeedId）兜底，避免随机来回切换。
        /// </summary>
        internal static void RankGoals(List<ShadowNeedGoal> goals)
        {
            goals.Sort((a, b) =>
            {
                int order = a.Priority.CompareTo(b.Priority);
                if (order != 0) return order;
                // v0.4 §6 时限优先于 §9 等待加权：**可估时限者优先于不可估者**，都可估时 slack 小者更急
                // （负 slack 表示赶不上原计划，仍需排序，但由上层改选快速减损方案）。
                bool knownA = a.SlackTicks >= 0, knownB = b.SlackTicks >= 0;
                if (knownA != knownB) return knownA ? -1 : 1;
                if (knownA)
                {
                    order = a.SlackTicks.CompareTo(b.SlackTicks);
                    if (order != 0) return order;
                }
                order = b.WaitingTicks.CompareTo(a.WaitingTicks);
                return order != 0
                    ? order
                    : StringComparer.Ordinal.Compare(a.Gap?.RequirementKey ?? a.NeedId, b.Gap?.RequirementKey ?? b.NeedId);
            });
        }
    }
}
