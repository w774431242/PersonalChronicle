using System.Collections.Generic;

namespace PersonalChronicle.Application.PawnEconomy
{
    /// <summary>
    /// 柜台工作台 KPI 的**呈现语调**（语义名）。到具体颜色的映射**唯一**在 UI 层
    /// （<c>EconomyUiKit.ToneColor</c>）—— 应用层不认识任何 Token 或 Color。
    /// </summary>
    public enum WorkspaceTone { Neutral, Accent, Alive, Warn, Dead, Muted, Dim }

    /// <summary>一项 KPI：值 + 标签 + 语调。只读快照的一部分，UI 不重算。</summary>
    public sealed class WorkspaceKpi
    {
        public readonly string Label, Value;
        public readonly WorkspaceTone Tone;
        public WorkspaceKpi(string label, string value, WorkspaceTone tone)
        { Label = label; Value = value; Tone = tone; }
    }

    /// <summary>
    /// 柜台工作台可执行的动词 —— **只列真正放行的写路径**。
    ///
    /// <para>为什么这么少：<c>GameComponent_PawnEconomy.Execute</c> 首行即
    /// <c>if (!LegacyFeesAllowed) return false;</c>，而该值恒为 <c>false</c> ⇒ 旧动词
    /// （撤下岗位 / 解约 / 上架 / 指派医生 / 预约体检 / 补发欠薪）**全部不可用**。
    /// 给玩家挂一个点了没反应的按钮违反既有 F4 纪律 ⇒ 工作台只挂这三项，且「账户」与「政策」
    /// 都走**深链**（打开聚合窗口的对应页），参数由该页既有实现构造 —— 工作台不复制任何命令参数语义。</para>
    ///
    /// <para>CHG-20261012-ECON-CONTRACTIZE：<c>VanillaJobsDown/Up</c>（"每份 ±"档位）**已整体移除** ——
    /// 合同件数改由「工作需求」表行点击 →「工作合同弹窗」**逐份设定**（用户裁决；
    /// 同一动作一次点击只有一个含义）。</para>
    ///
    /// <para>CHG-20261010-ECON-AGG-MIGRATE 批次 B-2：删除 <c>OpenAccounts</c> —— 账户页已由工资结算处
    /// **就地承载**（含操作人与国库戳等写上下文），同一窗口里不再需要第二个同名入口去聚合窗口看同一页。</para>
    /// </summary>
    public enum WorkspaceVerb { Reverify, WorkPrices }

    // CHG-20261012-ECON-CONTRACTIZE · B2：`WorkspaceHostedRenderer` 枚举**已删除** —— 它存在的唯一理由是
    // 区分"行族页"与"政策页内容层"（`PolicyPanel` 自定义排版）；政策页整体退役后只剩行族一种渲染，
    // 保留一个只有一个取值的枚举只是让呈现层多写一个恒真分支（LAND-010 的反面）。

    public sealed class WorkspaceAction
    {
        public readonly string Label;
        public readonly WorkspaceVerb Verb;
        public WorkspaceAction(string label, WorkspaceVerb verb) { Label = label; Verb = verb; }
    }

    /// <summary>柜台工作台可承载的**次级页**（CHG-20261010-ECON-AGG-MIGRATE 批次 B-1）。
    /// 索引是聚合窗口的行族页签号（<c>Dialog_PawnEconomy.LedgerTab</c> 等，其自述"调用方不得硬编码数字"），
    /// 标签在应用层就已本地化 —— 呈现层只按快照画按钮，**不出现任何机构类型或页签号的判断**（LAND-010）。</summary>
    public sealed class WorkspaceSecondaryTab
    {
        public readonly int Index;
        public readonly string Label;
        /// <summary>该页的行尾动词是否需要**宿主提供写上下文**（操作人 + 国库戳），并由宿主按与聚合窗口
        /// 相同的规则提交（<c>Treasury.*</c> → <c>ExecuteTreasuryCommand</c>；其余 → <c>Execute</c>）。
        /// 台账 / 报告为 <c>false</c>（只读）；账户为 <c>true</c>（B-2）。
        ///
        /// <para><b>承载页的动词契约</b>（C-1 起两类，B2 后 <c>Policy</c> 已退场）：<c>Reverify</c>（就地核验 ——
        /// <b>宿主自带、不需要任何能力位</b>；含该行的只读页不因此挂按钮，其核验入口是柜台行动区的
        /// 「重新核验」，见 <c>QueryWorkspace</c> 的 faulted 分支）、<c>Treasury.*</c>（需写上下文 ⇒ 本标志）、
        /// <c>Focus</c>（导航 ⇒ <see cref="NeedsNavigation"/>）。
        /// 越界动词由写通道 fail-closed 拒绝并如实回报，不会静默无效。</para></summary>
        public readonly bool NeedsCommandHost;
        /// <summary>该页的动词含**导航**（<c>Focus</c> 重定承载页焦点），由宿主按自己的形态执行
        /// （柜台：就地重定焦点；聚合窗口：切页签并聚焦）。机构列表为 <c>true</c>（C-1）。</summary>
        public readonly bool NeedsNavigation;
        public WorkspaceSecondaryTab(int index, string label, bool needsCommandHost = false, bool needsNavigation = false)
        { Index = index; Label = label; NeedsCommandHost = needsCommandHost; NeedsNavigation = needsNavigation; }

        /// <summary>该页的行动词是否需要宿主参与 —— **挂按钮的唯一判据**（两个能力位的或）。
        /// 为什么不直接用 `<c>NeedsCommandHost</c>`：C-1 的机构列表既不需要写上下文（没有缴存提取），
        /// 也不该因为这一点被当成"只读页"而**静默丢掉**「政策」「配置」按钮 ——
        /// 那正是"能力位与挂载判据不同源"会造成的劣化。</summary>
        public bool HostsRowVerbs => NeedsCommandHost || NeedsNavigation;
    }

    /// <summary>
    /// 表格**承载区段**（CHG-20261011-WORKCAT · S2b）：一张表 + 它自己的行集 + 切换条标签。
    ///
    /// <para><b>为什么按区段承载而不是"一页一张表"</b>：劳务站同屏有**两种列结构**
    /// （工作目录 = 工种/订单/待办/派发…；工作合同 = 承接人/工种/完成/已付…）。
    /// 强行合并成一列集必然有一种语义错位（"别人的列头，我的数据"）；分为多个区段后
    /// 每张表仍有**自己的列集与筛选/搜索面**，而渲染、行族、会话态机制**完全共用同一份**
    /// （禁止第二套表格实现 —— AI-001/GATE-ARCH 单实现裁决）。</para>
    ///
    /// <para><b>Key 是会话态的寻址键</b>：排序/筛选/搜索/列宽按 Key 存取（不随本地化、列序变化），
    /// 且**绝不**用对象引用判断"是否同一张表"——读模型每次快照都新建实例，引用比较恒假曾是
    /// S2a 的 P0（GATE-UI/GATE-ARCH 2026-10-11 一致 BLOCKED：排序点击后 1 帧被复位）。</para>
    /// </summary>
    public sealed class WorkspaceTableSection
    {
        public readonly string Key;
        public readonly string Label;
        public readonly EconomyTable Table;
        /// <summary>该表的行集。**只读视图**（`AsReadOnly`）—— 来源 `List` 可能与 <see cref="WorkspaceView.Rows"/>
        /// 共享同一实例（组装后零写入，但收紧在这里可让"事后篡改"在类型层面不可能）。</summary>
        public readonly IReadOnlyList<EconomyRow> Rows;
        public WorkspaceTableSection(string key, string label, EconomyTable table, List<EconomyRow> rows)
        { Key = key; Label = label; Table = table; Rows = rows != null ? rows.AsReadOnly() : (IReadOnlyList<EconomyRow>)System.Array.Empty<EconomyRow>(); }
    }

    /// <summary>
    /// 柜台工作台的一次**完整读快照**（唯一读入口 <c>GameComponent_PawnEconomy.QueryWorkspace</c> 产出）。
    /// UI 只按它排版：机构类型差异（显示哪些 KPI / 哪些行动 / 哪些明细行）**全部在应用层组装完成**，
    /// 呈现层没有 <c>if (kind == …)</c> 特判（LAND-010）。
    /// </summary>
    public sealed class WorkspaceView
    {
        public readonly int Revision;
        public readonly string Title;      // 建筑 LabelCap
        public readonly string KindLabel;  // 机构类型标签（与 Def label 同词）
        /// <summary>制度阶段（已本地化）。CHG-20261012 · B2：随机构政策退役一并删除
        /// <c>OpenText</c>（营业/停业）· <c>HoursText</c>（营业时段）· <c>OpenTone</c> ——
        /// 三者都是政策读数，政策净删后已无权威来源（保留字段只会逼组装方发明数据，M5）。</summary>
        public readonly string PhaseText;
        public readonly WorkspaceTone PhaseTone;
        public readonly IReadOnlyList<WorkspaceKpi> Kpis;
        public readonly IReadOnlyList<WorkspaceAction> Actions;
        /// <summary>行动区右侧的**只读**说明（<c>null</c> = 不显示）。CHG-20261010-ECON-LABOR-UI2：
        /// 把"每份 N 件"这类参数当前值放在调节按钮旁边 —— 玩家点 ± 时立即看到变化（不必往下读明细行）。
        /// 组装点在构造后赋值（不加构造参数 ⇒ 既有调用点零改动）。</summary>
        public string ActionNote;
        /// <summary>明细区行集（复用既有行族视图类型；空集时行族自走空态）。</summary>
        public readonly EconomyView Rows;
        /// <summary>**数据表格承载区段**（CHG-20261011-WORKCAT · S2a 单表 → S2b 区段化）：
        /// 非空 ⇒ 本柜台明细区按表格渲染（表切换条 + 筛选条 + 搜索框 + 密度切换 + 表头 + 列化行）。
        /// 行集在各区段内 —— 表格只声明列与筛选/搜索面，行族继续负责测量/滚动/分页/空态（单一实现）。
        /// 空集 = 既有行族渲染（零回归）。
        ///
        /// <para><b>契约：本属性永不为 null</b>（GATE-UI/GATE-ARCH 2026-10-11 的 P0：生产端曾对
        /// 非劳务站柜台写入 null，4/5 类柜台窗口打开即 NRE —— 单属性化 + <see cref="SetTables"/> 归一是
        /// 在**类型层面**消除该类接缝，窗口侧因此不需要任何空判）。</para></summary>
        public IReadOnlyList<WorkspaceTableSection> Tables { get; private set; } = System.Array.Empty<WorkspaceTableSection>();

        /// <summary>设置表格区段（**null 归一为空集** —— 契约收紧的唯一入口；读模型组装方只走这里）。</summary>
        public void SetTables(IReadOnlyList<WorkspaceTableSection> sections)
        { Tables = sections ?? System.Array.Empty<WorkspaceTableSection>(); }
        /// <summary>该柜台**可承载的次级页**（批次 B-1：工资结算处 → 台账；体检中心 → 报告）。
        /// 空集 ⇒ 工作台不显示切换条，明细区就是本柜台业务行（既有行为不变）。
        /// 组装点在构造后赋值；呈现层据此决定"是否需要切换条"，不做类型判断。</summary>
        public IReadOnlyList<WorkspaceSecondaryTab> SecondaryTabs = System.Array.Empty<WorkspaceSecondaryTab>();

        /// <summary>按**切换条位置**取承载页的**聚合页签号**（越界返回 <c>-1</c>）。
        /// 这是"位置 → 页签号"的**唯一翻译点**：纯函数、有离线断言
        ///（<c>Tests/WorkspaceSecondaryTabTests.cs</c>）。
        ///
        /// <para><b>为什么必须有它</b>：位置是 UI 排版序号（0,1,…），页签号是读入口的语义参数
        ///（<c>AccountsTab=3 / LedgerTab=4 / ReportsTab=5</c>）。两者一旦混用，「账户」会查到**机构页**，
        /// 且**编译与单测都不会报错** —— GATE-ARCH（2026-10-10）抓到的 P0 正是这一处
        ///（`EnsureHostedView` 曾把位置直接当页签号传 <c>Query</c>）。</para></summary>
        public int HostedTabIndex(int position)
            => position >= 0 && position < SecondaryTabs.Count ? SecondaryTabs[position].Index : -1;

        /// <summary>按**聚合页签号**取承载页在切换条里的位置（未承载返回 <c>-1</c>）——
        /// <see cref="HostedTabIndex"/> 的**逆向翻译**（导航动词 `Policy` 需要它来"就地切到政策页"）。
        /// 同样是纯函数并有离线断言：位置与页签号这两个空间一旦混用，症状是"切错页"且编译与单测都不会报错
        ///（GATE-ARCH 2026-10-10 的 P0 教训）。</summary>
        public int HostedPositionOf(int tabIndex)
        {
            for (int i = 0; i < SecondaryTabs.Count; i++)
                if (SecondaryTabs[i].Index == tabIndex) return i;
            return -1;
        }
        public WorkspaceView(int revision, string title, string kindLabel, string phaseText,
            WorkspaceTone phaseTone, List<WorkspaceKpi> kpis, List<WorkspaceAction> actions, EconomyView rows)
        {
            Revision = revision; Title = title; KindLabel = kindLabel;
            PhaseText = phaseText; PhaseTone = phaseTone;
            Kpis = kpis.AsReadOnly(); Actions = actions.AsReadOnly(); Rows = rows;
        }
    }
}
