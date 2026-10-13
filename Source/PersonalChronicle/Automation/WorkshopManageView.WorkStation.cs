using System.Collections.Generic;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Automation;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间管理内容层 —— <b>车间专属工位 · L4 状态行</b>切片
    /// （`CHG-20261003-WORKSTATION` / EXC-2026-015）。
    ///
    /// <para><b>要解决的现象（用户诉求原文）</b>：「看不出到底有没有生效」。既有
    /// <c>DrawWorkPriorityNotice</c> 只有一行「手动/简单模式」提示，而
    /// <see cref="MapComponent_WorkshopManager"/> 里的 <c>WorkTypeIsDisabled</c> 守卫是
    /// <b>静默 continue</b> —— 玩家看到"已开启手动优先级"却不知道<b>到底对齐了几个人、
    /// 几个人被跳过了</b>。</para>
    ///
    /// <para><b>绘制纪律（PERF-002 / UI-003）</b>：状态文本在 <see cref="Refresh"/> 期算好
    /// 存入字段，绘制路径<b>只读字符串</b>，零翻译、零拼接、零字典查找。
    /// 取词走 <c>T()</c>（KeyPrefix + Translate），全部 Keyed 中英同步（GOV-010）。</para>
    ///
    /// <para><b>分层纪律（MATRIX-003）</b>：本切片只<b>读</b> <c>Manager</c> 的状态快照与
    /// <c>RestoreAllDemotedWork()</c> 命令入口，<b>不直接碰</b> <c>Pawn_WorkSettings</c>。</para>
    /// </summary>
    internal sealed partial class WorkshopManageView
    {
        /// <summary>状态行各分项（Refresh 期构建；供 tooltip 逐行展示，绘制路径零计算）。</summary>
        private readonly List<string> workStationStatusLines = new List<string>();

        /// <summary>状态行文本缓存（Refresh 期构建；绘制路径零计算）。</summary>
        private string workStationStatusText = string.Empty;

        /// <summary>状态行色调缓存（0 = 正常 / 1 = 警示）。</summary>
        private bool workStationStatusWarn;

        /// <summary>幽灵在岗人数（Refresh 期读取；&gt; 0 ⇒ 状态行转警示色）。</summary>
        private int workStationStatusGhosts;

        /// <summary>作息体检用的在编班组成员列表 + 去重集（Refresh 期复用，避免每帧分配）。</summary>
        private readonly List<Pawn> rosterMembers = new List<Pawn>();
        private readonly HashSet<Pawn> rosterSeen = new HashSet<Pawn>();

        /// <summary>
        /// 本车间<b>全部在编班组成员</b>（Refresh 期收集一次，供作息表体检逐人读数）。
        ///
        /// <para>为什么以「班组在编成员」为 PC 的作息管理边界（甲口径，用户 2026-10-03 裁决）：
        /// 这些人的作息<b>本来就是本系统在管</b>，覆写是应有行为；而**不在任何班组**的殖民者
        /// PC 永不碰其作息表（那是他自己排的）。该边界可由既有 <c>zone.Crews[].Members</c> 推导，
        /// 因此<b>不需要新增存档字段</b>来标记归属。</para>
        /// </summary>
        private List<Pawn> AllRosterMembers()
        {
            WorkshopPostAssignmentService.CollectRosterMembers(zone, rosterMembers, rosterSeen);
            return rosterMembers;
        }

        /// <summary>作息表 Work 小时异常（&gt;16h）的在编班组成员数（Refresh 期读取）。</summary>
        private int workStationStatusBroken;

        /// <summary>在编班组成员里，原版作息表 Work 小时的最大值（Refresh 期读取）。</summary>
        private int workStationStatusMaxWorkHours;

        /// <summary>
        /// 作息表 Work 小时数的**异常阈值**（GOV-006 登记：分类=作息健康阈值 /
        /// 来源="一个班次 8–12h，跨午夜 12h 封顶" / 生命周期=常量）。
        /// 超过它 ⇒ 睡眠被剥夺的嫌疑，转警示色。
        /// </summary>
        private const int AbnormalWorkHoursThreshold = 16;

        // ⚠ CHG-20261005-NODEMOTE：原 WorkRestoreBtnW（「一键全解除」按钮宽）已随按钮一并删除
        // —— 见 DrawWorkStationStatusRow 的注释（出口改为自动化：移出车间即恢复）。

        /// <summary>
        /// 构建状态行文本（<b>Refresh 期调用</b>，幂等）。
        ///
        /// <para>口径与数据源（全部来自 <c>MapComponent_WorkshopManager.LastWorkStationStatus</c>，
        /// 该快照由<b>同步事件</b>装配，绘制路径不重算）：</para>
        /// <list type="bullet">
        /// <item><b>已对齐 N 人</b> = <c>alignedPawns</c>（本次至少产生一处**提权**写入的人数）</item>
        /// <item><b>跳过 M 人</b> = <c>skippedPawns</c>（<c>WorkTypeIsDisabled</c>：技能缺失/背景禁用）</item>
        /// <item><b>在册 K 人</b> = <c>scopedPawns</c>（参与本次同步的班组成员数）</item>
        /// <para>⚠ CHG-20261005-NODEMOTE：原第三项是「台账中 K 项可解除」= <c>ledgerEntries</c>，
        /// 随降档通道一并移除，**不再显示**（<c>WorkStationStatus.ledgerEntries</c> 已无读取者）。</para>
        /// </list>
        /// </summary>
        private void BuildWorkStationStatusText()
        {
            MapComponent_WorkshopManager manager = Manager;
            if (manager == null)
            {
                workStationStatusText = T("WorkSyncNoManager");
                workStationStatusWarn = true;
                return;
            }
            MapComponent_WorkshopManager.WorkStationStatus st = manager.LastWorkStationStatus;
            // 刻意**不**新增多参数字符串拼接助手：4 个数字拼一条长文本会让翻译语序在
            // 中/英之间无法各自调整（GOV-010 占位符一致性）。改为每项一条独立 Keyed 行，
            // 每行只带 1 个占位符 —— 语序自由、行高统一、截断互不干扰。
            workStationStatusLines.Clear();
            // CHG-20261005-NODEMOTE：「已对齐」**保留**（= 本轮至少产生一处提权写入的人数，
            // 回答的是"到底生效没有"，与降档无关）；只去掉「台账 N 项」（那读的是降档台账）。
            workStationStatusLines.Add(T1("WorkSyncAligned", st.alignedPawns));
            workStationStatusLines.Add(T1("WorkSyncSkipped", st.skippedPawns));
            workStationStatusLines.Add(T1("WorkSyncScope", st.scopedPawns));
            // ⚠ 2026-10-03（用户实机「到点了还卡在值勤、不下班睡觉」）：把**在岗实况**也摆出来。
            //   「在岗 > 应到岗」= 有幽灵在岗（登记表与现实脱节，`TryGetDutyJob` 会因此让该人拿不到
            //   新值勤 Job）；两者不等就是"有人被班次窗口之外的路径拉进来了"。
            //   这是把 A/B/C 三种成因从"看不出来"变成"一眼可辨"的唯一读数（UI-009）。
            workStationStatusLines.Add(T1("DutyOnDuty", manager.ActiveWorkerCount));
            workStationStatusLines.Add(T1("DutyExpected", manager.DutyPlanCount));
            // 幽灵数 > 0 ⇒ 必须警示（它会让在岗 KPI 虚高、并阻断该人上岗）。
            workStationStatusGhosts = manager.GhostDutyCount;
            if (workStationStatusGhosts > 0)
            {
                workStationStatusLines.Add(T1("DutyGhost", workStationStatusGhosts));
            }
            // ⚠ 2026-10-03 · 作息表可观测性（本工程把班次写进**原版作息表**且它随存档持久化，
            //   一旦被写坏玩家只看到"他不睡觉"、看不到作息表被写成什么 ⇒ 修复只能靠推测。
            //   这里把「作息异常人数」与「最长 Work 小时」摆出来，让该类问题可自证。
            workStationStatusBroken = 0;
            workStationStatusMaxWorkHours = 0;
            List<Pawn> roster = AllRosterMembers();
            for (int i = 0; i < roster.Count; i++)
            {
                int workHours = MapComponent_WorkshopManager.WorkHoursOf(roster[i]);
                if (workHours > workStationStatusMaxWorkHours)
                {
                    workStationStatusMaxWorkHours = workHours;
                }
                if (workHours > AbnormalWorkHoursThreshold)
                {
                    workStationStatusBroken++;
                }
            }
            if (workStationStatusBroken > 0)
            {
                workStationStatusLines.Add(T1("RestBroken", workStationStatusBroken));
                workStationStatusLines.Add(T1("RestMaxHours", workStationStatusMaxWorkHours));
            }
            workStationStatusText = string.Join(" · ", workStationStatusLines);
            // 简单模式 = 降档通道整条跳过（写了引擎也不读）⇒ 必须醒目提示，否则玩家以为功能坏了。
            workStationStatusWarn = st.needsManualPriorities || workStationStatusGhosts > 0
                || workStationStatusBroken > 0;
        }

        /// <summary>
        /// 绘制「车间专属工位」**状态行**（只读读数；按钮已移除）。
        ///
        /// <para><b>⚠ CHG-20261005-NODEMOTE</b>：本行原还带一颗「一键全解除」按钮
        /// （降档通道的唯一出口）。降档经玩家裁定整体移除 ⇒ 按钮与 <c>WorkRestoreBtnW</c> 一并删除，
        /// 状态文本改为占满整行。**不要把它加回来** —— 出口现在是自动化的：
        /// 人员移出车间体系即自动恢复显示原版工作面板（见 <c>ReconcileAutoHideOnLeave</c>）。</para>
        ///
        /// <para>保留的读数：跳过 / 在册 / 在岗 / 应到岗（+ 幽灵 / 作息异常），
        /// 由 <see cref="BuildWorkStationStatusText"/> 在 Refresh 期装配。</para>
        /// </summary>
        private void DrawWorkStationStatusRow(Rect row)
        {
            // 预截断 + 截断补偿（UI-009）：Label **不**做截断，调用方必须先截断。
            // CHG-20261005-NODEMOTE：按钮已移除 ⇒ 状态文本占满整行（原为其预留 WorkRestoreBtnW+间距）。
            Rect textRect = new Rect(row.x, row.y, row.width, row.height);
            string text = workStationStatusText ?? string.Empty;
            UIComponents.Label(textRect,
                UIComponents.TruncateToWidth(text, textRect.width, UITheme.FontLabel, textRect),
                UITheme.FontLabel, workStationStatusWarn ? UITheme.Warn : UITheme.Muted,
                TextAnchor.MiddleLeft);

            // ⚠ CHG-20261005-NODEMOTE：**「一键全解除」按钮已随降档通道一并移除**
            // （2026-10-05 玩家批准 A 方案：出口改为自动化 —— 人员移出车间体系即自动恢复）。
            // 原实现：台账 > 0 时可点，调 RestoreAllDemotedWork()。降档链已删 ⇒ 无物可解除 ⇒ 删按钮。
            // 状态行本身保留（玩家仍关心"跳过几人 / 在册几人 / 在岗 / 应到岗"）。
            // tooltip 走 lambda 延迟求值（0x505249 = "PRI"，仅作 TipSignal 缓存键）。
            TooltipHandler.TipRegion(row, new TipSignal(() => T("WorkSyncHint"), 0x505249));
        }
    }
}