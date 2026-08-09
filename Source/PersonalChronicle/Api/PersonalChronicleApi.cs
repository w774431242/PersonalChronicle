using PersonalChronicle.Application;

namespace PersonalChronicle.Api
{
    /// <summary>
    /// Static entry point for integrators. Resolves the live
    /// <see cref="IPersonalChronicleApi"/> from the running mod instance.
    ///
    /// Usage:
    /// <code>
    /// if (PersonalChronicleApi.TryGet(out var api)
    ///     &amp;&amp; api.Supports(4, 1))
    /// {
    ///     api.Events.TryRecord(input);
    /// }
    /// </code>
    /// </summary>
    public static class PersonalChronicleApi
    {
        /// <summary>
        /// Resolves the public API facade. Returns false (and sets api = null) when
        /// PersonalChronicle is not loaded or not yet initialized — callers must
        /// handle the false branch gracefully.
        /// </summary>
        public static bool TryGet(out IPersonalChronicleApi api)
        {
            api = PersonalChronicleMod.Api;
            return api != null;
        }
    }
}
