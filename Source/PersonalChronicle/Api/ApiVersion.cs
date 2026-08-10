namespace PersonalChronicle.Api
{
    /// <summary>
    /// Semantic version of the public API surface. Breaking changes bump
    /// <see cref="Major"/>; backward-compatible additions bump <see cref="Minor"/>.
    /// External mods use <see cref="Supports"/> against the capability flags
    /// exposed by <see cref="IPersonalChronicleApi"/> rather than hardcoding a
    /// version string.
    /// </summary>
    public sealed class ApiVersion
    {
        /// <summary>Breaking changes (4 = v4.x line).</summary>
        public int Major { get; }

        /// <summary>Backward-compatible additions within the Major line.</summary>
        public int Minor { get; }

        /// <summary>Internal patch / bugfix level (informational).</summary>
        public int Patch { get; }

        public ApiVersion(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        /// <summary>
        /// Public API contract version. Bumped in lock-step with the shipped
        /// modVersion: v4.1 unified the API facade, v4.2 exposed the work-intensity
        /// provider contract, v4.3 added the faction codex, v4.5 introduced the UI
        /// Design System, v4.6 added the inspect-tab integration and v4.7 the
        /// legacy/ownership chain. The Minor level tracks the latest capability so
        /// <see cref="IPersonalChronicleApi.Supports"/> reports true for every
        /// capability actually shipped.
        /// </summary>
        public static ApiVersion Current { get; } = new ApiVersion(4, 7, 0);

        /// <summary>
        /// Returns true when this version satisfies a (major, minMinor) contract,
        /// i.e. same Major and at least the requested Minor. Used by
        /// <see cref="IPersonalChronicleApi.Supports"/> so callers never branch on
        /// raw version numbers.
        /// </summary>
        public bool Satisfies(int major, int minMinor)
        {
            return Major == major && Minor >= minMinor;
        }

        public override string ToString()
        {
            return Major + "." + Minor + "." + Patch;
        }
    }
}
