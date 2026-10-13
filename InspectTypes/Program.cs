using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

class P
{
    static void Main()
    {
        string path = @"E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\Assembly-CSharp.dll";
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(@"E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed");
        var asm = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver, ReadingMode = ReadingMode.Immediate });

        string[] want = { "RimWorld.Bill_Production", "RimWorld.Bill", "RimWorld.CompBreakdownable",
                          "RimWorld.IngredientCount", "RimWorld.RecipeDef" };

        foreach (var m in asm.Modules)
        {
            foreach (var t in Enumerate(m.Types))
            {
                if (!want.Contains(t.FullName)) continue;
                Console.WriteLine("==== " + t.FullName + " : " + (t.BaseType != null ? t.BaseType.FullName : "-"));
                foreach (var f in t.Fields.Where(f => f.IsPublic))
                    Console.WriteLine("  F " + f.FieldType.Name + " " + f.Name);
                foreach (var p in t.Properties)
                    Console.WriteLine("  P " + p.PropertyType.Name + " " + p.Name);
                foreach (var me in t.Methods.Where(me => me.IsPublic && !me.IsGetter && !me.IsSetter))
                    Console.WriteLine("  M " + me.ReturnType.Name + " " + me.Name + "(" + string.Join(",", me.Parameters.Select(x => x.ParameterType.Name)) + ")");
            }
        }
    }

    static IEnumerable<TypeDefinition> Enumerate(IEnumerable<TypeDefinition> types)
    {
        foreach (var t in types)
        {
            yield return t;
            foreach (var n in Enumerate(t.NestedTypes)) yield return n;
        }
    }
}
