using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PersonalChronicle.Economy.PawnEconomy;

namespace PersonalChronicle.Tests
{
    /// <summary>CHG-20261012-WORKGATE 的离线可证伪面：授权判定核 + 白名单口径 + **封印**（不许碰面板读数）。</summary>
    [TestFixture]
    public sealed class WorkAuthorityTests
    {
        [Test] public void ThreeSourcesAreAUnion_NeverAnIntersection()
        {
            Assert.That(WorkAuthority.IsAllowed(contract: false, shift: false, whitelist: false), Is.False,
                "三源皆无 ⇒ 拦（这是本功能的全部意义）");
            Assert.That(WorkAuthority.IsAllowed(true, false, false), Is.True, "合同单独成立");
            Assert.That(WorkAuthority.IsAllowed(false, true, false), Is.True, "车间班次托管单独成立");
            Assert.That(WorkAuthority.IsAllowed(false, false, true), Is.True, "白名单单独成立");
            Assert.That(WorkAuthority.IsAllowed(true, true, true), Is.True);
        }

        [TestCase(10, -1, true)]
        [TestCase(249, 0, false)]
        [TestCase(250, 0, true)]
        [TestCase(100, 1000, true)]
        [TestCase(int.MaxValue, 0, true)]
        public void MaintenanceBoundIsIndependentOfCacheReads(int now, int previous, bool expected)
        {
            Assert.That(WorkAuthority.RefreshDue(now, previous), Is.EqualTo(expected));
        }

        [Test] public void MaintenanceIsDrivenByGameTicksEvenWhenTheGiverCacheStaysClean()
        {
            string runtime = File.ReadAllText(Path.Combine(Root(), "Source", "PersonalChronicle", "Economy", "PawnEconomy", "EconomyRuntime.cs"));
            Assert.That(runtime, Does.Contain("WorkAuthorityService.Tick(Find.TickManager?.TicksGame ?? 0)"),
                "dirty=false 的原版作业表不会自行重建；实际 GameComponentTick 必须主动驱动失效");
            string service = File.ReadAllText(Path.Combine(Root(), "Source", "PersonalChronicle", "Economy", "PawnEconomy", "WorkAuthorityService.cs"));
            Assert.That(service, Does.Contain("WorkAuthority.RefreshDue(now, lastInvalidateTick)"));
            Assert.That(service, Does.Contain("ReferenceEquals(cachedGame, game)"), "新游戏即使 tick 相同也不能保留旧 Pawn 缓存");
            Assert.That(service, Does.Contain("cachedRevision != revision"), "暂停同 tick 改设置必须更新 UI");
            Assert.That(service, Does.Contain("manager.AuthorizesScheduledWork(pawn, type)"), "地图里存在班次不能授权无关 Pawn");
            string schedule = File.ReadAllText(Path.Combine(Root(), "Source", "PersonalChronicle", "Automation", "MapComponent_WorkshopManager.Runtime.cs"));
            Assert.That(schedule, Does.Contain("IsRuntimeManaged(pawn, type) && CanExecuteDuty(pawn) && CanRunScheduledWork(pawn, type)"),
                "当前 Pawn 的实际岗位必须覆盖该工种；CanRun 对非托管工种默认 true 不能作为授权");
        }

        [Test] public void PresentationPriorityPutsSurvivalFirstThenShiftThenContract()
        {
            Assert.That(WorkAuthority.Resolve(false, false, false), Is.EqualTo(WorkAuthoritySource.None));
            Assert.That(WorkAuthority.Resolve(true, false, false), Is.EqualTo(WorkAuthoritySource.Contract));
            Assert.That(WorkAuthority.Resolve(false, true, false), Is.EqualTo(WorkAuthoritySource.Shift));
            Assert.That(WorkAuthority.Resolve(false, false, true), Is.EqualTo(WorkAuthoritySource.Whitelist));
            // 优先级只影响"显示哪一条理由"，不影响放行结果（并集）
            Assert.That(WorkAuthority.Resolve(true, true, false), Is.EqualTo(WorkAuthoritySource.Shift));
            Assert.That(WorkAuthority.Resolve(true, false, true), Is.EqualTo(WorkAuthoritySource.Whitelist));
        }

        [Test] public void EmptyWorkTypeIsNeverGated_BecauseItBelongsToNoWorkType()
        {
            // 取证：原版有 3 个 workType 为空的 WorkGiverDef（自我急救 / 赶动物入栏 / 栏内再平衡）
            // —— 它们不进 wtsByPrio（因而永不进有序作业表），既不受勾选影响也不受本闸门影响。
            Assert.That(WorkAuthority.ShouldGate(null), Is.False);
            Assert.That(WorkAuthority.ShouldGate(string.Empty), Is.False);
            Assert.That(WorkAuthority.ShouldGate("Hauling"), Is.True);
        }

        [Test] public void WhitelistMatchesByStableDefNameOnly()
        {
            var list = new List<string> { "Firefighter", "Doctor" };
            Assert.That(WorkAuthority.IsWhitelisted("Firefighter", list), Is.True);
            Assert.That(WorkAuthority.IsWhitelisted("firefighter", list), Is.False,
                "稳定 ID 大小写敏感：绝不因本地化/大小写差异误判（GOV-009）");
            Assert.That(WorkAuthority.IsWhitelisted("灭火", list), Is.False, "绝不按显示文本匹配");
            Assert.That(WorkAuthority.IsWhitelisted(null, list), Is.False);
            Assert.That(WorkAuthority.IsWhitelisted("Hauling", null), Is.False);
        }

        [Test] public void DefaultWhitelistCoversTheTwoSurvivalFamiliesThatHaveNoGatelessAlternative()
        {
            List<string> defaults = WorkAuthority.Defaults();
            Assert.That(defaults, Is.EqualTo(new[] { "Firefighter", "Doctor" }),
                "默认白名单 = 灭火 + 医疗（对他人治疗没有 ThinkNode 替代路径；自我急救本身不在工种内）");
            // 交付默认值的副本，调用方改它不得污染默认值
            defaults.Add("Hauling");
            Assert.That(WorkAuthority.Defaults(), Does.Not.Contain("Hauling"));
        }

        [Test] public void NormalizeDropsEmptyAndDuplicatesButKeepsOrder()
        {
            var messy = new List<string> { "Doctor", null, "", "Firefighter", "Doctor" };
            List<string> clean = WorkAuthority.Normalize(messy);
            Assert.That(clean, Is.EqualTo(new[] { "Doctor", "Firefighter" }));
            Assert.That(WorkAuthority.Normalize(null), Is.Empty);
        }

        /// <summary>★ 封印：本功能的**前提**是"面板与其它原版判定照旧"。
        /// 一旦有人顺手去 patch <c>GetPriority</c>/<c>WorkIsActive</c>（面板读数与非作业系统的判定也走它们），
        /// 这个前提就没了 —— 用源扫描把这条纪律钉死。</summary>
        [Test] public void GateNeverPatchesTheSharedPriorityReads()
        {
            string root = Root();
            var files = Directory.GetFiles(Path.Combine(root, "Source", "PersonalChronicle"), "*.cs", SearchOption.AllDirectories);
            var offenders = new List<string>();
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                bool patchedPriority = text.Contains("HarmonyPatch(typeof(Pawn_WorkSettings), \"GetPriority\"")
                    || text.Contains("HarmonyPatch(typeof(Pawn_WorkSettings), \"WorkIsActive\"")
                    || text.Contains("nameof(Pawn_WorkSettings.GetPriority)")
                    || text.Contains("nameof(Pawn_WorkSettings.WorkIsActive)");
                if (patchedPriority) offenders.Add(Path.GetFileName(file));
            }
            Assert.That(offenders, Is.Empty,
                "闸门必须只改作业表缓存；patch GetPriority/WorkIsActive 会连带改面板与 GenConstruct/BillDialogUtility/ITab_Pawn_Visitor："
                + string.Join(", ", offenders));
            // 反向对照：闸门补丁本身必须在，否则上面这条"没违规"是空断言（文件被删也照样通过）
            string gate = Path.Combine(root, "Source", "PersonalChronicle", "Capture", "Patch_PawnWorkSettings_CacheGivers.cs");
            Assert.That(File.Exists(gate), Is.True, "闸门补丁文件不存在 ⇒ 封印失效：" + gate);
            Assert.That(File.ReadAllText(gate).Contains("\"CacheWorkGiversInOrder\""), Is.True,
                "闸门必须挂在唯一入表点 CacheWorkGiversInOrder");
        }

        private static string Root()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Source", "PersonalChronicle"))) return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("找不到工程根（封印失效）");
        }
    }
}
