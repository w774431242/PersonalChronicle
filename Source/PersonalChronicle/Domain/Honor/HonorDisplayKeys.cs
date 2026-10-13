using System;

namespace PersonalChronicle.Domain.Honor
{
    /// <summary>
    /// 人物「显示称号」存档键（<see cref="Career.CareerData.DisplayedHonorKey"/>）。
    /// 空 = 自动；<see cref="None"/> = 隐藏；其余为职称/勋章 defName 带前缀。
    /// </summary>
    public static class HonorDisplayKeys
    {
        public const string None = "none";
        public const string TitlePrefix = "title:";
        public const string MedalPrefix = "medal:";

        public static bool IsNone(string key)
        {
            return string.Equals(key, None, StringComparison.Ordinal);
        }

        public static bool IsAuto(string key)
        {
            return string.IsNullOrEmpty(key);
        }

        public static string ForTitle(string titleDefName)
        {
            return TitlePrefix + titleDefName;
        }

        public static string ForMedal(string medalDefName)
        {
            return MedalPrefix + medalDefName;
        }

        public static bool TryParseTitle(string key, out string titleDefName)
        {
            titleDefName = null;
            if (string.IsNullOrEmpty(key) || !key.StartsWith(TitlePrefix, StringComparison.Ordinal))
            {
                return false;
            }
            titleDefName = key.Substring(TitlePrefix.Length);
            return !string.IsNullOrEmpty(titleDefName);
        }

        public static bool TryParseMedal(string key, out string medalDefName)
        {
            medalDefName = null;
            if (string.IsNullOrEmpty(key) || !key.StartsWith(MedalPrefix, StringComparison.Ordinal))
            {
                return false;
            }
            medalDefName = key.Substring(MedalPrefix.Length);
            return !string.IsNullOrEmpty(medalDefName);
        }
    }
}
