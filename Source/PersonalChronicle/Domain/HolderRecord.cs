using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// One owner/keeper record in a thing's legacy (传承) chain.
    ///
    /// Legacy semantics (user-confirmed): a "generation" is an ownership
    /// TRANSFER only — borrowing / lending (loan) is recorded for context but
    /// never counts toward the generation count, the first-holder badge, or the
    /// verdict derivation.
    ///
    /// <see cref="StartTick"/> marks when this record began (craft completion for
    /// the first holder, first observed hold otherwise). <see cref="KillCount"/>
    /// is derived by the Read Model from the weapon's kill events within
    /// [StartTick, EndTick) — never persisted, keeping the event index the single
    /// source of truth.
    /// </summary>
    public sealed class HolderRecord : IExposable
    {
        /// <summary>Stable id of the holder pawn (Pawn.GetUniqueLoadID()).</summary>
        public string StableId;

        /// <summary>Identity snapshot for display (label at capture time).</summary>
        public string LabelSnapshot;

        /// <summary>
        /// "own" = ownership transfer (counts toward generations);
        /// "loan" = borrow/lend context (never counts).
        /// </summary>
        public string Kind = HolderKindOwn;

        /// <summary>When this tenure began (craft tick for the first owner).</summary>
        public long StartTick = -1L;

        /// <summary>
        /// Start tick of the NEXT record (exclusive end of this tenure). -1 when
        /// this is still the current tenure.
        /// </summary>
        [Unsaved]
        public long EndTick = -1L;

        /// <summary>True when this record is the first owner (craft holder).</summary>
        public bool IsFirst;

        public const string HolderKindOwn = "own";
        public const string HolderKindLoan = "loan";

        public HolderRecord()
        {
        }

        public HolderRecord(string stableId, string labelSnapshot, long startTick, bool isFirst, string kind)
        {
            StableId = stableId;
            LabelSnapshot = labelSnapshot;
            StartTick = startTick;
            IsFirst = isFirst;
            Kind = kind;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref StableId, "stableId");
            Scribe_Values.Look(ref LabelSnapshot, "labelSnapshot");
            Scribe_Values.Look(ref Kind, "kind");
            Scribe_Values.Look(ref StartTick, "startTick", -1L);
            Scribe_Values.Look(ref IsFirst, "isFirst", false);
            // EndTick is deliberately not persisted: it is the next record's
            // StartTick (derived in the Read Model), never a stored duplicate.
        }
    }
}
