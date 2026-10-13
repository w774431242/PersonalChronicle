using System;
using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>成员满足同一工艺要求。首成员为缺失项显示使用的原型。</summary>
    public sealed class WorkshopDeviceEquivalenceDef : Def
    {
        public List<string> members = new List<string>();
    }

    public static class WorkshopDeviceIdentity
    {
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>();

        public static void Rebuild()
        {
            Names.Clear();
            foreach (WorkshopDeviceEquivalenceDef group in DefDatabase<WorkshopDeviceEquivalenceDef>.AllDefsListForReading)
            {
                if (group.members == null || group.members.Count == 0) continue;
                string canonical = group.members[0];
                if (string.IsNullOrEmpty(canonical)) throw new InvalidOperationException("Empty device family: " + group.defName);
                foreach (string name in group.members)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    if (Names.TryGetValue(name, out string old) && old != canonical)
                        throw new InvalidOperationException("Conflicting device family: " + name);
                    Names[name] = canonical;
                }
            }
        }

        public static string CanonicalName(string name)
        {
            return name != null && Names.TryGetValue(name, out string identity) ? identity : name;
        }

        public static bool IsAccepted(WorkshopTypeDef type, string name)
        {
            if (type == null || name == null) return false;
            string identity = CanonicalName(name);
            return Contains(type.expectedDeviceDefNames, identity) || Contains(type.acceptedDeviceDefNames, identity);
        }

        private static bool Contains(List<string> names, string identity)
        {
            if (names == null) return false;
            foreach (string name in names)
                if (name != null && CanonicalName(name) == identity) return true;
            return false;
        }
    }
}
