using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using NUnit.Framework;
using PersonalChronicle.Economy.PawnEconomy;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// CHG-20261010-ECON-UI-BATCH 的离线可证伪面（经济窗口呈现层）：
    /// F1 零行空态、F2 总览缺键回落、F3 政策行"实测文本高 vs 按钮带"、F5 七门 ID → 呈现键。
    ///
    /// <para>本 fixture 刻意只断言**离线可判**的部分：键集合 + 纯几何（<see cref="EconomyWindowLayout"/>）。
    /// 真实绘制（IMGUI 折行、滚动命中、实际亚像素位置）属 GameVerified，本文件不冒充覆盖
    /// —— 相关未验证项在变更标注里标 NOT RUN。</para>
    /// </summary>
    [TestFixture]
    public sealed class EconomyWindowPresentationTests
    {
        /// <summary>本批新增/引用的键（zh/en 必须逐键齐备，防裸键复发）。</summary>
        private static readonly string[] BatchKeys =
        {
            "PC_PEA.Tab.Empty", "PC_PEA.Tab.EmptyHint",
            // CHG-20261012 · B2：`PC_PEA.Overview.Unpublished` 已从清单移除 —— 它唯一的用户是总览
            // 「车间区工资报价」块的 `O("Unpublished")`，该块随机构政策退役 ⇒ 键与语言文件条目一并删除
            //（保留一个零引用的键，只会让"键必须双语齐备"这条守卫去守一段死数据）。
            // B2 新增 4 键（GATE-LOC 2026-10-11 P3-a：它们此前**不在任何自动扫描内** ——
            // 三条走 `TranslatedOrId`/`Formatted` 全键形态，`[TW]("…")` 正则收不到；此处补上这层守卫）。
            "PC_PEA.Policy.ServicePriceHelp", "PC_PEA.Policy.ServicePriceClear",
            "PC_PEA.Policy.ServicePriceChanged", "PC_PEA.Workspace.Pay.Frozen",
            "PC_PEA.Care.Gate.VanillaAccess", "PC_PEA.Care.Gate.DeliveryEvidence",
            "PC_PEA.Care.Gate.CapacityAndHours", "PC_PEA.Care.Gate.PriceCap",
            "PC_PEA.Care.Gate.Settlement", "PC_PEA.Care.Gate.InterruptionRecovery",
            "PC_PEA.Care.Gate.UiExplanation",
        };

        /// <summary>P2-3（2026-10-10）本批新增的键：诊断窗 12 处硬编码中文 → <c>PC_PEA.Diag.*</c>。</summary>
        private static readonly string[] DiagnosticsKeys =
        {
            "PC_PEA.Diag.RuntimeNull", "PC_PEA.Diag.Section.Perception", "PC_PEA.Diag.PerceptionUnavailable",
            "PC_PEA.Diag.Population", "PC_PEA.Diag.PopulationComplete", "PC_PEA.Diag.OldestDueNote",
            "PC_PEA.Diag.Section.Institution", "PC_PEA.Diag.None", "PC_PEA.Diag.Section.PawnPlans",
            "PC_PEA.Diag.NoColonists", "PC_PEA.Diag.PlanMissing", "PC_PEA.Diag.WillFailFacts"
        };

        private static string RepositoryRoot()
            => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../.."));

        private static Dictionary<string, string> Keyed(string language)
        {
            string root = RepositoryRoot();
            var values = new Dictionary<string, string>();
            foreach (string file in new[] { "PawnEconomy.xml", "TreasuryOperations.xml" })
            {
                var xml = new XmlDocument();
                xml.Load(Path.Combine(root, "Languages", language, "Keyed", file));
                foreach (XmlNode child in xml.DocumentElement.ChildNodes)
                    if (child.NodeType == XmlNodeType.Element && !values.ContainsKey(child.Name))
                        values.Add(child.Name, child.InnerText);
            }
            return values;
        }

        [Test] public void EveryBatchKeyResolvesInBothLanguages()
        {
            var zh = Keyed("ChineseSimplified");
            var en = Keyed("English");
            foreach (string key in BatchKeys)
            {
                Assert.That(zh.ContainsKey(key), Is.True, "中文 Keyed 缺键：" + key);
                Assert.That(en.ContainsKey(key), Is.True, "英文 Keyed 缺键：" + key);
                Assert.That(zh[key], Is.Not.EqualTo(key), "中文键值不得退回键名本身（裸键）：" + key);
                Assert.That(en[key], Is.Not.EqualTo(key), "英文键值不得退回键名本身（裸键）：" + key);
            }
        }

        [Test] public void EveryCareGateIdHasItsOwnPresentedKey()
        {
            // 门 ID 产自 Domain 的 CareRules.Missing()；呈现层只允许把它们映射成 PC_PEA.Care.Gate.*。
            var zh = Keyed("ChineseSimplified");
            var en = Keyed("English");
            foreach (string gate in new[] { "VanillaAccess", "DeliveryEvidence", "CapacityAndHours", "PriceCap",
                "Settlement", "InterruptionRecovery", "UiExplanation" })
            {
                Assert.That(zh.ContainsKey("PC_PEA.Care.Gate." + gate), Is.True, "中文缺门键：" + gate);
                Assert.That(en.ContainsKey("PC_PEA.Care.Gate." + gate), Is.True, "英文缺门键：" + gate);
            }
        }

        [Test] public void ZeroRowsIsTheOnlyStateThatShowsTheEmptyState()
        {
            Assert.That(EconomyWindowLayout.ShowsEmptyState(0), Is.True, "零行必须走空态原子（否则是白纸）");
            Assert.That(EconomyWindowLayout.ShowsEmptyState(1), Is.False, "有行不得走空态");
            // 分支与文案绑在同一组常量上：改了键名或删了键，本 fixture 会红。
            Assert.That(EconomyWindowLayout.EmptyStateTitleKey, Is.EqualTo("PC_PEA.Tab.Empty"));
            Assert.That(EconomyWindowLayout.EmptyStateHintKey, Is.EqualTo("PC_PEA.Tab.EmptyHint"));
            var zh = Keyed("ChineseSimplified");
            Assert.That(zh.ContainsKey(EconomyWindowLayout.EmptyStateTitleKey), Is.True);
            Assert.That(zh.ContainsKey(EconomyWindowLayout.EmptyStateHintKey), Is.True);
        }

        // CHG-20261012 · B2：`PolicyFieldGridCoversSixGroupsInThreeColumns` **已删除** —— 它断言的政策字段卡
        // 网格（6 组 / 3 列 / 列宽恰好铺满内宽）随机构政策与政策页一并退役：被测常量已删，
        // 留着这条用例只会让"测试守着一段已经不存在的几何"（恒红或恒真，两种都不是验收）。

        /// <summary>诊断窗的全部提示文本必须在 zh/en 逐键可解析（缺键会把裸键打进诊断读数里）。</summary>
        [Test] public void EveryDiagnosticsKeyResolvesInBothLanguages()
        {
            var zh = Keyed("ChineseSimplified");
            var en = Keyed("English");
            foreach (string key in DiagnosticsKeys)
            {
                Assert.That(zh.ContainsKey(key), Is.True, "中文 Keyed 缺键：" + key);
                Assert.That(en.ContainsKey(key), Is.True, "英文 Keyed 缺键：" + key);
                Assert.That(zh[key], Is.Not.EqualTo(key), "中文键值不得退回键名本身（裸键）：" + key);
                Assert.That(en[key], Is.Not.EqualTo(key), "英文键值不得退回键名本身（裸键）：" + key);
            }
        }

        /// <summary>裸键防线（GOV-010）：经济呈现层不得对 <c>"PC_PEA.*"</c> **字面量键**直接 <c>Translate()</c> ——
        /// 必须经 <see cref="EconomyLabels"/> 的回落入口；否则缺键时整把键会直接上屏（P0-4 登记的根因形态）。
        /// 唯一例外是 <c>EconomyLabels.cs</c> 本身（它就是回落实现）。</summary>
        [Test] public void EconomyPresentationNeverTranslatesRawKeyLiterals()
        {
            string folder = Path.Combine(RepositoryRoot(), "Source", "PersonalChronicle", "Economy", "PawnEconomy");
            Assert.That(Directory.Exists(folder), Is.True, "经济呈现层目录不存在：" + folder);
            var pattern = new Regex("\"PC_PEA\\.[^\"]*\"\\s*\\.Translate\\(\\)");
            foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == "EconomyLabels.cs") continue;
                Assert.That(pattern.IsMatch(File.ReadAllText(file)), Is.False,
                    "裸键 Translate（应经 EconomyLabels 回落入口）：" + Path.GetFileName(file));
            }
        }

        /// <summary>带参翻译防线：原版 <c>Translate(this string, params object[])</c> 在 1.6 已 <c>[Obsolete]</c>，
        /// 且缺键时会把**带 <c>{0}</c> 的裸键**打上屏 ⇒ 呈现层禁止使用该形态（一律走 <c>TF</c>/<c>Formatted</c>）。</summary>
        [Test] public void EconomyPresentationNeverUsesObsoletePositionalTranslate()
        {
            string folder = Path.Combine(RepositoryRoot(), "Source", "PersonalChronicle", "Economy", "PawnEconomy");
            // 匹配 "任意键".Translate( 至少一个参数 ) —— 零参 Translate() 是合规通道，不在此列。
            var pattern = new Regex("\\.Translate\\(\\s*[^)\\s]");
            foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == "EconomyLabels.cs") continue;
                Assert.That(pattern.IsMatch(File.ReadAllText(file)), Is.False,
                    "使用了 1.6 已 Obsolete 的带参 Translate（应走 TF/Formatted）：" + Path.GetFileName(file));
            }
        }

        /// <summary>P0 家族（GOV-010）**未解析键自证**：键形"译文"必须被判为未解析并回落 ——
        /// 这是「政策页裸键 <c>PC_PEA.Cap.Tool.&lt;hash&gt;</c>」唯一的可解释机制（外部字典把键形串当译文写回）。</summary>
        [Test] public void KeyShapedTranslationsAreTreatedAsUnresolved()
        {
            Assert.That(EconomyLabels.LooksLikeUnresolvedKey("PC_PEA.Cap.Tool.0ab9dc0b"), Is.True,
                "键形译文必须被判为未解析（P0 登记的裸键形态）");
            Assert.That(EconomyLabels.LooksLikeUnresolvedKey("  PC_PEA.Overview.Title"), Is.True, "前导空白后仍是键形");
            Assert.That(EconomyLabels.LooksLikeUnresolvedKey("工具"), Is.False);
            Assert.That(EconomyLabels.LooksLikeUnresolvedKey("Stock"), Is.False);
            Assert.That(EconomyLabels.LooksLikeUnresolvedKey(""), Is.False);
            Assert.That(EconomyLabels.LooksLikeUnresolvedKey(null), Is.False);
        }

        /// <summary>语言纯度（同一 P0 家族的**数据面**防线）：任何语言文件的键值都不得是键形串 ——
        /// 一旦出现，说明字典被覆盖/机翻注入，游戏内会把 <c>PC_PEA.*</c> 直接打上屏。</summary>
        [Test] public void NoKeyedValueIsAKeyShapedString()
        {
            foreach (string language in new[] { "ChineseSimplified", "English" })
            {
                string folder = Path.Combine(RepositoryRoot(), "Languages", language, "Keyed");
                Assert.That(Directory.Exists(folder), Is.True, "Keyed 目录不存在：" + folder);
                foreach (string file in Directory.GetFiles(folder, "*.xml"))
                {
                    var xml = new XmlDocument();
                    xml.Load(file);
                    foreach (XmlNode child in xml.DocumentElement.ChildNodes)
                    {
                        if (child.NodeType != XmlNodeType.Element) continue;
                        Assert.That(EconomyLabels.LooksLikeUnresolvedKey(child.InnerText), Is.False,
                            "键值形如键（会被判为未解析）：" + Path.GetFileName(file) + " / " + child.Name);
                    }
                }
            }
        }

        // CHG-20261012 · B2：`PolicyHeadDropsLabelsBeforeSqueezingTheName` **已删除** —— 它断言的
        // 政策卡头降级判定（`FitPolicyHead` / `MinPolicyNameWidth`：按优先级舍「修订」「车间区」件链，
        // 保证名称拿到下限宽）随政策页退役一并消失：卡头件链本身已不存在，被测纯函数已删。
        // ⚠ 该不变量（"标签与名称争宽时按优先级舍标签"）若将来出现在别的卡头上，应在新卡头的几何里重建断言。
    }
}
