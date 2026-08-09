using System.Collections.Generic;

namespace PersonalChronicle.Api
{
    /// <summary>
    /// Unified, domain-agnostic provider registry (design doc §7.3). One registry
    /// owns every <see cref="IArchiveProvider"/> regardless of domain — production,
    /// battle, relation, place, work-intensity — so integrators are discovered by
    /// capability token rather than by registering into N separate silos.
    ///
    /// Thread-safety: registration happens on the main thread during mod init and
    /// on Harmony-patched capture entry points; iteration happens on the UI thread.
    /// RimWorld is single-threaded for game logic, so no lock is required.
    ///
    /// Failure isolation: a throwing provider must never break the archive read or
    /// write path. Callers use <see cref="ForEach"/> (or the typed extension in
    /// <see cref="ArchiveProviderRegistryExtensions"/>) which swallows and
    /// deduplicates provider exceptions.
    /// </summary>
    public interface IArchiveProviderRegistry
    {
        /// <summary>
        /// Registers a provider. Returns false for null / empty-id providers.
        /// Re-registration with the same <see cref="IArchiveProvider.ProviderId"/>
        /// replaces the previous entry (last writer wins) and re-sorts by priority.
        /// </summary>
        bool Register(IArchiveProvider provider);

        /// <summary>
        /// All currently registered providers, pre-sorted by descending
        /// <see cref="IArchiveProvider.Priority"/> then ascending ProviderId.
        /// Read-only snapshot — mutating the returned list is unsupported.
        /// </summary>
        IReadOnlyList<IArchiveProvider> Providers { get; }

        /// <summary>
        /// Iterates providers in priority order. <paramref name="visit"/> is invoked
        /// exactly once per non-null provider. Any exception thrown by the visitor
        /// (or by a provider's property access during enumeration) is captured and
        /// forwarded to <paramref name="onError"/> at most once per ProviderId, so a
        /// misbehaving provider degrades gracefully instead of aborting the loop.
        /// </summary>
        void ForEach(
            System.Action<IArchiveProvider> visit,
            System.Action<IArchiveProvider, System.Exception> onError = null);

        /// <summary>
        /// Returns providers whose <see cref="IArchiveProvider.Capabilities"/>
        /// contain <paramref name="capability"/>. Useful for capability-scoped
        /// lookups without loading every domain's typed interface.
        /// </summary>
        IReadOnlyList<IArchiveProvider> GetByCapability(string capability);
    }
}
