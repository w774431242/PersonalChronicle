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
        /// <c>modVersion</c>: v1.1.x continues the unified 1.x line (v1.0.0 folded
        /// the previous 4.x development line into a single published version; v1.1.0
        /// adds the location atlas, battle three-elements capture, social-network
        /// graph, equipment legacy chain and related fixes on top of it; v1.1.1
        /// brings the full HTML-preview parity pass — Location/Battle KPI strips,
        /// partitioned atlas & battle cards, exploitation net-yield ledger cell,
        /// cover identity pill, death-dossier battle row, important-relation table,
        /// weapon Overview KPI and health-valuation verdict; v1.1.2 reworks the
        /// pawn-inspect "档案" tab — removes the milestone timeline, condenses the
        /// six-cell KPI grid (工时/产出/击杀/战役/足迹/神器传承) with unified
        /// progress bars, fixes text clipping/overlap and widens the inspect window);
        /// v1.1.3 fixes coexistence bugs with Character Editor — supplies a proper
        /// LanguageInfo.xml so Simplified Chinese is recognized (CE editor no longer
        /// falls back to English), removes a stray loadBefore that placed this mod
        /// before the RimWorld core, and drops a duplicate Keyed key;
        /// v1.1.4 lands the HTML-preview parity for the pawn-inspect "档案" tab —
        /// battle KPI becomes the personal-view contract (累计参与/累计规模/累计歼敌
        /// + 个人贡献占比, removing Decisive/Losses blocks) and the kill KPI gains
        /// persistent personal combat dimensions (ParticipatedBattles,
        /// DamageDealtTotal, melee/ranged combat-style ratio).
        /// <see cref="IPersonalChronicleApi.Supports"/> now branches on the
        /// capability flags exposed by the facade instead of raw version numbers,
        /// so callers should use <c>Supports(major, minMinor)</c> rather than
        /// comparing against <see cref="Current"/> directly.
        /// </summary>
        public static ApiVersion Current { get; } = new ApiVersion(1, 1, 4);

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
