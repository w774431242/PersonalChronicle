using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Automation
{
    internal sealed partial class WorkshopManageView
    {
        // ---- 排班页（2026-10-01 重设计 · 方案 C：班组卡片看板）------------------
        //
        // 旧版三段文字列表（班组/班次/逐人绩效）实机判定「太复杂」。本版以**班组为中心**：
        //   每张卡 = 一个班组：成员头像条 + 今日班次 chips（轮值/指派/未轮 三态着色）+
        //   出勤/生产双迷你条；卡片点击 = 编辑班组，chip 点击 = 编辑班次，卡头 X = 删除班组。
        // 成员绩效明细收进**卡片悬停提示**（悬停才拼串，绘制路径零分配）。
        //
        // 领域语义不变：WorkshopCrew（编制）× WorkshopShift（时段+名额）× 倒班表轮转 +
        // 显式指派叠加（Domain/WorkshopCrewCycle）；绩效口径与考勤台账同源。
        private const float SectionHeaderH = 26f;
        private const int CardCols = 2;              // 卡片列数（1080 窗口下 2 列最稳）
        private const float CardGap = 10f;           // 卡片间距
        private const float CardHeaderH = 26f;       // 卡头（名 + 在编 + X）
        private const float AvatarSize = 22f;        // 成员行头像边长（成员行内）
        private const float ChipH = 22f;             // 班次 chip 高（固定一行一个）
        private const int MaxMemberRows = 4;         // 卡内成员明细最多 4 行（更多 "+N"，全量在悬停）
        private const float MemberRowH = 24f;        // 成员行高（放大：头像 22 + 上下呼吸）
        private const float CardPadY = 8f;           // 卡内纵向呼吸（≥ 令牌 SpaceXxs）
        private const float BarBlockH = 30f;         // 卡底双迷你条区块高
        private const float MemberRightW = 112f;     // 成员行右侧固定块（工时 52 + 产量 56 + 边距）

        // UI 态（不持久化）。
        private Vector2 cardsScroll = Vector2.zero;

        private void DrawSchedule(Rect rect)
        {
            MapComponent_WorkshopManager manager = Manager;
            List<WorkshopCrew> crews = zone != null ? zone.Crews : new List<WorkshopCrew>();
            List<WorkshopShift> shifts = OwnShifts(manager);

            // ---- 标题行：班组看板 + 三操作（从右往左）----
            Rect header = new Rect(rect.x, rect.y, rect.width, SectionHeaderH);
            float addCrewW = MeasuredButtonW(T("CrewAdd"));
            float addShiftW = MeasuredButtonW(T("AddShift"));
            float patternW = MeasuredButtonW(T("PatternApply"));
            DrawSectionTitle(new Rect(rect.x, header.y, Mathf.Max(80f, rect.width
                - addCrewW - addShiftW - patternW - Gap * 4f), header.height), T("SectionCrews"));
            float rx = rect.xMax;
            if (SectionButton(header, ref rx, addCrewW, T("CrewAdd")))
            {
                AddCrew(crews);
            }
            if (SectionButton(header, ref rx, addShiftW, T("AddShift")))
            {
                OpenNewShiftEditor(manager);
            }
            if (SectionButton(header, ref rx, patternW, T("PatternApply")))
            {
                OpenApplyPatternMenu(manager);
            }

            // ---- 汇总行：今日出勤/产量 + 目标步进（生产率分母，随 zone 持久化）----
            float summaryY = header.yMax;
            int today = GenDate.DaysPassed;
            int target = zone != null ? zone.PerMemberDailyTarget : 10;
            // 2026-10-02 减法审计：本页原在「汇总行」与「每张班组卡」各算一遍同样的两项
            // （PlannedTicksPerMember 遍历全部班次 + CollectCrewPerformance 遍历成员）——
            // 班组数 N 时每帧多算 N 次成员遍历。现每帧只算一次，卡片与汇总行共用同一份读数。
            crewPerfCache.Clear();
            long sumActual = 0L, sumPlanned = 0L;
            int sumProduced = 0;
            for (int c = 0; c < crews.Count; c++)
            {
                WorkshopCrew crew = crews[c];
                if (crew == null)
                {
                    continue;
                }
                CollectCrewPerformance(crew, today, out long a, out int p, out int hc);
                long perMember = PlannedTicksPerMember(crew, shifts, crews.Count);
                crewPerfCache[crew] = new[] { a, p, hc, perMember };
                sumPlanned += perMember * System.Math.Max(1, hc);
                sumActual += a;
                sumProduced += p;
            }
            Rect summaryRect = new Rect(rect.x, summaryY, rect.width - 200f, RowH);
            UIComponents.Label(summaryRect,
                T("PerfSummary").Translate(
                    TicksToHours(sumActual), TicksToHours(sumPlanned), sumProduced).ToString(),
                UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);
            float prx = rect.xMax;
            Rect plusBtn = new Rect(prx - 22f, summaryY + (RowH - 22f) * 0.5f, 22f, 22f);
            prx = plusBtn.x - Gap;
            Rect minusBtn = new Rect(prx - 22f, summaryY + (RowH - 22f) * 0.5f, 22f, 22f);
            prx = minusBtn.x - Gap;
            float targetW = Mathf.Max(120f, rect.width * 0.24f);
            Rect targetLabel = new Rect(prx - targetW, summaryY, targetW, RowH);
            UIComponents.Label(targetLabel, T1("PerfTarget", target),
                UITheme.FontBody, UITheme.Muted, TextAnchor.MiddleRight);
            if (zone != null)
            {
                if (UIComponents.ThemedButton(minusBtn, "-"))
                {
                    zone.PerMemberDailyTarget = Mathf.Max(1, zone.PerMemberDailyTarget - 1);
                }
                if (UIComponents.ThemedButton(plusBtn, "+"))
                {
                    zone.PerMemberDailyTarget = Mathf.Min(999, zone.PerMemberDailyTarget + 1);
                }
            }

            // ---- 未编入班组提示 + 一键平均编入（2026-10-01）----
            int unassigned = CountUnassignedMembers(crews);
            float hintH = 0f;
            if (unassigned > 0 && zone != null)
            {
                Rect hint = new Rect(rect.x, summaryY + RowH + 2f, rect.width, 22f);
                UIComponents.Panel(hint);
                // 预截断（2026-10-02 · UI-009）：右侧是固定的「一键平均编入」按钮。
                Rect hintTextRect = new Rect(hint.x + 8f, hint.y, hint.width - 116f, hint.height);
                UIComponents.Label(hintTextRect,
                    UIComponents.TruncateToWidth(T1("UnassignedHint", unassigned),
                        hintTextRect.width, UITheme.FontLabel, hintTextRect),
                    UITheme.FontLabel, UITheme.Info, TextAnchor.MiddleLeft);
                Rect assignBtn = new Rect(hint.xMax - 100f, hint.y + 2f, 96f, hint.height - 4f);
                if (UIComponents.ThemedButton(assignBtn, T("UnassignedAssign")))
                {
                    DistributeUnassigned(crews);
                }
                hintH = 26f;
            }

            // ---- 简单模式警示行（EXC-2026-013 的披露要求）----
            // 原版只在「手动优先级」模式下读取工作档位 ⇒ 简单模式时车间的"最高优先度"提升
            // 不会被引擎读取。设置页那行是常驻版；这里在玩家**实际操作排班**的页面再出现一次
            //（仅简单模式，不占位），避免"改完排班却不知为何没人去干活"。
            PlaySettings play = Find.PlaySettings;
            if (play == null || !play.useWorkPriorities)
            {
                DrawWorkPriorityNotice(new Rect(rect.x, summaryY + RowH + hintH, rect.width, WorkPrioRowH));
                hintH += WorkPrioRowH + 2f;
            }

            // ---- 作息策略行（CHG-20261003-WORKSHOP-SPLIT · 修成因 A 的玩家入口）----
            // 放在卡片网格**之上**：策略决定每个人的作息时段，是比"谁在哪张卡里"更前置的设置。
            float policyY = summaryY + RowH + hintH;
            float policyH = DrawRestPolicyRow(new Rect(rect.x, policyY, rect.width, 80f));

            // ---- 卡片网格（整体滚动；2 列）----
            float gridY = policyY + policyH + Gap;
            Rect gridRect = new Rect(rect.x, gridY, rect.width, Mathf.Max(0f, rect.yMax - gridY));
            if (crews.Count == 0)
            {
                UIComponents.EmptyState(gridRect, T("CrewEmptyTitle"), T("CrewEmptySub"));
                return;
            }

            List<int> kinds = TodayExceptionKinds(crews);
            // 单班组 ⇒ 占满整行（用户实机反馈：单卡半宽 + 周围大片空白 ⇒ 信息更显稀少）。
            int cols = crews.Count == 1 ? 1 : CardCols;
            float cardW = (gridRect.width - Gap * (cols - 1)) / cols;

            // 卡高随**成员数**变化 ⇒ 逐行取同行卡的最大高（行内对齐，行间紧凑）。
            int rows = (crews.Count + cols - 1) / cols;
            var cardHeights = new float[crews.Count];
            var rowHeights = new float[rows];
            for (int c = 0; c < crews.Count; c++)
            {
                cardHeights[c] = CardHeight(AliveMemberCount(crews[c]));
                int row = c / cols;
                rowHeights[row] = Mathf.Max(rowHeights[row], cardHeights[c]);
            }
            float gridH = 0f;
            for (int r = 0; r < rows; r++)
            {
                gridH += rowHeights[r] + CardGap;
            }

            Widgets.BeginScrollView(gridRect, ref cardsScroll,
                new Rect(0f, 0f, gridRect.width, gridH));
            try
            {
                float rowY = 0f;
                for (int r = 0; r < rows; r++)
                {
                    for (int i = 0; i < cols; i++)
                    {
                        int c = r * cols + i;
                        if (c >= crews.Count)
                        {
                            break;
                        }
                        Rect card = new Rect(i * (cardW + CardGap), rowY, cardW, rowHeights[r]);
                        DrawCrewCard(card, crews[c], c, crews.Count, shifts, kinds, today, target);
                    }
                    rowY += rowHeights[r] + CardGap;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>有效成员数（剔除 null/已销毁）—— 卡高计算用。</summary>
        private static int AliveMemberCount(WorkshopCrew crew)
        {
            int n = 0;
            if (crew?.Members != null)
            {
                for (int i = 0; i < crew.Members.Count; i++)
                {
                    Pawn p = crew.Members[i];
                    if (p != null && !p.Destroyed)
                    {
                        n++;
                    }
                }
            }
            return n;
        }

        /// <summary>卡高由内容行数决定：卡头 + 成员明细（≤4 行）+ 单个班次 chip + 双迷你条 + 内距。</summary>
        /// <remarks>
        /// 2026-10-02（用户需求「固定一个显示班次即可」）：chip 行数恒为 1，卡高只随成员数变化。
        /// </remarks>
        private static float CardHeight(int memberCount)
        {
            float memberH = memberCount <= 0
                ? MemberRowH
                : Mathf.Min(memberCount, MaxMemberRows) * MemberRowH
                    + (memberCount > MaxMemberRows ? 16f : 0f);
            return CardHeaderH + memberH + 6f + ChipH + BarBlockH + CardPadY * 2f;
        }

        /// <summary>
        /// 单张班组卡。命中检测顺序：chip/X（先）→ 卡体 ButtonInvisible（后）⇒
        /// 空白区点击开班组编辑器、chip 点击开班次编辑器，互不误触。
        /// </summary>
        private void DrawCrewCard(Rect card, WorkshopCrew crew, int crewIndex, int crewCount,
            List<WorkshopShift> shifts, List<int> kinds, int today, int target)
        {
            UIComponents.Card(card, UITheme.Info);

            // ---- 卡头：名 + 在编 + 删除 ----
            float innerX = card.x + 8f;
            float innerW = card.width - 16f;
            Rect headRect = new Rect(innerX, card.y + 4f, innerW - 26f, CardHeaderH - 8f);
            // 预截断（2026-10-02 · UI-009）：班组名可自定义，右侧紧邻 X 按钮（仅 4px 余量）。
            UIComponents.Label(headRect,
                UIComponents.TruncateToWidth(crew.Name + " · " + T1("CrewHeadcount", crew.Headcount),
                    headRect.width, UITheme.FontBody, headRect),
                UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft);
            Rect rm = new Rect(card.xMax - 30f, card.y + 5f, 24f, CardHeaderH - 10f);
            bool removeClicked = UIComponents.ThemedButton(rm, T("RemoveMark"));

            // ---- 成员明细行（头像 + 名字 + 当前活动 + 今日工时 + 产量；≤4 行，更多 "+N"）----
            // 实机反馈（2026-10-01）：头像条信息太少 —— 成员是这张卡的主角 ⇒ 逐人成行。
            float memberY = card.y + CardHeaderH + 2f;
            int aliveCount = 0;
            if (crew.Members != null)
            {
                for (int i = 0; i < crew.Members.Count; i++)
                {
                    Pawn member = crew.Members[i];
                    if (member == null || member.Destroyed)
                    {
                        continue;
                    }
                    aliveCount++;
                    if (aliveCount > MaxMemberRows)
                    {
                        continue;
                    }
                    Rect row = new Rect(innerX, memberY, innerW, MemberRowH);
                    UIComponents.ZebraRow(row, aliveCount - 1);
                    DrawMemberAvatar(new Rect(row.x, row.y + (MemberRowH - AvatarSize) * 0.5f,
                        AvatarSize, AvatarSize), member);
                    // 布局（2026-10-01 细化）：左区 = 头像 + 名/技能/活动（按剩余宽比例），
                    //   右区 = 固定 112px（工时 52 + 产量 56）—— 旧实现右块按 xMax 反推、
                    //   左区按全宽比例 ⇒ 窄卡上两区重叠（实机截图"0小时(今日)产量0"挤在一团）。
                    float listX = row.x + AvatarSize + 6f;
                    float listW = innerW - AvatarSize - 6f - MemberRightW;
                    float nameW = listW * 0.30f;
                    float skillW = listW * 0.38f;
                    float actW = listW - nameW - skillW;
                    WorkshopPawnPerformance perf = zone != null ? zone.Performance.Peek(member) : null;
                    long mActual = perf != null ? perf.ActualTicksOn(today) : 0L;
                    int mProduced = perf != null ? perf.ProducedOn(today) : 0;
                    // ⚠ 左区三列 + 右区两列全部**预截断**（2026-10-02 · UI-009）：
                    // 左区右缘正好等于右侧固定 112px 区块的左缘，任何超宽文本会直接压住
                    // 「0 小时 / 今日产量 0」—— 用户实机截图里"文字被挤兑"的主要形态之一。
                    Rect mNameRect = new Rect(listX, row.y, nameW, row.height);
                    UIComponents.Label(mNameRect,
                        UIComponents.TruncateToWidth(member.LabelShortCap, mNameRect.width,
                            UITheme.FontLabel, mNameRect),
                        UITheme.FontLabel, UITheme.Text, TextAnchor.MiddleLeft);
                    // 用户需求（2026-10-01）：显示 pawn 的 2 个最强技能（等级 + 热情标记）。
                    Rect mSkillRect = new Rect(listX + nameW, row.y, skillW, row.height);
                    UIComponents.Label(mSkillRect,
                        UIComponents.TruncateToWidth(WorkshopPawnAccess.TopSkillsOf(member, 2),
                            mSkillRect.width, UITheme.FontLabel, mSkillRect),
                        UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);
                    Rect mActRect = new Rect(listX + nameW + skillW, row.y, actW, row.height);
                    UIComponents.Label(mActRect,
                        UIComponents.TruncateToWidth(ActivityText(member), mActRect.width,
                            UITheme.FontLabel, mActRect),
                        UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);
                    Rect mHoursRect = new Rect(row.xMax - MemberRightW, row.y, 50f, row.height);
                    string mHoursText = (KeyPrefix + "RuntimeHours").Translate(
                        (mActual / (float)GenDate.TicksPerHour).ToString("0.#",
                            CultureInfo.InvariantCulture)).ToString();
                    UIComponents.Label(mHoursRect,
                        UIComponents.TruncateToWidth(mHoursText, mHoursRect.width,
                            UITheme.FontLabel, mHoursRect),
                        UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleRight);
                    Rect mProdRect = new Rect(row.xMax - 56f, row.y, 54f, row.height);
                    UIComponents.Label(mProdRect,
                        UIComponents.TruncateToWidth(T1("PerfProduced", mProduced),
                            mProdRect.width, UITheme.FontLabel, mProdRect),
                        UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleRight);
                    memberY += MemberRowH;
                }
                if (aliveCount > MaxMemberRows)
                {
                    Rect moreRect = new Rect(innerX, memberY, innerW, 16f);
                    UIComponents.Label(moreRect,
                        UIComponents.TruncateToWidth(
                            T1("MembersMore", aliveCount - MaxMemberRows)
                                + " · " + T("MembersHoverHint"),
                            moreRect.width, UITheme.FontLabel, moreRect),
                        UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
                    memberY += 16f;
                }
            }
            if (aliveCount == 0)
            {
                UIComponents.Label(new Rect(innerX, memberY, innerW, MemberRowH),
                    T("CrewNoMembers"), UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
                memberY += MemberRowH;
            }

            // ---- 今日班次 chip（固定一个）----
            // 用户实测反馈（2026-10-02）：旧实现画的是"本车间**全部**班次"，于是白班卡上会冒出
            // 夜班时段、且多班次时整排重复（≥4 个 chip）；现按班组语义固定只显示**一个**班次
            // （取值优先级见 PrimaryShiftOf）。全量班次仍可在 chip 悬停与班组设置里查。
            int slot = WorkshopCrewCycle.EffectiveShiftSlotOfCrew(
                today - (zone != null ? zone.CycleAnchorDay : 0),
                crewIndex, crewCount, shifts.Count, kinds);
            float chipY = memberY + 6f;
            WorkshopShift primary = PrimaryShiftOf(crew, shifts, slot);
            if (primary != null)
            {
                DrawShiftChip(new Rect(innerX, chipY, innerW, ChipH), crew, primary,
                    primary.UseCrewCycle && primary.ShiftSlotIndex == slot, shifts);
            }
            else if (shifts.Count == 0)
            {
                UIComponents.Label(new Rect(innerX, chipY, innerW, ChipH),
                    T("CardNoShifts"), UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
            }
            else
            {
                // 有班次但本班组当前无班可上（未指派 / 多班次尚未分派）⇒ 显式「休 + 指派」入口，
                // 不留空白（UI-008）。
                DrawRestChip(new Rect(innerX, chipY, innerW, ChipH), crew);
            }

            // ---- 双迷你条：出勤 / 生产（读本页共享读数，避免与汇总行重复计算）----
            float barY = card.yMax - BarBlockH - CardPadY;
            long[] crewRow = CrewPerfOf(crew, shifts, crewCount, today);
            long actual = crewRow[0];
            int produced = (int)crewRow[1];
            int headcount = (int)crewRow[2];
            long planned = crewRow[3] * System.Math.Max(1, headcount);
            float barW = (innerW - Gap) * 0.5f;
            DrawPerfBar(new Rect(innerX, barY, barW, 22f), actual, planned,
                T("PerfAttendance"), UITheme.Info);
            DrawPerfBar(new Rect(innerX + barW + Gap, barY, barW, 22f), produced,
                target * System.Math.Max(1, headcount), T("PerfProduction"), UITheme.Alive);

            // ---- 卡体点击（最后注册 ⇒ 不与 chip/X 抢事件）----
            if (Widgets.ButtonInvisible(card))
            {
                Find.WindowStack.Add(new Dialog_WorkshopCrewEditor(zone, crew, Manager, OnRosterChanged));
            }
            // 延迟求值（2026-10-02 · PERF-003）：旧写法把拼串放在参数里 ⇒ 每帧为每张卡每名成员
            // 拼 4 段文本（60fps 下每秒数千次小分配）。TipSignal 的 Func 重载只在真正显示时求值。
            TooltipHandler.TipRegion(new Rect(card.x, card.yMax - 26f, card.width, 26f),
                new TipSignal(() => CrewPerfTooltip(crew, today, planned, target),
                    crew != null ? crew.GetHashCode() : 0));

            if (removeClicked && zone != null)
            {
                // 2026-10-02 审计修复（P1-I）：删除班组会连带丢掉成员编制与班次指派、且不可撤销
                // —— 与「删除班次」同规格走二次确认（destructive），避免一次误点。
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    T("ConfirmRemoveCrew").Formatted(crew != null ? crew.Name : string.Empty),
                    () =>
                    {
                        zone.RemoveCrew(crew);
                        OnRosterChanged();
                    },
                    true));
            }
        }

        /// <summary>
        /// 班次 chip：状态着色（今日轮值 = Alive 填充 / 显式指派 = Accent 描边 / 未轮值 = 暗底），
        /// 文本 = 时段 + 命中/名额；点击 = 打开班次编辑器。
        /// </summary>
        private void DrawShiftChip(Rect chip, WorkshopCrew crew, WorkshopShift shift,
            bool onDuty, List<WorkshopShift> allShifts)
        {
            bool assigned = crew.AssignedShifts != null && crew.AssignedShifts.Contains(shift);
            Color fill = onDuty ? UITheme.WithAlpha(UITheme.Alive, 0.45f)
                : assigned ? UITheme.WithAlpha(UITheme.Accent, 0.18f)
                : UITheme.Panel;
            Color line = onDuty ? UITheme.Alive
                : assigned ? UITheme.Accent
                : UITheme.BorderSoft;
            Widgets.DrawBoxSolid(chip, fill);
            UIComponents.Border(chip, line);

            // 「正在上班」指示点：当前游戏小时落在班次窗口内 ⇒ 左缘亮点。
            // 判定收敛到 ShiftIsActiveNow（底层是工程唯一的时段判定 WorkshopHours.InRange）——
            // 旧实现是内联重复逻辑，且对"全天班"(start == end) 会恒判 false 而 InRange 判 true。
            bool now = ShiftIsActiveNow(shift);

            // 绘制路径用只读版本：绝不触发调度计划重建（PERF-003，2026-10-02）。
            int hit = Manager != null ? Manager.FilledCountOfCached(shift) : 0;
            string timeText = WorkshopHours.LabelOf(shift.StartHour, shift.EndHour);
            string countText = hit + "/" + (shift.RequiredCount <= 0
                ? T("UnlimitedMark")
                : shift.RequiredCount.ToString());
            float textX = chip.x + 4f;
            if (now)
            {
                Color prev = GUI.color;
                GUI.color = UITheme.Alive;
                Widgets.DrawBoxSolid(new Rect(chip.x + 3f, chip.y + ChipH * 0.5f - 2.5f, 5f, 5f),
                    Color.white);
                GUI.color = prev;
                textX += 8f;
            }
            // 预截断（2026-10-02 · UI-009）：chip 内左右两块之间的间隙只有 0.05×宽 − 4px
            //（窄卡上约 8px），任何一侧超宽都会立刻重叠。
            Rect chipTimeRect = new Rect(textX, chip.y, chip.width * 0.55f - (textX - chip.x), chip.height);
            UIComponents.Label(chipTimeRect,
                UIComponents.TruncateToWidth(timeText, chipTimeRect.width, UITheme.FontLabel, chipTimeRect),
                UITheme.FontLabel, onDuty ? UITheme.Text : UITheme.Muted, TextAnchor.MiddleLeft);
            Rect chipCountRect = new Rect(chip.xMax - chip.width * 0.4f - 4f, chip.y,
                chip.width * 0.4f, chip.height);
            UIComponents.Label(chipCountRect,
                UIComponents.TruncateToWidth(countText, chipCountRect.width, UITheme.FontLabel, chipCountRect),
                UITheme.FontLabel, onDuty ? UITheme.Text : UITheme.Muted, TextAnchor.MiddleRight);
            // 2026-10-02（用户反馈「班组卡每次加载都不会保存班次」）：卡片上的班次 chip 由
            // 「打开班次编辑器」改为「切换本班组的指派」—— 指派入口此前只藏在班组编辑器对话框里，
            // 玩家在排班页找不到"谁上哪个班"，而这恰恰是卡片要表达的信息。
            // 班次本身的编辑（时段 / 岗位）在「班次表」页。
            if (Widgets.ButtonInvisible(chip))
            {
                OpenCrewShiftMenu(crew);
            }
            // 同上：延迟求值，避免每帧拼提示串。
            TooltipHandler.TipRegion(chip,
                new TipSignal(() => ShiftTooltip(shift, hit, allShifts)
                    + "\n" + T("ChipClickAssignHint"),
                    shift != null ? shift.GetHashCode() : 0));
        }

        /// <summary>
        /// 班组绩效读数的<b>本页共享缓存</b>（班组 → [实际工时, 产量, 在编人数, 人均计划工时]）。
        ///
        /// <para>2026-10-02 减法审计：汇总行与每张班组卡原先<b>各算一遍</b>同样的两项
        /// （<c>PlannedTicksPerMember</c> 遍历全部班次 + <c>CollectCrewPerformance</c> 遍历成员），
        /// 班组数 N 时每帧多算 N 次成员遍历。现改为每帧算一次、两处共用。</para>
        /// </summary>
        private readonly Dictionary<WorkshopCrew, long[]> crewPerfCache
            = new Dictionary<WorkshopCrew, long[]>();

        /// <summary>
        /// 取该班组的绩效读数（见 <see cref="crewPerfCache"/>）；缓存未命中时现场计算 ——
        /// 保证任何异常路径（缓存未构建 / 班组为 null）都不会显示空白。
        /// </summary>
        private long[] CrewPerfOf(WorkshopCrew crew, List<WorkshopShift> shifts,
            int crewCount, int today)
        {
            long[] row;
            if (crew != null && crewPerfCache.TryGetValue(crew, out row))
            {
                return row;
            }
            CollectCrewPerformance(crew, today, out long a, out int p, out int hc);
            return new[] { a, p, hc, PlannedTicksPerMember(crew, shifts, crewCount) };
        }

        /// <summary>
        /// 该班组卡片应显示的**唯一**班次（2026-10-02 用户需求：「固定一个显示班次即可」）。
        ///
        /// <para>取值优先级：</para>
        /// <list type="number">
        /// <item><b>显式指派</b>（班组设置里勾选的班次，取列表首个且属于本车间）—— 这是玩家的
        /// 明确意图，最稳定，因此「白班卡永远显示白班」；</item>
        /// <item><b>今日轮值</b>（参与倒班且当日序位命中的班次）—— 未指派时按倒班表显示今日实际班次，
        /// 与倒班表/交接板同源；</item>
        /// <item>车间仅有一个班次时直接显示它（兜底，保证单班车间一卡一班）；</item>
        /// <item>其余（多班次 + 未指派 + 当日轮休）⇒ null（卡片显示「休」）。</item>
        /// </list>
        /// </summary>
        private WorkshopShift PrimaryShiftOf(WorkshopCrew crew, List<WorkshopShift> shifts, int slot)
        {
            if (shifts == null || shifts.Count == 0)
            {
                return null;
            }
            if (crew != null && crew.AssignedShifts != null)
            {
                for (int i = 0; i < crew.AssignedShifts.Count; i++)
                {
                    WorkshopShift assigned = crew.AssignedShifts[i];
                    if (assigned != null && (zone == null || assigned.TargetZone == zone))
                    {
                        return assigned;
                    }
                }
            }
            if (slot >= 0)
            {
                for (int i = 0; i < shifts.Count; i++)
                {
                    if (shifts[i] != null && shifts[i].UseCrewCycle && shifts[i].ShiftSlotIndex == slot)
                    {
                        return shifts[i];
                    }
                }
            }
            return shifts.Count == 1 ? shifts[0] : null;
        }

        /// <summary>
        /// 班组 ⇄ 班次指派菜单（2026-10-02）：列出本车间的全部班次，点击即切换该班组的指派。
        ///
        /// <para><b>为什么放在这里</b>：用户实机反馈「班组卡每次加载都不会保存班次」——
        /// 指派入口原先只在班组编辑器对话框里，排班页卡片是只读的；而"这个班组上哪个班"
        /// 恰恰是卡片要表达的核心信息。菜单勾选态直接反映 <see cref="WorkshopCrew.IsAssigned"/>，
        /// 写入走 <see cref="WorkshopCrew.AssignShift"/> / <see cref="WorkshopCrew.UnassignShift"/>
        /// （同步维护引用列表与持久化 ID 列表，保证存档不丢）。</para>
        /// </summary>
        private void OpenCrewShiftMenu(WorkshopCrew crew)
        {
            if (crew == null || zone == null)
            {
                return;
            }
            var options = new List<FloatMenuOption>();
            List<WorkshopShift> all = Manager != null ? Manager.Shifts : null;
            if (all == null)
            {
                Messages.Message(T("CrewNoShiftsToAssign"), MessageTypeDefOf.RejectInput, false);
                return;
            }
            for (int i = 0; i < all.Count; i++)
            {
                WorkshopShift shift = all[i];
                if (shift == null || shift.TargetZone != zone)
                {
                    continue;
                }
                bool assigned = crew.IsAssigned(shift);
                string label = (assigned ? "✓ " : string.Empty)
                    + WorkshopHours.LabelOf(shift.StartHour, shift.EndHour)
                    + " · " + WorkshopLoc.TaskLabel(shift.TaskType)
                    + (shift.RequiredCount > 0 ? " · " + T1("ShiftCount", shift.RequiredCount) : string.Empty);
                WorkshopShift captured = shift;
                options.Add(new FloatMenuOption(label, () =>
                {
                    if (crew.IsAssigned(captured))
                    {
                        crew.UnassignShift(captured);
                    }
                    else
                    {
                        crew.AssignShift(captured);
                    }
                    // 走统一失效入口：重算调度计划 + 同步原版时间表 / 工作面板 + 刷新快照。
                    OnRosterChanged();
                }));
            }
            if (options.Count == 0)
            {
                Messages.Message(T("CrewNoShiftsToAssign"), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Find.WindowStack.Add(new FloatMenu(options, crew.Name));
        }

        /// <summary>
        /// 当前游戏小时是否落在该班次窗口内（跨午夜按环绕判定，全天班恒为「在上班」）。
        /// 排班页 chip 与班次表行**共用**同一判定，避免两处各自实现导致漂移。
        /// </summary>
        private static bool ShiftIsActiveNow(WorkshopShift shift)
        {
            if (shift == null)
            {
                return false;
            }
            int hour = Find.TickManager != null
                ? GenDate.HourOfDay(Find.TickManager.TicksGame, 0f) : 0;
            return WorkshopHours.InRange(hour, shift.StartHour, shift.EndHour);
        }

        /// <summary>
        /// 「休」chip：本班组今日无班可上（未指派 + 轮休）。刻意<b>不可点击</b>（disabled 语义），
        /// 只用来说明"不是漏画"（UI-008 空态）；全量班次仍在兄弟 chip 的悬停提示里。
        /// </summary>
        private void DrawRestChip(Rect chip, WorkshopCrew crew)
        {
            // 2026-10-02（用户反馈「班组卡每次加载都不会保存班次」）：由「刻意不可点击」改为
            // 可点击 —— 多班次车间里"未指派"才是常态，若此处不可点、又无任何指引，
            // 玩家会一直看到「休」而找不到"谁上哪个班"的入口。
            bool hover = Mouse.IsOver(chip);
            Widgets.DrawBoxSolid(chip, hover ? UITheme.BorderSoft : UITheme.Panel);
            UIComponents.Border(chip, hover ? UITheme.Accent : UITheme.BorderSoft);
            string label = T("CrewRest") + " · " + T("CrewAssignShort");
            UIComponents.Label(new Rect(chip.x + 6f, chip.y, chip.width - 12f, chip.height),
                label, UITheme.FontLabel, hover ? UITheme.Text : UITheme.Dim, TextAnchor.MiddleLeft);
            if (Widgets.ButtonInvisible(chip))
            {
                OpenCrewShiftMenu(crew);
            }
            TooltipHandler.TipRegion(chip,
                new TipSignal(() => crew != null ? crew.Name + "\n" + T("ChipClickAssignHint") : label,
                    crew != null ? crew.GetHashCode() : 0));
        }

        /// <summary>
        /// 成员头像（原版 <c>PortraitsCache</c> 立绘）。失败静默跳过（单件异常隔离，不刷日志
        /// —— 与制造中心设备卡同纪律）；暗底 + 描边压住立绘浅色底（设备卡审计 S11 同修法）。
        ///
        /// <para><b>分帧预热</b>（2026-10-02 · PERF-003 修复）：<c>PortraitsCache.Get</c> 命中时只是
        /// 两次字典查找（便宜），<b>未命中</b>时是「新建 RenderTexture + 完整 pawn 渲染管线」（昂贵）。
        /// 而原版缓存条目在 <b>1 秒</b>未使用后即被清理、其 RT 回收到池中复用 ⇒ 重新打开排班页时
        /// 全部成员头像都是 miss，一次性提交 N 次立绘渲染正是「点击排班按钮卡约 1 秒」的主因。</para>
        ///
        /// <para>因此每帧最多为 <see cref="PortraitWarmPerFrame"/> 个<b>尚未预热</b>的头像触发渲染
        /// （其余本帧先画空底 + 描边，下一帧补齐 —— 几帧内全部就位，肉眼几乎不可见）；
        /// 已预热的头像仍<b>每帧</b>调用一次 <c>Get</c> 作心跳，避免被 1 秒淘汰回收。
        /// <b>不可</b>改为自持 RenderTexture 引用：原版会把它放回池中复用，跨帧持有即悬挂纹理。</para>
        /// </summary>
        private void DrawMemberAvatar(Rect rect, Pawn pawn)
        {
            Widgets.DrawBoxSolid(rect, UITheme.Window);
            if (IsPortraitWarm(pawn))
            {
                try
                {
                    RenderTexture portrait = PortraitsCache.Get(pawn,
                        new Vector2(rect.width, rect.height), Rot4.South,
                        Vector3.zero, 1f, true, true, true, true, null, null, false, null);
                    if (portrait != null)
                    {
                        Widgets.DrawTextureFitted(rect, portrait, 1f);
                    }
                }
                catch (Exception)
                {
                    // 头像失败不得影响整卡/整页（铁律：单件异常隔离）；不写日志以免每帧刷屏。
                }
            }
            UIComponents.Border(rect, UITheme.BorderSoft);
        }

        // ---- 头像分帧预热（2026-10-02 · PERF-003）--------------------------------

        /// <summary>每帧允许触发几次「未命中」的头像渲染（摊薄首帧尖峰；值小 ⇒ 渐显更平滑）。</summary>
        private const int PortraitWarmPerFrame = 2;

        /// <summary>已预热过的 pawn（UI 态，不持久化，随窗口销毁；死亡引用由 PurgeDeadMembers 清理）。</summary>
        private readonly HashSet<Pawn> warmedPortraits = new HashSet<Pawn>();
        private int portraitBudgetFrame = -1;
        private int portraitBudgetLeft;

        /// <summary>本帧是否应为该 pawn 提交立绘渲染（已预热者恒 true 且不占名额）。</summary>
        private bool IsPortraitWarm(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }
            if (Time.frameCount != portraitBudgetFrame)
            {
                portraitBudgetFrame = Time.frameCount;
                portraitBudgetLeft = PortraitWarmPerFrame;
            }
            if (warmedPortraits.Contains(pawn))
            {
                return true;
            }
            if (portraitBudgetLeft <= 0)
            {
                return false;
            }
            portraitBudgetLeft--;
            warmedPortraits.Add(pawn);
            return true;
        }

        // ---- 绩效聚合与绘制 --------------------------------------------------

        /// <summary>未编入任何班组的成员数（车间名单 − 各班组并集）。</summary>
        private int CountUnassignedMembers(List<WorkshopCrew> crews)
        {
            if (zone?.Members == null)
            {
                return 0;
            }
            int n = 0;
            for (int i = 0; i < zone.Members.Count; i++)
            {
                Pawn p = zone.Members[i];
                if (p == null || p.Destroyed || InAnyCrew(crews, p))
                {
                    continue;
                }
                n++;
            }
            return n;
        }

        /// <summary>把未编入班组的成员按轮转平均编入现有班组（走域级单一编制入口）。</summary>
        private void DistributeUnassigned(List<WorkshopCrew> crews)
        {
            if (zone == null || crews.Count == 0)
            {
                return;
            }
            int next = 0;
            for (int i = 0; i < zone.Members.Count; i++)
            {
                Pawn p = zone.Members[i];
                if (p == null || p.Destroyed || InAnyCrew(crews, p))
                {
                    continue;
                }
                zone.AssignPawnToCrew(crews[next % crews.Count], p);
                next++;
            }
            OnRosterChanged();
        }

        private static bool InAnyCrew(List<WorkshopCrew> crews, Pawn pawn)
        {
            for (int k = 0; k < crews.Count; k++)
            {
                if (crews[k]?.Members != null && crews[k].Members.Contains(pawn))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>班组当日绩效聚合（actual 人时 tick / 产量 / 有效编制）。</summary>
        private void CollectCrewPerformance(WorkshopCrew crew, int today,
            out long actual, out int produced, out int headcount)
        {
            actual = 0L;
            produced = 0;
            headcount = 0;
            if (crew?.Members == null || zone == null)
            {
                return;
            }
            for (int m = 0; m < crew.Members.Count; m++)
            {
                Pawn member = crew.Members[m];
                if (member == null || member.Destroyed)
                {
                    continue;
                }
                headcount++;
                WorkshopPawnPerformance row = zone.Performance.Peek(member);
                if (row != null)
                {
                    actual += row.ActualTicksOn(today);
                    produced += row.ProducedOn(today);
                }
            }
        }

        /// <summary>绩效迷你条：可见轨道（底 + 描边）+ 填充 + 右侧百分比；悬停 = 指标名。</summary>
        private static void DrawPerfBar(Rect rect, long numerator, long denominator, string label, Color color)
        {
            float share = denominator > 0L
                ? Mathf.Clamp01(numerator / (float)denominator) : 0f;
            // 实机修复（2026-10-01）：轨道在暗色卡上几乎不可见 ⇒ 画可见底 + 描边再填充。
            Rect bar = new Rect(rect.x, rect.y + (rect.height - 10f) * 0.5f, rect.width - 40f, 10f);
            Widgets.DrawBoxSolid(bar, UITheme.WithAlpha(UITheme.Window, 0.9f));
            UIComponents.Border(bar, UITheme.BorderSoft);
            if (share > 0f)
            {
                Color prev = GUI.color;
                GUI.color = color;
                Rect fill = new Rect(bar.x + 1f, bar.y + 1f,
                    (bar.width - 2f) * share, bar.height - 2f);
                Widgets.DrawBoxSolid(fill, Color.white);
                GUI.color = prev;
            }
            Rect pctRect = new Rect(bar.xMax + 4f, rect.y, 36f, rect.height);
            UIComponents.Label(pctRect, RateText(numerator, denominator),
                UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);
            TooltipHandler.TipRegion(rect, label);
        }

        /// <summary>班组悬停提示：成员级 出勤/产量 明细（悬停时才拼串）。</summary>
        private string CrewPerfTooltip(WorkshopCrew crew, int today, long planned, int target)
        {
            var lines = new List<string>();
            if (crew?.Members != null)
            {
                for (int i = 0; i < crew.Members.Count; i++)
                {
                    Pawn member = crew.Members[i];
                    if (member == null || member.Destroyed)
                    {
                        continue;
                    }
                    WorkshopPawnPerformance row = zone != null ? zone.Performance.Peek(member) : null;
                    long mActual = row != null ? row.ActualTicksOn(today) : 0L;
                    int mProduced = row != null ? row.ProducedOn(today) : 0;
                    lines.Add(member.LabelShortCap
                        + " · " + T("PerfAttendance") + " " + RateText(mActual, planned)
                        + " · " + T("PerfProduction") + " " + RateText(mProduced, target)
                        + " · " + T1("PerfProduced", mProduced));
                }
            }
            return lines.Count > 0 ? string.Join("\n", lines) : T1("CrewHeadcount", 0);
        }

        /// <summary>班次悬停提示（完整参数；命中/名额在 chip 上已有缩略）。</summary>
        /// <remarks>
        /// 2026-10-02：卡面固定只画一个班次 ⇒ 提示补一行**本车间全部班次**，让"其它班次"仍可发现。
        /// 纯悬停时拼接（<c>TipRegion</c> 的延迟求值），不在绘制热路径分配。
        /// </remarks>
        private string ShiftTooltip(WorkshopShift shift, int hit, List<WorkshopShift> allShifts)
        {
            string text = WorkshopHours.LabelOf(shift.StartHour, shift.EndHour)
                + "\n" + T1("ShiftHit", hit)
                + (shift.RequiredCount > 0 ? " / " + T1("ShiftCount", shift.RequiredCount) : string.Empty)
                + "\n" + WorkshopLoc.TaskLabel(shift.TaskType)
                + "\nP" + shift.Priority;
            // 2026-10-02 配套可发现性（用户实机反馈「下班后无法离开车间」）：多班次车间里若没有
            // 任何班组勾选本班次、也没有自定义候选池 ⇒ 该班次空岗。必须显式告知玩家怎么修，
            // 否则表现只是"排了班却没人去"，无从排查。
            if (Manager != null && !Manager.HasResolvablePool(shift))
            {
                text += "\n" + T("ShiftNoPool");
            }
            if (allShifts != null && allShifts.Count > 1)
            {
                var labels = new List<string>();
                for (int i = 0; i < allShifts.Count; i++)
                {
                    if (allShifts[i] != null)
                    {
                        labels.Add(WorkshopHours.LabelOf(allShifts[i].StartHour, allShifts[i].EndHour));
                    }
                }
                text += "\n" + T1("CardShiftAll", string.Join(" / ", labels));
            }
            return text;
        }

        /// <summary>班组悬停提示：成员名单（悬停时才拼串）。</summary>
        private string CrewTooltip(WorkshopCrew crew)
        {
            if (crew.Members == null || crew.Members.Count == 0)
            {
                return T1("CrewHeadcount", 0);
            }
            var names = new List<string>();
            for (int i = 0; i < crew.Members.Count; i++)
            {
                Pawn p = crew.Members[i];
                if (p != null && !p.Destroyed)
                {
                    names.Add(p.LabelShortCap);
                }
            }
            return string.Join("\n", names);
        }

        private static float TicksToHours(long ticks)
        {
            return Mathf.RoundToInt(ticks / (float)GenDate.TicksPerHour);
        }

        /// <summary>当日各班组的例外（调休/顶班）—— 每帧最多构建一次（O(班组数)）。</summary>
        private readonly List<int> exceptionKindsCache = new List<int>();
        private int exceptionKindsFrame = -1;

        private List<int> TodayExceptionKinds(List<WorkshopCrew> crews)
        {
            if (exceptionKindsFrame == Time.frameCount && zone != null)
            {
                return exceptionKindsCache;
            }
            exceptionKindsFrame = Time.frameCount;
            exceptionKindsCache.Clear();
            int today = GenDate.DaysPassed;
            for (int i = 0; i < crews.Count; i++)
            {
                exceptionKindsCache.Add(zone != null ? zone.ExceptionKindOf(today, i)
                    : WorkshopCrewCycle.ExceptionNone);
            }
            return exceptionKindsCache;
        }

        /// <summary>新建班组：zone.AddCrew 按甲乙丙丁自动命名（随 Zone 持久化）。</summary>
        private void AddCrew(List<WorkshopCrew> crews)
        {
            if (zone == null)
            {
                return;
            }
            zone.AddCrew();
            OnRosterChanged();
        }

        /// <summary>
        /// 新建班次：先建对象（随调度器持久化）再打开编辑器；
        /// <b>取消（未点确定关闭）由编辑器的 <c>PostClose</c> 回滚删除</b>。
        ///
        /// <para>2026-10-04 修正：原注释声称"取消也不会有半成品"，但与实现不符 ——
        /// 新建路径确实先把对象加进了调度器，而取消没有任何回滚 ⇒ 玩家实测
        /// 「单击添加班次后取消，列表里照样多出一条未设置的班次」。回滚挂在编辑器
        /// <c>PostClose</c>（唯一必经收口点），故此处传 <c>isNew: true</c>。</para>
        /// </summary>
        private void OpenNewShiftEditor(MapComponent_WorkshopManager manager)
        {
            if (manager == null)
            {
                return;
            }
            // 2026-10-02 精简：不再预设「名额 1 人」—— 名额已从 UI 撤下，预设值会让新建班次
            // 只放行 1 人而玩家在界面上找不到原因。保持默认（0 = 不限人数）。
            var shift = new WorkshopShift
            {
                TargetZone = zone,
                StartHour = 6,
                EndHour = 18
            };
            manager.AddShift(shift);
            // isNew: true ⇒ 玩家未点「确定」就关闭时，编辑器 PostClose 回滚这次新增
            //（2026-10-04 修复"取消仍加入未设置班次"）。
            Find.WindowStack.Add(new Dialog_WorkshopShiftEditor(zone, shift, Refresh, isNew: true));
        }

        // ---- 班制（两班倒/三班倒）------------------------------------------------
        // 「预设班次」仅作用于「班组倒班」（企业化排班）：套用后生成参与倒班的班次（UseCrewCycle），
        // 并按「班次数 + 1」自动组建班组编制（甲/乙/丙/丁…）。

        private void OpenApplyPatternMenu(MapComponent_WorkshopManager manager)
        {
            if (manager == null)
            {
                return;
            }
            var options = new List<FloatMenuOption>();
            List<ShiftPattern> patterns = WorkshopShiftPatterns.All;
            for (int i = 0; i < patterns.Count; i++)
            {
                ShiftPattern pattern = patterns[i];
                // CHG-20261004-POSTGUIDE：披露副作用 —— ApplyPattern → EnsureCrewsForCycle 会在
                // 「本车间还没有班组」时自动建「班次数 + 1」个班组，**并把车间人员名单轮流编入**。
                // 玩家实测反馈"殖民者莫名添加到其他班组"，根因就是这里没有事前披露
                //（代码侧无自动编组路径：5 个写入口全在 UI 事件里，已核查）。
                string label = T("Pattern" + pattern.Key) + " · " + T1("PatternShifts", pattern.ShiftCount)
                    + "  " + T1("PatternAutoCrewHint", pattern.ShiftCount + 1);
                options.Add(new FloatMenuOption(label, () => ApplyPattern(manager, pattern)));
            }
            Find.WindowStack.Add(new FloatMenu(options, T("PatternApply")));
        }

        /// <summary>把班制铺成本车间的「班组倒班」班次，并自动组建班组编制。
        /// 追加而非替换：玩家可以先铺三班倒，再逐条微调。</summary>
        /// <remarks>
        /// 2026-10-02 精简：不再按「池人数 ÷ 班次数」预设名额 —— 名额已从班次编辑器与班次表撤下
        /// （用户实测结论「名额、门槛等等无作用」），预设值会形成玩家看不见、又确实生效的隐藏限制。
        /// 新建班次一律用默认值（不限名额）。
        /// </remarks>
        private void ApplyPattern(MapComponent_WorkshopManager manager, ShiftPattern pattern)
        {
            if (manager == null || pattern == null)
            {
                return;
            }
            EnsureCrewsForCycle(pattern);
            for (int i = 0; i < pattern.Windows.Count; i++)
            {
                ShiftWindow window = pattern.Windows[i];
                manager.AddShift(new WorkshopShift
                {
                    TargetZone = zone,
                    StartHour = window.StartHour,
                    EndHour = window.EndHour,
                    TaskType = WorkshopTaskType.Production,
                    UseCrewCycle = true,
                    ShiftSlotIndex = i
                });
            }
            OnRosterChanged();
        }

        /// <summary>组建班组编制（企业化排班）：车间还没有班组时，按「班次数 + 1」建组
        /// （= 四班三运转），并把车间人员名单轮流编入各班组。已有班组则不动编制。</summary>
        private void EnsureCrewsForCycle(ShiftPattern pattern)
        {
            if (pattern == null || zone == null || zone.Crews.Count > 0)
            {
                return;
            }
            int crewTarget = pattern.ShiftCount + 1;
            for (int i = 0; i < crewTarget; i++)
            {
                zone.AddCrew();
            }
            List<Pawn> roster = zone.Members;
            for (int i = 0; i < roster.Count; i++)
            {
                Pawn member = roster[i];
                if (member == null || member.Destroyed)
                {
                    continue;
                }
                List<WorkshopCrew> crews = zone.Crews;
                // AUDIT-2026-WSCREW-INVARIANT（P2 收口）：初编也走域级入口。
                zone.AssignPawnToCrew(crews[i % crews.Count], member);
            }
        }

        /// <summary>比率文本：分母 ≤ 0 显示 "—"（未指派班次/未设目标），否则取整百分比。</summary>
        private static string RateText(long numerator, long denominator)
        {
            if (denominator <= 0L)
            {
                return T("PerfNA");
            }
            int percent = (int)System.Math.Min(999L, numerator * 100L / denominator);
            return T1("PerfRate", percent);
        }

        /// <summary>按人计划人时：班组被指派班次时长之和；未指派 ⇒ 参与倒班班次总时长 ÷ 班组数平摊。</summary>
        private static long PlannedTicksPerMember(WorkshopCrew crew, List<WorkshopShift> shifts, int crewCount)
        {
            long planned = 0L;
            if (crew != null && crew.AssignedShifts != null)
            {
                for (int i = 0; i < crew.AssignedShifts.Count; i++)
                {
                    WorkshopShift shift = crew.AssignedShifts[i];
                    if (shift != null)
                    {
                        planned += ShiftTicksOf(shift);
                    }
                }
            }
            if (planned <= 0L && shifts != null && shifts.Count > 0 && crewCount > 0)
            {
                long total = 0L;
                for (int i = 0; i < shifts.Count; i++)
                {
                    if (shifts[i] != null && shifts[i].UseCrewCycle)
                    {
                        total += ShiftTicksOf(shifts[i]);
                    }
                }
                planned = total / crewCount;
            }
            return planned;
        }

        private static long ShiftTicksOf(WorkshopShift shift)
        {
            int hours = shift.EndHour - shift.StartHour;
            if (hours <= 0)
            {
                hours += 24;
            }
            return hours * GenDate.TicksPerHour;
        }

        /// <summary>
        /// 区块标题：accent 竖条 + 标题。与 UIComponents.DrawSubsectionHeader 同一视觉语言，
        /// 但不画通栏分隔线 —— 标题行右侧还要放操作按钮。
        /// </summary>
        private static void DrawSectionTitle(Rect rect, string title)
        {
            RUIF.Draw.Box(new Rect(rect.x, rect.y + (rect.height - 14f) / 2f, 4f, 14f), UITheme.Accent);
            UIComponents.Label(new Rect(rect.x + 10f, rect.y, Mathf.Max(0f, rect.width - 10f), rect.height),
                title, UITheme.FontLabel, UITheme.Text, TextAnchor.MiddleLeft);
        }

        /// <summary>区块标题行右侧按钮：在 rightX 左侧按给定宽度绘制（宽度实测缓存）。</summary>
        private static bool SectionButton(Rect header, ref float rightX, float w, string label)
        {
            var rect = new Rect(rightX - w, header.y, w, header.height);
            rightX = rect.x - Gap;
            return UIComponents.ThemedButton(rect, label);
        }

        // 审计修复（2026-09-30 · P2）：按 (文案, 语言) 缓存按钮宽 —— 语言切换 ⇒ 整表作废。
        private static readonly Dictionary<string, float> buttonWidthCache = new Dictionary<string, float>();
        private static string buttonWidthLang;

        private static float MeasuredButtonW(string label)
        {
            string lang = LanguageDatabase.activeLanguage != null
                ? LanguageDatabase.activeLanguage.folderName : string.Empty;
            if (!string.Equals(lang, buttonWidthLang, StringComparison.Ordinal))
            {
                buttonWidthCache.Clear();
                buttonWidthLang = lang;
            }
            if (string.IsNullOrEmpty(label))
            {
                return 56f;
            }
            float cached;
            if (buttonWidthCache.TryGetValue(label, out cached))
            {
                return cached;
            }
            GameFont prev = Verse.Text.Font;
            Verse.Text.Font = UITheme.FontBody;
            float w = Mathf.Max(56f, Verse.Text.CalcSize(label).x + 20f);
            Verse.Text.Font = prev;
            buttonWidthCache[label] = w;
            return w;
        }

        // 审计修复（2026-09-30 · P2）：本车间的班次清单**每帧最多重算一次**。
        private readonly List<WorkshopShift> ownShiftCache = new List<WorkshopShift>();
        private int ownShiftFrame = -1;

        private List<WorkshopShift> OwnShifts(MapComponent_WorkshopManager manager)
        {
            if (ownShiftFrame == Time.frameCount)
            {
                return ownShiftCache;
            }
            ownShiftFrame = Time.frameCount;
            var list = ownShiftCache;
            list.Clear();
            if (manager == null)
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
    }
}
