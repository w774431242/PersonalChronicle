using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// CHG-20261004-WORKTAB-HIDE · EXC-2026-018(E2) —— 隐藏 / 恢复的操作入口。
    ///
    /// <para><b>目标</b>：<c>Pawn.GetGizmos()</c>（<c>public virtual</c>，故可用 <c>nameof</c>，无裸字符串）。
    /// 追加语义（Postfix），不替换、不阻止任何既有 Gizmo。</para>
    ///
    /// <para><b>IL 实测约束</b>：<c>GetGizmos</c> 是<strong>迭代器状态机</strong>，
    /// 返回值不是 <c>List&lt;Gizmo&gt;</c> ⇒ 禁止 <c>as IList&lt;Gizmo&gt;().Add()</c>（会 InvalidCastException），
    /// 必须构造新序列后换引用。</para>
    ///
    /// <para><b>为什么需要它</b>：RimWorld 没有任何 Def/XML 手段能为 Pawn 追加运行时 Gizmo；
    /// 备选 <c>CompPawn + CompGetGizmosExtra</c> 需要给所有人类 Pawn 挂 Comp，侵入面更大。</para>
    ///
    /// <para><b>fail-open</b>：异常 ⇒ 只是没有这个按钮，隐藏名单本身不受影响。</para>
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    [HarmonyPriority(Priority.Low)]
    // 2026-10-04：Player.log 报 "Type Patch_PawnWorkTabGizmo probably needs a StaticConstructorOnStartup
    // attribute, because it has a field HideIcon of type Texture2D" —— 静态字段初始化不在主线程，
    // 纹理可能未加载 ⇒ 隐藏按钮的图标变 null。补上该特性让初始化回到主线程。
    [StaticConstructorOnStartup]
    public static class Patch_PawnWorkTabGizmo
    {
        /// <summary>
        /// 图标一律取原版资源 + 显式兜底（与工程既有 <c>Command_Action</c> 用法一致，
        /// 例如 <c>CompItemPort</c> 的 <c>UI/Buttons/Outfit ?? TexButton.OpenStatsReport</c>）。
        /// 第二参数 <c>false</c> = 路径缺失返回 null 而不抛。
        /// </summary>
        private static readonly Texture2D HideIcon =
            ContentFinder<Texture2D>.Get("UI/Designators/HideEverything", false)
            ?? ContentFinder<Texture2D>.Get("UI/Buttons/Drop", false)
            ?? TexButton.OpenStatsReport;

        private static readonly Texture2D ShowIcon =
            ContentFinder<Texture2D>.Get("UI/Buttons/Visible", false)
            ?? ContentFinder<Texture2D>.Get("UI/Designators/Deconstruct", false)
            ?? TexButton.OpenStatsReport;

        static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            try
            {
                if (__instance == null) return;
                if (__instance.Dead) return;
                if (__instance.Faction == null || !__instance.Faction.IsPlayer) return;
                // 婴儿本来就不出现在工作面板，给它按钮会让玩家困惑。
                if (__instance.DevelopmentalStage.Baby()) return;

                Data.ChronicleGameComponent component =
                    Current.Game?.GetComponent<Data.ChronicleGameComponent>();
                if (component == null) return;

                bool hidden = component.IsHiddenFromWorkTab(__instance);

                Command_Action toggle = new Command_Action
                {
                    defaultLabel = Tr(hidden
                        ? "PersonalChronicle.WorkTab.Show"
                        : "PersonalChronicle.WorkTab.Hide"),
                    defaultDesc = Tr(hidden
                        ? "PersonalChronicle.WorkTab.ShowDesc"
                        : "PersonalChronicle.WorkTab.HideDesc"),
                    icon = hidden ? ShowIcon : HideIcon,
                    action = () =>
                    {
                        // 再取一次组件：动作发生在游戏帧里，持有捕获的组件引用可能已跨档失效。
                        Data.ChronicleGameComponent live =
                            Current.Game?.GetComponent<Data.ChronicleGameComponent>();
                        if (live == null) return;
                        live.ToggleHiddenFromWorkTab(__instance);
                    }
                };

                __result = Append(__result, toggle);
            }
            catch (System.Exception)
            {
                // fail-open：只是没有这个按钮，隐藏功能本身不受影响。
            }
        }

        /// <summary>换引用式追加（<c>GetGizmos</c> 是迭代器，禁止原地 Add）。</summary>
        private static IEnumerable<Gizmo> Append(IEnumerable<Gizmo> source, Gizmo extra)
        {
            foreach (Gizmo gizmo in source)
            {
                yield return gizmo;
            }
            yield return extra;
        }

        private static string Tr(string key)
        {
            return Translator.Translate(key);
        }
    }
}
