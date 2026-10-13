using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Application.PawnEconomy.Governance;
using PersonalChronicle.Data;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Governance;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy
    {
        private InstitutionModeSaveData institutionMode = new InstitutionModeSaveData();
        private InstitutionModeService modeService;
        private InstitutionReadiness modeReadiness = new InstitutionReadiness(false, false, false, false, false, false, false);
        private bool modeInitialized, modeOutstanding;
        private InstitutionModeSnapshot modeSnapshot;
        private InstitutionModeState modeSnapshotState;
        // Old code remains a reference implementation, never a fallback payment owner.
        internal bool LegacyFeesAllowed => false;
        private InstitutionModeService ModeService => modeService ?? (modeService = new InstitutionModeService(institutionMode.State));
        public InstitutionModeSnapshot QueryInstitutionMode()
        {
            if (modeSnapshot == null || !ReferenceEquals(modeSnapshotState, institutionMode.State))
            { modeSnapshotState = institutionMode.State; modeSnapshot = new InstitutionModeSnapshot(institutionMode.State, modeReadiness, modeOutstanding); }
            return modeSnapshot;
        }
        /// <summary>制度未开放时**第一项待完成门**的「下一步操作」文本（唯一实现：门已开放或无待办门 ⇒ 空串）。
        /// 2026-10-12 实机反馈驱动：新殖民地未完成一次存档往返时制度停在 Preparing，玩家此前在劳务站/右键只看到
        /// 「制度尚未开放新的用工承诺」，无从得知要做什么。本方法让**三处**（经济页逐门列表 / 劳务站 inspect /
        /// 右键失败提示）共用同一条组装与同一条回落纪律。**只读**当前快照：不写状态、不改阶段、不触发规划。</summary>
        internal static string FirstGateStep()
        {
            var mode = Current?.QueryInstitutionMode();
            if (mode == null || mode.NewCommitmentsAllowed || mode.Blockers == null || mode.Blockers.Count == 0)
                return string.Empty;
            return EconomyLabels.GateStep(mode.Blockers[0]);
        }
        internal void ApplyInstitutionModeSetting(bool requested)
        {
            if (Scribe.mode != LoadSaveMode.Inactive) return;
            settlement.BindToMainThread();
            var original = settlement.ReadLegacyArchive();
            if (requested && State.Revision >= 0 && !institutionMode.Frozen && !institutionMode.PreserveRaw)
            {
                if (original == null) { settlement.CaptureLegacy(State.Ready ? PawnEconomySaveData.Encode(State) : save.OriginalPayload, "BeforeInstitutionRequest"); original = settlement.ReadLegacyArchive(); }
                institutionMode.Freeze(State.Ready ? PawnEconomySaveData.Encode(State) : save.OriginalPayload,
                    original?.Hash, State.Revision, settlement.Version);
            }
            var final = institutionMode.ReadCutover();
            if (final != null) settlement.CaptureCutover(final.Raw);
            ReconcileTreasuryOperations(requested);
            var audit = settlement.ReadAudit();
            var plan = institutionMode.Frozen ? ReadCutoverPlan() : null;
            bool oldRights = institutionMode.Frozen && (plan == null || !plan.Complete);
            // FA-03（经理侧审计 2026-10-10）：此前 TotalLiabilitiesMinor/ActualReserveMinor **非零** 即算 outstanding，
            // 于是"库里有正常托管余额"被当成"未完成事项" —— 关闭一次就再也开不回来（Draining 永久停留，
            // 见 InstitutionModeService「Never bypass unfinished rights by toggling back on」）。
            // 正确口径：只有**在途/待对账的结算行**与**旧权益未承接**才是未完成事项；
            // 账户余额与专用储备是**已归属的正常保留资产**，不阻断重新开启，也不要求归零。
            // 故障不在此判定（readiness.Fault 会走 Faulted），避免把"故障"与"未完成承诺"混为一谈。
            modeOutstanding = oldRights || treasuryOperations.Pending != null || HasUnresolvedSettlementRows();
            string fault = institutionMode.State.Fault ?? ReadLiveFault(original, final, audit, plan);
            // 「完整游戏存档与恢复验收」（B2.4.3）：闸门由**真实证据**驱动，不再是硬编码 false
            // （硬编码会让制度永远停在 Preparing，见变更标注_CHG-20261009-ECON-SAVEACCEPT）。
            // 证据 = ① 本次会话确由存档恢复（LoadedFromSave，会话内标志、不落盘）
            //        ② 制度已冻结  ③ 恢复后账本健全（audit 与托管操作均无 fault）。
            // 通过后把「已证明版本」写回存档（幂等），此后新会话同样成立；恢复出现 fault 时本分支
            // **不标记**，闸门保持关闭 —— 能力只能由真的恢复过证明，不由设置开关或加载标志冒充。
            if (institutionMode.LoadedFromSave && institutionMode.Frozen
                && audit.Fault == null && treasuryOperations.Fault == null)
                institutionMode.MarkSaveRecoveryProven(institutionMode.SourceRevision);
            // 适用面收窄（2026-10-12 用户裁决 + EXC-2026-031，CHG-20261012-INSTGATE-EXEMPT）：
            // 该门的证明对象是"制度开启时**已存在**的旧账/账本内容"；新殖民地（或旧经济从未使用过）在冻结时
            // 两者皆空，没有可被恢复保护的既得资金 ⇒ 本门对该档**判不适用（N/A）**，且**不写任何伪证据**。
            // 判据全取自**冻结时刻的持久事实**（旧账快照 + LedgerVersionAtFreeze + 在途托管单），故单调、可审计、
            // 不随后续收支翻转；有旧账、冻结时已有账本条目、或有在途托管单的档，语义**一律不放宽**（仍需真实往返）。
            bool nothingToRecover = NothingToRecover(original);
            // Preparatory physical deliveries are authorized separately; the save/recovery gate now
            // reads the proven round trip (above) instead of blocking unconditionally.
            modeReadiness = new InstitutionReadiness(institutionMode.Frozen, institutionMode.Frozen && !oldRights,
                audit.Fault == null, treasuryOperations.Fault == null, true,
                institutionMode.RecoveryProvenRevision >= 0 || nothingToRecover, true, fault);
            ModeService.Observe(requested, modeReadiness, modeOutstanding);
            institutionMode.State = ModeService.State; modeInitialized = true;
            modeSnapshot = null;
        }
        /// <summary>「无可恢复内容」的唯一判定（**只读**；判据全部取自冻结时刻的持久事实 ⇒ 单调、可审计）：
        /// ① 制度已冻结、旧账快照可读且无故障；
        /// ② 冻结时**账本条目为 0**（<see cref="InstitutionModeSaveData.LedgerVersionAtFreeze"/> 即
        /// <c>settlement.Version</c> = <c>FundedSettlementLedger.Version</c> = <c>entries.Count</c>
        /// ⇒ 既无结算行、也无已完成/在途储备移动）；
        /// ③ **无在途托管操作**（存款单等由 <c>treasuryOperations</c> 独立承载、**不进账本** ⇒ 必须单独判；
        /// 2026-10-12 由 DATA 席复核指出，不能由 ② 蕴含）；
        /// ④ 旧账信封未启用/未注资，且既无记录也无现金债权（空 raw 时 <c>Report</c> 为 null —— 这正是"真的没有旧账"）。
        /// 任一条不成立 ⇒ 该档仍需一次真实的存档→恢复往返证明。</summary>
        private bool NothingToRecover(LegacySettlementArchive original)
        {
            if (!institutionMode.Frozen || original == null || original.Fault != null) return false;
            if (institutionMode.LedgerVersionAtFreeze != 0) return false;
            if (treasuryOperations?.Pending != null) return false;
            if (original.Claims.Count != 0) return false;
            var report = original.Report;
            if (report == null) return true;
            return report.Records.Count == 0 && !report.Enabled && !report.Funded;
        }
        private void TickInstitutionMode()
        {
            bool requested = PersonalChronicleMod.Settings?.EnablePawnEconomicInstitution == true;
            if (!modeInitialized || requested != institutionMode.State.Desired || Find.TickManager.TicksGame % 250 == 0)
                ApplyInstitutionModeSetting(requested);
        }

        /// <summary>
        /// FA-23：**实时**故障源（不含已持久化的 <c>State.Fault</c>）。抽出这一个读法的两个原因：
        /// ① 设置切换路径只观察它（不写），② 显式重新核验命令必须能独立重算"故障到底还在不在"。
        /// </summary>
        private string ReadLiveFault(LegacySettlementArchive original, LegacySettlementArchive final,
            SettlementAudit audit, LegacyCutoverPlan plan)
        {
            string fault = audit?.Fault ?? treasuryOperations.Fault ?? plan?.Fault;
            if (State.Revision < 0) fault = fault ?? "InvalidLegacyRevision";
            if (institutionMode.Frozen && original?.Hash != institutionMode.OriginalHash) fault = fault ?? "LegacyArchiveBindingChanged";
            if (final != null && final.Hash != settlement.ReadCutoverArchive()?.Hash) fault = fault ?? "FinalCutoverBindingChanged";
            if (institutionMode.Frozen && State.Revision != institutionMode.SourceRevision) fault = fault ?? "LegacyStateChangedAfterFreeze";
            if (institutionMode.Frozen && (final?.Fault != null || original?.Fault != null)) fault = fault ?? "LegacyReviewRequired";
            return fault;
        }

        /// <summary>FA-03：只有**在途/待对账**的结算行才是"未完成事项"。它们会真的挡住新交易
        /// （<c>FundedSettlementLedger.Prepare</c> 拒绝在存在此类行时新建），因此必须阻断；
        /// 而账户余额与专用储备是已归属的保留资产，不在此列。</summary>
        /// <summary>FA-03：只有**在途/待对账**的结算行才是"未完成事项"。它们会真的挡住新交易
        /// （<c>FundedSettlementLedger.Prepare</c> 拒绝在存在此类行时新建），因此必须阻断；
        /// 而账户余额与专用储备是已归属的保留资产，不在此列。判据在账本内直读（不复制条目数组）。</summary>
        private bool HasUnresolvedSettlementRows() => settlement.HasUnresolvedRows();

        /// <summary>
        /// FA-23：**显式、审计可追踪**的重新核验命令（fail-closed 保持不变）。
        /// 与"切换设置清故障"严格区分：这里先重算实时故障源，只要还有任何一个故障源存在就**拒绝**解除并如实返回；
        /// 全部消失才清 <c>State.Fault</c>（revision +1 = 持久审计痕迹，被解除的故障码一并落盘）。
        /// </summary>
        internal InstitutionReverifyCode ReVerifyInstitutionMode()
        {
            if (Scribe.mode != LoadSaveMode.Inactive) return InstitutionReverifyCode.Unavailable;
            settlement.BindToMainThread();
            if (!modeInitialized) ApplyInstitutionModeSetting(PersonalChronicleMod.Settings?.EnablePawnEconomicInstitution == true);
            if (institutionMode.State.Phase != InstitutionPhase.Faulted) return InstitutionReverifyCode.NotFaulted;
            var original = settlement.ReadLegacyArchive();
            var final = institutionMode.ReadCutover();
            if (final != null) settlement.CaptureCutover(final.Raw);
            var audit = settlement.ReadAudit();
            var plan = institutionMode.Frozen ? ReadCutoverPlan() : null;
            string live = ReadLiveFault(original, final, audit, plan);
            if (live != null) return InstitutionReverifyCode.StillFaulted;
            if (!institutionMode.ClearFaultAfterReverification(institutionMode.State.Fault)) return InstitutionReverifyCode.StillFaulted;
            ApplyInstitutionModeSetting(PersonalChronicleMod.Settings?.EnablePawnEconomicInstitution == true);
            return InstitutionReverifyCode.Cleared;
        }
    }
    /// <summary>FA-23：重新核验命令的结果（供界面如实显示"解除了 / 仍有故障 / 本就不在故障态"）。</summary>
    internal enum InstitutionReverifyCode { Cleared, NotFaulted, StillFaulted, Unavailable }
}
