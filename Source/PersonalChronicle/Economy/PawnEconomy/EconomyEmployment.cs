using System;
using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy.Employment;
using PersonalChronicle.Application.PawnEconomy.Planning;
using PersonalChronicle.Automation;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Planning;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>B4 employment wiring: a pawn whose shadow plan selected a funded opportunity signs one
    /// contract for that position, the position's required work types are switched on through the
    /// ownership channel (EXC-2026-028), and real labour evidence creates exactly one payable that the
    /// treasury settles. Nothing here starts a job: vanilla ThinkTree still picks the work.</summary>
    public sealed partial class GameComponent_PawnEconomy
    {
        private PersonalChronicle.Data.PawnEconomy.EmploymentSaveData employment
            = new PersonalChronicle.Data.PawnEconomy.EmploymentSaveData();
        private EmploymentService employmentService;
        private EmploymentService Employment => employmentService ?? (employmentService = new EmploymentService(employment));
        private readonly Dictionary<string, string> recruitmentStates = new Dictionary<string, string>(StringComparer.Ordinal);

        internal string RecruitmentDiagnostic(Pawn pawn)
        {
            var plan = pawn?.Map == null ? null : QueryShadowPlan(pawn.GetUniqueLoadID(), pawn.Map);
            var mode = QueryInstitutionMode();
            if (plan == null) return "plan=Unavailable mode=" + mode?.Phase + " commitments=" + (mode?.NewCommitmentsAllowed == true);
            var budget = plan.Budget;
            var text = new System.Text.StringBuilder();
            text.Append("decision=").Append(plan.Decision).Append(" reason=").Append(plan.Reason)
                .Append(" mode=").Append(mode?.Phase).Append(" commitments=").Append(mode?.NewCommitmentsAllowed == true)
                .Append(" budget=").Append(budget?.State).Append(" cashMinor=").Append(FactAmount(budget?.AvailableMinor))
                .Append(" targetMinor=").Append(FactAmount(budget?.ReserveTargetMinor))
                .Append(" gapMinor=").Append(FactAmount(budget?.IncomeGapMinor))
                .Append(" selected=").Append(plan.SelectedOpportunity ?? "none");
            text.Append(" activeNeeds=[");
            foreach (var goal in plan.Goals) if (goal.Active) text.Append(goal.NeedId).Append(',');
            text.Append("] offers=").Append(plan.Opportunities.Count);
            foreach (var offer in plan.Opportunities)
            {
                if (!offer.PlayerPosted) continue;
                text.Append(" {posted=").Append(offer.Id).Append(" unitMinor=").Append(offer.WageMinor)
                    .Append(" capable=").Append(FactFlag(offer.Capable)).Append(" funded=").Append(FactFlag(offer.Funded))
                    .Append(" incremental=").Append(offer.IncrementalIncome).Append('}');
            }
            return text.ToString();
        }
        private static string FactAmount(PersonalChronicle.Domain.PawnEconomy.Perception.Fact<long>? fact)
            => fact.HasValue && fact.Value.TryGetValue(out long value) ? value.ToString() : "Unknown";
        private static string FactFlag(PersonalChronicle.Domain.PawnEconomy.Perception.Fact<bool> fact)
            => fact.TryGetValue(out bool value) ? value.ToString() : "Unknown";

        private void LogRecruitmentState(ShadowPlan plan, Pawn pawn, string stage)
        {
            bool posted = false;
            foreach (var offer in plan.Opportunities) if (offer.PlayerPosted) { posted = true; break; }
            if (!posted) return;
            string signature = plan.Decision + "|" + plan.Reason + "|" + stage + "|" + plan.SelectedOpportunity;
            string key = plan.PawnId + (stage == "PlanPublished" || stage == "InstitutionNotReady" ? "|Plan" : "|Submission");
            if (recruitmentStates.TryGetValue(key, out string previous) && previous == signature) return;
            if (recruitmentStates.Count >= 64 && !recruitmentStates.ContainsKey(key)) return;
            recruitmentStates[key] = signature;
            Log.Message("[PC][RECRUIT] " + pawn.LabelShort + " (" + plan.PawnId + ") stage=" + stage + " " + RecruitmentDiagnostic(pawn));
        }

        /// <summary>CHG-20261012-WORKGATE：工作合同闸门的读面 —— 把该 pawn 仍有效合同覆盖的工种并入
        /// <paramref name="into"/>（**只读**；由 <c>WorkAuthorityService</c> 在作业表重建时调用一次）。</summary>
        public void CollectWorkAuthorityWorkTypes(string pawnKey, HashSet<string> into)
            => CollectWorkAuthorityWorkTypes(PawnByKey(pawnKey), into);

        /// <summary>直接取当前 Pawn 实例，避免出生/抵达地图后尚未进入旧索引时误判无合同。</summary>
        public void CollectWorkAuthorityWorkTypes(Pawn pawn, HashSet<string> into)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Map == null || into == null) return;
            var availableInstitutions = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor != null && anchor.Spawned && !anchor.Destroyed && anchor.Map == pawn.Map && !anchor.IsBurning())
                    availableInstitutions.Add(anchor.GetUniqueLoadID());
            }
            var covered = new HashSet<string>(StringComparer.Ordinal);
            Employment.CollectActiveWorkTypesFor(pawn.GetUniqueLoadID(), Find.TickManager?.TicksGame ?? 0,
                GenLocalDate.HourOfDay(pawn.Map), availableInstitutions, covered);
            foreach (string name in covered)
            {
                var type = DefDatabase<WorkTypeDef>.GetNamedSilentFail(name);
                if (type != null && !pawn.WorkTypeIsDisabled(type) && (pawn.workSettings?.GetPriority(type) ?? 0) > 0)
                    into.Add(name);
            }
        }

        private WorkContract ActiveContractFor(string pawnKey)
        {
            var rows = employment.ReadContractsFor(pawnKey);
            for (int i = 0; i < rows.Count; i++)
                if (EmploymentSaveDataState(rows[i]) == ContractState.Active) return Employment.ReadContract(rows[i].ContractId);
            return null;
        }
        private static ContractState EmploymentSaveDataState(PersonalChronicle.Data.PawnEconomy.EmploymentSaveData.ContractRow row)
            => PersonalChronicle.Data.PawnEconomy.EmploymentSaveData.ParseState(row.State);

        /// <summary>
        /// M6 合理退出的**周期复查**（2026-10-09 补齐）：合同到期、达计件上限、制度关闭或岗位不可用时，
        /// 结束合同并**按所有权释放工作类型**（EMP-07）。释放本身仍走 CanRestore 门，玩家改写过的值保留。
        ///
        /// ⚠ 必须能在**制度门之前**调用：制度关闭时若整体跳过复查，已签合同会永久占着工作类型 ——
        /// 这正是本条实装要修的缺陷。合同已结束时返回 None，重复调用安全（幂等由 ShouldExit 保证）。
        /// </summary>
        private readonly Dictionary<string, PersonalChronicle.Domain.PawnEconomy.Learning.StyleLedger> styleLedgers
            = new Dictionary<string, PersonalChronicle.Domain.PawnEconomy.Learning.StyleLedger>(StringComparer.Ordinal);
        /// <summary>
        /// M9 学习账本（每 Pawn 一本）。**已落盘**（2026-10-09 U6-A5 更正过期注释）：`EconomyLearningPersistence`
        /// 在 `EconomyRuntime.ExposeData` 的 Saving 段导出、`PostLoadInit` 恢复（节点 `pawnEconomicLearning`），
        /// 旧档无该节点 ⇒ 空账（SAVE-003）。丢失的代价只是"人格随经历的微调"回退到中性基线，
        /// 不涉及任何金额或权利（与 WageCredibility 同级）。
        /// </summary>
        internal PersonalChronicle.Domain.PawnEconomy.Learning.StyleLedger LedgerFor(string pawnKey)
        {
            string key = pawnKey ?? string.Empty;
            if (!styleLedgers.TryGetValue(key, out var ledger))
            {
                ledger = new PersonalChronicle.Domain.PawnEconomy.Learning.StyleLedger();
                styleLedgers[key] = ledger;
            }
            return ledger;
        }

        // M9 有界退避账：键 = pawnId|actionClass|reason。失败数与下次可试时刻**只进不退** ——
        // "变化只解除相关原因，不把所有重试历史清零"（《09》§4）。
        private readonly Dictionary<string, int> retryFailures = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> retryNextTick = new Dictionary<string, int>(StringComparer.Ordinal);
        private static string RetryKey(string pawnKey, string actionClass, string reason)
            => (pawnKey ?? string.Empty) + "|" + (actionClass ?? string.Empty) + "|" + (reason ?? string.Empty);

        /// <summary>M9 退避门：冷却未到则不放行本次尝试（紧急与真实相关变化的放行权在调用方）。</summary>
        internal bool MayAttempt(string pawnKey, string actionClass, string reason, int now)
            => !retryNextTick.TryGetValue(RetryKey(pawnKey, actionClass, reason), out int next)
                || PersonalChronicle.Domain.PawnEconomy.Learning.RetryBackoffRules.MayRetry(now, next, false, false);

        /// <summary>记一次失败：失败数 +1，并按 <c>min(6000,150×2^min(n−1,6))</c> + 稳定抖动排下次可试时刻。</summary>
        internal void RecordAttemptFailure(string pawnKey, string actionClass, string reason, int now, string fingerprint)
        {
            string key = RetryKey(pawnKey, actionClass, reason);
            retryFailures.TryGetValue(key, out int failures);
            failures++;
            retryFailures[key] = failures;
            retryNextTick[key] = PersonalChronicle.Domain.PawnEconomy.Learning.RetryBackoffRules
                .NextRetryTick(now, failures, fingerprint, out _);
            TrimRetryLedger(now);
        }

        /// <summary>
        /// §6 有界历史：活跃重试最多 16 条，超出按**终态/年龄**裁剪。
        /// "终态"在此即**冷却已过**的条目 —— 它已无行动价值（下次直接放行），故可作为裁剪候选；
        /// 仍在冷却中的条目是"未终态"，只有在终态条目不够时才会被按年龄裁掉。
        /// 持有未清对账义务的条目一律不裁（凭据不随经验裁剪）。
        /// </summary>
        private void TrimRetryLedger(int now)
        {
            var rules = PersonalChronicle.Domain.PawnEconomy.Learning.BoundedHistoryRules.MaximumActiveRetries;
            if (retryNextTick.Count <= rules) return;
            var keys = new List<string>(retryNextTick.Keys);
            var rows = new List<PersonalChronicle.Domain.PawnEconomy.Learning.HistoryEntry>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                int next = retryNextTick[keys[i]];
                rows.Add(new PersonalChronicle.Domain.PawnEconomy.Learning.HistoryEntry(next <= now, next, false));
            }
            var drop = PersonalChronicle.Domain.PawnEconomy.Learning.BoundedHistoryRules.Trim(rows, rules);
            for (int i = 0; i < drop.Count; i++)
            {
                string key = keys[drop[i]];
                retryNextTick.Remove(key);
                retryFailures.Remove(key);
            }
        }

        /// <summary>该键当前连续失败数（0 = 无失败记录）。只读，供排查与展示。</summary>
        internal int ConsecutiveFailures(string pawnKey, string actionClass, string reason)
            => retryFailures.TryGetValue(RetryKey(pawnKey, actionClass, reason), out int failures) ? failures : 0;

        /// <summary>
        /// **原版作业计酬**（2026-10-10：需求"经济岗位池扩展到原版工作岗位"的最小可用落地）。
        /// 一次真实的原版作业完成（砍伐 / 采矿，由既有 Capture 挂点上报）⇒ 若该 Pawn 的活跃合同
        /// **声明了该工作类型**，则计一件工资。
        ///
        /// <para>复用整条劳动证据链（<c>AcceptEvidence</c> → <c>TrySettlePayable</c>）⇒ 幂等、欠薪补付、
        /// 读档保留全部免费获得，**不新建第二条资金链**。核对口径与 R01 同构：那里查"设备在合同车间内"
        /// （位置证据），这里查"完成的工作类型 = 合同声明的工作类型"（工作证据）。</para>
        ///
        /// <para>无合同 / 工作类型不符 ⇒ **零成本返回**（绝大多数原版作业不属任何经济合同）。
        /// 证据键含**作业对象**的稳定 ID ⇒ 同一 Pawn 同一 tick 完成两个不同对象各计一件，
        /// 而同一对象的重复回调被去重。</para>
        /// </summary>
        internal bool ReportVanillaWorkCompletion(string workTypeDefName, Thing target, Pawn pawn, int now)
        {
            if (pawn == null || string.IsNullOrEmpty(workTypeDefName)) return false;
            var contract = ActiveContractFor(pawn.GetUniqueLoadID());
            if (contract == null) return false;
            if (!DeclaresWorkType(contract, workTypeDefName)) return false;
            if (pawn.Map == null || contract.ExpiresTick <= now || contract.SignedTick > now
                || !contract.AllowsHour(GenLocalDate.HourOfDay(pawn.Map))) return false;
            if (!WorkPriceRules.Parse(contract.PositionKey, out string scopedWork, out string anchorKey, out string order)
                || scopedWork != workTypeDefName) return false;
            if (!string.IsNullOrEmpty(order) && order != CompletionOrder(workTypeDefName, target, pawn)) return false;
            bool validAnchor = false;
            for (int i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor != null && anchor.Spawned && !anchor.Destroyed && anchor.Map == pawn.Map
                    && !anchor.IsBurning() && anchor.GetUniqueLoadID() == anchorKey) { validAnchor = true; break; }
            }
            if (!validAnchor) return false;
            // CHG-20261010-ECON-JOB-AUDITFIX（审计 P1 修复）：**自动来源不付酬** ——
            // 建筑 / 植物自维护产生的标记（风电机自动清障、枯萎蔓延、树桩补标、矿脉续标）在合同存续期内
            // 同样会产生"真实劳动完成"，但那是建筑的意图，不是玩家的发布。
            // ⚠ 口径设计（关键）：这里只排除**黑名单**（自动来源）；白名单缺记的路径（例如
            // `Plant.GetGizmos` 的「全砍枯萎」命令 —— 它不进白名单但也不在黑名单）**仍会付酬**：
            // 工人确实干了活就该付，除非确认该目标为自维护产生。
            if (target != null && IsAutoIssuedTarget(target, workTypeDefName)) return false;
            string targetKey = target != null ? target.GetUniqueLoadID() : now.ToString();
            var evidence = new WorkEvidence(contract.ContractId,
                "vanilla:" + workTypeDefName + ":" + targetKey + ":" + pawn.GetUniqueLoadID(),
                "VanillaWork", now, 1, "job", now);
            var accepted = Employment.AcceptEvidence(evidence);
            if (accepted.Code != EmploymentCode.Applied) return false;
            return TrySettleAfterEvidence(pawn, accepted, now);
        }

        /// <summary>
        /// CHG-20261011-LABOR-VISUAL：**三个完成入口共用的统一出口**（三处必须同规则，缺一即为提前付款漏口）。
        ///
        /// <para>证据成立即已计一件工资；是否**当场付款**由应付的冻结资格裁决（唯一判据
        /// <see cref="MayPayObligation"/>）：</para>
        /// <list type="bullet">
        /// <item><b>旧模式 / 旧档应付</b>（<see cref="WageEligibility.Immediate"/>）⇒ 立即尝试结算 ——
        /// 与改动前逐字等价（零回归；旧欠薪、旧合同、旧历史全部不受影响）。</item>
        /// <item><b>新模式</b>（<see cref="WageEligibility.PendingSubmission"/>）⇒ **不付款**，工资以
        /// "已赚取 / 待交验"呈现；到工资结算处交验核验后才由补付链结算。</item>
        /// </list>
        /// <para>返回 true = 本次完成事件已被记账（**不是**"已到账"）—— 表现层据此区分"已赚"与"已付"。</para>
        /// </summary>
        private bool TrySettleAfterEvidence(Pawn pawn, WageResult accepted, int now)
        {
            var obligation = Employment.ReadObligation(accepted.ObligationId);
            if (obligation == null) return false;
            // CHG-20261011-LABOR-VISUAL：把**刚由原版真实生成**的产物实例登记为本证据的批次
            // （实物型交付的来源；无产品劳务不登记 —— 那会是伪造来源）。
            RegisterEvidenceOutputs(pawn, obligation, now);
            if (!MayPayObligation(obligation)) return true;   // 待交验/阻断：证据已记账，**不付**（不是失败）
            return TrySettlePayable(pawn, accepted.ObligationId, accepted.AmountMinor, now) == EmploymentCode.Applied;
        }

        /// <summary>合同声明的工作类型集合是否包含该原版工作类型（口径与签约时的 WorkTypeDefNames 同源）。</summary>
        private static bool DeclaresWorkType(WorkContract contract, string workTypeDefName)
        {
            var names = contract?.WorkTypeDefNames;
            if (names == null) return false;
            for (int i = 0; i < names.Count; i++)
                if (string.Equals(names[i], workTypeDefName, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// ECON-R01 履约身份核对：一次完成的迭代只有在**真的在合同岗位上**发生，才算本合同的劳动证据。
        /// 位置证据 = 账单设备当前所处的车间，正是合同绑定的那一个（<c>WorkshopAt</c> 返回 null = 设备不在任何车间）。
        /// 此前只检查"有合同" ⇒ 在别的工坊或普通工作台完成无关账单同样能领这份工资（单件上限只限金额、不证明履约）。
        /// 不合格的完成**不发工资、也不记为失败** —— 与本合同无关 ≠ 一次中断，不得污染 M9 退避与学习账。
        /// 证据去重键（<c>bill.GetUniqueLoadID() + "#" + PawnKey</c>）保持不变：它是持久化契约，
        /// 改格式会让旧档中已结算的完成事件被重新计薪。
        /// </summary>
        private bool FulfilsContractedPosition(Pawn pawn, Bill bill, WorkContract contract)
        {
            if (!TryParseOpportunity(contract.PositionKey, out string anchor, out int zoneId, out int taskType)) return false;
            var giver = bill.billStack?.billGiver as Thing;
            if (giver == null || giver.Map != pawn.Map) return false;
            var zone = PersonalChronicle.Automation.WorkshopZoneUtility.WorkshopAt(giver.Position, pawn.Map);
            if (zone == null || zone.ID != zoneId) return false;
            return PersonalChronicle.Automation.WorkshopZoneUtility.IsAlive(zone);
        }

        /// <summary>只读合同查询（供只读页展示）：当前履约中的合同，无则 null。**不**写任何状态。</summary>
        internal WorkContract QueryActiveContract(string pawnKey)
            => string.IsNullOrEmpty(pawnKey) ? null : ActiveContractFor(pawnKey);

        /// <summary>
        /// M6 退出预判的**只读**版本：与 <see cref="MaintainContract"/> 用同一份规则
        /// （<see cref="ContractExitRules.ShouldExit"/>）、同一批真实证据，但**不结束合同、不释放工作类型** ——
        /// 只读页必须能在不改状态的前提下回答"这份合同因何会被结束"（幂等由规则本身保证）。
        /// </summary>
        internal ContractExitCode QueryContractExit(Pawn pawn, int now)
            => EvaluateContractExit(pawn, now, out _);

        /// <summary>
        /// FA-10：退出判据的**唯一实现**。查询（界面预判）与执行（真实维护）必须读同一批证据 ——
        /// 修复前 `QueryContractExit` 已读 Desired / 班次记录 / 能力三项，`MaintainContract` 一项都没读，
        /// 于是界面提示"应退出"而实际合同与工作优先级都不退出（UI 与真实状态分叉，FA-10）。
        /// 本函数只判定；结束合同与释放工作类型由调用方负责。
        /// </summary>
        private ContractExitCode EvaluateContractExit(Pawn pawn, int now, out WorkContract contract)
        {
            contract = null;
            if (pawn == null || pawn.Map == null) return ContractExitCode.None;
            contract = ActiveContractFor(pawn.GetUniqueLoadID());
            if (contract == null) return ContractExitCode.None;
            // CHG-20261010-VANILLA-JOBS：原版工作合同没有区/班次 ⇒ 必须走它自己的判据；
            // 否则 parsed=false 会让 positionAvailable=false，读档后第一帧就把合同当场清退。
            if (IsVanillaOpportunity(contract.PositionKey))
                return EvaluateVanillaContractExit(contract, now);
            bool parsed = TryParseOpportunity(contract.PositionKey, out string _, out int zoneId, out int shiftLoadId);
            // CHG-20261012-ECON-CONTRACTIZE · B2（政策净删）：机构"营业"不再有政策来源 ⇒ 只剩**制度阶段**
            // 这一个闸门（下面 mode.Desired）。岗位可用性判据（区 / 班次记录 / 工种可上手）原样保留 ——
            // 存量车间合同因此**不被政策清退**，由制度阶段 + 三条岗位判据 + 件数 / 到期处置。
            bool institutionOpen = true;
            // ECON-R08：报价策略不是制度开关。玩家**主动关闭**（Desired=false）才按清退语义释放存量合同；
            // 仅"未就绪"（AllReady=false）**不**清退 —— 那是"不能签新的"，不是"旧的要撤"。
            var mode = QueryInstitutionMode();
            if (mode != null && !mode.Desired) institutionOpen = false;
            bool positionAvailable = parsed
                && PersonalChronicle.Automation.WorkshopZoneUtility.IsAlive(QueryWorkshopZone(pawn.Map, zoneId));
            // ECON-R08：区域存活 ≠ 岗位仍在 —— 班次被删同样使岗位失效。
            if (positionAvailable) positionAvailable = HasShiftRecord(pawn.Map, zoneId, shiftLoadId);
            // ECON-R08：Pawn 失去能力同样使岗位不可执行 —— 合同所需的每一种工作类型都必须仍可上手。
            if (positionAvailable) positionAvailable = CanStillWork(pawn, contract);
            return ContractExitRules.ShouldExit(contract, now, institutionOpen, positionAvailable);
        }

        /// <summary>
        /// ECON-R08：合同所需的工作类型在该 Pawn 上是否仍可上手（原版禁用判定，如伤残、特性、年龄变化）。
        /// 所需类型为空集合时返回 false —— 一份无法执行任何工种的合同不构成可履约岗位。
        /// </summary>
        private static bool CanStillWork(Pawn pawn, WorkContract contract)
        {
            if (pawn?.workSettings == null || contract?.WorkTypeDefNames == null || contract.WorkTypeDefNames.Count == 0) return false;
            for (int i = 0; i < contract.WorkTypeDefNames.Count; i++)
            {
                var type = DefDatabase<WorkTypeDef>.GetNamedSilentFail(contract.WorkTypeDefNames[i]);
                if (type == null || pawn.WorkTypeIsDisabled(type)) return false;
            }
            return true;
        }

        /// <summary>
        /// ECON-R08：岗位是否仍存在 —— 只看班次记录本身，**不**看当前是否在时段内。
        /// （<c>FindShift</c> 含 InTime 检查：时段外只是"现在不能开工"，不是"岗位没了"；
        /// 用它判定会让夜班合同每个白天被误退一次。）
        /// </summary>
        private bool HasShiftRecord(Map map, int zoneId, int loadId)
        {
            var zone = QueryWorkshopZone(map, zoneId);
            if (zone == null) return false;
            var manager = PersonalChronicle.Automation.MapComponent_WorkshopManager.Get(map);
            if (manager?.Shifts == null) return false;
            for (int i = 0; i < manager.Shifts.Count; i++)
            {
                var shift = manager.Shifts[i];
                if (shift != null && shift.LoadId == loadId && shift.TargetZone == zone) return true;
            }
            return false;
        }

        /// <summary>M9 购买退避只读读数：该 Pawn 的购买尝试连续失败数（0 = 无失败记录）。</summary>
        internal int QueryPurchaseBackoffFailures(string pawnKey) => ConsecutiveFailures(pawnKey, "Purchase", "Food");

        /// <summary>FA-10：执行侧复用 <see cref="EvaluateContractExit"/> 的判据（唯一实现），
        /// 判定应退出时结束合同并**按所有权释放**工作类型（玩家改写过的优先级保留）。</summary>
        internal ContractExitCode MaintainContract(Pawn pawn, int now)
        {
            var code = EvaluateContractExit(pawn, now, out var contract);
            if (code == ContractExitCode.None) return code;
            Employment.Terminate(contract.ContractId);
            ReleaseWorkTypes(pawn);
            return code;
        }

        /// <summary>Signs one contract for a selected opportunity and switches on only the work types
        /// that position declares. Returns false with a reason when any real precondition is missing.</summary>
        internal bool TrySignForOpportunity(string pawnId, int mapId, string opportunityKey, int now, out string reason)
        {
            reason = null;
            if (!TryGetPlanningPawn(pawnId, mapId, out var pawn) || pawn == null || pawn.Map == null) { reason = "PawnUnknown"; return false; }
            // CHG-20261010-VANILLA-JOBS：原版工作合同与车间岗位合同是**同一张表的两形态**，
            // 按 key 前缀分派解析（车间岗位 key = anchor/zone/shift；原版工作 key = vanilla:workType:anchor）。
            if (IsVanillaOpportunity(opportunityKey))
                return TrySignVanillaWork(pawnId, mapId, opportunityKey, now, out reason);
            if (string.IsNullOrEmpty(opportunityKey)
                || !TryParseOpportunity(opportunityKey, out string _, out int _, out int _))
            { reason = "OpportunityMalformed"; return false; }
            // CHG-20261012-ECON-CONTRACTIZE · B2（用户裁决⑦「工坊岗位链冻结 + 界面如实标注」）：
            // 车间岗位的**发布（机会源已删）与签约**整体冻结。本分支保留为**防御性收尾** —— 旧档残留的
            // 车间岗位键、或外部直接调用，一律拿到稳定 reason `WorkshopChainFrozen`（**不静默**，F4），
            // 而不是在这里重新长出一套"没有政策也要能签"的平行规则。
            reason = "WorkshopChainFrozen";
            return false;
        }

        private static bool TryParseOpportunity(string key, out string anchorKey, out int zoneId, out int shiftLoadId)
        {
            anchorKey = null; zoneId = 0; shiftLoadId = 0;
            int last = key.LastIndexOf('/'), middle = last < 0 ? -1 : key.LastIndexOf('/', last - 1);
            if (last <= 0 || middle <= 0) return false;
            if (!int.TryParse(key.Substring(middle + 1, last - middle - 1), out zoneId) || zoneId <= 0) return false;
            if (!int.TryParse(key.Substring(last + 1), out shiftLoadId) || shiftLoadId <= 0) return false;
            anchorKey = key.Substring(0, middle);
            return !string.IsNullOrEmpty(anchorKey);
        }

        /// <summary>One real vanilla recipe iteration = one piece of evidence. The dedupe key carries the
        /// bill instance, the phase and the tick, and the contract job cap bounds the total payment, so a
        /// repeated notification can never pay twice.</summary>
        internal bool ReportCraftIteration(Pawn pawn, Bill bill, int now)
        {
            if (pawn == null || bill == null || pawn.Map == null) return false;
            var contract = ActiveContractFor(pawn.GetUniqueLoadID());
            if (contract == null) return false;
            // ECON-R01：先证明**履约身份**，再发工资 —— "有合同"不等于"完成了合同工作"。
            if (!FulfilsContractedPosition(pawn, bill, contract)) return false;
            var evidence = new WorkEvidence(contract.ContractId, bill.GetUniqueLoadID() + "#" + contract.PawnKey,
                "Iteration", now, 1, "iteration", now);
            var accepted = Employment.AcceptEvidence(evidence);
            if (accepted.Code != EmploymentCode.Applied) return false;
            return TrySettleAfterEvidence(pawn, accepted, now);
        }

        /// <summary>
        /// ECON-R08 生命周期清理：**离图 / 被销毁的 Pawn 无法自己触发退出复查**（维护路径按 Pawn 查找），
        /// 其合同会因此永久占着岗位与工作类型。此处在周期槽里按 <c>ExpiresTick</c> 统一终止 ——
        /// 不依赖 Pawn 实例，因此对离图者同样生效。有界：单轮最多 32 份。
        /// 仍在地图上的 Pawn 顺手释放工作类型所有权（玩家改写过的优先级不会被覆盖）；
        /// 离图者的工作类型记录留在持久表里，等其回图或存档清理，**不在此强改**（无 Pawn 实例可写）。
        /// </summary>
        internal void TrimExpiredContracts(int now)
        {
            var expired = Employment.ReadExpiredContractIds(now, 32);
            for (int i = 0; i < expired.Count; i++)
            {
                var contract = Employment.ReadContract(expired[i]);
                Employment.Terminate(expired[i]);
                var pawn = contract == null ? null : FindSpawnedPawn(contract.PawnKey);
                if (pawn != null) ReleaseWorkTypes(pawn);
            }
        }

        /// <summary>CHG-20261012-ECON-CONTRACTIZE · B1（P1-2）：「工作合同」表的**撤销**写入口（唯一）。
        /// 只撤销 **Posted（已派发待承接）** 合同（规则在 <c>EmploymentService.RevokePosted</c>，可离线断言）：
        /// 成功 ⇒ 该合同的件数与资金预留当场释放；失败 ⇒ 稳定 reason ID（呈现层负责翻译，GOV-009），
        /// **绝不静默失败**（F4）。</summary>
        internal bool TryRevokePostedContract(string contractId, out string reason)
        {
            reason = null;
            var code = Employment.RevokePosted(contractId);
            if (code == EmploymentCode.Applied) return true;
            // ContractNotActive 在本入口的语义是"已经不是待承接状态"（已被接走 / 已结束）——用玩家能
            // 理解的稳定 ID 表达，避免把域枚举名直接塞给呈现层去翻译（GOV-009 的 key 形稳定）。
            reason = code == EmploymentCode.ContractNotActive ? "NotPosted" : code.ToString();
            return false;
        }

        /// <summary>在所有已加载地图上按键找**在图的** Pawn（不依赖 mapId；离图/已销毁返回 null）。</summary>
        private static Pawn FindSpawnedPawn(string pawnKey)
        {
            if (string.IsNullOrEmpty(pawnKey) || Find.Maps == null) return null;
            for (int m = 0; m < Find.Maps.Count; m++)
            {
                var pawns = Find.Maps[m].mapPawns?.AllPawnsSpawned;
                if (pawns == null) continue;
                for (int p = 0; p < pawns.Count; p++)
                    if (pawns[p].GetUniqueLoadID() == pawnKey) return pawns[p];
            }
            return null;
        }

        /// <summary>
        /// ECON-R06：把该 Pawn 的**未付**应付账逐条补付（有界：单次最多 16 条）。
        /// 幂等由 <see cref="TrySettlePayable"/> 的命令 id（"wage:" + obligationId）与账本 Replay 语义保证 ——
        /// 重试永不重复支付；资金不足时该条保持未付，留待下一个自然决策点（注资后自动到账，无需再做一件工作）。
        /// 合同终止不清除欠薪：本方法按 PayeeKey 读取应付账，与合同状态无关（对账而非重开凭证）。
        /// </summary>
        private void RetryOutstandingPayables(Pawn pawn, int now)
        {
            var pending = Employment.ReadObligationsFor(pawn.GetUniqueLoadID());
            if (pending == null || pending.Count == 0) return;
            int attempts = 0;
            for (int i = 0; i < pending.Count && attempts < 16; i++)
            {
                var row = pending[i];
                if (row == null || row.State == ObligationState.Paid) continue;
                long remaining = row.AmountMinor - row.PaidMinor;
                if (remaining <= 0) continue;
                // CHG-20261011-LABOR-VISUAL：补付同样过统一门 —— 新模式未交验的应付**不补付**
                // （否则"交验后才付"会被这条自动补付链绕过，T08/T18 复现的提前到账）。
                // 旧模式/旧档应付（Immediate）不受影响：门序仍**早于**制度开关，关制度吞不掉既欠工资。
                if (!MayPayObligation(row)) continue;
                attempts++;
                TrySettlePayable(pawn, row.ObligationId, remaining, now);
            }
        }

        /// <summary>M11 settlement of one payable. The command id is derived from the obligation, so the
        /// same payable can never be transferred twice; a funding failure leaves it outstanding.</summary>
        internal EmploymentCode TrySettlePayable(Pawn pawn, string obligationId, long amountMinor, int now)
        {
            if (pawn == null || string.IsNullOrEmpty(obligationId) || amountMinor <= 0) return EmploymentCode.InvalidCommand;
            var obligation = Employment.ReadObligation(obligationId);
            if (obligation == null) return EmploymentCode.ObligationUnknown;
            if (obligation.State == ObligationState.Paid) return EmploymentCode.AlreadyPaid;
            if (obligation.PayeeKey != pawn.GetUniqueLoadID() || obligation.AmountMinor - obligation.PaidMinor != amountMinor)
                return EmploymentCode.InvalidCommand;
            // CHG-20261011-LABOR-VISUAL：**付款门**（唯一判据，与补付链共用）。资格在应付创建时冻结并随行持久，
            // 因此旧档应付（缺键 ⇒ Immediate）与旧模式链**逐字不变**；新模式未交验 ⇒ SubmissionRequired
            // （**不是失败**：留待到站交验后补付）。未知/阻断资格 ⇒ EligibilityBlocked（fail-closed，不付款）。
            if (!MayPayObligation(obligation))
                return obligation.Eligibility == WageEligibility.PendingSubmission
                    ? EmploymentCode.SubmissionRequired : EmploymentCode.EligibilityBlocked;
            string command = "wage:" + obligationId;
            var previous = settlement.ReadCommand(command);
            string payer = previous != null ? previous.Intent.PayerAccount ?? SettlementLedgerNames.Treasury
                : StagePayrollFunds(pawn.Map, amountMinor, Employment.ReadContract(obligation.ContractId)?.InstitutionKey);
            if (previous == null && payer == null)
            { NoteLaborPaymentUnavailable(pawn, obligation, now); return EmploymentCode.PaymentUnavailable; }
            var intent = previous?.Intent ?? SettlementIntent.ForWage(command, settlement.Version, obligationId,
                pawn.GetUniqueLoadID(), amountMinor, payer == SettlementLedgerNames.Treasury ? null : payer);
            var result = settlement.PayWage(intent);
            EmploymentCode code;
            bool justPaid = false;   // CHG-20261011-LABOR-VISUAL：**首次付清**（MarkPaid 的 Applied）才播到账反馈
            if (result.Code == SettlementCode.Replay)
            {
                // Only a finalized transfer counts as paid; a cancelled or in-flight entry must not be
                // recorded as settled (it stays payable and will be retried).
                if (result.Entry == null || result.Entry.Phase != SettlementPhase.Finalized) code = EmploymentCode.PaymentUnavailable;
                else
                {
                    justPaid = Employment.MarkPaid(obligationId, amountMinor, command) == EmploymentCode.Applied;
                    code = EmploymentCode.AlreadyPaid;
                }
            }
            else if (result.Code == SettlementCode.Finalized)
            {
                code = Employment.MarkPaid(obligationId, amountMinor, command);
                justPaid = code == EmploymentCode.Applied;
            }
            else if (result.Code == SettlementCode.CommandConflict) code = EmploymentCode.PaymentConflict;
            else code = EmploymentCode.PaymentUnavailable;
            RecordSettlementOutcome(obligation, code, now);
            if (code == EmploymentCode.PaymentUnavailable || code == EmploymentCode.PaymentConflict)
                NoteLaborPaymentUnavailable(pawn, obligation, now);
            // §7.1 到账反馈：**一次**，且只在账本 Finalized 且应付首次标记为已付时（重放/幂等不回播，T20）。
            if (justPaid)
            {
                // 流程补记（逐阶到"工资已到账"）与表现反馈**分离**：前者是业务事实，后者可失败。
                NotePaymentConfirmed(obligation.ContractId, now);
                var paidFlow = LaborFlow.ReadWorkflow(obligation.ContractId);
                if (paidFlow != null) LaborFlow.RecordEvent(paidFlow, command + "|paid", "PaymentConfirmed", now,
                    obligation.EvidenceId, obligation.SubmissionId, 0, result.Entry.Intent.AmountMinor, "Finalized");
                PostLaborSpeech(pawn, "PC_PEA.LaborVisual.Speech.Paid", command + "|paid", (result.Entry.Intent.AmountMinor / 100m).ToString("0.00"));
            }
            return code;
        }

        /// <summary>M9: one real settlement outcome feeds the bounded wage credibility ledger. Counts and
        /// delay only — losing it can never lose a right and it is never a payment credential.</summary>
        private void RecordSettlementOutcome(WageObligation obligation, EmploymentCode code, int now)
        {
            var contract = Employment.ReadContract(obligation.ContractId);
            if (contract == null || string.IsNullOrEmpty(contract.InstitutionKey)) return;
            // §5.3 信用时点：延迟只从"雇主**已经可以付款**"起算 —— 新模式在交验核验成立前不构成雇主欠薪
            // （交货/搬运路程不记为雇主失败）。旧模式在创建时 EligibleTick == DueTick ⇒ 旧链读数逐字不变。
            long baseline = obligation.EligibleTick > 0 ? obligation.EligibleTick : obligation.DueTick;
            long delay = now > baseline ? (long)now - baseline : 0;
            bool success = code == EmploymentCode.Applied || code == EmploymentCode.AlreadyPaid;
            RecordWageOutcome(contract.InstitutionKey, success, delay, now);
            // v0.9 §2 M9：一次真实结算结果同时是**人格慢调**的一条摘要。方向是明确的 ——
            // 该维（ReliabilityPreference）定义即"对到账延误、排队、失败证据的敏感度"：
            // 按时足额到账 ⇒ 敏感度下调；失败或延误 ⇒ 上调。幅度由 StyleLedger 按当日额度限幅、
            // 按摘要 id 去重（同一笔应付只算一次），一次事故不会永久改变风格。
            string summary = "wage:" + obligation.ObligationId + (success && delay == 0 ? ":OnTime" : ":Unreliable");
            LedgerFor(obligation.PayeeKey).Deliver("ReliabilityPreference", summary,
                success && delay == 0 ? -1f : 1f, .5f, true);
        }
        /// <summary>Acts on a freshly published decision: only a funded-opportunity selection starts a
        /// signing attempt, only when this pawn has no front-office contract yet and the institution has
        /// actually opened new commitments (InstitutionPhase.Active with every readiness gate proven).</summary>
        void IShadowPlanningSink.OnPlanPublished(ShadowPlan plan)
        {
            if (plan == null) return;
            if (!TryGetPlanningPawn(plan.PawnId, plan.MapId, out var pawn) || pawn == null) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            // M6 合理退出复查**先于**制度门：制度关闭时若整体跳过，已签合同会永久占着工作类型（EMP-07）。
            // 计划发布是设计认可的"自然决策点"，退出复查挂在这里既及时又不引入每 tick 轮询。
            MaintainContract(pawn, now);
            // M9 §6：当日额度按**游戏日序号**推进（计划发布是设计认可的自然决策点）。
            // 同一天（读档 / Tick 回退）时 BeginDay 返回 false 且**不清**额度 ⇒ 不会重获已消费的学习额度。
            LedgerFor(plan.PawnId).BeginDay(now / PersonalChronicle.Domain.PawnEconomy.Planning.PlanningPolicy.DayTicks);
            // ECON-R06：欠薪补付（有界、幂等）。此前新工资支付**只**由新工作证据触发 —— 支付失败后
            // （资金不足）即使后来注资也再无机会：证据已 Replay、单件上限不再接受新证据，欠薪成死账。
            // 挂在同一自然决策点上补付；**早于**制度门，因为履约补付不是新承诺（关制度不能吞掉既欠工资）。
            RetryOutstandingPayables(pawn, now);
            if (!NewCommitmentsAllowed()) { LogRecruitmentState(plan, pawn, "InstitutionNotReady"); return; }
            LogRecruitmentState(plan, pawn, "PlanPublished");
            // ECON-R04：规划说「让路原版」或「事实缺失」时，执行层**不得**产生经济副作用 ——
            // 规划的最终判决必须与执行一致（此前发布任何计划都会先试交易/穿戴：界面写着"让路原版"，
            // 执行层却仍在花钱、仍会即时改穿戴）。注意 NativeNeed **不在**此列：原版需求更急时，
            // 买一份食物正是它的解法，挡掉反而违背规划。
            if (plan.Decision == ShadowDecision.NativeYield || plan.Decision == ShadowDecision.WaitingFacts) return;
            // 计划发布到执行之间 Pawn 状态可能已变（被征召 / 倒地 / 精神崩溃）：发起交易前复核当前保护状态。
            if (pawn.Downed || pawn.Drafted || pawn.InMentalState) return;
            // A hungry pawn with its own money buys a meal first; only a pawn still needing income is
            // signed to a position. Both paths need real evidence (published price, real stock, cash).
            // M9 §5（CHG-20261009-ECON-M9-S5）：一次购买失败只经**一个**判决入口写记录 ——
            // 定格证据（货是否已到买方 / M11 结算行阶段）在 EconomyConsumption 内采集；
            // 让路、适配未知与已结算三类由判决零记录（§4 / §5 第 5 条）。
            // ECON-R07：退避按**动作**隔离 —— 食品动作被退避时只跳过食品，衣物与求职照常推进。
            // 此前一个 return 把整条链挡掉：越没钱越买不成 ⇒ 越求职不得 ⇒ 越没钱（死锁）。
            if (MayAttempt(plan.PawnId, "Purchase", "Food", now))
            {
                if (TryPurchaseFoodAtCounter(pawn, now, out PurchaseAttempt attempt)) return;
                ResolvePurchaseAttempt(plan, attempt, now);
            }
            // 闭环 §6 第 5–7 条：食物链不适用（不饿 / 无报价 / 买不起）时，尝试**衣物**购买与穿戴。
            // 本批**不接** M9 退避账：衣物链的失败多数是"未挂牌 / 无库存"这类长期条件，
            // 逐次记账只会把退避历史刷满，反而掩盖真正的可重试失败。
            if (TryPurchaseApparelAtCounter(pawn, now, out PurchaseAttempt _)) return;
            if (plan.Decision != ShadowDecision.SeekWork || string.IsNullOrEmpty(plan.SelectedOpportunity)) return;
            if (ActiveContractFor(plan.PawnId) != null) return;
            // CHG-20261011-LABOR-VISUAL：**不再远程直签**。计划只形成"办理意图"（IntentReady），
            // 真正的 SignCommit 发生在 Pawn **寻路抵达劳务站柜台之后**（Job `PC_PEA_VisitLaborOffice`，
            // 到站复验机会/工时/能力/资金并原子占用）。此处既不占岗、不启用工种，也不产生任何工资承诺。
            bool intent = TryBeginLaborIntent(plan.PawnId, plan.MapId, plan.SelectedOpportunity, now, out string intentReason);
            LogRecruitmentState(plan, pawn, intent ? "IntentAccepted" : "IntentRejected:" + intentReason);
        }
        /// <summary>Single governance gate for new economic commitments. Until the institution reaches
        /// Active (which needs the deferred save-acceptance unit) nothing new can be promised.</summary>
        private bool NewCommitmentsAllowed()
        {
            var mode = QueryInstitutionMode();
            return mode != null && mode.NewCommitmentsAllowed;
        }
        internal IReadOnlyList<WageObligation> QueryPayables(string pawnKey) => Employment.ReadObligationsFor(pawnKey);
        internal int QueryContractCount => Employment.Contracts;
    }

    internal static class EmploymentPolicy
    {
        /// <summary>Prototype terms: one guaranteed payment per signed position (B3.1 slots are a single
        /// funded payment), one in-game day of validity, matching the planning wage term.</summary>
        public const int MaxJobsPerPosition = 1, ContractTermTicks = PlanningPolicy.DayTicks;
    }
}
