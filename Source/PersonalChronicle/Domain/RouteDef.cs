using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 工艺路线（对应预览的 <c>ROUTES</c>）—— 车间俯视图「工序链路层」的静态定义。
    ///
    /// <para>预览把路线写成<b>手绘引导折线</b>（<c>guide:[...]</c>）；本工程的车间接线是
    /// **固定的 12 格栅格**（<c>WorkshopFloorLayout</c>），所以路线只需声明「格的先后顺序」，
    /// 折线由格中心自动生成 —— 既省掉手绘坐标，又保证与布局永远一致。</para>
    ///
    /// <para>只放静态数据：本 Def 不含流量、不含在制数（那些是运行态，来自工单与产出 ledger）。</para>
    /// </summary>
    public sealed class RouteDef : Def
    {
        /// <summary>英文名（与 label 并列显示，纯展示）。</summary>
        public string nameEn = string.Empty;

        /// <summary>工序序列 = 车间格序号（0 起，对应 <c>WorkshopFloorLayout.BayRect</c>）。</summary>
        public List<int> steps = new List<int>();

        /// <summary>物料光点走完全程的秒数（预览 <c>dur</c>）。</summary>
        public float cycleSeconds = 6f;

        /// <summary>是否画「原料入线」（左轨原料库 → 首道工序）。</summary>
        public bool feedFromRawStore = true;
    }
}
