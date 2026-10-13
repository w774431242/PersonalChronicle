using System;
using System.Collections.Generic;
using System.Linq;

namespace PersonalChronicle.Domain.PawnEconomy.Governance
{
    public enum InstitutionPhase { Off, Preparing, Active, Draining, Faulted }
    internal sealed class InstitutionModeState
    {
        public InstitutionPhase Phase { get; }
        public bool Desired { get; }
        public long Revision { get; }
        public string Fault { get; }
        public InstitutionModeState(InstitutionPhase phase = InstitutionPhase.Off, bool desired = false, long revision = 0, string fault = null)
        {
            if (!Enum.IsDefined(typeof(InstitutionPhase), phase) || revision < 0 || phase == InstitutionPhase.Active && !desired
                || phase == InstitutionPhase.Faulted && string.IsNullOrEmpty(fault)
                || phase != InstitutionPhase.Faulted && fault != null) throw new ArgumentException("InvalidInstitutionMode");
            Phase = phase; Desired = desired; Revision = revision; Fault = fault;
        }
    }
    internal sealed class InstitutionReadiness
    {
        public bool FrozenLegacy { get; }
        public bool LegacyRightsResolved { get; }
        public bool BackingVerified { get; }
        public bool DeliveryVerified { get; }
        public bool AuthorityVerified { get; }
        public bool SaveRecoveryVerified { get; }
        public bool ExclusiveRoutesVerified { get; }
        public string Fault { get; }
        public InstitutionReadiness(bool frozenLegacy, bool legacyRightsResolved, bool backingVerified, bool deliveryVerified,
            bool authorityVerified, bool saveRecoveryVerified, bool exclusiveRoutesVerified, string fault = null)
        {
            FrozenLegacy = frozenLegacy; LegacyRightsResolved = legacyRightsResolved; BackingVerified = backingVerified;
            DeliveryVerified = deliveryVerified; AuthorityVerified = authorityVerified; SaveRecoveryVerified = saveRecoveryVerified;
            ExclusiveRoutesVerified = exclusiveRoutesVerified; Fault = fault;
        }
        public IReadOnlyList<string> Missing()
        {
            var rows = new List<string>();
            if (!FrozenLegacy) rows.Add("LegacyFreeze");
            if (!LegacyRightsResolved) rows.Add("LegacyRights");
            if (!BackingVerified) rows.Add("Backing");
            if (!DeliveryVerified) rows.Add("Delivery");
            if (!AuthorityVerified) rows.Add("Authority");
            if (!SaveRecoveryVerified) rows.Add("SaveAcceptance");
            if (!ExclusiveRoutesVerified) rows.Add("Routing");
            return rows.AsReadOnly();
        }
        public bool AllReady => FrozenLegacy && LegacyRightsResolved && BackingVerified && DeliveryVerified && AuthorityVerified
            && SaveRecoveryVerified && ExclusiveRoutesVerified && Fault == null;
    }
    public sealed class InstitutionModeSnapshot
    {
        public InstitutionPhase Phase { get; }
        public bool Desired { get; }
        public long Revision { get; }
        public string Fault { get; }
        public bool LegacyFeesAllowed => false;
        public bool NewCommitmentsAllowed { get; }
        public bool HasOutstandingRights { get; }
        public bool CutoverFrozen { get; }
        public IReadOnlyList<string> Blockers { get; }
        internal InstitutionModeSnapshot(InstitutionModeState state, InstitutionReadiness readiness, bool outstanding)
        {
            Phase = state.Phase; Desired = state.Desired; Revision = state.Revision; Fault = state.Fault ?? readiness.Fault;
            NewCommitmentsAllowed = state.Phase == InstitutionPhase.Active && state.Desired && readiness.AllReady;
            HasOutstandingRights = outstanding; CutoverFrozen = readiness.FrozenLegacy;
            Blockers = Array.AsReadOnly(readiness.Missing().ToArray());
        }
    }
}
