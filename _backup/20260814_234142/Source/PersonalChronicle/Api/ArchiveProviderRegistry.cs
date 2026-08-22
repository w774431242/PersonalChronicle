using System;
using System.Collections.Generic;

namespace PersonalChronicle.Api
{
    /// <summary>
    /// Default <see cref="IArchiveProviderRegistry"/> implementation (design doc
    /// §7.3). Instance-owned: no global mutable static collection. Each game
    /// process gets a fresh registry populated by <see cref="ChronicleMod"/> and by
    /// third-party mods that resolve the API facade and call
    /// <c>api.Providers.Register(...)</c>.
    ///
    /// Providers are kept sorted by descending <see cref="IArchiveProvider.Priority"/>
    /// then ascending ProviderId, so the first successful <c>TryEvaluate</c> in a
    /// priority-ordered loop wins (mirrors the work-intensity fallback chain).
    /// </summary>
    public sealed class ArchiveProviderRegistry : IArchiveProviderRegistry
    {
        private readonly List<IArchiveProvider> providers = new List<IArchiveProvider>();

        public IReadOnlyList<IArchiveProvider> Providers
        {
            get { return providers; }
        }

        public bool Register(IArchiveProvider provider)
        {
            if (provider == null || string.IsNullOrEmpty(provider.ProviderId))
            {
                return false;
            }
            for (int i = providers.Count - 1; i >= 0; i--)
            {
                if (providers[i] != null && providers[i].ProviderId == provider.ProviderId)
                {
                    providers.RemoveAt(i);
                }
            }
            providers.Add(provider);
            providers.Sort(CompareProviders);
            return true;
        }

        public void ForEach(
            Action<IArchiveProvider> visit,
            Action<IArchiveProvider, Exception> onError = null)
        {
            if (visit == null)
            {
                return;
            }
            HashSet<string> warned = onError != null ? new HashSet<string>() : null;
            for (int i = 0; i < providers.Count; i++)
            {
                IArchiveProvider provider = providers[i];
                if (provider == null)
                {
                    continue;
                }
                try
                {
                    visit(provider);
                }
                catch (Exception ex)
                {
                    if (onError != null && warned.Add(provider.ProviderId))
                    {
                        onError(provider, ex);
                    }
                }
            }
        }

        public IReadOnlyList<IArchiveProvider> GetByCapability(string capability)
        {
            if (string.IsNullOrEmpty(capability))
            {
                return new List<IArchiveProvider>();
            }
            List<IArchiveProvider> matched = new List<IArchiveProvider>();
            for (int i = 0; i < providers.Count; i++)
            {
                IArchiveProvider provider = providers[i];
                if (provider == null)
                {
                    continue;
                }
                IReadOnlyCollection<string> caps = provider.Capabilities;
                if (caps != null)
                {
                    foreach (string cap in caps)
                    {
                        if (cap == capability)
                        {
                            matched.Add(provider);
                            break;
                        }
                    }
                }
            }
            return matched;
        }

        private static int CompareProviders(IArchiveProvider a, IArchiveProvider b)
        {
            if (ReferenceEquals(a, b))
            {
                return 0;
            }
            if (a == null)
            {
                return 1;
            }
            if (b == null)
            {
                return -1;
            }
            int priority = b.Priority.CompareTo(a.Priority);
            return priority != 0
                ? priority
                : string.Compare(a.ProviderId, b.ProviderId, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Typed traversal helpers over <see cref="IArchiveProviderRegistry"/>. These
    /// keep call sites terse and guarantee the same failure-isolation semantics as
    /// <see cref="IArchiveProviderRegistry.ForEach"/> for domain-specific providers.
    /// </summary>
    public static class ArchiveProviderRegistryExtensions
    {
        /// <summary>
        /// Visits every provider castable to <typeparamref name="T"/> (e.g.
        /// <c>IProductionProvider</c>) in priority order. Provider exceptions are
        /// captured and forwarded to <paramref name="onError"/> at most once per
        /// ProviderId.
        /// </summary>
        public static void ForEach<T>(
            this IArchiveProviderRegistry registry,
            Action<T> visit,
            Action<IArchiveProvider, Exception> onError = null)
            where T : class, IArchiveProvider
        {
            if (registry == null || visit == null)
            {
                return;
            }
            registry.ForEach(
                provider =>
                {
                    T typed = provider as T;
                    if (typed != null)
                    {
                        visit(typed);
                    }
                },
                onError);
        }
    }
}
