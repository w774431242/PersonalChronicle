using System.Collections.Generic;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// 制造中心 ⑦ 数据层契约快照（对应预览 §⑦：Def 注册表 + Runtime Things · Read Model 快照）。
    ///
    /// <para>这一块展示的是**本 mod 真正依赖的原版数据契约**：Def 注册表（ThingDef / RecipeDef /
    /// QualityCategory 枚举顺序）与运行时对象快照（Thing + Comp + BillStack）—— 数据全部可实证。</para>
    /// </summary>
    public sealed class ManufacturingDataLayerSnapshot : ArchiveSectionSnapshot
    {
        /// <summary>副标："ThingDef N · RecipeDef M · Quality 7 档"。</summary>
        public string DefsSub = string.Empty;

        /// <summary>QualityCategory 枚举顺序 chips（"0 Awful" … "6 Legendary"）。</summary>
        public List<string> QualityChips = new List<string>();

        /// <summary>ThingDef 节选。</summary>
        public List<DefRowView> ThingDefs = new List<DefRowView>();

        /// <summary>RecipeDef 节选。</summary>
        public List<DefRowView> Recipes = new List<DefRowView>();

        /// <summary>副标："revision N · builtAt HH:MM · EMPTY?"。</summary>
        public string ThingsSub = string.Empty;

        /// <summary>运行时对象表头（列名已本地化）。</summary>
        public List<string> ThingsHeader = new List<string>();

        /// <summary>运行时对象行（列数 = <see cref="ThingsHeader"/>.Count）。</summary>
        public List<ThingRowView> Things = new List<ThingRowView>();

        /// <summary>PowerNet 汇总（名称 → 值）。</summary>
        public List<string> PowerNets = new List<string>();
    }

    /// <summary>Def 注册表一行。</summary>
    public sealed class DefRowView
    {
        public string Name = string.Empty;
        public string Detail = string.Empty;
        public string Meta = string.Empty;
        public string Extra = string.Empty;
    }

    /// <summary>运行时对象一行。</summary>
    public sealed class ThingRowView
    {
        public List<string> Cells = new List<string>();
    }
}
