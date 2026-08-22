using PersonalChronicle.Domain;

namespace PersonalChronicle.Archive.ReadModels
{
    /// <summary>
    /// 勋章展示翻译键（架构方案 §6.7）。defName 含点号（如 Medal.Labor.Model.Bronze），
    /// 翻译键 = PersonalChronicle.UI.Medal.&lt;defName&gt;.Label/.Desc（点层级安全）。
    /// Read Model（BuildMedals）与 UI（勋章墙/金质公告）共用，避免裸 key 泄漏。
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
    }
}
