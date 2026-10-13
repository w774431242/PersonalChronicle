using System;
using System.Collections.Generic;
using System.IO;

class RenameMedals
{
    static void Main()
    {
        string dir = @"E:\SteamLibrary\steamapps\common\RimWorld\11\殖民地综合管理系统\Textures\Medals";
        var map = new Dictionary<string, string>
        {
            {"劳动勋章铜牌.png", "Medal_Labor_Model_Bronze.png"},
            {"劳动勋章银牌.png", "Medal_Labor_Model_Silver.png"},
            {"劳动勋章金牌.png", "Medal_Labor_Model_Gold.png"},
            {"先进团体铜牌.png", "Medal_Labor_Worker_Bronze.png"},
            {"先进团体银牌.png", "Medal_Labor_Worker_Silver.png"},
            {"先进团体金牌.png", "Medal_Labor_Worker_Gold.png"},
        };
        foreach (var kv in map)
        {
            string src = Path.Combine(dir, kv.Key);
            string dst = Path.Combine(dir, kv.Value);
            if (File.Exists(src))
            {
                File.Move(src, dst);
                Console.WriteLine("Renamed: {0} -> {1}", kv.Key, kv.Value);
            }
            else
            {
                Console.WriteLine("MISSING: {0}", src);
            }
        }
        Console.WriteLine("--- Current files:");
        foreach (string f in Directory.GetFiles(dir))
        {
            Console.WriteLine(Path.GetFileName(f));
        }
    }
}
