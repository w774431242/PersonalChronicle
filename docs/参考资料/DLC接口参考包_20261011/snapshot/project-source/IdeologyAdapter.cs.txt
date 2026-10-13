using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace PersonalChronicle.Compatibility.Dlc
{
    /// <summary>
    /// **Ideology DLC 适配器**（COMP-001 / COMP-006 / GOV-008）。
    ///
    /// <para><b>为什么需要它</b>：2026-09-15 全栈审计（`pc-compat-auditor`）判定 —— DLC 类型
    /// <c>Ideo</c> / <c>MemeDef</c> / <c>Precept</c> 此前**直接出现在 `Application` 编排层**
    /// （`BuiltinPopulationProvider.Overview.cs` / `…Derived.cs`），而规范要求第三方 / DLC 类型
    /// 「只能出现在 Compatibility 适配器、检测器与局部 Patch 中；Adapter 必须向 Core 转换稳定数据后
    /// **立即隔离外部类型**」。</para>
    ///
    /// <para><b>契约</b>：本类**只向 Core 回传值类型**（`string` / `List&lt;string&gt;` /
    /// `Dictionary&lt;string,int&gt;`）—— 调用方（Provider）永不接触 `Ideo`/`MemeDef`/`Precept`。
    /// 参数与返回值只用核心类型（`Pawn` 属常驻 `Assembly-CSharp`，非 DLC 类型）。</para>
    ///
    /// <para><b>运行时安全性</b>（审计已核实，此处以代码固化）：
    /// ① `Ideo` 等类型由**常驻** `Assembly-CSharp.dll` 提供（`Managed/` 下无 DLC 独立程序集）
    /// ⇒ 未装 DLC **不会** `TypeLoadException`；
    /// ② 所有入口先判 <see cref="Available"/>（`ModsConfig.IdeologyActive` 能力探测）；
    /// ③ `Pawn.Ideo` 的原版 getter 在 tracker 为 null 时返回 null（不抛）⇒ 调用点再次判空。</para>
    ///
    /// <para><b>隔离手段</b>：每个 DLC 相关入口标 <see cref="MethodImplOptions.NoInlining"/> —— 把
    /// 「引用 DLC 类型的方法体」挡在独立栈帧内，避免 JIT 内联把类型引用扩散到调用方（COMP-008 惯例）。</para>
    /// </summary>
    public static class IdeologyAdapter
    {
        /// <summary>DLC 能力探测（`ModsConfig.IdeologyActive`）。未启用时所有入口返回空值，不抛。</summary>
        public static bool Available
        {
            get { return ModsConfig.IdeologyActive; }
        }

        /// <summary>Meme 摘要最多取几个（与预览「核心理念」chips 一致）。</summary>
        private const int MaxMemes = 3;

        /// <summary>
        /// 取该 pawn 的**理念名**（值类型回传）。无理念 / 未装 DLC / 名为空 ⇒ 返回 <c>null</c>。
        /// ⚠ 返回 null 与「无信仰」是**不同语义**：调用方据此区分「计入某理念」与「计入无信仰」。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string IdeologyNameOf(Pawn pawn)
        {
            if (!Available || pawn == null)
            {
                return null;
            }
            Ideo ideo = pawn.Ideo;
            if (ideo == null)
            {
                return null;
            }
            return string.IsNullOrEmpty(ideo.name) ? "?" : ideo.name;
        }

        /// <summary>
        /// 取该 pawn 理念的 **Meme 摘要**（`" · "` 拼接，最多 3 个；无则空串），并可选把**逐项标签**
        /// 追加进 <paramref name="chipsOut"/>（供 UI「核心理念」chips 一行绘制，构建期拆分 ⇒ 绘制期零分配）。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string MemeSummaryOf(Pawn pawn, List<string> chipsOut)
        {
            if (!Available || pawn == null)
            {
                return string.Empty;
            }
            Ideo ideo = pawn.Ideo;
            if (ideo == null || ideo.memes == null || ideo.memes.Count == 0)
            {
                return string.Empty;
            }
            string text = string.Empty;
            int count = System.Math.Min(MaxMemes, ideo.memes.Count);
            for (int i = 0; i < count; i++)
            {
                MemeDef meme = ideo.memes[i];
                if (meme == null)
                {
                    continue;
                }
                string label = meme.LabelCap.ToString();
                if (text.Length > 0)
                {
                    text += " · ";
                }
                text += label;
                if (chipsOut != null)
                {
                    chipsOut.Add(label);
                }
            }
            return text;
        }

        /// <summary>
        /// 把该 pawn 理念的**戒律 defName** 计入 <paramref name="counts"/>（按 def 名聚合，供「戒律榜」）。
        /// 无理念 / 未装 DLC ⇒ 零操作。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void AccumulatePreceptsOf(Pawn pawn, Dictionary<string, int> counts)
        {
            if (!Available || pawn == null || counts == null)
            {
                return;
            }
            Ideo ideo = pawn.Ideo;
            if (ideo == null)
            {
                return;
            }
            List<Precept> precepts = ideo.PreceptsListForReading;
            if (precepts == null)
            {
                return;
            }
            for (int i = 0; i < precepts.Count; i++)
            {
                Precept precept = precepts[i];
                if (precept == null || precept.def == null || string.IsNullOrEmpty(precept.def.defName))
                {
                    continue;
                }
                int seen;
                counts.TryGetValue(precept.def.defName, out seen);
                counts[precept.def.defName] = seen + 1;
            }
        }
    }
}
