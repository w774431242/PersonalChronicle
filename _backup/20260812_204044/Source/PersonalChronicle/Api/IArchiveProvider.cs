using System.Collections.Generic;

namespace PersonalChronicle.Api
{
    /// <summary>
    /// Minimal common contract for every PersonalChronicle provider (work intensity,
    /// production, combat, social, UI sections, ...). Defined per design doc §7.1.
    ///
    /// Concrete domain providers extend this interface and add their capability
    /// methods — the base contract deliberately stays minimal so a single registry
    /// can hold heterogeneous providers and reason about them generically.
    ///
    /// Lifecycle rules (design doc §7.2):
    /// - <see cref="ProviderId"/> must be stable and globally unique.
    /// - Duplicate <see cref="ProviderId"/> re-registration replaces the earlier one.
    /// - Providers are invoked highest <see cref="Priority"/> first.
    /// - A provider throwing must never break vanilla capture or built-in providers.
    /// - The registry is a process-level runtime object; it is NOT persisted and is
    ///   re-populated on load / new game.
    ///
    /// A provider must NOT (design doc §7.3): mutate ChronicleGameComponent.Objects,
    /// touch PawnObject.WorkTime internals, write to save during evaluation, allocate
    /// heavily every tick, or assume a UI window is open.
    /// </summary>
    public interface IArchiveProvider
    {
        /// <summary>Stable, globally unique provider identity (e.g. "PersonalChronicle.WorkIntensity").</summary>
        string ProviderId { get; }

        /// <summary>Contract version this provider was built against (e.g. "4.1").</summary>
        string ContractVersion { get; }

        /// <summary>Invocation order: higher runs first. Ties broken by registration order.</summary>
        int Priority { get; }

        /// <summary>
        /// Capability tokens this provider contributes (e.g. "WorkIntensity",
        /// "Production", "Combat"). Consumers use these to discover providers of a
        /// given kind without knowing concrete types.
        /// </summary>
        IReadOnlyCollection<string> Capabilities { get; }
    }
}
