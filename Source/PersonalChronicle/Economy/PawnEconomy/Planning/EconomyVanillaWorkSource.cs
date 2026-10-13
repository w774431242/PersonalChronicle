using System;
using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy.Perception;
using PersonalChronicle.Application.PawnEconomy.Planning;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Planning;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy.Planning
{
    /// <summary>
    /// Phase 1（CHG-20261010-VANILLA-JOBS）：把**玩家的原版工作动作**作为"工作合同"机会发布给**劳务站**。
    ///
    /// <para>识别口径（与需求「劳务站必须能识别玩家的动作」对齐）：读 <c>DesignationManager</c> 的
    /// 指定（本图 + 玩家派系），不轮询、零 Save。**本类不挂 Harmony** —— 来源判定（玩家下达 vs
    /// 建筑 / 植物自维护）由 Capture 层 <c>Patch_DesignationSource</c> 的只读挂点统一提供；
    /// 审计（2026-10-10）后的口径分工：**待办**只算玩家白名单，**计酬**另外排除自动黑名单：
    /// <list type="bullet">
    /// <item><c>PlantCutting</c> ← <c>DesignationDefOf.HarvestPlant</c> 且目标植物的 <c>IsTree</c>（伐木木材）。</item>
    /// <item><c>PlantCutting</c> ← <c>DesignationDefOf.CutPlant</c>（**割除**：草 / 灌木 / 播种与建造清障；
    ///       CHG-20261010-ECON-VANILLA-CUT —— 原版对植物有这两个独立 designation，只数前者会让
    ///       玩家对草/灌木下达的割除恒为 0）。</item>
    /// <item><c>Mining</c> ← <c>DesignationDefOf.Mine</c> / <c>MineVein</c>。</item>
    /// </list>
    /// 两类的"完成"已有既有捕获点并直连劳动证据链（<c>Patch_ItemFlowPlant</c> / <c>Patch_ItemFlowMining</c>
    /// → <c>ReportVanillaWorkCompletion</c>），因此本合同**不新建任何资金链**。</para>
    ///
    /// <para>发布方要求：**劳务站**（<c>InstitutionKind.LaborOffice</c>）。CHG-20261012 · B2 之后
    /// **不再有**"政策已发布工资 / 营业中 / 在时段内"三道门（机构政策整体退役，工资由每份合同的条款承载）。
    /// 本源也不要求绑定车间区 —— 原版工作与车间区无关。</para>
    ///
    /// <para>可及性判据 <c>reachable</c> 取 <c>true</c>：指定本身只存在于本图且已由玩家下达，
    /// 逐条 CanReach 会引入无界遍历（PERF-001）；原版派工仍会自行处理可达性。</para>
    /// </summary>
    internal sealed class EconomyVanillaWorkSource : IWorkOpportunitySource
    {
        private readonly GameComponent_PawnEconomy owner;
        public EconomyVanillaWorkSource(GameComponent_PawnEconomy owner)
        { this.owner = owner ?? throw new ArgumentNullException(nameof(owner)); }

        public IReadOnlyList<WorkOpportunity> ReadOpportunities(PerceptionSubject subject, int now, int deadline, out bool complete)
        {
            complete = false;
            var rows = new List<WorkOpportunity>();
            if (!owner.TryGetPlanningPawn(subject.PawnId, subject.MapId, out var pawn) || pawn == null || !pawn.Spawned || pawn.Dead) return rows;
            var map = pawn.Map;
            if (map == null || map.uniqueID != subject.MapId) return rows;
            var institutions = owner.QueryVanillaWorkInstitutions(subject.MapId);
            if (institutions.Count == 0) { complete = true; return rows; }   // 无劳务站：真实的零
            int hour = GenLocalDate.HourOfDay(map);
            for (int i = 0; i < institutions.Count && rows.Count < PlanningPolicy.MaximumCandidates; i++)
            {
                var reference = institutions[i];
                long? treasury = owner.QueryTreasuryAvailableMinor().HasValue ? owner.QueryPayrollFundingMinor(map, reference.AnchorKey) : (long?)null;
                if (reference.Anchor == null || !reference.Anchor.Spawned || reference.Anchor.IsBurning()) continue;
                // ── CHG-20261012-ECON-CONTRACTIZE：「工作合同弹窗」草拟的 **Posted（已派发）** 合同 ──
                // B2 之后这是**唯一**的"显式派发"入口（政策发布那条路径已随机构政策净删）。
                // 判据 = 状态 Posted + 未过期 + 合同时段允许（跨夜安全）。
                var posted = owner.ReadPostedContractsFor(reference.AnchorKey);
                for (int c = 0; c < posted.Count && rows.Count < PlanningPolicy.MaximumCandidates; c++)
                {
                    var contract = posted[c];
                    if (contract == null || contract.State != ContractState.Posted) continue;
                    if (contract.ExpiresTick <= now || !contract.AllowsHour(hour)) continue;
                    if (contract.WorkTypeDefNames.Count == 0) continue;
                    string postedWorkType = contract.WorkTypeDefNames[0];
                    var postedType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(postedWorkType);
                    if (postedType == null) continue;
                    bool postedCapable = !pawn.WorkTypeIsDisabled(postedType);
                    bool postedFunded = treasury.HasValue && treasury.Value >= contract.WagePerJobMinor;
                    int postedPriority = pawn.workSettings?.GetPriority(postedType) ?? 0;
                    rows.Add(new WorkOpportunity(contract.PositionKey,
                        Fact<bool>.Known(postedCapable), Fact<bool>.Known(true), Fact<bool>.Known(postedFunded), true,
                        now + PlanningPolicy.OpportunityLifetimeTicks, now + PlanningPolicy.WageTermTicks,
                        contract.WagePerJobMinor, postedPriority, incrementalIncome: true, playerPosted: true));
                }
                // CHG-20261012-ECON-CONTRACTIZE：**政策不再参与派工**（用户裁决「移除全部的机构政策」）——
                // 旧政策的"营业中 / 已发布工资 / 营业时段"三道门槛整体退役（时段权威来源改为合同自身条款，
                // 工资见下方基准常量）。本分支仅作**老档兜底**：待办 > 0 即可发布机会。
                _ = hour;
                for (int t = 0; t < GameComponent_PawnEconomy.VanillaWorkTypes.Length; t++)
                {
                    string workType = GameComponent_PawnEconomy.VanillaWorkTypes[t];
                    var type = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workType);
                    if (type == null) continue;
                    int backlog = owner.QueryVanillaWorkBacklog(map, workType, GameComponent_PawnEconomy.VanillaWorkBacklogCap);
                    int availableJobs = backlog - owner.VanillaJobsCommitted(map, workType, reference.AnchorKey);
                    if (availableJobs <= 0) continue; // Posted and active contracts already own these targets.
                    bool capable = !pawn.WorkTypeIsDisabled(type);
                    long quotedWage = owner.QuotedWorkPrice(map, reference.AnchorKey, workType, null, out _);
                    if (quotedWage <= 0) continue;
                    bool funded = treasury.HasValue && treasury.Value >= quotedWage;
                    int priority = pawn.workSettings?.GetPriority(type) ?? 0;
                    rows.Add(new WorkOpportunity(GameComponent_PawnEconomy.VanillaOpportunityKey(workType, reference.AnchorKey),
                        Fact<bool>.Known(capable), Fact<bool>.Known(true), Fact<bool>.Known(funded), true,
                        now + PlanningPolicy.OpportunityLifetimeTicks, now + PlanningPolicy.WageTermTicks,
                        quotedWage, priority, incrementalIncome: true));
                    if (rows.Count >= PlanningPolicy.MaximumCandidates) break;
                }
            }
            _ = deadline;
            complete = true;
            return rows;
        }
    }

    // CHG-20261012-ECON-CONTRACTIZE · B2：`EconomyCompositeWorkSource`（组合机会源）**已删除** ——
    // 它存在的唯一理由是"车间岗位源 + 原版工作源并列发布"，而车间岗位源（`EconomyInstitutionWorkSource`）
    // 已随机构政策一并净删 ⇒ 组合器退化为单源包装（零引用）。留着一个"可以再接回第二源"的空壳，
    // 与裁决⑦「工坊岗位链冻结」的封印哲学相冲突：要恢复车间岗位链必须显式新开批次（含政策替代物设计）。
}
