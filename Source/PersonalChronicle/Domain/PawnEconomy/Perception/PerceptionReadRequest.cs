using System;
using System.Collections.Generic;

namespace PersonalChronicle.Domain.PawnEconomy.Perception
{
    internal enum PerceptionReadiness { Unknown = 0, Loading = 1, Ready = 2, Partial = 3, Stale = 4, OutOfScope = 5, Error = 6 }
    internal enum PerceptionPurpose { Display = 0, Planning = 1 }
    internal sealed class PerceptionReadRequest
    {
        public string PawnId { get; }
        public long SessionEpoch { get; }
        public int MapId { get; }
        public PerceptionPurpose Purpose { get; }
        public PerceptionGroups RequiredGroups { get; }
        private readonly int fastAge, bodyAge, relationshipAge;
        public PerceptionReadRequest(string pawnId, long epoch, int mapId, PerceptionPurpose purpose,
            PerceptionGroups required, int maxFastAge, int maxBodyAge, int maxRelationshipAge)
        {
            if (string.IsNullOrWhiteSpace(pawnId)) throw new ArgumentException("A pawn ID is required.", nameof(pawnId));
            if (epoch <= 0 || maxFastAge < 0 || maxBodyAge < 0 || maxRelationshipAge < 0)
                throw new ArgumentOutOfRangeException(nameof(epoch));
            if ((required & ~PerceptionGroups.All) != 0 || required == PerceptionGroups.None)
                throw new ArgumentOutOfRangeException(nameof(required));
            if (purpose != PerceptionPurpose.Display && purpose != PerceptionPurpose.Planning)
                throw new ArgumentOutOfRangeException(nameof(purpose));
            PawnId = pawnId; SessionEpoch = epoch; MapId = mapId; Purpose = purpose; RequiredGroups = required;
            fastAge = maxFastAge; bodyAge = maxBodyAge; relationshipAge = maxRelationshipAge;
        }
        public int MaximumAge(PerceptionGroups group) => group == PerceptionGroups.Body ? bodyAge
            : group == PerceptionGroups.Relationships ? relationshipAge : fastAge;
    }
    internal sealed class GroupReadStatus
    {
        public PerceptionGroups Group { get; }
        public CaptureState CaptureState { get; }
        public long AgeTicks { get; }
        public bool Required { get; }
        public PerceptionReadiness Readiness { get; }
        public string ReasonCode { get; }
        public GroupReadStatus(PerceptionGroups group, CaptureState state, long age, bool required,
            PerceptionReadiness readiness, string reason)
        { Group = group; CaptureState = state; AgeTicks = age; Required = required; Readiness = readiness; ReasonCode = reason; }
    }
    internal sealed class FactQueryResult
    {
        public PerceptionReadiness Readiness { get; }
        public string ReasonCode { get; }
        public bool RequiredGroupsUsable { get; }
        public PawnFactSnapshot Snapshot { get; }
        public IReadOnlyList<GroupReadStatus> Groups { get; }
        public FactQueryResult(PerceptionReadiness readiness, string reason, bool usable,
            PawnFactSnapshot snapshot = null, IEnumerable<GroupReadStatus> groups = null)
        { Readiness = readiness; ReasonCode = reason; RequiredGroupsUsable = usable; Snapshot = snapshot; Groups = PerceptionCollections.Freeze(groups); }
    }
    internal sealed class ObservationQueryResult
    {
        public FactQueryResult Facts { get; }
        public IReadOnlyList<CurrentNeedGap> Gaps { get; }
        public ObservationQueryResult(FactQueryResult facts, IEnumerable<CurrentNeedGap> gaps)
        { Facts = facts; Gaps = PerceptionCollections.Freeze(gaps); }
    }
}
