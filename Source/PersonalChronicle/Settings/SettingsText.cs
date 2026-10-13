using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Settings
{
    /// <summary>
    /// 设置页文案缓存（CHG-20261005-SETTINGS-UI）。
    ///
    /// <para><b>为什么必须缓存</b>：<c>Translate()</c> 每次都产生 key 拼接 + 译文串 + <c>ToString()</c>
    /// 三笔分配；设置页是在 <c>OnGUI</c> 里每帧重绘的 ⇒ 不缓存就是每帧几十笔垃圾
    /// （GATE-PERF / skill 反模式清单「绘制路径里字符串拼接」）。</para>
    ///
    /// <para><b>失效</b>：按<b>语言对象引用</b>比较（<c>LanguageDatabase.activeLanguage</c>），
    /// 切语言后整体重建 —— 引用比较零分配，不需要每帧比字符串。</para>
    /// </summary>
    internal static class SettingsText
    {
        /// <summary>本页所有键的前缀（除 <c>OptionKeys</c> 里已含完整前缀的少数复用键）。</summary>
        public const string KeyPrefix = "PersonalChronicle.Settings.";

        private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
        private static readonly Dictionary<string, string[]> optionsCache = new Dictionary<string, string[]>();
        private static string[] tabs;
        private static object cachedLang;

        /// <summary>语言变化时清空缓存。<paramref name="changed"/> = 本次调用刚发生了重建。</summary>
        public static void EnsureFresh(out bool changed)
        {
            changed = false;
            object lang = LanguageDatabase.activeLanguage;
            if (ReferenceEquals(lang, cachedLang))
            {
                return;
            }
            cachedLang = lang;
            cache.Clear();
            optionsCache.Clear();
            rangeCache.Clear();
            tabs = null;
            changed = true;
        }

        /// <summary>取带前缀的文案（<paramref name="suffix"/> = 不含前缀的键后缀）。</summary>
        public static string T(string suffix)
        {
            if (string.IsNullOrEmpty(suffix))
            {
                return string.Empty;
            }
            string v;
            if (cache.TryGetValue(suffix, out v))
            {
                return v;
            }
            v = (KeyPrefix + suffix).Translate().ToString();
            cache[suffix] = v;
            return v;
        }

        /// <summary>取完整键文案（用于复用别处既有键：车间面板、工作面板等）。</summary>
        public static string Raw(string fullKey)
        {
            if (string.IsNullOrEmpty(fullKey))
            {
                return string.Empty;
            }
            string v;
            if (cache.TryGetValue(fullKey, out v))
            {
                return v;
            }
            v = fullKey.Translate().ToString();
            cache[fullKey] = v;
            return v;
        }

        /// <summary>
        /// 页签名数组（懒构建一次，随语言失效重建）。
        /// <para>长度**必须**与 <see cref="SettingsCatalog.VisibleTabs"/> 一致 —— 可见页集合
        /// 随开发者模式变化，故 <see cref="SettingsCatalog.VisibleTabs"/> 会回调
        /// <see cref="InvalidateTabs"/> 使本缓存在此处重建（CHG-20261005-SETTINGS §7-8）。</para>
        /// </summary>
        public static string[] Tabs
        {
            get
            {
                if (tabs != null)
                {
                    return tabs;
                }
                string[] keys = SettingsCatalog.VisibleTabKeys;
                tabs = new string[keys.Length];
                for (int i = 0; i < keys.Length; i++)
                {
                    tabs[i] = T(keys[i]);
                }
                return tabs;
            }
        }

        /// <summary>使页签文案缓存失效（可见页集合变化时由目录表回调）。</summary>
        public static void InvalidateTabs()
        {
            tabs = null;
        }

        private static readonly Dictionary<string, string> rangeCache = new Dictionary<string, string>();

        /// <summary>
        /// 滑块行的范围提示（如「范围：72 – 1000」）。**按行缓存** ⇒ 绘制期零分配
        /// （PERF-001：绘制路径禁 `string.Format`）。
        /// <para>为什么需要它：滑块改用 9 参重载后不再画原版的 min/max 标签
        /// （那会与「当前值列」挤成三数并列，见 `SettingsDrawer.PaintControl`），
        /// 范围信息改由悬停提供。</para>
        /// </summary>
        public static string RangeTip(string titleKey, float min, float max)
        {
            string v;
            if (rangeCache.TryGetValue(titleKey, out v))
            {
                return v;
            }
            v = string.Format(T("RangeFmt"), min.ToString("F0"), max.ToString("F0"));
            rangeCache[titleKey] = v;
            return v;
        }

        /// <summary>
        /// 取某行 <c>Segmented</c> 的选项文案（按行缓存 ⇒ 绘制期零分配）。
        /// <para>选项键含 <c>PersonalChronicle.</c> 前缀时按完整键取（复用既有文案），
        /// 否则按本页前缀取（短标签）。</para>
        /// </summary>
        public static string[] Options(SettingDef def)
        {
            if (def.OptionKeys == null || def.OptionKeys.Length == 0)
            {
                return null;
            }
            string[] arr;
            if (optionsCache.TryGetValue(def.TitleKey, out arr))
            {
                return arr;
            }
            arr = new string[def.OptionKeys.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                string k = def.OptionKeys[i];
                arr[i] = k.StartsWith("PersonalChronicle.") ? Raw(k) : T(k);
            }
            optionsCache[def.TitleKey] = arr;
            return arr;
        }
    }
}
