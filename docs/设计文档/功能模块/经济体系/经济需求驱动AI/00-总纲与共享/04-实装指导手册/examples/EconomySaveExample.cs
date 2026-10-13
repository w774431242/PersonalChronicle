// Documentation-only specimen. Do not include this file in the production mod.
using System.Collections.Generic;
using Verse;

namespace PawnEconomy.HandbookExamples
{
    public sealed class AccountRecordExample : IExposable
    {
        public string AccountId;
        public Pawn Pawn;
        public long BalanceMinor;
        public long ReservedMinor;

        public void ExposeData()
        {
            Scribe_Values.Look(ref AccountId, "accountId");
            Scribe_References.Look(ref Pawn, "pawn");
            Scribe_Values.Look(ref BalanceMinor, "balanceMinor", 0L);
            Scribe_Values.Look(ref ReservedMinor, "reservedMinor", 0L);
        }
    }

    // GameComponent's base constructor is parameterless in the verified target.
    // Game.FillComponents creates its subclass with a Game constructor argument.
    public sealed class GameComponent_EconomyExample : GameComponent
    {
        private int schemaVersion = 1;
        private List<AccountRecordExample> accounts = new List<AccountRecordExample>();

        public GameComponent_EconomyExample(Game game) { }

        public override void ExposeData()
        {
            base.ExposeData();
            // Default 0 permits distinguishing absent keys in older saves.
            Scribe_Values.Look(ref schemaVersion, "peaExampleSchemaVersion", 0);
            Scribe_Collections.Look(ref accounts, "peaExampleAccounts", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && accounts == null)
                accounts = new List<AccountRecordExample>();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // Production code must migrate, validate references, rebuild indices,
            // reconcile reservations and transactions before accepting commands.
            // This specimen verifies API calls only; it implements no ledger.
        }
    }
}
