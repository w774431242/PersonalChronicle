using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Governance;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// 制度页共用标签（唯一实现）：稳定标识（枚举名/原因码）优先走本地化键，缺失时按固定顺序回落到
    /// 另一命名空间，仍缺失则显示标识本身 —— 永不空白、永不臆造文本（GOV-010）。
    /// 两个命名空间：服务/照护标签在 <c>PC_PEA.Care.*</c>，政策/制度标签在 <c>PC_PEA.Policy.*</c>。
    /// 经济首页（<c>GameComponent_PawnEconomy.BuildInstitutions</c>）与政策页此前各写一份，
    /// 现在只此一处 —— 同一种标签不会再出现两种回落行为。
    ///
    /// <para><b>P2-3 加固（2026-10-10）</b>：新增**未解析键自证**（<see cref="LooksLikeUnresolvedKey"/>）与
    /// **带参格式化入口**（<see cref="Formatted"/>）。前者防"译文本身是键形串"（被覆盖/机翻注入的语言字典），
    /// 后者替代 1.6 已 <c>[Obsolete]</c> 且缺键即打裸键的原版 <c>Translate(params object[])</c>。</para>
    /// </summary>
    internal static class EconomyLabels
    {
        /// <summary>短键显示（取尾部 14 字符；空 ⇒ "-"）。CHG-20261010-ECON-AGG-MIGRATE 批次 A-2 前置：
        /// 从聚合窗口政策页**提取为共享唯一实现** —— 照护等内容层搬迁时直接复用，不再复制第二份。</summary>
        public static string ShortKey(string key)
            => string.IsNullOrEmpty(key) ? "-" : key.Length <= 14 ? key : key.Substring(key.Length - 14);

        /// <summary>minor 金额的显示文本。当前实现即原样 <c>ToString()</c>（与政策页原实现逐字一致）；
        /// 提取为共享入口以便未来统一（若将来要加千分位等格式，只改这一处）。</summary>
        public static string FormatMinor(long minor) => minor.ToString();

        /// <summary>银两显示（minor → 按 PerSilver 换算的两位小数）。CHG-20261010-ECON-AGG-MIGRATE：
        /// 从政策页提取为共享唯一实现（总览内容层搬迁时需要它，不应复制第二份）。</summary>
        public static string FormatSilver(long minor)
            => (minor / (double)PersonalChronicle.Domain.PawnEconomy.Finance.MinorCurrency.PerSilver).ToString("0.##");

        /// <summary>生产类别标签：Def 存在用其 DefInjected 标签，否则回落稳定 ID
        /// （绝不出现第二套命名，GOV-009/010）。</summary>
        public static string CategoryLabel(string category)
        {
            var def = Verse.DefDatabase<Verse.ThingDef>.GetNamedSilentFail(category);
            return def != null ? def.LabelCap.ToString() : category;
        }

        /// <summary>诊断 / 原因码标签（稳定 ID → 键；未映射回落 ID 本身，永不空白、永不臆造）。</summary>
        public static string ReasonLabel(string reason)
            => string.IsNullOrEmpty(reason) ? string.Empty : LocalizedId("Reason.", reason);

        public static string LocalizedId(string group, string name)
        {
            string careKey = "PC_PEA.Care." + group + name;
            string text = careKey.Translate();
            if (Resolved(text, careKey)) return text;
            string policyKey = "PC_PEA.Policy." + group + name;
            text = policyKey.Translate();
            if (Resolved(text, policyKey)) return text;
            // ⚠ 只有**两次探测都未命中**才记警告：首个命名空间未命中是**预期**行为（同一标识可能只登记在另一个
            // 命名空间）。若在首次探测就报警，实机 Debug log 会刷出成片假警报 —— 2026-10-10 实机截图实证：
            // 8 条 Tool 键（`PC_PEA.Care.Tool.*` 未命中但 `PC_PEA.Policy.Tool.*` 命中）全部误报。
            WarnUnresolved(careKey + " | " + policyKey, text);
            return name;
        }
        public static string KindLabel(InstitutionKind kind) => LocalizedId("Kind.", kind.ToString());
        /// <summary>制度就绪门的**门名**文案（唯一实现）。<paramref name="blockerCode"/> 取自
        /// <c>InstitutionReadiness.Missing()</c> 的稳定码；与经济页 blocker 行同源同键。</summary>
        public static string GateBlocker(string blockerCode) => GateText("Mode.Blocker.", blockerCode);
        /// <summary>制度就绪门的**下一步操作**文案（唯一实现）：玩家据此把制度推进到可用。
        /// 与经济页 blocker 行共用同一条组装 —— 同一句话不会再出现两种回落行为（BASE-005 / GOV-010）。</summary>
        public static string GateStep(string blockerCode) => GateText("Mode.Step.", blockerCode);
        private static string GateText(string group, string blockerCode)
            => string.IsNullOrEmpty(blockerCode) ? string.Empty
                : TranslatedOrId("PC_PEA." + group + blockerCode, group + blockerCode);
        // CHG-20261012 · B2：`CodeLabel(PolicyCode)` 已删 —— 政策结果码随机构政策整体退役，
        // 该映射没有任何调用者（保留它只会让 `PC_PEA.Policy.Code.*` 一组键看起来"还在用"）。
        /// <summary>
        /// **整键**翻译：命中即返回译文，缺键返回 <paramref name="fallbackId"/>（永不把完整键名打上屏）。
        /// 与 <see cref="LocalizedId"/> 是同一条回落纪律（唯一实现），差别只是输入是**完整键**而不是
        /// (命名空间组, 稳定标识) 两段。经济总览的 <c>O(suffix)</c> 委托这里，因此缺键页签显示的是
        /// 标识本身（可读）而不是 <c>PC_PEA.Overview.Xxx</c>（裸键）。
        /// </summary>
        public static string TranslatedOrId(string key, string fallbackId)
        {
            string text = key.Translate();
            if (Resolved(text, key)) return text;
            WarnUnresolved(key, text);
            return fallbackId;
        }
        /// <summary>
        /// **带位置参数的整键翻译（唯一入口）**：<c>string.Format(回落后的文本, args)</c>。
        /// <para>为什么不用原版 <c>Translate(this string, params object[] args)</c>：① 1.6 已把它标为
        /// <c>[Obsolete("Use TranslatorFormattedStringExtensions")]</c>；② 它缺键时把**带 <c>{0}</c> 的裸键**
        /// 直接打上屏（`PC_PEA.Treasury.Funds {0} …`）。本入口同时补上回落，并吞掉格式异常
        /// （坏译文不得把异常抛进 UI 绘制循环）。</para>
        /// <para>参数仍为**位置**占位（现有语言文件用 <c>{0}</c> 等）；具名参数迁移是独立议题（改文案次序即静默错位）。</para>
        /// </summary>
        public static string Formatted(string key, string fallbackId, params object[] args)
        {
            string text = TranslatedOrId(key, fallbackId);
            if (args == null || args.Length == 0) return text;
            try
            {
                return string.Format(text, args);
            }
            catch (System.FormatException)
            {
                Log.WarningOnce("[PC][LOC] format failed: " + key + " / " + text, key.GetHashCode() & 0x7fffffff);
                return text;
            }
        }

        /// <summary>
        /// **未解析键自证**：若"译文"本身仍以 <c>PC_PEA.</c> 开头，它就不是译文 —— 一律按缺键处理并回落。
        ///
        /// <para><b>为什么需要它</b>：P1 标注登记的「政策页裸键 <c>PC_PEA.Cap.Tool.&lt;hash&gt;</c>」经全机取证
        /// （源码/Defs/语言文件/全部 Mod xml 与非 xml 字典/全部第三方 DLL/全部历史部署 DLL/部署件 IL）判定：
        /// 本工程任何构建都产不出该串 —— <c>LocalizedId</c> 缺键只返回枚举名。唯一可解释机制是**活动语言字典把某个
        /// 我们请求的键的"译文"写成了键形串**（外部词典覆盖 / 机翻注入）。本判定对该形态**无条件生效**，
        /// 与定责无关：只要译文形如键，就回落显示稳定标识。</para>
        /// </summary>
        internal static bool LooksLikeUnresolvedKey(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            // ⚠ 只允许**最古老**的 BCL 重载：本工程目标运行时是 RimWorld 自带的 Mono mscorlib，
            // `text.TrimStart()`（无参重载）在该运行时上抛 MissingMethodException —— 由离线测试实测捕获
            // （2026-10-10）。`Trim()` 与 `StartsWith(string, StringComparison)` 到处都有。
            return text.Trim().StartsWith("PC_PEA.", System.StringComparison.Ordinal);
        }

        /// <summary>**命中判定**（唯一实现）：译文既不是键名本身、也不形如键 ⇒ 才算命中。</summary>
        private static bool Resolved(string text, string key)
            => text != key && !LooksLikeUnresolvedKey(text);

        /// <summary>未解析告警（唯一出口）：每条键最多一次（键位取自键名哈希 ⇒ 无需静态可变状态）。</summary>
        private static void WarnUnresolved(string key, string text)
            => Log.WarningOnce("[PC][LOC] unresolved key: " + key + " -> " + (text ?? "null"),
                key.GetHashCode() & 0x7fffffff);
    }
}
