using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 制度门「缺哪一步」的**可见性与单一来源**（2026-10-12 实机反馈驱动）：
    /// 新殖民地未完成一次存档往返时，制度停在 Preparing —— 劳务站此前只显示
    /// 「制度尚未开放新的用工承诺」，玩家无从得知要做什么（`Player.log` 实证：
    /// `Initializing new game` + `[PC][RECRUIT] … mode=Preparing commitments=False decision=SeekWork`）。
    /// 现在由劳务站写出与经济页 blocker 行**同源**的下一步操作。
    ///
    /// <para>本 fixture 刻意只断言**离线可判**的部分：文案组装的唯一实现 + 被挡路径确实调用它。
    /// 真实 inspect 文本（游戏内 IMGUI 显示）属 GameVerified，本文件不冒充覆盖。</para>
    /// </summary>
    [TestFixture]
    public sealed class InstitutionGateGuidanceTests
    {
        private const string LabelsFile = "EconomyLabels.cs";
        private const string StationFile = "EconomyWorld.cs";
        private const string WindowFile = "EconomyCommands.cs";

        /// <summary>门名与下一步两个前缀**只允许**出现在唯一实现里；第二处出现即失败
        /// —— 同一句话两份组装 = 缺键时两种回落行为（BASE-005 / GOV-010）。</summary>
        [Test] public void GateTextPrefixesLiveInOnePlace()
        {
            string root = Root();
            var offenders = new List<string>();
            foreach (string file in Directory.GetFiles(Path.Combine(root, "Source", "PersonalChronicle"), "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == LabelsFile) continue;
                string text = File.ReadAllText(file);
                if (text.Contains("Mode.Step.") || text.Contains("Mode.Blocker.")) offenders.Add(Path.GetFileName(file));
            }
            Assert.That(offenders, Is.Empty, "制度门文案只能由 EconomyLabels 组装，第二处出现：" + string.Join(", ", offenders));

            // 反向对照：唯一实现本身必须在，否则上面"没有第二处"是空断言（实现被删也照样通过）。
            string labels = Path.Combine(root, "Source", "PersonalChronicle", "Economy", "PawnEconomy", LabelsFile);
            Assert.That(File.Exists(labels), Is.True, "唯一实现文件不存在：" + labels);
            string impl = File.ReadAllText(labels);
            Assert.That(Regex.Matches(impl, "\"Mode\\.Step\\.\"").Count, Is.EqualTo(1), "下一步前缀必须恰好一处");
            Assert.That(Regex.Matches(impl, "\"Mode\\.Blocker\\.\"").Count, Is.EqualTo(1), "门名前缀必须恰好一处");
        }

        /// <summary>**被挡住的地方必须真的写出下一步**：劳务站识别制度门原因码并调用唯一实现；
        /// 经济页门名/下一步也走唯一实现（两处同源，不再是各写一份）。</summary>
        [Test] public void BlockedLaborStationShowsTheNextStep()
        {
            string root = Root();
            string folder = Path.Combine(root, "Source", "PersonalChronicle", "Economy", "PawnEconomy");
            string station = File.ReadAllText(Path.Combine(folder, StationFile));
            Assert.That(station.Contains("InstitutionNotReady"), Is.True,
                "劳务站必须识别制度门原因码（否则新档只看到「制度尚未开放」，不知要做什么）");
            Assert.That(station.Contains("FirstGateStep()"), Is.True,
                "劳务站必须转发到唯一实现（不得自拼文案）");

            string window = File.ReadAllText(Path.Combine(folder, WindowFile));
            Assert.That(window.Contains("EconomyLabels.GateBlocker("), Is.True, "经济页门名必须走唯一实现");
            Assert.That(window.Contains("EconomyLabels.GateStep("), Is.True, "经济页下一步必须走唯一实现");

            // 右键「接合同」失败的那条消息是玩家最常撞到的入口（截图同一局面）。
            string interact = File.ReadAllText(Path.Combine(folder, "EconomyLaborInteraction.cs"));
            Assert.That(interact.Contains("InstitutionNotReady"), Is.True, "右键失败提示必须识别制度门原因码");
            Assert.That(interact.Contains("GameComponent_PawnEconomy.FirstGateStep("), Is.True,
                "右键失败提示必须用唯一实现写出下一步（否则玩家仍只看到「制度尚未开放」）");
        }

        /// <summary>「第一项缺失门的下一步」只能有一处组装：三处入口（经济页 / 劳务站 inspect / 右键失败提示）
        /// 都必须转发到它 —— 否则同一句话会出现三种回落行为与三种"缺哪一步"的口径。</summary>
        [Test] public void FirstGateStepIsAssembledOnce()
        {
            string root = Root();
            var files = Directory.GetFiles(Path.Combine(root, "Source", "PersonalChronicle"), "*.cs", SearchOption.AllDirectories);
            var definitions = new List<string>();
            var callers = new List<string>();
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                if (text.Contains("internal static string FirstGateStep(")) definitions.Add(Path.GetFileName(file));
                if (text.Contains("FirstGateStep()") && !text.Contains("internal static string FirstGateStep("))
                    callers.Add(Path.GetFileName(file));
            }
            Assert.That(definitions.Count, Is.EqualTo(1), "唯一实现必须恰好一处，实际：" + string.Join(", ", definitions));
            Assert.That(callers.Count, Is.GreaterThanOrEqualTo(2), "至少劳务站与右键两个入口必须转发到它，实际：" + string.Join(", ", callers));
        }

        private static string Root()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Source", "PersonalChronicle"))) return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("找不到工程根");
        }
    }
}
