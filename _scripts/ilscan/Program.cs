using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

class Program
{
    static void Main(string[] args)
    {
        string path = args.Length > 1 && File.Exists(args[1])
            ? args[1]
            : @"E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\Assembly-CSharp.dll";
        if (!File.Exists(path))
        {
            Console.WriteLine("NOT FOUND: " + path);
            return;
        }
        using (var fs = File.OpenRead(path))
        using (var pe = new PEReader(fs))
        {
            var md = pe.GetMetadataReader();
            if (args.Length >= 2 && args[0] == "--dump")
            {
                DumpType(md, pe, args[1]);
                return;
            }
            if (args.Length >= 2 && args[0] == "--learn-calls")
            {
                DumpLearnCalls(md, pe, args[1]);
                return;
            }
            // 默认：全量找 Learn 调用点
            var learnRefs = new HashSet<int>();
            int rowId = 0;
            foreach (var mdh in md.MethodDefinitions)
            {
                rowId++;
                var m = md.GetMethodDefinition(mdh);
                if (md.GetString(m.Name) == "Learn")
                {
                    learnRefs.Add(rowId);
                }
            }
            foreach (var tdh in md.TypeDefinitions)
            {
                var td = md.GetTypeDefinition(tdh);
                foreach (var mdh in td.GetMethods())
                {
                    var m = md.GetMethodDefinition(mdh);
                    if (m.RelativeVirtualAddress == 0) continue;
                    try
                    {
                        var body = pe.GetMethodBody(m.RelativeVirtualAddress);
                        byte[] il = body.GetILBytes();
                        float? lastR4 = null;
                        int i = 0;
                        while (i < il.Length)
                        {
                            byte b = il[i];
                            int opLen;
                            if (b == 0xFE) { if (i + 1 >= il.Length) break; opLen = FeLen(il[i + 1]); i += 2; }
                            else if (b == 0xFF) { i += 1; opLen = 0; }
                            else { opLen = OneLen(b); i += 1; }
                            if (b == 0x22 && i + 4 <= il.Length) lastR4 = BitConverter.ToSingle(il, i);
                            if ((b == 0x28 || b == 0x6F) && opLen >= 4 && i + 4 <= il.Length)
                            {
                                int token = BitConverter.ToInt32(il, i);
                                if ((token >> 24) == 0x06 && learnRefs.Contains(token & 0xFFFFFF))
                                {
                                    Console.WriteLine("LearnCall in " + FullTypeName(md, tdh) + "." + md.GetString(m.Name)
                                        + "  [last ldc.r4=" + (lastR4.HasValue ? lastR4.Value.ToString("R") : "N/A") + "]");
                                }
                            }
                            i += opLen;
                        }
                    }
                    catch (Exception) { }
                }
            }
        }
        Console.WriteLine("DONE");
    }

    static void DumpType(MetadataReader md, PEReader pe, string want)
    {
        string[] mdNames = BuildMdNames(md);
        string[] mrNames = BuildMrNames(md);
        foreach (var tdh in md.TypeDefinitions)
        {
            string tn = FullTypeName(md, tdh);
            if (tn != want && !tn.StartsWith(want + "/", StringComparison.Ordinal)) continue;
            var td = md.GetTypeDefinition(tdh);
            foreach (var mdh in td.GetMethods())
            {
                var m = md.GetMethodDefinition(mdh);
                if (m.RelativeVirtualAddress == 0)
                {
                    Console.WriteLine("[" + tn + "." + md.GetString(m.Name) + "] <no-body>");
                    continue;
                }
                var body = pe.GetMethodBody(m.RelativeVirtualAddress);
                byte[] il = body.GetILBytes();
                var targets = new List<string>();
                var consts = new List<string>();
                int i = 0;
                while (i < il.Length)
                {
                    byte b = il[i];
                    int opLen;
                    if (b == 0xFE) { if (i + 1 >= il.Length) break; opLen = FeLen(il[i + 1]); i += 2; }
                    else if (b == 0xFF) { i += 1; opLen = 0; }
                    else { opLen = OneLen(b); i += 1; }
                    if (opLen >= 4 && (b == 0x28 || b == 0x6F || b == 0x70 || b == 0x71 || b == 0x72 || b == 0x73 || b == 0x74 || b == 0x75 || b == 0x79 || b == 0x7B || b == 0x7C || b == 0x7D || b == 0x7E || b == 0x7F || b == 0x80 || b == 0x81 || b == 0x8D || b == 0x8E || b == 0x90 || b == 0xB7 || b == 0xC2 || b == 0xC6) && i + 4 <= il.Length)
                    {
                        int token = BitConverter.ToInt32(il, i);
                        string nm = ResolveName(md, mdNames, mrNames, token);
                        if (nm != null && !nm.StartsWith("get_", StringComparison.Ordinal) && !nm.StartsWith("set_", StringComparison.Ordinal))
                            targets.Add(nm);
                    }
                    if (b == 0x22 && i + 4 <= il.Length) consts.Add(BitConverter.ToSingle(il, i).ToString("R"));
                    if (b == 0x23 && i + 8 <= il.Length) consts.Add(BitConverter.ToDouble(il, i).ToString("R"));
                    i += opLen;
                }
                Console.WriteLine("[" + tn + "." + md.GetString(m.Name) + "] body=" + il.Length
                    + " calls=" + (targets.Count == 0 ? "-" : string.Join(", ", targets)));
                if (consts.Count > 0) Console.WriteLine("   consts: " + string.Join(", ", consts));
            }
        }
    }

    /// <summary>dump 指定类型中所有方法体内 Learn 调用前的 IL 指令（含常量/操作数）。</summary>
    static void DumpLearnCalls(MetadataReader md, PEReader pe, string want)
    {
        string[] mdNames = BuildMdNames(md);
        string[] mrNames = BuildMrNames(md);
        string[] fdNames = BuildFdNames(md);
        int rowId = 0;
        var learnRefs = new HashSet<int>();
        foreach (var mdh in md.MethodDefinitions)
        {
            rowId++;
            if (md.GetString(md.GetMethodDefinition(mdh).Name) == "Learn") learnRefs.Add(rowId);
        }
        foreach (var tdh in md.TypeDefinitions)
        {
            string tn = FullTypeName(md, tdh);
            if (tn != want && !tn.StartsWith(want + "/", StringComparison.Ordinal)) continue;
            var td = md.GetTypeDefinition(tdh);
            foreach (var mdh in td.GetMethods())
            {
                var m = md.GetMethodDefinition(mdh);
                if (m.RelativeVirtualAddress == 0) continue;
                var body = pe.GetMethodBody(m.RelativeVirtualAddress);
                byte[] il = body.GetILBytes();
                int i = 0;
                var prev = new List<string>();
                while (i < il.Length)
                {
                    byte b = il[i];
                    int opLen;
                    if (b == 0xFE) { if (i + 1 >= il.Length) break; opLen = FeLen(il[i + 1]); i += 2; }
                    else if (b == 0xFF) { i += 1; opLen = 0; }
                    else { opLen = OneLen(b); i += 1; }
                    string desc = OpDesc(md, mdNames, mrNames, fdNames, b, il, i, opLen);
                    if (b == 0x28 || b == 0x6F)
                    {
                        int token = BitConverter.ToInt32(il, i);
                        if ((token >> 24) == 0x06 && learnRefs.Contains(token & 0xFFFFFF))
                        {
                            Console.WriteLine("### " + tn + "." + md.GetString(m.Name) + " -> Learn at off=" + (i - 1));
                            for (int k = prev.Count - 1; k >= Math.Max(0, prev.Count - 10); k--)
                                Console.WriteLine("    " + prev[k]);
                            Console.WriteLine("    ==> " + desc);
                        }
                    }
                    prev.Add(string.Format("off={0,-5} {1,-22} {2}", i - 1, OpName(b), desc));
                    i += opLen;
                }
            }
        }
    }

    static string OpDesc(MetadataReader md, string[] mdNames, string[] mrNames, string[] fdNames, byte b, byte[] il, int i, int opLen)
    {
        try
        {
            if ((b == 0x7B || b == 0x7C || b == 0x7D || b == 0x7E) && opLen >= 4 && i + 4 <= il.Length)
            {
                int token = BitConverter.ToInt32(il, i);
                if ((token >> 24) == 0x04)
                {
                    int rid = token & 0xFFFFFF;
                    return "field:" + (rid < fdNames.Length ? fdNames[rid] : ("token " + token.ToString("X8")));
                }
                if ((token >> 24) == 0x0A)
                {
                    int rid = token & 0xFFFFFF;
                    return "mref:" + (rid < mrNames.Length ? mrNames[rid] : ("token " + token.ToString("X8")));
                }
                return "ftoken " + token.ToString("X8");
            }
            if (b == 0x22 && i + 4 <= il.Length) return "ldc.r4 " + BitConverter.ToSingle(il, i).ToString("R");
            if (b == 0x20 && i + 4 <= il.Length) return "ldc.i4 " + BitConverter.ToInt32(il, i);
            if (b == 0x21 && i + 8 <= il.Length) return "ldc.i8 " + BitConverter.ToInt64(il, i);
            if (b == 0x72 && i + 4 <= il.Length)
            {
                int token = BitConverter.ToInt32(il, i);
                return "ldstr [token " + token.ToString("X8") + "]";
            }
            if ((b == 0x28 || b == 0x6F || b == 0x7B || b == 0x7C || b == 0x8D || b == 0x90 || b == 0xB7) && opLen >= 4 && i + 4 <= il.Length)
            {
                int token = BitConverter.ToInt32(il, i);
                string nm = ResolveName(md, mdNames, mrNames, token);
                return (nm != null ? nm : ("token " + token.ToString("X8")));
            }
        }
        catch (Exception) { }
        return "";
    }

    static string OpName(byte b)
    {
        switch (b)
        {
            case 0x16: return "ldloc.0";
            case 0x17: return "ldloc.1";
            case 0x18: return "ldloc.2";
            case 0x19: return "ldloc.3";
            case 0x0A: return "stloc.0";
            case 0x0B: return "stloc.1";
            case 0x0C: return "stloc.2";
            case 0x0D: return "stloc.3";
            case 0x02: return "ldarg.0";
            case 0x03: return "ldarg.1";
            case 0x04: return "ldarg.2";
            case 0x05: return "ldarg.3";
            case 0x00: return "nop";
            case 0x2A: return "ret";
            case 0x26: return "ldnull";
            case 0x6E: return "ldfld";
            case 0x7B: return "ldfld";
            case 0x7C: return "ldsfld";
            case 0x7D: return "stfld";
            case 0x7E: return "stsfld";
            case 0x8C: return "ldelem";
            case 0x8E: return "stelem";
            case 0x8D: return "newarr";
            case 0x73: return "newobj";
            case 0x28: return "call";
            case 0x6F: return "callvirt";
            case 0x90: return "dup";
            case 0x70: return "box";
            case 0x22: return "ldc.r4";
            case 0x20: return "ldc.i4";
            case 0x21: return "ldc.i8";
            case 0x1F: return "ldc.i4.s";
            case 0x24: return "ldnull-obj";
            case 0x72: return "ldstr";
            case 0x10: return "ldarg.s";
            case 0x0E: return "ldarg.s";
            case 0x0F: return "ldarg.s";
            case 0x25: return "ldc.i4.1";
            case 0x2B: return "br.s";
            case 0x2D: return "brtrue.s";
            case 0x2C: return "brfalse.s";
            case 0x39: return "blt.s";
            case 0x3B: return "ble.s";
            case 0x38: return "bgt.s";
            case 0x3A: return "bge.s";
            case 0x33: return "beq.s";
            case 0x32: return "bne.un.s";
            case 0x57: return "add";
            case 0x58: return "sub";
            case 0x5B: return "mul";
            case 0x5A: return "div";
            case 0x59: return "neg";
            case 0x6A: return "conv.r4";
            case 0x6C: return "conv.r8";
            default: return "op" + b.ToString("X2");
        }
    }

    static string ResolveName(MetadataReader md, string[] mdNames, string[] mrNames, int token)
    {
        try
        {
            int table = (token >> 24) & 0xFF;
            int rid = token & 0xFFFFFF;
            if (table == 0x06 && rid < mdNames.Length) return mdNames[rid];
            if (table == 0x0A && rid < mrNames.Length) return mrNames[rid];
        }
        catch (Exception) { }
        return null;
    }

    static string[] BuildMdNames(MetadataReader md)
    {
        var names = new List<string> { "" };
        foreach (var mdh in md.MethodDefinitions)
        {
            names.Add(md.GetString(md.GetMethodDefinition(mdh).Name));
        }
        return names.ToArray();
    }

    static string[] BuildMrNames(MetadataReader md)
    {
        var names = new List<string> { "" };
        foreach (var mrh in md.MemberReferences)
        {
            names.Add(md.GetString(md.GetMemberReference(mrh).Name));
        }
        return names.ToArray();
    }

    static string[] BuildFdNames(MetadataReader md)
    {
        var names = new List<string> { "" };
        foreach (var fdh in md.FieldDefinitions)
        {
            names.Add(md.GetString(md.GetFieldDefinition(fdh).Name));
        }
        return names.ToArray();
    }

    static string FullTypeName(MetadataReader md, TypeDefinitionHandle h)
    {
        try
        {
            string name = md.GetString(md.GetTypeDefinition(h).Name);
            TypeDefinitionHandle outer = md.GetTypeDefinition(h).GetDeclaringType();
            if (!outer.IsNil)
            {
                string outerName = FullTypeName(md, outer);
                return outerName + "/" + name;
            }
            return name;
        }
        catch (Exception)
        {
            try { return md.GetString(md.GetTypeDefinition(h).Name); }
            catch (Exception) { return "?"; }
        }
    }

    static int OneLen(byte b)
    {
        switch (b)
        {
            case 0x0E: case 0x0F: case 0x10: case 0x11: case 0x12: case 0x13:
            case 0x1F: case 0x24: case 0xD0:
                return 1;
            case 0x20: case 0x22:
            case 0x28: case 0x29:
            case 0x6F: case 0x70: case 0x71:
            case 0x72: case 0x73: case 0x74: case 0x75:
            case 0x79:
            case 0x7B: case 0x7C: case 0x7D: case 0x7E: case 0x7F: case 0x80:
            case 0x81:
            case 0x8D: case 0x8E:
            case 0x90:
            case 0xB7:
            case 0xC2: case 0xC4: case 0xC6:
            case 0xDD:
                return 4;
            case 0x21: case 0x23:
                return 8;
            default:
                return 0;
        }
    }

    static int FeLen(byte b2)
    {
        switch (b2)
        {
            case 0x09: case 0x0A: case 0x0B: case 0x0C: case 0x0D: case 0x0E:
                return 2;
            case 0x1E:
                return 1;
            case 0x21: case 0x22: case 0x27:
                return 4;
            default:
                return 0;
        }
    }
}
