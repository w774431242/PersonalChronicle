using System.Collections.Generic;
using HarmonyLib;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// CHG-20261004-WORKTAB-HIDE · EXC-2026-017(E2) —— 从工作优先级面板隐藏指定殖民者。
    ///
    /// <para><b>目标</b>：<c>MainTabWindow_Work.get_Pawns</c>。该成员是 <c>protected virtual</c>，
    /// 外部代码无法 <c>nameof</c> ⇒ HarmonyPatch 必须写<strong>字面量</strong> <c>"get_Pawns"</c>。</para>
    ///
    /// <para><b>铁律：只换引用，绝不原地改 <c>__result</c>。</b>
    /// IL 实测链路：<c>MainTabWindow_Work.get_Pawns</c> 返回的是 <c>base.Pawns.Where(...)</c>
    /// 的<strong>惰性 IEnumerable</strong>；而基类 <c>get_Pawns</c> 返回的
    /// <c>MapPawns.FreeColonists</c> 是 MapPawns <b>内部复用的缓存 List</b>，全库 <b>63 处</b>消费
    /// （ColonistBar / 床位分配 / 任务生成 / 强度评级…）。对它 RemoveAll 会污染原版缓存 ⇒ 连锁出错。</para>
    ///
    /// <para><b>fail-open</b>：名单为空、组件缺失或任何异常 ⇒ 原样返回，
    /// 功能退化为「不隐藏任何人」，绝不出现空表 / 崩溃 / 改上游。</para>
    ///
    /// <para><b>为什么 Postfix 而不是 Prefix</b>：Postfix 保留原版全部过滤逻辑
    /// （当前是 <c>!DevelopmentalStage.Baby()</c>），未来原版新增过滤条件会被自动继承；
    /// Prefix 需要复刻整条谓词链，一旦原版加条件就会静默漏掉（COMP-005）。</para>
    /// </summary>
    [HarmonyPatch(typeof(MainTabWindow_Work), "get_Pawns")]
    [HarmonyPriority(Priority.Low)]
    public static class Patch_MainTabWindowWorkPawns
    {
        static void Postfix(ref IEnumerable<Pawn> __result)
        {
            try
            {
                // 会话级「临时显示全部」开关打开时，完全不介入。
                if (RevealAll) return;

                Data.ChronicleGameComponent component =
                    Current.Game?.GetComponent<Data.ChronicleGameComponent>();
                if (component == null) return;

                // ⚠ CHG-20261005-NODEMOTE：隐藏名单已**分账**（玩家手动 ∪ PC 自动），
                //   判定必须查两处 —— 否则 PC 自动隐藏的那批会"藏了不生效"（面板照样显示）。
                HashSet<string> manual = component.HiddenWorkTabColonists;
                HashSet<string> auto = component.AutoHiddenWorkTabColonists;
                if ((manual == null || manual.Count == 0) && (auto == null || auto.Count == 0))
                {
                    return;
                }

                // 手写循环，不用 LINQ（PERF-004：闭包 + 迭代器分配在每次表格重算时都要付）。
                List<Pawn> kept = new List<Pawn>();
                foreach (Pawn pawn in __result)
                {
                    if (pawn == null) continue;
                    if (component.IsHiddenFromWorkTab(pawn)) continue;   // 并集判定（单一实现）
                    kept.Add(pawn);
                }

                // 只换引用 —— 见类注释「铁律」。
                __result = kept;
            }
            catch (System.Exception)
            {
                // fail-open：绝不让一次失败把原版工作面板变成空表或抛到调用方。
            }
        }

        /// <summary>
        /// 会话级「临时显示全部已隐藏者」开关。语义是<strong>临时查看</strong>，
        /// 因此<strong>不落盘、不进存档</strong>，避免变成常驻状态。
        /// </summary>
        public static bool RevealAll { get; private set; }

        public static void SetRevealAll(bool value)
        {
            if (RevealAll == value) return;
            RevealAll = value;
            Data.ChronicleGameComponent.NotifyWorkTabChanged();
        }

        /// <summary>
        /// 跨档防污染：读档后强制回到「不显示全部」。
        /// 与既有 <c>Capture.Patch_PawnTakeDamage.Reset()</c> 同一模式
        /// （由 <c>ChronicleGameComponent.FinalizeInit</c> 调用）。
        /// </summary>
        public static void Reset()
        {
            RevealAll = false;
        }
    }
}
