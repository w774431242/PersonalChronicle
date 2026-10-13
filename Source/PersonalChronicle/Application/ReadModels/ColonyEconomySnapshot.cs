using System.Collections.Generic;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// **殖民地经济中心**整页快照（`CHG-LCE-001` 重建期 = 最小骨架）。
    ///
    /// <para>⚠ 2026-09-20（用户裁决）：原 MVS 市场体系（五面板 / 市场 / 派系画像 / 供需）
    /// 已整体删除；本中心重建为「本地殖民地经济」——定位 = **产能 → 库存 → 财富的观测与记账层**
    /// （回归 `经济体系/README.md` 立项初心），数据全部来自 ① 原版真值或 ② 本模组实测捕获。</para>
    ///
    /// <para>重建期内容 = 面板标题 + 建设中说明行（全部已本地化）；
    /// 后续批次在此扩容：库存与市值 / 产消账本（含可维持天数）/ 劳动力与产出。</para>
    /// </summary>
    public sealed class ColonyEconomySnapshot : ArchiveSectionSnapshot
    {
        /// <summary>面板标题（已本地化）。</summary>
        public string TitleText = string.Empty;

        /// <summary>
        /// 说明行（已本地化、**逐行**）—— `UIComponents.Label` 强制不换行，
        /// 两行文案必须拆成两条，故用列表而不是含换行符的单串。
        /// </summary>
        public List<string> BodyLines = new List<string>();
    }
}
