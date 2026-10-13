using System;
using System.Collections.Generic;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    internal sealed partial class WorkshopManageView
    {
        // ---- 班次表页（2026-10-02 新增）------------------------------------------
        //
        // 用户需求：「增加一个班次表功能，支持删除、查看班次等等」。
        //
        // 背景：排班页的班组卡片已收敛为「一卡一班次」（同日修复），因此「本车间有哪些班次」
        // 这一全局视图必须独立成页 —— 本页是唯一能一览全班次并直接操作的地方。
        //
        // 交互（与排班页一致的事件注册顺序）：
        //   整行点击 = 打开班次编辑器（查看/编辑，编辑器即时生效）；
        //   行尾 [删除] = 二次确认后移除，并解除各班组对它的显式指派。
        //
        // 纪律：行文本每帧拼接 ⇒ 只对**可见行**拼串（UI-007 大列表降级）；
        //       命中人数走只读 Manager.FilledCountOfCached（绝不触发调度计划重建）。

        /// <summary>班次表列顺序的表头 Key 后缀（与 <see cref="ShiftColShares"/> 一一对应）。</summary>
        /// <remarks>
        /// 2026-10-02 精简（用户实测结论：「班次只需要岗位、优先级就行。名额、门槛等等无作用」）：
        /// 撤下「名额 / 技能门槛」两列 —— 这两个字段已从班次编辑器移除、不再可设，留列只会永远显示
        /// 「不限 / —」；「命中」列移入行悬停提示（保留运行时可见性，但不占列宽）。
        /// 表列现只声明班次本身的属性（时段 / 岗位 / 优先）+ 归属（指派班组）+ 操作。
        /// </remarks>
        private static readonly string[] ShiftColKeys =
        {
            "ColHours", "ColTask", "ColPriority", "ColCrews", "ColActions"
        };

        /// <summary>班次表列宽占比（最后一列「操作」吃剩余宽度）。</summary>
        private static readonly float[] ShiftColShares =
        {
            0.22f,  // 时段
            0.15f,  // 岗位
            0.10f,  // 优先
            0.33f,  // 指派班组
            0f      // 操作（CHG-20261005-TABLESORT：补 0 ⇒ 由 ColumnSplit 把余宽给它，
                    //              与原"操作吃余"的口径一致；缺这一项会让"班组"列意外吃余）
        };

        /// <summary>班次表列间留白（CHG-20261005-TABLESORT：与设备页同值，全工程表格统一切分口径）。</summary>
        private const float ShiftColGap = 4f;

        /// <summary>
        /// 班次表**列样式**（CHG-20261005-TABLESORT：与设备页共用同一套机制）。
        /// <para>班次表的列都是文本 ⇒ 全左对齐；末列是按钮区 ⇒ 样式不参与。</para>
        /// </summary>
        private static readonly RUIF.StatCol[] ShiftColStyles =
        {
            new RUIF.StatCol { Align = TextAnchor.MiddleLeft },   // 时段
            new RUIF.StatCol { Align = TextAnchor.MiddleLeft },   // 岗位
            new RUIF.StatCol { Align = TextAnchor.MiddleLeft },   // 优先
            new RUIF.StatCol { Align = TextAnchor.MiddleLeft },   // 指派班组
            new RUIF.StatCol { Align = TextAnchor.MiddleLeft }    // 操作
        };

        /// <summary>班次表列宽缓冲（绘制期零分配）。</summary>
        private readonly float[] shiftColWidths = new float[8];

        private const float ShiftTableHeadH = 22f;
        private const float ShiftDeleteBtnW = 60f;

        private Vector2 shiftTableScroll = Vector2.zero;

        /// <summary>表头文本按语言缓存（列名是 <c>T()</c> 取词，语言切换时整表作废）。</summary>
        private readonly string[] shiftHeaderCache = new string[ShiftColKeysLength];
        private string shiftHeaderLang;

        /// <summary>列数（由 <see cref="ShiftColKeys"/> 派生，避免平行数组各自写死上界）。</summary>
        private const int ShiftColKeysLength = 5;

        private void DrawShiftTable(Rect rect)
        {
            MapComponent_WorkshopManager manager = Manager;
            List<WorkshopShift> shifts = OwnShifts(manager);

            // ---- 标题行：区块标题 + 两个操作（从右往左）----
            Rect header = new Rect(rect.x, rect.y, rect.width, SectionHeaderH);
            float addW = MeasuredButtonW(T("AddShift"));
            float patternW = MeasuredButtonW(T("PatternApply"));
            DrawSectionTitle(new Rect(rect.x, header.y,
                Mathf.Max(80f, rect.width - addW - patternW - Gap * 3f), header.height), T("ShiftTable"));
            float rx = rect.xMax;
            if (SectionButton(header, ref rx, addW, T("AddShift")))
            {
                OpenNewShiftEditor(manager);
            }
            if (SectionButton(header, ref rx, patternW, T("PatternApply")))
            {
                OpenApplyPatternMenu(manager);
            }

            // ---- 说明行 ----
            float y = header.yMax;
            UIComponents.Label(new Rect(rect.x, y, rect.width, RowH), T("ShiftTableHint"),
                UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
            y += RowH;

            if (shifts.Count == 0)
            {
                UIComponents.EmptyState(new Rect(rect.x, y, rect.width, Mathf.Max(0f, rect.yMax - y)),
                    T("ShiftTableEmptyTitle"), T("ShiftTableEmptySub"));
                return;
            }

            // ---- 汇总 + 表头 + 行（整体滚动）----
            UIComponents.Label(new Rect(rect.x, y, rect.width, 20f),
                T1("ShiftTableSummary", shifts.Count), UITheme.FontLabel, UITheme.Muted,
                TextAnchor.MiddleLeft);
            y += 22f;

            Rect tableRect = new Rect(rect.x, y, rect.width, Mathf.Max(0f, rect.yMax - y));
            float bodyW = Mathf.Max(0f, tableRect.width - UITheme.ScrollbarThickness);
            EnsureShiftHeader();
            // 表头**冻结**在滚动区之上（与设备页同风格）：列名常驻，只有行滚动。
            UIComponents.TableHeader(new Rect(tableRect.x, tableRect.y, bodyW, ShiftTableHeadH),
                shiftHeaderCache, ShiftColShares, ShiftColStyles, ShiftColGap);
            Rect scrollRect = new Rect(tableRect.x, tableRect.y + ShiftTableHeadH, tableRect.width,
                Mathf.Max(0f, tableRect.height - ShiftTableHeadH));
            if (scrollRect.height <= 0f)
            {
                return;
            }
            Rect viewRect = new Rect(0f, 0f, bodyW, shifts.Count * RowH);
            Widgets.BeginScrollView(scrollRect, ref shiftTableScroll, viewRect);
            try
            {
                float scrollTop = shiftTableScroll.y;
                float scrollBottom = scrollTop + scrollRect.height;
                for (int i = 0; i < shifts.Count; i++)
                {
                    float ry = i * RowH;
                    // 可见行裁剪：不可见行既不拼串也不注册控件（UI-007）。
                    if (ry + RowH < scrollTop || ry > scrollBottom)
                    {
                        continue;
                    }
                    DrawShiftRow(new Rect(0f, ry, bodyW, RowH), shifts[i], i);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>表头文本按语言缓存（语言未变 ⇒ 零取词零分配）。</summary>
        private void EnsureShiftHeader()
        {
            string lang = LanguageDatabase.activeLanguage != null
                ? LanguageDatabase.activeLanguage.folderName : string.Empty;
            if (string.Equals(lang, shiftHeaderLang, StringComparison.Ordinal))
            {
                return;
            }
            shiftHeaderLang = lang;
            for (int i = 0; i < ShiftColKeys.Length; i++)
            {
                shiftHeaderCache[i] = T(ShiftColKeys[i]);
            }
        }

        /// <summary>单行班次。事件顺序：先注册行尾 [删除]（抢占），再注册整行点击。</summary>
        private void DrawShiftRow(Rect row, WorkshopShift shift, int index)
        {
            if (shift == null)
            {
                return;
            }
            UIComponents.ZebraRow(row, index, true);

            // CHG-20261005-TABLESORT：列宽改用 RUIF 的**统一切分**（与表头同源、扣同样的 gap）。
            // 原先行用 `shares[i] * row.width`（不扣 gap）而表头另有算法 ⇒ 两端列宽本就不同，
            // 越靠右差得越多（这是"表头与数据错位"的第二个来源）。
            float[] w = shiftColWidths;
            RUIF.TableFmt.ColumnSplit(row, ShiftColShares, ShiftColGap, w);
            float x = row.x;
            DrawShiftTimeCell(new Rect(x, row.y, w[0], row.height), shift);
            x += w[0] + ShiftColGap;
            Rect taskRect = new Rect(x, row.y, w[1], row.height);
            UIComponents.Label(taskRect,
                UIComponents.TruncateToWidth(WorkshopLoc.TaskLabel(shift.TaskType), taskRect.width,
                    UITheme.FontLabel, taskRect),
                UITheme.FontLabel, UITheme.Text, RUIF.TableFmt.ColumnAlignOf(ShiftColStyles, 1));
            x += w[1] + ShiftColGap;
            UIComponents.Label(new Rect(x, row.y, w[2], row.height), T1("PriorityMark", shift.Priority),
                UITheme.FontLabel, UITheme.Muted, RUIF.TableFmt.ColumnAlignOf(ShiftColStyles, 2));
            x += w[2] + ShiftColGap;
            // 预截断（2026-10-02 · UI-009）：班组名由玩家自定义、数量无界（"·"拼接），
            // 同行右侧还有删除按钮 —— 不截断必然压过去。
            Rect crewsRect = new Rect(x, row.y, w[3], row.height);
            UIComponents.Label(crewsRect,
                UIComponents.TruncateToWidth(CrewNamesOf(shift), crewsRect.width,
                    UITheme.FontLabel, crewsRect),
                UITheme.FontLabel, UITheme.Muted, RUIF.TableFmt.ColumnAlignOf(ShiftColStyles, 3));
            x += w[3] + ShiftColGap;

            // 行尾删除按钮：**先**注册（内层抢占），整行点击在后，二者不误触。
            // 宽度自适应操作列：窗口被缩窄时按钮不溢出压住「指派班组」列（UI 门 EXC-UI-2）。
            float actionW = row.xMax - x;
            float btnW = Mathf.Min(ShiftDeleteBtnW, Mathf.Max(24f, actionW - 8f));
            Rect del = new Rect(row.xMax - btnW - 4f, row.y + 3f, btnW, row.height - 6f);
            bool deleteClicked = UIComponents.ThemedButton(del, T("ShiftRowDelete"));

            bool rowClicked = Widgets.ButtonInvisible(row);
            TooltipHandler.TipRegion(row, new TipSignal(() => ShiftRowTooltip(shift), shift.GetHashCode()));

            if (deleteClicked)
            {
                ConfirmDeleteShift(shift);
            }
            else if (rowClicked)
            {
                Find.WindowStack.Add(new Dialog_WorkshopShiftEditor(zone, shift, OnRosterChanged));
            }
        }

        /// <summary>时段单元格：正在上班时左缘亮点（与排班页 chip 同一判定）。</summary>
        private void DrawShiftTimeCell(Rect cell, WorkshopShift shift)
        {
            float textX = cell.x;
            if (ShiftIsActiveNow(shift))
            {
                Color prev = GUI.color;
                GUI.color = UITheme.Alive;
                Widgets.DrawBoxSolid(new Rect(cell.x + 2f, cell.y + cell.height * 0.5f - 2.5f, 5f, 5f),
                    Color.white);
                GUI.color = prev;
                textX += 9f;
            }
            // 预截断（2026-10-02 · UI-009）：时段列窄于「08:00 – 16:00」时不再溢出压住右侧岗位列。
            Rect stampRect = new Rect(textX, cell.y, Mathf.Max(0f, cell.xMax - textX), cell.height);
            UIComponents.Label(stampRect,
                UIComponents.TruncateToWidth(WorkshopHours.LabelOf(shift.StartHour, shift.EndHour),
                    stampRect.width, UITheme.FontLabel, stampRect),
                UITheme.FontLabel, UITheme.Text, TextAnchor.MiddleLeft);
        }

        /// <summary>指派了该班次的班组名；无显式指派 ⇒ 「按倒班轮转」（参与倒班）或「—」。</summary>
        private string CrewNamesOf(WorkshopShift shift)
        {
            if (zone == null || shift == null)
            {
                return T("PerfNA");
            }
            List<WorkshopCrew> crews = zone.Crews;
            string names = null;
            for (int c = 0; c < crews.Count; c++)
            {
                WorkshopCrew crew = crews[c];
                if (crew == null || crew.AssignedShifts == null || !crew.AssignedShifts.Contains(shift))
                {
                    continue;
                }
                names = names == null ? crew.Name : names + " · " + crew.Name;
            }
            if (names != null)
            {
                return names;
            }
            return shift.UseCrewCycle ? T("ShiftNotAssigned") : T("PerfNA");
        }

        /// <summary>
        /// 行悬停提示：完整参数（命中人数 / 倒班序位 / 指派班组）+ 操作提示。延迟求值。
        /// 2026-10-02：表中撤下的「命中」移到这里；「技能门槛」一并移除（字段已不可设）。
        /// </summary>
        private string ShiftRowTooltip(WorkshopShift shift)
        {
            if (shift == null)
            {
                return string.Empty;
            }
            string text = WorkshopHours.LabelOf(shift.StartHour, shift.EndHour)
                + "\n" + T("ColTask") + " " + WorkshopLoc.TaskLabel(shift.TaskType)
                + "\n" + T1("ShiftHit", Manager != null ? Manager.FilledCountOfCached(shift) : 0)
                + "\nP" + shift.Priority;
            if (shift.UseCrewCycle)
            {
                // -1（未指定序位）按第 1 班显示，避免出现「倒班序位 0」这种无意义值。
                text += "\n" + T1("ShiftSlotLabel", Mathf.Max(1, shift.ShiftSlotIndex + 1));
            }
            // 班组列在窄窗口下可能被截断 ⇒ 提示里补全（UI 门 R3）。
            text += "\n" + T("ColCrews") + " " + CrewNamesOf(shift);
            return text + "\n" + T("ShiftTableHint");
        }

        /// <summary>删除班次：二次确认（destructive）后移除，并解除班组的显式指派。</summary>
        private void ConfirmDeleteShift(WorkshopShift shift)
        {
            if (shift == null)
            {
                return;
            }
            string label = WorkshopHours.LabelOf(shift.StartHour, shift.EndHour);
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                T1("ShiftDeleteConfirm", label),
                () =>
                {
                    Manager?.RemoveShift(shift);
                    OnRosterChanged();
                },
                true));
        }
    }
}
