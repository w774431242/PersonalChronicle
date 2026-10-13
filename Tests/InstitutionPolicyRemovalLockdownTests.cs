using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace PersonalChronicle.Tests
{
    /// <summary>CHG-20261012-ECON-CONTRACTIZE · B2：**机构政策净删的封印**（源扫描式 · 离线可证伪）。
    ///
    /// <para><b>为什么需要这条</b>：净删类改动的失败模式不是"编译不过"，而是**悄悄复活** ——
    /// 后续批次里有人写回一句 `QueryInstitutionPolicy(...)`，编译照样通过（类型若被重新引入），
    /// 而"政策已退役"这个已经交付给玩家的结论就变成了谎话。本封印把三件事钉死：</para>
    ///
    /// <para>① 四个政策类型（`InstitutionPolicy` / `InstitutionPolicySaveData` / `InstitutionPolicyRules`
    /// / `InstitutionCapabilityRules`）的文件**不存在**；② 生产源里**没有任何调用级引用**
    /// （只查"调用形态"——`new X` / `X.` / `X(`——不误伤解释"为什么删"的注释）；③ 旧档节点
    /// `pawnEconomicPolicies` **只被只读探针**消费且**只在 `LoadingVars` 分支**（永不写回）。</para>
    /// </summary>
    [TestFixture]
    public sealed class InstitutionPolicyRemovalLockdownTests
    {
        /// <summary>失败即说明"回到仓库根"的路径推断失效（改名/搬目录后本封印会自己变红，不会静默失效）。</summary>
        private static string SourceRoot
        {
            get
            {
                var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, "Source", "PersonalChronicle");
                    if (Directory.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
                throw new DirectoryNotFoundException("找不到 Source/PersonalChronicle（封印失效）");
            }
        }

        private static IEnumerable<string> ProductionFiles
            => Directory.GetFiles(SourceRoot, "*.cs", SearchOption.AllDirectories);

        /// <summary>只匹配**调用形态**：`new InstitutionPolicy(`、`InstitutionPolicyRules.`、
        /// `ApplyInstitutionPolicy(`… —— 注释里出现类型名（说明"为什么删"）不算引用，
        /// 否则本封印会把解释文字判成违规（这正是"sweep .md 必然自指假阳性"的同族教训）。</summary>
        private static readonly string[] ForbiddenCallForms =
        {
            "new InstitutionPolicy(", "InstitutionPolicyRules.", "InstitutionPolicySaveData.",
            "InstitutionCapabilityRules.", "InstitutionCapabilityKind.",
            "ApplyInstitutionPolicy(", "QueryInstitutionPolicy(", "QueryInstitutionPolicies(",
            "QueryStockTargetPolicy(", "QueryServicePricePolicy(", "QueryFoodPricePolicy(",
            "QueryApparelPricePolicy(", "QueryZoneWagePolicy(", "PolicyCode.", "PolicyResult.",
            "EconomyHostedTabs.Policy", "WorkspaceHostedRenderer",
        };

        [Test] public void TheDeletedPolicyTypesAreGone()
        {
            var remaining = new List<string>();
            foreach (string file in ProductionFiles)
            {
                string name = Path.GetFileName(file);
                if (name == "InstitutionPolicy.cs" || name == "InstitutionPolicySaveData.cs"
                    || name == "InstitutionPolicyRules.cs" || name == "InstitutionCapabilityRules.cs"
                    || name == "EconomyInstitutionWorkSource.cs")
                    remaining.Add(Path.GetFileName(file));
            }
            Assert.That(remaining, Is.Empty,
                "净删的文件又回来了（政策 4 类型 / 车间岗位机会源）：" + string.Join(", ", remaining));
        }

        /// <summary>剥掉注释再扫 —— 封印只判**代码引用**，不判"解释为什么删"的文字。
        /// 否则文档注释里写一句 `<c>InstitutionPolicySaveData.ExposeData</c>` 就会被判违规，
        /// 逼着后来者把"为什么删"的解释删掉（这正是"扫 .md 必然自指假阳性"的同族教训）。</summary>
        private static string StripComments(string text)
        {
            var builder = new System.Text.StringBuilder(text.Length);
            bool inBlockComment = false;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw;
                if (inBlockComment)
                {
                    int close = line.IndexOf("*/", StringComparison.Ordinal);
                    if (close < 0) { builder.Append('\n'); continue; }
                    line = line.Substring(close + 2);
                    inBlockComment = false;
                }
                int open = line.IndexOf("/*", StringComparison.Ordinal);
                if (open >= 0) { inBlockComment = true; line = line.Substring(0, open); }
                int slash = line.IndexOf("//", StringComparison.Ordinal);
                if (slash >= 0) line = line.Substring(0, slash);
                builder.Append(line).Append('\n');
            }
            return builder.ToString();
        }

        [Test] public void NoProductionFileCallsBackIntoThePolicySurface()
        {
            var hits = new List<string>();
            foreach (string file in ProductionFiles)
            {
                string text = StripComments(File.ReadAllText(file));
                foreach (string form in ForbiddenCallForms)
                    if (text.Contains(form)) hits.Add(Path.GetFileName(file) + " → " + form);
            }
            Assert.That(hits, Is.Empty,
                "政策面被重新引用（净删封印被突破）：" + string.Join(" | ", hits));
        }

        [Test] public void TheCommentStripperItselfStillSeesRealCode()
        {
            // ★ 反向对照：剥注释不能把**代码层**的违例也一起剥掉（否则封印变成恒真）。
            string code = "var x = 1; // new InstitutionPolicy(\n/* QueryInstitutionPolicy( */ var y = 2;\n";
            Assert.That(StripComments(code).Contains("new InstitutionPolicy("), Is.False, "行注释内的违例应被剥掉");
            Assert.That(StripComments(code).Contains("QueryInstitutionPolicy("), Is.False, "块注释内的违例应被剥掉");
            Assert.That(StripComments("var p = QueryInstitutionPolicy(\"a\");").Contains("QueryInstitutionPolicy("), Is.True,
                "真实调用必须仍被看到 —— 否则封印没有牙齿");
        }

        [Test] public void TheLegacyNodeIsConsumedReadOnlyAndExactlyOnce()
        {
            var nodeHits = new List<string>();
            var lookHits = new List<KeyValuePair<string, string[]>>();
            foreach (string file in ProductionFiles)
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains("\"pawnEconomicPolicies\"")) nodeHits.Add(Path.GetFileName(file));
                    if (lines[i].Contains("Scribe_Deep.Look(ref legacyPolicyProbe"))
                        lookHits.Add(new KeyValuePair<string, string[]>(Path.GetFileName(file), lines));
                }
            }
            Assert.That(nodeHits.Distinct().ToArray(), Is.EqualTo(new[] { "LegacyPolicyProbe.cs" }),
                "节点名的裸字符串只允许出现在探针的 `NodeName` 常量定义处（唯一真值）；"
                + "别处再写一遍即说明有人绕开探针直接读写该节点。实际命中：" + string.Join(", ", nodeHits));
            Assert.That(lookHits.Count, Is.EqualTo(1),
                "旧档节点必须**恰好**在一个地方被读（EconomyRuntime 的读档分支）");
            var entry = lookHits[0];
            int index = Array.FindIndex(entry.Value, l => l.Contains("Scribe_Deep.Look(ref legacyPolicyProbe"));
            string guard = string.Join("\n", entry.Value.Skip(Math.Max(0, index - 6)).Take(7));
            Assert.That(guard.Contains("LoadSaveMode.LoadingVars"), Is.True,
                "读取必须在 `LoadingVars` 守卫内（否则保存分支会把这个已退役节点写回存档）");
            Assert.That(entry.Key, Is.EqualTo("EconomyRuntime.cs"), "读取点应在 EconomyRuntime.cs（当前：" + entry.Key + "）");
        }
    }
}
