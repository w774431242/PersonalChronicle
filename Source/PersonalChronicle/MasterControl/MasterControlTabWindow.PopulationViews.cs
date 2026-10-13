using PersonalChronicle.MasterControl.Population;
using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Application.SystemCenters.Dev;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl
{
    /// <summary>
    /// Partial of <see cref="MasterControlTabWindow"/> ——人员中心的**视图 tab 条**与第 4 个视图
    /// 「分档与契约」。
    ///
    /// <para><b>为什么加 tab 条</b>：预览是 4 个视图分页（<c>#navseg</c>：概览 / 档案总览 /
    /// 人物档案 / 分档与契约），而旧实机把「概览 + 档案馆承载 + 契约清单」**同屏全塞**在一个
    /// 滚动页里 —— 用户报「缺少 tab」与「多出旧内容」都源于此。现在同一时刻只画当前视图。</para>
    ///
    /// <para><b>可用性判据是数据**不是**中心名</b>（LAND-010）：概览视图存在 ⇔
    /// <c>PopulationOverview</c> 非空；档案视图存在 ⇔ <c>ArchiveView</c> 非空；
    /// 「分档与契约」恒可用（内容全部来自快照与窗口既有字段，不依赖任何中心专属数据）。</para>
    /// </summary>
    public sealed partial class MasterControlTabWindow
    {
        private const int PopulationViewCount = 4;

        /// <summary>
        /// 视图 tab 条**本体高**（预览 <c>#navseg button</c> 的引擎等价）；**兜底档**（内容区独立行）与
        /// 页头档共用这个符号做「行高来源」。页头档另见 <see cref="PopulationViewHeaderTabHeight"/>。
        /// </summary>
        private const float PopulationViewTabHeight = 26f;

        /// <summary>
        /// 页头**嵌入档**的 tab 高（主理人核定 <b>24</b>）：与页头右侧按钮组（高 26）**同一行、垂直居中**
        /// 对齐 ⇒ 视觉基线齐平，而页头总高 <see cref="UITheme.HeaderHeight"/>（56）**不增高**。
        /// </summary>
        private const float PopulationViewHeaderTabHeight = 24f;

        /// <summary>页头按钮行在页头内的起始偏移（与 <c>Shell.cs DrawHeaderButtons</c> 的 <c>y = rect.y + 14f</c> 同源）。</summary>
        private const float PopulationViewHeaderRowY = 14f;

        /// <summary>页头按钮组高（与 <c>DrawHeaderButtons</c> 的 <c>btnHeight = 26f</c> 同源；仅用于垂直居中）。</summary>
        private const float PopulationViewHeaderRowHeight = 26f;

        /// <summary>嵌在页头时，tab 条与「标题右缘 / 按钮组左缘」之间的呼吸间距。</summary>
        private const float PopulationViewHeaderTabGap = 10f;

        /// <summary>单枚 tab 最大宽（预览 <c>#navseg</c> 胶囊宽上界；条不满预算时右对齐、余量留白）。</summary>
        private const float PopulationViewTabMaxWidth = 150f;

        /// <summary>单枚 tab 最小宽：低于此值连一个字都放不下 ⇒ 落到下一档降级。</summary>
        private const float PopulationViewTabMinWidth = 22f;

        /// <summary>相邻两枚 tab 的间距。</summary>
        private const float PopulationViewTabGap = 4f;

        // ---- 标签降级档位（绘制规范铁律 4：顺序固定，不得自创）----
        /// <summary>D0：实测放得下 ⇒ 完整标签。</summary>
        private const int TabLabelFull = 0;
        /// <summary>D1：放不下完整标签 ⇒ <see cref="UIComponents.TruncateToWidth"/> 省略（配悬停给全文）。</summary>
        private const int TabLabelTruncated = 1;
        /// <summary>D2：连省略档都放不下 ⇒ 只留首字（图标化）。</summary>
        private const int TabLabelInitial = 2;
        /// <summary>D6：连首字都放不下 ⇒ 兜底回落内容区独立行（占高同源恢复，见 <c>Detail.cs</c>）。</summary>
        private const int TabLabelContentRow = 3;

        /// <summary>视图提示行高（如「人物档案」tab 的进入方式说明）。</summary>
        private const float PopulationViewHintHeight = 18f;

        /// <summary>契约视图的单行高（键值同行，长值截断 + 悬停给全文）。</summary>
        private const float ContractRowHeight = 18f;

        private static readonly string[] PopulationViewTabKeys =
        {
            "PersonalChronicle.UI.MasterControl.PopulationView.Overview",
            "PersonalChronicle.UI.MasterControl.PopulationView.Archive",
            "PersonalChronicle.UI.MasterControl.PopulationView.Pawn",
            "PersonalChronicle.UI.MasterControl.PopulationView.Contract"
        };

        /// <summary>契约视图的 6 个键值行（构建期拼好，绘制期零拼接）。</summary>
        private readonly List<string> contractLines = new List<string>(6);
        private string contractCacheId;

        /// <summary>
        /// 内容区 tab 行是否启用 —— <b>兜底档</b>标志（页头宽度预算连「首字档」都放不下时置 true）。
        ///
        /// <para><b>同源铁律（LAND-002）</b>：本标志由页头每帧的**那一次**布局决策写入
        /// （<see cref="DrawHeaderPopulationViewTabs"/>），随后 <c>ComputeDetailHeight</c>（计高）与
        /// <c>DrawDetailContent</c>（绘制）**只读它**（经 <c>DetailHeaderTabsHeight</c>）⇒ 计高 ≡ 绘制。</para>
        /// </summary>
        private bool PopulationViewTabsInContentArea;

        /// <summary>
        /// 视图 tab 条是否由**①「人员规模与构成」面板的标题条**托管（2026-09-17 用户需求：
        /// 「这部分 tab 嵌入到『人员规模』面板内，节省组件空间」）。
        ///
        /// <para>判据：当前视图 = 概览（<c>populationView == 0</c>）—— 概览视图里 ① 面板必然显示，
        /// 其标题条（高 26）右侧本来就空着 ⇒ tab（高 24）嵌进去**零新增高度**，页头那条水平带
        /// 完全归还给标题 / 副标题（页头再无「标题 vs tab vs 按钮」三方抢宽）。</para>
        ///
        /// <para>⚠ 非概览视图（档案总览 / 人物档案 / 分档与契约）**没有 ① 面板** ⇒ 仍由页头托管。
        /// 否则切走后 tab 条随面板消失 ⇒ 无法切回（与「嵌入档详情无返回键」同类的卡死）。</para>
        ///
        /// <para><b>同源铁律（LAND-002）</b>：与 <see cref="PopulationViewTabsInContentArea"/> 同法 ——
        /// 由页头每帧的那一次布局决策写入，绘制侧只读。</para>
        /// </summary>
        private bool PopulationViewTabsInScalePanel;

        /// <summary>
        /// **4 枚视图 tab 嵌入窗口页头**（用户需求「这一排 tab 的空间占位太大，建议嵌入式，避免浪费布局空间」）。
        ///
        /// <para>位置：**标题右侧、右侧按钮组左侧**，与按钮组同行垂直居中；占的是页头原本就存在的空白
        /// 水平带 ⇒ 页头总高不变、内容区不再为 tab 留行。</para>
        ///
        /// <para><b>宽度预算</b> = <c>标题实测右缘 → 按钮组实测左缘</c>（见方法体），左界按标题
        /// **实测字宽**推进（不是标题矩形的名义宽）⇒ 任何窗口宽度下都不与标题 / 按钮重叠（UI-009）。</para>
        ///
        /// <para><b>降级阶梯（绘制规范铁律 4，禁止硬挤 / 静默截断 / 画到容器外）</b>：
        /// D0 完整标签 → D1 <see cref="UIComponents.TruncateToWidth"/> 省略 →
        /// D2 只留首字（图标化）→ <b>D6 兜底</b>回落内容区独立行。档位判据 = 单枚可用宽 vs
        /// **该语言下最宽标签的实测宽**（不写死像素）。</para>
        /// </summary>
        /// <summary>
        /// **tab 行托管判据**（2026-09-17）：本帧整条视图 tab 行（4 枚视图 tab + dev「测试数据」开关）
        /// 是否由 ①「人员规模与构成」面板的标题条托管。
        ///
        /// <para><b>唯一判据来源</b>：<see cref="DrawHeaderPopulationViewTabs"/>（页头是否画视图 tab）与
        /// <c>Shell.cs → DrawHeaderButtons</c>（页头是否画 dev 开关）**都调本方法** —— 二者必须同进同退，
        /// 否则会出现「同一行被画两遍」或「一行都不画（无切换入口）」。</para>
        ///
        /// <para>条件：中心详情页 + 有业务视图 + 当前视图 = 概览（<c>populationView == 0</c>；
        /// 只有概览视图才有 ① 面板）+ 面板标题条**确实放得下**这条行（宽度预算式与
        /// <c>Population.cs → DrawScalePanelViewTabs</c> 同源，LAND-002）。</para>
        /// </summary>
        private bool PopulationViewTabsHostedByScalePanel(float width)
        {
            if (view != ViewKind.CenterDetail
                || Mathf.Clamp(populationView, 0, PopulationViewCount - 1) != 0)
            {
                return false;
            }
            SystemCenterDetailSnapshot detail = cachedDetail;
            if (detail == null || detail.IsEmpty || UsesAppShell(detail))
            {
                return false;
            }
            PopulationOverviewSnapshot population = PopulationForDisplay(detail);
            bool hasPopulation = population != null && !population.IsEmpty;
            ArchiveViewSnapshot archive = ArchiveViewForDisplay(detail);
            bool hasArchive = archive != null && !archive.IsEmpty;
            if (!hasPopulation && !hasArchive)
            {
                return false;
            }
            float gaps = PopulationViewTabGap * (PopulationViewCount - 1);
            float budget = Mathf.Min(width * 0.62f,
                PopulationViewTabMaxWidth * PopulationViewCount + gaps);
            return budget - gaps >= PopulationViewTabMinWidth * PopulationViewCount;
        }

        private void DrawHeaderPopulationViewTabs(Rect headerRect, float reserved, float titleWidth)
        {
            PopulationViewTabsInContentArea = false;
            PopulationViewTabsInScalePanel = false;
            if (view != ViewKind.CenterDetail)
            {
                return;
            }
            SystemCenterDetailSnapshot detail = cachedDetail;
            if (detail == null || detail.IsEmpty || UsesAppShell(detail))
            {
                // 业务视图专页（cockpit 型）整页适配一屏，不承载 4 视图 tab
                //（与 DrawDetailContent 的 UsesAppShell 分支同判据 ⇒ 不会出现「页头有 tab、内容无对应视图」）。
                return;
            }
            PopulationOverviewSnapshot population = PopulationForDisplay(detail);
            bool hasPopulation = population != null && !population.IsEmpty;
            ArchiveViewSnapshot archive = ArchiveViewForDisplay(detail);
            bool hasArchive = archive != null && !archive.IsEmpty;
            if (!hasPopulation && !hasArchive)
            {
                return;
            }

            // ══ 2026-09-17（用户需求「这部分 tab 嵌入到『人员规模』面板内，节省组件空间」）══
            // 概览视图：**整条 tab 行**（4 枚视图 tab + dev「测试数据」开关）交给 ①「人员规模与构成」
            // 面板的**标题条**托管（`Population.cs` → DrawScalePanelViewTabs）：那里右侧本就空着、
            // 标题条高 26 > tab 高 24 ⇒ **零新增高度**，页头这条水平带完全归还标题 / 副标题。
            // 判据走**唯一来源** <see cref="PopulationViewTabsHostedByScalePanel"/>（Shell.cs 的 dev
            // 开关同用）⇒ 不可能出现「两处各画一半」或「两处都不画」（LAND-002 同源）。
            if (PopulationViewTabsHostedByScalePanel(headerRect.width))
            {
                PopulationViewTabsInScalePanel = true;
                return;
            }

            // 左界 = 标题**实测**占宽（截到标题矩形宽上限，与绘制侧同源）；右界 = 按钮组实测左缘。
            float titleActual = Mathf.Min(
                MeasureWidth(Translate("PersonalChronicle.UI.MasterControl.Title"), GameFont.Medium),
                titleWidth);
            float left = headerRect.x + 2f + titleActual + PopulationViewHeaderTabGap;
            float right = headerRect.xMax - reserved - PopulationViewHeaderTabGap;
            float budget = right - left;
            float gaps = PopulationViewTabGap * (PopulationViewCount - 1);
            float tabWidth = (budget - gaps) / PopulationViewCount;

            int level;
            if (tabWidth >= WidestTabLabelWidth() + 8f)
            {
                level = TabLabelFull;                          // D0：该语言下最宽标签都放得下
            }
            else if (tabWidth >= PopulationViewTabMinWidth * 2f)
            {
                level = TabLabelTruncated;                     // D1：省略（TruncateToWidth 自带「…」）
            }
            else if (tabWidth >= PopulationViewTabMinWidth)
            {
                level = TabLabelInitial;                       // D2：只留首字
            }
            else
            {
                level = TabLabelContentRow;                    // D6：兜底（预算已放不下任何可读标签）
            }
            if (level == TabLabelContentRow)
            {
                PopulationViewTabsInContentArea = true;
                return;
            }

            // 右对齐：条宽取 min(预算, 4×最大宽 + 间距) —— 预算再宽也不会把胶囊拉成空条。
            float width = Mathf.Min(budget,
                PopulationViewTabMaxWidth * PopulationViewCount + gaps);
            float y = headerRect.y + PopulationViewHeaderRowY
                + (PopulationViewHeaderRowHeight - PopulationViewHeaderTabHeight) * 0.5f;
            Rect bar = new Rect(right - width, y, width, PopulationViewHeaderTabHeight);
            DrawPopulationViewTabCapsules(bar, (width - gaps) / PopulationViewCount,
                hasPopulation, hasArchive, level);
        }

        /// <summary>
        /// 该语言下**最宽**视图标签的实测宽（构建期/绘制期均可调用；语言切换后自动跟随）。
        /// 用于把「D0 完整标签」的判据从写死像素改为**量出来的宽度**（LAND-007：不手写魔数）。
        /// </summary>
        private static float WidestTabLabelWidth()
        {
            float widest = 0f;
            for (int i = 0; i < PopulationViewTabKeys.Length; i++)
            {
                // ⚠ 2026-09-19（CHG-20260919-ECON-UI-010）：原为**绘制期对 4 个键逐个翻译 + 分配**
                // （审计登记点）；改走本类助手的语言级缓存。
                float width = MeasureWidth(Translate(PopulationViewTabKeys[i]), GameFont.Tiny);
                if (width > widest)
                {
                    widest = width;
                }
            }
            return widest;
        }

        /// <summary>
        /// **兜底档**：4 枚视图 tab 回落为内容区顶部独立行（旧版式）。几何与旧实现逐字一致
        /// （右对齐、单枚 72~150），占高由 <c>DetailHeaderTabsHeight</c> 同源计入。
        /// </summary>
        private float DrawPopulationViewTabs(Rect rect, float y, bool hasPopulation, bool hasArchive)
        {
            float tabWidth = Mathf.Max(72f, Mathf.Min(PopulationViewTabMaxWidth,
                (rect.width - 12f) / PopulationViewCount));
            float totalWidth = tabWidth * PopulationViewCount
                + PopulationViewTabGap * (PopulationViewCount - 1);
            Rect bar = new Rect(Mathf.Max(rect.x, rect.xMax - totalWidth), y, totalWidth,
                PopulationViewTabHeight);
            DrawPopulationViewTabCapsules(bar, tabWidth, hasPopulation, hasArchive, TabLabelFull);
            return bar.yMax + 6f;
        }

        /// <summary>
        /// 4 枚胶囊的**公共绘制**：几何由调用方给定（<paramref name="bar"/> = 整条矩形，
        /// <paramref name="tabWidth"/> = 单枚宽，<paramref name="level"/> = 标签降级档）。
        ///
        /// <para><b>不变项（主理人核定，逐字保留）</b>：不可用项灰显且点击无效（<b>不隐藏</b> —— 保持入口
        /// 可预期）、可用性判据 <c>i==0 概览 / 1,2 档案 / 3 恒可用</c>、<c>Widgets.ButtonInvisible</c>
        /// 命中检测、点击切 <c>populationView</c>、选中态底部 2px 强调条、悬停高亮。</para>
        /// </summary>
        private void DrawPopulationViewTabCapsules(
            Rect bar, float tabWidth, bool hasPopulation, bool hasArchive, int level)
        {
            int current = Mathf.Clamp(populationView, 0, PopulationViewCount - 1);
            for (int i = 0; i < PopulationViewCount; i++)
            {
                Rect tab = new Rect(bar.x + i * (tabWidth + PopulationViewTabGap), bar.y,
                    tabWidth, bar.height);
                // 0 概览要看概览数据；1/2 档案与人物都靠承载区；3 契约恒可用。
                bool enabled = i == 3
                    || (i == 0 ? hasPopulation : hasArchive);
                bool on = i == current;
                UIComponents.Panel(tab, on ? UITheme.PanelRaised : UITheme.Card);
                UIComponents.Border(tab, on ? UITheme.Accent : UITheme.BorderSoft);
                if (on)
                {
                    Widgets.DrawBoxSolid(new Rect(tab.x, tab.yMax - 2f, tab.width, 2f), UITheme.Accent);
                }
                else if (enabled && Mouse.IsOver(tab))
                {
                    Widgets.DrawHighlight(tab);
                }
                string label = TabLabelOf(i, tabWidth, level);
                UIComponents.Label(tab, label, GameFont.Tiny,
                    enabled ? (on ? UITheme.Accent : UITheme.Text) : UITheme.Dim,
                    TextAnchor.MiddleCenter);
                // 省略档 / 首字档下悬停给完整标签（LAND-008：截断必须可读回全文）。
                if (level != TabLabelFull && Mouse.IsOver(tab))
                {
                    TooltipHandler.TipRegion(tab, Translate(PopulationViewTabKeys[i]));
                }
                if (enabled && !on && Widgets.ButtonInvisible(tab))
                {
                    populationView = i;
                }
            }
        }

        /// <summary>按降级档位取 tab 文本（D0 原样 / D1 省略 / D2 首字）。</summary>
        private static string TabLabelOf(int index, float tabWidth, int level)
        {
            string label = Translate(PopulationViewTabKeys[index]);
            if (level == TabLabelFull)
            {
                return label;      // 档位已保证放得下 ⇒ 不做截断计算（也杜绝出现「…」）
            }
            if (level == TabLabelTruncated)
            {
                return UIComponents.TruncateToWidth(label, Mathf.Max(0f, tabWidth - 8f), GameFont.Tiny);
            }
            // D2：只留首字（顺序语义不变；CJK 单字仍有辨识度，拉丁文退化为首字母）。
            return string.IsNullOrEmpty(label) ? label : label.Substring(0, 1);
        }

        // ---------------------------------------------------------------- ④ 分档与契约

        /// <summary>契约块高（行数 × 行高；与 <see cref="DrawContractBlock"/> **同源**）。</summary>
        private static float ContractBlockHeight(SystemCenterDetailSnapshot detail)
        {
            return ContractRowHeight * (1f + 1f + 1f + 6f + 1f + 3f) + 8f;
        }

        /// <summary>
        /// ④「分档与契约」：中心的数据契约与**接入现状**如实登记 ——
        /// 中心 ID / 数据供给 / 职责 / 提供 / 消费 / 依赖 / 发出 / 挂点 / 未落地项。
        /// 全部取自快照与窗口既有字段（不编造指标，LAND-009）。
        /// </summary>
        private float DrawContractBlock(Rect viewRect, float y, SystemCenterDetailSnapshot detail)
        {
            EnsureContractLines(detail);
            // ⚠ 2026-09-19（CHG-20260919-ECON-UI-010）：本块原先**每帧翻译 9 条并各分配一次字符串**
            // （审计登记的重灾区）。改走本类的 `Translate` 助手 ⇒ 内部走 `UIComponents.Loc` 语言级缓存。
            UIComponents.Label(new Rect(viewRect.x, y, viewRect.width, 22f),
                Translate("PersonalChronicle.UI.MasterControl.Contract.Title"),
                GameFont.Small, UITheme.Text, TextAnchor.MiddleLeft);
            y += 22f;

            string[] labels =
            {
                Translate("PersonalChronicle.UI.MasterControl.Contract.Id"),
                Translate("PersonalChronicle.UI.MasterControl.Contract.Sources"),
                Translate("PersonalChronicle.UI.MasterControl.Contract.Responsible"),
                Translate("PersonalChronicle.UI.MasterControl.Contract.Provides"),
                Translate("PersonalChronicle.UI.MasterControl.Contract.Consumes"),
                Translate("PersonalChronicle.UI.MasterControl.Contract.DependsOn"),
                Translate("PersonalChronicle.UI.MasterControl.Contract.Emits"),
                Translate("PersonalChronicle.UI.MasterControl.Contract.Hooks")
            };
            for (int i = 0; i < labels.Length; i++)
            {
                y = DrawContractRow(viewRect, y, labels[i],
                    i < contractLines.Count ? contractLines[i] : DashText());
            }

            UIComponents.Label(new Rect(viewRect.x, y + 2f, viewRect.width, 20f),
                Translate("PersonalChronicle.UI.MasterControl.Contract.Gaps"),
                GameFont.Small, UITheme.Muted, TextAnchor.MiddleLeft);
            y += 22f;
            for (int i = 1; i <= 3; i++)
            {
                // ⚠ `GapLabel` **带参数**（i 逐行不同）⇒ 有意**不走缓存**，保留绘制期翻译；
                // `Gap{i}` 是**动态键但无参** ⇒ 可安全走缓存。
                // ⚠ 必须写成 `Translate("…Gap" + i)` 而**不能**写成
                // `UIComponents.Loc("…Gap") + i` —— 后者会把**不含数字的键**塞进缓存，
                // 那是个不存在的键，取回的是键名本身（静默错值）。
                y = DrawContractRow(viewRect, y,
                    "PersonalChronicle.UI.MasterControl.Contract.GapLabel".Translate(
                        (NamedArgument)i).ToString(),
                    Translate("PersonalChronicle.UI.MasterControl.Contract.Gap" + i));
            }
            return y + 6f;
        }

        /// <summary>键值行：键固定宽 + 值截断（截断时悬停给全文，LAND-008）。</summary>
        private static float DrawContractRow(Rect rect, float y, string label, string value)
        {
            float labelWidth = Mathf.Min(120f, rect.width * 0.22f);
            float valueWidth = Mathf.Max(20f, rect.width - labelWidth - 6f);
            Rect line = new Rect(rect.x, y, rect.width, ContractRowHeight);
            UIComponents.Label(new Rect(line.x, line.y, labelWidth, line.height),
                UIComponents.TruncateToWidth(label, labelWidth - 2f, GameFont.Tiny),
                GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
            string shown = UIComponents.TruncateToWidth(value, valueWidth, GameFont.Tiny);
            UIComponents.Label(new Rect(line.x + labelWidth, line.y, valueWidth, line.height),
                shown, GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);
            if (Mouse.IsOver(line) && !string.Equals(shown, value, System.StringComparison.Ordinal))
            {
                TooltipHandler.TipRegion(line, value);
            }
            return y + ContractRowHeight;
        }

        /// <summary>契约 6 行文本按中心缓存（切换中心才重建；绘制期零拼接）。</summary>
        private void EnsureContractLines(SystemCenterDetailSnapshot detail)
        {
            if (string.Equals(contractCacheId, selectedCenterDefName, System.StringComparison.Ordinal)
                && contractLines.Count == 6)
            {
                return;
            }
            contractCacheId = selectedCenterDefName;
            contractLines.Clear();
            contractLines.Add(selectedCenterDefName ?? "—");
            contractLines.Add("PersonalChronicle.UI.MasterControl.Contract.DataSource".Translate(
                (NamedArgument)(detail != null && PopulationForDisplay(detail) != null ? "●" : "—"),
                (NamedArgument)(detail != null && ArchiveViewForDisplay(detail) != null ? "●" : "—")).ToString());
            contractLines.Add(JoinList(detail != null ? detail.Responsible : null));
            contractLines.Add(JoinList(detail != null ? detail.Provides : null));
            contractLines.Add(JoinList(detail != null ? detail.Consumes : null));
            contractLines.Add(JoinList(detail != null ? detail.DependsOn : null));
            contractLines.Add(JoinList(detail != null ? detail.Emits : null));
            contractLines.Add(JoinList(detail != null ? detail.Hooks : null));
            // 8 行（Id/Sources/职责/提供/消费/依赖/发出/挂点）——与绘制侧 labels 等长。
            if (contractLines.Count > 8)
            {
                contractLines.RemoveRange(8, contractLines.Count - 8);
            }
        }

        private static string JoinList(List<string> items)
        {
            if (items == null || items.Count == 0)
            {
                return DashText();
            }
            return string.Join(" · ", items.ToArray());
        }

        private static string DashText()
        {
            return "—";
        }

        // ---------------------------------------------------------------- 开发者测试数据
        //
        // 【仅 Prefs.DevMode】「测试数据」按钮（**页头右侧按钮组**内，见 `Shell.cs DrawHeaderButtons`）：
        // 用 `DevPopulationFixtures` 的**随机结构完整快照**整体替换概览显示对象 —— 空档 / 新档也能检验
        // 全部前端组件（环形 / 面积图 / 柱图 / 卡片 / chips / 榜单）的满值形态。
        //
        // 【2026-09-16 合并】旧 tab 条右端有两个开发者按钮（「测试数据」+「重随机」）。按用户需求
        // 「移除非正式内容的 tab / 优化测试数据，它只有一个固定数据而不是随机数据」：
        //   ① 二者**合并为一个页头按钮** —— **每次「关→开」都换一批新的随机数据**（「重随机」不再独立占位）；
        //   ② 快照内部全部数值改为 rng 驱动且**逐项自洽**（见 DevPopulationFixtures）。
        // 开→关立即恢复真实数据；快照不入存档、不触碰 Provider 路径。

        private bool devPreview;

        /// <summary>
        /// 测试数据种子。<b>不再写死初值</b>：每次「关→开」都由 <see cref="NextDevSeed"/> 取新值
        /// （⇒ 第一次开就与上次会话不同，之后每次开启都换一批）。仅决定
        /// <see cref="DevPopulationFixtures.BuildRandom"/> 的随机序列（同 seed 同结果，可复现截图）。
        /// </summary>
        private int devSeed;

        private PopulationOverviewSnapshot devSnapshot;
        private int devSnapshotSeed = int.MinValue;

        /// <summary>dev 预览的档案馆承载外壳快照（与 <see cref="devSnapshot"/> 同生命周期；关→开重建）。</summary>
        private ArchiveViewSnapshot devArchiveSnapshot;
        private int devArchiveSeed = int.MinValue;

        /// <summary>dev 预览是否生效（非 DevMode 恒 false）。</summary>
        private bool DevPreviewActive
        {
            get { return devPreview && Prefs.DevMode; }
        }

        /// <summary>取当前应显示的概览快照：dev 预览优先，否则真实快照（单一取值点，两个调用处共用）。</summary>
        private PopulationOverviewSnapshot PopulationForDisplay(SystemCenterDetailSnapshot detail)
        {
            if (DevPreviewActive)
            {
                if (devSnapshot == null || devSnapshotSeed != devSeed)
                {
                    devSnapshot = DevPopulationFixtures.BuildRandom(devSeed);
                    devSnapshotSeed = devSeed;
                }
                return devSnapshot;
            }
            return detail != null ? detail.PopulationOverview : null;
        }

        /// <summary>
        /// 取当前应显示的档案馆承载外壳快照：dev 预览优先，否则真实快照（单一取值点，三处调用共用）。
        /// <para>与 <see cref="PopulationForDisplay"/> 对称：让 dev 开关对「控制总览」「人物档案」两视图同样生效——
        /// 未注册档案馆承载的中心经外壳快照即可渲染承载区（hasArchive 判据为真），内部内容仍由
        /// <c>PersonnelArchiveHost</c> 实时读 <c>IArchiveService</c> 呈现真实数据；关→开重建快照。</para>
        /// </summary>
        private ArchiveViewSnapshot ArchiveViewForDisplay(SystemCenterDetailSnapshot detail)
        {
            if (DevPreviewActive)
            {
                if (devArchiveSnapshot == null || devArchiveSeed != devSeed)
                {
                    devArchiveSnapshot = DevArchiveFixtures.BuildRandom(devSeed);
                    devArchiveSeed = devSeed;
                }
                return devArchiveSnapshot;
            }
            return detail != null ? detail.ArchiveView : null;
        }

        /// <summary>
        /// 页头右侧「测试数据」开关（仅 DevMode；排在「返回」左侧）。返回新的右边界。
        ///
        /// <para><b>六态 / 反馈</b>：走原版 <c>Widgets.ButtonText</c>（与同排「返回」同族）⇒ 默认 / 悬停 /
        /// 按下 / 禁用态由引擎出；「开」态额外缀 <c>✓</c>（与页头既有 dev 按钮同法）。
        /// 文案复用既有 Keyed <c>…MasterControl.Dev.Toggle</c>（本轮 Keyed <b>零新增</b>）。</para>
        ///
        /// <para>非 DevMode <b>连按钮都不画</b>（而不是画灰按钮）并把开关复位 —— 普通玩家不可能触发。</para>
        /// </summary>
        private float DrawDevToggleButton(float y, float btnHeight, float btnGap, float right)
        {
            if (!Prefs.DevMode)
            {
                devPreview = false;
                return right;
            }
            // ⚠ 2026-09-19：常量键改走缓存；`" ✓"` 是**排版符号**（非文案），拼接保留 ——
            // 且本分支只在 DevMode 下执行。
            string label = Translate("PersonalChronicle.UI.MasterControl.Dev.Toggle")
                + (DevPreviewActive ? " ✓" : string.Empty);
            float width = MeasureWidth(label, GameFont.Small) + 24f;
            Rect btn = new Rect(right - width, y, width, btnHeight);
            if (Widgets.ButtonText(btn, label))
            {
                devPreview = !devPreview;
                if (devPreview)
                {
                    // 「关→开」：换新种子 ⇒ PopulationForDisplay 下一帧重建快照（与上次 / 上次会话都不同）。
                    devSeed = NextDevSeed();
                }
                ForceRefresh();
            }
            return btn.x - btnGap;
        }

        /// <summary>
        /// 新测试数据种子：游戏时钟（<c>TickManager.TicksAbs</c>）与全局 <c>Rand</c> 混合 ——
        /// 同会话连点不同、跨会话也不同；<b>不落存档</b>（纯 UI 瞬时值）。
        /// </summary>
        private static int NextDevSeed()
        {
            long ticks = Find.TickManager != null ? Find.TickManager.TicksAbs : 0L;
            int seed = Rand.RangeInclusive(1, 2000000000);
            unchecked
            {
                seed = seed * 31 + (int)(ticks & 0x7FFFFFFF);
            }
            return seed == 0 ? 1 : seed;
        }
    }
}
