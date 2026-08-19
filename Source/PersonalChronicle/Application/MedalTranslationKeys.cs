using PersonalChronicle.Domain;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// 勋章翻译键集中类（架构方案 §6.7）。defName 使用下划线分隔（如 Medal_Labor_Model_Bronze），
    /// 翻译键 = PersonalChronicle.UI.Medal.&lt;defName&gt;.Label/.Desc（下划线层级）。
    /// 授勋服务（Application，金质公告）与勋章墙 Read Model（UI，BuildMedals）共用，
    /// 归 Application 层满足单向依赖（UI → Application → Domain），避免裸 key 泄漏。
    /// </summary>
    public static class MedalTranslationKeys
    {
        public const string Root = "PersonalChronicle.UI.Medal.";

        /// <summary>
        /// 翻译键约定使用 series/tier 层级（如 Labor_Model_Bronze），与 defName 的 Medal_ 前缀解耦。
        /// </summary>
        private static string KeyOf(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return defName;
            return defName.StartsWith("Medal_") ? defName.Substring("Medal_".Length) : defName;
        }

        public static string Label(string defName)
        {
            return Root + KeyOf(defName) + ".Label";
        }

        public static string Desc(string defName)
        {
            return Root + KeyOf(defName) + ".Desc";
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
        /// 无材质称号名（UI.Medal.&lt;seriesKey&gt;.Name，如 Labor_Model → 劳动模范）。
        /// seriesKey 由 <see cref="MedalDef.SeriesKeyOf"/> 从 defName 推导（含 Medal_ 前缀），
        /// 此处剥离前缀以匹配语言键；公告 {1} 与勋章墙分组共用一个键，避免与 Label（含材质）重复文案。
        /// </summary>
        public static string SeriesName(string seriesKey)
        {
            return Root + KeyOf(seriesKey) + ".Name";
        }

        /// <summary>手动授勋对话框标题（UI.Medal.ManualAward.Title）。</summary>
        public static string ManualAwardTitle()
        {
            return Root + "ManualAward.Title";
        }

        /// <summary>勋章墙「手动授勋」按钮文案（UI.Medal.ManualAward.Button）。</summary>
        public static string ManualAwardButton()
        {
            return Root + "ManualAward.Button";
        }

        /// <summary>手动授勋对话框空态提示（UI.Medal.ManualAward.Empty，无可授勋章时）。</summary>
        public static string ManualAwardEmpty()
        {
            return Root + "ManualAward.Empty";
        }

        /// <summary>手动授勋成功反馈（UI.Medal.ManualAward.Done，{0}=勋章 Label 键 defName）。</summary>
        public static string ManualAwardDone()
        {
            return Root + "ManualAward.Done";
        }

        /// <summary>手动授勋被拦截反馈（UI.Medal.ManualAward.Blocked，{0}=勋章 Label 键 defName）。</summary>
        public static string ManualAwardBlocked()
        {
            return Root + "ManualAward.Blocked";
        }
    }
}
