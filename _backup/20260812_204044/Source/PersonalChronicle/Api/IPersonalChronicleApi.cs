using PersonalChronicle.Application;

namespace PersonalChronicle.Api
{
    /// <summary>
    /// The single public entry point for integrators. Instead of discovering
    /// several scattered static service properties, a third-party mod asks for
    /// this one facade and branches on <see cref="Version"/> / <see cref="Supports"/>.
    ///
    /// Legacy static properties on <c>PersonalChronicleMod</c> remain for binary
    /// compatibility but new code should go through <see cref="PersonalChronicleApi.TryGet"/>.
    /// </summary>
    public interface IPersonalChronicleApi
    {
        /// <summary>API version of this facade. Compare with <see cref="ApiVersion.Satisfies"/>.</summary>
        ApiVersion Version { get; }

        /// <summary>Read-only archive queries.</summary>
        IArchiveQueryService Queries { get; }

        /// <summary>Unified event write entry point.</summary>
        IArchiveEventSink Events { get; }

        /// <summary>Custom work-system sampling (4.0 contract, retained).</summary>
        IWorkTimeCaptureService WorkTime { get; }

        /// <summary>Work intensity / gradient / colony work aggregation (4.0 contract).</summary>
        IWorkIntensityService WorkIntensity { get; }

        /// <summary>Unified provider registry — every domain provider (work intensity,
        /// production, battle, relation, place) lives here, discoverable by capability
        /// token. This is the single registry for P2+ integrations.</summary>
        IArchiveProviderRegistry Providers { get; }

        /// <summary>Work-intensity scoped registry (v4.1 contract, retained for
        /// binary compatibility). Same backing registry as <see cref="Providers"/>,
        /// narrowed to <see cref="IWorkIntensityProvider"/> lookups.</summary>
        IWorkIntensityProviderRegistry WorkIntensityProviders { get; }

        /// <summary>
        /// Capability probe: returns true when the requested Major line is matched
        /// at the requested Minor level. Use named feature checks instead of
        /// parsing <see cref="Version"/> directly.
        /// </summary>
        bool Supports(int major, int minMinor);
    }
}
