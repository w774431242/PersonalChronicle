using PersonalChronicle.Domain;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// 勋章翻译键集中类（架构方案 §6.7）。defName 含点号（如 Medal.Labor.Model.Bronze），
    /// 翻译键 = PersonalChronicle.UI.Medal.&lt;defName&gt;.Label/.Desc（点层级安全）。
    /// 授勋服务（Application，金质公告）与勋章墙 Read Model（UI，BuildMedals）共用，
    /// 归 Application 层满足单向依赖（UI → Application → Domain），避免裸 key 泄漏。
    /// </summary>
    public static class MedalTranslationKeys
    {
        public const string Root = "PersonalChronicle.UI.Medal.";

        public static string Label(string defName)
        {
            return Root + defName + ".Label";
        }

        public static string Desc(string defName)
        {
            return Root + defName + ".Desc";
        }

        /// <summary>材质后缀（UI.Medal.Tier.Bronze/Silver/Gold）。</summary>
        public static string Tier(MedalTier tier)
        {
            return Root + "Tier." + tier;
        }

        /// <summary>金质勋章公告正文（UI.Medal.Gold.Letter，{0}=人物名 {1}=称号）。</summary>
        public static string GoldLetter()
        {
            return Root + "Gold.Letter";
        }

        /// <summary>勋章墙区块标题（UI.Medal.Wall.Title）。</summary>
        public static string WallTitle()
        {
            return Root + "Wall.Title";
        }

        /// <summary>勋章墙空态提示（UI.Medal.Wall.Empty）。</summary>
        public static string WallEmpty()
        {
            return Root + "Wall.Empty";
        }

        /// <summary>
        /// 无材质称号名（UI.Medal.&lt;seriesKey&gt;.Name，如 Medal.Labor.Model → 劳动模范）。
        /// seriesKey 由 <see cref="MedalDef.SeriesKeyOf"/> 从 defName 推导；公告 {1} 与
        /// 勋章墙分组共用一个键，避免与 Label（含材质）重复文案。
        /// </summary>
        public static string SeriesName(string seriesKey)
        {
            return Root + seriesKey + ".Name";
        }
    }
}
