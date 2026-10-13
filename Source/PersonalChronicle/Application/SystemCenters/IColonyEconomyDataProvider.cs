using PersonalChronicle.Application.ReadModels;
using Verse;

namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// 可选契约：**殖民地经济中心整页**的数据源（`CHG-LCE-001`）。
    ///
    /// <para>与既有可选契约同形（<c>IManufacturing*DataProvider</c> / <c>IPopulationOverviewProvider</c>）：由中心的 KPI Provider 兼实现
    /// （「一个 Provider 多契约」，GOV-002），<c>SystemCenterReadModelProvider</c> 经
    /// <c>provider as IColonyEconomyDataProvider</c> 探测 —— 新增能力用可选接口，
    /// 不给 <see cref="ISystemCenterDataProvider"/> 加成员（不破坏第三方实现）。</para>
    ///
    /// <para>快照**非 null 即渲染**（LAND-010：UI 只看数据、不看中心名）。</para>
    /// </summary>
    public interface IColonyEconomyDataProvider
    {
        /// <summary>
        /// 构建整页快照。重建期 = 骨架说明；三块业务视图（库存与市值 / 产消账本 / 劳动力与产出）
        /// 落地后在此扩容（<paramref name="map"/> 为 null 时必须安全返回，不抛）。
        /// </summary>
        ColonyEconomySnapshot BuildColonyEconomy(Map map);

        /// <summary>
        /// 构建「库存 K 线 + 热力图」整页快照（`SDD-LCE-003` §4，阶段 3 落地）。
        ///
        /// <para>与 <see cref="BuildColonyEconomy"/> **同族**：可选契约、失败隔离（异常退化为
        /// 空快照，不冒泡）；<paramref name="request"/> = 窗口瞬时态的选中品种/档位
        /// （缺省 ⇒ 实现方自行解析默认）。</para>
        /// </summary>
        ColonyStockGallerySnapshot BuildStockGallery(Map map, ColonyStockViewRequest request);
    }
}
