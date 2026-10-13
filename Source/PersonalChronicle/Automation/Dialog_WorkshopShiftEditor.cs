using System;
using System.Collections.Generic;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 班次编辑器（2026-10-02 精简）：一个班次的属性只有三项 —— <b>时段</b>、<b>岗位</b>、<b>优先级</b>。
    /// （历史名「班组设置」已随领域语义修正更名：WorkshopShift=班次，WorkshopCrew=班组。）
    ///
    /// <para><b>为什么撤下「名额 / 技能门槛 / 候选池」</b>（用户实测结论：「名额、门槛等等无作用」）：
    /// 这三项与「班组 + 倒班表」的派工主机制职责重叠 —— 谁上哪个班由 <b>班组编制</b> 与
    /// <c>WorkshopCrewCycle</c> 倒班表决定，再叠一层名额 / 门槛 / 池只会让行为不直观。
    /// 撤下后，班次回到纯粹的「作业窗口」声明：几点开工、干什么岗位、同时段冲突时谁优先。</para>
    ///
    /// <para><b>数据兼容</b>：<see cref="WorkshopShift"/> 的 <c>RequiredCount</c> /
    /// <c>RequiredSkill</c> / <c>MinSkillLevel</c> / <c>Pool</c> 字段<b>保留序列化</b>
    /// （旧档照常读取、不做 Migration）；<b>新建班次一律使用默认值</b>
    /// （无门槛 / 回落车间名单）。
    ///
    /// <para><b>2026-10-03 语义修正（用户实机 P0）</b>：名额字段的<b>缺省值语义</b>此前是
    /// "不限名额 = 全部合格者上岗"（见 <c>WorkshopShiftPlanner.SelectDutyIndexes</c>）
    /// ⇒ <b>每个班次都把整份名单吃光</b>，于是「日班 / 夜班」名义上分开了人、实际同一批人
    /// 全时段在岗，表现为 <b>"到点不下班、不回床"</b>。现在缺省按
    /// <c>WorkshopShiftPlanner.DefaultHeadcount</c>（1 人）办理，一个班次一个人；
    /// 这与玩家显式做出的三班倒意图一致。</para>
    ///
    /// <para>⚠ <b>CHG-20261005-WSDOCFIX（注释与实现对齐）</b>：原文写"名额 UI <b>不恢复</b>…
    /// 当前无 UI 入口"，与实现<b>矛盾</b> —— 本类 <c>DrawStepperRow</c> 区段（②bis）已于
    /// 2026-10-03 <b>补回「上岗名额」行</b>，且缺省显示**实际生效值**（0 → 1，否则玩家看到 "0"
    /// 会以为没人）；窗口高 490 亦按含该行计算。以实现为准，更新本注释。</para>
    ///
    /// <para>编辑即写回对象字段（本窗口是"即时生效"编辑器），每次改动都通知宿主刷新调度计划
    /// （<c>onChanged</c>）。</para>
    /// </summary>
    internal sealed class Dialog_WorkshopShiftEditor : Window
    {
        private const string KeyPrefix = "PersonalChronicle.Automation.Tab.";
        private const float RowH = 30f;

        /// <summary><see cref="WorkshopTaskType"/> 枚举成员数（命名常量：避免 "% 4" 与枚举脱节）。</summary>
        private const int TaskTypeCount = 4;
        private const float Gap = 8f;

        private readonly Zone_Workshop zone;
        private readonly WorkshopShift shift;
        private readonly Action onChanged;

        /// <summary>是否为「新建」草稿 —— 关闭时未点「确定」则回滚删除（2026-10-04 修复）。</summary>
        private readonly bool isNew;

        /// <summary>玩家是否点过「确定」；<c>false</c> 且 <see cref="isNew"/> ⇒ 关闭即回滚。</summary>
        private bool committed;

        // 高 490：标题 + 时段 + 优先级 + **名额** + 岗位 + 联动提示行
        //          + **班中餐三行 + 跟随全局行**（CHG-20261004-SHIFTMEAL）+ 底部按钮。
        // 2026-10-03：补回「上岗名额」行（RowH 30 + Gap 8 = 38，故 300 → 338）。
        // 2026-10-04：加班中餐 3 行 + 说明/清除行（4 × 38 = 152，故 338 → 490）。
        // ⚠ 本窗口 `resizeable = false` 且**无滚动视图** ⇒ 加行必须同步增高 InitialSize，
        //   否则底部按钮被裁掉（内容溢出不可滚动，这是固定高窗口的固有约束）。
        public override Vector2 InitialSize => new Vector2(540f, 490f);

        public Dialog_WorkshopShiftEditor(Zone_Workshop zone, WorkshopShift shift, Action onChanged,
            bool isNew = false)
        {
            this.zone = zone;
            this.shift = shift;
            this.onChanged = onChanged;
            this.isNew = isNew;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            forcePause = true;
            doCloseX = true;
            draggable = true;
            resizeable = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (shift == null || zone == null)
            {
                Close();
                return;
            }
            // 孤儿自愈（2026-10-02）：班次表页提供了「在编辑器之外删除班次」的入口 ⇒ 编辑器
            // 所持对象可能已被移除。此时继续编辑一个不在调度器里的班次毫无意义（且会误导玩家
            // 以为设置生效），直接关闭。
            MapComponent_WorkshopManager owner = Manager;
            if (owner != null && owner.Shifts != null && !owner.Shifts.Contains(shift))
            {
                Close();
                return;
            }

            float y = inRect.y;
            UIComponents.Label(new Rect(inRect.x, y, inRect.width, RowH),
                T("ShiftEditorTitle"), UITheme.FontValue, UITheme.Text);
            y += RowH + Gap;

            // ---- ① 时段（自定义；跨午夜 / 全天由 Dialog_TimeRange 与 WorkshopHours 判定）----
            Rect timeBtn = new Rect(inRect.x, y, inRect.width - 170f, RowH);
            if (UIComponents.ThemedButton(timeBtn, WorkshopHours.LabelOf(shift.StartHour, shift.EndHour)))
            {
                Find.WindowStack.Add(new Dialog_TimeRange(shift.StartHour, shift.EndHour, (s, e) =>
                {
                    shift.StartHour = s;
                    shift.EndHour = e;
                    NotifyChanged();
                }));
            }
            // ⚠ 2026-10-03（用户实机「到点了还卡在值勤、不下班」）：`StartHour == EndHour` 在
            //   `WorkshopTime.InRange` 里被解释为**全天**（直接 return true）⇒ 该班次覆盖 0–23 时，
            //   命中它的成员**永远不会收工**。这是"配置语义"而非 bug，但此前只用灰色 Muted 标签
            //   一笔带过，玩家完全看不出它意味着"无下班时间"。现在升级为警示色 + Tooltip 说明。
            bool allDay = shift.StartHour == shift.EndHour;
            string timeHint = allDay
                ? T("TimeAllDay")
                : (WorkshopHours.CrossesMidnight(shift.StartHour, shift.EndHour) ? T("TimeWrap") : string.Empty);
            Rect hintRect = new Rect(timeBtn.xMax + Gap, y, 162f, RowH);
            UIComponents.Label(hintRect, timeHint, UITheme.FontBody,
                allDay ? UITheme.Warn : UITheme.Muted);
            if (allDay)
            {
                TooltipHandler.TipRegion(new Rect(timeBtn.x, y, inRect.width - 170f, RowH),
                    T("TimeAllDayTip"));
            }
            y += RowH + Gap;

            // ---- ② 优先级（同时段多个班次冲突时，数字大者优先）----
            y = DrawStepperRow(inRect, y, T("ColPriority"), T1("PriorityMark", shift.Priority),
                () => { shift.Priority = (shift.Priority + 9) % 10; NotifyChanged(); },
                () => { shift.Priority = (shift.Priority + 1) % 10; NotifyChanged(); });

            // ---- ②bis 上岗名额（2026-10-03 补回）----
            //  为什么补回：2026-02 曾按"名额、门槛等等无作用"撤下，但 2026-10-03 的 P0 诊断
            //  证明**名额恰恰是三班倒的关键**——缺省 1 人/班才让"日班/夜班"真正分开
            //  （旧缺省"不限名额 = 全选"会把每个班次都撑成整份名单，见 DefaultHeadcount）。
            //  缺省展示必须显示**实际生效值**（0 → 缺省 1），否则玩家看到"0"会以为没人。
            string headcountText = shift.RequiredCount == 0
                ? T1("HeadcountDefault", PersonalChronicle.Domain.WorkshopShiftPlanner.DefaultHeadcount)
                : (shift.RequiredCount < 0
                    ? T("HeadcountUnlimited")
                    : T1("PriorityMark", shift.RequiredCount));
            y = DrawStepperRow(inRect, y, T("ColHeadcount"), headcountText,
                () => { shift.RequiredCount--; NotifyChanged(); },
                () => { shift.RequiredCount++; NotifyChanged(); });
            TooltipHandler.TipRegion(new Rect(inRect.x, y - RowH - Gap, inRect.width, RowH),
                T("HeadcountTip"));

            // ---- ③ 岗位 ----
            // 2026-10-02（用户需求「岗位 · 生产等工作不会自动勾选原版」）：
            // 由「点击循环切换」改为 FloatMenu —— ① 循环切换不可发现、易误改；② 玩家需要在这里
            // 看到"这个岗位会勾选哪些原版工作"。映射数据取自车间类型 Def（UI 不硬编码业务映射）。
            Rect taskRect = new Rect(inRect.x, y, inRect.width, RowH);
            if (UIComponents.ThemedButton(taskRect, T("ColTask") + " · " + WorkshopLoc.TaskLabel(shift.TaskType)))
            {
                OpenTaskTypeMenu();
            }
            y += RowH + 4f;

            // 联动说明行（选定岗位 ⇄ 原版「工作」面板自动勾选并提到最高档）。
            // 2026-10-02 审计（P1-J）：补预截断 —— 英文/长语言下这句会溢出窗口右缘。
            Rect syncHintRect = new Rect(inRect.x, y, inRect.width, 20f);
            UIComponents.Label(syncHintRect,
                UIComponents.TruncateToWidth(T("TaskSyncHint"), syncHintRect.width,
                    UITheme.FontBody, syncHintRect),
                UITheme.FontBody, UITheme.Muted);
            y += 24f;

            // ---- ④ 班中餐（CHG-20261004-SHIFTMEAL / CHG-20261005-MEALHOUR）----
            // 倒班制下白班与夜班的餐点本就不同 ⇒ 每条班次可配自己的一日三餐。
            // 每个餐点是**单个时刻**（0-23；-1 = 关闭该餐）；全局三餐设置已删除
            //（CHG-20261005-MEALBYSHIFT）⇒ 这里就是餐点的唯一来源。
            y = DrawShiftMealRow(inRect, y, T("MealBreakfast"), 0);
            y = DrawShiftMealRow(inRect, y, T("MealLunch"), 1);
            y = DrawShiftMealRow(inRect, y, T("MealDinner"), 2);

            // 说明 + 「全部清空」：同一行左右分栏（省一行高度，见 InitialSize 注释）。
            float clearW = 130f;
            Rect mealHintRect = new Rect(inRect.x, y, inRect.width - clearW - Gap, RowH);
            UIComponents.Label(mealHintRect,
                UIComponents.TruncateToWidth(T("ShiftMealHint"), mealHintRect.width,
                    UITheme.FontBody, mealHintRect),
                UITheme.FontBody, UITheme.Muted);
            if (UIComponents.ThemedButton(new Rect(inRect.xMax - clearW, y, clearW, RowH),
                T("ShiftMealClearAll")))
            {
                shift.ClearShiftMeal();
                NotifyChanged();
            }
            y += RowH + Gap;

            // ---- 底部 ----
            float footerY = inRect.yMax - RowH;
            float btnW = 120f;
            if (UIComponents.ThemedButton(new Rect(inRect.xMax - btnW, footerY, btnW, RowH), T("Ok")))
            {
                committed = true;   // 新建草稿的"落库"确认（回滚逻辑见 PostClose）
                Close();
            }
            if (UIComponents.ThemedButton(new Rect(inRect.xMax - btnW * 2f - Gap, footerY, btnW, RowH),
                T("ShiftDelete")))
            {
                // 2026-10-02 减法审计（一致性）：此前与「班次表」页的删除**规格不一致** ——
                // 那边走 destructive 二次确认（ConfirmDeleteShift），这边一点即删、不可撤销。
                // 删除还会连带解除各班组对它的显式指派（RemoveShift），故统一走确认框。
                // 复用既有键 ShiftDeleteConfirm（班次表页同款），不新增文案。
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    T1("ShiftDeleteConfirm", WorkshopHours.LabelOf(shift.StartHour, shift.EndHour)),
                    () =>
                    {
                        MapComponent_WorkshopManager manager = Manager;
                        if (manager != null)
                        {
                            manager.RemoveShift(shift);
                        }
                        Close();
                    },
                    true));
            }
        }

        /// <summary>
        /// 岗位选择菜单（2026-10-02）：列出 4 个岗位，并把该岗位在本车间类型下将联动的
        /// <b>原版工作名</b>附在项后（运行时取 <c>WorkTypeDef.LabelCap</c>，含第三方翻译，
        /// 本模组不复制文本 —— COMP-007/GOV-010）。映射唯一来源是
        /// <see cref="WorkshopTypeDef.WorkTypeDefNamesOf"/>，UI 不做任何业务映射硬编码。
        /// </summary>
        private void OpenTaskTypeMenu()
        {
            var options = new List<FloatMenuOption>();
            for (int i = 0; i < TaskTypeCount; i++)
            {
                WorkshopTaskType task = (WorkshopTaskType)i;
                string label = (task == shift.TaskType ? "✓ " : string.Empty) + WorkshopLoc.TaskLabel(task);
                string mapped = MappedWorkLabelOf(task);
                if (!string.IsNullOrEmpty(mapped))
                {
                    label += " · " + T1("TaskSyncTo", mapped);
                }
                WorkshopTaskType captured = task;
                options.Add(new FloatMenuOption(label, () =>
                {
                    shift.TaskType = captured;
                    NotifyChanged();
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options, T("ColTask")));
        }

        /// <summary>该岗位在本车间类型下将联动的原版工作名（空串 = 不联动）。</summary>
        private string MappedWorkLabelOf(WorkshopTaskType task)
        {
            WorkshopTypeDef typeDef = zone != null ? zone.WorkshopType : null;
            if (typeDef == null)
            {
                return string.Empty;
            }
            List<string> names = typeDef.WorkTypeDefNamesOf(task);
            var labels = new List<string>();
            for (int i = 0; i < names.Count; i++)
            {
                WorkTypeDef def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(names[i]);
                if (def != null)
                {
                    labels.Add(def.LabelCap.ToString());
                }
            }
            return labels.Count > 0 ? string.Join(" / ", labels) : string.Empty;
        }

        /// <summary>带 − / ＋ 的数值行（优先级用）。</summary>
        private float DrawStepperRow(Rect inRect, float y, string label, string value,
            Action onMinus, Action onPlus)
        {
            const float stepW = 30f;
            UIComponents.Label(new Rect(inRect.x, y, 120f, RowH), label, UITheme.FontBody, UITheme.Muted);
            if (UIComponents.ThemedButton(new Rect(inRect.x + 124f, y, stepW, RowH), "-"))
            {
                onMinus();
            }
            UIComponents.Label(new Rect(inRect.x + 124f + stepW, y, inRect.width - 124f - stepW * 2f, RowH),
                value, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleCenter);
            if (UIComponents.ThemedButton(new Rect(inRect.xMax - stepW, y, stepW, RowH), "+"))
            {
                onPlus();
            }
            return y + RowH + Gap;
        }

        private MapComponent_WorkshopManager Manager
        {
            get
            {
                return zone != null && zone.Map != null
                    ? MapComponent_WorkshopManager.Get(zone.Map)
                    : null;
            }
        }

        /// <summary>
        /// 关闭后处理：**新建草稿未确认 ⇒ 回滚**。
        ///
        /// <para>2026-10-04 修复（玩家实测）：「只要单击了添加班次，弹窗取消还是会加入一个
        /// 未设置的班次」。根因：新建路径<b>先 <c>AddShift</c> 再开窗</b>（<c>OpenNewShiftEditor</c>
        /// 的注释甚至写着"取消也不会有半成品"，与实现不符），而取消没有任何回滚路径。</para>
        ///
        /// <para><b>为什么挂在 <c>PostClose</c></b>：窗口关闭入口很多（确定 / X / ESC /
        /// 点窗外 <c>closeOnClickedOutside</c> / 删除按钮），<c>PostClose</c> 是<b>唯一</b>
        /// 必经的收口点 —— 挂钩一处即覆盖全部路径（GOV-005）。</para>
        ///
        /// <para>回滚走既有 <c>RemoveShift</c>（它会连带解除各班组对该班次的显式指派，
        /// 不会留悬空引用）；若玩家在草稿里点了「删除」，RemoveShift 已执行过 ⇒ 这里幂等 no-op。</para>
        /// </summary>
        public override void PostClose()
        {
            base.PostClose();
            if (!isNew || committed)
            {
                return;
            }
            MapComponent_WorkshopManager manager = Manager;
            if (manager != null)
            {
                manager.RemoveShift(shift);
            }
            if (onChanged != null)
            {
                onChanged();   // 让班次表 / 班组卡立即反映"这个班次不存在"
            }
        }

        /// <summary>
        /// 一行「班中餐」：标签 + 时段按钮（点击开 <see cref="Dialog_TimeRange"/>）。
        /// <paramref name="slot"/>：0 = 早、1 = 午、2 = 晚。
        ///
        /// <para>CHG-20261005-MEALBYSHIFT：未设置（-1）时按钮显示「未设置」——
        /// 全局三餐设置已整节删除，餐点<b>唯一来源就是本班次配置</b>，
        /// 所以不再有"继承来的值"可显示（旧文案是「跟随全局（当前全局窗口）」）。</para>
        ///
        /// <para>⚠ 餐点还受一条硬约束：只会落在**本班次的工作时段**内
        /// （见 <c>WorkshopDutyCoordinator.AddMealHoursOfShift</c>）⇒ 把夜班的餐点配到白天
        /// 不会生效，也不会污染别人的作息。</para>
        /// </summary>
        private float DrawShiftMealRow(Rect inRect, float y, string label, int slot)
        {
            UIComponents.Label(new Rect(inRect.x, y, 120f, RowH), label, UITheme.FontBody, UITheme.Muted);
            int hour = ShiftMealHourOf(slot);
            // CHG-20261005-MEALHOUR：单点显示「07:00」；未设置 ⇒ 「未设置」。
            // ⚠ 格式化走 Dialog_HourPick.LabelOfHour（单一实现）—— 否则弹窗与行内可能显示不同格式。
            string text = hour >= 0 ? Dialog_HourPick.LabelOfHour(hour) : T("ShiftMealUnset");
            if (UIComponents.ThemedButton(new Rect(inRect.x + 124f, y, inRect.width - 124f, RowH), text))
            {
                // 单点选择器（CHG-20261005-MEALHOUR）：不再用"起止段"弹窗 ——
                // 玩家要求「三餐不要时间段，改为时间选择，选择单个时间」。
                // 沿用"起止段"会带来那一整套无关语义（起==止 / 跨天 / 全天），
                // 正是玩家实测困惑的来源。
                Find.WindowStack.Add(new Dialog_HourPick(hour, h =>
                {
                    SetShiftMealHour(slot, h);
                    NotifyChanged();
                }));
            }
            return y + RowH + Gap;
        }

        /// <summary>该餐的当前时间点（-1 = 该餐关闭）。CHG-20261005-MEALHOUR：单点模型。</summary>
        private int ShiftMealHourOf(int slot)
        {
            switch (slot)
            {
                case 0: return shift.MealBreakfastHour;
                case 1: return shift.MealLunchHour;
                default: return shift.MealDinnerHour;
            }
        }

        /// <summary>写入该餐的时间点（-1 = 关闭该餐）。</summary>
        private void SetShiftMealHour(int slot, int hour)
        {
            switch (slot)
            {
                case 0:
                    shift.MealBreakfastHour = hour;
                    break;
                case 1:
                    shift.MealLunchHour = hour;
                    break;
                default:
                    shift.MealDinnerHour = hour;
                    break;
            }
        }

        private void NotifyChanged()
        {
            MapComponent_WorkshopManager manager = Manager;
            if (manager != null)
            {
                manager.MarkDutyPlanDirty();
            }
            if (onChanged != null)
            {
                onChanged();
            }
        }

        private static string T(string suffix)
        {
            return (KeyPrefix + suffix).Translate().ToString();
        }

        /// <summary>带 1 个整型占位符的取词（int 重载，与 <see cref="T1(string,string)"/> 并存）。</summary>
        private static string T1(string suffix, int arg)
        {
            return (KeyPrefix + suffix).Translate(arg).ToString();
        }

        /// <summary>带 1 个字符串占位符的取词（岗位 ⇄ 原版工作映射展示用）。</summary>
        private static string T1(string suffix, string arg)
        {
            return (KeyPrefix + suffix).Translate(arg).ToString();
        }
    }
}
