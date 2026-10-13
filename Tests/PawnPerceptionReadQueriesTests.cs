using System.Linq;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Data;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class PawnPerceptionReadQueriesTests
    {
        [Test] public void MissingRecordsStayUnknownAndDoNotEnrollOrMint()
        {
            var state = new EconomyState(); string before = PawnEconomySaveData.Encode(state);
            for (int i = 0; i < 100; i++)
            {
                Assert.That(EconomyReadQueries.ReadAccount(state, "p").ReasonCode, Is.EqualTo("NotEnrolled"));
                Assert.That(EconomyReadQueries.ReadPerson(state, "p").State, Is.EqualTo(FactState.Unknown));
            }
            Assert.That(state.Records, Is.Empty); Assert.That(PawnEconomySaveData.Encode(state), Is.EqualTo(before));
        }
        [Test] public void ReturnedBalancesAndPeopleAreImmutableCopiesOfLegacyRecords()
        {
            var state = new EconomyState(); var account = state.Add(new EconomyAccount { Key = "p", Balance = 1000, Reserved = 300 });
            var person = state.Add(new EconomyPerson { PawnKey = "p", AcquiredAsset = "a", Autonomy = true });
            Assert.That(EconomyReadQueries.ReadAccount(state, "p").TryGetValue(out var snapshot), Is.True);
            Assert.That(EconomyReadQueries.ReadPerson(state, "p").TryGetValue(out var personSnapshot), Is.True);
            account.Balance = 0; person.Autonomy = false;
            Assert.That(snapshot.Available, Is.EqualTo(700)); Assert.That(snapshot.BackingVerified, Is.False);
            Assert.That(personSnapshot.Autonomy, Is.True);
        }
        [Test] public void FaultedOrFutureLedgerIsNotInterpretedAsZeroCash()
        {
            var state = new EconomyState { Schema = 999 };
            Assert.That(EconomyReadQueries.ReadAccount(state, "p").State, Is.EqualTo(FactState.Error));
            Assert.That(EconomyReadQueries.ReadPerson(state, "p").State, Is.EqualTo(FactState.Error));
            Assert.That(state.Records, Is.Empty);
        }
        [TestCase(-1L, 0L)] [TestCase(10L, -1L)] [TestCase(10L, 11L)]
        public void InvalidHeldMoneyCannotBecomeSpendableCash(long balance, long reserved)
        {
            var state = new EconomyState(); state.Add(new EconomyAccount { Key = "p", Balance = balance, Reserved = reserved });
            Assert.That(EconomyReadQueries.ReadAccount(state, "p").ReasonCode, Is.EqualTo("InvalidLedger"));
            Assert.That(state.All<EconomyAccount>().Single().Balance, Is.EqualTo(balance));
        }
    }
}
