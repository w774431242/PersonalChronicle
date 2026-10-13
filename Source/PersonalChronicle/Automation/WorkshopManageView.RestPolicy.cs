using System.Collections.Generic;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using UnityEngine;
using Verse;
namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 排班页的「作息策略」区块（CHG-20261003-WORKSHOP-SPLIT · 修成因 A 的<b>玩家可见入口</b>）。
    ///
    /// <para><b>MATRIX-003 纪律</b>：本文件<b>只发命令</b>（写 <c>zone.RestPolicyDefName</c>
    /// + 调 <c>Manager.SyncVanillaTimetables</c>），<b>不写任何 timetable</b>，
    /// 也不做任何判定 —— 判定全在 Domain，写入全在 <see cref="WorkshopDutyCoordinator"/>。</para>
    ///
    /// <para><b>为什么必须有这个入口</b>：默认策略从 RotateDaily（旧行为，会翻转睡眠时段）
    /// 切到 FixedPerShift 是<b>行为变更</b>，玩家需要一个能看见、能改、能理解代价的地方。
    /// 尤其 RotateDaily 必须<b>显式警示</b>（GOV-010 + 缺陷 A 的根因不得静默）。</para>
    /// </summary>
    internal sealed partial class WorkshopManageView
    {
        private const float RestPolicyRowH = 24f;

        /// <summary>
        /// 绘制作息策略行（返回占用高度）。在排班页汇总行之下调用。
        /// </summary>
        public float DrawRestPolicyRow(Rect rect)
        {
            if (zone == null)
            {
                return 0f;
            }
            List<WorkshopRestPolicyDef> options = SelectableRestPolicies();
            if (options.Count == 0)
            {
                return 0f;
            }

            WorkshopRestPolicyDef current = WorkshopDutyCoordinator.PolicyOf(zone);
            int currentIndex = IndexOf(options, current);

            float y = rect.y;
            // ---- 标签 + 说明 ----
            UIComponents.Label(new Rect(rect.x, y, 120f, RestPolicyRowH),
                T("RestPolicy"), UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);
            float descX = rect.x + 124f;
            float descW = rect.width - 124f - 220f;
            if (descW > 40f && current != null)
            {
                string desc = current.descKey.Translate();
                UIComponents.Label(new Rect(descX, y, descW, RestPolicyRowH),
                    string.IsNullOrEmpty(desc) ? T("RestPolicy") : desc.ToString(),
                    UITheme.FontBody, UITheme.Muted, TextAnchor.MiddleLeft);
            }
            y += RestPolicyRowH;

            // ---- 三档按钮（从右往左）----
            float btnW = 68f;
            float bx = rect.xMax - btnW;
            for (int i = options.Count - 1; i >= 0; i--)
            {
                WorkshopRestPolicyDef def = options[i];
                var btn = new Rect(bx, y, btnW, RestPolicyRowH);
                if (UIComponents.ThemedButton(btn, RestPolicyLabelOf(def)))
                {
                    ApplyRestPolicy(def);
                }
                if (i == currentIndex)
                {
                    Widgets.DrawBoxSolidWithOutline(btn,
                        UITheme.WithAlpha(UITheme.Accent, 0.18f), UITheme.Accent, 2);
                }
                bx -= btnW + Gap;
            }
            y += RestPolicyRowH;

            // ---- RotateDaily 警示（GOV-010 强制：不得静默启用会翻转睡眠的档位）----
            if (current != null && WorkshopShiftPolicy.FlipsSleepAcrossDays(current.policy))
            {
                string warn = current.warningKey.Translate();
                if (string.IsNullOrEmpty(warn))
                {
                    warn = T("RestPolicyRotateWarning");
                }
                UIComponents.Label(new Rect(rect.x, y, rect.width, RestPolicyRowH),
                    warn.ToString(), UITheme.FontBody, UITheme.Warn, TextAnchor.MiddleLeft);
                y += RestPolicyRowH;
            }
            return y - rect.y;
        }

        /// <summary>档位按钮文本（Def 的 labelKey；缺失时回落到稳定 ID 文本，绝不抛异常）。</summary>
        private string RestPolicyLabelOf(WorkshopRestPolicyDef def)
        {
            if (def == null)
            {
                return "-";
            }
            string label = def.labelKey.Translate();
            return string.IsNullOrEmpty(label) ? def.defName : label.ToString();
        }

        /// <summary>
        /// UI 只发命令：写 Def 名 → 重算调度计划 → 重写作息表 → 刷新快照。
        /// </summary>
        private void ApplyRestPolicy(WorkshopRestPolicyDef def)
        {
            if (def == null || zone == null)
            {
                return;
            }
            zone.RestPolicyDefName = def.defName;
            // 岗位→班次固定绑定在策略切换后可能失效（班组指派变了），清掉让 Domain 重算。
            ClearCrewFixedShiftBindings();
            Manager?.MarkDutyPlanDirty();
            Manager?.SyncVanillaTimetables(zone);
            Refresh();
        }

        /// <summary>清掉本车间所有班组的固定绑定（策略切换时调用；-1 = 让 Domain 重算）。</summary>
        private void ClearCrewFixedShiftBindings()
        {
            if (zone == null)
            {
                return;
            }
            List<WorkshopCrew> crews = zone.Crews;
            for (int i = 0; i < crews.Count; i++)
            {
                if (crews[i] != null)
                {
                    crews[i].FixedShiftId = -1;
                }
            }
        }

        private static int IndexOf(List<WorkshopRestPolicyDef> list, WorkshopRestPolicyDef def)
        {
            if (list == null || def == null)
            {
                return -1;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].defName == def.defName)
                {
                    return i;
                }
            }
            return -1;
        }
        /// <summary>
        /// UI 可选的作息策略档位（<c>selectable</c> 且 Def 存在）。
        /// <para><b>排序</b>：按 Def 的 <c>order</c> 升序 ⇒ Def 文件里的顺序即 UI 顺序；
        /// 缺失时按 defName 稳定排序兜底（GOV-009：不依赖 XML 顺序以外的任何隐式约定）。</para>
        /// </summary>
        private static List<WorkshopRestPolicyDef> SelectableRestPolicies()
        {
            var result = new List<WorkshopRestPolicyDef>();
            List<WorkshopRestPolicyDef> all = DefDatabase<WorkshopRestPolicyDef>.AllDefsListForReading;
            if (all == null)
            {
                return result;
            }
            for (int i = 0; i < all.Count; i++)
            {
                WorkshopRestPolicyDef def = all[i];
                if (def != null && def.selectable)
                {
                    result.Add(def);
                }
            }
            result.Sort((a, b) => string.CompareOrdinal(a.defName, b.defName));
            return result;
        }
    }
}
