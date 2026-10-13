using System;
using System.Linq;
using System.Threading;
using System.Text;
using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>Persistent sidecar only. No gameplay entry point may execute it until B2.4 authorizes
    /// and exclusively routes the institution. Internal commands are exercised by offline fixtures.</summary>
    internal sealed class PawnSettlementSaveHost : IExposable
    {
        private int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private SettlementReservePrototype reserve = new SettlementReservePrototype();
        private FundedSettlementLedger ledger = new FundedSettlementLedger();
        private string metadata, fault, rawLegacy, archiveSource, loadedArchiveHash;
        private const string TextEncoding = "base64-utf8-v1";
        private string metadataWire, metadataEncoding, legacyWire, legacyEncoding;
        private bool preserveWire;
        private bool preserveRaw, legacyCaptured, busy, recovered = true;
        private LegacySettlementArchive archive;
        private LegacySettlementArchive cutoverArchive;
        private string cutoverWire, cutoverEncoding, cutoverHash;
        public bool CanExecuteInGame => false;
        public long Version { get { AssertThread(); Recover(); return ledger.Version; } }
        public long ReserveVersion { get { AssertThread(); return reserve.ReadAudit().Version; } }
        private void AssertThread() { if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("WrongSettlementHostThread"); }
        private void BindAtHostBoundary()
        {
            if (busy) throw new InvalidOperationException("CannotRebindActiveSettlement");
            reserve.BindOwnerAtHostBoundary(); ledger.BindOwnerAtHostBoundary(); ownerThread = Thread.CurrentThread.ManagedThreadId;
        }
        internal void BindToMainThread()
        {
            if (!UnityData.IsInMainThread || Scribe.mode != LoadSaveMode.Inactive) return;
            if (Thread.CurrentThread.ManagedThreadId != ownerThread) BindAtHostBoundary();
            Recover();
        }
        public void CaptureLegacy(string originalPayload, string source)
        {
            if (Scribe.mode != LoadSaveMode.Inactive && Thread.CurrentThread.ManagedThreadId != ownerThread) BindAtHostBoundary();
            AssertThread(); if (legacyCaptured || preserveRaw) return;
            legacyCaptured = true; rawLegacy = originalPayload; archiveSource = source;
            archive = new LegacySettlementArchive(rawLegacy, archiveSource); loadedArchiveHash = archive.Hash;
        }
        public LegacySettlementArchive ReadLegacyArchive()
        { AssertThread(); return legacyCaptured && !preserveWire ? archive ?? (archive = new LegacySettlementArchive(rawLegacy, archiveSource)) : null; }
        internal LegacySettlementArchive ReadCutoverArchive() { AssertThread(); return cutoverArchive; }
        internal void CaptureCutover(string raw)
        {
            AssertThread(); if (preserveRaw || preserveWire || cutoverArchive != null) return;
            cutoverArchive = new LegacySettlementArchive(raw, "AuthorizedFinalCutover"); cutoverHash = cutoverArchive.Hash;
        }
        internal System.Collections.Generic.IReadOnlyList<SettlementEntry> ReadEntries() { AssertThread(); Recover(); return ledger.ReadEntries(); }
        internal ReserveAuditSnapshot ReadReserveAudit() { AssertThread(); return reserve.ReadAudit(); }
        internal ReserveMovementEvidence ReadPhysicalEvidence(string commandId) { AssertThread(); return reserve.ReadMovement(commandId); }
        public SettlementEntry ReadCommand(string id) { AssertThread(); Recover(); return preserveRaw ? null : ledger.Read(id); }
        public FinancialAccountSnapshot ReadAccount(string id) { AssertThread(); Recover(); return preserveRaw ? null : ledger.ReadAccount(id); }
        /// <summary>
        /// CB-ECON-002 FA-04：账本账户读取可用性。只读核验实物储备与负债，
        /// 不复制账户数组；复杂度随有界储备栈和账本规模变化，只用于 60 tick 门面的采样，不能每帧调用。
        /// <para>存在的意义：<see cref="ReadAccount"/> 返回 <c>null</c> 时**语义不唯一**（"没有这个账户"与
        /// "账本不可判定"都是 null）。界面若把两者合并，就会在账本受损时谎报"尚未建立账户"。</para>
        /// <para><b>恢复先行边界（LR-03，经理复核 §10.2）</b>：本方法<b>不调用</b> <c>Recover()</c> ——
        /// Recover 在 <c>recovered == false</c> 时会做校验并可能推进 Cancel / CompletePhysical /
        /// RequireReconciliation（<b>写状态</b>）。恢复必须由宿主生命周期点（<see cref="BindToMainThread"/> /
        /// GameComponentTick）先行驱动；展示查询只<b>读</b>结果：未恢复（含存读档中与恢复忙）一律返回
        /// <see cref="SettlementLedgerState.Unavailable"/>。
        /// 因此本方法"只读且无副作用"对<b>所有</b>状态成立，而不只是已恢复的稳态。</para>
        /// 查询路径**只读**：不开户、不迁移、不补齐资金、不解除预留。
        /// </summary>
        internal SettlementLedgerState ReadLedgerState()
        {
            AssertThread();
            if (Scribe.mode != LoadSaveMode.Inactive || !recovered || busy) return SettlementLedgerState.Unavailable;
            if (preserveRaw) return SettlementLedgerState.Unsupported;
            if (fault != null) return SettlementLedgerState.Faulted;
            var actual = reserve.ReadAudit();
            if (actual.Fault != null || !actual.ActualSilver.HasValue || !actual.RecordedSilver.HasValue
                || MinorCurrency.FromSilver(actual.RecordedSilver.Value) != ledger.TotalMinor)
                return SettlementLedgerState.Faulted;
            return SettlementLedgerState.Ready;
        }
        /// <summary>展示专用纯读取；恢复、开户和取消预留必须在明确的宿主生命周期或命令路径进行。
        /// 非健康状态不返回旧账户或伪造零余额。</summary>
        internal FinancialAccountSnapshot ReadAccountForDisplay(string id)
        {
            AssertThread();
            return ReadLedgerState() == SettlementLedgerState.Ready ? ledger.ReadAccount(id) : null;
        }
        /// <summary>FA-03：制度观察用的"是否还有未完成结算行"读数（不复制条目数组；preserveRaw 时按不可判定处理）。</summary>
        internal bool HasUnresolvedRows() { AssertThread(); Recover(); return !preserveRaw && ledger.HasUnresolvedRows(); }
        /// <summary>FA-01：明确注册边界（设计 §4 EnrollPawn）——给合法新主体建立**零余额**账户。
        /// 只在账本健康时登记（preserveRaw / AuditCore().Fault 一律拒绝）；查询路径永不调用它。</summary>
        internal bool EnrollAccount(string id)
        {
            AssertThread();
            if (Scribe.mode != LoadSaveMode.Inactive || preserveRaw) return false;
            Recover();
            if (AuditCore().Fault != null) return false;
            return ledger.EnrollAccount(id);
        }
        private string ValidateClaims()
        {
            if (cutoverArchive != null)
            {
                var cutoverOriginal = ReadLegacyArchive();
                if (cutoverOriginal == null || cutoverOriginal.Fault != null || cutoverArchive.Fault != null || cutoverArchive.Hash != cutoverHash) return "CutoverArchiveUnavailable";
                return LegacyCutoverPlan.Build(cutoverOriginal.Hash, cutoverOriginal.Report, cutoverOriginal.Claims, cutoverArchive.Hash, cutoverArchive.Report,
                    cutoverArchive.Claims, ledger.ReadEntries(), null, null).Fault;
            }
            var allocations = ledger.ReadEntries().Where(x => x.Intent.Kind == SettlementKind.LegacyAllocation && x.Phase != SettlementPhase.Cancelled).ToArray();
            if (allocations.Length == 0) return null;
            var original = ReadLegacyArchive();
            if (original == null || original.Fault != null) return "LegacyArchiveUnavailable";
            foreach (var group in allocations.GroupBy(x => x.Intent.ClaimId, StringComparer.Ordinal))
            {
                var claim = original.Claims.SingleOrDefault(x => x.Id == group.Key);
                if (claim == null || group.Sum(x => x.Intent.AmountMinor) > claim.AmountMinor
                    || group.Select(x => x.Intent.TargetAccount).Distinct(StringComparer.Ordinal).Count() != 1) return "InvalidLegacyAllocation";
            }
            return null;
        }
        public void Recover()
        {
            AssertThread(); if (recovered || busy || Scribe.mode != LoadSaveMode.Inactive) return;
            recovered = true; if (preserveRaw) return;
            if (loadedArchiveHash != (legacyCaptured ? ReadLegacyArchive().Hash : null)) { fault = "LegacyArchiveHashMismatch"; return; }
            string claimsFault = ValidateClaims(); if (claimsFault != null) { fault = claimsFault; return; }
            // Validate the complete binding BEFORE consuming any unfinished proof.
            foreach (var proof in reserve.ReadMovements())
            {
                var row = ledger.Read(proof.Intent.CommandId);
                if (row == null || row.Intent.Movement == null || !row.Intent.Movement.SameAs(proof.Intent)
                    || row.Phase == SettlementPhase.Cancelled
                    || row.Phase == SettlementPhase.Finalized && proof.Phase != ReserveMovementPhase.Completed)
                { fault = "OrphanOrConflictingReserveProof"; return; }
            }
            foreach (var row in ledger.ReadEntries())
            {
                if (row.Intent.Movement != null && row.Phase == SettlementPhase.Finalized)
                {
                    var proof = reserve.ReadMovement(row.Intent.CommandId);
                    if (proof == null || !proof.HasExactQuantityProof || proof.CompletedMinor != row.Intent.AmountMinor)
                    { fault = "FinalizedWithoutPhysicalProof"; return; }
                }
            }
            foreach (var row in ledger.ReadEntries().Where(x => x.Phase == SettlementPhase.Prepared))
            {
                // Goods may already be in the saved buyer inventory. Storage's persisted delivery
                // proof must finish or return those goods; cancelling here would hand them out free.
                if (row.Intent.Kind == SettlementKind.Purchase) continue;
                var proof = row.Intent.Movement == null ? null : reserve.ReadMovement(row.Intent.CommandId);
                if (proof == null) ledger.Cancel(row.Intent.CommandId, "RecoveryWithoutPhysicalMovement");
                else if (proof.Phase == ReserveMovementPhase.Completed) ledger.CompletePhysical(row.Intent.CommandId, proof);
                else ledger.RequireReconciliation(row.Intent.CommandId, "UncertainPhysicalMovement");
            }
            if (ledger.ReadEntries().Any(x => x.Phase == SettlementPhase.ReconciliationRequired)) fault = fault ?? "SettlementReconciliationRequired";
            var check = AuditCore(); if (check.Fault != null) fault = fault ?? check.Fault;
        }
        private SettlementAudit AuditCore()
        {
            var actual = reserve.ReadAudit(); long? minor = actual.ActualSilver.HasValue ? MinorCurrency.FromSilver(actual.ActualSilver.Value) : (long?)null;
            string problem = fault ?? (preserveRaw ? "UnknownSettlementState" : !recovered ? "RecoveryPending" : busy ? "Busy" : actual.Fault);
            if (problem == null && MinorCurrency.FromSilver(actual.RecordedSilver.Value) != ledger.TotalMinor) problem = "ReserveLedgerMismatch";
            return new SettlementAudit(ledger.Version, preserveRaw ? (long?)null : ledger.TotalMinor, minor, problem,
                preserveRaw ? Array.Empty<FinancialAccountSnapshot>() : ledger.ReadAccounts());
        }
        public SettlementAudit ReadAudit() { AssertThread(); Recover(); return AuditCore(); }
        private SettlementResult Existing(SettlementIntent intent)
        {
            var previous = ledger.Read(intent.CommandId);
            return previous == null ? null : new SettlementResult(previous.Intent.SameAs(intent) ? SettlementCode.Replay : SettlementCode.CommandConflict, previous);
        }
        public SettlementResult ExecutePhysical(SettlementIntent intent, ThingOwner counterparty) => ExecutePhysicalCore(intent, counterparty, false);
        internal SettlementResult PreparePhysical(SettlementIntent intent)
        {
            AssertThread(); if (intent == null || intent.Movement == null) throw new ArgumentException("PhysicalIntentRequired");
            if (busy || Scribe.mode != LoadSaveMode.Inactive) return new SettlementResult(SettlementCode.NotReady);
            Recover(); if (preserveRaw || AuditCore().Fault != null) return new SettlementResult(SettlementCode.NotReady);
            var previous = Existing(intent); if (previous != null) return previous;
            if (intent.Movement.ExpectedVersion != reserve.ReadAudit().Version) return new SettlementResult(SettlementCode.StaleVersion);
            return ledger.Prepare(intent);
        }
        internal SettlementResult DeliverPreparedPhysical(SettlementIntent intent, ThingOwner counterparty) => ExecutePhysicalCore(intent, counterparty, true);
        internal SettlementResult CancelUnmovedPhysical(string commandId)
        {
            AssertThread(); if (busy || Scribe.mode != LoadSaveMode.Inactive || preserveRaw) return new SettlementResult(SettlementCode.NotReady);
            Recover(); var row = ledger.Read(commandId);
            if (row?.Intent.Movement == null || reserve.ReadMovement(commandId) != null) return new SettlementResult(SettlementCode.NotReady, row);
            return ledger.Cancel(commandId, "DeliveryCancelledWithoutPhysicalMovement");
        }
        private SettlementResult ExecutePhysicalCore(SettlementIntent intent, ThingOwner counterparty, bool consumePrepared)
        {
            AssertThread(); if (intent == null || intent.Movement == null) throw new ArgumentException("PhysicalIntentRequired");
            if (busy || Scribe.mode != LoadSaveMode.Inactive) return new SettlementResult(SettlementCode.NotReady);
            Recover(); if (preserveRaw) return new SettlementResult(SettlementCode.NotReady);
            var previous = Existing(intent);
            if (consumePrepared && previous == null) return new SettlementResult(SettlementCode.NotReady);
            bool resume = consumePrepared && previous?.Code == SettlementCode.Replay && previous.Entry.Phase == SettlementPhase.Prepared;
            if (previous != null && !resume) return previous;
            var audit = AuditCore(); if (audit.Fault != null) { fault = fault ?? audit.Fault; return new SettlementResult(SettlementCode.NotReady); }
            if (intent.Movement.ExpectedVersion != reserve.ReadAudit().Version) return new SettlementResult(SettlementCode.StaleVersion);
            var prepared = resume ? new SettlementResult(SettlementCode.Prepared, previous.Entry) : ledger.Prepare(intent);
            if (prepared.Code != SettlementCode.Prepared) return prepared;
            busy = true;
            try
            {
                var movement = reserve.Execute(intent.Movement, counterparty);
                if (movement.Evidence?.Phase == ReserveMovementPhase.Completed) return ledger.CompletePhysical(intent.CommandId, movement.Evidence);
                if (movement.Evidence == null && movement.Code != ReserveResultCode.ReconciliationRequired)
                    return ledger.Cancel(intent.CommandId, "PhysicalPreflight:" + movement.Code);
                fault = "SettlementReconciliationRequired";
                return ledger.RequireReconciliation(intent.CommandId, "PhysicalMovement:" + movement.Code);
            }
            catch (Exception error) when (!(error is OutOfMemoryException) && !(error is StackOverflowException))
            {
                fault = "SettlementException:" + error.GetType().FullName;
                return ledger.RequireReconciliation(intent.CommandId, fault);
            }
            finally { busy = false; }
        }
        /// <summary>M7 purchase: prepares the buyer's debit before the goods move, so a missing item can
        /// never leave a partially charged order; the caller completes or cancels it in the same step.</summary>
        internal SettlementResult PreparePurchase(SettlementIntent intent)
        {
            AssertThread(); if (intent == null || intent.Kind != SettlementKind.Purchase) throw new ArgumentException("PurchaseIntentRequired");
            if (busy || Scribe.mode != LoadSaveMode.Inactive) return new SettlementResult(SettlementCode.NotReady);
            Recover(); if (preserveRaw) return new SettlementResult(SettlementCode.NotReady);
            var previous = Existing(intent); if (previous != null) return previous;
            var audit = AuditCore(); if (audit.Fault != null) { fault = fault ?? audit.Fault; return new SettlementResult(SettlementCode.NotReady); }
            return ledger.Prepare(intent);
        }
        public SettlementResult CompletePurchase(string id)
        {
            AssertThread(); if (busy || Scribe.mode != LoadSaveMode.Inactive || preserveRaw) return new SettlementResult(SettlementCode.NotReady);
            Recover(); return ledger.CompletePurchase(id);
        }
        public SettlementResult CancelPurchase(string id, string reason)
        {
            AssertThread(); if (busy || Scribe.mode != LoadSaveMode.Inactive || preserveRaw) return new SettlementResult(SettlementCode.NotReady);
            Recover(); return ledger.Cancel(id, reason);
        }
        /// <summary>M6 wage transfer: one short synchronous commit from the treasury to one worker
        /// account. The obligation id is the command id, so a repeated attempt replays instead of paying
        /// twice; a failed funding check leaves the payable outstanding (never a silent shortfall).</summary>
        public SettlementResult PayWage(SettlementIntent intent)
        {
            AssertThread(); if (intent == null || intent.Kind != SettlementKind.WageTransfer) throw new ArgumentException("WageIntentRequired");
            if (busy || Scribe.mode != LoadSaveMode.Inactive) return new SettlementResult(SettlementCode.NotReady);
            Recover(); if (preserveRaw) return new SettlementResult(SettlementCode.NotReady);
            var previous = Existing(intent); if (previous != null) return previous;
            var audit = AuditCore(); if (audit.Fault != null) { fault = fault ?? audit.Fault; return new SettlementResult(SettlementCode.NotReady); }
            var prepared = ledger.Prepare(intent);
            return prepared.Code == SettlementCode.Prepared ? ledger.CompleteTransfer(intent.CommandId) : prepared;
        }
        internal SettlementResult TransferPublic(SettlementIntent intent)
        {
            AssertThread(); if (intent?.Kind != SettlementKind.PublicTransfer) throw new ArgumentException("PublicTransferIntentRequired");
            if (busy || Scribe.mode != LoadSaveMode.Inactive) return new SettlementResult(SettlementCode.NotReady);
            Recover(); if (preserveRaw) return new SettlementResult(SettlementCode.NotReady);
            var previous = Existing(intent); if (previous != null) return previous;
            var audit = AuditCore(); if (audit.Fault != null) return new SettlementResult(SettlementCode.NotReady);
            var prepared = ledger.Prepare(intent);
            return prepared.Code == SettlementCode.Prepared ? ledger.CompleteTransfer(intent.CommandId) : prepared;
        }
        public SettlementResult AllocateLegacy(SettlementIntent intent)
        {
            AssertThread(); if (intent == null || intent.Kind != SettlementKind.LegacyAllocation) throw new ArgumentException("AllocationIntentRequired");
            if (busy || Scribe.mode != LoadSaveMode.Inactive) return new SettlementResult(SettlementCode.NotReady);
            Recover(); if (preserveRaw) return new SettlementResult(SettlementCode.NotReady);
            if (cutoverArchive != null) return new SettlementResult(SettlementCode.InvalidClaim);
            var previous = Existing(intent); if (previous != null) return previous;
            var audit = AuditCore(); if (audit.Fault != null) { fault = fault ?? audit.Fault; return new SettlementResult(SettlementCode.NotReady); }
            var original = ReadLegacyArchive(); var claim = original?.Fault == null ? original?.Claims.SingleOrDefault(x => x.Id == intent.ClaimId) : null;
            var prior = ledger.ReadEntries().Where(x => x.Intent.ClaimId == intent.ClaimId && x.Phase != SettlementPhase.Cancelled).ToArray();
            if (claim == null || prior.Any(x => x.Intent.TargetAccount != intent.TargetAccount)
                || intent.AmountMinor > claim.AmountMinor - prior.Sum(x => x.Intent.AmountMinor)) return new SettlementResult(SettlementCode.InvalidClaim);
            var prepared = ledger.Prepare(intent); return prepared.Code == SettlementCode.Prepared ? ledger.CompleteAllocation(intent.CommandId) : prepared;
        }
        internal SettlementResult AllocateCutover(SettlementIntent intent)
        {
            AssertThread(); if (intent == null || intent.Kind != SettlementKind.LegacyAllocation) throw new ArgumentException("AllocationIntentRequired");
            if (busy || Scribe.mode != LoadSaveMode.Inactive) return new SettlementResult(SettlementCode.NotReady);
            Recover(); if (preserveRaw || cutoverArchive == null || AuditCore().Fault != null) return new SettlementResult(SettlementCode.NotReady);
            var previous = Existing(intent); if (previous != null) return previous;
            var original = ReadLegacyArchive();
            if (original == null || original.Fault != null || cutoverArchive.Fault != null) return new SettlementResult(SettlementCode.InvalidClaim);
            var plan = LegacyCutoverPlan.Build(original.Hash, original.Report, original.Claims, cutoverArchive.Hash, cutoverArchive.Report,
                cutoverArchive.Claims, ledger.ReadEntries(), null, null);
            var claim = plan.Cash.SingleOrDefault(x => x.Claim.Id == intent.ClaimId);
            if (plan.Fault != null || claim == null || claim.Claim.LegacyParty != intent.TargetAccount || intent.AmountMinor > claim.Remaining)
                return new SettlementResult(SettlementCode.InvalidClaim);
            var prepared = ledger.Prepare(intent); return prepared.Code == SettlementCode.Prepared ? ledger.CompleteAllocation(intent.CommandId) : prepared;
        }
        public void ExposeData()
        {
            // Game loading/saving can run on a long-event worker. These are serialized lifecycle
            // boundaries, not permission for arbitrary command callers to change thread ownership.
            if (Scribe.mode != LoadSaveMode.Inactive && Thread.CurrentThread.ManagedThreadId != ownerThread) BindAtHostBoundary();
            AssertThread(); if (busy) throw new InvalidOperationException("CannotSaveDuringSettlement");
            if (Scribe.mode == LoadSaveMode.Saving && !preserveRaw)
                metadata = SettlementLedgerCodec.Encode(ledger, fault, legacyCaptured ? ReadLegacyArchive().Hash : null);
            if (Scribe.mode == LoadSaveMode.Saving && !preserveWire)
            {
                metadataWire = EncodeText(metadata); legacyWire = EncodeText(rawLegacy);
                metadataEncoding = legacyEncoding = TextEncoding;
            }
            Scribe_Values.Look(ref metadataWire, "metadata");
            Scribe_Values.Look(ref metadataEncoding, "metadataEncoding");
            Scribe_Values.Look(ref legacyCaptured, "legacyCaptured");
            Scribe_Values.Look(ref legacyWire, "legacyOriginalPayload");
            Scribe_Values.Look(ref legacyEncoding, "legacyOriginalEncoding");
            Scribe_Values.Look(ref archiveSource, "legacyArchiveSource");
            if (Scribe.mode == LoadSaveMode.Saving && !preserveWire && cutoverArchive != null)
            { cutoverWire = EncodeText(cutoverArchive.Raw); cutoverEncoding = TextEncoding; cutoverHash = cutoverArchive.Hash; }
            Scribe_Values.Look(ref cutoverWire, "legacyCutoverPayload"); Scribe_Values.Look(ref cutoverEncoding, "legacyCutoverEncoding");
            Scribe_Values.Look(ref cutoverHash, "legacyCutoverHash");
            Scribe_Deep.Look(ref reserve, "reserve");
            if (reserve == null) reserve = new SettlementReservePrototype();
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                try
                {
                    metadata = DecodeText(metadataWire, metadataEncoding); rawLegacy = DecodeText(legacyWire, legacyEncoding);
                    cutoverArchive = cutoverHash == null && cutoverWire == null && cutoverEncoding == null ? null
                        : new LegacySettlementArchive(DecodeText(cutoverWire, cutoverEncoding), "AuthorizedFinalCutover");
                    if (cutoverArchive != null && cutoverArchive.Hash != cutoverHash) throw new FormatException("CutoverHashMismatch");
                }
                catch (Exception error) when (error is FormatException || error is DecoderFallbackException)
                {
                    preserveWire = preserveRaw = true; fault = "InvalidOrUnsupportedSettlementTextEncoding";
                    recovered = false; archive = null; return;
                }
                var loaded = SettlementLedgerCodec.Decode(metadata); ledger = loaded.Ledger; fault = loaded.Fault;
                preserveRaw = loaded.PreserveRaw; loadedArchiveHash = loaded.ArchiveHash; recovered = false; archive = null;
            }
            // Deep children may receive PostLoadInit after this host. Recovery waits until Scribe is inactive.
        }
        private static string EncodeText(string value) => value == null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        private static string DecodeText(string wire, string encoding)
        {
            if (encoding == null) return wire; // Missing old sidecar/text encoding.
            if (encoding != TextEncoding) throw new FormatException("UnsupportedTextEncoding");
            return wire == null ? null : new UTF8Encoding(false, true).GetString(Convert.FromBase64String(wire));
        }
    }
}
