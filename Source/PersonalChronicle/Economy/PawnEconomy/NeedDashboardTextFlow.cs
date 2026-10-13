using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PersonalChronicle.Economy.PawnEconomy
{
    // Text preparation only; drawing remains in RUIF, whose Draw.Label disables automatic wrapping.
    //
    // 兼容性约定（2026-10-08 实测）：本文件在 netstandard 2.1 引用面下编译（RimWorld 的
    // netstandard.dll），Roslyn 会把 `Split('\n')` / `TrimEnd()` 解析到 2.1 才有的重载
    // （`Split(char, StringSplitOptions)` / 无参 `TrimEnd()`），而 net48 宿主（引擎转发）
    // 没有它们 ⇒ 测试与 net48 运行期抛 MissingMethodException。此处一律显式写成
    // net48/2.1 双方都存在的重载形式；`TrimEnd((char[])null)` 语义等同无参版（裁剪空白）。
    internal static class NeedDashboardTextFlow
    {
        public static string Wrap(string text, float width, Func<string, float> measure)
        {
            if (measure == null) throw new ArgumentNullException(nameof(measure));
            if (!(width > 0) || float.IsInfinity(width)) throw new ArgumentOutOfRangeException(nameof(width));
            if (string.IsNullOrEmpty(text)) return text ?? "";
            var output = new List<string>();
            // 显式写 Split(char[], StringSplitOptions)：在 netstandard 2.1 引用面下 `Split('\n')`
            // 会被解析到 `Split(char, StringSplitOptions)`（normal form 优先于 params 展开），
            // 而 net48 宿主（经 netstandard.dll 转发）没有该重载 ⇒ 运行期 MissingMethodException。
            foreach (string paragraph in text.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.None))
            {
                var line = new StringBuilder();
                int before = output.Count;
                foreach (Match token in Regex.Matches(paragraph, @"\S+|\s+"))
                {
                    if (string.IsNullOrWhiteSpace(token.Value)) { if (line.Length != 0) line.Append(token.Value); continue; }
                    if (measure(line.ToString() + token.Value) <= width) { line.Append(token.Value); continue; }
                    if (line.Length != 0) { output.Add(line.ToString().TrimEnd((char[])null)); line.Clear(); }
                    var elements = StringInfo.GetTextElementEnumerator(token.Value);
                    while (elements.MoveNext())
                    {
                        string element = elements.GetTextElement();
                        if (line.Length != 0 && measure(line.ToString() + element) > width)
                        { output.Add(line.ToString()); line.Clear(); }
                        line.Append(element);
                    }
                }
                if (line.Length != 0) output.Add(line.ToString().TrimEnd((char[])null));
                else if (output.Count == before) output.Add("");
            }
            return string.Join("\n", output);
        }
    }
}
