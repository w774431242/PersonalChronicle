using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Archive.UI
{
    /// <summary>
    /// <see cref="UIComponents"/> 的**绘制期文案缓存**（partial 切片，GOV-010 落地件）。
    ///
    /// <para><b>要解决的问题</b>：绘制路径上有大量 <c>"Key".Translate()</c>（无参 Keyed 键）。
    /// 单次只是字典查询，但 <c>.ToString()</c> 会**每帧分配一个新字符串** —— 稳态下持续的
    /// GC 压力，且与 GOV-010「绘制期零翻译、零拼接」的既定约定直接冲突
    /// （2026-09-19 审计：`MasterControl` / `Manufacturing·UI` 两区合计 ≥80 处）。</para>
    ///
    /// <para><b>为什么按语言缓存就够了</b>：无参 Keyed 键的值**只随语言变化**，
    /// 不依赖任何数据 —— 与 <c>BuiltFromRevision</c> 无关（那是快照缓存的事）。
    /// 故失效条件只有一个：当前语言变了。</para>
    ///
    /// <para><b>⛔ 不得用于带参翻译</b>（<c>Translate(arg)</c> / <c>Translate2</c> …）：
    /// 参数逐次不同，缓存会把上一次的结果返回给下一次调用 —— **错值比慢严重得多**。
    /// 带参且每帧调用的点，必须在**调用方**按自己的失效键（快照 revision + 语言）缓存。</para>
    /// </summary>
    internal static partial class UIComponents
    {
        /// <summary>
        /// 键 → 当前语言下的成品文案。
        ///
        /// <para><b>有界</b>：键来自代码里的字面量（本工程 <c>PersonalChronicle.*</c> 命名空间），
        /// 数量是固定的 ⇒ 正常路径下不会增长。仍设 <see cref="LocCacheCapacity"/> 上限兜底 ——
        /// 万一有人传了动态键（DefName / 拼接串），超出即整表清空，绝不无界增长
        /// （代价只是重新翻译，不会泄漏）。</para>
        /// </summary>
        private static readonly Dictionary<string, string> locCache = new Dictionary<string, string>();

        /// <summary>缓存建立时所用的语言标识（<c>LoadedLanguage.folderName</c>；null 语言记空串）。</summary>
        private static string locCacheLanguage;

        /// <summary>缓存容量上限（超出即清表重建，见 <see cref="locCache"/> 的说明）。</summary>
        private const int LocCacheCapacity = 512;

        /// <summary>
        /// 取一条**无参** Keyed 文案（按当前语言缓存）。
        ///
        /// <para><b>⚠ 只接受无参键</b>：见类注释 —— 有参翻译必须留在调用方。</para>
        ///
        /// <para><b>⚠ 键缺失时返回键本身**并同样缓存**</b>：这是 <c>Translate()</c> 的原生行为
        /// （RimWorld 找不到 Keyed 时回退成键名），缓存它不会改变语义。副作用是：
        /// 若某个键在运行期才被别的 Mod 注入，本缓存不会察觉 —— 但本缓存只服务本工程自己的
        /// <c>PersonalChronicle.*</c> 键，不在运行期变化，故可接受。</para>
        ///
        /// <para><b>⚠ 单线程假设</b>：RimWorld 的绘制与语言库都在主线程访问，本表无锁。
        /// 一旦将来有后台线程要读，必须先加同步（现在的调用者全在 <c>OnGUI</c> 路径上）。</para>
        /// </summary>
        internal static string Loc(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            // 语言变更 ⇒ 整表作废（不复用旧值：混语言的界面比慢一帧更糟）。
            // 语言标识用 folderName 而非 LoadedLanguage 引用：它与既有两处先例同源
            // （UIComponents.ThemeSwitch.EnsureThemeTexts / MasterControlTabWindow.Population 的绘制缓存）。
            string language = LanguageDatabase.activeLanguage == null
                ? string.Empty
                : LanguageDatabase.activeLanguage.folderName;
            if (!string.Equals(locCacheLanguage, language, System.StringComparison.Ordinal))
            {
                locCache.Clear();
                locCacheLanguage = language;
            }

            string cached;
            if (locCache.TryGetValue(key, out cached))
            {
                return cached;
            }

            string text = key.Translate().ToString();

            if (locCache.Count >= LocCacheCapacity)
            {
                // 兜底：动态键把表撑满 ⇒ 清空重建（有界优先，代价只是重新翻译一轮）。
                locCache.Clear();
            }
            locCache[key] = text;
            return text;
        }
    }
}
