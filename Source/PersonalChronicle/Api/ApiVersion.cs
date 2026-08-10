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
        /// Public API contract version. Shipped in lock-step with the release
        /// <c>modVersion</c>: v1.1.0 continues the unified 1.x line (v1.0.0 folded
        /// the previous 4.x development line into a single published version; v1.1.0
        /// adds the location atlas, battle three-elements capture, social-network
        /// graph, equipment legacy chain and related fixes on top of it).
        /// <see cref="IPersonalChronicleApi.Supports"/> now branches on the
        /// capability flags exposed by the facade instead of raw version numbers,
        /// so callers should use <c>Supports(major, minMinor)</c> rather than
        /// comparing against <see cref="Current"/> directly.
        /// </summary>
        public static ApiVersion Current { get; } = new ApiVersion(1, 1, 0);

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
