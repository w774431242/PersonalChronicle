using System.Collections.Generic;

namespace PersonalChronicle.Application.PawnEconomy
{
    public sealed class EconomyRow
    {
        public readonly string Text, Action, Verb, Key;
        public readonly long Id;
        /// <summary>可选进度（0..1）。<c>&lt; 0</c> = 无进度 ⇒ 行族按普通行绘制（默认值即此）。
        /// CHG-20261010-ECON-LABOR-UI2：柜台工作台用它给"已派发 / 待办"与"已完成 / 总件数"画进度条 ——
        /// 以**可选字段**表达而非新增行类型，共用行族（聚合窗口也用它）因此零回归。</summary>
        public readonly float Progress;
        /// <summary>**列化载荷**（CHG-20261011-WORKCAT · S2a）：非空 = 该行由数据表格按"一格一列"渲染。
        /// 可选字段（与 <see cref="Progress"/> 同一先例）⇒ 既有全部行零改动。纯 UI DTO，**不入存档**
        /// （AI-001 已核实：`Application/PawnEconomy` 全链零 Scribe）。</summary>
        public readonly string[] Cells;
        /// <summary>与 <see cref="Cells"/> 同序的**排序数值**（数字列用；缺失的列退化为文本序）。</summary>
        public readonly long[] CellSorts;
        /// <summary>筛选标签（null = 说明/分区行，**任何筛选下都保留**；否则与表格声明的筛选键比较）。</summary>
        public readonly string Tag;
        /// <summary>**搜索键**（CHG-20261011-WORKCAT · S2b）：非空 ⇒ 参与自由搜索（大小写不敏感子串）。
        /// null = 不参与（搜索开启时该类行被丢弃 —— 见 <c>EconomyTableOps.Search</c>）。可选字段，既有行零改动。</summary>
        public readonly string Search;
        /// <summary>**行展开详情**（只读；多行文本用 <c>\n</c>）。null = 不可展开（不显示展开指示、点击不响应）。
        /// 详情内容由读模型在快照期拼好（含日期/键），呈现层只做折行与绘制（PERF-002）。</summary>
        public readonly string Detail;
        /// <summary>**起草载荷**（CHG-20261012-ECON-CONTRACTIZE）：非空 = 该行可**点击起草**一份工作合同，
        /// 内容 = 稳定寻址键 <c>{机构锚键}|{工作类型}</c>（由宿主窗口解析后打开 <c>Dialog_WorkContract</c>）。
        /// 与 <see cref="Detail"/>（行展开）**互斥**：可起草行不参与展开（一次点击只有一个含义，F4）。</summary>
        public readonly string Compose;
        /// <summary>**撤销载荷**（CHG-20261012-ECON-CONTRACTIZE · B1）：非空 = 该行提供一个"撤销"控件，
        /// 内容 = 稳定 ID（已派发合同的合同号）。它与 <see cref="Action"/>/<see cref="Verb"/> 配套，只画在
        /// **控件格**（列语义 <c>EconomyCellControl.Action</c>，由读模型声明）里 —— 呈现层既不认列键、
        /// 也不认"末列"位置（LAND-010）。纯 UI DTO，不入存档（`Application/PawnEconomy` 全链零 Scribe）。</summary>
        public readonly string Revoke;
        public EconomyRow(string text, string action = null, string verb = null, long id = 0, string key = null,
            float progress = -1f, string[] cells = null, long[] cellSorts = null, string tag = null,
            string search = null, string detail = null, string compose = null, string revoke = null)
        {
            Text = text; Action = action; Verb = verb; Id = id; Key = key;
            Cells = cells; CellSorts = cellSorts; Tag = tag; Search = search; Detail = detail; Compose = compose;
            Revoke = revoke;
            // 构造期夹取上限：>1 会让容量条画成"超载红段"（语义不符）—— 越界值在此归位，
            // 绘制层因此可以无条件信任 Progress ∈ {-1} ∪ [0,1]（GATE-UI 2026-10-10 P3-c）。
            Progress = progress > 1f ? 1f : progress;
        }
    }
    public sealed class EconomyView
    {
        public readonly int Revision;
        public readonly bool Enabled, Funded;
        public readonly string Status, Focus;
        public readonly IReadOnlyList<EconomyRow> Rows, Actors;
        public EconomyView(int revision, bool enabled, bool funded, string status, string focus, List<EconomyRow> rows, List<EconomyRow> actors)
        { Revision = revision; Enabled = enabled; Funded = funded; Status = status; Focus = focus; Rows = rows.AsReadOnly(); Actors = actors.AsReadOnly(); }
    }
}
