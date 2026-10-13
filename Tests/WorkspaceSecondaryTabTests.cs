using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Economy.PawnEconomy;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// CHG-20261010-ECON-AGG-MIGRATE 批次 B-2：柜台切换条「**位置 → 聚合页签号**」翻译的离线断言。
    ///
    /// <para><b>为什么单独立一条</b>：这条翻译此前**零覆盖**，于是漏掉了 GATE-ARCH（2026-10-10）抓到的 P0 ——
    /// `EnsureHostedView` 把切换条位置（0,1,…）直接当作聚合页签号传给 `Query`：
    /// 位置 0 命中 <c>BuildInstitutions</c>（机构页）⇒ 柜台里的「账户 / 台账 / 报告」**全部查错页**，
    /// 账户的写动词永不可达；而**编译与既有 1286 条单测都不会报错**（`Index` 字段当时全仓零读取）。
    /// 位置是排版序号、页签号是读入口语义参数，两者混用属"静默错页"，因此把翻译抽成纯函数并在此锁定。</para>
    /// </summary>
    [TestFixture]
    public class WorkspaceSecondaryTabTests
    {
        /// <summary>与工资结算处的真实声明同形：账户 = 聚合页签 3（可写）+ 台账 = 4（只读）。</summary>
        private static WorkspaceView SettlementCounter()
            => ViewWith(new WorkspaceSecondaryTab(3, "账户", true), new WorkspaceSecondaryTab(4, "台账"));

        private static WorkspaceView ViewWith(params WorkspaceSecondaryTab[] tabs)
        {
            // B2：`WorkspaceView` 已收掉 openText / hoursText / openTone（政策读数，随政策退役）⇒ 少 3 参。
            var view = new WorkspaceView(1, "counter", "kind", "phase",
                WorkspaceTone.Neutral, new List<WorkspaceKpi>(), new List<WorkspaceAction>(),
                new EconomyView(1, false, false, "status", "focus", new List<EconomyRow>(), new List<EconomyRow>()));
            view.SecondaryTabs = tabs;
            return view;
        }

        [Test] public void B2_HostedTabIndexTranslatesStripPositionToAggregateTabNumber()
        {
            var view = SettlementCounter();
            Assert.That(view.HostedTabIndex(0), Is.EqualTo(3),
                "位置 0（账户）必须翻译成页签号 3 —— 若返回 0 就会命中机构页（P0 复现）");
            Assert.That(view.HostedTabIndex(1), Is.EqualTo(4),
                "位置 1（台账）必须翻译成页签号 4 —— 若返回 1 就会命中岗位页（P0 复现）");
            Assert.That(view.HostedTabIndex(-1), Is.EqualTo(-1), "本柜台（-1）没有承载页，调用方据此跳过查询");
            Assert.That(view.HostedTabIndex(2), Is.EqualTo(-1), "越界位置返回 -1（不抛异常、不误查）");
        }

        [Test] public void B2_ExamCenterReportsTabTranslatesToTabFive()
        {
            // 体检中心：报告 = 聚合页签 5（只读）。
            var view = ViewWith(new WorkspaceSecondaryTab(5, "体检报告", false));
            Assert.That(view.HostedTabIndex(0), Is.EqualTo(5), "位置 0（报告）必须翻译成页签号 5");
            Assert.That(view.SecondaryTabs[0].NeedsCommandHost, Is.False, "报告页只读：宿主不挂按钮（F4）");
        }

        [Test] public void B2_CommandHostFlagIsPerPageNotPerCounter()
        {
            var view = SettlementCounter();
            Assert.That(view.SecondaryTabs[0].NeedsCommandHost, Is.True,
                "账户页需要宿主提供写上下文（操作人 + 国库戳）");
            Assert.That(view.SecondaryTabs[1].NeedsCommandHost, Is.False,
                "台账页只读（其 Cancel 经 Execute 的 LegacyFeesAllowed 门恒拒，且 Query 已剥成纯文本行）");
        }

        [Test] public void B2_NoSecondaryTabsMeansNoStripAndNoHostedQuery()
        {
            // 其余柜台（劳务站 / 商店 / …）没有承载页 ⇒ 切换条不出现、明细区就是本柜台业务行。
            var view = ViewWith();
            Assert.That(view.SecondaryTabs.Count, Is.EqualTo(0));
            Assert.That(view.HostedTabIndex(0), Is.EqualTo(-1));
            Assert.That(view.HostedTabIndex(-1), Is.EqualTo(-1));
        }

        // ─────────────────────────── C-1：机构列表 → 工坊管理处 ───────────────────────────

        [Test] public void C1_NavigationPageMountsRowVerbsWithoutCommandHost()
        {
            // 工坊管理处的机构列表：动词是**导航**（`Focus` 重定焦点；`Policy` 深链已随政策页退役），
            // 但不需要写上下文（没有缴存提取 ⇒ 不需要操作人与国库戳）。
            var view = ViewWith(new WorkspaceSecondaryTab(0, "机构", false, true));
            Assert.That(view.SecondaryTabs[0].NeedsNavigation, Is.True, "机构列表含导航动词");
            Assert.That(view.SecondaryTabs[0].NeedsCommandHost, Is.False, "机构列表不需要写上下文");
            Assert.That(view.SecondaryTabs[0].HostsRowVerbs, Is.True,
                "挂载判据必须是两个能力位的**或** —— 只看 NeedsCommandHost 会把「政策」「配置」按钮静默丢掉");
            Assert.That(view.HostedTabIndex(0), Is.EqualTo(0), "机构列表 = 聚合页签 0（InstitutionsTab）");
        }

        [Test] public void C1_CardRowPredicateMatchesInstitutionRowsOnly()
        {
            // 卡片判据的唯一来源（两组证据：verb 恰为 `InstitutionCard` **且** Key 非空）。它是"柜台把卡片组件
            // **无条件**挂到每个承载页"的前提 —— 非机构页的行绝不能命中，否则账户行 / 台账行会被当成机构卡。
            // B2：动词由 `Policy` 改名 `InstitutionCard`（政策页退役后再叫 Policy 会暗示一个不存在的目标），
            // 故本用例改为**读生产常量** —— 断言与实现同源，改名不会再漏改测试。
            string cardVerb = EconomyInstitutionCards.InstitutionRowVerb;
            Assert.That(cardVerb, Is.EqualTo("InstitutionCard"), "行种标记的稳定值（改名需同步本断言）");
            Assert.That(EconomyInstitutionCards.IsCardRow(
                new EconomyRow("机构摘要", null, cardVerb, key: "anchor:A")), Is.True, "机构行 = 卡片");
            Assert.That(EconomyInstitutionCards.IsCardRow(
                new EconomyRow("缴存", "缴存", "Treasury.Deposit", 25, "anchor:A")), Is.False, "账户页写动词不是卡片");
            Assert.That(EconomyInstitutionCards.IsCardRow(
                new EconomyRow("订单", "取消", "Cancel", 3)), Is.False, "台账页动词不是卡片");
            Assert.That(EconomyInstitutionCards.IsCardRow(
                new EconomyRow("陈旧记录", "配置", "Focus", key: "anchor:L")), Is.False,
                "旧记录行（Focus）是通用行，不是机构卡");
            Assert.That(EconomyInstitutionCards.IsCardRow(
                new EconomyRow("机构摘要", null, cardVerb)), Is.False, "缺 Key 的行无法定位机构 ⇒ 不是卡片");
            Assert.That(EconomyInstitutionCards.IsCardRow(new EconomyRow("说明文字")), Is.False, "说明行不是卡片");
        }

        // ─────────────────────── C-2：承载页 ↔ 页签号（就地切页）───────────────────────

        [Test] public void C2_HostedPositionOfIsTheInverseTranslation()
        {
            // B2：政策页（页签号 7 / 渲染器 Policy）已退役 ⇒ 一个柜台可以挂**任意多个 Query 页签**，
            // 但"位置 ↔ 页签号"的翻译纪律不变（C-1 的 P0 就是位置被当成页签号）。用 3/4/5 三页签覆盖。
            var view = ViewWith(new WorkspaceSecondaryTab(3, "账户", true), new WorkspaceSecondaryTab(4, "台账"),
                new WorkspaceSecondaryTab(5, "体检报告"));
            Assert.That(view.HostedTabIndex(2), Is.EqualTo(5), "位置 2 → 页签号 5");
            Assert.That(view.HostedPositionOf(5), Is.EqualTo(2), "页签号 5 → 位置 2");
            Assert.That(view.HostedPositionOf(3), Is.EqualTo(0));
            Assert.That(view.HostedPositionOf(1), Is.EqualTo(-1), "未承载的页签号返回 -1（调用方据此退回深链）");
            Assert.That(view.HostedPositionOf(-1), Is.EqualTo(-1));
            // 双向一致：位置 → 页签号 → 位置 必须回到原位 —— 这是"两个空间不混用"的最强断言
            //（C-1 的 P0 就是位置被当成页签号）。
            for (int i = 0; i < view.SecondaryTabs.Count; i++)
                Assert.That(view.HostedPositionOf(view.HostedTabIndex(i)), Is.EqualTo(i), "round-trip " + i.ToString());
        }

        [Test] public void C1_MountJudgmentIsTheUnionOfBothCapabilities()
        {
            var settlement = SettlementCounter();   // 账户（可写）+ 台账（只读）
            Assert.That(settlement.SecondaryTabs[0].HostsRowVerbs, Is.True, "账户可写 ⇒ 挂按钮");
            Assert.That(settlement.SecondaryTabs[1].HostsRowVerbs, Is.False, "台账只读 ⇒ 不挂按钮");

            var workshop = ViewWith(new WorkspaceSecondaryTab(0, "机构", false, true));
            var reports = ViewWith(new WorkspaceSecondaryTab(5, "体检报告", false));
            Assert.That(workshop.SecondaryTabs[0].HostsRowVerbs, Is.True, "导航页 ⇒ 挂按钮");
            Assert.That(reports.SecondaryTabs[0].HostsRowVerbs, Is.False, "报告只读 ⇒ 不挂按钮");
        }

        // ───────────────────── S2b：表格区段契约（Tables 永不为 null）─────────────────────

        [Test] public void S2b_TablesContractIsNeverNull()
        {
            // GATE-UI / GATE-ARCH（2026-10-11）一致的 P0：生产端曾对**非劳务站柜台**把 null 写进本字段
            // ⇒ 4/5 类柜台窗口打开即 NRE（`view.Tables.Count` 位于 DoWindowContents 的 try 之外，
            // 首帧异常逃逸、明细区此后每帧静默失败）。契约收紧在 SetTables 唯一入口。
            var view = ViewWith();
            Assert.That(view.Tables, Is.Not.Null, "非表格柜台必须拿到**空集**（不是 null）");
            Assert.That(view.Tables.Count, Is.EqualTo(0), "空集 = 既有行族渲染（零回归）");
            view.SetTables(null);
            Assert.That(view.Tables, Is.Not.Null, "SetTables(null) 必须归一为空集（唯一写入口）");
            Assert.That(view.Tables.Count, Is.EqualTo(0));
            view.SetTables(new List<WorkspaceTableSection>
            {
                new WorkspaceTableSection("workcatalogue", "目录",
                    new EconomyTable(new List<EconomyTableColumn>(), null, null), new List<EconomyRow>())
            });
            Assert.That(view.Tables.Count, Is.EqualTo(1));
            Assert.That(view.Tables[0].Key, Is.EqualTo("workcatalogue"), "区段按稳定 Key 寻址（会话态据此存取）");
        }
    }
}
