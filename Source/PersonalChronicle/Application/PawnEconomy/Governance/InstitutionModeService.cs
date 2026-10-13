using System;
using PersonalChronicle.Domain.PawnEconomy.Governance;

namespace PersonalChronicle.Application.PawnEconomy.Governance
{
    /// <summary>Settings express intent. Runtime evidence authorizes capabilities; loaded flags never do.</summary>
    internal sealed class InstitutionModeService
    {
        public InstitutionModeState State { get; private set; }
        public InstitutionModeService(InstitutionModeState state) { State = state ?? throw new ArgumentNullException(nameof(state)); }
        public void Observe(bool desired, InstitutionReadiness readiness, bool outstanding)
        {
            if (readiness == null) throw new ArgumentNullException(nameof(readiness));
            InstitutionPhase next = State.Phase; string fault = State.Fault;
            if (next != InstitutionPhase.Faulted)
            {
                if (readiness.Fault != null) { next = InstitutionPhase.Faulted; fault = readiness.Fault; }
                else if (!desired)
                { next = next == InstitutionPhase.Off ? InstitutionPhase.Off : outstanding ? InstitutionPhase.Draining : InstitutionPhase.Off; }
                else if (next == InstitutionPhase.Draining && outstanding) { /* Never bypass unfinished rights by toggling back on. */ }
                else if (next == InstitutionPhase.Active && !readiness.AllReady && outstanding) next = InstitutionPhase.Draining;
                else next = readiness.AllReady ? InstitutionPhase.Active : InstitutionPhase.Preparing;
            }
            if (next == State.Phase && desired == State.Desired && fault == State.Fault) return;
            if (State.Revision == long.MaxValue)
            { State = new InstitutionModeState(InstitutionPhase.Faulted, desired, State.Revision, "ModeRevisionExhausted"); return; }
            State = new InstitutionModeState(next, desired, State.Revision + 1, fault);
        }
        public InstitutionModeSnapshot Read(InstitutionReadiness readiness, bool outstanding) => new InstitutionModeSnapshot(State, readiness, outstanding);
    }
}
