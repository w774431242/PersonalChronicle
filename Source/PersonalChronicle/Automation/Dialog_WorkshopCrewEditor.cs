using System;
using System.Collections.Generic;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 班组编辑器：玩家在此设置班组的<b>名称</b>、<b>人员安排</b>（从车间名单挑成员入编）
    /// 与<b>班次安排</b>（勾选本车间已有班次，使本班组成员并入这些班次的候选池）。
    ///
    /// <para>与倒班表（<see cref="PersonalChronicle.Domain.WorkshopCrewCycle"/>）的关系：
    /// 这里勾选的班次是<b>显式指派</b>，会和全局倒班表轮转<b>叠加</b>，二者都满足者优先上岗；
    /// 不勾选则班组只按倒班表轮转。</para>
    /// </summary>
    internal sealed class Dialog_WorkshopCrewEditor : Window
    {
        private const float RowH = 30f;
        private const float Gap = 8f;
        private const float SectionHeaderH = 24f;
        private const int VisibleRows = 6;

        private readonly Zone_Workshop zone;
        private readonly WorkshopCrew crew;
        private readonly MapComponent_WorkshopManager manager;
        private readonly Action onChanged;

        private string nameBuf;
        private Vector2 memberScroll = Vector2.zero;
        private Vector2 shiftScroll = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(560f, 640f);

        public Dialog_WorkshopCrewEditor(Zone_Workshop zone, WorkshopCrew crew,
            MapComponent_WorkshopManager manager, Action onChanged)
        {
            this.zone = zone;
            this.crew = crew;
            this.manager = manager;
            this.onChanged = onChanged;
            nameBuf = crew != null ? crew.Name : "A";
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            forcePause = true;
            doCloseX = true;
            draggable = true;
            resizeable = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (crew == null || zone == null)
            {
                Close();
                return;
            }

            float y = inRect.y;
            UIComponents.Label(new Rect(inRect.x, y, inRect.width, RowH),
                T("CrewEditorTitle") + " · " + crew.Name, UITheme.FontValue, UITheme.Text);
            y += RowH + Gap;

            // ---- 班组名称（自定义）----
            UIComponents.Label(new Rect(inRect.x, y, inRect.width, RowH),
                T("CrewNameLabel"), UITheme.FontBody, UITheme.Muted);
            y += RowH;
            Rect nameRect = new Rect(inRect.x, y, inRect.width, RowH);
            nameBuf = Widgets.TextField(nameRect, nameBuf);
            y += RowH + Gap;

            // ---- 人员安排 ----
            y = DrawSectionHeader(inRect, y, T("CrewMembersLabel"));
            List<Pawn> candidates = zone.Members;
            int mCount = candidates != null ? candidates.Count : 0;
            float memberBody = mCount > 0 ? Mathf.Min(mCount, VisibleRows) * RowH : RowH;
            Rect mOut = new Rect(inRect.x, y, inRect.width, memberBody);
            Rect mView = new Rect(0f, 0f, mOut.width - UITheme.ScrollbarThickness, mCount * RowH);
            // 审计修复（2026-09-30）：出栈进 finally（成员行异常不得污染滚动栈）。
            Widgets.BeginScrollView(mOut, ref memberScroll, mView);
            try
            {
                for (int i = 0; i < mCount; i++)
                {
                    DrawMemberRow(new Rect(0f, i * RowH, mView.width, RowH), candidates[i]);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
            y += memberBody + Gap;

            // ---- 班次安排 ----
            y = DrawSectionHeader(inRect, y, T("CrewShiftsLabel"));
            List<WorkshopShift> shifts = ShiftsOfZone();
            int sCount = shifts.Count;
            if (sCount == 0)
            {
                UIComponents.Label(new Rect(inRect.x, y, inRect.width, RowH),
                    T("CrewNoShifts"), UITheme.FontBody, UITheme.Muted);
                y += RowH;
            }
            else
            {
                float shiftBody = Mathf.Min(sCount, VisibleRows) * RowH;
                Rect sOut = new Rect(inRect.x, y, inRect.width, shiftBody);
                Rect sView = new Rect(0f, 0f, sOut.width - UITheme.ScrollbarThickness, sCount * RowH);
                // 审计修复（2026-09-30）：出栈进 finally。
                Widgets.BeginScrollView(sOut, ref shiftScroll, sView);
                try
                {
                    for (int i = 0; i < sCount; i++)
                    {
                        DrawShiftRow(new Rect(0f, i * RowH, sView.width, RowH), shifts[i]);
                    }
                }
                finally
                {
                    Widgets.EndScrollView();
                }
                y += shiftBody;
            }
            y += Gap;

            // ---- 确定 ----
            Rect okRect = new Rect(inRect.x, inRect.yMax - RowH, inRect.width, RowH);
            if (UIComponents.ThemedButton(okRect, T("CrewEditorOk")))
            {
                crew.Name = nameBuf != null ? nameBuf.Trim() : crew.Name;
                NotifyChanged();
                Close();
            }
        }

        private float DrawSectionHeader(Rect inRect, float y, string title)
        {
            RUIF.Draw.Box(new Rect(inRect.x, y + (SectionHeaderH - 12f) / 2f, 4f, 12f), UITheme.Accent);
            UIComponents.Label(new Rect(inRect.x + 10f, y, inRect.width - 10f, SectionHeaderH),
                title, UITheme.FontLabel, UITheme.Text);
            return y + SectionHeaderH;
        }

        private void DrawMemberRow(Rect rect, Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }
            bool inCrew = crew.Members.Contains(pawn);
            UIComponents.ZebraRow(rect, 0);
            // 已安排班组的人员必须显示所在班组：本班组由按钮表达，其它班组以「 · 乙组」标签呈现。
            string tag = CrewTagOf(pawn);
            // 用户需求（2026-10-01）：名字 + 最强 2 技能 + 所在班组 tag 三段并排。
            float btnW = 72f;
            float rowW = rect.width - btnW - 10f;
            float nameW = rowW * 0.30f;
            float skillW = rowW * 0.42f;
            UIComponents.Label(new Rect(rect.x + 2f, rect.y, nameW, rect.height),
                pawn.LabelShortCap, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(rect.x + 2f + nameW, rect.y, skillW, rect.height),
                WorkshopPawnAccess.TopSkillsOf(pawn, 2), UITheme.FontLabel, UITheme.Muted,
                TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(rect.x + 2f + nameW + skillW, rect.y,
                    rowW - nameW - skillW, rect.height),
                string.IsNullOrEmpty(tag) ? string.Empty : "→ " + tag,
                UITheme.FontLabel, UITheme.Info, TextAnchor.MiddleRight);
            Rect btn = new Rect(rect.xMax - 72f, rect.y + 3f, 68f, rect.height - 6f);
            // 单一编制（2026-09-26 修订）：按钮语义以「是否还在别的班组」为准 ——
            //   · 在别的班组 ⇒ 「移入本组」（动作 = 其它组移出 + 编入本组，严格移动）
            //   · 只在本组     ⇒ 「移出」（动作 = 从本组移除，并顺带收敛脏数据）
            //   · 无班组       ⇒ 「加入」
            // ⚠ 旧实现先看 inCrew：脏数据（本组 + 他组并存）时会显示「移出」，玩家点下去
            //   结果只从本组退出、人仍留在原班组 —— 表现为「怎么分配都会被锁回原班组」。
            bool elsewhere = !string.IsNullOrEmpty(tag);
            string buttonText = elsewhere ? T("CrewMoveMember")
                : (inCrew ? T("CrewRemoveMember") : T("CrewAddMember"));
            if (UIComponents.ThemedButton(btn, buttonText))
            {
                if (elsewhere)
                {
                    zone.AssignPawnToCrew(crew, pawn);
                }
                else if (inCrew)
                {
                    crew.Remove(pawn);
                    zone.DedupeCrewMemberships();
                }
                else
                {
                    zone.AssignPawnToCrew(crew, pawn);
                }
                NotifyChanged();
            }
        }

        /// <summary>成员所属的其它班组名称（逗号分隔）；本班组不列入（由按钮表达）。</summary>
        private string CrewTagOf(Pawn pawn)
        {
            if (zone == null || zone.Crews == null)
            {
                return string.Empty;
            }
            var tags = new List<string>();
            for (int i = 0; i < zone.Crews.Count; i++)
            {
                WorkshopCrew c = zone.Crews[i];
                if (c == crew || c.Members == null || !c.Members.Contains(pawn))
                {
                    continue;
                }
                tags.Add(c.Name);
            }
            return string.Join(",", tags);
        }

        private void DrawShiftRow(Rect rect, WorkshopShift shift)
        {
            if (shift == null)
            {
                return;
            }
            bool assigned = crew.IsAssigned(shift);
            UIComponents.ZebraRow(rect, 0);

            // CHG-20261004-WORKTAB-SHIFT · 单元 B：单按钮「负责/移除」→ 恒定勾选框。
            // 玩家诉求是「按班次取消、勾选」：按钮的文案会随状态翻转，读起来像动作；
            // 勾选框的勾恒定在控件上，翻转只改变勾本身 ⇒ 状态与动作不再混读（UI-005）。
            const float boxW = 24f;
            const float padL = 4f;
            const float gapL = 6f;
            float textX = rect.x + padL + boxW + gapL;

            // 命中顺序（UI-005 /既有 WorkshopManageView.Shifts.cs:188-197 先例）：
            // 勾选框**先**注册、整行**后**注册 ⇒ 点勾选框不会落到行按钮上。
            // 二者用 OR 合并 ⇒ 点任意位置都是「翻转一次」，不会双触发。
            bool boxClicked = UIComponents.ThemedCheckbox(
                new Rect(rect.x + padL, rect.y, boxW, rect.height), assigned);
            bool rowClicked = Widgets.ButtonInvisible(rect);
            bool toggled = boxClicked || rowClicked;

            string summary = WorkshopHours.LabelOf(shift.StartHour, shift.EndHour)
                + " · " + (shift.RequiredCount <= 0 ? T("ShiftCountUnlimited") : T1("ShiftCount", shift.RequiredCount))
                + " · " + WorkshopLoc.TaskLabel(shift.TaskType);
            UIComponents.Label(new Rect(textX, rect.y, rect.xMax - textX - 6f, rect.height),
                summary, UITheme.FontBody, assigned ? UITheme.Text : UITheme.Muted,
                TextAnchor.MiddleLeft);

            // 提示串延迟求值（UI-006：不在每帧拼串）。
            TooltipHandler.TipRegion(rect, new TipSignal(() => T("CrewShiftAssignTip"), shift.GetHashCode()));

            if (!toggled)
            {
                return;
            }

            // 2026-10-02：走班组侧的双份同步方法（引用列表 + 持久化 ID 列表）。
            // 旧写法只改引用列表，而该列表已不再持久化 ⇒ 指派会随存档丢失。
            if (assigned)
            {
                crew.UnassignShift(shift);
            }
            else
            {
                crew.AssignShift(shift);
            }
            NotifyChanged();
        }

        private List<WorkshopShift> ShiftsOfZone()
        {
            var list = new List<WorkshopShift>();
            if (manager == null || manager.Shifts == null || zone == null)
            {
                return list;
            }
            List<WorkshopShift> all = manager.Shifts;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].TargetZone == zone)
                {
                    list.Add(all[i]);
                }
            }
            return list;
        }

        private void NotifyChanged()
        {
            onChanged?.Invoke();
        }

        private const string KeyPrefix = "PersonalChronicle.Automation.Tab.";

        private static string T(string suffix)
        {
            return (KeyPrefix + suffix).Translate().ToString();
        }

        private static string T1(string suffix, int arg)
        {
            return (KeyPrefix + suffix).Translate(arg).ToString();
        }
    }
}
