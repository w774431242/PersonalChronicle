using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// CHG-20261004-MEALSCHEDULE-B · 在管制/作息表的时段选择器里追加「吃饭」按钮。
    ///
    /// <para><b>为什么必须 Harmony（EXC-2026-022）</b>：原版
    /// <c>RimWorld.TimeAssignmentSelector.DrawTimeAssignmentSelectorGrid</c> 的按钮是
    /// <b>硬编码 5 项</b>（Cecil IL 逐条确认）：
    /// <code>
    /// DrawTimeAssignmentSelectorFor(half, TimeAssignmentDefOf.Anything)
    /// half.x += half.width; … Work / Joy / Sleep
    /// if (ModsConfig.RoyaltyActive) { half.x += half.width; … Meditate }
    /// </code>
    /// 它<b>不遍历 <c>DefDatabase&lt;TimeAssignmentDef&gt;</c></b>，因此新增 Def（PC_Meal）
    /// 会自动获得格子涂色与存读档支持，却<b>没有按钮</b>。原版没有留任何 XML 扩展点
    /// （无 TimetableDef 列表、无 DME），Postfix 是唯一最小改法。</para>
    ///
    /// <para><b>为什么选 Postfix 而不是 Prefix 重画</b>：原版方法无返回值、布局逻辑简单
    /// （见下方 <see cref="MealButtonRect"/> 的复刻），Postfix 追加可完整保留原版 5 个按钮的
    /// 行为（含 <c>UIHighlighter</c> 教学高亮与选中框），风险面最小。</para>
    ///
    /// <para><b>点击链路完全复用原版</b>：点击只写
    /// <c>TimeAssignmentSelector.selectedAssignment</c>（public static），
    /// 之后由原版 <c>PawnColumnWorker_Timetable.DoTimeAssignment</c> 完成
    /// 「把这一小时的 <c>Pawn_TimetableTracker.times</c> 写成本 Def」与存档，
    /// PC 侧零重复实现（GOV-005）。</para>
    /// </summary>
    [HarmonyPatch(typeof(TimeAssignmentSelector), nameof(TimeAssignmentSelector.DrawTimeAssignmentSelectorGrid))]
    internal static class Patch_TimeAssignmentSelector
    {
        /// <summary>缓存的 PC_Meal Def（缺失时按钮不画，只告警一次）。</summary>
        private static TimeAssignmentDef cachedMeal;
        private static bool warnedMissing;

        /// <summary>
        /// 原版按钮的固定内缩（<c>DrawTimeAssignmentSelectorFor</c> 首行
        /// <c>rect = rect.ContractedBy(2)</c>，IL 实证）。
        /// </summary>
        private const float ButtonInset = 2f;

        private static void Postfix(Rect rect)
        {
            TimeAssignmentDef meal = ResolveMealAssignment();
            if (meal == null)
            {
                return;
            }

            Rect button = MealButtonRect(rect);
            if (!button.width.Equals(0f) && !button.height.Equals(0f))
            {
                DrawAssignmentButton(button, meal);
            }
        }

        /// <summary>
        /// 复刻原版布局以定位「原版 5 个按钮之后」的那个格子。
        ///
        /// <para>原版算法（IL 逐条抄录）：<c>rect.yMax -= 2</c> →
        /// <c>half.xMax = half.center.x; half.yMax = half.center.y</c>（即取左上四分之一）
        /// → 每个按钮画完后 <c>half.x += half.width</c>。
        /// 于是第 N 个按钮的 rect = (x + N·w, y, w, h)，w = rect.width/2、h = rect.height/2。</para>
        /// </summary>
        private static Rect MealButtonRect(Rect rect)
        {
            Rect area = rect;
            area.yMax -= ButtonInset;

            float width = area.width / 2f;
            float height = area.height / 2f;
            if (width <= 0f || height <= 0f)
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            // 原版按钮数：Anything / Work / Joy / Sleep +（Royalty 激活时）Meditate。
            int vanillaCount = ModsConfig.RoyaltyActive ? 5 : 4;
            return new Rect(area.x + vanillaCount * width, area.y, width, height);
        }

        /// <summary>
        /// 画一个时段按钮（与原版 <c>DrawTimeAssignmentSelectorFor</c> 逐项同构）。
        ///
        /// <para><b>文字必须居中</b>（2026-10-04 实机对齐）：原版 IL 是
        /// <c>ldloca.s V_0; ldc.i4.4; call TextBlock::.ctor</c> 之后才画 Label，
        /// 即 <c>TextAnchor = 4 = MiddleCenter</c>。直接调 <c>Widgets.Label(rect, text)</c>
        /// 会用默认左对齐 ⇒ 文字贴左、与原版 5 个按钮视觉不一致（实机截图确认）。</para>
        ///
        /// <para>唯一刻意差异：不调用 <c>UIHighlighter.HighlightOpportunity</c>（它需要读
        /// <c>TimeAssignmentDef.cachedHighlightNotSelectedTag</c>，该字段是 private，
        /// 且只影响首次教学高亮，无功能价值）。</para>
        /// </summary>
        private static void DrawAssignmentButton(Rect rect, TimeAssignmentDef assignment)
        {
            Rect inner = rect.ContractedBy(ButtonInset);
            GUI.DrawTexture(inner, assignment.ColorTexture);
            if (Widgets.ButtonInvisible(inner, true))
            {
                TimeAssignmentSelector.selectedAssignment = assignment;
                SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
            }
            if (Mouse.IsOver(inner))
            {
                Widgets.DrawHighlight(inner);
            }
            // TextBlock 是 struct + IDisposable（存/改/恢复 GUI 的 font/anchor/wordWrap/color），
            // using 即等价于原版的 ref TextBlock 参数写法。
            using (new TextBlock(TextAnchor.MiddleCenter))
            {
                Widgets.Label(inner, assignment.LabelCap);
            }
            if (TimeAssignmentSelector.selectedAssignment == assignment)
            {
                Widgets.DrawBox(inner, 2);
            }
        }

        private static TimeAssignmentDef ResolveMealAssignment()
        {
            if (cachedMeal != null)
            {
                return cachedMeal;
            }
            cachedMeal = DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(
                Domain.Meal.MealScheduleDefaults.MealTimeAssignmentDefName);
            if (cachedMeal == null && !warnedMissing)
            {
                // Def 缺失只影响按钮可见性，不得打断 UI 绘制（COMP-011 降级）。
                warnedMissing = true;
                Log.Warning("[PersonalChronicle] Meal time-assignment not found (button hidden): "
                    + Domain.Meal.MealScheduleDefaults.MealTimeAssignmentDefName);
            }
            return cachedMeal;
        }
    }
}