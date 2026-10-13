using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace PersonalChronicle
{
    /// <summary>
    /// Uses the same discovery and class processors as Harmony.PatchAll, but
    /// isolates failures per class. Each class is attempted once: replaying a
    /// failed assembly batch would duplicate patches on earlier classes and on
    /// the successful targets of a partially installed multi-target class.
    /// </summary>
    internal static class HarmonyStartup
    {
        internal static int PatchAll(Harmony harmony, Assembly assembly, Action<string> reportError)
        {
            return PatchClasses(harmony, AccessTools.GetTypesFromAssembly(assembly), reportError);
        }

        internal static int PatchClasses(Harmony harmony, IEnumerable<Type> types, Action<string> reportError)
        {
            if (harmony == null) throw new ArgumentNullException(nameof(harmony));
            if (types == null) throw new ArgumentNullException(nameof(types));
            if (reportError == null) throw new ArgumentNullException(nameof(reportError));
            int failures = 0;
            var attempted = new HashSet<Type>();
            foreach (Type type in types)
            {
                if (type == null || !attempted.Add(type)) continue;
                try
                {
                    // Exact predicate used by the installed Harmony.PatchAll.
                    // Attribute discovery does not run unrelated static constructors.
                    if (!type.HasHarmonyAttribute()) continue;
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception ex)
                {
                    failures++;
                    reportError("[PersonalChronicle] Harmony class failed; owner=" + harmony.Id
                        + "; class=" + type.FullName
                        + "; continuing remaining classes without retry; any installed targets retained: " + ex);
                }
            }
            return failures;
        }
    }
}
