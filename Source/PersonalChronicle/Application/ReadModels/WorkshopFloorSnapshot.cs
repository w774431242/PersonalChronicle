using System.Collections.Generic;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// 车间俯视快照（制造中心 ② 主视图）。**只含数据键与预格式化文本**，
    /// 几何位置一律由 <c>WorkshopFloorLayout</c> 的设计坐标给出（快照只回答"第几格第几台"）。
    ///
    /// <para>与 08c 数据层契约一致：字段已本地化、只读、失败隔离。框架（厂房/柱网/通道/图例）
    /// 是**固定图元**，不随数据变化；本快照只驱动"格内有谁、设备什么状态"。</para>
    /// </summary>
    public sealed class WorkshopFloorSnapshot : ArchiveSectionSnapshot
    {
        /// <summary>
        /// 框架格位数（与 <c>WorkshopFloorLayout</c> 的几何表**必须一致**：
        /// 这里定"能放几个"，那里定"放在哪"）。
        /// </summary>
        public const int BaySlots = 12;

        /// <summary>每格设备位数。</summary>
        public const int DevicesPerBay = 2;

        /// <summary>12 个格位（不足的格 <see cref="FloorBayView.Occupied"/> = false，画空位）。</summary>
        public List<FloorBayView> Bays = new List<FloorBayView>();

        /// <summary>落在格内的真实设备（≤ 格位 × 2）。</summary>
        public List<FloorDeviceView> Devices = new List<FloorDeviceView>();

        /// <summary>车间成员圆点。</summary>
        public List<FloorWorkerView> Workers = new List<FloorWorkerView>();

        /// <summary>真实车间数。</summary>
        public int WorkshopCount;

        /// <summary>真实设备数。</summary>
        public int DeviceCount;

        /// <summary>
        /// 左轨「原料库」（对应预览 <c>#rail-left</c>：收货月台 → IQC → 原料条）。
        /// 数据 = 当前工单涉及的原料 → 库存 / 单次需求。
        /// </summary>
        public List<FloorRawItemView> RawItems = new List<FloorRawItemView>();

        /// <summary>右轨「成品库」（对应预览 <c>#rail-right</c>：在库件数 + Top 成品 + 品质分布）。</summary>
        public FloorDepotView Depot = new FloorDepotView();

        /// <summary>工序链路（对应预览 <c>buildRouteLayer</c> 的 <c>ROUTES</c>；每条 = 一个 <c>RouteDef</c>）。</summary>
        public List<FloorRouteView> Routes = new List<FloorRouteView>();

        /// <summary>底注（已本地化，含真实计数）。</summary>
        public string Caption = string.Empty;

        /// <summary>有框架但无数据时的提示（无车间 / 无设备 / 无地图）；空 = 正常。</summary>
        public string Hint = string.Empty;

        /// <summary>无地图时为 true（此时连框架也不画）。</summary>
        public bool NoMap;
    }

    /// <summary>一个车间格（= 预览 <c>ZONES</c> 的一个 bay）。</summary>
    public sealed class FloorBayView
    {
        /// <summary>格位序号 0..11（对应 <c>WorkshopFloorLayout.BayRect</c>）。</summary>
        public int Slot;

        /// <summary>false = 空位（矿图里还没有这么多车间）。</summary>
        public bool Occupied;

        /// <summary>格内编号文字（"1".."12"）。</summary>
        public string Number = string.Empty;

        /// <summary>车间名（真实 <c>Zone.label</c>）或空位文案。</summary>
        public string Name = string.Empty;

        /// <summary>车间类型标签；无类型时为空字符串（UI 画「—」）。</summary>
        public string TypeLabel = string.Empty;

        /// <summary>格右下角元信息（"{0} 台设备"）。</summary>
        public string Meta = string.Empty;

        /// <summary>强调色标记（有设备 = 青色圆标，与预览 T1 观感一致）。</summary>
        public bool Highlight;
    }

    /// <summary>格内一台真实设备。</summary>
    public sealed class FloorDeviceView
    {
        public ProductionDeviceCardView Card;

        /// <summary>
        /// 稳定 ID = <c>&lt;defName&gt;:&lt;thingIDNumber&gt;</c>（DL 契约 / GOV-009）。
        /// 自由画布上的设备卡靠它**回指**本视图，从而打开设备详情（2026-09-12 新增）。
        /// </summary>
        public string StableId = string.Empty;

        public int Slot;

        /// <summary>格内序号 0/1（对应 <c>WorkshopFloorLayout.MachineLocalX</c>）。</summary>
        public int Index;

        /// <summary>设备显示名（"{label} A/B"）。</summary>
        public string Name = string.Empty;

        /// <summary>状态键：run / idle / fault / off（UI 只做键→灯色映射）。</summary>
        public string StateKey = string.Empty;

        /// <summary>状态文案（已本地化）。</summary>
        public string StateText = string.Empty;

        /// <summary>功率（"{0}W"，真实 <c>PowerConsumption</c> 绝对值）。</summary>
        public string PowerText = string.Empty;

        /// <summary>机箱第二行左侧小字（工单数："N 单" / "无单"）。</summary>
        public string SubText = string.Empty;

        /// <summary>操作者名（当前在做的 pawn，或空）。</summary>
        public string OperatorText = string.Empty;

        /// <summary>进度 0..1；&lt; 0 = 不画进度条。</summary>
        public float Progress01 = -1f;

        /// <summary>悬停提示（构建期拼好，绘制零分配）。</summary>
        public string Tooltip = string.Empty;
    }

    /// <summary>
    /// 一条工艺路线（工序链路）。**只给数据、不给几何**：几何（折线/角标/WIP 箱位置）
    /// 由 UI 层按 <c>WorkshopFloorLayout</c> 从格序号推导 —— 数据层不认识设计坐标（分层）。
    /// </summary>
    public sealed class FloorRouteView
    {
        public string Name = string.Empty;
        public string NameEn = string.Empty;

        /// <summary>物料光点走完全程的秒数（预览 <c>dur</c>）。</summary>
        public float CycleSeconds = 6f;

        /// <summary>是否画「原料入线」（原料库 → 首道工序）。</summary>
        public bool HasFeed;

        /// <summary>有序工序（每个 = 一个车间格）。</summary>
        public List<FloorRouteStepView> Steps = new List<FloorRouteStepView>();

        /// <summary>末端流向件数（"→库 N 件"里的 N = 末道工序最近 1 游戏日产出）。</summary>
        public int EndFlowCount;

        /// <summary>末端流向标注（已本地化）。</summary>
        public string EndText = string.Empty;
    }

    /// <summary>链路里的一道工序（= 一个车间格）。</summary>
    public sealed class FloorRouteStepView
    {
        /// <summary>车间格序号（0 起，对应 <c>WorkshopFloorLayout.BayRect</c>）。</summary>
        public int Slot;

        /// <summary>工序号文字（"1".."12"）。</summary>
        public string StepText = string.Empty;

        /// <summary>该工序最近 1 游戏日的产出件数（= 段流量）。</summary>
        public int FlowCount;

        /// <summary>流量归一化 0..1（线宽与颜色）。</summary>
        public float FlowRatio01;

        /// <summary>该格在制工单数（= 线边"在制"量）。</summary>
        public int WipCount;
    }

    /// <summary>左轨一行原料：库存 / 单次开单需求。</summary>
    public sealed class FloorRawItemView
    {
        /// <summary>原料名（ThingDef label）。</summary>
        public string Name = string.Empty;

        /// <summary>现有库存（已格式化）。</summary>
        public string CountText = string.Empty;

        /// <summary>已本地化的口径说明（"单次需 N"）。</summary>
        public string Note = string.Empty;

        /// <summary>库存 / 单次需求（0..1，截顶）。</summary>
        public float Ratio01;

        /// <summary>库存不够开一单（Ratio &lt; 1）→ 行标红。</summary>
        public bool Low;

        /// <summary>
        /// 需求分档角标（预览 <c>.rabc</c> 的 <c>A</c> / <c>B</c> / <c>C</c>）：按**当前工单单次需求量**
        /// 降序累计占比分档（A ≤ 50% · B ≤ 80% · C 其余）。
        ///
        /// <para>与预览 <c>railAbc()</c> 同法，**口径差**：预览按"全部配方需求"（静态演示数据），
        /// 本工程按"当前工单需求"（可动态变化）—— 差异写在轨道 <c>Floor.Raw.AbcTip</c> 悬停提示里。
        /// 空串 = 无分档（UI 不画角标，不编造）。</para>
        /// </summary>
        public string TierKey = string.Empty;
    }

    /// <summary>右轨成品库。</summary>
    public sealed class FloorDepotView
    {
        /// <summary>在库件数（已格式化："N 件在库"）。</summary>
        public string TotalText = string.Empty;

        /// <summary>Top N 成品：名称 → 数量（已格式化）。</summary>
        public List<string> TopItems = new List<string>();

        /// <summary>品质分布 chips（"良好 12"，用原版 <c>QualityUtility.GetLabel</c>）。</summary>
        public List<string> QualityChips = new List<string>();

        /// <summary>在库为空 → 画「暂无入库」。</summary>
        public bool Empty;
    }

    /// <summary>格内一个成员圆点。</summary>
    public sealed class FloorWorkerView
    {
        public int Slot;
        public int Index;

        /// <summary>姓名首字。</summary>
        public string Initial = string.Empty;
    }
}
