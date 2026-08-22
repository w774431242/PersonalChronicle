using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive.UI
{
    /// <summary>
    /// v1.1.5 UI texture asset layer — single convergence point for all
    /// theme-able image assets.
    ///
    /// Assets live under <c>Textures/UI/&lt;themeId&gt;/&lt;element&gt;.png</c> and are
    /// loaded lazily via <see cref="ContentFinder{T}"/> (engine-standard, no
    /// custom loader). Each theme's assets are cached per element so the lookup
    /// is allocation-free after first paint.
    ///
    /// Callers (UIComponents) MUST treat a null result as "fall back to the
    /// token colour" — never assume an asset exists. This keeps the other three
    /// themes (wuxia/steampunk/gothic) fully functional with zero assets.
    ///
    /// AI-004 compliant: no bare asset paths anywhere except this class.
    /// </summary>
    internal static class UITextureLibrary
    {
        // Element keys — the only vocabulary callers use.
        internal const string Card = "card";
        internal const string Panel = "panel";
        internal const string StatCell = "statcell";

        // Per-theme cache: themeId → (element → texture). Lazily populated.
        private static readonly Dictionary<string, Dictionary<string, Texture2D>> Cache =
            new Dictionary<string, Dictionary<string, Texture2D>>();

        /// <summary>
        /// Returns the texture for <paramref name="element"/> under the active
        /// theme, or null if the asset is absent (caller falls back to colour).
        /// </summary>
        internal static Texture2D Get(string themeId, string element)
        {
            if (string.IsNullOrEmpty(themeId) || string.IsNullOrEmpty(element))
                return null;

            if (!Cache.TryGetValue(themeId, out Dictionary<string, Texture2D> themeCache))
            {
                themeCache = new Dictionary<string, Texture2D>();
                Cache[themeId] = themeCache;
            }
            if (themeCache.TryGetValue(element, out Texture2D cached))
                return cached;

            // First lookup for this element — load once, cache result (even null).
            string path = $"UI/{themeId}/{element}";
            Texture2D tex = ContentFinder<Texture2D>.Get(path, false);
            themeCache[element] = tex;
            return tex;
        }

        /// <summary>
        /// Drop all cached textures (e.g. on language/theme reset). Cheap;
        /// textures reload on next paint.
        /// </summary>
        internal static void Clear()
        {
            Cache.Clear();
        }
    }
}
