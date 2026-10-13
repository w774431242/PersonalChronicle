using System;
using System.Linq;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Application.PawnEconomy
{
    internal sealed class EconomyAccountSnapshot
    {
        public string AccountId { get; }
        public long Balance { get; }
        public long Reserved { get; }
        public long Available { get; }
        public int LedgerSchema { get; }
        // These are legacy accounting observations, never evidence of verified physical backing.
        public bool BackingVerified => false;
        public EconomyAccountSnapshot(EconomyAccount account, int schema)
        { AccountId = account.Key; Balance = account.Balance; Reserved = account.Reserved; Available = checked(Balance - Reserved); LedgerSchema = schema; }
    }
    internal sealed class EconomyPersonSnapshot
    {
        public string PawnId { get; }
        public bool Autonomy { get; }
        public EconomicGoalStatus Goal { get; }
        public string AcquiredAssetId { get; }
        public EconomyPersonSnapshot(EconomyPerson person)
        { PawnId = person.PawnKey; Autonomy = person.Autonomy; Goal = person.Goal; AcquiredAssetId = person.AcquiredAsset; }
    }
    internal static class EconomyReadQueries
    {
        public static Fact<EconomyAccountSnapshot> ReadAccount(EconomyState state, string id)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An account ID is required.", nameof(id));
            if (!state.Ready) return Fact<EconomyAccountSnapshot>.Error(state.Fault ?? "UnsupportedSchema");
            var account = state.All<EconomyAccount>().FirstOrDefault(a => a.Key == id);
            if (account == null) return Fact<EconomyAccountSnapshot>.Unknown("NotEnrolled");
            if (account.Balance < 0 || account.Reserved < 0 || account.Reserved > account.Balance)
                return Fact<EconomyAccountSnapshot>.Error("InvalidLedger");
            return Fact<EconomyAccountSnapshot>.Known(new EconomyAccountSnapshot(account, state.Schema));
        }
        public static Fact<EconomyPersonSnapshot> ReadPerson(EconomyState state, string id)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A pawn ID is required.", nameof(id));
            if (!state.Ready) return Fact<EconomyPersonSnapshot>.Error(state.Fault ?? "UnsupportedSchema");
            var person = state.All<EconomyPerson>().FirstOrDefault(p => p.PawnKey == id);
            return person == null ? Fact<EconomyPersonSnapshot>.Unknown("NotEnrolled")
                : Fact<EconomyPersonSnapshot>.Known(new EconomyPersonSnapshot(person));
        }
    }
}
