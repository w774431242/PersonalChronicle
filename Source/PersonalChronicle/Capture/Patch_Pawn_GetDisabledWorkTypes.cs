using HarmonyLib;
using PersonalChronicle.Automation;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// CHG-20261004-EMPLOYEE-ACCESS（EXC-2026-025）：车间在编员工可从事被原版禁用
    /// （背景故事 / 特质）的、且属于其车间类型映射的原版工作类型。
    ///
    /// <para><b>为什么挂在 `GetDisabledWorkTypes` 而不是 `WorkTypeIsDisabled`</b>
    /// （2026-10-04 实机报错修复，**推翻上一版实现**）：</para>
    /// <code>
    /// // 原版
    /// public bool WorkTypeIsDisabled(WorkTypeDef w) =&gt; GetDisabledWorkTypes(false).Contains(w);
    /// </code>
    /// <para>`WorkTypeIsDisabled` 只是 `GetDisabledWorkTypes` 的**一个**消费点。Cecil `refs` 显示
    /// 后者还有 <b>13 个直调点</b>，其中至少三个会与本功能产生**可观测矛盾**：</para>
    /// <list type="bullet">
    /// <item><c>SkillRecord.CalculateTotallyDisabled</c> / <c>CalculatePermanentlyDisabled</c>
    /// —— 决定"该技能是否完全不可用"。它**绕过** `WorkTypeIsDisabled` ⇒ 只 patch 后者时，
    /// 工人虽被允许烹饪，但其 `Cooking` 技能仍被判为 `TotallyDisabled`。</item>
    /// <item>⇒ 连锁后果（实机红字，玩家 2026-10-04 提供）：
    /// <c>StatWorker.IsDisabledFor</c>（IL：`foreach (SkillNeed sn in stat.skillNeedFactors)
    /// if (sn.required &amp;&amp; pawn.skills.GetSkill(sn.skill).TotallyDisabled) return true;`）
    /// 会对 <c>FoodPoisonChance</c>（带 `skillNeedFactors(Cooking)`）返回 <b>true</b>
    /// ⇒ 该 pawn 做完饭时 <c>CompFoodPoisonable.Notify_RecipeProduced</c> 求该 stat
    /// ⇒ <c>StatWorker.GetValueUnfinalized</c> 打红字
    /// 「<c>Attempted to calculate value for disabled stat FoodPoisonChance …</c>」
    /// （thing= 做饭的那名殖民者）。</item>
    /// <item><c>Pawn_WorkSettings.EnableAndInitialize</c> / <c>SetPriority</c> / <c>ExposeData</c>
    /// —— 档位与存档记录的启用集合。</item>
    /// </list>
    ///
    /// <para><b>为什么是 Postfix 且改 `__result`</b>：原版对该结果有**缓存**
    /// （`Pawn.cachedDisabledWorkTypes`，由 `Notify_DisabledWorkTypesChanged` 失效重建）
    /// ⇒ Postfix 只在**缓存重建时**运行，**比每调用一次的 Prefix 更便宜**；
    /// 而把授权项从缓存里移除正是我们想要的语义（"这个 pawn 对该类型确实不再被禁用"），
    /// 缓存失效后重算会再次应用同一授权 ⇒ 全程一致。</para>
    ///
    /// <para><b>影响面控制</b>：只移除同时满足两个条件的项，其余一律原样返回 ——</para>
    /// <list type="number">
    /// <item><see cref="MapComponent_WorkshopManager.DisabledByStoryOrTrait"/>：
    /// 该类型确实被**背景故事或特质**禁用（正向确认 ⇒ 年龄/健康/奴隶/变异/任务/爵位/
    /// 意识形态角色造成的禁用**天然不会被移除**）；</item>
    /// <item><see cref="MapComponent_WorkshopManager.EmployeeAccessAllows"/>：该 pawn 是某车间
    /// 在编成员、该车间已指定类型、且该类型是它的映射类型。</item>
    /// </list>
    /// <para>⇒ 把人移出车间名单，下一轮缓存重建即恢复原版限制（无需开关、无需重启）。</para>
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetDisabledWorkTypes))]
    internal static class Patch_Pawn_GetDisabledWorkTypes
    {
        /// <summary>
        /// 第二参数用 <c>__0</c>（按位置）而非参数名 —— 原版该参数名未取证，
        /// 而 Harmony 按参数名注入，名字不符会静默失败。
        /// </summary>
        private static void Postfix(Pawn __instance, bool __0, List<WorkTypeDef> __result)
        {
            if (__instance == null || __result == null || __result.Count == 0)
            {
                return;
            }
            // 倒序移除（避免索引位移）。
            for (int i = __result.Count - 1; i >= 0; i--)
            {
                WorkTypeDef workType = __result[i];
                if (workType == null)
                {
                    continue;
                }
                if (!MapComponent_WorkshopManager.DisabledByStoryOrTrait(__instance, workType))
                {
                    continue;   // 非"来历性"禁用 ⇒ 不动（年龄/健康/奴隶/任务/…）
                }
                if (!MapComponent_WorkshopManager.EmployeeAccessAllows(__instance, workType))
                {
                    continue;   // 该 pawn 未获本车间授权 ⇒ 不动
                }
                __result.RemoveAt(i);
            }
        }
    }
}
