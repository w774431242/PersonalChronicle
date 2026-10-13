using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Care;
using Verse;

namespace PersonalChronicle.Data.PawnEconomy
{
    /// <summary>Additive save node for M8 care authorizations and M7 service capability states. A save
    /// without this node has no authorization at all (no care may be charged) and every service stays on
    /// the vanilla public path.</summary>
    internal sealed class CareSaveData : IExposable
    {
        public sealed class AuthorizationRow : IExposable
        {
            public string AuthorizationId, Kind, BeneficiaryKey, GuardianKey, ExecutorKey, PayerKey;
            public long MaxAmountMinor, ChargedMinor, Revision;
            public int CreatedTick, ExpiresTick;
            public void ExposeData()
            {
                Scribe_Values.Look(ref AuthorizationId, "auth"); Scribe_Values.Look(ref Kind, "kind");
                Scribe_Values.Look(ref BeneficiaryKey, "beneficiary"); Scribe_Values.Look(ref GuardianKey, "guardian");
                Scribe_Values.Look(ref ExecutorKey, "executor"); Scribe_Values.Look(ref PayerKey, "payer");
                Scribe_Values.Look(ref MaxAmountMinor, "max"); Scribe_Values.Look(ref ChargedMinor, "charged");
                Scribe_Values.Look(ref Revision, "revision");
                Scribe_Values.Look(ref CreatedTick, "created"); Scribe_Values.Look(ref ExpiresTick, "expires");
            }
        }
        public sealed class CapabilityRow : IExposable
        {
            public string Kind, Capability;
            public long Revision;
            public List<string> Blockers = new List<string>();
            public void ExposeData()
            {
                Scribe_Values.Look(ref Kind, "kind"); Scribe_Values.Look(ref Capability, "capability");
                Scribe_Values.Look(ref Revision, "revision");
                Scribe_Collections.Look(ref Blockers, "blockers", LookMode.Value);
                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                {
                    if (Blockers == null) Blockers = new List<string>();
                    if (Blockers.Count > CareSaveData.MaximumBlockers)
                        Blockers.RemoveRange(CareSaveData.MaximumBlockers, Blockers.Count - CareSaveData.MaximumBlockers);
                }
            }
        }
        public const int MaximumAuthorizations = 4096, MaximumCapabilities = 16, MaximumBlockers = 16, MaximumCareEvidence = 4096;
        private List<AuthorizationRow> authorizations = new List<AuthorizationRow>();
        private List<string> examinationEvidence = new List<string>();
        private HashSet<string> evidenceIndex;
        private List<CapabilityRow> capabilities = new List<CapabilityRow>();
        private Dictionary<string, AuthorizationRow> authorizationIndex;
        private Dictionary<string, CapabilityRow> capabilityIndex;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref authorizations, "authorizations", LookMode.Deep);
            Scribe_Collections.Look(ref examinationEvidence, "examinationEvidence", LookMode.Value);
            Scribe_Collections.Look(ref capabilities, "capabilities", LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            if (authorizations == null) authorizations = new List<AuthorizationRow>();
            if (capabilities == null) capabilities = new List<CapabilityRow>();
            authorizationIndex = null; capabilityIndex = null;
            _ = AuthorizationIndex; _ = CapabilityIndex;
            if (examinationEvidence == null) examinationEvidence = new List<string>();
            evidenceIndex = null;
            if (authorizations.Count > MaximumAuthorizations)
                authorizations.RemoveRange(0, authorizations.Count - MaximumAuthorizations);
            if (capabilities.Count > MaximumCapabilities)
                capabilities.RemoveRange(0, capabilities.Count - MaximumCapabilities);
        }
        private Dictionary<string, AuthorizationRow> AuthorizationIndex
        {
            get
            {
                if (authorizationIndex != null) return authorizationIndex;
                authorizationIndex = new Dictionary<string, AuthorizationRow>(StringComparer.Ordinal);
                for (int i = 0; i < authorizations.Count; i++)
                {
                    var row = authorizations[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.AuthorizationId) || row.MaxAmountMinor < 0
                        || row.ChargedMinor < 0 || row.ChargedMinor > row.MaxAmountMinor) continue;
                    authorizationIndex[row.AuthorizationId] = row;
                }
                return authorizationIndex;
            }
        }
        private Dictionary<string, CapabilityRow> CapabilityIndex
        {
            get
            {
                if (capabilityIndex != null) return capabilityIndex;
                capabilityIndex = new Dictionary<string, CapabilityRow>(StringComparer.Ordinal);
                for (int i = 0; i < capabilities.Count; i++)
                {
                    var row = capabilities[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.Kind) || string.IsNullOrWhiteSpace(row.Capability)) continue;
                    capabilityIndex[row.Kind] = row;
                }
                return capabilityIndex;
            }
        }

        /// <summary>Care delivery evidence is the dedupe guard for charging later: it is never trimmed,
        /// so the set simply stops accepting new keys at its cap.</summary>
        public bool HasCareEvidence(string evidenceId) => evidenceId != null && EvidenceIndex.Contains(evidenceId);
        public bool CanRecordCareEvidence(string evidenceId)
            => !string.IsNullOrWhiteSpace(evidenceId)
                && (EvidenceIndex.Contains(evidenceId) || examinationEvidence.Count < MaximumCareEvidence);
        public bool MarkCareEvidence(string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(evidenceId)) return false;
            if (EvidenceIndex.Contains(evidenceId)) return true;
            if (examinationEvidence.Count >= MaximumCareEvidence) return false;
            examinationEvidence.Add(evidenceId); evidenceIndex.Add(evidenceId);
            return true;
        }
        private HashSet<string> EvidenceIndex
        {
            get
            {
                if (evidenceIndex != null) return evidenceIndex;
                evidenceIndex = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < examinationEvidence.Count; i++) if (!string.IsNullOrWhiteSpace(examinationEvidence[i])) evidenceIndex.Add(examinationEvidence[i]);
                return evidenceIndex;
            }
        }
        public int AuthorizationCount => AuthorizationIndex.Count;
        public bool CanWriteAuthorization => authorizations.Count < MaximumAuthorizations;
        public bool CanWriteCapability(ServiceKind kind) => CapabilityIndex.ContainsKey(kind.ToString()) || capabilities.Count < MaximumCapabilities;

        public CareAuthorization ReadAuthorization(string authorizationId)
        {
            if (authorizationId == null || !AuthorizationIndex.TryGetValue(authorizationId, out var row)) return null;
            if (!Enum.IsDefined(typeof(ServiceKind), row.Kind ?? "")) return null;
            try
            {
                return new CareAuthorization(row.AuthorizationId, (ServiceKind)Enum.Parse(typeof(ServiceKind), row.Kind),
                    row.BeneficiaryKey, row.GuardianKey, row.ExecutorKey, row.PayerKey, row.MaxAmountMinor,
                    row.CreatedTick, row.ExpiresTick, row.Revision < 1 ? 1 : row.Revision, row.ChargedMinor);
            }
            catch (ArgumentException) { return null; }
        }
        public IReadOnlyList<CareAuthorization> ReadForBeneficiary(string beneficiaryKey)
        {
            var rows = new List<CareAuthorization>();
            if (beneficiaryKey == null) return rows.AsReadOnly();
            foreach (var row in AuthorizationIndex.Values)
                if (string.Equals(row.BeneficiaryKey, beneficiaryKey, StringComparison.Ordinal))
                { var read = ReadAuthorization(row.AuthorizationId); if (read != null) rows.Add(read); }
            return rows.AsReadOnly();
        }
        public AuthorizationRow ReadRow(string authorizationId)
            => authorizationId != null && AuthorizationIndex.TryGetValue(authorizationId, out var row) ? row : null;
        public bool WriteAuthorization(CareAuthorization authorization)
        {
            if (authorization == null) return false;
            if (!AuthorizationIndex.TryGetValue(authorization.AuthorizationId, out var row))
            {
                if (authorizations.Count >= MaximumAuthorizations) return false;
                row = new AuthorizationRow { AuthorizationId = authorization.AuthorizationId };
                authorizations.Add(row); authorizationIndex[authorization.AuthorizationId] = row;
            }
            row.Kind = authorization.Kind.ToString(); row.BeneficiaryKey = authorization.BeneficiaryKey;
            row.GuardianKey = authorization.GuardianKey; row.ExecutorKey = authorization.ExecutorKey;
            row.PayerKey = authorization.PayerKey; row.MaxAmountMinor = authorization.MaxAmountMinor;
            row.ChargedMinor = authorization.ChargedMinor; row.Revision = authorization.Revision;
            row.CreatedTick = authorization.CreatedTick; row.ExpiresTick = authorization.ExpiresTick;
            return true;
        }
        public void RemoveAuthorization(string authorizationId)
        {
            if (authorizationId == null) return;
            var row = ReadRow(authorizationId);
            if (row == null) return;
            authorizations.Remove(row); AuthorizationIndex.Remove(authorizationId);
        }
        /// <summary>U6-A4：确定性的到期裁剪（由组件在 250 tick 调用）。读/写路径本已惰性判定过期
        /// （<c>CareAuthorization.IsExpired</c>：`now &gt; ExpiresTick`），这里只是把陈行真正清掉，
        /// 避免持久化表被历史行填满（4096 行上限下会挤压有效授权）。判据与 IsExpired **逐字一致**；
        /// 先收集 id 再删，返回删除行数，不抛异常、重复调用幂等。</summary>
        public int TrimExpired(int now)
        {
            var expired = new List<string>();
            foreach (var row in AuthorizationIndex.Values)
                if (row != null && !string.IsNullOrWhiteSpace(row.AuthorizationId) && now > row.ExpiresTick)
                    expired.Add(row.AuthorizationId);
            for (int i = 0; i < expired.Count; i++) RemoveAuthorization(expired[i]);
            return expired.Count;
        }
        public ServiceCapabilityState ReadCapability(ServiceKind kind)
        {
            if (!CapabilityIndex.TryGetValue(kind.ToString(), out var row)) return null;
            if (!Enum.IsDefined(typeof(ServiceCapability), row.Capability ?? "")) return null;
            return new ServiceCapabilityState(kind, (ServiceCapability)Enum.Parse(typeof(ServiceCapability), row.Capability),
                row.Revision, row.Blockers);
        }
        public bool WriteCapability(ServiceCapabilityState state)
        {
            if (state == null) return false;
            if (!CapabilityIndex.TryGetValue(state.Kind.ToString(), out var row))
            {
                if (capabilities.Count >= MaximumCapabilities) return false;
                row = new CapabilityRow { Kind = state.Kind.ToString() };
                capabilities.Add(row); capabilityIndex[state.Kind.ToString()] = row;
            }
            row.Capability = state.Capability.ToString(); row.Revision = state.Revision;
            row.Blockers = new List<string>(state.Blockers);
            if (row.Blockers.Count > MaximumBlockers)
                row.Blockers.RemoveRange(MaximumBlockers, row.Blockers.Count - MaximumBlockers);
            return true;
        }
    }
}
