using System.Collections.Generic;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// 设备详情弹窗快照（对齐预览 <c>#drawer</c> 居中弹窗）。
    ///
    /// <para><b>LAY-001 分层</b>：本快照只提供**数据键与预格式化、已本地化的文本**，
    /// 所有几何（弹窗宽高、栅格列数、卡高）都在 UI 层按 <see cref="WorkshopFloorLayout"/>
    /// 风格的设计常量推导 —— 数据层不认识像素。</para>
    ///
    /// <para><b>LAND-009 数据缺口如实</b>：任何取不到真实值的指标，<see cref="FloorDetailMetricView.Available"/>
    /// 置 false 且 <see cref="FloorDetailMetricView.ValueText"/> 填「—」，
    /// **不得**用 0 / 估算值冒充。UI 按 Available 决定是否画进度条与语气色。</para>
    ///
    /// <para><b>预览结构对照</b>：<c>.dhead</c>（标题+编号+状态灯+节拍）→ <c>.mkpi</c>（6 格指标）
    /// → <c>.dtab</c>（4 个页签）→ <c>.dsec</c>（页签内容）→ <c>.dgrid2</c>（数据层契约 + 工序导航）。</para>
    /// </summary>
    public sealed class FloorDeviceDetailSnapshot : ArchiveSectionSnapshot
    {
        /// <summary>是否有可展示的设备（false 时 UI 直接不弹窗）。</summary>
        public bool HasDevice;

        // ---- .dhead 标题带 ----

        /// <summary>设备编号（预览 <c>m.id</c>，如 "M03"）。无稳定编号时为空。</summary>
        public string DeviceId = string.Empty;

        /// <summary>设备显示名（真实 <c>ThingDef.label</c>，已本地化）。</summary>
        public string Name = string.Empty;

        /// <summary>第二行：车间名 · 工序位次 · 档位（预览 <c>.did</c>）。已本地化。</summary>
        public string Identity = string.Empty;

        /// <summary>状态键：run / idle / fault / off（UI 只做键→灯色映射）。</summary>
        public string StateKey = string.Empty;

        /// <summary>状态文案（已本地化，含英文键，如「RUN 运行中」）。</summary>
        public string StateText = string.Empty;

        /// <summary>
        /// 状态行**右侧补充文本**：当前作业（`CurJobDef` 本地化标签，选中工单时追加「 · 配方名」）。
        ///
        /// <para>⚠ 契约同步（CHG-20261002-FLOORDETAIL-AUDIT）：本字段旧注释写「节拍文本
        /// （"节拍 42s/件"）」，但写入端（<c>WorkshopFloorViewProvider.BuildHeader</c>）早已改为
        /// 当前作业、渲染端（<c>FloorDeviceDetailWindow.DrawHeader</c>）也按此显示 ⇒
        /// **字段名保留（避免快照契约重命名波及其它消费点）、语义以本注释为准**。
        /// 取不到作业时为空串（不编造，LAND-009）。</para>
        /// </summary>
        public string CycleText = string.Empty;

        // ---- .mkpi 指标格 ----

        /// <summary>
        /// 指标格（预览 `.mkpi`）—— 顺序即显示顺序，张数由 Provider 决定；
        /// 列数 = min(6, 本表 Count)（预览默认 4 格，CHG-20261002-FLOORDETAIL-AUDIT）。
        /// </summary>
        public List<FloorDetailMetricView> Metrics = new List<FloorDetailMetricView>();

        // ---- .dtab 页签 ----

        /// <summary>
        /// 页签标题（已本地化）。**顺序即页签顺序**；S2-1（2026-09-23）重组后为
        /// 概览 / 生产 / 人员 / 健康（**开发模式**下追加「契约」）。
        /// </summary>
        public List<string> TabTitles = new List<string>();

        /// <summary>
        /// 全部页签内容块 —— **一页可含多块**（用 <see cref="FloorDetailSectionView.TabIndex"/> 关联页签，
        /// 与 <see cref="TabTitles"/> **不是**下标一一对应；生产 = 列表 + 工单摘要；健康 = TPM + 品质）。
        /// </summary>
        public List<FloorDetailSectionView> Sections = new List<FloorDetailSectionView>();
    }

    /// <summary>一格 mkpi 指标（预览 <c>.mkpi&gt;div</c>：大数值 + 小标签）。</summary>
    public sealed class FloorDetailMetricView
    {
        /// <summary>大数值文本（"87%" / "42" / "—"）。</summary>
        public string ValueText = string.Empty;

        /// <summary>小标签（"单台 OEE"）。</summary>
        public string Label = string.Empty;

        /// <summary>false = 该指标在 1.6 引擎/本 mod 范围内取不到真实值（LAND-009）。</summary>
        public bool Available;
    }

    /// <summary>一个页签的内容块（标题 + 小标题 + 若干行 + 可选品质柱）。</summary>
    public sealed class FloorDetailSectionView
    {
        /// <summary>小标题（预览 <c>.dsub</c>，如「状态时序 · MachineStateTimeline」）。</summary>
        public string Caption = string.Empty;

        /// <summary>行（预览 <c>.drow2</c>：左标签 + 右值）。</summary>
        public List<FloorDetailRowView> Rows = new List<FloorDetailRowView>();

        /// <summary>
        /// 该节的**渲染种类**（LAND-010：结构经契约声明 —— UI 按声明渲染，不认识"制造中心"这类模块名）。
        /// 空 = 通用渲染；<see cref="RenderKindBillQueue"/> = ⑦ 生产列表（**可写**：调序/挂起/移出/追加）。
        /// </summary>
        public string RenderKind = string.Empty;

        /// <summary>⑦ 生产列表行（仅 <see cref="RenderKindBillQueue"/> 用）。</summary>
        public List<FloorBillRowView> BillRows = new List<FloorBillRowView>();

        /// <summary>可追加的配方 defName（与 <see cref="RecipeLabels"/> 同序；点击回传下标）。</summary>
        public List<string> RecipeDefNames = new List<string>();

        /// <summary>可追加的配方显示名（与 <see cref="RecipeDefNames"/> 同序）。</summary>
        public List<string> RecipeLabels = new List<string>();

        /// <summary>本节的稳定 ID（生产列表动作回传用；<c>&lt;defName&gt;:&lt;thingIDNumber&gt;</c>）。</summary>
        public string BenchStableId = string.Empty;

        /// <summary>状态时序条（预览 <c>.tl&gt;i</c>）：每项 = 状态键（run/idle/starved/fault/off）。</summary>
        // S1（2026-09-23）：`TimelineKeys` 已删除 —— 状态时序条整链移除（40 格只有 1 段真数据）。

        /// <summary>品质分布柱（预览 <c>.qh&gt;i</c>）：7 档计数（Awful..Legendary 顺序）。</summary>
        public List<int> QualityHistogram = new List<int>();

        /// <summary>品质柱标签（7 档，已本地化）。</summary>
        public List<string> QualityLabels = new List<string>();

        /// <summary>
        /// **InspectString 区**（新预览 2026-09-11 在「运行」页新增）：
        /// 由 <c>Thing.GetInspectString()</c> 原样取回后按行拆开 —— 该串本身即
        /// **基类内容 + 各 `CompInspectString`** 的官方合成，因此**只读不改**：
        /// 不拼接、不过滤、不重排，避免漏掉基类的火灾/温度告警（LAND-009 / UI-004）。
        /// </summary>
        public List<string> InspectLines = new List<string>();

        /// <summary>空态提示（页签内容不可得时显示；空 = 有内容）。</summary>
        public string EmptyHint = string.Empty;

        /// <summary>
        /// true = 该块**跨两栏**（新预览 2026-09-11：页签内容改两栏后，
        /// <c>.dsub/.insp/.giz/.nav2/.chips</c> 等仍跨栏）。UI 据此决定是否走整宽。
        /// </summary>
        public bool Wide;

        /// <summary>
        /// **块级数据来源标**（与 <see cref="FloorDetailRowView.SourceKind"/> 同号体系）：
        /// <c>0 = 未标注</c>（不画）/ <c>1 = API</c> / <c>2 = LEDGER</c> / <c>3 = SIM</c>。
        /// 用于整块口径不同的情形（如「状态时序」整块都是原版无历史的推断填满 ⇒ SIM），
        /// UI 画在小标题（<see cref="Caption"/>）之后。
        /// </summary>
        public int SourceKind;

        /// <summary>
        /// S2-1（2026-09-23）：**本块归属的页签序号**（0 起）。
        ///
        /// <para>页签 7→4 重组后**一页可含多块**（生产 = 可写列表 + 工单摘要；健康 = TPM + 品质），
        /// UI 按本值过滤并按 <c>Sections</c> 顺序纵向绘制；<c>Sections</c> 与
        /// <c>TabTitles</c> **不再是下标一一对应**。</para>
        /// </summary>
        public int TabIndex;
    }

    /// <summary>
    /// ⑦ 生产列表的一行（= 本机 <c>BillStack</c> 的一条 Bill）。
    /// **只读数据**：所有写操作由 UI 回传"动作 + 行下标"给 <c>WorkshopBillActions</c> 执行。
    /// </summary>
    public sealed class FloorBillRowView
    {
        /// <summary>配方显示名（已本地化）。</summary>
        public string RecipeLabel = string.Empty;

        /// <summary>配方 defName（逻辑键，GOV-009）。</summary>
        public string RecipeDefName = string.Empty;

        /// <summary>重复模式与目标（"Forever" / "N 次" / "目标 N"，已本地化）。</summary>
        public string ModeText = string.Empty;

        /// <summary><c>Bill.suspended</c>。</summary>
        public bool Suspended;

        /// <summary>是否为**第一条未挂起**的工单（原版：它就是当前执行的那条）。</summary>
        public bool Active;

        /// <summary>
        /// S3-5d（2026-09-23）：**本工单指定的生产人员**（原版 <c>Bill.PawnRestriction</c> 的
        /// <c>LabelShort</c>）；未指定 = 空串。只带名字，**不带 Pawn 引用**（跨层/跨档安全）；
        /// 改指定走写面 <c>WorkshopBillActions.TrySetPawnRestriction</c>（原版公开
        /// <c>Bill.SetPawnRestriction</c>，已 Cecil 核实）。
        /// </summary>
        public string AssignedName = string.Empty;

        /// <summary>
        /// 批次 2（2026-09-23）：**该工单的进度**（0–1）；<c>-1</c> = 无真实进度（不画条）。
        /// 仅"当前执行中"那条有值（<c>JobDriver_DoBill.workLeft ÷ WorkAmountTotal</c>，与俯视图同源）；
        /// 其余工单处于等待/挂起 ⇒ 如实不画条，不伪造 0%。
        /// </summary>
        public float Progress01 = -1f;

        /// <summary>
        /// 批次 2：**该工单的状态文案**（已本地化：运行中 N% / 等待 / 已挂起）。
        ///
        /// <para>预览要求列表有**独立状态列** —— 原实现把状态塞进副行（`ModeText · 已挂起`），
        /// 状态与模式混在一起，扫一眼分不出"这条在跑还是在等"。</para>
        /// </summary>
        public string StateText = string.Empty;
    }

    /// <summary>⑦ 生产列表的渲染种类标识（契约常量；UI 只比这个值）。</summary>
    public static class FloorDetailRenderKinds
    {
        public const string BillQueue = "billqueue";
    }

    /// <summary>
    /// S3-5（2026-09-23）：**行级动作类别**（<see cref="FloorDetailRowView.ActionKind"/> 的取值）。
    ///
    /// <para>快照只声明"这行能做什么"，**实际写入由窗口层经写面动作类执行**
    /// （本层不改原版状态、不持有 Pawn / Thing 引用）。</para>
    /// </summary>
    public static class FloorDetailActionKinds
    {
        /// <summary>派工 / 取消派工：把行的 <c>PawnThingId</c> 指定给 <c>ActionIndex</c> 号工单。</summary>
        public const int AssignPawn = 1;
    }

    public sealed class FloorDetailRowView
    {
        /// <summary>左标签（已本地化）。</summary>
        public string Label = string.Empty;

        /// <summary>右值（已格式化 / 已本地化）。</summary>
        public string Value = string.Empty;

        /// <summary>
        /// 语气（0=中性 1=好 2=注意 3=差）。
        /// UI 只做语气→配色映射，不重算阈值。
        /// </summary>
        public int Tone;

        /// <summary>
        /// CHG-20261002-PRODCOLLAPSE（LAND-010）：**折叠摘要行声明** ——
        /// true = 该行属于**摘要行集合**（折叠面只显示这些行，如「配方 / 基础工时 / 重复模式·目标数量」）；
        /// 块内**无任何 true 行 ⇒ 该块不提供折叠交互**。UI 只读标志、**不按行下标特判**；
        /// Provider 按**行语义**逐行标注（行键映射），非「前 N 行」顺序约定。
        /// </summary>
        public bool IsSummary;

        /// <summary>
        /// **数据来源徽标**（新预览 2026-09-11 新增：每行右端一枚小徽标）——
        /// <c>0 = 未标注</c>（**不画徽标**，避免把未知来源误标成 API）；
        /// <c>1 = API</c>（原版字段直读）；<c>2 = LEDGER</c>（本 mod 累计 / Provider 计算）；
        /// <c>3 = SIM</c>（原版**无此口径** ⇒ <see cref="Value"/> 必须为「—」并在
        /// <see cref="Label"/> 或附注里写明原因，LAND-009）。
        ///
        /// <para>UI 只按号映射文案与颜色，**不自行判定来源**（LAND-010）。</para>
        /// </summary>
        public int SourceKind;

        /// <summary>
        /// S3-4（2026-09-23）：**&gt; 0 ⇒ 该行左侧画小人头像**。
        /// 值 = <c>Pawn.thingIDNumber</c> —— 快照**不带 Pawn 引用**（跨层 / 跨档不安全，全工程既有纪律），
        /// 窗口层经 <c>WorkshopPawnAccess.FindByThingId</c> 按 ID 找回；找不到就不画（不抛异常）。
        /// </summary>
        public int PawnThingId;

        /// <summary>
        /// S3-4：**≥ 0 ⇒ 该行值左侧画分数条**（0–1 占位）；<c>-1</c> = 不画。
        /// 与 <see cref="PawnThingId"/> 配合即"带头像与条的排名行"（人员页胜任度排名）。
        /// </summary>
        public float Bar01 = -1f;

        /// <summary>
        /// S3-5（2026-09-23）：**该行点击后要执行的动作**。
        /// <c>0 = 不可点</c>（默认，普通信息行）；<c>1 = 派工 / 取消派工</c>
        /// （把 <see cref="PawnThingId"/> 指定给 <see cref="ActionIndex"/> 号工单）。
        ///
        /// <para>快照只描述"这行能做什么"，**具体怎么写由窗口层经写面动作类执行**
        /// （UI 不直接改原版字段，LAND-010 的可写面纪律）。</para>
        /// </summary>
        public int ActionKind;

        /// <summary>S3-5：动作目标（工单序号；-1 = 不适用）。</summary>
        public int ActionIndex = -1;
    }
}
